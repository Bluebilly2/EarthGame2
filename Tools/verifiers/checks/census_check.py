#!/usr/bin/env python3
"""census_check.py: is the water where the map says, the beach a beach, the bay salt, the south coast hard?

Reads the world's layers with numpy alone (water.u8, surface.r32, topology.u32, heights.r32, and world.json for
the chosen wake), converts published coordinates to cells by the frame rule the sidecars state (cell centres at
east = col * cell - extent / 2 and north = extent / 2 - row * cell, the small-angle map about the centre), and
checks each named place against a source that is not the bake's input:
  - areas from Wikipedia, "Jervis Bay Territory": Lake Windermere 31 ha, Lake McKenzie 7 ha, Blacks Waterhole
    1.4 ha;
  - positions from Wikidata: Lake McKenzie Q21908519 at 35.14666 S 150.67079 E to a metre, with an elevation of
    35 m; Lake Windermere Q23759865 at 35 08 S 150 40 E and Ryans Swamp Q21901802 at 35 10 S 150 40 E, both to
    the minute, so those rows search 1500 m; Steamers Head Q106289416 at 35.17978 S 150.7369 E;
  - Cave Beach from Destination NSW (visitnsw.com, the Cave Beach page): 35.162201 S 150.671814 E;
  - the bay point summit_check uses: 500 m north of Green Patch Beach's latitude (35.136686 S, beachesontheair.com)
    at the box's east edge, 150.7180 E;
  - the south coast's character from Parks Australia (booderee.gov.au: Cave Beach a surf beach backed by dunes;
    Steamers Beach flanked by high cliffs) and Caravan World's Booderee guide (Summercloud Bay: "scalloped coves
    bracketed by rock formations", Bherwerre Beach arcing 7 km along the southern shore west of Cave Beach).
The bake's water outlines are OpenStreetMap's (Tools/data/bake_water.py); none of the numbers above come from
OpenStreetMap except Blacks Waterhole's position, which no other source states, and that row says so.

Rows, each printed with both numbers:
  1. Lake Windermere: the lake cells within 1500 m of the point, within a factor of two of 31 ha (the largest
     patch among them and its distance are printed). Its level is printed beside Wikidata's 75 m without a
     verdict: the bake's ground around the outline lies at 12 to 45 m and this file does not believe the 75 m.
  2. Lake McKenzie: the lake cells within 500 m of the point, within a factor of two of 7 ha; their level within
     5 m of 35 m. A lake's water can lie in more than one patch where the bake's ground inside its outline has
     two hollows, so cells are summed rather than one patch picked.
  3. Blacks Waterhole: the lake cells within 500 m of OpenStreetMap's centre (35.1741306 S 150.7004685 E),
     within a factor of two of 1.4 ha.
  4. Ryans Swamp: at least a hectare of swamp within 1500 m of the point. Swamp is a fresh class by the layer's
     own definition (the sea is the only salt class), so the row prints how far the nearest swamp patch lies from
     the point and from the sea rather than testing for sea near a point rounded to the minute, which on this
     coast lands 400 m offshore.
  5. Jervis Bay: the bay point is sea, its surface at the datum.
  6. Cave Beach: at least a hectare of beach within 300 m of the point, and at least a hectare of dune within
     600 m of it (a beach's swale lies behind its dune).
  7. The hard south coast east of Cave Beach: the shore from 500 m east of the box's centre line to the edge,
     south of -2500 m, carries at least a hectare of platform or cliff. Steamers Head lies outside the box (the
     distance is printed), and so does the fact, found on 2026-09-09, that the box's south-east corner is land:
     the shore leaves the box through its south edge east of Summercloud Bay, so the corner holds no sea.
     Bherwerre Beach's corner (west of -1500 m, south of -2500 m) is beach and carries less platform than beach.
  8. The chosen wake (world.json): its distance from Cave Beach, printed as a note. The scorer may land anywhere
     in the region (CANON ruling 20, which on 2026-09-10 withdrew the point this check once held the region to:
     35.159 S 150.6485 E, recorded as "the Cave Beach swale" though it lies behind Bherwerre Beach).

Every landmark above is Bherwerre's. A world set in another region (world.json's "region", since WG.2, 2026-09-22) has
no census here yet: the check says so and exits 2 rather than judging the valley by the peninsula's lakes; the
Kangaroo Valley's census against its own published points is owed (DEBTS.md, "The valley has no census").

Exit 0 when every row passes, 1 when any fails, 2 when a layer is missing or the region has no landmarks here.
Run from the repository root:  python Tools/verifiers/checks/census_check.py [world folder]
(default Artefacts/worlds/gate, the folder Tools/world/create.py leaves behind).
"""
import json
import math
import os
import sys

import numpy as np

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))))
DEFAULT_WORLD = os.path.join("Artefacts", "worlds", "gate")
EARTH_RADIUS_M = 6371000.0
NP_DTYPES = {"u8": "u1", "u16": "<u2", "i16": "<i2", "u32": "<u4", "f32": "<f4"}

WINDERMERE = (-35.13333333, 150.66666667, 1500.0, 31.0, 75.0)
MCKENZIE = (-35.14666, 150.67079, 500.0, 7.0, 35.0)
BLACKS = (-35.1741306, 150.7004685, 500.0, 1.4)
RYANS = (-35.16666667, 150.66666667, 1500.0)
CAVE_BEACH = (-35.162201, 150.671814)
STEAMERS_HEAD = (-35.17978, 150.7369)
BAY_POINT = (-35.136686 + 500.0 / 111195.0, 150.7180)

SEA, LAKE, SWAMP = 7, 5, 6
BEACH, DUNE, CLIFF, PLATFORM = 2, 4, 128, 256


def load(sidecar_path):
    if not os.path.isfile(sidecar_path):
        return None, None
    sidecar = json.load(open(sidecar_path, encoding="utf-8"))
    raw = os.path.join(os.path.dirname(sidecar_path), sidecar.get("raw", sidecar["name"] + ".r32"))
    if not os.path.isfile(raw):
        return None, None
    return sidecar, np.fromfile(raw, dtype=NP_DTYPES[sidecar.get("dtype", "f32")]).reshape(sidecar["height"], sidecar["width"])


class Frame:
    def __init__(self, sidecar):
        self.lat0 = sidecar["centre_lat"]
        self.lon0 = sidecar["centre_lon"]
        self.half = sidecar["extent_m"] / 2.0
        self.cell = sidecar["cell_m"]
        self.rows = sidecar["height"]
        self.cols = sidecar["width"]

    def local(self, lat, lon):
        north = math.radians(lat - self.lat0) * EARTH_RADIUS_M
        east = math.radians(lon - self.lon0) * EARTH_RADIUS_M * math.cos(math.radians(self.lat0))
        return east, north

    def cell_of(self, east, north):
        return int(round((self.half - north) / self.cell)), int(round((east + self.half) / self.cell))

    def disc(self, east, north, radius_m):
        """A boolean grid of the cells within a radius of a local point."""
        rr, cc = np.ogrid[0:self.rows, 0:self.cols]
        de = (cc * self.cell - self.half) - east
        dn = (self.half - rr * self.cell) - north
        return de * de + dn * dn <= radius_m * radius_m

    def grids(self):
        rr, cc = np.ogrid[0:self.rows, 0:self.cols]
        return cc * self.cell - self.half, self.half - rr * self.cell

    def hectares(self, cells):
        return cells * self.cell * self.cell / 10000.0


def patches(mask):
    """8-connected components of a boolean grid, as (rows, cols) index arrays, largest first."""
    h, w = mask.shape
    flat = mask.ravel()
    lab = np.zeros(h * w, dtype=np.int32)
    result = []
    for start in np.flatnonzero(flat):
        start = int(start)
        if lab[start]:
            continue
        n = len(result) + 1
        stack = [start]
        lab[start] = n
        cells = []
        while stack:
            i = stack.pop()
            cells.append(i)
            r, c = divmod(i, w)
            for dr in (-1, 0, 1):
                for dc in (-1, 0, 1):
                    rr, cc = r + dr, c + dc
                    if 0 <= rr < h and 0 <= cc < w:
                        j = rr * w + cc
                        if flat[j] and not lab[j]:
                            lab[j] = n
                            stack.append(j)
        result.append(np.divmod(np.array(cells), w))
    result.sort(key=lambda rc: -len(rc[0]))
    return result


def lake_within(frame, water, lake_patches, east, north, radius_m):
    """The lake cells within a radius of a local point: (their count, the largest patch's cells, its distance, the mask), or None."""
    near = (water == LAKE) & frame.disc(east, north, radius_m)
    cells = int(near.sum())
    if cells == 0:
        return None
    largest, largest_d = 0, None
    for rows, cols in lake_patches:
        ce, cn = cols.mean() * frame.cell - frame.half, frame.half - rows.mean() * frame.cell
        d = math.hypot(ce - east, cn - north)
        if d <= radius_m and len(rows) > largest:
            largest, largest_d = len(rows), d
    return cells, largest, largest_d, near


def main(argv):
    world = argv[1] if len(argv) > 1 else DEFAULT_WORLD
    layers = os.path.join(ROOT, world, "layers")
    sidecar, water = load(os.path.join(layers, "water.json"))
    _, surface = load(os.path.join(layers, "surface.json"))
    _, topology = load(os.path.join(layers, "topology.json"))
    _, heights = load(os.path.join(layers, "heights.json"))
    world_json = os.path.join(ROOT, world, "world.json")
    if water is None or surface is None or topology is None or heights is None or not os.path.isfile(world_json):
        print("a layer is missing under %s (water, surface, topology, heights and world.json are needed)" % layers)
        return 2
    region = json.load(open(world_json, encoding="utf-8")).get("region", "bherwerre")
    if region != "bherwerre":
        print("census_check: this file holds Bherwerre's landmarks (Windermere, McKenzie, Blacks Waterhole, Ryans Swamp, Jervis Bay, "
              "Cave Beach, Steamers Head); the world is set in %s, whose census against published points is owed (DEBTS.md: "
              "\"The valley has no census\"); no verdict" % region)
        return 2
    frame = Frame(sidecar)
    failures = []

    def expect(name, ok, detail):
        print("%-46s %s  %s" % (name, "ok " if ok else "FAIL", detail))
        if not ok:
            failures.append(name)

    def note(name, detail):
        print("%-46s note %s" % (name, detail))

    def factor_of_two(a, b):
        return a > 0 and b > 0 and 0.5 <= a / b <= 2.0

    lake_patches = patches(water == LAKE)

    # 1. Lake Windermere.
    e, n = frame.local(WINDERMERE[0], WINDERMERE[1])
    found = lake_within(frame, water, lake_patches, e, n, WINDERMERE[2])
    if found is None:
        expect("Lake Windermere is where Wikidata puts it", False, "no lake cell within %.0f m of the point (to the minute)" % WINDERMERE[2])
    else:
        cells, largest, d, near = found
        ha = frame.hectares(cells)
        level = float(np.median(surface[near]))
        expect("Lake Windermere is where Wikidata puts it, its size", factor_of_two(ha, WINDERMERE[3]),
               "%.1f ha of lake within %.0f m of the point, the largest patch %.1f ha at %.0f m; Wikipedia 31 ha" % (ha, WINDERMERE[2], frame.hectares(largest), d))
        note("Lake Windermere's level", "%.1f m in the world; Wikidata says %.0f m, the bake's ground around the outline 12 to 45 m; no verdict" % (level, WINDERMERE[4]))

    # 2. Lake McKenzie.
    e, n = frame.local(MCKENZIE[0], MCKENZIE[1])
    found = lake_within(frame, water, lake_patches, e, n, MCKENZIE[2])
    if found is None:
        expect("Lake McKenzie is where Wikidata puts it", False, "no lake cell within %.0f m of the point (to a metre)" % MCKENZIE[2])
    else:
        cells, largest, d, near = found
        ha = frame.hectares(cells)
        level = float(np.median(surface[near]))
        expect("Lake McKenzie is where Wikidata puts it, its size", factor_of_two(ha, MCKENZIE[3]),
               "%.1f ha of lake within %.0f m of the point, the largest patch %.1f ha at %.0f m; Wikipedia 7 ha" % (ha, MCKENZIE[2], frame.hectares(largest), d))
        expect("Lake McKenzie stands at Wikidata's level", abs(level - MCKENZIE[4]) <= 5.0, "%.1f m in the world; Wikidata %.0f m" % (level, MCKENZIE[4]))

    # 3. Blacks Waterhole.
    e, n = frame.local(BLACKS[0], BLACKS[1])
    found = lake_within(frame, water, lake_patches, e, n, BLACKS[2])
    if found is None:
        expect("Blacks Waterhole is where the map puts it", False, "no lake cell within %.0f m of OpenStreetMap's centre (the bake's own source)" % BLACKS[2])
    else:
        cells, largest, d, near = found
        ha = frame.hectares(cells)
        expect("Blacks Waterhole is where the map puts it, its size", factor_of_two(ha, BLACKS[3]),
               "%.1f ha of lake within %.0f m of OpenStreetMap's centre (the bake's own source; the size is Wikipedia's 1.4 ha), the largest patch at %.0f m" % (ha, BLACKS[2], d))

    # 4. Ryans Swamp.
    e, n = frame.local(RYANS[0], RYANS[1])
    near = frame.disc(e, n, RYANS[2])
    swamp_ha = frame.hectares(int(((water == SWAMP) & near).sum()))
    nearest, nearest_sea = None, None
    for rows, cols in patches((water == SWAMP) & near):
        ce, cn = cols.mean() * frame.cell - frame.half, frame.half - rows.mean() * frame.cell
        d = math.hypot(ce - e, cn - n)
        if nearest is None or d < nearest:
            nearest = d
            sea_rows, sea_cols = np.nonzero(water == SEA)
            nearest_sea = float(np.sqrt(((sea_rows - rows.mean()) * frame.cell) ** 2 + ((sea_cols - cols.mean()) * frame.cell) ** 2).min()) if sea_rows.size else float("inf")
    expect("Ryans Swamp is swamp, and swamp is fresh", swamp_ha >= 1.0,
           "%.1f ha of swamp within %.0f m of the point (to the minute); the nearest patch %s m from the point and %s m from the sea" % (
               swamp_ha, RYANS[2], "%.0f" % nearest if nearest is not None else "none", "%.0f" % nearest_sea if nearest_sea is not None else "none"))

    # 5. The bay.
    e, n = frame.local(BAY_POINT[0], BAY_POINT[1])
    r, c = frame.cell_of(e, n)
    expect("Jervis Bay is salt water at the datum", int(water[r, c]) == SEA and abs(float(surface[r, c])) < 1e-3,
           "row %d col %d: water class %d (7 is sea), surface %.2f m, floor %.1f m" % (r, c, int(water[r, c]), float(surface[r, c]), float(heights[r, c])))

    # 6. Cave Beach.
    ce, cn = frame.local(CAVE_BEACH[0], CAVE_BEACH[1])
    beach_ha = frame.hectares(int(((topology & BEACH) > 0)[frame.disc(ce, cn, 300.0)].sum()))
    dune_ha = frame.hectares(int(((topology & DUNE) > 0)[frame.disc(ce, cn, 600.0)].sum()))
    expect("Cave Beach is a beach with dune behind it", beach_ha >= 1.0 and dune_ha >= 1.0,
           "beach %.1f ha within 300 m of Destination NSW's point, dune %.1f ha within 600 m" % (beach_ha, dune_ha))

    # 7. The hard south coast east of Cave Beach, and Bherwerre Beach's soft corner.
    se, sn = frame.local(STEAMERS_HEAD[0], STEAMERS_HEAD[1])
    outside = max(0.0, abs(se) - frame.half, abs(sn) - frame.half)
    east_grid, north_grid = frame.grids()
    hard = ((topology & PLATFORM) > 0) | ((topology & CLIFF) > 0)
    south_east = (east_grid >= 500.0) & (north_grid <= -2500.0)
    hard_ha = frame.hectares(int(hard[south_east].sum()))
    corner_sea = frame.hectares(int((water == SEA)[(east_grid >= 1500.0) & (north_grid <= -2500.0)].sum()))
    expect("the south coast east of Cave Beach is hard", hard_ha >= 1.0,
           "platform or cliff %.1f ha east of 500 m and south of -2500 m; Steamers Head lies %.0f m outside the box, and the box's south-east corner holds %.1f ha of sea" % (hard_ha, outside, corner_sea))
    soft = (east_grid <= -1500.0) & (north_grid <= -2500.0)
    soft_beach = frame.hectares(int(((topology & BEACH) > 0)[soft].sum()))
    soft_hard = frame.hectares(int(hard[soft].sum()))
    expect("Bherwerre Beach's corner is soft", soft_beach >= 1.0 and soft_beach > soft_hard,
           "beach %.1f ha, platform or cliff %.1f ha in the corner west of -1500 m and south of -2500 m" % (soft_beach, soft_hard))

    # 8. The wake the world chose.
    saved = json.load(open(world_json, encoding="utf-8"))
    if "wake_east" in saved and "wake_north" in saved:
        chosen_d = math.hypot(saved["wake_east"] - ce, saved["wake_north"] - cn)
        note("the chosen wake", "east %.0f north %.0f, %.0f m from Cave Beach; the census says why" % (saved["wake_east"], saved["wake_north"], chosen_d))
    else:
        expect("world.json names the wake", False, "no wake_east/wake_north in %s" % world_json)

    if failures:
        print("census_check: FAIL (%s)" % ", ".join(failures))
        return 1
    print("census_check: ok, the water, the beach, the bay and the hard shore are where the sources put them")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
