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
    static class Shadow7Setup
    {
        const string TriggerPath = "Assets/Interaction/Editor/.run_shadow7";
        const string ReportPath = "Backups/shadow7_report.txt";
        const string MapScene = "Assets/Scenes/FourfoldCitadel_WithOurStuff.unity";
        const string Res = "Assets/Resources/Shadow2D";
        const string EmberMusic = "Assets/Audio/Music/EmberCryptBGM.mp3";

        static Shadow7Setup()
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

        [MenuItem("Tools/Interaction/Setup Shadow2D v7 And Ember Music")]
        static void Run()
        {
            var log = new StringBuilder();
            try
            {
                AssetDatabase.Refresh();
                int textures = 0;
                foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Res }))
                {
                    var ti = AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid)) as TextureImporter;
                    if (ti == null) continue;
                    ti.textureType = TextureImporterType.Default;
                    ti.alphaIsTransparency = true;
                    ti.mipmapEnabled = false;
                    ti.filterMode = FilterMode.Point;
                    ti.wrapMode = TextureWrapMode.Clamp;
                    ti.npotScale = TextureImporterNPOTScale.None;
                    ti.textureCompression = TextureImporterCompression.Uncompressed;
                    ti.maxTextureSize = 2048;
                    ti.SaveAndReimport();
                    textures++;
                }
                log.AppendLine("shadow2d textures configured: " + textures + ", dialogue lines " + AssetDatabase.FindAssets("dlg_", new[] { Res }).Length);

                Scene active = SceneManager.GetActiveScene();
                ZoneMusic zmOpen = UnityEngine.Object.FindAnyObjectByType<ZoneMusic>();
                bool stale = active.path == MapScene && zmOpen != null && zmOpen.zones.Count == 0;
                if (!stale) EditorSceneManager.SaveOpenScenes();
                else log.AppendLine("open map looked stale, reloading from disk");
                Directory.CreateDirectory("Backups");
                File.Copy(MapScene, "Backups/FourfoldCitadel_WithOurStuff_before_shadow7.unity", true);
                Scene map = EditorSceneManager.OpenScene(MapScene, OpenSceneMode.Single);
                FPCharacterMover mover = UnityEngine.Object.FindAnyObjectByType<FPCharacterMover>(FindObjectsInactive.Include);

                int linked = 0;
                foreach (ComicPanelTransition t in UnityEngine.Object.FindObjectsByType<ComicPanelTransition>(FindObjectsInactive.Include))
                {
                    if (t.nextScene != "Shadow2D") continue;
                    BlackoutTransition b = t.GetComponent<BlackoutTransition>();
                    if (b == null) b = t.gameObject.AddComponent<BlackoutTransition>();
                    b.nextScene = "Shadow2D";
                    b.puzzleSolved = false;
                    b.playerMovement = mover;
                    t.blackout = b;
                    t.nextMusic = null;
                    EditorUtility.SetDirty(b);
                    EditorUtility.SetDirty(t);
                    linked++;
                    log.AppendLine("blackout transition on " + t.name);
                }
                if (linked == 0) log.AppendLine("no Shadow2D trigger found");

                ZoneMusic zm = UnityEngine.Object.FindAnyObjectByType<ZoneMusic>(FindObjectsInactive.Include);
                AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(EmberMusic);
                if (zm != null)
                {
                    ZoneMusic.Zone z = zm.zones.Find(x => x != null && x.name == "Ember Keep");
                    if (z == null) { z = new ZoneMusic.Zone(); zm.zones.Add(z); }
                    z.name = "Ember Keep";
                    z.center = new Vector3(112.15f, -2f, 0f);
                    z.size = new Vector2(46.5f, 30f);
                    z.clip = clip;
                    z.volume = 1f;
                    z.resumeWhereLeft = true;
                    z.startDelay = 5f;
                    z.startAt = 0f;
                    z.fadeInSeconds = 4f;
                    EditorUtility.SetDirty(zm);
                    log.AppendLine("ember keep music zone, clip " + (clip != null) + ", zones now " + zm.zones.Count);
                }
                else log.AppendLine("no ZoneMusic in map");

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
