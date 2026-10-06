using UnityEngine;

namespace FPCharacter
{
    public sealed class LighterView : MonoBehaviour
    {
        public Transform lidHinge;
        public Transform flame;
        public Light flameLight;
        public float openAngle = -125f;
        public float lidSpeed = 9f;
        public float flameIntensity = 1.3f;

        float lidTarget, lidAmount, flameTarget, flameAmount, flash, seed;
        Vector3 flameBaseScale = Vector3.one;

        public bool LidOpen => lidAmount > 0.95f;

        void Awake()
        {
            seed = Random.value * 100f;
            if (flame != null) flameBaseScale = flame.localScale;
            ResetState();
        }

        public void ResetState()
        {
            lidTarget = lidAmount = flameTarget = flameAmount = flash = 0f;
            Apply();
        }

        public void SetOpen(bool open) { lidTarget = open ? 1f : 0f; }

        public void SetLit(bool lit)
        {
            flameTarget = lit ? 1f : 0f;
            if (lit) flash = 1f;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            lidAmount = Mathf.MoveTowards(lidAmount, lidTarget, dt * lidSpeed);
            flameAmount = Mathf.MoveTowards(flameAmount, flameTarget, dt * (flameTarget > flameAmount ? 6f : 14f));
            flash = Mathf.MoveTowards(flash, 0f, dt * 10f);
            Apply();
        }

        void Apply()
        {
            if (lidHinge != null)
            {
                float s = lidAmount * lidAmount * (3f - 2f * lidAmount);
                lidHinge.localRotation = Quaternion.Euler(0f, 0f, openAngle * s);
            }
            float t = Time.time;
            float n1 = Mathf.PerlinNoise(seed, t * 9f) - 0.5f;
            float n2 = Mathf.PerlinNoise(seed + 31f, t * 13f) - 0.5f;
            if (flame != null)
            {
                bool on = flameAmount > 0.001f;
                if (flame.gameObject.activeSelf != on) flame.gameObject.SetActive(on);
                flame.localScale = new Vector3(flameBaseScale.x * (1f - n1 * 0.25f), flameBaseScale.y * (1f + n1 * 0.5f), flameBaseScale.z * (1f + n2 * 0.2f)) * flameAmount;
                flame.localRotation = Quaternion.Euler(n2 * 10f, 0f, n1 * 12f);
            }
            if (flameLight != null)
            {
                float intensity = flameIntensity * flameAmount * (1f + n1 * 0.4f) + flash * 3f;
                flameLight.intensity = intensity;
                flameLight.enabled = intensity > 0.001f;
            }
        }
    }
}
