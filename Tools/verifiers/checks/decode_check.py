#!/usr/bin/env python3
"""decode_check.py: does the bake read the Terrarium tiles the way the format's publisher says?

Reads one cached AWS Terrain Tile (Terrarium encoding) as raw pixels and applies the published decoding by hand,
    elevation_m = R * 256 + G + B / 256 - 32768
(Mapzen / AWS Terrain Tiles documentation, "Terrarium" format), then compares it with what the bake wrote into
the region raster at the same places. Also exercises the formula on values it must reproduce exactly. Never
imports Tools/data/terrarium.py or bake_region.py: the tile is opened with Pillow, the raster with numpy and the
sidecar with json, and the tile arithmetic (Web Mercator) is written here from the published definition.

Checks, each printed with both numbers:
  1. the formula's fixed points: pixel (128, 0, 0) is 0 m; pixel (0, 0, 0) is -32768 m; 123.5 m survives an
     encode-decode round trip;
  2. a point behind Bherwerre Beach decodes to land, above sea level and below the peninsula's cliffs, and the
     bake's raster agrees with the tile's own pixel there within a few metres (bilinear resampling of 7.8 m pixels
     onto 4 m cells is allowed to smooth, never to move the ground);
  3. a point a kilometre off Cave Beach decodes to sea (at or below zero) in the tile and in the raster alike.

Exit 0 when every check passes, 1 when any fails, 2 when a file it needs is missing.
Run from the repository root:  python Tools/verifiers/checks/decode_check.py
"""
import json
import math
import os
import sys

import numpy as np
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))))
CACHE = os.path.join(ROOT, "Data", "cache", "terrarium")
REGION = os.path.join(ROOT, "Data", "regions", "bherwerre")
ZOOM = 14

# Places, restated here rather than read from the engine or the tools. The point behind the beach is the site
# research's first wake (CANON ruling 8); ruling 20 withdrew it as a wake, and it stays here as land to probe.
LAND = ("a point behind Bherwerre Beach", -35.159, 150.6485)
SEA = ("a kilometre off Cave Beach", -35.172, 150.650)
LAND_MIN_M, LAND_MAX_M = 1.0, 150.0     # a coastal swale: above the beach, well below the 130 m cliffs
SEA_MIN_M, SEA_MAX_M = -200.0, 0.0      # inner shelf off the peninsula
RASTER_TOLERANCE_M = 5.0


def decode(r, g, b):
    """The published Terrarium formula, on scalars or arrays."""
    return r * 256.0 + g + b / 256.0 - 32768.0


def encode(elevation_m):
    """The inverse, as the format's publisher describes it: split (elevation + 32768) into 256ths."""
    v = elevation_m + 32768.0
    r = int(v // 256)
    g = int(v - r * 256)
    b = int(round((v - r * 256 - g) * 256))
    return r, g, b


def tile_xy(lat, lon, zoom):
    """Web Mercator tile coordinates, fractional (OpenStreetMap slippy-map definition)."""
    n = 2 ** zoom
    x = (lon + 180.0) / 360.0 * n
    phi = math.radians(lat)
    y = (1.0 - math.log(math.tan(phi) + 1.0 / math.cos(phi)) / math.pi) / 2.0 * n
    return x, y


def tile_pixel(lat, lon, zoom):
    """The tile that holds a place and the pixel inside it, and the decoded elevation of that pixel."""
    x, y = tile_xy(lat, lon, zoom)
    tx, ty = int(x), int(y)
    px, py = int((x - tx) * 256), int((y - ty) * 256)
    path = os.path.join(CACHE, str(zoom), str(tx), "%d.png" % ty)
    if not os.path.isfile(path):
        return None, path, (tx, ty, px, py)
    rgb = np.asarray(Image.open(path).convert("RGB"), dtype=np.float64)
    r, g, b = rgb[py, px]
    return decode(r, g, b), path, (tx, ty, px, py)


def raster_at(lat, lon, sidecar, heights):
    """The bake's raster at a place, by the sidecar's stated frame: cell centres at east = col*cell - extent/2,
    north = extent/2 - row*cell, with the small-angle map about the centre."""
    R = 6371000.0
    north = (lat - sidecar["centre_lat"]) * math.pi / 180.0 * R
    east = (lon - sidecar["centre_lon"]) * math.pi / 180.0 * R * math.cos(math.radians(sidecar["centre_lat"]))
    half = sidecar["extent_m"] / 2.0
    col = int(round((east + half) / sidecar["cell_m"]))
    row = int(round((half - north) / sidecar["cell_m"]))
    return float(heights[row, col]), (row, col)


def main():
    failures = []

    def expect(name, ok, detail):
        print("%-34s %s  %s" % (name, "ok " if ok else "FAIL", detail))
        if not ok:
            failures.append(name)

    # 1. The formula on values it must reproduce exactly.
    expect("formula: (128,0,0) is sea level", decode(128, 0, 0) == 0.0, "decodes to %.3f m" % decode(128, 0, 0))
    expect("formula: (0,0,0) is the floor", decode(0, 0, 0) == -32768.0, "decodes to %.1f m" % decode(0, 0, 0))
    r, g, b = encode(123.5)
    expect("formula: 123.5 m round-trips", abs(decode(r, g, b) - 123.5) < 1e-9, "encoded as (%d,%d,%d), decoded %.4f m" % (r, g, b, decode(r, g, b)))

    # The bake's raster, read by hand from its sidecar's stated shape.
    sidecar_path = os.path.join(REGION, "heights.json")
    raw_path = os.path.join(REGION, "heights.r32")
    if not (os.path.isfile(sidecar_path) and os.path.isfile(raw_path)):
        print("the region raster is missing (%s); bake it first" % REGION)
        return 2
    sidecar = json.load(open(sidecar_path, encoding="utf-8"))
    heights = np.fromfile(raw_path, dtype="<f4").reshape(sidecar["height"], sidecar["width"])

    # 2. Land behind the beach, in the tile and in the raster.
    for (name, lat, lon), (lo, hi) in ((LAND, (LAND_MIN_M, LAND_MAX_M)), (SEA, (SEA_MIN_M, SEA_MAX_M))):
        tile_m, path, where = tile_pixel(lat, lon, ZOOM)
        if tile_m is None:
            print("tile not in the cache: %s (fetch it first)" % path)
            return 2
        raster_m, cell = raster_at(lat, lon, sidecar, heights)
        expect("tile decodes %s" % name, lo <= tile_m <= hi,
               "tile %d/%d pixel (%d,%d) -> %.1f m, expected %.0f..%.0f" % (where[0], where[1], where[2], where[3], tile_m, lo, hi))
        expect("raster agrees at %s" % name, abs(raster_m - tile_m) <= RASTER_TOLERANCE_M,
               "raster cell %s -> %.1f m, tile %.1f m, difference %.1f, tolerance %.0f" % (cell, raster_m, tile_m, abs(raster_m - tile_m), RASTER_TOLERANCE_M))
        expect("raster classifies %s" % name, lo <= raster_m <= hi, "raster %.1f m, expected %.0f..%.0f" % (raster_m, lo, hi))

    if failures:
        print("decode_check: FAIL (%s)" % ", ".join(failures))
        return 1
    print("decode_check: ok, the bake reads the tiles as the format's publisher describes")
    return 0


if __name__ == "__main__":
    sys.exit(main())
