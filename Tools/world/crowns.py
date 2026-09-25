#!/usr/bin/env python3
"""crowns.py: how much of a world's ground its trees' crowns cover, a 40 m square at a time, read from the world's own
stand tree by tree and the way the far layers carry it (M1.6d: a square's commonest tall plant at its mean height, and
the count of all its trees), by the ground's slope, by each square's commonest tree, and along a view.

Written for M1.6g (2026-09-23), whose lookout frames showed the hills north of the whole valley's village paler than the
forest standing on them. A square's share is its crowns' area over its own, overlaps not taken off and never more than the
square, as `FarCanopy.ShareOf` counts it. The squares here are whole blocks of the stand's cells from the north-west
corner, where a far post takes the block round it, so a square here and a far post differ by half a square and agree in
their distribution. The layers are read by the stand check's readers and its crown table, restated from ECOSYSTEM.md
(`Tools/verifiers/checks/stand_check.py`), and nothing the game runs is imported.

Claude's tool: it reports; what the far forest should look like is the owner's to judge.

Usage, from the repository root:
    python Tools/world/crowns.py [--world Artefacts/worlds/valley-whole] [--look -5160,-3358,0 ...]

--look east,north,bearing: the squares in a 60 degree sector from that point toward the bearing (degrees east of north), in
bands of distance, with their slope and height. Exit 0 once printed; 2 when the world has no stand.
"""
import argparse
import math
from pathlib import Path
import sys

import numpy as np

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "Tools/verifiers/checks"))
from stand_check import CROWN_SHARE, height_step, layer, legend_names, stand_layout  # noqa: E402  (one owner of the readers and the table)

SQUARE_M = 40.0
SLOPE_BANDS = [(0, 10), (10, 20), (20, 25), (25, 30), (30, 35), (35, 45), (45, 90)]
DISTANCE_BANDS = [(500, 1500), (1500, 3000), (3000, 5000), (5000, 8000), (8000, 12000), (12000, 20000)]


def main(argv):
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--world", default="Artefacts/worlds/valley-whole")
    parser.add_argument("--look", action="append", default=[], help="east,north,bearing of a view to read along")
    args = parser.parse_args(argv)
    sidecar, stand = layer(args.world, "stand")
    if stand is None:
        print(f"crowns: {args.world} has no stand layer")
        return 2
    _, heights = layer(args.world, "heights")
    cell, extent = float(sidecar["cell_m"]), float(sidecar["extent_m"])
    k = int(round(SQUARE_M / cell))
    m = (stand.shape[0] - 1) // k
    names = legend_names(sidecar)
    step = height_step(sidecar)
    shift, mask = stand_layout(sidecar)
    tall = sorted(names)
    share_of = np.zeros(max(tall) + 1)
    for code, name in names.items():
        share_of[code] = CROWN_SHARE[name]

    # Every tree's own crown, a square at a time.
    blocks = stand[: m * k, : m * k].reshape(m, k, m, k).transpose(0, 2, 1, 3).reshape(m, m, k * k)
    species = (blocks >> shift).astype(np.int64)
    height = (blocks & mask).astype(np.float64) * step
    area = math.pi / 4.0 * (share_of[species] * height) ** 2
    own = np.minimum(1.0, area.sum(axis=2) / (SQUARE_M * SQUARE_M))
    count = (blocks > 0).sum(axis=2)

    # The far layers' way: the commonest tall plant (the first on a tie) at its trees' mean height, packed to the step, for all.
    per = np.stack([(species == i).sum(axis=2) for i in range(max(tall) + 1)], axis=2)
    per[..., 0] = 0
    most = per.argmax(axis=2)
    metres = np.stack([np.where(species == i, height, 0.0).sum(axis=2) for i in range(max(tall) + 1)], axis=2)
    mean_h = np.take_along_axis(metres, most[..., None], 2)[..., 0] / np.maximum(1, np.take_along_axis(per, most[..., None], 2)[..., 0])
    mean_h = np.clip(np.round(mean_h / step), 1, mask) * step
    far = np.where(count > 0, np.minimum(1.0, count * math.pi / 4.0 * (share_of[most] * mean_h) ** 2 / (SQUARE_M * SQUARE_M)), 0.0)
    del blocks, species, height, area, per, metres

    # The ground's slope, a square's mean of its cells' own, and its mean height.
    gy, gx = np.gradient(heights.astype(np.float32), np.float32(cell))
    slope = np.degrees(np.arctan(np.hypot(gx, gy)))
    del gx, gy
    slope_sq = slope[: m * k, : m * k].reshape(m, k, m, k).mean(axis=(1, 3))
    up_sq = heights[: m * k, : m * k].reshape(m, k, m, k).mean(axis=(1, 3))
    del slope

    print(f"crowns over {args.world}: {m} x {m} squares of {SQUARE_M:.0f} m; no trunk in {100.0 * (count == 0).mean():.1f}%")
    forested = own > 0.05
    print(f"  share covered, tree by tree: median {np.median(own):.3f}, mean {own.mean():.3f}")
    print(f"  share covered, the far layers' way: median {np.median(far):.3f}, mean {far.mean():.3f}; "
          f"the far way over tree by tree, median of the squares over 0.05: {np.median(far[forested] / own[forested]):.3f}")
    print("  by the ground's slope (tree by tree, the far way, squares with no trunk, of all squares):")
    for lo, hi in SLOPE_BANDS:
        sel = (slope_sq >= lo) & (slope_sq < hi)
        if sel.any():
            print(f"    {lo:2d} to {hi:2d} degrees: {own[sel].mean():.3f}, {far[sel].mean():.3f}, "
                  f"{100.0 * (count[sel] == 0).mean():5.1f}% bare, {100.0 * sel.mean():5.1f}% of squares")
    print("  by the square's commonest tree (squares, median trees, median mean height, tree by tree, the far way):")
    for code in tall:
        sel = (most == code) & (count > 0)
        if sel.any():
            print(f"    {names[code]}: {100.0 * sel.mean():.1f}%, {np.median(count[sel]):.0f}, {np.median(mean_h[sel]):.1f} m, "
                  f"{np.median(own[sel]):.3f}, {np.median(far[sel]):.3f}")

    half = extent / 2.0
    for look in args.look:
        east, north, bearing = (float(v) for v in look.split(","))
        print(f"  a 60 degree view from east {east:.0f} north {north:.0f} toward {bearing:.0f} degrees:")
        for d0, d1 in DISTANCE_BANDS:
            own_v, far_v, slopes, ups = [], [], [], []
            for d in range(d0, d1, int(SQUARE_M)):
                for a in np.linspace(-30.0, 30.0, 31):
                    b = math.radians(bearing + a)
                    e, n = east + d * math.sin(b), north + d * math.cos(b)
                    if abs(e) >= half or abs(n) >= half:
                        continue
                    row, col = min(m - 1, int((half - n) / SQUARE_M)), min(m - 1, int((e + half) / SQUARE_M))
                    own_v.append(own[row, col]); far_v.append(far[row, col]); slopes.append(slope_sq[row, col]); ups.append(up_sq[row, col])
            if own_v:
                sl = np.array(slopes)
                print(f"    {d0 / 1000:4.1f} to {d1 / 1000:4.1f} km: tree by tree {np.mean(own_v):.3f}, the far way {np.mean(far_v):.3f}, "
                      f"slope {sl.mean():4.1f} degrees ({100.0 * (sl > 30).mean():4.1f}% over 30), {np.mean(ups):4.0f} m up, {len(own_v)} squares")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
