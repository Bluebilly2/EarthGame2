# The world makes itself

**Status:** design pillar, binding. Owner direction, 2026-08-25:
*"i want to design a real ecosystem and let it form naturally."*
Clarified by the owner on 2026-09-10 (CANON ruling 22): *"the idea that things are there because they should be
is a guide for how it should be designed. dont need to literally simulate them coming into existence."*

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

Each link is a consequence of the one above it.

| | Made by | Produces |
|---|---|---|
| **1. Rock** | real elevation, real geology | relief, and how hard the ground is to cut |
| **2. Landform** | water and gravity, over time | valleys, ridges, drainage, sediment |
| **3. Water** | where the landform sends it | creeks, rivers, wetlands, ponds |
| **4. Soil** | weathering + what the water deposited | depth, texture, drainage, fertility |
| **5. Plants** | soil + climate + light, and competition | communities, not a biome stamp |
| **6. Animals** | what the plants support | populations that eat, move and die |

That is a rule for how the world is designed, not a history to be simulated (ruling 22). Nobody sees the world
before its first load, so its first state is generated in one go from the country's own facts — a stone lies
where that stone crops out, a stick where that tree stands — and from then on it changes as time passes and as
players act. The generation has to be good, and it has to produce a state the chain could have reached.

The founder's knowledge has to transfer at every link (§3). Someone who knows that a north-facing
slope is drier, that a valley floor holds deeper soil, that river flats grow different trees from a
ridge crest, should be able to act on all of it — and they will be right, because each of those is
a consequence here rather than a rule.

## How EarthGame2 builds it

v1 built the chain on a global grid of about 9.8 km a pixel, so it had to make its own landform by erosion (its
E-slices, `Docs/v1/SLICE_E1_EROSION.md` onwards). EarthGame2 starts from real elevation at 4 to 8 m, whose
valleys are already the record of what water did, so it does not erode (ARCHITECTURE §3, decision of
2026-09-07): links 1 and 2 are the data, and links 3 to 6 are computed from them once, when a world is created,
and saved as its layers (`WorldLayers`). What the layers say is the world's first state. The things that stand
and lie in it — trees, shrubs, grass, stones, litter — are to be generated from those layers, and are not built
yet (DEBTS.md); the simulation takes the world on from there.

## The test that decides whether this worked

Not a screenshot. Someone who can read country should be able to stand anywhere in this world, say
where the water is, where the deep soil is, and which slope is the dry one — and be right, without
ever having been told, because all three are consequences of the same history.


## The plants and stones of Bherwerre (M1.2, opened 2026-09-09)

The chain above was built in v1 on a Southern Highlands species list. v2's region is Bherwerre Peninsula, the
southern shore of Jervis Bay (ARCHITECTURE §3), and the species are its own. Each row names where the plant
stands on the peninsula and what its tolerances in `PlantSpecies.cs` are set from. The tolerances are not
measurements: they are Claude's reading of the habitat each source describes, stated as numbers so that a
person who knows the country can dispute them line by line (checked against published sources by the agent: CANON ruling 21).

Sources: the Booderee National Park management plan's vegetation communities (Director of National Parks;
the park is the Commonwealth half of the peninsula); PlantNET, the NSW flora online (Royal Botanic Gardens
Sydney) for each species' habitat; the site research of 2026-09-07 recorded in the plan (§4.2) and CANON.md.

| Plant | Form | Where it stands on the peninsula | The tolerances follow from |
|---|---|---|---|
| Blackbutt, *Eucalyptus pilularis* | tree, 20–40 m | the tall forest on the deeper, moist sands behind the dunes and on the sandstone slopes | needs half a metre of sand and shelter from the salt wind; the timber and the rough lower bark |
| Bangalay, *E. botryoides* | tree, 12–25 m | the eucalypt nearest the sea and around the swamps; sand, salt wind, wet feet | salt-hardy (exposure 0.75), wide moisture, fibrous bark to the branches |
| Old-man banksia, *Banksia serrata* | small tree | heathy woodland on dry sand behind the foredune, out of the worst wind | dry optimum, thin sand, moderate exposure |
| Coast banksia, *B. integrifolia* | small tree | the seaward face of the dune, where nothing else woody stands the salt | roots in 120 mm of sand; exposure tolerance near one, and a floor on it: out of the salt wind the trees take its ground |
| Swamp paperbark, *Melaleuca ericifolia* | small tree | the rim of Ryans Swamp and the lakes, feet in the water | wet optimum with a narrow breadth; bark in sheets |
| Grass tree, *Xanthorrhoea resinosa* | shrub | the heath on poor sand in full sun | needs almost no soil, no shade; the fire drill's spindle |
| Heath banksia, *B. ericifolia* | shrub | the heath itself, head high and dense, on the poorer and damper sands | wide moisture, thin sand, open ground |
| Bracken, *Pteridium esculentum* | herb | under the forest | shade tolerance near one, needs shelter |
| Lomandra, *Lomandra longifolia* | herb | the dune toe, the forest floor, the creek edges | the widest moisture range here; the fibre of the first cordage |
| Saw-sedge, *Gahnia sieberiana* (with the *Baumea* sedgeland) | herb | the swamp and the lake shore, where the ground is water half the year | wet optimum, narrow; shade-tolerant enough for the paperbark's rim |
| Kangaroo grass, *Themeda triandra* | grass | everywhere the trees and the heath are not | wide moisture, open ground; the first bedding |
| Spinifex, *Spinifex sericeus* | grass | the foredune, binding the sand | the one pioneer: a ceiling of 200 mm of soil takes it out wherever real soil has formed, and on a dune the soil is what the salt wind has let form, so it holds the young dune nearest the sea |

What the list leaves out, and why: the rock platforms and the beach carry nothing (the waves rework a beach, so
no soil forms on it: `WorldLayers.SiteAt`); the *Casuarina* and *Allocasuarina* of the sandstone country wait on the stone layer
that says where the sandstone crops out; the rainforest gullies of the escarpment are beyond the box.

### Held against where people found them (M1.2b, 2026-09-10)

The rules above were first held against the place on 2026-09-10 (CANON ruling 21), with the Atlas of Living
Australia's occurrence records of the twelve plants inside the box (`Tools/data/fetch_ala.py`: 587 records, 340
usable once the Booderee Botanic Gardens' planted specimens and the records not placed to within 100 m are dropped)
and the park's own account of its country. `species_check.py` asks, plant by plant, whether the world grows it round
the places it was recorded more than it grows it anywhere, and whether it grows it about as far from the sea. Four
things in the rules were wrong, and each is now a rule of its own:

- **The soil a root can use on sand** is the soil that has formed there. The soil model's depth is the loose
  material over the rock, two metres and more of sand under a beach, and read as soil it put plants of the forest
  floor on the foredune and kept spinifex off every dune. The beach, which the waves still rework, has none a root
  can use, and a dune has the soil model's depth less the share of it the salt wind reaches (`WorldLayers.SiteAt`).
- **Coast banksia has a floor on exposure**, at the salt wind of the dunes' inland edge: it stands the salt the
  trees cannot, and out of the wind the trees take the ground. Without it, it held two fifths of the land at a
  median kilometre from the sea, where its records are at a median 392 m.
- **A tall plant stands on a cell as often as the ground suits the best of them** (`PlantCommunity.CanopyCover`).
  Drawn the old way, any tree that could stand on a cell took it, and 99.9 % of the land stood under a canopy, the
  beaches included, where the park describes heaths. The canopy covers about half the land now, and the heath, the
  grass and the sand-binder have the open ground.
- **The dune is asked what grows on it before it is called sand** (`GroundCovers.Of`). The park says its dunes
  are held by what grows on them; the world's 1,005 ha of dune read as bare sand, and 85 ha of bare dune sand remain.

What is still off is owed in DEBTS.md ("The plants the records still find nearer the sea"): the records put
blackbutt, bangalay, lomandra and saw-sedge within about 450 m of the sea, and the world grows them a kilometre
inland. The records are presence only, gathered along tracks and biased towards plants people notice, so they say
where a plant is and never how much of it there is; they were checked for the bias that would matter most here,
and the recorders walked the whole peninsula rather than its beaches (all the usable records at a median 935 m from
the sea, against the land's 958 m).

### The stones (v1's open item E0)

v1's `SurfaceGeology` drew flint on coastal plains, and the Sydney Basin has none. What the peninsula offers a
knapper is what its archaeological record is made of: silcrete (the commonest flaked stone of the New South
Wales coast; Webb and Domanski, *Archaeometry* 50, 2008, for its flaking properties), quartz from veins and as
beach pebbles, rhyolite beach pebbles rolled from the volcanics to the north, and quartzite; the sandstone of
the cliffs abrades and does not flake. Their mineralogy is in `StoneType.cs`; the layer that says where each
lies waits on the topology mask (M1.2 promise 7) and reads beach pebbles onto the platforms and beaches, silcrete
onto the old land surfaces, and quartz where the sandstone's veins crop out.

### The water and the wake (M1.2, landed 2026-09-09)

Every rule below carries its number so that a person who knows the country can call it wrong by name; the
census a world creation prints (`census.txt`) is these rules read out over the real ground.

- **The sea** is the bake's ground at or below the datum. Its floor falls at one in twenty from the water's edge
  to 30 m (`WorldLayers.SeaFloor`), a rule in place of the bathymetry (DEBTS.md).
- **A lake read off the ground** is a flat (relief under 0.6 m across a 5 × 5 window) grown from its lowest cell
  through the cells within 0.5 m of that level, at least a hectare, with a rim: of the ground around it that lies
  outside the band, 85 % stands above it. A strip of a slope has a lower side and fails.
- **A mapped lake** (`water_bodies`, OpenStreetMap through `Tools/data/bake_water.py`) stands at the median of the
  bake's ground inside its outline; the outline's cells at or below that level are its water, the rest margin.
  A mapped wetland is swamp throughout. The sea and the lakes are the drainage's sinks: Windermere and McKenzie
  are perched dune lakes with no outlet, and a fill that had to spill flooded their basins to the lip.
- **Swamp** elsewhere is ground whose wetness is at or above 0.9 with a slope under 5 %; creek and stream are the
  drainage's channels (120,000 m² and 600,000 m² of catchment); trickle and damp the smaller ones. Fresh water
  for drinking is a creek, a stream or a lake; the sea is salt.
- **The surface** layer is the water's surface where water stands (the sea at the datum, a lake at its level) and
  the ground's elsewhere, so wading in a lake is the surface less the ground.
- **The wake** is the standable cell whose product of five graded factors is greatest: fresh water within 500 m,
  knappable stone (knappability at or above 0.25) within 1 km, fibre (lomandra, saw-sedge, spinifex) and
  firewood (any canopy) within 500 m, shelter rock (a cliff or a shore platform) within 2 km, each factor full at
  the thing itself, two thirds at the stated distance and nothing at three times it. No place to wake: the sea,
  within 500 m of the region's edge, under 2 m, steeper than ten degrees, a lake, a swamp, a creek or a stream
  underfoot, ground as wet as a swamp's, the cliff or the platform itself. Ties go to the least wind exposure
  (the site's, the coast's salt wind included), then the first cell in row order. The census prints the winner's
  reading and the water by name.

### The animals of Bherwerre (M1.2, promise 5)

Three, for now: the two kinds M1.7 materialises and the bird the dawn chorus needs. Each is described in
`AnimalSpecies.cs` by what it eats and what it can stand, never by where it lives, and the density figure is a
ceiling that `AnimalCapacity` scales down by forage, water and slope. The sources are in the doc comments.

| Animal | Living | Hours | Group | Ceiling | The figure follows from |
|---|---|---|---|---|---|
| Eastern grey kangaroo, *Macropus giganteus* | grazer of the flats and the open woodland | dawn and dusk | mobs of eight | 30 / km² | the band 10–30 / km² for good south-eastern woodland, between the published flanks of 3.18 / km² on the inland plains and 20–490 / km² at Coffs Harbour |
| Pied oystercatcher, *Haematopus longirostris* | the tideline: shellfish off the platforms, worms out of the wet sand | by day | pairs | 10 / km² of shore strip | about one pair per kilometre of ocean beach (the NSW threatened-species profile and recovery plan) on the two-hundred-metre strip a pair works |
| Superb fairy-wren, *Malurus cyaneus* | insects off the heath and the forest edge | by day; sings before first light | family parties of four | 250 / km² | territories of one to two hectares holding two to five birds (colour-banded studies); the top of that band |

What the list leaves out, and why: the swamp wallaby, the echidna, the bandicoot and the possums of the
peninsula's record wait on M1.7's second contract; the megafauna are absent because the peninsula has no
fossil record of them the canon has them (2026-08-25), and this list names only what the peninsula's own record supports.
