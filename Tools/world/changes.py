#!/usr/bin/env python3
"""changes.py: the world changes (BF.3 promise 7), recorded by the built player on a world folder, windowless and muted.

Runs the player's `changes` scenario as a development game on a copy of the world named: at the wake with a full body the
founder walks to the nearest trunk whose bark strips and strips it; takes up a keen flake the panel sets down, walks to the
nearest sedge clump and cuts its fibre, takes up two strips and lays them into cord; pulls a tussock with empty hands and
clears the cell under the crosshair; points a stick of the litter with the flake, takes it up and digs the cleared cell;
takes up a chopper the panel sets down and cuts the nearest small tree, through when the offer is under ten minutes and
for a minute otherwise, the cut kept. Every work is a `work` record (the offer's seconds, the answer, the end's words),
every change the client is told a `change` record, and a frame of each: changes-stripped, -fibre, -cord, -cleared, -dug and
-felled or -cut. Then the two verifiers run on what the run left: `save_check.py` on the world copy the server saved (the
region file's change layers) and `tile_check.py` on the run's tile cache against it (the tiles unchanged).

Claude's tool: it runs the player and reports; the frames are the owner's to judge, the numbers the checks'.

Usage, from the repository root:
    python Tools/world/changes.py [--build] [--player Build/Harness/EarthGame2.exe] [--world Artefacts/worlds/gate]

Exit 0 when the player exits 0 (the strip, the fibre, the cord, the clearing and the dig done, every frame written, nothing
logged as an error) and both checks are green; 1 otherwise.
"""
import argparse
import json
from pathlib import Path
import subprocess
import sys
from datetime import datetime, timezone

sys.path.insert(0, str(Path(__file__).resolve().parent))
from wade import HOSTS, copied, run  # noqa: E402  (one owner of the runner)

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
    directory = ROOT / "Artefacts/frames" / ("changes-" + datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%SZ"))
    directory.mkdir(parents=True)

    if args.build:
        code = run([sys.executable, ROOT / "Tools/build/install.py", "--into", "harness", "--label", "changes"], 1200)
        if code != 0:
            raise RuntimeError("install exit %d" % code)
    if not player.is_file():
        raise RuntimeError("no player at %s; run with --build" % player)
    if not (world / "world.json").is_file():
        raise RuntimeError("no world at %s; run Tools/world/create.py first" % world)
    if next((h for h in HOSTS if h.is_file()), None) is None:
        raise RuntimeError("no server host; build it: dotnet build Engine/tools/EarthGame.ServerHost -c Release")

    world = copied(world, directory)
    code = run([player, "-batchmode", "-logFile", directory / "player.log",
                "-eg-mode", "solo", "-eg-dev", "-eg-name", "William", "-eg-world", world.name, "-eg-saves", world.parent,
                "-eg-tiles", directory / "tiles", "-eg-record", directory, "-eg-scenario", "changes"], 1500)

    log = directory / "run.jsonl"
    records = [json.loads(line) for line in log.read_text(encoding="utf-8").splitlines()] if log.is_file() else []
    end = next((r for r in reversed(records) if r.get("kind") == "end"), {})
    frames = sorted(p.name for p in (directory / "frames").glob("*.png")) if (directory / "frames").is_dir() else []
    print("player exit %d: %d frame(s): %s" % (code, len(frames), ", ".join(frames)))
    print("stripped %s, fibre %s, corded %s, pulled %s, cleared %s, dug %s, cut kept %s/255, felled %s; %s cell(s) of change held"
          % (end.get("stripped"), end.get("fibre"), end.get("corded"), end.get("pulled"), end.get("cleared"), end.get("dug"),
             end.get("cut_kept"), end.get("felled"), end.get("changes_held")))
    changes = 0
    for r in records:
        if r.get("kind") == "work":
            print("  %-12s on %s: offered %.1f s, answer %s, ended %s: \"%s\"%s" % (r.get("work"), r.get("on", r.get("row", "")), float(r.get("seconds") or 0.0), r.get("answer"),
                                                                                  r.get("ended"), r.get("words", ""), (" (cut kept %s, through %s)" % (r.get("cut_kept"), r.get("through"))) if "cut_kept" in r else ""))
        elif r.get("kind") == "aim":
            print("  aimed at the %s %s; the line: '%s'" % (r.get("at"), r.get("aimed"), r.get("line", "")))
        elif r.get("kind") == "change":
            changes += 1
        elif r.get("kind") in ("error", "exception"):
            print("  %s: %s" % (r["kind"], r.get("message", "")[:300]))
    print("  %d change record(s) told to the client" % changes)
    print("%s error(s), %s correction(s)" % (end.get("errors"), end.get("corrections")))

    # The verifiers on what the run left: the world the server saved and the tiles the client cached.
    checks = 0
    for name, argv in (("save_check", [str(world)]), ("tile_check", [str(world), str(directory / "tiles")])):
        print("---- %s" % name)
        result = subprocess.run([sys.executable, str(ROOT / "Tools/verifiers/checks" / (name + ".py"))] + argv, cwd=ROOT, capture_output=True, text=True)
        tail = (result.stdout + result.stderr).strip().splitlines()
        for line in tail[-8:]:
            print("  " + line)
        print("  %s exit %d" % (name, result.returncode))
        if result.returncode != 0:
            checks += 1
    print(directory)
    return 0 if code == 0 and checks == 0 else 1


if __name__ == "__main__":
    try:
        sys.exit(main())
    except (OSError, RuntimeError, subprocess.TimeoutExpired, ValueError) as error:
        print("changes recording FAILED: %s" % error, file=sys.stderr)
        sys.exit(1)
