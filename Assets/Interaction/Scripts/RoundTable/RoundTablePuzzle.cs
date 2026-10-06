using UnityEngine;
using UnityEngine.Events;

namespace FPCharacter
{
    public sealed class RoundTablePuzzle : MonoBehaviour
    {
        public PrismBeamSplitter splitter;
        public PlayerSeat playerSeat;
        public PrismOverload overload;
        public string completedMessage = "The circle is complete";
        public float messageSeconds = 5f;
        public UnityEvent onCompleted;

        public bool Completed { get; private set; }

        float messageUntil = -1f;
        GUIStyle style;

        void Update()
        {
            if (Completed || splitter == null || playerSeat == null) return;
            bool charging = splitter.Solved && playerSeat.Occupied;
            bool done = charging;
            if (overload != null)
            {
                overload.Tick(charging, Time.deltaTime);
                done = overload.Shattered;
            }
            if (done)
            {
                Completed = true;
                messageUntil = Time.time + messageSeconds;
                Debug.Log("[RoundTable] Puzzle complete: all beams reach the seats and the player is seated");
                onCompleted?.Invoke();
            }
        }

        void OnGUI()
        {
            if (Time.time > messageUntil) return;
            if (style == null)
            {
                style = new GUIStyle(GUI.skin.label);
                style.alignment = TextAnchor.MiddleCenter;
                style.fontStyle = FontStyle.Bold;
            }
            style.fontSize = Mathf.Max(18, Screen.height / 18);
            float a = Mathf.Clamp01((messageUntil - Time.time) / 1f);
            Rect r = new Rect(0f, Screen.height * 0.3f, Screen.width, style.fontSize * 2f);
            style.normal.textColor = new Color(0f, 0f, 0f, 0.8f * a);
            GUI.Label(new Rect(r.x + 3f, r.y + 3f, r.width, r.height), completedMessage, style);
            style.normal.textColor = new Color(1f, 0.9f, 0.65f, a);
            GUI.Label(r, completedMessage, style);
        }
    }
}
