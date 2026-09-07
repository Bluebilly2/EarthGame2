"""The eg2.raster format, written in one place (Claude's tool).

A region raster is a raw little-endian float32 file, row-major, row 0 the NORTH edge and column 0 the WEST edge,
values in metres, plus a JSON sidecar of the same stem (`heights.r32` + `heights.json`). The sidecar is the header
the engine's RegionRaster loader reads (ARCHITECTURE.md §3 and §10; format `eg2.raster`, version 1). The bake and
the fixture writer both call `write_raster`, so the loader's tests on a fixture prove the bake's output shape.

Cell centres sit on the tangent plane at east = col * cell_m - extent_m / 2, north = extent_m / 2 - row * cell_m,
so width == height == round(extent_m / cell_m) + 1 and the centre cell is exactly (0, 0).
"""
import hashlib
import json
import os
import time

import numpy as np

FORMAT = "eg2.raster"
FORMAT_VERSION = 1


def expected_side(extent_m, cell_m):
    """Cells along one side for an extent and a pitch: the centre cell plus half the extent each way."""
    return int(round(extent_m / cell_m)) + 1


def write_raster(out_dir, name, region, heights, cell_m, extent_m, centre_lat, centre_lon, source, baked_by,
                 attribution):
    """Writes <name>.r32 and <name>.json into out_dir atomically; returns the sidecar dict."""
    heights = np.asarray(heights, dtype=np.float32)
    if heights.ndim != 2:
        raise ValueError("heights must be a 2-D array")
    h, w = heights.shape
    side = expected_side(extent_m, cell_m)
    if w != side or h != side:
        raise ValueError("a %dx%d raster does not match extent %s m at %s m per cell (expected %d)" % (w, h, extent_m, cell_m, side))
    if np.isnan(heights).any():
        raise ValueError("the raster contains NaN")
    os.makedirs(out_dir, exist_ok=True)
    raw_path = os.path.join(out_dir, name + ".r32")
    data = heights.astype("<f4").tobytes()
    tmp = raw_path + ".part"
    with open(tmp, "wb") as f:
        f.write(data)
    os.replace(tmp, raw_path)
    sidecar = {
        "format": FORMAT,
        "version": FORMAT_VERSION,
        "name": name,
        "region": region,
        "dtype": "f32",
        "byte_order": "little",
        "layout": "row-major; row 0 is the north edge, column 0 is the west edge; values are metres above sea level",
        "width": w,
        "height": h,
        "cell_m": float(cell_m),
        "extent_m": float(extent_m),
        "centre_lat": float(centre_lat),
        "centre_lon": float(centre_lon),
        "frame": "tangent plane, +east +north metres from the centre; small-angle mapping as Engine LocalFrame",
        "min_m": float(heights.min()),
        "max_m": float(heights.max()),
        "sea_fraction": float((heights <= 0.0).mean()),
        "source": source,
        "sha256": hashlib.sha256(data).hexdigest(),
        "attribution": attribution,
        "baked_by": baked_by,
        "baked_at_utc": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()),
    }
    json_path = os.path.join(out_dir, name + ".json")
    tmp = json_path + ".part"
    with open(tmp, "w", encoding="utf-8") as f:
        json.dump(sidecar, f, indent=2)
        f.write("\n")
    os.replace(tmp, json_path)
    return sidecar
