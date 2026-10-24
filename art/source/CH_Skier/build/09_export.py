"""CH_Skier Phase 9: FBX for Unity through the skill's export_delivery.py (--engine unity, the PR_Ski settings),
with one change: takes come from the NLA strips that 08_poses.py made (one strip per clip, same name), not from
"all actions", because Blender names all-action takes "<Armature>|<Action>" and Unity names clips after takes.
Then the manifest gets the per-object tris, material colours, bone hierarchy, socket axes and the take names read
back from the FBX file itself.

  $BLENDER_BIN -b --python build/09_export.py
"""
import os, sys, json, importlib.util
import bpy

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.normpath(os.path.join(HERE, "../../../.."))
SK = os.path.join(REPO, ".claude/skills/blender-image-to-3d/scripts")
OUT = os.path.join(REPO, "Assets/Art/Characters/CH_Skier")
MASTER = os.path.normpath(os.path.join(HERE, "..", "CH_Skier_master.blend"))
_spec = importlib.util.spec_from_file_location("export_delivery", os.path.join(SK, "export_delivery.py"))
ED = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(ED)
_orig = ED.op_kwargs


def op_kwargs(op, kw):
    if "bake_anim_use_all_actions" in kw:
        kw = dict(kw, bake_anim_use_all_actions=False, bake_anim_use_nla_strips=True, bake_anim_simplify_factor=0.0)
    return _orig(op, kw)


ED.op_kwargs = op_kwargs
_orig_export = ED.export


def export_then_rest(path, objs, a):
    """After the FBX is written, put the rig in its rest pose so the manifest (bounds, socket positions) is measured at
    rest; otherwise the top NLA track (Crash) is what the depsgraph evaluates."""
    _orig_export(path, objs, a)
    rig = bpy.data.objects.get("CH_Skier_Rig")
    if rig:
        rig.data.pose_position = "REST"
        bpy.context.view_layer.update()


ED.export = export_then_rest
INFERRED = ("every dimension except height and arm angle (proportion table, brief INFERRED), back of the figure from "
            "symmetry, clavicle root, hand and foot bone tails, skin weights (analytic field, not painted), skin and "
            "mouth colours (placeholders), all pose clips (authored from the brief, no motion reference)")
DESCR = {"Stance": "athletic ski stance with a 1 s breathing bob", "CarveLeft": "left-edge carve, skis on left edges ~50 deg",
         "CarveRight": "mirror of CarveLeft", "Tuck": "deep downhill tuck", "Crouch": "loaded before a jump pop",
         "Air": "balanced air, skis off the snow", "Brake": "hard skid stop, feet 60 deg right of the body",
         "Pole": "double-pole push cycle", "Crash": "fallen on the left side and hip",
         "GrabMute": "air grab: left hand across onto the right ski, in front of the boot (sim GrabKind.Mute)",
         "GrabTip": "air grab: a pike, right hand near the right ski's tip (sim GrabKind.Tip)",
         "GrabTail": "air grab: right hand back on the right ski's tail (sim GrabKind.Tail)",
         "GrabJapan": "air grab: left hand behind the left boot, trunk turned (sim GrabKind.Japan)",
         "GrabSafety": "air grab: right hand on the right ski's outside edge under the boot (sim GrabKind.Safety)",
         "RaceCarveLeft": "low racer carve on the left edges, hands forward (sim Stance.Carve)",
         "RaceCarveRight": "mirror of RaceCarveLeft"}


def read_takes(path):
    from io_scene_fbx import parse_fbx
    root, _ = parse_fbx.parse(path)
    out = []
    for e in root.elems:
        if e.id != b"Objects":
            continue
        for c in e.elems:
            if c.id != b"AnimationStack":
                continue
            rec = {"name": c.props[1].split(b"\x00\x01")[0].decode()}
            for p in c.elems:
                if p.id == b"Properties70":
                    for q in p.elems:
                        if q.props[0] in (b"LocalStart", b"LocalStop"):
                            rec[q.props[0].decode() + "_frame"] = round(q.props[4] / 46186158000 * 30, 3)
            out.append(rec)
    return out


def main():
    ED.main(ED.parse(["--blend", MASTER, "--out", OUT, "--name", "CH_Skier", "--format", "FBX",
                      "--collections", "LOW,RIG_DEF,SOCKETS", "--engine", "unity", "--inferred", INFERRED]))
    fbx = os.path.join(OUT, "CH_Skier.fbx")
    mp, cp = os.path.join(OUT, "asset-manifest.json"), os.path.join(OUT, "animation-contract.json")
    man, con = json.load(open(mp)), json.load(open(cp))
    takes = read_takes(fbx)
    arm = bpy.data.objects["CH_Skier_Rig"]
    for lod in man["lods"]:
        lod["objects_tris"] = {n: ED.tri_count(bpy.data.objects[n]) for n in lod["objects"]}
    man["lod_note"] = ("Unity builds a LODGroup from CH_Skier_Body_LOD0/1/2. Helmet and boots carry no LOD suffix, so they "
                       "are counted in LOD0 here and render at every LOD: totals with props are LOD0 3677, LOD1 2913, LOD2 1855.")
    man["body_tris_per_lod"] = {"LOD%d" % l["level"]: l["objects_tris"].get("CH_Skier_Body_LOD%d" % l["level"]) for l in man["lods"]}
    mats = {}
    for o in bpy.data.collections["LOW"].all_objects:
        for m in o.data.materials:
            mats[m.name] = {"name": m.name, "hex_srgb": m.get("hex"), "roughness": round(m.roughness, 3),
                            "base_color_linear": [round(c, 4) for c in m.diffuse_color[:3]], "roles": m.get("roles")}
    man["materials_detail"] = sorted(mats.values(), key=lambda d: d["name"])
    sk = man["skeleton"][0]
    sk["rest_pose"] = arm.get("rest_pose")
    sk["bones_detail"] = [{"name": b.name, "parent": b.parent.name if b.parent else None,
                           "head_m": [round(v, 4) for v in b.head_local], "tail_m": [round(v, 4) for v in b.tail_local]}
                          for b in arm.data.bones]
    for s in man["sockets"]:
        o = bpy.data.objects[s["name"]]
        s["axes"] = o.get("note")
        s["local_offset_m"] = [round(v, 4) for v in o.matrix_basis.translation]
    man["skinning"] = {"max_influences": 4, "normalised": True, "method": "analytic weight field shared by all body shells "
                       "(build/06_rig.py); rigid: helmet->Head, boot shells->Foot_L/R, boot cuffs->LowerLeg_L/R"}
    man["snow_contact"] = ("Root at the origin. Flat poses put the soles at z 0; PR_Ski's base is 0.045 m below its SOCKET_boot, "
                           "so with skis on, the snow is 0.045 m below the root (place the root 0.045 m above the snow).")
    man["fbx_takes"] = takes
    tk = {t["name"]: t for t in takes}
    for c in con["clips"]:
        c["take_name"] = c["name"] if c["name"] in tk else None
        c["description"] = DESCR.get(c["name"], "")
    for f, d in ((mp, man), (cp, con)):
        with open(f, "w") as fh:
            json.dump(d, fh, indent=2)
    print("TAKES", takes)
    print("CLIPS", [(c["name"], c["frame_start"], c["frame_end"], c["length_s"], c["loop"]) for c in con["clips"]])


main()
