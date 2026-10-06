using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace FPCharacter.EditorTools
{
    [InitializeOnLoad]
    static class NotePrepSetup
    {
        const string TriggerPath = "Assets/Interaction/Editor/.run_noteprep";
        const string ReportPath = "Backups/noteprep_report.txt";

        static NotePrepSetup()
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

        [MenuItem("Tools/Interaction/Prepare And Preview Notes")]
        static void Run()
        {
            var log = new StringBuilder();
            try
            {
                foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Resources/Notes" }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    var ti = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (ti == null) continue;
                    bool normal = path.Contains("_Normal");
                    bool changed = false;
                    if (normal && ti.textureType != TextureImporterType.NormalMap) { ti.textureType = TextureImporterType.NormalMap; changed = true; }
                    int max = normal ? 1024 : 2048;
                    if (ti.maxTextureSize != max) { ti.maxTextureSize = max; changed = true; }
                    if (!normal && !ti.alphaIsTransparency) { ti.alphaIsTransparency = true; changed = true; }
                    if (changed) { ti.SaveAndReimport(); log.AppendLine("texture set: " + path); }
                }
                foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { "Assets/Resources/Notes" }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    var mi = AssetImporter.GetAtPath(path) as ModelImporter;
                    if (mi == null) continue;
                    if (mi.materialImportMode != ModelImporterMaterialImportMode.None) { mi.materialImportMode = ModelImporterMaterialImportMode.None; mi.SaveAndReimport(); }
                    GameObject m = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    Bounds b = new Bounds();
                    bool any = false;
                    foreach (MeshFilter f in m.GetComponentsInChildren<MeshFilter>(true))
                    {
                        if (f.sharedMesh == null) continue;
                        if (!any) { b = f.sharedMesh.bounds; any = true; } else b.Encapsulate(f.sharedMesh.bounds);
                    }
                    log.AppendLine("model " + path + " mesh bounds " + b.size.ToString("F3") + " root scale " + m.transform.localScale.ToString("F2") + " rot " + m.transform.localEulerAngles.ToString("F0"));
                }

                var made = NoteSetup.SpawnAll();
                log.AppendLine("notes placed in preview: " + made.Count);
                int i = 0;
                foreach (GameObject n in made)
                {
                    n.hideFlags = HideFlags.DontSave;
                    Bounds nb = new Bounds(n.transform.position, Vector3.zero);
                    foreach (Renderer r in n.GetComponentsInChildren<Renderer>()) nb.Encapsulate(r.bounds);
                    log.AppendLine(n.name + " at " + n.transform.position.ToString("F3") + " size " + nb.size.ToString("F3"));
                    Vector3 c = nb.center;
                    Snap(c + Vector3.up * 0.45f, c, n.transform.forward, "Backups/note_" + i + "_top.png", 40f);
                    Vector3 side = Vector3.ProjectOnPlane(-n.transform.forward, Vector3.up).normalized;
                    Snap(c + side * 1.4f + Vector3.up * 1.0f, c, Vector3.up, "Backups/note_" + i + "_context.png", 55f);
                    i++;
                }
                foreach (GameObject n in made) UnityEngine.Object.DestroyImmediate(n);
                log.AppendLine("DONE");
            }
            catch (Exception e) { log.AppendLine("FAILED: " + e); }
            File.WriteAllText(ReportPath, log.ToString());
        }

        static void Snap(Vector3 from, Vector3 at, Vector3 up, string path, float fov)
        {
            var go = new GameObject("Snap Cam") { hideFlags = HideFlags.DontSave };
            Camera cam = go.AddComponent<Camera>();
            cam.fieldOfView = fov;
            cam.nearClipPlane = 0.02f;
            go.transform.SetPositionAndRotation(from, Quaternion.LookRotation(at - from, up));
            var lg = new GameObject("Snap light") { hideFlags = HideFlags.DontSave };
            Light l = lg.AddComponent<Light>(); l.type = LightType.Point; l.range = 4f; l.intensity = 2f; l.color = new Color(1f, 0.9f, 0.8f);
            lg.transform.position = from + Vector3.up * 0.3f;
            var rt = new RenderTexture(960, 720, 24);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(960, 720, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 960, 720), 0, 0);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            RenderTexture.active = null;
            cam.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(tex);
            rt.Release();
            UnityEngine.Object.DestroyImmediate(rt);
            UnityEngine.Object.DestroyImmediate(go);
            UnityEngine.Object.DestroyImmediate(lg);
        }
    }
}
