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
    static class TunnelShadesSetup
    {
        const string TriggerPath = "Assets/Interaction/Editor/.run_tunnelshades";
        const string ReportPath = "Backups/tunnelshades_report.txt";
        const string MapScene = "Assets/Scenes/FourfoldCitadel_WithOurStuff.unity";
        const string Dir = "Assets/Interaction/Generated/WhiteRegion";

        static readonly string[] Names = { "Ivory", "Pearl", "Ash", "Bone", "Cool", "Smoke" };
        static readonly Color[] Lit = { new Color(0.72f, 0.70f, 0.66f), new Color(0.66f, 0.67f, 0.71f), new Color(0.55f, 0.56f, 0.59f), new Color(0.64f, 0.61f, 0.56f), new Color(0.58f, 0.62f, 0.69f), new Color(0.44f, 0.45f, 0.48f) };
        static readonly Color[] Shade = { new Color(0.30f, 0.28f, 0.26f), new Color(0.27f, 0.28f, 0.32f), new Color(0.21f, 0.22f, 0.25f), new Color(0.27f, 0.24f, 0.21f), new Color(0.21f, 0.24f, 0.30f), new Color(0.16f, 0.17f, 0.20f) };

        static TunnelShadesSetup()
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

        [MenuItem("Tools/Interaction/Tunnel Shades Of White")]
        static void Run()
        {
            var log = new StringBuilder();
            try
            {
                Scene active = SceneManager.GetActiveScene();
                ZoneMusic zmOpen = UnityEngine.Object.FindAnyObjectByType<ZoneMusic>();
                bool stale = active.path == MapScene && zmOpen != null && zmOpen.zones.Count == 0;
                if (!stale) EditorSceneManager.SaveOpenScenes();
                File.Copy(MapScene, "Backups/FourfoldCitadel_WithOurStuff_before_tunnelshades3.unity", true);
                Scene map = EditorSceneManager.OpenScene(MapScene, OpenSceneMode.Single);

                GameObject root = GameObject.Find("Ring Tunnel");
                if (root == null) throw new Exception("Ring Tunnel not found");
                float floorY = 0f;
                Transform floorT = root.transform.Find("Tunnel floor");
                if (floorT != null) floorY = floorT.GetComponent<Renderer>().bounds.min.y;

                var mats = new Material[Names.Length];
                for (int i = 0; i < Names.Length; i++) mats[i] = Mat("Tunnel_" + Names[i], Lit[i], Shade[i], floorY, 0.35f, 0f);
                Material floorMat = Mat("Tunnel_Floor", new Color(0.40f, 0.40f, 0.43f), new Color(0.22f, 0.22f, 0.25f), floorY, 0.2f, 0f);

                var rng = new System.Random(19);
                int rings = 0;
                var counts = new int[Names.Length];
                foreach (Transform t in root.transform)
                {
                    if (!t.name.StartsWith("Ring ")) continue;
                    int i = int.Parse(t.name.Substring(5));
                    double wave = Math.Sin(i * 0.42) * 1.6 + Math.Sin(i * 0.17 + 1.3) * 1.4 + (rng.NextDouble() - 0.5) * 1.6;
                    int[] order = { 5, 2, 4, 1, 0, 3 };
                    int k = order[Mathf.Clamp((int)Math.Round(2.5 + wave), 0, 5)];
                    t.GetComponent<MeshRenderer>().sharedMaterial = mats[k];
                    counts[k]++;
                    rings++;
                }
                if (floorT != null)
                {
                    floorT.GetComponent<MeshRenderer>().sharedMaterial = floorMat;
                    floorT.localPosition = new Vector3(0f, 0.03f, 0f);
                }
                int removed = 0;
                var kill = new System.Collections.Generic.List<GameObject>();
                foreach (Transform t in root.transform) if (t.name.StartsWith("Black torch") || t.name.StartsWith("Tunnel light")) kill.Add(t.gameObject);
                foreach (GameObject g in kill) { if (g.name.StartsWith("Black torch")) removed++; UnityEngine.Object.DestroyImmediate(g); }
                int lights = 0;
                var ringList = new System.Collections.Generic.List<Transform>();
                foreach (Transform t in root.transform) if (t.name.StartsWith("Ring ")) ringList.Add(t);
                ringList.Sort((x, y) => int.Parse(x.name.Substring(5)).CompareTo(int.Parse(y.name.Substring(5))));
                for (int i = 3; i < ringList.Count; i += 7)
                {
                    var lg = new GameObject("Tunnel light " + lights);
                    lg.transform.SetParent(root.transform, false);
                    lg.transform.position = ringList[i].position + Vector3.up * 3.2f;
                    Light l = lg.AddComponent<Light>();
                    l.type = LightType.Point;
                    l.color = new Color(0.85f, 0.88f, 0.95f);
                    l.intensity = 2.4f;
                    l.range = 11f;
                    l.shadows = LightShadows.None;
                    lights++;
                }
                log.AppendLine("black torches removed " + removed + ", tunnel lights " + lights);
                for (int i = 0; i < Names.Length; i++) log.AppendLine(Names[i] + " rings: " + counts[i]);
                log.AppendLine("rings shaded " + rings + ", floor y " + floorY);

                EditorSceneManager.MarkSceneDirty(map);
                EditorSceneManager.SaveScene(map);
                ZoneMusic zm = UnityEngine.Object.FindAnyObjectByType<ZoneMusic>();
                log.AppendLine("zone music areas: " + (zm != null ? zm.zones.Count.ToString() : "none"));

                Snapshot(new Vector3(149.1f, floorY + 1.7f, 0f), new Vector3(160f, floorY + 1.8f, 0f), "Backups/tunnelshades_0.png");
                Transform r20 = root.transform.Find("Ring 14");
                if (r20 != null) Snapshot(r20.position + Vector3.up * 1.7f, r20.position + r20.forward * 10f + Vector3.up * 1.7f, "Backups/tunnelshades_1.png");
                log.AppendLine("DONE");
            }
            catch (Exception e)
            {
                log.AppendLine("FAILED: " + e);
            }
            File.WriteAllText(ReportPath, log.ToString());
        }

        static Material Mat(string name, Color lit, Color shade, float floorY, float corner, float far)
        {
            string path = Dir + "/" + name + ".mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("CasaFX/SoftWhite")); AssetDatabase.CreateAsset(m, path); }
            m.SetColor("_Light", lit);
            m.SetColor("_Shade", shade);
            m.SetVector("_Dir", new Vector4(-0.6f, 0.55f, 0.3f, 0f));
            m.SetFloat("_FarBright", far);
            m.SetFloat("_FarDist", 45f);
            m.SetFloat("_FloorY", floorY);
            m.SetFloat("_Corner", corner);
            EditorUtility.SetDirty(m);
            AssetDatabase.SaveAssets();
            return m;
        }

        static void Snapshot(Vector3 from, Vector3 at, string path)
        {
            var go = new GameObject("Snap Cam");
            Camera cam = go.AddComponent<Camera>();
            cam.fieldOfView = 70f;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 200f;
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

