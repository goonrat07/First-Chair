# Story 008: Turn styles and speed: carve, quick turns, powder, faster slopes

> **Epic**: First Chair MVP (base skiing system)
> **Status**: In Review (sim and view done; the user's playtest of the feel is open)
> **Layer**: Core
> **Type**: Logic
> **Estimate**: L
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: 2026-10-24

## Context

**GDD**: `design/game-brief.md` (MVP feature 2, "Skiing on snow"; Build order 2, "prove turning feels good first")
**Requirement**: the user's playtest of build 40060424205 (2026-10-24, story 003 "Playtest feedback"): "The speed is
still way too slow... The slope alone should accelerate me like in Grand Mountain Adventure 2." Turning needs a carve
that keeps speed on groomers, quicker turns that trade speed for tighter lines in the trees, turning that depends on
speed and on the snow, and no carving in the trees. "Just do your best to nail the skiing feel this time."

**ADR Governing Implementation**: N/A (minimal — no ADRs)

**Engine**: Unity 6.3 | **Risk**: HIGH (riding feel). The sim changes only in `Packages/com.firstchair.sim/`.

---

## Research (2026-10-24)

- **Carving:** in an ideal carve the edges alone trace the path, with no slip, so the only losses are snow friction
  and air drag. The turn radius follows the edge (tilt) angle: R = R_sidecut · cos(edge) (Jentschura and Fahrbach,
  "Physics of Skiing: The Ideal-Carving Equation", arXiv physics/0310086). Carving skis have R_sidecut ≈ 13 to 16 m.
  - A skier needs speed to lean in, so a slow carve can only follow a wide arc and barely turns.
  - Above about √(g·R_sidecut) (11 to 12 m/s on slalom skis) a pure carve can't hold the turn, so at speed the radius
    has to grow.
  - Friction grows with the turn's g-force, so tight carves cost some speed (Komissarov, "Dynamics of carving runs in
    alpine skiing", arXiv 1812.02539). That paper uses μ = 0.04 and κ = C_d·A·ρ/2 with C_d 0.5, A 0.4 m² and
    80 kg, which is about 0.0015 to 0.003 per metre of air drag per unit mass.
  - Pure carving is only possible on fairly gentle slopes. Steeper ones need hybrid, partly skidded turns.
- **Skidded and pivot turns:** the skis rotate ahead of the travel direction and the snow drags the velocity round.
  Speed is lost in proportion to the slip; the base plows snow. Tree skiing is mostly pivot slips and short skidded
  turns sized to the gaps, not carving (SKI Magazine "How to pivot slip"; SkiTalk glade threads).
- **Powder:** skiers keep the skis flatter (edging sinks the tips), weight both skis, unweight between turns, and make
  rounder, slower-starting turns that stay near the fall line. The deep snow supports the skis sideways, and plowing
  it controls the speed (Carv, Montec and Public Lands technique guides).
- **Game feel:** many board and ski games drive the rider like a steerable vehicle, not a rigid body. Grip should
  depend on edge, speed and load, with a skid penalty when the skis are misaligned with the velocity. Speed reads
  faster with a wider field of view at speed, nearby detail and particles (Holm et al., perceived speed in racing
  games, 2017).
- **Smooth, not random:** GMA2's developers first made control less precise the faster you go. Players found that
  "random and annoying", so they settled on "a middle way between realism and fun". Their camera slowly adapts to the
  rider's direction (80.lv, "Developing a Ski Open World Game for Mobile"). Lonely Mountains' team keeps input
  simple and fakes stability on top of real rules, so riding feels real "although not necessarily physically
  correct" (80.lv, "Level & Game Production: Lonely Mountains: Downhill").
  - Car games get a smooth breakaway from tyre grip that rises linearly with slip angle (cornering stiffness) up to a
    limit (the Pacejka "Magic Formula"). The grip doesn't switch from holding to sliding in one step.
  - Hence here: no randomness at speed, and a carve stays precise. Quick turns slip by a small, predictable angle that
    grows smoothly into a capped skid. The carve fades in with speed, and the edge ramps.

## Design

Inputs are unchanged: A/D or the stick steer, W is forward, S is back.

- **W held (low stance):**
  - Steering on snow that can carve, at or above `CarveMinSpeed`, is a **carve**. The stance is `Carve`: low,
    racer-like, and a little less air drag (`CarveAirDragFactor`).
  - No steering is the **tuck**: straight, with the least air drag.
  - On a flat below the pole cap, W still poles.
- **No W:**
  - Steering is a **quick turn**: the skis pivot, and the snow drags the velocity after them.
- **S** brakes. Steering still pivots while braking.

**Carve:** the edge ramps toward the steering (`EdgeRate`, up to `MaxEdgeAngle`). The turn's curvature is
edge fraction × the largest curvature the carve holds at this speed:

    grip left   = CarveTurnShare · (CarveGrip(snow) − gravity's pull across the slope)
    κ_max(v)    = min(1 / CarveRadiusMin, grip left / v²)
    turn rate ω = edge fraction · v · κ_max(v)

- Slow carves turn slowly, because ω scales with v.
- Fast carves are wide, because the grip caps the sideways acceleration v²κ.
- The edges hold the velocity on the arc: the snow cancels sideways velocity up to `CarveGrip(snow)`, so a carve
  never skids on groomed snow.
- Snow friction acts on the load μ·√(g_n² + (vω)²), so tight carves cost a little speed.
- Below `CarveMinSpeed` you can't carve: W plus steering pivots instead. From `CarveMinSpeed` to
  `CarveMinSpeed + CarveBlendSpeed`, the turn rate and grip blend from the pivot into the carve. Speeding up or slowing
  down mid-turn never jumps.

**Quick turn (pivot):** the skis turn at `PivotRate · PivotFactor(snow) / (1 + v / PivotFadeSpeed)`: nimble when slow,
slower when fast.

- The snow holds pivoted skis like a tyre holds a car. It pushes back in proportion to the slip angle,
  `SkidStiffness` per radian, up to `SkidGrip(snow)`.
- So a quick turn always slips a little: slip angle = sideways acceleration / `SkidStiffness`. That slip costs speed,
  sideways acceleration · tan(slip), plus `SkidDrag(snow) · slip · v` for plowing.
- Gentle corrections are nearly free, while hard turns slow the rider down.
- Past the grip the skis skid, and the slip grows. From `CarveMinSpeed` up, the skis never lead the path by more than
  `PivotSlipMax`. So a quick turn held at speed is a steady, controlled skid that checks speed, never a spin to a stop.
  Below it the snow drags the path round after any pivot, so a slow rider can turn on the spot.

This follows car-game tyre models: linear grip up to a limit, so the slide sets in smoothly.

**Snow types:**

| Snow | Carve | Quick turns | Friction |
|---|---|---|---|
| Groomed | carves: grip 20 m/s², tightest 9 m | crisp (skid grip 22) | low (0.035) |
| Ice | carves with little grip (8): washes out early | long slides (skid grip 4), pivots easily (1.1) | very fast (0.020) |
| Slush | weak carve (12) | heavy (skid grip 16, pivot 0.85) | high (0.090) |
| Powder | no carve: W plus steering is a powder turn | slower to start (pivot 0.7), supported sideways (16) | plowing costs more (skid drag 0.45, friction 0.075) |
| Crud | no carve | quick and grippy (skid grip 20): speed checks | fairly fast (0.045) |

Crud is the tracked, overskied snow between and beside the runs and in the tree lines. Pivots there scrub speed, and
it is fairly fast in a straight line.

**Speed:** friction and air drag drop to realistic values: groomed μ 0.035, air 0.0028 per metre (C_d·A 0.4 m² at
80 kg), and tuck 0.55 of that. So the slope alone accelerates the rider hard, about 2.5 m/s² on the 17° groomer, and
the tree lines and glade are fast without W.

**Tuned values, from the probe (`dotnet` console against the committed test slope):**

| Turn of 60° on the groomer | Carve (W) | Quick turn (no W) |
|---|---|---|
| 22 km/h | 0.67 s, +20% speed | 0.49 s, +12% |
| 36 km/h | 0.91 s, +14% | 0.62 s, +5% |
| 54 km/h | 1.02 s, +7% | 0.85 s, −6% |
| 72 km/h | 1.31 s, +4% | 1.07 s, −12% |

| Ride (no W unless carving) | Result |
|---|---|
| Groomer, straight from the start | 50 km/h at 6.1 s, 75 at 10.5 s, about 103 at 20 s |
| Groomer, key S-turns ±50° from z 60 to 170 m | carving 63 km/h, quick turns 45 km/h |
| Tree lines, straight | 50 km/h at 6.3 s (the playtested build: over 8 s) |
| Glade, straight | 50 km/h at 7.3 s |
| Crud tree line, a 0.25 s tap every second | about 56 km/h average |

## Acceptance Criteria

- [x] Holding W while steering on groomed snow carves. The velocity stays on the ski heading with no skid, and the
  speed stays within friction and drag of a straight run.
- [x] A carve's tightest radius grows with speed: at full edge the radius is CarveRadiusMin when slow, and
  v² / CarveGrip at speed.
- [x] Below CarveMinSpeed, W plus steering pivots like a quick turn.
- [x] Steering without W pivots the skis faster than a carve turns at the same moderate speed, loses more speed
  than a carve over the same change of direction, and pivots more slowly the faster the rider goes.
- [x] Powder and crud never carve: W plus steering there turns like a quick turn. Each snow type reads its own
  carve grip, skid grip, skid drag, pivot factor and friction from tuning data.
- [x] On the test slope, from the start with no input, the rider passes 50 km/h within 8 s and 75 km/h within 12 s on
  the groomer, and straight down the tree lines and the glade passes 50 km/h within 8 s without W.
- [x] Key S-turns down the groomer hold a higher average speed carving (W) than with quick turns (no W). Quick
  turns are the speed control.
- [x] No riding number is inline in logic. The sim version is bumped, and the Story 001 determinism test still
  passes.

All eight are pinned by tests in `sim/FirstChair.Sim.Tests/` (`SnowRidingTests`, `TestSlopeTests`, `TuningValidationTests`,
`DeterminismTests`): `dotnet test sim/` 381/381 (2026-10-24). Each new test was seen failing first, against the
playtested sim (version 6) or a deliberately broken one. Seen in Linux builds 40116719127 and 40117608164 under Xvfb.
Software rendering there runs the sim in slow motion, so on-screen speeds are not real time.

## Tuning Knobs

All in `Tuning`:
- Global: `AirDrag`, `TuckAirDragFactor`, `CarveAirDragFactor`, `MaxEdgeAngle`, `EdgeRate`, `CarveRadiusMin`,
  `CarveMinSpeed`, `CarveBlendSpeed`, `CarveTurnShare`, `PivotRate`, `PivotFadeSpeed`, `SkidStiffness`,
  `PivotSlipMax`.
- Per snow type: `Drag`, `CarveGrip` (0 = can't carve), `SkidGrip`, `SkidDrag`, `PivotFactor`.

## Unity view

- `Stance.Carve` shows the new `RaceCarveLeft` / `RaceCarveRight` clips (CH_Skier, `08_poses.py`). They keep
  CarveLeft's skis and edges, with the hips lower and further forward and the hands out in front. The racer carve
  blends through the tuck as the edge comes on (`SkierPoseWeights`).
- Turns without W keep the standing carve poses.
- Snow spray (`SnowSpray`, `SnowSprayMath`): the skis throw snow the way they slide, in proportion to the sideways
  slide (speed · sin slip). Brakes throw it ahead. A clean carve slides none, so it throws none, and the two kinds of
  turn read apart. Powder throws more, ice a few chips. All numbers are in `RiderViewSettings`.
  - The first build (40117971068) showed no spray. Neutral tonemapping turns unlit white into the sunlit snow's own
    brightness (about 218 against 220 of 255), so the flakes vanished against it. They now take colours between snow
    in shade and in sun. The player logs one line the first time the spray emits.
  - Seen in build 40120266835 once the rider was up to speed (a 45 s straight first: software rendering runs the
    game in slow motion, and a first ride at 0 to 6 km/h threw almost nothing, as the spray scales with speed). Quick
    turns at about 83 km/h and a brake from 66 km/h throw a cloud of shaded flakes. A clean W carve throws none and
    leaves two thin tracks.
- Evidence, all in `production/qa/evidence/story-003-test-slope-unity-view/`: `2026-10-24-race-carve-pose.png`,
  `2026-10-24-build-40120266835-spray-brake-and-clean-carve.png` and
  `2026-10-24-build-40122603687-quick-turn-spray-at-speed.png`. In these slow-motion rides the flakes drift ahead of
  the rider: they move on wall-clock time while the sim is held to 12 steps a frame. At full frame rate both keep the
  same time and the spray trails behind.
- The same ride showed the camera losing the rider at speed: the look-ahead grows with speed to 13 m, which put the
  feet below the bottom of the screen from about 73 km/h when the camera's smoothing barely lagged (as at a low frame
  rate). The look-ahead is now capped so the feet stay at or above 85% of the screen height (story 003's camera,
  `FollowCameraSettings.LowestRiderOnScreen`). Stopped, the rider still sits 80% of the way down. Seen in build
  40122603687.

## Out of Scope

- Snowboard turning, and the art and level work (story 007).
- A wider field of view at speed: the user likes the camera as it is, so it stays unchanged until they ask.

## Test Evidence

**Story Type**: Logic. Unit tests in `sim/FirstChair.Sim.Tests/` (`SnowRidingTests`, `TestSlopeTests`).

## Dependencies

- Depends on: Story 002 (the snow-riding step it replaces), Story 003 (the test slope).
- Unlocks: closing story 003 (the user's playtest of the feel).
