#!/usr/bin/env python3
"""install.py: build the Windows player once and put it where the game lives (M1.Bb, 2026-09-16).

The game has one folder for the owner, Build/Player, and one for the automated runs, Build/Harness; each is replaced in
place, never sprouted beside as Build/Player-<something>. The owner's words: "i dont want each new version of the game to
be in a new folder that asks for perms here in the claude harness and in my firewall". Every new folder an exe runs from
is a permission prompt in the harness and, if the exe opens a socket, a Windows firewall box on his screen.

What it does: refuses if the Unity editor is running (one batch run at a time); builds with Unity into Build/.staging
(a folder nothing ever runs from); writes version.json there (the commit, the branch, whether the tree had uncommitted
changes, the time, the label given); then, for each target asked for, refuses if any process is running from that
folder (never overwrite a game in use, never kill it), keeps the folder that was there as <target>.previous (one step
back), and moves the staging folder into place (the second target gets a copy). The player logs the version at start
(BuildInfo) and the run log's header carries it.

Usage, from the repository root:
    python Tools/build/install.py --into harness [--label "FP.2"]
    python Tools/build/install.py --into player                 (refused while the owner's game runs from Build/Player)
    python Tools/build/install.py --into both
    python Tools/build/install.py --show                        (what is installed where)
    python Tools/build/install.py --into harness --from-staging (install the last build again, no Unity run)

Exit 0 when the build landed where asked; 1 with the reason when refused or failed.
"""
import argparse
import json
import os
import shutil
import subprocess
import sys
import time
from datetime import datetime, timezone
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "Tools" / "world"))
from wade import UNITY  # noqa: E402  (one owner of the editor's path, read from the project's own version file)

BUILD = ROOT / "Build"
STAGING = BUILD / ".staging"
STAGING_LOG = BUILD / ".staging-build.log"
TARGETS = {"player": BUILD / "Player", "harness": BUILD / "Harness"}
EXE = "EarthGame2.exe"
VERSION_FILE = "version.json"


def say(text):
    print(text, flush=True)


def git(*args):
    return subprocess.run(["git"] + list(args), cwd=ROOT, capture_output=True, text=True, encoding="utf-8", errors="replace").stdout.strip()


def unity_running():
    out = subprocess.run(["tasklist"], capture_output=True, text=True, encoding="utf-8", errors="replace").stdout
    return any(line.lower().startswith("unity.exe") for line in out.splitlines())


def processes_running_from(folder):
    """The names of processes whose exe lies under the folder, by the system's own process table."""
    script = ("Get-Process | Where-Object { $_.Path -and $_.Path.StartsWith('%s', [System.StringComparison]::OrdinalIgnoreCase) } "
              "| ForEach-Object { $_.ProcessName + ' (' + $_.Id + ')' }") % str(folder).replace("'", "''")
    out = subprocess.run(["powershell", "-NoProfile", "-NonInteractive", "-Command", script], capture_output=True, text=True, encoding="utf-8", errors="replace").stdout
    return [line.strip() for line in out.splitlines() if line.strip()]


def facts(label):
    commit = git("rev-parse", "HEAD")
    dirty = git("status", "--porcelain", "--untracked-files=no") != ""
    return {
        "format": "eg2.version",
        "version": 1,
        "label": label or "",
        "commit": commit,
        "commit_short": commit[:9],
        "branch": git("branch", "--show-current"),
        "dirty": dirty,
        "built_utc": datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"),
        "tool": "Tools/build/install.py",
    }


def build():
    if unity_running():
        raise RuntimeError("the Unity editor (or a batch run) is running; one at a time")
    if STAGING.exists():
        shutil.rmtree(STAGING)
    STAGING.mkdir(parents=True)
    command = [str(UNITY), "-batchmode", "-nographics", "-projectPath", str(ROOT / "Unity"), "-quit",
               "-executeMethod", "EarthGame.Editor.CIBuild.BuildWindows", "-buildOut", str(STAGING), "-logFile", str(STAGING_LOG)]
    say("building with Unity into %s (about three minutes)" % STAGING)
    started = time.time()
    code = subprocess.run(command, cwd=ROOT).returncode
    say("unity exit %d after %.0f s (log %s)" % (code, time.time() - started, STAGING_LOG))
    if code != 0 or not (STAGING / EXE).is_file():
        raise RuntimeError("the build did not land: unity exit %d, %s %s" % (code, STAGING / EXE, "present" if (STAGING / EXE).is_file() else "missing"))


def write_version(folder, label):
    v = facts(label)
    (folder / VERSION_FILE).write_text(json.dumps(v, indent=2) + "\n", encoding="utf-8")
    return v


def install(target_name, staged=STAGING, copy=False):
    target = TARGETS[target_name]
    running = processes_running_from(target)
    if running:
        raise RuntimeError("%s is in use by %s; close it and run again (the game is never killed, and never overwritten while it runs)" % (target, ", ".join(running)))
    previous = target.with_name(target.name + ".previous")
    if previous.exists():
        if processes_running_from(previous):
            raise RuntimeError("%s is in use; it is the step back and cannot be replaced while something runs from it" % previous)
        shutil.rmtree(previous)
    if target.exists():
        target.rename(previous)
    if copy:
        shutil.copytree(staged, target)
    else:
        staged.rename(target)
    return target


def show():
    for name, folder in TARGETS.items():
        path = folder / VERSION_FILE
        if path.is_file():
            v = json.loads(path.read_text(encoding="utf-8"))
            say("%-8s %s: %s %s%s built %s on %s" % (name, folder, v.get("label") or "(no label)", v.get("commit_short"), "+dirty" if v.get("dirty") else "", v.get("built_utc"), v.get("branch")))
        elif (folder / EXE).is_file():
            say("%-8s %s: a player with no %s (installed by hand)" % (name, folder, VERSION_FILE))
        else:
            say("%-8s %s: nothing installed" % (name, folder))


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--into", choices=["player", "harness", "both"], help="where the build goes")
    parser.add_argument("--label", default="", help="what to call the build: a slice's name")
    parser.add_argument("--from-staging", action="store_true", help="install what Build/.staging holds, without building")
    parser.add_argument("--show", action="store_true", help="say what is installed where")
    args = parser.parse_args()
    if args.show:
        show()
        return 0
    if not args.into:
        parser.error("say where it goes: --into player|harness|both, or --show")
    if args.from_staging:
        if not (STAGING / EXE).is_file():
            raise RuntimeError("nothing staged at %s" % STAGING)
    else:
        build()
    v = write_version(STAGING, args.label)
    targets = ["harness", "player"] if args.into == "both" else [args.into]
    first = None
    for name in targets:
        if first is None:
            first = install(name)
        else:
            install(name, staged=first, copy=True)
        say("installed %s: %s %s %s%s built %s" % (name, TARGETS[name], v["label"] or "(no label)", v["commit_short"], "+dirty" if v["dirty"] else "", v["built_utc"]))
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except (OSError, RuntimeError, subprocess.SubprocessError) as error:
        print("install FAILED: %s" % error, file=sys.stderr)
        sys.exit(1)
