using UnityEngine;

namespace FPCharacter
{
    /// <summary>Optional read-only adapter for an existing CharacterController. It never moves or rotates the player.</summary>
    [DefaultExecutionOrder(100)]
    public sealed class FPCharacterControllerAdapter : MonoBehaviour
    {
        public FirstPersonCharacterAnimator character;
        public CharacterController movementSource;
        [Tooltip("Actual horizontal speed above which the sprint cycle plays.")]
        [Min(0.1f)] public float sprintThreshold = 4.5f;
        [Tooltip("Controller height below which crouch is considered active.")]
        [Min(0.1f)] public float crouchHeightThreshold = 1.4f;
        [Min(0.1f)] public float fullMovementSpeed = 5.5f;

        void Reset()
        {
            movementSource = GetComponentInParent<CharacterController>();
            character = GetComponentInChildren<FirstPersonCharacterAnimator>();
        }
        void Update()
        {
            if (character == null || movementSource == null) return;
            Vector3 velocity = movementSource.velocity;
            float speed = new Vector2(velocity.x, velocity.z).magnitude;
            character.SetMotionState(speed / Mathf.Max(0.1f, fullMovementSpeed), speed > sprintThreshold,
                movementSource.height < crouchHeightThreshold, movementSource.isGrounded);
        }
    }
}
