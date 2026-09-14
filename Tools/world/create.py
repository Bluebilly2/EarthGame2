#!/usr/bin/env python3
"""create.py: make a world from the command line, the way the M1.2 contract asks to see it made.

Builds the server host (Release), removes the target folder, runs the host once with +server.world so that the
world-creation pipeline writes every layer and the census, then prints what the contract asks for: the layer
files with their sizes and the checksums their sidecars state, the census as the host printed it, and the run's
time. Claude's tool: it creates and lists, it verifies nothing; the verifiers (drainage_check.py,
census_check.py) read the folder it leaves behind.

Usage, from the repository root:
    python Tools/world/create.py [folder]        default Artefacts/worlds/gate
Exit 0 when the host exits 0 and the folder holds a census; 1 otherwise.
"""
import json
import os
import shutil
import subprocess
import sys
import time

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
HOST_PROJECT = os.path.join(ROOT, "Engine", "tools", "EarthGame.ServerHost")
HOST_DLL = os.path.join(ROOT, "Engine", ".build", "bin", "EarthGame.ServerHost", "Release", "net10.0", "EarthGame.ServerHost.dll")
DEFAULT_FOLDER = os.path.join("Artefacts", "worlds", "gate")
PORT = 28299


def run(cmd, log_path):
    with open(log_path, "w", encoding="utf-8") as log:
        return subprocess.call(cmd, cwd=ROOT, stdout=log, stderr=subprocess.STDOUT)


def main(argv):
    folder = argv[1] if len(argv) > 1 else DEFAULT_FOLDER
    full = os.path.join(ROOT, folder)
    os.makedirs(os.path.dirname(full), exist_ok=True)
    build_log = full + ".build.log"
    if run(["dotnet", "build", HOST_PROJECT, "-c", "Release", "--nologo", "-v", "minimal"], build_log) != 0:
        print("host build failed; see %s" % build_log)
        return 1
    if os.path.isdir(full):
        shutil.rmtree(full)
    started = time.time()
    host_log = full + ".log"
    code = run(["dotnet", HOST_DLL, "+server.port", str(PORT), "+server.local", "1", "+server.world", folder, "+server.seconds", "1"], host_log)
    elapsed = time.time() - started
    census_path = os.path.join(full, "census.txt")
    if code != 0 or not os.path.isfile(census_path):
        print("the host exited %d and %s %s; see %s" % (code, census_path, "exists" if os.path.isfile(census_path) else "is missing", host_log))
        return 1

    layers_dir = os.path.join(full, "layers")
    total = 0
    print("layers of %s:" % folder)
    for name in sorted(os.listdir(layers_dir)):
        if not name.endswith(".json"):
            continue
        sidecar = json.load(open(os.path.join(layers_dir, name), encoding="utf-8"))
        raw = os.path.join(layers_dir, sidecar["raw"])
        size = os.path.getsize(raw)
        total += size
        print("  %-30s %-4s %12s bytes  sha256 %s" % (sidecar["raw"], sidecar["dtype"], "{:,}".format(size), sidecar["sha256"]))
    print("  %d layers, %s bytes" % (len([n for n in os.listdir(layers_dir) if n.endswith(".json")]), "{:,}".format(total)))
    print("census:")
    for line in open(census_path, encoding="utf-8").read().splitlines():
        print("  " + line)
    for line in open(host_log, encoding="utf-8"):
        if "created" in line and "layers" in line:
            print("host: " + line.strip())
    print("the run took %.1f s (build excluded)" % elapsed)
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
