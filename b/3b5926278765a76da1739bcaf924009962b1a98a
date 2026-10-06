using System.Collections;
using UnityEngine;

namespace FPCharacter
{
    public sealed class PrismAutoEquip : MonoBehaviour
    {
        public PrismPickup pickup;
        public Vector3 areaCenter = new Vector3(112.15f, -2f, 0f);
        public Vector2 areaSize = new Vector2(46.5f, 30f);
        public string minigameId = "warmstatues";

        Transform player;
        bool done;

        void Start()
        {
            if (pickup != null) pickup.gameObject.SetActive(false);
        }

        void Update()
        {
            if (done || pickup == null) return;
            if (MinigameGate.IsFinished(minigameId)) { done = true; return; }
            if (player == null)
            {
                FPCharacterMover m = FindAnyObjectByType<FPCharacterMover>();
                if (m != null) player = m.transform;
                if (player == null) return;
            }
            Vector3 d = player.position - areaCenter;
            if (Mathf.Abs(d.x) > areaSize.x * 0.5f || Mathf.Abs(d.z) > areaSize.y * 0.5f) return;
            if (PrismPickup.Carried != null) { done = true; return; }
            done = true;
            StartCoroutine(Equip());
        }

        IEnumerator Equip()
        {
            pickup.gameObject.SetActive(true);
            yield return null;
            yield return null;
            if (pickup != null) pickup.Equip();
        }
    }
}
