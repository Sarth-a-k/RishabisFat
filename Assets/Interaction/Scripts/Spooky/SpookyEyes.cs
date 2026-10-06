// using System.Collections.Generic;
// using UnityEngine;

// namespace FPCharacter
// {
//     public sealed class SpookyEyes : MonoBehaviour
//     {
//         public Material eyesMaterial;
//         public Vector3 roomCenter = new Vector3(48.85f, 0f, 0f);
//         public Vector2 roomSize = new Vector2(48.1f, 36.4f);
//         public float minDelay = 2f;
//         public float maxDelay = 4f;
//         public float minDistance = 29f;
//         public float maxDistance = 30f;
//         public float minShow = 1.4f;
//         public float maxShow = 3.2f;
//         public float size = 0.9f;
//         public float vanishDistance = 4.5f;
//         public float fadeIn = 0.5f;
//         public float fadeOut = 0.35f;
//         public Color tint = new Color(1f, 0.15f, 0.08f, 1f);

//         GameObject eyes;
//         Material inst;
//         Camera cam;
//         Transform player;
//         float nextSpawn;
//         float shownAt;
//         float showFor;
//         float fadeStart = -1f;
//         float alpha;
//         float blinkAt;
//         bool showing;
//         readonly List<Light> lights = new List<Light>();
//         float nextLightScan;

//         void Start()
//         {
//             eyes = GameObject.CreatePrimitive(PrimitiveType.Quad);
//             eyes.name = "Eyes";
//             eyes.transform.SetParent(transform, false);
//             Destroy(eyes.GetComponent<Collider>());
//             Renderer r = eyes.GetComponent<Renderer>();
//             inst = new Material(eyesMaterial);
//             r.sharedMaterial = inst;
//             r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
//             r.receiveShadows = false;
//             eyes.SetActive(false);
//             nextSpawn = Time.time + Random.Range(minDelay * 0.5f, maxDelay * 0.6f);
//         }

//         bool InRoom(Vector3 p)
//         {
//             Vector3 d = p - roomCenter;
//             return Mathf.Abs(d.x) <= roomSize.x * 0.5f && Mathf.Abs(d.z) <= roomSize.y * 0.5f;
//         }

//         void Update()
//         {
//             if (cam == null) cam = Camera.main;
//             if (player == null)
//             {
//                 FPCharacterMover m = FindAnyObjectByType<FPCharacterMover>();
//                 if (m != null) player = m.transform;
//             }
//             if (cam == null || player == null) return;
//             if (!InRoom(player.position))
//             {
//                 if (showing) Hide();
//                 return;
//             }
//             if (Time.time > nextLightScan)
//             {
//                 nextLightScan = Time.time + 2f;
//                 lights.Clear();
//                 foreach (Light l in FindObjectsByType<Light>(FindObjectsSortMode.None))
//                     if (l.type == LightType.Point && (l.enabled || l.GetComponentInParent<SunkenPrism.TorchFlame>() != null) && !l.transform.IsChildOf(player) && l.intensity > 0.3f) lights.Add(l);
//             }
//             if (!showing)
//             {
//                 if (Time.time >= nextSpawn && !TrySpawn()) nextSpawn = Time.time + 1.5f;
//                 return;
//             }
//             UpdateShowing();
//         }

//         bool TrySpawn()
//         {
//             float vfov = cam.fieldOfView;
//             float hfov = Camera.VerticalToHorizontalFieldOfView(vfov, cam.aspect);
//             for (int attempt = 0; attempt < 8; attempt++)
//             {
//                 float yaw = Random.Range(-hfov * 0.4f, hfov * 0.4f);
//                 float pitch = Random.Range(-vfov * 0.15f, vfov * 0.25f);
//                 Vector3 dir = Quaternion.AngleAxis(yaw, Vector3.up) * Quaternion.AngleAxis(-pitch, cam.transform.right) * cam.transform.forward;
//                 Vector3 pos;
//                 if (Physics.Raycast(cam.transform.position, dir, out RaycastHit hit, maxDistance, ~0, QueryTriggerInteraction.Ignore))
//                 {
//                     if (hit.distance < minDistance) continue;
//                     pos = hit.point + hit.normal * 0.35f;
//                 }
//                 else pos = cam.transform.position + dir * Random.Range(minDistance, maxDistance);
//                 pos.y = Mathf.Clamp(pos.y, cam.transform.position.y - 0.9f, cam.transform.position.y + 1.4f);
//                 if (!InRoom(pos)) continue;
//                 bool lit = false;
//                 foreach (Light l in lights)
//                     if (l != null && (l.transform.position - pos).sqrMagnitude < (l.range * 0.55f) * (l.range * 0.55f)) { lit = true; break; }
//                 if (lit) continue;
//                 if (Physics.Linecast(cam.transform.position, pos, out RaycastHit block, ~0, QueryTriggerInteraction.Ignore) && block.distance < Vector3.Distance(cam.transform.position, pos) - 0.5f) continue;
//                 Show(pos);
//                 return true;
//             }
//             return false;
//         }

//         void Show(Vector3 pos)
//         {
//             showing = true;
//             eyes.SetActive(true);
//             eyes.transform.position = pos;
//             float dist = Vector3.Distance(cam.transform.position, pos);
//             float s = size * Mathf.Lerp(0.8f, 1.5f, Mathf.InverseLerp(minDistance, maxDistance, dist));
//             eyes.transform.localScale = new Vector3(s, s * 0.5f, 1f);
//             shownAt = Time.time;
//             showFor = Random.Range(minShow, maxShow);
//             blinkAt = Random.value < 0.6f ? shownAt + Random.Range(0.5f, showFor - 0.3f) : -1f;
//             fadeStart = -1f;
//             alpha = 0f;
//             Apply();
//         }

//         void UpdateShowing()
//         {
//             Transform ct = cam.transform;
//             Vector3 to = eyes.transform.position - ct.position;
//             eyes.transform.rotation = Quaternion.LookRotation(to, Vector3.up);
//             float t = Time.time - shownAt;
//             bool flee = to.magnitude < vanishDistance || Vector3.Dot(ct.forward, to.normalized) < Mathf.Cos(cam.fieldOfView * Mathf.Deg2Rad);
//             if (fadeStart < 0f && (t > showFor || flee)) fadeStart = Time.time;
//             if (fadeStart < 0f) alpha = Mathf.Clamp01(t / fadeIn);
//             else
//             {
//                 alpha = 1f - Mathf.Clamp01((Time.time - fadeStart) / fadeOut);
//                 if (alpha <= 0f) { Hide(); return; }
//             }
//             float sy = 1f;
//             if (blinkAt > 0f && Time.time > blinkAt && Time.time < blinkAt + 0.16f) sy = 0.12f;
//             Vector3 sc = eyes.transform.localScale;
//             eyes.transform.localScale = new Vector3(sc.x, sc.x * 0.5f * sy, 1f);
//             Apply();
//         }

//         void Apply()
//         {
//             float flicker = 0.85f + 0.15f * Mathf.PerlinNoise(Time.time * 6f, 0.3f);
//             Color c = tint * (alpha * flicker);
//             c.a = alpha;
//             inst.SetColor("_BaseColor", c);
//         }

//         void Hide()
//         {
//             showing = false;
//             eyes.SetActive(false);
//             nextSpawn = Time.time + Random.Range(minDelay, maxDelay);
//         }
//     }
// }



using System.Collections.Generic;
using UnityEngine;

namespace FPCharacter
{
    public sealed class SpookyEyes : MonoBehaviour
    {
        public Material eyesMaterial;
        public Vector3 roomCenter = new Vector3(48.85f, 0f, 0f);
        public Vector2 roomSize = new Vector2(48.1f, 36.4f);

        [Header("Spawn Heights (Relative to Player Y)")]
        public float minHeightAbovePlayer = 5.0f; // Min height above player position
        public float maxHeightAbovePlayer = 6.0f; // Max height above player position

        [Header("Timings & Distances")]
        public float minDelay = 3f;
        public float maxDelay = 7f;
        public float minDistance = 25f;
        public float maxDistance = 30f;
        public float minShow = 1.4f;
        public float maxShow = 3.2f;
        public float size = 0.9f;
        public float vanishDistance = 4.5f;
        public float fadeIn = 0.5f;
        public float fadeOut = 0.35f;
        public Color tint = new Color(1f, 0.15f, 0.08f, 1f);

        GameObject eyes;
        Material inst;
        Camera cam;
        Transform player;
        float nextSpawn;
        float shownAt;
        float showFor;
        float fadeStart = -1f;
        float alpha;
        float blinkAt;
        bool showing;
        readonly List<Light> lights = new List<Light>();
        float nextLightScan;

        void Start()
        {
            eyes = GameObject.CreatePrimitive(PrimitiveType.Quad);
            eyes.name = "Eyes";
            eyes.transform.SetParent(transform, false);
            Destroy(eyes.GetComponent<Collider>());
            Renderer r = eyes.GetComponent<Renderer>();
            inst = new Material(eyesMaterial);
            r.sharedMaterial = inst;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            eyes.SetActive(false);
            nextSpawn = Time.time + Random.Range(minDelay * 0.5f, maxDelay * 0.6f);
        }

        bool InRoom(Vector3 p)
        {
            Vector3 d = p - roomCenter;
            return Mathf.Abs(d.x) <= roomSize.x * 0.5f && Mathf.Abs(d.z) <= roomSize.y * 0.5f;
        }

        void Update()
        {
            if (cam == null) cam = Camera.main;
            if (player == null)
            {
                FPCharacterMover m = FindAnyObjectByType<FPCharacterMover>();
                if (m != null) player = m.transform;
            }
            if (cam == null || player == null) return;
            if (!InRoom(player.position))
            {
                if (showing) Hide();
                return;
            }
            if (Time.time > nextLightScan)
            {
                nextLightScan = Time.time + 2f;
                lights.Clear();
                foreach (Light l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                    if (l.type == LightType.Point && (l.enabled || l.GetComponentInParent<SunkenPrism.TorchFlame>() != null) && !l.transform.IsChildOf(player) && l.intensity > 0.3f) lights.Add(l);
            }
            if (!showing)
            {
                if (Time.time >= nextSpawn && !TrySpawn()) nextSpawn = Time.time + 1.5f;
                return;
            }
            UpdateShowing();
        }

        bool TrySpawn()
        {
            float vfov = cam.fieldOfView;
            float hfov = Camera.VerticalToHorizontalFieldOfView(vfov, cam.aspect);

            for (int attempt = 0; attempt < 8; attempt++)
            {
                float yaw = Random.Range(-hfov * 0.4f, hfov * 0.4f);
                // Pitch directed upwards (0.05 to 0.40) to aim high on walls / ceiling
                float pitch = Random.Range(vfov * 0.05f, vfov * 0.40f); 
                Vector3 dir = Quaternion.AngleAxis(yaw, Vector3.up) * Quaternion.AngleAxis(-pitch, cam.transform.right) * cam.transform.forward;
                
                Vector3 pos;
                if (Physics.Raycast(cam.transform.position, dir, out RaycastHit hit, maxDistance, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (hit.distance < minDistance) continue;
                    pos = hit.point + hit.normal * 0.35f;
                }
                else pos = cam.transform.position + dir * Random.Range(minDistance, maxDistance);

                // Reject any position that is below the minimum height requirement
                if (pos.y < player.position.y + minHeightAbovePlayer) continue;

                // Clamp to height range above player
                pos.y = Mathf.Clamp(pos.y, player.position.y + minHeightAbovePlayer, player.position.y + maxHeightAbovePlayer);

                if (!InRoom(pos)) continue;

                bool lit = false;
                foreach (Light l in lights)
                    if (l != null && (l.transform.position - pos).sqrMagnitude < (l.range * 0.55f) * (l.range * 0.55f)) { lit = true; break; }
                if (lit) continue;

                if (Physics.Linecast(cam.transform.position, pos, out RaycastHit block, ~0, QueryTriggerInteraction.Ignore) && block.distance < Vector3.Distance(cam.transform.position, pos) - 0.5f) continue;

                Show(pos);
                return true;
            }
            return false;
        }

        void Show(Vector3 pos)
        {
            showing = true;
            eyes.SetActive(true);
            eyes.transform.position = pos;
            float dist = Vector3.Distance(cam.transform.position, pos);
            float s = size * Mathf.Lerp(0.8f, 1.5f, Mathf.InverseLerp(minDistance, maxDistance, dist));
            eyes.transform.localScale = new Vector3(s, s * 0.5f, 1f);
            shownAt = Time.time;
            showFor = Random.Range(minShow, maxShow);
            blinkAt = Random.value < 0.6f ? shownAt + Random.Range(0.5f, showFor - 0.3f) : -1f;
            fadeStart = -1f;
            alpha = 0f;
            Apply();
        }

        void UpdateShowing()
        {
            Transform ct = cam.transform;
            Vector3 to = eyes.transform.position - ct.position;
            eyes.transform.rotation = Quaternion.LookRotation(to, Vector3.up);
            float t = Time.time - shownAt;
            bool flee = to.magnitude < vanishDistance || Vector3.Dot(ct.forward, to.normalized) < Mathf.Cos(cam.fieldOfView * Mathf.Deg2Rad);
            if (fadeStart < 0f && (t > showFor || flee)) fadeStart = Time.time;
            if (fadeStart < 0f) alpha = Mathf.Clamp01(t / fadeIn);
            else
            {
                alpha = 1f - Mathf.Clamp01((Time.time - fadeStart) / fadeOut);
                if (alpha <= 0f) { Hide(); return; }
            }
            float sy = 1f;
            if (blinkAt > 0f && Time.time > blinkAt && Time.time < blinkAt + 0.16f) sy = 0.12f;
            Vector3 sc = eyes.transform.localScale;
            eyes.transform.localScale = new Vector3(sc.x, sc.x * 0.5f * sy, 1f);
            Apply();
        }

        void Apply()
        {
            float flicker = 0.85f + 0.15f * Mathf.PerlinNoise(Time.time * 6f, 0.3f);
            Color c = tint * (alpha * flicker);
            c.a = alpha;
            inst.SetColor("_BaseColor", c);
        }

        void Hide()
        {
            showing = false;
            eyes.SetActive(false);
            nextSpawn = Time.time + Random.Range(minDelay, maxDelay);
        }
    }
}