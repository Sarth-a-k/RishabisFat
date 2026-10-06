using UnityEngine;

namespace FPCharacter
{
    public sealed class ShapeBeacon : MonoBehaviour
    {
        public MirrorSocket socket;
        public LineRenderer symbol;
        public float bobHeight = 0.06f;
        public float bobSpeed = 1.6f;
        public float fadeSpeed = 2.5f;
        [Range(0f, 1f)] public float minAlpha = 0.55f;
        public float delaySeconds = 75f;
        public Vector3 areaCenter = new Vector3(48.85f, 0f, 0f);
        public Vector2 areaSize = new Vector2(48.1f, 36.4f);

        Vector3 basePos;
        Color baseColor;
        float shown;
        float waited;
        Transform player;
        LineRenderer emblem;
        Color emblemColor;
        bool infrared;
        Transform cam;
        float seed;
        MirrorSocket[] allSockets;
        int lastPlaced = -1;

        void Start()
        {
            if (symbol == null) { enabled = false; return; }
            basePos = symbol.transform.localPosition;
            baseColor = symbol.startColor;
            seed = Random.value * 6f;
            allSockets = FindObjectsByType<MirrorSocket>(FindObjectsInactive.Include);
            symbol.enabled = false;
            if (socket != null && socket.mirror != null)
            {
                Transform e = socket.mirror.transform.Find("Shape symbol");
                if (e != null) emblem = e.GetComponent<LineRenderer>();
                if (emblem != null) { emblemColor = emblem.startColor; emblem.enabled = false; }
            }
            InfraredVision.Changed += OnInfrared;
            infrared = InfraredVision.Active;
        }

        void OnDestroy()
        {
            InfraredVision.Changed -= OnInfrared;
        }

        void OnInfrared(bool on) { infrared = on; }

        bool InArea(Vector3 p)
        {
            Vector3 d = p - areaCenter;
            return Mathf.Abs(d.x) <= areaSize.x * 0.5f && Mathf.Abs(d.z) <= areaSize.y * 0.5f;
        }

        void LateUpdate()
        {
            if (cam == null && Camera.main != null) cam = Camera.main.transform;
            if (player == null)
            {
                FPCharacterMover m = FindAnyObjectByType<FPCharacterMover>();
                if (m != null) player = m.transform;
            }
            int placed = 0;
            foreach (MirrorSocket s in allSockets) if (s != null && s.Occupied) placed++;
            if (placed > lastPlaced) waited = 0f;
            lastPlaced = placed;
            bool empty = socket == null || !socket.Occupied;
            if (empty && player != null && InArea(player.position)) waited += Time.deltaTime;
            bool want = empty && waited >= delaySeconds;
            shown = Mathf.MoveTowards(shown, want ? 1f : 0f, Time.deltaTime * fadeSpeed);
            bool vis = shown > 0.001f && !infrared;
            symbol.enabled = vis;
            if (emblem != null)
            {
                emblem.enabled = vis;
                Color ec = new Color(emblemColor.r, emblemColor.g, emblemColor.b, shown);
                emblem.startColor = ec;
                emblem.endColor = ec;
            }
            if (!vis) return;

            float t = Time.time + seed;
            symbol.transform.localPosition = basePos + Vector3.up * (Mathf.Sin(t * bobSpeed) * bobHeight);
            if (cam != null)
            {
                Vector3 d = symbol.transform.position - cam.position;
                d.y = 0f;
                if (d.sqrMagnitude > 1e-4f) symbol.transform.rotation = Quaternion.LookRotation(d, Vector3.up);
            }
            float a = Mathf.Lerp(minAlpha, 1f, (Mathf.Sin(t * bobSpeed * 1.3f) + 1f) * 0.5f) * shown;
            Color c = new Color(baseColor.r, baseColor.g, baseColor.b, a);
            symbol.startColor = c;
            symbol.endColor = c;
        }
    }
}
