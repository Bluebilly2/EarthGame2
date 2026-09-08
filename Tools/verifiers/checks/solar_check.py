#!/usr/bin/env python3
"""solar_check.py: is the engine's sun where an independent almanac puts it?

Runs the engine's almanac tool (Engine/tools/EarthGame.Almanac, format eg2.almanac v1) for the wake day and
compares its declination, sunrise, sunset, noon height and the sun's place at the wake hour with the same
quantities computed HERE by the NOAA solar-position algorithm (the equations of the NOAA Solar Calculator:
Julian century, mean longitude and anomaly, equation of centre, apparent longitude, obliquity), which shares
nothing with the engine's Spencer-series declination. Both sets of numbers are printed side by side; the exit
code is the verdict: 0 within tolerance, 1 outside it, 2 when the engine's tool could not be run.

Independence, stated (CANON ruling 17): the engine's SolarClock takes the declination from Spencer's 1971
Fourier fit and the hour angle from local mean solar time; this file takes the declination from the sun's
apparent ecliptic longitude on the real date (25 August 2026, which is day 237 of that non-leap year) and never
imports or reads the engine. The time basis is the same on both sides, hours from the sun's own noon, so the
equation of time does not enter; the engine's line says so in its time_basis field and this file checks that it
does.

Run from the repository root:  python Tools/verifiers/checks/solar_check.py
"""
import json
import math
import os
import subprocess
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))))
ALMANAC_DLL = os.path.join(ROOT, "Engine", ".build", "bin", "EarthGame.Almanac", "Debug", "net10.0", "EarthGame.Almanac.dll")
ALMANAC_PROJECT = os.path.join(ROOT, "Engine", "tools", "EarthGame.Almanac")

# The place and the day, restated from Docs/ARCHITECTURE.md §3 rather than read from the engine.
LATITUDE_DEG = -35.140
LONGITUDE_DEG = 150.675
YEAR, MONTH, DAY = 2026, 8, 25
DAY_OF_YEAR = 237
WAKE_LOCAL_HOUR = 8.0

# Tolerances. Spencer's Fourier fit, which the engine uses, is good to about a third of a degree of declination
# (the first run measured 0.24 against NOAA on this date), so the declination tolerance is half a degree: it
# passes honest arithmetic and still catches a wrong sign, a wrong hemisphere or a wrong season. Sunrise and
# sunset are held to three minutes, which a wrong latitude or a swapped day-of-year convention cannot pass.
DECLINATION_TOLERANCE_DEG = 0.5
HOURS_TOLERANCE = 0.05          # three minutes
ELEVATION_TOLERANCE_DEG = 0.5   # the reference includes no refraction either; 0.5 is a wide margin
AZIMUTH_TOLERANCE_DEG = 1.0


def engine_line(day):
    """The engine's almanac line for a day, as a dictionary, or None with a reason printed."""
    if os.path.isfile(ALMANAC_DLL):
        command = ["dotnet", ALMANAC_DLL, "+day", str(day)]
    else:
        command = ["dotnet", "run", "--project", ALMANAC_PROJECT, "--", "+day", str(day)]
    try:
        result = subprocess.run(command, capture_output=True, text=True, cwd=ROOT)
    except OSError as ex:
        print("cannot run the almanac tool: %s (%s)" % (ex, " ".join(command)))
        return None
    if result.returncode != 0:
        print("the almanac tool exited %d: %s" % (result.returncode, (result.stderr or result.stdout).strip()[:400]))
        return None
    lines = [l for l in result.stdout.splitlines() if l.startswith("{")]
    if not lines:
        print("the almanac tool printed no JSON line; stdout was: %s" % result.stdout.strip()[:400])
        return None
    return json.loads(lines[-1])


def julian_day(year, month, day, hour_utc):
    """Julian Day for a civil date and a UTC hour (Meeus' algorithm for the Gregorian calendar)."""
    if month <= 2:
        year -= 1
        month += 12
    a = year // 100
    b = 2 - a + a // 4
    jd0 = int(365.25 * (year + 4716)) + int(30.6001 * (month + 1)) + day + b - 1524.5
    return jd0 + hour_utc / 24.0


def noaa_sun(jd):
    """Declination (deg), equation of time (min) and the apparent ecliptic longitude (deg) from the NOAA equations."""
    t = (jd - 2451545.0) / 36525.0
    l0 = (280.46646 + t * (36000.76983 + 0.0003032 * t)) % 360.0
    m = 357.52911 + t * (35999.05029 - 0.0001537 * t)
    e = 0.016708634 - t * (0.000042037 + 0.0000001267 * t)
    mr = math.radians(m)
    c = (math.sin(mr) * (1.914602 - t * (0.004817 + 0.000014 * t))
         + math.sin(2 * mr) * (0.019993 - 0.000101 * t)
         + math.sin(3 * mr) * 0.000289)
    true_long = l0 + c
    omega = 125.04 - 1934.136 * t
    apparent_long = true_long - 0.00569 - 0.00478 * math.sin(math.radians(omega))
    obliquity0 = 23.0 + (26.0 + (21.448 - t * (46.815 + t * (0.00059 - 0.001813 * t))) / 60.0) / 60.0
    obliquity = obliquity0 + 0.00256 * math.cos(math.radians(omega))
    declination = math.degrees(math.asin(math.sin(math.radians(obliquity)) * math.sin(math.radians(apparent_long))))
    y = math.tan(math.radians(obliquity / 2.0)) ** 2
    l0r = math.radians(l0)
    eot = 4.0 * math.degrees(y * math.sin(2 * l0r) - 2 * e * math.sin(mr) + 4 * e * y * math.sin(mr) * math.cos(2 * l0r)
                             - 0.5 * y * y * math.sin(4 * l0r) - 1.25 * e * e * math.sin(2 * mr))
    return declination, eot, apparent_long


def sunrise_hour_angle(latitude_deg, declination_deg, horizon_deg=-0.833):
    """Hour angle of sunrise in degrees for the refracted upper limb; NOAA's zenith of 90.833."""
    lat = math.radians(latitude_deg)
    dec = math.radians(declination_deg)
    cos_ha = math.cos(math.radians(90.0 - horizon_deg)) / (math.cos(lat) * math.cos(dec)) - math.tan(lat) * math.tan(dec)
    cos_ha = max(-1.0, min(1.0, cos_ha))
    return math.degrees(math.acos(cos_ha))


def elevation_and_azimuth(latitude_deg, declination_deg, hour_angle_deg):
    """Geometric elevation and compass bearing (0 north, 90 east) of the sun for an hour angle (negative before noon)."""
    lat = math.radians(latitude_deg)
    dec = math.radians(declination_deg)
    ha = math.radians(hour_angle_deg)
    sin_alt = math.sin(lat) * math.sin(dec) + math.cos(lat) * math.cos(dec) * math.cos(ha)
    elevation = math.degrees(math.asin(max(-1.0, min(1.0, sin_alt))))
    azimuth = (math.degrees(math.atan2(math.sin(ha), math.cos(ha) * math.sin(lat) - math.tan(dec) * math.cos(lat))) + 180.0) % 360.0
    return elevation, azimuth


def main():
    engine = engine_line(DAY_OF_YEAR)
    if engine is None:
        print("could not obtain the engine's almanac line; build the engine first: dotnet build Engine/EarthGame.slnx")
        return 2

    failures = []

    def expect(name, condition, detail):
        print("%-22s %s  %s" % (name, "ok " if condition else "FAIL", detail))
        if not condition:
            failures.append(name)

    expect("format", engine.get("format") == "eg2.almanac" and engine.get("version") == 1,
           "format %r version %r" % (engine.get("format"), engine.get("version")))
    expect("time basis", "no equation of time" in str(engine.get("time_basis", "")), repr(engine.get("time_basis")))
    expect("day and place", engine.get("day_of_year") == DAY_OF_YEAR
           and abs(engine.get("latitude_deg", 999) - LATITUDE_DEG) < 1e-6 and abs(engine.get("longitude_deg", 999) - LONGITUDE_DEG) < 1e-6,
           "day %s at %s, %s" % (engine.get("day_of_year"), engine.get("latitude_deg"), engine.get("longitude_deg")))

    # The reference: the sun at local apparent noon on the real date. Local noon is 12 h minus the longitude's
    # share of the day, in UTC; the declination moves a hundredth of a degree over the hours either side.
    noon_utc_hour = 12.0 - LONGITUDE_DEG / 15.0
    declination, eot, _ = noaa_sun(julian_day(YEAR, MONTH, DAY, noon_utc_hour))
    ha = sunrise_hour_angle(LATITUDE_DEG, declination)
    ref_sunrise = 12.0 - ha / 15.0
    ref_sunset = 12.0 + ha / 15.0
    ref_daylight = 2.0 * ha / 15.0
    ref_noon_elevation = 90.0 - abs(LATITUDE_DEG - declination)
    ref_wake_elevation, ref_wake_azimuth = elevation_and_azimuth(LATITUDE_DEG, declination, (WAKE_LOCAL_HOUR - 12.0) * 15.0)

    def compare(name, key, reference, tolerance, unit):
        value = engine.get(key)
        ok = value is not None and abs(value - reference) <= tolerance
        expect(name, ok, "engine %.4f  reference %.4f  difference %.4f  tolerance %.3f %s"
               % (value if value is not None else float("nan"), reference, abs((value or 0.0) - reference), tolerance, unit))

    compare("declination", "declination_deg", declination, DECLINATION_TOLERANCE_DEG, "deg")
    compare("daylight", "daylight_hours", ref_daylight, 2 * HOURS_TOLERANCE, "h")
    compare("sunrise", "sunrise_local_hour", ref_sunrise, HOURS_TOLERANCE, "h")
    compare("sunset", "sunset_local_hour", ref_sunset, HOURS_TOLERANCE, "h")
    compare("noon elevation", "noon_elevation_deg", ref_noon_elevation, ELEVATION_TOLERANCE_DEG, "deg")
    compare("wake elevation", "wake_elevation_deg", ref_wake_elevation, ELEVATION_TOLERANCE_DEG, "deg")
    compare("wake azimuth", "wake_azimuth_deg", ref_wake_azimuth, AZIMUTH_TOLERANCE_DEG, "deg")
    print("(for the record: NOAA's equation of time on this date is %.2f min; neither side uses it)" % eot)

    if failures:
        print("solar_check: FAIL (%s)" % ", ".join(failures))
        return 1
    print("solar_check: ok, the engine's sun agrees with the NOAA reference within tolerance")
    return 0


if __name__ == "__main__":
    sys.exit(main())
