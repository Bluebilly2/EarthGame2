# Region 1 — The Beach

**Status:** first pass, shipped. Owner direction, 2026-08-26: *"work on the first 10x10, i want it
to be centred around a beach scene with a workshop full of tools. there is a forest behind the
beach, and crashing waves on the sand... we will use this first 10x10 for the minecraft beta bar."*

## The place

**Seven Mile Beach, NSW — 34.86°S, 150.76°E.** A real east-facing sweep of sand with forest on the
coastal plain behind it, about forty kilometres from the wake point. The founder spawns on the
upper beach, waterline 45 m to the east, foredune 90 m to the west, the workshop in its lee, and
the forest beyond. This region is the default world; the highlands remain behind
`-eg-region highlands`, and every legacy self-test forces them automatically.

## How it is built

The global elevation data is ~10 km a pixel and cannot say where a waterline is, so near the coast
the region owns the profile (`Sim/World/Region.cs`): a swash zone, a beach face at the **1:36**
slope fine sand actually stands at, a 4.3 m foredune, a plain, and a smooth handover to the
data-driven bedrock inland. Erosion still runs, but is only trusted where the soil starts — a beach
is not an eroded landform in this model's terms, it is sand the sea keeps placing.

Everything else falls out of the systems that already existed:

- **Bare sand is barren** because `SiteAt` reports 2 cm of "soil" there and every plant's minimum
  depth vetoes it. **Dune sand grows tussock** because 10 cm at wetness 0.18 admits exactly the
  grasses and nothing with a trunk. **The forest behind** is the same nine-species ecology on the
  plain's real soil — measured 100% canopy over 120 sites.
- **The sea is salt**: the drainage network happily marks channels across the seabed, so
  `WaterBodies.IsSea` excludes anything below 0.4 m from drinking, from distance-to-water, and
  from creek quads. Measured: standing at the waterline, `CanDrinkAt` is false; the nearest fresh
  water is **174 m** inland.
- **Sand costs what sand costs**: Pandolf's loose-ground factor (1.8×) applies on it — 444 W
  against 331 W for the same walk — and it has its own footstep sound.

## The waves

One 14 km transparent plane at sea level; the shader (`EarthGame/Ocean`) works in metres seaward
of the same waterline curve the terrain is built from. Two breaker fronts sweep from ~55 m out to
the sand on nine- and eleven-second periods with a z-dependent wobble, so the crash line is never
straight and never synchronised, over a swash-foam band at the waterline. The surf is an 18-second
generated loop — a low bed of broken water with two breaker envelopes — whose source rides the
waterline at the player's own north, so the sound stays abreast along the whole beach.

## The workshop

An open-fronted shed behind the dune at about (−63, 14), stocked once per new game through the
same `PlacedItem` records every dropped object uses. "Full of tools" in this game's vocabulary is
the complete Phase 2 kit:

| on the bench | why |
|---|---|
| basalt cobble 1.4 kg | the hammerstone — best pounding stone in the catalogue |
| flint 0.9 kg, chert 0.7 kg | the edges that are coming |
| shale 0.6 kg | good for nothing, and finding that out is part of learning stone |
| grass-tree stalk | a fire-drill spindle, for whoever recognises it |
| sandstone slab 3.2 kg | the grinder, leaning on a bench leg |
| digging stick, firewood, softwood hearth board | on the ground beside |
| 6 m lomandra cord + dressed fibre | in the founder's hands |
| a laid fire ring, **unlit** | the drill is on the bench; the ember is the founder's problem |

## Measured (BeachSelfTest, `-eg-newgame -eg-beach-test`)

Sea −3.00 m at waterline+60; beach 0.84 m at −30; dune crest 5.92 m; 0 of 60 bare-sand samples
carry plants; 120 of 120 plain sites carry canopy; 9 placed tools; surf playing; sea not drinkable;
fresh water 174 m; spawn surface Sand. Highlands regression: walk test PASS, 239/239 headless.

## First-pass limits

- The eroded core is still 2.4 km around spawn; the 10×10 is region spec + far terrain.
- Swash does not wash over the sand (the sea plane is occluded above 0 m).
- Saves do not record the region; it comes from launch flags, Beach default.
- The waves are a shader; the water has no physics — wading and swimming are future slices.
