using UnityEngine;

namespace FPCharacter
{
    public sealed class UVFootprintTrail : MonoBehaviour
    {
        public Vector3 areaCenter = new Vector3(173.9f, 0f, 0f);
        public Vector2 areaSize = new Vector2(44f, 44f);
        public float hintDelay = 10f;

        public static bool HintOn { get; private set; }
        static UVFootprintTrail current;
        float searching;
        float lastFound = -100f;
        Transform player;
        bool playerInside;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { HintOn = false; current = null; }

        void Start() { current = this; }

        void OnDestroy() { if (current == this) { current = null; HintOn = false; } }

        public static void NotifyFound()
        {
            if (current == null) return;
            current.lastFound = Time.time;
            current.searching = 0f;
            HintOn = false;
        }

        void Update()
        {
            if (player == null)
            {
                FPCharacterMover m = FindAnyObjectByType<FPCharacterMover>();
                if (m != null) player = m.transform;
                if (player == null) return;
            }
            Vector3 d = player.position - areaCenter;
            bool inside = Mathf.Abs(d.x) <= areaSize.x * 0.5f && Mathf.Abs(d.z) <= areaSize.y * 0.5f;
            playerInside = inside;
            if (!inside) { searching = 0f; HintOn = false; return; }
            if (Time.time - lastFound < 0.5f) return;
            searching += Time.deltaTime;
            if (searching >= hintDelay) HintOn = true;
        }

        // key reminder while in the void; lowest priority so look-at prompts like [E] always win
        void OnGUI()
        {
            if (!playerInside || NotePickup.Busy) return;
            PromptBox.Draw(UVBaton.Active ? "[2] Put away the UV baton" : "[2] Take out the UV baton", 0.92f, 0);
        }
    }
}
