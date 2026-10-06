using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Video;

namespace FPCharacter.EditorTools
{
    [InitializeOnLoad]
    static class EclipseRemainsSetup
    {
        const string TriggerPath = "Assets/Interaction/Editor/.run_eclipseremains";
        const string ReportPath = "Backups/eclipseremains_report.txt";
        const string MapScene = "Assets/Scenes/FourfoldCitadel_WithOurStuff.unity";
        const string Dir = "Assets/Interaction/Generated/EclipseKeep";
        const string RootName = "Eclipse Keep Remains";
        const float X0 = 211f, X1 = 263f, Z0 = -26f, Z1 = 26f, ThroneX = 237.15f;

        static EclipseRemainsSetup()
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

        [MenuItem("Tools/Interaction/Eclipse Keep Remains And NPC3")]
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
                foreach (string fbx in new[] { "SkeletonRemains", "SkeletonRemains_Grave", "BonePile" })
                {
                    var imp = AssetImporter.GetAtPath("Assets/Environment/Skeleton/" + fbx + ".fbx") as ModelImporter;
                    if (imp != null && !imp.isReadable) { imp.isReadable = true; imp.SaveAndReimport(); }
                }
                Scene active = SceneManager.GetActiveScene();
                ZoneMusic zmOpen = UnityEngine.Object.FindAnyObjectByType<ZoneMusic>();
                bool stale = active.path == MapScene && zmOpen != null && zmOpen.zones.Count == 0;
                if (!stale) EditorSceneManager.SaveOpenScenes();
                File.Copy(MapScene, "Backups/FourfoldCitadel_WithOurStuff_before_eclipseremains3.unity", true);
                Scene map = EditorSceneManager.OpenScene(MapScene, OpenSceneMode.Single);
                Physics.SyncTransforms();
                GameObject old = GameObject.Find(RootName);
                if (old != null) UnityEngine.Object.DestroyImmediate(old);
                var root = new GameObject(RootName);

                float floorY = 2f;
                if (Physics.Raycast(new Vector3(ThroneX, 6f, -12f), Vector3.down, out RaycastHit fh, 10f, ~0, QueryTriggerInteraction.Ignore)) floorY = fh.point.y;

                Material bronze = null, obsidian = null;
                foreach (Renderer r in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include))
                {
                    if (bronze == null && r.name.StartsWith("Eclipse crown spike")) bronze = r.sharedMaterial;
                    if (obsidian == null && r.name.StartsWith("Throne silhouette")) obsidian = r.sharedMaterial;
                }
                Mesh spike = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Interaction/Generated/UVRoom/Meshes/uv_pendant.asset");
                Mesh link = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Interaction/Generated/UVRoom/Meshes/uv_chain_link.asset");
                Material chalk = AssetDatabase.LoadAssetAtPath<Material>(Dir + "/EK_Chalk.mat");
                var rng = new System.Random(77);

                var tallyRoot = new GameObject("Tally marks (hall)");
                tallyRoot.transform.SetParent(root.transform, false);
                int tallies = 0;
                Vector3[] origins = { new Vector3(222f, 0f, -14f), new Vector3(222f, 0f, 14f), new Vector3(252f, 0f, -14f), new Vector3(252f, 0f, 14f), new Vector3(ThroneX, 0f, -18f), new Vector3(ThroneX, 0f, 0f), new Vector3(216f, 0f, 0f), new Vector3(258f, 0f, 0f) };
                for (int i = 0; i < 400 && tallies < 70; i++)
                {
                    Vector3 o = origins[i % origins.Length];
                    o.y = floorY + 1.1f + (float)rng.NextDouble() * 5.5f;
                    float a = (float)rng.NextDouble() * 360f;
                    Vector3 dir = Quaternion.Euler(0f, a, 0f) * Vector3.forward;
                    if (!Physics.Raycast(o, dir, out RaycastHit hit, 30f, ~0, QueryTriggerInteraction.Ignore)) continue;
                    if (Mathf.Abs(hit.normal.y) > 0.3f || hit.distance < 2f) continue;
                    string hn = hit.collider.transform.root.name;
                    if (hn == RootName || hn.StartsWith("Eclipse Keep Dressing") || hn.StartsWith("Eclipse Keep Atmosphere")) continue;
                    if (hit.point.x < X0 - 1f || hit.point.x > X1 + 1f || hit.point.z < Z0 - 1f || hit.point.z > Z1 + 1f) continue;
                    bool tooClose = false;
                    foreach (Transform t in tallyRoot.transform) if ((t.position - hit.point).sqrMagnitude < 1.4f) { tooClose = true; break; }
                    if (tooClose) continue;
                    var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    q.name = "Tally";
                    UnityEngine.Object.DestroyImmediate(q.GetComponent<Collider>());
                    q.transform.SetParent(tallyRoot.transform, false);
                    q.transform.position = hit.point + hit.normal * 0.03f;
                    q.transform.rotation = Quaternion.LookRotation(-hit.normal, Vector3.up) * Quaternion.Euler(0f, 0f, (float)(rng.NextDouble() - 0.5) * 14f);
                    float s = 0.4f + (float)rng.NextDouble() * 0.7f;
                    float wide = 1f + (float)rng.NextDouble() * 1.4f;
                    q.transform.localScale = new Vector3(s * wide, s * 0.5f, 1f);
                    var mr = q.GetComponent<MeshRenderer>();
                    mr.sharedMaterial = chalk;
                    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    tallies++;
                }
                log.AppendLine("tally groups across the hall: " + tallies);

                GameObject remainsA = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Interaction/Prefabs/SkeletonRemains.prefab");
                GameObject remainsB = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Interaction/Prefabs/SkeletonRemains_Grave.prefab");
                GameObject pile = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Environment/Skeleton/BonePile.fbx");
                Mesh slumped = AssetDatabase.LoadAssetAtPath<Mesh>(Dir + "/Meshes/skeleton_slumped.asset");
                GameObject seatedSrc = null;
                foreach (MeshFilter mf in UnityEngine.Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include))
                    if (mf.sharedMesh != null && mf.sharedMesh.name.StartsWith("Skeleton_Sitting") && mf.transform.root.name != RootName) { seatedSrc = mf.gameObject; break; }

                var bones = new GameObject("Bones and skeletons");
                bones.transform.SetParent(root.transform, false);
                var placed = new List<Vector3>();
                int lying = 0, seated = 0, piles = 0, necklaces = 0;
                for (int i = 0; i < 600 && (lying < 9 || piles < 10); i++)
                {
                    Vector3 p = new Vector3(Mathf.Lerp(X0 + 1.5f, X1 - 1.5f, (float)rng.NextDouble()), 0f, Mathf.Lerp(Z0 + 1.5f, Z1 - 1.5f, (float)rng.NextDouble()));
                    if (Mathf.Abs(p.x - ThroneX) < 2.6f && p.z < 6f) continue;
                    if (p.x > 226f && p.x < 248.5f && p.z > 3.5f) continue;
                    if (p.x < 216f && Mathf.Abs(p.z) < 4.5f) continue;
                    bool near = false;
                    foreach (Vector3 q in placed) if ((q - p).sqrMagnitude < 9f) { near = true; break; }
                    if (near) continue;
                    if (!Physics.Raycast(new Vector3(p.x, floorY + 6f, p.z), Vector3.down, out RaycastHit h, 10f, ~0, QueryTriggerInteraction.Ignore)) continue;
                    if (Mathf.Abs(h.point.y - floorY) > 0.25f) continue;
                    if (Physics.CheckSphere(h.point + Vector3.up * 0.8f, 0.55f, ~0, QueryTriggerInteraction.Ignore)) continue;
                    p = h.point;
                    placed.Add(p);
                    bool doPile = piles < 10 && (lying >= 9 || rng.NextDouble() < 0.5);
                    GameObject src = doPile ? pile : (rng.NextDouble() < 0.5 ? remainsA : remainsB);
                    if (src == null) continue;
                    var g = (GameObject)PrefabUtility.InstantiatePrefab(src);
                    if (g == null) g = UnityEngine.Object.Instantiate(src);
                    g.transform.SetParent(bones.transform, true);
                    g.name = doPile ? "Bone pile" : "Old skeleton";
                    g.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f) * g.transform.rotation;
                    g.transform.position = p;
                    Bounds b = BoundsOf(g);
                    g.transform.position += Vector3.up * (p.y - b.min.y);
                    foreach (Collider c in g.GetComponentsInChildren<Collider>()) UnityEngine.Object.DestroyImmediate(c);
                    if (doPile) { piles++; continue; }
                    lying++;
                    if (AddLyingNecklace(g, bronze, obsidian, spike, link, rng)) necklaces++;
                }
                if (seatedSrc != null && slumped != null)
                {
                    Vector3[] seats = { new Vector3(215.5f, 0f, -20f), new Vector3(259f, 0f, -20f), new Vector3(259f, 0f, 19f) };
                    foreach (Vector3 s in seats)
                    {
                        Vector3 dirToWall = new Vector3(s.x < ThroneX ? -1f : 1f, 0f, 0f);
                        if (!Physics.Raycast(new Vector3(s.x, floorY + 1f, s.z), dirToWall, out RaycastHit wh, 8f, ~0, QueryTriggerInteraction.Ignore)) continue;
                        var g = UnityEngine.Object.Instantiate(seatedSrc, bones.transform);
                        g.name = "Skeleton against the wall";
                        g.GetComponentInChildren<MeshFilter>().sharedMesh = slumped;
                        foreach (Collider c in g.GetComponentsInChildren<Collider>()) UnityEngine.Object.DestroyImmediate(c);
                        foreach (MonoBehaviour mb in g.GetComponentsInChildren<MonoBehaviour>()) UnityEngine.Object.DestroyImmediate(mb);
                        g.transform.localScale = seatedSrc.transform.lossyScale * 1.6f;
                        g.transform.rotation = Quaternion.LookRotation(-dirToWall, Vector3.up) * Quaternion.Euler(0f, 180f, 0f) * Quaternion.Inverse(Quaternion.LookRotation(Vector3.forward)) * seatedSrc.transform.rotation;
                        g.transform.position = new Vector3(wh.point.x, floorY, s.z);
                        Bounds b = BoundsOf(g);
                        Vector3 back = dirToWall * (Mathf.Abs(dirToWall.x) > 0f ? (dirToWall.x > 0 ? b.max.x : b.min.x) : 0f);
                        g.transform.position += new Vector3(wh.point.x - 0.05f * dirToWall.x - (dirToWall.x > 0 ? b.max.x : b.min.x), floorY - b.min.y, 0f);
                        if (AddSeatedNecklace(g, bronze, obsidian, spike, link)) necklaces++;
                        seated++;
                    }
                }
                log.AppendLine("lying skeletons " + lying + ", seated " + seated + ", bone piles " + piles + ", necklaces " + necklaces);

                GameObject oldNpc = GameObject.Find("Ghost_NPC (Void Passage)");
                if (oldNpc != null) UnityEngine.Object.DestroyImmediate(oldNpc);
                GameObject ghostPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Interaction/Prefabs/Ghost_NPC.prefab");
                if (ghostPrefab != null)
                {
                    var npc = (GameObject)PrefabUtility.InstantiatePrefab(ghostPrefab);
                    npc.name = "Ghost_NPC (Void Passage)";
                    Vector3 np = new Vector3(203.5f, 0f, -2.2f);
                    np.y = 0f;
                    var floorHits = new List<string>();
                    float bestY = float.MinValue;
                    foreach (float zz in new[] { 0f, -1f, 1f, -2f })
                        if (Physics.Raycast(new Vector3(np.x, 6f, zz), Vector3.down, out RaycastHit nh, 12f, ~0, QueryTriggerInteraction.Ignore))
                        {
                            floorHits.Add(zz + ":" + nh.point.y.ToString("F2") + " " + nh.collider.name);
                            if (nh.point.y > bestY && nh.point.y < 4f) bestY = nh.point.y;
                        }
                    if (bestY > float.MinValue) np.y = bestY;
                    log.AppendLine("passage floor samples: " + string.Join(" | ", floorHits));
                    GameObject refNpc = GameObject.Find("Ghost_NPC");
                    float yOff = refNpc != null && Physics.Raycast(refNpc.transform.position + Vector3.up * 2f, Vector3.down, out RaycastHit rh, 6f, ~0, QueryTriggerInteraction.Ignore) ? refNpc.transform.position.y - rh.point.y : 0f;
                    npc.transform.position = np + Vector3.up * yOff;
                    npc.transform.rotation = Quaternion.Euler(0f, 270f, 0f);
                    Bounds fb = BoundsOf(npc);
                    npc.transform.position += Vector3.up * (np.y - fb.min.y);
                    var vp = npc.GetComponent<VideoPlayer>();
                    if (vp == null) vp = npc.AddComponent<VideoPlayer>();
                    vp.playOnAwake = false;
                    var cs = npc.GetComponent<NPCCutscene>();
                    if (cs == null) cs = npc.AddComponent<NPCCutscene>();
                    cs.videoFileName = "npc3cutscene.mp4";
                    cs.talkRadius = 2.5f;
                    if (npc.GetComponent<GhostIdleLife>() == null) npc.AddComponent<GhostIdleLife>();
                    var qm = npc.GetComponent<QuestMarker>();
                    if (qm == null) qm = npc.AddComponent<QuestMarker>();
                    Bounds nb = BoundsOf(npc);
                    var mq = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    mq.name = "Quest marker";
                    UnityEngine.Object.DestroyImmediate(mq.GetComponent<Collider>());
                    mq.transform.SetParent(npc.transform, true);
                    mq.transform.position = new Vector3(nb.center.x, nb.max.y + 0.4f, nb.center.z);
                    Vector3 ls = npc.transform.lossyScale;
                    mq.transform.localScale = new Vector3(0.42f / Mathf.Max(0.001f, ls.x), 0.42f / Mathf.Max(0.001f, ls.y), 1f / Mathf.Max(0.001f, ls.z));
                    var mmr = mq.GetComponent<MeshRenderer>();
                    mmr.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Interaction/Generated/Assist/QuestMarker.mat");
                    mmr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    qm.npc = cs;
                    qm.marker = mq.transform;
                    EditorUtility.SetDirty(cs);
                    log.AppendLine("npc3 ghost placed at " + npc.transform.position + " with npc3cutscene.mp4");
                }
                else log.AppendLine("Ghost_NPC prefab not found");

                EditorSceneManager.MarkSceneDirty(map);
                EditorSceneManager.SaveScene(map);
                ZoneMusic zm = UnityEngine.Object.FindAnyObjectByType<ZoneMusic>();
                log.AppendLine("zone music areas: " + (zm != null ? zm.zones.Count.ToString() : "none"));
                Snapshot(new Vector3(222f, floorY + 2.2f, -20f), new Vector3(240f, floorY + 0.5f, 0f), "Backups/eclipseremains_0.png");
                Snapshot(new Vector3(252f, floorY + 2f, 18f), new Vector3(232f, floorY + 0.8f, -6f), "Backups/eclipseremains_1.png");
                Snapshot(new Vector3(199f, 1.7f + (ghostPrefab != null ? GameObject.Find("Ghost_NPC (Void Passage)").transform.position.y : 0f), 0.5f), new Vector3(203.5f, 1f + (ghostPrefab != null ? GameObject.Find("Ghost_NPC (Void Passage)").transform.position.y : 0f), -2.6f), "Backups/eclipseremains_npc.png");
                log.AppendLine("DONE");
            }
            catch (Exception e)
            {
                log.AppendLine("FAILED: " + e);
            }
            File.WriteAllText(ReportPath, log.ToString());
        }

        static GameObject Pendant(Material bronze, Material obsidian, Mesh spike, float scale)
        {
            var p = new GameObject("Sunburst necklace");
            var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            UnityEngine.Object.DestroyImmediate(disc.GetComponent<Collider>());
            disc.transform.SetParent(p.transform, false);
            disc.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            disc.transform.localScale = new Vector3(0.075f, 0.006f, 0.075f);
            disc.GetComponent<MeshRenderer>().sharedMaterial = obsidian;
            var rim = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            UnityEngine.Object.DestroyImmediate(rim.GetComponent<Collider>());
            rim.transform.SetParent(p.transform, false);
            rim.transform.localPosition = new Vector3(0f, 0f, 0.003f);
            rim.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            rim.transform.localScale = new Vector3(0.088f, 0.004f, 0.088f);
            rim.GetComponent<MeshRenderer>().sharedMaterial = bronze;
            if (spike != null)
                for (int i = 0; i < 12; i++)
                {
                    float a = i * 30f;
                    var sp = new GameObject("Spike");
                    sp.transform.SetParent(p.transform, false);
                    sp.transform.localPosition = new Vector3(Mathf.Sin(a * Mathf.Deg2Rad) * 0.064f, Mathf.Cos(a * Mathf.Deg2Rad) * 0.064f, 0f);
                    sp.transform.localRotation = Quaternion.Euler(0f, 0f, -a);
                    sp.transform.localScale = new Vector3(0.009f, i % 3 == 0 ? 0.028f : 0.022f, 0.006f);
                    sp.AddComponent<MeshFilter>().sharedMesh = spike;
                    sp.AddComponent<MeshRenderer>().sharedMaterial = bronze;
                }
            p.transform.localScale = Vector3.one * scale;
            return p;
        }

        static bool AddLyingNecklace(GameObject g, Material bronze, Material obsidian, Mesh spike, Mesh link, System.Random rng)
        {
            MeshFilter mf = g.GetComponentInChildren<MeshFilter>();
            if (mf == null || mf.sharedMesh == null || !mf.sharedMesh.isReadable) return false;
            Bounds b = BoundsOf(g);
            bool alongX = b.size.x >= b.size.z;
            int bins = 12;
            var maxY = new float[bins];
            var sumC = new float[bins];
            var cnt = new int[bins];
            for (int i = 0; i < bins; i++) maxY[i] = float.MinValue;
            Vector3[] verts = mf.sharedMesh.vertices;
            var world = new Vector3[verts.Length];
            for (int i = 0; i < verts.Length; i++)
            {
                Vector3 w = mf.transform.TransformPoint(verts[i]);
                world[i] = w;
                float u = alongX ? (w.x - b.min.x) / Mathf.Max(0.01f, b.size.x) : (w.z - b.min.z) / Mathf.Max(0.01f, b.size.z);
                int k = Mathf.Clamp((int)(u * bins), 0, bins - 1);
                if (w.y > maxY[k]) maxY[k] = w.y;
                sumC[k] += alongX ? w.z : w.x;
                cnt[k]++;
            }
            int best = bins / 2;
            for (int i = 2; i < bins - 2; i++) if (cnt[i] > 0 && maxY[i] > maxY[best]) best = i;
            float along = (best + 0.5f) / bins;
            float cross = cnt[best] > 0 ? sumC[best] / cnt[best] : (alongX ? b.center.z : b.center.x);
            Vector3 pos = alongX ? new Vector3(b.min.x + along * b.size.x, maxY[best] + 0.012f, cross) : new Vector3(cross, maxY[best] + 0.012f, b.min.z + along * b.size.z);
            var p = Pendant(bronze, obsidian, spike, 1.2f);
            p.transform.SetParent(g.transform, true);
            p.transform.position = pos;
            p.transform.rotation = Quaternion.Euler(-90f, (float)rng.NextDouble() * 360f, 0f);
            return true;
        }

        static bool AddSeatedNecklace(GameObject g, Material bronze, Material obsidian, Mesh spike, Mesh link)
        {
            MeshFilter mf = g.GetComponentInChildren<MeshFilter>();
            if (mf == null || mf.sharedMesh == null || !mf.sharedMesh.isReadable) return false;
            Bounds b = BoundsOf(g);
            Vector3 fwd = g.transform.position.x < ThroneX ? Vector3.right : Vector3.left;
            float lo = b.min.y + b.size.y * 0.6f, hi = b.min.y + b.size.y * 0.74f;
            Vector3 sum = Vector3.zero; int n = 0; float front = float.MinValue;
            foreach (Vector3 v in mf.sharedMesh.vertices)
            {
                Vector3 w = mf.transform.TransformPoint(v);
                if (w.y < lo || w.y > hi) continue;
                sum += w; n++;
                float f = Vector3.Dot(w, fwd);
                if (f > front) front = f;
            }
            if (n == 0) return false;
            Vector3 c = sum / n;
            Vector3 pos = c - fwd * Vector3.Dot(c, fwd) + fwd * (front + 0.015f);
            pos.y = b.min.y + b.size.y * 0.67f;
            var p = Pendant(bronze, obsidian, spike, 1f);
            p.transform.SetParent(g.transform, true);
            p.transform.position = pos;
            p.transform.rotation = Quaternion.LookRotation(-fwd, Vector3.up);
            return true;
        }

        static Bounds BoundsOf(GameObject g)
        {
            Renderer[] rs = g.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return new Bounds(g.transform.position, Vector3.one * 0.2f);
            Bounds b = rs[0].bounds;
            foreach (Renderer r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        static void Snapshot(Vector3 from, Vector3 at, string path)
        {
            var go = new GameObject("Snap Cam");
            Camera cam = go.AddComponent<Camera>();
            cam.fieldOfView = 68f;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 200f;
            go.transform.SetPositionAndRotation(from, Quaternion.LookRotation(at - from, Vector3.up));
            var lg = new GameObject("Snap light");
            Light l = lg.AddComponent<Light>(); l.type = LightType.Point; l.range = 14f; l.intensity = 3f; l.color = new Color(1f, 0.8f, 0.6f);
            lg.transform.position = from + Vector3.up * 0.5f;
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
            UnityEngine.Object.DestroyImmediate(lg);
        }
    }
}
