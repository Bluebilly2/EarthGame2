# Contract WG.1b — Rivers that run through

**Status:** drafted 2026-09-24 by the side worker on the main session's decision (ruling 46: the rivers first, then the WG.2c
proposal), measured on the sweep's fresh worlds of 15e05e3 and on a trial build of the rule below. **Read and decided the same
day by the main session** (ruling 46):
- Rule 5, the dams table, is built in this slice: Fitzroy Falls is William's chosen place (ruling 43), and Yarrunga Creek has
  to go over it.
- The wake must not land on a left-out reservoir's or pond's flat. If the scorer still picked one after rule 5, those outlines
  would come out of its candidates; it did not (below).
- The side worker may change `vantages.py`'s valley-whole wake row and remake the three template worlds in this slice. The
  gate world's wake, drink place and loop must come out unchanged, proved by the drink scenario and `join_check --only N2` on a
  corpus walk.
- The OpenStreetMap stream query is a read source (ruling 42 is about datasets baked into a world); DEM-H or DEM-S stays
  William's yes.
- The cost stays within 10 s of today's making time.

**Built** in the side copy (the sections "What was built" and "The exit record" below). Owner: Claude (the side worker):
`DrainageNetwork`, the lake rule in `WorldLayers`, and the verifiers that restate them. The main session's lane: the wake, the
sweep after it lands.

## Why

`census_check`'s valley rows (2be3375) found on their first run that the valley's rivers end in ponds (DEBTS: "The valley's
rivers end in ponds"): the Kangaroo River gathers 182.9 km² and ends in a pond of 0.58 ha 1.7 km above Hampden Bridge, where its
gauge publishes 334 km² and the world's river gathers 3 km². `DrainageNetwork` makes every lake a sink that passes nothing on
(2026-09-09, for the peninsula's dune lakes). Water is the Founder's Path's first beat, and WG.2c's river plants would stand on
the wrong rivers.

## What the measuring found

Each lake's surface against its basin's lowest lip, the lip found by a fill from the sea and the grid's edge that leaves no
gradient across a hollow and treats no lake as a sink (`drainage_check`'s own fill), on the sweep's worlds:

| World | Lakes | Held below their lip | The ones that matter |
|---|---|---|---|
| Bherwerre (gate) | 84 patches, 37.0 ha | 3 over 2 m | Windermere +30.0 m, McKenzie +15.7 m, Blacks Waterhole +5.9 m; every other lake at most +1.5 m |
| Whole valley | 762 patches, 61.1 ha | 148 over 0 m, 36 over 2 m | the Kangaroo River's pond at the village +13.5 m (182.9 km²); Barrengarry Creek's +6.5 m (29.1 km², the same lip); Upper Kangaroo Valley +6.2 m (33.6 km²); the south edge +8.2 m (30.1 km²); Lake Yarrunga's arm +4.0 m (52.0 km²); two on the Shoalhaven's floodplain +3.0 m (21.2, 18.6 km²); above Carrington Falls +2.3 m (10.1 km²); the plateau above Fitzroy Falls +1.1 m (33.1 km²) |
| 8 km valley | 79 patches, 135.3 ha | 78 over 1 m | the reservoir's flat, 660–668 m, held 1.0–3.7 m by the dam (water baked before WG.2b's reservoir rule) |

So opening the lakes is not enough on its own. The Kangaroo River's way out of the village pond runs 4.3 km across the flat
floor at 72–76 m. It passes 95 m from Hampden Bridge, then climbs 76.8 → 87.5 m over about 430 m, 681 m past the bridge, where
the real river drops into its gorge towards Lake Yarrunga. With the pond opened and filled, the whole floor round the village
would stand 13.5 m deep behind that rise, and the drainage's "pond" cells class as swamp and dry, not stream. The tiles have
lost the river's bed there.

## The rule, kind by kind

1. **The sea** is the base level, as now.
2. **A lake that holds its water** stays a sink: its inflow ends in it (the sand and the sun take it). It is named in its
   region's table with its reason. Bherwerre's three:
   - Windermere and McKenzie: Parks Australia (booderee.gov.au, "Geology") says they "evolved when streams were blocked by
     sand".
   - All three: OpenStreetMap maps no stream within 900, 600 and 400 m of them (read 2026-09-24). As a control, the same query
     round Hampden Bridge returns the Kangaroo River, Tanners Creek and a weir.
   - All three: the tiles hold each 5.9 to 30 m below its basin's lip.

   The whole valley names none.
3. **Every other lake passes its water on** at its own level. The published reasons:
   - The Kangaroo River runs past the village and under Hampden Bridge to Lake Yarrunga (Wikipedia).
   - Its gauge 215220 at Hampden Bridge gathers 334 km², a mean 185.73 GL a year (Bioregional Assessments, Sydney Basin
     context statement, table 17).
   - Yarrunga Creek's water "makes its way through Yarrunga Valley toward the Shoalhaven River" below Fitzroy Falls (NSW
     National Parks, the Fitzroy Falls lookout).
4. **Where the ground holds an open lake below its basin's lip, the lowest way out is cut** in the surface the water is routed
   on. The cut descends from the lake's level to the first ground that drains lower; lower lakes are cut first, and a later cut
   may end in an earlier one. The ground itself is not changed: the heights layer, the walk and `region_stats`' "the land is
   the bake's" stay as they are.

   The reason: the tiles' source here is SRTM (its voids show it), a surface model. Geoscience Australia made its 1-second
   ground model from SRTM "by automatically removing vegetation offsets", and its DEM-H from that "using ... mapped stream lines"
   to carry the flow paths. The tiles keep the offsets, so a river under forest in a gorge narrower than a cell reads as high
   ground. Cutting the way out is the conditioning DEM-H itself was made by.
5. **A dam** (proposed; see "The dam" below): a left-out reservoir's way out is cut through its dam's published point.

## What moved in the trial

A trial build of rules 1–4, made from 2be3375 in a scratch copy (b1), against the same commit untouched (b0), on the same data:

| | Bherwerre | 8 km valley | Whole valley |
|---|---|---|---|
| Wake | (-1392, 2804), unchanged | (436, -1040), unchanged | (10840, -9268) → **(-7236, 8124)**: onto Fitzroy Falls Reservoir's flat at 662.4 m, 4 m from a stream of 1.93 km² that was dry before |
| The drink scenario's place | (-1392, 2804), unchanged; its creek 0.18 m deep, 0.18 km², unchanged | (none within 150 m of a sea) | (not a coast scenario) |
| Water classes changed | 5,921 cells (swamp −3.0 ha, streams +0.5 ha) | 39,690 cells (swamp −12.2 ha, streams +3.4 ha) | see below |
| Largest catchment gains | the creek mouths behind Bherwerre Beach: +5.35 km² (swamp → stream), +4.14 km², +2.51 km² | the reservoir flat's 15.5 km² go on south-east | the Kangaroo River: 371.2 km² under Hampden Bridge |
| Census (`census.txt`) | two figures: swamp 342.0 → 339.1 ha, streams 6.0 → 6.5 ha, creeks 15.9 → 16.0 ha, trickles 31.4 → 31.8 ha | swamp 254.2 → 242.0 ha, streams 13.3 → 16.7 ha | |
| `census_check` | every row as before (Ryans Swamp's nearest patch 255 → 254 m) | Fitzroy still owed | Hampden Bridge **passes** (16 m from the point, 371.2 km²); the gauge **passes** (378.5 km² against 334); Belmore, the tops and the escarpment as before; Fitzroy and Carrington still fail (below) |
| Time to make (server) | 8.9 → 9.7 s | 6.2 → 6.6 s | **87.7 → 138.3 s** |

The corpus loop is laid round the gate world's wake, which does not move, so it needs nothing. `drainage_check` on the trial's
Bherwerre world fails two rows, where its own restatement of the old rule disagrees:
- the second creek: engine 334,538 cells, the check 185;
- the flow's law at the creek mouths.

It is to be given the new rule in its own code.

## What the trial left

- **Fitzroy Falls**: the creek at the falls still gathers 0.32 km². The plateau's 33 km² now leave the pond, cross the
  reservoir's flat at 662–663 m and drop off the escarpment 1.7 km south-east, into Kangaroo Valley near Barrengarry. The real
  Yarrunga Creek went over Fitzroy Falls. In the tiles the dam blocks it: Fitzroy Falls Dam, Wikipedia's point 34.64611 S
  150.48750 E, 500 m north-east of the falls, 14 m high and 1,530 m long. Its wall stands higher than a saddle 1 m above the
  flat. That misrouted water is also why the gauge reads 13 % over its 334 km²; without it the reading would be about 3 % over.
- **Carrington Falls**: its river now gathers 27.1 km² (16.2 before), but falls 21.8 m within 100 m of run where the NSW
  Government says 50. The plunge is not in the tiles; its ground lies in pools at 562 and 528 m with the gorge 300–400 m
  south-west.
- **The cost**: 50.6 s more to make the whole valley on the server, where the in-game making time is already over its five
  minutes (DEBTS). The trial's level fill and its searches use a dictionary and a list heap; the build will measure the
  stages and bring the rule within 10 s of today, or say why not.

## The dam (rule 5)

A dam's reservoir is let across its wall and down the creek it dams, between two published points: the wall's, and a point on
the creek below it. The region's table names the dams whose reservoirs' water matters. Fitzroy Falls Dam is in both valley
regions:
- the wall at Wikipedia's 34 38 46 S 150 29 15 E (on Yarrunga Creek, 14 m high, 1,530 m long, 1974);
- below it, the NSW Government's map point for Fitzroy Falls, 500 m down the creek.

The 117 farm dams need none: their flats are small and their lowest lips are their own spillways.

**Changed on the way.** Proposed as a way out cut through the dam's point to the first lower ground, first built as a disc of
100 m round the point lowered to the lowest ground in it. That did not cross the wall. Wikipedia's point lies on the reservoir's
side of a ridge the tiles hold at 673 to 677 m, 150 to 250 m wide, and the lowest ground within 100 m was the reservoir's own
floor (659 m); the low ground below the wall begins 200 m west.

An unbounded search from the wall to the falls fails too. It finds the way round: through the reservoir, over its saddle, down
the escarpment and back up the gorge below the falls, none of which stands above the reservoir.

So each of the two searches is kept near the wall, taking the lowest way (the highest cell lowest, then the lower ground,
then the lower cell number):
- from the wall's point to the reservoir, the nearest lake cell or cell of a left-out outline, within 300 m;
- from the wall's point to the point below, within the ellipse about the two points whose distances sum to 200 m more than
  the straight way.

The joined way is cut to descend from the reservoir's ground to the point below's. In the whole valley it is 197 cells from
664.7 m to 648.7 m; in the 8 km valley 329 cells from 662.0 m to 649.0 m. `drainage_check`'s own search finds the same ways,
cell for cell.

With it, Yarrunga Creek falls 118.4 m over Fitzroy Falls in both valleys, and the whole valley's wake stays on the Shoalhaven's
floodplain: the scorer picks no left-out flat, so no outline is taken out of its candidates. The rest of the reservoir debt
stays owed: the ground under a dam's flat, where a creek's valley was.

## What it does not touch

No format changes: the stand, cover and tile formats and the protocol are untouched. The world's layers change in what they
hold: catchment, water, surface, wetness, suitability, the plants, the stand, the loose layer, the distances, capacity and
wake_score. A world made before keeps its layers; a world made after differs.

## How it is proved

- **Tests, seen red first:**
  - a pond on a watercourse passes its catchment on;
  - a lake its region holds stays a sink;
  - a lake behind a false barrier gets its way out cut while the ground (the heights and the surface layer's ground) is
    unchanged;
  - a reservoir's way out goes through its dam's point;
  - the existing tests that make a world twice stay byte-identical.
- **`drainage_check`:** the rule from its own code, with its own held-lake and dam tables and sources, its own level fill and
  its own lowest way out. Its rows print both numbers and are green on the three worlds. Sabotages: a held lake opened; a cut
  left out.
- **`census_check`:**
  - the whole valley's Hampden Bridge and gauge rows leave the owed table (they print PAID today on the trial);
  - Fitzroy's row passes with the dam, or is re-owed under the reservoir row;
  - Carrington's fall is re-owed under a row of its own ("Carrington Falls is not in the tiles");
  - "The valley's rivers end in ponds" moves to Paid.
- **The rest:** `stand_check`, `cover_check`, `species_check` and `region_stats` are green on the three worlds. The engine
  suite and the Unity-shaped compile are green. The whole valley's making time and peak memory are measured on the server and
  in the game.
- **The sweep** is started by the main session after it lands.

## What was built

- **`Region`:**
  - `HeldLakes` names the water bake's lakes that hold their water: Bherwerre's three, with their sources in its doc.
  - `Dams` names the dams with their two points: Fitzroy Falls Dam in both valley regions.
- **`DrainageNetwork`:**
  - a routing surface of its own beside the ground; standing water's depth is read against the ground, and never below
    nothing;
  - `LevelFill`, the flood that leaves no gradient across a hollow, with a plain queue for the cells it raises (Barnes,
    Lehman and Mulla 2014);
  - `CellHeap`, a growing heap ordered by a key, a second key, then the cell's index.
- **`WorldLayers`:**
  - `HeldLakes` marks the patches of lake cells that touch a held lake's outline.
  - `Outlets` lets each dam across its wall, level-fills that surface with the sea and the held lakes as sinks, then, lowest
    level first and then by first cell, cuts each open lake's lowest way out where its basin spills above its level: to the
    first cell that spills lower, the grid's edge, or a way already cut.
  - The drainage runs on that surface with the sea and the held lakes as its sinks.
  - A stage of its own on the loading screen, "Finding where the lakes spill", lets the making collect between it and the
    drainage.
  - The census names each held lake "holding its water", each dam's way (cells, from and to), and the number of ways cut and
    the longest.
- **`WorldCreation`:** the catchment layer's description says what it is routed on.
- **`drainage_check`:** restates the rule from its own code, with its own tables (`HELD_LAKES_BY_REGION`, `DAMS_BY_REGION`),
  its own level fill (its flood with no step), its own searches, and cuts in 32-bit floats.
- **`census_check`:**
  - Fitzroy's, Hampden Bridge's and the gauge's rows leave the owed table;
  - Carrington's is owed under "Carrington Falls is not in the tiles".
- **DEBTS:**
  - "The valley's rivers end in ponds" is paid;
  - the reservoir row says the water now crosses the dam;
  - Carrington's row is new.

## The exit record

**Tests.** `RiversThatRunThroughTests`, on a made valley whose floor falls east and whose sides rise 2 m a cell:
- a pond on a watercourse passes its water on (below it 467,200 m² before, against 1,626,100 in the same valley without it);
- a lake behind a wall 5 m above it has its way out cut, and the wall is still in the ground with no pond standing behind it;
- a lake its region holds keeps its water;
- a reservoir's water leaves by the gully over its saddle until its dam is named, then crosses the dam, for a lake and for a
  left-out outline alike.

The three that test the new behaviour were red on the code as it was. The held lake is the guard of the old behaviour.

Two fixes of the tests themselves on the way:
- the made valley's sides first rose half a metre a cell, the lake rule's own band, so the flat leaked along the slopes and
  was no lake;
- the gully's water crosses its flat floor in more than one column, so it is summed across the floor.

Two findings of the rule on the way:
- lower cell number first alone ran the wall's way out along the valley's side 30 cells before it dropped, a canal on the
  slope, so the lower ground comes first;
- the disc at the dam (above).

The engine suite: 853 tests, 0 failed.

**Sabotages.** Four of the engine's rule, each turning its test red, every file restored byte for byte:
- every lake held → the pond, the wall and the dam;
- the ways out not cut → the wall;
- the dams ignored → the dam;
- no lower ground first → the wall.

Three of `drainage_check`'s restatement, each turning its rows red:
- no lake held (Bherwerre) → the second creek, both held lakes and the flow's law;
- no way out cut (Bherwerre) → the flow's law;
- the dam ignored (8 km valley) → both creeks and the flow's law.

**The worlds**, made by the dedicated server at the slice's code (`Artefacts/worlds/wg1b-s3-*`) against the commit before it
(`wg1b-b0-*`):

| | Bherwerre | 8 km valley | Whole valley |
|---|---|---|---|
| Wake | (-1392, 2804), unchanged | (436, -1040), unchanged | (10840, -9268), unchanged |
| Drink place and its creek | (-1392, 2804), 0.18 m, 0.18 km², unchanged | | |
| Water classes changed | 4,677 cells (swamp −2.6 ha, streams +0.5 ha) | | |
| Held, cut, dams | 3 lakes held (7 patches), 15 ways cut, the longest 77 cells | 26 ways cut, the longest 772; the dam, 329 cells | 106 ways cut, the longest 1,445; the dam, 197 cells |
| `census_check` | every row ok | Fitzroy ok: 118.4 m (published 81 to 100 m) | Fitzroy ok (118.4 m, 35.5 km²); Hampden Bridge ok (17 m, 335.9 km²); gauge ok (343.2 against 334 km²); Carrington owed |
| `drainage_check` | ok: 14,079 of 14,129 channel cells on the flow's law | ok: 23,301 of 23,350 | ok: 407,190 of 409,136, in 218.5 s |
| stand, cover, species, region_stats | ok | ok | ok (species: the eight rows owed as before; region_stats: the land is the bake's, cell for cell) |

**The cost**, the dedicated server under a request for quiet from the sweep: the whole valley made in 87.2 s where the commit
before took 79.7 s. The new stage takes 8 s and the drainage stage is unchanged at 13 to 14 s. Bherwerre takes 9.1 s and the
8 km valley 6.3 s.

**Still to do before the exit is whole:** the three template worlds remade (moved aside, not deleted) and the drink scenario and
a corpus walk with `join_check --only N2` on the new gate world. `vantages.py` needs no change: the whole valley's wake did not
move.

**Owed after, not in this slice:**
- the ground itself under the lost reaches, from Geoscience Australia's DEM-H or DEM-S (William's yes to a download; WG.0
  already waits on it), which Carrington's row waits on too;
- the 8 km valley's water baked again with the reservoir rule;
- the ground under a dam's flat.
