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
  for settings, prefab and registry invariants.

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
  layer (the sea at the datum, a lake at its level, else the ground); the landforms (the topology bitmask: sea,
  beach, dune, cliff, platform, water, crest), which the plants read; the plant community (`PlantSpecies`,
  `PlantCommunity`: suitability product, dominance², a tall plant on a cell as often as the ground suits the best of
  them, overstory then understory, drawn by the seed; since M1.2b no soil a root can use on a beach, and on a dune
  the soil model's depth less the salt wind's share), which adds the forest and the heath to the topology; the
  stone by topology and the province lattice (`GeologyScale`); animal capacity (`AnimalCapacity`);
  the distances the wake scorer asks about and its score. The sea's floor is a stated rule until the bathymetry
  arrives: one in twenty from the shore, to 30 m. **No landform evolution on real data** (decision 2026-09-07:
  the valleys in a 5–30 m DEM are already the record of what water did; v1's erosion existed to replace a
  9.8 km/px gradient). `LandscapeEvolution` is ported but dormant.
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
- **Climate:** `Climate`/`Synoptic`/`Weather` ported; station 068034 (Point Perpendicular Lighthouse, 1899–2004)
  with 068072 (Nowra) as the second anchor; pre-human baseline by the owner's rule; the known 03:00-minimum
  defect fixed as a contracted step.

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
  plain-struct components (`ItemComponent` first); entities kept in id order so every walk is the same walk;
  lifecycle Spawn → Init (the end of the tick after the spawn) → Kill (removed at the end of the tick, kept
  for the server to tell its viewers). Every field that can change is stamped with the tick it changed at, and
  a viewer reads what it has not seen off the stamps against the tick of its own last send: dirtiness is per
  viewer, never a flag cleared for everyone, so a session behind on its byte budget is sent what it missed.
- Definitions (`Definition`, `DefinitionCatalogue`): a stable string key (`plant/blackbutt`, `stone/silcrete`,
  `animal/eastern-grey-kangaroo`, `item/cobble`, `player`; lower case, one slash, hyphens between words) hashed
  FNV-1a 32 at load, the catalogue refusing a collision or a malformed key; built from the tables that exist plus
  the items and the player; each states its kind, whether it is spawnable in M1 (the items now; trees and animals
  with M1.6 and M1.7) and an item's mass and radius, which live nowhere else. The id travels in the spawn
  message; `PrefabRegistry` (a ScriptableObject under `Resources/EarthGame`) maps key → prefab; the edit-mode
  `PrefabRegistryTests` asserts every spawnable definition is bound exactly once and every binding names one.
- The fast tick's systems run in the order `WorldState.Systems` lists (`ItemFall` first: a dropped thing falls at
  9.81 m/s² until it meets the ground the server holds, the sea's floor under the sea, and rests); the slow
  layers (`ISlowLayer.AdvanceTo`) are advanced by `SlowScheduler` per 512 m cell (`RegionCells`, cell (0, 0) at
  the south-west corner): a cell within the interest radius of any player every tick, a cell farther away every
  60 ticks on its own phase, catching up in one call, the layer analytic over the gap.
- Fauna: the presence function is the source of expectation; the server materialises animals as entities inside
  each player's interest radius, simulates them, and dematerialises them outside.
- **What stands and lies on the ground is a layer until something moves it** (M1.6a, 2026-09-11; this line used to
  make the trees inside the radius entities). The world places every tree, stick and cobble when it is created and
  saves them as two layers, `stand` and `loose` (§10), which travel as code tiles like the cover and are drawn by the
  client (§8). A generated thing becomes an entity only when something moves it — a stick picked up, a tree felled,
  M1.5 and after — and the region file's layer-diff count is where a world will record that it is gone. The numbers
  that retired the old line: the interest radius is 1,500 m, about 7 km² round each player, and the nine tiles round
  the four vantages of M1.6a's frames hold 103,850 to 176,906 trees; every entity inside the radius goes out in the
  join snapshot with no byte budget, `EntityStore.Within` walks every entity for every session every tick, and the
  client makes a GameObject for each. Grass and litter are layer-derived instances too, never entities.
- Dropped items rest where the server says; the client animates the fall.
- Interaction: v1's `PlayerAction` model; verbs are intents sent to the server; the server commits.

## 6. Save and world folder

`Saves/<world>/world.json` (seed, region, extent, wake point, created, protocol version), the baked layer rasters,
`regions/r.X.Y.egr` (one file per 512 m cell: entities and mutable layer diffs; atomic write), `players/<id>.egp`
(server-side), `digest.txt` (the world digest the tests diff: `WorldDigest`, every number a whole count of micrometres or nanohours, rounded to even, the lines sorted and hashed with FNV-1a 64; integers rather than a printed double because the player's Mono and the server's .NET print the twelfth significant figure of some doubles differently, as the first full corpus found on 2026-09-08). The server is the only writer.
Additive fields with initialisers; every persisted field enters the digest in the same commit. Solo worlds are
byte-identical to server worlds.

Landed 2026-09-08 (M1.A): `world.json` as format `eg2.world` version 1 (region, seed, extent, created and saved
dates, protocol version, tick, the clock's two numbers) and a player's resting place as `players/<name>.json`
(position, yaw, pitch, grounded, tick). The binary `.egp`/`.egr` files arrive with the entity store (M1.3); the
player file moves to `.egp` then, as a versioned change of format, not a quiet rename. `WorldSave` in
`EarthGame.Server` is the writer; every file is written to a `.part` and moved into place.

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
  word, the N3 cut.
- **Handshake (protocol v1):** the first message must be `Hello` (protocol version, player name, password); the
  server answers `Welcome` (session id, seed, region, time, tick, tick rate) or `Refused` (reason) and closes with
  the same reason. A malformed packet is a refusal, never a crash. Over UDP a close can overtake the `Refused`
  message, so a client in the handshake treats a peer-stated close as the refusal.
- **Movement:** the client runs the engine's `Step()` locally and sends `{tick, input, resulting state}`; the
  server validates (max speed × dt × tolerance; heightfield ground clamp; penetration probe), logs violations and
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
  asks again. **Interactive keeps the meaning CANON ruling 11 gave it**: the ground under the founder, the snapshot, the
  tile built. The water travels beside the ground and gates nothing, so N1 measures the same thing as before. The client's ground (`TileHeightfield`) reads
  the tiles bilinearly exactly as the server reads the raster, and is NaN where no tile is held.
- **Snapshot and interest:** the Welcome is followed by a `PlayerState` for every body the joiner can see and a
  `SnapshotEnd`; after each tick the server sends a body only to sessions within `ServerConfig.InterestRadiusM`
  (1500 m; a session without a body yet sees everything); a session's end is a `PlayerLeft`. The client keeps a
  `RemoteMirror` per remote player (states by server tick) and draws it a stated delay (three ticks) behind the
  estimated server tick, interpolating between the two states around it and holding at the newest beyond them.
  *Interactive* (N1) is: the nine tiles answered, the snapshot applied, and the tile under the founder built.
- **Rejoin:** a session that ends keeps its body by player name for the life of the server (and in the world
  folder on save); the same name wakes there, and a Hello for a name still connected supersedes the old session
  (the transport had not yet noticed the cut). **Digests** (`WorldDigest`, ARCHITECTURE §6): FNV-1a 64 over
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
  (`-eg-latency`, `-eg-jitter`, `-eg-loss`, `-eg-sendcap`) and runs a scenario with `-eg-scenario` and
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

## 8. Rendering, input, UI (Unity 6000.3 LTS, pinned)

- URP 17, Forward+, linear colour, DX12 with DX11 fallback. Settings in load-bearing order: BatchRendererGroup
  variants = Keep All → Rendering Path = Forward+ → GPU Resident Drawer = Instanced Drawing (SRP Batcher on) →
  Render Graph on → GPU Occlusion on (validate: open bug with the Resident Drawer) → Depth Priming = Forced → STP on
  at render scale 0.7 (1440p output) as the default quality; native 1440p as an option. Asserted by an edit-mode
  test.
- **Water** (M1.4c, 2026-09-10): the standing water of each streamed tile is its own mesh, built from the depth
  the server sent over that tile's ground (§7). A cell is water only when all four of its posts carry depth and
  sits at the lowest of their surfaces, so the drawn edge stops at the last cell wholly under water and never
  stands above its bank; cells merge along a row while the surface holds at one height, because standing water is
  flat. `WaterSurface` in ClientCore finds the rectangles and is tested headlessly; `WaterTileBuilder` in the
  Unity layer turns them into a mesh and nothing else. The mesh carries no collider: what a body may wade in is
  the server's and the mover's, not the view's. The sea keeps the plane it has had since 2026-09-08 and only
  water above the datum is drawn from a tile, so the two never contend for one surface.
- **The ground's colour** (M1.4d, 2026-09-10): the world names what covers every one of its cells — one byte, the
  cover in the low six bits and the quarter of the land's own wetness in the top two (§10) — and that byte is
  streamed as a layer like any other. `GroundPalette` in ClientCore is the single owner of what a ground looks
  like: each cover names its dry colour and its wet colour and the quarter sits between them, so a tuft of grass
  added later asks the same question and cannot disagree with the ground it stands on. `GroundColourMap` turns a
  tile's cover into a picture, blending between the posts so a beach does not end on a line, and the Unity layer
  makes that the tile's own terrain layer, one texture stretched once over the kilometre. Two texels a post is
  what a blend needs; a metre a texel costs four times the work and shows the same 4 m raster. The ingredients do
  not travel: measured on the nine tiles around the wake, wetness is 476 KB and soil depth 501 KB against the
  ground's own 418 KB, and the byte they come to is 96 KB on the wire.
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
  `StandForms` in ClientCore is the one table of what each looks like). The bands' materials copy a material asset
  with instancing on (`ProjectSetup`): made from the shader alone, the build stripped its instanced variants and the
  first player drew no trees, as happened to the terrain on 2026-09-08. Measured without a window (the recorder's `-eg-hold`,
  §12) on a freshly created world in the morning, when the low sun lets every near tree cast: the trees first cost
  15.2 ms of the median frame beside Windermere and 10.5 ms at the wake, against the budget's 2.0 ms; with the
  choosing by block, the thinned and cheaper far band and the per-instance savings they cost 4.1 ms and 2.9 ms
  (beside Windermere each band alone 2.1 ms near and 2.4 ms far, and 1.2 ms of the whole the main thread's choosing
  and handing over), still over: a debt (DEBTS.md). The sticks and cobbles cost 0.4 ms and 0.3 ms, inside 0.7 ms. On the wire the two layers are small beside the ground: the nine tiles round the gate world's wake
  are 110,056 bytes of stand and 119,421 of loose as cached, deflated, against the ground's 282,686 (the M1.6
  gate's shaped join, 2026-09-11).
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
  fact of a world and they are read from the region's bake. Grain underfoot waits for a terrain material that can
  multiply a tiled detail texture by the colour map: the stock layer blends, and a blend washes the colour out
  (DEBTS.md, 2026-09-10). No "build all now" path.
- Flora: grass and shrubs by Terrain detail instancing first, BatchRendererGroup only if measured over 3 ms. Trees
  are drawn instanced from the stand layer (above), not as prefabs: the plan's parametric skeleton, with v1's vertex
  colour in place of authored leaf and bark materials and a near and a far band in place of a LODGroup and
  billboards (M1.6a, 2026-09-11). Fauna: sourced meshes.
- Rules: no `MaterialPropertyBlock` (source-text scan + play-mode walk); named layer masks in one file; no
  `Awaitable` in engine-free assemblies; `[SerializeField]` on fields only; Unity 6 physics names.
- Input System 1.20 with one actions asset; the verb rule; a scripted-input seam for scenarios. UI Toolkit for
  HUD (M1: crosshair, verb line, clock), menus, the Tab window, settings, and the tablet page; readability floor
  3:1. Sound in M1: wind by exposure, surf by distance, birds from presence, footsteps by surface, rain.

## 9. Player and collision

The mover is a pure function in the engine, `Step(state, input, dt, IWorldCollision)`: walk/climb/slide slope
thresholds, step-up, grounded/airborne friction, capsule swap for stances, a wading state; speed from the ported
`Locomotion` model. The client implements `IWorldCollision` over PhysX (capsule casts against the Terrain collider
and prefab colliders); the server implements a heightfield ground clamp and speed cap for validation. **Named
defect class:** the two disagree most at cliff edges, rock platforms and the water's edge (`DEBTS.md`); the walk
scenario carries that segment and N2 carries its budget. The mover-feel decision is the owner's, hands on the
controls (ruling 12).

## 10. Contracted formats (owner ruling 16: versioned; a schema change is a renegotiation in writing)

| Format | Version | Owner of the writer | Readers | Defined |
|---|---|---|---|---|
| Wire protocol | 5 (v1 Hello, Welcome, Refused, Ping, Pong; v2 PlayerMove, PlayerState, Correction, the spawn in Welcome; v3 the extent in Welcome, TileRequest, TileHeader, TileChunk, SnapshotEnd, PlayerLeft; v4 EntitySpawn, EntityState, EntityGone; v5 a layer byte on TileRequest's wants, TileHeader and TileChunk) | `EarthGame.Protocol` | server, client | `Messages.cs`; `ProtocolInfo.Version`; a Hello of another version is refused with both numbers. A tile message names its layer as a byte (§10's tile row), and a layer byte this build does not know is refused rather than guessed. EntitySpawn (reliable): u64 id, u32 definition id, i64 tick, f64 east, up, north, f32 yaw, u8 component mask (1 item), then an item's u8 resting and f32 fall speed. EntityState (unreliable; reliable when it carries the item's rest): u64 id, i64 tick, u8 field mask (1 position, 2 yaw, 4 item), then the fields named. EntityGone (reliable): u64 id, u8 reason (1 died, 2 left the viewer's interest). |
| Layer tile (`TileCodec`) | 2 (v1 carried the ground alone and named no layer) | `TileCodec.cs` (Engine) | `TileService` (server), `TileReceiver` and `DiskTileCache` (client) | A tile carries one layer, named by a wire-visible byte that is never renumbered: 0 the ground, 1 the water's depth over it, 2 the water's class, 3 the ground's cover, 4 what stands on it and 5 what lies loose on it (M1.6a, 2026-09-11). Which layers exist, which of them carry codes, and the folder each caches under (`ground`, `water-depth`, `water-class`, `ground-cover`, `stand`, `loose`) are `TileLayers`' alone: the receiver asked the layer's number instead and dropped every cover tile it was sent, silently (2026-09-10). A layer of metres (0 and 1) is posts² int16 centimetres, each row the running delta from its first post, deflate; a value beyond ±327 m or a NaN is refused at encode. A layer of codes (2 to 5) is posts² raw bytes through deflate, read from the raster's own cells rather than interpolated, and a code beyond a byte is refused at encode. CRC-32 (IEEE) of the deflated bytes in the header and in front of the cached file. The water's surface is not a layer of its own: a client that holds a tile's ground is sent only the depth standing over it, zero where the ground is dry — measured on the Bherwerre world, the nine tiles around the wake are 31 KB as depth against 277 KB as a surface, beside 300 KB for the ground itself. |
| `RegionRaster` (`eg2.raster`: a raw grid + JSON sidecar) | 2 (v1 read as a heights layer in metres) | `Tools/data/raster_io.py` (the bake and the fixture writer) and `RegionRaster.Write`/`WriteCodes` (the world-creation pipeline); the loader's tests pin both writers to one fixture law by checksum | `RegionRaster.cs` (server and client), the verifiers | Sidecar keys: format, version, name, region, layer, dtype (f32, u8, u16, i16, u32), byte_order little, raw (the raw file's name: .r32 for f32, else .u8/.u16/.i16/.u32), scale, unit (m, 1, id, flags), width, height, cell_m, extent_m, centre_lat, centre_lon, min, max (in the unit), source, sha256, and sea_fraction for heights. A value is raw × scale in the unit; version 1 carried f32 metres with min_m/max_m. Row 0 north, column 0 west, cell centres at east = col·cell − extent/2, north = extent/2 − row·cell; width = height = extent/cell + 1. The loader refuses any other version. |
| `eg2.almanac` (one JSON line) | 1 | `Almanac.cs` via the almanac console tool | `solar_check.py` | Keys: format, version, time_basis, day_of_year, latitude_deg, longitude_deg, declination_deg, daylight_hours, sunrise_local_hour, sunset_local_hour, noon_elevation_deg, wake_local_hour, wake_elevation_deg, wake_azimuth_deg. Hours are local mean solar time: no zone, no equation of time. |
| `world.json` (`eg2.world`) | 1 | `WorldSave.cs` (the server) | server (continue), the shell (which world is newest), `save_check.py` | Keys: format, version, region, seed, extent_m, created_utc, saved_utc, protocol_version, tick, clock {total_hours, started_at_hours}; additive since M1.2 (2026-09-09): wake_east, wake_up, wake_north, layers {layer name: sha256 of the raw file}; additive since M1.3 (2026-09-09): next_entity_id. |
| `players/<name>.egp` (`eg2.player`) | 2 (version 1, `players/<name>.json`, is still read and replaced on the next save) | `PlayerFile` in `RegionFile.cs` (the server) | server, `save_check.py` | Little-endian: magic `EG2P`, u16 version, the name as a u16 UTF-8 byte length and the bytes, f64 east, up, north, f32 yaw, f32 pitch, u8 flags (1 grounded, 2 wading, 4 crouching), i64 saved tick, u32 CRC-32 of everything before it. Version 1 (JSON): format, version, name, east, up, north, yaw_deg, pitch_deg, grounded, saved_tick. |
| `regions/r.X.Y.egr` (`eg2.region`) | 1 | `RegionFile` (the server) | server, `save_check.py` | One per 512 m cell (`RegionCells`: cell (0, 0) at the south-west corner, index = floor((coordinate + extent/2) / 512), clamped), holding the entities whose position lies in it. Little-endian: magic `EG2R`, u16 version, i32 cell x, i32 cell z, f64 cell size, u32 entity count, u32 layer-diff count (0 until the diffs land), u32 CRC-32 of the records, then per entity u64 id, the key as a u16 UTF-8 byte length and the bytes, f64 east, up, north, f32 yaw, i64 spawn tick, u8 component mask (1 item), and for an item u8 resting and f32 fall speed. |
| `digest.txt` | the lines of `WorldDigest.World` | `WorldSave.cs` (the server) | tests, `save_check.py` | One hex FNV-1a 64 of these lines in this order: `clock <nanohours>`, `tick <n>`, one line per body sorted by name (`<name> <micrometres east> <up> <north> g|a w|d c|s`), one line per entity in id order (`entity <id> <key> <micrometres east> <up> <north> <microdegrees yaw>[ item r|f <micrometres per second>]`), `next_entity <n>`; every number a whole count of its resolution, rounded half to even. |
| The world folder's layers (`Saves/<world>/layers/`, M1.2) | each a version-2 raster | `WorldCreation.Create` (the server, at creation) | the server (`heights` as its terrain), `drainage_check.py`, `census_check.py`, `region_stats.py --world`; M1.4 streams them | heights f32 m (the bake's ground with the sea floor); surface f32 m (the water's surface where water stands, else the ground's); soil_depth u16 × 0.01 m; wetness u8 × 1/255; suitability u8 × 1/255; water u8 id (0 dry, 1 damp, 2 trickle, 3 creek, 4 stream, 5 lake, 6 swamp, 7 sea); overstory, understory u8 id (`PlantSpecies.All` index + 1, 0 none); topology u32 flags (1 sea, 2 beach, 4 dune, 8 wetland, 16 forest, 32 heath, 64 crest, 128 cliff, 256 shore platform, 512 lake, 1024 creek); stone u8 id (`StoneType.All` index + 1, 0 none); cover u8 (the low six bits a `GroundCover` — 1 sea, 2 fresh water, 3 sand, 4 dune sand, 5 rock, 6 bare earth, 7 heath, 8 bracken, 9 sedge, 10 grass, 11 forest floor, 12 swamp floor — and the top two the quarter of the land's own wetness); stand u8 (M1.6a: 0 where no trunk stands, else the top three bits the tall plant as `StandCodes.Tall` index + 1 — blackbutt, bangalay, old-man banksia, coast banksia, swamp paperbark — and the low five its height in steps of 1.25 m, both in the sidecar's legend); loose u8 (M1.6a: the low four bits the sticks lying on the cell and the high four its cobbles, each to 15); catchment u32 cells; shore_distance, fresh_water_distance, stone_distance, fibre_distance, firewood_distance, shelter_distance u16 m (65535 none); capacity_<species> u16 × 0.01 per km²; wake_score u8 × 1/255. `census.txt` beside the folder is prose, not a format. |
| `water_bodies` (a bake input beside `heights`) | a version-2 raster, u8 id | `Tools/data/bake_water.py` (OpenStreetMap through Overpass; THIRD_PARTY_NOTICES.md) | `WorldLayers` at creation | 0 outside every outline, else the body's code; the sidecar's `bodies` legend lists code, `osm` way, `name` and `kind` (lake, wetland, salt), and `fetched_utc`. |
| `loading.json` (`eg2.loading`, optional `-eg-loading-record <dir>`) | 1 | `LoadingRecorder.cs` | `loading_check.py` | Keys: format, version, outcome (ready or failed), error (empty on success), returned_to_menu (true after the failure scenario presses Back and Bootstrap rebuilds the shell), preparation_s (real seconds to prepared, 0 on failure), updates (main-thread updates during preparation), samples [{t real seconds, update count, stage}], stages [progress strings in received order], frames [{file relative path, width, height, stage, t}]. Samples are taken every 0.25 s and at preparation completion. The new-world proof requires more than 2 s of samples, more than 10 updates and no sample gap over 2 s; this detects a sustained preparation freeze, not a gameplay frame-rate guarantee. `Tools/world/loading.py` writes separate `process.json` {exit: integer} from each child exit status and an `Artefacts/loading/latest.txt` absolute directory pointer only after all three launches finish. Existing `run.jsonl` records playable frames; the verifier also reads actual PNG dimensions and pixel variation. |
| `run.jsonl` (`eg2.run`) | 1 | `RunLog.cs` (Engine), written by the recorder, the scenario runner and the server host | `corpus_check.py`, `join_check.py` | One JSON object per line. Line 1 is the header: `format`, `version`, then what the run says about itself (`role` client or server; scenario, region, seed, name, mode, address, port, the shaping `latency_ms`/`jitter_ms`/`loss_percent`/`send_cap_bytes_per_second`, started_utc, unity, terrain, duration_s, cycles). Every later line starts `t` (real seconds since the run began), `tick` (the server tick last known to the writer, −1 before any), `kind`, then the record's fields. **Client kinds** — M1.A: `frame` (file, width, height, east, up, north, yaw_deg, pitch_deg, grounded, corrections), `error`, `exception` (message, stack), `end`. M1.4e (additive): `build` (what "ground" or "colour", ix, iz, worker_ms, main_ms, before_ms the frame's spend when the budget allowed it to start, frame_ms the spend when it landed, and the main thread's cost split as texture_ms, heights_ms, object_ms, water_ms), and `interactive` gains worst_streaming_ms. M1.6a (additive): `timing` (seconds, frames, width, height, median_ms, p95_ms, worst_ms, hidden the `-eg-hide` list, trees held, stand_cpu_ms the median of what choosing and handing over the stand cost the main thread), written once by the recorder after its frames when run with `-eg-hold <seconds>`: the founder turns a full circle and every frame is rendered into a 1080p target and waited for. M1.B: `welcome` (rejoin, session, seed, region, tick_rate, spawn_east/up/north, world_total_hours), `interactive` (rejoin, since_connect_s, tiles_held, tiles_from_cache, tiles_refused, tiles_built, bytes_received, bytes_sent, rtt_ms, east, up, north), `segment` (name, index, lap, east, north), `correction` (sequence, reason, displacement_m, segment, east, up, north), `sample` once a second (east, up, north, grounded, wading, speed, segment, remotes, rtt_ms, bytes_sent, bytes_received, corrections, interactive, connection, fps), `mirror` once a second per remote (session, latest_tick, digest, latest_east/up/north, east, up, north, at_tick, interpolated, estimated_tick, states_held), `cut` (cycle, east, up, north, mirrors), `rejoin` (cycle), `dropped` (reason, severed), `end` (exit, samples, errors, corrections, interactive_s, rejoin_interactive_s, cuts, rejoins_interactive, laps, skipped_waypoints, moves_sent, east, up, north). **Server kinds** (M1.B): `join` (session, name, remembered, east, up, north), `leave` (session, name, reason), `correction` (session, name, reason, east, up, north), `bodies` once a second per session (session, from, digests[] one per tick from `from`, positions[] east, up, north per tick), `bandwidth` once a second per session (session, name, sent, received, sent_total, received_total, moves_accepted, corrections), `memory` once a second (heap_bytes, working_set_bytes, players, tiles_served_bytes, dropped_seconds, digest), `ticks` once a minute (count, over_interval, max_ms, mean_ms, p95_ms, heap_collected_bytes, working_set_bytes), `pause`/`resume`/`digest` on a console command (digest, paused), `end` (seconds, players, digest, dropped_seconds). |
| The corpus folder | 1 | `Tools/corpus/run.py` | `join_check.py` | `Artefacts/corpus/<stamp>/<scenario>/{server,A,B}/run.jsonl` with `player.log` and `console.log` beside them; scenarios `join-a`, `join-b`, `walk`, `walk-solo`, `rejoin-held`, `rejoin-live`, `soak`; `latest` points at the newest; `summary.json` is the harness's own and no verifier reads it |

A row moves from "not yet" to a version number in the commit that first writes the format; the version field is
mandatory in every file from the first write.

## 11. Verification

- `dotnet test Engine/EarthGame.slnx` — the engine-free suite; exit code is the verdict; never piped.
- `Tools/gate/run_gate.py <gate>` — refuses when a verifier is missing or a stub, then runs the gate's
  steps; `Tools/hooks/pre-push` runs the engine suite, and the Unity compile check when present (its third check,
  the verifier-lane authorship rule, went with CANON ruling 17).
- Unity edit-mode tests: the settings checklist, prefab instantiation, registry completeness, the
  `MaterialPropertyBlock` scan. Built-player scenario runs with frames and `run.jsonl` from M1.A.

## 12. Decision log (agent decisions; owner rulings are in CANON.md)

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
