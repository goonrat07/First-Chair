---
paths:
  - "tools/blender/**"
  - "art/**"
---

# Blender Asset Rules (First Chair)

Claude builds First Chair's models in Blender by script, following a vetted
skill pack rather than improvising. The research behind these choices is in
`docs/research/engine-and-pipeline.md` §4.

## Pipeline

- Blender 5.2 LTS, run headless: `blender --background --python <script> -- <args>`.
  This works in cloud sessions as well as on the PC.
- Each asset's working folder (brief, build scripts, master `.blend`, review
  renders) lives in `art/source/<ASSET>/`, and its exports go to
  `Assets/Art/<Category>/<ASSET>/`. Binaries are in Git LFS. Shared pipeline
  scripts that aren't part of one asset live in `tools/blender/`.
- Skill pack: `.claude/skills/blender-image-to-3d/`, vendored from
  blender-game-skills at `f0ef293` and adapted for Unity. Use it for every
  modelled asset, and read its `FIRST-CHAIR.md` first.
- Trees, forests and terrain are outside that pack. They get First Chair's own
  skills in the same gated format: measured checks at each phase, tri budgets
  per LOD, and a round-trip import check.

## Conventions

- 1 Blender unit = 1 m, Z up, origin at ground contact.
- Export rigged assets as FBX for Unity, with axis conversion fixed at export so
  the object imports with no rotation in Unity.
- Every asset has a manifest: tri count per LOD, materials, sockets, source
  references and their licenses.
- Gather reference images before modeling. The skill pack's checks compare
  against them, and a text-only brief loses those checks.

## Look (Grand Mountain Adventure 2 direction)

- Clean, low-detail shapes with flat colors and baked ambient occlusion on
  props, buildings and characters.
- Detail and texture effort goes into the snow and the lighting, not the props.
- Textures and HDRIs only from CC0 sources (such as Poly Haven) or with a
  recorded license.
