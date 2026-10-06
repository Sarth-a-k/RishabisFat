using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class JulianTransition : MonoBehaviour
{
    public string nextScene = "WarmStatues2D";
    public float startDelay = 1.8f;
    public float blackFade = 0.8f;

    [Header("Julian")]
    public string word = "JULIAN";
    public int wordSize = 64;
    public Color wordColor = new Color(0.94f, 0.9f, 0.82f, 1f);
    public float firstAt = 0.6f;
    public float interval = 1.3f;
    public float hold = 1.1f;

    [Header("Final line")]
    [TextArea(2, 4)] public string finalLine = "TURN AROUND.\nYOU CAN'T ALWAYS GO FORWARD.";
    public int finalSize = 46;
    public Color finalColor = new Color(1f, 0.16f, 0.12f, 1f);
    public float finalFadeIn = 0.6f;
    public float finalHold = 2.6f;
    public float finalFadeOut = 0.8f;

    const float W = 1280f, H = 720f;
    const string Dir = "WarmStatues/";

    static bool running;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { running = false; }

    class Runner : MonoBehaviour { }

    public bool IsRunning => running;

    public void Play()
    {
        if (running) return;
        running = true;
        var go = new GameObject("JulianTransition (running)");
        DontDestroyOnLoad(go);
        go.AddComponent<Runner>().StartCoroutine(Run(go, this));
    }

    static IEnumerator Run(GameObject root, JulianTransition cfg)
    {
        string scene = cfg.nextScene;
        string word = cfg.word;
        int wordSize = cfg.wordSize;
        Color wordColor = cfg.wordColor;
        float firstAt = cfg.firstAt, interval = cfg.interval, hold = cfg.hold, blackFade = cfg.blackFade;
        string line = cfg.finalLine;
        int lineSize = cfg.finalSize;
        Color lineColor = cfg.finalColor;
        float fIn = cfg.finalFadeIn, fHold = cfg.finalHold, fOut = cfg.finalFadeOut;

        yield return new WaitForSeconds(cfg.startDelay);

        foreach (FPCharacter.FPCharacterMover m in FindObjectsByType<FPCharacter.FPCharacterMover>(FindObjectsInactive.Exclude)) m.enabled = false;
        foreach (FPCharacter.PlayerInteraction p in FindObjectsByType<FPCharacter.PlayerInteraction>(FindObjectsInactive.Exclude)) p.enabled = false;
        FPCharacter.ZoneMusic.FadeOutAll(blackFade + 0.5f);

        Font font = Resources.Load<Font>(Dir + "DotGothic16");
        if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;
        var group = root.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;

        var blackGo = new GameObject("Black", typeof(RectTransform));
        blackGo.transform.SetParent(root.transform, false);
        var black = blackGo.AddComponent<Image>();
        black.color = new Color(0f, 0f, 0f, 0f);
        black.raycastTarget = false;
        var brt = (RectTransform)blackGo.transform;
        brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one; brt.offsetMin = Vector2.zero; brt.offsetMax = Vector2.zero;

        var stage = (RectTransform)new GameObject("Stage", typeof(RectTransform)).transform;
        stage.SetParent(root.transform, false);
        stage.anchorMin = stage.anchorMax = new Vector2(0.5f, 0.5f);
        stage.pivot = new Vector2(0.5f, 0.5f);
        stage.sizeDelta = new Vector2(W, H);

        Vector2[] corners = { new Vector2(190f, 110f), new Vector2(W - 190f, 110f), new Vector2(190f, H - 100f), new Vector2(W - 190f, H - 100f) };
        var labels = new Text[4];
        var sources = new AudioSource[4];
        var played = new bool[4];
        for (int i = 0; i < 4; i++)
        {
            var lg = new GameObject("Julian", typeof(RectTransform));
            lg.transform.SetParent(stage, false);
            var rt = (RectTransform)lg.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(600f, wordSize * 2f);
            rt.anchoredPosition = new Vector2(corners[i].x, -corners[i].y);
            var tx = lg.AddComponent<Text>();
            tx.font = font;
            tx.fontSize = wordSize;
            tx.alignment = TextAnchor.MiddleCenter;
            tx.horizontalOverflow = HorizontalWrapMode.Overflow;
            tx.verticalOverflow = VerticalWrapMode.Overflow;
            tx.raycastTarget = false;
            tx.text = word;
            tx.enabled = false;
            var o = lg.AddComponent<Outline>();
            o.effectColor = new Color(0.02f, 0.02f, 0.05f, 1f);
            o.effectDistance = new Vector2(2.5f, -2.5f);
            labels[i] = tx;
            sources[i] = root.AddComponent<AudioSource>();
            sources[i].playOnAwake = false;
            sources[i].clip = Resources.Load<AudioClip>(Dir + "julian" + (i + 1));
        }

        JiggleLine jiggle = JiggleLine.Create(stage, font, line, lineSize, lineColor, new Vector2(W * 0.5f, H * 0.5f), 1100f);
        jiggle.SetAlpha(0f);

        float jOut = firstAt + 3f * interval + hold;
        float f0 = jOut + 0.7f;
        float end = f0 + fIn + fHold + fOut + 0.3f;
        float t = 0f;
        float fadeT = 0f;
        while (fadeT < blackFade)
        {
            fadeT += Time.unscaledDeltaTime;
            Fit(stage);
            black.color = new Color(0f, 0f, 0f, Mathf.Clamp01(fadeT / Mathf.Max(0.05f, blackFade)));
            yield return null;
        }
        black.color = Color.black;

        while (t < end)
        {
            t += Time.unscaledDeltaTime;
            Fit(stage);
            for (int i = 0; i < 4; i++)
            {
                float ts = firstAt + i * interval;
                if (t >= ts && !played[i])
                {
                    played[i] = true;
                    if (sources[i].clip != null) sources[i].Play();
                }
                float a = Mathf.Clamp01((t - ts) / 0.15f) * (1f - Mathf.Clamp01((t - jOut) / 0.5f));
                Text l = labels[i];
                l.enabled = a > 0.001f;
                if (!l.enabled) continue;
                float pop = 1f + 0.35f * (1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - ts) / 0.25f)));
                l.rectTransform.localScale = new Vector3(pop, pop, 1f);
                l.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 3.1f + i * 1.7f) * 1.2f);
                Color c = wordColor;
                c.a = a * (0.86f + 0.14f * Mathf.Sin(t * 23f + i * 3f));
                l.color = c;
            }
            float fp = t - f0;
            float fa = fp < 0f ? 0f : fp < fIn ? fp / fIn : fp < fIn + fHold ? 1f : 1f - (fp - fIn - fHold) / Mathf.Max(0.05f, fOut);
            jiggle.SetAlpha(Mathf.Clamp01(fa));
            float vol = 1f - Mathf.Clamp01((t - f0) / 2f);
            foreach (AudioSource s in sources) s.volume = vol;
            yield return null;
        }

        foreach (Text l in labels) l.enabled = false;
        jiggle.SetAlpha(0f);
        AsyncOperation op = SceneManager.LoadSceneAsync(scene);
        if (op != null) while (!op.isDone) yield return null;
        else Debug.LogError("JulianTransition: scene '" + scene + "' is not in the build list.");
        yield return null;

        for (float f = 0f; f < 1f; f += Time.unscaledDeltaTime / 0.5f)
        {
            group.alpha = 1f - f;
            yield return null;
        }
        Destroy(root);
        running = false;
    }

    static void Fit(RectTransform stage)
    {
        float sc = Mathf.Min(Screen.width / W, Screen.height / H);
        stage.localScale = new Vector3(sc, sc, 1f);
    }
}
