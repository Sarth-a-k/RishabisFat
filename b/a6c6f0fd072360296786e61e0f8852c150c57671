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
    static class AreaTitleSetup
    {
        const string TriggerPath = "Assets/Interaction/Editor/.run_areatitles";
        const string WorkScene = "Assets/Scenes/FourfoldCitadel_WithOurStuff.unity";
        const string Dir = "Assets/AreaTitles";

        static AreaTitleSetup()
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

        static Texture2D Tex(string name)
        {
            string path = Dir + "/" + name + ".png";
            TextureImporter ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null) return null;
            ti.textureType = TextureImporterType.Default;
            ti.alphaIsTransparency = true;
            ti.mipmapEnabled = false;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.maxTextureSize = 2048;
            ti.npotScale = TextureImporterNPOTScale.None;
            ti.textureCompression = TextureImporterCompression.CompressedHQ;
            ti.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static AudioClip Clip(string name)
        {
            string path = Dir + "/" + name + ".wav";
            AudioImporter ai = AssetImporter.GetAtPath(path) as AudioImporter;
            if (ai == null) return null;
            AudioImporterSampleSettings st = ai.defaultSampleSettings;
            st.loadType = AudioClipLoadType.DecompressOnLoad;
            st.compressionFormat = AudioCompressionFormat.Vorbis;
            st.quality = 0.7f;
            ai.defaultSampleSettings = st;
            ai.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }

        [MenuItem("Tools/Interaction/Set Up Area Titles")]
        static void Run()
        {
            var sb = new StringBuilder();
            try
            {
                EditorSceneManager.SaveOpenScenes();
                Scene s = EditorSceneManager.OpenScene(WorkScene, OpenSceneMode.Single);
                foreach (GameObject r in s.GetRootGameObjects())
                    if (r.name == "Area Titles") UnityEngine.Object.DestroyImmediate(r);
                GameObject go = new GameObject("Area Titles");
                SceneManager.MoveGameObjectToScene(go, s);
                AudioSource src = go.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.spatialBlend = 0f;
                AreaTitleManager m = go.AddComponent<AreaTitleManager>();
                m.areas.Add(new AreaTitleManager.Area { name = "Prismatic Hollows", center = new Vector3(48.85f, 0f, 0f), size = new Vector2(48.1f, 36.4f), title = Tex("02_PrismaticHollows"), sound = Clip("02_PrismaticHollows") });
                m.areas.Add(new AreaTitleManager.Area { name = "Ember Keep", center = new Vector3(112.15f, -2f, 0f), size = new Vector2(46.5f, 30f), title = Tex("03_EmberKeep"), sound = Clip("03_EmberKeep") });
                Tex("01_CasaDelSilencio"); Clip("01_CasaDelSilencio"); Tex("04_PhosphorVoid"); Clip("04_PhosphorVoid");
                EditorUtility.SetDirty(m);
                EditorSceneManager.MarkSceneDirty(s);
                EditorSceneManager.SaveScene(s);
                foreach (var a in m.areas) sb.AppendLine(a.name + ": title " + (a.title != null) + ", sound " + (a.sound != null));
                sb.AppendLine("DONE");
            }
            catch (Exception e) { sb.AppendLine("FAILED " + e); }
            File.WriteAllText("Backups/areatitles_report.txt", sb.ToString());
        }
    }
}
