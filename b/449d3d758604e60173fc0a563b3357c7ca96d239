using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace FPCharacter
{
    [DefaultExecutionOrder(260)]
    public sealed class PlayerInteraction : MonoBehaviour
    {
        public FPCharacterMover mover;
        public FirstPersonCharacterAnimator character;
        public Transform viewCamera;
        public LighterView lighterPrefab;

        public float interactDistance = 2.6f;
        public float torchDistance = 2.6f;
        public float torchViewAngle = 40f;
        public float holdForward = 0.42f;
        public float holdDrop = 1.02f;
        public float viewClearance = 0.28f;
        public float holdSide = 0.12f;
        public float gripHeight = 0.8f;
        public float gripInset = 0.75f;
        public float placeDistance = 1.2f;
        public float rotateSpeed = 90f;
        public bool showPrompts = true;
        public float gripCurl = 0.7f;
        public float thumbCurl = 0.45f;
        public Vector3 lighterGripOffset = Vector3.zero;
        public bool restoreFlashlight = true;
        public GameObject gogglesViewPrefab;
        public bool hasGoggles;

        public bool IsBusy => busy;
        public MirrorPickup Carried => carried;

        Transform upperL, foreL, handL, upperR, foreR, handR;
        Transform[] knucklesL = new Transform[0];
        Transform[][] fingerChainsL = new Transform[0][];
        Transform[] thumbChainL = new Transform[0];
        Vector3 gripPoint;
        Transform[] trackedBones = new Transform[0];
        Quaternion[] preRotations = new Quaternion[0];
        Quaternion[] postRotations = new Quaternion[0];
        bool bonesModified, carryHadFlashlight;
        float reachL = 0.6f, reachR = 0.6f;
        LighterView lighter;
        MirrorPickup carried, lookMirror;
        RotatingRing lookRing;
        PlayerSeat lookSeat, seat;
        InfraredGogglesPickup lookGoggles;
        InfraredVision vision;
        GameObject gogglesView;
        bool gogglesOn, handAnim, gogglesVisible;
        float handW;
        float batonW;
        float pressW;
        Vector3 pressTarget;
        public static PlayerInteraction Instance { get; private set; }
        Vector3 bookGripL, bookGripR;
        float bookW, bookWL, bookWR;

        public void HoldBooks(Vector3 leftGrip, float leftWeight, Vector3 rightGrip, float rightWeight)
        {
            bookGripL = leftGrip;
            bookGripR = rightGrip;
            bookWL = Mathf.Clamp01(leftWeight);
            bookWR = Mathf.Clamp01(rightWeight);
            bookW = Mathf.Max(bookWL, bookWR);
        }

        public IEnumerator ClearHands()
        {
            UVBaton.PutAway();
            if (gogglesOn) yield return RemoveGoggles();
            if (FlashlightOut()) yield return PutAwayFlashlight();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetInstance() { Instance = null; }

        public float ReachToPress(Vector3 target, float reachTime, float holdTime)
        {
            if (upperR == null || handR == null) return 0f;
            StartCoroutine(PressRoutine(target, reachTime, holdTime));
            return reachTime;
        }

        IEnumerator PressRoutine(Vector3 target, float reachTime, float holdTime)
        {
            pressTarget = target;
            yield return Animate(reachTime, t => pressW = Smooth(t));
            yield return new WaitForSeconds(holdTime);
            yield return Animate(reachTime * 1.2f, t => pressW = 1f - Smooth(t));
            pressW = 0f;
        }

        Vector3 PressPoint()
        {
            Vector3 shoulder = upperR != null ? upperR.position : viewCamera.position;
            Vector3 to = pressTarget - shoulder;
            float d = Mathf.Min(to.magnitude - 0.02f, reachR * 0.97f);
            return shoulder + to.normalized * Mathf.Max(0.1f, d);
        }

        float prismW;

        Vector3 PrismRest()
        {
            return viewCamera.position + viewCamera.forward * 0.36f - viewCamera.right * 0.17f - viewCamera.up * 0.22f;
        }

        Vector3 BatonRest()
        {
            return viewCamera.position + viewCamera.forward * 0.34f - viewCamera.right * 0.13f - viewCamera.up * 0.17f;
        }
        Vector3 gogglesPos;
        Quaternion gogglesRot;
        string toast;
        float toastUntil;
        PuzzleTorch lookTorch, activeTorch;
        PuzzleTorch[] torches = new PuzzleTorch[0];
        readonly List<Collider> ownColliders = new List<Collider>();
        readonly RaycastHit[] hits = new RaycastHit[32];
        bool busy, lighterActive;
        float placeYaw, weightL, weightR, reachBlend, flick, carryBlend, tweenT;
        int tweenMode;
        Vector3 fromPos, toPos;
        Quaternion fromRot, toRot;
        string prompt;
        GUIStyle style;

        void Awake()
        {
            if (mover == null) mover = GetComponentInParent<FPCharacterMover>();
            if (mover == null) mover = FindFirstObjectByType<FPCharacterMover>();
            if (character == null) character = FindFirstObjectByType<FirstPersonCharacterAnimator>();
            if (viewCamera == null && mover != null) viewCamera = mover.viewCamera;
            if (viewCamera == null && Camera.main != null) viewCamera = Camera.main.transform;
            if (viewCamera == null)
            {
                Camera cam = FindFirstObjectByType<Camera>();
                if (cam != null) viewCamera = cam.transform;
            }

            Transform rig = character != null ? character.transform : transform;
            upperL = FindDeep(rig, "upper_arm.L");
            foreL = FindDeep(rig, "forearm.L");
            handL = FindDeep(rig, "hand.L");
            upperR = FindDeep(rig, "upper_arm.R");
            foreR = FindDeep(rig, "forearm.R");
            handR = FindDeep(rig, "hand.R");
            string[] fingers = { "f_index", "f_middle", "f_ring", "f_pinky" };
            var knuckles = new List<Transform>();
            var chains = new List<Transform[]>();
            foreach (string f in fingers)
            {
                Transform b1 = FindDeep(rig, f + ".01.L");
                Transform b2 = FindDeep(rig, f + ".02.L");
                Transform b3 = FindDeep(rig, f + ".03.L");
                if (b1 != null) knuckles.Add(b1);
                if (b1 != null && b2 != null && b3 != null) chains.Add(new[] { b1, b2, b3 });
            }
            knucklesL = knuckles.ToArray();
            fingerChainsL = chains.ToArray();
            Transform t1 = FindDeep(rig, "thumb.01.L");
            Transform t2 = FindDeep(rig, "thumb.02.L");
            Transform t3 = FindDeep(rig, "thumb.03.L");
            if (t1 != null && t2 != null && t3 != null) thumbChainL = new[] { t1, t2, t3 };
            var tracked = new List<Transform> { upperL, foreL, handL, upperR, foreR, handR };
            foreach (Transform[] chain in fingerChainsL) tracked.AddRange(chain);
            tracked.AddRange(thumbChainL);
            trackedBones = tracked.Where(t => t != null).Distinct().ToArray();
            preRotations = new Quaternion[trackedBones.Length];
            postRotations = new Quaternion[trackedBones.Length];
            if (upperL != null && foreL != null && handL != null)
                reachL = Vector3.Distance(upperL.position, foreL.position) + Vector3.Distance(foreL.position, handL.position);
            if (upperR != null && foreR != null && handR != null)
                reachR = Vector3.Distance(upperR.position, foreR.position) + Vector3.Distance(foreR.position, handR.position);

            Transform owner = mover != null ? mover.transform : transform;
            owner.GetComponentsInChildren(true, ownColliders);

            if (viewCamera != null)
            {
                vision = viewCamera.GetComponent<InfraredVision>();
                if (vision == null) vision = viewCamera.gameObject.AddComponent<InfraredVision>();
            }
            if (gogglesViewPrefab != null)
            {
                gogglesView = Instantiate(gogglesViewPrefab);
                gogglesView.name = "FP_Goggles";
                foreach (Collider c in gogglesView.GetComponentsInChildren<Collider>(true)) Destroy(c);
                foreach (Renderer r in gogglesView.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                gogglesView.SetActive(false);
            }
            if (lighterPrefab != null)
            {
                lighter = Instantiate(lighterPrefab);
                lighter.name = "FP_Lighter";
                lighter.gameObject.SetActive(false);
            }

            torches = FindObjectsByType<PuzzleTorch>(FindObjectsInactive.Exclude);
            foreach (PuzzleTorch t in torches) t.readInteractKey = false;
        }

        void Update()
        {
            if (viewCamera == null) return;
            if (bookW > 0f || GamePause.BlockInput) { prompt = null; return; }

            lookMirror = null;
            lookTorch = null;
            lookRing = null;
            lookSeat = null;
            lookGoggles = null;
            if (!busy && carried == null && seat == null) FindTargets();

            if (carried != null && !busy) placeYaw += RotateInput() * rotateSpeed * Time.deltaTime;
            if (carried == null && !busy && seat == null && lookMirror != null)
            {
                float spin = RotateInput();
                if (spin != 0f) lookMirror.transform.Rotate(0f, spin * rotateSpeed * 0.35f * Time.deltaTime, 0f, Space.World);
            }

            if (busy) prompt = null;
            else if (seat != null) prompt = "[E] Stand up";
            else if (lookSeat != null) prompt = "[E] Sit";
            else if (lookGoggles != null) prompt = "[E] Pick up " + lookGoggles.displayName;
            else if (carried != null) prompt = "[E] Place mirror      [Q] / [R] Rotate";
            else if (lookMirror != null) prompt = "[E] Pick up mirror      [Q] / [R] Rotate";
            else if (lookRing != null) prompt = "[Q] / [E] Rotate " + lookRing.displayName;
            else if (lookTorch != null) prompt = "[E] Light the torch";
            else prompt = null;

            if (!busy && carried == null && seat == null && hasGoggles && Digit1Pressed())
            {
                StartCoroutine(gogglesOn ? RemoveGoggles() : WearGoggles());
                return;
            }
            if (!busy && carried == null && lookGoggles != null && InteractPressed())
            {
                StartCoroutine(PickGogglesRoutine(lookGoggles));
                return;
            }
            if (!busy && seat != null)
            {
                if (InteractPressed()) StartCoroutine(StandRoutine());
                return;
            }
            if (!busy && carried == null && lookSeat != null && InteractPressed())
            {
                StartCoroutine(SitRoutine(lookSeat));
                return;
            }
            if (!busy && carried == null && lookRing != null)
            {
                if (QPressed()) lookRing.Step(-1);
                if (InteractPressed()) lookRing.Step(1);
                return;
            }
            if (busy || !InteractPressed()) return;
            if (carried != null) StartCoroutine(PlaceRoutine());
            else if (lookMirror != null) StartCoroutine(PickRoutine(lookMirror));
            else if (lookTorch != null) StartCoroutine(LightRoutine(lookTorch));
        }

        void LateUpdate()
        {
            if (viewCamera == null) return;
            RestoreUnanimatedBones();
            for (int i = 0; i < trackedBones.Length; i++) preRotations[i] = trackedBones[i].localRotation;
            UpdateCarriedPose();
            ApplyArms();
            UpdateLighterPose();
            UpdateGogglesView();
            for (int i = 0; i < trackedBones.Length; i++) postRotations[i] = trackedBones[i].localRotation;
            bonesModified = true;
        }

        void RestoreUnanimatedBones()
        {
            if (!bonesModified) return;
            for (int i = 0; i < trackedBones.Length; i++)
            {
                Transform b = trackedBones[i];
                if (b != null && Quaternion.Angle(preRotations[i], postRotations[i]) > 0.001f && Quaternion.Angle(b.localRotation, postRotations[i]) < 0.01f) b.localRotation = preRotations[i];
            }
        }

        bool FlashlightOut()
        {
            return character != null && (character.RequestedTorchEquipped || character.TorchVisible || character.TorchTransitioning);
        }

        IEnumerator PutAwayFlashlight()
        {
            if (character == null) yield break;
            character.SetTorchEquipped(false);
            float t = 0f;
            while ((character.TorchVisible || character.TorchTransitioning) && t < 4f)
            {
                t += Time.deltaTime;
                yield return null;
            }
            yield return new WaitForSeconds(0.1f);
        }

        void FindTargets()
        {
            Ray ray = new Ray(viewCamera.position, viewCamera.forward);
            int n = Physics.SphereCastNonAlloc(ray, 0.18f, hits, interactDistance + 0.3f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            Collider bestCollider = null;
            for (int i = 0; i < n; i++)
            {
                if (ownColliders.Contains(hits[i].collider)) continue;
                if (hits[i].distance <= 0f && hits[i].point == Vector3.zero) continue;
                if (hits[i].distance < best)
                {
                    best = hits[i].distance;
                    bestCollider = hits[i].collider;
                }
            }
            if (bestCollider != null)
            {
                MirrorPickup m = bestCollider.GetComponentInParent<MirrorPickup>();
                if (m != null && !m.IsCarried)
                {
                    lookMirror = m;
                    return;
                }
                InfraredGogglesPickup gp = bestCollider.GetComponentInParent<InfraredGogglesPickup>();
                if (gp != null)
                {
                    lookGoggles = gp;
                    return;
                }
                PlayerSeat ps = bestCollider.GetComponentInParent<PlayerSeat>();
                if (ps != null && !ps.Occupied && !NotePickup.Blocks(ps.transform))
                {
                    lookSeat = ps;
                    return;
                }
                RotatingRing ring = bestCollider.GetComponentInParent<RotatingRing>();
                if (ring != null && PrismPickup.Carried == null && !PrismSocket.AnyLooking)
                {
                    lookRing = ring;
                    return;
                }
            }

            if (AimAssist()) return;

            Vector3 feet = mover != null ? mover.transform.position : viewCamera.position;
            float bestAngle = torchViewAngle;
            foreach (PuzzleTorch t in torches)
            {
                if (t == null || t.IsLit) continue;
                Vector3 flat = t.transform.position - feet;
                flat.y = 0f;
                if (flat.magnitude > torchDistance) continue;
                float a1 = Vector3.Angle(viewCamera.forward, TorchPoint(t) - viewCamera.position);
                float a2 = Vector3.Angle(viewCamera.forward, t.transform.position + Vector3.up * 1.2f - viewCamera.position);
                float angle = Mathf.Min(a1, a2);
                if (angle < bestAngle)
                {
                    bestAngle = angle;
                    lookTorch = t;
                }
            }
        }

        IEnumerator LightRoutine(PuzzleTorch torch)
        {
            busy = true;
            bool hadFlashlight = FlashlightOut();
            if (hadFlashlight) yield return PutAwayFlashlight();
            activeTorch = torch;
            lighterActive = true;
            reachBlend = 0f;
            if (lighter != null)
            {
                lighter.gameObject.SetActive(true);
                lighter.ResetState();
            }
            yield return Animate(0.35f, t => weightL = Smooth(t));
            if (lighter != null) lighter.SetOpen(true);
            yield return Animate(0.16f, t => flick = Mathf.Sin(t * Mathf.PI));
            flick = 0f;
            yield return new WaitForSeconds(0.08f);
            if (lighter != null) lighter.SetLit(true);
            yield return new WaitForSeconds(0.35f);
            yield return Animate(0.5f, t => reachBlend = Smooth(t));
            torch.Ignite();
            yield return new WaitForSeconds(0.45f);
            yield return Animate(0.4f, t => reachBlend = 1f - Smooth(t));
            if (lighter != null) lighter.SetOpen(false);
            yield return new WaitForSeconds(0.07f);
            if (lighter != null) lighter.SetLit(false);
            yield return new WaitForSeconds(0.15f);
            yield return Animate(0.35f, t => weightL = 1f - Smooth(t));
            lighterActive = false;
            if (lighter != null) lighter.gameObject.SetActive(false);
            activeTorch = null;
            if (hadFlashlight && restoreFlashlight && character != null) character.SetTorchEquipped(true);
            busy = false;
        }

        IEnumerator SitRoutine(PlayerSeat target)
        {
            if (mover == null) yield break;
            busy = true;
            if (FlashlightOut()) yield return PutAwayFlashlight();
            seat = target;
            target.SetOccupied(true);
            CharacterController cc = mover.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            mover.inputLocked = true;
            Transform body = mover.transform;
            Vector3 p0 = body.position;
            Quaternion r0 = body.rotation;
            Vector3 p1 = target.SeatPosition;
            Quaternion r1 = target.FacingRotation;
            yield return Animate(0.9f, t =>
            {
                float s = Smooth(t);
                body.position = Vector3.Lerp(p0, p1, s);
                body.rotation = Quaternion.Slerp(r0, r1, Smooth(Mathf.Clamp01(t * 1.6f)));
                float sink = Smooth(Mathf.Clamp01(t * 1.3f - 0.2f));
                float settle = Mathf.Sin(Mathf.Clamp01(t * 1.3f - 0.2f) * Mathf.PI) * 0.04f;
                mover.eyeHeightOffset = target.seatedEyeOffset * sink - settle;
            });
            mover.eyeHeightOffset = target.seatedEyeOffset;
            busy = false;
        }

        IEnumerator StandRoutine()
        {
            if (mover == null || seat == null) yield break;
            busy = true;
            PlayerSeat s0 = seat;
            Transform body = mover.transform;
            Vector3 p0 = body.position;
            Vector3 p1 = s0.StandPosition;
            p1.y = p0.y;
            float start = mover.eyeHeightOffset;
            yield return Animate(0.75f, t =>
            {
                float lift = Smooth(Mathf.Clamp01(t * 1.4f));
                mover.eyeHeightOffset = Mathf.Lerp(start, 0f, lift) + Mathf.Sin(lift * Mathf.PI) * 0.05f;
                body.position = Vector3.Lerp(p0, p1, Smooth(Mathf.Clamp01(t * 1.2f - 0.15f)));
            });
            mover.eyeHeightOffset = 0f;
            mover.inputLocked = false;
            CharacterController cc = mover.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = true;
            s0.SetOccupied(false);
            seat = null;
            busy = false;
        }

        IEnumerator PickRoutine(MirrorPickup mirror)
        {
            busy = true;
            carryHadFlashlight = FlashlightOut();
            if (carryHadFlashlight) yield return PutAwayFlashlight();
            carried = mirror;
            mirror.SetCarried(true);
            fromPos = mirror.transform.position;
            fromRot = mirror.transform.rotation;
            placeYaw = 0f;
            tweenMode = 1;
            yield return Animate(0.5f, t =>
            {
                tweenT = t;
                carryBlend = Smooth(Mathf.Clamp01(t * 1.6f - 0.3f));
            });
            tweenMode = 0;
            carryBlend = 1f;
            busy = false;
        }

        IEnumerator PlaceRoutine()
        {
            busy = true;
            ComputePlacement(out toPos, out toRot);
            tweenMode = 2;
            yield return Animate(0.5f, t =>
            {
                tweenT = t;
                carryBlend = 1f - Smooth(Mathf.Clamp01(t * 1.5f - 0.4f));
            });
            MirrorPickup m = carried;
            carried = null;
            tweenMode = 0;
            carryBlend = 0f;
            if (m != null)
            {
                m.transform.SetPositionAndRotation(toPos, toRot);
                m.SetCarried(false);
            }
            if (carryHadFlashlight && restoreFlashlight && character != null) character.SetTorchEquipped(true);
            carryHadFlashlight = false;
            busy = false;
        }

        void ComputePlacement(out Vector3 position, out Quaternion rotation)
        {
            Vector3 f = FlatForward();
            Vector3 feet = mover != null ? mover.transform.position : viewCamera.position - Vector3.up * 1.6f;
            float dist = placeDistance;
            if (NearestHit(new Ray(viewCamera.position, f), placeDistance + 0.6f, out RaycastHit wall))
                dist = Mathf.Max(0.45f, Mathf.Min(dist, wall.distance - 0.45f));
            Vector3 p = feet + f * dist;
            float groundY = feet.y;
            Vector3 start = new Vector3(p.x, viewCamera.position.y + 0.3f, p.z);
            if (NearestHit(new Ray(start, Vector3.down), 4f, out RaycastHit ground)) groundY = ground.point.y;
            position = new Vector3(p.x, groundY, p.z);
            rotation = FacingRotation(f);
        }

        MirrorPickup[] assistMirrors = new MirrorPickup[0];
        InfraredGogglesPickup[] assistGoggles = new InfraredGogglesPickup[0];
        float assistRefreshAt;
        public float pickupAssistAngle = 16f;

        bool AimAssist()
        {
            if (Time.time >= assistRefreshAt)
            {
                assistRefreshAt = Time.time + 1.5f;
                assistMirrors = FindObjectsByType<MirrorPickup>(FindObjectsInactive.Exclude);
                assistGoggles = FindObjectsByType<InfraredGogglesPickup>(FindObjectsInactive.Exclude);
            }
            float best = pickupAssistAngle;
            MirrorPickup bm = null;
            InfraredGogglesPickup bg = null;
            foreach (MirrorPickup m in assistMirrors)
            {
                if (m == null || m.IsCarried) continue;
                float a = AssistAngle(m.transform);
                if (a < best) { best = a; bm = m; bg = null; }
            }
            foreach (InfraredGogglesPickup g in assistGoggles)
            {
                if (g == null || !g.gameObject.activeInHierarchy) continue;
                float a = AssistAngle(g.transform);
                if (a < best) { best = a; bg = g; bm = null; }
            }
            if (bm != null) { lookMirror = bm; return true; }
            if (bg != null) { lookGoggles = bg; return true; }
            return false;
        }

        float AssistAngle(Transform target)
        {
            Collider c = target.GetComponentInChildren<Collider>();
            Bounds b;
            if (c != null) b = c.bounds;
            else
            {
                Renderer r = target.GetComponentInChildren<Renderer>();
                if (r == null) return float.MaxValue;
                b = r.bounds;
            }
            Vector3 eye = viewCamera.position;
            Vector3 closest = b.ClosestPoint(eye);
            float dist = Vector3.Distance(eye, closest);
            if (dist > interactDistance + 0.4f) return float.MaxValue;
            float angle = Mathf.Min(Vector3.Angle(viewCamera.forward, b.center - eye), Vector3.Angle(viewCamera.forward, closest - eye));
            if (angle >= pickupAssistAngle) return float.MaxValue;
            Vector3 to = b.center - eye;
            int n = Physics.RaycastNonAlloc(new Ray(eye, to.normalized), hits, to.magnitude, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                Collider hc = hits[i].collider;
                if (ownColliders.Contains(hc) || hc.transform.IsChildOf(target)) continue;
                if (hits[i].distance < to.magnitude - 0.25f) return float.MaxValue;
            }
            return angle;
        }

        bool NearestHit(Ray ray, float distance, out RaycastHit result)
        {
            result = default;
            int n = Physics.RaycastNonAlloc(ray, hits, distance, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            bool found = false;
            for (int i = 0; i < n; i++)
            {
                if (ownColliders.Contains(hits[i].collider)) continue;
                if (carried != null && hits[i].collider.transform.IsChildOf(carried.transform)) continue;
                if (hits[i].distance < best)
                {
                    best = hits[i].distance;
                    result = hits[i];
                    found = true;
                }
            }
            return found;
        }

        Quaternion FacingRotation(Vector3 flatForward)
        {
            Vector3 glassDir = Quaternion.AngleAxis(placeYaw, Vector3.up) * -flatForward;
            Vector3 front = carried != null ? carried.FlatFront : Vector3.forward;
            return Quaternion.LookRotation(glassDir, Vector3.up) * Quaternion.Inverse(Quaternion.LookRotation(front, Vector3.up));
        }

        void UpdateCarriedPose()
        {
            if (carried == null) return;
            Vector3 f = FlatForward();
            Vector3 side = Vector3.Cross(Vector3.up, f).normalized;
            Vector3 holdPos = viewCamera.position + f * (holdForward + 0.06f) + Vector3.down * (holdDrop + viewClearance) + side * holdSide;
            Quaternion holdRot = FacingRotation(f);
            Vector3 pos = holdPos;
            Quaternion rot = holdRot;
            if (tweenMode == 1)
            {
                float s = Smooth(tweenT);
                pos = Vector3.Lerp(fromPos, holdPos, s) + Vector3.up * (Mathf.Sin(s * Mathf.PI) * 0.15f);
                rot = Quaternion.Slerp(fromRot, holdRot, s);
            }
            else if (tweenMode == 2)
            {
                float s = Smooth(tweenT);
                pos = Vector3.Lerp(holdPos, toPos, s) + Vector3.up * (Mathf.Sin(s * Mathf.PI) * 0.08f);
                rot = Quaternion.Slerp(holdRot, toRot, s);
            }
            carried.transform.SetPositionAndRotation(pos, rot);
        }

        void ApplyArms()
        {
            float dt = Time.deltaTime;
            if (Instance == null) Instance = this;
            if (pressW > 0f && upperR != null) Solve(upperR, foreR, handR, PressPoint(), Pole(upperR, 1f), pressW, reachR);
            if (bookW > 0f)
            {
                Solve(upperL, foreL, handL, bookGripL, Pole(upperL, -1f), bookWL, reachL);
                Solve(upperR, foreR, handR, bookGripR, Pole(upperR, 1f), bookWR, reachR);
                CurlFingers(GripCenter(), bookWL * 0.8f);
                return;
            }
            if (carried != null)
            {
                bool rightFree = character == null || !(character.RequestedTorchEquipped || character.TorchVisible || character.TorchTransitioning);
                weightL = carryBlend;
                weightR = Mathf.MoveTowards(weightR, rightFree ? carryBlend : 0f, dt * 5f);
                Vector3 a = carried.GripPoint(1f, gripHeight, gripInset);
                Vector3 b = carried.GripPoint(-1f, gripHeight, gripInset);
                bool aIsRight = Vector3.Dot(a - b, viewCamera.right) > 0f;
                Vector3 gripR = aIsRight ? a : b;
                Vector3 gripL = aIsRight ? b : a;
                Solve(upperL, foreL, handL, gripL, Pole(upperL, -1f), weightL, reachL);
                Solve(upperR, foreR, handR, gripR, Pole(upperR, 1f), weightR, reachR);
                return;
            }

            if (handAnim)
            {
                Vector3 c = gogglesPos;
                Vector3 right = gogglesRot * Vector3.right;
                Vector3 up = gogglesRot * Vector3.up;
                Vector3 gripL = c - right * 0.085f - up * 0.01f;
                Vector3 gripR = c + right * 0.085f - up * 0.01f;
                if (Vector3.Dot(gripR - gripL, viewCamera.right) < 0f) { Vector3 t = gripL; gripL = gripR; gripR = t; }
                Solve(upperL, foreL, handL, gripL, Pole(upperL, -1f), handW, reachL);
                Solve(upperR, foreR, handR, gripR, Pole(upperR, 1f), handW, reachR);
                return;
            }
            weightR = Mathf.MoveTowards(weightR, 0f, dt * 5f);

            PrismPickup held = PrismPickup.Carried;
            bool prismOut = held != null && !UVBaton.Active && !lighterActive && upperL != null && handL != null;
            prismW = Mathf.MoveTowards(prismW, prismOut ? 1f : 0f, dt * 4f);
            if (prismOut)
            {
                Solve(upperL, foreL, handL, PrismRest(), Pole(upperL, -1f), prismW, reachL);
                Vector3 g = GripCenter();
                CurlFingers(g, prismW * 0.75f);
                Vector3 off = viewCamera.right * held.handOffset.x + viewCamera.up * held.handOffset.y + viewCamera.forward * held.handOffset.z;
                held.transform.position = Vector3.Lerp(held.transform.position, g + off, prismW);
                held.transform.localScale = held.HandScaleValue;
                return;
            }

            bool batonOut = UVBaton.Active && !lighterActive;
            batonW = Mathf.MoveTowards(batonW, batonOut ? 1f : 0f, dt * 6f);
            if (batonOut && upperL != null && handL != null)
            {
                Solve(upperL, foreL, handL, BatonRest(), Pole(upperL, -1f), batonW, reachL);
                Vector3 g = GripCenter();
                CurlFingers(g, batonW);
                UVBaton.Hold(g, viewCamera);
                return;
            }

            if (lighterActive && upperL != null)
            {
                Vector3 rest = RestPoint();
                Vector3 target = activeTorch != null ? Vector3.Lerp(rest, ReachPoint(activeTorch), reachBlend) : rest;
                Solve(upperL, foreL, handL, target, Pole(upperL, -1f), weightL, reachL);
                if (flick > 0f && handL != null)
                    handL.rotation = Quaternion.AngleAxis(flick * -28f, viewCamera.forward) * handL.rotation;
                gripPoint = GripCenter();
                CurlFingers(gripPoint, weightL);
            }
        }

        void UpdateGogglesView()
        {
            if (gogglesView == null) return;
            if (gogglesView.activeSelf != gogglesVisible) gogglesView.SetActive(gogglesVisible);
            if (gogglesVisible) gogglesView.transform.SetPositionAndRotation(gogglesPos, gogglesRot);
        }

        Vector3 ChestPoint() => viewCamera.position + viewCamera.forward * 0.34f - viewCamera.up * 0.2f;
        Vector3 EyePoint() => viewCamera.position + viewCamera.forward * 0.045f;
        Quaternion FaceRot() => Quaternion.LookRotation(viewCamera.forward, viewCamera.up);

        IEnumerator PickGogglesRoutine(InfraredGogglesPickup pickup)
        {
            busy = true;
            Transform g = pickup.transform;
            Vector3 p0 = g.position;
            Quaternion r0 = g.rotation;
            gogglesVisible = false;
            handAnim = true;
            float reach = 0f;
            yield return Animate(0.45f, t =>
            {
                reach = Smooth(t);
                handW = reach;
                Vector3 shoulderMid = upperL != null && upperR != null ? (upperL.position + upperR.position) * 0.5f : viewCamera.position;
                Vector3 to = p0 - shoulderMid;
                gogglesPos = shoulderMid + to.normalized * Mathf.Min(to.magnitude, Mathf.Min(reachL, reachR) * 0.92f);
                gogglesRot = r0;
            });
            pickup.gameObject.SetActive(false);
            gogglesVisible = true;
            Vector3 grabbed = gogglesPos;
            yield return Animate(0.4f, t =>
            {
                float s = Smooth(t);
                gogglesPos = Vector3.Lerp(grabbed, ChestPoint(), s);
                gogglesRot = Quaternion.Slerp(r0, FaceRot(), s);
            });
            yield return new WaitForSeconds(0.2f);
            gogglesVisible = false;
            yield return Animate(0.3f, t => handW = 1f - Smooth(t));
            handAnim = false;
            hasGoggles = true;
            pickup.Collect();
            ShowToast("Infrared goggles  -  press [1] to wear / remove", 4f);
            busy = false;
        }

        IEnumerator WearGoggles()
        {
            busy = true;
            bool hadFlashlight = FlashlightOut();
            if (hadFlashlight) yield return PutAwayFlashlight();
            handAnim = true;
            gogglesVisible = true;
            yield return Animate(0.4f, t =>
            {
                handW = Smooth(t);
                gogglesPos = ChestPoint() - viewCamera.up * (0.15f * (1f - Smooth(t)));
                gogglesRot = FaceRot();
            });
            yield return Animate(0.5f, t =>
            {
                float s = Smooth(t);
                gogglesPos = Vector3.Lerp(ChestPoint(), EyePoint(), s);
                gogglesRot = FaceRot() * Quaternion.Euler(-12f * (1f - s), 0f, 0f);
            });
            gogglesVisible = false;
            gogglesOn = true;
            if (vision != null) vision.SetActive(true);
            yield return Animate(0.35f, t =>
            {
                handW = 1f - Smooth(t);
                gogglesPos = EyePoint() - viewCamera.up * 0.12f * Smooth(t);
                gogglesRot = FaceRot();
            });
            handAnim = false;
            if (hadFlashlight && restoreFlashlight && character != null) character.SetTorchEquipped(true);
            busy = false;
        }

        IEnumerator RemoveGoggles()
        {
            busy = true;
            bool hadFlashlight = FlashlightOut();
            if (hadFlashlight) yield return PutAwayFlashlight();
            handAnim = true;
            yield return Animate(0.35f, t =>
            {
                handW = Smooth(t);
                gogglesPos = EyePoint() - viewCamera.up * 0.12f * (1f - Smooth(t));
                gogglesRot = FaceRot();
            });
            gogglesVisible = true;
            gogglesOn = false;
            if (vision != null) vision.SetActive(false);
            yield return Animate(0.5f, t =>
            {
                float s = Smooth(t);
                gogglesPos = Vector3.Lerp(EyePoint(), ChestPoint(), s);
                gogglesRot = FaceRot() * Quaternion.Euler(-12f * s, 0f, 0f);
            });
            yield return Animate(0.35f, t =>
            {
                handW = 1f - Smooth(t);
                gogglesPos = ChestPoint() - viewCamera.up * 0.2f * Smooth(t);
            });
            gogglesVisible = false;
            handAnim = false;
            if (hadFlashlight && restoreFlashlight && character != null) character.SetTorchEquipped(true);
            busy = false;
        }

        void ShowToast(string message, float seconds)
        {
            toast = message;
            toastUntil = Time.time + seconds;
        }

        bool Digit1Pressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.digit1Key.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.Alpha1);
#endif
        }

        void UpdateLighterPose()
        {
            if (lighter == null || !lighterActive) return;
            Quaternion rot = Quaternion.LookRotation(-viewCamera.forward, viewCamera.up) * Quaternion.Euler(0f, 25f, 0f);
            Vector3 center = handL != null ? gripPoint : RestPoint();
            if (activeTorch != null && reachBlend > 0f)
            {
                Vector3 to = (TorchPoint(activeTorch) - center).normalized;
                rot = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(viewCamera.up, to), reachBlend * 0.5f) * rot;
            }
            Vector3 offset = viewCamera.right * lighterGripOffset.x + viewCamera.up * lighterGripOffset.y + viewCamera.forward * lighterGripOffset.z;
            Vector3 pos = center - rot * Vector3.up * 0.024f + offset;
            lighter.transform.SetPositionAndRotation(pos, rot);
        }

        Vector3 GripCenter()
        {
            if (handL == null) return RestPoint();
            Vector3 knuckles = Vector3.zero;
            int count = 0;
            foreach (Transform k in knucklesL)
            {
                if (k == null) continue;
                knuckles += k.position;
                count++;
            }
            if (count == 0) return handL.position;
            knuckles /= count;
            if (thumbChainL.Length == 3) return Vector3.Lerp(knuckles, thumbChainL[2].position, 0.5f);
            return knuckles;
        }

        void CurlFingers(Vector3 target, float weight)
        {
            if (weight <= 0f) return;
            foreach (Transform[] chain in fingerChainsL)
            {
                BendToward(chain[0], chain[1], target, gripCurl * weight);
                BendToward(chain[1], chain[2], target, gripCurl * weight);
            }
            if (thumbChainL.Length == 3)
            {
                BendToward(thumbChainL[0], thumbChainL[1], target, thumbCurl * weight);
                BendToward(thumbChainL[1], thumbChainL[2], target, thumbCurl * weight);
            }
        }

        static void BendToward(Transform bone, Transform child, Vector3 target, float amount)
        {
            if (bone == null || child == null) return;
            Vector3 dir = child.position - bone.position;
            Vector3 to = target - bone.position;
            if (dir.sqrMagnitude < 1e-8f || to.sqrMagnitude < 1e-8f) return;
            bone.rotation = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(dir, to), Mathf.Clamp01(amount)) * bone.rotation;
        }

        Vector3 RestPoint()
        {
            return viewCamera.position + viewCamera.forward * 0.3f - viewCamera.right * 0.08f - viewCamera.up * 0.15f;
        }

        Vector3 ReachPoint(PuzzleTorch torch)
        {
            Vector3 shoulder = upperL != null ? upperL.position : viewCamera.position;
            Vector3 to = TorchPoint(torch) + Vector3.down * 0.05f - shoulder;
            float d = Mathf.Min(to.magnitude - 0.08f, reachL * 0.97f);
            return shoulder + to.normalized * Mathf.Max(0.1f, d);
        }

        Vector3 Pole(Transform shoulder, float side)
        {
            Vector3 origin = shoulder != null ? shoulder.position : viewCamera.position;
            return origin - viewCamera.up * 0.6f + viewCamera.right * (0.5f * side) - viewCamera.forward * 0.1f;
        }

        static Vector3 TorchPoint(PuzzleTorch t)
        {
            return t.flameSocket != null ? t.flameSocket.position : t.BeamOrigin;
        }

        Vector3 FlatForward()
        {
            Vector3 f = viewCamera.forward;
            f.y = 0f;
            if (f.sqrMagnitude < 1e-6f) f = mover != null ? mover.transform.forward : Vector3.forward;
            return f.normalized;
        }

        static void Solve(Transform a, Transform b, Transform c, Vector3 target, Vector3 pole, float weight, float reach)
        {
            if (a == null || b == null || c == null || weight <= 0f) return;
            Quaternion aLocal = a.localRotation;
            Quaternion bLocal = b.localRotation;
            Vector3 pa = a.position, pb = b.position, pc = c.position;
            float lab = Vector3.Distance(pa, pb);
            float lbc = Vector3.Distance(pb, pc);
            if (lab < 1e-4f || lbc < 1e-4f) return;
            Vector3 toTarget = target - pa;
            float lat = Mathf.Clamp(toTarget.magnitude, Mathf.Abs(lab - lbc) + 1e-3f, Mathf.Min(reach, lab + lbc) - 1e-3f);
            float cosB = Mathf.Clamp((lab * lab + lbc * lbc - lat * lat) / (2f * lab * lbc), -1f, 1f);
            float elbow = Mathf.Acos(cosB) * Mathf.Rad2Deg;
            Vector3 ba = (pa - pb).normalized;
            Vector3 bc = (pc - pb).normalized;
            Vector3 n = Vector3.Cross(ba, bc);
            if (n.sqrMagnitude < 1e-8f) n = Vector3.Cross(toTarget, pole - pa);
            if (n.sqrMagnitude < 1e-8f) n = Vector3.up;
            n.Normalize();
            Vector3 v1 = Quaternion.AngleAxis(elbow, n) * ba;
            Vector3 v2 = Quaternion.AngleAxis(-elbow, n) * ba;
            Vector3 want = Vector3.Dot(v1, bc) >= Vector3.Dot(v2, bc) ? v1 : v2;
            b.rotation = Quaternion.FromToRotation(bc, want) * b.rotation;
            pc = c.position;
            if ((pc - pa).sqrMagnitude > 1e-8f && toTarget.sqrMagnitude > 1e-8f)
                a.rotation = Quaternion.FromToRotation(pc - pa, toTarget) * a.rotation;
            Vector3 axis = toTarget.normalized;
            Vector3 elbowDir = Vector3.ProjectOnPlane(b.position - pa, axis);
            Vector3 poleDir = Vector3.ProjectOnPlane(pole - pa, axis);
            if (elbowDir.sqrMagnitude > 1e-8f && poleDir.sqrMagnitude > 1e-8f)
                a.rotation = Quaternion.AngleAxis(Vector3.SignedAngle(elbowDir, poleDir, axis), axis) * a.rotation;
            if (weight < 1f)
            {
                a.localRotation = Quaternion.Slerp(aLocal, a.localRotation, weight);
                b.localRotation = Quaternion.Slerp(bLocal, b.localRotation, weight);
            }
        }

        static IEnumerator Animate(float duration, Action<float> step)
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                step(Mathf.Clamp01(t / duration));
                yield return null;
            }
            step(1f);
        }

        static float Smooth(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        static Transform FindDeep(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform r = FindDeep(root.GetChild(i), name);
                if (r != null) return r;
            }
            return null;
        }

        bool InteractPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.E);
#endif
        }

        bool QPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.Q);
#endif
        }

        float RotateInput()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard k = Keyboard.current;
            if (k == null) return 0f;
            return (k.rKey.isPressed ? 1f : 0f) - (k.qKey.isPressed ? 1f : 0f);
#else
            return (Input.GetKey(KeyCode.R) ? 1f : 0f) - (Input.GetKey(KeyCode.Q) ? 1f : 0f);
#endif
        }

        void OnGUI()
        {
            if (!showPrompts || viewCamera == null) return;
            if (style == null)
            {
                style = new GUIStyle(GUI.skin.label);
                style.alignment = TextAnchor.MiddleCenter;
                style.fontStyle = FontStyle.Bold;
            }
            style.fontSize = Mathf.Max(14, Screen.height / 34);
            float cx = Screen.width * 0.5f;
            float cy = Screen.height * 0.5f;
            Color old = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.75f);
            GUI.DrawTexture(new Rect(cx - 2f, cy - 2f, 4f, 4f), Texture2D.whiteTexture);
            GUI.color = old;
            if (!string.IsNullOrEmpty(toast) && Time.time < toastUntil)
            {
                Rect tr = new Rect(0f, Screen.height * 0.74f, Screen.width, style.fontSize * 2f);
                style.normal.textColor = new Color(0f, 0f, 0f, 0.8f);
                GUI.Label(new Rect(tr.x + 2f, tr.y + 2f, tr.width, tr.height), toast, style);
                style.normal.textColor = new Color(1f, 0.85f, 0.6f);
                GUI.Label(tr, toast, style);
            }
            if (string.IsNullOrEmpty(prompt)) return;
            PromptBox.Draw(prompt, 0.66f);
        }
    }
}
