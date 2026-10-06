using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FPCharacter.EditorTools
{
    [InitializeOnLoad]
    static class ParticlesSetup
    {
        const string TriggerPath = "Assets/Interaction/Editor/.run_particles";
        static readonly string[] Scenes = { "Assets/GameTest.unity", "Assets/Scenes/FourfoldCitadel_WithOurStuff.unity" };

        static ParticlesSetup()
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

        [MenuItem("Tools/Interaction/Add Ambient Particles To Player")]
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
                        foreach (FPCharacterMover m in root.GetComponentsInChildren<FPCharacterMover>(true))
                            if (m.GetComponent<AmbientParticles>() == null)
                            {
                                m.gameObject.AddComponent<AmbientParticles>();
                                n++;
                            }
                    EditorSceneManager.MarkSceneDirty(s);
                    EditorSceneManager.SaveScene(s);
                    report += Path.GetFileNameWithoutExtension(path) + ": " + n + "  ";
                }
                Debug.Log("[Particles] DONE " + report);
            }
            catch (Exception e)
            {
                Debug.LogError("[Particles] FAILED: " + e);
            }
        }
    }
}
