using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace FPCharacter
{
    [RequireComponent(typeof(CharacterController))]
    public class FPCharacterMover : MonoBehaviour
    {
        public Transform viewCamera;
        public bool driveCameraPosition = true;

        public float walkSpeed = 2.5f;
        public float runSpeed = 5f;
        public float crouchSpeed = 1.4f;
        public float acceleration = 12f;
        public float jumpHeight = 1f;
        public float gravity = -20f;

        public float mouseSensitivity = 0.12f;
        public float minPitch = -80f;
        public float maxPitch = 80f;

        public float standingHeight = 1.8f;
        public float crouchHeight = 1.2f;
        public float standingEyeHeight = 1.64f;
        public float crouchEyeDrop = 0.36f;
        public float crouchBlendSpeed = 10f;

        [System.NonSerialized] public bool inputLocked;
        [System.NonSerialized] public float eyeHeightOffset;
        public bool IsSeated => inputLocked;
        public bool IsCrouching { get; private set; }
        public float CurrentSpeed { get; private set; }
        public bool IsGrounded => inputLocked || (controller != null && controller.enabled && controller.isGrounded);

        CharacterController controller;
        Vector3 horizontalVelocity;
        float verticalVelocity;
        float pitch;
        float eyeHeight;

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            controller.height = standingHeight;
            controller.center = new Vector3(0f, standingHeight * 0.5f, 0f);
            eyeHeight = standingEyeHeight;
        }

        void Start()
        {
            LockCursor(true);
        }

        void Update()
        {
            Vector2 move = ReadMove();
            Vector2 look = ReadLook();
            bool run = ReadRun();
            bool jump = ReadJump();
            bool crouch = ReadCrouch();

            if (ReadUnlock()) LockCursor(false);
            if (ReadClick() && Cursor.lockState != CursorLockMode.Locked) LockCursor(true);

            if (Cursor.lockState == CursorLockMode.Locked)
            {
                transform.Rotate(0f, look.x * mouseSensitivity, 0f);
                pitch = Mathf.Clamp(pitch - look.y * mouseSensitivity, minPitch, maxPitch);
            }

            if (inputLocked)
            {
                IsCrouching = false;
                horizontalVelocity = Vector3.zero;
                verticalVelocity = 0f;
                CurrentSpeed = 0f;
                eyeHeight = Mathf.Lerp(eyeHeight, standingEyeHeight + eyeHeightOffset, crouchBlendSpeed * Time.deltaTime);
                return;
            }

            IsCrouching = crouch;
            float targetHeight = IsCrouching ? crouchHeight : standingHeight;
            controller.height = Mathf.Lerp(controller.height, targetHeight, crouchBlendSpeed * Time.deltaTime);
            controller.center = new Vector3(0f, controller.height * 0.5f, 0f);

            float targetEye = IsCrouching ? standingEyeHeight - crouchEyeDrop : standingEyeHeight;
            eyeHeight = Mathf.Lerp(eyeHeight, targetEye, crouchBlendSpeed * Time.deltaTime);

            float speed = IsCrouching ? crouchSpeed : (run ? runSpeed : walkSpeed);
            Vector3 wishDir = transform.right * move.x + transform.forward * move.y;
            if (wishDir.sqrMagnitude > 1f) wishDir.Normalize();
            horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, wishDir * speed, acceleration * Time.deltaTime);

            if (controller.isGrounded)
            {
                if (verticalVelocity < 0f) verticalVelocity = -2f;
                if (jump && !IsCrouching) verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }
            verticalVelocity += gravity * Time.deltaTime;

            Vector3 velocity = horizontalVelocity + Vector3.up * verticalVelocity;
            controller.Move(velocity * Time.deltaTime);

            CurrentSpeed = new Vector3(controller.velocity.x, 0f, controller.velocity.z).magnitude;
        }

        void LateUpdate()
        {
            if (viewCamera == null) return;
            if (driveCameraPosition) viewCamera.position = transform.position + Vector3.up * eyeHeight;
            viewCamera.rotation = Quaternion.Euler(pitch, transform.eulerAngles.y, 0f);
        }

        void LockCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

#if ENABLE_INPUT_SYSTEM
        Vector2 ReadMove()
        {
            var k = Keyboard.current;
            if (k == null) return Vector2.zero;
            float x = (k.dKey.isPressed || k.rightArrowKey.isPressed ? 1f : 0f) - (k.aKey.isPressed || k.leftArrowKey.isPressed ? 1f : 0f);
            float y = (k.wKey.isPressed || k.upArrowKey.isPressed ? 1f : 0f) - (k.sKey.isPressed || k.downArrowKey.isPressed ? 1f : 0f);
            return new Vector2(x, y);
        }
        Vector2 ReadLook() => Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;
        bool ReadRun() => Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed;
        bool ReadJump() => Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
        bool ReadCrouch() => Keyboard.current != null && (Keyboard.current.leftCtrlKey.isPressed || Keyboard.current.cKey.isPressed);
        bool ReadUnlock() => Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
        bool ReadClick() => Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
#else
        Vector2 ReadMove() => new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        Vector2 ReadLook() => new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y")) * 10f;
        bool ReadRun() => Input.GetKey(KeyCode.LeftShift);
        bool ReadJump() => Input.GetKeyDown(KeyCode.Space);
        bool ReadCrouch() => Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.C);
        bool ReadUnlock() => Input.GetKeyDown(KeyCode.Escape);
        bool ReadClick() => Input.GetMouseButtonDown(0);
#endif
    }
}
