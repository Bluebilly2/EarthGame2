# The world makes itself

**Status:** design pillar, binding. Owner direction, 2026-08-25:
*"i want to design a real ecosystem and let it form naturally."*

## What was wrong

Water was drawn on top of a landscape that had no place for it, and it looked exactly like what it
was. The measurements said why:

> the global elevation dataset is about **9 km per pixel**, so across a 2.4 km catchment the real
> Earth contributes a gradient and nothing else — every hill and hollow the founder can see is
> procedural noise
>
> **53% of cells sat in closed hollows.** A real eroded landscape has almost none.

Fractal noise makes hummocks. It does not make valleys, because valleys are not a shape — they are
a **record of what water did**. Nothing had ever run over this ground, so there was nothing for a
drainage network to follow, and every attempt to place water was an attempt to fake the evidence
of a process that had not happened.

The same argument applies to everything above water in the chain. Soil placed by a noise function
is soil that no rock weathered into and no river deposited. Vegetation placed by a biome lookup is
vegetation that nothing competed for. Each is the same mistake at a different level.

## The chain

Each link is a consequence of the one above it. Nothing in it is placed.

| | Made by | Produces |
|---|---|---|
| **1. Rock** | real elevation, real geology | relief, and how hard the ground is to cut |
| **2. Landform** | water and gravity, over time | valleys, ridges, drainage, sediment |
| **3. Water** | where the landform sends it | creeks, rivers, wetlands, ponds |
| **4. Soil** | weathering + what the water deposited | depth, texture, drainage, fertility |
| **5. Plants** | soil + climate + light, and competition | communities, not a biome stamp |
| **6. Animals** | what the plants support | populations that eat, move and die |

The founder's knowledge has to transfer at every link (§3). Someone who knows that a north-facing
slope is drier, that a valley floor holds deeper soil, that river flats grow different trees from a
ridge crest, should be able to act on all of it — and they will be right, because each of those is
a consequence here rather than a rule.

## What this replaces

`TerrainSynthesis` classifies terrain and applies noise appropriate to the class. That gets the
*character* of a place roughly right and the *structure* of it entirely wrong, because character
was never the thing that made landforms. Erosion is not a filter applied to noise afterwards; it
is the thing that produced the shape in the first place, and it has to run.

The channel incision added while chasing the water problem was a fake of exactly this. It goes.

## Where it runs

Erosion is stateful and iterative, and the terrain is currently a pure function of position. The
two are reconciled the way the drainage network already is: **simulate on a region grid, cache the
result, and let the height function read it.** A region is computed once, deterministically, from
its own coordinates — so it is still reproducible from nothing and still never saved, but within a
region the ground is the outcome of a process rather than a formula.

## Build order

- **E1 Landform** — **done.** A landscape evolution model: uplift, fluvial incision by stream power, hillslope
  diffusion. This is the slice that makes valleys exist. `SLICE_E1_EROSION.md`.
- **E2 Water, again** — **done.** The incision hack is deleted; the drainage now follows valleys that water made,
  and the water sits in them. Wetlands where the ground is flat and wet, ponds only in real basins.
- **E3 Soil** — **done.** Depth from weathering against creep from what the model deposited; texture from the rock it came from;
  drainage from slope and position. Litter and moisture already read from the ground — they start
  reading this instead.
- **E4 Plants** (`SLICE_E4_PLANTS.md`) — **done.** Eight species described by what they can stand,
  contesting every site; suitability multiplies its conditions, so one a plant cannot meet rules it
  out however good the rest are. Measured on the real landscape: she-oak 98% of the dry exposed
  crests, stringybark 77% of the mid slopes, silver wattle 68% of the gully margins, ribbon gum 66%
  of the wet flats, with a genuine ecotone between each. Exposure came out inversely correlated
  with wetness on its own — crests dry and windy, hollows wet and sheltered — because the erosion
  made both.
- **E5 Animals** (`CONTRACT_E5_ANIMALS.md`) — **in flight.** What the plants can support:
  capacity from the E4 communities, presence as a stateless seeded function on the day's own
  clock, four pre-human species at the wake site.

E1–E4 are done. E5 was reached 2026-09-01 and its contract exists.

## The test that decides whether this worked

Not a screenshot. Someone who can read country should be able to stand anywhere in this world, say
where the water is, where the deep soil is, and which slope is the dry one — and be right, without
ever having been told, because all three are consequences of the same history.
