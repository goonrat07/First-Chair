"""Compose one review sheet from review_render.py output: front, side, three-quarter, game camera, 80 px strip.

  python3 build/review_sheet.py --dir review/03_forms --mode material --title "..." --out review/03_forms/colour_sheet.png
Backgrounds are flattened to a light snow grey so colours read against something like snow.
"""
import argparse, os
from PIL import Image, ImageDraw, ImageFont

BG = (236, 236, 240)


def font(s):
    try:
        return ImageFont.load_default(size=s)
    except TypeError:
        return ImageFont.load_default()


def flat(path):
    im = Image.open(path).convert("RGBA")
    alpha = im.getchannel("A")
    if alpha.getextrema()[0] < 250:
        box = alpha.getbbox()
    else:
        corner = im.getpixel((0, 0))
        box = Image.eval(im.convert("L"), lambda v, c=sum(corner[:3]) // 3: 255 if abs(v - c) > 12 else 0).getbbox()
    bg = Image.new("RGBA", im.size, BG + (255,))
    bg.alpha_composite(im)
    return bg.convert("RGB"), box or (0, 0, im.width, im.height)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--dir", required=True)
    ap.add_argument("--mode", default="clay")
    ap.add_argument("--title", default="")
    ap.add_argument("--out", required=True)
    a = ap.parse_args()
    views = [("front (ortho, az 0)", "front"), ("side (ortho, az 90)", "side"),
             ("three-quarter (az 35, el 15, 50 mm)", "threequarter"), ("game camera (az 30, el 45, 35 mm)", "ref")]
    tiles = []
    for label, v in views:
        p = os.path.join(a.dir, "%s_%s.png" % (a.mode, v))
        if not os.path.exists(p):
            continue
        im, b = flat(p)
        pad = 20
        im = im.crop((max(0, b[0] - pad), max(0, b[1] - pad), min(im.width, b[2] + pad), min(im.height, b[3] + pad)))
        s = 600.0 / im.height
        tiles.append((label, im.resize((max(1, int(im.width * s)), 600), Image.LANCZOS)))
    gp = os.path.join(a.dir, "%s_gameplay_80px.png" % a.mode)
    g = flat(gp)[0] if os.path.exists(gp) else None
    W = sum(t.width for _, t in tiles) + 20 * (len(tiles) + 1) + ((g.width * 4 + 40) if g else 0)
    sheet = Image.new("RGB", (W, 690), "white")
    d = ImageDraw.Draw(sheet)
    d.text((20, 8), a.title, fill="black", font=font(18))
    x = 20
    for label, t in tiles:
        sheet.paste(t, (x, 50))
        d.text((x, 660), label, fill="black", font=font(14))
        x += t.width + 20
    if g:
        sheet.paste(g, (x, 50))
        big = g.resize((g.width * 3, g.height * 3), Image.NEAREST)
        sheet.paste(big, (x + g.width + 10, 50))
        d.text((x, 660), "80 px (1x and 3x)", fill="black", font=font(14))
    sheet.save(a.out)
    print("sheet", a.out, sheet.size)


if __name__ == "__main__":
    main()
