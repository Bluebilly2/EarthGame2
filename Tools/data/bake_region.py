#!/usr/bin/env python3
"""Bake a region's heights raster from cached Terrarium tiles (Claude's tool).

    python Tools/data/bake_region.py --zoom 14 --cell-m 4 [--name heights] [--centre-lat ... --centre-lon ... --extent-m ...]

Writes Data/regions/<region>/<name>.r32 (float32 little-endian metres, row 0 = the NORTH edge, column 0 = the
WEST edge, row-major) and <name>.json, the self-describing sidecar in the RegionRaster header form the engine's
loader reads (ARCHITECTURE.md §3). Sampling is bilinear from the Mercator mosaic at the lon/lat of every cell's
centre, so a cell is the surface at that place, not a nearest-pixel copy.

Refusals (exit 1, nothing written): any tile in the range missing from the cache; a mosaic with no elevation
variation; a raster whose min is below -11,000 m or max above 9,000 m; a sea fraction of 100% (the box missed the
land) or 0% for a region that declares a coast.

The correctness of the output is NOT this tool's claim to make: the owner's verifiers (summit_check.py,
region_stats.py) say whether a named summit and the sea are where they should be. This file only refuses what
it can prove is nonsense.
"""
import argparse
import math
import os
import sys
import time

import numpy as np
from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import raster_io  # noqa: E402
import terrarium  # noqa: E402

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
CACHE = os.path.join(ROOT, "Data", "cache", "terrarium")


def load_mosaic(zoom, x0, y0, x1, y1):
    """One float32 array covering the tile range, plus the list of tiles that were missing."""
    w = (x1 - x0 + 1) * terrarium.TILE_PX
    h = (y1 - y0 + 1) * terrarium.TILE_PX
    mosaic = np.full((h, w), np.nan, dtype=np.float32)
    missing = []
    for x in range(x0, x1 + 1):
        for y in range(y0, y1 + 1):
            path = os.path.join(CACHE, str(zoom), str(x), "%d.png" % y)
            if not os.path.isfile(path):
                missing.append("%d/%d/%d" % (zoom, x, y))
                continue
            px = np.asarray(Image.open(path).convert("RGB"), dtype=np.float32)
            tile = terrarium.decode_terrarium(px[:, :, 0], px[:, :, 1], px[:, :, 2])
            r0 = (y - y0) * terrarium.TILE_PX
            c0 = (x - x0) * terrarium.TILE_PX
            mosaic[r0:r0 + terrarium.TILE_PX, c0:c0 + terrarium.TILE_PX] = tile
    return mosaic, missing


def bilinear(mosaic, px, py):
    """Sample the mosaic at fractional pixel coordinates (arrays). Edges clamp."""
    h, w = mosaic.shape
    px = np.clip(px, 0.0, w - 1.001)
    py = np.clip(py, 0.0, h - 1.001)
    x0 = np.floor(px).astype(np.int64)
    y0 = np.floor(py).astype(np.int64)
    fx = (px - x0).astype(np.float32)
    fy = (py - y0).astype(np.float32)
    a = mosaic[y0, x0]
    b = mosaic[y0, x0 + 1]
    c = mosaic[y0 + 1, x0]
    d = mosaic[y0 + 1, x0 + 1]
    return (a * (1 - fx) + b * fx) * (1 - fy) + (c * (1 - fx) + d * fx) * fy


#: Rows of the raster worked at once. The median below copies all 49 neighbours of every cell it works on, which for
#: the whole of a 32 km box at 4 m (8001 x 8001) is 12.5 GB; in bands of this many rows it is 0.4 GB, and the
#: answer is the same cell for cell, each cell's window and arithmetic being its own (WG.2b, 2026-09-23).
BAND_ROWS = 256


def despike(heights, threshold_m, band_rows=BAND_ROWS):
    """Cells more than threshold_m away from the median of their neighbourhood, in either direction, take that
    median. Returns (heights, count of replacements over both passes). Pits count as well as spikes: the 4 m region raster held a 1,097 m pit
    three cells wide on lowland near the bay shore (2026-09-08), a single bad tile pixel spread by resampling."""
    from numpy.lib.stride_tricks import sliding_window_view
    total = 0
    out = heights
    # Two passes over a 7x7 window: a bad pixel resampled onto 4 m cells is a pit up to five cells wide, and a
    # single 5x5 pass left its floor in place (min -94 m after the first pass on 2026-09-08). Each pass reads only the
    # pass before it, so a band's windows are the padded rows three above to three below it.
    for _ in range(2):
        padded = np.pad(out, 3, mode="edge")
        result = np.empty_like(out)
        for r0 in range(0, out.shape[0], band_rows):
            r1 = min(r0 + band_rows, out.shape[0])
            med = np.median(sliding_window_view(padded[r0:r1 + 6], (7, 7)), axis=(2, 3)).astype(np.float32)
            band = out[r0:r1]
            spike = np.abs(band - med) > threshold_m
            result[r0:r1] = np.where(spike, med, band)
            total += int(spike.sum())
        out = result
    return out, total


def sample_rows(mosaic, r0, r1, n, cell_m, extent_m, centre_lat, centre_lon, zoom, x0, y0):
    """The heights of raster rows r0 to r1 (row 0 the north edge), bilinear from the Mercator mosaic at the lon/lat of
    each cell's centre on the tangent plane."""
    half = extent_m / 2.0
    east_m = (np.arange(n, dtype=np.float64) * cell_m) - half
    north_m = half - (np.arange(r0, r1, dtype=np.float64) * cell_m)
    east_grid, north_grid = np.meshgrid(east_m, north_m)
    lat = centre_lat + np.degrees(north_grid / terrarium.EARTH_RADIUS_M)
    lon = centre_lon + np.degrees(east_grid / (terrarium.EARTH_RADIUS_M * math.cos(math.radians(centre_lat))))

    # Lon/lat to mosaic pixels (vectorised form of terrarium.lonlat_to_tile_xy).
    scale = 2 ** zoom
    tx = (lon + 180.0) / 360.0 * scale
    latr = np.radians(lat)
    ty = (1.0 - np.log(np.tan(latr) + 1.0 / np.cos(latr)) / math.pi) / 2.0 * scale
    px = (tx - x0) * terrarium.TILE_PX - 0.5
    py = (ty - y0) * terrarium.TILE_PX - 0.5
    return bilinear(mosaic, px, py).astype(np.float32)


def main():
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("--zoom", type=int, required=True)
    p.add_argument("--cell-m", type=float, required=True)
    p.add_argument("--name", default="heights")
    p.add_argument("--region", default="bherwerre")
    p.add_argument("--centre-lat", type=float, default=terrarium.BHERWERRE_CENTRE_LAT)
    p.add_argument("--centre-lon", type=float, default=terrarium.BHERWERRE_CENTRE_LON)
    p.add_argument("--extent-m", type=float, default=terrarium.BHERWERRE_EXTENT_M)
    p.add_argument("--coast", action="store_true", default=True, help="the region declares a coast (default)")
    p.add_argument("--inland", action="store_true", help="the region has no coast (WG.2): no cell need lie at or below sea level")
    p.add_argument("--despike-m", type=float, default=0.0,
                   help="replace any cell more than this many metres from its 7x7 median, up or down, with that median, in two passes (0 = off). "
                        "The zoom-11 surround carries isolated bad cells over the open sea, hundreds of metres high, "
                        "which drew as spikes on the skyline (2026-09-08); the 4 m region raster has none.")
    a = p.parse_args()
    if a.inland:
        a.coast = False

    started = time.time()
    margin = a.cell_m * 4 + 200.0
    west, south, east, north = terrarium.region_box(a.centre_lat, a.centre_lon, a.extent_m, margin)
    x0, y0, x1, y1 = terrarium.tile_range(west, south, east, north, a.zoom)
    mosaic, missing = load_mosaic(a.zoom, x0, y0, x1, y1)
    if missing:
        print("refused: %d tile(s) missing from the cache, e.g. %s; run fetch_tiles.py --zoom %d first" % (len(missing), missing[0], a.zoom))
        return 1

    # Cell centres on the tangent plane, row 0 at the north edge, column 0 at the west edge, a band of rows at a time.
    n = raster_io.expected_side(a.extent_m, a.cell_m)
    heights = np.empty((n, n), dtype=np.float32)
    for r0 in range(0, n, BAND_ROWS):
        r1 = min(r0 + BAND_ROWS, n)
        heights[r0:r1] = sample_rows(mosaic, r0, r1, n, a.cell_m, a.extent_m, a.centre_lat, a.centre_lon, a.zoom, x0, y0)

    if np.isnan(heights).any():
        print("refused: the raster contains NaN (a hole in the mosaic)")
        return 1
    despiked = 0
    if a.despike_m > 0.0:
        heights, despiked = despike(heights, a.despike_m)
    hmin, hmax = float(heights.min()), float(heights.max())
    if hmax - hmin < 1.0:
        print("refused: no elevation variation (min %.1f, max %.1f); the tiles are not terrain" % (hmin, hmax))
        return 1
    if hmin < -11000.0 or hmax > 9000.0:
        print("refused: implausible range %.1f..%.1f m" % (hmin, hmax))
        return 1
    sea_fraction = float((heights <= 0.0).mean())
    if sea_fraction >= 0.999:
        print("refused: %.1f%% of the raster is at or below sea level; the box missed the land" % (sea_fraction * 100))
        return 1
    if a.coast and sea_fraction <= 0.0:
        print("refused: the region declares a coast but no cell is at or below sea level")
        return 1

    out_dir = os.path.join(ROOT, "Data", "regions", a.region)
    source = {
        "dataset": "AWS Terrain Tiles (Terrarium)",
        "url": terrarium.TILE_URL,
        "zoom": a.zoom,
        "tiles": "x %d..%d, y %d..%d" % (x0, x1, y0, y1),
        "pixel_m_at_centre": terrarium.metres_per_pixel(a.centre_lat, a.zoom),
        "effective_resolution_note": "the tile pixel pitch; the underlying source is SRTM 30 m inland and Geoscience Australia 5 m where present, so detail below ~30 m is interpolation, not data",
        "sampling": "bilinear at each cell centre",
        "despike_m": a.despike_m,
        "despiked_cells": despiked,
    }
    raster_io.write_raster(out_dir, a.name, a.region, heights, a.cell_m, a.extent_m, a.centre_lat, a.centre_lon,
                           source, "Tools/data/bake_region.py", terrarium.ATTRIBUTION)
    print("wrote %s: %dx%d at %.1f m, %.1f..%.1f m, sea %.1f%%, %.1f s"
          % (os.path.join(out_dir, a.name + ".r32"), n, n, a.cell_m, hmin, hmax, sea_fraction * 100, time.time() - started))
    return 0


if __name__ == "__main__":
    sys.exit(main())
