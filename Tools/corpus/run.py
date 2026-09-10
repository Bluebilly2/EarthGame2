#!/usr/bin/env python3
"""run.py: the M1.B corpus. A dedicated server and headless players per scenario, under the harness conditions.

Usage, from the repository root:
    python Tools/corpus/run.py [--scenarios join,walk,rejoin,soak] [--quick] [--out Artefacts/corpus/<stamp>]

What it runs (ARCHITECTURE §7.1; the conditions are CANON ruling 11's, not the owner's link):
  join-a    server + player A walking; player B joins 20 s later and exits at interactive.  100 ms RTT ±20, 2% loss, 10 Mbit/s.
  join-b    the same at 200 ms RTT ±40, 5% loss, 5 Mbit/s.
  walk      two players on the wake loop for ten minutes at 100 ms / 2%.
  walk-solo one player in SOLO (server and client in one process) for ten minutes: N2's second column.
  rejoin-held  A walks; B walks and is cut without a leave every 30 s for ten cycles; at each cut this harness
            pauses the server (a test control) and resumes it once B is interactive again, so the world digest
            is held across the rejoin (N3 held-tick).
  rejoin-live  the same with the server running through the cut (N3 live-tick).
  soak      two players looping for thirty minutes at 100 ms / 2% (N4).

Latency is applied on both sockets at half the round trip each way; loss on both sockets at the stated
percent, so a packet in either direction sees that loss. The send cap is applied to both ends.

Every scenario runs on its own copy of one world, created at the start of the run by Tools/world/create.py (the
gate's creation, seed 1347): the server continues its copy and a solo player loads its own, so every founder wakes
where the world's scorer chose and walks Routes.WakeLoop, which is laid around that wake. Until 2026-09-10 the
server ran on the bare bake and woke founders at the region's stated point; CANON ruling 20 withdrew the point and
real worlds had woken elsewhere since M1.2, so the corpus now runs what a player runs. A server is waited for until
its console says it is listening, since loading a world takes longer than the two seconds a bare bake did.

Every process's run.jsonl (eg2.run v1) lands under the output folder; this harness's own summary.json is
written beside them for a reader's convenience and is what join_check.py ignores by design. The exit code is
0 only when every player and the server exited 0 and every run.jsonl carries an end record newer than the
scenario's start (matched by mtime).
"""
import argparse
import datetime
import json
import os
import shutil
import subprocess
import sys
import threading
import time

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
PLAYER_EXE = os.path.join(ROOT, "Build", "Player", "EarthGame2.exe")
SERVER_DLL_RELEASE = os.path.join(ROOT, "Engine", ".build", "bin", "EarthGame.ServerHost", "Release", "net10.0", "EarthGame.ServerHost.dll")
SERVER_DLL_DEBUG = os.path.join(ROOT, "Engine", ".build", "bin", "EarthGame.ServerHost", "Debug", "net10.0", "EarthGame.ServerHost.dll")
BASE_PORT = 28115

# The harness conditions (CANON ruling 11): round trip, its jitter, loss per direction, cap in bytes per second.
CONDITIONS = {
    "a": {"rtt_ms": 100, "jitter_ms": 20, "loss_percent": 2, "cap_bytes_per_second": 1250000},
    "b": {"rtt_ms": 200, "jitter_ms": 40, "loss_percent": 5, "cap_bytes_per_second": 625000},
}


def log(line):
    print(datetime.datetime.now().strftime("%H:%M:%S") + "  " + line)
    sys.stdout.flush()


def server_dll():
    for candidate in (SERVER_DLL_RELEASE, SERVER_DLL_DEBUG):
        if os.path.isfile(candidate):
            return candidate
    return None


class Server:
    """The dedicated server as a child process with a console we can write to."""

    def __init__(self, out_dir, port, cond, seconds):
        os.makedirs(out_dir, exist_ok=True)
        self.dir = out_dir
        self.console_path = os.path.join(out_dir, "console.log")
        self.console = open(self.console_path, "w", encoding="utf-8")
        world = copy_world(os.path.join(out_dir, "world"))
        args = ["dotnet", server_dll(), "+server.world", world,
                "+server.port", str(port), "+server.log", os.path.join(out_dir, "run.jsonl"),
                "+server.simulate.latency", str(cond["rtt_ms"] // 2), "+server.simulate.jitter", str(cond["jitter_ms"] // 2),
                "+server.simulate.loss", str(cond["loss_percent"]), "+server.sendcap", str(cond["cap_bytes_per_second"]),
                "+server.seconds", str(seconds)]
        self.process = subprocess.Popen(args, cwd=ROOT, stdin=subprocess.PIPE, stdout=self.console, stderr=subprocess.STDOUT, text=True)
        self.started = time.time()
        self.wait_listening()

    def wait_listening(self, timeout=180.0):
        """Blocks until the console says the server is listening; a server that never does fails the scenario loudly."""
        deadline = time.time() + timeout
        while time.time() < deadline:
            if self.process.poll() is not None:
                raise RuntimeError("the server exited with %s before listening; see %s" % (self.process.returncode, self.console_path))
            try:
                with open(self.console_path, encoding="utf-8", errors="replace") as f:
                    if "listening on UDP" in f.read():
                        return
            except OSError:
                pass
            time.sleep(0.5)
        raise RuntimeError("the server was not listening after %.0f s; see %s" % (timeout, self.console_path))

    def command(self, text):
        try:
            self.process.stdin.write(text + "\n")
            self.process.stdin.flush()
        except (OSError, ValueError):
            pass

    def stop(self, grace=15.0):
        self.command("stop")
        try:
            self.process.wait(timeout=grace)
        except subprocess.TimeoutExpired:
            self.process.kill()
            self.process.wait()
        self.console.close()
        return self.process.returncode


WORLD_TEMPLATE = None       # set by main: the world this run created, copied for every scenario
WORLD_NAME = "world-1347"


def create_world(out):
    """The run's one world, made the way the gate makes it, so every scenario starts from the same country."""
    global WORLD_TEMPLATE
    folder = os.path.join(out, "world-template")
    code = subprocess.call([sys.executable, os.path.join(ROOT, "Tools", "world", "create.py"), folder], cwd=ROOT,
                           stdout=open(os.path.join(out, "create.log"), "w", encoding="utf-8"), stderr=subprocess.STDOUT)
    if code != 0 or not os.path.isfile(os.path.join(folder, "world.json")):
        raise RuntimeError("creating the corpus world failed (exit %s); see %s" % (code, os.path.join(out, "create.log")))
    WORLD_TEMPLATE = folder
    saved = json.load(open(os.path.join(folder, "world.json"), encoding="utf-8"))
    log("world created: wake at east %.0f north %.0f" % (saved.get("wake_east", float("nan")), saved.get("wake_north", float("nan"))))


def copy_world(dest):
    """A scenario's own copy of the run's world, so no scenario's saves leak into the next."""
    if WORLD_TEMPLATE is None:
        raise RuntimeError("no corpus world: create_world must run first")
    if os.path.isdir(dest):
        shutil.rmtree(dest)
    shutil.copytree(WORLD_TEMPLATE, dest)
    return dest


def start_player(out_dir, name, scenario, port, cond, mode="join", seconds=None, cycles=None):
    os.makedirs(out_dir, exist_ok=True)
    if mode == "solo":
        copy_world(os.path.join(out_dir, "saves", WORLD_NAME))
    args = [PLAYER_EXE, "-batchmode", "-nographics", "-logFile", os.path.join(out_dir, "player.log"),
            "-eg-mode", mode, "-eg-address", "127.0.0.1", "-eg-port", str(port), "-eg-name", name,
            "-eg-scenario", scenario, "-eg-record", out_dir,
            "-eg-saves", os.path.join(out_dir, "saves"), "-eg-tiles", os.path.join(out_dir, "tiles")]
    if mode == "solo":
        args += ["-eg-world", WORLD_NAME, "-eg-seed", "1347"]
    if cond is not None:
        args += ["-eg-latency", str(cond["rtt_ms"] // 2), "-eg-jitter", str(cond["jitter_ms"] // 2),
                 "-eg-loss", str(cond["loss_percent"]), "-eg-sendcap", str(cond["cap_bytes_per_second"])]
    if seconds:
        args += ["-eg-seconds", str(seconds)]
    if cycles:
        args += ["-eg-cycles", str(cycles)]
    return subprocess.Popen(args, cwd=ROOT)


def wait_all(processes, timeout):
    deadline = time.time() + timeout
    codes = {}
    for name, p in processes.items():
        remaining = max(1.0, deadline - time.time())
        try:
            codes[name] = p.wait(timeout=remaining)
        except subprocess.TimeoutExpired:
            log("  %s did not exit in time; killed" % name)
            p.kill()
            codes[name] = p.wait()
    return codes


def answered(run_jsonl, since):
    """True when the log exists, was written after the scenario began, and ends with an end record of exit 0."""
    if not os.path.isfile(run_jsonl) or os.path.getmtime(run_jsonl) < since:
        return False
    last = None
    with open(run_jsonl, encoding="utf-8") as f:
        for line in f:
            line = line.strip()
            if line:
                last = line
    try:
        record = json.loads(last) if last else None
    except json.JSONDecodeError:
        return False
    return record is not None and record.get("kind") == "end" and record.get("exit", 0) == 0


class Tail(threading.Thread):
    """Follows a player's run.jsonl and calls back with every new record: how the held-tick pause is timed."""

    def __init__(self, path, on_record):
        super().__init__(daemon=True)
        self.path = path
        self.on_record = on_record
        self.stop_flag = False

    def run(self):
        position = 0
        while not self.stop_flag:
            if os.path.isfile(self.path):
                with open(self.path, encoding="utf-8") as f:
                    f.seek(position)
                    for line in f:
                        if not line.endswith("\n"):
                            break
                        position += len(line.encode("utf-8"))
                        try:
                            self.on_record(json.loads(line))
                        except json.JSONDecodeError:
                            pass
            time.sleep(0.2)


def scenario_join(out, cond_name, quick, port):
    cond = CONDITIONS[cond_name]
    d = os.path.join(out, "join-" + cond_name)
    started = time.time()
    a_seconds = 45 if quick else 90
    server = Server(os.path.join(d, "server"), port, cond, a_seconds + 60)
    time.sleep(2.0)
    a = start_player(os.path.join(d, "A"), "William", "walk", port, cond, seconds=a_seconds)
    time.sleep(10.0 if quick else 20.0)
    b = start_player(os.path.join(d, "B"), "Guest", "join", port, cond)
    codes = wait_all({"A": a, "B": b}, a_seconds + 150)
    codes["server"] = server.stop()
    ok = all(c == 0 for c in codes.values()) and all(answered(os.path.join(d, who, "run.jsonl"), started) for who in ("A", "B", "server"))
    return {"dir": d, "codes": codes, "answered": ok, "seconds": time.time() - started}


def scenario_walk(out, quick, port):
    cond = CONDITIONS["a"]
    d = os.path.join(out, "walk")
    started = time.time()
    seconds = 60 if quick else None
    server = Server(os.path.join(d, "server"), port, cond, (seconds or 600) + 60)
    time.sleep(2.0)
    a = start_player(os.path.join(d, "A"), "William", "walk", port, cond, seconds=seconds)
    time.sleep(5.0)
    b = start_player(os.path.join(d, "B"), "Guest", "walk", port, cond, seconds=seconds)
    codes = wait_all({"A": a, "B": b}, (seconds or 600) + 120)
    codes["server"] = server.stop()
    ok = all(c == 0 for c in codes.values()) and all(answered(os.path.join(d, who, "run.jsonl"), started) for who in ("A", "B", "server"))
    return {"dir": d, "codes": codes, "answered": ok, "seconds": time.time() - started}


def scenario_walk_solo(out, quick, port):
    d = os.path.join(out, "walk-solo")
    started = time.time()
    seconds = 60 if quick else None
    a = start_player(os.path.join(d, "A"), "William", "walk", port + 1, None, mode="solo", seconds=seconds)
    codes = wait_all({"A": a}, (seconds or 600) + 120)
    ok = codes["A"] == 0 and answered(os.path.join(d, "A", "run.jsonl"), started)
    return {"dir": d, "codes": codes, "answered": ok, "seconds": time.time() - started}


def scenario_rejoin(out, held, quick, port):
    cond = CONDITIONS["a"]
    d = os.path.join(out, "rejoin-held" if held else "rejoin-live")
    started = time.time()
    cycles = 2 if quick else 10
    b_seconds = 45 + cycles * 30 + 15
    server = Server(os.path.join(d, "server"), port, cond, b_seconds + 120)
    time.sleep(2.0)
    a = start_player(os.path.join(d, "A"), "William", "walk", port, cond, seconds=b_seconds + 20)
    time.sleep(5.0)
    b_dir = os.path.join(d, "B")
    state = {"paused": False}

    def on_record(record):
        kind = record.get("kind")
        if not held:
            return
        if kind == "cut" and not state["paused"]:
            state["paused"] = True
            server.command("digest")
            server.command("pause")
            log("  cut %s: server paused" % record.get("cycle"))
        elif kind == "interactive" and record.get("rejoin") and state["paused"]:
            state["paused"] = False
            server.command("resume")
            server.command("digest")
            log("  rejoin interactive in %.2f s: server resumed" % record.get("since_connect_s", -1))

    tail = Tail(os.path.join(b_dir, "run.jsonl"), on_record)
    tail.start()
    b = start_player(b_dir, "Guest", "rejoin", port, cond, cycles=cycles)
    codes = wait_all({"A": a, "B": b}, b_seconds + 150)
    tail.stop_flag = True
    if state["paused"]:
        server.command("resume")
    codes["server"] = server.stop()
    ok = all(c == 0 for c in codes.values()) and all(answered(os.path.join(d, who, "run.jsonl"), started) for who in ("A", "B", "server"))
    return {"dir": d, "codes": codes, "answered": ok, "seconds": time.time() - started}


def scenario_soak(out, quick, port):
    cond = CONDITIONS["a"]
    d = os.path.join(out, "soak")
    started = time.time()
    seconds = 90 if quick else None
    server = Server(os.path.join(d, "server"), port, cond, (seconds or 1800) + 90)
    time.sleep(2.0)
    a = start_player(os.path.join(d, "A"), "William", "soak", port, cond, seconds=seconds)
    time.sleep(5.0)
    b = start_player(os.path.join(d, "B"), "Guest", "soak", port, cond, seconds=seconds)
    codes = wait_all({"A": a, "B": b}, (seconds or 1800) + 180)
    codes["server"] = server.stop()
    ok = all(c == 0 for c in codes.values()) and all(answered(os.path.join(d, who, "run.jsonl"), started) for who in ("A", "B", "server"))
    return {"dir": d, "codes": codes, "answered": ok, "seconds": time.time() - started}


def main(argv):
    global PLAYER_EXE
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--scenarios", default="join,walk,rejoin,soak", help="comma-separated subset of join,walk,rejoin,soak")
    parser.add_argument("--quick", action="store_true", help="short durations and two rejoin cycles: a smoke run, never the numbers")
    parser.add_argument("--out", default=None, help="output folder (default Artefacts/corpus/<stamp>)")
    parser.add_argument("--port", type=int, default=BASE_PORT)
    parser.add_argument("--player", default=PLAYER_EXE, help="the built player (default Build/Player/EarthGame2.exe)")
    args = parser.parse_args(argv[1:])

    PLAYER_EXE = os.path.abspath(args.player)
    if not os.path.isfile(PLAYER_EXE):
        log("no player at %s; build it with EarthGame.Editor.CIBuild.BuildWindows" % PLAYER_EXE)
        return 2
    if server_dll() is None:
        log("no server host; build it: dotnet build Engine/tools/EarthGame.ServerHost -c Release")
        return 2
    stamp = datetime.datetime.now().strftime("%Y%m%d-%H%M%S")
    out = os.path.abspath(args.out or os.path.join(ROOT, "Artefacts", "corpus", stamp + ("-quick" if args.quick else "")))
    os.makedirs(out, exist_ok=True)
    wanted = [s.strip() for s in args.scenarios.split(",") if s.strip()]
    log("corpus %s -> %s (%s)" % (",".join(wanted), out, "quick" if args.quick else "full"))
    create_world(out)

    results = {}
    port = args.port
    for name in wanted:
        log("== " + name)
        if name == "join":
            results["join-a"] = scenario_join(out, "a", args.quick, port)
            port += 2
            results["join-b"] = scenario_join(out, "b", args.quick, port)
        elif name == "walk":
            results["walk"] = scenario_walk(out, args.quick, port)
            port += 2
            results["walk-solo"] = scenario_walk_solo(out, args.quick, port)
        elif name == "rejoin":
            results["rejoin-held"] = scenario_rejoin(out, True, args.quick, port)
            port += 2
            results["rejoin-live"] = scenario_rejoin(out, False, args.quick, port)
        elif name == "soak":
            results["soak"] = scenario_soak(out, args.quick, port)
        else:
            log("unknown scenario '%s'" % name)
            return 2
        port += 2
        for key, r in results.items():
            if key.startswith(name):
                log("  %s: %s in %.0f s, exit codes %s" % (key, "answered" if r["answered"] else "NOT answered", r["seconds"], r["codes"]))

    summary = {"format": "eg2.corpus-summary", "version": 1, "out": out, "quick": args.quick, "conditions": CONDITIONS, "results": results,
               "written_utc": datetime.datetime.now(datetime.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")}
    with open(os.path.join(out, "summary.json"), "w", encoding="utf-8") as f:
        json.dump(summary, f, indent=2)
    all_ok = all(r["answered"] for r in results.values()) and len(results) > 0
    point_latest(out)
    log("corpus %s: %s" % (out, "every scenario answered" if all_ok else "NOT every scenario answered"))
    return 0 if all_ok else 1


def point_latest(out):
    """Artefacts/corpus/latest -> this run (a directory junction on Windows, a symlink elsewhere), for the gate."""
    latest = os.path.join(os.path.dirname(out), "latest")
    try:
        if os.path.islink(latest) or os.path.isdir(latest):
            os.rmdir(latest)
    except OSError:
        shutil.rmtree(latest, ignore_errors=True)
    try:
        if os.name == "nt":
            subprocess.run(["cmd", "/c", "mklink", "/J", latest, out], check=True, stdout=subprocess.DEVNULL)
        else:
            os.symlink(out, latest)
    except (OSError, subprocess.CalledProcessError) as ex:
        log("could not point %s at %s: %s" % (latest, out, ex))


if __name__ == "__main__":
    sys.exit(main(sys.argv))
