using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace FPCharacter.EditorTools
{
    [InitializeOnLoad]
    public static class SkeletonGogglesSetup
    {
        const string TriggerPath = "Assets/Interaction/Editor/.run_skeleton";
        const string Dir = "Assets/Environment/Skeleton";
        const string SkelFbx = Dir + "/Skeleton_Sitting.fbx";
        const string GogFbx = Dir + "/InfraredGoggles.fbx";
        const string GenDir = "Assets/Interaction/Skeleton";
        const string RootName = "SkeletonWithGoggles";
        static readonly Vector3 Position = new Vector3(0.5f, 0f, 15f);
        static readonly float Yaw = 180f;

        static SkeletonGogglesSetup()
        {
            if (!File.Exists(TriggerPath) || File.ReadAllText(TriggerPath).Trim() != "pending") return;
            EditorApplication.update += WaitAndRun;
        }

        static void WaitAndRun()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            EditorApplication.update -= WaitAndRun;
            Run();
            File.WriteAllText(TriggerPath, "done");
        }

        [MenuItem("Tools/Interaction/Rebuild Skeleton And Goggles")]
        public static void Run()
        {
            try
            {
                Scene scene = SceneManager.GetActiveScene();
                if (scene.path != "Assets/GameTest.unity")
                {
                    if (scene.isDirty && !string.IsNullOrEmpty(scene.path)) EditorSceneManager.SaveScene(scene);
                    scene = EditorSceneManager.OpenScene("Assets/GameTest.unity", OpenSceneMode.Single);
                }
                else if (scene.isDirty) EditorSceneManager.SaveScene(scene);
                Directory.CreateDirectory("Backups");
                File.Copy("Assets/GameTest.unity", "Backups/GameTest_before_skeleton_v2.unity", true);
                EnsureFolder(GenDir);

                Color boot = new Color(20f / 255f, 22f / 255f, 24f / 255f);
                Material bone = Mat("Skel_Bone", new Color(0.82f, 0.77f, 0.66f), 0f, 0.15f);
                Material hollow = Mat("Skel_Hollow", new Color(0.05f, 0.04f, 0.035f), 0f, 0.05f);
                Material bootM = Mat("Skel_Boot", boot, 0.1f, 0.35f);
                Material sole = Mat("Skel_BootSole", new Color(0.04f, 0.04f, 0.045f), 0f, 0.2f);
                Material hat = Mat("Skel_Hat", new Color(0.4f, 0.27f, 0.16f), 0f, 0.15f);
                Material wood = Mat("Crate_Wood", new Color(0.45f, 0.32f, 0.2f), 0f, 0.15f);
                Material woodDk = Mat("Crate_WoodDark", new Color(0.29f, 0.2f, 0.13f), 0f, 0.1f);
                Material iron = Mat("Crate_Iron", new Color(0.22f, 0.22f, 0.24f), 0.7f, 0.45f);
                Material gFrame = Mat("Goggles_Frame", new Color(0.18f, 0.18f, 0.2f), 0.6f, 0.55f);
                Material gLens = Mat("Goggles_Lens", new Color(0.55f, 0.06f, 0.05f), 0f, 0.9f);
                gLens.EnableKeyword("_EMISSION");
                gLens.SetColor("_EmissionColor", new Color(1f, 0.08f, 0.03f) * 1.4f);
                gLens.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                Material gStrap = Mat("Goggles_Strap", new Color(0.32f, 0.22f, 0.14f), 0f, 0.15f);
                Material gBrass = Mat("Goggles_Brass", new Color(0.72f, 0.55f, 0.28f), 0.9f, 0.6f);
                Material Pick(string n)
                {
                    if (n.Contains("Skel_BootSole")) return sole;
                    if (n.Contains("Skel_Boot")) return bootM;
                    if (n.Contains("Skel_Bone")) return bone;
                    if (n.Contains("Skel_Hollow")) return hollow;
                    if (n.Contains("Skel_Hat")) return hat;
                    if (n.Contains("Crate_WoodDark")) return woodDk;
                    if (n.Contains("Crate_Wood")) return wood;
                    if (n.Contains("Crate_Iron")) return iron;
                    if (n.Contains("Goggles_Frame")) return gFrame;
                    if (n.Contains("Goggles_Lens")) return gLens;
                    if (n.Contains("Goggles_Strap")) return gStrap;
                    if (n.Contains("Goggles_Brass")) return gBrass;
                    return null;
                }

                GameObject gogModel = AssetDatabase.LoadAssetAtPath<GameObject>(GogFbx);
                GameObject skelModel = AssetDatabase.LoadAssetAtPath<GameObject>(SkelFbx);
                if (gogModel == null || skelModel == null) throw new Exception("Skeleton or goggles model not imported yet");

                GameObject fp = Object.Instantiate(gogModel);
                fp.name = "Goggles_FP";
                Remap(fp, Pick);
                foreach (Renderer r in fp.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = ShadowCastingMode.Off;
                GameObject fpPrefab = PrefabUtility.SaveAsPrefabAsset(fp, "Assets/Interaction/Prefabs/Goggles_FP.prefab");
                Object.DestroyImmediate(fp);

                GameObject old = GameObject.Find(RootName);
                if (old != null) Object.DestroyImmediate(old);
                GameObject root = new GameObject(RootName);
                root.transform.SetPositionAndRotation(Position, Quaternion.Euler(0f, Yaw, 0f));

                GameObject skel = Object.Instantiate(skelModel, root.transform);
                skel.name = "Skeleton";
                skel.transform.localPosition = Vector3.zero;
                skel.transform.localRotation = Quaternion.identity;
                Remap(skel, Pick);
                foreach (MeshRenderer mr in skel.GetComponentsInChildren<MeshRenderer>(true))
                {
                    Bounds lb = mr.GetComponent<MeshFilter>().sharedMesh.bounds;
                    BoxCollider bc = mr.gameObject.AddComponent<BoxCollider>();
                    bc.center = lb.center;
                    bc.size = mr.name.StartsWith("Crate") ? lb.size : new Vector3(lb.size.x, lb.size.y * 0.6f, lb.size.z * 0.6f);
                }

                Bounds sb = new Bounds(root.transform.position, Vector3.zero);
                foreach (Renderer r in skel.GetComponentsInChildren<Renderer>(true)) if (r.name.StartsWith("Skeleton")) sb = r.bounds;
                GameObject gog = Object.Instantiate(gogModel, root.transform);
                gog.name = "InfraredGoggles_Pickup";
                Remap(gog, Pick);
                Vector3 side = root.transform.right;
                Vector3 fwd = root.transform.forward;
                gog.transform.position = new Vector3(sb.center.x, root.transform.position.y + 0.02f, sb.center.z) - side * (sb.extents.x + 0.12f) + fwd * 0.15f;
                gog.transform.rotation = Quaternion.LookRotation(Vector3.up, fwd) * Quaternion.Euler(0f, 0f, 30f);
                Bounds gb = new Bounds(gog.transform.position, Vector3.zero);
                foreach (Renderer r in gog.GetComponentsInChildren<Renderer>(true)) gb.Encapsulate(r.bounds);
                gog.transform.position += Vector3.up * (root.transform.position.y + 0.005f - gb.min.y);
                foreach (MeshFilter mf in gog.GetComponentsInChildren<MeshFilter>(true))
                {
                    BoxCollider bc = mf.gameObject.AddComponent<BoxCollider>();
                    bc.center = mf.sharedMesh.bounds.center;
                    bc.size = mf.sharedMesh.bounds.size + Vector3.one * 0.06f;
                }
                gog.AddComponent<InfraredGogglesPickup>();

                PlayerInteraction pi = Object.FindAnyObjectByType<PlayerInteraction>(FindObjectsInactive.Include);
                if (pi != null)
                {
                    pi.gogglesViewPrefab = fpPrefab;
                    pi.hasGoggles = false;
                    EditorUtility.SetDirty(pi);
                }

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                GameObject pc = Object.Instantiate(root);
                pc.name = RootName;
                pc.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                PrefabUtility.SaveAsPrefabAsset(pc, "Assets/Interaction/Prefabs/" + RootName + ".prefab");
                Object.DestroyImmediate(pc);
                AssetDatabase.SaveAssets();
                Debug.Log("[Skeleton] DONE at " + Position + ". Skeleton size " + sb.size.ToString("F2") + ", goggles at " + gog.transform.position.ToString("F2") + ", player wired: " + (pi != null));
            }
            catch (Exception e)
            {
                Debug.LogError("[Skeleton] FAILED: " + e);
            }
        }

        static void Remap(GameObject go, Func<string, Material> pick)
        {
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
            {
                Material[] ms = r.sharedMaterials;
                for (int i = 0; i < ms.Length; i++)
                {
                    Material m = ms[i] != null ? pick(ms[i].name) : null;
                    if (m != null) ms[i] = m;
                }
                r.sharedMaterials = ms;
            }
        }

        static Material Mat(string name, Color c, float metallic, float smoothness)
        {
            string path = GenDir + "/" + name + ".mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                Shader s = Shader.Find("Universal Render Pipeline/Lit");
                m = new Material(s != null ? s : Shader.Find("Standard"));
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Metallic", metallic);
            m.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(m);
            return m;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
