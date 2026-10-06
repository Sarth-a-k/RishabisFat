using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Casa del Silencio: Furnace Heart.
// Nine skeletons wake around a cold furnace. Each one you cut down sends an ember into it. When all nine
// are in, the furnace stands up. Only its heart can be cut, and the heart (and a third censer) can only be
// seen in thermal sight. When it falls it leaves a pocket watch; picking it up ends the sequence.
//
// Put this on an empty GameObject. Everything is built at runtime from Resources/FurnaceHeart.
// It makes its own camera and removes it again when the sequence ends.
public class FurnaceHeart : MonoBehaviour
{
    [Header("Files")]
    public string folder = "FurnaceHeart";
    [Header("Fight")]
    public int hitPoints = 8;
    [Header("Sound")]
    [Range(0f, 1f)] public float musicVolume = 0.5f;
    [Range(0f, 1f)] public float sfxVolume = 0.7f;
    [Header("Flow")]
    public bool playOnStart = true;
    public bool skipSkeletons = false;      // start at the furnace (for testing)
    public float endHold = 4f;              // seconds after the watch is picked up
    public string nextSceneName = "";
    public UnityEvent onFinished;
    [Header("Easy mode after repeated deaths")]
    public bool easierAfterDeaths = true;
    public int deathsBeforeEasy = 2;
    public int easyExtraHearts = 4;
    public float easyInvulnerable = 2.2f;     // seconds of safety after a hit in easy mode (normal: 1)
    public bool easyHalfDamage = true;        // easy mode: every other hit only grazes (no heart lost)
    public int easyCutDamage = 2;             // easy mode: embers knocked out of the heart per cut (normal: 1)
    public float easyExposeLonger = 1.6f;     // easy mode: the heart stays out this many times longer
    bool grazeNext;

    static int deaths;                        // kept between tries until the fight is won
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetDeaths() { deaths = 0; }
    bool Easy => easierAfterDeaths && deaths >= deathsBeforeEasy;
    int MaxHp => hitPoints + (Easy ? Mathf.Max(0, easyExtraHearts) : 0);

    enum Mode { Idle, Play, Won, Dead, End }
    Mode mode = Mode.Idle;
    enum BS { Idle, Heat, Drop, Charge, Beam, Shake, Shedding, Exposed, Return, SweepT, Sweep, LanesT, Lanes, VolleyT }

    const float U = 0.01f, HW = 520f, HH = 400f, K = 0.6f, CL = 1100f, PITCH = 36f, BX = 0f, BY = -250f, SWR = 285f, GS = 0.78f;
    static readonly float ST = Mathf.Sin(PITCH * Mathf.Deg2Rad), CT = Mathf.Cos(PITCH * Mathf.Deg2Rad);
    static readonly float CF = CL * 0.95f, HC = CL * ST, DC = CL * CT;
    static readonly float[,] PILLARS = { { -300, 80 }, { 300, 80 }, { 0, 210 } };
    const float PR = 34f;
    static readonly int[][] PAT = { new int[] { 0, 6, 2, 4, 0, 3 }, new int[] { 1, 5, 2, 4, 3, 6, 0 }, new int[] { 1, 5, 4, 2, 6, 1, 3 } }; // 0 slam 1 double 2 breath 3 coals 4 sweep 5 lanes 6 volley

    GameObject camObj; Camera cam; Transform root;
    AudioSource music, sfx; readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
    readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
    Sprite sqSprite; Texture2D blackTex, whiteTex; Font font, bigFont; GUIStyle capStyle, bigStyle, wordStyle;
    readonly Stack<SpriteRenderer> pool = new Stack<SpriteRenderer>();
    float tNow, fadeBlack; string cap = ""; float capT; string banner = "", bannerSub = ""; float bannerT;

    // ------------------------------------------------------------------ helpers
    static Color Hex(string h, float a = 1f) { Color c; ColorUtility.TryParseHtmlString(h, out c); c.a = a; return c; }
    static float Rnd(float a, float b) { return Random.Range(a, b); }
    static Vector3 W(float x, float y, float h = 0f) { return new Vector3(x * U, h * U, -y * U); }
    static int Ord(float y) { return Mathf.Clamp(Mathf.RoundToInt(y * 10f), -12000, 12000); }

    Sprite Spr(string name, float pivotX, float pivotY, float ppu, bool point = false, float rectW = 0f, float rectH = 0f)
    {
        Sprite s; if (sprites.TryGetValue(name, out s)) return s;
        Texture2D t = Resources.Load<Texture2D>(folder + "/" + name);
        if (t == null) { Debug.LogError("FurnaceHeart: missing Resources/" + folder + "/" + name + ".png"); t = Texture2D.whiteTexture; }
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


    static float Dist(float a, float b, float c, float d) { return Mathf.Sqrt((a - c) * (a - c) + (b - d) * (b - d)); }
    static Color TC(float k, float a)
    {
        k = Mathf.Clamp01(k); float[,] s = { { 0f, 14, 16, 90 }, { 0.3f, 150, 28, 150 }, { 0.55f, 232, 70, 40 }, { 0.8f, 255, 180, 44 }, { 1f, 255, 255, 232 } };
        int i = 1; while (i < 4 && k > s[i, 0]) i++; float u = (k - s[i - 1, 0]) / (s[i, 0] - s[i - 1, 0]);
        return new Color(Mathf.Lerp(s[i - 1, 1], s[i, 1], u) / 255f, Mathf.Lerp(s[i - 1, 2], s[i, 2], u) / 255f, Mathf.Lerp(s[i - 1, 3], s[i, 3], u) / 255f, a);
    }
    bool KeyThermal()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard k = Keyboard.current; return k != null && (k.qKey.wasPressedThisFrame || k.eKey.wasPressedThisFrame);
#else
        return Input.GetKeyDown(KeyCode.Q) || Input.GetKeyDown(KeyCode.E);
#endif
    }
    bool KeyRestart()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard k = Keyboard.current; return k != null && k.rKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.R);
#endif
    }

    // ------------------------------------------------------------------ life cycle
    void Start() { if (playOnStart) Begin(); }

    public void Begin()
    {
        if (camObj != null) return;
        camObj = new GameObject("FurnaceHeart_Camera"); camObj.transform.SetParent(transform, false);
        cam = camObj.AddComponent<Camera>(); cam.depth = 100; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Hex("#080304");
        cam.nearClipPlane = 0.1f; cam.farClipPlane = 200f; cam.orthographic = false; cam.fieldOfView = 2f * Mathf.Atan(300f / CF) * Mathf.Rad2Deg;
        cam.transform.rotation = Quaternion.Euler(PITCH - Mathf.Atan(50f / CF) * Mathf.Rad2Deg, 0f, 0f);
        if (FindObjectOfType<AudioListener>() == null) camObj.AddComponent<AudioListener>();
        music = gameObject.AddComponent<AudioSource>(); music.loop = true; music.playOnAwake = false; music.volume = 0f; music.clip = Resources.Load<AudioClip>(folder + "/bgm");
        sfx = gameObject.AddComponent<AudioSource>(); sfx.playOnAwake = false;
        font = Resources.Load<Font>(folder + "/DotGothic16"); bigFont = Resources.Load<Font>(folder + "/Cinzel-Bold");
        blackTex = new Texture2D(1, 1); blackTex.SetPixel(0, 0, Color.black); blackTex.Apply();
        whiteTex = new Texture2D(4, 4); Color[] wp = new Color[16]; for (int i = 0; i < 16; i++) wp[i] = Color.white; whiteTex.SetPixels(wp); whiteTex.Apply(); whiteTex.filterMode = FilterMode.Point;
        sqSprite = Sprite.Create(whiteTex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
        lastMouse = MousePos();
        Build(); StartFight();
    }

    float musicTarget, musicRate = 1f;
    void Update()
    {
        if (mode == Mode.Idle || cam == null) return;
        float dt = Mathf.Min(0.05f, Time.deltaTime); tNow += dt;
        if (capT > 0f && capT < 90f) capT -= dt; if (bannerT > 0f) bannerT -= dt;
        Vector2 mp = MousePos(); if ((mp - lastMouse).sqrMagnitude > 4f) { useMouse = true; lastMouse = mp; }
        if (music != null && music.clip != null) music.volume = Mathf.MoveTowards(music.volume, musicTarget, dt * musicRate);
        if (KeyRestart() && mode != Mode.End) { StartFight(); return; }
        if (mode == Mode.Dead) { if (KeyAttack() || KeyAct() || MouseDown(0)) StartFight(); else Draw(dt); return; }
        if (mode == Mode.End) { endT += dt; fadeBlack = Mathf.Clamp01((endT - (endHold - 1.2f)) / 1.2f); Draw(dt); if (endT >= endHold) Finish(); return; }
        Tick(dt);
    }
    void Finish()
    {
        deaths = 0;                           // won: the next play-through starts at normal difficulty
        mode = Mode.Idle; if (root != null) Destroy(root.gameObject); if (camObj != null) Destroy(camObj); if (music != null) music.Stop();
        if (onFinished != null) onFinished.Invoke(); if (!string.IsNullOrEmpty(nextSceneName)) SceneManager.LoadScene(nextSceneName);
    }

    // ================================================================== data
    class Fist { public float x, y, up, r = 42f, tx, ty, heat, t, T; public int st; public Transform tr; public SpriteRenderer sr, blob, mark; public SpriteRenderer[] links; }
    class Min { public float x, y, sx, sy, tx, ty, t, T, cd, seed, z; public int state, face = 1; public bool dead; public SpriteRenderer sr, blob; }
    class Ember { public float x, y, wob, h; public bool done; public SpriteRenderer sr; }
    class Ring { public float x, y, r, max; public bool hit; public SpriteRenderer sr; }
    class Rock { public float x, y, t, T; public bool done; public SpriteRenderer mark, inner; }
    class Rubble { public float x, y, life; public SpriteRenderer sr; }
    class Ball { public float x, y, vx, vy, l; public SpriteRenderer sr, blob; }
    class Part { public float x, y, z, vx, vy, vz, l, s, g; public bool flash; public Color c; public SpriteRenderer sr; }
    class Fx { public float l, max, a0; public SpriteRenderer sr; }
    class Word { public float x, y, z, l; public int size; public string text; public Color c; }
    class Prop { public float x, y, ph; public int kind; public SpriteRenderer sr, glowA, glowB; }
    class Sleeper { public float x, y; public bool awake; public SpriteRenderer sr; }

    readonly List<Min> minions = new List<Min>(); readonly List<Ember> embers = new List<Ember>(); readonly List<Ring> rings = new List<Ring>(); readonly List<Rock> rocks = new List<Rock>();
    readonly List<Rubble> rubble = new List<Rubble>(); readonly List<Ball> balls = new List<Ball>(); readonly List<Part> parts = new List<Part>(); readonly List<Fx> fxs = new List<Fx>();
    readonly List<Word> words = new List<Word>(); readonly List<Prop> props = new List<Prop>(); readonly List<Transform> bills = new List<Transform>(); readonly List<SpriteRenderer> piles = new List<SpriteRenderer>();
    readonly Sleeper[] sleepers = new Sleeper[9]; readonly int[] order = new int[9]; readonly float[] pillarGlow = new float[3]; readonly SpriteRenderer[] pillarGlowSr = new SpriteRenderer[3];
    readonly Fist[] fist = new Fist[3];   // 0 left, 1 right, 2 the third (thermal only)
    // player
    float px, py, pvx, pvy, pkx, pky, pface, pinv, pcd, psw, pswA, pswH = 1f, pcomboT, pdash, pdashCd, pdashA, pdashT, pdx, pdy = -1f, pwalk; int php, pcombo;
    Transform pRoot; SpriteRenderer pBody, pArm, pSword, pBlob, pGlow; Sprite sHunterIdle, sHunterW1, sHunterW2;
    // flow
    int stage; float T, stageT, stageTime, nextWake, camX, camY = 30f, shake, stopT, endT; int woken, killed, irUses; bool ir, got; float irFlip;
    // boss
    int bhp; BS bstate; float bt, bT = 1f, beamA, beamEnd, a0, a1, bshake, bdead, flinch, lit, rise, hx, hy, hh, wander; readonly float[] heat = new float[3];
    int side, phase = 1, patI, heartAt = 2, heartPick = -1; readonly List<int> queue = new List<int>(); readonly List<float> lanes = new List<float>(); bool hasWatch; float watchX, watchY;
    Transform bRoot; SpriteRenderer bDark, bLit, bHot, bGlowFloor, bGlowTop, watchSr; Sprite[] litFrames, hotFrames, censerFrames, brazierFrames;
    SpriteRenderer[] beamSr = new SpriteRenderer[3], laneSr = new SpriteRenderer[6], sweepSr = new SpriteRenderer[9];
    Sprite sGlow, sBlob, sRing, sMark, sLane, sSlash, sSpin, sSkW1, sSkW2, sSkWind, sSkStrike, sSkIdle, sBall, sPile;
    static readonly Quaternion FLAT = Quaternion.Euler(90f, 0f, 0f);

    // ================================================================== build
    void Build()
    {
        root = new GameObject("FurnaceHeart_Scene").transform; root.SetParent(transform, false);
        SpriteRenderer floor = SR("Floor", Spr("floor", 1024, 1024, 100f), root, -30000); floor.transform.rotation = FLAT; floor.transform.localScale = new Vector3(1640f / 2048f, 1500f / 2048f, 1f);
        sGlow = Spr("glow", 128, 128, 100f); sBlob = Spr("blob", 64, 64, 100f); sRing = Spr("ring", 256, 256, 100f); sMark = Spr("mark", 128, 128, 100f); sLane = Spr("lane", 0, 16, 100f);
        sSlash = Spr("slash", 256, 256, 100f); sSpin = Spr("spin", 256, 256, 100f); sBall = Spr("fireball", 32, 32, 200f); sPile = Spr("pile", 32, 32, 100f);
        sSkW1 = Spr("skel_walk1", 128, 20, 200f); sSkW2 = Spr("skel_walk2", 128, 20, 200f); sSkWind = Spr("skel_wind", 128, 20, 200f); sSkStrike = Spr("skel_strike", 128, 20, 200f); sSkIdle = Spr("skel_idle", 128, 20, 200f);
        sHunterIdle = Spr("hunter_idle", 128, 24, 200f); sHunterW1 = Spr("hunter_walk1", 128, 24, 200f); sHunterW2 = Spr("hunter_walk2", 128, 24, 200f);
        litFrames = new Sprite[4]; hotFrames = new Sprite[4]; censerFrames = new Sprite[4]; brazierFrames = new Sprite[4];
        for (int i = 0; i < 4; i++) { litFrames[i] = Spr("giant_lit_" + i, 256, 24, 200f); hotFrames[i] = Spr("giant_hot_" + i, 256, 24, 200f); censerFrames[i] = Spr("censer_" + i, 128, 128, 200f); brazierFrames[i] = Spr("brazier_" + i, 64, 12, 200f); }

        // scenery
        float[] backX = { -560, -430, -300, -170, 170, 300, 430, 560 }; Sprite col = Spr("column", 128, 28, 200f);
        foreach (float x in backX) AddProp(0, x, -HH - Rnd(60, 150), col, Rnd(30, 42) / 36f, Rnd(110, 230) / 200f);
        for (int sg = -1; sg <= 1; sg += 2) for (float y = -HH + 40; y < HH + 150; y += Rnd(150, 230)) AddProp(0, sg * (HW + Rnd(90, 170)), y, col, Rnd(30, 40) / 36f, Rnd(80, 190) / 200f);
        for (int i = 0; i < 7; i++) AddProp(0, Rnd(-700, 700), -HH - Rnd(230, 330), col, Rnd(34, 46) / 36f, Rnd(160, 280) / 200f);
        for (int i = 0; i < 3; i++)
        {
            AddProp(1, PILLARS[i, 0], PILLARS[i, 1], Spr("pillar", 128, 28, 200f), 1f, 1f);
            pillarGlowSr[i] = SR("PillarGlow", sGlow, root, Ord(PILLARS[i, 1]) + 1); pillarGlowSr[i].transform.position = W(PILLARS[i, 0], PILLARS[i, 1], 60); pillarGlowSr[i].transform.localScale = Vector3.one * 0.7f; pillarGlowSr[i].color = new Color(1f, 0.67f, 0.24f, 0f); bills.Add(pillarGlowSr[i].transform);
        }
        for (int sg = -1; sg <= 1; sg += 2) { AddProp(2, sg * (HW - 40), -330, null, 1f, 1f); AddProp(2, sg * (HW - 40), -60, null, 1f, 1f); AddProp(2, sg * (HW - 40), 240, null, 1f, 1f); AddProp(2, sg * 260, HH - 40, null, 1f, 1f); }

        // the furnace
        bRoot = new GameObject("Furnace").transform; bRoot.SetParent(root, false); bRoot.position = W(BX, BY); bills.Add(bRoot);
        bDark = SR("Dark", Spr("giant_dark", 256, 24, 200f), bRoot, Ord(BY)); bLit = SR("Lit", litFrames[0], bRoot, Ord(BY) + 1); bHot = SR("Hot", hotFrames[0], bRoot, Ord(BY) + 2);
        bGlowFloor = SR("FurnaceGlow", sGlow, root, -19800); bGlowFloor.transform.rotation = FLAT; bGlowFloor.transform.position = W(BX, BY, 0.5f); bGlowFloor.transform.localScale = Vector3.one * (520f / 256f);
        bGlowTop = SR("FurnaceGlowTop", sGlow, root, Ord(BY) + 3); bGlowTop.transform.position = W(BX, BY, 195); bGlowTop.transform.localScale = Vector3.one * 1.2f; bills.Add(bGlowTop.transform);
        for (int s = 0; s < 3; s++)
        {
            Fist f = new Fist(); fist[s] = f; if (s == 2) continue;
            f.tr = new GameObject("Censer").transform; f.tr.SetParent(root, false); bills.Add(f.tr); f.sr = SR("Ball", censerFrames[0], f.tr, 0);
            f.blob = SR("CenserShadow", sBlob, root, -19000); f.blob.transform.rotation = FLAT;
            f.mark = SR("CenserMark", sMark, root, -18000); f.mark.transform.rotation = FLAT; f.mark.enabled = false;
            f.links = new SpriteRenderer[9]; for (int i = 0; i < 9; i++) { f.links[i] = SR("Link", Spr("link", 16, 16, 100f), root, 0); f.links[i].transform.localScale = Vector3.one * 0.7f; bills.Add(f.links[i].transform); }
        }
        for (int i = 0; i < 3; i++) { beamSr[i] = SR("Beam", sLane, root, -17000 + i); beamSr[i].transform.SetParent(root, false); beamSr[i].enabled = false; }
        for (int i = 0; i < 6; i++) { laneSr[i] = SR("Lane", sLane, root, -17500); laneSr[i].enabled = false; }
        for (int i = 0; i < 9; i++) { sweepSr[i] = SR("SweepMark", sMark, root, -18100); sweepSr[i].transform.rotation = FLAT; sweepSr[i].enabled = false; }
        watchSr = SR("Watch", Spr("watch", 32, 32, 200f), root, 0); watchSr.enabled = false; bills.Add(watchSr.transform);
        for (int i = 0; i < 9; i++)
        {
            float a = Mathf.PI * (0.06f + i / 8f * 0.88f); Sleeper s = new Sleeper(); s.x = BX + Mathf.Cos(a) * 250f; s.y = BY + 50f + Mathf.Sin(a) * 150f;
            s.sr = SR("Sleeper", sSkIdle, root, -18500); s.sr.transform.position = W(s.x, s.y, 1f); s.sr.transform.rotation = Quaternion.Euler(0f, Rnd(60f, 120f), 0f) * FLAT; sleepers[i] = s;
        }
        // the hunter
        pRoot = new GameObject("Hunter").transform; pRoot.SetParent(root, false); pRoot.localScale = Vector3.one * 1.22f; bills.Add(pRoot);
        pBody = SR("Body", sHunterIdle, pRoot, 0); pArm = SR("Arm", Spr("arm", 8, 8, 200f), pRoot, 1); pSword = SR("Sword", Spr("sword", 40, 32, 200f), pRoot, 2);
        pBlob = SR("HunterShadow", sBlob, root, -19000); pBlob.transform.rotation = FLAT; pBlob.transform.localScale = new Vector3(0.36f, 0.24f, 1f);
        pGlow = SR("HunterLight", sGlow, root, -19500); pGlow.transform.rotation = FLAT; pGlow.transform.localScale = Vector3.one * (380f / 256f); pGlow.color = new Color(1f, 0.9f, 0.85f, 0.22f);
    }
    void AddProp(int kind, float x, float y, Sprite s, float sx, float sy)
    {
        Prop p = new Prop(); p.kind = kind; p.x = x; p.y = y; p.ph = Rnd(0f, 6f); if (kind == 2) s = brazierFrames[0];
        p.sr = SR("Prop" + kind, s, root, Ord(y)); p.sr.transform.position = W(x, y); p.sr.transform.localScale = new Vector3(sx, sy, 1f); bills.Add(p.sr.transform);
        if (kind == 2)
        {
            p.glowA = SR("GlowFloor", sGlow, root, -19800); p.glowA.transform.rotation = FLAT; p.glowA.transform.position = W(x, y, 0.5f); p.glowA.transform.localScale = Vector3.one * (300f / 256f); p.glowA.color = new Color(1f, 0.5f, 0.16f, 0.3f);
            p.glowB = SR("GlowFlame", sGlow, root, Ord(y) + 1); p.glowB.transform.position = W(x, y, 84f); p.glowB.transform.localScale = Vector3.one * (160f / 256f); p.glowB.color = new Color(1f, 0.6f, 0.2f, 0.4f); bills.Add(p.glowB.transform);
        }
        props.Add(p);
    }

    void Clear<TT>(List<TT> l, System.Action<TT> free) { foreach (TT o in l) free(o); l.Clear(); }
    void StartFight()
    {
        Clear(minions, m => { Free(m.sr); Free(m.blob); }); Clear(embers, e => Free(e.sr)); Clear(rings, r => Free(r.sr)); Clear(rocks, r => { Free(r.mark); Free(r.inner); }); Clear(rubble, r => Free(r.sr));
        Clear(balls, b => { Free(b.sr); Free(b.blob); }); Clear(parts, p => Free(p.sr)); Clear(fxs, f => Free(f.sr)); foreach (SpriteRenderer p in piles) Free(p); piles.Clear(); words.Clear();
        px = 0; py = 230; pvx = pvy = pkx = pky = 0; pface = -1.57f; pinv = 0; pcd = 0; psw = 0; pcombo = 0; pcomboT = 0; pdash = 0; pdashCd = 0; pdashT = 0; pdx = 0; pdy = -1; pwalk = 0; php = MaxHp; grazeNext = false;
        bhp = 9; bstate = BS.Idle; bt = 1.4f; bT = 1f; heat[0] = heat[1] = heat[2] = 0; side = 0; queue.Clear(); heartPick = -1; bshake = 0; phase = 1; patI = 0; bdead = 0; flinch = 0; lit = 0; rise = 0; heartAt = 2; hx = BX; hy = BY; hh = 125; wander = 0; lanes.Clear();
        for (int s = 0; s < 3; s++) { Fist f = fist[s]; f.x = s == 0 ? -200f : s == 1 ? 200f : 0f; f.y = s == 2 ? -110f : -140f; f.up = s == 2 ? 1f : 0f; f.tx = f.x; f.ty = f.y; f.heat = 0; f.st = 0; f.t = Rnd(6f, 8f); }
        for (int i = 0; i < 9; i++) { sleepers[i].awake = false; sleepers[i].sr.enabled = true; order[i] = i; }
        for (int i = 8; i > 0; i--) { int j = Random.Range(0, i + 1); int tmp = order[i]; order[i] = order[j]; order[j] = tmp; }
        T = 0; stage = 0; stageT = 0; stageTime = 0; woken = 0; nextWake = 1.6f; killed = 0; irUses = 0; ir = false; got = false; hasWatch = false; watchSr.enabled = false; camX = 0; camY = 30f; shake = 0; stopT = 0; endT = 0; fadeBlack = 0;
        for (int i = 0; i < 3; i++) pillarGlow[i] = 0;
        mode = Mode.Play; Banner("THE NINE AROUND THE FURNACE", Easy ? "The furnace burns lower. You feel sturdier." : "They were lying still a moment ago.", 3f); Say("", 0f);
        if (music != null && music.clip != null) { music.Stop(); music.volume = 0f; music.Play(); musicTarget = musicVolume; musicRate = musicVolume / 1.5f; }
        if (skipSkeletons) { for (int i = 0; i < 9; i++) { sleepers[i].awake = true; sleepers[i].sr.enabled = false; } killed = 9; woken = 9; stage = 2; lit = 1; rise = 1; bt = 1.2f; }
    }
    void Banner(string a, string b, float t) { banner = a; bannerSub = b; bannerT = t; }
    void AddWord(float x, float y, float z, string text, Color c, int size) { Word w = new Word(); w.x = x; w.y = y; w.z = z; w.text = text; w.c = c; w.size = size; w.l = 0.9f; words.Add(w); }
    void AddPart(float x, float y, float z, float vx, float vy, float vz, float l, float s, Color c, bool flash, float g)
    {
        Part p = new Part(); p.x = x; p.y = y; p.z = z; p.vx = vx; p.vy = vy; p.vz = vz; p.l = l; p.s = s; p.c = c; p.flash = flash; p.g = g; p.sr = Pooled(flash ? sGlow : sqSprite, root, 0); parts.Add(p);
    }
    void Burst(float x, float y, float z, int n, Color c, float sp, float life, float size) { for (int i = 0; i < n; i++) AddPart(x, y, z + Rnd(0, 20), Rnd(-sp, sp), Rnd(-sp, sp) * 0.6f, Rnd(0, sp * 0.7f), Rnd(life * 0.5f, life), Rnd(size * 0.6f, size * 1.4f), c, false, 260f); }
    static readonly Color BONE = new Color(0.87f, 0.82f, 0.71f), EMBER = new Color(1f, 0.7f, 0.28f), HURT = new Color(1f, 0.42f, 0.35f);

    bool Hurt()
    {
        if (pinv > 0f || pdash > 0f || mode != Mode.Play) return false;
        if (Easy && easyHalfDamage)
        {
            grazeNext = !grazeNext;
            if (grazeNext)
            {
                pinv = easyInvulnerable; shake = Mathf.Max(shake, 8f); Sfx("sfx_hurt", 0.5f);
                AddWord(px, py, 70, "GRAZED", new Color(1f, 0.85f, 0.6f), 22);
                return true;
            }
        }
        php--; pinv = Easy ? easyInvulnerable : 1f; shake = Mathf.Max(shake, 12f); stopT = 0.06f; Sfx("sfx_hurt", 0.8f); AddWord(px, py, 70, "OOF!", HURT, 26); Burst(px, py, 30, 10, HURT, 160, 0.4f, 4);
        if (php <= 0) { mode = Mode.Dead; deaths++; bannerT = 0; Say("", 0f); musicTarget = 0.12f * musicVolume / 0.5f; musicRate = 0.4f; }
        return true;
    }
    float AimAng()
    {
        Ray r = cam.ScreenPointToRay(MousePos()); if (Mathf.Abs(r.direction.y) < 1e-4f) return pface;
        float k = -r.origin.y / r.direction.y; Vector3 p = r.origin + r.direction * k; return Mathf.Atan2(-p.z / U - py, p.x / U - px);
    }
    static float ADiff(float a, float b) { return Mathf.Abs(Mathf.DeltaAngle(a * Mathf.Rad2Deg, b * Mathf.Rad2Deg)) * Mathf.Deg2Rad; }
    bool InArc(float tx, float ty, float tr, float range, float half, float ang) { float d = Dist(px, py, tx, ty); if (d > range + tr) return false; if (d < tr + 14f) return true; return ADiff(ang, Mathf.Atan2(ty - py, tx - px)) < half; }
    void HeartGround(out float gx, out float gy) { if (heartAt == 0 || heartAt == 1) { gx = fist[heartAt].x; gy = fist[heartAt].y; } else { gx = BX; gy = BY + 92f; } }
    void PartPos(int at, out float x, out float y, out float h) { if (at == 0 || at == 1) { x = fist[at].x; y = fist[at].y; h = 24f + fist[at].up * 110f; } else { x = BX; y = BY; h = at == 3 ? 195f : 125f; } }

    void Attack(float ang)
    {
        if (pcd > 0f || (mode != Mode.Play && mode != Mode.Won)) return;
        pcombo = pcomboT > 0f ? pcombo + 1 : 1; pcomboT = 0.7f; bool wide = pcombo % 3 == 0; float range = wide ? 150f : 128f, half = wide ? Mathf.PI : 1.15f; pcd = wide ? 0.4f : 0.22f; psw = 0.15f; pswA = ang; pswH = half; pface = ang;
        if (!wide) { px += Mathf.Cos(ang) * 16f; py += Mathf.Sin(ang) * 16f; }
        for (int i = 0; i < 2; i++)
        {
            Fx f = new Fx(); f.max = f.l = i == 0 ? 0.9f : 0.16f; f.a0 = i == 0 ? 0.8f : 1f; f.sr = Pooled(wide ? sSpin : sSlash, root, i == 0 ? -15000 : -14000);
            f.sr.transform.position = W(px, py, i == 0 ? 1f : 3f); f.sr.transform.rotation = Quaternion.Euler(0f, ang * Mathf.Rad2Deg, 0f) * FLAT; f.sr.transform.localScale = Vector3.one * (range * (i == 0 ? 0.78f : 0.8f) / 100f); fxs.Add(f);
        }
        Sfx(wide ? "sfx_spin" : "sfx_swing", 0.75f, 0.08f); if (mode != Mode.Play) return; int hit = 0;
        if (stage == 0) { foreach (Min m in minions) if (!m.dead && m.state != 0 && InArc(m.x, m.y, 16f, range - 10f, half, ang)) { KillMinion(m); hit++; } }
        else if (stage == 2 && bdead <= 0f)
        {
            float gx, gy; HeartGround(out gx, out gy);
            if (bstate == BS.Exposed && InArc(gx, gy, 30f, range, half, ang))
            {
                bhp = Mathf.Max(0, bhp - (Easy ? Mathf.Max(1, easyCutDamage) : 1)); flinch = 0.25f; stopT = 0.08f; shake = 10f; Sfx("sfx_hit", 0.9f); Sfx("sfx_thud", 0.8f);
                AddWord(gx, gy, 90, bhp > 0 ? bhp + " EMBERS LEFT" : "THE LAST EMBER", new Color(1f, 0.83f, 0.29f), 28); Burst(gx, gy, 40, 16, EMBER, 260, 0.5f, 5);
                int cuts = 9 - bhp; if ((cuts == 4 || cuts == 7 || cuts == 8) && php < MaxHp) { php++; AddWord(px, py, 96, "+1", new Color(0.62f, 1f, 0.69f), 30); Burst(px, py, 30, 10, new Color(0.62f, 1f, 0.69f), 140, 0.6f, 4); Sfx("sfx_heal", 0.8f); }
                if (bhp <= 0) Die(); else { bstate = BS.Return; bt = 0.5f; }   // one cut per opening
                return;
            }
            float[,] tg = { { fist[0].x, fist[0].y, 44, fist[0].up }, { fist[1].x, fist[1].y, 44, fist[1].up }, { BX, BY + 40f, 100, 0 } };
            for (int i = 0; i < 3; i++) if (tg[i, 3] < 0.4f && InArc(tg[i, 0], tg[i, 1], tg[i, 2], range - 20f, half, ang))
            { Sfx("sfx_clank", 0.7f); AddWord(tg[i, 0], tg[i, 1], 80, "CLONK", BONE, 22); Burst(tg[i, 0], tg[i, 1], 40, 6, Color.white, 200, 0.25f, 3); pkx = -Mathf.Cos(ang) * 200f; pky = -Mathf.Sin(ang) * 200f; break; }
        }
        if (hit > 0) { stopT = 0.04f; shake = Mathf.Min(10f, 4f + hit * 2f); Sfx("sfx_hit", 0.8f, 0.06f); }
    }
    void Dash()
    {
        if (pdashCd > 0f || (mode != Mode.Play && mode != Mode.Won)) return; pdash = 0.17f; pdashCd = 0.5f; pinv = Mathf.Max(pinv, 0.4f);
        bool mv = Held(0) || Held(1) || Held(2) || Held(3); pdashA = mv ? Mathf.Atan2(pdy, pdx) : (useMouse ? AimAng() : pface); Sfx("sfx_dash", 0.8f, 0.05f);
    }
    static void PushOut(ref float ox, ref float oy, float x, float y, float r) { float d = Dist(ox, oy, x, y), m = r + 14f; if (d < m && d > 0.01f) { ox = x + (ox - x) / d * m; oy = y + (oy - y) / d * m; } }
    void Collide(ref float ox, ref float oy, bool isPlayer)
    {
        float nx = (ox - BX) / 134f, ny = (oy - (BY + 10f)) / 88f, l = Mathf.Sqrt(nx * nx + ny * ny); if (l < 1f && l > 0.01f) { ox = BX + nx / l * 134f; oy = BY + 10f + ny / l * 88f; }
        if (isPlayer && stage == 2 && bdead <= 0f) for (int s = 0; s < 3; s++) if (fist[s].up < 0.3f) PushOut(ref ox, ref oy, fist[s].x, fist[s].y, fist[s].r - 6f);
        for (int i = 0; i < 3; i++) PushOut(ref ox, ref oy, PILLARS[i, 0], PILLARS[i, 1], PR);
        ox = Mathf.Clamp(ox, -HW + 24f, HW - 24f); oy = Mathf.Clamp(oy, -HH + 24f, HH - 24f);
    }

    // ---------------------------------------------------------------- stage 1: the nine wake
    void WakeGroup()
    {
        for (int i = 0; i < 3 && woken < 9; i++)
        {
            Sleeper s = sleepers[order[woken++]]; s.awake = true; s.sr.enabled = false;
            Min m = new Min(); m.x = m.sx = s.x; m.y = m.sy = s.y; m.tx = Mathf.Clamp(s.x + Rnd(-90, 90), -HW + 60, HW - 60); m.ty = Rnd(20, 110); m.state = 0; m.t = m.T = 0.8f; m.cd = Rnd(0.4f, 1.4f); m.seed = Rnd(0, 10);
            m.sr = Pooled(sSkW1, root, 0); m.blob = Pooled(sBlob, root, -19000); m.blob.transform.rotation = FLAT; m.blob.transform.localScale = new Vector3(0.32f, 0.2f, 1f); minions.Add(m);
        }
        AddWord(BX, BY + 140f, 60, "CLAK CLAK CLAK", BONE, 26); Sfx("sfx_wake", 0.8f);
    }
    void KillMinion(Min m)
    {
        m.dead = true; killed++; Burst(m.x, m.y, 30, 14, BONE, 200, 0.8f, 5);
        SpriteRenderer p = Pooled(sPile, root, -18400); p.transform.position = W(m.x, m.y, 1f); p.transform.rotation = Quaternion.Euler(0f, Rnd(0f, 360f), 0f) * FLAT; piles.Add(p);
        Ember e = new Ember(); e.x = m.x; e.y = m.y; e.wob = Rnd(0f, 6.28f); e.h = 30f; e.sr = Pooled(sGlow, root, 0); embers.Add(e);
        AddWord(m.x, m.y, 70, "CLATTER!", BONE, 26); Sfx("sfx_bone", 0.8f, 0.1f);
    }
    void UpdMinions(float dt)
    {
        if (woken < 9) { nextWake -= dt; if (nextWake <= 0f || (woken > 0 && minions.Count == 0 && nextWake < 8f)) { WakeGroup(); nextWake = 9f; } }
        foreach (Min m in minions)
        {
            if (m.dead) continue; m.t -= dt; m.cd = Mathf.Max(0f, m.cd - dt); float d = Dist(m.x, m.y, px, py), a = Mathf.Atan2(py - m.y, px - m.x);
            if (m.state == 0) { float k = Mathf.Clamp01(1f - m.t / m.T); m.x = Mathf.Lerp(m.sx, m.tx, k); m.y = Mathf.Lerp(m.sy, m.ty, k); m.z = Mathf.Sin(k * Mathf.PI) * 70f; if (m.t <= 0f) { m.state = 1; m.z = 0; } continue; }
            float mv = 0f, dir = a;
            if (m.state == 1) { m.face = Mathf.Cos(a) >= 0f ? 1 : -1; mv = 100f; dir = a + Mathf.Sin(T * 3f + m.seed) * 0.4f; if (d < 66f && m.cd <= 0f) { m.state = 2; m.t = 0.65f; } }
            else if (m.state == 2) { if (m.t <= 0f) { m.state = 3; m.t = 0.15f; if (d < 74f) Hurt(); } }
            else if (m.state == 3) { if (m.t <= 0f) { m.state = 4; m.t = 0.9f; m.cd = 1.2f; } }
            else if (m.t <= 0f) m.state = 1;
            m.x += Mathf.Cos(dir) * mv * dt; m.y += Mathf.Sin(dir) * mv * dt;
            foreach (Min o in minions) { if (o == m || o.dead || o.state == 0) continue; float dd = Dist(m.x, m.y, o.x, o.y); if (dd < 34f && dd > 0.01f) { m.x += (m.x - o.x) / dd * (34f - dd) / 2f; m.y += (m.y - o.y) / dd * (34f - dd) / 2f; } }
            Collide(ref m.x, ref m.y, false);
        }
        for (int i = minions.Count - 1; i >= 0; i--) if (minions[i].dead) { Free(minions[i].sr); Free(minions[i].blob); minions.RemoveAt(i); }
        if (killed >= 9 && minions.Count == 0 && embers.Count == 0) { stage = 1; stageT = 3.4f; stageTime = 0; Banner("NINE EMBERS, ONE HEART", "The furnace wakes. You will need thermal sight to find its heart.", 3.2f); Sfx("sfx_roar", 0.9f); }
    }
    void UpdEmbers(float dt)
    {
        float tx = BX, ty = BY + 30f;
        for (int i = embers.Count - 1; i >= 0; i--)
        {
            Ember e = embers[i]; float a = Mathf.Atan2(ty - e.y, tx - e.x) + Mathf.Sin(T * 5f + e.wob) * 0.6f, d = Dist(e.x, e.y, tx, ty); e.x += Mathf.Cos(a) * 170f * dt; e.y += Mathf.Sin(a) * 170f * dt; e.h = Mathf.Lerp(e.h, 150f, dt * 1.5f);
            if (d < 16f) { lit = Mathf.Min(1f, lit + 0.08f); Sfx("sfx_heal", 0.3f); Free(e.sr); embers.RemoveAt(i); }
        }
    }

    // ---------------------------------------------------------------- stage 2: the furnace
    void Next(float sp)
    {
        int[] pat = PAT[phase - 1]; int a = pat[patI % pat.Length]; patI++; int near = px < BX ? 0 : 1;
        if (a == 0) { side = near; queue.Clear(); heartPick = -1; bstate = BS.Heat; bT = 1.3f * sp; bt = bT; }
        else if (a == 1) { side = near; queue.Clear(); queue.Add(1 - near); heartPick = Random.Range(0, 2); bstate = BS.Heat; bT = 1.1f * sp; bt = bT; }
        else if (a == 2) { bstate = BS.Charge; bT = 1.4f * sp; bt = bT; Sfx("sfx_charge", 0.7f); }
        else if (a == 4) { side = near; bstate = BS.SweepT; bT = 1f * sp; bt = bT; a0 = near == 0 ? Mathf.PI * 0.97f : Mathf.PI * 0.03f; a1 = near == 0 ? Mathf.PI * 0.03f : Mathf.PI * 0.97f; Sfx("sfx_toggle", 0.6f); }
        else if (a == 5) { bstate = BS.LanesT; bT = 1.15f * sp; bt = bT; int n = phase == 3 ? 6 : 5; float off = Rnd(-0.08f, 0.08f); lanes.Clear(); for (int i = 0; i < n; i++) lanes.Add(Mathf.PI * (0.1f + 0.8f * i / (n - 1)) + off); Sfx("sfx_charge", 0.6f); }
        else if (a == 6) { bstate = BS.VolleyT; bT = 0.7f * sp; bt = bT; Sfx("sfx_toggle", 0.6f); }
        else { bstate = BS.Shake; bt = 0.55f; Sfx("sfx_roar", 0.4f); }
    }
    void Impact(Fist f)
    {
        shake = 16f; Sfx("sfx_boom", 1f); if (Dist(px, py, f.x, f.y) < f.r + 22f) Hurt();
        Ring r = new Ring(); r.x = f.x; r.y = f.y; r.r = f.r; r.max = 215f; r.sr = Pooled(sRing, root, -16000); r.sr.transform.rotation = FLAT; rings.Add(r);
        Burst(f.x, f.y, 10, 16, new Color(1f, 0.67f, 0.35f, 0.9f), 260, 0.5f, 6);
    }
    void BeamCheck()
    {
        float mx = BX, my = BY + 70f, dx = Mathf.Cos(beamA), dy = Mathf.Sin(beamA), end = 1100f;
        for (int i = 0; i < 3; i++)
        {
            float t = (PILLARS[i, 0] - mx) * dx + (PILLARS[i, 1] - my) * dy, perp = Mathf.Abs((PILLARS[i, 0] - mx) * dy - (PILLARS[i, 1] - my) * dx);
            if (t > 0f && perp < PR) { float ht = t - Mathf.Sqrt(PR * PR - perp * perp); if (ht < end) { end = ht; pillarGlow[i] = 1f; } }
        }
        beamEnd = end; float tp = (px - mx) * dx + (py - my) * dy, pp = Mathf.Abs((px - mx) * dy - (py - my) * dx); if (tp > 60f && tp < end && pp < 26f) Hurt();
    }
    void AddRock(float x, float y, float sp)
    {
        Rock r = new Rock(); r.x = x; r.y = y; r.T = r.t = 1.15f * sp + Rnd(0f, 0.5f); r.mark = Pooled(sMark, root, -17800); r.mark.transform.rotation = FLAT; r.mark.transform.position = W(x, y, 1f); r.mark.transform.localScale = Vector3.one * (46f * U / 1.18f);
        r.inner = Pooled(sRing, root, -17700); r.inner.transform.rotation = FLAT; r.inner.transform.position = W(x, y, 1.2f); rocks.Add(r);
    }
    void DropRocks(int ph, float sp)
    {
        int n = 6 + ph * 2;
        for (int i = 0; i < n; i++)
        {
            float x = 0, y = 0; for (int k = 0; k < 20; k++) { x = Rnd(-HW + 60, HW - 60); y = Rnd(-130, HH - 50); bool bad = false; for (int q = 0; q < 3; q++) if (Dist(x, y, PILLARS[q, 0], PILLARS[q, 1]) < PR + 30f) bad = true; if (!bad) break; }
            AddRock(x, y, sp);
        }
        AddRock(px, py, sp);
    }
    void Expose(float t, int at) { bstate = BS.Exposed; bt = t * (Easy ? Mathf.Max(1f, easyExposeLonger) : 1f); heartAt = at; }
    void UpdBoss(float dt)
    {
        int ph = bhp > 6 ? 1 : bhp > 3 ? 2 : 3;
        if (ph != phase)
        {
            phase = ph; if (ph == 2) Banner("IT GETS CAGEY", "Double slams. The heart can end up in either censer. Check thermal.", 2.8f); else Banner("SUPERHEAT", "Everything is boiling. In thermal, the heart is now the COLD spot.", 3.4f); Sfx("sfx_roar", 0.9f);
        }
        float sp = (ph == 3 ? 0.8f : 1f) * 0.86f; bt -= dt; flinch = Mathf.Max(0f, flinch - dt); bshake = Mathf.Max(0f, bshake - dt);
        for (int k = 0; k < 3; k++)
        {
            bool act = k < 2 ? ((bstate == BS.Heat || bstate == BS.SweepT || bstate == BS.Sweep) && side == k) : (bstate == BS.Charge || bstate == BS.Beam || bstate == BS.LanesT || bstate == BS.Lanes || bstate == BS.VolleyT);
            if (!act) heat[k] = Mathf.Max(0f, heat[k] - dt * 1.2f);
        }
        float tx, ty, th; PartPos(heartAt, out tx, out ty, out th); float hk = Mathf.Min(1f, dt * 7f); hx = Mathf.Lerp(hx, tx, hk); hy = Mathf.Lerp(hy, ty, hk); hh = Mathf.Lerp(hh, th, hk);
        float open = ph == 1 ? 2.1f : 1.6f; Fist f = fist[side];
        switch (bstate)
        {
            case BS.Idle:
                wander -= dt; if (wander <= 0f) { heartAt = Random.value < 0.34f ? 3 : 2; wander = Rnd(0.8f, 1.5f); } if (bt <= 0f) Next(sp); break;
            case BS.Heat:
                {
                    float k = Mathf.Clamp01(1f - bt / bT); heat[side] = k; f.up = Mathf.Min(1f, f.up + dt * 3f);
                    if (k < 0.6f)
                    {
                        float ax = Mathf.Clamp(px, -HW + 50, HW - 50), ay = Mathf.Clamp(py, -150, HH - 40), sxh = BX + (side == 0 ? -80f : 80f), dd = Dist(sxh, BY, ax, ay);
                        if (dd > 620f) { float a = Mathf.Atan2(ay - BY, ax - sxh); ax = sxh + Mathf.Cos(a) * 620f; ay = BY + Mathf.Sin(a) * 620f; }
                        f.tx = ax; f.ty = ay;
                    }
                    f.x = Mathf.Lerp(f.x, f.tx, Mathf.Min(1f, dt * 6f)); f.y = Mathf.Lerp(f.y, f.ty, Mathf.Min(1f, dt * 6f)); if (k > 0.25f) heartAt = side; if (bt <= 0f) { bstate = BS.Drop; bt = 0.1f; }
                    break;
                }
            case BS.Drop:
                f.up = Mathf.Max(0f, bt / 0.1f);
                if (bt <= 0f)
                {
                    f.up = 0f; Impact(f); heat[side] = 0.6f;
                    if (queue.Count > 0) { side = queue[0]; queue.RemoveAt(0); bstate = BS.Heat; bT = 0.85f * sp; bt = bT; }
                    else { int at = heartPick >= 0 ? heartPick : side; heartPick = -1; Expose(open, at); }
                }
                break;
            case BS.Charge:
                heat[2] = Mathf.Clamp01(1f - bt / bT); heartAt = 2;
                if (bt <= 0f) { bstate = BS.Beam; bT = 2f * sp; bt = bT; bool dir = Random.value < 0.5f; a0 = dir ? 0.14f * Mathf.PI : 0.86f * Mathf.PI; a1 = dir ? 0.86f * Mathf.PI : 0.14f * Mathf.PI; Sfx("sfx_beam", 0.9f); }
                break;
            case BS.Beam:
                beamA = Mathf.Lerp(a0, a1, Mathf.Clamp01(1f - bt / bT)); heat[2] = 1f; BeamCheck();
                if (bt <= 0f) { heat[2] = 0.5f; Expose(open, ph == 1 ? 2 : (Random.value < 0.5f ? 2 : Random.Range(0, 2))); }
                break;
            case BS.Shake: bshake = 0.1f; if (bt <= 0f) { DropRocks(ph, sp); bstate = BS.Shedding; bt = 1.8f * sp; } break;
            case BS.Shedding: wander -= dt; if (wander <= 0f) { heartAt = Random.Range(0, 4); wander = 0.3f; } if (bt <= 0f) { bstate = BS.Idle; bt = 0.35f; } break;
            case BS.Exposed: if (bt <= 0f) { bstate = BS.Return; bt = 0.5f; } break;
            case BS.Return:
                for (int s = 0; s < 2; s++) { Fist g = fist[s]; float k = Mathf.Min(1f, dt * 8f), rx = s == 0 ? -200f : 200f; g.x = Mathf.Lerp(g.x, rx, k); g.y = Mathf.Lerp(g.y, -140f, k); g.tx = g.x; g.ty = g.y; g.up = Mathf.Max(0f, g.up - dt * 4f); }
                if (bt <= 0f) { bstate = BS.Idle; bt = 0.5f * sp; }
                break;
            case BS.SweepT:
                heat[side] = Mathf.Clamp01(1f - bt / bT); f.up = Mathf.Max(0.08f, f.up - dt * 3f); f.x = Mathf.Lerp(f.x, BX + Mathf.Cos(a0) * SWR, Mathf.Min(1f, dt * 7f)); f.y = Mathf.Lerp(f.y, BY + 30f + Mathf.Sin(a0) * SWR * 0.8f, Mathf.Min(1f, dt * 7f));
                if (bt <= 0f) { bstate = BS.Sweep; bT = 0.95f * sp; bt = bT; Sfx("sfx_swoop", 0.9f); }
                break;
            case BS.Sweep:
                {
                    float k = Mathf.Clamp01(1f - bt / bT), a = Mathf.Lerp(a0, a1, k * k * (3f - 2f * k)); heat[side] = 1f; f.up = 0.08f; f.x = BX + Mathf.Cos(a) * SWR; f.y = BY + 30f + Mathf.Sin(a) * SWR * 0.8f;
                    if (Dist(px, py, f.x, f.y) < f.r + 24f) Hurt(); if (dt > 0f && Random.value < 0.8f) AddPart(f.x, f.y, 20, 0, 0, 30, 0.35f, Rnd(5, 9), EMBER, false, 0f);
                    if (bt <= 0f) { f.up = 0f; shake = 8f; heat[side] = 0.6f; Expose(open, ph == 1 ? side : Random.Range(0, 2)); }
                    break;
                }
            case BS.LanesT: heat[2] = Mathf.Clamp01(1f - bt / bT); if (bt <= 0f) { bstate = BS.Lanes; bt = 0.55f; Sfx("sfx_boom", 1f); shake = 10f; } break;
            case BS.Lanes:
                heat[2] = 1f;
                foreach (float a in lanes) { float dx = Mathf.Cos(a), dy = Mathf.Sin(a), tp = (px - BX) * dx + (py - (BY + 70f)) * dy, pp = Mathf.Abs((px - BX) * dy - (py - (BY + 70f)) * dx); if (tp > 50f && pp < 24f) Hurt(); }
                if (bt <= 0f) { bstate = BS.Idle; bt = 0.5f * sp; }
                break;
            case BS.VolleyT:
                heat[2] = Mathf.Clamp01(1f - bt / bT);
                if (bt <= 0f)
                {
                    float mx = BX, my = BY + 70f, aa = Mathf.Atan2(py - my, px - mx); int n = ph == 3 ? 7 : 5;
                    for (int i = 0; i < n; i++) { float a = aa + (i - (n - 1) / 2f) * 0.24f; Ball b = new Ball(); b.x = mx; b.y = my; b.vx = Mathf.Cos(a) * 340f; b.vy = Mathf.Sin(a) * 340f; b.l = 3.2f; b.sr = Pooled(sBall, root, 0); b.blob = Pooled(sBlob, root, -19000); b.blob.transform.rotation = FLAT; b.blob.transform.localScale = new Vector3(0.22f, 0.14f, 1f); balls.Add(b); }
                    Sfx("sfx_volley", 0.9f); bstate = BS.Idle; bt = 0.6f * sp;
                }
                break;
        }
    }
    void UpdThird(float dt)
    {
        Fist f = fist[2]; f.t -= dt;
        if (f.st == 0) { f.up = Mathf.Min(1f, f.up + dt * 2f); f.x = Mathf.Lerp(f.x, BX + Mathf.Sin(T * 0.8f) * 120f, dt * 2f); f.y = Mathf.Lerp(f.y, BY + 130f, dt * 2f); f.heat = Mathf.Max(0f, f.heat - dt); if (f.t <= 0f && bstate != BS.Exposed && bstate != BS.Beam) { f.st = 1; f.T = f.t = 1.5f; Sfx("sfx_toggle", 0.4f); } }
        else if (f.st == 1) { float k = Mathf.Clamp01(1f - f.t / f.T); f.heat = k; f.up = 1f; if (k < 0.6f) { f.tx = Mathf.Clamp(px, -HW + 50, HW - 50); f.ty = Mathf.Clamp(py, -150, HH - 40); } f.x = Mathf.Lerp(f.x, f.tx, Mathf.Min(1f, dt * 6f)); f.y = Mathf.Lerp(f.y, f.ty, Mathf.Min(1f, dt * 6f)); if (f.t <= 0f) { f.st = 2; f.t = 0.1f; } }
        else if (f.st == 2) { f.up = Mathf.Max(0f, f.t / 0.1f); if (f.t <= 0f) { f.up = 0f; Impact(f); f.st = 3; f.t = 0.7f; } }
        else if (f.t <= 0f) { f.st = 0; f.t = Rnd(8f, 11f); }
    }
    void Die()
    {
        bdead = 0.001f; mode = Mode.Won; shake = 20f; Sfx("sfx_boom", 1f); musicTarget = 0.3f * musicVolume; musicRate = 0.2f;
        Clear(rocks, r => { Free(r.mark); Free(r.inner); }); Clear(rings, r => Free(r.sr)); Clear(balls, b => { Free(b.sr); Free(b.blob); });
        Burst(BX, BY, 120, 70, new Color(0.16f, 0.08f, 0.06f), 260, 1.4f, 9); Burst(BX, BY, 160, 40, EMBER, 240, 1.2f, 6);
        hasWatch = true; watchX = BX; watchY = BY + 130f; Banner("THE FURNACE GOES COLD", "Something small is still glowing in the ash.", 3.2f);
    }

    // ================================================================== frame
    void Tick(float dt)
    {
        if (stopT > 0.15f) stopT = 0.15f; if (stopT > 0f) { stopT -= Mathf.Max(dt, 0.004f); dt = 0f; }
        if (shake > 0f) shake = Mathf.Max(0f, shake - Time.deltaTime * 40f);
        T += dt; stageTime += dt; irFlip = Mathf.Max(0f, irFlip - dt);
        if (KeyThermal() || MouseDown(1)) { ir = !ir; irFlip = 0.18f; irUses++; Sfx("sfx_toggle", 0.6f); }
        if (MouseDown(0)) { useMouse = true; Attack(AimAng()); }
        if (KeyAttack()) { useMouse = false; Attack(pface); }
        if (KeyDash()) Dash();
        float mx = (Held(1) ? 1 : 0) - (Held(0) ? 1 : 0), my = (Held(3) ? 1 : 0) - (Held(2) ? 1 : 0); bool mv = mx != 0 || my != 0;
        if (mv) { float l = Mathf.Sqrt(mx * mx + my * my); pdx = mx / l; pdy = my / l; if (!useMouse) pface = Mathf.Atan2(my, mx); pwalk += dt * 11f; } else pwalk = 0f;
        if (useMouse && psw <= 0f) pface = AimAng(); float spd = psw > 0f ? 90f : 225f;
        if (pdash > 0f) { pdash -= dt; px += Mathf.Cos(pdashA) * 760f * dt; py += Mathf.Sin(pdashA) * 760f * dt; pdashT = 0.12f; if (dt > 0f && Random.value < 0.9f) AddPart(px, py, 26, 0, 0, 0, 0.22f, 22, Color.white, true, 0f); }
        else { if (pdashT > 0f) pdashT -= dt; float k = Mathf.Min(1f, dt * (mv ? 16f : 11f)); pvx += ((mv ? pdx * spd : 0f) - pvx) * k; pvy += ((mv ? pdy * spd : 0f) - pvy) * k; px += (pvx + pkx) * dt; py += (pvy + pky) * dt; }
        float dm = Mathf.Pow(0.002f, dt); pkx *= dm; pky *= dm; Collide(ref px, ref py, true);
        if (pcd > 0f) pcd -= dt; if (psw > 0f) psw -= dt; if (pinv > 0f) pinv -= dt; if (pdashCd > 0f) pdashCd -= dt; if (pcomboT > 0f) pcomboT -= dt;
        for (int i = 0; i < 3; i++) pillarGlow[i] = Mathf.Max(0f, pillarGlow[i] - dt * 2f);
        UpdEmbers(dt);
        if (mode == Mode.Won)
        {
            bdead += dt;
            if (hasWatch && !got && Dist(px, py, watchX, watchY) < 44f) { got = true; bannerT = 0; Say("A pocket watch. Still ticking. Still warm.", 99f); Sfx("sfx_tick", 0.8f); mode = Mode.End; endT = 0f; }
        }
        else if (stage == 0) UpdMinions(dt);
        else if (stage == 1)
        {
            stageT -= dt; rise = Mathf.Clamp01(1f - stageT / 3.4f); lit = Mathf.Max(lit, rise); if (Random.value < dt * 8f) shake = Mathf.Max(shake, 3f);
            if (stageT <= 0f) { stage = 2; stageTime = 0; foreach (SpriteRenderer p in piles) Free(p); piles.Clear(); bstate = BS.Idle; bt = 1.2f; rise = 1f; shake = 16f; Sfx("sfx_boom", 1f); }
        }
        else
        {
            foreach (Ring w in rings)
            {
                w.r += 520f * dt; float d = Mathf.Abs(Dist(px, py, w.x, w.y) - w.r);
                if (!w.hit && d < 22f) { if (pdash > 0f || pdashT > 0f) { w.hit = true; AddWord(px, py, 80, "THROUGH!", new Color(1f, 0.94f, 0.75f), 20); } else if (d < 16f && Hurt()) w.hit = true; }
            }
            for (int i = rings.Count - 1; i >= 0; i--) if (rings[i].r >= rings[i].max) { Free(rings[i].sr); rings.RemoveAt(i); }
            for (int i = rocks.Count - 1; i >= 0; i--)
            {
                Rock r = rocks[i]; r.t -= dt;
                if (r.t <= 0f) { Sfx("sfx_thud", 0.7f); Burst(r.x, r.y, 6, 10, new Color(1f, 0.59f, 0.27f, 0.9f), 180, 0.45f, 5); Rubble rb = new Rubble(); rb.x = r.x; rb.y = r.y; rb.life = 1.6f; rb.sr = Pooled(sBlob, root, -18300); rb.sr.transform.rotation = FLAT; rb.sr.transform.position = W(r.x, r.y, 0.8f); rb.sr.transform.localScale = Vector3.one * 0.55f; rubble.Add(rb); if (Dist(px, py, r.x, r.y) < 46f) Hurt(); Free(r.mark); Free(r.inner); rocks.RemoveAt(i); }
            }
            for (int i = rubble.Count - 1; i >= 0; i--) { rubble[i].life -= dt; if (rubble[i].life <= 0f) { Free(rubble[i].sr); rubble.RemoveAt(i); } }
            for (int i = balls.Count - 1; i >= 0; i--)
            {
                Ball o = balls[i]; o.x += o.vx * dt; o.y += o.vy * dt; o.l -= dt; if (Mathf.Abs(o.x) > HW + 40f || Mathf.Abs(o.y) > HH + 40f) o.l = 0f;
                for (int q = 0; q < 3; q++) if (o.l > 0f && Dist(o.x, o.y, PILLARS[q, 0], PILLARS[q, 1]) < PR + 10f) { o.l = 0f; pillarGlow[q] = 1f; Burst(o.x, o.y, 26, 6, EMBER, 160, 0.3f, 4); }
                if (o.l > 0f && Dist(o.x, o.y, px, py) < 24f && Hurt()) o.l = 0f;
                if (o.l <= 0f) { Free(o.sr); Free(o.blob); balls.RemoveAt(i); }
            }
            if (mode == Mode.Play) { UpdBoss(dt); if (mode == Mode.Play && bdead <= 0f) UpdThird(dt); }
        }
        Draw(dt);
    }

    void PlaceLane(SpriteRenderer sr, float ang, float len, float width, Color c, float h)
    {
        sr.enabled = true; sr.color = c; sr.transform.position = W(BX + Mathf.Cos(ang) * 46f, BY + 70f + Mathf.Sin(ang) * 46f, h);
        sr.transform.rotation = Quaternion.Euler(0f, ang * Mathf.Rad2Deg, 0f) * FLAT; sr.transform.localScale = new Vector3(len * U / 0.64f, width * 2f * U / 0.13f, 1f);
    }
    void Draw(float dt)
    {
        float t = tNow;
        camX += (px * 0.6f - camX) * Mathf.Min(1f, dt * 5f); camY += (py * 0.4f - 60f - camY) * Mathf.Min(1f, dt * 5f);
        cam.transform.position = W(camX, camY + DC, HC) + new Vector3(Rnd(-shake, shake), Rnd(-shake, shake), 0f) * U;
        Quaternion bill = cam.transform.rotation; foreach (Transform b in bills) if (b != null) b.rotation = bill;

        // hunter
        float fx = Mathf.Cos(pface) >= 0f ? 1f : -1f; bool mv = pwalk > 0f;
        pRoot.position = W(px, py, mv ? 0f : Mathf.Sin(t * 2.6f) * 1.1f + 1.1f); pRoot.rotation = bill * Quaternion.Euler(0f, 0f, -(pvx / 225f) * 5f);
        pBody.sprite = mv ? (Mathf.FloorToInt(pwalk / 1.6f) % 2 == 0 ? sHunterW1 : sHunterW2) : sHunterIdle; pBody.flipX = fx < 0f;
        int po = Ord(py); pBody.sortingOrder = po; pArm.sortingOrder = po + 1; pSword.sortingOrder = po + 2;
        float blink = pinv > 0f && pdash <= 0f && Mathf.FloorToInt(t * 20f) % 2 == 1 ? 0.45f : 1f; Color pc = new Color(1f, 1f, 1f, blink); pBody.color = pc; pArm.color = pc; pSword.color = pc;
        float ga = pface + 0.9f; if (psw > 0f) { float k = 1f - psw / 0.15f; ga = pswA - pswH + k * 2f * pswH; }
        float bx = Mathf.Cos(ga), by = Mathf.Sin(ga) * K; Vector2 sh = new Vector2(fx * 12f, 43f) * 0.01f, hand = new Vector2(bx * 13f + fx * 3f, 29f - by * 10f) * 0.01f, dv = hand - sh;
        pArm.transform.localPosition = sh; pArm.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dv.y, dv.x) * Mathf.Rad2Deg); pArm.transform.localScale = new Vector3(dv.magnitude / 0.24f, 1f, 1f);
        pSword.transform.localPosition = hand; pSword.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(-by, bx) * Mathf.Rad2Deg); pSword.transform.localScale = new Vector3((0.72f + 0.28f * Mathf.Sqrt(bx * bx + by * by)) * 1.12f, 1.35f, 1f);
        pBlob.transform.position = W(px, py, 0.6f); pGlow.transform.position = W(px, py, 0.4f);

        // skeletons, embers
        foreach (Min m in minions)
        {
            m.sr.sprite = m.state == 2 ? sSkWind : m.state == 3 ? sSkStrike : m.state == 1 ? (Mathf.Sin(t * 9f + m.seed) > 0f ? sSkW1 : sSkW2) : sSkIdle;
            m.sr.transform.position = W(m.x + (m.state == 2 ? Rnd(-1.5f, 1.5f) : 0f), m.y, m.z); m.sr.transform.rotation = bill; m.sr.transform.localScale = new Vector3(1.1f, 1.1f, 1f); m.sr.flipX = m.face < 0; m.sr.sortingOrder = Ord(m.y);
            m.sr.color = m.state == 2 ? new Color(1f, 0.75f, 0.7f) : Color.white; m.blob.transform.position = W(m.x, m.y, 0.6f);
        }
        foreach (Ember e in embers) { e.sr.transform.position = W(e.x, e.y, e.h); e.sr.transform.rotation = bill; e.sr.transform.localScale = Vector3.one * 0.2f; e.sr.color = new Color(1f, 0.7f, 0.3f, 0.8f); e.sr.sortingOrder = Ord(e.y) + 5; }

        // the furnace and its censers
        int fr = Mathf.FloorToInt(t * 8f) % 4; bLit.sprite = litFrames[fr]; bHot.sprite = hotFrames[fr];
        float sq = stage == 2 ? 1f : 0.72f + 0.28f * rise, jx = (bshake > 0f || bstate == BS.Shake) ? Rnd(-4f, 4f) : 0f;
        if (bdead > 0f)
        {
            float k = Mathf.Min(1f, bdead / 1.4f); bRoot.position = W(BX, BY); bRoot.rotation = bill * Quaternion.Euler(0f, 0f, -k * 28f); bRoot.localScale = new Vector3(GS, GS * (1f - k * 0.55f), 1f);
            bLit.color = new Color(1f, 1f, 1f, Mathf.Max(0f, 0.6f - k)); bHot.color = new Color(1f, 1f, 1f, 0f); bGlowFloor.color = new Color(1f, 0.47f, 0.16f, 0f); bGlowTop.color = new Color(1f, 0.6f, 0.24f, 0f);
        }
        else
        {
            float punch = flinch > 0f ? 1f + 0.08f * Mathf.Sin(flinch * 50f) : 1f;
            bRoot.position = W(BX + jx, BY); bRoot.localScale = new Vector3(GS * punch, GS * sq * (1f + Mathf.Sin(t * 2f) * 0.008f), 1f);
            bLit.color = new Color(1f, 1f, 1f, lit); bHot.color = new Color(1f, 1f, 1f, phase == 3 && stage == 2 ? 1f : 0f);
            float mo = (bstate == BS.Charge || bstate == BS.LanesT || bstate == BS.VolleyT) ? Mathf.Clamp01(1f - bt / bT) : (bstate == BS.Beam || bstate == BS.Lanes) ? 1f : 0f;
            bGlowFloor.color = new Color(1f, 0.47f, 0.16f, 0.3f * lit); bGlowTop.color = new Color(1f, 0.6f + 0.3f * mo, 0.24f + 0.5f * mo, (0.4f + 0.4f * mo) * lit); bGlowTop.transform.position = W(BX, BY, 195f - 80f * mo);
        }
        bool showFists = stage == 2 && bdead <= 0f;
        for (int s = 0; s < 2; s++)
        {
            Fist f = fist[s]; float sg = s == 0 ? -1f : 1f; f.sr.enabled = showFists; f.blob.enabled = showFists; foreach (SpriteRenderer l in f.links) l.enabled = showFists; f.mark.enabled = false; if (!showFists) continue;
            float ch = 30f + f.up * 120f; f.tr.position = W(f.x, f.y, ch); f.sr.sortingOrder = Ord(f.y + f.up * 4f); f.sr.sprite = censerFrames[heat[s] > 0.7f ? 3 : heat[s] > 0.35f ? 2 : heat[s] > 0.1f ? 1 : 0];
            f.blob.transform.position = W(f.x, f.y, 0.6f); f.blob.transform.localScale = new Vector3(0.8f - 0.2f * f.up, 0.5f - 0.14f * f.up, 1f);
            Vector3 a = W(BX + sg * 67f * GS, BY, 204f * GS), c = W(f.x, f.y, ch + 36f);
            for (int i = 0; i < 9; i++) { float u = (i + 1) / 10f; Vector3 p = Vector3.Lerp(a, c, u); p.y -= 4f * u * (1f - u) * 0.6f * (1f - f.up); f.links[i].transform.position = p; f.links[i].sortingOrder = Ord(Mathf.Lerp(BY, f.y, u)) + 1; }
            if (bstate == BS.Heat && side == s) { float k = Mathf.Clamp01(1f - bt / bT); f.mark.enabled = true; f.mark.transform.position = W(f.x, f.y, 1f); f.mark.transform.localScale = Vector3.one * ((f.r + 16f) * U / 1.18f); f.mark.color = new Color(1f, 0.78f - 0.5f * k, 0.24f, 0.5f + 0.3f * Mathf.Sin(t * 20f)); }
        }
        // beam, lanes, sweep warning
        bool beaming = showFists && bstate == BS.Beam;
        if (beaming) { PlaceLane(beamSr[0], beamA, beamEnd - 46f, 46f, new Color(1f, 0.43f, 0.12f, 0.4f), 2f); PlaceLane(beamSr[1], beamA, beamEnd - 46f, 22f, new Color(1f, 0.67f, 0.24f, 0.85f), 2.2f); PlaceLane(beamSr[2], beamA, beamEnd - 46f, 9f, new Color(1f, 0.96f, 0.82f, 1f), 2.4f); }
        else for (int i = 0; i < 3; i++) beamSr[i].enabled = false;
        bool lanesOn = showFists && (bstate == BS.LanesT || bstate == BS.Lanes);
        for (int i = 0; i < 6; i++)
        {
            if (!lanesOn || i >= lanes.Count) { laneSr[i].enabled = false; continue; }
            if (bstate == BS.Lanes) PlaceLane(laneSr[i], lanes[i], 1000f, 30f, new Color(1f, 0.8f, 0.4f, 0.95f), 2f); else PlaceLane(laneSr[i], lanes[i], 1000f, 22f, new Color(1f, 0.4f, 0.14f, 0.12f + 0.3f * Mathf.Clamp01(1f - bt / bT)), 1.5f);
        }
        bool sweepWarn = showFists && bstate == BS.SweepT;
        for (int i = 0; i < 9; i++)
        {
            sweepSr[i].enabled = sweepWarn; if (!sweepWarn) continue; float a = Mathf.Lerp(a0, a1, i / 8f);
            sweepSr[i].transform.position = W(BX + Mathf.Cos(a) * SWR, BY + 30f + Mathf.Sin(a) * SWR * 0.8f, 1f); sweepSr[i].transform.localScale = Vector3.one * (56f * U / 1.18f); sweepSr[i].color = new Color(1f, 0.45f, 0.16f, 0.25f + 0.35f * Mathf.Clamp01(1f - bt / bT));
        }
        foreach (Ring w in rings) { w.sr.transform.position = W(w.x, w.y, 1.5f); w.sr.transform.localScale = Vector3.one * (w.r * U / 2.36f); w.sr.color = new Color(1f, 0.6f, 0.25f, 1f - w.r / w.max); }
        foreach (Rock r in rocks) { float k = 1f - r.t / r.T; r.mark.color = new Color(1f, 0.4f, 0.14f, 0.35f + 0.4f * k); r.inner.transform.localScale = Vector3.one * ((46f * (1f - k) + 4f) * U / 2.36f); r.inner.color = new Color(1f, 0.94f, 0.75f, 0.9f); }
        foreach (Rubble r in rubble) r.sr.color = new Color(1f, 1f, 1f, Mathf.Min(1f, r.life));
        foreach (Ball o in balls) { o.sr.transform.position = W(o.x, o.y, 26f); o.sr.transform.rotation = bill; o.sr.sortingOrder = Ord(o.y); o.blob.transform.position = W(o.x, o.y, 0.6f); }
        for (int i = 0; i < 3; i++) pillarGlowSr[i].color = new Color(1f, 0.67f, 0.24f, 0.8f * pillarGlow[i]);
        foreach (Prop p in props) { if (p.kind != 2) continue; p.sr.sprite = brazierFrames[Mathf.FloorToInt(t * 9f + p.ph) % 4]; Color c = p.glowB.color; c.a = 0.4f * (0.85f + 0.15f * Mathf.Sin(t * 11f + p.x)); p.glowB.color = c; }
        watchSr.enabled = hasWatch && !got && bdead > 1f; if (watchSr.enabled) { watchSr.transform.position = W(watchX, watchY, 10f); watchSr.sortingOrder = Ord(watchY); watchSr.transform.localScale = Vector3.one * (1f + 0.1f * Mathf.Sin(t * 5f)); }

        // particles, slash arcs, words
        for (int i = parts.Count - 1; i >= 0; i--)
        {
            Part p = parts[i]; p.l -= dt; p.x += p.vx * dt; p.y += p.vy * dt; p.z += p.vz * dt;
            if (p.g > 0f) { p.vz -= p.g * dt; if (p.z < 0f) { p.z = 0f; p.vz = 0f; p.vx *= 0.5f; p.vy *= 0.5f; } } else if (dt > 0f) { p.vx *= 0.96f; p.vy *= 0.96f; }
            if (p.l <= 0f) { Free(p.sr); parts.RemoveAt(i); continue; }
            p.sr.transform.position = W(p.x, p.y, p.z); p.sr.transform.rotation = bill; p.sr.sortingOrder = Ord(p.y) + 3;
            if (p.flash) { p.sr.transform.localScale = Vector3.one * (2f * p.s * U / 2.56f); p.sr.color = new Color(1f, 1f, 1f, Mathf.Clamp01(p.l / 0.2f) * 0.7f); }
            else { p.sr.transform.localScale = Vector3.one * (p.s * U); Color c = p.c; c.a *= Mathf.Min(1f, p.l * 2f); p.sr.color = c; }
        }
        for (int i = fxs.Count - 1; i >= 0; i--) { Fx f = fxs[i]; f.l -= dt; if (f.l <= 0f) { Free(f.sr); fxs.RemoveAt(i); continue; } f.sr.color = new Color(1f, 1f, 1f, f.a0 * f.l / f.max); }
        for (int i = words.Count - 1; i >= 0; i--) { words[i].l -= dt; words[i].z += 46f * dt; if (words[i].l <= 0f) words.RemoveAt(i); }
    }

    // ================================================================== thermal sight, HUD (all OnGUI)
    bool Project(float x, float y, float h, out Vector2 p, out float s)
    {
        Vector3 a = cam.WorldToScreenPoint(W(x, y, h)), b = cam.WorldToScreenPoint(W(x + 100f, y, h)); p = new Vector2(a.x, Screen.height - a.y); s = Mathf.Abs(b.x - a.x) / 100f; return a.z > 0f;
    }
    void Blob(float x, float y, float h, float r, float k, float a)
    {
        Vector2 p; float s; if (!Project(x, y, h, out p, out s)) return; float d = r * 2f * s; GUI.color = TC(k, a); GUI.DrawTexture(new Rect(p.x - d / 2f, p.y - d / 2f, d, d), sGlow.texture);
    }
    void Thermal()
    {
        bool inv = stage == 2 && phase == 3 && bdead <= 0f;
        GUI.color = inv ? new Color(0.55f, 0.2f, 0.03f, 0.62f) : new Color(0.03f, 0.05f, 0.3f, 0.8f); GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), whiteTex);
        Blob(px, py, 36, 46, 0.62f, 0.9f); foreach (Prop p in props) if (p.kind == 2) Blob(p.x, p.y, 70, 60, 0.9f, 0.9f);
        foreach (Min m in minions) { Blob(m.x, m.y, 36 + m.z, 40, 0.3f, 0.9f); Blob(m.x, m.y, 42 + m.z, 15, 1f, 0.9f); }
        foreach (Ember e in embers) Blob(e.x, e.y, e.h, 26, 1f, 0.9f);
        foreach (Sleeper s in sleepers) if (!s.awake) Blob(s.x, s.y, 8, 18, 0.95f, 0.9f);
        if (stage != 0 || lit > 0f)
        {
            Blob(BX, BY, 135, 150, inv ? 0.9f : 0.16f + Mathf.Max(heat[2], lit * 0.25f) * 0.82f, 0.85f); Blob(BX, BY, 50, 100, inv ? 0.85f : 0.22f, 0.6f);
            if (stage == 2 && bdead <= 0f) for (int s = 0; s < 2; s++) Blob(fist[s].x, fist[s].y, 30 + fist[s].up * 120, 70, inv ? 0.86f + heat[s] * 0.14f : 0.2f + heat[s] * 0.78f, 0.9f);
        }
        foreach (Rock r in rocks) Blob(r.x, r.y, 4, 50, 0.75f, 0.6f); foreach (Ball o in balls) Blob(o.x, o.y, 26, 34, 1f, 0.9f);
        if (stage == 2 && bdead <= 0f)
        {
            if (bstate == BS.Beam) for (float q = 60f; q < beamEnd; q += 45f) Blob(BX + Mathf.Cos(beamA) * q, BY + 70f + Mathf.Sin(beamA) * q, 10, 44, 1f, 0.8f);
            if (bstate == BS.Lanes || bstate == BS.LanesT) foreach (float a in lanes) for (float q = 80f; q < 900f; q += 70f) Blob(BX + Mathf.Cos(a) * q, BY + 70f + Mathf.Sin(a) * q, 6, 40, bstate == BS.Lanes ? 1f : 0.6f, 0.7f);
            // the third censer: only ever drawn here
            Fist f = fist[2]; Vector2 p; float s;
            if (f.st == 1 && Project(f.x, f.y, 0f, out p, out s)) { float k = Mathf.Clamp01(1f - f.t / f.T), w = (f.r + 16f) * 2f * s; GUI.color = new Color(1f, 0.86f - 0.5f * k, 0.35f, 0.55f + 0.3f * Mathf.Sin(tNow * 20f)); GUI.DrawTexture(new Rect(p.x - w / 2f, p.y - w * 0.3f, w, w * 0.6f), sMark.texture); }
            if (Project(f.x, f.y, 30f + f.up * 120f, out p, out s))
            {
                Vector2 top; float s2; if (Project(BX, BY, 186f * GS, out top, out s2)) { GUI.color = new Color(1f, 0.6f, 0.24f, 0.7f); for (int i = 1; i < 8; i++) { Vector2 q = Vector2.Lerp(top, p, i / 8f); GUI.DrawTexture(new Rect(q.x - 5f, q.y - 5f, 10f, 10f), sGlow.texture); } }
                float d = 84f * s; GUI.color = Color.white; GUI.DrawTexture(new Rect(p.x - d / 2f, p.y - d / 2f, d, d), censerFrames[f.heat > 0.6f ? 3 : 2].texture);
            }
            // the heart
            Vector2 hp; float hs;
            if (Project(hx, hy, hh, out hp, out hs))
            {
                float pul = 1f + 0.15f * Mathf.Sin(tNow * 10f);
                if (inv) { float d = 60f * hs * pul; GUI.color = new Color(0.04f, 0.08f, 0.4f, 1f); GUI.DrawTexture(new Rect(hp.x - d / 2f, hp.y - d / 2f, d, d), sGlow.texture); GUI.color = new Color(0.5f, 0.82f, 1f, 1f); GUI.DrawTexture(new Rect(hp.x - d * 0.3f, hp.y - d * 0.3f, d * 0.6f, d * 0.6f), sRing.texture); }
                else { float d = 96f * hs; GUI.color = new Color(1f, 0.9f, 0.5f, 1f); GUI.DrawTexture(new Rect(hp.x - d / 2f, hp.y - d / 2f, d, d), sGlow.texture); GUI.color = Color.white; float d2 = 42f * hs * pul; GUI.DrawTexture(new Rect(hp.x - d2 / 2f, hp.y - d2 / 2f, d2, d2), sRing.texture); }
            }
            if (bstate == BS.Exposed)
            {
                float gx, gy; HeartGround(out gx, out gy); if (Project(gx, gy, 44f, out hp, out hs)) { float d = 96f * hs * (1f + 0.12f * Mathf.Sin(tNow * 14f)); GUI.color = Color.white; GUI.DrawTexture(new Rect(hp.x - d / 2f, hp.y - d / 2f, d, d), Spr("emberheart", 64, 64, 100f).texture); }
            }
        }
        GUI.color = Color.white;
    }
    void OnGUI()
    {
        if (mode == Mode.Idle || cam == null) return;
        if (capStyle == null)
        {
            capStyle = new GUIStyle(GUI.skin.label); capStyle.alignment = TextAnchor.MiddleCenter; capStyle.fontSize = 16; capStyle.normal.textColor = Hex("#e6dcd2"); capStyle.wordWrap = false;
            bigStyle = new GUIStyle(capStyle); bigStyle.fontSize = 34; bigStyle.normal.textColor = Hex("#ffd9a0"); wordStyle = new GUIStyle(capStyle);
            if (font != null) { capStyle.font = font; wordStyle.font = font; } if (bigFont != null) bigStyle.font = bigFont; else if (font != null) bigStyle.font = font;
        }
        float k = Screen.height / 600f, offX = (Screen.width - 960f * k) / 2f; Matrix4x4 old = GUI.matrix;
        if (ir) Thermal();
        // floating words
        foreach (Word w in words)
        {
            Vector2 p; float s; if (!Project(w.x, w.y, w.z, out p, out s)) continue; wordStyle.fontSize = Mathf.Max(8, Mathf.RoundToInt(w.size * s)); Rect r = new Rect(p.x - 200f, p.y - 20f, 400f, 40f); float a = Mathf.Min(1f, w.l * 2f);
            wordStyle.normal.textColor = new Color(0.05f, 0.02f, 0.03f, a); GUI.Label(new Rect(r.x + 2, r.y + 2, r.width, r.height), w.text, wordStyle); GUI.Label(new Rect(r.x - 2, r.y - 1, r.width, r.height), w.text, wordStyle);
            Color c = w.c; c.a = a; wordStyle.normal.textColor = c; GUI.Label(r, w.text, wordStyle);
        }
        GUI.color = Color.white; GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Spr("vignette", 256, 256, 100f).texture, ScaleMode.StretchToFill);
        if (irFlip > 0f) { GUI.color = new Color(1f, 1f, 1f, irFlip / 0.18f * 0.35f); GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), whiteTex); GUI.color = Color.white; }
        GUI.matrix = Matrix4x4.TRS(new Vector3(offX, 0f, 0f), Quaternion.identity, new Vector3(k, k, 1f));
        Texture hOn = Spr("hp", 16, 16, 100f).texture, hOff = Spr("hp_empty", 16, 16, 100f).texture;
        for (int i = 0; i < MaxHp; i++) GUI.DrawTexture(new Rect(18 + i * 24, 18, 32, 32), i < php ? hOn : hOff);
        capStyle.alignment = TextAnchor.MiddleRight; capStyle.normal.textColor = ir ? Hex("#ffd34a") : Hex("#b9a898");
        GUI.Label(new Rect(630, 22, 300, 24), ir ? (stage == 2 && phase == 3 ? "THERMAL · INVERTED" : "THERMAL ON") : "Q: THERMAL", capStyle); capStyle.alignment = TextAnchor.MiddleCenter; capStyle.normal.textColor = Hex("#e6dcd2");
        if (mode == Mode.Play && stage == 0) GUI.Label(new Rect(280, 22, 400, 24), "SKELETONS  " + killed + " / 9", capStyle);
        else if (mode == Mode.Play && stage == 2)
        {
            GUI.Label(new Rect(280, 12, 400, 22), "THE NINE", capStyle);
            for (int i = 0; i < 9; i++) { GUI.color = i < bhp ? new Color(1f, 0.7f, 0.28f) : new Color(0.23f, 0.11f, 0.09f); GUI.DrawTexture(new Rect(480 - 96 + i * 24 - 8, 38, 16, 16), sGlow.texture); GUI.DrawTexture(new Rect(480 - 96 + i * 24 - 5, 41, 10, 10), whiteTex); }
            GUI.color = Color.white;
        }
        if (mode == Mode.Play)
        {
            string h = "";
            if (stage == 0 && T < 14f) h = "Slash to drop a skeleton. Press Q for thermal: each one carries an ember.";
            else if (stage == 2 && stageTime < 14f) h = "Only the heart takes damage. Thermal (Q) shows where it is hiding.";
            else if (stage == 2 && irUses == 0 && stageTime > 14f) h = "Stuck? Press Q for thermal sight.";
            else if (stage == 2 && bstate == BS.Exposed) h = ir ? "The heart is out. Cut it." : "An opening. Thermal sight (Q) shows where the heart is.";
            if (h.Length > 0) { float w = capStyle.CalcSize(new GUIContent(h)).x + 30f; Rect r = new Rect(480f - w / 2f, 558f, w, 30f); GUI.color = new Color(0.03f, 0.012f, 0.012f, 0.8f); GUI.DrawTexture(r, whiteTex); GUI.color = Color.white; GUI.Label(r, h, capStyle); }
        }
        if (bannerT > 0f)
        {
            float a = Mathf.Min(1f, bannerT * 2f); GUI.color = new Color(0.03f, 0.012f, 0.012f, 0.75f * a); GUI.DrawTexture(new Rect(0, 108, 960, 96), whiteTex); GUI.color = new Color(1f, 1f, 1f, a);
            GUI.Label(new Rect(0, 118, 960, 46), banner, bigStyle); GUI.Label(new Rect(0, 168, 960, 26), bannerSub, capStyle); GUI.color = Color.white;
        }
        if (capT > 0f && cap.Length > 0) { float w = capStyle.CalcSize(new GUIContent(cap)).x + 40f; Rect r = new Rect(480f - w / 2f, 552f, w, 36f); GUI.color = new Color(0.03f, 0.012f, 0.012f, 0.85f); GUI.DrawTexture(r, whiteTex); GUI.color = Color.white; GUI.Label(r, cap, capStyle); }
        if (mode == Mode.Dead)
        {
            GUI.matrix = old; GUI.color = new Color(0.024f, 0.008f, 0.008f, 0.72f); GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), whiteTex);
            GUI.matrix = Matrix4x4.TRS(new Vector3(offX, 0f, 0f), Quaternion.identity, new Vector3(k, k, 1f)); GUI.color = Color.white; GUI.Label(new Rect(0, 236, 960, 50), "CRUSHED", bigStyle); GUI.Label(new Rect(0, 296, 960, 30), "press SPACE or click to try again", capStyle);
        }
        GUI.matrix = old;
        if (fadeBlack > 0f) { GUI.color = new Color(0f, 0f, 0f, fadeBlack); GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), blackTex); GUI.color = Color.white; }
    }
}
