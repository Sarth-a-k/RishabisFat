# Journal Ending (book holding) – handoff

Unity 6000.6.4f1, URP. Project: `C:\Users\xitsp\Downloads\gamejam\My project`.
**Project rule: do not put comments in any code file.**

## What it does
In Eclipse Keep (the last level), the player looks at the old journal lying on the throne seat beside the skeleton and presses **E**:
1. The right hand reaches for the old journal, and it flies into the right hand. Subtitle: "This journal... it's the same as mine."
2. The player's own journal rises into the left hand and opens. Subtitle: "Same cover. Same torn corner. Same handwriting."
3. Both journals turn pages together, about 0.25 s apart. The pages are identical procedural scribbles, tally marks and drawings. Subtitle: "Every page... I wrote these."
4. The screen fades to black, then `Assets/StreamingAssets/Cutscene_Journal_Ending_3D.mp4` plays fullscreen (Space skips it).
5. After the video, `LoopEnding.PlayFromBlack()` runs: the player becomes the sitting ghost and watches the newcomer walk past, then the game returns to the main menu.

Nothing here has been play-tested yet. The code compiles. The hand poses and book positions are the parts most likely to need tuning.

## Files

### `Assets/Interaction/Scripts/JournalCompareEnding.cs` (main file)
- Self-spawning: it creates itself after a scene loads if a `JournalReader` exists. On start it disables the `JournalReader`, which means the old 6-page reading UI no longer opens.
- Public fields for tuning (lines ~15-28): the subtitle lines, `pageTurns`, `holdDistance`, `holdSide`, `holdDrop`, `holdTilt`, `videoFileName`, `playLoopEndingAfter`.
- `Sequence()` (~line 116) runs the whole animation timeline: reach, fly, hold, bring in own book, open, page turns, fade, video, loop ending.
- `HoldPose(side, out rot)` (~line 291) gives each book's position and rotation in front of the camera. `side` is -1 for left and +1 for right.
- `HoldBook(book, side, wobble)` (~line 302) places a book every frame and adds a small sway.
- `PageTexture(index)` (~line 390) generates the procedural page images. Both books share them, so their pages match.
- The nested `Book` class (~line 553) builds the procedural book model:
  - Two halves hinged at the spine, plus a two-segment turning page that curls as it turns.
  - `SetOpen(t)`: 0 = closed, 1 = open.
  - `Turn(k, t)`: turns page k, with t going from 0 to 1.
  - `Grip(side)`: the point where that hand holds the book.
  - Book local axes: spine along +Z (top of the page), page normal +Y, open spread along ±X.

### `Assets/Interaction/Scripts/PlayerInteraction.cs` (arm IK)
- `HoldBooks(leftGrip, leftWeight, rightGrip, rightWeight)` (~line 68) is called every frame by the journal ending. Set both weights to 0 to release.
- `ClearHands()` (~line 77) puts away the UV baton, goggles and flashlight before the sequence starts.
- In `ApplyArms()` (~line 576), the `if (bookW > 0f)` block solves the two-bone IK of each arm to the grip points and curls the left fingers.
- In `Update()` (~line 209), a check stops normal interaction while the books are held.

### Supporting files
- `Assets/Interaction/Scripts/LoopEnding.cs`: `PlayFromBlack()` (~line 73) starts the loop ending without its opening fade.
- `Assets/Interaction/Scripts/UVBaton.cs`: `PutAway()`.
- `Assets/Interaction/Scripts/SubtitleBox.cs`: the Julian-style subtitle box used for the lines.
- `Assets/Scripts/DevShortcuts.cs`: **F10** starts the journal ending immediately (Editor only). **F3** teleports into Eclipse Keep.

## How to test
Press Play in `FourfoldCitadel_WithOurStuff`, press **F3**, walk to the journal on the throne seat and press **E**. Or press **F10** anywhere to start it straight away.

## Ideas for further work
- Tune the hand grip and wrist rotation so the hands visibly hold the book edges. The IK currently places only the wrist position.
- Make the old journal visibly more worn, for example with stains, a frayed cover or loose pages.
- Add a real page-turn sound. It currently reuses `Resources/WarmStatues/thump` at a higher pitch.
- Optionally show real handwriting from `Resources/Dialogue/EclipseKeep_Journal.asset` on the pages.
