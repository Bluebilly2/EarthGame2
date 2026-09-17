#!/usr/bin/env python3
"""knap_check.py: the first stone (FP.3) as a knap run recorded it, held to the fracture mechanics restated here.

Reference: Auerbach's law (F. Auerbach, "Messung der Haertigkeit der Koerper", Annalen der Physik 279, 1891; B. R. Lawn,
Fracture of Brittle Solids, 2nd ed., Cambridge 1993, ch. 8): the load that starts a Hertzian cone crack rises with the
radius of what strikes, and the cone's angle is a property of the material, not of the load, so what a harder blow buys is
a bigger fracture and not a different one. The game's numbers are v1's Knapping (Assets/EarthGame/Sim/Materials/Knapping.cs,
ported to FP.3): the critical energy 2825 x the core's fracture toughness x the hammer's radius squared, the swing's energy
from the wind-up, the hammer's mass and the body's work under a 40 J arm, a flake of 0.0022 kg a joule above the critical
between a floor of 0.010 kg a kilogram of hammer and a ceiling of 0.22 of the core, shatter past 40 J a kilogram of core, a
hammer under 0.15 kg driving no cone, the edge the stone's own docked 0.35 for a blow four times the critical, and the
platform worked 4.5 degrees plus five for the overshoot toward the dead angle of 90. The stones' mineralogy is v1's
StoneType (density, Mohs hardness, conchoidal fracture, grain, fracture toughness; silcrete after Webb and Domanski,
Archaeometry 50, 2008), from which the edge a stone can hold and whether it knaps at all are derived here again. The body's
work capacity is v1's BodyState table over the water lost. Source: the run's run.jsonl (eg2.run, ARCHITECTURE section 10)
alone, read with the standard library: every `blow` record carries the hammer, the core as it was, the wind-up the client
sent and the founder's water, and this check works out from those what the stone should have done and holds the recorded
outcome, the flake's mass and edge and the core's new mass and platform to it. Every number below is written here again,
not read from the engine, so a slip in the engine is a red row.

The tolerances carry two things the record cannot say exactly: the state travels as 32-bit floats (seven figures), and the
water the client records is the server's as of the last word, a second old at most, which moves the capacity by less than
a part in ten thousand at a full body.

Usage: python Tools/verifiers/checks/knap_check.py Artefacts/frames/knap-<stamp>
Exit 0 when every row passes, 1 otherwise; prints each row's actual and required beside its verdict.
"""
import json
import math
from pathlib import Path
import sys

# v1's Knapping, restated.
CRITICAL_CONSTANT = 2825.0
MINIMUM_KNAPPABILITY = 0.25
FLAKE_KG_PER_J = 0.0022
MINIMUM_FLAKE_PER_HAMMER_KG = 0.010
MAX_FLAKE_SHARE = 0.22
SHATTER_J_PER_KG = 40.0
MINIMUM_HAMMER_KG = 0.15
ARM_CEILING_J = 40.0
USABLE_EDGE = 0.20
USABLE_FLAKE_KG = 0.0025
SPENT_KG = 0.12
DEAD_PLATFORM_DEG = 90.0
FRESH_PLATFORM_DEG = 68.0
PLAIN_COBBLE_DENSITY = 2600.0     # the plain cobble's stated density, which a hammer of no named stone is taken at
# v1's StoneType: name -> (density kg/m3, Mohs, conchoidal fracture 0..1, grain mm, fracture toughness MPa m^0.5).
STONES = {
    "Flint": (2600.0, 7.0, 0.95, 0.001, 0.9),
    "Chert": (2600.0, 6.8, 0.85, 0.004, 1.0),
    "Obsidian": (2400.0, 5.5, 0.99, 0.0001, 0.7),
    "Quartzite": (2650.0, 7.0, 0.55, 0.3, 1.8),
    "Sandstone": (2300.0, 6.5, 0.10, 0.5, 1.2),
    "Basalt": (2900.0, 6.0, 0.45, 0.5, 2.2),
    "Granite": (2700.0, 6.5, 0.20, 3.0, 2.5),
    "Shale": (2400.0, 3.0, 0.15, 0.02, 0.5),
    "Silcrete": (2600.0, 7.0, 0.80, 0.05, 1.1),
    "Quartz": (2650.0, 7.0, 0.50, 0.5, 1.3),
    "Rhyolite": (2500.0, 6.5, 0.70, 0.05, 1.3),
}
# v1's BodyState: the work a body can do against the fraction of its water lost, straight between the points.
CAPACITY_POINTS = ((0.00, 1.00), (0.02, 0.97), (0.04, 0.90), (0.06, 0.75), (0.10, 0.50), (0.15, 0.15))
MASS_TOLERANCE_KG = 2e-5
EDGE_TOLERANCE = 2e-4
PLATFORM_TOLERANCE_DEG = 0.02
FRAMES = 4                 # two names at two sizes


def check(name, actual, required, good):
    print("%s %s: actual=%r; required=%r" % ("PASS" if good else "FAIL", name, actual, required), flush=True)
    return 0 if good else 1


def clamp01(x):
    return max(0.0, min(1.0, x))


def edge_quality(stone):
    _, mohs, conchoidal, grain, _ = STONES[stone]
    fine = 1.0 / (1.0 + grain * 4.0)
    hard = clamp01((mohs - 3.0) / 4.0)
    return clamp01(conchoidal * fine * (0.35 + 0.65 * hard))


def knappability(stone):
    _, _, conchoidal, _, toughness = STONES[stone]
    tough = clamp01(1.0 - (toughness - 0.3) / 2.2)
    return clamp01(conchoidal * (0.3 + 0.7 * tough))


def hammer_radius_m(density, mass_kg):
    return (3.0 * (max(1e-6, mass_kg) / density) / (4.0 * math.pi)) ** (1.0 / 3.0)


def critical_energy_j(core_stone, hammer_density, hammer_mass_kg):
    r = hammer_radius_m(hammer_density, hammer_mass_kg)
    return CRITICAL_CONSTANT * STONES[core_stone][4] * r * r


def capacity(water):
    loss = clamp01(1.0 - water)
    for (l0, c0), (l1, c1) in zip(CAPACITY_POINTS, CAPACITY_POINTS[1:]):
        if loss <= l1:
            return c0 + (c1 - c0) * (loss - l0) / (l1 - l0)
    return CAPACITY_POINTS[-1][1]


def swing_energy_j(wind_up, hammer_mass_kg, work_capacity):
    speed = 1.4 + 5.4 * clamp01(wind_up)
    cap = 0.55 + 0.45 * clamp01(work_capacity)
    swung = 0.5 * max(0.05, hammer_mass_kg) * speed * speed
    return min(swung, ARM_CEILING_J * clamp01(wind_up)) * cap


def strike(core_stone, core_mass, platform, hammer_density, hammer_mass, energy):
    """What the stone does with the blow: the outcome as the wire names it, the flake's mass and edge, and the core after."""
    if core_mass <= SPENT_KG:
        return "Crushed", 0.0, 0.0, core_mass, platform
    if hammer_mass < MINIMUM_HAMMER_KG:
        return "Bounced", 0.0, 0.0, core_mass, platform
    if knappability(core_stone) < MINIMUM_KNAPPABILITY:
        return "Crushed", 0.0, 0.0, core_mass - core_mass * 0.02, min(140.0, platform + 3.0)
    if platform >= DEAD_PLATFORM_DEG:
        return "Crushed", 0.0, 0.0, core_mass - core_mass * 0.01, min(140.0, platform + 1.0)
    critical = critical_energy_j(core_stone, hammer_density, hammer_mass)
    if energy < critical:
        return "Bounced", 0.0, 0.0, core_mass, platform
    if energy > core_mass * SHATTER_J_PER_KG:
        return "Shattered", 0.0, 0.0, core_mass - core_mass * 0.55, min(140.0, platform + 14.0)
    excess = energy - critical
    ceiling = core_mass * MAX_FLAKE_SHARE
    floor = MINIMUM_FLAKE_PER_HAMMER_KG * hammer_mass
    if floor > ceiling:
        return "Shattered", 0.0, 0.0, core_mass - core_mass * 0.5, min(140.0, platform + 14.0)
    flake = min(max(FLAKE_KG_PER_J * excess, floor), ceiling)
    overshoot = clamp01((energy / critical - 1.0) / 4.0)
    edge = edge_quality(core_stone) * (1.0 - 0.35 * overshoot)
    return "Flaked", flake, edge, core_mass - flake, min(140.0, platform + 4.5 + overshoot * 5.0)


def main(argv):
    if len(argv) != 2:
        print(__doc__)
        return 2
    run = Path(argv[1]) / "run.jsonl"
    if not run.is_file():
        print("no run.jsonl at %s" % run)
        return 2
    records = [json.loads(line) for line in run.read_text(encoding="utf-8").splitlines() if line.strip()]
    blows = [r for r in records if r.get("kind") == "blow"]
    end = next((r for r in reversed(records) if r.get("kind") == "end"), {})
    failures = 0
    failures += check("knap.blows_recorded", len(blows), ">= 3 (a tap, a measured blow, a full swing)", len(blows) >= 3)

    flakes = []
    for r in blows:
        name = str(r.get("name"))
        core_stone = r.get("core_stone") or ""
        if core_stone not in STONES:
            failures += check("knap.%s.core_stone_known" % name, core_stone, "one of %s" % ", ".join(sorted(STONES)), False)
            continue
        hammer_stone = r.get("hammer_stone") or ""
        hammer_density = STONES[hammer_stone][0] if hammer_stone in STONES else PLAIN_COBBLE_DENSITY
        hammer_mass = float(r["hammer_mass_kg"])
        core_mass = float(r["core_mass_before_kg"])
        platform = float(r.get("core_platform_before_deg", FRESH_PLATFORM_DEG))
        wind_up = float(r["wind_up"])
        work = capacity(float(r.get("water", 1.0)))
        energy = swing_energy_j(wind_up, hammer_mass, work)
        critical = critical_energy_j(core_stone, hammer_density, hammer_mass)
        outcome, flake_kg, edge, mass_after, platform_after = strike(core_stone, core_mass, platform, hammer_density, hammer_mass, energy)
        failures += check("knap.%s.outcome" % name, "%s (recorded; %.2f J behind the blow against %.2f J to start a fracture in %s)"
                          % (r.get("outcome"), energy, critical, core_stone.lower()), outcome, r.get("outcome") == outcome)
        if outcome == "Flaked":
            got_mass = float(r.get("flake_mass_kg", float("nan")))
            got_edge = float(r.get("flake_edge", float("nan")))
            failures += check("knap.%s.flake_mass_kg" % name, round(got_mass, 6), "%.6f within %g" % (flake_kg, MASS_TOLERANCE_KG), abs(got_mass - flake_kg) <= MASS_TOLERANCE_KG)
            failures += check("knap.%s.flake_edge" % name, round(got_edge, 5), "%.5f within %g" % (edge, EDGE_TOLERANCE), abs(got_edge - edge) <= EDGE_TOLERANCE)
            failures += check("knap.%s.flake_is_of_the_cores_stone" % name, r.get("flake_key"), "item/flake-" + core_stone.lower(), r.get("flake_key") == "item/flake-" + core_stone.lower())
            flakes.append((got_mass, got_edge))
        elif outcome == "Bounced":
            failures += check("knap.%s.nothing_changed" % name, (r.get("core_mass_after_kg"), "flake_mass_kg" in r), (core_mass, False),
                              not r.get("core_gone") and abs(float(r.get("core_mass_after_kg", -1.0)) - core_mass) <= MASS_TOLERANCE_KG and "flake_mass_kg" not in r)
        if outcome != "Bounced":
            if mass_after <= SPENT_KG:
                failures += check("knap.%s.core_gone_when_spent" % name, r.get("core_gone"), True, r.get("core_gone") is True)
            else:
                got_after = float(r.get("core_mass_after_kg", float("nan")))
                got_platform = float(r.get("core_platform_after_deg", float("nan")))
                failures += check("knap.%s.core_mass_after_kg" % name, round(got_after, 6), "%.6f within %g" % (mass_after, MASS_TOLERANCE_KG), abs(got_after - mass_after) <= MASS_TOLERANCE_KG)
                failures += check("knap.%s.core_platform_after_deg" % name, round(got_platform, 4), "%.4f within %g" % (platform_after, PLATFORM_TOLERANCE_DEG),
                                  abs(got_platform - platform_after) <= PLATFORM_TOLERANCE_DEG)
                if outcome == "Flaked":
                    failures += check("knap.%s.core_counts_its_flake" % name, r.get("core_flakes_after"), int(r.get("core_flakes_before", 0)) + 1,
                                      r.get("core_flakes_after") == int(r.get("core_flakes_before", 0)) + 1)

    # The path's three things: the wrong blow teaches (the tap bounced), an edge was made that is a tool, and two flakes off
    # one core are different tools.
    tap = next((r for r in blows if r.get("name") == "tap"), None)
    failures += check("knap.tap_bounced", tap.get("outcome") if tap else None, "Bounced", tap is not None and tap.get("outcome") == "Bounced")
    usable = [(m, e) for m, e in flakes if m >= USABLE_FLAKE_KG and e >= USABLE_EDGE]
    failures += check("knap.a_usable_flake_came_away", "%d of %d flakes" % (len(usable), len(flakes)), ">= 1 with mass >= %g kg and edge >= %g" % (USABLE_FLAKE_KG, USABLE_EDGE), len(usable) >= 1)
    if len(flakes) >= 2:
        differ = abs(flakes[0][0] - flakes[1][0]) > MASS_TOLERANCE_KG or abs(flakes[0][1] - flakes[1][1]) > EDGE_TOLERANCE
        failures += check("knap.two_flakes_off_one_core_differ", [(round(m, 5), round(e, 4)) for m, e in flakes[:2]], "different masses or edges", differ)
    else:
        failures += check("knap.two_flakes_off_one_core_differ", len(flakes), ">= 2 flakes", False)
    failures += check("end.flake_held", end.get("flake_held"), True, end.get("flake_held") is True)
    failures += check("end.errors", end.get("errors"), 0, end.get("errors") == 0)
    failures += check("end.frames", end.get("frames"), FRAMES, end.get("frames") == FRAMES)
    print("knap_check %s: %s" % ("GREEN" if failures == 0 else "RED", argv[1]))
    return 1 if failures else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
