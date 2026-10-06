using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FPCharacter
{
    public sealed class LoopEnding : MonoBehaviour
    {
        public GameObject sittingGhost;
        public JournalReader journal;
        public string menuScene = "MainMenu";
        public float delayAfterJournal = 5f;
        public Vector3 startPoint = new Vector3(1.5f, 0f, -0.6f);
        public Vector3[] walkPath = { new Vector3(5.5f, 0f, 0.2f), new Vector3(8.3f, 0f, 0.4f), new Vector3(17f, 0f, 0.2f), new Vector3(24f, 0f, 0f), new Vector3(34f, 0f, 0f) };
        public float fadeOutAtX = 21.5f;
        public float eyeForward = 0.13f;
        public float lookAheadOfLap = 0.35f;

        bool running;
        CanvasGroup black;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Hook()
        {
            SceneManager.sceneLoaded -= OnLoaded;
            SceneManager.sceneLoaded += OnLoaded;
            TrySpawn();
        }

        static void OnLoaded(Scene s, LoadSceneMode m) { TrySpawn(); }

        static void TrySpawn()
        {
            if (FindAnyObjectByType<LoopEnding>() != null) return;
            if (FindAnyObjectByType<FPCharacterMover>() == null) return;
            new GameObject("Loop Ending").AddComponent<LoopEnding>();
        }

        void Start()
        {
            if (sittingGhost == null)
                foreach (NPCCutscene n in FindObjectsByType<NPCCutscene>(FindObjectsInactive.Include))
                    if (n.videoFileName == "npc1cutscene.mp4") { sittingGhost = n.gameObject; break; }
            if (journal == null) journal = FindAnyObjectByType<JournalReader>();
            if (journal != null) journal.onFinishedReading.AddListener(OnJournalDone);
        }

        void OnDestroy()
        {
            if (journal != null) journal.onFinishedReading.RemoveListener(OnJournalDone);
        }

        void OnJournalDone()
        {
            if (!running) StartCoroutine(DelayThenPlay());
        }

        IEnumerator DelayThenPlay()
        {
            yield return new WaitForSeconds(delayAfterJournal);
            Play();
        }

        public void Play()
        {
            if (running) return;
            running = true;
            StartCoroutine(Run());
        }

        public void PlayFromBlack()
        {
            if (running) return;
            running = true;
            StartCoroutine(Run(true));
        }

        IEnumerator Run(bool fromBlack = false)
        {
            BuildFader();
            if (fromBlack) black.alpha = 1f;
            else yield return Fade(0f, 1f, 2f);

            FPCharacterMover mover = FindAnyObjectByType<FPCharacterMover>();
            Camera playerCam = Camera.main;
            foreach (MonoBehaviour m in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude))
            {
                if (m == null || m == this) continue;
                string n = m.GetType().Name;
                if (n == "FPCharacterMover" || n == "PlayerInteraction" || n == "VoiceDirector" || n == "PuzzleHints" || n == "UVBaton" || n == "AreaTitleManager" || n == "LookThought" || n == "JournalReader") m.enabled = false;
            }
            Transform ghostHead = null;
            var lapBones = new List<Transform>();
            if (sittingGhost != null)
            {
                foreach (MonoBehaviour mb in sittingGhost.GetComponentsInChildren<MonoBehaviour>(true)) if (mb.GetType().Name == "QuestMarker" || mb.GetType().Name == "NPCCutscene") mb.enabled = false;
                foreach (Transform t2 in sittingGhost.GetComponentsInChildren<Transform>(true))
                {
                    if (t2.name == "Quest marker") t2.gameObject.SetActive(false);
                    if (t2.name == "Head") ghostHead = t2;
                    if (t2.name == "LeftHand" || t2.name == "RightHand" || t2.name == "LeftLeg" || t2.name == "RightLeg" || t2.name == "LeftFoot" || t2.name == "RightFoot") lapBones.Add(t2);
                }
            }
            Vector3 eye = sittingGhost != null ? HeadOf(sittingGhost) : new Vector3(10f, 1.2f, 3.5f);
            if (mover != null)
            {
                var cc = mover.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;
                mover.transform.position = new Vector3(eye.x, 0.12f, eye.z);
                foreach (Renderer r in mover.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
                foreach (Light l in mover.GetComponentsInChildren<Light>(true)) l.enabled = false;
            }
            if (playerCam != null)
            {
                playerCam.enabled = false;
                var al = playerCam.GetComponent<AudioListener>();
                if (al != null) al.enabled = false;
            }
            var camGo = new GameObject("Loop Ending Camera");
            camGo.transform.SetParent(transform, false);
            Camera cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 62f;
            cam.nearClipPlane = 0.2f;
            camGo.AddComponent<AudioListener>();
            camGo.tag = "MainCamera";
            camGo.transform.position = eye;

            var explorerGo = new GameObject("The next explorer");
            Vector3 sp = Ground(startPoint);
            explorerGo.transform.position = sp;
            var walker = explorerGo.AddComponent<SpriteWalker>();
            foreach (Vector3 w in walkPath) walker.path.Add(Ground(w));
            walker.pauseAtIndex = 1;
            walker.pauseSeconds = 1.8f;
            Transform explorerT = explorerGo.transform;
            GameObject explorer = explorerGo;
            System.Func<Vector3> lapRaw = () =>
            {
                if (lapBones.Count == 0) return eye + Vector3.down * 1f + Vector3.left * 0.6f;
                Vector3 sum = Vector3.zero;
                foreach (Transform b2 in lapBones) sum += b2.position;
                return sum / lapBones.Count;
            };
            System.Func<Vector3> fwdNow = () =>
            {
                Vector3 h = ghostHead != null ? ghostHead.position : eye;
                Vector3 f = lapRaw() - h; f.y = 0f;
                return f.sqrMagnitude > 1e-4f ? f.normalized : Vector3.left;
            };
            System.Func<Vector3> eyeNow = () => (ghostHead != null ? ghostHead.position + Vector3.up * 0.06f : eye) + fwdNow() * eyeForward;
            System.Func<Vector3> lapNow = () => lapRaw() + fwdNow() * lookAheadOfLap;
            Quaternion look = Quaternion.LookRotation(lapNow() - eyeNow(), Vector3.up);
            camGo.transform.rotation = look;
            StartCoroutine(Fade(1f, 0f, 2.2f));
            float t = 0f;
            bool started = false, fading = false;
            while (true)
            {
                t += Time.deltaTime;
                if (!started && t > 3.2f) { walker.walking = true; started = true; }
                Vector3 eyePos = eyeNow();
                float ghostX = sittingGhost != null ? sittingGhost.transform.position.x : 10f;
                bool passed = explorerT.position.x > ghostX + 1.2f && !walker.IsPaused;
                bool watch = started && t > 4.6f && !passed;
                {
                    Vector3 target = watch ? explorerT.position + Vector3.up * 1.25f : lapNow();
                    Quaternion want = Quaternion.LookRotation(target - eyePos, Vector3.up);
                    look = Quaternion.Slerp(look, want, 1f - Mathf.Exp((watch ? -1.8f : -1.1f) * Time.deltaTime));
                }
                float breathe = Mathf.Sin(t * 1.3f) * 0.6f;
                camGo.transform.SetPositionAndRotation(eyePos + Vector3.up * Mathf.Sin(t * 1.3f) * 0.006f, look * Quaternion.Euler(breathe * 0.3f, breathe * 0.4f, 0f));
                if (!fading && explorer != null && explorer.transform.position.x > fadeOutAtX) { fading = true; StartCoroutine(Fade(0f, 1f, 2.5f)); }
                if (fading && black.alpha >= 0.999f) break;
                if (explorer == null && t > 6f) break;
                yield return null;
            }
            yield return new WaitForSeconds(1.5f);
            SessionReset.ResetAll();
            SceneManager.LoadScene(menuScene);
        }

        void BuildFader()
        {
            var go = new GameObject("Loop Ending Fade");
            go.transform.SetParent(transform, false);
            var c = go.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = 3000;
            black = go.AddComponent<CanvasGroup>();
            black.alpha = 0f;
            black.blocksRaycasts = false;
            var img = new GameObject("Black", typeof(RectTransform)).AddComponent<Image>();
            img.transform.SetParent(go.transform, false);
            var rt = img.rectTransform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            img.color = Color.black;
            img.raycastTarget = false;
        }

        IEnumerator Fade(float from, float to, float seconds)
        {
            for (float f = 0f; f < 1f; f += Time.deltaTime / Mathf.Max(0.05f, seconds))
            {
                black.alpha = Mathf.Lerp(from, to, f * f * (3f - 2f * f));
                yield return null;
            }
            black.alpha = to;
        }

        static Vector3 Ground(Vector3 p)
        {
            if (Physics.Raycast(new Vector3(p.x, 3f, p.z), Vector3.down, out RaycastHit h, 8f, ~0, QueryTriggerInteraction.Ignore)) return h.point;
            return new Vector3(p.x, 0f, p.z);
        }

        static Vector3 HeadOf(GameObject g)
        {
            Transform h = FindBone(g.transform, "Head");
            if (h != null) return h.position + Vector3.up * 0.08f;
            Bounds b = BoundsOf(g);
            return new Vector3(b.center.x, b.max.y - 0.15f, b.center.z);
        }

        static Transform FindBone(Transform root, string n)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true)) if (t.name == n) return t;
            return null;
        }

        static Bounds BoundsOf(GameObject g)
        {
            Renderer[] rs = g.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return new Bounds(g.transform.position, Vector3.one * 0.2f);
            Bounds b = rs[0].bounds;
            foreach (Renderer r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        static void MakeSolid(GameObject g)
        {
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            var cache = new Dictionary<Material, Material>();
            foreach (Renderer r in g.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer) continue;
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    Material src = mats[i];
                    if (src == null) continue;
                    if (!cache.TryGetValue(src, out Material m))
                    {
                        m = new Material(lit);
                        Color c = src.HasProperty("_BaseColor") ? src.GetColor("_BaseColor") : Color.gray;
                        Color.RGBToHSV(c, out float h, out float s, out float v);
                        c = Color.HSVToRGB(h, Mathf.Clamp01(s * 1.5f + 0.08f), v * 0.85f);
                        c.a = 1f;
                        m.SetColor("_BaseColor", c);
                        if (src.HasProperty("_BaseMap") && src.GetTexture("_BaseMap") != null) m.SetTexture("_BaseMap", src.GetTexture("_BaseMap"));
                        m.SetFloat("_Smoothness", 0.25f);
                        cache[src] = m;
                    }
                    mats[i] = m;
                }
                r.sharedMaterials = mats;
            }
        }
    }
}
