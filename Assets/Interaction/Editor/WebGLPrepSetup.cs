using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace FPCharacter.EditorTools
{
    [InitializeOnLoad]
    static class WebGLPrepSetup
    {
        const string TriggerPath = "Assets/Interaction/Editor/.run_webglprep";
        const string ReportPath = "Backups/webglprep_report.txt";
        const string KeepDir = "Assets/Resources/ShaderKeep";

        static WebGLPrepSetup()
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

        [MenuItem("Tools/Interaction/Prepare WebGL Build")]
        static void Run()
        {
            var log = new StringBuilder();
            try
            {
                Directory.CreateDirectory(KeepDir);
                Keep("Lit_Plain", "Universal Render Pipeline/Lit", log, m => { });
                Keep("Lit_Emissive", "Universal Render Pipeline/Lit", log, m => { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", Color.white); });
                Keep("Lit_NormalEmissive", "Universal Render Pipeline/Lit", log, m => { m.EnableKeyword("_NORMALMAP"); m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", Color.white); });
                Keep("Lit_Transparent", "Universal Render Pipeline/Lit", log, m => Transparent(m, false));
                Keep("SimpleLit_Cutout", "Universal Render Pipeline/Simple Lit", log, m => { m.SetFloat("_AlphaClip", 1f); m.EnableKeyword("_ALPHATEST_ON"); m.EnableKeyword("_EMISSION"); m.SetFloat("_Cull", 0f); });
                Keep("Unlit_Plain", "Universal Render Pipeline/Unlit", log, m => { });
                Keep("Unlit_Transparent", "Universal Render Pipeline/Unlit", log, m => Transparent(m, false));
                Keep("Particles_Alpha", "Universal Render Pipeline/Particles/Unlit", log, m => Transparent(m, false));
                Keep("Particles_Additive", "Universal Render Pipeline/Particles/Unlit", log, m => Transparent(m, true));
                Keep("Sprites_Default", "Sprites/Default", log, m => { });
                Keep("Unlit_Color", "Unlit/Color", log, m => { });
                foreach (string s in new[] { "CasaFX/UVGlow", "CasaFX/SoftWhite", "CasaFX/PickupGlow", "CasaFX/BlackFlame", "CasaFX/BlackAura", "SunkenPrism/TorchFlame", "SunkenPrism/WorldText", "SunkenPrism/LunarHalo", "SunkenPrism/LightShaft", "CasaMenu/VHSOverlay", "CasaMenu/AnimatedLayer" })
                    Keep(s.Replace("/", "_"), s, log, m => { });
                AssetDatabase.SaveAssets();

                int rp = 0;
                foreach (string guid in AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset"))
                {
                    var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(AssetDatabase.GUIDToAssetPath(guid));
                    if (asset == null) continue;
                    var so = new SerializedObject(asset);
                    var p = so.FindProperty("m_GPUResidentDrawerMode");
                    if (p != null && p.intValue != 0) { p.intValue = 0; so.ApplyModifiedPropertiesWithoutUndo(); rp++; }
                }
                log.AppendLine("GPU Resident Drawer turned off on " + rp + " render pipeline asset(s)");

                PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
                PlayerSettings.WebGL.decompressionFallback = true;
                PlayerSettings.WebGL.dataCaching = true;
                PlayerSettings.runInBackground = true;
                log.AppendLine("WebGL: Gzip + decompression fallback, data caching on");
                AssetDatabase.SaveAssets();
                log.AppendLine("DONE");
            }
            catch (Exception e)
            {
                log.AppendLine("FAILED: " + e);
            }
            File.WriteAllText(ReportPath, log.ToString());
        }

        static void Transparent(Material m, bool additive)
        {
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", additive ? 2f : 0f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)(additive ? UnityEngine.Rendering.BlendMode.One : UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha));
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            if (additive) m.EnableKeyword("_BLENDMODE_ADD");
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = 3000;
        }

        static void Keep(string name, string shader, StringBuilder log, Action<Material> setup)
        {
            Shader sh = Shader.Find(shader);
            if (sh == null) { log.AppendLine("missing shader: " + shader); return; }
            string path = KeepDir + "/" + name + ".mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, path); }
            else m.shader = sh;
            setup(m);
            EditorUtility.SetDirty(m);
            log.AppendLine("kept " + shader + " (" + name + ")");
        }
    }
}
