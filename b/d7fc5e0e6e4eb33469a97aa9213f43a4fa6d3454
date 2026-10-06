using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace FPCharacter
{
    public sealed class RedButton : MonoBehaviour
    {
        public Transform cap;
        public Light glow;
        public AudioClip clickSound;
        public float useDistance = 2.6f;
        public float travel = 0.025f;
        public float reachTime = 0.35f;
        public string prompt = "[E] Press the button";
        public UnityEvent onPressed;

        public bool Pressed { get; private set; }
        public int PressCount { get; private set; }

        bool looking, busy;
        Vector3 capRest;
        float glowBase;
        AudioSource audioSource;

        void Start()
        {
            if (cap != null) capRest = cap.localPosition;
            if (glow != null) glowBase = glow.intensity;
            if (clickSound == null) clickSound = Resources.Load<AudioClip>("Sfx/button_click");
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0.8f;
            audioSource.minDistance = 2f;
            audioSource.maxDistance = 20f;
        }

        void Update()
        {
            looking = false;
            if (NotePickup.Blocks(transform)) return;
            Camera cam = Camera.main;
            if (cam == null || busy) return;
            if (Physics.SphereCast(new Ray(cam.transform.position, cam.transform.forward), 0.12f, out RaycastHit hit, useDistance + 0.3f, ~0, QueryTriggerInteraction.Ignore))
                looking = hit.collider.transform.IsChildOf(transform);
            if (looking && PrismPickup.InteractPressed()) StartCoroutine(Press());
        }

        IEnumerator Press()
        {
            busy = true;
            looking = false;
            float wait = 0f;
            if (PlayerInteraction.Instance != null && cap != null) wait = PlayerInteraction.Instance.ReachToPress(cap.position + Vector3.up * 0.03f, reachTime, 0.18f);
            if (wait > 0f) yield return new WaitForSeconds(wait * 0.92f);
            if (clickSound != null) audioSource.PlayOneShot(clickSound);
            for (float t = 0f; t < 1f; t += Time.deltaTime / 0.08f)
            {
                if (cap != null) cap.localPosition = capRest - Vector3.up * travel * t;
                if (glow != null) glow.intensity = Mathf.Lerp(glowBase, glowBase * 4f, t);
                yield return null;
            }
            Pressed = true;
            PressCount++;
            onPressed?.Invoke();
            yield return new WaitForSeconds(0.15f);
            for (float t = 0f; t < 1f; t += Time.deltaTime / 0.25f)
            {
                if (cap != null) cap.localPosition = capRest - Vector3.up * travel * (1f - t);
                if (glow != null) glow.intensity = Mathf.Lerp(glowBase * 4f, glowBase, t);
                yield return null;
            }
            if (cap != null) cap.localPosition = capRest;
            busy = false;
        }

        void OnGUI()
        {
            if (looking) PromptBox.Draw(prompt, 0.66f);
        }
    }
}
