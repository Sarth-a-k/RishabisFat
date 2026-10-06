using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace FPCharacter
{
    public sealed class JournalReader : MonoBehaviour
    {
        public JournalSet journal;
        public string journalResource = "Dialogue/EclipseKeep_Journal";
        public float useDistance = 2.6f;
        public string prompt = "[E] Read the journal";
        public AudioClip pageSound;
        public UnityEngine.Events.UnityEvent onFinishedReading = new UnityEngine.Events.UnityEvent();
        bool finishedFired;

        public static bool Reading { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Reading = false; }

        bool looking, open, finished;
        int page;
        float openedAt, thoughtUntil = -1f;
        GameObject ui;
        Text heading, body, footer;
        Behaviour[] frozen;
        AudioSource audioSource;

        void Start()
        {
            if (journal == null) journal = Resources.Load<JournalSet>(journalResource);
            if (pageSound == null) pageSound = Resources.Load<AudioClip>("WarmStatues/thump");
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
            audioSource.volume = 0.35f;
        }

        void Update()
        {
            if (open)
            {
                if (Time.unscaledTime - openedAt < 0.2f) return;
                if (Pressed(KeyLeft())) Turn(-1);
                else if (Pressed(KeyRight())) Turn(1);
                else if (Pressed(KeyClose())) Close();
                return;
            }
            looking = false;
            Camera cam = Camera.main;
            if (cam == null || journal == null) return;
            if (Physics.SphereCast(new Ray(cam.transform.position, cam.transform.forward), 0.12f, out RaycastHit hit, useDistance + 0.3f, ~0, QueryTriggerInteraction.Ignore))
                looking = hit.collider.transform.IsChildOf(transform);
            if (looking && PrismPickup.InteractPressed()) Open();
        }

        void Open()
        {
            if (journal.pages.Count == 0) return;
            open = true;
            Reading = true;
            openedAt = Time.unscaledTime;
            page = 0;
            if (ui == null) BuildUI();
            ui.SetActive(true);
            var list = new System.Collections.Generic.List<Behaviour>();
            FPCharacterMover m = FindAnyObjectByType<FPCharacterMover>();
            if (m != null && m.enabled) { m.enabled = false; list.Add(m); }
            PlayerInteraction pi = FindAnyObjectByType<PlayerInteraction>();
            if (pi != null && pi.enabled) { pi.enabled = false; list.Add(pi); }
            frozen = list.ToArray();
            Show();
            Play();
        }

        void Close()
        {
            open = false;
            Reading = false;
            if (ui != null) ui.SetActive(false);
            if (frozen != null) foreach (Behaviour b in frozen) if (b != null) b.enabled = true;
            frozen = null;
            if (finished && !string.IsNullOrEmpty(journal.thoughtAfterLastPage)) thoughtUntil = Time.time + 4.5f;
            if (finished && !finishedFired) { finishedFired = true; onFinishedReading.Invoke(); }
        }

        void Turn(int dir)
        {
            int next = Mathf.Clamp(page + dir, 0, journal.pages.Count - 1);
            if (next == page) return;
            page = next;
            Show();
            Play();
        }

        void Show()
        {
            JournalSet.Page p = journal.pages[page];
            heading.text = p.heading;
            body.text = p.body;
            footer.text = (page > 0 ? "[A] back     " : "") + (page + 1) + " / " + journal.pages.Count + (page < journal.pages.Count - 1 ? "     [D] next" : "") + "          [E] close";
            if (page == journal.pages.Count - 1) finished = true;
        }

        void Play()
        {
            if (pageSound != null) { audioSource.pitch = Random.Range(1.4f, 1.7f); audioSource.PlayOneShot(pageSound); }
        }

        void BuildUI()
        {
            ui = new GameObject("Journal UI");
            var canvas = ui.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 900;
            var scaler = ui.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            Font font = Resources.Load<Font>("WarmStatues/DotGothic16");
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Image dim = Box(ui.transform, "Dim", new Color(0f, 0f, 0f, 0.78f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Image paper = Box(ui.transform, "Page", new Color(0.82f, 0.75f, 0.6f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-520f, -400f), new Vector2(520f, 400f));
            Box(paper.transform, "Edge", new Color(0.35f, 0.26f, 0.16f, 0.35f), Vector2.zero, Vector2.one, new Vector2(18f, 18f), new Vector2(-18f, -18f)).color = new Color(0.75f, 0.67f, 0.5f, 1f);
            heading = Label(paper.transform, font, 44, new Color(0.25f, 0.15f, 0.08f), new Vector2(60f, -50f), new Vector2(920f, 70f), TextAnchor.UpperLeft);
            body = Label(paper.transform, font, 34, new Color(0.18f, 0.12f, 0.08f), new Vector2(60f, -140f), new Vector2(920f, 560f), TextAnchor.UpperLeft);
            body.lineSpacing = 1.25f;
            footer = Label(paper.transform, font, 24, new Color(0.35f, 0.25f, 0.16f), new Vector2(60f, -730f), new Vector2(920f, 40f), TextAnchor.UpperCenter);
            ui.SetActive(false);
        }

        static Image Box(Transform parent, string name, Color c, Vector2 aMin, Vector2 aMax, Vector2 oMin, Vector2 oMax)
        {
            var g = new GameObject(name, typeof(RectTransform));
            g.transform.SetParent(parent, false);
            var rt = (RectTransform)g.transform;
            rt.anchorMin = aMin; rt.anchorMax = aMax; rt.offsetMin = oMin; rt.offsetMax = oMax;
            var im = g.AddComponent<Image>();
            im.color = c;
            im.raycastTarget = false;
            return im;
        }

        static Text Label(Transform parent, Font font, int size, Color c, Vector2 pos, Vector2 box, TextAnchor align)
        {
            var g = new GameObject("Text", typeof(RectTransform));
            g.transform.SetParent(parent, false);
            var rt = (RectTransform)g.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = box;
            var t = g.AddComponent<Text>();
            t.font = font; t.fontSize = size; t.color = c; t.alignment = align;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        void OnGUI()
        {
            if (!open && looking) PromptBox.Draw(prompt, 0.66f);
            if (!open && Time.time < thoughtUntil) SubtitleBox.Draw(journal.thoughtAfterLastPage, SubtitleBox.Fade(thoughtUntil - 4.5f, thoughtUntil));
        }

        enum K { Left, Right, Close }
        static K KeyLeft() => K.Left;
        static K KeyRight() => K.Right;
        static K KeyClose() => K.Close;

        static bool Pressed(K k)
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb == null) return false;
            if (k == K.Left) return kb.aKey.wasPressedThisFrame || kb.leftArrowKey.wasPressedThisFrame;
            if (k == K.Right) return kb.dKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame;
            return kb.eKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame;
#else
            if (k == K.Left) return Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow);
            if (k == K.Right) return Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow);
            return Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Escape);
#endif
        }
    }
}
