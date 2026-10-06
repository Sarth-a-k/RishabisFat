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
    static class GateStaticSetup
    {
        const string TriggerPath = "Assets/Interaction/Editor/.run_gatestatic";
        const string ReportPath = "Backups/gatestatic_report.txt";
        const string MapScene = "Assets/Scenes/FourfoldCitadel_WithOurStuff.unity";

        static GateStaticSetup()
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

        [MenuItem("Tools/Interaction/Make Gates Movable")]
        static void Run()
        {
            var log = new StringBuilder();
            try
            {
                Scene active = SceneManager.GetActiveScene();
                ZoneMusic zmOpen = UnityEngine.Object.FindAnyObjectByType<ZoneMusic>();
                bool stale = active.path == MapScene && zmOpen != null && zmOpen.zones.Count == 0;
                if (!stale) EditorSceneManager.SaveOpenScenes();
                else log.AppendLine("open map looked stale, reloading from disk");
                Directory.CreateDirectory("Backups");
                File.Copy(MapScene, "Backups/FourfoldCitadel_WithOurStuff_before_gatestatic.unity", true);
                Scene map = EditorSceneManager.OpenScene(MapScene, OpenSceneMode.Single);

                foreach (LockedGate g in UnityEngine.Object.FindObjectsByType<LockedGate>(FindObjectsInactive.Include))
                {
                    int changed = 0, total = 0;
                    foreach (Transform t in g.GetComponentsInChildren<Transform>(true))
                    {
                        total++;
                        if (GameObjectUtility.GetStaticEditorFlags(t.gameObject) == 0) continue;
                        log.AppendLine("  " + t.name + " was " + GameObjectUtility.GetStaticEditorFlags(t.gameObject));
                        GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);
                        changed++;
                    }
                    log.AppendLine("gate " + g.name + ": cleared static on " + changed + " of " + total + " objects");
                }

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
