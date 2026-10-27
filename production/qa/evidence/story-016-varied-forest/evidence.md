# Story 016 evidence: a varied forest

The trees were built, gated and exported in a cloud session on 2026-10-27 (Blender 5.2 headless, the tree kit's
phases through `tools/blender/trees/phases.sh`). The forest was ridden in the cloud builds of cadd45d ("before", one
spruce: `unity.yml` run 40522231518) and 81380ac ("after": run 40582924145, pending), under Xvfb with Mesa's software renderer,
which draws under a frame a second, so the views are pinned (the Feel Lab's gravity at 0.001). No Unity Editor has
opened this project.

| What | Result | Evidence |
|---|---|---|
| Each new tree passes the kit's gates | Slim fir, broad spruce, young spruce and snag: front, side and top IoU 0.985 to 0.998 at LOD0 (minimum 0.90), LOD1 0.72 to 0.97 (minimum 0.70), tris in budget, proportions within 2 percent, origin at the trunk base | `art/source/<ASSET>/review/final/gates.json`, `diff_*.png` |
| Each exports with its LODs and a billboard | One FBX per tree with `_LOD0` and `_LOD1`; export check 30 of 30 and the FBX round trip (heights 14.0, 11.0, 4.5 and 9.0 m) pass; billboards IoU 0.983 to 0.996, none cut off by its cell | `review/final/export_check.json`, `roundtrip.json`, `gate_billboard.json`; `2026-10-27-tree-billboards.png` |
| The four shapes | Look renders from the kit (three-quarter, and a cluster from the game camera's height) beside the spruce | `2026-10-27-tree-shapes-look-renders.png` |
| The choice is pure C# with tests | `TreeVariantPicker`: 11 EditMode tests (the same position, the mix within 1.5 points over 40,000 trees, neighbours differ, a zero weight never chosen, the scale's fit, limits and height cap); the Core tests run locally under NUnit: 237 passed | `Assets/Tests/EditMode/TreeVariantPickerTests.cs` |
| CI | Pending: run 40582924145 (81380ac). Run 40579929851 (55ad865, before the exports) built both players and logged each variant as missing, as designed | the runs |
| The forest from the chase camera | Before (cadd45d): the test slope's start and the park, pinned, every tree the one spruce. After: pending the new build | `2026-10-27-build-40522231518-start-before.png`, `-park-before.png` |
| No change to the sim or the layout format | `dotnet test sim/`: 435 passed; `pytest tools/mountain tools/terrain`: 66 passed, the mountains rebuilt byte for byte; no file under `Packages/com.firstchair.sim/` or `Assets/Terrain/` changed | the diff |
