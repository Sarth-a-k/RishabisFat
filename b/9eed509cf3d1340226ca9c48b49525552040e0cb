using System.Collections;
using UnityEngine;

namespace FPCharacter
{
    public sealed class ShadowRunGate : MonoBehaviour
    {
        public LockedGate gate;
        public ComicPanelTransition transition;
        public Transform returnPoint;
        public float openDelay = 1.6f;
        public string playerTag = "Player";

        public static bool ShadowRunFinished { get; private set; }
        static bool entered;

        void OnTriggerEnter(Collider other)
        {
            if (ShadowRunFinished || transition == null || !transition.puzzleSolved) return;
            if (other.CompareTag(playerTag)) entered = true;
        }

        IEnumerator Start()
        {
            if (!entered && !ShadowRunFinished) yield break;
            bool fresh = entered;
            entered = false;
            ShadowRunFinished = true;
            if (transition != null)
            {
                transition.puzzleSolved = false;
                Destroy(transition);
            }
            yield return null;
            if (fresh && returnPoint != null)
            {
                GameObject p = GameObject.FindWithTag(playerTag);
                if (p != null)
                {
                    CharacterController cc = p.GetComponent<CharacterController>();
                    if (cc != null) cc.enabled = false;
                    p.transform.SetPositionAndRotation(returnPoint.position, Quaternion.Euler(0f, returnPoint.eulerAngles.y, 0f));
                    if (cc != null) cc.enabled = true;
                }
                yield return new WaitForSeconds(openDelay);
            }
            if (gate != null) gate.Open();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void ResetProgress()
        {
            ShadowRunFinished = false;
            entered = false;
        }
    }
}
