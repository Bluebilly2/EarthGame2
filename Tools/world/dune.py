#!/usr/bin/env python3
"""dune.py: the dune (M1.5h, CANON ruling 34), walked down and up by the built player on a copy of a world, windowless and muted.

Runs the player's `dune` scenario: the steepest face of 18 to 34 degrees within 150 m of where the founder stands is found
on the client's own ground, the founder is walked by script to a point above it, then down its fall line for four seconds
and back up, and the slope walked and the speed held past the first second are written beside each other. Then prints
what the end record says, against the flat walk. Leaves the frames ("dune-top", "dune-bottom"), run.jsonl and the logs
under Artefacts/frames/dune-<stamp>/.

The world named is read and never written: the run takes its copy through wade.copied (WORKING.md).

Claude's tool: it runs the player and reports; the feel of the descent is the owner's to judge, hands on (ruling 12).

Usage, from the repository root:
    python Tools/world/dune.py [--player Build/Harness/EarthGame2.exe] [--world Artefacts/worlds/gate]

Exit 0 when the player exits 0 (a face found and walked, the descent faster than half a metre a second and slower than
the flat walk, the ascent slower than the descent); 1 otherwise.
"""
import argparse
import json
from pathlib import Path
import sys
from datetime import datetime, timezone

sys.path.insert(0, str(Path(__file__).resolve().parent))
from wade import copied, number, run  # noqa: E402  (one owner of running the player, reading its numbers and copying its world)

ROOT = Path(__file__).resolve().parents[2]
PLAYER = ROOT / "Build/Harness/EarthGame2.exe"


def main():
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--player", default=str(PLAYER))
    parser.add_argument("--world", default="Artefacts/worlds/gate")
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

    # -batchmode without -nographics: the recorder renders its frames on the GPU into textures, with no window.
    code = run([player, "-batchmode", "-logFile", directory / "player.log",
                "-eg-mode", "solo", "-eg-name", "William", "-eg-world", world.name, "-eg-saves", world.parent,
                "-eg-tiles", directory / "tiles", "-eg-record", directory, "-eg-scenario", "dune"], 600)

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
    for r in records:
        if r.get("kind") in ("error", "exception"):
            print("  %s: %s" % (r["kind"], r.get("message", "")[:300]))
    print(directory)
    return 0 if code == 0 else 1


if __name__ == "__main__":
    sys.exit(main())
