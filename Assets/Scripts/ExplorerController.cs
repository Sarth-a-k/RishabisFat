using UnityEngine;

namespace SunkenPrism
{
    /// <summary>A compact, dependency-free first-person explorer using Unity's legacy input.</summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class ExplorerController : MonoBehaviour
    {
        public static ExplorerController Instance { get; private set; }
        public Camera View;
        public PuzzleInteractable Focus { get; private set; }
        public bool Paused { get; private set; }
        public bool JournalOpen { get; private set; }
        public bool MapOpen { get; private set; }
        public bool FlashlightOn { get { return flashlight != null && flashlight.enabled; } }
        public bool IsSprinting { get; private set; }
        public bool IsCrouching { get; private set; }
        public float Sensitivity = 2.0f;
        public Vector3 SpawnPosition { get; private set; }

        CharacterController motor;
        Light flashlight;
        float pitch;
        float fallSpeed;
        float stride;
        float eyeHeight = 1.64f;
        float jumpCooldown;
        bool cursorCaptured;
        GameObject lighterView;
        Material lighterMetal, lighterFuel;
        const float StandingHeight = 1.8f;
        const float CrouchingHeight = 1.12f;

        public static ExplorerController Build(Vector3 spawn, Transform parent)
        {
            var player = new GameObject("Artifact Hunter • First Person");
            player.layer = 2; // Never let interaction or stand-up queries hit the explorer's own capsule.
            if (parent != null) player.transform.SetParent(parent, false);
            player.transform.position = spawn;
            var cc = player.AddComponent<CharacterController>();
            cc.height = StandingHeight;
            cc.radius = .3f;
            cc.center = Vector3.up * .9f;
            cc.skinWidth = .025f;
            cc.stepOffset = .32f;
            cc.slopeLimit = 48f;
            cc.minMoveDistance = 0;
            var cameraObject = new GameObject("Explorer Camera");
            cameraObject.transform.SetParent(player.transform, false);
            cameraObject.transform.localPosition = Vector3.up * 1.64f;
            cameraObject.tag = "MainCamera";
            var view = cameraObject.AddComponent<Camera>();
            view.nearClipPlane = .06f;
            view.farClipPlane = 250f;
            view.fieldOfView = 72f;
            view.clearFlags = CameraClearFlags.SolidColor;
            view.backgroundColor = new Color(.012f, .02f, .027f);
            view.allowHDR = true;
            cameraObject.AddComponent<AudioListener>();
            var lampObject = new GameObject("Hand Lamp • visual light only");
            lampObject.transform.SetParent(cameraObject.transform, false);
            lampObject.transform.localPosition = new Vector3(.12f, -.12f, .08f);
            var lamp = lampObject.AddComponent<Light>();
            lamp.type = LightType.Spot;
            lamp.color = new Color(.86f, .94f, 1f);
            lamp.intensity = 1.25f;
            lamp.range = 10f;
            lamp.spotAngle = 58f;
            lamp.innerSpotAngle = 32f;
            lamp.shadows = LightShadows.None;
            lamp.enabled = false;
            var controller = player.AddComponent<ExplorerController>();
            controller.View = view;
            controller.flashlight = lamp;
            controller.SpawnPosition = spawn;
            return controller;
        }

        void Awake()
        {
            Instance = this;
            motor = GetComponent<CharacterController>();
            if (View == null) View = GetComponentInChildren<Camera>();
            if (flashlight == null && View != null) flashlight = View.GetComponentInChildren<Light>();
            SpawnPosition = transform.position;
            Time.timeScale = 1f;
        }

        void Start()
        {
            CaptureCursor(true);
            if (View != null) eyeHeight = View.transform.localPosition.y;
        }

        void Update()
        {
            if (!Application.isPlaying || View == null) return;
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (JournalOpen || MapOpen) { JournalOpen = false; MapOpen = false; CaptureCursor(!Paused); }
                else SetPaused(!Paused);
            }
            if (!Paused && Input.GetKeyDown(KeyCode.Tab))
            {
                JournalOpen = !JournalOpen;
                MapOpen = false;
                CaptureCursor(!JournalOpen);
            }
            if (!Paused && Input.GetKeyDown(KeyCode.M))
            {
                MapOpen = !MapOpen;
                JournalOpen = false;
                CaptureCursor(!MapOpen);
            }
            LunarPrismPuzzle lunar = LunarPrismPuzzle.Instance;
            if (Paused || JournalOpen || MapOpen)
            {
                Focus = null;
                IsSprinting = false;
                if (lunar != null) lunar.CancelIgnition();
                UpdateLighterView(false);
                return;
            }
            bool recaptured = Input.GetMouseButtonDown(0) && !cursorCaptured;
            if (recaptured) CaptureCursor(true);
            if (!cursorCaptured)
            {
                Focus = null;
                IsSprinting = false;
                if (lunar != null) lunar.CancelIgnition();
                UpdateLighterView(false);
                return;
            }

            float mouseX = Input.GetAxisRaw("Mouse X") * Sensitivity;
            float mouseY = Input.GetAxisRaw("Mouse Y") * Sensitivity;
            transform.Rotate(0f, mouseX, 0f);
            pitch = Mathf.Clamp(pitch - mouseY, -86f, 86f);
            View.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);

            Vector2 axes = Vector2.zero;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) axes.y += 1;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) axes.y -= 1;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) axes.x += 1;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) axes.x -= 1;
            SimulationStep(axes, Input.GetKey(KeyCode.LeftShift),
                Input.GetKey(KeyCode.C) || Input.GetKey(KeyCode.LeftControl),
                Input.GetKeyDown(KeyCode.Space), Time.deltaTime);

            Focus = null;
            if (Physics.Raycast(View.transform.position, View.transform.forward, out RaycastHit hit,
                4.5f, ~(1 << 2), QueryTriggerInteraction.Ignore))
                Focus = hit.collider.GetComponentInParent<PuzzleInteractable>();
            if (Focus != null)
            {
                if (Input.GetKeyDown(KeyCode.E)) Focus.Interact();
                if (Input.GetKeyDown(KeyCode.Q)) Focus.Rotate(-1);
                if (Input.GetKeyDown(KeyCode.R)) Focus.Rotate(1);
            }
            if (lunar != null)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) lunar.ToggleLighter();
                lunar.TickIgnition(this, !recaptured && Input.GetMouseButton(0), Time.deltaTime);
                UpdateLighterView(lunar.LighterEquipped);
            }
            if (Input.GetKeyDown(KeyCode.F) && flashlight != null) flashlight.enabled = !flashlight.enabled;
            if (PuzzleGame.Instance != null)
            {
                if (Input.GetKeyDown(KeyCode.G)) PuzzleGame.Instance.ResetCurrentPuzzle();
                if (Input.GetKeyDown(KeyCode.V)) PuzzleGame.Instance.ToggleVisor();
            }
            else if (CitadelDirector.Instance != null)
            {
                if (Input.GetKeyDown(KeyCode.G))
                {
                    if (lunar != null && CitadelDirector.RegionAt(transform.position) == 0) lunar.ResetPuzzle();
                    else CitadelDirector.Instance.ResetSurvey();
                }
                if (Input.GetKeyDown(KeyCode.V)) CitadelDirector.Instance.ToggleVisor();
            }
        }

        /// <summary>
        /// Advances the same collision, movement, gravity and jumping logic used by keyboard input.
        /// Tests may disable this behaviour to prevent Update and call this directly; the CharacterController stays enabled.
        /// Axes are local right/forward, jump is a press event, and dt is the simulation interval in seconds.
        /// </summary>
        public void SimulationStep(Vector2 axes, bool sprint, bool crouch, bool jump, float dt)
        {
            if (dt <= 0f || Paused) return;
            if (motor == null) motor = GetComponent<CharacterController>();
            if (motor == null || !motor.enabled) return;
            if (!crouch && IsCrouching)
            {
                // Check only the newly occupied portion above our own collider.
                var head = transform.position + Vector3.up * (motor.height - .16f);
                crouch = Physics.SphereCast(head, .22f, Vector3.up, out _,
                    Mathf.Max(0f, StandingHeight - motor.height), ~((1 << 2)), QueryTriggerInteraction.Ignore);
            }
            IsCrouching = crouch;
            float desiredHeight = IsCrouching ? CrouchingHeight : StandingHeight;
            float nextHeight = Mathf.MoveTowards(motor.height, desiredHeight, dt * 5f);
            if (!Mathf.Approximately(motor.height, nextHeight))
            {
                motor.height = nextHeight;
                motor.center = Vector3.up * (nextHeight * .5f);
            }

            axes = Vector2.ClampMagnitude(axes, 1f);
            IsSprinting = !IsCrouching && axes.y > 0 && sprint;
            float speed = IsCrouching ? 1.8f : IsSprinting ? 5.7f : 3.5f;
            Vector3 motion = (transform.right * axes.x + transform.forward * axes.y) * speed;
            if (motor.isGrounded && fallSpeed < 0) fallSpeed = -2.5f;
            jumpCooldown -= dt;
            if (motor.isGrounded && !IsCrouching && jump && jumpCooldown <= 0)
            {
                fallSpeed = Mathf.Sqrt(2f * 22f * 1.25f);
                jumpCooldown = .18f;
            }
            fallSpeed += -22f * dt;
            motion.y = fallSpeed;
            motor.Move(motion * dt);

            float walking = motor.isGrounded && axes.sqrMagnitude > .02f ? 1f : 0f;
            stride += dt * (IsSprinting ? 12f : 9f) * walking;
            float bob = Mathf.Sin(stride) * (IsSprinting ? .035f : .018f) * walking;
            eyeHeight = Mathf.Lerp(eyeHeight, motor.height - .16f, 1f - Mathf.Exp(-12f * dt));
            if (View != null)
            {
                View.transform.localPosition = new Vector3(0, eyeHeight + bob, 0);
                View.fieldOfView = Mathf.Lerp(View.fieldOfView, IsSprinting ? 76f : 72f, dt * 5f);
            }
            if (transform.position.y < -25f)
            {
                Teleport(SpawnPosition);
                if (PuzzleGame.Instance != null) PuzzleGame.Instance.Say("Gravity. Still the least imaginative trap down here.");
            }
        }

        public void SetPaused(bool paused)
        {
            Paused = paused;
            if (paused && LunarPrismPuzzle.Instance != null) LunarPrismPuzzle.Instance.CancelIgnition();
            Time.timeScale = paused ? 0f : 1f;
            CaptureCursor(!paused && !JournalOpen && !MapOpen);
        }

        public void CloseOverlays()
        {
            JournalOpen = false;
            MapOpen = false;
            CaptureCursor(!Paused);
        }

        public void Teleport(Vector3 feetPosition)
        {
            if (LunarPrismPuzzle.Instance != null) LunarPrismPuzzle.Instance.CancelIgnition();
            if (motor == null) motor = GetComponent<CharacterController>();
            motor.enabled = false;
            transform.position = feetPosition;
            fallSpeed = 0;
            motor.enabled = true;
        }

        void CaptureCursor(bool capture)
        {
            if (!Application.isPlaying) return;
            cursorCaptured = capture;
            if (!capture && LunarPrismPuzzle.Instance != null) LunarPrismPuzzle.Instance.CancelIgnition();
            Cursor.lockState = capture ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !capture;
        }

        // A tiny, unlit view model makes the inventory selection readable without adding a scene light.
        void UpdateLighterView(bool visible)
        {
            if (lighterView == null && visible && View != null)
            {
                lighterView = new GameObject("Inventory lighter / equipped with 1");
                lighterView.transform.SetParent(View.transform, false);
                lighterView.transform.localPosition = new Vector3(.26f, -.24f, .46f);
                lighterView.transform.localRotation = Quaternion.Euler(-8, -16, -7);
                Shader lighterShader = Shader.Find("Unlit/Color") ?? (Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                lighterMetal = new Material(lighterShader);
                lighterMetal.color = new Color(.47f, .38f, .23f);
                lighterFuel = new Material(lighterShader);
                lighterFuel.color = new Color(1f, .69f, .25f);
                if (lighterMetal.HasProperty("_EmissionColor"))
                {
                    lighterMetal.EnableKeyword("_EMISSION");
                    lighterMetal.SetColor("_EmissionColor", lighterMetal.color * .45f);
                    lighterFuel.EnableKeyword("_EMISSION");
                    lighterFuel.SetColor("_EmissionColor", lighterFuel.color);
                }
                LighterPart("Brass case", new Vector3(0, -.02f, 0), new Vector3(.075f, .105f, .037f), lighterMetal);
                LighterPart("Open hinged lid", new Vector3(-.059f, .032f, 0), new Vector3(.04f, .046f, .038f), lighterMetal);
                LighterPart("Wick guard", new Vector3(.009f, .049f, 0), new Vector3(.036f, .04f, .029f), lighterMetal);
                LighterPart("Small lighter flame", new Vector3(.01f, .077f, 0), new Vector3(.009f, .024f, .009f), lighterFuel);
            }
            if (lighterView != null && lighterView.activeSelf != visible) lighterView.SetActive(visible);
        }

        void LighterPart(string label, Vector3 position, Vector3 size, Material material)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = label;
            part.layer = 2;
            part.transform.SetParent(lighterView.transform, false);
            part.transform.localPosition = position;
            part.transform.localScale = size;
            Collider shape = part.GetComponent<Collider>();
            shape.enabled = false;
            Destroy(shape);
            var renderer = part.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        void OnApplicationFocus(bool focus)
        {
            if (!focus && Application.isPlaying) CaptureCursor(false);
        }

        void OnDisable()
        {
            if (!Application.isPlaying) return;
            CaptureCursor(false);
            UpdateLighterView(false);
            Time.timeScale = 1f;
        }

        void OnDestroy()
        {
            if (lighterMetal != null) Destroy(lighterMetal);
            if (lighterFuel != null) Destroy(lighterFuel);
            if (Instance == this) Instance = null;
            if (!Application.isPlaying) return;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Time.timeScale = 1f;
        }
    }
}
