using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class ComicPanelTransition : MonoBehaviour
{
    public string nextScene = "Shadow2D";
    public bool puzzleSolved = false;
    public string playerTag = "Player";
    public MonoBehaviour playerMovement;

    [Header("Next panel")]
    public string heroFolder = "Shadow2D";
    public Texture2D nextPanel;
    public Rect nextPanelUv = new Rect(0f, 0f, 227f / 1024f, 1f);
    public Vector2 heroTarget = new Vector2(0.677f, 0.2f);
    public float heroHeight = 0.271f;

    [Header("Music")]
    public AudioClip nextMusic;
    [Range(0f, 1f)] public float nextMusicVolume = 0.9f;
    public float musicFadeOut = 2.5f;
    public float musicFadeIn = 3f;
    public BlackoutTransition blackout;

    struct Settings
    {
        public string folder;
        public Texture2D panel;
        public Rect uv;
        public Vector2 target;
        public float height;
    }

    static bool running;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { running = false; carried = null; }

    class Runner : MonoBehaviour { }

    class MusicCarry : MonoBehaviour
    {
        public AudioSource src;
        public float target;
        public float rate;
        public bool dying;

        void Update()
        {
            src.volume = Mathf.MoveTowards(src.volume, dying ? 0f : target, rate * Time.unscaledDeltaTime);
            if (dying && src.volume <= 0.001f) Destroy(gameObject);
        }

        void OnDestroy() { if (carried == this) carried = null; }
    }

    static MusicCarry carried;

    public static bool AdoptMusic(AudioSource target)
    {
        if (carried == null || target == null || carried.src.clip != target.clip) return false;
        target.time = Mathf.Repeat(carried.src.time, target.clip.length);
        if (!target.isPlaying) target.Play();
        Destroy(carried.gameObject);
        carried = null;
        return true;
    }

    void StartMusic()
    {
        FPCharacter.ZoneMusic.FadeOutAll(musicFadeOut);
        if (nextMusic == null) return;
        if (carried != null) Destroy(carried.gameObject);
        var go = new GameObject("Carried Music");
        DontDestroyOnLoad(go);
        AudioSource s = go.AddComponent<AudioSource>();
        s.clip = nextMusic;
        s.loop = true;
        s.spatialBlend = 0f;
        s.volume = 0f;
        s.Play();
        carried = go.AddComponent<MusicCarry>();
        carried.src = s;
        carried.target = nextMusicVolume;
        carried.rate = Mathf.Max(0.01f, nextMusicVolume) / Mathf.Max(0.05f, musicFadeIn);
    }

    const float PageW = 2400f;
    const float PageH = 1600f;
    const float Inset = 7f;
    static readonly Rect TL = new Rect(40f, 40f, 1140f, 641f);
    static readonly Rect TR = new Rect(1220f, 40f, 1140f, 641f);
    static readonly Rect[] Others =
    {
        new Rect(40f, 721f, 740f, 400f),
        new Rect(820f, 721f, 760f, 400f),
        new Rect(1620f, 721f, 740f, 400f),
        new Rect(40f, 1161f, 1440f, 399f),
        new Rect(1520f, 1161f, 840f, 399f)
    };

    public void SetPuzzleSolved() { puzzleSolved = true; }

    void OnTriggerEnter(Collider other) { if (puzzleSolved && other.CompareTag(playerTag)) Play(); }
    void OnTriggerEnter2D(Collider2D other) { if (puzzleSolved && other.CompareTag(playerTag)) Play(); }

    public void Play()
    {
        if (blackout != null)
        {
            blackout.playerMovement = playerMovement;
            blackout.Play();
            return;
        }
        if (running) return;
        running = true;
        if (playerMovement != null) playerMovement.enabled = false;
        StartMusic();
        var go = new GameObject("ComicPanelTransition (running)");
        DontDestroyOnLoad(go);
        var settings = new Settings { folder = string.IsNullOrEmpty(heroFolder) ? "Shadow2D" : heroFolder, panel = nextPanel, uv = nextPanelUv, target = heroTarget, height = heroHeight };
        go.AddComponent<Runner>().StartCoroutine(Run(go, nextScene, settings));
    }

    static IEnumerator Run(GameObject root, string sceneName, Settings cfg)
    {
        yield return new WaitForEndOfFrame();
        Texture2D shot = ScreenCapture.CaptureScreenshotAsTexture();
        Texture2D bg = cfg.panel != null ? cfg.panel : Resources.Load<Texture2D>(cfg.folder + "/bg");
        Sprite[] walk = { LoadSprite(cfg.folder, "player_walk1"), LoadSprite(cfg.folder, "player_walk2") };
        Sprite idle = LoadSprite(cfg.folder, "player_idle");
        RenderTexture blurShot = Blur(shot, 48, 27);
        RenderTexture blurBg = bg != null ? (cfg.panel != null ? Blur(bg, 48, 27) : Blur(bg, 64, 8)) : null;

        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;
        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        var group = root.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;

        var backdrop = NewImage(root.transform, "Backdrop", new Color(0.04f, 0.035f, 0.03f));
        Stretch(backdrop.rectTransform);

        var pageImg = NewImage(root.transform, "Page", new Color(0.93f, 0.9f, 0.82f));
        RectTransform page = pageImg.rectTransform;
        page.anchorMin = page.anchorMax = new Vector2(0.5f, 0.5f);
        page.pivot = new Vector2(0f, 1f);
        page.sizeDelta = new Vector2(PageW, PageH);

        RawImage tlInner = Panel(page, TL, shot, CoverUv(shot, TL));
        Rect bgUv = cfg.panel != null ? cfg.uv : new Rect(0f, 0f, 227f / 1024f, 1f);
        RawImage trInner = Panel(page, TR, bg, bgUv);
        trInner.color = Color.black;

        Color[] tints =
        {
            new Color(1f, 0.85f, 0.7f), new Color(0.7f, 0.85f, 1f), new Color(1f, 0.6f, 0.55f),
            new Color(0.85f, 0.8f, 1f), new Color(0.65f, 1f, 0.9f)
        };
        Rect[] uvs =
        {
            new Rect(0.05f, 0.25f, 0.45f, 0.55f), new Rect(0.3f, 0f, 0.22f, 1f), new Rect(0.5f, 0.1f, 0.45f, 0.65f),
            new Rect(0.48f, 0f, 0.5f, 1f), new Rect(0.25f, 0.35f, 0.4f, 0.45f)
        };
        var blurred = new RawImage[Others.Length];
        for (int i = 0; i < Others.Length; i++)
        {
            bool useBg = (i == 1 || i == 3) && blurBg != null;
            blurred[i] = Panel(page, Others[i], useBg ? (Texture)blurBg : blurShot, uvs[i]);
            blurred[i].color = new Color(tints[i].r, tints[i].g, tints[i].b, 0f);
        }

        var heroGo = new GameObject("Hero");
        heroGo.transform.SetParent(page, false);
        var hero = heroGo.AddComponent<Image>();
        hero.raycastTarget = false;
        hero.sprite = walk[0];
        hero.preserveAspect = true;
        hero.color = new Color(1f, 1f, 1f, 0f);
        RectTransform hr = hero.rectTransform;
        hr.anchorMin = hr.anchorMax = new Vector2(0f, 1f);
        hr.pivot = new Vector2(0.5f, 12f / 512f);
        float innerH = TR.height - Inset * 2f;
        float heroSize = innerH * cfg.height;
        hr.sizeDelta = new Vector2(heroSize, heroSize);
        float groundY = TR.y + Inset + innerH * (1f - cfg.target.y);
        Vector2 heroStart = new Vector2(TL.x + Inset + (TL.width - Inset * 2f) * 0.42f, groundY);
        Vector2 heroEnd = new Vector2(TR.x + Inset + (TR.width - Inset * 2f) * cfg.target.x, groundY);
        hr.anchoredPosition = new Vector2(heroStart.x, -heroStart.y);

        var blackImg = NewImage(root.transform, "Black", Color.black);
        Stretch(blackImg.rectTransform);
        SetAlpha(blackImg, 0f);

        Canvas.ForceUpdateCanvases();
        yield return null;
        Vector2 screen = ((RectTransform)root.transform).rect.size;

        float kTL; Vector2 pTL; Focus(TL, screen, true, out kTL, out pTL);
        float kTR; Vector2 pTR; Focus(TR, screen, true, out kTR, out pTR);
        float kPage = Mathf.Min(screen.x / PageW, screen.y / PageH) * 0.94f;
        Vector2 pPage = new Vector2(-kPage * PageW * 0.5f, kPage * PageH * 0.5f);
        SetCam(page, kTL, pTL);

        for (float f = 0f; f < 1f; f += Time.unscaledDeltaTime / 1.3f)
        {
            float e = Mathf.SmoothStep(0f, 1f, f);
            float k = Mathf.Exp(Mathf.Lerp(Mathf.Log(kTL), Mathf.Log(kPage), e));
            SetCam(page, k, Vector2.Lerp(pTL / kTL, pPage / kPage, e) * k);
            float a = Mathf.Clamp01((f - 0.3f) / 0.7f);
            foreach (RawImage b in blurred) SetAlpha(b, a);
            tlInner.color = Color.Lerp(Color.white, new Color(0.8f, 0.8f, 0.85f), e);
            yield return null;
        }
        SetCam(page, kPage, pPage);
        foreach (RawImage b in blurred) SetAlpha(b, 1f);

        yield return new WaitForSecondsRealtime(0.35f);

        float walkTime = 2.1f;
        for (float t = 0f; t < walkTime; t += Time.unscaledDeltaTime)
        {
            float f = t / walkTime;
            Vector2 p = Vector2.Lerp(heroStart, heroEnd, f);
            float bob = Mathf.Abs(Mathf.Sin(t * Mathf.PI / 0.16f)) * heroSize * 0.02f;
            hr.anchoredPosition = new Vector2(p.x, -p.y + bob);
            hero.sprite = ((int)(t / 0.16f) % 2 == 0) ? walk[0] : walk[1];
            SetAlpha(hero, Mathf.Clamp01(t / 0.3f));
            tlInner.color = Color.Lerp(new Color(0.8f, 0.8f, 0.85f), new Color(0.22f, 0.22f, 0.28f), f);
            trInner.color = Color.Lerp(Color.black, Color.white, Mathf.Clamp01((f - 0.35f) / 0.65f));
            yield return null;
        }
        hr.anchoredPosition = new Vector2(heroEnd.x, -heroEnd.y);
        if (idle != null) hero.sprite = idle;
        trInner.color = Color.white;

        yield return new WaitForSecondsRealtime(0.35f);

        for (float f = 0f; f < 1f; f += Time.unscaledDeltaTime / 1.2f)
        {
            float e = Mathf.SmoothStep(0f, 1f, f);
            float k = Mathf.Exp(Mathf.Lerp(Mathf.Log(kPage), Mathf.Log(kTR), e));
            SetCam(page, k, Vector2.Lerp(pPage / kPage, pTR / kTR, e) * k);
            yield return null;
        }
        SetCam(page, kTR, pTR);

        for (float f = 0f; f < 1f; f += Time.unscaledDeltaTime / 0.4f)
        {
            SetAlpha(blackImg, f);
            yield return null;
        }
        SetAlpha(blackImg, 1f);

        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
        if (op != null) while (!op.isDone) yield return null;
        else Debug.LogError("ComicPanelTransition: scene '" + sceneName + "' is not in the build list.");
        yield return null;

        for (float f = 0f; f < 1f; f += Time.unscaledDeltaTime / 0.6f)
        {
            group.alpha = 1f - f;
            yield return null;
        }

        if (carried != null) { carried.dying = true; carried.rate = Mathf.Max(0.01f, carried.src.volume) / 1.5f; }
        Destroy(shot);
        blurShot.Release();
        Destroy(blurShot);
        if (blurBg != null) { blurBg.Release(); Destroy(blurBg); }
        Destroy(root);
        running = false;
    }

    static void Focus(Rect r, Vector2 screen, bool cover, out float k, out Vector2 pos)
    {
        float kx = screen.x / (r.width - Inset * 2f);
        float ky = screen.y / (r.height - Inset * 2f);
        k = cover ? Mathf.Max(kx, ky) : Mathf.Min(kx, ky);
        Vector2 c = r.center;
        pos = new Vector2(-k * c.x, k * c.y);
    }

    static void SetCam(RectTransform page, float k, Vector2 pos)
    {
        page.localScale = new Vector3(k, k, 1f);
        page.anchoredPosition = pos;
    }

    static Rect CoverUv(Texture tex, Rect panel)
    {
        float ta = tex.width / (float)tex.height;
        float pa = (panel.width - Inset * 2f) / (panel.height - Inset * 2f);
        if (ta > pa)
        {
            float w = pa / ta;
            return new Rect((1f - w) * 0.5f, 0f, w, 1f);
        }
        float h = ta / pa;
        return new Rect(0f, (1f - h) * 0.5f, 1f, h);
    }

    static RenderTexture Blur(Texture src, int w, int h)
    {
        RenderTexture a = RenderTexture.GetTemporary(Mathf.Max(w * 4, 8), Mathf.Max(h * 4, 8), 0);
        RenderTexture b = RenderTexture.GetTemporary(Mathf.Max(w * 2, 4), Mathf.Max(h * 2, 4), 0);
        a.filterMode = FilterMode.Bilinear;
        b.filterMode = FilterMode.Bilinear;
        Graphics.Blit(src, a);
        Graphics.Blit(a, b);
        var final = new RenderTexture(w, h, 0);
        final.filterMode = FilterMode.Bilinear;
        final.wrapMode = TextureWrapMode.Clamp;
        Graphics.Blit(b, final);
        RenderTexture.ReleaseTemporary(a);
        RenderTexture.ReleaseTemporary(b);
        return final;
    }

    static Sprite LoadSprite(string folder, string file)
    {
        Texture2D tex = Resources.Load<Texture2D>(folder + "/" + file);
        if (tex == null) return null;
        return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 12f / 512f), 100f);
    }

    static RawImage Panel(RectTransform page, Rect r, Texture tex, Rect uv)
    {
        var border = NewImage(page, "Panel", new Color(0.05f, 0.04f, 0.04f));
        RectTransform rt = border.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(r.x, -r.y);
        rt.sizeDelta = new Vector2(r.width, r.height);
        var g = new GameObject("Inner");
        g.transform.SetParent(rt, false);
        var inner = g.AddComponent<RawImage>();
        inner.texture = tex;
        inner.uvRect = uv;
        inner.raycastTarget = false;
        RectTransform ir = inner.rectTransform;
        ir.anchorMin = Vector2.zero;
        ir.anchorMax = Vector2.one;
        ir.offsetMin = new Vector2(Inset, Inset);
        ir.offsetMax = new Vector2(-Inset, -Inset);
        return inner;
    }

    static Image NewImage(Transform parent, string name, Color color)
    {
        var g = new GameObject(name);
        g.transform.SetParent(parent, false);
        var im = g.AddComponent<Image>();
        im.color = color;
        im.raycastTarget = false;
        return im;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static void SetAlpha(Graphic g, float a)
    {
        Color c = g.color;
        c.a = a;
        g.color = c;
    }
}
