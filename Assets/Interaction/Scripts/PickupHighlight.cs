using System.Collections.Generic;
using UnityEngine;

namespace FPCharacter
{
    public sealed class PickupHighlight : MonoBehaviour
    {
        public Material glowMaterial;
        public Material sparkleMaterial;
        public Color color = new Color(1f, 0.78f, 0.4f, 1f);
        [Range(0f, 3f)] public float intensity = 0.6f;
        [Range(0f, 1f)] public float pulseMin = 0.35f;
        public float pulseSpeed = 2f;
        public float nearDistance = 3f;
        public float farDistance = 14f;
        [Range(0f, 1f)] public float farStrength = 0.25f;
        public float flashInterval = 4.5f;
        public float flashDuration = 0.9f;
        [Range(0f, 3f)] public float flashIntensity = 1.4f;
        public bool sparkles = true;
        public float sparklesPerSecond = 2f;

        MirrorPickup mirror;
        PuzzleTorch torch;
        MirrorSocket socket;
        readonly List<Renderer> shells = new List<Renderer>();
        readonly List<GameObject> shellObjects = new List<GameObject>();
        readonly List<Renderer> sources = new List<Renderer>();
        MaterialPropertyBlock block;
        ParticleSystem sparkleSystem;
        float shown;
        float seed;
        bool infrared;
        Transform cam;

        void Start()
        {
            mirror = GetComponent<MirrorPickup>();
            torch = GetComponent<PuzzleTorch>();
            socket = GetComponent<MirrorSocket>();
            block = new MaterialPropertyBlock();
            seed = Random.value * 10f;
            foreach (Renderer r in GetComponentsInChildren<Renderer>())
            {
                if (r is ParticleSystemRenderer || r is LineRenderer || r is TrailRenderer) continue;
                if (r.name.StartsWith("Flame") || r.name.StartsWith("Embers")) continue;
                if (socket != null && r.GetComponentInParent<MirrorPickup>() != null) continue;
                sources.Add(r);
            }
            BuildShells();
            if (sparkles && sparkleMaterial != null) BuildSparkles();
            InfraredVision.Changed += OnInfrared;
            OnInfrared(InfraredVision.Active);
        }

        void OnDestroy()
        {
            InfraredVision.Changed -= OnInfrared;
        }

        void OnInfrared(bool on)
        {
            infrared = on;
            foreach (GameObject g in shellObjects) if (g != null) g.SetActive(!on);
        }

        void BuildShells()
        {
            if (glowMaterial == null) return;
            foreach (MeshFilter mf in GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                MeshRenderer src = mf.GetComponent<MeshRenderer>();
                if (src == null || !src.enabled) continue;
                string n = mf.name;
                if (n.StartsWith("Flame") || n.StartsWith("Embers") || n.StartsWith("Glow shell")) continue;
                if (socket != null && mf.GetComponentInParent<MirrorPickup>() != null) continue;
                var g = new GameObject("Glow shell");
                g.transform.SetParent(mf.transform, false);
                g.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
                var r = g.AddComponent<MeshRenderer>();
                Material[] mats = new Material[mf.sharedMesh.subMeshCount];
                for (int i = 0; i < mats.Length; i++) mats[i] = glowMaterial;
                r.sharedMaterials = mats;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
                shells.Add(r);
                shellObjects.Add(g);
            }
        }

        void BuildSparkles()
        {
            Bounds b = WorldBounds();
            var g = new GameObject("Pickup sparkles");
            g.transform.SetParent(transform, false);
            g.transform.position = b.center;
            sparkleSystem = g.AddComponent<ParticleSystem>();
            sparkleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = sparkleSystem.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.055f);
            main.startColor = color;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 20;
            main.gravityModifier = -0.03f;
            var em = sparkleSystem.emission;
            em.rateOverTime = sparklesPerSecond;
            var shape = sparkleSystem.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = b.size;
            var col = sparkleSystem.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(color, 1f) },
                         new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(0f, 1f) });
            col.color = grad;
            var pr = g.GetComponent<ParticleSystemRenderer>();
            pr.sharedMaterial = sparkleMaterial;
            pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            pr.receiveShadows = false;
            sparkleSystem.Play();
        }

        Bounds WorldBounds()
        {
            bool any = false;
            Bounds b = new Bounds(transform.position, Vector3.one * 0.3f);
            foreach (Renderer r in sources)
            {
                if (r == null) continue;
                if (!any) { b = r.bounds; any = true; }
                else b.Encapsulate(r.bounds);
            }
            return b;
        }

        bool Wanted()
        {
            if (mirror != null && mirror.IsCarried) return false;
            if (torch != null && torch.IsLit) return false;
            if (socket != null && socket.Occupied) return false;
            return true;
        }

        void Update()
        {
            if (cam == null && Camera.main != null) cam = Camera.main.transform;
            bool want = Wanted() && !infrared;
            shown = Mathf.MoveTowards(shown, want ? 1f : 0f, Time.deltaTime * 2.5f);

            float distK = 1f;
            Bounds b = WorldBounds();
            if (cam != null)
            {
                float d = Vector3.Distance(cam.position, b.center);
                distK = Mathf.Lerp(1f, farStrength, Mathf.InverseLerp(nearDistance, farDistance, d));
                if (d > farDistance * 1.6f) distK = 0f;
            }

            float t = Time.time + seed;
            float pulse = Mathf.Lerp(pulseMin, 1f, (Mathf.Sin(t * pulseSpeed) + 1f) * 0.5f);
            float k = intensity * pulse * distK * shown;

            float cycle = Mathf.Repeat(t, Mathf.Max(flashDuration + 0.1f, flashInterval));
            float sweepY = -10000f;
            if (cycle < flashDuration)
            {
                float f = cycle / flashDuration;
                sweepY = Mathf.Lerp(b.min.y - 0.15f, b.max.y + 0.15f, f);
            }

            bool visible = k > 0.001f || (sweepY > -9999f && shown * distK > 0.001f);
            block.SetColor("_Color", color);
            block.SetFloat("_Intensity", k);
            block.SetFloat("_SweepY", sweepY);
            block.SetFloat("_SweepWidth", Mathf.Max(0.06f, b.size.y * 0.12f));
            block.SetFloat("_SweepIntensity", flashIntensity * distK * shown);
            foreach (Renderer r in shells)
            {
                if (r == null) continue;
                r.enabled = visible;
                r.SetPropertyBlock(block);
            }

            if (sparkleSystem != null)
            {
                var em = sparkleSystem.emission;
                em.enabled = shown > 0.5f && distK > 0.2f;
                sparkleSystem.transform.position = b.center;
            }
        }
    }
}
