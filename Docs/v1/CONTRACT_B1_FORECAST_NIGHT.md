# Contract B1 — the forecast and the night are assembled from the same facts

**Status: binding contract, written before code (delivery rule, ROADMAP). Author: FABLE,
2026-08-31, on MAIN DEV's request. Implementation: MAIN DEV. Review: REVIEWER, against this
document line by line.**

## The problem, precisely

The loss **arithmetic** is already unified: `BodyState.SensibleLossW` (BodyState.cs:625) is one
method both `Tick` and `Forecast` call — that battle was won in 2.7. What still disagrees is the
**assembly of inputs** each side feeds it. Four axes:

1. **Wind.** The oracle forecasts with the outside wind (`Situation.WindMs`) while the lived
   night ticks with `shelter.WindInsideMs(outside)` — a sleeping founder is becalmed by their own
   walls and the forecast doesn't know it.
2. **Sky view.** `Forecast` takes `bool sheltered` and zeroes the sky term; `Tick` uses the
   continuous `SkyViewFactor` through `SkyExcessLossW`. The bool is manufactured in `Tablet.Read`
   by thresholding `Enclosure01 > 0.35` — a lossy second opinion about the same wall mass.
3. **Wall clo.** `Situation.ShelterWallClo` is evaluated at one wind, the live body's clo
   composition at another; nothing names which wind `WallClo` is *supposed* to take.
4. **The coldest hour.** The forecast prices the whole night at `NightMinimumC`; the lived night
   follows the diurnal curve and only touches the minimum near dawn.

## The rulings

### R1 — Wind: name the wind by where it blows

**`WallClo(outsideWindMs)` and `WindInsideMs(outsideWindMs)` take the OUTSIDE wind** — wind is a
fact about the air hitting the wall. **Every convective term on the body takes the wind at the
skin**: `WindInsideMs(outside)` when the body is (or will be) in the shelter, the outside wind
otherwise. This is already how `Tick`'s callers behave (`ForecastMatchesTheNightTests.CoreAtDawn`
line 50); the forecast conforms to it, not the reverse.

### R2 — Sky view: the bool dies

`Forecast` stops taking `bool sheltered` for the sky and instead overrides `SkyViewFactor` the
same save/override/restore way it already handles `ClothingClo` and `SkyTemperatureC`: a new
optional parameter `skyView01 = double.NaN` (NaN = the body's own reading). The sky term is
always `SkyExcessLossW` scaled by that view — one formula, continuous, in both paths.
`Situation.ShelterBreaksTheSky` survives only as display language ("under cover"); it may no
longer feed arithmetic. `Situation` gains the continuous reading instead (see R5).

### R3 — Wall clo: evaluated once, at the named wind

`WallClo` is computed in exactly one place per night — the assembly (R5) — at the outside wind
per R1, and the resulting clo is what both the forecast and the live body's composition use.
If `Survival`'s live composition currently evaluates it at a different wind, `Survival` conforms.

### R4 — The coldest hour: the forecast is the night at its minimum, and says so

The forecast deliberately prices the whole night at `NightMinimumC` with the two-phase shivering
arithmetic. That is a **stated pessimism**, not a bug — the founder plans against the worst hour.
The contract makes it safe by bounding it in both directions (T2, T3): the forecast may never be
more optimistic than the lived night, and may not be so pessimistic that it wastes an afternoon.
(Integrating the real curve is explicitly out of scope for B1; if T3's bound cannot be met at the
stated budget, escalate back to FABLE rather than widening the budget — law 8.)

### R5 — One assembly, owned

A new Sim type owns the gathering of a night's inputs:

```csharp
/// <summary>Everything a night is made of, assembled once, read by everyone.</summary>
public readonly struct NightConditions
{
    public readonly double AirC;              // the night's minimum, R4
    public readonly double WindAtBodyMs;      // R1: inside wind if sheltered, outside otherwise
    public readonly double SkyView01;         // R2: continuous, from the shelter's real geometry
    public readonly double EffectiveClo;      // worn + WallClo(outside wind), R3
    public readonly double GroundConductanceWm2K;
    public readonly double RadiantGainW;
    public readonly double Hours;
}
```

with one builder (suggested home: `Sim/Knowledge/Night.cs`) that takes the same primitives
`Tablet.Read` already gathers — worn clo, the `DebrisShelter`, outside wind, fire — and applies
R1–R3. `Oracle.TheNight` builds its `actual`/`bare`/`roofed`/`unwalled` variants **only** by
building `NightConditions` with pieces removed, never by re-deriving a number inline. The
night-living test harness (`CoreAtDawn`) ticks from the same struct's fields. That is the whole
point: the forecast and the lived night can then only disagree about *time*, never about *inputs*.

## The tests (extend `ForecastMatchesTheNightTests`)

- **T1 — same inputs, same watts.** Build one `NightConditions`; feed `Forecast` and a single
  `Tick` step at identical state; the forecast's `LossW` equals the tick's sensible + sky +
  respiratory losses within 1e-6 W. This pins the assembly seam itself — if someone adds a term
  to one side, T1 fails the same day.
- **T2 — promised nights are survived** (exists; keeps its non-vacuity count) — now run over a
  grid: wind {0, 1.5, 6} m/s × walls {0, 30, 80} kg × bedding {0, 12, 40} kg.
- **T3 — refusals are honest.** For every grid night the forecast refuses, the lived night's
  dawn core is below `HypothermiaC + 1.5 °C`. The 1.5 °C is this contract's §37 error budget for
  R4's stated pessimism; it goes in the test as a named constant with this document cited.
- **T4 — the axes actually flow.** At fixed everything-else, moving outside wind 1.5 → 6 m/s must
  change `actual.LossW` by less inside a walled shelter than in the open (proves R1's inside wind
  is live), and wall mass 30 → 80 kg must reduce the sky term continuously (proves R2's view is
  live, not a threshold).

## Order of work (MAIN DEV)

1. `NightConditions` + builder, with T1 red-green first.
2. `Forecast` gains `skyView01`; the `sheltered` bool's sky meaning is removed (grep every call
   site — `Oracle.TheNight` passes it four times; the self-tests and
   `ForecastMatchesTheNightTests.Founder/CoreAtDawn` also construct nights).
3. `Tablet.Read`/`Situation`: add the continuous readings, demote `ShelterBreaksTheSky` to prose.
4. Re-point `Oracle.TheNight`'s four variants at the builder; delete the inline assemblies.
5. Grid tests T2–T4. Expect T3 to expose the real disagreement first — that is it working.
6. `Diagnosis.NightForecast`'s sky line updates to quote the continuous view.

Numbers in other tests **will** move (the whole point). A moved number is renegotiated against
its published referent, never against "what the test said yesterday" — if a pinned value must
change, the commit says which referent justifies the new one.

## Out of scope

Diurnal-curve integration in the forecast (R4); weather/cloud cover (Phase 3.1); any change to
`SensibleLossW`'s physics; save-format changes (none of this state is saved).
