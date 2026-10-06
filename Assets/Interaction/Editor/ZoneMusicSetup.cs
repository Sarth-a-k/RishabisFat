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
    static class ZoneMusicSetup
    {
        const string TriggerPath = "Assets/Interaction/Editor/.run_zonemusic";
        const string ReportPath = "Backups/zonemusic_report.txt";
        const string MapScene = "Assets/Scenes/FourfoldCitadel_WithOurStuff.unity";
        const string Hollow = "Assets/Audio/Music/PrismaticHollowBGM.ogg";
        const string Passage = "Assets/Audio/Music/Passageway.ogg";

        static ZoneMusicSetup()
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

        [MenuItem("Tools/Interaction/Setup Zone Music")]
        static void Run()
        {
            var log = new StringBuilder();
            try
            {
                EditorSceneManager.SaveOpenScenes();
                AssetDatabase.Refresh();
                Configure(Hollow);
                Configure(Passage);
                AudioClip hollow = AssetDatabase.LoadAssetAtPath<AudioClip>(Hollow);
                AudioClip passage = AssetDatabase.LoadAssetAtPath<AudioClip>(Passage);
                log.AppendLine("clips: hollow " + (hollow != null) + ", passage " + (passage != null));

                Scene map = SceneManager.GetActiveScene();
                if (map.path != MapScene) map = EditorSceneManager.OpenScene(MapScene, OpenSceneMode.Single);

                GameObject old = GameObject.Find("Zone Music");
                if (old != null) UnityEngine.Object.DestroyImmediate(old);
                var go = new GameObject("Zone Music");
                ZoneMusic zm = go.AddComponent<ZoneMusic>();
                zm.masterVolume = 0.6f;
                zm.fadeSeconds = 1.5f;

                for (int i = 0; i < 4; i++)
                {
                    Vector3 a = SunkenPrism.CitadelLayout.PassageStart(i);
                    Vector3 b = SunkenPrism.CitadelLayout.PassageEnd(i);
                    var z = new ZoneMusic.Zone
                    {
                        name = "Passage " + (i + 1),
                        center = (a + b) * 0.5f,
                        size = new Vector2(Mathf.Abs(b.x - a.x), SunkenPrism.CitadelLayout.PassageWidth + 2f),
                        clip = passage,
                        volume = 1f,
                        resumeWhereLeft = false
                    };
                    zm.zones.Add(z);
                    log.AppendLine(z.name + ": x " + a.x.ToString("F1") + " to " + b.x.ToString("F1"));
                }
                zm.zones.Add(new ZoneMusic.Zone
                {
                    name = "Prismatic Hollows",
                    center = SunkenPrism.CitadelLayout.Centers[1],
                    size = SunkenPrism.CitadelLayout.Sizes[1],
                    clip = hollow,
                    volume = 1f,
                    resumeWhereLeft = true
                });

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

        static void Configure(string path)
        {
            var ai = AssetImporter.GetAtPath(path) as AudioImporter;
            if (ai == null) return;
            AudioImporterSampleSettings s = ai.defaultSampleSettings;
            s.loadType = AudioClipLoadType.CompressedInMemory;
            s.compressionFormat = AudioCompressionFormat.Vorbis;
            s.quality = 0.5f;
            ai.defaultSampleSettings = s;
            ai.SaveAndReimport();
        }
    }
}
