# Story 015 evidence: a mountain design tool

The tool's checks ran in a cloud session on 2026-10-27 (Python 3 with numpy and Pillow, .NET 10 for the sim tests).
The build is `unity.yml` run 40510043401 (commit a399708, the tool and Pine Bowl): EditMode 238/238, the Linux and
Windows players build, and the Linux player carries `PineBowl/` in its StreamingAssets beside `TestSlope/` and
`TestMountain/`. It was ridden under Xvfb with Mesa's software renderer, which draws about 0.75 frames a second, so
a ride there runs in slow motion. No Unity Editor has opened this project.

| What | Result | Evidence |
|---|---|---|
| The format, field by field | `tools/mountain/README.md`: every key with its unit, default and meaning; `design/mountains/test-mountain.mountain.toml` is the worked example | the README |
| The test mountain, byte for byte | `build` writes `TestMountain.raw` and `TestMountainSnow.raw` identical to story 010's, and the layout identical but for its first comment line, which now names the tool and the file (`test_committed_mountain_files_match_a_fresh_build`, `test_test_mountain_heights_and_snow_are_story_010s`); story 010's 20 generator tests pass through the wrapper | pytest |
| The tool's tests | 31 in `tools/mountain/test_mountain.py`: the format and its defaults, the errors it names, the four feature kinds, `check`'s errors (a lift the cable can't span, heights out of range), the run report, two builds byte for byte, the preview, `new` | pytest: 66 passed with `tools/terrain/` |
| A second mountain, made only with the format | Pine Bowl: 512 m, a blue, a red and a green cat track, a gully, a knoll with a powder stash, a lookout pad, slush at the lift, a diagonal chairlift with 4 towers, 1,816 trees. `check` passes | `2026-10-27-pine-bowl-preview.png`, `2026-10-27-check.txt` |
| The test mountain's preview | The same map from the test mountain's file | `2026-10-27-test-mountain-preview.png` |
| Every mountain ridden by the sim | `EveryMountainTests` loads each mountain under `Assets/Terrain/` and rides each run's centre line with a scripted rider to the base area. Pine Bowl's green first stopped at the base area's edge (its runs ended 1 to 3 m inside it); the runs now end 25 m in, as the test mountain's do, and `check` reports how far in each run ends | `dotnet test sim/`: 435 passed |
| The Feel Lab lists the new mountain | The Controls tab's places are the build's scenes in build order: "Ride the test slope", "Ride the pine bowl", "Ride the test mountain". No code names Pine Bowl | `2026-10-27-build-40510043401-feel-lab-places.png` |
| Pine Bowl from the chase camera | Started with `--scene PineBowl`, restarted at the top and ridden from the summit: the groomed summit plateau (3 to 4 degrees), the diagonal chairlift climbing in from the left, a run marker and the trees below, and the ski tracks behind the rider. In the cloud's slow motion the rider reaches 3 km/h on the plateau; riding the runs down is for the PC | `2026-10-27-build-40510043401-pine-bowl-ride.png` |
| No change to the sim's riding or the layout format | The sim's code is untouched (only its tests changed); the layout format is story 010's | the diff |
