using UnityEngine;

namespace FPCharacter
{
    public sealed class SeatBeamTarget : MonoBehaviour
    {
        public int index;
        public Light glow;
        public float litIntensity = 2.2f;

        float level;

        public bool IsLit { get; private set; }
        public Vector3 BeamPoint => transform.position;

        public void SetLit(bool lit)
        {
            IsLit = lit;
        }

        void Update()
        {
            level = Mathf.MoveTowards(level, IsLit ? 1f : 0f, Time.deltaTime * 4f);
            if (glow == null) return;
            glow.intensity = litIntensity * level * (1f + 0.1f * Mathf.Sin(Time.time * 6f + index));
            glow.enabled = level > 0.001f;
        }
    }
}
