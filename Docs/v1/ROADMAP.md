# Earth Game — Development Roadmap

**Status:** approved 2026-08-25. Amended the same day to incorporate `THE_CIVILISATION_OF_ONE.md`.

## The three documents

| Doc | Role |
|---|---|
| `GAME_DESIGN.md` | **The constitution** — how the world must be simulated. Governs every technical decision. |
| `THE_CIVILISATION_OF_ONE.md` | **The north star** — the ascent as it plays when played well, Ages I–IX. Governs content direction, feel, and progression. |
| `ROADMAP.md` (this) | **The build order** — phases, slices, and gates. Governs what gets constructed when. |

Where the chronicle and the constitution appear to conflict, the constitution wins on *mechanism*, the chronicle wins on *destination*.

## Canon decisions (owner, 2026-08-25)

- **Wake point:** 34.5° S, 150.4° E — Southern Highlands, NSW. ~700 m elevation, late winter, 8am, 25 August 2026. This is **pilot region #1** for all DEM/hydrography/climate slices, and the canonical site for the Phase 2 gate.
- **The founder is ageless.** One of the few rules the game deliberately breaks. Canonically a 20–30 year old adult. There is **no mortality clock and no capability-vs-decline race** — the chronicle's "Standing Question" is explicitly *not* adopted as an endgame. Medicine remains valuable for injury and disease, not as a race against death. (Character customisation on new-game creation: far-future, not roadmapped.)
- **The tablet is a physical object.** The founder wakes holding it: an iPad-like slab with cameras, microphones, and whatever sensors it needs. Unbreakable, never dies. It is the **oracle that cannot act** — it identifies, explains, plans, and coaches, but lifts nothing. Later in the ascent the player can **physically connect it to things they build** (a home-made computer, a heads-up display, instrumentation), which is a real progression axis, not flavour.
- **Mechanically the tablet IS the §5 knowledge system** and is fully §34-compliant (no LLM required at runtime): species/mineral identification from the same real datasets the world is built from; the capability dependency graph; authored technique coaching per domain spec (§35); "perfect geology" because it reads the same geological data the world reads. Its sensors are diegetic justification for identification UI. **Doctrine of the Deaf Machine** (chronicle, Age VII) is adopted as design law: every autonomous system the player builds must function with the tablet absent.
- **Megafauna are canon:** diprotodon, marsupial lion, and the rest of the unextinct Pleistocene fauna — the §2 "no human-caused extinctions" clause made literal. Phase 3.4 content direction.
- **Time scale is a first-class architectural concern**, not a Phase 4 detail. The chronicle spans ~50 years: seasons, multi-year selective breeding, decades of unattended industry. Its mechanics get their own contract at the Phase 3 re-plan, and every phase from 2 onward states its time-compression assumptions.

## Gate philosophy (6 rules — every gate satisfies all)

1. **External referent.** Gates compare the game to something outside the project: published data, real maps, reference calculators (NOAA solar, JPL), or a real person's real-world knowledge — never developer taste.
2. **Owner picks the probe at gate time.** Gates declare a *class* of targets ("any coastline", "any real technique"); the specific coordinate/date/technique is chosen after the build. Teaching to the test is structurally impossible.
3. **Transfer test (§48 Level 4).** Gameplay gates must be passable by real-world knowledge without game docs — and failable for the correct physical reasons, explained by the causal log.
4. **Automated floor + playtest ceiling.** Both mandatory: (a) the phase's §36 validation suite green with numeric error budgets; (b) an owner playtest of the broad experience.
5. **Binary and pre-written.** Gate criteria live in the slice contract before code. Pass/fail; a failed gate reopens the slice, never the criteria.
6. **No fudge inputs.** Gates run from a clean clone at fixed seeds. Any code path special-cased to a gate scenario is an automatic fail.

## Delivery mechanism (per slice)

A binding `Docs/SLICEn_*.md` contract written **before** code — domain spec (§35), error budget (§37), exact API, §36 validation tests, feel checklist — then parallel-writer build, review pass, fixer, batch compile + tests. Established by `SLICE1_BOLT_FACTORY.md`; that file is the template.

---

## Completed

- **Milestone A — Planet prototype** (`ARCHITECTURE.md`): first-person walker on a toy 2 km procedural planet; cube-sphere quadtree LOD, radial gravity, fly-to-space, day/night, ocean, atmosphere, scatter, HUD. Runtime-generated.
- **Milestone B — Slice 1, the bolt factory** (`SLICE1_BOLT_FACTORY.md`): deterministic engine-free sim core proving process capability → property distributions → metrology → assembly → emergent reliability → causal failure explanation. 14/14 validation tests. Chronicle position: the Age V machine-shop physics, built first as a test rig.

---

## Phase 0 — Groundwork *(days, deliberately boring)*

Make gates trustworthy: a gate run from an unversioned working copy proves nothing.

- **0.1** `git init`, commit all current work, tag `v0.2-boltfactory`; large-data policy (raw Earth datasets live outside the repo in a cache dir with a fetch/preprocess script).
- **0.2** Headless validation: `EarthGame.Sim` tests run on plain .NET via a mirror csproj (no Unity licence needed in CI), plus the editor batch-mode run.
- **0.3** CI smoke: a standalone Windows build that boots to the planet.

**Gate.** Fresh clone on a machine that has never seen the project: fetch script pulls data, CI green, standalone build boots, owner walks the planet and completes one bolt-factory cycle.

---

## Phase 1 — Real Earth, Really There *(the identity phase)*

The planet becomes Earth — shape, then scale, then local detail — all behind the existing seam `TerrainNoise.SurfaceRadius(dir)` that meshing, spawning, scatter, and collision already call.

- **1.1 Earth by shape.** ETOPO 2022 global relief (15 arc-sec, public domain) → cube-sphere tile pyramid; `SurfaceRadius` becomes a tile sampler, noise demoted to sub-data-resolution detail. Planet stays small-radius/float.
- **1.2 True scale — the precision migration.** Radius 6,371 km, g 9.81; double-precision world coordinates as a sim-layer service; floating-origin camera-relative rendering; chunk-local mesh origins in `ChunkMeshBuilder`; quadtree MaxDepth 7 → ~20. The prototype's no-Jobs/no-async constraints are formally retired here for meshing/streaming.
- **1.3 Earth-following procedural terrain.** *(Re-scoped 2026-08-25 on the owner's direction.)*
  Storing the real world at human resolution is not possible — 30 m for the whole Earth is
  terabytes, and 1 m is a planet-sized dataset nobody can ship — so real data stays the
  **skeleton** and procedural generation invents everything below its resolution, *conditioned on
  what is actually known about that place*: elevation, ruggedness, latitude, distance to coast,
  and later real geology and climate. Each terrain class (alpine, plateau, escarpment, coastal
  plain, dune desert, abyssal plain…) carries its own synthesis rules, blended smoothly.
  This is an algorithm we keep improving rather than a download we keep growing, and it matches
  GAME_DESIGN §6: model the reasons a landscape has its shape, do not store the answer.
  Real high-resolution DEMs remain available later as a *validation reference* and as an optional
  overlay for a small pilot region — but they are no longer the plan for detail.
- **1.4 A place to stand.** Lat/lon spawn picker (defaulting to the wake point), real-coordinate HUD + compass, ocean/atmosphere re-scaled, fly-to-space preserved.

**Gate.** Owner picks at gate time: (a) any coastline on Earth — spawn there, visible shape matches the real map; (b) any landform inside a pilot region — recognisable against a topo map; (c) fly to space — unmistakably Earth. Floor: sampled `SurfaceRadius` vs held-out ETOPO points within stated RMS; 1,000 HUD-metres = 1,000 real geodesic metres; sub-pixel camera-jitter test at six widely separated points including the origin anchor's antipode; frame budget met on owner hardware.

**Risk.** The migration touches everything assuming float world-space — precisely why it happens before content exists.

---

## Phase 2 — The First Survivor *(Ages I–II; first real content)*

Embodied survival on pilot regions, reusing Slice-1 statistical machinery. The chronicle's Age I is the design target: **hypothermia is the first enemy**, triage order is orient → insulate → fire → water → food.

- **2.1 Hands, eyes, and the tablet.** Crosshair inspect/interact (§40); scatter trees/rocks become sim-backed entities; carrying; the tablet as a held object with its identification UI.
- **2.2 Body as a system.** Human heat balance (metabolism, wind, radiation, insulation) validated against published physiology; hydration and energy; ageless but *killable*. Climate v0: analytic temperature by latitude/altitude/season/diurnal + NOAA-validated solar position.
- **2.3 Stone, string, fire, shelter.** Split on 2026-08-25 into the tool-free half and the rest,
  because a naked founder can build a debris shelter with bare hands and cannot make fire without
  cordage first — so shelter is what the first afternoon actually consists of.
  - **2.3a Insulation** (`SLICE2_3_INSULATION.md`) — **done.** Property-driven insulating materials;
    litter on the forest floor with moisture derived from dew, canopy, ground shape and how deep
    the founder rakes; debris shelters whose bedding and walls are modelled as the two different
    pieces of physics they are. 20 validation tests, including four that run the real body against
    the real climate for a whole night and ask whether the founder is alive at dawn.
  - **2.3b Fire** (`SLICE2_3B_FIRE.md`) — **done.** Hand-drill friction ignition as a heat balance
    at the contact patch, not a progress bar: the wood and its moisture decide whether the patch
    can ever reach the point where dust becomes a coal. Grass trees in the world for the spindle
    the chronicle names; bark fibre for tinder; deadwood carrying species and thickness so what a
    stick is good for follows from what it is. Fires burn by size class, radiate by the inverse
    square, and warm one side of the founder. 14 more validation tests, four of them whole nights.
  - **2.3c Water** (`SLICE2_3C_WATER.md`) — **done.** Drainage computed from the terrain the player
    is standing on: D8 flow directions, Priority-Flood depression filling, catchment accumulation,
    and channels classified by how much land drains through them. No hydrography dataset — water
    is where *this* ground sends it, so reading the slope works. Thirst gained teeth: work capacity
    interpolated from published dehydration figures, which takes away the shivering that gets a
    founder through a cold night, and death at 15% of body water in a little under three days.
    Measured at the wake point: water 52 m away and 8 m below, found in five downhill steps.
    **Found a terrain-fidelity gap on the way** — pit filling had to raise 16.6% of cells by a mean
    of 3.12 m, which says the procedural terrain has far more closed basins than a real eroded
    landscape. Recorded for Phase 3.2 rather than hidden; in the meantime those basins hold ponds,
    which is what closed basins do.
  - **2.3d Stone and string** (`SLICE2_3D_STONE.md`) — **the hammer half is done.**
    `PoundingQuality` derived from the same five mineralogical numbers `EdgeQuality` is, and asking
    the opposite of them, which produced an inversion nobody wrote down: flint is 0.95 for an edge
    and 0.05 for a hammer, basalt the reverse. Granite lost to basalt on grain coherence. The
    founder pounds with the best stone they are carrying, and a poor one takes far longer rather
    than failing.
    **Still owed:** knapping as process capability (Slice-1 machinery on conchoidal fracture);
    **cordage** (the chronicle's Age II keystone unlock — bindings, snares, and the fire-bow that
    retires the hand drill); rock-overhang shelter. Both are named in the Phase 2 gate.
- **2.4 A night alive.** — **done.** Save/load through `StateDigest`, a canonical text rendering
  of a save plus a hash of it, so a round trip can be proved rather than assumed. Verified at
  three levels: the sim round trips, Unity's serialiser, and a world torn down and rebuilt
  from the file. Bit-identity is replaced by a stated §37 budget of twelve significant
  figures — finer than anything observable, coarser than a serialiser's last-digit noise, and
  still five figures beyond what a float can hold.
- **2.5 Sleep** (`SLICE2_5_SLEEP.md`) — **done.** The night lived at speed rather than skipped.
  One clock: `GameSession.TimeScale` is the only thing that decides how fast the world runs, and
  the body, the fire and the sun all read `InGameSecondsThisFrame` — measured in the player at
  handed 5.90 h against sun moved 5.90 h. The founder wakes to light, to cold, or by choice.
  **It found a hole in the body model on the way:** sweating cost water and shed no heat, so a warm
  day dried the founder out and cooked them at once. Sweating is now demand-driven, like shivering.
- **2.6 Food** (`SLICE2_6_FOOD.md`) — **done.** The fourth need. Everything edible is a part of one
  of the plants the ecology already places, so finding food is reading country rather than learning
  a spawn table. A ninth species joined for it: the yam daisy, at its annual peak in late August.
  Measured across 1.6 km: 53% of the ground has nothing, 39% has a nine-calorie mouthful, and the
  food worth digging is on seven per cent of it. Living on bracken is 4.8 hours of work a day, and
  you cannot eat it until you can cook — which is what makes the fire matter for more than warmth.
- **2.7 The oracle's first words** (`SLICE2_7_ORACLE.md`) — **done.** The tablet answers the two
  questions it could not: *what can I attempt and what is stopping me* (§5) and *why did that fail*
  (§23). Five capabilities, each assembled from live queries against the same models that decide
  the attempt — so the oracle cannot promise what the world will refuse, and two tests exist purely
  to hold that line by putting its verdict beside the mechanism's, stone by stone and moisture by
  moisture. Naked on bare ground it reports the night as 748 W leaving against 422 W arriving, short
  326 W, 0.4 h to hypothermia, with shivering covering 350 W of it for three hours and then
  stopping. **It forced a refactor that was overdue:** the body's heat loss lived inside `Tick`, so
  a forecast would have been a second copy that drifted; it is now one method both call.
  **And the tests found a real gap** — swing energy went as hammer mass while Auerbach's critical
  load goes as mass^(2/3), so a bigger hammer was always better and the law had no teeth. An arm
  energy ceiling fixed it.

**Gate.** Transfer test: a playtester with real bushcraft knowledge (owner recruits; owner also plays) wakes naked at the canonical wake point in late winter with no game documentation and, using only real-world technique, survives the first night and reaches a stone cutting edge and cordage within 48 in-game hours. The mirror: wrong technique (wet wood, poor stone, exposed camp, ground-conduction heat loss) fails for the correct physical reasons with causal-log explanations. Floor: per-domain validation vs published values (ignition temperatures, thermoneutral ranges, stone fracture properties) within budgets; persistence hash test green.

*Time-scale assumption: real-time to modest compression; multi-day survival is played, not skipped.*

---

## The ecosystem pillar *(added 2026-08-25 on owner direction)*

`ECOSYSTEM.md` is binding alongside the three governing documents. The owner's direction — *"I want
to design a real ecosystem and let it form naturally"* — reframes terrain, water, soil, vegetation
and fauna as **one chain of consequences** rather than five features: rock → landform → water →
soil → plants → animals, with nothing in it placed.

It began because water drawn on un-eroded noise looked exactly like what it was, and the numbers
said why: 53% of the ground sat in closed hollows, because fractal noise makes hummocks and only
water makes valleys.

- **E1 Landform** (`SLICE_E1_EROSION.md`) — **done.** A landscape evolution model (uplift, stream
  power, hillslope diffusion) runs on the terrain synthesis, which becomes bedrock rather than the
  answer. Validated against published geomorphology: slope–area concavity, steady state within a
  factor of two of theory, hollows down an order of magnitude, hard rock standing up. 0.91 s for a
  million years.
- **E2 Water, again** — largely folded into E1: the drainage now follows valleys the water made.
  Remaining: wetlands on flat wet ground, and real lakes once hydrography arrives.
- **E3 Soil** — **done.** Depth from the soil production function against hillslope creep
  (thin ridges, deep hollows, bare steeps); wetness from the topographic wetness index with
  multiple-flow-direction accumulation; texture and fertility derived from the parent rock's
  grain and hardness. The ground's colour now comes from these, so a dry spur is straw and a
  gully is dark for the reason they are in the field.
- **E4 Plants**, **E5 Animals** — the direction. Each gets its own contract.

This supersedes the terrain half of 1.3 and much of 3.3.

## Phase 3 — Living Earth *(planet-wide world systems)*

- **3.1 Climate from data.** Monthly climatology tiles (WorldClim/ERA5-derived) driving temperature/precipitation/wind by place, season, hour; stochastic weather consistent with climatology.
- **3.2 Water where it belongs.** Real rivers and lakes (HydroSHEDS) reconciled with the DEM; springs and drainage logic so terrain-reading transfers.
- **3.3 The green machine.** Biomes *computed* (Köppen from the climate field, cross-checked against the published map); property-driven vegetation — including the chronicle's stringybark, paperbark, grass-tree, lomandra, murnong, wattleseed.
- **3.4 Things that move.** Regional fauna as statistics promoted to individuals near the player (first §18 taste); **Pleistocene megafauna canon** — diprotodon, marsupial lion; yabbies, fish, possums, macropods; nutrition and pelts close the survival loop.
- **3.5 The real sky.** Axial tilt, Moon phase/position, bright-star catalogue, planets as points — celestial navigation becomes transferable.

**Gate.** Three owner-picked unseen locations match reality (biome, vegetation character, the river that should be there is there and flows the right way). Transfer probes: reach fresh water from an owner-picked ridge by terrain reading alone; determine latitude within ~2° by a real night-sky technique. Floor: Köppen agreement above stated threshold; river topology vs HydroSHEDS within budget; sun/moon vs NOAA/JPL within 0.5°.

**Re-plan trigger:** time-scale/seasonality mechanics get their own binding contract at this phase's planning pass; Built-in-RP decision formally revisited here.

---

## Phase 4 — From Stone to Machine Shop *(Ages III–V)*

The chronicle's homestead-to-machinist arc, and the phase that retires the Workshop panel as a test rig. **Free time is the real resource** (Age III) — food security must become cheap enough to buy engineering time.

- **4.1 Reading the rocks.** Surface lithology and mineral occurrence from real geological data — clay, flint, iron-bearing stone, limestone, coal findable by real prospecting knowledge; the tablet's "perfect geology" is literally this dataset.
- **4.2 The homestead.** Clay → pit-fired pottery → waterproof vessels (boiling expands the edible world); permanent dwelling; food storage, drying, smoking; the daily-hunger treadmill breaks.
- **4.3 First metal.** Kilns → charcoal → bloomery → iron tools. Formal §35 metallurgy domain spec; sim-time acceleration mechanics land here if not already settled.
- **4.4 The measurement bootstrap.** Precision from imprecision — straightedge, square, balance, gauges (three-plate method); instruments become in-world artifacts whose σ derives from their build history (§13, §14, §27).
- **4.5 The machine that makes machines.** Water wheel + camshaft (trip hammer, bellows, saw frame) then the water-powered lathe and boring rig; the hand-filed first leadscrew cutting better leadscrews. `LatheState` can no longer be conjured — only built. Knowledge/quest v1: goal-planner decompositions over the capability graph.

**Gate.** Chain transfer test: from a Phase-2 save, a player applying real metallurgical/craft knowledge (owner + a knowledgeable playtester) reaches a working iron tool and then a functioning lathe with no game-specific recipes. The lathe's measured σ is a consequence of its build history, causally explainable three levels deep. Floor: metallurgy/thermal validation vs published data; **structural fudge-check — the magic pre-existing workshop is deleted from the codebase and no code path instantiates a machine except from in-world construction.**

**UI debt comes due here** (OnGUI → real UI, forced by knowledge and engineering interfaces, §42).
**Mandatory deep review after this gate** before detailing Phase 5+.

---

## Phase 5 — Power and the Absent Player *(Age VI, first half)*

- **5.1 Harnessed energy.** Water/wind as the first §7D network (shafts, loads, losses), sited against real hydrology; the campus-along-the-river layout becomes possible.
- **5.2 Heat into motion.** Boiler/steam with honest thermodynamic budgets; cement, quicklime, coke, glass as the parallel kiln program (glass matters for lenses and chemistry vessels, not windows).
- **5.3 The absent player — sim LOD.** §18–19 at the first moment genuinely needed: unattended facilities as aggregate flow models, promoted to detail on approach or anomaly; a mass/energy conservation ledger across LOD transitions as a tested engine invariant (§47).
- **5.4 Many hands.** Automated production lines; stockpiles→flows inventory transition (§41); civilisation dashboard seed (§43); Kardashev as a *measured* readout — the chronicle's "continuous watts under command" (§33).

**Gate.** Build a water-powered automated line at an owner-picked river site (anywhere real flow data supports it); leave 500 km away for N sim-days; return. Pass requires: the production ledger balances (every kilogram and joule accounted); the aggregated line's output statistics match a fully-detailed simulation of the same line within tolerance (**aggregation-equivalence — the un-fudgeable heart of §18**); an owner-chosen injected fault (tool wear, furnace drift, low water) propagates into the product population and is explained end-to-end.

---

## Phase 6 — The Current and the Nervous System *(Ages VI–VII)*

- Electricity from primitives: copper, magnets, dynamos, transmission, motors, electric furnaces, electrolysis — the river's chain broken.
- Interchangeable parts, standardised threads, jigs and fixtures, cam-driven semi-attended machines — labour encoded into hardware.
- **Self-replication proof:** machine shop #2 built entirely from parts made in machine shop #1.
- Telegraph → radio → vacuum tubes → sensors, relays, regulators: infrastructure that holds its own set-points.
- The medicine program (microscopes, aseptic technique, antibiotics, surgery) — valuable and real, but *not* a race against mortality per canon.
- **The tablet gets ports:** the first player-built devices it can physically connect to (computer, HUD, instrumentation).

**Gate (provisional).** Machine shop #2 assembled from shop-#1 parts, verified structurally (no part instantiated outside the production chain). A dam or furnace holds its set-point through an owner-injected disturbance **with the tablet powered off** — the Doctrine of the Deaf Machine, enforced as a test.

---

## Phase 7 — Computation and the Hands *(Ages VII–VIII)*

- The full computation stack built for real: semiconductors from the chemistry lab, purification, lithography, boards, microcontrollers, PLCs; 1970s-class computing is the explicit target, not modern silicon.
- Automation generations: CNC retrofits → gantry robots and material handling → mobile platforms (rail-guided, then free-roving).
- Command architecture: dumb reliable local controllers → site computers → a planning layer where founder and oracle sit over the map.

**Gate (provisional).** **The reproduction metric crosses 100%:** a complete machine mined, smelted, machined, assembled, and programmed by existing systems while the player merely watches — measured by an in-game ledger of "percentage of parts made without human hands", which the engine computes from production provenance and cannot be asserted by hand.

---

## Phase 8 — The Web of Nodes *(Age IX)*

- Expansion by *replicating the pattern*: standardised self-sufficient nodes at strategic resources, rail links, coastal shipping, eventually nodes founded by machines that arrive, unpack, and raise power before the founder ever visits.
- Energy at continental scale: hydro, machine-built solar, fission; Type I defined as commanding a planet's energy budget.
- The constitutional question the exponential forces: how much standing autonomy do nodes receive? (Design question, deliberately open.)

**Gate (provisional).** A node founded and brought to self-sufficiency without the player present, with the Kardashev ledger reading a defensible planetary-fraction figure derived from simulated energy flows.

---

## Dependency rationale

Real Earth is Phases 1–3 because it is the identity **and** the highest-inertia engine risk: every later phase pins content to real coordinates, so migrating later would invalidate content wholesale. It is uniquely cheap now — `SurfaceRadius(dir)` is a single seam and almost nothing else yet assumes float world-space. §45's warning against "beginning with the whole Earth" is honoured by slicing the planet along its strata (shape → scale → detail → climate → water → life → sky → resources), interleaved with content phases so the project stays a game throughout. Precision migration (1.2) sits after 1.1 so its gate verifies against reality, and before all content for the inertia reason. Embodiment (Phase 2) precedes world systems (Phase 3) so the project is playable early. Sim LOD waits for Phase 5 per §46 — aggregation earns its place exactly when machinery first runs unattended, not a phase before.

## Cross-cutting tracks

- **Validation suite** grows monotonically; sim tests on plain .NET in CI, Unity-side in batch mode. Loosening a test to pass a gate is a gate failure.
- **Contracts and domain specs** — one binding `Docs/SLICEn_*.md` per slice before code; `GAME_DESIGN.md` stays the constitution; `ARCHITECTURE.md` is progressively superseded by a living engine spec updated at each gate.
- **Infrastructure earned per phase:** git/CI (0), data pipeline + performance budgets (1 onward, in every gate), save/load (2), real UI (4).
- **Determinism discipline (§34):** bit-identical seed reproduction, no runtime LLM, actual-vs-measured split (§14), and causal provenance (§23) are standing requirements in every new domain. The Doctrine of the Deaf Machine extends this into the fiction.

## Re-planning cadence

At every phase gate: owner playtest debrief → next phase's slices detailed into binding contracts → this roadmap amended and re-committed. Mid-phase slice re-scoping is allowed; gate criteria change only at a gate review (rule 5). Mandatory deep review after the Phase 4 gate before committing Phase 5+ detail; Phases 6–8 stay coarse until then. Emergency full revisit: any gate failed twice, or a phase running ~2× its planned span.
