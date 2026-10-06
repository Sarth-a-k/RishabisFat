using UnityEngine;

namespace FPCharacter
{
    public sealed class GhostIdleLife : MonoBehaviour
    {
        [Header("Breathing")]
        public float breathPeriod = 4.2f;
        public float breathDegrees = 1.6f;
        public float shoulderDegrees = 1.2f;

        [Header("Look")]
        public float lookRadius = 7f;
        public float maxYaw = 55f;
        public float maxPitch = 20f;
        public float lookSpeed = 2.2f;
        public float glanceYaw = 25f;
        public Vector2 glanceInterval = new Vector2(3f, 7f);

        [Header("Fidget")]
        public float headTiltDegrees = 2.5f;
        public float weightShiftDegrees = 1.5f;
        public float weightShiftPeriod = 9f;
        public float armSwayDegrees = 2f;
        public float lanternFlicker = 0.18f;

        Transform hips, spine1, spine2, neck, head, lShoulder, rShoulder, lArm, rArm, rForeArm;
        Transform[] bones;
        Quaternion[] lastSet;
        Quaternion[] lastBase;
        Light lantern;
        float lanternBase;
        Transform cam;
        float seed;
        float yaw, pitch;
        float glanceTarget, glancePitch, nextGlance;

        void Start()
        {
            hips = Bone("Hips");
            spine1 = Bone("Spine1");
            spine2 = Bone("Spine2");
            neck = Bone("Neck");
            head = Bone("Head");
            lShoulder = Bone("LeftShoulder");
            rShoulder = Bone("RightShoulder");
            lArm = Bone("LeftArm");
            rArm = Bone("RightArm");
            rForeArm = Bone("RightForeArm");
            bones = new[] { hips, spine1, spine2, lShoulder, rShoulder, lArm, rArm, rForeArm, neck, head };
            lastSet = new Quaternion[bones.Length];
            lastBase = new Quaternion[bones.Length];
            for (int i = 0; i < bones.Length; i++)
                if (bones[i] != null) { lastBase[i] = bones[i].localRotation; lastSet[i] = new Quaternion(0f, 0f, 0f, 0f); }
            Transform lt = Bone("LanternLight");
            if (lt != null) lantern = lt.GetComponent<Light>();
            if (lantern != null) lanternBase = lantern.intensity;
            seed = Random.value * 100f;
            nextGlance = Time.time + Random.Range(glanceInterval.x, glanceInterval.y);
        }

        Transform Bone(string n)
        {
            foreach (Transform t in GetComponentsInChildren<Transform>(true)) if (t.name == n) return t;
            return null;
        }

        void LateUpdate()
        {
            float t = Time.time;
            for (int i = 0; i < bones.Length; i++)
            {
                Transform b = bones[i];
                if (b == null) continue;
                Quaternion cur = b.localRotation;
                Quaternion baseRot = Quaternion.Dot(cur, lastSet[i]) > 0.999999f ? lastBase[i] : cur;
                lastBase[i] = baseRot;
                b.localRotation = baseRot;
            }

            Vector3 up = transform.up;
            Vector3 right = transform.right;
            Vector3 fwd = transform.forward;
            float breath = Mathf.Sin((t + seed) * 2f * Mathf.PI / breathPeriod);
            float shift = Mathf.Sin((t + seed) * 2f * Mathf.PI / weightShiftPeriod);

            Turn(hips, Quaternion.AngleAxis(weightShiftDegrees * shift, fwd));
            Turn(spine1, Quaternion.AngleAxis(-breathDegrees * 0.5f * breath, right) * Quaternion.AngleAxis(-weightShiftDegrees * 0.6f * shift, fwd));
            Turn(spine2, Quaternion.AngleAxis(-breathDegrees * 0.5f * breath, right));
            Turn(lShoulder, Quaternion.AngleAxis(shoulderDegrees * breath, fwd));
            Turn(rShoulder, Quaternion.AngleAxis(-shoulderDegrees * breath, fwd));
            float sway = (Mathf.PerlinNoise(seed, t * 0.35f) - 0.5f) * 2f;
            Turn(lArm, Quaternion.AngleAxis(armSwayDegrees * 0.5f * sway, right));
            Turn(rArm, Quaternion.AngleAxis(armSwayDegrees * sway, right));
            Turn(rForeArm, Quaternion.AngleAxis(armSwayDegrees * 0.6f * Mathf.Sin(t * 1.3f + seed), right));

            UpdateLook(t);
            float tilt = (Mathf.PerlinNoise(seed + 7f, t * 0.25f) - 0.5f) * 2f * headTiltDegrees;
            Quaternion look = Quaternion.AngleAxis(yaw, up) * Quaternion.AngleAxis(pitch, right);
            Turn(neck, Quaternion.Slerp(Quaternion.identity, look, 0.4f));
            Turn(head, Quaternion.Slerp(Quaternion.identity, look, 0.6f) * Quaternion.AngleAxis(tilt, fwd));

            for (int i = 0; i < bones.Length; i++) if (bones[i] != null) lastSet[i] = bones[i].localRotation;

            if (lantern != null) lantern.intensity = lanternBase * (1f - lanternFlicker * 0.5f + lanternFlicker * Mathf.PerlinNoise(seed + 3f, t * 6f));
        }

        void UpdateLook(float t)
        {
            if (cam == null && Camera.main != null) cam = Camera.main.transform;
            float wantYaw = glanceTarget;
            float wantPitch = glancePitch;
            bool tracking = false;
            if (cam != null && head != null)
            {
                Vector3 d = transform.InverseTransformDirection(cam.position - head.position);
                float flat = new Vector2(d.x, d.z).magnitude;
                float a = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
                if (flat <= lookRadius && Mathf.Abs(a) <= maxYaw + 35f)
                {
                    tracking = true;
                    wantYaw = Mathf.Clamp(a, -maxYaw, maxYaw);
                    wantPitch = Mathf.Clamp(-Mathf.Atan2(d.y, Mathf.Max(0.01f, flat)) * Mathf.Rad2Deg, -maxPitch, maxPitch);
                }
            }
            if (!tracking && t >= nextGlance)
            {
                glanceTarget = Random.value < 0.35f ? 0f : Random.Range(-glanceYaw, glanceYaw);
                glancePitch = Random.Range(-4f, 8f);
                nextGlance = t + Random.Range(glanceInterval.x, glanceInterval.y);
            }
            float k = 1f - Mathf.Exp(-lookSpeed * Time.deltaTime);
            yaw = Mathf.Lerp(yaw, wantYaw, k);
            pitch = Mathf.Lerp(pitch, wantPitch, k);
        }

        static void Turn(Transform b, Quaternion worldOffset)
        {
            if (b != null) b.rotation = worldOffset * b.rotation;
        }
    }
}
