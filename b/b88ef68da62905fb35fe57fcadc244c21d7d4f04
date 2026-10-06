using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FPCharacter
{
    public sealed class MinigameGate : MonoBehaviour
    {
        public string id = "warmstatues";
        public LockedGate gate;
        public ComicPanelTransition transition;
        public Transform returnPoint;
        public float openDelay = 1.6f;
        public string playerTag = "Player";

        static readonly HashSet<string> finished = new HashSet<string>();
        static readonly HashSet<string> entered = new HashSet<string>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            finished.Clear();
            entered.Clear();
        }

        public static bool IsFinished(string gateId) { return finished.Contains(gateId); }
        public static void MarkEntered(string gateId) { if (!finished.Contains(gateId)) entered.Add(gateId); }

        void OnTriggerEnter(Collider other)
        {
            if (finished.Contains(id) || transition == null || !transition.puzzleSolved) return;
            if (other.CompareTag(playerTag)) entered.Add(id);
        }

        IEnumerator Start()
        {
            bool fresh = entered.Contains(id);
            if (!fresh && !finished.Contains(id)) yield break;
            entered.Remove(id);
            finished.Add(id);
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
    }
}
