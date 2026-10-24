"""EN_Conifer billboard renders (tree-kit Phase 5, the far LOD; story 007). LOD0 alone, orthographic, flat-lit in its
own material colours (Workbench FLAT, Standard view transform: the pixels are the albedo, and Unity lights the quads),
from the front (looking +Y) and the side (looking -X), at RENDER_SCALE times the atlas cell, on a transparent film.
build/billboard_atlas.py (system python3 with Pillow) downsamples them into the atlas and runs the gate.

Lives with the asset because the tree-kit skill's billboard script isn't written yet (.claude/skills/tree-kit/SKILL.md,
Phase 5); move both scripts there when the skill is next edited.

  $BLENDER_BIN -b --python art/source/EN_Conifer/build/billboard.py -- --master art/source/EN_Conifer/EN_Conifer_master.blend \
    --out art/source/EN_Conifer/review/05_billboard
"""
import argparse
import json
import math
import os
import sys

import bpy

# One atlas cell: CELL_PX_W x CELL_PX_H pixels covering CELL_W_M x CELL_H_M metres, its bottom edge BOTTOM_M below the
# tree's origin (the trunk base). The tree is 12 m tall and about 3.5 m wide (asset-manifest.json bounds).
CELL_PX_W, CELL_PX_H = 256, 1024
CELL_W_M = 3.6
CELL_H_M = CELL_W_M * CELL_PX_H / CELL_PX_W
BOTTOM_M = -0.1
RENDER_SCALE = 4
VIEWS = {  # name: camera location direction from the tree, rotation (Euler, degrees)
    "front": ((0.0, -1.0), (90.0, 0.0, 0.0)),
    "side": ((1.0, 0.0), (90.0, 0.0, 90.0)),
}


def main(a):
    bpy.ops.wm.open_mainfile(filepath=os.path.abspath(a.master))
    scene = bpy.context.scene
    keep = {o.name for o in bpy.data.collections["LOD0"].all_objects}
    for o in scene.objects:
        o.hide_render = o.name not in keep
    scene.render.engine = "BLENDER_WORKBENCH"
    shading = scene.display.shading
    shading.light, shading.color_type = "FLAT", "MATERIAL"
    shading.show_shadows = shading.show_cavity = shading.show_object_outline = False
    scene.view_settings.view_transform = "Standard"
    scene.render.film_transparent = True
    scene.render.resolution_x = CELL_PX_W * RENDER_SCALE
    scene.render.resolution_y = CELL_PX_H * RENDER_SCALE
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    cam = bpy.data.objects.new("_billboard_cam", bpy.data.cameras.new("_billboard_cam"))
    scene.collection.objects.link(cam)
    scene.camera = cam
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = CELL_H_M  # the larger render dimension is the height
    centre_z = BOTTOM_M + CELL_H_M / 2.0
    os.makedirs(a.out, exist_ok=True)
    for name, ((dx, dy), rot) in VIEWS.items():
        cam.location = (dx * 50.0, dy * 50.0, centre_z)
        cam.rotation_euler = tuple(math.radians(r) for r in rot)
        scene.render.filepath = os.path.join(a.out, "render_%s.png" % name)
        bpy.ops.render.render(write_still=True)
    layout = {"cells": list(VIEWS), "cell_px": [CELL_PX_W, CELL_PX_H], "cell_m": [CELL_W_M, CELL_H_M], "bottom_m": BOTTOM_M,
              "render_scale": RENDER_SCALE}
    with open(os.path.join(a.out, "render_layout.json"), "w") as f:
        json.dump(layout, f, indent=2)
    print("BILLBOARD_RENDERS", json.dumps(layout))


if __name__ == "__main__":
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    p = argparse.ArgumentParser()
    p.add_argument("--master", required=True)
    p.add_argument("--out", required=True)
    main(p.parse_args(argv))
