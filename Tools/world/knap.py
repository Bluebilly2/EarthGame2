#!/usr/bin/env python3
"""knap.py: the first stone (FP.3), recorded by the built player on a world folder, windowless and muted.

Runs the player's `knap` scenario as a development game on the world named: the founder is stood at the wake by the panel's
deed with a full body; two silcrete cobbles, the coast's knapping stone, are set down two metres ahead by the panel's deed,
the founder turning a little between them so they lie a pace apart; the first is faced and picked up as the hammer, the
second faced, and the work button is pressed three times: a tap, a hold of about half a second and a full swing. Each blow
is a `blow` record with the hammer and the core as they were, the wind-up the client sent, the body's water, the server's
answer in its own words, and what came of it (the core as it is now, the flake with its own mass and edge). A frame of the
flakes lying beside the core ("flake-lying") and of a flake picked up and held ("flake-held"). Leaves the frames, run.jsonl,
the tile cache and the logs under Artefacts/frames/knap-<stamp>/; `Tools/verifiers/checks/knap_check.py <that folder>`
holds every blow to the fracture mechanics it restates from its sources.

Claude's tool: it runs the player and reports; the frames are the owner's to judge, the numbers the check's.

Usage, from the repository root:
    python Tools/world/knap.py [--build] [--player Build/Harness/EarthGame2.exe] [--world Artefacts/worlds/gate]

Exit 0 when the player exits 0 (the tap bounced, a flake came away and was a usable tool, a flake was held, every frame
written and nothing logged as an error); 1 otherwise.
"""
import argparse
import json
from pathlib import Path
import subprocess
import sys
from datetime import datetime, timezone

sys.path.insert(0, str(Path(__file__).resolve().parent))
from wade import HOSTS, number, run  # noqa: E402  (one owner of the runner and the number)

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
    directory = ROOT / "Artefacts/frames" / ("knap-" + datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%SZ"))
    directory.mkdir(parents=True)

    if args.build:
        # The build lands through the one install tool (M1.Bb): staged, versioned, never over a running player.
        code = run([sys.executable, ROOT / "Tools/build/install.py", "--into", "harness", "--label", "knap"], 1200)
        if code != 0:
            raise RuntimeError("install exit %d" % code)
    if not player.is_file():
        raise RuntimeError("no player at %s; run with --build" % player)
    if not (world / "world.json").is_file():
        raise RuntimeError("no world at %s; run Tools/world/create.py first" % world)
    if next((h for h in HOSTS if h.is_file()), None) is None:
        raise RuntimeError("no server host; build it: dotnet build Engine/tools/EarthGame.ServerHost -c Release")

    # -batchmode without -nographics: the recorder renders its frames on the GPU into textures, with no window.
    # -eg-dev: a development game, whose server takes the scenario's deeds (the wake, the body, the stones set down).
    code = run([player, "-batchmode", "-logFile", directory / "player.log",
                "-eg-mode", "solo", "-eg-dev", "-eg-name", "William", "-eg-world", world.name, "-eg-saves", world.parent,
                "-eg-tiles", directory / "tiles", "-eg-record", directory, "-eg-scenario", "knap"], 900)

    log = directory / "run.jsonl"
    records = [json.loads(line) for line in log.read_text(encoding="utf-8").splitlines()] if log.is_file() else []
    end = next((r for r in reversed(records) if r.get("kind") == "end"), {})
    frames = sorted(p.name for p in (directory / "frames").glob("*.png")) if (directory / "frames").is_dir() else []
    print("player exit %d: %d frame(s): %s" % (code, len(frames), ", ".join(frames)))
    print("hammer held %s, the blow offered %s; %s blow(s): %s flaked, %s bounced; %s flake(s) seen, a usable flake %s, a flake held %s"
          % (end.get("hammer_held"), end.get("offered"), end.get("blows"), end.get("flaked"), end.get("bounced"),
             end.get("flakes_seen"), end.get("usable_flake"), end.get("flake_held")))
    for r in records:
        if r.get("kind") == "aim":
            print("  aimed at the core %s; the line: '%s'" % (r.get("aimed"), r.get("line", "")))
        elif r.get("kind") == "blow":
            line = "  %s blow: wind-up %.3f on %s %.3f kg at %.1f deg -> %s: \"%s\"" % (
                r.get("name"), number(r.get("wind_up")), r.get("core_stone", "").lower() or r.get("core_key"),
                number(r.get("core_mass_before_kg")), number(r.get("core_platform_before_deg")), r.get("outcome"), r.get("note", ""))
            if "flake_mass_kg" in r:
                line += "; a flake of %.1f g with an edge of %.3f%s" % (number(r.get("flake_mass_kg")) * 1000.0, number(r.get("flake_edge")),
                                                                       " (a tool)" if r.get("flake_usable") else " (a scrap)")
            if "core_mass_after_kg" in r:
                line += "; the core %.3f kg at %.1f deg, %s flake(s) off it" % (number(r.get("core_mass_after_kg")), number(r.get("core_platform_after_deg")), r.get("core_flakes_after"))
            elif r.get("core_gone"):
                line += "; the core is gone"
            print(line)
        elif r.get("kind") in ("error", "exception"):
            print("  %s: %s" % (r["kind"], r.get("message", "")[:300]))
    print("%s error(s), %s correction(s); %s footfall(s) on %s" % (end.get("errors"), end.get("corrections"), end.get("footfalls"), end.get("underfoot")))
    print(directory)
    return 0 if code == 0 else 1


if __name__ == "__main__":
    try:
        sys.exit(main())
    except (OSError, RuntimeError, subprocess.TimeoutExpired, ValueError) as error:
        print("knap recording FAILED: %s" % error, file=sys.stderr)
        sys.exit(1)
