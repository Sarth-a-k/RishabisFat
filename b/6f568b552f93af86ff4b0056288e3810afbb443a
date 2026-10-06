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
    static class VoiceSetup
    {
        const string TriggerPath = "Assets/Interaction/Editor/.run_voice";
        const string ReportPath = "Backups/voice_report.txt";
        const string MapScene = "Assets/Scenes/FourfoldCitadel_WithOurStuff.unity";
        const string MenuScene = "Assets/Scenes/MainMenu.unity";
        const string VoicePath = "Assets/Resources/Dialogue/VoiceLines.asset";

        static VoiceSetup()
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

        [MenuItem("Tools/Interaction/Voice Lines, Void Darken, Menu Card")]
        static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                File.WriteAllText(TriggerPath, "pending");
                EditorApplication.delayCall += Check;
                return;
            }
            var log = new StringBuilder();
            try
            {
                AssetDatabase.Refresh();
                if (AssetDatabase.LoadAssetAtPath<VoiceLineSet>(VoicePath) == null)
                {
                    Directory.CreateDirectory("Assets/Resources/Dialogue");
                    var v = ScriptableObject.CreateInstance<VoiceLineSet>();
                    Add(v, "Npc_Ignore_01", "npc1_ignore");
                    Add(v, "Npc_Ignore_02", "npc2_ignore");
                    Add(v, "Hint_Torch", "torchhint");
                    Add(v, "Hint_Mirrors", "3mirrorhint");
                    Add(v, "Hint_PrismRotate", "prismrotation");
                    Add(v, "Hint_RedMoon", "redmoonhint");
                    AssetDatabase.CreateAsset(v, VoicePath);
                    AssetDatabase.SaveAssets();
                    int clips = 0; foreach (var l in v.lines) if (l.clip != null) clips++;
                    log.AppendLine("voice lines file created, clips linked " + clips + " / " + v.lines.Count);
                }
                else log.AppendLine("voice lines file exists, left untouched");

                Scene active = SceneManager.GetActiveScene();
                ZoneMusic zmOpen = UnityEngine.Object.FindAnyObjectByType<ZoneMusic>();
                bool stale = active.path == MapScene && zmOpen != null && zmOpen.zones.Count == 0;
                if (!stale) EditorSceneManager.SaveOpenScenes();
                File.Copy(MapScene, "Backups/FourfoldCitadel_WithOurStuff_before_voice.unity", true);
                Scene map = EditorSceneManager.OpenScene(MapScene, OpenSceneMode.Single);

                GameObject uv = GameObject.Find("UV Sanctum Dressing");
                int dimmed = 0;
                if (uv != null)
                    foreach (Light l in uv.GetComponentsInChildren<Light>(true))
                    {
                        if (l.name == "UV light") { l.intensity *= 0.65f; dimmed++; }
                        else if (l.name == "UV wall wash") { l.intensity *= 0.65f; dimmed++; }
                    }
                log.AppendLine("phosphor void lights dimmed by 35%: " + dimmed);
                ZoneMusic zm = UnityEngine.Object.FindAnyObjectByType<ZoneMusic>(FindObjectsInactive.Include);
                if (zm != null)
                {
                    var z = zm.zones.Find(x => x != null && x.name == "Phosphor Void");
                    if (z != null) { z.volume = 0.35f; EditorUtility.SetDirty(zm); log.AppendLine("phosphor void music volume 0.35"); }
                }
                EditorSceneManager.MarkSceneDirty(map);
                EditorSceneManager.SaveScene(map);

                Scene menu = EditorSceneManager.OpenScene(MenuScene, OpenSceneMode.Single);
                foreach (MainMenuScene mm in UnityEngine.Object.FindObjectsByType<MainMenuScene>(FindObjectsInactive.Include))
                {
                    mm.againJiggle = 7f;
                    mm.againBop = 0.08f;
                    EditorUtility.SetDirty(mm);
                    log.AppendLine("menu card jiggle");
                }
                EditorSceneManager.MarkSceneDirty(menu);
                EditorSceneManager.SaveScene(menu);
                EditorSceneManager.OpenScene(MapScene, OpenSceneMode.Single);
                log.AppendLine("DONE");
            }
            catch (Exception e)
            {
                log.AppendLine("FAILED: " + e);
            }
            File.WriteAllText(ReportPath, log.ToString());
        }

        static void Add(VoiceLineSet v, string key, string file)
        {
            v.lines.Add(new VoiceLineSet.Line
            {
                key = key,
                clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Resources/Voice/" + file + ".wav"),
                speaker = "",
                subtitle = "[" + file + " - type the subtitle here]",
                volume = 1f
            });
        }
    }
}
