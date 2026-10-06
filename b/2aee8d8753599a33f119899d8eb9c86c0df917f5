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
    static class LevelTwoSetup
    {
        const string TriggerPath = "Assets/Interaction/Editor/.run_level2";
        const string ReportPath = "Backups/level2_report.txt";
        const string MapScene = "Assets/Scenes/FourfoldCitadel_WithOurStuff.unity";
        const string MiniScene = "Assets/Scenes/WarmStatues2D.unity";
        const string Res = "Assets/Resources/WarmStatues";
        static readonly Vector3 EmberCenter = new Vector3(112.15f, -2f, 0f);

        static LevelTwoSetup()
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

        [MenuItem("Tools/Interaction/Setup Level 2 Warm Statues")]
        static void Run()
        {
            var log = new StringBuilder();
            try
            {
                Scene active = SceneManager.GetActiveScene();
                ZoneMusic zmOpen = UnityEngine.Object.FindAnyObjectByType<ZoneMusic>();
                if (!(active.path == MapScene && zmOpen != null && zmOpen.zones.Count == 0)) EditorSceneManager.SaveOpenScenes();
                Directory.CreateDirectory("Backups");
                File.Copy(MapScene, "Backups/FourfoldCitadel_WithOurStuff_before_level2.unity", true);
                AssetDatabase.Refresh();

                int textures = 0;
                foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Res }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    var ti = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (ti == null) continue;
                    ti.textureType = TextureImporterType.Default;
                    ti.mipmapEnabled = false;
                    ti.wrapMode = TextureWrapMode.Clamp;
                    ti.alphaIsTransparency = true;
                    ti.npotScale = TextureImporterNPOTScale.None;
                    ti.maxTextureSize = 2048;
                    ti.textureCompression = TextureImporterCompression.Uncompressed;
                    ti.filterMode = FilterMode.Bilinear;
                    ti.SaveAndReimport();
                    textures++;
                }
                foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { Res }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    var ai = AssetImporter.GetAtPath(path) as AudioImporter;
                    if (ai == null) continue;
                    AudioImporterSampleSettings s = ai.defaultSampleSettings;
                    s.loadType = AudioClipLoadType.DecompressOnLoad;
                    s.compressionFormat = AudioCompressionFormat.Vorbis;
                    s.quality = 0.6f;
                    ai.defaultSampleSettings = s;
                    ai.SaveAndReimport();
                }
                log.AppendLine("textures configured: " + textures + ", font " + (AssetDatabase.LoadAssetAtPath<Font>(Res + "/DotGothic16.ttf") != null));

                Scene mini = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var mg = new GameObject("Warm Statues 2D");
                WarmStatues2D ws = mg.AddComponent<WarmStatues2D>();
                ws.returnScene = Path.GetFileNameWithoutExtension(MapScene);
                mg.AddComponent<Cursor2DFix>();
                EditorSceneManager.SaveScene(mini, MiniScene);
                log.AppendLine("minigame scene saved");

                Scene map = EditorSceneManager.OpenScene(MapScene, OpenSceneMode.Single);
                Physics.SyncTransforms();
                FPCharacterMover mover = UnityEngine.Object.FindAnyObjectByType<FPCharacterMover>(FindObjectsInactive.Include);

                RoundTablePuzzle puzzle = null;
                LockedGate gate = null;
                foreach (PuzzleGateLink link in UnityEngine.Object.FindObjectsByType<PuzzleGateLink>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (link.puzzle != null) puzzle = link.puzzle;
                    if (link.gate != null) gate = link.gate;
                    log.AppendLine("removed PuzzleGateLink on " + link.name);
                    UnityEngine.Object.DestroyImmediate(link);
                }
                if (puzzle == null) puzzle = UnityEngine.Object.FindAnyObjectByType<RoundTablePuzzle>(FindObjectsInactive.Include);
                if (gate == null)
                {
                    float best = float.MaxValue;
                    foreach (LockedGate g in UnityEngine.Object.FindObjectsByType<LockedGate>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    {
                        float d = Mathf.Abs(g.transform.position.x - (EmberCenter.x + 23.25f));
                        if (d < best) { best = d; gate = g; }
                    }
                }
                if (puzzle == null || gate == null) throw new Exception("puzzle " + (puzzle != null) + " gate " + (gate != null));
                gate.isOpen = false;
                EditorUtility.SetDirty(gate);

                Bounds gb = RendererBounds(gate.gameObject);
                Vector3 c = gb.center;
                Vector3 dir = new Vector3(Mathf.Sign(EmberCenter.x - c.x), 0f, 0f);
                float floorY = gb.min.y;
                if (Physics.Raycast(new Vector3(c.x + dir.x * 1.5f, gb.max.y, c.z), Vector3.down, out RaycastHit fh, 20f, ~0, QueryTriggerInteraction.Ignore)) floorY = fh.point.y;
                float width = Mathf.Max(gb.size.z, 1f);
                log.AppendLine("gate " + gate.name + " centre " + c + " size " + gb.size + " floor " + floorY + " room side " + dir.x);

                GameObject old = GameObject.Find("Warm Statues Trigger");
                if (old != null) UnityEngine.Object.DestroyImmediate(old);
                var trig = new GameObject("Warm Statues Trigger");
                trig.transform.position = new Vector3(c.x + dir.x * 2.2f, floorY + 1.6f, c.z);
                BoxCollider box = trig.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.size = new Vector3(1.2f, 3.2f, Mathf.Max(width + 1.5f, 8.5f));

                ComicPanelTransition transition = trig.AddComponent<ComicPanelTransition>();
                transition.nextScene = Path.GetFileNameWithoutExtension(MiniScene);
                transition.puzzleSolved = false;
                transition.playerMovement = mover;
                transition.heroFolder = "WarmStatues";
                transition.nextPanel = AssetDatabase.LoadAssetAtPath<Texture2D>(Res + "/panel_preview.png");
                transition.nextPanelUv = new Rect(0f, 0f, 1f, 1f);
                transition.heroTarget = new Vector2(0.5f, 1f - 536f / 720f);
                transition.heroHeight = 138.24f / 720f;

                var ret = new GameObject("Return Point");
                ret.transform.SetParent(trig.transform, false);
                Vector3 rp = new Vector3(c.x + dir.x * 5.5f, floorY, c.z);
                string hitName = "none";
                if (Physics.Raycast(rp + Vector3.up * 2.5f, Vector3.down, out RaycastHit rh, 8f, ~0, QueryTriggerInteraction.Ignore)) { rp.y = rh.point.y; hitName = rh.collider.name; }
                ret.transform.SetPositionAndRotation(rp + Vector3.up * 0.12f, Quaternion.LookRotation(-dir, Vector3.up));
                log.AppendLine("return point " + ret.transform.position + " on " + hitName);

                MinigameGate mgate = trig.AddComponent<MinigameGate>();
                mgate.id = "warmstatues";
                mgate.gate = gate;
                mgate.transition = transition;
                mgate.returnPoint = ret.transform;

                PuzzleTransitionLink ptl = trig.AddComponent<PuzzleTransitionLink>();
                ptl.puzzle = puzzle;
                ptl.transition = transition;

                PrismPickup pickup = UnityEngine.Object.FindAnyObjectByType<PrismPickup>(FindObjectsInactive.Include);
                GameObject oldEquip = GameObject.Find("Prism Auto Equip");
                if (oldEquip != null) UnityEngine.Object.DestroyImmediate(oldEquip);
                if (pickup != null)
                {
                    var eq = new GameObject("Prism Auto Equip");
                    PrismAutoEquip pae = eq.AddComponent<PrismAutoEquip>();
                    pae.pickup = pickup;
                    pae.areaCenter = EmberCenter;
                    pae.areaSize = new Vector2(46.5f, 30f);
                    log.AppendLine("prism auto-equip linked to " + pickup.name);
                }
                else log.AppendLine("no prism pickup found");

                EditorSceneManager.MarkSceneDirty(map);
                EditorSceneManager.SaveScene(map);

                var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
                if (!list.Exists(s => s.path == MiniScene)) list.Add(new EditorBuildSettingsScene(MiniScene, true));
                EditorBuildSettings.scenes = list.ToArray();
                log.AppendLine("build: " + string.Join(" | ", Array.ConvertAll(EditorBuildSettings.scenes, s => s.path + (s.enabled ? "" : " (off)"))));

                ZoneMusic zm = UnityEngine.Object.FindAnyObjectByType<ZoneMusic>();
                log.AppendLine("zone music areas: " + (zm != null ? zm.zones.Count.ToString() : "none"));

                Snapshot(ret.transform.position + Vector3.up * 1.55f, c + Vector3.up * 0.5f, "Backups/level2_gate.png");
                log.AppendLine("DONE");
            }
            catch (Exception e)
            {
                log.AppendLine("FAILED: " + e);
            }
            File.WriteAllText(ReportPath, log.ToString());
        }

        static Bounds RendererBounds(GameObject go)
        {
            Renderer[] rs = go.GetComponentsInChildren<Renderer>(true);
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.one);
            Bounds b = rs[0].bounds;
            foreach (Renderer r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        static void Snapshot(Vector3 from, Vector3 at, string path)
        {
            var go = new GameObject("Snap Cam");
            Camera cam = go.AddComponent<Camera>();
            cam.fieldOfView = 70f;
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
