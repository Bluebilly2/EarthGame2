#!/usr/bin/env python3
"""stand_check.py: do the world's trees stand where its canopy does, as far apart as their crowns allow, and do its
sticks and cobbles lie where they would?

The M1.6a contract (`Docs/contracts/M1.6_WHAT_STANDS_AND_LIES.md`) states the rules; this check restates them in numpy
against the world's other layers — the overstory, the topology, the water and the soil — and imports nothing the game
runs. The stand byte and the loose byte are read by the legends their sidecars print. Each tall plant's crown share and
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
     either side;
  6. stems a hectare of each tall plant's own canopy, inside the design's 50 to 1,500 (a published benchmark by
     diameter class exists, Gibbons and others 2010, and is not yet read: DEBTS.md);
  7. sticks lie only within a crown's reach, and a cell, of a trunk;
  8. cobbles lie only where stone lies loose: never on a dune, the sea, a lake or a swamp, and on deep soil only on a
     shore platform, a cliff, a beach or a creek's or a stream's bed. The soil layer keeps a depth to its own step, so
     a cell within half a step of the thin-soil line is counted thin, and the row says how many cobbled cells were.

Exit 0 when every row passes, 1 when any fails, 2 when the world is missing a layer this needs.
Run from the repository root:
    python Tools/verifiers/checks/stand_check.py [world folder]
(default Artefacts/worlds/gate.)
"""
import json
import os
import sys

import numpy as np

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))))
DEFAULT_WORLD = os.path.join("Artefacts", "worlds", "gate")
NP_DTYPES = {"u8": "u1", "u16": "<u2", "i16": "<i2", "u32": "<u4", "f32": "<f4"}

# The tall plants, from ECOSYSTEM.md: the heights each grows to (its table of plants) and the crown's diameter as a
# share of height (its table of what stands).
CROWN_SHARE = {"Blackbutt": 0.35, "Bangalay": 0.45, "OldManBanksia": 0.60, "CoastBanksia": 0.55, "SwampPaperbark": 0.50}
HEIGHTS_M = {"Blackbutt": (20.0, 40.0), "Bangalay": (12.0, 20.0), "OldManBanksia": (4.0, 12.0), "CoastBanksia": (5.0, 15.0), "SwampPaperbark": (3.0, 9.0)}
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


def layer(world, name):
    path = os.path.join(ROOT, world, "layers", name + ".json")
    if not os.path.isfile(path):
        return None, None
    sidecar = json.load(open(path, encoding="utf-8"))
    raw = os.path.join(os.path.dirname(path), sidecar["raw"])
    return sidecar, np.fromfile(raw, dtype=NP_DTYPES[sidecar["dtype"]]).reshape(sidecar["height"], sidecar["width"])


def legend_names(sidecar):
    """'... (1=Blackbutt, 2=Bangalay, ...) ...' -> {1: 'Blackbutt', ...}, from the first parenthesised list."""
    text = sidecar.get("source", "")
    names = {}
    for part in text.replace("(", ",").replace(")", ",").replace(":", ",").split(","):
        if "=" in part:
            number, name = part.strip().split("=", 1)
            if number.strip().isdigit():
                names.setdefault(int(number), name.strip().split(" ")[0])
    return names


def height_step(sidecar):
    """The height step the stand legend states ('... steps of 1.25 m')."""
    text = sidecar.get("source", "")
    marker = "steps of "
    at = text.find(marker)
    if at < 0:
        raise ValueError("the stand layer's legend states no height step")
    return float(text[at + len(marker):].split(" ")[0])


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

    species_code = stand >> 5
    heights = (stand & 0x1F).astype(np.float64) * step
    trunk = stand > 0
    names = np.array([""] + [tall_names.get(i, "?") for i in range(1, 8)])[species_code]
    share = np.zeros(stand.shape)
    for name, s in CROWN_SHARE.items():
        share[names == name] = s
    radius = np.where(trunk, 0.5 * share * heights, 0.0)
    over_name = np.array([""] + [over_names.get(i, "?") for i in range(1, int(over.max()) + 1)])[over]
    canopied = over > 0

    failures = []

    def expect(title, ok, detail):
        print("%-50s %s  %s" % (title, "ok " if ok else "FAIL", detail))
        if not ok:
            failures.append(title)

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
    expect("the sand forest stands inside BioNet's 5 to 20 m", sh.size > 0 and inside == sh.size,
           "%d of %d bangalay and coast banksia on sand inside %g-%g m, a step either side%s"
           % (inside, sh.size, SAND_FOREST_M[0], SAND_FOREST_M[1], (", from %.2f to %.2f m" % (sh.min(), sh.max())) if sh.size else ""))

    rows, bad = [], []
    for name in CROWN_SHARE:
        canopy_ha = float((over_name == name).sum()) * cell * cell / 10000.0
        stems = int((trunk & (names == name)).sum())
        if canopy_ha <= 0:
            continue
        per_ha = stems / canopy_ha
        rows.append("%s %.0f" % (name, per_ha))
        if not STEMS_BAND[0] <= per_ha <= STEMS_BAND[1]:
            bad.append(name)
    expect("stems a hectare of each plant's own canopy", not bad,
           ", ".join(rows) + " a hectare; the design's band %g to %g%s" % (STEMS_BAND[0], STEMS_BAND[1], (" (outside: " + ", ".join(bad) + ")") if bad else ""))

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

    if failures:
        print("stand_check: FAIL (%s)" % ", ".join(failures))
        return 1
    print("stand_check: ok, %s trees stand where the canopy does and what lies under them lies where it would" % "{:,}".format(trunks))
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main(sys.argv))
    except (OSError, ValueError, KeyError, IndexError) as error:
        print("stand_check FAILED: %s" % error, file=sys.stderr)
        sys.exit(1)
