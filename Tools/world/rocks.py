#!/usr/bin/env python3
"""rocks.py: the rocks that stand (BF.4 stage three), walked into and onto by the built player on copies of a world, windowless
and muted.

The dedicated host lists the rocks the server's own rule stands near the world's wake (`rocks near`), and two are chosen from
its list: a boulder too tall to step onto, and a low one with a top to stand on. The first is walked at: a point six metres off
is found where the way to it is gentle, dry, clear of trees and of every other rock, the host stands the founder there, and the
player's `rocks` scenario walks straight at the rock; stopped or glancing off its side, the end record says how near the body's
middle came to its outline (never further in than the physics' fit to the rock allows). On the
second the host stands the founder itself, where the server's surface is the rock's top, and the end record says how far the
feet are from its surface and how many times the server corrected them. Leaves each walk's frames ("rock-ahead", "rock" or
"rock-on"), run.jsonl and the logs under Artefacts/frames/rocks-<stamp>/stop and /on.

Each walk takes its own copy of the world through wade.copied, so the world named is read and never written (WORKING.md).

Claude's tool: it runs the host and the player and reports; the look of the rocks is the owner's to judge (ruling 12).

Usage, from the repository root:
    python Tools/world/rocks.py [--player Build/Harness/EarthGame2.exe] [--world Artefacts/worlds/gate]

Exit 0 when both walks' players exit 0 (the body met the tall rock and never went into it, the founder stood on the low one's
top uncorrected); 1 otherwise.
"""
import argparse
import json
import math
import re
import subprocess
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from wade import between, copied, layer, machine, number, run, run_folder  # noqa: E402  (one owner of running the player, reading layers and copying worlds)
from vantages import HOSTS  # noqa: E402  (one owner of where the server host is built)

ROOT = Path(__file__).resolve().parents[2]
PLAYER = ROOT / "Build/Harness/EarthGame2.exe"
LINE = re.compile(r"rock (-?\d+\.\d+) (-?\d+\.\d+) (\w+) (\w+) above (-?\d+\.\d+) half (\d+\.\d+) (\d+\.\d+) (\d+\.\d+) yaw (\d+) (\w+) away (\d+\.\d+)")
# The covers a walk may not cross: the sea and fresh water (GroundCover 1 and 2, the low six bits of the cover byte).
WET = {1, 2}
OFF_M = 6.0


def host():
    found = next((h for h in HOSTS if h.is_file()), None)
    if found is None:
        raise RuntimeError("no server host; build it: dotnet build Engine/tools/EarthGame.ServerHost -c Release")
    return found


def ask_host(world, commands, directory, name):
    done = subprocess.run(["dotnet", str(host()), "+server.world", str(world), "+server.port", str(machine.port(28295)), "+server.local", "1"],
                          input=commands, capture_output=True, text=True, encoding="utf-8", errors="replace", cwd=ROOT, timeout=900)
    (directory / (name + ".log")).write_text(done.stdout + done.stderr, encoding="utf-8")
    if done.returncode != 0:
        raise RuntimeError("the host exited %d; see %s" % (done.returncode, directory / (name + ".log")))
    return done.stdout


def rocks_near(world, east, north, metres, directory):
    out = ask_host(world, "rocks near %.1f %.1f %.1f\nstop\n" % (east, north, metres), directory, "rocks-near")
    rocks = []
    for m in LINE.finditer(out):
        rocks.append({"east": float(m.group(1)), "north": float(m.group(2)), "form": m.group(3), "stone": m.group(4),
                      "above": float(m.group(5)), "half": (float(m.group(6)), float(m.group(7)), float(m.group(8))),
                      "yaw": int(m.group(9)), "place": m.group(10), "away": float(m.group(11))})
    return rocks


def way_in(world, rock, others):
    """A point OFF_M metres from a rock whose straight way to it is gentle, dry, clear of trunks and of every other rock; None if
    no one of sixteen ways is."""
    hs, z = layer(world, "heights")
    _, cover = layer(world, "cover")
    _, stand = layer(world, "stand")
    cell, half = float(hs["cell_m"]), float(hs["extent_m"]) / 2.0
    for k in range(16):
        a = 2.0 * math.pi * k / 16.0
        ue, un = math.sin(a), math.cos(a)
        ok = True
        prev = between(z, half, cell, rock["east"], rock["north"])
        for step in range(1, int(OFF_M * 2) + 1):
            d = step * 0.5
            e, n = rock["east"] + ue * d, rock["north"] + un * d
            h = between(z, half, cell, e, n)
            if abs(h - prev) > 0.25:  # steeper than about 27 degrees over half a metre
                ok = False
                break
            prev = h
            col, row = int(round((e + half) / cell)), int(round((half - n) / cell))
            # The strip the body walks through, a little wider than its shoulders: no cell a sample of it falls in holds a
            # trunk. (Every cell round the way, as first tried, ruled out every way in on a platform with forest behind it.)
            for side in (-0.8, 0.0, 0.8):
                sc = int(round((e + un * side + half) / cell))
                sr = int(round((half - (n - ue * side)) / cell))
                if stand[sr, sc] != 0:
                    ok = False
            if int(cover[row, col]) & 0x3F in WET:
                ok = False
            for o in others:
                if o is rock:
                    continue
                if math.hypot(o["east"] - e, o["north"] - n) < max(o["half"][0], o["half"][1]) + 1.2:
                    ok = False
            if not ok:
                break
        if ok:
            return rock["east"] + ue * OFF_M, rock["north"] + un * OFF_M
    return None


def walk(player, source, directory, rock, start, how):
    directory.mkdir(parents=True)
    world = copied(source, directory)
    ask_host(world, "stand William %.2f %.2f\nsave\nstop\n" % start, directory, "stand")
    print("%s: the %s %s at %.2f %.2f, %.2f m above the ground, half-axes %s, %.0f m from the wake; the founder stood at %.1f %.1f"
          % (how, rock["stone"], rock["form"].lower(), rock["east"], rock["north"], rock["above"], rock["half"], rock["away"], start[0], start[1]))
    code = run([player, "-batchmode", "-logFile", directory / "player.log",
                "-eg-mode", "solo", "-eg-name", "William", "-eg-world", world.name, "-eg-saves", world.parent,
                "-eg-tiles", directory / "tiles", "-eg-record", directory, "-eg-scenario", "rocks",
                "-eg-rock", "%.2f,%.2f" % (rock["east"], rock["north"]), "-eg-rock-walk", how], 600)
    log = directory / "run.jsonl"
    records = [json.loads(line) for line in log.read_text(encoding="utf-8").splitlines()] if log.is_file() else []
    end = next((r for r in reversed(records) if r.get("kind") == "end"), {})
    for r in records:
        if r.get("kind") in ("error", "exception"):
            print("  %s: %s" % (r["kind"], r.get("message", "")[:300]))
    print("  never inside: %s; the body's middle came %.2f m from the rock's outline at the nearest (its radius %.2f); the rock's top "
          "reached %.3f m into the round foot at the most; corrections %s"
          % (end.get("never_inside"), number(end.get("nearest_outline_m")), number(end.get("body_radius_m")),
             number(end.get("deepest_overlap_m")), end.get("corrections")))
    if how == "stop":
        print("  met it: %s; ok %s" % (end.get("met"), end.get("ok")))
    else:
        print("  on its top: %s; the feet at %.3f m, its surface there %.3f m, the ground %.3f m"
              % (end.get("ok"), number(end.get("up")), number(end.get("top_under_feet_m")), number(end.get("ground_m"))))
    print("  player exit %d; %s" % (code, directory))
    return code


def main():
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--player", default=str(PLAYER))
    parser.add_argument("--world", default="Artefacts/worlds/gate")
    args = parser.parse_args()
    player = (ROOT / args.player).resolve()
    source = (ROOT / args.world).resolve()
    if not player.is_file():
        raise RuntimeError("no player at %s; build it with Tools/build/install.py --into harness" % player)
    about = json.loads((source / "world.json").read_text(encoding="utf-8"))
    directory = run_folder("rocks")
    directory.mkdir(parents=True)
    listing = copied(source, directory)
    rocks = rocks_near(listing, float(about["wake_east"]), float(about["wake_north"]), 1500.0, directory)
    print("%d rocks listed near the wake" % len(rocks))
    boulders = [r for r in rocks if r["form"] == "Boulder"]
    # Taller than the mover steps up (0.4 m) with room to spare, on any ground a way in can be found across; the first count
    # on Bherwerre (2026-09-25) found no boulder a metre high on flat ground within a kilometre and a half of the wake.
    tall = [r for r in boulders if r["above"] >= 0.55]
    low = [r for r in boulders if r["place"] in ("FlatRock", "ThinSoil") and 0.12 <= r["above"] <= 0.32
           and r["half"][0] >= 0.35 and r["half"][1] >= 0.3]
    results = []
    for how, choices in (("stop", tall), ("on", low)):
        # Walked at, a rock is met from a clear way in; stood on, the founder is put on its middle, where the server's surface is its top.
        pick = next(((r, s) for r in choices for s in [way_in(listing, r, rocks) if how == "stop" else (r["east"], r["north"])] if s is not None), None)
        if pick is None:
            print("%s: no rock of that kind with a clear way in among the %d listed" % (how, len(choices)))
            results.append(False)
            continue
        rock, start = pick
        results.append(walk(player, source, directory / how, rock, start, how) == 0)
    print(directory)
    return 0 if all(results) else 1


if __name__ == "__main__":
    sys.exit(main())
