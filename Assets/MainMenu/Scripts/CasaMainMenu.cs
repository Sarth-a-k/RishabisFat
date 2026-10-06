using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class CasaMainMenu : MonoBehaviour
{
    [Header("Scene")]
    public string gameSceneName = "FourfoldCitadel_WithOurStuff";

    [Header("Layers")]
    public RectTransform sky;
    public RectTransform figures;
    public Graphic figureEyes;
    public Graphic leftShade;
    public Graphic title;
    public Graphic pressAnyKey;
    public Image mountainEyelids;
    public CanvasGroup fader;

    [Header("Audio")]
    public AudioSource music;
    [Range(0f, 1f)] public float musicVolume = 0.8f;

    [Header("Background motion")]
    public float skyBreath = 0.015f;
    public float skyBreathSpeed = 0.22f;
    public float figureBob = 0.004f;
    public float figureBobSpeed = 0.4f;
    public float figureBreath = 0.006f;

    [Header("Mountain blink")]
    public Vector2 blinkInterval = new Vector2(2.5f, 6.5f);
    public float closeTime = 0.08f;
    public float holdTime = 0.07f;
    public float openTime = 0.13f;
    [Range(0f, 1f)] public float doubleBlinkChance = 0.25f;

    [Header("Prompt")]
    public float promptPulseSpeed = 2f;
    public float fadeInTime = 1.6f;
    public float fadeOutTime = 1.5f;
    public float inputDelay = 1f;

    bool starting;
    Vector2 figuresBasePos;
    Vector2 skyBasePos;

    void Start()
    {
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        if (figures != null) figuresBasePos = figures.anchoredPosition;
        if (sky != null) skyBasePos = sky.anchoredPosition;
        if (mountainEyelids != null) mountainEyelids.fillAmount = 0f;
        if (music != null)
        {
            music.loop = true;
            music.volume = 0f;
            if (!music.isPlaying) music.Play();
        }
        StartCoroutine(FadeIn());
        if (mountainEyelids != null) StartCoroutine(BlinkLoop());
    }

    IEnumerator FadeIn()
    {
        for (float t = 0f; t < fadeInTime; t += Time.unscaledDeltaTime)
        {
            float k = t / fadeInTime;
            if (fader != null) fader.alpha = 1f - k * k * (3f - 2f * k);
            if (music != null && !starting) music.volume = musicVolume * k;
            yield return null;
        }
        if (fader != null && !starting) fader.alpha = 0f;
        if (music != null && !starting) music.volume = musicVolume;
    }

    void Update()
    {
        float t = Time.unscaledTime;

        if (sky != null)
        {
            float s = 1.03f + skyBreath * Mathf.Sin(t * skyBreathSpeed);
            sky.localScale = new Vector3(s, s, 1f);
            sky.anchoredPosition = skyBasePos + new Vector2(Mathf.Sin(t * 0.07f), Mathf.Cos(t * 0.05f)) * 6f;
        }

        if (figures != null)
        {
            float h = figures.rect.height;
            figures.anchoredPosition = figuresBasePos + new Vector2(0f, Mathf.Sin(t * figureBobSpeed) * figureBob * h);
            float b = 1f + figureBreath * Mathf.Sin(t * figureBobSpeed * 0.7f + 1.3f);
            figures.localScale = new Vector3(b, b, 1f);
        }

        if (figureEyes != null)
        {
            float n = Mathf.PerlinNoise(t * 3f, 0.37f);
            SetAlpha(figureEyes, n < 0.1f ? 0.45f : Mathf.Lerp(0.82f, 1f, n));
        }

        if (leftShade != null)
            SetAlpha(leftShade, 0.9f + 0.1f * Mathf.Sin(t * 0.3f));

        if (title != null)
        {
            float f = Mathf.PerlinNoise(t * 9f, 4.2f);
            SetAlpha(title, f > 0.93f ? 0.82f : 1f);
        }

        if (starting) return;

        if (pressAnyKey != null)
            SetAlpha(pressAnyKey, Mathf.Lerp(0.3f, 1f, (Mathf.Sin(t * promptPulseSpeed) + 1f) * 0.5f));

        if (Time.timeSinceLevelLoad > inputDelay && AnyInput())
            StartCoroutine(Begin());
    }

    static bool AnyInput()
    {
        return Input.anyKeyDown || Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1) || Input.touchCount > 0;
    }

    IEnumerator BlinkLoop()
    {
        while (true)
        {
            yield return new WaitForSecondsRealtime(Random.Range(blinkInterval.x, blinkInterval.y));
            yield return Blink();
            if (Random.value < doubleBlinkChance)
            {
                yield return new WaitForSecondsRealtime(0.12f);
                yield return Blink();
            }
        }
    }

    IEnumerator Blink()
    {
        for (float t = 0f; t < closeTime; t += Time.unscaledDeltaTime)
        {
            mountainEyelids.fillAmount = Mathf.SmoothStep(0f, 1f, t / closeTime);
            yield return null;
        }
        mountainEyelids.fillAmount = 1f;
        yield return new WaitForSecondsRealtime(holdTime);
        for (float t = 0f; t < openTime; t += Time.unscaledDeltaTime)
        {
            mountainEyelids.fillAmount = Mathf.SmoothStep(1f, 0f, t / openTime);
            yield return null;
        }
        mountainEyelids.fillAmount = 0f;
    }

    IEnumerator Begin()
    {
        starting = true;
        if (pressAnyKey != null)
        {
            for (int i = 0; i < 4; i++)
            {
                SetAlpha(pressAnyKey, 1f);
                yield return new WaitForSecondsRealtime(0.06f);
                SetAlpha(pressAnyKey, 0.2f);
                yield return new WaitForSecondsRealtime(0.06f);
            }
            SetAlpha(pressAnyKey, 1f);
        }

        AsyncOperation load = SceneManager.LoadSceneAsync(gameSceneName);
        if (load != null) load.allowSceneActivation = false;

        float startVol = music != null ? music.volume : 0f;
        float startFade = fader != null ? fader.alpha : 0f;
        for (float t = 0f; t < fadeOutTime; t += Time.unscaledDeltaTime)
        {
            float k = t / fadeOutTime;
            if (fader != null) fader.alpha = Mathf.Lerp(startFade, 1f, k);
            if (music != null) music.volume = Mathf.Lerp(startVol, 0f, k);
            yield return null;
        }
        if (fader != null) fader.alpha = 1f;

        if (load != null)
        {
            while (load.progress < 0.9f) yield return null;
            load.allowSceneActivation = true;
        }
        else
        {
            SceneManager.LoadScene(gameSceneName);
        }
    }

    static void SetAlpha(Graphic g, float a)
    {
        Color c = g.color;
        c.a = a;
        g.color = c;
    }
}
