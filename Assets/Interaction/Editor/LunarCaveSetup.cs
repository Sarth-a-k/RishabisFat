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
    static class LunarCaveSetup
    {
        const string TriggerPath = "Assets/Interaction/Editor/.run_lunarcave";
        const string ReportPath = "Backups/lunarcave_report.txt";
        const string MapScene = "Assets/Scenes/FourfoldCitadel_WithOurStuff.unity";
        const string Dir = "Assets/Interaction/Generated/LunarStyle";
        const string RootName = "Lunar Cave Dressing";
        static readonly Vector3 Center = new Vector3(48.85f, 0f, 0f);
        static readonly Vector2 Size = new Vector2(48.1f, 36.4f);
        static readonly string[] SkinMaterials = { "Mayan carved limestone", "Citadel weathered ashlar" };
        static readonly string[] SkipWords = { "moon", "altar", "seal", "lunar", "banner", "gate", "door", "prism", "mirror", "platform", "torch", "sigil", "ghost", "bronze", "fillet", "water", "eye", "beam", "socket", "trigger", "quest", "lintel", "frame" };
        static readonly Vector2[] Beam = { new Vector2(26.96f, -13.12f), new Vector2(52.91f, -5.69f), new Vector2(30.95f, 6.42f), new Vector2(47.13f, 6.98f), new Vector2(41.05f, 11.7f), new Vector2(41.05f, 17.4f) };
        static readonly Vector3[] KeepClear = { new Vector3(26.96f, -13.12f, 3.2f), new Vector3(52.91f, -5.69f, 3.2f), new Vector3(30.95f, 6.42f, 3.2f), new Vector3(47.13f, 6.98f, 3.2f), new Vector3(41.05f, 11.7f, 3.5f), new Vector3(41.05f, 16.5f, 5.5f), new Vector3(51.29f, -5.4f, 2.5f) };

        static LunarCaveSetup()
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

        [MenuItem("Tools/Interaction/Lunar Cave Dressing")]
        static void Run()
        {
            var log = new StringBuilder();
            try
            {
                AssetDatabase.Refresh();
                Directory.CreateDirectory(Dir + "/Meshes");
                Directory.CreateDirectory(Dir + "/Materials");
                foreach (string f in new[] { "painted_cave_wall_normal", "painted_cave_floor_normal" })
                {
                    var ti = AssetImporter.GetAtPath(Dir + "/" + f + ".png") as TextureImporter;
                    if (ti != null && ti.textureType != TextureImporterType.NormalMap) { ti.textureType = TextureImporterType.NormalMap; ti.SaveAndReimport(); }
                }

                Scene active = SceneManager.GetActiveScene();
                ZoneMusic zmOpen = UnityEngine.Object.FindAnyObjectByType<ZoneMusic>();
                bool stale = active.path == MapScene && zmOpen != null && zmOpen.zones.Count == 0;
                if (!stale) EditorSceneManager.SaveOpenScenes();
                else log.AppendLine("open map looked stale, reloading from disk");
                Directory.CreateDirectory("Backups");
                File.Copy(MapScene, "Backups/FourfoldCitadel_WithOurStuff_before_lunarcave_run2.unity", true);
                Scene map = EditorSceneManager.OpenScene(MapScene, OpenSceneMode.Single);
                Physics.SyncTransforms();

                GameObject old = GameObject.Find(RootName);
                if (old != null) UnityEngine.Object.DestroyImmediate(old);

                int skinned = SkinRoom(log);
                log.AppendLine("re-skinned renderers: " + skinned);

                var root = new GameObject(RootName);
                Material rockMat = TiledMat("Rock", "painted_cave_wall", Vector2.one, 0.15f);
                Material crystalMat = CrystalMat();
                var rocks = new Mesh[8];
                for (int i = 0; i < rocks.Length; i++) rocks[i] = SaveMesh(RockMesh(1000 + i), "rock_" + i);
                var crystals = new Mesh[3];
                for (int i = 0; i < crystals.Length; i++) crystals[i] = SaveMesh(CrystalMesh(2000 + i), "crystal_" + i);
                var drips = new Mesh[3];
                for (int i = 0; i < drips.Length; i++) drips[i] = SaveMesh(StalactiteMesh(3000 + i), "stalactite_" + i);

                var rng = new System.Random(77);
                int clusters = 0, crystalClusters = 0, lights = 0, rockCount = 0, stal = 0;
                var spots = PerimeterSpots(rng);
                foreach (Vector3 s in spots)
                {
                    if (!Clear(s, 1.2f)) continue;
                    clusters++;
                    bool crystal = clusters % 3 == 0;
                    var cg = new GameObject(crystal ? "Crystal cluster" : "Rock cluster");
                    cg.transform.SetParent(root.transform, false);
                    cg.transform.position = Ground(s);
                    int n = crystal ? 1 + rng.Next(2) : 2 + rng.Next(3);
                    for (int k = 0; k < n; k++)
                    {
                        float sc = (float)(0.5 + rng.NextDouble() * (k == 0 ? 1.3 : 0.7));
                        Vector3 off = new Vector3((float)(rng.NextDouble() - 0.5) * 1.6f, 0f, (float)(rng.NextDouble() - 0.5) * 1.6f);
                        var r = Piece(cg.transform, rocks[rng.Next(rocks.Length)], rockMat, off, new Vector3(sc * (1f + (float)rng.NextDouble() * 0.5f), sc * (0.6f + (float)rng.NextDouble() * 0.6f), sc), rng, true);
                        rockCount++;
                    }
                    if (crystal)
                    {
                        crystalClusters++;
                        int m = 3 + rng.Next(4);
                        for (int k = 0; k < m; k++)
                        {
                            float h = (float)(0.6 + rng.NextDouble() * 1.6);
                            Vector3 off = new Vector3((float)(rng.NextDouble() - 0.5) * 1.2f, 0f, (float)(rng.NextDouble() - 0.5) * 1.2f);
                            var c = Piece(cg.transform, crystals[rng.Next(crystals.Length)], crystalMat, off, new Vector3(h * 0.35f, h, h * 0.35f), rng, false);
                            c.transform.localRotation = Quaternion.Euler((float)(rng.NextDouble() - 0.5) * 50f, (float)rng.NextDouble() * 360f, (float)(rng.NextDouble() - 0.5) * 50f);
                        }
                        if (crystalClusters % 2 == 1 && lights < 7)
                        {
                            var lg = new GameObject("Crystal glow");
                            lg.transform.SetParent(cg.transform, false);
                            lg.transform.localPosition = Vector3.up * 1.1f;
                            Light l = lg.AddComponent<Light>();
                            l.type = LightType.Point;
                            l.color = new Color(0.55f, 0.72f, 1f);
                            l.intensity = 2.2f;
                            l.range = 6f;
                            l.shadows = LightShadows.None;
                            lights++;
                        }
                    }
                    int rubble = 3 + rng.Next(4);
                    for (int k = 0; k < rubble; k++)
                    {
                        float sc = (float)(0.12 + rng.NextDouble() * 0.22);
                        Vector3 off = new Vector3((float)(rng.NextDouble() - 0.5) * 3f, 0f, (float)(rng.NextDouble() - 0.5) * 3f);
                        Piece(cg.transform, rocks[rng.Next(rocks.Length)], rockMat, off, Vector3.one * sc, rng, false);
                    }
                }

                for (int i = 0; i < 70 && stal < 34; i++)
                {
                    float x = Center.x + (float)(rng.NextDouble() - 0.5) * (Size.x - 3f);
                    float z = (float)(rng.NextDouble() - 0.5) * (Size.y - 3f);
                    if (!Physics.Raycast(new Vector3(x, 2f, z), Vector3.up, out RaycastHit hit, 30f, ~0, QueryTriggerInteraction.Ignore)) continue;
                    if (hit.point.y < 5f) continue;
                    var sg = new GameObject("Stalactite");
                    sg.transform.SetParent(root.transform, false);
                    sg.transform.position = hit.point + Vector3.up * 0.15f;
                    float len = (float)(0.8 + rng.NextDouble() * 2.4);
                    float w = len * (float)(0.22 + rng.NextDouble() * 0.12);
                    sg.transform.localScale = new Vector3(w, len, w);
                    sg.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
                    sg.AddComponent<MeshFilter>().sharedMesh = drips[rng.Next(drips.Length)];
                    var mr = sg.AddComponent<MeshRenderer>();
                    mr.sharedMaterial = rockMat;
                    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    stal++;
                }

                foreach (float fx in new[] { 33f, 48.85f, 64.5f })
                {
                    var fillGo = new GameObject("Cave moonlight");
                    fillGo.transform.SetParent(root.transform, false);
                    fillGo.transform.position = new Vector3(fx, 8f, 0f);
                    Light fill = fillGo.AddComponent<Light>();
                    fill.type = LightType.Point;
                    fill.color = new Color(0.66f, 0.75f, 1f);
                    fill.range = 32f;
                    fill.intensity = 22f;
                    fill.shadows = LightShadows.None;
                }

                log.AppendLine("clusters " + clusters + " (crystal " + crystalClusters + "), rocks " + rockCount + ", crystal lights " + lights + ", stalactites " + stal);

                EditorSceneManager.MarkSceneDirty(map);
                EditorSceneManager.SaveScene(map);
                ZoneMusic zm = UnityEngine.Object.FindAnyObjectByType<ZoneMusic>();
                log.AppendLine("zone music areas: " + (zm != null ? zm.zones.Count.ToString() : "none"));

                Snapshot(new Vector3(27.5f, 1.7f, 0f), new Vector3(50f, 1.2f, 2f), "Backups/lunarcave_0.png");
                Snapshot(new Vector3(41f, 1.8f, -4f), new Vector3(41f, 3.5f, 17f), "Backups/lunarcave_1.png");
                Snapshot(new Vector3(66f, 6f, -14f), new Vector3(44f, 0f, 4f), "Backups/lunarcave_2.png");
                Snapshot(new Vector3(30f, 2.2f, 15f), new Vector3(60f, 0.5f, -10f), "Backups/lunarcave_3.png");
                log.AppendLine("DONE");
            }
            catch (Exception e)
            {
                log.AppendLine("FAILED: " + e);
            }
            File.WriteAllText(ReportPath, log.ToString());
        }

        static bool Skip(Transform t)
        {
            for (Transform p = t; p != null && p.parent != null; p = p.parent)
            {
                string n = p.name.ToLowerInvariant();
                if (n.StartsWith("01 ") || n.StartsWith("the fourfold")) break;
                foreach (string w in SkipWords) if (n.Contains(w)) return true;
                foreach (MonoBehaviour mb in p.GetComponents<MonoBehaviour>())
                    if (mb != null && mb.GetType().Namespace == "FPCharacter") return true;
            }
            return false;
        }

        static int SkinRoom(StringBuilder log)
        {
            var cache = new Dictionary<string, Material>();
            int count = 0;
            foreach (MeshRenderer r in UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude))
            {
                Bounds b = r.bounds;
                Vector3 d = b.center - Center;
                if (Mathf.Abs(d.x) > Size.x * 0.5f + 1.5f || Mathf.Abs(d.z) > Size.y * 0.5f + 1.5f) continue;
                Material sm = r.sharedMaterial;
                if (sm == null) continue;
                bool match = false;
                foreach (string m in SkinMaterials) if (sm.name.StartsWith(m)) match = true;
                if (!match || Skip(r.transform)) continue;
                bool floor = b.size.y < 0.7f && b.max.y < 0.9f && b.size.x * b.size.z > 3f;
                bool wall = b.size.y > 2.2f && (b.size.x < 2.5f || b.size.z < 2.5f) && b.size.x * b.size.z < 400f;
                if (!floor && !wall) continue;
                Vector3 s = b.size;
                float u = Mathf.Max(s.x, s.z), v = floor ? Mathf.Min(s.x, s.z) : s.y;
                Vector2 tile = new Vector2(Mathf.Max(1f, Mathf.Round(u / 4f * 2f) / 2f), Mathf.Max(1f, Mathf.Round(v / 4f * 2f) / 2f));
                string key = (floor ? "Floor_" : "Wall_") + tile.x.ToString("0.0") + "x" + tile.y.ToString("0.0");
                if (!cache.TryGetValue(key, out Material mat))
                {
                    mat = TiledMat(key, floor ? "painted_cave_floor" : "painted_cave_wall", tile, floor ? 0.22f : 0.15f);
                    cache[key] = mat;
                }
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                r.sharedMaterials = mats;
                count++;
            }
            log.AppendLine("tiling materials: " + cache.Count);
            return count;
        }

        static Material TiledMat(string name, string tex, Vector2 tile, float smooth)
        {
            string path = Dir + "/Materials/LunarCave_" + name + ".mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Dir + "/" + tex + ".png"));
            m.SetTextureScale("_BaseMap", tile);
            m.SetColor("_BaseColor", Color.white);
            Texture2D n = AssetDatabase.LoadAssetAtPath<Texture2D>(Dir + "/" + tex + "_normal.png");
            if (n != null) { m.SetTexture("_BumpMap", n); m.SetTextureScale("_BumpMap", tile); m.EnableKeyword("_NORMALMAP"); m.SetFloat("_BumpScale", 0.8f); }
            m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material CrystalMat()
        {
            string path = Dir + "/Materials/LunarCave_Crystal.mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
            Texture2D t = AssetDatabase.LoadAssetAtPath<Texture2D>(Dir + "/painted_moon_crystal.png");
            m.SetTexture("_BaseMap", t);
            m.SetColor("_BaseColor", new Color(0.8f, 0.88f, 1f));
            m.EnableKeyword("_EMISSION");
            m.SetTexture("_EmissionMap", t);
            m.SetColor("_EmissionColor", new Color(0.35f, 0.5f, 0.95f) * 1.4f);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            m.SetFloat("_Smoothness", 0.75f);
            EditorUtility.SetDirty(m);
            return m;
        }

        static Mesh SaveMesh(Mesh m, string name)
        {
            string path = Dir + "/Meshes/" + name + ".asset";
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null) { existing.Clear(); EditorUtility.CopySerialized(m, existing); return existing; }
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        static GameObject Piece(Transform parent, Mesh mesh, Material mat, Vector3 off, Vector3 scale, System.Random rng, bool collide)
        {
            var g = new GameObject(mat.name.Contains("Crystal") ? "Crystal" : "Rock");
            g.transform.SetParent(parent, false);
            g.transform.localPosition = off + Vector3.up * scale.y * 0.25f;
            g.transform.localRotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
            g.transform.localScale = scale;
            g.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = g.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (collide) { var mc = g.AddComponent<MeshCollider>(); mc.sharedMesh = mesh; mc.convex = true; }
            GameObjectUtility.SetStaticEditorFlags(g, StaticEditorFlags.BatchingStatic);
            return g;
        }

        static List<Vector3> PerimeterSpots(System.Random rng)
        {
            var list = new List<Vector3>();
            float x0 = Center.x - Size.x * 0.5f, x1 = Center.x + Size.x * 0.5f, z0 = -Size.y * 0.5f, z1 = Size.y * 0.5f;
            for (float x = x0 + 2f; x < x1 - 2f; x += 3.1f)
            {
                list.Add(new Vector3(x + (float)rng.NextDouble(), 0f, z0 + 1.2f + (float)rng.NextDouble() * 1.4f));
                list.Add(new Vector3(x + (float)rng.NextDouble(), 0f, z1 - 1.2f - (float)rng.NextDouble() * 1.4f));
            }
            for (float z = z0 + 2f; z < z1 - 2f; z += 3.1f)
            {
                list.Add(new Vector3(x0 + 1.2f + (float)rng.NextDouble() * 1.4f, 0f, z + (float)rng.NextDouble()));
                list.Add(new Vector3(x1 - 1.2f - (float)rng.NextDouble() * 1.4f, 0f, z + (float)rng.NextDouble()));
            }
            list.Add(new Vector3(x0 + 3.5f, 0f, z0 + 3.5f)); list.Add(new Vector3(x1 - 3.5f, 0f, z0 + 3.5f));
            list.Add(new Vector3(x0 + 3.5f, 0f, z1 - 3.5f)); list.Add(new Vector3(x1 - 3.5f, 0f, z1 - 3.5f));
            return list;
        }

        static bool Clear(Vector3 p, float r)
        {
            Vector2 q = new Vector2(p.x, p.z);
            if (p.x < 33f && Mathf.Abs(p.z) < 5f) return false;
            if (p.x > 64f && Mathf.Abs(p.z) < 5.5f) return false;
            for (int i = 0; i < Beam.Length - 1; i++) if (SegDist(q, Beam[i], Beam[i + 1]) < 2.2f + r) return false;
            foreach (Vector3 k in KeepClear) if (Vector2.Distance(q, new Vector2(k.x, k.y)) < k.z + r) return false;
            return true;
        }

        static float SegDist(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(0.0001f, ab.sqrMagnitude));
            return Vector2.Distance(p, a + ab * t);
        }

        static Vector3 Ground(Vector3 p)
        {
            if (Physics.Raycast(new Vector3(p.x, 4f, p.z), Vector3.down, out RaycastHit hit, 10f, ~0, QueryTriggerInteraction.Ignore)) return hit.point;
            return new Vector3(p.x, 0f, p.z);
        }

        static Mesh RockMesh(int seed)
        {
            var rng = new System.Random(seed);
            var t = (1f + Mathf.Sqrt(5f)) / 2f;
            var v = new List<Vector3> { new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0), new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t), new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1) };
            int[] f = { 0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8, 3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1 };
            var mid = new Dictionary<long, int>();
            var tris = new List<int>();
            int Mid(int a, int b)
            {
                long key = a < b ? ((long)a << 32) + b : ((long)b << 32) + a;
                if (mid.TryGetValue(key, out int i)) return i;
                v.Add((v[a] + v[b]) * 0.5f);
                mid[key] = v.Count - 1;
                return v.Count - 1;
            }
            for (int i = 0; i < f.Length; i += 3)
            {
                int a = f[i], b = f[i + 1], c = f[i + 2];
                int ab = Mid(a, b), bc = Mid(b, c), ca = Mid(c, a);
                tris.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
            }
            for (int i = 0; i < v.Count; i++)
            {
                Vector3 n = v[i].normalized;
                float k = 0.78f + (float)rng.NextDouble() * 0.42f;
                Vector3 p = n * k * 0.5f;
                if (p.y < -0.12f) p.y = -0.12f + (p.y + 0.12f) * 0.25f;
                v[i] = p;
            }
            var verts = new List<Vector3>(); var uvs = new List<Vector2>(); var idx = new List<int>();
            for (int i = 0; i < tris.Count; i += 3)
            {
                for (int k = 0; k < 3; k++)
                {
                    Vector3 p = v[tris[i + k]];
                    verts.Add(p);
                    uvs.Add(new Vector2(Mathf.Atan2(p.z, p.x) / (2f * Mathf.PI) * 2f + 0.5f, p.y + 0.5f));
                    idx.Add(verts.Count - 1);
                }
            }
            var m = new Mesh { name = "Lunar rock " + seed };
            m.SetVertices(verts); m.SetUVs(0, uvs); m.SetTriangles(idx, 0);
            m.RecalculateNormals(); m.RecalculateTangents(); m.RecalculateBounds();
            return m;
        }

        static Mesh CrystalMesh(int seed)
        {
            var rng = new System.Random(seed);
            int sides = 6;
            float tip = 1f, shoulder = 0.72f + (float)rng.NextDouble() * 0.1f;
            var verts = new List<Vector3>(); var uvs = new List<Vector2>(); var idx = new List<int>();
            void Tri(Vector3 a, Vector3 b, Vector3 c)
            {
                verts.Add(a); verts.Add(b); verts.Add(c);
                uvs.Add(new Vector2(0f, 0f)); uvs.Add(new Vector2(1f, 0f)); uvs.Add(new Vector2(0.5f, 1f));
                idx.Add(verts.Count - 3); idx.Add(verts.Count - 2); idx.Add(verts.Count - 1);
            }
            Vector3 top = new Vector3(0f, tip, 0f);
            for (int i = 0; i < sides; i++)
            {
                float a0 = i / (float)sides * Mathf.PI * 2f, a1 = (i + 1) / (float)sides * Mathf.PI * 2f;
                Vector3 b0 = new Vector3(Mathf.Cos(a0) * 0.5f, 0f, Mathf.Sin(a0) * 0.5f), b1 = new Vector3(Mathf.Cos(a1) * 0.5f, 0f, Mathf.Sin(a1) * 0.5f);
                Vector3 s0 = new Vector3(b0.x, shoulder, b0.z), s1 = new Vector3(b1.x, shoulder, b1.z);
                Tri(b0, s0, s1); Tri(b0, s1, b1); Tri(s0, top, s1);
            }
            var m = new Mesh { name = "Moon crystal " + seed };
            m.SetVertices(verts); m.SetUVs(0, uvs); m.SetTriangles(idx, 0);
            m.RecalculateNormals(); m.RecalculateTangents(); m.RecalculateBounds();
            return m;
        }

        static Mesh StalactiteMesh(int seed)
        {
            var rng = new System.Random(seed);
            int sides = 7, rings = 4;
            var ringPts = new List<Vector3[]>();
            for (int r = 0; r <= rings; r++)
            {
                float y = -r / (float)rings;
                float rad = 0.5f * (1f - r / (float)rings) * (0.85f + (float)rng.NextDouble() * 0.3f);
                var pts = new Vector3[sides];
                for (int s = 0; s < sides; s++)
                {
                    float a = s / (float)sides * Mathf.PI * 2f;
                    float j = 0.8f + (float)rng.NextDouble() * 0.4f;
                    pts[s] = new Vector3(Mathf.Cos(a) * rad * j, y, Mathf.Sin(a) * rad * j);
                }
                ringPts.Add(pts);
            }
            var verts = new List<Vector3>(); var uvs = new List<Vector2>(); var idx = new List<int>();
            for (int r = 0; r < rings; r++)
                for (int s = 0; s < sides; s++)
                {
                    int s1 = (s + 1) % sides;
                    Vector3 a = ringPts[r][s], b = ringPts[r][s1], c = ringPts[r + 1][s], d = ringPts[r + 1][s1];
                    foreach (Vector3 p in new[] { a, c, b, b, c, d })
                    {
                        verts.Add(p);
                        uvs.Add(new Vector2(s / (float)sides, -p.y));
                        idx.Add(verts.Count - 1);
                    }
                }
            var m = new Mesh { name = "Stalactite " + seed };
            m.SetVertices(verts); m.SetUVs(0, uvs); m.SetTriangles(idx, 0);
            m.RecalculateNormals(); m.RecalculateTangents(); m.RecalculateBounds();
            return m;
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
