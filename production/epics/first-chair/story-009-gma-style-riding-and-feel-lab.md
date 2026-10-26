# Story 009: GMA-style riding, the Feel Lab and speed cues

> **Epic**: First Chair MVP (base skiing system)
> **Status**: In Progress
> **Layer**: Core
> **Type**: Logic (sim) + Visual/Feel (view, camera, Feel Lab)
> **Estimate**: L
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: 2026-10-26

## Context

**GDD**: `design/game-brief.md` (MVP feature 2, "Skiing on snow"; Build order 2, "prove turning feels good first")
**Requirement**: the user's playtest of build 40122603687 (2026-10-26): it still doesn't feel fast. "This is less of it not
being fast and more of it's not looking fast." Is anything in the movement inherently off? "Scrap the whole movement
thing and literally try to copy it exactly from GMA2, and slightly improve it from there for keyboard controls", or take
it from a game like Steep. Look into tools that help understand a game's feel.

**Decisions (user, 2026-10-26):**
- GMA2 copy + Feel Lab: GMA-style controls as the default (the version 7 scheme stays switchable in the Feel Lab), the live tuning panel, and a first look pass. Then match GMA2's numbers from the user's clips.
- The user records GMA2 clips for measurement (shot list sent 2026-10-26). GMA2 footage is never committed.
- The old prototype, `goonrat07/Bluebird`, was opened read-only for its riding and camera numbers.

**Engine**: Unity 6.3 | **Risk**: HIGH (riding feel). Sim changes only in `Packages/com.firstchair.sim/`.

---

## Findings (2026-10-26)

Measured on build 40122603687 (Linux player, 960x540, at speed):
- **The snow is almost blank.** The groomer's brightness varies by 3 to 7 levels of 255. Its few marks (corduroy, old tracks) run down the fall line, the way the rider travels, so they don't stream past. OpenCV optical flow between two frames a quarter second apart at 86 km/h found the snow moving 0.09 px, when it had moved about 15 px. Only 2 to 4% of the snow had enough texture to track. The groomed texture has 5% shade and a weak bump (`SnowSurface.Colour`).
- **The camera is high and far.** At 45 m with a 50 mm lens a mark on the snow crosses the screen in 2.2 s at 90 km/h. A close chase cam streams the near snow 4 to 5 times faster. GMA2's camera is farther still (its rider is about 4% of the screen tall, ours 7%). So GMA2 gets its sense of speed from what is on the snow, not from the camera.
- **No speed effects.** No field-of-view change, wind, blur, shake or sound.
- **The movement code itself is sound.** The fixed 120 Hz step is rendered with interpolation and doesn't jitter.
- **But the controls are backwards compared to GMA.** Plain A/D was the speed-checking quick turn (key S-turns held about 45 km/h); only W plus steering carved (63 km/h). In GMA plain turning is the main turn.
- **Turning at speed was slow:** a 60° turn took 1 to 1.3 s at 72 km/h.

**GMA** (Toppluva), from the first game's PC version (GMA2 likely matches; the user is confirming):
- Controls: ←/→ turn, ↓ brake, Space jump, Ctrl slap, Backspace rewind.
- Holding Shift while turning is a drift turn. Releasing one held long enough gives a slight speed boost.
- Holding Alt while turning is a quick ("short") turn: useful when almost missing a gate, but it costs speed.
- No forward key: gravity does it. Turning left/right/left builds speed, but only up to a certain velocity.
- The camera slowly adapts to the rider's direction; the rider usually moves at about 15° to the screen.
- The snow is one large texture, constantly updated with ski tracks and used as a bump map, so every mark stays.
- The developers aimed for "a middle way between realism and fun" and dropped control that got less precise with speed ("random and annoying").

**Bluebird** (the user's browser prototype, which got close to the right feel quickly):
- Plain A/D carves. The edge builds in 0.2 s, flattens in 0.11 s and goes edge to edge in 0.09 s.
- The turn rate is edge × min(2.3 rad/s, 1.6 g / speed). The edges hold up to 1.9 g on groomed snow (scaled by 0.35 + 0.65 × edge), and redirect the sideways speed into forward speed at 99% efficiency, so carves barely cost speed. Tucked turns are 60% as sharp.
- S + A/D throws the skis 77° across in a braking pivot at 4.6 rad/s and holds them there.
- The camera is close: 7 m behind, dropping from 7 m up to 4.4 m up by 26 m/s. The field of view is 62° + 0.35° per m/s up to +16°, and the camera follows the travel direction with a 0.25 s time constant. It rolls 0.035 rad per g into turns and shakes gently above 18 m/s.
- The snow has a scanned snow normal map (Poly Haven `snow_02`, CC0) at two scales. Fast carves throw spray at 40%.
- A live tuning panel (backtick) edits every riding and camera number in place, with a "Copy values" button.

**Tools to understand feel:** optical flow and tracking on recorded clips (OpenCV; installed in the cloud session), the Feel Lab (below), and Kinovea for manual frame-by-frame study.

## Design

### Controls (keyboard; gamepad keeps the stick and triggers)

| Input | On snow | In the air |
|---|---|---|
| A/D or ←/→ | turn (carve): the main turn, keeps speed | spin |
| Shift (either) + turn | drift turn: tails slide, tighter, a small boost on release after holding | Shift + direction grabs |
| Ctrl or Alt + turn | quick turn: sharp pivot that costs speed | (none) |
| S or ↓ | brake; S + turn throws the skis across (hockey stop) | flip back |
| W or ↑ | tuck (turns 60% as sharp); poles on flats when slow | flip forward |
| Space | hold to load, release to pop | |
| Turning left/right/left | skates: a push at each edge change, up to a speed cap | |

`SimButtons.Quick` is new. On snow `SimButtons.Grab` held with steering is the drift turn (it does nothing else on snow).

### The ground step (sim version 8)

1. **Edge.** The edge fraction e moves toward the steering. A flip toward the other side runs at 2 / `EdgeFlipTime` until the new edge is set; a held key ramps at 1 / `EdgeRampTime`; a released one flattens at 1 / `EdgeReleaseTime`.
2. **Turn rate.**
   - Carve (plain turn): |e| × min(`TurnRateLow`, `TurnAcceleration` × snow `TurnFactor` / speed). It always turns, with no dead zone between slow and fast. In a tuck it is × `TuckTurnFactor`.
   - Quick turn: the skis pivot at |e| × `QuickTurnRate`, up to `QuickSlipMax` ahead of the path from `QuickMinSpeed` up.
   - Drift: the skis swing out at `DriftSwingRate` to `DriftAngle` ahead of the path and hold there.
   - Hockey stop (S + turn): the skis swing at `HockeySwingRate` to `HockeyAngle` across the path and hold there.
3. **Gravity** projected on the surface.
4. **Grip.** Split the velocity along and across the skis. The edges cancel sideways speed up to snow `CarveGrip` × g-share (`GripBase` + (1 − `GripBase`) × |e|) × step, times `DriftGripFactor` in a drift or `BrakeGripFactor` braking; a quick turn grips like a tyre (`SkidStiffness` per radian of slip up to snow `SkidGrip`). The cancelled sideways speed is turned into forward speed at `CarveEfficiency` (carve), `DriftKeep` (drift), `QuickKeep` (quick turn) or `BrakeKeep` (brake). Sideways speed left above `SkidSlipSpeed` is a skid.
5. **Skate.** When an edge flip starts from at least `SkateMinEdge` of edge, below `SkateMaxSpeed` and at least `SkateInterval` after the last push, the skis push `SkatePush` along themselves (never past the cap).
6. **Drift boost.** Releasing a drift held at least `DriftBoostTime` adds `DriftBoost` along the skis.
7. **Slowing:** snow drag × load, air drag × speed² (× `TuckAirDragFactor` tucked), `ScrubDrag` × sideways speed left, snow `SkidDrag` × slip × speed in a quick turn, and `BrakeDeceleration` braking. Never past a stop. Flow cuts snow and air drag as before.
8. Pole, pop, takeoff and rails are unchanged.

Speeds on straight runs are unchanged from version 7: the user's call on 2026-10-26 was that the numbers are fast enough and the look is the problem.

### The Feel Lab (Unity, in every build)

- The backtick key opens a panel over the game, like Bluebird's.
- **Riding:** every `Tuning` number. "Apply" restarts the run from the current spot with the new numbers.
- **Camera** and **Effects:** every follow-camera and view setting, live.
- **Presets:** "GMA far" (the default) and "Chase" (Bluebird's close camera), plus "Version 7 controls" for comparison.
- A live graph of speed and turn rate.
- "Copy values" puts every changed number on the clipboard to paste back to Claude, and "Save" writes `feel-lab.json` next to the game's log.

### Speed cues (the look pass)

- **Snow that shows motion:** stronger, rounder texture on every snow type, so marks cross the rider's path; the corduroy stays but no longer dominates.
- **The camera:** follows turns faster, rolls a little into them, widens its field of view at speed and shakes gently when fast. All are settings, and all are in the Feel Lab.
- **Spray** from fast carves as well as skids, and tracks that last longer.

## Acceptance Criteria

- [ ] Plain steering on groomed snow carves: the velocity follows the skis with no skid, and key S-turns down the groomer hold at least as much speed as carving with W did in version 7.
- [ ] A carve's turn rate is TurnRateLow when slow and TurnAcceleration / speed when fast, scaled by the snow's turn factor; a tuck turns TuckTurnFactor as sharply.
- [ ] Edge to edge takes EdgeFlipTime; a held key reaches full edge in EdgeRampTime; a released one flattens in EdgeReleaseTime.
- [ ] A quick turn (Quick + steering) turns the path sooner than a carve at the same speed but loses more speed.
- [ ] A drift turn (Grab + steering on snow) holds the skis DriftAngle ahead of the path, turns tighter than a carve, loses some speed, and releasing it after DriftBoostTime adds DriftBoost.
- [ ] S plus steering swings the skis HockeyAngle across and stops the rider sooner than a straight brake.
- [ ] Alternating steering below SkateMaxSpeed on the flat gains speed, never past SkateMaxSpeed.
- [ ] Straight-run speeds on the test slope still pass story 008's targets (50 km/h within 8 s, 75 within 12).
- [ ] No riding number is inline in logic; the sim version is 8; the determinism test passes.
- [ ] The Feel Lab opens with backtick in a build, edits riding, camera and effect numbers, and copies them out (retained screenshot).
- [ ] At speed the snow visibly moves: optical flow on two frames of a build finds the snow's motion (retained evidence).

## Out of Scope

- Matching GMA2's exact numbers: that waits for the user's clips (shot list sent 2026-10-26).
- Slapping, rewind, sound, the lighting pass and track buildup in the snow shader.
- A Steep-style control scheme (the chase camera preset covers its look).

## Test Evidence

**Story Type**: Logic + Visual/Feel. Unit tests in `sim/FirstChair.Sim.Tests/`; retained screenshots in
`production/qa/evidence/story-009-gma-style-riding/`.

## Dependencies

- Depends on: Story 008 (the version 7 riding it replaces), Story 003 (the view and camera).
- Unlocks: tuning to GMA2's measured numbers; closing story 003 (the user's playtest of the feel).
