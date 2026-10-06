using UnityEngine;

namespace FPCharacter
{
    public sealed class BeamGlowManager : MonoBehaviour
    {
        public Material haloMaterial;
        public string[] beamNames = { "Beam_", "LightBeam", "Prism to moon beam", "Crystal beam" };
        public float widthScale = 5f;
        public float minWidth = 0.14f;

        float next;

        void Update()
        {
            if (haloMaterial == null || Time.unscaledTime < next) return;
            next = Time.unscaledTime + 0.5f;
            foreach (LineRenderer lr in FindObjectsByType<LineRenderer>(FindObjectsSortMode.None))
            {
                if (lr == null || lr.name == "Halo" || lr.GetComponent<LineGlow>() != null) continue;
                bool match = false;
                foreach (string n in beamNames)
                    if (lr.name.StartsWith(n)) { match = true; break; }
                if (!match) continue;
                LineGlow g = lr.gameObject.AddComponent<LineGlow>();
                g.material = haloMaterial;
                g.widthScale = widthScale;
                g.minWidth = minWidth;
            }
        }
    }
}
