using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace FPCharacter
{
    public sealed class InfraredVision : MonoBehaviour
    {
        public static bool Active { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Active = false; }
        public static event Action<bool> Changed;

        public float blendSpeed = 5f;
        public Color hotColor = new Color(1.6f, 1.45f, 0.9f);
        public Color warmColor = new Color(1.4f, 0.45f, 0.08f);
        public Color bodyColor = new Color(1.1f, 0.3f, 0.12f);
        public Color blockerColor = new Color(2.2f, 0.75f, 0.05f);
        public Color prismColor = new Color(4f, 0.18f, 0.08f);
        public Color beamColor = new Color(4f, 0.12f, 0.05f);
        public Color coldDark = new Color(0.05f, 0.015f, 0.14f);
        public Color coldLight = new Color(0.08f, 0.32f, 0.6f);
        public int coldLevels = 6;

        Volume volume;
        UniversalAdditionalCameraData camData;
        bool previousPost;
        float weight;
        bool target;
        Material hotMat, warmMat, bodyMat, blockerMat, prismMat;
        Material[] coldMats;
        readonly Dictionary<Material, Material> beamMats = new Dictionary<Material, Material>();
        readonly List<Renderer> swapped = new List<Renderer>();
        readonly HashSet<Renderer> swappedSet = new HashSet<Renderer>();
        readonly List<Material[]> originals = new List<Material[]>();
        GUIStyle style;

        void Awake()
        {
            camData = GetComponent<UniversalAdditionalCameraData>();
            GameObject vgo = new GameObject("InfraredVolume");
            volume = vgo.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 500f;
            volume.weight = 0f;
            VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
            volume.profile = profile;
            ColorAdjustments ca = profile.Add<ColorAdjustments>(true);
            ca.saturation.Override(25f);
            ca.contrast.Override(30f);
            ca.postExposure.Override(0.2f);
            Vignette vg = profile.Add<Vignette>(true);
            vg.intensity.Override(0.5f);
            vg.smoothness.Override(0.4f);
            vg.color.Override(Color.black);
            FilmGrain fg = profile.Add<FilmGrain>(true);
            fg.type.Override(FilmGrainLookup.Medium3);
            fg.intensity.Override(0.4f);
            Bloom bl = profile.Add<Bloom>(true);
            bl.intensity.Override(1.8f);
            bl.threshold.Override(0.9f);
            bl.scatter.Override(0.7f);

            hotMat = Unlit(hotColor);
            warmMat = Unlit(warmColor);
            bodyMat = Unlit(bodyColor);
            blockerMat = Unlit(blockerColor);
            prismMat = Unlit(prismColor);

            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            coldMats = new Material[Mathf.Max(2, coldLevels)];
            for (int i = 0; i < coldMats.Length; i++)
            {
                Color c = Color.Lerp(coldDark, coldLight, i / (float)(coldMats.Length - 1));
                Material m = new Material(lit);
                m.SetColor("_BaseColor", c);
                m.SetFloat("_Smoothness", 0.1f);
                m.SetFloat("_Metallic", 0f);
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", c * 0.55f);
                coldMats[i] = m;
            }
        }

        static Material Unlit(Color c)
        {
            Material m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            m.SetColor("_BaseColor", c);
            return m;
        }

        public void SetActive(bool on)
        {
            if (target == on) return;
            target = on;
            Active = on;
            if (on)
            {
                if (camData != null)
                {
                    previousPost = camData.renderPostProcessing;
                    camData.renderPostProcessing = true;
                }
                SwapAll();
            }
            else RestoreAll();
            Changed?.Invoke(on);
        }

        void Update()
        {
            weight = Mathf.MoveTowards(weight, target ? 1f : 0f, Time.deltaTime * blendSpeed);
            if (volume != null) volume.weight = weight;
            if (!target && weight <= 0f && camData != null && camData.renderPostProcessing && !previousPost)
                camData.renderPostProcessing = previousPost;
        }

        void SwapAll()
        {
            RestoreAll();

            var hot = new HashSet<Transform>();
            foreach (GhostPresence g in FindObjectsByType<GhostPresence>(FindObjectsInactive.Exclude)) hot.Add(g.transform);
            foreach (Animator a in FindObjectsByType<Animator>(FindObjectsInactive.Exclude))
                if (a.transform.root.name == "RoundTablePuzzle" || a.name.StartsWith("SeatedGhost") || a.name.StartsWith("Ghost")) hot.Add(a.transform);
            var warm = new HashSet<Transform>();
            foreach (HeatSignature h in FindObjectsByType<HeatSignature>(FindObjectsInactive.Exclude))
                (h.heat >= 0.6f ? hot : warm).Add(h.transform);

            FPCharacterMover mover = FindAnyObjectByType<FPCharacterMover>();
            Transform player = mover != null ? mover.transform : transform.root;

            var prismRenderers = new HashSet<Renderer>();
            foreach (PrismOverload o in FindObjectsByType<PrismOverload>(FindObjectsInactive.Exclude))
                if (o.crystal != null) prismRenderers.Add(o.crystal);
            foreach (PrismBeamSplitter s in FindObjectsByType<PrismBeamSplitter>(FindObjectsInactive.Exclude))
                foreach (Renderer r in s.GetComponentsInChildren<Renderer>(true))
                    if (!(r is LineRenderer) && (r.name.Contains("Crystal") || r.name.Contains("Shard"))) prismRenderers.Add(r);

            var torchParts = new Dictionary<Renderer, PuzzleTorch>();
            foreach (PuzzleTorch t in FindObjectsByType<PuzzleTorch>(FindObjectsInactive.Exclude))
                foreach (Renderer r in t.GetComponentsInChildren<Renderer>(true)) torchParts[r] = t;

            foreach (Renderer r in FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (r == null || r is ParticleSystemRenderer || r is TrailRenderer || r is SpriteRenderer) continue;
                if (r is LineRenderer lr) { SwapBeam(lr); continue; }
                if (prismRenderers.Contains(r)) { Swap(r, prismMat); continue; }
                if (r.GetComponentInParent<RingBlocker>() != null) { Swap(r, blockerMat); continue; }
                if (torchParts.TryGetValue(r, out PuzzleTorch torch))
                {
                    if (r.name.StartsWith("Flame") || r.name.StartsWith("Embers")) Swap(r, hotMat);
                    else if (torch.IsLit) Swap(r, warmMat);
                    else Swap(r, Cold(r));
                    continue;
                }
                if (UnderAny(r.transform, hot)) { Swap(r, hotMat); continue; }
                if (UnderAny(r.transform, warm)) { Swap(r, warmMat); continue; }
                if (player != null && r.transform.IsChildOf(player)) { Swap(r, bodyMat); continue; }
                Swap(r, Cold(r));
            }
        }

        static bool UnderAny(Transform t, HashSet<Transform> roots)
        {
            for (Transform p = t; p != null; p = p.parent)
                if (roots.Contains(p)) return true;
            return false;
        }

        Material Cold(Renderer r)
        {
            float lum = 0.4f;
            Material m = r.sharedMaterial;
            if (m != null)
            {
                Color c = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : m.HasProperty("_Color") ? m.GetColor("_Color") : Color.gray;
                lum = Mathf.Clamp01(c.r * 0.3f + c.g * 0.59f + c.b * 0.11f);
            }
            float h = Mathf.Repeat(r.transform.position.y * 0.35f, 0.25f);
            int i = Mathf.Clamp(Mathf.RoundToInt((lum * 0.75f + h) * (coldMats.Length - 1)), 0, coldMats.Length - 1);
            return coldMats[i];
        }

        void SwapBeam(LineRenderer lr)
        {
            if (!swappedSet.Add(lr)) return;
            Material[] src = lr.sharedMaterials;
            Material[] arr = new Material[src.Length];
            for (int i = 0; i < src.Length; i++)
            {
                Material o = src[i];
                if (o == null) { arr[i] = prismMat; continue; }
                if (!beamMats.TryGetValue(o, out Material red))
                {
                    red = new Material(o);
                    if (red.HasProperty("_BaseColor")) red.SetColor("_BaseColor", beamColor);
                    if (red.HasProperty("_Color")) red.SetColor("_Color", beamColor);
                    if (red.HasProperty("_TintColor")) red.SetColor("_TintColor", beamColor);
                    if (red.HasProperty("_EmissionColor")) red.SetColor("_EmissionColor", beamColor);
                    beamMats[o] = red;
                }
                arr[i] = red;
            }
            swapped.Add(lr);
            originals.Add(src);
            lr.sharedMaterials = arr;
        }

        void Swap(Renderer r, Material m)
        {
            if (r == null || !swappedSet.Add(r)) return;
            Material[] src = r.sharedMaterials;
            swapped.Add(r);
            originals.Add(src);
            Material[] arr = new Material[src.Length];
            for (int i = 0; i < arr.Length; i++) arr[i] = m;
            r.sharedMaterials = arr;
        }

        void RestoreAll()
        {
            for (int i = 0; i < swapped.Count; i++)
                if (swapped[i] != null) swapped[i].sharedMaterials = originals[i];
            swapped.Clear();
            swappedSet.Clear();
            originals.Clear();
        }

        void OnDisable()
        {
            if (target) SetActive(false);
        }

        void OnGUI()
        {
            if (weight <= 0.01f) return;
            if (style == null)
            {
                style = new GUIStyle(GUI.skin.label);
                style.fontStyle = FontStyle.Bold;
            }
            style.fontSize = Mathf.Max(12, Screen.height / 45);
            style.normal.textColor = new Color(1f, 0.55f, 0.15f, weight * 0.9f);
            GUI.Label(new Rect(Screen.width * 0.04f, Screen.height * 0.05f, 400f, 40f), "IR  " + (Mathf.Repeat(Time.time, 1f) < 0.5f ? "●" : " ") + "  THERMAL", style);
        }
    }
}
