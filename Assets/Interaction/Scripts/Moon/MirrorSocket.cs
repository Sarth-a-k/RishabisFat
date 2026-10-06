using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace FPCharacter
{
    public sealed class MirrorSocket : MonoBehaviour
    {
        public string shapeName = "Oval";
        public MirrorPickup mirror;
        public Transform previousPoint;
        public Transform nextPoint;
        public float snapRadius = 0.9f;
        public float angleTolerance = 9f;
        public float surfaceHeight = 0.12f;
        public LineRenderer outline;
        public Color idleColor = new Color(1f, 0.72f, 0.3f, 1f);
        public Color filledColor = new Color(0.45f, 1f, 0.55f, 1f);

        float solvedYaw;
        bool hasSolved;
        Material outlineMat;

        public bool Occupied { get; private set; }
        public bool Aligned { get; private set; }

        void Start()
        {
            ComputeSolvedYaw();
            if (outline != null)
            {
                outlineMat = outline.material;
            }
        }

        void ComputeSolvedYaw()
        {
            if (mirror == null || previousPoint == null || nextPoint == null) return;
            Vector3 p = Flat(transform.position);
            Vector3 a = (Flat(previousPoint.position) - p).normalized;
            Vector3 b = (Flat(nextPoint.position) - p).normalized;
            Vector3 n = (a + b);
            if (n.sqrMagnitude < 1e-6f) return;
            n.Normalize();
            Vector3 local = mirror.localNormal;
            local.y = 0f;
            if (local.sqrMagnitude < 1e-6f) local = mirror.FlatFront;
            local.Normalize();
            float worldAngle = Mathf.Atan2(n.x, n.z) * Mathf.Rad2Deg;
            float localAngle = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
            solvedYaw = worldAngle - localAngle;
            hasSolved = true;
        }

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

        void LateUpdate()
        {
            Occupied = false;
            Aligned = false;
            if (mirror != null && !mirror.IsCarried)
            {
                Vector3 d = Flat(mirror.transform.position - transform.position);
                if (d.magnitude <= snapRadius)
                {
                    Occupied = true;
                    Vector3 top = transform.position + Vector3.up * surfaceHeight;
                    if ((mirror.transform.position - top).sqrMagnitude > 1e-6f) mirror.transform.position = top;
                    if (hasSolved)
                    {
                        float yaw = mirror.transform.eulerAngles.y;
                        float diff = Mathf.DeltaAngle(yaw, solvedYaw);
                        if (Mathf.Abs(diff) <= angleTolerance && !RotateHeld())
                        {
                            mirror.transform.rotation = Quaternion.Euler(0f, solvedYaw, 0f);
                            Aligned = true;
                        }
                        else if (Mathf.Abs(diff) < 0.05f) Aligned = true;
                    }
                }
            }
            if (outlineMat != null)
            {
                Color c = Aligned ? filledColor : idleColor;
                float pulse = Aligned ? 1f : 0.65f + 0.35f * Mathf.Sin(Time.time * 2.2f);
                c *= pulse;
                c.a = 1f;
                outlineMat.SetColor("_BaseColor", c);
            }
        }

        static bool RotateHeld()
        {
#if ENABLE_INPUT_SYSTEM
            var k = Keyboard.current;
            return k != null && (k.qKey.isPressed || k.rKey.isPressed);
#else
            return Input.GetKey(KeyCode.Q) || Input.GetKey(KeyCode.R);
#endif
        }
    }
}
