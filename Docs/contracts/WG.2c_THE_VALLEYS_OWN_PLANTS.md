# Contract WG.2c — The valley's own plants

**Status:** drafted 2026-09-25 by the side worker at the main session's request ("the WG.2c proposal, no code, draft to me
before any"). Measured on the whole valley's template world as remade at fa4f501 (WG.1b's code) and on the 8 km valley's, by
exploration scripts that restate the rules (kept in the side worker's scratchpad, not the repository). **Read and decided the
same day by the main session**, all six questions as recommended (ruling 46; the questions and their reasons are the last
section):
1. A region names its plants: Bherwerre its twelve; both valleys the thirteen below; coast banksia, swamp paperbark and spinifex
   out; bangalay out until its records are fetched.
2. The slope a plant keeps its vigour to: zero for the coast's twelve.
3. The cell: two bytes, the catalogue number high and the height in 1.25 m steps low; `eg2.world` 1 → 2; `TileCodec` 3 → 4; old
   worlds read and converted in memory, not remade. **The protocol is 21**, this slice's: BF.4 stage three's first part (the
   rocks) changes no format or protocol, and its second part (the country's things, tile layer 9) takes the number after it and
   rebases on this cell.
4. The clip: accepted. The gate world's tallest blackbutts (at most 271) and their neighbours move; proved cell by cell and by
   the sweep, the count named in the exit record.
5. Lanes: the side worker builds both stages, the client's drawing included (`StandForms`, `TreeGeometry`, `StandMeshes`, the
   palm's crown), in the side copy with its own Unity; the main session reviews before each merge. **Stage one starts when the
   main session says BF.4 stage three's first part is on origin**: it touches `Runtime/World` (a new `StandingRocks.cs`;
   `FineGround`, `WorldState`, `Carrying`, `Systems`, `LooseTaken`, `StandingThings`, `Work`, `ThingWords`), the client core
   (`StandPreparation`, `LyingNear`, `ClientGround`, `StandForms`, a new `ClientRocks`) and Unity's `StandViews`, `StandMeshes`,
   `VerbController` and `ClientRuntime`. From then the stand files are this slice's until stage two lands.
6. The palm's missing wood: no stick or log for a tall plant with no `Wood` row; the palm is not felled or stripped in this
   slice.

Bangalay's download goes on William's list, put to him by the main session with the geology download.

**Amended the same day by the main session** (ruling 46), on the vegetation map it fetched (NVIS 7.0's pre-1750 Major Vegetation
Groups, DCCEEW, CC BY 4.0, `Data/cache/nvis/`, `Tools/data/fetch_nvis.py`):
7. **A sixth new plant, the valley's rainforest canopy tree.** The map puts rainforest on 20.6 % of the whole valley's square,
   and the plant table grew none: lilly pilly (below, "The rainforest tree").
8. **The map is the check, never the model's input.** A new verifier row holds the world's canopy share by Major Vegetation
   Group, and the valley's crown cover against the groups' typical cover. The plants stay placed by their tolerances. The
   canopy is to rise: the map's groups at their typical cover give about 0.52 of the whole valley's land under crowns,
   where the world as made covers about 0.33 (its 45 % canopy times the 0.74 of a canopied cell that crowns cover).
   *Corrected in stage two:* the 0.52 was foliage cover. The groups' NVIS fact sheets state crown cover, which is what the
   world's crowns are (open forest 50 to 80 %, woodland 20 to 50 %; rainforest's foliage cover over 70 % and tall open forest's
   30 to 70 % read to a crown cover by the sheets' own pairing), and against crown cover the whole valley's groups give 0.50
   to 0.78 together and the 8 km valley's 0.48 to 0.76. `stand_check` holds that range.
9. Its colours go in `StandForms` in sRGB, as the table's are, and its mesh by the existing path. `StandMeshes.ToColor` is the
   main session's (M1.6h makes every stand colour linear in one place). Each session tells the other before it touches
   `StandForms`, `TreeGeometry` or `StandMeshes`.

Owner: Claude (the side worker). The main session's lane: the review before each merge, the sweep after each stage.

## Why

Three findings, each recorded:

- **The valley grows the coast's trees** (DEBTS, "The valley grows the coast's plant table", 2026-09-22). The world's twelve
  plants are Bherwerre's, so the valley grows coast banksia and swamp paperbark on its plateau, 13.5 and 16.9 km from the sea
  where their records lie half a kilometre from it, and none of the five trees the Atlas of Living Australia records there:
  Sydney blue gum, river oak, the cabbage tree palm, silvertop ash and scribbly gum. The whole valley owes eight
  `species_check` rows under that debt, the 8 km valley four.
- **The valley's walls stand bare** (the same row; M1.6g, 2026-09-23): 47 per cent of the whole valley's 40 m squares at 30
  to 35 degrees and 85 per cent at 35 to 45 hold no trunk, where the real walls carry forest below their cliffs.
- **The stand byte is full.** A stand cell is one byte: the tall plant in its top three bits (seven at most, five used) and the
  height in its low five, in steps of 1.25 m. Ten tall plants will not fit in three bits, and 31 steps end at 38.75 m, where
  blackbutt's ceiling is 40 m and Sydney blue gum's is 50 (`StandCodes.HeightStepM`'s comment says the five bits reach past the
  tallest blackbutt; they do not).

In plain words: the valley is growing the wrong trees, and not growing them where it should. Fixing that needs room in the
world's record of trees, which is a change of file format and network protocol.

## What the measuring found (2026-09-25)

**Where the records stand.** Each plant's usable records (`species_check`'s rule: no planted specimen, placed to within 100 m)
laid on the whole valley's own layers. Records are gathered along tracks and lookouts, so every plant's share is read against
the share of *all* the plants' records in the same class: the recorders' own bias as the background (the "target-group
background" of presence-only modelling, Phillips and others 2009, *Ecological Applications* 19). Above 1 the plant is found
there more than the recorders' walking explains; below 1, less.

| By slope (degrees) | 0–15 | 15–25 | 25–31 | 31–35 | 35–45 | 45+ |
|---|---|---|---|---|---|---|
| the land | 74.4 % | 16.2 % | 4.9 % | 1.9 % | 2.0 % | 0.6 % |
| all records (the background) | 78.5 % | 14.8 % | 3.1 % | 1.5 % | 1.7 % | 0.4 % |
| Sydney blue gum (133) | 0.86 | 1.47 | 1.70 | 1.01 | 2.18 | 0 |
| cabbage tree palm (285) | 0.65 | 2.25 | 3.40 | 1.65 | 1.22 | 0.99 |
| silvertop ash (269) | 1.05 | 0.65 | 0.60 | 1.00 | 2.15 | 2.11 |
| river oak (103) | 1.12 | 0.46 | 1.57 | 0 | 0 | 0 |
| scribbly gum (102) | 1.24 | 0.20 | 0 | 0 | 0 | 0 |
| old-man banksia (235) | 0.94 | 0.89 | 1.10 | 2.28 | 2.71 | 4.82 |
| blackbutt (115) | 1.20 | 0.41 | 0 | 0 | 0 | 0 |
| **the world's canopy now** | 55.6 % | 22.1 % | 4.7 % | 0 | 0 | 0 |
| **the soil model's depth (median)** | 1.58 m | 0.86 m | 0.41 m | 0.13 m | 0.00 m | 0.00 m |

So blue gum and the palm are found *more* on the slopes than on the flat, and the world's canopy falls to nothing across the
same ground. Two rules do it: `PlantSpecies.Suitability`'s slope factor falls in a straight line from level ground to nothing at
each plant's steepest face (0.40 to 0.60 rise in run, 22 to 31 degrees), halving a tree's vigour on a moderate slope; and
`SoilModel` holds no soil past its angle of repose (0.7 rise in run, 35 degrees), so nothing that needs soil stands there at all.

| By wetness (the layer, 0–1) | 0–0.05 | 0.05–0.2 | 0.2–0.4 | 0.4–0.7 | 0.7–1 |
|---|---|---|---|---|---|
| Sydney blue gum | 0.92 | 0.98 | 1.19 | 0.92 | 0.92 |
| cabbage tree palm | 1.00 | 0.67 | 0.99 | 1.13 | 1.66 |
| silvertop ash | 1.28 | 1.11 | 0.98 | 0.80 | 0.67 |
| river oak | 0.74 | 0.73 | 0.98 | 1.33 | 1.56 |
| scribbly gum | 0.40 | 1.21 | 0.82 | 1.34 | 1.11 |

| By exposure (openness or salt wind, 0–1) | 0–0.05 | 0.05–0.3 | 0.3–0.6 | 0.6–1 |
|---|---|---|---|---|
| Sydney blue gum | 1.15 | 1.13 | 1.15 | 0.69 |
| cabbage tree palm | 1.69 | 0.46 | 0.37 | 0.79 |
| silvertop ash | 0.81 | 0.84 | 1.25 | 1.16 |
| river oak | 1.78 | 1.53 | 0.29 | 0.24 |
| scribbly gum | 0.83 | 2.08 | 1.39 | 0.49 |

The wetness signal is weak: a record placed to 100 m sits on a random 4 m cell of a wetness field that changes over metres.
The directions agree with what PlantNET says of each plant's habitat (below), and the tolerances are read from both.
River oak's records have a creek or stream cell within 50 m for 38 per cent of them, against 18 per cent of all records.

**The restatement is trusted because it reproduces the world.** The same scripts, run with the coast's five tall plants on
the whole valley, give the canopy the world has (55.6 / 22.2 / 5.0 / 0 per cent by class against the measured 55.6 / 22.1 /
4.7 / 0), and predict `species_check`'s "round its records" ratios the check measured: blackbutt 0.82 against 0.77, old-man
banksia 0.99 against 1.00, bracken 0.89 against 0.87, Lomandra 0.85 against 0.85, saw-sedge 0.78 against 0.79.

## The rule, part by part

### 1. A region names its plants

`Region` gains `Plants`: which of the plant catalogue's plants its country carries. Ecology calls this the regional species
pool: which plants reached a country is its history, and the tolerances then place them within it. The pool says nothing about
*where* in the region a plant stands; `ECOSYSTEM.md`'s principle ("described by what it can stand rather than by where it lives")
holds inside the region as before. The canopy and understory draws (`PlantCommunity`) choose only among the region's plants.

- **Bherwerre:** its twelve, as now, so nothing added to the catalogue can reach the coast.
- **Both Kangaroo Valley regions** (the same country, one list): blackbutt, old-man banksia, heath banksia, the grass tree,
  bracken, Lomandra, saw-sedge and kangaroo grass (WG.2's "stay"); Sydney blue gum, river oak, the cabbage tree palm, silvertop
  ash and scribbly gum (WG.2's "come in"); and, by the amendment, lilly pilly, the rainforest tree: fourteen in all. Coast banksia,
  swamp paperbark and spinifex are out (WG.2's "go"). The source for each
  is WG.2's table checked against the Atlas's usable records in the whole valley's box (the counts in the table above, and
  kangaroo grass's 85; the 8 km box is too small to judge by: one blackbutt record, none of kangaroo grass).
- **Bangalay** was named neither way by WG.2 and its records were never fetched for the valley (`fetch_ala.py`'s valley list
  leaves it out). PlantNET says every Sydney blue gum south of Port Jackson carries some of bangalay's genes, and BioNet names a
  "Sydney Blue Gum x Bangalay" moist forest of the southern Sydney Basin. It stays out of the valley's list until its records for
  the two boxes are fetched (two small files, William's yes: "For William", below).
- **What the list costs:** coast banksia's 17 and swamp paperbark's 9 records lie in the whole valley's south-east corner, on the
  Shoalhaven's floodplain half a kilometre from the sea, and will be grown nowhere. That is owed (a new DEBTS row) where today
  they are grown across the plateau.

The catalogue itself becomes append-only: the new plants take the numbers 13 to 18, and no plant is ever reordered or
removed, pinned by a test that names every number. The overstory, understory and stand layers all carry a plant by that number.

### 2. Five new plants, and the rainforest tree by the amendment

Each number with its source, as the coast's rows have them. The sources: **[P]** PlantNET, the NSW Flora Online (Royal
Botanic Gardens Sydney), read 2026-09-25; **[A]** the Atlas's records against the recorders' background, the tables above;
**[B]** NSW BioNet's community descriptions; **[H]** Claude's reading of the habitat the source describes, stated as a number
so a person who knows the country can dispute it (the coast's method, CANON ruling 21); **[E]** an estimate, said so.

**Sydney blue gum**, *Eucalyptus saligna*, tree. The valley's is the southern form PlantNET describes, with bangalay's influence.
- Height 25–50 m: to 50 m [P]; the floor half the ceiling, as blackbutt's range is [E].
- Moisture 0.30, breadth 0.25: "wet forest ... often on slopes" [P] [H], set at the records' one lean, 1.19 in the 0.2–0.4 class [A].
- Soil at least 0.20 m: the records' 5th percentile, 0.21 m [A]. PlantNET's "soils of moderate fertility" is not in the model.
- Slope: full vigour to 0.60 (31°), nothing past 0.90 (42°): found 1.5 to 2.2 times the background on 15 to 45 degrees [A],
  "often on slopes" [P], "in gullies and on sheltered slopes" [B]; the ceiling is the records' 99th percentile, 0.90 [A].
- Exposure tolerance 0.30: found 1.15 in the sheltered classes and 0.69 on the exposed [A], "sheltered slopes" [B].
- Shade tolerance 0.30, as blackbutt's [E] (a tree's own site is never shaded; the number is carried for the form's sake).
- Crown 0.35 of its height and 0.5 sticks a metre, as blackbutt, a tall forest eucalypt [E].
- Bark to strip: none. Smooth and powdery, shedding in short ribbons or flakes [P].

**Cabbage tree palm**, *Livistona australis*, tree (a palm).
- Height 15–30 m: a single stem to 30 m, occasionally more [P]; the floor half the ceiling [E].
- Moisture 0.80, breadth 0.25: "moist sclerophyll forest, often in swampy sites, and on margins of rainforest" [P] [H]; found
  1.66 times the background in the wettest class [A].
- Soil at least 0.10 m: the records' 5th percentile [A].
- Slope: full vigour to 0.70 (35°), nothing past 0.80 (39°): found 2.25, 3.40 and 1.65 times the background on 15–25, 25–31
  and 31–35 degrees [A]; the ceiling the records' 99th percentile, 0.76 [A].
- Exposure tolerance 0.15: found 1.69 times the background in the most sheltered class, 0.37 to 0.79 elsewhere [A].
- Shade tolerance 0.70: a palm of rainforest margins [P] [E].
- Crown 0.25 of its height: leaves 3 to 4.5 m long in a terminal crown [P], about 6 m across on a stem of about 24 m [E].
- Sticks: none. It drops fronds, and a frond as a thing is owed (a new DEBTS row) [E].
- Bark to strip: none; and **no wood**: a palm, like every monocot, makes none (no secondary growth). No `Wood` row.

**Silvertop ash**, *Eucalyptus sieberi*, tree.
- Height 15–45 m: to 30 m, sometimes to 45 m [P]; the floor set so the usual 30 m is the range's middle [E].
- Moisture 0.10, breadth 0.20: "shallow soils of low to medium fertility on rises" [P] [H]; found 1.28 times the background in the
  driest class, falling to 0.67 in the wettest [A].
- Soil at least 0.20 m: "shallow soils" [P] [H], under the records' 10th percentile, 0.30 m [A].
- Slope: the straight fall from level ground to nothing at 0.95 (43.5°), the records' 99th percentile [A]. No knee: its records
  dip on the middle slopes (0.65, 0.60) and rise on the steepest (2.15, 2.11: the escarpment's rims) [A].
- Exposure tolerance 0.70: found 1.25 and 1.16 times the background in the exposed classes [A], "on rises" [P].
- Shade tolerance 0.25, crown 0.35, 0.5 sticks a metre [E].
- Bark to strip 0.008 m: persistent on the trunk and larger branches, shortly fibrous and compact [P]; as blackbutt's rough lower
  bark [E].

**River oak**, *Casuarina cunninghamiana*, tree.
- Height 15–35 m: 15 to 35 m, rarely 50 [P].
- Moisture 0.80, breadth 0.20: "along permanent freshwater streams" [P] [H]; found 1.33 and 1.56 times the background in the two
  wettest classes, and 38 per cent of its records within 50 m of a creek or stream against 18 per cent of all [A].
- Soil at least 0.45 m: the records' 5th percentile, 0.46 m, the banks' alluvium [A].
- Slope: the straight fall to nothing at 0.55 (29°), the records' 99th percentile 0.54 [A].
- Exposure tolerance 0.30: found 1.78 and 1.53 times the background in the sheltered classes, 0.29 and 0.24 in the exposed [A].
- Shade tolerance 0.20: a tree of open banks [E]. Crown 0.35 [E]. Sticks 0.4 a metre: it sheds its branchlets as a mat, and
  fewer twigs than a eucalypt [E].
- Bark to strip: none. Finely fissured and scaly [P].

**Scribbly gum**, *Eucalyptus racemosa* (the Atlas files its records under its former name, *E. sclerophylla*), small tree.
- Height 7.5–15 m: to 15 m [P]; the floor half the ceiling [E].
- Moisture 0.30, breadth 0.25: "dry sclerophyll woodland on shallow infertile sandy soil on sandstone" [P] [H]; its records avoid
  the driest class (0.40) and are otherwise even (0.82 to 1.34) [A].
- Soil at least 0.20 m: "shallow ... soil" [P] [H]. (The model's plateau soil is deep: the records' 5th percentile is 0.80 m [A].)
- Slope: the straight fall to nothing at 0.35 (19°), the records' 99th percentile 0.33; found 0.20 times the background on 15 to
  25 degrees [A].
- Exposure tolerance 0.60: found 2.08 and 1.39 times the background in the middle classes [A], open woodland [P].
- Shade tolerance 0.15; crown 0.55, a spreading woodland crown as coast banksia's; 0.5 sticks a metre [E].
- Bark to strip: none. Smooth, with scribbles, shedding in short ribbons [P].

**The rainforest tree** (the amendment's seventh decision): **lilly pilly**, *Syzygium smithii* (PlantNET files it as *Acmena
smithii*), tree.

Chosen from twelve candidates, whose Atlas records over the whole valley's box were fetched on 2026-09-25 into the side
worker's scratchpad (12 files, 11,562 to 119,021 bytes, their SHA-256s in the stage-two exit record). The candidates were the
NSW Scientific Committee's characteristic trees of Illawarra Subtropical Rainforest (brush bloodwood, Illawarra flame tree, giant
stinging tree, native tamarind, brown beech, red cedar, three figs) and the gullies' warm temperate rainforest (lilly pilly,
sassafras, coachwood).

Lilly pilly was chosen because:
- it has the most usable records (503);
- its records lie in the map's rainforest 1.38 times as often as the recorders' own records do. Sassafras leans harder (1.58)
  on fewer records (448) and is the runner-up; the purest indicators, native tamarind (1.96) and the Illawarra flame tree
  (1.66), have 56 and 74 records;
- it leans to the 15-35 degree slopes the rainforest holds (1.25 to 1.79 times the background);
- BioNet names it in the valley's own moist forest ("Sydney Blue Gum x Bangalay - Lilly Pilly moist forest in gullies and on
  sheltered slopes");
- its fruit is food: white to maroon berries, eaten by Aboriginal people.

Its first values:
- Height 10-20 m: a tree to 20 m (Wikipedia's account), the floor half the ceiling [E].
- Moisture 0.60, breadth 0.35: "widespread in rainforest ... often along watercourses" [P] [H]. The records' wetness lean is
  flat (0.78 to 1.26) [A], hence the breadth.
- Soil at least 0.10 m: the records' 5th percentile, 0.12 m [A].
- Slope: full vigour to 0.70 (35 degrees), nothing past 0.80 (39 degrees): the lean to 15-35 degrees above, and the records'
  99th percentile, 0.79 [A].
- Exposure tolerance 0.20 and shade tolerance 0.85: a rainforest tree, raised in shade [H] [E]. The records' exposure lean is
  flat [A].
- Crown 0.50 of its height: 5 to 15 m wide on a 20 m tree [E from Wikipedia].
- Sticks 0.3 a metre [E].
- Bark to strip: none. Smooth to slightly flaky [P].
- Wood: its air-dry density, hardness and strength are estimates until the Global Wood Density Database (Zanne and others 2009,
  a download ruling 49 leaves to the agents) is read. The same file gives river oak and scribbly gum a published density.

**What they are first values of.** The tolerances are the build's starting point, not its answer. `species_check` decides, as it
did for the coast in M1.2b: a number moved to pass a row is moved with its reason written beside it in `ECOSYSTEM.md`.

**Wood — which of the ten tall plants have rows already.** Five have: blackbutt, bangalay, old-man banksia, coast banksia and
swamp paperbark (all read from memory of Bootle's and Ilic's tables, their re-reading owed: DEBTS 2026-09-22). The grass tree's
flower stalk has one too (the drill), and it is not a tall plant. None of the five new plants has one. The new rows:

| Plant | Air-dry density | Janka, dry | Rupture, dry | Source |
|---|---|---|---|---|
| Sydney blue gum | 840 kg/m³ | 8.1 kN | 122 MPa | WoodSolutions (Forest and Wood Products Australia), its Sydney blue gum page, seasoned figures; unseasoned 1,110 kg/m³, 5.8 kN, 76 MPa |
| silvertop ash | 850 kg/m³ | 9.7 kN | 137 MPa | WoodSolutions, "Ash, Silvertop", seasoned; unseasoned 1,100 kg/m³, 6.7 kN, 78 MPa |
| river oak | 850 kg/m³ | 8.0 kN **estimate** | 90 MPa **estimate** | World Agroforestry's species sheet: 800 to 900 kg/m³ (stated as green density, owed a re-reading against Bootle), "moderately strong but tough and fissile", "excellent firewood"; hardness and strength estimated from the table's hardwoods of that density, and marked as estimates as the grass tree's row is |
| scribbly gum | 850 kg/m³ **estimate** | 8.0 kN **estimate** | 100 MPa **estimate** | no figure found for *E. racemosa*; a search reports the sister scribbly gum *E. haemastoma* at 0.71 t/m³ in the USDA's i-Tree wood density table (Appendix 11; its PDF could not be read here, so the figure is owed a reading), taken as a basic density and read to an air-dry figure; every number an estimate, said so |
| cabbage tree palm | none | | | not wood (above): no row, so no stick and no log |

Green wood's water is the table's rule for all of them (0.60 of dry mass for a dense hardwood); WoodSolutions' unseasoned
densities agree with it if seasoning takes about a tenth of the volume.

### 3. The slope a plant keeps its vigour to

`PlantSpecies` gains one number, the slope up to which a plant's vigour does not fall; past it the factor falls in a straight line
to nothing at the plant's steepest face, as now. For the coast's twelve it is zero, which is today's factor to the bit (the
subtraction of zero is exact), so no coast plant changes. Blue gum takes 0.60 and the palm 0.70, from their records; the other
three new plants keep the fall from level ground, since their records fall with slope (scribbly gum, river oak) or dip on the
middle slopes (silvertop ash).

Old-man banksia's records lean to the steepest ground of all (2.3 to 4.8 times the background past 31°), but its row is the
coast's; a knee for it waits for a reading of both places' records (a new DEBTS row).

**What it leaves:** past 35 degrees the soil model holds no soil, so the walls past 35° stay bare although blue gum, silvertop ash
and old-man banksia are recorded there at 2.2 to 2.7 times the background. That is 2.6 per cent of the valley's land, owed to the
rock that holds roots (BF.4's stone and the geology; a new DEBTS row). This slice does not touch the soil model.

### 4. What the rule predicts

From the same restatement, on the whole valley and (in brackets) the 8 km valley.

**The canopy by slope:** 75.0 / 64.0 / 48.2 / 11.9 / 0.1 / 0 per cent on 0–15 / 15–25 / 25–31 / 31–35 / 35–45 / 45+ degrees,
68.7 per cent of the land in all (78.0 / 63.5 / 48.2 / 11.2 / 0.1 / 0, 68.4 per cent), where the world has 55.6 / 22.1 / 4.7 /
0 / 0 / 0 and 45.1 per cent. Blue gum takes 19.3 per cent of the land, silvertop ash 13.9, the palm 10.8, old-man banksia 7.1,
blackbutt 6.9, scribbly gum 5.8 and river oak 5.0. Without the knees the same rows give the slopes 46.8 and 26.7 per cent, and
the palm's ratio below falls from 1.00 to 0.85: the knee is what lets the palm stand where it is recorded.

**`species_check`, round its records** (at least 1 passes): blue gum 1.10 (1.08), silvertop ash 1.30 (1.08), river oak 1.43 (two
records in the 8 km box: no verdict), scribbly gum 1.78 (1.72), **the palm 1.00 (0.95)**, old-man banksia 1.03 (0.83, owed there
now at 0.77), blackbutt 0.82 (one record: no verdict), the grass tree 1.01 (four records), heath banksia 1.15 (1.23), kangaroo
grass 1.18. No row that passes today is predicted to fail. Bracken 0.87 (0.86), Lomandra 0.85 (0.78) and saw-sedge 0.78 (0.38)
stay where they are: they fail today and will after, and their rows are the coast's, so they move from this debt to a narrower one
("The understorey's rows are the coast's in the valley"). The palm is the row to watch: it is found in gullies, and the model's
wetness at a record's 100 m does not see a gully well.

**What the prediction cannot see:** the draw's rolls, the trunks' spacing and the frame. The build measures those.

### 5. The stand cell widened

**The layout.** A stand cell becomes two bytes: the high byte the plant's catalogue number (its place in `PlantSpecies.All` plus
one, the overstory's own number), the low byte its height in steps of 1.25 m (1 to 255, to 318.75 m); zero where no trunk stands.
- One number for a plant in every layer (overstory, understory, stand): one owner of the fact.
- The step is unchanged, so every height the old byte holds converts exactly and every check's step constant stays.
- Room for 255 plants and for any tree on Earth, and every reader is a shift and a mask.

**The worlds already made load, with the trees they were made with.** The old byte's plant numbers are already the catalogue's
(the five tall plants are its first five) and its heights are the same steps, so an old cell converts exactly: the plant is
`old >> 5`, the height `old & 0x1F`. A build of this slice reads a world's stand layer by the dtype its sidecar states (u8, the old
three and five bits; u16, the new two bytes) and converts an old one in memory, leaving the world's files as they were made (the
manifest's checksums still match). A valley made before this slice keeps the coast's trees, because a world is what it was made; a
valley made after it has its own. No world needs remaking; the templates are remade for the proofs.

**The versions:**
- **The world file** (`eg2.world`) 1 → 2. A build of this slice reads version 1 and writes version 2 at its first save; an older
  build refuses a version-2 world by name ("version 2; this build reads 1", `WorldSave`'s existing check) instead of misreading its
  trees. One way only, as the players' version-1 files were.
- **The stand layer's sidecar:** dtype u16 and a legend stating the layout and naming every plant by its number.
- **The tile format** (`TileCodec.Version`) 3 → 4: stand and far-stand tiles carry two bytes a post, the plant plane and then the
  height plane, deflated together; the far count stays one byte.
- **The protocol** 20 → 21 (decided: BF.4 stage three's second part takes the number after it): the stand and far-stand payloads
  change meaning, and a client and server of different versions already refuse each other.
- **Unchanged:** the region files (`.egr` v5), the player files (v7), the digest and the change record, which name a trunk by its
  row and column only; and the client's tile cache, which keys a tile by the checksum of its bytes (`DiskTileCache`), so a tile of
  the old format is never read as the new: it misses and is fetched again. The old far tiles stay on disk unread, as far tiles are
  never trimmed (a note, not a row).

**What the coast gains, and the one place it moves.** A world made after this slice stores a tree at its drawn height, so the
blackbutts the old byte clipped at 38.75 m stand at up to 40 m. `StandTrees` spaces trunks and `LayLoose` drops sticks by the
stored height, so the gate world's stand moves where the old byte held a blackbutt at its top step (at most 271 of its 992,066
trunks; 288 of the 8 km valley's 1,169,718) and in the spacing and sticks round those trees. Everything else about the coast is
unchanged by construction: its plant list is its twelve, their numbers and rows are the same, and their knees are zero. The proof
states every changed cell (below).

**Where it touches** (from the map of the stand byte's readers, 2026-09-24):
- *Engine:* `Stand.cs` (the layout, the legend, no cap of seven), `PlantSpecies.cs` (five rows, the knee, append-only),
  `Region.cs` (`Plants`), `WorldLayers.cs` (the stand as two bytes, the draws over the region's plants, `StandTrees`' widest crown
  over the region's tall plants), `TileCodec.cs` (version 4, the far squares by catalogue number), `WorldState.cs` (an old stand
  converted), `Wood.cs` (four rows), `Definition.cs` (no stick or log for a tall plant without wood: the palm), and the readers
  that cast a stand code to a byte (`StandPreparation`, `TrunksNear`, `Understorey`, `LyingProperties`, `LyingSiteReader`,
  `StandingThings`). Since BF.4 stage three's first part, `StandPreparation` also places the rocks (`PreparedStand.Rocks`,
  `StoneCrc`), and the rock rule reads a stand code only as "a trunk stands here or not" (a code of zero or not): that meaning is
  kept with two bytes, and a stage-one test pins it.
- *Server and protocol:* `WorldCreation` (write two bytes and the legend), `WorldPreparation` (read either), `WorldSave` (version
  2, reading 1), `GameServer`'s decode sites, `ProtocolInfo.Version`.
- *Client core and Unity:* `TileReceiver` (the stand and far-stand codes as two bytes), `FarForest`, `FarCanopy`, `StandForms`
  and `TreeGeometry` (five forms, keyed by catalogue number, the palm a new crown: a stem to 50 cm through and a whorl of fronds
  3 to 4.5 m long [P]), `ThingWords`, `StandViews`, `StandMeshes` (mesh groups and seeds by catalogue number, which for the
  coast's five are the numbers they have now, so their meshes and seeds do not change), `TrunkBodies`, `VerbController`,
  `Recorder.Changes`.
- *Verifiers:* `stand_check`, `crowns.py`, `tile_check`, `lay_loop.py` (the layout by dtype; the new plants' crowns and heights
  restated from `ECOSYSTEM.md`'s sources), `species_check` (below).
- *Tests:* every test that packs a stand code by hand or counts the tall plants (the map lists them), and `TestRasters` gains a
  two-byte helper.
- *Documents:* `ARCHITECTURE.md` §10 (the stand's format, the tiles, the protocol) in the same commit;
  `WORLD_GENERATION_DESIGN.md` (its "wider IDs are options, not adopted formats": adopted); `ECOSYSTEM.md` (the valley's table, as
  the coast's, and the crowns and sticks table); DEBTS.

## What it does not touch

The coast's twelve rows, every number; the soil model and its angle of repose; the wetness model; the understorey's rows; soil
fertility, which the model does not have (so it cannot keep blue gum off the plateau's poor sandstone: the plateau's soil is
deep in the model, and what keeps blue gum mostly to the slopes is its knee and its moisture); the stone; the wake scorer's rules.

## How it is proved

1. **Engine tests,** each seen red first: the region's draw never names a plant outside its list; a knee of zero gives today's
   factor exactly, and a knee holds full vigour to it; the five rows' numbers; the catalogue's numbers pinned by name; the
   two-byte layout's round trip; every one of the 256 old bytes converting exactly; an old-format world loading with its trees
   equal to the conversion; version-4 tiles round trip; far squares with more than seven tall plants.
2. **Sabotages,** restored byte for byte: the draw ignoring the region's list; the knee ignored; the conversion off by a step;
   the tile's planes swapped.
3. **The verifiers,** each an independent restatement printing both numbers: `species_check` restates the regions' lists with
   their sources, judges the five new plants round their records (and, in the whole valley, by their distance from the sea), and
   its owed table is re-read: the rows this slice pays taken out, the rest moved to their narrower debts. `stand_check`,
   `tile_check`, `crowns.py` and `lay_loop.py` read the new layout. A new `stand_check` row holds the valley's canopy by slope
   against the records' slope table: on 15 to 31 degrees at least half the flat's canopy share, with the records' ratios printed
   beside it.
4. **The coast:** the gate world made before and after. Every layer's raw bytes equal, except the stand and what reads it (the
   loose layer and the layers made from the loose layer); the stand's differences all at or next to a blackbutt the old byte held
   at its top step, and counted. The gate world's wake, drink place and loop unchanged. The sweep green on the slice's build
   (main's lane); `join_check --only N2` on a corpus walk.
5. **The valleys:** both remade; `species_check` as above; the wakes re-read (`vantages.py`'s valley rows follow the scorer);
   frames at 1440p and 1080p from the valley's vantages (a wall, a gully with palms, the river's oaks, the plateau), for William's
   eyes; the frame cost at those vantages under `vantages.py --hold`, with the trees in the held tiles counted.
6. **The cost:** the whole valley's making time and peak memory against WG.1b's 87.2 s; the stand layer's extra 64 MB; a stand
   tile's bytes on the wire round the gate world's wake, before and after.

## In two stages

- **Stage one, the cell widened.** The format alone: no new plant, no new rule. The proof is item 4 above plus every world
  loading. Nothing visible changes but the tallest blackbutts. It starts when the main session says BF.4 stage three's first
  part is on origin, and is built on it.
- **Stage two, the valley's plants.** The region's list, the five rows, the knee, the four `Wood` rows, the new forms and the
  palm's crown; the valleys remade; the checks; the frames.

## Owed after (the DEBTS rows it would open)

- The walls past 35 degrees hold no soil for a root (above).
- The coast's plants in the whole valley's estuary corner (coast banksia, swamp paperbark).
- Old-man banksia's lean to steep ground (its row is the coast's).
- The palm's fronds, as things.
- River oak's and scribbly gum's timber figures are estimates (joining the Wood rows' re-reading of 2026-09-22).
- The understorey's rows are the coast's in the valley (bracken, Lomandra, saw-sedge).
- Soil fertility, which the valley's forests follow and the model does not have.

## The questions put to the main session, and why each was recommended

All six were decided as recommended on 2026-09-25 (the status block above).

1. **The region's list of plants,** against giving each plant a mechanism instead (coast banksia's salt floor read from the salt
   wind alone, a brackish floor for swamp paperbark). Recommended: the list. It is what WG.2 proposed, it keeps the coast exact,
   and the mechanisms would change the gate world and still leave spinifex and swamp paperbark without a reason to stop inland.
2. **The knee,** zero for the coast. Recommended as written.
3. **The cell:** two bytes, the catalogue number and the height at 1.25 m; the world file to version 2, the tiles to 4, the
   protocol to the next number; old worlds read and converted, not remade. Recommended as written.
4. **The clip:** accept that the gate world's tallest blackbutts (at most 271 trees) and their neighbours move, proved cell by
   cell and by the sweep. Recommended: yes; the alternative is keeping a cap the stand's own comment says was never meant.
5. **Stages and lanes:** both stages built by the side worker once BF.4 stage three is on origin, main reviewing; or main takes
   the client's drawing (`StandForms`, `TreeGeometry`, `StandMeshes`: the palm is a new crown kind). Main's call.
6. **The palm's missing wood** in main's code: `Definition` makes no stick or log for a tall plant with no `Wood` row, and the
   palm is not felled or stripped in this slice. Recommended as written.

## For William

- **His eyes** on the valley's frames when stage two stands.

Bangalay's records (the two files above, the same public query as the other sixteen, 1 to 103 KB each) needed his yes when this
was drafted; he gave it the same morning, and ruling 49 made downloads the agents' own. They are fetched after the main
session's fourth sweep, since the sweep copy reads the same `Data/` and its `species_check` steps would judge a plant its code
does not owe; they decide whether bangalay grows in the valley.

## Stage one: what was built (2026-09-25, in the side copy on c301931)

- **`StandCodes`**, the one owner of the layout: `Pack` gives a `ushort`; `SpeciesOf`, `HeightOf` and `TallIndexOf` take one;
  `FromOneByte` converts the one-byte layout; `IsOneByte` and `CodeAt` read a world's stand layer in the two-byte layout,
  converting a one-byte layer as it reads and refusing a code neither layout carries. The legend names each tall plant by its
  catalogue number. `BuildTall`'s cap of seven is gone.
- **`PlantSpecies.NumberOf` and `ByNumber`**: the catalogue's numbers, which the overstory and understory already carried
  (`WorldLayers`' private `IndexOf` retired into them), the one owner for all three layers.
- **The world's layer**: `WorldLayers.Stand` is `ushort[]` and `WorldCreation` writes it u16.
- **The tiles**: `TileLayers.CodeBytes` (two for the stand and the far stand), `TileCodec.PackWideCodes` and `UnpackWideCodes` (two
  planes through one deflate), `EncodeCodes` reading the stand through `CodeAt`, `FarSquare` counting by the tall plants' places
  through `CodeAt`, `TileCodec.Version` 4.
- **The client**: `ReceivedTile.WideCodes`, `HasCodes` and `CodeAt`; `TileReceiver` unpacks by `CodeBytes`; the stand's readers
  (`StandPreparation`, `TrunksNear`, `Understorey`, `FarForest`, `FarCanopy`, `ClientRocks`, `LyingSiteReader`, and Unity's
  `StandViews`) read the two-byte grid. The client keeps its forms and meshes by the place in `StandCodes.Tall`, which for the
  coast's five is the place it was, so their meshes and seeds are unchanged.
- **The server's readers** of a world's stand (`StandingThings`, `StandingRocks`, `LyingSites`) read through `CodeAt`;
  `StandingRocks.TryDecide` takes the stand code as a `ushort`, read as before only as whether a trunk stands.
- **The versions**: `WorldSave.Version` 2, reading 1; `ProtocolInfo.Version` 21, its history's missing line for 20 added.
- **The verifiers**: `stand_check.stand_layout` (the layout by the layer's dtype), used by `crowns.py`; `tile_check`
  (`unpack_wide_codes`, `two_byte_stand`, the far squares in the two-byte layout); `lay_loop.py` (the height's mask by dtype).
- **The documents**: ARCHITECTURE §10's wire, tile, world file and layers rows, and its decision log; WORLD_GENERATION_DESIGN's
  catalogue row notes the adoption.

**Tests.** The suite is 879, green in the side copy, again after the rebase onto 091d029. Eight are new:
- `ThePlantsKeepTheirNumbers`;
- `EveryOneByteCodeConvertsToTheSameTree`, over all 256 codes against the one-byte legend as it printed;
- `AStandLayerIsReadInTheTwoByteLayoutWhicheverItHolds`;
- `ATreeAsTallAsItsPlantGrowsIsStoredAsTall`;
- `AFarSquareOfAOneByteStandIsTheSameAsOfItsTwoByteTwin`;
- `AWorldMadeBeforeSendsItsTreesAsItsTwinMadeAfterDoes`, where every tile a client holds of a one-byte world carries the same
  bytes as its two-byte twin's;
- `AVersionOneWorldIsReadAndSavedAsTwo`;
- `TheRuleReadsATwoByteStandCodeOnlyAsWhetherATrunkStands`, which pins the rock rule's reading, a code with a zero low byte
  included.

Beyond those, `TheCodesGoInAndComeOut` pins the legend's punctuation and the far square's tie. The protocol, tile and
world-file pins moved. Every test world that built a stand now builds it two bytes a cell; two keep the one-byte stand on
purpose.

**Sabotages.** Seven, each turning its tests red, every file restored byte for byte (checked by SHA-256):

| Sabotage | Tests that went red |
|---|---|
| The conversion a step off | 4, among them the 256 codes and the twin world's tiles |
| The planes packed low byte first | 3: the stand post for post, the far layers streamed and encoded |
| A far square's tie to the last plant | 1 |
| A version-1 world refused | 1 |
| The rock rule reading one byte of the code | 1 |
| The height clipped at 31 steps | 2 |
| A plant named by its tall place instead of its number | 7 |

**Caught on the way.** The first two-byte legend put a semicolon after its last plant ("5=SwampPaperbark; the overstory's
numbers"). `stand_check` reads a name up to the next space, so it took "SwampPaperbark;" for a plant it did not know: all
216,350 swamp paperbarks of a two-byte copy of the gate world failed its first row. The legend now closes its list with its
bracket, the reader strips a stray stop, and a test pins the punctuation. On that copy `stand_check` and `crowns.py` now print
what they print on the one-byte world, line for line.

## Stage one: the proofs

**The worlds.** The side copy's own host made the three worlds again (2026-09-25, into `Artefacts/worlds/wg2c-s1-gate`,
`-valley` and `-whole`), under a request for quiet from the sweep. They were held against the template worlds made at fa4f501:
no commit between fa4f501 and c301931 touched world creation, only run-time files, so the templates are the worlds this code
would have made before the change. `compare_stand.py` (the side worker's scratchpad) compares every layer's raw bytes, and the
stand cell by cell with the one-byte layer converted by its own statement of the rule. In every world the other 22 layers are
the same raw bytes, and the wakes are unchanged: the gate (-1392, 2804), the 8 km valley (436, -1040), the whole (10840, -9268).

| World | Stand cells that differ | The clip itself: a blackbutt at step 31 now at 32 (40 m) | Trunks lost, gained | Farthest from a clipped blackbutt | Loose cells that differ (farthest) | Trunks before, after |
|---|---|---|---|---|---|---|
| the gate | 10 of 4,004,001 | 7 of the 271 at the top step | 3, 0 | 5.7 m | 14 (8.9 m) | 992,066, 992,063 |
| the 8 km valley | 22 of 4,004,001 | 20 of 288 | 2, 0 | 5.7 m | 71 (8.9 m) | 1,169,718, 1,169,716 |
| the whole valley | 237 of 64,016,001 | 149 of 3,574 | 58, 30 | 12.0 m | 702 (16.0 m) | 18,968,097, 18,968,069 |

The rest of each difference is the spacing round a taller crown: `StandTrees` takes a candidate's chance and its crowding from the
stored height, so a blackbutt 1.25 m taller turns a neighbour or two away, and one turned away can let a later one stand. The loose
layer follows its trees' heights and crowns. Only a blackbutt drawn at 39.375 m or more was clipped, so most of the trees at the
old top step keep their height. The whole valley took 87.2 s to make, as it did after WG.1b.

**The checks.** The six the sweep runs on a made world (save, cover, stand, drainage, species and census) were run by the side
copy on all three worlds, under the same request for quiet: all 18 exit 0. `species_check` owes the rows it owed before: 9 on the
gate, 4 on the 8 km valley, 8 on the whole. `census_check` owes Carrington Falls on the whole valley, as before. `stand_check`
reads the two-byte stand by its dtype: 992,063, 1,169,716 and 18,968,069 trees, each where its canopy stands.

**Unity and the build.** The edit-mode suite in the side copy passed 12 of 12. The harness was built and installed in the side
copy (`wg2c-s1`, c301931 with this slice's changes).

**Over the wire** (`stream.py`: the dedicated host and the harness joining over a shaped socket, 100 ms round trip, 2 per cent
loss, then `tile_check` on that join's own tile cache):
- **The new gate world** (`Artefacts/streaming/20260925T010104762570Z`): interactive 1.17 s after connecting, 0 errors, 0
  corrections. `tile_check` ok, 4,055,591 cached posts agreeing with the world: 0 posts disagree of the 9 two-byte stand tiles,
  0 of the 64 far-stand and 64 far-count tiles, and the far counts add up to the stand's 992,063 trees.
- **A world made before**, a copy of the gate template, its stand one byte a cell and its world file version 1
  (`Artefacts/worlds/wg2c-s1-oldgate`, run `20260925T010140509062Z`): the new host continued it and sent its trees in the
  two-byte layout. Interactive 1.21 s after connecting, 0 errors, 0 corrections. `tile_check`, reading the old layer through its
  own statement of the conversion, ok on every row, the far counts adding up to its 992,066 trees. Its 9 stand tiles and 64
  far-stand tiles are the same bytes as the new world's; 4 of the 64 far-count tiles differ, those holding the 3 trunks the clip
  moved. A few seconds of the same host with a clean stop wrote its world file back as version 2 (protocol 21), its stand still
  the one-byte layer it was made with, the same SHA-256 its manifest names.
- **The cost on the wire**: the 9 stand tiles round the gate world's wake are 177,175 bytes as cached, where the sweep's join of
  2026-09-24 cached 110,022 at one byte a post; the whole region's far stand 21,174 bytes where it was 14,223. The far count and
  the loose layer are unchanged (25,301 and 119,421 bytes).

**The drink scenario** on the new gate world (`Artefacts/frames/drink-20260925T010318Z-side`, SOLO, every message serialised):
the founder at the wake, east -1392 north 2804, beside the same creek; drank at the fresh water and was refused the sea as salt;
6 frames, 0 errors. `thirst_check` GREEN, every row.

**The corpus** (`Artefacts/corpus/wg2c-s1-20260925T014907Z-side`), on stage one rebased onto the main session's 091d029
(a6a743d). The harness was built there, the edit-mode suite 12 of 12 again. The run made a fresh gate world, its wake at
(-1392, 2804), under a request for quiet from the sweep once the main session's runs were done:
- `join_check --only N1`: join-a interactive in 1.10 s (9 tiles, 291,550 bytes; at most 10 s), join-b in 3.31 s (329,852 bytes;
  at most 20 s).
- `join_check --only N2`: 12 rows, 0 failed. 0 corrections for every walker over ten minutes each, two over the wire and one in
  SOLO; every named segment reached (bank, shore, cliff, platform, wade); the largest displacement 0.00 m.

Stage one is closed on the side worker's part, reviewed by the main session before the push. The main session's sweep and its
re-bake of the heights follow.

## Stage two: what was built (2026-09-25, in the side copy, rebased onto 36c7acb as a39ca79)

- **A region's plants** (`Region.Plants`), in the catalogue's order whatever order a list is written in, each plant once:
  Bherwerre the coast's twelve (`Region.CoastPlants`, also the plants of a region that names none and of a world whose heights
  name no region this build knows, which is every test fixture's); both Kangaroo Valley regions fifteen. **Bangalay is in the
  valley's list**, decided by its records as this contract said they would decide it: fetched on 2026-09-25 (below), 36 of its 45
  records over the whole valley's box are usable, 17 of them within 2 km of the sea on the Shoalhaven's floodplain and 19 from 7
  to 22 km inland in the valley's tall open forest and rainforest margins, on slopes to 42 degrees, the country of BioNet's "Sydney
  Blue Gum x Bangalay" moist forest. Its row is the coast's. The 8 km box holds 3 of its records, too few to judge.
- **The draws take the list**: `PlantCommunity.Canopy`, `CanopyCover`, `Understory` and `TotalSuitability` take the region's
  plants and have no form without them; `AnimalCapacity.PerKm2` and `ForageScore` take them too, so an animal is fed only by
  what its region grows. `WorldLayers` reads the list once (`Region.PlantsOf`) and passes it to the community, to the animals'
  capacity, and to `StandTrees`' widest crown (now the widest a stored code of the region's tall plants can hold).
- **Six plants** at the catalogue's end, 13 to 18, with the values and sources above; `PlantSpecies.FullVigourSlope`, the knee,
  zero for the coast's twelve (its factor `(steepest - slope) / (steepest - knee)`, the old one to the bit when the knee is zero).
- **Wood**: rows for Sydney blue gum and silvertop ash (WoodSolutions' seasoned figures) and for river oak, scribbly gum and lilly
  pilly (estimates that say so; the Global Wood Density Database named for them was not read, its workbook having no reader in
  the tools' environment, a clause of the wood table's DEBTS row). The palm has none. `DefinitionCatalogue` makes no stick or log
  for a tall plant without wood (`StickOf` then gives the plain stick), and `Work.Judge` refuses to fell a palm whatever is in
  hand ("the cabbage tree palm makes no wood to fell"); its bark does not strip.
- **The drawing**: `TreeGeometry` gains a crown kind (clumps, or a palm's fronds) and six rows; `StandForms` six rows, their
  colours in sRGB as the table's are; `StandMeshes` grows a palm as its stem and a whorl of fan fronds set a golden angle apart,
  near and far, drawn on both sides; every other tree keeps its seed and its mesh. `FarCanopy` fails by name for a tree with no
  form, where it painted it blackbutt's green.
- **The checks**: `stand_check` knows the six trees' crowns and heights from ECOSYSTEM.md's tables, and holds three new rows (the
  valley's walls; the crowns against the pre-1750 map in all; the same group by group, owed in every region under a new DEBTS row);
  `cover_check` knows their forms; `species_check` asks that a world grow its region's plants and no others, restating both lists.
- **The documents**: ECOSYSTEM.md's section on the valley's plants; ARCHITECTURE's plant community and a decision row; DEBTS rows
  for the map's groups and the palm's fronds, and the valley's wood in the wood table's row.

**What the prediction said before any world was made** (`canopy_sim2.py`, the side worker's restatement of the rule, which
reproduces the world within 0.05, on the whole valley's template as remade at 36c7acb): the canopy on 72.4 % of the land, 69.0 and
52.8 % on 15 to 25 and 25 to 31 degrees against 78.4 % under 15; round their records Sydney blue gum 1.11, silvertop ash 1.31, river
oak 1.45, scribbly gum 1.79, old-man banksia 1.05, and three at the line: the palm 1.01, lilly pilly 0.99 and bangalay 0.99. Lilly
pilly takes an even share of every group of the pre-1750 map, the rainforest's included, because nothing in the model's site
tells the map's rainforest from its eucalypt forest but the slope, and the rainforest's steep ground has the thinnest soil.

**Tests.** 940 green after the rebase (the suite and main's since). Nine are new: a region's draw never names a plant outside its
list; the coast keeps its twelve in order and the valley has its fifteen; a list is taken in the catalogue's order, each plant
once; a world grows its region's plants and no others (the made coast grown with the valley's list stands none of the coast's own
trees, and two or more of the valley's); a knee of zero is the fall from level ground to the bit, restated; a knee holds full
vigour and falls straight past it; the blue gum holds the valley's moderate walls; a palm gives no stick or log and is not felled;
an animal is fed only by its region's plants. `P8_EverySpeciesWinsSomewhereOnRealGround` now asks it of every plant in each
region's list, and that every plant is some region's. The edit-mode suite in the side copy passed 12 of 12, its stand meshes
test growing every tree, the palm's included, near and far.

## Stage two: the proofs

**The worlds.** The side copy's own host made the three worlds again on a39ca79 (2026-09-25, `Artefacts/worlds/wg2c-s2-gate`,
`-valley` and `-whole`), under a request for quiet from the sweep, and they were held against the templates the main session made
at 36c7acb from the same bakes (`compare_same.py`, the side worker's scratchpad: every layer's raw bytes by SHA-256, and the
census line by line).

- **The coast is the same world.** All 24 of the gate's layers are the same raw bytes as the template's, the animals' habitat
  layers among them, and its census is the same line for line; only the plant layers' legends now name plants 13 to 18. Its wake,
  drink place and trees (991,965) are the template's.
- **The valleys.** 12 of each valley's 24 layers are the same raw bytes: the ground, the water, the soil, the stone and the
  distances made from them. The 12 that differ are the plants' and what is made from them: the overstory, the understory, the
  suitability, the stand, the loose layer, the cover, the topology's forest and heath, two animals' habitat, the fibre's and the
  firewood's distances and the wake's score. Both wakes are where they were: the 8 km valley's at (436, -1040), where a palm now
  stands over the Lomandra, and the whole valley's at (10840, -9268), under a lilly pilly where a bangalay stood. Trunks: 854,822
  in the 8 km valley (1,169,716 before) and 14,043,225 in the whole (18,968,069): bigger trees, fewer of them.
- **The cost.** The whole valley took 93.8 s to make on a quiet machine, against stage one's 87.2 s, and the host's working set
  peaked at 8.16 GB. A second making gave the same 24 layers and census byte for byte. The first run, 155.9 s, was taken while the
  main session built its harness: the stages this slice does not touch ran slow with the rest (the soil 26 s where the template's
  took 15, the wake's choice 34 s where it took 18), so it was made again rather than averaged.
- **The frame.** A held turn of 20 s at two of the 8 km valley's vantages (`vantages.py --hold 20`, the side copy's harness), on
  the new world and on the template made before it: at the wake a median frame of 5.4 ms (the 95th percentile 6.7, the worst
  16.8) against 5.1 (6.2, 14.5), with 118,305 trees in the held tiles against 154,866; at the escarpment 5.4 ms (6.8, 14.3)
  against 5.4 (6.5, 13.1), with 127,806 trees against 186,270 (`Artefacts/frames/wg2c-s2-hold-after-20260925T040814Z` and
  `-before-`). Fewer trees, the valley's being bigger, and about the same cost: a palm's fronds are more faces than a clump.

**The checks.** The six the sweep runs on a made world, on all three: save, cover, drainage and census exit 0 on every world, and
`stand_check` and `species_check` exit 0 once the rows the valleys fail were owed, each under the DEBTS row that records its cause.

| Row | The gate | The 8 km valley | The whole valley |
|---|---|---|---|
| The valley's walls: canopy under 15, on 15 to 25, on 25 to 31 degrees | a note (the row is the valley's) | 81.4, 68.6, 53.0 % (58.7, 21.3, 4.5 before) | 78.7, 69.0, 52.8 % |
| Crowns over the land the pre-1750 map classes, against its groups' own crown cover | 0.43 in 0.24 to 0.51 | 0.47 against 0.48 to 0.76 (0.36 before): owed | 0.48 against 0.50 to 0.78: owed |
| The same, group by group | owed | owed | owed |
| Stems a hectare of each plant's own canopy | every plant in the band | Sydney blue gum 40, under 50: owed | Sydney blue gum 40: owed |
| Grows its region's plants and no others | 12 of the coast's 12 | 15 of its 15 (before: 4 it does not carry) | 15 of its 15 |

The crowns fall short of the map for a reason older than this slice. The stand's rule stands trunks at random and keeps them a crown
apart, and big crowns fill their canopy thinly: in the 8 km valley the crowns cover 0.39 of Sydney blue gum's own canopy and 0.95 of
old-man banksia's, and the gate world's blackbutt has always been at 0.33. The coast's small trees kept its whole at 0.74 of the
canopied ground; the valley's big eucalypts bring it to 0.58. The canopy itself stands on 72 % of the valley's land, where the map's
groups would have it; the crowns drawn on it are what is thin ("Big crowns cover less of their own canopy", a slice of its own,
since it moves the coast's stand).

Round their records (`species_check`; the whole valley's box, then the 8 km box where it holds five records or more):

| Plant | Whole valley | 8 km valley |
|---|---|---|
| Sydney blue gum | 1.11 (134 records) | 1.09 (12) |
| silvertop ash | 1.34 (269) | 1.07 (23) |
| river oak | 1.39 (105) | 2 records |
| scribbly gum | 1.79 (117) | 1.78 (10) |
| the cabbage tree palm | 1.02 (292) | 0.96 (15): owed |
| lilly pilly | 0.99 (508): owed | 1.04 (34) |
| bangalay | 0.94 (36), and grown a median 15.6 km from the sea against its records' 2.4: owed | 3 records |
| old-man banksia | 1.07: its owed row paid | 0.89: owed |
| heath banksia, the grass tree, kangaroo grass | 1.20, 1.04, 1.17 | 1.37, 4 records, none |
| blackbutt | 0.79, and 16.0 km from the sea against 1.5: owed | 1 record |
| bracken, Lomandra, saw-sedge | 0.85, 0.84, 0.79: owed | 0.88, 0.79, 0.42: owed |
| coast banksia, swamp paperbark | grown nowhere, their 17 and 9 records on the floodplain: owed | none |

The rows owed are 9 on the gate (M1.2b's, unchanged), 5 in the 8 km valley and 12 in the whole, under four DEBTS rows: "The valley
grows some plants by the coast's rows", "The coast's plants of the whole valley's estuary corner", "The palm crowds the wettest
ground" and, for lilly pilly, "The canopy does not follow the pre-1750 map's groups". "The valley grows the coast's plant table"
moved to Paid. The palm's row names both of the grove's causes, and the slice that would pay it: "the palm as the eucalypts'
understorey", a second tall layer standing the palm under the canopy trees as the moist forest carries it (the main session's
review, 2026-09-25, which agreed the big crowns' row too as a slice of its own, since it moves the coast's stand).

**Two things the frames will show** (the numbers read off the worlds): the palm is the commonest trunk at almost every vantage, 1,080
of them within 150 m of the whole valley's lake head, because its crown is a quarter of its height and the stand's rule stands
about ten times the stems on a palm's canopy that it does on a blue gum's; and 62 % of the 8 km valley's palms stand on the plateau's
upland swamps at 600 to 700 m, where a third of its records lie. A palm counts as firewood to the wake's scorer, as any canopy does;
the scorer's rules were left alone, and a palm never stands far from wood.

**Over the wire** (`stream.py`, the side copy's harness joining the side copy's host over a shaped socket, then `tile_check` on
that join's own tile cache): the new 8 km valley interactive 2.34 s after connecting (589,038 bytes by then), 0 errors, 0
corrections, and `tile_check` ok on 4,055,591 cached posts, its far counts adding up to the stand's 854,822 trees
(`Artefacts/streaming/20260925T035749912030Z`); the new gate 1.17 s, ok on the same posts (`20260925T035825801957Z`).

**Sabotages.** Seven, each turning its tests red, every file restored byte for byte (checked by SHA-256):

| Sabotage | Tests that went red |
|---|---|
| The draw taking every plant, not the region's | 4, among them the region's draw and the world grown with the valley's list |
| The cover taking every plant | 2 |
| The knee ignored | 1 |
| A region's list taken in the order written | 1 |
| The palm felled | 1 |
| A log for the palm | 3 |
| The animals fed by every plant | 2, once the animals' test held the insectivore's feed (a browser's on the dune is its shrubs' either way, and the first version compared only that) |

**Unity and the build.** The edit-mode suite 12 of 12 in the side copy; the harness built and installed there (`wg2c-s2`, a39ca79 with
the checks' uncommitted rows, which are not in the player).

**The downloads** (CANON ruling 49), each with its source, its licence and a tool that fetches it again:
- **The Atlas of Living Australia's records** (biocache, each record under its resource's licence; `Tools/data/fetch_ala.py`, whose
  valley list now names both plants), installed under `Data/cache/ala/` with this slice:

  | File | Records | Bytes | SHA-256 |
  |---|---|---|---|
  | `kangaroo-valley/Bangalay.json` | 3 | 1,252 | ca02f961b8312e2980195327b1dc4c59a57d2970ce59f2cd9c9b779eb0fc7197 |
  | `kangaroo-valley/LillyPilly.json` | 40 | 8,944 | dbbdf2a3390dc7da90a2367c202317144ae724760042955b3d9eef170001ac34 |
  | `kangaroo-valley-whole/Bangalay.json` | 45 | 9,929 | 87087e52bf155ae0c8c23f0b3b20b4be323d902b013b2af9beca31cece9a25c9 |
  | `kangaroo-valley-whole/LillyPilly.json` | 572 | 119,033 | 77765f2780e0865e9638e04b3237d45be7e6b4a5154d00a0b9300956b965b880 |

- **The twelve rainforest candidates' records** over the whole valley's box, fetched by the same query into the side worker's
  scratchpad to choose the rainforest tree by, and kept there: brown beech 192 records (40,766 bytes, 12af22cf...), brush bloodwood
  64 (14,044, d3a2f7ff...), coachwood 366 (76,791, 4a788719...), giant stinging tree 213 (44,969, d49205b5...), Illawarra flame tree
  93 (19,900, 199668aa...), lilly pilly 572 (119,021, 7807b855...), native tamarind 85 (18,474, 3debf720...), Port Jackson fig 51
  (11,562, 7fc07936...), red cedar 214 (45,190, 42004dc2...), sandpaper fig 269 (56,919, ffe98b77...), sassafras 543 (113,336,
  90931f7c...), small-leaved fig 52 (11,579, 7ba47a6a...).
- **NVIS's fact sheets for the Major Vegetation Groups** (DCCEEW, from https://www.dcceew.gov.au/sites/default/files/documents/,
  each "licensed by Commonwealth of Australia under a Creative Commons Attribution 4.0 International licence"; fetched again by
  those addresses), read for the crown cover each group's sheet states and kept in the scratchpad: the series'
  introduction (1,147,955 bytes, 0e7c3ec0...) and MVG 1 (2,355,249, b3954d7b...), 2 (2,760,387, d5a6828d...), 3 (1,669,619,
  af716a4d...), 5 (1,034,368, 827e6ba4...), 8 (1,137,156, e94eda69...), 9 (2,435,680, afe3df9a...), 10 (1,398,817, 40598e3e...),
  16 (1,842,213, 786fdc11...), 18 (3,830,416, b5d5c859...) and 23 (2,087,597, 195b381a...).

The Global Wood Density Database was not fetched: its workbook needs a reader the tools' environment does not have, and three of
the valley's wood rows stay estimates that say so (the wood table's DEBTS row).

**The frames, for William's eyes** (the side copy's harness, windowless and muted, at 1440p and 1080p): the 8 km valley's five
vantages (`Artefacts/frames/wg2c-s2-valley-20260925T040031Z`: the wake, the river, the escarpment, the falls and the reservoir),
and six of the whole valley's, each with four frames from 150 m up (`Artefacts/frames/wg2c-s2-whole-20260925T040031Z`: the
escarpment, Fitzroy and Belmore Falls, the village, Cambewarra and the lake's head). What to look at: the palms, a grove of
them in the gully below the escarpment and a crowd of them at the lake's head; the walls below the cliffs, which carry trees now;
the far palms, small pale starbursts among the eucalypts' clumps; and every colour paler than its table, as the stand's colours
all are until M1.6h makes them linear.
