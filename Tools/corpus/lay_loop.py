#!/usr/bin/env python3
"""lay_loop.py: lay the corpus's walking loop round a world's wake, and write it into Routes.WakeLoop.

The walking scenarios (ARCHITECTURE section 7.1, N2) walk a loop that must start near where the founder wakes and
must hold the two named divergence segments: a shore walked along the water's edge, and a bank of the slope N2 was
specified on (22 to 28 degrees, 2026-09-08). The scorer's wake moves whenever the layer rules change (DEBTS.md, "The
corpus loop is tied to one world's wake"); the first re-laying was this search done by hand (2026-09-10), and this is
that search as a tool. Claude's tool: it lays a loop and surveys it against its own criteria; whether founders walk
it without being corrected is N2's question, measured by the corpus and judged by join_check.

The search, over the world's own layers:
  - candidate points on a 16 m grid within 560 m of the wake, dry, and no steeper than 12 degrees;
  - legs between points up to 96 m apart, sampled every 2 m: never across a stream, a lake, a swamp or the sea;
    across a creek only at a 250 m penalty, so only where there is no dry way, and then labelled a creek leg;
  - a shore leg: 100 to 230 m between points no more than 12 m from the sea, every sample no more than 16 m from
    it, on ground no higher than 2.5 m and no steeper than 10 degrees;
  - a bank leg: 30 to 170 m between points with six or more 22-28 degree cells within 32 m, a fifth or more of its
    samples at 22 to 28 degrees and none over 30;
  - the loop: the shore, the shortest flat way to the bank, the bank, the shortest flat way back; 700 to 2,300 m,
    scored for a long shore and a long bank at the specified slope, a lap near 1.4 km and a short walk from the wake;
  - the lap starts at the nearest of its waypoints that a straight, dry, flat walk from the wake reaches, because the
    founder walks straight from where they wake to the first waypoint (the first run of this tool found a loop whose
    own legs were dry and whose walk from the wake crossed a swamp); the best-scored loop with such a start is laid.

Usage, from the repository root:
    python Tools/corpus/lay_loop.py [world folder] [--write]
(default Artefacts/worlds/gate). Prints the survey of the loop it chose and its waypoints; with --write it replaces
the waypoints in Engine/packages/com.earthgame.clientcore/Runtime/Routes.cs. A player carries the loop, so it is
rebuilt before the corpus walks it. Exit 0 when a loop was laid, 1 when none meets the criteria, 2 when the world
cannot be read.
"""
import argparse
import heapq
import json
import math
import os
import re
import sys
import time

import numpy as np

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
DEFAULT_WORLD = os.path.join("Artefacts", "worlds", "gate")
ROUTES = os.path.join(ROOT, "Engine", "packages", "com.earthgame.clientcore", "Runtime", "Routes.cs")
NP_DTYPES = {"u8": "u1", "u16": "<u2", "i16": "<i2", "u32": "<u4", "f32": "<f4"}
# The water layer's classes, as its legend states them.
CREEK, STREAM, LAKE, SWAMP, SEA = 3, 4, 5, 6, 7

RADIUS_M = 560.0
GRID_M = 16.0
LEG_M = 96.0
SAMPLE_M = 2.0
CREEK_PENALTY_M = 250.0
FLAT_MAX_DEG = 12.0
SHORE_POINT_M, SHORE_SAMPLE_M, SHORE_GROUND_M, SHORE_SLOPE_DEG = 12.0, 16.0, 2.5, 10.0
SHORE_LENGTH_M = (100.0, 230.0)
BANK_DEG = (22.0, 28.0)
BANK_MAX_DEG = 30.0
BANK_SHARE = 0.20
BANK_NEAR_CELLS, BANK_NEAR_COUNT = 8, 6
BANK_LENGTH_M = (30.0, 170.0)
LAP_M = (700.0, 2300.0)
LAP_AIM_M = 1400.0
KEEP = 40


def layer(world, name):
    path = os.path.join(ROOT, world, "layers", name + ".json")
    sidecar = json.load(open(path, encoding="utf-8"))
    raw = os.path.join(os.path.dirname(path), sidecar["raw"])
    return sidecar, np.fromfile(raw, dtype=NP_DTYPES[sidecar["dtype"]]).reshape(sidecar["height"], sidecar["width"])


class Ground:
    def __init__(self, world):
        side, self.z = layer(world, "heights")
        _, self.water = layer(world, "water")
        _, self.shore = layer(world, "shore_distance")
        self.cell, self.half = side["cell_m"], side["extent_m"] / 2.0
        gy, gx = np.gradient(self.z.astype(np.float64), self.cell)
        self.slope = np.degrees(np.arctan(np.hypot(gx, gy)))
        self.barred = np.isin(self.water, [STREAM, LAKE, SWAMP, SEA])
        self.creek = self.water == CREEK
        self.cache = {}

    def rc(self, east, north):
        return int(round((self.half - north) / self.cell)), int(round((east + self.half) / self.cell))

    def samples(self, a, b):
        length = math.hypot(b[0] - a[0], b[1] - a[1])
        k = max(2, int(length / SAMPLE_M))
        return length, [self.rc(a[0] + (b[0] - a[0]) * i / k, a[1] + (b[1] - a[1]) * i / k) for i in range(k + 1)]

    def leg(self, a, b):
        """(length, steepest, share at the bank's slope, farthest from the sea, highest ground, crosses a creek), or
        None where the leg crosses water it may not."""
        key = (a, b) if a <= b else (b, a)
        if key in self.cache:
            return self.cache[key]
        length, cells = self.samples(a, b)
        rows = np.array([r for r, _ in cells])
        cols = np.array([c for _, c in cells])
        out = None
        if not self.barred[rows, cols].any():
            s = self.slope[rows, cols]
            out = (length, float(s.max()), float(((s >= BANK_DEG[0]) & (s <= BANK_DEG[1])).mean()),
                   float(self.shore[rows, cols].max()), float(self.z[rows, cols].max()), bool(self.creek[rows, cols].any()))
        self.cache[key] = out
        return out

    def survey(self, a, b):
        length, cells = self.samples(a, b)
        rows = np.array([r for r, _ in cells])
        cols = np.array([c for _, c in cells])
        s = self.slope[rows, cols]
        return {
            "length": length, "steepest": float(s.max()),
            "at_bank_slope": float(((s >= BANK_DEG[0]) & (s <= BANK_DEG[1])).mean()),
            "over_bank_slope": float((s > BANK_DEG[1]).mean()),
            "ground": (float(self.z[rows, cols].min()), float(self.z[rows, cols].max())),
            "sea": (float(self.shore[rows, cols].min()), float(self.shore[rows, cols].max())),
            "water": sorted(set(int(v) for v in self.water[rows, cols])),
        }


def dijkstra(nbr, src):
    dist, prev, q = {src: 0.0}, {}, [(0.0, src)]
    while q:
        dv, v = heapq.heappop(q)
        if dv > dist.get(v, 1e18):
            continue
        for u, w in nbr[v]:
            nd = dv + w
            if nd < dist.get(u, 1e18):
                dist[u] = nd
                prev[u] = v
                heapq.heappush(q, (nd, u))
    return dist, prev


def path(prev, src, dst):
    out = [dst]
    while out[-1] != src:
        out.append(prev[out[-1]])
    return out[::-1]


def lay(g, wake):
    t0 = time.time()
    pts = [wake]
    for e in np.arange(wake[0] - RADIUS_M, wake[0] + RADIUS_M + 1, GRID_M):
        for n in np.arange(wake[1] - RADIUS_M, wake[1] + RADIUS_M + 1, GRID_M):
            if math.hypot(e - wake[0], n - wake[1]) > RADIUS_M:
                continue
            r, c = g.rc(e, n)
            if g.barred[r, c] or g.creek[r, c] or g.slope[r, c] > FLAT_MAX_DEG:
                continue
            pts.append((float(e), float(n)))
    idx = {p: i for i, p in enumerate(pts)}
    arr = np.array(pts)
    nbr = [[] for _ in pts]
    for i, a in enumerate(pts):
        dd = np.hypot(arr[:, 0] - a[0], arr[:, 1] - a[1])
        for j in np.nonzero((dd > 1.0) & (dd <= LEG_M))[0]:
            if j < i:
                continue
            got = g.leg(a, pts[j])
            if got and got[1] <= FLAT_MAX_DEG:
                w = got[0] + (CREEK_PENALTY_M if got[5] else 0.0)
                nbr[i].append((j, w))
                nbr[j].append((i, w))
    print("candidate points %d, flat legs %d (%.0f s)" % (len(pts), sum(len(x) for x in nbr) // 2, time.time() - t0))

    comp = [-1] * len(pts)
    for i in range(len(pts)):
        if comp[i] >= 0:
            continue
        stack, comp[i] = [i], i
        while stack:
            v = stack.pop()
            for u, _ in nbr[v]:
                if comp[u] < 0:
                    comp[u] = i
                    stack.append(u)
    home = comp[idx[wake]]

    def near_sea(p):
        r, c = g.rc(*p)
        return g.shore[r, c] <= SHORE_POINT_M

    steep = (g.slope >= BANK_DEG[0]) & (g.slope <= BANK_DEG[1])

    def by_bank(p):
        r, c = g.rc(*p)
        k = BANK_NEAR_CELLS
        return steep[max(0, r - k):r + k + 1, max(0, c - k):c + k + 1].sum() >= BANK_NEAR_COUNT

    seapts = [p for p in pts if comp[idx[p]] == home and near_sea(p)]
    bankpts = [p for p in pts if comp[idx[p]] == home and by_bank(p)]
    shore, bank = [], []
    for a in seapts:
        for b in seapts:
            length = math.hypot(b[0] - a[0], b[1] - a[1])
            if a != b and SHORE_LENGTH_M[0] <= length <= SHORE_LENGTH_M[1]:
                got = g.leg(a, b)
                if got and not got[5] and got[1] <= SHORE_SLOPE_DEG and got[3] <= SHORE_SAMPLE_M and got[4] <= SHORE_GROUND_M:
                    shore.append((got[0], a, b, got))
    for a in bankpts:
        for b in bankpts:
            length = math.hypot(b[0] - a[0], b[1] - a[1])
            if a != b and BANK_LENGTH_M[0] <= length <= BANK_LENGTH_M[1]:
                got = g.leg(a, b)
                if got and not got[5] and got[1] <= BANK_MAX_DEG and got[2] >= BANK_SHARE:
                    bank.append((got[0] * (0.5 + got[2]), a, b, got))
    shore.sort(key=lambda x: -x[0])
    bank.sort(key=lambda x: -x[0])
    shore, bank = shore[:KEEP], bank[:KEEP]
    print("on the wake's side of the water: %d point(s) by the sea, %d by a bank; %d shore leg(s), %d bank leg(s) kept"
          % (len(seapts), len(bankpts), len(shore), len(bank)))
    if not shore or not bank:
        return None

    dist_wake, _ = dijkstra(nbr, idx[wake])
    sp = {}
    for _, a, b, _ in shore + bank:
        if idx[b] not in sp:
            sp[idx[b]] = dijkstra(nbr, idx[b])
    loops = []
    for _, s1, s2, gs in shore:
        if idx[s1] not in dist_wake:
            continue
        d1, _ = sp[idx[s2]]
        for _, b1, b2, gb in bank:
            d2, _ = sp[idx[b2]]
            if idx[b1] not in d1 or idx[s1] not in d2:
                continue
            lap = gs[0] + d1[idx[b1]] + gb[0] + d2[idx[s1]]
            if not LAP_M[0] <= lap <= LAP_M[1]:
                continue
            score = gs[0] + gb[0] * (0.5 + gb[2]) - 0.12 * abs(lap - LAP_AIM_M) - 0.2 * dist_wake[idx[s1]]
            loops.append((score, s1, s2, b1, b2))
    loops.sort(key=lambda x: -x[0])
    print("%d loop(s) of the right length (%.0f s)" % (len(loops), time.time() - t0))
    for _, s1, s2, b1, b2 in loops:
        way = waypoints(g, pts, idx, sp, s1, s2, b1, b2)
        start = approach(g, wake, way)
        if start is not None:
            return way[start:] + way[:start]
    return None


def approach(g, wake, way):
    """Where the lap starts: the founder walks straight from the wake to the first waypoint, so it is the nearest
    waypoint that a straight, dry, flat walk from the wake reaches; None when no waypoint is."""
    best = None
    for k, (p, _, _) in enumerate(way):
        got = g.leg(wake, p)
        if got and not got[5] and got[1] <= FLAT_MAX_DEG and (best is None or got[0] < best[0]):
            best = (got[0], k)
    return None if best is None else best[1]


def waypoints(g, pts, idx, sp, s1, s2, b1, b2):
    """The loop as waypoints, each with the segment of the leg that ends there and whether that leg is sprinted."""

    def pull(nodes):
        """Fewer waypoints: from each kept point, jump to the farthest point on the path one flat leg still reaches."""
        keep, i = [nodes[0]], 0
        while i < len(nodes) - 1:
            j = len(nodes) - 1
            while j > i + 1:
                got = g.leg(pts[nodes[i]], pts[nodes[j]])
                wet_path = any(g.leg(pts[nodes[k]], pts[nodes[k + 1]])[5] for k in range(i, j))
                if got and got[1] <= FLAT_MAX_DEG and (not got[5] or wet_path):
                    break
                j -= 1
            keep.append(nodes[j])
            i = j
        return keep

    way = [(s2, "shore", False)]                          # (point, the segment of the leg that ends there, sprint)

    def flat(nodes, last):
        for k in range(1, len(nodes)):
            got = g.leg(pts[nodes[k - 1]], pts[nodes[k]])
            way.append((pts[nodes[k]], "creek" if got[5] else (last if k == len(nodes) - 1 else "plain"), not got[5]))

    flat(pull(path(sp[idx[s2]][1], idx[s2], idx[b1])), "plain")
    way.append((b2, "bank", False))
    flat(pull(path(sp[idx[b2]][1], idx[b2], idx[s1])), "return")
    return way[-1:] + way[:-1]                            # the lap starts where the shore does


def report(g, wake, way):
    legs = [(wake, way[0][0], "approach")] + [(way[k - 1][0], way[k][0], way[k][1]) for k in range(1, len(way))] \
        + [(way[-1][0], way[0][0], way[0][1])]
    lap = sum(math.hypot(b[0] - a[0], b[1] - a[1]) for a, b, _ in legs[1:])
    wet, plains = [], 0.0
    for a, b, name in legs:
        s = g.survey(a, b)
        crossed = [w for w in s["water"] if w in (STREAM, LAKE, SWAMP, SEA) or (w == CREEK and name != "creek")]
        if crossed:
            wet.append("%s leg %s -> %s crosses water class(es) %s" % (name, a, b, crossed))
        if name == "shore":
            print("  the shore: %.0f m, %.0f to %.0f m from the sea, on ground %.1f to %.1f m, steepest %.1f degrees"
                  % (s["length"], s["sea"][0], s["sea"][1], s["ground"][0], s["ground"][1], s["steepest"]))
        elif name == "bank":
            print("  the bank: %.0f m, %.0f %% of it at %g to %g degrees, %.0f %% over %g, steepest %.1f"
                  % (s["length"], 100 * s["at_bank_slope"], BANK_DEG[0], BANK_DEG[1], 100 * s["over_bank_slope"], BANK_DEG[1], s["steepest"]))
        else:
            plains = max(plains, s["steepest"])
    print("  the flat legs and the approach: steepest %.1f degrees" % plains)
    print("  the lap %.0f m; the approach from the wake %.0f m" % (lap, math.hypot(way[0][0][0] - wake[0], way[0][0][1] - wake[1])))
    for line in wet:
        print("  WET: " + line)
    return lap, not wet


def write_routes(way, lap):
    raw = open(ROUTES, encoding="utf-8", newline="").read()
    crlf = "\r\n" in raw
    s = raw.replace("\r\n", "\n")
    body = "".join('                new Waypoint(%.1f, %.1f, "%s", %s),\n' % (p[0], p[1], name, "true" if sprint else "false")
                   for p, name, sprint in way)
    s, n = re.subn(r"(            return new\[\]\n            \{\n)(.*?)(            \};)", lambda m: m.group(1) + body + m.group(3), s, count=1, flags=re.S)
    if n != 1:
        raise ValueError("Routes.cs: the WakeLoop waypoints are not where this tool writes them")
    summary = "/// <summary>About %.2f km: along the water's edge, the flat way round to the bank, down it, and the flat way home.</summary>" % (lap / 1000.0)
    s, n = re.subn(r"/// <summary>About [^<]*</summary>(\n        public static Waypoint\[\] WakeLoop\(\))", lambda m: summary + m.group(1), s, count=1)
    if n != 1:
        raise ValueError("Routes.cs: WakeLoop's summary is not where this tool writes it")
    if crlf:
        s = s.replace("\n", "\r\n")
    open(ROUTES, "w", encoding="utf-8", newline="").write(s)
    print("wrote %d waypoints into %s" % (len(way), os.path.relpath(ROUTES, ROOT)))


def main(argv):
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("world", nargs="?", default=DEFAULT_WORLD)
    parser.add_argument("--write", action="store_true", help="write the waypoints into Routes.WakeLoop")
    args = parser.parse_args(argv[1:])
    try:
        saved = json.load(open(os.path.join(ROOT, args.world, "world.json"), encoding="utf-8"))
        wake = (float(saved["wake_east"]), float(saved["wake_north"]))
        g = Ground(args.world)
    except (OSError, KeyError, ValueError) as error:
        print("lay_loop: cannot read the world at %s: %s" % (args.world, error))
        return 2
    print("the wake of %s: east %.0f north %.0f" % (args.world, wake[0], wake[1]))
    way = lay(g, wake)
    if way is None:
        print("lay_loop: no loop within %.0f m of the wake meets the criteria" % RADIUS_M)
        return 1
    lap, dry = report(g, wake, way)
    if not dry:
        print("lay_loop: the loop crosses water it may not; not written")
        return 1
    for p, name, sprint in way:
        print('                new Waypoint(%.1f, %.1f, "%s", %s),' % (p[0], p[1], name, "true" if sprint else "false"))
    if args.write:
        write_routes(way, lap)
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
