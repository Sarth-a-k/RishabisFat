using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Casa del Silencio - pause menu.
//
// ESC (or Start on a gamepad) pauses: the game freezes, the frame behind blurs and darkens, a dark
// ragged band slides in from the left with the game title at the top (SILENCIO glitches now and then,
// like the main menu) and two buttons in the main menu's style: RESUME and HINTS.
// Mouse hover, W/S or arrow keys (or the d-pad) select; click, Enter or Space (or A) press.
// HINTS does nothing yet - hook your own function onto "On Hints" when you have hints to show.
//
// Setup: put this on one empty GameObject in your first gameplay scene. It survives scene loads,
// and stays asleep in the scenes listed in "Disabled In Scenes" (the main menu by default).
// Everything else is built at runtime from Resources/PauseMenu.
public class PauseMenu : MonoBehaviour
{
    [Header("Behaviour")]
    [Tooltip("Scenes where the pause menu never opens (e.g. the main menu).")]
    public string[] disabledInScenes = { "MainMenu" };
    public bool keepAcrossScenes = true;
    [Tooltip("Pause all game audio while the menu is open.")]
    public bool pauseAudio = true;
    [Tooltip("Show and unlock the mouse cursor while paused (restored on resume).")]
    public bool freeCursor = true;
    [Tooltip("Optional: scripts to switch off while paused (e.g. mouse look), switched back on when resuming.")]
    public MonoBehaviour[] disableWhilePaused;
    [Range(0f, 1f)] public float backgroundDim = 0.45f;

    [Header("Buttons")]
    [Tooltip("Called when HINTS is pressed. Empty for now.")]
    public UnityEvent onHints = new UnityEvent();
    public UnityEvent onPaused = new UnityEvent();
    public UnityEvent onResumed = new UnityEvent();

    [Header("Sound")]
    [Range(0f, 1f)] public float uiVolume = 0.6f;

    // Other systems (cutscenes, transitions) can set this to stop the menu opening.
    public static bool Blocked;
    public static bool IsPaused { get; private set; }
    public static PauseMenu Instance { get; private set; }

    const string Dir = "PauseMenu/";
    const float BandX = 430f;                 // band centre, from the left edge, on a 1920x1080 screen
    static readonly float[] ButtonY = { 520f, 625f };
    static readonly Vector2 HitSize = new Vector2(280f, 64f);

    Canvas canvas;
    CanvasGroup group;
    RawImage blurImage;
    Image dim, band, titleTop, titleBottom, arrow, escHint;
    readonly Image[] buttons = new Image[2];
    Sprite[] normal = new Sprite[2], hot = new Sprite[2];
    Sprite silencio;
    Sprite[] silencioGlitch;
    RectTransform left;                       // everything on the band, anchored to the left edge
    readonly RectTransform[] specks = new RectTransform[40];
    readonly Vector4[] speckData = new Vector4[40];
    RenderTexture blurRT;
    AudioSource ui;
    AudioClip hoverClip, clickClip;

    int selected = -1;
    bool open, busy;
    float prevTimeScale = 1f;
    CursorLockMode prevLock;
    bool prevVisible;
    float glitchUntil, nextGlitch;
    Vector2 lastMouse;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { Blocked = false; IsPaused = false; Instance = null; }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        if (keepAcrossScenes) { transform.SetParent(null); DontDestroyOnLoad(gameObject); }
        Build();
        SetVisible(false);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (Instance == this) { Instance = null; if (IsPaused) ForceResume(); }
        if (blurRT != null) blurRT.Release();
    }

    void OnSceneLoaded(Scene s, LoadSceneMode m) { if (open) ForceResume(); }

    bool AllowedHere()
    {
        string n = SceneManager.GetActiveScene().name;
        foreach (string d in disabledInScenes) if (d == n) return false;
        return !Blocked && !CutsceneGate.Active;
    }

    // ------------------------------------------------------------------ build
    void Build()
    {
        var cgo = new GameObject("PauseMenu UI");
        cgo.transform.SetParent(transform, false);
        canvas = cgo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5000;
        var scaler = cgo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 1f;
        group = cgo.AddComponent<CanvasGroup>();
        group.blocksRaycasts = true;            // nothing in the game can be clicked behind the menu

        blurImage = new GameObject("Blurred frame", typeof(RectTransform)).AddComponent<RawImage>();
        blurImage.transform.SetParent(cgo.transform, false);
        Stretch(blurImage.rectTransform);
        blurImage.raycastTarget = false;

        dim = NewImage(cgo.transform, "Dim", null);
        dim.color = new Color(0.02f, 0.025f, 0.05f, backgroundDim);
        Stretch(dim.rectTransform);

        Sprite speck = Load("speck");
        for (int i = 0; i < specks.Length; i++)
        {
            var im = NewImage(cgo.transform, "Speck", speck);
            specks[i] = im.rectTransform;
            specks[i].anchorMin = specks[i].anchorMax = Vector2.zero;
            speckData[i] = new Vector4(Random.value, Random.value, Random.Range(5f, 14f), Random.Range(0.12f, 0.4f));
        }

        var lgo = new GameObject("Band group", typeof(RectTransform));
        lgo.transform.SetParent(cgo.transform, false);
        left = (RectTransform)lgo.transform;
        left.anchorMin = new Vector2(0f, 0f); left.anchorMax = new Vector2(0f, 1f);
        left.pivot = new Vector2(0.5f, 0.5f);
        left.sizeDelta = new Vector2(560f, 0f);
        left.anchoredPosition = new Vector2(BandX, 0f);

        band = NewImage(left, "Band", Load("band"));
        Stretch(band.rectTransform);

        titleTop = NewImage(left, "Title CASA DEL", Load("title_casa_del"));
        PlaceTop(titleTop, 170f, 0.82f);
        silencio = Load("title_silencio");
        silencioGlitch = new Sprite[10];
        for (int i = 0; i < 10; i++) silencioGlitch[i] = Load("title_silencio_glitch_" + i.ToString("00"));
        titleBottom = NewImage(left, "Title SILENCIO", silencio);
        PlaceTop(titleBottom, 250f, 1.12f);

        string[] names = { "resume", "hints" };
        for (int i = 0; i < 2; i++)
        {
            normal[i] = Load("btn_" + names[i]);
            hot[i] = Load("btn_" + names[i] + "_hot");
            buttons[i] = NewImage(left, names[i].ToUpper(), normal[i]);
            PlaceTop(buttons[i], ButtonY[i], 1f);
        }
        arrow = NewImage(left, "Arrow", Load("arrow"));
        PlaceTop(arrow, ButtonY[0], 1f);
        escHint = NewImage(left, "ESC hint", Load("esc_hint"));
        PlaceTop(escHint, 985f, 1f);
        escHint.color = new Color(1f, 1f, 1f, 0.8f);

        ui = gameObject.AddComponent<AudioSource>();
        ui.playOnAwake = false; ui.spatialBlend = 0f; ui.ignoreListenerPause = true;
        hoverClip = Resources.Load<AudioClip>(Dir + "ui_hover");
        clickClip = Resources.Load<AudioClip>(Dir + "ui_click");
    }

    // ------------------------------------------------------------------ open / close
    void Update()
    {
        if (FPCharacter.HintsScreen.Open || FPCharacter.HintsScreen.ClosedThisFrame) { if (open) Animate(Time.unscaledTime, 1f); return; }
        if (TogglePressed())
        {
            if (!open && !busy && AllowedHere()) StartCoroutine(Open());
            else if (open && !busy) StartCoroutine(Close());
        }
        if (!open || busy) return;

        float t = Time.unscaledTime;
        Animate(t, 1f);

        // mouse hover
        Vector2 mp = MousePos();
        bool moved = (mp - lastMouse).sqrMagnitude > 1f;
        lastMouse = mp;
        int over = -1;
        for (int i = 0; i < 2; i++) if (Over(buttons[i].rectTransform, mp)) over = i;
        if (moved && over >= 0) Select(over);
        if (over >= 0 && ClickDown()) { Press(over); return; }

        // keyboard / gamepad
        if (UpPressed()) Select(selected <= 0 ? 1 : selected - 1);
        if (DownPressed()) Select(selected >= 1 ? 0 : selected + 1);
        if (SubmitPressed() && selected >= 0) Press(selected);
        if (BackPressed()) StartCoroutine(Close());
    }

    public void Pause() { if (!open && !busy && AllowedHere()) StartCoroutine(Open()); }
    public void Resume() { if (open && !busy) StartCoroutine(Close()); }

    IEnumerator Open()
    {
        busy = true;
        // grab the frame before anything of the menu is visible, then freeze the game
        yield return new WaitForEndOfFrame();
        CaptureBlur();
        prevTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        if (pauseAudio) AudioListener.pause = true;
        if (disableWhilePaused != null) foreach (var m in disableWhilePaused) if (m != null) m.enabled = false;
        if (freeCursor) { prevLock = Cursor.lockState; prevVisible = Cursor.visible; Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
        IsPaused = true; open = true;
        onPaused?.Invoke();
        SetVisible(true);
        lastMouse = MousePos();
        Select(0, false);
        nextGlitch = Time.unscaledTime + Random.Range(1.2f, 2.5f);

        for (float f = 0f; f < 1f; f += Time.unscaledDeltaTime / 0.45f)
        {
            Animate(Time.unscaledTime, f);
            yield return null;
        }
        Animate(Time.unscaledTime, 1f);
        busy = false;
    }

    IEnumerator Close()
    {
        busy = true;
        Play(clickClip, 0.8f);
        for (float f = 1f; f > 0f; f -= Time.unscaledDeltaTime / 0.25f)
        {
            Animate(Time.unscaledTime, f);
            yield return null;
        }
        ForceResume();
        busy = false;
    }

    void ForceResume()
    {
        SetVisible(false);
        Time.timeScale = prevTimeScale <= 0f ? 1f : prevTimeScale;
        if (pauseAudio) AudioListener.pause = false;
        if (disableWhilePaused != null) foreach (var m in disableWhilePaused) if (m != null) m.enabled = true;
        if (freeCursor) { Cursor.lockState = prevLock; Cursor.visible = prevVisible; }
        IsPaused = false; open = false;
        onResumed?.Invoke();
    }

    void Press(int i)
    {
        if (i == 0) { StartCoroutine(Close()); return; }
        Play(clickClip, 0.8f);
        onHints?.Invoke();                    // HINTS: nothing yet
    }

    void Select(int i, bool sound = true)
    {
        if (i == selected) return;
        selected = i;
        for (int b = 0; b < 2; b++) buttons[b].sprite = b == selected ? hot[b] : normal[b];
        if (sound) Play(hoverClip, 0.5f);
    }

    // k: 0 = hidden, 1 = fully shown (used for the slide in / out)
    void Animate(float t, float k)
    {
        float e = 1f - (1f - Mathf.Clamp01(k)) * (1f - Mathf.Clamp01(k));
        blurImage.color = new Color(1f, 1f, 1f, e);
        dim.color = new Color(0.02f, 0.025f, 0.05f, backgroundDim * e);
        left.anchoredPosition = new Vector2(Mathf.Lerp(-320f, BandX, e), 0f);
        band.color = new Color(1f, 1f, 1f, e);

        float tk = Mathf.Clamp01((k - 0.25f) / 0.6f);
        float titleGlow = Mathf.Lerp(0.85f, 1f, Mathf.PerlinNoise(t * 0.8f, 0f));
        titleTop.color = new Color(1f, 1f, 1f, tk * titleGlow);
        titleBottom.color = new Color(1f, 1f, 1f, tk * titleGlow);
        for (int i = 0; i < 2; i++)
        {
            float bk = Mathf.Clamp01((k - 0.4f - i * 0.12f) / 0.45f);
            buttons[i].color = new Color(1f, 1f, 1f, bk);
            buttons[i].rectTransform.anchoredPosition = new Vector2(-24f * (1f - bk), TopY(ButtonY[i]));
        }
        arrow.color = new Color(1f, 1f, 1f, Mathf.Clamp01((k - 0.6f) / 0.4f) * (selected >= 0 ? 1f : 0f));
        if (selected >= 0)
            arrow.rectTransform.anchoredPosition = new Vector2(-170f + Mathf.Sin(t * 4f) * 4f, TopY(ButtonY[selected]));
        escHint.color = new Color(1f, 1f, 1f, 0.8f * Mathf.Clamp01((k - 0.7f) / 0.3f));

        // SILENCIO glitches every few seconds, as on the main menu
        if (t > nextGlitch) { glitchUntil = t + Random.Range(0.12f, 0.3f); nextGlitch = t + Random.Range(4f, 9f); }
        titleBottom.sprite = t < glitchUntil ? silencioGlitch[(int)(t * 24f) % silencioGlitch.Length] : silencio;

        // slow drifting dust
        var root = (RectTransform)canvas.transform;
        float W = root.rect.width, H = root.rect.height;
        for (int i = 0; i < specks.Length; i++)
        {
            Vector4 d = speckData[i];
            float x = Mathf.Repeat(d.x * W + Mathf.Sin(t * 0.3f + i) * 30f + t * d.z * 0.6f, W);
            float y = Mathf.Repeat(d.y * H - t * d.z, H);
            specks[i].anchoredPosition = new Vector2(x, y);
            specks[i].sizeDelta = Vector2.one * (4f + d.z * 0.7f);
            specks[i].GetComponent<Image>().color = new Color(0.85f, 0.88f, 1f, d.w * e);
        }
    }

    void CaptureBlur()
    {
        Texture2D shot = ScreenCapture.CaptureScreenshotAsTexture();
        int w = Mathf.Max(16, shot.width / 8), h = Mathf.Max(16, shot.height / 8);
        if (blurRT == null || blurRT.width != w || blurRT.height != h)
        {
            if (blurRT != null) blurRT.Release();
            blurRT = new RenderTexture(w, h, 0) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        }
        var mid = RenderTexture.GetTemporary(shot.width / 3, shot.height / 3, 0);
        mid.filterMode = FilterMode.Bilinear;
        Graphics.Blit(shot, mid);               // two bilinear downsamples = a cheap, soft blur
        Graphics.Blit(mid, blurRT);
        RenderTexture.ReleaseTemporary(mid);
        Destroy(shot);
        blurImage.texture = blurRT;
    }

    void SetVisible(bool v)
    {
        if (canvas == null) return;
        canvas.enabled = v;
        group.blocksRaycasts = v;
        if (!v) selected = -1;
    }

    // ------------------------------------------------------------------ helpers
    void Play(AudioClip c, float v) { if (c != null) ui.PlayOneShot(c, v * uiVolume); }

    static float TopY(float y) { return 540f - y; }

    void PlaceTop(Image im, float y, float scale)
    {
        var rt = im.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        Vector2 size = im.sprite != null ? im.sprite.rect.size : new Vector2(100f, 100f);
        rt.sizeDelta = size * scale;
        rt.anchoredPosition = new Vector2(0f, TopY(y));
    }

    static bool Over(RectTransform rt, Vector2 screen)
    {
        Vector2 local;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, screen, null, out local)) return false;
        return Mathf.Abs(local.x) <= HitSize.x * 0.5f && Mathf.Abs(local.y) <= HitSize.y * 0.5f;
    }

    static Sprite Load(string file)
    {
        var tex = Resources.Load<Texture2D>(Dir + file);
        if (tex == null) { Debug.LogError("PauseMenu: missing Assets/Resources/" + Dir + file + ".png"); return null; }
        return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
    }

    static Image NewImage(Transform parent, string name, Sprite sprite)
    {
        var g = new GameObject(name, typeof(RectTransform));
        g.transform.SetParent(parent, false);
        var im = g.AddComponent<Image>();
        im.sprite = sprite;
        im.raycastTarget = false;
        return im;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    // ------------------------------------------------------------------ input (old and new input systems)
    static Vector2 MousePos()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null ? Mouse.current.position.ReadValue() : new Vector2(-1f, -1f);
#else
        return Input.mousePosition;
#endif
    }

    static bool ClickDown()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
#else
        return Input.GetMouseButtonDown(0);
#endif
    }

    static bool TogglePressed()
    {
#if ENABLE_INPUT_SYSTEM
        return (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            || (Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame);
#else
        return Input.GetKeyDown(KeyCode.Escape);
#endif
    }

    static bool UpPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return (Keyboard.current != null && (Keyboard.current.wKey.wasPressedThisFrame || Keyboard.current.upArrowKey.wasPressedThisFrame))
            || (Gamepad.current != null && Gamepad.current.dpad.up.wasPressedThisFrame);
#else
        return Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow);
#endif
    }

    static bool DownPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return (Keyboard.current != null && (Keyboard.current.sKey.wasPressedThisFrame || Keyboard.current.downArrowKey.wasPressedThisFrame))
            || (Gamepad.current != null && Gamepad.current.dpad.down.wasPressedThisFrame);
#else
        return Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow);
#endif
    }

    static bool SubmitPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return (Keyboard.current != null && (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame || Keyboard.current.spaceKey.wasPressedThisFrame))
            || (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame);
#else
        return Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space);
#endif
    }

    static bool BackPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame;
#else
        return false;
#endif
    }
}
