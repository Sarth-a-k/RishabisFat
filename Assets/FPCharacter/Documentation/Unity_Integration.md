# Unity flashlight setup

This revision uses a metal flashlight and separate below-knee legs. **Standing look-down views show only shins and feet. All legs disappear during crouching, jumping, stand-up, and landing recovery.** Arms and the equipped flashlight remain visible, with idle breathing and hand movement.

1. Import **FP_Flashlight.unitypackage**, or copy the supplied `Assets/FPCharacter` folder into your Unity project's `Assets` folder. This revision retains the earlier asset GUIDs and `Torch_*` clip/API names for compatibility.
2. If the prefab/controller has not been generated, choose **Tools > FP Character > Build or rebuild prefab**. The same command updates the shared material to the currently active Built-in, URP, or HDRP Lit shader after a pipeline change. The main asset was validated in the Unity version recorded in the validation reports.
3. Place **Assets/FPCharacter/Prefabs/FP_Character.prefab** under your existing player at the feet origin. Keep local position/rotation zero and scale one. This is one assembled character prefab, with one skeleton, one Animator, and three independently controlled skinned meshes: `FP_Arms`, `FP_LowerLegs`, and `Flashlight`.
4. Assign your existing player camera to **First Person Character Animator > View Camera**. The player owns yaw; the component follows camera pitch with the arms while keeping the legs upright. Set the camera near clip plane to about **0.025–0.04 m**. The reference composition uses an eye position of **(0, 1.64, −0.04) m**, about **2° down**, and **45.75° vertical FOV at 16:9** (matching the Blender 24 mm camera). Wider game FOVs make the hands appear smaller. Adjust your controller's standing eye height and crouch eye drop to match the included preview.
5. Press **F** to equip or unequip. Change the shortcut in the component, or disable **Read Toggle Key** and call `ToggleTorch()` from your existing input action. Both the new Input System and the legacy Input Manager are supported without requiring either package. When both are enabled, the new Input System takes precedence.

The prefab does not move the player, change your camera, add a second movement controller, or take ownership of WASD. Connect actual movement state using either method below.

## Connect your movement controller

Call once per frame, after your controller has calculated movement:

```csharp
// Assign the FirstPersonCharacterAnimator from the prefab in the Inspector.
public FPCharacter.FirstPersonCharacterAnimator visuals;

// In your existing controller's update, using your own actual state:
visuals.SetMotionState(
    Mathf.Clamp01(planarSpeed / runSpeed),
    isSprinting,
    isCrouching,
    isGrounded);

// Optional if View Camera is unassigned: Unity-positive X means looking down.
visuals.SetViewPitch(cameraPitchDegrees);

// Bind these only if Read Toggle Key is disabled:
visuals.ToggleTorch();
// Or request an explicit state:
visuals.SetTorchEquipped(true);
```

The toggle calls are examples of input callbacks, not calls to run every frame. The `Torch` API names now control the metal flashlight and remain compatible with the earlier version. Your controller remains responsible for collision, speed, jump physics, crouch capsule size, and camera height. Supply real grounded state so takeoff, landing, and leg visibility follow the game rather than a fixed timer.

For a controller based on Unity's `CharacterController`, you can instead add the optional **FPCharacterControllerAdapter**, assign the movement source and visual component, and tune the sprint and crouch thresholds. The adapter observes velocity, capsule height, and grounded state. It does not provide movement. Do not use both the adapter and your own `SetMotionState` calls.

## Leg visibility

`FP_LowerLegs` contains only shin and foot geometry. Normal camera culling determines when it enters the standing look-down view; there is no artificial camera-angle threshold.

- Standing idle, walking, and sprinting: lower legs are visible.
- Crouch input: lower legs hide immediately and remain hidden through crouch down, idle, walking, and the complete stand-up clip.
- Airborne input: lower legs hide immediately and remain hidden through takeoff, airborne motion, and the complete landing clip.
- Finishing stand-up or landing while grounded and not crouched: lower legs return.

The public `LegsVisible` property reports the current display state. Only the dedicated lower-leg renderer is switched; the skeleton continues animating, and the arms and flashlight are unaffected.

## Animation behavior

- Idle: standing hands and equipped hands use a four-second breathing and hand-motion loop rather than a frozen pose.
- Equip: the right arm reaches out of view while the inward-facing empty left hand makes a small support gesture. The right hand returns gripping the metal flashlight, then the left hand withdraws. The flashlight and spotlight appear at 1.35 seconds. The full equip clip is 2.1 seconds.
- Equipped: only the flashlight hand remains in the main view. Walking uses a restrained rhythmic movement, and sprinting uses greater motion. Fingers remain wrapped around the grip.
- Unequip: the right hand retracts to put away the flashlight; its mesh and spotlight switch off at 0.4 seconds; both hands return to their natural pose. The full clip is 1.6 seconds.
- Repeated toggle presses finish the current transition before resolving the final requested state.
- Lower-body clips provide idle, walk, sprint, crouch down/idle/walk/up, jump takeoff/airborne/landing. They continue independently during torch transitions. Root motion is disabled.
- After equipping or unequipping during movement, hand sway resumes at the current leg-stride phase. Crouch-walk hand playback speed follows the lower-body cycle length.

The lower layer owns the pelvis branch and the `upper_body` root position. The upper layer owns the descendants of `upper_body`. This preserves flashlight grip and hand animation while the hidden lower body continues through crouch/jump transitions. The rig is **Generic**, because this partial first-person body does not contain a full Humanoid skeleton.

## Preview and performance

Open **Assets/FPCharacter/Scenes/FP_Character_Preview.unity** and press Play. Buttons demonstrate the animation states, and the pitch slider shows the standing shins/feet and verifies that all legs disappear when crouched or airborne. If needed, save the current untitled scene and generate the demo using **Tools > FP Character > Create animation preview scene**. The preview component is present only in this demonstration scene.

The model uses one shared atlas material and three skinned renderers so the legs and flashlight can disappear independently. Animation compression uses key reduction; mesh Read/Write is disabled; there are no body shadow passes or physics components; only the local player's Animator should use this first-person asset. The flashlight has a neutral warm-white spotlight with **10 m range, a 50° cone, and no shadows**, enabled only while the flashlight mesh is visible. `torch_tip` and `torch_beam_target` align the beam with the lens. Disable **Enable Torch Light** if your game supplies its own illumination. Rendering cost still depends on the scene, platform, and effects.

The shared texture workflow is BaseColor plus MetallicGloss for Built-in/URP. HDRP uses the color atlas and a plain rough material by default; create a proper HDRP mask map if per-region smoothness is required. The material builder does not install or switch render pipelines.

## Verification

Asset and runtime reports record what was tested. **Tools > FP Character > Validate generated assets** checks the imported clip set and durations, upper-body transition endpoints, renderer count, atlas assignments, rig, and root-motion configuration. The batch Play Mode validator measures the left-hand support gesture relative to `upper_body`, independently of leg bob or camera pitch, and captures its pose near 0.9 seconds. The generated preview is the place to adjust field of view and your own camera/controller offsets for the final game.

Unity references used for integration: [Animator Controller](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Animations.AnimatorController.html), [transform Avatar Masks](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/AvatarMask.SetTransformPath.html), [fixed-time crossfades](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Animator.CrossFadeInFixedTime.html), [input backend compilation](https://github.com/Unity-Technologies/InputSystem/blob/develop/Packages/com.unity.inputsystem/Documentation~/enable-correct-input-system.md).
