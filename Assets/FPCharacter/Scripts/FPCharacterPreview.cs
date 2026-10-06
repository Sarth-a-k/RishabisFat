using UnityEngine;

namespace FPCharacter
{
    /// <summary>Animation-only preview UI. This is in the demo scene only, never on the production prefab.</summary>
    public sealed class FPCharacterPreview : MonoBehaviour
    {
        public FirstPersonCharacterAnimator character;
        public Transform previewCamera;
        public float standingEyeHeight = 1.64f;
        public float crouchEyeDrop = 0.36f;
        float movement, airTime;
        float pitch = 2f;
        bool sprint, crouch;

        void Update()
        {
            airTime = Mathf.Max(0f, airTime - Time.deltaTime);
            character.SetMotionState(movement, sprint, crouch, airTime <= 0f);
            if (previewCamera != null)
            {
                float desiredHeight = standingEyeHeight - (crouch ? crouchEyeDrop : 0f);
                Vector3 position = previewCamera.position;
                position.y = Mathf.Lerp(position.y, desiredHeight, 1f - Mathf.Exp(-10f * Time.deltaTime));
                previewCamera.position = position;
                previewCamera.rotation = Quaternion.Euler(pitch, 0f, 0f);
            }
        }

        void OnGUI()
        {
            GUILayout.BeginArea(new Rect(16, 16, 320, 280), GUI.skin.box);
            GUILayout.Label("FIRST PERSON ANIMATION PREVIEW");
            GUILayout.Label("F: equip / unequip metal flashlight");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Idle")) { movement = 0f; sprint = false; }
            if (GUILayout.Button("Walk")) { movement = 0.6f; sprint = false; }
            if (GUILayout.Button("Sprint")) { movement = 1f; sprint = true; crouch = false; }
            GUILayout.EndHorizontal();
            crouch = GUILayout.Toggle(crouch, "Crouch");
            if (GUILayout.Button("Jump / land")) { airTime = 0.8f; crouch = false; }
            if (GUILayout.Button(character.RequestedTorchEquipped ? "Unequip flashlight" : "Equip flashlight")) character.ToggleTorch();
            GUILayout.Label("Look pitch: " + pitch.ToString("0") + " degrees");
            pitch = GUILayout.HorizontalSlider(pitch, -30f, 75f);
            GUILayout.Label(character.CurrentLegState + " / " + character.CurrentArmState);
            GUILayout.Label("Lower legs: " + (character.LegsVisible ? "visible" : "hidden for crouch / jump"));
            GUILayout.Label("Animation preview only; movement belongs to your controller.");
            GUILayout.EndArea();
        }
    }
}
