#!/usr/bin/env python3
"""tile_check.py: does the client's tile cache hold what the world folder holds?

Reads a client's disk tile cache with Python and zlib alone and compares every cached tile with the world folder's
own layer, cell for cell. It never imports TileCodec, TileGrid or anything else the game runs: the layouts below
are restated from ARCHITECTURE section 10 and section 7, so a writer that drifts from its stated format is caught
by a reader that did not drift with it.

The formats, as section 10 states them:
  - A cache file is <root>/<region>/<layer>/<ix>_<iz>.tile, the layer folder being ground, water-depth,
    water-class or ground-cover. Its first four bytes are the CRC-32 (IEEE, little-endian) of everything after them.
  - Those bytes are a raw deflate stream (no zlib header).
  - Inflated, a layer of metres (ground, water-depth) is posts squared signed 16-bit little-endian centimetres,
    each row its first post absolute and every later post the difference from the one before.
  - A layer of codes (water-class, ground-cover) is posts squared raw bytes.
  - The tile grid is the region's alone: a kilometre tile where the extent divides into kilometres, else the whole
    region as one tile; tile (0, 0) at the south-west corner; a tile's origin is (-extent/2 + ix * size,
    -extent/2 + iz * size) and it has size/cell + 1 posts a side, sharing an edge post with each neighbour.
  - Posts sit on raster cell centres, so a post is a lookup: col = (east + extent/2) / cell, row = (extent/2 -
    north) / cell.
  - The water's surface is not sent; what a client holds is the depth over that tile's own ground, so the surface
    it can put back together is ground + depth.

Rows, each with both numbers:
  1. every cached file's CRC matches its bytes, and inflates to exactly its tile's posts;
  2. the ground: the largest disagreement with the world's heights layer, against half a centimetre, which is
     exactly what rounding to int16 centimetres can cost. The bound carries a nanometre of slack because the
     comparison runs in floating point and a real cache sits precisely on it: the worst post of the first run
     read 0.005000000000002558 m, half a centimetre and two femtometres of arithmetic;
  3. the water's depth: the largest disagreement between ground + depth and the world's surface layer, against
     two centimetres (each of the two was quantised to one);
  4. the water's class and the ground's cover: how many posts disagree with the world's own layers, which must
     be none. Whether the cover layer is itself right is cover_check's row, not this one: this check reads the
     wire and the cache, that one reads the rule;
  5. how many tiles of each layer the cache holds, against the residency bound section 7 states.

Exit 0 when every row passes, 1 when any fails, 2 when the world or the cache is missing.
Run from the repository root:
    python Tools/verifiers/checks/tile_check.py [world folder] [tile cache root]
(defaults Artefacts/worlds/gate and Artefacts/streaming/latest's tiles.)
"""
import glob
import json
import os
import struct
import sys
import zlib

import numpy as np

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))))
DEFAULT_WORLD = os.path.join("Artefacts", "worlds", "gate")
NP_DTYPES = {"u8": "u1", "u16": "<u2", "i16": "<i2", "u32": "<u4", "f32": "<f4"}
GROUND, DEPTH, CLASS, COVER = "ground", "water-depth", "water-class", "ground-cover"
# Half a centimetre is what rounding to int16 centimetres costs; the nanometre is for the float that carries it.
GROUND_TOLERANCE_M = 0.005 + 1e-9
# Twice that, since the client adds two separately rounded numbers to put a surface back together.
DEPTH_TOLERANCE_M = 0.02
# Section 7: at most this many tiles of one layer, and the nine around the founder are never let go.
RESIDENCY = 25


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
    values = np.frombuffer(zlib.decompress(body, -15), dtype="<i2")
    if values.size != posts * posts:
        raise ValueError("%d posts inflated where %d were expected" % (values.size, posts * posts))
    rows = values.reshape(posts, posts).astype(np.int32)
    return np.cumsum(rows, axis=1) / 100.0     # each row: the first post, then the differences


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
    if heights is None or surface is None or water is None:
        print("the world's heights, surface and water layers are needed under %s" % os.path.join(ROOT, world, "layers"))
        return 2
    if not os.path.isdir(cache_root):
        print("no tile cache at %s" % cache_root)
        return 2

    cell = heights_sidecar["cell_m"]
    posts = int(round(tile_size(heights_sidecar["extent_m"]) / cell)) + 1
    failures = []

    def expect(name, ok, detail):
        print("%-44s %s  %s" % (name, "ok " if ok else "FAIL", detail))
        if not ok:
            failures.append(name)

    files = sorted(glob.glob(os.path.join(cache_root, "*", "*", "*.tile")))
    if not files:
        print("no tiles cached under %s" % cache_root)
        return 2

    counts = {GROUND: 0, DEPTH: 0, CLASS: 0, COVER: 0}
    bad_crc, bad_shape = [], []
    worst_ground = (0.0, "")
    worst_depth = (0.0, "")
    class_wrong = 0
    cover_wrong = 0
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
            if folder in (CLASS, COVER):
                held = unpack_codes(body, posts)
                if folder == COVER and cover is None:
                    bad_shape.append("%s in %s: this world has no cover layer to check it against" % (name, folder))
                    continue
                truth = world_block(water if folder == CLASS else cover,
                                    water_sidecar if folder == CLASS else cover_sidecar, ix, iz, posts)
                wrong = int((held != truth).sum())
                if folder == CLASS:
                    class_wrong += wrong
                else:
                    cover_wrong += wrong
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
           "%d files, %s posts each, %s cells compared; %s" % (len(files), posts, "{:,}".format(compared),
                                                               "; ".join(bad_crc + bad_shape) or "none refused"))
    expect("the ground matches the world's heights", counts[GROUND] > 0 and worst_ground[0] <= GROUND_TOLERANCE_M,
           "largest disagreement %.6f m at %s; rounding to int16 centimetres costs %.6f" % (worst_ground[0], worst_ground[1] or "no ground tile", GROUND_TOLERANCE_M))
    expect("ground plus depth matches the world's surface", counts[DEPTH] > 0 and worst_depth[0] <= DEPTH_TOLERANCE_M,
           "largest disagreement %.6f m at %s; two roundings of a centimetre allow %.2f" % (worst_depth[0], worst_depth[1] or "no depth tile", DEPTH_TOLERANCE_M))
    expect("the water's class matches post for post", counts[CLASS] > 0 and class_wrong == 0,
           "%d posts disagree of %d class tiles" % (class_wrong, counts[CLASS]))
    expect("the ground cover matches post for post", counts[COVER] > 0 and cover_wrong == 0,
           "%d posts disagree of %d cover tiles" % (cover_wrong, counts[COVER]))
    expect("the cache is inside the residency bound", all(n <= RESIDENCY for n in counts.values()),
           "ground %d, depth %d, class %d, cover %d held; the bound is %d each"
           % (counts[GROUND], counts[DEPTH], counts[CLASS], counts[COVER], RESIDENCY))

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
