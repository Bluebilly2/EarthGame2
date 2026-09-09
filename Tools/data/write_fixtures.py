#!/usr/bin/env python3
"""Write the small fixtures the engine's tests read (Claude's tool). Idempotent; committed under Data/fixtures/.

    python Tools/data/write_fixtures.py

- frame/bherwerre_corners.json: local (east, north) points and the (lon, lat) the bake's small-angle rule maps them
  to, so the C# LocalFrame is pinned to the Python bake rather than to a shared belief (CoordinateTests).
- raster/tiny.r32 + tiny.json: a 5 x 5 raster at 10 m over a 40 m extent, written by the same writer as the real
  bake, with heights that are a known function of (row, col) so the loader's tests can say what every cell must
  read (RegionRasterTests).
"""
import json
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import raster_io  # noqa: E402
import terrarium  # noqa: E402

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
FIXTURES = os.path.join(ROOT, "Data", "fixtures")


def tiny_height(row, col):
    """The fixture's height law: 100 m plus a metre per row south plus ten per column east, and a 5 m bump at the centre."""
    h = 100.0 + row * 1.0 + col * 10.0
    if row == 2 and col == 2:
        h += 5.0
    return h


def write_frame_fixture():
    half = terrarium.BHERWERRE_EXTENT_M / 2.0
    points = []
    for east, north in [(0.0, 0.0), (half, half), (-half, half), (half, -half), (-half, -half), (1000.0, 0.0), (0.0, 1000.0), (-2500.0, 3100.0)]:
        lon, lat = terrarium.local_to_lonlat(east, north, terrarium.BHERWERRE_CENTRE_LAT, terrarium.BHERWERRE_CENTRE_LON)
        points.append({"east": east, "north": north, "lon": lon, "lat": lat})
    doc = {
        "format": "eg2.frame-fixture",
        "version": 1,
        "centre_lat": terrarium.BHERWERRE_CENTRE_LAT,
        "centre_lon": terrarium.BHERWERRE_CENTRE_LON,
        "extent_m": terrarium.BHERWERRE_EXTENT_M,
        "earth_radius_m": terrarium.EARTH_RADIUS_M,
        "rule": "lat = centre_lat + deg(north / R); lon = centre_lon + deg(east / (R cos(centre_lat)))",
        "points": points,
    }
    out_dir = os.path.join(FIXTURES, "frame")
    os.makedirs(out_dir, exist_ok=True)
    path = os.path.join(out_dir, "bherwerre_corners.json")
    with open(path, "w", encoding="utf-8") as f:
        json.dump(doc, f, indent=2)
        f.write("\n")
    return path


def write_tiny_raster():
    side = raster_io.expected_side(40.0, 10.0)
    heights = np.zeros((side, side), dtype=np.float32)
    for row in range(side):
        for col in range(side):
            heights[row, col] = tiny_height(row, col)
    out_dir = os.path.join(FIXTURES, "raster")
    raster_io.write_raster(
        out_dir, "tiny", "fixture", heights, 10.0, 40.0, terrarium.BHERWERRE_CENTRE_LAT, terrarium.BHERWERRE_CENTRE_LON,
        {"dataset": "synthetic fixture", "law": "100 + row + 10*col, +5 at the centre cell"},
        "Tools/data/write_fixtures.py", "none: synthetic")
    return os.path.join(out_dir, "tiny.r32")


def write_tiny_layers():
    """Two version-2 layers over the same 5 x 5 grid: soil depth as u16 centimetres (law: 3 cm per row plus 7 per column,
    so the north-west corner is bare and the south-east corner is 40 cm) and a u32 flag mask (law: bit row set, bit
    (8 + col) set), so the loader's integer path and its scale are pinned to numbers the tests can name."""
    side = raster_io.expected_side(40.0, 10.0)
    depth_m = np.zeros((side, side), dtype=np.float64)
    flags = np.zeros((side, side), dtype=np.uint32)
    for row in range(side):
        for col in range(side):
            depth_m[row, col] = (3 * row + 7 * col) / 100.0
            flags[row, col] = (1 << row) | (1 << (8 + col))
    out_dir = os.path.join(FIXTURES, "raster")
    raster_io.write_raster(out_dir, "tiny_soil", "fixture", depth_m, 10.0, 40.0, terrarium.BHERWERRE_CENTRE_LAT, terrarium.BHERWERRE_CENTRE_LON,
                           {"dataset": "synthetic fixture", "law": "(3*row + 7*col) cm"}, "Tools/data/write_fixtures.py", "none: synthetic",
                           layer="soil_depth", dtype="u16", scale=0.01, unit="m")
    raster_io.write_raster(out_dir, "tiny_flags", "fixture", flags, 10.0, 40.0, terrarium.BHERWERRE_CENTRE_LAT, terrarium.BHERWERRE_CENTRE_LON,
                           {"dataset": "synthetic fixture", "law": "bit row | bit (8 + col)"}, "Tools/data/write_fixtures.py", "none: synthetic",
                           layer="topology", dtype="u32", scale=1.0, unit="flags")
    return os.path.join(out_dir, "tiny_soil.u16") + " and tiny_flags.u32"


def main():
    print("wrote " + write_frame_fixture())
    print("wrote " + write_tiny_raster())
    print("wrote " + write_tiny_layers())
    return 0


if __name__ == "__main__":
    sys.exit(main())
