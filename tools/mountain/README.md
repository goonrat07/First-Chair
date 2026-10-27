# The mountain tool

A First Chair mountain is a text file. `tools/mountain/mountain.py` builds it into the three files the sim and Unity
load, draws it as a map, checks it, and starts new ones (story 015).

```sh
python3 tools/mountain/mountain.py new     PineBowl                                  # design/mountains/PineBowl.mountain.toml
python3 tools/mountain/mountain.py check   design/mountains/pine-bowl.mountain.toml  # runs, lifts, snow, trees
python3 tools/mountain/mountain.py preview design/mountains/pine-bowl.mountain.toml  # pine-bowl.mountain.png
python3 tools/mountain/mountain.py build   design/mountains/pine-bowl.mountain.toml  # Assets/Terrain/PineBowl/
python3 -m pytest tools/mountain -q                                                  # the tool's tests
```

`build` writes `Assets/Terrain/<Name>/<Name>.raw` (the heightmap), `<Name>Snow.raw` (the snow map) and `<Name>.txt`
(the layout). Commit that folder and the mountain is in the game: the scene builder makes a scene for every mountain
folder under `Assets/Terrain/`, the build carries them all, and the Feel Lab lists every place in the build. The same
file always builds the same bytes, on any machine.

The two mountains so far are the worked examples: `design/mountains/test-mountain.mountain.toml` (story 010's test
mountain, every section used) and `design/mountains/pine-bowl.mountain.toml` (a smaller bowl shaped with features).

## The map

Positions are metres on a square map seen from above: **x** runs across it, **z** down the fall line, from the summit
side at z = 0 toward the base at z = the map's size. Heights are metres. A run, a lift or a feature is drawn from top
to bottom. Every field's name ends in its unit: `_m` metres, `_mm` millimetres, `_per_mille` thousandths, `_deg`
degrees; a list of two is `[x, z]` or a `[from, to]` range, a rectangle is `[x_min, z_min, x_max, z_max]` and a circle
is `[x, z, radius]`.

A file is read in full before anything is built: an unknown key (a typo), a missing one, a wrong type, a run off the
map or two runs with one name stop it with a message naming the problem.

## The file, section by section

Top-level keys come first, before any `[section]`.

| Key | | Meaning |
|---|---|---|
| `format` | required | `1`. |
| `name` | required | One CamelCase word, such as `PineBowl`: the folder, the files and the scene are named after it. |
| `title` | `name` | Shown on the preview and in the layout's header. |
| `start_m` | required | Where a run begins, usually on the summit. |

### `[grid]`

| Key | | Meaning |
|---|---|---|
| `resolution` | required | Samples a side, 2^n + 1 from 33 to 4097 (Unity's terrain sizes). 0.5 m a sample is fine enough to shape a kicker: 1025 over 512 m, 2049 over 1024 m. |
| `size_m` | required | The map's side. |
| `min_height_m` | `0` | The heightmap's lowest height. |
| `max_height_m` | required | Its highest. A height step is (max - min) / 65535; `build` fails if the terrain leaves the range. |

### The mountain's shape: `[profile]`, `[peak]`, `[ridges]`, `[noise]`

The mountain starts as one long slope down the fall line, as steep as `[profile]` says at each z, the same across x.

| Key | | Meaning |
|---|---|---|
| `profile.base_height_m` | required | The height at the bottom of the fall line. |
| `profile.centre_x_m` | half the map | Where the fall line runs: the peak and the ridges are measured from it. |
| `profile.gradient` | required | `[z, drop per metre]` knots, z rising; between two knots the gradient eases from one to the other. 0.45 is about 24 degrees, 0.33 about 18, 0.2 about 11, 0 level, below 0 rising. |
| `peak.drop_m` | none | How far the map's sides fall below the fall line near the top, so the summit is a peak, not a wall... |
| `peak.fade_z_m` | | ...full above the first z, gone below the second. |
| `ridges.rise_m` | none | How far the sides rise lower down, so the face reads as a bowl... |
| `ridges.start_off_centre_m`, `ridges.width_m` | | ...starting this far either side of the fall line and reaching full height over this width... |
| `ridges.fade_z_m` | | ...gone above the first z, full below the second. |
| `noise.layers` | none | Undulation: a list of `{ spacing_m, amplitude_m, seed }`, value noise from a fixed integer hash. |
| `noise.run_kept` | `0.25` | The share of the undulation an `open` or `gully` run keeps: gentle rollers, no bumps. |

### `[[features]]`: sculpting

Each feature is shaped on top of the mountain, in the file's order, before the summit, the runs and the base (which
then shape over them). Every edge eases in and out, so nothing leaves a crease.

| `kind` | Keys | What it does |
|---|---|---|
| `hill` | `centre_m`, `radius_m`, `height_m` | A rounded rise, full at its centre, gone at its radius. A negative height makes a hollow. |
| `ridge` | `points_m`, `width_m`, `height_m` | A raised line along a smooth curve through the points, full on the line, gone at half the width. |
| `valley` | `points_m`, `width_m`, `depth_m` | The same, sunk: a gully or a channel. |
| `flatten` | `centre_m`, `radius_m`, `height_m` (default: the ground at the centre), `blend_m` (`20`) | A level pad: a hut's site, a lookout, a landing. On a slope it cuts the uphill side and fills the downhill one. |

### `[summit]` and `[base]`

| Key | | Meaning |
|---|---|---|
| `summit.centre_m`, `summit.radius_m` | required | The top station's area: the fall line's own gentle top, level across x, groomed. Runs leave its surface alone. |
| `summit.blend_m` | required | It rounds over into the face across this much. |
| `summit.noise_free_m` | required | No undulation within the first distance from its centre, full beyond the second. |
| `summit.tree_free_m` | `25` | Trees keep this far beyond its edge. |
| `base.area_m` | required | The base area's rectangle: the fall line's profile, level across x, groomed, no trees. Shaped last, so every run ends on it. |
| `base.blend_m` | required | It blends into the mountain over this much. |

### `[tree_line]`

| Key | | Meaning |
|---|---|---|
| `height_m` | none (trees to the top) | Open snow above this height, as on GMA2's upper mountains. |
| `spread_m` | | Trees thin out over this much height below it, and the snow above `height_m - spread_m` is `snow.above_tree_line`. |

### `[[runs]]` and `[run_shaping]`

Runs are shaped in the file's order. An earlier run keeps its surface where a later one meets it (the later run blends
in only over the earlier one's shoulders), so put each branch after its trunk.

| Key | | Meaning |
|---|---|---|
| `name` | required | One word, unique: `kettle-run`. |
| `difficulty` | required | `green`, `blue`, `red`, `black` or `park`: its colour on signs and on the preview. |
| `width_m` | required | The groomed width; trees keep `trees.run_clearance_m` beyond it. |
| `shape` | `open` | `open` follows the mountain's fall and cross slope, smoothed; `smooth` the same with no undulation (a park); `cat` is level across its width (a cat track); `gully` is an open run carved into a channel. |
| `snow` | `groomed` | The snow on it. |
| `points_m` | required | Its centre line, top to foot: a smooth curve (centripetal Catmull-Rom) through the points. |

A run ends on another run or well inside the base area: the sim's every-mountain test
(`sim/FirstChair.Sim.Tests/EveryMountainTests.cs`) rides each run's centre line until the rider is 10 m past the base
zone's top edge (`base.area_m`'s top plus half `base.blend_m`), and a slow green that ends at the edge stops short of
that. `check` reports how far into the base area each run ends; the test mountain's end 25 to 30 m in.

`[run_shaping]` holds how runs blend in, the same for every run: `shoulder_m` per shape (`{ open = 22, smooth = 22,
cat = 14, gully = 26 }`: the surface blends back into the mountain over this), `end_fade_m` (`10`, beyond either end),
`centre_smooth_m` (`18`, a run's height and cross slope are averaged over this each way along it), `cat_cross_kept`
(`0.15`, the cross slope a cat track keeps, so it drains), `gully_depth_m` (`7`), `gully_fade_m` (`[40, 70]`, a gully
deepens over its first metres and fills in over its last) and `densify_step_m` (`2`, how finely centre lines are
sampled).

### `[snow]`

The snow map is painted in this order, a later layer winning: `background` (`crud`) below the tree line and
`above_tree_line` (`powder`) above it; the runs that are not groomed; `[[snow.run_patches]]`; the groomed runs (so a
groomer keeps its snow where another run meets it); the summit and the base, groomed; `[[snow.areas]]`. The snow types
are `groomed`, `powder`, `crud`, `ice` and `slush`.

| Key | Meaning |
|---|---|
| `[[snow.run_patches]]` | `run`, `from_m`, `to_m`, `snow`: a stretch of a run, measured along it, such as ice on a black's steepest part. |
| `[[snow.areas]]` | `snow` and `rect_m` or `circle_m`: a painted area, such as slush where riders queue for a lift or a powder stash. |

### `[park]`

| Key | | Meaning |
|---|---|---|
| `run` | required | The park's run (it should be `smooth`). |
| `kicker_lane_x_m`, `jib_lane_x_m` | required | The two lanes down the park's straight: kickers on one, rails and boxes on the other. |
| `zone_m` | required | The park's rectangle, named in the layout for the view. |
| `kickers` | | `{ lip_z_m, length_m, width_m, height_m }`, each facing straight down the fall line: a ramp rising to its lip, a short fall-away behind it. |
| `rails` | | `{ kind = "rail" or "box", from_z_m, to_z_m, height_m, width_m }`, straight down the jib lane. |
| `kicker_shoulder_m`, `kicker_back_per_height` | `1`, `2` | A kicker's rounded shoulders, and how far behind its lip it falls away, per metre of its height. |

### `[[lifts]]`

Chairlifts. The script places the towers: walking up from the bottom station, each span reaches as far as it can with
a tower no taller than `tower_easy_m`, or else as far as the shortest tower allows, and the cable clears the snow by
`min_clearance_m` everywhere but near the stations, where it comes down for riders to get on and off. `check` reports
the towers and the least clearance; a lift whose cable cannot clear the snow is an error. The game draws the first
lift in the file.

| Key | Default | Meaning |
|---|---|---|
| `name`, `bottom_m`, `top_m` | required | The lift and its stations. It can climb at an angle. |
| `station_cable_m` | `5` | The cable's height above the snow at each station. |
| `span_min_m`, `span_max_m` | `40`, `110` | The shortest and longest span between towers. |
| `tower_min_m`, `tower_easy_m`, `tower_max_m` | `8`, `14`, `26` | Tower heights. |
| `station_approach_m` | `25` | The cable comes down to each station over this much line. |
| `min_clearance_m` | `7` | Elsewhere it clears the snow by at least this. |
| `corridor_half_width_m` | `12` | Cleared of trees either side of the line. |

### `[trees]`

One tree at most in each cell of a grid over the whole map, kept or not by a fixed hash: none on a run (with
clearance), a lift's corridor, the summit or the base, or above the tree line.

| Key | Default | Meaning |
|---|---|---|
| `cell_mm` | `8000` | The grid's cell. |
| `seed` | `21` | A different seed grows a different forest. |
| `keep_near_per_mille`, `keep_far_per_mille` | `850`, `620` | How many cells keep a tree within `near_m` (`40`) of a run, and farther off: dense by the runs, so runs read as runs. |
| `jitter_mm` | `2500` | How far a tree strays from its cell's centre. |
| `trunk_mm`, `branch_mm` | `[180, 380]`, `[1400, 1450]` | Trunk and canopy radius ranges. The jitter and the canopy must keep each tree inside its cell. |
| `run_clearance_m` | `2` | A canopy keeps this far off a run's edge. |
| `glades` | none | `{ area_m, keep_per_mille }`: an open stand to ride through. |

## Why the format looks like this

- TOML, because a mountain file explains itself in comments and Python reads it with no extra package (`tomllib`).
- The shaping is story 010's test-mountain generator with its constants named in the file; the test mountain's file
  builds exactly its old heightmap and snow map, byte for byte (`test_mountain.py` holds their SHA-256).
- Every shape is built from + - * /, square roots and clipping, and every random choice from an integer hash, so a
  file builds the same bytes everywhere: the sim's replays depend on the heightmap being the same.
