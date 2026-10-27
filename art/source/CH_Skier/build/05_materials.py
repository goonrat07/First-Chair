"""CH_Skier Phase 5 (light): one flat material per colour on the LOW meshes (FIRST-CHAIR.md: flat colours, no
textures). Roles that share a colour are merged (brief: Decisions): navy = stripes, zip, cuffs, trousers; red =
mittens, boot band, neck warmer. Principled BSDF base colour and roughness from the brief's MATERIALS table; the
viewport colour is set too, so Workbench reviews show the same colours. No UVs and no bake: Unity's URP Lit takes
the FBX diffuse colour. The AO bake is deferred (brief: Decisions). The HIGH and BLOCKOUT pieces keep the 16 role
materials of the forms phase (story 012 added buckle, light grey).

  $BLENDER_BIN -b --python build/05_materials.py -- --master CH_Skier_master.blend
"""
import os, sys, argparse
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.normpath(os.path.join(HERE, "../../../../.claude/skills/blender-image-to-3d/assets")))
import build_template as T  # noqa: E402
import bpy  # noqa: E402

T.PHASE = "05_materials"
T.OWNER_TAG = "phase:" + T.PHASE

# (material, hex sRGB, roughness, master roles merged into it)
PALETTE = [
    ("M_CH_Skier_Yellow", "#F2B705", 0.80, ["jacket"]),
    ("M_CH_Skier_Navy", "#22304A", 0.80, ["stripe", "cuff", "trousers"]),
    ("M_CH_Skier_Red", "#D7263D", 0.80, ["mitten", "boot_band", "neck_warmer"]),
    ("M_CH_Skier_KneePatch", "#18223A", 0.85, ["knee_patch"]),
    ("M_CH_Skier_Boot", "#2B2F38", 0.50, ["boot"]),
    ("M_CH_Skier_Helmet", "#2C3A57", 0.50, ["helmet"]),
    ("M_CH_Skier_HelmetPanel", "#4A5B7D", 0.50, ["helmet_panel"]),
    ("M_CH_Skier_GoggleFrame", "#15181E", 0.50, ["goggle_frame"]),
    ("M_CH_Skier_Lens", "#E2557A", 0.15, ["goggle_lens"]),
    ("M_CH_Skier_Skin", "#D9A07A", 0.60, ["skin"]),
    ("M_CH_Skier_Mouth", "#9A5A4E", 0.60, ["mouth"]),
    ("M_CH_Skier_Buckle", "#AEB6C2", 0.45, ["buckle"]),        # story 012: boot buckles, helmet strap buckle
]
LOW_OBJECTS = ["CH_Skier_Body_LOD0", "CH_Skier_Body_LOD1", "CH_Skier_Body_LOD2", "CH_Skier_Helmet",
               "CH_Skier_BootShell_L", "CH_Skier_BootCuff_L", "CH_Skier_BootShell_R", "CH_Skier_BootCuff_R"]


def linear(hexs):
    def c(v):
        v = v / 255.0
        return v / 12.92 if v <= 0.04045 else ((v + 0.055) / 1.055) ** 2.4
    return (c(int(hexs[1:3], 16)), c(int(hexs[3:5], 16)), c(int(hexs[5:7], 16)), 1.0)


def build(scene, args):
    mats, remap = {}, {}
    for name, hexs, rough, roles in PALETTE:
        m = T.material(name, linear(hexs), roughness=rough)
        m.diffuse_color = linear(hexs)
        m.roughness = rough
        m["hex"] = hexs
        m["roles"] = ",".join(roles)
        mats[name] = m
        remap[name] = name
        for r in roles:
            remap["M_CH_Skier_" + r] = name
    report = {}
    for n in LOW_OBJECTS:
        ob = bpy.data.objects[n]
        me = ob.data
        old = [m.name if m else None for m in me.materials]
        idx = [p.material_index for p in me.polygons]
        new = []
        for o in old:
            t = remap[o]
            if t not in new:
                new.append(t)
        me.materials.clear()
        for t in new:
            me.materials.append(mats[t])
        me.polygons.foreach_set("material_index", [new.index(remap[old[i]]) for i in idx])
        me.update()
        report[n] = new
        T.log(n, new)
    scene["phase05_materials"] = str({k: v for k, v in report.items()})


if __name__ == "__main__":
    p = argparse.ArgumentParser()
    p.add_argument("--master", required=True)
    a = p.parse_args(T.argv_after_dashes())
    scene = T.open_master(a.master)
    build(scene, a)
    T.save_master(a.master)
    T.log("saved", a.master)
