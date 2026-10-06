using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FPCharacter.EditorTools
{
    [InitializeOnLoad]
    static class LavaCryptBuilder
    {
        const string TriggerPath = "Assets/Interaction/Editor/.run_lavacrypt";
        const string WorkScene = "Assets/Scenes/FourfoldCitadel_WithOurStuff.unity";
        const string Dir = "Assets/Interaction/Generated/LavaCrypt";
        const string RootName = "02 - THE EMBER CRYPT (lava)";
        const string OldRoomPrefix = "02 • The Ember Crypt";
        static readonly Vector3 Center = new Vector3(112.15f, -2f, 0f);
        const float HX = 23.25f, HZ = 15f, CEIL = 4.4f, DOME_R = 7f, DOME_H = 7.4f, WALL_T = 1f, WALL_H = 9f, OPEN = 4f;

        static StringBuilder report;
        static Mesh meshContainer;
        static int meshCount;
        static Transform root;
        static Material floorM, wallM, ceilM, colM, basaltM, lavaPoolM, lavaFallM, bronzeM, crystalM, darkM;

        static LavaCryptBuilder()
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

        static void Log(string s) { report.AppendLine(s); }

        [MenuItem("Tools/Interaction/Rebuild Lava Ember Crypt")]
        static void Run()
        {
            report = new StringBuilder();
            try
            {
                EditorSceneManager.SaveOpenScenes();
                Directory.CreateDirectory("Backups");
                File.Copy(WorkScene, "Backups/FourfoldCitadel_WithOurStuff_before_lavacrypt.unity", true);
                Scene s = EditorSceneManager.OpenScene(WorkScene, OpenSceneMode.Single);
                SceneManager.SetActiveScene(s);
                Directory.CreateDirectory(Dir);

                foreach (GameObject r in s.GetRootGameObjects())
                    foreach (Transform t in r.GetComponentsInChildren<Transform>(true))
                    {
                        if (t.name == RootName) { UnityEngine.Object.DestroyImmediate(t.gameObject); break; }
                    }
                int disabled = 0;
                foreach (GameObject r in s.GetRootGameObjects())
                    foreach (Transform t in r.GetComponentsInChildren<Transform>(true))
                        if (t.name.StartsWith(OldRoomPrefix, StringComparison.Ordinal) && t.gameObject.activeSelf) { t.gameObject.SetActive(false); disabled++; }
                Log("old ember room objects disabled: " + disabled);

                BuildMaterials();
                string meshPath = Dir + "/LavaCryptMeshes.asset";
                AssetDatabase.DeleteAsset(meshPath);
                meshContainer = null;
                meshCount = 0;

                GameObject rootGo = new GameObject(RootName);
                SceneManager.MoveGameObjectToScene(rootGo, s);
                Transform citadel = s.GetRootGameObjects().Select(g => g.transform).FirstOrDefault(t => t.name.StartsWith("THE FOURFOLD CITADEL", StringComparison.Ordinal));
                if (citadel != null) rootGo.transform.SetParent(citadel, true);
                rootGo.transform.position = Center;
                root = rootGo.transform;

                BuildShell();
                BuildColumnsAndTombs();
                BuildLava();
                BuildAlcove(s);
                BuildCrystalAndTable(s);
                BuildEastDoor(s);
                BuildTorches();

                AssetDatabase.SaveAssets();
                EditorSceneManager.MarkSceneDirty(s);
                EditorSceneManager.SaveScene(s);
                Log("DONE meshes " + meshCount);
                Snapshots();
                File.WriteAllText("Backups/lavacrypt_report.txt", report.ToString());
                CryptFixSetup.Run();
            }
            catch (Exception e)
            {
                Log("FAILED " + e);
            }
            File.WriteAllText("Backups/lavacrypt_report.txt", report.ToString());
        }

        static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 982451653);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return h / 4294967295f;
            }
        }

        static float VNoise(float x, float y, int period, int seed)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            int ax = ((x0 % period) + period) % period, ay = ((y0 % period) + period) % period;
            int bx = (ax + 1) % period, by = (ay + 1) % period;
            float a = Hash(ax, ay, seed), b = Hash(bx, ay, seed), c = Hash(ax, by, seed), d = Hash(bx, by, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        static float Fbm(float u, float v, int baseFreq, int octaves, int seed)
        {
            float sum = 0f, amp = 0.5f, norm = 0f;
            int f = baseFreq;
            for (int o = 0; o < octaves; o++)
            {
                sum += amp * VNoise(u * f, v * f, f, seed + o * 31);
                norm += amp;
                amp *= 0.5f;
                f *= 2;
            }
            return sum / norm;
        }

        static Texture2D SaveTex(string name, Color[] px, int n, bool normal, bool linear)
        {
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            tex.SetPixels(px);
            tex.Apply();
            string path = Dir + "/" + name + ".png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            TextureImporter ti = (TextureImporter)AssetImporter.GetAtPath(path);
            ti.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            ti.sRGBTexture = !linear && !normal;
            ti.wrapMode = TextureWrapMode.Repeat;
            ti.maxTextureSize = 1024;
            ti.mipmapEnabled = true;
            ti.anisoLevel = 4;
            ti.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static Color[] NormalFromHeight(float[] h, int n, float strength)
        {
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float l = h[y * n + (x - 1 + n) % n], r = h[y * n + (x + 1) % n];
                    float d = h[((y - 1 + n) % n) * n + x], u = h[((y + 1) % n) * n + x];
                    Vector3 nv = new Vector3((l - r) * strength, (d - u) * strength, 1f).normalized;
                    px[y * n + x] = new Color(nv.x * 0.5f + 0.5f, nv.y * 0.5f + 0.5f, nv.z * 0.5f + 0.5f, 1f);
                }
            return px;
        }

        static void FloorTextures(out Texture2D alb, out Texture2D nrm, out Texture2D emi)
        {
            const int N = 512, T = 128, G = 5;
            var a = new Color[N * N];
            var e = new Color[N * N];
            var h = new float[N * N];
            var crack = new float[N * N];
            var rng = new System.Random(41);
            for (int c = 0; c < 4; c++)
            {
                float x = (float)rng.NextDouble() * N, y = (float)rng.NextDouble() * N;
                float ang = (float)rng.NextDouble() * Mathf.PI * 2f;
                int len = 60 + rng.Next(90);
                for (int i = 0; i < len; i++)
                {
                    ang += ((float)rng.NextDouble() - 0.5f) * 0.9f;
                    x += Mathf.Cos(ang) * 2f; y += Mathf.Sin(ang) * 2f;
                    for (int oy = -2; oy <= 2; oy++)
                        for (int ox = -2; ox <= 2; ox++)
                        {
                            int px = (((int)x + ox) % N + N) % N, py = (((int)y + oy) % N + N) % N;
                            float d = Mathf.Sqrt(ox * ox + oy * oy);
                            crack[py * N + px] = Mathf.Max(crack[py * N + px], Mathf.Clamp01(1.6f - d * 0.6f));
                        }
                }
            }
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    int tx = x / T, ty = y / T, lx = x % T, ly = y % T;
                    int d = Mathf.Min(Mathf.Min(lx, ly), Mathf.Min(T - 1 - lx, T - 1 - ly));
                    float u = (float)x / N, v = (float)y / N;
                    float detail = Fbm(u, v, 8, 5, 3);
                    float tint = 0.75f + 0.45f * Hash(tx, ty, 9);
                    Color c = new Color(0.085f, 0.072f, 0.066f) * tint * (0.65f + 0.7f * detail);
                    float height = Mathf.Clamp01(d / 10f) * (0.8f + 0.2f * detail);
                    if (d < G) { c = new Color(0.02f, 0.017f, 0.016f); height = 0f; }
                    else if (d < G + 6) c *= Mathf.Lerp(0.55f, 1f, (d - G) / 6f);
                    float k = crack[y * N + x];
                    if (k > 0f)
                    {
                        c = Color.Lerp(c, new Color(0.25f, 0.06f, 0.02f), k);
                        height *= 1f - 0.7f * k;
                    }
                    c.a = 1f;
                    a[y * N + x] = c;
                    e[y * N + x] = new Color(1f, 0.32f, 0.04f) * (k * k);
                    h[y * N + x] = height;
                }
            alb = SaveTex("Floor_Albedo", a, N, false, false);
            nrm = SaveTex("Floor_Normal", NormalFromHeight(h, N, 6f), N, true, true);
            emi = SaveTex("Floor_Emission", e, N, false, false);
        }

        static void WallTextures(out Texture2D alb, out Texture2D nrm)
        {
            const int N = 512, BW = 256, BH = 128, G = 5;
            var a = new Color[N * N];
            var h = new float[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    int row = y / BH;
                    int xx = (x + (row % 2) * (BW / 2)) % N;
                    int col = xx / BW;
                    int lx = xx % BW, ly = y % BH;
                    int d = Mathf.Min(Mathf.Min(lx, ly), Mathf.Min(BW - 1 - lx, BH - 1 - ly));
                    float u = (float)x / N, v = (float)y / N;
                    float detail = Fbm(u, v, 8, 5, 17);
                    float chips = Fbm(u, v, 32, 3, 23);
                    float tint = 0.75f + 0.45f * Hash(col, row, 5);
                    Color c = new Color(0.1f, 0.082f, 0.072f) * tint * (0.6f + 0.8f * detail);
                    float height = Mathf.Clamp01(d / 12f) * (0.75f + 0.25f * detail) - Mathf.Max(0f, chips - 0.7f) * 1.5f;
                    if (d < G) { c = new Color(0.025f, 0.02f, 0.018f); height = 0f; }
                    else if (d < G + 7) c *= Mathf.Lerp(0.6f, 1f, (d - G) / 7f);
                    c.a = 1f;
                    a[y * N + x] = c;
                    h[y * N + x] = Mathf.Clamp01(height);
                }
            alb = SaveTex("Wall_Albedo", a, N, false, false);
            nrm = SaveTex("Wall_Normal", NormalFromHeight(h, N, 5f), N, true, true);
        }

        static Texture2D LavaTexture()
        {
            const int N = 512;
            var a = new Color[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float u = (float)x / N, v = (float)y / N;
                    float f = Fbm(u, v, 4, 5, 77);
                    float ridge = 1f - Mathf.Abs(2f * Fbm(u, v, 6, 4, 91) - 1f);
                    float t = Mathf.Clamp01(f * 0.55f + Mathf.Pow(ridge, 3f) * 0.75f);
                    Color c;
                    if (t < 0.38f) c = Color.Lerp(new Color(0.02f, 0.006f, 0.004f), new Color(0.22f, 0.03f, 0.01f), t / 0.38f);
                    else if (t < 0.55f) c = Color.Lerp(new Color(0.22f, 0.03f, 0.01f), new Color(0.95f, 0.25f, 0.02f), (t - 0.38f) / 0.17f);
                    else if (t < 0.75f) c = Color.Lerp(new Color(0.95f, 0.25f, 0.02f), new Color(1f, 0.6f, 0.08f), (t - 0.55f) / 0.2f);
                    else c = Color.Lerp(new Color(1f, 0.6f, 0.08f), new Color(1f, 0.92f, 0.5f), Mathf.Clamp01((t - 0.75f) / 0.25f));
                    c.a = 1f;
                    a[y * N + x] = c;
                }
            return SaveTex("Lava_Albedo", a, N, false, false);
        }

        static Material Lit(string name, Color baseColor, Texture2D alb, Texture2D nrm, float smooth, float metal)
        {
            string path = Dir + "/" + name + ".mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
            m.SetColor("_BaseColor", baseColor);
            m.SetTexture("_BaseMap", alb);
            if (nrm != null) { m.SetTexture("_BumpMap", nrm); m.SetFloat("_BumpScale", 1f); m.EnableKeyword("_NORMALMAP"); }
            m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_Metallic", metal);
            m.SetTextureScale("_BaseMap", Vector2.one);
            EditorUtility.SetDirty(m);
            return m;
        }

        static void Emissive(Material m, Texture2D map, Color c)
        {
            m.EnableKeyword("_EMISSION");
            if (map != null) m.SetTexture("_EmissionMap", map);
            m.SetColor("_EmissionColor", c);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            EditorUtility.SetDirty(m);
        }

        static void BuildMaterials()
        {
            FloorTextures(out Texture2D fa, out Texture2D fn, out Texture2D fe);
            WallTextures(out Texture2D wa, out Texture2D wn);
            Texture2D lava = LavaTexture();
            floorM = Lit("Crypt_Floor", Color.white, fa, fn, 0.28f, 0f);
            Emissive(floorM, fe, new Color(0.75f, 0.75f, 0.75f));
            wallM = Lit("Crypt_Wall", Color.white, wa, wn, 0.18f, 0f);
            ceilM = Lit("Crypt_Ceiling", new Color(0.7f, 0.68f, 0.66f), wa, wn, 0.12f, 0f);
            basaltM = Lit("Crypt_Basalt", new Color(0.62f, 0.6f, 0.6f), wa, wn, 0.3f, 0f);
            colM = Lit("Crypt_Column", new Color(0.85f, 0.82f, 0.8f), wa, wn, 0.2f, 0f);
            colM.SetTextureScale("_BaseMap", new Vector2(2f, 2.2f));
            lavaPoolM = Lit("Crypt_LavaPool", Color.white, lava, null, 0.55f, 0f);
            Emissive(lavaPoolM, lava, new Color(2.4f, 2.4f, 2.4f));
            lavaFallM = Lit("Crypt_LavaFall", Color.white, lava, null, 0.55f, 0f);
            Emissive(lavaFallM, lava, new Color(2.8f, 2.8f, 2.8f));
            lavaFallM.SetFloat("_Cull", 0f);
            bronzeM = Lit("Crypt_Bronze", new Color(0.4f, 0.26f, 0.1f), null, null, 0.55f, 0.85f);
            bronzeM.SetFloat("_Cull", 0f);
            darkM = Lit("Crypt_Dark", new Color(0.035f, 0.03f, 0.03f), null, null, 0.3f, 0f);
            crystalM = Lit("Crypt_RedCrystal", new Color(0.8f, 0.08f, 0.03f), null, null, 0.75f, 0f);
            Emissive(crystalM, null, new Color(2.2f, 0.12f, 0.04f));
            Log("materials ready");
        }

        static Mesh SaveMesh(Mesh m)
        {
            string path = Dir + "/LavaCryptMeshes.asset";
            m.name = "M" + (meshCount++) + "_" + m.name;
            if (meshContainer == null) { AssetDatabase.CreateAsset(m, path); meshContainer = m; }
            else AssetDatabase.AddObjectToAsset(m, path);
            return m;
        }

        static Mesh BoxMesh(Vector3 size, float tile)
        {
            var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var tr = new List<int>();
            Vector3 hs = size * 0.5f;
            void Face(Vector3 normal, Vector3 right, Vector3 up, float w, float h)
            {
                int b = v.Count;
                Vector3 c = Vector3.Scale(normal, hs);
                Vector3 r = right * (w * 0.5f), u = up * (h * 0.5f);
                v.Add(c - r - u); v.Add(c + r - u); v.Add(c + r + u); v.Add(c - r + u);
                for (int i = 0; i < 4; i++) n.Add(normal);
                uv.Add(new Vector2(0, 0)); uv.Add(new Vector2(w / tile, 0)); uv.Add(new Vector2(w / tile, h / tile)); uv.Add(new Vector2(0, h / tile));
                tr.Add(b); tr.Add(b + 2); tr.Add(b + 1); tr.Add(b); tr.Add(b + 3); tr.Add(b + 2);
            }
            Face(Vector3.forward, Vector3.left, Vector3.up, size.x, size.y);
            Face(Vector3.back, Vector3.right, Vector3.up, size.x, size.y);
            Face(Vector3.right, Vector3.forward, Vector3.up, size.z, size.y);
            Face(Vector3.left, Vector3.back, Vector3.up, size.z, size.y);
            Face(Vector3.up, Vector3.right, Vector3.forward, size.x, size.z);
            Face(Vector3.down, Vector3.right, Vector3.back, size.x, size.z);
            var m = new Mesh { name = "Box" };
            m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv); m.SetTriangles(tr, 0);
            m.RecalculateTangents(); m.RecalculateBounds();
            return SaveMesh(m);
        }

        static GameObject Box(string name, Vector3 localPos, Vector3 size, Material mat, float tile, bool collider, Transform parent = null, Quaternion? rot = null)
        {
            GameObject g = new GameObject(name);
            g.transform.SetParent(parent != null ? parent : root, false);
            g.transform.localPosition = localPos;
            g.transform.localRotation = rot ?? Quaternion.identity;
            Mesh m = BoxMesh(size, tile);
            g.AddComponent<MeshFilter>().sharedMesh = m;
            g.AddComponent<MeshRenderer>().sharedMaterial = mat;
            if (collider) g.AddComponent<BoxCollider>();
            GameObjectUtility.SetStaticEditorFlags(g, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
            return g;
        }

        static GameObject Prim(PrimitiveType type, string name, Vector3 localPos, Vector3 scale, Material mat, bool collider, Transform parent = null, Quaternion? rot = null)
        {
            GameObject g = GameObject.CreatePrimitive(type);
            g.name = name;
            g.transform.SetParent(parent != null ? parent : root, false);
            g.transform.localPosition = localPos;
            g.transform.localRotation = rot ?? Quaternion.identity;
            g.transform.localScale = scale;
            if (!collider) UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());
            g.GetComponent<Renderer>().sharedMaterial = mat;
            return g;
        }

        static Light PointLight(string name, Vector3 localPos, Color c, float intensity, float range, Transform parent = null)
        {
            GameObject g = new GameObject(name);
            g.transform.SetParent(parent != null ? parent : root, false);
            g.transform.localPosition = localPos;
            Light l = g.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = c;
            l.intensity = intensity;
            l.range = range;
            l.shadows = LightShadows.None;
            return l;
        }

        static void BuildShell()
        {
            Box("Floor", new Vector3(0f, -0.2f, 0f), new Vector3(HX * 2f, 0.4f, HZ * 2f), floorM, 4f, true);
            float wz = HZ + WALL_T * 0.5f, wx = HX + WALL_T * 0.5f;
            Box("Wall South", new Vector3(0f, WALL_H * 0.5f, -wz), new Vector3(HX * 2f + 2f * WALL_T, WALL_H, WALL_T), wallM, 2f, true);
            float alcoveX = -6f, alcoveHalf = 1.6f;
            float leftLen = (alcoveX - alcoveHalf) - (-HX - WALL_T);
            float rightLen = (HX + WALL_T) - (alcoveX + alcoveHalf);
            Box("Wall North A", new Vector3(-HX - WALL_T + leftLen * 0.5f, WALL_H * 0.5f, wz), new Vector3(leftLen, WALL_H, WALL_T), wallM, 2f, true);
            Box("Wall North B", new Vector3(HX + WALL_T - rightLen * 0.5f, WALL_H * 0.5f, wz), new Vector3(rightLen, WALL_H, WALL_T), wallM, 2f, true);
            Box("Wall North Over Alcove", new Vector3(alcoveX, (3.2f + WALL_H) * 0.5f, wz), new Vector3(alcoveHalf * 2f, WALL_H - 3.2f, WALL_T), wallM, 2f, true);
            foreach (int side in new[] { -1, 1 })
            {
                float segLen = HZ + WALL_T - OPEN;
                float segC = OPEN + segLen * 0.5f;
                Box(side < 0 ? "Wall West N" : "Wall East N", new Vector3(side * wx, WALL_H * 0.5f, segC), new Vector3(WALL_T, WALL_H, segLen), wallM, 2f, true);
                Box(side < 0 ? "Wall West S" : "Wall East S", new Vector3(side * wx, WALL_H * 0.5f, -segC), new Vector3(WALL_T, WALL_H, segLen), wallM, 2f, true);
                Box(side < 0 ? "Lintel West" : "Lintel East", new Vector3(side * wx, (CEIL + WALL_H) * 0.5f + 0.6f, 0f), new Vector3(WALL_T, WALL_H - CEIL - 1.2f, OPEN * 2f), wallM, 2f, true);
                Box("Door frame", new Vector3(side * (HX - 0.15f), CEIL * 0.5f, OPEN + 0.25f), new Vector3(0.5f, CEIL, 0.5f), basaltM, 1f, true);
                Box("Door frame", new Vector3(side * (HX - 0.15f), CEIL * 0.5f, -OPEN - 0.25f), new Vector3(0.5f, CEIL, 0.5f), basaltM, 1f, true);
            }

            GameObject ceil = new GameObject("Ceiling");
            ceil.transform.SetParent(root, false);
            ceil.AddComponent<MeshFilter>().sharedMesh = CeilingMesh();
            ceil.AddComponent<MeshRenderer>().sharedMaterial = ceilM;
            GameObject drum = new GameObject("Dome drum");
            drum.transform.SetParent(root, false);
            drum.AddComponent<MeshFilter>().sharedMesh = DrumMesh();
            drum.AddComponent<MeshRenderer>().sharedMaterial = wallM;
            Prim(PrimitiveType.Cylinder, "Dome cap", new Vector3(0f, DOME_H + 0.15f, 0f), new Vector3(DOME_R * 2f + 0.6f, 0.15f, DOME_R * 2f + 0.6f), ceilM, false);
            for (int k = 0; k < 16; k++)
            {
                float a = k * Mathf.PI * 2f / 16f;
                Box("Dome rib", new Vector3(Mathf.Cos(a) * (DOME_R - 0.2f), (CEIL + DOME_H) * 0.5f, Mathf.Sin(a) * (DOME_R - 0.2f)), new Vector3(0.4f, DOME_H - CEIL, 0.4f), colM, 1f, false, null, Quaternion.Euler(0f, -a * Mathf.Rad2Deg, 0f));
            }
            GameObject rim = new GameObject("Dome bronze rim");
            rim.transform.SetParent(root, false);
            rim.AddComponent<MeshFilter>().sharedMesh = RingMesh(DOME_R - 0.12f, DOME_R + 0.2f, CEIL - 0.14f, 0.14f, 64);
            rim.AddComponent<MeshRenderer>().sharedMaterial = bronzeM;
            Log("shell built");
        }

        static Mesh CeilingMesh()
        {
            const int seg = 96;
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var tr = new List<int>();
            for (int i = 0; i < seg; i++)
            {
                float a = i * Mathf.PI * 2f / seg;
                Vector2 d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                float tx = Mathf.Abs(d.x) > 1e-4f ? HX / Mathf.Abs(d.x) : float.MaxValue;
                float tz = Mathf.Abs(d.y) > 1e-4f ? HZ / Mathf.Abs(d.y) : float.MaxValue;
                float t = Mathf.Min(tx, tz);
                Vector3 inner = new Vector3(d.x * DOME_R, CEIL, d.y * DOME_R);
                Vector3 outer = new Vector3(d.x * t, CEIL, d.y * t);
                v.Add(inner); v.Add(outer);
                uv.Add(new Vector2(inner.x / 2f, inner.z / 2f)); uv.Add(new Vector2(outer.x / 2f, outer.z / 2f));
            }
            for (int i = 0; i < seg; i++)
            {
                int a0 = i * 2, a1 = a0 + 1, b0 = ((i + 1) % seg) * 2, b1 = b0 + 1;
                tr.Add(a0); tr.Add(b0); tr.Add(a1);
                tr.Add(a1); tr.Add(b0); tr.Add(b1);
            }
            var m = new Mesh { name = "Ceiling" };
            m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(tr, 0);
            m.RecalculateNormals();
            Vector3[] ns = m.normals;
            if (ns.Length > 0 && ns[0].y > 0f)
            {
                for (int i = 0; i < tr.Count; i += 3) { int t = tr[i + 1]; tr[i + 1] = tr[i + 2]; tr[i + 2] = t; }
                m.SetTriangles(tr, 0);
                m.RecalculateNormals();
            }
            m.RecalculateTangents(); m.RecalculateBounds();
            return SaveMesh(m);
        }

        static Mesh DrumMesh()
        {
            const int seg = 64;
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var tr = new List<int>();
            float circ = Mathf.PI * 2f * DOME_R;
            for (int i = 0; i <= seg; i++)
            {
                float a = i * Mathf.PI * 2f / seg;
                Vector3 d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * DOME_R;
                v.Add(d + Vector3.up * CEIL); v.Add(d + Vector3.up * DOME_H);
                float u = circ * i / seg / 2f;
                uv.Add(new Vector2(u, CEIL / 2f)); uv.Add(new Vector2(u, DOME_H / 2f));
            }
            for (int i = 0; i < seg; i++)
            {
                int a0 = i * 2, a1 = a0 + 1, b0 = a0 + 2, b1 = a0 + 3;
                tr.Add(a0); tr.Add(a1); tr.Add(b0);
                tr.Add(a1); tr.Add(b1); tr.Add(b0);
            }
            var m = new Mesh { name = "Drum" };
            m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(tr, 0);
            m.RecalculateNormals();
            if (Vector3.Dot(m.normals[0], -v[0]) < 0f)
            {
                for (int i = 0; i < tr.Count; i += 3) { int t = tr[i + 1]; tr[i + 1] = tr[i + 2]; tr[i + 2] = t; }
                m.SetTriangles(tr, 0);
                m.RecalculateNormals();
            }
            m.RecalculateTangents(); m.RecalculateBounds();
            return SaveMesh(m);
        }

        static Mesh RingMesh(float r0, float r1, float y, float h, int seg)
        {
            var v = new List<Vector3>(); var tr = new List<int>();
            float[][] prof = { new[] { r0, y }, new[] { r1, y }, new[] { r1, y + h }, new[] { r0, y + h } };
            for (int i = 0; i < seg; i++)
            {
                float a = i * Mathf.PI * 2f / seg;
                foreach (float[] p in prof) v.Add(new Vector3(Mathf.Cos(a) * p[0], p[1], Mathf.Sin(a) * p[0]));
            }
            for (int i = 0; i < seg; i++)
            {
                int A = i * 4, B = ((i + 1) % seg) * 4;
                for (int k = 0; k < 4; k++)
                {
                    int k2 = (k + 1) % 4;
                    tr.Add(A + k); tr.Add(B + k2); tr.Add(A + k2);
                    tr.Add(A + k); tr.Add(B + k); tr.Add(B + k2);
                }
            }
            var m = new Mesh { name = "Ring" };
            m.SetVertices(v); m.SetTriangles(tr, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return SaveMesh(m);
        }

        static void BuildColumnsAndTombs()
        {
            Transform cols = new GameObject("Columns").transform;
            cols.SetParent(root, false);
            int n = 0;
            for (int ix = -3; ix <= 3; ix++)
                foreach (float z in new[] { -11.2f, -5.6f, 5.6f, 11.2f })
                {
                    float x = ix * 6.2f;
                    if (new Vector2(x, z).magnitude < DOME_R + 1.8f) continue;
                    Prim(PrimitiveType.Cylinder, "Column", new Vector3(x, CEIL * 0.5f, z), new Vector3(1f, CEIL * 0.5f, 1f), colM, true, cols);
                    Box("Column base", new Vector3(x, 0.2f, z), new Vector3(1.3f, 0.4f, 1.3f), basaltM, 1f, true, cols);
                    Box("Column capital", new Vector3(x, CEIL - 0.2f, z), new Vector3(1.3f, 0.4f, 1.3f), basaltM, 1f, false, cols);
                    n++;
                }
            Transform tombs = new GameObject("Sarcophagi").transform;
            tombs.SetParent(root, false);
            foreach (float x in new[] { -15.5f, -9.3f, 9.3f, 15.5f })
                foreach (float z in new[] { -11f, 11f })
                {
                    Box("Sarcophagus", new Vector3(x, 0.45f, z), new Vector3(2.4f, 0.9f, 1.1f), basaltM, 1f, true, tombs);
                    Box("Sarcophagus lid", new Vector3(x, 0.97f, z), new Vector3(2.55f, 0.14f, 1.25f), wallM, 1f, true, tombs);
                }
            Transform fis = new GameObject("Heat fissures").transform;
            fis.SetParent(root, false);
            float[][] lines = { new[] { -20f, 7.5f, -12f, 9f }, new[] { 12f, -8.5f, 21f, -7.2f }, new[] { -6f, -10.2f, 4f, -9.4f }, new[] { 14f, 9.6f, 20f, 8.1f }, new[] { -21f, -4f, -16f, -6f } };
            foreach (float[] l in lines)
            {
                Vector3 prev = Vector3.zero;
                for (int i = 0; i <= 9; i++)
                {
                    float t = i / 9f;
                    Vector3 p = new Vector3(l[0] + (l[2] - l[0]) * t, 0.012f, l[1] + (l[3] - l[1]) * t + Mathf.Sin(t * 9f + l[0]) * 0.35f);
                    if (i > 0)
                    {
                        Vector3 d = p - prev;
                        GameObject f = Box("Fissure", (p + prev) * 0.5f, new Vector3(d.magnitude + 0.05f, 0.02f, 0.13f + 0.06f * Mathf.Sin(i * 1.7f)), lavaPoolM, 3f, false, fis, Quaternion.Euler(0f, -Mathf.Atan2(d.z, d.x) * Mathf.Rad2Deg, 0f));
                        f.AddComponent<HeatSignature>().heat = 0.8f;
                    }
                    prev = p;
                }
                PointLight("Fissure glow", new Vector3((l[0] + l[2]) * 0.5f, 0.5f, (l[1] + l[3]) * 0.5f), new Color(1f, 0.38f, 0.08f), 1.2f, 5f, fis);
            }
            Log("columns " + n);
        }

        static Mesh FallMesh(float mouthY, float zOut0, float zOut1, out float length)
        {
            const int seg = 24;
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var tr = new List<int>();
            length = 0f;
            Vector3 prev = Vector3.zero;
            for (int i = 0; i <= seg; i++)
            {
                float t = i / (float)seg;
                float z = zOut0 + (zOut1 - zOut0) * Mathf.Pow(Mathf.Sin(t * Mathf.PI * 0.5f), 0.8f);
                float y = (mouthY - 0.05f) * (1f - Mathf.Pow(t, 1.8f)) + 0.06f * t;
                float w = 1.05f + 0.45f * t;
                Vector3 c = new Vector3(0f, y, z);
                if (i > 0) length += Vector3.Distance(prev, c);
                prev = c;
                v.Add(c + Vector3.left * w * 0.5f); v.Add(c + Vector3.right * w * 0.5f);
                uv.Add(new Vector2(0f, length / 1.6f)); uv.Add(new Vector2(1f, length / 1.6f));
            }
            for (int i = 0; i < seg; i++)
            {
                int a0 = i * 2, a1 = a0 + 1, b0 = a0 + 2, b1 = a0 + 3;
                tr.Add(a0); tr.Add(b0); tr.Add(a1); tr.Add(a1); tr.Add(b0); tr.Add(b1);
            }
            var m = new Mesh { name = "Lavafall" };
            m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(tr, 0);
            m.RecalculateNormals(); m.RecalculateTangents(); m.RecalculateBounds();
            return SaveMesh(m);
        }

        static void BuildLava()
        {
            Transform lava = new GameObject("Lava").transform;
            lava.SetParent(root, false);
            const float cw = 1.7f;
            foreach (int side in new[] { 1, -1 })
            {
                float cz = side * (HZ - cw * 0.5f - 0.15f);
                GameObject ch = Box("Lava channel", new Vector3(0f, 0.035f, cz), new Vector3(HX * 2f - 0.4f, 0.05f, cw), lavaPoolM, 3f, false, lava);
                LavaFlow lf = ch.AddComponent<LavaFlow>();
                lf.scrollSpeed = new Vector2(0.03f * side, 0.01f);
                ch.AddComponent<HeatSignature>().heat = 1f;
                Box("Channel bed edge", new Vector3(0f, 0.02f, side * (HZ - 0.07f)), new Vector3(HX * 2f, 0.06f, 0.14f), darkM, 1f, false, lava);
                float curbZ = cz - side * (cw * 0.5f + 0.18f);
                Box("Channel curb", new Vector3(0f, 0.17f, curbZ), new Vector3(HX * 2f - 0.4f, 0.34f, 0.36f), basaltM, 1f, true, lava);
                float bz = side * (HZ - 1.0f);
                if (side > 0)
                {
                    float gapL = -7.1f, gapR = -4.9f;
                    float l1 = gapL - (-HX), l2 = HX - gapR;
                    Box("Lava blocker", new Vector3(-HX + l1 * 0.5f, 1.2f, bz), new Vector3(l1, 2.4f, 2.2f), basaltM, 1f, true, lava).GetComponent<MeshRenderer>().enabled = false;
                    Box("Lava blocker", new Vector3(HX - l2 * 0.5f, 1.2f, bz), new Vector3(l2, 2.4f, 2.2f), basaltM, 1f, true, lava).GetComponent<MeshRenderer>().enabled = false;
                }
                else
                {
                    Box("Lava blocker", new Vector3(0f, 1.2f, bz), new Vector3(HX * 2f, 2.4f, 2.2f), basaltM, 1f, true, lava).GetComponent<MeshRenderer>().enabled = false;
                }
                foreach (float gx in new[] { -15f, 0f, 15f })
                    PointLight("Channel glow", new Vector3(gx, 1.2f, cz - side * 1.2f), new Color(1f, 0.34f, 0.08f), 3.2f, 10f, lava);

                foreach (float fx in new[] { -13f, 13f })
                {
                    float wallZ = side * HZ;
                    float mouthY = 3.5f;
                    Transform f = new GameObject("Lavafall").transform;
                    f.SetParent(lava, false);
                    f.localPosition = new Vector3(fx, 0f, 0f);
                    Box("Spout block", new Vector3(0f, mouthY + 0.25f, wallZ - side * 0.35f), new Vector3(1.9f, 1.3f, 0.9f), basaltM, 1f, false, f);
                    Box("Spout brow", new Vector3(0f, mouthY + 0.75f, wallZ - side * 0.75f), new Vector3(2.2f, 0.3f, 0.35f), basaltM, 1f, false, f);
                    foreach (float dx in new[] { -0.75f, 0.75f })
                        Box("Spout horn", new Vector3(dx, mouthY + 1.05f, wallZ - side * 0.9f), new Vector3(0.18f, 0.55f, 0.18f), basaltM, 1f, false, f, Quaternion.Euler(side * 20f, 0f, -dx * 28f));
                    Box("Spout lip", new Vector3(0f, mouthY - 0.12f, wallZ - side * 0.9f), new Vector3(1.5f, 0.18f, 0.5f), basaltM, 1f, false, f);
                    GameObject mouth = Box("Spout mouth glow", new Vector3(0f, mouthY + 0.12f, wallZ - side * 0.82f), new Vector3(1.1f, 0.42f, 0.12f), lavaPoolM, 1f, false, f);
                    mouth.AddComponent<HeatSignature>().heat = 1f;
                    GameObject fall = new GameObject("Lava sheet");
                    fall.transform.SetParent(f, false);
                    fall.AddComponent<MeshFilter>().sharedMesh = FallMesh(mouthY, wallZ - side * 1.05f, wallZ - side * 1.8f, out float len);
                    fall.AddComponent<MeshRenderer>().sharedMaterial = lavaFallM;
                    fall.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    LavaFlow flow = fall.AddComponent<LavaFlow>();
                    flow.scrollSpeed = new Vector2(0f, -0.55f);
                    fall.AddComponent<HeatSignature>().heat = 1f;
                    GameObject splash = Prim(PrimitiveType.Sphere, "Lava splash", new Vector3(0f, 0.05f, wallZ - side * 1.85f), new Vector3(2.4f, 0.4f, 1.5f), lavaPoolM, false, f);
                    LavaFlow sf = splash.AddComponent<LavaFlow>();
                    sf.scrollSpeed = new Vector2(0.08f, 0.05f);
                    sf.embers = true;
                    sf.embersPerSecond = 9f;
                    splash.AddComponent<HeatSignature>().heat = 1f;
                    PointLight("Lavafall glow", new Vector3(0f, 1.8f, wallZ - side * 2.8f), new Color(1f, 0.36f, 0.08f), 4.5f, 13f, f);
                }
            }
            Transform bridge = new GameObject("Bridge").transform;
            bridge.SetParent(root, false);
            Box("Bridge deck", new Vector3(-6f, 0.27f, HZ - 1.05f), new Vector3(2.0f, 0.18f, 2.5f), basaltM, 1f, true, bridge);
            Box("Bridge rail", new Vector3(-6.95f, 0.55f, HZ - 1.05f), new Vector3(0.12f, 0.4f, 2.5f), basaltM, 1f, true, bridge);
            Box("Bridge rail", new Vector3(-5.05f, 0.55f, HZ - 1.05f), new Vector3(0.12f, 0.4f, 2.5f), basaltM, 1f, true, bridge);
            Log("lava built");
        }

        static void BuildAlcove(Scene s)
        {
            Transform a = new GameObject("Skeleton alcove").transform;
            a.SetParent(root, false);
            float depth = 2.2f;
            Box("Alcove floor", new Vector3(-6f, 0.18f, HZ + depth * 0.5f), new Vector3(3.2f, 0.36f, depth + WALL_T), floorM, 4f, true, a);
            Box("Alcove back", new Vector3(-6f, 1.8f, HZ + depth + WALL_T * 0.5f), new Vector3(3.4f, 3.6f, WALL_T), wallM, 2f, true, a);
            Box("Alcove side", new Vector3(-7.7f, 1.8f, HZ + depth * 0.5f + 0.5f), new Vector3(0.4f, 3.6f, depth + WALL_T), wallM, 2f, true, a);
            Box("Alcove side", new Vector3(-4.3f, 1.8f, HZ + depth * 0.5f + 0.5f), new Vector3(0.4f, 3.6f, depth + WALL_T), wallM, 2f, true, a);
            Box("Alcove roof", new Vector3(-6f, 3.4f, HZ + depth * 0.5f + 0.5f), new Vector3(3.6f, 0.4f, depth + WALL_T), ceilM, 2f, false, a);
            PointLight("Alcove candle", new Vector3(-7.1f, 0.9f, HZ + 1.4f), new Color(1f, 0.6f, 0.25f), 1.2f, 4f, a);
            Prim(PrimitiveType.Cylinder, "Candle", new Vector3(-7.1f, 0.5f, HZ + 1.4f), new Vector3(0.08f, 0.14f, 0.08f), lavaPoolM, false, a);

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Interaction/Prefabs/SkeletonWithGoggles.prefab");
            foreach (GameObject r in s.GetRootGameObjects())
                if (r.name == "SkeletonWithGoggles") UnityEngine.Object.DestroyImmediate(r);
            if (prefab != null)
            {
                GameObject sk = (GameObject)PrefabUtility.InstantiatePrefab(prefab, s);
                sk.name = "SkeletonWithGoggles";
                sk.transform.SetParent(a, false);
                sk.transform.localPosition = new Vector3(-6f, 0.36f, HZ + 1.75f);
                sk.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                Log("skeleton placed at " + sk.transform.position.ToString("F2"));
            }
            else Log("SkeletonWithGoggles prefab missing");
            PlayerInteraction pi = UnityEngine.Object.FindObjectsByType<PlayerInteraction>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(p => p.gameObject.scene == s);
            if (pi != null && pi.gogglesViewPrefab == null)
            {
                pi.gogglesViewPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Interaction/Prefabs/Goggles_FP.prefab");
                EditorUtility.SetDirty(pi);
            }
            Log("player goggles prefab set: " + (pi != null && pi.gogglesViewPrefab != null));
        }

        static Mesh CrystalMesh(float r, float top, float bottom)
        {
            var v = new List<Vector3>(); var tr = new List<int>();
            for (int i = 0; i < 4; i++)
            {
                float a0 = i * Mathf.PI * 0.5f, a1 = (i + 1) * Mathf.PI * 0.5f;
                Vector3 p0 = new Vector3(Mathf.Cos(a0) * r, 0f, Mathf.Sin(a0) * r), p1 = new Vector3(Mathf.Cos(a1) * r, 0f, Mathf.Sin(a1) * r);
                int b = v.Count;
                v.Add(p0); v.Add(Vector3.up * top); v.Add(p1);
                tr.Add(b); tr.Add(b + 1); tr.Add(b + 2);
                b = v.Count;
                v.Add(p1); v.Add(Vector3.down * bottom); v.Add(p0);
                tr.Add(b); tr.Add(b + 1); tr.Add(b + 2);
            }
            var m = new Mesh { name = "Crystal" };
            m.SetVertices(v); m.SetTriangles(tr, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            Vector3[] ns = m.normals;
            if (Vector3.Dot(ns[0], (v[0] + v[1] + v[2]) / 3f) < 0f)
            {
                for (int i = 0; i < tr.Count; i += 3) { int t = tr[i + 1]; tr[i + 1] = tr[i + 2]; tr[i + 2] = t; }
                m.SetTriangles(tr, 0); m.RecalculateNormals();
            }
            return SaveMesh(m);
        }

        static void BuildCrystalAndTable(Scene s)
        {
            Transform t = new GameObject("Crystal pendant").transform;
            t.SetParent(root, false);
            float pz = DOME_H - 0.1f;
            Prim(PrimitiveType.Cylinder, "Ceiling medallion", new Vector3(0f, pz, 0f), new Vector3(3f, 0.08f, 3f), darkM, false, t);
            Box("Mount block", new Vector3(0f, pz - 0.35f, 0f), new Vector3(0.7f, 0.4f, 0.45f), basaltM, 1f, false, t);
            Box("Mount neck", new Vector3(0f, pz - 0.7f, 0f), new Vector3(0.38f, 0.32f, 0.3f), basaltM, 1f, false, t);
            float cy = pz - 1.4f;
            GameObject crystal = new GameObject("Red crystal");
            crystal.transform.SetParent(t, false);
            crystal.transform.localPosition = new Vector3(0f, cy, 0f);
            crystal.AddComponent<MeshFilter>().sharedMesh = CrystalMesh(0.25f, 0.5f, 0.6f);
            crystal.AddComponent<MeshRenderer>().sharedMaterial = crystalM;
            crystal.AddComponent<HeatSignature>().heat = 1f;
            PointLight("Crystal light", new Vector3(0f, cy, 0f), new Color(1f, 0.14f, 0.06f), 4.5f, 11f, t);
            GameObject sp = new GameObject("Crystal spot");
            sp.transform.SetParent(t, false);
            sp.transform.localPosition = new Vector3(0f, cy - 0.7f, 0f);
            sp.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Light spot = sp.AddComponent<Light>();
            spot.type = LightType.Spot;
            spot.color = new Color(1f, 0.16f, 0.07f);
            spot.intensity = 9f;
            spot.range = 10f;
            spot.spotAngle = 42f;
            spot.innerSpotAngle = 20f;
            spot.shadows = LightShadows.None;

            GameObject dais = Prim(PrimitiveType.Cylinder, "Table dais", new Vector3(0f, 0.06f, 0f), new Vector3(8.8f, 0.06f, 8.8f), darkM, false, root);
            dais.AddComponent<MeshCollider>().sharedMesh = dais.GetComponent<MeshFilter>().sharedMesh;
            GameObject inlay = new GameObject("Dais bronze inlay");
            inlay.transform.SetParent(root, false);
            inlay.AddComponent<MeshFilter>().sharedMesh = RingMesh(4.25f, 4.4f, 0.12f, 0.012f, 96);
            inlay.AddComponent<MeshRenderer>().sharedMaterial = bronzeM;

            foreach (GameObject r in s.GetRootGameObjects())
                if (r.name == "RoundTablePuzzle") UnityEngine.Object.DestroyImmediate(r);
            GameObject tablePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Interaction/Prefabs/RoundTablePuzzle.prefab");
            Vector3 prismTop = root.TransformPoint(new Vector3(0f, 1.5f, 0f));
            if (tablePrefab != null)
            {
                GameObject table = (GameObject)PrefabUtility.InstantiatePrefab(tablePrefab, s);
                table.name = "RoundTablePuzzle";
                table.transform.SetParent(root, false);
                table.transform.localPosition = new Vector3(0f, 0.12f, 0f);
                table.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);
                Transform prism = table.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name == "Crystal" && x.parent != null && x.parent.name == "Prism");
                if (prism != null) prismTop = prism.GetComponent<Renderer>() != null ? prism.GetComponent<Renderer>().bounds.center : prism.position;
                Log("table placed at " + table.transform.position.ToString("F2") + " prism " + prismTop.ToString("F2"));
            }
            else Log("RoundTablePuzzle prefab missing");

            GameObject beam = new GameObject("Crystal beam");
            beam.transform.SetParent(t, false);
            LineRenderer lr = beam.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.positionCount = 2;
            lr.SetPosition(0, root.TransformPoint(new Vector3(0f, cy - 0.6f, 0f)));
            lr.SetPosition(1, prismTop);
            lr.widthMultiplier = 0.09f;
            lr.numCapVertices = 4;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Material bm = AssetDatabase.LoadAssetAtPath<Material>(Dir + "/Crypt_Beam.mat");
            if (bm == null) { bm = new Material(Shader.Find("Universal Render Pipeline/Unlit")); AssetDatabase.CreateAsset(bm, Dir + "/Crypt_Beam.mat"); }
            bm.SetColor("_BaseColor", new Color(1f, 0.12f, 0.05f));
            lr.sharedMaterial = bm;
            beam.AddComponent<HeatSignature>().heat = 1f;
        }

        static void BuildEastDoor(Scene s)
        {
            Transform d = new GameObject("East sealed door").transform;
            d.SetParent(root, false);
            d.localPosition = new Vector3(HX + WALL_T * 0.5f, 0f, 0f);
            Box("Door slab", new Vector3(0f, CEIL * 0.5f, 0f), new Vector3(0.7f, CEIL, OPEN * 2f), basaltM, 2f, true, d);
            Box("Door rune", new Vector3(-0.37f, 2.1f, 0f), new Vector3(0.04f, 0.1f, 3.2f), crystalM, 1f, false, d);
            Box("Door rune", new Vector3(-0.37f, 1.5f, 0f), new Vector3(0.04f, 0.1f, 2.0f), crystalM, 1f, false, d);
            LockedGate gate = d.gameObject.AddComponent<LockedGate>();
            gate.raiseHeight = CEIL + 0.3f;
            gate.liftSeconds = 6f;
            RoundTablePuzzle puzzle = root.GetComponentInChildren<RoundTablePuzzle>(true);
            PuzzleGateLink link = d.gameObject.AddComponent<PuzzleGateLink>();
            link.puzzle = puzzle;
            link.gate = gate;
            Log("east door linked to puzzle: " + (puzzle != null));
        }

        static void BuildTorches()
        {
            Transform tr = new GameObject("Wall torches").transform;
            tr.SetParent(root, false);
            int n = 0;
            Color fire = new Color(1f, 0.45f, 0.14f);
            foreach (float x in new[] { -18.6f, -6.2f, 6.2f, 18.6f })
                foreach (float z in new[] { -11.2f, 11.2f })
                {
                    if (Mathf.Abs(x - (-6.2f)) < 0.1f && z > 0f) continue;
                    float off = z < 0 ? 0.75f : -0.75f;
                    Vector3 local = new Vector3(x, 2.7f, z + off);
                    Box("Sconce", local + Vector3.down * 0.25f, new Vector3(0.16f, 0.3f, 0.16f), bronzeM, 1f, false, tr);
                    SunkenPrism.TorchFlame.Create(tr, root.TransformPoint(local), fire, 0.8f);
                    n++;
                }
            foreach (int side in new[] { -1, 1 })
                foreach (float z in new[] { -5.2f, 5.2f })
                {
                    Vector3 local = new Vector3(side * (HX - 0.3f), 2.9f, z);
                    Box("Sconce", local + Vector3.down * 0.25f, new Vector3(0.16f, 0.3f, 0.16f), bronzeM, 1f, false, tr);
                    SunkenPrism.TorchFlame.Create(tr, root.TransformPoint(local), fire, 0.8f);
                    n++;
                }
            Log("torches " + n);
        }

        static void Snapshots()
        {
            try
            {
                Vector3 C = Center;
                Snap(C + new Vector3(-HX - 3.5f, 1.7f, 0f), C + new Vector3(0f, 1.6f, 0f), "Backups/lava_entrance.png", 60f);
                Snap(C + new Vector3(4.8f, 3.6f, -5.2f), C + new Vector3(0f, 0.8f, 0f), "Backups/lava_table.png", 55f);
                Snap(C + new Vector3(-16.8f, 2.3f, 8.2f), C + new Vector3(-13f, 1.6f, 14f), "Backups/lava_fall.png", 55f);
                Snap(C + new Vector3(-4.4f, 1.6f, 9.6f), C + new Vector3(-6f, 0.7f, 16.5f), "Backups/lava_alcove.png", 55f);
                Snap(C + new Vector3(15f, 1.7f, -2f), C + new Vector3(HX, 1.6f, 0f), "Backups/lava_east.png", 60f);
                Snap(C + new Vector3(3.6f, 1.6f, -5.2f), C + new Vector3(0f, 4.5f, 0f), "Backups/lava_crystal.png", 70f);
                Log("snapshots written");
            }
            catch (Exception e) { Log("SNAP FAILED " + e); }
        }

        static void Snap(Vector3 camPos, Vector3 target, string file, float fov)
        {
            GameObject go = new GameObject("SnapCam");
            go.hideFlags = HideFlags.HideAndDontSave;
            Camera cam = go.AddComponent<Camera>();
            cam.transform.position = camPos;
            cam.transform.LookAt(target);
            cam.fieldOfView = fov;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 200f;
            RenderTexture rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            Texture2D tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            File.WriteAllBytes(file, tex.EncodeToPNG());
            cam.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(rt);
            UnityEngine.Object.DestroyImmediate(tex);
            UnityEngine.Object.DestroyImmediate(go);
        }
    }
}
