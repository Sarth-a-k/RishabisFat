using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Video;

namespace FPCharacter.EditorTools
{
    [InitializeOnLoad]
    static class Npc2Setup
    {
        const string TriggerPath = "Assets/Interaction/Editor/.run_npc2";
        const string ReportPath = "Backups/npc2_report.txt";
        const string MapScene = "Assets/Scenes/FourfoldCitadel_WithOurStuff.unity";
        const string NpcName = "Ghost_NPC";
        const string Video = "npc2cutscene.mp4";
        const string MarkerMat = "Assets/Interaction/Generated/Assist/QuestMarker.mat";
        const string ShadowMusic = "Assets/Resources/Shadow2D/ambience.mp3";

        static Npc2Setup()
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

        [MenuItem("Tools/Interaction/Setup NPC2 Cutscene And Music")]
        static void Run()
        {
            var log = new StringBuilder();
            try
            {
                Scene active = SceneManager.GetActiveScene();
                ZoneMusic zmOpen = UnityEngine.Object.FindAnyObjectByType<ZoneMusic>();
                bool stale = active.path == MapScene && zmOpen != null && zmOpen.zones.Count == 0;
                if (!stale) EditorSceneManager.SaveOpenScenes();
                else log.AppendLine("open map looked stale, reloading from disk");
                Directory.CreateDirectory("Backups");
                File.Copy(MapScene, "Backups/FourfoldCitadel_WithOurStuff_before_npc2.unity", true);

                Scene map = EditorSceneManager.OpenScene(MapScene, OpenSceneMode.Single);

                GameObject npc = null;
                foreach (GameObject r in map.GetRootGameObjects()) if (r.name == NpcName) { npc = r; break; }
                if (npc == null) throw new Exception(NpcName + " not found");

                VideoPlayer vp = npc.GetComponent<VideoPlayer>();
                if (vp == null) vp = npc.AddComponent<VideoPlayer>();
                vp.playOnAwake = false;
                NPCCutscene cs = npc.GetComponent<NPCCutscene>();
                if (cs == null) cs = npc.AddComponent<NPCCutscene>();
                cs.videoFileName = Video;
                cs.talkRadius = 2.5f;
                EditorUtility.SetDirty(cs);
                log.AppendLine("cutscene " + Video + " on " + npc.name + " at " + npc.transform.position + ", file exists " + File.Exists("Assets/StreamingAssets/" + Video));

                if (npc.GetComponent<GhostIdleLife>() == null) npc.AddComponent<GhostIdleLife>();
                log.AppendLine("idle life added, presence " + (npc.GetComponent<GhostPresence>() != null));

                QuestMarker qm = npc.GetComponent<QuestMarker>();
                if (qm == null) qm = npc.AddComponent<QuestMarker>();
                if (qm.marker != null) UnityEngine.Object.DestroyImmediate(qm.marker.gameObject);
                Bounds b = NpcBounds(npc);
                GameObject q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                q.name = "Quest marker";
                UnityEngine.Object.DestroyImmediate(q.GetComponent<Collider>());
                q.transform.SetParent(npc.transform, true);
                q.transform.position = new Vector3(b.center.x, b.max.y + 0.4f, b.center.z);
                q.transform.rotation = Quaternion.identity;
                Vector3 ls = npc.transform.lossyScale;
                float size = 0.42f;
                q.transform.localScale = new Vector3(size / Mathf.Max(0.001f, ls.x), size / Mathf.Max(0.001f, ls.y), 1f / Mathf.Max(0.001f, ls.z));
                var mr = q.GetComponent<MeshRenderer>();
                mr.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(MarkerMat);
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                qm.npc = cs;
                qm.marker = q.transform;
                EditorUtility.SetDirty(qm);
                log.AppendLine("quest marker at " + q.transform.position + ", material " + (mr.sharedMaterial != null));

                AudioClip music = AssetDatabase.LoadAssetAtPath<AudioClip>(ShadowMusic);
                int transitions = 0;
                foreach (ComicPanelTransition t in UnityEngine.Object.FindObjectsByType<ComicPanelTransition>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (t.nextScene != "Shadow2D") continue;
                    t.nextMusic = music;
                    t.nextMusicVolume = 0.9f;
                    t.musicFadeOut = 2.5f;
                    t.musicFadeIn = 3f;
                    EditorUtility.SetDirty(t);
                    transitions++;
                    log.AppendLine("shadow transition on " + t.name + " music " + (music != null));
                }
                if (transitions == 0) log.AppendLine("no Shadow2D transition found");

                RoomSolvedGlow glow = UnityEngine.Object.FindAnyObjectByType<RoomSolvedGlow>(FindObjectsInactive.Include);
                log.AppendLine("room solved glow " + (glow != null) + (glow != null ? " prism " + (glow.prism != null) : ""));

                EditorSceneManager.MarkSceneDirty(map);
                EditorSceneManager.SaveScene(map);
                ZoneMusic zm = UnityEngine.Object.FindAnyObjectByType<ZoneMusic>();
                log.AppendLine("zone music areas: " + (zm != null ? zm.zones.Count.ToString() : "none"));

                Vector3 eye = npc.transform.position + npc.transform.forward * 3f + Vector3.up * 1.6f;
                Snapshot(eye, b.center, "Backups/npc2_ghost.png");
                log.AppendLine("DONE");
            }
            catch (Exception e)
            {
                log.AppendLine("FAILED: " + e);
            }
            File.WriteAllText(ReportPath, log.ToString());
        }

        static Bounds NpcBounds(GameObject go)
        {
            bool any = false;
            Bounds b = new Bounds(go.transform.position + Vector3.up, Vector3.one);
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer || r is LineRenderer || r.name == "Quest marker") continue;
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            return b;
        }

        static void Snapshot(Vector3 from, Vector3 at, string path)
        {
            var go = new GameObject("Snap Cam");
            Camera cam = go.AddComponent<Camera>();
            cam.fieldOfView = 60f;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 200f;
            go.transform.SetPositionAndRotation(from, Quaternion.LookRotation(at - from, Vector3.up));
            var rt = new RenderTexture(960, 540, 24);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(960, 540, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 960, 540), 0, 0);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            RenderTexture.active = null;
            cam.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(tex);
            rt.Release();
            UnityEngine.Object.DestroyImmediate(rt);
            UnityEngine.Object.DestroyImmediate(go);
        }
    }
}
