using System.Collections;
using UnityEngine;

namespace FPCharacter
{
    public sealed class PrismOverload : MonoBehaviour
    {
        public Renderer crystal;
        public Light glow;
        public PrismBeamSplitter splitter;
        public float duration = 7.5f;
        public float shakeStart = 5.5f;
        public Color startColor = new Color(1f, 0.88f, 0.68f);
        public Color hotColor = new Color(1f, 0.07f, 0.03f);
        public float baseIntensity = 1.5f;
        public float maxIntensity = 10f;
        public int shardCount = 30;
        public float blastSpeed = 3.2f;
        public AudioClip shatterClip;
        public AudioClip chargeLoop;
        [Range(0f, 1f)] public float chargeVolume = 0.45f;
        [Range(0f, 1f)] public float shatterVolume = 1f;

        public float Charge01 => Mathf.Clamp01(charge / Mathf.Max(0.01f, duration));
        public bool Shattered { get; private set; }

        float charge;
        AudioSource chargeSource;
        Material mat;
        Color baseEmission, baseColor;
        Vector3 crystalPos;
        Quaternion crystalRot;

        void Awake()
        {
            if (crystal != null)
            {
                mat = crystal.material;
                baseEmission = mat.GetColor("_EmissionColor");
                baseColor = mat.GetColor("_BaseColor");
                crystalPos = crystal.transform.localPosition;
                crystalRot = crystal.transform.localRotation;
            }
        }

        public void Tick(bool charging, float dt)
        {
            if (Shattered) return;
            charge = charging ? charge + dt : Mathf.Max(0f, charge - dt * 1.5f);
            Apply();
            UpdateChargeSound();
            if (charge >= duration) Shatter();
        }

        void Apply()
        {
            float k = Charge01;
            float heat = k * k;
            Color c = Color.Lerp(startColor, hotColor, Mathf.SmoothStep(0f, 1f, k));
            float pulse = 1f + Mathf.Sin(Time.time * Mathf.Lerp(2f, 22f, k)) * 0.18f * k;
            if (glow != null)
            {
                glow.color = c;
                glow.intensity = Mathf.Lerp(baseIntensity, maxIntensity, heat) * pulse;
                glow.range = Mathf.Lerp(1.5f, 4.5f, k);
            }
            if (mat != null)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", Color.Lerp(baseEmission, c * 8f, heat) * pulse);
                Color tint = Color.Lerp(baseColor, new Color(1f, 0.25f, 0.18f, Mathf.Max(baseColor.a, 0.6f)), k);
                mat.SetColor("_BaseColor", tint);
            }
            if (crystal != null)
            {
                float s = charge > shakeStart ? Mathf.InverseLerp(shakeStart, duration, charge) : 0f;
                float tremble = k > 0.3f ? 0.0015f * k : 0f;
                float amp = tremble + 0.03f * s * s;
                crystal.transform.localPosition = crystalPos + Random.insideUnitSphere * amp;
                crystal.transform.localRotation = crystalRot * Quaternion.Euler(Random.insideUnitSphere * (40f * s * s + 2f * tremble * 100f));
            }
            if (splitter != null) splitter.overload = k;
        }

        void Shatter()
        {
            Shattered = true;
            if (chargeSource != null) chargeSource.Stop();
            if (splitter != null) splitter.Shatter();
            PlayShatterSound();
            if (crystal == null) return;
            Bounds b = crystal.bounds;
            Vector3 center = b.center;
            Material shardMat = new Material(mat);
            shardMat.EnableKeyword("_EMISSION");
            shardMat.SetColor("_EmissionColor", hotColor * 4f);
            for (int i = 0; i < shardCount; i++)
            {
                GameObject g = new GameObject("PrismShard");
                g.AddComponent<MeshFilter>().sharedMesh = ShardMesh();
                g.AddComponent<MeshRenderer>();
                BoxCollider bc = g.AddComponent<BoxCollider>();
                bc.center = new Vector3(0f, 0.05f, 0f);
                bc.size = new Vector3(1f, 0.9f, 0.3f);
                Vector3 r = Random.insideUnitSphere;
                g.transform.position = center + Vector3.Scale(r, b.extents * 0.7f);
                g.transform.rotation = Random.rotation;
                float sz = Random.Range(0.6f, 1.4f);
                g.transform.localScale = new Vector3(Random.Range(0.035f, 0.07f), Random.Range(0.045f, 0.1f), Random.Range(0.025f, 0.05f)) * sz;
                MeshRenderer mr = g.GetComponent<MeshRenderer>();
                mr.sharedMaterial = shardMat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                Rigidbody rb = g.AddComponent<Rigidbody>();
                rb.mass = 0.05f;
                Vector3 dir = (g.transform.position - center + Vector3.up * 0.04f).normalized;
                rb.linearVelocity = (dir + Vector3.up * 0.6f) * blastSpeed * Random.Range(0.6f, 1.3f);
                rb.angularVelocity = Random.insideUnitSphere * 20f;
                g.AddComponent<ShardFade>().life = Random.Range(3.5f, 5.5f);
            }
            crystal.enabled = false;
            StartCoroutine(Flash());
        }

        void UpdateChargeSound()
        {
            float k = Charge01;
            if (k <= 0.001f)
            {
                if (chargeSource != null && chargeSource.isPlaying) chargeSource.Stop();
                return;
            }
            if (chargeSource == null)
            {
                if (chargeLoop == null) chargeLoop = Resources.Load<AudioClip>("Sfx/beam_charge_loop");
                if (chargeLoop == null) return;
                chargeSource = gameObject.AddComponent<AudioSource>();
                chargeSource.clip = chargeLoop;
                chargeSource.loop = true;
                chargeSource.playOnAwake = false;
                chargeSource.spatialBlend = 0.7f;
                chargeSource.rolloffMode = AudioRolloffMode.Linear;
                chargeSource.minDistance = 3f;
                chargeSource.maxDistance = 30f;
            }
            if (!chargeSource.isPlaying) chargeSource.Play();
            chargeSource.volume = chargeVolume * Mathf.Pow(k, 0.7f);
            chargeSource.pitch = 0.75f + 0.85f * k * k;
        }

        void PlayShatterSound()
        {
            if (shatterClip == null) shatterClip = Resources.Load<AudioClip>("Sfx/prism_shatter");
            if (shatterClip == null) return;
            var go = new GameObject("Prism shatter sound");
            go.transform.position = crystal != null ? crystal.bounds.center : transform.position;
            var s = go.AddComponent<AudioSource>();
            s.spatialBlend = 0.75f;
            s.rolloffMode = AudioRolloffMode.Linear;
            s.minDistance = 4f;
            s.maxDistance = 45f;
            s.PlayOneShot(shatterClip, shatterVolume);
            Destroy(go, shatterClip.length + 0.2f);
        }

        static Mesh shardMesh;

        static Mesh ShardMesh()
        {
            if (shardMesh != null) return shardMesh;
            Vector3 a = new Vector3(0f, 0.6f, 0f), b = new Vector3(-0.5f, -0.3f, 0f), c = new Vector3(0.5f, -0.3f, 0f);
            Vector3 f = Vector3.forward * 0.15f;
            var v = new System.Collections.Generic.List<Vector3>();
            var t = new System.Collections.Generic.List<int>();
            void Tri(Vector3 p0, Vector3 p1, Vector3 p2)
            {
                int i = v.Count;
                v.Add(p0); v.Add(p1); v.Add(p2);
                t.Add(i); t.Add(i + 1); t.Add(i + 2);
                t.Add(i); t.Add(i + 2); t.Add(i + 1);
            }
            Tri(a + f, b + f, c + f);
            Tri(a - f, c - f, b - f);
            Tri(a + f, a - f, b - f); Tri(a + f, b - f, b + f);
            Tri(b + f, b - f, c - f); Tri(b + f, c - f, c + f);
            Tri(c + f, c - f, a - f); Tri(c + f, a - f, a + f);
            shardMesh = new Mesh { name = "PrismShardTri" };
            shardMesh.SetVertices(v);
            shardMesh.SetTriangles(t, 0);
            shardMesh.RecalculateNormals();
            shardMesh.RecalculateBounds();
            return shardMesh;
        }

        IEnumerator Flash()
        {
            if (glow == null) yield break;
            glow.enabled = true;
            glow.range = 7f;
            float t = 0f;
            while (t < 1.2f)
            {
                t += Time.deltaTime;
                float k = t / 1.2f;
                glow.color = Color.Lerp(new Color(1f, 0.85f, 0.7f), hotColor, k);
                glow.intensity = Mathf.Lerp(30f, 0f, Mathf.Sqrt(k));
                yield return null;
            }
            glow.enabled = false;
        }
    }

    public sealed class ShardFade : MonoBehaviour
    {
        public float life = 4f;
        Vector3 startScale;

        void Start()
        {
            startScale = transform.localScale;
        }

        void Update()
        {
            life -= Time.deltaTime;
            if (life < 1f) transform.localScale = startScale * Mathf.Max(0f, life);
            if (life <= 0f) Destroy(gameObject);
        }
    }
}
