using UnityEngine;

namespace FPCharacter
{
    public sealed class PuzzleGateLink : MonoBehaviour
    {
        public RoundTablePuzzle puzzle;
        public LockedGate gate;

        void Start()
        {
            if (puzzle != null && gate != null) puzzle.onCompleted.AddListener(gate.Open);
        }
    }
}
