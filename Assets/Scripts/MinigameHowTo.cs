using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// "How to play" card shown over a 2D minigame the first time it is entered in a playthrough.
// The game behind it is frozen (time scale 0) and the pause menu is blocked until Space, Enter or a click.
// Warm Statues is not listed: its own title screen already shows the rules and controls.
public sealed class MinigameHowTo : MonoBehaviour
{
    struct Card { public string title, goal; public string[] keys, actions; }

    static readonly Dictionary<string, Card> Cards = new Dictionary<string, Card>
    {
        { "Shadow2D", new Card {
            title = "THE SHADOW RUN",
            goal = "Reach the ember door at the far right of the corridor.",
            keys    = new[] { "A  D", "MOUSE", "HOLD THE LIGHT", "DON'T LET THEM TOUCH" },
            actions = new[] { "walk left and right", "aim the torch", "a shadow burns away after about a second", "a shadow standing on you too long ends the run" } } },
        { "LightBlade2D", new Card {
            title = "THE LIGHT BLADE",
            goal = "Survive until the shadows stop coming, cut down the last one, then walk through the arch.",
            keys    = new[] { "W A S D", "LEFT CLICK / SPACE", "RIGHT CLICK / SHIFT", "E" },
            actions = new[] { "move", "slash (every third slash is a spin)", "dash past lunges and orbs", "press the button in the hall" } } },
        { "FurnaceHeart2D", new Card {
            title = "THE FURNACE HEART",
            goal = "Cut down the nine skeletons, then cut the furnace's heart until it falls.",
            keys    = new[] { "W A S D", "LEFT CLICK / SPACE / J", "SHIFT / K", "Q / E / RIGHT CLICK", "R" },
            actions = new[] { "move", "slash", "dash (passes through the shockwave)", "thermal sight: the heart only shows here", "restart after a fall" } } },
    };

    static readonly HashSet<string> shown = new HashSet<string>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { shown.Clear(); }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Hook()
    {
        SceneManager.sceneLoaded -= OnLoaded;
        SceneManager.sceneLoaded += OnLoaded;
        TrySpawn(SceneManager.GetActiveScene());
    }

    static void OnLoaded(Scene s, LoadSceneMode m) { TrySpawn(s); }

    static void TrySpawn(Scene s)
    {
        if (!Cards.ContainsKey(s.name) || shown.Contains(s.name)) return;
        if (FindAnyObjectByType<MinigameHowTo>() != null) return;
        shown.Add(s.name);
        var go = new GameObject("How To Play");
        SceneManager.MoveGameObjectToScene(go, s);
        go.AddComponent<MinigameHowTo>().card = Cards[s.name];
    }

    Card card;
    float openedAt;
    bool blockedPause, closed;
    Font font;
    GUIStyle titleStyle, goalStyle, keyStyle, actStyle, footStyle;
    Texture2D white;

    void Awake()
    {
        Time.timeScale = 0f;
        if (!PauseMenu.Blocked) { PauseMenu.Blocked = true; blockedPause = true; }
        openedAt = Time.unscaledTime;
    }

    void Update()
    {
        if (Time.unscaledTime - openedAt < 0.4f || !StartPressed()) return;
        Close();
    }

    void Close()
    {
        closed = true;
        Time.timeScale = 1f;
        if (blockedPause) PauseMenu.Blocked = false;
        blockedPause = false;
        Destroy(gameObject);
    }

    void OnDestroy()
    {
        // never leave the game frozen or the pause menu blocked (e.g. the scene unloads with the card up)
        if (closed) return;
        Time.timeScale = 1f;
        if (blockedPause) PauseMenu.Blocked = false;
    }

    static bool StartPressed()
    {
#if ENABLE_INPUT_SYSTEM
        var k = Keyboard.current; var m = Mouse.current;
        return (k != null && (k.spaceKey.wasPressedThisFrame || k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame))
            || (m != null && m.leftButton.wasPressedThisFrame);
#else
        return Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetMouseButtonDown(0);
#endif
    }

    void Setup()
    {
        if (titleStyle != null) return;
        font = Resources.Load<Font>("WarmStatues/DotGothic16");
        white = Texture2D.whiteTexture;
        titleStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 34, wordWrap = false };
        titleStyle.normal.textColor = new Color(1f, 0.78f, 0.38f);
        goalStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperCenter, fontSize = 18, wordWrap = true };
        goalStyle.normal.textColor = new Color(0.95f, 0.93f, 0.88f);
        keyStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleRight, fontSize = 16, wordWrap = false };
        keyStyle.normal.textColor = new Color(1f, 0.86f, 0.55f);
        actStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleLeft, fontSize = 16, wordWrap = false };
        actStyle.normal.textColor = new Color(0.9f, 0.9f, 0.92f);
        footStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 18, wordWrap = false };
        if (font != null) titleStyle.font = goalStyle.font = keyStyle.font = actStyle.font = footStyle.font = font;
    }

    void OnGUI()
    {
        Setup();
        GUI.depth = -1000;   // above the minigame's own HUD
        Color old = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.86f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), white);

        // 960 x 600 virtual canvas, scaled to the screen height and centred
        float k = Screen.height / 600f, offX = (Screen.width - 960f * k) / 2f;
        Matrix4x4 oldM = GUI.matrix;
        GUI.matrix = Matrix4x4.TRS(new Vector3(offX, 0f, 0f), Quaternion.identity, new Vector3(k, k, 1f));

        int rows = card.keys.Length;
        float panelH = 210f + rows * 34f;
        Rect panel = new Rect(100f, (600f - panelH) / 2f, 760f, panelH);
        GUI.color = new Color(1f, 0.78f, 0.38f, 0.5f);
        GUI.DrawTexture(new Rect(panel.x - 2, panel.y - 2, panel.width + 4, panel.height + 4), white);
        GUI.color = new Color(0.05f, 0.035f, 0.04f, 0.97f);
        GUI.DrawTexture(panel, white);
        GUI.color = Color.white;

        float y = panel.y + 22f;
        GUI.Label(new Rect(panel.x, y, panel.width, 40f), card.title, titleStyle); y += 46f;
        GUI.Label(new Rect(panel.x + 40f, y, panel.width - 80f, 50f), card.goal, goalStyle); y += 62f;
        for (int i = 0; i < rows; i++)
        {
            GUI.Label(new Rect(panel.x + 20f, y, 270f, 30f), card.keys[i], keyStyle);
            GUI.Label(new Rect(panel.x + 310f, y, 430f, 30f), card.actions[i], actStyle);
            y += 34f;
        }
        float blink = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 3f));
        footStyle.normal.textColor = new Color(1f, 1f, 1f, blink);
        GUI.Label(new Rect(panel.x, panel.yMax - 46f, panel.width, 30f), "PRESS SPACE OR CLICK TO START", footStyle);

        GUI.matrix = oldM;
        GUI.color = old;
    }
}
