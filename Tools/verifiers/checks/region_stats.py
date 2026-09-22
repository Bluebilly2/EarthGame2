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
  - the ground behind Bherwerre Beach is low land: the cell at 35.159 S 150.6485 E (the site research's first
    wake, withdrawn as a wake by CANON ruling 20 and kept as a probe) lies between 3 and 40 m.

With --world <folder> (M1.2) it checks a created world's heights layer instead, against the bake of the region the
world's own world.json names: the sea has the floor the rule
states (WorldLayers: one in twenty from the shore, to 30 m; "the sea has no floor" in DEBTS.md), so every sea
cell lies between the datum and -30 m, the deepest cell is 30 m down (an inland box, one whose bake has no cell at or
below the datum, has no sea to floor: those rows print a note and the land row alone judges, since WG.2, 2026-09-22),
the depth at sampled sea cells matches
one twentieth of this file's own Euclidean distance to the nearest land cell (within a tenth plus 0.3 m: the
engine steps along the grid's eight directions, which overstates a straight line by up to 8 %), and the land is
the bake's, cell for cell.

Exit 0 when every check passes, 1 when any fails, 2 when the raster is missing.
Run from the repository root:  python Tools/verifiers/checks/region_stats.py [--world Artefacts/worlds/gate]
"""
import json
import math
import os
import sys

import numpy as np

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))))
REGION = os.path.join(ROOT, "Data", "regions", "bherwerre")

PROBE_LAT, PROBE_LON = -35.159, 150.6485  # behind Bherwerre Beach: the site research's first wake, now a probe
MAX_M_RANGE = (110.0, 250.0)             # Parks Australia: slopes at Steamers Head rise 130 m
MIN_M_FLOOR = -60.0                      # no bathymetry in the source; only despike residue may dip below zero
SEA_FRACTION_RANGE = (0.10, 0.40)        # sea on two sides of a peninsula box
PROBE_M_RANGE = (3.0, 40.0)              # low ground behind the beach


def region_dir(world):
    """The bake the world was made from: Data/regions/<region>, the region read off the world's own world.json (a world
    owns which piece of the Earth it is; Bherwerre when the file does not say, as the worlds before WG.2 were)."""
    world_json = os.path.join(ROOT, world, "world.json")
    region = "bherwerre"
    if os.path.isfile(world_json):
        region = json.load(open(world_json, encoding="utf-8")).get("region", region)
    return os.path.join(ROOT, "Data", "regions", region)


def cell_of(lat, lon, sidecar):
    R = 6371000.0
    north = math.radians(lat - sidecar["centre_lat"]) * R
    east = math.radians(lon - sidecar["centre_lon"]) * R * math.cos(math.radians(sidecar["centre_lat"]))
    half = sidecar["extent_m"] / 2.0
    return int(round((half - north) / sidecar["cell_m"])), int(round((east + half) / sidecar["cell_m"]))


SEA_FLOOR_GRADIENT = 0.05
SEA_FLOOR_MAX_M = 30.0
FLOOR_SAMPLES = 400


def check_world(world):
    """The created world's sea floor against the stated rule, by this file's own distances."""
    layers = os.path.join(ROOT, world, "layers")
    region = region_dir(world)
    world_sidecar = os.path.join(layers, "heights.json")
    bake_sidecar = os.path.join(region, "heights.json")
    if not (os.path.isfile(world_sidecar) and os.path.isfile(bake_sidecar)):
        print("the world's heights (%s) or the bake (%s) is missing" % (world_sidecar, bake_sidecar))
        return 2
    ws = json.load(open(world_sidecar, encoding="utf-8"))
    bs = json.load(open(bake_sidecar, encoding="utf-8"))
    world_h = np.fromfile(os.path.join(layers, ws.get("raw", "heights.r32")), dtype="<f4").reshape(ws["height"], ws["width"])
    bake_h = np.fromfile(os.path.join(region, "heights.r32"), dtype="<f4").reshape(bs["height"], bs["width"])
    failures = []

    def expect(name, ok, detail):
        print("%-30s %s  %s" % (name, "ok " if ok else "FAIL", detail))
        if not ok:
            failures.append(name)

    expect("same shape as the bake", world_h.shape == bake_h.shape, "%s against %s" % (world_h.shape, bake_h.shape))
    if world_h.shape != bake_h.shape:
        print("region_stats: FAIL (shape)")
        return 1
    sea = bake_h <= 0.0
    land = ~sea
    expect("the land is the bake's", bool(np.array_equal(world_h[land], bake_h[land])),
           "greatest difference on land %.3f m" % float(np.abs(world_h[land] - bake_h[land]).max()))
    floor = world_h[sea]
    if floor.size == 0:
        print("%-30s note  an inland box: no cell of the bake lies at or below the datum, so there is no sea to floor" % "the sea floor")
        if failures:
            print("region_stats: FAIL (%s)" % ", ".join(failures))
            return 1
        print("region_stats: ok, the world's land is the bake's (no sea in this box)")
        return 0
    expect("the sea lies under the datum", floor.size > 0 and float(floor.max()) < 0.0, "shallowest sea cell %.2f m, %d sea cells" % (float(floor.max()) if floor.size else float("nan"), int(floor.size)))
    expect("the floor stops at 30 m", float(floor.min()) >= -SEA_FLOOR_MAX_M - 1e-3 and float(floor.min()) <= -SEA_FLOOR_MAX_M + 0.5,
           "deepest sea cell %.2f m, the rule's cap %.0f m" % (float(floor.min()), SEA_FLOOR_MAX_M))
    # The rule at sampled cells, with this file's own distance to land.
    cell = float(bs["cell_m"])
    rng = np.random.default_rng(1347)
    sea_rows, sea_cols = np.nonzero(sea)
    pick = rng.choice(sea_rows.size, size=min(FLOOR_SAMPLES, sea_rows.size), replace=False)
    reach = int(math.ceil(SEA_FLOOR_MAX_M / SEA_FLOOR_GRADIENT / cell)) + 2
    worst, off = 0.0, 0
    for k in pick:
        r, c = int(sea_rows[k]), int(sea_cols[k])
        r0, r1 = max(0, r - reach), min(sea.shape[0], r + reach + 1)
        c0, c1 = max(0, c - reach), min(sea.shape[1], c + reach + 1)
        block = land[r0:r1, c0:c1]
        if not block.any():
            distance = float("inf")
        else:
            rr, cc = np.nonzero(block)
            distance = float(np.sqrt(((rr + r0 - r) * cell) ** 2 + ((cc + c0 - c) * cell) ** 2).min())
        expected = min(SEA_FLOOR_MAX_M, SEA_FLOOR_GRADIENT * distance)
        actual = -float(world_h[r, c])
        error = abs(actual - expected)
        if error > 0.1 * expected + 0.3:
            off += 1
        worst = max(worst, error)
    expect("the floor follows the shore", off == 0,
           "%d of %d sampled sea cells off the one-in-twenty rule by more than a tenth plus 0.3 m; worst %.2f m" % (off, len(pick), worst))
    if failures:
        print("region_stats: FAIL (%s)" % ", ".join(failures))
        return 1
    print("region_stats: ok, the world's sea has the floor the rule states and the land is the bake's")
    return 0


def main():
    if "--world" in sys.argv:
        return check_world(sys.argv[sys.argv.index("--world") + 1])
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
    r, c = cell_of(PROBE_LAT, PROBE_LON, sidecar)
    probe = float(heights[r, c])
    expect("the ground behind Bherwerre Beach is low land", PROBE_M_RANGE[0] <= probe <= PROBE_M_RANGE[1], "row %d col %d -> %.1f m, expected %.0f..%.0f" % (r, c, probe, PROBE_M_RANGE[0], PROBE_M_RANGE[1]))

    if failures:
        print("region_stats: FAIL (%s)" % ", ".join(failures))
        return 1
    print("region_stats: ok, the raster is whole and the shape of the peninsula the sources describe")
    return 0


if __name__ == "__main__":
    sys.exit(main())
