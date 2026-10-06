using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace FPCharacter
{
    // The two-journal ending, reworked for feel:
    //  - the player notices the old journal, reaches for it, lifts it with a little weight and
    //    turns it over in the hand while dust drifts off it;
    //  - the OLD journal now opens (it used to stay shut): the stiff binding lifts, sticks, then
    //    falls open with a soft thud and a puff of dust;
    //  - the player's own journal comes up from below and opens briskly;
    //  - the pages are compared one after the other with glances left and right, getting faster,
    //    the hands start to tremble, and on the last page the view narrows, the edges darken and a
    //    heartbeat rises before the fade to the ending video.
    // Both books stay still in the hands while the camera glances, so the eye moves, not the books.
    // Sounds load from Resources/Journal/ (included); anything missing is simply skipped.
    public sealed class JournalCompareEnding : MonoBehaviour
    {
        public string videoFileName = "Cutscene_Journal_Ending_3D.mp4";
        [Range(0f, 1f)] public float videoVolume = 1f;
        public bool playLoopEndingAfter = true;
        public string menuScene = "MainMenu";
        public float useDistance = 3f;
        public string prompt = "[E] Pick up the old journal";
        public string linePickup = "\"This journal... it's the same as mine.\"";
        public string lineCompare = "\"Same cover. Same torn corner. Same handwriting.\"";
        public string lineLast = "\"Every page... I wrote these.\"";
        public int pageTurns = 4;
        public float holdDistance = 0.48f;
        public float holdSide = 0.165f;
        public float holdDrop = 0.12f;
        public float holdTilt = 37f;

        [Header("Feel")]
        [Tooltip("How far the eyes glance left/right between the books, in degrees.")]
        public float glanceAngle = 7f;
        [Tooltip("How much the hands shake by the last page (0 = steady).")]
        [Range(0f, 2f)] public float trembleAmount = 1f;
        [Tooltip("How much the view narrows on the last page, in degrees of field of view.")]
        public float realiseZoom = 9f;
        [Range(0f, 1f)] public float soundVolume = 0.8f;
        public bool dust = true;

        const float PageW = 0.14f, PageH = 0.2f;

        GameObject oldBookWorld;
        bool looking, running;
        string subtitle;
        float subtitleUntil, subtitleAt;
        Transform cam;
        Camera camComp;
        Book oldBook, ownBook;
        AudioSource sfx, heart;
        AudioClip thumpFallback, touchSound, liftSound, creakSound, thudSound, leatherSound, openNewSound, heartbeatSound;
        readonly List<AudioClip> flips = new List<AudioClip>();
        readonly List<Texture2D> pages = new List<Texture2D>();
        VideoPlayer vp;
        RenderTexture videoRT;
        CanvasGroup black;
        RawImage screen, vignette;
        Text skipLabel;
        bool videoDone;
        Texture2D dustTex, vignetteTex;
        Material dustMat;

        // reading frame: where the books are held, independent of where the eyes look
        Vector3 framePos;
        Quaternion frameRot;
        // live pose state, applied every frame by PoseBooks()
        bool oldHeld, ownHeld;
        float wl, wr, tremble, closer;
        Vector3 oldInspect, ownSlide;
        float ownRoll;
        PlayerInteraction pi;

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
            if (FindAnyObjectByType<JournalCompareEnding>() != null) return;
            if (FindAnyObjectByType<JournalReader>() == null) return;
            new GameObject("Journal Compare Ending").AddComponent<JournalCompareEnding>();
        }

        void Start()
        {
            JournalReader reader = FindAnyObjectByType<JournalReader>();
            if (reader != null)
            {
                oldBookWorld = reader.gameObject;
                reader.enabled = false;
                AddGlow(oldBookWorld);
            }
            sfx = gameObject.AddComponent<AudioSource>();
            sfx.playOnAwake = false;
            sfx.spatialBlend = 0f;
            heart = gameObject.AddComponent<AudioSource>();
            heart.playOnAwake = false;
            heart.spatialBlend = 0f;
            heart.loop = true;
            heart.volume = 0f;

            thumpFallback = Resources.Load<AudioClip>("WarmStatues/thump");
            for (int i = 1; i <= 3; i++)
            {
                AudioClip c = Resources.Load<AudioClip>("Journal/page_flip_" + i);
                if (c != null) flips.Add(c);
            }
            touchSound = Resources.Load<AudioClip>("Journal/book_touch");
            liftSound = Resources.Load<AudioClip>("Journal/book_lift_dust");
            creakSound = Resources.Load<AudioClip>("Journal/old_binding_creak");
            thudSound = Resources.Load<AudioClip>("Journal/book_fall_open");
            leatherSound = Resources.Load<AudioClip>("Journal/leather_slide");
            openNewSound = Resources.Load<AudioClip>("Journal/book_open_new");
            heartbeatSound = Resources.Load<AudioClip>("Journal/heartbeat");
        }

        public bool allowVideoSkip = false;

        static void AddGlow(GameObject book)
        {
            if (book.GetComponent<PickupHighlight>() != null) return;
            PickupHighlight src = null;
            foreach (PickupHighlight h in FindObjectsByType<PickupHighlight>(FindObjectsInactive.Include))
                if (h.glowMaterial != null) { src = h; break; }
            if (src == null) return;
            PickupHighlight g = book.AddComponent<PickupHighlight>();
            g.glowMaterial = src.glowMaterial;
            g.sparkleMaterial = src.sparkleMaterial;
            g.color = new Color(1f, 0.8f, 0.45f, 1f);
            g.intensity = 0.7f;
            g.sparklesPerSecond = 2f;
        }

        void Update()
        {
            if (running)
            {
                if (allowVideoSkip && vp != null && vp.isPlaying && !videoDone && SkipPressed()) videoDone = true;
                return;
            }
            looking = false;
            Camera c = Camera.main;
            if (c == null || oldBookWorld == null || !oldBookWorld.activeInHierarchy) return;
            looking = LookingAtBook(c.transform);
            if (looking && PrismPickup.InteractPressed()) Begin();
        }

        // Forgiving aim check: a thick ray that also sees trigger colliders, ignoring the player's own
        // colliders, plus a fallback "looking roughly at it and nothing in the way" test.
        bool LookingAtBook(Transform c)
        {
            Transform book = oldBookWorld.transform;
            Transform player = null;
            FPCharacterMover mover = FindAnyObjectByType<FPCharacterMover>();
            if (mover != null) player = mover.transform;

            RaycastHit[] hits = Physics.SphereCastAll(new Ray(c.position, c.forward), 0.06f, useDistance, ~0, QueryTriggerInteraction.Collide);
            float best = float.MaxValue;
            Transform bestT = null;
            foreach (RaycastHit h in hits)
            {
                if (player != null && h.collider.transform.IsChildOf(player)) continue;
                if (h.distance < best) { best = h.distance; bestT = h.collider.transform; }
            }
            if (bestT != null && bestT.IsChildOf(book)) return true;

            Vector3 centre = BookCentre(book);
            Vector3 to = centre - c.position;
            if (to.magnitude > useDistance || Vector3.Angle(c.forward, to) > 10f) return false;
            if (Physics.Raycast(c.position, to.normalized, out RaycastHit block, to.magnitude - 0.05f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (!block.collider.transform.IsChildOf(book) && (player == null || !block.collider.transform.IsChildOf(player))) return false;
            }
            return true;
        }

        static Vector3 BookCentre(Transform t)
        {
            Renderer r = t.GetComponentInChildren<Renderer>();
            return r != null ? r.bounds.center : t.position;
        }

        public void Begin()
        {
            if (running) return;
            running = true;
            looking = false;
            StartCoroutine(Sequence());
        }

        void Say(string line, float seconds)
        {
            subtitle = line;
            subtitleUntil = Time.time + seconds;
            subtitleAt = Time.time;
        }

        void Play(AudioClip clip, float volume, float pitch = 1f)
        {
            if (clip == null) return;
            sfx.pitch = pitch;
            sfx.PlayOneShot(clip, volume * soundVolume);
        }

        void PlayFlip()
        {
            if (flips.Count > 0) Play(flips[Random.Range(0, flips.Count)], 0.9f, Random.Range(0.92f, 1.08f));
            else if (thumpFallback != null) Play(thumpFallback, 0.3f, Random.Range(1.55f, 1.8f));
        }

        IEnumerator Sequence()
        {
            FPCharacterMover mover = FindAnyObjectByType<FPCharacterMover>();
            if (mover != null) mover.enabled = false;
            foreach (MonoBehaviour m in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude))
            {
                if (m == null) continue;
                string n = m.GetType().Name;
                if (n == "VoiceDirector" || n == "PuzzleHints" || n == "AreaTitleManager" || n == "LookThought" || n == "UVBaton") m.enabled = false;
            }
            pi = PlayerInteraction.Instance != null ? PlayerInteraction.Instance : FindAnyObjectByType<PlayerInteraction>();
            if (pi != null) yield return pi.ClearHands();
            if (pi != null && pi.viewCamera != null) cam = pi.viewCamera;
            else if (Camera.main != null) cam = Camera.main.transform;
            if (cam == null) yield break;
            camComp = cam.GetComponent<Camera>();
            float fov0 = camComp != null ? camComp.fieldOfView : 60f;

            PrepareVideo();
            for (int i = 0; i < (pageTurns + 1) * 2; i++) pages.Add(PageTexture(i));
            BuildVignette();

            var lightGo = new GameObject("Journal reading light");
            lightGo.transform.SetParent(cam, false);
            lightGo.transform.localPosition = new Vector3(0.1f, 0.25f, 0.15f);
            Light lamp = lightGo.AddComponent<Light>();
            lamp.type = LightType.Point;
            lamp.color = new Color(1f, 0.78f, 0.52f);
            lamp.range = 1.6f;
            lamp.intensity = 0f;
            lamp.shadows = LightShadows.None;
            float lampLevel = 0f;
            System.Action lampTick = () => lamp.intensity = lampLevel * (0.93f + 0.07f * Mathf.PerlinNoise(Time.time * 6f, 0.4f));

            Quaternion camFrom = cam.rotation;
            Vector3 flat = Vector3.ProjectOnPlane(cam.forward, Vector3.up);
            if (flat.sqrMagnitude < 1e-4f) flat = Vector3.ProjectOnPlane(cam.up, Vector3.up);
            Quaternion camTo = Quaternion.LookRotation(flat.normalized, Vector3.up) * Quaternion.Euler(18f, 0f, 0f);
            framePos = cam.position;
            frameRot = camTo;

            oldBook = new Book("Old journal (held)", true, pages);
            ownBook = new Book("My journal (held)", false, pages);
            ownBook.root.SetActive(false);
            Vector3 startPos = oldBookWorld != null ? oldBookWorld.transform.position + Vector3.up * 0.02f : cam.position + cam.forward * 0.8f;
            Quaternion startRot = oldBookWorld != null ? oldBookWorld.transform.rotation : Quaternion.LookRotation(flat, Vector3.up);
            bool near = Vector3.Distance(startPos, cam.position) < 3.2f;
            if (!near) { startPos = cam.position + cam.forward * 0.6f - cam.up * 0.5f; startRot = cam.rotation; }
            oldBook.SetOpen(0f);
            oldBook.root.transform.SetPositionAndRotation(startPos, startRot);
            if (oldBookWorld != null) oldBookWorld.SetActive(false);

            // 1. Notice it: the eyes settle on the journal.
            Quaternion lookBook = Quaternion.LookRotation((startPos - cam.position).normalized, Vector3.up);
            lookBook = Quaternion.Slerp(camFrom, lookBook, 0.85f);
            yield return Animate(0.6f, t => cam.rotation = Quaternion.Slerp(camFrom, lookBook, Smooth(t)));

            // 2. Reach for it, a small pause on contact.
            yield return Animate(0.5f, t => { wr = EaseOut(t); Grips(); });
            Play(touchSound, 0.7f);
            yield return Hold(0.18f, t => Grips());

            // 3. Lift it with a little weight; dust drifts off.
            Play(liftSound, 0.8f);
            Puff(startPos + Vector3.up * 0.02f, 22, 0.08f);
            Vector3 p0 = oldBook.root.transform.position;
            Quaternion r0 = oldBook.root.transform.rotation;
            yield return Animate(1.25f, t =>
            {
                float lift = BackOut(Mathf.Clamp01(t * 1.05f), 0.9f);
                float turn = Smooth(Mathf.Clamp01(t * 1.25f - 0.12f));
                cam.rotation = Quaternion.Slerp(lookBook, Look(0f), Smooth(t));
                Vector3 hp = TargetPose(oldBook, 1f, out Quaternion hr);
                Vector3 arc = Vector3.up * Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI) * 0.07f;
                oldBook.root.transform.SetPositionAndRotation(Vector3.LerpUnclamped(p0, hp, lift) + arc, Quaternion.Slerp(r0, hr, turn));
                lampLevel = 1.1f * Smooth(t);
                lampTick();
                Grips();
            });
            oldHeld = true;

            // 4. Turn it over in the hand.
            Say(linePickup, 3.6f);
            yield return Hold(1.6f, t =>
            {
                float k = Mathf.Clamp01(t / 1.6f);
                float env = Mathf.Sin(k * Mathf.PI);
                oldInspect = new Vector3(Mathf.Sin(t * 2.2f) * 7f * env, Mathf.Sin(t * 1.5f + 0.5f) * 9f * env, Mathf.Sin(t * 1.1f) * 4f * env);
                cam.rotation = Look(0.35f * env);
                lampTick();
                PoseBooks();
            });
            oldInspect = Vector3.zero;

            // 5. Open the OLD journal: the stiff binding lifts, sticks, then falls open.
            Play(creakSound, 0.9f);
            yield return Animate(0.5f, t => { oldBook.SetOpen(0.14f * Smooth(t)); cam.rotation = Look(0.3f); lampTick(); PoseBooks(); });
            yield return Hold(0.32f, t => { oldBook.SetOpen(0.14f + Mathf.Sin(t * 40f) * 0.004f); lampTick(); PoseBooks(); });
            yield return Animate(0.5f, t => { oldBook.SetOpen(Mathf.Lerp(0.14f, 1.05f, t * t)); cam.rotation = Look(0.3f); lampTick(); PoseBooks(); });
            Play(thudSound, 0.85f);
            Puff(oldBook.root.transform.position + FrameUp() * 0.03f, 30, 0.12f);
            yield return Animate(0.25f, t => { oldBook.SetOpen(Mathf.Lerp(1.05f, 1f, Smooth(t))); lampTick(); PoseBooks(); });
            yield return Hold(0.7f, t => { cam.rotation = Look(0.3f); lampTick(); PoseBooks(); });

            // 6. Bring up my own journal and open it - this one is newer and opens easily.
            ownBook.root.SetActive(true);
            ownBook.SetOpen(0f);
            ownHeld = true;
            Play(leatherSound, 0.75f);
            yield return Animate(0.8f, t =>
            {
                float s = BackOut(t, 1.1f);
                ownSlide = new Vector3(-0.08f, -0.35f, 0f) * (1f - s);
                ownRoll = 25f * (1f - Smooth(t));
                wl = Smooth(t);
                cam.rotation = Look(Mathf.Lerp(0.3f, -0.45f, Smooth(t)));
                lampTick();
                PoseBooks();
            });
            ownSlide = Vector3.zero; ownRoll = 0f;
            Play(openNewSound, 0.75f);
            yield return Animate(0.6f, t => { ownBook.SetOpen(Smooth(t)); lampTick(); PoseBooks(); });

            Say(lineCompare, 3.8f);
            if (heartbeatSound != null) { heart.clip = heartbeatSound; heart.volume = 0.12f * soundVolume; heart.Play(); }
            yield return Hold(1.5f, t => { cam.rotation = Look(-0.45f * Mathf.Cos(t * 2.2f)); lampTick(); PoseBooks(); });

            // 7. Compare page by page: my page, then theirs, faster each time; the hands start to shake.
            float glance = -0.45f;
            for (int k = 0; k < pageTurns; k++)
            {
                int kk = k;
                float p = pageTurns > 1 ? k / (float)(pageTurns - 1) : 1f;
                bool last = k == pageTurns - 1;
                float pace = Mathf.Lerp(1f, 0.62f, p);
                float shakeFrom = tremble, shakeTo = Mathf.Lerp(0.15f, 0.6f, p) * trembleAmount;
                if (last) Say(lineLast, 3.6f);
                heart.volume = Mathf.Lerp(0.25f, 0.55f, p) * soundVolume;

                float g0 = glance;
                PlayFlip();
                yield return Animate(0.8f * pace, t =>
                {
                    cam.rotation = Look(Mathf.Lerp(g0, -1f, Smooth(Mathf.Clamp01(t * 2.5f))));
                    ownBook.Turn(kk, t);
                    tremble = Mathf.Lerp(shakeFrom, shakeTo, t * 0.5f);
                    lampTick(); PoseBooks();
                });
                ownBook.Turn(kk, 1f);
                PlayFlip();
                yield return Animate(0.8f * pace, t =>
                {
                    cam.rotation = Look(Mathf.Lerp(-1f, 1f, Smooth(Mathf.Clamp01(t * 2f))));
                    oldBook.Turn(kk, t);
                    tremble = Mathf.Lerp(shakeFrom, shakeTo, 0.5f + t * 0.5f);
                    lampTick(); PoseBooks();
                });
                oldBook.Turn(kk, 1f);
                glance = 1f;
                if (!last) yield return Hold(0.35f * pace, t => { lampTick(); PoseBooks(); });
            }

            // 8. Realisation: the eyes come back to centre, the view narrows, the edges darken, heartbeat.
            if (heartbeatSound != null && !heart.isPlaying) { heart.clip = heartbeatSound; heart.Play(); }
            float hv0 = heart.volume;
            float tr0 = tremble;
            yield return Animate(2.4f, t =>
            {
                float s = Smooth(t);
                cam.rotation = Look(Mathf.Lerp(1f, 0f, s) * 0.6f);
                if (camComp != null) camComp.fieldOfView = fov0 - realiseZoom * s;
                closer = 0.06f * s;
                tremble = Mathf.Lerp(tr0, 1f * trembleAmount, s);
                if (vignette != null) vignette.color = new Color(0f, 0f, 0f, 0.75f * s * (0.85f + 0.15f * Beat()));
                heart.volume = Mathf.Lerp(hv0, 0.9f * soundVolume, s);
                lampLevel = Mathf.Lerp(1.1f, 0.8f, s);
                lampTick(); PoseBooks();
            });

            BuildScreen();
            ZoneMusic.FadeOutAll(1.2f);
            yield return Animate(1.2f, t =>
            {
                black.alpha = Smooth(t);
                if (vignette != null) vignette.color = new Color(0f, 0f, 0f, 0.75f * (0.85f + 0.15f * Beat()));
                heart.volume = 0.9f * soundVolume * (1f - Smooth(t));
                lampTick(); PoseBooks();
            });
            black.alpha = 1f;
            heart.Stop();
            if (camComp != null) camComp.fieldOfView = fov0;

            float wait = 0f;
            while (vp != null && !vp.isPrepared && wait < 15f) { wait += Time.deltaTime; yield return null; }
            if (vp != null && vp.isPrepared)
            {
                screen.enabled = true;
                vp.SetDirectAudioVolume(0, videoVolume);
                vp.Play();
                CutsceneGate.Begin(this);
                float since = 0f;
                while (!videoDone)
                {
                    since += Time.deltaTime;
                    if (skipLabel != null) skipLabel.color = new Color(1f, 1f, 1f, allowVideoSkip ? 0.7f * Mathf.Clamp01((since - 1f) / 0.5f) : 0f);
                    if (since > 2f && !vp.isPlaying) videoDone = true;
                    yield return null;
                }
                if (skipLabel != null) skipLabel.enabled = false;
                yield return Animate(0.8f, t =>
                {
                    screen.color = new Color(1f, 1f, 1f, 1f - t);
                    vp.SetDirectAudioVolume(0, videoVolume * (1f - t));
                });
                vp.Stop();
                CutsceneGate.End(this);
            }

            if (pi != null) pi.HoldBooks(Vector3.zero, 0f, Vector3.zero, 0f);
            Destroy(oldBook.root);
            Destroy(ownBook.root);
            Destroy(lightGo);
            if (vignette != null) Destroy(vignette.canvas.gameObject);
            if (videoRT != null) videoRT.Release();
            yield return new WaitForSeconds(0.4f);
            LoopEnding loop = playLoopEndingAfter ? FindAnyObjectByType<LoopEnding>() : null;
            if (loop != null)
            {
                loop.PlayFromBlack();
                yield return null;
                yield return null;
                Destroy(black.gameObject);
            }
            else
            {
                SessionReset.ResetAll();
                SceneManager.LoadScene(menuScene);
            }
        }

        // ------------------------------------------------------------------ posing

        Vector3 FrameF() => frameRot * Vector3.forward;
        Vector3 FrameU() => frameRot * Vector3.up;
        Vector3 FrameR() => frameRot * Vector3.right;
        Vector3 FrameUp() => FrameU();

        // Eyes: g = -1 looks at my journal (left), +1 at the old one (right), 0 between. Breathing on top.
        Quaternion Look(float g)
        {
            float t = Time.time;
            float breathPitch = Mathf.Sin(t * 1.15f) * 0.35f;
            float drift = (Mathf.PerlinNoise(t * 0.35f, 2.7f) - 0.5f) * 0.8f;
            return frameRot * Quaternion.Euler(breathPitch + Mathf.Abs(g) * 1.5f, g * glanceAngle + drift, 0f);
        }

        Vector3 HoldPose(float side, out Quaternion rot)
        {
            Vector3 f = FrameF(), u = FrameU(), r = FrameR();
            float d = holdDistance - closer;
            Vector3 pos = framePos + f * d + r * (holdSide * side * (1f - closer * 2f)) - u * (holdDrop - closer * 0.6f);
            float a = holdTilt * Mathf.Deg2Rad;
            Vector3 top = u * Mathf.Cos(a) + f * Mathf.Sin(a);
            Vector3 normal = -f * Mathf.Cos(a) + u * Mathf.Sin(a);
            rot = Quaternion.AngleAxis(-9f * side, u) * Quaternion.LookRotation(top, normal);
            return pos;
        }

        // Full held pose for a book: hold position, closed books centred in the hand, breathing, tremble.
        Vector3 TargetPose(Book b, float side, out Quaternion rot)
        {
            Vector3 pos = HoldPose(side, out rot);
            float t = Time.time;
            pos += rot * Vector3.right * (-PageW * 0.5f * (1f - Mathf.Clamp01(b.openAmount)));
            pos += FrameU() * (Mathf.Sin(t * 1.15f + side * 0.3f) * 0.0035f) + FrameR() * (Mathf.Sin(t * 0.6f + side) * 0.0015f);
            if (tremble > 0f)
            {
                float n1 = Mathf.PerlinNoise(t * 9f, side * 3.1f) - 0.5f;
                float n2 = Mathf.PerlinNoise(t * 8.3f, side * 5.7f + 1f) - 0.5f;
                float n3 = Mathf.PerlinNoise(t * 11f, side * 1.3f + 4f) - 0.5f;
                rot = rot * Quaternion.Euler(n1 * 3f * tremble, n2 * 2f * tremble, n3 * 1.5f * tremble);
                pos += FrameU() * (n3 * 0.004f * tremble);
            }
            return pos;
        }

        void PoseBooks()
        {
            if (oldHeld)
            {
                Vector3 p = TargetPose(oldBook, 1f, out Quaternion r);
                oldBook.root.transform.SetPositionAndRotation(p, r * Quaternion.Euler(oldInspect));
            }
            if (ownHeld && ownBook.root.activeSelf)
            {
                Vector3 p = TargetPose(ownBook, -1f, out Quaternion r);
                p += FrameR() * ownSlide.x + FrameU() * ownSlide.y;
                ownBook.root.transform.SetPositionAndRotation(p, r * Quaternion.Euler(0f, 0f, ownRoll));
            }
            Grips();
        }

        void Grips()
        {
            if (pi == null) return;
            Vector3 left = ownBook != null && ownBook.root.activeSelf ? ownBook.Grip(-1f) : HoldPose(-1f, out _);
            pi.HoldBooks(left, wl, oldBook.Grip(1f), wr);
        }

        static float Beat()
        {
            // two quick pulses per ~0.95 s, like lub-dub
            float t = Time.time % 0.95f;
            return Mathf.Exp(-t * 18f) + 0.7f * Mathf.Exp(-Mathf.Max(0f, t - 0.22f) * 18f) * (t > 0.22f ? 1f : 0f);
        }

        // ------------------------------------------------------------------ dust and vignette

        void Puff(Vector3 at, int count, float radius)
        {
            if (!dust) return;
            if (dustMat == null)
            {
                Shader sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                if (sh == null) sh = Shader.Find("Particles/Standard Unlit");
                if (sh == null) { dust = false; return; }
                dustTex = SoftDot(32);
                dustMat = new Material(sh);
                dustMat.SetTexture("_BaseMap", dustTex);
                dustMat.mainTexture = dustTex;
                dustMat.SetFloat("_Surface", 1f);
                dustMat.SetFloat("_Blend", 0f);
                dustMat.SetOverrideTag("RenderType", "Transparent");
                dustMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                dustMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                dustMat.SetInt("_ZWrite", 0);
                dustMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                dustMat.renderQueue = 3000;
            }
            var go = new GameObject("Dust");
            go.transform.position = at;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 1f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.4f, 2.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.02f, 0.1f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.006f, 0.022f);
            main.startColor = new Color(0.85f, 0.78f, 0.66f, 0.55f);
            main.gravityModifier = -0.004f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 200;
            var em = ps.emission; em.enabled = false;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = radius;
            var col = ps.colorOverLifetime; col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                         new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0f, 1f) });
            col.color = grad;
            var noise = ps.noise; noise.enabled = true; noise.strength = 0.03f; noise.frequency = 1.5f;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = dustMat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ps.Play();
            ps.Emit(count);
            Destroy(go, 3.5f);
        }

        static Texture2D SoftDot(int size)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                    float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                    t.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
                }
            t.Apply();
            return t;
        }

        void BuildVignette()
        {
            var go = new GameObject("Journal vignette");
            go.transform.SetParent(transform, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 2400;
            const int S = 128;
            vignetteTex = new Texture2D(S, S, TextureFormat.RGBA32, false);
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float dx = (x + 0.5f) / S * 2f - 1f, dy = (y + 0.5f) / S * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx * 0.8f + dy * dy);
                    float a = Mathf.Clamp01((d - 0.45f) / 0.75f);
                    vignetteTex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a * (3f - 2f * a)));
                }
            vignetteTex.wrapMode = TextureWrapMode.Clamp;
            vignetteTex.Apply();
            vignette = new GameObject("Vignette", typeof(RectTransform)).AddComponent<RawImage>();
            vignette.transform.SetParent(go.transform, false);
            Stretch(vignette.rectTransform);
            vignette.texture = vignetteTex;
            vignette.color = new Color(0f, 0f, 0f, 0f);
            vignette.raycastTarget = false;
        }

        void PrepareVideo()
        {
            vp = gameObject.AddComponent<VideoPlayer>();
            vp.playOnAwake = false;
            vp.isLooping = false;
            vp.source = VideoSource.Url;
            vp.url = System.IO.Path.Combine(Application.streamingAssetsPath, videoFileName);
            vp.renderMode = VideoRenderMode.RenderTexture;
            vp.audioOutputMode = VideoAudioOutputMode.Direct;
            vp.loopPointReached += s => videoDone = true;
            vp.errorReceived += (s, msg) => { Debug.LogWarning("JournalCompareEnding: " + msg); videoDone = true; };
            vp.prepareCompleted += s =>
            {
                int w = (int)Mathf.Max(16, s.width), h = (int)Mathf.Max(16, s.height);
                videoRT = new RenderTexture(w, h, 0);
                vp.targetTexture = videoRT;
                if (screen != null)
                {
                    screen.texture = videoRT;
                    screen.GetComponent<AspectRatioFitter>().aspectRatio = (float)w / h;
                }
                vp.EnableAudioTrack(0, true);
            };
            vp.Prepare();
        }

        void BuildScreen()
        {
            var go = new GameObject("Journal Ending Screen");
            go.transform.SetParent(transform, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 2500;
            black = go.AddComponent<CanvasGroup>();
            black.alpha = 0f;
            black.blocksRaycasts = false;
            var bg = new GameObject("Black", typeof(RectTransform)).AddComponent<Image>();
            bg.transform.SetParent(go.transform, false);
            Stretch(bg.rectTransform);
            bg.color = Color.black;
            bg.raycastTarget = false;
            screen = new GameObject("Video", typeof(RectTransform)).AddComponent<RawImage>();
            screen.transform.SetParent(go.transform, false);
            Stretch(screen.rectTransform);
            screen.raycastTarget = false;
            screen.enabled = false;
            var fitter = screen.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = 16f / 10f;
            if (videoRT != null)
            {
                screen.texture = videoRT;
                fitter.aspectRatio = (float)videoRT.width / videoRT.height;
            }
            skipLabel = new GameObject("Skip", typeof(RectTransform)).AddComponent<Text>();
            skipLabel.transform.SetParent(go.transform, false);
            var lrt = skipLabel.rectTransform;
            lrt.anchorMin = new Vector2(0f, 0f); lrt.anchorMax = new Vector2(1f, 0f);
            lrt.pivot = new Vector2(0.5f, 0f);
            lrt.anchoredPosition = new Vector2(0f, 24f);
            lrt.sizeDelta = new Vector2(0f, 40f);
            skipLabel.font = Resources.Load<Font>("WarmStatues/DotGothic16");
            if (skipLabel.font == null) skipLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            skipLabel.fontSize = Mathf.Max(28, Screen.height / 26);
            skipLabel.alignment = TextAnchor.MiddleCenter;
            skipLabel.color = new Color(1f, 1f, 1f, 0f);
            skipLabel.text = "SPACE  SKIP";
            skipLabel.raycastTarget = false;
        }

        void OnGUI()
        {
            if (!running && looking) PromptBox.Draw(prompt, 0.66f);
            if (Time.time < subtitleUntil && (black == null || black.alpha < 0.5f)) SubtitleBox.Draw(subtitle, SubtitleBox.Fade(subtitleAt, subtitleUntil));
        }

        void OnDestroy()
        {
            foreach (Texture2D t in pages) if (t != null) Destroy(t);
            if (dustTex != null) Destroy(dustTex);
            if (vignetteTex != null) Destroy(vignetteTex);
            if (dustMat != null) Destroy(dustMat);
        }

        static Texture2D PageTexture(int index)
        {
            const int W = 224, H = 320;
            var px = new Color32[W * H];
            var rng = new System.Random(4271 + index * 97);
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float n = Mathf.PerlinNoise(x * 0.05f + index, y * 0.05f) * 0.06f + Mathf.PerlinNoise(x * 0.3f, y * 0.3f + index) * 0.03f;
                    float ex = Mathf.Min(x, W - 1 - x) / (float)W, ey = Mathf.Min(y, H - 1 - y) / (float)H;
                    float edge = Mathf.Clamp01(Mathf.Min(ex, ey) * 9f);
                    float v = 0.86f + n - (1f - edge) * 0.12f;
                    px[y * W + x] = new Color(v, v * 0.94f, v * 0.8f, 1f);
                }
            Color32 ink = new Color(0.32f, 0.2f, 0.11f, 1f);
            Color32 red = new Color(0.55f, 0.1f, 0.07f, 1f);
            int yTop = H - 30;
            Scribble(px, W, H, rng, 18, yTop, 110 + rng.Next(50), 2.2f, ink);
            if (index % 3 == 1) Line(px, W, H, 18, yTop - 10, 150, yTop - 10, 1.2f, red);
            int yy = yTop - 34;
            int kind = index % 4;
            if (kind == 0)
            {
                for (int l = 0; l < 4; l++) { Scribble(px, W, H, rng, 18, yy, 140 + rng.Next(50), 1.3f, ink); yy -= 18; }
                int cx = 70, cy = yy - 50;
                Line(px, W, H, cx - 34, cy - 26, cx + 34, cy - 26, 1.6f, ink);
                Line(px, W, H, cx - 34, cy - 26, cx, cy + 32, 1.6f, ink);
                Line(px, W, H, cx + 34, cy - 26, cx, cy + 32, 1.6f, ink);
                for (int r = 0; r < 4; r++) Line(px, W, H, cx + 22, cy, cx + 90, cy + 26 - r * 17, 1.2f, ink);
                yy = cy - 46;
            }
            else if (kind == 1)
            {
                for (int l = 0; l < 3; l++) { Scribble(px, W, H, rng, 18, yy, 120 + rng.Next(60), 1.3f, ink); yy -= 18; }
                int cy = yy - 30;
                Circle(px, W, H, 50, cy, 16, 1.6f, ink, false);
                Circle(px, W, H, 100, cy, 16, 1.6f, ink, false);
                Circle(px, W, H, 100, cy, 15, 0f, ink, true, 0);
                Circle(px, W, H, 150, cy, 16, 1.6f, ink, true);
                yy = cy - 34;
            }
            else if (kind == 2)
            {
                for (int l = 0; l < 2; l++) { Scribble(px, W, H, rng, 18, yy, 150, 1.3f, ink); yy -= 18; }
                yy -= 8;
                for (int row = 0; row < 5; row++)
                {
                    for (int g = 0; g < 4; g++) Tally(px, W, H, 20 + g * 48, yy, ink);
                    yy -= 30;
                }
            }
            else
            {
                for (int l = 0; l < 4; l++) { Scribble(px, W, H, rng, 18, yy, 150 + rng.Next(40), 1.3f, ink); yy -= 18; }
                int cx = 112, cy = yy - 44;
                Circle(px, W, H, cx, cy, 22, 1.8f, ink, false);
                for (int i = 0; i < 10; i++)
                {
                    float a = i * Mathf.PI * 0.2f;
                    Line(px, W, H, cx + (int)(Mathf.Cos(a) * 28), cy + (int)(Mathf.Sin(a) * 28), cx + (int)(Mathf.Cos(a) * 40), cy + (int)(Mathf.Sin(a) * 40), 1.4f, ink);
                }
                yy = cy - 54;
            }
            while (yy > 26) { Scribble(px, W, H, rng, 18, yy, 120 + rng.Next(70), 1.3f, ink); yy -= 18; }
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, true);
            tex.SetPixels32(px);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.anisoLevel = 4;
            tex.Apply(true, true);
            return tex;
        }

        static void Scribble(Color32[] px, int W, int H, System.Random rng, int x0, int y, int length, float width, Color32 c)
        {
            float x = x0;
            float phase = (float)rng.NextDouble() * 6f;
            float px0 = x, py0 = y;
            while (x < x0 + length)
            {
                x += 1.5f;
                if (rng.NextDouble() < 0.025) { x += 6f; px0 = x; py0 = y; continue; }
                float yy = y + Mathf.Sin(x * 0.55f + phase) * 3f + Mathf.Sin(x * 0.13f + phase) * 1.5f;
                Line(px, W, H, (int)px0, (int)py0, (int)x, (int)yy, width, c);
                px0 = x; py0 = yy;
            }
        }

        static void Tally(Color32[] px, int W, int H, int x, int y, Color32 c)
        {
            for (int i = 0; i < 4; i++) Line(px, W, H, x + i * 7, y - 10, x + i * 7 + 1, y + 10, 1.3f, c);
            Line(px, W, H, x - 4, y - 7, x + 26, y + 6, 1.3f, c);
        }

        static void Circle(Color32[] px, int W, int H, int cx, int cy, int r, float width, Color32 c, bool fill, int halfOnly = -1)
        {
            for (int y = cy - r - 3; y <= cy + r + 3; y++)
                for (int x = cx - r - 3; x <= cx + r + 3; x++)
                {
                    if (x < 0 || y < 0 || x >= W || y >= H) continue;
                    float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                    bool on = fill ? d <= r && (halfOnly < 0 || x >= cx) : Mathf.Abs(d - r) <= width * 0.5f + 0.3f;
                    if (on) px[y * W + x] = c;
                }
        }

        static void Line(Color32[] px, int W, int H, int x0, int y0, int x1, int y1, float width, Color32 c)
        {
            int steps = Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0)) + 1;
            int rad = Mathf.CeilToInt(width * 0.5f);
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                int x = Mathf.RoundToInt(Mathf.Lerp(x0, x1, t)), y = Mathf.RoundToInt(Mathf.Lerp(y0, y1, t));
                for (int dy = -rad; dy <= rad; dy++)
                    for (int dx = -rad; dx <= rad; dx++)
                    {
                        if (dx * dx + dy * dy > width * width * 0.25f + 0.3f) continue;
                        int xx = x + dx, yy = y + dy;
                        if (xx < 0 || yy < 0 || xx >= W || yy >= H) continue;
                        px[yy * W + xx] = c;
                    }
            }
        }

        static IEnumerator Animate(float duration, System.Action<float> step)
        {
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                step(t / duration);
                yield return null;
            }
            step(1f);
        }

        static IEnumerator Hold(float duration, System.Action<float> step)
        {
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                step(t);
                yield return null;
            }
        }

        static float EaseOut(float t)
        {
            t = Mathf.Clamp01(t);
            return 1f - (1f - t) * (1f - t);
        }

        // Ease-out with a small overshoot that settles back (the weight of the book in the hand).
        static float BackOut(float t, float overshoot)
        {
            t = Mathf.Clamp01(t);
            float c1 = overshoot, c3 = c1 + 1f, u = t - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }

        static float Smooth(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        static void Stretch(RectTransform r)
        {
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
        }

        static bool SkipPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && (Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.escapeKey.wasPressedThisFrame);
#else
            return Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Escape);
#endif
        }

        sealed class Book
        {
            public readonly GameObject root;
            readonly Transform leftHalf, rightHalf, turner, turnerOuter;
            readonly Material leftPage, rightPage, turnFrontIn, turnFrontOut, turnBackIn, turnBackOut;
            readonly List<Texture2D> pages;
            const float Block = 0.011f, CoverT = 0.005f, Open = 9f;
            int shownTurn = -1;
            public float openAmount;

            public Book(string name, bool old, List<Texture2D> pages)
            {
                this.pages = pages;
                root = new GameObject(name);
                Shader lit = Shader.Find("Universal Render Pipeline/Lit");
                Color leatherC = old ? new Color(0.2f, 0.12f, 0.07f) : new Color(0.36f, 0.2f, 0.1f);
                Color paperTint = old ? new Color(0.8f, 0.68f, 0.5f) : new Color(1f, 0.96f, 0.88f);
                Material leather = Mat(lit, leatherC, null, 0.25f, Color.black);
                Material edge = Mat(lit, paperTint * 0.85f, null, 0.1f, paperTint * 0.08f);
                leftPage = PageMat(lit, paperTint);
                rightPage = PageMat(lit, paperTint);
                turnFrontIn = PageMat(lit, paperTint);
                turnFrontOut = PageMat(lit, paperTint);
                turnBackIn = PageMat(lit, paperTint);
                turnBackOut = PageMat(lit, paperTint);
                rightHalf = Half("Right half", 1f, leather, edge, rightPage);
                leftHalf = Half("Left half", -1f, leather, edge, leftPage);
                var spine = Cube("Spine", root.transform, new Vector3(0f, -CoverT * 0.5f, 0f), new Vector3(0.012f, CoverT * 1.4f, PageH + 0.012f), leather);
                if (old)
                {
                    var tear = Cube("Torn corner", rightHalf, new Vector3(PageW - 0.004f, Block + 0.0015f, PageH * 0.5f - 0.006f), new Vector3(0.022f, 0.002f, 0.022f), Mat(lit, new Color(0.12f, 0.07f, 0.04f), null, 0.1f, Color.black));
                    tear.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
                }
                else
                {
                    var tear = Cube("Torn corner", rightHalf, new Vector3(PageW - 0.004f, Block + 0.0015f, PageH * 0.5f - 0.006f), new Vector3(0.022f, 0.002f, 0.022f), Mat(lit, new Color(0.2f, 0.12f, 0.06f), null, 0.1f, Color.black));
                    tear.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
                }
                turner = new GameObject("Turning page").transform;
                turner.SetParent(root.transform, false);
                turner.localPosition = new Vector3(0f, Block + 0.0012f, 0f);
                PagePair(turner, PageW * 0.25f, turnFrontIn, turnBackIn);
                turnerOuter = new GameObject("Outer").transform;
                turnerOuter.SetParent(turner, false);
                turnerOuter.localPosition = new Vector3(PageW * 0.5f, 0f, 0f);
                PagePair(turnerOuter, PageW * 0.25f, turnFrontOut, turnBackOut);
                turner.gameObject.SetActive(false);
                foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                ShowSpread(0);
            }

            Transform Half(string n, float side, Material leather, Material edge, Material page)
            {
                var t = new GameObject(n).transform;
                t.SetParent(root.transform, false);
                Cube("Cover", t, new Vector3(side * (PageW * 0.5f + 0.004f), -CoverT * 0.5f, 0f), new Vector3(PageW + 0.008f, CoverT, PageH + 0.012f), leather);
                Cube("Pages", t, new Vector3(side * PageW * 0.5f, Block * 0.5f, 0f), new Vector3(PageW, Block, PageH), edge);
                var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                q.name = "Page";
                Object.Destroy(q.GetComponent<Collider>());
                q.transform.SetParent(t, false);
                q.transform.localPosition = new Vector3(side * PageW * 0.5f, Block + 0.0006f, 0f);
                q.transform.localRotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
                q.transform.localScale = new Vector3(PageW * 0.97f, PageH * 0.97f, 1f);
                q.GetComponent<MeshRenderer>().sharedMaterial = page;
                return t;
            }

            static void PagePair(Transform parent, float centerX, Material front, Material back)
            {
                var f = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Object.Destroy(f.GetComponent<Collider>());
                f.transform.SetParent(parent, false);
                f.transform.localPosition = new Vector3(centerX, 0.0003f, 0f);
                f.transform.localRotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
                f.transform.localScale = new Vector3(PageW * 0.485f, PageH * 0.97f, 1f);
                f.GetComponent<MeshRenderer>().sharedMaterial = front;
                var b = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Object.Destroy(b.GetComponent<Collider>());
                b.transform.SetParent(parent, false);
                b.transform.localPosition = new Vector3(centerX, -0.0003f, 0f);
                b.transform.localRotation = Quaternion.LookRotation(Vector3.up, Vector3.forward) * Quaternion.Euler(0f, 0f, 0f);
                b.transform.localRotation = Quaternion.LookRotation(Vector3.up, Vector3.forward);
                b.transform.localScale = new Vector3(-PageW * 0.485f, PageH * 0.97f, 1f);
                b.GetComponent<MeshRenderer>().sharedMaterial = back;
            }

            static GameObject Cube(string n, Transform parent, Vector3 pos, Vector3 size, Material m)
            {
                var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
                g.name = n;
                Object.Destroy(g.GetComponent<Collider>());
                g.transform.SetParent(parent, false);
                g.transform.localPosition = pos;
                g.transform.localScale = size;
                g.GetComponent<MeshRenderer>().sharedMaterial = m;
                return g;
            }

            static Material Mat(Shader lit, Color c, Texture tex, float smooth, Color emission)
            {
                var m = new Material(lit);
                m.SetColor("_BaseColor", c);
                if (tex != null) m.SetTexture("_BaseMap", tex);
                m.SetFloat("_Smoothness", smooth);
                if (emission.maxColorComponent > 0f)
                {
                    m.EnableKeyword("_EMISSION");
                    m.SetColor("_EmissionColor", emission);
                }
                return m;
            }

            static Material PageMat(Shader lit, Color tint)
            {
                var m = Mat(lit, tint, null, 0.05f, tint * 0.22f);
                m.SetFloat("_Cull", 0f);
                return m;
            }

            static void SetTex(Material m, Texture2D t, float scaleX, float offsetX)
            {
                m.SetTexture("_BaseMap", t);
                m.SetTexture("_EmissionMap", t);
                m.SetTextureScale("_BaseMap", new Vector2(scaleX, 1f));
                m.SetTextureOffset("_BaseMap", new Vector2(offsetX, 0f));
            }

            Texture2D PageAt(int i) { return pages[Mathf.Clamp(i, 0, pages.Count - 1)]; }

            void ShowSpread(int s)
            {
                SetTex(leftPage, PageAt(s * 2), 1f, 0f);
                SetTex(rightPage, PageAt(s * 2 + 1), 1f, 0f);
            }

            // t: 0 closed, 1 open. Values a little above 1 let the cover overshoot as it falls open.
            public void SetOpen(float t)
            {
                openAmount = t;
                float a = Mathf.LerpUnclamped(180f - Open, Open, t);
                rightHalf.localPosition = Vector3.zero;
                rightHalf.localRotation = Quaternion.Euler(0f, 0f, Open);
                leftHalf.localRotation = Quaternion.Euler(0f, 0f, -a);
                leftHalf.localPosition = new Vector3(0f, (Block * 2f + CoverT) * (1f - Mathf.Clamp01(t)), 0f);
            }

            public void Turn(int k, float t)
            {
                if (t <= 0f) return;
                if (shownTurn != k)
                {
                    shownTurn = k;
                    SetTex(turnFrontIn, PageAt(k * 2 + 1), 0.5f, 0f);
                    SetTex(turnFrontOut, PageAt(k * 2 + 1), 0.5f, 0.5f);
                    SetTex(turnBackIn, PageAt(k * 2 + 2), 0.5f, 0.5f);
                    SetTex(turnBackOut, PageAt(k * 2 + 2), 0.5f, 0f);
                    SetTex(rightPage, PageAt(k * 2 + 3), 1f, 0f);
                    turner.gameObject.SetActive(true);
                }
                float s = Smooth(t);
                turner.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(Open, 180f - Open, s));
                turnerOuter.localRotation = Quaternion.Euler(0f, 0f, -Mathf.Sin(s * Mathf.PI) * 28f);
                if (t >= 1f)
                {
                    turner.gameObject.SetActive(false);
                    ShowSpread(k + 1);
                }
            }

            // Where the hand holds the book. The right hand holds the right edge (that half never moves).
            // The left hand holds the spine while the book is shut and slides to the left edge as it opens,
            // so it no longer swings across the book with the cover.
            public Vector3 Grip(float side)
            {
                if (side > 0f) return rightHalf.TransformPoint(new Vector3(PageW + 0.03f, -0.03f, -PageH * 0.22f));
                Vector3 spine = root.transform.TransformPoint(new Vector3(-0.02f, -0.03f, -PageH * 0.22f));
                Vector3 edge = leftHalf.TransformPoint(new Vector3(-(PageW + 0.03f), -0.03f, -PageH * 0.22f));
                return Vector3.Lerp(spine, edge, Smooth(Mathf.Clamp01((openAmount - 0.35f) / 0.65f)));
            }
        }
    }
}
