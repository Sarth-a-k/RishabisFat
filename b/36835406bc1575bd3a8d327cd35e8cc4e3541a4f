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
    static class JulianSetup
    {
        const string TriggerPath = "Assets/Interaction/Editor/.run_julian";
        const string ReportPath = "Backups/julian_report.txt";
        const string MapScene = "Assets/Scenes/FourfoldCitadel_WithOurStuff.unity";

        static JulianSetup()
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

        [MenuItem("Tools/Interaction/Setup Julian Transition")]
        static void Run()
        {
            var log = new StringBuilder();
            try
            {
                AssetDatabase.Refresh();
                int clips = 0;
                for (int i = 1; i <= 4; i++) if (AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Resources/WarmStatues/julian" + i + ".wav") != null) clips++;
                log.AppendLine("julian clips: " + clips);

                Scene active = SceneManager.GetActiveScene();
                ZoneMusic zmOpen = UnityEngine.Object.FindAnyObjectByType<ZoneMusic>();
                bool stale = active.path == MapScene && zmOpen != null && zmOpen.zones.Count == 0;
                if (!stale) EditorSceneManager.SaveOpenScenes();
                else log.AppendLine("open map looked stale, reloading from disk");
                Directory.CreateDirectory("Backups");
                File.Copy(MapScene, "Backups/FourfoldCitadel_WithOurStuff_before_julian.unity", true);
                Scene map = EditorSceneManager.OpenScene(MapScene, OpenSceneMode.Single);

                int linked = 0;
                foreach (PuzzleTransitionLink link in UnityEngine.Object.FindObjectsByType<PuzzleTransitionLink>(FindObjectsInactive.Include))
                {
                    JulianTransition j = link.GetComponent<JulianTransition>();
                    if (j == null) j = link.gameObject.AddComponent<JulianTransition>();
                    j.nextScene = "WarmStatues2D";
                    link.julian = j;
                    link.minigameGate = link.GetComponent<MinigameGate>();
                    EditorUtility.SetDirty(j);
                    EditorUtility.SetDirty(link);
                    linked++;
                    log.AppendLine("julian on " + link.name + ", minigame gate " + (link.minigameGate != null ? link.minigameGate.id : "none") + ", puzzle " + (link.puzzle != null));
                }
                if (linked == 0) log.AppendLine("no PuzzleTransitionLink found");

                RoundTablePuzzle rt = UnityEngine.Object.FindAnyObjectByType<RoundTablePuzzle>(FindObjectsInactive.Include);
                log.AppendLine("round table overload " + (rt != null && rt.overload != null));

                EditorSceneManager.MarkSceneDirty(map);
                EditorSceneManager.SaveScene(map);
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
