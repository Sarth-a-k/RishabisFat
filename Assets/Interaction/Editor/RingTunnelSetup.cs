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
    static class RingTunnelSetup
    {
        const string TriggerPath = "Assets/Interaction/Editor/.run_ringtunnel";
        const string ReportPath = "Backups/ringtunnel_report.txt";
        const string MapScene = "Assets/Scenes/FourfoldCitadel_WithOurStuff.unity";
        const string Dir = "Assets/Interaction/Generated/WhiteRegion";
        const string RootName = "Ring Tunnel";
        const float X0 = 151.6f, X1 = 196.2f, Amp = 3.6f;
        const float InnerW = 6.4f, InnerH = 5.6f, InnerR = 1.9f, Thick = 0.85f, Depth = 0.62f, Spacing = 1.22f;
        static readonly Vector3 RoomCenter = new Vector3(173.9f, 0f, 0f);

        static RingTunnelSetup()
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

        static float yIn, yOut;
        static float[] floorSamples;
        static float BaseY(float x) { return Mathf.Lerp(yIn, yOut, Mathf.Clamp01((x - X0) / (X1 - X0))); }
        static float Y(float x)
        {
            if (floorSamples == null) return BaseY(x);
            float f = Mathf.Clamp((x - (X0 - 1f)) / 0.5f, 0f, floorSamples.Length - 1.001f);
            int i = (int)f;
            return Mathf.Lerp(floorSamples[i], floorSamples[i + 1], f - i);
        }

        static float Z(float x) { float t = Mathf.Clamp01((x - X0) / (X1 - X0)); return Amp * 1.3f * Mathf.Sin(Mathf.PI * 2f * t) * Mathf.Sin(Mathf.PI * t); }
        static Vector3 Tangent(float x) { return new Vector3(0.2f, 0f, Z(x + 0.1f) - Z(x - 0.1f)).normalized; }

        [MenuItem("Tools/Interaction/Region 3 Ring Tunnel")]
        static void Run()
        {
            var log = new StringBuilder();
            try
            {
                AssetDatabase.Refresh();
                Directory.CreateDirectory(Dir);
                Material soft = Mat("Tunnel_Soft", "CasaFX/SoftWhite", m => { m.SetVector("_Dir", new Vector4(-0.6f, 0.55f, 0.3f, 0f)); m.SetFloat("_FarBright", 0.35f); m.SetFloat("_FarDist", 42f); });
                Material voidMat = Mat("Tunnel_Void", "Universal Render Pipeline/Unlit", m => m.SetColor("_BaseColor", new Color(0.10f, 0.11f, 0.15f)));
                if (soft.shader == null || soft.shader.name != "CasaFX/SoftWhite") throw new Exception("SoftWhite shader not found");
                Material blackAura = AssetDatabase.LoadAssetAtPath<Material>(Dir + "/Black_Aura.mat");
                Material blackFlame = AssetDatabase.LoadAssetAtPath<Material>(Dir + "/Black_Flame.mat");
                Material blackBody = AssetDatabase.LoadAssetAtPath<Material>(Dir + "/Black_Unlit.mat");

                Scene active = SceneManager.GetActiveScene();
                ZoneMusic zmOpen = UnityEngine.Object.FindAnyObjectByType<ZoneMusic>();
                bool stale = active.path == MapScene && zmOpen != null && zmOpen.zones.Count == 0;
                if (!stale) EditorSceneManager.SaveOpenScenes();
                Directory.CreateDirectory("Backups");
                File.Copy(MapScene, "Backups/FourfoldCitadel_WithOurStuff_before_ringtunnel_run3.unity", true);
                Scene map = EditorSceneManager.OpenScene(MapScene, OpenSceneMode.Single);
                Physics.SyncTransforms();

                GameObject old = GameObject.Find(RootName);
                if (old != null) UnityEngine.Object.DestroyImmediate(old);

                Transform region = null;
                foreach (Transform t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
                    if (t.name.StartsWith("03 ") && t.name.Contains("Violet")) { region = t; break; }
                if (region == null) throw new Exception("region 3 root not found");

                Physics.SyncTransforms();
                floorSamples = null;
                yIn = 0f; yOut = 0f;
                if (Physics.Raycast(new Vector3(X0 - 1.5f, 3f, 0f), Vector3.down, out RaycastHit h0, 12f, ~0, QueryTriggerInteraction.Ignore)) yIn = h0.point.y;
                if (Physics.Raycast(new Vector3(X1 + 1.5f, 3f, 0f), Vector3.down, out RaycastHit h1, 12f, ~0, QueryTriggerInteraction.Ignore)) yOut = h1.point.y;
                float floorY = Mathf.Min(yIn, yOut);
                log.AppendLine("floor at entrance " + yIn + ", at exit " + yOut);

                var flames = region.GetComponentsInChildren<SunkenPrism.TorchFlame>(true);
                GameObject torchTemplate = null;
                foreach (var f in flames)
                {
                    Transform tr = f.transform.parent;
                    if (tr != null && tr != region && tr.GetComponentsInChildren<Renderer>(true).Length <= 24) { torchTemplate = tr.gameObject; break; }
                }
                if (torchTemplate == null && flames.Length > 0) torchTemplate = flames[0].gameObject;

                int voided = 0, collOff = 0;
                var hideTorches = new HashSet<GameObject>();
                foreach (var f in flames)
                {
                    Transform tr = f.transform.parent;
                    hideTorches.Add(tr != null && tr != region && tr.GetComponentsInChildren<Renderer>(true).Length <= 24 ? tr.gameObject : f.gameObject);
                }
                foreach (Renderer r in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude))
                {
                    Vector3 d = r.bounds.center - RoomCenter;
                    bool inRegion = r.transform.IsChildOf(region);
                    bool inBox = Mathf.Abs(d.x) < 21.8f && Mathf.Abs(d.z) < 21.8f;
                    if (!inRegion && !inBox) continue;
                    if (r is ParticleSystemRenderer || r is LineRenderer || r is SpriteRenderer) { if (inBox) { r.enabled = false; voided++; } continue; }
                    var mats = r.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++) mats[i] = voidMat;
                    r.sharedMaterials = mats;
                    voided++;
                }
                foreach (GameObject g in hideTorches) g.SetActive(false);

                var corridor = new List<Bounds>();
                for (float x = X0; x <= X1; x += 1f) corridor.Add(new Bounds(new Vector3(x, Y(x) + 3f, Z(x)), new Vector3(2.5f, 6f, InnerW + 1.5f)));
                foreach (Collider c in UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsInactive.Exclude))
                {
                    if (c.isTrigger || !c.enabled) continue;
                    if (c.GetComponentInParent<CharacterController>() != null) continue;
                    Bounds b = c.bounds;
                    if (b.max.y < Y(b.center.x) + 0.35f) continue;
                    foreach (Bounds cb in corridor) if (cb.Intersects(b)) { c.enabled = false; collOff++; break; }
                }
                Physics.SyncTransforms();
                int hidden = 0;
                foreach (Renderer r in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude))
                {
                    Bounds b = r.bounds;
                    if (b.max.y < BaseY(b.center.x) + 0.25f) continue;
                    foreach (Bounds cb in corridor) if (cb.Intersects(b) && b.size.y < 12f) { r.enabled = false; hidden++; break; }
                }
                int n = Mathf.CeilToInt((X1 - X0 + 2f) / 0.5f) + 2;
                var samples0 = new float[n];
                for (int i = 0; i < n; i++)
                {
                    float x = X0 - 1f + i * 0.5f;
                    float by = BaseY(x);
                    samples0[i] = by;
                    if (Physics.Raycast(new Vector3(x, by + 1.2f, Z(x)), Vector3.down, out RaycastHit fh2, 3f, ~0, QueryTriggerInteraction.Ignore)) samples0[i] = fh2.point.y;
                }
                floorSamples = new float[n];
                for (int i = 0; i < n; i++)
                {
                    float m = samples0[i];
                    for (int k = -2; k <= 2; k++) m = Mathf.Max(m, samples0[Mathf.Clamp(i + k, 0, n - 1)]);
                    floorSamples[i] = m + 0.04f;
                }
                log.AppendLine("intruding renderers hidden " + hidden + ", floor samples " + n);
                log.AppendLine("voided renderers " + voided + ", blocking colliders disabled " + collOff + ", torches hidden " + hideTorches.Count);

                var root = new GameObject(RootName);
                Mesh ring = SaveMesh(RingMesh(), "tunnel_ring");
                float pathLen = 0f;
                var samples = new List<Vector3>();
                for (float x = X0; x <= X1 + 0.001f; x += 0.05f) samples.Add(new Vector3(x, Y(x), Z(x)));
                for (int i = 1; i < samples.Count; i++) pathLen += Vector3.Distance(samples[i - 1], samples[i]);
                int count = Mathf.FloorToInt(pathLen / Spacing);
                int ringsMade = 0;
                float acc = 0f, next = 0.3f;
                for (int i = 1; i < samples.Count && ringsMade <= count; i++)
                {
                    acc += Vector3.Distance(samples[i - 1], samples[i]);
                    if (acc < next) continue;
                    next += Spacing;
                    Vector3 p = samples[i];
                    var g = new GameObject("Ring " + ringsMade);
                    g.transform.SetParent(root.transform, false);
                    g.transform.position = p;
                    g.transform.rotation = Quaternion.LookRotation(Tangent(p.x), Vector3.up);
                    g.AddComponent<MeshFilter>().sharedMesh = ring;
                    var mr = g.AddComponent<MeshRenderer>();
                    mr.sharedMaterial = soft;
                    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    var mc = g.AddComponent<MeshCollider>();
                    mc.sharedMesh = ring;
                    GameObjectUtility.SetStaticEditorFlags(g, StaticEditorFlags.BatchingStatic);
                    ringsMade++;
                }

                var floor = new GameObject("Tunnel floor");
                floor.transform.SetParent(root.transform, false);
                Mesh fm = SaveMesh(FloorMesh(), "tunnel_floor");
                floor.AddComponent<MeshFilter>().sharedMesh = fm;
                var fmr = floor.AddComponent<MeshRenderer>();
                fmr.sharedMaterial = soft;
                fmr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                floor.AddComponent<MeshCollider>().sharedMesh = fm;

                var walls = new GameObject("Tunnel side walls");
                walls.transform.SetParent(root.transform, false);
                for (float x = X0; x < X1; x += 1.5f)
                {
                    Vector3 c = new Vector3(x + 0.75f, Y(x + 0.75f) + 3f, Z(x + 0.75f));
                    Vector3 tg = Tangent(c.x);
                    Vector3 side = Vector3.Cross(Vector3.up, tg).normalized;
                    foreach (float s in new[] { -1f, 1f })
                    {
                        var w = new GameObject("Wall");
                        w.transform.SetParent(walls.transform, false);
                        w.transform.position = c + side * s * (InnerW * 0.5f + 0.25f);
                        w.transform.rotation = Quaternion.LookRotation(tg, Vector3.up);
                        var bc = w.AddComponent<BoxCollider>();
                        bc.size = new Vector3(0.5f, 6f, 1.7f);
                    }
                }

                int torches = 0;
                if (torchTemplate != null)
                {
                    float[] at = { 0.14f, 0.3f, 0.46f, 0.62f, 0.78f, 0.92f };
                    for (int i = 0; i < at.Length; i++)
                    {
                        float x = Mathf.Lerp(X0, X1, at[i]);
                        Vector3 c = new Vector3(x, Y(x), Z(x));
                        Vector3 tg = Tangent(x);
                        Vector3 side = Vector3.Cross(Vector3.up, tg).normalized * (i % 2 == 0 ? 1f : -1f);
                        GameObject t = UnityEngine.Object.Instantiate(torchTemplate, root.transform);
                        t.name = "Black torch " + i;
                        t.SetActive(true);
                        t.transform.position = c + side * (InnerW * 0.5f - 0.35f) + Vector3.up * 2.3f;
                        t.transform.rotation = Quaternion.LookRotation(-side, Vector3.up);
                        var flameRs = new HashSet<Renderer>();
                        foreach (var tf in t.GetComponentsInChildren<SunkenPrism.TorchFlame>(true))
                        {
                            tf.boost = 0f;
                            foreach (Light l in tf.GetComponentsInChildren<Light>(true)) { l.intensity = 0f; l.enabled = false; }
                            foreach (Renderer fr in tf.GetComponentsInChildren<Renderer>(true)) flameRs.Add(fr);
                        }
                        foreach (Renderer tr2 in t.GetComponentsInChildren<Renderer>(true))
                        {
                            Material use = tr2.name == "Black aura" ? blackAura : flameRs.Contains(tr2) ? blackFlame : blackBody;
                            if (use == null) continue;
                            var ms = tr2.sharedMaterials;
                            for (int k = 0; k < ms.Length; k++) ms[k] = use;
                            tr2.sharedMaterials = ms;
                        }
                        torches++;
                    }
                }
                log.AppendLine("rings " + ringsMade + " over " + pathLen.ToString("0.0") + " m, black torches " + torches + (torchTemplate != null ? " from " + torchTemplate.name : ""));

                EditorSceneManager.MarkSceneDirty(map);
                EditorSceneManager.SaveScene(map);
                ZoneMusic zm = UnityEngine.Object.FindAnyObjectByType<ZoneMusic>();
                log.AppendLine("zone music areas: " + (zm != null ? zm.zones.Count.ToString() : "none"));

                Vector3 e = new Vector3(X0 - 2.5f, Y(X0) + 1.7f, 0f);
                Snapshot(e, e + Tangent(e.x) * 10f + Vector3.up * 0.1f, "Backups/ringtunnel_0.png");
                Vector3 m2 = new Vector3(165f, Y(165f) + 1.7f, Z(165f));
                Snapshot(m2, m2 + Tangent(165f) * 10f, "Backups/ringtunnel_1.png");
                Vector3 m3 = new Vector3(182f, Y(182f) + 1.7f, Z(182f));
                Snapshot(m3, m3 + Tangent(182f) * 10f + Vector3.up * 0.4f, "Backups/ringtunnel_2.png");
                log.AppendLine("DONE");
            }
            catch (Exception ex)
            {
                log.AppendLine("FAILED: " + ex);
            }
            File.WriteAllText(ReportPath, log.ToString());
        }

        static List<Vector2> Loop(float w, float h, float r, float y0, int seg)
        {
            var pts = new List<Vector2>();
            Vector2[] centers = { new Vector2(w * 0.5f - r, y0 + h - r), new Vector2(-w * 0.5f + r, y0 + h - r), new Vector2(-w * 0.5f + r, y0 + r), new Vector2(w * 0.5f - r, y0 + r) };
            float[] start = { 0f, 90f, 180f, 270f };
            for (int c = 0; c < 4; c++)
                for (int i = 0; i <= seg; i++)
                {
                    float a = (start[c] + 90f * i / seg) * Mathf.Deg2Rad;
                    pts.Add(centers[c] + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r);
                }
            return pts;
        }

        static Mesh RingMesh()
        {
            int seg = 10;
            var inner = Loop(InnerW, InnerH, InnerR, 0f, seg);
            var outer = Loop(InnerW + Thick * 2f, InnerH + Thick * 2f, InnerR + Thick, -Thick, seg);
            int n = inner.Count;
            float zf = -Depth * 0.5f, zb = Depth * 0.5f;
            var v = new List<Vector3>(); var nr = new List<Vector3>(); var t = new List<int>();
            void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 na, Vector3 nb, Vector3 nc, Vector3 nd)
            {
                int i = v.Count;
                v.Add(a); v.Add(b); v.Add(c); v.Add(d);
                nr.Add(na); nr.Add(nb); nr.Add(nc); nr.Add(nd);
                t.Add(i); t.Add(i + 1); t.Add(i + 2); t.Add(i); t.Add(i + 2); t.Add(i + 3);
            }
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                Vector3 oi = outer[i], oj = outer[j], ii = inner[i], ij = inner[j];
                Vector3 ni = ((Vector3)(outer[i] - inner[i])).normalized, nj = ((Vector3)(outer[j] - inner[j])).normalized;
                Quad(new Vector3(oi.x, oi.y, zf), new Vector3(oj.x, oj.y, zf), new Vector3(ij.x, ij.y, zf), new Vector3(ii.x, ii.y, zf), Vector3.back, Vector3.back, Vector3.back, Vector3.back);
                Quad(new Vector3(oi.x, oi.y, zb), new Vector3(ii.x, ii.y, zb), new Vector3(ij.x, ij.y, zb), new Vector3(oj.x, oj.y, zb), Vector3.forward, Vector3.forward, Vector3.forward, Vector3.forward);
                Quad(new Vector3(oi.x, oi.y, zf), new Vector3(oi.x, oi.y, zb), new Vector3(oj.x, oj.y, zb), new Vector3(oj.x, oj.y, zf), ni, ni, nj, nj);
                Quad(new Vector3(ii.x, ii.y, zf), new Vector3(ij.x, ij.y, zf), new Vector3(ij.x, ij.y, zb), new Vector3(ii.x, ii.y, zb), -ni, -nj, -nj, -ni);
            }
            var m = new Mesh { name = "Tunnel ring" };
            m.SetVertices(v); m.SetNormals(nr); m.SetTriangles(t, 0);
            m.RecalculateBounds();
            return m;
        }

        static Mesh FloorMesh()
        {
            var v = new List<Vector3>(); var nr = new List<Vector3>(); var t = new List<int>();
            float hw = InnerW * 0.5f + 0.4f;
            for (float x = X0 - 0.6f; x <= X1 + 0.6f; x += 0.5f)
            {
                Vector3 c = new Vector3(x, Y(x), Z(x));
                Vector3 side = Vector3.Cross(Vector3.up, Tangent(x)).normalized;
                v.Add(c - side * hw); v.Add(c + side * hw);
                nr.Add(Vector3.up); nr.Add(Vector3.up);
                int i = v.Count - 2;
                if (i >= 2) { t.Add(i - 2); t.Add(i); t.Add(i - 1); t.Add(i - 1); t.Add(i); t.Add(i + 1); }
            }
            var m = new Mesh { name = "Tunnel floor" };
            m.SetVertices(v); m.SetNormals(nr); m.SetTriangles(t, 0);
            m.RecalculateBounds();
            return m;
        }

        static Mesh SaveMesh(Mesh m, string name)
        {
            Directory.CreateDirectory(Dir + "/Meshes");
            string path = Dir + "/Meshes/" + name + ".asset";
            if (AssetDatabase.LoadAssetAtPath<Mesh>(path) != null) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        static Material Mat(string name, string shader, Action<Material> setup)
        {
            string path = Dir + "/" + name + ".mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader sh = Shader.Find(shader);
            if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, path); }
            else if (sh != null) m.shader = sh;
            setup(m);
            EditorUtility.SetDirty(m);
            AssetDatabase.SaveAssets();
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
