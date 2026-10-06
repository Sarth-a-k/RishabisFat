using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FPCharacter
{
    public sealed class ItemHintArrow : MonoBehaviour
    {
        public float showAfterSeconds = 180f;
        public float levelRadius = 40f;
        public Color arrowColor = new Color(1f, 0.82f, 0.38f, 1f);

        static ItemHintArrow instance;
        static bool forced;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { instance = null; forced = false; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Hook()
        {
            SceneManager.sceneLoaded -= OnLoaded;
            SceneManager.sceneLoaded += OnLoaded;
            TrySpawn();
        }

        static void OnLoaded(Scene s, LoadSceneMode m) { TrySpawn(); }

        static void TrySpawn()
        {
            if (FindAnyObjectByType<ItemHintArrow>() != null) return;
            if (FindAnyObjectByType<FPCharacterMover>() == null) return;
            new GameObject("Item Hint Arrow").AddComponent<ItemHintArrow>();
        }

        public static void ForceShow()
        {
            forced = true;
            if (instance != null) instance.idle = instance.showAfterSeconds;
        }

        readonly List<GameObject> items = new List<GameObject>();
        readonly HashSet<GameObject> found = new HashSet<GameObject>();
        FPCharacterMover mover;
        GameObject target;
        float idle, scanAt;
        Texture2D arrow;

        void Awake() { instance = this; }

        void Start()
        {
            mover = FindAnyObjectByType<FPCharacterMover>();
            foreach (MirrorPickup m in FindObjectsByType<MirrorPickup>(FindObjectsInactive.Exclude)) items.Add(m.gameObject);
            foreach (PrismPickup p in FindObjectsByType<PrismPickup>(FindObjectsInactive.Exclude)) items.Add(p.gameObject);
            foreach (InfraredGogglesPickup g in FindObjectsByType<InfraredGogglesPickup>(FindObjectsInactive.Exclude)) items.Add(g.gameObject);
            foreach (JournalReader j in FindObjectsByType<JournalReader>(FindObjectsInactive.Exclude)) items.Add(j.gameObject);
            if (forced) idle = showAfterSeconds;
        }

        bool IsFound(GameObject g)
        {
            if (g == null || !g.activeInHierarchy) return true;
            MirrorPickup m = g.GetComponent<MirrorPickup>();
            if (m != null && m.IsCarried) return true;
            PrismPickup p = g.GetComponent<PrismPickup>();
            if (p != null && PrismPickup.Carried == p) return true;
            return false;
        }

        void Update()
        {
            if (mover == null) return;
            if (Time.time >= scanAt)
            {
                scanAt = Time.time + 0.25f;
                bool newFind = false;
                foreach (GameObject g in items)
                    if (!found.Contains(g) && IsFound(g)) { found.Add(g); newFind = true; }
                if (newFind) { idle = 0f; target = null; }
                if (target != null && found.Contains(target)) target = null;
            }
            GameObject nearest = Nearest();
            if (nearest == null) { target = null; return; }
            if (mover.enabled && !JournalReader.Reading) idle += Time.deltaTime;
            if (idle >= showAfterSeconds && target == null) target = nearest;
        }

        GameObject Nearest()
        {
            Vector3 p = mover.transform.position;
            GameObject best = null;
            float bd = levelRadius;
            foreach (GameObject g in items)
            {
                if (found.Contains(g) || g == null) continue;
                Vector3 d = g.transform.position - p;
                d.y = 0f;
                float m = d.magnitude;
                if (m < bd) { bd = m; best = g; }
            }
            return best;
        }

        Vector3 TargetPoint()
        {
            Bounds b = new Bounds(target.transform.position, Vector3.zero);
            bool any = false;
            foreach (Renderer r in target.GetComponentsInChildren<Renderer>())
            {
                if (r is ParticleSystemRenderer) continue;
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            return new Vector3(b.center.x, b.max.y, b.center.z);
        }

        void OnGUI()
        {
            if (target == null || mover == null || !mover.enabled || JournalReader.Reading) return;
            if (Event.current.type != EventType.Repaint) return;
            Camera cam = Camera.main;
            if (cam == null) return;
            if (arrow == null) arrow = BuildArrow();
            Vector3 sp = cam.WorldToScreenPoint(TargetPoint());
            float size = Mathf.Max(36f, Screen.height / 13f);
            float bob = Mathf.Sin(Time.time * 4f) * size * 0.15f;
            float pulse = 0.75f + 0.25f * Mathf.Sin(Time.time * 5f);
            Color c = new Color(arrowColor.r, arrowColor.g, arrowColor.b, arrowColor.a * pulse);
            float margin = size * 1.1f;
            bool onScreen = sp.z > 0f && sp.x > margin && sp.x < Screen.width - margin && sp.y > margin && sp.y < Screen.height - margin;
            Vector2 pos;
            float angle;
            if (onScreen)
            {
                pos = new Vector2(sp.x, Screen.height - sp.y - size * 0.9f + bob);
                angle = 180f;
            }
            else
            {
                Vector2 centre = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
                Vector2 dir = new Vector2(sp.x - centre.x, (Screen.height - sp.y) - centre.y);
                if (sp.z < 0f) dir = -dir;
                if (dir.sqrMagnitude < 1f) dir = Vector2.down;
                dir.Normalize();
                float hx = Screen.width * 0.5f - margin, hy = Screen.height * 0.5f - margin;
                float k = Mathf.Min(hx / Mathf.Max(0.001f, Mathf.Abs(dir.x)), hy / Mathf.Max(0.001f, Mathf.Abs(dir.y)));
                pos = centre + dir * k + dir * bob;
                angle = Mathf.Atan2(dir.x, -dir.y) * Mathf.Rad2Deg;
            }
            Matrix4x4 old = GUI.matrix;
            Color oc = GUI.color;
            GUIUtility.RotateAroundPivot(angle, pos);
            var rect = new Rect(pos.x - size * 0.5f, pos.y - size * 0.5f, size, size);
            GUI.color = new Color(0f, 0f, 0f, 0.45f * pulse);
            GUI.DrawTexture(new Rect(rect.x + 2f, rect.y + 3f, rect.width, rect.height), arrow);
            GUI.color = c;
            GUI.DrawTexture(rect, arrow);
            GUI.matrix = old;
            GUI.color = oc;
        }

        static Texture2D BuildArrow()
        {
            const int N = 128;
            var t = new Texture2D(N, N, TextureFormat.RGBA32, false);
            t.wrapMode = TextureWrapMode.Clamp;
            var px = new Color32[N * N];
            Vector2 tip = new Vector2(64f, 120f), l = new Vector2(14f, 58f), r = new Vector2(114f, 58f);
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                    float head = Mathf.Min(Edge(l, tip, p), Mathf.Min(Edge(tip, r, p), p.y - 58f));
                    float shaft = Mathf.Min(Mathf.Min(p.x - 44f, 84f - p.x), Mathf.Min(p.y - 8f, 62f - p.y));
                    float d = Mathf.Max(head, shaft);
                    float outline = Mathf.Clamp01(d + 1f);
                    float inner = Mathf.Clamp01(d - 5f);
                    Color col = Color.Lerp(new Color(0.12f, 0.08f, 0.04f, outline), new Color(1f, 1f, 1f, 1f), inner);
                    px[y * N + x] = col;
                }
            t.SetPixels32(px);
            t.Apply();
            t.hideFlags = HideFlags.DontSave;
            return t;
        }

        static float Edge(Vector2 a, Vector2 b, Vector2 p)
        {
            Vector2 e = b - a;
            Vector2 n = new Vector2(e.y, -e.x).normalized;
            return Vector2.Dot(p - a, n);
        }
    }
}
