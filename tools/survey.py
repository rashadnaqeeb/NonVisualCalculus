#!/usr/bin/env python3
"""Survey the active scene through the dev server for description authoring.

Drives src/NonVisualCalculus.Module/World/DevSurvey.cs over http://127.0.0.1:8771 (the game must be
running, in the scene to survey). See docs/zones-and-descriptions-plan.md.

Modes:
  status     Print the scene, the character's spot and the zone count.
  zones      One capture per survey spot of every zone (or --zone N ...), plus data.json per zone
             with the zone's exits, contents and the on-screen labels. Output:
             survey/<scene>/zones/zone_NN/.
  shot       One capture at --at X,Z (mod frame) with --zoom; prints the file. For details.
  entities   Dump every entity of the scene (inactive twins included) to survey/<scene>/entities.json.
  objects    One tight capture per accessible entity (or --name ...), survey/<scene>/objects/.

Captures hide the HUD and lift every fog; the camera, HUD and fog are restored at the end (also on
Ctrl+C). Shots are downscaled to --max-width for review. Coordinates are the mod's world frame.
"""
import argparse
import json
import shutil
import sys
import time
import urllib.request
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent
NS = "NonVisualCalculus.Module.World.DevSurvey."


class Dev:
    def __init__(self, base):
        self.base = base.rstrip("/")

    def http(self, path, body=None):
        req = urllib.request.Request(self.base + path, data=body.encode("utf-8") if body is not None else None,
                                     method="POST" if body is not None else "GET")
        with urllib.request.urlopen(req, timeout=120) as r:
            return r.read().decode("utf-8", "replace")

    def call(self, expr):
        out = self.http("/eval?speech=0", expr)
        for line in out.splitlines():
            if line.startswith("=> "):
                payload = json.loads(line[3:])
                if isinstance(payload, dict) and payload.get("error"):
                    raise RuntimeError(f"{expr}: {payload['error']}")
                return payload
        raise RuntimeError(f"eval failed: {expr}\n{out.strip()}")

    def wait(self, expr, timeout_ms):
        return self.http(f"/wait?timeout={timeout_ms}", expr).strip()

    def screenshot(self, dest, max_width):
        out = self.http("/screenshot").strip()
        src = Path(out.splitlines()[-1])
        if not src.is_file():
            raise RuntimeError(f"screenshot failed: {out}")
        dest.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(src, dest)
        return downscale(dest, max_width)


def downscale(path, max_width):
    if not max_width:
        return 1.0
    from PIL import Image
    with Image.open(path) as im:
        if im.width <= max_width:
            return 1.0
        h = round(im.height * max_width / im.width)
        im.resize((max_width, h), Image.LANCZOS).save(path)
        return max_width / im.width


def frame_and_shoot(dev, dest, x, y, z, zoom, settle, max_width, outline_zone=None):
    dev.call(f"{NS}Frame({x}f, {y}f, {z}f, {zoom}f)")
    time.sleep(settle)
    # The painting streams in around the camera: wait for its page streamer to go idle (bounded).
    dev.wait(f"System.Text.Json.JsonDocument.Parse({NS}Settled()).RootElement.GetProperty(\"settled\").GetBoolean()", 4000)
    time.sleep(0.2)
    scale = dev.screenshot(dest, max_width)
    labels = dev.call(f"{NS}Labels()")
    labels["w"] = round(labels["w"] * scale)
    labels["h"] = round(labels["h"] * scale)
    for l in labels["labels"]:
        l["sx"] = round(l["sx"] * scale)
        l["sy"] = round(l["sy"] * scale)
    if outline_zone is not None:
        draw_outline(dest, dev.call(f"{NS}ZoneOutline({outline_zone})"), scale, outline_zone)
    return labels


def draw_outline(path, outline, scale, zone_id):
    """Mark the zone on its capture: red dots along its boundary, white rings at its exits with the
    destination zone's id, and the zone's own id in the middle of the marks."""
    from PIL import Image, ImageDraw
    with Image.open(path) as im:
        im = im.convert("RGB")
        draw = ImageDraw.Draw(im)
        pts = [(round(p[0] * scale), round(p[1] * scale)) for p in outline["points"]]
        for x, y in pts:
            draw.ellipse([x - 2, y - 2, x + 2, y + 2], fill=(255, 40, 40))
        for ex in outline["exits"]:
            x, y = round(ex[0] * scale), round(ex[1] * scale)
            draw.ellipse([x - 9, y - 9, x + 9, y + 9], outline=(255, 255, 255), width=3)
            draw.text((x + 11, y - 7), str(ex[2]), fill=(255, 255, 255))
        if pts:
            cx = sum(p[0] for p in pts) // len(pts)
            cy = sum(p[1] for p in pts) // len(pts)
            draw.rectangle([cx - 16, cy - 10, cx + 16, cy + 10], fill=(0, 0, 0))
            draw.text((cx - 10, cy - 7), str(zone_id), fill=(255, 255, 0))
        im.save(path)


def survey_zones(dev, args, scene, out_dir):
    zones = dev.call(f"{NS}ZoneList({args.max_points})")["zones"]
    if args.zone:
        zones = [z for z in zones if z["id"] in args.zone]
    print(f"{len(zones)} zones to capture in {scene}")
    for zone in zones:
        zid = zone["id"]
        zone_dir = out_dir / "zones" / f"zone_{zid:03d}"
        if not args.force and (zone_dir / "data.json").is_file():
            print(f"  zone {zid}: already surveyed (--force to redo)")
            continue
        shots = []
        for pi, pt in enumerate(zone["points"]):
            name = f"p{pi}.png"
            labels = frame_and_shoot(dev, zone_dir / name, pt["x"], pt["y"], pt["z"], args.zoom, args.settle, args.max_width,
                                     outline_zone=zid)
            shots.append({"file": name, "point": pt, "zoom": args.zoom, "screen": {"w": labels["w"], "h": labels["h"]},
                          "labels": labels["labels"]})
        contents = dev.call(f"{NS}Contents({zid})")
        data = {"scene": scene, "zone": zone, "shots": shots, "contents": contents}
        (zone_dir / "data.json").write_text(json.dumps(data, indent=2, ensure_ascii=False), encoding="utf-8")
        print(f"  zone {zid} ({zone['cls']}, {zone['area']:.0f} m2, {zone.get('title') or 'untitled'}): "
              f"{len(shots)} shots, {len(contents['things'])} things, {len(contents['people'])} people")


def survey_objects(dev, args, scene, out_dir):
    entities = dev.call(f"{NS}Entities()")["entities"]
    todo = [e for e in entities if e["active"] and (not args.name or e["name"] in args.name)]
    print(f"{len(todo)} entities to capture in {scene}")
    obj_dir = out_dir / "objects"
    index = []
    for e in todo:
        safe = "".join(c if c.isalnum() or c in "._-" else "_" for c in e["name"])
        dest = obj_dir / f"{safe}.png"
        if not args.force and dest.is_file():
            continue
        labels = frame_and_shoot(dev, dest, e["x"], e["y"], e["z"], args.object_zoom, args.settle, args.max_width)
        index.append({**e, "file": dest.name, "labels": labels["labels"]})
        print(f"  {e['name']} ({e['type']}, zone {e['zone']})")
    idx_path = obj_dir / "index.json"
    merged = index
    if idx_path.is_file():
        old = json.loads(idx_path.read_text(encoding="utf-8"))
        keep = {i["name"] for i in index}
        merged = [o for o in old if o["name"] not in keep] + index
    if merged:
        obj_dir.mkdir(parents=True, exist_ok=True)
        idx_path.write_text(json.dumps(merged, indent=2, ensure_ascii=False), encoding="utf-8")


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("mode", choices=["status", "zones", "shot", "entities", "objects"])
    ap.add_argument("--url", default="http://127.0.0.1:8771")
    ap.add_argument("--out", default=str(REPO / "survey"))
    ap.add_argument("--zone", type=int, action="append", help="zones mode: only these zone ids")
    ap.add_argument("--name", action="append", help="objects mode: only these entity names")
    ap.add_argument("--at", help="shot mode: X,Z (mod frame); Y from the character")
    ap.add_argument("--zoom", type=float, default=1.0, help="zone/shot zoom factor, 1 = widest")
    ap.add_argument("--object-zoom", type=float, default=0.3, help="objects mode zoom factor")
    ap.add_argument("--max-points", type=int, default=4)
    ap.add_argument("--max-width", type=int, default=1600)
    ap.add_argument("--settle", type=float, default=0.6)
    ap.add_argument("--force", action="store_true")
    args = ap.parse_args()

    dev = Dev(args.url)
    status = dev.call(f"{NS}Status()")
    scene = status["scene"]
    if args.mode == "status":
        print(json.dumps(status, indent=2))
        return
    if not status["zonesReady"]:
        sys.exit("zone map not built yet")
    out_dir = Path(args.out) / scene
    try:
        if args.mode == "zones":
            survey_zones(dev, args, scene, out_dir)
        elif args.mode == "entities":
            data = dev.call(f"{NS}Entities()")
            out_dir.mkdir(parents=True, exist_ok=True)
            (out_dir / "entities.json").write_text(json.dumps(data, indent=2, ensure_ascii=False), encoding="utf-8")
            print(f"{len(data['entities'])} entities -> {out_dir / 'entities.json'}")
        elif args.mode == "objects":
            survey_objects(dev, args, scene, out_dir)
        elif args.mode == "shot":
            x, z = (float(v) for v in args.at.split(","))
            y = status["player"]["y"]
            dest = out_dir / "shots" / f"shot_{x:+.1f}_{z:+.1f}_z{args.zoom:.2f}.png"
            frame_and_shoot(dev, dest, x, y, z, args.zoom, args.settle, args.max_width)
            print(dest)
    finally:
        if args.mode != "entities":
            dev.call(f"{NS}Restore()")
            print("camera, HUD and fog restored")


if __name__ == "__main__":
    main()
