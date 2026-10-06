using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace FPCharacter
{
    public static class GamePause
    {
        static int resumedFrame = -10;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { resumedFrame = -10; }

        public static bool Paused => global::PauseMenu.IsPaused || HintsScreen.Open;
        public static bool BlockInput => Paused || Time.frameCount - resumedFrame <= 2;
        public static void MarkResumed() { resumedFrame = Time.frameCount; }
    }

    public sealed class HintsScreen : MonoBehaviour
    {
        public static bool Open { get; private set; }
        static int closedFrame = -10;
        public static bool ClosedThisFrame => Time.frameCount == closedFrame;
        static HintsScreen instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Open = false; closedFrame = -10; instance = null; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Hook()
        {
            SceneManager.sceneLoaded -= OnLoaded;
            SceneManager.sceneLoaded += OnLoaded;
            Ensure(SceneManager.GetActiveScene());
        }

        static void OnLoaded(Scene s, LoadSceneMode m) { Ensure(s); }

        static void Ensure(Scene s)
        {
            if (instance == null)
            {
                var go = new GameObject("Hints Screen");
                DontDestroyOnLoad(go);
                instance = go.AddComponent<HintsScreen>();
            }
            if (s.name.Contains("MainMenu")) return;
            if (global::PauseMenu.Instance == null) new GameObject("Pause Menu").AddComponent<global::PauseMenu>();
            instance.Wire(global::PauseMenu.Instance);
        }

        global::PauseMenu wired;
        readonly List<Behaviour> frozen = new List<Behaviour>();

        void Wire(global::PauseMenu menu)
        {
            if (menu == null || menu == wired) return;
            wired = menu;
            menu.onHints.AddListener(Show);
            menu.onPaused.AddListener(OnPaused);
            menu.onResumed.AddListener(OnResumed);
        }

        void OnPaused()
        {
            frozen.Clear();
            foreach (FPCharacterMover m in FindObjectsByType<FPCharacterMover>(FindObjectsInactive.Exclude)) if (m.enabled) { m.enabled = false; frozen.Add(m); }
        }

        void OnResumed()
        {
            foreach (Behaviour b in frozen) if (b != null) b.enabled = true;
            frozen.Clear();
            Open = false;
            GamePause.MarkResumed();
        }

        void Show()
        {
            shown = CurrentLevel();
            Open = true;
        }

        void Close()
        {
            Open = false;
            closedFrame = Time.frameCount;
        }

        void Update()
        {
            if (Open && EscPressed()) Close();
        }

        struct Level { public string title; public string[] hints; }

        static readonly Level Nexus = new Level { title = "CASA DEL SILENCIO", hints = new[] {
            "Move with W A S D, look with the mouse, hold Shift to run.",
            "Walk east through the passage.",
            "A ghost sits in the passage. Walk up to him and press E to listen.",
            "Glowing objects can be picked up or used. Look at them and press E." } };
        static readonly Level Hollows = new Level { title = "PRISMATIC HOLLOWS", hints = new[] {
            "Light the torch first: walk up to it and press E.",
            "Three mirrors glow in the hollow. Press E to pick one up, then set it on the stone with the same shape and colour.",
            "The light must travel Oval, then Rectangle, then Circle, and on to the prism.",
            "Look at a placed mirror and hold Q or R to turn it. Its stone glows green when it faces true.",
            "After the third mirror is placed, check the others. One may have twisted out of line.",
            "Look at the prism and press E to turn it until the moon burns red.",
            "When the prism shatters, head for the gate on the east side." } };
        static readonly Level Passage = new Level { title = "THE PASSAGE", hints = new[] {
            "Keep heading east.",
            "Ghosts in the passages can be spoken to. Walk up to one and press E.",
            "If you skipped a ghost, you can go back and listen any time." } };
        static readonly Level Ember = new Level { title = "EMBER KEEP", hints = new[] {
            "You carry the prism from the hollow. It belongs on the round table in the middle of the crypt.",
            "Read the note lying on your stool before you sit.",
            "Look at the centre of the table and press E to place the prism.",
            "Then look at your stool and press E to sit.",
            "If the light does not reach the prism, look at the table's rings and turn them with Q and E.",
            "The infrared goggles lie beside the skeleton in the side alcove. Press E to take them and 1 to wear them." } };
        static readonly Level Void = new Level { title = "PHOSPHOR VOID", hints = new[] {
            "Press 2 to take out the UV baton. Its light reveals what the eye cannot see.",
            "Sweep the beam over the floor: hidden footprints appear. After a while they glow on their own.",
            "Follow the footprints. They lead to a red button.",
            "Read the note on the button, then look at the button and press E." } };
        static readonly Level Final = new Level { title = "BACK AGAIN", hints = new[] {
            "Look around the hall: the remains, the tally marks, the figure on the throne.",
            "Look closely at the skeleton, his goggles and his necklace.",
            "A journal lies on the seat beside him. Look at it and press E to pick it up." } };
        static readonly Level ShadowRun = new Level { title = "THE SHADOW RUN", hints = new[] {
            "Space, Enter or a click moves the ghost's conversation along.",
            "Walk with A and D or the arrow keys. Aim the torch with the mouse.",
            "Point the torch at the shadows to hold them back, and keep walking." } };
        static readonly Level WarmStatues = new Level { title = "THE WARM STATUES", hints = new[] {
            "Move with W A S D or the arrow keys and push the statues onto the plates.",
            "Press G to put the goggles on. They show the hidden plates and which statue is still warm.",
            "Warm statue on a warm plate, cold statue on a cold plate.",
            "The stone statues move while the goggles are on, so do not wear them too long.",
            "Press R to start again if you get stuck." } };
        static readonly Level LightBlade = new Level { title = "THE LIGHT BLADE", hints = new[] {
            "Move with W A S D or the arrows. Left click or Space slashes; every third slash is a spin.",
            "Right click or Shift dashes. Use it to slip past lunges and orbs.",
            "Survive until the shadows stop coming, then cut down the last one.",
            "When the way opens, walk through the arch. In the hall, walk with A and D and press E at the button.",
            "If you fall a few times, the light holds a little longer on the next try." } };

        GUIStyle titleStyle, buttonStyle, hintStyle, smallStyle;
        Font font;
        Level shown;

        Level CurrentLevel()
        {
            string scene = SceneManager.GetActiveScene().name;
            if (scene.Contains("Shadow")) return ShadowRun;
            if (scene.Contains("WarmStatues")) return WarmStatues;
            if (scene.Contains("LightBlade")) return LightBlade;
            FPCharacterMover mover = FindAnyObjectByType<FPCharacterMover>();
            float x = mover != null ? mover.transform.position.x : 0f;
            if (x < 25f) return Nexus;
            if (x < 73f) return Hollows;
            if (x < 89f) return Passage;
            if (x < 136f) return Ember;
            if (x < 152f) return Passage;
            if (x < 196f) return Void;
            if (x < 212f) return Passage;
            return Final;
        }

        void Setup()
        {
            if (titleStyle != null) return;
            font = Resources.Load<Font>("WarmStatues/DotGothic16");
            titleStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Normal };
            buttonStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter };
            hintStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperLeft, wordWrap = true };
            smallStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter };
            if (font != null) { titleStyle.font = font; buttonStyle.font = font; smallStyle.font = font; }
        }

        void OnGUI()
        {
            if (!Open) return;
            Setup();
            GUI.depth = -1000;
            float u = Screen.height / 1080f;
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, new Color(0f, 0f, 0f, 0.55f), 0f, 0f);
            titleStyle.fontSize = Mathf.RoundToInt(64 * u);
            buttonStyle.fontSize = Mathf.RoundToInt(34 * u);
            hintStyle.fontSize = Mathf.RoundToInt(28 * u);
            smallStyle.fontSize = Mathf.RoundToInt(22 * u);
            DrawHints(u);
        }

        void DrawHints(float u)
        {
            float w = Mathf.Min(Screen.width - 80f * u, 1100f * u);
            float inner = w - 120f * u;
            float listH = 0f;
            for (int i = 0; i < shown.hints.Length; i++) listH += hintStyle.CalcHeight(new GUIContent(shown.hints[i]), inner - 50f * u) + 18f * u;
            float h = Mathf.Min(Screen.height - 60f * u, 230f * u + listH + 120f * u);
            var panel = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);
            Panel(panel, u);
            Label(new Rect(panel.x, panel.y + 30f * u, w, 70f * u), "HINTS", titleStyle, new Color(0.96f, 0.93f, 0.86f));
            Label(new Rect(panel.x, panel.y + 105f * u, w, 36f * u), shown.title, smallStyle, new Color(1f, 0.78f, 0.45f, 0.9f));
            float y = panel.y + 170f * u;
            float x = panel.x + 60f * u;
            for (int i = 0; i < shown.hints.Length; i++)
            {
                float hh = hintStyle.CalcHeight(new GUIContent(shown.hints[i]), inner - 50f * u);
                Label(new Rect(x, y, 44f * u, hh), (i + 1) + ".", hintStyle, new Color(1f, 0.78f, 0.45f));
                Label(new Rect(x + 50f * u, y, inner - 50f * u, hh), shown.hints[i], hintStyle, new Color(0.95f, 0.93f, 0.88f));
                y += hh + 18f * u;
            }
            float bw = 260f * u, bh = 64f * u;
            if (Button(new Rect(panel.x + (w - bw) * 0.5f, panel.yMax - bh - 40f * u, bw, bh), "BACK", u)) Close();
        }

        static void Panel(Rect r, float u)
        {
            GUI.DrawTexture(new Rect(r.x - 6f * u, r.y - 4f * u, r.width + 12f * u, r.height + 14f * u), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, new Color(0f, 0f, 0f, 0.35f), 0f, 22f * u);
            GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, new Color(0.06f, 0.055f, 0.07f, 0.92f), 0f, 18f * u);
            GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, new Color(1f, 0.8f, 0.5f, 0.28f), Mathf.Max(1f, 2f * u), 18f * u);
        }

        static void Label(Rect r, string text, GUIStyle s, Color c)
        {
            s.normal.textColor = new Color(0f, 0f, 0f, 0.6f * c.a);
            GUI.Label(new Rect(r.x + 2f, r.y + 2f, r.width, r.height), text, s);
            s.normal.textColor = c;
            GUI.Label(r, text, s);
        }

        bool Button(Rect r, string text, float u)
        {
            bool over = r.Contains(Event.current.mousePosition);
            Color fill = over ? new Color(1f, 0.78f, 0.45f, 0.95f) : new Color(1f, 1f, 1f, 0.1f);
            GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, fill, 0f, 10f * u);
            GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, new Color(1f, 0.85f, 0.6f, over ? 0f : 0.35f), Mathf.Max(1f, 2f * u), 10f * u);
            Label(r, text, buttonStyle, over ? new Color(0.1f, 0.07f, 0.04f) : new Color(0.96f, 0.93f, 0.86f));
            return Event.current.type == EventType.MouseUp && Event.current.button == 0 && over && Consume();
        }

        static bool Consume()
        {
            Event.current.Use();
            return true;
        }

        static bool EscPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && (Keyboard.current.escapeKey.wasPressedThisFrame || Keyboard.current.backspaceKey.wasPressedThisFrame);
#else
            return Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Backspace);
#endif
        }
    }
}
