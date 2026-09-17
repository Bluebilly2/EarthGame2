#!/usr/bin/env python3
"""lay_loop.py: lay the corpus's walking loop round a world's wake, and write it into Routes.WakeLoop.

The walking scenarios (ARCHITECTURE section 7.1, N2) walk a loop that must start near where the founder wakes and must
hold the named divergence segments of CANON rulings 11 and 13: a shore walked along the water's edge and a bank of the
slope N2 was specified on (22 to 28 degrees, 2026-09-08), and since M1.5d (2026-09-11) a cliff's top edge, a shore
platform and a wade into the sea, the three places ruling 13 names, which the loop had never walked. The scorer's wake
moves whenever the layer rules change (DEBTS.md, "The corpus loop is tied to one world's wake"); the first re-laying
was this search done by hand (2026-09-10), and this is that search as a tool. Claude's tool: it lays a loop and
surveys it against its own criteria; whether founders walk it without being corrected is N2's question, measured by
the corpus and judged by join_check.

The search, over the world's own layers:
  - candidate points on a 16 m grid within 560 m of the wake, and on an 8 m grid within two cells of a cliff's top
    edge, dry, and no steeper than 12 degrees;
  - legs between points up to 96 m apart, sampled every 2 m: never across a stream, a lake, a swamp or the sea;
    across a creek only at a 250 m penalty, so only where there is no dry way, and then labelled a creek leg;
  - no waypoint inside a tree: trunks stop a founder since M1.6b, so a point within a trunk's bark and the body's
    own radius is not a candidate. The legs themselves are not laid clear of the trunks, because at this region's
    stand — 155 trees a hectare, and 22 per cent of its ground within a body's reach of a trunk — no straight leg of
    any length is clear of them: the first run of this tool with such a rule kept 0 shore, bank, cliff and platform
    legs out of 18,195. A walker goes round a tree instead (`RouteFollower`, M1.6b);
  - a shore leg: 100 to 230 m between points no more than 12 m from the sea, every sample no more than 16 m from
    it, on ground no higher than 2.5 m and no steeper than 10 degrees;
  - a bank leg: 30 to 100 m between points with six or more 22-28 degree cells within 32 m, a fifth or more of its
    samples at 22 to 28 degrees and none over 30 (up to 170 m until M1.5d, when a bank walked at Tobler's pace down
    such a slope, well under a metre a second, was found to cost most of the ten-minute walk);
  - a cliff leg: 16 to 120 m between points near a cliff's top edge, half or more of its samples within a cell of
    the brink — a walkable cell with a face a metre or more below it beside it, a face being a cell the topology calls
    cliff and steeper than 30 degrees — none on a face and none steeper than 20 degrees;
  - a platform leg: 24 to 150 m between points on shore platform, four fifths or more of its samples on it and none
    steeper than 12 degrees;
  - a wade: from a point no more than 30 m from the sea out to sea water 0.5 to 0.9 m deep, 8 to 40 m away, no
    deeper than a metre and no steeper than 12 degrees on the way, and back;
  - the loop: the shore, the shortest flat way to the bank, the bank, the shortest flat way back, 700 to 2,300 m;
    then the cliff, the platform and the wade each put in, walked the way round, where they add the least flat
    walking;
  - the lap starts at the nearest of its waypoints that a straight, dry, flat walk from the wake reaches, because the
    founder walks straight from where they wake to the first waypoint (the first run of this tool found a loop whose
    own legs were dry and whose walk from the wake crossed a swamp);
  - the walk from the wake until the last of N2's named legs is done must take no more than 500 s at the mover's own
    pace (run on the flat legs, walked on the named ones, half pace wading), so the ten-minute walk reaches every
    named segment with time to spare (the first loop this tool laid with the new legs was 1.9 km, about a quarter of
    an hour, and a ten-minute walk would never have reached the shore); until 2026-09-16 the whole lap was budgeted,
    and a pass laid after the last named leg is the soak's business, not the walk's. The candidates for each named
    leg are the nearest the wake, and of the loops that fit, the one with the most time on the named segments is laid;
  - a pass (2026-09-16, DEBTS "The corpus's loop startles no animal"): `--pass LABEL EAST NORTH` puts a waypoint
    within a stated distance of a place, so the soak's founders walk within an animal group's flight distance
    (M1.7c: a mob runs from a founder 80 m off, a pair 60 m) wherever presence's wander has carried it: the distance
    asked is the kind's flight distance less presence's 40 m wander and the group's own spread (five metres times the
    root of its size), so 25 m for a mob and 12 m for a pair, or the number given as a fourth word. Where the groups
    stand comes from a soak's own host log: `--groups <soak/server/run.jsonl>` lists every group it stood up, its mean
    place and the pass to ask for. A pass is put in where it adds the least flat walking, like the cliff, the platform
    and the wade;
  - a water leg (`--water`, the same day): a waypoint on dry ground within a body's reach of fresh water that stands
    (a creek, a stream or a lake with the world's surface at least 2 cm over the ground, WorldState.StandingWaterM
    restated), so a thirsty founder on the loop can drink (FP.1) without leaving it far. Asked for and not found
    within the radius, the tool says how far the nearest such water is and lays nothing: on the gate world of
    2026-09-16 no creek or stream carries a surface at all (WorldLayers sets the surface to the ground everywhere but
    the sea and the lakes), and the nearest lake with water in it is 1,977 m from the wake.

Usage, from the repository root:
    python Tools/corpus/lay_loop.py [world folder] [--write] [--radius 560] [--pass LABEL EAST NORTH [WITHIN]]...
                                    [--water] [--groups <run.jsonl>]
(default Artefacts/worlds/gate). Prints the survey of the loop it chose and its waypoints; with --write it replaces
the waypoints in Engine/packages/com.earthgame.clientcore/Runtime/Routes.cs. A player carries the loop, so it is
rebuilt before the corpus walks it. Exit 0 when a loop with every named segment and every pass asked for was laid,
1 when none meets the criteria, 2 when the world cannot be read.
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
# The water layer's classes and the topology layer's flags, as their legends state them.
CREEK, STREAM, LAKE, SWAMP, SEA = 3, 4, 5, 6, 7
TOPO_CLIFF, TOPO_PLATFORM = 128, 256

RADIUS_M = 560.0
GRID_M = 16.0
EDGE_GRID_M = 8.0
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
BANK_LENGTH_M = (30.0, 100.0)
CLIFF_FACE_DEG, CLIFF_DROP_M = 30.0, 1.0
EDGE_MAX_DEG, CLIFF_SHARE, CLIFF_NEAR_CELLS = 20.0, 0.5, 2
CLIFF_LENGTH_M = (16.0, 120.0)
PLATFORM_SHARE = 0.8
PLATFORM_LENGTH_M = (24.0, 150.0)
WADE_SHORE_M = 30.0
WADE_DEPTH_M = (0.5, 0.9)
WADE_MAX_DEPTH_M = 1.0
WADE_LENGTH_M = (8.0, 40.0)
WADE_STEP_M = 4.0
# Trunks (M1.6b). The founder's capsule is MoverConfig.CapsuleRadius and the stoutest butt radius any tree is drawn
# with is StandForms' old-man banksia, as a share of its height; both are restated here, and a corridor laid with them
# is wide enough for any tree, since every other form is slenderer.
BODY_RADIUS_M = 0.35
STOUTEST_TRUNK_SHARE = 0.060
TRUNK_MARGIN_M = 0.25
BLOCK_CELL_M = 2.0
# The stand code's five low bits count steps of this (StandCodes.HeightStepM), restated.
HEIGHT_MASK, HEIGHT_STEP_M = 0x1F, 1.25
BASE_LAP_M = (700.0, 2300.0)
LAP_MAX_M = 3000.0
LAP_AIM_M = 1400.0
WALK_BUDGET_S = 500.0
KEEP = 40
KEEP_EXTRA = 12
EXTRAS = ("cliff", "platform", "wade")
NAMED = ("shore", "bank") + EXTRAS
# A pass by an animal group (M1.7c restated): how near a founder sends a kind running, presence's wander round a group's
# own place, and the spread of its members round the group (AnimalStandUp.MemberSpacingM times the root of the size).
FLEE_WITHIN_M = {"mob": 80.0, "pair": 60.0}
GROUP_SIZE = {"mob": 8, "pair": 2}
WANDER_M = 40.0
MEMBER_SPACING_M = 5.0
# The water leg: water stands where the surface is this much over the ground (WorldState.StandingWaterM restated), and a
# founder drinks from within a body's reach (Hands.ReachM, from the eye), less a stride so the crosshair can find it.
STANDING_WATER_M = 0.02
DRINK_FROM_M = 3.0
FRESH = (CREEK, STREAM, LAKE)


def layer(world, name):
    path = os.path.join(ROOT, world, "layers", name + ".json")
    sidecar = json.load(open(path, encoding="utf-8"))
    raw = os.path.join(os.path.dirname(path), sidecar["raw"])
    return sidecar, np.fromfile(raw, dtype=NP_DTYPES[sidecar["dtype"]]).reshape(sidecar["height"], sidecar["width"])


def mix64(x):
    """splitmix64's finaliser, as StandLayout.Mix runs it: the same bits from the same cell in Python and in C#."""
    x = x + np.uint64(0x9E3779B97F4A7C15)
    x = (x ^ (x >> np.uint64(30))) * np.uint64(0xBF58476D1CE4E5B9)
    x = (x ^ (x >> np.uint64(27))) * np.uint64(0x94D049BB133111EB)
    return x ^ (x >> np.uint64(31))


def trunk_offsets(rows, cols, cell_cm):
    """Where the trunks of those cells stand inside them, whole centimetres east and north: StandLayout.Place for a
    trunk (kind 1, index 0), restated, so a route is laid round the trees the client draws."""
    h = mix64((rows.astype(np.uint64) << np.uint64(32)) | cols.astype(np.uint64))
    h = mix64(h ^ (np.uint64(1) << np.uint64(56)))
    east = (h % np.uint64(cell_cm)).astype(np.int64) - cell_cm // 2
    north = ((h >> np.uint64(21)) % np.uint64(cell_cm)).astype(np.int64) - cell_cm // 2
    return east, north


def around(mask, cells):
    """A mask grown by some cells every way."""
    out = mask.copy()
    for dr in range(-cells, cells + 1):
        for dc in range(-cells, cells + 1):
            if dr or dc:
                out |= np.roll(np.roll(mask, dr, axis=0), dc, axis=1)
    return out


class Ground:
    def __init__(self, world):
        side, self.z = layer(world, "heights")
        _, self.water = layer(world, "water")
        _, self.shore = layer(world, "shore_distance")
        _, topology = layer(world, "topology")
        _, surface = layer(world, "surface")
        self.cell, self.half = side["cell_m"], side["extent_m"] / 2.0
        z = self.z.astype(np.float64)
        gy, gx = np.gradient(z, self.cell)
        self.slope = np.degrees(np.arctan(np.hypot(gx, gy)))
        self.depth = surface.astype(np.float64) - z
        self.barred = np.isin(self.water, [STREAM, LAKE, SWAMP, SEA])
        self.creek = self.water == CREEK
        self.platform = (topology & TOPO_PLATFORM) != 0
        self.face = ((topology & TOPO_CLIFF) != 0) & (self.slope > CLIFF_FACE_DEG)
        below = np.zeros(self.face.shape, dtype=bool)
        for dr in (-1, 0, 1):
            for dc in (-1, 0, 1):
                if dr or dc:
                    below |= np.roll(np.roll(self.face, dr, axis=0), dc, axis=1) & (np.roll(np.roll(z, dr, axis=0), dc, axis=1) < z - CLIFF_DROP_M)
        walkable = ~self.face & ~self.barred & (self.slope <= EDGE_MAX_DEG)
        self.edge = below & walkable
        self.edge_near = around(self.edge, 1) & walkable
        # Fresh water that stands: a creek, a stream or a lake whose surface is over its ground (the water leg, 2026-09-16).
        self.drinkable = np.isin(self.water, FRESH) & (self.depth >= STANDING_WATER_M)
        self.blocked = self.block_trunks(world)
        self.cache = {}

    def block_trunks(self, world):
        """The ground a founder cannot walk through (M1.6b): within a trunk's drawn radius, the body's own and a little
        room to spare, of every tree the stand layer stands, marked on a two-metre grid. None where the world has no
        stand layer (a world made before M1.6a)."""
        if not os.path.exists(os.path.join(ROOT, world, "layers", "stand.json")):
            return None
        _, stand = layer(world, "stand")
        codes = stand.astype(np.int64)
        rows, cols = np.nonzero(codes)
        n = int(round(2.0 * self.half / BLOCK_CELL_M)) + 1
        blocked = np.zeros((n, n), dtype=bool)
        if not len(rows):
            return blocked
        heights = (codes[rows, cols] & HEIGHT_MASK) * HEIGHT_STEP_M
        east_cm, north_cm = trunk_offsets(rows, cols, int(round(self.cell * 100.0)))
        east = cols * self.cell - self.half + east_cm / 100.0
        north = self.half - rows * self.cell + north_cm / 100.0
        # Half a cell over the bark, so that what is marked covers the whole of every trunk's room.
        radius = STOUTEST_TRUNK_SHARE * heights + BODY_RADIUS_M + TRUNK_MARGIN_M + BLOCK_CELL_M / 2.0
        r0 = np.rint((self.half - north) / BLOCK_CELL_M).astype(np.int64)
        c0 = np.rint((east + self.half) / BLOCK_CELL_M).astype(np.int64)
        reach = int(math.ceil(float(radius.max()) / BLOCK_CELL_M))
        for dr in range(-reach, reach + 1):
            for dc in range(-reach, reach + 1):
                rr, cc = r0 + dr, c0 + dc
                de = (cc * BLOCK_CELL_M - self.half) - east
                dn = (self.half - rr * BLOCK_CELL_M) - north
                hit = (de * de + dn * dn <= radius * radius) & (rr >= 0) & (rr < n) & (cc >= 0) & (cc < n)
                blocked[rr[hit], cc[hit]] = True
        return blocked

    def in_a_trunk(self, east, north):
        """Whether a founder standing there would be inside a tree."""
        if self.blocked is None:
            return False
        n = self.blocked.shape[0]
        r = min(max(int(round((self.half - north) / BLOCK_CELL_M)), 0), n - 1)
        c = min(max(int(round((east + self.half) / BLOCK_CELL_M)), 0), n - 1)
        return bool(self.blocked[r, c])

    def trunks_blocked_share(self):
        """How much of the region lies within a body's reach of a trunk: what a route laid to miss every one would
        have to thread."""
        return 0.0 if self.blocked is None else float(self.blocked.mean())

    def rc(self, east, north):
        return int(round((self.half - north) / self.cell)), int(round((east + self.half) / self.cell))

    def samples(self, a, b):
        length = math.hypot(b[0] - a[0], b[1] - a[1])
        k = max(2, int(length / SAMPLE_M))
        return length, [self.rc(a[0] + (b[0] - a[0]) * i / k, a[1] + (b[1] - a[1]) * i / k) for i in range(k + 1)]

    def cells(self, a, b):
        length, cells = self.samples(a, b)
        return length, np.array([r for r, _ in cells]), np.array([c for _, c in cells])

    def leg(self, a, b):
        """(length, steepest, share at the bank's slope, farthest from the sea, highest ground, crosses a creek), or
        None where the leg crosses water it may not."""
        key = (a, b) if a <= b else (b, a)
        if key in self.cache:
            return self.cache[key]
        length, rows, cols = self.cells(a, b)
        out = None
        if not self.barred[rows, cols].any():
            s = self.slope[rows, cols]
            out = (length, float(s.max()), float(((s >= BANK_DEG[0]) & (s <= BANK_DEG[1])).mean()),
                   float(self.shore[rows, cols].max()), float(self.z[rows, cols].max()), bool(self.creek[rows, cols].any()))
        self.cache[key] = out
        return out

    def cliff_leg(self, a, b):
        """(length, share within a cell of the brink) for a leg along a cliff's top edge, or None."""
        length, rows, cols = self.cells(a, b)
        if self.barred[rows, cols].any() or self.face[rows, cols].any() or self.slope[rows, cols].max() > EDGE_MAX_DEG:
            return None
        share = float(self.edge_near[rows, cols].mean())
        return (length, share) if share >= CLIFF_SHARE else None

    def platform_leg(self, a, b):
        """(length, share on shore platform) for a leg over a platform, or None."""
        length, rows, cols = self.cells(a, b)
        if self.barred[rows, cols].any() or self.slope[rows, cols].max() > FLAT_MAX_DEG:
            return None
        share = float(self.platform[rows, cols].mean())
        return (length, share) if share >= PLATFORM_SHARE else None

    def wade_leg(self, a, w):
        """(length, depth at the far end) for a walk from a dry point out into the sea, or None."""
        length, rows, cols = self.cells(a, w)
        if np.isin(self.water[rows, cols], [STREAM, LAKE, SWAMP]).any() or self.slope[rows, cols].max() > FLAT_MAX_DEG:
            return None
        r, c = self.rc(*w)
        if self.depth[rows, cols].max() > WADE_MAX_DEPTH_M or self.water[r, c] != SEA or not WADE_DEPTH_M[0] <= self.depth[r, c] <= WADE_DEPTH_M[1]:
            return None
        return length, float(self.depth[r, c])

    def survey(self, a, b):
        length, rows, cols = self.cells(a, b)
        s = self.slope[rows, cols]
        return {
            "length": length, "steepest": float(s.max()),
            "at_bank_slope": float(((s >= BANK_DEG[0]) & (s <= BANK_DEG[1])).mean()),
            "over_bank_slope": float((s > BANK_DEG[1]).mean()),
            "ground": (float(self.z[rows, cols].min()), float(self.z[rows, cols].max())),
            "sea": (float(self.shore[rows, cols].min()), float(self.shore[rows, cols].max())),
            "water": sorted(set(int(v) for v in self.water[rows, cols])),
            "edge": float(self.edge_near[rows, cols].mean()),
            "platform": float(self.platform[rows, cols].mean()),
            "deepest": float(self.depth[rows, cols].max()),
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


def candidate_points(g, wake, radius, places=()):
    """The points a leg may start or end at: the coarse grid within the radius, the fine grid near a cliff's top edge, and
    the fine grid within two of a pass's distances of each place passed (2026-09-16), so a pass can come as near as asked."""
    pts, seen = [wake], {wake}

    def add(e, n):
        p = (float(e), float(n))
        if p in seen or math.hypot(e - wake[0], n - wake[1]) > radius:
            return
        r, c = g.rc(e, n)
        if g.barred[r, c] or g.creek[r, c] or g.slope[r, c] > FLAT_MAX_DEG or g.in_a_trunk(e, n):
            return
        seen.add(p)
        pts.append(p)

    for e in np.arange(wake[0] - radius, wake[0] + radius + 1, GRID_M):
        for n in np.arange(wake[1] - radius, wake[1] + radius + 1, GRID_M):
            add(e, n)
    near_edge = around(g.edge, CLIFF_NEAR_CELLS)
    for e in np.arange(wake[0] - radius, wake[0] + radius + 1, EDGE_GRID_M):
        for n in np.arange(wake[1] - radius, wake[1] + radius + 1, EDGE_GRID_M):
            if near_edge[g.rc(e, n)]:
                add(e, n)
    for (pe, pn), within in places:
        span = 2.0 * within
        for e in np.arange(pe - span, pe + span + 1, EDGE_GRID_M):
            for n in np.arange(pn - span, pn + span + 1, EDGE_GRID_M):
                add(e, n)
    return pts


def pass_legs(g, ours, place, within):
    """Zero-length legs at the candidate points within a distance of a place, the nearest the place first: a pass by an
    animal group's place (2026-09-16), so the founder walks within the kind's flight distance wherever the group wanders."""
    legs = []
    for p in ours:
        off = math.hypot(p[0] - place[0], p[1] - place[1])
        if off <= within:
            legs.append((off, p, p, None))
    legs.sort(key=lambda x: x[0])
    return legs[:KEEP_EXTRA]


def water_legs(g, ours, wake):
    """Zero-length legs at the candidate points within a body's reach of fresh water that stands, the nearest the wake
    first (the water leg, 2026-09-16)."""
    reach = int(math.ceil(DRINK_FROM_M / g.cell))
    by_water = around(g.drinkable, reach)
    legs = [(math.hypot(p[0] - wake[0], p[1] - wake[1]), p, p, None) for p in ours if by_water[g.rc(*p)]]
    legs.sort(key=lambda x: x[0])
    return legs[:KEEP_EXTRA]


def nearest_drinkable_m(g, wake):
    """How far from the wake the nearest fresh water that stands is, m; infinite where the world has none."""
    rows, cols = np.nonzero(g.drinkable)
    if not len(rows):
        return float("inf")
    east, north = cols * g.cell - g.half, g.half - rows * g.cell
    return float(np.hypot(east - wake[0], north - wake[1]).min())


def from_wake(wake, a, b):
    """How far a leg's middle is from the wake, m: the candidates kept are the nearest, so a lap can be short."""
    return math.hypot((a[0] + b[0]) / 2.0 - wake[0], (a[1] + b[1]) / 2.0 - wake[1])


def cliff_legs(g, near, wake):
    legs = []
    for i, a in enumerate(near):
        for b in near[i + 1:]:
            length = math.hypot(b[0] - a[0], b[1] - a[1])
            if CLIFF_LENGTH_M[0] <= length <= CLIFF_LENGTH_M[1] and g.cliff_leg(a, b):
                legs.append((from_wake(wake, a, b), a, b, None))
    legs.sort(key=lambda x: x[0])
    return legs[:KEEP_EXTRA]


def platform_legs(g, on, wake):
    legs = []
    for i, a in enumerate(on):
        for b in on[i + 1:]:
            length = math.hypot(b[0] - a[0], b[1] - a[1])
            if PLATFORM_LENGTH_M[0] <= length <= PLATFORM_LENGTH_M[1] and g.platform_leg(a, b):
                legs.append((from_wake(wake, a, b), a, b, None))
    legs.sort(key=lambda x: x[0])
    return legs[:KEEP_EXTRA]


def wade_legs(g, starts, wake):
    legs = []
    span = WADE_LENGTH_M[1]
    steps = np.arange(-span, span + 0.1, WADE_STEP_M)
    for a in starts:
        best = None
        for de in steps:
            for dn in steps:
                if not WADE_LENGTH_M[0] <= math.hypot(de, dn) <= WADE_LENGTH_M[1]:
                    continue
                w = (a[0] + float(de), a[1] + float(dn))
                got = g.wade_leg(a, w)
                if got and (best is None or got[0] < best[0]):
                    best = (got[0], w)
        if best:
            legs.append((from_wake(wake, a, best[1]), a, a, best[1]))
    legs.sort(key=lambda x: x[0])
    return legs[:KEEP_EXTRA]


def insert(legs, extras, idx, sp_of):
    """Each extra kind of leg put in where it adds the least flat walking, the way round it is walked chosen with it: the
    cliff, the platform and the wade, then any pass and the water leg, in the order the extras are given."""
    legs = list(legs)
    for kind in extras:
        best = None
        for _, a, b, extra in extras[kind]:
            for x, y in (((a, b), (b, a)) if a != b else ((a, b),)):
                dx, dy = sp_of(idx[x])[0], sp_of(idx[y])[0]
                for k in range(len(legs)):
                    prev_end, next_start = legs[k][1], legs[(k + 1) % len(legs)][0]
                    d1, d2 = dx.get(idx[prev_end]), dy.get(idx[next_start])
                    d0 = sp_of(idx[prev_end])[0].get(idx[next_start])
                    if d1 is None or d2 is None or d0 is None:
                        continue
                    added = d1 + d2 - d0
                    if best is None or added < best[0]:
                        best = (added, k, (x, y, kind, extra))
        if best is not None:
            legs.insert(best[1] + 1, best[2])
    return legs


def pace(slope, run, wading):
    """How fast the founder goes on a slope, m/s, as the mover walks (Locomotion; CANON ruling 18): Tobler's 6 km/h at a
    five per cent descent, falling away exponentially either side of it, times the travel pace's 1.3 and never under
    0.45 m/s on ground that can be stood on; wading half of that and never run. A run is counted at the jog's 1.7 times
    the walk, under the 2.6 the mover runs at, so a lap is reckoned slower than it is walked and the budget errs long.
    Restated here only to budget a lap into the ten-minute walk, never to judge one."""
    speed = max(0.45, 6.0 * math.exp(-3.5 * abs(slope + 0.05)) / 3.6 * 1.3)
    return speed * (0.5 if wading else (1.7 if run else 1.0))


def leg_seconds(g, a, b, run, wading):
    length, rows, cols = g.cells(a, b)
    z = g.z[rows, cols].astype(np.float64)
    step = length / max(1, len(z) - 1)
    if step <= 0.0:
        return 0.0
    return sum(step / pace((z[i + 1] - z[i]) / step, run, wading) for i in range(len(z) - 1))


def walk_seconds(g, wake, way):
    """How long the walk from the wake and one lap take, how much of it is on the named segments, and how long it is
    until the last of N2's named legs is done, s: the budget's measure since 2026-09-16, so a pass laid after them
    lengthens the lap without costing the ten-minute walk a named segment."""
    total = named = to_named = 0.0
    prev = wake
    for p, segment, run in way + [way[0]]:
        seconds = leg_seconds(g, prev, p, run, segment == "wade")
        total += seconds
        if segment in NAMED:
            named += seconds
            to_named = total
        prev = p
    return total, named, to_named


def lap_length(legs, idx, sp_of):
    total = 0.0
    for k, (a, b, kind, extra) in enumerate(legs):
        total += 2.0 * math.hypot(extra[0] - a[0], extra[1] - a[1]) if kind == "wade" else math.hypot(b[0] - a[0], b[1] - a[1])
        d = sp_of(idx[b])[0].get(idx[legs[(k + 1) % len(legs)][0]])
        if d is None:
            return None
        total += d
    return total


def lay(g, wake, radius=RADIUS_M, passes=(), water=False):
    """The loop, as (point, segment, run) waypoints, or None. `passes` are (label, place, within) to lay a waypoint by."""
    t0 = time.time()
    pts = candidate_points(g, wake, radius, [(place, within) for _, place, within in passes])
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
    ours = [p for p in pts if comp[idx[p]] == home]

    def near_sea(p):
        r, c = g.rc(*p)
        return g.shore[r, c] <= SHORE_POINT_M

    steep = (g.slope >= BANK_DEG[0]) & (g.slope <= BANK_DEG[1])

    def by_bank(p):
        r, c = g.rc(*p)
        k = BANK_NEAR_CELLS
        return steep[max(0, r - k):r + k + 1, max(0, c - k):c + k + 1].sum() >= BANK_NEAR_COUNT

    seapts = [p for p in ours if near_sea(p)]
    bankpts = [p for p in ours if by_bank(p)]
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
                    bank.append((-from_wake(wake, a, b), a, b, got))
    shore.sort(key=lambda x: -x[0])
    bank.sort(key=lambda x: -x[0])
    shore, bank = shore[:KEEP], bank[:KEEP]
    near_edge = around(g.edge, CLIFF_NEAR_CELLS)
    extras = {
        "cliff": cliff_legs(g, [p for p in ours if near_edge[g.rc(*p)]], wake),
        "platform": platform_legs(g, [p for p in ours if g.platform[g.rc(*p)]], wake),
        "wade": wade_legs(g, [p for p in ours if g.shore[g.rc(*p)] <= WADE_SHORE_M], wake),
    }
    for label, place, within in passes:
        extras[label] = pass_legs(g, ours, place, within)
        if not extras[label]:
            nearest = min((math.hypot(p[0] - place[0], p[1] - place[1]) for p in ours), default=float("inf"))
            print("no dry, flat point within %.0f m of the %s's place at east %.0f north %.0f on the wake's side of the water (the nearest %.0f m off; widen --radius?)"
                  % (within, label, place[0], place[1], nearest))
            return None
    if water:
        extras["water"] = water_legs(g, ours, wake)
        if not extras["water"]:
            print("no dry point within %.0f m of fresh water that stands, within %.0f m of the wake: the nearest such water is %.0f m from the wake"
                  % (DRINK_FROM_M, radius, nearest_drinkable_m(g, wake)))
            return None
    print("on the wake's side of the water: %d point(s) by the sea, %d by a bank; %d shore leg(s), %d bank leg(s), %s kept (%.0f s)"
          % (len(seapts), len(bankpts), len(shore), len(bank), ", ".join("%d %s" % (len(extras[k]), k) for k in extras), time.time() - t0))
    if not shore or not bank:
        return None

    cache = {}

    def sp_of(node):
        if node not in cache:
            cache[node] = dijkstra(nbr, node)
        return cache[node]

    dist_wake = sp_of(idx[wake])[0]
    loops = []
    for _, s1, s2, gs in shore:
        if idx[s1] not in dist_wake:
            continue
        d1 = sp_of(idx[s2])[0]
        for _, b1, b2, gb in bank:
            d2 = sp_of(idx[b2])[0]
            if idx[b1] not in d1 or idx[s1] not in d2:
                continue
            lap = gs[0] + d1[idx[b1]] + gb[0] + d2[idx[s1]]
            if not BASE_LAP_M[0] <= lap <= BASE_LAP_M[1]:
                continue
            score = gs[0] + gb[0] * (0.5 + gb[2]) - 0.12 * abs(lap - LAP_AIM_M) - 0.2 * dist_wake[idx[s1]]
            loops.append((score, s1, s2, b1, b2))
    loops.sort(key=lambda x: -x[0])
    print("%d loop(s) of the right length (%.0f s)" % (len(loops), time.time() - t0))
    best = None
    for _, s1, s2, b1, b2 in loops:
        legs = insert([(s1, s2, "shore", None), (b1, b2, "bank", None)], extras, idx, sp_of)
        lap = lap_length(legs, idx, sp_of)
        if lap is None or lap > LAP_MAX_M:
            continue
        way = waypoints(g, pts, idx, sp_of, legs)
        start = approach(g, wake, way)
        if start is None:
            continue
        way = way[start:] + way[:start]
        total, named, to_named = walk_seconds(g, wake, way)
        if to_named > WALK_BUDGET_S:
            continue
        score = named - 0.25 * total
        if best is None or score > best[0]:
            best = (score, way, total, named, to_named)
    if best is None:
        return None
    print("laid in %.0f s: the walk from the wake and a lap take %.0f s at the mover's pace, %.0f s of it on the named segments; the last named leg is done by %.0f s (budget %.0f s)"
          % (time.time() - t0, best[2], best[3], best[4], WALK_BUDGET_S))
    return best[1]


def approach(g, wake, way):
    """Where the lap starts: the founder walks straight from the wake to the first waypoint, so it is the nearest
    waypoint that a straight, dry, flat walk from the wake reaches; None when no waypoint is."""
    best = None
    for k, (p, _, _) in enumerate(way):
        got = g.leg(wake, p)
        if got and not got[5] and got[1] <= FLAT_MAX_DEG and (best is None or got[0] < best[0]):
            best = (got[0], k)
    return None if best is None else best[1]


def waypoints(g, pts, idx, sp_of, legs):
    """The loop as waypoints, each with the segment of the leg that ends there and whether that leg is run: every named
    leg walked, and the flat way from each to the next, the last of those labelled the return."""

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

    way = []                                              # (point, the segment of the leg that ends there, run)

    def flat(nodes, last):
        for k in range(1, len(nodes)):
            got = g.leg(pts[nodes[k - 1]], pts[nodes[k]])
            way.append((pts[nodes[k]], "creek" if got[5] else (last if k == len(nodes) - 1 else "plain"), not got[5]))

    for k, (a, b, kind, extra) in enumerate(legs):
        if kind == "wade":
            way.append((extra, "wade", False))            # out into the water, and back to where it began
            way.append((a, "wade", False))
        else:
            way.append((b, kind, False))
        nxt = legs[(k + 1) % len(legs)][0]
        if nxt != b:
            flat(pull(path(sp_of(idx[b])[1], idx[b], idx[nxt])), "return" if k == len(legs) - 1 else "plain")
    return way


def report(g, wake, way, passes=()):
    legs = [(wake, way[0][0], "approach")] + [(way[k - 1][0], way[k][0], way[k][1]) for k in range(1, len(way))] \
        + [(way[-1][0], way[0][0], way[0][1])]
    for label, place, within in passes:
        at = [p for p, segment, _ in way if segment == label]
        if at:
            print("  the pass by the %s: the waypoint at east %.0f north %.0f, %.1f m from the place asked (east %.0f north %.0f), within %.0f m"
                  % (label, at[0][0], at[0][1], math.hypot(at[0][0] - place[0], at[0][1] - place[1]), place[0], place[1], within))
    for p, segment, _ in way:
        if segment == "water":
            print("  the water: the waypoint at east %.0f north %.0f, fresh water standing within %.0f m" % (p[0], p[1], DRINK_FROM_M))
    lap = sum(math.hypot(b[0] - a[0], b[1] - a[1]) for a, b, _ in legs[1:])
    wet, plains = [], 0.0
    for a, b, name in legs:
        s = g.survey(a, b)
        allowed = {CREEK} if name == "creek" else ({SEA} if name == "wade" else set())
        crossed = [w for w in s["water"] if w in (CREEK, STREAM, LAKE, SWAMP, SEA) and w not in allowed]
        if crossed:
            wet.append("%s leg %s -> %s crosses water class(es) %s" % (name, a, b, crossed))
        if name == "shore":
            print("  the shore: %.0f m, %.0f to %.0f m from the sea, on ground %.1f to %.1f m, steepest %.1f degrees"
                  % (s["length"], s["sea"][0], s["sea"][1], s["ground"][0], s["ground"][1], s["steepest"]))
        elif name == "bank":
            print("  the bank: %.0f m, %.0f %% of it at %g to %g degrees, %.0f %% over %g, steepest %.1f"
                  % (s["length"], 100 * s["at_bank_slope"], BANK_DEG[0], BANK_DEG[1], 100 * s["over_bank_slope"], BANK_DEG[1], s["steepest"]))
        elif name == "cliff":
            print("  the cliff's edge: %.0f m, %.0f %% of it within a cell of the brink, steepest %.1f degrees"
                  % (s["length"], 100 * s["edge"], s["steepest"]))
        elif name == "platform":
            print("  the platform: %.0f m, %.0f %% of it on shore platform, steepest %.1f degrees"
                  % (s["length"], 100 * s["platform"], s["steepest"]))
        elif name == "wade":
            print("  the wade: %.0f m, deepest %.2f m" % (s["length"], s["deepest"]))
        else:
            plains = max(plains, s["steepest"])
    print("  the flat legs and the approach: steepest %.1f degrees" % plains)
    print("  the trees: %.0f %% of the region lies within a body's reach of a trunk, which the legs cross and a walker goes round"
          % (100 * g.trunks_blocked_share()))
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
    labels = [name for _, name, _ in way]
    passes = []
    for label in labels:
        if label not in NAMED and label not in ("plain", "return", "creek", "water") and label not in passes:
            passes.append(label)
    extra = ""
    if passes:
        extra += ", a pass by " + " and by ".join(("a kangaroo mob" if p == "mob" else "an oystercatcher pair" if p == "pair" else "the " + p) for p in passes)
    if "water" in labels:
        extra += ", a stand by fresh water"
    summary = ("/// <summary>About %.2f km: the shore, the bank, a cliff's top edge, a shore platform and a wade into the sea%s, "
               "and the flat ways between them.</summary>" % (lap / 1000.0, extra))
    s, n = re.subn(r"/// <summary>About [^<]*</summary>(\n        public static Waypoint\[\] WakeLoop\(\))", lambda m: summary + m.group(1), s, count=1)
    if n != 1:
        raise ValueError("Routes.cs: WakeLoop's summary is not where this tool writes it")
    if crlf:
        s = s.replace("\n", "\r\n")
    open(ROUTES, "w", encoding="utf-8", newline="").write(s)
    print("wrote %d waypoints into %s" % (len(way), os.path.relpath(ROUTES, ROOT)))


def groups_of(run_jsonl):
    """The animal groups a host stood up, from its fauna records: by kind and square (the id's layout, ARCHITECTURE section
    10: the kind in the seven bits under the top, the square's east and north indices in twenty-four bits each, offset by
    2^23), each with its mean place over the records and how many times it was seen."""
    groups = {}
    with open(run_jsonl, encoding="utf-8") as f:
        for line in f:
            line = line.strip()
            if not line:
                continue
            record = json.loads(line)
            if record.get("kind") != "fauna":
                continue
            ids, positions = record.get("ids", []), record.get("positions", [])
            for i, animal_id in enumerate(ids):
                animal_id = int(animal_id)
                if not (animal_id >> 63) & 1 or len(positions) < 3 * (i + 1):
                    continue
                kind = (animal_id >> 56) & 0x7F
                square = (((animal_id >> 32) & 0xFFFFFF) - (1 << 23), ((animal_id >> 8) & 0xFFFFFF) - (1 << 23))
                g = groups.setdefault((kind, square), [0.0, 0.0, 0])
                g[0] += float(positions[3 * i])
                g[1] += float(positions[3 * i + 2])
                g[2] += 1
    return {key: (east / n, north / n, n) for key, (east, north, n) in groups.items() if n}


def print_groups(run_jsonl, wake):
    """What `--groups` prints: every group the soak stood up, nearest the wake first, and the pass to ask for."""
    labels = {1: "mob", 2: "pair"}
    rows = []
    for (kind, square), (east, north, n) in groups_of(run_jsonl).items():
        label = labels.get(kind, "kind %d" % kind)
        rows.append((math.hypot(east - wake[0], north - wake[1]), label, square, east, north, n))
    rows.sort()
    print("%d group(s) stood up in %s:" % (len(rows), os.path.relpath(run_jsonl, ROOT)))
    for off, label, square, east, north, n in rows:
        within = pass_within(label)
        print("  %s of square (%d, %d): mean place east %.0f north %.0f over %d sightings, %.0f m from the wake; ask --pass %s %.0f %.0f%s"
              % (label, square[0], square[1], east, north, n, off, label, east, north, " %.0f" % within if within is not None else ""))


def pass_within(label):
    """How near a pass must come to a group's place for a founder to be within its kind's flight distance wherever the
    wander and the members' spread have put them; None for a label that is no kind stood up."""
    if label not in FLEE_WITHIN_M:
        return None
    return math.floor(FLEE_WITHIN_M[label] - WANDER_M - MEMBER_SPACING_M * math.sqrt(GROUP_SIZE[label]))


def main(argv):
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("world", nargs="?", default=DEFAULT_WORLD)
    parser.add_argument("--write", action="store_true", help="write the waypoints into Routes.WakeLoop")
    parser.add_argument("--radius", type=float, default=RADIUS_M, help="how far from the wake candidate points are taken, m")
    parser.add_argument("--pass", dest="passes", action="append", nargs="+", metavar="WORD", default=[],
                        help="LABEL EAST NORTH [WITHIN]: a waypoint within WITHIN m of the place (mob 25, pair 12 unless given)")
    parser.add_argument("--water", action="store_true", help="a waypoint within a body's reach of fresh water that stands")
    parser.add_argument("--groups", metavar="RUN_JSONL", help="list the animal groups a soak's host stood up, and the passes to ask for, then stop")
    args = parser.parse_args(argv[1:])
    try:
        saved = json.load(open(os.path.join(ROOT, args.world, "world.json"), encoding="utf-8"))
        wake = (float(saved["wake_east"]), float(saved["wake_north"]))
        if args.groups:
            print_groups(args.groups, wake)
            return 0
        g = Ground(args.world)
    except (OSError, KeyError, ValueError) as error:
        print("lay_loop: cannot read the world at %s: %s" % (args.world, error))
        return 2
    passes = []
    for words in args.passes:
        if len(words) not in (3, 4):
            print("lay_loop: --pass takes LABEL EAST NORTH [WITHIN], not %r" % (words,))
            return 2
        within = float(words[3]) if len(words) == 4 else pass_within(words[0])
        if within is None:
            print("lay_loop: --pass %s needs a WITHIN, %s being no kind stood up (mob, pair)" % (words[0], words[0]))
            return 2
        passes.append((words[0], (float(words[1]), float(words[2])), within))
    print("the wake of %s: east %.0f north %.0f" % (args.world, wake[0], wake[1]))
    way = lay(g, wake, args.radius, passes, args.water)
    if way is None:
        print("lay_loop: no loop within %.0f m of the wake meets the criteria" % args.radius)
        return 1
    lap, dry = report(g, wake, way, passes)
    wanted = list(NAMED) + [label for label, _, _ in passes] + (["water"] if args.water else [])
    missing = [name for name in wanted if not any(segment == name for _, segment, _ in way)]
    if missing:
        print("lay_loop: the loop has no %s leg; not written" % ", no ".join(missing))
        return 1
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
