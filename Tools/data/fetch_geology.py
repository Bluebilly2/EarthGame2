#!/usr/bin/env python3
"""fetch_geology.py: the rock units of the Geological Survey of New South Wales's seamless geology that touch each region,
fetched on William's yes to the exact files (BF.4 stage three, promise 6; the ask of 2026-09-25).

The manifest is the two requests put to him, and nothing outside it is ever fetched: the layer `geology:rock_units_nsw`
of the Survey's public map service (WFS 2.0, GeoJSON), filtered to one box a region, each box the region's square as
its heights raster states it (centre and extent, rounded to 1e-4 degree). On 2026-09-25 the service counted 142 units
touching Bherwerre's box and 803 touching the whole Kangaroo Valley's, which holds the 8 km valley inside it.

A file is kept only whole and under the ceiling put to him (50 MB): the answer is read into memory, and written only
when it is complete, parses as GeoJSON and is under the ceiling; so a refused fetch leaves nothing, and nothing is ever
deleted. A file already there is never fetched again. Each kept file's size, SHA-256 and feature count go into
`manifest.json` beside it, which THIRD_PARTY_NOTICES.md's entry is written from. Nothing under Data/ is committed.

Claude's tool: it fetches, measures and records; it judges nothing but the ceiling and the count.

Usage, from the repository root:
    python Tools/data/fetch_geology.py --fetch [ids]     fetch the files named (default both) that are not yet here
    python Tools/data/fetch_geology.py --verify          recompute every kept file's SHA-256 against manifest.json

Exit 0 when every step asked for succeeded; 1 when any file failed or was refused.
"""
import argparse
import hashlib
import json
import sys
import time
from datetime import datetime, timezone
from pathlib import Path

import requests

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "Data" / "cache" / "nsw-seamless-geology"
RECORD = OUT / "manifest.json"
CEILING_BYTES = 50 * 1000 ** 2
TIMEOUT_S = 120
USER_AGENT = "EarthGame2 geology acquisition"
SERVICE = "https://gs-seamless.geoscience.nsw.gov.au/geoserver/ows"
LAYER = "geology:rock_units_nsw"
SOURCE = {
    "name": "NSW Seamless Geology, rock unit polygons (geology:rock_units_nsw), Edition 1",
    "publisher": "Geological Survey of NSW, Department of Primary Industries and Regional Development (DPIRD)",
    "licence": "Creative Commons Attribution 4.0 (https://creativecommons.org/licenses/by/4.0/)",
    "licence_page": "https://datasets.seed.nsw.gov.au/dataset/nsw-seamless-geology",
    "citation": "(c) State Government of NSW and Department of Primary Industries and Regional Development (DPIRD) 2025, "
                "NSW Seamless Geology, accessed from The Sharing and Enabling Environmental Data Portal "
                "[https://datasets.seed.nsw.gov.au/dataset/32ce9b05-0a22-4741-b292-64bcef50770f]",
}
# The two files put to William on 2026-09-25: (id, file, box as south, west, north, east in degrees, the count the service
# gave for that box that day). The boxes are the regions' squares from Data/regions/<id>/heights.json.
FILES = [
    ("bherwerre", "bherwerre-rock-units.geojson", (-35.1759, 150.6311, -35.1041, 150.7189), 142),
    ("kangaroo-valley-whole", "kangaroo-valley-whole-rock-units.geojson", (-34.8487, 150.4142, -34.5613, 150.7638), 803),
]


def request_params(box):
    south, west, north, east = box
    return {"service": "WFS", "version": "2.0.0", "request": "GetFeature", "typeNames": LAYER,
            "outputFormat": "application/json",
            "bbox": "%.4f,%.4f,%.4f,%.4f,urn:ogc:def:crs:EPSG::4326" % (south, west, north, east)}


def sha256_of(data):
    return hashlib.sha256(data).hexdigest()


def load_record():
    if RECORD.is_file():
        return json.loads(RECORD.read_text(encoding="utf-8"))
    return {"format": "eg2.geology-manifest", "version": 1, "source": SOURCE, "files": {}}


def save_record(record):
    OUT.mkdir(parents=True, exist_ok=True)
    RECORD.write_text(json.dumps(record, indent=2), encoding="utf-8")


def fetch_one(session, box):
    """The answer for one box, read whole into memory, or an error naming why it was refused."""
    with session.get(SERVICE, params=request_params(box), stream=True, timeout=TIMEOUT_S) as r:
        r.raise_for_status()
        url = r.url
        body = bytearray()
        for block in r.iter_content(chunk_size=1 << 16):
            body.extend(block)
            if len(body) > CEILING_BYTES:
                raise RuntimeError("over the ceiling of %d bytes before it ended; nothing kept" % CEILING_BYTES)
    return bytes(body), url


def fetch(wanted):
    session = requests.Session()
    session.headers["User-Agent"] = USER_AGENT
    record = load_record()
    failed = 0
    for rid, name, box, counted in FILES:
        if rid not in wanted:
            continue
        path = OUT / name
        if path.exists():
            print("%s: already here (%d bytes); not fetched again" % (name, path.stat().st_size))
            continue
        started = time.time()
        try:
            data, url = fetch_one(session, box)
            doc = json.loads(data.decode("utf-8"))
            features = doc.get("features")
            if doc.get("type") != "FeatureCollection" or not isinstance(features, list):
                raise RuntimeError("the answer is not a GeoJSON FeatureCollection; nothing kept")
        except (requests.RequestException, RuntimeError, ValueError) as e:
            print("%s: refused: %s" % (name, e))
            failed += 1
            continue
        OUT.mkdir(parents=True, exist_ok=True)
        path.write_bytes(data)
        digest = sha256_of(data)
        record["files"][name] = {
            "region": rid, "url": url, "box_south_west_north_east": list(box), "bytes": len(data), "sha256": digest,
            "features": len(features), "features_counted_2026_09_25": counted,
            "fetched_utc": datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"),
        }
        save_record(record)
        note = "" if len(features) == counted else "; the service counted %d on 2026-09-25" % counted
        print("%s: %d features, %d bytes in %.1f s, sha256 %s%s"
              % (name, len(features), len(data), time.time() - started, digest, note))
    return failed


def verify():
    record = load_record()
    failed = 0
    for name, entry in sorted(record["files"].items()):
        path = OUT / name
        if not path.is_file():
            print("%s: missing" % name)
            failed += 1
            continue
        data = path.read_bytes()
        same = sha256_of(data) == entry["sha256"] and len(data) == entry["bytes"]
        print("%s: %s" % (name, "ok" if same else "CHANGED"))
        failed += 0 if same else 1
    return failed


def main():
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--fetch", nargs="*", metavar="id", help="fetch the named files' regions (default both)")
    parser.add_argument("--verify", action="store_true")
    args = parser.parse_args()
    ids = [rid for rid, _, _, _ in FILES]
    failed = 0
    if args.fetch is not None:
        wanted = set(args.fetch or ids)
        unknown = wanted - set(ids)
        if unknown:
            parser.error("not in the manifest: %s (it names %s)" % (", ".join(sorted(unknown)), ", ".join(ids)))
        failed += fetch(wanted)
    if args.verify:
        failed += verify()
    if args.fetch is None and not args.verify:
        parser.print_help()
        return 1
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
