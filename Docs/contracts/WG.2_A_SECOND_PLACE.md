# Contract WG.2 — A second, steeper place

**Status:** drafted 2026-09-22 on CANON ruling 42 ("a second steeper place"); nothing fetched. Waits on William's
approval of the exact files in the manifest below. Owner: Claude. William's lane: the approval, the station's table
pasted as Point Perpendicular's was, and his eyes on the first frames.

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

## The acquisition manifest (nothing fetched until approved)

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
