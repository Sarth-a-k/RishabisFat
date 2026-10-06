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
    static class MapRestoreSetup
    {
        const string TriggerPath = "Assets/Interaction/Editor/.run_restore";
        const string WorkScene = "Assets/Scenes/FourfoldCitadel_WithOurStuff.unity";
        const string OriginalScene = "Assets/Scenes/FourfoldCitadel.unity";
        const string GroupName = "PRISM AND LUNAR ALTAR";
        static readonly string[] TrialParts = { "White triangular prism", "Pearl prism glow", "THE CONCORDANCE PRISM", "Framed lunar altar", "Cube_" };
        static readonly string[] StandaloneParts = { "PuzzleSocket_", "Region socket inscription", "LUNAR SEAL" };

        static MapRestoreSetup()
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

        static Transform FindByName(Scene s, string name)
        {
            foreach (GameObject root in s.GetRootGameObjects())
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                    if (t.name == name) return t;
            return null;
        }

        static GameObject CopyInto(Transform original, Transform parent, Scene map)
        {
            GameObject c = parent != null ? UnityEngine.Object.Instantiate(original.gameObject, parent, true) : UnityEngine.Object.Instantiate(original.gameObject);
            if (c.scene != map) SceneManager.MoveGameObjectToScene(c, map);
            c.name = original.name;
            if (parent == null) c.transform.SetPositionAndRotation(original.position, original.rotation);
            c.transform.SetSiblingIndex(Mathf.Min(original.GetSiblingIndex(), c.transform.parent != null ? c.transform.parent.childCount - 1 : 0));
            foreach (MonoBehaviour mb in c.GetComponentsInChildren<MonoBehaviour>(true))
                if (mb != null && mb.GetType().Namespace == "SunkenPrism" && !(mb is SunkenPrism.TorchFlame)) UnityEngine.Object.DestroyImmediate(mb);
            return c;
        }

        static bool Starts(string n, string[] prefixes)
        {
            foreach (string p in prefixes) if (n.StartsWith(p, StringComparison.Ordinal)) return true;
            return false;
        }

        [MenuItem("Tools/Interaction/Restore Prism Altar Sigils And Gate")]
        static void Run()
        {
            try
            {
                EditorSceneManager.SaveOpenScenes();
                Directory.CreateDirectory("Backups");
                File.Copy(WorkScene, "Backups/FourfoldCitadel_WithOurStuff_before_restore.unity", true);
                Scene map = EditorSceneManager.OpenScene(WorkScene, OpenSceneMode.Single);
                Scene orig = EditorSceneManager.OpenScene(OriginalScene, OpenSceneMode.Additive);
                SceneManager.SetActiveScene(map);
                var made = new List<string>();

                Transform old = FindByName(map, GroupName);
                if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
                foreach (GameObject root in map.GetRootGameObjects())
                    foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                        if (t != null && Starts(t.name, StandaloneParts)) UnityEngine.Object.DestroyImmediate(t.gameObject);

                Transform trial = null;
                foreach (GameObject root in orig.GetRootGameObjects())
                    foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                        if (t.name.StartsWith("LUNAR TRIAL", StringComparison.Ordinal)) trial = t;
                if (trial != null)
                {
                    Transform mapParent = trial.parent != null ? FindByName(map, trial.parent.name) : null;
                    GameObject group = new GameObject(GroupName);
                    SceneManager.MoveGameObjectToScene(group, map);
                    if (mapParent != null) group.transform.SetParent(mapParent, false);
                    group.transform.SetPositionAndRotation(trial.position, trial.rotation);
                    group.transform.localScale = trial.localScale;
                    foreach (Transform child in trial)
                        if (Starts(child.name, TrialParts))
                        {
                            CopyInto(child, group.transform, map);
                            made.Add(child.name);
                        }
                }

                var standalone = new List<Transform>();
                foreach (GameObject root in orig.GetRootGameObjects())
                    foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                        if (Starts(t.name, StandaloneParts) && (t.parent == null || !Starts(t.parent.name, StandaloneParts))) standalone.Add(t);
                foreach (Transform t in standalone)
                {
                    Transform mapParent = t.parent != null ? FindByName(map, t.parent.name) : null;
                    GameObject c = CopyInto(t, mapParent, map);
                    made.Add(t.name);
                    if (t.name.StartsWith("LUNAR SEAL", StringComparison.Ordinal))
                        foreach (Transform g in c.transform)
                            if (g.name.StartsWith("Ember gate", StringComparison.Ordinal) && g.GetComponent<LockedGate>() == null)
                                g.gameObject.AddComponent<LockedGate>();
                }

                EditorSceneManager.CloseScene(orig, true);
                EditorSceneManager.MarkSceneDirty(map);
                EditorSceneManager.SaveScene(map);
                Debug.Log("[MapRestore] DONE restored: " + string.Join(", ", made));
            }
            catch (Exception e)
            {
                Debug.LogError("[MapRestore] FAILED: " + e);
            }
        }
    }
}
