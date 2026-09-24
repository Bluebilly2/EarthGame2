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

Every landmark above is Bherwerre's. The Kangaroo Valley's (since 2026-09-24) serve both of its regions, the 8 km box about
Fitzroy Falls and the whole valley; a landmark whose search reaches past the box's edge is printed as a note. Its sources,
none the bake's input (AWS Terrain Tiles' heights, OpenStreetMap's water outlines):
  - Fitzroy Falls on Yarrunga Creek: the NSW Government's map point (nsw.gov.au, "Fitzroy Falls", 34.648011 S
    150.482544 E); 81 m (Wikipedia) and "nearly 100m" (NSW National Parks, the Fitzroy Falls lookout's page);
  - Belmore Falls on Barrengarry Creek: Wikipedia's point to the second (34.64028 S 150.55972 E); 77 to 130 m in three
    drops (Wikipedia, from the Bonzle Digital Atlas), the top at 552 m AHD (Wikipedia) or 572 m (Wikidata Q38406);
  - Carrington Falls on the Kangaroo River: the NSW Government's map point (nsw.gov.au, "Carrington Falls, Budderoo
    National Park", 34.623921 S 150.654809 E), which "drops 50 metres"; the top at 542 m (Wikipedia, whose 130 to 160 m
    is the descent from the plateau to the valley floor, printed and not judged);
  - Hampden Bridge, Moss Vale Road's 77 m span over the Kangaroo River: Wikipedia's point to the second (34.72778 S
    150.52111 E);
  - the river's gauge 215220 at 34.73 S 150.52 E, with a catchment of 334 km2 (Bioregional Assessments, the Sydney Basin
    bioregion's context statement, table 17, from the Sydney Catchment Authority's and the NSW Office of Water's data);
  - the village of Kangaroo Valley at 86 m, Wikipedia's point to the minute (34.7333 S 150.5333 E).

The valley's rows, each printed with both numbers:
  9. each fall: the creek is the world's trickle, creek and stream cells within 300 m of the point that gather at least
     1 km2 (a fall's named creek drains the plateau above it; the gullies off the same cliff gather hectares), and the fall
     the greatest drop from a creek cell to the lowest creek cell within 100 m of it (a plunge the source's cells spread
     over tens of metres). It passes within half the least and twice the most published height;
 10. Belmore's and Carrington's tops: the cell the fall drops from, within 30 m of the published top;
 11. the Kangaroo River runs under Hampden Bridge: the river's line, the cells within 2 km of the bridge's point that
     gather at least half what the most-gathering cell there gathers, passes within 150 m of the point;
 12. the river at the gauge gathers its 334 km2: the greatest catchment within 800 m of the gauge's point (stated to a
     hundredth of a degree), within a quarter either way;
 13. the escarpment: Belmore's top stands over the village's ground by the published rise (552 or 572 m less 86 m),
     within 15 %.
Rows the world is known to fail are owed, each under the Docs/DEBTS.md row OWED_BY_REGION names, as in species_check: an
owed row prints its numbers and "owed" and does not fail the check; an owed row that passes does, until it is taken out
of the table and its debt moved to Paid.

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

# The Kangaroo Valley's landmarks, the sources the docstring names. A fall: its name, its creek, the published point, the
# least and most published height of the fall (m), and the least and most published height of its top (m) or None.
VALLEY_REGIONS = ("kangaroo-valley", "kangaroo-valley-whole")
VALLEY_FALLS = (
    ("Fitzroy Falls", "Yarrunga Creek", -34.648011, 150.482544, 81.0, 100.0, None),
    ("Belmore Falls", "Barrengarry Creek", -34.64028, 150.55972, 77.0, 130.0, (552.0, 572.0)),
    ("Carrington Falls", "the Kangaroo River", -34.623920585248, 150.65480947495, 50.0, 50.0, (542.0, 542.0)),
)
HAMPDEN_BRIDGE = (-34.72778, 150.52111)
GAUGE_215220 = (-34.73, 150.52, 334.0)
VILLAGE = (-34.7333, 150.5333, 86.0)
TRICKLE, STREAM = 2, 4
FALL_SEARCH_M, CREEK_MIN_KM2, FALL_RUN_M, FALL_FACTOR, TOP_M = 300.0, 1.0, 100.0, 2.0, 30.0
BRIDGE_M, RIVER_REACH_M, GAUGE_SEARCH_M, GAUGE_SHARE, RISE_SHARE = 150.0, 2000.0, 800.0, 0.25, 0.15
# The valley's rows the world is known to fail, a table per region, each owed under the DEBTS.md row named. Found by these
# rows' first run (2026-09-24): a lake is a sink where the world's water stops, so a pond on a creek's course ends all it
# gathered, and the creek below it starts again from nothing.
PONDS = "The valley's rivers end in ponds"
OWED_BY_REGION = {
    "kangaroo-valley": {"Fitzroy Falls: Yarrunga Creek falls": PONDS},
    "kangaroo-valley-whole": {
        "Fitzroy Falls: Yarrunga Creek falls": PONDS,
        "Carrington Falls: the Kangaroo River falls": PONDS,
        "the Kangaroo River runs under Hampden Bridge": PONDS,
        "the river at gauge 215220 gathers its catchment": PONDS,
    },
}


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


def local_disc(frame, east, north, radius_m):
    """The cells within a radius of a local point, as row and column index arrays, from a window about the point (the whole
    valley's grid is 64 million cells, too many to measure a distance over for each landmark)."""
    r0, c0 = int(round((frame.half - north) / frame.cell)), int(round((east + frame.half) / frame.cell))
    k = int(math.ceil(radius_m / frame.cell)) + 1
    rr, cc = np.meshgrid(np.arange(r0 - k, r0 + k + 1), np.arange(c0 - k, c0 + k + 1), indexing="ij")
    keep = (cc * frame.cell - frame.half - east) ** 2 + (frame.half - rr * frame.cell - north) ** 2 <= radius_m * radius_m
    keep &= (rr >= 0) & (rr < frame.rows) & (cc >= 0) & (cc < frame.cols)
    return rr[keep], cc[keep]


def reaches_past(frame, east, north, radius_m):
    """How far a search of this radius about a local point reaches past the box's edge, m; 0 when it stays inside."""
    return max(0.0, abs(east) + radius_m - frame.half, abs(north) + radius_m - frame.half)


def fall(frame, heights, rows, cols, creek):
    """The greatest drop from a creek cell among (rows, cols) to the lowest creek cell within FALL_RUN_M of it, as (the drop,
    the upper cell, the lower cell); None when no creek cell is among them."""
    k = int(math.ceil(FALL_RUN_M / frame.cell))
    dr, dc = np.meshgrid(np.arange(-k, k + 1), np.arange(-k, k + 1), indexing="ij")
    within = (dr * dr + dc * dc) * frame.cell * frame.cell <= FALL_RUN_M * FALL_RUN_M
    dr, dc = dr[within], dc[within]
    on = creek[rows, cols]
    best = None
    for r, c in zip(rows[on], cols[on]):
        rr, cc = r + dr, c + dc
        ok = (rr >= 0) & (rr < frame.rows) & (cc >= 0) & (cc < frame.cols)
        rr, cc = rr[ok], cc[ok]
        low = int(np.argmin(np.where(creek[rr, cc], heights[rr, cc], np.inf)))
        drop = float(heights[r, c]) - float(heights[rr[low], cc[low]])
        if best is None or drop > best[0]:
            best = (drop, (int(r), int(c)), (int(rr[low]), int(cc[low])))
    return best


def valley(region, frame, heights, water, catchment):
    """The Kangaroo Valley's census, rows 9 to 13 of the docstring."""
    owed_rows = OWED_BY_REGION.get(region, {})
    km2 = frame.cell * frame.cell / 1e6
    failures, owed = [], []

    def judge(name, ok, detail):
        debt = owed_rows.get(name)
        if debt is None:
            print("%-46s %s  %s" % (name, "ok " if ok else "FAIL", detail))
            if not ok:
                failures.append(name)
        elif ok:
            print("%-46s PAID %s; take the row out of OWED_BY_REGION and move \"%s\" to Paid" % (name, detail, debt))
            failures.append(name + " (passes, still owed)")
        else:
            print("%-46s owed %s (DEBTS.md: \"%s\")" % (name, detail, debt))
            owed.append(name)

    def note(name, detail):
        print("%-46s note %s" % (name, detail))

    def from_point(r, c, east, north):
        return math.hypot(c * frame.cell - frame.half - east, frame.half - r * frame.cell - north)

    # 9 and 10. The falls, and the tops they fall from.
    creek = (water >= TRICKLE) & (water <= STREAM) & (catchment >= CREEK_MIN_KM2 / km2)
    tops = {}
    for name, stream, lat, lon, low, high, top in VALLEY_FALLS:
        title, top_title = "%s: %s falls" % (name, stream), "%s: the top it falls from" % name
        e, n = frame.local(lat, lon)
        past = reaches_past(frame, e, n, FALL_SEARCH_M)
        if past > 0.0:
            note(title, "the search about the point (east %.0f, north %.0f) reaches %.0f m past this box's edge; not judged here" % (e, n, past))
            continue
        published = ("%.0f m" % low) if low == high else ("%.0f to %.0f m" % (low, high))
        rows, cols = local_disc(frame, e, n, FALL_SEARCH_M)
        found = fall(frame, heights, rows, cols, creek)
        if found is None:
            judge(title, False, "no creek gathering %.0f km2 within %.0f m of the point, where the most any cell gathers is %.2f km2; published %s"
                  % (CREEK_MIN_KM2, FALL_SEARCH_M, float(catchment[rows, cols].max()) * km2, published))
            if top is not None:
                judge(top_title, False, "no fall found to have a top")
            continue
        drop, (tr, tc), (lr, lc) = found
        tops[name] = float(heights[tr, tc])
        judge(title, low / FALL_FACTOR <= drop <= high * FALL_FACTOR,
              "%.1f m within %.0f m of run, from %.1f m at %.0f m from the point (gathering %.1f km2) to %.1f m; published %s"
              % (drop, FALL_RUN_M, tops[name], from_point(tr, tc, e, n), float(catchment[tr, tc]) * km2, float(heights[lr, lc]), published))
        if top is not None:
            off = max(0.0, top[0] - tops[name], tops[name] - top[1])
            judge(top_title, off <= TOP_M, "the fall drops from %.1f m; published %s, %.1f m from it"
                  % (tops[name], ("%.0f m" % top[0]) if top[0] == top[1] else ("%.0f or %.0f m" % top), off))

    # 11. The river under the bridge: its line is the cells within RIVER_REACH_M of the bridge's point that gather at least
    # half what the most-gathering cell there does, so a side stream that happens to pass the bridge does not stand for it.
    title = "the Kangaroo River runs under Hampden Bridge"
    e, n = frame.local(*HAMPDEN_BRIDGE)
    past = reaches_past(frame, e, n, RIVER_REACH_M)
    if past > 0.0:
        note(title, "the search about the bridge's point (east %.0f, north %.0f) reaches %.0f m past this box's edge; not judged here" % (e, n, past))
    else:
        rows, cols = local_disc(frame, e, n, RIVER_REACH_M)
        gathered = catchment[rows, cols]
        line = gathered >= 0.5 * float(gathered.max())
        d = np.hypot(cols[line] * frame.cell - frame.half - e, frame.half - rows[line] * frame.cell - n)
        i = int(np.argmin(d))
        judge(title, float(d[i]) <= BRIDGE_M,
              "the river's line (the cells within %.0f m gathering at least half the most there, %.1f km2) passes %.0f m from the bridge's"
              " point, gathering %.1f km2 there; the span is 77 m, the search %.0f m"
              % (RIVER_REACH_M, float(gathered.max()) * km2, float(d[i]), float(gathered[line][i]) * km2, BRIDGE_M))

    # 12. What the river gathers at the gauge.
    title = "the river at gauge 215220 gathers its catchment"
    e, n = frame.local(GAUGE_215220[0], GAUGE_215220[1])
    past = reaches_past(frame, e, n, GAUGE_SEARCH_M)
    if past > 0.0:
        note(title, "the search about the gauge's point (east %.0f, north %.0f) reaches %.0f m past this box's edge; not judged here" % (e, n, past))
    else:
        rows, cols = local_disc(frame, e, n, GAUGE_SEARCH_M)
        i = int(np.argmax(catchment[rows, cols]))
        most = float(catchment[rows[i], cols[i]]) * km2
        judge(title, abs(most - GAUGE_215220[2]) <= GAUGE_SHARE * GAUGE_215220[2],
              "at most %.1f km2 within %.0f m of the gauge's point (%.0f m from it, water class %d); published %.0f km2"
              % (most, GAUGE_SEARCH_M, from_point(rows[i], cols[i], e, n), int(water[rows[i], cols[i]]), GAUGE_215220[2]))

    # 13. The escarpment, from the village's ground to Belmore's top.
    title = "the escarpment rises from the floor to Belmore"
    e, n = frame.local(VILLAGE[0], VILLAGE[1])
    belmore = VALLEY_FALLS[1]
    be, bn = frame.local(belmore[2], belmore[3])
    if reaches_past(frame, e, n, 0.0) > 0.0 or reaches_past(frame, be, bn, FALL_SEARCH_M) > 0.0:
        note(title, "the village's point (east %.0f, north %.0f) or Belmore Falls' search lies past this box's edge; not judged here" % (e, n))
    elif belmore[0] not in tops:
        judge(title, False, "no fall found at Belmore to measure its top")
    else:
        r, c = frame.cell_of(e, n)
        floor = float(heights[r, c])
        rise = tops[belmore[0]] - floor
        least, most = belmore[6][0] - VILLAGE[2], belmore[6][1] - VILLAGE[2]
        judge(title, least * (1.0 - RISE_SHARE) <= rise <= most * (1.0 + RISE_SHARE),
              "Belmore's top at %.1f m over the village's ground at %.1f m: %.1f m; published %.0f or %.0f m (the top at %.0f or %.0f m, the village at %.0f m)"
              % (tops[belmore[0]], floor, rise, least, most, belmore[6][0], belmore[6][1], VILLAGE[2]))

    if failures:
        print("census_check: FAIL (%s)" % ", ".join(failures))
        return 1
    if owed:
        print("census_check: ok, every judged row of the valley passes; %d row(s) owed in DEBTS.md" % len(owed))
    else:
        print("census_check: ok, the valley's falls, river and escarpment are where the sources put them")
    return 0


def main(argv):
    world = argv[1] if len(argv) > 1 else DEFAULT_WORLD
    layers = os.path.join(ROOT, world, "layers")
    world_json = os.path.join(ROOT, world, "world.json")
    if os.path.isfile(world_json) and json.load(open(world_json, encoding="utf-8")).get("region") in VALLEY_REGIONS:
        sidecar, water = load(os.path.join(layers, "water.json"))
        _, heights = load(os.path.join(layers, "heights.json"))
        _, catchment = load(os.path.join(layers, "catchment.json"))
        if water is None or heights is None or catchment is None:
            print("a layer is missing under %s (water, heights and catchment are needed)" % layers)
            return 2
        return valley(json.load(open(world_json, encoding="utf-8"))["region"], Frame(sidecar), heights, water, catchment)
    sidecar, water = load(os.path.join(layers, "water.json"))
    _, surface = load(os.path.join(layers, "surface.json"))
    _, topology = load(os.path.join(layers, "topology.json"))
    _, heights = load(os.path.join(layers, "heights.json"))
    if water is None or surface is None or topology is None or heights is None or not os.path.isfile(world_json):
        print("a layer is missing under %s (water, surface, topology, heights and world.json are needed)" % layers)
        return 2
    region = json.load(open(world_json, encoding="utf-8")).get("region", "bherwerre")
    if region != "bherwerre":
        print("census_check: this file holds Bherwerre's landmarks and the Kangaroo Valley's; the world is set in %s, which has"
              " none here; no verdict" % region)
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
