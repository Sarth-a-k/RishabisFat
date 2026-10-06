using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Video;

namespace FPCharacter.EditorTools
{
    [InitializeOnLoad]
    static class CutsceneNPCSetup
    {
        const string TriggerPath = "Assets/Interaction/Editor/.run_cutscene";
        const string WorkScene = "Assets/Scenes/FourfoldCitadel_WithOurStuff.unity";
        const string RemoveName = "NPC1_Cutscene";
        const string TargetName = "Ghost_NPC_Sitting";
        const string Video = "npc1cutscene.mp4";

        static CutsceneNPCSetup()
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

        [MenuItem("Tools/Interaction/Put Cutscene On Sitting NPC")]
        static void Run()
        {
            try
            {
                EditorSceneManager.SaveOpenScenes();
                Scene s = EditorSceneManager.OpenScene(WorkScene, OpenSceneMode.Single);
                int removed = 0;
                GameObject target = null;
                foreach (GameObject r in s.GetRootGameObjects())
                {
                    if (r.name == RemoveName) { UnityEngine.Object.DestroyImmediate(r); removed++; continue; }
                    if (r.name == TargetName && target == null) target = r;
                }
                if (target == null) throw new Exception(TargetName + " not found in the scene");
                VideoPlayer vp = target.GetComponent<VideoPlayer>();
                if (vp == null) vp = target.AddComponent<VideoPlayer>();
                vp.playOnAwake = false;
                NPCCutscene c = target.GetComponent<NPCCutscene>();
                if (c == null) c = target.AddComponent<NPCCutscene>();
                c.videoFileName = Video;
                c.talkRadius = 2.5f;
                EditorUtility.SetDirty(c);
                EditorSceneManager.MarkSceneDirty(s);
                EditorSceneManager.SaveScene(s);
                Debug.Log("[Cutscene] DONE removed " + removed + ", cutscene on " + target.name + " at " + target.transform.position);
            }
            catch (Exception e)
            {
                Debug.LogError("[Cutscene] FAILED: " + e);
            }
        }
    }
}
