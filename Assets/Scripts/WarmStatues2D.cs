using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class WarmStatues2D : MonoBehaviour
{
    [Header("Return to the 3D world")]
    public string returnScene = "FourfoldCitadel_WithOurStuff";

    [Header("Next minigame (played before the final line; leave empty to skip it)")]
    public string bossScene = "FurnaceHeart2D";

    // set when leaving for the boss scene; the boss loads this scene again and only the final line is shown
    static bool finaleOnly;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { finaleOnly = false; catches = 0; }

    [Header("Easy mode after repeated catches")]
    public bool easierAfterCatches = true;
    public int catchesBeforeEasy = 2;
    [Tooltip("In easy mode statues creep this many times slower and the time limit is this many times longer.")]
    public float easySlowdown = 3f;

    static int catches;                     // kept between tries until the puzzle is solved
    bool Easy => easierAfterCatches && catches >= catchesBeforeEasy;
    float Slow => Easy ? Mathf.Max(1f, easySlowdown) : 1f;

    [Header("Ending")]
    public string crackLine = "THE LAST STATUE CRACKS IN THE HEAT.";
    public string bodyLine = "THERE IS A BODY INSIDE. IT IS WEARING YOUR HELMET.";
    [TextArea(2, 5)] public string finalLine = "9 SILHOUETTES, ALL CARRYING THE SAME SHADOWS.";
    public int finalFontSize = 44;
    public Color finalColor = new Color(1f, 0.16f, 0.12f, 1f);
    public float finalFadeIn = 0.8f;
    public float finalHold = 3.5f;
    public float finalFadeOut = 1.2f;

    [Header("Rules")]
    [Tooltip("Off: stone statues still shuffle while the goggles are on, but never lunge at or catch the player.")]
    public bool statuesAttack = false;
    public float timeLimit = 60f;
    public float[] creepIntervals = { 1.15f, 0.9f, 0.7f };
    [Range(0f, 1f)] public float musicVolume = 0.4f;
    public float holdRepeatDelay = 0.35f;

    [Header("Goggle sight")]
    public float revealTiles = 3f;
    public float revealFadeTiles = 1f;
    [Range(0f, 1f)] public float rememberFoundAlpha = 0f;

    [Header("2.5D view")]
    public bool perspectiveView = true;
    public float floorTilt = 42f;
    public float cameraFov = 38f;
    [Range(0.4f, 1f)] public float viewWidth = 0.74f;
    public float followSmooth = 0.35f;
    public float lookAhead = 36f;
    public bool colorGrade = true;

    const float W = 1280f, H = 720f, CX = 640f;
    const float WallH = 122f, SideH = 70f;
    static readonly float[,] PropRects =
    {
        { 1124, 477, 1178, 561 }, { 104, 481, 158, 609 }, { 30, 542, 90, 644 }, { 1195, 542, 1255, 654 },
        { 172, 557, 249, 612 }, { 854, 603, 907, 669 }, { 374, 604, 426, 669 }, { 259, 607, 356, 692 },
        { 939, 612, 1071, 697 }, { 599, 629, 696, 702 }, { 520, 633, 591, 680 }
    };
    const int COLS = 9, ROWS = 6;
    const float TW = 84f, TH = 52f, Y0 = 238f, PSC = 0.27f;
    const float X0 = CX - COLS * TW / 2f;
    const string Dir = "WarmStatues/";

    class Mover
    {
        public int c, r, fc, fr;
        public float mt = 1f;
        public bool hasBack;
        public int backC, backR;
    }

    class Statue : Mover
    {
        public bool warm, locked;
        public float crack;
        public RectTransform root;
        public Image pedestal, body, crackImg, lockGlow, deadT, heat;
        public Image[] eyes = new Image[2];
        public Image[] trail = new Image[6];
        public Image[] chunks = new Image[5];
    }

    class Plate
    {
        public int c, r;
        public bool warm, done, seen;
        public float vent = -9f;
        public Image outline, heat;
        public Image[] flames = new Image[3];
    }

    class PlayerState : Mover
    {
        public int face = 1, walk;
        public RectTransform root;
        public Image img;
    }

    class Ember
    {
        public float x, y, sp, ph, s;
        public Image img;
    }

    string phase;
    float pt, t, pulse, black, msgT, creepAt, startT, beatAt, heldFor;
    bool thermal, e0, e1, loading;
    string msg = "";
    PlayerState pl;
    readonly List<Statue> statues = new List<Statue>();
    readonly List<Plate> plates = new List<Plate>();
    Statue last;
    readonly List<Ember> embers = new List<Ember>();

    Font font;
    RectTransform stage, world, actors, platesRoot, emberRoot;
    Image back, front, vignette, thermalOverlay, danger, frame, seam, blackImg, titleDim, barBg, barFill;
    readonly List<Image> grid = new List<Image>();
    Text sightLabel, platesLabel, msgLabel, warnLabel, warn2Label, caughtLabel, pressLabel, controlsLabel;
    readonly List<Text> titleLabels = new List<Text>();
    JiggleLine titleJiggle, finalJiggle;
    Sprite sBack, sBackT, sFront, sFrontT, sStone, sBody, sDeadT, sPed, sCrack, sChunk, sPlateW, sPlateC, sDoneW, sDoneC, sHeat, sFlame, sDot;
    Sprite[] sPlayer = new Sprite[3], sHot = new Sprite[3];
    AudioSource sfx, bgm;
    AudioClip aThump, aBeat, aBoom, aScrape, aBell, aScreech;

    Camera cam;
    Canvas worldCanvas;
    Image wall, side;
    Image[] props;
    Sprite sFloor, sFloorT, sWall, sWallT, sSide, sSideT;
    Sprite[] sProp, sPropT;
    readonly Image[] bars = new Image[4];
    Vector3 camVel;
    bool camInit;

    float Tilt { get { return perspectiveView ? floorTilt : 0f; } }
    Quaternion Upright { get { return Quaternion.Euler(-Tilt, 0f, 0f); } }

    void Start()
    {
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        SetupCamera();
        LoadAssets();
        BuildUI();
        ResetGame();
        if (finaleOnly)
        {
            // back from Furnace Heart: go straight to the final line, then on to the 3D world as before
            finaleOnly = false;
            phase = "finale"; pt = 0f; black = 1f;
            finalJiggle.SetText(finalLine, finalFontSize, finalColor);
        }
    }

    void SetupCamera()
    {
        cam = Camera.main;
        if (cam == null)
        {
            var g = new GameObject("Main Camera");
            g.tag = "MainCamera";
            cam = g.AddComponent<Camera>();
            g.AddComponent<AudioListener>();
        }
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        cam.cullingMask = 1;
        cam.orthographic = false;
        cam.fieldOfView = cameraFov;
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = 100f;
        cam.transform.rotation = Quaternion.identity;
        if (!colorGrade) return;
        UnityEngine.Rendering.Universal.CameraExtensions.GetUniversalAdditionalCameraData(cam).renderPostProcessing = true;
        var vg = new GameObject("Grade");
        vg.transform.SetParent(transform, false);
        var vol = vg.AddComponent<UnityEngine.Rendering.Volume>();
        vol.isGlobal = true;
        var prof = ScriptableObject.CreateInstance<UnityEngine.Rendering.VolumeProfile>();
        var bloom = prof.Add<UnityEngine.Rendering.Universal.Bloom>(true);
        bloom.threshold.Override(0.82f);
        bloom.intensity.Override(0.9f);
        bloom.scatter.Override(0.65f);
        var ca = prof.Add<UnityEngine.Rendering.Universal.ColorAdjustments>(true);
        ca.saturation.Override(18f);
        ca.contrast.Override(12f);
        ca.colorFilter.Override(new Color(1f, 0.9f, 0.94f));
        vol.profile = prof;
    }

    Sprite Sub(string file, float x, float yTop, float w, float h, Vector2 pivot)
    {
        Texture2D tex = Resources.Load<Texture2D>(Dir + file);
        if (tex == null) return null;
        float sx = tex.width / W, sy = tex.height / H;
        return Sprite.Create(tex, new Rect(x * sx, (H - yTop - h) * sy, w * sx, h * sy), pivot, 100f, 0, SpriteMeshType.FullRect);
    }

    Sprite Spr(string file, Vector2 pivot)
    {
        Texture2D tex = Resources.Load<Texture2D>(Dir + file);
        if (tex == null) { Debug.LogError("WarmStatues2D: missing Resources/" + Dir + file); return null; }
        return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), pivot, 100f, 0, SpriteMeshType.FullRect);
    }

    void LoadAssets()
    {
        Vector2 c = new Vector2(0.5f, 0.5f);
        Vector2 feet = new Vector2(0.5f, 12f / 512f);
        sBack = Spr("back", c); sBackT = Spr("back_thermal", c);
        sFront = Spr("front", c); sFrontT = Spr("front_thermal", c);
        sFloor = Sub("back", 0f, WallH, W, H - WallH, new Vector2(0f, 1f));
        sFloorT = Sub("back_thermal", 0f, WallH, W, H - WallH, new Vector2(0f, 1f));
        sWall = Sub("back", 0f, 0f, W, WallH, Vector2.zero);
        sWallT = Sub("back_thermal", 0f, 0f, W, WallH, Vector2.zero);
        sSide = Sub("back", 0f, H - SideH, W, SideH, new Vector2(0f, 1f));
        sSideT = Sub("back_thermal", 0f, H - SideH, W, SideH, new Vector2(0f, 1f));
        int pc = PropRects.GetLength(0);
        sProp = new Sprite[pc];
        sPropT = new Sprite[pc];
        for (int i = 0; i < pc; i++)
        {
            float px = PropRects[i, 0], py = PropRects[i, 1], pw = PropRects[i, 2] - px, ph = PropRects[i, 3] - py;
            sProp[i] = Sub("front", px, py, pw, ph, new Vector2(0.5f, 0f));
            sPropT[i] = Sub("front_thermal", px, py, pw, ph, new Vector2(0.5f, 0f));
        }
        sStone = Spr("stone", feet); sBody = Spr("body", feet); sDeadT = Spr("deadt", feet);
        sPlayer[0] = Spr("player_idle", feet); sPlayer[1] = Spr("player_walk1", feet); sPlayer[2] = Spr("player_walk2", feet);
        sHot[0] = Spr("hot0", feet); sHot[1] = Spr("hot1", feet); sHot[2] = Spr("hot2", feet);
        sPed = Spr("pedestal", new Vector2(0.5f, 0.35f));
        sCrack = Spr("crack", new Vector2(0.5f, 1f - 4f / 140f));
        sChunk = Spr("chunk", c);
        sPlateW = Spr("plate_warm", c); sPlateC = Spr("plate_cold", c);
        sDoneW = Spr("plate_done_warm", c); sDoneC = Spr("plate_done_cold", c);
        sHeat = Spr("heat", c); sFlame = Spr("flame", new Vector2(0.5f, 8f / 72f)); sDot = Spr("dot", c);
        font = Resources.Load<Font>(Dir + "DotGothic16");
        if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        sfx = gameObject.AddComponent<AudioSource>();
        sfx.playOnAwake = false;
        bgm = gameObject.AddComponent<AudioSource>();
        bgm.playOnAwake = false;
        bgm.loop = true;
        bgm.volume = musicVolume;
        bgm.clip = Resources.Load<AudioClip>(Dir + "WarmStatuesBGM");
        aThump = Resources.Load<AudioClip>(Dir + "thump");
        aBeat = Resources.Load<AudioClip>(Dir + "beat");
        aBoom = Resources.Load<AudioClip>(Dir + "boom");
        aScrape = Resources.Load<AudioClip>(Dir + "scrape");
        aBell = Resources.Load<AudioClip>(Dir + "bell");
        aScreech = Resources.Load<AudioClip>(Dir + "screech");
    }

    RectTransform Node(string name, Transform parent)
    {
        var g = new GameObject(name, typeof(RectTransform));
        g.transform.SetParent(parent, false);
        var rt = (RectTransform)g.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = Vector2.zero;
        return rt;
    }

    Image Img(string name, Transform parent, Sprite s, float w, float h)
    {
        RectTransform rt = Node(name, parent);
        var im = rt.gameObject.AddComponent<Image>();
        im.sprite = s;
        im.raycastTarget = false;
        rt.sizeDelta = new Vector2(w, h);
        if (s != null) rt.pivot = new Vector2(s.pivot.x / s.rect.width, s.pivot.y / s.rect.height);
        return im;
    }

    Image Full(string name, Transform parent, Sprite s, Color c)
    {
        var im = Img(name, parent, s, W, H);
        RectTransform rt = im.rectTransform;
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = Vector2.zero;
        im.color = c;
        return im;
    }

    static void Put(Graphic g, float x, float y) { g.rectTransform.anchoredPosition = new Vector2(x, -y); }

    Text Label(string name, Transform parent, int size, TextAnchor align, Color col)
    {
        RectTransform rt = Node(name, parent);
        var tx = rt.gameObject.AddComponent<Text>();
        tx.font = font;
        tx.fontSize = size;
        tx.alignment = align;
        tx.color = col;
        tx.horizontalOverflow = HorizontalWrapMode.Overflow;
        tx.verticalOverflow = VerticalWrapMode.Overflow;
        tx.raycastTarget = false;
        rt.sizeDelta = new Vector2(1200f, size * 2f);
        rt.pivot = align == TextAnchor.MiddleLeft ? new Vector2(0f, 0.5f) : align == TextAnchor.MiddleRight ? new Vector2(1f, 0.5f) : new Vector2(0.5f, 0.5f);
        var o = rt.gameObject.AddComponent<Outline>();
        o.effectColor = new Color(0.02f, 0.02f, 0.05f, 1f);
        o.effectDistance = new Vector2(2.5f, -2.5f);
        var o2 = rt.gameObject.AddComponent<Outline>();
        o2.effectColor = new Color(0.02f, 0.02f, 0.05f, 1f);
        o2.effectDistance = new Vector2(-2f, 2f);
        return tx;
    }

    void PutLabel(Text tx, float x, float baselineY) { tx.rectTransform.anchoredPosition = new Vector2(x, -(baselineY - tx.fontSize * 0.36f)); }

    void BuildUI()
    {
        var cgo = new GameObject("Warm Statues UI");
        cgo.transform.SetParent(transform, false);
        var canvas = cgo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;

        Vector2[] bmin = { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0f) };
        Vector2[] bmax = { new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f) };
        Vector2[] bpiv = { new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0f) };
        for (int i = 0; i < 4; i++)
        {
            var bg = new GameObject("Letterbox", typeof(RectTransform));
            bg.transform.SetParent(cgo.transform, false);
            bars[i] = bg.AddComponent<Image>();
            bars[i].color = Color.black;
            bars[i].raycastTarget = false;
            var bgr = (RectTransform)bg.transform;
            bgr.anchorMin = bmin[i]; bgr.anchorMax = bmax[i]; bgr.pivot = bpiv[i];
            bgr.anchoredPosition = Vector2.zero;
            bgr.sizeDelta = Vector2.zero;
        }

        stage = (RectTransform)new GameObject("Stage", typeof(RectTransform)).transform;
        stage.SetParent(cgo.transform, false);
        stage.anchorMin = stage.anchorMax = new Vector2(0.5f, 0.5f);
        stage.pivot = new Vector2(0.5f, 0.5f);
        stage.sizeDelta = new Vector2(W, H);
        stage.gameObject.AddComponent<RectMask2D>();

        var wgo = new GameObject("Warm Statues World", typeof(RectTransform));
        wgo.transform.SetParent(transform, false);
        worldCanvas = wgo.AddComponent<Canvas>();
        worldCanvas.renderMode = RenderMode.WorldSpace;
        worldCanvas.worldCamera = cam;
        var wrt = (RectTransform)wgo.transform;
        wrt.sizeDelta = new Vector2(W, H);
        wrt.pivot = new Vector2(0.5f, 0.5f);
        wrt.position = Vector3.zero;
        wrt.rotation = Quaternion.Euler(Tilt, 0f, 0f);
        wrt.localScale = Vector3.one * 0.01f;

        world = (RectTransform)new GameObject("World", typeof(RectTransform)).transform;
        world.SetParent(wgo.transform, false);
        world.anchorMin = Vector2.zero; world.anchorMax = Vector2.one; world.offsetMin = Vector2.zero; world.offsetMax = Vector2.zero;

        side = Img("Platform side", world, sSide, W, SideH);
        side.rectTransform.pivot = new Vector2(0f, 1f);
        side.rectTransform.anchoredPosition = new Vector2(0f, -H);
        side.rectTransform.localRotation = Upright;
        side.color = new Color(0.32f, 0.26f, 0.27f, 1f);
        wall = Img("Back wall", world, sWall, W, WallH);
        wall.rectTransform.pivot = Vector2.zero;
        wall.rectTransform.anchoredPosition = new Vector2(0f, -WallH);
        wall.rectTransform.localRotation = Upright;
        back = Img("Room floor", world, sFloor, W, H - WallH);
        back.rectTransform.pivot = new Vector2(0f, 1f);
        back.rectTransform.anchoredPosition = new Vector2(0f, -WallH);
        seam = Img("Door seam", wall.transform, null, 4f, 86f);
        seam.rectTransform.anchorMin = seam.rectTransform.anchorMax = new Vector2(0f, 1f);
        seam.rectTransform.pivot = new Vector2(0.5f, 1f);
        Put(seam, CX, 36f);

        for (int c = 0; c <= COLS; c++)
        {
            var l = Img("Grid", world, null, 2f, ROWS * TH);
            l.rectTransform.pivot = new Vector2(0.5f, 1f);
            Put(l, X0 + c * TW, Y0);
            grid.Add(l);
        }
        for (int r = 0; r <= ROWS; r++)
        {
            var l = Img("Grid", world, null, COLS * TW, 2f);
            l.rectTransform.pivot = new Vector2(0f, 0.5f);
            Put(l, X0, Y0 + r * TH);
            grid.Add(l);
        }

        platesRoot = Node("Plates", world);
        platesRoot.anchoredPosition = Vector2.zero;
        actors = Node("Actors", world);
        actors.anchoredPosition = Vector2.zero;
        props = new Image[sProp.Length];
        for (int i = 0; i < props.Length; i++)
        {
            float pw = PropRects[i, 2] - PropRects[i, 0], ph = PropRects[i, 3] - PropRects[i, 1];
            props[i] = Img("Prop", world, sProp[i], pw, ph);
            Put(props[i], (PropRects[i, 0] + PropRects[i, 2]) * 0.5f, PropRects[i, 3]);
            props[i].rectTransform.localRotation = Upright;
        }
        vignette = Full("Vignette", stage, Spr("vignette", new Vector2(0.5f, 0.5f)), Color.white);
        emberRoot = Node("Embers", world);
        emberRoot.anchoredPosition = Vector2.zero;
        for (int i = 0; i < 46; i++)
        {
            var e = new Ember { x = Random.value * W, y = Random.value * H, sp = 14f + Random.value * 30f, ph = Random.value * 9f, s = 1.5f + Random.value * 2f };
            e.img = Img("Ember", emberRoot, null, e.s, e.s);
            embers.Add(e);
        }
        thermalOverlay = Full("Thermal overlay", stage,Spr("thermal_overlay", new Vector2(0.5f, 0.5f)), Color.white);

        danger = Full("Danger", stage, Spr("danger_vignette", new Vector2(0.5f, 0.5f)), new Color(0.86f, 0.04f, 0.04f, 0f));
        frame = Full("Frame", stage, Spr("frame", new Vector2(0.5f, 0.5f)), Color.white);

        Color cream = Hex("#efe6cf");
        sightLabel = Label("Sight", stage, 22, TextAnchor.MiddleLeft, cream);
        PutLabel(sightLabel, 70f, 84f);
        platesLabel = Label("Plates", stage, 22, TextAnchor.MiddleRight, cream);
        PutLabel(platesLabel, W - 70f, 84f);
        warnLabel = Label("Warning", stage, 28, TextAnchor.MiddleCenter, cream);
        warn2Label = Label("Warning 2", stage, 24, TextAnchor.MiddleCenter, cream);
        warn2Label.text = "TAKE THE GOGGLES OFF  [G]";
        PutLabel(warn2Label, CX, 196f);
        barBg = Img("Bar bg", stage, null, 368f, 18f);
        barBg.rectTransform.pivot = new Vector2(0f, 1f);
        barBg.color = Hex("#05050c");
        Put(barBg, CX - 184f, 214f);
        barFill = Img("Bar", stage, null, 360f, 10f);
        barFill.rectTransform.pivot = new Vector2(0f, 1f);
        barFill.color = Hex("#ff3b30");
        Put(barFill, CX - 180f, 218f);
        caughtLabel = Label("Caught", stage, 52, TextAnchor.MiddleCenter, Hex("#ff3b30"));
        caughtLabel.text = "THE STATUE CAUGHT YOU";
        PutLabel(caughtLabel, CX, H / 2f - 10f);
        msgLabel = Label("Message", stage, 24, TextAnchor.MiddleCenter, cream);
        PutLabel(msgLabel, CX, H - 64f);
        controlsLabel = Label("Controls", stage, 16, TextAnchor.MiddleCenter, new Color(cream.r, cream.g, cream.b, 0.75f));
        controlsLabel.text = "[WASD] MOVE     [G] GOGGLES     [R] RESTART";
        PutLabel(controlsLabel, CX, 84f);

        titleDim = Full("Title dim", stage, null, new Color(0f, 0f, 0f, 0.6f));
        AddTitle("THREE STATUES. THREE PLATES YOU CANNOT SEE.", 230f, 36);
        AddTitle("THE GOGGLES SHOW THE PLATES, AND WHICH STATUE IS STILL WARM.", 296f, 22);
        AddTitle("THE STONE ONES MOVE WHILE THE GOGGLES ARE ON.", 430f, 22);
        AddTitle("[WASD] WALK AND PUSH     [G] GOGGLES     [R] RESTART", 476f, 18);
        titleJiggle = JiggleLine.Create(stage, font, "WARM ON WARM. COLD ON COLD.", 30, Hex("#ffb040"), new Vector2(CX, 366f), 1100f);
        pressLabel = Label("Press", stage, 24, TextAnchor.MiddleCenter, cream);
        pressLabel.text = "PRESS ANY KEY TO BEGIN";
        PutLabel(pressLabel, CX, H - 92f);

        blackImg = Full("Black", stage, null, new Color(0f, 0f, 0f, 0f));
        finalJiggle = JiggleLine.Create(stage, font, finalLine, finalFontSize, finalColor, new Vector2(CX, H / 2f), 1100f);
        finalJiggle.SetAlpha(0f);
    }

    void AddTitle(string s, float y, int size)
    {
        var l = Label("Title", stage, size, TextAnchor.MiddleCenter, Hex("#efe6cf"));
        l.text = s;
        PutLabel(l, CX, y);
        titleLabels.Add(l);
    }

    static Color Hex(string h) { Color c; ColorUtility.TryParseHtmlString(h, out c); return c; }

    static float Tx(float c) { return X0 + (c + 0.5f) * TW; }
    static float Ty(float r) { return Y0 + (r + 0.5f) * TH; }

    void ResetGame()
    {
        foreach (Statue s in statues)
        {
            if (s.root != null) Destroy(s.root.gameObject);
            foreach (Image tr in s.trail) if (tr != null) Destroy(tr.gameObject);
        }
        foreach (Plate p in plates)
        {
            if (p.outline != null) Destroy(p.outline.gameObject);
            if (p.heat != null) Destroy(p.heat.gameObject);
            foreach (Image f in p.flames) if (f != null) Destroy(f.gameObject);
        }
        if (pl != null && pl.root != null) Destroy(pl.root.gameObject);
        statues.Clear();
        plates.Clear();

        phase = "title"; pt = 0f; thermal = false; pulse = 0f; msg = ""; msgT = -10f; black = 0f;
        e0 = e1 = false; last = null; creepAt = 0f; beatAt = 0f;

        int[,] sdef = { { 2, 2, 0 }, { 6, 2, 0 }, { 4, 1, 1 } };
        for (int i = 0; i < 3; i++) statues.Add(MakeStatue(sdef[i, 0], sdef[i, 1], sdef[i, 2] == 1));
        int[,] pdef = { { 1, 4, 0 }, { 7, 4, 0 }, { 4, 3, 1 } };
        for (int i = 0; i < 3; i++) plates.Add(MakePlate(pdef[i, 0], pdef[i, 1], pdef[i, 2] == 1));

        pl = new PlayerState { c = 4, r = 5, fc = 4, fr = 5 };
        pl.root = Node("Player", actors);
        pl.root.localRotation = Upright;
        float sz = 512f * PSC;
        pl.img = Img("Sprite", pl.root, sPlayer[0], sz, sz);
        pl.img.rectTransform.anchoredPosition = Vector2.zero;
    }

    Statue MakeStatue(int c, int r, bool warm)
    {
        var s = new Statue { c = c, r = r, fc = c, fr = r, warm = warm };
        s.root = Node("Statue", actors);
        s.root.localRotation = Upright;
        float sz = 512f * PSC;
        s.lockGlow = Img("Lock glow", s.root, sDot, 68f, 24f);
        s.lockGlow.color = new Color(1f, 0.67f, 0.24f, 0.18f);
        s.lockGlow.rectTransform.anchoredPosition = new Vector2(0f, 4f);
        s.lockGlow.rectTransform.localRotation = Quaternion.Inverse(Upright);
        s.pedestal = Img("Pedestal", s.root, sPed, 80f, 40f);
        s.pedestal.rectTransform.anchoredPosition = Vector2.zero;
        s.body = Img("Stone", s.root, sStone, sz, sz);
        s.body.rectTransform.anchoredPosition = new Vector2(0f, 12f);
        s.crackImg = Img("Crack", s.root, sCrack, 48f, 140f);
        s.crackImg.type = Image.Type.Filled;
        s.crackImg.fillMethod = Image.FillMethod.Vertical;
        s.crackImg.fillOrigin = (int)Image.OriginVertical.Top;
        s.crackImg.rectTransform.anchoredPosition = new Vector2(0f, 128f);
        for (int i = 0; i < 5; i++) s.chunks[i] = Img("Chunk", s.root, sChunk, 40f, 32f);
        s.deadT = Img("Warm glow body", s.root, sDeadT, sz, sz);
        s.deadT.rectTransform.anchoredPosition = new Vector2(0f, 8f);
        s.heat = Img("Heat", s.root, sHeat, 80f, 80f);
        s.heat.rectTransform.anchoredPosition = new Vector2(0f, 78f);
        for (int i = 0; i < 2; i++) s.eyes[i] = Img("Eye", s.root, sDot, 6f, 6f);
        for (int i = 0; i < s.trail.Length; i++) s.trail[i] = Img("Trail", actors, sDot, 5f, 5f);
        return s;
    }

    Plate MakePlate(int c, int r, bool warm)
    {
        var p = new Plate { c = c, r = r, warm = warm };
        p.heat = Img("Plate heat", platesRoot, sHeat, 116f, 116f);
        Put(p.heat, Tx(c), Ty(r));
        p.outline = Img("Plate", platesRoot, sPlateW, 96f, 64f);
        Put(p.outline, Tx(c), Ty(r));
        for (int i = 0; i < 3; i++)
        {
            p.flames[i] = Img("Vent flame", platesRoot, sFlame, 64f, 72f);
            Put(p.flames[i], Tx(c) + (i - 1) * 18f, Ty(r) + 6f);
            p.flames[i].rectTransform.localRotation = Upright;
        }
        return p;
    }

    Statue StAt(int c, int r) { foreach (Statue s in statues) if (s.c == c && s.r == r) return s; return null; }
    Plate PlAt(int c, int r) { foreach (Plate p in plates) if (p.c == c && p.r == r) return p; return null; }
    static bool Inside(int c, int r) { return c >= 0 && c < COLS && r >= 0 && r < ROWS; }
    static bool Interior(int c, int r) { return c >= 1 && c < COLS - 1 && r >= 1 && r < ROWS - 1; }
    static void Slide(Mover o, int c, int r) { o.fc = o.c; o.fr = o.r; o.c = c; o.r = r; o.mt = 0f; }
    void Say(string m) { msg = m; msgT = t; }

    void Play(AudioClip clip, float vol) { if (clip != null) sfx.PlayOneShot(clip, vol); }
    void Scrape(float g) { Play(aScrape, g / 0.3f * 0.8f); }
    void Boom(float g) { Play(aBoom, g / 0.5f); }

    void Begin()
    {
        if (bgm.clip != null && !bgm.isPlaying) bgm.Play();
        if (phase == "title")
        {
            phase = "play"; pt = 0f; startT = t;
            Say(Easy ? "THE STATUES GROW SLUGGISH. TAKE YOUR TIME." : "FIND THE PLATES. PUSH THE STATUES.");
        }
    }

    void Move(int dx, int dy)
    {
        if (phase == "title") { Begin(); return; }
        if (phase != "play") return;
        if (pl.mt < 1f) return;
        if (dx != 0) pl.face = dx;
        int nc = pl.c + dx, nr = pl.r + dy;
        if (!Inside(nc, nr)) return;
        Statue s = StAt(nc, nr);
        if (s != null)
        {
            if (s.locked) { Say("IT WILL NOT MOVE AGAIN."); return; }
            int sc = nc + dx, sr = nr + dy;
            if (!Interior(sc, sr) || StAt(sc, sr) != null) { Say("IT WILL NOT GO THAT WAY."); Scrape(0.12f); return; }
            Slide(s, sc, sr);
            Scrape(0.3f);
            Plate p = PlAt(sc, sr);
            if (p != null && !p.done)
            {
                if (p.warm == s.warm)
                {
                    p.done = true; s.locked = true; last = s;
                    Play(aBell, 1f);
                    bool all = true;
                    foreach (Plate q in plates) if (!q.done) all = false;
                    Say(all ? "" : (s.warm ? "THE WARM ONE SETTLES. SOMETHING INSIDE IT SIGHS." : "STONE ON COLD STONE. IT LOCKS."));
                }
                else
                {
                    p.vent = t;
                    Boom(0.35f);
                    Say("WRONG PLATE. THE FLOOR SPITS IT BACK.");
                    s.hasBack = true; s.backC = nc; s.backR = nr;
                }
            }
        }
        Slide(pl, nc, nr);
        pl.walk++;
    }

    void Creep()
    {
        bool moved = false;
        foreach (Statue s in statues)
        {
            if (s.warm || s.locked) continue;
            int dx = pl.c - s.c, dy = pl.r - s.r;
            if (Mathf.Abs(dx) + Mathf.Abs(dy) <= 1)
            {
                if (!statuesAttack) continue;   // beside the player: it waits instead of attacking
                Slide(s, pl.c, pl.r);
                Play(aScreech, 1f);
                Caught();
                return;
            }
            int[][] opts = Mathf.Abs(dx) >= Mathf.Abs(dy)
                ? new[] { new[] { Sign(dx), 0 }, new[] { 0, Sign(dy) } }
                : new[] { new[] { 0, Sign(dy) }, new[] { Sign(dx), 0 } };
            foreach (int[] a in opts)
            {
                if (a[0] == 0 && a[1] == 0) continue;
                int c = s.c + a[0], r = s.r + a[1];
                if (Interior(c, r) && StAt(c, r) == null && PlAt(c, r) == null && !(pl.c == c && pl.r == r))
                {
                    Slide(s, c, r);
                    moved = true;
                    break;
                }
            }
        }
        if (moved)
        {
            Scrape(0.3f);
            Play(aThump, 0.55f);
            foreach (Statue s in statues)
                if (!s.warm && !s.locked && Mathf.Abs(s.c - pl.c) + Mathf.Abs(s.r - pl.r) <= 1) { Play(aScreech, 1f); break; }
        }
    }

    static int Sign(int v) { return v > 0 ? 1 : v < 0 ? -1 : 0; }

    void Caught(string title = "THE STATUE CAUGHT YOU", string line = "YOU LOOKED AWAY TOO LONG.")
    {
        phase = "caught"; pt = 0f; thermal = false;
        catches++;
        caughtLabel.text = title;
        Boom(0.5f);
        Say(line);
    }

    float Closeness()
    {
        int m = 9;
        foreach (Statue s in statues)
        {
            if (s.warm || s.locked) continue;
            m = Mathf.Min(m, Mathf.Abs(s.c - pl.c) + Mathf.Abs(s.r - pl.r));
        }
        return Mathf.Clamp01(1f - (m - 1) / 5f);
    }

    void ReadInput()
    {
        if (phase == "title")
        {
            if (AnyDown()) Begin();
            return;
        }
        if (Down(KeyCode.R) && (phase == "play" || phase == "caught")) { ResetGame(); return; }
        if (phase != "play") return;
        if (Down(KeyCode.G))
        {
            thermal = !thermal;
            if (thermal) creepAt = t + 0.8f * Slow;
        }
        int dx = 0, dy = 0;
        bool down = false;
        if (Down(KeyCode.A) || Down(KeyCode.LeftArrow)) { dx = -1; down = true; }
        else if (Down(KeyCode.D) || Down(KeyCode.RightArrow)) { dx = 1; down = true; }
        else if (Down(KeyCode.W) || Down(KeyCode.UpArrow)) { dy = -1; down = true; }
        else if (Down(KeyCode.S) || Down(KeyCode.DownArrow)) { dy = 1; down = true; }
        if (down) { heldFor = 0f; Move(dx, dy); return; }

        int hx = 0, hy = 0;
        if (Held(KeyCode.A) || Held(KeyCode.LeftArrow)) hx = -1;
        else if (Held(KeyCode.D) || Held(KeyCode.RightArrow)) hx = 1;
        else if (Held(KeyCode.W) || Held(KeyCode.UpArrow)) hy = -1;
        else if (Held(KeyCode.S) || Held(KeyCode.DownArrow)) hy = 1;
        if (hx != 0 || hy != 0)
        {
            heldFor += Time.deltaTime;
            if (heldFor >= holdRepeatDelay && pl.mt >= 1f) Move(hx, hy);
        }
        else heldFor = 0f;
    }

    // Read through the Input System like the other minigames; the legacy Input class does not register keys here.
    static bool AnyDown()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame) return true;
        if (Mouse.current != null && (Mouse.current.leftButton.wasPressedThisFrame || Mouse.current.rightButton.wasPressedThisFrame)) return true;
        if (Gamepad.current != null && (Gamepad.current.startButton.wasPressedThisFrame || Gamepad.current.buttonSouth.wasPressedThisFrame)) return true;
        return false;
#else
        return Input.anyKeyDown;
#endif
    }

    static bool Down(KeyCode code)
    {
#if ENABLE_INPUT_SYSTEM
        var k = KeyFor(code);
        return k != null && k.wasPressedThisFrame;
#else
        return Input.GetKeyDown(code);
#endif
    }

    static bool Held(KeyCode code)
    {
#if ENABLE_INPUT_SYSTEM
        var k = KeyFor(code);
        return k != null && k.isPressed;
#else
        return Input.GetKey(code);
#endif
    }

#if ENABLE_INPUT_SYSTEM
    static UnityEngine.InputSystem.Controls.KeyControl KeyFor(KeyCode code)
    {
        var kb = Keyboard.current;
        if (kb == null) return null;
        switch (code)
        {
            case KeyCode.A: return kb.aKey;
            case KeyCode.D: return kb.dKey;
            case KeyCode.W: return kb.wKey;
            case KeyCode.S: return kb.sKey;
            case KeyCode.R: return kb.rKey;
            case KeyCode.G: return kb.gKey;
            case KeyCode.LeftArrow: return kb.leftArrowKey;
            case KeyCode.RightArrow: return kb.rightArrowKey;
            case KeyCode.UpArrow: return kb.upArrowKey;
            case KeyCode.DownArrow: return kb.downArrowKey;
            default: return null;
        }
    }
#endif

    void Update()
    {
        float dt = Mathf.Min(0.05f, Time.deltaTime);
        ReadInput();
        Step(dt);
        Draw();
    }

    void Step(float dt)
    {
        t += dt; pt += dt;
        pulse = Mathf.Max(0f, pulse - dt * 2f);
        Hunt();
        if (phase == "title") return;
        if (phase == "black") { if (pt > 1.8f) ResetGame(); return; }
        if (phase == "finale") { Finale(); return; }

        MoveStep(pl, dt, 0.13f);
        foreach (Statue s in statues) MoveStep(s, dt, 0.2f);

        if (phase == "play")
        {
            if (Mathf.Floor(t * 1.1f) != Mathf.Floor((t - dt) * 1.1f)) pulse = 1f;
            if (thermal)
            {
                int n = 0;
                foreach (Plate q in plates) if (q.done) n++;
                if (t >= creepAt)
                {
                    Creep();
                    creepAt = t + creepIntervals[Mathf.Clamp(n, 0, creepIntervals.Length - 1)] * Slow;
                }
            }
            if (phase == "play" && t - startT > timeLimit * Slow) { Caught("OUT OF TIME", "THE KEEP HAS RUN OUT OF PATIENCE."); }
            if (phase == "play")
            {
                bool all = true, settled = true;
                foreach (Plate q in plates) if (!q.done) all = false;
                foreach (Statue s in statues) if (s.mt < 1f) settled = false;
                if (all && settled) { phase = "ending"; pt = 0f; thermal = false; catches = 0; }
            }
        }
        if (phase == "ending")
        {
            float q = pt;
            if (last != null) last.crack = Mathf.Min(1f, q / 1.2f);
            if (q > 0.1f && !e0) { e0 = true; Say(crackLine); Scrape(0.3f); }
            if (q > 1.3f && !e1) { e1 = true; Boom(0.4f); Say(bodyLine); }
            black = Mathf.Clamp01((q - 4.2f) / 1.2f);
            if (q > 5.6f)
            {
                if (!loading && !string.IsNullOrEmpty(bossScene) && Application.CanStreamedLevelBeLoaded(bossScene))
                {
                    loading = true;
                    finaleOnly = true;
                    SceneManager.LoadScene(bossScene);
                    return;
                }
                if (!loading) { phase = "finale"; pt = 0f; finalJiggle.SetText(finalLine, finalFontSize, finalColor); }
            }
        }
        if (phase == "caught")
        {
            black = Mathf.Clamp01((pt - 1.1f) / 1f);
            if (pt > 2.2f) { phase = "black"; pt = 0f; }
        }
    }

    void MoveStep(Mover o, float dt, float dur)
    {
        if (o.mt >= 1f) return;
        o.mt = Mathf.Min(1f, o.mt + dt / dur);
        if (o.mt >= 1f && o.hasBack) { o.hasBack = false; Slide(o, o.backC, o.backR); }
    }

    void Finale()
    {
        black = 1f;
        float a;
        if (pt < finalFadeIn) a = pt / finalFadeIn;
        else if (pt < finalFadeIn + finalHold) a = 1f;
        else a = 1f - (pt - finalFadeIn - finalHold) / Mathf.Max(0.05f, finalFadeOut);
        finalJiggle.SetAlpha(Mathf.Clamp01(a));
        if (!loading && pt > finalFadeIn + finalHold + finalFadeOut + 0.4f)
        {
            loading = true;
            FPCharacter.VoiceDirector.QueueOnReturn("Return_EmberMinigame");
            if (string.IsNullOrEmpty(returnScene)) SceneManager.LoadScene(0);
            else SceneManager.LoadScene(returnScene);
        }
    }

    void Hunt()
    {
        bool on = phase == "play" && thermal;
        float cl = on ? Closeness() : 0f;
        float v = phase == "black" ? 0f : phase == "caught" ? Mathf.Max(0f, musicVolume - pt * 0.25f) : musicVolume;
        if (phase == "ending") v = musicVolume * Mathf.Clamp01(1f - (pt - 4f) / 1.5f);
        if (phase == "finale") v = 0f;
        bgm.volume = v;
        if (on && t >= beatAt) { Play(aBeat, 1f); beatAt = t + 0.9f - 0.55f * cl; }
    }

    Vector2 At(Mover o)
    {
        float k = o.mt < 1f ? o.mt * o.mt * (3f - 2f * o.mt) : 1f;
        return new Vector2(Mathf.Lerp(Tx(o.fc), Tx(o.c), k), Mathf.Lerp(Ty(o.fr), Ty(o.r), k) + 12f);
    }

    void Draw()
    {
        float sc = Mathf.Min(Screen.width / W, Screen.height / H);
        stage.localScale = new Vector3(sc, sc, 1f);

        bool th = thermal;
        float dg = statuesAttack && phase == "play" && th ? Closeness() : 0f;
        bool adj = dg >= 0.99f;
        world.anchoredPosition = adj ? new Vector2(Mathf.Sin(t * 71f) * 4f, -Mathf.Cos(t * 63f) * 3f) : Vector2.zero;

        back.sprite = th ? sFloorT : sFloor;
        wall.sprite = th ? sWallT : sWall;
        side.sprite = th ? sSideT : sSide;
        for (int i = 0; i < props.Length; i++) props[i].sprite = th ? sPropT[i] : sProp[i];
        vignette.enabled = !th;
        thermalOverlay.enabled = th;
        seam.enabled = !th;
        seam.color = new Color(1f, (150f + 60f * Mathf.Sin(t * 5f)) / 255f, 70f / 255f, 0.75f + 0.25f * pulse);
        Color gc = th ? new Color(120f / 255f, 110f / 255f, 1f, 0.10f) : new Color(0f, 0f, 0f, 0.28f);
        foreach (Image g in grid) g.color = gc;

        foreach (Plate p in plates) DrawPlate(p, th);
        foreach (Statue s in statues) DrawStatue(s, th);
        DrawPlayer(th);
        SortActors();
        DrawEmbers(th);
        UpdateView(sc);

        float k = phase == "caught" ? 1f : dg;
        if (k > 0f)
        {
            float pu = 0.55f + 0.45f * Mathf.Sin(t * (5f + k * 9f));
            danger.color = new Color(0.86f, 0.04f, 0.04f, (0.12f + 0.5f * k * k) * pu);
        }
        else danger.color = new Color(0.86f, 0.04f, 0.04f, 0f);

        bool title = phase == "title";
        titleDim.enabled = title;
        foreach (Text l in titleLabels) l.enabled = title;
        titleJiggle.gameObject.SetActive(title);
        pressLabel.enabled = title;
        pressLabel.color = Mathf.Sin(t * 4f) > 0f ? Hex("#ff5a48") : Hex("#efe6cf");

        bool hud = !title && phase != "finale";
        sightLabel.enabled = hud;
        platesLabel.enabled = hud;
        controlsLabel.enabled = hud && (phase == "play" || phase == "caught");
        int done = 0;
        foreach (Plate q in plates) if (q.done) done++;
        sightLabel.text = th ? "THERMAL SIGHT" : "NORMAL SIGHT";
        sightLabel.color = th ? Hex("#ffe14a") : Hex("#efe6cf");
        platesLabel.text = "PLATES  " + done + " / 3";

        bool warn = hud && phase == "play" && th && dg >= 0.79f;
        warnLabel.enabled = warn;
        warn2Label.enabled = warn && adj;
        barBg.enabled = warn && adj;
        barFill.enabled = warn && adj;
        if (warn)
        {
            bool on = Mathf.Sin(t * (adj ? 22f : 10f)) > 0f;
            warnLabel.text = adj ? "IT IS RIGHT BESIDE YOU" : "SOMETHING IS CLOSE";
            warnLabel.fontSize = adj ? 40 : 28;
            warnLabel.color = on ? Hex("#ff3b30") : Hex("#ffd9d2");
            PutLabel(warnLabel, CX, 150f);
            float left = Mathf.Max(0f, creepAt - t);
            barFill.rectTransform.sizeDelta = new Vector2(360f * Mathf.Min(1f, left / 0.9f), 10f);
        }
        caughtLabel.enabled = phase == "caught";

        float ma = Mathf.Clamp01((t - msgT) / 0.25f) * (1f - Mathf.Clamp01((t - msgT - 3.4f) / 0.6f));
        msgLabel.enabled = hud && !string.IsNullOrEmpty(msg) && ma > 0f;
        if (msgLabel.enabled)
        {
            msgLabel.text = msg;
            Color mc = Hex("#efe6cf");
            mc.a = ma;
            msgLabel.color = mc;
        }

        float b = phase == "black" || phase == "finale" ? 1f : black;
        blackImg.color = new Color(0f, 0f, 0f, b);
        finalJiggle.gameObject.SetActive(phase == "finale");
    }

    Vector3 Lift(float x, float y, float height)
    {
        float a = Tilt * Mathf.Deg2Rad;
        return new Vector3(x, -y + height * Mathf.Cos(a), -height * Mathf.Sin(a));
    }

    void UpdateView(float sc)
    {
        float vw = W * sc, vh = H * sc;
        float sw = Screen.width, sh = Screen.height;
        float bx = Mathf.Max(0f, (sw - vw) * 0.5f) + 1f, by = Mathf.Max(0f, (sh - vh) * 0.5f) + 1f;
        bars[0].rectTransform.sizeDelta = new Vector2(bx, 0f);
        bars[1].rectTransform.sizeDelta = new Vector2(bx, 0f);
        bars[2].rectTransform.sizeDelta = new Vector2(0f, by);
        bars[3].rectTransform.sizeDelta = new Vector2(0f, by);
        cam.rect = new Rect((sw - vw) * 0.5f / sw, (sh - vh) * 0.5f / sh, vw / sw, vh / sh);
        cam.fieldOfView = cameraFov;
        float tanHalf = Mathf.Tan(cameraFov * 0.5f * Mathf.Deg2Rad);
        Transform wt = worldCanvas.transform;
        float aspect = W / H;
        Vector3 focus;
        float dist;
        if (!perspectiveView)
        {
            focus = wt.position;
            dist = H * 0.5f * 0.01f / tanHalf;
        }
        else
        {
            float hw = viewWidth * W * 0.5f;
            float hh = hw / aspect / Mathf.Cos(Tilt * Mathf.Deg2Rad);
            Vector2 p = new Vector2(CX, Y0 + ROWS * TH * 0.5f);
            if (phase != "title" && pl != null)
            {
                p = At(pl);
                p.x += pl.face * lookAhead;
            }
            float lx = Mathf.Clamp(p.x - W * 0.5f, -(W * 0.5f - hw), W * 0.5f - hw);
            float ly = H * 0.5f - p.y;
            float lo = -H * 0.5f + hh * 0.85f, hi = H * 0.5f - hh * 0.7f;
            ly = lo <= hi ? Mathf.Clamp(ly, lo, hi) : (lo + hi) * 0.5f;
            focus = wt.TransformPoint(new Vector3(lx, ly, 0f));
            dist = hw * 0.01f / aspect / tanHalf;
        }
        Vector3 want = focus - Vector3.forward * dist;
        if (!camInit) { cam.transform.position = want; camVel = Vector3.zero; camInit = true; }
        else cam.transform.position = Vector3.SmoothDamp(cam.transform.position, want, ref camVel, Mathf.Max(0.01f, followSmooth), Mathf.Infinity, Mathf.Min(0.05f, Time.deltaTime));
        cam.transform.rotation = Quaternion.identity;
    }

    void DrawPlate(Plate p, bool th)
    {
        if (p.done)
        {
            p.outline.enabled = true;
            p.outline.sprite = p.warm ? sDoneW : sDoneC;
            p.outline.color = Color.white;
            p.heat.enabled = false;
        }
        else if (th)
        {
            Vector2 pp = At(pl);
            float dx = (pp.x - Tx(p.c)) / TW, dy = (pp.y - 12f - Ty(p.r)) / TH;
            float near = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((Mathf.Abs(dx) + Mathf.Abs(dy) - revealTiles) / Mathf.Max(0.01f, revealFadeTiles)));
            if (near > 0.6f) p.seen = true;
            float vis = Mathf.Max(near, p.seen ? rememberFoundAlpha : 0f);
            p.outline.enabled = vis > 0.01f;
            p.outline.sprite = p.warm ? sPlateW : sPlateC;
            p.outline.color = new Color(1f, 1f, 1f, vis);
            p.heat.enabled = p.warm && vis > 0.01f;
            if (p.warm) p.heat.color = new Color(1f, 1f, 1f, (0.55f + 0.2f * Mathf.Sin(t * 4f)) * vis);
        }
        else
        {
            p.outline.enabled = false;
            p.heat.enabled = false;
        }
        float v = (t - p.vent) / 0.7f;
        bool vent = v >= 0f && v < 1f;
        for (int i = 0; i < 3; i++)
        {
            p.flames[i].enabled = vent;
            if (vent)
            {
                float s = (2.2f * (1f - v) + 0.4f) / 1.6f;
                float w = 1f + 0.12f * Mathf.Sin(t * 13f + i);
                p.flames[i].rectTransform.localScale = new Vector3(s, s * w, 1f);
            }
        }
    }

    void DrawStatue(Statue s, bool th)
    {
        Vector2 pos = At(s);
        s.root.anchoredPosition = new Vector2(pos.x, -pos.y);
        bool isLast = s == last && s.crack > 0f;

        s.pedestal.enabled = !th;
        s.body.enabled = !th;
        s.crackImg.enabled = false;
        foreach (Image ch in s.chunks) ch.enabled = false;
        s.lockGlow.enabled = !th && s.locked && !(isLast && e1);
        s.deadT.enabled = th && s.warm;
        s.heat.enabled = th && s.warm;
        foreach (Image e in s.eyes) e.enabled = false;
        foreach (Image tr in s.trail) tr.enabled = false;

        if (th)
        {
            if (s.warm)
            {
                s.deadT.color = new Color(1f, 1f, 1f, 0.3f);
                s.heat.color = new Color(1f, 1f, 1f, 0.5f + 0.35f * Mathf.Abs(Mathf.Sin(t * 2.2f)));
            }
            else if (!s.locked)
            {
                bool near = Mathf.Abs(s.c - pl.c) + Mathf.Abs(s.r - pl.r) <= 2;
                bool mv = s.mt < 1f;
                float a = (mv ? 0.85f : 0.22f + 0.1f * Mathf.Sin(t * 5f + s.c)) * (phase == "play" ? 1f : 0f);
                Color col = near ? new Color(1f, 70f / 255f, 50f / 255f, a) : new Color(110f / 255f, 200f / 255f, 1f, a);
                for (int i = 0; i < 2; i++)
                {
                    s.eyes[i].enabled = true;
                    s.eyes[i].color = col;
                    s.eyes[i].rectTransform.anchoredPosition = new Vector2(i == 0 ? -5f : 5f, 104f);
                }
                if (mv)
                {
                    Vector2 o = new Vector2(Tx(s.fc), Ty(s.fr) + 12f);
                    for (int k = 1; k <= 3; k++)
                    {
                        float u = k / 4f;
                        Image tr = s.trail[k - 1];
                        tr.enabled = true;
                        tr.color = new Color(col.r, col.g, col.b, 0.3f * (1f - u));
                        tr.rectTransform.anchoredPosition3D = Lift(pos.x + (o.x - pos.x) * u, pos.y + (o.y - pos.y) * u, 104f);
                    }
                }
            }
            return;
        }

        if (isLast)
        {
            float k = s.crack;
            if (e1)
            {
                s.body.sprite = sBody;
                s.body.rectTransform.anchoredPosition = new Vector2(0f, 12f);
                float f = Mathf.Min(1f, (pt - 1.3f) / 0.7f);
                float[,] d = { { -40, -90, 0.5f }, { 38, -70, -0.6f }, { -30, -40, 0.9f }, { 34, -30, -0.3f }, { 0, -120, 0.2f } };
                for (int i = 0; i < 5; i++)
                {
                    Image ch = s.chunks[i];
                    ch.enabled = true;
                    ch.rectTransform.anchoredPosition = new Vector2(d[i, 0] * (0.3f + f), -(d[i, 1] * (1f - f) - 6f * f));
                    ch.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -d[i, 2] * f * 3f * Mathf.Rad2Deg);
                }
            }
            else
            {
                s.body.sprite = sStone;
                s.body.rectTransform.anchoredPosition = new Vector2(Mathf.Sin(t * 50f) * 2f * k, 12f);
                s.crackImg.enabled = true;
                s.crackImg.fillAmount = k;
            }
        }
        else
        {
            s.body.sprite = sStone;
            s.body.rectTransform.anchoredPosition = new Vector2(0f, 12f);
        }
    }

    void DrawPlayer(bool th)
    {
        Vector2 pos = At(pl);
        bool mv = pl.mt < 1f;
        int fr = mv ? (pl.walk % 2 == 1 ? 2 : 1) : 0;
        pl.img.sprite = th ? sHot[fr] : sPlayer[fr];
        float bob = mv ? Mathf.Sin(pl.mt * Mathf.PI) * 4f : 0f;
        pl.root.anchoredPosition = new Vector2(pos.x, -(pos.y - bob));
        pl.img.rectTransform.localScale = new Vector3(pl.face < 0 ? -1f : 1f, 1f, 1f);
    }

    readonly List<KeyValuePair<float, Transform>> order = new List<KeyValuePair<float, Transform>>();

    void SortActors()
    {
        order.Clear();
        foreach (Statue s in statues) order.Add(new KeyValuePair<float, Transform>(At(s).y, s.root));
        order.Add(new KeyValuePair<float, Transform>(At(pl).y + 1f, pl.root));
        order.Sort((a, b) => a.Key.CompareTo(b.Key));
        for (int i = 0; i < order.Count; i++) order[i].Value.SetSiblingIndex(i);
        foreach (Statue s in statues) foreach (Image tr in s.trail) tr.transform.SetAsLastSibling();
    }

    void DrawEmbers(bool th)
    {
        foreach (Ember e in embers)
        {
            float y = Mathf.Repeat(e.y - t * e.sp, H);
            float x = e.x + Mathf.Sin(t * 1.3f + e.ph) * 18f;
            float a = 0.35f + 0.35f * Mathf.Sin(t * 3f + e.ph);
            e.img.color = th ? new Color(1f, 225f / 255f, 74f / 255f, a) : new Color(1f, Mathf.Min(1f, (130f + e.ph * 10f) / 255f), 50f / 255f, a);
            Put(e.img, x, y);
        }
    }
}

public class JiggleLine : MonoBehaviour
{
    Font font;
    int size;
    Color color;
    float maxWidth;
    float alpha = 1f;
    readonly List<Text> chars = new List<Text>();
    readonly List<Vector2> basePos = new List<Vector2>();
    readonly List<int> index = new List<int>();

    public static JiggleLine Create(Transform parent, Font font, string text, int size, Color color, Vector2 htmlPos, float maxWidth)
    {
        var g = new GameObject("Jiggle text", typeof(RectTransform));
        g.transform.SetParent(parent, false);
        var rt = (RectTransform)g.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.sizeDelta = Vector2.zero;
        rt.anchoredPosition = new Vector2(htmlPos.x, -htmlPos.y);
        var j = g.AddComponent<JiggleLine>();
        j.font = font;
        j.maxWidth = maxWidth;
        j.SetText(text, size, color);
        return j;
    }

    float Advance(char ch)
    {
        CharacterInfo info;
        if (font.GetCharacterInfo(ch, out info, size)) return info.advance;
        return size * 0.6f;
    }

    public void SetText(string text, int newSize, Color newColor)
    {
        size = newSize;
        color = newColor;
        foreach (Text t in chars) if (t != null) Destroy(t.gameObject);
        chars.Clear(); basePos.Clear(); index.Clear();
        if (string.IsNullOrEmpty(text)) return;
        font.RequestCharactersInTexture(text + " ", size);
        float space = Advance(' ');

        var lines = new List<string>();
        foreach (string para in text.Replace("\\n", "\n").Split('\n'))
        {
            string cur = "";
            foreach (string word in para.Split(' '))
            {
                string trial = cur.Length > 0 ? cur + " " + word : word;
                if (Width(trial, space) > maxWidth && cur.Length > 0) { lines.Add(cur); cur = word; }
                else cur = trial;
            }
            lines.Add(cur);
        }

        float lineH = size * 1.35f;
        float top = -(lines.Count - 1) * lineH * 0.5f;
        int ci = 0;
        for (int li = 0; li < lines.Count; li++)
        {
            string ln = lines[li];
            float w = Width(ln, space);
            float px = -w * 0.5f;
            float py = top + li * lineH;
            foreach (char ch in ln)
            {
                float cw = ch == ' ' ? space : Advance(ch) * 1.06f;
                if (ch != ' ')
                {
                    var g = new GameObject("c", typeof(RectTransform));
                    g.transform.SetParent(transform, false);
                    var tx = g.AddComponent<Text>();
                    tx.font = font;
                    tx.fontSize = size;
                    tx.alignment = TextAnchor.MiddleCenter;
                    tx.horizontalOverflow = HorizontalWrapMode.Overflow;
                    tx.verticalOverflow = VerticalWrapMode.Overflow;
                    tx.raycastTarget = false;
                    tx.text = ch.ToString();
                    var o = g.AddComponent<Outline>();
                    o.effectColor = new Color(0.02f, 0.02f, 0.05f, 1f);
                    o.effectDistance = new Vector2(2.5f, -2.5f);
                    ((RectTransform)g.transform).sizeDelta = new Vector2(size * 2f, size * 2f);
                    chars.Add(tx);
                    basePos.Add(new Vector2(px + cw * 0.5f, py));
                    index.Add(ci);
                }
                px += cw;
                ci++;
            }
            ci++;
        }
        SetAlpha(alpha);
    }

    float Width(string s, float space)
    {
        float w = 0f;
        foreach (char ch in s) w += ch == ' ' ? space : Advance(ch) * 1.06f;
        return w;
    }

    public void SetAlpha(float a)
    {
        alpha = a;
        foreach (Text t in chars)
        {
            if (t == null) continue;
            Color c = color;
            c.a = color.a * a;
            t.color = c;
            Outline o = t.GetComponent<Outline>();
            if (o != null) o.effectColor = new Color(0.02f, 0.02f, 0.05f, a);
        }
    }

    void Update()
    {
        float t = Time.time;
        float amp = size / 30f;
        int q = Mathf.FloorToInt(t * 30f);
        for (int i = 0; i < chars.Count; i++)
        {
            int ci = index[i];
            float h = Mathf.Sin(q * 12.9898f + ci * 78.233f) * 43758.5453f;
            float r1 = (h - Mathf.Floor(h)) * 2f - 1f;
            float h2 = Mathf.Sin(q * 39.346f + ci * 11.135f) * 24634.6345f;
            float r2 = (h2 - Mathf.Floor(h2)) * 2f - 1f;
            float bop = -Mathf.Abs(Mathf.Sin(t * 13f + ci * 0.7f)) * 4.5f * amp;
            Vector2 b = basePos[i];
            RectTransform rt = chars[i].rectTransform;
            rt.anchoredPosition = new Vector2(b.x + r1 * 1.3f * amp, -(b.y + bop + r2 * 1.3f * amp));
            rt.localRotation = Quaternion.Euler(0f, 0f, -r1 * 0.065f * Mathf.Rad2Deg);
        }
    }
}
