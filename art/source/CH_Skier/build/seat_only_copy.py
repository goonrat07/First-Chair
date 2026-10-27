"""Seat check, step 1 (story 012): a copy of the master with only the trouser seat removed from the body meshes.

  $BLENDER_BIN -b --python build/seat_only_copy.py -- CH_Skier_master.blend review/06_rig/no_seat.blend

The seat is the navy shell with no jacket-yellow face whose rest-position centre is on the middle line (|x| < 0.05);
the legs, cuffs and the jacket's own navy stripes are other shells and stay. Prints the faces removed per mesh and
exits 1 if a body mesh has no seat to remove (the check would then pass without looking).
"""
import sys
import bpy, bmesh

SEAT_X = 0.05


def shells(bm):
    out, seen = [], set()
    for f in bm.faces:
        if f.index in seen:
            continue
        group, stack = [], [f]
        seen.add(f.index)
        while stack:
            g = stack.pop()
            group.append(g)
            for e in g.edges:
                for h in e.link_faces:
                    if h.index not in seen:
                        seen.add(h.index)
                        stack.append(h)
        out.append(group)
    return out


def main(src, dst):
    bpy.ops.wm.open_mainfile(filepath=src)
    missing = []
    for ob in bpy.data.objects:
        if ob.type != "MESH" or not ob.name.startswith("CH_Skier_Body_LOD"):
            continue
        names = [m.name if m else "" for m in ob.data.materials]
        bm = bmesh.new()
        bm.from_mesh(ob.data)
        bm.faces.ensure_lookup_table()
        kill = []
        for group in shells(bm):
            mats = {names[f.material_index] for f in group}
            cx = sum(f.calc_center_median().x for f in group) / len(group)
            if any(m.endswith("_Navy") for m in mats) and not any(m.endswith("_Yellow") for m in mats) \
                    and abs(cx) < SEAT_X:
                kill += group
        bmesh.ops.delete(bm, geom=kill, context="FACES")
        bm.to_mesh(ob.data)
        bm.free()
        print("SEAT_REMOVED", ob.name, len(kill))
        if not kill:
            missing.append(ob.name)
    bpy.ops.wm.save_as_mainfile(filepath=dst, copy=True)
    print("saved", dst)
    if missing:
        print("NO SEAT FOUND in", missing)
        sys.exit(1)


if __name__ == "__main__":
    a = sys.argv[sys.argv.index("--") + 1:]
    main(a[0], a[1])
