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
    static class WhiteRegionSetup
    {
        const string TriggerPath = "Assets/Interaction/Editor/.run_region3";
        const string ReportPath = "Backups/region3_report.txt";
        const string MapScene = "Assets/Scenes/FourfoldCitadel_WithOurStuff.unity";
        const string Dir = "Assets/Interaction/Generated/WhiteRegion";
        const string RegionPrefix = "03 ";

        static WhiteRegionSetup()
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

        [MenuItem("Tools/Interaction/White Region 3")]
        static void Run()
        {
            var log = new StringBuilder();
            try
            {
                AssetDatabase.Refresh();
                Directory.CreateDirectory(Dir);
                Material white = Mat("White_Unlit", "Universal Render Pipeline/Unlit", m => m.SetColor("_BaseColor", Color.white));
                Material black = Mat("Black_Unlit", "Universal Render Pipeline/Unlit", m => m.SetColor("_BaseColor", new Color(0.02f, 0.02f, 0.025f)));
                Material flame = Mat("Black_Flame", "CasaFX/BlackFlame", m => m.SetColor("_Color", new Color(0f, 0f, 0f, 1f)));
                Material aura = Mat("Black_Aura", "CasaFX/BlackAura", m => { m.SetFloat("_Radius", 2.6f); m.SetFloat("_Strength", 0.9f); });
                if (flame.shader == null || flame.shader.name != "CasaFX/BlackFlame") throw new Exception("BlackFlame shader not found");
                if (aura.shader == null || aura.shader.name != "CasaFX/BlackAura") throw new Exception("BlackAura shader not found");

                Scene active = SceneManager.GetActiveScene();
                ZoneMusic zmOpen = UnityEngine.Object.FindAnyObjectByType<ZoneMusic>();
                bool stale = active.path == MapScene && zmOpen != null && zmOpen.zones.Count == 0;
                if (!stale) EditorSceneManager.SaveOpenScenes();
                Directory.CreateDirectory("Backups");
                File.Copy(MapScene, "Backups/FourfoldCitadel_WithOurStuff_before_region3.unity", true);
                Scene map = EditorSceneManager.OpenScene(MapScene, OpenSceneMode.Single);

                Transform region = null;
                foreach (Transform t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
                    if (t.name.StartsWith(RegionPrefix) && t.name.Contains("Violet")) { region = t; break; }
                if (region == null) throw new Exception("region 3 root not found");
                log.AppendLine("region root: " + region.name);

                var flames = region.GetComponentsInChildren<SunkenPrism.TorchFlame>(true);
                var torchParts = new HashSet<Renderer>();
                var flameParts = new HashSet<Renderer>();
                foreach (var f in flames)
                {
                    foreach (Renderer r in f.GetComponentsInChildren<Renderer>(true)) flameParts.Add(r);
                    Transform torchRoot = f.transform.parent;
                    if (torchRoot == null || torchRoot == region || torchRoot.GetComponentsInChildren<Renderer>(true).Length > 24) continue;
                    foreach (Renderer r in torchRoot.GetComponentsInChildren<Renderer>(true)) if (!flameParts.Contains(r)) torchParts.Add(r);
                }

                int whiteCount = 0, blackCount = 0, flameCount = 0;
                foreach (Renderer r in region.GetComponentsInChildren<Renderer>(true))
                {
                    if (r is ParticleSystemRenderer || r is LineRenderer || r is SpriteRenderer) continue;
                    Material use = flameParts.Contains(r) ? flame : torchParts.Contains(r) ? black : white;
                    var mats = r.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++) mats[i] = use;
                    r.sharedMaterials = mats;
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    if (use == white) whiteCount++; else if (use == black) blackCount++; else flameCount++;
                }

                int lightsOff = 0, auras = 0;
                foreach (var f in flames)
                {
                    f.boost = 0f;
                    EditorUtility.SetDirty(f);
                    foreach (Light l in f.GetComponentsInChildren<Light>(true)) { l.intensity = 0f; l.enabled = false; lightsOff++; }
                    Transform oldAura = f.transform.Find("Black aura");
                    if (oldAura != null) UnityEngine.Object.DestroyImmediate(oldAura.gameObject);
                    var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    q.name = "Black aura";
                    UnityEngine.Object.DestroyImmediate(q.GetComponent<Collider>());
                    q.transform.SetParent(f.transform, false);
                    q.transform.localPosition = Vector3.up * 0.25f;
                    q.transform.localScale = Vector3.one * 5.2f;
                    var mr = q.GetComponent<MeshRenderer>();
                    mr.sharedMaterial = aura;
                    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    mr.receiveShadows = false;
                    auras++;
                }

                int otherLights = 0;
                foreach (Light l in region.GetComponentsInChildren<Light>(true))
                {
                    if (!l.enabled) continue;
                    l.enabled = false;
                    otherLights++;
                }

                log.AppendLine("white " + whiteCount + ", black torch parts " + blackCount + ", black flame ribbons " + flameCount);
                log.AppendLine("torches " + flames.Length + ", torch lights off " + lightsOff + ", other lights off " + otherLights + ", black auras " + auras);

                Bounds rb = new Bounds(region.position, Vector3.zero);
                bool any = false;
                foreach (Renderer r in region.GetComponentsInChildren<Renderer>(true)) { if (!any) { rb = r.bounds; any = true; } else rb.Encapsulate(r.bounds); }
                log.AppendLine("region bounds centre " + rb.center + " size " + rb.size);

                EditorSceneManager.MarkSceneDirty(map);
                EditorSceneManager.SaveScene(map);
                ZoneMusic zm = UnityEngine.Object.FindAnyObjectByType<ZoneMusic>();
                log.AppendLine("zone music areas: " + (zm != null ? zm.zones.Count.ToString() : "none"));

                float floorY = rb.min.y;
                if (Physics.Raycast(new Vector3(173.9f, rb.max.y - 0.5f, 0f), Vector3.down, out RaycastHit hit, 40f, ~0, QueryTriggerInteraction.Ignore)) floorY = hit.point.y;
                Vector3 eyeIn = new Vector3(155f, floorY + 1.7f, 0f);
                Snapshot(eyeIn, new Vector3(185f, floorY + 1.5f, 0f), "Backups/region3_0.png");
                Snapshot(new Vector3(173.9f, floorY + 1.7f, -12f), new Vector3(173.9f, floorY + 2f, 20f), "Backups/region3_1.png");
                if (flames.Length > 0)
                {
                    Vector3 tp = flames[0].transform.position;
                    Vector3 dir = new Vector3(173.9f, tp.y, 0f) - tp;
                    dir.y = 0f;
                    Snapshot(tp + dir.normalized * 5f + Vector3.down * 0.4f, tp, "Backups/region3_torch.png");
                }
                log.AppendLine("DONE");
            }
            catch (Exception e)
            {
                log.AppendLine("FAILED: " + e);
            }
            File.WriteAllText(ReportPath, log.ToString());
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
