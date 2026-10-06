using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FPCharacter.EditorTools
{
    [InitializeOnLoad]
    static class EclipseSurvey
    {
        const string TriggerPath = "Assets/Interaction/Editor/.run_eclipsesurvey";
        const string ReportPath = "Backups/eclipsesurvey_report.txt";
        const string MapScene = "Assets/Scenes/FourfoldCitadel_WithOurStuff.unity";

        static EclipseSurvey()
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

        static void Run()
        {
            var log = new StringBuilder();
            try
            {
                Scene active = SceneManager.GetActiveScene();
                if (active.path != MapScene)
                {
                    EditorSceneManager.SaveOpenScenes();
                    EditorSceneManager.OpenScene(MapScene, OpenSceneMode.Single);
                }
                Transform region = null;
                foreach (Transform t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
                    if (t.name.StartsWith("04 ")) { region = t; break; }
                if (region == null) throw new Exception("region 4 not found");
                Bounds rb = new Bounds(region.position, Vector3.zero); bool any = false;
                foreach (Renderer r in region.GetComponentsInChildren<Renderer>()) { if (!any) { rb = r.bounds; any = true; } else rb.Encapsulate(r.bounds); }
                log.AppendLine("region root " + region.position + " rot " + region.eulerAngles + " scale " + region.lossyScale);
                log.AppendLine("region bounds centre " + rb.center + " size " + rb.size);
                string[] keys = { "Throne silhouette", "Royal dais", "Black sun throne seal", "Eclipse crown spike", "IV  /  ECLIPSE KEEP", "Keep dais approach", "Regional puzzle dais", "PuzzleSocket_4", "Pointed portal", "Torch", "Side stair" };
                foreach (Transform t in region.GetComponentsInChildren<Transform>(true))
                {
                    foreach (string k in keys)
                    {
                        if (!t.name.StartsWith(k)) continue;
                        Renderer r = t.GetComponent<Renderer>();
                        string b = r != null ? " bounds c " + r.bounds.center.ToString("F2") + " s " + r.bounds.size.ToString("F2") : "";
                        string mat = r != null && r.sharedMaterial != null ? " mat " + r.sharedMaterial.name : "";
                        log.AppendLine(t.name + " pos " + t.position.ToString("F2") + " rot " + t.eulerAngles.ToString("F0") + " scale " + t.lossyScale.ToString("F2") + b + mat);
                        break;
                    }
                }
                foreach (Transform t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
                {
                    if (t.name == "Skeleton alcove" || t.name.StartsWith("InfraredGoggles"))
                    {
                        log.AppendLine("== " + t.name + " pos " + t.position.ToString("F2"));
                        foreach (Transform c in t.GetComponentsInChildren<Transform>(true))
                        {
                            Renderer r = c.GetComponent<Renderer>();
                            if (r == null) continue;
                            log.AppendLine("   " + c.name + " pos " + c.position.ToString("F2") + " bounds " + r.bounds.size.ToString("F2") + " mesh " + (c.GetComponent<MeshFilter>() != null && c.GetComponent<MeshFilter>().sharedMesh != null ? c.GetComponent<MeshFilter>().sharedMesh.name : "-") + " mat " + (r.sharedMaterial != null ? r.sharedMaterial.name : "-"));
                        }
                    }
                }
                Vector3 c0 = rb.center;
                var lg = new GameObject("Survey light");
                Light l = lg.AddComponent<Light>(); l.type = LightType.Point; l.range = 80f; l.intensity = 25f; lg.transform.position = c0 + Vector3.up * 6f;
                Snapshot(new Vector3(c0.x, c0.y - rb.extents.y + 2f, c0.z - rb.extents.z + 3f), new Vector3(c0.x, c0.y - rb.extents.y + 4f, c0.z + rb.extents.z), "Backups/eclipsesurvey_0.png");
                Snapshot(new Vector3(c0.x + rb.extents.x - 3f, c0.y + 6f, c0.z), new Vector3(c0.x - 5f, c0.y - 3f, c0.z + 5f), "Backups/eclipsesurvey_1.png");
                UnityEngine.Object.DestroyImmediate(lg);
                log.AppendLine("DONE (nothing saved)");
            }
            catch (Exception e)
            {
                log.AppendLine("FAILED: " + e);
            }
            File.WriteAllText(ReportPath, log.ToString());
        }

        static void Snapshot(Vector3 from, Vector3 at, string path)
        {
            var go = new GameObject("Snap Cam");
            Camera cam = go.AddComponent<Camera>();
            cam.fieldOfView = 75f;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 300f;
            go.transform.SetPositionAndRotation(from, Quaternion.LookRotation(at - from, Vector3.up));
            var rt = new RenderTexture(1280, 720, 24);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            RenderTexture.active = null;
            cam.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(tex);
            rt.Release();
            UnityEngine.Object.DestroyImmediate(rt);
            UnityEngine.Object.DestroyImmediate(go);
        }
    }
}
