# Contract WG.2 — A second, steeper place

**Status:** drafted 2026-09-22 on CANON ruling 42 ("a second steeper place"); the place chosen the same day by ruling 43,
"go with fitzroy falls and kangaroo valley"; nothing fetched. Waits on William's approval of the exact files in the
manifest below. Owner: Claude. William's lane: the approval, the first act's re-cut, and his eyes on the first frames.

## What the place must give

The Founder's Path's first two acts, unchanged: a beach to wake on with surf in the ear, a creek within a walk, cobbles
of a stone that knaps, driftwood and dead wood, fibre plants at the dune's foot, timber and bark, a late-winter night
cold enough to kill a bare founder, rain. And now, by ruling 42, steep country: valleys, ridges, cliffs, rock that
stands out of the ground, slopes a founder chooses a way down, inside an 8 km box. Ruling 8's frame stays: a real place,
pre-human, named by its landforms; ruling 21: checked against published sources by the agent.

## The candidates, measured

Relief at the 900 m scale of ETOPO 2022 (`Data/global/etopo`, read by the atlas's own reader): the 9 × 9 cells round
the centre are the 8 km box; slopes at that scale are far gentler than the ground's, and rank the places all the same.

| Place | Land cells of 81 | Land height, m | Mean slope at 900 m | Steepest | Station with a Bureau table |
|---|---|---|---|---|---|
| Bherwerre (the gate world, a check) | 64 | 1–109 | 1.6° | 4.6° | Point Perpendicular 068034 |
| Royal National Park, Garie to Otford (−34.170, 151.060) | 60 | 3–212 | 3.1° | 13.1° | Lucas Heights 066078 (1958–, 140 m, 12 km inland) |
| Nadgee, the Nadgee River mouth (−37.420, 149.960) | 43 | 0–119 | 1.7° | 5.2° | Gabo Island 084016 (1859–) |
| Beowa, Bittangabee to Green Cape (−37.220, 150.020) | 48 | 0–120 | 2.1° | 6.5° | Green Cape |
| **Wilsons Promontory, Tidal River to Oberon Bay (−39.040, 146.330)** | 58 | 5–417 | **6.1°** | **20.2°** | Wilsons Promontory Lighthouse 085096 (1872–, 95 m) |
| Tasman Peninsula, Fortescue Bay (−43.130, 147.970) | 57 | 2–439 | 5.6° | 17.2° | Tasman Island (1906–, unmanned since 1977) |
| Cape Otway, the cape and the Aire (−38.850, 143.520) | 42 | 8–155 | 1.9° | 5.6° | Cape Otway Lighthouse 090015 |

Nadgee, Beowa and the cape at Otway are as gentle as Bherwerre at this scale and are set aside. Three are steep.

## The recommendation: Wilsons Promontory

The box centred at −39.040, 146.330: Norman Bay and Tidal River at its west edge, Oberon Bay and Little Oberon at its
south, Mount Oberon (558 m) and Mount Bishop (319 m) inside it, sea level to over 400 m in four kilometres by the
coarse model and higher by the mountain's own figure. Why it, over the other two:

- **It is the steepest coast with beaches and creeks in south-eastern Australia**, granite mountains falling straight
  to sand: what "steeper" asks for, and what ruling 41's world section asks for — cliffs, tors, boulders, gullies,
  exposed stone as the place's own character.
- **A lighthouse's own record**, as Point Perpendicular's is: the Bureau's all-years table for 085096, the same kind of
  source the climate model was built on, on the same coast's weather (Bass Strait's late-winter nights are colder than
  Jervis Bay's).
- **A national park with the pre-human framing intact**: one campground at Tidal River and a road, which the bake does
  not read; no town, no farm inside the box.
- **Most of the ecology carries over**: coast banksia, old-man banksia, swamp paperbark, Lomandra, bracken, saw-sedge,
  kangaroo grass, spinifex and the grass tree all grow there; the eastern grey kangaroo, the pied oystercatcher and the
  superb fairy-wren all live there. Blackbutt and bangalay do not reach it: two tall eucalypts of the Prom (messmate
  stringybark and coast manna gum, by the published flora) replace them in the plant table, with their own wood rows.

**Its honest weakness: the stone.** The Prom is granite, which does not knap (`StoneType.Granite`'s knappability is
under the floor); what knaps there is quartz, in veins through the granite and as pebbles on the beaches and in the
creeks, which the table holds and which the coast's own people knapped. The first stone's beat becomes what it is in
that country: the granite cobble crushes and says so, the quartz pebble flakes coarsely. The stone layer's rule for the
region is written to that (quartz on beaches, creek beds and platforms; granite elsewhere), and the census checks it
against the geology in print (ruling 21).

**The alternative, if the NSW tables matter more than the height:** the Royal National Park's southern coast, Garie to
Otford. Half the relief (a sandstone escarpment of two to three hundred metres over the beaches, cliffs of a hundred),
the whole plant and animal table as it stands, a stone story like Bherwerre's (quartzite and silcrete pebbles in
sandstone country), and a station twelve kilometres inland. The Tasman Peninsula is as steep as the Prom and its
hornfels knaps well, but its plants and animals are Tasmania's and the port is larger; it is the third.

## Asked the same day: must it be coastal, and could it have a tall waterfall?

Nothing in the constitution needs a coast. The path's first act does, as William approved it (2026-09-01): the wake on
sand with surf in the ear, the sea that will not drink as the first lesson, cobbles at the tideline, driftwood, fibre at
the dune's foot. An inland place re-cuts that beat, and the path is his to re-cut: a river below a fall gives water at
once, so the first "no" is lost or becomes another; river cobbles are the first stone (better tool stone than most
beaches); driftwood lies along the river and fibre on its banks; the surf is the fall's roar; the shorebird gives way
to a bird of the river or the forest. Fire, the night, the camp and the rain are the same anywhere. The bake takes an
inland box (`bake_region.py --coast` off), and the layers cope with no sea and no shore.

What a fall is in the game today, honestly: at 30 m a hundred-metre fall is a cliff with a creek running down it; the
water is drawn as a steep sheet with no plunge pool, no spray and no sound. Giving a fall its presence (found in the
drainage as a channel's drop over a cell, a pool, mist, its roar in the ear) is a slice of its own, beside BF.4.

Inland waterfall country, measured as the coasts were (ETOPO at 928 m; the 9 × 9 cells round the centre):

| Place | Height range in the box | Mean slope at 900 m | Steepest | Station | The fall |
|---|---|---|---|---|---|
| Grose Valley, Blue Mountains (−33.640, 150.330) | 353–1035 m | 9.1° | 24.8° | Katoomba 063039 (1017 m, since the 1880s) | Govetts Leap about 180 m; Bridal Veil |
| Jamison Valley, Blue Mountains (−33.730, 150.370) | 244–960 m | 7.0° | 26.8° | Katoomba | Wentworth Falls 187 m in two tiers |
| Fitzroy Falls and the Kangaroo Valley (−34.660, 150.500) | 91–724 m | 6.5° | 23.2° | Moss Vale 068045 (675 m, since 1898) | Fitzroy 81 m; Belmore about 100 m |
| Wollomombi and Chandler, New England (−30.520, 152.060) | 643–1035 m | 2.7° | 22.2° | Armidale | Wollomombi about 220 m, in a gorge through a plateau |
| Apsley Falls and gorge (−31.050, 151.780) | 738–1226 m | 4.3° | 14.8° | Walcha | Apsley, two tiers |

The Blue Mountains' valleys and the Kangaroo Valley are steeper on average than the Promontory; the New England gorges
are a plateau with a gorge cut through it, gentle on average and sheer at the gorge. The highlands are far colder in
late winter than Jervis Bay (Katoomba's August nights near 2 °C, Moss Vale's near 3, Armidale's near 0, against the
lighthouse's 8): a bare first night is harsher there, which is the path's enemy and not a fault, and a wake on a valley
floor (the Grose at 350 m, the Kangaroo River at 100 m; `Weather.At` takes the altitude) keeps it survivable. If he
chooses a fall: the Grose Valley for the tallest falls in the deepest valley, a long station record, a national park,
sandstone country with river gravels and a basalt cap (Mount Banks) for stone, and the Blue Gum Forest; the Kangaroo
Valley if the cold is the worry, milder and nearer Jervis Bay in its plants. Either box is 36 tiles at zoom 14 and 25
at zoom 11, the same manifest's shape with its own centre and station. The decision, coastal or inland, is his.

## The place: Fitzroy Falls and the Kangaroo Valley (ruling 43)

The box: centre −34.660, 150.500, 8 km; west 150.4453, south −34.7050, east 150.5547, north −34.6150 with the bake's
kilometre of margin. Inside it: the Kangaroo River and its valley floor at about a hundred metres, the Morton plateau's
sandstone escarpment rising to over 700 m along the north and west, Yarrunga Creek falling 81 m at Fitzroy Falls and
running down its gorge to the river, Belmore Falls' creek to the west, rainforest gullies under the cliffs, forest and
farmland (the farms regrow as forest: the plant layer is drawn from suitability, never from land use) on the floor,
heath and woodland on the plateau. Region id `kangaroo-valley`, "Kangaroo Valley, Fitzroy Falls"; the wake day and hour
as Bherwerre's (late winter); no coast (`bake_region.py --coast` off).

**Pre-human corrections the bake must make here.** Lake Yarrunga, the Tallowa Dam's reservoir, backs up the Kangaroo
River and Yarrunga Creek from the south-east, and its upper arms may reach the box's southern edge; OpenStreetMap
tags it `water=reservoir`. The constitution has no dams: `bake_water.py` excludes every way tagged `water=reservoir`
or `landuse=reservoir` and lists what it excluded in the sidecar, and the census names them. The village, the roads
and the visitor centre at the falls are not read by anything and leave no mark.

**The station.** The valley floor stands at about 100 m, twenty kilometres west of Nowra RAN Air Station (068072, 109 m),
whose all-years table the repository already holds (`Data/stations/`, M1.8c): Nowra is the region's station
(`Climate`'s second `Station`, its means, dew points and winds Nowra's own; the fronts' widening, the rain, the cloud and
the cold snap's depth still the lighthouse's fits: DEBTS), and the valley's colder nights under the escarpment are the
inland gradient's debt as they are for Bherwerre. Moss Vale's table (068045, 675 m, since 1898) is the plateau's
reference and is pasted when William chooses to; nothing waits on it.

**What the valley offers at a wake (ruling 44: no first act is written; the scorer finds the wake and the country offers
what it offers).** The wake scorer's criteria are the same as Bherwerre's — fresh water within 500 m, stone that knaps
within a kilometre, fibre and firewood within 500 m, shelter rock within two, standable ground — and here they find a
river bar or a creek bank under the escarpment: the river and its creeks to drink; river cobbles (basalt down from the
Robertson caps for a hammer, quartzite and whatever chert the gravels hold for an edge, sandstone that crumbles);
driftwood along the bar and dead wood under the forest; Lomandra and saw-sedge on the banks; the falls' roar where the
surf was. The shorebird gives way to a bird of the river or the forest (the superb lyrebird is the valley's own); the
kangaroo and the fairy-wren stay. Nothing tells the founder what to do with any of it.

**The plant table, to be checked against the Atlas of Living Australia's records for the box after the fetch (ruling
21):** blackbutt, old-man banksia, heath banksia, the grass tree, bracken, Lomandra, saw-sedge and kangaroo grass stay;
coast banksia, swamp paperbark and spinifex are the coast's and go; Sydney blue gum (*Eucalyptus saligna*), river oak
(*Casuarina cunninghamiana*) along the river, the cabbage tree palm (*Livistona australis*) of the valley's rainforest
gullies, and silvertop ash or scribbly gum on the plateau's sandstone come in, each with its suitability, height, bark,
sticks and a `Wood` row with a source. The stone layer's rule is written from the Nowra 1:100 000 geology sheet's
descriptions (Permian Nowra Sandstone and Berry Siltstone on the floor, Hawkesbury Sandstone cliffs, basalt and
quartzite in the river gravels) and checked by the census.

**Fitzroy Falls in the game.** At the bake's 30 m the fall is a cliff with Yarrunga Creek running down it, drawn as a
steep sheet; a fall's presence (its pool, its mist, its roar) is the slice `WG.3_A_FALL` beside BF.4, after the world
stands.

## The acquisition manifest (nothing fetched until approved)

**For the Kangaroo Valley** (the Promontory's manifest below it stands as the record of what was measured, not fetched):

1. **AWS Terrain Tiles, zoom 14, the box plus a kilometre's margin**: `https://s3.amazonaws.com/elevation-tiles-prod/terrarium/14/{x}/{y}.png`
   for x 15038–15043 and y 9872–9877 — **36 PNG tiles**, about 50 KB each, **about 1.8 MB**; 7.9 m a pixel here; SRTM 30 m
   under them unless Geoscience Australia's 5 m tiles cover the valley, which the sidecar will say. Fetched by
   `Tools/data/fetch_tiles.py --zoom 14 --centre-lat -34.660 --centre-lon 150.500`. Licence: AWS Open Data (the Terrarium
   set), attributed as `terrarium.ATTRIBUTION` states.
2. **AWS Terrain Tiles, zoom 11, the 64 km surround for the far skirt**: x 1878–1882, y 1232–1236 — **25 tiles, about
   1.0 MB**, the same source and licence.
3. **OpenStreetMap water round the box**: one Overpass query, `bake_water.py`'s own with the new centre — every way tagged
   `natural=water` or `natural=wetland` inside the padded box, reservoirs excluded as above — **one JSON response, well under
   a megabyte**. Licence: ODbL 1.0, © OpenStreetMap contributors, as the notices record.
4. **The plant records for the verifier**: the Atlas of Living Australia's occurrence records inside the box for each species
   of the region's plant table, by `Tools/data/fetch_ala.py` given the box — **about a dozen small JSON files, a few
   megabytes at most**; an input to `species_check.py` only, never to the world. Licence: the Atlas's records are CC BY
   (each record carries its own licence), as the notices record for Bherwerre's.
5. **No station table to fetch**: Nowra's is already held.

Total: **about 60 to 75 small files, under 10 MB.** Nothing else.

**The Promontory's manifest, as measured (not chosen, not fetched):**

The bake's own tools take any centre, so the second place is fetched as Bherwerre was, not as the six global datasets
were. Every file, its source, its size and its licence:

1. **AWS Terrain Tiles, zoom 14, the box plus a kilometre's margin**: `https://s3.amazonaws.com/elevation-tiles-prod/terrarium/14/{x}/{y}.png`
   for x 14849–14854 and y 10122–10127 — **36 PNG tiles**, about 50 KB each, **about 1.8 MB**; 7.4 m a pixel at this
   latitude; the elevation under them is SRTM 30 m here (no Geoscience Australia 5 m at the Prom, so the relief is real
   to 30 m and the ground below it is BF.4's). Fetched by `Tools/data/fetch_tiles.py --zoom 14 --centre-lat -39.040
   --centre-lon 146.330` into `Data/cache/terrarium/14/`. Licence: the tiles are AWS Open Data (Mapzen's Terrarium
   set), derived from NASA SRTM (public domain) and the sources `terrarium.ATTRIBUTION` names, attributed as it states.
2. **AWS Terrain Tiles, zoom 11, the 64 km surround for the far skirt**: x 1854–1858, y 1263–1267 — **25 tiles, about
   1.0 MB**, the same source and licence.
3. **OpenStreetMap water round the box**: one Overpass query, `bake_water.py`'s own with the new centre — every way
   tagged `natural=water` or `natural=wetland` inside the box padded by a kilometre — **one JSON response, well under a
   megabyte**, cached under `Data/cache/`. Licence: ODbL 1.0, © OpenStreetMap contributors, as `THIRD_PARTY_NOTICES.md`
   already records for Bherwerre's.
4. **The lighthouse's table**: the Bureau's climate statistics for Wilsons Promontory Lighthouse, 085096, all years,
   pasted by William into `Data/stations/` as Point Perpendicular's and Nowra's were (the Bureau's site refuses a
   fetch), under the Bureau's terms already in the notices. Nothing to download.

Total: **61 small files, about 3 MB.** Nothing else. No LiDAR, no atlas change, no new global dataset.

## What follows the fetch (the slice itself)

For the Kangaroo Valley, in order: `Region.KangarooValley` in the engine's region table and its station bound to Nowra's
numbers in `Climate` (a second `Station` constant from the held table, its fronts fitted by the almanac tool's `+fit`);
`bake_region.py --coast` off with the reservoir exclusion in `bake_water.py`; the world's creation, the layers and the
verifiers (`drainage_check`, `census_check`, `species_check` on the fetched records, `cover_check`, `tile_check`,
`world_inputs_check`) with the wake scored; the plant table's changes with their `Wood` rows; the stone rule; the far
skirt; the first act's re-cut written into `FOUNDERS_PATH.md` on William's approval; frames from the wake and the
vantages; the built player installed for him when his game is closed. Bherwerre stays the first region and the gate
world until the valley is proved. The Promontory's steps below are the record of the alternative.

**The Promontory's steps, as planned:**

1. `Region.WilsonsPromontory` in the engine's region table: id `wilsons-prom`, "Wilsons Promontory, Tidal River",
   centre −39.040, 146.330, 8 km, the wake day and hour as Bherwerre's (late winter), the station 085096.
2. The bake (`bake_region.py`, `bake_water.py`), the world's creation (`create.py`), the layers and the verifiers
   (`drainage_check`, `census_check`, `species_check` against the Atlas of Living Australia's records for the Prom,
   `cover_check`, `tile_check`), the wake scored as the scorer scores it — a steeper place will have the scorer refusing
   more of it (over ten degrees), which is right.
3. The plant table: messmate stringybark (*Eucalyptus obliqua*) and coast manna gum (*E. viminalis* subsp. *pryoriana*)
   with suitability, height, bark and sticks as the others, and their `Wood` rows with sources; the stone rule for
   granite country; the far-skirt bake at zoom 11.
4. The climate: `Climate` from the pasted table, the warming's share, the almanac tool's `+fit`, `weather_check`.
5. Frames from the four vantages and the wake, at 1440p and 1080p, for his eyes; the built player installed for him
   when his game is closed.

Bherwerre stays: the first region, the gate world, every scenario's world until the second is proved.

## Exit

The files approved and fetched with their sizes as fetched; the world created and every verifier green with its
numbers; the frames; or "What changed on the way".

### Exit record (2026-09-22, on William's "you have my yes for the files")

**Fetched, as fetched.** 36 Terrarium tiles at zoom 14 (x 15038-15043, y 9872-9877; 3.36 MB, twice the estimate: the
plateau's relief compresses worse than a peninsula's) and 25 at zoom 11 (x 1878-1882, y 1232-1236; 15 new at 0.69 MB, ten
already held from Bherwerre's surround, which overlaps); one Overpass response (36.6 KB; the first two asks were 504s
and the third answered); the records of 16 species (51.4 KB) under `Data/cache/ala/kangaroo-valley/`. Nothing else.
`THIRD_PARTY_NOTICES.md` names each. Data/ is never committed: the bakes and caches live on this machine.

**Baked.** `heights` 2001 x 2001 at 4 m, 80.7 to 769.5 m; the tiles held SRTM voids read as -4,121 m, and
`--despike-m 25` rewrote 457 cells (DEBTS, "The valley's voids are invented ground"). `surround` 1001 x 1001 at 64 m.
`water_bodies`: one body (an unnamed pond of 60 cells); 24 ways left out as humanity's, Fitzroy Falls Lake and 23 farm
ponds, listed in the sidecar under `excluded`. The station: `Climate`'s `Nowra` from the held table, `HasRecordFor` true.

**What changed on the way.** (1) The first encode of the world's tiles threw: tile format 2 held every height as a
signed 16-bit centimetre, 327 m, and the plateau stands at 700. Tile format 3 begins each metres row with a 32-bit first
post (ARCHITECTURE section 10, decision log). (2) A saved world opened by a tool that named no region was refused
"opened as 'bherwerre'": a world now owns which piece of the Earth it is (`WorldSave.RegionOf`, tested; the host and the
game read it; sabotaged to answer Bherwerre for every world, the test went red, restored byte for byte). (3) The
verifiers that judged a world by Bherwerre's landmarks read the region off the world's file and keep their tables per
region; a row that does not apply inland prints a note, and `census_check` says it holds no landmarks for the valley and
exits 2 (DEBTS, "The valley has no census"). (4) The falls vantage first stood the founder on the lip, a 35 degree
face, and the frames show them sliding; it stands 90 m up the creek now.

**The world.** `create.py Artefacts/worlds/valley --region kangaroo-valley` in 9.3 s, 24 layers; the scorer's wake at
east 436 north -1040, 431 m on the plateau in forest (WG.2 proposed the river's bar; his word, DEBTS' first table).

**The verifiers, as run.** On the valley: `drainage_check` ok (the largest creek 1,727,266 cells by the engine against
1,726,806 by its own D8, the second 1,647,645 against 1,647,198; the channel law held on 21,651 of 21,689 cells; no water
off the channels on 3,897,761 cells; no published lake in the region's table, a note); `cover_check` ok (0 of 4,004,001
posts off the cascade; 8 covers, grass 31.1 %); `species_check` ok with four rows owed (bracken 0.89, Lomandra 0.78,
saw-sedge 0.43, old-man banksia 0.77 round their records; heath banksia 1.17 ok; every distance-from-the-sea row a note,
there being no sea; DEBTS, "The valley grows the coast's plant table"); `region_stats --world` ok (the land the bake's to
0.000 m; no sea to floor, a note); `census_check` exit 2 by design. On the gate world, unchanged: all four green, the
nine owed rows as before. The suite 801 green; the Unity-shaped compile clean; the harness built by `install.py`.

**The shell (ruling 44).** A new world is made in a chosen place: Bherwerre and the valley offered, "Wilsons Promontory",
"Blue Mountains, the Grose Valley" and "Alice Springs, the MacDonnell Ranges" greyed "(not yet)". The loading scenario
through the scripted shell (`Tools/world/loading.py`, `loading_check`): 33 rows PASS, a new world in Bherwerre, the
continue in the world's own region, the missing-data failure back to the menu.

**Frames.** `Artefacts/frames/valley-20260922`: wake, river, escarpment, falls (the lip, the slide) and reservoir, 14
frames each at 1440p and 1080p with the 150 m lookout to the four points; `valley-20260922-falls`: the creek above the
lip. Seen: the escarpment's face above the wake and the gorge from its edge, real relief at last; the coast's plants on
inland ground; the reservoir's flat as a lake; the river a cell wide (DEBTS); beyond the streamed tiles the far ring
bare tan, stark from height (the old debt). His eyes owed.
