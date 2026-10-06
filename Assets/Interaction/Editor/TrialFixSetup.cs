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
    static class TrialFixSetup
    {
        const string TriggerPath = "Assets/Interaction/Editor/.run_trialfix";
        const string SnapTrigger = "Assets/Interaction/Editor/.run_snap";
        const string WorkScene = "Assets/Scenes/FourfoldCitadel_WithOurStuff.unity";
        const string GenDir = "Assets/Interaction/Generated/Sigils";
        const string LockName = "Sigil Lock";
        static readonly string[] RemoveTexts = { "THE CONCORDANCE PRISM", "CRESCENT" };

        static StringBuilder report;

        static TrialFixSetup()
        {
            EditorApplication.delayCall += Check;
        }

        static void Check()
        {
            bool fix = File.Exists(TriggerPath) && File.ReadAllText(TriggerPath).Trim() == "pending";
            bool snap = File.Exists(SnapTrigger) && File.ReadAllText(SnapTrigger).Trim() == "pending";
            if (!fix && !snap) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.delayCall += Check;
                return;
            }
            report = new StringBuilder();
            if (fix) { File.WriteAllText(TriggerPath, "done"); Run(); }
            if (snap) { File.WriteAllText(SnapTrigger, "done"); Snapshots(); }
            File.WriteAllText("Backups/trialfix_report.txt", report.ToString());
        }

        static void Log(string s) { report.AppendLine(s); }

        static Transform FindStarts(Scene s, string prefix)
        {
            foreach (GameObject root in s.GetRootGameObjects())
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                    if (t.name.StartsWith(prefix, StringComparison.Ordinal)) return t;
            return null;
        }

        [MenuItem("Tools/Interaction/Rebuild Sigil Lock And Clean Labels")]
        static void MenuRun()
        {
            report = new StringBuilder();
            Run();
            Snapshots();
            File.WriteAllText("Backups/trialfix_report.txt", report.ToString());
        }

        static void Run()
        {
            try
            {
                EditorSceneManager.SaveOpenScenes();
                Directory.CreateDirectory("Backups");
                File.Copy(WorkScene, "Backups/FourfoldCitadel_WithOurStuff_before_trialfix.unity", true);
                Scene s = EditorSceneManager.OpenScene(WorkScene, OpenSceneMode.Single);
                Directory.CreateDirectory(GenDir);

                int removed = 0;
                var doomed = new List<GameObject>();
                foreach (GameObject root in s.GetRootGameObjects())
                    foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                    {
                        if (t.name == "Label" && t.parent != null && t.parent.GetComponent<MirrorSocket>() != null) doomed.Add(t.gameObject);
                        else if (t.GetComponent<TextMesh>() != null && RemoveTexts.Any(p => t.name.StartsWith(p, StringComparison.Ordinal))) doomed.Add(t.gameObject);
                    }
                foreach (GameObject g in doomed) { Log("removed " + g.name); UnityEngine.Object.DestroyImmediate(g); removed++; }

                Transform prism = FindStarts(s, "White triangular prism");
                PrismMoonController pmc = prism != null ? prism.GetComponent<PrismMoonController>() : null;
                if (pmc != null) { pmc.rotateTarget = prism; EditorUtility.SetDirty(pmc); }

                Texture2D[] sigilTex = new Texture2D[3];
                string[] seals = { "Lunar seal 0", "Lunar seal 2", "Lunar seal 1" };
                string[] names = { "Sigil_ThreeBars", "Sigil_Spiral", "Sigil_Triangle" };
                int[] kindOfSeal = { 0, 2, 1 };
                for (int i = 0; i < 3; i++)
                {
                    Transform seal = FindStarts(s, seals[i]);
                    int kind = kindOfSeal[i];
                    sigilTex[kind] = BakeSigil(seal, names[i]);
                }
                SetupLock(s, prism, sigilTex);

                EditorSceneManager.MarkSceneDirty(s);
                EditorSceneManager.SaveScene(s);
                AssetDatabase.SaveAssets();
                Log("DONE removed " + removed);
            }
            catch (Exception e)
            {
                Log("FAILED " + e);
            }
        }

        static Texture2D BakeSigil(Transform seal, string name)
        {
            const int N = 512;
            var segs = new List<Vector4>();
            float widthPx = 22f;
            if (seal != null)
            {
                var lines = seal.GetComponentsInChildren<LineRenderer>(true);
                var all = new List<Vector2>();
                var polys = new List<List<Vector2>>();
                float worldWidth = 0f;
                foreach (LineRenderer lr in lines)
                {
                    var pts = new Vector3[lr.positionCount];
                    lr.GetPositions(pts);
                    var poly = new List<Vector2>();
                    foreach (Vector3 p in pts)
                    {
                        Vector3 w = lr.useWorldSpace ? p : lr.transform.TransformPoint(p);
                        Vector3 l = seal.InverseTransformPoint(w);
                        poly.Add(new Vector2(l.x, l.y));
                    }
                    if (lr.loop && poly.Count > 2) poly.Add(poly[0]);
                    polys.Add(poly);
                    all.AddRange(poly);
                    worldWidth = Mathf.Max(worldWidth, lr.widthMultiplier * lr.widthCurve.Evaluate(0.5f));
                }
                if (all.Count > 1)
                {
                    Vector2 min = new Vector2(all.Min(p => p.x), all.Min(p => p.y));
                    Vector2 max = new Vector2(all.Max(p => p.x), all.Max(p => p.y));
                    Vector2 c = (min + max) * 0.5f;
                    float span = Mathf.Max(max.x - min.x, max.y - min.y, 0.01f);
                    float scale = (N - 150f) / span;
                    widthPx = Mathf.Clamp(worldWidth * scale, 18f, 34f);
                    foreach (var poly in polys)
                        for (int k = 0; k + 1 < poly.Count; k++)
                        {
                            Vector2 a = (poly[k] - c) * scale + new Vector2(N * 0.5f, N * 0.5f);
                            Vector2 b = (poly[k + 1] - c) * scale + new Vector2(N * 0.5f, N * 0.5f);
                            segs.Add(new Vector4(a.x, a.y, b.x, b.y));
                        }
                    Log(name + ": " + lines.Length + " lines, " + segs.Count + " segments, stroke " + widthPx.ToString("F0") + "px");
                }
            }
            if (segs.Count == 0) Log(name + ": no line data found");

            var tex = new Texture2D(N, N, TextureFormat.RGBA32, true);
            Color bg = new Color(0.07f, 0.06f, 0.05f, 1f);
            Color rim = new Color(0.55f, 0.4f, 0.18f, 1f);
            Color ink = new Color(1f, 0.88f, 0.55f, 1f);
            float half = widthPx * 0.5f;
            var px = new Color[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    int border = Mathf.Min(Mathf.Min(x, y), Mathf.Min(N - 1 - x, N - 1 - y));
                    Color c = bg;
                    if (border < 14) c = Color.Lerp(rim, bg, border / 14f);
                    float d = float.MaxValue;
                    Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                    foreach (Vector4 sg in segs)
                    {
                        Vector2 a = new Vector2(sg.x, sg.y), b = new Vector2(sg.z, sg.w);
                        Vector2 ab = b - a;
                        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-4f, ab.sqrMagnitude));
                        d = Mathf.Min(d, Vector2.Distance(p, a + ab * t));
                    }
                    float stroke = Mathf.Clamp01(1f - (d - half) / 3f);
                    float glow = Mathf.Clamp01(1f - (d - half) / (half * 2.2f)) * 0.3f;
                    c = Color.Lerp(c, ink, Mathf.Max(stroke, glow));
                    c.a = 1f;
                    px[y * N + x] = c;
                }
            tex.SetPixels(px);
            tex.Apply();
            string path = GenDir + "/" + name + ".png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            TextureImporter ti = (TextureImporter)AssetImporter.GetAtPath(path);
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.maxTextureSize = 512;
            ti.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static Material Mat(string name, Color c, float metal, float smooth)
        {
            string path = GenDir + "/" + name + ".mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Metallic", metal);
            m.SetFloat("_Smoothness", smooth);
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material FaceMat(int kind, Texture2D tex)
        {
            string[] names = { "Dial_ThreeBars", "Dial_Triangle", "Dial_Spiral" };
            string path = GenDir + "/" + names[kind] + ".mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
            m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", Color.white);
            m.SetTexture("_EmissionMap", tex);
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", new Color(0.45f, 0.36f, 0.22f));
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            m.SetFloat("_Smoothness", 0.35f);
            m.SetFloat("_Metallic", 0.2f);
            EditorUtility.SetDirty(m);
            return m;
        }

        static GameObject Prim(PrimitiveType type, Transform parent, string name, Vector3 pos, Quaternion rot, Vector3 scale, Material m)
        {
            GameObject g = GameObject.CreatePrimitive(type);
            g.name = name;
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            g.transform.localRotation = rot;
            g.transform.localScale = scale;
            UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());
            g.GetComponent<Renderer>().sharedMaterial = m;
            return g;
        }

        static void SetupLock(Scene s, Transform prism, Texture2D[] sigilTex)
        {
            Transform gateT = FindStarts(s, "Ember gate 0");
            Transform door = FindStarts(s, "Sealed carved stone door");
            if (gateT == null || door == null) { Log("gate or door missing"); return; }
            LockedGate gate = gateT.GetComponent<LockedGate>();
            Transform old = gateT.Find(LockName);
            if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);

            Bounds db = door.GetComponent<Renderer>().bounds;
            Vector3 f = door.forward; f.y = 0f; f.Normalize();
            Vector3 toPrism = prism != null ? prism.position - door.position : -f;
            toPrism.y = 0f;
            Vector3 outward = Vector3.Dot(toPrism, f) >= 0f ? f : -f;
            Vector3 absOut = new Vector3(Mathf.Abs(outward.x), 0f, Mathf.Abs(outward.z));
            float half = Mathf.Abs(Vector3.Dot(db.extents, absOut));
            foreach (Renderer r in gateT.GetComponentsInChildren<Renderer>(true))
            {
                if (r.GetComponent<TextMesh>() != null) continue;
                float front = Vector3.Dot(r.bounds.center - db.center, outward) + Vector3.Dot(r.bounds.extents, absOut);
                if (front > half && front < half + 1.5f) half = front;
            }
            Vector3 center = new Vector3(db.center.x, db.min.y + 1.35f, db.center.z) + outward * (half + 0.02f);

            Material bronze = Mat("LockBronze", new Color(0.42f, 0.29f, 0.13f), 0.85f, 0.55f);
            Material iron = Mat("LockIron", new Color(0.07f, 0.065f, 0.06f), 0.6f, 0.35f);
            Material[] faces = { FaceMat(0, sigilTex[0]), FaceMat(1, sigilTex[1]), FaceMat(2, sigilTex[2]) };

            GameObject lockGo = new GameObject(LockName);
            lockGo.transform.SetParent(gateT, true);
            lockGo.transform.position = center;
            lockGo.transform.rotation = Quaternion.LookRotation(outward, Vector3.up);
            Vector3 ls = lockGo.transform.lossyScale;
            lockGo.transform.localScale = new Vector3(lockGo.transform.localScale.x / ls.x, lockGo.transform.localScale.y / ls.y, lockGo.transform.localScale.z / ls.z);
            Transform L = lockGo.transform;

            const float side = 0.34f;
            const float width = 0.3f;
            const float spacing = 0.44f;
            float apothem = side / (2f * Mathf.Sqrt(3f));
            float circum = side / Mathf.Sqrt(3f);
            float totalW = spacing * 3f + 0.12f;
            float dialZ = 0.06f + circum;

            GameObject back = Prim(PrimitiveType.Cube, L, "Back plate", new Vector3(0f, 0f, 0.03f), Quaternion.identity, new Vector3(totalW + 0.1f, 0.78f, 0.06f), iron);
            back.AddComponent<BoxCollider>();
            float barY = side * 0.5f + 0.045f;
            Prim(PrimitiveType.Cube, L, "Top bar", new Vector3(0f, barY + 0.06f, dialZ * 0.5f + 0.04f), Quaternion.identity, new Vector3(totalW + 0.1f, 0.12f, dialZ + 0.08f), bronze);
            Prim(PrimitiveType.Cube, L, "Bottom bar", new Vector3(0f, -barY - 0.06f, dialZ * 0.5f + 0.04f), Quaternion.identity, new Vector3(totalW + 0.1f, 0.12f, dialZ + 0.08f), bronze);
            for (int i = 0; i <= 3; i++)
            {
                float x = (i - 1.5f) * spacing;
                Prim(PrimitiveType.Cube, L, "Divider", new Vector3(x, 0f, dialZ * 0.5f + 0.04f), Quaternion.identity, new Vector3(0.06f, barY * 2f + 0.24f, dialZ + 0.08f), bronze);
            }
            Prim(PrimitiveType.Cube, L, "Window lip top", new Vector3(0f, side * 0.5f + 0.012f, dialZ + apothem + 0.05f), Quaternion.identity, new Vector3(totalW, 0.025f, 0.04f), bronze);
            Prim(PrimitiveType.Cube, L, "Window lip bottom", new Vector3(0f, -side * 0.5f - 0.012f, dialZ + apothem + 0.05f), Quaternion.identity, new Vector3(totalW, 0.025f, 0.04f), bronze);

            CombinationLock cl = lockGo.AddComponent<CombinationLock>();
            cl.gate = gate;
            cl.values = new[] { 1, 0, 0 };
            cl.solution = new[] { 0, 2, 1 };
            var faceRenderers = new List<Renderer>();
            for (int i = 0; i < 3; i++)
            {
                GameObject w = new GameObject("Dial " + (i + 1));
                w.transform.SetParent(L, false);
                w.transform.localPosition = new Vector3((i - 1) * spacing, 0f, dialZ);
                BoxCollider bc = w.AddComponent<BoxCollider>();
                bc.size = new Vector3(width + 0.02f, circum * 2f, circum * 2f);
                Prim(PrimitiveType.Cylinder, w.transform, "Core", Vector3.zero, Quaternion.Euler(0f, 0f, 90f), new Vector3(apothem * 1.9f, width * 0.5f, apothem * 1.9f), iron);
                foreach (float x in new[] { -width * 0.5f - 0.006f, width * 0.5f + 0.006f })
                    Prim(PrimitiveType.Cylinder, w.transform, "Flange", new Vector3(x, 0f, 0f), Quaternion.Euler(0f, 0f, 90f), new Vector3(circum * 2.08f, 0.006f, circum * 2.08f), bronze);
                for (int k = 0; k < 3; k++)
                {
                    Quaternion r = Quaternion.AngleAxis(-120f * k, Vector3.right);
                    GameObject q = Prim(PrimitiveType.Quad, w.transform, "Face " + (k == 0 ? "three bars" : k == 1 ? "triangle" : "spiral"),
                        r * new Vector3(0f, 0f, apothem + 0.002f), r * Quaternion.Euler(0f, 180f, 0f), new Vector3(width, side, 1f), faces[k]);
                    Renderer qr = q.GetComponent<Renderer>();
                    qr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    faceRenderers.Add(qr);
                }
                cl.wheels[i] = w.transform;
                w.transform.localRotation = Quaternion.AngleAxis(120f * cl.values[i], Vector3.right);
            }
            cl.faceRenderers = faceRenderers.ToArray();
            cl.faceGlow = new Color(0.45f, 0.36f, 0.22f);

            GameObject lamp = new GameObject("Lock lamp");
            lamp.transform.SetParent(L, false);
            lamp.transform.localPosition = new Vector3(0f, 0.7f, 0.8f);
            Light l = lamp.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(1f, 0.78f, 0.5f);
            l.range = 3f;
            l.intensity = 1.4f;
            l.shadows = LightShadows.None;

            if (gate != null)
            {
                gate.isOpen = false;
                gate.raiseHeight = 8f;
                EditorUtility.SetDirty(gate);
            }
            EditorUtility.SetDirty(cl);
            Log("lock at " + center.ToString("F2") + " outward " + outward.ToString("F2"));
        }

        static void Snapshots()
        {
            try
            {
                Scene s = SceneManager.GetActiveScene();
                Transform lockT = FindStarts(s, LockName);
                Transform prism = FindStarts(s, "White triangular prism");
                if (lockT != null)
                {
                    Snap(lockT.position + lockT.forward * 1.6f + Vector3.up * 0.15f, lockT.position, "Backups/snap_lock_close.png", 45f, true);
                    Snap(lockT.position + lockT.forward * 4.5f + Vector3.up * 0.6f, lockT.position + Vector3.up * 1.2f, "Backups/snap_lock_far.png", 60f, true);
                }
                if (prism != null)
                {
                    Vector3 p = prism.position;
                    Snap(new Vector3(p.x, 2.4f, p.z - 7.5f), new Vector3(p.x, 3.2f, p.z + 3f), "Backups/snap_prism.png", 60f, true);
                }
                foreach (MirrorSocket ms in UnityEngine.Object.FindObjectsByType<MirrorSocket>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                {
                    Vector3 c = ms.transform.position;
                    Snap(c + new Vector3(1.8f, 1.6f, -1.8f), c + Vector3.up * 0.4f, "Backups/snap_platform_" + ms.shapeName + ".png", 50f, true);
                    break;
                }
                Log("snapshots written");
            }
            catch (Exception e)
            {
                Log("SNAP FAILED " + e);
            }
        }

        static void Snap(Vector3 camPos, Vector3 target, string file, float fov, bool lamp)
        {
            GameObject go = new GameObject("SnapCam");
            go.hideFlags = HideFlags.HideAndDontSave;
            Camera cam = go.AddComponent<Camera>();
            cam.transform.position = camPos;
            cam.transform.LookAt(target);
            cam.fieldOfView = fov;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 200f;
            GameObject lg = null;
            if (lamp)
            {
                lg = new GameObject("SnapLamp");
                lg.hideFlags = HideFlags.HideAndDontSave;
                lg.transform.position = camPos + Vector3.up * 0.3f;
                Light l = lg.AddComponent<Light>();
                l.type = LightType.Point;
                l.range = 12f;
                l.intensity = 2.5f;
                l.color = new Color(1f, 0.92f, 0.82f);
            }
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
            if (lg != null) UnityEngine.Object.DestroyImmediate(lg);
        }
    }
}
