#!/usr/bin/env python3
"""night.py: the night's cold and death (FP.2), recorded by the built player on a world folder, windowless and muted.

Runs the player's `night` scenario as a development game on the world named, with the beta arc's bridge down
(-eg-no-bridge) so the cold can kill: the founder is stood at the wake by the panel's deed with a full, warm body, a stick
spawned, faced and picked up, and walked a dozen metres off; the clock is put to ten in the evening and sped to a day in half a minute,
every word the server has of the body written as a `warmth` record with the sky the client works out and the world's hours
beside it. The founder stands through the night: the first "cold" is a `cold_reached` record and a frame, the sun's rise a
`dawn` record and a frame (the lowest core, the coldest word, whether the night itself killed); then, unless it did, the
panel's row puts the core below the lethal, the death that follows is a `died` record with its sentence ("died" frame),
and the end record says whether a new founder stood at the wake with the stick lying where they fell. Leaves the frames,
run.jsonl, the tile cache and the logs under Artefacts/frames/night-<stamp>/; `Tools/verifiers/checks/cold_check.py <that
folder>` holds the records to the physiology they claim.

Claude's tool: it runs the player and reports; the frames are the owner's to judge, the numbers the check's.

Usage, from the repository root:
    python Tools/world/night.py [--build] [--player Build/Harness/EarthGame2.exe] [--world Artefacts/worlds/gate]

Exit 0 when the player exits 0 (cold in time, the dawn seen or the night itself deadly, died, woke at the wake, the stick
where they fell, every frame written and nothing logged as an error); 1 otherwise.
"""
import argparse
import json
from pathlib import Path
import subprocess
import sys
from datetime import datetime, timezone

sys.path.insert(0, str(Path(__file__).resolve().parent))
from wade import HOSTS, UNITY, copied, number, run, run_folder  # noqa: E402  (one owner of the runner and the number)

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
    directory = run_folder("night")
    directory.mkdir(parents=True)

    if args.build:
        # The build lands through the one install tool (M1.Bb): staged, versioned, never over a running player.
        code = run([sys.executable, ROOT / "Tools/build/install.py", "--into", "harness", "--label", "night"], 1200)
        if code != 0:
            raise RuntimeError("install exit %d" % code)
    if not player.is_file():
        raise RuntimeError("no player at %s; run with --build" % player)
    if not (world / "world.json").is_file():
        raise RuntimeError("no world at %s; run Tools/world/create.py first" % world)
    world = copied(world, directory)
    if next((h for h in HOSTS if h.is_file()), None) is None:
        raise RuntimeError("no server host; build it: dotnet build Engine/tools/EarthGame.ServerHost -c Release")

    # -batchmode without -nographics: the recorder renders its frames on the GPU into textures, with no window.
    # -eg-dev: a development game, whose server takes the scenario's settings (the wake, the body, the clock, the core).
    # -eg-no-bridge: the beta arc's bridge down, so the death this scenario proves can happen (ServerConfig.BetaArcBridge).
    code = run([player, "-batchmode", "-logFile", directory / "player.log",
                "-eg-mode", "solo", "-eg-dev", "-eg-no-bridge", "-eg-name", "William", "-eg-world", world.name, "-eg-saves", world.parent,
                "-eg-tiles", directory / "tiles", "-eg-record", directory, "-eg-scenario", "night"], 900)

    log = directory / "run.jsonl"
    records = [json.loads(line) for line in log.read_text(encoding="utf-8").splitlines()] if log.is_file() else []
    end = next((r for r in reversed(records) if r.get("kind") == "end"), {})
    frames = sorted(p.name for p in (directory / "frames").glob("*.png")) if (directory / "frames").is_dir() else []
    print("player exit %d: %d frame(s): %s" % (code, len(frames), ", ".join(frames)))
    print("cold %s after %.1f s at %s times the game's rate; the night's lowest core %.2f, coldest word '%s', dawn seen %s, the night itself killed %s; "
          "died %s (%s); woke at the wake %s (%.1f m off); the stick lies where they fell %s (%.1f m off)"
          % (end.get("cold"), number(end.get("cold_after_s")), end.get("clock_scale"), number(end.get("lowest_core_c")), end.get("coldest_word"),
             end.get("sun_up"), end.get("died_in_the_night"),
             end.get("died"), end.get("cause"), end.get("respawned_at_wake"), number(end.get("wake_distance_m")),
             end.get("stick_lies_where_fell"), number(end.get("stick_distance_m"))))
    if end.get("sentence"):
        print("  " + end["sentence"])
    print("%s error(s), %s correction(s); %s footfall(s) on %s" % (end.get("errors"), end.get("corrections"), end.get("footfalls"), end.get("underfoot")))
    for r in records:
        if r.get("kind") in ("error", "exception"):
            print("  %s: %s" % (r["kind"], r.get("message", "")[:300]))
    print(directory)
    return 0 if code == 0 else 1


if __name__ == "__main__":
    try:
        sys.exit(main())
    except (OSError, RuntimeError, subprocess.TimeoutExpired, ValueError) as error:
        print("night recording FAILED: %s" % error, file=sys.stderr)
        sys.exit(1)
