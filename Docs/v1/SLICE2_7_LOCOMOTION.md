# Slice 2.7 — Locomotion: moving like a person

**Status:** contract, binding. Written before code.
**Phase:** 2 — The First Survivor.
**Owner direction, 2026-08-26:** *"when can we work on the physics, they dont feel nearly
realistic."*

## Why

The body model knows what it costs to shiver and what a wet shelter loses and how much starch is in
a rhizome. The founder walks at **6 m/s** and sprints at **11 m/s** — a fast run and a sprint within
a whisker of the fastest human who has ever lived — up a forty-degree hill, carrying twenty-five
kilograms, on an empty stomach, for free.

That is the first thing anybody notices and the last thing that got any attention. It also quietly
breaks the world: a 2.4 km catchment crossed in seven minutes is not a landscape, it is a room.

## The transfer test (§3)

1. **Speed comes from the ground.** How fast the founder moves is decided by the slope they are on,
   the load they are carrying and the state of their body — not by which key is held.
2. **Uphill is slow and expensive, downhill is fast and not free.** Both are real curves with
   published forms, and a person who knows to contour around a spur rather than go over it should
   be rewarded for it.
3. **Carrying costs.** Twenty-five kilos of stone is not a number in a menu; it is in every step.
4. **The body you have is the body you move with.** Thirst, exhaustion, hunger and illness already
   multiply work capacity. They multiply this too.
5. **Moving is eating.** Locomotion feeds the same energy budget the food slice fills, so a day
   spent walking is a day that has to be paid for.

## Domain spec (§35)

### Speed against slope — Tobler's hiking function

The published form, from Tobler (1993), used unchanged:

```
W = 6 · exp(−3.5 · |S + 0.05|)      km/h,  S = rise/run
```

| slope | | m/s | what it is |
|---|---|---|---|
| −0.05 | gentle descent | **1.67** | the fastest a person walks; the function peaks here, not on the flat |
| 0.00 | flat | 1.40 | ordinary walking |
| 0.10 | 6° | 0.99 | noticeably working |
| 0.30 | 17° | 0.49 | a scramble |
| 0.50 | 27° | 0.24 | hands out of pockets |
| −0.40 | steep descent | 0.47 | braking all the way down |

That the peak is at a slight *descent* rather than flat is the sort of thing that has to fall out of
a real function rather than be invented, and it is why traverses in real country zigzag.

### Gaits

Tobler gives the sustainable walk. The founder can also press on:

| gait | multiple of the Tobler speed | sustainable |
|---|---|---|
| walk | 1.0 | indefinitely |
| jog | 2.1 | tens of minutes |
| run | 3.6 | minutes |

Gait is chosen by the player (shift), capped by the body, and paid for in the energy equation
below — there is no separate stamina bar, because there is already an energy budget and a second
one would be a lie about the same thing.

### What it costs — the Pandolf equation

Pandolf, Givoni & Goldman (1977), the standard load-carriage metabolic model:

```
M = 1.5·W + 2.0·(W+L)·(L/W)² + η·(W+L)·(1.5·V² + 0.35·V·G)      watts
```

with `W` body mass kg, `L` load kg, `V` speed m/s, `G` grade in per cent, and `η` a terrain factor
(1.0 made ground, 1.2 light brush, 1.5 heavy brush, 1.8 loose sand). Santee's correction applies
going downhill, where the equation otherwise over-predicts.

This **replaces `WalkingHeatW = 180` and `RunningHeatW = 420`**, which were constants standing in
for a function. The heat balance gets the real number, so walking uphill with a load warms the
founder — which is true, and is the reason people strip off on a climb and freeze at the top.

### Movement that feels like a body

| | was | is |
|---|---|---|
| ground acceleration | 60 m/s² (6 g) | 4.0 m/s² |
| stopping | instant | ~1.5 m of it |
| jump apex | 1.20 m | 0.45 m |
| air control | 8 m/s² | 1.2 m/s² |

## §36 validation

| # | Test | Criterion |
|---|---|---|
| L1 | **Tobler, unchanged** | flat 1.40 m/s ±0.02, and the peak is at S = −0.05 |
| L2 | **Uphill is a crawl** | 27° is under a quarter of flat speed |
| L3 | **Steep descent is not free** | −0.40 is slower than flat |
| L4 | **Pandolf against published points** | 1.34 m/s flat unloaded within 10% of the literature |
| L5 | **Load costs** | 25 kg raises the cost of a flat walk by at least a third |
| L6 | **Grade costs more than load** | 15% grade unloaded beats 25 kg on the flat |
| L7 | **The body caps the gait** | a founder at 0.4 work capacity cannot run |
| L8 | **Crossing the catchment is a walk** | 2.4 km on rolling ground is over 25 minutes |
| L9 | Nothing is free | every gait on every slope costs more than basal |

## Out of scope

Stumbling, injury, swimming, climbing with hands, footprints, and the sound of any of it.
