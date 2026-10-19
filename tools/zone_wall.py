#!/usr/bin/env python3
"""Turn the boundary between two zones of a zone-map dump into a curation wall line, so a split that
one save makes and another does not is pinned in every save (docs/description-rules.md).

Usage:
    python3 tools/zone_wall.py <dump.txt> <zoneA> <zoneB> [--extend M] [--step M]

Prints one `wall @<elevation> x,z x,z ...` line for descriptions/<scene>.txt. The line follows the
boundary cells between the two zones (averaged every --step metres, default 1), and each end runs on
along the line until it leaves the walkable ground, plus --extend metres (default 0.5), so the wall
closes the whole gap even where the other save's ground is wider. Coordinates are the mod's world frame,
the frame the dump is written in. Check the result on a render (tools/zones_render.py --anchors).
"""
import sys

import numpy as np


def main():
    argv = sys.argv[1:]
    opts = {"--extend": 0.5, "--step": 1.0}
    for flag in list(opts):
        if flag in argv:
            i = argv.index(flag)
            opts[flag] = float(argv[i + 1])
            del argv[i:i + 2]
    if len(argv) < 3:
        sys.exit(__doc__)
    dump, za, zb = argv[0], int(argv[1]), int(argv[2])

    lines = open(dump, encoding="utf-8").read().split("\n")
    head = lines[0].split()
    w, h, cell, x0, z0 = int(head[0]), int(head[1]), float(head[2]), float(head[3]), float(head[4])
    label = np.array([int(v) for v in lines[1].split(",") if v != ""], dtype=np.int32).reshape(h, w)
    elev = np.array([float(v) for v in lines[2].split(",") if v != ""], dtype=np.float32).reshape(h, w)
    a, b = za - 1, zb - 1  # dump labels are zone indices, ids are index + 1

    pts, ys = [], []
    for gz in range(h):
        for gx in range(w):
            if label[gz, gx] != a:
                continue
            for dz, dx in ((0, 1), (1, 0), (0, -1), (-1, 0)):
                nz, nx = gz + dz, gx + dx
                if 0 <= nz < h and 0 <= nx < w and label[nz, nx] == b and abs(elev[gz, gx] - elev[nz, nx]) <= 0.6:
                    pts.append((x0 + (gx + nx + 1) * 0.5 * cell, z0 + (gz + nz + 1) * 0.5 * cell))
                    ys.append((elev[gz, gx] + elev[nz, nx]) * 0.5)
    if not pts:
        sys.exit(f"zones {za} and {zb} share no boundary")
    p = np.array(pts)
    mean = p.mean(axis=0)
    _, _, vt = np.linalg.svd(p - mean)
    d = vt[0]
    t = (p - mean) @ d
    order = np.argsort(t)
    p, t = p[order], t[order]

    verts = []
    edges = np.arange(t[0], t[-1] + opts["--step"], opts["--step"])
    for lo in edges:
        sel = (t >= lo) & (t < lo + opts["--step"])
        if sel.any():
            verts.append(p[sel].mean(axis=0))
    if len(verts) == 1:
        verts = [verts[0] - d * 0.25, verts[0] + d * 0.25]

    def walkable(x, z):
        gx, gz = int((x - x0) // cell), int((z - z0) // cell)
        return 0 <= gx < w and 0 <= gz < h and label[gz, gx] >= 0

    def extend(end, toward):
        u = end - toward
        u = u / (np.linalg.norm(u) or 1.0)
        q = end.copy()
        for _ in range(400):
            if not walkable(q[0], q[1]):
                break
            q = q + u * cell
        return q + u * opts["--extend"]

    verts[0] = extend(verts[0], verts[1])
    verts[-1] = extend(verts[-1], verts[-2])
    elevation = float(np.mean(ys))
    print(f"wall @{elevation:.1f} " + " ".join(f"{v[0]:.2f},{v[1]:.2f}" for v in verts))


if __name__ == "__main__":
    main()
