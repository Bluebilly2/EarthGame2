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


## The plants and stones of Bherwerre (M1.2, opened 2026-09-09)

The chain above was built in v1 on a Southern Highlands species list. v2's region is Bherwerre Peninsula, the
southern shore of Jervis Bay (ARCHITECTURE §3), and the species are its own. Each row names where the plant
stands on the peninsula and what its tolerances in `PlantSpecies.cs` are set from. The tolerances are not
measurements: they are Claude's reading of the habitat each source describes, stated as numbers so that a
person who knows the country can dispute them line by line (the owner's lane in the M1.2 contract).

Sources: the Booderee National Park management plan's vegetation communities (Director of National Parks;
the park is the Commonwealth half of the peninsula); PlantNET, the NSW flora online (Royal Botanic Gardens
Sydney) for each species' habitat; the site research of 2026-09-07 recorded in the plan (§4.2) and CANON.md.

| Plant | Form | Where it stands on the peninsula | The tolerances follow from |
|---|---|---|---|
| Blackbutt, *Eucalyptus pilularis* | tree, 20–40 m | the tall forest on the deeper, moist sands behind the dunes and on the sandstone slopes | needs half a metre of sand and shelter from the salt wind; the timber and the rough lower bark |
| Bangalay, *E. botryoides* | tree, 12–25 m | the eucalypt nearest the sea and around the swamps; sand, salt wind, wet feet | salt-hardy (exposure 0.75), wide moisture, fibrous bark to the branches |
| Old-man banksia, *Banksia serrata* | small tree | heathy woodland on dry sand behind the foredune, out of the worst wind | dry optimum, thin sand, moderate exposure |
| Coast banksia, *B. integrifolia* | small tree | the seaward face of the dune, where nothing else woody stands the salt | roots in 120 mm of sand; exposure tolerance near one |
| Swamp paperbark, *Melaleuca ericifolia* | small tree | the rim of Ryans Swamp and the lakes, feet in the water | wet optimum with a narrow breadth; bark in sheets |
| Grass tree, *Xanthorrhoea resinosa* | shrub | the heath on poor sand in full sun | needs almost no soil, no shade; the fire drill's spindle |
| Heath banksia, *B. ericifolia* | shrub | the heath itself, head high and dense, on the poorer and damper sands | wide moisture, thin sand, open ground |
| Bracken, *Pteridium esculentum* | herb | under the forest | shade tolerance near one, needs shelter |
| Lomandra, *Lomandra longifolia* | herb | the dune toe, the forest floor, the creek edges | the widest moisture range here; the fibre of the first cordage |
| Saw-sedge, *Gahnia sieberiana* (with the *Baumea* sedgeland) | herb | the swamp and the lake shore, where the ground is water half the year | wet optimum, narrow; shade-tolerant enough for the paperbark's rim |
| Kangaroo grass, *Themeda triandra* | grass | everywhere the trees and the heath are not | wide moisture, open ground; the first bedding |
| Spinifex, *Spinifex sericeus* | grass | the foredune, binding the sand | the one pioneer: a ceiling of 200 mm of soil takes it out wherever real soil has formed |

What the list leaves out, and why: the rock platforms and the beach carry nothing (20 mm of sand is under
even spinifex's floor); the *Casuarina* and *Allocasuarina* of the sandstone country wait on the stone layer
that says where the sandstone crops out; the rainforest gullies of the escarpment are beyond the box.

### The stones (v1's open item E0)

v1's `SurfaceGeology` drew flint on coastal plains, and the Sydney Basin has none. What the peninsula offers a
knapper is what its archaeological record is made of: silcrete (the commonest flaked stone of the New South
Wales coast; Webb and Domanski, *Archaeometry* 50, 2008, for its flaking properties), quartz from veins and as
beach pebbles, rhyolite beach pebbles rolled from the volcanics to the north, and quartzite; the sandstone of
the cliffs abrades and does not flake. Their mineralogy is in `StoneType.cs`; the layer that says where each
lies waits on the topology mask (M1.2 promise 7) and reads beach pebbles onto the platforms and beaches, silcrete
onto the old land surfaces, and quartz where the sandstone's veins crop out.

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
fossil record of them and CANON.md asks every species to be justified from the regional record.
