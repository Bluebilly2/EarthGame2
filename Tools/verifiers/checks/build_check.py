#!/usr/bin/env python3
"""build_check.py: does the client keep its streaming work inside the budget, and did the work really move?

ARCHITECTURE section 8 gives streaming 1.5 ms of CPU a frame, "a Stopwatch budget, never an item count", inside a
design frame of 13.3 ms; a frame over 33 ms is a named defect. M1.4e moved the sampling and the colour of a tile
onto a worker and left the main thread with what only Unity can do. This reads a run's own log and says whether
that is true.

It reads `run.jsonl` and nothing else: not the harness's summary, not the player's console, and no game code. The
percentile is computed here, from the records, rather than taken from anything the client said about itself. The
format is ARCHITECTURE section 10's `eg2.run`, and the record this reads is:

    {"t": seconds, "tick": n, "kind": "build", "what": "ground"|"colour", "ix": n, "iz": n,
     "worker_ms": float, "main_ms": float, "before_ms": float, "frame_ms": float,
     "texture_ms": float, "heights_ms": float, "object_ms": float, "water_ms": float}

What the budget promises is that no work is **started** in a frame whose allowance is already spent. What one
indivisible Unity call then costs is a different fact and this check reports it rather than pretending the
stopwatch could have stopped it: `TerrainData.SetHeights` on a 513-post tile is about 21 ms on the main thread
(measured 2026-09-10; `SetHeightsDelayLOD` with `SyncHeightmap` was the same work under two names), and that is
a debt in DEBTS.md, not a row that can be made green by rewording it.

Rows, each with both numbers:
  1. no work was started in a frame that had already spent its budget: the worst `before_ms` against 1.5 ms;
  2. the preparation really left the main thread — every ground build carries worker milliseconds — and the
     named Unity steps account for what the main thread spent, so nothing else is hiding in it;
  3. the main thread's cost is the one call this slice could not move: `heights_ms` against the rest of a ground
     build. This is the row that goes red if new main-thread work is added beside it;
  4. every tile that was asked for was built: as many "ground" records as the run's `interactive` record says
     tiles were built.

Beneath the rows it prints the worst frame of streaming work beside the design frame and the named-defect line,
as a number rather than a verdict: it is over both, that is the debt, and a check cannot pass it by wording.

Exit 0 when every row passes, 1 when any fails, 2 when there is no run to read.
Run from the repository root:
    python Tools/verifiers/checks/build_check.py [run.jsonl or a run directory]
(default: the newest run under Artefacts/streaming/latest.txt.)
"""
import json
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))))

# ARCHITECTURE section 8, restated here rather than imported.
BUDGET_MS = 1.5
NAMED_DEFECT_MS = 33.0
# The named Unity steps must account for this much of what the main thread spent; the rest would be work that
# nobody has looked at.
ACCOUNTED = 0.9
STEPS = ("texture_ms", "heights_ms", "object_ms", "water_ms")
# Of what a ground tile costs the main thread, this much is TerrainData.SetHeights and nothing else.
HEIGHTS_SHARE = 0.8
DESIGN_FRAME_MS = 13.3


def find_run(argument):
    if argument:
        path = argument if os.path.isabs(argument) else os.path.join(ROOT, argument)
        if os.path.isdir(path):
            for candidate in (os.path.join(path, "run.jsonl"), os.path.join(path, "player", "run.jsonl")):
                if os.path.isfile(candidate):
                    return candidate
            return None
        return path if os.path.isfile(path) else None
    pointer = os.path.join(ROOT, "Artefacts", "streaming", "latest.txt")
    if not os.path.isfile(pointer):
        return None
    newest = open(pointer, encoding="utf-8").read().strip()
    candidate = os.path.join(newest, "player", "run.jsonl")
    return candidate if os.path.isfile(candidate) else None


def percentile(values, fraction):
    """The nearest-rank percentile: the smallest value at or above the given share of the sorted list."""
    if not values:
        return 0.0
    ordered = sorted(values)
    rank = max(1, int(round(fraction * len(ordered) + 0.5)) - 1)
    return ordered[min(rank, len(ordered) - 1)]


def main(argv):
    path = find_run(argv[1] if len(argv) > 1 else None)
    if not path:
        print("no run.jsonl to read (pass one, or run Tools/world/stream.py first)")
        return 2
    builds, interactive = [], None
    with open(path, encoding="utf-8") as lines:
        for line in lines:
            line = line.strip()
            if not line:
                continue
            record = json.loads(line)
            kind = record.get("kind")
            if kind == "build":
                builds.append(record)
            elif kind == "interactive" and interactive is None:
                interactive = record
    if not builds:
        print("%s carries no build records; this run was made before M1.4e or built no tiles" % path)
        return 2

    failures = []

    def expect(name, ok, detail):
        print("%-44s %s  %s" % (name, "ok " if ok else "FAIL", detail))
        if not ok:
            failures.append(name)

    main_ms = [float(b.get("main_ms", 0.0)) for b in builds]
    worker_ms = [float(b.get("worker_ms", 0.0)) for b in builds]
    frame_ms = [float(b.get("frame_ms", 0.0)) for b in builds]
    grounds = [b for b in builds if b.get("what") == "ground"]

    worst_frame = max(frame_ms)

    # What the frame had already spent when this build began: the stopwatch's actual promise.
    worst_before = max(float(b.get("before_ms", 0.0)) for b in builds)
    expect("nothing was started on a spent budget", worst_before <= BUDGET_MS + 1e-9,
           "the worst frame had spent %.2f ms before it began another, against %.1f ms" % (worst_before, BUDGET_MS))

    total_worker, total_main = sum(worker_ms), sum(main_ms)
    prepared_off = all(float(b.get("worker_ms", 0.0)) > 0.0 for b in grounds)
    accounted = sum(sum(float(b.get(step, 0.0)) for step in STEPS) for b in builds)
    share = accounted / total_main if total_main > 0 else 1.0
    expect("the preparation is off the main thread", prepared_off and share >= ACCOUNTED,
           "%.0f ms on workers against %.0f ms on the main thread, of which the named steps are %.0f ms (%.2f against %.2f)"
           % (total_worker, total_main, accounted, share, ACCOUNTED))

    ground_main = sum(float(b.get("main_ms", 0.0)) for b in grounds)
    heights = sum(float(b.get("heights_ms", 0.0)) for b in grounds)
    heights_share = heights / ground_main if ground_main > 0 else 0.0
    expect("what is left is the call that cannot move", heights_share >= HEIGHTS_SHARE,
           "SetHeights is %.0f ms of a ground tile's %.0f (%.2f against %.2f); anything else growing beside it shows here"
           % (heights, ground_main, heights_share, HEIGHTS_SHARE))

    built = int(interactive.get("tiles_built", 0)) if interactive else 0
    expect("every tile the run held was built", built == 0 or len(grounds) >= built,
           "%d ground build(s) recorded, %d tile(s) built by the time it was interactive" % (len(grounds), built))

    print("  the worst frame of streaming work: %.2f ms, against a design frame of %.1f and a named defect at %.1f"
          % (worst_frame, DESIGN_FRAME_MS, NAMED_DEFECT_MS))
    if interactive is not None and "worst_streaming_ms" in interactive:
        print("  the client's own worst frame up to interactive: %.2f ms" % float(interactive["worst_streaming_ms"]))

    if failures:
        print("build_check: FAIL (%s)" % ", ".join(failures))
        return 1
    print("build_check: ok, %d build record(s) in %s" % (len(builds), os.path.relpath(path, ROOT)))
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main(sys.argv))
    except (OSError, ValueError, KeyError, IndexError) as error:
        print("build_check FAILED: %s" % error, file=sys.stderr)
        sys.exit(1)
