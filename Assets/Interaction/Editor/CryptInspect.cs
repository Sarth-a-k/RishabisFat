using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FPCharacter.EditorTools
{
    [InitializeOnLoad]
    static class CryptInspect
    {
        const string TriggerPath = "Assets/Interaction/Editor/.run_inspect";

        static CryptInspect()
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

        static string PathOf(Transform t)
        {
            string p = t.name;
            while (t.parent != null) { t = t.parent; p = t.name + "/" + p; }
            return p;
        }

        static void Run()
        {
            var sb = new StringBuilder();
            try
            {
                Scene s = SceneManager.GetActiveScene();
                foreach (GameObject root in s.GetRootGameObjects())
                    foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
                    {
                        Bounds b = r.bounds;
                        bool nearW = b.max.x > 80f && b.min.x < 95f;
                        bool nearE = b.max.x > 129f && b.min.x < 145f;
                        if (!nearW && !nearE) continue;
                        if (Mathf.Abs(b.center.z) > 9f && b.size.z < 10f) continue;
                        string path = PathOf(r.transform);
                        if (path.Contains("THE EMBER CRYPT (lava)")) continue;
                        sb.AppendLine((r.gameObject.activeInHierarchy ? "ON  " : "OFF ") + path.Replace("THE FOURFOLD CITADEL • linear regional dungeon/", "") + " c=" + b.center.ToString("F1") + " s=" + b.size.ToString("F1"));
                    }
                Snap(new Vector3(83f, 0.2f, 0f), new Vector3(95f, -0.2f, 0f), "Backups/pass_w1.png", 75f);
                Snap(new Vector3(86.5f, -0.3f, 1.5f), new Vector3(92f, 3.5f, -1f), "Backups/pass_w2.png", 75f);
                Snap(new Vector3(141f, 0.0f, 0f), new Vector3(129f, -0.2f, 0f), "Backups/pass_e1.png", 75f);
                Snap(new Vector3(100f, -0.4f, 0f), new Vector3(88f, 0.5f, 0f), "Backups/pass_w3.png", 75f);
            }
            catch (Exception e) { sb.AppendLine("FAILED " + e); }
            File.WriteAllText("Backups/inspect.txt", sb.ToString());
        }

        static void Snap(Vector3 camPos, Vector3 target, string file, float fov)
        {
            GameObject go = new GameObject("SnapCam");
            go.hideFlags = HideFlags.HideAndDontSave;
            Camera cam = go.AddComponent<Camera>();
            cam.transform.position = camPos;
            cam.transform.LookAt(target);
            cam.fieldOfView = fov;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 200f;
            GameObject lg = new GameObject("SnapLamp");
            lg.hideFlags = HideFlags.HideAndDontSave;
            lg.transform.position = camPos;
            Light l = lg.AddComponent<Light>();
            l.type = LightType.Point; l.range = 25f; l.intensity = 3f;
            RenderTexture rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            Texture2D tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            File.WriteAllBytes(file, tex.EncodeToPNG());
            cam.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(rt);
            UnityEngine.Object.DestroyImmediate(tex);
            UnityEngine.Object.DestroyImmediate(go);
            UnityEngine.Object.DestroyImmediate(lg);
        }
    }
}
