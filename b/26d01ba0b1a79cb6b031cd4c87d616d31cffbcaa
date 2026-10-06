using UnityEngine;

namespace FPCharacter
{
    public sealed class UVFootprint : MonoBehaviour
    {
        public float maxIntensity = 1.8f;
        public float hintLevel = 0.3f;
        public float revealSpeed = 6f;
        public float fadeSpeed = 0.7f;

        Renderer rend;
        MaterialPropertyBlock block;
        float reveal;
        float seed;
        float nextCheck;
        bool lit;

        void Start()
        {
            rend = GetComponent<Renderer>();
            block = new MaterialPropertyBlock();
            seed = Random.value * 10f;
            Apply(0f);
        }

        void Update()
        {
            if (Time.time >= nextCheck)
            {
                lit = UVBaton.Illuminates(transform.position + Vector3.up * 0.02f);
                nextCheck = Time.time + 0.06f;
            }
            reveal = Mathf.MoveTowards(reveal, lit ? 1f : 0f, Time.deltaTime * (lit ? revealSpeed : fadeSpeed));
            if (lit && reveal > 0.5f) UVFootprintTrail.NotifyFound();
            float hint = UVFootprintTrail.HintOn ? hintLevel * (0.65f + 0.35f * Mathf.Sin(Time.time * 2.4f + seed)) : 0f;
            Apply(Mathf.Max(reveal, hint));
        }

        void Apply(float v)
        {
            if (rend == null) return;
            rend.GetPropertyBlock(block);
            block.SetFloat("_Intensity", maxIntensity * v);
            rend.SetPropertyBlock(block);
            rend.enabled = v > 0.002f;
        }
    }
}
