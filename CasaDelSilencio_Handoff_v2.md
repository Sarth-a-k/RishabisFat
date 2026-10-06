# Casa del Silencio — Handoff v2

Continuation notes for the Unity game-jam project. This file is self-contained: it repeats everything the next session needs from the first handoff (`Downloads/CasaDelSilencio_Handoff.md`) and adds everything done since.

---

## 0. Quick context

- **Game:** *Casa del Silencio*, a first-person horror puzzle game for a game jam.
- **Target:** WebGL build for itch.io.
- **Engine:** Unity **6000.6.4f1**, URP (PC renderer is Forward+). Active Input Handling is "Both".
- **Project:** `C:\Users\xitsp\Downloads\gamejam\My project`
- **Main working scene:** `Assets/Scenes/FourfoldCitadel_WithOurStuff.unity`, referred to below as "the map".
- **Standing rule:** **no comments in any code file.**
- **Enter Play Mode Options:** on, with **domain reload off**. Every static has a `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]` reset method. `SessionReset.ResetAll()` calls all of them through reflection.

### Build order (Build Settings)

`MainMenu` → `FourfoldCitadel_WithOurStuff` → `Shadow2D` → (SampleScene, FourfoldCitadel and SunkenPrism are off) → `WarmStatues2D` → `LightBlade2D`

### Story flow

1. **Main menu:** the "Back Again" menu, then the BACK AGAIN card (about 1.8 s, with AGAIN jiggling as it turns red).
2. **Opening cutscene:** `cutscene0.mp4` plays when the map loads from the menu.
3. **Nexus:** the player spawns at (0, 0.12, 0), yaw 90.
4. **NPC1:** the sitting ghost at (10.07, 0.03, 3.55) in the passage. Talking plays `npc1cutscene.mp4`.
5. **Level 1 — Prismatic Hollows** (centre 48.85, 0, 0), now a moonlit cave:
   1. Light the torch.
   2. Place 3 mirrors. Once the third is placed, one of the others twists out of line ("I bet I placed this right...").
   3. Turn the prism through green, blue, red. All 3 mirrors must be aligned.
   4. At the red moon the prism vibrates and shatters, and the room glows.
   5. The Shadow Run Trigger at the Ember gate starts the **BlackoutTransition**, which loads `Shadow2D`.
   6. The player returns to the map and the Ember gate opens.
6. **NPC2:** standing ghost at (77.83, -0.6, -3.27) in the Ember passage, with `npc2cutscene.mp4`.
7. **Level 2 — Ember Keep / lava crypt** (centre 112.15, -2, 0):
   1. The prism is placed in the player's hand automatically on entry.
   2. Place it on the round table and sit. The beam overloads to red (rising hum), then the prism shatters.
   3. The **JULIAN sequence** plays.
   4. `WarmStatues2D` minigame. Its final line is "9 SILHOUETTES, ALL CARRYING THE SAME SHADOWS."
   5. The player returns in front of the east door, which opens (grinding sound).
8. **Ghost_NPC (1):** at x ≈ 139, in the Ember→Void passage. It has **no cutscene**.
9. **Level 3 — Phosphor Void** (the old "Violet Sanctum", centre 173.9, 0, 0), an ultraviolet room:
   1. Press **2** for the UV baton. Its beam reveals hidden footprints, and they glow on their own after 10 s.
   2. The footprints lead to the **red button**.
   3. Pressing it starts the BlackoutTransition, which loads `LightBlade2D`.
   4. The player returns at the Eclipse Keep entrance (215, ~2.1, 0).
10. **NPC3:** "Ghost_NPC (Void Passage)" at (203.5, 0.99, -2.2) in the Void→Eclipse passage, with `npc3cutscene.mp4`.
11. **Level 4 — Eclipse Keep** (centre about 237.15, 2, 0):
    - A slumped skeleton on the throne wears a sunburst necklace.
    - His goggles rest on the armrest, and his journal lies on the seat beside him.
    - The hall is filled with remains, tally marks and dying-sun lighting.
12. **Loop ending:** reading the journal to its last page and closing it starts the ending (see §6).

### World layout (x along the citadel)

| Area | Centre | Size (x × z) |
|---|---|---|
| Nexus | (0, 0, 0) | 21.6 × 21.6 |
| Prismatic Hollows | (48.85, 0, 0) | 48.1 × 36.4 |
| Ember Keep | (112.15, -2, 0) | 46.5 × 30 |
| Phosphor Void | (173.9, 0, 0) | 45 × 45 |
| Eclipse Keep | (237.15, 2, 0) | 49.5 × 48 |

- **Passages** run along x at 10.8–24.8, 72.9–88.9, 135.4–151.4 and 196.4–212.4.
- **Eclipse Keep:**
  - Floor is about y 2.0. The royal dais spans x 227.4–246.9, z 9.75–21.75.
  - The throne slab front is at z 18.86. The seat top is about y 4.82.
  - The carpet runs along x 237.15 from z -24.5 to 4.6.

---

## 1. How work is done (important)

- **Editor setup scripts** live in `Assets/Interaction/Editor/*Setup.cs`. Each is `[InitializeOnLoad]` and watches a trigger file at `Assets/Interaction/Editor/.run_<name>`.
  - When the trigger reads `pending`, the script writes `done`, runs, and writes `Backups/<name>_report.txt`.
  - It usually also saves snapshot PNGs in `Backups/`.
- **A trigger is only checked after a recompile.** If a trigger was set after the last compile, nothing happens. Fix it by touching the setup `.cs` file (`printf '\n' >> file`), or run the script from its **Tools → Interaction → …** menu item.
- **Unity must have window focus and Play mode must be off.** Newer scripts re-arm themselves if Play mode is on.
- **Stale scene guard:** every map-saving script checks whether the open map's Zone Music list is empty. If it is, the open copy is stale, so the script reloads from disk instead of saving over it. It also backs up the map first as `Backups/FourfoldCitadel_WithOurStuff_before_<name>.unity`.
- **Offline compile check:** this catches errors before Unity sees them. It uses Unity's Roslyn:
  - `"/c/Program Files/Unity/Hub/Editor/6000.6.4f1/Editor/Data/DotNetSdk/dotnet.exe" ".../DotNetSdk/sdk/8.0.318/Roslyn/bincore/csc.dll" @rsp`
  - Build the `.rsp` files from the `<HintPath>` references in `Assembly-CSharp.csproj` (runtime) and `Assembly-CSharp-Editor.csproj` (editor).
  - Runtime sources are `Assets/**/*.cs` outside `/Editor/`. Editor sources add `/Editor/` files and reference the runtime DLL.
  - Pass `-nostdlib -langversion:9.0 -target:library` plus the `DefineConstants` from the csproj.
- **Logs:** `Logs/Editor.log` works again. Search it for `error CS` and `Exception`.
- **Audio from mp4:** Unity can't import audio-only mp4. Convert to WAV by decoding in the in-app browser with `OfflineAudioContext` and POSTing to a small local Python server.

---

## 2. Dev keys (Editor only, `Assets/Scripts/DevShortcuts.cs`)

| Key | Action |
|---|---|
| F2 | Play the loop ending now |
| F3 | Teleport into Eclipse Keep |
| F4 | Teleport into the Phosphor Void |
| F5 | Load Shadow2D |
| F6 | Teleport into Ember Keep |
| F7 | Level 1 trigger flow (blackout → Shadow2D) |
| F8 | JULIAN sequence → Warm Statues |
| F9 | Load WarmStatues2D |

To play from the start, open `Assets/Scenes/MainMenu` and press Play.

---

## 3. Editable content files (all in `Assets/Resources/Dialogue/`)

- **`Shadow2D_Dialogue.asset`** (`DialogueSet`): every line of the level 1 2D game, with speakers ("You" = YOU, "Ghost" = ???).
  - Intro lines are keyed `Intro_01`–`Intro_03`.
  - In-run lines are `Bark_FirstShadow`, `Bark_SecondShadow`, `Bark_TorchFlicker`, `Bark_Halfway`, `Bark_TorchDies` and `Bark_Smothered`.
  - Each line has its own hold time.
- **`EclipseKeep_Journal.asset`** (`JournalSet`): six journal pages, all headed "The first day", plus `thoughtAfterLastPage` ("It's my handwriting.").
- **`VoiceLines.asset`** (`VoiceLineSet`): the voice lines, with clips in `Assets/Resources/Voice/*.wav`.
  - **Subtitles are still placeholders.** The user needs to supply the spoken text.
  - Keys are `Npc_Ignore_01`, `Npc_Ignore_02`, `Hint_Torch`, `Hint_Mirrors`, `Hint_PrismRotate` and `Hint_RedMoon`.
  - Mirror and prism-rotate are no longer used.

Setup scripts create these files only if they are missing, so they never overwrite user edits.

---

## 4. Systems added this session (by file)

### Audio and voice

- **`VoiceDirector.cs`** creates itself in the map. It plays one line at a time with a subtitle (`PromptBox`), and nothing plays during cutscenes or while the journal is open.
  - **NPC ignore:** walking about 4 m past a ghost without talking plays a random ignore line, once per ghost.
  - **Torch hint:** 5 s after entering Prismatic Hollows with the torch unlit, then once more 60 s later.
  - **Red moon hint:** 3.5 s after the moon turns red.
  - The mirror and prism-rotate hints were **removed** at the user's request.
- **`ZoneMusic`** has 7 zones:
  - Prismatic Hollows.
  - The 4 passages (`Passageway.ogg`).
  - Ember Keep (`EmberCryptBGM.mp3`): starts after 5 s, fades in over 4 s.
  - Phosphor Void (`PhosphorusVoidBGM.mp3`): volume 0.35, starts after 4 s, fades in over 4 s.
  - `ZoneMusic.FadeOutAll(sec)` stops all zone music.
- **Gate sound:** `LockedGate` plays `Sfx/gate_open`, `soundVolume` 0.55.
- **Prism sounds:** `prism_grind` plays on each turn. In level 1, `prism_hum` then `prism_shatter`. In level 2, `PrismOverload` plays the `beam_charge_loop` rising hum, then `prism_shatter`.
- **NPC cutscene volume:** `NPCCutscene` has `audioVolume` (all set to 1).

### Level 1 — Prismatic Hollows

- **Moonlit cave look:**
  - Painted cave rock and cracked-slab textures in `Generated/LunarStyle`.
  - "Lunar Cave Dressing": boulders, glowing moon crystals and stalactites.
  - Very dark, faint moonlight (fills at intensity 3), so the flashlight matters.
  - The 2D moon and the altar are untouched.
- **`MirrorSabotage.cs`:** once all three mirrors are placed, one of the other two twists 18–24° with a scrape sound and the thought appears. It happens once.
- **`PrismMoonController`:**
  - `requireAllMirrors` (all 3 must be aligned to power the prism).
  - Grind sound on each turn.
  - Vibrate, then shatter (shards and sparks). Exposes `Shattered` and `ShatteredAt`.
- **`RoomSolvedGlow`:** lights the room after the shatter by writing `RenderSettings.ambientProbe` directly, plus a fill light.
- **Hint circles:** the `ShapeBeacon` timer counts 75 s since the last mirror was placed.
- **Carried mirror:** held lower and to the side (`PlayerInteraction.viewClearance` and `holdSide`).

### Level 1 → 2D Shadow game

- **`BlackoutTransition.cs`:** user-provided eyelid blackout, now with a statics reset and a zone-music fade. `ComicPanelTransition.blackout` hands off to it.
- **`ShadowRun2D.cs` v7** (user-provided), with these changes:
  - Music at 0.45 with a 3 s fade-in.
  - The darkness sprite gets a black border so the screen-edge rectangle glitch is gone.
  - Lines come from `Shadow2D_Dialogue.asset` as live text instead of the baked `dlg_*.png` images, which are now unused.
  - The player can't walk past the ghost until `introLinesBeforeWalking` (2) lines have played.

### Level 2 — Ember Keep

- **`PuzzleTransitionLink`:** on table completion it calls `JulianTransition.Play()` and `MinigameGate.MarkEntered`.
- **`JulianTransition.cs`:**
  - Waits 1.3 s after the shatter, then fades to black in 0.45 s.
  - Shows "JULIAN" in the four corners, each with its own `julian1–4.wav`.
  - Then a red jiggling line "TURN AROUND. / YOU CAN'T ALWAYS GO FORWARD." before loading `WarmStatues2D`.
- **`WarmStatues2D.cs`:**
  - 2.5D world-space canvas with a follow camera and colour grade.
  - Goggle sight shows plates only within 3–4 tiles (`revealTiles`).
  - Ending line changed (see story flow).
- **Goggles pickup:** gets a `PickupHighlight` glow.
- **East door:** static flags were cleared so it visibly opens.

### Level 3 — Phosphor Void (`Assets/Interaction/Editor/UVRoomSetup.cs`, root "UV Sanctum Dressing")

- **Rebuild source:** the setup rebuilds the room from `Backups/FourfoldCitadel_WithOurStuff_before_region3.unity` each run. That backup has all other work except region 3.
  - **Caution:** re-running it re-copies the whole map from that backup, which would lose later work elsewhere (Eclipse Keep, NPC3 and so on). Change it to work in place before re-running.
- **Look:**
  - Themed violet stone (`uv_sanctum_wall`/`floor` with emissive veins).
  - Random crack decals on the dome, and a ceiling rune circle.
  - A cyan crystal hanging on a chain, plus 9 small hanging crystals, obelisks, rubble and motes.
  - Dim violet lights, darkened a further 35%.
- **`UVBaton.cs`** (key **2**):
  - Gripped in the left hand through the `PlayerInteraction` arm IK, slanted.
  - Glowing tube, halo light and a narrow beam.
  - `Illuminates()` checks a small 7° cone.
- **Footprints:** `UVFootprint.cs` and `UVFootprintTrail.cs` handle 45 hidden footprints that reveal under the beam, with the trail glowing after 10 s.
- **`RedButton.cs`:** the right hand reaches out and presses it (`PlayerInteraction.ReachToPress`).
- **`VoidButtonExit.cs`** (on the "Phosphor Void Exit" object): button → `BlackoutTransition` → `LightBlade2D`. A `MinigameGate` with id `lightblade` returns the player to the Eclipse Keep entrance.
- **Area title:** "Phosphor Void" uses `Assets/AreaTitles/04_PhosphorVoid.png` and `.wav`.

### Light Blade (`Assets/Scripts/LightBladeSequence.cs`, `Assets/Resources/LightBlade/`)

- User-provided: a 45-second sword fight, then a 2D hall. It runs in the `LightBlade2D` scene and returns to the map.
- Only one change: `FindObjectOfType` became `FindAnyObjectByType`.
- **Never confirmed to run in Unity yet.**

### Level 4 — Eclipse Keep

- **`EclipseKeepSetup.cs`** (root "Eclipse Keep Dressing"):
  - Throne seat with the skeleton on it. The skeleton is a mirrored, slumped mesh, `Generated/EclipseKeep/Meshes/skeleton_slumped.asset`.
  - Sunburst necklace (black disc, bronze rim, 12 spikes) on the ribcage.
  - Copy of the goggles on the armrest, and the journal (`JournalReader`) on the seat.
  - Leftovers from earlier loops: an old mirror, prism shards, a burnt torch, statue fragments and an old baton.
  - Corona, throne spotlight and tally marks.
  - `LookThought` lines on the skeleton and the goggles.
- **`EclipseBeautifySetup.cs`** (root "Eclipse Keep Atmosphere"):
  - Painted plum-charcoal walls and gold-veined slab floor (1281 renderers).
  - Amber torches, a red and gold carpet, 49 candles.
  - Five sun rays (`AxisBillboard`, so they face the player in play).
  - Drifting embers, floor haze and warm rim lights.
- **`EclipseRemainsSetup.cs`** (root "Eclipse Keep Remains"):
  - 70 tally groups across the hall.
  - 9 lying skeletons, 3 seated against walls and 10 bone piles. **All 12 skeletons wear the necklace.**
  - Places the NPC3 ghost.
- **Labels:** the old "REGION 04 / SURVEY SIGIL" labels are hidden.

### Menu and opening

- **`MainMenuScene.cs`** (user's "Back Again" menu, art in `Assets/Resources/MainMenu/`):
  - The card lasts 1.8 s, and AGAIN jiggles and bops (`againJiggle`, `againBop`).
  - Before loading the map it calls `SessionReset.ResetAll()` and sets `OpeningCutscene.Pending = true`.
- **`OpeningCutscene.cs`:** creates itself in the map when `Pending` is true.
  - Plays `StreamingAssets/cutscene0.mp4` full-screen over black. Space or Esc skips.
  - The video is 28 MB, which is large for WebGL.

### Misc

- **`QuestMarker`:** null property-block crash fixed.
- **Shadows:** URP high-tier additional-light shadows set to 512 to stop the shadow-atlas warning.
- **Region 3 experiments:** the white-room and ring-tunnel scripts (`WhiteRegionSetup`, `RingTunnelSetup`, `TunnelShadesSetup`) are **obsolete**. They were scrapped for the UV room, so don't run them.

---

## 5. Concept and texture art

- **`ConceptTextures/LunarHollows`** and **`ConceptTextures/LunarCave`:** early concept textures, not used in game.
- **Generated in-game art:**
  - `Assets/Interaction/Generated/LunarStyle` (level 1 cave)
  - `UVRoom` (level 3)
  - `EclipseKeep` (level 4)
  - `WhiteRegion` (obsolete)
- **Texture generators:** written in Python (numpy and PIL) in the old session's scratchpad, which is **not kept**. Re-create them if new textures are needed. The style used: painted cells with dark outlines, a light rim on top edges, blotchy fill and a few cracks.

---

## 6. LAST TASK: loop ending (just finished, NOT play-tested)

- **Files:** `Assets/Interaction/Scripts/LoopEnding.cs` and `SpriteWalker.cs`, plus a `JournalReader.onFinishedReading` event.
- **What happens:**
  1. `LoopEnding` creates itself in the map.
  2. Closing the journal after its last page starts the sequence after 5 s. **F2** starts it straight away.
  3. Fade to black.
  4. The scripts that drive the player, voice, hints, baton and area titles are switched off. The player is moved near the sitting ghost and hidden, its camera disabled, and a cinematic camera placed at the sitting ghost's head. The ghost's renderers are hidden, so you see "as" the ghost.
  5. A **2D billboard of the main character** (the Warm Statues explorer, `Resources/WarmStatues/player_idle/walk1/walk2`) appears at (1.5, 0, -0.6).
  6. He walks the path (5.5,0.2) → (11,0.6) → (17,0.2) → (24,0) → (34,0), **pausing at the second point** to face the player.
  7. The camera follows him. When he passes x 21.5 the screen fades out, `SessionReset.ResetAll()` runs, and the `MainMenu` scene loads.
- `LoopEnding.cs` still contains the unused helpers `MakeSolid` and `FindBone` from the dropped 3D approach. Delete them.
- **Next steps:**
  1. Play-test it with F2.
  2. Check the sprite's lighting and size (`height` 1.75), the camera angle and the pause timing.
  3. Make sure the player's FP arms or torch don't show.

---

## 7. Open items and ideas

- **Subtitles:** `VoiceLines.asset` still needs the real subtitle text.
- **Ghost_NPC (1):** the ghost at x ≈ 139 has no cutscene. Ask the user whether it should have one.
- **Light Blade:** first run in Unity is untested. Watch the Console.
- **WebGL prep, not started:**
  - Install WebGL Build Support.
  - Make a test build.
  - Include runtime shaders: `CasaFX/*`, `SunkenPrism/TorchFlame`, URP Particles/Unlit, Simple Lit. Many materials are created at runtime with `Shader.Find`, so add them to Always Included Shaders.
  - Turn the GPU Resident Drawer off.
  - Use Gzip compression with Decompression Fallback.
  - Compress the video files.
  - Many realtime lights and particles: do an optimisation pass.
- **Puzzle Hints overlap:** the older `PuzzleHints` text hints can overlap the voice hints. Consider disabling its torch hint.
