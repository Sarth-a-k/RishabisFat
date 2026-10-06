using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace FPCharacter.Editor
{
    [InitializeOnLoad]
    public static class FPCharacterValidation
    {
        const string RunningKey = "FPCharacter.BatchValidation";
        static FirstPersonCharacterAnimator driver;
        static GameObject instance;
        static double stageStarted;
        static int stage;
        static readonly List<string> runtimeChecks = new List<string>();
        static readonly List<string> runtimeErrors = new List<string>();
        static readonly List<string> editorWarnings = new List<string>();
        static Vector3 initialRoot;
        static Vector3 initialHand;
        static Quaternion initialHandRotation;
        static float standingUpperY;
        static float standingLowerMeshMaxY;
        static readonly List<HandSample> equipLeftSamples = new List<HandSample>();
        static bool capturedLeftSupport;
        static float equipLeftExcursion, equipLeftRotationExcursion;
        static bool testing;
        static Camera validationCamera;
        static string OutputDirectory => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Validation"));

        [Serializable] sealed class AssetReport
        {
            public string unityVersion;
            public string prefab;
            public int animators, skinnedRenderers, triangles, vertices, rigBones, sharedMaterials;
            public string materialShader;
            public string[] clips;
            public bool rootMotionDisabled, atlasAssigned, torchStartsHidden, legsStartVisible, spotlightStartsOff, spotlightEnabledOnEquip;
            public ClipContract[] clipContracts;
            public EndpointComparison[] endpointComparisons;
            public string[] errors;
        }
        [Serializable] sealed class ClipContract
        {
            public string name;
            public float expectedSeconds, importedSeconds;
            public bool passed;
        }
        [Serializable] sealed class EndpointComparison
        {
            public string description;
            public float maximumLocalPositionDifferenceMetres, maximumLocalRotationDifferenceDegrees, maximumLocalScaleDifference;
            public bool passed;
        }
        [Serializable] sealed class HandSample
        {
            public float clipSeconds;
            public Vector3 positionRelativeToUpperBody;
            public Quaternion rotationRelativeToUpperBody;
            public float shoulderScale;
        }
        struct LocalPose
        {
            public Vector3 position, scale;
            public Quaternion rotation;
        }
        [Serializable] sealed class RuntimeReport
        {
            public string unityVersion;
            public bool passed;
            public float standingLowerLegMeshMaxY;
            public float equipLeftExcursionMetres, equipLeftRotationExcursionDegrees;
            public HandSample[] equipLeftSamples;
            public string[] checks, errors, editorWarnings;
        }

        static FPCharacterValidation()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        [MenuItem("Tools/FP Character/Validate generated assets")]
        public static void ValidateAssets()
        {
            var errors = new List<string>();
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FPCharacterBuilder.PrefabPath);
            if (prefab == null) throw new InvalidOperationException("Build the FPCharacter prefab first.");
            var animator = prefab.GetComponentInChildren<Animator>();
            var control = animator.runtimeAnimatorController as AnimatorController;
            var renderers = prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var component = prefab.GetComponent<FirstPersonCharacterAnimator>();
            string[] clips = animator.runtimeAnimatorController.animationClips.Select(c => c.name).Distinct().OrderBy(n => n).ToArray();
            foreach (string clip in FPCharacterBuilder.RequiredClips) if (!clips.Contains(clip)) errors.Add("Missing clip " + clip);
            foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips.Distinct())
            {
                EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(clip);
                string branch = clip.name.StartsWith("Legs_", StringComparison.Ordinal) ? "pelvis" : "upper_body";
                if (!bindings.Any(binding => binding.path.Split('/').Contains(branch)))
                    errors.Add("No curve binding to " + branch + " in " + clip.name);
            }
            if (control == null || control.layers.Length != 2) errors.Add("Expected exactly two Animator layers.");
            if (prefab.GetComponentsInChildren<Animator>(true).Length != 1) errors.Add("Expected a single Animator.");
            if (renderers.Length != 3) errors.Add("Expected three skinned renderers: arms, below-knee legs, and flashlight.");
            if (component.torchRenderers == null || component.torchRenderers.Length == 0) errors.Add("Torch renderer is unassigned.");
            if (component.legRenderers == null || component.legRenderers.Length != 1 || component.legRenderers[0].name != "FP_LowerLegs") errors.Add("Separate FP_LowerLegs renderer is unassigned.");
            if (!renderers.Any(r => r.name == "FP_Arms") || !renderers.Any(r => r.name == "Flashlight")) errors.Add("Expected FP_Arms and Flashlight meshes.");
            if (component.upperBody == null) errors.Add("Upper body is unassigned.");
            int triangles = renderers.Sum(r => Enumerable.Range(0, r.sharedMesh.subMeshCount).Sum(s => (int)(r.sharedMesh.GetIndexCount(s) / 3)));
            bool atlas = renderers.All(r => r.sharedMaterials.All(m => m != null && (m.mainTexture != null || m.HasProperty("_BaseColorMap") && m.GetTexture("_BaseColorMap") != null)));
            if (!atlas) errors.Add("Atlas texture not assigned to every material.");
            if (animator.applyRootMotion) errors.Add("Root motion must be disabled.");
            bool hidden = component.torchRenderers != null && component.torchRenderers.All(r => r != null && !r.enabled);
            if (!hidden) errors.Add("Torch must start hidden.");
            bool legsVisible = component.legRenderers != null && component.legRenderers.Length > 0 && component.legRenderers.All(r => r != null && r.enabled);
            if (!legsVisible) errors.Add("Below-knee legs must start visible while standing.");
            bool spotReady = component.enableTorchLight && component.torchLight != null && component.torchLight.type == LightType.Spot && component.torchLight.shadows == LightShadows.None;
            if (!spotReady) errors.Add("Expected an enabled-on-equip, shadow-free spotlight.");
            AnimationClip[] importedClips = animator.runtimeAnimatorController.animationClips.Distinct().ToArray();
            ClipContract[] clipContracts = ValidateClipDurations(importedClips, errors);
            EndpointComparison[] endpointComparisons = ValidateUpperEndpoints(importedClips, errors);
            var report = new AssetReport {
                unityVersion = Application.unityVersion, prefab = FPCharacterBuilder.PrefabPath,
                animators = prefab.GetComponentsInChildren<Animator>(true).Length,
                skinnedRenderers = renderers.Length, triangles = triangles,
                vertices = renderers.Sum(r => r.sharedMesh.vertexCount), rigBones = renderers.SelectMany(r => r.bones).Distinct().Count(),
                sharedMaterials = renderers.SelectMany(r => r.sharedMaterials).Distinct().Count(),
                materialShader = renderers.First().sharedMaterial.shader.name,
                clips = clips, rootMotionDisabled = !animator.applyRootMotion,
                atlasAssigned = atlas, torchStartsHidden = hidden,
                legsStartVisible = legsVisible, spotlightStartsOff = component.torchLight != null && !component.torchLight.enabled,
                spotlightEnabledOnEquip = spotReady,
                clipContracts = clipContracts, endpointComparisons = endpointComparisons,
                errors = errors.ToArray()
            };
            Directory.CreateDirectory(OutputDirectory);
            File.WriteAllText(Path.Combine(OutputDirectory, "Unity_Asset_Validation.json"), JsonUtility.ToJson(report, true));
            if (errors.Count > 0) throw new InvalidOperationException(string.Join("; ", errors));
            Debug.Log("FPCharacter asset validation passed: " + triangles + " triangles, " + renderers.Length + " skinned renderers, " + clips.Length + " clips.");
        }

        // Invoke in a scratch Unity project with -batchmode -executeMethod and WITHOUT -quit.
        public static void BuildAndValidateBatch()
        {
            try
            {
                // This entry point is for the isolated validation project only.
                var validationScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorSceneManager.SaveScene(validationScene, "Assets/FPCharacterValidationScene.unity");
                FPCharacterBuilder.BuildBatch();
                SessionState.SetBool(RunningKey, true);
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorApplication.isPlaying = true;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (!SessionState.GetBool(RunningKey, false)) return;
            if (change == PlayModeStateChange.EnteredPlayMode)
            {
                testing = true;
                runtimeChecks.Clear(); runtimeErrors.Clear(); editorWarnings.Clear(); equipLeftSamples.Clear();
                capturedLeftSupport = false; equipLeftExcursion = 0f; equipLeftRotationExcursion = 0f;
                Application.logMessageReceived += CaptureLog;
                instance = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(FPCharacterBuilder.PrefabPath));
                driver = instance.GetComponent<FirstPersonCharacterAnimator>();
                driver.readToggleKey = false;
                initialRoot = instance.transform.position;
                if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null)
                {
                    validationCamera = new GameObject("ValidationCamera").AddComponent<Camera>();
                    validationCamera.transform.position = new Vector3(0f, 1.64f, -0.04f);
                    validationCamera.transform.rotation = Quaternion.Euler(2f, 0f, 0f);
                    validationCamera.fieldOfView = 45.75f;
                    validationCamera.nearClipPlane = 0.025f;
                    validationCamera.clearFlags = CameraClearFlags.SolidColor;
                    validationCamera.backgroundColor = new Color(0.10f, 0.12f, 0.15f);
                    driver.viewCamera = validationCamera.transform;
                    Light light = new GameObject("ValidationLight").AddComponent<Light>();
                    light.type = LightType.Directional;
                    light.intensity = 1.2f;
                    light.transform.rotation = Quaternion.Euler(38f, -25f, 0f);
                    RenderSettings.ambientMode = AmbientMode.Flat;
                    RenderSettings.ambientLight = new Color(0.4f, 0.43f, 0.48f);
                }
                stage = 0;
                stageStarted = EditorApplication.timeSinceStartup;
                EditorApplication.update += Tick;
            }
            else if (change == PlayModeStateChange.EnteredEditMode)
            {
                SessionState.SetBool(RunningKey, false);
                bool passed = File.Exists(Path.Combine(OutputDirectory, "Unity_Runtime_Validation.json"))
                    && File.ReadAllText(Path.Combine(OutputDirectory, "Unity_Runtime_Validation.json")).Contains("\"passed\": true");
                if (passed)
                {
                    AssetDatabase.ExportPackage(FPCharacterBuilder.AssetRoot, Path.Combine(OutputDirectory, "FP_Flashlight.unitypackage"), ExportPackageOptions.Recurse);
                    Debug.Log("FPCharacter: exported tested Unity package.");
                }
                EditorApplication.Exit(passed ? 0 : 1);
            }
        }

        static void CaptureLog(string message, string stackTrace, LogType type)
        {
            if (testing && (type == LogType.Exception || type == LogType.Error || type == LogType.Assert))
            {
                if (stackTrace.Contains("UnityEditor.Search.SearchDatabase") && !stackTrace.Contains("FPCharacter"))
                    editorWarnings.Add("Unity Editor search-index service (outside the player/asset): " + message);
                else runtimeErrors.Add(message + "\n" + stackTrace);
            }
        }

        static void Check(bool condition, string label)
        {
            if (!condition) runtimeErrors.Add("Failed: " + label);
            else runtimeChecks.Add(label);
        }

        static void Tick()
        {
            if (!testing || driver == null) return;
            double elapsed = EditorApplication.timeSinceStartup - stageStarted;
            try
            {
                if (stage == 3 || stage == 4) SampleLeftSupport();
                switch (stage)
                {
                    case 0:
                        if (elapsed < 0.35) return;
                        Check(driver.CurrentLegState == "Legs_Idle" && driver.CurrentArmState == "Arms_Idle", "Idle starts both layers in the natural pose");
                        Check(!driver.TorchVisible, "Torch starts invisible");
                        CheckActualStates("Legs_Idle", "Arms_Idle");
                        standingUpperY = driver.upperBody.position.y;
                        initialHand = FindBone("hand.R").position;
                        initialHandRotation = FindBone("hand.R").rotation;
                        CheckLegs(true, "Standing idle displays only the separate lower-leg mesh");
                        Check(!driver.torchLight.enabled, "Flashlight spotlight starts off while unequipped");
                        ValidateStandingLowerMesh();
                        Capture("Unity_Natural_Hands.png");
                        if (validationCamera != null) validationCamera.transform.rotation = Quaternion.Euler(75f, 0f, 0f);
                        stage = 100; stageStarted = EditorApplication.timeSinceStartup;
                        break;
                    case 100:
                        if (elapsed < 0.25) return;
                        Capture("Unity_Standing_LowerLegs.png");
                        CheckLegs(true, "Looking down while standing preserves shin and foot visibility");
                        if (validationCamera != null) validationCamera.transform.rotation = Quaternion.Euler(2f, 0f, 0f);
                        stage = 101; stageStarted = EditorApplication.timeSinceStartup;
                        break;
                    case 101:
                        if (elapsed < 0.75) return;
                        Check(Vector3.Distance(initialHand, FindBone("hand.R").position) > 0.0005f
                            || Quaternion.Angle(initialHandRotation, FindBone("hand.R").rotation) > 0.1f,
                            "Standing idle produces actual breathing or hand movement");
                        driver.SetMotionState(0.6f, false, false, true);
                        stage = 1; stageStarted = EditorApplication.timeSinceStartup;
                        break;
                    case 1:
                        if (elapsed < 0.4) return;
                        Check(driver.CurrentLegState == "Legs_Walk" && driver.CurrentArmState == "Arms_Walk", "Walk selects both matching clips");
                        CheckActualStates("Legs_Walk", "Arms_Walk");
                        CheckLegs(true, "Walking keeps lower legs visible");
                        driver.SetMotionState(1f, true, false, true);
                        Next(); break;
                    case 2:
                        if (elapsed < 0.4) return;
                        Check(driver.CurrentLegState == "Legs_Sprint" && driver.CurrentArmState == "Arms_Sprint", "Sprint selects both matching clips");
                        CheckActualStates("Legs_Sprint", "Arms_Sprint");
                        CheckLegs(true, "Sprinting keeps lower legs visible");
                        driver.SetTorchEquipped(true);
                        Next(); break;
                    case 3:
                        if (elapsed < 1.0) return;
                        Check(driver.CurrentArmState == "Torch_Equip" && !driver.TorchVisible, "Reach phase runs with torch hidden at 1.0 seconds");
                        Check(Vector3.Distance(initialHand, FindBone("hand.R").position) > 0.15f, "Equip actually displaces the right-hand bone");
                        Capture("Unity_Equip_Reach.png");
                        Next(); break;
                    case 4:
                        if (elapsed < 0.65) return;
                        Check(driver.CurrentArmState == "Torch_Equip" && driver.TorchVisible, "Torch becomes visible during the return phase");
                        ValidateLeftSupport();
                        Next(); break;
                    case 5:
                        if (elapsed < 0.8) return;
                        Check(driver.TorchEquipped && driver.CurrentArmState == "Torch_Sprint", "Equip completes into sprint while legs continue");
                        CheckGaitPhase("Torch locomotion resynchronizes with the continuing leg stride after equip");
                        Check(FindBone("shoulder.L").localScale.sqrMagnitude < 0.001f, "Equipped state hides the left-arm branch");
                        Check(driver.torchLight.enabled && driver.torchLight.type == LightType.Spot, "Equipping activates the flashlight spotlight");
                        Vector3 beamDirection = FindBone("torch_beam_target").position - FindBone("torch_tip").position;
                        Check(Vector3.Dot(beamDirection.normalized, driver.torchLight.transform.forward) > 0.99f, "Spotlight points along the flashlight beam marker");
                        if (validationCamera != null)
                        {
                            Vector3 gripInView = validationCamera.WorldToViewportPoint(FindBone("torch_socket").position);
                            Check(gripInView.z > 0f && gripInView.x > 0f && gripInView.x < 1f && gripInView.y > -0.15f && gripInView.y < 1f,
                                "Equipped flashlight grip remains in the first-person view after sustained camera-pitch updates");
                        }
                        Capture("Unity_Flashlight_Sprint.png");
                        driver.SetMotionState(0f, false, false, true);
                        stage = 105; stageStarted = EditorApplication.timeSinceStartup;
                        break;
                    case 105:
                        if (elapsed < 0.6) return;
                        Check(driver.CurrentArmState == "Torch_Idle", "Equipped standing settles into flashlight idle");
                        Vector3 wristToGrip = FindBone("torch_socket").position - FindBone("hand.R").position;
                        Check(Vector3.Dot(wristToGrip, instance.transform.right) < -0.01f,
                            "Right hand holds from the outer side with its palm directed inward toward the flashlight");
                        Capture("Unity_Torch_View.png");
                        driver.SetMotionState(0f, false, true, true);
                        CheckLegs(false, "Crouch input immediately hides all lower legs");
                        stage = 6; stageStarted = EditorApplication.timeSinceStartup;
                        break;
                    case 6:
                        if (elapsed < 0.12) return;
                        Check(driver.CurrentLegState == "Legs_Crouch_Down", "Crouch transition plays with torch equipped");
                        CheckLegs(false, "Lower legs remain hidden throughout crouch down");
                        if (validationCamera != null)
                        {
                            validationCamera.transform.position = new Vector3(0f, 1.28f, -0.04f);
                            validationCamera.transform.rotation = Quaternion.Euler(58f, 0f, 0f);
                        }
                        Next(); break;
                    case 7:
                        if (elapsed < 0.7) return;
                        Check(driver.CurrentLegState == "Legs_Crouch_Idle" && driver.CurrentArmState == "Torch_Idle", "Crouch idle preserves equipped upper-body state");
                        CheckActualStates("Legs_Crouch_Idle", "Torch_Idle");
                        CheckLegs(false, "Crouched idle renders no legs");
                        Check(driver.TorchVisible && driver.torchLight.enabled, "Crouching preserves the equipped flashlight and beam");
                        Check(driver.upperBody.position.y < standingUpperY - 0.15f, "Lower layer crouch height reaches the upper-body bone through the layer masks");
                        Capture("Unity_Crouch_View.png");
                        if (validationCamera != null) validationCamera.transform.rotation = Quaternion.Euler(2f, 0f, 0f);
                        driver.SetMotionState(0.4f, false, true, true);
                        Next(); break;
                    case 8:
                        if (elapsed < 0.4) return;
                        Check(driver.CurrentLegState == "Legs_Crouch_Walk" && driver.CurrentArmState == "Torch_Walk", "Crouch walk animates both branches");
                        CheckGaitPhase("Crouch-walk upper and lower cycles share stride phase");
                        CheckLegs(false, "Crouch walking renders no legs");
                        driver.SetMotionState(0f, false, false, true);
                        CheckLegs(false, "Releasing crouch keeps legs hidden until stand-up finishes");
                        if (validationCamera != null) validationCamera.transform.position = new Vector3(0f, 1.64f, -0.04f);
                        Next(); break;
                    case 9:
                        if (elapsed < 0.75) return;
                        Check(driver.CurrentLegState == "Legs_Idle", "Stand-up returns to idle");
                        CheckLegs(true, "Finishing stand-up restores lower legs");
                        driver.SetMotionState(0f, false, false, false);
                        CheckLegs(false, "Airborne input immediately hides all lower legs");
                        Next(); break;
                    case 10:
                        if (elapsed < 0.10) return;
                        Check(driver.CurrentLegState == "Legs_Jump_Start", "Ground departure starts jump clip");
                        CheckLegs(false, "Jump takeoff renders no legs");
                        if (validationCamera != null) validationCamera.transform.rotation = Quaternion.Euler(75f, 0f, 0f);
                        Next(); break;
                    case 11:
                        if (elapsed < 0.65) return;
                        Check(driver.CurrentLegState == "Legs_Jump_Loop", "Airborne state holds jump loop");
                        CheckLegs(false, "Airborne loop renders no legs");
                        Capture("Unity_Jump_HiddenLegs.png");
                        if (validationCamera != null) validationCamera.transform.rotation = Quaternion.Euler(2f, 0f, 0f);
                        driver.SetMotionState(0f, false, false, true);
                        CheckLegs(false, "Ground contact keeps legs hidden until landing finishes");
                        Next(); break;
                    case 12:
                        if (elapsed < 0.10) return;
                        Check(driver.CurrentLegState == "Legs_Jump_Land", "Ground contact starts landing clip");
                        CheckLegs(false, "Landing recovery renders no legs");
                        driver.SetTorchEquipped(false);
                        Next(); break;
                    case 13:
                        if (elapsed < 0.6) return;
                        Check(driver.CurrentArmState == "Torch_Unequip" && !driver.TorchVisible, "Unequip hides torch after retraction");
                        Check(!driver.torchLight.enabled, "Unequipping switches the spotlight off with the flashlight mesh");
                        Next(); break;
                    case 14:
                        if (elapsed < 1.3) return;
                        Check(!driver.TorchEquipped && driver.CurrentArmState == "Arms_Idle" && !driver.TorchVisible, "Unequip restores both natural hands");
                        Check(Vector3.Distance(FindBone("shoulder.L").localScale, Vector3.one) < 0.05f, "Unequip restores the left-arm branch scale");
                        CheckLegs(true, "After landing and recovery, lower legs are visible again");
                        driver.ToggleTorch(); driver.ToggleTorch(); driver.ToggleTorch();
                        Next(); break;
                    case 15:
                        if (elapsed < 0.3) return;
                        driver.ToggleTorch(); // Queue an unequip during the equip; current motion must finish.
                        Next(); break;
                    case 16:
                        if (elapsed < 4.1) return;
                        Check(!driver.TorchEquipped && !driver.TorchTransitioning && !driver.TorchVisible, "Rapid toggles resolve to the final requested state");
                        Check(Vector3.Distance(FindBone("hand.R").position, initialHand) < 0.06f, "Repeated view updates and transitions return the hand without cumulative pose drift");
                        Check(Vector3.Distance(initialRoot, instance.transform.position) < 0.00001f, "Animations never move the player root");
                        Check(!driver.animator.applyRootMotion, "Root motion remains disabled");
                        Finish(); break;
                }
            }
            catch (Exception e) { runtimeErrors.Add(e.ToString()); Finish(); }
        }

        static void Next() { stage++; stageStarted = EditorApplication.timeSinceStartup; }
        static ClipContract[] ValidateClipDurations(AnimationClip[] clips, List<string> errors)
        {
            var expected = new Dictionary<string, float> {
                { "Legs_Idle", 4f }, { "Legs_Walk", 1f }, { "Legs_Sprint", 20f / 30f },
                { "Legs_Crouch_Down", 0.6f }, { "Legs_Crouch_Idle", 2f }, { "Legs_Crouch_Walk", 40f / 30f },
                { "Legs_Crouch_Up", 0.6f }, { "Legs_Jump_Start", 0.4f }, { "Legs_Jump_Loop", 1f }, { "Legs_Jump_Land", 0.6f },
                { "Arms_Idle", 4f }, { "Arms_Walk", 1f }, { "Arms_Sprint", 20f / 30f },
                { "Torch_Equip", 2.1f }, { "Torch_Idle", 4f }, { "Torch_Walk", 1f }, { "Torch_Sprint", 20f / 30f }, { "Torch_Unequip", 1.6f }
            };
            return clips.OrderBy(c => c.name).Select(clip => {
                var result = new ClipContract { name = clip.name, importedSeconds = clip.length, expectedSeconds = expected[clip.name] };
                result.passed = Mathf.Abs(result.importedSeconds - result.expectedSeconds) < 0.001f;
                if (!result.passed) errors.Add("Clip duration changed: " + result.name + " = " + result.importedSeconds);
                return result;
            }).ToArray();
        }
        static EndpointComparison[] ValidateUpperEndpoints(AnimationClip[] clips, List<string> errors)
        {
            var byName = clips.ToDictionary(c => c.name);
            var results = new List<EndpointComparison>();
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(FPCharacterBuilder.ModelPath);
            // Sample fresh copies so imported constant-curve reduction cannot carry a previous pose into a check.
            Func<string, bool, Dictionary<string, LocalPose>> pose = (name, end) => {
                GameObject sample = UnityEngine.Object.Instantiate(model);
                try
                {
                    sample.hideFlags = HideFlags.HideAndDontSave;
                    Animator animator = sample.GetComponent<Animator>();
                    if (animator != null) animator.enabled = false;
                    byName[name].SampleAnimation(sample, end ? byName[name].length : 0f);
                    Transform upper = sample.GetComponentsInChildren<Transform>(true).First(t => t.name == "upper_body");
                    return upper.GetComponentsInChildren<Transform>(true).Where(t => t != upper).ToDictionary(
                        t => AnimationUtility.CalculateTransformPath(t, upper),
                        t => new LocalPose { position = t.localPosition, rotation = t.localRotation, scale = t.localScale });
                }
                finally { UnityEngine.Object.DestroyImmediate(sample); }
            };
            Action<string, bool, string, bool, string> compare = (a, aEnd, b, bEnd, description) => {
                var first = pose(a, aEnd); var second = pose(b, bEnd);
                var comparison = new EndpointComparison { description = description };
                foreach (string key in first.Keys)
                {
                    comparison.maximumLocalPositionDifferenceMetres = Mathf.Max(comparison.maximumLocalPositionDifferenceMetres, Vector3.Distance(first[key].position, second[key].position));
                    comparison.maximumLocalRotationDifferenceDegrees = Mathf.Max(comparison.maximumLocalRotationDifferenceDegrees, Quaternion.Angle(first[key].rotation, second[key].rotation));
                    comparison.maximumLocalScaleDifference = Mathf.Max(comparison.maximumLocalScaleDifference, Vector3.Distance(first[key].scale, second[key].scale));
                }
                comparison.passed = comparison.maximumLocalPositionDifferenceMetres < 0.003f
                    && comparison.maximumLocalRotationDifferenceDegrees < 0.3f && comparison.maximumLocalScaleDifference < 0.005f;
                if (!comparison.passed) errors.Add("Upper-branch endpoint discontinuity: " + description);
                results.Add(comparison);
            };
            compare("Arms_Idle", false, "Torch_Equip", false, "Natural idle to equip start");
            compare("Torch_Equip", true, "Torch_Idle", false, "Equip end to equipped idle");
            compare("Torch_Idle", false, "Torch_Unequip", false, "Equipped idle to unequip start");
            compare("Torch_Unequip", true, "Arms_Idle", false, "Unequip end to natural idle");
            return results.ToArray();
        }
        static void SampleLeftSupport()
        {
            AnimatorStateInfo state = driver.animator.GetCurrentAnimatorStateInfo(driver.animator.GetLayerIndex("Upper Body"));
            if (!state.IsName("Upper Body.Torch_Equip")) return;
            float clipSeconds = state.normalizedTime * 2.1f;
            if (!capturedLeftSupport && clipSeconds >= 0.9f && clipSeconds <= 1.05f)
            {
                Capture("Unity_Equip_LeftSupport.png");
                capturedLeftSupport = true;
            }
            if (clipSeconds < 0.25f || clipSeconds > 1.60f) return;
            if (equipLeftSamples.Count > 0 && clipSeconds - equipLeftSamples[equipLeftSamples.Count - 1].clipSeconds < 0.025f) return;
            Transform hand = FindBone("hand.L");
            var sample = new HandSample {
                clipSeconds = clipSeconds,
                positionRelativeToUpperBody = driver.upperBody.InverseTransformPoint(hand.position),
                rotationRelativeToUpperBody = Quaternion.Inverse(driver.upperBody.rotation) * hand.rotation,
                shoulderScale = FindBone("shoulder.L").localScale.x
            };
            foreach (HandSample previous in equipLeftSamples)
            {
                equipLeftExcursion = Mathf.Max(equipLeftExcursion, Vector3.Distance(previous.positionRelativeToUpperBody, sample.positionRelativeToUpperBody));
                equipLeftRotationExcursion = Mathf.Max(equipLeftRotationExcursion, Quaternion.Angle(previous.rotationRelativeToUpperBody, sample.rotationRelativeToUpperBody));
            }
            equipLeftSamples.Add(sample);
        }
        static void ValidateLeftSupport()
        {
            Check(equipLeftSamples.Count >= 8 && equipLeftSamples[0].clipSeconds < 0.4f
                && equipLeftSamples[equipLeftSamples.Count - 1].clipSeconds > 1.45f,
                "Equip left-support samples cover the pre-withdraw interval from 0.25 to 1.60 seconds");
            Check(equipLeftExcursion > 0.003f && equipLeftExcursion < 0.08f,
                "Left hand makes a subtle 3-80 mm support movement relative to upper_body before withdrawing");
            Check(equipLeftRotationExcursion > 0.1f && equipLeftRotationExcursion < 20f,
                "Left hand rotates subtly during the pre-withdraw support gesture");
            Check(equipLeftSamples.Count > 0 && equipLeftSamples.All(s => s.shoulderScale > 0.99f),
                "Left arm remains fully visible during the support gesture");
            Check(capturedLeftSupport, "Captured actual Play Mode left-support pose near 0.9 seconds");
        }
        static Transform FindBone(string name) => driver.animator.GetComponentsInChildren<Transform>().First(t => t.name == name);
        static void CheckLegs(bool visible, string label)
        {
            Check(driver.LegsVisible == visible && driver.legRenderers != null && driver.legRenderers.Length > 0
                && driver.legRenderers.All(r => r != null && r.enabled == visible), label);
        }
        static void ValidateStandingLowerMesh()
        {
            standingLowerMeshMaxY = float.NegativeInfinity;
            foreach (SkinnedMeshRenderer renderer in driver.legRenderers.OfType<SkinnedMeshRenderer>())
            {
                var mesh = new Mesh();
                renderer.BakeMesh(mesh);
                foreach (Vector3 vertex in mesh.vertices)
                    standingLowerMeshMaxY = Mathf.Max(standingLowerMeshMaxY, renderer.transform.TransformPoint(vertex).y);
                UnityEngine.Object.DestroyImmediate(mesh);
            }
            Check(standingLowerMeshMaxY > 0.1f && standingLowerMeshMaxY < 0.60f, "Standing leg geometry is restricted to below-knee shins and feet");
        }
        static void CheckActualStates(string lower, string upper)
        {
            Check(driver.animator.GetCurrentAnimatorStateInfo(driver.animator.GetLayerIndex("Lower Body")).IsName("Lower Body." + lower), "Animator evaluates " + lower);
            Check(driver.animator.GetCurrentAnimatorStateInfo(driver.animator.GetLayerIndex("Upper Body")).IsName("Upper Body." + upper), "Animator evaluates " + upper);
        }
        static void CheckGaitPhase(string label)
        {
            float lower = driver.animator.GetCurrentAnimatorStateInfo(driver.animator.GetLayerIndex("Lower Body")).normalizedTime;
            float upper = driver.animator.GetCurrentAnimatorStateInfo(driver.animator.GetLayerIndex("Upper Body")).normalizedTime;
            float difference = Mathf.Abs(Mathf.DeltaAngle(Mathf.Repeat(lower, 1f) * 360f, Mathf.Repeat(upper, 1f) * 360f)) / 360f;
            Check(difference < 0.12f, label);
        }
        static void Capture(string file)
        {
            if (validationCamera == null) return;
            Debug.Log("POSE " + file + " upper=" + driver.upperBody.position.ToString("F3")
                + " right=" + FindBone("hand.R").position.ToString("F3")
                + " rightScale=" + FindBone("shoulder.R").lossyScale.ToString("F3")
                + " rightView=" + validationCamera.WorldToViewportPoint(FindBone("hand.R").position).ToString("F3")
                + " left=" + FindBone("hand.L").position.ToString("F3")
                + " socket=" + FindBone("torch_socket").position.ToString("F3")
                + " renderer=" + driver.torchRenderers[0].bounds.ToString("F3"));
            var target = new RenderTexture(1280, 720, 24);
            validationCamera.targetTexture = target;
            validationCamera.Render();
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            image.Apply();
            File.WriteAllBytes(Path.Combine(OutputDirectory, file), image.EncodeToPNG());
            validationCamera.targetTexture = null;
            RenderTexture.active = previous;
            UnityEngine.Object.DestroyImmediate(image);
            UnityEngine.Object.DestroyImmediate(target);
        }
        static void Finish()
        {
            testing = false;
            EditorApplication.update -= Tick;
            Application.logMessageReceived -= CaptureLog;
            Directory.CreateDirectory(OutputDirectory);
            var report = new RuntimeReport { unityVersion = Application.unityVersion, passed = runtimeErrors.Count == 0, standingLowerLegMeshMaxY = standingLowerMeshMaxY,
                equipLeftExcursionMetres = equipLeftExcursion, equipLeftRotationExcursionDegrees = equipLeftRotationExcursion, equipLeftSamples = equipLeftSamples.ToArray(),
                checks = runtimeChecks.ToArray(), errors = runtimeErrors.ToArray(), editorWarnings = editorWarnings.ToArray() };
            File.WriteAllText(Path.Combine(OutputDirectory, "Unity_Runtime_Validation.json"), JsonUtility.ToJson(report, true));
            Debug.Log("FPCharacter runtime validation: " + (report.passed ? "PASS" : "FAIL") + ", " + runtimeChecks.Count + " checks, " + runtimeErrors.Count + " errors.");
            EditorApplication.isPlaying = false;
        }
    }
}
