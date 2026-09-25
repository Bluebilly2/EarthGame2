#!/usr/bin/env python3
"""stand_check.py: do the world's trees stand where its canopy does, as far apart as their crowns allow, and do its
sticks and cobbles lie where they would?

The M1.6a contract (`Docs/contracts/M1.6_WHAT_STANDS_AND_LIES.md`) states the rules; this check restates them in numpy
against the world's other layers — the overstory, the topology, the water and the soil — and imports nothing the game
runs. The stand's codes and the loose byte are read by the legends their sidecars print, the stand's layout by its dtype
(two bytes since WG.2c, 2026-09-25; one in a world made before: `stand_layout`). Each tall plant's crown share and
height range are restated here from `Docs/ECOSYSTEM.md`'s tables, so a check that agreed only because it asked the
engine is not possible; the sand forest's height is NSW BioNet's (its profile of Bangalay Sand Forest, "approximately
5 - 20 m tall", read 2026-09-10).

Rows, each with both numbers:
  1. every trunk stands on a canopied cell, and is the canopy's own species;
  2. no two trunks stand nearer than (1 - 0.4)(r_a + r_b), cell centre to cell centre, r a crown's radius (half its
     crown share of its height);
  3. the crowns' cover: the share of canopied cells lying under a crown, inside the design's 0.5 to 0.95;
  4. every trunk's height inside its species' range, a height step either side;
  5. the sand forest's trees — bangalay and coast banksia standing on beach or dune — inside BioNet's 5 to 20 m, a step
     either side. An inland box, one whose bake has no cell at or below the datum and whose world has no beach or dune
     (the 8 km Kangaroo Valley), has no sand forest to judge: the row prints a note and gives no verdict (2026-09-24);
  6. stems a hectare of each tall plant's own canopy, inside the design's 50 to 1,500 (a published benchmark by
     diameter class exists, Gibbons and others 2010, and is not yet read: DEBTS.md);
  7. sticks lie only within a crown's reach, and a cell, of a trunk;
  8. cobbles lie only where stone lies loose: never on a dune, the sea, a lake or a swamp, and on deep soil only on a
     shore platform, a cliff, a beach or a creek's or a stream's bed. The soil layer keeps a depth to its own step, so
     a cell within half a step of the thin-soil line is counted thin, and the row says how many cobbled cells were;
  9. the valley's walls carry its canopy (WG.2c, 2026-09-25): in a Kangaroo Valley world, the canopy stands on the land of
     15 to 25 and of 25 to 31 degrees at least half as often as on the land under 15, where the valley's records find its
     tall plants more often on those slopes than the recorders walk (the contract's table, printed beside). The coast's
     records lean no such way, and a world of another region prints a note;
 10. the crowns cover what the pre-1750 map's groups carry, in all (WG.2c): NVIS 7.0's Major Vegetation Groups for the
     world's region (`Tools/data/fetch_nvis.py`), each group's crown cover as its NVIS fact sheet states it, and the land
     under a crown on the land the map puts in those groups inside the range their own covers give it together. The map
     is the check and never the model's input. A region with no map here prints a note;
 11. the same group by group, for each group on at least 1 % of that land.

Rows the world is known to fail are owed, as species_check's are: each under the Docs/DEBTS.md row OWED names, a table per
region (the world's world.json names its region), and row 6 a plant at a time. An owed row prints its numbers and "owed" where
the verdict would be and does not fail the check; an owed row that passes does fail it, until it is taken out of OWED and its
debt moved to Paid.

Exit 0 when every row passes (a row that prints a note gives no verdict), 1 when any fails, 2 when the world is missing a
layer this needs.
Run from the repository root:
    python Tools/verifiers/checks/stand_check.py [world folder]
(default Artefacts/worlds/gate.)
"""
import json
import math
import os
import sys

import numpy as np

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))))
DEFAULT_WORLD = os.path.join("Artefacts", "worlds", "gate")
NP_DTYPES = {"u8": "u1", "u16": "<u2", "i16": "<i2", "u32": "<u4", "f32": "<f4"}
EARTH_RADIUS_M = 6371000.0   # the sidecar's frame: a tangent plane, small-angle, as the engine's LocalFrame

# The tall plants, from ECOSYSTEM.md: the heights each grows to (its tables of plants) and the crown's diameter as a
# share of height (its tables of what stands); the Kangaroo Valley's six from its own tables (WG.2c, 2026-09-25).
CROWN_SHARE = {"Blackbutt": 0.35, "Bangalay": 0.45, "OldManBanksia": 0.60, "CoastBanksia": 0.55, "SwampPaperbark": 0.50,
               "SydneyBlueGum": 0.35, "CabbageTreePalm": 0.25, "SilvertopAsh": 0.35, "RiverOak": 0.35, "ScribblyGum": 0.55,
               "LillyPilly": 0.50}
HEIGHTS_M = {"Blackbutt": (20.0, 40.0), "Bangalay": (12.0, 20.0), "OldManBanksia": (4.0, 12.0), "CoastBanksia": (5.0, 15.0), "SwampPaperbark": (3.0, 9.0),
             "SydneyBlueGum": (25.0, 50.0), "CabbageTreePalm": (15.0, 30.0), "SilvertopAsh": (15.0, 45.0), "RiverOak": (15.0, 35.0),
             "ScribblyGum": (7.5, 15.0), "LillyPilly": (10.0, 20.0)}
SAND_FOREST = ("Bangalay", "CoastBanksia")
SAND_FOREST_M = (5.0, 20.0)
# The contract's design.
CROWN_OVERLAP = 0.4
COVER_BAND = (0.5, 0.95)
STEMS_BAND = (50.0, 1500.0)
THIN_SOIL_M = 0.25
# The layers' own codes, as their legends state them.
T_BEACH, T_DUNE, T_CLIFF, T_PLATFORM = 2, 4, 128, 256
W_CREEK, W_STREAM, W_LAKE, W_SWAMP, W_SEA = 3, 4, 5, 6, 7

# Row 9, the valley's walls (WG.2c): the regions whose records put their tall plants on the slopes, and the records' ratios the
# contract WG.2c measured (a plant's share of its records in a slope class against all the plants' records' share there).
WALLED = ("kangaroo-valley", "kangaroo-valley-whole")
WALL_CLASSES_DEG = ((0.0, 15.0), (15.0, 25.0), (25.0, 31.0))
WALL_SHARE = 0.5
WALL_RECORDS = "Sydney blue gum 1.47 and 1.70, the cabbage tree palm 2.25 and 3.40 times as often as the recorders walk"

# Rows 10 and 11, the pre-1750 map (WG.2c): NVIS 7.0's Major Vegetation Groups for the world's region as fetch_nvis.py lays them,
# and each group's crown cover as its NVIS fact sheet states it (DCCEEW's NVIS fact sheet series, MVG 1 to 23, read
# 2026-09-25). The sheets state a crown cover where they give one and a foliage projective cover where they do not, and pair
# the two: open forest's crown cover of 50 to 80 per cent with a foliage cover of 30 to 70 (MVG 3), woodland's 20 to 50 with
# 10 to 30 (MVG 5); a foliage cover is read to a crown cover by that pairing. Crowns are what the world draws.
NVIS = os.path.join(ROOT, "Data", "cache", "nvis")
CROWN_BANDS = {
    "Rainforests and Vine Thickets": (0.80, 1.00),     # MVG 1: "typically with greater than 70 per cent foliage cover"
    "Eucalypt Tall Open Forests": (0.50, 0.80),        # MVG 2: "projective foliage cover of between 30 and 70 per cent"
    "Eucalypt Open Forests": (0.50, 0.80),             # MVG 3: "crown cover 50 - 80 per cent (foliage projective cover of 30 - 70 per cent)"
    "Eucalypt Woodlands": (0.20, 0.50),                # MVG 5: "a crown cover of 20 - 50 per cent (projective foliage cover 10 - 30 per cent)"
    "Heathlands": (0.00, 0.20),                        # MVG 18: trees as sparse emergents, or mallee "up to 20 per cent canopy cover"
    "Casuarina Forests and Woodlands": (0.20, 1.00),   # MVG 8: "crown cover >20 per cent"
}
GROUP_LEAST_LAND = 0.01
MAP_IN_ALL = "the crowns cover what the pre-1750 map carries"
MAP_BY_GROUP = "the crowns follow each group of that map"
STEMS_TITLE = "stems a hectare of each plant's own canopy"

# Rows the world is known to fail, each under the DEBTS.md row named, a table per region (the module's docstring); the stems row
# is owed a plant at a time, keyed (the row, the plant).
MAP_GROUPS = "The canopy does not follow the pre-1750 map's groups"
BIG_CROWNS = "Big crowns cover less of their own canopy"
OWED_BY_REGION = {
    "bherwerre": {MAP_BY_GROUP: MAP_GROUPS},
    "kangaroo-valley": {MAP_IN_ALL: BIG_CROWNS, MAP_BY_GROUP: MAP_GROUPS, (STEMS_TITLE, "SydneyBlueGum"): BIG_CROWNS},
    "kangaroo-valley-whole": {MAP_IN_ALL: BIG_CROWNS, MAP_BY_GROUP: MAP_GROUPS, (STEMS_TITLE, "SydneyBlueGum"): BIG_CROWNS},
}


def layer(world, name):
    path = os.path.join(ROOT, world, "layers", name + ".json")
    if not os.path.isfile(path):
        return None, None
    sidecar = json.load(open(path, encoding="utf-8"))
    raw = os.path.join(os.path.dirname(path), sidecar["raw"])
    return sidecar, np.fromfile(raw, dtype=NP_DTYPES[sidecar["dtype"]]).reshape(sidecar["height"], sidecar["width"])


def region_of(world):
    """The region the world is set in, off its own world.json; Bherwerre when the file does not say, as the worlds before WG.2 were."""
    world_json = os.path.join(ROOT, world, "world.json")
    if os.path.isfile(world_json):
        return json.load(open(world_json, encoding="utf-8")).get("region", "bherwerre")
    return "bherwerre"


def lowest_baked(world):
    """The lowest cell of the bake the world was made from, Data/regions/<region> with the region read off the world's own
    world.json, and the bake's sidecar; the height is None when the bake is not on this machine."""
    sidecar_path = os.path.join(ROOT, "Data", "regions", region_of(world), "heights.json")
    if not os.path.isfile(sidecar_path):
        return None, sidecar_path
    sidecar = json.load(open(sidecar_path, encoding="utf-8"))
    raw = os.path.join(os.path.dirname(sidecar_path), sidecar.get("raw", "heights.r32"))
    return float(np.fromfile(raw, dtype="<f4").min()), sidecar_path


def legend_names(sidecar):
    """'... (1=Blackbutt, 2=Bangalay, ...) ...' -> {1: 'Blackbutt', ...}, from the first parenthesised list."""
    text = sidecar.get("source", "")
    names = {}
    for part in text.replace("(", ",").replace(")", ",").replace(":", ",").split(","):
        if "=" in part:
            number, name = part.strip().split("=", 1)
            if number.strip().isdigit():
                names.setdefault(int(number), name.strip().split(" ")[0].rstrip(";."))
    return names


def stand_layout(sidecar):
    """How a stand code is laid out, by the layer's dtype: (the shift to the plant's number, the mask of the height's steps).
    Two bytes since WG.2c (2026-09-25), the plant's catalogue number high and the steps low; one byte in a world made
    before, the plant in the top three bits and the steps in the low five. The plant numbers are the legend's either way."""
    dtype = sidecar.get("dtype")
    if dtype == "u16":
        return 8, 0xFF
    if dtype == "u8":
        return 5, 0x1F
    raise ValueError("a stand layer of dtype %s, which is neither layout" % dtype)


def height_step(sidecar):
    """The height step the stand legend states ('... steps of 1.25 m')."""
    text = sidecar.get("source", "")
    marker = "steps of "
    at = text.find(marker)
    if at < 0:
        raise ValueError("the stand layer's legend states no height step")
    return float(text[at + len(marker):].split(" ")[0])


def map_groups(world, sidecar):
    """Each cell's pre-1750 Major Vegetation Group (NVIS 7.0) for the world's region, as an index into the names returned with
    it, -1 off the map or where it holds no data (a transparent pixel), and the map's path; None for the groups when the
    region has no map here. A cell's latitude and longitude are restated from the sidecar's small-angle frame, the cell's
    centre (half - row x cell) north and (col x cell - half) east of the region's centre, and the map's pixel is the one
    whose box holds it (the manifest's box and pixel size, north-west first)."""
    name = region_of(world) + "-pre1750-mvg.png"
    manifest_path = os.path.join(NVIS, "manifest.json")
    path = os.path.join(NVIS, name)
    if not os.path.isfile(manifest_path) or not os.path.isfile(path):
        return None, [], path
    entry = json.load(open(manifest_path, encoding="utf-8"))["files"].get(name)
    if entry is None:
        return None, [], path
    from PIL import Image   # only a world whose region has a map needs it
    image = np.array(Image.open(path).convert("RGBA"))
    names = sorted({c["name"] for c in entry["legend"]})
    code = np.full(image.shape[:2], -1, dtype=np.int16)
    for c in entry["legend"]:
        code[(image[..., 3] == 255) & np.all(image[..., :3] == np.array(c["rgb"], dtype=np.uint8), axis=2)] = names.index(c["name"])
    west, _, _, north = entry["box_west_south_east_north"]
    dlon, dlat = entry["pixel_deg"]
    cell, half = sidecar["cell_m"], sidecar["extent_m"] / 2.0
    lat = sidecar["centre_lat"] + np.degrees((half - np.arange(sidecar["height"]) * cell) / EARTH_RADIUS_M)
    lon = sidecar["centre_lon"] + np.degrees((np.arange(sidecar["width"]) * cell - half) / (EARTH_RADIUS_M * math.cos(math.radians(sidecar["centre_lat"]))))
    pixel_row = np.floor((north - lat) / dlat).astype(np.int64)
    pixel_col = np.floor((lon - west) / dlon).astype(np.int64)
    rows_in = (pixel_row >= 0) & (pixel_row < code.shape[0])
    cols_in = (pixel_col >= 0) & (pixel_col < code.shape[1])
    groups = np.full((sidecar["height"], sidecar["width"]), -1, dtype=np.int16)
    groups[np.ix_(rows_in, cols_in)] = code[np.ix_(pixel_row[rows_in], pixel_col[cols_in])]
    return groups, names, path


def shifted(grid, dr, dc, fill=0):
    """grid moved so out[r, c] = grid[r + dr, c + dc], filled outside."""
    out = np.full(grid.shape, fill, dtype=grid.dtype)
    h, w = grid.shape
    r0, r1 = max(0, -dr), min(h, h - dr)
    c0, c1 = max(0, -dc), min(w, w - dc)
    out[r0:r1, c0:c1] = grid[r0 + dr:r1 + dr, c0 + dc:c1 + dc]
    return out


def main(argv):
    world = argv[1] if len(argv) > 1 else DEFAULT_WORLD
    wanted = ["stand", "loose", "overstory", "topology", "water", "soil_depth"]
    got = {}
    for name in wanted:
        sidecar, grid = layer(world, name)
        if grid is None:
            print("this world has no %s layer; %s are needed under %s" % (name, ", ".join(wanted), os.path.join(ROOT, world, "layers")))
            return 2
        got[name] = (sidecar, grid)
    stand_side, stand = got["stand"]
    loose = got["loose"][1]
    over_side, over = got["overstory"]
    topo = got["topology"][1]
    water = got["water"][1]
    soil = got["soil_depth"][1].astype(np.float64) * got["soil_depth"][0]["scale"]
    cell = stand_side["cell_m"]
    step = height_step(stand_side)
    tall_names = legend_names(stand_side)
    over_names = legend_names(over_side)

    shift, mask = stand_layout(stand_side)
    species_code = (stand >> shift).astype(np.int64)
    heights = (stand & mask).astype(np.float64) * step
    trunk = stand > 0
    names = np.array([""] + [tall_names.get(i, "?") for i in range(1, int(species_code.max()) + 1)])[species_code]
    share = np.zeros(stand.shape)
    for name, s in CROWN_SHARE.items():
        share[names == name] = s
    radius = np.where(trunk, 0.5 * share * heights, 0.0)
    over_name = np.array([""] + [over_names.get(i, "?") for i in range(1, int(over.max()) + 1)])[over]
    canopied = over > 0

    region = region_of(world)
    owed_rows = OWED_BY_REGION.get(region, {})
    failures, owed = [], []

    def expect(title, ok, detail):
        debt = owed_rows.get(title)
        if debt is None:
            print("%-50s %s  %s" % (title, "ok " if ok else "FAIL", detail))
            if not ok:
                failures.append(title)
        elif ok:
            print("%-50s PAID %s; take the row out of OWED and move \"%s\" to Paid" % (title, detail, debt))
            failures.append(title + " (passes, still owed)")
        else:
            print("%-50s owed %s (DEBTS.md: \"%s\")" % (title, detail, debt))
            owed.append(title)

    trunks = int(trunk.sum())
    wrong_place = int((trunk & ~canopied).sum())
    wrong_species = int((trunk & canopied & (names != over_name)).sum())
    unknown = int((trunk & (share == 0.0)).sum())
    expect("every trunk stands under its own canopy", trunks > 0 and wrong_place == 0 and wrong_species == 0 and unknown == 0,
           "%s trunks; %d where no canopy stands, %d not the canopy's species, %d of a plant this check does not know"
           % ("{:,}".format(trunks), wrong_place, wrong_species, unknown))

    largest = max(0.5 * CROWN_SHARE[n] * HEIGHTS_M[n][1] for n in CROWN_SHARE) + step
    reach = int(np.ceil((1.0 - CROWN_OVERLAP) * 2.0 * largest / cell))
    too_near = 0
    for dr in range(0, reach + 1):
        for dc in range(-reach, reach + 1):
            if dr == 0 and dc <= 0:
                continue
            other = shifted(radius, dr, dc)
            d = cell * np.hypot(dr, dc)
            too_near += int(((radius > 0) & (other > 0) & (d < (1.0 - CROWN_OVERLAP) * (radius + other) - 1e-9)).sum())
    expect("no two trunks nearer than their crowns allow", too_near == 0,
           "%d pairs nearer than (1 - %.1f)(r_a + r_b), cell centre to cell centre, within %d cells" % (too_near, CROWN_OVERLAP, reach))

    covered = np.zeros(stand.shape, bool)
    cover_reach = int(np.ceil(largest / cell))
    for dr in range(-cover_reach, cover_reach + 1):
        for dc in range(-cover_reach, cover_reach + 1):
            covered |= shifted(radius, dr, dc) >= cell * np.hypot(dr, dc) - 1e-9 if (dr or dc) else radius > 0
    cover = float((covered & canopied).sum()) / max(1, int(canopied.sum()))
    expect("the crowns cover about what the canopy covers", COVER_BAND[0] <= cover <= COVER_BAND[1],
           "%.3f of %s canopied cells lie under a crown; the design's band is %.2f to %.2f" % (cover, "{:,}".format(int(canopied.sum())), COVER_BAND[0], COVER_BAND[1]))

    out_of_range = []
    for name, (low, high) in HEIGHTS_M.items():
        here = heights[trunk & (names == name)]
        if here.size and (here.min() < low - step or here.max() > high + step):
            out_of_range.append("%s %.2f..%.2f against %g..%g" % (name, here.min(), here.max(), low, high))
    expect("every tree is as tall as its species grows", not out_of_range, "; ".join(out_of_range) or "every tall plant inside its range, a step either side")

    sand = (topo & (T_BEACH | T_DUNE)) > 0
    sand_trees = trunk & sand & np.isin(names, SAND_FOREST)
    sh = heights[sand_trees]
    inside = int(((sh >= SAND_FOREST_M[0] - step) & (sh <= SAND_FOREST_M[1] + step)).sum())
    sand_title = "the sand forest stands inside BioNet's 5 to 20 m"
    lowest, bake = lowest_baked(world) if sh.size == 0 else (None, None)
    if sh.size == 0 and not sand.any() and lowest is not None and lowest > 0.0:
        # An inland box has no sand forest to judge: the engine lays a beach or a dune only within reach of the sea, and
        # BioNet's Bangalay Sand Forest grows on the coast's sands. Whether the box has a sea is read off the bake, not the
        # world, so a coast whose world lost its beaches still fails here (the sweep's first red, on the 8 km valley,
        # 2026-09-24).
        print("%-50s note  an inland box: the bake's lowest cell is %.1f m above the datum (%s) and the world has 0 cells of"
              " beach or dune; no sand for a sand forest, no verdict" % (sand_title, lowest, os.path.relpath(bake, ROOT)))
    else:
        expect(sand_title, sh.size > 0 and inside == sh.size,
               "%d of %d bangalay and coast banksia on sand inside %g-%g m, a step either side%s"
               % (inside, sh.size, SAND_FOREST_M[0], SAND_FOREST_M[1], (", from %.2f to %.2f m" % (sh.min(), sh.max())) if sh.size else ""))

    rows, bad, measured = [], [], []
    for name in CROWN_SHARE:
        canopy_ha = float((over_name == name).sum()) * cell * cell / 10000.0
        stems = int((trunk & (names == name)).sum())
        if canopy_ha <= 0:
            continue
        per_ha = stems / canopy_ha
        rows.append("%s %.0f" % (name, per_ha))
        measured.append(name)
        if not STEMS_BAND[0] <= per_ha <= STEMS_BAND[1]:
            bad.append(name)
    # The row is owed a plant at a time: a plant's own debt under (the row, the plant), the rest judged as ever.
    stems_owed = {key[1]: debt for key, debt in owed_rows.items() if isinstance(key, tuple) and key[0] == STEMS_TITLE}
    unowed = [n for n in bad if n not in stems_owed]
    paid = [n for n in stems_owed if n in measured and n not in bad]
    detail = ", ".join(rows) + " a hectare; the design's band %g to %g%s" % (STEMS_BAND[0], STEMS_BAND[1], (" (outside: " + ", ".join(bad) + ")") if bad else "")
    if unowed or not bad:
        expect(STEMS_TITLE, not unowed, detail)
    else:
        print("%-50s owed %s (DEBTS.md: %s)" % (STEMS_TITLE, detail, "; ".join("%s \"%s\"" % (n, stems_owed[n]) for n in bad)))
        owed.append(STEMS_TITLE)
    if paid:
        print("%-50s PAID for %s; take (the row, the plant) out of OWED and move the debt to Paid" % (STEMS_TITLE, ", ".join(paid)))
        failures.append(STEMS_TITLE + " (passes for " + ", ".join(paid) + ", still owed)")

    sticks = (loose & 0x0F) > 0
    reachable = np.zeros(stand.shape, bool)
    stick_reach = int(np.ceil((largest + cell) / cell))
    for dr in range(-stick_reach, stick_reach + 1):
        for dc in range(-stick_reach, stick_reach + 1):
            reachable |= (shifted(radius, dr, dc) > 0) & (shifted(radius, dr, dc) + cell >= cell * np.hypot(dr, dc))
    stray = int((sticks & ~reachable).sum())
    expect("sticks lie under the trees", int(sticks.sum()) > 0 and stray == 0,
           "%s cells hold %s sticks; %d lie beyond a crown and a cell of every trunk"
           % ("{:,}".format(int(sticks.sum())), "{:,}".format(int((loose & 0x0F).sum())), stray))

    cobbles = (loose >> 4) > 0
    under_water = np.isin(water, [W_SEA, W_LAKE, W_SWAMP])
    on_dune = (topo & T_DUNE) > 0
    # A depth the engine found just under the line can be written on it (the first stood world, 2026-09-11).
    soil_step = float(got["soil_depth"][0]["scale"])
    thin = soil < THIN_SOIL_M + 0.5 * soil_step
    at_line = int((cobbles & (np.abs(soil - THIN_SOIL_M) < 0.5 * soil_step)).sum())
    loose_ground = ((topo & (T_PLATFORM | T_CLIFF | T_BEACH)) > 0) | np.isin(water, [W_CREEK, W_STREAM]) | thin
    misplaced = int((cobbles & (under_water | on_dune | ~loose_ground)).sum())
    expect("cobbles lie only where stone lies loose", int(cobbles.sum()) > 0 and misplaced == 0,
           "%s cells hold %s cobbles; %d on a dune, under water, or on deep soil that is no platform, cliff, beach or bed;"
           " %d on the %.2f m thin-soil line as the soil layer's %g m step writes it, counted thin"
           % ("{:,}".format(int(cobbles.sum())), "{:,}".format(int((loose >> 4).sum())), misplaced, at_line, THIN_SOIL_M, soil_step))

    # The ground the plants grow on: the community skips the sea and the lakes.
    grows_here = ~np.isin(water, [W_SEA, W_LAKE])

    walls_title = "the valley's walls carry its canopy"
    if region not in WALLED:
        print("%-50s note  a world of %s; the row is the Kangaroo Valley's, whose records find its tall plants on its slopes" % (walls_title, region))
    else:
        heights_side, ground = layer(world, "heights")
        if ground is None:
            print("this world has no heights layer; the valley's walls row needs one under %s" % os.path.join(ROOT, world, "layers"))
            return 2
        dz_row, dz_col = np.gradient(ground.astype(np.float32), np.float32(heights_side["cell_m"]))
        slope = np.hypot(dz_row, dz_col)
        del dz_row, dz_col
        shares = []
        for low, high in WALL_CLASSES_DEG:
            in_class = grows_here & (slope >= math.tan(math.radians(low))) & (slope < math.tan(math.radians(high)))
            shares.append(float((canopied & in_class).sum()) / max(1, int(in_class.sum())))
        del slope
        flat = shares[0]
        expect(walls_title, flat > 0 and all(s >= WALL_SHARE * flat for s in shares[1:]),
               "canopy on %.1f %% of the land under %g degrees, %.1f %% of %g to %g and %.1f %% of %g to %g: at least %.1f of the flat's"
               " asked; the records on those slopes: %s (WG.2c's table)"
               % (100 * shares[0], WALL_CLASSES_DEG[0][1], 100 * shares[1], WALL_CLASSES_DEG[1][0], WALL_CLASSES_DEG[1][1],
                  100 * shares[2], WALL_CLASSES_DEG[2][0], WALL_CLASSES_DEG[2][1], WALL_SHARE, WALL_RECORDS))

    groups, group_names, map_path = map_groups(world, stand_side)
    if groups is None:
        print("%-50s note  no pre-1750 map for %s at %s; run Tools/data/fetch_nvis.py" % (MAP_IN_ALL, region, os.path.relpath(map_path, ROOT)))
    else:
        mapped = grows_here & (groups >= 0)
        judged, ha = [], cell * cell / 10000.0
        for name, (low, high) in CROWN_BANDS.items():
            if name not in group_names:
                continue
            in_group = grows_here & (groups == group_names.index(name))
            land_cells = int(in_group.sum())
            if land_cells:
                judged.append((name, low, high, land_cells, float(covered[in_group].mean())))
        cells = sum(j[3] for j in judged)
        if cells == 0:
            print("%-50s note  the map puts none of this world's land in a group whose fact sheet states its cover" % MAP_IN_ALL)
        else:
            overall = sum(j[3] * j[4] for j in judged) / cells
            band_low = sum(j[3] * j[1] for j in judged) / cells
            band_high = sum(j[3] * j[2] for j in judged) / cells
            expect(MAP_IN_ALL, band_low <= overall <= band_high,
                   "crowns over %.2f of the %s ha the map puts in a group whose NVIS fact sheet states its cover (%.0f %% of the mapped"
                   " land); those groups' covers give %.2f to %.2f together (%s)"
                   % (overall, "{:,.0f}".format(cells * ha), 100.0 * cells / max(1, int(mapped.sum())), band_low, band_high,
                      os.path.relpath(map_path, ROOT)))
            shown = [j for j in judged if j[3] >= GROUP_LEAST_LAND * cells]
            outside = [j[0] for j in shown if not j[1] <= j[4] <= j[2]]
            expect(MAP_BY_GROUP, not outside,
                   "; ".join("%s %.2f in %.2f to %.2f on %.0f %%" % (j[0], j[4], j[1], j[2], 100.0 * j[3] / cells) for j in shown)
                   + ((" (outside: %s)" % ", ".join(outside)) if outside else ""))

    if failures:
        print("stand_check: FAIL (%s)" % ", ".join(failures))
        return 1
    if owed:
        print("stand_check: ok, %s trees stand where the canopy does and every judged row passes; %d row(s) owed in DEBTS.md"
              % ("{:,}".format(trunks), len(owed)))
        return 0
    print("stand_check: ok, %s trees stand where the canopy does and what lies under them lies where it would" % "{:,}".format(trunks))
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main(sys.argv))
    except (OSError, ValueError, KeyError, IndexError) as error:
        print("stand_check FAILED: %s" % error, file=sys.stderr)
        sys.exit(1)
