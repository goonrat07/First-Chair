# Story 016 evidence: a varied forest

The trees were built, gated and exported in a cloud session on 2026-10-27 (Blender 5.2 headless, the tree kit's
phases through `tools/blender/trees/phases.sh`). The forest was ridden in the cloud builds of cadd45d ("before", one
spruce: `unity.yml` run 40522231518) and 81380ac ("after": run 40582924145), under Xvfb with Mesa's software renderer, which draws under a frame a
second, so the views are pinned (the Feel Lab's gravity at 0.001). No Unity Editor has opened this project.

| What | Result | Evidence |
|---|---|---|
| Each new tree passes the kit's gates | Slim fir, broad spruce, young spruce and snag: front, side and top IoU 0.985 to 0.998 at LOD0 (minimum 0.90), LOD1 0.72 to 0.97 (minimum 0.70), tris in budget, proportions within 2 percent, origin at the trunk base | `art/source/<ASSET>/review/final/gates.json`, `diff_*.png` |
| Each exports with its LODs and a billboard | One FBX per tree with `_LOD0` and `_LOD1`; export check 30 of 30 and the FBX round trip (heights 14.0, 11.0, 4.5 and 9.0 m) pass; billboards IoU 0.983 to 0.996, none cut off by its cell | `review/final/export_check.json`, `roundtrip.json`, `gate_billboard.json`; `2026-10-27-tree-billboards.png` |
| The four shapes | Look renders from the kit (three-quarter, and a cluster from the game camera's height) beside the spruce | `2026-10-27-tree-shapes-look-renders.png` |
| The choice is pure C# with tests | `TreeVariantPicker`: 11 EditMode tests (the same position, the mix within 1.5 points over 40,000 trees, neighbours differ, a zero weight never chosen, the scale's fit, limits and height cap); the Core tests run locally under NUnit: 237 passed | `Assets/Tests/EditMode/TreeVariantPickerTests.cs` |
| CI | Run 40582924145 (81380ac): EditMode 249/249 (the 11 `TreeVariantPickerTests` among them), the Linux and Windows players build (0 errors), every variant's FBX and billboard imports and none is logged missing. Run 40579929851 (55ad865, before the exports) logged each variant missing and fell back to the spruce, as designed | the runs |
| The forest from the chase camera | Pinned at the park's top, where tree lines run along the groomer and the park: before (cadd45d) every tree is the one spruce; after (81380ac) the lines mix tall slim firs, with long bare trunks and sparser tiers, among fuller and broader spruces, so neighbours differ in height and density. The repeated shot changed 0.18 levels, so the frame held still. From the test slope's start the tree lines are too far off to tell much apart | `2026-10-27-park-before-after.png` (both, and the left tree line at 2x), `2026-10-27-build-40522231518-park-before.png`, `2026-10-27-build-40582924145-park-after.png`, the `-start-` pair |
| The mix on the mountains | The picker over every layout tree (its hash done again in Python): the test slope's 938 trees are 43.7 percent spruce, 25.9 slim fir, 20.7 broad, 6.5 young and 3.2 snags; the test mountain's 5,968 and Pine Bowl's 1,816 are within 1.8 points of the mix | this file |
| No change to the sim or the layout format | `dotnet test sim/`: 435 passed; `pytest tools/mountain tools/terrain`: 66 passed, the mountains rebuilt byte for byte; no file under `Packages/com.firstchair.sim/` or `Assets/Terrain/` changed | the diff |

Not seen in the cloud: the forest in motion and at a real GPU's frame rate, and whether the slim fir reads too sparse
(EN_Conifer's brief: the spruce's first version "read too sparse, like a fishbone"). Both are for the user's look review on the PC.
