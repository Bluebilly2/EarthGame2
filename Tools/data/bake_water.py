#!/usr/bin/env python3
"""bake_water.py: the region's mapped water bodies as a raster, from OpenStreetMap.

The bake's heights cannot show a lake: over Lake Windermere the Terrarium tiles carry a noisy surface from 12 m
to 50 m and over Lake McKenzie a bowl from 21 m to 57 m (looked at on 2026-09-09), so a rule that reads lakes
off the ground alone either floods a closed basin to its lip or finds fragments. The outlines are known, though:
OpenStreetMap maps the lakes and the swamps of the peninsula as polygons. This tool fetches every closed way
tagged natural=water or natural=wetland inside the box (padded by a kilometre), rasterises each onto the region's
grid with its own code, and writes `water_bodies.u8` beside the heights with a legend in the sidecar
(`bodies`: code, OSM id, name, kind). The world-creation pipeline reads the layer when it is present: a lake's
level is the median of the bake's heights inside its outline, the cells at or below that level are the lake,
a wetland's cells are swamp. Without the layer the pipeline falls back to the ground alone.

A way tagged water=reservoir or landuse=reservoir is a dam's lake, humanity's, and is excluded and listed in the sidecar
(`excluded`), since the constitution's Earth has no dams (WG.2, 2026-09-22).
Kinds: `lake` (natural=water without a salt tag), `wetland` (natural=wetland), `salt` (natural=water tagged
water=bay, lagoon or harbour, or salt=yes; recorded, not yet used by the pipeline). Relations (St Georges
Basin, a multipolygon west of the box) are not rasterised.

The frame is the sidecar's: cell centres at east = col * cell - extent / 2, north = extent / 2 - row * cell,
latitude and longitude mapped by the small-angle rule about the centre, the same rule the heights were baked
with. Polygon nodes are placed by that rule and filled by Pillow; a cell is inside when its centre is.

The Overpass response is cached under Data/cache/osm/ and reused until --refresh; the sidecar records the fetch
date. Data © OpenStreetMap contributors, ODbL 1.0 (THIRD_PARTY_NOTICES.md).

Usage, from the repository root:
    python Tools/data/bake_water.py            (defaults: bherwerre, 4 m cells, 8 km)
    python Tools/data/bake_water.py --refresh
"""
import argparse
import json
import math
import os
import sys
import time

import numpy as np
import requests
from PIL import Image, ImageDraw

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import raster_io  # noqa: E402
import terrarium  # noqa: E402

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OVERPASS = "https://overpass-api.de/api/interpreter"
QUERY = '[out:json][timeout:90];(way["natural"~"^(water|wetland)$"](%.6f,%.6f,%.6f,%.6f););out tags geom;'
EARTH_RADIUS_M = 6371000.0
PAD_M = 1000.0
SALT_WATER = ("bay", "lagoon", "harbour", "sea")
# A reservoir is a dam's lake: humanity's, and the constitution's Earth has no dams (GAME_DESIGN section 2). Excluded from
# the layer and listed in the sidecar, so a census can name what the bake left out (WG.2, 2026-09-22: Lake Yarrunga).
HUMAN_MADE = ("reservoir",)


def is_human_made(tags):
    return tags.get("water") in HUMAN_MADE or tags.get("landuse") in HUMAN_MADE or tags.get("man_made") == "reservoir"


def fetch(cache_path, south, west, north, east, refresh):
    if os.path.isfile(cache_path) and not refresh:
        with open(cache_path, encoding="utf-8") as f:
            return json.load(f), False
    response = requests.get(OVERPASS, params={"data": QUERY % (south, west, north, east)},
                            headers={"User-Agent": "EarthGame2 bake_water.py (a game's map bake; one query per bake)"}, timeout=180)
    response.raise_for_status()
    data = response.json()
    data["_fetched_utc"] = time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime())
    os.makedirs(os.path.dirname(cache_path), exist_ok=True)
    tmp = cache_path + ".part"
    with open(tmp, "w", encoding="utf-8") as f:
        json.dump(data, f)
    os.replace(tmp, cache_path)
    return data, True


def kind_of(tags):
    if tags.get("natural") == "wetland":
        return "wetland"
    if tags.get("water") in SALT_WATER or tags.get("salt") == "yes":
        return "salt"
    return "lake"


def main():
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("--region", default="bherwerre")
    p.add_argument("--name", default="water_bodies")
    p.add_argument("--centre-lat", type=float, default=terrarium.BHERWERRE_CENTRE_LAT)
    p.add_argument("--centre-lon", type=float, default=terrarium.BHERWERRE_CENTRE_LON)
    p.add_argument("--extent-m", type=float, default=terrarium.BHERWERRE_EXTENT_M)
    p.add_argument("--cell-m", type=float, default=4.0)
    p.add_argument("--refresh", action="store_true", help="fetch again instead of reading the cached response")
    args = p.parse_args()

    lat0, lon0 = args.centre_lat, args.centre_lon
    half = args.extent_m / 2.0
    side = raster_io.expected_side(args.extent_m, args.cell_m)
    cos_lat = math.cos(math.radians(lat0))
    dlat = math.degrees((half + PAD_M) / EARTH_RADIUS_M)
    dlon = math.degrees((half + PAD_M) / (EARTH_RADIUS_M * cos_lat))
    cache_path = os.path.join(ROOT, "Data", "cache", "osm", "%s-water.json" % args.region)
    data, fetched = fetch(cache_path, lat0 - dlat, lon0 - dlon, lat0 + dlat, lon0 + dlon, args.refresh)
    fetched_utc = data.get("_fetched_utc", "unknown")
    print("%s the Overpass response of %s (%d elements)" % ("fetched" if fetched else "read", fetched_utc, len(data.get("elements", []))))

    def to_pixel(lat, lon):
        east = math.radians(lon - lon0) * EARTH_RADIUS_M * cos_lat
        north = math.radians(lat - lat0) * EARTH_RADIUS_M
        return (east + half) / args.cell_m + 0.5, (half - north) / args.cell_m + 0.5

    image = Image.new("L", (side, side), 0)
    draw = ImageDraw.Draw(image)
    bodies = []
    excluded = []
    ways = sorted((e for e in data.get("elements", []) if e.get("type") == "way" and e.get("geometry")), key=lambda e: e["id"])
    for way in ways:
        nodes = way["geometry"]
        if len(nodes) < 4 or nodes[0] != nodes[-1]:
            continue    # an open way is a shoreline or a river bank, not a body
        if is_human_made(way.get("tags", {})):
            excluded.append({"osm": "way/%d" % way["id"], "name": way.get("tags", {}).get("name", ""), "why": "a reservoir: humanity's, and this Earth has no dams"})
            continue
        points = [to_pixel(n["lat"], n["lon"]) for n in nodes[:-1]]
        code = len(bodies) + 1
        if code > 255:
            raise SystemExit("more than 255 water bodies in the box; the u8 layer cannot hold them")
        tags = way.get("tags", {})
        draw.polygon(points, fill=code)
        bodies.append({"code": code, "osm": "way/%d" % way["id"], "name": tags.get("name", ""), "kind": kind_of(tags), "nodes": len(points)})

    grid = np.array(image, dtype=np.uint8)
    if grid.shape != (side, side):
        raise SystemExit("the image is %s, not %d x %d" % (grid.shape, side, side))
    for body in bodies:
        body["cells"] = int((grid == body["code"]).sum())
    inside = [b for b in bodies if b["cells"] > 0]
    legend = ", ".join("%d=%s (%s, %s, %d cells)" % (b["code"], b["name"] or "unnamed", b["osm"], b["kind"], b["cells"]) for b in bodies)
    source = ("OpenStreetMap closed ways tagged natural=water or natural=wetland within the box padded by %.0f m, fetched through "
              "Overpass on %s, reservoirs excluded as humanity's; each body's outline filled with its own code, 0 outside every outline: %s"
              % (PAD_M, fetched_utc, legend))
    out_dir = os.path.join(ROOT, "Data", "regions", args.region)
    sidecar = raster_io.write_raster(out_dir, args.name, args.region, grid, args.cell_m, args.extent_m, lat0, lon0, source,
                                     "Tools/data/bake_water.py", "(c) OpenStreetMap contributors, ODbL 1.0, https://www.openstreetmap.org/copyright",
                                     layer="water_bodies", dtype="u8", scale=1.0, unit="id",
                                     extra={"bodies": bodies, "excluded": excluded, "fetched_utc": fetched_utc, "padding_m": PAD_M})
    print("wrote %s (%d bodies, %d with cells in the box) sha256 %s" % (os.path.join(out_dir, sidecar["raw"]), len(bodies), len(inside), sidecar["sha256"]))
    for b in bodies:
        print("  %3d  %-8s %-22s %-14s %6.1f ha in the box" % (b["code"], b["kind"], b["name"] or "(unnamed)", b["osm"], b["cells"] * args.cell_m * args.cell_m / 1e4))
    for e in excluded:
        print("  excluded %-22s %-14s %s" % (e["name"] or "(unnamed)", e["osm"], e["why"]))
    return 0


if __name__ == "__main__":
    sys.exit(main())
