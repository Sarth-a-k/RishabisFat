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
    static class FinalLevelSetup
    {
        const string TriggerPath = "Assets/Interaction/Editor/.run_finallevel";
        const string ReportPath = "Backups/finallevel_report.txt";
        const string MapScene = "Assets/Scenes/FourfoldCitadel_WithOurStuff.unity";
        const string AreaName = "Back Again";
        static readonly Vector3 Center = new Vector3(237f, 0f, 0f);
        static readonly Vector2 Size = new Vector2(52f, 52f);

        static FinalLevelSetup()
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

        [MenuItem("Tools/Interaction/Final Level Title And Music")]
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
                File.Copy(MapScene, "Backups/FourfoldCitadel_WithOurStuff_before_finallevel.unity", true);
                Scene map = EditorSceneManager.OpenScene(MapScene, OpenSceneMode.Single);

                AudioClip bgm = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Music/FinalBGM.mp3");
                Texture2D title = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/AreaTitles/05_BackAgain.png");
                AudioClip sting = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/AreaTitles/01_CasaDelSilencio.wav");
                log.AppendLine("bgm " + (bgm != null) + ", title " + (title != null) + ", sting " + (sting != null));

                ZoneMusic zm = UnityEngine.Object.FindAnyObjectByType<ZoneMusic>(FindObjectsInactive.Include);
                if (zm != null)
                {
                    var z = zm.zones.Find(x => x != null && x.name == AreaName);
                    if (z == null) { z = new ZoneMusic.Zone(); zm.zones.Add(z); }
                    z.name = AreaName;
                    z.center = Center;
                    z.size = Size;
                    z.clip = bgm;
                    z.volume = 0.5f;
                    z.resumeWhereLeft = true;
                    z.startDelay = 3f;
                    z.startAt = 0f;
                    z.fadeInSeconds = 4f;
                    EditorUtility.SetDirty(zm);
                    log.AppendLine("music zone set, zones " + zm.zones.Count);
                }
                else log.AppendLine("no ZoneMusic in map");

                AreaTitleManager at = UnityEngine.Object.FindAnyObjectByType<AreaTitleManager>(FindObjectsInactive.Include);
                if (at != null)
                {
                    var a = at.areas.Find(x => x != null && x.name == AreaName);
                    if (a == null) { a = new AreaTitleManager.Area(); at.areas.Add(a); }
                    a.name = AreaName;
                    a.center = Center;
                    a.size = Size;
                    a.title = title;
                    a.sound = sting;
                    EditorUtility.SetDirty(at);
                    log.AppendLine("area title set, areas " + at.areas.Count);
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
