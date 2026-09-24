#!/usr/bin/env python3
"""machine.py: the machine the project shares with William, and which copy of the project this is (M1.Bd, 2026-09-24).

The project is checked out three times on this machine: the main copy (the main session's, whose Build/Player William
plays), the side worker's and the sweep's. An untracked `.eg-copy.json` at a copy's root says which, for instance
{"copy": "side", "port_offset": 1000, "main_root": "C:/Users/willi/projects/EarthGame2"}; without one a copy is the main
one with no offset. Every port a tool opens is taken through `port()`, so two copies' runs never meet on one.

The machine is William's first. Its noise keeps him awake, so nothing heavy starts in the quiet hours (CANON ruling 47,
`refuse_in_quiet_hours`); his game runs from the main copy's Build/Player, and the sweep waits while it does; and a
session about to time a frame asks the sweep for quiet (`quiet()`), which the sweep gives by starting no step while the
request stands. Artefacts is the main copy's folder, joined into the others, so the requests and the sweep's state are
seen alike from every copy.

Nothing here judges a run; it says where a copy stands and what the machine is doing.
"""
import ctypes
import json
import os
import subprocess
import time
from contextlib import contextmanager
from datetime import datetime
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
COPY_FILE = ROOT / ".eg-copy.json"
# The quiet hours (CANON ruling 47): the agents' default, 22:00 to 08:00 local, until William gives his own.
QUIET_HOURS = (22, 8)
QUIET_OK = "EG_QUIET_HOURS_OK"
SWEEPS = ROOT / "Artefacts" / "sweeps"
QUIET = SWEEPS / "quiet"
STATE = SWEEPS / "state.json"
# A request older than this is a process that died with its file left behind and its number since given to another.
REQUEST_LIFE_S = 2 * 3600


def _copy():
    try:
        return json.loads(COPY_FILE.read_text(encoding="utf-8"))
    except FileNotFoundError:
        return {}


def name():
    """The copy's name: main, side or sweep."""
    return _copy().get("copy", "main")


def is_main():
    return name() == "main"


def port(base):
    """A tool's port in this copy: the tool's own and the copy's offset."""
    return int(base) + int(_copy().get("port_offset", 0))


def tag():
    """What a run folder of this copy carries after its stamp: nothing in the main copy, '-side' or '-sweep' in the others.
    The copies share one Artefacts, and on 2026-09-24 two drink runs started in the same second from the main and side copies
    named one folder; the second refused, finding it there."""
    return "" if is_main() else "-" + name()


def main_root():
    """The main copy's root, whose Build/Player is William's."""
    return Path(_copy().get("main_root", str(ROOT)))


def in_quiet_hours(now=None):
    hour = (now or datetime.now()).hour
    start, end = QUIET_HOURS
    return (hour >= start or hour < end) if start > end else (start <= hour < end)


def refuse_in_quiet_hours(what, now=None):
    """Ends a tool before it starts anything, in the quiet hours, unless the session was asked for a run then."""
    if in_quiet_hours(now) and os.environ.get(QUIET_OK) != "1":
        raise SystemExit("%s: not in the quiet hours, %02d:00 to %02d:00 (CANON ruling 47); %s=1 only when William asks for a run now"
                         % (what, QUIET_HOURS[0], QUIET_HOURS[1], QUIET_OK))


def unity_holds(root=ROOT):
    """Whether a Unity editor or batch run has this copy's project open: Unity keeps Temp/UnityLockfile while it does, and
    a Unity on another copy's project is no reason to wait (before 2026-09-24 any Unity on the machine was)."""
    return (Path(root) / "Unity" / "Temp" / "UnityLockfile").exists()


def processes_from(folder):
    """The processes whose exe lies under the folder, by the system's own process table, as 'name (id)'."""
    script = ("Get-Process | Where-Object { $_.Path -and $_.Path.StartsWith('%s', [System.StringComparison]::OrdinalIgnoreCase) } "
              "| ForEach-Object { $_.ProcessName + ' (' + $_.Id + ')' }") % str(folder).replace("'", "''")
    out = subprocess.run(["powershell", "-NoProfile", "-NonInteractive", "-Command", script],
                         capture_output=True, text=True, encoding="utf-8", errors="replace").stdout
    return [line.strip() for line in out.splitlines() if line.strip()]


def his_game():
    """William's game, while it runs: the processes from the main copy's Build/Player."""
    return processes_from(main_root() / "Build" / "Player")


class _MemoryStatus(ctypes.Structure):
    _fields_ = [("dwLength", ctypes.c_ulong), ("dwMemoryLoad", ctypes.c_ulong),
                ("ullTotalPhys", ctypes.c_ulonglong), ("ullAvailPhys", ctypes.c_ulonglong),
                ("ullTotalPageFile", ctypes.c_ulonglong), ("ullAvailPageFile", ctypes.c_ulonglong),
                ("ullTotalVirtual", ctypes.c_ulonglong), ("ullAvailVirtual", ctypes.c_ulonglong),
                ("ullAvailExtendedVirtual", ctypes.c_ulonglong)]


def free_memory_gb():
    """The machine's physical memory free now, GB, as Windows counts it."""
    status = _MemoryStatus()
    status.dwLength = ctypes.sizeof(_MemoryStatus)
    ctypes.windll.kernel32.GlobalMemoryStatusEx(ctypes.byref(status))
    return status.ullAvailPhys / 2 ** 30


_KERNEL = ctypes.windll.kernel32
_KERNEL.OpenProcess.restype = ctypes.c_void_p
_KERNEL.OpenProcess.argtypes = [ctypes.c_ulong, ctypes.c_int, ctypes.c_ulong]
_KERNEL.GetExitCodeProcess.argtypes = [ctypes.c_void_p, ctypes.POINTER(ctypes.c_ulong)]
_KERNEL.CloseHandle.argtypes = [ctypes.c_void_p]


def alive(pid):
    """Whether a process lives, by its handle's exit code. Never os.kill: on Windows it ends the process it asks about."""
    process_query_limited_information, still_active = 0x1000, 259
    handle = _KERNEL.OpenProcess(process_query_limited_information, 0, int(pid or 0))
    if not handle:
        return False
    try:
        code = ctypes.c_ulong()
        return bool(_KERNEL.GetExitCodeProcess(handle, ctypes.byref(code))) and code.value == still_active
    finally:
        _KERNEL.CloseHandle(handle)


def quiet_requests():
    """The requests for quiet that stand: each one's process still runs, and it is not older than a request can be."""
    standing = []
    for path in sorted(QUIET.glob("*.json")) if QUIET.is_dir() else []:
        try:
            request = json.loads(path.read_text(encoding="utf-8"))
        except (OSError, ValueError):
            continue
        if alive(request.get("pid")) and time.time() - float(request.get("since", 0)) < REQUEST_LIFE_S:
            standing.append(request)
    return standing


def sweep_state():
    """What the sweep is doing, from the state it writes, or {} when no sweep's process runs."""
    try:
        state = json.loads(STATE.read_text(encoding="utf-8"))
    except (OSError, ValueError):
        return {}
    return state if alive(state.get("pid")) else {}


@contextmanager
def quiet(who, reason, wait_s=2400):
    """Asks the sweep for quiet while the block runs: the sweep finishes the step in hand and starts no other until the
    request is gone. Waits for that step before the block, at most wait_s (the longest step, the soak, is half an hour), and
    says so when the sweep is still running one by then."""
    QUIET.mkdir(parents=True, exist_ok=True)
    mine = QUIET / ("%s-%d.json" % (who, os.getpid()))
    mine.write_text(json.dumps({"who": who, "pid": os.getpid(), "copy": name(), "reason": reason, "since": time.time()}),
                    encoding="utf-8")
    try:
        deadline = time.time() + wait_s
        state = sweep_state()
        if state.get("state") == "running":
            print("%s: asked the sweep for quiet; waiting for its step %s, running since %s, to finish"
                  % (who, state.get("step"), state.get("since")), flush=True)
        while sweep_state().get("state") == "running" and time.time() < deadline:
            time.sleep(2)
        if sweep_state().get("state") == "running":
            print("%s: the sweep did not stop within %d s; what follows is measured under it" % (who, wait_s), flush=True)
        yield
    finally:
        try:
            mine.unlink()
        except FileNotFoundError:
            pass


if __name__ == "__main__":
    print("copy %s (ports +%d), main at %s" % (name(), port(0), main_root()))
    print("quiet hours %02d:00 to %02d:00: %s now" % (QUIET_HOURS[0], QUIET_HOURS[1], "inside" if in_quiet_hours() else "outside"))
    print("Unity holds this copy's project: %s; William's game: %s" % (unity_holds(), ", ".join(his_game()) or "not running"))
    print("free memory %.1f GB; quiet requests %d; sweep %s" % (free_memory_gb(), len(quiet_requests()), sweep_state().get("state", "not running")))
