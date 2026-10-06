using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Casa del Silencio: the Light Blade sequence.
// Part 1: 45-second fight against the shadows on the brick platform (perspective view).
// Part 2: through the arch into the 2D pixel hall, the second button, the old man, the dissolve.
// Then onFinished fires (and nextSceneName loads, if set) so the 3D game can carry on.
//
// Put this on an empty GameObject. Everything is built at runtime from Resources/LightBlade.
// It makes its own camera and removes it again when the sequence ends.
public class LightBladeSequence : MonoBehaviour
{
    [Header("Files")]
    public string folder = "LightBlade";
    [Header("Fight")]
    public int hitPoints = 6;
    public float fightSeconds = 45f;
    public int maxShadowsAtOnce = 10;
    [Header("Easier after repeated deaths")]
    [Tooltip("After the player has died more than this many times, every new try gets a little easier.")]
    public bool easierAfterDeaths = true;
    public int deathsBeforeEasier = 2;
    [Tooltip("How many steps easier it can get at most (each death past the limit adds one step).")]
    public int maxEaseSteps = 3;
    [Header("Gate arrow")]
    public float arrowBobHeight = 10f;
    public float arrowBobSpeed = 3f;
    public float arrowHeight = 150f;
    [Header("Sound")]
    [Range(0f, 1f)] public float musicVolume = 0.5f;
    public float musicDelay = 4.5f;
    [Range(0f, 1f)] public float sfxVolume = 0.7f;
    [Header("Flow")]
    public bool playOnStart = true;
    public bool skipFight = false;          // start straight in the hall (for testing)
    public string nextSceneName = "";       // optional: loaded when the sequence ends
    public UnityEvent onFinished;           // optional: called when the sequence ends

    enum Mode { Idle, Play, Won, Leave, Dead, Hall, End }
    Mode mode = Mode.Idle;

    const float U = 0.01f, HW = 520f, HH = 400f, K = 0.6f;
    const float CL = 800f, PITCH = 36f;
    static readonly float ST = Mathf.Sin(PITCH * Mathf.Deg2Rad), CT = Mathf.Cos(PITCH * Mathf.Deg2Rad);
    static readonly float CF = CL * 1.22f, HC = CL * ST, DC = CL * CT;

    GameObject camObj; Camera cam; Transform fightRoot, hallRoot;
    AudioSource music, sfx; readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
    readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
    Sprite sqSprite; Texture2D blackTex, whiteTex; Font font; GUIStyle capStyle, bigStyle, dlgStyle, smallStyle, ctrlStyle;
    readonly Stack<SpriteRenderer> pool = new Stack<SpriteRenderer>();
    float tNow, fadeBlack; string cap = ""; float capT;

    // ------------------------------------------------------------------ helpers
    static Color Hex(string h, float a = 1f) { Color c; ColorUtility.TryParseHtmlString(h, out c); c.a = a; return c; }
    static float Rnd(float a, float b) { return Random.Range(a, b); }
    static Vector3 W(float x, float y, float h = 0f) { return new Vector3(x * U, h * U, -y * U); }
    static int Ord(float y) { return Mathf.Clamp(Mathf.RoundToInt(y * 10f), -12000, 12000); }

    Sprite Spr(string name, float pivotX, float pivotY, float ppu, bool point = false, float rectW = 0f, float rectH = 0f)
    {
        Sprite s; if (sprites.TryGetValue(name, out s)) return s;
        Texture2D t = Resources.Load<Texture2D>(folder + "/" + name);
        if (t == null) { Debug.LogError("LightBladeSequence: missing Resources/" + folder + "/" + name + ".png"); t = Texture2D.whiteTexture; }
        t.filterMode = point ? FilterMode.Point : FilterMode.Bilinear; t.wrapMode = TextureWrapMode.Clamp;
        Rect r = rectW > 0f ? new Rect(0, t.height - rectH, rectW, rectH) : new Rect(0, 0, t.width, t.height);
        s = Sprite.Create(t, r, new Vector2(pivotX / r.width, pivotY / r.height), ppu, 0, SpriteMeshType.FullRect);
        sprites[name] = s; return s;
    }
    SpriteRenderer SR(string name, Sprite s, Transform parent, int order)
    {
        GameObject g = new GameObject(name); g.transform.SetParent(parent, false);
        SpriteRenderer r = g.AddComponent<SpriteRenderer>(); r.sprite = s; r.sortingOrder = order; return r;
    }
    SpriteRenderer Pooled(Sprite s, Transform parent, int order)
    {
        SpriteRenderer r = pool.Count > 0 ? pool.Pop() : SR("fx", s, parent, order);
        r.transform.SetParent(parent, false); r.sprite = s; r.sortingOrder = order; r.color = Color.white; r.flipX = false;
        r.transform.localScale = Vector3.one; r.transform.localRotation = Quaternion.identity; r.gameObject.SetActive(true); return r;
    }
    void Free(SpriteRenderer r) { if (r == null) return; r.gameObject.SetActive(false); pool.Push(r); }
    void Sfx(string name, float vol = 1f, float pitchJitter = 0f)
    {
        AudioClip c; if (!clips.TryGetValue(name, out c)) { c = Resources.Load<AudioClip>(folder + "/" + name); clips[name] = c; }
        if (c == null || sfx == null) return;
        sfx.pitch = 1f + Rnd(-pitchJitter, pitchJitter); sfx.PlayOneShot(c, vol * sfxVolume);
    }
    void Say(string t, float d) { cap = t; capT = d; }

    // ------------------------------------------------------------------ input (old and new input systems)
    Vector2 lastMouse; bool useMouse;
    bool Held(int dir) // 0 left, 1 right, 2 up, 3 down
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard k = Keyboard.current; if (k == null) return false;
        if (dir == 0) return k.aKey.isPressed || k.leftArrowKey.isPressed;
        if (dir == 1) return k.dKey.isPressed || k.rightArrowKey.isPressed;
        if (dir == 2) return k.wKey.isPressed || k.upArrowKey.isPressed;
        return k.sKey.isPressed || k.downArrowKey.isPressed;
#else
        if (dir == 0) return Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow);
        if (dir == 1) return Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow);
        if (dir == 2) return Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow);
        return Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow);
#endif
    }
    bool KeyAttack()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard k = Keyboard.current; return k != null && (k.spaceKey.wasPressedThisFrame || k.jKey.wasPressedThisFrame);
#else
        return Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.J);
#endif
    }
    bool KeyDash()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard k = Keyboard.current; return k != null && (k.leftShiftKey.wasPressedThisFrame || k.rightShiftKey.wasPressedThisFrame || k.kKey.wasPressedThisFrame);
#else
        return Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift) || Input.GetKeyDown(KeyCode.K);
#endif
    }
    bool KeyAct()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard k = Keyboard.current; return k != null && (k.eKey.wasPressedThisFrame || k.spaceKey.wasPressedThisFrame || k.enterKey.wasPressedThisFrame);
#else
        return Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return);
#endif
    }
    bool MouseDown(int button)
    {
#if ENABLE_INPUT_SYSTEM
        Mouse m = Mouse.current; if (m == null) return false;
        return button == 0 ? m.leftButton.wasPressedThisFrame : m.rightButton.wasPressedThisFrame;
#else
        return Input.GetMouseButtonDown(button);
#endif
    }
    Vector2 MousePos()
    {
#if ENABLE_INPUT_SYSTEM
        Mouse m = Mouse.current; return m == null ? lastMouse : m.position.ReadValue();
#else
        return Input.mousePosition;
#endif
    }

    // ------------------------------------------------------------------ life cycle
    void Start() { if (playOnStart) Begin(); }

    public void Begin()
    {
        if (camObj != null) return;
        camObj = new GameObject("LightBlade_Camera"); camObj.transform.SetParent(transform, false);
        cam = camObj.AddComponent<Camera>(); cam.depth = 100; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Hex("#030109");
        cam.nearClipPlane = 0.1f; cam.farClipPlane = 200f;
        if (FindAnyObjectByType<AudioListener>() == null) camObj.AddComponent<AudioListener>();
        music = gameObject.AddComponent<AudioSource>(); music.loop = true; music.playOnAwake = false; music.volume = 0f;
        music.clip = Resources.Load<AudioClip>(folder + "/bgm");
        sfx = gameObject.AddComponent<AudioSource>(); sfx.playOnAwake = false;
        font = Resources.Load<Font>(folder + "/DotGothic16");
        blackTex = new Texture2D(1, 1); blackTex.SetPixel(0, 0, Color.black); blackTex.Apply();
        whiteTex = new Texture2D(4, 4); Color[] wp = new Color[16]; for (int i = 0; i < 16; i++) wp[i] = Color.white; whiteTex.SetPixels(wp); whiteTex.Apply(); whiteTex.filterMode = FilterMode.Point;
        sqSprite = Sprite.Create(whiteTex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
        lastMouse = MousePos();
        if (skipFight) EnterHall(); else { BuildFight(); StartFight(); }
    }

    void Update()
    {
        if (mode == Mode.Idle || cam == null) return;
        float dt = Mathf.Min(0.05f, Time.deltaTime); tNow += dt;
        if (capT > 0f && capT < 90f) capT -= dt;
        Vector2 mp = MousePos(); if ((mp - lastMouse).sqrMagnitude > 4f) { useMouse = true; lastMouse = mp; }
        if (mode == Mode.Play || mode == Mode.Won || mode == Mode.Leave || mode == Mode.Dead) UpdateFight(dt);
        else if (mode == Mode.Hall) UpdateHall(dt);
        UpdateMusic(dt);
    }

    float musicT = -1f, musicFadeFrom = -1f, musicFadeT;
    void StartMusic() { if (music == null || music.clip == null) return; music.Stop(); music.volume = 0f; musicT = 0f; musicFadeFrom = -1f; }
    void UpdateMusic(float dt)
    {
        if (music == null || music.clip == null) return;
        if (musicFadeFrom >= 0f)
        {   // fading out in the hall: starts 1.5 s in, gone 1.5 s later
            musicFadeT += dt; float k = Mathf.Clamp01((musicFadeT - 1.5f) / 1.5f); music.volume = musicFadeFrom * (1f - k);
            if (k >= 1f) { music.Stop(); musicFadeFrom = -1f; }
            return;
        }
        if (musicT < 0f) return; musicT += dt;
        if (musicT >= musicDelay) { if (!music.isPlaying) music.Play(); music.volume = musicVolume * Mathf.Clamp01((musicT - musicDelay) / 2f); }
    }

    void Finish()
    {
        mode = Mode.Idle;
        if (fightRoot != null) Destroy(fightRoot.gameObject);
        if (hallRoot != null) Destroy(hallRoot.gameObject);
        if (camObj != null) Destroy(camObj);
        if (music != null) music.Stop();
        deaths = 0;                          // the fight was won: the next play-through starts at normal difficulty
        if (onFinished != null) onFinished.Invoke();
        if (!string.IsNullOrEmpty(nextSceneName)) SceneManager.LoadScene(nextSceneName);
    }

    // ================================================================== PART 1: THE FIGHT
    class En { public float x, y, hp, h, sp, t, kx, ky, ph, flash, born, lx, ly; public bool cast, dead; public int st; public Transform root; public SpriteRenderer body, ghost, eyes, blob; }
    class Shot { public float x, y, vx, vy, l; public SpriteRenderer sr, blob; }
    class Part { public float x, y, z, vx, vy, vz, l, s; public bool flash; public Color c; public SpriteRenderer sr; }
    class Fx { public float l, max, a0; public SpriteRenderer sr; }
    class Prop { public float x, y, ph; public int kind; public SpriteRenderer sr, glowA, glowB; }

    readonly List<En> E = new List<En>(); readonly List<Shot> shots = new List<Shot>(); readonly List<Part> parts = new List<Part>();
    readonly List<Fx> fxs = new List<Fx>(); readonly List<Prop> props = new List<Prop>(); readonly List<Transform> bills = new List<Transform>();
    float px, py, pvx, pvy, pface, pinv, pcd, psw, pswA, pswH = 1f, pcomboT, pdash, pdashCd, pdashA, pdx = 1f, pdy, pwalk, pstep;
    int php, pcombo, maxHp = 6, ease;
    static int deaths;                       // kept between tries (and scene reloads) until the fight is won
    float curFightSeconds = 45f, speedMul = 1f, shotMul = 1f; int curMaxShadows = 10;
    SpriteRenderer archSr, gateArrow; float arrowA;
    const float ArchTop = 3.33f;             // arch.png: visible top is 666 px above the pivot, at 200 px per unit
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetDeaths() { deaths = 0; } float T, spawnT, wonT, leaveT, camX, camY = 110f, shake, stopT; int spawned; bool hinted;
    Transform pRoot; SpriteRenderer pBody, pArm, pSword, pBlob, pGlow, archGlow, corpse; float corpseT; bool hasCorpse; float corpseX, corpseY;
    Sprite sShadowA, sShadowB, sShadowW, sEyes, sBlob, sGlow, sOrb, sSlash, sSpin, sHunterIdle, sHunterW1, sHunterW2;

    void BuildFight()
    {
        fightRoot = new GameObject("LightBlade_Fight").transform; fightRoot.SetParent(transform, false);
        cam.orthographic = false; cam.fieldOfView = 2f * Mathf.Atan(300f / CF) * Mathf.Rad2Deg; cam.backgroundColor = Hex("#030109");
        cam.transform.rotation = Quaternion.Euler(PITCH - Mathf.Atan(50f / CF) * Mathf.Rad2Deg, 0f, 0f);
        Quaternion flat = Quaternion.Euler(90f, 0f, 0f);

        SpriteRenderer floor = SR("Floor", Spr("floor", 1024, 1024, 100f), fightRoot, -30000);
        floor.transform.rotation = flat; floor.transform.localScale = new Vector3(1640f / 2048f, 1500f / 2048f, 1f);

        sShadowA = Spr("shadow_a", 128, 20, 200f); sShadowB = Spr("shadow_b", 128, 20, 200f); sShadowW = Spr("shadow_w", 128, 20, 200f);
        sEyes = Spr("eyes", 32, 16, 200f); sBlob = Spr("blob", 64, 64, 100f); sGlow = Spr("glow", 128, 128, 100f); sOrb = Spr("orb", 32, 32, 200f);
        sSlash = Spr("slash", 256, 256, 100f); sSpin = Spr("spin", 256, 256, 100f);
        sHunterIdle = Spr("hunter_idle", 128, 24, 200f); sHunterW1 = Spr("hunter_walk1", 128, 24, 200f); sHunterW2 = Spr("hunter_walk2", 128, 24, 200f);

        // scenery (same layout as the browser demo)
        AddProp(5, 0, -HH - 110, Spr("arch", 512, 34, 200f), 1f, 1f); archSr = props[props.Count - 1].sr;
        float[] backX = { -560, -430, -300, -170, 170, 300, 430, 560 };
        foreach (float x in backX) AddProp(0, x, -HH - Rnd(60, 150), Spr("pillar", 128, 28, 200f), Rnd(30, 42) / 36f, Rnd(110, 230) / 200f);
        for (int sg = -1; sg <= 1; sg += 2) for (float y = -HH + 40; y < HH + 150; y += Rnd(150, 230)) AddProp(0, sg * (HW + Rnd(90, 170)), y, Spr("pillar", 128, 28, 200f), Rnd(30, 40) / 36f, Rnd(80, 190) / 200f);
        for (int i = 0; i < 7; i++) AddProp(0, Rnd(-700, 700), -HH - Rnd(230, 330), Spr("pillar", 128, 28, 200f), Rnd(34, 46) / 36f, Rnd(160, 280) / 200f);
        float[] torchX = { -400, -200, 200, 400 }; foreach (float x in torchX) AddProp(1, x, -HH + 14, null, 1f, 1f);
        for (int sg = -1; sg <= 1; sg += 2) { AddProp(1, sg * (HW - 14), -170, null, 1f, 1f); AddProp(1, sg * (HW - 14), 60, null, 1f, 1f); AddProp(1, sg * (HW - 14), 290, null, 1f, 1f); }
        for (int sx = -1; sx <= 1; sx += 2) for (int sy = -1; sy <= 1; sy += 2) AddProp(2, sx * (HW - 110), sy * (HH - 100), null, 1f, 1f);
        AddProp(2, -HW - 120, 40, null, 1f, 1f); AddProp(2, HW + 120, -220, null, 1f, 1f);
        float[,] sk = { { -HW + 70, -HH + 70, 1, 1 }, { HW - 80, -HH + 60, -1, 0 }, { -HW + 60, 150, 1, 0 }, { HW - 60, -40, -1, 1 }, { -250, HH - 50, 1, 1 }, { 310, HH - 60, -1, 0 }, { -HW - 150, -200, 1, 1 }, { HW + 170, 200, -1, 1 }, { -90, -HH - 60, 1, 0 } };
        for (int i = 0; i < sk.GetLength(0); i++) AddProp(3, sk[i, 0], sk[i, 1], Spr(sk[i, 3] > 0 ? "skel_helm" : "skel_bare", 110, 30, 200f), sk[i, 2], 1f);
        float[,] stakes = { { -HW - 90, -300 }, { HW + 100, -40 }, { -HW - 110, 330 }, { HW + 90, 360 } };
        for (int i = 0; i < 4; i++) AddProp(4, stakes[i, 0], stakes[i, 1], Spr("stake", 64, 20, 200f), 1f, 1f);

        archGlow = SR("ArchGlow", sGlow, fightRoot, Ord(-HH - 110) + 1); archGlow.transform.position = W(0, -HH - 110, 90); archGlow.transform.localScale = Vector3.one * 1.5f;
        archGlow.color = new Color(0.92f, 0.88f, 1f, 0f); bills.Add(archGlow.transform);
        // one arrow above the gate, in the world (so it never jitters against the scene)
        gateArrow = SR("GateArrow", Spr("arrow", 32, 32, 100f), fightRoot, 16000); gateArrow.color = new Color(1f, 1f, 1f, 0f); gateArrow.gameObject.SetActive(false);

        // the hunter
        pRoot = new GameObject("Hunter").transform; pRoot.SetParent(fightRoot, false); pRoot.localScale = Vector3.one * 1.22f; bills.Add(pRoot);
        pBody = SR("Body", sHunterIdle, pRoot, 0); pArm = SR("Arm", Spr("arm", 8, 8, 200f), pRoot, 1); pSword = SR("Sword", Spr("sword", 40, 32, 200f), pRoot, 2);
        pBlob = SR("HunterShadow", sBlob, fightRoot, -19000); pBlob.transform.rotation = flat; pBlob.transform.localScale = new Vector3(0.36f, 0.24f, 1f);
        pGlow = SR("HunterLight", sGlow, fightRoot, -19500); pGlow.transform.rotation = flat; pGlow.transform.localScale = Vector3.one * (380f / 256f); pGlow.color = new Color(0.92f, 0.88f, 1f, 0.3f);
        corpse = SR("OldMan", Spr("warden", 128, 24, 200f), fightRoot, 0); corpse.gameObject.SetActive(false);
    }
    void AddProp(int kind, float x, float y, Sprite s, float sx, float sy)
    {
        Prop p = new Prop(); p.kind = kind; p.x = x; p.y = y; p.ph = Rnd(0f, 6f);
        if (kind == 1) s = Spr("torch_0", 64, 12, 200f); if (kind == 2) s = Spr("pyre_0", 128, 30, 200f);
        p.sr = SR("Prop" + kind, s, fightRoot, Ord(y)); p.sr.transform.position = W(x, y); p.sr.transform.localScale = new Vector3(sx, sy, 1f); bills.Add(p.sr.transform);
        if (kind == 1 || kind == 2)
        {
            bool pyre = kind == 2; Color c = pyre ? new Color(0.2f, 0.82f, 0.94f) : new Color(0.67f, 0.35f, 1f);
            p.glowA = SR("GlowFloor", Spr("glow", 128, 128, 100f), fightRoot, -19800); p.glowA.transform.rotation = Quaternion.Euler(90f, 0f, 0f); p.glowA.transform.position = W(x, y, 0.5f);
            p.glowA.transform.localScale = Vector3.one * ((pyre ? 380f : 240f) / 256f); p.glowA.color = new Color(c.r, c.g, c.b, pyre ? 0.34f : 0.22f);
            p.glowB = SR("GlowFlame", Spr("glow", 128, 128, 100f), fightRoot, Ord(y) + 1); p.glowB.transform.position = W(x, y, pyre ? 48f : 82f);
            p.glowB.transform.localScale = Vector3.one * ((pyre ? 240f : 140f) / 256f); p.glowB.color = new Color(c.r, c.g, c.b, 0.4f); bills.Add(p.glowB.transform);
        }
        props.Add(p);
    }

    void StartFight()
    {
        foreach (En e in E) KillObjects(e); E.Clear();
        foreach (Shot s in shots) { Free(s.sr); Free(s.blob); } shots.Clear();
        foreach (Part p in parts) Free(p.sr); parts.Clear();
        foreach (Fx f in fxs) Free(f.sr); fxs.Clear();
        ApplyDifficulty();
        px = 0; py = 0; pvx = pvy = 0; pface = 0; pinv = 0; pcd = 0; psw = 0; pcombo = 0; pcomboT = 0; pdash = 0; pdashCd = 0; pdx = 1; pdy = 0; pwalk = 0; php = maxHp;
        T = 0; spawnT = 5f; spawned = 0; wonT = 0; leaveT = 0; hinted = false; hasCorpse = false; corpse.gameObject.SetActive(false); camX = 0; camY = 110f; shake = 0; stopT = 0; fadeBlack = 0;
        arrowA = 0f; if (gateArrow != null) gateArrow.gameObject.SetActive(false);
        mode = Mode.Play; StartMusic();
        if (ease > 0) Say(ease == 1 ? "The light feels steadier this time." : "The light holds. The dark is slower now.", 3.5f);
        else Say("They come when the light goes.", 3.5f);
    }
    // After more than "deathsBeforeEasier" deaths each new try is one step easier (up to maxEaseSteps):
    // +1 heart, a shorter fight, fewer shadows at once, slower shadows and orbs, fewer throwers,
    // longer warnings before lunges and throws, and longer safety after being hit.
    void ApplyDifficulty()
    {
        // drastic: once the player has died deathsBeforeEasier times, jump straight to the easiest setting
        ease = easierAfterDeaths && deaths >= deathsBeforeEasier ? Mathf.Max(0, maxEaseSteps) : 0;
        maxHp = hitPoints + ease;
        curFightSeconds = fightSeconds * (1f - 0.14f * ease);
        curMaxShadows = Mathf.Max(4, maxShadowsAtOnce - 2 * ease);
        speedMul = 1f - 0.1f * ease;
        shotMul = 1f - 0.12f * ease;
    }
    void KillObjects(En e) { if (e.root != null) Destroy(e.root.gameObject); if (e.blob != null) Destroy(e.blob.gameObject); e.root = null; e.blob = null; }

    float AimAng()
    {
        Ray r = cam.ScreenPointToRay(MousePos()); if (Mathf.Abs(r.direction.y) < 1e-4f) return pface;
        float k = -r.origin.y / r.direction.y; Vector3 p = r.origin + r.direction * k;
        return Mathf.Atan2(-p.z / U - py, p.x / U - px);
    }
    static float ADiff(float a, float b) { return Mathf.Abs(Mathf.DeltaAngle(a * Mathf.Rad2Deg, b * Mathf.Rad2Deg)) * Mathf.Deg2Rad; }

    void AddPart(float x, float y, float z, float vx, float vy, float vz, float l, float s, Color c, bool flash = false)
    {
        Part p = new Part(); p.x = x; p.y = y; p.z = z; p.vx = vx; p.vy = vy; p.vz = vz; p.l = l; p.s = s; p.c = c; p.flash = flash;
        p.sr = Pooled(flash ? sGlow : sqSprite, fightRoot, 0); parts.Add(p);
    }
    Color AshColor() { float r = Random.value; return r < 0.25f ? Hex("#cdb8ff") : r < 0.5f ? Hex("#3a3348") : Hex("#15101f"); }

    void Attack(float ang)
    {
        if (pcd > 0f || (mode != Mode.Play && mode != Mode.Won)) return;
        pcombo = pcomboT > 0f ? pcombo + 1 : 1; pcomboT = 0.7f; bool wide = pcombo % 3 == 0;
        float range = wide ? 150f : 128f, half = wide ? Mathf.PI : 1.15f; pcd = wide ? 0.4f : 0.22f; psw = 0.15f; pswA = ang; pswH = half; pface = ang;
        if (!wide) { px += Mathf.Cos(ang) * 16f; py += Mathf.Sin(ang) * 16f; }
        // glowing trail left on the floor, plus a brighter quick arc
        for (int i = 0; i < 2; i++)
        {
            Fx f = new Fx(); f.max = f.l = i == 0 ? 0.9f : 0.16f; f.a0 = i == 0 ? 0.8f : 1f; f.sr = Pooled(wide ? sSpin : sSlash, fightRoot, i == 0 ? -15000 : -14000);
            f.sr.transform.position = W(px, py, i == 0 ? 1f : 3f); f.sr.transform.rotation = Quaternion.Euler(0f, ang * Mathf.Rad2Deg, 0f) * Quaternion.Euler(90f, 0f, 0f);
            f.sr.transform.localScale = Vector3.one * (range * (i == 0 ? 0.78f : 0.8f) / 100f); fxs.Add(f);
        }
        Sfx(wide ? "sfx_spin" : "sfx_swing", 0.75f, 0.08f);
        int hit = 0;
        foreach (En e in E)
        {
            if (e.dead) continue; float d = Mathf.Sqrt((e.x - px) * (e.x - px) + (e.y - py) * (e.y - py)); if (d > range + 14f) continue;
            float a = Mathf.Atan2(e.y - py, e.x - px); if (!wide && ADiff(a, ang) > half) continue;
            e.hp -= 1f; hit++; e.kx = Mathf.Cos(a) * 420f; e.ky = Mathf.Sin(a) * 420f; e.flash = 0.1f; if (e.hp <= 0f) Kill(e);
        }
        foreach (Shot o in shots)
        {
            float d = Mathf.Sqrt((o.x - px) * (o.x - px) + (o.y - py) * (o.y - py));
            if (d < range + 10f && (wide || ADiff(Mathf.Atan2(o.y - py, o.x - px), ang) < half)) { o.l = 0f; AddPart(o.x, o.y, 22, 0, 0, 0, 0.15f, 26, Color.white, true); }
        }
        if (hit > 0) { stopT = 0.035f; shake = Mathf.Min(10f, 4f + hit * 1.5f); Sfx("sfx_hit", 0.8f, 0.06f); }
    }
    void Kill(En e)
    {
        e.dead = true;
        bool last = mode == Mode.Play && T >= curFightSeconds; if (last) foreach (En o in E) if (!o.dead) { last = false; break; }
        if (last)
        {   // the last shadow: the old man was inside it
            mode = Mode.Won; wonT = 0; hasCorpse = true; corpseT = 0; corpseX = e.x; corpseY = e.y; shake = 18f; stopT = 0.12f;
            foreach (Shot s in shots) s.l = 0f; AddPart(e.x, e.y, 40, 0, 0, 0, 0.3f, 220, Color.white, true);
            Say("...The old man. He was inside the last one.", 99f); Sfx("sfx_win", 0.7f);
        }
        for (int i = 0; i < 16; i++) AddPart(e.x + Rnd(-10, 10), e.y, Rnd(5, e.h), Rnd(-50, 50) + e.kx * 0.12f, Rnd(-30, 30) + e.ky * 0.12f, Rnd(20, 90), Rnd(0.5f, 1.1f), Rnd(2.5f, 5.5f), AshColor());
        AddPart(e.x, e.y, e.h * 0.5f, 0, 0, 0, 0.12f, 44, Color.white, true);
    }
    void Dash()
    {
        if (pdashCd > 0f || (mode != Mode.Play && mode != Mode.Won)) return; pdash = 0.17f; pdashCd = 0.7f; pinv = Mathf.Max(pinv, 0.25f);
        bool moving = Held(0) || Held(1) || Held(2) || Held(3); pdashA = moving ? Mathf.Atan2(pdy, pdx) : (useMouse ? AimAng() : pface); Sfx("sfx_dash", 0.8f, 0.05f);
    }
    void Hurt()
    {
        if (pinv > 0f || pdash > 0f || mode != Mode.Play) return; php--; pinv = 1.1f + 0.3f * ease; shake = 14f; stopT = 0.07f; Sfx("sfx_hurt", 0.8f);
        foreach (En o in E) { float ox = o.x - px, oy = o.y - py, od = Mathf.Max(1f, Mathf.Sqrt(ox * ox + oy * oy)); if (od < 170f) { o.kx = ox / od * 520f; o.ky = oy / od * 520f; } }
        AddPart(px, py, 30, 0, 0, 0, 0.2f, 150, Color.white, true);
        if (php <= 0) { mode = Mode.Dead; deaths++; Say("The dark takes him. It has before.", 99f); }
    }
    void AddEnemy(float x, float y)
    {
        En e = new En(); e.x = x; e.y = y; e.hp = 1; e.h = Rnd(50, 58); e.sp = Rnd(105, 150) * speedMul; e.cast = Random.value < 0.3f - 0.06f * ease; e.st = 0; e.t = Rnd(0.5f, 2f); e.ph = Rnd(0f, 9f);
        e.root = new GameObject("Shadow").transform; e.root.SetParent(fightRoot, false);
        e.body = SR("Body", sShadowA, e.root, 0); e.ghost = SR("Ghost", sShadowW, e.root, 0); e.ghost.color = new Color(1f, 0.17f, 0.82f, 0.45f); e.ghost.enabled = false;
        e.eyes = SR("Eyes", sEyes, e.root, 0);
        e.blob = SR("ShadowBlob", sBlob, fightRoot, -19000); e.blob.transform.rotation = Quaternion.Euler(90f, 0f, 0f); e.blob.transform.localScale = new Vector3(0.34f, 0.24f, 1f);
        E.Add(e);
    }

    void UpdateFight(float dt)
    {
        if (stopT > 0f) { stopT -= dt; dt = 0f; }
        if (shake > 0f) shake = Mathf.Max(0f, shake - Time.deltaTime * 40f);
        if (mode == Mode.Dead) { if (KeyAttack() || KeyAct() || MouseDown(0)) StartFight(); DrawFight(dt); return; }
        if (mode == Mode.Leave) { leaveT += dt; fadeBlack = Mathf.Clamp01(leaveT / 0.7f); wonT += dt; corpseT += dt; DrawFight(dt); if (leaveT > 0.8f) EnterHall(); return; }

        T += dt;
        if (useMouse && MouseDown(0)) Attack(AimAng()); else if (MouseDown(0)) { useMouse = true; Attack(AimAng()); }
        if (KeyAttack()) { useMouse = false; Attack(pface); }
        if (KeyDash() || MouseDown(1)) Dash();

        float mx = (Held(1) ? 1 : 0) - (Held(0) ? 1 : 0), my = (Held(3) ? 1 : 0) - (Held(2) ? 1 : 0); bool mv = mx != 0 || my != 0;
        if (mv) { float l = Mathf.Sqrt(mx * mx + my * my); pdx = mx / l; pdy = my / l; if (!useMouse) pface = Mathf.Atan2(my, mx); pwalk += dt * 11f; } else pwalk = 0f;
        if (useMouse && psw <= 0f) pface = AimAng();
        float sp = psw > 0f ? 90f : 225f;
        if (pdash > 0f)
        {
            pdash -= dt; px += Mathf.Cos(pdashA) * 760f * dt; py += Mathf.Sin(pdashA) * 760f * dt;
            if (dt > 0f && Random.value < 0.9f) AddPart(px, py, 26, 0, 0, 0, 0.22f, 22, Color.white, true);
        }
        else
        {
            float k = Mathf.Min(1f, dt * (mv ? 16f : 11f)); pvx += ((mv ? pdx * sp : 0f) - pvx) * k; pvy += ((mv ? pdy * sp : 0f) - pvy) * k; px += pvx * dt; py += pvy * dt;
            if (mv && (pstep -= dt) <= 0f) { pstep = 0.16f; AddPart(px - pdx * 8f + Rnd(-4, 4), py - pdy * 8f, 2, -pdx * 20f, -pdy * 20f, 22, 0.4f, 4.5f, new Color(0.78f, 0.75f, 0.9f, 0.5f)); }
        }
        px = Mathf.Clamp(px, -HW + 24f, HW - 24f);
        bool open = mode == Mode.Won && wonT > 1.5f && Mathf.Abs(px) < 100f;
        py = Mathf.Clamp(py, open ? -HH - 140f : Mathf.Max(-HH + 24f, Mathf.Min(py, -HH + 24f)), HH - 24f);
        if (py < -HH + 10f) px = Mathf.Clamp(px, -95f, 95f);
        if (pcd > 0f) pcd -= dt; if (psw > 0f) psw -= dt; if (pinv > 0f) pinv -= dt; if (pdashCd > 0f) pdashCd -= dt; if (pcomboT > 0f) pcomboT -= dt;

        if (mode == Mode.Won)
        {
            wonT += dt; corpseT += dt;
            if (wonT > 4f && !hinted) { hinted = true; Say("The way through the arch is open.", 6f); }
            if (py < -HH - 85f) { mode = Mode.Leave; leaveT = 0f; }
        }
        else
        {
            int alive = E.Count;
            bool want = T < curFightSeconds ? alive < Mathf.Min(curMaxShadows, 5f + T / (6f + 2f * ease)) : alive == 0;
            if ((spawnT -= dt) <= 0f && want)
            {
                spawnT = spawned < 5 ? 0.2f : Rnd(0.5f, 1f); int ed = Random.Range(0, 4);
                AddEnemy(ed < 2 ? Rnd(-HW + 30, HW - 30) : (ed == 2 ? -HW + 20 : HW - 20), ed < 2 ? (ed == 1 ? HH - 20 : -HH + 20) : Rnd(-HH + 30, HH - 30)); spawned++;
                if (spawned % 4 == 0) { En e = E[E.Count - 1]; e.hp = 2; e.h = 78; e.sp *= 0.85f; }
            }
            foreach (Shot o in shots)
            {
                o.x += o.vx * dt; o.y += o.vy * dt; o.l -= dt; if (Mathf.Abs(o.x) > HW + 60f || Mathf.Abs(o.y) > HH + 60f) o.l = 0f;
                if (o.l > 0f && (o.x - px) * (o.x - px) + (o.y - py) * (o.y - py) < 17f * 17f) { o.l = 0f; Hurt(); }
            }
            if (mode == Mode.Play)
            {
                foreach (En e in E)
                {
                    if (e.dead) continue; e.born += dt; e.ph += dt; if (e.flash > 0f) e.flash -= dt;
                    float dx = px - e.x, dy = py - e.y, d = Mathf.Max(1f, Mathf.Sqrt(dx * dx + dy * dy));
                    e.x += e.kx * dt; e.y += e.ky * dt; float damp = Mathf.Pow(0.002f, dt); e.kx *= damp; e.ky *= damp;
                    if (e.st == 0)
                    {
                        if (e.cast)
                        {   // throwers keep their distance, circle, and throw
                            float dir = d > 330f ? 1f : d < 210f ? -0.8f : 0f, sd = Mathf.Sin(e.ph * 0.7f) > 0f ? 1f : -1f;
                            e.x += (dx / d * dir - dy / d * 0.45f * sd) * e.sp * dt; e.y += (dy / d * dir + dx / d * 0.45f * sd) * e.sp * dt;
                            if (e.born > 1f && (e.t -= dt) <= 0f) { e.st = 1; e.t = 0.5f + 0.12f * ease; }
                        }
                        else
                        {
                            e.x += dx / d * e.sp * dt; e.y += dy / d * e.sp * dt;
                            if (d < 340f && d > 70f && (e.t -= dt) <= 0f)
                            {
                                e.st = 1; e.t = 0.3f + 0.1f * ease; float side = Random.value < 0.3f ? (Random.value < 0.5f ? 1f : -1f) * 0.7f : 0f;
                                float lx = dx / d - dy / d * side, ly = dy / d + dx / d * side, ll = Mathf.Sqrt(lx * lx + ly * ly); e.lx = lx / ll; e.ly = ly / ll;
                            }
                        }
                    }
                    else if (e.st == 1)
                    {
                        if ((e.t -= dt) <= 0f)
                        {
                            if (e.cast)
                            {
                                float a = Mathf.Atan2(dy, dx); int n = Random.value < 0.12f ? 1 : 0;
                                for (int k = -n; k <= n; k++)
                                {
                                    Shot s = new Shot(); s.x = e.x; s.y = e.y; s.vx = Mathf.Cos(a + k * 0.22f) * 270f * shotMul; s.vy = Mathf.Sin(a + k * 0.22f) * 270f * shotMul; s.l = 4f;
                                    s.sr = Pooled(sOrb, fightRoot, 0); s.blob = Pooled(sBlob, fightRoot, -19000); s.blob.transform.rotation = Quaternion.Euler(90f, 0f, 0f); s.blob.transform.localScale = new Vector3(0.18f, 0.12f, 1f); shots.Add(s);
                                }
                                Sfx("sfx_throw", 0.6f); e.st = 0; e.t = Rnd(1.6f, 2.6f);
                            }
                            else { e.st = 2; e.t = 0.24f; Sfx("sfx_lunge", 0.5f); }
                        }
                    }
                    else
                    {   // the dash
                        e.x += e.lx * 640f * speedMul * dt; e.y += e.ly * 640f * speedMul * dt;
                        if (dt > 0f && Random.value < 0.7f) AddPart(e.x, e.y, Rnd(10, e.h), 0, 0, 0, 0.25f, Rnd(5, 9), Hex("#3a1470"));
                        if ((e.t -= dt) <= 0f) { e.st = 0; e.t = Rnd(1.1f, 2.2f); }
                    }
                    e.x = Mathf.Clamp(e.x, -HW + 16f, HW - 16f); e.y = Mathf.Clamp(e.y, -HH + 16f, HH - 16f);
                    if (d < 27f) Hurt();
                }
                for (int i = 0; i < E.Count; i++)
                {
                    En a = E[i]; if (a.dead) continue;
                    for (int j = i + 1; j < E.Count; j++)
                    {
                        En b = E[j]; if (b.dead) continue; float dx = b.x - a.x, dy = b.y - a.y, d = Mathf.Sqrt(dx * dx + dy * dy);
                        if (d < 24f && d > 0f) { float p = (24f - d) / 2f / d; a.x -= dx * p; a.y -= dy * p; b.x += dx * p; b.y += dy * p; }
                    }
                }
            }
        }
        for (int i = E.Count - 1; i >= 0; i--) if (E[i].dead || mode == Mode.Won) { KillObjects(E[i]); E.RemoveAt(i); }
        for (int i = shots.Count - 1; i >= 0; i--) if (shots[i].l <= 0f || mode == Mode.Won) { Free(shots[i].sr); Free(shots[i].blob); shots.RemoveAt(i); }
        DrawFight(dt);
    }

    void DrawFight(float dt)
    {
        float t = tNow;
        camX += (px * 0.8f - camX) * Mathf.Min(1f, dt * 5f); camY += (py * 0.8f - 50f - camY) * Mathf.Min(1f, dt * 5f);
        cam.transform.position = W(camX, camY + DC, HC) + new Vector3(Rnd(-shake, shake), Rnd(-shake, shake), 0f) * U;
        Quaternion bill = cam.transform.rotation;
        foreach (Transform b in bills) if (b != null) b.rotation = bill;

        // hunter
        float fx = Mathf.Cos(pface) >= 0f ? 1f : -1f; bool mv = pwalk > 0f;
        pRoot.position = W(px, py, mv ? 0f : Mathf.Sin(t * 2.6f) * 1.1f + 1.1f);
        pRoot.rotation = bill * Quaternion.Euler(0f, 0f, -(pvx / 225f) * 5f);
        pBody.sprite = mv ? (Mathf.FloorToInt(pwalk / 1.6f) % 2 == 0 ? sHunterW1 : sHunterW2) : sHunterIdle; pBody.flipX = fx < 0f;
        int po = Ord(py); pBody.sortingOrder = po; pArm.sortingOrder = po + 1; pSword.sortingOrder = po + 2;
        float blink = pinv > 0f && Mathf.FloorToInt(t * 20f) % 2 == 1 ? 0.45f : 1f; Color pc = new Color(1f, 1f, 1f, blink); pBody.color = pc; pArm.color = pc; pSword.color = pc;
        float ga = pface + 0.9f; if (psw > 0f) { float k = 1f - psw / 0.15f; ga = pswA - pswH + k * 2f * pswH; }
        float bx = Mathf.Cos(ga), by = Mathf.Sin(ga) * K; Vector2 sh = new Vector2(fx * 12f, 43f) * 0.01f, hand = new Vector2(bx * 13f + fx * 3f, 29f - by * 10f) * 0.01f;
        Vector2 dv = hand - sh; pArm.transform.localPosition = sh; pArm.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dv.y, dv.x) * Mathf.Rad2Deg);
        pArm.transform.localScale = new Vector3(dv.magnitude / 0.24f, 1f, 1f);
        pSword.transform.localPosition = hand; pSword.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(-by, bx) * Mathf.Rad2Deg);
        pSword.transform.localScale = new Vector3((0.72f + 0.28f * Mathf.Sqrt(bx * bx + by * by)) * 1.12f, 1.35f, 1f);
        pBlob.transform.position = W(px, py, 0.6f); pGlow.transform.position = W(px, py, 0.4f);

        // shadows
        foreach (En e in E)
        {
            if (e.root == null) continue; float a = Mathf.Min(1f, e.born * 3f); bool gl = Random.value < 0.13f;
            e.root.position = W(e.x + (e.st == 1 ? Rnd(-2f, 2f) : 0f) + (gl ? Rnd(-6f, 6f) : 0f), e.y); e.root.rotation = bill;
            Vector3 sc = new Vector3(e.h > 70f ? 1.15f : 1f, e.h / 60f, 1f); int o = Ord(e.y);
            e.body.sprite = e.flash > 0f ? sShadowW : (Mathf.Sin(e.ph * 7f) > 0f ? sShadowA : sShadowB); e.body.transform.localScale = sc; e.body.color = new Color(1f, 1f, 1f, a); e.body.sortingOrder = o;
            e.ghost.enabled = gl; e.ghost.transform.localScale = sc; e.ghost.transform.localPosition = new Vector3(Rnd(-0.07f, 0.07f), 0f, 0f); e.ghost.sortingOrder = o - 1;
            e.eyes.enabled = e.flash <= 0f; e.eyes.transform.localPosition = new Vector3(0f, e.h * 0.85f * 0.01f, 0f); e.eyes.sortingOrder = o + 1;
            e.eyes.color = e.st == 1 ? new Color(1f, 0.82f, 0.82f, a) : e.cast ? new Color(1f, 0.48f, 0.82f, a) : new Color(1f, 0.16f, 0.16f, a);
            e.eyes.transform.localScale = e.st == 1 ? new Vector3(1.1f, 1.6f, 1f) : Vector3.one;
            e.blob.transform.position = W(e.x, e.y, 0.6f); e.blob.color = new Color(1f, 1f, 1f, a);
        }
        foreach (Shot s in shots) { s.sr.transform.position = W(s.x, s.y, 22f); s.sr.transform.rotation = bill; s.sr.sortingOrder = Ord(s.y); s.blob.transform.position = W(s.x, s.y, 0.6f); }

        // particles, slash arcs
        for (int i = parts.Count - 1; i >= 0; i--)
        {
            Part p = parts[i]; p.l -= dt; p.x += p.vx * dt; p.y += p.vy * dt; p.z += p.vz * dt; if (dt > 0f) { p.vx *= 0.96f; p.vy *= 0.96f; }
            if (p.l <= 0f) { Free(p.sr); parts.RemoveAt(i); continue; }
            p.sr.transform.position = W(p.x, p.y, p.z); p.sr.transform.rotation = bill; p.sr.sortingOrder = Ord(p.y) + 3;
            if (p.flash) { p.sr.transform.localScale = Vector3.one * (2f * p.s * U / 2.56f); p.sr.color = new Color(1f, 1f, 1f, Mathf.Clamp01(p.l / 0.2f) * 0.7f); }
            else { p.sr.transform.localScale = Vector3.one * (p.s * U); Color c = p.c; c.a *= Mathf.Min(1f, p.l * 2f); p.sr.color = c; }
        }
        for (int i = fxs.Count - 1; i >= 0; i--)
        {
            Fx f = fxs[i]; f.l -= dt; if (f.l <= 0f) { Free(f.sr); fxs.RemoveAt(i); continue; }
            f.sr.color = new Color(1f, 1f, 1f, f.a0 * f.l / f.max);
        }

        // torches and pyres flicker
        foreach (Prop p in props)
        {
            if (p.kind != 1 && p.kind != 2) continue; int fr = Mathf.FloorToInt(t * 9f + p.ph) % 4; bool pyre = p.kind == 2;
            p.sr.sprite = pyre ? Spr("pyre_" + fr, 128, 30, 200f) : Spr("torch_" + fr, 64, 12, 200f);
            float fl = 0.85f + 0.15f * Mathf.Sin(t * 11f + p.x); Color c = p.glowB.color; c.a = 0.4f * fl; p.glowB.color = c;
        }

        // the old man's body, and the open arch
        if (hasCorpse)
        {
            bool vis = corpseT >= 0.45f; corpse.gameObject.SetActive(vis);
            if (vis)
            {
                float k = Mathf.Clamp01((corpseT - 0.45f) / 0.55f), e = k * k * (3f - 2f * k);
                corpse.transform.position = W(corpseX, corpseY, 2f * e); corpse.transform.rotation = bill * Quaternion.Euler(0f, 0f, 90f * e);
                corpse.transform.localScale = Vector3.one * 1.22f; corpse.sortingOrder = Ord(corpseY) - 1; corpse.color = new Color(1f, 1f, 1f, Mathf.Clamp01((corpseT - 0.45f) * 3f));
            }
        }
        float ag = (mode == Mode.Won || mode == Mode.Leave) ? 0.5f + 0.12f * Mathf.Sin(t * 4f) : 0f; Color ac = archGlow.color; ac.a = ag; archGlow.color = ac;

        // the arrow above the gate: fades in once the way is open, then bobs gently up and down
        if (gateArrow != null && archSr != null)
        {
            bool show = (mode == Mode.Won && wonT > 1.5f) || mode == Mode.Leave;
            arrowA = Mathf.MoveTowards(arrowA, show ? 1f : 0f, Time.deltaTime * (show ? 2.5f : 4f));
            gateArrow.gameObject.SetActive(arrowA > 0.001f);
            if (arrowA > 0.001f)
            {
                float lift = (arrowHeight + arrowBobHeight + Mathf.Sin(t * arrowBobSpeed) * arrowBobHeight) * U;
                gateArrow.transform.rotation = bill * Quaternion.Euler(0f, 0f, -90f);         // the sprite points right; turn it to point down
                gateArrow.transform.position = archSr.transform.position + bill * Vector3.up * lift;
                gateArrow.transform.localScale = Vector3.one * 1.1f;
                gateArrow.color = new Color(1f, 1f, 1f, arrowA * (0.85f + 0.15f * Mathf.Sin(t * arrowBobSpeed * 2f)));
            }
        }
    }

    // ================================================================== PART 2: THE HALL (2D pixel side view)
    const float HU = 1f / 16f, FY = 158f, HWD = 1040f, B1 = 600f, B2 = 690f;
    static readonly float[][] CRYSTALS = { new float[]{70,35,3,0,3.17f,0},new float[]{127,90,5,0,5.81f,0},new float[]{173,58,5,1,5.01f,0},new float[]{220,16,7,0,4.08f,0},new float[]{263,27,5,0,5.78f,0},new float[]{323,66,5,0,1.48f,0},new float[]{379,48,3,0,2.08f,0},new float[]{416,93,7,0,2.40f,0},new float[]{459,48,3,0,5.44f,0},new float[]{511,70,3,0,4.46f,1},new float[]{560,92,5,0,1.32f,0},new float[]{618,28,3,0,5.35f,0},new float[]{677,46,7,0,0.75f,1},new float[]{713,70,5,0,4.62f,0},new float[]{749,68,3,0,1.97f,0},new float[]{805,37,7,1,0.11f,0},new float[]{861,38,5,0,1.90f,1},new float[]{914,27,3,1,1.91f,0} };   // x, chain length, size, colour (0 cyan, 1 pink), phase, far
    static readonly float[][] STANDS = { new float[]{150,1.26f},new float[]{271,3.95f},new float[]{453,1.64f},new float[]{630,0.45f},new float[]{808,0.17f},new float[]{931,1.20f} };       // x, phase
    static readonly string[] LINE_WHO = { "npc", "mc", "npc", "npc" };
    static readonly string[] LINE_TEXT = { "...You pressed it.", "You. I watched you fall.", "I couldn't stop you.", "I never can." };

    class Bit { public float x, y, d, vx, vy, l; public SpriteRenderer sr; }
    class Mark { public float x, y, vx, vy, l; public SpriteRenderer a, b; }
    static Vector3 HP(float x, float y, float z = 0f) { return new Vector3(x * HU, (200f - y) * HU, z); }

    float hx, hWalk, hT, hCam, hSt, hLc, hLw, hOut, npcX, npcWalk; int hDir = 1, hSc, hLi; bool hSaw, hasNpc;
    Transform hunter2D, hFarT; SpriteRenderer hBody, hArm, npcSr, btn2, darkSr, hGlow, npcGlow; Texture2D darkTex; Color32[] darkPx; float[] darkA;
    Sprite[] hFrames, standFrames, wardenFrames; Texture2D[] wardenTex;
    readonly List<SpriteRenderer> crystalSr = new List<SpriteRenderer>(), crystalGlow = new List<SpriteRenderer>(), standSr = new List<SpriteRenderer>(), standGlow = new List<SpriteRenderer>();
    readonly List<int> crystalIdx = new List<int>(); readonly List<Bit> bits = new List<Bit>(); readonly List<Mark> prints = new List<Mark>(), dust = new List<Mark>();
    const int DW = 256, DH = 100;

    void EnterHall()
    {
        if (fightRoot != null) { Destroy(fightRoot.gameObject); fightRoot = null; pool.Clear(); E.Clear(); shots.Clear(); parts.Clear(); fxs.Clear(); props.Clear(); bills.Clear(); }
        hallRoot = new GameObject("LightBlade_Hall").transform; hallRoot.SetParent(transform, false);
        cam.orthographic = true; cam.orthographicSize = 100f * HU; cam.transform.rotation = Quaternion.identity; cam.backgroundColor = Hex("#07050f");

        SpriteRenderer far = SR("Far", Spr("hall_far", 0, 0, 16f, true, 1040, 200), hallRoot, 0); hFarT = far.transform;
        SR("Main", Spr("hall_main", 0, 0, 16f, true, 1040, 200), hallRoot, 1);
        standFrames = new Sprite[] { Spr("stand_0", 8, 2, 16f, true), Spr("stand_1", 8, 2, 16f, true), Spr("stand_2", 8, 2, 16f, true) };
        Sprite glow = Spr("glow", 128, 128, 100f);
        for (int i = 0; i < STANDS.Length; i++)
        {
            SpriteRenderer s = SR("Stand", standFrames[0], hallRoot, 2); s.transform.position = HP(STANDS[i][0], FY); standSr.Add(s);
            SpriteRenderer g = SR("StandGlow", glow, hallRoot, 21); g.transform.position = HP(STANDS[i][0], FY - 40f); g.transform.localScale = Vector3.one * (52f * HU / 2.56f); g.color = new Color(0.59f, 0.31f, 1f, 0.4f); standGlow.Add(g);
        }
        for (int i = 0; i < CRYSTALS.Length; i++)
        {
            float[] k = CRYSTALS[i]; if (k[5] > 0f) continue; bool pink = k[3] > 0f; int sz = (int)k[2];
            SpriteRenderer s = SR("Crystal", Spr("crystal_" + (pink ? "p" : "c") + sz, 8, 8, 16f, true), hallRoot, 3); crystalSr.Add(s); crystalIdx.Add(i);
            SpriteRenderer g = SR("CrystalGlow", glow, hallRoot, 21); g.transform.position = HP(k[0], k[1]); g.transform.localScale = Vector3.one * ((16f + sz * 4.8f) * HU / 2.56f);
            g.color = pink ? new Color(1f, 0.59f, 0.86f, 0.36f) : new Color(0.35f, 0.86f, 1f, 0.4f); crystalGlow.Add(g);
        }
        SpriteRenderer b1 = SR("Button1", Spr("pedestal_down", 16, 0, 16f, true), hallRoot, 4); b1.transform.position = HP(B1, FY);
        btn2 = SR("Button2", Spr("pedestal_up", 16, 0, 16f, true), hallRoot, 4); btn2.transform.position = HP(B2, FY);

        hFrames = new Sprite[] { Spr("px_hunter_idle", 16, 0, 16f, true), Spr("px_hunter_w1", 16, 0, 16f, true), Spr("px_hunter_w2", 16, 0, 16f, true) };
        // the other hunter, already pressing the first button
        Transform other = new GameObject("OtherHunter").transform; other.SetParent(hallRoot, false); other.position = HP(B1 - 17f, FY);
        SR("Body", hFrames[0], other, 5); SpriteRenderer oa = SR("Arm", Spr("px_arm_bare", 1, 8, 16f, true), other, 6);
        oa.transform.localPosition = new Vector3(4f * HU, 17f * HU, 0f); oa.transform.localRotation = Quaternion.Euler(0f, 0f, 0.5f * Mathf.Rad2Deg);
        // our hunter
        hunter2D = new GameObject("Hunter2D").transform; hunter2D.SetParent(hallRoot, false);
        hBody = SR("Body", hFrames[0], hunter2D, 8); hArm = SR("Arm", Spr("px_arm_sword", 1, 8, 16f, true), hunter2D, 9); hArm.transform.localPosition = new Vector3(4f * HU, 17f * HU, 0f);
        hGlow = SR("SwordGlow", glow, hallRoot, 21); hGlow.transform.localScale = Vector3.one * (44f * HU / 2.56f); hGlow.color = new Color(0.86f, 0.82f, 1f, 0.28f);

        BuildWarden();
        npcSr = SR("OldMan2D", wardenFrames[0], hallRoot, 7); npcSr.color = new Color(1f, 1f, 1f, 0.9f); npcSr.gameObject.SetActive(false);

        darkTex = new Texture2D(DW, DH, TextureFormat.RGBA32, false); darkTex.filterMode = FilterMode.Bilinear; darkTex.wrapMode = TextureWrapMode.Clamp;
        darkPx = new Color32[DW * DH]; darkA = new float[DW * DH];
        darkSr = SR("Darkness", Sprite.Create(darkTex, new Rect(0, 0, DW, DH), new Vector2(0.5f, 0.5f), 8f), hallRoot, 20);

        hx = 30f; hDir = 1; hWalk = 0; hT = 0; hCam = 0; hSc = 0; hSt = 0; hLi = 0; hLc = 0; hLw = 0; hOut = 0; hSaw = false; hasNpc = false;
        musicFadeFrom = music != null ? music.volume : -1f; musicFadeT = 0f; musicT = -1f;
        mode = Mode.Hall; fadeBlack = 1f; Say("A hall. The lights are still burning for someone.", 5f);
    }

    // the old man as pixels, built in code so the dissolve can read every pixel
    void BuildWarden()
    {
        wardenTex = new Texture2D[2]; wardenFrames = new Sprite[2];
        for (int f = 0; f < 2; f++)
        {
            Texture2D t = new Texture2D(32, 32, TextureFormat.RGBA32, false); t.filterMode = FilterMode.Point; Color32[] p = new Color32[32 * 32];
            int a = f == 1 ? 1 : 0, b = f == 1 ? 0 : 1;
            Q(p, "#17120c", -9, -21, 5, 11); Q(p, "#8a7a5c", -8, -20, 3, 9); Q(p, "#55664a", -9, -22, 5, 2);
            Q(p, "#17120c", -5, -4, 5, 4); Q(p, "#17120c", 1, -4, 5, 4); Q(p, "#cdb083", -4, -3 - a, 3, 3); Q(p, "#cdb083", 2, -3 - b, 3, 3); Q(p, "#77756f", -4, -9, 3, 6 - a); Q(p, "#77756f", 2, -9, 3, 6 - b);
            Q(p, "#17120c", -6, -22, 13, 14); Q(p, "#7f9cba", -5, -21, 11, 12); Q(p, "#5d7896", -5, -21, 3, 12); Q(p, "#55704f", -1, -21, 3, 12); Q(p, "#55704f", -5, -11, 11, 2); Q(p, "#6b5a48", -5, -14, 11, 2); Q(p, "#d9d9d9", -1, -14, 2, 2);
            Q(p, "#4a3a2c", -4, -20, 2, 2); Q(p, "#4a3a2c", -2, -18, 2, 2); Q(p, "#4a3a2c", 2, -16, 2, 2);
            Q(p, "#17120c", 5, -20, 4, 9); Q(p, "#7f9cba", 6, -19, 2, 6); Q(p, "#eef1f6", 6, -13, 2, 2); Q(p, "#17120c", 5, -11, 4, 5); Q(p, "#ffffff", 6, -10, 2, 3);
            Q(p, "#17120c", -5, -31, 10, 10); Q(p, "#d5dae2", -4, -30, 8, 8); Q(p, "#f6f8fb", -4, -31, 8, 3); Q(p, "#f6f8fb", -3, -25, 7, 5); Q(p, "#f6f8fb", -1, -20, 4, 3); Q(p, "#b0473f", -5, -22, 4, 2); Q(p, "#b0473f", 3, -21, 2, 5);
            Q(p, "#ffffff", 0, -27, 2, 1); Q(p, "#ffffff", 3, -27, 2, 1);
            t.SetPixels32(p); t.Apply(); wardenTex[f] = t; wardenFrames[f] = Sprite.Create(t, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0f), 16f);
        }
    }
    static void Q(Color32[] p, string hex, int a, int b, int w, int h)
    {
        Color32 c = Hex(hex);
        for (int yy = b; yy < b + h; yy++) for (int xx = a; xx < a + w; xx++)
        {
            int tx = 16 + xx, ty = -yy - 1; if (tx < 0 || tx > 31 || ty < 0 || ty > 31) continue; p[ty * 32 + tx] = c;
        }
    }

    Mark NewMark(float x, float y, Color ca, Color cb, bool two)
    {
        Mark m = new Mark(); m.x = x; m.y = y; m.a = Pooled(sqSprite, hallRoot, 6); m.a.color = ca; if (two) { m.b = Pooled(sqSprite, hallRoot, 6); m.b.color = cb; } return m;
    }

    void HallAct()
    {
        if (hSc == 0 && Mathf.Abs(hx - (B2 - 17f)) < 14f) { hSc = 1; hSt = 0; hDir = 1; Sfx("sfx_button", 0.9f); btn2.sprite = Spr("pedestal_down", 16, 0, 16f, true); }
        else if (hSc == 3) { if (hLc < LINE_TEXT[hLi].Length) hLc = LINE_TEXT[hLi].Length; else hLw = 9f; }
    }

    void UpdateHall(float dt)
    {
        hT += dt; if (fadeBlack > 0f && hOut <= 0f) fadeBlack = Mathf.Max(0f, fadeBlack - dt * 1.3f);
        if (KeyAct() || MouseDown(0)) HallAct();
        if (hx > B1 - 90f && !hSaw) { hSaw = true; Say("...Who is that?", 4f); }

        // the scripted ending
        if (hSc > 0) hSt += dt;
        if (hSc == 1 && hSt > 1.1f) { hSc = 2; hDir = -1; hasNpc = true; npcX = hCam - 16f; npcWalk = 0; npcSr.gameObject.SetActive(true); }
        else if (hSc == 2) { float tx = hx - 40f; if (npcX < tx) { npcX += 46f * dt; npcWalk += dt * 7f; } else { npcWalk = 0; hSc = 3; hLi = 0; hLc = 0; hLw = 0; } }
        else if (hSc == 3)
        {
            string L = LINE_TEXT[hLi]; int b4 = Mathf.FloorToInt(hLc);
            if (hLc < L.Length) { hLc += dt * 26f; int nw = Mathf.Min(L.Length, Mathf.FloorToInt(hLc)); if (nw > b4 && L[nw - 1] != ' ') Sfx(LINE_WHO[hLi] == "npc" ? "blip_npc" : "blip_mc", 0.6f); }
            else
            {
                hLw += dt;
                if (hLw > 1.7f) { hLi++; hLc = 0; hLw = 0; if (hLi >= LINE_TEXT.Length) StartDissolve(); }
            }
        }
        else if (hSc == 4)
        {
            int alive = 0;
            foreach (Bit b in bits)
            {
                if (hSt > b.d) { b.x += b.vx * dt; b.y += b.vy * dt; b.l -= dt; }
                if (b.l > 0f) { alive++; b.sr.transform.position = HP(Mathf.Round(b.x) + 0.5f, Mathf.Round(b.y) + 0.5f); Color c = b.sr.color; c.a = Mathf.Min(1f, b.l); b.sr.color = c; }
                else if (b.sr.gameObject.activeSelf) b.sr.gameObject.SetActive(false);
            }
            if (alive == 0 || hSt > 4f) { hSc = 5; hSt = 0; }
        }
        else if (hSc == 5) { hOut = Mathf.Min(1f, hSt / 1.4f); fadeBlack = hOut; if (hSt > 1.6f) { mode = Mode.End; Finish(); return; } }

        // walking, footsteps
        float mv = hSc != 0 ? 0f : (Held(1) ? 1 : 0) - (Held(0) ? 1 : 0);
        if (mv != 0f)
        {
            hDir = mv > 0f ? 1 : -1; hx += mv * 74f * dt; int b4 = Mathf.FloorToInt(hWalk); hWalk += dt * 8f; int nw = Mathf.FloorToInt(hWalk);
            if (nw != b4 && nw % 2 == 1)
            {
                float fx2 = hx + mv * (nw % 4 == 1 ? 5f : -3f);
                Mark pr = NewMark(fx2, FY + 2f, Hex("#8f88a8"), Hex("#04020a"), true); pr.l = 6f; prints.Add(pr);
                for (int i = 0; i < 4; i++) { Mark d = NewMark(fx2 - mv * 2f, FY - 1f, Hex("#b9b2d6"), Color.clear, false); d.vx = -mv * Rnd(6f, 20f); d.vy = -Rnd(4f, 16f); d.l = 0.45f; dust.Add(d); }
                Sfx("sfx_step", 0.6f, 0.08f);
            }
        }
        else hWalk = 0f;
        hx = Mathf.Clamp(hx, 22f, hSc != 0 ? hx : B2 - 15f);
        float view = 100f * cam.aspect * 2f; // visible width in hall pixels
        hCam += (Mathf.Clamp(hx - view / 2f + hDir * 24f, 0f, Mathf.Max(0f, HWD - view)) - hCam) * Mathf.Min(1f, dt * 4f);
        float c0 = Mathf.Round(hCam);
        cam.transform.position = new Vector3((c0 + view / 2f) * HU, 100f * HU, -10f);
        hFarT.position = new Vector3(c0 * 0.5f * HU, 0f, 0f);

        // hunter sprite
        int fi = 0; float bob = 0f;
        if (hWalk > 0f) { int q = Mathf.FloorToInt(hWalk) % 4; fi = q == 0 ? 1 : q == 2 ? 2 : 0; if (fi == 0) bob = 1f; }
        hunter2D.position = HP(Mathf.Round(hx), FY - bob, 0f); hunter2D.localScale = new Vector3(hDir, 1f, 1f); hBody.sprite = hFrames[fi];
        float armA = hSc == 1 ? -0.5f : -0.32f + (hWalk > 0f ? Mathf.Sin(hWalk * 1.57f) * 0.05f : 0f);
        hArm.transform.localRotation = Quaternion.Euler(0f, 0f, -armA * Mathf.Rad2Deg);
        hGlow.transform.position = HP(hx + hDir * 26f, FY - 27f);
        if (hasNpc && hSc < 4) { npcSr.transform.position = HP(Mathf.Round(npcX), FY); npcSr.sprite = wardenFrames[npcWalk > 0f ? Mathf.FloorToInt(npcWalk) % 2 : 0]; }

        // flames, crystals, prints, dust
        for (int i = 0; i < standSr.Count; i++) standSr[i].sprite = standFrames[Mathf.FloorToInt(hT * 8f + STANDS[i][1]) % 3];
        for (int i = 0; i < crystalSr.Count; i++) { float[] k = CRYSTALS[crystalIdx[i]]; crystalSr[i].transform.position = HP(k[0] + 0.5f, k[1] + Mathf.Round(Mathf.Sin(hT * 1.3f + k[4]) * 1.5f) + 0.5f); }
        for (int i = prints.Count - 1; i >= 0; i--)
        {
            Mark p = prints[i]; p.l -= dt; if (p.l <= 0f) { Free(p.a); Free(p.b); prints.RemoveAt(i); continue; }
            float al = Mathf.Min(0.6f, p.l / 4f); p.a.transform.position = HP(Mathf.Round(p.x), p.y + 0.5f); p.a.transform.localScale = new Vector3(4f * HU, HU, 1f); Color ca = p.a.color; ca.a = al; p.a.color = ca;
            p.b.transform.position = HP(Mathf.Round(p.x) - 0.5f, p.y + 1.5f); p.b.transform.localScale = new Vector3(3f * HU, HU, 1f); Color cb = p.b.color; cb.a = al; p.b.color = cb;
        }
        for (int i = dust.Count - 1; i >= 0; i--)
        {
            Mark p = dust[i]; p.l -= dt; p.x += p.vx * dt; p.y += p.vy * dt; p.vy += 30f * dt; if (p.l <= 0f) { Free(p.a); dust.RemoveAt(i); continue; }
            p.a.transform.position = HP(Mathf.Round(p.x) + 1f, Mathf.Round(p.y) + 1f); p.a.transform.localScale = new Vector3(2f * HU, 2f * HU, 1f); Color c = p.a.color; c.a = Mathf.Min(1f, p.l * 2.4f) * 0.7f; p.a.color = c; p.a.sortingOrder = 10;
        }
        UpdateDarkness(c0, view);
    }

    void StartDissolve()
    {
        hSc = 4; hSt = 0; npcSr.gameObject.SetActive(false); Color32[] px32 = wardenTex[0].GetPixels32();
        for (int ty = 0; ty < 32; ty++) for (int tx = 0; tx < 32; tx++)
        {
            Color32 c = px32[ty * 32 + tx]; if (c.a < 40) continue;
            Bit b = new Bit(); b.x = npcX - 16f + tx; b.y = FY - 1f - ty; b.d = (31 - tx) * 0.045f + (31 - ty) * 0.012f + Rnd(0f, 0.35f); b.vx = Rnd(8f, 30f); b.vy = -Rnd(4f, 20f); b.l = Rnd(1.1f, 1.7f);
            b.sr = SR("bit", sqSprite, hallRoot, 7); b.sr.color = new Color32(c.r, c.g, c.b, 230); b.sr.transform.localScale = new Vector3(HU, HU, 1f); b.sr.transform.position = HP(b.x + 0.5f, b.y + 0.5f); bits.Add(b);
        }
        Sfx("sfx_dissolve", 0.8f);
    }

    // darkness with soft holes of light, drawn into a small texture each frame
    void Hole(float wx, float wy, float r, float s, float left)
    {
        float cx = (wx - left) / 2f, cy = (200f - wy) / 2f, rr = r / 2f; int x0 = Mathf.Max(0, (int)(cx - rr)), x1 = Mathf.Min(DW - 1, (int)(cx + rr) + 1), y0 = Mathf.Max(0, (int)(cy - rr)), y1 = Mathf.Min(DH - 1, (int)(cy + rr) + 1);
        for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++)
        {
            float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) / rr; if (d < 1f) darkA[y * DW + x] *= 1f - s * (1f - d);
        }
    }
    void UpdateDarkness(float c0, float view)
    {
        float centre = c0 + view / 2f, left = centre - DW; for (int i = 0; i < darkA.Length; i++) darkA[i] = 0.66f;
        Hole(hx + hDir * 14f, FY - 22f, 66f, 0.95f, left); Hole(29f, FY - 40f, 70f, 0.9f, left);
        for (int i = 0; i < STANDS.Length; i++) Hole(STANDS[i][0], FY - 40f, 38f + Mathf.Sin(hT * 9f + STANDS[i][1]) * 2f, 0.85f, left);
        for (int i = 0; i < CRYSTALS.Length; i++) if (CRYSTALS[i][5] <= 0f) Hole(CRYSTALS[i][0], CRYSTALS[i][1], 12f + CRYSTALS[i][2] * 3f, 0.8f, left);
        Hole(B1, FY - 26f, 26f, 0.7f, left); Hole(B2, FY - 26f, 26f, 0.7f, left); if (hasNpc && hSc < 5) Hole(npcX, FY - 16f, 34f, 0.85f, left);
        for (int i = 0; i < darkPx.Length; i++) darkPx[i] = new Color32(2, 1, 8, (byte)(darkA[i] * 255f));
        darkTex.SetPixels32(darkPx); darkTex.Apply(false);
        darkSr.transform.position = new Vector3(centre * HU, 100f * HU, 0f);
    }

    // ================================================================== HUD, captions, dialogue
    void OnGUI()
    {
        if (mode == Mode.Idle || cam == null) return;
        if (capStyle == null)
        {
            capStyle = new GUIStyle(GUI.skin.label); capStyle.alignment = TextAnchor.MiddleCenter; capStyle.fontSize = 20; capStyle.normal.textColor = Hex("#f1eef8"); capStyle.wordWrap = false;
            bigStyle = new GUIStyle(capStyle); bigStyle.fontSize = 34; smallStyle = new GUIStyle(capStyle); smallStyle.fontSize = 14; smallStyle.alignment = TextAnchor.UpperLeft; smallStyle.normal.textColor = Hex("#7d7892");
            dlgStyle = new GUIStyle(capStyle); dlgStyle.fontSize = 28; dlgStyle.alignment = TextAnchor.UpperLeft;
            if (font != null) { capStyle.font = font; bigStyle.font = font; smallStyle.font = font; dlgStyle.font = font; }
            ctrlStyle = new GUIStyle(capStyle); ctrlStyle.fontSize = 14; ctrlStyle.normal.textColor = new Color(0.95f, 0.93f, 0.97f, 0.7f);
        }
        float k = Screen.height / 600f, offX = (Screen.width - 960f * k) / 2f; Matrix4x4 old = GUI.matrix;
        bool fight = mode == Mode.Play || mode == Mode.Won || mode == Mode.Leave || mode == Mode.Dead;
        if (fight) { GUI.color = Color.white; GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Spr("vignette", 256, 256, 100f).texture, ScaleMode.StretchToFill); }
        GUI.matrix = Matrix4x4.TRS(new Vector3(offX, 0f, 0f), Quaternion.identity, new Vector3(k, k, 1f));
        if (fight)
        {
            Texture hOn = Spr("heart", 16, 16, 100f).texture, hOff = Spr("heart_empty", 16, 16, 100f).texture;
            for (int i = 0; i < maxHp; i++) GUI.DrawTexture(new Rect(18 + i * 26, 18, 32, 32), i < php ? hOn : hOff);
        }
        // controls reminder, top centre; hidden on the death screen and while the hall dialogue runs
        if (mode == Mode.Play || mode == Mode.Won || mode == Mode.Leave)
            GUI.Label(new Rect(120, 22, 840, 24), "[WASD] MOVE     [LMB / SPACE] SLASH     [RMB / SHIFT] DASH", ctrlStyle);
        else if (mode == Mode.Hall && hSc == 0)
            GUI.Label(new Rect(120, 22, 840, 24), "[A D] WALK     [E] PRESS THE BUTTON", ctrlStyle);
        if (mode == Mode.Hall && hSc == 0 && Mathf.Abs(hx - (B2 - 17f)) < 14f)
        {
            Vector3 s0 = cam.WorldToScreenPoint(HP(B2, FY - 46f + Mathf.Round(Mathf.Sin(tNow * 5f)))); Rect r = new Rect((s0.x - offX) / k - 15f, (Screen.height - s0.y) / k - 15f, 30f, 30f);
            GUI.color = Hex("#07040d"); GUI.DrawTexture(new Rect(r.x - 3, r.y - 3, r.width + 6, r.height + 6), whiteTex); GUI.color = Hex("#f1eef8"); GUI.DrawTexture(r, whiteTex);
            GUI.color = Color.white; GUIStyle e = new GUIStyle(capStyle); e.normal.textColor = Hex("#07040d"); e.fontSize = 22; GUI.Label(r, "E", e);
        }
        GUI.color = Color.white;
        if (capT > 0f && cap.Length > 0 && !(mode == Mode.Hall && hSc == 3))
        {
            float w = capStyle.CalcSize(new GUIContent(cap)).x + 36f; Rect r = new Rect(480f - w / 2f, 554f, w, 36f);
            GUI.color = new Color(0.02f, 0.012f, 0.04f, 0.85f); GUI.DrawTexture(r, whiteTex); GUI.color = Color.white; GUI.Label(r, cap, capStyle);
        }
        if (mode == Mode.Hall && hSc == 3)
        {   // the dialogue box, typed out letter by letter
            GUI.color = Color.white; GUI.DrawTexture(new Rect(150, 36, 660, 124), whiteTex); GUI.color = Color.black; GUI.DrawTexture(new Rect(155, 41, 650, 114), whiteTex);
            bool npc = LINE_WHO[hLi] == "npc"; string L = LINE_TEXT[hLi]; dlgStyle.normal.textColor = npc ? Hex("#cfe2ff") : Color.white; GUI.color = Color.white;
            GUI.Label(new Rect(184, 62, 600, 60), "* " + L.Substring(0, Mathf.Min(L.Length, Mathf.FloorToInt(hLc))), dlgStyle);
            GUI.Label(new Rect(184, 126, 400, 24), npc ? "THE OLD MAN" : "THE HUNTER", smallStyle);
        }
        if (mode == Mode.Dead)
        {
            GUI.color = new Color(0.01f, 0f, 0.03f, 0.72f); GUI.matrix = old; GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), whiteTex);
            GUI.matrix = Matrix4x4.TRS(new Vector3(offX, 0f, 0f), Quaternion.identity, new Vector3(k, k, 1f)); GUI.color = Color.white;
            GUI.Label(new Rect(0, 240, 960, 50), "THE DARK TAKES HIM", bigStyle); GUI.Label(new Rect(0, 296, 960, 30), "press SPACE or click to try again", capStyle);
            if (easierAfterDeaths && deaths >= deathsBeforeEasier && ease < maxEaseSteps)
            {
                GUIStyle soft = new GUIStyle(capStyle); soft.fontSize = 16; soft.normal.textColor = Hex("#a9a3c4");
                GUI.Label(new Rect(0, 332, 960, 26), "the light will hold a little longer", soft);
            }
        }
        GUI.matrix = old;
        if (fadeBlack > 0f) { GUI.color = new Color(0f, 0f, 0f, fadeBlack); GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), blackTex); GUI.color = Color.white; }
    }
}
