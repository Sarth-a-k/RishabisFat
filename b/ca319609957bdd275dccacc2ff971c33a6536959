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
    static class AudioTweakSetup
    {
        const string TriggerPath = "Assets/Interaction/Editor/.run_audiotweak";
        const string ReportPath = "Backups/audiotweak_report.txt";
        const string MapScene = "Assets/Scenes/FourfoldCitadel_WithOurStuff.unity";

        static AudioTweakSetup()
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

        [MenuItem("Tools/Interaction/Audio Tweaks")]
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
                File.Copy(MapScene, "Backups/FourfoldCitadel_WithOurStuff_before_audiotweak.unity", true);
                Scene map = EditorSceneManager.OpenScene(MapScene, OpenSceneMode.Single);

                foreach (NPCCutscene c in UnityEngine.Object.FindObjectsByType<NPCCutscene>(FindObjectsInactive.Include))
                {
                    c.audioVolume = 1f;
                    EditorUtility.SetDirty(c);
                    log.AppendLine("cutscene " + c.name + " " + c.videoFileName + " volume " + c.audioVolume);
                }
                foreach (LockedGate g in UnityEngine.Object.FindObjectsByType<LockedGate>(FindObjectsInactive.Include))
                {
                    g.soundVolume = 0.55f;
                    log.AppendLine("gate " + g.name + " sound volume " + g.soundVolume);
                    EditorUtility.SetDirty(g);
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
