# Story 008: Turn styles and speed: carve, quick turns, powder, faster slopes

> **Epic**: First Chair MVP (base skiing system)
> **Status**: In Progress
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

- [ ] Holding W while steering on groomed snow carves. The velocity stays on the ski heading with no skid, and the
  speed stays within friction and drag of a straight run.
- [ ] A carve's tightest radius grows with speed: at full edge the radius is CarveRadiusMin when slow, and
  v² / CarveGrip at speed.
- [ ] Below CarveMinSpeed, W plus steering pivots like a quick turn.
- [ ] Steering without W pivots the skis faster than a carve turns at the same moderate speed, loses more speed
  than a carve over the same change of direction, and pivots more slowly the faster the rider goes.
- [ ] Powder and crud never carve: W plus steering there turns like a quick turn. Each snow type reads its own
  carve grip, skid grip, skid drag, pivot factor and friction from tuning data.
- [ ] On the test slope, from the start with no input, the rider passes 50 km/h within 8 s and 75 km/h within 12 s on
  the groomer, and straight down the tree lines and the glade passes 50 km/h within 8 s without W.
- [ ] Key S-turns down the groomer hold a higher average speed carving (W) than with quick turns (no W). Quick
  turns are the speed control.
- [ ] No riding number is inline in logic. The sim version is bumped, and the Story 001 determinism test still
  passes.

## Tuning Knobs

All in `Tuning`:
- Global: `AirDrag`, `TuckAirDragFactor`, `CarveAirDragFactor`, `MaxEdgeAngle`, `EdgeRate`, `CarveRadiusMin`,
  `CarveMinSpeed`, `CarveBlendSpeed`, `CarveTurnShare`, `PivotRate`, `PivotFadeSpeed`, `SkidStiffness`,
  `PivotSlipMax`.
- Per snow type: `Drag`, `CarveGrip` (0 = can't carve), `SkidGrip`, `SkidDrag`, `PivotFactor`.

## Out of Scope

- Snowboard turning, and the art and level work (story 007).
- Visual feedback for speed (snow spray, field of view at speed) is presentation. It follows in the Unity view
  under story 003.

## Test Evidence

**Story Type**: Logic. Unit tests in `sim/FirstChair.Sim.Tests/` (`SnowRidingTests`, `TestSlopeTests`).

## Dependencies

- Depends on: Story 002 (the snow-riding step it replaces), Story 003 (the test slope).
- Unlocks: closing story 003 (the user's playtest of the feel).
