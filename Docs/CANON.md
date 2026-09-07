# Canon — the owner's rulings, dated

This file holds what the owner (William) has ruled, in his words where they were recorded verbatim, with the date
and the source. Nothing here is an agent's decision; agent decisions live in `ARCHITECTURE.md` and `STANDARDS.md`
and say so. A ruling is amended only by a later dated entry, never by editing the old one. Date the ruling, never
the state.

The constitution is `GAME_DESIGN.md` (owner-authored 2026-08-24, carried verbatim from v1 with its § numbering,
which every other document cites). Where this file and the constitution appear to conflict, the constitution wins
on mechanism and the later dated ruling wins on scope.

## Inherited from v1 (verbatim or as recorded, with the v1 source under `Docs/v1/`)

| Date | Ruling | Source |
|---|---|---|
| 2026-08-25 | The founder is **ageless**: no mortality clock, no capability-vs-decline race; medicine is for injury and disease. Canonically a 20–30-year-old adult. | `ROADMAP.md` canon block |
| 2026-08-25 | The **tablet** is a physical object the founder wakes holding: unbreakable, never dies, the oracle that cannot act. It is the §5 knowledge system, §34-compliant (no LLM at runtime). Later it can be physically connected to things the player builds. **Doctrine of the Deaf Machine:** every autonomous system the player builds must function with the tablet absent. | `ROADMAP.md` |
| 2026-08-25 | **Megafauna are canon**: diprotodon, marsupial lion and the rest of the unextinct Pleistocene fauna. | `ROADMAP.md` |
| 2026-08-25 | "i want to design a real ecosystem and let it form naturally." Rock → landform → water → soil → plants → animals is one chain of consequences; **nothing in it is placed.** | `ECOSYSTEM.md` |
| 2026-08-25 | "since it is realistically impossible to accurately map the whole world, use procedural generation that follows earth, and what is known to be in certain regions." Real data is the skeleton; procedural detail lives below its resolution. | `SLICE1_3_EARTH_FOLLOWING_TERRAIN.md` |
| 2026-08-25 | Every gate has an **external referent**; the owner picks the probe at gate time; a transfer test passable by real-world knowledge; an automated floor and an owner playtest ceiling; binary, pre-written criteria (a failed gate reopens the slice, never the criteria); no fudge inputs. | `ROADMAP.md` gate rules |
| 2026-08-26 | "new milestone goal: become as playable as the beta versions of minecraft. only, translate the impact of the respective time each game is in." The bar: **a stranger can play it.** | `MILESTONE_PLAYABLE.md` |
| 2026-08-26 | **The verb rule:** what an action is comes from what you are looking at and what is in your hand, never from which key you memorised. Hold left mouse = work on it; right mouse = use it; Tab = carrying; wheel or 1–9 = what is in hand; WASD, Space, Shift, Escape. | `MILESTONE_PLAYABLE.md` |
| 2026-08-26 | "research real physics, thermodynamics etc to figure out the games physics." Every number from published work, not from tuning. | `PHYSICS_NAKED_HUMAN.md` |
| 2026-08-26 | "when can we work on the physics, they dont feel nearly realistic." Locomotion must be real (Tobler, Pandolf). | `SLICE2_7_LOCOMOTION.md` |
| 2026-09-01 | "the founder wakes with nothing, just the tablet. no clothes, no tools, no workshop, just the tablet." The country must offer first materials as a consequence, never as a spawn table. | `FOUNDERS_PATH.md` |
| 2026-09-01 | "the beta arc isnt about how long the player survives, its about when they get to a shelter with something that resembles a bed, a fire and some tools. the core reason for the beta arc is a proof of concept of the game. i can show another developer what this game is, even though its not finished, and they can get a good idea of what i am trying to build." | `FOUNDERS_PATH.md` |
| 2026-09-01 | The climate is **pre-human**: "if the player is going to be seeing megafauna roaming around, [the modern climate] wouldn't make sense." Station record minus the cited regional anthropogenic warming signal; the station's diurnal and seasonal shape stays binding. | `CONTRACT_W1_WEATHER.md` |
| 2026-09-01 | Two **death modes** chosen at new game: Standard (death returns the founder to the wake beach, the world persists, what was carried lies where they fell) and Hardcore (one life). | `FOUNDERS_PATH.md` |
| 2026-09-01 | "Approved — make it binding": `FOUNDERS_PATH.md` is the signed beta arc. | `FOUNDERS_PATH.md` |
| 2026-09-01 | No session opens a visible game window; a window exists only when the owner asks to watch. Automated instances are muted and isolated. | v1 `CLAUDE.md`, sessions record |
| 2026-09-02 | "i want to finish the headless machine, and retire the current substrate of 15 sessions built for a human that is surrounded by overengineered not-needed fluff code." | `CONTRACT_HM2_CUTOVER.md` |
| 2026-09-03 | "Low poly is the beta's look, not the game's." | `BETA_PRODUCTION_PLAN.md` §8 |
| 2026-09-03 | There is no earth below the surface (a heightfield skin); fix order: deformation with the soil profile's colours, water at the water table, a true volume only if later needed. Phase 3. | `BETA_PRODUCTION_PLAN.md` §8 |

Rules v1 treated as canon that were **agent-authored and never ratified** (the "animals present, not hunted"
reconciliation, the M0–M9 production plan, the asymmetry doctrine, auto-push, the two-key gate, the test floors)
are not inherited as canon. The good ones return in `STANDARDS.md` as v2's own dated engineering choices.

## Rulings of 2026-09-07 (this project's founding)

1. **The engine is a custom library.** A game-specific simulation engine as a standalone C# library with no Unity
   dependency, consumed by the Unity client and by the server. Not a custom renderer. ("I also kinda meant a
   custom library.")
2. **What was wrong with v1:** the look and graphics; controls, feel and verbs; the world's scale and flatness.
3. **Multiplayer:** "the world is solo, but you can do it duo, or host servers with friends, similarly to how
   minecraft servers are hosted." Minecraft-scale servers planned, smaller for the first milestone.
4. **Assets:** standard Unity workflow (scenes, prefabs, materials, ScriptableObjects, imported meshes where they
   help); generated geometry where physics dictates the shape.
5. **World shape:** a bounded flat region anchored to real Earth, 8 × 8 km first.
6. **Steam:** undecided. Direct IP or VPN now; a Steam transport must slot in later without a rewrite.
7. **Setup:** install the .NET 10 SDK; create the private GitHub repository `EarthGame2`.
8. **Wake site:** delegated ("up to you to decide, maybe its something new altogether. something suitable").
   Decided by research against the owner's criteria: the southern shore of Jervis Bay, Bherwerre Peninsula, NSW.
   Region centre 35.140°S 150.675°E; wake point in the swale behind the dune at Cave Beach, about 35.159°S
   150.6485°E, to be snapped to the 1 m DEM. Known weaknesses are in `DEBTS.md` (thin iron, no middens on a
   pre-human Earth, hand-pulled station normals). Fallback: Ulladulla / Burrill Lake. Naming uses landform names
   (Bherwerre, Steamers Head, Cape St George) and states the pre-human framing plainly. Region id `bherwerre`.
9. **Process and first milestone:** delegated ("thats for you to decide, youre partly my teacher"). Decided:
   lightweight process, no multi-agent machine; first milestone "First Light", the living world first.
10. **Languages and learning:** the owner is learning programming and programs parts of v2; his lane is the
    verifiers (ruling 16).

### Constitutional amendments (dated, never silent)

- **§45 (2026-09-07):** the bolt-factory manufacturing slice is deferred; v1 proved it, and v2's first slice is
  the walkable living world. The thesis returns when the manufacturing era is contracted.
- **§2 "Earth's size" (2026-09-07):** the first world is a bounded 8 km region on a tangent plane anchored at a
  real place, a §47 simplification (about 2.5 m of curvature sag at the corners, accepted), not a §2
  contradiction. More and larger regions later; the frame stays a real place.
- **The tablet's page (2026-09-07):** the tablet remains a physical object the founder holds; its page is drawn as
  screen-space UI with the slab as a diegetic prop, because v1's page painted onto tilted glass consumed eight
  visual iterations and never read.

### Plan review rulings (2026-09-07, "Approved with revisions")

11. **M1.B "healthy" is numeric.** The week-3 netcode checkpoint is decided against written pass/fail criteria
    N1–N4 (join-in-progress time-to-interactive under stated loss and latency; corrections per player-minute on
    the scripted walk; disconnect and rejoin without a server restart; a two-client 30-minute soak) and nothing
    else. If they fail and cannot pass within one further contracted week, plan B is taken. "Sunk cost doesn't
    vote." Ratified 2026-09-07 with three amendments: N2's divergence segment includes the water's edge (wading is
    the third place the two collision models disagree); N3 gains a live-tick variant (the world keeps simulating
    through the cut; the digest-equality row is dropped, the other three apply); N4's entity-count row reads
    "equal in ≥ 99% of one-second samples, never diverging for more than 2 consecutive seconds". Send caps stand
    as harness conditions regardless of the owner's link; he measures his real upload before the week-3 gate and
    a row is added only if it lands under 5 Mbit/s. The full criteria and their justifications are in
    `ARCHITECTURE.md`.
12. **The mover-feel decision is the owner's**, made by playing the build, hands on the controls, not from frames.
    "Running is not looking, and feel is not frames."
13. **Collision divergence at cliff edges, rock platforms and the water's edge** is a named defect class; the walk
    scenario carries a segment there and movement validation carries a correction budget on it.
14. **Dated weeks end at week 3 by design.** M1.4–M1.10 are months, not weeks; a slice gets a date only when its
    contract is written; week 1–3 velocity sets no expectations for the rest.
15. **Standards are rulings, not a conductor:** every standard carries a date and is demotable by the same
    process; when its enforcement cost exceeds its failure story, it is demoted with a dated entry, never
    silently ignored.
16. **The verifier lane is structural:** the agent writes the tools; the owner verifies them. A gate that depends
    on an owner verifier fails loudly when the verifier does not exist, never silently skipped, never written by
    the agent to unblock the gate. If the lane stalls, the agent surfaces the stall and waits or renegotiates the
    contract. "That separation is the fix for the fabricated verdict, and it dies the moment you author both
    sides." Every run-log format the owner's checkers read (`run.jsonl`, the join and soak logs) is a contracted,
    versioned format recorded in `ARCHITECTURE.md`; a schema change is a renegotiation in writing, not a refactor.
