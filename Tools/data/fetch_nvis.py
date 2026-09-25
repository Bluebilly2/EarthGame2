#!/usr/bin/env python3
"""fetch_nvis.py: what grew over each region's surround before 1750, as the Australian Government's National Vegetation
Information System reconstructs it (NVIS 7.0, pre-1750 Major Vegetation Groups), fetched as an image of its classes for the
far view (M1.6h, 2026-09-25).

The Department's map service (`ogc_services/NVIS_pre_mvg`, ArcGIS MapServer) draws the 100 m raster in one flat colour a
class, which its legend names: 31 classes, 31 colours, no two alike. An export of the surround's box in longitude and latitude
is therefore the classes themselves, read back by colour. Where the raster has no data (the open sea: estuaries are a class of
their own) the export is transparent, asked for so, since the opaque form paints those pixels a near-white no class has. A
pixel neither transparent nor a class's colour means the service smoothed or changed its drawing: the fetch refuses the
image then, and keeps nothing. The whole-country raster (112.5 MB, a File
Geodatabase) would want GDAL, which the tools do not carry, and the boxes want a few hundred kilobytes of it.

Each region's box is its `surround` raster's square (Data/regions/<id>/surround.json: centre and extent, by the small-angle
rule the bakes use) and a kilometre more, at about 50 m a pixel, half the source's 100 m. The image, its request, size,
SHA-256 and the legend's colours go to `Data/cache/nvis/` with `manifest.json` beside them; a file already there is never
fetched again, and nothing is ever deleted. Nothing under Data/ is committed. Downloads are the agents' to make (CANON 49),
each recorded as this manifest records it.

Claude's tool: it fetches, decodes and records; `bake_vegetation.py` puts the classes on the surround's grid, and
`vegetation_check.py` asks the service's own identify, a path that never reads these colours.

Usage, from the repository root:
    python Tools/data/fetch_nvis.py --fetch [regions]     fetch each region's box not yet here (default: every region with a surround)
    python Tools/data/fetch_nvis.py --verify              recompute every kept file's SHA-256 against manifest.json

Exit 0 when every step asked for succeeded; 1 when any fetch failed or was refused.
"""
import argparse
import base64
import hashlib
import io
import json
import math
import sys
from datetime import datetime, timezone
from pathlib import Path

import requests
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "Data" / "cache" / "nvis"
RECORD = OUT / "manifest.json"
SERVICE = "https://gis.environment.gov.au/gispubmap/rest/services/ogc_services/NVIS_pre_mvg/MapServer"
USER_AGENT = "EarthGame2 vegetation acquisition"
TIMEOUT_S = 180
EARTH_RADIUS_M = 6371000.0
PIXEL_M = 50.0
MARGIN_M = 1000.0
MAX_PIXELS = 4096
SOURCE = {
    "name": "Australia - Pre-1750 Major Vegetation Groups - NVIS Version 7.0 (Albers 100m analysis product)",
    "publisher": "Department of Climate Change, Energy, the Environment and Water (DCCEEW), Environment Information Australia",
    "licence": "Creative Commons Attribution 4.0 International (https://creativecommons.org/licenses/by/4.0/)",
    "licence_page": "https://fed.dcceew.gov.au/datasets/d82f6eab808542ee9d9a0ea09ea36567",
    "citation": "NVIS v7.0, (c) Australian Government Department of Climate Change, Energy, the Environment and Water, compiled "
                "from data supplied by the States and Territories",
    "service": SERVICE,
}


def surround_box(region):
    """The region's surround square and a margin, as (west, south, east, north) degrees, and its span in metres."""
    sidecar = json.loads((ROOT / "Data" / "regions" / region / "surround.json").read_text(encoding="utf-8"))
    lat0, lon0 = float(sidecar["centre_lat"]), float(sidecar["centre_lon"])
    half = float(sidecar["extent_m"]) / 2.0 + MARGIN_M
    dlat = math.degrees(half / EARTH_RADIUS_M)
    dlon = math.degrees(half / (EARTH_RADIUS_M * math.cos(math.radians(lat0))))
    return (round(lon0 - dlon, 5), round(lat0 - dlat, 5), round(lon0 + dlon, 5), round(lat0 + dlat, 5)), 2.0 * half


def legend(session):
    """The service's legend: each class's name and the one colour it is drawn in, in the legend's order."""
    answer = session.get(SERVICE + "/legend", params={"f": "json"}, timeout=TIMEOUT_S)
    answer.raise_for_status()
    entries = answer.json()["layers"][0]["legend"]
    classes = []
    for entry in entries:
        swatch = Image.open(io.BytesIO(base64.b64decode(entry["imageData"]))).convert("RGB")
        w, h = swatch.size
        classes.append({"name": entry["label"], "rgb": list(swatch.getpixel((w // 2, h // 2)))})
    if len({tuple(c["rgb"]) for c in classes}) != len(classes):
        raise RuntimeError("two classes of the legend share a colour; the image cannot be read back by colour")
    return classes


def fetch(session, region, record):
    name = "%s-pre1750-mvg.png" % region
    if name in record["files"] and (OUT / name).is_file():
        print("%s: already here" % name)
        return True
    box, span_m = surround_box(region)
    side = int(math.ceil(span_m / PIXEL_M))
    if side > MAX_PIXELS:
        raise RuntimeError("%s's box wants %d pixels a side, past the service's %d" % (region, side, MAX_PIXELS))
    classes = legend(session)
    params = {"bbox": "%.5f,%.5f,%.5f,%.5f" % box, "bboxSR": "4326", "imageSR": "4326", "size": "%d,%d" % (side, side),
              "format": "png32", "transparent": "true", "f": "image", "layers": "show:0", "dpi": "96"}
    answer = session.get(SERVICE + "/export", params=params, timeout=TIMEOUT_S)
    if answer.status_code != 200 or not answer.headers.get("content-type", "").startswith("image/png"):
        print("%s: refused: HTTP %d, %s" % (name, answer.status_code, answer.headers.get("content-type")))
        return False
    body = answer.content
    image = Image.open(io.BytesIO(body)).convert("RGBA")
    if image.size != (side, side):
        print("%s: refused: the image is %dx%d where %d a side was asked" % (name, image.size[0], image.size[1], side))
        return False
    known = {tuple(c["rgb"]) for c in classes}
    counts, outside, strangers = {}, 0, 0
    for n, (r, g, b, a) in image.getcolors(maxcolors=1 << 24) or []:
        if a == 0:
            outside += n
        elif a == 255 and (r, g, b) in known:
            counts[(r, g, b)] = n
        else:
            strangers += n
    if strangers:
        print("%s: refused: %d of %d pixels are neither transparent nor a class's colour" % (name, strangers, side * side))
        return False
    OUT.mkdir(parents=True, exist_ok=True)
    (OUT / name).write_bytes(body)
    by_name = {c["name"]: counts.get(tuple(c["rgb"]), 0) for c in classes}
    record["files"][name] = {
        "region": region, "url": answer.url, "box_west_south_east_north": list(box), "pixels": [side, side],
        "pixel_deg": [(box[2] - box[0]) / side, (box[3] - box[1]) / side], "bytes": len(body),
        "sha256": hashlib.sha256(body).hexdigest(), "fetched_utc": datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"),
        "legend": classes, "pixels_by_class": {k: v for k, v in by_name.items() if v}, "pixels_outside": outside,
    }
    print("%s: %d bytes, %dx%d, sha256 %s" % (name, len(body), side, side, record["files"][name]["sha256"]))
    for k, v in sorted(by_name.items(), key=lambda kv: -kv[1]):
        if v:
            print("   %5.1f %%  %s" % (100.0 * v / (side * side), k))
    print("   %5.1f %%  (no data: the open sea)" % (100.0 * outside / (side * side)))
    return True


def main():
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--fetch", nargs="*", default=None)
    parser.add_argument("--verify", action="store_true")
    args = parser.parse_args()
    record = json.loads(RECORD.read_text(encoding="utf-8")) if RECORD.is_file() else {
        "format": "eg2.nvis-manifest", "version": 1, "source": SOURCE, "files": {}}
    ok = True
    if args.fetch is not None:
        regions = args.fetch or sorted(p.parent.name for p in (ROOT / "Data" / "regions").glob("*/surround.json"))
        session = requests.Session()
        session.headers["User-Agent"] = USER_AGENT
        for region in regions:
            ok = fetch(session, region, record) and ok
        OUT.mkdir(parents=True, exist_ok=True)
        RECORD.write_text(json.dumps(record, indent=2) + "\n", encoding="utf-8")
    if args.verify:
        for name, entry in sorted(record["files"].items()):
            path = OUT / name
            good = path.is_file() and hashlib.sha256(path.read_bytes()).hexdigest() == entry["sha256"]
            print("%s: %s" % (name, "ok" if good else "MISMATCH or missing"))
            ok = ok and good
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
