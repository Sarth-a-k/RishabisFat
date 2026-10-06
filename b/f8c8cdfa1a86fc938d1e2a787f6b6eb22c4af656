using UnityEngine;

namespace FPCharacter
{
    public sealed class InfraredOnly : MonoBehaviour
    {
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
            foreach (Renderer r in renderers)
                if (r != null) r.enabled = on;
        }
    }
}
