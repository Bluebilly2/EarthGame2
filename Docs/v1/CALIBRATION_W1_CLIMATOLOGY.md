# Calibration — the wake point against its own weather station

**Status:** measurement record, produced by MAIN DEV 2 for contract W1, 2026-09-01. This is the
climatology harness the contract demands *before anything the model says is believed*, and it owns
W1's referent. Measurement only; no `Assets/**` change rides it.

## The referent, and how close it sits

**Moss Vale AWS — BoM station 068239, 2001–2022, 678 m AMSL, 34.53° S, 150.42° E.**

The wake point is **34.5° S, 150.4° E, 687 m** (`SolarClock.WakePoint`, and the 687 m that
`SkySelfTest.cs:45` and `PHYSICS_NAKED_HUMAN.md`'s measured table already use). That is the same
place: about **3 km** apart and **9 m** of elevation, which is 0.06 °C of lapse rate. The founder
wakes at this station.

That is a better referent than this project usually gets, and it means the game's climate is
checkable against the real instrument record rather than against a regional impression.

| | August | annual |
|---|---|---|
| mean daily maximum | **13.4 °C** | — |
| mean daily minimum | **3.0 °C** | — |
| rainfall | **54.7 mm** | **784.5 mm** |
| precipitation days | **12.3** | **167.9** |

Fetched from the BoM-sourced climate table for Moss Vale AWS. **One caveat carried honestly:**
`bom.gov.au` refused every direct request while this was written (`ECONNRESET`, then timeout), so
the figures came from the station table reproduced on Wikipedia rather than from BoM's own page. The
station, period, elevation and coordinates all came with them and are consistent, but **the primary
source has not been read directly and someone should**. The precipitation-day threshold is almost
certainly ≥ 1 mm — 784.5 mm over 167.9 days is 4.7 mm a day, which is the right size for a 1 mm
threshold and too wet for a 0.2 mm one — but that too is inference and W1's rain-day test should
state the threshold it counts to.

## What the harness found before rain existed

The harness's first job is to prove itself against something the model already claims. It does, and
in doing so it measured the temperature model against this station for the first time:

| | model | station | delta |
|---|---|---|---|
| August mean daily maximum | 11.86 °C | 13.40 °C | **−1.54** |
| August mean daily minimum | 2.00 °C | 3.00 °C | **−1.00** |
| August diurnal range | 9.86 °C | 10.40 °C | −0.54 |
| annual mean | 11.45 °C | ~13.3 °C † | **−1.85** |

† secondary source (climate-data.org) for the annual mean, cross-check only.

**The shape is right and the level is low.** The diurnal range lands within half a degree, so the
daily machinery — the cosine, the 05:30/15:00 extremes, the lapse rate — is sound. The error is a
constant offset of roughly **1.5–1.9 °C**, which puts it in `Climate.SeasonalMeanC`'s annual-mean
term, `-20.0 + 48.0 * cos(|lat|)^1.5`. That is a generic latitude formula rather than anything
fitted to this place, and being ~2 °C out at one station is about what a generic formula earns.

**`Climate`'s own doc comment is accurate about the model and optimistic about reality.** It says
late August here gives *"a maximum near 12 °C and an overnight low around freezing"*. The model does
exactly that — 11.86 and 2.00. The station says 13.4 and 3.0. So nobody wrote a false comment; the
comment describes a model that was never checked against the station it was describing.

## Ruled: the world is pre-human, and the model already was

**Owner's word, 2026-09-01, canon** (`CONTRACT_W1_WEATHER.md`, "The climate baseline"), reasoning
verbatim: *"if the player is going to be seeing megafauna roaming around, [the modern climate]
wouldn't make sense."* The counterfactual that keeps the megafauna alive keeps their climate too.
So the referent is **the station minus the anthropogenic warming signal**, the station's *shape*
binds regardless of level, and the residual beyond the offset is corrected rather than excused.

**The constant, regional and cited:** Australia has warmed **1.51 ± 0.23 °C since national records
began in 1910** (Bureau of Meteorology and CSIRO, *State of the Climate 2024*). Regional, not
global — the global figure is nearer 1.1–1.3 °C, and using it would leave the wake point a quarter
of a degree too warm, which is the size of the residual this constant is used to measure. It lives
on `Climate.AnthropogenicWarmingSinceRecordsC` with its uncertainty and its two known refinements
(pre-1910 warming is not in it; the station window centres before its endpoint) named rather than
split, because each is smaller than the ±0.23.

**And the finding dissolves.** Against the ruled target the model was never 1.7 °C cold:

| August | model | pre-human target | delta |
|---|---|---|---|
| mean maximum | 11.86 °C | 13.4 − 1.51 = **11.89** | **−0.03** |
| mean minimum | 0.86 °C | 3.0 − 1.51 = **1.49** | −0.63 |
| diurnal range | 11.00 °C | **10.40** (a shape; the offset cancels) | +0.60 |

The maximum was already within **three hundredths of a degree** of a pre-human August. What looked
like a cold world was the anthropogenic signal, and the whole residual is a diurnal range too wide
about a correct mean — nights 0.63 °C too cold while the days are exact.

**The residual, corrected not excused.** `Climate.DiurnalRangeC` was `11.0`, written as "roughly
11 °C here". The station measures 10.4. Correcting it warms every night by 0.30 °C, cools every
afternoon by the same, and changes nothing at 09:00 or 21:00 where the cosine crosses its mean.

**Blast radius, measured before touching, per the ruling.** The suite: **363/363, nothing moved** —
the B1 grid and the shelter crossings price their nights at a fixed 4 °C test constant rather than
reading `Climate`, so they are structurally insulated from it. The game itself: an idle founder's
day-one death moves from **21:30 to 21:25** and the 20:00 core from 31.06 to 30.90 °C. Five minutes.

**What is left after the correction is inside the referent's own error bar.** Both August extremes
end 0.33 °C below target, so the seasonal *mean* is 0.33 low while the shape is now right. That is
**1.4σ of the offset constant's published ±0.23**, and it cannot be attributed to the formula rather
than to the offset from a single station. Chasing it would mean fitting a global latitude curve —
`-20 + 48·cos(lat)^1.5`, which sets the temperature of every latitude on Earth — to one weather
station in New South Wales. That is overfitting, and it is named here so that the next person meets
the reasoning rather than the number.

## The ruling's other half: the rain was dried too, and by a dated figure

**Ruled 2026-09-01: the cool-season drying correction rides W1 as a dated cited constant, and the
test asserts the derivation chain.** "Minus everything humanity has changed" cannot stop at
temperature — removing the warming while keeping the drying would be exactly the inconsistency the
baseline ruling exists to prevent.

**The constant, and why it must carry its date.** From the same source as the warming:

> *"In the south-east of Australia, there has been a decrease of around 9% in April to October
> rainfall since 1994."* — Bureau of Meteorology and CSIRO, *State of the Climate 2024*

**Since 1994** — and the station window that anchors this world's rain, Moss Vale AWS 2001–2022,
lies **entirely inside** that decline. So the modern record *is* the dried record, and the world's
rain is that record with the drying undone. Unlike the warming, which is measured from 1910 and
brackets the station window, this one is dated in a way that decides which side of it the referent
falls on. `Climate.CoolSeasonRainfallDeclineSince1994 = 0.09`.

**The derivation chain, which the tests assert step by step rather than quoting a target:**

| | | |
|---|---|---|
| August is wholly inside April–October | so it takes the **whole** correction | asserted against the season's own day bounds |
| pre-human August | 54.7 / (1 − 0.09) = **60.11 mm** | and drying it by the decline must give the station's own figure back |
| pre-human annual | 784.5 × (1 + 0.530 × 0.0989) = **825.6 mm** | only the cool season is corrected |

**The annual correction is smaller than 9% and is derived rather than asserted**, because the
cited figure covers April–October only. No cited figure covers the warm season here, so the warm
half is **left uncorrected and said so**. The cool season's share of the year is measured off the
model's own distribution at **53.0%** — sensibly below the 58.3% a flat year would give, because
this climate has a summer maximum, and the test holds it inside that range so a model whose seasons
inverted could not pass.

**What it moved.** `SeasonalAmplitude` 0.135 → **0.104** and `RainScaleMmPerHour` 0.979 → **1.0344**,
re-solved against the corrected targets. Undoing a *cool-season* drying necessarily lifts the cool
season relative to the year, so a smaller seasonal amplitude is what the correction physically
means rather than a fitting convenience.

| | model | pre-human target | |
|---|---|---|---|
| annual rainfall | **825.9 mm** | 825.6 mm | +0.04% |
| August rainfall | **60.11 mm** | 60.11 mm | exact |
| annual rain days | 146 (≥1 mm) … 199 (≥0.2 mm) | 167.9 † | brackets it |
| August rain days | 11.8 (≥1 mm) … 15.9 (≥0.2 mm) | 12.3 † | brackets it |

† **The day counts are deliberately left uncorrected**, and the bracket is what absorbs it. The
cited figure is a rainfall *amount*; no figure covers how the number of rain days changed, and
inventing one to match would be the opposite of what this document is for. The brackets are wide
enough that the uncorrected counts still fall inside them, which is the honest statement.

**Two sabotages, each observed red:** setting the decline to zero reddens both the chain test and
the annual; applying the correction to the whole year instead of the cool season reddens the annual
alone — which is the one that proves the *seasonal* restriction is doing work rather than being
described.

## A separate defect the measurement exposed: the minimum is at the wrong hour

`AirTemperatureC` is one cosine with its maximum pinned at 15:00, so its minimum falls at
**03:00** — twelve hours away, necessarily. Two comments said otherwise: *"the daily minimum falls
just before dawn"* and *"minimum near 05:30"*. Both described the physics anyone would expect and
neither described the arithmetic beneath them. Real diurnal cycles are asymmetric: a long slow
cooling to a minimum at sunrise, then a fast climb. One curve cannot do it; two limbs can.

**It has a measured cost.** `Survival.NightMinimumC` reads **05:00** to price the coldest hour of
the coming night, and at 05:00 this curve is already **0.74 °C above its own trough**. So the
tablet's night forecast is optimistic about the coldest hour by three quarters of a degree — on the
one number the founder is meant to plan the night against.

The comments now tell the truth and carry the consequence. **The fix is the asymmetric cycle and it
is its own change set**, because unlike the range correction it moves the *shape* of every night and
therefore every thermal number in the project — which gets measured before it is moved.

## Why the 1.7 °C was not fixed before the ruling

Moving `SeasonalMeanC` by 1.7 °C moves **every thermal number in the project**: the day-one death
hour, B1's 27-night grid and its T2/T3 margins, the S2 and W9 crossings, the two `OracleTests`
shelter masses, and the fire model's plateau margins. That is a calibration decision with a very
large blast radius and it is FABLE's to make, not a dev's to slip into a weather slice.

It also is not obviously a defect. Three things are worth weighing and none of them is mine to
weigh:

1. **The station period is 2001–2022**, which carries recent warming. A model anchored to a
   longer or older baseline would legitimately sit cooler.
2. **A colder wake point is a harder game**, and day-one lethality was ruled canon this afternoon
   partly on the strength of numbers taken at the current temperature. Warming the world by 1.7 °C
   pushes back on a ruling that was just made.
3. **The offset is a level, not a shape**, so if it is corrected the correction is one term and the
   diurnal behaviour is untouched.

## What W1 does with it meanwhile

Nothing, deliberately. W1's rain model is calibrated against **this station's rainfall**, and rain
occurrence is not sensitive to a 1.7 °C offset in air temperature at these temperatures — nothing in
the proposed model gates rain on temperature. So the offset is recorded, escalated, and does not
block the slice.

**The one place it will matter inside W1** is the equilibrium moisture content of the timelag model
(W1b), which is a function of temperature and humidity: a world 1.7 °C cold dries a little more
slowly than the real one. That is second-order against the rain rate itself, and it is the right
thing to re-check if the offset is ever corrected.

## Rainfall: the target W1 has to hit

The model has **no precipitation** — this harness has nothing to measure yet, which is the point of
running it now. The numbers to hit, at the wake point, are the table above: **54.7 mm across 12.3
rain days in August**, inside **784.5 mm across 167.9 days** in the year. The contract's acceptance
is the modelled month against those, within a stated tolerance, before the model is believed.

Two properties matter as much as the totals and are tested beside them:

- **Rain must arrive in fronts, not drizzle.** 54.7 mm over 12.3 days is 4.4 mm per rain day, and a
  model that delivers 1.8 mm every day of the month hits the monthly total while being wrong about
  every single day — and Act II's exam is *a rain day*, not a wet fortnight.
- **Dry spells must exist.** The camp has to be buildable, which means the seed has to produce runs
  of dry days as well as runs of wet ones. A rain-day count alone cannot see this; the distribution
  of dry-spell lengths can.

## What the model came out as

`Sim/World/Synoptic.cs`. Rain is a **seeded, stateless function of time** — not a stepped state —
because `Survival.NightMinimumC` asks what five o'clock *tomorrow* will be like so the founder can
be warned before dark, and a model that had to be walked forward could not answer. The shape is a
sum of sinusoids with seeded periods and phases in two nested bands.

Two free constants were solved against the two unambiguous totals, averaged over **eight seeds**,
because one seed is one realisation of a climate rather than the climate:

| | model | station | |
|---|---|---|---|
| annual rainfall | **784.87 mm** | 784.50 mm | 0.0% |
| August rainfall | **53.94 mm** | 54.70 mm | −1.4% |
| annual rain days | 144 (≥1 mm) … 197 (≥0.2 mm) | 167.9 | **brackets it** |
| August rain days | 11.3 (≥1 mm) … 15.6 (≥0.2 mm) | 12.3 | **brackets it** |

**The day counts are a bracket rather than a number, and that is the honest form of the check.** The
station's figure comes with no stated threshold, and BoM publishes counts at more than one, so the
model is held to the only thing that can be verified: the station's value must lie between the
model's count at ≥ 1 mm and at ≥ 0.2 mm. It does, in both the year and the month. That says the
rain frequency is right to within the referent's own ambiguity — and it means **pinning the BoM
threshold directly is worth more than any further tuning here.**

### The model was wrong once, and its own test caught it

The first calibration hit both totals to a fraction of a per cent and was still wrong. With only the
frontal band — periods of 2.5 to 14 days — the index moves so slowly that once it crosses the rain
threshold it stays across for the rest of the day. A wet August day was **eighteen hours of
continuous gentle rain**: correct totals, drizzle wearing a front's clothes. `MostHoursAreDry` went
red at 65%, which is how it was found.

The fix is a second, faster band — the **shower band**, four hours to a day and a half, carrying 38%
of the index's variance so it can pull the total back under the threshold between bands. Weather is
nested, and modelling only the outer scale gets the year right and the day wrong. A wet day now
rains for **8.6 hours** on average, in bands with breaks — which is what decides whether a founder
can gather between showers.

**The bound that caught it no longer catches it**, and that is recorded rather than glossed:
raising the rain threshold during recalibration made rain rare enough that `MostHoursAreDry` stays
green with the showers deleted. So a separate test — `RainOnAWetDayComesInShowersNotDrizzle` —
measures raining-hours-per-wet-day directly, and *that* is the guard. A test that would pass with
the defect planted is not the test for that defect.

### Sabotages, each observed red

| planted | reddens |
|---|---|
| seasonal term removed (`SeasonalAmplitude = 0`) | August rainfall; the day-count bracket |
| shower band removed (`FrontalVarianceShare = 1`) | the shower-structure test; the day-count bracket |
| rain scale moved 10% | both rainfall totals |

**361/361 green**, six weather tests, no sabotage left in the tree.
