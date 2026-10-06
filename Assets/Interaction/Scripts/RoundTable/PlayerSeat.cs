using UnityEngine;

namespace FPCharacter
{
    public sealed class PlayerSeat : MonoBehaviour
    {
        public Transform tableCenter;
        public float seatedEyeOffset = -0.44f;
        public float standUpDistance = 0.6f;
        public Collider[] disableWhileSeated = new Collider[0];

        public bool Occupied { get; private set; }

        public Vector3 SeatPosition => transform.position;

        public Quaternion FacingRotation
        {
            get
            {
                Vector3 d = tableCenter != null ? tableCenter.position - transform.position : transform.forward;
                d.y = 0f;
                return d.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(d.normalized, Vector3.up) : transform.rotation;
            }
        }

        public Vector3 StandPosition
        {
            get
            {
                Vector3 away = transform.position - (tableCenter != null ? tableCenter.position : transform.position - transform.forward);
                away.y = 0f;
                return transform.position + away.normalized * standUpDistance;
            }
        }

        public void SetOccupied(bool occupied)
        {
            Occupied = occupied;
            foreach (Collider c in disableWhileSeated)
                if (c != null) c.enabled = !occupied;
        }
    }
}
