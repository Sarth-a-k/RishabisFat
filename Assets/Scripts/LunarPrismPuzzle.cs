using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace SunkenPrism
{
    /// <summary>The Sunken Prism's portable mirrors, directed torch and three lunar seals.
    /// Beams use mirror-plane reflections and solid-world occlusion, independent of their visual effects.</summary>
    public sealed class LunarPrismPuzzle : MonoBehaviour
    {
        public enum MirrorShape { Rectangle, Oval, Circle }
        public enum MoonPhase { White, GreenCrescent, BlueHalf, RedFull }

        public static LunarPrismPuzzle Instance { get; private set; }
        public Transform TorchRoot, TorchOrigin;
        public GameObject TorchFlame;
        public Light TorchLight;
        public Transform[] MirrorRoots = new Transform[3];
        public Transform[] MirrorSockets = new Transform[3];
        public Transform PrismVisual, PrismOrigin;
        public Renderer PrismRenderer;
        public Light PrismLight;
        public SpriteRenderer Moon;
        public Renderer MoonHalo;
        public Light MoonLight;
        public Sprite[] MoonPhases = new Sprite[4];
        public Renderer[] LeftRune, UpperRune, RightRune;
        public Transform[] EmberGates = new Transform[1];
        public Material BeamMaterial;
        public float GateRaiseHeight = 8.5f;
        public Vector2 MirrorHalfSize = new Vector2(.65f, .9f);
        public float IgnitionSeconds = 2.5f;

        public bool LighterEquipped { get; private set; }
        public bool TorchLit { get; private set; }
        public bool Powered { get; private set; }
        public bool Complete { get; private set; }
        public bool Solved { get { return Complete; } }
        public MoonPhase Phase { get; private set; }
        public float IgnitionProgress01 { get { return TorchLit ? 1f : Mathf.Clamp01(ignition / IgnitionSeconds); } }
        public float IgnitionProgress { get { return IgnitionProgress01; } }
        public string HeldMirrorName { get { return held < 0 ? "" : ShapeName(held); } }
        public string HeldShape { get { return HeldMirrorName; } }
        public int PlacedMirrorCount { get { int count = 0; for (int i = 0; i < 3; i++) if (placed[i]) count++; return count; } }
        public int AlignedMirrorCount { get; private set; }
        public int ActivatedSymbolCount { get { int count = 0; for (int i = 0; i < 3; i++) if (activated[i]) count++; return count; } }
        public int ActivatedCount { get { return ActivatedSymbolCount; } }
        public bool MoonBeamReachesTarget { get; private set; }
        public string Objective
        {
            get
            {
                if (Complete) return "The three lunar seals are awake. Enter the Ember Crypt through the eastern passage.";
                if (!TorchLit) return "Equip the lighter [1], aim at the wooden corner torch, and hold LMB for 2.5 seconds.";
                if (held >= 0) return "Carry the " + ShapeName(held).ToLowerInvariant() + " mirror to its matching shadow. Press E to install it.";
                if (PlacedMirrorCount < 3) return "Find the three loose mirrors. E picks up a mirror and installs it in the matching shadow.";
                if (!Powered) return "Q rotates each installed mirror. Guide the beam: rectangle → oval → circle → prism.";
                return "Q rotates the prism: green crescent → blue half moon → red full moon. Awaken all three seals.";
            }
        }

        readonly bool[] placed = new bool[3];
        readonly bool[] activated = new bool[3];
        readonly int[] detents = { 2, 3, 1 };
        readonly int[] initialDetents = { 2, 3, 1 };
        readonly Quaternion[] solutionRotations = new Quaternion[3];
        readonly Vector3[] spawnPositions = new Vector3[3];
        readonly Vector3[] spawnScales = new Vector3[3];
        readonly Quaternion[] spawnRotations = new Quaternion[3];
        readonly Transform[] spawnParents = new Transform[3];
        readonly Collider[][] mirrorColliders = new Collider[3][];
        readonly PrismInteractable[] mirrorControls = new PrismInteractable[3];
        readonly LineRenderer[] beamSegments = new LineRenderer[6];
        readonly RaycastHit[] rayHits = new RaycastHit[64];
        MaterialPropertyBlock propertyBlock;
        readonly Color[] phaseColors = { Color.white, new Color(.16f, 1f, .28f), new Color(.18f, .42f, 1f), new Color(1f, .14f, .08f) };
        Vector3[] closedGatePositions;
        Collider[][] gateColliders;
        Renderer[][] runes;
        LineRenderer moonBeam;
        PrismInteractable torchControl;
        Material generatedBeamMaterial;
        Color prismEmission;
        Quaternion initialPrismRotation;
        float prismIntensity, torchIntensity, ignition, nextBeamRefresh;
        int held = -1;
        int lastSurfaceState = -1;
        bool initialized;

        void Awake() { Instance = this; propertyBlock = new MaterialPropertyBlock(); }
        void Start() { Initialize(); }

        public void Initialize()
        {
            if (initialized) return;
            if (propertyBlock == null) propertyBlock = new MaterialPropertyBlock();
            if (TorchRoot == null || TorchOrigin == null || PrismVisual == null || PrismOrigin == null || Moon == null
                || MirrorRoots == null || MirrorRoots.Length != 3 || MirrorSockets == null || MirrorSockets.Length != 3)
            { Debug.LogError("The lunar puzzle is missing its serialized scene references.", this); return; }
            for (int i = 0; i < 3; i++) if (MirrorRoots[i] == null || MirrorSockets[i] == null) return;
            initialized = true;
            runes = new[] { LeftRune, UpperRune, RightRune };
            EnablePuzzleEmission(PrismRenderer);
            foreach (Renderer[] seal in runes)
                if (seal != null) foreach (Renderer stroke in seal) EnablePuzzleEmission(stroke);
            prismEmission = PrismRenderer != null && PrismRenderer.sharedMaterial != null
                && PrismRenderer.sharedMaterial.HasProperty("_EmissionColor")
                ? PrismRenderer.sharedMaterial.GetColor("_EmissionColor") : Color.white * .2f;
            prismIntensity = PrismLight != null ? PrismLight.intensity : 0f;
            initialPrismRotation = PrismVisual.rotation;
            torchIntensity = TorchLight != null ? TorchLight.intensity : 0f;
            torchControl = Control(TorchRoot);
            torchControl.Text = () => TorchLit ? "THE KINDLED TORCH\nFollow its beam to the rectangular shadow."
                : LighterEquipped ? "Hold LMB  /  Ignite the wooden torch (2.5 s)" : "1  Equip your lighter\nAim here and hold LMB to kindle the torch.";
            torchControl.Use = () => Say(TorchLit ? "A remarkably elaborate way to illuminate a room." : "My lighter should do the trick. One steady flame.");
            for (int i = 0; i < 3; i++)
            {
                int index = i;
                Transform mirror = MirrorRoots[i];
                spawnPositions[i] = mirror.position;
                spawnRotations[i] = mirror.rotation;
                spawnScales[i] = mirror.localScale;
                spawnParents[i] = mirror.parent;
                mirrorColliders[i] = mirror.GetComponentsInChildren<Collider>(true);
                Vector3 previous = i == 0 ? TorchOrigin.position : MirrorSockets[i - 1].position;
                Vector3 next = i == 2 ? PrismOrigin.position : MirrorSockets[i + 1].position;
                Vector3 incoming = (MirrorSockets[i].position - previous).normalized;
                Vector3 outgoing = (next - MirrorSockets[i].position).normalized;
                Vector3 normal = (incoming - outgoing).normalized;
                solutionRotations[i] = Quaternion.LookRotation(normal, Vector3.up);
                mirrorControls[i] = Control(mirror);
                mirrorControls[i].Text = () => placed[index]
                    ? ShapeName(index).ToUpperInvariant() + " MIRROR\nQ / R  Rotate   •   E  Pick up"
                    : "E  Pick up the " + ShapeName(index).ToLowerInvariant() + " mirror";
                mirrorControls[i].Use = () => TryPickUpMirror(index);
                mirrorControls[i].Turn = direction => RotateMirror(index, direction);
                var socketControl = Control(MirrorSockets[i]);
                socketControl.Text = () => placed[index]
                    ? ShapeName(index).ToUpperInvariant() + " MIRROR\nQ / R  Rotate   •   E  Pick up"
                    : held == index ? "E  Place the " + ShapeName(index).ToLowerInvariant() + " mirror in its shadow"
                    : held >= 0 ? "This shadow requires a " + ShapeName(index).ToLowerInvariant() + " mirror"
                    : "EMPTY " + ShapeName(index).ToUpperInvariant() + " SHADOW\nFind a mirror with the same outline.";
                socketControl.Use = () => { if (placed[index] && held < 0) TryPickUpMirror(index); else TryPlaceMirror(index); };
                socketControl.Turn = direction => RotateMirror(index, direction);
            }
            var prismControl = Control(PrismVisual);
            prismControl.Text = () => Powered ? "THE LUNAR PRISM\nQ  Next color   •   R  Previous color\nWhite → green → blue → red"
                : "THE DORMANT PRISM\nGuide the torch through all three mirrors to awaken it.";
            prismControl.Turn = direction => RotatePrism(direction);
            prismControl.Use = () => Say(Powered ? "A prism that appraises moons. Finally, some craftsmanship." : "Still sleeping. Rectangle, oval, circle... then the crystal.");
            if (BeamMaterial == null)
            {
                generatedBeamMaterial = new Material(Shader.Find("Sprites/Default"));
                generatedBeamMaterial.name = "Lunar puzzle • shared beam material";
                BeamMaterial = generatedBeamMaterial;
            }
            for (int i = 0; i < beamSegments.Length; i++) beamSegments[i] = MakeBeam("Torch reflection " + i);
            moonBeam = MakeBeam("Prism → lunar relief");
            closedGatePositions = new Vector3[EmberGates.Length];
            gateColliders = new Collider[EmberGates.Length][];
            for (int i = 0; i < EmberGates.Length; i++)
            {
                if (EmberGates[i] == null) continue;
                closedGatePositions[i] = EmberGates[i].position;
                gateColliders[i] = EmberGates[i].GetComponentsInChildren<Collider>(true);
                var gate = Control(EmberGates[i]);
                gate.Text = () => Complete ? "THE EMBER CRYPT\nThe three lunar seals have released this door."
                    : "THE EMBER CRYPT IS SEALED\nAwaken the green, blue and red lunar symbols in the Sunken Prism.";
                gate.Use = () => Say(Complete ? "At last. A door that knows when to get out of my way." : "Three lunar seals. The answer is in the western hall.");
            }
            ResetPuzzle();
        }

        void Update()
        {
            if (!initialized) return;
            // All player actions trace immediately. A low-rate background trace catches changed
            // scenery without paying for raycasts or material updates every rendered frame.
            if (Time.time >= nextBeamRefresh)
            {
                RefreshBeams();
                nextBeamRefresh = Time.time + .1f;
            }
            if (!Complete) return;
            for (int i = 0; i < EmberGates.Length; i++)
            {
                if (EmberGates[i] == null) continue;
                Vector3 target = closedGatePositions[i] + Vector3.up * GateRaiseHeight;
                EmberGates[i].position = Vector3.MoveTowards(EmberGates[i].position, target, 2.8f * Time.deltaTime);
                bool cleared = EmberGates[i].position.y - closedGatePositions[i].y >= GateRaiseHeight - .05f;
                if (gateColliders[i] != null)
                    for (int c = 0; c < gateColliders[i].Length; c++) if (gateColliders[i][c] != null) gateColliders[i][c].enabled = !cleared;
            }
        }

        public void ToggleLighter()
        {
            if (held >= 0) { Say("First, put this mirror in its matching shadow."); return; }
            LighterEquipped = !LighterEquipped;
            CancelIgnition();
        }

        public void TickIgnition(ExplorerController player, bool triggerHeld, float dt)
        {
            bool aiming = player != null && player.Focus == torchControl && !player.Paused && !player.JournalOpen && !player.MapOpen;
            AdvanceIgnition(aiming, triggerHeld, dt);
        }

        /// <summary>Same continuous-hold transition used by player input; no progress survives losing aim or releasing.</summary>
        public void AdvanceIgnition(bool aimedAtTorch, bool triggerHeld, float dt)
        {
            if (!initialized || TorchLit) return;
            if (!LighterEquipped || !aimedAtTorch || !triggerHeld || dt <= 0f) { CancelIgnition(); return; }
            ignition += dt;
            if (ignition < IgnitionSeconds) return;
            ignition = IgnitionSeconds;
            TorchLit = true;
            if (TorchFlame != null) TorchFlame.SetActive(true);
            if (TorchLight != null) { TorchLight.enabled = true; TorchLight.intensity = torchIntensity; }
            Say("A torch with ambitions. Let's see where that beam wants to go.");
            RefreshBeams();
        }

        public void CancelIgnition() { if (!TorchLit) ignition = 0f; }

        public bool TryPickUpMirror(int index, Transform carryAnchor = null)
        {
            if (!initialized || index < 0 || index >= 3 || held >= 0) return false;
            held = index;
            placed[index] = false;
            LighterEquipped = false;
            CancelIgnition();
            if (carryAnchor == null && ExplorerController.Instance != null && ExplorerController.Instance.View != null)
                carryAnchor = ExplorerController.Instance.View.transform;
            Transform mirror = MirrorRoots[index];
            if (carryAnchor != null)
            {
                mirror.SetParent(carryAnchor, false);
                mirror.localPosition = new Vector3(.48f, -.34f, .83f);
                mirror.localRotation = Quaternion.Euler(0f, 160f, -12f);
                mirror.localScale = spawnScales[index] * .34f;
            }
            else mirror.gameObject.SetActive(false);
            SetLayer(mirror, 2);
            SetMirrorColliders(index, false);
            RefreshBeams();
            return true;
        }

        public bool TryPlaceMirror(int socketIndex)
        {
            if (!initialized || held < 0 || socketIndex < 0 || socketIndex >= 3 || placed[socketIndex]) return false;
            if (held != socketIndex)
            {
                Say("The " + ShapeName(held).ToLowerInvariant() + " frame won't fit a " + ShapeName(socketIndex).ToLowerInvariant() + " shadow.");
                return false;
            }
            Transform mirror = MirrorRoots[held];
            mirror.SetParent(spawnParents[held], true);
            mirror.localScale = spawnScales[held];
            mirror.position = MirrorSockets[held].position;
            mirror.rotation = solutionRotations[held] * Quaternion.Euler(0f, detents[held] * 45f, 0f);
            mirror.gameObject.SetActive(true);
            SetLayer(mirror, 0);
            SetMirrorColliders(held, true);
            placed[held] = true;
            held = -1;
            RefreshBeams();
            return true;
        }

        public bool RotateMirror(int index, int direction = -1)
        {
            if (!initialized || index < 0 || index >= 3 || !placed[index] || direction == 0) return false;
            detents[index] = (detents[index] + (direction < 0 ? -1 : 1) + 8) % 8;
            MirrorRoots[index].rotation = solutionRotations[index] * Quaternion.Euler(0f, detents[index] * 45f, 0f);
            RefreshBeams();
            return true;
        }

        public bool RotatePrism(int direction = -1)
        {
            if (!initialized || !Powered || direction == 0) return false;
            Phase = (MoonPhase)(((int)Phase + (direction < 0 ? 1 : 3)) % 4);
            PrismVisual.Rotate(Vector3.up, direction < 0 ? 90f : -90f, Space.World);
            RefreshBeams();
            return true;
        }

        public bool IsMirrorPlaced(int index) { return index >= 0 && index < 3 && placed[index]; }
        public int MirrorDetent(int index) { return index >= 0 && index < 3 ? detents[index] : -1; }
        public bool IsMirrorAligned(int index)
        {
            if (!initialized || !IsMirrorPlaced(index)) return false;
            Vector3 previous = index == 0 ? TorchOrigin.position : MirrorSockets[index - 1].position;
            Vector3 next = index == 2 ? PrismOrigin.position : MirrorSockets[index + 1].position;
            Vector3 reflected = Vector3.Reflect((MirrorSockets[index].position - previous).normalized, MirrorRoots[index].forward);
            return Vector3.Dot(reflected, (next - MirrorSockets[index].position).normalized) > .999f;
        }

        /// <summary>Trace at most three ordered reflections and one termination. Off-angle mirrors visibly send light astray.</summary>
        public void RefreshBeams()
        {
            if (!initialized) return;
            for (int i = 0; i < beamSegments.Length; i++) beamSegments[i].enabled = false;
            moonBeam.enabled = false;
            Powered = false;
            MoonBeamReachesTarget = false;
            AlignedMirrorCount = 0;
            for (int i = 0; i < 3; i++) if (IsMirrorAligned(i)) AlignedMirrorCount++;
            if (TorchLit)
            {
                Vector3 origin = TorchOrigin.position;
                Vector3 direction = (MirrorSockets[0].position - origin).normalized;
                int expected = 0, lastMirror = -1;
                for (int segment = 0; segment < beamSegments.Length; segment++)
                {
                    float distance = DistanceToObstacle(origin, direction, 90f);
                    int hitMirror = -1;
                    for (int i = 0; i < 3; i++)
                    {
                        if (!placed[i] || i == lastMirror) continue;
                        if (MirrorIntersection(i, origin, direction, out float hitDistance) && hitDistance < distance)
                        { distance = hitDistance; hitMirror = i; }
                    }
                    bool hitPrism = false;
                    if (RaySphere(origin, direction, PrismOrigin.position, .64f, out float prismDistance) && prismDistance < distance)
                    { distance = prismDistance; hitMirror = -1; hitPrism = true; }
                    Vector3 endpoint = origin + direction * distance;
                    SetBeam(beamSegments[segment], origin, endpoint, phaseColors[0]);
                    if (hitPrism) { Powered = expected == 3; break; }
                    if (hitMirror < 0) break;
                    if (hitMirror != expected) break;
                    expected++;
                    direction = Vector3.Reflect(direction, MirrorRoots[hitMirror].forward).normalized;
                    origin = endpoint + direction * .015f;
                    lastMirror = hitMirror;
                }
            }
            if (Powered)
            {
                Vector3 delta = Moon.transform.position - PrismOrigin.position;
                float distance = delta.magnitude;
                Vector3 direction = delta / Mathf.Max(.001f, distance);
                float clearDistance = DistanceToObstacle(PrismOrigin.position, direction, distance);
                MoonBeamReachesTarget = clearDistance >= distance - .08f;
                SetBeam(moonBeam, PrismOrigin.position, PrismOrigin.position + direction * clearDistance, phaseColors[(int)Phase]);
                if (MoonBeamReachesTarget && Phase != MoonPhase.White)
                {
                    int rune = (int)Phase - 1;
                    if (!activated[rune])
                    {
                        activated[rune] = true;
                        Say(rune == 0 ? "A green crescent. The left seal is awake."
                            : rune == 1 ? "Half a blue moon. The upper seal wakes. These ancients did enjoy a theme."
                            : "A full red moon. That should get their attention.");
                    }
                    if (!Complete && ActivatedSymbolCount == 3)
                    {
                        Complete = true;
                        if (CitadelDirector.Instance != null) CitadelDirector.Instance.Attune(0);
                        Say("Three moons, three seals. The Ember Crypt is open. Try to contain your applause.", 9f);
                    }
                }
            }
            UpdateSurfaceColors();
        }

        public void ResetPuzzle()
        {
            if (!initialized) return;
            held = -1;
            ignition = 0f;
            LighterEquipped = TorchLit = Powered = Complete = false;
            if (CitadelDirector.Instance != null) CitadelDirector.Instance.Attuned[0] = false;
            Phase = MoonPhase.White;
            PrismVisual.rotation = initialPrismRotation;
            lastSurfaceState = -1;
            if (TorchFlame != null) TorchFlame.SetActive(false);
            if (TorchLight != null) TorchLight.enabled = false;
            for (int i = 0; i < 3; i++)
            {
                placed[i] = activated[i] = false;
                detents[i] = initialDetents[i];
                Transform mirror = MirrorRoots[i];
                mirror.SetParent(spawnParents[i], true);
                mirror.position = spawnPositions[i];
                mirror.rotation = spawnRotations[i];
                mirror.localScale = spawnScales[i];
                mirror.gameObject.SetActive(true);
                SetLayer(mirror, 0);
                SetMirrorColliders(i, true);
            }
            for (int i = 0; i < EmberGates.Length; i++)
            {
                if (EmberGates[i] == null) continue;
                EmberGates[i].position = closedGatePositions[i];
                if (gateColliders[i] != null)
                    for (int c = 0; c < gateColliders[i].Length; c++) if (gateColliders[i][c] != null) gateColliders[i][c].enabled = true;
            }
            RefreshBeams();
        }

        bool MirrorIntersection(int index, Vector3 origin, Vector3 direction, out float distance)
        {
            Transform mirror = MirrorRoots[index];
            Vector3 normal = mirror.forward;
            float denominator = Vector3.Dot(direction, normal);
            distance = 0f;
            if (Mathf.Abs(denominator) < .0001f) return false;
            distance = Vector3.Dot(mirror.position - origin, normal) / denominator;
            if (distance <= .025f) return false;
            Vector3 offset = origin + direction * distance - mirror.position;
            float x = Vector3.Dot(offset, mirror.right) / MirrorHalfSize.x;
            float y = Vector3.Dot(offset, mirror.up) / MirrorHalfSize.y;
            return index == 0 ? Mathf.Abs(x) <= 1f && Mathf.Abs(y) <= 1f : x * x + y * y <= 1f;
        }

        float DistanceToObstacle(Vector3 origin, Vector3 direction, float maximum)
        {
            float result = maximum;
            int count = Physics.RaycastNonAlloc(origin + direction * .025f, direction, rayHits,
                maximum, ~(1 << 2), QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Transform hit = rayHits[i].collider.transform;
                if (hit.IsChildOf(TorchRoot) || hit.IsChildOf(PrismVisual) || hit.IsChildOf(Moon.transform)) continue;
                bool mechanism = false;
                for (int m = 0; m < 3; m++)
                    if (hit.IsChildOf(MirrorRoots[m]) || hit.IsChildOf(MirrorSockets[m])) { mechanism = true; break; }
                if (!mechanism) result = Mathf.Min(result, rayHits[i].distance + .025f);
            }
            return result;
        }

        static bool RaySphere(Vector3 origin, Vector3 direction, Vector3 center, float radius, out float distance)
        {
            Vector3 offset = origin - center;
            float b = Vector3.Dot(offset, direction);
            float discriminant = b * b - (offset.sqrMagnitude - radius * radius);
            distance = 0f;
            if (discriminant < 0f) return false;
            distance = -b - Mathf.Sqrt(discriminant);
            return distance > .025f;
        }

        void UpdateSurfaceColors()
        {
            int state = (int)Phase | (Powered ? 4 : 0) | (MoonBeamReachesTarget ? 8 : 0)
                | (activated[0] ? 16 : 0) | (activated[1] ? 32 : 0) | (activated[2] ? 64 : 0);
            if (state == lastSurfaceState) return;
            lastSurfaceState = state;
            if (PrismLight != null) PrismLight.intensity = prismIntensity * (Powered ? 1.7f : 1f);
            if (PrismRenderer != null)
            {
                PrismRenderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetColor("_EmissionColor", prismEmission * (Powered ? 1.7f : 1f));
                PrismRenderer.SetPropertyBlock(propertyBlock);
            }
            int phase = Powered && MoonBeamReachesTarget ? (int)Phase : 0;
            if (MoonPhases != null && MoonPhases.Length > phase && MoonPhases[phase] != null) Moon.sprite = MoonPhases[phase];
            Moon.flipX = phase == 1; // The supplied crescent is right-lit; the requested first phase is left-lit.
            float moonBrightness = phase > 0 ? 1f : Powered ? .55f : .20f;
            Moon.color = new Color(moonBrightness, moonBrightness * .985f, moonBrightness * .95f, 1f);
            if (MoonLight != null) { MoonLight.color = phaseColors[phase]; MoonLight.intensity = phase > 0 ? 1.4f : Powered ? .7f : .25f; }
            if (MoonHalo != null)
            {
                MoonHalo.GetPropertyBlock(propertyBlock);
                Color haloColor = phaseColors[phase]; haloColor.a = phase > 0 ? .18f : Powered ? .075f : .025f;
                propertyBlock.SetColor("_Color", haloColor); propertyBlock.SetColor("_BaseColor", haloColor); MoonHalo.SetPropertyBlock(propertyBlock);
            }
            for (int i = 0; i < 3; i++)
            {
                if (runes[i] == null) continue;
                Color color = activated[i] ? phaseColors[i + 1] : new Color(.14f, .135f, .12f);
                foreach (Renderer rune in runes[i])
                {
                    if (rune == null) continue;
                    rune.GetPropertyBlock(propertyBlock);
                    propertyBlock.SetColor("_Color", color); propertyBlock.SetColor("_BaseColor", color);
                    propertyBlock.SetColor("_EmissionColor", activated[i] ? color * 1.8f : Color.black);
                    rune.SetPropertyBlock(propertyBlock);
                }
            }
        }

        static void EnablePuzzleEmission(Renderer surface)
        {
            if (surface == null || surface.sharedMaterial == null) return;
            Material material = surface.sharedMaterial;
            if (!material.HasProperty("_EmissionColor")) return;
            // These four materials belong only to the trial. Property blocks change their
            // colors, but cannot enable the emission shader variant on an imported scene.
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            material.EnableKeyword("_EMISSION");
            if (surface is LineRenderer stroke) stroke.generateLightingData = true;
        }

        LineRenderer MakeBeam(string name)
        {
            var beam = new GameObject(name);
            beam.transform.SetParent(transform, false);
            var line = beam.AddComponent<LineRenderer>();
            line.sharedMaterial = BeamMaterial;
            line.positionCount = 2;
            line.useWorldSpace = true;
            line.startWidth = .065f;
            line.endWidth = .045f;
            line.numCapVertices = 2;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.enabled = false;
            return line;
        }

        static void SetBeam(LineRenderer line, Vector3 from, Vector3 to, Color color)
        {
            line.enabled = true;
            line.SetPosition(0, from);
            line.SetPosition(1, to);
            line.startColor = line.endColor = color;
        }

        static PrismInteractable Control(Transform host)
        {
            var control = host.GetComponent<PrismInteractable>();
            return control != null ? control : host.gameObject.AddComponent<PrismInteractable>();
        }

        void SetMirrorColliders(int index, bool enabled)
        {
            foreach (Collider collider in mirrorColliders[index]) if (collider != null) collider.enabled = enabled;
        }

        static void SetLayer(Transform root, int layer)
        {
            root.gameObject.layer = layer;
            for (int i = 0; i < root.childCount; i++) SetLayer(root.GetChild(i), layer);
        }

        static string ShapeName(int index) { return index == 0 ? "Rectangle" : index == 1 ? "Oval" : "Circle"; }
        static void Say(string text, float seconds = 6f) { if (CitadelDirector.Instance != null) CitadelDirector.Instance.Say(text, seconds); }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (generatedBeamMaterial != null) Destroy(generatedBeamMaterial);
        }
    }
}
