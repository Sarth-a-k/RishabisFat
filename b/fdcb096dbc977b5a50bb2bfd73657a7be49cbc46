using System.Collections.Generic;
using UnityEngine;

namespace FPCharacter
{
    public sealed class AmbientParticles : MonoBehaviour
    {
        [Header("Floating dust")]
        public bool dust = true;
        public float dustPerSecond = 40f;
        public Vector3 dustArea = new Vector3(16f, 6f, 16f);
        public Color dustColor = new Color(1f, 0.9f, 0.75f, 0.35f);
        public float dustSize = 0.025f;

        [Header("Torch embers")]
        public bool embers = true;
        public float embersPerSecond = 5f;
        public Color emberColor = new Color(1f, 0.55f, 0.15f, 1f);
        public float emberSize = 0.03f;

        ParticleSystem dustSystem;
        readonly List<ParticleSystem> emberSystems = new List<ParticleSystem>();
        static Texture2D softDot;
        static Material sharedMat;

        void Start()
        {
            if (dust) dustSystem = MakeDust();
            if (embers)
                foreach (SunkenPrism.TorchFlame f in FindObjectsByType<SunkenPrism.TorchFlame>(FindObjectsSortMode.None))
                    emberSystems.Add(MakeEmbers(f.transform));
        }

        void Update()
        {
            if (dustSystem != null)
            {
                dustSystem.transform.position = transform.position + Vector3.up * dustArea.y * 0.5f;
                var em = dustSystem.emission;
                em.rateOverTime = dust ? dustPerSecond : 0f;
            }
            foreach (ParticleSystem p in emberSystems)
            {
                if (p == null) continue;
                var em = p.emission;
                em.rateOverTime = embers ? embersPerSecond : 0f;
            }
        }

        static Texture2D SoftDot()
        {
            if (softDot != null) return softDot;
            const int n = 32;
            softDot = new Texture2D(n, n, TextureFormat.RGBA32, false);
            softDot.wrapMode = TextureWrapMode.Clamp;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n * 0.5f, n * 0.5f)) / (n * 0.5f);
                    float a = Mathf.Clamp01(1f - d);
                    a = a * a;
                    softDot.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            softDot.Apply();
            return softDot;
        }

        static Material AdditiveMaterial()
        {
            if (sharedMat != null) return sharedMat;
            Shader sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (sh == null) sh = Shader.Find("Sprites/Default");
            Material m = new Material(sh);
            m.SetTexture("_BaseMap", SoftDot());
            m.SetTexture("_MainTex", SoftDot());
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 2f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = 3000;
            sharedMat = m;
            return m;
        }

        ParticleSystem MakeDust()
        {
            GameObject go = new GameObject("Ambient Dust");
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.duration = 5f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 10f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.01f, 0.06f);
            main.startSize = new ParticleSystem.MinMaxCurve(dustSize * 0.5f, dustSize * 1.5f);
            main.startColor = dustColor;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 600;
            main.gravityModifier = -0.002f;
            var em = ps.emission;
            em.rateOverTime = dustPerSecond;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = dustArea;
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.08f;
            noise.frequency = 0.25f;
            noise.scrollSpeed = 0.1f;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            Gradient g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(1f, 0.75f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = AdditiveMaterial();
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            ps.Play();
            return ps;
        }

        ParticleSystem MakeEmbers(Transform torch)
        {
            GameObject go = new GameObject("Torch Embers");
            go.transform.SetParent(torch, false);
            go.transform.localPosition = Vector3.up * 0.15f;
            go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.1f);
            main.startSize = new ParticleSystem.MinMaxCurve(emberSize * 0.5f, emberSize * 1.3f);
            main.startColor = emberColor;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 40;
            main.gravityModifier = -0.05f;
            var em = ps.emission;
            em.rateOverTime = embersPerSecond;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 18f;
            shape.radius = 0.06f;
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.35f;
            noise.frequency = 1.2f;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            Gradient g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.85f, 0.5f), 0f), new GradientColorKey(new Color(1f, 0.3f, 0.05f), 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.6f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = AdditiveMaterial();
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            ps.Play();
            return ps;
        }
    }
}
