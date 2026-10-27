# Story 015: A mountain design tool (a mountain as a text file, and the scripts that build it)

> **Epic**: First Chair MVP (base skiing system)
> **Status**: In Review
> **Layer**: Tools
> **Type**: Logic (format, builder, checks) + Visual/Feel (previews, the new mountain in a build)
> **Estimate**: L
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: 2026-10-27

## Context

**GDD**: `design/game-brief.md`, "After a rough gameplay demo": "A mountain-making tool: shape the terrain and place
props such as trees, usable by the user, later by players for custom mountains (GMA2's coming Steam version has
custom mountains), and directly by Claude through a data format and scripts, so building a mountain is quicker and
looks better. Add the AI side only if it pays for itself in quality or speed."
**Requirement**: the user, 2026-10-27, starting sleep mode: "develop the mountain designing tool".

Today the test mountain (story 010) is `tools/terrain/make_test_mountain.py`: about 140 constants (the fall line's
profile, the peak and ridges, the noise, the summit and base areas, five runs, the park, the lift, the trees and
glades) and the code that shapes them into the heightmap, snow map and layout the sim and Unity load. A second
mountain today means copying the script and editing Python, and Unity names the test mountain in three places.

Written by Claude in sleep mode (the user asked for this work); the design choices are logged under Decisions and
are the user's to change.

## Design

This story is the first part of the tool: **a mountain is a text file**, and scripts build, preview and check it.
The user and Claude edit the file; a later story can put an editor on top of the same format (in Unity, and in the
game for players).

1. **The format.** A mountain is a commented TOML file, `design/mountains/<name>.mountain.toml` (`format = 1`):
   - `[grid]`: the map's size, resolution and height range;
   - `[profile]`, `[peak]`, `[ridges]`, `[noise]`: the mountain's shape down the fall line, its summit falling away
     to the sides, the side ridges that make a bowl, and the undulation;
   - `[[features]]`: sculpting on top: a `hill` (a rounded rise), a `ridge` or a `valley` along a line, a `flatten`
     (a level pad), each with its size and a soft edge;
   - `[summit]`, `[base]`: the top station's level area and the base area;
   - `[[runs]]`: each run's centre line, width, difficulty colour, shape (`open`, `smooth`, `cat` track or `gully`)
     and snow; `[run_shaping]`: how runs blend into the mountain;
   - `[snow]` and `[[snow.areas]]`: the snow off the runs, the tree line, and painted areas (a rectangle or a circle,
     or a stretch of a run, such as ice on the black);
   - `[park]`: kickers, rails and boxes on the park's lanes;
   - `[[lifts]]`: the bottom and top stations; the towers are placed by the script so the cable clears the snow;
   - `[trees]`: the forest's density near and far from the runs, the tree line and `[[trees.glades]]`;
   - `start`: where a run begins.
   Every value has a unit in its name (`_m`, `_mm`, `_per_mille`) and a comment; values a mountain leaves out take
   documented defaults, so a small mountain is a short file.
2. **The scripts**, `tools/mountain/mountain.py`:
   - `build <file>` writes `Assets/Terrain/<Name>/<Name>.raw`, `<Name>Snow.raw` and `<Name>.txt` in the formats the sim
     and Unity already load (no new code in the sim). Deterministic: the same file gives byte-identical output on
     any machine.
   - `preview <file>` draws the mountain as a map: shaded relief, the snow in colour, the runs in their difficulty
     colours, the trees, the lift and its towers, the park and the start, with a scale bar, so a design can be looked
     at without Unity.
   - `check <file>` reports each run's length, drop, average and steepest slope, the tree count and the lift's towers
     and least clearance, and fails with a message naming the problem (a run off the map, two runs with one name, a
     height out of range, a lift the cable cannot span).
   - `new <name>` writes a starter mountain (a hill, one run, a lift) to edit.
3. **The test mountain moves into the format**, `design/mountains/test-mountain.mountain.toml`, and building it gives
   byte-identical files to today's (`make_test_mountain.py` stays as a thin wrapper, so its tests keep running).
4. **Unity finds mountains instead of naming them.** The scene builder makes a scene for every mountain folder under
   `Assets/Terrain/` (besides the test slope), the build carries them all, and the Feel Lab lists every place in the
   build. A new mountain needs no code.
5. **A second mountain made only with the tool**, smaller and different in shape, to prove the format: built,
   checked, previewed and ridden in a cloud build.

## As built (2026-10-27)

- **The tool** (a399708): `tools/mountain/mountain.py` with `build`, `preview`, `check` and `new`, the format field by
  field in `tools/mountain/README.md`, and 31 pytest tests in `tools/mountain/test_mountain.py`: reading the format
  and its defaults, the four feature kinds, the errors `check` raises, two builds byte for byte, the committed
  mountains rebuilt byte for byte.
- **The test mountain** is `design/mountains/test-mountain.mountain.toml` and builds byte for byte as story 010's
  generator did. `tools/terrain/make_test_mountain.py` is now a thin wrapper, so story 010's 20 tests still run. Only
  the layout's first comment line changed: it names the tool and the file.
- **Pine Bowl** (`design/mountains/pine-bowl.mountain.toml`), made only with the format: 512 m square, a blue down
  the middle, a red along the east spine, a green cat track switching back across the west face past a gully, a knoll
  with a powder stash, a level lookout pad, slush where riders queue, and a chairlift climbing at an angle from the
  base's west side to the summit (4 towers, the cable at least 7 m above the snow), with 1,816 trees.
- **Found by the sim tests.** `EveryMountainTests` rides every run of every mountain with the scripted rider, and
  Pine Bowl's green stopped at the base area's edge: its runs ended 1 to 3 m inside it (the test mountain's end 25 to
  30 m in), and the slow cat track arrived with no speed to carry it the 10 m in that the test rides. The runs now
  end 25 m in, and `check` reports how far into the base area each run ends.
- **Unity.** `MountainCatalog` lists each folder under `Assets/Terrain/` that holds a mountain's three files. The CI
  build makes a scene for each (`TestSlopeSceneBuilder.BuildMountainScenes`), carries them all and copies their files
  into StreamingAssets, and the Feel Lab's places are the build's scenes. CI run 40510043401 built both players with
  Pine Bowl in StreamingAssets. Not verified in the editor.

## Acceptance Criteria

- [x] The format is documented field by field, with units and defaults (`tools/mountain/README.md`), and the test
  mountain is its worked example.
- [x] `build` on `design/mountains/test-mountain.mountain.toml` writes files byte-identical to the committed
  `Assets/Terrain/TestMountain/` ones (a test), and the existing test-mountain tests still pass.
- [x] Tests (pytest) cover reading the format and its defaults, each feature kind, the errors `check` reports, and
  determinism (two builds, identical bytes).
- [x] A second mountain, made only with the format, builds, passes `check`, and its preview is retained in
  `production/qa/evidence/story-015-mountain-design-tool/`.
- [x] In a cloud build, the Feel Lab lists the new mountain with no code naming it, and it is ridden from the chase
  camera (a retained screenshot).
  *Met on the summit plateau only: the cloud draws about 0.75 frames a second, so the ride covers a few metres;
  riding the runs down is for the PC.*
- [x] `dotnet test sim/` passes; no change to the sim's riding or the layout format.

## Out of Scope

- An editor with a UI (a Unity editor window, or an in-game editor for players): a later story on this format.
- AI generation of mountains (the brief: only if it pays for itself).
- New prop kinds beyond trees, kickers, rails, boxes and lifts; the full Kettle Peak mountain.

## Test Evidence

**Story Type**: Logic + Visual/Feel. Logic: `tools/mountain/test_mountain.py` (pytest). Visual: preview images and
a cloud-build screenshot in `production/qa/evidence/story-015-mountain-design-tool/`; the look is the user's call.

## Dependencies

- Depends on: Story 010 (the test mountain, its generator and the layout records).
- Unlocks: an editor UI for mountains; custom mountains for players.

## Decisions (sleep mode, 2026-10-27; the user's to change)

- TOML for the format: it allows comments (so a mountain file documents itself), Python reads it with no extra
  package (`tomllib`), and it reads well by hand. JSON has no comments; YAML needs a package and has traps.
- The test mountain's algorithm is kept, not rewritten: the format names its constants, so the refactor is checked
  byte for byte against today's files before anything new is added.
- Scenes for mountains are found by folder, so adding a mountain is a data change, as the brief asks.
