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
    static class EclipseKeepSetup
    {
        const string TriggerPath = "Assets/Interaction/Editor/.run_eclipsekeep";
        const string ReportPath = "Backups/eclipsekeep_report.txt";
        const string MapScene = "Assets/Scenes/FourfoldCitadel_WithOurStuff.unity";
        const string Dir = "Assets/Interaction/Generated/EclipseKeep";
        const string JournalPath = "Assets/Resources/Dialogue/EclipseKeep_Journal.asset";
        const string RootName = "Eclipse Keep Dressing";
        const float DaisTop = 4.2f;
        const float ThroneX = 237.15f;
        const float SlabFrontZ = 18.86f;

        static EclipseKeepSetup()
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

        [MenuItem("Tools/Interaction/Build Eclipse Keep Loop Room")]
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
                Directory.CreateDirectory(Dir);
                foreach (string f in new[] { "eclipse_corona", "chalk_tally" })
                {
                    var ti = AssetImporter.GetAtPath(Dir + "/" + f + ".png") as TextureImporter;
                    if (ti != null) { ti.alphaIsTransparency = true; ti.wrapMode = TextureWrapMode.Clamp; ti.SaveAndReimport(); }
                }
                EnsureJournal(log);
                var skelImp = AssetImporter.GetAtPath("Assets/Environment/Skeleton/Skeleton_Sitting.fbx") as ModelImporter;
                if (skelImp != null && !skelImp.isReadable) { skelImp.isReadable = true; skelImp.SaveAndReimport(); log.AppendLine("skeleton mesh made readable"); }

                Scene active = SceneManager.GetActiveScene();
                ZoneMusic zmOpen = UnityEngine.Object.FindAnyObjectByType<ZoneMusic>();
                bool stale = active.path == MapScene && zmOpen != null && zmOpen.zones.Count == 0;
                if (!stale) EditorSceneManager.SaveOpenScenes();
                File.Copy(MapScene, "Backups/FourfoldCitadel_WithOurStuff_before_eclipsekeep4.unity", true);
                Scene map = EditorSceneManager.OpenScene(MapScene, OpenSceneMode.Single);
                Physics.SyncTransforms();
                GameObject old = GameObject.Find(RootName);
                if (old != null) UnityEngine.Object.DestroyImmediate(old);
                var root = new GameObject(RootName);

                Material obsidian = null, bronze = null;
                foreach (Renderer r in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include))
                {
                    if (obsidian == null && r.name.StartsWith("Throne silhouette")) obsidian = r.sharedMaterial;
                    if (bronze == null && r.name.StartsWith("Eclipse crown spike")) bronze = r.sharedMaterial;
                }
                if (obsidian == null) obsidian = Lit("EK_Obsidian", new Color(0.08f, 0.08f, 0.1f), 0.6f, Color.black);
                if (bronze == null) bronze = Lit("EK_Bronze", new Color(0.5f, 0.33f, 0.18f), 0.55f, Color.black);
                Material leather = Lit("EK_Leather", new Color(0.24f, 0.11f, 0.06f), 0.25f, Color.black);
                Material paper = Lit("EK_Paper", new Color(0.82f, 0.76f, 0.62f), 0.1f, Color.black);
                Material ash = Lit("EK_Ash", new Color(0.12f, 0.1f, 0.09f), 0.1f, Color.black);
                Material statueStone = Lit("EK_StatueStone", new Color(0.5f, 0.48f, 0.45f), 0.15f, Color.black);
                Material shardMat = Lit("EK_PrismShard", new Color(0.85f, 0.88f, 0.95f), 0.9f, new Color(0.25f, 0.25f, 0.3f));

                float seatTop = DaisTop + 0.62f;
                float seatZ = SlabFrontZ - 0.8f;
                var throne = new GameObject("Throne seat");
                throne.transform.SetParent(root.transform, false);
                Box(throne.transform, "Seat", obsidian, new Vector3(ThroneX, DaisTop + 0.31f, seatZ), new Vector3(2.4f, 0.62f, 1.6f));
                Box(throne.transform, "Arm L", obsidian, new Vector3(ThroneX - 1.32f, seatTop + 0.32f, seatZ + 0.05f), new Vector3(0.3f, 0.64f, 1.5f));
                Box(throne.transform, "Arm R", obsidian, new Vector3(ThroneX + 1.32f, seatTop + 0.32f, seatZ + 0.05f), new Vector3(0.3f, 0.64f, 1.5f));
                Box(throne.transform, "Step", obsidian, new Vector3(ThroneX, DaisTop + 0.1f, seatZ - 1.05f), new Vector3(2.8f, 0.2f, 0.5f));
                float armTop = seatTop + 0.64f;

                GameObject srcSkel = null, srcGoggles = null;
                foreach (MeshFilter mf in UnityEngine.Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include))
                {
                    if (mf.sharedMesh == null) continue;
                    if (srcSkel == null && mf.sharedMesh.name.StartsWith("Skeleton_Sitting") && mf.transform.root.name != RootName) srcSkel = mf.gameObject;
                    if (srcGoggles == null && mf.sharedMesh.name.StartsWith("InfraredGoggles") && mf.transform.root.name != RootName) srcGoggles = mf.gameObject;
                }

                Bounds sb = new Bounds(new Vector3(ThroneX, seatTop + 0.6f, seatZ), new Vector3(0.9f, 1.3f, 1.4f));
                GameObject skel = null;
                if (srcSkel != null)
                {
                    skel = UnityEngine.Object.Instantiate(srcSkel, root.transform);
                    skel.name = "Throne skeleton";
                    skel.transform.rotation = srcSkel.transform.rotation;
                    skel.transform.localScale = srcSkel.transform.lossyScale * 1.6f;
                    foreach (Collider c in skel.GetComponentsInChildren<Collider>()) UnityEngine.Object.DestroyImmediate(c);
                    skel.transform.position = new Vector3(ThroneX, seatTop + 1f, seatZ);
                    sb = BoundsOf(skel);
                    skel.transform.position += new Vector3(ThroneX - sb.center.x, seatTop - 0.02f - sb.min.y, (SlabFrontZ - 0.06f) - sb.max.z);
                    sb = BoundsOf(skel);
                    MeshFilter pmf = skel.GetComponentInChildren<MeshFilter>();
                    if (pmf != null && pmf.sharedMesh != null && pmf.sharedMesh.isReadable)
                    {
                        pmf.sharedMesh = SaveMesh(Slump(pmf, sb), "skeleton_slumped");
                        sb = BoundsOf(skel);
                        skel.transform.position += new Vector3(ThroneX - sb.center.x, seatTop - 0.02f - sb.min.y, (SlabFrontZ - 0.06f) - sb.max.z);
                        sb = BoundsOf(skel);
                        log.AppendLine("skeleton re-posed: mirrored, slumped forward, head bowed");
                    }
                    var bc = skel.AddComponent<BoxCollider>();
                    bc.center = skel.transform.InverseTransformPoint(sb.center);
                    Vector3 ls = skel.transform.lossyScale;
                    bc.size = new Vector3(sb.size.x / Mathf.Abs(ls.x), sb.size.y / Mathf.Abs(ls.y), sb.size.z / Mathf.Abs(ls.z));
                    var lt = skel.AddComponent<LookThought>();
                    lt.thought = "\"...why does he look so familiar?\"";
                    lt.distance = 4f;
                    log.AppendLine("skeleton on throne, bounds " + sb.center.ToString("F2") + " size " + sb.size.ToString("F2"));
                }
                else log.AppendLine("seated skeleton model not found");

                Mesh spikeMesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Interaction/Generated/UVRoom/Meshes/uv_pendant.asset");
                Mesh linkMesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Interaction/Generated/UVRoom/Meshes/uv_chain_link.asset");
                var pendant = new GameObject("Sunburst necklace");
                pendant.transform.SetParent(root.transform, false);
                Vector3 chest = new Vector3(ThroneX, sb.min.y + sb.size.y * 0.66f, sb.max.z - sb.size.z * 0.3f);
                if (skel != null)
                {
                    MeshFilter smf = skel.GetComponentInChildren<MeshFilter>();
                    if (smf != null && smf.sharedMesh != null && smf.sharedMesh.isReadable)
                    {
                        float lo = sb.min.y + sb.size.y * 0.6f, hi = sb.min.y + sb.size.y * 0.74f;
                        float sx = 0f, minZ = float.MaxValue; int n = 0;
                        foreach (Vector3 v in smf.sharedMesh.vertices)
                        {
                            Vector3 w = smf.transform.TransformPoint(v);
                            if (w.y < lo || w.y > hi) continue;
                            sx += w.x; n++;
                            if (w.z < minZ) minZ = w.z;
                        }
                        if (n > 0) chest = new Vector3(sx / n, sb.min.y + sb.size.y * 0.67f, minZ - 0.015f);
                        log.AppendLine("necklace anchored to ribcage from " + n + " vertices at " + chest.ToString("F3"));
                    }
                }
                pendant.transform.position = chest;
                pendant.transform.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);
                var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                disc.name = "Black sun";
                UnityEngine.Object.DestroyImmediate(disc.GetComponent<Collider>());
                disc.transform.SetParent(pendant.transform, false);
                disc.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                disc.transform.localScale = new Vector3(0.075f, 0.006f, 0.075f);
                disc.GetComponent<MeshRenderer>().sharedMaterial = obsidian;
                var rim = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                rim.name = "Rim";
                UnityEngine.Object.DestroyImmediate(rim.GetComponent<Collider>());
                rim.transform.SetParent(pendant.transform, false);
                rim.transform.localPosition = new Vector3(0f, 0f, 0.003f);
                rim.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                rim.transform.localScale = new Vector3(0.088f, 0.004f, 0.088f);
                rim.GetComponent<MeshRenderer>().sharedMaterial = bronze;
                if (spikeMesh != null)
                {
                    for (int i = 0; i < 12; i++)
                    {
                        float a = i * 30f;
                        var sp = new GameObject("Spike");
                        sp.transform.SetParent(pendant.transform, false);
                        float rad = 0.064f;
                        sp.transform.localPosition = new Vector3(Mathf.Sin(a * Mathf.Deg2Rad) * rad, Mathf.Cos(a * Mathf.Deg2Rad) * rad, 0f);
                        sp.transform.localRotation = Quaternion.Euler(0f, 0f, -a);
                        float len = (i % 3 == 0) ? 0.028f : 0.022f;
                        sp.transform.localScale = new Vector3(0.009f, len, 0.006f);
                        sp.AddComponent<MeshFilter>().sharedMesh = spikeMesh;
                        sp.AddComponent<MeshRenderer>().sharedMaterial = bronze;
                    }
                }
                if (linkMesh != null)
                {
                    foreach (float side in new[] { -1f, 1f })
                    {
                        Vector3 a0 = chest + new Vector3(0f, 0.09f, 0f);
                        Vector3 a1 = chest + new Vector3(side * 0.11f, 0.3f, 0.06f);
                        for (int k = 0; k < 9; k++)
                        {
                            float t = k / 8f;
                            var lk = new GameObject("Chain link");
                            lk.transform.SetParent(pendant.transform, true);
                            lk.transform.position = Vector3.Lerp(a0, a1, t) + new Vector3(0f, 0f, -Mathf.Sin(t * Mathf.PI) * 0.02f);
                            lk.transform.rotation = Quaternion.LookRotation(Vector3.forward, (a1 - a0).normalized) * Quaternion.Euler(0f, k % 2 == 0 ? 0f : 90f, 0f);
                            lk.transform.localScale = Vector3.one * 0.016f;
                            lk.AddComponent<MeshFilter>().sharedMesh = linkMesh;
                            lk.AddComponent<MeshRenderer>().sharedMaterial = bronze;
                        }
                    }
                }

                if (srcGoggles != null)
                {
                    var gg = UnityEngine.Object.Instantiate(srcGoggles, root.transform);
                    gg.name = "His goggles";
                    foreach (MonoBehaviour mb in gg.GetComponents<MonoBehaviour>()) UnityEngine.Object.DestroyImmediate(mb);
                    foreach (Collider c in gg.GetComponentsInChildren<Collider>()) UnityEngine.Object.DestroyImmediate(c);
                    gg.transform.localScale = srcGoggles.transform.lossyScale * 1.3f;
                    gg.transform.position = new Vector3(ThroneX + 1.32f, armTop + 0.06f, seatZ - 0.25f);
                    gg.transform.rotation = Quaternion.Euler(0f, 205f, 0f);
                    var gb = gg.AddComponent<BoxCollider>();
                    Bounds bb = BoundsOf(gg);
                    gb.center = gg.transform.InverseTransformPoint(bb.center);
                    Vector3 gls = gg.transform.lossyScale;
                    gb.size = new Vector3(bb.size.x / Mathf.Abs(gls.x), bb.size.y / Mathf.Abs(gls.y), bb.size.z / Mathf.Abs(gls.z)) * 1.6f;
                    var glt = gg.AddComponent<LookThought>();
                    glt.thought = "\"Those are my goggles. The strap is torn in the same place.\"";
                    glt.distance = 3.2f;
                    log.AppendLine("goggles copy on the throne arm");
                }
                else log.AppendLine("goggles model not found");

                var book = new GameObject("Journal");
                book.transform.SetParent(root.transform, false);
                Vector3 lp = new Vector3(ThroneX - 0.86f, seatTop + 0.02f, seatZ - 0.15f);
                book.transform.position = lp;
                book.transform.rotation = Quaternion.Euler(0f, -14f, 0f);
                foreach (float side in new[] { -1f, 1f })
                {
                    var cov = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    cov.name = "Cover";
                    UnityEngine.Object.DestroyImmediate(cov.GetComponent<Collider>());
                    cov.transform.SetParent(book.transform, false);
                    cov.transform.localPosition = new Vector3(side * 0.105f, 0.006f, 0f);
                    cov.transform.localRotation = Quaternion.Euler(0f, 0f, side * -4f);
                    cov.transform.localScale = new Vector3(0.21f, 0.012f, 0.28f);
                    cov.GetComponent<MeshRenderer>().sharedMaterial = leather;
                    var pg = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    pg.name = "Pages";
                    UnityEngine.Object.DestroyImmediate(pg.GetComponent<Collider>());
                    pg.transform.SetParent(book.transform, false);
                    pg.transform.localPosition = new Vector3(side * 0.1f, 0.02f, 0f);
                    pg.transform.localRotation = Quaternion.Euler(0f, 0f, side * -4f);
                    pg.transform.localScale = new Vector3(0.19f, 0.016f, 0.26f);
                    pg.GetComponent<MeshRenderer>().sharedMaterial = paper;
                }
                var quill = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                quill.name = "Pencil stub";
                UnityEngine.Object.DestroyImmediate(quill.GetComponent<Collider>());
                quill.transform.SetParent(book.transform, false);
                quill.transform.localPosition = new Vector3(0.05f, 0.035f, -0.02f);
                quill.transform.localRotation = Quaternion.Euler(0f, 30f, 90f);
                quill.transform.localScale = new Vector3(0.008f, 0.05f, 0.008f);
                quill.GetComponent<MeshRenderer>().sharedMaterial = ash;
                var bcol = book.AddComponent<BoxCollider>();
                bcol.center = new Vector3(0f, 0.05f, 0f);
                bcol.size = new Vector3(0.6f, 0.2f, 0.45f);
                var reader = book.AddComponent<JournalReader>();
                reader.journal = AssetDatabase.LoadAssetAtPath<JournalSet>(JournalPath);
                reader.useDistance = 3f;
                var candle = new GameObject("Journal light");
                candle.transform.SetParent(root.transform, false);
                candle.transform.position = lp + new Vector3(0f, 0.7f, -0.5f);
                Light cl = candle.AddComponent<Light>();
                cl.type = LightType.Point; cl.color = new Color(1f, 0.72f, 0.42f); cl.intensity = 0.5f; cl.range = 1.6f; cl.shadows = LightShadows.None;

                var pile = new GameObject("Left from earlier loops");
                pile.transform.SetParent(root.transform, false);
                var rng = new System.Random(8);
                MirrorPickup srcMirror = UnityEngine.Object.FindAnyObjectByType<MirrorPickup>(FindObjectsInactive.Include);
                if (srcMirror != null)
                {
                    var mc = UnityEngine.Object.Instantiate(srcMirror.gameObject, pile.transform);
                    mc.name = "Old mirror";
                    foreach (MonoBehaviour mb in mc.GetComponentsInChildren<MonoBehaviour>()) UnityEngine.Object.DestroyImmediate(mb);
                    foreach (Collider c in mc.GetComponentsInChildren<Collider>()) UnityEngine.Object.DestroyImmediate(c);
                    foreach (Light l2 in mc.GetComponentsInChildren<Light>()) UnityEngine.Object.DestroyImmediate(l2.gameObject);
                    foreach (ParticleSystem p in mc.GetComponentsInChildren<ParticleSystem>()) UnityEngine.Object.DestroyImmediate(p.gameObject);
                    mc.transform.position = new Vector3(ThroneX - 3.6f, DaisTop + 0.05f, 16.3f);
                    mc.transform.rotation = Quaternion.Euler(0f, 30f, 0f) * Quaternion.Euler(-74f, 0f, 0f);
                    Bounds mb2 = BoundsOf(mc);
                    mc.transform.position += Vector3.up * (DaisTop + 0.02f - mb2.min.y);
                    log.AppendLine("old mirror placed");
                }
                if (spikeMesh != null)
                    for (int i = 0; i < 9; i++)
                    {
                        var sh = new GameObject("Prism shard");
                        sh.transform.SetParent(pile.transform, false);
                        sh.transform.position = new Vector3(ThroneX + 3.1f + (float)(rng.NextDouble() - 0.5) * 1.1f, DaisTop + 0.03f, 15.6f + (float)(rng.NextDouble() - 0.5) * 0.9f);
                        sh.transform.rotation = Quaternion.Euler((float)rng.NextDouble() * 80f + 50f, (float)rng.NextDouble() * 360f, 0f);
                        float s = 0.05f + (float)rng.NextDouble() * 0.07f;
                        sh.transform.localScale = new Vector3(s * 0.6f, s, s * 0.4f);
                        sh.AddComponent<MeshFilter>().sharedMesh = spikeMesh;
                        sh.AddComponent<MeshRenderer>().sharedMaterial = shardMat;
                    }
                GameObject srcTorch = null;
                foreach (Transform t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
                    if (t.name.StartsWith("Torch • supplied design") && t.root.name != RootName) { srcTorch = t.gameObject; break; }
                if (srcTorch != null)
                {
                    var tc = UnityEngine.Object.Instantiate(srcTorch, pile.transform);
                    tc.name = "Burnt-out torch";
                    foreach (SunkenPrism.TorchFlame f in tc.GetComponentsInChildren<SunkenPrism.TorchFlame>(true)) UnityEngine.Object.DestroyImmediate(f.gameObject);
                    foreach (Light l2 in tc.GetComponentsInChildren<Light>(true)) UnityEngine.Object.DestroyImmediate(l2.gameObject);
                    foreach (MonoBehaviour mb in tc.GetComponentsInChildren<MonoBehaviour>(true)) UnityEngine.Object.DestroyImmediate(mb);
                    foreach (Collider c in tc.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(c);
                    foreach (Renderer r in tc.GetComponentsInChildren<Renderer>(true)) r.sharedMaterial = ash;
                    tc.transform.rotation = Quaternion.Euler(0f, 52f, 88f);
                    tc.transform.position = new Vector3(ThroneX + 4.4f, DaisTop + 0.6f, 16.8f);
                    Bounds tb = BoundsOf(tc);
                    tc.transform.position += Vector3.up * (DaisTop + 0.02f - tb.min.y);
                    log.AppendLine("burnt torch placed");
                }
                var rocks = new List<Mesh>();
                for (int i = 0; i < 8; i++) { Mesh rm = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Interaction/Generated/LunarStyle/Meshes/rock_" + i + ".asset"); if (rm != null) rocks.Add(rm); }
                if (rocks.Count > 0)
                    for (int i = 0; i < 4; i++)
                    {
                        var fr = new GameObject("Statue fragment");
                        fr.transform.SetParent(pile.transform, false);
                        float s = i == 0 ? 0.42f : 0.18f + (float)rng.NextDouble() * 0.14f;
                        fr.transform.position = new Vector3(ThroneX - 4.6f + (float)(rng.NextDouble() - 0.5) * 1.2f, DaisTop + s * 0.3f, 14.9f + (float)(rng.NextDouble() - 0.5) * 1.0f);
                        fr.transform.rotation = Quaternion.Euler((float)rng.NextDouble() * 40f, (float)rng.NextDouble() * 360f, 0f);
                        fr.transform.localScale = new Vector3(s, s * 1.3f, s);
                        fr.AddComponent<MeshFilter>().sharedMesh = rocks[rng.Next(rocks.Count)];
                        fr.AddComponent<MeshRenderer>().sharedMaterial = statueStone;
                    }
                var oldBaton = new GameObject("Old UV baton");
                oldBaton.transform.SetParent(pile.transform, false);
                oldBaton.transform.position = new Vector3(ThroneX + 1.6f, DaisTop + 0.04f, 15.9f);
                oldBaton.transform.rotation = Quaternion.Euler(0f, 70f, 90f);
                var bg = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                UnityEngine.Object.DestroyImmediate(bg.GetComponent<Collider>());
                bg.transform.SetParent(oldBaton.transform, false);
                bg.transform.localPosition = new Vector3(0f, -0.12f, 0f);
                bg.transform.localScale = new Vector3(0.05f, 0.08f, 0.05f);
                bg.GetComponent<MeshRenderer>().sharedMaterial = Lit("EK_BatonGrip", new Color(0.04f, 0.04f, 0.05f), 0.35f, Color.black);
                var bt = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                UnityEngine.Object.DestroyImmediate(bt.GetComponent<Collider>());
                bt.transform.SetParent(oldBaton.transform, false);
                bt.transform.localPosition = new Vector3(0f, 0.11f, 0f);
                bt.transform.localScale = new Vector3(0.042f, 0.15f, 0.042f);
                bt.GetComponent<MeshRenderer>().sharedMaterial = Lit("EK_BatonTube", new Color(0.7f, 0.7f, 0.74f), 0.5f, Color.black);

                Texture2D tally = AssetDatabase.LoadAssetAtPath<Texture2D>(Dir + "/chalk_tally.png");
                Material chalk = Chalk(tally);
                int tallies = 0;
                var tallyRoot = new GameObject("Tally marks");
                tallyRoot.transform.SetParent(root.transform, false);
                for (int i = 0; i < 40 && tallies < 14; i++)
                {
                    float a = Mathf.Lerp(-80f, 80f, (float)rng.NextDouble());
                    Vector3 dir = Quaternion.Euler(0f, a, 0f) * Vector3.forward;
                    if (Mathf.Abs(a) < 18f) dir = Quaternion.Euler(0f, a < 0 ? -55f : 55f, 0f) * Vector3.forward;
                    Vector3 o = new Vector3(ThroneX, 3.5f + (float)rng.NextDouble() * 4.5f, 8f);
                    if (!Physics.Raycast(o, dir, out RaycastHit hit, 40f, ~0, QueryTriggerInteraction.Ignore)) continue;
                    if (hit.collider.transform.root.name == RootName) continue;
                    if (hit.distance < 6f || hit.point.y < 3.4f) continue;
                    string hn = hit.collider.name;
                    if (hn.StartsWith("Royal dais") || hn.StartsWith("Keep dais") || hn.StartsWith("Regional puzzle") || hn.StartsWith("Side stair") || hn.StartsWith("Throne")) continue;
                    if (Mathf.Abs(hit.normal.y) > 0.3f) continue;
                    var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    q.name = "Tally";
                    UnityEngine.Object.DestroyImmediate(q.GetComponent<Collider>());
                    q.transform.SetParent(tallyRoot.transform, false);
                    q.transform.position = hit.point + hit.normal * 0.03f;
                    q.transform.rotation = Quaternion.LookRotation(-hit.normal, Vector3.up) * Quaternion.Euler(0f, 0f, (float)(rng.NextDouble() - 0.5) * 10f);
                    float s = 0.7f + (float)rng.NextDouble() * 0.6f;
                    q.transform.localScale = new Vector3(s * 2f, s, 1f);
                    q.GetComponent<MeshRenderer>().sharedMaterial = chalk;
                    q.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    tallies++;
                }
                log.AppendLine("tally mark groups " + tallies);

                Texture2D corona = AssetDatabase.LoadAssetAtPath<Texture2D>(Dir + "/eclipse_corona.png");
                Shader glowSh = Shader.Find("CasaFX/UVGlow");
                if (glowSh != null && corona != null)
                {
                    var cm = Mat("EK_Corona", glowSh, m => { m.SetTexture("_MainTex", corona); m.SetColor("_Color", new Color(1f, 0.8f, 0.55f)); m.SetFloat("_Intensity", 0.8f); m.SetFloat("_Pulse", 0.1f); m.SetFloat("_Speed", 0.6f); });
                    var cq = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    cq.name = "Eclipse corona";
                    UnityEngine.Object.DestroyImmediate(cq.GetComponent<Collider>());
                    cq.transform.SetParent(root.transform, false);
                    cq.transform.position = new Vector3(ThroneX, 11f, 18.75f);
                    cq.transform.rotation = Quaternion.identity;
                    cq.transform.localScale = Vector3.one * 10f;
                    cq.GetComponent<MeshRenderer>().sharedMaterial = cm;
                }

                var warm = new GameObject("Corona light");
                warm.transform.SetParent(root.transform, false);
                warm.transform.position = new Vector3(ThroneX, 10f, 16.5f);
                Light wl = warm.AddComponent<Light>();
                wl.type = LightType.Point; wl.color = new Color(1f, 0.78f, 0.52f); wl.intensity = 3f; wl.range = 11f; wl.shadows = LightShadows.None;
                var spot = new GameObject("Throne spotlight");
                spot.transform.SetParent(root.transform, false);
                spot.transform.position = new Vector3(ThroneX, 13f, 11.5f);
                spot.transform.rotation = Quaternion.LookRotation(chest - spot.transform.position, Vector3.up);
                Light sl = spot.AddComponent<Light>();
                sl.type = LightType.Spot; sl.color = new Color(1f, 0.9f, 0.75f); sl.intensity = 9f; sl.range = 20f; sl.spotAngle = 26f; sl.innerSpotAngle = 12f; sl.shadows = LightShadows.Soft;

                int hiddenLabels = 0;
                foreach (Renderer r in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include))
                {
                    if (!(r.name.StartsWith("Region socket inscription") || r.name.StartsWith("Puzzle boundary engraving"))) continue;
                    if (r.bounds.center.x < 210f) continue;
                    r.enabled = false;
                    hiddenLabels++;
                }
                log.AppendLine("region 4 labels hidden " + hiddenLabels);

                EditorSceneManager.MarkSceneDirty(map);
                EditorSceneManager.SaveScene(map);
                ZoneMusic zm = UnityEngine.Object.FindAnyObjectByType<ZoneMusic>();
                log.AppendLine("zone music areas: " + (zm != null ? zm.zones.Count.ToString() : "none"));

                Snapshot(new Vector3(ThroneX, 6.2f, 6f), new Vector3(ThroneX, 6.2f, 18.5f), "Backups/eclipsekeep_0.png", 60f);
                Snapshot(new Vector3(ThroneX + 0.6f, seatTop + 1.1f, seatZ - 2.4f), chest, "Backups/eclipsekeep_1.png", 55f);
                Snapshot(lp + new Vector3(0.3f, 0.9f, -1.3f), lp, "Backups/eclipsekeep_2.png", 55f);
                Snapshot(new Vector3(222f, 3.8f, -6f), new Vector3(ThroneX, 6.5f, 18f), "Backups/eclipsekeep_3.png", 65f);
                log.AppendLine("DONE");
            }
            catch (Exception e)
            {
                log.AppendLine("FAILED: " + e);
            }
            File.WriteAllText(ReportPath, log.ToString());
        }

        static void EnsureJournal(StringBuilder log)
        {
            if (AssetDatabase.LoadAssetAtPath<JournalSet>(JournalPath) != null) { log.AppendLine("journal file exists, left untouched"); return; }
            Directory.CreateDirectory("Assets/Resources/Dialogue");
            var j = ScriptableObject.CreateInstance<JournalSet>();
            j.title = "Journal";
            j.pages.Add(new JournalSet.Page { heading = "The first day", body = "Found the door at the edge of the citadel. It opened before I touched it.\n\nI don't remember the climb. I remember the door." });
            j.pages.Add(new JournalSet.Page { heading = "The first day", body = "A hall under a painted moon. Three mirrors, one torch, one prism.\n\nI set the mirrors. When I turned around, one of them had moved.\n\nI bet I placed it right." });
            j.pages.Add(new JournalSet.Page { heading = "The first day", body = "A ghost sat in the passage. He spoke like he had known me for years.\n\nHe said this is the room behind my eyes." });
            j.pages.Add(new JournalSet.Page { heading = "The first day", body = "The statues were warm. The goggles showed me which one.\n\nThe last one cracked. There was a body inside, wearing my helmet." });
            j.pages.Add(new JournalSet.Page { heading = "The first day", body = "A violet room. Footprints only the light could find. They led to a red button.\n\nThe prints were my size." });
            j.pages.Add(new JournalSet.Page { heading = "The first day", body = "There is someone on the throne. He has my goggles. He has this journal.\n\nThe handwriting is mine.\n\nIt is the same day. It is always the same day." });
            j.thoughtAfterLastPage = "\"It's my handwriting.\"";
            AssetDatabase.CreateAsset(j, JournalPath);
            AssetDatabase.SaveAssets();
            log.AppendLine("journal file created with " + j.pages.Count + " pages");
        }

        static Mesh Slump(MeshFilter mf, Bounds sb)
        {
            Mesh src = mf.sharedMesh;
            Transform t = mf.transform;
            Vector3[] v = src.vertices;
            float cx = sb.center.x;
            float pelvisY = sb.min.y + sb.size.y * 0.42f;
            float neckY = sb.min.y + sb.size.y * 0.8f;
            float backZ = sb.max.z - sb.size.z * 0.18f;
            Quaternion lean = Quaternion.Euler(-16f, 0f, 9f);
            Quaternion droop = Quaternion.Euler(-30f, 0f, 14f);
            for (int i = 0; i < v.Length; i++)
            {
                Vector3 w = t.TransformPoint(v[i]);
                w.x = cx - (w.x - cx);
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((w.y - pelvisY) / (sb.size.y * 0.12f)));
                if (k > 0f)
                {
                    Vector3 pv = new Vector3(cx, pelvisY, backZ);
                    w = pv + Quaternion.Slerp(Quaternion.identity, lean, k) * (w - pv);
                }
                Vector3 neck = new Vector3(cx, pelvisY, backZ) + lean * (new Vector3(cx, neckY, backZ) - new Vector3(cx, pelvisY, backZ));
                float h = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((w.y - (neck.y - 0.02f)) / (sb.size.y * 0.06f)));
                if (h > 0f && k > 0.99f) w = neck + Quaternion.Slerp(Quaternion.identity, droop, h) * (w - neck);
                v[i] = t.InverseTransformPoint(w);
            }
            var m = new Mesh { name = "Skeleton slumped" };
            m.indexFormat = src.indexFormat;
            m.vertices = v;
            m.uv = src.uv;
            m.subMeshCount = src.subMeshCount;
            for (int sm = 0; sm < src.subMeshCount; sm++)
            {
                int[] tri = src.GetTriangles(sm);
                for (int i = 0; i < tri.Length; i += 3) { int tmp = tri[i + 1]; tri[i + 1] = tri[i + 2]; tri[i + 2] = tmp; }
                m.SetTriangles(tri, sm);
            }
            m.RecalculateNormals();
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

        static Bounds BoundsOf(GameObject g)
        {
            Renderer[] rs = g.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return new Bounds(g.transform.position, Vector3.one * 0.2f);
            Bounds b = rs[0].bounds;
            foreach (Renderer r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        static GameObject Box(Transform parent, string name, Material m, Vector3 pos, Vector3 size)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            g.name = name;
            g.transform.SetParent(parent, false);
            g.transform.position = pos;
            g.transform.localScale = size;
            g.GetComponent<MeshRenderer>().sharedMaterial = m;
            return g;
        }

        static Material Lit(string name, Color c, float smooth, Color emission)
        {
            return Mat(name, Shader.Find("Universal Render Pipeline/Lit"), m =>
            {
                m.SetColor("_BaseColor", c);
                m.SetFloat("_Smoothness", smooth);
                m.SetFloat("_Metallic", 0f);
                if (emission.maxColorComponent > 0.001f) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", emission); }
            });
        }

        static Material Chalk(Texture2D tex)
        {
            return Mat("EK_Chalk", Shader.Find("Universal Render Pipeline/Unlit"), m =>
            {
                m.SetTexture("_BaseMap", tex);
                m.SetColor("_BaseColor", new Color(0.82f, 0.8f, 0.74f, 0.75f));
                m.SetFloat("_Surface", 1f);
                m.SetFloat("_Blend", 0f);
                m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_ZWrite", 0f);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.SetOverrideTag("RenderType", "Transparent");
                m.renderQueue = 3000;
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

        static void Snapshot(Vector3 from, Vector3 at, string path, float fov)
        {
            var go = new GameObject("Snap Cam");
            Camera cam = go.AddComponent<Camera>();
            cam.fieldOfView = fov;
            cam.nearClipPlane = 0.02f;
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

