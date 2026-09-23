#!/usr/bin/env python3
"""drainage_check.py: does the engine's catchment layer agree with an independent D8 over the raw raster?

Reads the bake's heights (Data/regions/<region>/heights.r32, the region read off the world's own world.json) and the
world's water.u8 and catchment.u32 by hand: numpy from the sidecars' stated shapes, never the engine, never Tools/data. Then does the drainage again
its own way:
  - fills depressions with its own priority flood (heapq over a padded grid), seeded from the sea (raw height at
    or below 0 m), from the grid's edge, and from the cells the world's water layer marks as lake (a lake is a
    sink in the engine too; whether the lakes are where the map puts them is census_check's business, not this
    file's), raising each cell reached to a millimetre above the cell it was reached from;
  - routes every other cell to its steepest-descent neighbour by gradient, drop over distance with the diagonal
    the longer step, vectorised in numpy;
  - accumulates cells in descending order of the filled height, one cell of area each, the sea gathering nothing
    and a lake gathering what flows into it.
The engine does the same job in C#; nothing here reads its code or its filled surface, so two implementations
of one stated method are compared cell for cell where it matters:
  1. the largest creek: the greatest catchment on any land cell that is not a lake, engine and here, with the
     distance between the two cells and this file's greatest catchment within 20 m of the engine's cell;
  2. the second largest creek, at least 600 m from the first, the same way;
  3. the water gathered into the region's published lakes (LAKES, by region: Bherwerre's Windermere and McKenzie; the
     Kangaroo Valley's one mapped lake is an unnamed farm-side pond with no published point, so the valley's table is
     empty and the row prints a note, since WG.2, 2026-09-22): the sum of the catchment over every lake cell
     within reach of each published point (Wikidata: McKenzie Q21908519 at 35.14666 S 150.67079 E to a metre, so
     500 m; Windermere 35 08 S 150 40 E to the minute, so 1500 m), engine and here; a lake's water can lie in
     more than one patch where the bake's ground inside its outline has two hollows, so the cells are summed
     rather than one patch picked.
A row passes when the two numbers are within 10 % of each other: a D8 divide on flat sand moves with the tie
rule between two implementations, and more than a tenth of a catchment would not be that.
  4. the water the creeks and streams carry (2026-09-18, CANON ruling 26 as amended): for every land cell the world's
     water layer classes a creek (3) or a stream (4), the world's surface less the bake's ground against the law
     restated here from Leopold and Maddock (USGS Professional Paper 252, 1953): 0.15 m at the creek's catchment of
     120,000 m2, growing as the catchment to the 0.4, held at 0.8 m, with this file's own catchment; a cell passes
     within 15 % of its depth or a centimetre (the two D8s' divides), and the row holds 99 % of the cells;
  5. no water stands off the channels and the lakes: every cell classed dry, damp, a trickle or swamp has its surface
     at its ground, within a millimetre, and the sea's at the datum.

Exit 0 when every row passes, 1 when any fails, 2 when a raster is missing.
Run from the repository root:  python Tools/verifiers/checks/drainage_check.py [world folder]
(default Artefacts/worlds/gate, the folder Tools/world/create.py leaves behind).
"""
import heapq
import json
from array import array
import math
import os
import sys
import time

import numpy as np

# The water a channel carries (WorldLayers.ChannelDepthM, restated): Leopold and Maddock's downstream depth exponent, the
# game's ankle-deep creek at its catchment and its waist-deep cap; the tolerances carry the two D8s' divides.
CREEK_M2 = 120_000.0
CREEK_DEPTH_M = 0.15
CHANNEL_DEPTH_EXPONENT = 0.4
CHANNEL_DEPTH_MAX_M = 0.8
CHANNEL_DEPTH_TOLERANCE = 0.15
CHANNEL_DEPTH_FLOOR_M = 0.01
CHANNEL_SHARE = 0.99

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))))
DEFAULT_WORLD = os.path.join("Artefacts", "worlds", "gate")
EARTH_RADIUS_M = 6371000.0
FILL_STEP_M = 1e-3
TOLERANCE = 0.10
LAKES_BY_REGION = {
    "bherwerre": (("Lake Windermere", -35.13333333, 150.66666667, 1500.0, "Wikidata Q23759865, to the minute"),
                  ("Lake McKenzie", -35.14666, 150.67079, 500.0, "Wikidata Q21908519, to a metre")),
    "kangaroo-valley": (),
    "kangaroo-valley-whole": (),   # WG.2b: the dams' lakes are left out as humanity's, and OpenStreetMap names no other in the box
}
NP_DTYPES = {"u8": "u1", "u16": "<u2", "i16": "<i2", "u32": "<u4", "f32": "<f4"}


def region_dir(world):
    """The bake the world was made from: Data/regions/<region>, the region read off the world's own world.json (a world
    owns which piece of the Earth it is; Bherwerre when the file does not say, as the worlds before WG.2 were)."""
    world_json = os.path.join(ROOT, world, "world.json")
    region = "bherwerre"
    if os.path.isfile(world_json):
        region = json.load(open(world_json, encoding="utf-8")).get("region", region)
    return os.path.join(ROOT, "Data", "regions", region)


def load(sidecar_path):
    if not os.path.isfile(sidecar_path):
        return None, None
    sidecar = json.load(open(sidecar_path, encoding="utf-8"))
    raw = os.path.join(os.path.dirname(sidecar_path), sidecar.get("raw", sidecar["name"] + ".r32"))
    if not os.path.isfile(raw):
        return None, None
    grid = np.fromfile(raw, dtype=NP_DTYPES[sidecar.get("dtype", "f32")]).reshape(sidecar["height"], sidecar["width"])
    return sidecar, grid


def local(lat, lon, sidecar):
    north = math.radians(lat - sidecar["centre_lat"]) * EARTH_RADIUS_M
    east = math.radians(lon - sidecar["centre_lon"]) * EARTH_RADIUS_M * math.cos(math.radians(sidecar["centre_lat"]))
    return east, north


def position(row, col, sidecar):
    half = sidecar["extent_m"] / 2.0
    return col * sidecar["cell_m"] - half, half - row * sidecar["cell_m"]


def fill(z, sinks):
    """Priority flood on a grid padded by one closed ring, so no neighbour needs a bounds check. The grid is held as a flat
    array of doubles (8 bytes a cell) rather than a list of Python floats (about 32): the whole valley's 64 million cells
    (WG.2b, 2026-09-23) would have taken over 2 GB as a list, and the same arithmetic runs on either."""
    h, w = z.shape
    wp = w + 2
    padded = np.full((h + 2, wp), np.inf, dtype=np.float64)
    padded[1:-1, 1:-1] = z
    filled = array("d", padded.ravel().tobytes())
    del padded
    closed = bytearray(len(filled))
    ring = np.ones((h + 2, wp), dtype=bool)
    ring[1:-1, 1:-1] = False
    for i in np.flatnonzero(ring.ravel()):
        closed[i] = 1
    seeds = np.zeros((h + 2, wp), dtype=bool)
    seeds[1:-1, 1:-1] = sinks
    seeds[1, 1:-1] = True
    seeds[-2, 1:-1] = True
    seeds[1:-1, 1] = True
    seeds[1:-1, -2] = True
    heap = []
    for i in np.flatnonzero(seeds.ravel()):
        i = int(i)
        closed[i] = 1
        heap.append((filled[i], i))
    heapq.heapify(heap)
    offsets = (1, -1, wp, -wp, wp + 1, wp - 1, -wp + 1, -wp - 1)
    push, pop = heapq.heappush, heapq.heappop
    while heap:
        level, i = pop(heap)
        floor = level + FILL_STEP_M
        for off in offsets:
            n = i + off
            if closed[n]:
                continue
            closed[n] = 1
            if filled[n] < floor:
                filled[n] = floor
            push(heap, (filled[n], n))
    return np.frombuffer(filled, dtype=np.float64).reshape(h + 2, wp)[1:-1, 1:-1].copy()


def receivers(filled, sinks, cell_m):
    """Steepest descent by gradient; -1 for a sink or a cell with no lower neighbour."""
    h, w = filled.shape
    padded = np.full((h + 2, w + 2), np.inf)
    padded[1:-1, 1:-1] = filled
    steps = ((0, 1, cell_m), (0, -1, cell_m), (1, 0, cell_m), (-1, 0, cell_m),
             (1, 1, cell_m * math.sqrt(2)), (1, -1, cell_m * math.sqrt(2)), (-1, 1, cell_m * math.sqrt(2)), (-1, -1, cell_m * math.sqrt(2)))
    best = np.full((h, w), -1, dtype=np.int64)
    steepest = np.zeros((h, w))
    for dr, dc, dist in steps:
        neighbour = padded[1 + dr:1 + dr + h, 1 + dc:1 + dc + w]
        gradient = (filled - neighbour) / dist
        better = gradient > steepest
        steepest = np.where(better, gradient, steepest)
        target = (np.arange(h)[:, None] + dr) * w + (np.arange(w)[None, :] + dc)
        best = np.where(better, target, best)
    best[sinks] = -1
    return best.ravel()


def accumulate(filled, recv, sea):
    """Each cell's count passed to its receiver, highest cell first. Held as flat arrays of 64-bit integers and a byte a cell
    for the sea rather than Python lists of ints (WG.2b, 2026-09-23: four lists of the whole valley's 64 million cells would
    have been some 8 GB); the order and the sums are the same."""
    order = array("q", np.argsort(-filled.ravel(), kind="stable").astype(np.int64).tobytes())
    acc = array("q", np.ones(filled.size, dtype=np.int64).tobytes())
    recv_flat = array("q", recv.astype(np.int64).tobytes())
    sea_flat = bytearray(sea.ravel().astype(np.uint8).tobytes())
    for i in order:
        r = recv_flat[i]
        if r < 0 or sea_flat[r]:
            continue
        acc[r] += acc[i]
    return np.frombuffer(acc, dtype=np.int64).reshape(filled.shape).copy()


def patches(mask):
    """Connected components (8-neighbour) of a boolean grid, as a label grid and a list of cell index arrays."""
    h, w = mask.shape
    labels = np.zeros((h, w), dtype=np.int32)
    result = []
    flat = mask.ravel()
    lab = labels.ravel()
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
        result.append(np.array(cells))
    return labels, result


def main(argv):
    world = argv[1] if len(argv) > 1 else DEFAULT_WORLD
    layers = os.path.join(ROOT, world, "layers")
    region = region_dir(world)
    lakes = LAKES_BY_REGION.get(os.path.basename(region), ())
    sidecar, z = load(os.path.join(region, "heights.json"))
    _, water = load(os.path.join(layers, "water.json"))
    _, engine = load(os.path.join(layers, "catchment.json"))
    _, surface = load(os.path.join(layers, "surface.json"))
    if z is None or water is None or engine is None or surface is None:
        print("a raster is missing: the bake under %s and the world's water and catchment layers under %s are needed" % (region, layers))
        return 2
    if water.shape != z.shape or engine.shape != z.shape:
        print("the world's layers are %s, the bake %s" % (water.shape, z.shape))
        return 2
    cell = float(sidecar["cell_m"])
    started = time.time()
    sea = z <= 0.0
    lake = water == 5
    sinks = sea | lake
    filled = fill(z.astype(np.float64), sinks)
    recv = receivers(filled, sinks, cell)
    mine = accumulate(filled, recv, sea)
    engine = engine.astype(np.int64)
    print("independent D8 over %dx%d cells in %.1f s" % (z.shape[1], z.shape[0], time.time() - started))
    failures = []

    def expect(name, ok, detail):
        print("%-44s %s  %s" % (name, "ok " if ok else "FAIL", detail))
        if not ok:
            failures.append(name)

    def within(a, b):
        return a > 0 and b > 0 and abs(a - b) <= TOLERANCE * max(a, b)

    # 1 and 2: the largest creeks, by the engine's reckoning, checked here.
    land = ~sea & ~lake
    ranked = np.where(land, engine, 0)
    taken = []
    for rank, label in ((1, "the largest creek"), (2, "the second creek")):
        candidates = ranked.copy()
        for (r0, c0) in taken:
            rr, cc = np.ogrid[0:z.shape[0], 0:z.shape[1]]
            candidates[((rr - r0) ** 2 + (cc - c0) ** 2) * cell * cell < 600.0 ** 2] = 0
        r, c = np.unravel_index(int(candidates.argmax()), z.shape)
        taken.append((r, c))
        e = int(engine[r, c])
        window = mine[max(0, r - 5):r + 6, max(0, c - 5):c + 6]
        m = int(window.max())
        rm, cm = np.unravel_index(int(mine.argmax()), z.shape) if rank == 1 else (r, c)
        east, north = position(r, c, sidecar)
        detail = "engine %s cells at east %.0f north %.0f; here %s within 20 m of it" % ("{:,}".format(e), east, north, "{:,}".format(m))
        if rank == 1:
            de, dn = position(rm, cm, sidecar)
            detail += "; this file's own greatest on land %s cells, %.0f m away" % ("{:,}".format(int(np.where(land, mine, 0).max())), math.hypot(de - east, dn - north))
        expect("%s gathers the same water" % label, within(e, m), detail)

    # 3: the named lakes gather the same water.
    half = sidecar["extent_m"] / 2.0
    rr, cc = np.ogrid[0:z.shape[0], 0:z.shape[1]]
    east_grid = cc * cell - half
    north_grid = half - rr * cell
    if not lakes:
        print("%-44s note  no published lake in this region's table; the row does not apply" % "the named lakes gather the same water")
    for name, lat, lon, radius, source in lakes:
        pe, pn = local(lat, lon, sidecar)
        near = lake & ((east_grid - pe) ** 2 + (north_grid - pn) ** 2 <= radius * radius)
        cells = int(near.sum())
        if cells == 0:
            expect("%s gathers the same water" % name, False, "no lake cell within %.0f m of the point (%s)" % (radius, source))
            continue
        e = int(engine[near].sum())
        m = int(mine[near].sum())
        expect("%s gathers the same water" % name, within(e, m),
               "engine %s cells, here %s, into %d lake cells (%.1f ha) within %.0f m of the point (%s)" % ("{:,}".format(e), "{:,}".format(m), cells, cells * cell * cell / 1e4, radius, source))

    # 4 and 5: the water the channels carry, and none where there is no channel or lake.
    depth = surface.astype(np.float64) - z.astype(np.float64)
    channel = ((water == 3) | (water == 4)) & land
    law = np.minimum(CHANNEL_DEPTH_MAX_M, CREEK_DEPTH_M * np.power(np.maximum(mine, 1) * cell * cell / CREEK_M2, CHANNEL_DEPTH_EXPONENT))
    law = np.where(mine * cell * cell < CREEK_M2, 0.0, law)
    n_channel = int(channel.sum())
    if n_channel == 0:
        expect("creeks and streams carry water by the flow's law", False, "the water layer classes no land cell a creek or a stream")
    else:
        off = np.abs(depth[channel] - law[channel])
        allowed = np.maximum(CHANNEL_DEPTH_TOLERANCE * law[channel], CHANNEL_DEPTH_FLOOR_M)
        held = int((off <= allowed).sum())
        worst = int(off.argmax())
        expect("creeks and streams carry water by the flow's law", held >= CHANNEL_SHARE * n_channel,
               "%d of %d channel cells within 15 %% or 1 cm of the law (at least 99 %%); depths %.2f to %.2f m; the worst %.3f m off where the law says %.3f"
               % (held, n_channel, float(depth[channel].min()), float(depth[channel].max()), float(off[worst]), float(law[channel][worst])))
    no_channel = land & np.isin(water, (0, 1, 2, 6))
    standing = int((np.abs(depth[no_channel]) > 0.001).sum())
    sea_off = int((np.abs(surface[sea & (water == 7)]) > 0.001).sum())
    expect("no water stands off the channels and the lakes", standing == 0 and sea_off == 0,
           "%d of %d dry, damp, trickle or swamp cells with a surface off the ground (must be 0); %d sea cells off the datum (must be 0)" % (standing, int(no_channel.sum()), sea_off))

    if failures:
        print("drainage_check: FAIL (%s)" % ", ".join(failures))
        return 1
    print("drainage_check: ok, the engine's catchment and an independent D8 agree at the creeks and the lakes")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
