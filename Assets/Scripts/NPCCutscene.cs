using UnityEngine;
using UnityEngine.Video;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(VideoPlayer))]
public class NPCCutscene : MonoBehaviour
{
    public string videoFileName = "npc1cutscene.mp4";
    public float talkRadius = 2.5f;
    public string promptText = "E  Talk";
    public bool playOnlyOnce = true;
    public bool allowSkip = true;
    public bool replayable = true;
    bool canSkip;
    [Range(0f, 1f)] public float audioVolume = 1f;
    public MonoBehaviour[] disableWhilePlaying;
    public GameObject prompt;

    VideoPlayer vp;
    Transform player;
    bool inRange, playing, played, triggerInside;
    float startedAt;
    GUIStyle style;
    MonoBehaviour[] toggled = new MonoBehaviour[0];

    public bool IsPlaying => playing;
    public bool HasPlayed => played;

    void Awake()
    {
        vp = GetComponent<VideoPlayer>();
        vp.playOnAwake = false;
        vp.isLooping = false;
        vp.source = VideoSource.Url;
        vp.url = System.IO.Path.Combine(Application.streamingAssetsPath, videoFileName);
        vp.renderMode = VideoRenderMode.CameraNearPlane;
        vp.aspectRatio = VideoAspectRatio.FitInside;
        vp.audioOutputMode = VideoAudioOutputMode.Direct;
        vp.loopPointReached += OnFinished;
        vp.errorReceived += (src, msg) => { Debug.LogWarning("NPCCutscene video error: " + msg, this); EndCutscene(); };
        if (prompt != null) prompt.SetActive(false);
        if (replayable) playOnlyOnce = false;
    }

    void Start()
    {
        FPCharacter.FPCharacterMover mover = FindAnyObjectByType<FPCharacter.FPCharacterMover>();
        if (mover != null) player = mover.transform;
        else
        {
            GameObject tagged = GameObject.FindWithTag("Player");
            if (tagged != null) player = tagged.transform;
        }
        if (disableWhilePlaying == null || disableWhilePlaying.Length == 0)
        {
            var list = new System.Collections.Generic.List<MonoBehaviour>();
            if (mover != null) list.Add(mover);
            FPCharacter.PlayerInteraction pi = FindAnyObjectByType<FPCharacter.PlayerInteraction>();
            if (pi != null) list.Add(pi);
            toggled = list.ToArray();
        }
        else toggled = disableWhilePlaying;
    }

    void Update()
    {
        if (playing)
        {
            if (canSkip && Time.unscaledTime - startedAt > 0.5f && SkipPressed()) EndCutscene();
            return;
        }
        bool near = triggerInside || (player != null && Vector3.Distance(Flat(player.position), Flat(transform.position)) <= talkRadius);
        if (near != inRange) SetRange(near);
        if (!inRange || (playOnlyOnce && played)) return;
        if (EPressed()) StartCutscene();
    }

    static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

    static bool EPressed()
    {
        if (FPCharacter.GamePause.BlockInput) return false;
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.E);
#endif
    }

    static bool SkipPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && (Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.escapeKey.wasPressedThisFrame);
#else
        return Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Escape);
#endif
    }

    void StartCutscene()
    {
        playing = true;
        CutsceneGate.Begin(this);
        canSkip = allowSkip && CutsceneMemory.Seen(videoFileName);
        startedAt = Time.unscaledTime;
        if (prompt != null) prompt.SetActive(false);
        foreach (MonoBehaviour m in toggled) if (m != null) m.enabled = false;
        vp.targetCamera = Camera.main;
        vp.EnableAudioTrack(0, true);
        vp.SetDirectAudioVolume(0, audioVolume);
        vp.Play();
    }

    void OnFinished(VideoPlayer source)
    {
        EndCutscene();
    }

    void EndCutscene()
    {
        if (!playing) return;
        vp.Stop();
        playing = false;
        CutsceneGate.End(this);
        played = true;
        CutsceneMemory.MarkSeen(videoFileName);
        foreach (MonoBehaviour m in toggled) if (m != null) m.enabled = true;
        SetRange(inRange);
    }

    void SetRange(bool value)
    {
        inRange = value;
        if (prompt != null) prompt.SetActive(value && !playing && !(playOnlyOnce && played));
    }

    void OnGUI()
    {
        if (style == null)
        {
            style = new GUIStyle(GUI.skin.label);
            style.alignment = TextAnchor.MiddleCenter;
            style.fontStyle = FontStyle.Bold;
        }
        style.fontSize = Mathf.Max(14, Screen.height / 36);
        if (playing)
        {
            if (!canSkip) return;
            style.fontSize = Mathf.Max(22, Screen.height / 24);
            style.normal.textColor = new Color(1f, 1f, 1f, 0.75f);
            GUI.Label(new Rect(0, Screen.height * 0.9f, Screen.width, style.fontSize * 1.6f), "SPACE  SKIP", style);
            return;
        }
        if (prompt != null || !inRange || (playOnlyOnce && played) || string.IsNullOrEmpty(promptText)) return;
        FPCharacter.PromptBox.Draw(promptText, 0.58f);
    }

    void OnTriggerEnter(Collider other) { if (IsPlayer(other.transform)) triggerInside = true; }
    void OnTriggerExit(Collider other) { if (IsPlayer(other.transform)) triggerInside = false; }
    void OnTriggerEnter2D(Collider2D other) { if (IsPlayer(other.transform)) triggerInside = true; }
    void OnTriggerExit2D(Collider2D other) { if (IsPlayer(other.transform)) triggerInside = false; }

    bool IsPlayer(Transform t)
    {
        return t.CompareTag("Player") || (player != null && t.IsChildOf(player));
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, talkRadius);
    }
}
