using UnityEngine;

namespace FPCharacter
{
    public sealed class RotatingRing : MonoBehaviour
    {
        public string displayName = "ring";
        public float stepDegrees = 10f;
        public int stepIndex;
        public float turnSpeed = 120f;

        float current;

        public float TargetAngle => stepIndex * stepDegrees;
        public bool IsTurning => Mathf.Abs(Mathf.DeltaAngle(current, TargetAngle)) > 0.05f;

        void Awake()
        {
            current = TargetAngle;
            Apply();
        }

        public void Step(int direction)
        {
            stepIndex += direction;
        }

        void Update()
        {
            current = Mathf.MoveTowardsAngle(current, TargetAngle, turnSpeed * Time.deltaTime);
            Apply();
        }

        void Apply()
        {
            transform.localRotation = Quaternion.Euler(0f, current, 0f);
        }
    }
}
