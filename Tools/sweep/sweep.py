#!/usr/bin/env python3
"""sweep.py: every check the project has, run on origin/main in the sweep's own copy, in the daytime (M1.Bd, 2026-09-24).

Nothing ran the whole set of checks unless a slice asked, so a check that turned red between slices waited for the next
slice that happened to run it. The sweep runs them all on the pushed code: the engine suite, the Unity-shaped compile, the
dedicated host, the edit-mode tests and the build into its own Build/Harness; fresh worlds of Bherwerre, the 8 km valley
and the whole valley; the verifiers on those worlds and those that need none; every scenario in the built game with the
check of its records; the corpus a scenario at a time with its checks; and the vantages' frames of the three worlds.

It runs in the copy whose `.eg-copy.json` names it `sweep` (`EarthGame2-sweep`), which no one edits, and brings it to
origin/main with a fast-forward pull, which refuses rather than overwrites. It gives way before each step (CANON ruling 47
and `machine.py`): it waits through the quiet hours, while William's game runs from the main copy's Build/Player, while a
session's request for quiet stands, and while the machine has less free memory than the step needs. It never ends a step
part-way. A failed step does not stop the rest; a step whose build or world failed is skipped and says why.

Each sweep leaves Artefacts/sweeps/<stamp>/: report.md and report.json, rewritten after every step so a sweep cut short
still says what it found; steps/<nn>-<name>.log; runs/, where every scenario, corpus and frame run of the sweep lands;
worlds/. Artefacts/sweeps/latest.json names the last sweep and its verdicts, and the report puts first the steps that were
green in the sweep before and are red in this one.

Claude's tool: it runs the project's own tools and checks and reports their exit codes; it judges nothing itself.

Usage, from the sweep copy's root, in the daytime:
    python Tools/sweep/sweep.py [--force] [--only name,name] [--list]
--force sweeps a commit already swept; --only runs the named steps and what they need; --list prints the steps.
Exit 0 when every step that ran exited 0 (or with an exit NO_VERDICT names as no verdict); 1 when any was red or skipped; 2 when the sweep did not start (not the sweep's copy, the quiet hours, another sweep running, the pull refused).
"""
import argparse
import json
import os
import subprocess
import sys
import time
from datetime import datetime, timedelta, timezone
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "Tools" / "world"))
import machine  # noqa: E402  (which copy this is, the quiet hours, William's game, the requests for quiet)

LATEST = machine.SWEEPS / "latest.json"
VENV = ROOT / "Tools" / ".venv" / "Scripts" / "python.exe"
PYTHON = str(VENV) if VENV.is_file() else sys.executable
BELOW_NORMAL = 0x00004000
NO_WINDOW = 0x08000000
POLL_S = 15
STEP_TIMEOUT_S = 3 * 3600
CHECKS = "Tools/verifiers/checks/"
# A step's exit that its check states is no verdict, not red. The valleys' census exited 2 until it held their landmarks
# (2026-09-24); a 2 from it now is a missing layer, and red.
NO_VERDICT = {}


class Step:
    def __init__(self, name, cmd, needs=(), memory_gb=4.0, makes=None):
        self.name, self.cmd, self.needs, self.memory_gb, self.makes = name, cmd, tuple(needs), memory_gb, makes


def steps(run, short):
    """The sweep's steps in order. A command is a list, or a function of the results so far for a check that reads the folder
    a scenario made."""
    worlds, runs = run / "worlds", run / "runs"
    gate, valley, whole = worlds / "gate", worlds / "valley", worlds / "valley-whole"
    py = lambda script, *args: [PYTHON, script] + [str(a) for a in args]
    made = lambda step: (lambda results: results[step]["folder"])
    s = [
        Step("engine-build", ["dotnet", "build", "Engine/EarthGame.slnx", "-c", "Debug", "--nologo", "-v", "minimal"]),
        Step("engine-tests", ["dotnet", "test", "Engine/EarthGame.slnx", "-c", "Debug", "--nologo", "--no-build"], ["engine-build"]),
        Step("unity-compile", ["dotnet", "build", "Engine/EarthGame.Unity.Compile.csproj", "-c", "Debug", "--nologo", "-v", "minimal"]),
        Step("host", ["dotnet", "build", "Engine/tools/EarthGame.ServerHost", "-c", "Release", "--nologo", "-v", "minimal"]),
        Step("editmode", py("Tools/unity/editmode.py")),
        Step("build", py("Tools/build/install.py", "--into", "harness", "--label", "sweep-" + short)),
        Step("world-gate", py("Tools/world/create.py", gate), ["host"]),
        Step("populate-gate", py("Tools/world/populate.py", gate), ["world-gate"]),
        Step("world-valley", py("Tools/world/create.py", valley, "--region", "kangaroo-valley"), ["host"], 6.0),
        Step("world-whole", py("Tools/world/create.py", whole, "--region", "kangaroo-valley-whole"), ["host"], 12.0),
    ]
    for tag, world, needs in (("gate", gate, "populate-gate"), ("valley", valley, "world-valley"), ("whole", whole, "world-whole")):
        s += [Step("%s-%s" % (check, tag), py(CHECKS + "%s_check.py" % check, world), [needs], 8.0 if tag == "whole" else 4.0)
              for check in ("save", "cover", "stand", "drainage", "species", "census")]
        s.append(Step("region-stats-%s" % tag, py(CHECKS + "region_stats.py", "--world", world), [needs]))
    s += [Step(check.replace("_", "-"), py(CHECKS + check + ".py")) for check in
          ("solar_check", "decode_check", "summit_check", "atlas_check", "world_inputs_check", "world_save_integrity_check",
           "atlas_probe_check", "weather_check")]
    play = ["build", "host", "populate-gate"]
    s += [
        Step("drink", py("Tools/world/drink.py", "--world", gate), play, makes="drink-"),
        Step("thirst-check", lambda r: py(CHECKS + "thirst_check.py", made("drink")(r)), ["drink"]),
        Step("night", py("Tools/world/night.py", "--world", gate), play, makes="night-"),
        Step("cold-check", lambda r: py(CHECKS + "cold_check.py", made("night")(r)), ["night"]),
        Step("knap", py("Tools/world/knap.py", "--world", gate), play, makes="knap-"),
        Step("knap-check", lambda r: py(CHECKS + "knap_check.py", made("knap")(r)), ["knap"]),
        Step("wade", py("Tools/world/wade.py", "--world", gate), play, makes="wade-"),
        Step("swim", py("Tools/world/wade.py", "--world", gate, "--swim"), play, makes="swim-"),
        Step("dune", py("Tools/world/dune.py", "--world", gate), play, makes="dune-"),
        Step("dune-at-face", py("Tools/world/dune.py", "--world", gate, "--at-face"), play, makes="dune-"),
        Step("changes", py("Tools/world/changes.py", "--world", gate), play, makes="changes-"),
        Step("idle", py("Tools/world/idle.py", "--world", gate), play, makes="idle-"),
        Step("looks", py("Tools/world/looks.py", "--world", gate), play, makes="looks-"),
        Step("stream", py("Tools/world/stream.py", "--world", gate), play),
    ]
    s += [Step("carry-" + scenario, py("Tools/world/carry.py", "--world", gate, "--scenario", scenario), play, makes=scenario + "-")
          for scenario in ("carry", "litter", "controls", "trunk", "work")]
    for scenario, criterion in (("join", "N1"), ("walk", "N2"), ("rejoin", "N3"), ("soak", "N4")):
        out = runs / ("corpus-" + scenario)
        s += [Step("corpus-" + scenario, py("Tools/corpus/run.py", "--scenarios", scenario, "--out", out), ["build", "host"], 6.0),
              Step("join-check-" + scenario, py(CHECKS + "join_check.py", out, "--only", criterion), ["corpus-" + scenario])]
    # The animals' check reads a corpus's soak: its server logs the animals it stood up every ten seconds.
    s.append(Step("fauna-check", py(CHECKS + "fauna_check.py", runs / "corpus-soak"), ["corpus-soak"]))
    for tag, world, needs in (("gate", gate, "populate-gate"), ("valley", valley, "world-valley"), ("whole", whole, "world-whole")):
        s.append(Step("vantages-" + tag, py("Tools/world/vantages.py", "--world", world, "--out", runs / ("vantages-" + tag)),
                      ["build", "host", needs], 8.0 if tag == "whole" else 4.0))
    return s


def now_text():
    return datetime.now().strftime("%Y-%m-%d %H:%M:%S")


def write_state(run, commit, state, step=None, detail=None):
    machine.SWEEPS.mkdir(parents=True, exist_ok=True)
    machine.STATE.write_text(json.dumps({"pid": os.getpid(), "state": state, "step": step, "detail": detail, "since": now_text(),
                                         "commit": commit, "folder": str(run)}, indent=2), encoding="utf-8")


def hold_up(step, expected_s):
    """What keeps the next step from starting now, or None. A step starts only when it should end before the quiet hours
    begin, by how long it took in the sweep before (ten minutes when it has not run)."""
    if machine.in_quiet_hours():
        return "the quiet hours"
    if machine.in_quiet_hours(datetime.now() + timedelta(seconds=expected_s)):
        return "the quiet hours, which the step would run into (it took %d s before)" % expected_s
    game = machine.his_game()
    if game:
        return "William's game (%s)" % ", ".join(game)
    requests = machine.quiet_requests()
    if requests:
        return "a request for quiet (%s)" % ", ".join("%s: %s" % (r.get("who"), r.get("reason")) for r in requests)
    free = machine.free_memory_gb()
    if free < step.memory_gb:
        return "memory: %.1f GB free of the %.0f GB the step needs" % (free, step.memory_gb)
    return None


def wait_clear(run, commit, step, expected_s):
    waits, reason, started = [], hold_up(step, expected_s), time.time()
    while reason:
        write_state(run, commit, "waiting", step.name, reason)
        if not waits or waits[-1]["for"] != reason:
            print("%s  waiting before %s: %s" % (now_text(), step.name, reason), flush=True)
            waits.append({"for": reason, "from": now_text()})
        time.sleep(POLL_S)
        reason = hold_up(step, expected_s)
    return waits, time.time() - started


def run_step(run, commit, index, step, results, env, expected_s):
    record = {"name": step.name, "needs": list(step.needs)}
    missing = [n for n in step.needs if results.get(n, {}).get("verdict") != "green"]
    if missing:
        record.update(verdict="skipped", why="needs %s, which did not pass" % ", ".join(missing))
        return record
    waits, waited_s = wait_clear(run, commit, step, expected_s)
    cmd = step.cmd(results) if callable(step.cmd) else step.cmd
    record.update(cmd=[str(c) for c in cmd], waits=waits, waited_s=round(waited_s))
    log = run / "steps" / ("%02d-%s.log" % (index, step.name))
    log.parent.mkdir(parents=True, exist_ok=True)
    before = set(os.listdir(env["EG_RUNS"])) if os.path.isdir(env["EG_RUNS"]) else set()
    write_state(run, commit, "running", step.name)
    print("%s  %s: %s" % (now_text(), step.name, " ".join(record["cmd"])), flush=True)
    started = time.time()
    with open(log, "w", encoding="utf-8", errors="replace") as out:
        try:
            code = subprocess.run([str(c) for c in cmd], cwd=ROOT, env=env, stdin=subprocess.DEVNULL, stdout=out, stderr=subprocess.STDOUT,
                                  creationflags=BELOW_NORMAL | NO_WINDOW, timeout=STEP_TIMEOUT_S).returncode
        except subprocess.TimeoutExpired:
            code = None
    record.update(code=code, seconds=round(time.time() - started), log=str(log))
    if step.makes:
        after = set(os.listdir(env["EG_RUNS"])) if os.path.isdir(env["EG_RUNS"]) else set()
        new = sorted(n for n in after - before if n.startswith(step.makes))
        record["folder"] = str(Path(env["EG_RUNS"]) / new[-1]) if new else None
    if code == 0:
        record["verdict"] = "green"
    elif code is not None and NO_VERDICT.get(step.name) == code:
        record["verdict"] = "no verdict"
    else:
        record["verdict"] = "red"
        record["tail"] = log.read_text(encoding="utf-8", errors="replace").splitlines()[-12:]
    print("%s  %s: %s (exit %s, %d s)" % (now_text(), step.name, record["verdict"], code, record["seconds"]), flush=True)
    return record


def previous_report():
    try:
        latest = json.loads(LATEST.read_text(encoding="utf-8"))
        return json.loads((Path(latest["folder"]) / "report.json").read_text(encoding="utf-8"))
    except (OSError, ValueError, KeyError):
        return None


def write_report(run, commit, started, results, order, before, finished):
    verdicts = {name: results[name]["verdict"] for name in order if name in results}
    was = {r["name"]: r["verdict"] for r in (before or {}).get("steps", [])}
    newly_red = [n for n, v in verdicts.items() if v == "red" and was.get(n) == "green"]
    report = {"format": "eg2.sweep", "version": 1, "commit": commit, "copy": machine.name(), "started": started,
              "finished": now_text() if finished else None, "steps": [results[n] for n in order if n in results],
              "newly_red": newly_red, "before": (before or {}).get("commit")}
    (run / "report.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
    count = lambda v: sum(1 for x in verdicts.values() if x == v)
    lines = ["# Sweep of %s" % commit[:9], "",
             "Started %s, %s. %d green, %d red, %d skipped, %d without a verdict, of %d steps%s." % (
                 started, ("finished " + report["finished"]) if finished else "not finished", count("green"), count("red"),
                 count("skipped"), count("no verdict"), len(order), "" if finished else " (so far)"), ""]
    if newly_red:
        lines += ["**Red since the sweep of %s:** %s" % ((before or {}).get("commit", "?")[:9], ", ".join(newly_red)), ""]
    for verdict in ("red", "skipped", "no verdict", "green"):
        names = [n for n, v in verdicts.items() if v == verdict]
        if not names:
            continue
        lines += ["## %s" % verdict.capitalize(), ""]
        for n in names:
            r = results[n]
            waited = (" (waited %d s: %s)" % (r["waited_s"], "; ".join(w["for"] for w in r["waits"]))) if r.get("waits") else ""
            what = r.get("why") or ("exit %s in %s s%s" % (r.get("code"), r.get("seconds"), waited))
            lines.append("- `%s`: %s%s" % (n, what, ("; " + r["folder"]) if r.get("folder") else ""))
            for tail in r.get("tail", []) if verdict == "red" else []:
                lines.append("    " + tail)
        lines.append("")
    (run / "report.md").write_text("\n".join(lines), encoding="utf-8")
    if finished:
        LATEST.write_text(json.dumps({"folder": str(run), "commit": commit, "finished": report["finished"],
                                      "green": count("green"), "red": count("red"), "skipped": count("skipped"),
                                      "newly_red": newly_red}, indent=2), encoding="utf-8")


def git(*args):
    return subprocess.run(["git"] + list(args), cwd=ROOT, capture_output=True, text=True, encoding="utf-8", errors="replace")


def main(argv):
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--force", action="store_true", help="sweep a commit already swept")
    parser.add_argument("--only", default=None, help="the named steps (comma-separated) and the steps they need")
    parser.add_argument("--list", action="store_true", help="print the steps and stop")
    parser.add_argument("--pulled", action="store_true", help=argparse.SUPPRESS)
    args = parser.parse_args(argv)
    if args.list:
        for index, step in enumerate(steps(Path("<run>"), "<commit>"), 1):
            print("%2d %-28s needs %s" % (index, step.name, ", ".join(step.needs) or "nothing"))
        return 0
    if machine.name() != "sweep":
        print("sweep: this is the %s copy; the sweep runs in the copy whose .eg-copy.json names it sweep, which no one edits" % machine.name())
        return 2
    if machine.in_quiet_hours():
        print("sweep: the quiet hours (CANON ruling 47); a sweep starts in the daytime")
        return 2
    other = machine.sweep_state()
    if other:
        print("sweep: another sweep is running (process %s, %s %s)" % (other.get("pid"), other.get("state"), other.get("step")))
        return 2
    write_state(machine.SWEEPS, "", "starting")
    if not args.pulled:
        this = Path(__file__).read_bytes()
        pulled = git("pull", "--ff-only", "origin", "main")
        if pulled.returncode != 0:
            print("sweep: the fast-forward pull refused: %s" % (pulled.stderr or pulled.stdout).strip())
            return 2
        if Path(__file__).read_bytes() != this:
            # The pull brought a new sweep: it runs the sweep, not the one that pulled it.
            machine.STATE.write_text("{}", encoding="utf-8")
            return subprocess.call([sys.executable, __file__] + argv + ["--pulled"], cwd=ROOT)
    commit = git("rev-parse", "HEAD").stdout.strip()
    before = previous_report()
    if before and before.get("commit") == commit and before.get("finished") and not args.force and not args.only:
        print("sweep: %s was swept already (%s); --force to sweep it again" % (commit[:9], before.get("finished")))
        return 0

    started = now_text()
    run = machine.SWEEPS / datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%SZ")
    (run / "runs").mkdir(parents=True)
    env = dict(os.environ, EG_RUNS=str(run / "runs"), PYTHONUNBUFFERED="1")
    env.pop(machine.QUIET_OK, None)
    plan = steps(run, commit[:9])
    if args.only:
        wanted, by_name = set(), {s.name: s for s in plan}
        pending = [n.strip() for n in args.only.split(",") if n.strip()]
        while pending:
            n = pending.pop()
            if n not in by_name:
                print("sweep: no step %s; --list names them" % n)
                return 2
            if n not in wanted:
                wanted.add(n)
                pending.extend(by_name[n].needs)
        plan = [s for s in plan if s.name in wanted]
    order = [s.name for s in plan]
    print("sweep of %s into %s: %d steps" % (commit[:9], run, len(plan)), flush=True)
    took = {r["name"]: r.get("seconds") for r in (before or {}).get("steps", []) if r.get("seconds")}
    results = {}
    for index, step in enumerate(plan, 1):
        results[step.name] = run_step(run, commit, index, step, results, env, took.get(step.name, 600))
        write_report(run, commit, started, results, order, before, finished=False)
    write_report(run, commit, started, results, order, before, finished=True)
    write_state(run, commit, "finished")
    print((run / "report.md").read_text(encoding="utf-8").splitlines()[2], flush=True)
    print(run / "report.md")
    return 0 if all(r["verdict"] in ("green", "no verdict") for r in results.values()) else 1


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
