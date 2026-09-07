# Slice E1 — Landform: the valleys make themselves

**Status:** contract, binding. Written before code. Implements `ECOSYSTEM.md` link 2.

## What it is

A landscape evolution model. Rock goes up, water cuts down, hillslopes creep, and the shape that
results is a landscape — with valleys, ridges, a drainage network that integrates, and sediment
where sediment collects. Nothing about that shape is authored.

This is the standard geomorphological model, not an invention:

```
dz/dt  =  U  -  K · A^m · S^n  +  D · ∇²z
          ^      ^                ^
          |      |                hillslope diffusion: soil creep rounds
          |      |                everything the rivers do not cut
          |      fluvial incision by stream power: the river cuts
          |      faster where more water flows and the slope is steeper
          uplift: what keeps the high ground high
```

`A` is drainage area — which the existing `DrainageNetwork` already computes — and `S` is the slope
to the cell the water leaves by. The whole model is four terms and every one of them is a published
equation with published coefficients.

## Why it is the fix and not a nicer noise function

Valleys are not a shape. They are a **record of what water did**, and no amount of shaping produces
the record of a process that never happened. That is why 53% of the un-eroded ground sat in closed
hollows and no drainage network could be found in it: there was nothing to find.

Run the process and the structure is not added, it is left behind.

## Domain spec (§35)

### Parameters, with the ranges they come from

| Symbol | Meaning | Value | Published range |
|---|---|---|---|
| `K` | erodibility | 3e-5 | 1e-6 – 1e-4 (rock-dependent) |
| `m` | area exponent | 0.5 | 0.4 – 0.6 |
| `n` | slope exponent | 1.0 | 1 (linear form) |
| `D` | hillslope diffusivity | 0.005 m²/yr | 0.001 – 0.01 soil-mantled |
| `U` | uplift | 3e-4 m/yr | 0.1 – 1 mm/yr |
| `dt` | timestep | 2,000 yr | — |
| steps | | 500 (1 Myr) | long enough to integrate drainage |

`K` is later read from the rock: `SurfaceGeology` already says what the country rock is, and hard
rock holding up ridges while soft rock is cut away is one of the most legible facts in any
landscape. That link is made in this slice.

### Solving it

Fluvial incision is solved **implicitly** in downstream-to-upstream order, which is unconditionally
stable and linear in the number of cells:

```
f      = dt · K · A_i^m / L_i
z_i'   = (z_i + f · z_r') / (1 + f)          r = the cell i drains to, already updated
```

Diffusion is explicit with a sub-step count chosen so `D·dt/dx²` stays under a quarter. Flow is
re-routed every ten steps rather than every step, because drainage networks reorganise slowly and
routing is the expensive part.

Depressions are filled before routing, which is what a lake does — and the outlet is then cut down
by the same incision term, which is how a lake drains itself in the end. The model destroys its own
depressions, which is exactly the behaviour that was missing.

### Where it sits

The model runs on a 16 m grid over the catchment. The visible ground becomes:

```
height(x) = erodedSurface(x)        bilinear, the landform
          + fineDetail(x)           small amplitude, below the grid, for texture
```

`TerrainSynthesis` becomes the **initial bedrock** the model starts from rather than the final
answer. `FlatTerrain.SampleBedrock` exposes the before; `SampleHeight` returns the after.

The channel-incision hack added while chasing the water problem is deleted. It was a fake of this.

## §36 validation — pre-written, binary

The strength of this slice is that geomorphology has published, checkable signatures. A landscape
that has these is a landscape; one that does not is a noise field.

| # | Test | Criterion | Referent |
|---|---|---|---|
| E1 | **Slope–area concavity** | log S against log A over the fluvial range has slope −0.35 to −0.65 | θ = m/n, the most-measured relation in geomorphology |
| E2 | **Steady state matches theory** | mean S at given A within a factor of 2 of (U/K·A^m)^(1/n) | the equation itself |
| E3 | **Drainage integrates** | closed depressions fall below 2% of cells, from 53% | eroded landscapes drain |
| E4 | Hack's law | main-stem length against basin area, exponent 0.5–0.7 | Hack 1957 |
| E5 | Relief is bounded | total relief settles rather than growing or collapsing | uplift/erosion balance |
| E6 | Hard rock stands up | doubling K halves steady-state relief | the equation |
| E7 | Deterministic | same input, same output, bit for bit | §36 |
| E8 | Ridges and valleys | elevation histogram is not the input's, and slope is bimodal in the way a dissected landscape is | dissection |
| E9 | **Water ends up in valleys** | over 90% of drinkable cells are lower than the mean of the ground within 50 m | the point of the whole slice |
| E10 | Affordable | a 2.4 km catchment erodes in under three seconds | it runs at world build |

## Feel checklist (owner)

- From the air the land reads as country: spurs, gullies, a creek line you can follow.
- Water is in the bottom of things, and looks like it belongs there.
- Standing on a ridge and walking downhill reaches water without being told to.

## Out of scope

Sediment thickness as a soil model (E3), vegetation (E4), glaciation, karst, coastal processes,
landslides, and erosion at Earth scale — this slice is the flat world's catchment. Region-tiled
erosion for the whole planet is a Phase 3 concern.
