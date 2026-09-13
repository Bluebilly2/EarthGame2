# Canon — what the owner has decided

This file records the decisions William has made about the game and the project: his words where they were
recorded, the date, and where the decision came from. Only his decisions go here. The agent's engineering
decisions live in `ARCHITECTURE.md` (its decision log) and `STANDARDS.md`, and say so; a question, a remark or an
agent's inference is not a ruling, however it is phrased.

The file is kept current. When a decision changes, its entry is rewritten to say what stands now, with a line
saying what it replaced and when; git holds every earlier wording. The numbers are stable names that code,
contracts and checks cite, so a withdrawn ruling keeps its number and says so.

The constitution is `GAME_DESIGN.md` (owner-authored 2026-08-24, carried from v1 with its § numbering, which
every other document cites). Where this file and the constitution appear to conflict, the constitution wins on
mechanism and the later ruling wins on scope.

## Carried from v1 (the v1 source is under `Docs/v1/` unless it names a file at `Docs/`)

| Date | Ruling | Source |
|---|---|---|
| 2026-08-25 | The founder is **ageless**: no mortality clock, no capability-vs-decline race; medicine is for injury and disease. Canonically a 20–30-year-old adult. | `ROADMAP.md` canon block |
| 2026-08-25 | The **tablet** is a physical object the founder wakes holding: unbreakable, never dies, the oracle that cannot act. It is the §5 knowledge system, §34-compliant (no LLM at runtime). Later it can be physically connected to things the player builds. **Doctrine of the Deaf Machine:** every autonomous system the player builds must function with the tablet absent. | `ROADMAP.md` |
| 2026-08-25 | **Megafauna are canon**: diprotodon, marsupial lion and the rest of the unextinct Pleistocene fauna. | `ROADMAP.md` |
| 2026-08-25 | "i want to design a real ecosystem and let it form naturally." Rock → landform → water → soil → plants → animals is one chain of consequences: what exists where follows from the country. How literally it is simulated is ruling 22. | `Docs/ECOSYSTEM.md` |
| 2026-08-25 | "since it is realistically impossible to accurately map the whole world, use procedural generation that follows earth, and what is known to be in certain regions." Real data is the skeleton; procedural detail lives below its resolution. | `SLICE1_3_EARTH_FOLLOWING_TERRAIN.md` |
| 2026-08-25 | Every gate has an **external referent**; the owner picks the probe at gate time; a transfer test passable by real-world knowledge; an automated floor and an owner playtest ceiling; binary, pre-written criteria (a failed gate reopens the slice, never the criteria); no fudge inputs. | `ROADMAP.md` gate rules |
| 2026-08-26 | "new milestone goal: become as playable as the beta versions of minecraft. only, translate the impact of the respective time each game is in." The bar: **a stranger can play it.** | `MILESTONE_PLAYABLE.md` |
| 2026-08-26 | **The verb rule:** what an action is comes from what you are looking at and what is in your hand, never from which key you memorised. Hold left mouse = work on it; right mouse = use it; Tab = carrying; wheel or 1–9 = what is in hand; WASD, Space, Shift, Escape. | `MILESTONE_PLAYABLE.md` |
| 2026-08-26 | "research real physics, thermodynamics etc to figure out the games physics." Every number from published work, not from tuning. | `PHYSICS_NAKED_HUMAN.md` |
| 2026-08-26 | "when can we work on the physics, they dont feel nearly realistic." Locomotion must be real (Tobler, Pandolf). | `SLICE2_7_LOCOMOTION.md` |
| 2026-09-01 | "the founder wakes with nothing, just the tablet. no clothes, no tools, no workshop, just the tablet." The country must offer first materials, following from what it is (ruling 22), never from a spawn table. | `Docs/FOUNDERS_PATH.md` |
| 2026-09-01 | "the beta arc isnt about how long the player survives, its about when they get to a shelter with something that resembles a bed, a fire and some tools. the core reason for the beta arc is a proof of concept of the game. i can show another developer what this game is, even though its not finished, and they can get a good idea of what i am trying to build." | `Docs/FOUNDERS_PATH.md` |
| 2026-09-01 | The climate is **pre-human**: "if the player is going to be seeing megafauna roaming around, [the modern climate] wouldn't make sense." Station record minus the cited regional anthropogenic warming signal; the station's diurnal and seasonal shape stays binding. | `CONTRACT_W1_WEATHER.md` |
| 2026-09-01 | Two **death modes** chosen at new game: Standard (death returns the founder to the wake, the world persists, what was carried lies where they fell) and Hardcore (one life). | `Docs/FOUNDERS_PATH.md` |
| 2026-09-01 | "Approved — make it binding": `FOUNDERS_PATH.md` is the signed beta arc. | `Docs/FOUNDERS_PATH.md` |
| 2026-09-01 | No session opens a visible game window; a window exists only when the owner asks to watch. Automated instances are muted and isolated. | v1 `CLAUDE.md`, sessions record |
| 2026-09-03 | "Low poly is the beta's look, not the game's." What is shown to be judged as the game's look is not low poly. | `BETA_PRODUCTION_PLAN.md` §8 |
| 2026-09-03 | There is no earth below the surface (the ground is a heightfield skin). The fix, in order: deformation with the soil profile's colours by depth, water at the water table, a true volume only if later needed. | `BETA_PRODUCTION_PLAN.md` §8 |

One v1 ruling was about v1's own process and does not apply here: "i want to finish the headless machine, and
retire the current substrate of 15 sessions built for a human that is surrounded by overengineered not-needed
fluff code" (2026-09-02, `CONTRACT_HM2_CUTOVER.md`). EarthGame2 has no multi-agent machine to finish (ruling 9).

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
7. **Setup:** install the .NET 10 SDK; create the private GitHub repository `EarthGame2`. Done.
8. **The region:** delegated ("up to you to decide, maybe its something new altogether. something suitable").
   Decided by research against the owner's criteria: the southern shore of Jervis Bay, Bherwerre Peninsula, NSW;
   region centre 35.140°S 150.675°E; region id `bherwerre`; fallback Ulladulla / Burrill Lake. Naming uses
   landform names (Bherwerre, Steamers Head, Cape St George) and states the pre-human framing plainly. The site's
   known weaknesses are in `DEBTS.md`. The wake point first chosen with it (35.159°S 150.6485°E, recorded as the
   swale behind Cave Beach) was withdrawn by ruling 20.
9. **Process and first milestone:** delegated ("thats for you to decide, youre partly my teacher"). Decided:
   lightweight process, no multi-agent machine; first milestone "First Light", the living world first.
10. **Learning:** the owner is learning programming. He first meant to program parts of v2 himself; on 2026-09-08
    he withdrew that (ruling 17), and the agent writes the code and the checks.

### Constitutional amendments

- **§45 (2026-09-07):** the bolt-factory manufacturing slice is deferred; v1 proved it, and v2's first slice is
  the walkable living world. The thesis returns when the manufacturing era is contracted.
- **§2 "Earth's size" (2026-09-07):** the first world is a bounded 8 km region on a tangent plane anchored at a
  real place, a §47 simplification (about 2.5 m of curvature sag at the corners, accepted), not a §2
  contradiction. More and larger regions later; the frame stays a real place.
- **The tablet's page (2026-09-07):** the tablet remains a physical object the founder holds; its page is drawn as
  screen-space UI with the slab as a diegetic prop, because v1's page painted onto tilted glass consumed eight
  visual iterations and never read.

### Plan review rulings (2026-09-07, "Approved with revisions")

11. **The M1.B netcode checkpoint is decided on numbers.** Written pass/fail criteria N1–N4 (join-in-progress
    time-to-interactive under stated loss and latency; corrections per player-minute on the scripted walk;
    disconnect and rejoin without a server restart; a two-client 30-minute soak), and nothing else; had they
    failed and not passed within one further contracted week, plan B would have been taken. "Sunk cost doesn't
    vote." Ratified with three amendments: N2's divergence segment includes the water's edge; N3 gains a
    live-tick variant (the world keeps simulating through the cut; the digest-equality row is dropped, the other
    three apply); N4's entity-count row reads "equal in ≥ 99% of one-second samples, never diverging for more
    than 2 consecutive seconds". Send caps stand as harness conditions regardless of the owner's link. The full
    criteria are in `ARCHITECTURE.md` §7.1. **Decided 2026-09-10: pass (ruling 19).**
12. **The mover-feel decision is the owner's**, made by playing the build, hands on the controls, not from frames.
    "Running is not looking, and feel is not frames." **Decided 2026-09-08 (ruling 18).**
13. **Collision divergence at cliff edges, rock platforms and the water's edge** is a named defect class; the walk
    scenario carries a segment there and movement validation carries a correction budget on it.
14. **Dated weeks end at week 3 by design.** M1.4–M1.10 are months, not weeks; a slice gets a date only when its
    contract is written; week 1–3 velocity sets no expectations for the rest.
15. **Standards are rulings, not a conductor:** every standard carries a date and is demotable by the same
    process; when its enforcement cost exceeds its failure story, it is demoted with a dated entry, never
    silently ignored.
16. **Contracted formats, and gates that fail loudly.** Every run-log format a check reads (`run.jsonl`, the join
    and soak logs) is a contracted, versioned format recorded in `ARCHITECTURE.md`; a schema change is a
    renegotiation in writing, not a refactor. A gate whose check is missing fails loudly, never silently skipped.
    As first ruled, 16 also made the checks the owner's to write, so that one party would not author both a tool
    and its check ("that separation is the fix for the fabricated verdict"); ruling 17 withdrew that clause.

### Rulings of 2026-09-08

17. **The agent writes the checks too.** The owner, handed the skeleton of `solar_check.py` to complete: "ive
    changed my mind, do it for me. overturn the ruling about me doing things to learn." Ruling 16's authorship
    clause and the plan's lanes in which the owner wrote checks to learn are withdrawn. The agent writes the
    verifiers under `Tools/verifiers/checks/`; a gate still blocks loudly on a missing one. What replaced the
    separation is the agent's own rule, STANDARDS 19: a check is independent of what it checks by method and
    prints both numbers, so the owner reads a comparison rather than a verdict.
18. **The mover feels good** (ruling 12's decision, made by the owner playing the fixed build at Cave Beach): "it
    feels good, continue." The mover's numbers as ported stand: Tobler's walking speed with the 1.3 travel-pace
    factor, gait multipliers 1.0 / 1.7 / 2.6, a 0.5 m jump, a 0.4 m step, 45° walkable, and the camera smoothed
    vertically only. They change when the owner, playing, says so (ruling 12).

### Rulings of 2026-09-10

19. **Keep the hand-rolled netcode** (ruling 11's decision, on the M1.B numbers): "keep yours, it sounds like it
    works." Plan B (FishNet) is closed; the transport, the protocol and the replication are the game's own.
20. **The founder can wake anywhere in the region.** Asked whether ruling 8's point or Cave Beach, 2.1 km east of
    it, was meant: "i dont actually care where the player wakes, somewhere in this region thats all." The point
    in ruling 8 is withdrawn; the world-creation pipeline's own choice of wake stands.
21. **Checking the world against the real place is the agent's.** "look/research the area, i dont really know too
    well." The census and the world's layers are checked against published sources by the agent, who names them
    and records where the world disagrees as a debt or a fix.
22. **The world starts in a generated state, and changes from there.** On the ecosystem ruling of 2026-08-25:
    "the idea that things are there because they should be is a guide for how it should be designed. dont need
    to literally simulate them coming into existence." And: "the player doesnt know what the world looked like
    before they load in for the first time, the world starts in a set state that is procedurally generated
    (there needs to be serious effort into the procedural generation of this game), then as they do things, as
    time passes, things change." So what exists where follows from the country — a stone lies where that stone
    crops out, a stick where that tree stands — but the history that put it there is not simulated. The first
    state is generated, with serious effort, from the facts the world holds; from then on it changes as time
    passes and as players act.
23. *Not a ruling; removed.* It was recorded on 2026-09-10 from a question the owner asked about the frames ("will
    it be like that for long?"), and removed the same day, because a question is not a decision. That the
    palette's judgement waits until things stand on the ground is noted in the M1.4d contract.

### Rulings of 2026-09-11

24. **The founder swims.** Told that a founder in water over their head walked its bed, and that whether a founder
    floats, swims or is held back was his to decide (DEBTS, "water over the head"): "swimming needs implementing, and
    when we have a player model and character, animation too." A founder swims in water too deep to stand in; when
    the founder has a body that is drawn, the swimming is animated.

### Rulings of 2026-09-12

25. **A developer's flight goes through the ground.** Told that the flight built for him was held five centimetres over
    the ground: "dev flight should be toggleable noclip." The fly key turns on a flight that passes through the ground
    as through the air, and turns it off again.

### Rulings of 2026-09-13

26. **GPT works on its own judgement.** Asked for a prompt to set GPT (through Codex) to work: "dont tell it what
    to do, tell it how to get started, making its own sovereign decisions in the projects favour." GPT is told how
    to get started, not what to do: what it works on, and how, is its own decision in the project's favour, and the
    house rules and this file bind it as they bind every agent.

27. **A fully functioning biosphere comes after the beta, under world generation.** "once we get past the beta arc,
    one of the things we will do is design a fully functioning biosphere (earth). this is one thing that will come
    under the world generation," and "make sure this idea of a full functioning biosphere is written down for later
    development." After the beta arc, a biosphere for the whole Earth is designed as part of world generation. The
    idea is written down in `BIOSPHERE.md`.
