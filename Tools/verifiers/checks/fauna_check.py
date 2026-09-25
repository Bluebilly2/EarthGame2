#!/usr/bin/env python3
"""fauna_check.py: do the animals the server stood up round the founders stand where the world can hold them, and are
there as many as its capacity layers say?

The M1.7a contract (`Docs/contracts/M1.7a_ANIMALS_IN_THE_WORLD.md`) promises that the server stands up the kangaroo mobs
and oystercatcher pairs presence puts within 500 m of a founder, never on the water, each kind on the ground that feeds
it. This check reads what the server host wrote about them, the `fauna` records of its run.jsonl (every ten seconds: the
founders, and every animal's id, key, place and pose; ARCHITECTURE section 10), and holds each against the world
folder's own layers, read with numpy by the layouts their sidecars state. It imports nothing the game runs.

Rows, each with both numbers:
  1. animals stood up: fauna records were written, and animals stood round the founders in them;
  2. none on open water: no animal on a cell the water layer calls a creek, a stream, a lake or the sea;
  3. none under the water's surface: no animal between posts where the surface layer stands above the heights layer;
  4. every oystercatcher on the shore strip: its cell's distance to the sea within the tideline's reach, widened by what
     can carry a bird from the ground that feeds it (a square's diagonal, the wander, the pair's spread, half a cell);
  5. every kangaroo within reach of fresh water: its cell's distance to fresh water within the kind's range, widened the
     same way;
  6. and 7. for each kind, the groups that stood against what the capacity layers put there. Over the squares that lay,
     at some record, wholly within 450 m of a founder (the 500 m of the stand-up, less the 40 m wander, less what a
     founder and a group move in the second since the last stand-up) and wholly inside the region: the groups seen,
     against the sum over those squares of each square's chance of holding one (its mean capacity, in groups, over its
     area, as presence draws a square) times the share of the square that is dry (a group whose own place is wet is not
     stood up). The row passes within three standard deviations of that Bernoulli sum, and one group;
  8. every flight began within its kind's distance (M1.7c): each `flight` record's distance from the founder to the
     nearest member, held to the kind's distance (80 m a mob, 60 m a pair, the contract's) plus a metre for the step
     between the founder's move and the noticing; both numbers printed. With no flights the row holds nothing and says so;
  9. every animal logged fleeing stood on dry ground: the places of pose 3 among the fauna records, held as rows 2 and 3
     hold every place;
  10. a flight was recorded, and the fauna records bear it out (2026-09-16; DEBTS "The corpus's loop startles no animal"):
     at least one `flight` record, and for each one a fauna record within the kind's run (its length over its speed, and
     the ten seconds between records) after it shows the group's members either logged fleeing (pose 3) or moved farther
     since the record before than presence's wander can carry a group in the time between records. The flight record is
     the stand-up's own claim; the members' poses and places are what the host saw of the world's entities, so a flight
     that left no trace in them fails the row. A soak whose loop passes no group within its flight distance fails here,
     which is what the row is for: the loop is laid to pass one (lay_loop.py --pass).

What is restated here, and from where. The kinds' group sizes, from the species table (`Docs/ECOSYSTEM.md`, "The
animals of Bherwerre": mobs of eight, pairs); the kangaroo's 4,000 m from fresh water (`AnimalSpecies.WaterRangeM`) and
the tideline's 300 m (`AnimalCapacity.TidelineReachM`), past which their capacity is nothing; presence's 100 m squares
and 40 m wander and the stand-up's 500 m, from the contract and ARCHITECTURE section 5; a member's spread, five metres
times the root of its group's size, as the stand-up states it (a looseness, not a citation); an animal's id's layout,
from ARCHITECTURE section 10; the runs (M1.7c's contract: a mob 150 m at 7 m/s, a pair 200 m at 15 m/s) and the wander's
period of three world hours at the world's seconds a real second the server's run header states (ARCHITECTURE section
4; forty-eight on the corpus's servers), from which row 10 works out how far a group can move between two fauna records
without having fled.

Independent of the game: the squares' means are summed over the whole raster at once with numpy's bincount, every cell
into the one square its centre lies in, where the game adds a layer cell by cell; the dry share of a square is counted
from the water and surface layers, which the game never counts; the expectation is a Bernoulli sum the game never
forms; and every place comes from the host's log, never from presence.

Exit 0 when every row passes, 1 when any fails, 2 when the log or a layer is missing.
Run from the repository root:
    python Tools/verifiers/checks/fauna_check.py [corpus folder, or a server folder holding run.jsonl and world]
(default Artefacts/corpus/latest, whose soak/server/run.jsonl and soak/server/world are read.)
"""
import json
import math
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from corpus_check import world_seconds_per_real_second  # noqa: E402  (the pace a server's run header states, read in one place)

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))))
DEFAULT_CORPUS = os.path.join("Artefacts", "corpus", "latest")
NP_DTYPES = {"u8": "u1", "u16": "<u2", "i16": "<i2", "u32": "<u4", "f32": "<f4"}

# The kinds stood up, by the kind field of an animal's id: the species table's order, counting from one.
KANGAROO, OYSTERCATCHER = 1, 2
KINDS = (KANGAROO, OYSTERCATCHER)
NAMES = {KANGAROO: "kangaroo", OYSTERCATCHER: "oystercatcher"}
GROUP_SIZE = {KANGAROO: 8, OYSTERCATCHER: 2}
CAPACITY_LAYER = {KANGAROO: "capacity_easterngreykangaroo", OYSTERCATCHER: "capacity_piedoystercatcher"}
FRESH_WATER_RANGE_M = 4000.0
TIDELINE_REACH_M = 300.0
# The flights (M1.7c): how near a founder sends a group running, restated from the contract, and a step's grace.
FLEE_WITHIN_M = {KANGAROO: 80.0, OYSTERCATCHER: 60.0}
FLIGHT_STEP_M = 1.0
FLEEING_POSE = 3
# How far and how fast each kind runs (M1.7c's contract), and how long a fauna record follows the last.
RUN_M = {KANGAROO: 150.0, OYSTERCATCHER: 200.0}
RUN_MS = {KANGAROO: 7.0, OYSTERCATCHER: 15.0}
FAUNA_EVERY_S = 10.0
# Presence's wander: a circle of 40 m round the group's own place, once round in three world hours (AnimalPresence's
# DriftPeriodHours, restated), at the world's seconds a real second the server's run header states (forty-eight for the
# corpus's servers and for a run from before the header said so): the farthest it can carry a group between two records
# is the chord of that arc, plus the members' own spread twice over (a member's place is drawn afresh round the group each
# second).
WANDER_PERIOD_WORLD_S = 3.0 * 3600.0
# Presence and the stand-up.
SQUARE_M = 100.0
SQUARE_KM2 = 0.01
WANDER_M = 40.0
MEMBER_SPACING_M = 5.0
WHOLLY_WITHIN_M = 450.0
SIGMAS = 3.0
# The water layer's codes for open water, as its legend states them: creek, stream, lake, sea.
OPEN_WATER = (3, 4, 5, 7)


class Rows:
    def __init__(self):
        self.failed = 0

    def row(self, name, ok, detail):
        if not ok:
            self.failed += 1
        print("%-48s %s  %s" % (name, "PASS" if ok else "FAIL", detail))


def layer(world, name):
    path = os.path.join(world, "layers", name + ".json")
    if not os.path.isfile(path):
        return None, None
    sidecar = json.load(open(path, encoding="utf-8"))
    raw = os.path.join(os.path.dirname(path), sidecar["raw"])
    return sidecar, np.fromfile(raw, dtype=NP_DTYPES[sidecar["dtype"]]).reshape(sidecar["height"], sidecar["width"])


def in_unit(sidecar, grid):
    """A layer's values in its unit: the raw numbers times the sidecar's scale."""
    return grid.astype(np.float64) * float(sidecar.get("scale", 1.0))


def records_of(path, kind):
    records = []
    with open(path, encoding="utf-8") as f:
        for line in f:
            line = line.strip()
            if line:
                record = json.loads(line)
                if record.get("kind") == kind:
                    records.append(record)
    return records


def fauna_records(path):
    return records_of(path, "fauna")


def decode(animal_id):
    """(kind, square east, square north, member) from an id of the reserved range, or None for an id outside it."""
    if not (animal_id >> 63) & 1:
        return None
    return ((animal_id >> 56) & 0x7F, ((animal_id >> 32) & 0xFFFFFF) - (1 << 23), ((animal_id >> 8) & 0xFFFFFF) - (1 << 23), animal_id & 0xFF)


def nearest_cell(sidecar, east, north):
    """The cell whose centre is nearest a point: column 0 at the west edge, row 0 at the north, clamped to the grid."""
    half, cell = sidecar["extent_m"] / 2.0, sidecar["cell_m"]
    col = int(min(max(round((east + half) / cell), 0), sidecar["width"] - 1))
    row = int(min(max(round((half - north) / cell), 0), sidecar["height"] - 1))
    return row, col


def posts_between(sidecar, east, north):
    """The posts a point lies between that carry a share of a bilinear reading there; a post on a line of posts carries all of it."""
    half, cell = sidecar["extent_m"] / 2.0, sidecar["cell_m"]
    max_col, max_row = sidecar["width"] - 1, sidecar["height"] - 1
    col = min(max((east + half) / cell, 0.0), float(max_col))
    row = min(max((half - north) / cell, 0.0), float(max_row))
    c0 = min(int(math.floor(col)), max_col - 1)
    r0 = min(int(math.floor(row)), max_row - 1)
    tc, tr = col - c0, row - r0
    return [(r, c) for r, share_r in ((r0, 1.0 - tr), (r0 + 1, tr)) for c, share_c in ((c0, 1.0 - tc), (c0 + 1, tc))
            if share_r > 0.0 and share_c > 0.0]


def group_centre(record, kind, square):
    """Where a group's members stood in a fauna record, on average, or None where none of them was held."""
    ids, positions = record.get("ids", []), record.get("positions", [])
    east = north = 0.0
    n = 0
    for i, animal_id in enumerate(ids):
        d = decode(int(animal_id))
        if d is None or d[0] != kind or (d[1], d[2]) != square or len(positions) < 3 * (i + 1):
            continue
        east += float(positions[3 * i])
        north += float(positions[3 * i + 2])
        n += 1
    return (east / n, north / n) if n else None


def squares_of(sidecar):
    """Each column's square east and each row's square north, by the cell's centre."""
    half, cell = sidecar["extent_m"] / 2.0, sidecar["cell_m"]
    east = np.arange(sidecar["width"], dtype=np.float64) * cell - half
    north = half - np.arange(sidecar["height"], dtype=np.float64) * cell
    return np.floor(east / SQUARE_M).astype(np.int64), np.floor(north / SQUARE_M).astype(np.int64)


def main(argv):
    target = argv[1] if len(argv) > 1 else DEFAULT_CORPUS
    base = os.path.normpath(target if os.path.isabs(target) else os.path.join(ROOT, target))
    server = os.path.join(base, "soak", "server")
    if not os.path.isfile(os.path.join(server, "run.jsonl")):
        server = base
    log_path, world = os.path.join(server, "run.jsonl"), os.path.join(server, "world")
    if not os.path.isfile(log_path):
        print("no server log at %s" % log_path)
        return 2
    wanted = ["water", "surface", "heights", "shore_distance", "fresh_water_distance"] + [CAPACITY_LAYER[k] for k in KINDS]
    layers = {}
    for name in wanted:
        sidecar, grid = layer(world, name)
        if grid is None:
            print("the world at %s has no %s layer; %s are needed" % (world, name, ", ".join(wanted)))
            return 2
        layers[name] = (sidecar, grid)
    grid_side, heights = layers["heights"]
    for name in wanted:
        s = layers[name][0]
        if (s["width"], s["height"], s["cell_m"], s["extent_m"]) != (grid_side["width"], grid_side["height"], grid_side["cell_m"], grid_side["extent_m"]):
            print("the %s layer is not on the heights layer's grid" % name)
            return 2
    water = layers["water"][1]
    surface = layers["surface"][1]
    half = grid_side["extent_m"] / 2.0
    half_cell_diagonal = grid_side["cell_m"] * math.sqrt(2.0) / 2.0

    world_per_real = world_seconds_per_real_second(log_path)
    wander_period_s = WANDER_PERIOD_WORLD_S / world_per_real
    print("fauna_check: %s against %s (the world's clock at %g world seconds a real second, the wander once round in %.0f real s)"
          % (log_path, world, world_per_real, wander_period_s))
    records = fauna_records(log_path)
    rows = Rows()

    # Every place logged, with what it is, and its pose.
    animals = []
    fleeing = []
    malformed = 0
    for record in records:
        ids, positions, poses = record.get("ids", []), record.get("positions", []), record.get("poses", [])
        if len(positions) != 3 * len(ids) or len(record.get("founders", [])) % 2:
            malformed += 1
            continue
        for i, animal_id in enumerate(ids):
            place = (decode(int(animal_id)), float(positions[3 * i]), float(positions[3 * i + 2]))
            animals.append(place)
            if i < len(poses) and int(poses[i]) == FLEEING_POSE:
                fleeing.append(place)
    most = max((len(r.get("ids", [])) for r in records), default=0)
    outside = sum(1 for a in animals if a[0] is None or a[0][0] not in KINDS)
    rows.row("animals stood up round the founders", len(records) > 0 and most > 0 and malformed == 0 and outside == 0,
             "%d fauna records, at most %d animals held at once (must be above 0); %d malformed record(s), %d id(s) not of a kind stood up (must be 0)"
             % (len(records), most, malformed, outside))
    animals = [a for a in animals if a[0] is not None and a[0][0] in KINDS]

    on_open = sum(1 for _, east, north in animals if int(water[nearest_cell(grid_side, east, north)]) in OPEN_WATER)
    rows.row("none on open water", on_open == 0,
             "%d of %d places logged on a creek, a stream, a lake or the sea (must be 0)" % (on_open, len(animals)))

    under = sum(1 for _, east, north in animals
                if any(float(surface[p]) > float(heights[p]) for p in posts_between(grid_side, east, north)))
    rows.row("none under the water's surface", under == 0,
             "%d of %d places logged between posts under the water (must be 0)" % (under, len(animals)))

    for kind, name, reach, ground, what in ((OYSTERCATCHER, "every oystercatcher on the shore strip", TIDELINE_REACH_M, "shore_distance", "the sea"),
                                            (KANGAROO, "every kangaroo within reach of fresh water", FRESH_WATER_RANGE_M, "fresh_water_distance", "fresh water")):
        side, distances = layers[ground]
        bound = reach + SQUARE_M * math.sqrt(2.0) + WANDER_M + MEMBER_SPACING_M * math.sqrt(GROUP_SIZE[kind]) + half_cell_diagonal
        found = [float(distances[nearest_cell(side, east, north)]) * float(side.get("scale", 1.0)) for d, east, north in animals if d[0] == kind]
        beyond = sum(1 for m in found if m > bound)
        rows.row(name, beyond == 0,
                 "%d of %d places logged farther than %.0f m from %s (%.0f m of reach, widened); the farthest %s"
                 % (beyond, len(found), bound, what, reach, "%.0f m" % max(found) if found else "none"))

    # The squares wholly within reach of a founder at some record, and whether a group of each kind stood in them.
    seen = {k: {} for k in KINDS}
    for record in records:
        flat = record.get("founders", [])
        founders = [(float(flat[i]), float(flat[i + 1])) for i in range(0, len(flat) - 1, 2)]
        held = {k: set() for k in KINDS}
        for animal_id in record.get("ids", []):
            d = decode(int(animal_id))
            if d is not None and d[0] in held:
                held[d[0]].add((d[1], d[2]))
        covered = set()
        for fe, fn in founders:
            for x in range(int(math.floor((fe - WHOLLY_WITHIN_M) / SQUARE_M)), int(math.floor((fe + WHOLLY_WITHIN_M) / SQUARE_M)) + 1):
                for z in range(int(math.floor((fn - WHOLLY_WITHIN_M) / SQUARE_M)), int(math.floor((fn + WHOLLY_WITHIN_M) / SQUARE_M)) + 1):
                    if x * SQUARE_M < -half or (x + 1) * SQUARE_M > half or z * SQUARE_M < -half or (z + 1) * SQUARE_M > half:
                        continue
                    corners = ((x * SQUARE_M, z * SQUARE_M), ((x + 1) * SQUARE_M, z * SQUARE_M), (x * SQUARE_M, (z + 1) * SQUARE_M), ((x + 1) * SQUARE_M, (z + 1) * SQUARE_M))
                    if all((ce - fe) ** 2 + (cn - fn) ** 2 <= WHOLLY_WITHIN_M ** 2 for ce, cn in corners):
                        covered.add((x, z))
        for kind in KINDS:
            for square in covered:
                seen[kind][square] = seen[kind].get(square, False) or square in held[kind]

    square_east, square_north = squares_of(grid_side)
    x0, z0 = int(square_east.min()), int(square_north.min())
    across, down = int(square_east.max()) - x0 + 1, int(square_north.max()) - z0 + 1
    key = ((square_north - z0)[:, None] * across + (square_east - x0)[None, :]).ravel()
    cells = np.bincount(key, minlength=across * down).astype(np.float64)
    wet_cells = (np.isin(water, OPEN_WATER) | (surface > heights)).ravel().astype(np.float64)
    dry_share = 1.0 - np.bincount(key, weights=wet_cells, minlength=across * down) / np.maximum(cells, 1.0)
    for kind in KINDS:
        side, raw = layers[CAPACITY_LAYER[kind]]
        means = np.bincount(key, weights=in_unit(side, raw).ravel(), minlength=across * down) / np.maximum(cells, 1.0)
        mu = variance = 0.0
        stood = 0
        for (x, z), held_one in seen[kind].items():
            i = (z - z0) * across + (x - x0)
            chance = min(1.0, max(0.0, means[i] / GROUP_SIZE[kind] * SQUARE_KM2)) * dry_share[i]
            mu += chance
            variance += chance * (1.0 - chance)
            stood += 1 if held_one else 0
        sigma = math.sqrt(variance)
        rows.row("%s groups against the capacity layers" % NAMES[kind], len(seen[kind]) > 0 and abs(stood - mu) <= SIGMAS * sigma + 1.0,
                 "%d groups stood in the %d squares wholly within reach; the layers put %.1f there, give or take %.1f (|%d - %.1f| <= 3 x %.1f + 1)"
                 % (stood, len(seen[kind]), mu, sigma, stood, mu, sigma))

    # The flights (M1.7c): each began within its kind's distance of a founder, and every animal logged fleeing stood dry.
    # The kind is the record's `species`; a record from before 2026-09-16 wrote it under the record's own `kind` key and lost
    # it, and such a flight is held against the widest kind's distance and counted apart.
    flights = records_of(log_path, "flight")
    beyond, farthest, unknown, unnamed = 0, {}, 0, 0
    widest = max(FLEE_WITHIN_M.values())
    for flight in flights:
        distance = float(flight.get("distance_m", float("inf")))
        if "species" not in flight:
            unnamed += 1
            farthest[None] = max(farthest.get(None, 0.0), distance)
            if distance > widest + FLIGHT_STEP_M:
                beyond += 1
            continue
        kind = int(flight["species"])
        if kind not in FLEE_WITHIN_M:
            unknown += 1
            continue
        farthest[kind] = max(farthest.get(kind, 0.0), distance)
        if distance > FLEE_WITHIN_M[kind] + FLIGHT_STEP_M:
            beyond += 1
    told = ", ".join("%s farthest %.1f m of %.0f + %.0f" % (NAMES[k], farthest[k], FLEE_WITHIN_M[k], FLIGHT_STEP_M) for k in KINDS if k in farthest)
    if None in farthest:
        told = (told + "; " if told else "") + "%d of no species (records from before 2026-09-16) farthest %.1f m of the widest %.0f + %.0f" % (unnamed, farthest[None], widest, FLIGHT_STEP_M)
    rows.row("every flight began within its kind's distance", beyond == 0 and unknown == 0,
             "%d flight(s) recorded; %d began farther off than the kind's distance (must be 0), %d of a kind not stood up (must be 0); %s"
             % (len(flights), beyond, unknown, told if told else "no flights: nothing held"))
    fleeing = [a for a in fleeing if a[0] is not None and a[0][0] in KINDS]
    wet = sum(1 for _, east, north in fleeing
              if int(water[nearest_cell(grid_side, east, north)]) in OPEN_WATER
              or any(float(surface[p]) > float(heights[p]) for p in posts_between(grid_side, east, north)))
    rows.row("every animal logged fleeing stood on dry ground", wet == 0,
             "%d fleeing place(s) logged, %d on open water or under the surface (must be 0)" % (len(fleeing), wet))

    # A flight was recorded, and the fauna bear it out (2026-09-16): the group's members seen fleeing, or moved farther than
    # the wander can carry them, in a record within the run after the flight.
    borne, unborne, told_of = 0, 0, []
    for flight in flights:
        # The kind is the record's `species` (a record from before 2026-09-16 wrote it under the record's own `kind` key and
        # lost it: such a flight is held against every kind that stands at its square).
        kinds_to_try = [int(flight["species"])] if "species" in flight else list(RUN_M)
        square = (int(flight.get("cell_x", 0)), int(flight.get("cell_z", 0)))
        t0 = float(flight.get("t", 0.0))
        seen = False
        for kind in kinds_to_try:
            if kind not in RUN_M:
                continue
            until = t0 + RUN_M[kind] / RUN_MS[kind] + FAUNA_EVERY_S
            previous = None
            for record in records:
                centre = group_centre(record, kind, square)
                fled = any(int(record["poses"][i]) == FLEEING_POSE for i, animal_id in enumerate(record.get("ids", []))
                           if i < len(record.get("poses", [])) and decode(int(animal_id)) is not None and decode(int(animal_id))[:3] == (kind,) + square)
                t = float(record.get("t", 0.0))
                if centre is not None and previous is not None and t0 <= t <= until:
                    gap = t - previous[0]
                    wander = 2.0 * WANDER_M * math.sin(min(math.pi, math.pi * gap / wander_period_s)) + 2.0 * MEMBER_SPACING_M * math.sqrt(GROUP_SIZE[kind])
                    moved = math.hypot(centre[0] - previous[1][0], centre[1] - previous[1][1])
                    if fled or moved > wander:
                        seen = True
                        told_of.append("%s (%d, %d) at %.0f s: %s" % (NAMES[kind], square[0], square[1], t0, "logged fleeing" if fled else "moved %.0f m in %.0f s, past the wander's %.0f" % (moved, gap, wander)))
                        break
                if centre is not None:
                    previous = (t, centre)
            if seen:
                break
        if seen:
            borne += 1
        else:
            unborne += 1
            told_of.append("square (%d, %d) at %.0f s: no trace in the fauna records" % (square[0], square[1], t0))
    rows.row("a flight was recorded and the fauna bear it out", borne >= 1 and unborne == 0,
             "%d flight(s) recorded (at least 1), %d borne out by the fauna records, %d not (must be 0)%s"
             % (len(flights), borne, unborne, ("; " + "; ".join(told_of[:6])) if told_of else "; no flight: the loop passed no group within its flight distance"))

    print("fauna_check: %s" % ("PASS" if rows.failed == 0 else "FAIL (%d row(s))" % rows.failed))
    return 0 if rows.failed == 0 else 1


if __name__ == "__main__":
    sys.exit(main(sys.argv))
