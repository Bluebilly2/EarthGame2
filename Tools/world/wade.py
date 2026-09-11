#!/usr/bin/env python3
"""wade.py: the wading frames (M1.5d) and the swimming frames (M1.5e), recorded by the built player on a world folder,
windowless and muted.

Finds on the world's own layers a dry shore of a lake (Lake Windermere, whose water stands at 16.2 m, unless another
level is named), on ground no steeper than 6 degrees and the nearest such to the lake's middle: beside water 0.5 to
1.2 m deep for the wading, or within 30 m of water 2.2 m deep or more for the swimming (--swim). Stands the founder
there by the host (its console's `stand` and `save`) and runs the player's `wade` scenario, in which the founder finds
the nearest water deep enough to wade in what the server streamed, walks into it and stops; or its `swim` scenario, in
which they walk on into water deep enough to swim, swim, and stop. Then prints what the run's end record says beside
the depth the world's own layers put under where the founder stopped: the surface less the ground, read between cells
as the server reads them. Leaves the frames, run.jsonl, the tile cache and the logs under
Artefacts/frames/wade-<stamp>/ or swim-<stamp>/.

Claude's tool: it runs the player and holds one number the run reports against the layers it was streamed from. The
frames are the owner's to judge.

Usage, from the repository root:
    python Tools/world/wade.py [--build] [--swim] [--player Build/Player-Wade/EarthGame2.exe]
                               [--world Artefacts/worlds/gate] [--lake 16.2]
Exit 0 when the player exits 0 (the founder waded and went slower in the water than on land, or swam with the camera
never under the water; every frame was written and nothing was logged as an error) and the layers put water deeper
than the mover's wading depth, 0.4 m, or for the swimming its swimming depth, 1.53 m, where the founder stopped; 1
otherwise.
"""
import argparse
import json
import math
import os
from pathlib import Path
import subprocess
import sys
from datetime import datetime, timezone

import numpy as np

ROOT = Path(__file__).resolve().parents[2]
EDITOR_VERSION = next(line.split(":", 1)[1].strip()
                      for line in (ROOT / "Unity/ProjectSettings/ProjectVersion.txt").read_text().splitlines()
                      if line.startswith("m_EditorVersion:"))
UNITY = Path(os.environ.get("ProgramFiles", r"C:\Program Files")) / "Unity/Hub/Editor" / EDITOR_VERSION / "Editor/Unity.exe"
PLAYER = ROOT / "Build/Player-Wade/EarthGame2.exe"
HOSTS = [ROOT / "Engine/.build/bin/EarthGame.ServerHost/Release/net10.0/EarthGame.ServerHost.dll",
         ROOT / "Engine/.build/bin/EarthGame.ServerHost/Debug/net10.0/EarthGame.ServerHost.dll"]
NP_DTYPES = {"u8": "u1", "u16": "<u2", "i16": "<i2", "u32": "<u4", "f32": "<f4"}
LAKE = 5                    # the water layer's class for a lake, as its legend states it
WADING_DEPTH_M = 0.4        # the mover's wading depth (MoverConfig.WadeDepth), restated
SWIMMING_DEPTH_M = 1.53     # the mover's swimming depth (MoverConfig.SwimDepth: the standing eye's 1.65 m less the
                            # 0.12 m a swimmer's eye rides over the water), restated
SHORE_DEPTH_M = (0.5, 1.2)
SWIM_DEEP_M = 2.2
SWIM_WITHIN_M = 30.0
SHORE_SLOPE_DEG = 6.0
LEVEL_WITHIN_M = 0.06


def layer(world, name):
    sidecar = json.load(open(world / "layers" / (name + ".json"), encoding="utf-8"))
    raw = np.fromfile(world / "layers" / sidecar["raw"], dtype=NP_DTYPES[sidecar["dtype"]])
    return sidecar, raw.reshape(sidecar["height"], sidecar["width"]).astype(np.float64)


def between(values, half, cell, east, north):
    """A layer read between its cell centres, as the server's Heightfield reads it: bilinear, clamped at the edge."""
    rows, cols = values.shape
    col = min(max((east + half) / cell, 0.0), cols - 1.0)
    row = min(max((half - north) / cell, 0.0), rows - 1.0)
    c0, r0 = min(int(math.floor(col)), cols - 2), min(int(math.floor(row)), rows - 2)
    tc, tr = col - c0, row - r0
    top = values[r0, c0] + (values[r0, c0 + 1] - values[r0, c0]) * tc
    bottom = values[r0 + 1, c0] + (values[r0 + 1, c0 + 1] - values[r0 + 1, c0]) * tc
    return top + (bottom - top) * tr


def near(mask, cells, square=False):
    """The cells within `cells` of a true one: every cell of the square round it when asked, else those of the disc."""
    out = np.zeros(mask.shape, dtype=bool)
    for dr in range(-cells, cells + 1):
        for dc in range(-cells, cells + 1):
            if square or dr * dr + dc * dc <= cells * cells:
                out |= np.roll(np.roll(mask, dr, axis=0), dc, axis=1)
    return out


def number(value):
    """A number from a run record, NaN when the record has none."""
    return float(value) if isinstance(value, (int, float)) else float("nan")


class Lake:
    def __init__(self, world, level, swim=False):
        side, self.z = layer(world, "heights")
        _, self.surface = layer(world, "surface")
        _, water = layer(world, "water")
        self.cell, self.half = float(side["cell_m"]), float(side["extent_m"]) / 2.0
        depth = self.surface - self.z
        gy, gx = np.gradient(self.z, self.cell)
        slope = np.degrees(np.arctan(np.hypot(gx, gy)))
        lake = (water == LAKE) & (np.abs(self.surface - level) < LEVEL_WITHIN_M) & (depth > 0.0)
        self.found = bool(lake.any())
        if not self.found:
            return
        rows, cols = np.nonzero(lake)
        self.centre = (float((cols * self.cell - self.half).mean()), float((self.half - rows * self.cell).mean()))
        if swim:
            beside = near(lake & (depth >= SWIM_DEEP_M), int(math.ceil(SWIM_WITHIN_M / self.cell)))
        else:
            beside = near(lake & (depth >= SHORE_DEPTH_M[0]) & (depth <= SHORE_DEPTH_M[1]), 1, square=True)
        shore = beside & (depth <= 0.0) & (slope <= SHORE_SLOPE_DEG)
        rows, cols = np.nonzero(shore)
        self.shore = None
        if len(rows):
            east, north = cols * self.cell - self.half, self.half - rows * self.cell
            k = int(np.argmin(np.hypot(east - self.centre[0], north - self.centre[1])))
            self.shore = (float(east[k]), float(north[k]))

    def depth(self, east, north):
        return between(self.surface, self.half, self.cell, east, north) - between(self.z, self.half, self.cell, east, north)


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
    parser.add_argument("--swim", action="store_true", help="the swimming frames (M1.5e): walked in over the head, and swimming")
    parser.add_argument("--player", default=str(PLAYER))
    parser.add_argument("--world", default="Artefacts/worlds/gate")
    parser.add_argument("--lake", type=float, default=16.2, help="the level the lake's water stands at, m (Lake Windermere's by default)")
    args = parser.parse_args()
    scenario = "swim" if args.swim else "wade"
    player = (ROOT / args.player).resolve()
    world = (ROOT / args.world).resolve()
    directory = ROOT / "Artefacts/frames" / (scenario + "-" + datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%SZ"))
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
    host = next((h for h in HOSTS if h.is_file()), None)
    if host is None:
        raise RuntimeError("no server host; build it: dotnet build Engine/tools/EarthGame.ServerHost -c Release")

    lake = Lake(world, args.lake, args.swim)
    if not lake.found or lake.shore is None:
        raise RuntimeError("no lake at %.1f m with a shore a founder could %s in from" % (args.lake, scenario))
    east, north = lake.shore
    print("the lake at %.1f m: its middle at east %.0f north %.0f; the founder stands at east %.0f north %.0f"
          % (args.lake, lake.centre[0], lake.centre[1], east, north))
    stood = subprocess.run(["dotnet", str(host), "+server.world", str(world), "+server.port", "28317"],
                           input="stand William %d %d\nsave\nstop\n" % (round(east), round(north)),
                           capture_output=True, text=True, cwd=ROOT, timeout=600)
    (directory / "host.log").write_text(stood.stdout + stood.stderr, encoding="utf-8")
    if stood.returncode != 0:
        raise RuntimeError("the host exited %d standing the founder; see %s" % (stood.returncode, directory / "host.log"))

    # -batchmode without -nographics: the recorder renders its frames on the GPU into textures, with no window.
    code = run([player, "-batchmode", "-logFile", directory / "player.log",
                "-eg-mode", "solo", "-eg-name", "William", "-eg-world", world.name, "-eg-saves", world.parent,
                "-eg-tiles", directory / "tiles", "-eg-record", directory, "-eg-scenario", scenario], 600)

    log = directory / "run.jsonl"
    records = [json.loads(line) for line in log.read_text(encoding="utf-8").splitlines()] if log.is_file() else []
    end = next((r for r in reversed(records) if r.get("kind") == "end"), {})
    frames = sorted(p.name for p in (directory / "frames").glob("*.png")) if (directory / "frames").is_dir() else []
    print("player exit %d: %d frame(s): %s" % (code, len(frames), ", ".join(frames)))
    layers_depth = lake.depth(number(end.get("east", east)), number(end.get("north", north)))
    if args.swim:
        print("swam %s at %.2f m/s; the camera came to %.3f m over the water at the lowest; %s stroke(s) heard; where the"
              " founder stopped the run was streamed %.2f m of water and the layers put %.2f m there (swimming is over %.2f m)"
              % (end.get("swam"), number(end.get("swimming_speed")), number(end.get("eye_over_water_min_m")), end.get("strokes"),
                 number(end.get("depth_m")), layers_depth, SWIMMING_DEPTH_M))
        need = SWIMMING_DEPTH_M
    else:
        print("waded %s: %.2f m/s on land and %.2f m/s in the water; where the founder stopped the run was streamed %.2f m of water"
              " and the layers put %.2f m there (wading is over %.1f m)"
              % (end.get("waded"), number(end.get("land_speed")), number(end.get("wading_speed")),
                 number(end.get("depth_m")), layers_depth, WADING_DEPTH_M))
        need = WADING_DEPTH_M
    print("%s error(s), %s correction(s); %s footfall(s) on %s" % (end.get("errors"), end.get("corrections"), end.get("footfalls"), end.get("underfoot")))
    for r in records:
        if r.get("kind") in ("error", "exception"):
            print("  %s: %s" % (r["kind"], r.get("message", "")[:300]))
    print(directory)
    return 0 if code == 0 and layers_depth > need else 1


if __name__ == "__main__":
    try:
        sys.exit(main())
    except (OSError, RuntimeError, subprocess.TimeoutExpired, ValueError) as error:
        print("%s recording FAILED: %s" % ("swim" if "--swim" in sys.argv else "wade", error), file=sys.stderr)
        sys.exit(1)
