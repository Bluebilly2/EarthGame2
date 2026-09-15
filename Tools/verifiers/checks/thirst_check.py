#!/usr/bin/env python3
"""thirst_check.py: the founder's thirst and drink (FP.1) as a drink run recorded them, held to the physiology restated here.

Reference: v1's BodyState (Assets/EarthGame/Sim/Body/BodyState.cs), whose numbers are published human physiology: 42 litres
of body water in a 70 kg adult, 2.4 litres a day lost at rest, the words at 1.5%, 4%, 7% and 11% lost, a litre and a half
absorbed a visit; and the game's clock, 1,800 real seconds to a day (WorldClock.RealSecondsPerDay), sped sixty times by
the scenario. Source: the run's run.jsonl (eg2.run, ARCHITECTURE section 10) alone, read with the standard library; every
number below is written here again, not read from the engine, so a slip in the engine is a red row.

Usage: python Tools/verifiers/checks/thirst_check.py Artefacts/frames/drink-<stamp>
Exit 0 when every row passes, 1 otherwise; prints each row's actual and required beside its verdict.
"""
import json
from pathlib import Path
import sys

BODY_WATER_L = 42.0
LOSS_L_PER_DAY = 2.4
REAL_SECONDS_PER_DAY = 1800.0
DRINK_L = 1.5
THRESHOLDS = ((0.11, 4), (0.07, 3), (0.04, 2), (0.015, 1))   # loss at or past which the word is: collapsing, failing, very thirsty, thirsty
RATE_TOLERANCE = 0.15
RATE_SETTLE_S = 2.0        # the sped window's first seconds, before the first word at the new rate lands
FRAMES = 6                 # three names at two sizes


def check(name, actual, required, good):
    print("%s %s: actual=%r; required=%r" % ("PASS" if good else "FAIL", name, actual, required), flush=True)
    return 0 if good else 1


def level_of(loss):
    for at, level in THRESHOLDS:
        if loss >= at - 1e-12:
            return level
    return 0


def slope(points):
    """Least squares of loss against real seconds."""
    n = len(points)
    if n < 3:
        return float("nan")
    mt = sum(t for t, _ in points) / n
    ml = sum(l for _, l in points) / n
    sxx = sum((t - mt) ** 2 for t, _ in points)
    return sum((t - mt) * (l - ml) for t, l in points) / sxx if sxx > 0 else float("nan")


def main(argv):
    if len(argv) != 2:
        print(__doc__)
        return 2
    run = Path(argv[1]) / "run.jsonl"
    if not run.is_file():
        print("no run.jsonl at %s" % run)
        return 2
    records = [json.loads(line) for line in run.read_text(encoding="utf-8").splitlines() if line.strip()]
    thirst = [r for r in records if r.get("kind") == "thirst"]
    drinks = [r for r in records if r.get("kind") == "drink"]
    end = next((r for r in reversed(records) if r.get("kind") == "end"), {})
    failures = 0

    # The rate: the loss's slope over the sped window, from the start until the first word past thirsty, less the first
    # seconds; the resting rate at sixty times the game's rate is 2.4 / 42 per day, a day every thirty real seconds.
    scale = float(end.get("clock_scale", 60.0))
    start_t = next((r["t"] for r in thirst if r.get("why") == "start"), None)
    very_t = next((r["t"] for r in thirst if r.get("level", 0) >= 2), None)
    window = [(r["t"], r["loss"]) for r in thirst if start_t is not None and very_t is not None and start_t + RATE_SETTLE_S <= r["t"] <= very_t]
    required = LOSS_L_PER_DAY / BODY_WATER_L * scale / REAL_SECONDS_PER_DAY
    actual = slope(window)
    failures += check("thirst.rate_per_real_second", round(actual, 7) if actual == actual else actual, "%.7f within %d%% (%d records)" % (required, RATE_TOLERANCE * 100, len(window)),
                      actual == actual and abs(actual - required) <= RATE_TOLERANCE * required)

    # The words: every record's level is the one its loss earns by the thresholds restated here.
    wrong = sum(1 for r in thirst if level_of(float(r["loss"])) != int(r.get("level", -1)))
    failures += check("thirst.words_at_their_thresholds", wrong, 0, wrong == 0 and len(thirst) > 0)
    failures += check("thirst.very_thirsty_reached", end.get("very_thirsty"), True, end.get("very_thirsty") is True)

    # The drink from fresh water: done, and the water up by a litre and a half of forty-two, or by what the body was short of.
    fresh = next((r for r in drinks if r.get("water_kind") == "fresh"), None)
    if fresh is None:
        failures += check("drink.fresh_done", None, "Done", False)
        failures += check("drink.fresh_rise", None, DRINK_L / BODY_WATER_L, False)
    else:
        before, after = float(fresh["water_before"]), float(fresh["water_after"])
        expected = min(DRINK_L, (1.0 - before) * BODY_WATER_L) / BODY_WATER_L
        failures += check("drink.fresh_done", fresh.get("outcome"), "Done", fresh.get("outcome") == "Done")
        failures += check("drink.fresh_rise", round(after - before, 6), "%.6f within 0.0005" % expected, abs((after - before) - expected) <= 0.0005)

    # The sea: refused as salt, and nothing gained by it.
    sea = next((r for r in drinks if r.get("water_kind") == "sea"), None)
    if sea is None:
        failures += check("drink.sea_salt", None, "Salt", False)
    else:
        failures += check("drink.sea_salt", sea.get("outcome"), "Salt", sea.get("outcome") == "Salt")
        gained = float(sea["water_after"]) - float(sea["water_before"])
        failures += check("drink.sea_gives_nothing", round(gained, 6), "<= 0", gained <= 1e-9)

    failures += check("end.errors", end.get("errors"), 0, end.get("errors") == 0)
    failures += check("end.frames", end.get("frames"), FRAMES, end.get("frames") == FRAMES)
    print("thirst_check %s: %s" % ("GREEN" if failures == 0 else "RED", argv[1]))
    return 1 if failures else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
