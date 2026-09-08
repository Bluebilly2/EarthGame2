#!/usr/bin/env python3
"""summit_check.py: are the named places where the map says, at the heights the map says, the right way round?

Reads the two baked rasters by hand (numpy, from their sidecars' stated shapes; never the engine, never the
bake's code) and converts published coordinates to cells with the frame rule the sidecar states: cell centres at
east = col * cell - extent / 2 and north = extent / 2 - row * cell, the small-angle map about the centre.

The references, and where they come from:
  - Bherwerre Trig Point, the highest point of the Jervis Bay Territory: 35.16167 S, 150.73028 E, 170 m
    (Wikidata Q15107571; peakbagger.com gives 169 m). It lies about a kilometre EAST of the 8 km box, so the
    64 km surround raster holds the summit and the 4 m region raster holds the hill's western flank.
  - Steamers Head: "steep vegetated slopes rise 130 metres" above Steamers Beach (Parks Australia,
    booderee.gov.au, Steamers Head beaches); Steamers Beach at 35.171945 S, 150.724771 E (beachesontheair.com).
  - Green Patch Beach, on Jervis Bay's shore: 35.136686 S, 150.723938 E (beachesontheair.com); the bay lies
    north of it, so 500 m north of that latitude at the box's eastern edge is water. Wreck Bay and the Tasman
    Sea lie south of the wake. Water is 0 m in this source.

Checks, each printed with both numbers:
  1. the surround raster at the trig point reads a summit: 140..185 m (a 64 m cell averages a 170 m top);
  2. the region raster's highest cell within 2 km of the trig point is the hill's flank: 120..175 m;
  3. the region's highest cell overall lies within 2.5 km of the trig point (the peninsula's high ground is
     there and nowhere else);
  4. mirrors redden: the same 2 km search around the trig point's east-west mirror image (the box's west edge)
     and its north-south mirror image (the north-east corner) finds nothing above 60 m;
  5. the bay and the ocean are water: the cell 500 m north of Green Patch Beach's latitude at the box's eastern
     edge and the cell a kilometre off Cave Beach at 35.172 S 150.650 E are at or below 0 m, and the ground
     300 m north of the wake is land. (A first draft put "the bay" in the north-west corner from memory and the
     raster read land there; the raster was right and the memory wrong, which is what a published point is for.)

Exit 0 when every check passes, 1 when any fails, 2 when a raster is missing.
Run from the repository root:  python Tools/verifiers/checks/summit_check.py
"""
import json
import math
import os
import sys

import numpy as np

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))))
REGION = os.path.join(ROOT, "Data", "regions", "bherwerre")

TRIG_LAT, TRIG_LON, TRIG_M = -35.16166667, 150.73027778, 170.0
GREEN_PATCH_LAT, GREEN_PATCH_LON = -35.136686, 150.723938
BAY_LAT, BAY_LON = GREEN_PATCH_LAT + 500.0 / 111195.0, 150.7180   # 500 m north of the beach's latitude, inside the box
OCEAN_LAT, OCEAN_LON = -35.172, 150.650
WAKE_LAT, WAKE_LON = -35.159, 150.6485
EARTH_RADIUS_M = 6371000.0


def load(name):
    sidecar_path = os.path.join(REGION, name + ".json")
    raw_path = os.path.join(REGION, name + ".r32")
    if not (os.path.isfile(sidecar_path) and os.path.isfile(raw_path)):
        return None, None
    sidecar = json.load(open(sidecar_path, encoding="utf-8"))
    heights = np.fromfile(raw_path, dtype="<f4").reshape(sidecar["height"], sidecar["width"])
    return sidecar, heights


def local(lat, lon, sidecar):
    """Published latitude and longitude to local metres by the sidecar's stated small-angle rule."""
    north = math.radians(lat - sidecar["centre_lat"]) * EARTH_RADIUS_M
    east = math.radians(lon - sidecar["centre_lon"]) * EARTH_RADIUS_M * math.cos(math.radians(sidecar["centre_lat"]))
    return east, north


def cell(east, north, sidecar):
    half = sidecar["extent_m"] / 2.0
    return int(round((half - north) / sidecar["cell_m"])), int(round((east + half) / sidecar["cell_m"]))


def inside(east, north, sidecar):
    half = sidecar["extent_m"] / 2.0
    return -half <= east <= half and -half <= north <= half


def highest_within(heights, sidecar, east, north, radius_m):
    """The highest cell within a radius of a local point, searching only the part of the disc inside the raster."""
    half = sidecar["extent_m"] / 2.0
    c = sidecar["cell_m"]
    r0, c0 = cell(east, north, sidecar)
    n = int(math.ceil(radius_m / c))
    rows = slice(max(0, r0 - n), min(heights.shape[0], r0 + n + 1))
    cols = slice(max(0, c0 - n), min(heights.shape[1], c0 + n + 1))
    block = heights[rows, cols]
    if block.size == 0:
        return None, None
    rr, cc = np.mgrid[rows, cols]
    de = (cc * c - half) - east
    dn = (half - rr * c) - north
    mask = de * de + dn * dn <= radius_m * radius_m
    if not mask.any():
        return None, None
    masked = np.where(mask, block, -np.inf)
    i = int(masked.argmax())
    return float(masked.flat[i]), (int(rr.flat[i]), int(cc.flat[i]))


def main():
    fine_sidecar, fine = load("heights")
    coarse_sidecar, coarse = load("surround")
    if fine is None or coarse is None:
        print("a raster is missing under %s; bake heights and surround first" % REGION)
        return 2
    failures = []

    def expect(name, ok, detail):
        print("%-36s %s  %s" % (name, "ok " if ok else "FAIL", detail))
        if not ok:
            failures.append(name)

    # 1. The summit in the surround.
    te, tn = local(TRIG_LAT, TRIG_LON, coarse_sidecar)
    tr, tc = cell(te, tn, coarse_sidecar)
    at_trig = float(coarse[tr, tc])
    expect("surround reads the trig point", 140.0 <= at_trig <= 185.0,
           "%.1f m at row %d col %d (published %.0f m; a 64 m cell averages the top)" % (at_trig, tr, tc, TRIG_M))

    # 2. The flank in the region raster, within 2 km of the trig point (the trig itself is outside the box).
    fe, fn = local(TRIG_LAT, TRIG_LON, fine_sidecar)
    expect("the trig point is outside the box", not inside(fe, fn, fine_sidecar),
           "trig at local east %.0f north %.0f, box half-extent %.0f" % (fe, fn, fine_sidecar["extent_m"] / 2))
    flank, where = highest_within(fine, fine_sidecar, fe, fn, 2000.0)
    expect("region reads the hill's flank", flank is not None and 120.0 <= flank <= 175.0,
           "highest cell within 2 km of the trig: %s m at %s" % ("%.1f" % flank if flank is not None else "none", where))

    # 3. The region's high ground is that hill and nowhere else.
    rmax, cmax = np.unravel_index(int(fine.argmax()), fine.shape)
    half = fine_sidecar["extent_m"] / 2.0
    me = cmax * fine_sidecar["cell_m"] - half
    mn = half - rmax * fine_sidecar["cell_m"]
    dist = math.hypot(me - fe, mn - fn)
    expect("the region's maximum is that hill", dist <= 2500.0,
           "max %.1f m at east %.0f north %.0f, %.0f m from the trig point" % (float(fine[rmax, cmax]), me, mn, dist))

    # 4. Mirrors: the same search around the mirror images must find only low ground.
    ew, where_ew = highest_within(fine, fine_sidecar, -fe, fn, 2000.0)
    expect("no hill at the east-west mirror", ew is not None and ew < 60.0,
           "highest within 2 km of east %.0f north %.0f: %s m at %s" % (-fe, fn, "%.1f" % ew if ew is not None else "none", where_ew))
    ns, where_ns = highest_within(fine, fine_sidecar, fe, -fn, 2000.0)
    expect("no hill at the north-south mirror", ns is not None and ns < 60.0,
           "highest within 2 km of east %.0f north %.0f: %s m at %s" % (fe, -fn, "%.1f" % ns if ns is not None else "none", where_ns))

    # 5. Water where the map has water, land where the founder wakes.
    for name, lat, lon in (("Jervis Bay", BAY_LAT, BAY_LON), ("the ocean off Cave Beach", OCEAN_LAT, OCEAN_LON)):
        e, n = local(lat, lon, fine_sidecar)
        r, c = cell(e, n, fine_sidecar)
        v = float(fine[r, c])
        expect("%s is water" % name, v <= 0.0, "row %d col %d -> %.1f m" % (r, c, v))
    we, wn = local(WAKE_LAT, WAKE_LON, fine_sidecar)
    r, c = cell(we, wn + 300.0, fine_sidecar)
    v = float(fine[r, c])
    expect("300 m north of the wake is land", v > 0.0, "row %d col %d -> %.1f m" % (r, c, v))

    if failures:
        print("summit_check: FAIL (%s)" % ", ".join(failures))
        return 1
    print("summit_check: ok, the hill, the bay and the ocean are where the map puts them, the right way round")
    return 0


if __name__ == "__main__":
    sys.exit(main())
