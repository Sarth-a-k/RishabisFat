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
    static class SpookySetup
    {
        const string TriggerPath = "Assets/Interaction/Editor/.run_spooky";
        const string Dir = "Assets/Interaction/Generated/Spooky";
        static readonly string[] Scenes = { "Assets/GameTest.unity", "Assets/Scenes/FourfoldCitadel_WithOurStuff.unity" };

        static SpookySetup()
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

        static Texture2D Save(string name, Texture2D tex)
        {
            string path = Dir + "/" + name + ".png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            TextureImporter ti = (TextureImporter)AssetImporter.GetAtPath(path);
            ti.alphaIsTransparency = true;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.mipmapEnabled = true;
            ti.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static Texture2D EyesTexture()
        {
            const int W = 256, H = 128;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, true);
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float best = 0f;
                    Color col = Color.black;
                    foreach (float cx in new[] { 0.29f, 0.71f })
                    {
                        float side = cx < 0.5f ? -1f : 1f;
                        float u = (x / (float)W - cx) / 0.17f;
                        float v = (y / (float)H - 0.5f) / 0.34f;
                        float tilt = side * 0.35f * u;
                        float vv = v - tilt;
                        float half = Mathf.Max(0f, 1f - u * u);
                        float edge = Mathf.Abs(vv) - half * 0.55f;
                        float inside = Mathf.Clamp01(-edge * 14f);
                        float glow = Mathf.Clamp01(1f - Mathf.Sqrt(u * u * 0.5f + vv * vv * 1.2f) / 1.45f);
                        glow = glow * glow * 0.55f;
                        float pupil = Mathf.Clamp01(1f - (Mathf.Abs(u + side * 0.02f) - 0.07f * half) * 30f) * inside;
                        float core = Mathf.Clamp01(1f - Mathf.Sqrt(u * u + vv * vv * 3f) * 1.4f);
                        Color iris = Color.Lerp(new Color(0.9f, 0.05f, 0.03f), new Color(1f, 0.75f, 0.35f), core * 0.8f);
                        Color c = iris * inside * (1f - pupil * 0.95f) + new Color(0.8f, 0.05f, 0.03f) * glow * (1f - inside);
                        float a = Mathf.Max(inside * (1f - pupil * 0.85f), glow);
                        if (a > best) { best = a; col = c; }
                    }
                    col.a = best;
                    tex.SetPixel(x, y, col);
                }
            tex.Apply();
            return Save("SpookyEyes", tex);
        }

        static Texture2D HaloTexture()
        {
            const int W = 8, H = 64;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, true);
            for (int y = 0; y < H; y++)
            {
                float v = Mathf.Abs((y + 0.5f) / H * 2f - 1f);
                float a = Mathf.Pow(Mathf.Clamp01(1f - v), 2.2f);
                for (int x = 0; x < W; x++) tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
            tex.Apply();
            return Save("BeamHalo", tex);
        }

        static Material Additive(string name, Texture2D tex, Color c)
        {
            string path = Dir + "/" + name + ".mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")); AssetDatabase.CreateAsset(m, path); }
            m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 2f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = 3000;
            EditorUtility.SetDirty(m);
            return m;
        }

        [MenuItem("Tools/Interaction/Add Beam Glow And Spooky Eyes")]
        static void Run()
        {
            var sb = new StringBuilder();
            try
            {
                EditorSceneManager.SaveOpenScenes();
                Directory.CreateDirectory(Dir);
                Material eyesMat = Additive("SpookyEyes", EyesTexture(), new Color(1f, 0.15f, 0.08f, 1f));
                Material haloMat = Additive("BeamHalo", HaloTexture(), new Color(1f, 1f, 1f, 0.45f));
                AssetDatabase.SaveAssets();
                foreach (string path in Scenes)
                {
                    if (!File.Exists(path)) continue;
                    Scene s = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    int players = 0;
                    foreach (GameObject root in s.GetRootGameObjects())
                        foreach (FPCharacterMover m in root.GetComponentsInChildren<FPCharacterMover>(true))
                        {
                            BeamGlowManager g = m.GetComponent<BeamGlowManager>();
                            if (g == null) g = m.gameObject.AddComponent<BeamGlowManager>();
                            g.haloMaterial = haloMat;
                            EditorUtility.SetDirty(g);
                            players++;
                        }
                    if (path.Contains("FourfoldCitadel"))
                    {
                        foreach (GameObject root in s.GetRootGameObjects())
                            if (root.name == "Spooky eyes (Sunken Prism)") UnityEngine.Object.DestroyImmediate(root);
                        GameObject go = new GameObject("Spooky eyes (Sunken Prism)");
                        SceneManager.MoveGameObjectToScene(go, s);
                        go.transform.position = new Vector3(48.85f, 0f, 0f);
                        SpookyEyes eyes = go.AddComponent<SpookyEyes>();
                        eyes.eyesMaterial = eyesMat;
                        eyes.roomCenter = new Vector3(48.85f, 0f, 0f);
                        eyes.roomSize = new Vector2(48.1f, 36.4f);
                        EditorUtility.SetDirty(eyes);
                        sb.AppendLine("spooky eyes added");
                    }
                    EditorSceneManager.MarkSceneDirty(s);
                    EditorSceneManager.SaveScene(s);
                    sb.AppendLine(Path.GetFileName(path) + ": beam glow on " + players + " player(s)");
                }
                sb.AppendLine("DONE");
            }
            catch (Exception e) { sb.AppendLine("FAILED " + e); }
            File.WriteAllText("Backups/spooky_report.txt", sb.ToString());
        }
    }
}
