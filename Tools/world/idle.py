#!/usr/bin/env python3
"""idle.py: the idle pause (M1.E, CANON ruling 38), recorded by the built player on a copy of a world, windowless and muted.

Runs the player's `idle` scenario, the one recorded run allowed to sleep: the game played for a moment, then left
untouched until it sleeps (the watch's minute), held asleep, and woken by a key on a keyboard of the scenario's own;
the server's own clock is read from its pongs before the sleep and after it. Then prints what the end record says.
Leaves the frame ("paused": the line on the screen), run.jsonl and the logs under Artefacts/frames/idle-<stamp>/.

A world's clock moves while it is played, so the run takes a copy of the world under its own folder and never writes
the one named (WORKING.md: a scenario that leaves things behind runs on a copy of its world).

Claude's tool: it runs the player and reports; the frame is the owner's to judge.

Usage, from the repository root:
    python Tools/world/idle.py [--player Build/Harness/EarthGame2.exe] [--world Artefacts/worlds/gate]

Exit 0 when the player exits 0 (asleep within its minute, woken by the key, the world's clock moved while asleep by less
than a quarter of what it would have awake, the sleeping game drew frames at no more than half again its cap, and the
founder did not rise after the waking key); 1 otherwise.
"""
import argparse
import json
from pathlib import Path
import sys
from datetime import datetime, timezone

sys.path.insert(0, str(Path(__file__).resolve().parent))
from wade import copied, number, run  # noqa: E402  (one owner of running the player and reading its numbers)

ROOT = Path(__file__).resolve().parents[2]
PLAYER = ROOT / "Build/Harness/EarthGame2.exe"


def main():
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--player", default=str(PLAYER))
    parser.add_argument("--world", default="Artefacts/worlds/gate")
    args = parser.parse_args()
    player = (ROOT / args.player).resolve()
    source = (ROOT / args.world).resolve()
    directory = ROOT / "Artefacts/frames" / ("idle-" + datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%SZ"))
    directory.mkdir(parents=True)
    if not player.is_file():
        raise RuntimeError("no player at %s; build it with Tools/build/install.py --into harness" % player)
    if not (source / "world.json").is_file():
        raise RuntimeError("no world at %s; run Tools/world/create.py first" % source)
    world = copied(source, directory)

    # -batchmode without -nographics: the recorder renders its frame on the GPU into a texture, with no window.
    code = run([player, "-batchmode", "-logFile", directory / "player.log",
                "-eg-mode", "solo", "-eg-name", "William", "-eg-world", world.name, "-eg-saves", world.parent,
                "-eg-tiles", directory / "tiles", "-eg-record", directory, "-eg-scenario", "idle"], 600)

    log = directory / "run.jsonl"
    records = [json.loads(line) for line in log.read_text(encoding="utf-8").splitlines()] if log.is_file() else []
    end = next((r for r in reversed(records) if r.get("kind") == "end"), {})
    frames = sorted(p.name for p in (directory / "frames").glob("*.png")) if (directory / "frames").is_dir() else []
    print("player exit %d: %d frame(s): %s" % (code, len(frames), ", ".join(frames)))
    print("asleep %s after %.1f s left alone; woken by the key %s" % (end.get("slept"), number(end.get("slept_after_s")), end.get("woke")))
    print("the world's clock across the sleep moved %.4f h where awake it would have moved %.4f h (%.1f real seconds); stood %s"
          % (number(end.get("clock_moved_hours")), number(end.get("clock_would_have_moved_hours")),
             number(end.get("real_seconds_across")), end.get("clock_stood")))
    print("asleep, the game drew %.1f frames a second; held to its cap %s" % (number(end.get("frames_asleep_per_s")), end.get("frames_held")))
    print("after the waking key the founder rose %.3f m; stayed put %s" % (number(end.get("after_wake_rise_m")), end.get("stayed_put")))
    for r in records:
        if r.get("kind") in ("error", "exception"):
            print("  %s: %s" % (r["kind"], r.get("message", "")[:300]))
    print(directory)
    return 0 if code == 0 else 1


if __name__ == "__main__":
    sys.exit(main())
