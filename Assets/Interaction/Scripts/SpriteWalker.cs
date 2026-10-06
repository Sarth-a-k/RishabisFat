using System.Collections.Generic;
using UnityEngine;

namespace FPCharacter
{
    public sealed class SpriteWalker : MonoBehaviour
    {
        public string folder = "WarmStatues";
        public string framePrefix = "ending_";
        public float height = 1.75f;
        public float speed = 1.25f;
        public float frameTime = 0.16f;
        public List<Vector3> path = new List<Vector3>();
        public int pauseAtIndex = -1;
        public float pauseSeconds = 1.6f;
        public bool walking;

        Texture2D idle, walk1, walk2;
        MeshRenderer quad;
        Transform quadT;
        MaterialPropertyBlock block;
        Transform cam;
        int index;
        float clock, pauseLeft;
        bool paused, pauseDone;
        Vector3 moveDir = Vector3.right;

        public bool Arrived => index >= path.Count;

        void Awake()
        {
            idle = Load("idle");
            walk1 = Load("walk1");
            walk2 = Load("walk2");
            block = new MaterialPropertyBlock();
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "Explorer sprite";
            Destroy(go.GetComponent<Collider>());
            quadT = go.transform;
            quadT.SetParent(transform, false);
            float w = height;
            quadT.localScale = new Vector3(w, height, 1f);
            quadT.localPosition = new Vector3(0f, height * 0.5f - height * (12f / 512f), 0f);
            quad = go.GetComponent<MeshRenderer>();
            quad.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Shader sh = Shader.Find("Universal Render Pipeline/Simple Lit");
            var m = new Material(sh);
            m.SetFloat("_AlphaClip", 1f);
            m.SetFloat("_Cutoff", 0.5f);
            m.EnableKeyword("_ALPHATEST_ON");
            m.SetFloat("_Cull", 0f);
            m.SetColor("_BaseColor", Color.white);
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", new Color(0.18f, 0.16f, 0.14f));
            m.SetTexture("_BaseMap", idle);
            m.SetTexture("_EmissionMap", idle);
            quad.sharedMaterial = m;
            SetFrame(idle, false);
        }

        public bool IsPaused => paused;

        Texture2D Load(string n)
        {
            Texture2D t = Resources.Load<Texture2D>(folder + "/" + framePrefix + n);
            if (t == null) t = Resources.Load<Texture2D>(folder + "/player_" + n);
            return t;
        }

        void SetFrame(Texture2D t, bool flip)
        {
            if (t == null) return;
            quad.GetPropertyBlock(block);
            block.SetTexture("_BaseMap", t);
            block.SetTexture("_EmissionMap", t);
            block.SetVector("_BaseMap_ST", flip ? new Vector4(-1f, 1f, 1f, 0f) : new Vector4(1f, 1f, 0f, 0f));
            quad.SetPropertyBlock(block);
        }

        void Update()
        {
            if (cam == null && Camera.main != null) cam = Camera.main.transform;
            bool moving = false;
            if (walking && !Arrived)
            {
                if (paused)
                {
                    pauseLeft -= Time.deltaTime;
                    if (pauseLeft <= 0f) { paused = false; pauseDone = true; }
                }
                else
                {
                    Vector3 target = path[index];
                    Vector3 p = transform.position;
                    Vector3 flat = new Vector3(target.x - p.x, 0f, target.z - p.z);
                    float step = speed * Time.deltaTime;
                    if (flat.magnitude <= step)
                    {
                        if (index == pauseAtIndex && !pauseDone) { paused = true; pauseLeft = pauseSeconds; }
                        index++;
                    }
                    if (flat.sqrMagnitude > 1e-6f) moveDir = flat.normalized;
                    p += moveDir * Mathf.Min(step, flat.magnitude);
                    if (Physics.Raycast(new Vector3(p.x, p.y + 1.5f, p.z), Vector3.down, out RaycastHit hit, 4f, ~0, QueryTriggerInteraction.Ignore)) p.y = Mathf.Lerp(p.y, hit.point.y, 0.25f);
                    transform.position = p;
                    moving = true;
                }
            }
            clock += Time.deltaTime;
            bool flip = false;
            if (cam != null)
            {
                Vector3 toCam = cam.position - quadT.position;
                toCam.y = 0f;
                if (toCam.sqrMagnitude > 1e-6f) quadT.rotation = Quaternion.LookRotation(-toCam.normalized, Vector3.up);
                flip = Vector3.Dot(moveDir, cam.right) < 0f;
            }
            if (moving)
            {
                int f = (int)(clock / frameTime) % 2;
                SetFrame(f == 0 ? walk1 : walk2, flip);
                quadT.localPosition = new Vector3(0f, height * 0.5f - height * (12f / 512f) + Mathf.Abs(Mathf.Sin(clock * Mathf.PI / frameTime)) * 0.03f, 0f);
            }
            else
            {
                SetFrame(idle, false);
                quadT.localPosition = new Vector3(0f, height * 0.5f - height * (12f / 512f), 0f);
            }
        }
    }
}
