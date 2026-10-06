using UnityEngine;

namespace FPCharacter
{
    public sealed class AxisBillboard : MonoBehaviour
    {
        Vector3 axis;
        Transform cam;

        void Start()
        {
            axis = transform.up;
        }

        void LateUpdate()
        {
            if (cam == null) { if (Camera.main == null) return; cam = Camera.main.transform; }
            Vector3 toCam = cam.position - transform.position;
            Vector3 n = Vector3.ProjectOnPlane(toCam, axis);
            if (n.sqrMagnitude < 1e-6f) return;
            transform.rotation = Quaternion.LookRotation(-n.normalized, axis);
        }
    }
}
