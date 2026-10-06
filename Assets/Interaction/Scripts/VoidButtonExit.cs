using System.Collections;
using UnityEngine;

namespace FPCharacter
{
    public sealed class VoidButtonExit : MonoBehaviour
    {
        public string gateId = "lightblade";
        public float delayAfterPress = 0.6f;
        public BlackoutTransition blackout;
        public bool useComic = true;
        public string comicHeroFolder = "WarmStatues";
        public string comicPanel = "LightBlade/floor";
        public Rect comicPanelUv = new Rect(0.18f, 0.32f, 0.64f, 0.36f);
        public Vector2 comicHeroTarget = new Vector2(0.5f, 0.3f);
        public float comicHeroHeight = 0.2f;

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
            if (useComic)
            {
                var comic = gameObject.AddComponent<ComicPanelTransition>();
                comic.nextScene = "LightBlade2D";
                comic.heroFolder = comicHeroFolder;
                comic.nextPanel = Resources.Load<Texture2D>(comicPanel);
                comic.nextPanelUv = comicPanelUv;
                comic.heroTarget = comicHeroTarget;
                comic.heroHeight = comicHeroHeight;
                comic.blackout = null;
                comic.playerMovement = FindAnyObjectByType<FPCharacterMover>();
                comic.Play();
            }
            else if (blackout != null) blackout.Play();
            else UnityEngine.SceneManagement.SceneManager.LoadScene("LightBlade2D");
        }
    }
}
