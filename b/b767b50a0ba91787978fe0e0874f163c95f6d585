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
    static class EclipseBeautifySetup
    {
        const string TriggerPath = "Assets/Interaction/Editor/.run_eclipsebeauty";
        const string ReportPath = "Backups/eclipsebeauty_report.txt";
        const string MapScene = "Assets/Scenes/FourfoldCitadel_WithOurStuff.unity";
        const string Dir = "Assets/Interaction/Generated/EclipseKeep";
        const string RootName = "Eclipse Keep Atmosphere";
        const string StoryRoot = "Eclipse Keep Dressing";
        const float X0 = 209.5f, X1 = 264.8f, Z0 = -27f, Z1 = 27f, ThroneX = 237.15f;
        static readonly Color Amber = new Color(1f, 0.6f, 0.26f);
        static readonly string[] SkinMaterials = { "Mayan carved limestone", "Citadel weathered ashlar" };

        static EclipseBeautifySetup()
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

        [MenuItem("Tools/Interaction/Eclipse Keep Atmosphere")]
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
                foreach (string f in new[] { "eclipse_wall_normal", "eclipse_floor_normal" })
                {
                    var ti = AssetImporter.GetAtPath(Dir + "/" + f + ".png") as TextureImporter;
                    if (ti != null && ti.textureType != TextureImporterType.NormalMap) { ti.textureType = TextureImporterType.NormalMap; ti.SaveAndReimport(); }
                }
                foreach (string f in new[] { "light_shaft", "soft_glow" })
                {
                    var ti = AssetImporter.GetAtPath(Dir + "/" + f + ".png") as TextureImporter;
                    if (ti != null) { ti.alphaIsTransparency = true; ti.wrapMode = TextureWrapMode.Clamp; ti.SaveAndReimport(); }
                }

                Scene active = SceneManager.GetActiveScene();
                ZoneMusic zmOpen = UnityEngine.Object.FindAnyObjectByType<ZoneMusic>();
                bool stale = active.path == MapScene && zmOpen != null && zmOpen.zones.Count == 0;
                if (!stale) EditorSceneManager.SaveOpenScenes();
                File.Copy(MapScene, "Backups/FourfoldCitadel_WithOurStuff_before_eclipsebeauty2.unity", true);
                Scene map = EditorSceneManager.OpenScene(MapScene, OpenSceneMode.Single);
                Physics.SyncTransforms();
                GameObject old = GameObject.Find(RootName);
                if (old != null) UnityEngine.Object.DestroyImmediate(old);

                float floorY = 2f;
                if (Physics.Raycast(new Vector3(ThroneX, 6f, -12f), Vector3.down, out RaycastHit fh, 10f, ~0, QueryTriggerInteraction.Ignore)) floorY = fh.point.y;
                Views(floorY, "before");

                var cache = new Dictionary<string, Material>();
                int skinned = 0;
                foreach (MeshRenderer r in UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude))
                {
                    Bounds b = r.bounds;
                    if (b.center.x < X0 || b.center.x > X1 || b.center.z < Z0 || b.center.z > Z1) continue;
                    if (r.transform.root.name == StoryRoot) continue;
                    Material sm = r.sharedMaterial;
                    if (sm == null) continue;
                    bool match = false;
                    foreach (string m in SkinMaterials) if (sm.name.StartsWith(m)) match = true;
                    if (!match) continue;
                    bool isFloor = b.size.y < 0.7f && b.size.x * b.size.z > 2f;
                    Vector3 s = b.size;
                    Vector2 tile = isFloor ? new Vector2(Mathf.Max(1f, Mathf.Round(Mathf.Max(s.x, s.z) / 5f * 2f) / 2f), Mathf.Max(1f, Mathf.Round(Mathf.Min(s.x, s.z) / 5f * 2f) / 2f)) : (b.min.y > floorY + 6f ? new Vector2(0.35f, 0.35f) : new Vector2(0.6f, 0.6f));
                    string key = (isFloor ? "Floor_" : "Wall_") + tile.x.ToString("0.0") + "x" + tile.y.ToString("0.0");
                    if (!cache.TryGetValue(key, out Material mat))
                    {
                        mat = Stone("EK_" + key, isFloor ? "eclipse_floor" : "eclipse_wall", tile, isFloor);
                        cache[key] = mat;
                    }
                    var mats = r.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                    r.sharedMaterials = mats;
                    skinned++;
                }
                log.AppendLine("re-skinned " + skinned + " renderers with eclipse stone (" + cache.Count + " materials)");

                Shader flameSh = Shader.Find("SunkenPrism/TorchFlame");
                Material amberFlame = flameSh != null ? Mat("EK_AmberFlame", flameSh, m => m.SetColor("_Color", new Color(1f, 0.48f, 0.12f, 1f))) : null;
                int torches = 0;
                foreach (SunkenPrism.TorchFlame tf in UnityEngine.Object.FindObjectsByType<SunkenPrism.TorchFlame>(FindObjectsInactive.Include))
                {
                    Vector3 p = tf.transform.position;
                    if (p.x < X0 || p.x > X1 || p.z < Z0 || p.z > Z1) continue;
                    if (amberFlame != null) foreach (MeshRenderer mr in tf.GetComponentsInChildren<MeshRenderer>(true)) mr.sharedMaterial = amberFlame;
                    foreach (Light l in tf.GetComponentsInChildren<Light>(true)) l.color = new Color(1f, 0.55f, 0.24f);
                    tf.boost = 0.85f;
                    EditorUtility.SetDirty(tf);
                    torches++;
                }
                log.AppendLine("torches turned amber: " + torches);

                var root = new GameObject(RootName);
                Material glowSh(string n, Texture2D t, Color c, float k, float pulse, float speed) =>
                    Mat(n, Shader.Find("CasaFX/UVGlow"), m => { m.SetTexture("_MainTex", t); m.SetColor("_Color", c); m.SetFloat("_Intensity", k); m.SetFloat("_Pulse", pulse); m.SetFloat("_Speed", speed); });
                Texture2D glowTex = AssetDatabase.LoadAssetAtPath<Texture2D>(Dir + "/soft_glow.png");
                Texture2D shaftTex = AssetDatabase.LoadAssetAtPath<Texture2D>(Dir + "/light_shaft.png");

                Material carpetRed = Lit("EK_Carpet", new Color(0.3f, 0.04f, 0.05f), 0.15f, Color.black);
                Material carpetGold = Lit("EK_CarpetGold", new Color(0.62f, 0.42f, 0.16f), 0.45f, new Color(0.12f, 0.07f, 0.02f));
                var carpet = new GameObject("Carpet runner");
                carpet.transform.SetParent(root.transform, false);
                float cz0 = -24.5f, cz1 = 4.6f, cLen = cz1 - cz0, cMid = (cz0 + cz1) * 0.5f;
                Slab(carpet.transform, "Runner", carpetRed, new Vector3(ThroneX, floorY + 0.012f, cMid), new Vector3(3f, 0.024f, cLen));
                foreach (float sx in new[] { -1.42f, 1.42f }) Slab(carpet.transform, "Gold border", carpetGold, new Vector3(ThroneX + sx, floorY + 0.016f, cMid), new Vector3(0.12f, 0.03f, cLen));
                for (float z = cz0 + 2f; z < cz1 - 1f; z += 4f)
                {
                    var d = Slab(carpet.transform, "Gold motif", carpetGold, new Vector3(ThroneX, floorY + 0.016f, z), new Vector3(0.5f, 0.03f, 0.5f));
                    d.transform.rotation = Quaternion.Euler(0f, 45f, 0f);
                }

                Material wax = Lit("EK_Wax", new Color(0.86f, 0.8f, 0.66f), 0.3f, new Color(0.08f, 0.05f, 0.02f));
                Material flameMat = glowSh("EK_CandleFlame", glowTex, new Color(1f, 0.62f, 0.28f), 2.2f, 0.25f, 11f);
                var candleRoot = new GameObject("Candles");
                candleRoot.transform.SetParent(root.transform, false);
                var rng = new System.Random(4);
                var spots = new List<Vector3>();
                for (int i = -3; i <= 3; i++) if (i != 0) spots.Add(new Vector3(ThroneX + i * 2.6f, 0f, 9.4f));
                spots.Add(new Vector3(ThroneX - 5.2f, 0f, 4.2f)); spots.Add(new Vector3(ThroneX + 5.2f, 0f, 4.2f));
                spots.Add(new Vector3(ThroneX - 2.4f, 0f, -6f)); spots.Add(new Vector3(ThroneX + 2.4f, 0f, -6f));
                spots.Add(new Vector3(ThroneX - 2.4f, 0f, -16f)); spots.Add(new Vector3(ThroneX + 2.4f, 0f, -16f));
                int candles = 0;
                foreach (Vector3 sp in spots)
                {
                    if (!Physics.Raycast(new Vector3(sp.x, floorY + 6f, sp.z), Vector3.down, out RaycastHit ch, 10f, ~0, QueryTriggerInteraction.Ignore)) continue;
                    var cl = new GameObject("Candle cluster");
                    cl.transform.SetParent(candleRoot.transform, false);
                    cl.transform.position = ch.point;
                    int n = 3 + rng.Next(3);
                    for (int k = 0; k < n; k++)
                    {
                        float h = 0.12f + (float)rng.NextDouble() * 0.28f;
                        Vector3 off = new Vector3((float)(rng.NextDouble() - 0.5) * 0.45f, 0f, (float)(rng.NextDouble() - 0.5) * 0.45f);
                        var c = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                        c.name = "Candle";
                        UnityEngine.Object.DestroyImmediate(c.GetComponent<Collider>());
                        c.transform.SetParent(cl.transform, false);
                        c.transform.localPosition = off + Vector3.up * h * 0.5f;
                        float w = 0.045f + (float)rng.NextDouble() * 0.025f;
                        c.transform.localScale = new Vector3(w, h * 0.5f, w);
                        c.GetComponent<MeshRenderer>().sharedMaterial = wax;
                        var fq = GameObject.CreatePrimitive(PrimitiveType.Quad);
                        fq.name = "Flame";
                        UnityEngine.Object.DestroyImmediate(fq.GetComponent<Collider>());
                        fq.transform.SetParent(cl.transform, false);
                        fq.transform.localPosition = off + Vector3.up * (h + 0.035f);
                        fq.transform.localScale = new Vector3(0.05f, 0.09f, 1f);
                        fq.AddComponent<FaceCamera>();
                        fq.GetComponent<MeshRenderer>().sharedMaterial = flameMat;
                        fq.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                        candles++;
                    }
                    var lg = new GameObject("Candle light");
                    lg.transform.SetParent(cl.transform, false);
                    lg.transform.localPosition = Vector3.up * 0.45f;
                    Light l2 = lg.AddComponent<Light>();
                    l2.type = LightType.Point; l2.color = new Color(1f, 0.6f, 0.3f); l2.intensity = 0.9f; l2.range = 3.2f; l2.shadows = LightShadows.None;
                }
                log.AppendLine("candles " + candles + " in " + spots.Count + " clusters");

                if (shaftTex != null)
                {
                    Material shaftMat = glowSh("EK_Shaft", shaftTex, new Color(1f, 0.72f, 0.42f), 0.28f, 0.2f, 0.5f);
                    var shafts = new GameObject("Eclipse light shafts");
                    shafts.transform.SetParent(root.transform, false);
                    float[] angles = { -26f, -13f, 0f, 13f, 26f };
                    for (int i = 0; i < angles.Length; i++)
                    {
                        var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                        q.name = "Shaft";
                        UnityEngine.Object.DestroyImmediate(q.GetComponent<Collider>());
                        q.transform.SetParent(shafts.transform, false);
                        Vector3 top = new Vector3(ThroneX, 11f, 18.2f);
                        Vector3 bottom = new Vector3(ThroneX + Mathf.Sin(angles[i] * Mathf.Deg2Rad) * 9f, floorY, 4f - Mathf.Abs(angles[i]) * 0.1f);
                        Vector3 mid = (top + bottom) * 0.5f;
                        Vector3 axis = (top - bottom).normalized;
                        q.transform.position = mid;
                        q.transform.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(Vector3.back, axis).normalized, axis);
                        q.transform.localScale = new Vector3(2.4f + (i % 2) * 0.8f, Vector3.Distance(top, bottom) * 1.05f, 1f);
                        q.GetComponent<MeshRenderer>().sharedMaterial = shaftMat;
                        q.AddComponent<AxisBillboard>();
                        q.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    }
                }

                Particles(root.transform, "Drifting embers", new Vector3(ThroneX, floorY + 4f, 0f), new Vector3(40f, 6f, 46f), glowTex, new Color(1f, 0.55f, 0.2f), new Color(1f, 0.8f, 0.5f), 0.025f, 0.07f, 10f, 0.05f, 0.25f, -0.01f, 220, 16f);
                Particles(root.transform, "Floor haze", new Vector3(ThroneX, floorY + 0.35f, -4f), new Vector3(42f, 0.4f, 44f), glowTex, new Color(0.45f, 0.32f, 0.28f, 0.08f), new Color(0.35f, 0.25f, 0.24f, 0.06f), 2.5f, 5f, 14f, 0.02f, 0.12f, 0f, 120, 5f);

                var rims = new GameObject("Warm rim lights");
                rims.transform.SetParent(root.transform, false);
                Vector3[] rp = { new Vector3(213f, 0f, -18f), new Vector3(213f, 0f, 14f), new Vector3(261f, 0f, -18f), new Vector3(261f, 0f, 14f), new Vector3(225f, 0f, 23f), new Vector3(249f, 0f, 23f), new Vector3(ThroneX, 0f, -24f) };
                foreach (Vector3 p in rp)
                {
                    var g = new GameObject("Rim light");
                    g.transform.SetParent(rims.transform, false);
                    g.transform.position = new Vector3(p.x, floorY + 2.2f, p.z);
                    Light l = g.AddComponent<Light>();
                    l.type = LightType.Point; l.color = Amber; l.intensity = 1.6f; l.range = 9f; l.shadows = LightShadows.None;
                }

                EditorSceneManager.MarkSceneDirty(map);
                EditorSceneManager.SaveScene(map);
                ZoneMusic zm = UnityEngine.Object.FindAnyObjectByType<ZoneMusic>();
                log.AppendLine("zone music areas: " + (zm != null ? zm.zones.Count.ToString() : "none"));
                Views(floorY, "after");
                log.AppendLine("DONE");
            }
            catch (Exception e)
            {
                log.AppendLine("FAILED: " + e);
            }
            File.WriteAllText(ReportPath, log.ToString());
        }

        static void Views(float floorY, string tag)
        {
            Snapshot(new Vector3(ThroneX, floorY + 1.7f, -22f), new Vector3(ThroneX, floorY + 4.5f, 18f), "Backups/eclipsebeauty_" + tag + "_0.png");
            Snapshot(new Vector3(216f, floorY + 1.8f, -2f), new Vector3(ThroneX, floorY + 4f, 14f), "Backups/eclipsebeauty_" + tag + "_1.png");
            Snapshot(new Vector3(ThroneX - 6f, floorY + 3.2f, 1f), new Vector3(ThroneX, floorY + 3.4f, 16f), "Backups/eclipsebeauty_" + tag + "_2.png");
        }

        static void Particles(Transform parent, string name, Vector3 pos, Vector3 box, Texture2D tex, Color a, Color b, float sMin, float sMax, float life, float vMin, float vMax, float gravity, int max, float rate)
        {
            var g = new GameObject(name);
            g.transform.SetParent(parent, false);
            g.transform.position = pos;
            var ps = g.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.6f, life);
            main.startSpeed = new ParticleSystem.MinMaxCurve(vMin, vMax);
            main.startSize = new ParticleSystem.MinMaxCurve(sMin, sMax);
            main.startColor = new ParticleSystem.MinMaxGradient(a, b);
            main.gravityModifier = gravity;
            main.maxParticles = max;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.prewarm = true;
            var em = ps.emission; em.rateOverTime = rate;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = box;
            var col = ps.colorOverLifetime; col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            col.color = grad;
            var noise = ps.noise; noise.enabled = true; noise.strength = 0.2f; noise.frequency = 0.2f;
            var r = g.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = Mat("EK_Particle_" + name.Replace(" ", ""), Shader.Find("Universal Render Pipeline/Particles/Unlit"), m =>
            {
                m.SetTexture("_BaseMap", tex);
                m.SetFloat("_Surface", 1f);
                m.SetFloat("_Blend", 2f);
                m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
                m.SetFloat("_ZWrite", 0f);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.EnableKeyword("_BLENDMODE_ADD");
                m.SetOverrideTag("RenderType", "Transparent");
                m.renderQueue = 3000;
            });
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ps.Play();
        }

        static GameObject Slab(Transform parent, string name, Material m, Vector3 pos, Vector3 size)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            g.name = name;
            UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());
            g.transform.SetParent(parent, false);
            g.transform.position = pos;
            g.transform.localScale = size;
            g.GetComponent<MeshRenderer>().sharedMaterial = m;
            return g;
        }

        static Material Stone(string name, string tex, Vector2 tile, bool floor)
        {
            return Mat(name, Shader.Find("Universal Render Pipeline/Lit"), m =>
            {
                m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Dir + "/" + tex + ".png"));
                m.SetTextureScale("_BaseMap", tile);
                m.SetColor("_BaseColor", Color.white);
                Texture2D n = AssetDatabase.LoadAssetAtPath<Texture2D>(Dir + "/" + tex + "_normal.png");
                if (n != null) { m.SetTexture("_BumpMap", n); m.SetTextureScale("_BumpMap", tile); m.EnableKeyword("_NORMALMAP"); m.SetFloat("_BumpScale", 0.8f); }
                Texture2D e = floor ? AssetDatabase.LoadAssetAtPath<Texture2D>(Dir + "/eclipse_floor_emission.png") : null;
                if (e != null) { m.EnableKeyword("_EMISSION"); m.SetTexture("_EmissionMap", e); m.SetTextureScale("_EmissionMap", tile); m.SetColor("_EmissionColor", Color.white * 0.9f); }
                else { m.DisableKeyword("_EMISSION"); m.SetColor("_EmissionColor", Color.black); }
                m.SetFloat("_Smoothness", floor ? 0.42f : 0.18f);
                m.SetFloat("_Metallic", 0f);
            });
        }

        static Material Lit(string name, Color c, float smooth, Color emission)
        {
            return Mat(name, Shader.Find("Universal Render Pipeline/Lit"), m =>
            {
                m.SetColor("_BaseColor", c);
                m.SetFloat("_Smoothness", smooth);
                if (emission.maxColorComponent > 0.001f) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", emission); }
            });
        }

        static Material Mat(string name, Shader sh, Action<Material> setup)
        {
            string path = Dir + "/" + name + ".mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, path); }
            else if (sh != null) m.shader = sh;
            setup(m);
            EditorUtility.SetDirty(m);
            return m;
        }

        static void Snapshot(Vector3 from, Vector3 at, string path)
        {
            var go = new GameObject("Snap Cam");
            Camera cam = go.AddComponent<Camera>();
            cam.fieldOfView = 68f;
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
