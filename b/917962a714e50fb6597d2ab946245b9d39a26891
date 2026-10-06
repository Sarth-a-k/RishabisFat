using UnityEngine;

namespace FPCharacter
{
    public sealed class LavaFlow : MonoBehaviour
    {
        public Vector2 scrollSpeed = new Vector2(0f, -0.35f);
        public float pulseAmount = 0.15f;
        public float pulseSpeed = 1.3f;
        public bool embers;
        public float embersPerSecond = 10f;
        public Vector3 emberArea = new Vector3(1.4f, 0.2f, 0.6f);

        Material mat;
        Color baseEmission;
        Vector2 offset;
        float phase;
        static Material emberMat;

        void Start()
        {
            Renderer r = GetComponent<Renderer>();
            if (r != null)
            {
                mat = r.material;
                baseEmission = mat.HasProperty("_EmissionColor") ? mat.GetColor("_EmissionColor") : Color.white;
            }
            phase = Random.value * 10f;
            if (embers) MakeEmbers();
        }

        void Update()
        {
            if (mat == null) return;
            offset += scrollSpeed * Time.deltaTime;
            offset.x = Mathf.Repeat(offset.x, 1f);
            offset.y = Mathf.Repeat(offset.y, 1f);
            mat.SetTextureOffset("_BaseMap", offset);
            float p = 1f + pulseAmount * Mathf.Sin(Time.time * pulseSpeed + phase);
            mat.SetColor("_EmissionColor", baseEmission * p);
        }

        void MakeEmbers()
        {
            GameObject go = new GameObject("Lava embers");
            go.transform.SetParent(transform, false);
            go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.06f);
            main.startColor = new Color(1f, 0.55f, 0.15f, 1f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 120;
            main.gravityModifier = -0.04f;
            var em = ps.emission;
            em.rateOverTime = embersPerSecond;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = emberArea;
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.4f;
            noise.frequency = 1.1f;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            Gradient g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.85f, 0.45f), 0f), new GradientColorKey(new Color(1f, 0.25f, 0.04f), 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.6f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = EmberMaterial();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            ps.Play();
        }

        static Material EmberMaterial()
        {
            if (emberMat != null) return emberMat;
            const int n = 32;
            Texture2D tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n * 0.5f, n * 0.5f)) / (n * 0.5f);
                    float a = Mathf.Clamp01(1f - d);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
                }
            tex.Apply();
            Shader sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (sh == null) sh = Shader.Find("Sprites/Default");
            emberMat = new Material(sh);
            emberMat.SetTexture("_BaseMap", tex);
            emberMat.SetTexture("_MainTex", tex);
            emberMat.SetFloat("_Surface", 1f);
            emberMat.SetFloat("_Blend", 2f);
            emberMat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            emberMat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            emberMat.SetFloat("_ZWrite", 0f);
            emberMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            emberMat.SetOverrideTag("RenderType", "Transparent");
            emberMat.renderQueue = 3000;
            return emberMat;
        }
    }
}
