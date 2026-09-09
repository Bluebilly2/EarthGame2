"""The eg2.raster format, written in one place (Claude's tool).

A region raster is a raw little-endian grid, row-major, row 0 the NORTH edge and column 0 the WEST edge, plus a
JSON sidecar of the same stem that is the header the engine's RegionRaster loader reads (ARCHITECTURE.md §3 and
§10; format `eg2.raster`). The bake and the fixture writer both call `write_raster`, so the loader's tests on a
fixture prove the bake's output shape.

Version 1 carried one float32 grid of metres (`heights.r32` + `heights.json`). Version 2 (M1.2, 2026-09-09) names
the layer and its dtype, and states a scale and a unit for integer layers: a value is `raw * scale` in `unit`,
so soil depth is stored as u16 centimetres with scale 0.01 and unit "m", wetness as u8 with scale 1/255 and
unit "1", an id or a flag mask as an integer with scale 1 and unit "id" or "flags". The raw file's name is in
the sidecar (`raw`); f32 layers keep the .r32 extension of version 1, the others take .u8, .u16, .i16, .u32. A
version-1 file reads as a version-2 heights layer in metres; the engine's world-creation pipeline writes the
same version 2 from C# (RegionRaster.Write), and the loader's tests pin both writers to the same fixture law.

Cell centres sit on the tangent plane at east = col * cell_m - extent_m / 2, north = extent_m / 2 - row * cell_m,
so width == height == round(extent_m / cell_m) + 1 and the centre cell is exactly (0, 0).
"""
import hashlib
import json
import os
import time

import numpy as np

FORMAT = "eg2.raster"
FORMAT_VERSION = 2

# dtype name -> (numpy little-endian dtype, raw extension)
DTYPES = {
    "f32": ("<f4", ".r32"),
    "u8": ("<u1", ".u8"),
    "u16": ("<u2", ".u16"),
    "i16": ("<i2", ".i16"),
    "u32": ("<u4", ".u32"),
}


def expected_side(extent_m, cell_m):
    """Cells along one side for an extent and a pitch: the centre cell plus half the extent each way."""
    return int(round(extent_m / cell_m)) + 1


def raw_name(name, dtype):
    return name + DTYPES[dtype][1]


def write_raster(out_dir, name, region, values, cell_m, extent_m, centre_lat, centre_lon, source, baked_by,
                 attribution, layer="heights", dtype="f32", scale=1.0, unit="m", extra=None):
    """Writes <name>.<raw ext> and <name>.json into out_dir atomically; returns the sidecar dict.

    `values` are in the layer's unit (metres for heights); integer dtypes store round(value / scale) and refuse a
    value outside the dtype's range rather than wrapping it. `extra` adds keys to the sidecar (a legend for an id
    layer, say); it may not shadow a contracted key."""
    if dtype not in DTYPES:
        raise ValueError("unknown dtype %r; one of %s" % (dtype, ", ".join(DTYPES)))
    np_dtype, ext = DTYPES[dtype]
    values = np.asarray(values)
    if values.ndim != 2:
        raise ValueError("values must be a 2-D array")
    h, w = values.shape
    side = expected_side(extent_m, cell_m)
    if w != side or h != side:
        raise ValueError("a %dx%d raster does not match extent %s m at %s m per cell (expected %d)" % (w, h, extent_m, cell_m, side))
    if np.issubdtype(values.dtype, np.floating) and np.isnan(values).any():
        raise ValueError("the raster contains NaN")
    if dtype == "f32":
        stored = values.astype(np_dtype)
        if scale != 1.0:
            raise ValueError("an f32 layer is stored in its unit; scale must be 1")
    else:
        quantised = np.rint(np.asarray(values, dtype=np.float64) / scale)
        info = np.iinfo(np_dtype)
        if quantised.min() < info.min or quantised.max() > info.max:
            raise ValueError("layer %s: a value of %s / %s is outside %s" % (layer, quantised.min() if quantised.min() < info.min else quantised.max(), scale, dtype))
        stored = quantised.astype(np_dtype)
    os.makedirs(out_dir, exist_ok=True)
    raw_path = os.path.join(out_dir, name + ext)
    data = stored.tobytes()
    tmp = raw_path + ".part"
    with open(tmp, "wb") as f:
        f.write(data)
    os.replace(tmp, raw_path)
    in_unit = stored.astype(np.float64) * scale
    sidecar = {
        "format": FORMAT,
        "version": FORMAT_VERSION,
        "name": name,
        "region": region,
        "layer": layer,
        "dtype": dtype,
        "byte_order": "little",
        "raw": name + ext,
        "scale": float(scale),
        "unit": unit,
        "layout": "row-major; row 0 is the north edge, column 0 is the west edge; a value is raw * scale in the unit",
        "width": w,
        "height": h,
        "cell_m": float(cell_m),
        "extent_m": float(extent_m),
        "centre_lat": float(centre_lat),
        "centre_lon": float(centre_lon),
        "frame": "tangent plane, +east +north metres from the centre; small-angle mapping as Engine LocalFrame",
        "min": float(in_unit.min()),
        "max": float(in_unit.max()),
        "source": source,
        "sha256": hashlib.sha256(data).hexdigest(),
        "attribution": attribution,
        "baked_by": baked_by,
        "baked_at_utc": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()),
    }
    if layer == "heights" and unit == "m":
        sidecar["sea_fraction"] = float((in_unit <= 0.0).mean())
    for key, value in (extra or {}).items():
        if key in sidecar:
            raise ValueError("extra key %r would shadow the sidecar's own" % key)
        sidecar[key] = value
    json_path = os.path.join(out_dir, name + ".json")
    tmp = json_path + ".part"
    with open(tmp, "w", encoding="utf-8") as f:
        json.dump(sidecar, f, indent=2)
        f.write("\n")
    os.replace(tmp, json_path)
    return sidecar


def read_raster(sidecar_path):
    """Reads a version-1 or version-2 raster back as (sidecar, values in the layer's unit as float64)."""
    with open(sidecar_path, encoding="utf-8") as f:
        sidecar = json.load(f)
    if sidecar.get("format") != FORMAT or sidecar.get("version") not in (1, 2):
        raise ValueError("%s: not an eg2.raster version 1 or 2" % sidecar_path)
    dtype = sidecar.get("dtype", "f32")
    np_dtype, ext = DTYPES[dtype]
    raw = os.path.join(os.path.dirname(sidecar_path), sidecar.get("raw", sidecar["name"] + ".r32"))
    data = np.fromfile(raw, dtype=np_dtype)
    if hashlib.sha256(data.tobytes()).hexdigest() != sidecar["sha256"]:
        raise ValueError("%s: the raw file is not the one the sidecar names" % raw)
    grid = data.reshape(sidecar["height"], sidecar["width"]).astype(np.float64) * float(sidecar.get("scale", 1.0))
    return sidecar, grid
