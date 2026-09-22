# Architecture — how EarthGame2 is built (living)

**Status:** living document. Any commit that contradicts a paragraph here amends the paragraph in the same commit;
a paragraph nobody amended is a claim about the tree as it is. Decisions are agent decisions unless marked as an
owner ruling (those live in `CANON.md`). Last full revision 2026-09-08.

## 1. The shape in one paragraph

There is no single player. The world lives in an engine-free C# library that is the authoritative server; the
Unity client draws what the server says and sends intents; solo play is the client and the server in one process
joined by an in-memory transport, every message serialised; a friend joins the same server by IP over UDP; a
dedicated server is the same server object in a console with no client. The world is a bounded 8 × 8 km region
on a tangent plane anchored at a real place (Bherwerre Peninsula, NSW), its ground the record of real elevation
data at 5–30 m with procedural detail only below that, its soil, plants and animals derived from that ground
at world creation and saved as layers: the world starts in a generated state that follows from the country, and
changes from there by simulation and by what players do (CANON ruling 22).

## 2. Assemblies

| Assembly | Where | Engine-free | References | Runs where |
|---|---|---|---|---|
| `EarthGame.Engine` | `Engine/packages/com.earthgame.engine` | yes | — | server; client (read-only use, prediction) |
| `EarthGame.Protocol` | `Engine/packages/com.earthgame.protocol` | yes | Engine | both |
| `EarthGame.Transport` | `Engine/packages/com.earthgame.transport` | yes (LiteNetLib vendored, `#if UNITY_*` confined to it) | — | both |
| `EarthGame.Server` | `Engine/packages/com.earthgame.server` | yes | Engine, Protocol, Transport | SOLO, HOST, DEDICATED; never JOIN |
| `EarthGame.ClientCore` | `Engine/packages/com.earthgame.clientcore` | yes | Engine, Protocol, Transport | SOLO, HOST, JOIN |
| `EarthGame.Client` (Unity) | `Unity/Assets/EarthGame/Client` | no | ClientCore, Engine, Shared | client builds |
| `EarthGame.Shared` (Unity) | `Unity/Assets/EarthGame/Shared` | no | Engine | client builds |
| `EarthGame.Bootstrap` (Unity) | `Unity/Assets/EarthGame/Bootstrap` | no | everything | client builds; the only home of `UNITY_SERVER` |
| `EarthGame.ServerHost` (console) | `Engine/tools/EarthGame.ServerHost` | yes | Engine, Protocol, Transport, Server | DEDICATED |

- `EarthGame.Client` never references `EarthGame.Server`, and vice versa; the asmdef references enforce it.
- Each engine-free package is a local UPM package (`package.json`, `.asmdef` with `noEngineReferences: true`,
  `Runtime/**/*.cs`, committed `.meta` files) referenced by the Unity project via `file:`, **and** compiled by
  dotnet from `Engine/EarthGame.<Name>.csproj`, which globs the same `Runtime/**`. Output goes to
  `Engine/.build/` so Unity never imports a `bin/` or `obj/`. `Engine/Directory.Build.props` pins C# 9,
  `Nullable disable`, warnings as errors, deterministic builds.
- Tests: `Engine/tests/EarthGame.Tests` (NUnit on net10.0, outside the Unity project); Unity edit-mode tests only
  for settings, the controls asset and the items' looks.

Startup (M1.4 loading, 2026-09-10): `WorldPreparation.Load` is the one function that opens a
world, for the game and for the dedicated host, and `SourceRulesTests` holds it to that.
Bootstrap presents a separate loading panel, then runs it on a worker with resolved paths,
seed and cancellation token; the host calls the same function on its own thread and logs
the stages the panel would have shown. Progress enters a concurrent queue drained by the main thread;
only that thread creates Unity objects or starts transports. Creation writes the same
layers and an initial save; continuation reads its saved terrain and verifies the
manifest checksum. Only legacy saves without heights layers use the bake. Every refusal names
the file it could not read, since that message is the whole of what a player is told on the
failure screen and an operator in the host's log. A failed
read stays on an error screen with return to menu. Cancellation is checked between
stages, without joining the worker on exit. The overlay closes on the client's
existing collidable-terrain-and-snapshot readiness event. Client view construction
still runs on the main thread; this slice does not claim a frame-time budget for it.

## 3. The world

- **Frame:** tangent plane at the region centre, +X east, +Y up, +Z north, planet radius 6,371,000 m
  (`LocalFrame`, `WorldPoint`, `Double3`, ported from v1 with `CoordinateTests`).
- **Extent:** `Region.ExtentM = 8000`, stated once, enforced by the server (position clamp) and by a source scan
  that no other literal metre appears in the world code. The edge: the sea on the coast sides; on land a soft
  boundary with a "not yet" vignette on the client; animals treat it as impassable; a static far skirt draws
  what lies beyond.
- **Region:** `bherwerre` — centre 35.140°S 150.675°E; box 150.6311–150.7189°E, 35.1761–35.1039°S; the region names
  no wake point of its own (CANON ruling 20); the wake is the scorer's (below), and a world made without one wakes
  the founder at the region's centre. Fallback region: Ulladulla.
- **Data at the region's true resolution:** AWS Terrain Tiles at zoom 14–15, baked by `Tools/data/bake_region.py`
  to `Data/regions/bherwerre/heights.r32` with a sidecar recording the effective source resolution per tile
  (SRTM 30 m inland, Geoscience Australia 5 m where present). Procedural detail (v1's `TerrainSynthesis` recipes
  and double-precision `Noise3D`) only below the recorded resolution. A coarse zoom-11 bake over 64 km feeds the
  far skirt and the terrain-class/maritime context; v1's global 16 MB grid (`Data/global/`) serves
  `TerrainContext`, `TerrainClassifier` and `MaritimeMap`.
- **Layers, computed once at world creation and saved** (`WorldLayers.Compute`, landed 2026-09-09, M1.2): the
  lakes first (the ground's one-level flats with a rim, and the outlines of the bake's `water_bodies` layer from
  OpenStreetMap, a mapped lake standing at the median of the bake's ground inside its outline); the drainage
  with the sea and the lakes as sinks (`DrainageNetwork`: priority-flood fill, D8 by gradient, catchment); soil
  depth and wetness from the filled surface (`SoilModel`); the water classes with the fresh flag and the surface
  layer (the sea at the datum, a lake at its level, a creek or stream its ground plus the water its catchment carries by
  the flow's law, `WorldLayers.ChannelDepthM`, since WG.1 of 2026-09-18; else the ground); the landforms (the topology bitmask: sea,
  beach, dune, cliff, platform, water, crest), which the plants read; the plant community (`PlantSpecies`,
  `PlantCommunity`: suitability product, dominance², a tall plant on a cell as often as the ground suits the best of
  them, overstory then understory, drawn by the seed; since M1.2b no soil a root can use on a beach, and on a dune
  the soil model's depth less the salt wind's share), which adds the forest and the heath to the topology; the
  stone by topology and the province lattice (`GeologyScale`); animal capacity (`AnimalCapacity`);
  the distances the wake scorer asks about and its score. The sea's floor is a stated rule until the bathymetry
  arrives: one in twenty from the shore, to 30 m. **No landform evolution on real data** (decision 2026-09-07:
  the valleys in a 5–30 m DEM are already the record of what water did; v1's erosion existed to replace a
  9.8 km/px gradient). `LandscapeEvolution` is ported but dormant.
- **Generation inputs must agree:** `WorldLayers.Compute` validates the primary height layer and its optional
  mapped-water layer before doing generation work. [WG.0b](contracts/WG.0b_WORLD_INPUTS.md) owns the compatibility
  rules: mismatched maps are refused with the field and both values, not combined by array index. The existing
  raster format is unchanged; this validates a set of inputs, not a new file layout.
- **Layer format:** `eg2.raster` version 2 (§10): a raw grid and a JSON sidecar (name, layer, dtype, scale,
  unit, shape, frame, source, checksum), written by the Python bake and by `RegionRaster.Write` in the pipeline
  and read by `RegionRaster`; the loader's tests pin both writers to one fixture by checksum. The world folder's
  layers and their dtypes are §10's table, the ground's cover among them since M1.4d.
- **The wake point** is chosen by the world-creation pipeline (`WakeScorer`) scoring every standable cell
  against the criteria (fresh water 500 m, knappable stone 1 km, fibre and firewood 500 m, shelter rock 2 km),
  each graded from the thing itself to three times its distance so that the product has one best place rather
  than a plateau; a cell is no place to wake when it is sea, within 500 m of the region's edge, under 2 m,
  steeper than ten degrees, water underfoot (a lake, a swamp, a creek), sodden ground or the shelter rock
  itself; ties go to the least wind exposure. The winner is written into `world.json`; the same scorer read out
  in words, with the water read out by name, is the census (`census.txt`).
- **What the ground looks like** is two facts with one owner each, one on either side of the wire:
  `GroundCovers.Of` in the engine says what covers a cell (the world's `cover` layer, M1.4d), and `GroundPalette`
  in ClientCore says what that cover looks like. The terrain's colour, and later grass, litter and impostors, ask
  those two and nothing else. v1's `GroundColourAt` was one function; the split is the price of the wire (§8).
- **Climate and weather** (M1.8a, `contracts/M1.8a_THE_WEATHER.md`; M1.8c, `contracts/M1.8c_THE_LIGHTHOUSES_OWN_RECORD.md`):
  `Climate` is the day under the weather at a place and an hour, from a weather station's own table less what its fronts
  add to a day's extremes: Point Perpendicular Lighthouse (068034) for Bherwerre, the Bureau's all-years table as
  `Data/stations/` holds it since 2026-09-16, pre-human by the owner's rule with the warming taken off as the trend's
  value at the middle of the table's years (`Climate.WarmingInsideRecordC`), coldest at dawn. The wind is the station's
  9 am and 3 pm means by month brought to a founder's height by the log profile and read through the day as a night floor
  and an afternoon peak; the dew point is the station's month, and the humidity is read from it against the air.
  `Synoptic` is v1's seeded index of fronts and the showers inside them, its rain, its cloud's year and its cold snap's
  depth solved against the lighthouse's own table by the almanac tool's `+fit` (which calls the engine's formulas with
  candidates through `RainSettings` and `CloudSettings`, so the constants stay the one owner); `Weather.At` assembles
  the sky from both, once, and it is the sky, not the curve, that answers to the station. The weather is a function of the
  world's seed, its region and its clock, so a client works it out from what its Welcome carries and nothing of it is sent
  or saved; `WorldState` builds its climate when first asked, since a test fixture's region has no station record. Nowra
  (068072) is read and kept beside it: the same open wind, colder nights and warmer days 29 km inland, an inland gradient
  the region does not yet carry (DEBTS).

## 4. Time and ticking

- `WorldClock` owns in-game time: UTC hours since the epoch, local solar time derived per longitude; 30 real
  minutes per day at 1×. The server's fixed step drives it and nothing else does.
- **Fast tick** 20 Hz through `FixedStepAccumulator` (a stall beyond 5 steps is dropped and counted, never caught
  up). The client runs its own mover at 50 Hz and samples mouse look every render frame; the body is
  interpolated for rendering.
- **Slow layers** expose `AdvanceTo(worldTime)`; unloaded regions catch up analytically on load.
- **Pause is a server concept**: SOLO pauses the server; HOST and DEDICATED cannot pause. The transport is pumped
  while paused so pings are answered and late joiners refused cleanly.
- Sun, weather and animal presence are functions of continuous time (v1's synoptic law).
- Determinism budget: floats, same-architecture assumption, seeded `SimRandom` (xoshiro256** + splitmix64,
  `DeriveSeed` by FNV-1a); bit-determinism across platforms is not promised because authority replicates state.

## 5. Entities, definitions, items

- The engine owns an entity store (`EntityStore`, landed 2026-09-09, M1.3): 64-bit ids allocated in order and
  never reused (the next id is state, saved and restored), a definition, position and yaw in local metres, and
  plain-struct components (`ItemComponent` first, `AnimalComponent`'s pose since M1.7a); an animal stood up from
  presence (M1.7a) takes an id of a reserved range, the top bit set, made from what it is, and is held apart from the
  world's own entities, never saved and never named by the world's digest; entities kept in id order so every walk is the same walk;
  lifecycle Spawn → Init (the end of the tick after the spawn) → Kill (removed at the end of the tick, kept
  for the server to tell its viewers). Every field that can change is stamped with the tick it changed at, and
  a viewer reads what it has not seen off the stamps against the tick of its own last send: dirtiness is per
  viewer, never a flag cleared for everyone, so a session behind on its byte budget is sent what it missed.
- Definitions (`Definition`, `DefinitionCatalogue`): a stable string key (`plant/blackbutt`, `stone/silcrete`,
  `animal/eastern-grey-kangaroo`, `item/cobble`, `player`; lower case, one slash, hyphens between words) hashed
  FNV-1a 32 at load, the catalogue refusing a collision or a malformed key; built from the tables that exist plus
  the items and the player; each states its kind, whether it is spawnable in M1 (the items; a tree stays a layer,
  M1.6a, and an animal is stood up from presence rather than spawned, M1.7a) and an item's mass and radius, which
  live nowhere else. The id travels in the spawn
  message. What an item looks like is the client's `ItemLooks`, the stand's own mesh for it (§8; M1.5a, 2026-09-11,
  when M1.3's prefab registry and its placeholder prefabs went), and the edit-mode `ItemLooksTests` asserts every
  spawnable definition has one.
- The fast tick's systems run in the order `WorldState.Systems` lists (`ItemFall` first: a dropped thing falls at
  9.81 m/s² until it meets the ground the server holds, the sea's floor under the sea, and rests; then
  `AnimalStandUp`, M1.7a); the slow
  layers (`ISlowLayer.AdvanceTo`) are advanced by `SlowScheduler` per 512 m cell (`RegionCells`, cell (0, 0) at
  the south-west corner): a cell within the interest radius of any player every tick, a cell farther away every
  60 ticks on its own phase, catching up in one call, the layer analytic over the gap.
- Fauna: the presence function is the source of expectation (M1.7a, 2026-09-13; this line used to stand animals
  up to each player's interest radius and simulate them). Once a second the server stands up, member by member, the
  kangaroo mobs and oystercatcher pairs presence puts within 500 m of a founder (`AnimalStandUp`), each 100 m square
  drawn at the mean of the world's own capacity layer over it (`CapacitySquares`, worked out when the world loads);
  moves them where presence has them now, off the water and resting or grazing by the hour; and takes them away
  when no founder is within 550 m. Since M1.7c (2026-09-14) a standing group notices founders: every step, a group any
  of whose members a founder has come within its kind's distance of (`AnimalFlightRules`: 80 m a mob, 60 m a pair, the
  measured distances of animals nobody harms) runs straight away from that founder, its members keeping their places
  and moved every step, at its speed (7 m/s a mob, 15 m/s a pair) for its length (150 m, 200 m; design, DEBTS), keeping
  to dry ground by turning along a shore or the edge; stands a while; walks back to where presence puts it; and is
  presence's again. What a group is doing is its offset from presence's place, held by the stand-up while it stands and
  forgotten when it is taken away: nothing of it is saved or digested. A founder who comes near again sends it off
  again. The rules are per kind and a developer's settings move them (the panel's sliders, M1.D). Since M1.7b
  (2026-09-16) a developer's hand can set one animal down to be looked at (`AnimalStandUp.SetDown`, the panel's deeds):
  it takes an id of the reserved range that names no kind (`ByHandKind`), which presence can never reach, the refresh
  keeps it where it was put instead of taking it away, and it is transient like the rest — presence does not own it, so
  it never wanders and never takes flight of its own, and its pose is the panel's (`PoseSetDown`). What an animal looks
  like is the client's (§8).
- **What stands and lies on the ground is a layer until something moves it** (M1.6a, 2026-09-11; this line used to
  make the trees inside the radius entities). The world places every tree, stick and cobble when it is created and
  saves them as two layers, `stand` and `loose` (§10), which travel as code tiles like the cover and are drawn by the
  client (§8). A generated thing becomes an entity only when something moves it — a stick picked up, a tree felled,
  M1.5b for the sticks and cobbles — and what is taken is kept in the region files' layer diffs (§6). The numbers
  that retired the old line: the interest radius is 1,500 m, about 7 km² round each player, and the nine tiles round
  the four vantages of M1.6a's frames hold 103,850 to 176,906 trees; every entity inside the radius goes out in the
  join snapshot with no byte budget, `EntityStore.Within` walks every entity for every session every tick, and the
  client makes a GameObject for each. Grass and litter are layer-derived instances too, never entities.
- Dropped items rest where the server says; the client draws the fall between the states it is sent (§8).
- **The founder's body: water** (FP.1, `contracts/FP.1_THIRST_AND_WATER.md`, 2026-09-15). `Hydration` (engine) is the
  water part of v1's `BodyState`: 42 litres in the body, 2.4 lost a day at rest, the words at 1.5%, 4%, 7% and 11%
  lost, the work capacity by the published table, a litre and a half a drink. Each `PlayerSession` holds one; the
  server advances every founder's each step by `WorldClock.DaysFor(stepSeconds)`, so the body runs on the world's
  clock (a held clock holds it, a sped one dries it); it is saved as the fraction lost (`eg2.player` version 4) and
  restored at the join. The client is told its own founder's water (`FounderState`, protocol 13: once a second, and
  at every drink, setting and join) and takes the word and the capacity from the engine's own tables, so no reader
  holds thresholds of its own. The verb `Drink` names a point as a put-down does; the server judges the reach and
  the region, then the water there by `WorldState.WaterAt` (the surface read between posts as the client reads its
  streamed depth, and the class of the nearest post of the cell that is wet by its own depth, since 2026-09-18: until
  then the nearest water-class post, which at a creek's mouth served the sea as fresh): a creek, a stream or a lake
  gives, the sea answers `Salt`, anything else `NoWater`. The client's aim (`Drinking.Stands`) reads the same way. The work capacity is the body's,
  not the config's: `Mover.Step` takes it beside the config, and the validator's ceiling is `MaxHorizontalSpeedAt`
  the capacity the client was last told (the greater of the last two told), never the server's newer number, so a
  client is not corrected for a number it has not received.
- **The founder's body: warmth, and death** (FP.2, `contracts/FP.2_THE_NIGHTS_COLD_AND_DEATH.md`, 2026-09-16). `Warmth`
  (engine) is v1's heat balance ported term by term, its numbers published physiology: 70 kg, 1.8 m² of skin, 80 W at
  rest, 180 more walking and 420 running, up to 350 W of shivering for three hours' worth (spent in proportion to what is
  delivered, so the reserve decays exponentially), the tissue's insulation 0.3 to 0.9 clo as the cold constricts it, the
  still-air layer on bare skin thinned by the root of the wind, the clear sky 16 K colder than the air with cloud closing
  the gap (`Climate.ClearSkyDepressionK`), Fanger's breath, the Meinel beam on the body's projected area
  (`Climate.DirectSolarWm2`, `DiffuseSolarWm2`), sweat shedding a surplus and charged to the water. The founder is naked,
  standing or walking in the open, awake: shelter, fire, clothing, bedding, the ground and sleep are later beats' terms
  and add to the balance. `Surroundings` is one reading of the air, wind, cloud, humidity, sun and sky view;
  `WorldState.SurroundingsAt` makes it from `Weather.At` at the body's height with the wind for `ExposureAt` (the layer's
  openness rule read from the terrain at runtime, the openness not being saved, floored by the salt wind within a
  kilometre of the sea from the saved shore-distance layer, which the world now holds: a beach lies below its dunes and
  would otherwise blow as a hollow) and the sun's elevation. The server reads a
  founder's surroundings once a second and runs the warmth every step at the exertion the body's speed says
  (`Warmth.ExertionOf`); the water's loss is the resting 2.4 L a day plus the breath's water above rest (the balance's
  own latent respiratory term in litres, `Warmth.BreathWaterLPerHour`) and the sweat (`Hydration.Advance`; until
  2026-09-16 v1's multiple of the whole resting loss, which had a walking founder dead of thirst in fourteen hours
  where the design says days). The words:
  chilly under 36.7, cold under 36, hypothermic under 35, severely under 32; death at 28, or at 15% of the water lost,
  judged in the step (`GameServer.AdvanceFounders`) whatever moved the body there, the panel's row included. Death is
  the owner's Standard mode (2026-09-01): `Hands.LetGoOfEverything` where they fell, a new full body stood at the wake by
  a correction, `Died` (protocol 14) to the client with the numbers, and the one sentence made by `Death.Explain` for
  the screen, the game's log and the host's `death` record alike. At Bherwerre's shore an ordinary late-winter night
  under M1.8a's weather left a founder who stood still hypothermic in the small hours and alive at dawn (lowest core
  28.7 °C); under the lighthouse's own record (M1.8c, 2026-09-16), a degree milder but with the coast's night wind, the
  same night kills the standing founder at about dawn, and the third night run's founder in the forest behind the beach
  died at 05:47 under the earlier weather already; one who walks is never cold; a front's night (4 °C, 7 m/s, clear)
  kills a standing founder in the small hours and a walking one by morning: the path's canon 03:09 death was v1's,
  inland at 687 m and lying down.
  **The beta arc's bridge** (CANON ruling 33, 2026-09-16): while fire and shelter do not exist, `ServerConfig.BetaArcBridge`
  (on for every game; `-eg-no-bridge`, `+server.bridge 0` take it down) has the step hold a core past the lethal at
  `GameServer.BridgeCoreC`, a hair above it, instead of the death: the body cools, the words come down to severely
  hypothermic, and the sun brings the core back at dawn; thirst still kills. The bridge is the server's, in the one place
  that judges death, so the body's balance knows nothing of it and comes down with it (DEBTS).
- **Verbs and carrying** (M1.5a, 2026-09-11). A verb is an intent the client sends (§7); the server commits it on
  the world it holds and answers. A founder's `Hands` (engine) are nine places, one to each of the keys 1–9, one of
  them the hand. A thing picked up leaves the world: its entity is taken (`EntityStore.Take`: gone at the end of the
  tick as a killed one is, its viewers told it was taken up) and its id, key and spawn tick go into the first free
  place, and into the hand when the hand is empty. A thing put down comes back with the id it always had
  (`EntityStore.Return` refuses an id the store never allocated or still holds), let go 0.3 m above the ground where
  the founder aimed, and falls from there. So the world holds only what lies in it, and what a founder carries while
  away is in their player file (§6). Reach is 4 m from the eye, a thing's radius beyond it for a pick-up (v1's
  reach), and the eye's height, 1.65 m standing and 1.1 m crouching, is the engine's (`MoverConfig.EyeHeight`),
  which the client's camera reads too: what the server allows is what the camera can touch. While the world is held,
  or before a founder has a body and a snapshot, nothing is done, and a thing taken up in a tick cannot be put down
  until the tick ends.
- **Taking what lies** (M1.5b, 2026-09-11). A stick or cobble of the loose layer is named by its place (`LyingThing`:
  the cell's row and column, the kind, the index), which is how `StandLayout` already put it: the server finds where
  it lies from that alone (`LyingThings`), and so does the client. What is taken is kept beside the layer, which never
  changes, as a bit for each index of each cell (`LooseTaken`), never by lowering a count, which would take the cell's
  last thing rather than the one looked at. A thing taken goes straight into the hands under an id the store allocates
  (`EntityStore.AllocateId`) and is an item from then on: put down, it is an entity like any other, and nothing goes
  back into the layer. A cobble becomes the cobble of the stone the world's stone layer names on its cell
  (`DefinitionCatalogue.CobbleOf`: `item/cobble-silcrete` and one for every stone, weighing by the stone's density),
  the plain cobble where none is named.
- **Stone and knapping** (FP.3, `contracts/FP.3_THE_FIRST_STONE.md`, 2026-09-16). `Knapping` (engine, `Materials/`) is v1's
  fracture mechanics ported term by term: Auerbach's law gives the energy a blow must carry to start a cone crack in the
  core, 2825 times the core's fracture toughness times the hammer's radius squared, so a small hammer does the fine work; the
  cone's angle is fixed, so what a harder blow buys is a bigger flake (0.0022 kg a joule above the critical, between a floor
  of 0.010 kg a kilogram of hammer and a ceiling of 0.22 of the core) and a coarser one (the stone's own `EdgeQuality` docked
  0.35 for a blow four times the critical); past 40 J a kilogram of core it shatters; a stone under 150 g drives no cone; a
  stone whose `Knappability` is under 0.25 (sandstone, granite) crumbles and never takes an edge; every flake works the
  platform toward ninety degrees, and a dead platform crushes until the core is turned (`StoneCore.Turn`, which no verb
  reaches yet). The swing is the founder's: 1.4 to 6.8 m/s by the wind-up, under an arm's ceiling of 40 J, and less what
  thirst has taken of the body's work (`Hydration.WorkCapacity01`). The stones as items: a thing's mass is its definition's
  until a blow gives it one of its own, kept on `ItemComponent` (its own mass, its edge, a core's platform angle and flakes
  taken; zero for none, so everything written before reads as it was), read and written by `KnappingItems` alone, carried
  through the hands (`CarriedThing.Item`), the wire (protocol 15), the region file (version 3), the player file (version 6)
  and the digest (only when the thing has state of its own). A flake is a definition of its stone (`item/flake-<stone>`,
  `DefinitionCatalogue.FlakeOf`), spawnable, weighing what the blow took. The plain cobble, of no named stone, can be swung
  but not knapped (`KnappingItems.IsHammer`, `IsStone`); the panel's deed `spawn.silcrete_cobble` sets down one that knaps.
  The verb `Knap` (5) names the core (an item, one of the litter, or a place of the hands) and the wind-up as a byte; the
  server (`GameServer.Knap`) judges the hammer in hand and the reach, strikes by `Knapping.Strike`, writes the core's state
  back to wherever it is (or kills a spent item, drops a spent held core, takes a spent litter cobble from the layer), turns
  a litter cobble a blow changed into an item where it lay (M1.5b's rule for a thing moved), lets the flake fall beside the
  core, a step towards the founder and to alternate sides, or at the feet off a held core, and answers with the physics'
  own words on the `IntentResult`, which the verb line shows. The client's `VerbController` measures the work button's hold
  against a second for the wind-up and offers the blow on the stone aimed at with a stone in hand; `HandView` draws the hand
  back as it winds and a flake pressed flat.

## 6. Save and world folder

WG.0c (2026-09-14): numeric JSON seed, tick and next-entity-id fields are read as exact integers. The JSON reader
preserves whole-number tokens as signed/unsigned 64-bit integers; measured quantities
still use `Number`. Exact integer access requires integer tokens and refuses overflow, fractions and decimal/exponent
spellings rather than accepting their rounded values. The writer's numeric field spelling and `eg2.world` version 1 are unchanged.
An old save with its original integer text is repaired by reading it correctly; a seed rounded and resaved by an
older build cannot be inferred back. `SavedWorldLayers` checks every promised layer against the world's manifest
and region/frame before restoration. Optional unpromised legacy layers may be absent. Layers read for runtime
use are checked as they load; the remaining promised layers are checked and released, with cancellation between
layers. `WorldCreation` checks the primary bake against the requested region before it computes or writes.

`Saves/<world>/world.json` (seed, region, extent, wake point, created, protocol version), the baked layer rasters,
`regions/r.X.Y.egr` (one file per 512 m cell: entities and mutable layer diffs; atomic write), `players/<id>.egp`
(server-side), `digest.txt` (the world digest the tests diff: `WorldDigest`, every number a whole count of micrometres or nanohours, rounded to even, the lines sorted and hashed with FNV-1a 64; integers rather than a printed double because the player's Mono and the server's .NET print the twelfth significant figure of some doubles differently, as the first full corpus found on 2026-09-08). The server is the only writer.
Additive fields with initialisers; every persisted field enters the digest in the same commit. Solo worlds are
byte-identical to server worlds.

Landed 2026-09-08 (M1.A): `world.json` as format `eg2.world` version 1 (region, seed, extent, created and saved
dates, protocol version, tick, the clock's two numbers) and a player's resting place as `players/<name>.json`
(position, yaw, pitch, grounded, tick). The binary `.egp`/`.egr` files arrive with the entity store (M1.3); the
player file moves to `.egp` then, as a versioned change of format, not a quiet rename. `WorldSave` in
`EarthGame.Server` is the writer.

Landed 2026-09-09 (M1.2): `layers/` under the world folder, every layer of §10's table in the version-2 raster
format with a sidecar naming its source, and `census.txt` beside them; `world.json` gains the additive keys
`wake_east`, `wake_up`, `wake_north` and `layers` (layer name → the raw file's sha256), carried forward by every
later save. Once the folder exists the server reads its terrain from the folder's `heights` (the bake's ground
with the sea floor); the bake is needed only to create, and `water_bodies` beside it when the region has one.

Landed 2026-09-09 (M1.3): `regions/r.X.Y.egr` (format `eg2.region` version 1, one per 512 m cell, the entities
whose position lies in it; a cell's file is removed when it holds nothing), `players/<name>.egp` (format
`eg2.player` version 2, binary, with wading and stance, which version 1's JSON dropped and the round trip's
digest needs; version 1 is still read and is deleted when its version-2 file is written), `digest.txt` at every
save, and `next_entity_id` in `world.json` (additive). The digest gains every entity in id order and the next
id. `RegionSaveTests` holds the save law by sabotage: each persisted field flipped in turn changes the digest.

Landed 2026-09-11 (M1.5a): the player file moves to version 3 with the hand and what is carried (each thing's
place, id, key and spawn tick); version 2 is still read. The digest names what each founder carries after the
bodies, and gives no line to a founder with nothing and no hand chosen, so a world saved before carrying keeps its
name; it leaves out an entity killed or taken up in a tick not yet ended, as the region files do. A folder in which a
carried thing also lies in a region file, is carried twice, or carries an id the world has not allocated is refused
when read.

Landed 2026-09-11 (M1.5b): the region file moves to version 2 with its layer diffs — what was taken from the loose
layer, by raster cell, in the file of the 512 m cell the raster cell's centre lies in; version 1 is still read. The
digest names each cell something was taken from, after the entities. A save whose takings name a thing its cell never
held, or keep a cell's takings in another cell's file, is refused when the world is restored; the server reads the
world's stone layer with the others, for the stone a cobble taken up is made of.

Landed 2026-09-13 (M1.3b): a save is whole or not there. `WorldSave.Write` writes every file it changes beside its
target as `.part`, each flushed to the disk; then `save.commit` (§10), the record of what it puts in place and what it
removes, written beside itself, flushed and moved in whole; then puts the files in place, replacing any that stood, and
removes the files of cells that emptied; then removes the record. Before the record is placed the previous save is
untouched. After it, `WorldSave.Recover`, which `Read` and every `Write` run first, finishes the save, knowing a file
already in place by its CRC-32, and refuses by name a file whose bytes are not the record's; with no record, it clears
what was written aside. `Exists` counts a folder holding the record as a world, and so does the game's menu. Until then
each file was moved in on its own, the old one deleted first: a crash could lose `world.json`, and the folder became a
place to make a new world in, or leave a thing both carried and lying, and the whole folder was refused.

Landed 2026-09-13 (M1.3c): the game writes its autosaves behind its main thread. `WorldSave.Prepare` makes a save from
the world and the players as they stand, every file's bytes and the names of the files it removes, and
`PreparedSave.Commit` writes it as above, once; nothing done to the world after the making reaches it. `Bootstrap` makes
each autosave between two frames and writes it on a worker, one at a time: an autosave that comes round while the last
is still being written is left for the next, and closing the game waits up to half a minute for the one being written,
then writes its own on the main thread. A file operation of a save or a recovery that another program holds up is tried
again, each wait longer than the last, before the save fails naming the file, for the next load to finish. The dedicated
host saves in place on its own thread, which draws no frames. Until then the whole save, its flushing among it, stood
between two of the game's frames.

Landed 2026-09-14 (M1.3d): a world folder is held by the program that opened it. `WorldPreparation.Load` takes
`world.lock` in the folder before it reads anything: a file kept open, shared with no one and deleted when it is closed.
A second program, the game opened twice or the game beside a host, is refused in plain words before its recovery of the
folder could delete what the first program's save has written aside. A load that fails lets the folder go; the game lets
it go once it has written its closing save, the host once it has written its last, and a program that dies lets it go
with its files. A founder's file name sets capitals aside as Windows does: a name whose file would be another known
founder's, William beside william or Jo Jo beside Jo_Jo, is refused at the door, and a save that would write two founders
to one file is refused before it writes anything. A name Windows keeps for a device takes an underscore in front of its
file's name.

## 7. Networking

- **Rule 1 — there is no single player.** SOLO = server + client in one process over `InMemoryTransport`; every
  message serialised and queued; no fast path.
- **Rule 2 — the server owns the simulation.** Clients predict visually, never commit.
- **Rule 3 — split by assembly, not `#if`.** **Rule 4 — engine-free core, exploited** (the server is tested by
  dotnet). **Rule 5 — dirty masks and interest management from day one** (a radius filter for ≤ 8 players; a
  grid later; a per-connection bandwidth budget; baseline streaming on join by proximity).
- **Transport:** `ITransport` (`Update(elapsedSeconds)`/Poll; reliable and unreliable delivery; a peer-stated
  close reason is flagged `ReasonFromPeer`; every connection counts its payload bytes both ways) with
  `InMemoryTransport` and LiteNetLib over UDP (vendored, MIT; its loss/latency simulator is the N1–N4 harness,
  always compiled with `SIMULATE_NETWORK`). `UdpOptions.SendCapBytesPerSecond` is a token bucket per connection
  with a quarter-second burst (never smaller than one 64 KB message), refilled by the caller's elapsed time: the
  harness's stand-in for a real uplink (CANON ruling 11). `UdpTransportBase.Sever()` drops the socket without a
  word, the N3 cut. A socket binds every address only when it is meant to be reached from off the machine (a game
  hosted for friends, a dedicated server): an end for this machine binds the loopback address alone
  (`UdpOptions.LocalOnly`: the host's `+server.local 1`, the game's `-eg-local`, and a client joining a loopback
  address takes it by itself), which is what keeps Windows' firewall from asking after every fresh build
  (M1.Ba, 2026-09-14). Since M1.Bb (2026-09-16) that is the default and the exception is the flag: `UdpOptions.LocalOnly` is true unless
  a game is hosted for friends or a dedicated server is told `+server.local 0`, and the test suite binds no socket on
  every address, reading the transport's choice (`UdpTransportBase.BindAddressFor`, `BindsLocalOnlyFor`) instead.
- **Handshake (protocol v1):** the first message must be `Hello` (protocol version, player name, password); the
  server answers `Welcome` (session id, seed, region, time, tick, tick rate) or `Refused` (reason) and closes with
  the same reason. A malformed packet is a refusal, never a crash. Over UDP a close can overtake the `Refused`
  message, so a client in the handshake treats a peer-stated close as the refusal.
- **Movement:** the client runs the engine's `Step()` locally and sends `{tick, input, resulting state}`; the
  server validates (max speed × dt × tolerance, where the speed of a founder off their feet is a run's together with
  what the height lost since they last stood gives, M1.5f; heightfield ground clamp; penetration probe), logs violations and
  corrects with a sequence number. Inputs are on the wire from day one so server-side simulation with client
  prediction is a later switch. A move reported while the server is paused is neither accepted nor corrected,
  so a held tick holds the bodies. The interval a report is judged over is its sequence spacing (the client
  sends one report per tick interval of its own simulated time), bounded by the real time the session has banked
  since the reports it accepted (`MovementRules.MoveCreditCapSeconds`, five seconds): jitter that bunches two
  reports into one server tick does not double their speed, and a report that claims time the client did not
  have is judged over what really passed. A report older than the newest accepted is stale and ignored.
- **Streaming on join (protocol v3, M1.B):** the Welcome carries the region's extent; the client lays out the
  fixed tile grid from it (`TileGrid`: kilometre tiles when the extent divides into kilometres, else the whole
  region as one tile; tile (0, 0) at the south-west corner) and asks for the nine tiles around its spawn with a
  `TileRequest` that names the checksum of any copy it holds. The server answers each with a `TileHeader` (posts,
  cell, origin, byte length, CRC-32, chunk count; zero posts means no ground to serve; zero length with a
  checksum means the copy is current) and reliable, ordered `TileChunk`s of 16 KB. A tile is int16 centimetres,
  row-delta, deflate (`TileCodec`; heights beyond ±327 m refused); the client checks the CRC, unpacks, stores
  the bytes on disk by checksum (`DiskTileCache` under `Saves/tiles/<region>/`), and raises the tile. The coarse
  region and the 64 km skirt stay the bake on disk, stated as such. Since M1.4b (2026-09-10) the client asks for
  three layers of each of those tiles — the ground, the water's depth over it, and the water's class — and a
  layer the world has none of is answered with a zero-post header once, after which the client stops asking for
  it. A client keeps at most `TileReceiver.MaxTilesPerLayer` (25) tiles of each layer, letting the farthest go
  first and never the tile under the founder or the eight around it; a tile let go stays on disk, so walking
  back to it costs a header and a read rather than the wire, and the client forgets having asked so that it
  asks again. Since M1.4f (2026-09-14) a tile let go of frees the Terrain data and the water mesh it drew from,
  which destroying their GameObjects had left behind, and the coarse ground shows again where it stood; every
  Unity object the client makes is freed through `UnityObjects.Free`. The far layers (M1.6d, 2026-09-13) are the exception: the client asks for the far stand and the far
  count of every tile of the region at the join, after the nine tiles round it, in requests of at most
  `TileRequestMessage.MostTiles` (64) tiles, and keeps them wherever the founder walks (`TileLayers.IsFar`).
  **Interactive keeps the meaning CANON ruling 11 gave it**: the ground under the founder, the snapshot, the
  tile built. The water and the far layers travel beside the ground and gate nothing, so N1 measures the same thing as before. The client's ground (`TileHeightfield`) reads
  the tiles bilinearly exactly as the server reads the raster, and is NaN where no tile is held.
- **Snapshot and interest:** the Welcome is followed by a `PlayerState` for every body the joiner can see and a
  `SnapshotEnd`; after each tick the server sends a body only to sessions within `ServerConfig.InterestRadiusM`
  (1500 m; a session without a body yet sees everything); a session's end is a `PlayerLeft`. The client keeps a
  `RemoteMirror` per remote player (states by server tick) and draws it a stated delay (three ticks) behind the
  estimated server tick, interpolating between the two states around it and holding at the newest beyond them.
  *Interactive* (N1) is: the nine tiles answered, the snapshot applied, and the tile under the founder built.
- **Intents (protocol v6, M1.5a, 2026-09-11):** a verb travels as `Intent` (a sequence, the verb and its target: an
  entity to pick up, a point to put down at, a place to hold) over the reliable channel; the server answers
  `IntentResult` under the same sequence (done, or why not), and sends the hands to their owner alone as `Carrying`
  (the hand, and each place's thing by id and definition) at the join before `SnapshotEnd` and after every verb that
  changed them. An entity taken up is `EntityGone` with reason 3 to everyone who held it; a thing put down is an
  ordinary spawn under its old id.
- **Takings (protocol v7, M1.5b, 2026-09-11):** a pick-up can name a thing lying by its place, and the server tells
  every client what has been taken from the loose layer as `LooseTaken` (cells and their masks): all of it to a joiner
  before `SnapshotEnd`, and each taking to every client as it happens. A client adds what it is told to its own
  `LooseTaken` and draws a cell less what was taken from it; the layer's tiles, and every client's cache of them, stay
  as the world made them, so no tile is sent again.
- **The first stone (protocol v15, FP.3, 2026-09-16):** an item's component carries the state a blow gave it on every spawn
  and state that carries an item (its own mass, edge, platform and flakes taken; zeros for a thing with none of its own);
  `Intent` has a fifth verb, knap, naming the core as a pick-up names its target or as a place of the hands, and the wind-up
  as a byte; `IntentResult` carries the words for what the stone did, made once in the engine's `Knapping` and shown by the
  client as they came, so the screen never holds a second copy of them. A spent core that was an item is `EntityGone` with
  reason 1 (it died); a litter cobble a blow changed is `LooseTaken` to every client and spawns as an item where it lay.
- **A developer's settings (protocol v10, M1.D, 2026-09-14):** `DevSetting` (a name and a number) travels reliably from
  a client to the server, which, started for development (`+server.dev 1` on a host, and every SOLO game since M1.E:
  `MovementRules.AllowFlight`, the one mark of a server that may grant developer mode), holds it to its range and applies
  it, or refuses one it does not know; any other server refuses it and closes, as it does a malformed message. Since
  M1.E (protocol v16, 2026-09-20, CANON ruling 39) the mark says who *may* have developer mode and `DeveloperMode` says
  who does: the client asks for it on or off (F2) and the server answers with what it granted and whether it refused —
  a server without the mark refuses and keeps the player, F2 being a key every game answers. A development server takes
  a setting, and lets a founder fly, only from a player whose switch is on (`GameServer.MovementRulesFor`); one whose
  switch is off is held to the rules without flight and a setting from them is let be rather than closed on, since one
  can be sent a moment before the switch's own answer lands. `DevSettings` in the protocol package is the one table
  of what exists (standing at the wake as a deed; the local hour and the day of the year; how fast the clock runs,
  `WorldClock.Scale`, nought holding the sky still; the animals' stand-up and take-away distances; a stick or a cobble
  set down two metres ahead as deeds), read by the panel for its sliders and by the server for what it takes. `Pong`
  carries the server's clock and, since protocol v11 the same day, its scale: a client's own clock, which runs from its
  Welcome, runs at that scale and is set to the server's when it slips by more than 0.002 h of the world's time (about seven world-seconds; M1.D's first landing allowed three minutes),
  so a clock a developer moves moves every client's sky.
  The founder's water slider has a floor a hair above the lethal loss (2026-09-22): dragged past it, it killed the founder
  and then each new one the Standard death woke, since the slider stayed where it was left.
- **Rejoin:** a session that ends keeps its body and what it carries by player name for the life of the server (and in the world
  folder on save); the same name wakes there, and a Hello for a name still connected supersedes the old session
  (the transport had not yet noticed the cut). A name that would share a player file with another name the server knows,
  remembered or connected, is refused, naming both (M1.3d). **Digests** (`WorldDigest`, ARCHITECTURE §6): FNV-1a 64 over
  lines of fixed-resolution integers (micrometres, nanohours), sorted by key; the world digest names the clock, the tick and every
  body by name; the bodies digest names one body by session id, computed the same way by the server's record
  and the client's mirror, so N3 and N4 compare strings.
- **Measurement in the server:** `GameServer.Ticks` (a `TickStats` window: count, overruns, mean, max, p95 of
  each host update that released a step, timed by the clock the host passes in; the server reads none),
  `TileService.BytesServed`, and each connection's byte counters. The host writes them to its run log (§10).
- **Hosting:** "Host game" listens on a port; join by IP:port with an optional shared password in `Hello`
  (LiteNetLib has no encryption — friends only); Tailscale for CGNAT; `EarthGame.ServerHost` is the dedicated
  console (`+server.port`, `+server.password`, `+server.maxplayers`, `+server.seed`, `+server.region`,
  `+server.simulate.latency/jitter/loss`, `+server.sendcap`, `+server.log`, `+server.seconds`; `status`, `pause`,
  `resume`, `digest`, `stop` on stdin). The Unity client takes the same shaping for its own socket
  (`-eg-latency`, `-eg-jitter`, `-eg-loss`, `-eg-sendcap`; `-eg-dune slide` runs the dune scenario's slide part alone for a
  founder a tool has stood below a steep face, 2026-09-22) and runs a scenario with `-eg-scenario` and
  `-eg-record` (`ScenarioRunner`: the founder driven along `Routes.WakeLoop` by a `RouteFollower`; `join`, `walk`,
  `soak`, `rejoin`; the first-frame recorder stays). `Tools/corpus/run.py` is the harness (§7.1's conditions,
  the held-tick pause driven from the player's own log; since 2026-09-10 every scenario runs on its own copy of a
  world the harness creates first, so founders wake where the scorer chose and walk `Routes.WakeLoop`, laid round
  that wake) and `Tools/verifiers/checks/join_check.py` recomputes
  N1–N4 from the logs.
- **Plan B (true cost):** FishNet 4.7 requires the server to be a Unity process; adopting it replaces
  `EarthGame.Server`'s replication and its dotnet tests and forfeits the pure-.NET dedicated server. Not taken:
  at the checkpoint the owner kept the hand-rolled netcode (CANON ruling 19, 2026-09-10).

### 7.1 The M1.B checkpoint criteria (owner ruling 11, ratified 2026-09-07 with three amendments; decided 2026-09-10)

"Healthy" has no adjectives. The decision is made against N1–N4 and nothing else; if any fails and cannot pass
within one further contracted week, plan B is taken. Sunk cost does not vote. Each is measured by a scenario the
corpus runs, its raw results in the contracted run logs (§10), recomputed independently by
`join_check.py`. The owner decided on the corpus of 2026-09-08, every row green: the hand-rolled netcode stays
(CANON ruling 19). Later slices go on measuring N1 under the same conditions (`Tools/world/stream.py`).

- **N1 Join-in-progress, time-to-interactive.** *Interactive* = the joining client holds the nine 1 km layer
  tiles around its player, has collidable ground under the player, and has applied its first entity snapshot.
  Pass: ≤ 10 s at 100 ms RTT ±20 ms jitter, 2% loss, 10 Mbit/s send cap; ≤ 20 s at 200 ms RTT, 5% loss,
  5 Mbit/s send cap. (Nine tiles were first estimated at about 3 MB compressed; measured, the four layers streamed
  since M1.4d come to about 490 KB. 10 s is where a person suspects a hang; 200 ms and 5% loss is worse than any Australia-to-Australia
  link.)
- **N2 Corrections on the scripted walk.** The ten-minute walk uses legal inputs only, so every server
  correction is a false positive by construction. Pass: ≤ 0.5 corrections per player-minute over the whole walk;
  ≤ 2 per minute on the named divergence segment — the cliff edge, the rock platform, and the water's edge on the
  lake or swamp approach (wading is the third place the two collision models disagree); no single correction
  displacing the player by more than 1.0 m; the same budget in SOLO and over the wire at 100 ms / 2%.
  A founder with developer mode on (M1.E) is held to nothing but the region's edge, the ground included, since a report
  carries no word of whether they are in the flight the mode allows: the solo server's silence while William fell through
  a face on 2026-09-21 was this rule, not a fault (DEBTS, paid 2026-09-22).
- **N3 Disconnect and rejoin without a server restart.** A client's transport is cut mid-walk with no clean leave
  and it rejoins within 60 s. *Held-tick variant (tests the plumbing):* the harness holds the server tick for the
  interval (a test control, not a game feature); pass: the server's world digest before the cut equals the digest
  after the rejoin completes, except the player-session record; the rejoined client's mirror of every entity
  inside its interest radius matches the server's digest of that subset exactly; time-to-interactive on rejoin
  ≤ 3 s at 100 ms / 2% (layer tiles cached on disk); server managed-heap growth after ten cut-and-rejoin cycles
  ≤ 5 MB. *Live-tick variant (tests the game):* the world keeps simulating through the cut; the before/after
  digest row is dropped and the remaining three rows apply as written, the mirror matching the server's *current*
  digest of the interest subset.
- **N4 Two-client 30-minute soak.** Two scripted clients walk loops for 30 minutes at 100 ms RTT / 2% loss.
  Pass: zero crashes and zero disconnects the harness did not induce; server working set ≤ 1 GB; managed heap at
  minute 30 ≤ 1.2× the heap at minute 5; each client's mirror of the other player within 0.5 m of the server's
  authoritative position (after the interpolation delay) in ≥ 99% of one-second samples and within 2.0 m in 100%;
  entity counts inside the shared interest radius equal on both clients in ≥ 99% of one-second samples and never
  diverging for more than 2 consecutive seconds; ≥ 99.9% of server ticks finish inside the 50 ms tick interval
  with p95 tick ≤ 5 ms; average bandwidth ≤ 20 KB/s per client outside the join stream.
- **Send caps** stand as harness conditions regardless of the owner's link; the owner measures his real upload
  before the week-3 gate and a row is added only if it lands under 5 Mbit/s.

**What the corpus's founders are since 2026-09-16.** They have bodies (FP.1, FP.2): each loses water at its exertion's
rate on the world's clock (the loop's flat legs are run, its named legs walked), is told the word for it, walks slower
as it goes, and dies at 15% lost to wake again at the wake, under the beta arc's bridge, so the cold never kills them.
The walker answers as a player would (`ScenarioRunner`, the rules engine-free in `Drinking`): told thirsty, it looks once
a second for fresh water standing within 25 m in the tiles it holds, leaves the loop for it, drinks through the use key
until the word is gone and walks on, writing a `drink` record; a death is a `died` record, and the loop is walked again
from its approach. On a gate world made before 2026-09-18 there was nothing to drink within the loop's reach: no creek or
stream carried a surface (WG.1 gave them one) and the nearest lake with water in it was 1,977 m from the wake, so
by the body's own numbers a soak's founders dry at about a quarter of their water a world day and die of thirst around
the seventeenth minute and again near the end, each death a jump back to the wake that N4's mirror rows may see. The loop
is laid by `Tools/corpus/lay_loop.py` with a pass by a kangaroo mob's place and an oystercatcher pair's (`--pass`, the
places from a soak's own fauna records), 1.86 km and about two laps a soak, so a soak startles animals and `fauna_check`
holds a flight; the ten-minute walk reaches the last N2 leg by 348 s of its 500 s budget, which is now measured to that
leg rather than to the whole lap. `corpus_check.py` reads what the bodies lived through.

## 8. Rendering, input, UI (Unity 6000.3 LTS, pinned)

- URP 17, Forward+, linear colour, DX12 with DX11 fallback. Settings in load-bearing order: BatchRendererGroup
  variants = Keep All → Rendering Path = Forward+ → GPU Resident Drawer = Instanced Drawing (SRP Batcher on) →
  Render Graph on → GPU Occlusion on (validate: open bug with the Resident Drawer) → Depth Priming = Forced → STP on
  at render scale 0.7 (1440p output) as the default quality; native 1440p as an option. Asserted by an edit-mode
  test.
- **Water** (M1.4c, 2026-09-10; the rule and the material amended by M1.4g, 2026-09-20): the standing water of
  each streamed tile is its own mesh, built from the depth the server sent over that tile's ground (§7). Every
  corner of a cell stands at its own height: a wet corner at its water's surface, a dry corner on the ground itself
  a millimetre under it. A creek's surface falls with its bed, the skin is continuous because neighbouring cells
  share their posts, and on a shore it tapers to nothing where the bank begins, so water never laps over dry ground.
  A flat cell is one whose corners lie within 2.5 cm, and it sits at the mean of them (2026-09-22): a flat body's posts
  travel rounded to the centimetre, so 1.1 cm called a cell of a flat lake not flat and drew it with its own corners, a
  step above its row that showed along the water as the rows of dark dashes across Windermere. A dry post with wet
  posts on every side is a shallow inside the body, not a bank, and takes the body's surface.
  A cell wholly under water and level keeps the merged flat run of M1.4c. Requiring all four corners, as the rule did
  until 2026-09-20, drew nothing at all along a channel narrower than the 4 m between posts, so every creek WG.1
  had given water was invisible while the founder waded in it. Cells merge along a row while the surface holds at
  one height, because standing water is flat. `WaterSurface` in ClientCore finds the rectangles and is tested
  headlessly; `WaterTileBuilder` in the Unity layer turns them into a mesh and nothing else. The mesh carries no
  collider: what a body may wade in is the server's and the mover's, not the view's. The sea keeps the plane it
  has had since 2026-09-08 and only water above the datum is drawn from a tile, so the two never contend for one
  surface. Both are drawn with `EarthGame/Water`: transparent, its colour and its hiding power taken from the
  water column between the surface and whatever opaque thing the scene's depth buffer says lies behind it
  (Beer–Lambert, nine tenths hidden by 2.5 m), lit by the same spherical harmonics and main light as everything
  else so it darkens with the country. It samples no reflection probe: the environment cubemap is built from the
  sky once and refreshed by nothing, which is why the old near-mirror water was the brightest thing in the frame
  after dark. Since M1.4h (2026-09-21, William's "ripples now") the surface moves: four waves running downwind tilt
  the normal in the shader — never the mesh — at the deep-water dispersion's speeds, as steep as the weather's wind
  makes them (`Weather.At`, read by the client once a second) and fading where they would be finer than the pixels;
  their time is the game's awake seconds, so a paused game's water stands with its world. `-eg-hide water` draws no
  water at all, for the frame's cost with and without it; `-eg-day D` and `-eg-hour H` ask a development game for a day of the year
  and an hour at launch (sent once as the developer's clock settings when the mode is granted), which is how the frames
  of a windy afternoon are taken on a world whose first day is still.
- **The ground's colour** (M1.4d, 2026-09-10): the world names what covers every one of its cells — one byte, the
  cover in the low six bits and the quarter of the land's own wetness in the top two (§10) — and that byte is
  streamed as a layer like any other. `GroundPalette` in ClientCore is the single owner of what a ground looks
  like: each cover names its dry colour and its wet colour and the quarter sits between them, so a tuft of grass
  added later asks the same question and cannot disagree with the ground it stands on. `GroundColourMap` turns a
  tile's cover into a picture, blending between the posts so a beach does not end on a line, and the Unity layer
  makes that the tile's own terrain layer, one texture stretched once over the kilometre. Two texels a post is
  what a blend needs; a metre a texel costs four times the work and shows the same 4 m raster. The ingredients do
  not travel: measured on the nine tiles around the wake, wetness is 476 KB and soil depth 501 KB against the
  ground's own 418 KB, and the byte they come to is 96 KB on the wire. Since M1.6e (2026-09-22) the tile's picture is
  drawn by `EarthGame/Ground`, a terrain shader of this project's own in place of the pipeline's: a Lambert lit as the
  stand is, which multiplies the map's colour by a grain made in the shader from the world position (a fine scale of
  0.35 m and a coarse one of 1.7 m, moving a sixth of the brightness, the troughs a touch darker and warmer) and tilts
  the normal by the grain's slope so the sun shows it; the grain fades out by 110 m so the far ground stays the map's
  colour and never shimmers. `-eg-hide grain` flattens it, so its cost can be parted from the rest.
- **What a streamed tile costs, and where** (M1.4e, 2026-09-10). Sampling a tile's posts and building its colour
  map are pure over the tile alone and run on a worker (`TilePreparation` in ClientCore): 21.9 ms and 7.1 ms a
  tile in Release, off the main thread. The main thread does only what Unity requires — `SetHeights`, the
  texture, the objects — under `FrameBudget`, which is §8's streaming allowance made a stopwatch: no piece of
  work is started in a frame that has already spent 1.5 ms. What one indivisible call costs is a different fact
  and is measured, not pretended away: `TerrainData.SetHeights` on a 513-post tile is 21 ms and is 93 % of what a
  tile costs the main thread, so a tile is a frame of about 30 ms (`SetHeightsDelayLOD` with `SyncHeightmap` was
  measured the same day at 21 to 25 ms: the same work under two names). That is a debt in DEBTS.md, and
  `build_check.py` reads each tile's `build` record and holds the rest of it in place.
- **What stands and lies on the ground** (M1.6a, 2026-09-11). A tile's trees, sticks and cobbles are placed on a
  worker (`StandPreparation` in ClientCore: where the server put each, to the centimetre, by `StandLayout`), with
  the matrices they are drawn by, and swapped in whole, so a tile costs the main thread a dictionary write. They are
  drawn by `Graphics.RenderMeshInstanced` from lists that do not change as the founder walks, in two bands of one
  shader (`EarthGame/StandLit`): the trees of the 64 m blocks within 250 m near, the grown tree, and those of the
  blocks beyond far, a trunk under one clump. The shader puts each instance in exactly one band by its distance
  from an eye the client hands it each frame, so the shadow caster's pass agrees with the lit pass. Sticks and
  cobbles are drawn within 60 m. What a frame draws is chosen by block (2026-09-11): a far block only when it lies in
  the view, since far trees cast no shadow, and a near block out of the view into the shadows alone, since a tree
  behind the founder still shades what is in front; the view is tested a little wider than the camera's, since it is
  taken before the camera settles for the frame. Farther off, a block draws one far tree in two, each crown spread to
  cover what two did, and beyond 1 km one in four; a far tree's corners are shared between its faces and shaded
  smooth, since far off a facet is smaller than a pixel; and a near tree casts only where its shadow can reach the
  ground the shadows are drawn on, which the pipeline's shadow distance and the sun's height decide. Each instance
  carries only its own matrix: the shader assumes uniform scaling and takes no light probe, lightmap or LOD fade, and
  the draws ask for no probe, so the renderer makes no inverse matrix and looks nothing up for each of tens of
  thousands of trees a frame. Colour is in the vertices (v1's `EucalyptBuilder`, ported with Bherwerre's forms;
  `StandForms` in ClientCore is the one table of what each looks like). A tree's faces share their corners: each
  face leads with a corner that carries its normal and its colour, and the shader takes both from the leading corner
  alone (`nointerpolation`), so a face stays one plane of one colour with about a third of the corners. The bands' materials copy a material asset
  with instancing on (`ProjectSetup`): made from the shader alone, the build stripped its instanced variants and the
  first player drew no trees, as happened to the terrain on 2026-09-08. Measured without a window (the recorder's `-eg-hold`,
  §12) on freshly created worlds in the morning, when the low sun lets every near tree cast, each configuration the
  faster of two passes: the trees first cost 15.2 ms of the median frame beside Windermere and 10.5 ms at the wake;
  they now cost 4.4 ms and 3.0 ms. The budget's own lines divide that, beside Windermere, into the main thread's
  choosing and handing over (1.1 ms, against culling and submit's 2.0), the near trees' shadows (0.9 ms, against the
  1.6 ms the shadows share with the terrain) and the trees drawn (2.0 to 2.4 ms, the bands measured apart or the whole
  less its shadows and its main thread, against the trees' 2.0). The sticks and cobbles cost 0.2 ms, and 0.6 ms on
  the wake's beach of cobbles, against 0.7 ms. The understorey (M1.6c) is drawn the same way, from the cover byte
  rather than a layer of its own: the tufts within 45 m of the founder, placed again when they have walked 6 m or when
  a cover tile arrives, instanced in five shapes and six variants and casting no shadows. Since M1.6e (2026-09-22)
  a cover grows its own shape with a companion at a stated share between (tussocks in the heath and the bracken,
  clumps of sedge in the grass, tussocks in the sedge), a low herb stands between them on every growing cover, the
  tufts run from six tenths to one and a half of their cover's height, the density comes in patches five cells wide
  by a smooth hash of the cell grid with the stated density as its mean, each tuft takes its own shade from a hash of
  where it stands (`_Vary` on the stand's shader, zero for the trees), and nothing stands where the client's own
  water-depth tile says water does, save a sedge's clump in water a hand deep or less. Measured the same way, it
  costs 0.1 to 0.4 ms of the median frame (6.7 ms against 6.3 at the new wake, 6.8 against 6.7 beside Windermere),
  against the 3.0 ms §8 gives grass and the understorey, and its placing costs the main thread half a millisecond once
  every six metres (0.9 to 1.5 ms since M1.6e's herbs doubled what is placed, measured at the four vantages). The far forest (M1.6d) stands beyond the stand tiles a client holds: one far tree to each 40 m square of the region that holds a tree, from the far stand and the far count the client is sent once at the join, as tall as the square's trees on average and as wide as their crowns together, drawn in the far band's own mesh and material in quarter-tile blocks chosen by the view, and only where that tile's own stand is not held. Measured the same way, it costs 0.1 to 0.3 ms of the median frame (7.4 ms against 7.2 at the wake, 8.2 against 8.1 beside Windermere, 7.5 against 7.2 at the new wake), its 28,842 far trees drawn unthinned and casting no shadow. §8's own protocol, in a visible window, is still owed (DEBTS.md). On the wire the two layers are small beside the ground: the nine tiles round the gate world's wake
  are 110,056 bytes of stand and 119,421 of loose as cached, deflated, against the ground's 282,686 (the M1.6
  gate's shaped join, 2026-09-11).
- **Things lying and in hand** (M1.5a, 2026-09-11): an item is drawn from the stand's own mesh for it (`ItemLooks`: a
  stick is a stick of the litter, a cobble the item's own diameter, which is now the size the litter's are drawn at)
  in the stand's loose material, one object each, placed every frame where it was three ticks behind the estimated
  server tick, between the positions it was stated at (`EntityView.PositionAt`), so a thing let fall is drawn
  falling as a remote body is drawn walking. The thing in hand is the same mesh in front of the camera; since M1.5c it
  moves as v1's did (`HandMotion`): it keeps a shoulder's share of the head's pitch, walks with the stride, gets where
  it should be late by its weight, and swings through every use. Nothing lying
  is a collider: the crosshair picks a thing by its mesh's bounds widened 4 cm and the ground by a ray against the
  tiles' colliders, and the verb line offers only what the server's reach allows. A rejoin draws the new
  connection's things (until 2026-09-11 it went on drawing the first connection's).
- **The animals, made in code** (M1.7b, 2026-09-16; CANON ruling 29, which retired the plan's "sourced meshes"; one
  skin in place of the pieces since 2026-09-18). An animal is a skeleton of fixed bone lengths whose joints a pose
  moves, and one skin grown over it once in its resting pose: a few closed shells, each a tube of rings run along the
  bones with a radius that changes gently, mitred where its line bends and rounded off at both ends, every limb rooted
  inside its parent and the sharpest joints one ball nested in another, the belly a paler colour below a line round each
  ring. `AnimalShapes` in ClientCore is the one table of that, engine-free and tested headlessly: every shell closed with
  no open edge, every face wound outward and every limb rooted in every pose, the sizes held to the species' published
  bands, and the corners shared between faces so the skin has as many corners as faces. The Unity layer's `AnimalLooks`
  turns the skin into one mesh a kind, kept as the trees' meshes are, and hangs it on a skinned renderer whose bones are
  the skeleton's; `EntityViews` gives each animal an object with a child transform for each bone and places the bones
  every frame for the pose the server last gave it, and the graphics card moves the skin with them. Until 2026-09-18 an
  animal was a child object with a rigid mesh for each piece, which William saw in the first frames as "a load of 3d
  shapes put together". The skin is drawn at scale one under the animal's own transform, because the stand's shader
  assumes a uniform scale and reads no inverse matrix; the material is a copy of the stand's with its band's split set
  past any distance a world holds, since the animals have one band and no far form, and a device that draws nothing
  instanced has no stand material and so makes no animals. The three poses the wire carries
  are resting, grazing and fleeing (§5): the last is a function of time — the kangaroo's hop raising the body and
  swinging the legs on a cadence of the stride over the kind's flee speed (3.15 m at 7 m/s, the measured 0.45 s), the
  oystercatcher's flight holding it a metre or so up with its wings beating every 0.18 s — and each animal starts its
  cycle at a place of its own drawn from its id, so a mob does not bound as one machine. Animals are never offered to
  the crosshair. What they cost a frame against §8's 0.6 ms for the fauna is unmeasured (DEBTS.md), and the frames are
  owed the owner's eyes.
- **The litter under the crosshair** (M1.5b, 2026-09-11): a tile's sticks and cobbles are placed again, on the worker,
  less what was taken from its cells whenever the client is told of a taking (`StandPreparation.TakenIn`, a copy the
  worker alone reads, and a count of takings per tile that says when to), and the rest keep their places. The
  crosshair searches the held tiles for the things round the founder (`LyingNear`, placing each as the drawing does)
  and meets each by its mesh's bounds as it meets an entity, so it offers "a stick — pick up" for a stick of the
  litter as for one put down.
- Budget: 13.3 ms design at 1080p-internal (60 fps hard floor at 16.7 ms; any frame over 33 ms is a named
  defect): shadows 1.6, terrain 1.8, grass/understorey 3.0, trees 2.0, props 0.7, fauna 0.6, sky/fog/water 0.8,
  post 1.2, UI 0.4, reserve 0.6; SSAO off until measured. CPU: sim tick 2.0, streaming hard-capped 1.5 (a
  Stopwatch budget, never an item count), culling/submit 2.0, physics 1.5, fauna 1.0, UI/scripts 1.0. VRAM ceiling
  3,500 MB of content. Measurement: uncapped, visible window on a named display at a stated resolution,
  median/p95/worst, machine load recorded.
- Terrain: tiled Unity Terrain (kilometre tiles streamed at 513 posts, `TerrainTileBuilder.TilePosts`, the
  3 × 3 around each player collidable; the coarse ring over the region and the 64 km skirt beyond it at 1025,
  `CoarsePosts`). Each streamed tile carries its own terrain layer, whose
  picture is that tile's cover (above); the coarse ring and the skirt keep the flat layer, because a cover is a
  fact of a world and they are read from the region's bake. The grain underfoot is the ground shader's own (M1.6e,
  above): the stock layer could carry the colour or a tiled detail but not both without blending, and a blend washes
  the colour out (DEBTS.md, 2026-09-10, paid). No "build all now" path.
- Flora: grass and shrubs by Terrain detail instancing first, BatchRendererGroup only if measured over 3 ms. Trees
  are drawn instanced from the stand layer (above), not as prefabs: the plan's parametric skeleton, with v1's vertex
  colour in place of authored leaf and bark materials and a near and a far band in place of a LODGroup and
  billboards (M1.6a, 2026-09-11). Fauna: made in code, rigid pieces on a skeleton of fixed bones, posed and animated by
  arithmetic (M1.7b, 2026-09-16; this line said "sourced meshes" until CANON ruling 29 settled that nothing is bought or
  downloaded, and the plan's §8 line goes with it).
- Rules: no `MaterialPropertyBlock` (source-text scan + play-mode walk); named layer masks in one file; no
  `Awaitable` in engine-free assemblies; `[SerializeField]` on fields only; Unity 6 physics names.
- **A game left alone pauses itself** (M1.E, CANON ruling 38, 2026-09-20): `IdleWatch` in ClientCore decides it, fed
  each frame with the seconds, whether anything was touched (the controls asset's `Touched` action — any key, the mouse's
  buttons, movement and wheel, the pad's sticks, triggers and buttons — named in the asset and nowhere in code, M1.5a's
  rule) and whether the window has the focus —
  a minute untouched or the focus lost sends it to sleep, any touch or the focus coming back wakes it, and the edges are
  reported once. Asleep, the bootstrap steps its SOLO server not at all and drops the slept time rather than catching it
  up, the client's clock stands, the founder is held, the frames are held to ten a second (the display sync a played game
  keeps while awake is dropped for it, 2026-09-22, since a capped frame cannot also be a synced one) and the HUD shows one line;
  the waking frame's presses, and the jump or flight they queued, are thrown away, so the key that wakes the game does
  nothing else. A recorded run drives its founder by a scripted source whose frames carry no touch, so the idle scenario,
  like the controls one, takes the real source and presses its waking key on a keyboard of its own. Only a SOLO game with
  hands at it may sleep: never a joined game, whose world is the server's, and never a recorded run, which waits minutes
  between its presses — except the `idle` scenario, whose whole business is to sleep. An idle game drew over six
  hundred frames a second before this, a GPU the machine could have spent on a build or a measured run.
- Input System 1.20 with one actions asset, `Assets/InputSystem_Actions.inputactions`: the project's actions and,
  since M1.5a (2026-09-11), the one owner of every binding. Its "Player" map is CANON's verb rule — move, look, jump,
  sprint, crouch, work (left mouse, held), use (right mouse), carrying (Tab), the wheel, the gamepad's shoulders and
  the keys 1–9 for the hand, Escape, F11 for fullscreen (CANON ruling 28: `WindowMode`, polled by `Bootstrap` in the
  menu and in play alike, fills the screen the window is on and gives the window back), and F12 for a screenshot — and
  code names actions (`Controls`), never keys. An
  action the asset lacks does nothing and is named in the log; `CIBuild` refuses to build without every one, the
  edit-mode `ControlsTests` checks each is bound at the desk, and a source rule refuses a key in code. The
  scripted-input seam hands the same frame (`ControlsFrame`), its pitch the camera's. The verbs (`VerbController`)
  act on what the crosshair is on and what is in hand: a thing within reach, the ground within reach, the keys and the
  wheel for the hand, Tab for the carrying window; Escape lets the mouse go and a click takes it back; Alt held turns
  the view without the body (M1.E, CANON ruling 36: `FreeLook` in ClientCore keeps the turn's offset, bounded at 135°, and since his word of 2026-09-21 the tilt's, bounded at 89°, both
  gliding home in a quarter second when Alt is let go; the mover walks and the server is sent the body's facing, the
  camera and the crosshair take the view's); F2 turns developer mode on and off (M1.E, ruling 39, the server's answer
  deciding: flight, the panel and the settings follow what was granted, and turning it off sets a flying founder down);
  F3, while developer mode is on, opens the developer's panel (M1.D, CANON ruling 30) in sections: the founder (the flight switch
  with noclip under it, standing at the wake), time (the hour, the day of the year, the clock's rate), the animals
  (stood up within, taken away beyond; M1.7c's wariness, run and pace; and the pose an animal set down by hand is shown
  in, M1.7b), spawning (a stick, a cobble, a kangaroo or an oystercatcher ahead, the animals since M1.7b gave them looks
  to judge), the environment (shown greyed until the weather is drawn, M1.8b) and
  the world as the client reads it; a row with a start has a reset, and one button resets them all with the flight.
  It has the mouse while it is open and rests the founder's hands, and its text is white on the panel. UI Toolkit
  for HUD (M1: crosshair, verb line, clock, and the carrying window, which only shows the nine places, their things'
  masses and which is the hand while the world runs on), menus, settings, and the tablet page; readability floor
  3:1. Sound in M1: wind by exposure, surf by distance, birds from presence, footsteps by surface, rain. Footsteps
  and things landing came first (M1.5c, 2026-09-11), made at load from arithmetic rather than recorded
  (`FootstepSynth`): a footfall comes from the distance walked (`Stride`) and sounds of the cover the server streams,
  or of the water over it (`Footing`), and a thing that falls sounds of wood or stone when it is drawn landing, by the
  energy it came down with. Since M1.5j (2026-09-22) a footstep is an impact and the ground's own answer to it rather
  than v1's one burst of filtered noise: the heel strikes and the forefoot follows 70 to 110 ms later, and each strike
  is answered by a body (the damped low thump of the foot's weight), by modes (damped tones at a hard ground's own
  pitches, rock's knock and ring at `RockRingHz`) and by grains (small impacts scattered after the strike, dense and
  dull on sand, fewer and crisper on litter), with twigs snapping under the whole step on heath, a swish of blades on
  grass, and a burst, a wash and chirping bubbles for a wading step. Heath is a footing of its own (`FootingSound.Heath`);
  the forest floor stays litter. Every step is made from whole numbers, the same in every build, and put at its
  ground's own peak inside a stated band. `Engine/tools/EarthGame.Sounds` writes the same sounds as WAV files for the
  owner's ears, with a blind set lettered A to G and the key kept apart. Every automated run is muted.

## 9. Player and collision

The mover is a pure function in the engine, `Step(state, input, dt, IWorldCollision)`: walk/climb/slide slope
thresholds, step-up, grounded/airborne friction, capsule swap for stances, a wading state; speed from `Locomotion`'s
walker's law (M1.5h, CANON ruling 34, 2026-09-21: a table of the speeds people are measured walking at on slopes —
Sun 1996, Kawamura 1991, a treadmill hiking study of 2024, Minetti 2002 — with Tobler kept as `JourneySpeedMs` for the
journey's hour-average and Pandolf for the cost), reached by the gait's acceleration and left by the brake (a body at
nine tenths of its pace by the third step; v1's 4 m/s² brake), on ground no steeper than dry sand's repose, 35°, past
which the founder slides — and a sliding body keeps its feet on the surface by the mover's own rule (M1.5i, the evening
M1.5h landed: the client's capsule cast starts inside a steep face and reports nothing, and William fell through the
world), so what the sweep fails to see the ground probe corrects. The client implements `IWorldCollision` over PhysX (capsule casts against the Terrain collider;
nothing lying on the ground is a collider, §8); the server implements a heightfield ground clamp and speed cap for validation.
The water a founder wades in is where the world stands it (M1.5d, 2026-09-11): the client's collision reads the depth
the server streams over each tile (`StreamedWater`: the held ground and that depth, added, which is the server's own
surface to the centimetre the wire carries), and the sea at the datum where no depth is held yet; the engine's
heightfield collision takes a world's surface layer the same way. The server does not judge wading (§12). Water
deeper than a standing founder can keep their eye out of is swum (M1.5e, CANON ruling 24): the mover floats the body
with its feet at a float line, the swimming depth (the standing eye's height less a hand's breadth) under the surface,
swims it at the breaststroke's pace or the crawl's, and stands it where the bottom comes back within reach; whether a
body swims is worked out every step from where it is and the water there, so no message, save or digest carries it.
A development game (`-eg-dev`) can fly (`Flight`, v1's): the fly key takes the body out of the mover's hands into the
flight's, which holds it to nothing at all — it passes through the ground as through the air (CANON ruling 25), and the
founder is set on the ground when a flight ends under it — and the game's server lets the flight stand under
`MovementRules.AllowFlight`, which a host sets with `+server.dev 1` (§12). Since M1.6b the client's collision knows
the trunks: every tree it holds within ten metres of the body is lent a capsule on the props layer (`TrunkBodies`),
standing where `StandPreparation` draws its tree and as thick as `StandForms.TrunkRadiusAt` draws it where a founder
meets it, so a founder is stopped at the bark and slides along it. The server has no trunk of its own, by decision
(§12). **Named
defect class:** the two disagree most at cliff edges, rock platforms and the water's edge (`DEBTS.md`); the walk
scenario carries that segment and N2 carries its budget. The mover-feel decision is the owner's, hands on the
controls (ruling 12).

## 10. Contracted formats (owner ruling 16: versioned; a schema change is a renegotiation in writing)

| Format | Version | Owner of the writer | Readers | Defined |
|---|---|---|---|---|
| `eg2.atlas_probe` (offline JSON diagnostic) | 1 | `Tools/world/atlas_probe.py` | `atlas_probe_check.py` | [WG.0a diagnostic format](contracts/WG.0a_EXISTING_ELEVATION.md#diagnostic-format-eg2atlas_probe-version-1); only the legacy global bake is read. This is not a game/world format. |
| Wire protocol | 16 (v1 Hello, Welcome, Refused, Ping, Pong; v2 PlayerMove, PlayerState, Correction, the spawn in Welcome; v3 the extent in Welcome, TileRequest, TileHeader, TileChunk, SnapshotEnd, PlayerLeft; v4 EntitySpawn, EntityState, EntityGone; v5 a layer byte on TileRequest's wants, TileHeader and TileChunk; v6 Intent, IntentResult, Carrying and EntityGone's third reason; v7 a pick-up of a thing lying, and LooseTaken; v8 tile layers 6 and 7, the far stand and the far count, asked for over the region's whole grid; v9 an animal's pose on EntitySpawn and EntityState, and the interest radius in Welcome; v10 to v14 as `ProtocolInfo.Version`'s history states them: a developer's settings and the clock on the pong, the clock's scale on the pong, the fleeing pose, the founder's water and the drink, the founder's core and the death; v15 the stone's state on an item, the knap and the answer's words, FP.3; v16 DeveloperMode both ways, M1.E) | `EarthGame.Protocol` | server, client | `Messages.cs`; `ProtocolInfo.Version`; a Hello of another version is refused with both numbers. A tile message names its layer as a byte (§10's tile row), and a layer byte this build does not know is refused rather than guessed. A `TileRequest` names at most `TileRequestMessage.MostTiles` (64) tiles, and a writer given more refuses to write it. EntitySpawn (reliable): u64 id, u32 definition id, i64 tick, f64 east, up, north, f32 yaw, u8 component mask (1 item, 2 an animal; a bit this build does not know is refused), then an item's u8 resting and f32 fall speed, then an animal's u8 pose (1 resting, 2 grazing). EntityState (unreliable; reliable when it carries an item's rest or an animal's pose): u64 id, i64 tick, u8 field mask (1 position, 2 yaw, 4 item, 8 an animal's pose), then the fields named. EntityGone (reliable): u64 id, u8 reason (1 died, 2 left the viewer's interest, which an animal taken away always has, 3 a founder took it up). Intent (reliable, client to server): u32 sequence, u8 verb (1 pick up, 2 put down, 3 hold), then for a pick-up u8 target kind and, for kind 1 (an entity), u64 id, or for kind 2 (a thing lying, v7) u16 row, u16 column, u8 kind (2 a stick, 3 a cobble) and u8 index; for a put-down f64 east, up, north; for a hold u8 place (0 an empty hand). IntentResult (reliable): u32 sequence, u8 outcome (0 done, 1 out of reach, 2 not there, 3 hands full, 4 nothing in hand, 5 no such place, 6 not now). Carrying (reliable, to the carrier alone): u8 hand, u8 count (at most 9), then per thing u8 place (1 to 9), u64 id, u32 definition id; a thing's spawn tick stays with the server. LooseTaken (reliable, v7): u16 count (at most 2048), then per cell u16 row, u16 column, u16 sticks taken, u16 cobbles taken, a bit for each index; to a joiner before SnapshotEnd, and each taking to every client. A verb, target kind, outcome, place, definition or taking past the fifteen things a code counts that this build does not know is refused. FP.3 (protocol 15, additive): an item's component gains, after its u8 resting and f32 fall speed, f32 its own mass kg, f32 its edge, f32 its platform degrees and u16 the flakes taken, zeros for a thing with none of its own (a mass below nothing, an edge past one, a platform past 180 degrees or a number that is not one is refused); Intent's verb 5 (knap) names its core as a pick-up names its target, or as a place of the hands (target kind 3: u8 place, refused for a pick-up), then u8 the wind-up (0 a tap, 255 a full swing); IntentResult gains a string, the words for what the stone did, empty for every other verb; outcomes 9 bounced, 10 flaked, 11 shattered, 12 crushed, 13 no hammer in hand, 14 no stone to knap. M1.E (protocol 16, additive): DeveloperMode (24, reliable, both ways): one byte of flags, bit 1 on and bit 2 refused; a bit beyond the two is refused where it is read. Client to server it asks; server to client it answers with what the session now has. |
| Layer tile (`TileCodec`) | 2 (v1 carried the ground alone and named no layer) | `TileCodec.cs` (Engine) | `TileService` (server), `TileReceiver` and `DiskTileCache` (client) | A tile carries one layer, named by a wire-visible byte that is never renumbered: 0 the ground, 1 the water's depth over it, 2 the water's class, 3 the ground's cover, 4 what stands on it and 5 what lies loose on it (M1.6a, 2026-09-11), and 6 the far stand and 7 the far count (M1.6d, 2026-09-13). Which layers exist, which of them carry codes, and the folder each caches under (`ground`, `water-depth`, `water-class`, `ground-cover`, `stand`, `loose`, `far-stand`, `far-count`) are `TileLayers`' alone: the receiver asked the layer's number instead and dropped every cover tile it was sent, silently (2026-09-10). A layer of metres (0 and 1) is posts² int16 centimetres, each row the running delta from its first post, deflate; a value beyond ±327 m, a step between neighbouring posts too long for a signed 16-bit number of centimetres, or a NaN is refused at encode (until 2026-09-13 the first was clamped and the second wrapped, unsaid). A layer of codes (2 to 7) is posts² raw bytes through deflate, read from the raster's own cells rather than interpolated, and a code beyond a byte is refused at encode. The far layers' posts stand 40 m apart (`TileLayers.FarCellM`), and the server works both out from the world's stand when a tile is first asked for (`TileCodec.FarSquare`), serving none from a stand whose cells do not make a 40 m square whole: a far post carries the square of stand cells from half a square before its cell to just short of half a square after, in rows and in columns, so that no tree is counted by two posts; the far count is how many trees stand in the square, to a byte, and the far stand is the `StandCodes` code of the tall plant most of them are (the first in `StandCodes.Tall`'s order on a tie) at their mean height, zero where none stands. The whole Bherwerre region's far stand is 14,257 bytes as cached and its far count 25,297 (M1.6d's shaped join, 2026-09-13). CRC-32 (IEEE) of the deflated bytes in the header and in front of the cached file. The water's surface is not a layer of its own: a client that holds a tile's ground is sent only the depth standing over it, zero where the ground is dry — measured on the Bherwerre world, the nine tiles around the wake are 31 KB as depth against 277 KB as a surface, beside 300 KB for the ground itself. |
| `RegionRaster` (`eg2.raster`: a raw grid + JSON sidecar) | 2 (v1 read as a heights layer in metres) | `Tools/data/raster_io.py` (the bake and the fixture writer) and `RegionRaster.Write`/`WriteCodes` (the world-creation pipeline); the loader's tests pin both writers to one fixture law by checksum | `RegionRaster.cs` (server and client), the verifiers | Sidecar keys: format, version, name, region, layer, dtype (f32, u8, u16, i16, u32), byte_order little, raw (the raw file's name: .r32 for f32, else .u8/.u16/.i16/.u32), scale, unit (m, 1, id, flags), width, height, cell_m, extent_m, centre_lat, centre_lon, min, max (in the unit), source, sha256, and sea_fraction for heights. A value is raw × scale in the unit; version 1 carried f32 metres with min_m/max_m. Row 0 north, column 0 west, cell centres at east = col·cell − extent/2, north = extent/2 − row·cell; width = height = extent/cell + 1. The loader refuses any other version. |
| `eg2.almanac` (one JSON line) | 1 | `Almanac.cs` via the almanac console tool | `solar_check.py` | Keys: format, version, time_basis, day_of_year, latitude_deg, longitude_deg, declination_deg, daylight_hours, sunrise_local_hour, sunset_local_hour, noon_elevation_deg, wake_local_hour, wake_elevation_deg, wake_azimuth_deg. Hours are local mean solar time: no zone, no equation of time. |
| `eg2.weather` (a JSON sidecar and a raw file, M1.8a; version 2 M1.8c) | 2 | `WeatherYears.cs` via the almanac console tool's `+weather` | `weather_check.py` | `weather.json` keys, in order: format, version, region, latitude_deg, longitude_deg, altitude_m, exposure, first_seed, seeds, first_local_day (0: the year's first local solar midnight), days, steps_per_day, sampled_at ("the middle of each step"), time_basis (the almanac's), fields (version 1: `air_c`, `rain_mm_per_hour`; version 2 adds `wind_ms` at a founder's height, `cloud_01`, `humidity_01` after them), dtype (f32), byte_order (little), raw (`weather.f32`). The raw file holds a 32-bit float for each field, in the sidecar's order, of each step of each day of each world seed in turn from first_seed: step k of day d is the sky from `Weather.At` at local solar day d + (k + 0.5) / steps_per_day since the epoch, at the region's centre, the sidecar's height and openness of ground. A reader takes the fields and their count from the sidecar: version 2's stride is five floats where version 1's was two. |
| `world.json` (`eg2.world`) | 1 | `WorldSave.cs` (the server) | server (continue), the shell (which world is newest), `save_check.py` | Keys: format, version, region, seed, extent_m, created_utc, saved_utc, protocol_version, tick, clock {total_hours, started_at_hours}; additive since M1.2 (2026-09-09): wake_east, wake_up, wake_north, layers {layer name: sha256 of the raw file}; additive since M1.3 (2026-09-09): next_entity_id. |
| `players/<name>.egp` (`eg2.player`) | 6 (versions 2 to 5, without the hands, the water, the core and the stones' state in turn, are still read; version 1, `players/<name>.json`, is still read and replaced on the next save) | `PlayerFile` in `RegionFile.cs` (the server) | server, `save_check.py` | Little-endian: magic `EG2P`, u16 version, the name as a u16 UTF-8 byte length and the bytes, f64 east, up, north, f32 yaw, f32 pitch, u8 flags (1 grounded, 2 wading, 4 crouching), i64 saved tick; since version 3 (M1.5a) u8 the hand's place and u8 the count of things carried (at most 9), then per thing u8 place (1 to 9, none twice), u64 id, the key as a u16 UTF-8 byte length and the bytes, i64 spawn tick and, since version 6 (FP.3), f32 the thing's own mass kg, f32 its edge, f32 its platform degrees and u16 the flakes taken off it (zeros for a thing with none of its own); since version 4 (FP.1) f64 the fraction of body water lost; since version 5 (FP.2) f64 how far below normal the core was; then u32 CRC-32 of everything before it. Version 1 (JSON): format, version, name, east, up, north, yaw_deg, pitch_deg, grounded, saved_tick. |
| `regions/r.X.Y.egr` (`eg2.region`) | 3 (version 1, without diffs, and version 2, without the stones' state, are still read) | `RegionFile` (the server) | server, `save_check.py` | One per 512 m cell (`RegionCells`: cell (0, 0) at the south-west corner, index = floor((coordinate + extent/2) / 512), clamped), holding the entities whose position lies in it and, since version 2 (M1.5b), the layer diffs of the raster cells whose centres lie in it. Little-endian: magic `EG2R`, u16 version, i32 cell x, i32 cell z, f64 cell size, u32 entity count, u32 layer-diff count (none in version 1), u32 CRC-32 of the records and diffs, then per entity u64 id, the key as a u16 UTF-8 byte length and the bytes, f64 east, up, north, f32 yaw, i64 spawn tick, u8 component mask (1 item), and for an item u8 resting and f32 fall speed and, since version 3 (FP.3), f32 its own mass kg, f32 edge, f32 platform degrees and u16 flakes taken, zeros for a thing with none of its own, as the wire carries them; then per diff u8 layer (the tile layer's byte: 5, the loose layer, the only one yet), u16 row and u16 column of the world's raster, and for the loose layer u16 sticks taken and u16 cobbles taken, a bit for each index. |
| `digest.txt` | the lines of `WorldDigest.World` | `WorldSave.cs` (the server) | tests, `save_check.py` | One hex FNV-1a 64 of these lines in this order: `clock <nanohours>`, `tick <n>`, one line per body sorted by name (`<name> <micrometres east> <up> <north> g|a w|d c|s`), then for each founder by name who carries something or has chosen a hand `hands <name> <place>` and one line per thing by place (`carried <name> <place> <id> <key>[ stone <milligrams> <millionths of edge> <microdegrees platform> <flakes>]`, M1.5a; the stone's state since FP.3, only when the thing has a mass of its own, so an older world keeps its name), one line per entity in id order, leaving out one killed or taken up in a tick not yet ended (`entity <id> <key> <micrometres east> <up> <north> <microdegrees yaw>[ item r|f <micrometres per second>[ stone <milligrams> <millionths> <microdegrees> <flakes>]]`), one line per cell something was taken from, by row and then column (`taken <row> <col> <sticks mask> <cobbles mask>`, M1.5b), `next_entity <n>`; every number a whole count of its resolution, rounded half to even. |
| `save.commit` (`eg2.save_commit`, in the world folder only while a save is part of the way through) | 1 | `WorldSave.cs` (the server) | `WorldSave.Recover`; `save_check.py`, which refuses one left at rest | M1.3b (2026-09-13). JSON keys: format, version, place (an array of {file, crc32}: each file the save puts in place, by its name in the world folder with forward slashes, and the CRC-32 (IEEE) of its bytes), remove (an array of names: the files of cells that emptied, and version-1 player files superseded). Each file's replacement lies beside it as `<name>.part` until it is put in place. |
| The world folder's layers (`Saves/<world>/layers/`, M1.2) | each a version-2 raster | `WorldCreation.Create` (the server, at creation) | the server (`heights` as its terrain; since M1.7a each `capacity_<species>`, averaged over presence's 100 m squares), `drainage_check.py`, `census_check.py`, `region_stats.py --world`; M1.4 streams them | heights f32 m (the bake's ground with the sea floor); surface f32 m (the water's surface where water stands, else the ground's; since 2026-09-18 a creek or stream cell's surface stands above its ground by the flow's law); soil_depth u16 × 0.01 m; wetness u8 × 1/255; suitability u8 × 1/255; water u8 id (0 dry, 1 damp, 2 trickle, 3 creek, 4 stream, 5 lake, 6 swamp, 7 sea); overstory, understory u8 id (`PlantSpecies.All` index + 1, 0 none); topology u32 flags (1 sea, 2 beach, 4 dune, 8 wetland, 16 forest, 32 heath, 64 crest, 128 cliff, 256 shore platform, 512 lake, 1024 creek); stone u8 id (`StoneType.All` index + 1, 0 none); cover u8 (the low six bits a `GroundCover` — 1 sea, 2 fresh water, 3 sand, 4 dune sand, 5 rock, 6 bare earth, 7 heath, 8 bracken, 9 sedge, 10 grass, 11 forest floor, 12 swamp floor — and the top two the quarter of the land's own wetness); stand u8 (M1.6a: 0 where no trunk stands, else the top three bits the tall plant as `StandCodes.Tall` index + 1 — blackbutt, bangalay, old-man banksia, coast banksia, swamp paperbark — and the low five its height in steps of 1.25 m, both in the sidecar's legend); loose u8 (M1.6a: the low four bits the sticks lying on the cell and the high four its cobbles, each to 15); catchment u32 cells; shore_distance, fresh_water_distance, stone_distance, fibre_distance, firewood_distance, shelter_distance u16 m (65535 none); capacity_<species> u16 × 0.01 per km²; wake_score u8 × 1/255. `census.txt` beside the folder is prose, not a format. |
| `water_bodies` (a bake input beside `heights`) | a version-2 raster, u8 id | `Tools/data/bake_water.py` (OpenStreetMap through Overpass; THIRD_PARTY_NOTICES.md) | `WorldLayers` at creation | 0 outside every outline, else the body's code; the sidecar's `bodies` legend lists code, `osm` way, `name` and `kind` (lake, wetland, salt), and `fetched_utc`. |
| `loading.json` (`eg2.loading`, optional `-eg-loading-record <dir>`) | 1 | `LoadingRecorder.cs` | `loading_check.py` | Keys: format, version, outcome (ready or failed), error (empty on success), returned_to_menu (true after the failure scenario presses Back and Bootstrap rebuilds the shell), preparation_s (real seconds to prepared, 0 on failure), updates (main-thread updates during preparation), samples [{t real seconds, update count, stage}], stages [progress strings in received order], frames [{file relative path, width, height, stage, t}]. Samples are taken every 0.25 s and at preparation completion. The new-world proof requires more than 2 s of samples, more than 10 updates and no sample gap over 2 s; this detects a sustained preparation freeze, not a gameplay frame-rate guarantee. `Tools/world/loading.py` writes separate `process.json` {exit: integer} from each child exit status and an `Artefacts/loading/latest.txt` absolute directory pointer only after all three launches finish. Existing `run.jsonl` records playable frames; the verifier also reads actual PNG dimensions and pixel variation. |
| `run.jsonl` (`eg2.run`) | 1 | `RunLog.cs` (Engine), written by the recorder, the scenario runner and the server host | `corpus_check.py`, `join_check.py`, `fauna_check.py` | One JSON object per line. Line 1 is the header: `format`, `version`, then what the run says about itself (`role` client or server; scenario, region, seed, name, mode, address, port, the shaping `latency_ms`/`jitter_ms`/`loss_percent`/`send_cap_bytes_per_second`, started_utc, unity, terrain, duration_s, cycles; since M1.Bb, 2026-09-16, build_label, build_commit, build_dirty and build_utc from the `version.json` the install tool wrote beside the player, empty and false where there is none). The corpus's walker (2026-09-16, additive): `sample` adds water (the body's water as the server last told it, 1 full), thirst (the word for it, empty while fine) and drinking (true while off the loop for water); `segment` is "water" while the founder is off the loop for a drink, and the loop's legs "mob" and "pair" end where it passes an animal group's place; `drink` once a visit to water (outcome: drank, unreached, no answer, or the server's refusal; last, the last answer; presses; drinks, the presses answered Done; water_before, water_after, seconds; water_east, water_north where the water was found; east, up, north where the founder stood); `died` when the server tells the founder they died (cause, local_hour, clock, water_loss, core_c, air_c, wind_ms, east, north where they fell, segment, sentence); and `end` adds drinks, drinks_done, deaths and lowest_water. `corpus_check.py` reads them. Every later line starts `t` (real seconds since the run began), `tick` (the server tick last known to the writer, −1 before any), `kind`, then the record's fields. **Client kinds** — M1.A: `frame` (file, width, height, east, up, north, yaw_deg, pitch_deg, grounded, corrections; since M1.4h wind_ms and wind_from_deg, the weather's wind at the frame), `error`, `exception` (message, stack), `end`. M1.4e (additive): `build` (what "ground" or "colour", ix, iz, worker_ms, main_ms, before_ms the frame's spend when the budget allowed it to start, frame_ms the spend when it landed, and the main thread's cost split as texture_ms, heights_ms, object_ms, water_ms), and `interactive` gains worst_streaming_ms. M1.6a (additive): `timing` (seconds, frames, width, height, median_ms, p95_ms, worst_ms, hidden the `-eg-hide` list, trees held, stand_cpu_ms the median of what choosing and handing over the stand cost the main thread), written once by the recorder after its frames when run with `-eg-hold <seconds>`: the founder turns a full circle and every frame is rendered into a 1080p target and waited for. M1.6c (additive): the first-frame scenario's `end` adds understorey (the tufts placed round the founder, `shape:count` comma-separated), cover (the cover's cells within the same 45 m, `name:count`) and understorey_ms (what placing them last cost the main thread). M1.6d (additive): that `end` adds far_trees (the far trees placed over the whole region); and run as a development game with `-eg-lookout <metres>` (`Tools/world/vantages.py --lookout`), the scenario flies the founder up that far over where the turn left them and writes four more `frame` records, lookout-north, lookout-east, lookout-south and lookout-west, each looking 8° below the horizon. M1.5a (additive): the recorder's `carry` scenario (`-eg-scenario carry` with `-eg-items`, run by `Tools/world/carry.py`) writes the same `frame` records and an `end` that adds picked_up, put_down, kept_carried and answers (the server's outcomes in the order they came, comma-separated); M1.5b's `litter` scenario (`-eg-scenario litter`, the litter's own sticks, no `-eg-items`) writes the same records and the same end. M1.5c (additive): each of the recorder's scenarios adds to its `end` footfalls and underfoot (the grounds they fell on, `name:count` comma-separated). M1.5d (additive): the recorder's `wade` scenario (`-eg-scenario wade`, the founder stood on a lake's shore by `Tools/world/wade.py`) writes the same `frame` records and an `end` that adds waded, wading, land_speed, wading_speed and depth_m (the streamed depth where the founder stopped); its `controls` scenario (`-eg-scenario controls` with `-eg-items`, run by `Tools/world/carry.py --scenario controls`) writes a `control` record per check (name, scheme, control the binding pressed as the controls asset states it, before, after, ok, saw) and an `end` that adds checks and failed. M1.5e (additive): the recorder's `swim` scenario (`-eg-scenario swim`, the founder stood on a lake's shore by `Tools/world/wade.py --swim`) writes the same `frame` records and an `end` that adds swam, swimming, swimming_speed, eye_over_water_min_m (the lowest the camera came over the water's surface on the way in) and strokes beside depth_m; run with `-eg-dev` (`carry.py --dev`), the `controls` scenario adds its flight's checks as `control` records of the same shape. M1.B: `welcome` (rejoin, session, seed, region, tick_rate, spawn_east/up/north, world_total_hours), `interactive` (rejoin, since_connect_s, tiles_held, tiles_from_cache, tiles_refused, tiles_built, bytes_received, bytes_sent, rtt_ms, east, up, north), `segment` (name, index, lap, east, north), `correction` (sequence, reason, displacement_m, segment, east, up, north), `sample` once a second (east, up, north, grounded, wading, speed, segment, remotes, rtt_ms, bytes_sent, bytes_received, corrections, interactive, connection, fps), `mirror` once a second per remote (session, latest_tick, digest, latest_east/up/north, east, up, north, at_tick, interpolated, estimated_tick, states_held), `cut` (cycle, east, up, north, mirrors), `rejoin` (cycle), `dropped` (reason, severed), `end` (exit, samples, errors, corrections, interactive_s, rejoin_interactive_s, cuts, rejoins_interactive, laps, skipped_waypoints, moves_sent, east, up, north). M1.7a (additive): `sample` adds entities_shared (the entities the client holds inside its founder's interest radius, the Welcome's, and inside every other player's it holds, each where their newest state puts them). **Server kinds** (M1.B): `join` (session, name, remembered, east, up, north), `leave` (session, name, reason), `correction` (session, name, reason, east, up, north), `bodies` once a second per session (session, from, digests[] one per tick from `from`, positions[] east, up, north per tick), `bandwidth` once a second per session (session, name, sent, received, sent_total, received_total, moves_accepted, corrections), `memory` once a second (heap_bytes, working_set_bytes, players, tiles_served_bytes, dropped_seconds, digest), `ticks` once a minute (count, over_interval, max_ms, mean_ms, p95_ms, heap_collected_bytes, working_set_bytes), `pause`/`resume`/`digest` on a console command (digest, paused), `end` (seconds, players, digest, dropped_seconds). M1.7a (additive): `memory` adds animals (the animals held); `ticks` adds stand_up_updates, stand_up_mean_ms and stand_up_p95_ms (the updates whose steps stood the animals up, by the host's clock) beside other_updates, other_mean_ms and other_p95_ms (the rest that released a step); `fauna` every ten seconds (count; founders[] east and north of each founder the animals are stood up round; ids[], keys[], positions[] east, up and north of each animal, and poses[] 1 resting, 2 grazing or, since M1.7c, 3 fleeing); M1.7c (additive): `flight` as a group takes flight (species, the kind's number as an animal's id carries it, written as `kind` until 2026-09-16 when that key was the record's own and the number was lost; cell_x and cell_z of its square, east and north of the group, founder_east and founder_north of the founder it runs from, distance_m from that founder to the nearest member, bearing_deg it runs on). An animal's id is of the reserved range: the top bit set; the kind in the next seven bits (`AnimalSpecies.All` index + 1: 1 the eastern grey kangaroo, 2 the pied oystercatcher); the square's east and north indices (`floor(metres / 100)`) each plus 2^23, in twenty-four bits apiece; and the member of its group in the low eight. FP.1 (additive): the recorder's `drink` scenario (`-eg-scenario drink` as a development game, run by `Tools/world/drink.py`) writes a `thirst` record at every word the server has of the founder's water (why: start, told or word; water, loss, level 0 fine to 4 collapsing, word), a `drink` record per drink asked (water_kind fresh or sea, outcome, water_before, water_after, looked_at, east, up, north), the same `frame` records, and an `end` that adds very_thirsty, thirst_seconds, clock_scale, drank, water_before, water_after and salt_refused; `thirst_check.py` reads them. FP.2 (additive): the recorder's `night` scenario (`-eg-scenario night` as a development game with `-eg-no-bridge`, run by `Tools/world/night.py`) writes a `night` record where the founder stands for the night (east, up, north, stick, wake_east, wake_north, speed_ms), a `warmth` record at every word the server has of the body (why: start or told; water, core_c, cold 0 well to 4 severely hypothermic, cold_word, thirst_word, and the sky the client works out: air_c, wind_open_ms, cloud, humidity, sun_elevation_deg; speed_ms, east, up, north), a `cold_reached` record at the first "cold" (cold, after_s, core_c, lowest_core_c), a `dawn` record when the sun rises or the night itself kills (sun_up, after_s, coldest_word, coldest, died_in_the_night, core_c, lowest_core_c the night's own lowest, sun_elevation_deg), a `died` record (cause, local_hour, clock, air_c, wind_ms, loss_w, production_w, core_c, water_loss, east, north, sentence), a `stick` record when the stick is found again (id, east, up, north, fell_east, fell_north, distance_m, resting), the same `frame` records (cold, dawn, died), and an `end` that adds cold, cold_after_s, clock_scale, lowest_core_c, sun_up, died_in_the_night, coldest_word, died, cause, sentence, respawned_at_wake, wake_distance_m, stick_lies_where_fell and stick_distance_m. Every record of the scenario but the frames carries world_hours, the world's clock on the client at the moment of writing (it follows the server's by pongs and lags a change of rate by a second or so, so it places a record in the night but is not the check's time base); a number that is not one (NaN) is left out of a record rather than written, JSON having no form for it. `cold_check.py` lives the night again from the words by the server's ticks they carry (a tick a twentieth of a real second, the game's forty-eight world seconds times the rate), once in the open's wind and once in still air, and holds the recorded core between the two. **Server kind** (FP.2): `death` (session, name, cause, local_hour, air_c, wind_ms, loss_w, production_w, core_c, water_loss, east, north, sentence). FP.3 (additive): the recorder's `knap` scenario (`-eg-scenario knap` as a development game, run by `Tools/world/knap.py`) writes a `set_down` record for each stone the panel's deed set down (what, id, east, up, north), an `aim` record when the core is under the crosshair (core, aimed, line), a `blow` record per press of the work button (name: tap, half or full; hold_s; wind_up as it went on the wire, 0 to 1 in 255 steps; sequence; hammer_id, hammer_key, hammer_stone, hammer_mass_kg; core_id, core_key, core_stone, core_mass_before_kg, core_platform_before_deg, core_flakes_before; water; outcome, note; core_gone, and unless gone core_mass_after_kg, core_platform_after_deg and core_flakes_after; and when a flake came away flake_id, flake_key, flake_mass_kg, flake_edge, flake_usable, flake_resting, flake_east, flake_up, flake_north), the same `frame` records (flake-lying, flake-held), and an `end` that adds hammer_held, offered, blows, flaked, bounced, flakes_seen, usable_flake and flake_held; `knap_check.py` reads them. M1.E (additive): the recorder's `idle` scenario (`-eg-scenario idle`, run by `Tools/world/idle.py` on a copy of its world) writes `idle_start` (server_hours) when the game is left alone, `asleep` (asleep, after_s, server_hours) when it sleeps or the minute and its margin run out, `woke` (woke, after_s, and what the player saw of the key: touched_action_found, touched_action_enabled, touched_controls, bound_to_this_keyboard, player_saw_touch, action_pressed, key_pressed) after a key of the scenario's own, one `frame` (paused), and an `end` that adds slept, slept_after_s, woke, clock_rate_hours_per_s (the server's clock's rate read from its pongs while awake), clock_moved_hours (across the sleep, by the pongs either side), clock_would_have_moved_hours (at that rate over the same real seconds), clock_stood (moved under a quarter of would-have), real_seconds_across, frames_asleep_per_s (the frames drawn while held asleep, over those seconds), frames_held (that rate within half again of the runtime's sleeping cap), after_wake_rise_m (the founder's rise in the half second after the waking key) and stayed_put (under 5 cm: the key did nothing else); the `controls` scenario adds `control` records for the developer's switch (F2 off and on, the panel's key opening nothing while it is off) and the free look (Alt held turning the view and not the body, let go bringing the view home). M1.5h (additive): the `dune` scenario (`-eg-scenario dune`, run by `Tools/world/dune.py` on a copy of its world) writes `dune` (found, face_deg, east, north, from_m: the steepest face of 18 to 34 degrees within 150 m of the founder, on the client's ground), `dune_down` and `dune_up` (slope_deg, speed_ms, moved_m: the slope walked and the speed held past the first 1.2 s), two `frame`s (dune-top, dune-bottom) a `slide` (M1.5i: found, face_deg, east, north, from_m of the steepest face over the walkable limit within 300 m, lowest_under_m the feet were read below the ground while walking onto it, kept_feet under 5 cm, walked_into_deg, steepest_under_deg the steepest ground under the feet meanwhile) and an `end` that adds found, face_deg, slope_down_deg, speed_down_ms, slope_up_deg, speed_up_ms, flat_ms, walked_down (over 0.5 m/s and under the flat walk, on 15 degrees or more), slower_up, slide_found, slide_face_deg, slide_lowest_under_m, slide_kept_feet and slide_steepest_under_deg. |
| `Data/global/atlas/<layer>.json` + `.r16`/`.u16`/`.u8` (`eg2.atlas`) | 1 | `Tools/atlas/derive.py` (WG.0b, 2026-09-22) | `Tools/verifiers/checks/atlas_check.py`; the choosing of a place (WG.0) | A raw little-endian array of the header's `dtype` (int16, uint16 or uint8), row 0 the north edge (+90N), column 0 the west edge (-180E), `cells_per_degree` cells a degree; the header names the source file and its SHA-256, the unit, the nodata value, the min and max, and how the layer was derived; `ecoregion_names.json` beside the ecoregion layer maps its ids to names. |
| `Tools/atlas/probes.json` (`eg2.atlas_probe_set`) | 1 | Claude, before a lookup runs (WG.0 promise 3) | `Tools/atlas/choose.py --probes`, `atlas_check.py --probes` | Eight named coordinates with expectations written from references named beside each; a revision after a failed check is recorded in the open as `first_pinned` beside the new expectation, never silently. |
| `Artefacts/atlas/probes-<stamp>.json` (`eg2.atlas_probes`) | 1 | `Tools/atlas/choose.py --probes` | `atlas_check.py` reads the layers itself, not the report; the report is the record of a run | Every field of every probe with its value, unit, missing flag, source, resolution and derivation, and the query times cold and warm. |
| The corpus folder | 1 | `Tools/corpus/run.py` | `join_check.py` | `Artefacts/corpus/<stamp>/<scenario>/{server,A,B}/run.jsonl` with `player.log` and `console.log` beside them; scenarios `join-a`, `join-b`, `walk`, `walk-solo`, `rejoin-held`, `rejoin-live`, `soak`; `latest` points at the newest; `summary.json` is the harness's own and no verifier reads it |

A row moves from "not yet" to a version number in the commit that first writes the format; the version field is
mandatory in every file from the first write.

## 11. Verification

- `dotnet test Engine/EarthGame.slnx` — the engine-free suite; exit code is the verdict; never piped.
- `Tools/gate/run_gate.py <gate>` — refuses when a verifier is missing or a stub, then runs the gate's
  steps; `Tools/hooks/pre-push` runs the engine suite, and the Unity compile check when present (its third check,
  the verifier-lane authorship rule, went with CANON ruling 17).
- Unity edit-mode tests (`Tools/unity/editmode.py`): the settings checklist, the controls asset, every spawnable
  definition's look, the terrain tile builder, the stand's meshes, the `MaterialPropertyBlock` scan. Built-player scenario runs with frames and `run.jsonl` from M1.A;
  since M1.5d the `controls` scenario presses every action's binding in a built player and checks what each did, and
  since M1.5e, run as a development game (`-eg-dev`), it flies the fly key too; the `swim` scenario walks the founder
  from a lake's shore into water over their head and holds the camera's height over the surface all the way in.

## 12. Decision log (agent decisions; owner rulings are in CANON.md)

- **2026-09-14 — Preserve saved integers and enforce the complete layer promise (WG.0c, Codex).** Keep the existing
  numeric JSON fields so old saves with exact written seeds remain readable; retain large integer tokens before
  floating-point conversion, and use exact access for saved seed/tick/allocation counters. Validate a layer's
  existence, hash, role and geographic frame against the world, including layers not currently consumed at runtime.
  Different pitches remain legal; region, centre and extent do not. Check each loaded grid once and release the
  unused ones instead of retaining a second whole world. A legacy absence is allowed only without a manifest promise.
  M1.3d's lock may leave an empty directory after refusal, so WG.0b's checker now requires zero written world files,
  rather than no directory. No valid terrain or wire/save schema changes; William was told before implementation.

| Date | Decision | Why |
|---|---|---|
| 2026-09-07 | Hand-rolled protocol over the engine-free core; FishNet is plan B | FishNet/NGO/Mirror require a Unity server process, forfeiting the engine-free server, its dotnet tests and the pure-.NET dedicated server. Kept by the owner at the checkpoint (CANON ruling 19, 2026-09-10) |
| 2026-09-07 | Client-authoritative movement validated by the server; mover as a pure function in the engine | What Minecraft and Rust actually ship; prediction later is a switch, not a rewrite |
| 2026-09-07 | No landform evolution on real data | With 5–30 m data the valleys are already the record; erosion would re-carve a real place |
| 2026-09-07 | Fauna materialised from the presence function inside the interest radius | One source of expectation, one authority for what exists |
| 2026-09-07 | Frame budget at 1080p-internal with STP to 1440p | Derived from the GTX 1660 SUPER, not from the display |
| 2026-09-07 | Bherwerre Peninsula as the first region | Research against the owner's criteria (permanent fresh water, confirmed knappable stone, rock shelters, two coasts, a 105-year station) |
| 2026-09-08 | A peer-stated close during the handshake is a refusal (`TransportEvent.ReasonFromPeer`) | Over UDP the close overtakes the Refused message; the loopback test proved it |
| 2026-09-08 | The fixed-step accumulator releases a step within 1 ns of a whole one | 0.15 − 0.05 − 0.05 is not 0.05 in binary; the third step of a 150 ms update never fired |
| 2026-09-08 | `LocalFrame` maps local metres to latitude and longitude by the equirectangular small-angle rule about the region centre, the same rule the bake samples with; v1's spherical displacement is dropped | The two disagreed by ~1.5 m at the corners of the 8 km box; with one rule a raster cell's (row, column) is local (east, north) by definition, and a Python-written fixture pins the engine to it |
| 2026-09-08 | The engine carries its own strict JSON reader/writer (`Json.cs`) | Neither System.Text.Json (not in .NET Standard 2.1) nor Newtonsoft (a Unity package) is available to files both Unity and dotnet compile; the sidecars, `world.json` and the run logs are small documents |
| 2026-09-08 | An owner verifier, once present, is run as a gate step for its exit code | Presence alone was found to pass a gate on 2026-09-08; a verifier that is never run is a stub with extra steps |
| 2026-09-08 | Every verifier Claude writes (CANON ruling 17) uses an algorithm or data source independent of the tool it checks, names its reference and source, prints both numbers beside the verdict, and never imports the tool's code | The owner withdrew the two-author rule; independence of method and a printed comparison are what remain of the guard against v1's fabricated verdict, and they are stated in each file's docstring and in `Tools/verifiers/MANIFEST.json` |
| 2026-09-08 | Unity Terrain draws non-instanced until profiled (M1.4); the terrain material keeps its instancing variants regardless | The owner's first play showed "a void". The built player's instanced terrain drew nothing (instancing variants stripped from the build), and with the variants kept it drew without the sun (a probe sphere beside it was lit, the ground stayed sky-blue); the non-instanced path draws lit. Found by bisection with `-eg-hide` and `-eg-probe`, never by a test, an edit-mode test cannot see a build |
| 2026-09-08 | The client draws the whole region at coarse posts under the near tile, the 64 km surround as the far skirt, and a sea plane a few kilometres wide around the founder, ahead of M1.4's streaming | One kilometre of bare coastal plain was a void to the owner's eyes; the coast, the hills and the bay are what make the place a place. The far skirt and the sea were already in §3 and §8 |
| 2026-09-08 | `-eg-plain`, `-eg-hide`, `-eg-probe`, `-eg-shell` and F12 screenshots are diagnostics that stay in the client | Every one of them found something a test could not; a frame the owner can send is the only way his eyes reach Claude's |
| 2026-09-08 | The tile grid is laid out from the region's extent, which the Welcome carries, not from the region id | A client that does not know a region (the fixture regions of the tests; a region baked after the client shipped) must still stream it; the grid is arithmetic on the extent and the two ends compute it separately |
| 2026-09-08 | Tiles travel as int16 centimetres with row deltas and deflate, chunked at 16 KB over the reliable channel, and are cached on disk by checksum | A kilometre of 4 m posts deflates to a few tens of kilobytes; centimetres are what the wire can promise and the digest compares; a rejoin with a warm cache costs a header per tile |
| 2026-09-08 | The mirror is sampled three ticks behind the estimated server tick and holds still past its newest state | Extrapolation invents motion the server never saw; the delay keeps a state either side of the sample under the harness's loss, and N4 measures the residue against the server's log |
| 2026-09-08 | A paused server accepts no moves; the same name superseding a live session is the rejoin path | The held-tick digest row is only meaningful if the bodies hold with the clock; a cut cable leaves the old session alive until the transport's timeout, longer than N3's three seconds |
| 2026-09-08 | The server's instruments live in the host, which passes its clock in | The engine-free server reads no clock and no process; the host is the place that has both, and the tick window, heap and working set are written from there |
| 2026-09-08 | The harness times the held-tick pause from the player's own log rather than from a schedule | The player's clock starts after the process does; a pause sent by the wall clock would land seconds off the cut, and the row it serves compares what the server wrote at the pause and the resume |
| 2026-09-08 | A movement report is judged over its sequence spacing, bounded by the real time the session has banked (a five-second cap) | The first corpus run at 100 ms ± 20 ms corrected a legal sprint thirty-one times in forty-five seconds: jitter delivered two reports in one server tick and the old rule read their spacing as one tick. The cap keeps a client from claiming time it never had; the burst it permits is five seconds of running at the honest average |
| 2026-09-08 | The founder's body is frozen until the tile under it is built, and a corrected body the server holds below the client's ground is lifted onto it | In the second before the tiles arrived the body fell onto the sunk coarse terrain and was under the tile when it arrived; the server's metre of ground tolerance then held it there and corrected every report |
| 2026-09-08 | Streamed tiles are built one Terrain per frame, the one under the founder first | Nine in one frame stalled the client for most of a second: the first mirror sample found one state and an estimate fourteen ticks past it |
| 2026-09-08 | The digest names numbers as whole micrometres and nanohours, never as a printed double | The first full corpus had one mirror digest in 310 disagree with the server's for a body whose bits were identical on both ends: Unity's Mono printed −2639.4949265549981 with G12 as …656 where .NET printed …655. An editor probe reproduced the client's digest exactly |
| 2026-09-08 | The snapshot that follows a Welcome goes out after the next step (at once while paused) | A snapshot sent at the Hello carried a body a move had just changed, stamped with the tick before it; sent after the step it carries exactly what that tick's digest names, and a paused server, which accepts no moves, can send it without waiting |
| 2026-09-09 | The sea is a sink of the drainage at the datum, and the soil model reads the filled surface | v1's network had no sea and drained everything to the grid's edge; fed the raw ground, the wetness test contradicted the D8 the water followed |
| 2026-09-09 | The sea's floor is a rule (one in twenty from the shore, to 30 m) until the Geoscience Australia grids are read | The tiles carry no bathymetry; wading needed a floor at the shore, and the shelf off this coast is of that order (`region_stats.py --world` checks the rule, not the sea) |
| 2026-09-09 | Lakes are found before the drainage and are its sinks; the ground's flats are one level with a rim, and the mapped outlines (OpenStreetMap, `bake_water.py`) stand at the median of the bake's ground inside them | A fill that had to spill read Windermere's closed basin as a 106 ha pond thirty metres deep; the tiles are noise over these lakes (12 to 50 m inside Windermere's outline, a bowl inside McKenzie's), so the outlines are the only evidence of where the water is, and the median is the level the ground itself supports |
| 2026-09-09 | The wake's criteria grade from the thing itself to three times the stated distance; sea, the region's edge, low ground, steep ground, water underfoot, sodden ground and the shelter rock itself are no place to wake; ties go to the least wind exposure | Threshold factors scored a whole coast at 1.000 and woke the founder at the first cell in row order on the north edge; then in a creek mouth on a platform; then at a paperbark swamp's edge. Each rule is named in the census so the owner can call it wrong |
| 2026-09-09 | `Region`'s stated wake stays at 35.159°S 150.6485°E although Destination NSW and OpenStreetMap put Cave Beach 2.1 km east of it | CANON ruling 8 names the number; the corpus scenarios' loop is laid out from the spawn and M1.B's numbers were run on that ground; `census_check.py` keeps the row red and DEBTS.md names the owner's ruling. The point itself was withdrawn by CANON ruling 20 (2026-09-10) and left the code the same day |
| 2026-09-09 | The oystercatcher forages by the tideline: its water factor is the shore's, not fresh water's | A shorebird that needed a creek within range had no capacity on the beach it lives on |
| 2026-09-09 | The gate creates a world under `Artefacts/worlds/gate` (`Tools/world/create.py`) and the verifiers read that folder | A verifier that read a hand-made probe would check yesterday's code; the folder is rebuilt from the current host on every gate run and lists its layers with their sizes and checksums, as the contract asks to see them |
| 2026-09-09 | An entity's fields carry the tick they changed at; a session's interest set carries the tick of its last send per entity; what is sent is the fields stamped at or after that tick | A dirty flag cleared after a broadcast is cleared for the session whose byte budget deferred the message too; stamps are dirtiness per viewer, and the budget defers without losing |
| 2026-09-09 | Leaving a session's interest takes 50 m more than entering it, gones never wait on the budget, and a state that carries an item's rest goes reliably | An entity on the radius would flap in and out every tick; a viewer told nothing of a death draws a ghost; the rest is the last state an item ever sends, and an unreliable last state can leave a cobble hanging in the air for good |
| 2026-09-09 | The player file moves from JSON (version 1) to binary (version 2) with wading and stance | Version 1 dropped both, so a body saved wading came back dry and the round trip's digest differed; a format that cannot round-trip its own digest is changed by version, not patched in place |
| 2026-09-09 | Definition keys are the tables' names as slugs (`animal/eastern-grey-kangaroo`), hashed FNV-1a 32, the catalogue refusing a collision at load | A key read by a person and a hash carried by the wire, with the collision found on the first run rather than as a founder holding the wrong thing |
| 2026-09-10 | Standing water is drawn from the depth streamed over each tile, merged along a row into one quad per run; the sea keeps its plane and only water above the datum is drawn from a tile | Standing water is flat, so a kilometre tile under water is a quad a row rather than sixty thousand a tile. The sea has had a plane at the datum around the founder since 2026-09-08 and two surfaces at one height would fight for it; drawing the sea from the layer that knows its shape is left to the look, with the debt stated |
| 2026-09-10 | A lake covers only the ground beneath its own surface, both for a mapped outline and for a flat read off the ground | The flat was made water at its patch's median, so about half of a dished flat held water under its own bed: 5.6 ha of the Bherwerre world, by up to 0.486 m. Found by measuring what a client would be sent, since the depth over the ground is what travels |
| 2026-09-10 | One function opens a world for the game and the host, refusing terrain it cannot read rather than reaching for the region's bake | The host and the game each had their own reader, and both answered a missing or corrupt heights layer by quietly standing the world on the bake: its ground changed under its players, and its digest with it. The lenient `TryLoadTerrain` is gone and a source rule keeps the second path from growing back |
| 2026-09-10 | World preparation runs off the Unity main thread and reports stage boundaries | The layer chain froze New world before a frame could be shown; a loading panel needs both worker progress and a live main thread. Saved terrain corruption now fails explicitly instead of silently substituting a bake. |
| 2026-09-10 | The client is sent what covers each post, not what the cover is made of: one byte, the cover and the quarter of the land's own wetness | Priced on the nine tiles around the wake, wetness is 476 KB and soil depth 501 KB against the ground's own 418 KB — a continuous field over noisy country does not compress — and the byte they come to is 96 KB. What is lost is the variation inside a quarter; what is kept is the country reading as itself |
| 2026-09-10 | The cover cascade asks the understory before the canopy | Asking the canopy first made a third of the peninsula one colour, because almost every cell here has some canopy over it. The ground under an open banksia is the ground layer; the canopy is drawn as trees when trees are drawn |
| 2026-09-10 | A tile is prepared on a worker and only Unity's own calls are left on the main thread, under a stopwatch that starts no work on a spent frame | Nobody had measured it: sampling a tile's posts is 21.9 ms and its colour map 7.1 ms against §8's 1.5 ms a frame, and the whole of it was on the main thread. What cannot move is `SetHeights` at 21 ms, which is Unity's own call and a debt; the budget's job is to stop a second tile joining it in one frame, and the run log now says what each tile cost so a check can hold it there |
| 2026-09-10 | A tile's posts are sampled from that tile, not through the field of held tiles | A tile's last post is its neighbour's first, and at the edge of what a client holds the neighbour is not held: the field answered NaN and 1,025 posts of every outermost tile were built from nothing. Neighbours share their edge posts, so reading the tile's own is the same number wherever both exist |
| 2026-09-10 | A tile's colour is its own terrain layer, one picture stretched once over the kilometre, and grain underfoot waits | The stock terrain layer can carry either a colour at the raster's pitch or a detail texture at its own, and blending two layers averages them, which washes the colour out. Measured on the frames: the ground at the boots varies by one level in 255 where the old tiled texture gave it grain. A material that multiplies the two is its own slice (DEBTS.md) |
| 2026-09-10 | A world made without a wake puts the founder at the region's centre | The region named a wake point until CANON ruling 20 withdrew it; a bare world (a test's, a save from before M1.2) still needs somewhere, and the centre is the one point the region already owns: the origin of its frame |
| 2026-09-10 | The corpus runs on a created world, a copy per scenario, and its walking loop is laid round that world's wake | The corpus's server ran on the bare bake and woke founders at the stated point the loop was laid round, while every real world since M1.2 wakes where the scorer chose, 4 km away. The new loop was found by a search over the gate world's layers, checked every 2 m, and walked: 0 corrections in ten minutes for each of the two players over the wire (100 ms ± 20 ms with 2 % loss, 200 ms ± 40 ms with 5 % loss) and for SOLO, two minutes of each on the bank and three on the shore, every N2 row of join_check green |
| 2026-09-10 | On sand the soil a plant reads is the soil formed there: none on a beach, and on a dune the soil model's depth less the salt wind's share | The soil model's depth is the loose material over rock, and under the beaches and dunes that is sand; read as soil, it kept the sand-binder off every dune and grew bracken, a plant of the forest floor, over half the ground within 100 m of the sea (M1.2b, against the Atlas of Living Australia's records) |
| 2026-09-10 | Coast banksia has a floor on exposure, at the salt wind of the dunes' inland edge | A salt specialist had nothing to lose inland: it held two fifths of the land at a median kilometre from the sea, where its records are at 392 m. It is competition, as spinifex's ceiling on soil is |
| 2026-09-10 | A tall plant stands on a cell as often as the ground suits the best of them | The draw named which tall plant held a cell and never none, so any tree that could stand took it: 99.9 % of the land stood under a canopy, the beaches included, where the park describes heaths, and nothing that needs full sun could win where a tree survived |
| 2026-09-10 | The ground cover asks the dune what grows on it before calling it sand | The park's dunes are held by their plants, and all 1,005 ha of the world's read as bare sand |
| 2026-09-10 | The plants are held against where people found them (`species_check`, a step of the M1.2 gate), and the rows the world still fails are owed by name | The records are the one source that never saw the rules. An owed row prints its numbers and fails the check the day it passes while still owed, so the owed table only shrinks; the check run on the world before M1.2b fails four rows, one of them an owed row that passed there |
| 2026-09-10 | The corpus loop is laid by a tool, `Tools/corpus/lay_loop.py` | M1.2b moved the wake 900 m north (east −1392 north 2804 on seed 1347). The search the first re-laying did by hand now reads any world's wake, lays the loop, surveys it every 2 m and writes it into `Routes.WakeLoop`; its first run crossed a swamp on the straight walk from the wake to the loop, so the lap starts at the nearest waypoint a dry, flat, straight walk reaches. Walked: 0 corrections in ten minutes for each of the two players over the wire and for SOLO, about two and a quarter minutes of each on the bank and two on the shore, every N2 row of join_check green (Artefacts/corpus/walk-m12b-20260910) |
| 2026-09-11 | What stands and lies on the ground is a layer until something moves it: trees, sticks and cobbles are placed when a world is created, saved as `stand` and `loose`, and streamed as code tiles; a thing becomes an entity only when something moves it | As entities, the 1,500 m interest radius would hold of the order of a hundred thousand trees a player (the nine tiles round M1.6a's four vantages hold 103,850 to 176,906), sent in a join snapshot with no byte budget, walked by `EntityStore.Within` for every session every tick, and made a GameObject each on the client; as codes they are two more tile layers (§5) |
| 2026-09-11 | Where a thing lies inside its cell is an integer hash of the cell (`StandLayout`), never a stored or floating-point position | The server and the client put every trunk, stick and cobble at the same centimetre from whole numbers alone, and a later verb can name the second stick of a cell |
| 2026-09-11 | The stand's bands copy a material asset with instancing on, and its shader measures the bands from an eye the client hands it | Made from the shader at runtime, the materials left no material in the build asking for instancing, and the build stripped the instanced variants (its log's variant counts showed it): the first player held its trees and none stood in its frames, as the terrain's did on 2026-09-08. `_WorldSpaceCameraPos` belongs to whichever camera a pass renders for; an eye handed in is the same in every pass |
| 2026-09-11 | A frame's cost is measured without a window: the recorder's `-eg-hold <seconds>` turns the founder a full circle and waits for the GPU every frame, and `-eg-hide trees` and `loose` part what those cost from the rest | §8's protocol asks for a visible window, which no automated run opens (the owner's rule), and a batch-mode player renders nothing to a screen it does not have, so its own frame rate measures only its scripts. Waiting for each frame puts the CPU's and the GPU's work end to end, so the numbers bound a pipelined frame from above, and the difference between two runs is what the hidden things cost |
| 2026-09-11 | What a frame draws of the stand is chosen by 64 m block: a far block only in the view, a near block out of the view into the shadows alone, and a near tree casting only where the sun's height lets its shadow reach the pipeline's shadow distance | Every tree of the nine tiles drawn far every frame cost 15.2 ms beside Windermere; far trees cast no shadow, so what is out of the view is nothing, while a near tree behind the founder still shades what is in front |
| 2026-09-11 | Far trees thin with distance, one in two beyond 500 m and one in four beyond 1 km with their crowns spread to hold the canopy's cover, and a far tree's corners are shared and shaded smooth | After the block choice the far band alone still took 9.5 ms beside Windermere; the thinned, shared-corner band took 3.4 ms, with the forest reading the same in the frames |
| 2026-09-11 | The stand's draws ask for no light or reflection probe, and its shader assumes uniform scaling and takes no lightmap or LOD fade | A far tree with a fiftieth of a near tree's corners cost a quarter as much, so the cost was per instance, not per corner; without a probe lookup and an inverse matrix for each instance the far band went from 3.4 ms to 2.2 ms (far trees cast no shadow, so the hours of the two runs do not bear on it) |
| 2026-09-11 | A tree's faces share their corners, each face lit and coloured from its leading corner (`nointerpolation`), and a whole tile whose trees are out of the view and whose ground is beyond the near band's reach is passed over | A near tree had three corners to every face, each drawn in the depth, colour and two shadow passes; sharing them took the near band alone from 2.1 ms to 1.6 ms beside Windermere with the frames unchanged at the same hours (`Artefacts/frames/m16a-7` against `m16a-8`), and the edit-mode `StandMeshesTests` hold every face to its leading corner |
| 2026-09-11 | A thing picked up leaves the world: its entity is taken, and it comes back with the id it always had when it is put down; what is carried lives in the carrier's player file | A carried entity kept in the store and moved with its carrier would touch the interest sets, the region files and every walk over the store, and would need hiding from everyone but its carrier. Out of the world, the store holds only what lies in it, and no id repeats, because a return is refused any id the store did not allocate or still holds |
| 2026-09-11 | The server commits every verb, measuring reach from the eye it holds; the eye's heights moved from the client's camera into `MoverConfig` | A reach the client judged would be the client's to stretch. With the eye in the engine the camera and the server measure from one number, and the verb line never offers what the server would refuse |
| 2026-09-11 | An item is drawn from the stand's own mesh for it (`ItemLooks`), and M1.3's prefab registry, its prefabs and their material went; the crosshair picks a thing by its mesh's bounds | The placeholder prefabs drew a spawned stick unlike a lying one (DEBTS). They had no colliders to keep, and a collider on a thing lying would stop a founder at a stick the server's ground does not know |
| 2026-09-11 | The controls asset is the one owner of every binding: code names actions, binds no key of its own, and the build refuses an asset that lacks an action | The input source bound keys of its own when the asset was incomplete, so a build that lost the asset moved on keys nobody had chosen; a missing action is now a refused build, an edit-mode test and a source rule rather than a quiet fallback |
| 2026-09-11 | Things are drawn between the positions they were stated at, three ticks behind the estimated server tick, as the other bodies are | A cobble dropped from the hand fell in 5 cm hops at the tick rate (DEBTS); the remote mirror's delay already keeps a state either side of the sample |
| 2026-09-11 | A scenario's pitch is the camera's, positive looking down | The scripted seam took a pitch as the mouse's push, which the body subtracts, so the first-frame scenario's "the ground ahead" looked 4° above the horizon. The existing scenarios' numbers were turned round with it, so their cameras point where they did |
| 2026-09-11 | What is taken from the loose layer is kept beside it as a bit for each index of each cell, and the layer never changes | A lowered count takes a cell's last thing, not the one the founder looked at, and moves nothing else only by luck; a mask names the very thing, leaves the rest where they lay, and keeps the layer's tiles and every client's cache of them good |
| 2026-09-11 | The server tells every client what has been taken, all of it at the join and each taking as it happens, rather than sending changed tiles again | A taking is a few bytes and a world has at most eight founders; a tile sent again would cost a kilometre of codes for one stick, and a client's cache would stop meaning the world as made |
| 2026-09-11 | A thing taken from the layer becomes an item under a new id and is never put back | Put down, it is where the founder put it, not where the layout would; one kind of thing lying about (an entity) where the layer and the store might otherwise hold the same stick twice |
| 2026-09-11 | A cobble taken up is the cobble of its cell's stone, a definition for each stone, weighing by its density | A cobble's stone decides what it can be knapped or hammered into (M2), and once it has left the layer its cell is the only witness; the plain cobble's 0.6 kg is taken as silcrete's density, 2,600 kg/m³ |
| 2026-09-11 | The stride's dip and sway are laid on the smoothed eye, and `-eg-still` takes them off | CANON ruling 18 keeps the camera smoothed vertically only until the owner, playing, says otherwise, and the plan's checklist asks for a footstep dip. Laid on top rather than in place of the smoothing, both cameras stay one launch option apart, and he chooses between them by playing (ruling 12) |
| 2026-09-11 | A footfall sounds of the cover the server streams, grouped for the ear (`Footing`), not of a surface the client works out | v1 worked its surface out in the client from the ecology; here the server has already said what covers each cell (M1.4d), and a second derivation would be a second owner of that fact, free to disagree with the ground's colour |
| 2026-09-11 | The client's sounds are made from arithmetic at load, and every automated run is muted where the view is built | The project imports no sound, as it imports no mesh (v1's rule, kept); muting in one place, for a windowless run or any a script drives, keeps a recording from ever being heard |
| 2026-09-11 | A founder wades in the depth the server streams, and the server holds no lower speed ceiling in water | The depth already travelled and was drawn (M1.4b, M1.4c) and moved nobody, so a lake was dry to the legs. A wader is only ever slower than on land, which the ceiling allows; a ceiling for water would correct founders wherever the client's ground and the server's differ by centimetres at the water's edge, the defect class of CANON ruling 13, to catch only a client that cheats, whom the ceiling already holds to a run |
| 2026-09-11 | The controls scenario presses the controls asset's own bindings on a keyboard, a mouse and a gamepad it adds to the player, naming actions and schemes and never a key | The asset is the one owner of which key does what (M1.5a) and a source rule refuses a key in game code; a scenario that pressed keys it named would be a second owner, and would pass on keys the asset no longer binds. Reading the asset's bindings and pressing them proves the whole road from a finger to the verb, whatever the bindings are |
| 2026-09-11 | The corpus loop carries a cliff's top edge, a shore platform and a wade into the sea beside the shore and the bank, and `lay_loop.py` budgets a lap into the ten-minute walk at the mover's own pace | Ruling 13 names the three places and the loop had walked none of them; the first loop laid with them was 1.9 km and a ten-minute walk would never have reached its shore |
| 2026-09-12 | Whether a founder swims is worked out every step, from where the body is and the water there, and no message, save or digest carries it | It follows from the water as wading follows from the depth; a flag on the wire or in the save would be a second owner of the fact, free to disagree with the water the next step finds, and would have changed the protocol, the player file and the digest for nothing any reader needs |
| 2026-09-12 | The eye is kept above the water: a swimmer floats with it a hand's breadth over the surface, a fall into deep water is taken before it goes under, and a founder does not crouch where the crouched eye would be under | Nothing is drawn from under the water, whose surface is drawn from above alone; diving, and a view from under the water, belong to the contract that draws one |
| 2026-09-12 | A developer's flight is the client's alone, and a server lets it stand only by its rules (`MovementRules.AllowFlight`: a SOLO game's under `-eg-dev`, a host's under `+server.dev 1`) | The server validates movement and never runs the mover (§7, §9), so a flight is one more report the validator is asked about, and a switch in its rules is the whole of the server's part; any server not started so corrects a flying founder back, as it would a cheat |
| 2026-09-12 | Trunks stop a founder on the client alone, and the server keeps no trunk rule | The server validates movement and never runs the mover (§7, §9), so trunks there could only catch a client that walked through a tree; against that they would weigh a correction at every disagreement between the server's idea of a trunk and the client's capsule — the defect class of CANON ruling 13 — at every one of the hundred thousand trees round a founder |
| 2026-09-12 | The trunk's taper moved out of the mesh into `StandForms`, and the bodies take their radius from it | What stops a founder and what is drawn are one fact; two copies of the taper would drift, and a founder would be stopped where no bark is |
| 2026-09-12 | The trunks near a body are read from the stand tiles each frame into a pool of capsules, rather than a collider for every tree | The nine tiles a client holds carry about 110,000 trees, and a body can touch two or three of them; a collider each would be a hundred thousand shapes for PhysX to keep, against sixty-four lent to whichever trunks are nearest |
| 2026-09-12 | The understorey is drawn from the cover byte, and no layer of its own is added | The cover already says what grows in the terms an eye reads — heath, bracken, sedge, grass, a swamp's floor, a canopy's litter — and the wetness quarter beside it says how thickly and how tall; a layer of understorey codes would be another tile a kilometre for the one thing the cover cannot say, which plant of a shape stands there, and nothing yet reads that. The M1.6 draft prices it if the grass tree must ever stand apart from the heath |
| 2026-09-12 | The tufts round the founder are placed on the main thread when they have walked six metres, not per tile on a worker as the trees are | A few hundred cells and a few thousand tufts are a fraction of a millisecond, and a disc round the founder needs no per-tile bookkeeping at all; the trees are prepared per tile on workers because a tile holds tens of thousands of them and they are drawn to a kilometre and a half |
| 2026-09-12 | A walker goes round a trunk (`RouteFollower`), and the corpus loop is not laid clear of them; only a waypoint is kept out of a tree | At this region's stand — 155 trees a hectare, 22 per cent of the ground within a body's reach of a trunk — a loop laid to miss every trunk cannot be laid: the first run of `lay_loop.py` with such a rule kept none of the shore, bank, cliff and platform legs out of 18,195 flat ones, and the wake's own corner of the graph reached neither sea nor bank. A founder who gains no ground steers off the line and tries the other side after a moment, which is what a person does; a waypoint inside a tree would still be unreachable, so the tool keeps its candidates out of the bark (restating `StandLayout.Place`'s hash and the stoutest share any form draws) |
| 2026-09-12 | A flight that ends under the ground sets the founder on it | Noclip (CANON ruling 25) can leave a body under the terrain, where the mover finds no ground to land on and a development server, holding no ground rule, never corrects it: the body would fall for ever. The client already lifts a corrected body out of the ground it holds, and this is that rule at that place |
| 2026-09-13 | The far forest travels as two tile layers the server works out from the stand when a tile is first asked for, and the world folder gains nothing | The stand is the one owner of where every tree stands; far layers written at creation would be a second copy, free to disagree with it when the rules change, and every world made before would lack them. A far tile costs the server about a millisecond to work out, against 7.8 ms for a tile of the stand itself (timed over the Bherwerre world's 64 tiles), and it is worked out once and kept |
| 2026-09-13 | A client asks for the far layers of the region's whole grid at the join and keeps them wherever it walks | The whole region's two far layers are 39,554 bytes as cached, beside 282,686 for the nine tiles of ground; asked for round the founder and let go like the rest, they would only move the line where the forest ends a kilometre further out |
| 2026-09-13 | A tile's far forest is drawn only while its own stand is not held: one far tree to a 40 m square, as tall as the square's trees on average and as wide as all their crowns would cover, in the far band's mesh and material, unthinned and casting no shadow | Two owners of one kilometre's trees would stand two forests in it; giving way tile by tile keeps one at a time. A square's trees seen from beyond a kilometre are a patch of canopy, and the far band already draws canopy as one clump on a trunk |
| 2026-09-13 | The far trees stand on the region's bake, the ground the coarse ring beyond the tiles is drawn from | A far tree stands where the client holds no stand tile, over ground drawn from the bake; stood on the streamed ground it would need tiles the client does not hold |
| 2026-09-13 | A world whose stand is not a whole number of cells to a 40 m square serves no far layers, rather than failing when one is asked for | The far squares are counted in whole cells so that no tree is counted twice; a stand of another cell size (a world generation of its own) then joins without a far forest, as a world without a stand joins without trees |
| 2026-09-13 | Animals are stood up within 500 m of a founder and taken away beyond 550 m, once a second, rather than to the interest radius | A kangaroo half a kilometre off is a few pixels high at 1440p; standing them up to the interest radius's 1.5 km would hold nine times as many, in the bytes every client is sent and in the update, for nothing a founder can make out. The margin keeps a group on the edge from being stood up and taken away by turns, as the interest margin does for what a client is shown |
| 2026-09-13 | N4's entity counts are paired by the server's second (a sample's tick less one, over the tick rate), no longer by the wall clock | The clients were paired on start times written to the whole second, and nothing came into or left the shared radius until animals did; once they did, a change parted the two clients whenever it fell in the second their clocks disagreed on (3 of 86 seconds in the quick soak of 2026-09-13, every count equal once settled). An update stamps what it sends with the tick after its last step and the animals are stood up on whole seconds, so paired by the server's second neither client can be caught either side of a change, and what is left is replication itself |
| 2026-09-13 | A state that carries an animal's pose goes reliably, as one that carries an item's rest does; and a standing animal is moved every second even to where it already stands | A pose changes at dawn and dusk and then holds for hours of the world's time, and the stamps count a state as seen once it is sent, so a change sent unreliably and lost would show the client that lost it a mob lying up through the evening. A position lost is put right by the next second's move, and moving an animal to where it already stands keeps that true of one the water holds still |
| 2026-09-13 | WG.0a (Codex): the existing global bake is inspected before any atlas input is acquired; a point reads its containing cell, and any cell crossing the legacy writer's Mercator coverage edge is unavailable | Containing-cell sampling exposes the bake's actual resolution without inventing fine detail, and repeated polar rows are not evidence of polar ground. No change to beta geography or formats; source acquisition and landscape generation remain separate work |
| 2026-09-13 | WG.0b (Codex): generation checks map roles and exact shared-grid identity before any layer work; invalid inputs raise InvalidDataException with both values | Equal array sizes did not establish equal places: shifted origins, wrong regions and the wrong integral layer were accepted. These inputs come from one bake, so silent resampling or a geographic tolerance would conceal an upstream mistake. A valid world's generated bytes stay the same; this adds no atlas acquisition, new save format or start policy |
| 2026-09-13 | M1.8a: a climate's curve is the day under its station's weather. Its mean is one yearly wave fitted to the station's twelve months, read as averages over their days; its range is each month's own, eased between mid-months so that every month's average is the one asked for; both ask for the station's extremes less what the weather's fronts add to a day's extremes as the Bureau reads them at 9 am; and the day is Parton and Logan's, coldest at dawn | v1 worked the mean out of latitude, which fits no particular coast, and its one cosine put the coldest hour at 03:00. A yearly wave for the range left August's half a degree narrow, where the lighthouse's widens 1.2 °C from July, and a second harmonic still 0.4. A station's months are its thermometers' readings, fronts and all: fitted to the lighthouse's extremes themselves, the weather's August range came out a quarter of a degree wide, and read over calendar days its nights 0.6 °C cold |
| 2026-09-13 | M1.8a: the rain's season changes how hard it rains as well as how often. The threshold, the scale and both yearly waves were fitted together on many world seeds to the lighthouse's twelve months of rain and of rain days, each weighed by a fourteen-year record's noise, under the contract's bounds on August and the year | From March to August the lighthouse's rain falls about four tenths harder on each rain day than from September to February, on about as many days, and no shift in how often fronts come can give that. Fitted on eight seeds, the months chased those seeds' luck, 11 to 27 mm a month; fitted without the bounds, August kept seven or eight rain days against the lighthouse's 6.0; with them, July and September come out with six or seven against 9.2 and 8.5, the price of one yearly wave for how often, until the Bureau's own tables say which months are a short record's noise |
| 2026-09-13 | M1.8a: the weather reads the world's continuous local time, and its tests hold the climate over many world seeds | v1 read its index at the day of the year, so its weather jumped to another front at each new year's midnight and repeated every year; and one world's August rain runs from a quarter of the month's mean to twice it, so a bound held on eight seeds holds their luck |
| 2026-09-13 | M1.8a: each side works the weather out from the world's seed, its region and its clock; none of it is sent or saved | It is a function of those three alone, which every Welcome already carries, and a copy sent beside them is a second owner of one fact |
| 2026-09-13 | M1.3b: a save writes its files aside and flushed, places one record of what it will put in place, and only then puts them in place; reading a world recovers its folder first | A save is many files and was written one at a time, each moved in by deleting the old first, so a crash could lose `world.json`, and a new world would be made over the old, or mix old files and new, and the folder was refused. One record placed in one move is the moment a save counts, and its CRCs tell a file already put in place from one never put there. It stays in the folder the world already has, rather than a folder for each save, so the layers and every tool that reads `world.json` stay where they are |
| 2026-09-13 | M1.3c: the game makes an autosave on its main thread and writes it on a worker, one save at a time; closing the game waits for the save being written, then writes its own in place | The making reads the world and the players, which the main thread steps, and once warm it takes about a millisecond; the writing touches only the bytes made and the disk, and since M1.3b's flushing it is most of a save's time. Made on a worker, the world would have to be copied or locked for it, and a copy is what the making already is. Two saves written at once would write aside over each other's files. The host saves in place, since it draws no frames |
| 2026-09-13 | M1.3c: a file operation of a save or a recovery is tried again while another program holds the file, each wait longer than the last, and then fails naming the file | On 2026-09-13 the built game's closing save could not replace a region file its autosave had written fifteen seconds before. Nothing in the game holds its files open, the same run repeated closed cleanly, and on Windows a virus scanner or the search indexer reading a file just written is the usual holder. M1.3b's record already made the failed save one the next load finishes; trying again lets the game finish it itself, and the file's name in a failure says where to look |
| 2026-09-14 | M1.3d: a name that would share a player file with a founder the world knows is refused at the door, rather than folded into that founder or given a file of its own | Folding William and william into one founder decides who a founder is, which is the owner's to decide. A file name that kept capitals apart, an escape before each capital say, would rename every existing file and still meet Jo Jo beside Jo_Jo. Refusing keeps every file where it is and loses nobody, and the refused player chooses another name or types the one the world knows |
| 2026-09-14 | M1.3d: a world folder is held by a file the opening program keeps open, shared with no one and deleted when closed, rather than by a marker written at opening and removed at closing | The operating system closes a dead program's files, so a crash leaves no hold to clear by hand, where a marker a crash left would refuse the world to its own next run. It is taken before anything is read, because reading recovers the folder and recovering deletes what a save left aside |
| 2026-09-14 | M1.5f: a body off its feet is held to what a run and the height lost since the founder last stood give, v² = run² + 2 g drop, and the server keeps that height for each founder | A slide or a fall has nothing driving it but gravity, so the speed it can have is a run's and the fall's. A ceiling measured from where the founder stood cannot grow report by report, as one measured from the last report would, and a body hanging in the air gains nothing from it. The reports' own velocities were not used, since the client states them |
| 2026-09-14 | M1.5g: a move whose wish or facing is not a number, and a put-down whose point is not one, are refused where they are read, as a malformed message is, rather than caught by the checks after them | A NaN passes every comparison, so a put-down at height NaN cleared the reach and region checks and re-entered the world as a thing that never lands, sent to everyone in reach every tick and saved so; the reader is the one door every number from a client comes through, and a refusal there closes the connection with the reason, which is what a client that is not the game's deserves |
| 2026-09-14 | M1.4f: a Unity object the client made is freed through one helper, which destroys at the end of the frame in play and at once in the editor's edit mode; a tile's Terrain frees its data and a water tile its mesh, and the hole cut in the coarse ground under a tile is filled by the cut's own rounding once the tile has gone | A TerrainData or a Mesh made with `new` is neither collected nor destroyed with the GameObject that drew it, so every tile a walk let go of left a 513-post heightmap and a mesh behind (the bug hunt of 2026-09-13); one helper keeps one owner of the edit-mode switch, which the tests need since Destroy is refused there; the fill reuses the cut's rounding so the two cover the same cells |
| 2026-09-14 | M1.D: a developer's setting is one message, a name and a number, and one table of names, ranges and starts in the protocol package that the panel's sliders and the server's applying both read; a deed (standing at the wake) is a row of the same table | Two lists, one for the sliders and one for the server, would be the named bug shape; a message a setting, or a field a setting, would make every new setting a layout change, where a row and the code that applies it is what growing the panel should cost (ruling 30's "other things in the future") |
| 2026-09-14 | M1.D: the server's clock rides on every pong, and a client's clock is set to it only when it slips by more than three minutes of the world's time | The client's clock has run from its Welcome since M1.A and nothing corrected it; a developer moving the server's clock would have moved no sky. Setting it on every pong would jerk the sun by the round trip's worth every second; a slip larger than any round trip and smaller than any deliberate move is the line between drift and a move |
| 2026-09-14 | M1.D: standing a founder at the wake is done as a correction is done, the server's body sent back for the client to take, under the reason that names the panel | Movement is client-authoritative and server-validated (§7, §9): the one way a server moves a founder is the correction, and a second way would be a second owner of where a body is |
| 2026-09-14 | M1.D: the noclip switch off gives the flight the walk's collision, swept by the walk's own rule; what stops it is ground when its normal points more up than cos 60°, and then the flight skims, keeping its way across and losing its fall, else a face or a trunk slid along; feet that end under a gentle rise are set on the ground | The sweep stops a flight at faces and lands it on ground it falls onto, as it does the walk; slid along the ground's normal as along a wall, a flight pointed steeply into a rise was sent back down it (the test's first run), where the walk keeps its way and is set on the ground. A rise gentler than a face the sweep leaves to the walk's step-up, which a flight does not have; setting the feet on the ground within twice a step's reach keeps the flight out of the hill without a second collision rule, and 60° is the steepest ground that reach covers at the flight's running pace |
| 2026-09-14 | M1.D's second landing: what the panel is asked for but cannot yet do (a hand on the weather, an animal set down) is shown greyed with a note of what it waits for, rather than left out or wired to nothing | The owner asked for these stubbed; a control that does nothing silently is the thing STANDARDS forbids in a verifier and no better in a panel, and one that is absent tells him nothing; greyed and named, it says what is not built and where it comes from |
| 2026-09-14 | M1.D's second landing: the clock's rate is the clock's own (`WorldClock.Scale`), set by a developer's setting and never saved, and it rides on every pong beside the clock | A rate kept beside the clock would be a second owner of how fast time goes; kept in the clock, `Advance` is still the one place time moves. Without the rate on the pong a client running at the game's rate would slip past three minutes every second at any other rate and its sun would jerk at each pong |
| 2026-09-14 | M1.7c: a group's flight is its offset from where presence puts it, held by the stand-up while the group stands and forgotten when it is taken away; presence stays a pure function | Presence must stay derivable (the tablet, a test, the save carry no animal); an animal that fled is state, and the least state is how far its group is from where presence would have it, which the once-a-second refresh adds back and the walk home takes away |
| 2026-09-14 | M1.7c: a running group is moved every step and a standing one once a second; a run turns 45°, 90° and then 135° either way at a wet or edge step, and stops where none is dry; a group startled while standing or walking back runs again from where it is | A client draws an entity three ticks behind between its states, so a run told once a second would jump seven metres a second; a run told every step is smooth and costs a mob eight states a tick, inside the byte budget. The turns keep a mob along a shore rather than through it or stuck at it; the second startle is the contract's "a founder who comes near again sends it off again" |
| 2026-09-14 | M1.7c: an animal's third pose, fleeing, is protocol 12, though no layout changed | A client of an earlier protocol would take the byte and draw nothing for it; the version is the one place a meaning's change is declared, and the Hello refuses the mismatch as it does a layout's |
| 2026-09-14 | M1.Ba: a socket for this machine binds the loopback address alone (`UdpOptions.LocalOnly`; the harness's hosts and a client joining a loopback address), and only a socket meant for friends binds every address | Windows' firewall asks about a program the moment it binds every address, once per program path, with a box on the owner's main screen: by 2026-09-14 he had answered it for eight test builds and the test suite twice, each a fresh build folder's players joining 127.0.0.1. The fix is in the game, not in his firewall |
| 2026-09-15 | FP.1: a founder's body holds water on the world's clock, told to its own client as one number (protocol 13); a drink is a verb the server judges by the water layer's class at the point; the work capacity is the body's, and the validator's ceiling is the capacity the client was told | The Founder's Path's first beat (ruling 31). The number rather than the word on the wire, so no reader holds thresholds of its own; the told capacity for the ceiling, so a client is never corrected for a number it has not received; the sea's refusal carries its reason, as the path's tablet will |
| 2026-09-16 | FP.2: a founder's body has v1's heat balance, run every step on the world's clock in the world's own weather at the body; death is the step's judgement at the lethal core or the lethal loss, under the owner's Standard mode, with the one sentence made in one place for the screen and the logs | The Founder's Path's second beat (ruling 31): the two kill conditions bound to their explanations. The balance is one reading of the surroundings for the body and any forecast to come (v1's B1 seam); the death judged in the step whatever moved the body there, so the panel's row and the night kill by the same door; the core on the wire as a number, the word from the engine's table |
| 2026-09-16 | FP.2: the beta arc's bridge is a server setting read in the one step that judges death, holding the core a hair above the lethal, not a term of the body's balance and not a client's mercy | CANON ruling 33: the night is a night and teaches nothing; what answers it is not built yet, so a bridge, and one that comes down by deleting a flag. In the body it would have bent the physiology every later beat builds on; on the client it would have let a client decide who dies |
| 2026-09-16 | FP.2: the night's check integrates by the server's ticks the words carry, not by real time and a rate, nor by the client's clock; a value that is not a number is left out of a record | The night's second run died in the recorder: a stick never picked up left a NaN for the record and the JSON writer, rightly, has no form for it. The first check counted real seconds at the clock's rate, which a frame's hitch mid-night stretched (the in-process server catches up its steps); the client's clock lagged the change of rate by a word and under-timed the first hour by half. The server steps the body once a tick and tells it every twentieth, so the ticks are the night's own time |
| 2026-09-16 | M1.7b: an animal is a list of rigid pieces on a skeleton of fixed bone lengths, described engine-free in ClientCore and grown into meshes by the Unity layer; a pose moves joints and never resizes a piece | The looks are made here (ruling 29) and the bar is recognisable, not fancy (William, 2026-09-15). Shapes in ClientCore can be held to the species' published sizes by a headless test in milliseconds, where a mesh in Unity could only be looked at; fixed bones make the two sides mirror and the hop's swing honest, and a piece whose size never changes needs one mesh for all three poses. A skinned mesh was never open: Unity does not instance one, and no model of either animal exists to buy (the research of 2026-09-13) |
| 2026-09-16 | M1.7b: a piece's mesh carries its real size and is drawn at scale one, in a copy of the stand's material whose band split is set past any distance a world holds | The stand's shader is compiled with `assumeuniformscaling` and reads no inverse matrix (2026-09-11), so a non-uniform instance scale would bend every normal; baking the size into the mesh costs one mesh a piece and nothing a frame. The split exists to put a tree in the near band or the far one; the animals have one band and no far form, so a split past every distance keeps them all drawn without the client having to hand the material an eye each frame |
| 2026-09-16 | M1.7b: an animal is drawn as an object with a child renderer for each piece, not instanced as the trees are, and its pieces are placed again every frame | The trees are tens of thousands of instances of a handful of meshes and could not be objects; the animals are at most a few dozen bodies and every piece of a bounding one moves on its own, which instancing by mesh would have to gather and rebuild every frame anyway. What it costs against §8's 0.6 ms for the fauna is unmeasured and is a debt, not a claim |
| 2026-09-16 | M1.7b: the hop's cadence is a stride over the kind's flee speed rather than a period typed down, and each animal starts its cycle at a place drawn from its id | The research gives both the pace and the cycle (7 m/s, about 0.45 s), which makes the stride the fact and the cycle its consequence: a kangaroo slowed by the panel then takes its hops slower and not shorter. A mob whose members all started at nought would rise and fall as one machine, which is the tell of a function and not an animal |
| 2026-09-16 | M1.7b: an animal set down by a developer's hand takes an id of the reserved range that names no kind, is kept by the refresh rather than swept away, and is never presence's | `AnimalStandUp.IdOf` packs a kind counting from one, so the whole of the range with no kind in it was already unreachable by presence and is free: no new field, and no way for a hand-set animal to take a mob member's id. Making it presence's instead would have meant inventing a group the country does not have; keeping it a thing to look at is honest about what it is, and DEBTS carries what it therefore cannot do |
| 2026-09-16 | M1.8c: the climate stands on the station's own all-years table, kept in the repository as the owner copied it, and the warming taken off a table is the trend's value at the middle of the table's years, stated as a rule | The Bureau serves browsers and refuses scripts, so the tables came by hand; a fourteen-year reproduction had August's day 0.7 °C warm and its rain days two short of the long record, and taking a whole century's trend off fourteen recent years made the pre-human night a degree too cold. A rule for the warming's share means the next table read gets the same treatment without a new argument |
| 2026-09-16 | M1.8c: the weather's settings are fitted by a tool that calls the engine's own formulas with candidate settings (`RainSettings`, `CloudSettings`, a depth), and the tool is kept | M1.8a's fit was a script that did not survive, so its numbers could not be re-derived when the record changed; a copy of the rain's formula in a fitting script would have been a second owner that drifted. The constants in the engine are what the tool printed, and the tests hold them to the record |
| 2026-09-16 | M1.8c: the wind is the station's two readings a day by month, read as a night floor and an afternoon peak a half sine fixes, at a founder's height by the log profile; the humidity is a dew point against the air; the cloud has its own fitted year; the cold snap's depth is set by the deciles' spread | v1's wind shape was a guess a third low; one humidity level off the index could not be damp at dawn and dry at three as the record is, where a dew point and the day's temperature give both for nothing; the cloud's season is not the fronts' frequency alone; and a highland station's depth spread this coast's nights too wide |
| 2026-09-16 | M1.Bb: the game lives in two folders replaced in place (`Build/Player` the owner's, `Build/Harness` the runs'), landed only by `Tools/build/install.py` with a `version.json` the player logs and the run log carries; every socket is closed unless opened, and the suite opens none | The owner: \"i dont want each new version of the game to be in a new folder that asks for perms here in the claude harness and in my firewall\". Forty-one build folders stood under `Build/` by then, each a prompt; M1.Ba's contrast tests bound sockets on every address and raised three firewall boxes in ten minutes from three new test-host paths. A build's identity belongs in the build, not in a folder's name |
| 2026-09-16 | The corpus's walker drinks and dies as a player does rather than the harness holding the body: told thirsty it looks for standing fresh water within 25 m of where it stands, drinks until the word is gone, and a death restarts the loop from the wake's approach; the rules are engine-free (`Drinking`) and the search reads the tiles the client holds, never the world's layers | The soak measures the netcode under what the game does; a body held for the harness would hide a death's respawn, which is a jump the mirror rows must see or excuse in writing |
| 2026-09-16 | `lay_loop.py` budgets the walk to the last of N2's named legs rather than the whole lap, and lays passes by animal groups' places taken from a soak's own fauna records (`--pass`, `--groups`) rather than from presence's arithmetic | A pass within a mob's flight distance lies 620 m from the wake, which no 500 s lap could hold, and the ten-minute walk needs only the named legs in time; the fauna records are what the host saw, and restating presence's seeded draw in Python would be a second owner of it |
| 2026-09-16 | The water's price of exertion is the breath's water above rest, from the heat balance's own latent respiratory term, and the sweat; not a multiple of the whole resting loss | The owner asked whether a founder walking all day should die of thirst in fourteen hours. v1's factor (walking 3.25 times, running 6.25) scaled urine and the skin's diffusion with the metabolism, which physiology does not: what rises with work is the breath and the sweat, and both were already in the balance. Walking a cool day now costs about three litres and two days; a warm day's sweat or a run makes it hours, which is the design's "thirst in days" |
| 2026-09-16 | M1.Bc: the soak's mirror row leaves out the samples within two seconds of a founder's death, when the server itself moves them to the wake | With bodies (FP.1, FP.2) a soak's founders die of thirst and wake at the wake; the jump is the body's rule, told to every client as a correction and a state, and no interpolation follows it. Excusing it keeps the row a measure of the netcode; counting it apart keeps the deaths visible |
| 2026-09-16 | FP.3: the state a blow gives a stone rides on `ItemComponent` beside its rest and fall, zero meaning "none of its own" and the definition's mass standing in; it goes into the hands with the thing, into the region and player files and onto the wire, and enters the digest only when the thing has state of its own | A flake's mass and edge are the blow's, not the kind's, and a core grows lighter; one owner (`KnappingItems`) reads and writes the rule, so the server, the save and the wire agree about what a struck stone is; zero as "none" keeps every stick and cobble written before FP.3 reading as it was and every older save's digest its name, where a version bump would have renamed worlds nothing had changed |
| 2026-09-16 | FP.3: a blow's answer carries the physics' own words on the `IntentResult`, made once in `Knapping` and shown by the client as they came | The path binds the answers to their mechanism ("the wrong stone teaches as much as the right one" without a lesson, ruling 33); a client mapping outcomes to sentences of its own would be a second owner of what the stone said, free to drift from the physics, and the note is a few bytes on a reliable channel |
| 2026-09-18 | M1.7b: an animal's skin is one closed surface a kind — shells lofted along the bones, mitred at the bends, rounded at the ends, rooted inside one another — grown once in the resting pose in ClientCore and moved by the bones on one skinned renderer an animal; the rigid pieces of 2026-09-16 are gone | William saw the pieces in the first frames and asked for the animals "more smooth and connected" (2026-09-16); a skin that follows its bones is what that costs. It is grown engine-free so a headless test can prove what a frame could only show: every shell closed, every face outward and every limb rooted inside its parent in every pose. One skinned renderer an animal is one draw a pass where the pieces were nineteen, and the skinning is the graphics card's; a skinned mesh is never instanced, which the trees need and a few dozen animals do not. A folded wing's frame takes the bird's up for its chord and a spread wing's the fore-and-aft line, because a fixed line nearly along a bone gives it no steady square and the first skin's wrist came out turned over. What an animal costs a frame stays the debt it was |
| 2026-09-18 | WG.1: a creek or stream carries water over its bed by the flow's law (ankle-deep at the creek's catchment, deeper as the catchment to the 0.4, capped waist-deep), written into the surface layer at creation so every reader of the surface has it | William gave world generation to Claude with the creeks first (ruling 26 as amended); the water layer had given a surface to the sea and the lakes alone, so the path's first drink from the creek by the wake could not be had and the corpus's founders died of thirst beside a dry bed. One law in the engine, restated by `drainage_check`; the surface is the one place every consumer already reads |
| 2026-09-20 | The region is held to **recognisable, not faithful**: the country's shape, its plants, its weather and its physics come from published record and stay true to what a player can learn from, but no further slice is spent making Bherwerre's map match the real coast feature by feature (the bay's own seafloor grids, a finer elevation bake, the salt wind tuned until every species lands on its recorded hectare) | William was asked on 2026-09-13 whether the beta stays a faithful Bherwerre or a recognisable one and again on 2026-09-20, when he answered "im not sure"; the head developer decided it under his standing word of 2026-09-18 ("answer ... to the best of your ability, i will correct you") and it stands until he corrects it. His founding ruling makes real data the skeleton with invented detail below it (2026-08-25); the beta exists to show another developer what the game is (2026-09-01), which no exact coast is needed for; and the atlas path generates any region from worldwide data, so precision tuned to this one coast is work that gets superseded. What stays exact is whatever real knowledge transfers through (GAME_DESIGN §3): reading the land for water, which plant grows where, the climate's own numbers |
| 2026-09-20 | The drink scenario pins mid-morning through the panel's clock setting before its first capture, as the looks scenario does | Its frames are for the owner's eyes, and the drying leaves the world's clock wherever sixty times the rate took it: WG.1's creek was recorded at five past midnight and had to be run again to be seen at all |
| 2026-09-20 | M1.4g: a cell with dry corners is drawn at its highest wet corner's surface, held down to the lowest dry corner's ground, instead of not being drawn at all | Requiring four wet corners kept a flat sheet off a rising beach and also hid every channel narrower than the 4 m between posts: WG.1's creeks were waded in, drunk from and never drawn. The clamp keeps what the four-corner rule was for, and on a shore it puts the sheet at the sand, where the opaque ground hides whatever reaches past the waterline |
| 2026-09-20 | M1.4g, the same day: that flat height a cell is replaced by a corner apiece — a wet corner at its water's surface, a dry corner on its ground | A creek runs downhill and a flat plate cannot: William's frame of the first answer showed a staircase of level panes stepping down the slope, gaps between them, some standing proud of the ground at their lower edge. Corners of their own make the skin continuous along a channel, since cells share their posts, and make the clamp unnecessary: a dry corner on the ground cannot float over it |
| 2026-09-20 | M1.4g: water is drawn by `EarthGame/Water`, which takes its column from the scene's depth buffer and its light from the sky's spherical harmonics and the main light, sampling no reflection probe | One number, the water between the eye and the bed, serves a lake's plane, a four-metre creek and the sea alike, lengthens as the eye looks along the water as a real sightline does, and gives the soft waterline for nothing. The light matters as much: the environment cubemap is built from the sky once and refreshed by nothing here, so the old opaque water at smoothness 0.92 mirrored a midday sky at 19:26 and was the brightest thing in a dark frame (William, playing, 2026-09-20) |
| 2026-09-20 | M1.E: developer mode is a per-player switch the server answers (`DeveloperMode`, protocol 16), not a property of the server; a SOLO game's server may always grant it, a hosted one only when started for development | William's ruling 39 ("toggleable in game, not a restart with the devmode flag") and his key, F2. The server's mark had made every player of a development server a developer; a switch each player asks for keeps the server deciding (a client cannot grant itself flight on someone else's world) while a solo player is master of their own. A server that may not grant it refuses and keeps the player, because F2 is a key every game answers, where a developer's setting sent to such a server still closes, no client of this game sending one |
| 2026-09-20 | M1.E: a game left alone sleeps by a rule in ClientCore (`IdleWatch`), the bootstrap dropping the slept time rather than stepping the world through it, and only a SOLO game with hands at it may sleep | William's ruling 38: an open idle game is him looking at something else, not playing. Stepping the world through the slept time would thirst and freeze a founder nobody was with; a joined game's world is the server's and cannot stop for one player; a recorded run waits minutes between its presses and the corpus's soak would send itself to sleep. The rule is engine-free so its minute, its edges and its exemption for recorded runs are asserted without a window |
| 2026-09-20 | M1.E: the free look (`FreeLook`, ClientCore) sits between the mouse and the body's facing — held, the turn goes to the view; let go, the view glides home in a quarter second — bounded at 135° | William's ruling 36, after Rust. The body's facing is what the mover walks by and what the server is sent, so the seam is the one place the mouse's turn enters `YawDeg`; a bound keeps a head from turning further than a head does; a glide keeps the return from snapping. The gamepad has no free look until its layout is judged by his hands (DEBTS) |
| 2026-09-21 | Every tool under `Tools/world/` that runs the player runs it on a copy of its world, taken through one owner (`wade.copied`) under the run's own folder; the world named is read and never written | A run saves its world on the way out (M1.3c) with the founder where the scenario left him: the drink scenario of 2026-09-20 left him at the water's edge sixty metres from the wake, and the controls scenario, whose things are dropped at the wake, found nothing within reach. `knap.py` alone had copied (2026-09-18); WORKING.md's rule was kept by one tool of eight, and a rule kept by hand in eight places is the named bug shape. One owner keeps it kept |
| 2026-09-21 | M1.4h: ripples are slopes on the water's normal in the shader — four waves at the dispersion relation's speeds, steepened by the wind, faded with distance — never a displaced mesh, and their clock is the game's awake seconds | William's "ripples now". A displaced mesh would move the wading and the depth column M1.4g had just made agree with the world; a normal moves only what the eye sees. The dispersion relation is one law where four tuned speeds would be four facts; the wind ties the look to the weather the game already computes (M1.8a); and a ripple on real time would have moved while the world stood still (M1.E) |
| 2026-09-21 | M1.5h: the founder's second-by-second speed is a table of measured walking speeds on slopes, reached by acceleration and left by braking, on ground no steeper than 35°; Tobler stays as the journey's hour-average | William's ruling 34: a founder walked down a dune slower than a stroll, because Tobler's hiking function, fitted to journey times with the pauses in them, was used as a stepping pace and floored. The stepping pace and the journey's pace are two facts with two owners now; the acceleration is the third step's (Gait & Posture 2021) and the brake v1's; the repose is where loose ground stops standing. His hands judge the feel (ruling 12) |
| 2026-09-22 | M1.5j: a footstep is an impact and the ground's own answer to it — a heel and a forefoot, each answered by a body, modes and grains from the ground's numbers — and heath is a footing of its own; the tests assert the numbers an ear would give (a second strike, a ring's autocorrelation, the count of snaps, the splash's length, a brightness share) and the tool writes the same sounds as WAV files for the owner's ears, blind | William heard every M1.5c ground as "a static sounding noise" (ruling 37): one burst of filtered noise differs by brightness and length only. What an ear tells apart is the structure of the sound, and a test can only hold that structure if it measures it; whether the result sounds like a foot on that ground stays his, so the proof ends with a blind set rather than a claim. |
| 2026-09-22 | M1.6e: the ground's grain is made in a terrain shader of the project's own (the map's colour multiplied by a two-scale noise of the world position, the normal tilted by its slope, faded by 110 m), never a detail texture from a file; and the understorey's variety comes from the cover's own numbers — a companion shape at a share, herbs between, patches by a hash of the cell grid, a shade per tuft — with nothing standing where the water-depth tile says water stands | William judged the understorey "a smooth green floor with some small bits scattered around weirdly" (ruling 37) and the ground had no grain since M1.4d. A grain from a file would be the project's first imported picture and would tile visibly; a grain from the position tiles never and costs a few hashes a pixel. Variety from the cover's numbers keeps one owner for what grows where, and the water rule reads a tile the client already holds rather than adding a layer. |
| 2026-09-22 | A flat water cell is one whose corners lie within 2.5 cm and it sits at the mean of them; a dry post with wet posts on every side takes the body's surface | The rows of dark dashes across Windermere since M1.4g, laid at the understorey's door on 2026-09-20, survived the understorey, the sticks, the trees, the far ring, a finer terrain, the coarse backdrop and stilled ripples being hidden in turn, and fell to a test: a flat lake's posts come off the wire rounded to the centimetre, and a cell spreading two was drawn a step above its row. Seven hide runs named what it was not; the test named what it was. |
| 2026-09-22 | WG.0b: the atlas's raw datasets are read by readers of the project's own (a DEFLATE GeoTIFF reader with both predictors, a shapefile reader and a scanline rasteriser, an ASCII grid reader, in numpy and the standard library), never by GDAL, into layers at a twelfth of a degree, the climate at a sixth, each a raw array beside a JSON header | The six datasets are classic TIFFs with DEFLATE, shapefiles and an ASCII grid, all readable in a few hundred lines that a self-test proves on made files; GDAL is a binary the tools' Python does not carry and the project need not, and the one dataset that needs it (HydroLAKES) has its own row. A twelfth of a degree divides the sources' 1/120 evenly and is finer than any choice of place needs; twenty-four climate layers at that pitch would be 450 MB, so they take a sixth. |
| 2026-09-22 | WG.0's choosing of a place is a tool that reports every field of the atlas at a coordinate with its provenance and its gaps, and a verifier that reads the layers itself; a probe's expectation revised after a failed check keeps what was first pinned beside it | The contract forbade filling expectations in after a failed check; keeping the first pin and the reason in the same file makes a revision a record rather than a cover, and the three that were the references' own errors say so. |
