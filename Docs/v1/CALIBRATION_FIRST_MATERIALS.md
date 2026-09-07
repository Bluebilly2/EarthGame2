# Calibration — first materials on the beach

**Owner:** DEV 2 (sim track). **Measured:** 2026-09-01, commits `2da247b`…`5b72b31` on `track-sim`.

What this file is for: the littoral supply only runs where the elevation grid is loaded, and the
grid is absent from the headless suite. So the composition of the wake point's beach cannot be
asserted by a test, and would otherwise be believed rather than known. It is measured here, with
the method written down so anyone can repeat it and disagree.

## Method

Through the game's own Sim calls, not a second derivation of any of them. A scratch console
harness compiled the `Assets/EarthGame/Sim` tree, loaded
`Assets/StreamingAssets/EarthElevation.r16` via `ElevationGrid.FromInt16LittleEndian`, and
reproduced only the *wiring* `ScatterField.ShoreCobbles` does — grid, `LocalFrame` at the region
origin, planet radius 6371 km. Every answer below comes from `TerrainClassifier.Dominant`,
`SurfaceGeology.DominantStone` / `SecondaryStone` and `ShoreMaterials.Cobbles`.

The wiring had to be reproduced because `EarthElevation` and `TerrainSynthesis` import
UnityEngine and cannot be compiled outside the editor. `TerrainSynthesis.DominantClass` is a
four-line wrapper over `TerrainContext.Sample` + `TerrainClassifier.Dominant`, both of which are
Sim and were called directly.

## The coast this beach is fed by

Eleven samples along the waterline, ±4000 m of local north.

| north, m | terrain class | dominant | secondary |
|---:|---|---|---|
| −3636 | ContinentalShelf | Sandstone | Quartzite |
| −2909 | ContinentalShelf | Basalt | Shale |
| −2182 | ContinentalShelf | Basalt | Shale |
| −1455 | ContinentalShelf | Sandstone | Chert |
| −727 | ContinentalShelf | Basalt | Chert |
| 0 | ContinentalShelf | Basalt | Sandstone |
| 727 | ContinentalShelf | Basalt | Flint |
| 1455 | ContinentalShelf | Sandstone | Chert |
| 2182 | ContinentalShelf | Sandstone | Chert |
| 2909 | **CoastalPlain** | **Flint** | Sandstone |
| 3636 | CoastalPlain | Shale | Quartzite |

The flint and most of the chert come from up the coast, not from underfoot — two `CoastalPlain`
headlands at the northern end and chert as the secondary rock at four places between. That is the
whole mechanism visible in one table: **the beach is made of a coastline.**

## What the surf leaves

| stone | share | largest | knappability | pounding |
|---|---:|---:|---:|---:|
| Basalt | 45.3 % | 0.15 m | 0.18 | **0.55** |
| Sandstone | 26.9 % | 0.11 m | 0.07 | 0.28 |
| Chert | 10.4 % | 0.14 m | **0.66** | 0.08 |
| Flint | 9.9 % | 0.13 m | **0.77** | 0.05 |
| Quartzite | 7.6 % | **0.18 m** | **0.29** | 0.35 |

`ShoreMaterials.CanArmAKnapper` → **yes**. Knappable share (≥ `Knapping.MinimumKnappability`,
0.25) is **27.8 %**; hammerstone share (pounding ≥ 0.5, which in this catalogue is basalt alone)
is **45.3 %**.

`ScatterField` puts 0–3 stones in each 32 m cell, so a 60 m search covers roughly eleven cells and
about sixteen cobbles: **≈ 4.5 knappable and ≈ 7.2 hammerstones within a first search.** The
founder has to look, and will find.

Quartzite being both the most durable cobble on the beach and the largest is not a coincidence
arranged for the table — it is the same fact twice, because `CobbleKind.LargestM` is derived from
durability. It is also, at 0.29, just over the knapping threshold. A beach's best cobble being
barely workable is a good first lesson in stone.

## What it replaced, at the same spot

The wake point cell classifies as `ContinentalShelf` and outcrops **basalt (75 %) + sandstone
(25 %)** — knappability 0.18 and 0.07, **neither above the threshold.**

That is an exact match for what QA measured by playing it (`CI/board/tests/firststone.test.md`,
commit `aca8e5a`): *"The two cobbles a founder would actually pick up first … came out sandstone
and basalt, and the game offered NO knap verb for that pair at all."* The old rule and the old
measurement agree, which is what makes this a diagnosis rather than a guess.

## What a founder actually walks to

The shares above are the beach's composition. What matters to the arc is what lies *near the
founder*, which is a different question — so the cell walk was replayed too, from the wake point
at the local origin, 45 m inland.

This prediction rests on a **transcribed** random stream. `ScatterField.CellRandom` lives in a
UnityEngine file and had to be copied into the harness; it was checked for its own documented
properties (same cell → same numbers, neighbouring cells → different numbers) before anything
below was believed, but that is not proof it matches. **The stone *kinds* come from Sim and do not
depend on the transcription; the *positions and order* do.** Only a player run settles the order.

Within 60 m — `Scenario.SearchRadiusM`, what the criterion calls a first search — **19 cobbles: 4
knappable, 11 good hammerstones.** Nearest core: quartzite at **25.7 m**. Nearest hammerstone:
basalt at **18.1 m**.

| # | distance | stone | size | knappability | |
|---:|---:|---|---:|---:|---|
| 1 | 16.9 m | Sandstone | 0.04 m | 0.07 | |
| 2 | 18.1 m | Basalt | 0.11 m | 0.18 | hammer |
| 3 | 20.7 m | Basalt | 0.71 m | 0.18 | hammer (inland cell, old size rule) |
| 4 | 25.7 m | Quartzite | 0.17 m | 0.29 | **core** |
| 5 | 30.6 m | Sandstone | 0.15 m | 0.07 | |
| 6 | 35.2 m | Basalt | 0.07 m | 0.18 | hammer |
| 7 | 37.6 m | Flint | 0.08 m | 0.77 | **core** |

Against QA's measurement of the same ground before this work — *knappable 0 at the wake point,
nearest knappable stone a basalt cobble 65 m inland, five metres past the probe's reach* — the
first search now contains four knappable stones instead of none, and the nearest is 25.7 m
instead of out of range.

### And the criterion's third step would still be red

`firststone.test.md` walks to the nearest cobble, picks it up, walks to the next, picks that up,
and then expects `verbs.work contains knap`. **The two nearest are sandstone and basalt** — the
same pair QA played and reported. There is a hammerstone among them and no core, so no knap verb.

Three separate statements, and they should not be run together:

1. **Fact.** `expect nearby.knappable > 0` goes from red to green: 0 → 4.
2. **Fact.** `expect verbs.work contains knap` stays red, because it depends on the two *nearest*
   cobbles rather than on what is in reach.
3. **Judgement, for QA and FABLE and not for me to settle.** `FOUNDERS_PATH` describes this beat
   as *"Knap an edge (the wrong stone teaches as much as the right one)"*. A founder picking up a
   sandstone and a basalt and finding they will not flake is arguably the designed lesson rather
   than a defect — in which case the criterion wants a step that walks past the wrong stones, not a
   beach with fewer of them.

It would be very convenient for this seat to declare the test wrong, so it is worth saying what
was **not** done: the knappable share was not raised to make the nearest two work, and
`Knapping.MinimumKnappability` (0.25, with basalt at 0.18) was not touched. Both would have turned
the criterion green and both would have been tuning a world to pass a test.

## What is still unproven

Everything above is the model answering. Nobody has walked this beach.

- The cobbles are placed by `ScatterField.BuildCell`, which no test here exercised — object
  placement, harvest ids and the `Harvested` skip are unmeasured by this harness.
- `firststone.test.md` is the scenario that closes this, and it asks the right questions.
- `nearby.fibre` in that file will stay red for an unrelated reason: it counts `WorldObject.Species`
  and cannot see the underfoot half of `Cordmaking`. Filed with QA 2026-09-01; the dune's fibre is
  real and cuttable regardless.

## Copies that must carry the condition

The 27.8 % / 45.3 % figures are of **this** eleven-sample supply at **this** region origin. Change
`LittoralReachM`, `LittoralSamples` or `CountryRockShare` in `ScatterField` and they are void —
those three constants are the only inputs besides the dataset. Anyone quoting the shares elsewhere
must say which supply produced them.
