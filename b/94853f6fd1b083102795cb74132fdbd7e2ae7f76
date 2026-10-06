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
    static class ShadowRunSetup
    {
        const string TriggerPath = "Assets/Interaction/Editor/.run_shadowrun";
        const string ReportPath = "Backups/shadowrun_report.txt";
        const string MapScene = "Assets/Scenes/FourfoldCitadel_WithOurStuff.unity";
        const string MenuScene = "Assets/Scenes/MainMenu.unity";
        const string ShadowScene = "Assets/Scenes/Shadow2D.unity";
        const string Gen = "Assets/Interaction/Generated/EmberGate";
        static readonly Vector3 SunkenCenter = new Vector3(48.85f, 0f, 0f);

        static ShadowRunSetup()
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

        [MenuItem("Tools/Interaction/Setup Shadow Run And Ember Gate")]
        static void Run()
        {
            var log = new StringBuilder();
            try
            {
                EditorSceneManager.SaveOpenScenes();
                Directory.CreateDirectory("Backups");
                File.Copy(MapScene, "Backups/FourfoldCitadel_WithOurStuff_before_shadowrun.unity", true);
                AssetDatabase.Refresh();

                foreach (string n in new[] { "bg", "bg_glow" })
                {
                    var ti = AssetImporter.GetAtPath("Assets/Resources/Shadow2D/" + n + ".png") as TextureImporter;
                    if (ti == null) { log.AppendLine("missing " + n); continue; }
                    ti.textureCompression = TextureImporterCompression.Uncompressed;
                    ti.filterMode = FilterMode.Point;
                    ti.mipmapEnabled = false;
                    ti.npotScale = TextureImporterNPOTScale.None;
                    ti.SaveAndReimport();
                }

                Scene shadow = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var runGo = new GameObject("Shadow Run 2D");
                ShadowRun2D run = runGo.AddComponent<ShadowRun2D>();
                run.restartScene = Path.GetFileNameWithoutExtension(MapScene);
                EditorSceneManager.SaveScene(shadow, ShadowScene);
                log.AppendLine("Shadow2D scene saved, returns to " + run.restartScene);

                Scene map = EditorSceneManager.OpenScene(MapScene, OpenSceneMode.Single);

                FPCharacterMover mover = UnityEngine.Object.FindAnyObjectByType<FPCharacterMover>(FindObjectsInactive.Include);
                if (mover == null) throw new Exception("No player");
                mover.gameObject.tag = "Player";
                log.AppendLine("player " + mover.name + " tagged Player, CharacterController " + (mover.GetComponent<CharacterController>() != null));

                LockedGate gate = null;
                foreach (CombinationLock cl in UnityEngine.Object.FindObjectsByType<CombinationLock>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (cl.gate != null) gate = cl.gate;
                    if (gate != null && gate.transform.IsChildOf(cl.transform))
                    {
                        log.AppendLine("lock contains gate, removing only the lock component and wheels");
                        foreach (Transform w in cl.wheels) if (w != null) UnityEngine.Object.DestroyImmediate(w.gameObject);
                        UnityEngine.Object.DestroyImmediate(cl);
                    }
                    else
                    {
                        log.AppendLine("removed lock " + cl.gameObject.name + " at " + cl.transform.position);
                        UnityEngine.Object.DestroyImmediate(cl.gameObject);
                    }
                }
                if (gate == null)
                {
                    float best = float.MaxValue;
                    foreach (LockedGate g in UnityEngine.Object.FindObjectsByType<LockedGate>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    {
                        float d = Mathf.Abs(g.transform.position.x - 75f);
                        log.AppendLine("gate candidate " + g.name + " at " + g.transform.position);
                        if (d < best) { best = d; gate = g; }
                    }
                }
                if (gate == null) throw new Exception("No Ember gate found");
                gate.isOpen = false;
                EditorUtility.SetDirty(gate);

                Physics.SyncTransforms();
                Bounds gb = RendererBounds(gate.gameObject);
                Vector3 c = gb.center;
                float floorY = gb.min.y;
                if (Physics.Raycast(new Vector3(c.x, gb.max.y, c.z) + Vector3.right * Mathf.Sign(SunkenCenter.x - c.x) * 1.2f, Vector3.down, out RaycastHit fh, 20f, ~0, QueryTriggerInteraction.Ignore))
                    floorY = fh.point.y;
                Vector3 dir = new Vector3(Mathf.Sign(SunkenCenter.x - c.x), 0f, 0f);
                float width = gb.size.z;
                log.AppendLine("gate " + gate.name + " bounds " + gb.center + " size " + gb.size + " floor " + floorY + " sunken side " + dir.x);

                foreach (string old in new[] { "Shadow Run Trigger", "Ember Gate Dressing" })
                {
                    GameObject o = GameObject.Find(old);
                    if (o != null) UnityEngine.Object.DestroyImmediate(o);
                }

                var trig = new GameObject("Shadow Run Trigger");
                trig.transform.position = new Vector3(c.x + dir.x * 2.2f, floorY + 1.6f, c.z);
                BoxCollider box = trig.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.size = new Vector3(1.2f, 3.2f, Mathf.Max(width + 1.5f, 8.5f));
                ComicPanelTransition transition = trig.AddComponent<ComicPanelTransition>();
                transition.nextScene = Path.GetFileNameWithoutExtension(ShadowScene);
                transition.puzzleSolved = false;
                transition.playerMovement = mover;

                var ret = new GameObject("Return Point");
                ret.transform.SetParent(trig.transform, false);
                Vector3 rp = new Vector3(c.x + dir.x * 5.5f, floorY, c.z);
                if (Physics.Raycast(rp + Vector3.up * 2f, Vector3.down, out RaycastHit rh, 6f, ~0, QueryTriggerInteraction.Ignore)) rp.y = rh.point.y;
                ret.transform.SetPositionAndRotation(rp + Vector3.up * 0.12f, Quaternion.LookRotation(-dir, Vector3.up));

                ShadowRunGate srg = trig.AddComponent<ShadowRunGate>();
                srg.gate = gate;
                srg.transition = transition;
                srg.returnPoint = ret.transform;

                int prisms = 0;
                foreach (PrismMoonController p in UnityEngine.Object.FindObjectsByType<PrismMoonController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    p.transition = transition;
                    EditorUtility.SetDirty(p);
                    prisms++;
                }
                log.AppendLine("prism controllers linked: " + prisms);

                BuildDressing(gate, gb, floorY, dir, log);

                EditorSceneManager.MarkSceneDirty(map);
                EditorSceneManager.SaveScene(map);

                var list = new List<EditorBuildSettingsScene>();
                if (File.Exists(MenuScene)) list.Add(new EditorBuildSettingsScene(MenuScene, true));
                list.Add(new EditorBuildSettingsScene(MapScene, true));
                list.Add(new EditorBuildSettingsScene(ShadowScene, true));
                foreach (EditorBuildSettingsScene s in EditorBuildSettings.scenes)
                    if (s.path != MenuScene && s.path != MapScene && s.path != ShadowScene) list.Add(new EditorBuildSettingsScene(s.path, false));
                EditorBuildSettings.scenes = list.ToArray();
                log.AppendLine("build: " + string.Join(" | ", Array.ConvertAll(EditorBuildSettings.scenes, s => s.path + (s.enabled ? "" : " (off)"))));

                Snapshot(ret.transform.position + Vector3.up * 1.55f, c + Vector3.up * 0.3f, "Backups/embergate_snap.png");
                Snapshot(new Vector3(c.x + dir.x * 2.2f, floorY + 1.4f, c.z + width * 0.45f), new Vector3(c.x, floorY + 1.2f, c.z - width * 0.2f), "Backups/embergate_snap2.png");
                log.AppendLine("DONE");
            }
            catch (Exception e)
            {
                log.AppendLine("FAILED: " + e);
            }
            File.WriteAllText(ReportPath, log.ToString());
        }

        static void BuildDressing(LockedGate gate, Bounds gb, float floorY, Vector3 dir, StringBuilder log)
        {
            Directory.CreateDirectory(Gen);
            AssetDatabase.Refresh();
            ConfigureTex(Gen + "/Gate_EmberCrack.png");
            ConfigureTex(Gen + "/Gate_EmberDot.png");
            Material crackMat = AdditiveMaterial(Gen + "/Gate_EmberCrack.mat", AssetDatabase.LoadAssetAtPath<Texture2D>(Gen + "/Gate_EmberCrack.png"), new Color(1.6f, 0.55f, 0.12f, 1f));
            Material emberMat = AdditiveMaterial(Gen + "/Gate_Ember.mat", AssetDatabase.LoadAssetAtPath<Texture2D>(Gen + "/Gate_EmberDot.png"), Color.white);

            string seamPath = Gen + "/Gate_LavaSeam.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(seamPath) == null)
                AssetDatabase.CopyAsset("Assets/Interaction/Generated/LavaCrypt/Crypt_LavaPool.mat", seamPath);
            Material seam = AssetDatabase.LoadAssetAtPath<Material>(seamPath);

            Vector3 c = gb.center;
            float width = gb.size.z;
            Transform parent = gate.transform.parent;
            var root = new GameObject("Ember Gate Dressing");
            if (parent != null) root.transform.SetParent(parent, true);
            var glow = root.AddComponent<EmberGlow>();
            var lights = new List<Light>();
            var cracks = new List<Renderer>();

            if (seam != null)
            {
                seam.SetTextureScale("_BaseMap", new Vector2(Mathf.Max(1f, width / 1.6f), 0.35f));
                EditorUtility.SetDirty(seam);
                GameObject strip = GameObject.CreatePrimitive(PrimitiveType.Cube);
                strip.name = "Lava seam beyond gate";
                UnityEngine.Object.DestroyImmediate(strip.GetComponent<Collider>());
                strip.transform.SetParent(root.transform, true);
                strip.transform.position = new Vector3(c.x - dir.x * 0.9f, floorY + 0.012f, c.z);
                strip.transform.localScale = new Vector3(0.45f, 0.025f, Mathf.Max(1f, width - 0.4f));
                var r = strip.GetComponent<MeshRenderer>();
                r.sharedMaterial = seam;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                LavaFlow lf = strip.AddComponent<LavaFlow>();
                lf.scrollSpeed = new Vector2(0.03f, 0.01f);
                lf.pulseAmount = 0.25f;
                lf.pulseSpeed = 0.9f;
                lf.embers = false;
            }

            Light warm = NewLight(root.transform, "Ember glow beyond gate", new Vector3(c.x - dir.x * 1.8f, floorY + 1.0f, c.z), new Color(1f, 0.42f, 0.12f), 2.6f, 8f);
            Light spill = NewLight(root.transform, "Ember spill under gate", new Vector3(c.x + dir.x * 0.5f, floorY + 0.2f, c.z), new Color(1f, 0.38f, 0.1f), 0.9f, 3f);
            lights.Add(warm);
            lights.Add(spill);

            int placed = 0;
            foreach (float side in new[] { -1f, 1f })
            {
                Vector3 origin = new Vector3(c.x + dir.x * 3f, floorY + 1.5f, c.z + side * (width * 0.5f + 0.55f));
                if (!Physics.Raycast(origin, -dir, out RaycastHit hit, 5f, ~0, QueryTriggerInteraction.Ignore)) continue;
                if (hit.collider.transform.IsChildOf(gate.transform)) continue;
                GameObject q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                q.name = side < 0 ? "Ember crack left" : "Ember crack right";
                UnityEngine.Object.DestroyImmediate(q.GetComponent<Collider>());
                q.transform.SetParent(root.transform, true);
                q.transform.position = new Vector3(hit.point.x, floorY + 1.45f, hit.point.z) + hit.normal * 0.015f;
                q.transform.rotation = Quaternion.LookRotation(-hit.normal, Vector3.up);
                q.transform.localScale = new Vector3(0.9f, 2.9f, 1f);
                var mr = q.GetComponent<MeshRenderer>();
                mr.sharedMaterial = crackMat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                cracks.Add(mr);
                placed++;
            }
            log.AppendLine("ember cracks placed: " + placed);

            var pgo = new GameObject("Embers drifting through the bars");
            pgo.transform.SetParent(root.transform, true);
            pgo.transform.SetPositionAndRotation(new Vector3(c.x - dir.x * 0.6f, floorY + 0.05f, c.z), Quaternion.Euler(-90f, 0f, 0f));
            ParticleSystem ps = pgo.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2.5f, 4.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.25f, 0.7f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.05f);
            main.startColor = new Color(1f, 0.55f, 0.15f, 1f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 60;
            main.gravityModifier = -0.02f;
            var em = ps.emission;
            em.rateOverTime = 5f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(1.4f, Mathf.Max(1f, width - 0.5f), 0.05f);
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(dir.x * 0.05f, dir.x * 0.35f);
            vel.y = new ParticleSystem.MinMaxCurve(0f, 0f);
            vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.35f;
            noise.frequency = 0.8f;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.8f, 0.4f), 0f), new GradientColorKey(new Color(1f, 0.25f, 0.04f), 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0.7f, 0.6f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var pr = pgo.GetComponent<ParticleSystemRenderer>();
            pr.sharedMaterial = emberMat;
            pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            pr.receiveShadows = false;

            glow.lights = lights.ToArray();
            glow.cracks = cracks.ToArray();
        }

        static Light NewLight(Transform parent, string name, Vector3 pos, Color color, float intensity, float range)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, true);
            go.transform.position = pos;
            Light l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.intensity = intensity;
            l.range = range;
            l.shadows = LightShadows.None;
            return l;
        }

        static void ConfigureTex(string path)
        {
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null) return;
            ti.textureType = TextureImporterType.Default;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.alphaIsTransparency = true;
            ti.SaveAndReimport();
        }

        static Material AdditiveMaterial(string path, Texture2D tex, Color color)
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(sh);
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = sh;
            m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", color);
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

        static Bounds RendererBounds(GameObject go)
        {
            Renderer[] rs = go.GetComponentsInChildren<Renderer>(true);
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.one);
            Bounds b = rs[0].bounds;
            foreach (Renderer r in rs) b.Encapsulate(r.bounds);
            return b;
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
