using UnityEngine;

namespace FPCharacter
{
    public sealed class RingBlocker : MonoBehaviour
    {
        public bool infraredOnly = true;

        Renderer[] renderers;

        void Awake()
        {
            renderers = GetComponentsInChildren<Renderer>(true);
            Apply(InfraredVision.Active);
            InfraredVision.Changed += Apply;
        }

        void OnDestroy()
        {
            InfraredVision.Changed -= Apply;
        }

        void Apply(bool on)
        {
            bool show = !infraredOnly || on;
            foreach (Renderer r in renderers)
                if (r != null) r.enabled = show;
        }
    }
}
