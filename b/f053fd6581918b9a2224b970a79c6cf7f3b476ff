using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FPCharacter.EditorTools
{
    [InitializeOnLoad]
    static class FlashlightTuneSetup
    {
        const string TriggerPath = "Assets/Interaction/Editor/.run_flash";
        const float Intensity = 7f;
        const float Range = 28f;
        const float SpotAngle = 66f;
        const float InnerAngle = 38f;
        static readonly string[] Scenes = { "Assets/GameTest.unity", "Assets/Scenes/FourfoldCitadel_WithOurStuff.unity" };

        static FlashlightTuneSetup()
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

        [MenuItem("Tools/Interaction/Brighten Flashlight")]
        static void Run()
        {
            try
            {
                EditorSceneManager.SaveOpenScenes();
                string report = "";
                foreach (string path in Scenes)
                {
                    if (!File.Exists(path)) continue;
                    Scene s = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    int n = 0;
                    foreach (GameObject root in s.GetRootGameObjects())
                        foreach (Light l in root.GetComponentsInChildren<Light>(true))
                        {
                            if (l.name != "Flashlight_Spotlight") continue;
                            Undo.RecordObject(l, "Brighten Flashlight");
                            l.intensity = Intensity;
                            l.range = Range;
                            l.spotAngle = SpotAngle;
                            l.innerSpotAngle = InnerAngle;
                            PrefabUtility.RecordPrefabInstancePropertyModifications(l);
                            n++;
                        }
                    EditorSceneManager.MarkSceneDirty(s);
                    EditorSceneManager.SaveScene(s);
                    report += Path.GetFileNameWithoutExtension(path) + ": " + n + "  ";
                }
                Debug.Log("[Flashlight] DONE " + report);
            }
            catch (Exception e)
            {
                Debug.LogError("[Flashlight] FAILED: " + e);
            }
        }
    }
}
