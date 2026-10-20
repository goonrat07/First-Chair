"""PR_SkiPole sockets: SOCKET_grip on the axis where the hand closes (static prop, no rig).
The shaft runs down the socket's -Z toward the tip.

  $BLENDER_BIN --background --python build/06_sockets.py -- --master PR_SkiPole_master.blend
"""
import os, sys, argparse

SKILL_ASSETS = os.path.join(os.path.dirname(os.path.abspath(__file__)),
                            "../../../../.claude/skills/blender-image-to-3d/assets")
sys.path.insert(0, os.path.normpath(SKILL_ASSETS))
import build_template as T  # noqa: E402
import bpy  # noqa: E402

T.PHASE = "06_sockets"
T.OWNER_TAG = "phase:" + T.PHASE

GRIP_CENTRE_Z = 1.10   # middle of the grip, ref/front_matte.png


def build(scene, args):
    low = bpy.data.objects["PR_SkiPole_LOD0"]
    T.socket("SOCKET_grip", (0.0, 0.0, GRIP_CENTRE_Z), parent=low, size=0.06,
             role="hand", note="grip centre; shaft runs down -Z")
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
