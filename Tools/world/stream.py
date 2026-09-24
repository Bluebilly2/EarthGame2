#!/usr/bin/env python3
"""stream.py: one real join over a shaped socket, so the tile cache and the join's bytes can be read.

Starts the dedicated host on a world folder and runs the built player against it in join mode, windowless and
muted, with a tile cache of its own so nothing carries over from a previous run. The shaping is M1.B's first
condition, which CANON ruling 11's N1 is measured under: 100 ms round trip, 20 ms of jitter, 2 per cent loss and
a 10 Mbit/s cap, applied to both ends' sockets. Leaves the run under Artefacts/streaming/<stamp>/ with the
player's run.jsonl, its tile cache, and the host's log; tile_check.py reads the cache and the world.

Claude's tool: it runs and reports exit codes. It measures nothing and judges nothing.

Usage, from the repository root:
    python Tools/world/stream.py [--build] [--world Artefacts/worlds/gate]
Exit 0 when the host and the player both exit 0 and the player reached interactive; 1 otherwise.
"""
import argparse
import json
import os
from pathlib import Path
import subprocess
import sys
import time
from datetime import datetime, timezone

ROOT = Path(__file__).resolve().parents[2]
HOST_DLL = ROOT / "Engine/.build/bin/EarthGame.ServerHost/Release/net10.0/EarthGame.ServerHost.dll"
EDITOR_VERSION = next(line.split(":", 1)[1].strip()
                      for line in (ROOT / "Unity/ProjectSettings/ProjectVersion.txt").read_text().splitlines()
                      if line.startswith("m_EditorVersion:"))
UNITY = Path(os.environ.get("ProgramFiles", r"C:\Program Files")) / "Unity/Hub/Editor" / EDITOR_VERSION / "Editor/Unity.exe"
PLAYER = ROOT / "Build/Harness/EarthGame2.exe"
sys.path.insert(0, str(Path(__file__).resolve().parent))
import machine  # noqa: E402  (which copy this is, its ports, and the quiet hours: M1.Bd)

PORT = machine.port(28297)
# CANON ruling 11's first condition, one way on each end's socket.
LATENCY_MS, JITTER_MS, LOSS_PERCENT, CAP_BYTES = 50, 10, 2, 1250000
SECONDS = 60
WALK_SECONDS = 30


def run(args, timeout, **kwargs):
    print("run:", " ".join(map(str, args)), flush=True)
    with subprocess.Popen(list(map(str, args)), cwd=ROOT,
                          creationflags=subprocess.CREATE_NO_WINDOW if os.name == "nt" else 0, **kwargs) as process:
        try:
            return process.wait(timeout=timeout)
        except subprocess.TimeoutExpired:
            process.kill()
            process.wait()
            raise


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--build", action="store_true")
    parser.add_argument("--world", default="Artefacts/worlds/gate")
    args = parser.parse_args()
    machine.refuse_in_quiet_hours("stream")
    directory = ROOT / "Artefacts/streaming" / datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%S%fZ")
    directory.mkdir(parents=True)

    if subprocess.call(["dotnet", "build", str(ROOT / "Engine/tools/EarthGame.ServerHost"), "-c", "Release", "--nologo", "-v", "minimal"],
                       cwd=ROOT, stdout=open(directory / "host-build.log", "w"), stderr=subprocess.STDOUT) != 0:
        raise RuntimeError("the host would not build: %s" % (directory / "host-build.log"))
    if args.build:
        # The build lands through the one install tool (M1.Bb): staged, versioned, never over a running player.
        code = run([sys.executable, ROOT / "Tools/build/install.py", "--into", "harness", "--label", "stream"], 1200)
        if code != 0:
            raise RuntimeError("install exit %d" % code)
    if not PLAYER.is_file():
        raise RuntimeError("no player at %s; run with --build" % PLAYER)
    if not (ROOT / args.world / "world.json").is_file():
        raise RuntimeError("no world at %s; run Tools/world/create.py first" % args.world)

    host_log = open(directory / "host.log", "w")
    host = subprocess.Popen(["dotnet", str(HOST_DLL), "+server.port", str(PORT), "+server.local", "1", "+server.world", args.world,
                             "+server.seconds", str(SECONDS),
                             "+server.simulate.latency", str(LATENCY_MS), "+server.simulate.jitter", str(JITTER_MS),
                             "+server.simulate.loss", str(LOSS_PERCENT), "+server.sendcap", str(CAP_BYTES),
                             "+server.log", str(directory / "server.jsonl")],
                            cwd=ROOT, stdin=subprocess.DEVNULL, stdout=host_log, stderr=subprocess.STDOUT,
                            creationflags=subprocess.CREATE_NO_WINDOW if os.name == "nt" else 0)
    try:
        # The host reads its world and encodes its tiles before it listens; wait for it to say so.
        deadline = time.time() + 180.0
        while time.time() < deadline:
            if "listening" in (directory / "host.log").read_text(errors="replace") or host.poll() is not None:
                break
            time.sleep(0.5)
        if host.poll() is not None:
            raise RuntimeError("the host stopped before it listened: %s" % (directory / "host.log"))
        player = directory / "player"
        player.mkdir()
        code = run([PLAYER, "-batchmode", "-nographics", "-logFile", player / "player.log",
                    "-eg-mode", "join", "-eg-address", "127.0.0.1", "-eg-port", str(PORT), "-eg-name", "William",
                    # The walk, not the join: the join scenario quits the moment the ground is ready, and the
                    # water travels behind it. Walking also asks for tiles as the founder moves, which is what
                    # the residency bound is for.
                    "-eg-scenario", "walk", "-eg-seconds", str(WALK_SECONDS), "-eg-record", player,
                    "-eg-saves", directory / "saves", "-eg-tiles", directory / "tiles",
                    "-eg-latency", str(LATENCY_MS), "-eg-jitter", str(JITTER_MS),
                    "-eg-loss", str(LOSS_PERCENT), "-eg-sendcap", str(CAP_BYTES)], 300)
        if code != 0:
            raise RuntimeError("the player exited %d: %s" % (code, player / "player.log"))
    finally:
        if host.poll() is None:
            host.terminate()
            try:
                host.wait(timeout=60)
            except subprocess.TimeoutExpired:
                host.kill()
        host_log.close()

    records = [json.loads(line) for line in (player / "run.jsonl").read_text().splitlines()]
    interactive = [r for r in records if r.get("kind") == "interactive"]
    if not interactive:
        raise RuntimeError("the player never became interactive: %s" % (player / "run.jsonl"))
    first = interactive[0]
    end = records[-1]
    (directory / "latest.txt").write_text(str(directory), encoding="utf-8")
    (directory.parent / "latest.txt").write_text(str(directory), encoding="utf-8")
    print("interactive %.2f s after connect: %d tile(s) of ground, %s bytes received by then"
          % (first.get("since_connect_s", -1), first.get("tiles_held", -1), "{:,}".format(first.get("bytes_received", -1))))
    # The end record carries no byte count; the per-second samples do, and the last of them is the run's total.
    samples = [r for r in records if r.get("kind") == "sample" and "bytes_received" in r]
    total = samples[-1]["bytes_received"] if samples else None
    print("at the end: %s bytes received, %d error(s), %d correction(s), exit %s"
          % ("{:,}".format(total) if total is not None else "not sampled", end.get("errors", -1),
             end.get("corrections", -1), end.get("exit", "?")))
    for folder in sorted((directory / "tiles").glob("*/*")):
        tiles = list(folder.glob("*.tile"))
        print("  cached %-12s %2d tile(s), %s bytes" % (folder.name, len(tiles), "{:,}".format(sum(t.stat().st_size for t in tiles))))
    print(directory)
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except (OSError, RuntimeError, subprocess.TimeoutExpired, ValueError) as error:
        print("stream recording FAILED: %s" % error, file=sys.stderr)
        sys.exit(1)
