using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FPCharacter.EditorTools
{
    [InitializeOnLoad]
    static class MainMenuSetup
    {
        const string TriggerPath = "Assets/Interaction/Editor/.run_mainmenu";
        const string ReportPath = "Backups/mainmenu_report.txt";
        const string Root = "Assets/MainMenu";
        const string Art = Root + "/Art";
        const string MenuScene = "Assets/Scenes/MainMenu.unity";
        const string GameScene = "Assets/Scenes/FourfoldCitadel_WithOurStuff.unity";

        static MainMenuSetup()
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

        [MenuItem("Tools/Interaction/Build Main Menu")]
        static void Run()
        {
            var log = new StringBuilder();
            try
            {
                EditorSceneManager.SaveOpenScenes();
                Directory.CreateDirectory("Backups");
                if (File.Exists(MenuScene)) File.Copy(MenuScene, "Backups/MainMenu_before.unity", true);
                AssetDatabase.Refresh();

                string[] layers = { "01_Sky", "02_Figures", "03_Eyes_Glow", "04_Island_Sea", "05_LeftShade", "06_Title_Logo", "07_PressAnyKey" };
                foreach (string l in layers) ConfigureTexture(Art + "/" + l + ".png", false, 2048);
                ConfigureTexture(Art + "/MountainEyes_Closed.png", true, 512);
                ConfigureIds(Art + "/Menu_BlinkIds.png");
                ConfigureAudio(Root + "/Audio/MainMenuMusic.ogg");

                Shader layerShader = Shader.Find("CasaMenu/AnimatedLayer");
                Shader vhsShader = Shader.Find("CasaMenu/VHSOverlay");
                if (layerShader == null || vhsShader == null) throw new Exception("Menu shaders not found");

                Texture2D ids = AssetDatabase.LoadAssetAtPath<Texture2D>(Art + "/Menu_BlinkIds.png");
                Material mFigures = MakeMaterial("M_Menu_Figures", layerShader, m =>
                {
                    m.SetFloat("_SwayAmp", 0.0028f);
                });
                Material mEyes = MakeMaterial("M_Menu_Eyes", layerShader, m =>
                {
                    m.SetFloat("_SwayAmp", 0.0028f);
                    m.SetTexture("_BlinkTex", ids);
                    m.SetFloat("_BlinkAmount", 1f);
                    m.SetFloat("_BlinkRate", 0.12f);
                });
                Material mIsland = MakeMaterial("M_Menu_IslandSea", layerShader, m =>
                {
                    m.SetFloat("_WaterLine", 0.255f);
                    m.SetFloat("_WaterAmp", 0.0016f);
                });
                Material mVhs = MakeMaterial("M_Menu_VHS", vhsShader, m =>
                {
                    m.SetFloat("_Scanlines", 0.08f);
                    m.SetFloat("_Grain", 0.06f);
                    m.SetFloat("_Vignette", 0.45f);
                    m.SetFloat("_Band", 0.06f);
                });

                Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

                var camGo = new GameObject("Menu Camera");
                Camera cam = camGo.AddComponent<Camera>();
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Color.black;
                cam.cullingMask = 0;
                cam.tag = "MainCamera";
                camGo.AddComponent<AudioListener>();

                var canvasGo = new GameObject("Main Menu Canvas", typeof(RectTransform));
                Canvas canvas = canvasGo.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(3840, 2160);
                scaler.matchWidthOrHeight = 0.5f;

                RectTransform frame = Rect("Frame", canvasGo.transform);
                Stretch(frame);
                var fitter = frame.gameObject.AddComponent<AspectRatioFitter>();
                fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fitter.aspectRatio = 16f / 9f;

                RawImage sky = Layer("Sky", frame, "01_Sky", null);
                RawImage figures = Layer("Shadow Figures", frame, "02_Figures", mFigures);
                RawImage eyes = Layer("Figure Eyes", figures.rectTransform, "03_Eyes_Glow", mEyes);
                RawImage island = Layer("Island And Sea", frame, "04_Island_Sea", mIsland);

                var lidGo = new GameObject("Mountain Eyelids", typeof(RectTransform));
                lidGo.transform.SetParent(island.transform, false);
                Image lid = lidGo.AddComponent<Image>();
                lid.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Art + "/MountainEyes_Closed.png");
                lid.type = Image.Type.Filled;
                lid.fillMethod = Image.FillMethod.Vertical;
                lid.fillOrigin = (int)Image.OriginVertical.Top;
                lid.fillAmount = 0f;
                lid.raycastTarget = false;
                RectTransform lr = lid.rectTransform;
                lr.anchorMin = new Vector2(2395f / 3840f, 1f - 1315f / 2160f);
                lr.anchorMax = new Vector2(2665f / 3840f, 1f - 1205f / 2160f);
                lr.offsetMin = Vector2.zero;
                lr.offsetMax = Vector2.zero;

                RawImage shade = Layer("Left Shade", frame, "05_LeftShade", null);

                RawImage title = Layer("Title", frame, "06_Title_Logo", null);
                Box(title.rectTransform, 206f, 272f, 1264f, 1078f);
                RawImage prompt = Layer("Press Any Key", frame, "07_PressAnyKey", null);
                Box(prompt.rectTransform, 128f, 1270f, 1252f, 1566f);

                var vhsGo = new GameObject("VHS Overlay", typeof(RectTransform));
                vhsGo.transform.SetParent(canvasGo.transform, false);
                RawImage vhs = vhsGo.AddComponent<RawImage>();
                vhs.material = mVhs;
                vhs.raycastTarget = false;
                Stretch(vhs.rectTransform);

                var faderGo = new GameObject("Fader", typeof(RectTransform));
                faderGo.transform.SetParent(canvasGo.transform, false);
                Image black = faderGo.AddComponent<Image>();
                black.color = Color.black;
                black.raycastTarget = false;
                Stretch(black.rectTransform);
                CanvasGroup fg = faderGo.AddComponent<CanvasGroup>();
                fg.alpha = 0f;
                fg.blocksRaycasts = false;
                fg.interactable = false;

                var menuGo = new GameObject("Main Menu");
                AudioSource src = menuGo.AddComponent<AudioSource>();
                src.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(Root + "/Audio/MainMenuMusic.ogg");
                src.loop = true;
                src.playOnAwake = false;
                src.spatialBlend = 0f;
                CasaMainMenu menu = menuGo.AddComponent<CasaMainMenu>();
                menu.gameSceneName = Path.GetFileNameWithoutExtension(GameScene);
                menu.sky = sky.rectTransform;
                menu.figures = figures.rectTransform;
                menu.figureEyes = eyes;
                menu.leftShade = shade;
                menu.title = title;
                menu.pressAnyKey = prompt;
                menu.mountainEyelids = lid;
                menu.fader = fg;
                menu.music = src;

                EditorSceneManager.SaveScene(scene, MenuScene);

                var list = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(MenuScene, true), new EditorBuildSettingsScene(GameScene, true) };
                foreach (EditorBuildSettingsScene s in EditorBuildSettings.scenes)
                    if (s.path != MenuScene && s.path != GameScene) list.Add(new EditorBuildSettingsScene(s.path, false));
                EditorBuildSettings.scenes = list.ToArray();

                log.AppendLine("music " + (src.clip != null) + ", eyelid sprite " + (lid.sprite != null) + ", ids " + (ids != null));
                log.AppendLine("build scenes: " + string.Join(" | ", Array.ConvertAll(EditorBuildSettings.scenes, s => s.path + (s.enabled ? "" : " (off)"))));

                Snapshot(canvas, cam, fitter, lid, "Backups/mainmenu_open.png", 0f);
                Snapshot(canvas, cam, fitter, lid, "Backups/mainmenu_blink.png", 1f);

                EditorSceneManager.SaveScene(scene, MenuScene);
                log.AppendLine("DONE");
            }
            catch (Exception e)
            {
                log.AppendLine("FAILED: " + e);
            }
            File.WriteAllText(ReportPath, log.ToString());
        }

        static void Snapshot(Canvas canvas, Camera cam, AspectRatioFitter fitter, Image lid, string path, float fill)
        {
            var rt = new RenderTexture(1920, 1080, 24);
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 1f;
            cam.cullingMask = ~0;
            cam.targetTexture = rt;
            lid.fillAmount = fill;
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)canvas.transform);
            fitter.enabled = false;
            fitter.enabled = true;
            Canvas.ForceUpdateCanvases();
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            RenderTexture.active = null;
            cam.targetTexture = null;
            cam.cullingMask = 0;
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.worldCamera = null;
            lid.fillAmount = 0f;
            UnityEngine.Object.DestroyImmediate(tex);
            rt.Release();
            UnityEngine.Object.DestroyImmediate(rt);
        }

        static Material MakeMaterial(string name, Shader shader, Action<Material> setup)
        {
            string path = Root + "/Materials/" + name + ".mat";
            Directory.CreateDirectory(Root + "/Materials");
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = shader;
            setup(m);
            EditorUtility.SetDirty(m);
            AssetDatabase.SaveAssets();
            return m;
        }

        static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static void Stretch(RectTransform r)
        {
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = Vector2.zero;
            r.offsetMax = Vector2.zero;
        }

        static void Box(RectTransform r, float left, float top, float right, float bottom)
        {
            r.anchorMin = new Vector2(left / 3840f, 1f - bottom / 2160f);
            r.anchorMax = new Vector2(right / 3840f, 1f - top / 2160f);
            r.offsetMin = Vector2.zero;
            r.offsetMax = Vector2.zero;
        }

        static RawImage Layer(string name, RectTransform parent, string tex, Material mat)
        {
            RectTransform r = Rect(name, parent);
            Stretch(r);
            RawImage img = r.gameObject.AddComponent<RawImage>();
            img.texture = AssetDatabase.LoadAssetAtPath<Texture2D>(Art + "/" + tex + ".png");
            if (img.texture == null) throw new Exception("Missing texture " + tex);
            if (mat != null) img.material = mat;
            img.raycastTarget = false;
            return img;
        }

        static void ConfigureTexture(string path, bool sprite, int maxSize)
        {
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null) throw new Exception("Missing texture " + path);
            ti.textureType = sprite ? TextureImporterType.Sprite : TextureImporterType.Default;
            if (sprite) ti.spriteImportMode = SpriteImportMode.Single;
            ti.mipmapEnabled = false;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.alphaIsTransparency = true;
            ti.alphaSource = TextureImporterAlphaSource.FromInput;
            ti.npotScale = TextureImporterNPOTScale.None;
            ti.maxTextureSize = maxSize;
            ti.textureCompression = TextureImporterCompression.CompressedHQ;
            ti.SaveAndReimport();
        }

        static void ConfigureIds(string path)
        {
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null) throw new Exception("Missing texture " + path);
            ti.textureType = TextureImporterType.Default;
            ti.sRGBTexture = false;
            ti.mipmapEnabled = false;
            ti.filterMode = FilterMode.Point;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.npotScale = TextureImporterNPOTScale.None;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.SaveAndReimport();
        }

        static void ConfigureAudio(string path)
        {
            var ai = AssetImporter.GetAtPath(path) as AudioImporter;
            if (ai == null) throw new Exception("Missing audio " + path);
            AudioImporterSampleSettings s = ai.defaultSampleSettings;
            s.loadType = AudioClipLoadType.CompressedInMemory;
            s.compressionFormat = AudioCompressionFormat.Vorbis;
            s.quality = 0.6f;
            ai.defaultSampleSettings = s;
            ai.SaveAndReimport();
        }
    }
}
