#!/usr/bin/env python3
"""join_check.py: N1-N4 recomputed from the run logs alone.

Usage, from the repository root:  python Tools/verifiers/checks/join_check.py Artefacts/corpus/<stamp>

Reads every run.jsonl (format eg2.run, version 1; ARCHITECTURE §10) under the corpus folder that
Tools/corpus/run.py wrote, and recomputes each criterion of ARCHITECTURE §7.1 (CANON ruling 11) from the
records. Every row prints its number beside its threshold and its verdict; a row whose records are absent is a
FAIL that says so, never a skip. The exit code is the verdict: 0 when every row passes, 1 otherwise, 2 when the
folder cannot be read.

Independence, stated (CANON ruling 17): this file never imports the harness or the engine and never reads the
harness's summary.json; the thresholds are restated here from the ruling; the mirror error is computed against
the server's per-tick positions by this file's own linear interpolation; the held-tick digest comparison is a
string comparison of what the server wrote at 'pause' and at 'resume'.

What each row uses:
  N1  join-a/B and join-b/B: the 'interactive' record (rejoin false), since_connect_s.
  N2  walk/A, walk/B, walk-solo/A: 'correction' records over the run's duration ('end'.t), per named divergence
      segment (bank, shore) over the seconds spent in it ('sample' records), and the largest displacement_m.
  N3  rejoin-held/server: every 'pause' digest equals the following 'resume' digest; rejoin-held and rejoin-live:
      every 'interactive' with rejoin true has since_connect_s <= 3; after each rejoin, B's 'mirror' digests equal
      the server's 'bodies' digest of the same session at the same tick; the server's collected heap in the last
      'ticks' record minus the first is <= 5 MB.
  N4  soak: no 'exception' in any log and no 'dropped' the harness did not induce; server 'memory'
      working_set_bytes <= 1 GB; 'ticks' heap_collected_bytes at minute 30 <= 1.2 x minute 5; each client's
      'mirror' samples (once the mirror holds two states, so the delay has something to interpolate between;
      the first sample after a join holds one) within 0.5 m of the server's interpolated position in >= 99 %
      and 2.0 m in all; each
      two clients' 'sample'.remotes are equal in >= 99 % of the wall-clock seconds both were interactive (their
      headers' started_utc plus t) and never unequal for more than 2 consecutive seconds;
      sum(over_interval)/sum(count) of 'ticks' <= 0.001 with max p95_ms <= 5;
      server 'bandwidth'.sent averaged over seconds later than 15 s after the session's 'join' <= 20480 B/s.
"""
import datetime
import json
import math
import os
import sys

# The thresholds, restated from CANON ruling 11 / ARCHITECTURE §7.1.
N1_A_SECONDS = 10.0
N1_B_SECONDS = 20.0
N2_PER_MINUTE = 0.5
N2_DIVERGENCE_PER_MINUTE = 2.0
N2_MAX_DISPLACEMENT_M = 1.0
N2_DIVERGENCE_SEGMENTS = ("bank", "shore")
N3_REJOIN_SECONDS = 3.0
N3_HEAP_GROWTH_BYTES = 5 * 1024 * 1024
N4_WORKING_SET_BYTES = 1024 * 1024 * 1024
N4_HEAP_RATIO = 1.2
N4_MIRROR_NEAR_M = 0.5
N4_MIRROR_NEAR_FRACTION = 0.99
N4_MIRROR_FAR_M = 2.0
N4_REMOTES_FRACTION = 0.99
N4_REMOTES_MAX_DIVERGE_S = 2
N4_TICK_OVER_FRACTION = 0.001
N4_TICK_P95_MS = 5.0
N4_BANDWIDTH_BYTES_PER_SECOND = 20 * 1024
N4_JOIN_STREAM_SECONDS = 15.0


def read_log(path):
    header = None
    records = []
    with open(path, encoding="utf-8") as f:
        for line in f:
            line = line.strip()
            if not line:
                continue
            obj = json.loads(line)
            if header is None:
                header = obj
                if header.get("format") != "eg2.run" or header.get("version") != 1:
                    raise ValueError("%s: not an eg2.run v1 log (%s)" % (path, header))
            else:
                records.append(obj)
    return header, records


class Rows:
    def __init__(self):
        self.failed = 0
        self.count = 0

    def row(self, name, ok, text):
        self.count += 1
        if not ok:
            self.failed += 1
        print("%-34s %s  %s" % (name, "PASS" if ok else "FAIL", text))

    def missing(self, name, what):
        self.row(name, False, "missing: " + what)


def load(corpus, *parts):
    path = os.path.join(corpus, *parts, "run.jsonl")
    if not os.path.isfile(path):
        return None, None
    return read_log(path)


def kinds(records, kind):
    return [r for r in records if r.get("kind") == kind]


# ---------------------------------------------------------------- N1

def check_n1(corpus, rows):
    for cond, limit in (("a", N1_A_SECONDS), ("b", N1_B_SECONDS)):
        name = "N1 join-%s time-to-interactive" % cond
        header, records = load(corpus, "join-" + cond, "B")
        if records is None:
            rows.missing(name, "join-%s/B/run.jsonl" % cond)
            continue
        first = [r for r in kinds(records, "interactive") if not r.get("rejoin")]
        if not first:
            rows.missing(name, "no interactive record")
            continue
        seconds = float(first[0]["since_connect_s"])
        rows.row(name, seconds <= limit, "%.2f s <= %.0f s (tiles %s, %d bytes)" % (seconds, limit, first[0].get("tiles_held"), first[0].get("bytes_received", 0)))


# ---------------------------------------------------------------- N2

def check_n2_log(name, records, rows):
    ends = kinds(records, "end")
    if not ends:
        rows.missing(name, "no end record")
        return
    duration_min = float(ends[-1]["t"]) / 60.0
    corrections = kinds(records, "correction")
    per_minute = len(corrections) / duration_min if duration_min > 0 else float("inf")
    rows.row(name + " per minute", per_minute <= N2_PER_MINUTE, "%d corrections in %.1f min = %.3f/min <= %.1f" % (len(corrections), duration_min, per_minute, N2_PER_MINUTE))
    samples = kinds(records, "sample")
    divergence_seconds = sum(1 for s in samples if s.get("segment") in N2_DIVERGENCE_SEGMENTS)
    divergence_corrections = sum(1 for c in corrections if c.get("segment") in N2_DIVERGENCE_SEGMENTS)
    if divergence_seconds == 0:
        rows.row(name + " divergence", False, "the route never reached the bank or the shore")
    else:
        rate = divergence_corrections / (divergence_seconds / 60.0)
        rows.row(name + " divergence", rate <= N2_DIVERGENCE_PER_MINUTE, "%d in %.1f min on %s = %.3f/min <= %.1f" % (divergence_corrections, divergence_seconds / 60.0, "+".join(N2_DIVERGENCE_SEGMENTS), rate, N2_DIVERGENCE_PER_MINUTE))
    worst = max((float(c.get("displacement_m", 0.0)) for c in corrections), default=0.0)
    rows.row(name + " displacement", worst <= N2_MAX_DISPLACEMENT_M, "largest %.2f m <= %.1f m" % (worst, N2_MAX_DISPLACEMENT_M))


def check_n2(corpus, rows):
    for folder, who in (("walk", "A"), ("walk", "B"), ("walk-solo", "A")):
        name = "N2 %s/%s" % (folder, who)
        header, records = load(corpus, folder, who)
        if records is None:
            rows.missing(name, "%s/%s/run.jsonl" % (folder, who))
            continue
        check_n2_log(name, records, rows)


# ---------------------------------------------------------------- shared: the server's per-tick record

def server_ticks(records):
    """Per session: tick -> (digest, east, up, north) from the 'bodies' records."""
    by_session = {}
    for r in kinds(records, "bodies"):
        session = int(r["session"])
        start = int(r["from"])
        digests = r.get("digests", [])
        positions = r.get("positions", [])
        table = by_session.setdefault(session, {})
        for i, digest in enumerate(digests):
            pos = positions[3 * i:3 * i + 3] if len(positions) >= 3 * (i + 1) else (None, None, None)
            table[start + i] = (digest, pos[0], pos[1], pos[2])
    return by_session


def mirror_matches(client_records, ticks, after_t=0.0):
    """How many of the client's mirror digests (t >= after_t) equal the server's at the same tick."""
    total = 0
    equal = 0
    unmatched = 0
    for m in kinds(client_records, "mirror"):
        if float(m["t"]) < after_t:
            continue
        table = ticks.get(int(m["session"]))
        tick = int(m["latest_tick"])
        if table is None or tick not in table:
            unmatched += 1
            continue
        total += 1
        if table[tick][0] == m["digest"]:
            equal += 1
    return total, equal, unmatched


# ---------------------------------------------------------------- N3

def check_n3(corpus, rows):
    for variant in ("held", "live"):
        folder = "rejoin-" + variant
        sheader, srecords = load(corpus, folder, "server")
        bheader, brecords = load(corpus, folder, "B")
        if srecords is None or brecords is None:
            rows.missing("N3 %s" % folder, "%s/server or %s/B run.jsonl" % (folder, folder))
            continue
        if variant == "held":
            pauses = kinds(srecords, "pause")
            resumes = kinds(srecords, "resume")
            name = "N3 held digest before = after"
            if not pauses or len(pauses) != len(resumes):
                rows.row(name, False, "%d pause and %d resume records" % (len(pauses), len(resumes)))
            else:
                mismatches = [(p["digest"], r["digest"]) for p, r in zip(pauses, resumes) if p["digest"] != r["digest"]]
                rows.row(name, not mismatches, "%d cycle(s), %d mismatch(es); first pair %s / %s" % (len(pauses), len(mismatches), pauses[0]["digest"], resumes[0]["digest"]))
        rejoins = [r for r in kinds(brecords, "interactive") if r.get("rejoin")]
        cuts = kinds(brecords, "cut")
        name = "N3 %s rejoin interactive" % variant
        if not rejoins or len(rejoins) < len(cuts):
            rows.row(name, False, "%d cut(s), %d rejoin(s) interactive" % (len(cuts), len(rejoins)))
        else:
            worst = max(float(r["since_connect_s"]) for r in rejoins)
            rows.row(name, worst <= N3_REJOIN_SECONDS, "%d rejoin(s), slowest %.2f s <= %.0f s" % (len(rejoins), worst, N3_REJOIN_SECONDS))
        ticks = server_ticks(srecords)
        first_rejoin_t = float(rejoins[0]["t"]) if rejoins else float("inf")
        total, equal, unmatched = mirror_matches(brecords, ticks, first_rejoin_t)
        name = "N3 %s mirror = server digest" % variant
        rows.row(name, total > 0 and equal == total, "%d of %d mirror samples after the first rejoin equal the server's digest (%d at ticks the server did not log)" % (equal, total, unmatched))
        windows = kinds(srecords, "ticks")
        name = "N3 %s heap growth" % variant
        if len(windows) < 2:
            rows.row(name, False, "%d ticks window(s); two are needed" % len(windows))
        else:
            growth = int(windows[-1]["heap_collected_bytes"]) - int(windows[0]["heap_collected_bytes"])
            rows.row(name, growth <= N3_HEAP_GROWTH_BYTES, "%.2f MB over %d cycle(s) <= 5 MB (collected heap, first to last minute)" % (growth / 1048576.0, len(cuts)))


# ---------------------------------------------------------------- N4

def interpolate(table, at_tick):
    lo = int(math.floor(at_tick))
    hi = lo + 1
    if lo in table and hi in table and table[lo][1] is not None and table[hi][1] is not None:
        t = at_tick - lo
        a, b = table[lo], table[hi]
        return (a[1] + (b[1] - a[1]) * t, a[2] + (b[2] - a[2]) * t, a[3] + (b[3] - a[3]) * t)
    if lo in table and table[lo][1] is not None and abs(at_tick - lo) < 1e-9:
        return table[lo][1:]
    return None


def wall_seconds(started_utc):
    """A header's started_utc as seconds since the epoch, or None when it is absent or unreadable."""
    if not started_utc:
        return None
    try:
        return datetime.datetime.strptime(started_utc, "%Y-%m-%dT%H:%M:%SZ").replace(tzinfo=datetime.timezone.utc).timestamp()
    except ValueError:
        return None


def check_n4(corpus, rows):
    sheader, srecords = load(corpus, "soak", "server")
    aheader, arecords = load(corpus, "soak", "A")
    bheader, brecords = load(corpus, "soak", "B")
    if srecords is None or arecords is None or brecords is None:
        rows.missing("N4 soak", "soak/server, soak/A or soak/B run.jsonl")
        return
    clients = {"A": arecords, "B": brecords}

    exceptions = sum(len(kinds(r, "exception")) for r in (srecords, arecords, brecords))
    induced = sum(1 for who in clients for d in kinds(clients[who], "dropped") if d.get("severed"))
    uninduced = sum(1 for who in clients for d in kinds(clients[who], "dropped") if not d.get("severed"))
    ends = kinds(srecords, "end")
    end_t = float(ends[-1]["t"]) if ends else float("inf")
    early_leaves = [l for l in kinds(srecords, "leave") if float(l["t"]) < end_t - 60.0]
    rows.row("N4 crashes and disconnects", exceptions == 0 and uninduced == 0 and not early_leaves,
             "%d exception(s), %d uninduced drop(s), %d leave(s) before the last minute" % (exceptions, uninduced, len(early_leaves)))

    memory = kinds(srecords, "memory")
    if memory:
        peak = max(int(m["working_set_bytes"]) for m in memory)
        rows.row("N4 working set", 0 < peak <= N4_WORKING_SET_BYTES, "peak %.0f MB <= 1024 MB" % (peak / 1048576.0))
    else:
        rows.missing("N4 working set", "no memory records")
    windows = kinds(srecords, "ticks")
    name = "N4 heap minute 30 vs 5"
    at5 = [w for w in windows if 290.0 <= float(w["t"]) <= 330.0]
    at30 = [w for w in windows if 1790.0 <= float(w["t"]) <= 1830.0]
    if at5 and at30:
        h5 = int(at5[0]["heap_collected_bytes"])
        h30 = int(at30[-1]["heap_collected_bytes"])
        rows.row(name, h30 <= N4_HEAP_RATIO * h5, "%.2f MB at 30 min <= 1.2 x %.2f MB at 5 min" % (h30 / 1048576.0, h5 / 1048576.0))
    else:
        rows.row(name, False, "no ticks window at minute 5 (%d) or minute 30 (%d); a full soak is needed" % (len(at5), len(at30)))

    ticks = server_ticks(srecords)
    for who, records in clients.items():
        near = far = measured = unmeasured = unestablished = 0
        for m in kinds(records, "mirror"):
            if int(m.get("states_held", 2)) < 2:
                unestablished += 1
                continue
            table = ticks.get(int(m["session"]))
            truth = interpolate(table, float(m["at_tick"])) if table else None
            if truth is None:
                unmeasured += 1
                continue
            measured += 1
            d = math.sqrt((float(m["east"]) - truth[0]) ** 2 + (float(m["up"]) - truth[1]) ** 2 + (float(m["north"]) - truth[2]) ** 2)
            if d <= N4_MIRROR_NEAR_M:
                near += 1
            if d > N4_MIRROR_FAR_M:
                far += 1
        name = "N4 %s mirror error" % who
        if measured == 0:
            rows.row(name, False, "no mirror sample could be measured (%d unmeasured)" % unmeasured)
        else:
            fraction = near / measured
            rows.row(name, fraction >= N4_MIRROR_NEAR_FRACTION and far == 0,
                     "%.2f%% of %d samples within 0.5 m (>= 99%%), %d beyond 2.0 m (must be 0), %d unmeasured, %d before the mirror held two states" % (100.0 * fraction, measured, far, unmeasured, unestablished))

    # The remote counts, compared second by second on the wall clock while both clients were interactive.
    by_second = {}
    for who, header in (("A", aheader), ("B", bheader)):
        started = wall_seconds(header.get("started_utc"))
        table = {}
        if started is not None:
            for smp in kinds(clients[who], "sample"):
                if smp.get("interactive"):
                    table[int(started + float(smp["t"]))] = int(smp.get("remotes", 0))
        by_second[who] = table
    shared = sorted(set(by_second["A"]) & set(by_second["B"]))
    equal = sum(1 for sec in shared if by_second["A"][sec] == by_second["B"][sec])
    longest = run = 0
    previous = None
    for sec in shared:
        unequal = by_second["A"][sec] != by_second["B"][sec]
        run = run + 1 if unequal and previous is not None and sec == previous + 1 else (1 if unequal else 0)
        longest = max(longest, run)
        previous = sec
    name = "N4 remote counts equal"
    if not shared:
        rows.row(name, False, "no wall-clock second with both clients interactive (started_utc missing, or the runs never overlapped)")
    else:
        rows.row(name, equal / len(shared) >= N4_REMOTES_FRACTION and longest <= N4_REMOTES_MAX_DIVERGE_S,
                 "%.2f%% of %d shared seconds agree (>= 99%%), longest disagreement %d s <= 2 s" % (100.0 * equal / len(shared), len(shared), longest))

    if windows:
        count = sum(int(w["count"]) for w in windows)
        over = sum(int(w["over_interval"]) for w in windows)
        p95 = max(float(w["p95_ms"]) for w in windows)
        fraction = over / count if count else 1.0
        rows.row("N4 ticks", fraction <= N4_TICK_OVER_FRACTION and p95 <= N4_TICK_P95_MS,
                 "%d of %d updates over 50 ms = %.5f <= 0.001; worst per-minute p95 %.2f ms <= 5 ms" % (over, count, fraction, p95))
    else:
        rows.missing("N4 ticks", "no ticks records")

    joins = {int(j["session"]): float(j["t"]) for j in kinds(srecords, "join")}
    per_session = {}
    for b in kinds(srecords, "bandwidth"):
        session = int(b["session"])
        if session in joins and float(b["t"]) >= joins[session] + N4_JOIN_STREAM_SECONDS:
            per_session.setdefault(session, []).append(int(b["sent"]))
    if per_session:
        worst = max(sum(v) / len(v) for v in per_session.values())
        rows.row("N4 bandwidth", worst <= N4_BANDWIDTH_BYTES_PER_SECOND,
                 "worst session average %.0f B/s sent <= %d B/s outside the join stream (%s)" % (worst, N4_BANDWIDTH_BYTES_PER_SECOND,
                 ", ".join("session %d: %.0f B/s over %d s" % (s, sum(v) / len(v), len(v)) for s, v in sorted(per_session.items()))))
    else:
        rows.missing("N4 bandwidth", "no bandwidth records outside the join stream")


def main(argv):
    if len(argv) != 2:
        print("usage: join_check.py <corpus folder>", file=sys.stderr)
        return 2
    corpus = argv[1]
    if not os.path.isdir(corpus):
        print("not a folder: " + corpus, file=sys.stderr)
        return 2
    print("join_check: " + os.path.abspath(corpus))
    rows = Rows()
    try:
        check_n1(corpus, rows)
        check_n2(corpus, rows)
        check_n3(corpus, rows)
        check_n4(corpus, rows)
    except (OSError, ValueError, KeyError) as ex:
        print("join_check: cannot read the corpus: %r" % (ex,), file=sys.stderr)
        return 2
    print("join_check: %d row(s), %d failed" % (rows.count, rows.failed))
    return 0 if rows.failed == 0 and rows.count > 0 else 1


if __name__ == "__main__":
    sys.exit(main(sys.argv))
