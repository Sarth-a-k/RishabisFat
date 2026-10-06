using UnityEngine;

namespace SunkenPrism
{
    /// <summary>Lets visible illumination fall away as the explorer descends toward infrared.</summary>
    public sealed class DungeonMood : MonoBehaviour
    {
        public float DescentStart = 135f;
        public float DarknessStart = 159f;
        public float MinimumVisibleLight = .025f;

        Color originalSky;
        Color originalEquator;
        Color originalGround;
        Color originalFog;
        Light sunlight;
        float originalSunIntensity;
        bool captured;

        void Start()
        {
            originalSky = RenderSettings.ambientSkyColor;
            originalEquator = RenderSettings.ambientEquatorColor;
            originalGround = RenderSettings.ambientGroundColor;
            originalFog = RenderSettings.fogColor;
            if (RenderSettings.sun != null && RenderSettings.sun.type == LightType.Directional)
                sunlight = RenderSettings.sun;
            else
            {
                foreach (Light candidate in FindObjectsByType<Light>(FindObjectsSortMode.None))
                {
                    if (candidate.type != LightType.Directional || !candidate.enabled) continue;
                    sunlight = candidate;
                    break;
                }
            }
            if (sunlight != null) originalSunIntensity = sunlight.intensity;
            captured = true;
            ApplyMood();
        }

        void Update()
        {
            ApplyMood();
        }

        void ApplyMood()
        {
            if (!captured) return;
            ExplorerController explorer = ExplorerController.Instance;
            if (explorer == null || explorer.View == null) return;
            float descent = Mathf.InverseLerp(DescentStart, DarknessStart, explorer.View.transform.position.z);
            float visibleLight = Mathf.Lerp(1f, MinimumVisibleLight, descent);
            RenderSettings.ambientSkyColor = originalSky * visibleLight;
            RenderSettings.ambientEquatorColor = originalEquator * visibleLight;
            RenderSettings.ambientGroundColor = originalGround * visibleLight;
            RenderSettings.fogColor = Color.Lerp(originalFog, new Color(.0015f, .003f, .007f), descent);
            if (sunlight != null) sunlight.intensity = originalSunIntensity * visibleLight;
            // The hand lamp and visor's thermal point light remain independent of the sun.
        }

        void OnDisable()
        {
            if (!captured) return;
            RenderSettings.ambientSkyColor = originalSky;
            RenderSettings.ambientEquatorColor = originalEquator;
            RenderSettings.ambientGroundColor = originalGround;
            RenderSettings.fogColor = originalFog;
            if (sunlight != null) sunlight.intensity = originalSunIntensity;
        }
    }
}
