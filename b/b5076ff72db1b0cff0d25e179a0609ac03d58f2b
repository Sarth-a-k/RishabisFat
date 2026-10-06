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
    static class LunarStyleTest
    {
        const string TriggerPath = "Assets/Interaction/Editor/.run_lunarstyle";
        const string ReportPath = "Backups/lunarstyle_report.txt";
        const string MapScene = "Assets/Scenes/FourfoldCitadel_WithOurStuff.unity";
        const string Dir = "Assets/Interaction/Generated/LunarStyle";
        static readonly Vector3 Center = new Vector3(48.85f, 0f, 0f);
        static readonly Vector2 Size = new Vector2(48.1f, 36.4f);

        static LunarStyleTest()
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

        [MenuItem("Tools/Interaction/Lunar Style Test Snapshots")]
        static void Run()
        {
            var log = new StringBuilder();
            try
            {
                AssetDatabase.Refresh();
                foreach (string f in new[] { "painted_cave_wall_normal", "painted_cave_floor_normal" })
                {
                    var ti = AssetImporter.GetAtPath(Dir + "/" + f + ".png") as TextureImporter;
                    if (ti != null && ti.textureType != TextureImporterType.NormalMap) { ti.textureType = TextureImporterType.NormalMap; ti.SaveAndReimport(); }
                }
                Material wallMat = MakeMat("LunarStyle_Wall", "painted_cave_wall");
                Material floorMat = MakeMat("LunarStyle_Floor", "painted_cave_floor");

                Scene active = SceneManager.GetActiveScene();
                ZoneMusic zmOpen = UnityEngine.Object.FindAnyObjectByType<ZoneMusic>();
                bool stale = active.path == MapScene && zmOpen != null && zmOpen.zones.Count == 0;
                if (!stale) EditorSceneManager.SaveOpenScenes();
                EditorSceneManager.OpenScene(MapScene, OpenSceneMode.Single);

                var keep = new HashSet<Renderer>();
                foreach (MonoBehaviour mb in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include))
                {
                    string n = mb.GetType().Name;
                    if (n == "MirrorPickup" || n == "MirrorSocket" || n == "PrismMoonController" || n == "PuzzleTorch" || n == "TorchFlame" || n == "NPCCutscene" || n == "LockedGate" || n == "ShapeBeacon" || n == "PickupHighlight")
                        foreach (Renderer r in mb.GetComponentsInChildren<Renderer>(true)) keep.Add(r);
                }

                var floors = new List<Renderer>();
                var walls = new List<Renderer>();
                var matCount = new Dictionary<string, int>();
                foreach (MeshRenderer r in UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude))
                {
                    if (keep.Contains(r)) continue;
                    Bounds b = r.bounds;
                    Vector3 d = b.center - Center;
                    if (Mathf.Abs(d.x) > Size.x * 0.5f + 1.5f || Mathf.Abs(d.z) > Size.y * 0.5f + 1.5f) continue;
                    string nm = r.gameObject.name.ToLowerInvariant();
                    if (nm.Contains("torch") || nm.Contains("mirror") || nm.Contains("platform") || nm.Contains("prism") || nm.Contains("moon") || nm.Contains("seal") || nm.Contains("altar") || nm.Contains("sigil") || nm.Contains("ghost") || nm.Contains("fillet") || nm.Contains("bronze")) continue;
                    bool floor = b.size.y < 0.7f && b.max.y < 0.9f && b.size.x * b.size.z > 3f;
                    bool wall = b.size.y > 2.2f && (b.size.x < 2.5f || b.size.z < 2.5f) && b.size.x * b.size.z < 400f;
                    if (!floor && !wall) continue;
                    string mn = r.sharedMaterial != null ? r.sharedMaterial.name : "none";
                    string key = (floor ? "floor | " : "wall | ") + mn;
                    matCount[key] = matCount.TryGetValue(key, out int c) ? c + 1 : 1;
                    (floor ? floors : walls).Add(r);
                }
                foreach (var kv in matCount) log.AppendLine(kv.Key + " x" + kv.Value);
                log.AppendLine("floors " + floors.Count + ", walls " + walls.Count);

                var lightGo = new GameObject("Snap fill");
                Light fill = lightGo.AddComponent<Light>();
                fill.type = LightType.Point;
                fill.range = 60f;
                fill.intensity = 30f;
                fill.color = new Color(0.75f, 0.82f, 1f);
                lightGo.transform.position = Center + Vector3.up * 9f;
                Color oldSky = RenderSettings.ambientSkyColor, oldEq = RenderSettings.ambientEquatorColor, oldGr = RenderSettings.ambientGroundColor;
                RenderSettings.ambientSkyColor = new Color(0.16f, 0.18f, 0.26f);
                RenderSettings.ambientEquatorColor = new Color(0.10f, 0.11f, 0.16f);
                RenderSettings.ambientGroundColor = new Color(0.05f, 0.05f, 0.08f);

                var views = new[]
                {
                    new KeyValuePair<Vector3, Vector3>(new Vector3(27.5f, 1.7f, 0f), new Vector3(50f, 1.2f, 2f)),
                    new KeyValuePair<Vector3, Vector3>(new Vector3(41f, 1.8f, -4f), new Vector3(41f, 3.5f, 17f)),
                    new KeyValuePair<Vector3, Vector3>(new Vector3(66f, 6f, -14f), new Vector3(44f, 0f, 4f))
                };
                for (int i = 0; i < views.Length; i++) Snapshot(views[i].Key, views[i].Value, "Backups/lunarstyle_before_" + i + ".png");

                foreach (Renderer r in floors) Apply(r, floorMat);
                foreach (Renderer r in walls) Apply(r, wallMat);
                for (int i = 0; i < views.Length; i++) Snapshot(views[i].Key, views[i].Value, "Backups/lunarstyle_after_" + i + ".png");

                RenderSettings.ambientSkyColor = oldSky; RenderSettings.ambientEquatorColor = oldEq; RenderSettings.ambientGroundColor = oldGr;
                EditorSceneManager.OpenScene(MapScene, OpenSceneMode.Single);
                log.AppendLine("scene reloaded from disk, nothing saved");
                log.AppendLine("DONE");
            }
            catch (Exception e)
            {
                log.AppendLine("FAILED: " + e);
                try { EditorSceneManager.OpenScene(MapScene, OpenSceneMode.Single); } catch { }
            }
            File.WriteAllText(ReportPath, log.ToString());
        }

        static Material MakeMat(string name, string tex)
        {
            string path = Dir + "/" + name + ".mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
            Texture2D albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(Dir + "/" + tex + ".png");
            Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(Dir + "/" + tex + "_normal.png");
            m.SetTexture("_BaseMap", albedo);
            m.SetColor("_BaseColor", Color.white);
            if (normal != null) { m.SetTexture("_BumpMap", normal); m.EnableKeyword("_NORMALMAP"); m.SetFloat("_BumpScale", 0.8f); }
            m.SetFloat("_Smoothness", 0.18f);
            m.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(m);
            AssetDatabase.SaveAssets();
            return m;
        }

        static void Apply(Renderer r, Material src)
        {
            var inst = new Material(src);
            Vector3 s = r.bounds.size;
            float u = Mathf.Max(s.x, s.z), v = r is MeshRenderer && s.y > 1f ? s.y : Mathf.Min(s.x, s.z);
            inst.SetTextureScale("_BaseMap", new Vector2(Mathf.Max(1f, u / 4f), Mathf.Max(1f, v / 4f)));
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++) mats[i] = inst;
            r.sharedMaterials = mats;
        }

        static void Snapshot(Vector3 from, Vector3 at, string path)
        {
            var go = new GameObject("Snap Cam");
            Camera cam = go.AddComponent<Camera>();
            cam.fieldOfView = 70f;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 200f;
            go.transform.SetPositionAndRotation(from, Quaternion.LookRotation(at - from, Vector3.up));
            var rt = new RenderTexture(1280, 720, 24);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
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
