#!/usr/bin/env python3
"""choose.py: the atlas inspection tool (WG.0 promise 2). Given a latitude and a longitude, reports what the atlas's own layers
(WG.0b, Data/global/atlas/) say of that place, every field with its source and version, its unit, the source's resolution
and the atlas's cell, and whether the field is missing there. It never substitutes a default region for an unknown place:
a cell with nothing in it is said to have nothing.

With --probes it runs the eight pinned probes of Tools/atlas/probes.json, whose coordinates and expectations were written
before this lookup existed, and writes a report (eg2.atlas_probes, ARCHITECTURE §10) beside the answer; the judging of
that report against the expectations is atlas_check.py's, by its own reading of the layers.

Claude's tool: it reads and reports; it judges nothing.

Usage, from the repository root:
    python Tools/atlas/choose.py --lat -35.09 --lon 150.80
    python Tools/atlas/choose.py --probes [--report Artefacts/atlas/probes-<stamp>.json]
Exit 0 when every field asked for could be read (missing data is an answer, not a failure); 2 on a bad coordinate; 1 when a
layer the atlas should hold is absent.
"""
import argparse
import json
import sys
import time
from datetime import datetime, timezone
from pathlib import Path

import numpy as np

ROOT = Path(__file__).resolve().parents[2]
ATLAS = ROOT / "Data/global/atlas"

MONTHS = ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"]


class Atlas:
    """The atlas's layers opened once, memory-mapped, and read at a coordinate."""

    def __init__(self, folder=ATLAS):
        self.folder = Path(folder)
        if not (self.folder / "elevation.json").is_file():
            raise FileNotFoundError("no atlas at %s; run Tools/atlas/derive.py first" % self.folder)
        self.layers = {}
        self.eco_names = json.loads((self.folder / "ecoregion_names.json").read_text(encoding="utf-8"))

    def layer(self, name):
        if name not in self.layers:
            head = json.loads((self.folder / (name + ".json")).read_text(encoding="utf-8"))
            dtype = {"int16": "<i2", "uint16": "<u2", "uint8": "u1"}[head["dtype"]]
            array = np.memmap(self.folder / head["raw"], dtype=dtype, mode="r", shape=(head["height"], head["width"]))
            self.layers[name] = (head, array)
        return self.layers[name]

    def cell(self, name, lat, lon):
        head, array = self.layer(name)
        per = head["cells_per_degree"]
        r = min(head["height"] - 1, max(0, int((90.0 - lat) * per)))
        c = min(head["width"] - 1, max(0, int((lon + 180.0) * per)))
        return head, int(array[r, c])

    def field(self, name, lat, lon, unit_scale=1.0, unit=None):
        head, value = self.cell(name, lat, lon)
        nodata = head.get("nodata")
        missing = nodata is not None and value == nodata
        return {
            "value": None if missing else value * unit_scale,
            "unit": unit or head["unit"],
            "missing": missing,
            "source": head["source"],
            "derived_from": head.get("derived_from"),
            "atlas_cell_deg": 1.0 / head["cells_per_degree"],
            "derived_at_utc": head.get("derived_at_utc"),
        }

    def describe(self, lat, lon):
        """Every field the atlas holds at a place, with its provenance, in a dict a report can carry."""
        if not (-90.0 <= lat <= 90.0) or not (-180.0 <= lon <= 180.0):
            raise ValueError("a coordinate is a latitude in [-90, 90] and a longitude in [-180, 180]; got %s, %s" % (lat, lon))
        out = {"lat": lat, "lon": lon}
        out["elevation_m"] = self.field("elevation", lat, lon)
        out["elevation_m"]["source_resolution"] = "30 arc-seconds (about 900 m)"
        out["rain_mm"] = {}
        out["temperature_degC"] = {}
        for m in range(1, 13):
            out["rain_mm"][MONTHS[m - 1]] = self.field("pr_%02d" % m, lat, lon, 0.1, "mm a month")
            out["temperature_degC"][MONTHS[m - 1]] = self.field("tas_%02d" % m, lat, lon, 0.1, "degC")
        for k in ("rain_mm", "temperature_degC"):
            for f in out[k].values():
                f["source_resolution"] = "30 arc-seconds (about 1 km), 1981-2010"
        head, cls = self.cell("lithology", lat, lon)
        out["lithology"] = {"value": None if cls == 0 else cls, "name": head["classes"].get(str(cls)), "missing": cls == 0, "unit": "GLiM class",
                            "source": head["source"], "source_resolution": "half a degree", "atlas_cell_deg": 1.0 / head["cells_per_degree"], "derived_from": head.get("derived_from")}
        head, eco = self.cell("ecoregion", lat, lon)
        names = self.eco_names.get(str(eco), {})
        out["ecoregion"] = {"value": None if eco == 0 else eco, "name": names.get("eco_name"), "biome": names.get("biome"), "realm": names.get("realm"),
                            "missing": eco == 0, "unit": "RESOLVE ECO_ID + 1", "source": head["source"], "source_resolution": "vector, rasterised at the cell's centre",
                            "atlas_cell_deg": 1.0 / head["cells_per_degree"], "derived_from": head.get("derived_from")}
        out["coast_km"] = self.field("coast_km", lat, lon)
        out["coast_km"]["source_resolution"] = "1:10m vector, a chamfer on the grid"
        out["lake_km"] = self.field("lake_km", lat, lon)
        out["lake_km"]["source_resolution"] = "1:10m vector, a chamfer on the grid"
        _, land = self.cell("coast_km", lat, lon)
        return out


def print_answer(answer):
    print("at %.4f, %.4f:" % (answer["lat"], answer["lon"]))
    e = answer["elevation_m"]
    print("  elevation: %s (%s; %s; source %s)" % ("missing" if e["missing"] else "%d m" % e["value"], e["source_resolution"], "atlas cell 1/%d deg" % round(1 / e["atlas_cell_deg"]), e["source"][:60]))
    rain = answer["rain_mm"]; temp = answer["temperature_degC"]
    if all(f["missing"] for f in rain.values()):
        print("  climate: missing (CHELSA has no land here)")
    else:
        print("  rain, mm a month:        " + " ".join("%s %4.0f" % (m, rain[m]["value"] if not rain[m]["missing"] else float("nan")) for m in MONTHS))
        print("  temperature, degC:       " + " ".join("%s %4.1f" % (m, temp[m]["value"] if not temp[m]["missing"] else float("nan")) for m in MONTHS))
        print("  (CHELSA V2.1 1981-2010 at about 1 km; atlas cell 1/6 deg)")
    l = answer["lithology"]
    print("  rock: %s (GLiM at half a degree)" % ("missing" if l["missing"] else "%s" % l["name"]))
    r = answer["ecoregion"]
    print("  ecoregion: %s (RESOLVE 2017 at the cell's centre)" % ("none" if r["missing"] else "%s; %s; %s" % (r["name"], r["biome"], r["realm"])))
    print("  the coast: %d km; the nearest lake: %d km (Natural Earth 1:10m, a chamfer on the 1/12 deg grid)" % (answer["coast_km"]["value"], answer["lake_km"]["value"]))


def main():
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--lat", type=float)
    parser.add_argument("--lon", type=float)
    parser.add_argument("--probes", action="store_true", help="run the eight pinned probes of Tools/atlas/probes.json and write a report")
    parser.add_argument("--report", default=None)
    parser.add_argument("--atlas", default=str(ATLAS))
    args = parser.parse_args()
    try:
        atlas = Atlas(args.atlas)
    except FileNotFoundError as e:
        print(e)
        return 1
    if args.probes:
        probes = json.loads((ROOT / "Tools/atlas/probes.json").read_text(encoding="utf-8"))
        report = {"format": "eg2.atlas_probes", "version": 1, "atlas": str(Path(args.atlas)), "run_utc": datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"), "probes": []}
        cold = time.perf_counter()
        for probe in probes["probes"]:
            t0 = time.perf_counter()
            answer = atlas.describe(probe["lat"], probe["lon"])
            answer["name"] = probe["name"]
            answer["query_ms"] = (time.perf_counter() - t0) * 1000.0
            report["probes"].append(answer)
            print_answer(answer)
        report["cold_ms_all"] = (time.perf_counter() - cold) * 1000.0
        warm = time.perf_counter()
        for probe in probes["probes"]:
            atlas.describe(probe["lat"], probe["lon"])
        report["warm_ms_all"] = (time.perf_counter() - warm) * 1000.0
        out = Path(args.report) if args.report else ROOT / "Artefacts/atlas" / ("probes-" + datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%SZ") + ".json")
        out.parent.mkdir(parents=True, exist_ok=True)
        out.write_text(json.dumps(report, indent=1), encoding="utf-8")
        print("%d probes; cold %.0f ms, warm %.0f ms; report %s" % (len(report["probes"]), report["cold_ms_all"], report["warm_ms_all"], out))
        return 0
    if args.lat is None or args.lon is None:
        parser.print_help()
        return 2
    try:
        answer = atlas.describe(args.lat, args.lon)
    except ValueError as e:
        print(e)
        return 2
    print_answer(answer)
    return 0


if __name__ == "__main__":
    sys.exit(main())
