using UnityEngine;

namespace FPCharacter
{
    public sealed class FaceCamera : MonoBehaviour
    {
        public bool uprightOnly = true;
        Transform cam;

        void LateUpdate()
        {
            if (cam == null) { if (Camera.main == null) return; cam = Camera.main.transform; }
            Vector3 d = transform.position - cam.position;
            if (uprightOnly) d.y = 0f;
            if (d.sqrMagnitude > 1e-6f) transform.rotation = Quaternion.LookRotation(d, Vector3.up);
        }
    }
}
