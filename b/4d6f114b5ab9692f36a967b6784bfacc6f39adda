using System;
using System.Collections.Generic;
using UnityEngine;

namespace FPCharacter
{
    public sealed class AreaTitleManager : MonoBehaviour
    {
        [Serializable]
        public class Area
        {
            public string name;
            public Vector3 center;
            public Vector2 size;
            public Texture2D title;
            public AudioClip sound;
            [NonSerialized] public bool shown;
        }

        public List<Area> areas = new List<Area>();
        static readonly HashSet<string> shownThisSession = new HashSet<string>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { shownThisSession.Clear(); }
        public bool rememberBetweenSessions;
        public float fadeIn = 1.2f;
        public float hold = 2.6f;
        public float fadeOut = 1.6f;
        public float startScale = 0.96f;
        public float endScale = 1.04f;
        [Range(0.2f, 1f)] public float widthOfScreen = 0.62f;
        [Range(0f, 1f)] public float heightOnScreen = 0.36f;
        [Range(0f, 1f)] public float bandOpacity = 0.55f;
        [Range(0f, 1f)] public float volume = 1f;

        Transform player;
        AudioSource audioSource;
        readonly Queue<Area> queue = new Queue<Area>();
        Area current;
        float startedAt;
        Texture2D band;

        void Start()
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
            if (rememberBetweenSessions)
                foreach (Area a in areas) a.shown = PlayerPrefs.GetInt("AreaTitle_" + a.name, 0) == 1;
            foreach (Area a in areas) if (shownThisSession.Contains(a.name)) a.shown = true;
            band = new Texture2D(1, 64, TextureFormat.RGBA32, false);
            band.wrapMode = TextureWrapMode.Clamp;
            for (int y = 0; y < 64; y++)
            {
                float v = Mathf.Abs((y + 0.5f) / 64f * 2f - 1f);
                band.SetPixel(0, y, new Color(0f, 0f, 0f, Mathf.Pow(1f - v, 1.6f)));
            }
            band.Apply();
        }

        void Update()
        {
            if (player == null)
            {
                FPCharacterMover m = FindAnyObjectByType<FPCharacterMover>();
                if (m != null) player = m.transform;
                if (player == null) return;
            }
            Vector3 p = player.position;
            foreach (Area a in areas)
            {
                if (a.shown) continue;
                Vector3 d = p - a.center;
                if (Mathf.Abs(d.x) <= a.size.x * 0.5f && Mathf.Abs(d.z) <= a.size.y * 0.5f)
                {
                    a.shown = true;
                    shownThisSession.Add(a.name);
                    if (rememberBetweenSessions) { PlayerPrefs.SetInt("AreaTitle_" + a.name, 1); PlayerPrefs.Save(); }
                    queue.Enqueue(a);
                }
            }
            if (current == null && queue.Count > 0)
            {
                current = queue.Dequeue();
                startedAt = Time.unscaledTime;
                if (current.sound != null) audioSource.PlayOneShot(current.sound, volume);
            }
            if (current != null && Time.unscaledTime - startedAt > fadeIn + hold + fadeOut) current = null;
        }

        public void ResetShown()
        {
            shownThisSession.Clear();
            foreach (Area a in areas)
            {
                a.shown = false;
                PlayerPrefs.DeleteKey("AreaTitle_" + a.name);
            }
        }

        void OnGUI()
        {
            if (current == null || current.title == null) return;
            float t = Time.unscaledTime - startedAt;
            float total = fadeIn + hold + fadeOut;
            float a = t < fadeIn ? t / fadeIn : t < fadeIn + hold ? 1f : 1f - (t - fadeIn - hold) / fadeOut;
            a = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(a));
            float s = Mathf.Lerp(startScale, endScale, Mathf.Clamp01(t / total));
            float w = Screen.width * widthOfScreen * s;
            float h = w * current.title.height / Mathf.Max(1f, current.title.width);
            float cx = Screen.width * 0.5f, cy = Screen.height * heightOnScreen;
            Color old = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, a * bandOpacity);
            GUI.DrawTexture(new Rect(0f, cy - h * 0.42f, Screen.width, h * 0.84f), band, ScaleMode.StretchToFill, true);
            GUI.color = new Color(1f, 1f, 1f, a);
            GUI.DrawTexture(new Rect(cx - w * 0.5f, cy - h * 0.5f, w, h), current.title, ScaleMode.ScaleToFit, true);
            GUI.color = old;
        }
    }
}
