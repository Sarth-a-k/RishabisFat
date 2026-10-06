using System.Collections;
using UnityEngine;

namespace FPCharacter
{
    public sealed class PrismSocket : MonoBehaviour
    {
        public GameObject prismObject;
        public Transform tableCenter;
        public float useDistance = 3.6f;
        public float centerRadius = 1.0f;
        public bool startPlaced;
        public Light placeFlash;

        public bool Placed { get; private set; }

        bool looking;
        public static bool AnyLooking { get; private set; }
        static int lookingFrame = -10;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { AnyLooking = false; lookingFrame = -10; }

        void LateUpdate()
        {
            if (looking) lookingFrame = Time.frameCount;
            AnyLooking = Time.frameCount - lookingFrame <= 1;
        }

        void Awake()
        {
            Placed = startPlaced;
            if (prismObject != null && !startPlaced) prismObject.SetActive(false);
        }

        void Update()
        {
            looking = false;
            if (Placed || PrismPickup.Carried == null) return;
            Camera cam = Camera.main;
            if (cam == null) return;
            if (Physics.Raycast(new Ray(cam.transform.position, cam.transform.forward), out RaycastHit hit, useDistance, ~0, QueryTriggerInteraction.Ignore))
            {
                Vector3 c = tableCenter != null ? tableCenter.position : transform.position;
                Vector3 d = hit.point - c; d.y = 0f;
                bool onTable = hit.collider.transform.IsChildOf(transform) && hit.collider.GetComponentInParent<RotatingRing>() == null;
                looking = d.magnitude <= centerRadius || (onTable && d.magnitude <= centerRadius + 0.6f);
            }
            if (looking && PrismPickup.InteractPressed()) StartCoroutine(Place());
        }

        IEnumerator Place()
        {
            Placed = true;
            looking = false;
            PrismPickup p = PrismPickup.Carried;
            if (p != null) p.Consume();
            if (prismObject == null) yield break;
            Vector3 full = prismObject.transform.localScale;
            prismObject.transform.localScale = full * 0.01f;
            prismObject.SetActive(true);
            if (placeFlash != null) placeFlash.enabled = true;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / 0.7f;
                float k = Mathf.SmoothStep(0f, 1f, t);
                prismObject.transform.localScale = full * Mathf.Max(0.01f, k);
                if (placeFlash != null) placeFlash.intensity = Mathf.Lerp(6f, 0f, t);
                yield return null;
            }
            prismObject.transform.localScale = full;
            if (placeFlash != null) placeFlash.enabled = false;
        }

        void OnGUI()
        {
            if (looking) PromptBox.Draw("[E] Place the prism on the table", 0.66f);
        }
    }
}
