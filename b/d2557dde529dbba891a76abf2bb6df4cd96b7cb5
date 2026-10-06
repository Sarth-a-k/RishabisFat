using UnityEngine;

namespace FPCharacter
{
    public sealed class LockedGate : MonoBehaviour
    {
        public bool isOpen;
        public float raiseHeight = 8f;
        public float rumbleBeforeLift = 0.9f;
        public float liftSeconds = 7f;
        public float shakeAmount = 0.035f;
        public float shakeTilt = 0.5f;
        public float shakeSpeed = 22f;
        public bool dust = true;
        public AudioClip openSound;
        [Range(0f, 1f)] public float soundVolume = 0.55f;
        public float soundMaxDistance = 40f;

        Vector3 closedLocal;
        Quaternion closedRot;
        bool ready;
        float openedAt = -1f;
        float lift;
        ParticleSystem dustSystem;
        AudioSource audioSource;
        static Material dustMat;

        void Awake()
        {
            closedLocal = transform.localPosition;
            closedRot = transform.localRotation;
            ready = true;
        }

        public void Open() { isOpen = true; }
        public void Close() { isOpen = false; openedAt = -1f; }

        void Update()
        {
            if (!ready) return;
            if (isOpen && openedAt < 0f)
            {
                openedAt = Time.time;
                if (dust) StartDust();
                PlayOpenSound();
            }
            float shake = 0f;
            if (isOpen)
            {
                float t = Time.time - openedAt;
                if (t < rumbleBeforeLift) shake = Mathf.Lerp(0.4f, 1f, t / Mathf.Max(0.01f, rumbleBeforeLift));
                else
                {
                    float k = Mathf.Clamp01((t - rumbleBeforeLift) / Mathf.Max(0.1f, liftSeconds));
                    float jolt = Mathf.Pow(Mathf.Abs(Mathf.Sin(k * Mathf.PI * 9f)), 6f);
                    float eased = k * k * (3f - 2f * k);
                    lift = Mathf.Max(lift, eased - jolt * 0.004f * (1f - k));
                    shake = (1f - k) * 0.85f + jolt * 0.6f * (1f - k) + 0.1f;
                    if (k >= 1f) { shake = 0f; StopDust(); }
                }
            }
            else
            {
                lift = Mathf.MoveTowards(lift, 0f, Time.deltaTime / Mathf.Max(0.1f, liftSeconds));
            }

            Vector3 offset = Vector3.zero;
            Quaternion tilt = Quaternion.identity;
            if (shake > 0f)
            {
                float s = Time.time * shakeSpeed;
                offset = new Vector3(Mathf.PerlinNoise(s, 0.3f) - 0.5f, (Mathf.PerlinNoise(0.7f, s) - 0.5f) * 0.4f, Mathf.PerlinNoise(s, 5.1f) - 0.5f) * 2f * shakeAmount * shake;
                tilt = Quaternion.Euler((Mathf.PerlinNoise(s, 9.2f) - 0.5f) * 2f * shakeTilt * shake, 0f, (Mathf.PerlinNoise(3.3f, s) - 0.5f) * 2f * shakeTilt * shake);
            }
            transform.localPosition = closedLocal + Vector3.up * raiseHeight * lift + offset;
            transform.localRotation = closedRot * tilt;
        }

        void PlayOpenSound()
        {
            if (openSound == null) openSound = Resources.Load<AudioClip>("Sfx/gate_open");
            if (openSound == null) return;
            if (audioSource == null)
            {
                Bounds b = new Bounds(transform.position, Vector3.zero);
                foreach (Renderer r in GetComponentsInChildren<Renderer>()) b.Encapsulate(r.bounds);
                var go = new GameObject("Gate sound");
                go.transform.position = new Vector3(b.center.x, b.min.y + 1.5f, b.center.z);
                audioSource = go.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 1f;
                audioSource.rolloffMode = AudioRolloffMode.Linear;
                audioSource.minDistance = 4f;
                audioSource.maxDistance = soundMaxDistance;
                audioSource.dopplerLevel = 0f;
            }
            audioSource.clip = openSound;
            audioSource.volume = soundVolume;
            audioSource.Play();
        }

        void StartDust()
        {
            if (dustSystem == null)
            {
                Bounds b = new Bounds(transform.position, Vector3.zero);
                foreach (Renderer r in GetComponentsInChildren<Renderer>()) b.Encapsulate(r.bounds);
                GameObject go = new GameObject("Gate dust");
                go.transform.position = new Vector3(b.center.x, b.min.y + 0.1f, b.center.z);
                dustSystem = go.AddComponent<ParticleSystem>();
                dustSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = dustSystem.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 3f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.8f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.7f);
                main.startColor = new Color(0.45f, 0.4f, 0.33f, 0.35f);
                main.gravityModifier = -0.02f;
                main.maxParticles = 300;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                var em = dustSystem.emission;
                em.rateOverTime = 45f;
                var shape = dustSystem.shape;
                shape.shapeType = ParticleSystemShapeType.Box;
                shape.scale = new Vector3(Mathf.Max(0.5f, b.size.x), 0.2f, Mathf.Max(0.5f, b.size.z));
                var col = dustSystem.colorOverLifetime;
                col.enabled = true;
                Gradient g = new Gradient();
                g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                          new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0f, 1f) });
                col.color = g;
                var size = dustSystem.sizeOverLifetime;
                size.enabled = true;
                size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.6f));
                var r2 = go.GetComponent<ParticleSystemRenderer>();
                r2.sharedMaterial = DustMaterial();
                r2.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            dustSystem.Play();
        }

        void StopDust()
        {
            if (dustSystem != null && dustSystem.isEmitting) dustSystem.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        static Material DustMaterial()
        {
            if (dustMat != null) return dustMat;
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
            dustMat = new Material(sh);
            dustMat.SetTexture("_BaseMap", tex);
            dustMat.SetTexture("_MainTex", tex);
            dustMat.SetFloat("_Surface", 1f);
            dustMat.SetFloat("_Blend", 0f);
            dustMat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            dustMat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            dustMat.SetFloat("_ZWrite", 0f);
            dustMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            dustMat.SetOverrideTag("RenderType", "Transparent");
            dustMat.renderQueue = 3000;
            return dustMat;
        }
    }
}
