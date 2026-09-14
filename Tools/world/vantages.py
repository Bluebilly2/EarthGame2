#!/usr/bin/env python3
"""vantages.py: the founder's frames at the four vantages (M1.4d's three and the new wake), recorded by the built
player on a world folder, windowless and muted.

Stands the founder at each vantage by the host (its console's `stand` and `save`) and runs the player's first-frame
scenario there — wake, walk and turn at 1440p and 1080p — and with `--hold <seconds>` measures what a frame costs over
a full turn, as M1.6a's rounds did. Prints, for each vantage, what the run's end record says: the frames written, the
errors, the median frame, and (M1.6c) how many tufts of each shape the client placed round the founder beside what the
cover layer says grows there. With `--lookout <metres>` (M1.6d) each run is a development game whose founder, after the
turn, flies up that far and takes four more frames, one to each point of the compass, so the country is seen to the
region's edge, which from the ground inside the forest the trees hide.

Claude's tool: it runs the player and reports what the run reported. The frames are the owner's to judge.

Usage, from the repository root:
    python Tools/world/vantages.py [--build] [--player Build/Player-Vantages/EarthGame2.exe]
                                   [--world Artefacts/worlds/gate] [--hold 20] [--hide understorey] [--lookout 150]
                                   [--only wake,shore,windermere,new-wake] [--out Artefacts/frames/vantages-<stamp>]
Exit 0 when every vantage's player exited 0; 1 otherwise.
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
PLAYER = ROOT / "Build/Player-Vantages/EarthGame2.exe"
HOSTS = [ROOT / "Engine/.build/bin/EarthGame.ServerHost/Release/net10.0/EarthGame.ServerHost.dll",
         ROOT / "Engine/.build/bin/EarthGame.ServerHost/Debug/net10.0/EarthGame.ServerHost.dll"]

# The vantages: M1.4d's three, where the ground's colour was judged, and the wake the scorer chose after M1.2b moved it.
VANTAGES = {
    "wake": (-1352, 1904),
    "shore": (-1332, 1892),
    "windermere": (-650, 334),
    "new-wake": (-1392, 2804),
}


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


def number(value):
    return float(value) if isinstance(value, (int, float)) else float("nan")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--build", action="store_true")
    parser.add_argument("--player", default=str(PLAYER))
    parser.add_argument("--world", default="Artefacts/worlds/gate")
    parser.add_argument("--hold", type=float, default=0.0, help="seconds of a full turn to measure a frame's cost over")
    parser.add_argument("--hide", default=None, help="what the player draws nothing of (-eg-hide), to part what it costs")
    parser.add_argument("--lookout", type=float, default=0.0, help="metres to fly the founder up for four frames to the compass points")
    parser.add_argument("--only", default=None, help="a comma-separated subset of " + ",".join(VANTAGES))
    parser.add_argument("--out", default=None)
    args = parser.parse_args()
    player = (ROOT / args.player).resolve()
    world = (ROOT / args.world).resolve()
    wanted = [v.strip() for v in args.only.split(",")] if args.only else list(VANTAGES)
    for name in wanted:
        if name not in VANTAGES:
            raise RuntimeError("no vantage called %s; there are %s" % (name, ", ".join(VANTAGES)))
    directory = Path(args.out).resolve() if args.out else ROOT / "Artefacts/frames" / ("vantages-" + datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%SZ"))
    directory.mkdir(parents=True, exist_ok=True)

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
    host = next((h for h in HOSTS if h.is_file()), None)
    if host is None:
        raise RuntimeError("no server host; build it: dotnet build Engine/tools/EarthGame.ServerHost -c Release")

    failed = 0
    for name in wanted:
        east, north = VANTAGES[name]
        at = directory / name
        at.mkdir(parents=True, exist_ok=True)
        stood = subprocess.run(["dotnet", str(host), "+server.world", str(world), "+server.port", "28318", "+server.local", "1"],
                               input="stand William %d %d\nsave\nstop\n" % (east, north),
                               capture_output=True, text=True, cwd=ROOT, timeout=600)
        (at / "host.log").write_text(stood.stdout + stood.stderr, encoding="utf-8")
        if stood.returncode != 0:
            raise RuntimeError("the host exited %d standing the founder at %s; see %s" % (stood.returncode, name, at / "host.log"))

        # -batchmode without -nographics: the recorder renders its frames on the GPU into textures, with no window.
        command = [player, "-batchmode", "-logFile", at / "player.log",
                   "-eg-mode", "solo", "-eg-name", "William", "-eg-world", world.name, "-eg-saves", world.parent,
                   "-eg-tiles", at / "tiles", "-eg-record", at]
        if args.hold > 0.0:
            command += ["-eg-hold", str(args.hold)]
        if args.hide:
            command += ["-eg-hide", args.hide]
        if args.lookout > 0.0:
            command += ["-eg-dev", "-eg-lookout", str(args.lookout)]
        code = run(command, 900)
        failed += 1 if code != 0 else 0

        records = [json.loads(line) for line in (at / "run.jsonl").read_text(encoding="utf-8").splitlines()] if (at / "run.jsonl").is_file() else []
        end = next((r for r in reversed(records) if r.get("kind") == "end"), {})
        timing = next((r for r in reversed(records) if r.get("kind") == "timing"), {})
        frames = sorted(p.name for p in (at / "frames").glob("*.png")) if (at / "frames").is_dir() else []
        print("%s (east %d north %d): player exit %d, %d frame(s), %s error(s)" % (name, east, north, code, len(frames), end.get("errors")))
        if timing:
            print("  the frame: median %.1f ms, p95 %.1f ms, worst %.1f ms over %s frames%s"
                  % (number(timing.get("median_ms")), number(timing.get("p95_ms")), number(timing.get("worst_ms")),
                     timing.get("frames"), (" hiding " + timing.get("hidden")) if timing.get("hidden") else ""))
        if end.get("understorey") is not None:
            print("  the understorey placed %s (%.2f ms), where the cover says %s"
                  % (end.get("understorey") or "nothing", number(end.get("understorey_ms")), end.get("cover") or "nothing"))
        if end.get("far_trees") is not None:
            print("  the far forest: %s far trees placed over the whole region, drawn wherever a tile's stand is not held" % end.get("far_trees"))
        for r in records:
            if r.get("kind") in ("error", "exception"):
                print("  %s: %s" % (r["kind"], r.get("message", "")[:300]))
    print(directory)
    return 1 if failed else 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except (OSError, RuntimeError, subprocess.TimeoutExpired, ValueError) as error:
        print("vantages FAILED: %s" % error, file=sys.stderr)
        sys.exit(1)
