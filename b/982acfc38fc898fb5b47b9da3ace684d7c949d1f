using System.Collections;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace FPCharacter
{
    public sealed class CombinationLock : MonoBehaviour
    {
        public Transform[] wheels = new Transform[3];
        public Renderer[] faceRenderers = new Renderer[0];
        public int[] values = { 1, 0, 0 };
        public int[] solution = { 0, 2, 1 };
        public LockedGate gate;
        public float useDistance = 2.8f;
        public float turnSeconds = 0.25f;
        public Color faceGlow = new Color(0.25f, 0.2f, 0.12f);
        public Color solvedGlow = new Color(0.3f, 1f, 0.4f);

        public bool Solved { get; private set; }

        bool[] turning;
        int lookWheel = -1;
        float solvedAt;
        Material[] faceMats;

        void Start()
        {
            turning = new bool[wheels.Length];
            for (int i = 0; i < wheels.Length; i++)
                if (wheels[i] != null) wheels[i].localRotation = Quaternion.AngleAxis(120f * values[i], Vector3.right);
            faceMats = new Material[faceRenderers.Length];
            for (int i = 0; i < faceRenderers.Length; i++)
            {
                if (faceRenderers[i] == null) continue;
                faceMats[i] = faceRenderers[i].material;
                faceMats[i].EnableKeyword("_EMISSION");
                faceMats[i].SetColor("_EmissionColor", faceGlow);
            }
        }

        void Update()
        {
            lookWheel = -1;
            if (Solved)
            {
                float k = Mathf.Clamp01((Time.time - solvedAt) / 0.8f);
                foreach (Material m in faceMats) if (m != null) m.SetColor("_EmissionColor", Color.Lerp(faceGlow, solvedGlow, k));
                return;
            }
            Camera cam = Camera.main;
            if (cam == null) return;
            Ray ray = new Ray(cam.transform.position, cam.transform.forward);
            if (Physics.Raycast(ray, out RaycastHit hit, useDistance, ~0, QueryTriggerInteraction.Ignore))
            {
                for (int i = 0; i < wheels.Length; i++)
                    if (wheels[i] != null && hit.collider.transform.IsChildOf(wheels[i])) { lookWheel = i; break; }
            }
            if (lookWheel >= 0 && !turning[lookWheel] && InteractPressed()) StartCoroutine(Turn(lookWheel));
        }

        IEnumerator Turn(int i)
        {
            turning[i] = true;
            Quaternion from = wheels[i].localRotation;
            values[i] = (values[i] + 1) % 3;
            Quaternion to = Quaternion.AngleAxis(120f * values[i], Vector3.right);
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / Mathf.Max(0.01f, turnSeconds);
                wheels[i].localRotation = Quaternion.Slerp(from, to, Mathf.SmoothStep(0f, 1f, t));
                yield return null;
            }
            wheels[i].localRotation = to;
            turning[i] = false;
            CheckSolved();
        }

        void CheckSolved()
        {
            for (int i = 0; i < wheels.Length; i++)
                if (values[i] != solution[i]) return;
            Solved = true;
            solvedAt = Time.time;
            StartCoroutine(OpenGate());
        }

        IEnumerator OpenGate()
        {
            yield return new WaitForSeconds(0.9f);
            if (gate != null) gate.Open();
        }

        static bool InteractPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.E);
#endif
        }

        void OnGUI()
        {
            if (lookWheel < 0 || Solved) return;
            PromptBox.Draw("[E] Turn dial", 0.66f);
        }
    }
}
