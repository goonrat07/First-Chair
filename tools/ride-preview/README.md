# ride-preview

Runs First Chair's riding sim (`Packages/com.firstchair.sim`, built by `sim/FirstChair.Sim/FirstChair.Sim.csproj`)
on the committed test slope (`Assets/Terrain/TestSlope/`) with scripted inputs, and writes the rides as one JSON
file for the web viewer. No Unity needed: the .NET 10 SDK is enough.

The tool only reads the sim. Nothing it computes goes back into a ride, so it may use `double` and `System.Math`.
It is not in `sim/FirstChair.Sim.sln`, so `dotnet test sim/` does not build it.

## Run

From the repo root (the slope files must be pulled from Git LFS):

```sh
dotnet run --project tools/ride-preview -c Release              # writes tools/ride-preview/out/rides.json
dotnet run --project tools/ride-preview -c Release -- my.json    # writes my.json instead
dotnet run --project tools/ride-preview -c Release -- --probe-kickers   # kicker diagnostic table, no JSON
```

It prints one summary per ride (end point, speeds, event counts, skid share by snow type and script phase, and
every event except near misses) and takes a few seconds. `out/` is gitignored. The file is written to
`rides.json.tmp` and moved into place, so a failed run never leaves a half-written file. Exit code 1 means a
reported error: a missing slope file, an LFS pointer instead of the file, or a layout without a zone a ride needs.

## Rides

| Name | Start (from the layout) | What it does |
|---|---|---|
| `groomer-tuck` | the layout's `start` | Tucks straight down the groomer, stands up in the run-out. |
| `groomer-carve` | the layout's `start` | Full-lock left/right turns on a timer, in blocks of wide (2.1 s) and tight (0.6 s) turns. |
| `park-line` | 30 m before the first kicker's foot | Japan grab off kicker 1, box, 360 off kicker 2, pop onto the rail, backflip off kicker 3, box. Holds an approach speed per kicker by braking. |
| `glade-line` | 50 m above the glade, at its centre x | Follows a line through the trees found by a beam search with the sim in the loop, scored for near misses against branch clips and crashes. |
| `ice-stop` | 70 m above the ice patch | Tucks in, crosses the ice on a diagonal, brakes to a stop below it. |

Steering is a proportional controller toward a point a little way ahead on a line (`Steering.Toward`), quantized
into `SimInput` before it reaches the sim. Script numbers are constants at the top of `Rides.cs`. To add a ride,
add a `RideScript` factory there and yield it from `Rides.All`; a script's callbacks may keep state, so build a new
script for every run.

## rides.json

Units: metres, seconds, m/s. x runs along heightmap columns, z along rows (z = 0 is the top, the fall line runs
toward +z), y is up. Angles in samples are radians; the event air angles are degrees (`...Deg`). Numbers carry at
most 3 decimals (heights in the grid 2).

```text
format: 1
sim:    { version, stepsPerSecond: 120, sampleEverySteps: 4 }
units:  { ...plain-text unit notes... }
slope:
  sizeX, sizeZ, minHeight, maxHeight
  grid:      { resolution: 257, cellX, cellZ, order }   row-major, index = row * 257 + column
  heights:   [257*257] terrain height, m (2 decimals)
  snow:      [257*257] snow type 0-4
  snowTypes: { "0": "Groomed", "1": "Powder", ... }
  background, start: { x, z }
  zones:   [{ name, snow, xMin, zMin, xMax, zMax }]
  kickers: [{ x, z, y, facing (deg, 0 = +z), length, width, height }]   x, z = the lip
  trees:   [{ x, z, y, trunk, branch }]                                 radii, m
  rails:   [{ kind: "rail" | "box", x0, z0, x1, z1, height, width }]
rides: [{
  name, description, start: { x, z }, duration (s), end ("script done" | "left heightmap" | "time cap"),
  score (RunScore.Total), counts: { <event kind>: n },
  samples: [{ t, x, y, z, speed, heading, edge, stance, airborne, grinding, crashed, flow, skidding, snow }],
  events:  [{ tick, t, kind, x, y, z, ...kind data }],
  tricks:  [{ tick, name, points }]
}]
```

Samples come every 4 steps (30 Hz) plus the last step. `heading` is clockwise seen from above, 0 = +z, pi/2 = +x,
wrapped to [-pi, pi]; `edge` is signed like steering (+ = right); `stance` is `Stand`, `Tuck`, `Brake` or `Pole`;
`flow` is 0 to 1. An event's x, y, z is the rider's position after the step that raised it. Event data by kind:

| kind | data |
|---|---|
| `Takeoff` | `speed` |
| `Land` | `grade`, `airTime`, `maxHeight`, `spinDeg`, `flipDeg`, `grab`, `grabTime`, `landingAngleDeg`, `switch`, `flow`, and `trick`, `points` (`repeat`) when `RunScore` scored it |
| `Crash` | `cause`; a tree crash adds `tree`, `speed`; a rail fall adds `rail`; a landing crash adds the air data |
| `NearMiss` | `tree`, `gap` (m), `speed` |
| `BranchHit` | `tree`, `speed` |
| `GrindStart` | `rail` (index into `slope.rails`), `box` |
| `GrindEnd` | `rail`, `box`, `fell`, `seconds`, `balance`, `trick`, `points` |
| `Reset`, `LeftHeightmap` | none |
