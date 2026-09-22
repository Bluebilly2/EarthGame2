#!/usr/bin/env python3
"""derive.py: the coordinate atlas's six datasets read once, offline, into the atlas's own layers (WG.0b promise 2), each a
raw array beside a JSON header in Data/global/atlas/, in the shape Data/global/EarthElevation already has.

The layers, on a grid whose row 0 is the north edge and column 0 the west edge (row-major, little-endian):
  elevation      1/12 degree, int16 metres: ETOPO 2022's surface, the mean of the 10 x 10 pixels in each cell
  pr_01..pr_12   1/6 degree, int16 tenths of a millimetre a month: CHELSA V2.1's monthly precipitation, the mean of the cell's pixels
  tas_01..tas_12 1/6 degree, int16 tenths of a degree Celsius: CHELSA V2.1's monthly mean temperature, the same way
  lithology      1/12 degree, uint8: GLiM's class at the half-degree cell the atlas cell lies in; the classes in the header
  ecoregion      1/12 degree, uint16: RESOLVE Ecoregions 2017's ECO_ID + 1 at the cell's centre, 0 for none; names in ecoregion_names.json
  coast_km       1/12 degree, uint16 kilometres: the distance from the cell to the nearest cell across the coast (Natural Earth 10m land)
  lake_km        1/12 degree, uint16 kilometres: the distance to the nearest cell of a lake (Natural Earth 10m lakes)
The distances are chamfer distances on the grid with each row's own cell widths, good to a tenth; the header says so.

The raw files are read by Tools/atlas/readers.py and never shipped; each layer's header names what it is derived from and
the source's checksum from Data/global/manifest.json, and THIRD_PARTY_NOTICES.md carries the derived layers' entries.
Nothing under Data/ is committed. Claude's tool: it reads and writes and judges nothing; atlas_check.py judges.

Usage, from the repository root:
    python Tools/atlas/derive.py [--only elevation,climate,lithology,ecoregion,distances] [--out Data/global/atlas]
Exit 0 when every layer asked for was written; 1 otherwise.
"""
import argparse
import io
import json
import re
import sys
import time
import zipfile
from datetime import datetime, timezone
from pathlib import Path

import numpy as np

sys.path.insert(0, str(Path(__file__).resolve().parent))
from readers import GeoTiff, rasterise, read_ascii_grid, read_dbf, read_shapefile  # noqa: E402

ROOT = Path(__file__).resolve().parents[2]
GLOBAL = ROOT / "Data/global"
FINE = 12    # cells a degree for the elevation, the rock, the region and the distances
COARSE = 6   # cells a degree for the climate
NODATA_I16 = -32768
EARTH_KM_PER_DEG = 111.195


def header(name, width, height, cells_per_deg, dtype, unit, source, derived_from, extra=None):
    manifest = json.loads((GLOBAL / "manifest.json").read_text(encoding="utf-8"))
    sums = {}
    files = manifest.get("files", {})
    entries = files.items() if isinstance(files, dict) else [(str(f), f) for f in files]
    for key, f in entries:
        named = key + " " + " ".join(str(v) for v in (f.values() if isinstance(f, dict) else []) if isinstance(v, str))
        for d in derived_from:
            if d in named:
                sums[d] = f.get("sha256") if isinstance(f, dict) else None
    h = {
        "format": "eg2.atlas", "version": 1, "name": name, "dtype": dtype, "byte_order": "little",
        "layout": "row-major; row 0 is the north edge (+90N), column 0 the west edge (-180E); a cell is 1/%d degree" % cells_per_deg,
        "width": width, "height": height, "cells_per_degree": cells_per_deg, "unit": unit, "source": source,
        "derived_from": [{"file": d, "sha256": sums.get(d)} for d in derived_from],
        "derived_at_utc": datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"), "derived_by": "Tools/atlas/derive.py",
    }
    if extra:
        h.update(extra)
    return h


def write_layer(out, name, array, head):
    raw = out / (name + {"int16": ".r16", "uint16": ".u16", "uint8": ".u8"}[head["dtype"]])
    array.astype({"int16": "<i2", "uint16": "<u2", "uint8": "u1"}[head["dtype"]]).tofile(raw)
    head["raw"] = raw.name
    head["min"] = float(np.nanmin(array)) if array.size else None
    head["max"] = float(np.nanmax(array)) if array.size else None
    (out / (name + ".json")).write_text(json.dumps(head, indent=2), encoding="utf-8")
    print("  %s: %d x %d %s, %.1f MB" % (name, head["width"], head["height"], head["dtype"], raw.stat().st_size / 1e6))


def block_mean(tif, cells_per_deg, nodata=None, first_lat=90.0):
    """A GeoTIFF at 1/120 degree averaged into cells of 1/cells_per_deg degree over the whole globe, NaN where nothing valid."""
    factor = int(round(1.0 / (tif.pixel_deg[1] * cells_per_deg)))
    width, height = 360 * cells_per_deg, 180 * cells_per_deg
    out = np.full((height, width), np.nan, dtype=np.float64)
    top_row = int(round((90.0 - tif.origin[1]) * cells_per_deg))  # the atlas row the file's first row falls in
    cell_rows = -(-tif.height // factor)
    # Whole cell rows at a time, as many as a block holds, so a tile of ETOPO is decoded once and not once a cell row.
    per = max(1, tif.block_h // factor)
    cols = tif.width // factor
    for cr0 in range(0, cell_rows, per):
        crn = min(per, cell_rows - cr0)
        r0 = cr0 * factor
        rows = tif.rows(r0, min(crn * factor, tif.height - r0)).astype(np.float64)
        if nodata is not None:
            rows[rows == nodata] = np.nan
        if rows.shape[0] < crn * factor:
            rows = np.concatenate([rows, np.full((crn * factor - rows.shape[0], rows.shape[1]), np.nan)], axis=0)
        with np.errstate(invalid="ignore"):
            means = np.nanmean(rows[:, :cols * factor].reshape(crn, factor, cols, factor), axis=(1, 3))
        for k in range(crn):
            row = top_row + cr0 + k
            if 0 <= row < height:
                out[row, :cols] = means[k]
    return out


def scale_offset(tif):
    """CHELSA's scale and offset from its GDAL metadata tag, 1 and 0 when it carries none."""
    text = str(tif.tags.get(42112, ("",))[0])
    s = re.search(r'name="SCALE"[^>]*>([-0-9.eE]+)<', text)
    o = re.search(r'name="OFFSET"[^>]*>([-0-9.eE]+)<', text)
    return (float(s.group(1)) if s else 1.0), (float(o.group(1)) if o else 0.0)


def derive_elevation(out):
    path = GLOBAL / "etopo/ETOPO_2022_v1_30s_N90W180_surface.tif"
    tif = GeoTiff(path)
    means = block_mean(tif, FINE, nodata=tif.nodata)
    tif.close()
    array = np.where(np.isnan(means), NODATA_I16, np.rint(means)).astype(np.int16)
    write_layer(out, "elevation", array, header("elevation", array.shape[1], array.shape[0], FINE, "int16", "m",
                "NOAA ETOPO 2022 30 arc-second surface, the mean of each cell's 10 x 10 pixels; %d where the source has nothing" % NODATA_I16,
                [path.name], {"nodata": NODATA_I16}))


def derive_climate(out):
    for kind, unit, describe in (("pr", "0.1 mm a month", "monthly precipitation"), ("tas", "0.1 degC", "monthly mean temperature")):
        for month in range(1, 13):
            path = GLOBAL / ("chelsa/CHELSA_%s_%02d_1981-2010_V.2.1.tif" % (kind, month))
            tif = GeoTiff(path)
            scale, offset = scale_offset(tif)
            means = block_mean(tif, COARSE, nodata=65535)
            tif.close()
            # In the dataset's own units first (scale and offset), then into tenths: mm for rain, degrees Celsius for warmth.
            real = means * scale + offset
            tenths = real * 10.0
            array = np.where(np.isnan(tenths), NODATA_I16, np.clip(np.rint(tenths), -32767, 32767)).astype(np.int16)
            name = "%s_%02d" % (kind, month)
            write_layer(out, name, array, header(name, array.shape[1], array.shape[0], COARSE, "int16", unit,
                        "CHELSA V2.1 %s 1981-2010 (the source's scale %g and offset %g applied, so rain is in mm and warmth in degrees Celsius), the mean of each cell's 20 x 20 pixels; %d where the source has nothing (the sea, and north of 84N)" % (describe, scale, offset, NODATA_I16),
                        [path.name], {"nodata": NODATA_I16, "month": month}))


def derive_lithology(out):
    z = zipfile.ZipFile(GLOBAL / "glim/glim_gridded_0point5deg.zip")
    names = z.read("Classnames.txt").decode("latin-1")
    west, south, cell, nodata, rows = read_ascii_grid(z.read("glim_wgs84_0point5deg.txt.asc").decode("latin-1"))
    long = {"su": "unconsolidated sediments", "vb": "basic volcanic rocks", "ss": "siliciclastic sedimentary rocks", "pb": "basic plutonic rocks",
            "sm": "mixed sedimentary rocks", "sc": "carbonate sedimentary rocks", "va": "acid volcanic rocks", "mt": "metamorphic rocks",
            "pa": "acid plutonic rocks", "vi": "intermediate volcanic rocks", "wb": "water bodies", "py": "pyroclastics",
            "pi": "intermediate plutonic rocks", "ev": "evaporites", "nd": "no data", "ig": "ice and glaciers"}
    classes = {}
    for line in names.splitlines():
        parts = [x.strip().strip('"') for x in line.split(";")]
        if len(parts) >= 4 and parts[1].isdigit():
            classes[int(parts[1])] = parts[3] + " (" + long.get(parts[3], parts[3]) + ")"
    width, height = 360 * FINE, 180 * FINE
    lat = 90.0 - (np.arange(height) + 0.5) / FINE
    lon = -180.0 + (np.arange(width) + 0.5) / FINE
    north = south + cell * rows.shape[0]
    r = np.clip(((north - lat) / cell).astype(int), 0, rows.shape[0] - 1)
    c = np.clip(((lon - west) / cell).astype(int), 0, rows.shape[1] - 1)
    picked = rows[r[:, None], c[None, :]]
    array = np.where((picked == nodata) | np.isnan(picked), 0, picked).astype(np.uint8)
    write_layer(out, "lithology", array, header("lithology", width, height, FINE, "uint8", "class",
                "GLiM (Hartmann & Moosdorf 2012) gridded at half a degree, the class of the half-degree cell each atlas cell lies in; 0 for none",
                ["glim_gridded_0point5deg.zip"], {"classes": {str(k): v for k, v in sorted(classes.items())}}))


def derive_ecoregion(out):
    z = zipfile.ZipFile(GLOBAL / "ecoregions/Ecoregions2017.zip")
    folder = out / "_tmp_eco"
    folder.mkdir(exist_ok=True)
    for name in z.namelist():
        (folder / Path(name).name).write_bytes(z.read(name))
    records = read_shapefile(folder / "Ecoregions2017.shp")
    ids = [int(a.get("ECO_ID") or 0) + 1 for _, a in records]
    array = rasterise([r for r, _ in records], ids, west=-180.0, north=90.0, cell_deg=1.0 / FINE, width=360 * FINE, height=180 * FINE, dtype=np.uint16)
    names = {}
    for (_, a), i in zip(records, ids):
        names[str(i)] = {"eco_name": a.get("ECO_NAME"), "biome": a.get("BIOME_NAME"), "realm": a.get("REALM")}
    (out / "ecoregion_names.json").write_text(json.dumps(names, indent=1), encoding="utf-8")
    for p in folder.iterdir():
        p.unlink()
    folder.rmdir()
    write_layer(out, "ecoregion", array, header("ecoregion", array.shape[1], array.shape[0], FINE, "uint16", "id",
                "RESOLVE Ecoregions 2017: the ECO_ID + 1 of the ecoregion the cell's centre lies in, 0 for none (the sea); the names in ecoregion_names.json",
                ["Ecoregions2017.zip"]))


def chamfer_km(mask, cells_per_deg):
    """
    The distance, km, from every cell to the nearest cell where mask is true, by a chamfer over the grid with each row's own
    cell width (a degree of longitude shrinks with the latitude): four sweeps, the running minimum along a row done as a
    cumulative minimum. Good to about a tenth of the distance, which is what a choice of place needs.
    """
    height, width = mask.shape
    dy = EARTH_KM_PER_DEG / cells_per_deg
    lat = 90.0 - (np.arange(height) + 0.5) / cells_per_deg
    dx = np.maximum(dy * np.cos(np.radians(lat)), 1e-3)
    big = 1e9
    d = np.where(mask, 0.0, big)
    cols = np.arange(width, dtype=np.float64)
    for sweep in range(2):
        order = range(height) if sweep == 0 else range(height - 1, -1, -1)
        prev = None
        for r in order:
            row = d[r]
            if prev is not None:
                diag = np.sqrt(dx[r] ** 2 + dy ** 2)
                row = np.minimum(row, prev + dy)
                row = np.minimum(row, np.concatenate([[big], prev[:-1]]) + diag)
                row = np.minimum(row, np.concatenate([prev[1:], [big]]) + diag)
            # Along the row, both ways, with the wrap at the date line ignored (the choice of place is not at sea).
            row = np.minimum(row, np.minimum.accumulate(row - dx[r] * cols) + dx[r] * cols)
            back = np.minimum.accumulate((row + dx[r] * cols)[::-1])[::-1] - dx[r] * cols
            row = np.minimum(row, back)
            d[r] = row
            prev = row
    return np.minimum(d, 65535.0)


def derive_distances(out):
    z = zipfile.ZipFile(GLOBAL / "naturalearth/10m_physical.zip")
    folder = out / "_tmp_ne"
    folder.mkdir(exist_ok=True)
    wanted = [n for n in z.namelist() if Path(n).stem in ("ne_10m_land", "ne_10m_lakes", "ne_10m_lakes_europe", "ne_10m_lakes_north_america", "ne_10m_lakes_australia")]
    for name in wanted:
        (folder / Path(name).name).write_bytes(z.read(name))
    width, height = 360 * FINE, 180 * FINE
    land_records = read_shapefile(folder / "ne_10m_land.shp")
    land = rasterise([r for r, _ in land_records], [1] * len(land_records), -180.0, 90.0, 1.0 / FINE, width, height, dtype=np.uint8) > 0
    lakes = np.zeros((height, width), dtype=bool)
    for stem in ("ne_10m_lakes", "ne_10m_lakes_europe", "ne_10m_lakes_north_america", "ne_10m_lakes_australia"):
        shp = folder / (stem + ".shp")
        if not shp.is_file():
            continue
        recs = read_shapefile(shp)
        lakes |= rasterise([r for r, _ in recs], [1] * len(recs), -180.0, 90.0, 1.0 / FINE, width, height, dtype=np.uint8) > 0
    for p in folder.iterdir():
        p.unlink()
    folder.rmdir()
    # The coast: from land the distance to the nearest sea cell, from the sea the distance to the nearest land cell.
    to_sea = chamfer_km(~land, FINE)
    to_land = chamfer_km(land, FINE)
    coast = np.where(land, to_sea, to_land)
    write_layer(out, "coast_km", np.rint(coast).astype(np.uint16), header("coast_km", width, height, FINE, "uint16", "km",
                "Natural Earth 1:10m physical (land): the chamfer distance from the cell to the nearest cell on the other side of the coast, on land to the sea and at sea to the land",
                ["10m_physical.zip"], {"method": "chamfer on the grid with each row's own cell width, good to about a tenth"}))
    write_layer(out, "lake_km", np.rint(chamfer_km(lakes, FINE)).astype(np.uint16), header("lake_km", width, height, FINE, "uint16", "km",
                "Natural Earth 1:10m physical (lakes, with the European, North American and Australian supplements): the chamfer distance to the nearest lake cell",
                ["10m_physical.zip"], {"method": "chamfer on the grid with each row's own cell width, good to about a tenth"}))


def main():
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--only", default="elevation,climate,lithology,ecoregion,distances")
    parser.add_argument("--out", default="Data/global/atlas")
    args = parser.parse_args()
    out = ROOT / args.out
    out.mkdir(parents=True, exist_ok=True)
    steps = {"elevation": derive_elevation, "climate": derive_climate, "lithology": derive_lithology, "ecoregion": derive_ecoregion, "distances": derive_distances}
    for name in args.only.split(","):
        name = name.strip()
        if name not in steps:
            print("no such layer set: %s" % name)
            return 1
        t0 = time.time()
        print("%s:" % name)
        steps[name](out)
        print("  %.0f s" % (time.time() - t0))
    print(out)
    return 0


if __name__ == "__main__":
    sys.exit(main())
