#!/usr/bin/env python3
"""fetch_ala.py: where Bherwerre's plants have actually been recorded, from the Atlas of Living Australia.

The world grows twelve plants by rule (`PlantSpecies`, `PlantCommunity`); this fetches the occurrence records of
the same twelve inside the region's box, so a check can compare where the rules put each plant with where people
have found it (CANON ruling 21: the world is checked against the real place from published sources). Claude's
tool: it fetches and caches, it verifies nothing; `Tools/verifiers/checks/species_check.py` reads the cache.

The source is the Atlas of Living Australia's occurrence web service (biocache), one query per species filtered
to the box, paged 100 at a time with a pause between pages. Records are kept as the Atlas gives them — latitude,
longitude, year, basis of record, the stated coordinate uncertainty, the data resource and its licence — because
a record's licence belongs to the resource it came from (THIRD_PARTY_NOTICES.md). The binomials are restated
here from `Docs/ECOSYSTEM.md`'s table on purpose: a check reads names it was given, not names the engine holds.

Usage, from the repository root:
    python Tools/data/fetch_ala.py [--refresh]
Writes Data/cache/ala/<Species>.json (Data/ is fetched, never committed). Without --refresh a cached species is
left as it is. Exit 0 when every species was fetched or cached, 1 when a fetch failed.
"""
import argparse
import datetime
import json
import math
import os
import sys
import time
import urllib.parse
import urllib.request

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
CACHE = os.path.join(ROOT, "Data", "cache", "ala")
API = "https://biocache-ws.ala.org.au/ws/occurrences/search"

# Bherwerre's box, as ARCHITECTURE section 3 states it; another region's is worked out from its centre (--centre-lat,
# --centre-lon, --extent-m), padded by nothing, as the bake's own box is.
SOUTH, NORTH, WEST, EAST = -35.1761, -35.1039, 150.6311, 150.7189
EARTH_RADIUS_M = 6371000.0

# The engine's name for each plant, and the binomial ECOSYSTEM.md gives it.
SPECIES = {
    "Blackbutt": "Eucalyptus pilularis",
    "Bangalay": "Eucalyptus botryoides",
    "OldManBanksia": "Banksia serrata",
    "CoastBanksia": "Banksia integrifolia",
    "SwampPaperbark": "Melaleuca ericifolia",
    "GrassTree": "Xanthorrhoea resinosa",
    "HeathBanksia": "Banksia ericifolia",
    "Bracken": "Pteridium esculentum",
    "Lomandra": "Lomandra longifolia",
    "SawSedge": "Gahnia sieberiana",
    "KangarooGrass": "Themeda triandra",
    "Spinifex": "Spinifex sericeus",
}

# The Kangaroo Valley's table as proposed in WG.2 (2026-09-22), fetched to check the proposal against the records (ruling 21):
# the eight of Bherwerre's expected to stay, the four proposed to come in, and the three of the coast's expected to be absent.
SPECIES_BY_REGION = {
    "bherwerre": SPECIES,
    "kangaroo-valley": {
        "Blackbutt": "Eucalyptus pilularis",
        "OldManBanksia": "Banksia serrata",
        "HeathBanksia": "Banksia ericifolia",
        "GrassTree": "Xanthorrhoea resinosa",
        "Bracken": "Pteridium esculentum",
        "Lomandra": "Lomandra longifolia",
        "SawSedge": "Gahnia sieberiana",
        "KangarooGrass": "Themeda triandra",
        "SydneyBlueGum": "Eucalyptus saligna",
        "RiverOak": "Casuarina cunninghamiana",
        "CabbageTreePalm": "Livistona australis",
        "SilvertopAsh": "Eucalyptus sieberi",
        "ScribblyGum": "Eucalyptus sclerophylla",
        "CoastBanksia": "Banksia integrifolia",
        "SwampPaperbark": "Melaleuca ericifolia",
        "Spinifex": "Spinifex sericeus",
    },
}
FIELDS = "decimalLatitude,decimalLongitude,year,basisOfRecord,coordinateUncertaintyInMeters,dataResourceName,license"
# The service answers 503 to a page of 500 and serves a page of 100 (measured 2026-09-10).
PAGE = 100


def query(binomial, start, box):
    south, north, west, east = box
    params = [
        ("q", 'taxa:"%s"' % binomial),
        ("fq", "decimalLatitude:[%s TO %s]" % (south, north)),
        ("fq", "decimalLongitude:[%s TO %s]" % (west, east)),
        ("fl", FIELDS),
        ("pageSize", str(PAGE)),
        ("startIndex", str(start)),
    ]
    url = API + "?" + urllib.parse.urlencode(params)
    # The service answers 503 to a request that does not say it wants JSON (found on the first run, 2026-09-10).
    request = urllib.request.Request(url, headers={"Accept": "application/json",
                                                   "User-Agent": "Mozilla/5.0 (compatible; EarthGame2 Tools/data/fetch_ala.py; research cache)"})
    with urllib.request.urlopen(request, timeout=90) as response:
        return url, json.load(response)


def fetch(key, binomial, box):
    records, start, total, first_url = [], 0, None, None
    while True:
        url, data = query(binomial, start, box)
        first_url = first_url or url
        total = data.get("totalRecords", 0)
        page = data.get("occurrences", []) or []
        for r in page:
            if "decimalLatitude" not in r or "decimalLongitude" not in r:
                continue
            records.append({
                "lat": r["decimalLatitude"], "lon": r["decimalLongitude"], "year": r.get("year"),
                "basis": r.get("basisOfRecord"), "uncertainty_m": r.get("coordinateUncertaintyInMeters"),
                "resource": r.get("dataResourceName"), "license": r.get("license"),
            })
        start += len(page)
        if not page or start >= total:
            break
        time.sleep(1.0)
    return {
        "format": "eg2.ala", "version": 1, "species": key, "binomial": binomial,
        "box": {"south": box[0], "north": box[1], "west": box[2], "east": box[3]},
        "query": first_url, "fetched_utc": datetime.datetime.now(datetime.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"),
        "total_records": total, "records": records,
    }


def main(argv):
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--refresh", action="store_true", help="fetch every species again")
    parser.add_argument("--region", default="bherwerre", choices=sorted(SPECIES_BY_REGION), help="which region's table and cache folder")
    parser.add_argument("--centre-lat", type=float, default=None, help="another region's centre; with --centre-lon and --extent-m the box is worked out from it")
    parser.add_argument("--centre-lon", type=float, default=None)
    parser.add_argument("--extent-m", type=float, default=8000.0)
    args = parser.parse_args(argv[1:])
    if args.centre_lat is not None and args.centre_lon is not None:
        half = args.extent_m / 2.0
        dlat = math.degrees(half / EARTH_RADIUS_M)
        dlon = math.degrees(half / (EARTH_RADIUS_M * math.cos(math.radians(args.centre_lat))))
        box = (round(args.centre_lat - dlat, 4), round(args.centre_lat + dlat, 4), round(args.centre_lon - dlon, 4), round(args.centre_lon + dlon, 4))
    else:
        if args.region != "bherwerre":
            print("the region '%s' needs --centre-lat and --centre-lon" % args.region)
            return 2
        box = (SOUTH, NORTH, WEST, EAST)
    cache = CACHE if args.region == "bherwerre" else os.path.join(CACHE, args.region)
    print("%s: the box south %s north %s west %s east %s, under %s" % (args.region, box[0], box[1], box[2], box[3], cache))
    os.makedirs(cache, exist_ok=True)
    failed = []
    for key, binomial in SPECIES_BY_REGION[args.region].items():
        path = os.path.join(cache, key + ".json")
        if os.path.isfile(path) and not args.refresh:
            print("%-15s cached" % key)
            continue
        try:
            doc = fetch(key, binomial, box)
        except (OSError, ValueError) as error:
            print("%-15s FAILED: %s" % (key, error))
            failed.append(key)
            continue
        with open(path + ".part", "w", encoding="utf-8") as f:
            json.dump(doc, f, indent=1)
        os.replace(path + ".part", path)
        print("%-15s %4d record(s) in the box (%s)" % (key, len(doc["records"]), binomial))
        time.sleep(1.0)
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
