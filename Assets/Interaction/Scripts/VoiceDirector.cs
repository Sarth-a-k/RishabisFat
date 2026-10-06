using System;
using System.Collections.Generic;
using UnityEngine;

namespace FPCharacter
{
    public sealed class VoiceDirector : MonoBehaviour
    {
        public VoiceLineSet voiceLines;
        public string resourcePath = "Dialogue/VoiceLines";
        public Vector3 hollowsCenter = new Vector3(48.85f, 0f, 0f);
        public Vector2 hollowsSize = new Vector2(48.1f, 36.4f);
        public float torchHintFirst = 45f;
        public float torchHintRepeat = 45f;
        public float mirrorHintDelay = 4f;
        public float prismHintDelay = 1.5f;
        public float redMoonHintDelay = 3.5f;
        public float npcPassDistance = 4f;
        public float subtitlePad = 0.4f;
        public float returnLineDelay = 2f;
        public Vector3 gogglesRoomCenter = new Vector3(112.15f, 0f, 1.5f);
        public Vector2 gogglesRoomSize = new Vector2(46.5f, 34f);
        public float gogglesHintAfter = 12f;
        public float sitHintAfter = 45f;
        public Vector3 finalRoomCenter = new Vector3(237f, 0f, 0f);
        public Vector2 finalRoomSize = new Vector2(52f, 52f);
        public float journalHintAfter = 45f;
        float currentStart;

        static readonly HashSet<string> done = new HashSet<string>();
        static readonly HashSet<string> npcsHandled = new HashSet<string>();
        static int torchHintsPlayed;
        static string returnLine;

        public static void QueueOnReturn(string key) { returnLine = key; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { done.Clear(); npcsHandled.Clear(); torchHintsPlayed = 0; returnLine = null; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Hook()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnLoaded;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnLoaded;
            TrySpawn();
        }

        static void OnLoaded(UnityEngine.SceneManagement.Scene s, UnityEngine.SceneManagement.LoadSceneMode m) { TrySpawn(); }

        static void TrySpawn()
        {
            if (FindAnyObjectByType<VoiceDirector>() != null) return;
            if (FindAnyObjectByType<FPCharacterMover>() == null) return;
            new GameObject("Voice Director").AddComponent<VoiceDirector>();
        }

        class Pending { public string key; public float at; public Func<bool> stillValid; }

        readonly List<Pending> queue = new List<Pending>();
        AudioSource source;
        VoiceLineSet.Line current;
        float currentUntil = -1f;
        Transform player;
        PuzzleTorch torch;
        PrismMoonController prism;
        MirrorPickup[] mirrors;
        NPCCutscene[] npcs;
        InfraredGogglesPickup goggles;
        float inGogglesRoom, inEmberUnseated, inFinalRoom;
        PlayerSeat emberSeat;
        bool seatUsed;
        GameObject finalJournal;
        float inHollows, torchLitAt = -1f, poweredAt = -1f, solvedAt = -1f, lastTorchHint = -1f;
        bool mirrorTouched;

        void Start()
        {
            if (voiceLines == null) voiceLines = Resources.Load<VoiceLineSet>(resourcePath);
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            foreach (PuzzleTorch t in FindObjectsByType<PuzzleTorch>(FindObjectsInactive.Include)) if (Inside(t.transform.position)) { torch = t; break; }
            prism = FindAnyObjectByType<PrismMoonController>(FindObjectsInactive.Include);
            mirrors = FindObjectsByType<MirrorPickup>(FindObjectsInactive.Include);
            npcs = FindObjectsByType<NPCCutscene>(FindObjectsInactive.Include);
            goggles = FindAnyObjectByType<InfraredGogglesPickup>(FindObjectsInactive.Exclude);
            foreach (PlayerSeat s in FindObjectsByType<PlayerSeat>(FindObjectsInactive.Include))
                if (InRoom(s.transform.position, gogglesRoomCenter, gogglesRoomSize)) { emberSeat = s; break; }
            JournalReader jr = FindAnyObjectByType<JournalReader>(FindObjectsInactive.Include);
            if (jr != null) finalJournal = jr.gameObject;
            if (!string.IsNullOrEmpty(returnLine))
            {
                Enqueue(returnLine, returnLineDelay, null);
                returnLine = null;
            }
            if (PrismMoonController.SolvedThisSession) { done.Add("Hint_Torch"); done.Add("Hint_Mirrors"); done.Add("Hint_PrismRotate"); done.Add("Hint_RedMoon"); }
        }

        bool Inside(Vector3 p)
        {
            Vector3 d = p - hollowsCenter;
            return Mathf.Abs(d.x) <= hollowsSize.x * 0.5f && Mathf.Abs(d.z) <= hollowsSize.y * 0.5f;
        }

        bool Busy() { return source.isPlaying || Time.time < currentUntil || CutscenePlaying() || JournalReader.Reading; }

        bool CutscenePlaying()
        {
            if (npcs != null) foreach (NPCCutscene n in npcs) if (n != null && n.IsPlaying) return true;
            return FindAnyObjectByType<OpeningCutscene>() != null;
        }

        void Enqueue(string key, float delay, Func<bool> valid, bool once = true)
        {
            if (once && done.Contains(key)) return;
            foreach (Pending p in queue) if (p.key == key) return;
            if (once) done.Add(key);
            queue.Add(new Pending { key = key, at = Time.time + delay, stillValid = valid });
        }

        void Update()
        {
            if (voiceLines == null) return;
            if (player == null)
            {
                FPCharacterMover m = FindAnyObjectByType<FPCharacterMover>();
                if (m != null) player = m.transform;
                if (player == null) return;
            }
            UpdateNpcs();
            UpdateHollows();
            UpdateGoggles();
            UpdateSitHint();
            UpdateJournalHint();
            if (current != null && Time.time >= currentUntil && !source.isPlaying) current = null;
            if (Busy() || queue.Count == 0) return;
            for (int i = 0; i < queue.Count; i++)
            {
                Pending p = queue[i];
                if (Time.time < p.at) continue;
                queue.RemoveAt(i);
                if (p.stillValid != null && !p.stillValid()) return;
                Play(p.key);
                return;
            }
        }

        void UpdateNpcs()
        {
            if (npcs == null || voiceLines.npcIgnoreKeys.Count == 0) return;
            foreach (NPCCutscene n in npcs)
            {
                if (n == null || n.HasPlayed || n.IsPlaying) continue;
                string id = n.name + n.transform.position.ToString("F0");
                if (npcsHandled.Contains(id)) continue;
                Vector3 d = player.position - n.transform.position;
                if (d.x > npcPassDistance && Mathf.Abs(d.z) < 10f)
                {
                    npcsHandled.Add(id);
                    string key = voiceLines.npcIgnoreKeys[UnityEngine.Random.Range(0, voiceLines.npcIgnoreKeys.Count)];
                    NPCCutscene who = n;
                    Enqueue(key, 0.3f, () => !who.HasPlayed, false);
                }
            }
        }

        bool GogglesStillLying()
        {
            if (goggles == null || !goggles.gameObject.activeInHierarchy) return false;
            PlayerInteraction pi = PlayerInteraction.Instance;
            return pi == null || !pi.hasGoggles;
        }

        static bool InRoom(Vector3 p, Vector3 c, Vector2 size)
        {
            Vector3 d = p - c;
            return Mathf.Abs(d.x) <= size.x * 0.5f && Mathf.Abs(d.z) <= size.y * 0.5f;
        }

        bool StillUnseated()
        {
            if (emberSeat == null || seatUsed || MinigameGate.IsFinished("warmstatues")) return false;
            if (emberSeat.Occupied) { seatUsed = true; return false; }
            return true;
        }

        void UpdateSitHint()
        {
            if (done.Contains("Hint_SitChair") || !StillUnseated()) return;
            if (!InRoom(player.position, gogglesRoomCenter, gogglesRoomSize)) return;
            inEmberUnseated += Time.deltaTime;
            if (inEmberUnseated >= sitHintAfter) Enqueue("Hint_SitChair", 0f, StillUnseated);
        }

        bool JournalUntouched()
        {
            return finalJournal != null && finalJournal.activeInHierarchy;
        }

        void UpdateJournalHint()
        {
            if (done.Contains("Hint_Journal") || !JournalUntouched()) return;
            if (!InRoom(player.position, finalRoomCenter, finalRoomSize)) return;
            inFinalRoom += Time.deltaTime;
            if (inFinalRoom >= journalHintAfter) Enqueue("Hint_Journal", 0f, JournalUntouched);
        }

        void UpdateGoggles()
        {
            if (done.Contains("Hint_Goggles") || !GogglesStillLying()) return;
            Vector3 d = player.position - gogglesRoomCenter;
            if (Mathf.Abs(d.x) > gogglesRoomSize.x * 0.5f || Mathf.Abs(d.z) > gogglesRoomSize.y * 0.5f) return;
            inGogglesRoom += Time.deltaTime;
            if (inGogglesRoom >= gogglesHintAfter) Enqueue("Hint_Goggles", 0f, GogglesStillLying);
        }

        void UpdateHollows()
        {
            bool inside = Inside(player.position);
            bool solved = prism != null && prism.Solved;
            bool lit = torch != null && torch.IsLit;
            if (inside && !solved) inHollows += Time.deltaTime;

            if (torch != null && !lit && inside && !solved)
            {
                if (torchHintsPlayed == 0 && inHollows >= torchHintFirst) { torchHintsPlayed = 1; lastTorchHint = Time.time; Enqueue("Hint_Torch", 0f, () => !torch.IsLit, false); }
                else if (torchHintsPlayed == 1 && lastTorchHint > 0f && Time.time - lastTorchHint >= torchHintRepeat) { torchHintsPlayed = 2; Enqueue("Hint_Torch", 0f, () => !torch.IsLit, false); }
            }
            if (lit && torchLitAt < 0f) torchLitAt = Time.time;

            if (solved && solvedAt < 0f) solvedAt = Time.time;
            if (solvedAt > 0f) Enqueue("Hint_RedMoon", redMoonHintDelay, null);
        }

        void Play(string key)
        {
            VoiceLineSet.Line l = voiceLines.Find(key);
            if (l == null) { Debug.LogWarning("VoiceDirector: no voice line named " + key); return; }
            current = l;
            float len = l.clip != null ? l.clip.length : Mathf.Max(2.5f, l.subtitle.Length * 0.06f);
            currentUntil = Time.time + len + subtitlePad;
            currentStart = Time.time;
            if (l.clip != null) source.PlayOneShot(l.clip, l.volume);
        }

        void OnGUI()
        {
            if (current == null || Time.time > currentUntil || string.IsNullOrEmpty(current.subtitle)) return;
            SubtitleBox.Draw(string.IsNullOrEmpty(current.speaker) ? SubtitleBox.Player : current.speaker, current.subtitle, SubtitleBox.Fade(currentStart, currentUntil, 0.25f));
        }
    }
}
