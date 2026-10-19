# First Chair overrides

Read this before Phase 0. For First Chair it replaces the skill's defaults where the two
disagree. Upstream: blender-game-skills at `f0ef293` (see `THIRD_PARTY_NOTICES.md`).

## Target

- **Engine and format:** Unity 6.3 LTS (URP), **FBX** for every asset, and always pass
  `--engine unity` to `export_delivery.py`.
- **Units and axes:** unchanged from the skill: 1 unit = 1 m, Z up, the subject faces -Y,
  origin at ground contact. In Unity that becomes Y up with the subject facing +Z.
- **Game camera:** a high, pulled-back follow camera like Grand Mountain Adventure 2's.
  Until the Unity camera exists, review with `--cam-elev 45 --cam-az 30 --cam-dist 18
  --cam-lens 35` and `--gameplay-px 80` for a skier. These are starting estimates; replace them
  with measured values once the camera is built.

## Look (GMA2 direction)

- Clean, simple shapes with flat colours. No normal maps or detail textures on props, characters
  or buildings by default.
- One material per colour role. Bake ambient occlusion into vertex colours or a small atlas
  (512 to 1024) when a part needs it.
- Texture effort goes to snow and terrain, which are built in Unity, not here.
- The skill's Phase 5 therefore usually shrinks to: UVs for the AO atlas (if any), flat material
  colours, and an AO bake. Skip normal, roughness and metallic bakes unless the brief asks.

## Triangle budgets (starting points)

These replace the table in `references/delivery-and-acceptance.md` §3, which assumes far more
detail than GMA2's camera shows. Pass the LOD0 figure to `validate.py --budget-tris`.

| Asset | LOD0 | LOD1 / LOD2 | Notes |
| --- | --- | --- | --- |
| Player skier (rigged) | 5k to 10k | 2k to 4k / ~1k | Skis, poles, boots and helmet are separate socketed props |
| Crowd skier (rigged, later) | 0.5k to 1.5k | ~0.5k | Many on screen |
| Skis, poles, boots, helmet | 100 to 600 per piece | one LOD | Share the skier's material |
| Conifer tree | 300 to 1.5k | 100 to 400 / billboard | Thousands are instanced; solid low-poly tiers, cutouts only if needed |
| Rock | 100 to 800 | ~1/3 | |
| Lift chair | 0.5k to 1.5k | ~0.3k | |
| Lift tower | 0.3k to 1k | ~0.2k | Cable is drawn in Unity |
| Lodge or hut | 1k to 6k | ~1/3 | |

## Export rule for Unity (patched in `scripts/export_delivery.py`)

- **Static assets** (no armature in the export): exported with `bake_space_transform`, so every
  imported object arrives with no rotation.
- **Rigged assets:** Blender's default is kept, because its bake option is documented as broken
  with armatures and animation. The armature node then carries -90° on X internally. The model's
  top-level object in Unity stays clean, so code only ever moves that top-level object.
- How this was checked: a test hierarchy was exported both ways and the FBX files were read
  directly (Blender 5.2.2). In both, the front lands on +Z and up on +Y. Confirm in the Unity
  editor the first time a real asset is imported.

## Where files go

- Asset working folder: `art/source/<ASSET>/` (brief, `build/` scripts, master `.blend`,
  `review/`).
- Exports: `Assets/Art/<Category>/<ASSET>/` (FBX, `asset-manifest.json`), so Unity picks them up.
- Commit the brief, build scripts, master `.blend` (LFS), exports and `review/final/`. Leave other
  review renders uncommitted; they rebuild from the scripts.
- References: commit only references we made or that are CC0. Record the source URL of anything
  else in the brief instead of committing the image.
- Engineered objects (skis, poles, lift parts) can use a dimensioned drawing made from published
  real-world dimensions as the reference. Name the source of the dimensions in the brief.

## Running in a cloud session

- The `cloud-setup.sh` SessionStart hook installs everything below and sets `BLENDER_BIN`. The
  rest of this list is what it does, for when it fails or you are on another machine.
- Blender: the Linux build of 5.2 LTS from download.blender.org.
- Workbench renders, including `world_gate.py`, need EGL, which a fresh container lacks. Mesa's
  software renderer provides it: `apt-get install -y --no-install-recommends libegl1 libegl-mesa0
  libgl1-mesa-dri libgbm1`. After that, Workbench works headless; the `EGL_BAD_MATCH` lines it
  prints are harmless.
- `compose_review.py` needs Pillow: `python3 -m pip install pillow`.

## Lessons from the first assets (PR_Ski, PR_SkiPole)

- **Draw reference mattes supersampled.** Pillow's polygon fill includes its outline, which made
  the first ski drawing about 1.6 px (1.3 mm) thicker than its own dimensions. On a thin object
  that alone dropped the side IoU from 0.994 to 0.935, and it failed band widths on shallow curves,
  where one pixel of height is over a centimetre of horizontal extent. Draw at 4x, downsample with a
  box filter and threshold at 50 percent (see `art/source/PR_Ski/ref/make_refs.py`).
- **Phase scripts import the template** (`sys.path` to `assets/`, `import build_template as T`,
  then set `T.PHASE` and `T.OWNER_TAG`) instead of copying its 600 lines into every phase.
- **Imported FBX objects carry a 90° axis correction in Blender.** When placing a re-imported
  export in a test scene, add to its rotation; don't overwrite it.
- **Readability on snow:** light-grey pole shafts almost vanish against snow, and from the game
  camera poles are a pixel or two wide. Pick equipment colours against snow, not a grey studio.

## Changes from upstream

- `scripts/export_delivery.py`: Unity-aware `bake_space_transform` (`fbx_bake_space()`), and the
  manifest now records the measured export forward (+Z) for FBX as well as glTF.
- `SKILL.md`: one line pointing here.
- This file.
