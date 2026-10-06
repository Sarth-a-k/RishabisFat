using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace FPCharacter
{
    public sealed class PrismPickup : MonoBehaviour
    {
        public float useDistance = 2.6f;
        public Vector3 holdOffset = new Vector3(0.28f, -0.3f, 0.65f);
        public float holdScale = 0.45f;
        public float bobSpeed = 1.6f;
        public float bobHeight = 0.06f;
        public float spinSpeed = 30f;

        public static PrismPickup Carried { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Carried = null; }

        bool looking;
        Vector3 restPos;
        Vector3 restScale;
        Collider[] colliders;
        Light glow;

        void Start()
        {
            restPos = transform.position;
            restScale = transform.localScale;
            colliders = GetComponentsInChildren<Collider>(true);
            glow = GetComponentInChildren<Light>(true);
        }

        void Update()
        {
            if (Carried == this)
            {
                bool show = !NotePickup.Busy;
                foreach (Renderer r in GetComponentsInChildren<Renderer>()) r.enabled = show;
                return;
            }
            transform.position = restPos + Vector3.up * Mathf.Sin(Time.time * bobSpeed) * bobHeight;
            transform.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.World);
            looking = false;
            Camera cam = Camera.main;
            if (cam == null || Carried != null) return;
            if (Physics.SphereCast(new Ray(cam.transform.position, cam.transform.forward), 0.12f, out RaycastHit hit, useDistance + 0.3f, ~0, QueryTriggerInteraction.Ignore))
                looking = hit.collider.transform.IsChildOf(transform);
            if (looking && InteractPressed()) PickUp(cam);
        }

        public void Equip()
        {
            Camera cam = Camera.main;
            if (cam != null && Carried == null) PickUp(cam);
        }

        void PickUp(Camera cam)
        {
            Carried = this;
            foreach (Collider c in colliders) if (c != null) c.enabled = false;
            if (glow != null) glow.enabled = false;
            transform.SetParent(cam.transform, false);
            transform.localPosition = holdOffset;
            transform.localRotation = Quaternion.Euler(10f, -20f, 0f);
            transform.localScale = restScale * holdScale;
            foreach (Renderer r in GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        public void Consume()
        {
            if (Carried == this) Carried = null;
            Destroy(gameObject);
        }

        void OnDestroy()
        {
            if (Carried == this) Carried = null;
        }

        public static bool InteractPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.E);
#endif
        }

        void OnGUI()
        {
            if (Carried == this && NotePickup.Busy) return;
            if (Carried == this) { PromptBox.Draw("Carrying the prism  -  place it on the round table", 0.9f); return; }
            if (looking) PromptBox.Draw("[E] Take the prism", 0.66f);
        }
    }
}
