#!/usr/bin/env python3
"""Render a zone-map dump as a coloured floor plan PNG, the authoring eye for tuning the segmentation
and curating a scene (docs/zones-and-descriptions-plan.md).

Usage:
    python3 tools/zones_render.py <dump.txt> [out.png] [--scale N] [--anchors descriptions/<scene>.txt]
                                  [--crop X0,Z0,X1,Z1]

--anchors draws each zone anchor (a magenta dot and its key) and each curation wall (a magenta line)
from the scene's geometry file; --crop keeps one rectangle of the mod's world frame.

The dump comes from the live game through the dev server:
    curl -X POST --data-binary 'NonVisualCalculus.Module.World.DevZones.Dump()' http://127.0.0.1:8771/eval

Output: zones in stable colours with their ids at the centroid, unwalkable ground near-black, exits as
white dots. North (the mod's +Z) is up and east (+X) is right, the frame every readout uses.
"""
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFont


def main():
    argv = sys.argv[1:]
    opts = {}
    for flag in ("--scale", "--anchors", "--crop"):
        if flag in argv:
            i = argv.index(flag)
            opts[flag] = argv[i + 1]
            del argv[i:i + 2]
    args = [a for a in argv if not a.startswith("--")]
    if not args:
        sys.exit(__doc__)
    dump_path = args[0]
    out_path = args[1] if len(args) > 1 else dump_path.rsplit(".", 1)[0] + ".png"
    scale = int(opts.get("--scale", 3))

    lines = open(dump_path, encoding="utf-8").read().split("\n")
    head = lines[0].split()
    w, h, cell, x0, z0 = int(head[0]), int(head[1]), float(head[2]), float(head[3]), float(head[4])
    scene = head[5] if len(head) > 5 else "?"
    label = np.array([int(x) for x in lines[1].split(",") if x != ""], dtype=np.int32).reshape(h, w)
    zones, exits = [], []
    for line in lines[3:]:
        if line.startswith("Z "):
            zid, cls, sqm, cx, cy, cz = line[2:].split("|")
            zones.append((int(zid), cls, float(sqm), float(cx), float(cy), float(cz)))
        elif line.startswith("E "):
            fr, to, x, y, z = line[2:].split("|")
            exits.append((int(fr), int(to), float(x), float(y), float(z)))

    def px(wx, wz):
        """World to image pixels, north up: the row axis flips here."""
        return int((wx - x0) / cell) * scale, (h - 1 - int((wz - z0) / cell)) * scale

    rng = np.random.RandomState(42)
    palette = rng.randint(70, 240, (max(label.max() + 2, 2), 3))
    img = np.zeros((h, w, 3), np.uint8)
    img[label < 0] = (24, 24, 24)
    mask = label >= 0
    img[mask] = palette[label[mask]]
    img = img[::-1]
    im = Image.fromarray(np.repeat(np.repeat(img, scale, 0), scale, 1))
    draw = ImageDraw.Draw(im)
    try:
        font = ImageFont.truetype("/System/Library/Fonts/Helvetica.ttc", 8 * scale)
    except OSError:
        font = ImageFont.load_default()

    for fr, to, x, y, z in exits:
        cx, cz = px(x, z)
        draw.ellipse([cx - scale, cz - scale, cx + scale, cz + scale], fill=(255, 255, 255))
    for zid, cls, sqm, cx, cy, cz in zones:
        ix, iz = px(cx, cz)
        draw.text((ix + 1, iz + 1), str(zid), fill=(0, 0, 0), font=font, anchor="mm")
        draw.text((ix, iz), str(zid), fill=(255, 255, 255), font=font, anchor="mm")

    if "--anchors" in opts:
        for line in open(opts["--anchors"], encoding="utf-8"):
            parts = line.split()
            if len(parts) >= 5 and parts[0] == "zone":
                ix, iz = px(float(parts[2]), float(parts[4]))
                draw.ellipse([ix - 2 * scale, iz - 2 * scale, ix + 2 * scale, iz + 2 * scale], fill=(255, 0, 255))
                draw.text((ix + 3 * scale, iz), parts[1], fill=(255, 0, 255), font=font, anchor="lm")
            elif parts and parts[0] == "wall":
                pts = [p.split(",") for p in parts[1:] if not p.startswith("@")]
                xy = [px(float(a), float(b)) for a, b in pts]
                draw.line(xy, fill=(255, 0, 255), width=max(1, scale // 2))

    if "--crop" in opts:
        cx0, cz0, cx1, cz1 = (float(v) for v in opts["--crop"].split(","))
        a, b = px(min(cx0, cx1), max(cz0, cz1)), px(max(cx0, cx1), min(cz0, cz1))
        im = im.crop((max(0, a[0]), max(0, a[1]), min(im.width, b[0]), min(im.height, b[1])))

    im.save(out_path)
    print(f"{scene}: {len(zones)} zones, {len(exits)} exit ends -> {out_path} ({im.width}x{im.height})")


if __name__ == "__main__":
    main()
