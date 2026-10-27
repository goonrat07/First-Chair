"""A tree's billboard renders (tree-kit Phase 5, the far LOD; story 007, shared since story 016). LOD0 alone, orthographic, flat-lit in its
own material colours (Workbench FLAT, Standard view transform: the pixels are the albedo, and Unity lights the quads),
from the front (looking +Y) and the side (looking -X), at RENDER_SCALE times the atlas cell, on a transparent film.
billboard_atlas.py (system python3 with Pillow) downsamples them into the atlas and runs the gate. Each cell is sized to
the tree (story 016): its LOD0 from the front and the side plus a margin, at a fixed texel density, so a broad tree isn't
cut off and a small one isn't lost in an empty cell.

Lives in tools/blender/trees/ because the tree-kit skill's billboard script isn't written yet
(.claude/skills/tree-kit/SKILL.md, Phase 5); move both scripts there when the skill is next edited.

  $BLENDER_BIN -b --python tools/blender/trees/billboard.py -- --master art/source/EN_Conifer/EN_Conifer_master.blend \
    --out art/source/EN_Conifer/review/05_billboard
"""
import argparse
import json
import math
import os
import sys

import bpy

# One atlas cell covers the tree's LOD0 seen from the front and the side, plus SIDE_MARGIN_M either side and TOP_MARGIN_M
# above, its bottom edge BOTTOM_M below the tree's origin (the trunk base), at PX_PER_M texels a metre (EN_Conifer's first,
# fixed cell was 1,024 px over 14.4 m: 71 a metre), rounded up to whole CELL_PX_STEP-pixel blocks for texture compression.
PX_PER_M = 72.0
CELL_PX_STEP = 32
SIDE_MARGIN_M = 0.15
TOP_MARGIN_M = 0.2
BOTTOM_M = -0.1
RENDER_SCALE = 4
VIEWS = {  # name: camera location direction from the tree, rotation (Euler, degrees)
    "front": ((0.0, -1.0), (90.0, 0.0, 0.0)),
    "side": ((1.0, 0.0), (90.0, 0.0, 90.0)),
}


def lod0_extent(scene, names):
    """The farthest any LOD0 vertex reaches from the trunk axis along X or Y, and its highest point, metres."""
    depsgraph = bpy.context.evaluated_depsgraph_get()
    half_width, top = 0.0, 0.0
    for o in scene.objects:
        if o.name in names and o.type == "MESH":
            evaluated = o.evaluated_get(depsgraph)
            for v in evaluated.data.vertices:
                p = evaluated.matrix_world @ v.co
                half_width = max(half_width, abs(p.x), abs(p.y))
                top = max(top, p.z)
    return half_width, top


def cell_size(half_width, top):
    """A cell's pixels and metres, (px_w, px_h, w_m, h_m): the tree plus its margins, in whole blocks, square texels."""
    blocks = lambda metres: int(math.ceil(metres * PX_PER_M / CELL_PX_STEP)) * CELL_PX_STEP
    px_w = blocks(2.0 * (half_width + SIDE_MARGIN_M))
    px_h = blocks(top + TOP_MARGIN_M - BOTTOM_M)
    return px_w, px_h, px_w / PX_PER_M, px_h / PX_PER_M


def main(a):
    bpy.ops.wm.open_mainfile(filepath=os.path.abspath(a.master))
    scene = bpy.context.scene
    keep = {o.name for o in bpy.data.collections["LOD0"].all_objects}
    for o in scene.objects:
        o.hide_render = o.name not in keep
    cell_px_w, cell_px_h, cell_w_m, cell_h_m = cell_size(*lod0_extent(scene, keep))
    scene.render.engine = "BLENDER_WORKBENCH"
    shading = scene.display.shading
    shading.light, shading.color_type = "FLAT", "MATERIAL"
    shading.show_shadows = shading.show_cavity = shading.show_object_outline = False
    scene.view_settings.view_transform = "Standard"
    scene.render.film_transparent = True
    scene.render.resolution_x = cell_px_w * RENDER_SCALE
    scene.render.resolution_y = cell_px_h * RENDER_SCALE
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    cam = bpy.data.objects.new("_billboard_cam", bpy.data.cameras.new("_billboard_cam"))
    scene.collection.objects.link(cam)
    scene.camera = cam
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = max(cell_w_m, cell_h_m)  # spans the render's larger dimension
    centre_z = BOTTOM_M + cell_h_m / 2.0
    os.makedirs(a.out, exist_ok=True)
    for name, ((dx, dy), rot) in VIEWS.items():
        cam.location = (dx * 50.0, dy * 50.0, centre_z)
        cam.rotation_euler = tuple(math.radians(r) for r in rot)
        scene.render.filepath = os.path.join(a.out, "render_%s.png" % name)
        bpy.ops.render.render(write_still=True)
    layout = {"cells": list(VIEWS), "cell_px": [cell_px_w, cell_px_h], "cell_m": [round(cell_w_m, 4), round(cell_h_m, 4)],
              "bottom_m": BOTTOM_M, "render_scale": RENDER_SCALE}
    with open(os.path.join(a.out, "render_layout.json"), "w") as f:
        json.dump(layout, f, indent=2)
    print("BILLBOARD_RENDERS", json.dumps(layout))


if __name__ == "__main__":
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    p = argparse.ArgumentParser()
    p.add_argument("--master", required=True)
    p.add_argument("--out", required=True)
    main(p.parse_args(argv))
