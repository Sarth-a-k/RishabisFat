using System;
using UnityEngine;

namespace FPCharacter
{
    public sealed class PuzzleHints : MonoBehaviour
    {
        [Serializable]
        public class Step
        {
            [TextArea(1, 3)] public string[] hints = new string[0];
        }

        public PuzzleTorch torch;
        public MirrorSocket[] sockets = new MirrorSocket[0];
        public PrismMoonController prism;
        public Vector3 areaCenter = new Vector3(48.85f, 0f, 0f);
        public Vector2 areaSize = new Vector2(48.1f, 36.4f);

        [Header("Timing")]
        public Vector2 delayRange = new Vector2(10f, 15f);
        public float showSeconds = 7f;
        public float fadeSeconds = 0.8f;
        [Range(0f, 1f)] public float screenHeight = 0.86f;

        [Header("Sound")]
        public AudioClip whisper;
        [Range(0f, 1f)] public float whisperVolume = 0.6f;

        [Header("Hints ({shape} and {color} are filled in for the carried mirror)")]
        public Step lightTorch = new Step { hints = new[] {
            "The torch is cold... walk up to it and press E to light it.",
            "Without light, nothing in this hollow will answer you." } };
        public Step findMirrors = new Step { hints = new[] {
            "Three mirrors rest in the hollow. Each glows the colour of the stone it belongs on.",
            "Find a glowing mirror and press E to pick it up." } };
        public Step carryMirror = new Step { hints = new[] {
            "This is the {shape} mirror. Carry it to the {color} stone marked with the {shape}.",
            "Look at the {color} {shape} stone and press E to set the mirror down." } };
        public Step alignMirrors = new Step { hints = new[] {
            "The light must travel Oval, then Rectangle, then Circle, and on to the prism.",
            "Look at a placed mirror and hold Q or R to turn it. Its stone glows green when it faces true." } };
        public Step turnPrism = new Step { hints = new[] {
            "The prism drinks the light. Look at it and press E to turn it.",
            "Keep turning the prism until the moon burns red." } };
        public Step goToGate = new Step { hints = new[] {
            "The moon bleeds red. Something stirs beyond the gate to the east.",
            "Walk to the great gate." } };

        Transform player;
        PlayerInteraction interaction;
        AudioSource audioSource;
        int lastKey = int.MinValue;
        float stuck;
        float nextDelay;
        int index;
        string text;
        float shownAt = -100f;
        float hideAt = -100f;

        void Start()
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
            nextDelay = UnityEngine.Random.Range(delayRange.x, delayRange.y);
        }

        void Update()
        {
            if (player == null)
            {
                FPCharacterMover m = FindAnyObjectByType<FPCharacterMover>();
                if (m != null)
                {
                    player = m.transform;
                    interaction = m.GetComponentInChildren<PlayerInteraction>();
                    if (interaction == null) interaction = FindAnyObjectByType<PlayerInteraction>();
                }
                if (player == null) return;
            }

            MirrorPickup carried = interaction != null ? interaction.Carried : null;
            int key;
            Step step = Current(carried, out key);
            if (key != lastKey)
            {
                lastKey = key;
                stuck = 0f;
                index = 0;
                nextDelay = UnityEngine.Random.Range(delayRange.x, delayRange.y);
                if (Showing) hideAt = Mathf.Min(hideAt, Time.time);
            }

            bool inside = InArea(player.position);
            if (step == null || step.hints.Length == 0 || !inside || Time.timeScale <= 0f)
            {
                if (!inside && Showing) hideAt = Mathf.Min(hideAt, Time.time);
                return;
            }
            if (Showing) return;

            stuck += Time.deltaTime;
            if (stuck < nextDelay) return;

            text = Fill(step.hints[index % step.hints.Length], carried);
            index++;
            shownAt = Time.time;
            hideAt = Time.time + fadeSeconds + showSeconds;
            stuck = 0f;
            nextDelay = UnityEngine.Random.Range(delayRange.x, delayRange.y);
            if (whisper != null) audioSource.PlayOneShot(whisper, whisperVolume);
        }

        bool Showing => Time.time < hideAt + fadeSeconds;

        Step Current(MirrorPickup carried, out int key)
        {
            if (torch != null && !torch.IsLit) { key = 0; return lightTorch; }
            int occupied = 0, aligned = 0;
            foreach (MirrorSocket s in sockets)
            {
                if (s == null) continue;
                if (s.Occupied) occupied++;
                if (s.Aligned) aligned++;
            }
            int total = 0;
            foreach (MirrorSocket s in sockets) if (s != null) total++;
            if (occupied < total)
            {
                key = 100 + occupied * 10 + (carried != null ? 1 : 0);
                return carried != null ? carryMirror : findMirrors;
            }
            if (prism != null && !prism.Powered && !prism.Solved) { key = 300 + aligned; return alignMirrors; }
            if (prism != null && !prism.Solved) { key = 400; return turnPrism; }
            if (!ShadowRunGate.ShadowRunFinished) { key = 500; return goToGate; }
            key = 600;
            return null;
        }

        string Fill(string line, MirrorPickup carried)
        {
            string shape = "matching";
            string color = "glowing";
            if (carried != null)
            {
                foreach (MirrorSocket s in sockets)
                {
                    if (s != null && s.mirror == carried)
                    {
                        shape = s.shapeName;
                        color = ShapeColors.Name(s.shapeName);
                        break;
                    }
                }
            }
            return line.Replace("{shape}", shape).Replace("{color}", color);
        }

        bool InArea(Vector3 p)
        {
            Vector3 d = p - areaCenter;
            return Mathf.Abs(d.x) <= areaSize.x * 0.5f && Mathf.Abs(d.z) <= areaSize.y * 0.5f;
        }

        void OnGUI()
        {
            if (string.IsNullOrEmpty(text)) return;
            float now = Time.time;
            float a;
            if (now < shownAt + fadeSeconds) a = (now - shownAt) / fadeSeconds;
            else if (now < hideAt) a = 1f;
            else a = 1f - (now - hideAt) / fadeSeconds;
            a = Mathf.Clamp01(a);
            if (a <= 0f) return;

            Color bg = PromptBox.background, border = PromptBox.border, tc = PromptBox.textColor, sc = PromptBox.shadowColor;
            PromptBox.background = new Color(bg.r, bg.g, bg.b, bg.a * a);
            PromptBox.border = new Color(border.r, border.g, border.b, border.a * a);
            PromptBox.textColor = new Color(tc.r, tc.g, tc.b, tc.a * a);
            PromptBox.shadowColor = new Color(sc.r, sc.g, sc.b, sc.a * a);
            PromptBox.Draw(text, screenHeight);
            PromptBox.background = bg;
            PromptBox.border = border;
            PromptBox.textColor = tc;
            PromptBox.shadowColor = sc;
        }
    }
}
