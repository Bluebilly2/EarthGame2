#!/usr/bin/env python3
"""tile_check.py: does the client's tile cache hold what the world folder holds?

Reads a client's disk tile cache with Python and zlib alone and compares every cached tile with the world folder's
own layer, cell for cell. It never imports TileCodec, TileGrid or anything else the game runs: the layouts below
are restated from ARCHITECTURE section 10 and section 7, so a writer that drifts from its stated format is caught
by a reader that did not drift with it.

The formats, as section 10 states them:
  - A cache file is <root>/<region>/<layer>/<ix>_<iz>.tile, the layer folder being ground, water-depth,
    water-class, ground-cover, stand, loose, far-stand, far-count or stone. Its first four bytes are the CRC-32 (IEEE,
    little-endian) of everything after them.
  - Those bytes are a raw deflate stream (no zlib header).
  - Inflated, a layer of metres (ground, water-depth) is, since tile version 3, posts rows each of a signed 32-bit
    little-endian first post in centimetres and posts - 1 signed 16-bit steps between neighbours (version 2 was posts
    squared signed 16-bit centimetres,
    each row its first post absolute and every later post the difference from the one before.
  - A layer of codes (water-class, ground-cover, stand, loose, far-stand, far-count, stone since BF.1) is posts squared raw bytes.
  - The tile grid is the region's alone: a kilometre tile where the extent divides into kilometres, else the whole
    region as one tile; tile (0, 0) at the south-west corner; a tile's origin is (-extent/2 + ix * size,
    -extent/2 + iz * size) and it has size/cell + 1 posts a side, sharing an edge post with each neighbour.
  - Posts sit on raster cell centres, so a post is a lookup: col = (east + extent/2) / cell, row = (extent/2 -
    north) / cell.
  - The water's surface is not sent; what a client holds is the depth over that tile's own ground, so the surface
    it can put back together is ground + depth.
  - The far layers (M1.6d) have their posts 40 m apart, size/40 + 1 a side, and are worked out from the stand: a far
    post carries the square of stand cells from half a square before its cell to just short of half a square after,
    in rows and in columns. The far count is how many trees stand in the square, to a byte; the far stand is the
    stand code of the tall plant most of them are, the first in the legend's order on a tie, at those trees' mean
    height carried to the nearest step (half to even) and kept between one step and thirty-one; zero where none.

Rows, each with both numbers:
  1. every cached file's CRC matches its bytes, and inflates to exactly its tile's posts;
  2. the ground: the largest disagreement with the world's heights layer, against half a centimetre, which is
     exactly what rounding to int16 centimetres can cost. The bound carries a nanometre of slack because the
     comparison runs in floating point and a real cache sits precisely on it: the worst post of the first run
     read 0.005000000000002558 m, half a centimetre and two femtometres of arithmetic;
  3. the water's depth: the largest disagreement between ground + depth and the world's surface layer, against
     two centimetres (each of the two was quantised to one);
  4. the water's class, the ground's cover, what stands and what lies loose: how many posts disagree with the
     world's own layers, which must be none. Whether a layer is itself right is its own check's row (cover_check,
     stand_check), not this one: this check reads the wire and the cache, those read the rule. A world made before
     the stand and the loose layers has none to hold, and then none may be cached;
  5. the far stand and the far count: how many posts disagree with the squares worked out here. The game works out
     each post's square on its own; this check counts the whole raster at once, every cell into the one square it
     falls in, so a square that overlapped its neighbour or left a cell out would disagree;
  6. the far layers hold the whole region: how many tiles of each are cached against the region's tiles, and the
     trees the cached far counts add up to, every shared edge post once, against the trees the stand holds;
  7. how many tiles of each other layer the cache holds, against the residency bound section 7 states. The far
     layers are the whole region's and are not bound.

Exit 0 when every row passes, 1 when any fails, 2 when the world or the cache is missing.
Run from the repository root:
    python Tools/verifiers/checks/tile_check.py [world folder] [tile cache root]
(defaults Artefacts/worlds/gate and Artefacts/streaming/latest's tiles.)
"""
import glob
import json
import os
import re
import struct
import sys
import zlib

import numpy as np

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))))
DEFAULT_WORLD = os.path.join("Artefacts", "worlds", "gate")
NP_DTYPES = {"u8": "u1", "u16": "<u2", "i16": "<i2", "u32": "<u4", "f32": "<f4"}
GROUND, DEPTH, CLASS, COVER = "ground", "water-depth", "water-class", "ground-cover"
STAND, LOOSE, STONE = "stand", "loose", "stone"
FAR_STAND, FAR_COUNT = "far-stand", "far-count"
# Half a centimetre is what rounding to int16 centimetres costs; the nanometre is for the float that carries it.
GROUND_TOLERANCE_M = 0.005 + 1e-9
# Twice that, since the client adds two separately rounded numbers to put a surface back together.
DEPTH_TOLERANCE_M = 0.02
# Section 7: at most this many tiles of one layer, and the nine around the founder are never let go.
RESIDENCY = 25
# Section 10: a far layer's posts stand this far apart.
FAR_CELL_M = 40.0
# Section 10: a stand code's low five bits are its height in steps of this.
HEIGHT_STEP_M = 1.25


def layer(world, name):
    sidecar_path = os.path.join(ROOT, world, "layers", name + ".json")
    if not os.path.isfile(sidecar_path):
        return None, None
    sidecar = json.load(open(sidecar_path, encoding="utf-8"))
    raw = os.path.join(os.path.dirname(sidecar_path), sidecar.get("raw", name + ".r32"))
    grid = np.fromfile(raw, dtype=NP_DTYPES[sidecar.get("dtype", "f32")]).reshape(sidecar["height"], sidecar["width"])
    return sidecar, grid


def tile_size(extent_m):
    """The grid rule, restated: kilometre tiles when the region divides into kilometres, else one tile."""
    tiles = extent_m / 1000.0
    return 1000.0 if abs(tiles - round(tiles)) < 1e-9 and tiles >= 1.0 else extent_m


def unpack_metres(body, posts):
    """A metres layer since tile version 3 (WG.2): each row a 32-bit first post in centimetres, then 16-bit steps between neighbours."""
    raw = zlib.decompress(body, -15)
    row_bytes = 4 + 2 * (posts - 1)
    if len(raw) != posts * row_bytes:
        raise ValueError("%d bytes inflated where %d were expected for %d posts a side" % (len(raw), posts * row_bytes, posts))
    rows = np.frombuffer(raw, dtype="u1").reshape(posts, row_bytes)
    first = rows[:, :4].copy().view("<i4").reshape(posts, 1).astype(np.int64)
    steps = rows[:, 4:].copy().view("<i2").reshape(posts, posts - 1).astype(np.int64)
    return np.concatenate([first, first + np.cumsum(steps, axis=1)], axis=1) / 100.0


def unpack_codes(body, posts):
    values = np.frombuffer(zlib.decompress(body, -15), dtype="u1")
    if values.size != posts * posts:
        raise ValueError("%d posts inflated where %d were expected" % (values.size, posts * posts))
    return values.reshape(posts, posts)


def world_block(grid, sidecar, ix, iz, posts):
    """The world's own cells under a tile's posts. Row 0 of a tile is its south edge; row 0 of a raster is north."""
    half, cell = sidecar["extent_m"] / 2.0, sidecar["cell_m"]
    size = tile_size(sidecar["extent_m"])
    origin_east, origin_north = -half + ix * size, -half + iz * size
    col0 = int(round((origin_east + half) / cell))
    row0 = int(round((half - origin_north) / cell))          # the tile's southern row in the raster
    block = grid[row0 - posts + 1:row0 + 1, col0:col0 + posts]
    if block.shape != (posts, posts):
        raise ValueError("tile %d,%d needs %d posts and the raster offers %s" % (ix, iz, posts, block.shape))
    return block[::-1, :]                                     # north-up raster to south-up tile


def far_squares(stand, sidecar):
    """
    Every far square of the region, worked out over the whole raster at once: each cell is counted into the one square
    (m, k) it falls in, (row + span/2) // span and (col + span/2) // span, whose post stands on cell (m * span, k * span).
    Returns the span, the far stand and far count of every square (north to south, west to east), and the trees the
    far counts add up to.
    """
    cell = sidecar["cell_m"]
    span = int(round(FAR_CELL_M / cell))
    if span < 1 or abs(span * cell - FAR_CELL_M) > 1e-6:
        raise ValueError("a far square of %g m is not a whole number of the stand's %g m cells" % (FAR_CELL_M, cell))
    tall = max([int(n) for n in re.findall(r"(\d+)=", sidecar.get("source", ""))] or [0])
    if tall < 1:
        raise ValueError("the stand's legend names no tall plant: %r" % sidecar.get("source"))
    codes = stand.astype(np.int64)
    species = codes >> 5
    trees = (codes != 0) & (species >= 1) & (species <= tall)
    before = span // 2
    square_rows = (np.arange(codes.shape[0]) + before) // span
    square_cols = (np.arange(codes.shape[1]) + before) // span
    rows_n, cols_n = int(square_rows[-1]) + 1, int(square_cols[-1]) + 1
    index = square_rows[:, None] * cols_n + square_cols[None, :]
    squares = rows_n * cols_n
    count = np.bincount(index[trees], minlength=squares)
    of_each = np.zeros((tall, squares), dtype=np.int64)
    metres = np.zeros((tall, squares))
    for plant in range(1, tall + 1):
        mine = trees & (species == plant)
        of_each[plant - 1] = np.bincount(index[mine], minlength=squares)
        metres[plant - 1] = np.bincount(index[mine], weights=(codes[mine] & 0x1F) * HEIGHT_STEP_M, minlength=squares)
    most = np.argmax(of_each, axis=0)                         # the first of the commonest, in the legend's order
    every = np.arange(squares)
    mean = metres[most, every] / np.maximum(of_each[most, every], 1)
    step = np.clip(np.rint(mean / HEIGHT_STEP_M), 1, 0x1F).astype(np.int64)
    kept = np.minimum(count, 0xFF)
    tables = {FAR_STAND: np.where(count > 0, ((most + 1) << 5) | step, 0).reshape(rows_n, cols_n),
              FAR_COUNT: kept.reshape(rows_n, cols_n)}
    return span, tables, int(kept.sum())


def far_lookup(sidecar, span, ix, iz, posts):
    """The square each far post of a tile carries, by post row (south first) and post column; a post off a square's own cell is refused."""
    half, cell = sidecar["extent_m"] / 2.0, sidecar["cell_m"]
    size = tile_size(sidecar["extent_m"])
    cols = np.rint((-half + ix * size + np.arange(posts) * FAR_CELL_M + half) / cell).astype(np.int64)
    rows = np.rint((half - (-half + iz * size + np.arange(posts) * FAR_CELL_M)) / cell).astype(np.int64)
    if (cols % span).any() or (rows % span).any() or rows.min() < 0 or cols.max() >= sidecar["width"]:
        raise ValueError("tile %d,%d's far posts do not stand on the squares' own cells" % (ix, iz))
    return rows // span, cols // span


def main(argv):
    world = argv[1] if len(argv) > 1 else DEFAULT_WORLD
    if len(argv) > 2:
        cache_root = argv[2]
    else:
        pointer = os.path.join(ROOT, "Artefacts", "streaming", "latest.txt")
        cache_root = os.path.join(open(pointer, encoding="utf-8").read().strip(), "tiles") if os.path.isfile(pointer) else ""
    heights_sidecar, heights = layer(world, "heights")
    surface_sidecar, surface = layer(world, "surface")
    water_sidecar, water = layer(world, "water")
    cover_sidecar, cover = layer(world, "cover")
    stand_sidecar, stand = layer(world, "stand")
    loose_sidecar, loose = layer(world, "loose")
    stone_sidecar, stone = layer(world, "stone")
    if heights is None or surface is None or water is None:
        print("the world's heights, surface and water layers are needed under %s" % os.path.join(ROOT, world, "layers"))
        return 2
    if not os.path.isdir(cache_root):
        print("no tile cache at %s" % cache_root)
        return 2

    cell = heights_sidecar["cell_m"]
    size = tile_size(heights_sidecar["extent_m"])
    posts = int(round(size / cell)) + 1
    far_posts = int(round(size / FAR_CELL_M)) + 1
    tiles_per_side = int(round(heights_sidecar["extent_m"] / size))
    failures = []

    def expect(name, ok, detail):
        print("%-44s %s  %s" % (name, "ok " if ok else "FAIL", detail))
        if not ok:
            failures.append(name)

    files = sorted(glob.glob(os.path.join(cache_root, "*", "*", "*.tile")))
    if not files:
        print("no tiles cached under %s" % cache_root)
        return 2

    counts = {GROUND: 0, DEPTH: 0, CLASS: 0, COVER: 0, STAND: 0, LOOSE: 0, FAR_STAND: 0, FAR_COUNT: 0, STONE: 0}
    codes = {CLASS: (water, water_sidecar), COVER: (cover, cover_sidecar), STAND: (stand, stand_sidecar), LOOSE: (loose, loose_sidecar), STONE: (stone, stone_sidecar)}
    wrong_codes = {CLASS: 0, COVER: 0, STAND: 0, LOOSE: 0, FAR_STAND: 0, FAR_COUNT: 0, STONE: 0}
    span, far_tables, stand_trees = far_squares(stand, stand_sidecar) if stand is not None else (0, None, 0)
    far_trees = 0
    bad_crc, bad_shape = [], []
    worst_ground = (0.0, "")
    worst_depth = (0.0, "")
    compared = 0
    for path in files:
        name = os.path.basename(path)
        folder = os.path.basename(os.path.dirname(path))
        ix, iz = (int(part) for part in name[:-len(".tile")].split("_"))
        data = open(path, "rb").read()
        (stated,) = struct.unpack_from("<I", data, 0)
        body = data[4:]
        if zlib.crc32(body) & 0xFFFFFFFF != stated:
            bad_crc.append(name + " in " + folder)
            continue
        if folder not in counts:
            bad_shape.append("%s is not a layer this check knows" % folder)
            continue
        counts[folder] += 1
        try:
            if folder in (FAR_STAND, FAR_COUNT):
                held = unpack_codes(body, far_posts)
                if far_tables is None:
                    bad_shape.append("%s in %s: this world has no stand layer to work a far layer out of" % (name, folder))
                    continue
                rows, cols = far_lookup(stand_sidecar, span, ix, iz, far_posts)
                wrong_codes[folder] += int((held != far_tables[folder][np.ix_(rows, cols)]).sum())
                compared += held.size
                if folder == FAR_COUNT:
                    # A tile's last row and column of posts are its neighbour's first, but at the region's own edge.
                    last_x = far_posts if ix == tiles_per_side - 1 else far_posts - 1
                    last_z = far_posts if iz == tiles_per_side - 1 else far_posts - 1
                    far_trees += int(held[:last_z, :last_x].astype(np.int64).sum())
            elif folder in codes:
                held = unpack_codes(body, posts)
                grid, sidecar = codes[folder]
                if grid is None:
                    bad_shape.append("%s in %s: this world has no %s layer to check it against" % (name, folder, folder))
                    continue
                wrong_codes[folder] += int((held != world_block(grid, sidecar, ix, iz, posts)).sum())
                compared += held.size
            else:
                held = unpack_metres(body, posts)
                ground = world_block(heights, heights_sidecar, ix, iz, posts)
                compared += held.size
                if folder == GROUND:
                    off = float(np.abs(held - ground).max())
                    if off > worst_ground[0]:
                        worst_ground = (off, "%s tile %d,%d" % (folder, ix, iz))
                else:
                    off = float(np.abs((np.round(ground * 100) / 100 + held) - world_block(surface, surface_sidecar, ix, iz, posts)).max())
                    if off > worst_depth[0]:
                        worst_depth = (off, "%s tile %d,%d" % (folder, ix, iz))
        except ValueError as error:
            bad_shape.append("%s in %s: %s" % (name, folder, error))

    expect("every cached tile's CRC and shape hold", not bad_crc and not bad_shape,
           "%d files, %s posts each and %s a far layer's, %s cells compared; %s"
           % (len(files), posts, far_posts, "{:,}".format(compared), "; ".join(bad_crc + bad_shape) or "none refused"))
    expect("the ground matches the world's heights", counts[GROUND] > 0 and worst_ground[0] <= GROUND_TOLERANCE_M,
           "largest disagreement %.6f m at %s; rounding to int16 centimetres costs %.6f" % (worst_ground[0], worst_ground[1] or "no ground tile", GROUND_TOLERANCE_M))
    expect("ground plus depth matches the world's surface", counts[DEPTH] > 0 and worst_depth[0] <= DEPTH_TOLERANCE_M,
           "largest disagreement %.6f m at %s; two roundings of a centimetre allow %.2f" % (worst_depth[0], worst_depth[1] or "no depth tile", DEPTH_TOLERANCE_M))
    expect("the water's class matches post for post", counts[CLASS] > 0 and wrong_codes[CLASS] == 0,
           "%d posts disagree of %d class tiles" % (wrong_codes[CLASS], counts[CLASS]))
    expect("the ground cover matches post for post", counts[COVER] > 0 and wrong_codes[COVER] == 0,
           "%d posts disagree of %d cover tiles" % (wrong_codes[COVER], counts[COVER]))
    for folder, what in ((STAND, "what stands"), (LOOSE, "what lies loose"), (STONE, "the stone (BF.1)")):
        if codes[folder][0] is None:
            expect(what + " matches post for post", counts[folder] == 0,
                   "this world has no %s layer, and %d %s tiles are cached" % (folder, counts[folder], folder))
        else:
            expect(what + " matches post for post", counts[folder] > 0 and wrong_codes[folder] == 0,
                   "%d posts disagree of %d %s tiles" % (wrong_codes[folder], counts[folder], folder))
    whole = tiles_per_side * tiles_per_side
    if far_tables is None:
        expect("no far layer without a stand", counts[FAR_STAND] + counts[FAR_COUNT] == 0,
               "this world has no stand layer, and %d far-stand and %d far-count tiles are cached" % (counts[FAR_STAND], counts[FAR_COUNT]))
    else:
        for folder, what in ((FAR_STAND, "the far stand"), (FAR_COUNT, "the far count")):
            expect(what + " matches its squares post for post", counts[folder] > 0 and wrong_codes[folder] == 0,
                   "%d posts disagree of %d %s tiles, against squares of %d stand cells a side counted over the whole raster"
                   % (wrong_codes[folder], counts[folder], folder, span))
        expect("the far layers hold the whole region",
               counts[FAR_STAND] == whole and counts[FAR_COUNT] == whole and far_trees == stand_trees,
               "far stand %d and far count %d of the region's %d tiles; the cached far counts add up to %s trees, the stand holds %s"
               % (counts[FAR_STAND], counts[FAR_COUNT], whole, "{:,}".format(far_trees), "{:,}".format(stand_trees)))
    near = (GROUND, DEPTH, CLASS, COVER, STAND, LOOSE)
    expect("the cache is inside the residency bound", all(counts[folder] <= RESIDENCY for folder in near),
           "ground %d, depth %d, class %d, cover %d, stand %d, loose %d held; the bound is %d each, and the far layers are the whole region's"
           % (counts[GROUND], counts[DEPTH], counts[CLASS], counts[COVER], counts[STAND], counts[LOOSE], RESIDENCY))

    if failures:
        print("tile_check: FAIL (%s)" % ", ".join(failures))
        return 1
    print("tile_check: ok, %s cached posts agree with %s" % ("{:,}".format(compared), world))
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main(sys.argv))
    except (OSError, ValueError, KeyError, IndexError, zlib.error) as error:
        print("tile_check FAILED: %s" % error, file=sys.stderr)
        sys.exit(1)
