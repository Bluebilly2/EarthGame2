#!/usr/bin/env python3
"""looks.py: the animals' looks (M1.7b), recorded by the built player on a world folder, windowless and muted.

Runs the player's `looks` scenario as a development game on the world named: the clock is put to ten in the morning, a
kangaroo and an oystercatcher are set down two metres ahead by the panel's deeds, each is looked at from near and far, the
oystercatcher is put to flight and the kangaroo to its hop, and six pairs of frames are written (`roo-near`, `roo-far`,
`bird-near`, `bird-far`, `bird-flying`, `roo-fleeing`) with a `looks` record per frame beside the run's end record. Leaves
the frames, run.jsonl, the tile cache and the logs under Artefacts/frames/looks-<stamp>/.

Claude's tool: it runs the player and reports; whether a kangaroo reads as a kangaroo is the owner's to judge.

Usage, from the repository root:
    python Tools/world/looks.py [--build] [--player Build/Harness/EarthGame2.exe] [--world Artefacts/worlds/gate]

Exit 0 when the player exits 0 (both animals arrived, every frame was written, the fleeing pose took, and nothing was
logged as an error); 1 otherwise.
"""
import argparse
import json
from pathlib import Path
import subprocess
import sys
from datetime import datetime, timezone

sys.path.insert(0, str(Path(__file__).resolve().parent))
from wade import HOSTS, UNITY, run  # noqa: E402  (one owner of the runner)

ROOT = Path(__file__).resolve().parents[2]
PLAYER = ROOT / "Build/Harness/EarthGame2.exe"


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--build", action="store_true")
    parser.add_argument("--player", default=str(PLAYER))
    parser.add_argument("--world", default="Artefacts/worlds/gate")
    args = parser.parse_args()
    player = (ROOT / args.player).resolve()
    world = (ROOT / args.world).resolve()
    directory = ROOT / "Artefacts/frames" / ("looks-" + datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%SZ"))
    directory.mkdir(parents=True)

    if args.build:
        # The build lands through the one install tool (M1.Bb): staged, versioned, never over a running player.
        code = run([sys.executable, ROOT / "Tools/build/install.py", "--into", "harness", "--label", "looks"], 1200)
        if code != 0:
            raise RuntimeError("install exit %d" % code)
    if not player.is_file():
        raise RuntimeError("no player at %s; run with --build" % player)
    if not (world / "world.json").is_file():
        raise RuntimeError("no world at %s; run Tools/world/create.py first" % world)
    if next((h for h in HOSTS if h.is_file()), None) is None:
        raise RuntimeError("no server host; build it: dotnet build Engine/tools/EarthGame.ServerHost -c Release")

    # -batchmode without -nographics: the recorder renders its frames on the GPU into textures, with no window.
    # -eg-dev: a development game, whose server takes the scenario's deeds (the clock, the animals set down, their pose).
    code = run([player, "-batchmode", "-logFile", directory / "player.log",
                "-eg-mode", "solo", "-eg-dev", "-eg-name", "William", "-eg-world", world.name, "-eg-saves", world.parent,
                "-eg-tiles", directory / "tiles", "-eg-record", directory, "-eg-scenario", "looks"], 900)

    log = directory / "run.jsonl"
    records = [json.loads(line) for line in log.read_text(encoding="utf-8").splitlines()] if log.is_file() else []
    end = next((r for r in reversed(records) if r.get("kind") == "end"), {})
    frames = sorted(p.name for p in (directory / "frames").glob("*.png")) if (directory / "frames").is_dir() else []
    print("player exit %d: %d frame(s): %s" % (code, len(frames), ", ".join(frames)))
    print("end: %s" % json.dumps({k: v for k, v in end.items() if k not in ("kind",)}, ensure_ascii=False)[:600])
    for r in records:
        if r.get("kind") in ("error", "exception"):
            print("  %s: %s" % (r["kind"], r.get("message", "")[:300]))
        elif r.get("kind") == "looks":
            print("  looks: %s" % json.dumps({k: v for k, v in r.items() if k not in ("kind", "t", "tick")}, ensure_ascii=False)[:200])
    print(directory)
    return 0 if code == 0 else 1


if __name__ == "__main__":
    try:
        sys.exit(main())
    except (OSError, RuntimeError, subprocess.TimeoutExpired, ValueError) as error:
        print("looks recording FAILED: %s" % error, file=sys.stderr)
        sys.exit(1)
