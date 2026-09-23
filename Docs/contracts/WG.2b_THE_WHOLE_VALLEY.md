# Contract WG.2b — The whole Kangaroo Valley

**Status:** drafted 2026-09-23 on CANON ruling 45 ("make the area much much bigger to fit the kangaroo valley") and measured
on trial worlds. **Decided the same day** (ruling 45): shown 24, 28 and 32 km with 28 recommended, William chose "32km please",
and of the manifest, "yes to the download list": the box is 32 km a side about 34.705 S 150.589 E at 4 m cells, and the 32 km
manifest below is approved. Owner: Claude (the side worker, `Docs/WORKING.md`'s two sessions). William's lane: his eyes on the
frames. The main session's lane, a dependency here: the far forest's cost (D1 below).

## What the box must hold

Ruling 45: the whole valley, rim to rim, with its falls. Every point below is OpenStreetMap's (Nominatim, 2026-09-23), put
through the sidecar's frame rule (the tangent plane, small-angle, R 6,371,000 m) about the proposed centre:

| Place | Latitude, longitude | East, north of the centre (m) | From the 32 km box's edge |
|---|---|---|---|
| The valley's own outline (`natural=valley`, relation 19186022): 19,261 ha, 24.2 km east-west by 20.0 km north-south | −34.6151..−34.7951, 150.4571..150.7212 | −12,059..12,082, −10,014..9,997 | 3.9 km east and west, 6.0 km north and south |
| Fitzroy Falls (the waterfall node) | −34.6480, 150.4825 | −9,735, 6,338 | 6.3 km |
| Belmore Falls | −34.6398, 150.5591 | −2,733, 7,250 | 8.8 km |
| Carrington Falls | −34.6238, 150.6549 | 6,024, 9,029 | 7.0 km |
| Cambewarra Mountain, the range's peak | −34.8002, 150.5774 | −1,060, −10,586 | 5.4 km |
| The village (the locality's centre) | −34.7352, 150.5325 | −5,165, −3,358 | 10.8 km |
| Hampden Bridge | −34.7275, 150.5209 | −6,225, −2,502 | 9.8 km |
| Bendeela, where the river meets Lake Yarrunga's head | −34.7406, 150.4721 | −10,686, −3,959 | 5.3 km |
| Robertson, on the plateau | −34.5900, 150.5928 | 347, 12,787 | 3.2 km |
| Tallowa Dam | −34.7728, 150.3128 | −25,248, −7,539 | outside by 9.2 km |

Today's box (8 km, centre −34.660, 150.500) holds 1,674 ha of the valley's outline, 9 %. Its centre is the outline's own:
the middle of its bounds, −34.705, 150.589, to the thousandth as the other regions are stated.

## The boxes measured

Measured on the valley's zoom-11 surround at 64 m (`Data/regions/kangaroo-valley/surround`) and the outlines above:

| Box about −34.705, 150.589 | Area | Of today's | The valley's outline inside | Beyond the outline, E-W / N-S | Lake Yarrunga's outline inside (relation 5448831, 1,088 ha) | Zoom-14 tiles (box + 1 km), held / to fetch |
|---|---|---|---|---|---|---|
| 24 km | 576 km² | 9 × | 19,259 of 19,261 ha | none / 2.0 km | 47 ha | 182: 30 / 152 |
| **28 km** | **784 km²** | **12¼ ×** | **all** | **1.9 / 4.0 km** | **86 ha** | **240: 36 / 204** |
| 32 km | 1,024 km² | 16 × | all | 3.9 / 6.0 km | 242 ha | 306: 36 / 270 |

The 24 km box is the outline and nothing past it east and west, where the world's edge would stand on the rim. The 32 km box's
south-east corner reaches the Shoalhaven's tidal reach at Bomaderry: its trial bake held 166,451 cells at or below the datum
there, a sea in an inland region. The 28 km box stops short of it.

## What size costs, measured

**The trial.** A scratch copy of the engine (built outside the tree, in the side worker's scratchpad) given trial regions of
16 and 32 km about the proposed centre, their heights baked at 4 m from the zoom-11 tiles already held (62.9 m pixels; the
ground is smoother than the zoom-14 bake will be, and no water outlines were fetched, so the world's water is the ground's own);
the game's side from a harness player built with the same trial regions (`install.py --into harness --label wg2b-trial`, the
trial lines taken out of the tree afterwards and both files checked byte for byte against HEAD). The baseline is the valley's
own 8 km data. Every number as run on 2026-09-23 on this machine (Ryzen 9 9950X3D, 32 threads, 31 GB); memory is the peak
committed by the whole process tree (a job object's `PeakJobMemoryUsed`); a range is two runs.

| | 8 km (4.0 M cells) | 16 km (16.0 M cells) |
|---|---|---|
| The bake (`bake_region.py`, banded, below) | 7.5 s, 1.0 GB | 18.6 s, 1.4 GB (32 km: 55.1 s, 2.5 GB) |
| A world made by the dedicated server | 11.6 s, 1.4–1.75 GB | 45.3 s, 5.2–5.8 GB |
| … with .NET's collector told to conserve memory (`DOTNET_GCConserveMemory=9`) | | 3.5 GB |
| A new world made in the game (the loading's "prepared in") | | 155.2 s, 4.3 GB for the whole game |
| The world folder | 176 MB | 703 MB |
| A saved world continued in the game | 3.1–4.7 s, 1.6 GB | 11.9–14.8 s, 2.9–3.2 GB |
| A saved world continued by the dedicated server | | 3 s to read, 3.7 GB; then every tile encoded at its start, 10.0–16.5 s |
| Playable after the connect (the join) | 2.5 s | 2.6–2.9 s |
| The frame, 1080p over a held full turn: median / p95 | 13.1 / 19.3 ms | 15.5 / 28.2 ms |
| … with the far forest hidden (`-eg-hide far`) | 5.4 / 7.1 ms | 5.5 / 7.4 ms |
| Far trees placed over the region | 37,561 | 149,299 |
| `drainage_check` | | 39.0 s, 2.9 GB |
| `cover_check`, `region_stats --world`, `tile_check`, `save_check` | | 1.8 s, 0.3 s, 0.8 s, 0.1 s; at most 1.5 GB |

What the trial shows:

1. **Memory and disk grow with the cells, in a straight line**: about 360 bytes a cell at the peak of a world's making in the
   server, about 45 bytes a cell on disk. Where it goes, from the stages' own counts at 16 km: the layers worked out hold 2.9 GB
   (180 bytes a cell) by the wake's choosing; the saving then climbs to 5.4 GB committed while the live heap swings between 2
   and 3.8 GB, each code layer widened to four bytes a cell (`Widen`, `Metres`) and copied again into its raw bytes before it is
   written, the copies left to the collector. A continued world holds ten layers, and `RegionRaster` keeps each as a float
   and, for a code layer, a uint beside it: 72 bytes a cell for layers stored in 18.
2. **Time grows about as the cells do** on a quiet machine (11.6 s to 45.3 s for four times the cells); the game makes a world
   about three times slower than the server (155.2 s against 45.3 to 52.6 s).
3. **The far forest is the frame's growth, and nothing else is.** Hidden, the median frame is 5.4 and 5.5 ms at 8 and 16 km;
   drawn, it costs 7.7 ms of the median already at 8 km and 10 ms at 16, and 12 and 21 ms of the p95. These frame figures were
   taken with other work on the machine and overstate the cost: on a quiet machine the main session's pair before its bound
   read the whole far band at 2.5 to 3.0 ms of the median at 8 km (M1.6f, 2026-09-23). What they show stands: the far forest is
   the one part of the frame that grows with the region.
4. **The bake would not fit at 32 km as it was**: its 7 × 7 median copied every cell's 49 neighbours at once (12.5 GB for
   8001 × 8001) beside 5 GB of coordinate grids. Banded (below), it is 2.5 GB.
5. **The checks hold up**, but `drainage_check` keeps every cell in Python lists and its priority flood's heap: 2.9 GB at 16 km.
6. **Precision:** Unity draws in single precision and has no floating origin here; at the 32 km box's corner a coordinate's
   step is about 1 mm, against 0.24 to 0.49 mm in today's box. Frames from the corners will say whether the hand shimmers.

**As the code stands, the whole valley is not playable** (the measured lines carried to the size): at 28 km a new world in
the game about 8 minutes and 13 GB, the server's making 16 to 18 GB, a continued world about 40 s and 9 to 10 GB, 457,000
far trees; at 32 km about 10 minutes and 17 GB, 21 to 23 GB, 50 to 60 s and 11 to 13 GB, 600,000. The machine has 31 GB, 18 GB of it free on
2026-09-23 with nothing of ours running, so a 32 km world was not made as the code stands: it would have taken the machine's
commit near its limit, where other programs fail to get memory. 4 m cells are not forbidden by the data: the stand, the
litter and the tuft rule are per cell, and at 8 m the forest would thin fourfold. The work below makes 4 m workable.

## The box, as decided

- **The box (his choice, ruling 45):** centre **34.705 S, 150.589 E** (the valley outline's own middle), **32 km** a side:
  **1,024 km², 16 times today's**; every fall, the rim on both sides and the river down to the lake's head inside, 3.9 km
  beyond the rim east and west and 6.0 km north and south. **4 m cells** (8001 × 8001, 64.0 M a layer). The recommendation
  was 28 km (784 km², 1.9 and 4.0 km beyond the rim, 31 % less of every cost); the numbers below are the 32 km box's.
- **The Shoalhaven in its south-east corner.** The box reaches the river's tidal reach at Bomaderry, which the trial's
  zoom-11 bake read at or below the datum on 166,451 cells. A tidal river at sea level is what the place holds there, so the
  world takes it as the tiles give it, sea where the ground is at the datum, and the verifiers say where it lies and how much.
- **A new region** `kangaroo-valley-whole`, "Kangaroo Valley, rim to rim"; the same late-winter wake (day 237, 08:00), Nowra's
  station (34.95 S 150.54 E by its table, about 28 km south-south-west of the centre, the nearest Bureau table held). `kangaroo-valley` (8 km) stays with its bake and the
  worlds made in it, William's two of 2026-09-22 among them (`Saves/world-31429723329453`, `world-31674518579933`), until he says
  to retire it; his first screen reads `Data/regions/<id>`, so the new bake goes under the new id and nothing of the old moves.
- **The surround** grows with the box: the camera's far plane is 40 km, so from the box's edge the view reaches 54 km from the
  centre; the far skirt is baked over **128 km at 64 m** (2001 × 2001), from zoom 11 (the proposal said 128 m; see "The
  fetch and the bake" below).
- **What it costs William:** about 336 MB of region data on disk (heights 256 MB, water 64 MB, surround 16 MB) and about 2.8 GB
  for each world he makes; after the work below, the targets are a new world in a few minutes and a continued one in about ten
  seconds (the measured numbers go in the exit record).

## The work that makes it workable

In this order; each engine change tests first and red, ARCHITECTURE amended in the same commit, the main session told first
where the file is one it works in. The proof that nothing else moved: the gate world and the 8 km valley made before and after
each change, every layer's checksum the same.

- **W1. A layer held in the width it is stored in.** `RegionRaster` keeps the raw bytes (one a cell for u8, two for u16) and
  reads a value or a code from them, instead of a float and a uint for every cell: the running world's hold from 72 to 18 bytes
  a cell (32 km: 4.6 GB to 1.2 GB), and the continue's decode loop gone. Heights stay f32, so `Heightfield` does not change
  (agreed with the main session, which works in `Heightfield.cs` for BF.4). The format is unchanged. **Landed 2026-09-23:**
  a saved 16 km world loads at a peak of 1.17 GB where it took 3.70 (the dedicated server, each run on its own copy); the
  gate world and the 8 km valley made by two scratch builds differing only in `RegionRaster.cs` have all 24 layers' sha256,
  the census and the wake the same; the new `RegionRasterTests` case reads every dtype's range bit for bit as the old decode
  did, and a lost i16 sign or a scale applied in single precision turns it red; the suite 833 green.
- **W2. A world made without the widened copies.** A code layer written from its own bytes, the raw buffer reused, and the
  big arrays let go once written; the target, at most 120 bytes a cell at the server's peak (32 km: 7.7 GB) and a new 32 km
  world in the game under 10 GB. **Landed 2026-09-23:** every layer written a megabyte at a time through the SHA-256, code
  layers from functions of the cell (no `Widen`, no `Metres`, no catchment array), the manifest's checksums read back in chunks
  (`RegionRaster.CheckedSha256`) rather than by loading each layer, a full collection between the making's stages, and the
  computed layers let go and collected before the new world is read back. A 16 km world: peak 5.17 GB before, 2.39 after, made
  in 27.8 s against 30.4; the gate world and the 8 km valley identical in every layer's sha256, the census and the wake. What
  is left at the top is the save itself, the computed layers and the wake scorer's fields both held, about 134 bytes a cell:
  some 9 GB at 32 km, over the 120-byte target; slimming it means changing what the scorer keeps, which waits on the 32 km
  world's own numbers (below).
- **W3. The making across the processor's cores**, if after W2 a new 32 km world in the game takes more than five minutes; the
  output byte for byte the same: whether each step's draws are seeded per cell, and so independent of the order cells are
  worked in, is read in the code before a step is spread, and the checksums prove it after.
- **W4. The dedicated server's tiles encoded across cores** at its start (at 32 km 9,216 tiles, about 40 s on one core).
- **W5. `drainage_check` in less memory**: the grid in numpy and only the flood's frontier in the heap; its algorithm stays its
  own (STANDARDS 19). **Landed 2026-09-23 (98f5bfc):** the flood's grid an array of doubles and the accumulation's order, counts,
  receivers and sea arrays of integers and bytes, not Python lists; HEAD's check and this one print the same verdicts line for
  line on the gate world, the 8 km valley and a 16 km trial world (there 2.92 GB and 21.9 s before, 2.17 GB and 16.8 s after);
  on the whole valley, ok in 93.7 s at 6.45 GB.
- **D1 (the main session's, a dependency): the far forest bounded by distance**, and drawn cheaper if the bound is not enough;
  its target, at most 2 ms of the median frame at 16 km, proved by the same first-frame scenario at 8 and 16 km. The whole
  valley is not put in William's hands before D1 lands. **Landed by the main session 2026-09-23 (fba255a, M1.6f):** the far trees
  thinned by distance in nested levels; at 8 km on a quiet machine the ring beyond the held tiles cost 0.27 and 0.44 ms of the
  median at the wake and the escarpment against 1.08 and 1.02 before, the whole far band 1.81 and 2.29 ms against 2.53 and 3.04;
  its 32 km measure is on the whole valley's world.

Already done on the way, in the tree: **the bake in bands** (`Tools/data/bake_region.py`): the coordinates and the two
median passes worked 256 rows at a time, each cell's arithmetic unchanged. Proved: the valley's 8 km bake rebaked with the
banded tool is byte for byte the stored one (sha256 39f2a674…, 457 cells despiked, as WG.2's); the banded despike against the
whole-array one from HEAD, on the valley's heights at thresholds of 2 and 5 m (8,020 and 325 cells rewritten) in bands of 7,
256 and 1,000 rows, every cell bit for bit the same; sabotaged (each band's window shifted a row), the same comparison fails at
every band size.

## Lake Yarrunga

The Tallowa Dam's lake floods the Kangaroo River's lower gorge; its eastern tip, the river's arm at Bendeela, is 242 ha inside
the 32 km box. `bake_water.py` leaves it out as a reservoir, as it did Fitzroy Falls Reservoir. Whether the tiles carry its
surface as a flat that the world's depression rule makes a lake again (DEBTS, "The reservoir's surface stands as ground")
cannot be read at zoom 11: the 64 m cells inside its outline read 46 to 187 m (median 72), the gorge's banks mixed in with the
water. It is measured on the zoom-14 bake, recorded, and a fix is proposed, not built without his word.

## The acquisition manifest (approved 2026-09-23)

For the 32 km box, approved ("yes to the download list", 2026-09-23); the 28 km box's figures, recommended and not chosen, in brackets:

1. **AWS Terrain Tiles, zoom 14, the box plus a kilometre**: `https://s3.amazonaws.com/elevation-tiles-prod/terrarium/14/{x}/{y}.png`
   for x 15037–15053, y 9869–9886, **306 tiles, 36 already held, 270 to fetch** [x 15038–15052, y 9870–9885: 240, 204 to fetch],
   about 93 KB a tile as the valley's own came, **about 25 MB** [19 MB]; by `fetch_tiles.py --zoom 14 --centre-lat -34.705
   --centre-lon 150.589 --extent-m 32000`. Licence: AWS Open Data (the Terrarium set), attributed as `terrarium.ATTRIBUTION`
   states; SRTM 30 m under them here.
2. **AWS Terrain Tiles, zoom 11, the 128 km surround**: x 1876–1884, y 1230–1238, **81 tiles, 39 held, 42 to fetch, about
   2 MB**; `fetch_tiles.py --zoom 11 ... --extent-m 128000 --margin-m 0`. The same source and licence.
3. **OpenStreetMap water**: one Overpass query, `bake_water.py`'s own for the new box padded by a kilometre, cached as
   `Data/cache/osm/kangaroo-valley-whole-water.json`, **one JSON response, under 1 MB**. ODbL 1.0, © OpenStreetMap
   contributors.
4. **The plant records for the verifier**: the Atlas of Living Australia's occurrences inside the box for the sixteen species of
   the valley's table (`fetch_ala.py`, its table given the new id), **16 small JSON files, about 1 MB**, an input to
   `species_check.py` only. CC BY, each record carrying its own licence.
5. **No station table**: Nowra's is held.

Total: **about 330 files, about 29 MB** [about 263 files, 23 MB]. Nothing else.

## What follows the yes

The fetch, the files named in `THIRD_PARTY_NOTICES.md`; `Region.KangarooValleyWhole` with its tests, `Climate.StationFor`,
`ShellController.DefaultPlaces` offering it; the bake (heights with `--inland --despike-m 25`, the surround, the water, the
records) and Lake Yarrunga measured; W1, W2, W4, W5 (W3 if its trigger holds); the world by `create.py --region
kangaroo-valley-whole`; the verifiers on it (`drainage_check`, `cover_check`, `species_check`, `region_stats --world`,
`tile_check`, `save_check`; `census_check` exits 2 by design, its debt standing); the loading scenario and `loading_check`;
`vantages.py`'s table for the new region and frames at 1440p and 1080p for his eyes — the floor, the village's site, the three
falls, the escarpment, the river and the lake's head, from the ground and from the lookout, and one from a corner for the
precision; every number in the exit record as run. Not in this slice: the valley's own plant table and stone rule (BF.4's), the
census, the reservoir's fix, the far forest (D1).

## The fetch and the bake (2026-09-23, on his yes)

**Fetched, as fetched.** Zoom 14: 270 tiles, 25.9 MB (306 on disk for the box, 29.3 MB). Zoom 11: 42 tiles, 1.5 MB (81 on
disk for the surround). One Overpass response, answered at the first ask: 195 elements, 273 KB, cached as
`Data/cache/osm/kangaroo-valley-whole-water.json`. The Atlas of Living Australia: 16 files, 3,029 records, 0.64 MB, under
`Data/cache/ala/kangaroo-valley-whole/` (kangaroo grass, which had no record in the 8 km box, has 120 here). Nothing else;
`THIRD_PARTY_NOTICES.md` names each.

**Baked.** `heights`: 8001 × 8001 at 4 m by the banded tool, −4.7 to 852.1 m, in 44.1 s with a peak of 2.55 GB; the despike at
25 m rewrote 1,740 cells (SRTM's voids on the steep faces; the 8 km box's bake rewrote 457). At or below the datum, 0.37 % of
the cells, all in the south-east corner (east 2.8 to 16 km, north −16 to −8.1 km): the Shoalhaven's floodplain near
Bomaderry. The highest ground, 852.1 m at east −1,864 north 15,416, is a smooth hill on the plateau north of the valley (672 m
a kilometre off, 832 to 852 m along the row through it), not a spike. Read at OpenStreetMap's points: the village 80.3 m,
Hampden Bridge 70.4 m, Fitzroy Falls 648.7 m, Belmore Falls 531.0 m, Carrington Falls 539.8 m, Cambewarra's lookout 627.8 m.
`surround`: 2001 × 2001 at 64 m over 128 km, −24.5 to 1,123.6 m, sea 32.8 %, the despike rewrote 141,686 cells (3.5 %).
`water_bodies`: 8001 × 8001, 78 bodies from OpenStreetMap, 50 with cells in the box (39 lakes, 11 wetlands, the largest
wetlands 447, 332 and 263 ha: two swamps on the Shoalhaven's floodplain and a bog on the plateau by Robertson), and 117 ways
left out as humanity's, Fitzroy Falls Lake and Wingecarribee Reservoir among them.

**What changed on the way.** (1) The surround at 64 m, not the 128 m proposed: at 128 m the despike's 7 × 7 window is 900 m
wide and rewrote 131,643 cells (13 %), cutting down real ridges and cliff tops the 25 m threshold cannot tell from bad
pixels; at 64 m it rewrote 3.5 %, as the 8 km valley's surround's 4.2 %, and the skirt samples it at about 125 m either way.
(2) The Shoalhaven River's water area (a way tagged `natural=water`, `water=river`, 13.5 ha in the box's south-west corner)
is read as a lake, and a mapped lake stands flat at the median of the ground inside it; it is kept, because the river
enters the box from a catchment far outside it and the drainage alone would make it a trickle (DEBTS, "A river that enters
from outside the box"). (3) The shell names the whole valley greyed "not yet" rather than offering it: a new world there as
the code stands would take about ten minutes and 17 GB in the game.

## The whole valley's world (2026-09-23)

**Made first** by the dedicated server built from HEAD (a8a305b, with W1 and W2) in a scratch folder, `+server.region
kangaroo-valley-whole`, into `Artefacts/worlds/valley-whole` (moved aside to `valley-whole-lakes` when the world was made again
below): 157.9 s to make its 24 layers (204.5 s with the start and every tile encoded), a peak of 8.39 GB committed, 2.8 GB on
disk. By stage: about 6.0 GB held while the layers are worked out (the
drainage and the soil at up to 7.5 GB committed), the peak in the save with the layers and the wake scorer's fields both held
(8.7 GB in use), and 0.6 GB once they are let go and the world read back. As the code stood this morning the same world would
have wanted some 23 GB. The server's start encodes its 9,216 tiles in 42.1 s (W4).

**The first world's census.** The scorer's wake: east −7,156 north 7,988, 662 m on the plateau in forest, 4 m from fresh water, 0 m from
knappable stone, fibre and firewood, 4 m from shelter rock. The water: 378.3 ha of sea (the Shoalhaven's floodplain in the
south-east corner), lakes 235.2 ha, swamp 3,796.9 ha, streams 264.0 ha, creeks 374.6 ha, trickles 805.9 ha. Of the lakes,
131.0 ha stand at 662 m on the flat Fitzroy Falls Reservoir left in the tiles, and 33.1 ha at 675 m on Wingecarribee
Reservoir's (none inside a mapped outline): the wake is beside the first, 4 m from its water (DEBTS, "The reservoir's surface
stands as ground"). Lake Yarrunga's arm, 186.7 ha inside the box on the 4 m bake, is not flat in the tiles (38 to 90 m inside
its outline, median 63.2 m, 19 % within a metre of it) and made no lake.

**The reservoir rule, and the world made again.** No lake of the ground's own making may stand inside an outline the water
bake left out: `bake_water.py` draws its left-out ways into the layer as kinds `reservoir` and `pond`, numbered after the kept
bodies and drawn before them (proved on the 8 km valley's cached query: the kept body's 60 cells keep their code, and all 305,482
newly coded cells carry left-out codes), and `WorldLayers.Lakes` keeps their cells out of the flat rule's seeds and patches.
`WorldLayersTests` holds the made coast's hollow, a lake alone, to no lake inside a reservoir's outline, red before the rule and
green after; the gate world and the 8 km valley, whose bakes carry no such kinds, made with and without it have every layer's
sha256, the census and the wake the same; the suite 839 green. The whole valley's water baked again from its cached query: 78
bodies kept as before and 117 left out now drawn (43 reservoirs, 699.1 ha; 74 ponds, 21.7 ha; Fitzroy Falls Lake's outline 476.7
ha in the box, Wingecarribee Reservoir's 188.2 ha).

**The world as it stands** (`Artefacts/worlds/valley-whole`, made by a scratch build of a8a305b with the rule, the files the
rule's commit carries): 125.0 s to make (155.0 s with the start;
the start's 9,216 tiles in 28.6 s), a peak of 8.39 GB committed, 2.8 GB on disk. The census: the scorer's wake at east 10,840
north −9,268, 2 m above the sea on the Shoalhaven's floodplain in the box's south-east corner, beyond the Cambewarra Range, in
forest (bangalay over Lomandra), 4 m from fresh water, its wind exposure 0.94. The water: sea 378.3 ha, lakes 61.1 ha (48.4 of
them read off the ground alone), swamp 4,038.8 ha, streams 267.1 ha, creeks 378.1 ha, trickles 807.2 ha; "Fitzroy Falls Lake: an
outline of 476.7 ha, a reservoir left out as humanity's, no lake in it". The wake is the scorer's by rulings 20 and 44, and the
main session holds the wake's question; that it lies outside the valley proper, a walk of some 10 km from the floor, is said
here and to William.

**The verifiers on it.** `drainage_check` ok in 93.7 s at 6.45 GB (401,004 of 403,279 channel cells within the flow's law; no
water off the channels on 63,338,131 cells); `cover_check` ok in 5.7 s (64,016,001 posts follow the cascade); `species_check` ok
with eight rows owed under "The valley grows the coast's plant table" (BF.4's), its table given the whole valley's rows;
`region_stats --world` ok (236,407 sea cells, the floor under the datum and within the rule's 30 m); `save_check` ok;
`census_check` holds no landmarks for the valley and exits 2 by design (DEBTS, "The valley has no census").

## Exit

The box and the manifest approved and fetched, with the sizes as fetched; the world made and every verifier green with its
numbers; the costs measured against the targets above; the frames; or "What changed on the way".
