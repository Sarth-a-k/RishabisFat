using UnityEngine;

namespace FPCharacter
{
    public sealed class TorchLightBudget : MonoBehaviour
    {
        [Header("Room torches")]
        public float torchBrightness = 2.4f;
        public float torchRange = 14f;

        [Header("Performance")]
        public int maxLights = 8;
        public int maxShadowLights = 2;
        public float maxDistance = 40f;
        public float shadowDistance = 14f;

        Light[] torches;
        Light[] nearest;
        float[] distances;
        float nextRefresh;

        void Start()
        {
            ApplyTorchSettings();
            var flames = FindObjectsByType<SunkenPrism.TorchFlame>(FindObjectsSortMode.None);
            var list = new System.Collections.Generic.List<Light>();
            foreach (var f in flames)
            {
                Light l = f.GetComponentInChildren<Light>();
                if (l != null) list.Add(l);
            }
            torches = list.ToArray();
            nearest = new Light[maxLights];
            distances = new float[maxLights];
            Refresh();
        }

        void ApplyTorchSettings()
        {
            SunkenPrism.TorchFlame.Brightness = torchBrightness;
            SunkenPrism.TorchFlame.LightRange = torchRange;
        }

        void Update()
        {
            ApplyTorchSettings();
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + 0.25f;
            Refresh();
        }

        void Refresh()
        {
            if (torches == null) return;
            Vector3 p = transform.position;
            for (int i = 0; i < nearest.Length; i++) { nearest[i] = null; distances[i] = float.MaxValue; }
            foreach (Light l in torches)
            {
                if (l == null) continue;
                l.enabled = false;
                l.shadows = LightShadows.None;
                float d = (l.transform.position - p).sqrMagnitude;
                if (d > maxDistance * maxDistance) continue;
                for (int i = 0; i < nearest.Length; i++)
                {
                    if (d >= distances[i]) continue;
                    for (int j = nearest.Length - 1; j > i; j--) { nearest[j] = nearest[j - 1]; distances[j] = distances[j - 1]; }
                    nearest[i] = l;
                    distances[i] = d;
                    break;
                }
            }
            for (int i = 0; i < nearest.Length; i++)
            {
                if (nearest[i] == null) continue;
                nearest[i].enabled = true;
                if (i < maxShadowLights && distances[i] < shadowDistance * shadowDistance) nearest[i].shadows = LightShadows.Hard;
            }
        }

        void OnDisable()
        {
            if (torches == null) return;
            foreach (Light l in torches) if (l != null) l.enabled = true;
        }
    }
}
