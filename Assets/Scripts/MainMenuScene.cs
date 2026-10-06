using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Casa del Silencio - main menu, built entirely at runtime.
//
// Setup (that's all of it):
//   1. Copy the MainMenu folder into Assets/Resources/  (so the files sit in Assets/Resources/MainMenu/).
//   2. Make an empty scene named "MainMenu", create an empty GameObject, add this script.
//   3. Type the scene to load into Game Scene Name, and drag your menu song into Music.
//   4. Put the scene first in the build list.
// The script builds the fog-bound island, the nine shadow figures (each one drifts and breathes
// on its own), the drifting fog, Julian Thorne on the Santa Rosa heading for the island with his
// lantern (the boat bobs and rolls, foam streams along the hull, the wake churns, his scarf flaps
// and the lantern swings and flickers), the animated water, the stone face that blinks, the title
// the Play button and the QUE SERA, SERA line beneath it. Play is grey and turns orange
// under the mouse. A click on it, or Enter, fades to black, shows the BACK AGAIN card (AGAIN
// turns red over five seconds), and then loads the game scene, where your opening cutscene plays.
public class MainMenuScene : MonoBehaviour
{
    public string gameSceneName = "Game";
    [Tooltip("Menu song. If left empty, Resources/MainMenu/menu_music is used.")]
    public AudioClip music;
    [Range(0f, 1f)] public float musicVolume = 0.8f;
    [Tooltip("Seconds for the song to rise slowly from silence to Music Volume.")]
    public float musicFadeIn = 6f;
    public float fadeTime = 1.5f;

    [Header("BACK AGAIN card")]
    public float blackHold = 0.4f;
    public float cardFadeIn = 1.2f;
    public float holdBeforeRed = 0.6f;
    public float redSeconds = 1.8f;
    public float jiggleSeconds = 1.1f;
    public float cardFadeOut = 1.0f;
    public float blackAfter = 0f;
    public float againJiggle = 7f;
    public float againBop = 0.08f;
    public float againHop = 12f;
    public Color cardTextColor = new Color(0.85f, 0.84f, 0.90f, 1f);
    public Color againRed = new Color(0.78f, 0.06f, 0.05f, 1f);

    [Header("Boat (Julian Thorne on the Santa Rosa)")]
    [Tooltip("How far the boat bobs up and down, in pixels on a 1920x1080 screen.")]
    public float boatBob = 7f;
    [Tooltip("How far the boat rolls side to side, in degrees.")]
    public float boatRoll = 2.2f;
    [Tooltip("How far the boat slowly drifts toward the island and back, in pixels.")]
    public float boatDrift = 18f;
    [Range(0f, 1f)] public float lanternBrightness = 0.55f;
    [Tooltip("How far the lantern swings in Thorne's hand, in degrees.")]
    public float lanternSwing = 8f;
    [Tooltip("Frames per second for the foam, wake and scarf animations.")]
    public float boatAnimFps = 12f;

    [Header("Water")]
    [Tooltip("How far the glints on the water sway side to side, in pixels.")]
    public float waterSway = 10f;
    public float waterSpeed = 1f;

    [Header("SILENCIO glitch")]
    [Tooltip("Seconds between glitches on the word SILENCIO (random between min and max).")]
    public float glitchEveryMin = 4f;
    public float glitchEveryMax = 9f;
    [Tooltip("How long one glitch lasts, in seconds (random between min and max).")]
    public float glitchLengthMin = 0.12f;
    public float glitchLengthMax = 0.32f;
    [Range(0f, 1f)] public float doubleGlitchChance = 0.35f;
    public float glitchFps = 24f;

    [Header("Stone face blinking")]
    public float blinkEveryMin = 3.5f;
    public float blinkEveryMax = 7f;
    [Tooltip("Seconds for one blink (close and open).")]
    public float blinkDuration = 0.32f;
    [Range(0f, 1f)] public float doubleBlinkChance = 0.3f;

    [Header("Shadow figures")]
    [Tooltip("How far each figure drifts, in pixels on a 1920x1080 screen.")]
    public float figureDrift = 14f;
    public float figureSpeed = 1f;
    [Range(0f, 1f)] public float fogAmount = 0.4f;

    const string Dir = "MainMenu/";
    // Where each figure sits on a 1920x1080 screen: left, top, right, bottom.
    static readonly int[,] FigureRect =
    {
        { 591, 380, 894, 844 },
        { 711, 276, 1066, 846 },
        { 812, 270, 1166, 846 },
        { 946, 191, 1350, 844 },
        { 1052, 129, 1465, 846 },
        { 1184, 226, 1574, 846 },
        { 1348, 226, 1728, 844 },
        { 1481, 318, 1798, 846 },
        { 1613, 346, 1920, 844 }
    };
    static readonly int[] DrawOrder = { 0, 8, 1, 7, 2, 6, 3, 5, 4 };   // outer figures behind, centre one in front
    static readonly int[] TitleRect = { 456, 179, 1465, 336 };
    // The boat sprite (560x400) and the pieces that go with it, as left, top, right, bottom.
    static readonly int[] BoatRect = { 340, 640, 900, 1040 };
    static readonly int[] WakeRect = { -220, 910, 480, 1170 };
    static readonly int[] LidsRect = { 1202, 612, 1322, 644 };           // stone eyelids over the statue's eyes
    static readonly int[] GlintsRect = { 0, 770, 1920, 1080 };           // twinkling light on the water
    static readonly int[] EyeReflectionRect = { 1142, 780, 1382, 1040 }; // the eyes' glow reflected on the water
    static readonly Vector2 ScarfOffset = new Vector2(123f, 84f);       // scarf knot, relative to the boat's centre
    static readonly Vector2 LanternHandle = new Vector2(103f, 50f);     // top of the lantern handle, relative to the boat's centre
    static readonly int[] ReflectionRect = { 340, 970, 900, 1370 };
    static readonly int[] ShimmerRect = { 663, 970, 783, 1230 };
    const float LanternGlowSize = 360f;
    static readonly int[] PlayRect = { 704, 895, 1216, 1023 };
    static readonly int[] TaglineRect = { 704, 1003, 1216, 1051 };       // "QUE SERA, SERA" under the Play button
    static readonly int[] PlayHitBox = { 880, 936, 1040, 984 };

    RectTransform stage, fogA, fogB;
    readonly RectTransform[] figures = new RectTransform[9];
    readonly Image[] figureImages = new Image[9];
    readonly Vector2[] figureHome = new Vector2[9];
    Image title, titleSilencio, play, tagline, statueEyes, backWord;
    readonly Image[] againLetters = new Image[5];
    readonly Vector2[] letterHome = new Vector2[5];
    static readonly int[] LetterCuts = { 0, 116, 222, 329, 382, 512 };
    Sprite silencioSprite;
    Sprite[] silencioGlitch;
    RectTransform boat, boatReflection, shimmer, wake;
    Image lanternGlow, shimmerImage, wakeImage, reflectionImage, foamImage, scarfImage, lids, eyeReflection;
    RectTransform lantern, scarf, lidsRT;
    readonly Image[] glints = new Image[3];
    readonly Vector2[] glintHome = new Vector2[3];
    Sprite[] foamFrames, wakeFrames, scarfFrames;
    float lidClose;
    Vector2 boatHome, reflectionHome, shimmerHome, wakeHome;
    Sprite playSprite, playHotSprite;
    CanvasGroup fader;
    AudioSource source;
    bool starting;

    void Start()
    {
        var cgo = new GameObject("MainMenu UI");
        cgo.transform.SetParent(transform, false);
        var canvas = cgo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = cgo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 1f;

        var black = NewImage(cgo.transform, "Black", null);
        black.color = Color.black;
        Stretch(black.rectTransform);

        // A fixed 1920x1080 stage in the middle of the screen; everything is placed on it.
        var sgo = new GameObject("Stage", typeof(RectTransform), typeof(RectMask2D));
        sgo.transform.SetParent(cgo.transform, false);
        stage = (RectTransform)sgo.transform;
        stage.anchorMin = stage.anchorMax = stage.pivot = new Vector2(0.5f, 0.5f);
        stage.sizeDelta = new Vector2(1920f, 1080f);

        Place(NewImage(stage, "Sky", Load("sky")), 0, 0, 1920, 1080);
        foreach (int i in DrawOrder)
        {
            var im = NewImage(stage, "Figure " + (i + 1), Load("figure_" + (i + 1)));
            Place(im, FigureRect[i, 0], FigureRect[i, 1], FigureRect[i, 2], FigureRect[i, 3]);
            figures[i] = im.rectTransform;
            figureImages[i] = im;
            figureHome[i] = im.rectTransform.anchoredPosition;
        }
        Place(NewImage(stage, "Island", Load("island")), 0, 0, 1920, 1080);

        // The stone face's eyes: a light-blue glow laid over the island, pulsing slowly.
        statueEyes = NewImage(stage, "Statue Eyes", Load("statue_eyes"));
        Place(statueEyes, 1062, 529, 1462, 729);

        // Stone eyelids: they slide down over the eyes when the face blinks (pivot at their top edge).
        lids = NewImage(stage, "Statue Eyelids", Load("statue_lids"));
        Place(lids, LidsRect[0], LidsRect[1], LidsRect[2], LidsRect[3]);
        lidsRT = lids.rectTransform;
        lidsRT.pivot = new Vector2(0.5f, 1f);
        lidsRT.anchoredPosition += new Vector2(0f, (LidsRect[3] - LidsRect[1]) * 0.5f);
        lidsRT.localScale = new Vector3(1f, 0f, 1f);

        // The water: three layers of glints that twinkle and sway, and the eyes' glow on the surface.
        for (int i = 0; i < 3; i++)
        {
            glints[i] = NewImage(stage, "Water Glints " + (i + 1), Load("water_glints_" + "abc"[i]));
            Place(glints[i], GlintsRect[0], GlintsRect[1], GlintsRect[2], GlintsRect[3]);
            glintHome[i] = glints[i].rectTransform.anchoredPosition;
        }
        eyeReflection = NewImage(stage, "Eye Reflection", Load("eye_reflection"));
        Place(eyeReflection, EyeReflectionRect[0], EyeReflectionRect[1], EyeReflectionRect[2], EyeReflectionRect[3]);

        var fog = Load("fog");
        fogA = NewImage(stage, "Fog A", fog).rectTransform;
        fogB = NewImage(stage, "Fog B", fog).rectTransform;
        Place(fogA.GetComponent<Image>(), 0, 0, 1920, 1080);
        Place(fogB.GetComponent<Image>(), 1920, 0, 3840, 1080);
        fogB.localScale = new Vector3(-1f, 1f, 1f);          // mirrored so the two copies meet without a seam

        // Julian Thorne on the Santa Rosa, heading for the island with his lantern.
        foamFrames = LoadFrames("boat_foam_", 12);
        wakeFrames = LoadFrames("wake_", 12);
        scarfFrames = LoadFrames("thorne_scarf_", 6);

        wakeImage = NewImage(stage, "Wake", wakeFrames[0]);
        Place(wakeImage, WakeRect[0], WakeRect[1], WakeRect[2], WakeRect[3]);
        wake = wakeImage.rectTransform; wakeHome = wake.anchoredPosition;

        reflectionImage = NewImage(stage, "Boat Reflection", Load("boat_reflection"));
        Place(reflectionImage, ReflectionRect[0], ReflectionRect[1], ReflectionRect[2], ReflectionRect[3]);
        boatReflection = reflectionImage.rectTransform; reflectionHome = boatReflection.anchoredPosition;

        shimmerImage = NewImage(stage, "Lantern Shimmer", Load("lantern_shimmer"));
        Place(shimmerImage, ShimmerRect[0], ShimmerRect[1], ShimmerRect[2], ShimmerRect[3]);
        shimmer = shimmerImage.rectTransform; shimmerHome = shimmer.anchoredPosition;

        var boatImage = NewImage(stage, "Boat", Load("boat"));
        Place(boatImage, BoatRect[0], BoatRect[1], BoatRect[2], BoatRect[3]);
        boat = boatImage.rectTransform; boatHome = boat.anchoredPosition;

        // Foam along the hull and the bow wave ride with the boat.
        foamImage = NewImage(boat, "Hull Foam", foamFrames[0]);
        Centre(foamImage.rectTransform, Vector2.zero, new Vector2(560f, 400f), new Vector2(0.5f, 0.5f));

        // Thorne's scarf flaps in the wind (pivot at the knot at his neck).
        scarfImage = NewImage(boat, "Scarf", scarfFrames[0]);
        scarf = scarfImage.rectTransform;
        Centre(scarf, ScarfOffset, new Vector2(100f, 44f), new Vector2(0.94f, 1f - 16f / 44f));

        // The lantern hangs from his hand and swings; its glow is a child of the lantern.
        var lanternImage = NewImage(boat, "Lantern", Load("thorne_lantern"));
        lantern = lanternImage.rectTransform;
        Centre(lantern, LanternHandle, new Vector2(40f, 56f), new Vector2(0.5f, 1f - 4f / 56f));
        lanternGlow = NewImage(lantern, "Lantern Glow", Load("lantern_glow"));
        Centre(lanternGlow.rectTransform, new Vector2(0f, 10f), new Vector2(LanternGlowSize, LanternGlowSize), new Vector2(0.5f, 0.5f));
        lanternGlow.transform.SetAsFirstSibling();

        Place(NewImage(stage, "Vignette", Load("vignette")), 0, 0, 1920, 1080);

        // The title in two parts so only SILENCIO glitches.
        title = NewImage(stage, "Title CASA DEL", Load("title_casa_del"));
        Place(title, TitleRect[0], TitleRect[1], TitleRect[2], TitleRect[3]);
        silencioSprite = Load("title_silencio");
        silencioGlitch = LoadFrames("title_silencio_glitch_", 10);
        titleSilencio = NewImage(stage, "Title SILENCIO", silencioSprite);
        Place(titleSilencio, TitleRect[0], TitleRect[1], TitleRect[2], TitleRect[3]);

        playSprite = Load("play");
        playHotSprite = Load("play_hot");
        play = NewImage(stage, "Play", playSprite);
        Place(play, PlayRect[0], PlayRect[1], PlayRect[2], PlayRect[3]);

        tagline = NewImage(stage, "Tagline", Load("tagline"));
        Place(tagline, TaglineRect[0], TaglineRect[1], TaglineRect[2], TaglineRect[3]);

        var fgo = NewImage(cgo.transform, "Fader", null);
        fgo.color = Color.black;
        Stretch(fgo.rectTransform);
        fader = fgo.gameObject.AddComponent<CanvasGroup>();
        fader.alpha = 1f;                                      // the menu fades in from black

        // The BACK AGAIN card: two words on a 1920x1080 stage above the black fader, hidden until Play is pressed.
        var ogo = new GameObject("Card", typeof(RectTransform));
        ogo.transform.SetParent(cgo.transform, false);
        var card = (RectTransform)ogo.transform;
        card.anchorMin = card.anchorMax = card.pivot = new Vector2(0.5f, 0.5f);
        card.sizeDelta = new Vector2(1920f, 1080f);
        backWord = NewImage(card, "BACK", Load("back"));
        Place(backWord, 444, 412, 956, 668);
        backWord.color = new Color(cardTextColor.r, cardTextColor.g, cardTextColor.b, 0f);
        var againTex = Resources.Load<Texture2D>(Dir + "again");
        for (int i = 0; i < 5 && againTex != null; i++)
        {
            int x0 = LetterCuts[i], x1 = LetterCuts[i + 1];
            float sx = againTex.width / 512f;
            var sp = Sprite.Create(againTex, new Rect(x0 * sx, 0f, (x1 - x0) * sx, againTex.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            var im = NewImage(card, "AGAIN " + "AGAIN"[i], sp);
            Place(im, 919 + x0, 412, 919 + x1, 668);
            var rt = im.rectTransform;
            rt.pivot = new Vector2(0.5f, 1f - 85f / 256f);
            rt.anchoredPosition += new Vector2(0f, (rt.pivot.y - 0.5f) * 256f);
            letterHome[i] = rt.anchoredPosition;
            im.color = new Color(cardTextColor.r, cardTextColor.g, cardTextColor.b, 0f);
            againLetters[i] = im;
        }

        StartCoroutine(BlinkLoop());
        StartCoroutine(GlitchLoop());

        if (music == null) music = Resources.Load<AudioClip>(Dir + "menu_music");
        if (music != null)
        {
            source = gameObject.AddComponent<AudioSource>();
            source.clip = music;
            source.loop = true;
            source.volume = 0f;                                   // starts silent and rises slowly
            source.Play();
            StartCoroutine(MusicFadeIn());
        }
    }

    void Update()
    {
        float t = Time.time;

        // Each figure drifts, bobs, swells and dims on its own slow rhythm.
        for (int i = 0; i < 9; i++)
        {
            if (figures[i] == null) continue;
            float ph = i * 1.7f, s = figureSpeed;
            float x = Mathf.Sin(t * 0.23f * s + ph) * figureDrift;
            float y = Mathf.Sin(t * 0.37f * s + ph * 1.3f) * figureDrift * 0.7f + Mathf.Sin(t * 0.11f * s + ph) * figureDrift * 0.45f;
            figures[i].anchoredPosition = figureHome[i] + new Vector2(x, y);
            figures[i].localScale = Vector3.one * (1f + 0.02f * Mathf.Sin(t * 0.31f * s + ph));
            SetAlpha(figureImages[i], 0.88f + 0.12f * Mathf.Sin(t * 0.9f * s + ph * 2f));
        }

        // Fog slides slowly to the left, wrapping round.
        float off = Mathf.Repeat(t * 14f, 3840f);
        SetFogX(fogA, -off);
        SetFogX(fogB, 1920f - off);
        if (fogA != null) SetAlpha(fogA.GetComponent<Image>(), fogAmount);
        if (fogB != null) SetAlpha(fogB.GetComponent<Image>(), fogAmount);

        // The boat rides the swell: bob, roll, and a slow drift toward the island and back.
        if (boat != null)
        {
            float bob = Mathf.Sin(t * 1.3f) * boatBob + Mathf.Sin(t * 0.55f + 1f) * boatBob * 0.5f;
            float drift = Mathf.Sin(t * 0.07f) * boatDrift;
            boat.anchoredPosition = boatHome + new Vector2(drift, bob);
            boat.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 0.9f + 0.4f) * boatRoll);
            if (boatReflection != null)
            {
                boatReflection.anchoredPosition = reflectionHome + new Vector2(drift, -bob * 0.6f);
                boatReflection.localRotation = Quaternion.Euler(0f, 0f, -Mathf.Sin(t * 0.9f + 0.4f) * boatRoll);
                SetAlpha(reflectionImage, 0.8f + 0.2f * Mathf.Sin(t * 2.3f));
            }
            float flicker = Mathf.Lerp(0.75f, 1f, Mathf.PerlinNoise(t * 3.5f, 0.3f)) * (0.95f + 0.05f * Mathf.Sin(t * 23f));
            if (lanternGlow != null) SetAlpha(lanternGlow, lanternBrightness * flicker);
            if (shimmer != null)
            {
                shimmer.anchoredPosition = shimmerHome + new Vector2(drift + Mathf.Sin(t * 3.1f) * 3f, 0f);
                shimmer.localScale = new Vector3(1f + 0.08f * Mathf.Sin(t * 2.7f), 1f, 1f);
                SetAlpha(shimmerImage, 0.55f + 0.35f * Mathf.PerlinNoise(t * 2.2f, 5f));
            }
            if (wake != null)
            {
                wake.anchoredPosition = wakeHome + new Vector2(drift, bob * 0.3f);
                SetAlpha(wakeImage, 0.75f + 0.2f * Mathf.Sin(t * 0.8f));
            }
            int frame = (int)(t * boatAnimFps);
            if (wakeImage != null) wakeImage.sprite = wakeFrames[frame % wakeFrames.Length];
            if (foamImage != null) foamImage.sprite = foamFrames[frame % foamFrames.Length];
            if (scarfImage != null)
            {
                scarfImage.sprite = scarfFrames[(int)(t * boatAnimFps * 0.8f) % scarfFrames.Length];
                scarf.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 2.1f) * 4f);
            }
            // The lantern swings like a pendulum and partly cancels the boat's roll.
            if (lantern != null)
                lantern.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 1.7f + 0.6f) * lanternSwing - Mathf.Sin(t * 0.9f + 0.4f) * boatRoll * 0.7f);
        }

        // Water: glints twinkle in turn and sway; the eyes' reflection shimmers.
        for (int i = 0; i < 3; i++)
        {
            if (glints[i] == null) continue;
            float ph = i * 2.1f, ws = waterSpeed;
            SetAlpha(glints[i], 0.35f + 0.65f * Mathf.Pow(0.5f + 0.5f * Mathf.Sin(t * 1.3f * ws + ph), 1.5f));
            glints[i].rectTransform.anchoredPosition = glintHome[i] + new Vector2(Mathf.Sin(t * 0.21f * ws + ph) * waterSway, Mathf.Sin(t * 0.6f * ws + ph) * 1.5f);
        }

        // Stone face: eyes pulse, the lids close over them when it blinks.
        float eyesOpen = 1f - lidClose;
        float eyePulse = 0.75f + 0.25f * Mathf.Sin(t * 1.1f);
        if (statueEyes != null) SetAlpha(statueEyes, eyePulse * Mathf.Lerp(0.12f, 1f, eyesOpen));
        if (lidsRT != null) lidsRT.localScale = new Vector3(1f, lidClose, 1f);
        if (eyeReflection != null)
        {
            SetAlpha(eyeReflection, eyePulse * Mathf.Lerp(0.1f, 1f, eyesOpen) * Mathf.Lerp(0.7f, 1f, Mathf.PerlinNoise(t * 2.5f, 9f)));
            eyeReflection.rectTransform.localScale = new Vector3(1f + 0.06f * Mathf.Sin(t * 2.3f), 1f, 1f);
        }
        float titleGlow = Mathf.Lerp(0.84f, 1f, Mathf.PerlinNoise(t * 0.8f, 0f));
        if (title != null) SetAlpha(title, titleGlow);
        if (titleSilencio != null) SetAlpha(titleSilencio, titleGlow);
        if (tagline != null) SetAlpha(tagline, 0.82f + 0.12f * Mathf.Sin(t * 0.6f));   // slow, gentle breathing

        if (starting) return;
        if (fader.alpha > 0f) fader.alpha = Mathf.MoveTowards(fader.alpha, 0f, Time.deltaTime / 1.2f);

        bool over = OverPlay();
        if (play != null) play.sprite = over ? playHotSprite : playSprite;
        if ((over && ClickDown()) || EnterDown()) StartCoroutine(Begin());
    }

    void SetFogX(RectTransform fog, float left)
    {
        // Each copy is 1920 wide; wrap it so the pair always covers the stage.
        if (left < -1920f) left += 3840f;
        var p = fog.anchoredPosition;
        p.x = left + 960f;
        fog.anchoredPosition = p;
    }

    bool OverPlay()
    {
        Vector2 local;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(stage, MousePosition(), null, out local)) return false;
        float sx = local.x + 960f, sy = 540f - local.y;        // stage coordinates, origin top-left
        return sx >= PlayHitBox[0] && sx <= PlayHitBox[2] && sy >= PlayHitBox[1] && sy <= PlayHitBox[3];
    }

    IEnumerator Begin()
    {
        starting = true;
        float from = fader.alpha, startVol = source != null ? source.volume : 0f;
        for (float f = 0f; f < fadeTime; f += Time.deltaTime)
        {
            float k = f / fadeTime;
            fader.alpha = Mathf.Lerp(from, 1f, k);
            if (source != null) source.volume = Mathf.Lerp(startVol, 0f, k);
            yield return null;
        }
        fader.alpha = 1f;
        if (source != null) source.Stop();

        Application.backgroundLoadingPriority = ThreadPriority.BelowNormal;
        AsyncOperation load = SceneManager.LoadSceneAsync(gameSceneName);
        if (load != null) load.allowSceneActivation = false;
        yield return new WaitForSeconds(blackHold);
        float redAt = cardFadeIn + holdBeforeRed, shakeAt = redAt + redSeconds, outAt = shakeAt + jiggleSeconds, total = outAt + cardFadeOut;
        for (float t = 0f; t < total; t += Time.deltaTime)
        {
            float a = Smooth(t / cardFadeIn) * (1f - Smooth((t - outAt) / cardFadeOut));
            Color b = cardTextColor; b.a = a;
            if (backWord != null) backWord.color = b;
            for (int i = 0; i < 5; i++)
            {
                Image im = againLetters[i];
                if (im == null) continue;
                float red = Smooth((t - redAt - i * 0.18f) / (redSeconds - 0.72f));
                Color g = Color.Lerp(cardTextColor, againRed, red); g.a = a;
                im.color = g;
                float k = t < shakeAt ? red * 0.55f : Mathf.Lerp(0.55f, 1f, Smooth((t - shakeAt) / 0.5f));
                float seed = i * 13.7f;
                float j = againJiggle * k;
                float beat = Mathf.Abs(Mathf.Sin(t * Mathf.PI * 2.4f - i * 0.65f));
                Vector2 shake = new Vector2((Mathf.PerlinNoise(t * 20f, seed) - 0.5f) * 2f * j, (Mathf.PerlinNoise(seed, t * 20f) - 0.5f) * 2f * j);
                var rt = im.rectTransform;
                rt.anchoredPosition = letterHome[i] + shake + new Vector2(0f, beat * againHop * k);
                rt.localRotation = Quaternion.Euler(0f, 0f, (Mathf.PerlinNoise(t * 8f, seed + 4.2f) - 0.5f) * 14f * k);
                float bop = 1f + againBop * beat * k;
                rt.localScale = new Vector3(bop, bop, 1f);
            }
            yield return null;
        }
        if (backWord != null) SetAlpha(backWord, 0f);
        foreach (Image im in againLetters) if (im != null) SetAlpha(im, 0f);
        if (blackAfter > 0f) yield return new WaitForSeconds(blackAfter);
        SessionReset.ResetAll();
        OpeningCutscene.Pending = true;
        Application.backgroundLoadingPriority = ThreadPriority.Normal;
        if (load != null) load.allowSceneActivation = true;
        else SceneManager.LoadScene(gameSceneName);
    }

    IEnumerator BlinkLoop()
    {
        yield return new WaitForSeconds(2f);
        while (true)
        {
            int blinks = Random.value < doubleBlinkChance ? 2 : 1;
            for (int b = 0; b < blinks; b++)
            {
                float d = Mathf.Max(0.1f, blinkDuration);
                for (float f = 0f; f < d; f += Time.deltaTime)
                {
                    float k = f / d;                                  // close fast, hold a moment, open slower
                    lidClose = k < 0.35f ? Smooth(k / 0.35f) : k < 0.5f ? 1f : 1f - Smooth((k - 0.5f) / 0.5f);
                    yield return null;
                }
                lidClose = 0f;
                if (b < blinks - 1) yield return new WaitForSeconds(0.12f);
            }
            yield return new WaitForSeconds(Random.Range(blinkEveryMin, Mathf.Max(blinkEveryMin, blinkEveryMax)));
        }
    }

    IEnumerator MusicFadeIn()
    {
        float d = Mathf.Max(0.1f, musicFadeIn);
        for (float f = 0f; f < d && !starting; f += Time.deltaTime)
        {
            float k = f / d;
            source.volume = musicVolume * k * k;                  // slow start, gentle rise
            yield return null;
        }
        if (!starting) source.volume = musicVolume;
    }

    IEnumerator GlitchLoop()
    {
        yield return new WaitForSeconds(Random.Range(2.5f, 4f));
        while (true)
        {
            int bursts = Random.value < doubleGlitchChance ? 2 : 1;
            for (int b = 0; b < bursts; b++)
            {
                float len = Random.Range(glitchLengthMin, Mathf.Max(glitchLengthMin, glitchLengthMax));
                float step = 1f / Mathf.Max(1f, glitchFps);
                for (float f = 0f; f < len; f += step)
                {
                    if (titleSilencio != null) titleSilencio.sprite = silencioGlitch[Random.Range(0, silencioGlitch.Length)];
                    yield return new WaitForSeconds(step);
                }
                if (titleSilencio != null) titleSilencio.sprite = silencioSprite;
                if (b < bursts - 1) yield return new WaitForSeconds(Random.Range(0.08f, 0.2f));
            }
            yield return new WaitForSeconds(Random.Range(glitchEveryMin, Mathf.Max(glitchEveryMin, glitchEveryMax)));
        }
    }

    static float Smooth(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }

    // ------------------------------------------------------------------ helpers

    static Sprite[] LoadFrames(string prefix, int count)
    {
        var frames = new Sprite[count];
        for (int i = 0; i < count; i++) frames[i] = Load(prefix + i.ToString("00"));
        return frames;
    }

    // Place a child image by its centre offset from the parent's centre, with a given size and pivot.
    static void Centre(RectTransform rt, Vector2 offset, Vector2 size, Vector2 pivot)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.pivot = pivot;
        rt.anchoredPosition = offset;
    }

    static Vector2 MousePosition()
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

    static bool EnterDown()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame);
#else
        return Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
#endif
    }

    static Sprite Load(string file)
    {
        var tex = Resources.Load<Texture2D>(Dir + file);
        if (tex == null)
        {
            Debug.LogError("MainMenuScene: missing Assets/Resources/" + Dir + file + ".png");
            return null;
        }
        return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
    }

    static Image NewImage(Transform parent, string name, Sprite sprite)
    {
        var g = new GameObject(name);
        g.transform.SetParent(parent, false);
        var im = g.AddComponent<Image>();
        im.sprite = sprite;
        im.raycastTarget = false;
        return im;
    }

    // Place an image on the stage by its left, top, right, bottom edges (origin top-left).
    static void Place(Image im, int left, int top, int right, int bottom)
    {
        var rt = im.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(right - left, bottom - top);
        rt.anchoredPosition = new Vector2((left + right) * 0.5f, -(top + bottom) * 0.5f);
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static void SetAlpha(Graphic g, float a) { var c = g.color; c.a = a; g.color = c; }
}
