#!/usr/bin/env python3
"""bake_geology.py: the rock units of the Geological Survey of NSW's seamless geology under a region, as a raster of unit codes
with a legend naming each unit, its lithology and the stone the game knows it as (BF.4 stage three, the stone of the place).

The units come from `Data/cache/nsw-seamless-geology/` (fetched by `fetch_geology.py`, on William's yes of 2026-09-25): every
rock unit polygon of `geology:rock_units_nsw` touching the region's box. Each polygon's outer rings are filled with its unit's
code and its holes cleared, feature by feature within the feature's own box, on the region's grid by the small-angle rule about
the centre, the rule the heights and the water were baked with; a cell is inside when its centre is. Where two features claim a
cell (they tile the state, and meet only on their shared edges) the later in the file's order keeps it. Cells no feature covers,
and cells under a unit humanity made (a reservoir's water, a breakwater), take the nearest natural unit's code, since this Earth
has no dams (GAME_DESIGN section 2).

The legend says what each unit stands for in the game (a model, DEBTS "The stone of the place is a stated mapping"): the rock
beneath it by its dominant lithology, a `StoneType` name the engine resolves (`StoneType.ByName`), or none where the unit is
loose sediment (sand, alluvium, gravel, swamp) and stands no rock of its own. The region's rolled stone, what lies on a beach
and in a creek's bed, is the region's own table here, each with its source, since no rock unit names it.

Claude's tool: it reads the cached units and writes the layer; it fetches nothing.

Usage, from the repository root:
    python Tools/data/bake_geology.py --region bherwerre
    python Tools/data/bake_geology.py --region kangaroo-valley --units kangaroo-valley-whole
    python Tools/data/bake_geology.py --region kangaroo-valley-whole
Writes Data/regions/<region>/geology.u8 and geology.json.
"""
import argparse
import json
import math
import os
import sys

import numpy as np
from PIL import Image, ImageDraw

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import raster_io  # noqa: E402

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
UNITS = os.path.join(ROOT, "Data", "cache", "nsw-seamless-geology")
EARTH_RADIUS_M = 6371000.0

# The rock beneath a unit by its dominant lithology, as the Survey's own field names it, and a StoneType name; None for loose
# sediment. Latite is the Gerringong Volcanics' fine-grained dark lava, nearest to basalt of the stones the game knows, as
# are basanite (a lava), dolerite (basalt grown coarser in a dyke) and the valley's dark alkaline dyke rock; a syenite or a
# diorite, coarse and speckled, nearest to granite; a conglomerate's matrix is sandstone, its pebbles the rolled stone.
LITHOLOGY = {
    "Sandstone": "Sandstone",
    "Conglomerate": "Sandstone",
    "Siltstone": "Shale",
    "Shale": "Shale",
    "Mudstone": "Shale",
    "Claystone": "Shale",
    "Siliciclastic sedimentary rock": "Shale",
    "Basalt": "Basalt",
    "Latite": "Basalt",
    "Basanite": "Basalt",
    "Dolerite": "Basalt",
    "Alkaline igneous rock": "Basalt",
    "Rhyolite": "Rhyolite",
    "Foid-bearing syenite": "Granite",
    "Diorite": "Granite",
    "Saprolite": "Sandstone",
    "Iron rich sediment": "Sandstone",
    "Sand": None,
    "Gravel": None,
    "Clastic sediment": None,
    "Organic rich sediment": None,
    "Clay": None,
}
# Units of humanity's making, whose cells take the nearest natural unit.
HUMAN_MADE = ("Anthropogenic material",)

# What a region's beaches and creeks carry, rolled from elsewhere (a model with sources, DEBTS): a stone name and its share.
ROLLED = {
    "bherwerre": {
        "beach": [("Rhyolite", 0.45), ("Quartz", 0.35), ("Quartzite", 0.20)],
        "creek": [("Quartz", 0.55), ("Quartzite", 0.30), ("Sandstone", 0.15)],
        "source": "the coast's pebbles as ECOSYSTEM.md has them from the archaeological record of the Jervis Bay coast (rhyolite, "
                  "quartz and quartzite beach pebbles; silcrete and quartz on the old sand surfaces), the shares the lattice rule "
                  "used since M1.2",
    },
    "kangaroo-valley": {
        "beach": [("Quartz", 0.5), ("Quartzite", 0.5)],
        "creek": [("Quartzite", 0.35), ("Basalt", 0.25), ("Quartz", 0.2), ("Sandstone", 0.12), ("Chert", 0.08)],
        "source": "the Kangaroo River's gravels as WG.2 read them from the Nowra 1:100 000 sheet's notes (basalt from the "
                  "Robertson caps, quartzite and chert from the Kangaloon Sandstone's pebbles, sandstone); the shares an estimate",
    },
}
ROLLED["kangaroo-valley-whole"] = ROLLED["kangaroo-valley"]


def rings(geometry):
    """The polygons of a geometry, each as its outer ring and its holes, in longitude and latitude."""
    if geometry["type"] == "Polygon":
        return [geometry["coordinates"]]
    if geometry["type"] == "MultiPolygon":
        return geometry["coordinates"]
    return []


def main():
    p = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    p.add_argument("--region", required=True)
    p.add_argument("--units", default=None, help="the cached units' region, when another box holds this one (default: the region)")
    args = p.parse_args()
    heights = json.load(open(os.path.join(ROOT, "Data", "regions", args.region, "heights.json"), encoding="utf-8"))
    lat0, lon0 = float(heights["centre_lat"]), float(heights["centre_lon"])
    extent, cell = float(heights["extent_m"]), float(heights["cell_m"])
    half = extent / 2.0
    side = raster_io.expected_side(extent, cell)
    cos_lat = math.cos(math.radians(lat0))
    name = (args.units or args.region) + "-rock-units.geojson"
    manifest = json.load(open(os.path.join(UNITS, "manifest.json"), encoding="utf-8"))
    entry = manifest["files"][name]
    doc = json.load(open(os.path.join(UNITS, name), encoding="utf-8"))

    def to_pixel(lon, lat):
        east = math.radians(lon - lon0) * EARTH_RADIUS_M * cos_lat
        north = math.radians(lat - lat0) * EARTH_RADIUS_M
        return (east + half) / cell, (half - north) / cell

    # The legend: one code a unit, by the Survey's code and name, in the order units are first met.
    units, code_of = [], {}
    grid = np.zeros((side, side), dtype=np.uint8)
    for feature in doc["features"]:
        props = feature["properties"]
        key = (props.get("nsw_code") or "", (props.get("unit_name") or "").strip())
        lith = (props.get("dominant_lithology") or "").strip()
        if key not in code_of:
            if len(units) >= 255:
                raise SystemExit("more than 255 units under %s; the u8 layer holds 255" % args.region)
            human = lith in HUMAN_MADE
            if not human and lith not in LITHOLOGY:
                raise SystemExit("a lithology this bake does not map: %r (%s)" % (lith, key[1]))
            code_of[key] = len(units) + 1
            units.append({"code": len(units) + 1, "nsw_code": key[0], "name": key[1], "lithology": lith,
                          "age": (props.get("age_range") or "").strip(), "province": (props.get("province") or "").strip(),
                          "stone": None if human else LITHOLOGY[lith], "human_made": human, "cells": 0})
        code = code_of[key]
        for polygon in rings(feature["geometry"]):
            outer = [to_pixel(x, y) for x, y in polygon[0]]
            xs, ys = [q[0] for q in outer], [q[1] for q in outer]
            x0, x1 = max(0, int(math.floor(min(xs)))), min(side - 1, int(math.ceil(max(xs))))
            y0, y1 = max(0, int(math.floor(min(ys)))), min(side - 1, int(math.ceil(max(ys))))
            if x0 > x1 or y0 > y1:
                continue
            # The polygon filled within its own box: a cell whose centre (col + 0.5 in the drawing's terms) is inside.
            w, h = x1 - x0 + 1, y1 - y0 + 1
            mask = Image.new("1", (w, h), 0)
            draw = ImageDraw.Draw(mask)
            draw.polygon([(x - x0 + 0.5, y - y0 + 0.5) for x, y in outer], fill=1)
            for hole in polygon[1:]:
                draw.polygon([(x - x0 + 0.5, y - y0 + 0.5) for x, y in (to_pixel(a, b) for a, b in hole)], fill=0)
            inside = np.array(mask, dtype=bool)
            grid[y0:y1 + 1, x0:x1 + 1][inside] = code

    # Humanity's units and cells no feature covers take the nearest natural unit, growing it outward a cell at a time.
    natural = np.zeros(len(units) + 1, dtype=bool)
    for u in units:
        natural[u["code"]] = not u["human_made"]
    known = natural[grid]
    filled_in = int((~known).sum())
    rounds = 0
    while not known.all():
        rounds += 1
        if rounds > side:
            raise SystemExit("cells no natural unit reaches")
        for dy, dx in ((-1, 0), (1, 0), (0, -1), (0, 1)):
            shifted = np.roll(np.roll(grid, dy, axis=0), dx, axis=1)
            shifted_known = np.roll(np.roll(known, dy, axis=0), dx, axis=1)
            take = ~known & shifted_known
            grid[take] = shifted[take]
            known |= take
    for u in units:
        u["cells"] = int((grid == u["code"]).sum())
    rolled = ROLLED.get(args.region)
    if rolled is None:
        raise SystemExit("no rolled stone stated for %s" % args.region)
    source = ("Geological Survey of NSW seamless geology, rock unit polygons (geology:rock_units_nsw), %d features touching the box "
              "in %s (SHA-256 %s, fetched %s); each unit's polygons filled on the region's grid, humanity's units and uncovered "
              "cells (%d) given the nearest natural unit" % (len(doc["features"]), name, entry["sha256"], entry["fetched_utc"], filled_in))
    out_dir = os.path.join(ROOT, "Data", "regions", args.region)
    sidecar = raster_io.write_raster(out_dir, "geology", args.region, grid, cell, extent, lat0, lon0, source, "Tools/data/bake_geology.py",
                                     "(c) State Government of NSW and Department of Primary Industries and Regional Development (DPIRD) 2025, "
                                     "NSW Seamless Geology, CC BY 4.0", layer="geology", dtype="u8", scale=1.0, unit="id",
                                     extra={"units": units, "rolled": rolled})
    print("wrote %s: %d units, sha256 %s" % (os.path.join(out_dir, sidecar["raw"]), len(units), sidecar["sha256"]))
    area = cell * cell / 1e6
    for u in sorted(units, key=lambda u: -u["cells"]):
        if u["cells"]:
            print("  %3d  %8.2f km2  %-11s %-26s %s" % (u["code"], u["cells"] * area, u["stone"] or "(loose)", u["lithology"], u["name"]))
    return 0


if __name__ == "__main__":
    sys.exit(main())
