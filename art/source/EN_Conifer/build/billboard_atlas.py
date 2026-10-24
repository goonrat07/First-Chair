"""EN_Conifer billboard atlas and gate (tree-kit Phase 5, the far LOD; story 007). Reads build/billboard.py's renders,
downsamples each (premultiplied box filter) into one cell of a side-by-side atlas, bleeds colour into the transparent
texels so mipmaps don't darken the edges, and writes the atlas PNG and its layout JSON for the Unity scene builder.

Gate (tree-kit SKILL.md Phase 5): each cell's alpha mask against the LOD0 silhouette from the same view, here the
render's own mask at RENDER_SCALE times the resolution reduced by majority; IoU at or above the params' gates.iou_min.

  python3 art/source/EN_Conifer/build/billboard_atlas.py --renders art/source/EN_Conifer/review/05_billboard \
    --params art/source/EN_Conifer/EN_Conifer_params.json --out Assets/Art/Environment/EN_Conifer
"""
import argparse
import json
import os
import sys

import numpy as np
from PIL import Image

ATLAS_NAME = "EN_Conifer_Billboard"
ALPHA_CUTOFF = 128  # Unity's material clips at 0.5
BLEED_PASSES = 16


def bleed(rgba):
    """Fills the RGB of transparent texels from their opaque neighbours, outward, so filtering never mixes in black."""
    rgb = rgba[..., :3].astype(np.float64)
    known = rgba[..., 3] > 0
    for _ in range(BLEED_PASSES):
        total = np.zeros_like(rgb)
        count = np.zeros(known.shape)
        for dy in (-1, 0, 1):
            for dx in (-1, 0, 1):
                if dx == 0 and dy == 0:
                    continue
                shifted = np.roll(np.roll(known, dy, 0), dx, 1)
                total += np.roll(np.roll(rgb, dy, 0), dx, 1) * shifted[..., None]
                count += shifted
        grow = ~known & (count > 0)
        rgb[grow] = total[grow] / count[grow][:, None]
        known = known | grow
    out = rgba.copy()
    out[..., :3] = np.clip(np.round(rgb), 0, 255).astype(np.uint8)
    return out


def main(a):
    layout = json.load(open(os.path.join(a.renders, "render_layout.json")))
    iou_min = json.load(open(a.params))["gates"]["iou_min"]
    cell_w, cell_h = layout["cell_px"]
    scale = layout["render_scale"]
    cells, gates = [], {}
    for name in layout["cells"]:
        hi = Image.open(os.path.join(a.renders, "render_%s.png" % name)).convert("RGBA")
        lo = hi.convert("RGBa").resize((cell_w, cell_h), Image.BOX).convert("RGBA")
        lo_px = np.asarray(lo).copy()
        hi_mask = np.asarray(hi)[..., 3] >= ALPHA_CUTOFF
        majority = hi_mask.reshape(cell_h, scale, cell_w, scale).mean(axis=(1, 3)) >= 0.5
        lo_mask = lo_px[..., 3] >= ALPHA_CUTOFF
        union = np.logical_or(lo_mask, majority).sum()
        iou = float(np.logical_and(lo_mask, majority).sum() / union) if union else 0.0
        gates[name] = {"iou": round(iou, 4), "coverage": round(float(lo_mask.mean()), 4)}
        cells.append(bleed(lo_px))
    atlas = np.concatenate(cells, axis=1)
    os.makedirs(a.out, exist_ok=True)
    Image.fromarray(atlas, "RGBA").save(os.path.join(a.out, ATLAS_NAME + ".png"))
    quad = {"atlas": ATLAS_NAME + ".png", "cells": len(cells), "cellWidthMetres": layout["cell_m"][0],
            "cellHeightMetres": layout["cell_m"][1], "bottomMetres": layout["bottom_m"], "alphaCutoff": ALPHA_CUTOFF / 255.0,
            "views": layout["cells"], "source": "art/source/EN_Conifer/build/billboard.py and billboard_atlas.py"}
    with open(os.path.join(a.out, ATLAS_NAME + ".json"), "w") as f:
        json.dump(quad, f, indent=2)
        f.write("\n")
    passed = all(g["iou"] >= iou_min for g in gates.values())
    report = {"iou_min": iou_min, "views": gates, "verdict": "PASS" if passed else "FAIL"}
    with open(os.path.join(a.renders, "gate_billboard.json"), "w") as f:
        json.dump(report, f, indent=2)
    print("GATE billboard", json.dumps(report))
    return 0 if passed else 1


if __name__ == "__main__":
    p = argparse.ArgumentParser()
    p.add_argument("--renders", required=True)
    p.add_argument("--params", required=True)
    p.add_argument("--out", required=True)
    sys.exit(main(p.parse_args()))
