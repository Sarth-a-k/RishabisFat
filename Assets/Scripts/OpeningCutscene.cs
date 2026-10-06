using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class OpeningCutscene : MonoBehaviour
{
    public static bool Pending;
    public string videoFileName = "cutscene0.mp4";
    [Range(0f, 1f)] public float volume = 1f;
    public float fadeOut = 0.8f;
    public float fadeIn = 0.9f;
    public bool allowSkip = true;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { Pending = false; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Hook()
    {
        SceneManager.sceneLoaded -= OnLoaded;
        SceneManager.sceneLoaded += OnLoaded;
        TrySpawn();
    }

    static void OnLoaded(Scene s, LoadSceneMode m) { TrySpawn(); }

    static void TrySpawn()
    {
        if (!Pending) return;
        if (FindAnyObjectByType<FPCharacter.FPCharacterMover>() == null) return;
        Pending = false;
        new GameObject("Opening Cutscene").AddComponent<OpeningCutscene>();
    }

    VideoPlayer vp;
    RenderTexture rt;
    CanvasGroup group;
    RawImage screen;
    Text skipLabel;
    Behaviour[] frozen;
    bool finished, ending, canSkip;
    float startedAt;

    void Start()
    {
        CutsceneGate.Begin(this);
        var list = new System.Collections.Generic.List<Behaviour>();
        foreach (FPCharacter.FPCharacterMover m in FindObjectsByType<FPCharacter.FPCharacterMover>(FindObjectsInactive.Exclude)) if (m.enabled) { m.enabled = false; list.Add(m); }
        foreach (FPCharacter.PlayerInteraction p in FindObjectsByType<FPCharacter.PlayerInteraction>(FindObjectsInactive.Exclude)) if (p.enabled) { p.enabled = false; list.Add(p); }
        frozen = list.ToArray();

        var canvasGo = new GameObject("Opening Cutscene UI");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 2000;
        group = canvasGo.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        var black = new GameObject("Black", typeof(RectTransform)).AddComponent<Image>();
        black.transform.SetParent(canvasGo.transform, false);
        Stretch(black.rectTransform);
        black.color = Color.black;
        black.raycastTarget = false;
        screen = new GameObject("Video", typeof(RectTransform)).AddComponent<RawImage>();
        screen.transform.SetParent(canvasGo.transform, false);
        Stretch(screen.rectTransform);
        screen.raycastTarget = false;
        screen.enabled = false;
        var fitter = screen.gameObject.AddComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        fitter.aspectRatio = 16f / 9f;
        skipLabel = new GameObject("Skip", typeof(RectTransform)).AddComponent<Text>();
        skipLabel.transform.SetParent(canvasGo.transform, false);
        var lrt = skipLabel.rectTransform;
        lrt.anchorMin = new Vector2(0f, 0f); lrt.anchorMax = new Vector2(1f, 0f);
        lrt.pivot = new Vector2(0.5f, 0f);
        lrt.anchoredPosition = new Vector2(0f, 24f);
        lrt.sizeDelta = new Vector2(0f, 40f);
        skipLabel.font = Resources.Load<Font>("WarmStatues/DotGothic16");
        if (skipLabel.font == null) skipLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        skipLabel.fontSize = Mathf.Max(28, Screen.height / 26);
        lrt.sizeDelta = new Vector2(0f, skipLabel.fontSize * 1.8f);
        canSkip = allowSkip && CutsceneMemory.Seen(videoFileName);
        skipLabel.alignment = TextAnchor.MiddleCenter;
        skipLabel.color = new Color(1f, 1f, 1f, 0f);
        skipLabel.text = "SPACE  SKIP";
        skipLabel.raycastTarget = false;

        vp = gameObject.AddComponent<VideoPlayer>();
        vp.playOnAwake = false;
        vp.isLooping = false;
        vp.source = VideoSource.Url;
        vp.url = System.IO.Path.Combine(Application.streamingAssetsPath, videoFileName);
        vp.renderMode = VideoRenderMode.RenderTexture;
        vp.audioOutputMode = VideoAudioOutputMode.Direct;
        vp.loopPointReached += s => Finish();
        vp.errorReceived += (s, msg) => { Debug.LogWarning("OpeningCutscene: " + msg); Finish(); };
        vp.prepareCompleted += OnPrepared;
        vp.Prepare();
        startedAt = Time.unscaledTime;
    }

    void OnPrepared(VideoPlayer source)
    {
        int w = (int)Mathf.Max(16, source.width), h = (int)Mathf.Max(16, source.height);
        rt = new RenderTexture(w, h, 0);
        vp.targetTexture = rt;
        screen.texture = rt;
        screen.GetComponent<AspectRatioFitter>().aspectRatio = (float)w / h;
        screen.color = new Color(1f, 1f, 1f, 0f);
        screen.enabled = true;
        vp.EnableAudioTrack(0, true);
        vp.SetDirectAudioVolume(0, 0f);
        vp.Play();
        startedAt = Time.unscaledTime;
        StartCoroutine(FadeIn());
    }

    IEnumerator FadeIn()
    {
        for (float t = 0f; t < 1f && !ending; t += Time.unscaledDeltaTime / Mathf.Max(0.05f, fadeIn))
        {
            float k = t * t * (3f - 2f * t);
            screen.color = new Color(1f, 1f, 1f, k);
            vp.SetDirectAudioVolume(0, volume * k);
            yield return null;
        }
        if (!ending)
        {
            screen.color = Color.white;
            vp.SetDirectAudioVolume(0, volume);
        }
    }

    void Update()
    {
        if (finished) return;
        float since = Time.unscaledTime - startedAt;
        if (skipLabel != null) skipLabel.color = new Color(1f, 1f, 1f, canSkip ? 0.75f * Mathf.Clamp01((since - 1f) / 0.5f) : 0f);
        if (canSkip && since > 0.6f && SkipPressed()) Finish();
        if (!vp.isPrepared && since > 20f) Finish();
    }

    void Finish()
    {
        if (ending) return;
        ending = true;
        CutsceneMemory.MarkSeen(videoFileName);
        StartCoroutine(FadeAndEnd());
    }

    IEnumerator FadeAndEnd()
    {
        if (vp != null) vp.Pause();
        if (skipLabel != null) skipLabel.enabled = false;
        for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / Mathf.Max(0.05f, fadeOut))
        {
            group.alpha = 1f - t;
            if (vp != null) vp.SetDirectAudioVolume(0, volume * (1f - t));
            yield return null;
        }
        finished = true;
        CutsceneGate.End(this);
        if (vp != null) vp.Stop();
        if (frozen != null) foreach (Behaviour b in frozen) if (b != null) b.enabled = true;
        if (rt != null) rt.Release();
        Destroy(gameObject);
    }

    static void Stretch(RectTransform r)
    {
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
    }

    static bool SkipPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && (Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.escapeKey.wasPressedThisFrame);
#else
        return Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Escape);
#endif
    }
}
