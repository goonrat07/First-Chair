# Story 010: A small test mountain with a chairlift, runs, trees and a park

> **Epic**: First Chair MVP (base skiing system)
> **Status**: In Review (built and seen in a build; the look and the lift ride are yours to judge on the PC)
> **Layer**: Feature
> **Type**: Logic (layout, generator, rideability) + Visual/Feel (scene, lift, markers)
> **Estimate**: L
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: 2026-10-26

## Context

**GDD**: `design/game-brief.md` ("one test slope"; "a mountain-making tool" in the user's 2026-10-23 ideas)
**Requirement**: the user, 2026-10-26, before sleep mode: after story 009, keep working, "maybe visual changes like the
snow shaders or like starting on a small test mountain with a lift, runs, trees, park, etc."

This overrides the brief's and the epic's "lifts and the full mountain" out-of-scope line for a small test mountain.
The full Kettle Peak mountain, walking and the village stay out of scope. The test slope stays as it is: the
mountain is a second place to ride, picked in the Feel Lab.

**Engine**: Unity 6.3 | **Risk**: MEDIUM (a lot of Unity-side scene work written without the editor). The sim's riding
is unchanged; only the layout format gains records.

---

## Design

### The mountain (generated, like the test slope)

`tools/terrain/make_test_mountain.py` writes `Assets/Terrain/TestMountain/`: `TestMountain.raw` (R16, 2049 x 2049 over
1024 m, 0.5 m a sample), `TestMountainSnow.raw` and `TestMountain.txt`, in the test slope's formats, so the sim and
Unity load it with the same code. Byte-identical on every run (only + - * / and clipping, integer hashes for the noise
and the trees).

- **Shape:** about 280 m of vertical. A rounded summit with the top station, a steep upper face (about 24°), a long
  middle (about 18°), an easing lower mountain (about 11°) and a near-level base area. The sides rise into wooded
  ridges, so the face reads as a bowl. Gentle undulation from a hashed value noise, smoothed out of the runs.
- **Runs** (the layout's new `run` records: a centre line and a width, GMA-style difficulty colours):
  - green *Meadow Way*: a cat track winding across the face in switchbacks, its surface level across its width;
  - blue *Kettle Run*: the wide main groomer down the middle-east with rollers;
  - red *Ridge Run*: down the west-centre, steeper, following the fall line;
  - black *The Chute*: a steep gully on the west side, with an icy patch;
  - park *Kettle Park*: a groomed lane on the east with a kicker line (four sizes) and a rail and box line.
- **Off-piste:** a powder bowl under the summit's west side; powder above the tree line; crud between the runs lower
  down; slush at the base.
- **Trees:** dense forest between and beside the runs, lighter glades in places, none on the runs, the lift line, the
  base area or above the tree line.
- **The chairlift** (the layout's new `lift` and `tower` records): a double chair straight up the middle from the
  base to the summit, its towers placed and sized so the cable stays well clear of the snow, its line cleared of trees.
- **Start:** the top station's unload area.

### The sim

The layout format gains three records the sim parses and exposes but never rides on: `run <name> <difficulty> <width>
<x0> <z0> <x1> <z1> ...`, `lift <name> <bottomX> <bottomZ> <topX> <topZ>` and `tower <lift> <x> <z> <height>`.
Riding is unchanged, so the sim version stays 8. `TestSlopeData` loads a slope by name, so the same code loads
`TestSlope` and `TestMountain`.

### Unity

- A second scene, `TestMountain.unity`, built by the same scene builder: terrain, snow layers, trees, park features,
  old tracks down each run (the test slope's S-turns bent round the run's curves, clear of its edges and the park's
  kickers, rails and boxes; narrow runs get shorter turns), the rider, camera, HUD and Feel Lab.
- The chairlift: stations at both ends, towers, the cable, and chairs moving round the loop.
- Run markers: coloured poles along the runs' edges and a sign at each run's top, as in GMA2.
- **Riding the lift:** at the base station, slow down in the boarding area and the rider sits on the next chair and
  rides it to the top (hold W to speed the lift up), then starts a new run at the top station.
- **Picking a place:** the Feel Lab gets "Test slope" and "Test mountain" buttons; `-mountain` on the command line
  starts on the mountain.

## As built (2026-10-26, sleep mode)

- **The mountain** (commits 832ada8, 76d14ad, 2d3eafc): `tools/terrain/make_test_mountain.py` writes 2049 x 2049
  samples over 1024 m, from the summit at 269 m to the base at 12 m. Five runs: Meadow Way (green, an 11 m cat track
  winding 1814 m across the face), Kettle Run (blue, 50 m wide, 847 m), Ridge Run (red, 40 m, 840 m), The Chute (black,
  a 34 m gully with an icy patch, 383 m, ending on Kettle Run) and Kettle Park (56 m, 726 m: four kickers from 0.8 to
  2.2 m on the kicker lane, two boxes and two rails on the jib lane). 5968 trees in forest and glades, none on a run,
  the lift line, the summit, the base or above the tree line (205 m). The chairlift runs straight up the middle from
  (512, 952) to (512, 50) on 8 towers (8 to 12.1 m); the cable clears the snow by 7 m or more between the stations'
  approaches. The rider starts 28 m below the top station, beside the lift line, facing straight down the fall line.
- **Faults the rides found, fixed in the generator:** a run ending on another left a lip that launched a fast rider
  (an earlier run now keeps its surface); a noise bump stalled the green at its start (no noise near the summit); the
  level summit stood proud of the face; uneven spline spacing kinked the park; and, seen in a build, each run's start
  level spread behind it as a flat disc, cutting a trough 4 to 6 m deep into the summit where riders start (the summit
  now claims its own surface, and a run fades out within 10 m beyond its ends).
- **The sim** (commit 1f60167): `run`, `lift` and `tower` records, parsed and checked, never ridden on; the sim version
  stays 8 and the determinism golden is unchanged.
- **Unity** (commits 03b8b04, 17ef94b, bb48b3b, 5e1d06b, dbd2114, 75eeeb4): the `TestMountain` scene; the chairlift
  (towers, stations, the rope as a line, chairs running round it on `CableLoop`); run markers every 30 m and a sign at
  each run's top (`RunLine`); old tracks down each run, bent round its curves and clear of the park's features
  (`RunTracks`); riding the lift (`LiftRide`): ski or push into the bottom station, wait for a chair, ride it up (W
  hurries it five times over) and start a new run at the top, with a hint near the station and the HUD hidden on the
  chair; the Feel Lab's Controls tab rides either place and has "Go to the lift" and "Go to the park".
- **Not built:** walking, lift lines, the village, NPC skiers; the lift and stations are primitives (a Blender-built
  lift can replace them).

## Acceptance Criteria

- [x] The generator writes the three mountain files, byte-identical on every run; its tests check the shape (vertical,
  pitches), that each run's surface is smooth and rideable, that no tree stands on a run, the lift line or above the
  tree line, and that the lift's cable clears the snow everywhere.
- [x] The sim parses the `run`, `lift` and `tower` records and rejects bad ones; the test slope parses as before; the
  determinism golden is unchanged.
- [x] From the start, a scripted rider can ride each run's centre line to the base area without crashing, and the
  green run stays the gentlest.
- [x] Each kicker in the park sits on its ramp, and each rail and box on the park's lane.
- [x] In a build, the mountain loads with its trees, the lift with moving chairs, the run markers and the park
  (retained screenshot).
- [x] In a build, riding into the base station's boarding area takes the rider up the lift and starts a new run at the
  top (retained screenshots).

## Out of Scope

- The full Kettle Peak mountain, other lifts, walking, the village, NPC skiers, lift lines.
- A mountain-making tool (the generator is code, not a tool for the user yet).
- Any change to riding.

## Test Evidence

**Story Type**: Logic + Visual/Feel. Python tests in `tools/terrain/test_make_test_mountain.py`; sim tests in
`sim/FirstChair.Sim.Tests/`; retained screenshots in `production/qa/evidence/story-010-test-mountain/`.

## Dependencies

- Depends on: Story 003 (the test slope's formats and scene builder), Story 007 (the look), Story 009 (the Feel Lab).
- Unlocks: a mountain-making tool; more lifts and runs.
