#!/usr/bin/env python3
"""cover_check.py: does the world's ground cover follow from the world's other layers?

The cover layer is the one thing the client is told about how the ground looks (M1.4d). This check recomputes it
from the layers it is made of — the water class, the topology, the plant community, the soil's depth and its
wetness — by the cascade restated below from the contract, in numpy, importing nothing the game runs. A cover
that agreed with the engine because it called the engine would be a stub with extra steps.

The cascade, as `Docs/contracts/M1.4_GROUND_COLOUR.md` promise 1 states it, first match winning:

    salt water                        -> sea
    lake, stream or creek             -> fresh water
    topology wetland                  -> swamp floor
    topology beach                    -> sand
    topology dune                     -> dune sand
    topology cliff or shore platform  -> rock
    soil not deeper than 5 cm         -> rock
    understory shrub                  -> heath
    understory bracken                -> bracken
    understory herb                   -> sedge
    understory grass                  -> grass
    a canopy with nothing under it    -> forest floor
    otherwise                         -> bare earth

and the byte is that cover in the low six bits with the quarter of the land's own wetness (0 to 1, quartered at
0.25, 0.5 and 0.75) in the top two.

The plants' forms are not in any layer: the sidecar's legend names the species and this file holds the form of
each, from `Docs/ECOSYSTEM.md` and the field guides it cites. A species the table does not know fails the check
rather than being guessed at, which is what should happen when the species list grows and nobody told the ground.

A post the layers cannot decide is not counted against either side. The cascade turns on two thresholds — soil
deeper than 5 cm, and the quarters of wetness at 0.25, 0.50 and 0.75 — and the engine read the true numbers while
this check reads the record of them, which is stored to the centimetre and to one part in 255. A post whose
stored value sits within half a step of a threshold could honestly be on either side, so it is counted and named
rather than failed: on the first run of this check they were 17 posts of four million at exactly 5 cm of soil,
and 5,731 within half a step of a wetness cut (2026-09-10). Everything else must agree exactly.

Rows, each with both numbers:
  1. every post's cover matches the cascade, but for those the stored soil depth cannot decide;
  2. every post's wetness quarter matches the wetness layer quartered, but for those on a cut;
  3. how many posts the record cannot decide, against what the storage steps predict: three wetness cuts a step
     wide, doubled for country that is not evenly spread. A field that piled up on the cuts would fail here;
  4. the shares by cover, printed as the census prints country, with a floor: a world where nothing grows or
     nothing is sand has a defect upstream, so at least six covers must appear and none may take the whole box.

Exit 0 when every row passes, 1 when any fails, 2 when the world is missing a layer this needs.
Run from the repository root:
    python Tools/verifiers/checks/cover_check.py [world folder]
(default Artefacts/worlds/gate.)
"""
import json
import os
import sys

import numpy as np

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))))
DEFAULT_WORLD = os.path.join("Artefacts", "worlds", "gate")
NP_DTYPES = {"u8": "u1", "u16": "<u2", "i16": "<i2", "u32": "<u4", "f32": "<f4"}

# The covers, by the number the wire carries (Engine GroundCover; never renumbered).
SEA, FRESH, SAND, DUNE, ROCK, BARE, HEATH, BRACKEN, SEDGE, GRASS, LITTER, SWAMP = range(1, 13)
NAMES = {0: "unknown", SEA: "sea", FRESH: "fresh water", SAND: "sand", DUNE: "dune sand", ROCK: "rock",
         BARE: "bare earth", HEATH: "heath", BRACKEN: "bracken", SEDGE: "sedge", GRASS: "grass",
         LITTER: "forest floor", SWAMP: "swamp floor"}

# The topology bits, as the topology layer's own legend states them.
T_WETLAND, T_BEACH, T_DUNE, T_CLIFF, T_PLATFORM = 8, 2, 4, 128, 256
# The water classes, as the water layer's own legend states them.
W_CREEK, W_STREAM, W_LAKE, W_SEA = 3, 4, 5, 7
# Soil this thin holds nothing (the contract's 5 cm).
BARE_SOIL_M = 0.05
QUARTERS = 4

# What shape each plant is. Not in any layer: from ECOSYSTEM.md and the guides it cites.
FORMS = {
    "Blackbutt": "tree", "Bangalay": "tree", "OldManBanksia": "smalltree", "CoastBanksia": "smalltree",
    "SwampPaperbark": "smalltree", "GrassTree": "shrub", "HeathBanksia": "shrub", "Bracken": "bracken",
    "Lomandra": "herb", "SawSedge": "herb", "KangarooGrass": "grass", "Spinifex": "grass",
}
FORM_COVER = {"shrub": HEATH, "bracken": BRACKEN, "herb": SEDGE, "grass": GRASS}


def layer(world, name):
    path = os.path.join(ROOT, world, "layers", name + ".json")
    if not os.path.isfile(path):
        return None, None
    sidecar = json.load(open(path, encoding="utf-8"))
    raw = os.path.join(os.path.dirname(path), sidecar["raw"])
    grid = np.fromfile(raw, dtype=NP_DTYPES[sidecar["dtype"]]).reshape(sidecar["height"], sidecar["width"])
    return sidecar, grid


def species_forms(sidecar):
    """The legend names the species by index; this returns the form of each, refusing one it does not know."""
    source = sidecar.get("source", "")
    listed = source.split(":", 1)[1] if ":" in source else ""
    forms = {}
    for part in listed.split(","):
        part = part.strip()
        if "=" not in part:
            continue
        number, name = part.split("=", 1)
        name = name.strip()
        if name not in FORMS:
            raise KeyError("this check does not know what shape a %s is; add it from ECOSYSTEM.md" % name)
        forms[int(number)] = FORMS[name]
    if not forms:
        raise KeyError("the understory layer's legend names no species")
    return forms


def main(argv):
    world = argv[1] if len(argv) > 1 else DEFAULT_WORLD
    wanted = ["cover", "water", "topology", "understory", "overstory", "soil_depth", "wetness"]
    got = {}
    for name in wanted:
        sidecar, grid = layer(world, name)
        if grid is None:
            print("this world has no %s layer; %s is needed under %s" % (name, ", ".join(wanted), os.path.join(ROOT, world, "layers")))
            return 2
        got[name] = (sidecar, grid)

    cover_stated = got["cover"][1]
    water = got["water"][1]
    topo = got["topology"][1]
    under = got["understory"][1]
    over = got["overstory"][1]
    soil = got["soil_depth"][1].astype(np.float64) * got["soil_depth"][0]["scale"]
    wet = got["wetness"][1].astype(np.float64) * got["wetness"][0]["scale"]
    forms = species_forms(got["understory"][0])

    # The cascade, first match winning: written as "fill what is still unset", which is the same order read down.
    computed = np.zeros(cover_stated.shape, np.uint8)

    def put(mask, value):
        np.copyto(computed, np.uint8(value), where=(computed == 0) & mask)

    put(water == W_SEA, SEA)
    put(np.isin(water, [W_LAKE, W_STREAM, W_CREEK]), FRESH)
    put((topo & T_WETLAND) > 0, SWAMP)
    put((topo & T_BEACH) > 0, SAND)
    put((topo & T_DUNE) > 0, DUNE)
    put((topo & (T_CLIFF | T_PLATFORM)) > 0, ROCK)
    put(~(soil > BARE_SOIL_M), ROCK)
    for form, cover in FORM_COVER.items():
        put(np.isin(under, [i for i, f in forms.items() if f == form]), cover)
    put(over > 0, LITTER)
    put(computed == 0, BARE)

    quarter_stated = cover_stated >> 6
    quarter = np.clip((wet * QUARTERS).astype(np.int64), 0, QUARTERS - 1).astype(np.uint8)

    # What the record cannot decide: the stored value sits within half its own step of the threshold.
    soil_step = got["soil_depth"][0]["scale"]
    wet_step = got["wetness"][0]["scale"]
    soil_undecidable = np.abs(soil - BARE_SOIL_M) <= soil_step / 2 + 1e-12
    cuts = np.arange(1, QUARTERS) / float(QUARTERS)
    wet_undecidable = np.zeros(wet.shape, bool)
    for cut in cuts:
        wet_undecidable |= np.abs(wet - cut) <= wet_step / 2 + 1e-12

    failures = []

    def expect(name, ok, detail):
        print("%-44s %s  %s" % (name, "ok " if ok else "FAIL", detail))
        if not ok:
            failures.append(name)

    stated = cover_stated & 0x3F
    wrong = (stated != computed) & ~soil_undecidable
    count = int(wrong.sum())
    worst = ""
    if count:
        rows, cols = np.nonzero(wrong)
        r, c = int(rows[0]), int(cols[0])
        worst = "; first at row %d col %d: the world says %s and the cascade says %s" % (
            r, c, NAMES.get(int(stated[r, c]), "?"), NAMES.get(int(computed[r, c]), "?"))
    on_the_edge = int(((stated != computed) & soil_undecidable).sum())
    expect("every post's cover follows the cascade", count == 0,
           "%d of %s posts disagree%s; %d more sit within half a centimetre of the 5 cm the cascade turns on, which the stored soil cannot decide"
           % (count, "{:,}".format(stated.size), worst, on_the_edge))

    quarter_wrong = int(((quarter_stated != quarter) & ~wet_undecidable).sum())
    quarter_edge = int(((quarter_stated != quarter) & wet_undecidable).sum())
    expect("every post's wetness quarter follows the layer", quarter_wrong == 0,
           "%d of %s posts disagree; %d more sit within half a step (%.6f) of a cut at 0.25, 0.50 or 0.75"
           % (quarter_wrong, "{:,}".format(quarter.size), quarter_edge, wet_step / 2))

    # How many posts a threshold can swallow is arithmetic, not taste: three cuts, each a whole storage step
    # wide, over a wetness scaled to the land's own percentiles. Twice that leaves room for country that is not
    # evenly spread, and still catches a wetness field that piles up on the cuts.
    undecidable = int(soil_undecidable.sum()) + int(wet_undecidable.sum())
    share = undecidable / float(stated.size)
    allowed = 2.0 * (QUARTERS - 1) * wet_step + 0.001
    expect("what the layers cannot decide stays small", share <= allowed,
           "%s posts of %s, %.3f %%, against %.3f %%: three cuts a storage step wide (%.5f each), doubled, and a thousandth for the soil"
           % ("{:,}".format(undecidable), "{:,}".format(stated.size), 100.0 * share, 100.0 * allowed, wet_step))

    cell_m = got["cover"][0]["cell_m"]
    present, biggest, biggest_name = 0, 0.0, ""
    lines = []
    for value, name in sorted(NAMES.items()):
        n = int((stated == value).sum())
        if not n:
            continue
        present += 1
        share = n / float(stated.size)
        if share > biggest:
            biggest, biggest_name = share, name
        lines.append("    %-14s %6.2f %%  %9.1f ha" % (name, 100.0 * share, n * cell_m * cell_m / 10000.0))
    expect("the country is made of more than one thing", present >= 6 and biggest < 0.60,
           "%d covers present, the largest %s at %.1f %%; fewer than 6 or more than 60 %% is a defect upstream"
           % (present, biggest_name, 100.0 * biggest))
    print("  the region, by cover:")
    for line in lines:
        print(line)

    if failures:
        print("cover_check: FAIL (%s)" % ", ".join(failures))
        return 1
    print("cover_check: ok, %s posts of %s follow the cascade" % ("{:,}".format(stated.size), world))
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main(sys.argv))
    except (OSError, ValueError, KeyError, IndexError) as error:
        print("cover_check FAILED: %s" % error, file=sys.stderr)
        sys.exit(1)
