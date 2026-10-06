using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace FPCharacter.EditorTools
{
    [InitializeOnLoad]
    public static class SittingGhostSetup
    {
        const string TriggerPath = "Assets/Interaction/Editor/.run_sitting";
        const string SitFbx = "Assets/Environment/Ghost/GhostExplorer_Sitting.fbx";
        const string ControllerPath = "Assets/Interaction/Animations/Ghost_Sit.controller";
        const string CopyName = "Ghost_NPC_Sitting";

        static SittingGhostSetup()
        {
            if (!File.Exists(TriggerPath) || File.ReadAllText(TriggerPath).Trim() != "pending") return;
            EditorApplication.update += WaitAndRun;
        }

        static void WaitAndRun()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            EditorApplication.update -= WaitAndRun;
            Run();
            File.WriteAllText(TriggerPath, "done");
        }

        [MenuItem("Tools/Interaction/Add Sitting Ghost Copy")]
        public static void Run()
        {
            try
            {
                Scene scene = SceneManager.GetActiveScene();
                if (scene.path != "Assets/GameTest.unity")
                {
                    if (scene.isDirty && !string.IsNullOrEmpty(scene.path)) EditorSceneManager.SaveScene(scene);
                    scene = EditorSceneManager.OpenScene("Assets/GameTest.unity", OpenSceneMode.Single);
                }
                else if (scene.isDirty) EditorSceneManager.SaveScene(scene);

                ModelImporter mi = AssetImporter.GetAtPath(SitFbx) as ModelImporter;
                if (mi == null) throw new Exception("Sitting model not imported: " + SitFbx);
                bool changed = false;
                if (mi.animationType != ModelImporterAnimationType.Generic)
                {
                    mi.animationType = ModelImporterAnimationType.Generic;
                    changed = true;
                }
                ModelImporterClipAnimation[] clips = mi.clipAnimations;
                if (clips == null || clips.Length == 0) clips = mi.defaultClipAnimations;
                if (clips != null && clips.Length > 0 && clips.Any(c => !c.loopTime || c.name != "Ghost_Sit"))
                {
                    foreach (ModelImporterClipAnimation c in clips)
                    {
                        c.loopTime = true;
                        c.name = "Ghost_Sit";
                    }
                    mi.clipAnimations = clips.Take(1).ToArray();
                    changed = true;
                }
                if (changed) mi.SaveAndReimport();

                AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(SitFbx).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));
                if (clip == null) throw new Exception("No animation clip in " + SitFbx);
                AnimatorController ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
                if (ctrl == null) ctrl = AnimatorController.CreateAnimatorControllerAtPathWithClip(ControllerPath, clip);
                else
                {
                    AnimatorState state = ctrl.layers[0].stateMachine.defaultState;
                    if (state != null) state.motion = clip;
                    EditorUtility.SetDirty(ctrl);
                }

                GameObject old = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).Where(t => t.name == CopyName).Select(t => t.gameObject).FirstOrDefault();
                if (old != null) Object.DestroyImmediate(old);

                GhostPresence original = Object.FindObjectsByType<GhostPresence>(FindObjectsInactive.Include).FirstOrDefault(g => g.name != CopyName);
                if (original == null) throw new Exception("Original ghost (with GhostPresence) not found in the scene");

                GameObject copy = Object.Instantiate(original.gameObject, original.transform.parent);
                copy.name = CopyName;
                copy.transform.SetPositionAndRotation(original.transform.position + original.transform.right * 1.6f, original.transform.rotation);
                Animator animator = copy.GetComponentInChildren<Animator>();
                if (animator == null) animator = copy.AddComponent<Animator>();
                animator.runtimeAnimatorController = ctrl;
                animator.applyRootMotion = false;
                foreach (GhostPresence gp in copy.GetComponentsInChildren<GhostPresence>(true)) Object.DestroyImmediate(gp);
                foreach (CapsuleCollider cc in copy.GetComponents<CapsuleCollider>())
                {
                    cc.height = 1.0f;
                    cc.radius = 0.45f;
                    cc.center = new Vector3(0f, 0.5f, -0.1f);
                }

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);

                GameObject prefabCopy = Object.Instantiate(copy);
                prefabCopy.name = CopyName;
                prefabCopy.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                PrefabUtility.SaveAsPrefabAsset(prefabCopy, "Assets/Interaction/Prefabs/" + CopyName + ".prefab");
                Object.DestroyImmediate(prefabCopy);
                AssetDatabase.SaveAssets();
                Debug.Log("[SitGhost] DONE. Clip " + clip.name + " " + clip.length.ToString("F1") + "s loop=" + clip.isLooping + ". Copy at " + copy.transform.position.ToString("F2") + " next to " + original.name + " at " + original.transform.position.ToString("F2"));
            }
            catch (Exception e)
            {
                Debug.LogError("[SitGhost] FAILED: " + e);
            }
        }
    }
}
