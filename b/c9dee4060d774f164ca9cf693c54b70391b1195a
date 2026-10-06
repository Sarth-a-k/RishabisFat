using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FPCharacter.EditorTools
{
    [InitializeOnLoad]
    static class VoidFinaleSetup
    {
        const string TriggerPath = "Assets/Interaction/Editor/.run_voidfinale";
        const string ReportPath = "Backups/voidfinale_report.txt";
        const string MapScene = "Assets/Scenes/FourfoldCitadel_WithOurStuff.unity";
        const string MenuScene = "Assets/Scenes/MainMenu.unity";
        const string BladeScene = "Assets/Scenes/LightBlade2D.unity";
        static readonly Vector3 VoidCenter = new Vector3(173.9f, 0f, 0f);

        static VoidFinaleSetup()
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

        [MenuItem("Tools/Interaction/Main Menu, Light Blade, Phosphor Void")]
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
                AssetDatabase.Refresh();
                Scene active = SceneManager.GetActiveScene();
                ZoneMusic zmOpen = UnityEngine.Object.FindAnyObjectByType<ZoneMusic>();
                bool stale = active.path == MapScene && zmOpen != null && zmOpen.zones.Count == 0;
                if (!stale) EditorSceneManager.SaveOpenScenes();
                Directory.CreateDirectory("Backups");
                File.Copy(MapScene, "Backups/FourfoldCitadel_WithOurStuff_before_voidfinale.unity", true);
                if (File.Exists(MenuScene)) File.Copy(MenuScene, "Backups/MainMenu_before_backagain.unity", true);

                int px = 0;
                foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Resources/LightBlade" }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    string n = Path.GetFileNameWithoutExtension(path);
                    var ti = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (ti == null) continue;
                    ti.alphaIsTransparency = true;
                    ti.npotScale = TextureImporterNPOTScale.None;
                    ti.maxTextureSize = 4096;
                    bool pixel = n.StartsWith("hall_") || n.StartsWith("crystal_") || n.StartsWith("stand_") || n.StartsWith("pedestal_") || n.StartsWith("px_");
                    if (pixel) { ti.filterMode = FilterMode.Point; ti.textureCompression = TextureImporterCompression.Uncompressed; ti.mipmapEnabled = false; px++; }
                    ti.SaveAndReimport();
                }
                foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Resources/MainMenu" }))
                {
                    var ti = AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid)) as TextureImporter;
                    if (ti == null) continue;
                    ti.alphaIsTransparency = true; ti.npotScale = TextureImporterNPOTScale.None; ti.mipmapEnabled = false; ti.maxTextureSize = 4096;
                    ti.textureCompression = TextureImporterCompression.Uncompressed;
                    ti.SaveAndReimport();
                }
                log.AppendLine("light blade pixel textures " + px + ", menu textures configured");

                Scene menu = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var camGo = new GameObject("Main Camera");
                camGo.tag = "MainCamera";
                Camera cam = camGo.AddComponent<Camera>();
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Color.black;
                cam.cullingMask = 0;
                camGo.AddComponent<AudioListener>();
                var mm = new GameObject("Main Menu").AddComponent<MainMenuScene>();
                mm.gameSceneName = Path.GetFileNameWithoutExtension(MapScene);
                mm.music = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/MainMenu/Audio/MainMenuMusic.ogg");
                mm.gameObject.AddComponent<Cursor2DFix>();
                EditorSceneManager.SaveScene(menu, MenuScene);
                log.AppendLine("main menu rebuilt, music " + (mm.music != null) + ", loads " + mm.gameSceneName);

                Scene blade = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var lb = new GameObject("LightBlade").AddComponent<LightBladeSequence>();
                lb.nextSceneName = Path.GetFileNameWithoutExtension(MapScene);
                lb.playOnStart = true;
                lb.gameObject.AddComponent<Cursor2DFix>();
                EditorSceneManager.SaveScene(blade, BladeScene);
                log.AppendLine("light blade scene saved, returns to " + lb.nextSceneName);

                var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
                list.RemoveAll(s => s.path == MenuScene);
                list.Insert(0, new EditorBuildSettingsScene(MenuScene, true));
                if (!list.Exists(s => s.path == BladeScene)) list.Add(new EditorBuildSettingsScene(BladeScene, true));
                EditorBuildSettings.scenes = list.ToArray();
                log.AppendLine("build: " + string.Join(" | ", Array.ConvertAll(EditorBuildSettings.scenes, s => Path.GetFileNameWithoutExtension(s.path) + (s.enabled ? "" : "(off)"))));

                Scene map = EditorSceneManager.OpenScene(MapScene, OpenSceneMode.Single);
                Physics.SyncTransforms();

                GameObject oldExit = GameObject.Find("Phosphor Void Exit");
                if (oldExit != null) UnityEngine.Object.DestroyImmediate(oldExit);
                var exit = new GameObject("Phosphor Void Exit");
                exit.transform.position = new Vector3(185.6f, 0f, 11.8f);
                var bt = exit.AddComponent<BlackoutTransition>();
                bt.nextScene = Path.GetFileNameWithoutExtension(BladeScene);
                FPCharacterMover mover = UnityEngine.Object.FindAnyObjectByType<FPCharacterMover>(FindObjectsInactive.Include);
                bt.playerMovement = mover;
                var link = exit.AddComponent<VoidButtonExit>();
                link.blackout = bt;
                var ret = new GameObject("Return Point");
                ret.transform.SetParent(exit.transform, false);
                Vector3 rp = new Vector3(215f, 0f, 0f);
                if (Physics.Raycast(new Vector3(rp.x, 4f, rp.z), Vector3.down, out RaycastHit hit, 12f, ~0, QueryTriggerInteraction.Ignore)) rp.y = hit.point.y;
                ret.transform.SetPositionAndRotation(rp + Vector3.up * 0.12f, Quaternion.Euler(0f, 90f, 0f));
                var mg = exit.AddComponent<MinigameGate>();
                mg.id = "lightblade";
                mg.returnPoint = ret.transform;
                mg.gate = null;
                mg.openDelay = 0f;
                log.AppendLine("red button -> blackout -> LightBlade2D; return point " + ret.transform.position);

                ZoneMusic zm = UnityEngine.Object.FindAnyObjectByType<ZoneMusic>(FindObjectsInactive.Include);
                if (zm != null)
                {
                    ZoneMusic.Zone z = zm.zones.Find(x => x != null && x.name == "Phosphor Void");
                    if (z == null) { z = new ZoneMusic.Zone(); zm.zones.Add(z); }
                    z.name = "Phosphor Void";
                    z.center = VoidCenter;
                    z.size = new Vector2(44f, 44f);
                    z.clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Music/PhosphorusVoidBGM.mp3");
                    z.volume = 0.5f;
                    z.resumeWhereLeft = true;
                    z.startDelay = 4f;
                    z.startAt = 0f;
                    z.fadeInSeconds = 4f;
                    EditorUtility.SetDirty(zm);
                    log.AppendLine("phosphor void music zone, clip " + (z.clip != null) + ", zones " + zm.zones.Count);
                }

                AreaTitleManager at = UnityEngine.Object.FindAnyObjectByType<AreaTitleManager>(FindObjectsInactive.Include);
                if (at != null)
                {
                    var a = at.areas.Find(x => x != null && x.name == "Phosphor Void");
                    if (a == null) { a = new AreaTitleManager.Area(); at.areas.Add(a); }
                    a.name = "Phosphor Void";
                    a.center = VoidCenter;
                    a.size = new Vector2(40f, 40f);
                    a.title = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/AreaTitles/04_PhosphorVoid.png");
                    a.sound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/AreaTitles/04_PhosphorVoid.wav");
                    EditorUtility.SetDirty(at);
                    log.AppendLine("area title added, art " + (a.title != null) + ", sound " + (a.sound != null) + ", areas " + at.areas.Count);
                }
                else log.AppendLine("no AreaTitleManager in map");

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
    }
}
