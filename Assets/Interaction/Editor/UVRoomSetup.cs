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
    static class UVRoomSetup
    {
        const string TriggerPath = "Assets/Interaction/Editor/.run_uvroom";
        const string ReportPath = "Backups/uvroom_report.txt";
        const string MapScene = "Assets/Scenes/FourfoldCitadel_WithOurStuff.unity";
        const string CleanBackup = "Backups/FourfoldCitadel_WithOurStuff_before_region3.unity";
        const string Dir = "Assets/Interaction/Generated/UVRoom";
        const string RootName = "UV Sanctum Dressing";
        static readonly Vector3 Center = new Vector3(173.9f, 0f, 0f);
        static readonly Color[] Palette = { new Color(1f, 0.22f, 0.92f), new Color(0.2f, 0.95f, 1f), new Color(0.68f, 1f, 0.22f), new Color(0.66f, 0.38f, 1f) };

        static UVRoomSetup()
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

        [MenuItem("Tools/Interaction/Region 3 Ultraviolet Sanctum")]
        static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                File.WriteAllText(TriggerPath, "pending");
                EditorApplication.delayCall += Check;
                Debug.LogWarning("[UV Sanctum] Stop Play mode first. It will run automatically once Play mode ends.");
                return;
            }
            var log = new StringBuilder();
            try
            {
                Scene active = SceneManager.GetActiveScene();
                ZoneMusic zmOpen = UnityEngine.Object.FindAnyObjectByType<ZoneMusic>();
                bool stale = active.path == MapScene && zmOpen != null && zmOpen.zones.Count == 0;
                if (!stale) EditorSceneManager.SaveOpenScenes();
                if (!File.Exists(CleanBackup)) throw new Exception("clean backup missing: " + CleanBackup);
                File.Copy(MapScene, "Backups/FourfoldCitadel_WithOurStuff_before_uvroom9.unity", true);
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                File.Copy(CleanBackup, MapScene, true);
                AssetDatabase.ImportAsset(MapScene, ImportAssetOptions.ForceUpdate);
                log.AppendLine("restored region 3 from " + CleanBackup);
                AssetDatabase.Refresh();
                foreach (string f in new[] { "uv_rune_circle", "uv_glyphs", "uv_handprint", "uv_mote" })
                {
                    var ti = AssetImporter.GetAtPath(Dir + "/" + f + ".png") as TextureImporter;
                    if (ti == null) continue;
                    ti.alphaIsTransparency = true; ti.wrapMode = TextureWrapMode.Clamp; ti.mipmapEnabled = true; ti.SaveAndReimport();
                }

                Scene map = EditorSceneManager.OpenScene(MapScene, OpenSceneMode.Single);
                Physics.SyncTransforms();
                GameObject old = GameObject.Find(RootName);
                if (old != null) UnityEngine.Object.DestroyImmediate(old);

                Transform region = null;
                foreach (Transform t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
                    if (t.name.StartsWith("03 ") && t.name.Contains("Violet")) { region = t; break; }
                if (region == null) throw new Exception("region 3 root not found");

                Material dark = Lit("UV_DarkStone", new Color(0.05f, 0.042f, 0.085f), 0.35f, Color.black);
                Material fire = MatShader("UV_TorchFire", "SunkenPrism/TorchFlame", m => m.SetColor("_Color", new Color(0.62f, 0.32f, 1f, 1f)));
                var flames = region.GetComponentsInChildren<SunkenPrism.TorchFlame>(true);
                var flameRs = new HashSet<Renderer>();
                foreach (var f in flames) foreach (Renderer r in f.GetComponentsInChildren<Renderer>(true)) flameRs.Add(r);
                int darkened = 0;
                foreach (Renderer r in region.GetComponentsInChildren<Renderer>(true))
                {
                    if (r is ParticleSystemRenderer || r is LineRenderer || r is SpriteRenderer) continue;
                    var mats = r.sharedMaterials;
                    if (!flameRs.Contains(r)) continue;
                    Material use = fire;
                    for (int i = 0; i < mats.Length; i++) mats[i] = use;
                    r.sharedMaterials = mats;
                    darkened++;
                }
                foreach (var f in flames)
                {
                    f.boost = 0.85f;
                    EditorUtility.SetDirty(f);
                    foreach (Light l in f.GetComponentsInChildren<Light>(true)) l.color = new Color(0.6f, 0.3f, 1f);
                }
                log.AppendLine("violet flame ribbons " + darkened + ", violet torches " + flames.Length + ", original textures kept");

                float floorY0 = 0f;
                if (Physics.Raycast(new Vector3(158f, 3f, 0f), Vector3.down, out RaycastHit fh0, 10f, ~0, QueryTriggerInteraction.Ignore)) floorY0 = fh0.point.y;
                var skinCache = new Dictionary<string, Material>();
                var skinRng = new System.Random(57);
                int skinned = 0, labels = 0;
                foreach (Renderer r in region.GetComponentsInChildren<Renderer>(true))
                {
                    if (flameRs.Contains(r) || r is ParticleSystemRenderer || r is LineRenderer || r is SpriteRenderer) continue;
                    if (r.GetComponent<TextMesh>() != null || (r.sharedMaterial != null && r.sharedMaterial.shader != null && (r.sharedMaterial.shader.name.Contains("WorldText") || r.sharedMaterial.shader.name.Contains("Font"))))
                    {
                        r.enabled = false;
                        labels++;
                        continue;
                    }
                    Bounds b = r.bounds;
                    bool isFloor = b.size.y < 0.7f && b.max.y < 0.9f && b.size.x * b.size.z > 3f;
                    Vector3 sz = b.size;
                    float u = Mathf.Max(sz.x, sz.z), v = isFloor ? Mathf.Min(sz.x, sz.z) : sz.y;
                    bool high = b.min.y > floorY0 + 6f || sz.y > 12f;
                    Vector2 tile = isFloor ? new Vector2(Mathf.Max(1f, Mathf.Round(u / 6f * 2f) / 2f), Mathf.Max(1f, Mathf.Round(v / 6f * 2f) / 2f)) : high ? new Vector2(0.35f, 0.35f) : new Vector2(0.6f, 0.6f);
                    int variant = high ? skinRng.Next(5) : 0;
                    string key = (isFloor ? "Floor_" : high ? "High_" + variant + "_" : "Wall_") + tile.x.ToString("0.0") + "x" + tile.y.ToString("0.0");
                    if (!skinCache.TryGetValue(key, out Material sm))
                    {
                        sm = Stone("UV_Stone_" + key, isFloor ? "uv_sanctum_floor" : "uv_sanctum_wall", tile, isFloor ? new Color(0.9f, 0.45f, 1f) : new Color(0.5f, 0.85f, 1f));
                        if (high)
                        {
                            sm.SetColor("_EmissionColor", Color.black);
                            sm.DisableKeyword("_EMISSION");
                        }
                        skinCache[key] = sm;
                    }
                    var ms = r.sharedMaterials;
                    for (int i = 0; i < ms.Length; i++) ms[i] = sm;
                    r.sharedMaterials = ms;
                    skinned++;
                }
                log.AppendLine("themed stone on " + skinned + " renderers (" + skinCache.Count + " materials), labels hidden " + labels);

                float floorY = 0f;
                if (Physics.Raycast(new Vector3(158f, 3f, 0f), Vector3.down, out RaycastHit fh, 10f, ~0, QueryTriggerInteraction.Ignore)) floorY = fh.point.y;
                float daisTop = floorY;
                if (Physics.Raycast(new Vector3(Center.x, floorY + 9f, 0f), Vector3.down, out RaycastHit dh, 12f, ~0, QueryTriggerInteraction.Ignore)) daisTop = dh.point.y;
                log.AppendLine("floor " + floorY + ", centre top " + daisTop);

                var root = new GameObject(RootName);

                foreach (var p in new[] { new Vector3(0f, 7f, 0f), new Vector3(12f, 4f, 12f), new Vector3(-12f, 4f, 12f), new Vector3(12f, 4f, -12f), new Vector3(-12f, 4f, -12f) })
                {
                    var lg = new GameObject("UV light");
                    lg.transform.SetParent(root.transform, false);
                    lg.transform.position = new Vector3(Center.x + p.x, floorY + p.y, p.z);
                    Light l = lg.AddComponent<Light>();
                    l.type = LightType.Point;
                    l.color = new Color(0.42f, 0.18f, 1f);
                    l.intensity = p.y > 5f ? 3f : 1.7f;
                    l.range = p.y > 5f ? 30f : 18f;
                    l.shadows = LightShadows.None;
                }
                for (int i = 0; i < 10; i++)
                {
                    float a = i / 10f * Mathf.PI * 2f + 0.3f;
                    Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    float reach = 15f;
                    if (Physics.Raycast(new Vector3(Center.x, floorY + 2.4f, 0f) + dir * 4f, dir, out RaycastHit gh, 30f, ~0, QueryTriggerInteraction.Ignore)) reach = Vector3.Distance(new Vector3(Center.x, floorY + 2.4f, 0f), gh.point) - 2.2f;
                    var gg = new GameObject("UV wall wash");
                    gg.transform.SetParent(root.transform, false);
                    gg.transform.position = new Vector3(Center.x, floorY + 2.4f, 0f) + dir * Mathf.Max(5f, reach);
                    Light gl2 = gg.AddComponent<Light>();
                    gl2.type = LightType.Point;
                    gl2.color = new Color(0.5f, 0.22f, 1f);
                    gl2.intensity = 1.05f;
                    gl2.range = 8f;
                    gl2.shadows = LightShadows.None;
                }

                Texture2D runeTex = AssetDatabase.LoadAssetAtPath<Texture2D>(Dir + "/uv_rune_circle.png");
                Texture2D glyphTex = AssetDatabase.LoadAssetAtPath<Texture2D>(Dir + "/uv_glyphs.png");
                Texture2D handTex = AssetDatabase.LoadAssetAtPath<Texture2D>(Dir + "/uv_handprint.png");
                Texture2D moteTex = AssetDatabase.LoadAssetAtPath<Texture2D>(Dir + "/uv_mote.png");

                Material runeMat = Glow("UV_Rune", runeTex, Palette[3], 1.3f, Vector2.one, Vector2.zero, 0.25f);
                float ceilY = floorY + 18f;
                if (Physics.Raycast(new Vector3(Center.x, daisTop + 4f, 0f), Vector3.up, out RaycastHit rch, 40f, ~0, QueryTriggerInteraction.Ignore)) ceilY = rch.point.y;
                Quad(root.transform, "Ceiling rune circle", runeMat, new Vector3(Center.x, ceilY - 0.35f, 0f), Quaternion.Euler(90f, 0f, 0f), 14f);
                var crackMats = new List<Material>();
                for (int v = 0; v < 4; v++)
                {
                    Texture2D ct = AssetDatabase.LoadAssetAtPath<Texture2D>(Dir + "/uv_dome_crack_" + v + ".png");
                    if (ct != null) crackMats.Add(Glow("UV_DomeCrack_" + v, ct, new Color(0.5f, 0.85f, 1f), 1.2f, Vector2.one, Vector2.zero, 0.3f));
                }
                var crackRng = new System.Random(Environment.TickCount);
                int cracks = 0;
                for (int tries = 0; tries < 60 && cracks < 7 && crackMats.Count > 0; tries++)
                {
                    float az = (float)crackRng.NextDouble() * Mathf.PI * 2f;
                    float el = Mathf.Lerp(22f, 68f, (float)crackRng.NextDouble()) * Mathf.Deg2Rad;
                    Vector3 cdir = new Vector3(Mathf.Cos(az) * Mathf.Cos(el), Mathf.Sin(el), Mathf.Sin(az) * Mathf.Cos(el));
                    Vector3 origin = new Vector3(Center.x, floorY + 7f, 0f);
                    if (!Physics.Raycast(origin, cdir, out RaycastHit ck, 40f, ~0, QueryTriggerInteraction.Ignore)) continue;
                    if (ck.point.y < floorY + 8f) continue;
                    float size = 4f + (float)crackRng.NextDouble() * 4f;
                    var cq = Quad(root.transform, "Dome crack", crackMats[crackRng.Next(crackMats.Count)], ck.point + ck.normal * 0.06f, Quaternion.LookRotation(-ck.normal, Vector3.up) * Quaternion.Euler(0f, 0f, (float)crackRng.NextDouble() * 360f), size);
                    cracks++;
                }
                log.AppendLine("random dome cracks " + cracks);

                var rng = new System.Random(31);
                Vector3 buttonPos = new Vector3(185.6f, floorY, 11.8f);
                Vector2[] path = { new Vector2(154.5f, 0.6f), new Vector2(159f, 2.0f), new Vector2(163.5f, 4.6f), new Vector2(167.5f, 7.4f), new Vector2(172f, 9.4f), new Vector2(176.5f, 10.4f), new Vector2(181f, 10.9f), new Vector2(184.4f, 11.5f) };
                Texture2D footTex = AssetDatabase.LoadAssetAtPath<Texture2D>(Dir + "/uv_footprint.png");
                Material footMat = Glow("UV_Footprint", footTex, new Color(0.78f, 1f, 0.6f), 0f, Vector2.one, Vector2.zero, 0.12f);
                var trailGo = new GameObject("UV Footprints");
                trailGo.transform.SetParent(root.transform, false);
                var trail = trailGo.AddComponent<UVFootprintTrail>();
                trail.areaCenter = new Vector3(Center.x, floorY, 0f);
                trail.areaSize = new Vector2(44f, 44f);
                trail.hintDelay = 10f;
                int hands = 0;
                float stepLen = 0.72f, acc = 0f;
                bool left = true;
                for (int i = 0; i < path.Length - 1; i++)
                {
                    Vector2 a0 = path[i], a1 = path[i + 1];
                    float segLen = Vector2.Distance(a0, a1);
                    Vector2 dir2 = (a1 - a0).normalized;
                    Vector2 side2 = new Vector2(dir2.y, -dir2.x);
                    for (; acc < segLen; acc += stepLen)
                    {
                        Vector2 q = a0 + dir2 * acc + side2 * (left ? -0.14f : 0.14f);
                        if (!Physics.Raycast(new Vector3(q.x, floorY + 3f, q.y), Vector3.down, out RaycastHit hh, 6f, ~0, QueryTriggerInteraction.Ignore)) { left = !left; continue; }
                        if (hh.point.y > floorY + 0.25f) { left = !left; continue; }
                        float yaw = Mathf.Atan2(dir2.x, dir2.y) * Mathf.Rad2Deg + (float)(rng.NextDouble() - 0.5) * 10f;
                        var fp = Quad(trailGo.transform, left ? "Footprint L" : "Footprint R", footMat, hh.point + Vector3.up * 0.02f, Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(90f, 0f, 0f), 1f);
                        fp.transform.localScale = new Vector3(left ? 0.17f : -0.17f, 0.34f, 1f);
                        fp.AddComponent<UVFootprint>();
                        left = !left;
                        hands++;
                    }
                    acc -= segLen;
                }

                var btnRoot = new GameObject("Red Button");
                btnRoot.transform.SetParent(root.transform, false);
                btnRoot.transform.position = buttonPos;
                btnRoot.transform.rotation = Quaternion.LookRotation(new Vector3(path[path.Length - 2].x - buttonPos.x, 0f, path[path.Length - 2].y - buttonPos.z), Vector3.up);
                Material pedMat = Stone("UV_Pedestal", "uv_sanctum_wall", new Vector2(0.5f, 0.6f), new Color(0.5f, 0.85f, 1f));
                Material metal = Lit("UV_ButtonMetal", new Color(0.06f, 0.06f, 0.08f), 0.7f, Color.black);
                Material redMat = Lit("UV_ButtonRed", new Color(0.75f, 0.04f, 0.03f), 0.85f, new Color(1f, 0.08f, 0.04f) * 0.9f);
                Mesh cyl = SaveMesh(Cylinder(16), "uv_button_cyl");
                Mesh dome = SaveMesh(Cap(16, 6), "uv_button_dome");
                var ped = GameObject.CreatePrimitive(PrimitiveType.Cube);
                ped.name = "Pedestal";
                ped.transform.SetParent(btnRoot.transform, false);
                ped.transform.localPosition = new Vector3(0f, 0.5f, 0f);
                ped.transform.localScale = new Vector3(0.62f, 1f, 0.62f);
                ped.GetComponent<MeshRenderer>().sharedMaterial = pedMat;
                var pedTop = GameObject.CreatePrimitive(PrimitiveType.Cube);
                pedTop.name = "Pedestal top";
                pedTop.transform.SetParent(btnRoot.transform, false);
                pedTop.transform.localPosition = new Vector3(0f, 1.04f, 0f);
                pedTop.transform.localScale = new Vector3(0.78f, 0.08f, 0.78f);
                pedTop.GetComponent<MeshRenderer>().sharedMaterial = metal;
                var housing = new GameObject("Housing");
                housing.transform.SetParent(btnRoot.transform, false);
                housing.transform.localPosition = new Vector3(0f, 1.08f, 0f);
                housing.transform.localScale = new Vector3(0.3f, 0.07f, 0.3f);
                housing.AddComponent<MeshFilter>().sharedMesh = cyl;
                housing.AddComponent<MeshRenderer>().sharedMaterial = metal;
                var capGo = new GameObject("Button cap");
                capGo.transform.SetParent(btnRoot.transform, false);
                capGo.transform.localPosition = new Vector3(0f, 1.135f, 0f);
                capGo.transform.localScale = new Vector3(0.22f, 0.09f, 0.22f);
                capGo.AddComponent<MeshFilter>().sharedMesh = dome;
                capGo.AddComponent<MeshRenderer>().sharedMaterial = redMat;
                var capCol = capGo.AddComponent<SphereCollider>();
                capCol.radius = 0.6f;
                var bl = new GameObject("Button glow");
                bl.transform.SetParent(btnRoot.transform, false);
                bl.transform.localPosition = new Vector3(0f, 1.4f, 0f);
                Light btnLight = bl.AddComponent<Light>();
                btnLight.type = LightType.Point;
                btnLight.color = new Color(1f, 0.12f, 0.08f);
                btnLight.intensity = 0.9f;
                btnLight.range = 3f;
                btnLight.shadows = LightShadows.None;
                var rb2 = btnRoot.AddComponent<RedButton>();
                rb2.cap = capGo.transform;
                rb2.glow = btnLight;
                log.AppendLine("footprints " + hands + " leading to red button at " + buttonPos);

                Camera playerCam = null;
                FPCharacterMover moverObj = UnityEngine.Object.FindAnyObjectByType<FPCharacterMover>(FindObjectsInactive.Include);
                if (moverObj != null)
                {
                    foreach (Camera c in moverObj.GetComponentsInChildren<Camera>(true))
                    {
                        log.AppendLine("  camera under player: " + c.name + " tag " + c.tag + " enabled " + c.enabled + " active " + c.gameObject.activeInHierarchy + " listener " + (c.GetComponent<AudioListener>() != null));
                        if (c.CompareTag("MainCamera") || c.GetComponent<AudioListener>() != null) { playerCam = c; }
                    }
                    if (playerCam == null) foreach (Camera c in moverObj.GetComponentsInChildren<Camera>(false)) if (c.enabled) { playerCam = c; break; }
                }
                if (playerCam == null && Camera.main != null) playerCam = Camera.main;
                foreach (Camera c in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include))
                {
                    foreach (UVBaton ob in c.GetComponents<UVBaton>()) if (c != playerCam) UnityEngine.Object.DestroyImmediate(ob);
                    Transform om = c.transform.Find("UV Baton"); if (om != null && c != playerCam) UnityEngine.Object.DestroyImmediate(om.gameObject);
                    Transform ob2 = c.transform.Find("UV Beam"); if (ob2 != null) UnityEngine.Object.DestroyImmediate(ob2.gameObject);
                }
                if (playerCam != null)
                {
                    foreach (UVBaton oldB in playerCam.GetComponents<UVBaton>()) UnityEngine.Object.DestroyImmediate(oldB);
                    Transform oldModel = playerCam.transform.Find("UV Baton");
                    if (oldModel != null) UnityEngine.Object.DestroyImmediate(oldModel.gameObject);
                    var model = new GameObject("UV Baton");
                    model.transform.SetParent(playerCam.transform, false);
                    model.transform.localPosition = new Vector3(0.24f, -0.26f, 0.48f);
                    model.transform.localRotation = Quaternion.Euler(78f, -6f, 0f);
                    Material grip = Lit("UV_BatonGrip", new Color(0.04f, 0.04f, 0.05f), 0.35f, Color.black);
                    Material tube = Lit("UV_BatonTube", new Color(0.78f, 0.78f, 0.82f), 0.6f, Color.black);
                    var h = new GameObject("Grip");
                    h.transform.SetParent(model.transform, false);
                    h.transform.localPosition = new Vector3(0f, -0.16f, 0f);
                    h.transform.localScale = new Vector3(0.05f, 0.15f, 0.05f);
                    h.AddComponent<MeshFilter>().sharedMesh = cyl;
                    h.AddComponent<MeshRenderer>().sharedMaterial = grip;
                    var ring = new GameObject("Collar");
                    ring.transform.SetParent(model.transform, false);
                    ring.transform.localPosition = new Vector3(0f, -0.02f, 0f);
                    ring.transform.localScale = new Vector3(0.058f, 0.03f, 0.058f);
                    ring.AddComponent<MeshFilter>().sharedMesh = cyl;
                    ring.AddComponent<MeshRenderer>().sharedMaterial = grip;
                    var tb = new GameObject("Tube");
                    tb.transform.SetParent(model.transform, false);
                    tb.transform.localPosition = new Vector3(0f, 0.01f, 0f);
                    tb.transform.localScale = new Vector3(0.042f, 0.3f, 0.042f);
                    tb.AddComponent<MeshFilter>().sharedMesh = cyl;
                    var tubeR = tb.AddComponent<MeshRenderer>();
                    tubeR.sharedMaterial = tube;
                    tube.EnableKeyword("_EMISSION");
                    tube.SetColor("_EmissionColor", Color.black);
                    foreach (Renderer r in model.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    var beamGo = new GameObject("UV Beam");
                    beamGo.transform.SetParent(playerCam.transform, false);
                    beamGo.transform.localPosition = new Vector3(0.22f, -0.18f, 0.7f);
                    beamGo.transform.localRotation = Quaternion.identity;
                    Light beam = beamGo.AddComponent<Light>();
                    beam.type = LightType.Spot;
                    beam.color = new Color(0.52f, 0.22f, 1f);
                    beam.intensity = 6f;
                    beam.range = 13f;
                    beam.spotAngle = 42f;
                    beam.innerSpotAngle = 18f;
                    beam.shadows = LightShadows.None;
                    beam.enabled = false;
                    var baton = playerCam.gameObject.AddComponent<UVBaton>();
                    baton.model = model;
                    baton.beam = beam;
                    baton.glowTube = tubeR;
                    model.SetActive(false);
                    log.AppendLine("UV baton added to " + playerCam.name + " (key 2)");
                }
                else log.AppendLine("player camera not found, baton not added");

                var art = new GameObject("Hanging artifact");
                art.transform.SetParent(root.transform, false);
                Vector3 artPos = new Vector3(Center.x, Mathf.Max(daisTop, floorY) + 3.2f, 0f);
                art.transform.position = artPos;
                art.transform.localScale = Vector3.one * 1.1f;
                art.AddComponent<MeshFilter>().sharedMesh = SaveMesh(Octahedron(), "uv_octahedron");
                var amr = art.AddComponent<MeshRenderer>();
                amr.sharedMaterial = Lit("UV_Artifact", new Color(0.05f, 0.15f, 0.2f), 0.9f, Palette[1] * 2.6f);
                amr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var spin = art.AddComponent<SlowSpin>();
                spin.degreesPerSecond = new Vector3(0f, 7f, 0f);
                spin.bobHeight = 0f;
                float ceil = artPos.y + 8f;
                if (Physics.Raycast(artPos + Vector3.up * 1.3f, Vector3.up, out RaycastHit ch, 40f, ~0, QueryTriggerInteraction.Ignore)) ceil = ch.point.y;
                Material chainMat = Lit("UV_Chain", new Color(0.13f, 0.12f, 0.15f), 0.65f, Color.black);
                Mesh link = SaveMesh(Link(), "uv_chain_link");
                var chain = new GameObject("Chain");
                chain.transform.SetParent(root.transform, false);
                int links = 0;
                for (float y = artPos.y + 1.15f; y < ceil; y += 0.16f)
                {
                    var lk = new GameObject("Link");
                    lk.transform.SetParent(chain.transform, false);
                    lk.transform.position = new Vector3(artPos.x, y, artPos.z);
                    lk.transform.rotation = Quaternion.Euler(0f, links % 2 == 0 ? 0f : 90f, 0f);
                    lk.transform.localScale = Vector3.one * 0.11f;
                    lk.AddComponent<MeshFilter>().sharedMesh = link;
                    var lmr = lk.AddComponent<MeshRenderer>();
                    lmr.sharedMaterial = chainMat;
                    lmr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    links++;
                }
                log.AppendLine("artifact hangs from " + ceil.ToString("0.0") + " on " + links + " chain links");
                var al = new GameObject("Artifact light");
                al.transform.SetParent(art.transform, false);
                Light alt = al.AddComponent<Light>();
                alt.type = LightType.Point;
                alt.color = Palette[1];
                alt.intensity = 2.2f;
                alt.range = 7f;
                alt.shadows = LightShadows.None;

                Mesh octa = SaveMesh(Octahedron(), "uv_pendant");
                Mesh linkMesh = AssetDatabase.LoadAssetAtPath<Mesh>(Dir + "/Meshes/uv_chain_link.asset");
                Material chainMat2 = AssetDatabase.LoadAssetAtPath<Material>(Dir + "/UV_Chain.mat");
                var pendMats = new[] { Lit("UV_Pendant_Violet", new Color(0.1f, 0.05f, 0.2f), 0.9f, Palette[3] * 2.4f), Lit("UV_Pendant_Cyan", new Color(0.04f, 0.12f, 0.16f), 0.9f, Palette[1] * 2.2f) };
                int pendants = 0;
                for (int i = 0; i < 9; i++)
                {
                    float a = i / 9f * Mathf.PI * 2f + 0.35f;
                    float rad = 8.5f + (float)rng.NextDouble() * 5f;
                    Vector3 p = new Vector3(Center.x + Mathf.Cos(a) * rad, 0f, Mathf.Sin(a) * rad);
                    float top = floorY + 14f;
                    if (Physics.Raycast(new Vector3(p.x, floorY + 4f, p.z), Vector3.up, out RaycastHit ph, 40f, ~0, QueryTriggerInteraction.Ignore)) top = ph.point.y;
                    float y = floorY + 4.5f + (float)rng.NextDouble() * 4.5f;
                    if (top < y + 1.5f) continue;
                    var pg = new GameObject("Hanging crystal");
                    pg.transform.SetParent(root.transform, false);
                    pg.transform.position = new Vector3(p.x, y, p.z);
                    pg.transform.localScale = Vector3.one * (0.3f + (float)rng.NextDouble() * 0.25f);
                    pg.AddComponent<MeshFilter>().sharedMesh = octa;
                    var pmr = pg.AddComponent<MeshRenderer>();
                    pmr.sharedMaterial = pendMats[i % 2];
                    pmr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    var ps2 = pg.AddComponent<SlowSpin>();
                    ps2.degreesPerSecond = new Vector3(0f, 10f + (float)rng.NextDouble() * 12f, 0f);
                    ps2.bobHeight = 0f;
                    if (linkMesh != null)
                    {
                        int li = 0;
                        for (float cy = y + pg.transform.localScale.y; cy < top; cy += 0.16f)
                        {
                            var lk = new GameObject("Link");
                            lk.transform.SetParent(root.transform, false);
                            lk.transform.position = new Vector3(p.x, cy, p.z);
                            lk.transform.rotation = Quaternion.Euler(0f, li % 2 == 0 ? 0f : 90f, 0f);
                            lk.transform.localScale = Vector3.one * 0.07f;
                            lk.AddComponent<MeshFilter>().sharedMesh = linkMesh;
                            var lmr = lk.AddComponent<MeshRenderer>();
                            lmr.sharedMaterial = chainMat2;
                            lmr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                            li++;
                        }
                    }
                    if (i % 3 == 0)
                    {
                        var pl = new GameObject("Crystal light");
                        pl.transform.SetParent(pg.transform, false);
                        Light l2 = pl.AddComponent<Light>();
                        l2.type = LightType.Point;
                        l2.color = i % 2 == 0 ? Palette[3] : Palette[1];
                        l2.intensity = 1.6f;
                        l2.range = 5f;
                        l2.shadows = LightShadows.None;
                    }
                    pendants++;
                }

                Material obeliskMat = Stone("UV_Obelisk", "uv_sanctum_wall", new Vector2(1f, 2.5f), new Color(0.5f, 0.85f, 1f));
                Mesh obMesh = SaveMesh(Obelisk(), "uv_obelisk");
                Material rubbleMat = Lit("UV_Rubble", new Color(0.2f, 0.16f, 0.3f), 0.2f, Color.black);
                Texture2D rockTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Interaction/Generated/LunarStyle/painted_cave_wall.png");
                if (rockTex != null) { rubbleMat.SetTexture("_BaseMap", rockTex); rubbleMat.SetColor("_BaseColor", new Color(0.55f, 0.45f, 0.8f)); }
                var rocks = new List<Mesh>();
                for (int i = 0; i < 8; i++) { Mesh rm = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Interaction/Generated/LunarStyle/Meshes/rock_" + i + ".asset"); if (rm != null) rocks.Add(rm); }
                int obelisks = 0, rubble = 0;
                for (int i = 0; i < 12; i++)
                {
                    float a = i / 12f * Mathf.PI * 2f + 0.26f;
                    Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    if (Mathf.Abs(dir.z) < 0.35f) continue;
                    if (!Physics.Raycast(new Vector3(Center.x, floorY + 0.6f, 0f) + dir * 4f, dir, out RaycastHit oh, 30f, ~0, QueryTriggerInteraction.Ignore)) continue;
                    Vector3 basePos = new Vector3(oh.point.x, floorY, oh.point.z) - dir * 1.7f;
                    if (i % 2 == 0)
                    {
                        var ob = new GameObject("Obelisk");
                        ob.transform.SetParent(root.transform, false);
                        ob.transform.position = basePos;
                        ob.transform.rotation = Quaternion.LookRotation(-dir, Vector3.up) * Quaternion.Euler(0f, 45f, 0f);
                        float hgt = 3.2f + (float)rng.NextDouble() * 1.6f;
                        ob.transform.localScale = new Vector3(0.9f, hgt, 0.9f);
                        ob.AddComponent<MeshFilter>().sharedMesh = obMesh;
                        ob.AddComponent<MeshRenderer>().sharedMaterial = obeliskMat;
                        var bc = ob.AddComponent<BoxCollider>();
                        bc.center = new Vector3(0f, 0.5f, 0f);
                        bc.size = new Vector3(1f, 1f, 1f);
                        obelisks++;
                    }
                    if (rocks.Count > 0)
                    {
                        int n = 2 + rng.Next(4);
                        for (int k = 0; k < n; k++)
                        {
                            var rk = new GameObject("Rubble");
                            rk.transform.SetParent(root.transform, false);
                            Vector3 off = new Vector3((float)(rng.NextDouble() - 0.5) * 2.4f, 0f, (float)(rng.NextDouble() - 0.5) * 2.4f);
                            float sc = 0.2f + (float)rng.NextDouble() * (k == 0 ? 0.8f : 0.4f);
                            rk.transform.position = basePos + off + Vector3.up * sc * 0.2f;
                            rk.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
                            rk.transform.localScale = new Vector3(sc * 1.3f, sc * 0.8f, sc);
                            rk.AddComponent<MeshFilter>().sharedMesh = rocks[rng.Next(rocks.Count)];
                            rk.AddComponent<MeshRenderer>().sharedMaterial = rubbleMat;
                            rubble++;
                        }
                    }
                }
                log.AppendLine("ceiling rune at " + ceilY.ToString("0.0") + ", hanging crystals " + pendants + ", obelisks " + obelisks + ", rubble " + rubble);

                var mg = new GameObject("UV motes");
                mg.transform.SetParent(root.transform, false);
                mg.transform.position = new Vector3(Center.x, floorY + 3.5f, 0f);
                var ps = mg.AddComponent<ParticleSystem>();
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = ps.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(9f, 16f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.03f, 0.18f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.11f);
                var grad = new Gradient();
                grad.SetKeys(new[] { new GradientColorKey(Palette[0], 0f), new GradientColorKey(Palette[3], 0.4f), new GradientColorKey(Palette[1], 0.75f), new GradientColorKey(Palette[2], 1f) }, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
                main.startColor = new ParticleSystem.MinMaxGradient(grad) { mode = ParticleSystemGradientMode.RandomColor };
                main.maxParticles = 260;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.gravityModifier = -0.004f;
                var em = ps.emission;
                em.rateOverTime = 18f;
                var shape = ps.shape;
                shape.shapeType = ParticleSystemShapeType.Box;
                shape.scale = new Vector3(36f, 6f, 36f);
                var col = ps.colorOverLifetime;
                col.enabled = true;
                var fade = new Gradient();
                fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(1f, 0.75f), new GradientAlphaKey(0f, 1f) });
                col.color = fade;
                var noise = ps.noise;
                noise.enabled = true;
                noise.strength = 0.15f;
                noise.frequency = 0.25f;
                var pr = mg.GetComponent<ParticleSystemRenderer>();
                pr.sharedMaterial = MoteMat(moteTex);
                pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                main.prewarm = true;
                ps.Play();

                log.AppendLine("footprint trail ready");

                EditorSceneManager.MarkSceneDirty(map);
                EditorSceneManager.SaveScene(map);
                ZoneMusic zm = UnityEngine.Object.FindAnyObjectByType<ZoneMusic>();
                log.AppendLine("zone music areas: " + (zm != null ? zm.zones.Count.ToString() : "none"));

                Snapshot(new Vector3(155f, floorY + 1.7f, 0f), new Vector3(185f, floorY + 2f, 0f), "Backups/uvroom_0.png");
                Snapshot(new Vector3(166f, floorY + 1.7f, -12f), new Vector3(176f, floorY + 2.2f, 4f), "Backups/uvroom_1.png");
                Snapshot(new Vector3(182f, floorY + 2.5f, 10f), new Vector3(168f, floorY + 1f, -6f), "Backups/uvroom_2.png");
                log.AppendLine("DONE");
            }
            catch (Exception e)
            {
                log.AppendLine("FAILED: " + e);
            }
            File.WriteAllText(ReportPath, log.ToString());
        }

        static GameObject Quad(Transform parent, string name, Material m, Vector3 pos, Quaternion rot, float size)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = name;
            UnityEngine.Object.DestroyImmediate(q.GetComponent<Collider>());
            q.transform.SetParent(parent, false);
            q.transform.SetPositionAndRotation(pos, rot);
            q.transform.localScale = Vector3.one * size;
            var mr = q.GetComponent<MeshRenderer>();
            mr.sharedMaterial = m;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return q;
        }

        static GameObject Part(Transform parent, string name, Mesh mesh, Material mat, Vector3 pos, Vector3 scale)
        {
            var g = new GameObject(name);
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            g.transform.localScale = scale;
            g.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = g.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return g;
        }

        static Material Lit(string name, Color baseCol, float smooth, Color emission)
        {
            return MatShader(name, "Universal Render Pipeline/Lit", m =>
            {
                m.SetColor("_BaseColor", baseCol);
                m.SetFloat("_Smoothness", smooth);
                m.SetFloat("_Metallic", 0f);
                if (emission.maxColorComponent > 0.001f) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", emission); m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive; }
                else { m.DisableKeyword("_EMISSION"); m.SetColor("_EmissionColor", Color.black); }
            });
        }

        static Material Stone(string name, string tex, Vector2 tile, Color veinColor)
        {
            var ti = AssetImporter.GetAtPath(Dir + "/" + tex + "_normal.png") as TextureImporter;
            if (ti != null && ti.textureType != TextureImporterType.NormalMap) { ti.textureType = TextureImporterType.NormalMap; ti.SaveAndReimport(); }
            return MatShader(name, "Universal Render Pipeline/Lit", m =>
            {
                m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Dir + "/" + tex + ".png"));
                m.SetTextureScale("_BaseMap", tile);
                m.SetColor("_BaseColor", Color.white);
                Texture2D n = AssetDatabase.LoadAssetAtPath<Texture2D>(Dir + "/" + tex + "_normal.png");
                if (n != null) { m.SetTexture("_BumpMap", n); m.SetTextureScale("_BumpMap", tile); m.EnableKeyword("_NORMALMAP"); m.SetFloat("_BumpScale", 0.8f); }
                Texture2D e = AssetDatabase.LoadAssetAtPath<Texture2D>(Dir + "/" + tex + "_emission.png");
                if (e != null) { m.EnableKeyword("_EMISSION"); m.SetTexture("_EmissionMap", e); m.SetTextureScale("_EmissionMap", tile); m.SetColor("_EmissionColor", Color.white * 0.75f); m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive; }
                m.SetFloat("_Smoothness", 0.28f);
                m.SetFloat("_Metallic", 0f);
            });
        }

        static Mesh Obelisk()
        {
            float b = 0.5f, t = 0.32f, shoulder = 0.86f;
            Vector3[] bot = { new Vector3(-b, 0, -b), new Vector3(b, 0, -b), new Vector3(b, 0, b), new Vector3(-b, 0, b) };
            Vector3[] mid = { new Vector3(-t, shoulder, -t), new Vector3(t, shoulder, -t), new Vector3(t, shoulder, t), new Vector3(-t, shoulder, t) };
            Vector3 tip = new Vector3(0f, 1f, 0f);
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var tr = new List<int>();
            for (int i = 0; i < 4; i++)
            {
                int j = (i + 1) % 4, k = v.Count;
                v.Add(bot[i]); v.Add(mid[i]); v.Add(mid[j]); v.Add(bot[j]);
                uv.Add(new Vector2(0, 0)); uv.Add(new Vector2(0, 1)); uv.Add(new Vector2(1, 1)); uv.Add(new Vector2(1, 0));
                tr.AddRange(new[] { k, k + 1, k + 2, k, k + 2, k + 3 });
                k = v.Count;
                v.Add(mid[i]); v.Add(tip); v.Add(mid[j]);
                uv.Add(new Vector2(0, 0)); uv.Add(new Vector2(0.5f, 0.3f)); uv.Add(new Vector2(1, 0));
                tr.AddRange(new[] { k, k + 1, k + 2 });
            }
            var m = new Mesh { name = "UV obelisk" };
            m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(tr, 0);
            m.RecalculateNormals(); m.RecalculateTangents(); m.RecalculateBounds();
            return m;
        }

        static Material Glow(string name, Texture2D tex, Color c, float intensity, Vector2 tiling, Vector2 offset, float pulse)
        {
            return MatShader(name, "CasaFX/UVGlow", m =>
            {
                m.SetTexture("_MainTex", tex);
                m.SetTextureScale("_MainTex", tiling);
                m.SetTextureOffset("_MainTex", offset);
                m.SetColor("_Color", c);
                m.SetFloat("_Intensity", intensity);
                m.SetFloat("_Pulse", pulse);
            });
        }

        static Material MoteMat(Texture2D tex)
        {
            return MatShader("UV_Mote", "Universal Render Pipeline/Particles/Unlit", m =>
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
        }

        static Material MatShader(string name, string shader, Action<Material> setup)
        {
            string path = Dir + "/" + name + ".mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader sh = Shader.Find(shader);
            if (sh == null) throw new Exception("shader missing: " + shader);
            if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, path); }
            else m.shader = sh;
            setup(m);
            EditorUtility.SetDirty(m);
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

        static Mesh Cylinder(int sides)
        {
            var v = new List<Vector3>(); var t = new List<int>();
            for (int i = 0; i < sides; i++)
            {
                float a0 = i / (float)sides * Mathf.PI * 2f, a1 = (i + 1) / (float)sides * Mathf.PI * 2f;
                Vector3 b0 = new Vector3(Mathf.Cos(a0) * 0.5f, 0f, Mathf.Sin(a0) * 0.5f), b1 = new Vector3(Mathf.Cos(a1) * 0.5f, 0f, Mathf.Sin(a1) * 0.5f);
                Vector3 t0 = b0 * 0.8f + Vector3.up, t1 = b1 * 0.8f + Vector3.up;
                int k = v.Count;
                v.Add(b0); v.Add(t0); v.Add(t1); v.Add(b1);
                t.AddRange(new[] { k, k + 1, k + 2, k, k + 2, k + 3 });
            }
            var m = new Mesh { name = "UV stem" };
            m.SetVertices(v); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }

        static Mesh Cap(int sides, int rings)
        {
            var v = new List<Vector3>(); var t = new List<int>();
            for (int r = 0; r <= rings; r++)
            {
                float phi = r / (float)rings * Mathf.PI * 0.5f;
                for (int s = 0; s <= sides; s++)
                {
                    float a = s / (float)sides * Mathf.PI * 2f;
                    v.Add(new Vector3(Mathf.Cos(a) * Mathf.Cos(phi) * 0.5f, Mathf.Sin(phi), Mathf.Sin(a) * Mathf.Cos(phi) * 0.5f));
                }
            }
            for (int r = 0; r < rings; r++)
                for (int s = 0; s < sides; s++)
                {
                    int a = r * (sides + 1) + s, b = a + sides + 1;
                    t.AddRange(new[] { a, b, a + 1, a + 1, b, b + 1 });
                }
            int c = v.Count;
            v.Add(Vector3.zero);
            for (int s = 0; s < sides; s++) t.AddRange(new[] { c, s + 1, s });
            var m = new Mesh { name = "UV cap" };
            m.SetVertices(v); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }

        static Mesh Link()
        {
            var v = new List<Vector3>(); var t = new List<int>();
            int seg = 12, tube = 6;
            for (int i = 0; i <= seg; i++)
            {
                float a = i / (float)seg * Mathf.PI * 2f;
                Vector3 c = new Vector3(Mathf.Cos(a) * 0.45f, Mathf.Sin(a) * 0.8f, 0f);
                Vector3 n = new Vector3(Mathf.Cos(a), Mathf.Sin(a) * 0.55f, 0f).normalized;
                for (int j = 0; j <= tube; j++)
                {
                    float b = j / (float)tube * Mathf.PI * 2f;
                    v.Add(c + (n * Mathf.Cos(b) + Vector3.forward * Mathf.Sin(b)) * 0.14f);
                }
            }
            for (int i = 0; i < seg; i++)
                for (int j = 0; j < tube; j++)
                {
                    int a = i * (tube + 1) + j, b = a + tube + 1;
                    t.AddRange(new[] { a, b, a + 1, a + 1, b, b + 1 });
                }
            var m = new Mesh { name = "UV chain link" };
            m.SetVertices(v); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }

        static Mesh Octahedron()
        {
            Vector3[] p = { Vector3.up, Vector3.down, Vector3.left * 0.6f, Vector3.right * 0.6f, Vector3.forward * 0.6f, Vector3.back * 0.6f };
            int[][] f = { new[] { 0, 4, 3 }, new[] { 0, 3, 5 }, new[] { 0, 5, 2 }, new[] { 0, 2, 4 }, new[] { 1, 3, 4 }, new[] { 1, 5, 3 }, new[] { 1, 2, 5 }, new[] { 1, 4, 2 } };
            var v = new List<Vector3>(); var t = new List<int>();
            foreach (var tri in f) { foreach (int i in tri) { v.Add(p[i]); t.Add(v.Count - 1); } }
            var m = new Mesh { name = "UV artifact" };
            m.SetVertices(v); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds();
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
