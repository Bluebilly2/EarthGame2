#!/usr/bin/env python3
"""cold_check.py: a founder's night and death (FP.2) as a night run recorded them, held to the physiology restated here.

Reference: v1's BodyState (Assets/EarthGame/Sim/Body/BodyState.cs) and its calibration (CALIBRATION_B1_DAY_ONE.md), whose
numbers are published human physiology: 70 kg, 1.8 m² of skin, tissue at 3,500 J/(kg·K), 80 W at rest, 350 W of shivering
for three hours' worth, 0.7 clo of still air on bare skin thinned by the root of the wind, tissue 0.3 to 0.9 clo as the
cold constricts it, a clear sky 16 K colder than the air, Fanger's breath, the Meinel beam; the words at 36.7, 36, 35 and
32 °C; death at 28. Source: the run's run.jsonl (eg2.run, ARCHITECTURE section 10) alone, read with the standard library:
the `warmth` records carry the core the server told, the sky the client worked out (air, the wind in the open, cloud,
humidity, the sun's elevation) and the server's tick the word was told on, and this check lives the same night again in
Python from those and compares the core it reaches with the core recorded, every word along the way. The time base is
the ticks: the server steps the body once a tick, a tick is a twentieth of a real second, and a real second is the game's
forty-eight world seconds times the clock's rate the run states, so the world's seconds between two words are exact and
owe nothing to a clock on the client (which follows the server's by pongs and lags a change of rate by a second or so).
The window opens at the first word after the clock was sped, the word before it straddling the change of rate. Every
number below is written here, not read from the engine, so a slip in the engine is a red row.

The wind at the body is the open's wind scaled by the ground's openness where the founder stands, which the server reads
and the records do not carry; so the night is lived twice, once in the open's wind and once in still air, and the core
recorded must lie between the two, the true wind being between the two winds. A body in a hollow that cooled faster than
the open, or one in the open that cooled slower than still air, is a red row. The shivering reserve the body had spent
before the window's first word is hidden state: the floor's body (the open's wind) begins with the reserve a word's worth
of shivering at the demand the recorded fall implies would have spent, the most it could have; the ceiling's (still air)
begins fresh. The tick a word carries is the last the client knew when the word came, a few ticks after it was told at
worst, and the tolerance carries that.

Usage: python Tools/verifiers/checks/cold_check.py Artefacts/frames/night-<stamp>
Exit 0 when every row passes, 1 otherwise; prints each row's actual and required beside its verdict.
"""
import json
import math
from pathlib import Path
import sys

SKIN_M2 = 1.8
MASS_KG = 70.0
SPECIFIC_HEAT = 3500.0
BASAL_W = 80.0
WALKING_W, RUNNING_W = 180.0, 420.0
MAX_SHIVER_W = 350.0
SHIVER_HOURS = 3.0
BARE_SKIN_CLO = 0.7
TISSUE_WARM_CLO, TISSUE_COLD_CLO = 0.30, 0.90
CLO_SI = 0.155
SIGMA = 5.670374419e-8
EMISSIVITY = 0.98
RADIATING = 0.70
SKY_VIEW = 0.5
SKY_DEPRESSION_K = 16.0
ABSORPTANCE = 0.55
NORMAL_C, HYPOTHERMIA_C, SEVERE_C, LETHAL_C = 37.0, 35.0, 32.0, 28.0
CHILLY_C, COLD_C = 36.7, 36.0
REAL_SECONDS_PER_DAY = 1800.0
RESTING_BELOW_MS, RUNNING_FROM_MS = 0.2, 2.8
# Room outside the two winds' curves, °C: the server steps its body once a tick (144 world seconds at sixty times), this
# check in steps no longer than 150 s from the sky of the last word, and the two are not the same partition of the night.
BOUND_TOLERANCE_C = 0.3
FRAMES = 6


def check(name, actual, required, good):
    print("%s %s: actual=%r; required=%r" % ("PASS" if good else "FAIL", name, actual, required), flush=True)
    return 0 if good else 1


def clamp01(x):
    return max(0.0, min(1.0, x))


def level_of(core):
    if core < SEVERE_C:
        return 4
    if core < HYPOTHERMIA_C:
        return 3
    if core < COLD_C:
        return 2
    if core < CHILLY_C:
        return 1
    return 0


def direct_wm2(elev, cloud):
    if elev <= 0.5:
        return 0.0
    air_mass = 1.0 / max(0.05, math.sin(math.radians(elev)))
    return 1353.0 * 0.7 ** (air_mass ** 0.678) * (1.0 - 0.85 * clamp01(cloud))


def diffuse_wm2(elev, cloud):
    if elev <= 0.5:
        return 0.0
    s = math.sin(math.radians(elev))
    return 0.10 * direct_wm2(elev, 0.0) * s * (1.0 - clamp01(cloud)) + 0.28 * 1361.0 * s * clamp01(cloud)


class Body:
    """v1's heat balance for a naked founder standing or walking, as FP.2 ported it, written again here."""

    def __init__(self, core):
        self.core = core
        self.stamina = 1.0
        self.shivering = 0.0

    def step(self, seconds, air, wind, cloud, humidity, elev, speed):
        deficit = NORMAL_C - self.core
        demand = clamp01((deficit - 0.15) / 1.2)
        self.shivering = demand * self.stamina
        if demand > 0.01:
            self.stamina = max(0.0, self.stamina - self.shivering * seconds / (SHIVER_HOURS * 3600.0))
        else:
            self.stamina = min(1.0, self.stamina + seconds / (SHIVER_HOURS * 3600.0 * 4.0))
        activity = 0.0 if not speed >= RESTING_BELOW_MS else (RUNNING_W if speed >= RUNNING_FROM_MS else WALKING_W)
        production = BASAL_W + activity + self.shivering * MAX_SHIVER_W
        constriction = clamp01((1.0 if air < 28.0 else 0.0) * (deficit + 0.6))
        tissue = TISSUE_WARM_CLO + (TISSUE_COLD_CLO - TISSUE_WARM_CLO) * constriction
        boundary = max(0.12, BARE_SKIN_CLO / (1.0 + 0.55 * math.sqrt(max(0.0, wind))))
        sensible = SKIN_M2 * (self.core - air) / max(0.02, (tissue + boundary) * CLO_SI)
        elev = elev if elev is not None else -10.0
        projected = 0.25 - 0.17 * (min(90.0, max(0.0, elev)) / 90.0)
        solar = (direct_wm2(elev, cloud) * projected + diffuse_wm2(elev, cloud) * RADIATING * 0.5) * SKIN_M2 * ABSORPTANCE if elev > 0 else 0.0
        capacity = MASS_KG * SPECIFIC_HEAT
        surplus = production + solar - sensible
        evaporative = 0.0
        if surplus > 0.0:
            projected_core = self.core + surplus * seconds / capacity
            drive = clamp01((projected_core - NORMAL_C) / 0.4)
            most = 1.5 * 2430000.0 / 3600.0
            headroom = surplus + max(0.0, self.core - NORMAL_C) * capacity / seconds
            evaporative = min(drive * most, headroom)
        sky_c = air - SKY_DEPRESSION_K * (1.0 - clamp01(cloud))
        ta, ts = air + 273.15, sky_c + 273.15
        sky = EMISSIVITY * SIGMA * SKIN_M2 * RADIATING * SKY_VIEW * (ta ** 4 - ts ** 4) if ts < ta else 0.0
        saturation = 0.61094 * math.exp(17.625 * air / (air + 243.04))
        breath = max(0.0, 0.0014 * production * (34.0 - air) + 0.0173 * production * (5.87 - clamp01(humidity) * saturation))
        net = surplus - evaporative - sky - breath
        self.core = min(41.0, self.core + net * seconds / capacity)


def world_seconds(a, b, world_per_tick):
    """The world's seconds between two words: the server's ticks between them, each a tick's worth of the world at the run's rate."""
    return max(0, int(b["tick"]) - int(a["tick"])) * world_per_tick


def reserve_after(core_from, core_to, seconds):
    """The shivering reserve left after the core fell from one to the other over the seconds, the fall taken as steady."""
    stamina = 1.0
    n = 20
    for i in range(n):
        core = core_from + (core_to - core_from) * (i + 0.5) / n
        demand = clamp01((NORMAL_C - core - 0.15) / 1.2)
        stamina = max(0.0, stamina - demand * stamina * (seconds / n) / (SHIVER_HOURS * 3600.0))
    return stamina


def live(window, world_per_tick, wind_share, stamina=1.0):
    """The window's night lived again from its first word, in the open's wind times a share, with this much of the shivering
    reserve; the worst excursion of the recorded core above (positive) and below (negative) the core reached, and the core
    reached at the end."""
    body = Body(float(window[0]["core_c"]))
    body.stamina = stamina
    above, below = 0.0, 0.0
    for a, b in zip(window, window[1:]):
        seconds = world_seconds(a, b, world_per_tick)
        if seconds <= 0.0:
            continue
        n = max(1, int(math.ceil(seconds / 150.0)))
        for _ in range(n):
            body.step(seconds / n, float(a["air_c"]), float(a["wind_open_ms"]) * wind_share, float(a.get("cloud", 0.0)),
                      float(a.get("humidity", 0.7)), a.get("sun_elevation_deg"), float(a.get("speed_ms", 0.0)))
        gap = float(b["core_c"]) - body.core
        above = max(above, gap)
        below = min(below, gap)
    return above, below, body.core


def main(argv):
    if len(argv) != 2:
        print(__doc__)
        return 2
    run = Path(argv[1]) / "run.jsonl"
    if not run.is_file():
        print("no run.jsonl at %s" % run)
        return 2
    records = [json.loads(line) for line in run.read_text(encoding="utf-8").splitlines() if line.strip()]
    warmth = [r for r in records if r.get("kind") == "warmth" and "air_c" in r and "core_c" in r]
    night = next((r for r in records if r.get("kind") == "night"), None)
    reached = next((r for r in records if r.get("kind") == "cold_reached"), None)
    dawn = next((r for r in records if r.get("kind") == "dawn"), None)
    died = next((r for r in records if r.get("kind") == "died"), None)
    end = next((r for r in reversed(records) if r.get("kind") == "end"), {})
    failures = 0

    # The natural night: from the first word after the clock was sped to the dawn record, or failing that to the first
    # cold, and never past a death (the body after it is a new one). A tick's worth of the world at the run's rate is the
    # time base (see the top).
    scale = float(end.get("clock_scale", 60.0))
    welcome = next((r for r in records if r.get("kind") == "welcome"), {})
    tick_rate = float(welcome.get("tick_rate", 20))
    world_per_tick = scale * 86400.0 / REAL_SECONDS_PER_DAY / tick_rate
    window = [r for r in warmth if night is not None and r["t"] > night["t"] and "tick" in r]
    before = next((r for r in reversed(warmth) if night is not None and r["t"] <= night["t"]), None)
    closing = dawn if dawn is not None else reached
    if closing is not None:
        window = [r for r in window if r["t"] <= closing["t"]]
    if died is not None:
        window = [r for r in window if r["t"] < died["t"]]
    # The panel's own hand on the core is not the night's: a word more than four degrees under the one before it is the row's.
    trimmed = []
    for r in window:
        if trimmed and float(trimmed[-1]["core_c"]) - float(r["core_c"]) > 4.0:
            break
        trimmed.append(r)
    window = trimmed
    if len(window) >= 3:
        # The floor begins with the reserve a word's worth of the fall from the word before the night to the first would have spent.
        spent = reserve_after(float(before["core_c"]), float(window[0]["core_c"]), world_per_tick * tick_rate) if before is not None else 1.0
        _open_above, open_below, open_core = live(window, world_per_tick, 1.0, spent)
        still_above, _still_below, still_core = live(window, world_per_tick, 0.0)
        # The open's curve is the floor and still air's the ceiling: a recorded core under the floor cooled faster than
        # the open's wind allows, one over the ceiling slower than still air does.
        worst_under_open = max(0.0, -open_below)
        worst_over_still = max(0.0, still_above)
        failures += check("cold.core_between_still_air_and_the_opens_wind",
                          "under the open's curve by %.3f, over still air's by %.3f over %d words; reached %.2f (open, reserve %.2f at the first word) .. %.2f (still)"
                          % (worst_under_open, worst_over_still, len(window), open_core, spent, still_core),
                          "both <= %.1f °C" % BOUND_TOLERANCE_C, worst_under_open <= BOUND_TOLERANCE_C and worst_over_still <= BOUND_TOLERANCE_C)
        hours = sum(world_seconds(a, b, world_per_tick) for a, b in zip(window, window[1:])) / 3600.0
        failures += check("cold.night_lived_by_the_servers_ticks", round(hours, 2), ">= 5 h of the world between the night's first word and its close", hours >= 5.0)
    else:
        failures += check("cold.core_between_still_air_and_the_opens_wind", len(window), ">= 3 words between the night's start and its close", False)
    wrong = sum(1 for r in warmth if level_of(float(r["core_c"])) != int(r.get("cold", -1)))
    failures += check("cold.words_at_their_thresholds", wrong, 0, wrong == 0 and len(warmth) > 0)
    failures += check("cold.cold_reached", end.get("cold"), True, end.get("cold") is True)
    # The night's lowest core, before the panel moved it: cold, and above the lethal unless the night itself killed, when
    # the core the death names (the step's own, between two told words) must not be.
    lowest = float((dawn or reached or end).get("lowest_core_c", float("nan")))
    night_killed = bool((dawn or {}).get("died_in_the_night", False))
    if night_killed:
        at_death = float((died or {}).get("core_c", float("nan")))
        failures += check("cold.core_at_or_below_lethal_when_the_night_killed", (round(lowest, 2), round(at_death, 2)),
                          "the last word told above %.0f by less than a word's cooling, the death's core at or below it" % LETHAL_C,
                          at_death <= LETHAL_C and lowest <= LETHAL_C + 1.0)
    else:
        failures += check("cold.lowest_core_below_cold_and_above_lethal", round(lowest, 2), "(%.0f, %.0f)" % (LETHAL_C, COLD_C), LETHAL_C < lowest < COLD_C)
    if dawn is None:
        failures += check("dawn.told", None, "a dawn record", False)
    else:
        failures += check("dawn.sun_up_or_the_night_killed", (dawn.get("sun_up"), dawn.get("died_in_the_night")), "sun up, or the night itself deadly",
                          dawn.get("sun_up") is True or dawn.get("died_in_the_night") is True)
        if dawn.get("sun_up") is True:
            failures += check("dawn.sun_above_the_horizon", round(float(dawn.get("sun_elevation_deg", -99.0)), 2), ">= 0", float(dawn.get("sun_elevation_deg", -99.0)) >= 0.0)

    if died is None:
        failures += check("death.told", None, "a died record", False)
    else:
        failures += check("death.cause", died.get("cause"), "Cold", died.get("cause") == "Cold")
        failures += check("death.core_at_or_below_lethal", died.get("core_c"), "<= %.0f" % LETHAL_C, float(died.get("core_c", 99)) <= LETHAL_C)
        failures += check("death.sentence", (died.get("sentence") or "")[:24], "starts 'You died of the cold at'", (died.get("sentence") or "").startswith("You died of the cold at"))
        # The wind the death names is the wind at the body: never more than the open's wind the client had at the last word.
        last = next((r for r in reversed(warmth) if r["t"] <= died["t"]), None)
        if last is not None:
            failures += check("death.wind_at_the_body_within_the_opens", (round(float(died.get("wind_ms", 99.0)), 2), round(float(last["wind_open_ms"]), 2)),
                              "body <= open + 0.05", float(died.get("wind_ms", 99.0)) <= float(last["wind_open_ms"]) + 0.05)
    failures += check("respawn.at_the_wake", round(float(end.get("wake_distance_m", float("nan"))), 2), "<= 3 m", end.get("respawned_at_wake") is True)
    failures += check("stick.lies_where_they_fell", round(float(end.get("stick_distance_m", float("nan"))), 2), "<= 2.5 m", end.get("stick_lies_where_fell") is True)
    failures += check("end.errors", end.get("errors"), 0, end.get("errors") == 0)
    failures += check("end.frames", end.get("frames"), FRAMES, end.get("frames") == FRAMES)
    print("cold_check %s: %s" % ("GREEN" if failures == 0 else "RED", argv[1]))
    return 1 if failures else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
