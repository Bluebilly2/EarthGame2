#!/usr/bin/env python3
"""atlas_check.py: the coordinate atlas's derived layers (WG.0b promise 3) read at a handful of places whose answers are known
by other means, each reference and its source named, the two numbers printed beside every verdict.

The references are independent of Tools/atlas/derive.py and of the datasets it read: the Bureau of Meteorology's own record
for Point Perpendicular lighthouse (Data/stations, copied from its page on 2026-09-16), and facts of the country that any
reference work gives — Mount Kosciuszko's summit (2228 m), Lake Eyre's bed (about 15 m below the sea), the Nullarbor's
limestone, the forest Nowra stands in, Alice Springs' distance from any coast.

Usage, from the repository root:
    python Tools/verifiers/checks/atlas_check.py [--atlas Data/global/atlas]
Exit 0 when every check passes; 1 otherwise.
"""
import argparse
import json
import re
import sys
from pathlib import Path

import numpy as np

ROOT = Path(__file__).resolve().parents[3]


class Layer:
    def __init__(self, folder, name):
        self.head = json.loads((folder / (name + ".json")).read_text(encoding="utf-8"))
        dtype = {"int16": "<i2", "uint16": "<u2", "uint8": "u1"}[self.head["dtype"]]
        self.array = np.fromfile(folder / self.head["raw"], dtype=dtype).reshape(self.head["height"], self.head["width"])
        self.per = self.head["cells_per_degree"]
        self.nodata = self.head.get("nodata")

    def at(self, lat, lon):
        r = min(self.head["height"] - 1, int((90.0 - lat) * self.per))
        c = min(self.head["width"] - 1, int((lon + 180.0) * self.per))
        return int(self.array[r, c])

    def highest_within(self, lat, lon, km):
        cells = int(km / (111.195 / self.per)) + 1
        r = int((90.0 - lat) * self.per)
        c = int((lon + 180.0) * self.per)
        window = self.array[max(r - cells, 0):r + cells + 1, max(c - cells, 0):c + cells + 1]
        return int(window.max())


def bureau_rows(path):
    """The lighthouse's monthly mean rainfall, mean maximum and mean minimum temperature from the Bureau's text, Jan..Dec."""
    lines = path.read_text(encoding="utf-8").splitlines()
    want = {"Mean rainfall (mm)": "rain", "Mean maximum temperature (°C)": "tmax", "Mean minimum temperature (°C)": "tmin"}
    found = {}
    for i, line in enumerate(lines):
        key = line.strip()
        if key in want and want[key] not in found and i + 1 < len(lines):
            nums = re.findall(r"-?\d+\.?\d*", lines[i + 1])
            if len(nums) >= 12:
                found[want[key]] = [float(x) for x in nums[:12]]
    return found


def site(path):
    """A Bureau station's own latitude, longitude and elevation from its site-details line: 'Latitude: 34.95 S ... Elevation: 109 m'."""
    text = path.read_text(encoding="utf-8")
    m = re.search(r"Latitude:\s*([0-9.]+)\D*([NS]).*?Longitude:\s*([0-9.]+)\D*([EW]).*?Elevation:\s*([0-9.]+)\s*m", text, re.S)
    lat = float(m.group(1)) * (-1 if m.group(2) == "S" else 1)
    lon = float(m.group(3)) * (-1 if m.group(4) == "W" else 1)
    return lat, lon, float(m.group(5))


def check_probes(folder, verdict):
    """
    The eight pinned probes (WG.0 promise 3, Tools/atlas/probes.json, written before the lookup existed) read here by this
    verifier's own Layer class, never by choose.py: each expectation printed beside what the layers hold. A missing field is
    an answer and is checked as one; it is never a zero.
    """
    probes = json.loads((ROOT / "Tools/atlas/probes.json").read_text(encoding="utf-8"))["probes"]
    elevation, eco, lith = Layer(folder, "elevation"), Layer(folder, "ecoregion"), Layer(folder, "lithology")
    coast, lake = Layer(folder, "coast_km"), Layer(folder, "lake_km")
    names = json.loads((folder / "ecoregion_names.json").read_text(encoding="utf-8"))
    rain = [Layer(folder, "pr_%02d" % m) for m in range(1, 13)]
    for pr in probes:
        lat, lon, want, src = pr["lat"], pr["lon"], pr["expect"], pr["source"]
        tag = "probe %s (%.3f, %.3f)" % (pr["name"], lat, lon)
        e = elevation.at(lat, lon)
        if want["elevation_m"] == "missing":
            verdict(tag + ": elevation missing", e == elevation.nodata, "missing", str(e), src)
        else:
            lo, hi = want["elevation_m"]
            verdict(tag + ": elevation in [%d, %d] m" % (lo, hi), e != elevation.nodata and lo <= e <= hi, "%d to %d m" % (lo, hi), "%d m" % e, src)
        months = [r.at(lat, lon) for r in rain]
        present = any(v != rain[0].nodata for v in months)
        verdict(tag + ": climate " + want["climate"], present == (want["climate"] == "present"), want["climate"], "present" if present else "missing", src)
        if want["climate"] == "present" and "rain_year_mm" in want:
            lo, hi = want["rain_year_mm"]
            year = sum(v for v in months if v != rain[0].nodata) / 10.0
            verdict(tag + ": the year's rain in [%d, %d] mm" % (lo, hi), lo <= year <= hi, "%d to %d mm" % (lo, hi), "%.0f mm" % year, src)
        region = eco.at(lat, lon)
        name = names.get(str(region), {}).get("eco_name") or "none"
        if want["ecoregion"] == "none":
            verdict(tag + ": no ecoregion", region == 0, "none", name, src)
        else:
            verdict(tag + ": ecoregion named like '%s'" % want["ecoregion"], want["ecoregion"].lower() in name.lower(), want["ecoregion"], name, src)
        cls = lith.at(lat, lon)
        cls_name = lith.head["classes"].get(str(cls), "none")
        if want["lithology"] == "none":
            verdict(tag + ": no lithology", cls == 0, "none", cls_name, src)
        else:
            verdict(tag + ": lithology %s" % want["lithology"], cls_name.startswith(want["lithology"]), want["lithology"], cls_name, src)
        lo, hi = want["coast_km"]
        verdict(tag + ": the coast in [%d, %d] km" % (lo, hi), lo <= coast.at(lat, lon) <= hi, "%d to %d km" % (lo, hi), "%d km" % coast.at(lat, lon), src)
        lo, hi = want["lake_km"]
        verdict(tag + ": a lake in [%d, %d] km" % (lo, hi), lo <= lake.at(lat, lon) <= hi, "%d to %d km" % (lo, hi), "%d km" % lake.at(lat, lon), src)


def main():
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--atlas", default="Data/global/atlas")
    parser.add_argument("--probes", action="store_true", help="also the eight pinned probes of Tools/atlas/probes.json")
    args = parser.parse_args()
    folder = ROOT / args.atlas
    failed = 0

    def verdict(name, ok, reference, measured, source):
        nonlocal failed
        failed += 0 if ok else 1
        print("%s %s: reference %s (%s); atlas %s" % ("PASS" if ok else "FAIL", name, reference, source, measured))

    # 1. Point Perpendicular lighthouse (-35.09, 150.80): the Bureau's monthly means against CHELSA's, at the atlas's 1/6 degree.
    station = ROOT / "Data/stations/068034_point_perpendicular_lighthouse_all_years.txt"
    rows = bureau_rows(station)
    lat, lon = -35.09, 150.80
    if "rain" in rows:
        atlas_rain = [Layer(folder, "pr_%02d" % m).at(lat, lon) / 10.0 for m in range(1, 13)]
        year_ref, year_atlas = sum(rows["rain"]), sum(atlas_rain)
        verdict("the lighthouse's year of rain within a quarter", abs(year_atlas - year_ref) <= 0.25 * year_ref,
                "%.0f mm" % year_ref, "%.0f mm" % year_atlas, "Bureau of Meteorology site 068034, all years 1899-2004; CHELSA is 1981-2010 at 1 km, the atlas a 1/6 degree mean")
        wet_ref = rows["rain"].index(max(rows["rain"])); wet_atlas = atlas_rain.index(max(atlas_rain))
        verdict("the wettest month within two of the Bureau's", abs(wet_ref - wet_atlas) <= 2 or abs(wet_ref - wet_atlas) >= 10,
                "month %d" % (wet_ref + 1), "month %d" % (wet_atlas + 1), "the same record")
    if "tmax" in rows and "tmin" in rows:
        for m in (1, 7):
            t = Layer(folder, "tas_%02d" % m).at(lat, lon) / 10.0
            lo, hi = rows["tmin"][m - 1], rows["tmax"][m - 1]
            verdict("the lighthouse's mean temperature in month %d between the Bureau's mean minimum and maximum, a degree either way" % m,
                    lo - 1.0 <= t <= hi + 1.0, "%.1f to %.1f degC" % (lo, hi), "%.1f degC" % t, "Bureau of Meteorology site 068034")

    # 2. Elevation: a summit's cell within 20 km reaches most of its height; a lake bed below the sea; the lighthouse's cell on land.
    elevation = Layer(folder, "elevation")
    kos = elevation.highest_within(-36.456, 148.263, 20.0)
    verdict("Mount Kosciuszko: the highest 1/12 degree cell within 20 km over 1700 m", kos >= 1700, "2228 m at the summit", "%d m" % kos, "Geoscience Australia")
    eyre = elevation.at(-28.4, 137.3)
    verdict("Lake Eyre's bed below the sea and above -40 m", -40 <= eyre <= 0, "about -15 m", "%d m" % eyre, "Geoscience Australia")
    # The lighthouse stands on a headland's tip, and a 9 km cell there is mostly sea; Nowra's station is inland on the plain.
    nowra = ROOT / "Data/stations/068072_nowra_ran_air_station_aws_all_years.txt"
    n_lat, n_lon, n_up = site(nowra)
    n_cell = elevation.at(n_lat, n_lon)
    verdict("Nowra's cell on land, no higher than the station's elevation plus 150 m", 0 < n_cell <= n_up + 150, "%.0f m at the station" % n_up, "%d m" % n_cell, "Bureau of Meteorology site 068072 details")

    # 3. Lithology: the Nullarbor is limestone (GLiM's sc, carbonate sedimentary rocks).
    lith = Layer(folder, "lithology")
    nullarbor = lith.at(-31.0, 129.0)
    name = lith.head["classes"].get(str(nullarbor), "?")
    verdict("the Nullarbor's rock is carbonate sedimentary", name.startswith("sc"), "limestone (GLiM sc)", "class %d %s" % (nullarbor, name), "Geoscience Australia: the Nullarbor Plain is a limestone karst")

    # 4. Ecoregion: Nowra stands in one of RESOLVE's temperate forests of eastern Australia; the mid-Pacific in none.
    eco = Layer(folder, "ecoregion")
    names = json.loads((folder / "ecoregion_names.json").read_text(encoding="utf-8"))
    jb = eco.at(n_lat, n_lon)
    jb_name = names.get(str(jb), {}).get("eco_name", "none")
    verdict("Nowra's ecoregion is one of eastern Australia's temperate forests", re.search(r"Australian? temperate forests", jb_name) is not None, "Southeast Australia or Eastern Australian temperate forests", "%d %s" % (jb, jb_name), "RESOLVE Ecoregions 2017, Dinerstein et al.: the NSW south coast")
    verdict("the mid-Pacific (0, -150) is in no ecoregion", eco.at(0.0, -150.0) == 0, "none", str(eco.at(0.0, -150.0)), "the sea")

    # 5. Distances: the lighthouse a few km from the coast, Alice Springs hundreds; Lake Eyre's centre is a lake.
    coast = Layer(folder, "coast_km")
    verdict("the lighthouse within 15 km of the coast", coast.at(lat, lon) <= 15, "on the coast", "%d km" % coast.at(lat, lon), "the site itself")
    alice = coast.at(-23.7, 133.88)
    verdict("Alice Springs over 600 km from any coast", alice >= 600, "about 1,100 km to the nearest coast", "%d km" % alice, "any atlas of Australia")
    lake = Layer(folder, "lake_km")
    verdict("Lake Eyre's centre within 10 km of a lake", lake.at(-28.4, 137.3) <= 10, "0 km", "%d km" % lake.at(-28.4, 137.3), "Natural Earth's lakes include Lake Eyre")
    verdict("Alice Springs over 100 km from any lake", lake.at(-23.7, 133.88) >= 100, "no lake near", "%d km" % lake.at(-23.7, 133.88), "any atlas of Australia")

    if args.probes:
        check_probes(folder, verdict)
    print("atlas_check: %s" % ("GREEN" if failed == 0 else "RED, %d failed" % failed))
    return 0 if failed == 0 else 1


if __name__ == "__main__":
    sys.exit(main())
