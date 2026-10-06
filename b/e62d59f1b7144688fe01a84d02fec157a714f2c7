using System;
using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace FPCharacter
{
    /// <summary>Animates the supplied single-rig view model. Your controller owns all movement and camera motion.</summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(150)]
    public sealed class FirstPersonCharacterAnimator : MonoBehaviour
    {
        [Header("Rig")]
        public Animator animator;
        public Transform upperBody;
        public Renderer[] torchRenderers;
        public Light torchLight;
        [Tooltip("Optional. Arms follow this camera's pitch while the legs stay upright. Never moves the camera.")]
        public Transform viewCamera;

        [Header("Lower legs")]
        [Tooltip("Only the separate below-knee mesh. Hidden throughout crouching, jumping, and their recovery clips.")]
        public Renderer[] legRenderers;

        [Header("Flashlight shortcut")]
        public bool readToggleKey = true;
        public KeyCode legacyToggleKey = KeyCode.F;
#if ENABLE_INPUT_SYSTEM
        public Key inputSystemToggleKey = Key.F;
#endif
        public bool enableTorchLight = true;
        [Min(0f)] public float equipRevealSeconds = 1.35f;
        [Min(0f)] public float unequipHideSeconds = 0.40f;
        [Range(0f, 0.3f)] public float crossFadeSeconds = 0.10f;

        public bool TorchEquipped => torchPhase == TorchPhase.Equipped;
        public bool TorchVisible { get; private set; }
        public bool LegsVisible { get; private set; }
        public bool TorchTransitioning => torchPhase == TorchPhase.Equipping || torchPhase == TorchPhase.Unequipping;
        public bool RequestedTorchEquipped => requestedTorch;
        public string CurrentLegState => lowerState;
        public string CurrentArmState => upperState;
        public event Action<bool> TorchVisibilityChanged;

        enum TorchPhase { Unequipped, Equipping, Equipped, Unequipping }
        TorchPhase torchPhase;
        bool requestedTorch, sprinting, crouching, grounded = true, wasGrounded = true;
        bool lowerWasCrouched;
        float movement, upperClock, lowerClock, explicitPitch;
        Quaternion upperBodyBindRotation;
        Vector3 unpitchedUpperLocalPosition;
        string lowerState, upperState;
        string syncedLowerState;
        static readonly int UpperCycleRate = Animator.StringToHash("UpperCycleRate");
        readonly Dictionary<string, float> lengths = new Dictionary<string, float>();
        readonly Dictionary<string, int> upperHashes = new Dictionary<string, int>();
        readonly Dictionary<string, int> lowerHashes = new Dictionary<string, int>();
        int lowerLayer, upperLayer;

        void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (animator == null || animator.runtimeAnimatorController == null)
            {
                Debug.LogError("FirstPersonCharacterAnimator requires the generated Animator Controller.", this);
                enabled = false;
                return;
            }
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            lowerLayer = animator.GetLayerIndex("Lower Body");
            upperLayer = animator.GetLayerIndex("Upper Body");
            if (lowerLayer < 0 || upperLayer < 0)
            {
                Debug.LogError("Missing Lower Body / Upper Body layers. Rebuild the FPCharacter prefab.", this);
                enabled = false;
                return;
            }
            animator.SetLayerWeight(upperLayer, 1f);
            foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
                lengths[clip.name] = clip.length;
            if (upperBody == null)
                foreach (Transform child in animator.GetComponentsInChildren<Transform>())
                    if (child.name == "upper_body") { upperBody = child; break; }
            if (upperBody != null)
            {
                upperBodyBindRotation = upperBody.localRotation;
                unpitchedUpperLocalPosition = upperBody.localPosition;
            }
            if (legRenderers == null || legRenderers.Length == 0)
            {
                var found = new List<Renderer>();
                foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
                    if (renderer.name == "FP_LowerLegs" || renderer.name.StartsWith("FP_LowerLegs.", StringComparison.Ordinal))
                        found.Add(renderer);
                legRenderers = found.ToArray();
            }
            SetTorchVisible(false, true);
            PlayLower("Legs_Idle", true);
            PlayUpper("Arms_Idle", true);
            UpdateLegVisibility(true);
        }

        /// <summary>Call every frame from the actual movement controller. Speed is normalized 0..1, grounded is actual contact.</summary>
        public void SetMotionState(float movement01, bool isSprinting, bool isCrouching, bool isGrounded)
        {
            movement = Mathf.Clamp01(movement01);
            sprinting = isSprinting && movement > 0.05f;
            crouching = isCrouching;
            grounded = isGrounded;
            UpdateLegVisibility(); // Input hides legs immediately, before the next Animator evaluation.
        }

        /// <summary>Optional pitch when viewCamera is unassigned. Positive degrees look down, matching Unity camera X rotation.</summary>
        public void SetViewPitch(float degrees) { explicitPitch = Mathf.Clamp(degrees, -70f, 75f); }
        public void ToggleTorch() { requestedTorch = !requestedTorch; }
        public void SetTorchEquipped(bool equipped) { requestedTorch = equipped; }

        void Update()
        {
            // Undo only last frame's view-model offset before Mecanim evaluates the next base pose.
            if (upperBody != null)
            {
                upperBody.localPosition = unpitchedUpperLocalPosition;
                upperBody.localRotation = upperBodyBindRotation;
            }
            if (readToggleKey && TogglePressed()) ToggleTorch();
            float delta = Time.deltaTime;
            lowerClock += delta;
            upperClock += delta;
            UpdateLegs();
            UpdateLegVisibility();
            UpdateArms();
            // Keep hand sway tied to footfalls even when crouch-walk and standing walk have different clip lengths.
            animator.SetFloat(UpperCycleRate, CanShareGait(upperState)
                ? ClipLength(upperState, 1f) / Mathf.Max(0.01f, ClipLength(lowerState, 1f)) : 1f);
            wasGrounded = grounded;
        }

        void LateUpdate()
        {
            if (upperBody == null) return;
            float pitch = explicitPitch;
            if (viewCamera != null)
            {
                Vector3 forward = transform.InverseTransformDirection(viewCamera.forward);
                pitch = Mathf.Atan2(-forward.y, new Vector2(forward.x, forward.z).magnitude) * Mathf.Rad2Deg;
            }
            // This branch owns translation only. Use its bind rotation so key reduction of constant
            // rotation curves cannot make camera pitch accumulate from one frame to the next.
            unpitchedUpperLocalPosition = upperBody.localPosition;
            Quaternion parentRotation = upperBody.parent != null ? upperBody.parent.rotation : Quaternion.identity;
            Quaternion aim = Quaternion.AngleAxis(Mathf.Clamp(pitch, -70f, 75f), transform.right);
            Vector3 pivot = viewCamera != null ? viewCamera.position
                : upperBody.position + transform.TransformVector(new Vector3(0f, 0.34f, -0.04f));
            // Orbit the shoulder branch around the eye so the sleeve cannot swing into the camera when looking down.
            upperBody.position = pivot + aim * (upperBody.position - pivot);
            upperBody.rotation = aim * parentRotation * upperBodyBindRotation;
        }

        bool TogglePressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current[inputSystemToggleKey].wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(legacyToggleKey);
#else
            return false; // Bind ToggleTorch() from your input action if both input backends are disabled.
#endif
        }

        void UpdateLegs()
        {
            if (!grounded)
            {
                if (wasGrounded) PlayLower("Legs_Jump_Start");
                else if (lowerState != "Legs_Jump_Start" || FinishedLower()) PlayLower("Legs_Jump_Loop");
                return;
            }
            if (!wasGrounded)
            {
                PlayLower("Legs_Jump_Land");
                lowerWasCrouched = false;
                return;
            }
            if (lowerState == "Legs_Jump_Land" && !FinishedLower()) return;
            if (crouching != lowerWasCrouched)
            {
                PlayLower(crouching ? "Legs_Crouch_Down" : "Legs_Crouch_Up");
                lowerWasCrouched = crouching;
                return;
            }
            if ((lowerState == "Legs_Crouch_Down" || lowerState == "Legs_Crouch_Up") && !FinishedLower()) return;
            PlayLower(crouching
                ? (movement > 0.05f ? "Legs_Crouch_Walk" : "Legs_Crouch_Idle")
                : (movement <= 0.05f ? "Legs_Idle" : sprinting ? "Legs_Sprint" : "Legs_Walk"));
        }

        void UpdateArms()
        {
            switch (torchPhase)
            {
                case TorchPhase.Unequipped:
                    if (requestedTorch)
                    {
                        torchPhase = TorchPhase.Equipping;
                        PlayUpper("Torch_Equip");
                    }
                    else PlayUpper(UpperLocomotion(false));
                    break;
                case TorchPhase.Equipping:
                    SetTorchVisible(upperClock >= Mathf.Min(equipRevealSeconds, ClipLength("Torch_Equip", 2.1f)));
                    if (upperClock >= ClipLength("Torch_Equip", 2.1f))
                    {
                        torchPhase = TorchPhase.Equipped;
                        SetTorchVisible(true);
                        PlayUpper(UpperLocomotion(true));
                    }
                    break;
                case TorchPhase.Equipped:
                    if (!requestedTorch)
                    {
                        torchPhase = TorchPhase.Unequipping;
                        PlayUpper("Torch_Unequip");
                    }
                    else PlayUpper(UpperLocomotion(true));
                    break;
                case TorchPhase.Unequipping:
                    SetTorchVisible(upperClock < unequipHideSeconds);
                    if (upperClock >= ClipLength("Torch_Unequip", 1.6f))
                    {
                        torchPhase = TorchPhase.Unequipped;
                        SetTorchVisible(false);
                        PlayUpper(UpperLocomotion(false));
                    }
                    break;
            }
            if (torchLight != null) torchLight.enabled = enableTorchLight && TorchVisible;
        }

        string UpperLocomotion(bool equipped)
        {
            if (!grounded || movement <= 0.05f) return equipped ? "Torch_Idle" : "Arms_Idle";
            if (sprinting && !crouching) return equipped ? "Torch_Sprint" : "Arms_Sprint";
            return equipped ? "Torch_Walk" : "Arms_Walk";
        }

        bool FinishedLower() => lowerClock >= ClipLength(lowerState, 0.35f);
        float ClipLength(string clip, float fallback) => lengths.TryGetValue(clip, out float value) ? value : fallback;

        void PlayLower(string state, bool immediate = false)
        {
            if (lowerState == state) return;
            lowerState = state;
            lowerClock = 0f;
            Play(state, lowerLayer, lowerHashes, immediate);
        }
        void PlayUpper(string state, bool immediate = false)
        {
            bool shareGait = CanShareGait(state);
            if (upperState == state && (!shareGait || syncedLowerState == lowerState)) return;
            upperState = state;
            upperClock = 0f;
            float offset = 0f;
            if (shareGait)
            {
                float phase = Mathf.Repeat(lowerClock / Mathf.Max(0.01f, ClipLength(lowerState, 1f)), 1f);
                offset = phase * ClipLength(state, 1f);
                syncedLowerState = lowerState;
            }
            else syncedLowerState = null;
            Play(state, upperLayer, upperHashes, immediate, offset);
        }
        bool CanShareGait(string state)
        {
            bool handLoop = state == "Arms_Walk" || state == "Arms_Sprint" || state == "Torch_Walk" || state == "Torch_Sprint";
            return handLoop && (lowerState == "Legs_Walk" || lowerState == "Legs_Sprint" || lowerState == "Legs_Crouch_Walk");
        }
        void Play(string state, int layer, Dictionary<string, int> hashes, bool immediate, float offsetSeconds = 0f)
        {
            if (!hashes.TryGetValue(state, out int hash))
            {
                hash = Animator.StringToHash(animator.GetLayerName(layer) + "." + state);
                hashes[state] = hash;
            }
            if (immediate) animator.Play(hash, layer, offsetSeconds / Mathf.Max(0.01f, ClipLength(state, 1f)));
            else animator.CrossFadeInFixedTime(hash, crossFadeSeconds, layer, offsetSeconds);
        }
        void SetTorchVisible(bool visible, bool force = false)
        {
            if (!force && TorchVisible == visible) return;
            TorchVisible = visible;
            if (torchRenderers != null)
                foreach (Renderer renderer in torchRenderers) if (renderer != null) renderer.enabled = visible;
            if (torchLight != null) torchLight.enabled = visible && enableTorchLight;
            TorchVisibilityChanged?.Invoke(visible);
        }
        void UpdateLegVisibility(bool force = false)
        {
            bool transitionHidesLegs = lowerState != null
                && (lowerState.StartsWith("Legs_Crouch", StringComparison.Ordinal)
                    || lowerState.StartsWith("Legs_Jump", StringComparison.Ordinal));
            bool visible = grounded && !crouching && !transitionHidesLegs;
            if (!force && LegsVisible == visible) return;
            LegsVisible = visible;
            if (legRenderers != null)
                foreach (Renderer renderer in legRenderers) if (renderer != null) renderer.enabled = visible;
        }
    }
}
