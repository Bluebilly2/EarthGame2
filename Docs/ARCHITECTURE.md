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
once at world creation and saved as layers. Nothing in the landscape is placed.

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

## 3. The world

- **Frame:** tangent plane at the region centre, +X east, +Y up, +Z north, planet radius 6,371,000 m
  (`LocalFrame`, `WorldPoint`, `Double3`, ported from v1 with `CoordinateTests`).
- **Extent:** `Region.ExtentM = 8000`, stated once, enforced by the server (position clamp) and by a source scan
  that no other literal metre appears in the world code. The edge: the sea on the coast sides; on land a soft
  boundary with a "not yet" vignette on the client; animals treat it as impassable; a static far skirt draws
  what lies beyond.
- **Region:** `bherwerre` — centre 35.140°S 150.675°E; box 150.6311–150.7189°E, 35.1761–35.1039°S; wake point
  about 35.159°S 150.6485°E (Cave Beach swale, snapped to the 1 m DEM at bake time). Fallback region: Ulladulla.
- **Data at the region's true resolution:** AWS Terrain Tiles at zoom 14–15, baked by `Tools/data/bake_region.py`
  to `Data/regions/bherwerre/heights.r32` with a sidecar recording the effective source resolution per tile
  (SRTM 30 m inland, Geoscience Australia 5 m where present). Procedural detail (v1's `TerrainSynthesis` recipes
  and double-precision `Noise3D`) only below the recorded resolution. A coarse zoom-11 bake over 64 km feeds the
  far skirt and the terrain-class/maritime context; v1's global 16 MB grid (`Data/global/`) serves
  `TerrainContext`, `TerrainClassifier` and `MaritimeMap`.
- **Layers, computed once at world creation and saved:** pit-fill and flow routing on the DEM
  (`DrainageNetwork`); wetness by multiple-flow TWI and soil depth/texture/fertility (`SoilModel`); plant
  community (`PlantSpecies`: suitability product, dominance², overstory then understory); animal capacity
  (`AnimalSpecies`); water bodies with a fresh/salt flag; a topology bitmask. **No landform evolution on real
  data** (decision 2026-09-07: the valleys in a 5–30 m DEM are already the record of what water did; v1's
  erosion existed to replace a 9.8 km/px gradient). `LandscapeEvolution` is ported but dormant.
- **Layer format:** one `RegionRaster` header (magic, version, name, width, height, cell size, origin lat/lon,
  dtype, source resolution, checksum) shared by the Python writer and the C# loader; the loader's tests read
  Python-written fixtures. Layers and dtypes: heights f32 (or i16 cm), soil depth u16 cm, wetness u8, community
  (overstory id u8, understory id u8, suitability u8), ground colour rgb + wetness alpha, water u8 class + fresh
  flag, topology u32.
- **The wake point** is chosen by the world-creation pipeline scoring cells against the criteria (water ≤ 500 m,
  knappable stone ≤ 1 km, fibre and firewood ≤ 500 m, shelter rock ≤ 2 km) and written into `world.json`; the
  same scorer is the "read country" census test.
- **The ground-colour oracle** (v1's `GroundColourAt`) is the one function that says what a square metre looks
  like from what it is; terrain textures, grass, litter and impostors all derive from it.
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

- The engine owns an entity store: stable ids, a definition id, position/orientation, plain-struct components
  with per-field dirty flags; server-authoritative, client-mirrored; lifecycle Spawn → Init → … → Kill.
- Definition id: a stable string key (`species/blackbutt`, `stone/silcrete`, `item/cobble`) hashed FNV-1a 32-bit
  at load with a uniqueness test; the id travels in the spawn message; `PrefabRegistry` maps key → prefab; an
  edit-mode test asserts every engine definition has exactly one binding and every binding a definition.
- Fauna and trees: the presence function is the source of expectation; the server materialises animals as
  entities inside each player's interest radius, simulates them, and dematerialises them outside; trees inside
  the radius are entities; grass and litter are layer-derived instances, never entities.
- Dropped items rest where the server says; the client animates the fall.
- Interaction: v1's `PlayerAction` model; verbs are intents sent to the server; the server commits.

## 6. Save and world folder

`Saves/<world>/world.json` (seed, region, extent, wake point, created, protocol version), the baked layer rasters,
`regions/r.X.Y.egr` (one file per 512 m cell: entities and mutable layer diffs; atomic write), `players/<id>.egp`
(server-side), `digest.txt` (a twelve-significant-figure digest the tests diff). The server is the only writer.
Additive fields with initialisers; every persisted field enters the digest in the same commit. Solo worlds are
byte-identical to server worlds.

Landed 2026-09-08 (M1.A): `world.json` as format `eg2.world` version 1 (region, seed, extent, created and saved
dates, protocol version, tick, the clock's two numbers) and a player's resting place as `players/<name>.json`
(position, yaw, pitch, grounded, tick). The binary `.egp`/`.egr` files arrive with the entity store (M1.3); the
player file moves to `.egp` then, as a versioned change of format, not a quiet rename. `WorldSave` in
`EarthGame.Server` is the writer; every file is written to a `.part` and moved into place.

## 7. Networking

- **Rule 1 — there is no single player.** SOLO = server + client in one process over `InMemoryTransport`; every
  message serialised and queued; no fast path.
- **Rule 2 — the server owns the simulation.** Clients predict visually, never commit.
- **Rule 3 — split by assembly, not `#if`.** **Rule 4 — engine-free core, exploited** (the server is tested by
  dotnet). **Rule 5 — dirty masks and interest management from day one** (a radius filter for ≤ 8 players; a
  grid later; a per-connection bandwidth budget; baseline streaming on join by proximity).
- **Transport:** `ITransport` (Update/Poll; reliable and unreliable delivery; a peer-stated close reason is
  flagged `ReasonFromPeer`) with `InMemoryTransport` and LiteNetLib over UDP (vendored, MIT; its loss/latency
  simulator is the N1–N4 harness, always compiled with `SIMULATE_NETWORK`).
- **Handshake (protocol v1):** the first message must be `Hello` (protocol version, player name, password); the
  server answers `Welcome` (session id, seed, region, time, tick, tick rate) or `Refused` (reason) and closes with
  the same reason. A malformed packet is a refusal, never a crash. Over UDP a close can overtake the `Refused`
  message, so a client in the handshake treats a peer-stated close as the refusal.
- **Movement:** the client runs the engine's `Step()` locally and sends `{tick, input, resulting state}`; the
  server validates (max speed × dt × tolerance; heightfield ground clamp; penetration probe), logs violations and
  corrects with a sequence number. Inputs are on the wire from day one so server-side simulation with client
  prediction is a later switch.
- **Hosting:** "Host game" listens on a port; join by IP:port with an optional shared password in `Hello`
  (LiteNetLib has no encryption — friends only); Tailscale for CGNAT; `EarthGame.ServerHost` is the dedicated
  console (`+server.port`, `+server.password`, `+server.maxplayers`, `+server.seed`, `+server.region`; `status`,
  `pause`, `resume`, `stop` on stdin).
- **Plan B (true cost):** FishNet 4.7 requires the server to be a Unity process; adopting it replaces
  `EarthGame.Server`'s replication and its dotnet tests and forfeits the pure-.NET dedicated server. Decided at
  the week-3 checkpoint against N1–N4 only.

### 7.1 The M1.B checkpoint criteria (owner ruling, ratified 2026-09-07 with three amendments)

"Healthy" has no adjectives. The decision is made against N1–N4 and nothing else; if any fails and cannot pass
within one further contracted week, plan B is taken. Sunk cost does not vote. Each is measured by a scenario the
corpus runs, its raw results in the contracted run logs (§10), recomputed independently by the owner's
`join_check.py`.

- **N1 Join-in-progress, time-to-interactive.** *Interactive* = the joining client holds the nine 1 km layer
  tiles around its player, has collidable ground under the player, and has applied its first entity snapshot.
  Pass: ≤ 10 s at 100 ms RTT ±20 ms jitter, 2% loss, 10 Mbit/s send cap; ≤ 20 s at 200 ms RTT, 5% loss,
  5 Mbit/s send cap. (Nine tiles of heights, community and colour are about 3 MB compressed, roughly 2.5 s at
  10 Mbit/s; 10 s is where a person suspects a hang; 200 ms and 5% loss is worse than any Australia-to-Australia
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
- Budget: 13.3 ms design at 1080p-internal (60 fps hard floor at 16.7 ms; any frame over 33 ms is a named
  defect): shadows 1.6, terrain 1.8, grass/understorey 3.0, trees 2.0, props 0.7, fauna 0.6, sky/fog/water 0.8,
  post 1.2, UI 0.4, reserve 0.6; SSAO off until measured. CPU: sim tick 2.0, streaming hard-capped 1.5 (a
  Stopwatch budget, never an item count), culling/submit 2.0, physics 1.5, fauna 1.0, UI/scripts 1.0. VRAM ceiling
  3,500 MB of content. Measurement: uncapped, visible window on a named display at a stated resolution,
  median/p95/worst, machine load recorded.
- Terrain: tiled Unity Terrain (64 tiles of 1 km at 1025 posts; the 3×3 around each player at full detail and
  collidable; a static far skirt beyond the boundary); ground look from the oracle via per-tile colour + wetness
  textures sampled by a Shader Graph terrain material over ≤ 4 physical layers. No "build all now" path.
- Flora: Terrain detail instancing first, BatchRendererGroup only if measured over 3 ms; trees as prefabs
  (parametric skeleton + authored leaf/bark materials, LODGroup + billboards). Fauna: sourced meshes.
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
| Wire protocol | 1 (Hello, Welcome, Refused, Ping, Pong) | `EarthGame.Protocol` | server, client | `Messages.cs`; `ProtocolInfo.Version` |
| `RegionRaster` (`eg2.raster`: a `.r32` float32 grid + JSON sidecar) | 1 | `Tools/data/raster_io.py` (the bake and the fixture writer both call it) | `RegionRaster.cs` (server and client), owner verifiers | Sidecar keys: format, version, name, region, dtype f32, byte_order little, width, height, cell_m, extent_m, centre_lat, centre_lon, min_m, max_m, sea_fraction, source, sha256. Row 0 north, column 0 west, cell centres at east = col·cell − extent/2, north = extent/2 − row·cell; width = height = extent/cell + 1. The loader refuses any other version. |
| `eg2.almanac` (one JSON line) | 1 | `Almanac.cs` via the almanac console tool | `solar_check.py` | Keys: format, version, time_basis, day_of_year, latitude_deg, longitude_deg, declination_deg, daylight_hours, sunrise_local_hour, sunset_local_hour, noon_elevation_deg, wake_local_hour, wake_elevation_deg, wake_azimuth_deg. Hours are local mean solar time: no zone, no equation of time. |
| `world.json` (`eg2.world`) and `players/<name>.json` (`eg2.player`) | 1 | `WorldSave.cs` (the server) | server (continue), the shell (which world is newest) | Keys of world.json: format, version, region, seed, extent_m, created_utc, saved_utc, protocol_version, tick, clock {total_hours, started_at_hours}. Player: format, version, name, east, up, north, yaw_deg, pitch_deg, grounded, saved_tick. |
| `run.jsonl` (the recorder, `eg2.run`) | 1 | `RunLog.cs` (ClientCore), written by the recorder | `corpus_check.py`, `join_check.py` | One JSON object per line. Line 1 is the header: `format`, `version`, then what the run says about itself (scenario, region, seed, session, spawn, started_utc, unity, terrain). Every later line starts `t` (real seconds since the run began), `tick` (the server tick last known to the client, −1 before any), `kind`, then the record's fields. Kinds in M1.A: `frame` (file, width, height, east, up, north, yaw_deg, pitch_deg, grounded, corrections), `error`, `exception` (message, stack), `end` (frames, errors, corrections, moves_sent, east, up, north). |
| join log, soak log | — | the N1–N4 harness | `join_check.py` | M1.B contract (not yet) |

A row moves from "not yet" to a version number in the commit that first writes the format; the version field is
mandatory in every file from the first write.

## 11. Verification

- `dotnet test Engine/EarthGame.slnx` — the engine-free suite; exit code is the verdict; never piped.
- `Tools/gate/run_gate.py <gate>` — refuses when an owner verifier is missing or a stub, then runs the gate's
  steps; `Tools/hooks/pre-push` runs the engine suite, the Unity compile check when present, and the verifier-lane
  authorship rule.
- Unity edit-mode tests: the settings checklist, prefab instantiation, registry completeness, the
  `MaterialPropertyBlock` scan. Built-player scenario runs with frames and `run.jsonl` from M1.A.

## 12. Decision log (agent decisions; owner rulings are in CANON.md)

| Date | Decision | Why |
|---|---|---|
| 2026-09-07 | Hand-rolled protocol over the engine-free core; FishNet is plan B | FishNet/NGO/Mirror require a Unity server process, forfeiting the engine-free server, its dotnet tests and the pure-.NET dedicated server |
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
