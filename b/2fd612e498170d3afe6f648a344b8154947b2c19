using UnityEngine;

namespace FPCharacter
{
    public sealed class SlowSpin : MonoBehaviour
    {
        public Vector3 degreesPerSecond = new Vector3(0f, 18f, 0f);
        public float bobHeight = 0.18f;
        public float bobSpeed = 0.7f;

        Vector3 basePos;

        void Start()
        {
            basePos = transform.localPosition;
        }

        void Update()
        {
            transform.Rotate(degreesPerSecond * Time.deltaTime, Space.Self);
            transform.localPosition = basePos + Vector3.up * Mathf.Sin(Time.time * bobSpeed) * bobHeight;
        }
    }
}
