using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Casa del Silencio - 3D to 2D transition: the adventurer blacks out.
// (Replaces ComicPanelTransition. Delete that script and use this one on the same trigger object.)
//
// Setup, in the 3D scene:
//   1. Put an empty GameObject with a collider (Is Trigger ticked) on the way to the exit door. Add this script.
//   2. When the first puzzle is solved, call SetPuzzleSolved() on it (or tick Puzzle Solved for testing).
//   3. Tag the player "Player". Optionally drag the player's movement script into Player Movement.
// When the solved player walks into the trigger: he loses control, the edges of his vision close in,
// his eyes droop shut twice and then stay shut, the sound drains away, and the 2D scene loads in the dark.
//
// You can also start it from code at any time with Play().
public class BlackoutTransition : MonoBehaviour
{
    public string nextScene = "Shadow2D";
    public bool puzzleSolved = false;
    public string playerTag = "Player";
    [Tooltip("Optional: disabled when the blackout starts so the player can't walk away.")]
    public MonoBehaviour playerMovement;
    [Tooltip("Optional: these fade out with him (their materials must use a transparent or fade mode to show it).")]
    public Renderer[] playerRenderers;

    static bool running;
    static float savedVolume = -1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        running = false;
        if (savedVolume >= 0f) AudioListener.volume = savedVolume;
        savedVolume = -1f;
    }

    class Runner : MonoBehaviour { }

    public void SetPuzzleSolved() { puzzleSolved = true; }

    void OnTriggerEnter(Collider other) { if (puzzleSolved && other.CompareTag(playerTag)) Play(); }
    void OnTriggerEnter2D(Collider2D other) { if (puzzleSolved && other.CompareTag(playerTag)) Play(); }

    public void Play()
    {
        if (running) return;
        running = true;
        if (playerMovement != null) playerMovement.enabled = false;
        FPCharacter.ZoneMusic.FadeOutAll(2.5f);

        // The runner survives the scene change; this object does not.
        var go = new GameObject("BlackoutTransition (running)");
        DontDestroyOnLoad(go);
        go.AddComponent<Runner>().StartCoroutine(Run(go, nextScene, playerRenderers));
    }

    static IEnumerator Run(GameObject root, string sceneName, Renderer[] fading)
    {
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;

        // Soft dark ring that closes in from the edges.
        const int N = 64;
        var tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(N / 2f, N / 2f)) / (N / 2f);
                tex.SetPixel(x, y, new Color(0f, 0f, 0f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.35f, 1.05f, d))));
            }
        tex.Apply();
        Image vignette = NewImage(root.transform, "Vignette", Color.white);
        vignette.sprite = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f));
        Stretch(vignette.rectTransform, 0f, 1f, 0f, 1f);
        SetAlpha(vignette, 0f);

        Image dim = NewImage(root.transform, "Dim", new Color(0f, 0f, 0f, 0f));
        Stretch(dim.rectTransform, 0f, 1f, 0f, 1f);
        Image lidTop = NewImage(root.transform, "LidTop", Color.black);
        Image lidBottom = NewImage(root.transform, "LidBottom", Color.black);

        float startVolume = AudioListener.volume;
        savedVolume = startVolume;

        // Each entry: how far the eyelids close (0 = open, 1 = shut) and how long the move takes.
        float[] target = { 0.55f, 0.15f, 0.8f, 0.3f, 1f };
        float[] time = { 0.55f, 0.45f, 0.6f, 0.4f, 0.9f };
        float lids = 0f, total = 0f, elapsed = 0f;
        foreach (float s in time) total += s;

        for (int i = 0; i < target.Length; i++)
        {
            float from = lids;
            for (float f = 0f; f < 1f; f += Time.unscaledDeltaTime / time[i])
            {
                elapsed += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(elapsed / total);
                lids = Mathf.Lerp(from, target[i], Mathf.SmoothStep(0f, 1f, f));
                Stretch(lidTop.rectTransform, 0f, 1f, 1f - lids * 0.5f, 1f);
                Stretch(lidBottom.rectTransform, 0f, 1f, 0f, lids * 0.5f);
                SetAlpha(vignette, Mathf.Clamp01(k * 1.6f));
                SetAlpha(dim, k * k * 0.75f);
                AudioListener.volume = Mathf.Lerp(startVolume, 0f, k * k);
                if (fading != null)
                    foreach (var r in fading)
                        if (r != null && r.material.HasProperty("_Color"))
                        {
                            Color c = r.material.color; c.a = 1f - k; r.material.color = c;
                        }
                yield return null;
            }
            lids = target[i];
        }
        Stretch(lidTop.rectTransform, 0f, 1f, 0.5f, 1f);
        Stretch(lidBottom.rectTransform, 0f, 1f, 0f, 0.5f);
        SetAlpha(dim, 1f);

        // Hold in the dark for a moment, then load the 2D scene behind it.
        yield return new WaitForSecondsRealtime(0.7f);
        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
        if (op != null) while (!op.isDone) yield return null;
        else Debug.LogError("BlackoutTransition: scene '" + sceneName + "' is not in the build list.");
        yield return null;

        AudioListener.volume = startVolume;
        savedVolume = -1f;
        Destroy(tex);
        Destroy(root);      // the 2D scene fades itself in from black
        running = false;
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

    static void Stretch(RectTransform rt, float x0, float x1, float y0, float y1)
    {
        rt.anchorMin = new Vector2(x0, y0);
        rt.anchorMax = new Vector2(x1, y1);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static void SetAlpha(Graphic g, float a) { var c = g.color; c.a = a; g.color = c; }
}
