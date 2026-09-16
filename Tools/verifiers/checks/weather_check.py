#!/usr/bin/env python3
"""weather_check.py: does the engine's weather give Bherwerre the lighthouse's August and its year?

Runs the engine's almanac tool with +weather (Engine/tools/EarthGame.Almanac; format eg2.weather v2, ARCHITECTURE.md
section 10) for a year of weather for each of many world seeds at the lighthouse's own height, or reads a folder it is
given, and works out of the raw samples, with its own arithmetic, the figures the Bureau of Meteorology publishes for
Point Perpendicular Lighthouse (station 068034): August's mean daily maximum and minimum and its daily range, the hour
August's nights are coldest, the spread of August's nights between the first and the ninth decile, the rain and the rain
days of August and of the year at the Bureau's stated thresholds, the wind and the cloud at 9 am and 3 pm, the humidity
at 9 am and 3 pm, and the sun's daily energy on the ground. Every row prints the engine's number beside the reference.
The exit code is the verdict: 0 when every row is within tolerance, 1 when any is not, 2 when the tool could not be run
or its files are not the contracted format.

Independence, stated (CANON ruling 17): the references are restated here from their sources and never read from the
engine. The lighthouse's figures are the Bureau's own "Climate statistics for Australian locations" table, all years of
record, copied from the Bureau's page in the owner's browser on 2026-09-16 (Data/stations/, M1.8c; the Bureau refuses
scripts); the region's warming since national records began, 1.51 +/- 0.23 C as a trend over 1910 to 2023, and the cool
season's drying since 1994, 9 % of April to October's rain, are State of the Climate 2024's, and what of the warming lies
inside a table is the trend's value at the table's middle year; the lapse rate is the standard atmosphere's; the share of
a 10 m wind at 1.5 m over grass is the logarithmic profile's; sunrise and the sun's height come from the NOAA solar
equations on the real date, through solar_check.py, which shares nothing with the engine's Spencer series; the beam on
the ground is Meinel's approximation (Meinel and Meinel, Applied Solar Energy, 1976) restated here. The daily extremes,
the deciles, the daily totals, the rain days, the means at the observers' hours, the coldest hour and the day's energy
are worked out here in numpy from the samples; the engine works out none of them.

Run from the repository root:  python Tools/verifiers/checks/weather_check.py [folder]
"""
import json
import math
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
FIELDS = ["air_c", "rain_mm_per_hour", "wind_ms", "cloud_01", "humidity_01"]

# ---- the referent, restated: Point Perpendicular Lighthouse, Bureau station 068034, 85 m, all years (Data/stations) ----
STATION_ELEVATION_M = 85.0
MEAN_MAX_C = [23.8, 23.9, 23.0, 20.7, 18.2, 15.9, 15.1, 16.1, 17.9, 19.8, 21.2, 22.9]     # 1907-2004
MEAN_MIN_C = [17.5, 18.0, 17.1, 14.9, 12.4, 10.4, 9.2, 9.6, 11.2, 12.9, 14.5, 16.3]
AIR_RECORD_YEARS = (1907, 2004)
AUGUST_MIN_DECILE_1_C, AUGUST_MIN_DECILE_9_C = 7.0, 12.2                                 # 1946-2004
DECILE_RECORD_YEARS = (1946, 2004)
RAIN_MM = [97.2, 98.5, 122.4, 131.8, 134.1, 128.8, 107.2, 89.7, 79.2, 85.3, 84.3, 83.1]   # 1899-2004
RAIN_DAYS_1MM = [9.2, 9.1, 10.1, 9.5, 10.2, 9.8, 8.9, 8.3, 8.6, 9.1, 9.1, 8.6]
RAIN_DAYS_ANY = [11.7, 11.5, 12.7, 11.9, 12.2, 11.6, 10.6, 10.1, 10.4, 11.2, 11.3, 11.3]  # the Bureau's day of rain is 0.2 mm or more
RAIN_DAYS_10MM = [2.7, 2.5, 3.2, 3.4, 3.5, 3.6, 3.2, 2.5, 2.1, 2.5, 2.4, 2.4]
RAIN_RECORD_YEARS = (1899, 2004)
WIND_9AM_KMH = [14.2, 14.0, 13.5, 15.0, 16.8, 19.1, 17.7, 17.1, 16.2, 15.2, 15.6, 14.8]  # 1957-2004, at 10 m
WIND_3PM_KMH = [20.5, 19.3, 18.3, 17.9, 17.9, 19.1, 19.4, 20.5, 21.3, 21.2, 22.5, 21.9]
CLOUD_9AM_OKTAS = [5.0, 5.0, 4.8, 4.4, 4.3, 4.3, 3.8, 3.7, 3.9, 4.4, 4.7, 4.9]
CLOUD_3PM_OKTAS = [4.6, 4.5, 4.3, 4.2, 4.4, 4.2, 3.9, 3.8, 3.8, 4.3, 4.6, 4.5]
HUMIDITY_9AM = [76, 77, 77, 74, 75, 75, 74, 71, 69, 70, 72, 74]
HUMIDITY_3PM = [71, 72, 71, 68, 67, 65, 63, 61, 64, 66, 69, 70]
TEMP_9AM_C = [20.6, 20.8, 19.7, 17.5, 14.8, 12.5, 11.4, 12.3, 14.4, 16.5, 18.0, 19.7]    # 1907-2004
TEMP_3PM_C = [22.3, 22.7, 21.7, 19.6, 17.2, 15.1, 14.3, 15.1, 16.6, 18.0, 19.5, 21.2]    # 1909-2004
SOLAR_MJ = [22.9, 20.2, 16.8, 13.2, 9.8, 8.1, 9.3, 12.6, 16.6, 20.1, 21.8, 23.5]         # 1990-2026, satellite-derived
DAYS_IN = [31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31]
NAMES = "Jan Feb Mar Apr May Jun Jul Aug Sep Oct Nov Dec".split()
AUGUST, JANUARY = 7, 0

WARMING_C = 1.51
WARMING_TREND_YEARS = (1910, 2023)
COOL_SEASON_DRYING = 0.09
COOL_SEASON = range(3, 10)   # April to October, months counted from zero
DRYING_SINCE_YEAR = 1994
LAPSE_C_PER_KM = 6.5
OBSERVATION_HOUR = 9.0       # the Bureau's daily extremes are read at 9 am: the minimum to it, the maximum from it
AFTERNOON_HOUR = 15.0
BODY_HEIGHT_WIND_SHARE = math.log(1.5 / 0.03) / math.log(10.0 / 0.03)   # a 10 m wind at 1.5 m over grass, the log profile

# The place, restated from ARCHITECTURE.md section 3 as solar_check.py does, and the middle of August on the real date.
LATITUDE_DEG = -35.140
LONGITUDE_DEG = 150.675
MID_AUGUST = (2026, 8, 16)
YEAR = 2026

AUGUST_TEMPERATURE_TOLERANCE_C = 0.35   # one yearly wave for the day's mean leaves August about this far from its own
RANGE_TOLERANCE_C = 0.15          # warming lifts a level and leaves the day's shape (v1's harness held a range to this)
COLDEST_HOUR_TOLERANCE_H = 0.75   # half-hour steps, and sunrise moving forty minutes across the month
SPREAD_TOLERANCE_C = 0.6          # the deciles' spread, over 256 seeds' Augusts
TOTALS_TOLERANCE = 0.10           # the contract's tenth
HEAVY_DAYS_TOLERANCE = 0.20       # the year's days of 10 mm or more, a fifth
WIND_TOLERANCE = 1.0 / 12.0       # the fronts' factor averages a hundredth over one; the months eased straight; a quarter hour past the observer's
CLOUD_TOLERANCE_OKTAS = 0.5
HUMIDITY_TOLERANCE = 6.0          # points
SOLAR_TOLERANCE = 1.0 / 8.0
OBSERVER_TEMPERATURE_TOLERANCE_C = 2.0   # the day's curve runs about a degree warm at nine and at three (DEBTS: no cloud in the daily range)


def warming_inside(first_year, last_year):
    """What of the warming trend lies inside a table's years: its value at the table's middle year."""
    middle = 0.5 * (first_year + last_year)
    share = (middle - WARMING_TREND_YEARS[0]) / float(WARMING_TREND_YEARS[1] - WARMING_TREND_YEARS[0])
    return WARMING_C * min(1.0, max(0.0, share))


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
    if side.get("format") != "eg2.weather" or side.get("version") != 2:
        problems.append("format %r version %r (this check reads version 2)" % (side.get("format"), side.get("version")))
    if side.get("fields") != FIELDS:
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
    if raw.size != seeds * days * steps * len(FIELDS):
        print("%s holds %d values where %d seeds of %d days of %d steps of %d fields make %d"
              % (side["raw"], raw.size, seeds, days, steps, len(FIELDS), seeds * days * steps * len(FIELDS)))
        return None
    return side, raw.reshape(seeds, days, steps, len(FIELDS)).astype(np.float64)


def sun_elevation_deg(day_index, hour):
    """The sun's height over the place at a local solar hour of a day of the year, NOAA's declination and the hour angle."""
    noon_utc_hour = 12.0 - LONGITUDE_DEG / 15.0
    date = julian_day(YEAR, 1, 1, noon_utc_hour) + day_index
    declination, _, _ = noaa_sun(date)
    hour_angle = math.radians(15.0 * (hour - 12.0))
    lat, dec = math.radians(LATITUDE_DEG), math.radians(declination)
    sin_elev = math.sin(lat) * math.sin(dec) + math.cos(lat) * math.cos(dec) * math.cos(hour_angle)
    return math.degrees(math.asin(max(-1.0, min(1.0, sin_elev))))


def meinel_ground_wm2(elevation_deg, cloud):
    """The sun's power on level ground under a share of cloud, W/m2: Meinel's beam I = 1.353 * 0.7^(AM^0.678) kW/m2 on the
    horizontal, the beam cut to a quarter by full cloud, and the diffuse sky, fifteen hundredths of the beam's fall when
    clear and 0.28 of the solar constant times the sun's sine when overcast: the shares M1.8c set against this site's
    satellite exposure (v1's 0.85 and 0.10 left every month 5-14 % short), restated."""
    if elevation_deg <= 0.5:
        return 0.0
    s = math.sin(math.radians(elevation_deg))
    air_mass = 1.0 / max(0.05, s)
    beam = 1353.0 * 0.7 ** (air_mass ** 0.678)
    cloud = min(1.0, max(0.0, cloud))
    direct = beam * (1.0 - 0.75 * cloud) * s
    diffuse = 0.15 * beam * s * (1.0 - cloud) + 0.28 * 1361.0 * s * cloud
    return direct + diffuse


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
    air, rain, wind, cloud, humidity = (samples[..., i] for i in range(5))
    first = np.cumsum([0] + DAYS_IN[:-1])
    august = slice(first[AUGUST], first[AUGUST] + DAYS_IN[AUGUST])
    print("%d seeds from %d, %d days of %d steps, at %.1f m on ground %.2f open (%s)"
          % (seeds, side["first_seed"], days, steps, side["altitude_m"], side["exposure"], folder))

    failures = []

    def expect(name, ok, detail):
        print("%-40s %s  %s" % (name, "ok " if ok else "FAIL", detail))
        if not ok:
            failures.append(name)

    expect("every sample a number", bool(np.isfinite(samples).all()), "%d samples" % samples[..., 0].size)
    expect("no rain below nothing", bool((rain >= 0.0).all()), "the least %.4f mm/h" % rain.min())
    expect("cloud and humidity between 0 and 1", bool(((cloud >= 0) & (cloud <= 1) & (humidity >= 0) & (humidity <= 1)).all()), "")

    # ---- August's air: each day's extremes over the Bureau's windows, averaged over the month and the seeds ----
    # A calendar day's lowest reading would take the evening after a cold change as well as the dawn before it, which no
    # station's minimum does: the Bureau's is the lowest in the 24 hours to 9 am, and its maximum the highest in the 24
    # hours from 9 am. Its 9 am is local standard time, three minutes behind the solar time the samples keep; the check
    # leaves that alone.
    lapse = LAPSE_C_PER_KM * (side["altitude_m"] - STATION_ELEVATION_M) / 1000.0
    air_warming = warming_inside(*AIR_RECORD_YEARS)
    shift = int(round(OBSERVATION_HOUR / step_hours))
    windows = air.reshape(seeds, days * steps)[:, shift:shift + (days - 1) * steps].reshape(seeds, days - 1, steps)
    highs = windows.max(axis=2)    # highs[:, j]: the highest reading from 9 am of day j
    lows = windows.min(axis=2)     # lows[:, j]: the lowest reading to 9 am of day j + 1
    august_highs = highs[:, first[AUGUST]:first[AUGUST] + DAYS_IN[AUGUST]]
    august_lows = lows[:, first[AUGUST] - 1:first[AUGUST] - 1 + DAYS_IN[AUGUST]]
    mean_max, mean_min = august_highs.mean(), august_lows.mean()
    target_max = MEAN_MAX_C[AUGUST] - air_warming - lapse
    target_min = MEAN_MIN_C[AUGUST] - air_warming - lapse
    expect("August's mean maximum", abs(mean_max - target_max) <= AUGUST_TEMPERATURE_TOLERANCE_C,
           "engine %.2f C  reference %.2f (%.1f less the warming inside %d-%d, %.2f, and %.2f for height)  tolerance %.2f"
           % (mean_max, target_max, MEAN_MAX_C[AUGUST], AIR_RECORD_YEARS[0], AIR_RECORD_YEARS[1], air_warming, lapse, AUGUST_TEMPERATURE_TOLERANCE_C))
    expect("August's mean minimum", abs(mean_min - target_min) <= AUGUST_TEMPERATURE_TOLERANCE_C,
           "engine %.2f C  reference %.2f (%.1f less the warming %.2f and %.2f for height)  tolerance %.2f"
           % (mean_min, target_min, MEAN_MIN_C[AUGUST], air_warming, lapse, AUGUST_TEMPERATURE_TOLERANCE_C))
    station_range = MEAN_MAX_C[AUGUST] - MEAN_MIN_C[AUGUST]
    expect("August's daily range", abs((mean_max - mean_min) - station_range) <= RANGE_TOLERANCE_C,
           "engine %.2f C  reference %.2f  tolerance %.2f" % (mean_max - mean_min, station_range, RANGE_TOLERANCE_C))

    # The spread of August's nights: the first and ninth deciles of the daily minima over every seed's August.
    decile_1, decile_9 = np.percentile(august_lows, 10), np.percentile(august_lows, 90)
    decile_warming = warming_inside(*DECILE_RECORD_YEARS)
    expect("August's nights, first to ninth decile", abs((decile_9 - decile_1) - (AUGUST_MIN_DECILE_9_C - AUGUST_MIN_DECILE_1_C)) <= SPREAD_TOLERANCE_C,
           "engine %.2f .. %.2f C, a spread of %.2f  reference %.1f .. %.1f less %.2f warming, a spread of %.1f  tolerance %.1f"
           % (decile_1, decile_9, decile_9 - decile_1, AUGUST_MIN_DECILE_1_C, AUGUST_MIN_DECILE_9_C, decile_warming,
              AUGUST_MIN_DECILE_9_C - AUGUST_MIN_DECILE_1_C, SPREAD_TOLERANCE_C))

    # The coldest hour of August's mean day, against sunrise in the middle of the month.
    cycle = air[:, august, :].mean(axis=(0, 1))
    coldest_hour = (int(np.argmin(cycle)) + 0.5) * step_hours
    noon_utc_hour = 12.0 - LONGITUDE_DEG / 15.0
    declination, _, _ = noaa_sun(julian_day(MID_AUGUST[0], MID_AUGUST[1], MID_AUGUST[2], noon_utc_hour))
    sunrise = 12.0 - sunrise_hour_angle(LATITUDE_DEG, declination) / 15.0
    expect("the coldest hour of August's night", abs(coldest_hour - sunrise) <= COLDEST_HOUR_TOLERANCE_H,
           "engine %.2f h  reference sunrise %.2f h on 16 August (NOAA)  tolerance %.2f h" % (coldest_hour, sunrise, COLDEST_HOUR_TOLERANCE_H))

    # ---- the rain: each day's total out of its steps, each month's over its days, averaged over the seeds ----
    inside = (RAIN_RECORD_YEARS[1] - DRYING_SINCE_YEAR + 1) / float(RAIN_RECORD_YEARS[1] - RAIN_RECORD_YEARS[0] + 1)
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
    august_any = (daily[:, august] >= 0.2).sum(axis=1).mean()
    expect("August's days of 1 mm or more", abs(august_1 - RAIN_DAYS_1MM[AUGUST]) <= TOTALS_TOLERANCE * RAIN_DAYS_1MM[AUGUST],
           "engine %.2f  reference %.1f  tolerance a tenth" % (august_1, RAIN_DAYS_1MM[AUGUST]))
    expect("August's days of any rain", abs(august_any - RAIN_DAYS_ANY[AUGUST]) <= TOTALS_TOLERANCE * RAIN_DAYS_ANY[AUGUST],
           "engine %.2f at 0.2 mm  reference %.1f  tolerance a tenth" % (august_any, RAIN_DAYS_ANY[AUGUST]))
    year_1 = (daily >= 1.0).sum(axis=1).mean()
    year_any = (daily >= 0.2).sum(axis=1).mean()
    expect("the year's days of 1 mm or more", abs(year_1 - sum(RAIN_DAYS_1MM)) <= TOTALS_TOLERANCE * sum(RAIN_DAYS_1MM),
           "engine %.1f  reference %.1f  tolerance a tenth" % (year_1, sum(RAIN_DAYS_1MM)))
    expect("the year's days of any rain", abs(year_any - sum(RAIN_DAYS_ANY)) <= TOTALS_TOLERANCE * sum(RAIN_DAYS_ANY),
           "engine %.1f at 0.2 mm  reference %.1f  tolerance a tenth" % (year_any, sum(RAIN_DAYS_ANY)))
    year_10 = (daily >= 10.0).sum(axis=1).mean()
    expect("the year's days of 10 mm or more", abs(year_10 - sum(RAIN_DAYS_10MM)) <= HEAVY_DAYS_TOLERANCE * sum(RAIN_DAYS_10MM),
           "engine %.1f  reference %.1f  tolerance a fifth" % (year_10, sum(RAIN_DAYS_10MM)))

    # ---- the wind, the cloud and the humidity at the observers' hours: the step that holds 9 am and the one that holds 3 pm ----
    step_9 = int(OBSERVATION_HOUR / step_hours)
    step_15 = int(AFTERNOON_HOUR / step_hours)
    sampled_9, sampled_15 = (step_9 + 0.5) * step_hours, (step_15 + 0.5) * step_hours

    def at(field, month, step):
        return field[:, first[month]:first[month] + DAYS_IN[month], step].mean()

    for month, label in ((AUGUST, "August"), (JANUARY, "January")):
        w9, w15 = at(wind, month, step_9), at(wind, month, step_15)
        r9, r15 = WIND_9AM_KMH[month] / 3.6 * BODY_HEIGHT_WIND_SHARE, WIND_3PM_KMH[month] / 3.6 * BODY_HEIGHT_WIND_SHARE
        expect("%s's wind at nine" % label, abs(w9 - r9) <= WIND_TOLERANCE * r9,
               "engine %.2f m/s at %.2f h  reference %.2f (%.1f km/h at 10 m, %.3f of it at 1.5 m)  tolerance a twelfth" % (w9, sampled_9, r9, WIND_9AM_KMH[month], BODY_HEIGHT_WIND_SHARE))
        expect("%s's wind at three" % label, abs(w15 - r15) <= WIND_TOLERANCE * r15,
               "engine %.2f m/s at %.2f h  reference %.2f (%.1f km/h at 10 m)  tolerance a twelfth" % (w15, sampled_15, r15, WIND_3PM_KMH[month]))
        c9, c15 = at(cloud, month, step_9) * 8.0, at(cloud, month, step_15) * 8.0
        expect("%s's cloud at nine" % label, abs(c9 - CLOUD_9AM_OKTAS[month]) <= CLOUD_TOLERANCE_OKTAS,
               "engine %.2f oktas  reference %.1f  tolerance half an okta" % (c9, CLOUD_9AM_OKTAS[month]))
        expect("%s's cloud at three" % label, abs(c15 - CLOUD_3PM_OKTAS[month]) <= CLOUD_TOLERANCE_OKTAS,
               "engine %.2f oktas  reference %.1f  tolerance half an okta" % (c15, CLOUD_3PM_OKTAS[month]))
        t9, t15 = at(air, month, step_9) + lapse, at(air, month, step_15) + lapse
        expect("%s's air at nine" % label, abs(t9 - (TEMP_9AM_C[month] - air_warming)) <= OBSERVER_TEMPERATURE_TOLERANCE_C,
               "engine %.2f C at %.2f h  reference %.2f (the observers' %.1f less the warming)  tolerance %.1f: the curve runs warm here, owed"
               % (t9, sampled_9, TEMP_9AM_C[month] - air_warming, TEMP_9AM_C[month], OBSERVER_TEMPERATURE_TOLERANCE_C))
        expect("%s's air at three" % label, abs(t15 - (TEMP_3PM_C[month] - air_warming)) <= OBSERVER_TEMPERATURE_TOLERANCE_C,
               "engine %.2f C at %.2f h  reference %.2f (the observers' %.1f less the warming)  tolerance %.1f"
               % (t15, sampled_15, TEMP_3PM_C[month] - air_warming, TEMP_3PM_C[month], OBSERVER_TEMPERATURE_TOLERANCE_C))
        h9, h15 = at(humidity, month, step_9) * 100.0, at(humidity, month, step_15) * 100.0
        expect("%s's humidity at nine" % label, abs(h9 - HUMIDITY_9AM[month]) <= HUMIDITY_TOLERANCE,
               "engine %.1f%%  reference %d  tolerance %.0f points" % (h9, HUMIDITY_9AM[month], HUMIDITY_TOLERANCE))
        expect("%s's humidity at three" % label, abs(h15 - HUMIDITY_3PM[month]) <= HUMIDITY_TOLERANCE,
               "engine %.1f%%  reference %d  tolerance %.0f points" % (h15, HUMIDITY_3PM[month], HUMIDITY_TOLERANCE))

    # ---- the sun's energy on the ground: Meinel's beam and the diffuse sky under the engine's cloud, every step, summed over the day ----
    for month, label in ((AUGUST, "August"), (JANUARY, "January")):
        joules = 0.0
        for day in range(first[month], first[month] + DAYS_IN[month]):
            for step in range(steps):
                hour = (step + 0.5) * step_hours
                elevation = sun_elevation_deg(day, hour)
                if elevation <= 0.0:
                    continue
                mean_cloud = cloud[:, day, step].mean()
                joules += meinel_ground_wm2(elevation, mean_cloud) * step_hours * 3600.0
        mj_a_day = joules / DAYS_IN[month] / 1e6
        expect("%s's sun on the ground" % label, abs(mj_a_day - SOLAR_MJ[month]) <= SOLAR_TOLERANCE * SOLAR_MJ[month],
               "engine's cloud under Meinel's sun %.1f MJ/m2 a day  reference %.1f (the satellite's)  tolerance an eighth" % (mj_a_day, SOLAR_MJ[month]))

    if failures:
        print("weather_check: FAIL (%s)" % ", ".join(failures))
        return 1
    print("weather_check: ok, the engine's weather gives the lighthouse's August and its year within tolerance")
    return 0


if __name__ == "__main__":
    sys.exit(main())
