# The physics of a naked human in a field

**Status:** research, binding for the body model. Owner direction, 2026-08-26:
*"research real physics, thermodynamics etc to figure out the games physics."*

This is the audit of what governs the founder's body, what the game already models correctly, and
what it does not model at all. Every number here comes from published work, not from tuning.

## The equation

Partitional calorimetry gives the whole of it:

```
S = M - W - R - C - K - E - RES
```

| | | in the game |
|---|---|---|
| **S** | heat stored (core temperature moving) | yes |
| **M** | metabolic rate | yes - basal, shivering, and Pandolf for movement |
| **W** | external work | folded into M |
| **R** | radiation, skin to surroundings | **only as gain from a fire** |
| **C** | convection, skin to air | yes, via the clo boundary layer |
| **K** | conduction, to the ground | yes, and it is the best-modelled term |
| **E** | evaporation, sweat and insensible | yes, added with the sleep slice |
| **RES** | respiration, sensible and latent | **not at all** |

## What the split actually is

At rest, a body loses heat by **radiation 50%, convection 30%, evaporation 20%, conduction ~0%**.

Radiation is the largest single term and the game does not have it as a term. It is folded into the
clo boundary layer along with convection, which is standard practice for indoor comfort work,
because indoors the walls are at air temperature and the two can be lumped.

**Outdoors they cannot.** The sky is not at air temperature and neither is the sun.

## What the game gets right

| quantity | game | published | |
|---|---|---|---|
| tissue insulation, vasoconstricted | 0.90 clo | 0.9 clo, a >300% increase over dilated | ok |
| tissue insulation, vasodilated | 0.30 clo | ~0.22-0.3 clo | ok |
| bare-skin boundary layer | 0.70 clo | h_c 3.4 + h_r 4.7 = 8.1 W/m2K = 0.80 clo | close |
| latent heat of sweat | 2.43 MJ/L | 2.43 MJ/kg at skin temperature | exact |
| max sweat rate | 1.5 L/h | 1-2 L/h sustained, acclimatised | ok |
| body specific heat | 3500 J/kgK | 3470-3500 | ok |
| skin area | 1.8 m2 | 1.85 m2 by DuBois for 70 kg | ok |
| basal rate | 80 W | 1700 kcal/day = 82 W | ok |

The thermoregulation that is there is sound. The problem is what is missing.

## The three missing terms

### 1. Solar gain - the biggest one

Direct normal solar radiation reaches **600-800 W/m2** at noon with about **200 W/m2** diffuse.
Net gain for a person outdoors is around **70 W/m2 of body surface**, so roughly **125 W** for a
1.8 m2 naked body. The comfort ceiling for whole-body shortwave gain is 189 W/m2.

125 W is more than basal metabolic rate. For a naked founder in late winter it is the difference
between a survivable day and a lethal one.

**In the game right now, standing in winter sun is identical to standing in shade.** The founder
has no reason to prefer a sunny clearing, to face the sun, or to work in the middle of the day -
and every one of those is a real decision a person in that situation makes constantly.

### 2. Radiative loss to the sky - why roofs work

A clear night sky has an effective temperature **18-33 C below ambient**. Net radiative cooling to
it runs about **75 W/m2** of exposed surface, falling to roughly half that under cloud. A person in
cold exposure loses on the order of **200 W** by radiation.

The game has no sky term, which means:

- a clear night and an overcast night are the same night
- a shelter roof only matters because it breaks wind, which is not the main reason roofs work
- lying in the open under stars costs nothing extra

The correct form avoids double-counting the boundary layer. The clo model already carries radiation
to surroundings *at air temperature*; what is missing is only the excess because the sky is colder:

```
Q_sky = eps * sigma * A_eff * f_sky * (T_air^4 - T_sky^4)
```

At 0 C air, 15 K sky depression, f_sky 0.5, A_eff 0.7 x 1.8 m2, that is **about 39 W** - half the
basal rate, and it goes to zero under a roof. That is exactly the right size to make a roof matter
for the right reason.

### 3. Respiratory heat loss - the one that scales with work

Respiration is about **10% of total heat loss** in ordinary conditions, but in cold Fanger found it
reaches **25-30% of resting metabolic rate** and **15-20% of working metabolic rate**.

At rest at 0 C that is around 20 W. Working hard at 400 W metabolic it is 60-80 W. The important
property is that it **scales with ventilation, so it scales with exertion**: the harder the founder
works in the cold, the more heat goes out through their breathing. Nothing in the game does that.

> **Correction (MAIN DEV 2, 2026-09-01, measured):** the two figures in the paragraph above are
> quoted against **1 met** — a seated adult, 58 W/m² ≈ 105 W whole-body — while this model's
> `BasalHeatW` is a true basal 80 W. Fanger's own formula, which is what `BodyState.RespiratoryW`
> implements, gives **11.3 W at rest at 0 °C** and **55.1 W working at 400 W**, both 13.8% of
> metabolic rate. Those are the numbers this document's own measured table below records (11.0 W,
> 55.1 W) and calls *"exactly as it should"*, so the disagreement was inside this file rather than
> between the file and the code. The measured table is right; treat "around 20 W" and "60-80 W" as
> the 1-met figures they are. Detail in `Docs/CALIBRATION_B1_DAY_ONE.md`.

## What this changes about play

None of this is decoration. Each missing term is a decision the founder should be making:

| term | the decision it creates |
|---|---|
| solar gain | work in the sun, face it, choose a north-facing slope, rest at noon |
| sky radiation | build a roof, sleep under canopy not open sky, prefer cloudy nights |
| respiratory loss | working hard in cold costs more than the work alone |

The founder's own knowledge should transfer (S3). Anyone who has been cold outdoors knows that sun
on your back is worth more than the air temperature says, that a clear night is colder than a
cloudy one, and that you can see your own breath leaving. All three are currently absent.

## Order of work

1. **Solar gain**, with sun angle from the existing `SolarClock`, cloud cover, and shading from
   canopy and terrain. Largest effect, and it uses machinery that already exists.
2. **Sky radiative loss**, with an effective sky temperature from cloud cover, and a sky view
   factor from the shelter and the canopy.
3. **Respiratory loss**, scaled off the metabolic rate the body already computes.

Cloud cover has to exist for 1 and 2, and it does not yet - `Climate` has temperature and wind and
nothing else. That comes first.

## What it came out as

Measured across a whole day at the wake point, 25 August, with `-eg-sky-test`:

| hour | air | cloud | sun | sky | breath |
|---|---|---|---|---|---|
| 04:00 | 1.5 C | 0.14 | +0 W | -38 W | -11 W |
| 08:00 | 5.4 C | 0.11 | **+126 W** | -40 W | -10 W |
| 12:00 | 10.7 C | 0.18 | **+154 W** | -40 W | -9 W |
| 16:00 | 12.1 C | 0.19 | +121 W | -41 W | -9 W |
| 20:00 | 8.2 C | 0.09 | +0 W | -42 W | -10 W |

The sun at noon is worth **154 W** - nearly twice the founder's basal rate, and the difference
between a survivable winter day and a lethal one. It is flatter across the day than the irradiance
is, because a lower sun lands on more of a standing body: 126 W at 17 degrees elevation against
154 W at 44.

**Lying out at 2 C, a clear sky costs 64 W and an overcast one costs 10 W.** Six times, on nothing
but cloud. A roof takes all of it.

Respiratory loss came out at 11.0 W resting at 2 C and 55.1 W working at 400 W - 14% of metabolic
rate either way, exactly as it should, because ventilation follows metabolism.

### What it cost the shelters

Adding respiration moved every marginal threshold, and the amendments are recorded in the tests:

| | was | is |
|---|---|---|
| a proper shelter (V9) | 25 kg of walls | **30 kg** |
| survives awake, dies asleep (S2) | 15 kg | **17 kg** |
| water decides the night (W9) | 16 kg | **18 kg** |

**Breathing costs you an armful of debris.** W7 also had to move from 24 C to 30 C, because 24 C
is not thermoneutral for bare skin and never was - the body was already losing slowly there, and
respiration made it visible by taking the core under hypothermia before thirst had finished.

## Still to do

Solar gain, sky radiation and respiration are in. Not yet modelled:

- **Terrain shading** - the sun is blocked by canopy (0.85) and by being in a shelter, but not yet
  by the hill to the west, which in this country decides when the cold arrives.
- **Wet skin and rain**, which is the other way a person in a field dies.
- **Ground temperature** as distinct from air temperature; the conduction term uses air.

## Sources

- Partitional calorimetry and the heat balance equation, J Appl Physiol
- Vasoconstriction and tissue insulation, PubMed 8712443
- Nocturnal radiative cooling rates, PMC6777208
- Solar radiation on the human body, ScienceDirect S0360132320307885
- Respiratory heat loss during work at various ambient temperatures, PubMed 2336491
- Convective and radiative coefficients for body segments, PubMed 9195861
