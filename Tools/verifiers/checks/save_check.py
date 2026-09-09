#!/usr/bin/env python3
"""save_check.py: does the world folder hold what the server says it holds?

Reads a world folder with Python alone, by the formats ARCHITECTURE.md section 10 states and nothing else:
world.json (eg2.world version 1: the clock's total hours, the tick, next_entity_id, the extent), digest.txt (the
world's name as the server wrote it), players/*.egp (eg2.player version 2: magic EG2P, u16 version, the name as
a u16 UTF-8 byte length and the bytes, f64 east, up, north, f32 yaw, f32 pitch, u8 flags with 1 grounded,
2 wading, 4 crouching, i64 saved tick, then u32 CRC-32 of everything before it) and any players/*.json left
from version 1, and regions/r.X.Y.egr (eg2.region version 1: magic EG2R, u16 version, i32 cell x, i32 cell z,
f64 cell size, u32 entity count, u32 layer-diff count, u32 CRC-32 of the records, then per entity u64 id, the
key as a u16 UTF-8 byte length and the bytes, f64 east, up, north, f32 yaw, i64 spawn tick, u8 component mask
with 1 = item, and for an item u8 resting and f32 fall speed). Then it rebuilds the world's name from the
lines WorldDigest states — clock <nanohours>, tick <n>, one line per body sorted by name (<name> <micrometres
east> <up> <north> g|a w|d c|s), one line per entity in id order (entity <id> <key> <micrometres east> <up>
<north> <microdegrees yaw>[ item r|f <micrometres per second>]), next_entity <n> — hashed with FNV-1a 64, every
number a whole count of its resolution rounded half to even, and prints it beside the server's.

Rows, each with both numbers:
  1. every region file's CRC matches its records;
  2. every region file is the cell its name says, and every entity in it lies in that cell (512 m cells from the
     south-west corner);
  3. every player file's CRC matches, and no name has both a version-1 and a version-2 file;
  4. the recomputed digest equals digest.txt.
Independent of the tool by implementation: the server's C# writer and digest are never imported; the layouts
and the lines are restated here from the architecture, so a writer that drifts from its stated format is caught
by a reader that did not drift with it.

Exit 0 when every row passes, 1 when any fails, 2 when the folder is missing.
Run from the repository root:  python Tools/verifiers/checks/save_check.py [world folder]
(default Artefacts/worlds/gate, the folder Tools/world/populate.py leaves behind).
"""
import glob
import json
import math
import os
import struct
import sys
import zlib

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))))
DEFAULT_WORLD = os.path.join("Artefacts", "worlds", "gate")
CELL_M = 512.0
METRE = 1e-6
HOUR = 1e-9
DEGREE = 1e-6
REGION_HEADER = struct.Struct("<4sHiidIII")
COMPONENT_ITEM = 1


def fixed(value, resolution):
    """The value as a whole number of resolution units, rounded half to even (Python's round is banker's rounding)."""
    if math.isnan(value):
        return "nan"
    if math.isinf(value):
        return "inf" if value > 0 else "-inf"
    return str(int(round(value / resolution)))


def fnv1a64(text):
    h = 14695981039346656037
    for b in text.encode("utf-8"):
        h ^= b
        h = (h * 1099511628211) & 0xFFFFFFFFFFFFFFFF
    return "%016x" % h


def read_string(data, at):
    (n,) = struct.unpack_from("<H", data, at)
    return data[at + 2:at + 2 + n].decode("utf-8"), at + 2 + n


def cell_index(coordinate, extent_m):
    count = max(1, int(math.ceil(extent_m / CELL_M - 1e-9)))
    i = int(math.floor((coordinate + extent_m / 2.0) / CELL_M))
    return min(count - 1, max(0, i))


def read_region(path):
    data = open(path, "rb").read()
    magic, version, cx, cz, cell, count, diffs, crc = REGION_HEADER.unpack_from(data, 0)
    if magic != b"EG2R":
        raise ValueError("%s: not a region file" % path)
    if version != 1:
        raise ValueError("%s: version %d" % (path, version))
    records = data[REGION_HEADER.size:]
    actual = zlib.crc32(records) & 0xFFFFFFFF
    entities = []
    at = 0
    for _ in range(count):
        (eid,) = struct.unpack_from("<Q", records, at)
        at += 8
        key, at = read_string(records, at)
        east, up, north, yaw, spawn_tick, mask = struct.unpack_from("<dddfqB", records, at)
        at += 8 * 3 + 4 + 8 + 1
        item = None
        if mask & COMPONENT_ITEM:
            resting, fall = struct.unpack_from("<Bf", records, at)
            at += 5
            item = (resting != 0, fall)
        entities.append({"id": eid, "key": key, "east": east, "up": up, "north": north, "yaw": yaw, "spawn_tick": spawn_tick, "item": item})
    if at != len(records):
        raise ValueError("%s: %d bytes left over after %d records" % (path, len(records) - at, count))
    return {"cell": (cx, cz), "cell_m": cell, "diffs": diffs, "crc_stated": crc, "crc_actual": actual, "entities": entities}


def read_player_v2(path):
    data = open(path, "rb").read()
    body, (stated,) = data[:-4], struct.unpack_from("<I", data, len(data) - 4)
    actual = zlib.crc32(body) & 0xFFFFFFFF
    if data[:4] != b"EG2P":
        raise ValueError("%s: not a player file" % path)
    (version,) = struct.unpack_from("<H", data, 4)
    if version != 2:
        raise ValueError("%s: version %d" % (path, version))
    name, at = read_string(data, 6)
    east, up, north, yaw, pitch, flags, tick = struct.unpack_from("<dddffBq", data, at)
    return {"name": name, "east": east, "up": up, "north": north, "grounded": bool(flags & 1), "wading": bool(flags & 2), "crouching": bool(flags & 4),
            "crc_stated": stated, "crc_actual": actual}


def read_player_v1(path):
    doc = json.load(open(path, encoding="utf-8"))
    return {"name": doc["name"], "east": doc["east"], "up": doc["up"], "north": doc["north"], "grounded": bool(doc.get("grounded", False)),
            "wading": False, "crouching": False, "crc_stated": 0, "crc_actual": 0}


def main(argv):
    world = argv[1] if len(argv) > 1 else DEFAULT_WORLD
    full = os.path.join(ROOT, world)
    world_json = os.path.join(full, "world.json")
    digest_path = os.path.join(full, "digest.txt")
    if not (os.path.isfile(world_json) and os.path.isfile(digest_path)):
        print("no world.json and digest.txt under %s" % full)
        return 2
    doc = json.load(open(world_json, encoding="utf-8"))
    extent = float(doc.get("extent_m", 0.0))
    failures = []

    def expect(name, ok, detail):
        print("%-40s %s  %s" % (name, "ok " if ok else "FAIL", detail))
        if not ok:
            failures.append(name)

    # 1 and 2: the region files.
    regions = sorted(glob.glob(os.path.join(full, "regions", "r.*.egr")))
    entities = []
    crc_bad, misplaced, named_wrong = [], [], []
    for path in regions:
        name = os.path.basename(path)
        region = read_region(path)
        if region["crc_stated"] != region["crc_actual"]:
            crc_bad.append("%s stated %08x actual %08x" % (name, region["crc_stated"], region["crc_actual"]))
        parts = name[2:-4].split(".")
        if (int(parts[0]), int(parts[1])) != region["cell"]:
            named_wrong.append("%s holds cell %s" % (name, region["cell"]))
        for e in region["entities"]:
            if extent > 0 and (cell_index(e["east"], extent), cell_index(e["north"], extent)) != region["cell"]:
                misplaced.append("entity %d at (%.1f, %.1f) in %s" % (e["id"], e["east"], e["north"], name))
        entities.extend(region["entities"])
    expect("every region file's CRC matches", not crc_bad, "%d files, %d entities; %s" % (len(regions), len(entities), "; ".join(crc_bad) or "all match"))
    expect("every entity lies in the cell its file names", not misplaced and not named_wrong,
           "; ".join(misplaced + named_wrong) or "%d entities in %d cells of %.0f m" % (len(entities), len(regions), CELL_M))

    # 3: the players.
    players = {}
    dup = []
    for path in sorted(glob.glob(os.path.join(full, "players", "*.egp"))):
        p = read_player_v2(path)
        players[p["name"]] = p
    for path in sorted(glob.glob(os.path.join(full, "players", "*.json"))):
        p = read_player_v1(path)
        if p["name"] in players:
            dup.append(p["name"])
        else:
            players[p["name"]] = p
    crc_players = [p["name"] for p in players.values() if p["crc_stated"] != p["crc_actual"]]
    expect("every player file's CRC matches, one file a name", not crc_players and not dup,
           "%d players (%d version 2); bad CRC: %s; twice: %s" % (len(players), sum(1 for p in players.values() if p["crc_stated"]), ", ".join(crc_players) or "none", ", ".join(dup) or "none"))

    # 4: the digest, rebuilt from the stated lines.
    lines = ["clock %s" % fixed(float(doc["clock"]["total_hours"]), HOUR), "tick %d" % int(doc["tick"])]
    for name in sorted(players):
        p = players[name]
        lines.append("%s %s %s %s %s %s %s" % (name, fixed(p["east"], METRE), fixed(p["up"], METRE), fixed(p["north"], METRE),
                                                 "g" if p["grounded"] else "a", "w" if p["wading"] else "d", "c" if p["crouching"] else "s"))
    for e in sorted(entities, key=lambda e: e["id"]):
        line = "entity %d %s %s %s %s %s" % (e["id"], e["key"], fixed(e["east"], METRE), fixed(e["up"], METRE), fixed(e["north"], METRE), fixed(e["yaw"], DEGREE))
        if e["item"] is not None:
            line += " item %s %s" % ("r" if e["item"][0] else "f", fixed(e["item"][1], METRE))
        lines.append(line)
    lines.append("next_entity %d" % int(doc.get("next_entity_id", 1)))
    recomputed = fnv1a64("\n".join(lines) + "\n")
    written = open(digest_path, encoding="utf-8").read().strip()
    expect("the digest rebuilt by hand equals the server's", recomputed == written, "here %s, digest.txt %s (%d lines)" % (recomputed, written, len(lines)))

    if failures:
        print("save_check: FAIL (%s)" % ", ".join(failures))
        return 1
    print("save_check: ok, the folder holds what the server says it holds")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
