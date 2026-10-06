using System.Collections;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace FPCharacter
{
    public sealed class PrismMoonController : MonoBehaviour, ITorchBeamReceiver
    {
        public Renderer prismRenderer;
        public Light prismLight;
        public SpriteRenderer moon;
        public Renderer moonHalo;
        public Light moonLight;
        public Sprite whiteMoon, greenCrescent, blueHalf, redFull;
        public bool flipCrescent = true;
        public Renderer[] leftSigil;
        public Renderer[] upperSigil;
        public Renderer[] rightSigil;
        public float beamGrace = 0.35f;
        public float useDistance = 3.5f;
        public float turnSeconds = 0.7f;
        public Transform rotateTarget;
        public ComicPanelTransition transition;

        public Color green = new Color(0.16f, 1f, 0.28f);
        public Color blue = new Color(0.18f, 0.42f, 1f);
        public Color red = new Color(1f, 0.14f, 0.08f);

        [Header("Shatter")]
        public float vibrateSeconds = 2.4f;
        public float vibrateAmount = 0.035f;
        public int shardCount = 26;
        public float shardForce = 3.5f;
        public AudioClip grindClip;
        [Range(0f, 1f)] public float grindVolume = 0.35f;
        public bool requireAllMirrors = true;
        public AudioClip humClip;
        public AudioClip shatterClip;
        [Range(0f, 1f)] public float sfxVolume = 1f;

        public bool Powered { get; private set; }
        public int CurrentStep { get; private set; } = -1;
        public bool Solved { get; private set; }
        public bool Shattered { get; private set; }
        public float ShatteredAt { get; private set; } = -1f;
        public static bool SolvedThisSession;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { SolvedThisSession = false; }

        float lastHit = -100f;
        bool lastPowered;
        int selected = -1;
        bool turning;
        bool looking;
        Material prismMat;
        Color prismBaseEmission;
        float prismBaseLight;
        Material[][] sigilMats;
        LineRenderer moonBeam;
        MaterialPropertyBlock block;
        bool vibrating;
        MirrorSocket[] sockets;
        AudioSource grindSource;
        AudioSource sfx;
        static Material sparkMat;

        void Start()
        {
            block = new MaterialPropertyBlock();
            if (SolvedThisSession) { Solved = true; selected = 2; }
            if (humClip == null) humClip = Resources.Load<AudioClip>("Sfx/prism_hum");
            if (grindClip == null) grindClip = Resources.Load<AudioClip>("Sfx/prism_grind");
            sockets = FindObjectsByType<MirrorSocket>(FindObjectsInactive.Exclude);
            if (shatterClip == null) shatterClip = Resources.Load<AudioClip>("Sfx/prism_shatter");
            if (prismRenderer != null)
            {
                prismMat = prismRenderer.material;
                prismMat.EnableKeyword("_EMISSION");
                prismBaseEmission = prismMat.HasProperty("_EmissionColor") ? prismMat.GetColor("_EmissionColor") : Color.black;
            }
            if (prismLight != null) prismBaseLight = prismLight.intensity;
            Renderer[][] groups = { leftSigil, upperSigil, rightSigil };
            sigilMats = new Material[3][];
            for (int i = 0; i < 3; i++)
            {
                Renderer[] g = groups[i] ?? new Renderer[0];
                sigilMats[i] = new Material[g.Length];
                for (int j = 0; j < g.Length; j++)
                {
                    if (g[j] == null) continue;
                    Material m = g[j].material;
                    m.EnableKeyword("_EMISSION");
                    sigilMats[i][j] = m;
                }
            }
            GameObject go = new GameObject("Prism to moon beam");
            go.transform.SetParent(transform, false);
            moonBeam = go.AddComponent<LineRenderer>();
            moonBeam.useWorldSpace = true;
            moonBeam.positionCount = 2;
            moonBeam.widthMultiplier = 0.07f;
            moonBeam.numCapVertices = 4;
            moonBeam.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            moonBeam.receiveShadows = false;
            Shader sh = Shader.Find("Universal Render Pipeline/Unlit");
            moonBeam.material = new Material(sh != null ? sh : Shader.Find("Sprites/Default"));
            moonBeam.enabled = false;
            Apply(-1);
            if (Solved) { HidePrism(); Shattered = true; ShatteredAt = Time.time - 100f; Apply(2); }
        }

        public void OnTorchBeamEnter(PuzzleTorch torch, RaycastHit hit) { lastHit = Time.time; }
        public void OnTorchBeamStay(PuzzleTorch torch, RaycastHit hit) { lastHit = Time.time; }
        public void OnTorchBeamExit(PuzzleTorch torch) { }

        void Update()
        {
            Powered = Time.time - lastHit <= beamGrace && (!requireAllMirrors || AllMirrorsAligned());
            looking = false;
            Camera cam = Camera.main;
            if (cam != null && Physics.SphereCast(new Ray(cam.transform.position, cam.transform.forward), 0.12f, out RaycastHit hit, useDistance + 0.3f, ~0, QueryTriggerInteraction.Ignore))
                looking = hit.collider.transform.IsChildOf(transform);
            if (Solved) looking = false;
            if (looking && !turning && InteractPressed()) StartCoroutine(Turn());
            int step = Solved ? 2 : Powered ? selected : -1;
            if (step != CurrentStep || Powered != lastPowered) { CurrentStep = step; lastPowered = Powered; Apply(step); }
            if (!Solved && Powered && CurrentStep == 2)
            {
                Solved = true;
                SolvedThisSession = true;
                if (transition != null) transition.SetPuzzleSolved();
                StartCoroutine(Shatter());
            }
            if (!Shattered && !vibrating) UpdateGlow();
        }

        bool AllMirrorsAligned()
        {
            if (sockets == null || sockets.Length == 0) return true;
            int n = 0;
            foreach (MirrorSocket s in sockets) if (s != null && s.Occupied && s.Aligned) n++;
            return n >= Mathf.Min(3, sockets.Length);
        }

        IEnumerator Turn()
        {
            turning = true;
            if (grindClip != null)
            {
                if (grindSource == null)
                {
                    grindSource = gameObject.AddComponent<AudioSource>();
                    grindSource.playOnAwake = false;
                    grindSource.spatialBlend = 0.8f;
                    grindSource.rolloffMode = AudioRolloffMode.Linear;
                    grindSource.minDistance = 2f;
                    grindSource.maxDistance = 20f;
                }
                grindSource.pitch = Random.Range(0.92f, 1.08f);
                grindSource.PlayOneShot(grindClip, grindVolume);
            }
            Transform t = rotateTarget != null ? rotateTarget : transform;
            Quaternion from = t.rotation;
            Quaternion to = Quaternion.AngleAxis(120f, Vector3.up) * from;
            float k = 0f;
            while (k < 1f)
            {
                k += Time.deltaTime / Mathf.Max(0.01f, turnSeconds);
                t.rotation = Quaternion.Slerp(from, to, Mathf.SmoothStep(0f, 1f, k));
                yield return null;
            }
            t.rotation = to;
            selected = (selected + 1) % 3;
            turning = false;
        }

        IEnumerator Shatter()
        {
            while (turning) yield return null;
            vibrating = true;
            Transform shakeT = prismRenderer != null ? prismRenderer.transform : null;
            Vector3 rest = shakeT != null ? shakeT.localPosition : Vector3.zero;
            Vector3 center = prismRenderer != null ? prismRenderer.bounds.center : transform.position;
            if (sfx == null)
            {
                var go = new GameObject("Prism sfx");
                go.transform.SetParent(transform, false);
                go.transform.position = center;
                sfx = go.AddComponent<AudioSource>();
                sfx.playOnAwake = false;
                sfx.spatialBlend = 0.75f;
                sfx.rolloffMode = AudioRolloffMode.Linear;
                sfx.minDistance = 4f;
                sfx.maxDistance = 45f;
            }
            if (humClip != null) { sfx.clip = humClip; sfx.volume = sfxVolume; sfx.Play(); }
            float baseLight = prismLight != null ? Mathf.Max(prismBaseLight * 1.7f, 2.5f) : 0f;
            for (float t = 0f; t < vibrateSeconds; t += Time.deltaTime)
            {
                float p = t / Mathf.Max(0.05f, vibrateSeconds);
                float amp = vibrateAmount * Mathf.Pow(p, 1.4f);
                float speed = 30f + 60f * p;
                if (shakeT != null)
                {
                    Vector3 j = new Vector3(Mathf.PerlinNoise(t * speed, 1.3f) - 0.5f, Mathf.PerlinNoise(4.1f, t * speed) - 0.5f, Mathf.PerlinNoise(t * speed, 8.7f) - 0.5f) * 2f;
                    shakeT.localPosition = rest + shakeT.parent.InverseTransformVector(j * amp);
                }
                float flick = 0.75f + 0.25f * Mathf.Sin(t * (10f + 50f * p));
                if (prismMat != null) prismMat.SetColor("_EmissionColor", red * (1.6f + 5f * p * p) * flick);
                if (prismLight != null) { prismLight.color = red; prismLight.intensity = baseLight * (1f + 2.5f * p * p) * flick; }
                yield return null;
            }
            if (shakeT != null) shakeT.localPosition = rest;

            if (sfx.isPlaying) StartCoroutine(FadeOutHum(0.15f));
            if (shatterClip != null) sfx.PlayOneShot(shatterClip, sfxVolume);
            Bounds b = prismRenderer != null ? prismRenderer.bounds : new Bounds(center, Vector3.one * 0.4f);
            HidePrism();
            SpawnShards(b);
            SpawnSparks(b.center);
            Shattered = true;
            ShatteredAt = Time.time;
            vibrating = false;
            Apply(2);
            if (prismLight != null)
            {
                prismLight.enabled = true;
                prismLight.color = Color.Lerp(red, Color.white, 0.4f);
                float range = prismLight.range;
                prismLight.range = range * 2f;
                for (float t = 0f; t < 0.6f; t += Time.deltaTime)
                {
                    prismLight.intensity = Mathf.Lerp(14f, 0f, t / 0.6f);
                    yield return null;
                }
                prismLight.range = range;
                prismLight.enabled = false;
            }
        }

        IEnumerator FadeOutHum(float seconds)
        {
            AudioSource hum = sfx;
            float v = hum.volume;
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                hum.volume = Mathf.Lerp(v, 0f, t / seconds);
                yield return null;
            }
            hum.Stop();
            hum.clip = null;
            hum.volume = sfxVolume;
        }

        void HidePrism()
        {
            if (prismRenderer == null) return;
            foreach (Renderer r in prismRenderer.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
            foreach (Collider c in prismRenderer.GetComponentsInChildren<Collider>(true)) c.enabled = false;
            if (prismLight != null && !Shattered && !vibrating) prismLight.enabled = false;
            if (moonBeam != null) moonBeam.enabled = false;
        }

        void SpawnShards(Bounds b)
        {
            Material mat = prismMat != null ? new Material(prismMat) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", red * 3f);
            float scale = Mathf.Max(0.05f, b.extents.magnitude);
            var root = new GameObject("Prism shards");
            var rends = new Renderer[shardCount];
            for (int i = 0; i < shardCount; i++)
            {
                var go = new GameObject("Shard");
                go.layer = 2;
                go.transform.SetParent(root.transform, false);
                go.transform.position = b.center + Vector3.Scale(Random.insideUnitSphere, b.extents * 0.7f);
                go.transform.rotation = Random.rotation;
                Mesh m = ShardMesh(scale * Random.Range(0.12f, 0.32f));
                go.AddComponent<MeshFilter>().sharedMesh = m;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                rends[i] = mr;
                var mc = go.AddComponent<MeshCollider>();
                mc.sharedMesh = m;
                mc.convex = true;
                var rb = go.AddComponent<Rigidbody>();
                rb.mass = 0.05f;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                Vector3 dir = (go.transform.position - b.center).normalized;
                if (dir.sqrMagnitude < 0.01f) dir = Random.onUnitSphere;
                rb.linearVelocity = (dir + Vector3.up * 0.6f).normalized * shardForce * Random.Range(0.5f, 1.2f);
                rb.angularVelocity = Random.insideUnitSphere * 15f;
            }
            StartCoroutine(SettleShards(root, mat));
        }

        IEnumerator SettleShards(GameObject root, Material mat)
        {
            for (float t = 0f; t < 3f; t += Time.deltaTime)
            {
                mat.SetColor("_EmissionColor", red * Mathf.Lerp(3f, 0.25f, t / 3f));
                yield return null;
            }
            yield return new WaitForSeconds(3f);
            if (root == null) yield break;
            foreach (Rigidbody rb in root.GetComponentsInChildren<Rigidbody>()) Destroy(rb);
            foreach (Collider c in root.GetComponentsInChildren<Collider>()) Destroy(c);
        }

        static Mesh ShardMesh(float size)
        {
            Vector3 a = new Vector3(0f, size * Random.Range(0.8f, 1.6f), 0f);
            Vector3 b0 = Quaternion.Euler(0f, 0f, 0f) * new Vector3(size * Random.Range(0.3f, 0.6f), 0f, 0f);
            Vector3 c0 = Quaternion.Euler(0f, Random.Range(100f, 140f), 0f) * new Vector3(size * Random.Range(0.3f, 0.6f), 0f, 0f);
            Vector3 d0 = Quaternion.Euler(0f, Random.Range(220f, 260f), 0f) * new Vector3(size * Random.Range(0.3f, 0.6f), 0f, 0f);
            Vector3 e = new Vector3(0f, -size * Random.Range(0.2f, 0.6f), 0f);
            Vector3[] p = { a, b0, c0, d0, e };
            int[][] faces = { new[] { 0, 2, 1 }, new[] { 0, 3, 2 }, new[] { 0, 1, 3 }, new[] { 4, 1, 2 }, new[] { 4, 2, 3 }, new[] { 4, 3, 1 } };
            var verts = new Vector3[18];
            var tris = new int[18];
            for (int f = 0; f < 6; f++)
                for (int k = 0; k < 3; k++) { verts[f * 3 + k] = p[faces[f][k]]; tris[f * 3 + k] = f * 3 + k; }
            var mesh = new Mesh { name = "Prism shard" };
            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        void SpawnSparks(Vector3 at)
        {
            var go = new GameObject("Prism sparks");
            go.transform.position = at;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 0.2f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.07f);
            main.startColor = new ParticleSystem.MinMaxGradient(red, Color.Lerp(red, Color.white, 0.7f));
            main.gravityModifier = 0.5f;
            main.maxParticles = 200;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.stopAction = ParticleSystemStopAction.Destroy;
            var em = ps.emission;
            em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 140) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.1f;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            Gradient g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.5f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = SparkMaterial();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ps.Play();
        }

        static Material SparkMaterial()
        {
            if (sparkMat != null) return sparkMat;
            const int n = 32;
            Texture2D tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n * 0.5f, n * 0.5f)) / (n * 0.5f);
                    float a = Mathf.Clamp01(1f - d);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a * a));
                }
            tex.Apply();
            Shader sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (sh == null) sh = Shader.Find("Sprites/Default");
            sparkMat = new Material(sh);
            sparkMat.SetTexture("_BaseMap", tex);
            sparkMat.SetTexture("_MainTex", tex);
            sparkMat.SetFloat("_Surface", 1f);
            sparkMat.SetFloat("_Blend", 2f);
            sparkMat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            sparkMat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            sparkMat.SetFloat("_ZWrite", 0f);
            sparkMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            sparkMat.EnableKeyword("_BLENDMODE_ADD");
            sparkMat.SetOverrideTag("RenderType", "Transparent");
            sparkMat.renderQueue = 3000;
            return sparkMat;
        }

        static bool InteractPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.E);
#endif
        }

        void OnGUI()
        {
            if (!looking || turning) return;
            PromptBox.Draw(Powered ? "[E] Rotate the prism" : "[E] Rotate the prism  (it needs light)", 0.66f);
        }

        Color StepColor(int step)
        {
            return step == 0 ? green : step == 1 ? blue : step == 2 ? red : Color.white;
        }

        void Apply(int step)
        {
            if (moon != null)
            {
                Sprite s = step == 0 ? greenCrescent : step == 1 ? blueHalf : step == 2 ? redFull : whiteMoon;
                if (s != null) moon.sprite = s;
                moon.flipX = step == 0 && flipCrescent;
                float b = step >= 0 ? 1f : Powered ? 0.5f : 0.2f;
                moon.color = new Color(b, b * 0.985f, b * 0.95f, 1f);
            }
            Color c = StepColor(step);
            if (moonLight != null)
            {
                moonLight.color = c;
                moonLight.intensity = step >= 0 ? 1.6f : Powered ? 0.6f : 0.25f;
            }
            if (moonHalo != null)
            {
                moonHalo.GetPropertyBlock(block);
                Color h = c;
                h.a = step >= 0 ? 0.2f : Powered ? 0.07f : 0.025f;
                block.SetColor("_Color", h);
                block.SetColor("_BaseColor", h);
                moonHalo.SetPropertyBlock(block);
            }
            int lit = step == 0 ? 0 : step == 1 ? 2 : step == 2 ? 1 : -1;
            for (int i = 0; i < 3; i++)
            {
                Color sc = i == lit ? c : new Color(0.14f, 0.135f, 0.12f);
                Color em = i == lit ? c * 2.2f : Color.black;
                foreach (Material m in sigilMats[i])
                {
                    if (m == null) continue;
                    if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", sc);
                    if (m.HasProperty("_Color")) m.SetColor("_Color", sc);
                    m.SetColor("_EmissionColor", em);
                }
            }
            if (moonBeam != null)
            {
                moonBeam.enabled = Powered && !Shattered;
                Color bc = step >= 0 ? c : new Color(1f, 0.95f, 0.85f);
                moonBeam.material.SetColor("_BaseColor", bc * 1.5f);
                if (moon != null && prismRenderer != null)
                {
                    moonBeam.SetPosition(0, prismRenderer.bounds.center);
                    moonBeam.SetPosition(1, moon.transform.position - moon.transform.forward * 0.05f);
                }
            }
        }

        void UpdateGlow()
        {
            float pulse = 0.85f + 0.15f * Mathf.Sin(Time.time * 3f);
            Color c = CurrentStep >= 0 ? StepColor(CurrentStep) : new Color(1f, 0.95f, 0.85f);
            if (prismMat != null)
                prismMat.SetColor("_EmissionColor", Powered ? c * 1.6f * pulse : prismBaseEmission);
            if (prismLight != null)
            {
                prismLight.intensity = Powered ? Mathf.Max(prismBaseLight * 1.7f, 2.5f) * pulse : prismBaseLight;
                prismLight.color = Powered ? c : new Color(1f, 0.95f, 0.88f);
            }
        }
    }
}
