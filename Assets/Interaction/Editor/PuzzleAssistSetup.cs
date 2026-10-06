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
    static class PuzzleAssistSetup
    {
        const string TriggerPath = "Assets/Interaction/Editor/.run_assist";
        const string ReportPath = "Backups/assist_report.txt";
        const string MapScene = "Assets/Scenes/FourfoldCitadel_WithOurStuff.unity";
        const string Gen = "Assets/Interaction/Generated/Assist";

        static PuzzleAssistSetup()
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

        [MenuItem("Tools/Interaction/Add Puzzle Hints, Shape Symbols And NPC Marker")]
        static void Run()
        {
            var log = new StringBuilder();
            try
            {
                Scene active = SceneManager.GetActiveScene();
                ZoneMusic zmOpen = UnityEngine.Object.FindAnyObjectByType<ZoneMusic>();
                if (active.path == MapScene && zmOpen != null && zmOpen.zones.Count == 0)
                {
                    log.AppendLine("open map had an empty Zone Music list, reloading saved map");
                    EditorSceneManager.OpenScene(MapScene, OpenSceneMode.Single);
                }
                else EditorSceneManager.SaveOpenScenes();
                AssetDatabase.Refresh();
                Directory.CreateDirectory(Gen);

                Material lineMat = AdditiveMaterial(Gen + "/ShapeSymbol.mat");
                Material markerMat = MarkerMaterial(Gen + "/QuestMarker.mat", Gen + "/QuestMarker.png");

                Scene map = SceneManager.GetActiveScene();
                if (map.path != MapScene) map = EditorSceneManager.OpenScene(MapScene, OpenSceneMode.Single);
                Physics.SyncTransforms();

                foreach (GameObject old in FindAll("Shape symbol")) UnityEngine.Object.DestroyImmediate(old);

                var sockets = new List<MirrorSocket>(UnityEngine.Object.FindObjectsByType<MirrorSocket>(FindObjectsInactive.Include, FindObjectsSortMode.None));
                sockets.Sort((a, b) => Order(a.shapeName).CompareTo(Order(b.shapeName)));
                foreach (MirrorSocket s in sockets)
                {
                    Color c = ShapeColors.For(s.shapeName);
                    s.idleColor = c;
                    EditorUtility.SetDirty(s);
                    PickupHighlight ph = s.GetComponent<PickupHighlight>();
                    if (ph != null) { ph.color = c; EditorUtility.SetDirty(ph); }
                    int sides = PlatformSymbols(s, c, lineMat);
                    string mirrorInfo = "none";
                    if (s.mirror != null)
                    {
                        PickupHighlight mh = s.mirror.GetComponent<PickupHighlight>();
                        if (mh != null) { mh.color = c; EditorUtility.SetDirty(mh); }
                        MirrorEmblem(s.mirror, s.shapeName, c, lineMat);
                        mirrorInfo = s.mirror.name;
                    }
                    log.AppendLine(s.shapeName + ": colour " + ShapeColors.Name(s.shapeName) + ", floating symbol " + sides + ", mirror " + mirrorInfo);
                }

                GameObject oldHints = GameObject.Find("Puzzle Hints");
                if (oldHints != null) UnityEngine.Object.DestroyImmediate(oldHints);
                var hg = new GameObject("Puzzle Hints");
                PuzzleHints hints = hg.AddComponent<PuzzleHints>();
                hints.torch = UnityEngine.Object.FindAnyObjectByType<PuzzleTorch>(FindObjectsInactive.Include);
                hints.sockets = sockets.ToArray();
                hints.prism = UnityEngine.Object.FindAnyObjectByType<PrismMoonController>(FindObjectsInactive.Include);
                hints.areaCenter = SunkenPrism.CitadelLayout.Centers[1];
                hints.areaSize = SunkenPrism.CitadelLayout.Sizes[1];
                hints.delayRange = new Vector2(10f, 15f);
                log.AppendLine("hints: torch " + (hints.torch != null) + ", sockets " + hints.sockets.Length + ", prism " + (hints.prism != null));

                int markers = 0;
                foreach (NPCCutscene npc in UnityEngine.Object.FindObjectsByType<NPCCutscene>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    QuestMarker qm = npc.GetComponent<QuestMarker>();
                    if (qm == null) qm = npc.gameObject.AddComponent<QuestMarker>();
                    if (qm.marker != null) UnityEngine.Object.DestroyImmediate(qm.marker.gameObject);
                    Bounds b = NpcBounds(npc.gameObject);
                    GameObject q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    q.name = "Quest marker";
                    UnityEngine.Object.DestroyImmediate(q.GetComponent<Collider>());
                    q.transform.SetParent(npc.transform, true);
                    q.transform.position = new Vector3(b.center.x, b.max.y + 0.4f, b.center.z);
                    q.transform.rotation = Quaternion.identity;
                    Vector3 ls = npc.transform.lossyScale;
                    float size = 0.42f;
                    q.transform.localScale = new Vector3(size / Mathf.Max(0.001f, ls.x), size / Mathf.Max(0.001f, ls.y), 1f / Mathf.Max(0.001f, ls.z));
                    var mr = q.GetComponent<MeshRenderer>();
                    mr.sharedMaterial = markerMat;
                    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    mr.receiveShadows = false;
                    qm.npc = npc;
                    qm.marker = q.transform;
                    EditorUtility.SetDirty(qm);
                    markers++;
                    log.AppendLine("marker on " + npc.name + " at " + q.transform.position);
                }

                EditorSceneManager.MarkSceneDirty(map);
                EditorSceneManager.SaveScene(map);

                if (sockets.Count > 0)
                {
                    MirrorSocket s0 = sockets[0];
                    Bounds pb = PlatformBounds(s0);
                    Vector3 eye = pb.center + new Vector3(1.6f, 0.9f, 1.1f);
                    Snapshot(eye, pb.center, "Backups/assist_platform.png");
                    if (s0.mirror != null)
                    {
                        Bounds mb = MirrorBounds(s0.mirror);
                        Vector3 back = -s0.mirror.transform.TransformDirection(s0.mirror.localFront.sqrMagnitude > 0f ? s0.mirror.localFront : Vector3.forward);
                        Snapshot(mb.center + back * 1.6f + Vector3.up * 0.3f, mb.center, "Backups/assist_mirror.png");
                    }
                }
                foreach (QuestMarker qm in UnityEngine.Object.FindObjectsByType<QuestMarker>(FindObjectsSortMode.None))
                {
                    if (qm.marker == null) continue;
                    Vector3 m = qm.marker.position;
                    Vector3 eye = m + new Vector3(-2.5f, -0.4f, -1.5f);
                    qm.marker.rotation = Quaternion.LookRotation(new Vector3(m.x - eye.x, 0f, m.z - eye.z));
                    Snapshot(eye, m + Vector3.down * 0.6f, "Backups/assist_marker.png");
                    break;
                }
                log.AppendLine("DONE");
            }
            catch (Exception e)
            {
                log.AppendLine("FAILED: " + e);
            }
            File.WriteAllText(ReportPath, log.ToString());
        }

        static int Order(string shape)
        {
            string k = (shape ?? "").ToLowerInvariant();
            return k == "oval" ? 0 : k == "rectangle" ? 1 : k == "circle" ? 2 : 3;
        }

        static IEnumerable<GameObject> FindAll(string name)
        {
            var list = new List<GameObject>();
            foreach (Transform t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (t.name == name) list.Add(t.gameObject);
            return list;
        }

        static Bounds PlatformBounds(MirrorSocket s)
        {
            bool any = false;
            Bounds b = new Bounds(s.transform.position, Vector3.one * 0.5f);
            foreach (Renderer r in s.GetComponentsInChildren<Renderer>(true))
            {
                if (r is LineRenderer || r is ParticleSystemRenderer) continue;
                if (r.GetComponentInParent<MirrorPickup>() != null) continue;
                if (r.name == "Glow shell" || r.name == "Shape symbol") continue;
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            return b;
        }

        static Bounds MirrorBounds(MirrorPickup m)
        {
            bool any = false;
            Bounds b = new Bounds(m.transform.position, Vector3.one * 0.5f);
            foreach (Renderer r in m.GetComponentsInChildren<Renderer>(true))
            {
                if (r is LineRenderer || r is ParticleSystemRenderer) continue;
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            return b;
        }

        static Bounds NpcBounds(GameObject go)
        {
            bool any = false;
            Bounds b = new Bounds(go.transform.position + Vector3.up, Vector3.one);
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer || r is LineRenderer || r.name == "Quest marker") continue;
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            return b;
        }

        static int PlatformSymbols(MirrorSocket s, Color c, Material mat)
        {
            Bounds b = PlatformBounds(s);
            var g = new GameObject("Shape symbol");
            g.transform.SetParent(s.transform, true);
            g.transform.position = new Vector3(b.center.x, b.max.y + 1.0f, b.center.z);
            g.transform.rotation = Quaternion.identity;
            MakeShape(g, s.shapeName, 0.42f, 0.025f, c, mat);
            ShapeBeacon beacon = s.GetComponent<ShapeBeacon>();
            if (beacon == null) beacon = s.gameObject.AddComponent<ShapeBeacon>();
            beacon.socket = s;
            beacon.symbol = g.GetComponent<LineRenderer>();
            EditorUtility.SetDirty(beacon);
            return 1;
        }

        static void MirrorEmblem(MirrorPickup m, string shape, Color c, Material mat)
        {
            Transform t = m.transform;
            Vector3 front = m.localFront.sqrMagnitude > 0f ? m.localFront.normalized : Vector3.forward;
            bool any = false;
            Bounds lb = new Bounds(Vector3.zero, Vector3.zero);
            foreach (Renderer r in m.GetComponentsInChildren<Renderer>(true))
            {
                if (r is LineRenderer || r is ParticleSystemRenderer || r.name == "Glow shell") continue;
                Bounds wb = r.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = wb.center + Vector3.Scale(wb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    Vector3 lp = t.InverseTransformPoint(corner);
                    if (!any) { lb = new Bounds(lp, Vector3.zero); any = true; } else lb.Encapsulate(lp);
                }
            }
            if (!any) lb = m.localBounds;
            float back = Vector3.Dot(lb.center, front) - Mathf.Abs(Vector3.Dot(lb.extents, new Vector3(Mathf.Abs(front.x), Mathf.Abs(front.y), Mathf.Abs(front.z))));
            Vector3 centerOnPlane = lb.center - front * Vector3.Dot(lb.center, front);
            Vector3 lpos = centerOnPlane + front * (back + 0.03f);
            Vector3 side = Vector3.Cross(Vector3.up, front);
            float width = Mathf.Abs(Vector3.Dot(lb.size, new Vector3(Mathf.Abs(side.x), Mathf.Abs(side.y), Mathf.Abs(side.z))));
            float height = lb.size.y;
            float size = Mathf.Min(width, height) * 0.45f;
            var g = new GameObject("Shape symbol");
            g.transform.SetParent(t, false);
            g.transform.localPosition = lpos - front * 0.04f;
            g.transform.localRotation = Quaternion.LookRotation(front, Vector3.up);
            MakeShape(g, shape, size, 0.022f, c, mat);
        }

        static void MakeShape(GameObject g, string shape, float size, float width, Color c, Material mat)
        {
            var lr = g.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.loop = true;
            lr.alignment = LineAlignment.TransformZ;
            lr.widthMultiplier = width;
            lr.numCornerVertices = 2;
            lr.numCapVertices = 0;
            lr.sharedMaterial = mat;
            lr.startColor = c;
            lr.endColor = c;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            var pts = new List<Vector3>();
            string k = (shape ?? "").ToLowerInvariant();
            if (k == "rectangle")
            {
                float w = size * 0.5f, h = size * 0.32f;
                pts.Add(new Vector3(-w, -h, 0f));
                pts.Add(new Vector3(w, -h, 0f));
                pts.Add(new Vector3(w, h, 0f));
                pts.Add(new Vector3(-w, h, 0f));
            }
            else
            {
                float rx = k == "oval" ? size * 0.5f : size * 0.4f;
                float ry = k == "oval" ? size * 0.3f : size * 0.4f;
                for (int i = 0; i < 40; i++)
                {
                    float a = i / 40f * Mathf.PI * 2f;
                    pts.Add(new Vector3(Mathf.Cos(a) * rx, Mathf.Sin(a) * ry, 0f));
                }
            }
            lr.positionCount = pts.Count;
            lr.SetPositions(pts.ToArray());
        }

        static Material AdditiveMaterial(string path)
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, path); }
            m.shader = sh;
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 2f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_Cull", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = 3000;
            EditorUtility.SetDirty(m);
            AssetDatabase.SaveAssets();
            return m;
        }

        static Material MarkerMaterial(string path, string texPath)
        {
            var ti = AssetImporter.GetAtPath(texPath) as TextureImporter;
            if (ti != null)
            {
                ti.textureType = TextureImporterType.Default;
                ti.alphaIsTransparency = true;
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.mipmapEnabled = true;
                ti.SaveAndReimport();
            }
            Shader sh = Shader.Find("Universal Render Pipeline/Unlit");
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, path); }
            m.shader = sh;
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texPath));
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_Cull", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = 3000;
            EditorUtility.SetDirty(m);
            AssetDatabase.SaveAssets();
            return m;
        }

        static void Snapshot(Vector3 from, Vector3 at, string path)
        {
            var go = new GameObject("Snap Cam");
            Camera cam = go.AddComponent<Camera>();
            cam.fieldOfView = 60f;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 200f;
            go.transform.SetPositionAndRotation(from, Quaternion.LookRotation(at - from, Vector3.up));
            var rt = new RenderTexture(960, 540, 24);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(960, 540, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 960, 540), 0, 0);
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
