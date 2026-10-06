using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FPCharacter.EditorTools
{
    [InitializeOnLoad]
    static class LightingTuneSetup
    {
        const string TriggerPath = "Assets/Interaction/Editor/.run_lighting";
        const float TorchBrightness = 2.4f;
        const float TorchRange = 14f;
        static readonly string[] Scenes = { "Assets/GameTest.unity", "Assets/Scenes/FourfoldCitadel_WithOurStuff.unity" };

        static LightingTuneSetup()
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

        [MenuItem("Tools/Interaction/Add Hand Glow And Brighter Torches")]
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
                    int glows = 0, budgets = 0;
                    foreach (GameObject root in s.GetRootGameObjects())
                        foreach (FPCharacterMover m in root.GetComponentsInChildren<FPCharacterMover>(true))
                        {
                            if (m.GetComponent<HandGlow>() == null) { m.gameObject.AddComponent<HandGlow>(); glows++; }
                            TorchLightBudget b = m.GetComponent<TorchLightBudget>();
                            if (b != null)
                            {
                                b.torchBrightness = TorchBrightness;
                                b.torchRange = TorchRange;
                                b.maxDistance = 40f;
                                EditorUtility.SetDirty(b);
                                budgets++;
                            }
                        }
                    EditorSceneManager.MarkSceneDirty(s);
                    EditorSceneManager.SaveScene(s);
                    report += Path.GetFileNameWithoutExtension(path) + ": glow " + glows + ", torches " + budgets + "  ";
                }
                Debug.Log("[Lighting] DONE " + report);
            }
            catch (Exception e)
            {
                Debug.LogError("[Lighting] FAILED: " + e);
            }
        }
    }
}
