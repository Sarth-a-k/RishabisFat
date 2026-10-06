using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SunkenPrism
{
    // Visible at rest in the editor; the same supplied torch uses each region's fire colour.
    [DisallowMultipleComponent]
    public sealed class TorchFlame : MonoBehaviour
    {
        [SerializeField] Light fireLight;
        [SerializeField] Transform[] tongues;
        [SerializeField] float size = 1f;
        [SerializeField] float phase;
        static Mesh ribbonMesh;
        static readonly Dictionary<Color, Material> materials = new Dictionary<Color, Material>();
        public static float Brightness = 1.4196f;
        public static float LightRange = 8.5f;
        public float boost = 1f;

        public static GameObject Create(Transform parent, Vector3 worldPos, Color color, float size = 1f)
        {
            var root = new GameObject("Regional torch flame");
            root.transform.SetParent(parent, true);
            root.transform.position = worldPos;
            var flame = root.AddComponent<TorchFlame>();
            flame.size = size;
            flame.phase = worldPos.x * .73f + worldPos.z * .31f;
            flame.tongues = new Transform[3];
            if (!ribbonMesh)
            {
                ribbonMesh = new Mesh { name = "Soft torch flame ribbon" };
                ribbonMesh.vertices = new[] { new Vector3(-.5f, 0, 0), new Vector3(.5f, 0, 0), new Vector3(-.5f, 1, 0), new Vector3(.5f, 1, 0) };
                ribbonMesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one };
                ribbonMesh.triangles = new[] { 0, 2, 1, 1, 2, 3 };
                ribbonMesh.RecalculateNormals();
                ribbonMesh.RecalculateBounds();
            }
            if (!materials.TryGetValue(color, out var mat) || !mat)
            {
                mat = new Material(Shader.Find("SunkenPrism/TorchFlame")) { name = "Torch fire " + ColorUtility.ToHtmlStringRGB(color) };
                mat.SetColor("_Color", color);
                materials[color] = mat;
            }
            for (int i = 0; i < 3; i++)
            {
                var tongue = new GameObject("Soft flame tongue " + i);
                tongue.transform.SetParent(root.transform, false);
                tongue.transform.localRotation = Quaternion.Euler(0, i * 60, 0);
                tongue.transform.localScale = new Vector3(.48f * size, (.73f - i * .075f) * size, 1);
                tongue.AddComponent<MeshFilter>().sharedMesh = ribbonMesh;
                var renderer = tongue.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = mat;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                flame.tongues[i] = tongue.transform;
            }
            var lightObject = new GameObject("Torch local light");
            lightObject.transform.SetParent(root.transform, false);
            lightObject.transform.localPosition = Vector3.up * .20f * size;
            flame.fireLight = lightObject.AddComponent<Light>();
            flame.fireLight.type = LightType.Point;
            flame.fireLight.color = color;
            flame.fireLight.intensity = .95f * Brightness;
            flame.fireLight.range = LightRange;
            flame.fireLight.shadows = LightShadows.Soft;
            flame.fireLight.shadowStrength = .95f;
            flame.fireLight.shadowBias = .03f;
            flame.fireLight.renderMode = LightRenderMode.ForcePixel;
            return root;
        }

        void OnEnable()
        {
            RefreshLighting();
        }

        public void RefreshLighting()
        {
            if (!fireLight) return;
            fireLight.intensity = .95f * Brightness * boost;
            fireLight.range = LightRange * (1f + (boost - 1f) * .4f);
        }

        void Update()
        {
            float t = Time.time;
            float flutter = Mathf.PerlinNoise(phase, t * 7.3f);
            if (fireLight) { fireLight.intensity = (.78f + flutter * .30f) * Brightness * boost; fireLight.range = LightRange * (1f + (boost - 1f) * .4f); }
            if (tongues == null) return;
            for (int i = 0; i < tongues.Length; i++)
            {
                if (!tongues[i]) continue;
                float pulse = 1 + .10f * Mathf.Sin(t * (6.4f + i) + phase + i * 2);
                tongues[i].localScale = new Vector3(.48f * size / pulse, (.73f - i * .075f) * size * pulse, 1);
            }
        }
    }
}
