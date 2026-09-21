#!/usr/bin/env python3
"""drink.py: the thirst and the drink (FP.1), recorded by the built player on a world folder, windowless and muted.

Finds on the world's own layers a dry cell (no water standing, ground no steeper than 6 degrees) beside fresh water at
least 5 cm deep (a creek, a stream or a lake, classes 3 to 5 of the water layer) and within 150 m of the sea (class 7),
the nearest such to the world's wake; stands the founder there by the host (its console's `stand` and `save`); and runs
the player's `drink` scenario as a development game: the clock sped sixty times until the founder is very thirsty, the
walk to the fresh water and the drink, the walk to the sea and its refusal, three frames and the run's records
(`thirst`, `drink`, `end`). Then prints what the end record says. Leaves the frames, run.jsonl, the tile cache and the
logs under Artefacts/frames/drink-<stamp>/; `Tools/verifiers/checks/thirst_check.py <that folder>` holds the records to
the physiology they claim.

Claude's tool: it runs the player and reports; the frames are the owner's to judge, the numbers the check's.

Usage, from the repository root:
    python Tools/world/drink.py [--build] [--player Build/Harness/EarthGame2.exe] [--world Artefacts/worlds/gate]

Exit 0 when the player exits 0 (very thirsty in time, the fresh water drunk, the sea's answer salt, every frame written
and nothing logged as an error); 1 otherwise.
"""
import argparse
import json
import math
import os
from pathlib import Path
import subprocess
import sys
from datetime import datetime, timezone

import numpy as np

sys.path.insert(0, str(Path(__file__).resolve().parent))
from wade import EDITOR_VERSION, HOSTS, UNITY, copied, layer, near, number, run  # noqa: E402  (one owner of the layer reading)

ROOT = Path(__file__).resolve().parents[2]
PLAYER = ROOT / "Build/Harness/EarthGame2.exe"
FRESH = (3, 4, 5)          # the water layer's classes for a creek, a stream and a lake, as its legend states them
SEA = 7                    # the sea's
MIN_DEPTH_M = 0.02          # the engine's WorldState.StandingWaterM restated: shallower than this, water stands nowhere
SEA_WITHIN_M = 150.0
STAND_SLOPE_DEG = 6.0


class Stand:
    """Where the founder stands: dry, gentle, a cell from fresh water, within reach of the sea, nearest the wake."""

    def __init__(self, world):
        side, z = layer(world, "heights")
        _, surface = layer(world, "surface")
        _, water = layer(world, "water")
        cell, half = float(side["cell_m"]), float(side["extent_m"]) / 2.0
        depth = surface - z
        gy, gx = np.gradient(z, cell)
        slope = np.degrees(np.arctan(np.hypot(gx, gy)))
        fresh = np.isin(water, FRESH) & (depth >= MIN_DEPTH_M)
        sea = (water == SEA) & (depth >= MIN_DEPTH_M)
        dry = (depth <= 0.0) & (slope <= STAND_SLOPE_DEG)
        candidates = dry & near(fresh, 1, square=True) & near(sea, int(math.ceil(SEA_WITHIN_M / cell)))
        self.found = bool(candidates.any())
        self.place = None
        if not self.found:
            return
        saved = json.load(open(world / "world.json", encoding="utf-8"))
        wake = (number(saved.get("wake_east")), number(saved.get("wake_north")))
        rows, cols = np.nonzero(candidates)
        east, north = cols * cell - half, half - rows * cell
        k = int(np.argmin(np.hypot(east - wake[0], north - wake[1])))
        self.place = (float(east[k]), float(north[k]))
        self.wake = wake
        self.fresh_cells, self.sea_cells = int(fresh.sum()), int(sea.sum())


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--build", action="store_true")
    parser.add_argument("--player", default=str(PLAYER))
    parser.add_argument("--world", default="Artefacts/worlds/gate")
    # The scenario pins the hour before its first frame (M1.4g), so two runs light the same water the same way;
    # another hour here is how the water after dark is recorded.
    parser.add_argument("--hour", type=float, default=None, help="the local hour the frames are taken at (default mid-morning)")
    args = parser.parse_args()
    player = (ROOT / args.player).resolve()
    world = (ROOT / args.world).resolve()
    directory = ROOT / "Artefacts/frames" / ("drink-" + datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%SZ"))
    directory.mkdir(parents=True)

    if args.build:
        # The build lands through the one install tool (M1.Bb): staged, versioned, never over a running player.
        code = run([sys.executable, ROOT / "Tools/build/install.py", "--into", "harness", "--label", "drink"], 1200)
        if code != 0:
            raise RuntimeError("install exit %d" % code)
    if not player.is_file():
        raise RuntimeError("no player at %s; run with --build" % player)
    if not (world / "world.json").is_file():
        raise RuntimeError("no world at %s; run Tools/world/create.py first" % world)
    world = copied(world, directory)
    host = next((h for h in HOSTS if h.is_file()), None)
    if host is None:
        raise RuntimeError("no server host; build it: dotnet build Engine/tools/EarthGame.ServerHost -c Release")

    stand = Stand(world)
    if not stand.found:
        raise RuntimeError("no dry, gentle cell beside fresh water and within %.0f m of the sea on this world" % SEA_WITHIN_M)
    east, north = stand.place
    print("the wake at east %.0f north %.0f; the founder stands at east %.0f north %.0f (%d fresh cells, %d sea cells on the world)"
          % (stand.wake[0], stand.wake[1], east, north, stand.fresh_cells, stand.sea_cells))
    stood = subprocess.run(["dotnet", str(host), "+server.world", str(world), "+server.port", "28319", "+server.local", "1"],
                           input="stand William %d %d\nsave\nstop\n" % (round(east), round(north)),
                           capture_output=True, text=True, cwd=ROOT, timeout=600)
    (directory / "host.log").write_text(stood.stdout + stood.stderr, encoding="utf-8")
    if stood.returncode != 0:
        raise RuntimeError("the host exited %d standing the founder; see %s" % (stood.returncode, directory / "host.log"))

    # -batchmode without -nographics: the recorder renders its frames on the GPU into textures, with no window.
    # -eg-dev: a development game, whose server takes the scenario's clock setting.
    code = run([player, "-batchmode", "-logFile", directory / "player.log",
                "-eg-mode", "solo", "-eg-dev", "-eg-name", "William", "-eg-world", world.name, "-eg-saves", world.parent,
                "-eg-tiles", directory / "tiles", "-eg-record", directory, "-eg-scenario", "drink"]
               + ([] if args.hour is None else ["-eg-hour", "%g" % args.hour]), 900)

    log = directory / "run.jsonl"
    records = [json.loads(line) for line in log.read_text(encoding="utf-8").splitlines()] if log.is_file() else []
    end = next((r for r in reversed(records) if r.get("kind") == "end"), {})
    frames = sorted(p.name for p in (directory / "frames").glob("*.png")) if (directory / "frames").is_dir() else []
    print("player exit %d: %d frame(s): %s" % (code, len(frames), ", ".join(frames)))
    print("very thirsty %s after %.1f s at %s times the game's rate; drank %s (water %.4f -> %.4f); the sea refused with salt %s"
          % (end.get("very_thirsty"), number(end.get("thirst_seconds")), end.get("clock_scale"), end.get("drank"),
             number(end.get("water_before")), number(end.get("water_after")), end.get("salt_refused")))
    print("%s error(s), %s correction(s); %s footfall(s) on %s" % (end.get("errors"), end.get("corrections"), end.get("footfalls"), end.get("underfoot")))
    for r in records:
        if r.get("kind") in ("error", "exception"):
            print("  %s: %s" % (r["kind"], r.get("message", "")[:300]))
        elif r.get("kind") == "drink":
            print("  drink at the %s: %s, water %.4f -> %.4f, looked at %s" % (r.get("water_kind"), r.get("outcome"),
                  number(r.get("water_before")), number(r.get("water_after")), r.get("looked_at")))
    print(directory)
    return 0 if code == 0 else 1


if __name__ == "__main__":
    try:
        sys.exit(main())
    except (OSError, RuntimeError, subprocess.TimeoutExpired, ValueError) as error:
        print("drink recording FAILED: %s" % error, file=sys.stderr)
        sys.exit(1)
