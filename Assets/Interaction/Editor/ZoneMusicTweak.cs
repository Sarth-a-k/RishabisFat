using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FPCharacter.EditorTools
{
    [InitializeOnLoad]
    static class ZoneMusicTweak
    {
        const string TriggerPath = "Assets/Interaction/Editor/.run_zonetweak";
        const string MapScene = "Assets/Scenes/FourfoldCitadel_WithOurStuff.unity";

        static ZoneMusicTweak()
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
            string report;
            try
            {
                EditorSceneManager.SaveOpenScenes();
                Scene map = SceneManager.GetActiveScene();
                if (map.path != MapScene) map = EditorSceneManager.OpenScene(MapScene, OpenSceneMode.Single);
                ZoneMusic zm = UnityEngine.Object.FindAnyObjectByType<ZoneMusic>();
                if (zm == null) throw new Exception("Zone Music not found");
                report = "";
                foreach (ZoneMusic.Zone z in zm.zones)
                {
                    if (z.name == "Prismatic Hollows")
                    {
                        z.startDelay = 3f;
                        z.fadeInSeconds = 5f;
                        z.startAt = 0f;
                    }
                    report += z.name + ": delay " + z.startDelay + ", startAt " + z.startAt + ", fadeIn " + z.fadeInSeconds + "\n";
                }
                EditorUtility.SetDirty(zm);
                EditorSceneManager.MarkSceneDirty(map);
                EditorSceneManager.SaveScene(map);
                report += "DONE";
            }
            catch (Exception e)
            {
                report = "FAILED: " + e;
            }
            File.WriteAllText("Backups/zonetweak_report.txt", report);
        }
    }
}
