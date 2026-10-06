using UnityEngine;

namespace FPCharacter
{
    public sealed class EmberGlow : MonoBehaviour
    {
        public Light[] lights = new Light[0];
        public Renderer[] cracks = new Renderer[0];
        public float flicker = 0.22f;
        public float speed = 1.8f;
        public Color crackColor = new Color(1.6f, 0.55f, 0.12f, 1f);

        float[] baseIntensity;
        Material[] crackMats;
        float seed;

        void Start()
        {
            seed = Random.value * 50f;
            baseIntensity = new float[lights.Length];
            for (int i = 0; i < lights.Length; i++) if (lights[i] != null) baseIntensity[i] = lights[i].intensity;
            crackMats = new Material[cracks.Length];
            for (int i = 0; i < cracks.Length; i++) if (cracks[i] != null) crackMats[i] = cracks[i].material;
        }

        void Update()
        {
            float t = Time.time * speed + seed;
            float n = Mathf.PerlinNoise(t, 0.3f) * 0.7f + Mathf.PerlinNoise(t * 3.1f, 1.7f) * 0.3f;
            float k = 1f + flicker * (n - 0.5f) * 2f;
            for (int i = 0; i < lights.Length; i++) if (lights[i] != null) lights[i].intensity = baseIntensity[i] * k;
            for (int i = 0; i < crackMats.Length; i++)
            {
                if (crackMats[i] == null) continue;
                float c = 0.75f + 0.25f * Mathf.Sin(Time.time * 0.9f + i * 1.7f) * k;
                crackMats[i].SetColor("_BaseColor", crackColor * c);
            }
        }
    }
}
