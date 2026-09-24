#!/usr/bin/env python3
"""Record real new/continue/missing-data launches, isolated and muted. --build also runs Unity edit tests.
The verifier reads raw observations separately; this tool checks process exit codes only.
"""
import argparse
import json
import os
from pathlib import Path
import subprocess
import sys
from datetime import datetime, timezone

sys.path.insert(0, str(Path(__file__).resolve().parent))
import machine  # noqa: E402  (which copy this is, and the quiet hours: M1.Bd)

ROOT = Path(__file__).resolve().parents[2]
EDITOR_VERSION = next(line.split(":", 1)[1].strip()
                      for line in (ROOT / "Unity/ProjectSettings/ProjectVersion.txt").read_text().splitlines()
                      if line.startswith("m_EditorVersion:"))
UNITY = Path(os.environ.get("ProgramFiles", r"C:\Program Files")) / "Unity/Hub/Editor" / EDITOR_VERSION / "Editor/Unity.exe"


def run(args, timeout):
    print("run:", " ".join(map(str, args)), flush=True)
    with subprocess.Popen(list(map(str, args)), cwd=ROOT,
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
    parser.add_argument("--player", type=Path, default=ROOT / "Build/Harness/EarthGame2.exe")
    parser.add_argument("--unity", type=Path, default=UNITY)
    args = parser.parse_args()
    # It builds and launches the game three times: none of it in the quiet hours (CANON ruling 47). The folder ends with the
    # copy's tag, since the copies share one Artefacts and two runs may start in the same second (M1.Bd, 2026-09-24).
    machine.refuse_in_quiet_hours("loading")
    directory = ROOT / "Artefacts/loading" / (datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%S%fZ") + machine.tag())
    directory.mkdir(parents=True)
    if args.build:
        editor = [args.unity, "-batchmode", "-nographics", "-projectPath", ROOT / "Unity"]
        code = run(editor + ["-runTests", "-testPlatform", "EditMode", "-testResults",
                            directory / "editmode.xml", "-logFile", directory / "editmode.log"], 600)
        if code != 0:
            raise RuntimeError(f"Unity edit tests exit {code}: {directory}")
        # The build lands through the one install tool (M1.Bb): staged, versioned, never over a running player.
        code = run([sys.executable, ROOT / "Tools/build/install.py", "--into", "harness", "--label", "loading"], 1200)
        if code != 0:
            raise RuntimeError(f"install exit {code}")
    for scenario in ("new", "continue", "missing"):
        output = directory / scenario
        output.mkdir()
        saves = directory / ("missing-saves" if scenario == "missing" else "saves")
        command = [args.player.resolve(), "-batchmode", "-logFile", output / "player.log",
                   "-eg-mode", "solo", "-eg-seed", "1347", "-eg-world", "world-loading",
                   "-eg-saves", saves, "-eg-tiles", directory / "tiles",
                   "-eg-record", output, "-eg-loading-record", output]
        if scenario == "missing":
            command += ["-eg-data", directory / "absent-data"]
        code = run(command, 240)
        (output / "process.json").write_text(json.dumps({"exit": code}), encoding="utf-8")
        expected = 1 if scenario == "missing" else 0
        if code != expected:
            raise RuntimeError(f"{scenario} exit {code}, expected {expected}: {output}")
    (directory.parent / "latest.txt").write_text(str(directory), encoding="utf-8")
    print(directory)
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except (OSError, RuntimeError, subprocess.TimeoutExpired) as error:
        print(f"loading recording FAILED: {error}", file=sys.stderr)
        sys.exit(1)
