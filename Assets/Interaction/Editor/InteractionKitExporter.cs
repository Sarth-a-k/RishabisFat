using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace FPCharacter.EditorTools
{
    [InitializeOnLoad]
    public static class InteractionKitExporter
    {
        const string TriggerPath = "Assets/Interaction/Editor/.run_export";
        const string PrefabDir = "Assets/Interaction/Prefabs";

        static InteractionKitExporter()
        {
            if (!File.Exists(TriggerPath) || File.ReadAllText(TriggerPath).Trim() != "pending") return;
            EditorApplication.update += WaitAndRun;
        }

        static void WaitAndRun()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            EditorApplication.update -= WaitAndRun;
            Export();
            File.WriteAllText(TriggerPath, "done");
        }

        [MenuItem("Tools/Interaction/Build Prefabs And Export Kit")]
        public static void Export()
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
                AssetDatabase.SaveAssets();

                BuildPlayerPrefab();
                BuildGhostPrefab();
                BuildAimedTorchPrefab();
                AssetDatabase.SaveAssets();

                string[] roots = new[]
                {
                    "Assets/FPCharacter",
                    "Assets/Interaction",
                    "Assets/Environment",
                    "Assets/GhostExplorer_LowPoly.fbx",
                    "Assets/groundcolor.mat",
                    "Assets/GameTest.unity"
                }.Where(p => AssetDatabase.IsValidFolder(p) || File.Exists(p)).ToArray();
                string outPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "GhostTorchMirror_Kit_" + DateTime.Now.ToString("yyyyMMdd_HHmm") + ".unitypackage"));
#pragma warning disable 618
                AssetDatabase.ExportPackage(roots, outPath, ExportPackageOptions.Recurse);
#pragma warning restore 618
                Debug.Log("[KitExport] DONE -> " + outPath);
            }
            catch (Exception e)
            {
                Debug.LogError("[KitExport] FAILED: " + e);
            }
        }

        static void Rewire(GameObject root, Transform cam)
        {
            foreach (FPCharacterMover m in root.GetComponentsInChildren<FPCharacterMover>(true)) m.viewCamera = cam;
            foreach (FirstPersonCharacterAnimator a in root.GetComponentsInChildren<FirstPersonCharacterAnimator>(true)) a.viewCamera = cam;
            foreach (PlayerInteraction p in root.GetComponentsInChildren<PlayerInteraction>(true))
            {
                p.viewCamera = cam;
                p.mover = root.GetComponentInChildren<FPCharacterMover>(true);
                p.character = root.GetComponentInChildren<FirstPersonCharacterAnimator>(true);
            }
            foreach (FPLocomotionAnimator l in root.GetComponentsInChildren<FPLocomotionAnimator>(true))
            {
                l.mover = root.GetComponentInChildren<FPCharacterMover>(true);
                l.character = root.GetComponentInChildren<FirstPersonCharacterAnimator>(true);
            }
            foreach (FPCharacterPreview pv in root.GetComponentsInChildren<FPCharacterPreview>(true))
            {
                pv.previewCamera = cam;
                pv.character = root.GetComponentInChildren<FirstPersonCharacterAnimator>(true);
                pv.enabled = false;
            }
            foreach (TorchAngleAdjust t in root.GetComponentsInChildren<TorchAngleAdjust>(true))
                t.character = root.GetComponentInChildren<FirstPersonCharacterAnimator>(true);
        }

        static void BuildPlayerPrefab()
        {
            FPCharacterMover mover = Object.FindAnyObjectByType<FPCharacterMover>(FindObjectsInactive.Include);
            if (mover == null)
            {
                Debug.LogError("[KitExport] No player with FP Character Mover in the scene");
                return;
            }
            Transform srcCam = mover.viewCamera;
            GameObject copy = Object.Instantiate(mover.gameObject);
            copy.name = "FP_Player";
            Transform cam = null;
            if (srcCam != null && srcCam.IsChildOf(mover.transform))
            {
                string path = AnimationUtility.CalculateTransformPath(srcCam, mover.transform);
                cam = copy.transform.Find(path);
            }
            else if (srcCam != null)
            {
                GameObject camCopy = Object.Instantiate(srcCam.gameObject, copy.transform);
                camCopy.name = srcCam.name;
                camCopy.transform.position = srcCam.position - mover.transform.position + copy.transform.position;
                cam = camCopy.transform;
            }
            copy.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            if (cam != null)
            {
                cam.gameObject.tag = "MainCamera";
                if (cam.GetComponent<AudioListener>() == null) cam.gameObject.AddComponent<AudioListener>();
            }
            Rewire(copy, cam);
            PrefabUtility.SaveAsPrefabAsset(copy, PrefabDir + "/FP_Player.prefab");
            Object.DestroyImmediate(copy);
            Debug.Log("[KitExport] Saved " + PrefabDir + "/FP_Player.prefab (camera: " + (cam != null ? cam.name : "none") + ")");
        }

        static void BuildGhostPrefab()
        {
            GhostPresence ghost = Object.FindAnyObjectByType<GhostPresence>(FindObjectsInactive.Include);
            if (ghost == null)
            {
                Debug.LogWarning("[KitExport] No ghost in scene");
                return;
            }
            GameObject copy = Object.Instantiate(ghost.gameObject);
            copy.name = "Ghost_NPC";
            copy.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            PrefabUtility.SaveAsPrefabAsset(copy, PrefabDir + "/Ghost_NPC.prefab");
            Object.DestroyImmediate(copy);
            Debug.Log("[KitExport] Saved " + PrefabDir + "/Ghost_NPC.prefab");
        }

        static void BuildAimedTorchPrefab()
        {
            PuzzleTorch torch = Object.FindAnyObjectByType<PuzzleTorch>(FindObjectsInactive.Include);
            if (torch == null)
            {
                Debug.LogWarning("[KitExport] No torch in scene");
                return;
            }
            Vector3 offset = torch.target != null ? torch.target.position - torch.transform.position : new Vector3(-4f, 0f, -4f);
            GameObject copy = Object.Instantiate(torch.gameObject);
            copy.name = "PuzzleTorch_Aimed";
            copy.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            GameObject target = new GameObject("BeamTarget");
            target.transform.SetParent(copy.transform, false);
            target.transform.localPosition = offset;
            PuzzleTorch pt = copy.GetComponent<PuzzleTorch>();
            pt.beamAim = PuzzleTorch.BeamAim.TowardTarget;
            pt.target = target.transform;
            pt.keepBeamLevel = false;
            pt.readInteractKey = false;
            PrefabUtility.SaveAsPrefabAsset(copy, PrefabDir + "/PuzzleTorch_Aimed.prefab");
            Object.DestroyImmediate(copy);
            Debug.Log("[KitExport] Saved " + PrefabDir + "/PuzzleTorch_Aimed.prefab (beam target offset " + offset.ToString("F1") + ")");
        }
    }
}
