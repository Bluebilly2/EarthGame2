#!/usr/bin/env python3
"""populate.py: a world with things in it, for save_check.py to read.

Runs the server host on a world folder (creating it when it does not exist, which needs the region's bake), reads
the wake from world.json, then runs the host again with spawn commands on its console: four cobbles and four
sticks on a ring 12 m from the wake, two of them dropped from 2 m so they fall and rest; waits for them to rest;
'save'; the host saves again when it stops. Prints what the host logged for the spawns and the save, then the
region files with their sizes and the CRCs their headers state, and the digest the server wrote. Claude's tool:
it populates and lists, it verifies nothing.

Usage, from the repository root:
    python Tools/world/populate.py [folder]        default Artefacts/worlds/gate
Exit 0 when both host runs exit 0 and the folder holds digest.txt; 1 otherwise.
"""
import json
import math
import os
import struct
import subprocess
import sys
import time

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
HOST_PROJECT = os.path.join(ROOT, "Engine", "tools", "EarthGame.ServerHost")
HOST_DLL = os.path.join(ROOT, "Engine", ".build", "bin", "EarthGame.ServerHost", "Release", "net10.0", "EarthGame.ServerHost.dll")
DEFAULT_FOLDER = os.path.join("Artefacts", "worlds", "gate")
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import machine  # noqa: E402  (which copy this is, its ports, and the quiet hours: M1.Bd)

PORT = machine.port(28298)
RING_M = 12.0


def run_host(folder, seconds, log_path, commands=None, settle_s=0.0):
    """Runs the host for `seconds`, feeding console commands after it has started; returns its exit code."""
    args = ["dotnet", HOST_DLL, "+server.port", str(PORT), "+server.local", "1", "+server.world", folder, "+server.seconds", str(seconds)]
    with open(log_path, "w", encoding="utf-8") as log:
        process = subprocess.Popen(args, cwd=ROOT, stdin=subprocess.PIPE, stdout=log, stderr=subprocess.STDOUT, text=True)
        if commands:
            # The host creates or continues the world before it listens; wait for its log to say so.
            deadline = time.time() + 120.0
            while time.time() < deadline:
                text = open(log_path, encoding="utf-8", errors="replace").read()
                if "created " in text or "continuing " in text:
                    break
                time.sleep(0.2)
            for command in commands:
                process.stdin.write(command + "\n")
                process.stdin.flush()
            time.sleep(settle_s)
            process.stdin.write("save\n")
            process.stdin.flush()
        try:
            process.stdin.close()
        except OSError:
            pass
        return process.wait()


def main(argv):
    machine.refuse_in_quiet_hours("populate")
    folder = argv[1] if len(argv) > 1 else DEFAULT_FOLDER
    full = os.path.join(ROOT, folder)
    os.makedirs(os.path.dirname(full), exist_ok=True)
    build_log = full + ".build.log"
    with open(build_log, "w", encoding="utf-8") as log:
        if subprocess.call(["dotnet", "build", HOST_PROJECT, "-c", "Release", "--nologo", "-v", "minimal"], cwd=ROOT, stdout=log, stderr=subprocess.STDOUT) != 0:
            print("host build failed; see %s" % build_log)
            return 1
    started = time.time()
    if run_host(folder, 1, full + ".create.log") != 0:
        print("the host could not create or continue %s; see %s" % (folder, full + ".create.log"))
        return 1
    world = json.load(open(os.path.join(full, "world.json"), encoding="utf-8"))
    east, north = world.get("wake_east", 0.0), world.get("wake_north", 0.0)
    commands = []
    for i in range(8):
        angle = i * math.pi / 4.0
        key = "item/cobble" if i % 2 == 0 else "item/stick"
        e, n = east + RING_M * math.cos(angle), north + RING_M * math.sin(angle)
        command = "spawn %s %.3f %.3f" % (key, e, n)
        if i in (1, 6):
            command += " %.3f" % (world.get("wake_up", 0.0) + 2.0 + 20.0)   # well above any ground: it falls
        commands.append(command)
    populate_log = full + ".populate.log"
    code = run_host(folder, 6, populate_log, commands, settle_s=3.0)
    elapsed = time.time() - started
    digest_path = os.path.join(full, "digest.txt")
    if code != 0 or not os.path.isfile(digest_path):
        print("the host exited %d and %s %s; see %s" % (code, digest_path, "exists" if os.path.isfile(digest_path) else "is missing", populate_log))
        return 1
    for line in open(populate_log, encoding="utf-8", errors="replace"):
        if "spawned" in line or "saved" in line or "refused" in line or "unknown command" in line:
            print("host: " + line.strip())
    regions = os.path.join(full, "regions")
    print("region files of %s:" % folder)
    total = 0
    for name in sorted(os.listdir(regions)) if os.path.isdir(regions) else []:
        if not name.endswith(".egr"):
            continue
        path = os.path.join(regions, name)
        with open(path, "rb") as f:
            head = f.read(34)
        magic, version, cx, cz, cell, count, diffs, crc = struct.unpack("<4sHiidIII", head)
        size = os.path.getsize(path)
        total += 1
        print("  %-14s %6d bytes  cell (%d, %d)  %d entities  crc32 %08x" % (name, size, cx, cz, count, crc))
    players = os.path.join(full, "players")
    for name in sorted(os.listdir(players)) if os.path.isdir(players) else []:
        print("  players/%s %d bytes" % (name, os.path.getsize(os.path.join(players, name))))
    print("  %d region files; digest.txt %s" % (total, open(digest_path, encoding="utf-8").read().strip()))
    print("the run took %.1f s (build excluded)" % elapsed)
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
