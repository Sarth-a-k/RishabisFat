using UnityEngine;

namespace FPCharacter
{
    [DefaultExecutionOrder(200)]
    public sealed class TorchAngleAdjust : MonoBehaviour
    {
        [Tooltip("Tilt down (positive) or up (negative)")]
        public float lowerDegrees = 10f;
        [Tooltip("Turn left (negative) or right (positive)")]
        public float sideDegrees = 0f;
        [Tooltip("Twist the flashlight around its own length")]
        public float rollDegrees = 0f;
        public float blendSpeed = 6f;

        public FirstPersonCharacterAnimator character;
        public string handBoneName = "hand.R";
        public string socketBoneName = "torch_socket";

        Transform hand, socket;
        float weight;

        void Awake()
        {
            if (character == null) character = GetComponent<FirstPersonCharacterAnimator>();
            if (character == null) character = GetComponentInChildren<FirstPersonCharacterAnimator>();

            foreach (Transform t in GetComponentsInChildren<Transform>(true))
            {
                if (t.name == handBoneName) hand = t;
                else if (t.name == socketBoneName) socket = t;
            }

            if (hand == null || character == null)
            {
                Debug.LogWarning("TorchAngleAdjust: could not find '" + handBoneName + "' or the FirstPersonCharacterAnimator. Put this script on the FP_Character object.", this);
                enabled = false;
            }
        }

        void LateUpdate()
        {
            bool torchOut = character.TorchEquipped || character.TorchVisible;
            weight = Mathf.MoveTowards(weight, torchOut ? 1f : 0f, blendSpeed * Time.deltaTime);
            if (weight <= 0f) return;

            Transform reference = character.viewCamera != null ? character.viewCamera : character.transform;
            Quaternion tilt = Quaternion.AngleAxis(sideDegrees * weight, reference.up)
                            * Quaternion.AngleAxis(lowerDegrees * weight, reference.right)
                            * Quaternion.AngleAxis(rollDegrees * weight, reference.forward);

            Vector3 pivot = hand.position;
            hand.rotation = tilt * hand.rotation;

            if (socket != null && !socket.IsChildOf(hand))
            {
                socket.position = pivot + tilt * (socket.position - pivot);
                socket.rotation = tilt * socket.rotation;
            }
        }
    }
}
