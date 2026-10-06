using UnityEngine;

namespace FPCharacter
{
    public sealed class PuzzleTransitionLink : MonoBehaviour
    {
        public RoundTablePuzzle puzzle;
        public ComicPanelTransition transition;
        public JulianTransition julian;
        public MinigameGate minigameGate;

        void Start()
        {
            if (puzzle != null) puzzle.onCompleted.AddListener(Arm);
        }

        void OnDestroy()
        {
            if (puzzle != null) puzzle.onCompleted.RemoveListener(Arm);
        }

        public void Arm()
        {
            if (transition != null) transition.SetPuzzleSolved();
            if (julian == null) return;
            if (minigameGate != null) MinigameGate.MarkEntered(minigameGate.id);
            julian.Play();
        }
    }
}
