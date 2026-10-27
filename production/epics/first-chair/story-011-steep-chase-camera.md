# Story 011: A Steep-style chase camera

> **Epic**: First Chair MVP (base skiing system)
> **Status**: In Review
> **Layer**: Presentation
> **Type**: Logic (camera math) + Visual/Feel (framing, speed)
> **Estimate**: M
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: 2026-10-27

## Context

**GDD**: `design/game-brief.md` (MVP feature: "a high, pulled-back follow camera like GMA2's (a Steep-style chase cam
can come later)")
**Requirement**: the user, 2026-10-26, with seven Steep screenshots: "I think that we should change the camera angle to
a chase cam like steep ... the camera angle is kinda dynamic to the surroundings and also like the speed. Notice how
when the character is tucking and going really fast, the camera moves in and makes it seem even faster."

Decisions (user, 2026-10-26):
- The chase camera **replaces** the GMA2 far camera; no toggle back.
- Camera first; then a skier detail pass (story 012, the rider fills more of the screen) and procedural animation on
  top of the key poses (story 013).
- No backpack on the base skier; a pack becomes a customizable item later, once the basics are in.
- 2026-10-27, from the last Steep shot: "when the camera gets in close, the angle slightly changes and it might go
  ever so slightly to the side of the skier". The camera slides over the rider's **right shoulder** (the user's pick,
  over the outside of the turn or the downhill side).

The user may send Steep clips; they tune the camera's dynamics (how fast it follows, how far it pulls in) but nothing
waits on them.

**Engine**: Unity 6.3 (URP) | **Risk**: MEDIUM (the motion blur is URP's own; everything else is the camera's math).
Presentation only: the sim is untouched and the camera writes nothing back.

---

## Design

What the screenshots show, and what this camera does about it. Every number is a default in
`FollowCameraSettings` and live in the Feel Lab's Camera tab.

- **Chase framing.** Behind the rider along the direction of travel (the yaw follows the velocity, about 0.3 s behind;
  nearly stopped, it sits behind the skis), 4.8 m back along the ground and 2.5 m above the snow under it, looking at a
  point 1.2 m above the feet and 1.5 m ahead, so it looks over the rider's head: on a 17 degree groomer the rider
  spans about 52 to 82 percent of the screen's height and the slope's far end sits about 37 percent down. A 50 degree
  vertical lens (about 80 degrees across a 16:9 screen). Placing the camera above the snow, rather than pitching it a
  margin past the slope, makes the view follow the slope by itself.
- **Dynamic to the terrain.** The pitch follows the snow ahead, not just under the rider: the drop from the rider to
  points 5 and 15 m along the travel, so over a rollover or into a steep face the camera tilts down to show what is
  coming, and on flats and run-outs it levels out.
- **Never under the snow.** The camera keeps 1 m or more above the snow beneath it, and its line to the rider keeps
  0.5 m above the snow in between; where it would not, the camera rises (the pitch steepens) until it does.
- **Speed.** It closes in with speed, to 4 m back at full speed (90 km/h), and in a fast tuck right in, 2.4 m back
  and 1.8 m up, aiming lower (0.4 m above the feet), so the tucked rider grows to about 40 percent of the screen's
  height (the Steep tuck shots show about 35 to 60) while the slope ahead stays high on the screen: a fast tuck reads
  faster. The view widens with speed (up to 12 degrees more), and from about 55 km/h the screen's edges blur with the
  camera's motion (URP motion blur, camera and objects, so the rider stays sharp), more the faster. The blur's strength
  is set as at 60 frames a second and scales with each frame's length, so the streaks are as long at any frame rate.
- **Over the shoulder.** Closing in, the camera and its aim slide a little to the rider's right, 0.15 m at full speed
  and 0.4 m in a fast tuck (straight behind at rest), so it still looks along the travel, the rider sits a little left
  of the screen's middle (about 6 percent of the width in a fast tuck, as in the Steep shots, which show 4 to 9) and
  the view takes in a little of the rider's right side. A negative number in the Feel Lab slides it left.
- **Turns.** A little bank into the turn and a gentle shake at speed, as before.
- **Air.** The yaw keeps following the travel, so flips and spins turn the rider, not the camera; the pitch reads the
  snow ahead of where the rider is over the ground.
- **The lift.** It follows the chair, as before.
- **Removed.** The GMA2 far camera and both Feel Lab camera presets ("GMA far", "chase (old game)"). The Camera tab
  gets "Steep chase (default)", which puts every camera number back to these defaults.

## As built (2026-10-26)

- First build (run 40406647164, commit 50c6358), ridden in the cloud: the chase works (it follows the travel through
  carves and traverses, never dips into the snow), but the framing was too wide and too level: with a 62 degree lens,
  5.5 m back and 1.7 m up, the slope's far end sat at mid-screen and the rider filled only about 20 percent of the
  height, the head on the horizon. A framing model (camera height, distance and lens against the rider's feet, head and
  the slope's far end) reproduced that frame within a few percent of the screen; the numbers above come from it.
- Second build (run 40410870423, commit 87ab14a): at rest the framing matched the model (the rider 52 to 81 percent of
  the height, the slope's far end 39 percent down). A fast tuck did the opposite of the Steep shots: the rider shrank
  (34 to 28-31 percent of the height) and the slope's far end sank to mid-screen, because the 12 degree widening undid
  the close-in, the crouch shortened the rider, and the look-ahead, level with the rider, lifted the view off the slope
  as it grew with speed. The framing model, matched to the measured frames, gave the tuck numbers above (and a fixed
  1.5 m look-ahead); loaded live into that build through the Feel Lab, the tucked rider filled 36 to 47 percent of the
  height at 85 to 150 km/h (about a third wider than before) with the far end about 31 percent down.
- The blur depended on the frame rate. URP blurs across a share of each frame's motion, and the cloud's software
  rendering runs about 7 frames a second, each capped at 12 sim steps, so each frame moved the camera through 100 ms of
  riding against 17 ms on a PC at 60 fps: the cloud showed six times a PC's blur (and a halo of the rider's colours in
  the streaked snow beside it), and a 144 Hz screen would show less than half of a 60 Hz one. The sim clock now
  reports the ride time each frame moved the view through, and the blur scales by it. Previewed in the cloud at the
  60 fps strength: the edges streak from about 100 km/h, the rider stays sharp, no halo.
- To reach race speed in the cloud's slow sim, the fast-tuck rides load gravity times 4 through the Feel Lab (cloud
  evidence only). The camera reads only the speed, the tuck and the snow, so its framing at a given speed is the same.
- Third build (run 40417526631, commit 110f0c2): the fast tuck as designed. The rider filled 37 to 44 percent of the
  height at 76 to 164 km/h with the slope's far end about 28 to 33 percent down; the edges streaked from about
  100 km/h with the rider sharp and no halo; rest, carves and the traverse as before. The user then asked for the
  slide over the shoulder (above).
- Final build (run 40423919050, commit a5091fb), ridden 2026-10-27: every acceptance criterion seen in the cloud
  (`production/qa/evidence/story-011-steep-chase-camera/evidence.md`). In a fast tuck the rider sits 3 to 6 percent
  of the width left of the middle (0.0 at rest) at 35 to 46 percent of the height; the rollover tilt, pinned and
  toggled, drops the view about 2 degrees where the snow ahead steepens. The feel is the user's call on the PC.

## Acceptance Criteria

- [x] Unit tests cover the new math: the pitch from the snow ahead, the distance and height with speed and tuck, the
  slide to the rider's right, the rise that keeps the camera and its line to the rider above the snow, and the motion
  blur's strength with speed and with each frame's length.
- [x] In a build, on the test slope and the test mountain: the chase framing at rest, carving, in a fast tuck (closer,
  lower, wider, a little to the rider's right, edges blurred), over a rollover into steeper snow (tilted down), on a
  traverse, and off a kicker (retained screenshots).
- [x] The camera is never seen under the snow, and the snow never hides the rider, in those rides.
- [x] The Feel Lab's Camera tab shows the new numbers and "Steep chase (default)"; the old presets are gone.

## Out of Scope

- The skier detail pass (story 012) and procedural animation (story 013).
- Moving round trees and rocks between the camera and the rider; replay and cinematic cameras.
- Any change to riding.

## Test Evidence

**Story Type**: Logic + Visual/Feel. Unit tests in `Assets/Tests/EditMode/FollowCameraMathTests.cs` and
`FixedStepClockTests.cs` (the ride time each frame moved the view through); retained screenshots in
`production/qa/evidence/story-011-steep-chase-camera/`; the feel is the user's call on the PC.

## Dependencies

- Depends on: Story 009 (the follow camera's speed effects and the Feel Lab), Story 010 (the mountain).
- Unlocks: Story 012 (the skier detail pass, judged from this camera), Story 013 (procedural animation).
