#!/usr/bin/env python3
"""Render a zone-map dump as a coloured floor plan PNG, the authoring eye for tuning the segmentation
and curating a scene (docs/zones-and-descriptions-plan.md).

Usage:
    python3 tools/zones_render.py <dump.txt> [out.png] [--scale N]

The dump comes from the live game through the dev server:
    curl -X POST --data-binary 'NonVisualCalculus.Module.World.DevZones.Dump()' http://127.0.0.1:8771/eval

Output: zones in stable colours with their ids at the centroid, unwalkable ground near-black, exits as
white dots. North (the mod's +Z) is up and east (+X) is right, the frame every readout uses.
"""
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFont


def main():
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    if not args:
        sys.exit(__doc__)
    dump_path = args[0]
    out_path = args[1] if len(args) > 1 else dump_path.rsplit(".", 1)[0] + ".png"
    scale = int(sys.argv[sys.argv.index("--scale") + 1]) if "--scale" in sys.argv else 3

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

    im.save(out_path)
    print(f"{scene}: {len(zones)} zones, {len(exits)} exit ends -> {out_path} ({im.width}x{im.height})")


if __name__ == "__main__":
    main()
