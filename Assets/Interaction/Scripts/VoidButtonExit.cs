using System.Collections;
using UnityEngine;

namespace FPCharacter
{
    public sealed class VoidButtonExit : MonoBehaviour
    {
        public string gateId = "lightblade";
        public float delayAfterPress = 0.6f;
        public BlackoutTransition blackout;

        RedButton button;
        bool fired;

        void Start()
        {
            if (blackout == null) blackout = GetComponent<BlackoutTransition>();
            StartCoroutine(Hook());
        }

        IEnumerator Hook()
        {
            while (button == null)
            {
                button = FindAnyObjectByType<RedButton>();
                if (button == null) yield return new WaitForSeconds(0.5f);
            }
            button.onPressed.AddListener(OnPressed);
        }

        void OnDestroy()
        {
            if (button != null) button.onPressed.RemoveListener(OnPressed);
        }

        void OnPressed()
        {
            if (fired || MinigameGate.IsFinished(gateId)) return;
            fired = true;
            StartCoroutine(Go());
        }

        IEnumerator Go()
        {
            yield return new WaitForSeconds(delayAfterPress);
            MinigameGate.MarkEntered(gateId);
            if (blackout != null) blackout.Play();
            else UnityEngine.SceneManagement.SceneManager.LoadScene("LightBlade2D");
        }
    }
}
