using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace FPCharacter
{
    public sealed class UVBaton : MonoBehaviour
    {
        public GameObject model;
        public Light beam;
        public Renderer glowTube;
        public AudioClip toggleSound;
        public float range = 9f;
        public float halfAngle = 7f;
        public float beamAngle = 24f;
        public Vector3 gripLocal = new Vector3(0f, -0.085f, 0f);
        public Vector3 tipLocal = new Vector3(0f, 0.33f, 0f);
        public Color tubeOn = new Color(0.62f, 0.32f, 1f) * 5f;
        public Light halo;
        public Vector3 slant = new Vector3(0.42f, 0.62f, 0.75f);

        public static bool Active { get; private set; }
        static UVBaton current;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Active = false; current = null; }

        AudioSource audioSource;
        MaterialPropertyBlock block;
        float sway;
        int heldFrame = -10;
        Vector3 restLocalPos;
        Quaternion restLocalRot;
        Vector3 beamLocalPos;
        Material tubeMat;
        Color tubeBase;
        MeshRenderer glowSprite;

        static Texture2D MakeGlowTex()
        {
            const int n = 64;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                    float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                    t.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
                }
            t.Apply();
            t.wrapMode = TextureWrapMode.Clamp;
            return t;
        }

        void Start()
        {
            current = this;
            block = new MaterialPropertyBlock();
            if (toggleSound == null) toggleSound = Resources.Load<AudioClip>("Sfx/uv_baton_on");
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
            audioSource.volume = 0.5f;
            if (model != null) { restLocalPos = model.transform.localPosition; restLocalRot = model.transform.localRotation; }
            if (glowTube != null)
            {
                tubeMat = glowTube.material;
                tubeMat.EnableKeyword("_EMISSION");
                tubeMat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                tubeBase = tubeMat.HasProperty("_BaseColor") ? tubeMat.GetColor("_BaseColor") : Color.white;
                var gs = GameObject.CreatePrimitive(PrimitiveType.Quad);
                gs.name = "Tube glow";
                Destroy(gs.GetComponent<Collider>());
                gs.transform.SetParent(glowTube.transform.parent, false);
                gs.transform.localPosition = glowTube.transform.localPosition;
                gs.transform.localScale = new Vector3(0.78f, 0.17f, 1f);
                glowSprite = gs.GetComponent<MeshRenderer>();
                glowSprite.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                Shader add = Shader.Find("CasaFX/UVGlow");
                if (add != null)
                {
                    var gm = new Material(add);
                    gm.SetTexture("_MainTex", MakeGlowTex());
                    gm.SetColor("_Color", new Color(0.6f, 0.3f, 1f));
                    gm.SetFloat("_Intensity", 1.6f);
                    gm.SetFloat("_Pulse", 0.15f);
                    gm.SetFloat("_Speed", 9f);
                    glowSprite.sharedMaterial = gm;
                }
                else glowSprite.enabled = false;
            }
            if (halo == null && model != null)
            {
                var hg = new GameObject("Baton glow");
                hg.transform.SetParent(model.transform, false);
                hg.transform.localPosition = new Vector3(0f, 0.16f, 0f);
                halo = hg.AddComponent<Light>();
                halo.type = LightType.Point;
                halo.color = new Color(0.6f, 0.3f, 1f);
                halo.intensity = 3f;
                halo.range = 2.6f;
                halo.shadows = LightShadows.None;
            }
            if (beam != null) beamLocalPos = beam.transform.localPosition;
            SetActive(false, false);
        }

        public static void PutAway()
        {
            if (current != null && Active) current.SetActive(false, false);
        }

        public static void Hold(Vector3 grip, Transform cam)
        {
            if (current == null || current.model == null || cam == null) return;
            current.heldFrame = Time.frameCount;
            Vector3 dir = (cam.forward * current.slant.z + cam.up * current.slant.y + cam.right * current.slant.x).normalized;
            Quaternion rot = Quaternion.LookRotation(Vector3.ProjectOnPlane(-cam.forward, dir).sqrMagnitude > 1e-4f ? Vector3.ProjectOnPlane(cam.up, dir) : cam.right, dir);
            current.model.transform.SetPositionAndRotation(grip - rot * current.gripLocal, rot);
            if (current.beam != null)
                current.beam.transform.SetPositionAndRotation(current.model.transform.position + rot * current.tipLocal, Quaternion.LookRotation(cam.forward, cam.up));
        }

        void LateUpdate()
        {
            if (!Active || model == null) return;
            if (Time.frameCount - heldFrame <= 1) return;
            model.transform.localPosition = restLocalPos;
            model.transform.localRotation = restLocalRot;
            if (beam != null) { beam.transform.localPosition = beamLocalPos; beam.transform.localRotation = Quaternion.identity; }
        }

        void OnDestroy()
        {
            if (current == this) { current = null; Active = false; }
        }

        void Update()
        {
            if (Digit2Pressed()) SetActive(!Active, true);
            if (!Active || model == null) return;
            sway += Time.deltaTime;
            float fl = 0.94f + 0.06f * Mathf.Sin(sway * 31f) * Mathf.Sin(sway * 7f);
            if (beam != null) beam.intensity = 6f * fl;
            if (halo != null) halo.intensity = 3f * fl;
            if (glowSprite != null && Camera.main != null)
            {
                Transform c = Camera.main.transform;
                Vector3 axis = glowTube.transform.up;
                Vector3 toCam = c.position - glowSprite.transform.position;
                Vector3 n = Vector3.ProjectOnPlane(toCam, axis);
                if (n.sqrMagnitude > 1e-6f) glowSprite.transform.rotation = Quaternion.LookRotation(-n.normalized, axis) * Quaternion.Euler(0f, 0f, 90f);
            }
        }

        void SetActive(bool on, bool sound)
        {
            Active = on;
            if (model != null) model.SetActive(on);
            if (halo != null) halo.enabled = on;
            if (beam != null) { beam.enabled = on; beam.range = range + 3f; beam.spotAngle = beamAngle; beam.innerSpotAngle = beamAngle * 0.45f; }
            if (tubeMat != null)
            {
                tubeMat.SetColor("_EmissionColor", on ? tubeOn : Color.black);
                if (tubeMat.HasProperty("_BaseColor")) tubeMat.SetColor("_BaseColor", on ? new Color(0.85f, 0.7f, 1f) : tubeBase);
            }
            if (glowSprite != null) glowSprite.gameObject.SetActive(on);
            if (sound && on && toggleSound != null) audioSource.PlayOneShot(toggleSound);
        }

        public static bool Illuminates(Vector3 point)
        {
            if (!Active || current == null || current.beam == null) return false;
            Transform b = current.beam.transform;
            Vector3 d = point - b.position;
            float dist = d.magnitude;
            if (dist > current.range || dist < 0.05f) return false;
            if (Vector3.Angle(b.forward, d) > current.halfAngle) return false;
            if (Physics.Linecast(b.position, point + (b.position - point).normalized * 0.12f, out RaycastHit hit, ~0, QueryTriggerInteraction.Ignore))
                if (hit.collider.GetComponentInParent<CharacterController>() == null) return false;
            return true;
        }

        static bool Digit2Pressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.digit2Key.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.Alpha2);
#endif
        }
    }
}
