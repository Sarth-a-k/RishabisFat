using UnityEngine;

namespace FPCharacter
{
    public sealed class LookThought : MonoBehaviour
    {
        [TextArea(1, 3)] public string thought = "\"...\"";
        public float distance = 3.2f;
        public float seconds = 4f;
        public float lookDelay = 0.6f;

        bool shown;
        float lookedFor;
        float until = -1f;

        void Update()
        {
            if (shown || JournalReader.Reading) return;
            Camera cam = Camera.main;
            if (cam == null) return;
            bool looking = Physics.Raycast(new Ray(cam.transform.position, cam.transform.forward), out RaycastHit hit, distance, ~0, QueryTriggerInteraction.Ignore) && hit.collider.transform.IsChildOf(transform);
            lookedFor = looking ? lookedFor + Time.deltaTime : 0f;
            if (lookedFor >= lookDelay)
            {
                shown = true;
                until = Time.time + seconds;
            }
        }

        void OnGUI()
        {
            if (Time.time < until && !JournalReader.Reading) SubtitleBox.Draw(thought, SubtitleBox.Fade(until - seconds, until));
        }
    }
}
