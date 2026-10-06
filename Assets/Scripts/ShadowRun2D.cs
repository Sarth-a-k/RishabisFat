using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Casa del Silencio - the 2D "shadow run" section (version 7: pixel art, intro conversation with the ghost, captions).
//
// Setup (that's all of it):
//   1. Copy the Shadow2D folder into Assets/Resources/  (so the files sit in Assets/Resources/Shadow2D/).
//   2. Make a new empty scene named "Shadow2D", create an empty GameObject, add this script.
//   3. Add the scene to the build list.
// The script builds the whole scene at runtime: camera, world, player, torch, darkness, shadows, UI.
//
// The scene opens on a short conversation with the ghost (Space, Enter or a click skips a line).
// The player can walk during it; the shadow and torch timers below only start once it ends. Captions appear during the run. Every line is a baked image
// (dlg_*.png), so no font is needed.
//
// Controls: A/D or arrow keys to walk, mouse to aim the torch across the upper 180 degrees.
// The section is unwinnable by design: the swarm grows until the player is smothered,
// the screen goes black, and the game restarts.
//
// Everything is pixel art. For crisp pixels, select all the PNGs in the Shadow2D folder in Unity
// and set Compression to None (the script already switches them to point filtering).
public class ShadowRun2D : MonoBehaviour
{
    [Header("Restart")]
    [Tooltip("Scene loaded after the screen goes black. Empty = first scene in the build list.")]
    public string restartScene = "";

    [Header("Command text")]
    [Tooltip("Optional. Drag MS Gothic (or any font) here to draw the command line as live text. Empty = the baked hint.png image.")]
    public Font commandFont;
    public string commandText = "[A][D] WALK      [MOUSE] AIM THE TORCH";

    [Header("Music")]
    [Range(0f, 1f)] public float musicVolume = 0.45f;
    public float musicFadeIn = 3f;

    [Header("Dialogue")]
    [Tooltip("All lines and speakers. Empty = Resources/Dialogue/Shadow2D_Dialogue.")]
    public DialogueSet dialogueSet;
    [Tooltip("The player cannot walk past the ghost until this many intro lines have been shown.")]
    public int introLinesBeforeWalking = 2;

    [Header("Player")]
    public float walkSpeed = 2.5f;
    public float worldLeft = -2f;
    public float worldRight = 62f;
    public float ghostX = 4.2f;
    public float stopBeforeGhost = 1.3f;

    [Header("Torch")]
    [Tooltip("Seconds into the 2D world before the torch starts to flicker.")]
    public float flickerStartsAt = 10f;
    [Tooltip("The torch sputters and goes out for good at this time.")]
    public float torchDiesAt = 25.5f;

    [Header("Shadows")]
    [Tooltip("Seconds before the first shadow appears (from behind).")]
    public float firstShadowAt = 6f;
    [Tooltip("Seconds before the second shadow appears (from the front).")]
    public float secondShadowAt = 10f;
    [Tooltip("Time at which the swarm reaches full strength.")]
    public float swarmFullAt = 22f;
    [Tooltip("The screen goes black by this time even if the swarm has not finished the job.")]
    public float endsBy = 33f;
    [Tooltip("Average seconds of torchlight needed to burn one shadow away (each shadow varies).")]
    public float burnTime = 0.9f;
    [Tooltip("How many shadows must grab the player before the screen goes black.")]
    public int shadowsToSmother = 10;

    // These match the baked art; change them only if you redraw the textures.
    const float ConeHalfAngle = 26f;
    const float ConeRange = 8.5f;
    const float DarknessWorldSize = 52f;
    const float GroundY = -3f;
    const float PlayerScale = 0.53f;
    const float ShoulderY = 2.39f * PlayerScale;
    const float ShoulderX = 0.32f * PlayerScale;
    const float ArmScale = 0.4f;
    const float ShadowScale = 0.62f;
    const float WorldPixelsPerUnit = 12.8f;   // 128 px tall background = 10 units
    const float WorldLeftEdge = -12f;
    const float WorldWidth = 80f;             // 1024 px corridor
    const string Dir = "Shadow2D/";

    class Shadow
    {
        public Transform tr;
        public SpriteRenderer body, eyes, grin;
        public float hp = 1f, speed, phase, fade = 1f, size = ShadowScale, resistance = 1f, shrink = 1f;
        public int death, dir = 1;
        public bool attached, dying, fromAbove;
        public Vector2 offset;
    }

    Camera cam;
    Transform player, arm, darkness;
    SpriteRenderer bodySr, armSr, glowSr, blackoutSr;
    Sprite sprIdle, sprWalk1, sprWalk2, sprShadow, sprEyes, sprGrin;
    Image cover;
    Graphic hint;
    AudioSource ambience;
    readonly List<Shadow> shadows = new List<Shadow>();

    float t, px, playerVx, camX, aim = 20f, walkClock, nextSpawn;
    float light = 1f, flickUntil, flickDepth = 1f, nextFlick;
    int lastDir = 1, attachedCount, spawnedCount;
    bool ended;

    static readonly string[] IntroKeys = { "Intro_01", "Intro_02", "Intro_03" };
    bool intro = true, barkFirst, barkSecond, barkFlicker, barkMid, barkDead;
    int introLine = -1;
    float introClock, sceneClock, saidAt, sayUntil, ghostAlpha;
    SpriteRenderer ghostSr;
    CanvasGroup dialogueGroup;
    Text speakerText, bodyText;
    bool dialogueShown;

    // ------------------------------------------------------------------ setup

    void Start()
    {
        nextFlick = flickerStartsAt;
        SetupCamera();
        BuildWorld();
        BuildPlayer();
        BuildUI();

        var clip = Resources.Load<AudioClip>(Dir + "ambience");
        if (clip != null)
        {
            ambience = gameObject.AddComponent<AudioSource>();
            ambience.clip = clip;
            ambience.loop = true;
            ambience.volume = 0f;
            ambience.Play();
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
        cam.orthographic = true;
        cam.orthographicSize = 5f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        cam.transform.rotation = Quaternion.identity;
        cam.transform.position = new Vector3(0f, 0f, -10f);
    }

    Sprite LoadSprite(string file, Vector2 pivot, float pixelsPerUnit = 100f, bool pixelArt = false)
    {
        var tex = Resources.Load<Texture2D>(Dir + file);
        if (tex == null)
        {
            Debug.LogError("ShadowRun2D: missing Assets/Resources/" + Dir + file + ".png");
            return null;
        }
        if (pixelArt) tex.filterMode = FilterMode.Point;
        return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), pivot, pixelsPerUnit, 0, SpriteMeshType.FullRect);
    }

    SpriteRenderer MakeSprite(string name, Sprite sprite, int order, Transform parent)
    {
        var g = new GameObject(name);
        g.transform.SetParent(parent, false);
        var sr = g.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = order;
        return sr;
    }

    void BuildWorld()
    {
        // One long, non-repeating pixel-art corridor (ground included), 80 units wide.
        var bg = MakeSprite("World", LoadSprite("bg", new Vector2(0f, 0.5f), WorldPixelsPerUnit, true), 0, transform);
        bg.transform.position = new Vector3(WorldLeftEdge, 0f, 0f);

        // The same corridor's glowing parts (sparkles, mushrooms, reed tips, the door seam), drawn faintly over the dark.
        glowSr = MakeSprite("WorldGlow", LoadSprite("bg_glow", new Vector2(0f, 0.5f), WorldPixelsPerUnit, true), 105, transform);
        glowSr.transform.position = new Vector3(WorldLeftEdge, 0f, 0f);

        // The front halves of the tall reeds, drawn over the player so he wades through them.
        // Optional: if bg_front.png is missing the scene simply has no foreground reeds.
        if (Resources.Load<Texture2D>(Dir + "bg_front") != null)
        {
            var front = MakeSprite("WorldFront", LoadSprite("bg_front", new Vector2(0f, 0.5f), WorldPixelsPerUnit, true), 35, transform);
            front.transform.position = new Vector3(WorldLeftEdge, 0f, 0f);
        }
    }

    void BuildPlayer()
    {
        var feet = new Vector2(0.5f, 12f / 512f);
        sprIdle = LoadSprite("player_idle", feet, 100f, true);
        sprWalk1 = LoadSprite("player_walk1", feet, 100f, true);
        sprWalk2 = LoadSprite("player_walk2", feet, 100f, true);
        var shadowFeet = new Vector2(0.5f, 6f / 512f);
        sprShadow = LoadSprite("shadow", shadowFeet, 100f, true);
        sprEyes = LoadSprite("shadow_eyes", shadowFeet, 100f, true);
        sprGrin = LoadSprite("shadow_grin", shadowFeet, 100f, true);

        player = new GameObject("Player").transform;
        player.SetParent(transform, false);
        player.position = new Vector3(0f, GroundY, 0f);

        bodySr = MakeSprite("Body", sprIdle, 30, player);
        bodySr.transform.localScale = Vector3.one * PlayerScale;

        armSr = MakeSprite("TorchArm", LoadSprite("arm_torch", new Vector2(30f / 512f, 0.5f), 100f, true), 31, player);
        arm = armSr.transform;
        arm.localScale = Vector3.one * ArmScale;

        // One big sprite: black everywhere except a soft cone and a small circle around the player.
        var dsr = MakeSprite("Darkness", LoadSprite("darkness", new Vector2(0.5f, 0.5f), 1024f / DarknessWorldSize, true), 100, transform);
        darkness = dsr.transform;
        var skirtTex = new Texture2D(1, 1);
        skirtTex.SetPixel(0, 0, Color.white);
        skirtTex.Apply();
        var skirtSprite = Sprite.Create(skirtTex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        float half = DarknessWorldSize * 0.5f, big = 400f;
        Vector2[] skirtPos = { new Vector2(0f, half + big * 0.5f), new Vector2(0f, -half - big * 0.5f), new Vector2(half + big * 0.5f, 0f), new Vector2(-half - big * 0.5f, 0f) };
        Vector2[] skirtSize = { new Vector2(DarknessWorldSize + big * 2f, big), new Vector2(DarknessWorldSize + big * 2f, big), new Vector2(big, DarknessWorldSize), new Vector2(big, DarknessWorldSize) };
        for (int i = 0; i < 4; i++)
        {
            var sk = MakeSprite("DarknessSkirt", skirtSprite, 100, darkness);
            sk.transform.localPosition = skirtPos[i];
            sk.transform.localScale = new Vector3(skirtSize[i].x, skirtSize[i].y, 1f);
            sk.color = new Color(0f, 0f, 0f, 247f / 255f);
        }

        // Plain black sheet used for the torch's short blackouts.
        var tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        blackoutSr = MakeSprite("Blackout", Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f), 101, transform);
        blackoutSr.transform.localScale = new Vector3(60f, 30f, 1f);
        blackoutSr.color = new Color(0f, 0f, 0f, 0f);

        // The ghost, standing a few steps ahead. He glows, so he is drawn above the darkness.
        ghostSr = MakeSprite("Ghost", LoadSprite("ghost", feet, 100f, true), 106, transform);
        ghostSr.transform.localScale = Vector3.one * PlayerScale;
        ghostSr.flipX = true;
        SetAlpha(ghostSr, 0f);
    }

    void BuildUI()
    {
        var cgo = new GameObject("Shadow2D UI");
        cgo.transform.SetParent(transform, false);
        var canvas = cgo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        var scaler = cgo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        var frame = LoadSprite("frame", new Vector2(0.5f, 0.5f));
        if (frame != null) FullScreenImage(cgo.transform, "ComicFrame", frame, Color.white);

        var hg = new GameObject("Commands");
        hg.transform.SetParent(cgo.transform, false);
        if (commandFont != null)
        {
            var text = hg.AddComponent<Text>();
            text.font = commandFont;
            text.fontSize = 36;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(0.95f, 0.95f, 0.95f);
            text.text = commandText;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            var outline = hg.AddComponent<Outline>();
            outline.effectColor = new Color(0.02f, 0.02f, 0.05f);
            outline.effectDistance = new Vector2(3f, -3f);
            hint = text;
        }
        else
        {
            var im = hg.AddComponent<Image>();
            im.sprite = LoadSprite("hint", new Vector2(0.5f, 0.5f));
            hint = im;
        }
        hint.raycastTarget = false;
        var rt = hint.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.sizeDelta = new Vector2(1024f, 128f);
        rt.anchoredPosition = new Vector2(0f, 60f);

        cover = FullScreenImage(cgo.transform, "Smother", null, Color.black);

        if (dialogueSet == null) dialogueSet = Resources.Load<DialogueSet>("Dialogue/Shadow2D_Dialogue");
        if (dialogueSet == null) Debug.LogError("ShadowRun2D: no dialogue set found at Resources/Dialogue/Shadow2D_Dialogue");
        Font dlgFont = Resources.Load<Font>("WarmStatues/DotGothic16");
        if (dlgFont == null) dlgFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        var dg = new GameObject("Dialogue", typeof(RectTransform));
        dg.transform.SetParent(cgo.transform, false);
        var drt = (RectTransform)dg.transform;
        drt.anchorMin = drt.anchorMax = new Vector2(0.5f, 1f);
        drt.pivot = new Vector2(0.5f, 1f);
        drt.sizeDelta = new Vector2(1060f, 230f);
        drt.anchoredPosition = new Vector2(0f, -76f);
        dialogueGroup = dg.AddComponent<CanvasGroup>();
        dialogueGroup.alpha = 0f;
        dialogueGroup.blocksRaycasts = false;
        var border = dg.AddComponent<Image>();
        border.color = new Color(0.93f, 0.93f, 0.93f, 1f);
        border.raycastTarget = false;
        var inner = new GameObject("Panel", typeof(RectTransform));
        inner.transform.SetParent(dg.transform, false);
        var irt = (RectTransform)inner.transform;
        irt.anchorMin = Vector2.zero; irt.anchorMax = Vector2.one;
        irt.offsetMin = new Vector2(7f, 7f); irt.offsetMax = new Vector2(-7f, -7f);
        var panel = inner.AddComponent<Image>();
        panel.color = new Color(0.03f, 0.03f, 0.03f, 0.97f);
        panel.raycastTarget = false;
        speakerText = DialogueText(inner.transform, "Speaker", dlgFont, 38, new Vector2(28f, -18f), new Vector2(980f, 50f));
        bodyText = DialogueText(inner.transform, "Line", dlgFont, 36, new Vector2(28f, -70f), new Vector2(990f, 140f));
        bodyText.color = new Color(0.94f, 0.94f, 0.94f);
    }

    Text DialogueText(Transform parent, string name, Font font, int size, Vector2 pos, Vector2 box)
    {
        var g = new GameObject(name, typeof(RectTransform));
        g.transform.SetParent(parent, false);
        var rt = (RectTransform)g.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = box;
        var tx = g.AddComponent<Text>();
        tx.font = font;
        tx.fontSize = size;
        tx.alignment = TextAnchor.UpperLeft;
        tx.horizontalOverflow = HorizontalWrapMode.Wrap;
        tx.verticalOverflow = VerticalWrapMode.Overflow;
        tx.lineSpacing = 1.1f;
        tx.raycastTarget = false;
        return tx;
    }

    Image FullScreenImage(Transform parent, string name, Sprite sprite, Color color)
    {
        var g = new GameObject(name);
        g.transform.SetParent(parent, false);
        var im = g.AddComponent<Image>();
        im.sprite = sprite;
        im.color = color;
        im.raycastTarget = false;
        var rt = im.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return im;
    }

    // ------------------------------------------------------------------ input

    static float MoveAxis()
    {
#if ENABLE_INPUT_SYSTEM
        var k = Keyboard.current;
        if (k == null) return 0f;
        float v = 0f;
        if (k.aKey.isPressed || k.leftArrowKey.isPressed) v -= 1f;
        if (k.dKey.isPressed || k.rightArrowKey.isPressed) v += 1f;
        return v;
#else
        return Input.GetAxisRaw("Horizontal");
#endif
    }

    // Space, Enter or a click moves the conversation on. Walking keys do not, so he can move while it plays.
    static bool AnyKeyDown()
    {
#if ENABLE_INPUT_SYSTEM
        return (Keyboard.current != null && (Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.enterKey.wasPressedThisFrame))
            || (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame);
#else
        return Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetMouseButtonDown(0);
#endif
    }

    static Vector2 MousePosition()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
#else
        return Input.mousePosition;
#endif
    }

    // ------------------------------------------------------------------ loop

    void Update()
    {
        float dt = Time.deltaTime;
        sceneClock += dt;
        if (intro) UpdateIntro(dt); else t += dt;
        UpdateGhostAndDialogue(dt);

        float cover01 = Mathf.Clamp01(attachedCount / (float)Mathf.Max(1, shadowsToSmother));

        UpdateTorchLight();
        playerVx = 0f;
        if (!ended)
        {
            UpdatePlayer(dt, cover01);
            if (!intro)
            {
                UpdateSpawning();
                UpdateCaptions();
            }
        }
        UpdateShadows(dt);

        // Camera follows the player along the corridor.
        camX = Mathf.Lerp(camX, px, 4f * dt);
        float halfW = cam.orthographicSize * cam.aspect;
        camX = Mathf.Clamp(camX, WorldLeftEdge + halfW, WorldLeftEdge + WorldWidth - halfW);
        cam.transform.position = new Vector3(camX, 0f, -10f);

        // Darkness sits on the torch shoulder and turns with the aim; its size follows the torch's light.
        Vector3 shoulder = Shoulder();
        darkness.position = shoulder;
        darkness.rotation = Quaternion.Euler(0f, 0f, aim);
        float flick = 1f + 0.02f * Mathf.Sin(t * 17f) * Mathf.Sin(t * 5.3f);
        darkness.localScale = Vector3.one * flick * Mathf.Max(0.25f, light);

        blackoutSr.transform.position = new Vector3(camX, 0f, 0f);
        blackoutSr.color = new Color(0f, 0f, 0f, Mathf.Clamp01((0.5f - light) / 0.5f) * 0.92f);
        if (ambience != null && !ended) ambience.volume = musicVolume * Mathf.Clamp01(sceneClock / Mathf.Max(0.05f, musicFadeIn));
        if (glowSr != null) SetAlpha(glowSr, 0.42f + 0.1f * Mathf.Sin(t * 1.3f));

        if (!ended)
        {
            float fadeIn = 1f - Mathf.Clamp01(sceneClock / 1.2f);
            SetAlpha(cover, Mathf.Max(fadeIn, Mathf.SmoothStep(0f, 1f, cover01) * 0.9f));
            SetAlpha(hint, Mathf.Clamp01(t / 1.5f) * (1f - Mathf.Clamp01((t - 7f) / 1.5f)));
            if (attachedCount >= shadowsToSmother || t >= endsBy) StartCoroutine(Smothered());
        }
    }

    // ------------------------------------------------------------------ dialogue

    float Say(string key)
    {
        if (dialogueSet == null) return 0f;
        DialogueSet.Line line = dialogueSet.Find(key);
        if (line == null) { Debug.LogWarning("ShadowRun2D: no dialogue line named " + key); return 0f; }
        DialogueSet.Speaker sp = dialogueSet.FindSpeaker(line.speakerId);
        speakerText.text = sp != null ? sp.displayName : line.speakerId;
        speakerText.color = sp != null ? sp.nameColor : Color.white;
        bodyText.text = line.text;
        float hold = dialogueSet.HoldFor(line);
        dialogueShown = true;
        saidAt = sceneClock;
        sayUntil = sceneClock + hold;
        return hold;
    }

    float IntroHold(int index)
    {
        return dialogueSet != null ? dialogueSet.HoldFor(dialogueSet.Find(IntroKeys[index])) : 4f;
    }

    void UpdateIntro(float dt)
    {
        if (sceneClock < 1f) return;                       // let the fade-in begin first
        if (introLine < 0)
        {
            introLine = 0;
            introClock = 0f;
            Say(IntroKeys[0]);
        }
        introClock += dt;
        bool skip = introClock > 0.35f && AnyKeyDown();
        if (introClock >= IntroHold(introLine) || skip)
        {
            introLine++;
            introClock = 0f;
            if (introLine >= IntroKeys.Length)
            {
                intro = false;                             // the run and all its timers start now
                sayUntil = sceneClock;
                return;
            }
            Say(IntroKeys[introLine]);
        }
    }

    void UpdateCaptions()
    {
        if (!barkFirst && spawnedCount >= 1) { barkFirst = true; Say("Bark_FirstShadow"); }
        if (!barkSecond && spawnedCount >= 2) { barkSecond = true; Say("Bark_SecondShadow"); }
        if (!barkFlicker && t >= flickerStartsAt + 3f) { barkFlicker = true; Say("Bark_TorchFlicker"); }
        if (!barkMid && t >= 19f) { barkMid = true; Say("Bark_Halfway"); }
        if (!barkDead && t >= torchDiesAt) { barkDead = true; Say("Bark_TorchDies"); }
    }

    void UpdateGhostAndDialogue(float dt)
    {
        // The ghost fades in for the conversation and fades away when the run starts.
        float want = intro ? Mathf.Clamp01((sceneClock - 0.6f) / 0.8f) * 0.85f : 0f;
        ghostAlpha = intro ? want : Mathf.MoveTowards(ghostAlpha, 0f, dt);
        ghostSr.transform.position = new Vector3(ghostX, GroundY + Mathf.Sin(sceneClock * 1.6f) * 0.06f, 0f);
        SetAlpha(ghostSr, ghostAlpha * (0.93f + 0.07f * Mathf.Sin(sceneClock * 23f)));

        float a = !dialogueShown ? 0f
            : Mathf.Clamp01((sceneClock - saidAt) / 0.2f) * (1f - Mathf.Clamp01((sceneClock - sayUntil) / 0.4f));
        if (dialogueGroup != null) dialogueGroup.alpha = a;
    }

    Vector3 Shoulder()
    {
        return player.position + new Vector3(aim > 90f ? -ShoulderX : ShoulderX, ShoulderY, 0f);
    }

    // Torch flicker: dips and short blackouts that get more frequent as time passes.
    void UpdateTorchLight()
    {
        if (t >= nextFlick && t >= flickUntil)
        {
            float k = Mathf.Clamp01((t - flickerStartsAt) / 12f);
            bool blackout = Random.value < 0.15f + 0.35f * k;
            flickDepth = blackout ? 0f : Random.Range(0.35f, 0.75f);
            flickUntil = t + Random.Range(0.08f, 0.3f) + (blackout ? 0.1f : 0f);
            nextFlick = flickUntil + Mathf.Lerp(3.2f, 0.6f, k) * Random.Range(0.6f, 1.4f);
        }
        light = t < flickUntil ? flickDepth * (0.8f + 0.2f * Mathf.Sin(t * 90f)) : 1f;
        // Sputters for a moment, then dies for good.
        if (t > torchDiesAt) light = 0f;
        else if (t > torchDiesAt - 0.9f) light *= Mathf.Max(0f, Mathf.Sin(t * 47f)) * (torchDiesAt - t) / 0.9f;
    }

    void UpdatePlayer(float dt, float cover01)
    {
        float move = MoveAxis();   // he can walk while the ghost speaks
        if (move != 0f) lastDir = move > 0f ? 1 : -1;
        playerVx = move * walkSpeed * (1f - 0.85f * cover01);
        bool held = intro && introLine < introLinesBeforeWalking;
        float right = held ? Mathf.Min(worldRight, ghostX - stopBeforeGhost) : worldRight;
        px = Mathf.Clamp(px + playerVx * dt, worldLeft, right);
        if (held && px >= right - 0.001f && playerVx > 0f) playerVx = 0f;

        float bob = 0f;
        if (move != 0f)
        {
            walkClock += dt;
            bodySr.sprite = ((int)(walkClock / 0.16f) % 2 == 0) ? sprWalk1 : sprWalk2;
            bob = Mathf.Abs(Mathf.Sin(walkClock * Mathf.PI / 0.16f)) * 0.05f;
        }
        else
        {
            bodySr.sprite = sprIdle;
        }
        player.position = new Vector3(px, GroundY + bob, 0f);

        // Aim: mouse direction from the shoulder, clamped to the upper half (0 = right, 180 = left).
        Vector2 mouse = MousePosition();
        Vector3 world = cam.ScreenToWorldPoint(new Vector3(mouse.x, mouse.y, 10f));
        Vector2 d = (Vector2)world - (Vector2)Shoulder();
        if (d.sqrMagnitude > 0.04f)
        {
            float a = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            if (a < 0f) a = a < -90f ? 180f : 0f;
            aim = Mathf.MoveTowardsAngle(aim, a, 540f * dt);
        }
        bool left = aim > 90f;
        arm.position = Shoulder();
        arm.rotation = Quaternion.Euler(0f, 0f, aim);
        arm.localScale = new Vector3(ArmScale, ArmScale * (1f + 0.06f * Mathf.Sin(t * 23f)), 1f);   // flame shiver
        armSr.flipY = left;
        bodySr.flipX = left;
    }

    // First from behind, second from the front, third from behind, then whichever side has fewer.
    int PickSide()
    {
        spawnedCount++;
        if (spawnedCount == 1 || spawnedCount == 3) return -lastDir;
        if (spawnedCount == 2) return lastDir;
        int l = 0, r = 0;
        foreach (var s in shadows)
        {
            if (s.attached || s.dying || s.fromAbove) continue;
            if (s.tr.position.x < px) l++; else r++;
        }
        if (l != r) return l < r ? -1 : 1;
        return Random.value < 0.5f ? -1 : 1;
    }

    void UpdateSpawning()
    {
        if (spawnedCount == 0 && t >= firstShadowAt) Spawn(PickSide(), false, 0f);
        if (spawnedCount == 1 && t >= secondShadowAt)
        {
            Spawn(PickSide(), false, 0f);
            nextSpawn = t + 4f;
        }
        if (spawnedCount >= 2 && t >= nextSpawn)
        {
            float k = Mathf.InverseLerp(secondShadowAt, swarmFullAt, t);
            int count = k > 0.8f ? 2 : 1;
            for (int i = 0; i < count; i++)
            {
                bool above = k > 0.25f && Random.value < 0.3f;
                Spawn(PickSide(), above, k);
            }
            nextSpawn = t + (t > torchDiesAt ? 0.22f : Mathf.Lerp(2.6f, 0.3f, k));
        }
    }

    void Spawn(int side, bool fromAbove, float k)
    {
        if (sprShadow == null) return;
        var g = new GameObject("Shadow");
        g.transform.SetParent(transform, false);
        var s = new Shadow();
        s.tr = g.transform;
        s.body = g.AddComponent<SpriteRenderer>();
        s.body.sprite = sprShadow;
        s.body.sortingOrder = 20;
        s.eyes = MakeSprite("Eyes", sprEyes, 110, s.tr);   // above the darkness, so the eyes always show
        s.grin = MakeSprite("Grin", sprGrin, 111, s.tr);   // only appears when it is close
        SetAlpha(s.grin, 0f);
        s.size = ShadowScale * Random.Range(0.85f, 1.25f);
        s.tr.localScale = Vector3.one * s.size;
        s.speed = Mathf.Lerp(1.1f, 2.4f, k) * Random.Range(0.85f, 1.15f);
        s.phase = Random.value * 10f;
        s.resistance = Random.Range(0.5f, 1.7f);           // each one takes a different time to burn
        s.death = Random.Range(0, 4);                      // and vanishes in a different way
        s.dir = Random.value < 0.5f ? -1 : 1;
        s.fromAbove = fromAbove;

        float halfWidth = cam.orthographicSize * cam.aspect;
        if (fromAbove) s.tr.position = new Vector3(px + Random.Range(-7f, 7f), cam.orthographicSize + 1.5f, 0f);
        else s.tr.position = new Vector3(camX + side * (halfWidth + 1.5f), GroundY, 0f);
        shadows.Add(s);
    }

    void UpdateShadows(float dt)
    {
        Vector2 shoulder = Shoulder();
        Vector2 target = (Vector2)player.position + Vector2.up * 1.2f;
        float burnPower = light > 0.5f ? 1f : 0f;
        float range = ConeRange * Mathf.Max(0.25f, light);

        for (int i = shadows.Count - 1; i >= 0; i--)
        {
            Shadow s = shadows[i];
            float eyeFlicker = 0.8f + 0.2f * Mathf.Sin(t * 31f + s.phase * 7f);

            if (s.dying)
            {
                s.fade -= dt * (1.4f + s.death * 0.5f);
                if (s.death == 0) s.tr.position += Vector3.up * dt * 0.9f;                // drifts up
                else if (s.death == 1) s.tr.position += Vector3.down * dt * 1.6f;         // sinks into the floor
                else if (s.death == 2) s.tr.localScale = Vector3.one * s.size * Mathf.Max(0.05f, s.fade);   // shrivels
                else s.tr.position += Vector3.right * s.dir * dt * 3.5f;                  // torn sideways
                float a = Mathf.Max(0f, s.fade);
                SetAlpha(s.body, a);
                SetAlpha(s.eyes, a * eyeFlicker);
                SetAlpha(s.grin, 0f);
                if (s.fade <= 0f)
                {
                    Destroy(s.tr.gameObject);
                    shadows.RemoveAt(i);
                }
                continue;
            }

            SetAlpha(s.eyes, eyeFlicker);

            if (s.attached)
            {
                // Clings to the player and slowly swells.
                s.size = Mathf.MoveTowards(s.size, ShadowScale * 1.6f, dt * 0.08f);
                s.tr.localScale = Vector3.one * s.size;
                Vector2 wobble = new Vector2(Mathf.Sin(t * 2.1f + s.phase), Mathf.Cos(t * 1.7f + s.phase)) * 0.08f;
                s.tr.position = (Vector2)player.position + s.offset + wobble;
                SetAlpha(s.grin, 1f);
                continue;
            }

            Vector2 pos = s.tr.position;
            Vector2 chest = pos + Vector2.up * 2f * s.size;
            Vector2 toPlayer = target - chest;
            float dist = Mathf.Max(0.001f, toPlayer.magnitude);

            Vector2 fromTorch = chest - shoulder;
            float ang = Mathf.Atan2(fromTorch.y, fromTorch.x) * Mathf.Rad2Deg;
            bool lit = !ended && light > 0.3f && fromTorch.magnitude < range && Mathf.Abs(Mathf.DeltaAngle(aim, ang)) < ConeHalfAngle;

            // Lunging gait. A shadow behind a fleeing player keeps up with him.
            float lunge = 1f + 0.7f * Mathf.Pow(Mathf.Max(0f, Mathf.Sin(t * 2.3f + s.phase)), 4f);
            bool fleeing = playerVx != 0f && Mathf.Sign(playerVx) == Mathf.Sign(toPlayer.x);
            float v = (s.speed * lunge + (fleeing ? Mathf.Abs(playerVx) * 0.8f : 0f)) * (t > torchDiesAt ? 1.7f : 1f);

            if (lit)
            {
                s.hp -= dt / Mathf.Max(0.05f, burnTime * s.resistance) * burnPower;
                if (s.fromAbove) pos -= toPlayer / dist * 1.2f * dt;
                else pos.x -= Mathf.Sign(toPlayer.x) * 1.2f * dt;
                pos.x += Mathf.Sin(t * 60f + s.phase) * 0.012f;   // shivers in the light
            }
            else
            {
                s.hp = Mathf.Min(1f, s.hp + dt * 0.25f);
                if (s.fromAbove) { if (dist > 0.75f) pos += toPlayer / dist * v * dt; }
                else if (Mathf.Abs(toPlayer.x) > 0.55f) pos.x += Mathf.Sign(toPlayer.x) * v * dt;
            }
            if (!s.fromAbove) pos.y = GroundY + Mathf.Sin(t * 3f + s.phase) * 0.04f;
            s.tr.position = pos;
            SetAlpha(s.body, Mathf.Lerp(0.3f, 1f, Mathf.Clamp01(s.hp)));
            SetAlpha(s.grin, Mathf.Clamp01((4f - dist) / 2f));

            if (s.hp <= 0f)
            {
                s.dying = true;
                continue;
            }

            bool reached = s.fromAbove ? dist < 0.8f : Mathf.Abs(toPlayer.x) < 0.6f;
            // Only a few may grab him while the torch still burns; the rest wait for the dark.
            bool mayGrab = t > torchDiesAt || attachedCount < shadowsToSmother - 4;
            if (reached && !ended && mayGrab)
            {
                s.attached = true;
                s.offset = new Vector2(Random.Range(-0.7f, 0.7f), Random.Range(-0.4f, 0.5f));
                s.body.sortingOrder = 40;        // in front of the player now
                SetAlpha(s.body, 1f);
                attachedCount++;
            }
        }
    }

    IEnumerator Smothered()
    {
        ended = true;
        Say("Bark_Smothered");
        float start = cover.color.a;
        float startVol = ambience != null ? ambience.volume : 0f;
        for (float f = 0f; f < 1.5f; f += Time.deltaTime)
        {
            SetAlpha(cover, Mathf.Lerp(start, 1f, f / 1.5f));
            if (ambience != null) ambience.volume = Mathf.Lerp(startVol, 0f, f / 1.5f);
            yield return null;
        }
        SetAlpha(cover, 1f);
        yield return new WaitForSeconds(1.5f);

        FPCharacter.VoiceDirector.QueueOnReturn("Return_PrismaticMinigame");
        if (string.IsNullOrEmpty(restartScene)) SceneManager.LoadScene(0);
        else SceneManager.LoadScene(restartScene);
    }

    static void SetAlpha(SpriteRenderer r, float a) { if (r == null) return; var c = r.color; c.a = a; r.color = c; }
    static void SetAlpha(Graphic g, float a) { if (g == null) return; var c = g.color; c.a = a; g.color = c; }
}
