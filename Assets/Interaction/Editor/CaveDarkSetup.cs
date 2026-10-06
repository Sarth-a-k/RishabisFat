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
    static class CaveDarkSetup
    {
        const string TriggerPath = "Assets/Interaction/Editor/.run_cavedark";
        const string ReportPath = "Backups/cavedark_report.txt";
        const string MapScene = "Assets/Scenes/FourfoldCitadel_WithOurStuff.unity";
        const string CrystalMat = "Assets/Interaction/Generated/LunarStyle/Materials/LunarCave_Crystal.mat";

        static CaveDarkSetup()
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

        [MenuItem("Tools/Interaction/Darken Lunar Cave")]
        static void Run()
        {
            var log = new StringBuilder();
            try
            {
                Scene active = SceneManager.GetActiveScene();
                ZoneMusic zmOpen = UnityEngine.Object.FindAnyObjectByType<ZoneMusic>();
                bool stale = active.path == MapScene && zmOpen != null && zmOpen.zones.Count == 0;
                if (!stale) EditorSceneManager.SaveOpenScenes();
                Directory.CreateDirectory("Backups");
                File.Copy(MapScene, "Backups/FourfoldCitadel_WithOurStuff_before_cavedark.unity", true);
                Scene map = EditorSceneManager.OpenScene(MapScene, OpenSceneMode.Single);

                GameObject root = GameObject.Find("Lunar Cave Dressing");
                if (root == null) throw new Exception("Lunar Cave Dressing not found");
                int fills = 0, glows = 0;
                foreach (Light l in root.GetComponentsInChildren<Light>(true))
                {
                    if (l.name == "Cave moonlight") { l.intensity = 3f; l.range = 24f; l.color = new Color(0.5f, 0.6f, 1f); fills++; }
                    else if (l.name == "Crystal glow") { l.intensity = 1.1f; l.range = 4f; glows++; }
                }
                Material cm = AssetDatabase.LoadAssetAtPath<Material>(CrystalMat);
                if (cm != null)
                {
                    cm.SetColor("_EmissionColor", new Color(0.35f, 0.5f, 0.95f) * 0.55f);
                    cm.SetColor("_BaseColor", new Color(0.55f, 0.62f, 0.8f));
                    EditorUtility.SetDirty(cm);
                    AssetDatabase.SaveAssets();
                }
                log.AppendLine("moonlight fills " + fills + " -> 3, crystal lights " + glows + " -> 1.1, crystal emission dimmed " + (cm != null));

                EditorSceneManager.MarkSceneDirty(map);
                EditorSceneManager.SaveScene(map);
                ZoneMusic zm = UnityEngine.Object.FindAnyObjectByType<ZoneMusic>();
                log.AppendLine("zone music areas: " + (zm != null ? zm.zones.Count.ToString() : "none"));

                Snapshot(new Vector3(27.5f, 1.7f, 0f), new Vector3(50f, 1.2f, 2f), "Backups/cavedark_0.png", false);
                Snapshot(new Vector3(27.5f, 1.7f, 0f), new Vector3(50f, 1.2f, 2f), "Backups/cavedark_0_flashlight.png", true);
                Snapshot(new Vector3(41f, 1.8f, -4f), new Vector3(41f, 3.5f, 17f), "Backups/cavedark_1_flashlight.png", true);
                log.AppendLine("DONE");
            }
            catch (Exception e)
            {
                log.AppendLine("FAILED: " + e);
            }
            File.WriteAllText(ReportPath, log.ToString());
        }

        static void Snapshot(Vector3 from, Vector3 at, string path, bool flashlight)
        {
            var go = new GameObject("Snap Cam");
            Camera cam = go.AddComponent<Camera>();
            cam.fieldOfView = 70f;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 200f;
            go.transform.SetPositionAndRotation(from, Quaternion.LookRotation(at - from, Vector3.up));
            if (flashlight)
            {
                var lg = new GameObject("Snap flashlight");
                lg.transform.SetParent(go.transform, false);
                lg.transform.localPosition = new Vector3(0.25f, -0.2f, 0f);
                lg.transform.localRotation = Quaternion.Euler(4f, -2f, 0f);
                Light l = lg.AddComponent<Light>();
                l.type = LightType.Spot;
                l.spotAngle = 55f;
                l.innerSpotAngle = 25f;
                l.range = 28f;
                l.intensity = 7f;
                l.color = new Color(1f, 0.93f, 0.8f);
            }
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
