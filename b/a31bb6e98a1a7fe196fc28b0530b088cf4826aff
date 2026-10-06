#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

// IMPORTANT: this file must live inside a folder named "Editor"
// (for example Assets/FPCharacter/Editor/FPClipScaleFix.cs).
//
// The animations hide an arm while it is off-screen by shrinking its bone to almost zero size.
// Unity blends that shrink over one frame, so for a split second you can see a squashed arm.
// This script makes the shrink/grow happen instantly instead (no in-between size).
public class FPClipScaleFix : AssetPostprocessor
{
    void OnPostprocessAnimation(GameObject root, AnimationClip clip)
    {
        // Only touch the flashlight character model
        if (!assetPath.Contains("FP_Character")) return;

        int fixedCurves = 0;
        foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip))
        {
            if (binding.type != typeof(Transform)) continue;
            if (!binding.propertyName.StartsWith("m_LocalScale")) continue;

            AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
            if (curve == null || curve.length < 2) continue;

            // Only curves that go (almost) to zero are the "hide this arm" curves
            Keyframe[] keys = curve.keys;
            float min = float.MaxValue;
            foreach (Keyframe k in keys) min = Mathf.Min(min, k.value);
            if (min > 0.05f) continue;

            // Infinity tangents = stepped (jump straight to the next value)
            for (int i = 0; i < keys.Length; i++)
            {
                keys[i].inTangent = float.PositiveInfinity;
                keys[i].outTangent = float.PositiveInfinity;
            }
            curve.keys = keys;
            clip.SetCurve(binding.path, binding.type, binding.propertyName, curve);
            fixedCurves++;
        }

        if (fixedCurves > 0)
            Debug.Log("FPClipScaleFix: made " + fixedCurves + " arm-hide curves stepped in clip '" + clip.name + "'");
    }
}
#endif
