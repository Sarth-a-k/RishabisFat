using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace FPCharacter.EditorTools
{
    [InitializeOnLoad]
    static class CitadelMergeSetup
    {
        const string TriggerPath = "Assets/Interaction/Editor/.run_map";
        const string ExportTrigger = "Assets/Interaction/Editor/.run_export";
        const string MapScene = "Assets/Scenes/FourfoldCitadel.unity";
        const string WorkScene = "Assets/Scenes/FourfoldCitadel_WithOurStuff.unity";
        static readonly string[] MaterialRoots = { "Assets/Art", "Assets/Citadel", "Assets/Imported" };

        static CitadelMergeSetup()
        {
            EditorApplication.delayCall += Check;
        }

        static void Check()
        {
            if (!File.Exists(TriggerPath) || File.ReadAllText(TriggerPath).Trim() != "pending") return;
            if (File.Exists(ExportTrigger) && File.ReadAllText(ExportTrigger).Trim() == "pending")
            {
                EditorApplication.delayCall += Check;
                return;
            }
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.delayCall += Check;
                return;
            }
            File.WriteAllText(TriggerPath, "done");
            Run();
        }

        [MenuItem("Tools/Interaction/Prepare Fourfold Citadel Map")]
        static void Run()
        {
            try
            {
                EditorSceneManager.SaveOpenScenes();
                Directory.CreateDirectory("Backups");
                if (File.Exists("Assets/GameTest.unity")) File.Copy("Assets/GameTest.unity", "Backups/GameTest_before_map.unity", true);

                int converted = 0, transparent = 0;
                string[] guids = AssetDatabase.FindAssets("t:Material", MaterialRoots.Where(AssetDatabase.IsValidFolder).ToArray());
                Shader lit = Shader.Find("Universal Render Pipeline/Lit");
                AssetDatabase.StartAssetEditing();
                try
                {
                    foreach (string g in guids)
                    {
                        Material m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(g));
                        if (m == null || m.shader == null) continue;
                        string sn = m.shader.name;
                        if (sn != "Standard" && sn != "Standard (Specular setup)" && !sn.StartsWith("Hidden/InternalErrorShader")) continue;
                        if (ConvertToLit(m, lit)) transparent++;
                        EditorUtility.SetDirty(m);
                        converted++;
                    }
                }
                finally
                {
                    AssetDatabase.StopAssetEditing();
                }
                AssetDatabase.SaveAssets();

                var scenes = EditorBuildSettings.scenes.ToList();
                foreach (string p in new[] { MapScene, "Assets/Scenes/SunkenPrism.unity" })
                    if (File.Exists(p) && !scenes.Any(s => s.path == p)) scenes.Add(new EditorBuildSettingsScene(p, true));
                EditorBuildSettings.scenes = scenes.ToArray();

                if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
                {
                    urp.maxAdditionalLightsCount = 8;
                    EditorUtility.SetDirty(urp);
                }
                foreach (string g in AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset"))
                {
                    var a = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(AssetDatabase.GUIDToAssetPath(g));
                    if (a != null) { a.maxAdditionalLightsCount = 8; EditorUtility.SetDirty(a); }
                }

                bool inputChanged = false;
                UnityEngine.Object ps = Unsupported.GetSerializedAssetInterfaceSingleton("PlayerSettings");
                if (ps != null)
                {
                    SerializedObject so = new SerializedObject(ps);
                    SerializedProperty ih = so.FindProperty("activeInputHandler");
                    if (ih != null && ih.intValue != 2)
                    {
                        ih.intValue = 2;
                        so.ApplyModifiedPropertiesWithoutUndo();
                        inputChanged = true;
                    }
                }
                AssetDatabase.SaveAssets();

                if (!File.Exists(WorkScene)) AssetDatabase.CopyAsset(MapScene, WorkScene);
                AssetDatabase.Refresh();
                EditorSceneManager.OpenScene(WorkScene, OpenSceneMode.Single);

                Debug.Log("[MapMerge] DONE. Materials converted to URP: " + converted + " (transparent " + transparent + "). Build scenes: " + EditorBuildSettings.scenes.Length + ". Input handling set to Both: " + inputChanged + ". Opened " + WorkScene);
                if (inputChanged)
                    EditorApplication.delayCall += () =>
                    {
                        if (EditorUtility.DisplayDialog("Restart Unity", "The map uses the old input keys, so Input Handling was set to 'Both'. Unity needs to restart once for this to work.", "Restart now", "Later"))
                            EditorApplication.OpenProject(Directory.GetCurrentDirectory());
                    };
            }
            catch (Exception e)
            {
                Debug.LogError("[MapMerge] FAILED: " + e);
            }
        }

        static bool ConvertToLit(Material m, Shader lit)
        {
            Color color = m.HasProperty("_Color") ? m.GetColor("_Color") : Color.white;
            Texture mainTex = m.HasProperty("_MainTex") ? m.GetTexture("_MainTex") : null;
            Vector2 scale = m.HasProperty("_MainTex") ? m.GetTextureScale("_MainTex") : Vector2.one;
            Vector2 offset = m.HasProperty("_MainTex") ? m.GetTextureOffset("_MainTex") : Vector2.zero;
            float gloss = m.HasProperty("_Glossiness") ? m.GetFloat("_Glossiness") : 0.3f;
            float metal = m.HasProperty("_Metallic") ? m.GetFloat("_Metallic") : 0f;
            Texture bump = m.HasProperty("_BumpMap") ? m.GetTexture("_BumpMap") : null;
            float bumpScale = m.HasProperty("_BumpScale") ? m.GetFloat("_BumpScale") : 1f;
            Texture occ = m.HasProperty("_OcclusionMap") ? m.GetTexture("_OcclusionMap") : null;
            Texture metalMap = m.HasProperty("_MetallicGlossMap") ? m.GetTexture("_MetallicGlossMap") : null;
            Texture emitMap = m.HasProperty("_EmissionMap") ? m.GetTexture("_EmissionMap") : null;
            Color emit = m.HasProperty("_EmissionColor") ? m.GetColor("_EmissionColor") : Color.black;
            bool emissive = m.IsKeywordEnabled("_EMISSION") && emit.maxColorComponent > 0.001f;
            float mode = m.HasProperty("_Mode") ? m.GetFloat("_Mode") : 0f;
            float cutoff = m.HasProperty("_Cutoff") ? m.GetFloat("_Cutoff") : 0.5f;

            m.shader = lit;
            m.shaderKeywords = new string[0];
            m.SetColor("_BaseColor", color);
            m.SetTexture("_BaseMap", mainTex);
            m.SetTextureScale("_BaseMap", scale);
            m.SetTextureOffset("_BaseMap", offset);
            m.SetFloat("_Smoothness", gloss);
            m.SetFloat("_Metallic", metal);
            if (metalMap != null) { m.SetTexture("_MetallicGlossMap", metalMap); m.EnableKeyword("_METALLICSPECGLOSSMAP"); }
            if (bump != null) { m.SetTexture("_BumpMap", bump); m.SetFloat("_BumpScale", bumpScale); m.EnableKeyword("_NORMALMAP"); }
            if (occ != null) { m.SetTexture("_OcclusionMap", occ); m.EnableKeyword("_OCCLUSIONMAP"); }
            if (emissive)
            {
                m.SetColor("_EmissionColor", emit);
                if (emitMap != null) m.SetTexture("_EmissionMap", emitMap);
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            else
            {
                m.SetColor("_EmissionColor", Color.black);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            }

            bool isTransparent = mode >= 2f;
            if (mode == 1f)
            {
                m.SetFloat("_AlphaClip", 1f);
                m.SetFloat("_Cutoff", cutoff);
                m.EnableKeyword("_ALPHATEST_ON");
                m.SetOverrideTag("RenderType", "TransparentCutout");
                m.renderQueue = (int)RenderQueue.AlphaTest;
            }
            if (isTransparent)
            {
                m.SetFloat("_Surface", 1f);
                m.SetFloat("_Blend", mode == 3f ? 1f : 0f);
                m.SetFloat("_SrcBlend", mode == 3f ? (float)BlendMode.One : (float)BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
                m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_ZWrite", 0f);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                if (mode == 3f) m.EnableKeyword("_ALPHAPREMULTIPLY_ON");
                m.SetOverrideTag("RenderType", "Transparent");
                m.renderQueue = (int)RenderQueue.Transparent;
                m.SetShaderPassEnabled("DepthOnly", false);
                m.SetShaderPassEnabled("ShadowCaster", false);
            }
            else
            {
                m.SetFloat("_Surface", 0f);
                m.SetFloat("_ZWrite", 1f);
                m.SetFloat("_SrcBlend", (float)BlendMode.One);
                m.SetFloat("_DstBlend", (float)BlendMode.Zero);
                if (mode != 1f) { m.SetOverrideTag("RenderType", "Opaque"); m.renderQueue = -1; }
            }
            return isTransparent;
        }
    }
}
