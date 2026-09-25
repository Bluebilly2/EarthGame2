#!/usr/bin/env python3
"""acquire.py: the coordinate atlas's six datasets (WG.0's acquisition manifest, approved whole by William on 2026-09-21,
"all six"), measured before they are fetched, fetched into Data/global/, and recorded with their sizes and checksums.

The manifest is the one in Docs/contracts/WG.0_COORDINATE_ATLAS.md: nothing outside it is ever fetched, and CHELSA's
twenty-four files are fetched only if the server says they come to less than the ceiling William approved (8 GB).
Nothing under Data/ is committed (the .gitignore's rule); what is committed is this tool and, in the same commit,
each dataset's entry in THIRD_PARTY_NOTICES.md, written from the manifest.json this tool leaves beside the files.

Claude's tool: it measures, fetches and records; it judges nothing but the ceiling, and it deletes nothing — a fetch
that stops leaves its .part file for the next run to resume from.

Usage, from the repository root:
    python Tools/atlas/acquire.py --measure                 HEAD every file: status, size, type; the totals and the ceiling
    python Tools/atlas/acquire.py --fetch [ids]              fetch the datasets named (default all six), resuming .part files
    python Tools/atlas/acquire.py --verify                   recompute every fetched file's SHA-256 against manifest.json

Exit 0 when every step asked for succeeded; 1 when any file failed, was refused, or the ceiling was exceeded.
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
DATA = ROOT / "Data" / "global"
MANIFEST_RECORD = DATA / "manifest.json"
CHELSA_CEILING_BYTES = 8 * 1024 ** 3
TIMEOUT_S = 60
CHUNK = 1 << 20
USER_AGENT = "EarthGame2 atlas acquisition"

# The manifest: one entry a dataset, its files as (url, path under Data/global/<folder>/). Where the manifest states a size
# it is repeated here as `stated_bytes` so --measure can say whether the server agrees. The licence page is the one the
# term was read from, with the date, because ETOPO's product page carries no licence text at all (WG.0's note).
CHELSA_BASE = "https://os.zhdk.cloud.switch.ch/chelsav2/GLOBAL/climatologies/1981-2010"
DATASETS = [
    {
        "id": "naturalearth", "name": "Natural Earth 1:10m physical, v5.1.1", "licence": "Public domain",
        "licence_page": "https://www.naturalearthdata.com/about/terms-of-use/",
        "citation": "Made with Natural Earth. Free vector and raster map data @ naturalearthdata.com.",
        "files": [("https://naciscdn.org/naturalearth/10m/physical/10m_physical.zip", "naturalearth/10m_physical.zip")],
        "stated_bytes": 49_990_000,
    },
    {
        "id": "etopo", "name": "NOAA ETOPO 2022, 30 arc-second surface", "licence": "CC0 (the metadata record; the product page states none)",
        "licence_page": "https://www.ncei.noaa.gov/metadata/geoportal/rest/metadata/item/gov.noaa.ngdc.mgg.dem%3Aetopo_2022/html",
        "citation": "NOAA National Centers for Environmental Information. 2022: ETOPO 2022 15 Arc-Second Global Relief Model. "
                    "NOAA NCEI. DOI: 10.25921/fd45-gt74.",
        "files": [("https://www.ngdc.noaa.gov/mgg/global/relief/ETOPO2022/data/30s/30s_surface_elev_gtif/"
                   "ETOPO_2022_v1_30s_N90W180_surface.tif", "etopo/ETOPO_2022_v1_30s_N90W180_surface.tif")],
        "stated_bytes": 1_500_000_000,
    },
    {
        "id": "hydrolakes", "name": "HydroLAKES v1.0", "licence": "CC-BY 4.0",
        "licence_page": "https://www.hydrosheds.org/products/hydrolakes",
        "citation": "Messager, M.L., Lehner, B., Grill, G., Nedeva, I., Schmitt, O. (2016): Estimating the volume and age of water "
                    "stored in global lakes using a geo-statistical approach. Nature Communications 7: 13603. doi:10.1038/ncomms13603.",
        "files": [("https://data.hydrosheds.org/file/hydrolakes/HydroLAKES_polys_v10.gdb.zip", "hydrolakes/HydroLAKES_polys_v10.gdb.zip")],
        "stated_bytes": 763_000_000,
    },
    {
        "id": "ecoregions", "name": "RESOLVE Ecoregions 2017", "licence": "CC-BY 4.0",
        "licence_page": "https://ecoregions.appspot.com/",
        "citation": "Dinerstein, E. et al. (2017): An Ecoregion-Based Approach to Protecting Half the Terrestrial Realm. "
                    "BioScience 67(6): 534-545. doi:10.1093/biosci/bix014.",
        "files": [("https://storage.googleapis.com/teow2016/Ecoregions2017.zip", "ecoregions/Ecoregions2017.zip")],
        "stated_bytes": 150_000_000,
    },
    {
        "id": "glim", "name": "GLiM v1.0, PANGAEA gridded release (0.5 degree)", "licence": "CC-BY 3.0",
        "licence_page": "https://doi.org/10.1594/PANGAEA.788537",
        "citation": "Hartmann, J., Moosdorf, N. (2012): The new global lithological map database GLiM: A representation of rock "
                    "properties at the Earth surface. Geochemistry, Geophysics, Geosystems 13: Q12004. doi:10.1029/2012GC004370.",
        "files": [("https://hdl.handle.net/10013/epic.39939.d001", "glim/glim_gridded_0point5deg.zip")],
        "stated_bytes": 37_800,
    },
    {
        "id": "chelsa", "name": "CHELSA v2.1 climatologies 1981-2010, tas and pr, monthly", "licence": "CC0",
        "licence_page": "https://chelsa-climate.org/downloads/",
        "citation": "Karger, D.N. et al. (2017): Climatologies at high resolution for the earth's land surface areas. "
                    "Scientific Data 4: 170122. doi:10.1038/sdata.2017.122. Version 2.1: doi:10.16904/envidat.228.v2.1.",
        "files": [(CHELSA_BASE + "/%s/CHELSA_%s_%02d_1981-2010_V.2.1.tif" % (v, v, m), "chelsa/CHELSA_%s_%02d_1981-2010_V.2.1.tif" % (v, m))
                  for v in ("tas", "pr") for m in range(1, 13)],
        "stated_bytes": None,
        "ceiling_bytes": CHELSA_CEILING_BYTES,
    },
]


def human(n):
    if n is None:
        return "unknown"
    for unit in ("B", "kB", "MB", "GB"):
        if n < 1024 or unit == "GB":
            return "%.1f %s" % (n, unit) if unit != "B" else "%d B" % n
        n /= 1024.0
    return "%d" % n


def session():
    s = requests.Session()
    s.headers["User-Agent"] = USER_AGENT
    return s


def head(s, url):
    """The server's word on one file: final url, status, size and type. A HEAD refused (405) falls back to a GET of nothing."""
    try:
        r = s.head(url, allow_redirects=True, timeout=TIMEOUT_S)
        if r.status_code in (403, 405):
            r = s.get(url, allow_redirects=True, timeout=TIMEOUT_S, stream=True, headers={"Range": "bytes=0-0"})
            size = r.headers.get("Content-Range", "").split("/")[-1]
            r.close()
            return r.url, r.status_code, int(size) if size.isdigit() else None, r.headers.get("Content-Type", "")
        length = r.headers.get("Content-Length")
        return r.url, r.status_code, int(length) if length and length.isdigit() else None, r.headers.get("Content-Type", "")
    except requests.RequestException as e:
        return url, 0, None, str(e)[:80]


def measure(s, wanted):
    ok = True
    for d in DATASETS:
        if d["id"] not in wanted:
            continue
        total, unknown, bad = 0, 0, 0
        print("== %s (%s): %d file(s)" % (d["name"], d["id"], len(d["files"])))
        for url, rel in d["files"]:
            final, status, size, kind = head(s, url)
            good = 200 <= status < 300 or status == 206
            bad += 0 if good else 1
            if size is None:
                unknown += 1
            else:
                total += size
            print("   %s %3d %10s  %-28s %s%s" % ("ok " if good else "BAD", status, human(size), kind[:28], rel,
                                                   ("  <- " + final) if final != url else ""))
        stated = d.get("stated_bytes")
        note = ("; the manifest states %s" % human(stated)) if stated else "; the manifest states no size"
        ceiling = d.get("ceiling_bytes")
        verdict = ""
        if ceiling:
            verdict = "; UNDER the %s ceiling" % human(ceiling) if total <= ceiling and unknown == 0 else "; ceiling %s NOT proved: %s" % (
                human(ceiling), "over it" if total > ceiling else "%d size(s) unknown" % unknown)
        print("   total %s%s%s%s" % (human(total), " (+%d unknown)" % unknown if unknown else "", note, verdict))
        ok = ok and bad == 0 and (not ceiling or (total <= ceiling and unknown == 0))
    return ok


def sha256_of(path):
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for block in iter(lambda: f.read(CHUNK), b""):
            h.update(block)
    return h.hexdigest()


def load_record():
    return json.loads(MANIFEST_RECORD.read_text(encoding="utf-8")) if MANIFEST_RECORD.is_file() else {"format": "eg2.atlas-manifest", "version": 1, "files": {}}


def save_record(record):
    MANIFEST_RECORD.parent.mkdir(parents=True, exist_ok=True)
    MANIFEST_RECORD.write_text(json.dumps(record, indent=2), encoding="utf-8")


def fetch_one(s, url, path, expected):
    """One file to its path, resumed from a .part when one is there; the server's size checked when it gave one."""
    part = path.with_suffix(path.suffix + ".part")
    path.parent.mkdir(parents=True, exist_ok=True)
    have = part.stat().st_size if part.is_file() else 0
    headers = {"Range": "bytes=%d-" % have} if have else {}
    started = time.time()
    with s.get(url, stream=True, timeout=TIMEOUT_S, headers=headers, allow_redirects=True) as r:
        if have and r.status_code == 200:
            have = 0  # the server ignored the range: start again
        elif have and r.status_code != 206:
            raise RuntimeError("resume refused: %d" % r.status_code)
        if not (200 <= r.status_code < 300):
            raise RuntimeError("status %d" % r.status_code)
        with open(part, "ab" if have else "wb") as f:
            done = have
            for block in r.iter_content(CHUNK):
                f.write(block)
                done += len(block)
    if expected is not None and part.stat().st_size != expected:
        raise RuntimeError("size %d, the server said %d" % (part.stat().st_size, expected))
    part.replace(path)
    return path.stat().st_size, time.time() - started


def fetch(s, wanted):
    record = load_record()
    ok = True
    for d in DATASETS:
        if d["id"] not in wanted:
            continue
        print("== %s" % d["name"])
        if d.get("ceiling_bytes"):
            sizes = [head(s, url)[2] for url, _ in d["files"]]
            if any(x is None for x in sizes) or sum(sizes) > d["ceiling_bytes"]:
                print("   refused: %s against a ceiling of %s (%d unknown)" % (human(sum(x or 0 for x in sizes)), human(d["ceiling_bytes"]), sum(1 for x in sizes if x is None)))
                ok = False
                continue
            print("   %s for %d files, under the %s ceiling" % (human(sum(sizes)), len(sizes), human(d["ceiling_bytes"])))
        for url, rel in d["files"]:
            path = DATA / rel
            if path.is_file() and rel in record["files"]:
                print("   have  %s (%s)" % (rel, human(path.stat().st_size)))
                continue
            final, status, expected, _ = head(s, url)
            try:
                try:
                    size, seconds = fetch_one(s, url, path, expected)
                except requests.ConnectionError:
                    # A server that closes the connection after the HEAD (PANGAEA's handle for GLiM did, 2026-09-21) is asked
                    # once more on a fresh connection before the file is given up on.
                    size, seconds = fetch_one(session(), url, path, expected)
            except (requests.RequestException, RuntimeError, OSError) as e:
                print("   FAILED %s: %s" % (rel, e))
                ok = False
                continue
            digest = sha256_of(path)
            record["files"][rel] = {"dataset": d["id"], "url": url, "final_url": final, "bytes": size, "sha256": digest,
                                    "fetched_utc": datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"),
                                    "licence": d["licence"], "licence_page": d["licence_page"], "licence_read": "2026-09-20"}
            save_record(record)
            print("   fetched %s: %s in %.0f s, sha256 %s" % (rel, human(size), seconds, digest[:16]))
    return ok


def verify():
    record = load_record()
    ok = True
    for rel, entry in sorted(record["files"].items()):
        path = DATA / rel
        if not path.is_file():
            print("MISSING %s" % rel)
            ok = False
            continue
        digest = sha256_of(path)
        same = digest == entry["sha256"] and path.stat().st_size == entry["bytes"]
        ok = ok and same
        print("%s %s (%s)" % ("ok     " if same else "CHANGED", rel, human(path.stat().st_size)))
    print("%d file(s) recorded" % len(record["files"]))
    return ok


def main():
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--measure", action="store_true")
    parser.add_argument("--fetch", nargs="*", metavar="ID")
    parser.add_argument("--verify", action="store_true")
    args = parser.parse_args()
    ids = [d["id"] for d in DATASETS]
    if not (args.measure or args.fetch is not None or args.verify):
        parser.error("say --measure, --fetch or --verify")
    s = session()
    ok = True
    if args.measure:
        ok = measure(s, set(ids)) and ok
    if args.fetch is not None:
        wanted = set(args.fetch) if args.fetch else set(ids)
        unknown = wanted - set(ids)
        if unknown:
            parser.error("not in the manifest: %s (it names %s)" % (", ".join(sorted(unknown)), ", ".join(ids)))
        ok = fetch(s, wanted) and ok
    if args.verify:
        ok = verify() and ok
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
