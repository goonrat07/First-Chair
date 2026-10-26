# Story 011: A Steep-style chase camera

> **Epic**: First Chair MVP (base skiing system)
> **Status**: In Progress
> **Layer**: Presentation
> **Type**: Logic (camera math) + Visual/Feel (framing, speed)
> **Estimate**: M
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: 2026-10-26

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

The user may send Steep clips; they tune the camera's dynamics (how fast it follows, how far it pulls in) but nothing
waits on them.

**Engine**: Unity 6.3 (URP) | **Risk**: MEDIUM (the motion blur is URP's own; everything else is the camera's math).
Presentation only: the sim is untouched and the camera writes nothing back.

---

## Design

What the screenshots show, and what this camera does about it. Every number is a default in
`FollowCameraSettings` and live in the Feel Lab's Camera tab.

- **Chase framing.** Behind the rider along the direction of travel (the yaw follows the velocity, about 0.3 s behind;
  below 1.5 m/s it holds its last direction), 5.5 m from an aim point about 1.2 m above the feet and a little ahead,
  pitched so the rider sits in the lower middle of the screen. A wide lens, about 62 degrees vertical.
- **Dynamic to the terrain.** The pitch follows the snow ahead, not just under the rider: the drop from the rider to
  points 5 and 15 m along the travel, so over a rollover or into a steep face the camera tilts down to show what is
  coming, and on flats and run-outs it levels out.
- **Never under the snow.** The camera keeps 1 m or more above the snow beneath it, and its line to the rider keeps
  0.5 m above the snow in between; where it would not, the camera rises (the pitch steepens) until it does.
- **Speed.** It closes in with speed, to 4.5 m at full speed, and lower and closer still in a tuck (3.5 m), so a
  fast tuck reads faster. The view widens with speed (up to 12 degrees more), and from about 55 km/h the screen's
  edges blur with the camera's motion (URP motion blur, camera and objects, so the rider stays sharp), more the
  faster.
- **Turns.** A little bank into the turn and a gentle shake at speed, as before.
- **Air.** The yaw keeps following the travel, so flips and spins turn the rider, not the camera; the pitch reads the
  snow ahead of where the rider is over the ground.
- **The lift.** It follows the chair, as before.
- **Removed.** The GMA2 far camera and both Feel Lab camera presets ("GMA far", "chase (old game)"). The Camera tab
  gets "Steep chase (default)", which puts every camera number back to these defaults.

## Acceptance Criteria

- [ ] Unit tests cover the new math: the pitch from the snow ahead, the distance and height with speed and tuck, the
  rise that keeps the camera and its line to the rider above the snow, and the motion blur's strength with speed.
- [ ] In a build, on the test slope and the test mountain: the chase framing at rest, carving, in a fast tuck (closer,
  lower, wider, edges blurred), over a rollover into steeper snow (tilted down), on a traverse, and off a kicker
  (retained screenshots).
- [ ] The camera is never seen under the snow, and the snow never hides the rider, in those rides.
- [ ] The Feel Lab's Camera tab shows the new numbers and "Steep chase (default)"; the old presets are gone.

## Out of Scope

- The skier detail pass (story 012) and procedural animation (story 013).
- Moving round trees and rocks between the camera and the rider; replay and cinematic cameras.
- Any change to riding.

## Test Evidence

**Story Type**: Logic + Visual/Feel. Unit tests in `Assets/Tests/EditMode/FollowCameraMathTests.cs`; retained
screenshots in `production/qa/evidence/story-011-steep-chase-camera/`; the feel is the user's call on the PC.

## Dependencies

- Depends on: Story 009 (the follow camera's speed effects and the Feel Lab), Story 010 (the mountain).
- Unlocks: Story 012 (the skier detail pass, judged from this camera), Story 013 (procedural animation).
