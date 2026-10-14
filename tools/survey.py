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
             With --inactive, one capture per distinct name among the inactive props and containers
             (the early-game twins this save has looted or removed), each switched on for its shot
             and off again, to survey/<scene>/objects-inactive/. Never save after such a run.
  states     List the game's location state machines with their switchers' positions.
  variants   For each location machine with switchers in this scene (or --location ...), switch
             through every state, capture at the switchers' spot, and switch back. Output:
             survey/<scene>/variants/<LOCATION>/<state>.png + index.json. The game is left in its
             original states; never save during or after a variants run.

Captures hide the HUD, lift every fog and light the scene as noon; the camera, HUD, fog, lighting
and (after --teleport) the character's spot are restored at the end (also on Ctrl+C). Shots are downscaled to --max-width for review. Coordinates are the mod's world frame.
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
            if args.teleport:
                # Stand the character on the spot so the game cuts the roof away over it.
                try:
                    dev.call(f"{NS}Teleport({pt['x']}f, {pt['y']}f, {pt['z']}f)")
                except RuntimeError as ex:
                    print(f"  zone {zid} point {pi}: teleport skipped: {ex}")
                time.sleep(0.5)
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
    if args.inactive:
        # The early-game twins: containers looted or props removed by this save's story. One
        # capture per distinct name (the description is keyed by name), props and containers only,
        # never a character, trigger or outline, which may run story logic when switched on.
        seen = {e["name"] for e in entities if e["active"]}  # an active namesake is captured already
        todo = []
        for e in entities:
            if e["active"] or e["type"] not in ("BasicEntity", "ContainerSource"):
                continue
            if any(w in e["name"] for w in ("Trigger", "outline", "Perception", "Trap", "Viscal")):
                continue
            if e["name"] in seen or (args.name and e["name"] not in args.name):
                continue
            seen.add(e["name"])
            todo.append(e)
    else:
        todo = [e for e in entities if e["active"] and (not args.name or e["name"] in args.name)]
    print(f"{len(todo)} entities to capture in {scene}")
    obj_dir = out_dir / ("objects-inactive" if args.inactive else "objects")
    index = []
    for e in todo:
        safe = "".join(c if c.isalnum() or c in "._-" else "_" for c in e["name"])
        dest = obj_dir / f"{safe}.png"
        if not args.force and dest.is_file():
            continue
        fx, fy, fz = e["x"], e["y"], e["z"]
        if args.teleport:
            # Stand the character beside the thing so the game cuts the roof away and lifts the
            # room fog, as it does in play; the capture settles after the move. The shot is framed
            # on the character's spot, the nearest floor: a thing's own transform (a wall shelf, a
            # prop set behind a wall) aims the camera over the wall into the void behind it.
            try:
                at = dev.call(f"{NS}Teleport({e['x']}f, {e['y']}f, {e['z']}f)")["at"]
                fx, fy, fz = at["x"], at["y"], at["z"]
            except RuntimeError as ex:
                print(f"  {e['name']}: teleport skipped: {ex}")  # off the mesh; shot from where we stand
            time.sleep(0.5)
        if args.inactive:
            r = dev.call(f'{NS}ActivateEntity({json.dumps(e["name"])})')
            if not r.get("ok"):
                print(f"  {e['name']}: {r}")
                continue
        try:
            labels = frame_and_shoot(dev, dest, fx, fy, fz, args.object_zoom, args.settle, args.max_width)
        finally:
            if args.inactive:
                dev.call(f"{NS}RestoreEntities()")
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


def survey_variants(dev, args, scene, out_dir):
    locs = dev.call(f"{NS}States()")["locations"]
    todo = []
    for l in locs:
        here = [sw for sw in l["switchers"] if sw["scene"] == scene]
        if not here or not l["states"]:
            continue
        if args.location and l["name"] not in args.location:
            continue
        todo.append((l, here))
    print(f"{len(todo)} location machines with switchers in {scene}")
    var_dir = out_dir / "variants"
    index = []
    floor_y = dev.call(f"{NS}Status()")["player"]["y"]
    for l, here in todo:
        # One capture per distinct switcher spot (within 6 m), skipping switchers parked at the scene
        # origin or far off the map (FX and animation drivers); --at overrides the spots outright.
        spots = []
        if args.at:
            x, z = (float(v) for v in args.at.split(","))
            spots.append((x, floor_y, z))
        else:
            for sw in here:
                if abs(sw["x"]) < 0.5 and abs(sw["z"]) < 0.5:
                    continue
                if any(abs(sw["x"] - sx) < 6 and abs(sw["z"] - sz) < 6 for sx, _, sz in spots):
                    continue
                spots.append((sw["x"], sw["y"], sw["z"]))
        if not spots:
            print(f"  {l['name']}: switchers carry no usable position; pass --location {l['name']} --at X,Z")
            continue
        original = l["current"]
        loc_dir = var_dir / l["name"]
        try:
            for state in l["states"]:
                entered = False
                for i, (cx, cy, cz) in enumerate(spots):
                    dest = loc_dir / (f"{state}.png" if len(spots) == 1 else f"{state}_{i}.png")
                    if not args.force and dest.is_file():
                        continue
                    if not entered:
                        r = dev.call(f'{NS}SetState("{l["name"]}", "{state}")')
                        if not r.get("ok"):
                            print(f"  {l['name']}: could not enter {state}: {r}")
                            break
                        entered = True
                        time.sleep(0.4)
                    frame_and_shoot(dev, dest, cx, cy, cz, args.zoom, args.settle, args.max_width)
                    index.append({"location": l["name"], "state": state, "file": str(dest.relative_to(var_dir)),
                                  "at": {"x": cx, "y": cy, "z": cz}, "switchers": here})
                if entered:
                    print(f"  {l['name']} = {state} ({len(spots)} spots)")
        finally:
            if original:
                dev.call(f'{NS}SetState("{l["name"]}", "{original}")')
    if index:
        var_dir.mkdir(parents=True, exist_ok=True)
        idx_path = var_dir / "index.json"
        merged = index
        if idx_path.is_file():
            old = json.loads(idx_path.read_text(encoding="utf-8"))
            keep = {(i["location"], i["state"]) for i in index}
            merged = [o for o in old if (o["location"], o["state"]) not in keep] + index
        idx_path.write_text(json.dumps(merged, indent=2, ensure_ascii=False), encoding="utf-8")


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("mode", choices=["status", "zones", "shot", "entities", "objects", "states", "variants"])
    ap.add_argument("--location", action="append", help="variants mode: only these location machines")
    ap.add_argument("--url", default="http://127.0.0.1:8771")
    ap.add_argument("--out", default=str(REPO / "survey"))
    ap.add_argument("--zone", type=int, action="append", help="zones mode: only these zone ids")
    ap.add_argument("--name", action="append", help="objects mode: only these entity names")
    ap.add_argument("--teleport", action="store_true",
                    help="objects and zones modes: warp the character to each spot first (roofed and fogged spots); Restore warps back")
    ap.add_argument("--inactive", action="store_true",
                    help="objects mode: capture the inactive early-game twins instead, switching each on for its shot")
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
        elif args.mode == "states":
            for l in dev.call(f"{NS}States()")["locations"]:
                scenes = sorted({sw["scene"] for sw in l["switchers"]})
                print(f"{l['name']}: {l['current']} of {l['states']} switchers in {scenes}")
        elif args.mode == "variants":
            survey_variants(dev, args, scene, out_dir)
        elif args.mode == "shot":
            x, z = (float(v) for v in args.at.split(","))
            y = status["player"]["y"]
            dest = out_dir / "shots" / f"shot_{x:+.1f}_{z:+.1f}_z{args.zoom:.2f}.png"
            frame_and_shoot(dev, dest, x, y, z, args.zoom, args.settle, args.max_width)
            print(dest)
    finally:
        if args.mode not in ("entities", "states"):
            dev.call(f"{NS}Restore()")
            print("camera, HUD and fog restored")


if __name__ == "__main__":
    main()
