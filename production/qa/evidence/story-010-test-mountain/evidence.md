# Story 010 evidence: a small test mountain with a chairlift, runs, trees and a park

Build `unity.yml` run 40287291873 (commit 75eeeb4): EditMode tests, the Linux player and the Windows player all pass.
The Linux player was ridden in a cloud session on 2026-10-26 (Xvfb, Mesa llvmpipe software rendering, xdotool for
the keys and the mouse), started with `--scene TestMountain`. No Unity Editor has opened this project, and nobody has
played this build on the PC yet.

| What | Result | Source |
|---|---|---|
| Unity compiles every script; EditMode tests pass; both players build | Pass | run 40287291873 |
| The generator's files, byte-identical; shape, smooth rideable runs, trees off runs, lift line and tree line, cable clearance, kickers and rails | `python3 -m pytest tools/terrain`: 35 passed | `tools/terrain/test_make_test_mountain.py` |
| The sim reads `run`, `lift` and `tower` and rejects bad ones; the test slope as before; determinism golden unchanged | `dotnet test sim/`: 431/431 | `sim/FirstChair.Sim.Tests/SlopeLayoutTests.cs` |
| Each run ridden down its centre line to the base without a crash; the green the gentlest; the rider starts facing down the fall line | Pass (`TestMountainTests`) | `sim/FirstChair.Sim.Tests/TestMountainTests.cs` |
| The mountain loads: the start beside the lift, chairs on the rope, run markers and signs, old tracks on the runs | Seen | `2026-10-26-build-40287291873-top-station-start.png` |
| The park: "Go to the park" starts above the first kicker; the rider rolls down the kicker lane, past the box on the jib lane, over the first kicker and into the air | Seen | `2026-10-26-build-40287291873-park-kicker-line.png` |
| The lift: "Go to the lift", a short push into the bottom station, the chair, the ride up with the HUD hidden, a new run at the top | Seen: boarded after a 3 m push, rode to the top, got off into a new run beside the lift | `2026-10-26-build-40287291873-lift-loop.png` |
| The gamepad's south button hurrying the lift | Not verified | needs the user's PC |
| The look and the lift ride | Not judged | the user, on the PC |

Notes:
- Software rendering runs the game slower than real time (the forest is heavy for it), so the lift took minutes of
  wall time with W held; on a PC it is about 30 s hurried, 150 s not.
- An earlier build (run 40282956103) found two faults that this build fixes: "Go to the lift" started 25 m out on
  the station's flat hollow, so the rider stood still and needed a long push, with no hint; and the HUD showed the
  last speed all the way up the lift. The build before that (run 40281339077) found the runs' start levels cutting a
  trough into the summit, which started the rider 51 degrees off the fall line.

`stamp.txt` holds the content hashes, at commit 75eeeb4, of the files these screenshots prove.

## Sign-off (Visual/Feel)

- [ ] Approved: the mountain reads as a small resort: runs, forest, park, lift (the user, on the PC)
- [ ] Approved: riding the lift back up works and feels right (the user)
