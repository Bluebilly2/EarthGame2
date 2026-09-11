#!/usr/bin/env python3
"""carry.py: the carrying frames (M1.5a), recorded by the built player on a world folder, windowless and muted.

Runs the player SOLO on the world with the recorder's carry scenario and six things dropped round the wake
(-eg-items 6): a stick lying within reach is looked at, picked up and held, the carrying window opened, the ground
ahead aimed at and the stick put down; then a cobble is picked up and kept, and the world is saved on the way out with
it carried, for save_check.py to read. Leaves the frames, run.jsonl, the tile cache and the player's log under
Artefacts/frames/carry-<stamp>/.

Claude's tool: it runs the player and reports what its end record says. It judges nothing: the frames are the owner's
to judge, and save_check.py reads the world.

Usage, from the repository root:
    python Tools/world/carry.py [--build] [--player Build/Player-Carry/EarthGame2.exe] [--world Artefacts/worlds/gate]
Exit 0 when the player exits 0 (every frame written, every verb done, nothing logged as an error); 1 otherwise.
"""
import argparse
import json
import os
from pathlib import Path
import subprocess
import sys
from datetime import datetime, timezone

ROOT = Path(__file__).resolve().parents[2]
EDITOR_VERSION = next(line.split(":", 1)[1].strip()
                      for line in (ROOT / "Unity/ProjectSettings/ProjectVersion.txt").read_text().splitlines()
                      if line.startswith("m_EditorVersion:"))
UNITY = Path(os.environ.get("ProgramFiles", r"C:\Program Files")) / "Unity/Hub/Editor" / EDITOR_VERSION / "Editor/Unity.exe"
PLAYER = ROOT / "Build/Player-Carry/EarthGame2.exe"


def run(args, timeout):
    print("run:", " ".join(map(str, args)), flush=True)
    with subprocess.Popen(list(map(str, args)), cwd=ROOT, stdin=subprocess.DEVNULL,
                          creationflags=subprocess.CREATE_NO_WINDOW if os.name == "nt" else 0) as process:
        try:
            return process.wait(timeout=timeout)
        except subprocess.TimeoutExpired:
            process.kill()
            process.wait()
            raise


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--build", action="store_true")
    parser.add_argument("--player", default=str(PLAYER))
    parser.add_argument("--world", default="Artefacts/worlds/gate")
    args = parser.parse_args()
    player = (ROOT / args.player).resolve()
    world = (ROOT / args.world).resolve()
    directory = ROOT / "Artefacts/frames" / ("carry-" + datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%SZ"))
    directory.mkdir(parents=True)

    if args.build:
        code = run([UNITY, "-batchmode", "-nographics", "-projectPath", ROOT / "Unity", "-quit",
                    "-executeMethod", "EarthGame.Editor.CIBuild.BuildWindows", "-buildOut", player.parent,
                    "-logFile", directory / "build.log"], 900)
        if code != 0:
            raise RuntimeError("Unity build exit %d: %s" % (code, directory / "build.log"))
    if not player.is_file():
        raise RuntimeError("no player at %s; run with --build" % player)
    if not (world / "world.json").is_file():
        raise RuntimeError("no world at %s; run Tools/world/create.py first" % world)

    # -batchmode without -nographics: the recorder renders its frames on the GPU into textures, with no window.
    code = run([player, "-batchmode", "-logFile", directory / "player.log",
                "-eg-mode", "solo", "-eg-name", "William", "-eg-world", world.name, "-eg-saves", world.parent,
                "-eg-tiles", directory / "tiles", "-eg-record", directory, "-eg-scenario", "carry", "-eg-items", "6"], 600)

    log = directory / "run.jsonl"
    records = [json.loads(line) for line in log.read_text(encoding="utf-8").splitlines()] if log.is_file() else []
    end = next((r for r in reversed(records) if r.get("kind") == "end"), {})
    frames = sorted(p.name for p in (directory / "frames").glob("*.png")) if (directory / "frames").is_dir() else []
    print("player exit %d: %d frame(s): %s" % (code, len(frames), ", ".join(frames)))
    print("picked up %s, put down %s, kept carried %s; answers %s; %s error(s), %s correction(s)"
          % (end.get("picked_up"), end.get("put_down"), end.get("kept_carried"), end.get("answers"),
             end.get("errors"), end.get("corrections")))
    for r in records:
        if r.get("kind") in ("error", "exception"):
            print("  %s: %s" % (r["kind"], r.get("message", "")[:300]))
    print(directory)
    return 0 if code == 0 else 1


if __name__ == "__main__":
    try:
        sys.exit(main())
    except (OSError, RuntimeError, subprocess.TimeoutExpired, ValueError) as error:
        print("carry recording FAILED: %s" % error, file=sys.stderr)
        sys.exit(1)
