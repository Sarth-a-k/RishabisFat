using UnityEngine;

namespace FPCharacter
{
    [DefaultExecutionOrder(120)]
    public sealed class FPLocomotionAnimator : MonoBehaviour
    {
        public FirstPersonCharacterAnimator character;
        public FPCharacterMover mover;
        public bool disablePreviewComponent = true;
        [Min(0f)] public float airGraceSeconds = 0.25f;
        [Min(0f)] public float startMovingSpeed = 0.5f;
        [Min(0f)] public float stopMovingSpeed = 0.25f;
        [Min(0.01f)] public float speedSmoothing = 0.08f;

        float airTime;
        float smoothSpeed;
        float speedVelocity;
        bool moving;

        void Awake()
        {
            if (character == null) character = GetComponentInChildren<FirstPersonCharacterAnimator>();
            if (character == null) character = GetComponentInParent<FirstPersonCharacterAnimator>();
            if (character == null) character = FindFirstObjectByType<FirstPersonCharacterAnimator>();
            if (mover == null) mover = GetComponentInParent<FPCharacterMover>();
            if (mover == null) mover = GetComponentInChildren<FPCharacterMover>();
            if (mover == null) mover = FindFirstObjectByType<FPCharacterMover>();

            if (disablePreviewComponent)
                foreach (FPCharacterPreview preview in FindObjectsByType<FPCharacterPreview>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    preview.enabled = false;

            if (character == null || mover == null)
            {
                Debug.LogWarning("FPLocomotionAnimator could not find " + (character == null ? "FirstPersonCharacterAnimator " : "") + (mover == null ? "FPCharacterMover" : "") + " in the scene.", this);
                enabled = false;
            }
        }

        void Update()
        {
            airTime = mover.IsGrounded ? 0f : airTime + Time.deltaTime;
            bool grounded = airTime <= airGraceSeconds;

            float raw = mover.IsSeated ? 0f : mover.CurrentSpeed;
            smoothSpeed = Mathf.SmoothDamp(smoothSpeed, raw, ref speedVelocity, speedSmoothing);
            if (moving && smoothSpeed < stopMovingSpeed) moving = false;
            else if (!moving && smoothSpeed > startMovingSpeed) moving = true;

            float speed = moving ? smoothSpeed : 0f;
            float movement01 = speed / Mathf.Max(0.1f, mover.runSpeed);
            if (moving) movement01 = Mathf.Max(movement01, 0.08f);
            float sprintThreshold = (mover.walkSpeed + mover.runSpeed) * 0.5f;
            bool sprinting = moving && speed > sprintThreshold && !mover.IsSeated;

            character.SetMotionState(movement01, sprinting, mover.IsCrouching || mover.IsSeated, grounded);
        }
    }
}
