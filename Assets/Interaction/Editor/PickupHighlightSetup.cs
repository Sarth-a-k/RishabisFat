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
    static class PickupHighlightSetup
    {
        const string TriggerPath = "Assets/Interaction/Editor/.run_highlight";
        const string ReportPath = "Backups/highlight_report.txt";
        const string MapScene = "Assets/Scenes/FourfoldCitadel_WithOurStuff.unity";
        const string Gen = "Assets/Interaction/Generated/Highlight";

        static PickupHighlightSetup()
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

        [MenuItem("Tools/Interaction/Add Pickup Highlights")]
        static void Run()
        {
            var log = new StringBuilder();
            try
            {
                Scene active = SceneManager.GetActiveScene();
                ZoneMusic zmOpen = UnityEngine.Object.FindAnyObjectByType<ZoneMusic>();
                bool staleMusic = active.path == MapScene && zmOpen != null && zmOpen.zones.Count == 0;
                if (staleMusic)
                {
                    log.AppendLine("open map had an empty Zone Music list, reloading the saved map instead");
                    EditorSceneManager.OpenScene(MapScene, OpenSceneMode.Single);
                }
                else EditorSceneManager.SaveOpenScenes();
                AssetDatabase.Refresh();
                Directory.CreateDirectory(Gen);

                Shader glowShader = Shader.Find("CasaFX/PickupGlow");
                if (glowShader == null) throw new Exception("PickupGlow shader missing");
                log.AppendLine("shader errors: " + ShaderUtil.ShaderHasError(glowShader));
                Material glow = AssetDatabase.LoadAssetAtPath<Material>(Gen + "/PickupGlow.mat");
                if (glow == null)
                {
                    glow = new Material(glowShader);
                    AssetDatabase.CreateAsset(glow, Gen + "/PickupGlow.mat");
                }
                glow.shader = glowShader;
                EditorUtility.SetDirty(glow);

                Shader ps = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                Material spark = AssetDatabase.LoadAssetAtPath<Material>(Gen + "/PickupSparkle.mat");
                if (spark == null)
                {
                    spark = new Material(ps);
                    AssetDatabase.CreateAsset(spark, Gen + "/PickupSparkle.mat");
                }
                spark.shader = ps;
                spark.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Interaction/Generated/EmberGate/Gate_EmberDot.png"));
                spark.SetColor("_BaseColor", Color.white);
                spark.SetFloat("_Surface", 1f);
                spark.SetFloat("_Blend", 2f);
                spark.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                spark.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
                spark.SetFloat("_ZWrite", 0f);
                spark.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                spark.SetOverrideTag("RenderType", "Transparent");
                spark.renderQueue = 3000;
                EditorUtility.SetDirty(spark);
                AssetDatabase.SaveAssets();

                Scene map = SceneManager.GetActiveScene();
                if (map.path != MapScene) map = EditorSceneManager.OpenScene(MapScene, OpenSceneMode.Single);

                int mirrors = 0, torches = 0, sockets = 0;
                foreach (MirrorPickup m in UnityEngine.Object.FindObjectsByType<MirrorPickup>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    PickupHighlight h = Ensure(m.gameObject, glow, spark);
                    h.color = new Color(1f, 0.8f, 0.45f, 1f);
                    h.intensity = 0.6f;
                    h.sparklesPerSecond = 2f;
                    mirrors++;
                    log.AppendLine("mirror: " + m.name);
                }
                foreach (PuzzleTorch t in UnityEngine.Object.FindObjectsByType<PuzzleTorch>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    PickupHighlight h = Ensure(t.gameObject, glow, spark);
                    h.color = new Color(1f, 0.62f, 0.3f, 1f);
                    h.intensity = 0.65f;
                    h.sparklesPerSecond = 2f;
                    torches++;
                    log.AppendLine("torch: " + t.name);
                }
                foreach (MirrorSocket s in UnityEngine.Object.FindObjectsByType<MirrorSocket>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    PickupHighlight h = Ensure(s.gameObject, glow, spark);
                    h.color = new Color(0.55f, 0.85f, 1f, 1f);
                    h.intensity = 0.4f;
                    h.flashIntensity = 1.0f;
                    h.sparklesPerSecond = 1f;
                    sockets++;
                    log.AppendLine("platform: " + s.name + " (" + s.shapeName + ")");
                }
                ZoneMusic zm = UnityEngine.Object.FindAnyObjectByType<ZoneMusic>();
                log.AppendLine("zone music areas: " + (zm != null ? zm.zones.Count.ToString() : "none"));
                log.AppendLine("mirrors " + mirrors + ", torches " + torches + ", platforms " + sockets);

                EditorSceneManager.MarkSceneDirty(map);
                EditorSceneManager.SaveScene(map);
                log.AppendLine("DONE");
            }
            catch (Exception e)
            {
                log.AppendLine("FAILED: " + e);
            }
            File.WriteAllText(ReportPath, log.ToString());
        }

        static PickupHighlight Ensure(GameObject go, Material glow, Material spark)
        {
            PickupHighlight h = go.GetComponent<PickupHighlight>();
            if (h == null) h = go.AddComponent<PickupHighlight>();
            h.glowMaterial = glow;
            h.sparkleMaterial = spark;
            EditorUtility.SetDirty(h);
            return h;
        }
    }
}
