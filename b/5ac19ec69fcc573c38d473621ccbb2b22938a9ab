using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FPCharacter
{
    public sealed class MirrorSabotage : MonoBehaviour
    {
        public float delay = 2.6f;
        public float twistMin = 18f;
        public float twistMax = 24f;
        public float twistSeconds = 0.7f;
        public string thought = "\"I bet I placed this right...\"";
        public float thoughtSeconds = 3.5f;

        static bool done;
        MirrorSocket[] sockets;
        readonly Dictionary<MirrorSocket, float> occupiedAt = new Dictionary<MirrorSocket, float>();
        bool armed = true;
        float thoughtUntil = -1f;
        AudioSource audioSource;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { done = false; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Spawn()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnLoaded;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnLoaded;
            TryCreate();
        }

        static void OnLoaded(UnityEngine.SceneManagement.Scene s, UnityEngine.SceneManagement.LoadSceneMode m) { TryCreate(); }

        static void TryCreate()
        {
            if (FindAnyObjectByType<MirrorSabotage>() != null) return;
            if (FindObjectsByType<MirrorSocket>(FindObjectsInactive.Exclude).Length < 3) return;
            new GameObject("Mirror Sabotage").AddComponent<MirrorSabotage>();
        }

        void Start()
        {
            sockets = FindObjectsByType<MirrorSocket>(FindObjectsInactive.Exclude);
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1f;
            audioSource.minDistance = 2f;
            audioSource.maxDistance = 25f;
            if (done || PrismMoonController.SolvedThisSession) armed = false;
        }

        void Update()
        {
            if (!armed || sockets == null) return;
            if (PrismMoonController.SolvedThisSession) { armed = false; return; }
            int count = 0;
            foreach (MirrorSocket s in sockets)
            {
                if (s == null) continue;
                if (s.Occupied)
                {
                    count++;
                    if (!occupiedAt.ContainsKey(s)) occupiedAt[s] = Time.time;
                }
                else occupiedAt.Remove(s);
            }
            if (count >= 3)
            {
                armed = false;
                StartCoroutine(Twist());
            }
        }

        IEnumerator Twist()
        {
            yield return new WaitForSeconds(delay);
            if (PrismMoonController.SolvedThisSession) yield break;
            MirrorSocket latest = null;
            float latestT = -1f;
            foreach (var kv in occupiedAt) if (kv.Key != null && kv.Value > latestT) { latestT = kv.Value; latest = kv.Key; }
            var candidates = new List<MirrorSocket>();
            foreach (MirrorSocket s in sockets)
                if (s != null && s != latest && s.Occupied && s.mirror != null && !s.mirror.IsCarried) candidates.Add(s);
            if (candidates.Count == 0) yield break;
            MirrorSocket pick = candidates[Random.Range(0, candidates.Count)];
            Transform m = pick.mirror.transform;
            float sign = Random.value < 0.5f ? -1f : 1f;
            float amount = Random.Range(twistMin, twistMax) * sign;
            Quaternion from = m.rotation;
            Quaternion to = Quaternion.Euler(0f, amount, 0f) * from;
            AudioClip scrape = Resources.Load<AudioClip>("WarmStatues/scrape");
            if (scrape != null) { audioSource.transform.position = m.position; audioSource.PlayOneShot(scrape, 0.55f); }
            for (float t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(0.05f, twistSeconds))
            {
                if (pick.mirror == null || pick.mirror.IsCarried) yield break;
                float e = t * t * (3f - 2f * t);
                m.rotation = Quaternion.Slerp(from, to, e);
                yield return null;
            }
            m.rotation = to;
            done = true;
            thoughtUntil = Time.time + thoughtSeconds;
        }

        void OnGUI()
        {
            if (Time.time < thoughtUntil) SubtitleBox.Draw(thought, SubtitleBox.Fade(thoughtUntil - thoughtSeconds, thoughtUntil));
        }
    }
}
