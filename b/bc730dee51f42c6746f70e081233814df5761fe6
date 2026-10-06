using System.Collections.Generic;
using UnityEngine;

namespace FPCharacter
{
    public sealed class RoomSolvedGlow : MonoBehaviour
    {
        public PrismMoonController prism;
        public Vector3 areaCenter = new Vector3(48.85f, 0f, 0f);
        public Vector2 areaSize = new Vector2(48.1f, 36.4f);
        public float torchBoost = 2.2f;
        public float rampSeconds = 3f;
        public float flashExtra = 0.8f;
        public float flashSeconds = 0.6f;

        [Header("Room light")]
        public Color ambientSky = new Color(0.17f, 0.12f, 0.12f);
        public Color ambientEquator = new Color(0.11f, 0.075f, 0.075f);
        public Color ambientGround = new Color(0.05f, 0.035f, 0.035f);
        public Color fillColor = new Color(1f, 0.78f, 0.72f);
        public float fillLightIntensity = 40f;
        public float fillRange = 42f;
        public float fillHeight = 9f;
        public float roomBlendSeconds = 1.5f;

        readonly List<SunkenPrism.TorchFlame> flames = new List<SunkenPrism.TorchFlame>();
        readonly List<PuzzleTorch> torches = new List<PuzzleTorch>();
        readonly List<float> torchBase = new List<float>();
        float solvedAt = -1f;
        bool instant;
        bool torchesDone;
        Color baseSky, baseEquator, baseGround;
        UnityEngine.Rendering.SphericalHarmonicsL2 baseProbe, litProbe;
        Light fill;
        Transform player;
        float inside;
        bool captured;

        void Start()
        {
            foreach (SunkenPrism.TorchFlame f in FindObjectsByType<SunkenPrism.TorchFlame>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (Inside(f.transform.position)) flames.Add(f);
            foreach (PuzzleTorch t in FindObjectsByType<PuzzleTorch>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (Inside(t.transform.position)) { torches.Add(t); torchBase.Add(t.fireIntensity); }
            baseSky = RenderSettings.ambientSkyColor;
            baseEquator = RenderSettings.ambientEquatorColor;
            baseGround = RenderSettings.ambientGroundColor;
            baseProbe = RenderSettings.ambientProbe;
            litProbe = new UnityEngine.Rendering.SphericalHarmonicsL2();
            litProbe.AddAmbientLight(ambientEquator);
            litProbe.AddDirectionalLight(Vector3.up, (ambientSky - ambientGround) * 0.5f, 1f);
            captured = true;
            var go = new GameObject("Room Solved Fill Light");
            go.transform.SetParent(transform, false);
            go.transform.position = areaCenter + Vector3.up * fillHeight;
            fill = go.AddComponent<Light>();
            fill.type = LightType.Point;
            fill.color = fillColor;
            fill.range = fillRange;
            fill.intensity = 0f;
            fill.shadows = LightShadows.None;
            fill.enabled = false;
        }

        bool Inside(Vector3 p)
        {
            Vector3 d = p - areaCenter;
            return Mathf.Abs(d.x) <= areaSize.x * 0.5f && Mathf.Abs(d.z) <= areaSize.y * 0.5f;
        }

        void Update()
        {
            if (solvedAt < 0f)
            {
                if (prism == null || !prism.Shattered) return;
                solvedAt = Time.time;
                if (Time.time - prism.ShatteredAt > 5f) { solvedAt -= rampSeconds + flashSeconds; instant = true; }
            }
            float t = Time.time - solvedAt;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / Mathf.Max(0.05f, rampSeconds)));
            float flash = instant ? 0f : flashExtra * Mathf.Clamp01(1f - t / Mathf.Max(0.05f, flashSeconds));
            if (!torchesDone)
            {
                float boost = Mathf.Lerp(1f, torchBoost, k) + flash;
                foreach (SunkenPrism.TorchFlame f in flames) if (f != null) f.boost = boost;
                for (int i = 0; i < torches.Count; i++) if (torches[i] != null) torches[i].fireIntensity = torchBase[i] * boost;
                if (k >= 1f && flash <= 0f) torchesDone = true;
            }

            if (player == null)
            {
                FPCharacterMover m = FindAnyObjectByType<FPCharacterMover>();
                if (m != null) player = m.transform;
            }
            float want = player != null && Inside(player.position) ? 1f : 0f;
            inside = instant && t < 0.1f ? want : Mathf.MoveTowards(inside, want, Time.deltaTime / Mathf.Max(0.05f, roomBlendSeconds));
            float w = k * Mathf.SmoothStep(0f, 1f, inside);
            float lift = w * (1f + flash);
            RenderSettings.ambientSkyColor = Color.LerpUnclamped(baseSky, ambientSky, lift);
            RenderSettings.ambientEquatorColor = Color.LerpUnclamped(baseEquator, ambientEquator, lift);
            RenderSettings.ambientGroundColor = Color.LerpUnclamped(baseGround, ambientGround, lift);
            RenderSettings.ambientProbe = baseProbe * (1f - Mathf.Min(1f, lift)) + litProbe * lift;
            fill.intensity = fillLightIntensity * lift;
            fill.enabled = lift > 0.001f;
        }

        void OnDestroy()
        {
            if (!captured) return;
            RenderSettings.ambientProbe = baseProbe;
            RenderSettings.ambientSkyColor = baseSky;
            RenderSettings.ambientEquatorColor = baseEquator;
            RenderSettings.ambientGroundColor = baseGround;
        }
    }
}
