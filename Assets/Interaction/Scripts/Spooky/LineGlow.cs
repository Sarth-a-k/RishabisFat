using UnityEngine;

namespace FPCharacter
{
    public sealed class LineGlow : MonoBehaviour
    {
        public Material material;
        public float widthScale = 5f;
        public float minWidth = 0.14f;

        LineRenderer src;
        LineRenderer halo;
        Vector3[] buffer = new Vector3[2];

        void Start()
        {
            src = GetComponent<LineRenderer>();
            if (src == null || material == null) { enabled = false; return; }
            GameObject go = new GameObject("Halo");
            go.transform.SetParent(transform, false);
            halo = go.AddComponent<LineRenderer>();
            halo.sharedMaterial = material;
            halo.useWorldSpace = src.useWorldSpace;
            halo.textureMode = LineTextureMode.Stretch;
            halo.alignment = LineAlignment.View;
            halo.numCapVertices = Mathf.Max(2, src.numCapVertices);
            halo.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            halo.receiveShadows = false;
        }

        void LateUpdate()
        {
            if (halo == null) return;
            bool on = src.enabled && src.positionCount > 1;
            halo.enabled = on;
            if (!on) return;
            int n = src.positionCount;
            if (buffer.Length < n) buffer = new Vector3[n];
            src.GetPositions(buffer);
            halo.positionCount = n;
            halo.SetPositions(buffer);
            halo.widthCurve = src.widthCurve;
            halo.widthMultiplier = Mathf.Max(minWidth, src.widthMultiplier * widthScale);
            halo.colorGradient = src.colorGradient;
        }
    }
}
