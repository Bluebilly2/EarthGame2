# The beta, worked backward — from the Founder's Path down to what can be built

**Status:** written 2026-09-22 on William's direction (CANON ruling 41): work backward from the finished beta arc,
find every system underneath each thing a player should be able to do, and build the foundations first, by
dependency and leverage. This page is the audit and the dependency map, and it is kept current: a slice that pays
part of it marks the part paid, with its commit. The arc is `FOUNDERS_PATH.md` (binding). The constitution's
primitives the map rests on are `GAME_DESIGN.md` §7 (state, transformations, constraints, networks), §8 (materials
by their properties, never by labels), §9 (a process makes a distribution, not a perfect item), §16–17 (systems of
parts, hierarchical causality), §23 ("why did it fail?" as a first-class capability) and §46 (the one design test:
can the player decide differently because of it?).

The inventory in §2 was made on 2026-09-22 by reading the code, not the contracts; the numbers in §3 were measured
on the gate world (`Artefacts/worlds/gate`, seed 1347) by a script over its own layers.

## 1. The arc, reconstructed

What the beta is for, in William's words (2026-09-01): *"the beta arc isnt about how long the player survives, its
about when they get to a shelter with something that resembles a bed, a fire and some tools. the core reason for
the beta arc is a proof of concept of the game."* Its shape: wake late-winter with nothing but the tablet → water,
first stone, first fire before dark → choose ground and raise a camp from what the country gives → the first rain
proves it → the arc completes the first night the founder sleeps warm, in a bed they made, by a fire they lit, with
tools they knapped, under a roof they raised → dawn brings the tablet's summary. Three to four founder-days; ninety
minutes to two hours. Death at any point is explained. Two modes at new game. The bar of 2026-08-26: a stranger can
play it.

The five things a developer must see, unmissably: a real place; an oracle that cannot lie; materials over recipes
(damp tinder fails and says why; two flakes off one core are different tools); building judged by physics (the
windbreak on the wrong side does nothing); a world that made itself.

The player's goals, in the path's order. "Stands" is the honest state on 2026-09-22.

| | Goal | The path's beat | Stands |
|---|---|---|---|
| G0 | Start a game as a stranger, choosing a death mode | the shell | a menu with New world / Continue / Quit; no mode, no settings, no pause, no death screen |
| G1 | Wake with the tablet, look, walk, read the place | Waking | look, walk, slide, swim, wade, hands: yes; the tablet: nothing; surf sound: none |
| G2 | Drink: the sea will not, the creek will | Thirst, Water | FP.1 built and played; "reading the land" to find it: the gully and the green are only what the data gives |
| G3 | Make an edge from the right stone | First stone | FP.3's physics built; where stone is cannot be seen (no rock stands in the world); a flake does no work yet |
| G4 | Cut fibre and lay cord | First stone | nothing: no fibre taken, no cutting, no cord |
| G5 | Ask the tablet about tonight | The forecast | the weather model exists on both sides; the tablet does not |
| G6 | Make fire before dark from found wood and judged tinder | Fire | nothing: no wood properties, no tinder, no carving, no drill, no fire |
| G7 | Live through the first night at the fire | Night 1 | the night kills a standing founder at dawn; the bridge holds the death off (ruling 33) |
| G8 | Eat: the digging stick, tubers, roasting | Act II | nothing: no hunger, no food plants read as food, no digging, no cooking |
| G9 | Choose ground by reading the country | Act II | the layers know slope, wetness, exposure, stone; the player is shown none of it and the ground is smooth |
| G10 | Raise a camp from parts: windbreak, bedding, fire ring, rack | Act II | nothing: no placing, no parts, no structures, no judgement |
| G11 | Endure the first rain | Act II | rain exists in the weather and falls on nothing: not drawn, not heard, not wetting |
| G12 | Sleep a full night warm; the arc ends; the dawn summary | the end | nothing: no sleep, no goal state, no summary |
| G13 | Die and be told why; Standard and Hardcore | all | Standard death with one sentence; no Hardcore |
| — | A world that lives: animals present, weather, sound | all | kangaroo mobs and oystercatcher pairs stand up, flee and return; weather held to the lighthouse; footsteps and strokes; no wind, rain, fire, surf or birds |

## 2. What stands: the inventory of the code

**The world** (`Engine/packages/com.earthgame.engine/Runtime/World/`, `Tools/data/`, `Tools/world/`). Heights from
AWS Terrain Tiles at zoom 14 (7.8 m a pixel at the centre, SRTM 30 m inland with Geoscience Australia 5 m where
present) sampled to a 2001 × 2001 grid at 4 m; OpenStreetMap water outlines. Computed once at creation
(`WorldLayers.Compute`): the sea floor by rule, lakes, drainage (priority flood, D8, catchment), soil depth and
wetness (`SoilModel`), the water classes and the creeks' water (WG.1), the topology bits (sea, beach, dune, wetland,
forest, heath, crest, cliff, shore platform, lake, creek), the plant community (twelve species, `PlantSpecies`), the
cover byte, the stand (a tree is its cell: species and height in one byte), the loose layer (sticks under trees,
cobbles by topology, two four-bit counts a cell), the stone (a hashed province lattice keyed off topology,
`GeologyScale`, **not the real geology**), animal capacity, the wake score. No aspect, no outcrop, no boulder, no
scree, no canopy model, no lithology from data, no relief below the 4 m raster (`TerrainTileBuilder.cs:10`: "procedural
detail below that comes later"). The client draws a kilometre tile at 513 posts (1.95 m) bilinearly from the 4 m
posts, so the drawn ground already has twice the posts the layer carries and nothing to fill them with.

**The body** (`Runtime/Body/`). `Hydration` and `Warmth` are v1's physiology ported with published numbers; death by
cold and by thirst, one sentence each (`Death.Explain`); the wire carries two doubles (`FounderStateMessage`). The
body knows no roof, no ground, no fire, no clothes, no sleep, no hunger, no rain (DEBTS 2026-09-16). Exposure to
wind is read from the terrain's openness and the coast (`WorldState.ExposureAt`); no built thing shelters anything.
The player file (version 6) holds water loss and core deficit and nothing else of the body.

**Things** (`Runtime/World/Definition.cs`, `EntityStore.cs`, `Carrying.cs`). Kinds are a static catalogue built in
code: the player, one per plant, stone and animal, `item/stick`, `item/cobble`, and a cobble and a flake per stone. A
definition carries key, kind, name, spawnable, mass and radius, and an untyped `Row` back to its table. An instance
carries `ItemComponent`: resting, fall speed, mass, edge, platform angle, flakes taken — a fixed six-field struct
written by hand in three formats (`EntityWire`, region file v3, player file v6) and dropped entirely from the carrying
message, so the client shows a kind's nominal mass, never the thing's. A lying stick or cobble has no properties at
all until taken, and taking it changes its shape (DEBTS 2026-09-11). `StoneType` (eleven rows of published mineralogy
with derived knappability, edge quality, pounding quality) is the one property-driven material in the game and the
pattern to follow. There is no wood table: `PlantSpecies` has heights, bark thickness and sticks shed, no density,
hardness, strength, moisture, fibre or food.

**Verbs** (`Carrying.cs:7-52`, `GameServer.HandleIntent`, `VerbController.cs`). Five verbs in a byte enum, each
passing through three hand-written switches (the wire's write and read, the server's dispatch) and a fourth for the
target's kind. Every intent is one-shot with an immediate result; the work button's wind-up is a client timer sent as
a byte. Knapping (`Materials/Knapping.cs`) is a real fracture model, a single instantaneous blow, its constants in
code. Nothing continues over ticks; there is no notion of work with a rate, a requirement or progress; the offer
("what can I do to this with that") is a hand-written cascade of cases.

**Hands.** Nine places, one the hand; reach 4 m from the eye; no mass or bulk limit; the Tab window lists names and
nominal masses.

**Changes to the world.** Only takings: a bit per index per cell (`LooseTaken`), monotone, broadcast to all and
replayed whole at every join, saved as region-file diffs of a fixed nine-byte shape that the reader refuses to vary.
The server encodes every tile once and keeps it forever (`TileService._encoded`); the client caches by CRC. **There
is no way to change a tile after creation** — no felled tree, no cleared ground, no dug hole, no scar.

**Entities.** 64-bit ids, definitions, position, yaw, an item or an animal component, per-field tick stamps, per-viewer
dirtiness within 4096 bytes a tick a session; `Within` walks every entity for every session every tick (the reason
trees are a layer). No parent, child or reference between entities; a two-bit component mask; the region file
refuses any component but the item's. One fast system (`ItemFall`); the slow-layer scheduler exists with nothing
registered in production.

**Weather** (`Weather.At`): air, wind, cloud, rain rate, humidity, dew point, pure from seed, region and clock;
computed on both sides; rain and cloud drawn nowhere and heard nowhere.

**UI** (`Unity/Assets/EarthGame/Client/*Controller.cs`, all built in C#, no UXML). A shell (New world, Continue,
Quit), a loading screen, a HUD (dot crosshair, clock, two words of condition, a verb line, a notice, the Tab window),
the F3 panel with three greyed environment stubs. No settings, pause, death screen, first-run, tablet, objective,
forecast or inspection beyond the verb line's name.

**Sound.** Synthesised at load: footsteps over seven footings, strokes, two landings. No mixer, no wind, rain, fire,
surf or birds.

**Animals.** Three species by presence and capacity, stood up within 500 m, flee and return; nothing saved; no harm,
no meat, no tracks; the fairy-wren sings at dawn in its table and nowhere else.

**The wire and the files.** Protocol 16, tile format 2, region file 3, player file 6, `world.json` 1; every format
restated in Python by `save_check.py` and `tile_check.py`; a change to instance state costs protocol, region, player,
digest, verifier and ARCHITECTURE §10 in one commit. That cost is paid once per shape, so the shape must be made
general before it is paid again.

**Reusable as they are:** the definition ids, the entity store's stamps and per-viewer dirtiness, the tile codec and
its residency, the raster format and its verifiers, `StoneType`'s pattern, `Hydration`/`Warmth`'s balance, the weather,
the drainage and soil models, `StandLayout`'s hashing (a thing's place from its address alone), the intent/result
channel with words on it, the gate and the verifier discipline.

**Weak, and in the way of the beta:** the fixed `ItemComponent` mirrored by hand; the two-kind loose layer; the
per-verb switches and one-shot intents; the immutable tiles; the two-kind, no-reference entity; the definition
catalogue with no material behind wood; the client's `ItemLooks` if-chain that fails the build for a kind without a
look; the hands' nine unweighted places; a ground with nothing below its data.

## 3. The country as it is, measured

The gate world's own heights, read by a script on 2026-09-22 (the frame of `Tools/world/dune.py`):

| | |
|---|---|
| Land share of the 8 km box | 0.74 |
| Land heights | 0.1 to 157 m, mean 45 m |
| Land slope by band | under 3°: 36 %; 3–6°: 38 %; 6–10°: 18 %; 10–15°: 6 %; 15–20°: 1.3 %; 20–30°: 0.4 %; over 30°: 0.1 % |
| Within 500 m of the wake | heights 0 to 45 m; mean slope 4.7°; 7 % over 10°; the dune face 38 m from the wake at 30°+; nothing over 45° within 250 m |
| Relief below 30 m (the height less its 30 m box mean) | standard deviation 0.6 m everywhere — the ground carries nothing smaller than a dune |
| Ground within 500 m of the wake | dune 32 %, sea 21 %, beach 14 %, heath on dune 13 %, forest on dune 4 %, wetland 4 % |
| Stone the layer names within 500 m | quartz 37 %, sandstone 17 %, rhyolite 15 %, quartzite 2 %; none under the sea |

The honest reading. Bherwerre is a low sand peninsula: the data says so, and the data is right about the place. The
wake scorer then chooses the gentlest of it on purpose (under 10°, over 2 m, not sodden). William's "flat land
covered in trees" is what the region's true shape gives at the raster's 4 m with nothing built below it. The ruling
of 2026-08-25 — real data is the skeleton, procedural detail lives below its resolution — has never been paid
(DEBTS 2026-09-10 (ii)). What the country has at its real scale and does not show: the south coast's cliffs and shore
platforms (thin in the box; Steamers Head 1.6 km east of it), creek gullies and their banks, the dunes' faces and
swales, wet heath and swamp, the forest's edge and clearings, the rock where the sand is thin. What it does not have
at all in the data: the metre-scale relief (banks, hummocks, ruts, tussocks, gully sides, rock ledges, boulders) that
a person walking reads without thinking, and the geology (the stone layer is a lattice, not the Permian sandstone and
Quaternary sand the place is made of).

William's list of what a believable survival environment requires, against the world:

| Requirement | State |
|---|---|
| meaningful elevation, valleys, ridges, slopes | at the data's scale: yes, gently (a peninsula); below 4 m: none |
| rocky outcrops, cliffs, exposed stone | cliff and platform cells exist in the topology and cover; nothing stands: no ledge, no boulder, no visible rock a founder can walk to for stone |
| soil, different ground conditions | soil depth and wetness computed and saved; underfoot only as the cover's colour and the footstep's sound; nothing firm, soft, boggy or loose to the walk or the build |
| drainage and water flow | the drainage network, the creeks carrying water (WG.1): yes |
| natural clearings; dense and sparse vegetation | as the canopy layer falls out: yes, but nothing read from it (no shade, no shelter, no clearing as a place) |
| resource distribution | sticks under trees, cobbles by topology, fibre and firewood as distances the scorer reads; no driftwood on beaches, no bark or tinder, no food plants as food, no stone to be seen |
| terrain affordances; places suitable or unsuitable for shelter | the layers hold slope, wetness, exposure, cover; no rule reads them for a build, and nothing tells the player |
| variation in ecology | twelve species by suitability: yes |
| natural relationships between terrain, vegetation, resources and water | the chain rock → landform → water → soil → plants → animals is built (ruling of 2026-08-25); the player meets only its last link |

## 4. The chains: what each goal needs, down to what can be built

Each goal, then what it needs, then what that needs. A chain stops at a thing that can be built and tested. The
letter in brackets names the foundation of §5 it bottoms out in.

**G2 Drink** (built) → reading the land to find water → the gully seen as a gully and the green as green [F4]; the
tablet's answer to the sea [F8].

**G3 An edge** → knowing where stone is → stone that stands in the world: outcrops, ledges, the shore platform's rock,
boulders in the gully, gravel at the creek [F4] → the stone layer true to the place [F4] → a cobble whose stone is
seen and named when looked at [F1: inspection]. → Knapping (built) → turning the core and the hammer's wear (DEBTS)
[F2]. → A flake that does work: its edge and mass judged by the process it is used in [F2] → the flake's own state kept
wherever it goes, in the hand and on the wire [F1]. → "Two flakes off one core are different tools": their differing
state shown in words [F1].

**G4 Cord** → fibre plants standing where the country puts them (Lomandra: built as a species; the dune's foot) → taking
fibre from a plant: a process on a standing plant with an edge or by hand, yielding fibre by the plant's size, leaving
the plant less [F2, F3] → fibre as a thing with length, strength and wetness [F1] → laying cord: a process of the
hands over time, two strands to one, the cord's strength from the fibre's [F2] → cord as a thing with length and
strength [F1] → cord's uses: binding (a bow, a rack, a windbreak) as a requirement other processes read [F2, F6].

**G5 The forecast** → the tablet [F8] → tonight's minimum, wind and rain from `Weather.At` run forward (built, both
sides) → what a bare night does: `Warmth` run forward on that night (built) → the same model answering, so it cannot
lie (§23).

**G6 Fire** → *fuel*: wood as a material with density, moisture, hardness and heat by species [F1] → found wood: sticks
(built, no properties), driftwood on the beach (not placed; beaches shed no sticks) [F4], dead limbs and fallen wood
(not placed) [F4], a felled tree (nothing can be felled) [F3] → breaking a stick to length, splitting a log [F2] → *tinder*:
bark strips, paperbark sheets, dead grass, banksia fluff, each a material with moisture [F1] → placed where the plant
that sheds it stands, and taken from the standing plant by a process [F4, F2, F3] → *the kit*: a hearth board (split,
flat, soft dry wood: `split`, `carve`, `notch` with an edge) and a drill (a straight dry stick, thumb-thick, arm-long:
`point`) [F2, F1] → *ignition*: friction: power from the founder's force and speed against the wood pair's hardness and
moisture, the dust's temperature rising to a coal, failing when wet, hard or slow, and saying why [F2, F5] → the coal
into tinder (dry enough), tinder into kindling, kindling into fuel [F5] → *the fire*: a thing in the world with fuel by
mass and moisture, a burn rate, heat, light and embers over hours, fed by a process, dying when starved or rained on
[F5] → the server running it every tick and telling its viewers at a sensible rate [F5 on the entity store] → a fire ring
anywhere legal [F6] → *the body*: the fire's warmth on the body by distance and side (`Warmth`'s missing radiant term)
[F5] → the bridge comes down [F5].

**G7 Night 1** → fire (G6) → fuel classes and the night's arithmetic (kilograms an hour by the fire's size) [F5] → the
body's balance with the fire and the wind behind the dune [F5, F6] → dawn as the first victory said by the tablet [F8].

**G8 Eat** → hunger: the body's energy balance (`Warmth` already spends watts; the store, the deficit, the words, the
work capacity) [F7] → food plants read as food (Lomandra bases, bracken rhizome, bulbs, pigface, banksia nectar: which
are edible raw, which need roasting) [F1 content] → a digging stick (`point`) [F2] → digging where the plant stands: a
process on the ground yielding by the plant, leaving a hole [F2, F3] → roasting at a fire: a process with heat and time
[F2, F5] → pounding with a stone [F2] → eating [F7].

**G9 Choose ground** → the ground legible: relief, the lee of a dune, wet ground, the creek near, the stone near [F4] →
the judgement the player can make being the same the model makes: exposure, drainage, slope, clearance [F6 placement
rules over the layers] → the tablet saying what the place is [F8].

**G10 A camp** → parts: a windbreak (sticks stood in a row, wedged or bound, with height, length and density), a bedding
pile (bracken, grass, paperbark by mass to depth), a fire ring (stones), a drying rack (a frame and cord) [F6] → each a
structure of parts, the parts things consumed into it, qualified by properties (length ≥, count ≥, cord ≥) [F1, F6] →
placing as work over time [F2] → fixed to the world and saved with it [F3, F6] → the ground cleared first where the
understorey stands [F3] → judged by physics: the wind at the body in the wall's lee by its height and the wind's
direction, rain kept off or not, the ground's conduction through bedding, the sky's cold through a roof or a canopy,
the fire's warmth — all as terms in `Warmth`'s balance [F6, F5] → the parts drawn from what they are made of (sticks in a
row, a pile) [F6 client].

**G11 The first rain** → rain drawn and heard [F10] → rain on the body: wet skin, insulation lost, evaporation [F7] →
rain on things: tinder and fuel taking water, drying by sun, wind and fire [F1 moisture, F5] → a roof and a wall
keeping it off [F6] → the wet cold's death [F7].

**G12 Sleep, the end, the summary** → lying down on bedding: a process over hours, the clock run on while the balance
is checked every step, waking at cold, thirst or dawn [F7] → the goal state recognised: shelter standing, bedding laid,
fire alive, a knapped kit carried, a night slept [F8] → the dawn summary from a journal of what was found, made and
learned [F8].

**G0, G13 The shell and the modes** → new game with a death mode, Hardcore ending the run where the founder does,
settings, a pause, a death screen, the first run [F9] → death explained by mechanism [F8].

**G1 and the living world** → the tablet in hand and its first words [F8] → surf, wind, birds, rain, fire in the ear
[F10] → tracks by the creek, birds working the tideline, a wallaby at dawn (mostly built; tracks are a placed feature)
[F4].

## 5. Where the chains bottom out: the foundations

| | Foundation | Depended on by | Exists | In the way |
|---|---|---|---|---|
| F1 | **Things with properties.** Materials as tables with published numbers (wood by species: density, moisture green and dry, hardness, strength, heat; fibre; bark; stone as now); a thing's instance state as one general record with one owner (mass, length, diameter, moisture, edge, condition, look, flags), carried everywhere the thing goes; a lying thing's properties a function of its place, like its position; a thing named by its properties in words. | G3 G4 G6 G8 G10 G11 G12 and every later era (§8) | `StoneType`; `ItemComponent` for stone alone | the fixed struct mirrored by hand in three formats and dropped from carrying; no wood table; lying things without properties |
| F2 | **Work.** One model of a process: an actor with what is in hand, on what is looked at, doing a named kind of work; requirements read off properties (an edge of at least, a mass of at least, two hands); a rate from the tool's and the material's properties and the body's capacity; progress kept on the server over ticks, interruptible, resumable; results (things made, the target changed, byproducts) and failure with its reason in words; the offer computed by the same rule the server judges by. Knapping folded in as one blow of work. | G3 G4 G6 G8 G10 G12 | one-shot verbs; the knap's physics; the wind-up | the per-verb switches; no continuing action; no requirement or rate anywhere |
| F3 | **The world changes.** A general record of change to the generated world: per cell, a kind and a magnitude (taken, felled, cleared, dug, scarred, a plant's yield spent), replicated as diffs, saved with the region, and tiles re-encoded when a change touches what they carry. Felling a tree: its trunk and limbs become things where it fell. | G4 G6 G8 G9 G10 | takings (a bit per index) | the nine-byte diff; no tile invalidation; the two-kind loose layer; no magnitude |
| F4 | **The ground below the data, and the country's things where they belong.** Relief below 4 m as one deterministic function of the layers and the seed, the same on the server's ground and the client's: creek banks and gully sides, dune faces and swales, hummocks in heath, ruts and steps in the forest floor, rock ledges on crests and cliffs, the shore platform's rock, boulders where the soil is thin; the stone layer read from the place's real geology; ground condition underfoot (firm, soft, loose, boggy, rock) from cover, wetness and soil, felt by the walk and read by the build; things placed by the country: driftwood on beaches, bark and tinder under the trees that shed them, dead wood in the forest, gravel in creek beds, food plants where they grow, tracks by the water. | G2 G3 G6 G9 G10, and William's direction directly | the layers (slope, drainage, soil, wetness, topology, community); the drawn ground's spare posts | nothing below the raster; the stone lattice; no feature placement beyond sticks and cobbles |
| F5 | **Heat and fire.** A fire as a thing of the world with fuel by class, mass and moisture, a burn model (rate by surface and wind, heat by the wood's value and its water, embers, out), ignition by friction with its physics and its failures, feeding, drying, cooking, light; the fire's radiant warmth in the body's balance by distance; the bridge taken down. | G6 G7 G8 G10 G11 G12 | the weather's wind; `Warmth`'s balance ready for the term | no fire, no heat, no server system for a thing that changes by itself |
| F6 | **Structures and the body's shelter.** A structure as an entity of parts with a state, made by work from qualified things; placement judged by the layers (slope, ground, water, clearance); its effects as terms in the body's balance: wind in the lee by geometry and direction, rain kept off, the ground's conduction through bedding, the sky's cold under a roof or a canopy. | G9 G10 G11 G12 | exposure from terrain | no parts, no references between entities, no placement rule, no shelter term |
| F7 | **The body's rest.** Hunger as an energy balance with a store and words; food eaten; wetness from rain and water, drying; sleep as a process with the clock run on; the wet cold. | G8 G11 G12 | `Warmth`'s watts; `Hydration`'s pattern | none of the four terms; the player file's shape |
| F8 | **The tablet.** The page (screen-space, the slab as a prop); the objectives as goal states the models recognise, all shown, ordered by what presses most (§5, never hidden); questions answered by the models themselves (the sea, the night, the tinder, the wall, the death); the forecast; the journal and the dawn summary; the arc's end recognised. | G1 G2 G5 G7 G12 G13 | the models it would read | nothing; no page, no goal state, no journal |
| F9 | **The shell and the modes.** New game with a mode; Hardcore; settings; pause; a death screen; the first run; crash safety. | G0 G13 | the three-button shell | nothing behind it |
| F10 | **Sound and feedback.** Surf, wind by exposure, rain, fire, birds; rain and cloud drawn; work seen on the thing (bark comes away, a flake falls, a stick is pointed). | G1 G6 G11 | the synthesis pattern | no mixer; nothing drawn of the weather; no changing looks |

## 6. Prioritisation

**Foundational blockers** (many things depend on them; nothing above them can be built honestly without them): F1,
F2, F3, F4. F5 and F6 are half foundation (the fire's server system; the entity of parts) and half beat.

**Core beta systems** (the survival loop itself): F5 fire, F6 shelter, F7 hunger, wetness and sleep, and the arc's
end in F8.

**Content gaps** (data the systems need, each with a source): wood by species; fibre plants and their yield; tinder
by plant; food plants and their preparation; the stones of the place; driftwood and dead wood; the tracks; the
animals' dawn.

**Depth gaps** (built, too shallow): knapping's turn and wear and the plain cobble (DEBTS); the hands (nine places, no
weight, no bulk, no two-handed thing); the water's edge and the lake beds (DEBTS); the animals' flight on nothing read.

**World-generation gaps**: everything in F4; the cliffs thin; the sea floor a rule; no inland climate gradient; the
canopy as a boolean.

**Interaction gaps**: break, split, carve, point, notch, strip, cut, twist, bind, stack, dig, place, feed, blow, lie,
sleep, eat, drink from cupped hands.

**Feedback and UI gaps**: the tablet; inspection by properties; progress of work; the body's hunger and wet; the
camp's state; the forecast; the death explained.

**Polish**: the shell; sound; the weather drawn; crash safety; performance with structures and fires in the store.

## 7. The order of building, by dependency and leverage

Each is a contract under `Docs/contracts/` (the beta's foundations are the `BF.` series), proved by tests first,
sabotage, verifiers where a format or a layer changes, frames where the look changes, William's hands where the feel
does. The order is the dependency order; leverage decides ties.

1. **BF.1 Things with properties** (F1). The substrate for everything made. Pays the fixed record, the carrying
   message, the lost shape (DEBTS 2026-09-11, 2026-09-16 ×2), the wood table, the lying thing's properties, inspection.
   **Built 2026-09-22** (`contracts/BF.1_THINGS_WITH_PROPERTIES.md`): the stone layer travels as a tile with it.
2. **BF.2 Work** (F2). The process model on the server; the first processes: break, strip, cut, point, split, twist;
   knapping folded in; the offer by the rule. With BF.1 this makes fibre and cord (G4) and the fire kit's parts.
   **Built 2026-09-22** (`contracts/BF.2_WORK.md`): break, strip, point and twist; split and notch wait for the fire's
   kit; the knap stays a blow of its own, the offer rule shared.
3. **BF.3 The world changes** (F3). The change record, tile re-encoding, felling and fallen wood, clearing, digging,
   a plant's yield spent.
   **Stage one built 2026-09-22** (`contracts/BF.3_THE_WORLD_CHANGES.md`): the record of change beside the layers, on
   the wire, in the region file (5) and the digest; no tile is re-encoded, the diffs ride beside immutable layers. Stages
   two and three (standing targets and the works on them; the cut and the fall) follow.
4. **BF.4 The ground below the data** (F4). Relief below 4 m on both sides; rock that stands; ground condition; the
   stone of the place; things placed by the country. Frames, and William's eyes. (This is the slice that answers
   "flat land covered in trees" wherever the region is.)
5. **BF.5 Fire** (F5). Ignition, the fire in the world, its warmth on the body, the bridge down. G6, G7.
6. **BF.6 Parts and the camp** (F6). Structures of parts, placement, the shelter terms. G9, G10.
7. **BF.7 The body's rest** (F7). Hunger and food, wetness and rain, sleep. G8, G11, G12's night.
8. **BF.8 The tablet** (F8). The page, the objectives, the answers, the forecast, the journal, the summary, the end.
   Its first two answers (the sea, the night) could come earlier at little cost; its spine waits for the models.
9. **BF.9 The shell and the modes** (F9); **BF.10 Sound and the weather seen** (F10); the feedback pass.

Why this order and not the beats' own: fire (the next beat) needs wood with properties, tinder with moisture, a
carving process and a thing that changes by itself on the server — BF.1, BF.2, BF.5 — and building fire first would
have meant writing each of those as a special case of fire and then again for cord, shelter and food. The tablet is
the demo's spine but unlocks nothing underneath it; it reads models, so it comes when the models are there to read.
The ground (BF.4) has no code dependency on BF.1–3 except the placing of new kinds of lying thing, which BF.1's
properties and BF.3's record make room for; it could run beside them, and it is fourth only because the maker's chain
is longer.

## 8. What waits on William

- **The place: decided.** Bherwerre is a low peninsula and the data is right about it; on 2026-09-22 he chose "a
  second steeper place" (CANON ruling 42). The candidates measured, the recommendation (Wilsons Promontory, Tidal River
  to Oberon Bay) and the acquisition manifest are `contracts/WG.2_A_SECOND_PLACE.md`; nothing is fetched until he
  approves those exact files. BF.4 gives any place its grain below the data; the second place gives the beta its
  mountains.
- **His eyes** on the ground when BF.4 stands, in frames and in play; **his hands** on the work when BF.2 does.
