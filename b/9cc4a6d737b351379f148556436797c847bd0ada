using UnityEngine;

namespace FPCharacter
{
    public sealed class QuestMarker : MonoBehaviour
    {
        public NPCCutscene npc;
        public Transform marker;
        public float bobHeight = 0.08f;
        public float bobSpeed = 2.2f;
        public float pulse = 0.06f;
        public float hideSeconds = 0.5f;
        public float aboveHead = 0.45f;

        Vector3 basePos;
        Vector3 baseScale;
        Renderer markerRenderer;
        MaterialPropertyBlock block;
        float hide;
        bool infrared;
        Transform cam;
        Transform head;
        int frames;

        void Start()
        {
            if (marker == null) { enabled = false; return; }
            basePos = marker.localPosition;
            baseScale = marker.localScale;
            markerRenderer = marker.GetComponent<Renderer>();
            block = new MaterialPropertyBlock();
            InfraredVision.Changed += OnInfrared;
            infrared = InfraredVision.Active;
            Animator anim = GetComponentInChildren<Animator>();
            if (anim != null && anim.isHuman) head = anim.GetBoneTransform(HumanBodyBones.Head);
        }

        void OnDestroy()
        {
            InfraredVision.Changed -= OnInfrared;
        }

        void OnInfrared(bool on) { infrared = on; }

        void LateUpdate()
        {
            if (marker == null) return;
            if (cam == null && Camera.main != null) cam = Camera.main.transform;

            bool talked = npc != null && (npc.HasPlayed || npc.IsPlaying);
            if (talked) hide = Mathf.MoveTowards(hide, 1f, Time.deltaTime / Mathf.Max(0.05f, hideSeconds));
            if (hide >= 1f)
            {
                marker.gameObject.SetActive(false);
                enabled = false;
                return;
            }

            if (head != null && frames < 3)
            {
                frames++;
                if (frames == 3)
                {
                    Vector3 w = new Vector3(head.position.x, head.position.y + aboveHead, head.position.z);
                    basePos = marker.parent != null ? marker.parent.InverseTransformPoint(w) : w;
                }
            }

            float t = Time.time;
            marker.localPosition = basePos + Vector3.up * (Mathf.Sin(t * bobSpeed) * bobHeight + hide * 0.3f);
            float s = (1f + Mathf.Sin(t * bobSpeed * 2f) * pulse) * (1f - hide);
            marker.localScale = baseScale * s;
            if (cam != null)
            {
                Vector3 d = marker.position - cam.position;
                d.y = 0f;
                if (d.sqrMagnitude > 1e-4f) marker.rotation = Quaternion.LookRotation(d, Vector3.up);
            }
            if (markerRenderer != null)
            {
                markerRenderer.enabled = !infrared;
                if (block == null) block = new MaterialPropertyBlock();
                markerRenderer.GetPropertyBlock(block);
                block.SetColor("_BaseColor", new Color(1f, 1f, 1f, 1f - hide));
                markerRenderer.SetPropertyBlock(block);
            }
        }
    }
}
