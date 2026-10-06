using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace FPCharacter.EditorTools
{
    [InitializeOnLoad]
    static class GhostHatPreview
    {
        const string TriggerPath = "Assets/Interaction/Editor/.run_ghosthat";
        const string ReportPath = "Backups/ghosthat_report.txt";

        static GhostHatPreview()
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

        [MenuItem("Tools/Interaction/Preview Ghost Hat")]
        static void Run()
        {
            var log = new StringBuilder();
            try
            {
                GameObject npc = GameObject.Find(GhostHat.NpcName);
                if (npc == null) { log.AppendLine("npc not found in the open scene"); File.WriteAllText(ReportPath, log.ToString()); return; }
                GameObject hat = GhostHat.Attach(npc);
                if (hat == null) { log.AppendLine("no Head bone"); File.WriteAllText(ReportPath, log.ToString()); return; }
                hat.hideFlags = HideFlags.DontSave;
                Bounds hb = new Bounds(hat.transform.position, Vector3.zero);
                foreach (Renderer r in hat.GetComponentsInChildren<Renderer>()) hb.Encapsulate(r.bounds);
                log.AppendLine("hat at " + hat.transform.position.ToString("F3") + " size " + hb.size.ToString("F3"));
                Vector3 c = hb.center;
                Vector3 f = Vector3.ProjectOnPlane(npc.transform.forward, Vector3.up).normalized;
                Vector3 side = Vector3.Cross(Vector3.up, f);
                Snap(c + f * 1.1f + Vector3.up * 0.05f - Vector3.up * 0.25f, c - Vector3.up * 0.3f, "Backups/ghosthat_front.png");
                Snap(c + side * 1.1f + f * 0.2f, c - Vector3.up * 0.3f, "Backups/ghosthat_side.png");
                Snap(c + f * 2.6f + side * 0.8f + Vector3.up * 0.1f, c - Vector3.up * 0.7f, "Backups/ghosthat_wide.png");
                Object.DestroyImmediate(hat);
                log.AppendLine("DONE");
            }
            catch (System.Exception e) { log.AppendLine("FAILED: " + e); }
            File.WriteAllText(ReportPath, log.ToString());
        }

        static void Snap(Vector3 from, Vector3 at, string path)
        {
            var go = new GameObject("Snap Cam") { hideFlags = HideFlags.DontSave };
            Camera cam = go.AddComponent<Camera>();
            cam.fieldOfView = 50f;
            cam.nearClipPlane = 0.02f;
            go.transform.SetPositionAndRotation(from, Quaternion.LookRotation(at - from, Vector3.up));
            var lg = new GameObject("Snap light") { hideFlags = HideFlags.DontSave };
            Light l = lg.AddComponent<Light>(); l.type = LightType.Point; l.range = 6f; l.intensity = 2.5f; l.color = new Color(1f, 0.85f, 0.7f);
            lg.transform.position = from + Vector3.up * 0.4f;
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
            Object.DestroyImmediate(tex);
            rt.Release();
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(lg);
        }
    }
}
