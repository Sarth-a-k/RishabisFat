using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace SunkenPrism
{
    /// <summary>Opt-in built-player checks of the actual interaction, reflection and gate code.
    /// -lunarSmokeTest [-lunarTestResults absolutePath] [-lunarRenderDir absoluteDirectory]</summary>
    public sealed class LunarPuzzleSmokeTest : MonoBehaviour
    {
        readonly List<string> results = new List<string>();
        readonly List<string> failures = new List<string>();
        MaterialPropertyBlock block;
        LunarPrismPuzzle puzzle;
        ExplorerController player;
        Vector3[] gateCenters, gateNormals, initialMirrors;
        float initialIntensity;
        Color initialEmission;
        string resultPath, renderDirectory;
        bool fatalLog;

        IEnumerator Start()
        {
            string[] args = Environment.GetCommandLineArgs();
            if (Array.IndexOf(args, "-lunarSmokeTest") < 0) yield break;
            block = new MaterialPropertyBlock();
            resultPath = Argument(args, "-lunarTestResults");
            renderDirectory = Argument(args, "-lunarRenderDir");
            Application.logMessageReceived += OnLog;
            yield return null;
            yield return new WaitForSeconds(.25f);
            puzzle = LunarPrismPuzzle.Instance;
            player = ExplorerController.Instance;
            if (puzzle == null || player == null || player.View == null)
            {
                failures.Add("Runtime puzzle/player references did not initialize.");
                Finish();
                yield break;
            }
            player.enabled = false;
            puzzle.Initialize();
            Check("Serialized puzzle references and supplied phase assets", () => {
                Require(puzzle.PrismLight != null && puzzle.PrismRenderer != null && puzzle.Moon != null, "Prism/moon rendering references are missing.");
                Require(puzzle.TorchRoot != null && puzzle.TorchOrigin != null && puzzle.TorchFlame != null && puzzle.TorchLight != null, "Puzzle torch references are missing.");
                Require(puzzle.TorchFlame.GetComponentsInChildren<Renderer>(true).Length >= 3, "The supplied flame and ember meshes must belong to the ignition switch.");
                foreach(var renderer in puzzle.TorchRoot.GetComponentsInChildren<Renderer>(true))
                    if(renderer.name.StartsWith("Flame_") || renderer.name=="Embers")
                        Require(renderer.transform.IsChildOf(puzzle.TorchFlame.transform),"A flame mesh escaped the on/off parent: "+renderer.name);
                Require(puzzle.MirrorRoots != null && puzzle.MirrorRoots.Length == 3 && puzzle.MirrorSockets != null && puzzle.MirrorSockets.Length == 3, "Mirror arrays are incomplete.");
                for (int i = 0; i < 3; i++) Require(puzzle.MirrorRoots[i] != null && puzzle.MirrorSockets[i] != null, "Mirror/socket " + i + " reference is missing.");
                Require(puzzle.MoonPhases != null && puzzle.MoonPhases.Length == 4, "Four moon sprites are required.");
                for (int i = 0; i < 4; i++) Require(puzzle.MoonPhases[i] != null, "Moon sprite " + i + " is missing.");
                Require(puzzle.EmberGates != null && puzzle.EmberGates.Length == 1, "The linear route requires one Ember entrance gate.");
                foreach (Transform gate in puzzle.EmberGates) Require(gate != null, "An Ember gate reference is missing.");
                Require(Vector3.Distance(puzzle.EmberGates[0].position,CitadelLayout.PassageEnd(1))<.02f,"The Ember gate is not at its west threshold.");
                Require(Vector3.Dot(puzzle.EmberGates[0].forward,Vector3.right)>.99f,"The Ember gate does not face along the eastbound corridor.");
                Require(CitadelLayout.Contains(1,puzzle.TorchRoot.position)&&CitadelLayout.Contains(1,puzzle.PrismOrigin.position)
                    &&CitadelLayout.Contains(1,puzzle.Moon.transform.position),"A puzzle mechanism lies outside the resized Sunken Prism.");
            });
            if (failures.Count > 0) { Finish(); yield break; }
            gateCenters = new Vector3[puzzle.EmberGates.Length];
            gateNormals = new Vector3[puzzle.EmberGates.Length];
            initialMirrors = new Vector3[3];
            for (int i = 0; i < gateCenters.Length; i++)
            {
                gateCenters[i] = puzzle.EmberGates[i].position + Vector3.up * 1.2f;
                gateNormals[i] = puzzle.EmberGates[i].forward;
            }
            for (int i = 0; i < 3; i++) initialMirrors[i] = puzzle.MirrorRoots[i].position;
            initialIntensity = puzzle.PrismLight.intensity;
            initialEmission = Emission(puzzle.PrismRenderer);
            Check("Initial state: dormant torch, white moon, physically sealed forward entrance", InitialState);
            yield return Photograph("10-lunar-dormant.png", new Vector3(-57, 7.5f, -7), new Vector3(-81, 2.3f, 12));
            Check("Lighter inventory, real focus ray, interrupted and continuous ignition", Ignite);
            yield return Photograph("16-kindled-puzzle-torch.png", new Vector3(-94.1f, 1.85f, -15.3f), new Vector3(-96, 1.3f, -12), true);
            Check("E bindings, carried mirror, wrong-shape rejection and matching installation", PlaceMirrors);
            Check("Misaligned mirrors do not power the prism or advance its phases", () => {
                Require(!puzzle.Powered, "Unsolved mirror arrangement unexpectedly powers the prism.");
                Require(!puzzle.RotatePrism(-1), "An unpowered prism accepted a rotation.");
                Require(puzzle.Phase == LunarPrismPuzzle.MoonPhase.White && puzzle.ActivatedSymbolCount == 0, "Unpowered phase changed.");
            });
            Check("Q bindings align three physical reflections in the required order", AlignMirrors);
            Check("Prism light and material emission increase by exactly 70 percent", VerifyPrismBrightness);
            yield return Photograph("17-white-lantern-prism.png", new Vector3(-83, 2.65f, 18.8f), new Vector3(-80, 1.9f, 22), true);
            Check("An intervening collider blocks the reflected beam and phase activation", ReflectionObstruction);
            Check("Misaligning an installed mirror removes power; realigning restores it", BreakAndRestore);
            yield return Photograph("11-lunar-white-beam-chain.png", new Vector3(-57, 7.5f, -7), new Vector3(-81, 2.3f, 12));
            Check("Moon obstruction prevents a seal; clearing it activates the green crescent", FirstMoonSeal);
            yield return Photograph("12-lunar-green-crescent.png", new Vector3(-80, 5.2f, 20), new Vector3(-80, 5.1f, 30.7f));
            Check("Second prism turn: blue half moon and persistent green seal", () => {
                FocusControl(puzzle.PrismVisual).Rotate(-1);
                Require(puzzle.Phase == LunarPrismPuzzle.MoonPhase.BlueHalf && puzzle.ActivatedSymbolCount == 2, "Blue phase did not activate exactly the second seal.");
                Require(puzzle.Moon.sprite == puzzle.MoonPhases[2], "Blue half-moon sprite is incorrect.");
                VerifyRune(puzzle.LeftRune, 1); VerifyRune(puzzle.UpperRune, 2); VerifyRune(puzzle.RightRune, 0);
                Require(!puzzle.Complete, "Door opened with only two seals.");
                VerifyGates(false);
            });
            yield return Photograph("13-lunar-blue-half.png", new Vector3(-80, 5.2f, 20), new Vector3(-80, 5.1f, 30.7f));
            Check("Removing a mirror removes power but retains acquired seals", RemoveAndReplace);
            Check("Third turn: red full moon, all three colored seals and completed trial", CompleteTrial);
            yield return new WaitForSeconds(3.5f);
            Check("Ember entrance opens: disabled colliders and real player traversal", () => VerifyGates(true));
            yield return Photograph("14-lunar-red-full-complete.png", new Vector3(-80, 5.2f, 20), new Vector3(-80, 5.1f, 30.7f));
            yield return Photograph("18-moon-altar-with-hand-lamp.png", new Vector3(-85, 2.8f, 23), new Vector3(-80, 4.9f, 30.7f), true);
            yield return Photograph("15-ember-gate-open.png", CitadelLayout.PassageEnd(1)+new Vector3(-7,1.8f,0), CitadelLayout.PassageEnd(1)+new Vector3(9,2.7f,0),false,true);
            Check("Reset returns mirrors, clears colors and closes the forward entrance safely", ResetTrial);
            Check("A second complete playthrough succeeds after reset", Replay);
            yield return new WaitForSeconds(3.5f);
            Check("Replayed solution releases the forward entrance again", () => VerifyGates(true));
            player.Teleport(CitadelLayout.Map(1,new Vector3(-80, .1f, 18)));
            player.View.transform.LookAt(puzzle.Moon.transform.position);
            yield return null;
            // A native-player timing sample is diagnostic only; it is not a WebGL benchmark.
            var frameTimes = new List<float>(120);
            for (int i = 0; i < 120; i++) { yield return null; frameTimes.Add(Time.unscaledDeltaTime * 1000f); }
            frameTimes.Sort();
            results.Add("Native-player frame sample (120 frames, not a browser benchmark): median " + frameTimes[60].ToString("F2")
                + " ms; p95 " + frameTimes[114].ToString("F2") + " ms; " + SystemInfo.graphicsDeviceName + "; " + Screen.width + "x" + Screen.height + ".");
            Finish();
        }

        void InitialState()
        {
            Require(!puzzle.TorchLit && !puzzle.TorchFlame.activeSelf && !puzzle.TorchLight.enabled, "Torch initially has flame/light.");
            foreach(var renderer in puzzle.TorchFlame.GetComponentsInChildren<Renderer>(true))Require(!renderer.gameObject.activeInHierarchy,"An unlit flame mesh is visible.");
            Require(!puzzle.Powered && !puzzle.Complete && !puzzle.LighterEquipped, "Initial puzzle/inventory state is incorrect.");
            Require(puzzle.PlacedMirrorCount == 0 && puzzle.ActivatedSymbolCount == 0 && puzzle.HeldMirrorName == "", "Initial mirrors/seals were retained.");
            Require(puzzle.Moon.sprite == puzzle.MoonPhases[0], "Initial white moon sprite missing.");
            Require(puzzle.Moon.color.r < .3f, "Initial moon is not dim.");
            VerifyRune(puzzle.LeftRune, 0); VerifyRune(puzzle.UpperRune, 0); VerifyRune(puzzle.RightRune, 0);
            Require(puzzle.EmberGates.Length == 1, "Expected only the west Ember gate on the sequential route.");
            VerifyGates(false);
        }

        void Ignite()
        {
            FocusControl(puzzle.TorchRoot);
            Require(AimedAt(puzzle.TorchRoot), "Torch cannot be focused from an accessible interaction distance.");
            puzzle.AdvanceIgnition(true, true, 3f);
            Require(!puzzle.TorchLit, "Torch ignited without an equipped lighter.");
            puzzle.ToggleLighter();
            Require(puzzle.LighterEquipped, "Lighter did not equip.");
            puzzle.AdvanceIgnition(AimedAt(puzzle.TorchRoot), true, 1.2f);
            Require(puzzle.IgnitionProgress01 > .4f && puzzle.IgnitionProgress01 < .6f && !puzzle.TorchLit, "Partial hold duration is incorrect.");
            puzzle.AdvanceIgnition(true, false, .1f);
            Require(puzzle.IgnitionProgress01 == 0f, "Releasing the trigger did not cancel ignition.");
            puzzle.AdvanceIgnition(true, true, 1.2f);
            puzzle.AdvanceIgnition(false, true, .1f);
            Require(puzzle.IgnitionProgress01 == 0f, "Losing aim did not cancel ignition.");
            puzzle.AdvanceIgnition(true, true, 1.2f);
            player.SetPaused(true);
            Require(puzzle.IgnitionProgress01 == 0f, "Pause did not cancel the hold.");
            puzzle.TickIgnition(player, true, 3f);
            Require(!puzzle.TorchLit, "Paused input ignited the torch.");
            player.SetPaused(false);
            FocusControl(puzzle.TorchRoot);
            for (int i = 0; i < 26; i++) puzzle.AdvanceIgnition(AimedAt(puzzle.TorchRoot), true, .1f);
            Require(puzzle.TorchLit && puzzle.TorchFlame.activeSelf && puzzle.TorchLight.enabled, "Continuous focused hold did not ignite the torch.");
            foreach(var renderer in puzzle.TorchFlame.GetComponentsInChildren<Renderer>(true))
            {
                Require(renderer.enabled&&renderer.gameObject.activeInHierarchy,"An ignited flame mesh is hidden.");
                Require(renderer.bounds.center.y>1.7f&&renderer.bounds.max.y<3f,"A supplied flame mesh is not upright at the torch head.");
            }
        }

        void PlaceMirrors()
        {
            for (int i = 0; i < 3; i++)
            {
                FocusControl(puzzle.MirrorRoots[i]).Interact();
                Require(!string.IsNullOrEmpty(puzzle.HeldMirrorName), "E did not pick up mirror " + i + ".");
                Require(!puzzle.LighterEquipped, "Picking up a mirror did not free the lighter hand slot.");
                if (i == 0)
                {
                    string held = puzzle.HeldMirrorName;
                    Require(!puzzle.TryPlaceMirror(1), "Rectangle incorrectly fitted the oval socket.");
                    Require(puzzle.HeldMirrorName == held && puzzle.PlacedMirrorCount == 0, "A wrong socket lost or installed the held mirror.");
                    puzzle.ToggleLighter();
                    Require(!puzzle.LighterEquipped, "Lighter equipped while carrying a mirror.");
                }
                FocusControl(puzzle.MirrorSockets[i]).Interact();
                Require(puzzle.IsMirrorPlaced(i) && puzzle.HeldMirrorName == "", "E did not install matching mirror " + i + ".");
            }
            Require(puzzle.PlacedMirrorCount == 3, "Not all three mirrors were installed.");
        }

        void AlignMirrors()
        {
            for (int i = 0; i < 3; i++)
            {
                var control = FocusControl(puzzle.MirrorRoots[i]);
                for (int turn = 0; turn < 8 && !puzzle.IsMirrorAligned(i); turn++) control.Rotate(-1);
                Require(puzzle.IsMirrorAligned(i), "Mirror " + i + " never reflected toward its next target.");
            }
            Physics.SyncTransforms(); puzzle.RefreshBeams();
            Require(puzzle.Powered && puzzle.MoonBeamReachesTarget && puzzle.AlignedMirrorCount == 3, "The physical mirror chain does not reach both prism and moon.");
            Require(puzzle.Phase == LunarPrismPuzzle.MoonPhase.White && puzzle.ActivatedSymbolCount == 0, "Receiving white light incorrectly activated a colored seal.");
            for (int i = 0; i < 3; i++)
            {
                Transform beam = puzzle.transform.Find("Torch reflection " + i);
                Require(beam != null, "Reflection segment " + i + " missing.");
                var line = beam.GetComponent<LineRenderer>();
                Require(line.enabled && Vector3.Distance(line.GetPosition(1), puzzle.MirrorSockets[i].position) < .06f,
                    "Reflection segment " + i + " misses its ordered mirror pivot.");
            }
        }

        void VerifyPrismBrightness()
        {
            Require(initialIntensity > 0f && Mathf.Abs(puzzle.PrismLight.intensity - initialIntensity * 1.7f) < .001f, "Prism light is not baseline × 1.7.");
            Require(CloseColor(Emission(puzzle.PrismRenderer), initialEmission * 1.7f), "Prism emission is not baseline × 1.7.");
        }

        void ReflectionObstruction()
        {
            GameObject obstacle = Obstacle((puzzle.MirrorSockets[0].position + puzzle.MirrorSockets[1].position) * .5f);
            try
            {
                puzzle.RefreshBeams();
                Require(!puzzle.Powered && !puzzle.RotatePrism(-1) && puzzle.ActivatedSymbolCount == 0, "Light passed through an intervening solid collider.");
                Require(Mathf.Abs(puzzle.PrismLight.intensity - initialIntensity) < .001f, "Blocked prism retained its powered luminosity.");
            }
            finally { RemoveObstacle(obstacle); }
            Require(puzzle.Powered, "Removing the obstacle did not restore the beam.");
        }

        void BreakAndRestore()
        {
            puzzle.RotateMirror(1, -1);
            Require(!puzzle.Powered && !puzzle.RotatePrism(-1) && puzzle.ActivatedSymbolCount == 0, "Misaligned oval mirror still powered the prism.");
            puzzle.RotateMirror(1, 1);
            Require(puzzle.Powered, "Reverse rotation failed to restore alignment.");
        }

        void FirstMoonSeal()
        {
            FocusControl(puzzle.PrismVisual);
            GameObject obstacle = Obstacle((puzzle.PrismOrigin.position + puzzle.Moon.transform.position) * .5f);
            try
            {
                puzzle.RefreshBeams();
                Require(puzzle.Powered && !puzzle.MoonBeamReachesTarget, "Moon-only obstruction blocked the prism or failed to block the moon.");
                puzzle.RotatePrism(-1);
                Require(puzzle.Phase == LunarPrismPuzzle.MoonPhase.GreenCrescent && puzzle.ActivatedSymbolCount == 0, "A green beam that missed the moon activated a seal.");
            }
            finally { RemoveObstacle(obstacle); }
            Require(puzzle.MoonBeamReachesTarget && puzzle.ActivatedSymbolCount == 1, "The green beam did not activate exactly one seal after reaching the moon.");
            Require(puzzle.Moon.sprite == puzzle.MoonPhases[1], "Green crescent sprite is incorrect.");
            VerifyRune(puzzle.LeftRune, 1); VerifyRune(puzzle.UpperRune, 0); VerifyRune(puzzle.RightRune, 0);
        }

        void RemoveAndReplace()
        {
            FocusControl(puzzle.MirrorRoots[2]).Interact();
            Require(!puzzle.Powered && puzzle.ActivatedSymbolCount == 2 && !puzzle.RotatePrism(-1), "Removing a mirror did not suspend further progression.");
            Require(puzzle.Moon.sprite == puzzle.MoonPhases[0], "Unpowered moon did not return to its dim white state.");
            VerifyRune(puzzle.LeftRune, 1); VerifyRune(puzzle.UpperRune, 2);
            FocusControl(puzzle.MirrorSockets[2]).Interact();
            Require(puzzle.Powered && puzzle.ActivatedSymbolCount == 2, "Replacing the aligned circle mirror failed to restore power.");
        }

        void CompleteTrial()
        {
            FocusControl(puzzle.PrismVisual).Rotate(-1);
            Require(puzzle.Phase == LunarPrismPuzzle.MoonPhase.RedFull && puzzle.ActivatedSymbolCount == 3 && puzzle.Complete, "Red full moon did not complete the trial.");
            Require(puzzle.Moon.sprite == puzzle.MoonPhases[3], "Red full-moon sprite is incorrect.");
            VerifyRune(puzzle.LeftRune, 1); VerifyRune(puzzle.UpperRune, 2); VerifyRune(puzzle.RightRune, 3);
            Require(CitadelDirector.Instance.Attuned[0], "Completing the moon trial did not record the Sunken Prism sigil.");
        }

        void ResetTrial()
        {
            player.Teleport(CitadelLayout.Map(1,new Vector3(-80, .1f, 18)));
            Vector3 safePosition = player.transform.position;
            puzzle.ResetPuzzle(); Physics.SyncTransforms();
            InitialState();
            Require(player.transform.position == safePosition, "Reset moved the player unexpectedly.");
            for (int i = 0; i < 3; i++) Require(Vector3.Distance(puzzle.MirrorRoots[i].position, initialMirrors[i]) < .001f, "Reset lost mirror " + i + ".");
            Require(Mathf.Abs(puzzle.PrismLight.intensity - initialIntensity) < .001f && CloseColor(Emission(puzzle.PrismRenderer), initialEmission), "Reset did not restore the dim prism.");
        }

        void Replay()
        {
            Ignite(); PlaceMirrors(); AlignMirrors();
            for (int i = 0; i < 3; i++) FocusControl(puzzle.PrismVisual).Rotate(-1);
            Require(puzzle.Complete && puzzle.ActivatedSymbolCount == 3, "The trial was not replayable after reset.");
        }

        PrismInteractable FocusControl(Transform target)
        {
            Collider[] colliders = target.GetComponentsInChildren<Collider>();
            Vector3 aim = target.position;
            foreach (Collider collider in colliders) if (collider.enabled) { aim = collider.bounds.center; break; }
            for (int attempt = 0; attempt < 8; attempt++)
            {
                float angle = attempt * Mathf.PI * .25f;
                Vector3 feet = new Vector3(aim.x + Mathf.Sin(angle) * 2.5f, CitadelLayout.Centers[1].y+.1f, aim.z - Mathf.Cos(angle) * 2.5f);
                if(!CitadelLayout.Contains(1,feet))continue;
                player.Teleport(feet);
                player.View.transform.LookAt(aim);
                Physics.SyncTransforms();
                if (Physics.Raycast(player.View.transform.position, player.View.transform.forward, out RaycastHit hit, 4.5f, ~(1 << 2), QueryTriggerInteraction.Ignore))
                {
                    var control = hit.collider.GetComponentInParent<PrismInteractable>();
                    if (control != null && (control.transform == target || control.transform.IsChildOf(target))) return control;
                }
            }
            throw new InvalidOperationException("No unobstructed 4.5 m player focus ray reaches " + target.name + ".");
        }

        bool AimedAt(Transform target)
        {
            return Physics.Raycast(player.View.transform.position, player.View.transform.forward, out RaycastHit hit,
                4.5f, ~(1 << 2), QueryTriggerInteraction.Ignore) && hit.collider.transform.IsChildOf(target);
        }

        void VerifyGates(bool open)
        {
            Physics.SyncTransforms();
            for (int i = 0; i < puzzle.EmberGates.Length; i++)
            {
                Collider[] colliders = puzzle.EmberGates[i].GetComponentsInChildren<Collider>(true);
                Require(colliders.Length > 0, "Entrance " + i + " has no blocking collider.");
                foreach (Collider collider in colliders) Require(collider.enabled != open, "Entrance " + i + " collider state is incorrect.");
                bool blocked = Physics.Linecast(gateCenters[i] - gateNormals[i] * 2f, gateCenters[i] + gateNormals[i] * 2f,
                    out RaycastHit hit, ~(1 << 2), QueryTriggerInteraction.Ignore);
                Require(open ? !blocked : blocked && hit.collider.transform.IsChildOf(puzzle.EmberGates[i]),
                    "Entrance " + i + (open ? " remains obstructed: " : " can be bypassed at its center: ") + (blocked ? hit.collider.name : "no collider"));
            }
            VerifyGateTraversal(open);
        }

        void VerifyGateTraversal(bool open)
        {
            Vector3 savedPosition=player.transform.position;
            Quaternion savedRotation=player.transform.rotation,savedViewRotation=player.View.transform.localRotation;
            try
            {
                Vector3 entrance=CitadelLayout.PassageEnd(1),passageStart=CitadelLayout.PassageStart(1);
                Vector3 approach=Vector3.Lerp(passageStart,entrance,1-3f/(entrance.x-passageStart.x))+Vector3.up*.1f;
                player.transform.rotation=Quaternion.identity;
                // Test the middle and both sides inside the resized 6 m portal, allowing for the jambs and player radius.
                foreach(float lateral in new[]{-2.1f,0f,2.1f})
                {
                    player.Teleport(approach+Vector3.forward*lateral);Physics.SyncTransforms();
                    for(int step=0;step<35;step++)player.SimulationStep(Vector2.zero,false,false,false,1f/60f);
                    for(int step=0;step<140;step++)player.SimulationStep(Vector2.right,false,false,false,1f/60f);
                    Require(Mathf.Abs(player.transform.position.z-lateral)<.2f,"The gate traversal left its test lane.");
                    if(open)Require(player.transform.position.x>entrance.x+2,"The solved gate still blocks the player in lane "+lateral+" at "+player.transform.position);
                    else
                    {
                        Require(player.transform.position.x<entrance.x-.4f&&player.transform.position.x>entrance.x-1.5f,
                            "The unsolved gate can be bypassed, or approach is blocked early, in lane "+lateral+" at "+player.transform.position);
                        Require(Physics.SphereCast(player.transform.position+Vector3.up*.9f,.25f,Vector3.right,out RaycastHit hit,1.2f,~(1<<2),QueryTriggerInteraction.Ignore)
                            &&hit.collider.transform.IsChildOf(puzzle.EmberGates[0]),"A different collider stopped the player before the locked gate.");
                    }
                }
            }
            finally
            {
                player.Teleport(savedPosition);player.transform.rotation=savedRotation;player.View.transform.localRotation=savedViewRotation;
                Physics.SyncTransforms();
            }
        }

        void VerifyRune(Renderer[] rune, int color)
        {
            Require(rune != null && rune.Length > 0, "Rune geometry is missing.");
            foreach (Renderer renderer in rune)
            {
                Color emission = Emission(renderer);
                if (color == 0) Require(emission.maxColorComponent < .001f, "Dormant rune emits light.");
                else if (color == 1) Require(emission.g > emission.r * 2f && emission.g > emission.b * 2f, "Left seal is not green.");
                else if (color == 2) Require(emission.b > emission.r * 2f && emission.b > emission.g * 2f, "Upper seal is not blue.");
                else Require(emission.r > emission.g * 2f && emission.r > emission.b * 2f, "Right seal is not red.");
            }
        }

        Color Emission(Renderer renderer)
        {
            renderer.GetPropertyBlock(block);
            return block.GetColor("_EmissionColor");
        }

        static bool CloseColor(Color a, Color b)
        {
            return Mathf.Abs(a.r - b.r) < .001f && Mathf.Abs(a.g - b.g) < .001f && Mathf.Abs(a.b - b.b) < .001f;
        }

        GameObject Obstacle(Vector3 position)
        {
            var obstacle = new GameObject("Smoke test temporary beam obstruction");
            obstacle.transform.position = position;
            obstacle.AddComponent<BoxCollider>().size = Vector3.one * 1.5f;
            Physics.SyncTransforms();
            return obstacle;
        }

        void RemoveObstacle(GameObject obstacle)
        {
            obstacle.GetComponent<Collider>().enabled = false;
            Destroy(obstacle);
            Physics.SyncTransforms(); puzzle.RefreshBeams();
        }

        IEnumerator Photograph(string name, Vector3 position, Vector3 target, bool lampOn = false,bool worldCoordinates=false)
        {
            if (string.IsNullOrEmpty(renderDirectory)) yield break;
            if(!worldCoordinates)
            {
                Vector3 mappedTarget=CitadelLayout.Map(1,target);
                // Close-up objects keep their authored size, so retain their camera offset too.
                position=lampOn?mappedTarget+(position-target):CitadelLayout.Map(1,position);
                target=mappedTarget;
            }
            player.Teleport(position - Vector3.up * 1.64f);
            player.View.transform.position = position;
            player.View.transform.LookAt(target);
            var handLamp=player.View.transform.Find("Hand Lamp • visual light only").GetComponent<Light>();
            bool wasOn=handLamp.enabled;handLamp.enabled=lampOn;
            // Let the ordinary nearest-light budget settle after a test-camera teleport.
            yield return new WaitForSeconds(.3f);
            Check("Render " + name, () => {
                Directory.CreateDirectory(renderDirectory);
                RenderTexture render = RenderTexture.GetTemporary(1920, 1080, 24, RenderTextureFormat.ARGB32);
                RenderTexture previousTarget = player.View.targetTexture, previousActive = RenderTexture.active;
                Texture2D pixels = null;
                try
                {
                    player.View.targetTexture = render;
                    player.View.Render();
                    RenderTexture.active = render;
                    pixels = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
                    pixels.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0); pixels.Apply();
                    File.WriteAllBytes(Path.Combine(renderDirectory, name), pixels.EncodeToPNG());
                }
                finally
                {
                    player.View.targetTexture = previousTarget; RenderTexture.active = previousActive;
                    RenderTexture.ReleaseTemporary(render); if (pixels != null) Destroy(pixels);
                }
            });
            handLamp.enabled=wasOn;
        }

        void Check(string label, Action action)
        {
            try { action(); results.Add("PASS: " + label); Debug.Log("LUNAR_CHECK_PASS: " + label); }
            catch (Exception error) { failures.Add(label + " — " + error.Message); Debug.LogWarning("LUNAR_CHECK_FAIL: " + label + " — " + error); }
        }

        void Finish()
        {
            bool passed = failures.Count == 0 && !fatalLog;
            string report = (passed ? "LUNAR_RUNTIME_SMOKE_PASS" : "LUNAR_RUNTIME_SMOKE_FAILED") + "\n"
                + string.Join("\n", results.ToArray()) + (failures.Count > 0 ? "\nFAILURES:\n" + string.Join("\n", failures.ToArray()) : "")
                + "\nRuntime error log observed: " + fatalLog + "\n";
            if (!string.IsNullOrEmpty(resultPath))
            {
                string directory = Path.GetDirectoryName(resultPath);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
                File.WriteAllText(resultPath, report);
            }
            Debug.Log(report);
            Application.Quit(passed ? 0 : 1);
        }

        static string Argument(string[] args, string key)
        {
            int index = Array.IndexOf(args, key);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }

        void OnLog(string message, string trace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) fatalLog = true;
        }

        static void Require(bool success, string message) { if (!success) throw new InvalidOperationException(message); }
        void OnDestroy() { Application.logMessageReceived -= OnLog; }
    }
}
