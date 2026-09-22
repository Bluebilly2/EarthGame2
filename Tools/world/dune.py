#!/usr/bin/env python3
"""dune.py: the dune (M1.5h, CANON ruling 34), walked down and up by the built player on a copy of a world, windowless and muted.

Runs the player's `dune` scenario: the steepest face of 18 to 34 degrees within 150 m of where the founder stands is found
on the client's own ground, the founder is walked by script to a point above it, then down its fall line for four seconds
and back up, and the slope walked and the speed held past the first second are written beside each other. Then prints
what the end record says, against the flat walk. Leaves the frames ("dune-top", "dune-bottom"), run.jsonl and the logs
under Artefacts/frames/dune-<stamp>/.

The world named is read and never written: the run takes its copy through wade.copied (WORKING.md).

Claude's tool: it runs the player and reports; the feel of the descent is the owner's to judge, hands on (ruling 12).

With `--at-face` (M1.5i's owed proof) the nearest face over the walkable limit to the world's wake is found from the world's
own heights, the founder is stood six metres below its cell by the host, and the scenario runs its slide part alone
(`-eg-dune slide`): the face walked onto from below with the feet read against the ground every frame.

Usage, from the repository root:
    python Tools/world/dune.py [--player Build/Harness/EarthGame2.exe] [--world Artefacts/worlds/gate] [--at-face]

Exit 0 when the player exits 0 (a face found and walked, the descent faster than half a metre a second and slower than
the flat walk, the ascent slower than the descent); 1 otherwise.
"""
import argparse
import array
import json
import math
from pathlib import Path
import subprocess
import sys
from datetime import datetime, timezone

sys.path.insert(0, str(Path(__file__).resolve().parent))
from wade import copied, number, run  # noqa: E402  (one owner of running the player, reading its numbers and copying its world)
from vantages import HOSTS  # noqa: E402  (one owner of where the server host is built)

ROOT = Path(__file__).resolve().parents[2]
PLAYER = ROOT / "Build/Harness/EarthGame2.exe"


def main():
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--player", default=str(PLAYER))
    parser.add_argument("--world", default="Artefacts/worlds/gate")
    parser.add_argument("--at-face", action="store_true", help="stand the founder below the nearest face over the limit and run the slide part alone")
    args = parser.parse_args()
    player = (ROOT / args.player).resolve()
    source = (ROOT / args.world).resolve()
    directory = ROOT / "Artefacts/frames" / ("dune-" + datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%SZ"))
    directory.mkdir(parents=True)
    if not player.is_file():
        raise RuntimeError("no player at %s; build it with Tools/build/install.py --into harness" % player)
    if not (source / "world.json").is_file():
        raise RuntimeError("no world at %s; run Tools/world/create.py first" % source)
    world = copied(source, directory)
    scenario = ["-eg-scenario", "dune"]
    if args.at_face:
        east, north, deg, from_m = face_over_limit(world)
        print("the nearest face over the limit: %.1f degrees, %.0f m from the wake; the founder stood at east %d north %d" % (deg, from_m, east, north))
        host = next((h for h in HOSTS if h.is_file()), None)
        if host is None:
            raise RuntimeError("no server host; build it: dotnet build Engine/tools/EarthGame.ServerHost -c Release")
        stood = subprocess.run(["dotnet", str(host), "+server.world", str(world), "+server.port", "28318", "+server.local", "1"],
                               input="stand William %d %d\nsave\nstop\n" % (east, north), capture_output=True, text=True, cwd=ROOT, timeout=600)
        (directory / "host.log").write_text(stood.stdout + stood.stderr, encoding="utf-8")
        if stood.returncode != 0:
            raise RuntimeError("the host exited %d standing the founder; see %s" % (stood.returncode, directory / "host.log"))
        scenario += ["-eg-dune", "slide"]

    # -batchmode without -nographics: the recorder renders its frames on the GPU into textures, with no window.
    code = run([player, "-batchmode", "-logFile", directory / "player.log",
                "-eg-mode", "solo", "-eg-name", "William", "-eg-world", world.name, "-eg-saves", world.parent,
                "-eg-tiles", directory / "tiles", "-eg-record", directory] + scenario, 600)

    log = directory / "run.jsonl"
    records = [json.loads(line) for line in log.read_text(encoding="utf-8").splitlines()] if log.is_file() else []
    end = next((r for r in reversed(records) if r.get("kind") == "end"), {})
    frames = sorted(p.name for p in (directory / "frames").glob("*.png")) if (directory / "frames").is_dir() else []
    print("player exit %d: %d frame(s): %s" % (code, len(frames), ", ".join(frames)))
    print("a face of %.1f degrees found: %s" % (number(end.get("face_deg")), end.get("found")))
    print("down it: %.1f degrees walked at %.2f m/s; up it: %.1f degrees at %.2f m/s; the flat walk %.2f m/s"
          % (number(end.get("slope_down_deg")), number(end.get("speed_down_ms")), number(end.get("slope_up_deg")),
             number(end.get("speed_up_ms")), number(end.get("flat_ms"))))
    print("walked down (over half a metre a second, under the flat) %s; slower up %s" % (end.get("walked_down"), end.get("slower_up")))
    slide = next((r for r in reversed(records) if r.get("kind") == "slide"), {})
    if slide:
        print("the slide: a face over the limit found %s (%.1f degrees, %.0f m off); walked onto from below: the steepest ground under the feet %.1f degrees, the lowest they were read under it %.3f m, kept their feet %s, stopped %.1f m short of the foot"
              % (slide.get("found"), number(slide.get("face_deg")), number(slide.get("from_m")), number(slide.get("steepest_under_deg")), number(slide.get("lowest_under_m")), slide.get("kept_feet"), number(slide.get("short_of_foot_m"))))
    for r in records:
        if r.get("kind") in ("error", "exception"):
            print("  %s: %s" % (r["kind"], r.get("message", "")[:300]))
    print(directory)
    return 0 if code == 0 else 1


def face_over_limit(world, least_deg=40.0, most_deg=55.0):
    """The nearest cell to the world's wake whose ground, by the world's own heights at the raster's pitch, slopes between the
    degrees given, and the point six metres down its fall line: east, north, its degrees and its distance from the wake.
    The scenario measures the slope on the client's ground round the founder's own feet, off the raster's posts, where a
    face is read a few degrees gentler than at the posts; a face of 35.7 degrees here read under the 35.05 limit there
    (2026-09-22), so the face asked for is well over the limit and well short of a cliff."""
    meta = json.loads((world / "layers" / "heights.json").read_text(encoding="utf-8"))
    w, h, cell, ext = meta["width"], meta["height"], meta["cell_m"], meta["extent_m"]
    heights = array.array("f")
    heights.frombytes((world / "layers" / "heights.r32").read_bytes())
    about = json.loads((world / "world.json").read_text(encoding="utf-8"))
    wake_e, wake_n = float(about["wake_east"]), float(about["wake_north"])
    best = None
    for row in range(1, h - 1):
        north = ext / 2 - row * cell
        for col in range(1, w - 1):
            east = col * cell - ext / 2
            d2 = (east - wake_e) ** 2 + (north - wake_n) ** 2
            if best is not None and d2 >= best[0]:
                continue
            i = row * w + col
            de = (heights[i + 1] - heights[i - 1]) / (2 * cell)
            dn = (heights[i - w] - heights[i + w]) / (2 * cell)
            slope = math.hypot(de, dn)
            deg = math.degrees(math.atan(slope))
            if least_deg <= deg <= most_deg:
                best = (d2, east, north, deg, de / slope, dn / slope)
    if best is None:
        raise RuntimeError("no face of %.1f to %.1f degrees in the world's heights" % (least_deg, most_deg))
    d2, east, north, deg, ue, un = best
    # Down the fall line: the gradient points uphill, so six metres the other way.
    return int(round(east - ue * 6.0)), int(round(north - un * 6.0)), deg, math.sqrt(d2)


if __name__ == "__main__":
    sys.exit(main())
