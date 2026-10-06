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
    static class SunkenTrialSetup
    {
        const string TriggerPath = "Assets/Interaction/Editor/.run_trial";
        const string WorkScene = "Assets/Scenes/FourfoldCitadel_WithOurStuff.unity";
        const string GenDir = "Assets/Interaction/Generated/Sigils";
        const string PlatformsRoot = "SUNKEN TRIAL - mirror platforms";
        const string LockName = "Sigil Lock";
        const float PlatformHeight = 0.12f;
        const float PlatformRadius = 0.7f;

        static StringBuilder report;

        static SunkenTrialSetup()
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

        static Transform Find(Scene s, Func<Transform, bool> pred)
        {
            foreach (GameObject root in s.GetRootGameObjects())
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                    if (pred(t)) return t;
            return null;
        }

        static Transform FindStarts(Scene s, string prefix) => Find(s, t => t.name.StartsWith(prefix, StringComparison.Ordinal));

        [MenuItem("Tools/Interaction/Build Sunken Trial (platforms, moon, lock)")]
        static void Run()
        {
            report = new StringBuilder();
            try
            {
                EditorSceneManager.SaveOpenScenes();
                Directory.CreateDirectory("Backups");
                File.Copy(WorkScene, "Backups/FourfoldCitadel_WithOurStuff_before_trial.unity", true);
                Scene s = EditorSceneManager.OpenScene(WorkScene, OpenSceneMode.Single);
                Directory.CreateDirectory(GenDir);

                Transform oldRoot = FindStarts(s, PlatformsRoot);
                if (oldRoot != null) UnityEngine.Object.DestroyImmediate(oldRoot.gameObject);

                Material stone = null;
                Transform door = FindStarts(s, "Sealed carved stone door");
                if (door != null) stone = door.GetComponent<Renderer>().sharedMaterial;
                Material outlineMat = UnlitMat("SocketOutline", new Color(1f, 0.72f, 0.3f));
                Material bronze = LitMat("LockBronze", new Color(0.36f, 0.25f, 0.12f), 0.85f, 0.5f);
                Material iron = LitMat("LockIron", new Color(0.09f, 0.085f, 0.08f), 0.6f, 0.35f);
                TextMesh textSource = UnityEngine.Object.FindObjectsByType<TextMesh>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(t => t.gameObject.scene == s);

                var mirrors = new Dictionary<string, MirrorPickup>();
                foreach (MirrorPickup m in UnityEngine.Object.FindObjectsByType<MirrorPickup>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (m.gameObject.scene != s) continue;
                    if (m.name.Contains("Oval")) mirrors["Oval"] = m;
                    else if (m.name.Contains("Rectangle")) mirrors["Rectangle"] = m;
                    else if (m.name.Contains("Circle")) mirrors["Circle"] = m;
                }
                PuzzleTorch torch = UnityEngine.Object.FindObjectsByType<PuzzleTorch>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(t => t.gameObject.scene == s);
                Transform prism = FindStarts(s, "White triangular prism");
                Log("mirrors: " + string.Join(", ", mirrors.Select(kv => kv.Key + "@" + kv.Value.transform.position.ToString("F2"))));
                Log("torch: " + (torch != null ? torch.transform.position.ToString("F2") : "MISSING") + " prism: " + (prism != null ? prism.position.ToString("F2") : "MISSING"));
                if (mirrors.Count < 3 || torch == null || prism == null) throw new Exception("Need three mirrors, the PuzzleTorch and the prism in the scene");

                GameObject root = new GameObject(PlatformsRoot);
                SceneManager.MoveGameObjectToScene(root, s);
                string[] order = { "Oval", "Rectangle", "Circle" };
                var sockets = new Dictionary<string, MirrorSocket>();
                foreach (string shape in order)
                {
                    MirrorPickup m = mirrors[shape];
                    Vector3 mp = m.transform.position;
                    float ground = GroundY(mp, m.transform);
                    GameObject plat = new GameObject("Platform_" + shape);
                    plat.transform.SetParent(root.transform, false);
                    plat.transform.position = new Vector3(mp.x, ground, mp.z);

                    GameObject disk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    disk.name = "Stone";
                    disk.transform.SetParent(plat.transform, false);
                    disk.transform.localPosition = new Vector3(0f, PlatformHeight * 0.5f, 0f);
                    disk.transform.localScale = new Vector3(PlatformRadius * 2f, PlatformHeight * 0.5f, PlatformRadius * 2f);
                    UnityEngine.Object.DestroyImmediate(disk.GetComponent<Collider>());
                    disk.AddComponent<MeshCollider>().sharedMesh = disk.GetComponent<MeshFilter>().sharedMesh;
                    if (stone != null) disk.GetComponent<Renderer>().sharedMaterial = stone;

                    GameObject rim = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    rim.name = "Bronze rim";
                    rim.transform.SetParent(plat.transform, false);
                    rim.transform.localPosition = new Vector3(0f, PlatformHeight * 0.35f, 0f);
                    rim.transform.localScale = new Vector3(PlatformRadius * 2f + 0.06f, PlatformHeight * 0.35f, PlatformRadius * 2f + 0.06f);
                    UnityEngine.Object.DestroyImmediate(rim.GetComponent<Collider>());
                    rim.GetComponent<Renderer>().sharedMaterial = bronze;

                    GameObject icon = new GameObject("Mirror outline");
                    icon.transform.SetParent(plat.transform, false);
                    icon.transform.localPosition = new Vector3(0f, PlatformHeight + 0.006f, 0f);
                    icon.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
                    LineRenderer lr = icon.AddComponent<LineRenderer>();
                    lr.useWorldSpace = false;
                    lr.loop = true;
                    lr.alignment = LineAlignment.TransformZ;
                    lr.widthMultiplier = 0.035f;
                    lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    lr.receiveShadows = false;
                    lr.sharedMaterial = outlineMat;
                    Vector3[] pts = ShapePoints(shape);
                    lr.positionCount = pts.Length;
                    lr.SetPositions(pts);

                    MirrorSocket sock = plat.AddComponent<MirrorSocket>();
                    sock.shapeName = shape;
                    sock.mirror = m;
                    sock.surfaceHeight = PlatformHeight;
                    sock.outline = lr;
                    sockets[shape] = sock;

                    m.transform.position = new Vector3(mp.x, ground + PlatformHeight, mp.z);
                    EditorUtility.SetDirty(m.transform);
                    Log("platform " + shape + " at " + plat.transform.position.ToString("F2") + " ground " + ground.ToString("F3"));
                }
                Transform tp = torch.transform;
                sockets["Oval"].previousPoint = tp;
                sockets["Oval"].nextPoint = sockets["Rectangle"].transform;
                sockets["Rectangle"].previousPoint = sockets["Oval"].transform;
                sockets["Rectangle"].nextPoint = sockets["Circle"].transform;
                sockets["Circle"].previousPoint = sockets["Rectangle"].transform;
                sockets["Circle"].nextPoint = prism;

                foreach (string shape in order)
                {
                    MirrorSocket sock = sockets[shape];
                    if (textSource != null) AddLabel(sock.transform, shape.ToUpperInvariant(), textSource, sock.previousPoint.position);
                }

                float glassY = mirrors["Oval"].localBounds.center.y;
                GameObject aim = new GameObject("Beam aim");
                aim.transform.SetParent(sockets["Oval"].transform, false);
                aim.transform.localPosition = new Vector3(0f, PlatformHeight + glassY, 0f);
                SerializedObject ts = new SerializedObject(torch);
                SetEnum(ts, "beamAim", 1);
                SetRef(ts, "target", aim.transform);
                SetFloat(ts, "beamLength", 160f);
                SetInt(ts, "maxBounces", 5);
                SetBool(ts, "keepBeamLevel", false);
                ts.ApplyModifiedPropertiesWithoutUndo();

                SetupPrism(s, prism);
                SetupLock(s, prism, bronze, iron);

                EditorSceneManager.MarkSceneDirty(s);
                EditorSceneManager.SaveScene(s);
                AssetDatabase.SaveAssets();
                Log("DONE");
            }
            catch (Exception e)
            {
                Log("FAILED " + e);
            }
            File.WriteAllText("Backups/trial_report.txt", report.ToString());
        }

        static void SetEnum(SerializedObject so, string n, int v) { var p = so.FindProperty(n); if (p != null) p.enumValueIndex = v; else Log("missing " + n); }
        static void SetRef(SerializedObject so, string n, UnityEngine.Object v) { var p = so.FindProperty(n); if (p != null) p.objectReferenceValue = v; else Log("missing " + n); }
        static void SetFloat(SerializedObject so, string n, float v) { var p = so.FindProperty(n); if (p != null) p.floatValue = v; else Log("missing " + n); }
        static void SetInt(SerializedObject so, string n, int v) { var p = so.FindProperty(n); if (p != null) p.intValue = v; else Log("missing " + n); }
        static void SetBool(SerializedObject so, string n, bool v) { var p = so.FindProperty(n); if (p != null) p.boolValue = v; else Log("missing " + n); }

        static float GroundY(Vector3 p, Transform ignore)
        {
            RaycastHit[] hits = Physics.RaycastAll(p + Vector3.up * 2.5f, Vector3.down, 6f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            float y = p.y;
            foreach (RaycastHit h in hits)
            {
                if (ignore != null && h.collider.transform.IsChildOf(ignore)) continue;
                if (h.collider.GetComponentInParent<MirrorSocket>() != null) continue;
                if (h.distance < best) { best = h.distance; y = h.point.y; }
            }
            return y;
        }

        static Vector3[] ShapePoints(string shape)
        {
            var pts = new List<Vector3>();
            if (shape == "Rectangle")
            {
                float w = 0.2f, h = 0.3f;
                pts.Add(new Vector3(-w, -h, 0f)); pts.Add(new Vector3(w, -h, 0f)); pts.Add(new Vector3(w, h, 0f)); pts.Add(new Vector3(-w, h, 0f));
            }
            else
            {
                float rx = shape == "Oval" ? 0.2f : 0.28f;
                float ry = shape == "Oval" ? 0.31f : 0.28f;
                for (int i = 0; i < 40; i++)
                {
                    float a = i / 40f * Mathf.PI * 2f;
                    pts.Add(new Vector3(Mathf.Cos(a) * rx, Mathf.Sin(a) * ry, 0f));
                }
            }
            return pts.ToArray();
        }

        static void AddLabel(Transform plat, string text, TextMesh source, Vector3 from)
        {
            Vector3 dir = plat.position - from;
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-4f) dir = Vector3.forward;
            dir.Normalize();
            GameObject go = new GameObject("Label");
            go.transform.SetParent(plat, false);
            go.transform.position = plat.position - dir * (PlatformRadius - 0.17f) + Vector3.up * (PlatformHeight + 0.008f);
            float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            go.transform.rotation = Quaternion.Euler(90f, yaw, 0f);
            TextMesh tm = go.AddComponent<TextMesh>();
            tm.font = source.font;
            tm.fontSize = source.fontSize;
            tm.characterSize = source.characterSize;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = new Color(1f, 0.8f, 0.45f);
            tm.text = text;
            MeshRenderer mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = source.GetComponent<MeshRenderer>().sharedMaterial;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Vector3 srcScale = source.transform.lossyScale;
            go.transform.localScale = Vector3.one * Mathf.Max(0.05f, Mathf.Min(srcScale.x, srcScale.y)) * 0.45f;
        }

        static void SetupPrism(Scene s, Transform prism)
        {
            Transform group = prism.parent;
            Transform old = prism.Find("Beam catcher");
            if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
            Renderer pr = prism.GetComponent<Renderer>();
            Bounds b = pr.bounds;
            if (group != null)
                foreach (Renderer r in group.GetComponentsInChildren<Renderer>(true))
                {
                    if (!r.transform.name.StartsWith("Cube_") && !(r.transform.parent != null && r.transform.parent.name.StartsWith("Cube_"))) continue;
                    Vector3 d = r.bounds.center - b.center; d.y = 0f;
                    if (d.magnitude < 2.5f) b.Encapsulate(r.bounds);
                }
            float floor = GroundY(new Vector3(b.center.x, b.min.y - 0.05f, b.center.z), prism);
            Vector3 min = b.min; min.y = Mathf.Min(min.y, floor);
            Bounds catcher = new Bounds();
            catcher.SetMinMax(min - new Vector3(0.1f, 0f, 0.1f), b.max + new Vector3(0.1f, 0.1f, 0.1f));
            GameObject c = new GameObject("Beam catcher");
            c.transform.SetParent(prism, false);
            c.transform.position = catcher.center;
            c.transform.rotation = Quaternion.identity;
            BoxCollider bc = c.AddComponent<BoxCollider>();
            Vector3 ls = prism.lossyScale;
            bc.size = new Vector3(catcher.size.x / Mathf.Max(0.001f, ls.x), catcher.size.y / Mathf.Max(0.001f, ls.y), catcher.size.z / Mathf.Max(0.001f, ls.z));
            Log("prism catcher " + catcher.center.ToString("F2") + " size " + catcher.size.ToString("F2"));

            PrismMoonController pmc = prism.GetComponent<PrismMoonController>();
            if (pmc == null) pmc = prism.gameObject.AddComponent<PrismMoonController>();
            pmc.prismRenderer = pr;
            Transform glow = FindStarts(s, "Pearl prism glow");
            pmc.prismLight = glow != null ? glow.GetComponent<Light>() : null;
            Transform moon = FindStarts(s, "Engraved two dimensional moon");
            pmc.moon = moon != null ? moon.GetComponent<SpriteRenderer>() : null;
            Transform halo = FindStarts(s, "Soft lunar halo");
            pmc.moonHalo = halo != null ? halo.GetComponent<Renderer>() : null;
            Transform ml = FindStarts(s, "Lunar light on engraved stone");
            pmc.moonLight = ml != null ? ml.GetComponent<Light>() : null;
            string md = "Assets/Imported/LunarPuzzle/Moon/";
            pmc.whiteMoon = AssetDatabase.LoadAssetAtPath<Sprite>(md + "Moon_White_Full_DiscOnly.png");
            pmc.greenCrescent = AssetDatabase.LoadAssetAtPath<Sprite>(md + "Moon_Green_Crescent_DiscOnly.png");
            pmc.blueHalf = AssetDatabase.LoadAssetAtPath<Sprite>(md + "Moon_Blue_Half_DiscOnly.png");
            pmc.redFull = AssetDatabase.LoadAssetAtPath<Sprite>(md + "Moon_Red_Full_DiscOnly.png");
            Renderer[] Sigil(string n)
            {
                Transform t = FindStarts(s, n);
                return t != null ? t.GetComponentsInChildren<Renderer>(true) : new Renderer[0];
            }
            pmc.leftSigil = Sigil("Lunar seal 0");
            pmc.upperSigil = Sigil("Lunar seal 1");
            pmc.rightSigil = Sigil("Lunar seal 2");
            EditorUtility.SetDirty(pmc);
            Log("prism controller: light " + (pmc.prismLight != null) + " moon " + (pmc.moon != null) + " halo " + (pmc.moonHalo != null) + " moonLight " + (pmc.moonLight != null)
                + " sprites " + (pmc.whiteMoon != null) + (pmc.greenCrescent != null) + (pmc.blueHalf != null) + (pmc.redFull != null)
                + " sigils " + pmc.leftSigil.Length + "/" + pmc.upperSigil.Length + "/" + pmc.rightSigil.Length);
        }

        static void SetupLock(Scene s, Transform prism, Material bronze, Material iron)
        {
            Transform gateT = FindStarts(s, "Ember gate 0");
            Transform door = FindStarts(s, "Sealed carved stone door");
            if (gateT == null || door == null) { Log("gate or door missing"); return; }
            Transform old = gateT.Find(LockName);
            if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
            LockedGate gate = gateT.GetComponent<LockedGate>();
            if (gate == null) gate = gateT.gameObject.AddComponent<LockedGate>();
            gate.raiseHeight = 8f;
            gate.isOpen = false;

            Bounds db = door.GetComponent<Renderer>().bounds;
            Vector3 f = door.forward; f.y = 0f; f.Normalize();
            Vector3 toPrism = prism.position - door.position; toPrism.y = 0f;
            Vector3 outward = Vector3.Dot(toPrism, f) >= 0f ? f : -f;
            float half = Mathf.Abs(Vector3.Dot(db.extents, new Vector3(Mathf.Abs(outward.x), 0f, Mathf.Abs(outward.z))));
            Vector3 center = new Vector3(db.center.x, db.min.y + 1.3f, db.center.z) + outward * (half + 0.03f);

            GameObject lockGo = new GameObject(LockName);
            lockGo.transform.SetParent(gateT, true);
            lockGo.transform.position = center;
            lockGo.transform.rotation = Quaternion.LookRotation(outward, Vector3.up);
            lockGo.transform.localScale = Vector3.one;
            Transform L = lockGo.transform;
            Vector3 ps = L.lossyScale;
            if (Mathf.Abs(ps.x - 1f) > 0.01f || Mathf.Abs(ps.y - 1f) > 0.01f || Mathf.Abs(ps.z - 1f) > 0.01f)
                L.localScale = new Vector3(1f / ps.x, 1f / ps.y, 1f / ps.z);

            Box(L, "Back plate", new Vector3(0f, 0f, 0.03f), new Vector3(1.16f, 0.56f, 0.06f), iron, true);
            Box(L, "Top bar", new Vector3(0f, 0.205f, 0.13f), new Vector3(1.16f, 0.07f, 0.16f), bronze, false);
            Box(L, "Bottom bar", new Vector3(0f, -0.205f, 0.13f), new Vector3(1.16f, 0.07f, 0.16f), bronze, false);
            foreach (float x in new[] { -0.545f, -0.16f, 0.16f, 0.545f })
                Box(L, "Divider", new Vector3(x, 0f, 0.13f), new Vector3(0.04f, 0.44f, 0.16f), bronze, false);

            Material[] sigils = { SigilMat(0), SigilMat(1), SigilMat(2) };
            CombinationLock cl = lockGo.AddComponent<CombinationLock>();
            cl.gate = gate;
            var faces = new List<Renderer>();
            float side = 0.26f;
            float apothem = side / (2f * Mathf.Sqrt(3f));
            for (int i = 0; i < 3; i++)
            {
                GameObject w = new GameObject("Dial " + (i + 1));
                w.transform.SetParent(L, false);
                w.transform.localPosition = new Vector3((i - 1) * 0.35f, 0f, 0.14f);
                BoxCollider bc = w.AddComponent<BoxCollider>();
                bc.size = new Vector3(0.26f, 0.32f, 0.32f);
                GameObject core = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                core.name = "Core";
                core.transform.SetParent(w.transform, false);
                core.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                core.transform.localScale = new Vector3(apothem * 1.9f, 0.12f, apothem * 1.9f);
                UnityEngine.Object.DestroyImmediate(core.GetComponent<Collider>());
                core.GetComponent<Renderer>().sharedMaterial = iron;
                foreach (float x in new[] { -0.125f, 0.125f })
                {
                    GameObject fl = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    fl.name = "Flange";
                    fl.transform.SetParent(w.transform, false);
                    fl.transform.localPosition = new Vector3(x, 0f, 0f);
                    fl.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                    fl.transform.localScale = new Vector3(0.31f, 0.008f, 0.31f);
                    UnityEngine.Object.DestroyImmediate(fl.GetComponent<Collider>());
                    fl.GetComponent<Renderer>().sharedMaterial = bronze;
                }
                for (int k = 0; k < 3; k++)
                {
                    GameObject q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    q.name = "Face " + (k == 0 ? "three bars" : k == 1 ? "triangle" : "spiral");
                    q.transform.SetParent(w.transform, false);
                    Quaternion r = Quaternion.AngleAxis(-120f * k, Vector3.right);
                    q.transform.localRotation = r * Quaternion.Euler(0f, 180f, 0f);
                    q.transform.localPosition = r * new Vector3(0f, 0f, apothem + 0.002f);
                    q.transform.localScale = new Vector3(0.235f, side, 1f);
                    UnityEngine.Object.DestroyImmediate(q.GetComponent<Collider>());
                    Renderer qr = q.GetComponent<Renderer>();
                    qr.sharedMaterial = sigils[k];
                    qr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    faces.Add(qr);
                }
                cl.wheels[i] = w.transform;
                w.transform.localRotation = Quaternion.AngleAxis(120f * cl.values[i], Vector3.right);
            }
            cl.faceRenderers = faces.ToArray();

            GameObject lamp = new GameObject("Lock lamp");
            lamp.transform.SetParent(L, false);
            lamp.transform.localPosition = new Vector3(0f, 0.55f, 0.6f);
            Light l = lamp.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(1f, 0.75f, 0.45f);
            l.range = 2.4f;
            l.intensity = 1.1f;
            l.shadows = LightShadows.None;
            EditorUtility.SetDirty(cl);
            Log("lock at " + center.ToString("F2") + " facing " + outward.ToString("F2") + " gate raise " + gate.raiseHeight + " solution three-bars, spiral, triangle");
        }

        static GameObject Box(Transform parent, string name, Vector3 pos, Vector3 size, Material m, bool collider)
        {
            GameObject b = GameObject.CreatePrimitive(PrimitiveType.Cube);
            b.name = name;
            b.transform.SetParent(parent, false);
            b.transform.localPosition = pos;
            b.transform.localScale = size;
            if (!collider) UnityEngine.Object.DestroyImmediate(b.GetComponent<Collider>());
            b.GetComponent<Renderer>().sharedMaterial = m;
            return b;
        }

        static Material UnlitMat(string name, Color c)
        {
            string path = GenDir + "/" + name + ".mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Unlit")); AssetDatabase.CreateAsset(m, path); }
            m.SetColor("_BaseColor", c);
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material LitMat(string name, Color c, float metal, float smooth)
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

        static Material SigilMat(int kind)
        {
            string[] names = { "Sigil_ThreeBars", "Sigil_Triangle", "Sigil_Spiral" };
            string texPath = GenDir + "/" + names[kind] + ".png";
            File.WriteAllBytes(texPath, DrawSigil(kind).EncodeToPNG());
            AssetDatabase.ImportAsset(texPath, ImportAssetOptions.ForceUpdate);
            TextureImporter ti = (TextureImporter)AssetImporter.GetAtPath(texPath);
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.maxTextureSize = 256;
            ti.mipmapEnabled = true;
            ti.SaveAndReimport();
            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            string matPath = GenDir + "/" + names[kind] + ".mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, matPath); }
            m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", Color.white);
            m.SetTexture("_EmissionMap", tex);
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", new Color(0.25f, 0.2f, 0.12f));
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            m.SetFloat("_Smoothness", 0.3f);
            EditorUtility.SetDirty(m);
            return m;
        }

        static Texture2D DrawSigil(int kind)
        {
            const int N = 256;
            var segs = new List<Vector4>();
            void Seg(float x0, float y0, float x1, float y1) { segs.Add(new Vector4(x0, y0, x1, y1)); }
            if (kind == 0)
            {
                Seg(128, 40, 128, 216);
                Seg(70, 82, 186, 82);
                Seg(70, 128, 186, 128);
                Seg(70, 174, 186, 174);
            }
            else if (kind == 1)
            {
                Seg(128, 214, 46, 62);
                Seg(46, 62, 210, 62);
                Seg(210, 62, 128, 214);
                Seg(128, 82, 128, 160);
            }
            else
            {
                Vector2 prev = new Vector2(128, 128);
                for (int i = 1; i <= 260; i++)
                {
                    float th = i * 0.06f;
                    float r = 4f + 5.6f * th;
                    if (r > 92f) break;
                    Vector2 p = new Vector2(128 + Mathf.Cos(th) * r, 128 + Mathf.Sin(th) * r);
                    Seg(prev.x, prev.y, p.x, p.y);
                    prev = p;
                }
            }
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, true);
            Color bg = new Color(0.11f, 0.095f, 0.08f, 1f);
            Color edge = new Color(0.42f, 0.3f, 0.15f, 1f);
            Color ink = new Color(0.98f, 0.86f, 0.58f, 1f);
            var rng = new System.Random(kind * 97 + 13);
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float noise = (float)rng.NextDouble() * 0.03f;
                    Color c = bg + new Color(noise, noise, noise, 0f);
                    int border = Mathf.Min(Mathf.Min(x, y), Mathf.Min(N - 1 - x, N - 1 - y));
                    if (border < 10) c = Color.Lerp(edge, bg, border / 10f);
                    float d = float.MaxValue;
                    Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                    foreach (Vector4 s in segs)
                    {
                        Vector2 a = new Vector2(s.x, s.y), b = new Vector2(s.z, s.w);
                        Vector2 ab = b - a;
                        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-4f, ab.sqrMagnitude));
                        d = Mathf.Min(d, Vector2.Distance(p, a + ab * t));
                    }
                    float stroke = Mathf.Clamp01(1f - (d - 7f) / 2.5f);
                    float glow = Mathf.Clamp01(1f - (d - 7f) / 14f) * 0.25f;
                    c = Color.Lerp(c, ink, Mathf.Max(stroke, glow));
                    c.a = 1f;
                    tex.SetPixel(x, y, c);
                }
            tex.Apply();
            return tex;
        }
    }
}
