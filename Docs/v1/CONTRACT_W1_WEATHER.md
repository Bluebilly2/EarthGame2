# Contract W1 — weather: one new cause for six old consequences

**Status: binding contract, written before code. Author: FABLE, 2026-09-01, from MAIN DEV 2's
`AUDIT_WEATHER_V1.md` — the audit is part of this contract and its measurements are the ground
truth here. Implementation: MAIN DEV 2 on `track-sim`. Review: REVIEWER against both documents.
Serves: FOUNDERS_PATH Act II (the rain that proves the camp) and the living world.**

## The audit's finding, adopted as the design

Every consequence of rain already exists and answers to `Moisture01` — insulation collapse,
refused tinder, drowned fires, plateaued drills, the felt damp bed. **W1 adds causes, never
consequences.** A source-text test enforces the principle: no `rain` branch may appear in
`Fire`, `DebrisShelter` or `BodyState` — the claimed-removal pattern guarding an architecture
instead of a threshold. Anything that wants a rain special-case is the named bug shape asking
to be born.

## The climate baseline — owner-ruled canon (2026-09-01)

The 1.7 °C discrepancy against the co-located BoM station is settled by the owner's word,
reasoning verbatim: *"a. if the player is going to be seeing megafauna roaming around, b
wouldn't make sense."* **The world's climate is pre-human** — "minus everything humanity has
changed" includes the changed climate, in the same counterfactual that keeps the megafauna
alive. Therefore: the target is the station record **minus the anthropogenic warming signal
for the region** (a named, cited constant — regional attribution, not a global figure), the
station's diurnal and seasonal **shape** stays fully binding (the offset is a level), and the
residual formula error beyond the offset is corrected, not excused. `Climate.cs:11`'s comment
gets the truth it now has; this section is the baseline fact's one owner and everything cites
it here.

## Rulings the audit requested, made here

1. **Gather-moisture moves to the weather model's ownership.** `GroundCover.MoistureAt` and its
   hardcoded rake depths become readers of the Sim-owned ground-wetness fact W1 creates; the
   Scripts side keeps only the raycast-and-ask. Two opinions about how wet the world is would be
   law 3 at landscape scale.
2. **Rain occurrence is `SimRandom`-seeded** (deterministic under law 4, replayable
   bit-for-bit), because seeding gives fronts and dry spells rather than sine-wave drizzle.
3. **`BodyState.RelativeHumidity01` gets its owner** — written by `Weather`, its default-0.70
   life over. The audit's third law-3 instance, closed by the wiring that was always intended.
4. **Wind gains a bearing inside `Weather` from day one; shelter orientation is deferred** to
   its own post-W1 change set. A fact the world carries beats a fact retrofitted, and v1 stays
   finishable.

## The work

- **W1a — `Weather`, the sky at an instant.** A readonly struct assembled once per tick and
  read by everyone — `NightConditions`' sibling, for the same reason: the forecast and the lived
  hour must be structurally unable to diverge. Carries rain rate (mm/h), air-temperature anomaly
  (cold snaps as one function every consumer inherits — the tablet's `NightMinimumC` sees a snap
  coming for free), wind speed and bearing, humidity, cloud. `CloudCover01`'s two sines retire
  in favour of the seeded front model.
- **W1b — wetting and drying from one equation.** The published dead-fuel timelag model,
  `dm/dt = (EMC − m) / timelag`, with the 1-h/10-h/100-h classes; **timelag lives on the
  material beside its other physical properties**, never per recipe. This single law is the
  rain soaking a shelter AND the drying rack earning its place AND the bed drying by the fire —
  the write-once moisture era ends. Stable at 12× and 90× by construction (the audit's
  step-size argument), and proven so by test.
- **W1c — rain reaches things honestly.** Exposure through `DebrisShelter.SkyView01` (B1's
  continuous reading pays twice); ground wetness for gather-moisture per ruling 1; items in the
  open wet by class, items under cover by what the cover admits.
- **W1d — wet skin, its own change set.** Insulation collapse plus non-sweat evaporation
  through existing terms; it alone touches near `SensibleLossW`, so it lands separately with
  its own calibration check. Act II's "cold compounded by wet" becomes real here.

## Order of proof (the B1 pattern, demanded not suggested)

The harness comes first: a modelled August at the wake point checked against the named BoM
station climatology (rainfall total, rain-days — the same anchor `Climate` already cites for
temperature) **before anything the model says is believed**. Then the audit's six tests,
including the no-rain-branch source rule, each able to fail, each sabotaged once. T-grid
interaction: W1 must leave B1's 27-night grid green untouched — a weather layer that moves the
clear-night forecast has a bug by definition.

## Out of scope

Shelter orientation (deferred by ruling 4); storms as scripted events (fronts emerge from the
seeded model or not at all); snow and anything the wake point's climatology does not produce in
the beta's season; visual rain (GFX's, later, reading `Weather` like everyone else).

## Acceptance

Climatology harness within its stated tolerances before merge; suites green with the floor
stated; the six tests in; the no-rain-branch rule sabotaged red once; `-eg-scenario night`
unchanged to the digit under a zero-rain seed; a rained-on camp measurably colder through
existing consequence paths only, shown in the report's numbers.
