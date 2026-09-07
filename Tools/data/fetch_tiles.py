#!/usr/bin/env python3
"""Fetch AWS Terrain Tiles for a region box into Data/cache/terrarium/<z>/<x>/<y>.png (Claude's tool).

    python Tools/data/fetch_tiles.py --zoom 14 [--centre-lat -35.140 --centre-lon 150.675 --extent-m 8000 --margin-m 1000]

Idempotent: a tile already in the cache is not fetched again, so a re-bake needs no network. Refuses to write a
tile whose PNG does not decode. The exit code is the verdict: 0 only when every tile in the range is on disk.
The region numbers default to Engine's Region.Bherwerre; the sidecar the bake writes records what was used and
the engine's loader test compares them, so a drift between the two owners is a red, not a surprise.
"""
import argparse
import io
import os
import sys
import time

import requests
from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import terrarium  # noqa: E402

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
CACHE = os.path.join(ROOT, "Data", "cache", "terrarium")


def fetch_one(z, x, y, session, retries=3):
    path = os.path.join(CACHE, str(z), str(x), "%d.png" % y)
    if os.path.isfile(path):
        return "cached"
    url = terrarium.TILE_URL.format(z=z, x=x, y=y)
    for attempt in range(retries):
        try:
            r = session.get(url, timeout=30)
        except requests.RequestException as ex:
            if attempt == retries - 1:
                return "error: %s" % ex
            time.sleep(1.5 * (attempt + 1))
            continue
        if r.status_code == 404:
            return "missing (404)"
        if r.status_code != 200:
            if attempt == retries - 1:
                return "error: HTTP %d" % r.status_code
            time.sleep(1.5 * (attempt + 1))
            continue
        try:
            img = Image.open(io.BytesIO(r.content))
            img.load()
            if img.size != (terrarium.TILE_PX, terrarium.TILE_PX):
                return "error: tile is %dx%d, not %d" % (img.size[0], img.size[1], terrarium.TILE_PX)
        except Exception as ex:  # a PNG that does not decode is never written
            return "error: undecodable PNG (%s)" % ex
        os.makedirs(os.path.dirname(path), exist_ok=True)
        tmp = path + ".part"
        with open(tmp, "wb") as f:
            f.write(r.content)
        os.replace(tmp, path)
        return "fetched"
    return "error: gave up"


def main():
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("--zoom", type=int, required=True)
    p.add_argument("--centre-lat", type=float, default=terrarium.BHERWERRE_CENTRE_LAT)
    p.add_argument("--centre-lon", type=float, default=terrarium.BHERWERRE_CENTRE_LON)
    p.add_argument("--extent-m", type=float, default=terrarium.BHERWERRE_EXTENT_M)
    p.add_argument("--margin-m", type=float, default=1000.0)
    a = p.parse_args()

    west, south, east, north = terrarium.region_box(a.centre_lat, a.centre_lon, a.extent_m, a.margin_m)
    x0, y0, x1, y1 = terrarium.tile_range(west, south, east, north, a.zoom)
    total = (x1 - x0 + 1) * (y1 - y0 + 1)
    print("box %.5f..%.5f E, %.5f..%.5f N at z%d: tiles x %d..%d, y %d..%d (%d tiles, ~%.1f m/px)"
          % (west, east, south, north, a.zoom, x0, x1, y0, y1, total, terrarium.metres_per_pixel(a.centre_lat, a.zoom)))
    counts = {}
    failures = []
    with requests.Session() as session:
        session.headers["User-Agent"] = "EarthGame2 fetch_tiles (github.com/Bluebilly2/EarthGame2)"
        for x in range(x0, x1 + 1):
            for y in range(y0, y1 + 1):
                status = fetch_one(a.zoom, x, y, session)
                key = status.split(" ")[0].rstrip(":")
                counts[key] = counts.get(key, 0) + 1
                if key in ("error", "missing"):
                    failures.append("%d/%d/%d %s" % (a.zoom, x, y, status))
    print("result: " + ", ".join("%s %d" % (k, v) for k, v in sorted(counts.items())))
    for f in failures:
        print("  " + f)
    if failures:
        print("%d of %d tiles are not on disk; the range is incomplete" % (len(failures), total))
        return 1
    print("all %d tiles on disk under %s" % (total, CACHE))
    return 0


if __name__ == "__main__":
    sys.exit(main())
