using System.Collections.Generic;
using UnityEngine;

namespace FPCharacter
{
    public sealed class HideBeard : MonoBehaviour
    {
        public bool hide = true;
        readonly List<Transform> bones = new List<Transform>();

        void Awake()
        {
            foreach (Transform t in GetComponentsInChildren<Transform>(true))
                if (t.name == "Beard" || t.name == "Beard1") bones.Add(t);
        }

        void LateUpdate()
        {
            if (!hide) return;
            foreach (Transform b in bones) b.localScale = Vector3.one * 0.001f;
        }
    }
}
