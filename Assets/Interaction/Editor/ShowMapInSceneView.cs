using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace FPCharacter.EditorTools
{
    [InitializeOnLoad]
    static class ShowMapInSceneView
    {
        const string TriggerPath = "Assets/Interaction/Editor/.run_showmap";
        const string ReportPath = "Backups/showmap_report.txt";

        static ShowMapInSceneView()
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

        [MenuItem("Tools/Interaction/Show Everything In Scene View")]
        static void Run()
        {
            var log = new StringBuilder();
            SceneVisibilityManager.instance.ShowAll();
            SceneVisibilityManager.instance.EnableAllPicking();
            log.AppendLine("visible layers before: " + Tools.visibleLayers);
            Tools.visibleLayers = ~0;
            SceneView sv = SceneView.lastActiveSceneView;
            if (sv == null && SceneView.sceneViews.Count > 0) sv = (SceneView)SceneView.sceneViews[0];
            if (sv != null)
            {
                log.AppendLine("pivot " + sv.pivot + " size " + sv.size + " rotation " + sv.rotation.eulerAngles + " ortho " + sv.orthographic);
                log.AppendLine("clip " + sv.cameraSettings.nearClip + " .. " + sv.cameraSettings.farClip + " dynamic " + sv.cameraSettings.dynamicClip + " fov " + sv.cameraSettings.fieldOfView);
                log.AppendLine("mode " + sv.cameraMode.drawMode + " grid " + sv.showGrid + " gizmos " + sv.drawGizmos + " 2D " + sv.in2DMode);
                var cs = sv.cameraSettings;
                cs.dynamicClip = true;
                cs.nearClip = 0.03f;
                cs.farClip = 10000f;
                cs.fieldOfView = 60f;
                cs.occlusionCulling = false;
                sv.cameraSettings = cs;
                sv.in2DMode = false;
                sv.orthographic = false;
                sv.cameraMode = SceneView.GetBuiltinCameraMode(DrawCameraMode.Textured);
                sv.showGrid = true;
                sv.drawGizmos = true;
                sv.sceneViewState.SetAllEnabled(true);
                var mover = Object.FindAnyObjectByType<FPCharacterMover>();
                Vector3 at = mover != null ? mover.transform.position + Vector3.up * 1.5f : Vector3.zero;
                sv.LookAt(at, Quaternion.Euler(25f, 90f, 0f), 12f, false, true);
                sv.Repaint();
                log.AppendLine("reset and framed on " + at);
            }
            else log.AppendLine("no scene view open");
            SceneView.RepaintAll();
            log.AppendLine("DONE");
            File.WriteAllText(ReportPath, log.ToString());
        }
    }
}
