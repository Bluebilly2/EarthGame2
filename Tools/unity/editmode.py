#!/usr/bin/env python3
"""editmode.py: the Unity edit-mode tests, run in batch against the project, with the suite's verdict as the exit code.

The edit-mode tests hold what only the editor can see: the project checklist, the prefab registry, the terrain tile
builder, the stand's meshes, and the source scan for the per-draw property block the rendering rules forbid. Before
2026-09-11 only the loading gate ran them, inside `Tools/world/loading.py`'s recordings; the gates M1.6a ran did not,
and a comment naming the forbidden call in StandViews.cs kept the suite red from M1.6a's landing without anything
saying so.

Claude's tool: it runs the suite and reports the suite's own verdict; it judges nothing itself.

Usage, from the repository root, with the Unity editor closed:
    python Tools/unity/editmode.py
Exit 0 when every test passed; 1 when any failed or the run left nothing to read; 2 when the editor has the project
open. Leaves Artefacts/editmode/<stamp>/results.xml and unity.log.
"""
import os
import subprocess
import sys
import xml.etree.ElementTree as ET
from datetime import datetime, timezone
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
EDITOR_VERSION = next(line.split(":", 1)[1].strip()
                      for line in (ROOT / "Unity/ProjectSettings/ProjectVersion.txt").read_text().splitlines()
                      if line.startswith("m_EditorVersion:"))
UNITY = Path(os.environ.get("ProgramFiles", r"C:\Program Files")) / "Unity/Hub/Editor" / EDITOR_VERSION / "Editor/Unity.exe"
sys.path.insert(0, str(ROOT / "Tools" / "world"))
import machine  # noqa: E402  (which copy this is, and the quiet hours: M1.Bd)


def main():
    machine.refuse_in_quiet_hours("editmode")
    if machine.unity_holds(ROOT):
        print("the Unity editor has the project open (Unity/Temp/UnityLockfile); close it and run again")
        return 2
    directory = ROOT / "Artefacts/editmode" / datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%SZ")
    directory.mkdir(parents=True)
    results, log = directory / "results.xml", directory / "unity.log"
    code = subprocess.call([str(UNITY), "-batchmode", "-nographics", "-projectPath", str(ROOT / "Unity"),
                            "-runTests", "-testPlatform", "EditMode", "-testResults", str(results), "-logFile", str(log)],
                           cwd=ROOT, creationflags=subprocess.CREATE_NO_WINDOW if os.name == "nt" else 0)
    try:
        run = ET.parse(results).getroot()
    except (OSError, ET.ParseError) as error:
        print("Unity exited %d and left no results to read (%s); see %s" % (code, error, log))
        return 1
    print("edit-mode tests: %s total, %s passed, %s failed (Unity exit %d)" % (run.get("total"), run.get("passed"), run.get("failed"), code))
    for case in run.iter("test-case"):
        if case.get("result") != "Passed":
            message = case.find("failure/message")
            print("  %s %s: %s" % (case.get("result"), case.get("fullname"), (message.text or "").strip()[:400] if message is not None else ""))
    print(directory)
    return 0 if run.get("failed") == "0" and code == 0 else 1


if __name__ == "__main__":
    sys.exit(main())
