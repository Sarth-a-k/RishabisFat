using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace FPCharacter
{
    public sealed class NotePickup : MonoBehaviour
    {
        public string firstLine = "A note....";
        public string afterLine = "";
        public string prompt = "[E] Pick up the note";
        public Transform blocks;
        public float noticeDistance = 3.2f;
        public float useDistance = 2.6f;
        public float aimAngle = 14f;
        public float holdDistance = 0.4f;
        public float holdDrop = 0.07f;
        public float holdTilt = 12f;
        public static float textTopYaw = 180f;

        static readonly List<NotePickup> all = new List<NotePickup>();
        static readonly HashSet<string> readThisSession = new HashSet<string>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { all.Clear(); readThisSession.Clear(); }

        public static bool Blocks(Transform t)
        {
            if (t == null) return false;
            foreach (NotePickup n in all)
                if (n != null && !n.done && n.blocks != null && (t == n.blocks || t.IsChildOf(n.blocks) || n.blocks.IsChildOf(t))) return true;
            return false;
        }

        public static bool Busy { get; private set; }

        string id;
        bool noticed, looking, running, done;
        string subtitle;
        float subtitleAt, subtitleUntil;
        string bottomPrompt;
        AudioSource sfx;
        Collider col;

        void OnEnable() { if (!all.Contains(this)) all.Add(this); }
        void OnDisable() { all.Remove(this); }

        void Start()
        {
            id = gameObject.scene.name + name;
            col = GetComponent<Collider>();
            sfx = gameObject.AddComponent<AudioSource>();
            sfx.playOnAwake = false;
            sfx.spatialBlend = 0f;
            if (readThisSession.Contains(id)) { done = true; if (col != null) col.enabled = false; }
        }

        void Update()
        {
            looking = false;
            if (done || running) return;
            Camera c = Camera.main;
            if (c == null) return;
            Transform cam = c.transform;
            Vector3 to = transform.position - cam.position;
            float dist = to.magnitude;
            float angle = Vector3.Angle(cam.forward, to);
            bool visible = Clear(cam.position, transform.position);
            if (!noticed && visible && ((dist < noticeDistance && angle < 30f) || dist < 1.8f))
            {
                noticed = true;
                Say(firstLine, 2.6f);
            }
            looking = visible && dist < useDistance && angle < aimAngle;
            if (looking && PrismPickup.InteractPressed()) StartCoroutine(Read(cam));
        }

        bool Clear(Vector3 from, Vector3 target)
        {
            Vector3 d = target - from;
            RaycastHit[] hits = Physics.RaycastAll(from, d.normalized, d.magnitude - 0.03f, ~0, QueryTriggerInteraction.Ignore);
            FPCharacterMover mover = FindAnyObjectByType<FPCharacterMover>();
            foreach (RaycastHit h in hits)
            {
                Transform t = h.collider.transform;
                if (t.IsChildOf(transform)) continue;
                if (blocks != null && (t.IsChildOf(blocks) || t == blocks)) continue;
                if (mover != null && t.IsChildOf(mover.transform)) continue;
                return false;
            }
            return true;
        }

        void Say(string line, float seconds)
        {
            if (string.IsNullOrEmpty(line)) return;
            subtitle = line;
            subtitleAt = Time.time;
            subtitleUntil = Time.time + seconds;
        }

        void Paper(float pitch, float volume)
        {
            AudioClip clip = Resources.Load<AudioClip>("Journal/page_flip_" + Random.Range(1, 4));
            if (clip == null) clip = Resources.Load<AudioClip>("WarmStatues/thump");
            if (clip == null) return;
            sfx.pitch = pitch;
            sfx.PlayOneShot(clip, volume);
        }

        Quaternion FacingCamera(Transform cam)
        {
            Quaternion face = Quaternion.AngleAxis(-holdTilt, cam.right) * Quaternion.LookRotation(cam.up, -cam.forward);
            return face * Quaternion.Euler(0f, textTopYaw, 0f);
        }

        Vector3 HoldPoint(Transform cam)
        {
            return cam.position + cam.forward * holdDistance - cam.up * holdDrop;
        }

        Vector3 Corner(float side)
        {
            Camera c = Camera.main;
            Transform cam = c != null ? c.transform : transform;
            Vector3 centre = transform.position;
            Vector3 right = cam.right, up = cam.up;
            return centre + right * (side * 0.095f) - up * 0.17f - cam.forward * 0.03f;
        }

        IEnumerator Read(Transform cam)
        {
            running = true;
            Busy = true;
            FPCharacterMover mover = FindAnyObjectByType<FPCharacterMover>();
            if (mover != null) mover.enabled = false;
            PlayerInteraction pi = PlayerInteraction.Instance != null ? PlayerInteraction.Instance : FindAnyObjectByType<PlayerInteraction>();
            if (pi != null) yield return pi.ClearHands();
            if (col != null) col.enabled = false;

            var lampGo = new GameObject("Note reading light");
            lampGo.transform.SetParent(cam, false);
            lampGo.transform.localPosition = new Vector3(0.05f, 0.2f, 0.05f);
            Light lamp = lampGo.AddComponent<Light>();
            lamp.type = LightType.Point;
            lamp.color = new Color(1f, 0.85f, 0.65f);
            lamp.range = 1.2f;
            lamp.intensity = 0f;
            lamp.shadows = LightShadows.None;

            Vector3 p0 = transform.position;
            Quaternion r0 = transform.rotation;
            Transform oldParent = transform.parent;
            float wl = 0f, wr = 0f;
            System.Action grip = () => { if (pi != null) pi.HoldBooks(Corner(-1f), wl, Corner(1f), wr); };

            yield return Animate(0.45f, t => { wr = Smooth(t) * 0.8f; grip(); });
            Paper(1.3f, 0.5f);
            yield return Animate(0.8f, t =>
            {
                float s = Smooth(t);
                transform.position = Vector3.Lerp(p0, HoldPoint(cam), s) + Vector3.up * Mathf.Sin(s * Mathf.PI) * 0.12f;
                transform.rotation = Quaternion.Slerp(r0, FacingCamera(cam), s);
                wr = Mathf.Lerp(0.8f, 1f, s);
                wl = s;
                lamp.intensity = 1.1f * s;
                grip();
            });

            float held = 0f;
            while (true)
            {
                held += Time.deltaTime;
                float sway = Mathf.Sin(held * 1.4f);
                transform.SetPositionAndRotation(HoldPoint(cam) + cam.up * sway * 0.003f, FacingCamera(cam) * Quaternion.Euler(sway * 1.2f, 0f, Mathf.Sin(held * 0.9f) * 1.5f));
                grip();
                bottomPrompt = held > 1.2f ? "[E] Done reading" : null;
                if (held > 1.2f && DonePressed()) break;
                yield return null;
            }
            bottomPrompt = null;

            Say(afterLine, 3.8f);
            yield return Hold(2.2f, t =>
            {
                float sway = Mathf.Sin((held + t) * 1.4f);
                transform.SetPositionAndRotation(HoldPoint(cam) + cam.up * sway * 0.003f + cam.right * Mathf.Sin(t * 1.3f) * 0.006f, FacingCamera(cam) * Quaternion.Euler(0f, 0f, Mathf.Sin(t * 1.1f) * 3f));
                grip();
            });

            Vector3 hp = transform.position;
            Quaternion hr = transform.rotation;
            yield return Animate(0.28f, t =>
            {
                float s = Smooth(t);
                transform.position = hp - cam.right * 0.06f * s + cam.up * 0.02f * s;
                transform.rotation = hr * Quaternion.Euler(0f, 0f, 14f * s);
                wl = 1f - s;
                grip();
            });
            Paper(0.85f, 0.6f);
            Vector3 wp = transform.position;
            Quaternion wq = transform.rotation;
            Vector3 flingTo = cam.position + cam.forward * 0.5f + cam.right * 0.75f - cam.up * 0.35f;
            yield return Animate(0.4f, t =>
            {
                float s = t * t;
                transform.position = Vector3.Lerp(wp, flingTo, s) + cam.up * Mathf.Sin(t * Mathf.PI) * 0.08f;
                transform.rotation = wq * Quaternion.Euler(t * 160f, t * 90f, -t * 220f);
                wr = 1f - Smooth(t);
                grip();
            });
            if (pi != null) pi.HoldBooks(Vector3.zero, 0f, Vector3.zero, 0f);
            Destroy(lampGo);

            Vector3 flat = Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized;
            Vector3 right = Vector3.ProjectOnPlane(cam.right, Vector3.up).normalized;
            Vector3 feet = mover != null ? mover.transform.position : cam.position - Vector3.up * 1.6f;
            Vector3 land = feet + right * 1.1f + flat * 0.9f;
            float floorY = feet.y;
            if (Physics.Raycast(new Vector3(land.x, feet.y + 1.2f, land.z), Vector3.down, out RaycastHit fh, 3f, ~0, QueryTriggerInteraction.Ignore)) floorY = fh.point.y;
            land.y = floorY + 0.004f;
            Vector3 fallFrom = transform.position;
            Quaternion fq = transform.rotation;
            Quaternion restRot = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Quaternion.Euler(Random.Range(-4f, 4f), 0f, Random.Range(-4f, 4f));
            if (mover != null) mover.enabled = true;
            Busy = false;
            transform.SetParent(oldParent, true);
            yield return Animate(1.6f, t =>
            {
                float s = Smooth(t);
                Vector3 p = Vector3.Lerp(fallFrom, land, s);
                p += right * Mathf.Sin(t * Mathf.PI * 3f) * 0.18f * (1f - t);
                transform.position = p;
                transform.rotation = Quaternion.Slerp(fq, restRot, s) * Quaternion.Euler(Mathf.Sin(t * 9f) * 25f * (1f - t), 0f, Mathf.Cos(t * 7f) * 30f * (1f - t));
            });
            transform.SetPositionAndRotation(land, restRot);
            readThisSession.Add(id);
            done = true;
            running = false;
        }

        void OnGUI()
        {
            if (looking && !running && !done) PromptBox.Draw(prompt, 0.66f);
            if (!string.IsNullOrEmpty(bottomPrompt)) PromptBox.Draw(bottomPrompt, 0.92f);
            if (Time.time < subtitleUntil) SubtitleBox.Draw(subtitle, SubtitleBox.Fade(subtitleAt, subtitleUntil));
        }

        static bool DonePressed()
        {
#if ENABLE_INPUT_SYSTEM
            return (Keyboard.current != null && (Keyboard.current.eKey.wasPressedThisFrame || Keyboard.current.spaceKey.wasPressedThisFrame)) || (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame);
#else
            return Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0);
#endif
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

        static float Smooth(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }
    }

    public static class NoteSetup
    {
        public const string NoteA = "Notes/NoteA_ShadowsOfThePast/NoteA_ShadowsOfThePast";
        public const string NoteB = "Notes/NoteB_Silhouettes/NoteB_Silhouettes";
        static readonly Vector2 EmberX = new Vector2(85f, 140f);
        static readonly Vector2 VoidX = new Vector2(150f, 200f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Hook()
        {
            SceneManager.sceneLoaded -= OnLoaded;
            SceneManager.sceneLoaded += OnLoaded;
            SpawnAll();
        }

        static void OnLoaded(Scene s, LoadSceneMode m) { SpawnAll(); }

        public static List<GameObject> SpawnAll()
        {
            var made = new List<GameObject>();
            if (Object.FindAnyObjectByType<FPCharacterMover>(FindObjectsInactive.Include) == null) return made;
            if (Object.FindAnyObjectByType<NotePickup>(FindObjectsInactive.Include) != null) return made;
            foreach (PlayerSeat s in Object.FindObjectsByType<PlayerSeat>(FindObjectsInactive.Include))
            {
                if (s.transform.position.x < EmberX.x || s.transform.position.x > EmberX.y) continue;
                GameObject n = OnSeat(s);
                if (n != null) made.Add(n);
                break;
            }
            foreach (RedButton b in Object.FindObjectsByType<RedButton>(FindObjectsInactive.Include))
            {
                if (b.transform.position.x < VoidX.x || b.transform.position.x > VoidX.y) continue;
                GameObject n = OnButton(b);
                if (n != null) made.Add(n);
                break;
            }
            return made;
        }

        static GameObject OnSeat(PlayerSeat seat)
        {
            float top = seat.transform.position.y + 0.46f;
            foreach (Collider c in seat.GetComponentsInChildren<Collider>(true))
                if (c.name == "Stool") top = c.bounds.max.y;
            Vector3 pos = new Vector3(seat.transform.position.x, top + 0.003f, seat.transform.position.z);
            Vector3 toCenter = seat.tableCenter != null ? Vector3.ProjectOnPlane(seat.tableCenter.position - pos, Vector3.up).normalized : seat.transform.forward;
            Quaternion rot = Quaternion.LookRotation(toCenter, Vector3.up) * Quaternion.Euler(0f, NotePickup.textTopYaw + 8f, 0f);
            GameObject n = Make(NoteA, "Note A (seat)", pos, rot);
            if (n == null) return null;
            NotePickup p = n.GetComponent<NotePickup>();
            p.blocks = seat.transform;
            p.firstLine = "A note....";
            p.afterLine = "I think I just wrote something like that.... hmm, maybe a coincidence";
            return n;
        }

        static GameObject OnButton(RedButton button)
        {
            Transform root = button.transform;
            float top = root.position.y + 1.082f;
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == "Pedestal top") { Renderer r = t.GetComponent<Renderer>(); if (r != null) top = r.bounds.max.y; }
            Vector3 toward = Vector3.left;
            Vector3 pos = root.position + toward * 0.25f;
            pos.y = top + 0.003f;
            Quaternion rot = Quaternion.LookRotation(-toward, Vector3.up) * Quaternion.Euler(0f, NotePickup.textTopYaw - 12f, 0f);
            GameObject n = Make(NoteB, "Note B (button)", pos, rot);
            if (n == null) return null;
            NotePickup p = n.GetComponent<NotePickup>();
            p.blocks = root;
            p.firstLine = "A note again..";
            p.afterLine = "The coincidence is getting oddly specific...";
            return n;
        }

        static GameObject Make(string path, string name, Vector3 pos, Quaternion rot)
        {
            GameObject model = Resources.Load<GameObject>(path);
            if (model == null) { Debug.LogWarning("NoteSetup: missing Resources/" + path); return null; }
            GameObject n = Object.Instantiate(model);
            n.name = name;
            n.transform.SetPositionAndRotation(pos, rot);
            string dir = path.Substring(0, path.LastIndexOf('/'));
            string baseName = path.Substring(path.LastIndexOf('/') + 1);
            Material front = Mat(dir + "/Textures/" + baseName + "_Front");
            Material back = Mat(dir + "/Textures/" + baseName + "_Back");
            foreach (Renderer r in n.GetComponentsInChildren<Renderer>(true))
            {
                Material[] mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    string mn = mats[i] != null ? mats[i].name : "";
                    mats[i] = mn.Contains("Back") || (mats.Length > 1 && i == 1 && !mn.Contains("Front")) ? back : front;
                }
                r.sharedMaterials = mats;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            Bounds b = new Bounds(pos, Vector3.zero);
            bool any = false;
            foreach (Renderer r in n.GetComponentsInChildren<Renderer>(true)) { if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds); }
            if (any) n.transform.position += Vector3.up * (pos.y - b.min.y);
            var box = n.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.01f, 0f);
            box.size = new Vector3(0.17f, 0.03f, 0.22f);
            n.AddComponent<NotePickup>();
            return n;
        }

        static Material Mat(string texBase)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            Texture2D albedo = Resources.Load<Texture2D>(texBase + "_Albedo");
            Texture2D normal = Resources.Load<Texture2D>(texBase + "_Normal");
            if (albedo != null) { m.SetTexture("_BaseMap", albedo); m.SetTexture("_EmissionMap", albedo); }
            if (normal != null) { m.SetTexture("_BumpMap", normal); m.EnableKeyword("_NORMALMAP"); }
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Smoothness", 0.05f);
            m.SetFloat("_Metallic", 0f);
            m.SetFloat("_AlphaClip", 1f);
            m.SetFloat("_Cutoff", 0.5f);
            m.EnableKeyword("_ALPHATEST_ON");
            m.SetFloat("_Cull", 0f);
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", new Color(0.16f, 0.14f, 0.11f));
            return m;
        }
    }
}
