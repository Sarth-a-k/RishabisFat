using UnityEngine;

namespace FPCharacter
{
    public sealed class HeatSignature : MonoBehaviour
    {
        [Range(0f, 1f)] public float heat = 1f;
    }
}
