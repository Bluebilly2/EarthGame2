#!/usr/bin/env python3
"""save_check.py: does the world folder hold what the server says it holds?

Reads a world folder with Python alone, by the formats ARCHITECTURE.md section 10 states and nothing else:
world.json (eg2.world version 1: the clock's total hours, the tick, next_entity_id, the extent), digest.txt (the
world's name as the server wrote it), players/*.egp (eg2.player version 2 or 3: magic EG2P, u16 version, the name
as a u16 UTF-8 byte length and the bytes, f64 east, up, north, f32 yaw, f32 pitch, u8 flags with 1 grounded,
2 wading, 4 crouching, i64 saved tick; version 3 (M1.5a) then the hand's place as a u8 and what is carried as a u8
count and, per thing, u8 place, u64 id, the key as a u16 UTF-8 byte length and the bytes, and i64 spawn tick; then
u32 CRC-32 of everything before it) and any players/*.json left from version 1, regions/r.X.Y.egr (eg2.region
version 1 or 2: magic EG2R, u16 version, i32 cell x, i32 cell z, f64 cell size, u32 entity count, u32 layer-diff
count, none in version 1, u32 CRC-32 of the records, then per entity u64 id, the key as a u16 UTF-8 byte length and
the bytes, f64 east, up, north, f32 yaw, i64 spawn tick, u8 component mask with 1 = item, and for an item u8
resting and f32 fall speed; then per diff u8 layer, 5 for the loose layer, u16 row, u16 col, u16 sticks taken and
u16 cobbles taken, a bit for each index), and layers/loose.json with its raw bytes (a u8 per cell of the world's
raster, row 0 north: the low four bits the sticks lying on the cell, the high four its cobbles). Then it rebuilds
the world's name from the lines WorldDigest states — clock <nanohours>, tick <n>, one line per body sorted by name
(<name> <micrometres east> <up> <north> g|a w|d c|s), then for each founder by name who carries something or has
chosen a hand, hands <name> <place> and one line per thing by place (carried <name> <place> <id> <key>), one line
per entity in id order (entity <id> <key> <micrometres east> <up> <north> <microdegrees yaw>[ item r|f <micrometres
per second>]), one line per cell something was taken from, by row and then column (taken <row> <col> <sticks mask>
<cobbles mask>), next_entity <n> — hashed with FNV-1a 64, every number a whole count of its resolution rounded half
to even, and prints it beside the server's.

Rows, each with both numbers:
  0. the folder holds one whole save: no file written aside (.part) and no save's record (save.commit) left over, which
     only a save a crash stopped leaves, for the next load to finish or clear (M1.3b);
  1. every region file's CRC matches its records;
  2. every region file is the cell its name says, and every entity in it lies in that cell (512 m cells from the
     south-west corner);
  3. every player file's CRC matches, and no name has both a version-1 and a binary file;
  4. every carried thing is out of the world: its id lies in no region file, is below next_entity_id, and is
     carried by one founder only;
  5. every taking names only things its cell of the loose layer held, and is kept in the region file of the 512 m
     cell its raster cell's centre falls in;
  6. the recomputed digest equals digest.txt.
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
LAYER_LOOSE = 5


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
    if version not in (1, 2):
        raise ValueError("%s: version %d" % (path, version))
    if version == 1 and diffs != 0:
        raise ValueError("%s: a version-1 region file with %d layer diffs" % (path, diffs))
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
    taken = []
    for _ in range(diffs):
        layer, row, col, sticks, cobbles = struct.unpack_from("<BHHHH", records, at)
        at += 9
        if layer != LAYER_LOOSE:
            raise ValueError("%s: a diff of layer %d" % (path, layer))
        taken.append({"row": row, "col": col, "sticks": sticks, "cobbles": cobbles, "file_cell": (cx, cz)})
    if at != len(records):
        raise ValueError("%s: %d bytes left over after %d records and %d diffs" % (path, len(records) - at, count, diffs))
    return {"cell": (cx, cz), "cell_m": cell, "diffs": diffs, "crc_stated": crc, "crc_actual": actual, "entities": entities, "taken": taken}


def read_codes_layer(world_dir, name):
    """A u8 layer of the world folder by its sidecar: its width, height, cell and extent, and its raw bytes; None when absent."""
    sidecar = os.path.join(world_dir, "layers", name + ".json")
    if not os.path.isfile(sidecar):
        return None
    doc = json.load(open(sidecar, encoding="utf-8"))
    if doc.get("dtype") != "u8":
        raise ValueError("%s: dtype %s, not u8" % (sidecar, doc.get("dtype")))
    raw = open(os.path.join(os.path.dirname(sidecar), doc["raw"]), "rb").read()
    if len(raw) != int(doc["width"]) * int(doc["height"]):
        raise ValueError("%s: %d bytes for %s by %s cells" % (sidecar, len(raw), doc["width"], doc["height"]))
    return {"width": int(doc["width"]), "height": int(doc["height"]), "cell_m": float(doc["cell_m"]), "extent_m": float(doc["extent_m"]), "raw": raw}


def read_player_binary(path):
    data = open(path, "rb").read()
    body, (stated,) = data[:-4], struct.unpack_from("<I", data, len(data) - 4)
    actual = zlib.crc32(body) & 0xFFFFFFFF
    if data[:4] != b"EG2P":
        raise ValueError("%s: not a player file" % path)
    (version,) = struct.unpack_from("<H", data, 4)
    if version not in (2, 3):
        raise ValueError("%s: version %d" % (path, version))
    name, at = read_string(data, 6)
    east, up, north, yaw, pitch, flags, tick = struct.unpack_from("<dddffBq", data, at)
    at += 8 * 3 + 4 * 2 + 1 + 8
    hand, carried = 0, []
    if version >= 3:
        hand, count = struct.unpack_from("<BB", data, at)
        at += 2
        for _ in range(count):
            place, tid = struct.unpack_from("<BQ", data, at)
            key, at = read_string(data, at + 9)
            (spawn_tick,) = struct.unpack_from("<q", data, at)
            at += 8
            carried.append({"place": place, "id": tid, "key": key, "spawn_tick": spawn_tick})
    if at != len(body):
        raise ValueError("%s: %d bytes left over after the version %d layout" % (path, len(body) - at, version))
    return {"name": name, "east": east, "up": up, "north": north, "grounded": bool(flags & 1), "wading": bool(flags & 2), "crouching": bool(flags & 4),
            "version": version, "hand": hand, "carried": carried, "crc_stated": stated, "crc_actual": actual}


def read_player_v1(path):
    doc = json.load(open(path, encoding="utf-8"))
    return {"name": doc["name"], "east": doc["east"], "up": doc["up"], "north": doc["north"], "grounded": bool(doc.get("grounded", False)),
            "wading": False, "crouching": False, "version": 1, "hand": 0, "carried": [], "crc_stated": 0, "crc_actual": 0}


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

    # 0: one whole save, not a save a crash stopped part of the way through (M1.3b).
    left = sorted(os.path.relpath(p, full) for folder in (full, os.path.join(full, "players"), os.path.join(full, "regions"))
                  for p in glob.glob(os.path.join(folder, "*.part")))
    if os.path.isfile(os.path.join(full, "save.commit")):
        left.append("save.commit")
    expect("the folder holds one whole save", not left,
           "left over: " + ", ".join(left) if left else "no file written aside and no save's record left over")

    # 1 and 2: the region files.
    regions = sorted(glob.glob(os.path.join(full, "regions", "r.*.egr")))
    entities = []
    taken_all = []
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
        taken_all.extend(region["taken"])
    expect("every region file's CRC matches", not crc_bad, "%d files, %d entities; %s" % (len(regions), len(entities), "; ".join(crc_bad) or "all match"))
    expect("every entity lies in the cell its file names", not misplaced and not named_wrong,
           "; ".join(misplaced + named_wrong) or "%d entities in %d cells of %.0f m" % (len(entities), len(regions), CELL_M))

    # 3: the players.
    players = {}
    dup = []
    for path in sorted(glob.glob(os.path.join(full, "players", "*.egp"))):
        p = read_player_binary(path)
        players[p["name"]] = p
    for path in sorted(glob.glob(os.path.join(full, "players", "*.json"))):
        p = read_player_v1(path)
        if p["name"] in players:
            dup.append(p["name"])
        else:
            players[p["name"]] = p
    crc_players = [p["name"] for p in players.values() if p["crc_stated"] != p["crc_actual"]]
    versions = {v: sum(1 for p in players.values() if p["version"] == v) for v in (1, 2, 3)}
    expect("every player file's CRC matches, one file a name", not crc_players and not dup,
           "%d players (version 1: %d, 2: %d, 3: %d); bad CRC: %s; twice: %s" % (len(players), versions[1], versions[2], versions[3],
                                                                               ", ".join(crc_players) or "none", ", ".join(dup) or "none"))

    # 4: what is carried is out of the world.
    next_id = int(doc.get("next_entity_id", 1))
    lying = set(e["id"] for e in entities)
    carried_by, wrong = {}, []
    for name in sorted(players):
        for t in players[name]["carried"]:
            if t["id"] in lying:
                wrong.append("%s carries %d, which lies in a region file" % (name, t["id"]))
            if t["id"] == 0 or t["id"] >= next_id:
                wrong.append("%s carries %d, not below next_entity_id %d" % (name, t["id"], next_id))
            if t["id"] in carried_by:
                wrong.append("%s and %s both carry %d" % (carried_by[t["id"]], name, t["id"]))
            carried_by[t["id"]] = name
    expect("every carried thing is out of the world", not wrong,
           "; ".join(wrong) or "%d carried by %d founders, %d lying, next id %d" % (len(carried_by), sum(1 for p in players.values() if p["carried"]), len(lying), next_id))

    # 5: what was taken from the loose layer lay there to take, and is kept in the file of its cell.
    loose = read_codes_layer(full, "loose")
    wrong_taken, things_taken = [], 0
    for t in taken_all:
        things_taken += bin(t["sticks"]).count("1") + bin(t["cobbles"]).count("1")
        if loose is None:
            wrong_taken.append("cell (%d, %d) taken from a world with no loose layer" % (t["row"], t["col"]))
            continue
        if not (0 <= t["row"] < loose["height"] and 0 <= t["col"] < loose["width"]):
            wrong_taken.append("cell (%d, %d) lies outside the loose layer" % (t["row"], t["col"]))
            continue
        code = loose["raw"][t["row"] * loose["width"] + t["col"]]
        sticks, cobbles = code & 0x0F, code >> 4
        if (t["sticks"] >> sticks) or (t["cobbles"] >> cobbles):
            wrong_taken.append("cell (%d, %d) took sticks %d and cobbles %d of a cell holding %d and %d" % (t["row"], t["col"], t["sticks"], t["cobbles"], sticks, cobbles))
        east = t["col"] * loose["cell_m"] - loose["extent_m"] / 2.0
        north = loose["extent_m"] / 2.0 - t["row"] * loose["cell_m"]
        if extent > 0 and (cell_index(east, extent), cell_index(north, extent)) != t["file_cell"]:
            wrong_taken.append("cell (%d, %d) at (%.0f, %.0f) is kept in the file of cell %s" % (t["row"], t["col"], east, north, t["file_cell"]))
    expect("every taken thing lay there to take", not wrong_taken,
           "; ".join(wrong_taken) or "%d things taken from %d cells, each inside its cell's count and in its own region file" % (things_taken, len(taken_all)))

    # 6: the digest, rebuilt from the stated lines.
    lines = ["clock %s" % fixed(float(doc["clock"]["total_hours"]), HOUR), "tick %d" % int(doc["tick"])]
    for name in sorted(players):
        p = players[name]
        lines.append("%s %s %s %s %s %s %s" % (name, fixed(p["east"], METRE), fixed(p["up"], METRE), fixed(p["north"], METRE),
                                                 "g" if p["grounded"] else "a", "w" if p["wading"] else "d", "c" if p["crouching"] else "s"))
    for name in sorted(players):
        p = players[name]
        if not p["carried"] and p["hand"] == 0:
            continue
        lines.append("hands %s %d" % (name, p["hand"]))
        for t in sorted(p["carried"], key=lambda t: t["place"]):
            lines.append("carried %s %d %d %s" % (name, t["place"], t["id"], t["key"]))
    for e in sorted(entities, key=lambda e: e["id"]):
        line = "entity %d %s %s %s %s %s" % (e["id"], e["key"], fixed(e["east"], METRE), fixed(e["up"], METRE), fixed(e["north"], METRE), fixed(e["yaw"], DEGREE))
        if e["item"] is not None:
            line += " item %s %s" % ("r" if e["item"][0] else "f", fixed(e["item"][1], METRE))
        lines.append(line)
    for t in sorted(taken_all, key=lambda t: (t["row"], t["col"])):
        lines.append("taken %d %d %d %d" % (t["row"], t["col"], t["sticks"], t["cobbles"]))
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
