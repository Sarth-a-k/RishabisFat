using UnityEngine;

namespace FPCharacter
{
    public sealed class GhostPresence : MonoBehaviour
    {
        public float hoverHeight = 0.04f;
        public float bobHeight = 0.05f;
        public float bobPeriod = 4f;
        public float swayDegrees = 3f;
        public float swayPeriod = 7f;

        Vector3 basePosition;
        Quaternion baseRotation;
        float phase;

        void Start()
        {
            basePosition = transform.localPosition;
            baseRotation = transform.localRotation;
            phase = Random.value * 10f;
            Animator animator = GetComponentInChildren<Animator>();
            if (animator != null)
            {
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }
            else Debug.LogWarning("[Ghost] no Animator found", this);
        }

        void LateUpdate()
        {
            float t = Time.time + phase;
            float bob = hoverHeight + bobHeight * 0.5f * (1f + Mathf.Sin(t * 2f * Mathf.PI / bobPeriod));
            transform.localPosition = basePosition + Vector3.up * bob;
            transform.localRotation = baseRotation * Quaternion.Euler(0f, swayDegrees * Mathf.Sin(t * 2f * Mathf.PI / swayPeriod), 0f);
        }
    }
}
