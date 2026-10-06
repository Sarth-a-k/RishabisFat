using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace FPCharacter.EditorTools
{
    [InitializeOnLoad]
    static class DialogueSetup
    {
        const string TriggerPath = "Assets/Interaction/Editor/.run_dialogue";
        const string ReportPath = "Backups/dialogue_report.txt";
        const string AssetPath = "Assets/Resources/Dialogue/Shadow2D_Dialogue.asset";

        static DialogueSetup()
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

        [MenuItem("Tools/Interaction/Create Shadow2D Dialogue File")]
        static void Run()
        {
            try
            {
                if (AssetDatabase.LoadAssetAtPath<DialogueSet>(AssetPath) != null)
                {
                    File.WriteAllText(ReportPath, "dialogue file already exists, left untouched: " + AssetPath + "\nDONE");
                    Selection.activeObject = AssetDatabase.LoadAssetAtPath<DialogueSet>(AssetPath);
                    return;
                }
                Directory.CreateDirectory("Assets/Resources/Dialogue");
                var set = ScriptableObject.CreateInstance<DialogueSet>();
                set.defaultHoldSeconds = 3.2f;
                set.speakers.Add(new DialogueSet.Speaker { id = "You", displayName = "YOU", nameColor = new Color(0.95f, 0.78f, 0.42f) });
                set.speakers.Add(new DialogueSet.Speaker { id = "Ghost", displayName = "???", nameColor = new Color(0.55f, 0.75f, 1f) });
                Add(set, "Intro_01", "You", "Where... where am I? I was at the door. I can't feel my legs.", 3.6f);
                Add(set, "Intro_02", "Ghost", "You are still at the door, traveller. Your body is lying where it fell. This is the room behind your eyes.", 5f);
                Add(set, "Intro_03", "Ghost", "Everything that walks in here, you brought in with you. Run, if it makes you feel better. Running has never helped.", 5f);
                Add(set, "Bark_FirstShadow", "You", "Something is behind me. Please, no. Don't look back.", 3.2f);
                Add(set, "Bark_SecondShadow", "Ghost", "They are wearing your face, under the dark.", 3.2f);
                Add(set, "Bark_TorchFlicker", "You", "No, no, no. Not the torch. Stay lit. Please stay lit.", 3.2f);
                Add(set, "Bark_Halfway", "Ghost", "A mind cannot outrun itself.", 3.2f);
                Add(set, "Bark_TorchDies", "You", "I can't see. I can't see! Somebody, wake me up!", 3.2f);
                Add(set, "Bark_Smothered", "Ghost", "Sleep, then. We will begin again.", 2.8f);
                AssetDatabase.CreateAsset(set, AssetPath);
                AssetDatabase.SaveAssets();
                Selection.activeObject = set;
                File.WriteAllText(ReportPath, "created " + AssetPath + " with " + set.lines.Count + " lines and " + set.speakers.Count + " speakers\nDONE");
            }
            catch (Exception e)
            {
                File.WriteAllText(ReportPath, "FAILED: " + e);
            }
        }

        static void Add(DialogueSet set, string key, string speaker, string text, float hold)
        {
            set.lines.Add(new DialogueSet.Line { key = key, speakerId = speaker, text = text, holdSeconds = hold });
        }
    }
}
