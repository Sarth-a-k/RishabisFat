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
    static class SolveGlowSetup
    {
        const string TriggerPath = "Assets/Interaction/Editor/.run_solveglow";
        const string ReportPath = "Backups/solveglow_report.txt";
        const string MapScene = "Assets/Scenes/FourfoldCitadel_WithOurStuff.unity";
        const string ShadowScene = "Assets/Scenes/Shadow2D.unity";

        static SolveGlowSetup()
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
            var log = new StringBuilder();
            try
            {
                Scene active = SceneManager.GetActiveScene();
                ZoneMusic zmOpen = UnityEngine.Object.FindAnyObjectByType<ZoneMusic>();
                if (!(active.path == MapScene && zmOpen != null && zmOpen.zones.Count == 0)) EditorSceneManager.SaveOpenScenes();

                Scene shadow = EditorSceneManager.OpenScene(ShadowScene, OpenSceneMode.Single);
                ShadowRun2D run = UnityEngine.Object.FindAnyObjectByType<ShadowRun2D>();
                if (run != null && run.GetComponent<Cursor2DFix>() == null) run.gameObject.AddComponent<Cursor2DFix>();
                EditorSceneManager.MarkSceneDirty(shadow);
                EditorSceneManager.SaveScene(shadow);
                log.AppendLine("Shadow2D cursor fix: " + (run != null));

                Scene map = EditorSceneManager.OpenScene(MapScene, OpenSceneMode.Single);
                PrismMoonController prism = UnityEngine.Object.FindAnyObjectByType<PrismMoonController>(FindObjectsInactive.Include);
                GameObject old = GameObject.Find("Room Solved Glow");
                if (old != null) UnityEngine.Object.DestroyImmediate(old);
                var go = new GameObject("Room Solved Glow");
                RoomSolvedGlow glow = go.AddComponent<RoomSolvedGlow>();
                glow.prism = prism;
                glow.areaCenter = SunkenPrism.CitadelLayout.Centers[1];
                glow.areaSize = SunkenPrism.CitadelLayout.Sizes[1];
                int flames = 0;
                foreach (SunkenPrism.TorchFlame f in UnityEngine.Object.FindObjectsByType<SunkenPrism.TorchFlame>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    Vector3 d = f.transform.position - glow.areaCenter;
                    if (Mathf.Abs(d.x) <= glow.areaSize.x * 0.5f && Mathf.Abs(d.z) <= glow.areaSize.y * 0.5f) flames++;
                }
                int beacons = 0;
                foreach (ShapeBeacon b in UnityEngine.Object.FindObjectsByType<ShapeBeacon>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    b.delaySeconds = 75f;
                    b.areaCenter = glow.areaCenter;
                    b.areaSize = glow.areaSize;
                    EditorUtility.SetDirty(b);
                    beacons++;
                }
                EditorSceneManager.MarkSceneDirty(map);
                EditorSceneManager.SaveScene(map);
                log.AppendLine("prism " + (prism != null) + ", torches in Prismatic Hollows " + flames + ", shape hints " + beacons);
                ZoneMusic zm = UnityEngine.Object.FindAnyObjectByType<ZoneMusic>();
                log.AppendLine("zone music areas: " + (zm != null ? zm.zones.Count.ToString() : "none"));
                log.AppendLine("DONE");
            }
            catch (Exception e)
            {
                log.AppendLine("FAILED: " + e);
            }
            File.WriteAllText(ReportPath, log.ToString());
        }
    }
}
