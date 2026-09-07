# Audit — Weather v1, before the contract

**Status:** audit and proposal, produced by MAIN DEV 2 on FABLE's direction, 2026-09-01. Not a
contract and not a change: nothing in `Assets/**` moved for it. FABLE turns this into the contract.

**Scope anchor:** `FOUNDERS_PATH.md` — *"Weather v1: rain, wind shifts, cold snaps driving the
existing thermal model"* (build list, Act II), and Act II's exam: *"a rain day … Wet tinder, a
tested windbreak, the first night the camp earns its keep"*, with the kill condition **"cold
compounded by wet"**. The ROADMAP's 3.1 (*"stochastic weather consistent with climatology"*) is the
later, data-driven version; this is the analytic v1 that has to hold the arc up first.

Method is B1's: audit before contract, and every claim below checked at source rather than
remembered.

---

## 1. What the climate model already owns

`Sim/Body/Climate.cs`, and it is more than the name suggests:

| what | where | shape |
|---|---|---|
| air temperature | `AirTemperatureC(day, hour, altitude)` | seasonal mean + diurnal cosine (min 05:30, max 15:00) − lapse rate 6.5 °C/km |
| seasonal mean | `SeasonalMeanC(day)` | latitude by `cos^1.5`, swing growing with latitude, southern phase shift |
| wind speed | `WindSpeedMs(hour, exposure01)` | diurnal, freshening through the afternoon, scaled by exposure |
| cloud cover | `CloudCover01(day, hour)` | two sines on the date + an afternoon build |
| sky temperature | `SkyTemperatureC(air, cloud)` | air − 16 K × (1 − 0.85·cloud) |
| direct / diffuse solar | `DirectSolarWm2`, `DiffuseSolarWm2` | Meinel air-mass, cloud-killed beam |
| wind chill | `WindChillC(air, wind)` | JAG/NWS, valid below 10 °C |

`Sim/World/SolarClock.cs` owns sun position, day length and the date; `WorldClock` owns the one
absolute instant. `Sim/World/SoilModel.cs` owns `Wetness01`, the topographic wetness index
`ln(a/tanβ)` — a *place* fact, currently feeding soil and vegetation, not weather.

**Everything above is deterministic from date and hour.** Law 4 is satisfied by construction, and
weather v1 must keep that property.

## 2. What the thermal model already consumes

The body is already wired for weather it cannot yet be given (`Sim/Body/BodyState.cs`):

- `Tick(seconds, airC, windAtBody, …, radiantGainW, asleep)` — air and wind per step.
- `SkyViewFactor` + `SkyTemperatureC` → `SkyExcessLossW`. Cloud already changes the night by
  ~30 W through this path.
- `SolarGainW(direct, diffuse, elevation, shade)` — cloud already changes the day by >100 W.
- `TotalInsulationClo(windAtBodyMs, …)` — wind already strips the boundary layer, and clothing
  loses value in wind.
- **`RelativeHumidity01`** — read by `RespiratoryW:888`, **written by nothing**. It has held its
  default 0.70 since it was added. A property read and never written is the mirror of law 3's
  "field written and never read", and weather is its owner.

Since B1, the night's inputs are assembled once in `Sim/Knowledge/Night.cs`. **Weather must feed
that assembly, not go around it** — anything that reaches the body must arrive through
`NightConditions` for the forecast, and through `Survival` for the lived moment, from one source.

## 3. Rain's receiving end is already built, and this is the important finding

Every consequence Act II asks for already exists as a **function of moisture**. What is missing is
anything that *moves* moisture.

| consequence | already implemented | constant |
|---|---|---|
| wet insulation stops insulating | `InsulationMaterial.ConductivityAt(m)` — dry 0.045 → saturated 0.55 W/(m·K), convex, exponent 1.5 | `SaturatedWmK`, `MoistureExponent` |
| a damp bed is felt and reported | `DebrisShelter.BeddingMoisture01`, `MeanMoisture01`, `Assessment` | — |
| wet tinder will not take a coal | `Fire.BestTinder` skips anything wetter | `MaxTinderMoisture = 0.30` |
| a fire drowns | `Fire.Tick:185` | `DrowningMoisture = 0.55` |
| a wet drill never reaches ignition | `FrictionDrill.PlateauC` vs `PlateauDryC` | — |
| the founder is told in words, not numbers | `InsulationMaterial.DescribeMoisture` | — |

So **rain does not need a single new consequence.** It needs one new *cause*. That is the whole
shape of the slice, and it is why weather v1 is cheap for how much it buys: put water on the
existing `Moisture01` fields and six systems respond correctly, for free, with no second
derivation anywhere.

## 4. What does not exist

Checked at source, not assumed.

1. **No precipitation, at all.** `grep -rn -i "precipitat|rainfall|\brain\b"` over `Assets/EarthGame`
   returns two hits, both in `Scripts/Dev/Stubs.cs`, and both say so: *"Rain is the larger piece and
   does not exist in the model at all yet."*

2. **Moisture is write-once.** `ItemState.Moisture01`, `DebrisShelter`'s layer moisture and
   `Fire.Moisture01` are set when the thing is created or fuelled and **never change afterwards**.
   Nothing wets and nothing dries. A shelter gathered dry at noon is still dry after a night of
   rain; one gathered damp never dries by the fire. This is the gap rain must fill, and it is also
   a live design hole independent of rain — a drying rack is on Act II's build list and has nothing
   to act on.

3. **No wind direction.** `WindSpeedMs` returns a scalar; `grep -rn -i "WindDirection|windBearing"`
   returns **nothing**. `DebrisShelter` has no orientation either — `Enclosure01` is a function of
   wall mass alone. So "wind shifts" currently has no fact to shift: a windbreak cannot face the
   wrong way, and the founder's choice of which side to build up cannot be wrong.

4. **No wet-skin term on the body.** `PHYSICS_NAKED_HUMAN.md`'s own "Still to do" names it:
   *"Wet skin and rain, which is the other way a person in a field dies."* Act II's kill condition
   is literally **"cold compounded by wet"**, and the compounding does not exist.

5. **Cloud cannot produce weather.** `CloudCover01` is two sines plus an afternoon term — smooth,
   bounded, and incapable of a front. There are no cold snaps, and no run of days that is wetter
   than its neighbours.

6. **Gather-moisture is owned in the wrong layer.** `Scripts/Shelter/GroundCover.MoistureAt:186`
   computes moisture from dew (hour), canopy and topographic dampness — in **Scripts**, with a
   hardcoded rake depth at both call sites (`Interactor.cs:215`, `FireSelfTest.cs:47`). It is the
   only answer to "how wet is what I just picked up". When rain arrives there will be two opinions
   about how wet the world is unless this moves or is subordinated. **This is the named bug shape
   waiting to happen, and it is the one thing in this slice most likely to bite.**

## 5. The principle, taken from B1

Weather adds **causes**, never consequences. Every phenomenon must arrive through a term the model
already integrates:

- rain acts **only** by moving `Moisture01` and `RelativeHumidity01`;
- a cold snap acts **only** by moving what `AirTemperatureC` returns;
- wind acts **only** through the wind already fed to `WallClo`, `WindInsideMs` and
  `TotalInsulationClo`;
- wet skin acts **only** through insulation and evaporation the body already computes.

No system may gain a "rain penalty". If a consequence cannot be expressed as a change to an
existing input, that is a finding to escalate, not a licence to add a branch.

And, B1's own lesson: **one assembly, one owner.** Weather gets a single Sim type describing the
sky at an instant, built in one place and read by everyone, exactly as `NightConditions` did for
the night. The forecast and the lived weather must not be able to disagree about what the sky is
doing.

## 6. Proposal

### W1 — `Weather`, the state of the sky at an instant

A readonly struct in Sim (suggested `Sim/World/Weather.cs`) carrying what everything downstream
needs, built by one function from `Climate` + the clock:

```
AirC                 // with the anomaly applied (W3)
WindMs               // outside wind, before any wall
WindFromDeg          // bearing, 0 = from the north (W4)
CloudCover01         // as today
RainRateMmPerHour    // 0 when dry (W2)
RelativeHumidity01   // the field nothing currently writes
```

`Climate` stays the owner of the underlying curves; `Weather` is the assembly. `Survival` reads it
instead of calling four `Climate` methods itself, and `Night.For` takes it so the forecast and the
night cannot diverge.

### W2 — Rain, and the moisture it moves

**Occurrence** deterministic from the date, like `CloudCover01`, or seeded via `SimRandom`
(`DeriveSeed(worldSeed, "weather")`) — either satisfies law 4; the seeded form gives fronts and
runs of wet days that sines cannot, and I would take it.

**Rain rate** in mm/h, because that is the unit every published referent uses.

**The moisture model is the published one and is the heart of the proposal.** Dead fuel moisture
follows an **exponential approach to an equilibrium moisture content with a timelag set by the
fuel's size** — the standard 1-h / 10-h / 100-h / 1000-h classes of fire-danger rating, where the
timelag is the time to close 63% of the gap to equilibrium. That gives **wetting and drying from
one mechanism**, which is exactly what a drying rack and a rain day both need:

```
dm/dt = (EMC - m) / timelag
```

with EMC driven up by rain and humidity, down by warm dry air and (near a fire) by radiant heat.
Leaf litter, dry grass and bracken are 1-h fuels; a bark slab is a 10-h fuel. The timelag belongs on
`InsulationMaterial` and `WoodType` beside the other physical properties, derived from what the
material *is* — never listed as "how fast this dries" per recipe (GAME_DESIGN §8).

**What it costs:** `ItemState`, the shelter layers and `Fire` need their moisture ticked. That is
new plumbing, and it is where the slice's real work is. **Save law:** the moisture fields already
exist and are already in `StateDigest`; a timelag is derived, not stored, so v1 should need **no
new saved state** — worth confirming early, because it keeps the slice additive.

**Shelter geometry should decide what gets wet.** A shelter with real walls keeps the rain off its
bedding; a bare frame does not. `DebrisShelter` already has the continuous reading for this —
`SkyView01`, added in B1 — and it is the honest answer to "is the bed under cover", so rain reaching
the bed should scale with it. That is a reuse, not a new number, and it makes B1's sky work pay
twice.

### W3 — Cold snaps

An **anomaly** added to `AirTemperatureC`, not a second temperature model: a synoptic term, a few
days long, a few degrees deep, deterministic or seeded with the same stream as rain and correlated
with it (fronts bring both). One line into the existing function, and every consumer — the body, the
forecast, the tablet's `NightMinimumC`, the drill's plateau — follows automatically.

**Watch:** `Survival.NightMinimumC` reads `AirTemperatureC` at 05:00 *tomorrow*. If the anomaly is a
function of the date, the forecast sees the snap coming, which is correct and is exactly the tablet
earning its keep.

### W4 — Wind shifts

The smallest honest version is a **bearing** on `Weather` plus an **orientation** on the shelter,
so `Enclosure01`'s benefit depends on whether the walls face the wind. That is a real new fact and
a real new decision — "which side do I build up" — and it is the one part of this slice that is not
free.

**I would ask FABLE to rule on scope here.** Direction is the difference between "the wind got
stronger" (which the existing scalar already delivers, and which alone satisfies "wind shifts" as
literally written) and "the windbreak faces the wrong way" (which is what makes a *tested* windbreak
mean anything in Act II). The second is better play and costs a shelter-orientation model plus a
term in `WindInsideMs`; the first is nearly free. **My recommendation: bearing in `Weather` from
the start** — a fact the world has whether or not anything reads it yet — with the shelter
orientation deferred to its own change set once the moisture work has landed. That keeps this slice
finishable and does not paint W4 into a corner.

### W5 — Wet skin, the compounding kill condition

The body needs a `Wetness01` and two effects, both through existing terms:

- **insulation collapses** — wet clothing and a wet boundary layer lose most of their value; this
  is a scale inside `TotalInsulationClo`, not a new subtraction;
- **evaporation that is not sweat** — the body already has the latent machinery in `Tick`;
  evaporating water off skin is the same physics with a different source.

This is the term that makes Act II's stated kill condition real, and it is the one piece of W1–W5
that touches `SensibleLossW`'s neighbourhood — so it wants the most careful review and probably its
own change set.

## 7. Calibration referents

Weather v1 must be held to published figures the same way the body was, and the referents exist:

| quantity | referent |
|---|---|
| rain rate classes | light < 2.5 mm/h, moderate 2.5–7.6, heavy > 7.6 (standard meteorological classification) |
| August rainfall and rain-days at the wake point | Bureau of Meteorology station climatology for the Southern Highlands (Moss Vale / Bowral), the same anchor `Climate`'s own comment already uses for temperature |
| fuel moisture response | dead-fuel timelag classes (1-h, 10-h, 100-h, 1000-h) and the 63%-of-equilibrium definition; EMC from temperature and humidity (Simard / Nelson, as used in fire-danger rating) |
| wet insulation | already calibrated — `InsulationMaterial`'s dry-to-saturated band, 0.045 → 0.55 W/(m·K) |
| wet clothing | published loss of insulation value when soaked; the "wet is worse than naked in wind" result |
| cold-front anomaly depth | synoptic temperature drop for a frontal passage in this region |
| wind chill | already implemented and cited (JAG/NWS) |

**And the harness pattern B1 proved:** a scratch console harness against `CI/Sim`, validated against
a published table *before* anything it says is believed. `PHYSICS_NAKED_HUMAN.md`'s `-eg-sky-test`
table played that role for the thermal work; weather needs its own equivalent, and the natural one
is a modelled month at the wake point checked against station climatology for total rainfall and
rain-day count.

## 8. Risks specific to this slice

1. **Two opinions about how wet the world is** (gap 6). `GroundCover.MoistureAt` must become a
   reader of the weather model, or the weather model must own gather-moisture outright. Whichever
   FABLE rules, it should be settled in the contract rather than discovered in review.
2. **Moisture ticking is per-object and easy to get wrong under time compression.** The world runs
   at 12× and 90×; an exponential approach is stable at any step, which is another reason to prefer
   the timelag form over a linear soak.
3. **Rain must not silently make the forecast lie.** `Night.For` and `Oracle.TheNight` price a night
   that has not happened; if it will rain, the tablet must either say so or be honest that it does
   not know. B1's contract permits the forecast and the night to disagree only about *time* —
   weather threatens that invariant, and T1's seam test is where it would show.
4. **`Fire.DrowningMoisture` gains a second, currently impossible route.** The threshold is
   *already* reachable — `GroundCover.MoistureAt` tops out near 0.63 on damp ground before dawn,
   above the 0.55 drowning line, so a founder feeding sodden wood can put their own fire out today.
   What cannot happen is a fire drowning **from the sky**: `Fire.Moisture01` only ever moves when
   fuel is added (`Fire.cs:175`), so rain falling on a burning fire does nothing. Rain makes
   `Tick:185` reachable without the founder's hand in it, which is a different and much harsher
   experience, and it should get a scenario before it decides a night.

## 9. What I would want in the contract's test list

- **Wetting and drying are one mechanism**: the same material driven wet by rain and dry by warm
  air must move by the same equation, and the sabotage is to give drying its own constant.
- **A month at the wake point matches station climatology** for rainfall total and rain-day count,
  within a stated budget.
- **Rain reaches the bed through the shelter's own geometry**: at fixed rain, bedding under
  `SkyView01 = 0` must not wet, and a bare frame must wet at nearly the open rate — continuous
  across wall mass, the same discriminator T4b used.
- **The consequences arrive through the existing constants, proven by absence**: a source-text test
  that no new "rain penalty" branch exists in `Fire`, `DebrisShelter` or `BodyState` — the claimed-
  removal pattern of `KeybindSourceTests`.
- **Cold snaps move one function**: the anomaly is visible in `AirTemperatureC` and nowhere else,
  and every downstream reading follows without being told.
- **Determinism**: the same seed and date give a bit-identical month (law 4, `SimDeterminismTests`
  pattern).

## 10. Out of scope for v1, and worth saying so

Snow and hail; real climatology tiles (that is ROADMAP 3.1 and this is the analytic stand-in);
lightning and fire risk; wind damage to a built shelter; puddles, runoff or any change to
`DrainageNetwork`; humidity's effect on anything but respiration and fuel; and rendering — rain as a
visual is a Scripts/GFX concern that should follow the Sim model rather than lead it.

---

**One sentence, if the rest is too long:** the consequences of rain are already built and calibrated
across six systems, moisture is the single missing cause, the published fuel-timelag model supplies
both wetting and drying from one equation, and the only genuinely new facts this slice needs are a
rain rate, a wind bearing and a wetness on the founder's skin.
