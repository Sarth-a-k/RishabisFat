using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace FPCharacter.EditorTools
{
    [InitializeOnLoad]
    public static class InteractionSceneSetup
    {
        const string ScenePath = "Assets/GameTest.unity";
        const string Root = "Assets/Interaction";
        const string MarkerPath = "Assets/Interaction/Editor/.setup_done_v5";
        const string MirrorDir = "Assets/Environment/Mirrors";
        const string TorchFbx = "Assets/Environment/Torch/TorchStand_LowPoly.fbx";
        const string GhostFbx = "Assets/GhostExplorer_LowPoly.fbx";
        static readonly string[] MirrorKinds = { "Rectangle", "Circle", "Oval" };

        const string TriggerPath = "Assets/Interaction/Editor/.run_setup";

        static InteractionSceneSetup()
        {
            if (!File.Exists(TriggerPath) || File.ReadAllText(TriggerPath).Trim() != "pending") return;
            EditorApplication.update += WaitAndRun;
        }

        static void WaitAndRun()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            EditorApplication.update -= WaitAndRun;
            Run();
        }

        [MenuItem("Tools/Interaction/Rebuild Interaction Setup")]
        public static void Run()
        {
            try
            {
                RunInternal();
            }
            catch (Exception e)
            {
                Debug.LogError("[InteractionSetup] FAILED: " + e);
            }
        }

        static void RunInternal()
        {
            RestoreRecoveredScene();
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                if (!string.IsNullOrEmpty(scene.path) && scene.isDirty) EditorSceneManager.SaveScene(scene);
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }
            else if (scene.isDirty)
            {
                EditorSceneManager.SaveScene(scene);
            }
            AssetDatabase.SaveAssets();
            Directory.CreateDirectory("Backups");
            File.Copy(ScenePath, "Backups/GameTest_before_interaction.unity", true);
            Debug.Log("[InteractionSetup] Saved scene + project. Backup: Backups/GameTest_before_interaction.unity");

            AssetDatabase.Refresh();
            EnsureFolder(Root + "/Materials");
            EnsureFolder(Root + "/Prefabs");
            EnsureFolder(Root + "/Animations");
            ConfigureImporters();

            LighterView lighter = BuildLighter();
            GameObject[] mirrors = MirrorKinds.Select(BuildMirror).ToArray();
            GameObject torch = BuildTorch();
            AnimatorController ghostController = BuildGhostController();

            BuildScene(lighter, mirrors, torch, ghostController);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            File.WriteAllText(MarkerPath, DateTime.Now.ToString("o"));
            if (File.Exists(TriggerPath)) File.WriteAllText(TriggerPath, "done");
            Debug.Log("[InteractionSetup] DONE. Scene saved. Press Play: E = light torch / pick up / place mirror, Q/R = rotate mirror.");
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        static void ConfigureImporters()
        {
            foreach (string kind in MirrorKinds)
            {
                ModelImporter mi = AssetImporter.GetAtPath(MirrorDir + "/Mirror_" + kind + ".fbx") as ModelImporter;
                if (mi != null && !mi.isReadable)
                {
                    mi.isReadable = true;
                    mi.SaveAndReimport();
                }
            }
            ModelImporter gi = AssetImporter.GetAtPath(GhostFbx) as ModelImporter;
            if (gi != null)
            {
                bool changed = false;
                if (!gi.isReadable)
                {
                    gi.isReadable = true;
                    changed = true;
                }
                if (gi.animationType != ModelImporterAnimationType.Generic)
                {
                    gi.animationType = ModelImporterAnimationType.Generic;
                    changed = true;
                }
                ModelImporterClipAnimation[] clips = gi.clipAnimations;
                if (clips == null || clips.Length == 0) clips = gi.defaultClipAnimations;
                if (clips != null && clips.Length > 0 && clips.Any(c => !c.loopTime))
                {
                    foreach (ModelImporterClipAnimation c in clips) c.loopTime = true;
                    gi.clipAnimations = clips;
                    changed = true;
                }
                if (changed) gi.SaveAndReimport();
            }
        }

        static Shader FindShader(bool unlit)
        {
            Shader s = Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit");
            if (s == null) s = Shader.Find(unlit ? "Unlit/Color" : "Standard");
            return s;
        }

        static Material Mat(string name, bool unlit, Color color, float metallic = 0f, float smoothness = 0.5f, Texture tex = null, Color? emission = null)
        {
            string path = Root + "/Materials/" + name + ".mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = FindShader(unlit);
            if (m == null)
            {
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            else m.shader = shader;
            m.SetColor("_BaseColor", color);
            m.SetColor("_Color", color);
            if (!unlit)
            {
                m.SetFloat("_Metallic", metallic);
                m.SetFloat("_Smoothness", smoothness);
                m.SetFloat("_Glossiness", smoothness);
            }
            if (tex != null)
            {
                m.SetTexture("_BaseMap", tex);
                m.SetTexture("_MainTex", tex);
            }
            if (emission.HasValue)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emission.Value);
                if (tex != null) m.SetTexture("_EmissionMap", tex);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            EditorUtility.SetDirty(m);
            return m;
        }

        static GameObject Part(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, Quaternion rot, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = rot;
            go.transform.localScale = scale;
            MeshRenderer r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go;
        }

        static LighterView BuildLighter()
        {
            Material body = Mat("Lighter_Body", false, new Color(0.78f, 0.74f, 0.66f), 0.9f, 0.65f);
            Material dark = Mat("Lighter_Dark", false, new Color(0.12f, 0.12f, 0.13f), 0.8f, 0.4f);
            Material chimney = Mat("Lighter_Chimney", false, new Color(0.55f, 0.53f, 0.5f), 0.9f, 0.5f);
            Material flameOuter = Mat("Lighter_FlameOuter", true, new Color(1f, 0.5f, 0.12f));
            Material flameInner = Mat("Lighter_FlameInner", true, new Color(1f, 0.92f, 0.6f));

            GameObject root = new GameObject("Lighter");
            LighterView view = root.AddComponent<LighterView>();
            Part(PrimitiveType.Cube, "Body", root.transform, new Vector3(0f, 0.02f, 0f), new Vector3(0.036f, 0.04f, 0.013f), Quaternion.identity, body);
            Transform hinge = new GameObject("LidHinge").transform;
            hinge.SetParent(root.transform, false);
            hinge.localPosition = new Vector3(0.018f, 0.04f, 0f);
            Part(PrimitiveType.Cube, "Lid", hinge, new Vector3(-0.018f, 0.009f, 0f), new Vector3(0.0362f, 0.018f, 0.0134f), Quaternion.identity, body);
            Part(PrimitiveType.Cube, "Chimney", root.transform, new Vector3(-0.004f, 0.047f, 0f), new Vector3(0.02f, 0.014f, 0.0104f), Quaternion.identity, chimney);
            Part(PrimitiveType.Cylinder, "Wheel", root.transform, new Vector3(0.011f, 0.051f, 0f), new Vector3(0.008f, 0.0025f, 0.008f), Quaternion.Euler(90f, 0f, 0f), dark);
            Transform flame = new GameObject("Flame").transform;
            flame.SetParent(root.transform, false);
            flame.localPosition = new Vector3(-0.004f, 0.055f, 0f);
            Part(PrimitiveType.Sphere, "FlameOuter", flame, new Vector3(0f, 0.013f, 0f), new Vector3(0.011f, 0.026f, 0.011f), Quaternion.identity, flameOuter);
            Part(PrimitiveType.Sphere, "FlameInner", flame, new Vector3(0f, 0.009f, 0f), new Vector3(0.0055f, 0.013f, 0.0055f), Quaternion.identity, flameInner);
            GameObject lightGo = new GameObject("FlameLight");
            lightGo.transform.SetParent(root.transform, false);
            lightGo.transform.localPosition = new Vector3(-0.004f, 0.08f, 0f);
            Light light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.62f, 0.28f);
            light.range = 3f;
            light.intensity = 0f;
            light.shadows = LightShadows.None;
            view.lidHinge = hinge;
            view.flame = flame;
            view.flameLight = light;
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, Root + "/Prefabs/Lighter.prefab");
            Object.DestroyImmediate(root);
            Debug.Log("[InteractionSetup] Built Lighter prefab");
            return prefab.GetComponent<LighterView>();
        }

        static Bounds WorldBounds(GameObject go)
        {
            Renderer[] rs = go.GetComponentsInChildren<Renderer>(true);
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            Bounds b = rs[0].bounds;
            foreach (Renderer r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        static void Remap(GameObject go, Func<Renderer, string, Material> pick)
        {
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
            {
                Material[] ms = r.sharedMaterials;
                for (int i = 0; i < ms.Length; i++)
                {
                    Material m = pick(r, ms[i] != null ? ms[i].name : "");
                    if (m != null) ms[i] = m;
                }
                r.sharedMaterials = ms;
            }
        }

        static void RestoreRecoveredScene()
        {
            const string recovery = "Assets/_Recovery/0.unity";
            if (!File.Exists(recovery)) return;
            string text = File.ReadAllText(recovery);
            if (!text.Contains("FPCharacterMover")) return;
            Directory.CreateDirectory("Backups");
            File.Copy(recovery, "Backups/GameTest_recovered_from_crash.unity", true);
            if (File.Exists(ScenePath)) File.Copy(ScenePath, "Backups/GameTest_old_saved.unity", true);
            Scene rec = EditorSceneManager.OpenScene(recovery, OpenSceneMode.Single);
            EditorSceneManager.SaveScene(rec, ScenePath);
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            AssetDatabase.DeleteAsset("Assets/_Recovery");
            Debug.Log("[InteractionSetup] Restored your unsaved work from Unity's crash recovery into GameTest.unity");
        }

        static void LogHierarchy(Transform t, string indent)
        {
            Renderer r = t.GetComponent<Renderer>();
            Debug.Log("[InteractionSetup]   " + indent + t.name + " pos " + t.localPosition.ToString("F2") + " rot " + t.localEulerAngles.ToString("F0") + (r != null ? " bounds " + r.bounds.center.ToString("F2") + " size " + r.bounds.size.ToString("F2") : ""));
            for (int i = 0; i < t.childCount; i++) LogHierarchy(t.GetChild(i), indent + "  ");
        }

        static Transform FindChild(GameObject root, string name, bool needRenderer)
        {
            return root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name.StartsWith(name) && (!needRenderer || t.GetComponent<Renderer>() != null));
        }

        static void FixTorchParts(GameObject visual)
        {
            Transform stand = FindChild(visual, "TorchStand", true);
            Renderer standRenderer = stand != null ? stand.GetComponent<Renderer>() : visual.GetComponent<Renderer>();
            Transform socket = FindChild(visual, "FlameSocket", false);
            if (standRenderer == null || socket == null) return;
            Bounds sb = standRenderer.bounds;
            Vector3 cup = new Vector3(sb.center.x, sb.max.y - 0.12f, sb.center.z);
            if (Vector3.Distance(socket.position, cup) > 0.3f) socket.position = cup;
            foreach (Transform flame in visual.GetComponentsInChildren<Transform>(true))
            {
                if (!flame.name.StartsWith("Flame_")) continue;
                MeshFilter mf = flame.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                Bounds mb = mf.sharedMesh.bounds;
                int axis = mb.size.x >= mb.size.y && mb.size.x >= mb.size.z ? 0 : mb.size.y >= mb.size.z ? 1 : 2;
                Vector3 local = Vector3.zero;
                local[axis] = mb.center[axis] >= 0f ? 1f : -1f;
                Vector3 world = flame.TransformDirection(local);
                flame.rotation = Quaternion.FromToRotation(world, Vector3.up) * flame.rotation;
                flame.position = socket.position;
            }
            Debug.Log("[InteractionSetup] Torch parts after fix:");
            LogHierarchy(visual.transform, "");
        }

        static GameObject Wrap(string name, string fbx, Action<GameObject> fix = null)
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
            if (model == null) throw new Exception("Model not imported yet: " + fbx);
            GameObject root = new GameObject(name);
            GameObject visual = (GameObject)Object.Instantiate(model, root.transform);
            visual.name = "Visual";
            if (fix != null) fix(visual);
            Bounds b = WorldBounds(visual);
            visual.transform.position -= new Vector3(b.center.x, b.min.y, b.center.z);
            return root;
        }

        static Vector3 GlassFront(GameObject root)
        {
            Vector3 sum = Vector3.zero;
            foreach (MeshFilter mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                MeshRenderer mr = mf.GetComponent<MeshRenderer>();
                Mesh mesh = mf.sharedMesh;
                if (mr == null || mesh == null) continue;
                Material[] mats = mr.sharedMaterials;
                Vector3[] normals = mesh.normals;
                Vector3[] verts = mesh.vertices;
                for (int s = 0; s < mesh.subMeshCount && s < mats.Length; s++)
                {
                    if (mats[s] == null || !mats[s].name.Contains("Glass")) continue;
                    int[] tris = mesh.GetTriangles(s);
                    Vector3 local = Vector3.zero;
                    if (normals != null && normals.Length == verts.Length)
                        foreach (int i in tris) local += normals[i];
                    else
                        for (int i = 0; i + 2 < tris.Length; i += 3)
                            local += Vector3.Cross(verts[tris[i + 1]] - verts[tris[i]], verts[tris[i + 2]] - verts[tris[i]]);
                    sum += mf.transform.TransformDirection(local);
                }
            }
            Vector3 f = root.transform.InverseTransformDirection(sum);
            f.y = 0f;
            return f.sqrMagnitude > 1e-8f ? f.normalized : Vector3.forward;
        }

        static GameObject BuildMirror(string kind)
        {
            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(MirrorDir + "/MirrorGlass_Dust.png");
            Material frame = Mat("Mirror_Frame", false, new Color(0.02f, 0.025f, 0.05f), 0.15f, 0.45f);
            Material edge = Mat("Mirror_FrameEdge", false, new Color(0.05f, 0.06f, 0.11f), 0.3f, 0.55f);
            Material glass = Mat("Mirror_Glass", false, Color.white, 0f, 0.15f, tex, new Color(0.07f, 0.07f, 0.075f));
            Material back = Mat("Mirror_Back", false, new Color(0.03f, 0.032f, 0.045f), 0f, 0.2f);

            GameObject root = Wrap("Mirror_" + kind, MirrorDir + "/Mirror_" + kind + ".fbx");
            Remap(root, (r, n) => n.Contains("Glass") ? glass : n.Contains("Edge") ? edge : n.Contains("Back") ? back : n.Contains("Frame") ? frame : null);
            Bounds b = WorldBounds(root);
            BoxCollider box = root.AddComponent<BoxCollider>();
            box.center = b.center;
            box.size = b.size;
            MirrorPickup pickup = root.AddComponent<MirrorPickup>();
            pickup.localBounds = b;
            pickup.localFront = GlassFront(root);
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = ShadowCastingMode.On;
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, Root + "/Prefabs/Mirror_" + kind + ".prefab");
            Object.DestroyImmediate(root);
            Debug.Log("[InteractionSetup] Mirror_" + kind + " size " + b.size.ToString("F2") + " front " + pickup.localFront.ToString("F2"));
            return prefab;
        }

        static GameObject BuildTorch()
        {
            Material iron = Mat("Torch_Iron", false, new Color(0.09f, 0.085f, 0.09f), 0.75f, 0.45f);
            Material ironDark = Mat("Torch_IronDark", false, new Color(0.05f, 0.045f, 0.05f), 0.6f, 0.3f);
            Material embers = Mat("Torch_Embers", false, new Color(0.2f, 0.05f, 0f), 0f, 0.1f, null, new Color(2.2f, 0.55f, 0.06f));
            Material flameOuter = Mat("Torch_FlameOuter", true, new Color(1f, 0.45f, 0.08f));
            Material flameInner = Mat("Torch_FlameInner", true, new Color(1f, 0.85f, 0.4f));

            GameObject root = Wrap("PuzzleTorch", TorchFbx, FixTorchParts);
            Remap(root, (r, n) =>
            {
                if (r.name.StartsWith("Flame_Inner") || n.Contains("Flame_Inner")) return flameInner;
                if (r.name.StartsWith("Flame_Outer") || n.Contains("Flame_Outer")) return flameOuter;
                if (r.name.StartsWith("Embers") || n.Contains("Ember")) return embers;
                if (n.Contains("Dark")) return ironDark;
                return iron;
            });
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
                if (r.name.StartsWith("Flame") || r.name.StartsWith("Embers")) r.shadowCastingMode = ShadowCastingMode.Off;
            Bounds b = WorldBounds(root);
            CapsuleCollider cap = root.AddComponent<CapsuleCollider>();
            cap.radius = 0.22f;
            cap.height = Mathf.Max(0.5f, b.size.y);
            cap.center = new Vector3(0f, cap.height * 0.5f, 0f);
            PuzzleTorch pt = root.AddComponent<PuzzleTorch>();
            pt.readInteractKey = false;
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, Root + "/Prefabs/PuzzleTorch.prefab");
            Object.DestroyImmediate(root);
            Transform socket = prefab.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "FlameSocket");
            Debug.Log("[InteractionSetup] PuzzleTorch size " + b.size.ToString("F2") + " flame socket " + (socket != null ? socket.position.ToString("F2") : "MISSING"));
            return prefab;
        }

        static AnimatorController BuildGhostController()
        {
            AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(GhostFbx).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));
            string path = Root + "/Animations/Ghost_Idle.controller";
            AnimatorController ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (ctrl == null && clip != null) ctrl = AnimatorController.CreateAnimatorControllerAtPathWithClip(path, clip);
            Debug.Log("[InteractionSetup] Ghost clip: " + (clip != null ? clip.name + " " + clip.length.ToString("F2") + "s loop=" + clip.isLooping : "none found"));
            return ctrl;
        }

        static void MakeTransparent(Material m)
        {
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_AlphaClip", 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            m.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(m);
        }

        static Material GhostMat(string part, Color linear, float alpha, Color emissionLinear, float emissionBoost, float smoothness)
        {
            Color baseColor = linear.gamma;
            baseColor.a = alpha;
            Color emission = emissionLinear.gamma * emissionBoost;
            Material m = Mat("Ghost_" + part, false, baseColor, 0f, smoothness, null, emission);
            MakeTransparent(m);
            return m;
        }

        static Material FaceMat(string name, Color color, Color? emission, int queue, float smoothness)
        {
            Material m = Mat("Ghost_" + name, false, color, 0f, smoothness, null, emission);
            MakeTransparent(m);
            m.renderQueue = queue;
            EditorUtility.SetDirty(m);
            return m;
        }

        static void AddFaceDetails(GameObject ghost)
        {
            SkinnedMeshRenderer smr = ghost.GetComponentsInChildren<SkinnedMeshRenderer>(true).FirstOrDefault(r => r.sharedMesh != null && r.sharedMesh.isReadable);
            if (smr == null)
            {
                Debug.LogWarning("[InteractionSetup] Face: no readable ghost mesh");
                return;
            }
            Material[] mats = smr.sharedMaterials;
            int eyeSub = Array.FindIndex(mats, m => m != null && m.name.Contains("Eyes"));
            Transform head = smr.bones.FirstOrDefault(b => b != null && b.name == "Head");
            if (eyeSub < 0 || head == null || eyeSub >= smr.sharedMesh.subMeshCount)
            {
                Debug.LogWarning("[InteractionSetup] Face: eyes or head bone not found");
                return;
            }
            mats[eyeSub].renderQueue = 3008;
            EditorUtility.SetDirty(mats[eyeSub]);
            Vector3[] verts = smr.sharedMesh.vertices;
            var pts = smr.sharedMesh.GetTriangles(eyeSub).Distinct().Select(i => smr.transform.TransformPoint(verts[i])).ToList();
            Vector3 center = pts.Aggregate(Vector3.zero, (acc, p) => acc + p) / pts.Count;
            Vector3 fwd = center - head.position;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 1e-6f) fwd = ghost.transform.forward;
            fwd.Normalize();
            Vector3 lateral = Vector3.Cross(Vector3.up, fwd);
            var sideA = pts.Where(p => Vector3.Dot(p - center, lateral) >= 0f).ToList();
            var sideB = pts.Where(p => Vector3.Dot(p - center, lateral) < 0f).ToList();
            Vector3 eyeA = sideA.Count > 0 ? sideA.Aggregate(Vector3.zero, (acc, p) => acc + p) / sideA.Count : center + lateral * 0.032f;
            Vector3 eyeB = sideB.Count > 0 ? sideB.Aggregate(Vector3.zero, (acc, p) => acc + p) / sideB.Count : center - lateral * 0.032f;
            float sep = Vector3.Distance(eyeA, eyeB);
            float s = Mathf.Clamp(sep / 0.065f, 0.5f, 2f);

            GameObject face = new GameObject("FaceDetails");
            face.transform.SetPositionAndRotation(center, Quaternion.LookRotation(fwd, Vector3.up));
            face.transform.SetParent(head, true);

            Material socket = FaceMat("FaceSocket", new Color(0.13f, 0.15f, 0.2f, 0.9f), null, 3005, 0.1f);
            Material pupil = FaceMat("FacePupil", new Color(0.95f, 0.98f, 1f, 1f), new Color(0.85f, 0.95f, 1f) * 5f, 3010, 0.8f);
            Material brow = FaceMat("FaceBrow", new Color(0.55f, 0.55f, 0.55f, 0.95f), new Color(0.08f, 0.08f, 0.08f), 3006, 0.1f);
            Material nose = FaceMat("FaceNose", new Color(0.66f, 0.63f, 0.6f, 0.92f), new Color(0.12f, 0.12f, 0.12f), 3004, 0.3f);
            Material line = FaceMat("FaceLine", new Color(0.3f, 0.3f, 0.33f, 0.85f), null, 3006, 0.1f);

            foreach (Vector3 eyeWorld in new[] { eyeA, eyeB })
            {
                Vector3 lp = face.transform.InverseTransformPoint(eyeWorld);
                float side = lp.x >= 0f ? 1f : -1f;
                Part(PrimitiveType.Sphere, "EyeSocket", face.transform, lp + new Vector3(0f, 0f, 0.002f * s), new Vector3(0.032f, 0.021f, 0.01f) * s, Quaternion.identity, socket);
                Part(PrimitiveType.Sphere, "Pupil", face.transform, lp + new Vector3(0f, 0f, 0.008f * s), new Vector3(0.011f, 0.011f, 0.006f) * s, Quaternion.identity, pupil);
                Part(PrimitiveType.Cube, "Brow", face.transform, lp + new Vector3(0.002f * side * s, 0.022f * s, 0.007f * s), new Vector3(0.04f, 0.01f, 0.012f) * s, Quaternion.Euler(0f, 0f, -12f * side), brow);
                Part(PrimitiveType.Cube, "CrowsFeet", face.transform, lp + new Vector3(0.026f * side * s, -0.004f * s, 0.0f), new Vector3(0.012f, 0.0025f, 0.006f) * s, Quaternion.Euler(0f, 0f, -20f * side), line);
            }
            Part(PrimitiveType.Cube, "NoseBridge", face.transform, new Vector3(0f, -0.02f * s, 0.011f * s), new Vector3(0.014f, 0.03f, 0.016f) * s, Quaternion.Euler(-14f, 0f, 0f), nose);
            Part(PrimitiveType.Sphere, "NoseTip", face.transform, new Vector3(0f, -0.038f * s, 0.019f * s), new Vector3(0.022f, 0.017f, 0.02f) * s, Quaternion.identity, nose);
            Part(PrimitiveType.Cube, "ForeheadLine1", face.transform, new Vector3(0f, 0.042f * s, -0.002f * s), new Vector3(0.05f, 0.0025f, 0.006f) * s, Quaternion.identity, line);
            Part(PrimitiveType.Cube, "ForeheadLine2", face.transform, new Vector3(0f, 0.05f * s, -0.004f * s), new Vector3(0.04f, 0.0025f, 0.006f) * s, Quaternion.identity, line);
            Debug.Log("[InteractionSetup] Ghost face details added (eye separation " + sep.ToString("F3") + " m)");
        }

        static void ApplyGhostLook(GameObject ghost)
        {
            var table = new (string key, Material mat)[]
            {
                ("TunicDk", GhostMat("TunicDk", new Color(0.12f, 0.13f, 0.08f), 0.78f, new Color(0.066f, 0.072f, 0.069f), 1f, 0.15f)),
                ("Tunic", GhostMat("Tunic", new Color(0.2f, 0.25f, 0.29f), 0.78f, new Color(0.084f, 0.099f, 0.113f), 1f, 0.15f)),
                ("Trousers", GhostMat("Trousers", new Color(0.17f, 0.14f, 0.11f), 0.78f, new Color(0.075f, 0.074f, 0.074f), 1f, 0.1f)),
                ("LeatherDk", GhostMat("LeatherDk", new Color(0.1f, 0.065f, 0.045f), 0.78f, new Color(0.059f, 0.056f, 0.059f), 1f, 0.35f)),
                ("Leather", GhostMat("Leather", new Color(0.27f, 0.18f, 0.1f), 0.78f, new Color(0.1f, 0.083f, 0.072f), 1f, 0.35f)),
                ("Metal", GhostMat("Metal", new Color(0.5f, 0.5f, 0.46f), 0.78f, new Color(0.15f, 0.15f, 0.15f), 1f, 0.65f)),
                ("Lantern", GhostMat("Lantern", new Color(1f, 0.95f, 0.85f), 0.92f, new Color(1f, 0.92f, 0.75f), 4f, 0.6f)),
                ("Skin", GhostMat("Skin", new Color(0.55f, 0.52f, 0.5f), 0.78f, new Color(0.15f, 0.15f, 0.15f), 1f, 0.4f)),
                ("Eyes", GhostMat("Eyes", new Color(1f, 1f, 1f), 0.92f, new Color(0.85f, 0.95f, 1f), 4f, 0.6f)),
                ("Hair", GhostMat("Hair", new Color(0.72f, 0.72f, 0.7f), 0.78f, new Color(0.15f, 0.15f, 0.15f), 1f, 0.1f)),
                ("Scarf", GhostMat("Scarf", new Color(0.3f, 0.12f, 0.11f), 0.78f, new Color(0.108f, 0.072f, 0.074f), 1f, 0.15f)),
            };
            Vector3 lanternSum = Vector3.zero;
            int lanternCount = 0;
            SkinnedMeshRenderer lanternSkin = null;
            foreach (Renderer r in ghost.GetComponentsInChildren<Renderer>(true))
            {
                Material[] ms = r.sharedMaterials;
                for (int i = 0; i < ms.Length; i++)
                {
                    string n = ms[i] != null ? ms[i].name : "";
                    foreach (var entry in table)
                    {
                        if (!n.Contains(entry.key)) continue;
                        ms[i] = entry.mat;
                        if (entry.key == "Lantern" && r is SkinnedMeshRenderer smr && smr.sharedMesh != null && smr.sharedMesh.isReadable && i < smr.sharedMesh.subMeshCount)
                        {
                            Vector3[] verts = smr.sharedMesh.vertices;
                            foreach (int v in smr.sharedMesh.GetTriangles(i))
                            {
                                lanternSum += smr.transform.TransformPoint(verts[v]);
                                lanternCount++;
                            }
                            lanternSkin = smr;
                        }
                        break;
                    }
                }
                r.sharedMaterials = ms;
            }
            if (lanternCount > 0 && lanternSkin != null)
            {
                Vector3 lantern = lanternSum / lanternCount;
                Transform bone = lanternSkin.bones.Where(b => b != null).OrderBy(b => (b.position - lantern).sqrMagnitude).FirstOrDefault();
                GameObject lightGo = new GameObject("LanternLight");
                lightGo.transform.SetParent(bone != null ? bone : ghost.transform, true);
                lightGo.transform.position = lantern;
                Light l = lightGo.AddComponent<Light>();
                l.type = LightType.Point;
                l.color = new Color(1f, 0.88f, 0.7f);
                l.range = 3.5f;
                l.intensity = 1.8f;
                l.shadows = LightShadows.None;
                Debug.Log("[InteractionSetup] Ghost lantern light at " + lantern.ToString("F2") + " on bone " + (bone != null ? bone.name : "root"));
            }
            Debug.Log("[InteractionSetup] Ghost materials applied (translucent, colored)");
        }

        static void RestOnFloor(GameObject go, float floorY)
        {
            Bounds b = WorldBounds(go);
            go.transform.position += Vector3.up * (floorY + 0.002f - b.min.y);
        }

        static void BuildScene(LighterView lighter, GameObject[] mirrors, GameObject torchPrefab, AnimatorController ghostController)
        {
            GameObject old = GameObject.Find("Interaction_Props");
            if (old != null) Object.DestroyImmediate(old);
            GameObject props = new GameObject("Interaction_Props");

            float floorY = 0f;
            GameObject floor = GameObject.Find("PreviewFloor");
            if (floor != null)
            {
                if (floor.GetComponent<Collider>() == null) floor.AddComponent<MeshCollider>();
                if (floor.transform.localScale.x < 4f) floor.transform.localScale = new Vector3(4f, 1f, 4f);
                floorY = floor.transform.position.y;
            }

            GameObject torch = (GameObject)PrefabUtility.InstantiatePrefab(torchPrefab, props.transform);
            torch.transform.position = new Vector3(2.5f, floorY, 5f);
            GameObject beamTarget = new GameObject("BeamTarget");
            beamTarget.transform.SetParent(props.transform, false);
            beamTarget.transform.position = new Vector3(-1.5f, floorY, 1f);
            PuzzleTorch puzzleTorch = torch.GetComponent<PuzzleTorch>();
            puzzleTorch.beamAim = PuzzleTorch.BeamAim.TowardTarget;
            puzzleTorch.target = beamTarget.transform;
            puzzleTorch.keepBeamLevel = false;
            puzzleTorch.readInteractKey = false;
            EditorUtility.SetDirty(puzzleTorch);

            Vector3[] spots = { new Vector3(-1.5f, 0f, 3f), new Vector3(1.3f, 0f, 2.3f), new Vector3(-2.8f, 0f, 5.5f) };
            for (int i = 0; i < mirrors.Length; i++)
            {
                GameObject m = (GameObject)PrefabUtility.InstantiatePrefab(mirrors[i], props.transform);
                Vector3 front = m.GetComponent<MirrorPickup>().localFront;
                Quaternion rot;
                if (i == 0) rot = Quaternion.AngleAxis(25f, Vector3.up) * Quaternion.FromToRotation(front, Vector3.down);
                else if (i == 1) rot = Quaternion.AngleAxis(-40f, Vector3.up) * Quaternion.FromToRotation(front, Vector3.up);
                else rot = Quaternion.Euler(0f, 60f, 85f);
                m.transform.SetPositionAndRotation(spots[i] + Vector3.up * floorY, rot);
                RestOnFloor(m, floorY);
            }

            bool hasGhost = SceneManager.GetActiveScene().GetRootGameObjects().Any(g => g.name.StartsWith("GhostExplorer"));
            GameObject ghostModel = AssetDatabase.LoadAssetAtPath<GameObject>(GhostFbx);
            if (!hasGhost && ghostModel != null)
            {
                GameObject ghost = (GameObject)PrefabUtility.InstantiatePrefab(ghostModel, props.transform);
                ghost.transform.SetPositionAndRotation(new Vector3(-3.5f, floorY, 8f), Quaternion.Euler(0f, 160f, 0f));
                Animator animator = ghost.GetComponentInChildren<Animator>();
                if (animator == null) animator = ghost.AddComponent<Animator>();
                if (ghostController != null) animator.runtimeAnimatorController = ghostController;
                animator.applyRootMotion = false;
                ApplyGhostLook(ghost);
                AddFaceDetails(ghost);
                ghost.AddComponent<GhostPresence>();
                CapsuleCollider cc = ghost.AddComponent<CapsuleCollider>();
                cc.height = 1.8f;
                cc.radius = 0.3f;
                cc.center = new Vector3(0f, 0.9f, 0f);
                Debug.Log("[InteractionSetup] Ghost size " + WorldBounds(ghost).size.ToString("F2"));
            }

            FPCharacterMover mover = Object.FindAnyObjectByType<FPCharacterMover>(FindObjectsInactive.Include);
            if (mover == null)
            {
                Debug.LogError("[InteractionSetup] No FP Character Mover in the scene. Add it to FP_Character and run Tools > Interaction > Rebuild Interaction Setup.");
                return;
            }
            GameObject player = mover.gameObject;
            PlayerInteraction pi = player.GetComponent<PlayerInteraction>();
            if (pi == null) pi = player.AddComponent<PlayerInteraction>();
            pi.mover = mover;
            pi.character = Object.FindAnyObjectByType<FirstPersonCharacterAnimator>(FindObjectsInactive.Include);
            pi.viewCamera = mover.viewCamera;
            pi.lighterPrefab = lighter;
            EditorUtility.SetDirty(pi);
            if (Object.FindAnyObjectByType<FPLocomotionAnimator>(FindObjectsInactive.Include) == null) player.AddComponent<FPLocomotionAnimator>();
            foreach (FPCharacterPreview p in Object.FindObjectsByType<FPCharacterPreview>(FindObjectsInactive.Include))
            {
                p.enabled = false;
                EditorUtility.SetDirty(p);
            }
            Debug.Log("[InteractionSetup] Player '" + player.name + "' wired. Camera: " + (pi.viewCamera != null ? pi.viewCamera.name : "none (will use main camera)") + ", animator: " + (pi.character != null ? pi.character.name : "none"));
        }
    }
}
