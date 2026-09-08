#!/usr/bin/env python3
"""region_stats.py: is the baked heights raster the shape of the place it claims to be?

Reads Data/regions/bherwerre/heights.r32 by hand (numpy, from the sidecar's stated width and height, never from
the engine or the bake's code) and holds its statistics against what is published about the Bherwerre
Peninsula, not against the sidecar's own min_m, max_m and sea_fraction, which the bake wrote and cannot vouch for:

  - the raster is whole: the stated shape, no NaN, no row or column that is all zeros (a zero stripe is a
    missing tile);
  - the highest ground is a modest sandstone plateau: Parks Australia says the slopes above Steamers Beach
    "rise 130 metres" (booderee.gov.au, Steamers Head beaches), so the maximum lies between 110 and 250 m;
  - the sea has no floor in this source: AWS Terrain Tiles carry no bathymetry off this coast at zoom 14, so
    every sea cell is 0 m and nothing sits below -60 m once the bake has despiked its bad pixels (a 1,097 m pit
    was found on 2026-09-08 and is why the despike exists; a residual bad cell up to a few tens of metres is
    tolerated, a hundred is not);
  - the box has sea on two sides (Jervis Bay to the north-east, Wreck Bay and the Tasman Sea to the south and
    east) and the peninsula between: between 10 % and 40 % of cells at or below sea level;
  - the founder wakes on low ground behind a beach: the wake cell is land between 3 and 40 m.

Exit 0 when every check passes, 1 when any fails, 2 when the raster is missing.
Run from the repository root:  python Tools/verifiers/checks/region_stats.py
"""
import json
import math
import os
import sys

import numpy as np

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))))
REGION = os.path.join(ROOT, "Data", "regions", "bherwerre")

WAKE_LAT, WAKE_LON = -35.159, 150.6485   # Docs/ARCHITECTURE.md §3
MAX_M_RANGE = (110.0, 250.0)             # Parks Australia: slopes at Steamers Head rise 130 m
MIN_M_FLOOR = -60.0                      # no bathymetry in the source; only despike residue may dip below zero
SEA_FRACTION_RANGE = (0.10, 0.40)        # sea on two sides of a peninsula box
WAKE_M_RANGE = (3.0, 40.0)               # low ground behind Cave Beach


def cell_of(lat, lon, sidecar):
    R = 6371000.0
    north = math.radians(lat - sidecar["centre_lat"]) * R
    east = math.radians(lon - sidecar["centre_lon"]) * R * math.cos(math.radians(sidecar["centre_lat"]))
    half = sidecar["extent_m"] / 2.0
    return int(round((half - north) / sidecar["cell_m"])), int(round((east + half) / sidecar["cell_m"]))


def main():
    sidecar_path = os.path.join(REGION, "heights.json")
    raw_path = os.path.join(REGION, "heights.r32")
    if not (os.path.isfile(sidecar_path) and os.path.isfile(raw_path)):
        print("the region raster is missing (%s); bake it first" % REGION)
        return 2
    sidecar = json.load(open(sidecar_path, encoding="utf-8"))
    raw = np.fromfile(raw_path, dtype="<f4")
    failures = []

    def expect(name, ok, detail):
        print("%-30s %s  %s" % (name, "ok " if ok else "FAIL", detail))
        if not ok:
            failures.append(name)

    w, h = sidecar["width"], sidecar["height"]
    expect("shape", raw.size == w * h, "%d values for a stated %dx%d" % (raw.size, w, h))
    if raw.size != w * h:
        print("region_stats: FAIL (shape)")
        return 1
    heights = raw.reshape(h, w)
    expect("finite", bool(np.isfinite(heights).all()), "%d non-finite cells" % int((~np.isfinite(heights)).sum()))
    zero_rows = int((np.abs(heights).max(axis=1) == 0.0).sum())
    zero_cols = int((np.abs(heights).max(axis=0) == 0.0).sum())
    expect("no zero stripes", zero_rows == 0 and zero_cols == 0, "%d all-zero rows, %d all-zero columns" % (zero_rows, zero_cols))

    hmax, hmin = float(heights.max()), float(heights.min())
    rmax, cmax = np.unravel_index(int(heights.argmax()), heights.shape)
    expect("highest ground", MAX_M_RANGE[0] <= hmax <= MAX_M_RANGE[1],
           "max %.1f m at row %d col %d, expected %.0f..%.0f (slopes rise 130 m at Steamers Head)" % (hmax, rmax, cmax, MAX_M_RANGE[0], MAX_M_RANGE[1]))
    expect("no deep pits", hmin >= MIN_M_FLOOR, "min %.1f m, floor %.0f (the source has no bathymetry here)" % (hmin, MIN_M_FLOOR))
    sea = float((heights <= 0.0).mean())
    expect("sea fraction", SEA_FRACTION_RANGE[0] <= sea <= SEA_FRACTION_RANGE[1],
           "%.1f %% of cells at or below sea level, expected %.0f..%.0f %%" % (sea * 100, SEA_FRACTION_RANGE[0] * 100, SEA_FRACTION_RANGE[1] * 100))
    sea_cells = heights[heights <= 0.0]
    expect("sea is flat in this source", sea_cells.size > 0 and float(np.percentile(-sea_cells, 99.9)) <= 10.0,
           "99.9th percentile of depth %.1f m (a source without bathymetry reads 0)" % (float(np.percentile(-sea_cells, 99.9)) if sea_cells.size else float("nan")))
    r, c = cell_of(WAKE_LAT, WAKE_LON, sidecar)
    wake = float(heights[r, c])
    expect("the wake is low land", WAKE_M_RANGE[0] <= wake <= WAKE_M_RANGE[1], "row %d col %d -> %.1f m, expected %.0f..%.0f" % (r, c, wake, WAKE_M_RANGE[0], WAKE_M_RANGE[1]))

    if failures:
        print("region_stats: FAIL (%s)" % ", ".join(failures))
        return 1
    print("region_stats: ok, the raster is whole and the shape of the peninsula the sources describe")
    return 0


if __name__ == "__main__":
    sys.exit(main())
