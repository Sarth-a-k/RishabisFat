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
    static class Tweaks5Setup
    {
        const string TriggerPath = "Assets/Interaction/Editor/.run_tweaks5";
        const string ReportPath = "Backups/tweaks5_report.txt";
        const string MapScene = "Assets/Scenes/FourfoldCitadel_WithOurStuff.unity";
        const string MiniScene = "Assets/Scenes/WarmStatues2D.unity";
        const string FinalLine = "9 SILHOUETTES, ALL CARRYING THE SAME SHADOWS.";

        static Tweaks5Setup()
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

        [MenuItem("Tools/Interaction/Goggles Glow, Julian Fade, Final Line")]
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
                Scene active = SceneManager.GetActiveScene();
                ZoneMusic zmOpen = UnityEngine.Object.FindAnyObjectByType<ZoneMusic>();
                bool stale = active.path == MapScene && zmOpen != null && zmOpen.zones.Count == 0;
                if (!stale) EditorSceneManager.SaveOpenScenes();
                File.Copy(MapScene, "Backups/FourfoldCitadel_WithOurStuff_before_tweaks5.unity", true);
                Scene map = EditorSceneManager.OpenScene(MapScene, OpenSceneMode.Single);

                Material glow = AssetDatabase.LoadAssetAtPath<Material>("Assets/Interaction/Generated/Highlight/PickupGlow.mat");
                Material spark = AssetDatabase.LoadAssetAtPath<Material>("Assets/Interaction/Generated/Highlight/PickupSparkle.mat");
                int goggles = 0;
                foreach (InfraredGogglesPickup g in UnityEngine.Object.FindObjectsByType<InfraredGogglesPickup>(FindObjectsInactive.Include))
                {
                    PickupHighlight h = g.GetComponent<PickupHighlight>();
                    if (h == null) h = g.gameObject.AddComponent<PickupHighlight>();
                    h.glowMaterial = glow;
                    h.sparkleMaterial = spark;
                    h.color = new Color(1f, 0.45f, 0.2f, 1f);
                    h.intensity = 0.8f;
                    h.flashIntensity = 1.6f;
                    h.flashInterval = 3.5f;
                    h.sparklesPerSecond = 3f;
                    h.farStrength = 0.4f;
                    EditorUtility.SetDirty(h);
                    goggles++;
                    log.AppendLine("goggles highlight on " + g.name + " at " + g.transform.position);
                }
                if (goggles == 0) log.AppendLine("no goggles pickup found");

                int julians = 0;
                foreach (JulianTransition j in UnityEngine.Object.FindObjectsByType<JulianTransition>(FindObjectsInactive.Include))
                {
                    j.startDelay = 1.3f;
                    j.blackFade = 0.45f;
                    EditorUtility.SetDirty(j);
                    julians++;
                }
                log.AppendLine("julian transitions sped up: " + julians);

                EditorSceneManager.MarkSceneDirty(map);
                EditorSceneManager.SaveScene(map);
                ZoneMusic zm = UnityEngine.Object.FindAnyObjectByType<ZoneMusic>();
                log.AppendLine("zone music areas: " + (zm != null ? zm.zones.Count.ToString() : "none"));

                Scene mini = EditorSceneManager.OpenScene(MiniScene, OpenSceneMode.Single);
                int lines = 0;
                foreach (WarmStatues2D w in UnityEngine.Object.FindObjectsByType<WarmStatues2D>(FindObjectsInactive.Include))
                {
                    w.finalLine = FinalLine;
                    EditorUtility.SetDirty(w);
                    lines++;
                }
                EditorSceneManager.MarkSceneDirty(mini);
                EditorSceneManager.SaveScene(mini);
                log.AppendLine("minigame final line set on " + lines + " component(s)");
                EditorSceneManager.OpenScene(MapScene, OpenSceneMode.Single);
                log.AppendLine("DONE");
            }
            catch (Exception e)
            {
                log.AppendLine("FAILED: " + e);
            }
            File.WriteAllText(ReportPath, log.ToString());
        }
    }
}
