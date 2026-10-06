using UnityEngine;

namespace FPCharacter
{
    public sealed class HandGlow : MonoBehaviour
    {
        public float intensityFlashlightOff = 0.45f;
        public float intensityFlashlightOn = 1.1f;
        public float range = 1.1f;
        public Color color = new Color(1f, 0.86f, 0.7f);
        public Vector3 offsetFromCamera = new Vector3(0f, -0.28f, 0.3f);
        public float blendSpeed = 4f;

        FirstPersonCharacterAnimator character;
        Light glow;
        float level;

        void Start()
        {
            character = GetComponentInChildren<FirstPersonCharacterAnimator>(true);
            Camera cam = GetComponentInChildren<Camera>(true);
            if (cam == null) cam = Camera.main;
            if (cam == null) { enabled = false; return; }
            GameObject go = new GameObject("Hand Glow");
            go.transform.SetParent(cam.transform, false);
            glow = go.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.shadows = LightShadows.None;
            glow.renderMode = LightRenderMode.ForcePixel;
            level = Target();
            Apply();
        }

        float Target()
        {
            bool on = character != null && (character.TorchVisible || character.RequestedTorchEquipped);
            return on ? intensityFlashlightOn : intensityFlashlightOff;
        }

        void Update()
        {
            if (glow == null) return;
            level = Mathf.MoveTowards(level, Target(), blendSpeed * Time.deltaTime);
            Apply();
        }

        void Apply()
        {
            glow.transform.localPosition = offsetFromCamera;
            glow.intensity = level;
            glow.range = range;
            glow.color = color;
            glow.enabled = level > 0.001f;
        }
    }
}
