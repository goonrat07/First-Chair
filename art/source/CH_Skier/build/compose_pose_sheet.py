"""Compose the pose renders from build/pose_sheet.py into sheets (Pillow).
  python3 build/compose_pose_sheet.py --dir review/06_rig/poses --out review/06_rig
"""
import os, argparse
from PIL import Image, ImageDraw

CLIPS = ["Stance", "CarveLeft", "CarveRight", "Tuck", "Crouch", "Air", "Brake", "Pole", "Crash"]
GRABS = ["GrabMute", "GrabTip", "GrabTail", "GrabJapan", "GrabSafety"]


def label(im, text):
    d = ImageDraw.Draw(im)
    d.rectangle((0, 0, 8 + 7 * len(text), 16), fill=(20, 20, 20))
    d.text((4, 3), text, fill=(255, 255, 255))
    return im


def main(a):
    for sheet_name, clips in (("pose_sheet", CLIPS), ("grab_sheet", GRABS)):
        for view in ("front", "side", "game"):
            tiles = [Image.open(os.path.join(a.dir, "%s_%s.png" % (c, view))).convert("RGB") for c in clips]
            w, h = tiles[0].size
            sheet = Image.new("RGB", (3 * w, -(-len(clips) // 3) * h))
            for i, (c, t) in enumerate(zip(clips, tiles)):
                sheet.paste(label(t, "%s (%s)" % (c, view)), ((i % 3) * w, (i // 3) * h))
            sheet.save(os.path.join(a.out, "%s_%s.png" % (sheet_name, view)))
    cell = 280
    sheet = Image.new("RGB", (3 * cell, 9 * cell))
    for r, c in enumerate(CLIPS):
        for k, view in enumerate(("front", "side", "game")):
            t = Image.open(os.path.join(a.dir, "%s_%s.png" % (c, view))).convert("RGB").resize((cell, cell), Image.LANCZOS)
            sheet.paste(label(t, "%s %s" % (c, view)), (k * cell, r * cell))
    sheet.save(os.path.join(a.out, "pose_sheet.png"))
    frames = sorted(f for f in os.listdir(a.dir) if f.startswith("cycle_Pole"))
    if frames:
        ims = [Image.open(os.path.join(a.dir, f)).convert("RGB") for f in frames]
        w, h = ims[0].size
        strip = Image.new("RGB", (w * len(ims), h))
        for i, (f, im) in enumerate(zip(frames, ims)):
            strip.paste(label(im, f[:-4]), (i * w, 0))
        strip.save(os.path.join(a.out, "pole_cycle_side.png"))
    cl = sorted(f for f in os.listdir(a.dir) if f.startswith("closeup_"))
    if cl:
        clips = []
        for f in cl:
            if f.split("_")[1] not in clips:
                clips.append(f.split("_")[1])
        w, h = Image.open(os.path.join(a.dir, cl[0])).size
        sheet = Image.new("RGB", (2 * w, len(clips) * h))
        for r, clip in enumerate(clips):
            for k, v in enumerate(("fq", "bq")):
                f = os.path.join(a.dir, "closeup_%s_%s.png" % (clip, v))
                if os.path.exists(f):
                    sheet.paste(label(Image.open(f).convert("RGB"), "%s %s" % (clip, v)), (k * w, r * h))
        sheet.save(os.path.join(a.out, "joints_sheet.png"))


if __name__ == "__main__":
    p = argparse.ArgumentParser()
    p.add_argument("--dir", required=True)
    p.add_argument("--out", required=True)
    main(p.parse_args())
