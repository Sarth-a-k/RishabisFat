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
    static class ListenerFix
    {
        const string TriggerPath = "Assets/Interaction/Editor/.run_listener";
        static readonly string[] Scenes = { "Assets/GameTest.unity", "Assets/Scenes/FourfoldCitadel_WithOurStuff.unity" };

        static ListenerFix()
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

        [MenuItem("Tools/Interaction/Add Audio Listener To Player Camera")]
        static void Run()
        {
            var sb = new StringBuilder();
            try
            {
                EditorSceneManager.SaveOpenScenes();
                foreach (string path in Scenes)
                {
                    if (!File.Exists(path)) continue;
                    Scene s = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    int existing = 0, added = 0;
                    foreach (GameObject r in s.GetRootGameObjects()) existing += r.GetComponentsInChildren<AudioListener>(true).Length;
                    if (existing == 0)
                        foreach (GameObject r in s.GetRootGameObjects())
                        {
                            FPCharacterMover m = r.GetComponentInChildren<FPCharacterMover>(true);
                            if (m == null) continue;
                            Camera cam = m.GetComponentInChildren<Camera>(true);
                            if (cam != null && cam.GetComponent<AudioListener>() == null) { cam.gameObject.AddComponent<AudioListener>(); added++; }
                            break;
                        }
                    EditorSceneManager.MarkSceneDirty(s);
                    EditorSceneManager.SaveScene(s);
                    sb.AppendLine(Path.GetFileName(path) + ": existing " + existing + ", added " + added);
                }
                sb.AppendLine("DONE");
            }
            catch (Exception e) { sb.AppendLine("FAILED " + e); }
            File.WriteAllText("Backups/listener_report.txt", sb.ToString());
        }
    }
}
