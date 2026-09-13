# World generation: a brief for another agent

Written 2026-09-13 by Claude (Opus 5), which builds the beta arc in this repository, for the agent William is bringing
in to work on world generation as a side project. It is what that agent needs to know before it changes anything, so
that the two lines of work do not collide and nothing the project has already proved gets quietly broken. Amended the same day for CANON ruling 26.

## 1. Read these first

- `CLAUDE.md` — the house rules. `AGENTS.md` points to it; it is the one set of rules for every agent here.
- `Docs/GAME_DESIGN.md` — the constitution, written by William. Its § numbers are the shared vocabulary.
- `Docs/CANON.md` — what William has decided. **Only his decisions go in it.** Never record your own choices there,
  and never record one of his questions or remarks as a ruling.
- `Docs/ARCHITECTURE.md` — how the game is built. §5 is the world; §10 lists the contracted formats; §12 is the log of
  decisions agents have made, each with its reason.
- `Docs/STANDARDS.md`, `Docs/DEBTS.md`, `Docs/WORKING.md`, and the contracts under `Docs/contracts/`.

## 2. How to work alongside the beta arc

- **Your own judgement.** William's word of 2026-09-13 (CANON ruling 26): you are told how to get started, not
  what to do, and what you work on, and how, is your decision in the project's favour. You open your own
  contracts under `Docs/contracts/` and land them; the existing contracts show the shape: "What was found",
  "What this slice promises", "Non-goals", "How it is proved", and an exit record once it lands.
- **Sharing main and the machine with Claude** is `Docs/WORKING.md`'s section "Two agents, one machine".
- **Tell William before you change a format or a layer** that the game streams or draws, so he can sequence it with the
  beta arc. On 2026-09-13 Claude added two tile layers for the far forest (layer 6, the far stand, and layer 7, the
  far count), which the server works out from `stand` whenever a tile is first asked for, and moved the wire
  protocol to version 8 (contract `Docs/contracts/M1.6d_THE_FAR_FOREST.md`). A change to `stand` reaches them too:
  a far square is 40 m (`TileLayers.FarCellM`), which must stay a whole number of the stand's cells: the server
  serves no far layer from a stand whose cells do not make it whole, and the far forest is then not drawn.

## 3. How the world is made today

1. **The bake** (`Tools/data/`): elevation from the Terrarium terrain tiles and water bodies from OpenStreetMap, for the
   8 km Bherwerre region, written as rasters (`RegionRaster`, `eg2.raster` version 2).
2. **World creation** (`WorldCreation.Create` and `WorldLayers` in `Engine/packages/com.earthgame.engine`): seeded and
   deterministic. It turns the bake into the world folder's layers — heights, surface, soil depth, wetness, water
   class, overstory and understory plants, topology, stone, the ground's cover, the stand of trees, the loose sticks and
   cobbles, the distances to resources, the carrying capacities and the scorer's wake. ARCHITECTURE §10's row for the
   world folder's layers states every one, with its units and codes. `python Tools/world/create.py <folder>` creates
   a world; the gate world is seed 1347.
3. **Streaming** (`TileCodec`, `TileService`, `TileReceiver`): kilometre tiles of the layers a client needs, cached on
   disk by checksum.
4. **Drawing** (`Unity/Assets/EarthGame/Client`): the ground's colour and the understorey from `cover`, the water, the
   trees from `stand`, the litter from `loose`.

## 4. Rules that will bite

- **Generation changes ripple.** Change a layer rule and the layer checksums change, the scorer's wake can move, and
  the corpus's walking loop can be left off the wake (DEBTS, "the corpus loop is tied to one world's wake"). After a
  rule change, on a freshly created world: `Tools/corpus/lay_loop.py`, the corpus walk (`Tools/corpus/run.py
  --scenarios walk`) with `join_check --only N2`, and the checks under `Tools/verifiers/checks/` — `drainage_check`,
  `census_check`, `species_check`, `stand_check`, `tile_check`, `save_check`, `region_stats --world`.
- **It is a real place** (rulings 5, 21, 22). The layers are held against real records — the Atlas of Living
  Australia, BioNet, OpenStreetMap, the Bureau of Meteorology. Every dataset or vendored file lands with its entry in
  `THIRD_PARTY_NOTICES.md` in the same commit. **Nothing is downloaded without William's permission.**
- **Every other rule** is `CLAUDE.md`'s and `Docs/STANDARDS.md`'s, and binds you as it binds Claude.

## 5. Machine rules

`Docs/WORKING.md` holds them: its traps, and its section "Two agents, one machine".

## 6. Where world generation is already owed work

These are open in `Docs/DEBTS.md`, each with its measurements:

- **Plates of land stand in the sea** — flat plates at the datum that the sea rule takes for land.
- **Lake beds are the bake's ground** — a lake's depth is the tiles' noise, and its shore is where that noise crosses
  the level.
- **The sea's floor is a rule, not the bathymetry** — Geoscience Australia's Jervis Bay grids are unread.
- **The cliffs are thin** — a finer bake needs a download William has not yet allowed.
- **The plants the records still find nearer the sea** — the salt wind is the lead.
- **The stem-density benchmark is unread.**

## 7. If the work goes to planet scale

William has asked whether the world could be generated around a sphere with Earth's shape, and said that later, past
the beta arc, space would become a place a player moves into. That is a question and an intention, not a ruling;
CANON ruling 5 (one real region first) stands until he says otherwise. What the project already knows:

- The predecessor built a real-scale planet and froze it, because a flat-world renderer retrofitted to 6,371 km
  coordinates broke in seven places. Its account is `C:\Users\willi\projects\Earth Game\Docs\SLICE1_4_PLANET_REWRITE.md`.
  Build planet-centred double-precision coordinates on the server and re-centring on the client in from the start.
- Every region is already pinned to a real latitude and longitude (`Region`, `LocalFrame`), so each is a flattened
  patch of the sphere.
- Regions tiling the globe need seams that agree, and rivers cannot be generated one region at a time, because a
  river's size depends on everything upstream: a worldwide river dataset would be needed.
- Generating the world as it is visited means fetching data while playing, which is a download and a licence question,
  and so William's.
