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
import os
import sys
import time
import urllib.parse
import urllib.request

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
CACHE = os.path.join(ROOT, "Data", "cache", "ala")
API = "https://biocache-ws.ala.org.au/ws/occurrences/search"

# The region's box, as ARCHITECTURE section 3 states it.
SOUTH, NORTH, WEST, EAST = -35.1761, -35.1039, 150.6311, 150.7189

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
FIELDS = "decimalLatitude,decimalLongitude,year,basisOfRecord,coordinateUncertaintyInMeters,dataResourceName,license"
# The service answers 503 to a page of 500 and serves a page of 100 (measured 2026-09-10).
PAGE = 100


def query(binomial, start):
    params = [
        ("q", 'taxa:"%s"' % binomial),
        ("fq", "decimalLatitude:[%s TO %s]" % (SOUTH, NORTH)),
        ("fq", "decimalLongitude:[%s TO %s]" % (WEST, EAST)),
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


def fetch(key, binomial):
    records, start, total, first_url = [], 0, None, None
    while True:
        url, data = query(binomial, start)
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
        "box": {"south": SOUTH, "north": NORTH, "west": WEST, "east": EAST},
        "query": first_url, "fetched_utc": datetime.datetime.now(datetime.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"),
        "total_records": total, "records": records,
    }


def main(argv):
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--refresh", action="store_true", help="fetch every species again")
    args = parser.parse_args(argv[1:])
    os.makedirs(CACHE, exist_ok=True)
    failed = []
    for key, binomial in SPECIES.items():
        path = os.path.join(CACHE, key + ".json")
        if os.path.isfile(path) and not args.refresh:
            print("%-15s cached" % key)
            continue
        try:
            doc = fetch(key, binomial)
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
