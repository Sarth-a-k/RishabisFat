using System;
using System.Collections.Generic;
using UnityEngine;

namespace FPCharacter
{
    public sealed class ZoneMusic : MonoBehaviour
    {
        [Serializable]
        public class Zone
        {
            public string name;
            public Vector3 center;
            public Vector2 size;
            public AudioClip clip;
            [Range(0f, 1f)] public float volume = 1f;
            public bool resumeWhereLeft = true;
            [Tooltip("Seconds to wait after entering the area before the music starts.")]
            [Min(0f)] public float startDelay = 0f;
            [Tooltip("Seconds into the song where it starts playing.")]
            [Min(0f)] public float startAt = 0f;
            [Tooltip("Seconds the music takes to fade in. 0 or less uses the global Fade Seconds.")]
            public float fadeInSeconds = 0f;
        }

        public List<Zone> zones = new List<Zone>();
        [Range(0f, 1f)] public float masterVolume = 0.6f;
        public float fadeSeconds = 1.5f;

        AudioSource[] sources;
        int active;
        Zone current;
        Transform player;
        readonly Dictionary<AudioClip, float> resumeTimes = new Dictionary<AudioClip, float>();
        readonly float[] targetVol = new float[2];
        readonly float[] rate = new float[2];
        readonly float[] playAt = new float[2];
        bool silenced;

        public static void FadeOutAll(float seconds)
        {
            foreach (ZoneMusic zm in FindObjectsByType<ZoneMusic>(FindObjectsSortMode.None)) zm.FadeOut(seconds);
        }

        public void FadeOut(float seconds)
        {
            silenced = true;
            for (int i = 0; i < 2; i++)
            {
                playAt[i] = 0f;
                targetVol[i] = 0f;
                rate[i] = Mathf.Max(0.01f, sources[i].volume) / Mathf.Max(0.05f, seconds);
            }
        }

        void Awake()
        {
            sources = new AudioSource[2];
            for (int i = 0; i < 2; i++)
            {
                AudioSource s = gameObject.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.loop = true;
                s.spatialBlend = 0f;
                s.volume = 0f;
                sources[i] = s;
                rate[i] = 1f;
            }
        }

        void Update()
        {
            if (silenced)
            {
                for (int i = 0; i < 2; i++)
                {
                    sources[i].volume = Mathf.MoveTowards(sources[i].volume, 0f, rate[i] * Time.unscaledDeltaTime);
                    if (sources[i].isPlaying && sources[i].volume <= 0.001f) sources[i].Stop();
                }
                return;
            }
            if (player == null)
            {
                FPCharacterMover m = FindAnyObjectByType<FPCharacterMover>();
                if (m != null) player = m.transform;
            }
            Zone z = player != null ? Find(player.position) : null;
            if (z != current) Switch(z);
            if (current != null) targetVol[active] = current.volume * masterVolume;

            float now = Time.unscaledTime;
            float dt = Time.unscaledDeltaTime;
            for (int i = 0; i < 2; i++)
            {
                AudioSource s = sources[i];
                if (playAt[i] > 0f)
                {
                    if (now < playAt[i]) { s.volume = 0f; continue; }
                    playAt[i] = 0f;
                    s.Play();
                }
                s.volume = Mathf.MoveTowards(s.volume, targetVol[i], rate[i] * dt);
                if (i != active && s.isPlaying && s.volume <= 0.001f) s.Stop();
            }
        }

        Zone Find(Vector3 p)
        {
            foreach (Zone z in zones)
            {
                if (z == null) continue;
                Vector3 d = p - z.center;
                if (Mathf.Abs(d.x) <= z.size.x * 0.5f && Mathf.Abs(d.z) <= z.size.y * 0.5f) return z;
            }
            return null;
        }

        float FadeIn(Zone z)
        {
            float f = z.fadeInSeconds > 0f ? z.fadeInSeconds : fadeSeconds;
            return Mathf.Max(0.01f, z.volume * masterVolume) / Mathf.Max(0.05f, f);
        }

        void Switch(Zone z)
        {
            AudioSource old = sources[active];
            if (z != null && z.clip != null && old.clip == z.clip && (old.isPlaying || playAt[active] > 0f))
            {
                current = z;
                targetVol[active] = z.volume * masterVolume;
                rate[active] = FadeIn(z);
                return;
            }
            if (old.clip != null && old.isPlaying) resumeTimes[old.clip] = old.time;
            if (playAt[active] > 0f) { playAt[active] = 0f; old.Stop(); }
            targetVol[active] = 0f;
            rate[active] = Mathf.Max(0.01f, old.volume) / Mathf.Max(0.05f, fadeSeconds);
            current = z;
            if (z == null || z.clip == null) return;

            int other = 1 - active;
            AudioSource s = sources[other];
            if (s.clip == z.clip && s.isPlaying)
            {
                active = other;
                targetVol[active] = z.volume * masterVolume;
                rate[active] = FadeIn(z);
                return;
            }
            s.Stop();
            s.clip = z.clip;
            float t;
            float start = z.resumeWhereLeft && resumeTimes.TryGetValue(z.clip, out t) ? t : z.startAt;
            s.time = Mathf.Repeat(start, z.clip.length);
            s.volume = 0f;
            active = other;
            targetVol[active] = z.volume * masterVolume;
            rate[active] = FadeIn(z);
            if (z.startDelay > 0f) playAt[active] = Time.unscaledTime + z.startDelay;
            else { playAt[active] = 0f; s.Play(); }
        }
    }
}
