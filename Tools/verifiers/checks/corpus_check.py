#!/usr/bin/env python3
"""corpus_check.py: what the corpus's walkers lived through in their bodies, read from the run logs alone.

Since FP.1 and FP.2 (2026-09-15, 2026-09-16) a founder walking the corpus loop has a body: it loses water at the
exertion's rate on the world's clock, is told the word for it, dies at 15% lost and wakes again at the wake. Since
2026-09-16 the walker (`ScenarioRunner`) answers: a founder told thirsty looks for fresh water standing within 25 m in
the streamed tiles, walks to it and drinks through the use key until the word is gone, writing a `drink` record; a
death is a `died` record; every `sample` carries the water the server last told (`water`) and the word (`thirst`). This
check reads those records of the soak (the longest walk) and the server's own `death` records, and holds them to the
body's rules as FP.1 and FP.2 state them, restated here, never imported:

  1. the samples carry the body: every interactive sample of each walker carries `water`, and the water only falls
     between drinks (a rise without a `drink` record's Done between the two samples, and without a death, is a body
     nobody accounted for); both numbers: the samples, and the rises unaccounted for;
  2. the loss is the body's: the water lost per real second between the first and the last sample of each life
     (a life ends at a `died` record), against the resting loss (2.4 L a day of 42, at the world's seconds a real second
     the server's run header states: 86,400 over its day times its clock's rate, forty-eight for the corpus's servers and
     for a run from before the header said so) and the most a running body can lose (the resting loss, plus the breath's water above rest at 500 W in dry
     air by Fanger's latent term, about a litre and a half a day, plus the most sweat, 1.5 L an hour; the pricing of
     2026-09-16, restated); the rate must lie between the two, so a rate under rest is a body not being charged, and one
     over the most is one charged twice;
  3. a thirsty walker who came within reach drank: for each walker, when any sample's water is under the "thirsty"
     threshold (1.5% lost) while fresh water stood within 25 m of the sample's place by the world's own layers (the water
     class a creek, a stream or a lake and the surface at least 2 cm over the ground, read with numpy from the soak
     server's world folder, which the walker never reads), a `drink` record with a Done press follows within the lap;
     where no sample came within reach of standing fresh water, the row holds nothing and says how far the nearest was;
  4. every drink that was answered Done raised the water: `water_after` above `water_before` by about a litre and a half
     of forty-two a press (0.0357), less the seconds' loss between the two words, and never above full;
  5. every death of thirst is explained by the body: each server `death` record whose cause is thirst carries a water
     loss at or over the lethal 15%, and the walker wrote a `died` record of the same cause on the same server tick (each
     process counts its own seconds from its own start, so seconds cannot match them); the
     count is printed, so a soak in which founders die of thirst says so in its verdict line.

Independent of the game: the thresholds and rates are restated from the contracts; the water near the walk is found on
the world folder's layers with numpy (the game reads them cell by cell through its own heightfield); nothing here
imports the harness or the engine, and the harness's summary.json is not read.

Exit 0 when every row passes, 1 when any fails, 2 when the corpus cannot be read.
Run from the repository root:
    python Tools/verifiers/checks/corpus_check.py [corpus folder]
(default Artefacts/corpus/latest, whose soak/{server,A,B}/run.jsonl and soak/server/world are read.)
"""
import json
import math
import os
import sys

import numpy as np

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))))
DEFAULT_CORPUS = os.path.join("Artefacts", "corpus", "latest")
NP_DTYPES = {"u8": "u1", "u16": "<u2", "i16": "<i2", "u32": "<u4", "f32": "<f4"}

# The body (FP.1, FP.2, the pricing of 2026-09-16), restated.
TOTAL_BODY_WATER_L = 42.0
RESTING_LOSS_L_PER_DAY = 2.4
RESTING_BREATH_L_PER_DAY = 0.35    # the breath's share of the resting loss, already inside it
BASAL_W, WALKING_W, RUNNING_W = 80.0, 180.0, 420.0
LATENT_HEAT_J_PER_L = 2430000.0
MAX_SWEAT_L_PER_HOUR = 1.5


def breath_l_per_day(metabolic_w):
    """The most the breath can carry out at a metabolism, litres a day: Fanger's latent term in dry air (0.0173 * M * 5.87 W)."""
    return 0.0173 * metabolic_w * 5.87 * 86400.0 / LATENT_HEAT_J_PER_L
LETHAL_LOSS = 0.15
THIRSTY_AT = 0.015
DRINK_L = 1.5
# The walker (ScenarioRunner, 2026-09-16), restated.
SEARCH_M = 25.0
# The water layer's fresh classes (creek, stream, lake) and the depth at which water stands (WorldState.StandingWaterM).
FRESH = (3, 4, 5)
STANDING_WATER_M = 0.02
# A drink's rise: the seconds between the two words told can lose a little, and the word before may lag a step; a fifth
# of a press of slack covers both without letting a press pass that gave nothing.
RISE_PER_PRESS = DRINK_L / TOTAL_BODY_WATER_L
RISE_SLACK = 0.2 * RISE_PER_PRESS
DEATH_TOLD_WITHIN_TICKS = 20     # a second of ticks: the word is told on the death's own tick


class Rows:
    def __init__(self):
        self.failed = 0
        self.count = 0

    def row(self, name, ok, text):
        self.count += 1
        if not ok:
            self.failed += 1
        print("%-44s %s  %s" % (name, "PASS" if ok else "FAIL", text))


def records_of(path):
    out = []
    if not os.path.isfile(path):
        return None
    with open(path, encoding="utf-8") as f:
        for line in f:
            line = line.strip()
            if line:
                out.append(json.loads(line))
    return out


def kinds(records, kind):
    return [r for r in records if r.get("kind") == kind]


def layer(world, name):
    path = os.path.join(world, "layers", name + ".json")
    if not os.path.isfile(path):
        return None, None
    sidecar = json.load(open(path, encoding="utf-8"))
    raw = os.path.join(os.path.dirname(path), sidecar["raw"])
    return sidecar, np.fromfile(raw, dtype=NP_DTYPES[sidecar["dtype"]]).reshape(sidecar["height"], sidecar["width"])


def loss_per_real_second(world_per_real, activity_w, sweat_l_per_hour=0.0):
    """The fraction of body water lost a real second doing this much work above basal, plus a sweat rate: the resting
    loss, the breath above its resting share, and the sweat, at the run's world seconds a real second."""
    per_day = RESTING_LOSS_L_PER_DAY + max(0.0, breath_l_per_day(BASAL_W + activity_w) - RESTING_BREATH_L_PER_DAY) + sweat_l_per_hour * 24.0
    return per_day / TOTAL_BODY_WATER_L / 86400.0 * world_per_real


def world_seconds_per_real_second(log_path):
    """The world's seconds a real second on the run's server, as its run header states them: 86,400 over its day
    (real_seconds_per_day: a real day since CANON ruling 52) times the rate its clock was run at (clock_scale: 48 for the
    corpus's servers, the pace every corpus kept before). A run from before the header said either ran the thirty-minute
    day at the game's own rate: forty-eight."""
    header = {}
    with open(log_path, encoding="utf-8") as f:
        first = f.readline().strip()
        if first:
            record = json.loads(first)
            if record.get("format") == "eg2.run":
                header = record
    return 86400.0 / float(header.get("real_seconds_per_day", 1800.0)) * float(header.get("clock_scale", 1.0))


def lives(samples, deaths):
    """The walker's lives: runs of samples between deaths, each a list of samples with the water they carry."""
    marks = sorted(float(d["t"]) for d in deaths)
    out, current, k = [], [], 0
    for s in samples:
        t = float(s["t"])
        while k < len(marks) and t > marks[k]:
            if current:
                out.append(current)
            current, k = [], k + 1
        current.append(s)
    if current:
        out.append(current)
    return out


def main(argv):
    target = argv[1] if len(argv) > 1 else DEFAULT_CORPUS
    corpus = os.path.normpath(target if os.path.isabs(target) else os.path.join(ROOT, target))
    server_records = records_of(os.path.join(corpus, "soak", "server", "run.jsonl"))
    walkers = {who: records_of(os.path.join(corpus, "soak", who, "run.jsonl")) for who in ("A", "B")}
    if server_records is None or any(r is None for r in walkers.values()):
        print("corpus_check: no soak under %s (soak/server, soak/A and soak/B run.jsonl are needed)" % corpus)
        return 2
    world = os.path.join(corpus, "soak", "server", "world")
    world_per_real = world_seconds_per_real_second(os.path.join(corpus, "soak", "server", "run.jsonl"))
    print("corpus_check: %s (the world's clock at %g world seconds a real second, by the server's run header)" % (corpus, world_per_real))
    rows = Rows()

    # The standing fresh water of the world, for row 3, from the layers the walker never reads.
    side, water = layer(world, "water")
    _, surface = layer(world, "surface")
    _, heights = layer(world, "heights")
    fresh_east = fresh_north = None
    if water is not None and surface is not None and heights is not None:
        standing = np.isin(water, FRESH) & ((surface.astype(np.float64) - heights.astype(np.float64)) >= STANDING_WATER_M)
        rr, cc = np.nonzero(standing)
        cell, half = float(side["cell_m"]), float(side["extent_m"]) / 2.0
        fresh_east, fresh_north = cc * cell - half, half - rr * cell

    for who, records in sorted(walkers.items()):
        samples = [s for s in kinds(records, "sample") if s.get("interactive")]
        with_water = [s for s in samples if "water" in s]
        deaths = kinds(records, "died")
        drinks = kinds(records, "drink")
        done_at = [float(d["t"]) for d in drinks if int(d.get("drinks", 0)) > 0]

        # 1. The samples carry the body, and it only falls between drinks.
        name = "soak/%s samples carry the body" % who
        if not samples or len(with_water) != len(samples):
            rows.row(name, False, "%d of %d interactive samples carry water (a soak from before 2026-09-16 carries none)" % (len(with_water), len(samples)))
            continue
        unaccounted = 0
        for a, b in zip(with_water, with_water[1:]):
            if float(b["water"]) > float(a["water"]) + 1e-9:
                ta, tb = float(a["t"]), float(b["t"])
                if not any(ta <= t <= tb + 1.0 for t in done_at) and not any(ta <= float(d["t"]) <= tb for d in deaths):
                    unaccounted += 1
        rows.row(name, unaccounted == 0, "%d samples with water; %d rise(s) with no drink or death between the samples (must be 0)" % (len(with_water), unaccounted))

        # 2. The loss lies between the resting rate and the running rate with the most sweat.
        name = "soak/%s loss is the exertion's" % who
        rates = []
        for life in lives(with_water, deaths):
            # Between drinks only: a life is cut into stretches with no Done press inside.
            cuts = sorted(t for t in done_at if float(life[0]["t"]) <= t <= float(life[-1]["t"]))
            bounds = [float(life[0]["t"]) - 0.5] + cuts + [float(life[-1]["t"]) + 0.5]
            for lo, hi in zip(bounds, bounds[1:]):
                stretch = [s for s in life if lo + 1.5 <= float(s["t"]) <= hi - 1.5]
                if len(stretch) < 2 or float(stretch[-1]["t"]) - float(stretch[0]["t"]) < 60.0:
                    continue
                rates.append((float(stretch[0]["water"]) - float(stretch[-1]["water"])) / (float(stretch[-1]["t"]) - float(stretch[0]["t"])))
        rest, most = loss_per_real_second(world_per_real, 0.0), loss_per_real_second(world_per_real, RUNNING_W, MAX_SWEAT_L_PER_HOUR)
        if not rates:
            rows.row(name, False, "no stretch of a minute or more between drinks to measure")
        else:
            lo, hi = min(rates), max(rates)
            rows.row(name, lo >= rest * 0.98 and hi <= most * 1.02,
                     "%.3e to %.3e of the body a real second over %d stretch(es); rest %.3e <= rate <= running with the most sweat %.3e (walking would be %.3e, running %.3e)"
                     % (lo, hi, len(rates), rest, most, loss_per_real_second(world_per_real, WALKING_W), loss_per_real_second(world_per_real, RUNNING_W)))

        # 3. A thirsty walker who came within reach of standing fresh water drank.
        name = "soak/%s drank when thirsty by water" % who
        if fresh_east is None:
            rows.row(name, False, "the soak's world at %s has no water, surface or heights layer" % world)
        else:
            near, nearest = [], float("inf")
            for s in with_water:
                e, n = float(s["east"]), float(s["north"])
                if 1.0 - float(s["water"]) < THIRSTY_AT:
                    continue
                d = float(np.hypot(fresh_east - e, fresh_north - n).min()) if len(fresh_east) else float("inf")
                nearest = min(nearest, d)
                if d <= SEARCH_M:
                    near.append(float(s["t"]))
            if not near:
                rows.row(name, True, "no thirsty sample within %.0f m of standing fresh water: nothing held; the nearest standing fresh water to a thirsty walker was %s"
                         % (SEARCH_M, "%.0f m" % nearest if nearest < float("inf") else "nowhere (no thirsty sample, or no standing fresh water on the world)"))
            else:
                lap_s = 900.0
                answered = sum(1 for t in near if any(t <= d <= t + lap_s for d in done_at))
                rows.row(name, answered == len(near), "%d thirsty sample(s) within %.0f m of standing fresh water, %d followed by a drink within %.0f s (must be all); %d drink(s) recorded, %d with a Done press"
                         % (len(near), SEARCH_M, answered, lap_s, len(drinks), len(done_at)))

        # 4. Every drink answered Done raised the water by about a press's worth a press.
        name = "soak/%s drinks raised the water" % who
        done = [d for d in drinks if int(d.get("drinks", 0)) > 0]
        short = 0
        for d in done:
            rise = float(d.get("water_after", 0.0)) - float(d.get("water_before", 0.0))
            presses = int(d.get("drinks", 0))
            expected = min(presses * RISE_PER_PRESS, 1.0 - float(d.get("water_before", 0.0)))
            if rise < expected - RISE_SLACK or float(d.get("water_after", 0.0)) > 1.0 + 1e-9:
                short += 1
        if not done:
            rows.row(name, True, "no drink answered Done: nothing held (%d visit(s) to water recorded)" % len(drinks))
        else:
            rows.row(name, short == 0, "%d drink(s) answered Done, %d rose less than %.4f a press (less %.4f of slack) or over full (must be 0)" % (len(done), short, RISE_PER_PRESS, RISE_SLACK))

    # 5. Every death of thirst is explained by the body, and was told to the walker.
    server_deaths = [d for d in kinds(server_records, "death") if str(d.get("cause", "")).lower() in ("thirst", "2")]
    told = {who: kinds(records, "died") for who, records in walkers.items()}
    unexplained = untold = 0
    hours = []
    for d in server_deaths:
        if float(d.get("water_loss", 0.0)) < LETHAL_LOSS - 1e-9:
            unexplained += 1
        t = float(d["t"])
        # Each process counts its own seconds from its own start, so the server's death and the walker's word are matched by
        # the server's tick, which both carry: the walker writes the word on the tick the server told it.
        tick = int(d.get("tick", -1))
        if not any(abs(int(x.get("tick", -9)) - tick) <= DEATH_TOLD_WITHIN_TICKS and str(x.get("cause", "")).lower() == "thirst" for who in told for x in told[who]):
            untold += 1
        hours.append("%s at %.0f s (world %02d:%02d, %.1f%% lost)" % (d.get("name", "?"), t, int(float(d.get("local_hour", 0))) % 24, int(float(d.get("local_hour", 0)) * 60) % 60, 100.0 * float(d.get("water_loss", 0.0))))
    rows.row("soak deaths of thirst explained", unexplained == 0 and untold == 0,
             "%d death(s) of thirst on the server, %d under the lethal %.0f%% (must be 0), %d not told to the walker within %d ticks (must be 0)%s"
             % (len(server_deaths), unexplained, 100.0 * LETHAL_LOSS, untold, DEATH_TOLD_WITHIN_TICKS, ("; " + "; ".join(hours)) if hours else ""))

    print("corpus_check: %d row(s), %d failed" % (rows.count, rows.failed))
    return 0 if rows.failed == 0 and rows.count > 0 else 1


if __name__ == "__main__":
    sys.exit(main(sys.argv))
