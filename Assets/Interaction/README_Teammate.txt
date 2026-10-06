GHOST / TORCH / MIRROR KIT - how to use in your map

REQUIREMENTS
- Unity 6 (same version as ours: 6000.6.4f1 is safest)
- Universal Render Pipeline (URP) project
- Input System package installed
  (Edit > Project Settings > Player > Active Input Handling = "Input System Package (New)" or "Both")

IMPORT
1. Assets > Import Package > Custom Package... > pick the .unitypackage
2. Import everything EXCEPT nothing needs to be skipped. If Unity asks to enable
   the new input backend and restart, say Yes.

PUT IT IN YOUR MAP (all in Assets/Interaction/Prefabs)
- FP_Player          the player: movement (WASD, Shift sprint, Space jump, Ctrl/C crouch),
                     mouse look, arms + walk/sprint animations, flashlight (F),
                     lighter + interactions (E), carries its own camera.
                     -> Delete the "Main Camera" from your scene, then drag FP_Player in.
- PuzzleTorch_Aimed  unlit torch. E near it = light it with the lighter.
                     Its beam aims at the child "BeamTarget" - move that child to aim the beam.
- Mirror_Rectangle / Mirror_Circle / Mirror_Oval
                     E = pick up, E = place upright, Q/R = rotate while carrying.
                     The beam bounces off the glass and stays level after bouncing.
- Ghost_NPC          translucent ghost with idle animation, float and lantern light.

IMPORTANT
- Every floor / wall / object the player should stand on or the beam should hit
  needs a Collider (Mesh Collider or Box Collider).
- GameTest.unity is our test scene - open it to see everything working.
- Tools > Interaction menu: only run "Rebuild Interaction Setup" inside GameTest.unity,
  it rebuilds that test scene. Do not run it in your map scene.
