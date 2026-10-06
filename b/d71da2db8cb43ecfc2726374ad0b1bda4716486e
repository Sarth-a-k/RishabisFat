using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace FPCharacter.EditorTools
{
    [InitializeOnLoad]
    static class RemainsSetup
    {
        const string TriggerPath = "Assets/Interaction/Editor/.run_remains";
        const string ModelDir = "Assets/Environment/Skeleton";
        const string PrefabDir = "Assets/Interaction/Prefabs";
        const string MatDir = "Assets/Interaction/Generated/Remains";

        static RemainsSetup()
        {
            EditorApplication.delayCall += Check;
        }

        static void Check()
        {
            if (!File.Exists(TriggerPath) || File.ReadAllText(TriggerPath).Trim() != "pending") return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.delayCall += Check;
                return;
            }
            File.WriteAllText(TriggerPath, "done");
            Run();
        }

        static Material Mat(string name, Color c, float smooth)
        {
            string path = MatDir + "/" + name + ".mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(m);
            return m;
        }

        [MenuItem("Tools/Interaction/Rebuild Skeleton Remains")]
        static void Run()
        {
            var sb = new StringBuilder();
            try
            {
                Directory.CreateDirectory(MatDir);
                Material bone = Mat("Remains_Bone", new Color(0.72f, 0.64f, 0.5f), 0.15f);
                Material hollow = Mat("Remains_Hollow", new Color(0.04f, 0.03f, 0.025f), 0.05f);
                Material dirt = Mat("Remains_Dirt", new Color(0.3f, 0.22f, 0.15f), 0.05f);
                Material stone = Mat("Remains_Stone", new Color(0.22f, 0.21f, 0.2f), 0.1f);
                foreach (string name in new[] { "SkeletonRemains", "SkeletonRemains_Grave", "BonePile" })
                {
                    string path = ModelDir + "/" + name + ".fbx";
                    ModelImporter mi = (ModelImporter)AssetImporter.GetAtPath(path);
                    if (mi == null) { sb.AppendLine("missing " + path); continue; }
                    mi.animationType = ModelImporterAnimationType.None;
                    mi.importAnimation = false;
                    mi.avatarSetup = ModelImporterAvatarSetup.NoAvatar;
                    mi.importBlendShapes = false;
                    mi.importCameras = false;
                    mi.importLights = false;
                    mi.isReadable = false;
                    mi.materialImportMode = ModelImporterMaterialImportMode.None;
                    mi.SaveAndReimport();
                    GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    GameObject root = new GameObject(name);
                    GameObject vis = (GameObject)UnityEngine.Object.Instantiate(model, root.transform);
                    vis.name = name + "_Mesh (locked)";
                    int meshes = 0;
                    foreach (MeshRenderer mr in vis.GetComponentsInChildren<MeshRenderer>(true))
                    {
                        Material[] arr = new Material[mr.sharedMaterials.Length];
                        for (int i = 0; i < arr.Length; i++)
                            arr[i] = i == 0 ? bone : i == 1 ? hollow : i == 2 ? dirt : stone;
                        mr.sharedMaterials = arr;
                        mr.shadowCastingMode = ShadowCastingMode.On;
                        meshes++;
                    }
                    Bounds b = new Bounds(root.transform.position, Vector3.zero);
                    bool first = true;
                    foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
                    {
                        if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds);
                    }
                    BoxCollider bc = root.AddComponent<BoxCollider>();
                    bc.center = b.center - root.transform.position;
                    bc.size = new Vector3(b.size.x, Mathf.Max(0.05f, b.size.y), b.size.z);
                    foreach (Transform t in vis.GetComponentsInChildren<Transform>(true))
                    {
                        t.gameObject.hideFlags = HideFlags.NotEditable;
                        t.hideFlags = HideFlags.NotEditable;
                    }
                    foreach (Component c in vis.GetComponentsInChildren<Component>(true))
                        if (c != null) c.hideFlags |= HideFlags.NotEditable;
                    GameObjectUtility.SetStaticEditorFlags(vis, StaticEditorFlags.BatchingStatic);
                    string prefabPath = PrefabDir + "/" + name + ".prefab";
                    PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                    UnityEngine.Object.DestroyImmediate(root);
                    sb.AppendLine(name + ": prefab saved, meshes " + meshes + ", size " + b.size.ToString("F2") + ", rig: none");
                }
                AssetDatabase.SaveAssets();
                sb.AppendLine("DONE");
            }
            catch (Exception e) { sb.AppendLine("FAILED " + e); }
            File.WriteAllText("Backups/remains_report.txt", sb.ToString());
        }
    }
}
