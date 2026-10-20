"""PR_Ski sockets: SOCKET_boot where the skier's boot sole sits (static prop, no rig).
Socket convention: local +Y = forward of whatever attaches (the boot faces the ski tip, world -Y),
local +Z = up.

  $BLENDER_BIN --background --python build/06_sockets.py -- --master PR_Ski_master.blend
"""
import math, os, sys, argparse

SKILL_ASSETS = os.path.join(os.path.dirname(os.path.abspath(__file__)),
                            "../../../../.claude/skills/blender-image-to-3d/assets")
sys.path.insert(0, os.path.normpath(SKILL_ASSETS))
import build_template as T  # noqa: E402
import bpy  # noqa: E402

T.PHASE = "06_sockets"
T.OWNER_TAG = "phase:" + T.PHASE

BOOT_SOLE_Z = 0.045   # inferred: 17 mm ski + 28 mm binding stand height


def build(scene, args):
    low = bpy.data.objects["PR_Ski_LOD0"]
    T.socket("SOCKET_boot", (0.0, 0.0, BOOT_SOLE_Z), parent=low, rotation=(0, 0, math.radians(180)), size=0.08,
             role="boot", note="boot centre; +Y toward the ski tip")
    T.log("socket placed")


if __name__ == "__main__":
    p = argparse.ArgumentParser()
    p.add_argument("--master", required=True)
    a = p.parse_args(T.argv_after_dashes())
    scene = T.open_master(a.master)
    T.clear_owned()
    build(scene, a)
    T.save_master(a.master)
    T.log("saved", a.master)
