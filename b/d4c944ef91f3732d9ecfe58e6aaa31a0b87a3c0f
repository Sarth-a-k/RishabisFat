using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace FPCharacter.Editor
{
    public static class FPCharacterBuilder
    {
        public const string AssetRoot = "Assets/FPCharacter";
        public const string ModelPath = AssetRoot + "/Models/FP_Character.fbx";
        public const string PrefabPath = AssetRoot + "/Prefabs/FP_Character.prefab";
        public const string ControllerPath = AssetRoot + "/Animations/FP_Character.controller";
        public static readonly string[] RequiredClips = {
            "Legs_Idle", "Legs_Walk", "Legs_Sprint", "Legs_Crouch_Down", "Legs_Crouch_Idle",
            "Legs_Crouch_Walk", "Legs_Crouch_Up", "Legs_Jump_Start", "Legs_Jump_Loop", "Legs_Jump_Land",
            "Arms_Idle", "Arms_Walk", "Arms_Sprint", "Torch_Equip", "Torch_Idle", "Torch_Walk", "Torch_Sprint", "Torch_Unequip"
        };

        [MenuItem("Tools/FP Character/Build or rebuild prefab")]
        public static void Build()
        {
            foreach (string folder in new[] { "Animations", "Materials", "Prefabs", "Scenes" })
                if (!AssetDatabase.IsValidFolder(AssetRoot + "/" + folder)) AssetDatabase.CreateFolder(AssetRoot, folder);
            ConfigureModel();
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (model == null) throw new FileNotFoundException("Copy the supplied FBX to " + ModelPath);
            var clips = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview__", StringComparison.Ordinal)).ToDictionary(c => c.name);
            string[] missing = RequiredClips.Where(name => !clips.ContainsKey(name)).ToArray();
            if (missing.Length != 0) throw new InvalidOperationException("Missing animation clips: " + string.Join(", ", missing));

            AvatarMask lowerMask = BuildMask(model, false);
            AvatarMask upperMask = BuildMask(model, true);
            AnimatorController controller = BuildController(clips, lowerMask, upperMask);
            Material material = BuildMaterial();
            Scene stagingScene = EditorSceneManager.NewPreviewScene();
            GameObject wrapper = new GameObject("FP_Character");
            SceneManager.MoveGameObjectToScene(wrapper, stagingScene);
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(model, stagingScene);
            instance.transform.SetParent(wrapper.transform, false);
            Animator animator = instance.GetComponent<Animator>();
            if (animator == null) animator = instance.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            Transform upperBody = Find(instance.transform, "upper_body");
            if (upperBody == null || Find(instance.transform, "pelvis") == null)
                throw new InvalidOperationException("The generic rig requires upper_body and pelvis branches.");

            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                renderer.sharedMaterials = Enumerable.Repeat(material, renderer.sharedMaterials.Length).ToArray();
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                if (renderer is SkinnedMeshRenderer skinned)
                {
                    skinned.quality = SkinQuality.Bone4;
                    skinned.updateWhenOffscreen = false;
                    // Arm retraction needs generous bounds; retain the imported leg bounds for normal camera culling.
                    if (skinned.name != "FP_LowerLegs")
                        skinned.localBounds = new Bounds(new Vector3(0f, 1f, 0f), new Vector3(5f, 5f, 5f));
                }
            }
            var torchRenderers = instance.GetComponentsInChildren<Renderer>(true)
                .Where(r => r.name.Equals("Flashlight", StringComparison.OrdinalIgnoreCase) || r.name.StartsWith("Flashlight.", StringComparison.OrdinalIgnoreCase)).ToArray();
            if (torchRenderers.Length == 0) throw new InvalidOperationException("A separate renderer named Flashlight is required for timed equip visibility.");
            foreach (Renderer renderer in torchRenderers) renderer.enabled = false;
            var legRenderers = instance.GetComponentsInChildren<Renderer>(true)
                .Where(r => r.name == "FP_LowerLegs" || r.name.StartsWith("FP_LowerLegs.", StringComparison.Ordinal)).ToArray();
            if (legRenderers.Length == 0) throw new InvalidOperationException("A separate renderer named FP_LowerLegs is required for crouch/jump visibility.");
            foreach (Renderer renderer in legRenderers) renderer.enabled = true;
            Transform torchSocket = Find(instance.transform, "torch_socket");
            Transform torchTip = Find(instance.transform, "torch_tip") ?? torchSocket;
            Transform beamTarget = Find(instance.transform, "torch_beam_target");
            var lightObject = new GameObject("Flashlight_Spotlight");
            lightObject.transform.SetParent(torchTip != null ? torchTip : upperBody, false);
            Vector3 direction = beamTarget != null && torchTip != null ? beamTarget.position - torchTip.position
                : torchSocket != null && torchTip != null ? torchTip.position - torchSocket.position : lightObject.transform.forward;
            if (direction.sqrMagnitude < 0.000001f) direction = lightObject.transform.forward;
            Vector3 up = Mathf.Abs(Vector3.Dot(direction.normalized, Vector3.up)) > 0.99f ? Vector3.forward : Vector3.up;
            lightObject.transform.rotation = Quaternion.LookRotation(direction.normalized, up);
            Light torchLight = lightObject.AddComponent<Light>();
            torchLight.type = LightType.Spot;
            torchLight.color = new Color(1f, 0.95f, 0.88f);
            torchLight.range = 10f;
            torchLight.spotAngle = 50f;
            torchLight.intensity = 1.5f;
            torchLight.shadows = LightShadows.None;
            torchLight.enabled = false;

            var driver = wrapper.AddComponent<FirstPersonCharacterAnimator>();
            driver.animator = animator;
            driver.upperBody = upperBody;
            driver.torchRenderers = torchRenderers;
            driver.legRenderers = legRenderers;
            driver.torchLight = torchLight;
            driver.enableTorchLight = true;
            driver.readToggleKey = true;
            driver.legacyToggleKey = KeyCode.F;
            PrefabUtility.SaveAsPrefabAsset(wrapper, PrefabPath);
            UnityEngine.Object.DestroyImmediate(wrapper);
            EditorSceneManager.ClosePreviewScene(stagingScene);
            AssetDatabase.SaveAssets();
            Debug.Log("FPCharacter: built single-rig prefab with " + clips.Count + " animation clips at " + PrefabPath);
        }

        static void ConfigureModel()
        {
            AssetDatabase.ImportAsset(ModelPath, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
            if (importer == null) throw new FileNotFoundException(ModelPath);
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importBlendShapes = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.optimizeGameObjects = false; // Upper-body pitch and the torch light need exposed bone transforms.
            importer.optimizeMeshVertices = true;
            importer.optimizeMeshPolygons = true;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.isReadable = false;
            importer.useFileUnits = true;
            importer.globalScale = 1f;
            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = ModelImporterTangents.CalculateMikk;
            importer.animationCompression = ModelImporterAnimationCompression.KeyframeReduction;
            importer.animationRotationError = 0.15f;
            importer.animationPositionError = 0.10f;
            importer.animationScaleError = 0.10f;
            ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
            foreach (ModelImporterClipAnimation clip in clips)
            {
                string canonical = RequiredClips.FirstOrDefault(name => clip.name.EndsWith(name, StringComparison.Ordinal));
                if (canonical != null) clip.name = canonical;
                bool loop = clip.name.EndsWith("_Idle", StringComparison.Ordinal)
                    || clip.name.EndsWith("_Walk", StringComparison.Ordinal)
                    || clip.name.EndsWith("_Sprint", StringComparison.Ordinal)
                    || clip.name == "Legs_Jump_Loop";
                clip.loopTime = loop;
                clip.loopPose = loop;
                clip.lockRootRotation = true;
                clip.lockRootHeightY = true;
                clip.lockRootPositionXZ = true;
                clip.keepOriginalOrientation = true;
                clip.keepOriginalPositionY = true;
                clip.keepOriginalPositionXZ = true;
            }
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
        }

        static AvatarMask BuildMask(GameObject model, bool upper)
        {
            string path = AssetRoot + "/Animations/" + (upper ? "UpperBody" : "LowerBody") + ".mask";
            AvatarMask mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(path);
            if (mask == null) { mask = new AvatarMask(); AssetDatabase.CreateAsset(mask, path); }
            for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++) mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, false);
            Transform[] transforms = model.GetComponentsInChildren<Transform>(true);
            mask.transformCount = transforms.Length;
            int activeCount = 0;
            for (int i = 0; i < transforms.Length; i++)
            {
                string relative = AnimationUtility.CalculateTransformPath(transforms[i], model.transform);
                string[] segments = relative.Split('/');
                int upperIndex = Array.IndexOf(segments, "upper_body");
                bool upperDescendant = upperIndex >= 0 && upperIndex < segments.Length - 1;
                bool lowerBranch = Array.IndexOf(segments, "pelvis") >= 0;
                bool active = upper ? upperDescendant : lowerBranch || transforms[i].name == "upper_body";
                mask.SetTransformPath(i, relative);
                mask.SetTransformActive(i, active);
                if (active) activeCount++;
            }
            if (activeCount == 0) throw new InvalidOperationException("No bones matched " + path);
            EditorUtility.SetDirty(mask);
            return mask;
        }

        static AnimatorController BuildController(Dictionary<string, AnimationClip> clips, AvatarMask lower, AvatarMask upper)
        {
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            controller.parameters = new[] { new AnimatorControllerParameter { name = "UpperCycleRate", type = AnimatorControllerParameterType.Float, defaultFloat = 1f } };
            // Keep the controller GUID stable when rebuilding so existing prefab references survive.
            foreach (AnimatorControllerLayer old in controller.layers)
                if (old.stateMachine != null) UnityEngine.Object.DestroyImmediate(old.stateMachine, true);
            var lowerMachine = new AnimatorStateMachine { name = "Lower Body" };
            var upperMachine = new AnimatorStateMachine { name = "Upper Body" };
            AssetDatabase.AddObjectToAsset(lowerMachine, controller);
            AssetDatabase.AddObjectToAsset(upperMachine, controller);
            controller.layers = new[] {
                new AnimatorControllerLayer { name = "Lower Body", stateMachine = lowerMachine, avatarMask = lower, defaultWeight = 1f },
                new AnimatorControllerLayer { name = "Upper Body", stateMachine = upperMachine, avatarMask = upper, defaultWeight = 1f, blendingMode = AnimatorLayerBlendingMode.Override }
            };
            int lowerIndex = 0, upperIndex = 0;
            foreach (string name in RequiredClips)
            {
                bool isLeg = name.StartsWith("Legs_", StringComparison.Ordinal);
                AnimatorStateMachine machine = isLeg ? lowerMachine : upperMachine;
                int index = isLeg ? lowerIndex++ : upperIndex++;
                AnimatorState state = machine.AddState(name, new Vector3(260 * (index % 3), 85 * (index / 3), 0));
                state.motion = clips[name];
                state.writeDefaultValues = false;
                if (!isLeg && (name.EndsWith("_Walk", StringComparison.Ordinal) || name.EndsWith("_Sprint", StringComparison.Ordinal)))
                {
                    state.speedParameter = "UpperCycleRate";
                    state.speedParameterActive = true;
                }
                if (name == "Legs_Idle" || name == "Arms_Idle") machine.defaultState = state;
            }
            EditorUtility.SetDirty(controller);
            return controller;
        }

        static Material BuildMaterial()
        {
            string pipeline = GraphicsSettings.currentRenderPipeline == null ? "" : GraphicsSettings.currentRenderPipeline.GetType().Name;
            string shaderName = pipeline.Contains("HDRender") ? "HDRP/Lit" : pipeline.Contains("Universal") ? "Universal Render Pipeline/Lit" : "Standard";
            Shader shader = Shader.Find(shaderName) ?? Shader.Find("Standard");
            if (shader == null) throw new InvalidOperationException("No compatible Lit/Standard shader is installed.");
            string materialPath = AssetRoot + "/Materials/FP_Surface.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, materialPath); }
            material.shader = shader;
            Texture2D color = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetRoot + "/Textures/FP_Atlas_BaseColor.png");
            string metalPath = AssetRoot + "/Textures/FP_Atlas_MetallicGloss.png";
            var metalImporter = AssetImporter.GetAtPath(metalPath) as TextureImporter;
            if (metalImporter != null && metalImporter.sRGBTexture)
            {
                metalImporter.sRGBTexture = false;
                metalImporter.SaveAndReimport();
            }
            Texture2D metal = AssetDatabase.LoadAssetAtPath<Texture2D>(metalPath);
            foreach (string textureName in new[] { "_MainTex", "_BaseMap", "_BaseColorMap" })
                if (material.HasProperty(textureName)) material.SetTexture(textureName, color);
            foreach (string colorName in new[] { "_Color", "_BaseColor" })
                if (material.HasProperty(colorName)) material.SetColor(colorName, Color.white);
            if (material.HasProperty("_MetallicGlossMap")) material.SetTexture("_MetallicGlossMap", metal);
            if (material.HasProperty("_GlossMapScale")) material.SetFloat("_GlossMapScale", 1f);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", 0.3f);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", metal != null && material.HasProperty("_MetallicGlossMap") ? 1f : 0.3f);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
            if (metal != null) { material.EnableKeyword("_METALLICGLOSSMAP"); material.EnableKeyword("_METALLICSPECGLOSSMAP"); }
            material.enableInstancing = false;
            EditorUtility.SetDirty(material);
            return material;
        }

        [MenuItem("Tools/FP Character/Create animation preview scene")]
        public static void CreatePreviewScene()
        {
            if (!AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath)) Build();
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (string.IsNullOrEmpty(SceneManager.GetSceneAt(i).path))
                    throw new InvalidOperationException("Save your current untitled scene before creating the preview scene. Your scene is left unchanged.");
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), scene);
            GameObject cameraObject = new GameObject("PreviewCamera");
            SceneManager.MoveGameObjectToScene(cameraObject, scene);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.transform.position = new Vector3(0f, 1.64f, -0.04f);
            camera.transform.rotation = Quaternion.Euler(2f, 0f, 0f);
            camera.nearClipPlane = 0.025f;
            camera.farClipPlane = 100f;
            camera.fieldOfView = 45.75f; // Matches a 24 mm / 36 mm sensor Blender camera at 16:9.
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.10f, 0.12f, 0.15f);
            var driver = instance.GetComponent<FirstPersonCharacterAnimator>();
            driver.viewCamera = camera.transform;
            var demo = instance.AddComponent<FPCharacterPreview>();
            demo.character = driver;
            demo.previewCamera = camera.transform;
            GameObject lightObject = new GameObject("PreviewKeyLight");
            SceneManager.MoveGameObjectToScene(lightObject, scene);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.6f;
            light.transform.rotation = Quaternion.Euler(38f, -25f, 0f);
            light.shadows = LightShadows.None;
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            SceneManager.MoveGameObjectToScene(floor, scene);
            floor.name = "PreviewFloor";
            UnityEngine.Object.DestroyImmediate(floor.GetComponent<Collider>());
            EditorSceneManager.SaveScene(scene, AssetRoot + "/Scenes/FP_Character_Preview.unity");
            EditorSceneManager.CloseScene(scene, true);
            AssetDatabase.SaveAssets();
            Debug.Log("FPCharacter preview scene created. Open it and press Play for animation controls.");
        }

        public static void BuildBatch()
        {
            Build();
            CreatePreviewScene();
            FPCharacterValidation.ValidateAssets();
        }

        static Transform Find(Transform root, string name) => root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);
    }
}
