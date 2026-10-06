using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace FPCharacter.EditorTools
{
    [InitializeOnLoad]
    public static class RoundTablePuzzleSetup
    {
        const string TriggerPath = "Assets/Interaction/Editor/.run_roundtable";
        const string Dir = "Assets/Environment/RoundTable";
        const string TableFbx = Dir + "/RoundTable.fbx";
        const string StoolFbx = Dir + "/Stool.fbx";
        const string HunchedFbx = Dir + "/GhostExplorer_ChairHunched.fbx";
        const string SlumpedFbx = Dir + "/GhostExplorer_ChairSlumped.fbx";
        const string GenDir = "Assets/Interaction/RoundTable";
        const string RootName = "RoundTablePuzzle";
        const int Seats = 10;
        const int NpcSeats = 9;
        const int PlayerSeatIndex = 9;
        const int Slots = 40;
        const float TableTop = 0.972f;
        const float BandThickness = 0.015f;
        const float BeamHeight = 1.02f;
        const float SeatRadius = 2.7f;
        const float TableScale = 2.25f;
        const float PrismScale = 1.6f;
        static readonly Vector3 PuzzlePosition = new Vector3(7f, 0f, 11f);

        static RoundTablePuzzleSetup()
        {
            if (!File.Exists(TriggerPath) || File.ReadAllText(TriggerPath).Trim() != "pending") return;
            EditorApplication.update += WaitAndRun;
        }

        static void WaitAndRun()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            EditorApplication.update -= WaitAndRun;
            Run();
            File.WriteAllText(TriggerPath, "done");
        }

        [MenuItem("Tools/Interaction/Rebuild Round Table Puzzle")]
        public static void Run()
        {
            try
            {
                Scene scene = SceneManager.GetActiveScene();
                if (scene.path != "Assets/GameTest.unity")
                {
                    if (scene.isDirty && !string.IsNullOrEmpty(scene.path)) EditorSceneManager.SaveScene(scene);
                    scene = EditorSceneManager.OpenScene("Assets/GameTest.unity", OpenSceneMode.Single);
                }
                else if (scene.isDirty) EditorSceneManager.SaveScene(scene);
                Directory.CreateDirectory("Backups");
                File.Copy("Assets/GameTest.unity", "Backups/GameTest_before_roundtable_v4.unity", true);
                EnsureFolder(GenDir);

                AnimatorController hunched = SetupPose(HunchedFbx, "Ghost_Chair_Hunched");
                AnimatorController slumped = SetupPose(SlumpedFbx, "Ghost_Chair_Slumped");
                SetupTextures();

                Material wood = Mat("RT_TableWood", new Color(0.36f, 0.23f, 0.13f), 0f, 0.3f);
                Material stoolMat = StoolMaterial();
                Material inner = Mat("RT_InnerRing", new Color(0.62f, 0.45f, 0.22f), 0.85f, 0.55f);
                Material outer = Mat("RT_OuterRing", new Color(0.2f, 0.19f, 0.18f), 0.75f, 0.45f);
                Material blockerInner = Mat("RT_BlockerInner", new Color(0.45f, 0.32f, 0.16f), 0.7f, 0.4f);
                Material blockerOuter = Mat("RT_BlockerOuter", new Color(0.32f, 0.31f, 0.3f), 0.2f, 0.25f);
                Material stone = Mat("RT_PrismStand", new Color(0.12f, 0.12f, 0.14f), 0.3f, 0.4f);
                Material glass = GlassMat();

                GameObject old = GameObject.Find(RootName);
                if (old != null) Object.DestroyImmediate(old);
                GameObject root = new GameObject(RootName);
                root.transform.position = PuzzlePosition;

                GameObject table = Instance(TableFbx, root.transform, "Table");
                table.transform.localScale = new Vector3(TableScale, 1f, TableScale);
                foreach (Renderer r in table.GetComponentsInChildren<Renderer>(true)) r.sharedMaterials = Enumerable.Repeat(wood, r.sharedMaterials.Length).ToArray();
                foreach (MeshFilter mf in table.GetComponentsInChildren<MeshFilter>(true))
                    if (mf.GetComponent<Collider>() == null) mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;

                HashSet<int> innerOpen = OpenSlots(new[] { 0, 3, 5, 9 }, new[] { 1, 2, 6, 7 }, new[] { 4, 8 });
                HashSet<int> outerOpen = OpenSlots(new[] { 1, 4, 7, 8 }, new[] { 0, 5, 9 }, new[] { 2, 3, 6 });
                RotatingRing innerRing = BuildRing(root.transform, "InnerRing", "inner ring", 0.30f * TableScale, 0.44f * TableScale, innerOpen, inner, blockerInner, 2);
                RotatingRing outerRing = BuildRing(root.transform, "OuterRing", "outer ring", 0.60f * TableScale, 0.76f * TableScale, outerOpen, outer, blockerOuter, 3);

                GameObject prism = new GameObject("Prism");
                prism.transform.SetParent(root.transform, false);
                prism.transform.localPosition = new Vector3(0f, TableTop, 0f);
                GameObject stand = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                stand.name = "Stand";
                Object.DestroyImmediate(stand.GetComponent<Collider>());
                stand.transform.SetParent(prism.transform, false);
                stand.transform.localPosition = new Vector3(0f, 0.012f, 0f);
                stand.transform.localScale = new Vector3(0.14f * PrismScale, 0.012f, 0.14f * PrismScale);
                stand.GetComponent<MeshRenderer>().sharedMaterial = stone;
                GameObject crystal = new GameObject("Crystal");
                crystal.transform.SetParent(prism.transform, false);
                crystal.transform.localPosition = new Vector3(0f, (BeamHeight - TableTop) - 0.08f * PrismScale, 0f);
                crystal.transform.localScale = Vector3.one * PrismScale;
                crystal.AddComponent<MeshFilter>().sharedMesh = PrismMesh();
                MeshRenderer cr = crystal.AddComponent<MeshRenderer>();
                cr.sharedMaterial = glass;
                cr.shadowCastingMode = ShadowCastingMode.Off;
                BoxCollider pc = prism.AddComponent<BoxCollider>();
                pc.center = new Vector3(0f, 0.04f + 0.1f * PrismScale, 0f);
                pc.size = new Vector3(0.15f * PrismScale, 0.17f * PrismScale + 0.04f, 0.15f * PrismScale);
                GameObject emit = new GameObject("BeamOrigin");
                emit.transform.SetParent(prism.transform, false);
                emit.transform.localPosition = new Vector3(0f, BeamHeight - TableTop, 0f);
                GameObject glowGo = new GameObject("PrismGlow");
                glowGo.transform.SetParent(prism.transform, false);
                glowGo.transform.localPosition = new Vector3(0f, 0.12f, 0f);
                Light glow = glowGo.AddComponent<Light>();
                glow.type = LightType.Point;
                glow.color = new Color(1f, 0.9f, 0.7f);
                glow.range = 1.5f;
                glow.intensity = 1.5f;
                glow.shadows = LightShadows.None;
                PrismBeamSplitter splitter = prism.AddComponent<PrismBeamSplitter>();
                splitter.emitOrigin = emit.transform;
                splitter.prismGlow = glow;
                splitter.alwaysLit = true;
                PrismOverload overload = prism.AddComponent<PrismOverload>();
                overload.crystal = cr;
                overload.glow = glow;
                overload.splitter = splitter;

                GhostPresence original = Object.FindObjectsByType<GhostPresence>(FindObjectsInactive.Include).FirstOrDefault(g => g.name.StartsWith("GhostExplorer"));
                if (original == null) original = Object.FindObjectsByType<GhostPresence>(FindObjectsInactive.Include).FirstOrDefault();
                if (original == null) throw new Exception("Original ghost not found in the scene");
                Vector3 localFacing = GhostLocalFacing(original);

                System.Random rng = new System.Random(20261003);
                bool[] beard = new bool[Seats];
                int[] poseOf = new int[Seats];
                for (int i = 0; i < NpcSeats; i++)
                {
                    beard[i] = rng.NextDouble() < 0.5;
                    poseOf[i] = rng.NextDouble() < 0.5 ? 0 : 1;
                }
                beard[PlayerSeatIndex] = true;
                poseOf[PlayerSeatIndex] = 0;
                if (beard.Take(NpcSeats).Count(b => b) < 3) { beard[1] = beard[4] = beard[7] = true; }
                if (beard.Take(NpcSeats).Count(b => !b) < 3) { beard[0] = beard[3] = beard[6] = false; }
                if (poseOf.Take(NpcSeats).Count(p => p == 0) < 3) { poseOf[2] = poseOf[5] = poseOf[8] = 0; }
                if (poseOf.Take(NpcSeats).Count(p => p == 1) < 3) { poseOf[0] = poseOf[4] = poseOf[7] = 1; }

                GameObject seatsRoot = new GameObject("Seats");
                seatsRoot.transform.SetParent(root.transform, false);
                var targets = new List<SeatBeamTarget>();
                PlayerSeat playerSeat = null;
                GameObject centerMarker = new GameObject("TableCenter");
                centerMarker.transform.SetParent(root.transform, false);
                centerMarker.transform.localPosition = Vector3.zero;
                for (int i = 0; i < Seats; i++)
                {
                    float a = i * 360f / Seats;
                    Vector3 dir = Quaternion.Euler(0f, a, 0f) * Vector3.forward;
                    GameObject seat = new GameObject("Seat_" + (i + 1));
                    seat.transform.SetParent(seatsRoot.transform, false);
                    seat.transform.localPosition = dir * SeatRadius;
                    seat.transform.localRotation = Quaternion.LookRotation(-dir, Vector3.up);

                    GameObject stool = Instance(StoolFbx, seat.transform, "Stool");
                    stool.transform.localPosition = Vector3.zero;
                    stool.transform.localRotation = Quaternion.Euler(0f, 22.5f, 0f);
                    foreach (Renderer r in stool.GetComponentsInChildren<Renderer>(true)) r.sharedMaterials = Enumerable.Repeat(stoolMat, r.sharedMaterials.Length).ToArray();
                    BoxCollider sc = stool.AddComponent<BoxCollider>();
                    sc.center = new Vector3(0f, 0.23f, 0f);
                    sc.size = new Vector3(0.28f, 0.46f, 0.28f);
                    if (i == PlayerSeatIndex)
                    {
                        seat.name = "Seat_10_Player";
                        BoxCollider look = seat.AddComponent<BoxCollider>();
                        look.isTrigger = false;
                        look.center = new Vector3(0f, 0.55f, 0f);
                        look.size = new Vector3(0.45f, 0.25f, 0.45f);
                        playerSeat = seat.AddComponent<PlayerSeat>();
                        playerSeat.tableCenter = centerMarker.transform;
                        playerSeat.disableWhileSeated = new Collider[] { sc, look };
                        continue;
                    }

                    GameObject npc = Object.Instantiate(original.gameObject, seat.transform);
                    npc.name = "SeatedGhost_" + (i + 1) + (poseOf[i] == 0 ? "_Hunched" : "_Slumped") + (beard[i] ? "_Beard" : "_NoBeard");
                    npc.transform.localPosition = Vector3.zero;
                    npc.transform.rotation = Quaternion.LookRotation(-dir, Vector3.up) * Quaternion.Inverse(Quaternion.LookRotation(localFacing, Vector3.up)) * root.transform.rotation;
                    foreach (GhostPresence gp in npc.GetComponentsInChildren<GhostPresence>(true)) Object.DestroyImmediate(gp);
                    Animator an = npc.GetComponentInChildren<Animator>();
                    if (an == null) an = npc.AddComponent<Animator>();
                    an.runtimeAnimatorController = poseOf[i] == 0 ? hunched : slumped;
                    an.applyRootMotion = false;
                    an.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
                    if (!beard[i]) npc.AddComponent<HideBeard>();
                    ApplyPalette(npc, i);
                    foreach (CapsuleCollider cc in npc.GetComponents<CapsuleCollider>())
                    {
                        cc.height = 1.25f;
                        cc.radius = 0.25f;
                        cc.center = new Vector3(0f, 0.75f, 0f);
                    }
                    foreach (Light l in npc.GetComponentsInChildren<Light>(true))
                        if (l.name == "LanternLight") l.intensity = 0.9f;

                    GameObject tgt = new GameObject("BeamTarget");
                    tgt.transform.SetParent(seat.transform, false);
                    tgt.transform.position = root.transform.position + dir * (SeatRadius - 0.28f) + Vector3.up * BeamHeight;
                    GameObject lg = new GameObject("SeatGlow");
                    lg.transform.SetParent(tgt.transform, false);
                    Light sl = lg.AddComponent<Light>();
                    sl.type = LightType.Point;
                    sl.color = new Color(1f, 0.85f, 0.6f);
                    sl.range = 1.3f;
                    sl.intensity = 0f;
                    sl.shadows = LightShadows.None;
                    sl.enabled = false;
                    SeatBeamTarget st = tgt.AddComponent<SeatBeamTarget>();
                    st.index = i;
                    st.glow = sl;
                    targets.Add(st);
                }
                splitter.seats = targets.ToArray();
                RoundTablePuzzle puzzle = root.AddComponent<RoundTablePuzzle>();
                puzzle.splitter = splitter;
                puzzle.playerSeat = playerSeat;
                puzzle.overload = overload;

                int initial = 0;
                for (int k = 0; k < NpcSeats; k++)
                {
                    int j = k * (Slots / Seats);
                    if (innerOpen.Contains(Mod(j - innerRing.stepIndex, Slots)) && outerOpen.Contains(Mod(j - outerRing.stepIndex, Slots))) initial++;
                }

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                GameObject prefabCopy = Object.Instantiate(root);
                prefabCopy.name = RootName;
                prefabCopy.transform.position = Vector3.zero;
                PrefabUtility.SaveAsPrefabAsset(prefabCopy, "Assets/Interaction/Prefabs/" + RootName + ".prefab");
                Object.DestroyImmediate(prefabCopy);
                AssetDatabase.SaveAssets();
                Debug.Log("[RoundTable] DONE at " + PuzzlePosition + ". Beams reaching seats at start: " + initial + "/" + NpcSeats
                    + ". Poses: " + string.Join(",", poseOf.Take(NpcSeats).Select(p => p == 0 ? "H" : "S")) + " Beards: " + string.Join(",", beard.Take(NpcSeats).Select(b => b ? "Y" : "N")) + " PlayerSeat: " + (playerSeat != null)
                    + " Facing(local): " + localFacing.ToString("F2"));
            }
            catch (Exception e)
            {
                Debug.LogError("[RoundTable] FAILED: " + e);
            }
        }

        static int Mod(int a, int m) => ((a % m) + m) % m;

        static readonly Color[][] Palettes =
        {
            new[] { new Color(0.36f, 0.10f, 0.09f), new Color(0.18f, 0.05f, 0.05f), new Color(0.55f, 0.46f, 0.30f), new Color(0.20f, 0.16f, 0.13f) },
            new[] { new Color(0.26f, 0.33f, 0.16f), new Color(0.13f, 0.17f, 0.08f), new Color(0.45f, 0.35f, 0.22f), new Color(0.17f, 0.17f, 0.13f) },
            new[] { new Color(0.20f, 0.22f, 0.36f), new Color(0.10f, 0.11f, 0.19f), new Color(0.55f, 0.52f, 0.45f), new Color(0.15f, 0.15f, 0.17f) },
            new[] { new Color(0.48f, 0.24f, 0.10f), new Color(0.25f, 0.12f, 0.05f), new Color(0.30f, 0.28f, 0.24f), new Color(0.21f, 0.16f, 0.11f) },
            new[] { new Color(0.36f, 0.35f, 0.33f), new Color(0.19f, 0.18f, 0.17f), new Color(0.40f, 0.12f, 0.10f), new Color(0.16f, 0.15f, 0.14f) },
            new[] { new Color(0.45f, 0.36f, 0.17f), new Color(0.24f, 0.19f, 0.08f), new Color(0.22f, 0.26f, 0.20f), new Color(0.20f, 0.17f, 0.12f) },
            new[] { new Color(0.18f, 0.29f, 0.30f), new Color(0.09f, 0.15f, 0.16f), new Color(0.50f, 0.42f, 0.28f), new Color(0.14f, 0.16f, 0.16f) },
            new[] { new Color(0.30f, 0.17f, 0.27f), new Color(0.15f, 0.08f, 0.14f), new Color(0.48f, 0.44f, 0.38f), new Color(0.17f, 0.14f, 0.16f) },
            new[] { new Color(0.32f, 0.23f, 0.15f), new Color(0.17f, 0.12f, 0.08f), new Color(0.28f, 0.33f, 0.20f), new Color(0.18f, 0.15f, 0.11f) },
        };
        static readonly string[] PaletteNames = { "Oxblood", "Moss", "Indigo", "Rust", "Ash", "Ochre", "SlateTeal", "Plum", "Umber" };

        static void ApplyPalette(GameObject npc, int index)
        {
            Color[] pal = Palettes[index % Palettes.Length];
            string dir = GenDir + "/NpcColors";
            EnsureFolder(dir);
            foreach (Renderer r in npc.GetComponentsInChildren<Renderer>(true))
            {
                Material[] ms = r.sharedMaterials;
                bool changed = false;
                for (int k = 0; k < ms.Length; k++)
                {
                    if (ms[k] == null) continue;
                    string n = ms[k].name;
                    int part = n == "Ghost_Tunic" ? 0 : n == "Ghost_TunicDk" ? 1 : n == "Ghost_Scarf" ? 2 : n == "Ghost_Trousers" ? 3 : -1;
                    if (part < 0) continue;
                    string path = dir + "/Seat" + (index + 1) + "_" + PaletteNames[index % PaletteNames.Length] + "_" + n.Replace("Ghost_", "") + ".mat";
                    Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (m == null)
                    {
                        m = new Material(ms[k]);
                        AssetDatabase.CreateAsset(m, path);
                    }
                    else m.CopyPropertiesFromMaterial(ms[k]);
                    Color c = pal[part];
                    c.a = ms[k].GetColor("_BaseColor").a;
                    m.SetColor("_BaseColor", c);
                    m.EnableKeyword("_EMISSION");
                    m.SetColor("_EmissionColor", new Color(c.r, c.g, c.b) * 0.12f);
                    EditorUtility.SetDirty(m);
                    ms[k] = m;
                    changed = true;
                }
                if (changed) r.sharedMaterials = ms;
            }
        }

        static HashSet<int> OpenSlots(int[] r1, int[] r2, int[] r3)
        {
            var s = new HashSet<int>();
            for (int k = 0; k < Seats; k++) s.Add((Slots / Seats) * k);
            int g = Slots / Seats;
            foreach (int k in r1) s.Add(g * k + 1);
            foreach (int k in r2) s.Add(g * k + 2);
            foreach (int k in r3) s.Add(g * k + 3);
            return s;
        }

        static RotatingRing BuildRing(Transform parent, string name, string label, float r0, float r1, HashSet<int> open, Material band, Material blocker, int startStep)
        {
            GameObject ring = new GameObject(name);
            ring.transform.SetParent(parent, false);
            ring.transform.localPosition = new Vector3(0f, TableTop, 0f);
            GameObject bandGo = new GameObject("Band");
            bandGo.transform.SetParent(ring.transform, false);
            Mesh mesh = AnnulusMesh(name + "_Band", r0, r1, BandThickness, 96);
            bandGo.AddComponent<MeshFilter>().sharedMesh = mesh;
            bandGo.AddComponent<MeshRenderer>().sharedMaterial = band;
            bandGo.AddComponent<MeshCollider>().sharedMesh = mesh;
            float rm = (r0 + r1) * 0.5f;
            float arc = 2f * Mathf.PI * rm / Slots;
            for (int l = 0; l < Slots; l++)
            {
                if (open.Contains(l)) continue;
                float a = l * 360f / Slots;
                Vector3 dir = Quaternion.Euler(0f, a, 0f) * Vector3.forward;
                GameObject b = GameObject.CreatePrimitive(PrimitiveType.Cube);
                b.name = "Blocker_" + l;
                b.transform.SetParent(ring.transform, false);
                b.transform.localPosition = dir * rm + Vector3.up * (BandThickness + 0.035f);
                b.transform.localRotation = Quaternion.LookRotation(dir, Vector3.up);
                b.transform.localScale = new Vector3(arc * 1.04f, 0.07f, 0.035f);
                b.GetComponent<MeshRenderer>().sharedMaterial = blocker;
                b.AddComponent<RingBlocker>();
            }
            RotatingRing rr = ring.AddComponent<RotatingRing>();
            rr.displayName = label;
            rr.stepDegrees = 360f / Slots;
            rr.stepIndex = startStep;
            ring.transform.localRotation = Quaternion.Euler(0f, rr.TargetAngle, 0f);
            return rr;
        }

        static Mesh AnnulusMesh(string name, float r0, float r1, float h, int seg)
        {
            string path = GenDir + "/" + name + ".asset";
            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var t = new List<int>();
            void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal)
            {
                int i = v.Count;
                v.Add(a); v.Add(b); v.Add(c); v.Add(d);
                n.Add(normal); n.Add(normal); n.Add(normal); n.Add(normal);
                t.Add(i); t.Add(i + 1); t.Add(i + 2); t.Add(i); t.Add(i + 2); t.Add(i + 3);
            }
            for (int s = 0; s < seg; s++)
            {
                float a0 = s * Mathf.PI * 2f / seg, a1 = (s + 1) * Mathf.PI * 2f / seg;
                Vector3 d0 = new Vector3(Mathf.Sin(a0), 0f, Mathf.Cos(a0)), d1 = new Vector3(Mathf.Sin(a1), 0f, Mathf.Cos(a1));
                Vector3 up = Vector3.up * h;
                Quad(d0 * r0 + up, d0 * r1 + up, d1 * r1 + up, d1 * r0 + up, Vector3.up);
                Quad(d0 * r1, d1 * r1, d1 * r1 + up, d0 * r1 + up, (d0 + d1).normalized);
                Quad(d1 * r0, d0 * r0, d0 * r0 + up, d1 * r0 + up, -(d0 + d1).normalized);
            }
            Mesh m = new Mesh { name = name };
            m.SetVertices(v);
            m.SetNormals(n);
            m.SetTriangles(t, 0);
            m.RecalculateBounds();
            FixWinding(m);
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        static void FixWinding(Mesh m)
        {
            Vector3[] v = m.vertices;
            Vector3[] n = m.normals;
            int[] t = m.triangles;
            for (int i = 0; i < t.Length; i += 3)
            {
                Vector3 face = Vector3.Cross(v[t[i + 1]] - v[t[i]], v[t[i + 2]] - v[t[i]]);
                if (Vector3.Dot(face, n[t[i]]) < 0f)
                {
                    int tmp = t[i + 1];
                    t[i + 1] = t[i + 2];
                    t[i + 2] = tmp;
                }
            }
            m.triangles = t;
        }

        static Mesh PrismMesh()
        {
            string path = GenDir + "/PrismCrystal.asset";
            float rr = 0.075f, eq = 0.08f, top = 0.25f;
            Vector3 center = new Vector3(0f, eq, 0f);
            Vector3[] e = new Vector3[3];
            for (int i = 0; i < 3; i++)
            {
                float ang = i * Mathf.PI * 2f / 3f;
                e[i] = new Vector3(Mathf.Sin(ang) * rr, eq, Mathf.Cos(ang) * rr);
            }
            Vector3 apex = new Vector3(0f, top, 0f), foot = Vector3.zero;
            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var t = new List<int>();
            void Tri(Vector3 a0, Vector3 a1, Vector3 a2)
            {
                Vector3 normal = (((a0 + a1 + a2) / 3f) - center).normalized;
                int i = v.Count;
                v.Add(a0); v.Add(a1); v.Add(a2);
                n.Add(normal); n.Add(normal); n.Add(normal);
                t.Add(i); t.Add(i + 1); t.Add(i + 2);
            }
            for (int i = 0; i < 3; i++)
            {
                Vector3 a0 = e[i], a1 = e[(i + 1) % 3];
                Tri(apex, a0, a1);
                Tri(foot, a1, a0);
            }
            Mesh m = new Mesh { name = "PrismCrystal" };
            m.SetVertices(v);
            m.SetNormals(n);
            m.SetTriangles(t, 0);
            m.RecalculateBounds();
            FixWinding(m);
            if (AssetDatabase.LoadAssetAtPath<Mesh>(path) != null) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        static Vector3 GhostLocalFacing(GhostPresence ghost)
        {
            Transform face = ghost.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "FaceDetails");
            Vector3 f = face != null ? ghost.transform.InverseTransformDirection(face.forward) : Vector3.forward;
            f.y = 0f;
            return f.sqrMagnitude > 1e-6f ? f.normalized : Vector3.forward;
        }

        static GameObject Instance(string path, Transform parent, string name)
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (model == null) throw new Exception("Model not imported: " + path);
            GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(model, parent);
            go.name = name;
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            return go;
        }

        static AnimatorController SetupPose(string fbx, string clipName)
        {
            ModelImporter mi = AssetImporter.GetAtPath(fbx) as ModelImporter;
            if (mi == null) throw new Exception("Not imported: " + fbx);
            bool changed = false;
            if (mi.animationType != ModelImporterAnimationType.Generic) { mi.animationType = ModelImporterAnimationType.Generic; changed = true; }
            if (mi.materialImportMode != ModelImporterMaterialImportMode.None) { mi.materialImportMode = ModelImporterMaterialImportMode.None; changed = true; }
            ModelImporterClipAnimation[] clips = mi.clipAnimations;
            if (clips == null || clips.Length == 0) clips = mi.defaultClipAnimations;
            if (clips != null && clips.Length > 0 && (!clips[0].loopTime || clips[0].name != clipName))
            {
                clips[0].loopTime = true;
                clips[0].name = clipName;
                mi.clipAnimations = new[] { clips[0] };
                changed = true;
            }
            if (changed) mi.SaveAndReimport();
            AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));
            if (clip == null) throw new Exception("No clip in " + fbx);
            string path = GenDir + "/" + clipName + ".controller";
            AnimatorController ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (ctrl == null) ctrl = AnimatorController.CreateAnimatorControllerAtPathWithClip(path, clip);
            else
            {
                ctrl.layers[0].stateMachine.defaultState.motion = clip;
                EditorUtility.SetDirty(ctrl);
            }
            return ctrl;
        }

        static void SetupTextures()
        {
            string nm = Dir + "/chair_2_sk_Material_nmap.png";
            TextureImporter ti = AssetImporter.GetAtPath(nm) as TextureImporter;
            if (ti != null && ti.textureType != TextureImporterType.NormalMap)
            {
                ti.textureType = TextureImporterType.NormalMap;
                ti.SaveAndReimport();
            }
        }

        static Shader Lit()
        {
            Shader s = Shader.Find("Universal Render Pipeline/Lit");
            return s != null ? s : Shader.Find("Standard");
        }

        static Material Mat(string name, Color c, float metallic, float smoothness)
        {
            string path = GenDir + "/" + name + ".mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Lit());
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Metallic", metallic);
            m.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material StoolMaterial()
        {
            Material m = Mat("RT_Stool", Color.white, 0f, 0.25f);
            Texture2D col = AssetDatabase.LoadAssetAtPath<Texture2D>(Dir + "/chair_2_sk_Material_color.png");
            Texture2D nrm = AssetDatabase.LoadAssetAtPath<Texture2D>(Dir + "/chair_2_sk_Material_nmap.png");
            Texture2D ao = AssetDatabase.LoadAssetAtPath<Texture2D>(Dir + "/chair_2_sk_Material_ao.png");
            if (col != null) m.SetTexture("_BaseMap", col);
            if (nrm != null)
            {
                m.SetTexture("_BumpMap", nrm);
                m.EnableKeyword("_NORMALMAP");
            }
            if (ao != null)
            {
                m.SetTexture("_OcclusionMap", ao);
                m.EnableKeyword("_OCCLUSIONMAP");
            }
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material GlassMat()
        {
            Material m = Mat("RT_PrismGlass", new Color(0.78f, 0.92f, 1f, 0.38f), 0f, 0.95f);
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", new Color(0.35f, 0.42f, 0.5f));
            m.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(m);
            return m;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
