"""Seat check, step 2 (story 012): the trouser seat must never show through the jacket, in any clip.

  python3 build/seat_check.py --with review/06_rig/poses --without review/06_rig/poses_no_seat \
    --out review/06_rig/seat_check.json

For every clip in the animation contract, compares the back three-quarter close-up (closeup_<clip>_bq.png from
build/pose_sheet.py --closeup) with the same render of the copy from build/seat_only_copy.py. A pixel that is navy in
the first and jacket yellow in the second is seat in front of the jacket. Verdict: FAIL (seat pixels in any clip),
NOT ASSESSED (a close-up missing; the clips are named), PASS (0 in every clip). Exits 0 on PASS only.
"""
import os, sys, json, argparse
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
CONTRACT = os.path.normpath(os.path.join(HERE, "../../../../Assets/Art/Characters/CH_Skier/animation-contract.json"))


def yellow(p):
    r, g, b = p
    return r > 110 and g > 80 and b < 90 and r > b + 50


def navy(p):
    r, g, b = p
    return b > r + 8 and r < 100 and g < 110


def count(a_path, b_path):
    a, b = Image.open(a_path).convert("RGB"), Image.open(b_path).convert("RGB")
    if a.size != b.size:
        raise ValueError("sizes differ: %s %s" % (a.size, b.size))
    pa, pb = a.load(), b.load()
    return sum(1 for y in range(a.size[1]) for x in range(a.size[0]) if navy(pa[x, y]) and yellow(pb[x, y]))


def main(a):
    clips = [c["name"] for c in json.load(open(a.contract))["clips"]]
    per, missing = {}, []
    for c in clips:
        pa = os.path.join(a.with_seat, "closeup_%s_bq.png" % c)
        pb = os.path.join(a.without, "closeup_%s_bq.png" % c)
        if not (os.path.exists(pa) and os.path.exists(pb)):
            missing.append(c)
            continue
        per[c] = count(pa, pb)
        print("%-15s %6d" % (c, per[c]))
    if any(n > 0 for n in per.values()):
        verdict = "FAIL"
    elif missing or not clips:
        verdict = "NOT ASSESSED"
    else:
        verdict = "PASS"
    out = {"verdict": verdict, "clips": len(clips), "seat_pixels": per, "not_assessed": missing,
           "contract": a.contract}
    if a.out:
        json.dump(out, open(a.out, "w"), indent=1)
    print("SEAT_CHECK", verdict, "total", sum(per.values()), ("missing close-ups: " + ", ".join(missing)) if missing else "")
    return 0 if verdict == "PASS" else 1


if __name__ == "__main__":
    p = argparse.ArgumentParser()
    p.add_argument("--with", dest="with_seat", required=True, help="pose_sheet.py output of the master")
    p.add_argument("--without", required=True, help="pose_sheet.py output of the seat-removed copy")
    p.add_argument("--contract", default=CONTRACT, help="animation-contract.json: the clips to check")
    p.add_argument("--out", default="")
    sys.exit(main(p.parse_args()))
