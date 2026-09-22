#!/usr/bin/env python3
"""species_check.py: does the world grow its plants where people have found them?

The world grows twelve plants by rule. The Atlas of Living Australia holds records of the same twelve inside the
region's box, made by people in the field (`Tools/data/fetch_ala.py` caches them: Bherwerre's flat under Data/cache/ala,
another region's under Data/cache/ala/<region>, the region read off the world's own world.json). This check lays the records on
the world's own layers and asks, plant by plant, whether the rules put the plant where it was found. It imports
nothing the game runs: the plants are matched by the names the layers' own legends print, and the frame that
turns a latitude into metres is restated below from the raster sidecar's statement of it.

Which records count: not a planted one (basis LIVING_SPECIMEN — the Booderee Botanic Gardens are inside the box,
and a garden is not a habitat), and only one placed to within 100 m (its stated coordinate uncertainty, or four
decimal places of both coordinates where none is stated). The records are presence only, gathered along tracks
and biased towards plants people notice, so they can say where a plant is and never how much of it there is.
That is why no row asks for an area.

Rows, for every plant with at least MIN_RECORDS usable records, each printed with both numbers:
  1. round its records: the plant's share of the land within 100 m of its records, against its share of all the
     land in the box. At least one means the world grows it at least as often where it was found as anywhere;
     below one, the world has it elsewhere. For a plant the world grows on under a hectare the share means
     nothing, and the row asks instead that the world grow it within 100 m of at least half its records;
  2. how far from the sea: the median distance from the sea of the world's plant against its records', within a
     factor of two either way. A world with no sea cell (the Kangaroo Valley) has no such distance: the row prints a
     note and gives no verdict.
Plants with fewer records are printed as notes, with no verdict.

Rows the world is known to fail are owed, each under the Docs/DEBTS.md row OWED names, a table per region (the world's
world.json names its region; Bherwerre's rows are M1.2b's, the valley's are the coast's plant table grown in the
valley, WG.2). An owed row prints its
numbers and "owed" where the verdict would be and does not fail the check; an owed row that passes does fail it,
until it is taken out of OWED and its debt moved to Paid. So the table can only shrink as the world gets better,
and a row not in it that fails still fails.

Exit 0 when every judged row passes, 1 when any fails, 2 when the world or the records are missing.
Run from the repository root:
    python Tools/verifiers/checks/species_check.py [world folder]
(default Artefacts/worlds/gate.)
"""
import glob
import json
import math
import os
import sys

import numpy as np

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))))
DEFAULT_WORLD = os.path.join("Artefacts", "worlds", "gate")
CACHE = os.path.join(ROOT, "Data", "cache", "ala")
NP_DTYPES = {"u8": "u1", "u16": "<u2", "i16": "<i2", "u32": "<u4", "f32": "<f4"}
EARTH_RADIUS_M = 6371000.0   # the sidecar's frame: a tangent plane, small-angle, as the engine's LocalFrame
NEAR_M = 100.0
MIN_RECORDS = 5
MIN_SHARE_RATIO = 1.0
SEA_RATIO = 2.0
TINY_HA = 1.0
SEA = 7

# The rows M1.2b's four fixes left off (2026-09-10), each owed under the DEBTS.md row named; and the valley's rows, where
# the coast's plant table is grown by the coast's rules until the valley has a table of its own (WG.2, 2026-09-22).
STILL_OFF = "The plants the records still find nearer the sea"
COAST_TABLE = "The valley grows the coast's plant table"
OWED_BY_REGION = {
    "bherwerre": {
        ("Blackbutt", "round its records"): STILL_OFF,
        ("Blackbutt", "how far from the sea"): STILL_OFF,
        ("Bangalay", "round its records"): STILL_OFF,
        ("Bangalay", "how far from the sea"): STILL_OFF,
        ("Lomandra", "round its records"): STILL_OFF,
        ("Lomandra", "how far from the sea"): STILL_OFF,
        ("SawSedge", "round its records"): STILL_OFF,
        ("SawSedge", "how far from the sea"): STILL_OFF,
        ("Bracken", "round its records"): STILL_OFF,
    },
    "kangaroo-valley": {
        ("Bracken", "round its records"): COAST_TABLE,
        ("Lomandra", "round its records"): COAST_TABLE,
        ("SawSedge", "round its records"): COAST_TABLE,
        ("OldManBanksia", "round its records"): COAST_TABLE,
    },
}


def region_of(world):
    """The region the world is set in, off its own world.json; Bherwerre when the file does not say."""
    world_json = os.path.join(ROOT, world, "world.json")
    if os.path.isfile(world_json):
        return json.load(open(world_json, encoding="utf-8")).get("region", "bherwerre")
    return "bherwerre"


def cache_dir(world):
    """The records for the world's region, as fetch_ala.py lays them: Bherwerre's flat, another region's in its own folder."""
    region = region_of(world)
    return CACHE if region == "bherwerre" else os.path.join(CACHE, region)


def layer(world, name):
    path = os.path.join(ROOT, world, "layers", name + ".json")
    if not os.path.isfile(path):
        return None, None
    sidecar = json.load(open(path, encoding="utf-8"))
    raw = os.path.join(os.path.dirname(path), sidecar["raw"])
    return sidecar, np.fromfile(raw, dtype=NP_DTYPES[sidecar["dtype"]]).reshape(sidecar["height"], sidecar["width"])


def legend(sidecar):
    """Species name -> the code the plant layers use, read off the layer's own legend ('1=Blackbutt, 2=...')."""
    listed = sidecar.get("source", "").split(":", 1)[-1]
    out = {}
    for part in listed.split(","):
        if "=" in part:
            number, name = part.strip().split("=", 1)
            out[name.strip()] = int(number)
    return out


def usable(record):
    if record.get("basis") == "LIVING_SPECIMEN":
        return False
    uncertainty = record.get("uncertainty_m")
    if uncertainty is not None:
        return uncertainty <= NEAR_M
    def places(v):
        text = repr(v)
        return len(text.split(".")[1]) if "." in text else 0
    return places(record["lat"]) >= 4 and places(record["lon"]) >= 4


def main(argv):
    world = argv[1] if len(argv) > 1 else DEFAULT_WORLD
    got = {}
    for name in ("overstory", "understory", "water", "shore_distance"):
        sidecar, grid = layer(world, name)
        if grid is None:
            print("this world has no %s layer under %s" % (name, os.path.join(ROOT, world, "layers")))
            return 2
        got[name] = (sidecar, grid)
    cache = cache_dir(world)
    OWED = OWED_BY_REGION.get(region_of(world), {})
    files = sorted(glob.glob(os.path.join(cache, "*.json")))
    if not files:
        print("no records under %s; run Tools/data/fetch_ala.py first" % cache)
        return 2

    over_side, over = got["overstory"]
    _, under = got["understory"]
    _, water = got["water"]
    _, shore = got["shore_distance"]
    codes = legend(over_side)
    cell, half = over_side["cell_m"], over_side["extent_m"] / 2.0
    lat0, lon0 = over_side["centre_lat"], over_side["centre_lon"]
    land = water != SEA
    has_sea = bool((water == SEA).any())
    land_cells = float(land.sum())
    reach = int(round(NEAR_M / cell))
    yy, xx = np.mgrid[-reach:reach + 1, -reach:reach + 1]
    disc = (yy * yy + xx * xx) <= reach * reach

    failures, owed = [], []

    def expect(key, label, kind, ok, detail):
        name = "%s: %s" % (label, kind)
        debt = OWED.get((key, kind))
        if debt is None:
            print("%-44s %s  %s" % (name, "ok " if ok else "FAIL", detail))
            if not ok:
                failures.append(name)
        elif ok:
            print("%-44s PAID %s; take the row out of OWED and move \"%s\" to Paid" % (name, detail, debt))
            failures.append(name + " (passes, still owed)")
        else:
            print("%-44s owed %s (DEBTS.md: \"%s\")" % (name, detail, debt))
            owed.append(name)

    for path in files:
        doc = json.load(open(path, encoding="utf-8"))
        key = doc["species"]
        if key not in codes:
            print("%-44s note  the world's legend names no %s" % (key, key))
            continue
        grows = ((over == codes[key]) | (under == codes[key])) & land
        share_all = grows.sum() / land_cells
        area_ha = grows.sum() * cell * cell / 10000.0
        shares, found_near, distances = [], 0, []
        for record in doc["records"]:
            if not usable(record):
                continue
            east = EARTH_RADIUS_M * math.radians(record["lon"] - lon0) * math.cos(math.radians(lat0))
            north = EARTH_RADIUS_M * math.radians(record["lat"] - lat0)
            if abs(east) > half or abs(north) > half:
                continue
            r, c = int(round((half - north) / cell)), int(round((east + half) / cell))
            r0, c0 = max(0, r - reach), max(0, c - reach)
            g = grows[r0:r + reach + 1, c0:c + reach + 1]
            l = land[r0:r + reach + 1, c0:c + reach + 1]
            d = disc[r0 - (r - reach):r0 - (r - reach) + g.shape[0], c0 - (c - reach):c0 - (c - reach) + g.shape[1]]
            if (l & d).sum() == 0:
                continue
            shares.append((g & d).sum() / float((l & d).sum()))
            found_near += 1 if (g & d).any() else 0
            distances.append(float(shore[r, c]))
        n = len(shares)
        label = "%s (%s)" % (key, doc.get("binomial", ""))
        if n < MIN_RECORDS:
            print("%-44s note  %d usable record(s) of %d in the box; too few to judge" % (label, n, len(doc["records"])))
            continue
        record_sea = float(np.percentile(distances, 50))
        world_sea = float(np.percentile(shore[grows], 50)) if grows.any() else float("nan")
        if area_ha < TINY_HA:
            expect(key, label, "near its records", found_near >= 0.5 * n,
                   "the world grows it within %.0f m of %d of %d records, and on %.1f ha in all" % (NEAR_M, found_near, n, area_ha))
        else:
            near = float(np.mean(shares))
            ratio = near / share_all if share_all > 0 else float("nan")
            expect(key, label, "round its records", ratio >= MIN_SHARE_RATIO,
                   "%.1f %% of the land within %.0f m of %d records against %.1f %% of all land: a ratio of %.2f against %.2f"
                   % (100 * near, NEAR_M, n, 100 * share_all, ratio, MIN_SHARE_RATIO))
        if not has_sea:
            print("%-44s note  no sea in this box; the distance from it says nothing" % ("%s: how far from the sea" % label))
            continue
        sea_ratio = world_sea / max(1.0, record_sea)
        expect(key, label, "how far from the sea", 1.0 / SEA_RATIO <= sea_ratio <= SEA_RATIO,
               "median %.0f m for the world's plant against %.0f m for its records: %.2f times, within %.1f either way"
               % (world_sea, record_sea, sea_ratio, SEA_RATIO))

    if failures:
        print("species_check: FAIL (%d row(s))" % len(failures))
        return 1
    if owed:
        print("species_check: ok, every judged row passes; %d row(s) owed in DEBTS.md" % len(owed))
    else:
        print("species_check: ok, the world grows its plants where people have found them")
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main(sys.argv))
    except (OSError, ValueError, KeyError, IndexError) as error:
        print("species_check FAILED: %s" % error, file=sys.stderr)
        sys.exit(1)
