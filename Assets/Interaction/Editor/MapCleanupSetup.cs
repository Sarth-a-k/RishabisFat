using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FPCharacter.EditorTools
{
    [InitializeOnLoad]
    static class MapCleanupSetup
    {
        const string TriggerPath = "Assets/Interaction/Editor/.run_mapclean";
        const string WorkScene = "Assets/Scenes/FourfoldCitadel_WithOurStuff.unity";
        const string SourceScene = "Assets/GameTest.unity";
        static readonly Vector3 Spawn = new Vector3(0f, 0.12f, 0f);
        const float SpawnYaw = 90f;

        static readonly string[] RemoveByPrefix =
        {
            "Explorer",
            "LUNAR TRIAL",
            "LUNAR SEAL",
            "PuzzleSocket_",
            "Region socket inscription",
            "THE LUNAR CONCORDANCE",
            "KINDLE",
            "RECTANGLE   >",
            "Thermal survey"
        };

        static MapCleanupSetup()
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

        [MenuItem("Tools/Interaction/Clean Map And Add Our Player")]
        static void Run()
        {
            try
            {
                EditorSceneManager.SaveOpenScenes();
                Directory.CreateDirectory("Backups");
                File.Copy(WorkScene, "Backups/FourfoldCitadel_WithOurStuff_before_clean.unity", true);

                Scene map = EditorSceneManager.OpenScene(WorkScene, OpenSceneMode.Single);
                SceneManager.SetActiveScene(map);

                var doomed = new List<GameObject>();
                foreach (GameObject root in map.GetRootGameObjects())
                    foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                    {
                        string n = t.name;
                        foreach (string p in RemoveByPrefix)
                            if (n.StartsWith(p, StringComparison.Ordinal)) { doomed.Add(t.gameObject); break; }
                    }
                foreach (MonoBehaviour mb in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (mb == null || mb.gameObject.scene != map) continue;
                    string ns = mb.GetType().Namespace;
                    if (ns == "SunkenPrism" && !(mb is SunkenPrism.TorchFlame)) doomed.Add(mb.gameObject);
                }
                foreach (Camera c in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (c.gameObject.scene == map) doomed.Add(c.gameObject);

                int removed = 0;
                var names = new List<string>();
                foreach (GameObject g in doomed)
                {
                    if (g == null) continue;
                    names.Add(g.name);
                    UnityEngine.Object.DestroyImmediate(g);
                    removed++;
                }

                foreach (FPCharacterMover old in UnityEngine.Object.FindObjectsByType<FPCharacterMover>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (old.gameObject.scene == map) UnityEngine.Object.DestroyImmediate(old.transform.root.gameObject);

                Scene src = EditorSceneManager.OpenScene(SourceScene, OpenSceneMode.Additive);
                SceneManager.SetActiveScene(map);
                FPCharacterMover mover = null;
                foreach (GameObject root in src.GetRootGameObjects())
                {
                    mover = root.GetComponentInChildren<FPCharacterMover>(true);
                    if (mover != null) break;
                }
                if (mover == null) throw new Exception("No player with FPCharacterMover found in GameTest");
                Transform srcRoot = mover.transform.root;
                GameObject player = UnityEngine.Object.Instantiate(srcRoot.gameObject);
                player.name = "Player";
                if (player.scene != map) SceneManager.MoveGameObjectToScene(player, map);
                player.transform.SetPositionAndRotation(Spawn, Quaternion.Euler(0f, SpawnYaw, 0f));
                Camera cam = player.GetComponentInChildren<Camera>(true);
                if (cam != null)
                {
                    cam.gameObject.tag = "MainCamera";
                    cam.farClipPlane = Mathf.Max(cam.farClipPlane, 380f);
                }
                FPCharacterMover newMover = player.GetComponentInChildren<FPCharacterMover>(true);
                if (newMover.GetComponent<TorchLightBudget>() == null) newMover.gameObject.AddComponent<TorchLightBudget>();
                EditorSceneManager.CloseScene(src, true);

                EditorSceneManager.MarkSceneDirty(map);
                EditorSceneManager.SaveScene(map);

                var scenes = new List<EditorBuildSettingsScene>();
                scenes.Add(new EditorBuildSettingsScene(WorkScene, true));
                foreach (EditorBuildSettingsScene s in EditorBuildSettings.scenes)
                    if (s.path != WorkScene) scenes.Add(s);
                EditorBuildSettings.scenes = scenes.ToArray();

                Debug.Log("[MapClean] DONE. Removed " + removed + " objects: " + string.Join(", ", names) + ". Player copied from " + srcRoot.name + " with camera " + (cam != null ? cam.name : "none") + " at " + Spawn);
            }
            catch (Exception e)
            {
                Debug.LogError("[MapClean] FAILED: " + e);
            }
        }
    }
}
