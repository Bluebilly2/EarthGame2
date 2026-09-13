#!/usr/bin/env python3
"""weather_check.py: does the engine's weather give Bherwerre the lighthouse's August and its year?

Runs the engine's almanac tool with +weather (Engine/tools/EarthGame.Almanac; format eg2.weather v1, ARCHITECTURE.md
section 10) for a year of weather for each of many world seeds at the lighthouse's own height, or reads a folder it is
given, and works out of the raw samples, with its own arithmetic, the figures the Bureau of Meteorology publishes for
Point Perpendicular Lighthouse (station 068034): August's mean daily maximum and minimum and its daily range, the hour
August's nights are coldest, and the rain and the rain days of August and of the year. Every row prints the engine's
number beside the reference. The exit code is the verdict: 0 when every row is within tolerance, 1 when any is not, 2
when the tool could not be run or its files are not the contracted format.

Independence, stated (CANON ruling 17): the references are restated here from their sources and never read from the
engine. The lighthouse's 1991-2004 figures are the Bureau's as Wikipedia's Jervis Bay Village page reproduces them (the
Bureau refuses scripts; DEBTS.md, "station normals are hand-pulled"); the region's warming since national records began,
1.51 +/- 0.23 C, and the cool season's drying since 1994, 9 % of April to October's rain, are State of the Climate 2024's;
the lapse rate is the standard atmosphere's; and sunrise comes from the NOAA solar equations on the real date, through
solar_check.py, which shares nothing with the engine's Spencer series. The daily extremes, the daily totals, the rain days
and the coldest hour are worked out here in numpy from the samples; the engine works out none of them.

Run from the repository root:  python Tools/verifiers/checks/weather_check.py [folder]
"""
import json
import os
import subprocess
import sys

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(HERE)))
sys.path.insert(0, HERE)
from solar_check import julian_day, noaa_sun, sunrise_hour_angle  # noqa: E402  (the verifiers' own NOAA sun)

ALMANAC_PROJECT = os.path.join(ROOT, "Engine", "tools", "EarthGame.Almanac")
DEFAULT_FOLDER = os.path.join(ROOT, "Artefacts", "weather", "modelled")

# What the tool is asked for: a year for each of many seeds, since one world's August is one draw of the weather and not
# its climate (a single seed's August rain runs from a third of the month's mean to twice it), at half-hour steps, which
# put a sampled daily extreme within a few hundredths of a degree of the curve's own.
FIRST_SEED = 1000
SEEDS = 256
STEPS_PER_DAY = 48

# ---- the referent, restated: Point Perpendicular Lighthouse, Bureau station 068034, 85 m, 1991-2004 ----
STATION_ELEVATION_M = 85.0
MEAN_MAX_C = [24.2, 24.4, 23.0, 21.1, 18.6, 16.6, 15.6, 16.8, 18.5, 20.1, 21.0, 23.1]
MEAN_MIN_C = [17.9, 18.4, 17.1, 14.9, 12.8, 10.6, 9.5, 9.5, 11.4, 13.0, 14.3, 16.6]
RAIN_MM = [88.2, 91.0, 89.5, 103.2, 151.3, 110.3, 106.5, 83.5, 83.2, 61.5, 93.8, 62.0]
RAIN_DAYS = [9.4, 9.0, 9.0, 8.4, 11.5, 8.6, 9.2, 6.0, 8.5, 8.3, 10.2, 8.8]   # the table states no threshold
RECORD_FIRST_YEAR, RECORD_LAST_YEAR = 1991, 2004
DAYS_IN = [31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31]
NAMES = "Jan Feb Mar Apr May Jun Jul Aug Sep Oct Nov Dec".split()
AUGUST = 7

WARMING_C = 1.51
WARMING_UNCERTAINTY_C = 0.23
COOL_SEASON_DRYING = 0.09
COOL_SEASON = range(3, 10)   # April to October, months counted from zero
DRYING_SINCE_YEAR = 1994
LAPSE_C_PER_KM = 6.5
OBSERVATION_HOUR = 9.0       # the Bureau's daily extremes are read at 9 am: the minimum to it, the maximum from it

# The place, restated from ARCHITECTURE.md section 3 as solar_check.py does, and the middle of August on the real date.
LATITUDE_DEG = -35.140
LONGITUDE_DEG = 150.675
MID_AUGUST = (2026, 8, 16)

TEMPERATURE_TOLERANCE_C = 2.0 * WARMING_UNCERTAINTY_C   # a level is known no better than the warming taken off it
RANGE_TOLERANCE_C = 0.15          # warming lifts a level and leaves the day's shape (v1's harness held a range to this)
COLDEST_HOUR_TOLERANCE_H = 0.75   # half-hour steps, and sunrise moving forty minutes across the month
TOTALS_TOLERANCE = 0.10           # the contract's tenth


def run_tool(folder):
    command = ["dotnet", "run", "--project", ALMANAC_PROJECT, "-c", "Debug", "--",
               "+weather", folder, "+first", str(FIRST_SEED), "+seeds", str(SEEDS), "+days", "365", "+steps", str(STEPS_PER_DAY)]
    try:
        result = subprocess.run(command, capture_output=True, text=True, cwd=ROOT)
    except OSError as ex:
        print("cannot run the almanac tool: %s (%s)" % (ex, " ".join(command)))
        return False
    if result.returncode != 0:
        print("the almanac tool exited %d: %s" % (result.returncode, (result.stderr or result.stdout).strip()[:600]))
        return False
    return True


def load(folder):
    """The sidecar and the samples as (seeds, days, steps, fields), or None with the reasons printed."""
    sidecar_path = os.path.join(folder, "weather.json")
    if not os.path.isfile(sidecar_path):
        print("no weather.json in %s" % folder)
        return None
    with open(sidecar_path, encoding="utf-8") as f:
        side = json.load(f)
    problems = []
    if side.get("format") != "eg2.weather" or side.get("version") != 1:
        problems.append("format %r version %r" % (side.get("format"), side.get("version")))
    if side.get("fields") != ["air_c", "rain_mm_per_hour"]:
        problems.append("fields %r" % side.get("fields"))
    if side.get("dtype") != "f32" or side.get("byte_order") != "little":
        problems.append("dtype %r byte order %r" % (side.get("dtype"), side.get("byte_order")))
    if side.get("first_local_day") != 0 or side.get("sampled_at") != "the middle of each step":
        problems.append("first local day %r sampled at %r" % (side.get("first_local_day"), side.get("sampled_at")))
    if "no equation of time" not in str(side.get("time_basis", "")):
        problems.append("time basis %r" % side.get("time_basis"))
    if side.get("days") != 365:
        problems.append("%r days, where the year's rows need 365" % side.get("days"))
    if problems:
        print("weather.json is not the contracted format: %s" % "; ".join(problems))
        return None
    seeds, days, steps = side["seeds"], side["days"], side["steps_per_day"]
    raw = np.fromfile(os.path.join(folder, side["raw"]), dtype="<f4")
    if raw.size != seeds * days * steps * 2:
        print("%s holds %d values where %d seeds of %d days of %d steps of 2 fields make %d"
              % (side["raw"], raw.size, seeds, days, steps, seeds * days * steps * 2))
        return None
    return side, raw.reshape(seeds, days, steps, 2).astype(np.float64)


def main():
    folder = sys.argv[1] if len(sys.argv) > 1 else DEFAULT_FOLDER
    if len(sys.argv) <= 1 and not run_tool(folder):
        return 2
    loaded = load(folder)
    if loaded is None:
        return 2
    side, samples = loaded
    seeds, days, steps = side["seeds"], side["days"], side["steps_per_day"]
    step_hours = 24.0 / steps
    air, rain = samples[..., 0], samples[..., 1]
    first = np.cumsum([0] + DAYS_IN[:-1])
    august = slice(first[AUGUST], first[AUGUST] + DAYS_IN[AUGUST])
    print("%d seeds from %d, %d days of %d steps, at %.1f m on ground %.2f open (%s)"
          % (seeds, side["first_seed"], days, steps, side["altitude_m"], side["exposure"], folder))

    failures = []

    def expect(name, ok, detail):
        print("%-34s %s  %s" % (name, "ok " if ok else "FAIL", detail))
        if not ok:
            failures.append(name)

    expect("every sample a number", bool(np.isfinite(samples).all()), "%d samples" % samples[..., 0].size)
    expect("no rain below nothing", bool((rain >= 0.0).all()), "the least %.4f mm/h" % rain.min())

    # ---- August's air: each day's extremes over the Bureau's windows, averaged over the month and the seeds ----
    # A calendar day's lowest reading would take the evening after a cold change as well as the dawn before it, which no
    # station's minimum does: the Bureau's is the lowest in the 24 hours to 9 am, and its maximum the highest in the 24
    # hours from 9 am. Its 9 am is local standard time, three minutes behind the solar time the samples keep; the check
    # leaves that alone.
    lapse = LAPSE_C_PER_KM * (side["altitude_m"] - STATION_ELEVATION_M) / 1000.0
    shift = int(round(OBSERVATION_HOUR / step_hours))
    windows = air.reshape(seeds, days * steps)[:, shift:shift + (days - 1) * steps].reshape(seeds, days - 1, steps)
    highs = windows.max(axis=2)    # highs[:, j]: the highest reading from 9 am of day j
    lows = windows.min(axis=2)     # lows[:, j]: the lowest reading to 9 am of day j + 1
    mean_max = highs[:, first[AUGUST]:first[AUGUST] + DAYS_IN[AUGUST]].mean()
    mean_min = lows[:, first[AUGUST] - 1:first[AUGUST] - 1 + DAYS_IN[AUGUST]].mean()
    target_max = MEAN_MAX_C[AUGUST] - WARMING_C - lapse
    target_min = MEAN_MIN_C[AUGUST] - WARMING_C - lapse
    expect("August's mean maximum", abs(mean_max - target_max) <= TEMPERATURE_TOLERANCE_C,
           "engine %.2f C  reference %.2f (%.1f less the warming %.2f and %.2f for height)  tolerance %.2f"
           % (mean_max, target_max, MEAN_MAX_C[AUGUST], WARMING_C, lapse, TEMPERATURE_TOLERANCE_C))
    expect("August's mean minimum", abs(mean_min - target_min) <= TEMPERATURE_TOLERANCE_C,
           "engine %.2f C  reference %.2f (%.1f less the warming %.2f and %.2f for height)  tolerance %.2f"
           % (mean_min, target_min, MEAN_MIN_C[AUGUST], WARMING_C, lapse, TEMPERATURE_TOLERANCE_C))
    station_range = MEAN_MAX_C[AUGUST] - MEAN_MIN_C[AUGUST]
    expect("August's daily range", abs((mean_max - mean_min) - station_range) <= RANGE_TOLERANCE_C,
           "engine %.2f C  reference %.2f  tolerance %.2f" % (mean_max - mean_min, station_range, RANGE_TOLERANCE_C))

    # The coldest hour of August's mean day, against sunrise in the middle of the month.
    cycle = air[:, august, :].mean(axis=(0, 1))
    coldest_hour = (int(np.argmin(cycle)) + 0.5) * step_hours
    noon_utc_hour = 12.0 - LONGITUDE_DEG / 15.0
    declination, _, _ = noaa_sun(julian_day(MID_AUGUST[0], MID_AUGUST[1], MID_AUGUST[2], noon_utc_hour))
    sunrise = 12.0 - sunrise_hour_angle(LATITUDE_DEG, declination) / 15.0
    expect("the coldest hour of August's night", abs(coldest_hour - sunrise) <= COLDEST_HOUR_TOLERANCE_H,
           "engine %.2f h  reference sunrise %.2f h on 16 August (NOAA)  tolerance %.2f h" % (coldest_hour, sunrise, COLDEST_HOUR_TOLERANCE_H))

    # ---- the rain: each day's total out of its steps, each month's over its days, averaged over the seeds ----
    inside = (RECORD_LAST_YEAR - DRYING_SINCE_YEAR + 1) / float(RECORD_LAST_YEAR - RECORD_FIRST_YEAR + 1)
    pre_human = [RAIN_MM[m] / (1.0 - COOL_SEASON_DRYING * inside) if m in COOL_SEASON else RAIN_MM[m] for m in range(12)]
    daily = rain.sum(axis=2) * step_hours
    months = [daily[:, first[m]:first[m] + DAYS_IN[m]].sum(axis=1).mean() for m in range(12)]
    print("the months' rain, engine / pre-human reference mm: %s"
          % ", ".join("%s %.0f/%.0f" % (NAMES[m], months[m], pre_human[m]) for m in range(12)))
    expect("August's rain", abs(months[AUGUST] - pre_human[AUGUST]) <= TOTALS_TOLERANCE * pre_human[AUGUST],
           "engine %.1f mm  reference %.1f (the lighthouse's %.1f with the drying undone for %.0f%% of its years)  tolerance a tenth"
           % (months[AUGUST], pre_human[AUGUST], RAIN_MM[AUGUST], 100.0 * inside))
    year = daily.sum(axis=1).mean()
    expect("the year's rain", abs(year - sum(pre_human)) <= TOTALS_TOLERANCE * sum(pre_human),
           "engine %.1f mm  reference %.1f (the lighthouse's %.1f)  tolerance a tenth" % (year, sum(pre_human), sum(RAIN_MM)))
    august_1 = (daily[:, august] >= 1.0).sum(axis=1).mean()
    august_02 = (daily[:, august] >= 0.2).sum(axis=1).mean()
    expect("August's rain days", august_1 <= RAIN_DAYS[AUGUST] <= august_02,
           "engine %.2f at 1 mm .. %.2f at 0.2 mm  reference %.1f, which must lie between" % (august_1, august_02, RAIN_DAYS[AUGUST]))
    year_1 = (daily >= 1.0).sum(axis=1).mean()
    year_02 = (daily >= 0.2).sum(axis=1).mean()
    expect("the year's rain days", year_1 <= sum(RAIN_DAYS) <= year_02,
           "engine %.1f at 1 mm .. %.1f at 0.2 mm  reference %.1f, which must lie between" % (year_1, year_02, sum(RAIN_DAYS)))

    if failures:
        print("weather_check: FAIL (%s)" % ", ".join(failures))
        return 1
    print("weather_check: ok, the engine's weather gives the lighthouse's August and its year within tolerance")
    return 0


if __name__ == "__main__":
    sys.exit(main())
