# BETA PRODUCTION PLAN — the path to the finished beta arc

**Status:** proposed by FABLE on 2026-09-03 at the owner's ask ("detail the rest of the path up
to the finished beta arc; survey everything; reference how real triple-A development is carried
out; think like you have a full dev team"). Binding when the owner signs it. It supersedes the
order-of-work in `MILESTONE_PLAYABLE.md` and the beta build list in `FOUNDERS_PATH.md` §build
list as the schedule; it changes no rule in either.

---

## 0. What "finished beta arc" means, in a studio's words

Studios name their milestones by what a build can prove, and the names carry weight because
every discipline plans against them:

| Studio milestone | What it proves | Earth Game's equivalent |
|---|---|---|
| **Prototype** | the core mechanic is fun in isolation | done in 2026-08 (bolt factory, planet walker) |
| **First Playable / Vertical Slice** | one slice of the final game, at final quality, start to finish | **THIS is the "finished beta arc"** |
| **Alpha** (feature complete) | every feature exists, in some state | the Sim is here; the game is not |
| **Beta** (content complete, lock) | nothing new; only fixing and polish | after the arc |
| **Release candidate → Gold** | ships | beyond the beta |

The document the owner signed calls the arc "the beta"; in studio terms it is a **vertical
slice**: Act I and Act II of `FOUNDERS_PATH.md`, playable by a stranger with no documentation,
at a quality that shows the whole game's promise. The Phase 2 gate in `ROADMAP.md` is that
slice's acceptance test (a bushcraft-knowledgeable stranger wakes naked at the wake point and
survives night one). Naming it right matters because a vertical slice has one law that alpha
does not: **everything in it must be at final quality**, or it proves nothing. That is exactly
what the feel review of 2026-09-03 found missing.

## 1. The survey — where everything stands on 2026-09-03

**The tree.** 473 commits since 2026-08-25. Sim 84 files / 17,205 lines; Scripts 112 / 36,361;
Tests 78 / 21,109; machine (CI) 45 / 16,575; Docs 97 / 32,871. Suites: Sim 693 green (floor),
board 419, changelog 8, playback 6. Scripts compile clean under the PC gate; the player builds
headless in two minutes.

**The simulation (Sim, engine-free, the constitution's causal core).** Complete for the arc:
thermal physiology validated against literature, friction fire as a heat balance, the fuel-
moisture timelag model wetting and drying every holder through one census and one stepper
(W1d steps 1, 2, 3, 5, 6 landed), food that must be roasted and pounded, knapping and cordage
from material properties, a landscape eroded by its own history, nine plant species contesting
sites, four animal species with capacity, presence, rhythm and tracks, the oracle assessing
what can be attempted and why not, the goal planner recognising a camp and writing the dawn
summary. **Nothing in the Sim blocks the arc.** Remaining Sim items are polish or post-beta.

**The game (Scripts, Unity).** Every system shipped with its own UI and nobody unified them
(`MILESTONE_PLAYABLE.md`, 2026-08-31, still true). The state of the ten beta rows:

| Row (FOUNDERS_PATH build list) | State | Discipline |
|---|---|---|
| B1 forecast and night unified | done | Sim |
| First materials in the country | done | Sim + Scripts |
| Procedural sound layer | built, unjudged by ears | Audio |
| Placeables (windbreak, bedding, fire ring, drying rack) | built, reviewed, unplayed | Gameplay |
| Objective surface, camp recognition, dawn summary | built, unplayed | Sim + UI |
| Weather v1 | thermal side done; the world wetting visibly (W1d steps 4, 7–11) not built | Gameplay + VFX |
| Ambient animals | Sim done; on screen only as birdsong | Gameplay + Art |
| Death modes (Standard, Hardcore) | not built | Gameplay + UI |
| Stranger-shippable shell | crash safety done; first-run, menus, settings, performance not | UX + Engineering |
| Feedback pass (flakes fall, bark strips) | not built | Animation + VFX |

**The feel (from the owner's screen, `FEEL_REVIEW_2026-09-03.md`).** Terrain-demo world (no
sun, flat sky, hard horizon), confetti rocks, speck trees, an unreadable tablet slab, no body
in the frame, HUD as developer text blocks, "Digging… 17%" with nothing moving. The playback
of the owner's first recorded session: 9% standing, 6% walking, five seconds of work in
twenty-eight minutes; an offer never taken; ninety seconds of dead time; the crosshair
flickering four times in nine seconds, four separate times. These are the vertical slice's
real blockers. None of them is a system; all of them are presentation, feedback and pacing.

**Known player-facing defects (handoff B-list, 2026-08-31).** B2 the icon light leak; B3 the
sea does not darken at night; B4 erosion never receives rock hardness. On 2026-09-03: B4 closed
(1602e3e); B2 turned out to be the whole flat look (the icon key light was the world's main
light, so no shadow ever drew and dawn was lit like noon; `Docs/FEEL_REVIEW_2026-09-03.md`,
addendum 20:05) and is fixed on track-dev; B3 is queued to the dev seat.

**The machine.** Three seats on the VPS (a Sim dev seat, a review seat, a night watch), the
two-key gate inside it, a changelog the push refuses to let drift, a recorder in the game and a
playback reader beside it. Spend today: about USD 15 for 20 invocations. **The machine has no
seat that can touch Unity**: the PC seat cannot read the VPS board, so every Scripts row above
has no worker. This is the schedule's critical path and the first milestone below.

**Verification means that exist.** The Sim suite (headless, 693); the Scripts compile gate; the
scenario seam (`-eg-scenario`, a vocabulary of real button presses: press, hold, walk, look,
give, expect…) with a corpus under `CI/board/tests/`; the sandbox launcher (SBX1: muted, capped,
own logs, no window); the recorder (one telemetry row a second, a shot per event, F10 marks)
and `CI/watch/playback.py`; the PC gate; the review seat. What does not exist: any eyes on the
build other than the owner's and FABLE's reading of frames.

## 2. The team, as a studio would staff it, and who holds each chair here

A vertical slice at a large studio is built by a strike team of 15–40 across disciplines. We
have the same disciplines and fewer bodies; each chair below names its holder and its law.

| Discipline | Studio role | Here | Law that binds the chair |
|---|---|---|---|
| Production | producer, milestones, triage, the build | **FABLE** | CLAUDE.md; STANDARDS §10 |
| Creative direction | the director signs quality | **the owner** (veto at all times) | GAME_DESIGN.md |
| Simulation engineering | systems programmers | **HEADLESS-SIM** (VPS) | CONTRACT_HEADLESS_SIM.md |
| Gameplay and UX engineering | gameplay programmers, UI | **HEADLESS-DEV** (PC, after M0) | CONTRACT_HEADLESS_DEV.md |
| Rendering and technical art | lighting, shaders, environment | **GFX** contract on the PC seat | the recorder-frames law |
| Audio | sound designer | **SOUND** contract on the PC seat | audio verification captures, never broadcasts |
| Animation and VFX | feedback on the thing | the PC seat, under GFX | frames at 1920×1080 and 1280×720 |
| QA | test leads, playtest coordinators | the scenario corpus, the sandbox soak, the review seat | STANDARDS §8; the two-key gate |
| Code review | senior engineers | **REVIEW-SIM** (VPS), **REVIEW-DEV** (PC, after M0) | CONTRACT_REVIEW_SIM.md |
| User research | playtest lab | **the owner** (recorded, F10), a friend, the stranger | the playtest protocol, §5 |
| Live ops of the studio itself | IT, build engineers | the conductor, the night watch, the keeper | CONTRACT_HM1/HM3 |

What a studio has that we do not, and what we do instead: **an art department** (we are
procedural by design; the art pass is lighting, palette, silhouette, scatter rules and shader
work, not assets); **mocap and animation teams** (hands and work feedback are procedural
motion: sway, reach, particles); **a cert/compliance team** (none needed for a private beta);
**localisation, marketing, live services** (out of scope).

## 3. The milestones — the path, with exit criteria a build can prove

Each milestone is a studio-style gate: it names its exit criteria up front, the verification
that proves them, the chairs that do the work, and its size in machine-days (one seat working
unattended) and owner-hours. Milestones overlap where their disciplines differ; the order is
the critical path.

### M0 — The PC seat reads the board (machine; 2–3 machine-days)
The one item that unblocks every Scripts row. The PC conductor's seats run the wrapper against
the VPS board over SSH: every `board.py` verb the wrapper issues (feed, claim, lock, post,
ack, verdict) executes on the VPS, the worktree stays on the PC, recorder frames stay on the PC
as evidence FABLE reads. Roster on the PC: `HEADLESS-DEV | earth-game-headless | track-dev`,
`REVIEW-DEV | earth-game-review-dev | track-dev | read-only`. SBX1 sandbox law applies to every
verification launch.
**Exit:** a PC dev item claimed from the VPS board, actioned, its commit verified, reviewed by
REVIEW-DEV, merged by FABLE; D1–D4 re-run for the PC pair.

### M1 — First Light (rendering and technical art; 4–6 machine-days, 1 owner-hour)
Sun disc and sky gradient by hour; warm low light at dawn and dusk; distance haze; the sea
darkening at night (B3); terrain palette by soil and wetness (the Sim already knows both);
rocks clustered along the geology's contacts and creek lines, sized by the stone's fracture,
with contact shadows and far fewer in the open; canopy silhouettes and shadows at the plant
model's density; fog and cloud from the weather the Sim already runs.
**Status 2026-09-04 07:50:** landed on track-dev and judged in frames (`Docs/frames/`): one sun
(the icon key light was the world's main light; B2), shadows drawn; haze by Beer-Lambert from the
Sim's humidity; the sea lit by the light on it (B3); the eye's adaptation with a shoulder (twilight
legible, pre-dawn honest); the menus sized by content with a way back on every page (M6 S1); the
crosshair hysteresis (M3 C1). Open: full-daylight gain exactly 1.00 (F7d); rocks, canopy
silhouettes, the sand surface, the shore. Merge into main waits on F7d's PASS.

**Exit:** dawn, noon, dusk and rain frames at both resolutions judged by the owner; median
frame time ≤ 8 ms at 1080p from the recorder; the erosion and plant censuses unchanged (no
rule moved). Definition of done for every item: recorder frames attached to the review.
**Owed inside M1, opened 2026-09-03 by the haze work:** `Climate.CloudCover01` — the pair of
sines the W1 contract retired — lost its last game-path reader when `FlatSky` moved onto
`Survival.CloudCover01`, and now has no caller but `ThermalPhysicsTests`. Delete it and move
those cases onto `Weather`, in a commit that runs the Sim suite. Owner: HEADLESS-DEV, on
FABLE's word.

### M2 — The Tablet as a Page (UI and UX; 3–4 machine-days, 1 owner-hour)
The tablet renders as a readable page: one column, large type, light on dark, the page edge,
text facing the camera whatever the slab's tilt; the forecast, the body line, "what you can
attempt and why not", the fauna reading (Sim done), the dawn summary; two taps to any "why".
The HUD's developer text blocks become the tablet's business or disappear.
**Exit:** every tablet text legible in a 1280×720 frame; a scripted scenario reads each page;
the owner finds "why can't I drink the sea" in two taps without being told where.

### M3 — The Body and the Work (feel and feedback; 6–8 machine-days, 2 owner-hours)
Hands at rest and at work; the held item visible; the crosshair verb line stable (hysteresis
so an offer does not flicker at a boundary) and always naming what the button will do; work
that shows on the thing (flakes fall, bark comes away in strips, the digging stick moves earth,
debris lands where placed); footsteps and breath in step with motion; the six-keys-and-a-mouse
rule verified by the controls scenario.
**Exit:** playback of a ten-minute owner session shows flicker 0, dead time under 10%, every
offer either taken or explained; frames of each work verb in motion; the controls scenario
green.

### M4 — The World Wets, Visibly (gameplay and VFX; 4–5 machine-days)
W1d's Scripts half: the armful as one item (step 4), ground litter read from the Sim's patch
wetness (step 6's reader), the dropped-object registry and capture (7–8), the moisture
overwrite deleted (9–10), Survival building the census (11); rain that can be seen and heard;
wet surfaces; a drying rack that visibly dries.
**Status 2026-09-04 09:35:** W1d's Scripts half is landing one contract step per commit through
the VPS pair: steps 4, 6, 7-8 merged to main (976b551, 00a3bdc); 9-10 and the BeachSelfTest fix
passed on track-dev; the `weather` scenario exists (W2) and PASSES 21/21 on the PC against that
tree (`Docs/frames/weather-2026-09-04`); one doc-comment rule failure (W2b) holds the merge.

**Exit:** the `weather` scenario (new game, never saved, wet fibre, six dry hours, tinder found)
passes; the disconnect sabotage reds; frames of rain and of a dripping rack.

### M5 — Animals Present (gameplay and art; 3–4 machine-days)
The E5 binding: birds working the tideline, a wallaby at dawn on the plain, tracks by the
creek as decals, the fauna page bound to the live site; the Scripts side sets Situation's site,
hour and seed. Present, not hunted.
**Status 2026-09-04 10:12:** the E5 binding is on track-dev in three commits (the binding,
the drawing, the rider): the rider's capacity on the wake region is measured and pinned
(e64099d PASS); the binding and the drawing are withheld on two real bugs (tracks aged at
Greenwich hours; a retarget that keeps last bucket's species) with fix items posted. First frames:
a body on the dawn plain and specks on the tideline (`Docs/frames/fauna-track-dev-d10288b`); a
fauna scenario that walks to the nearest group is owed before the count check can run.

**Exit:** sightings in frames match the Sim's expected counts within the E5 rider's tolerance;
the fauna page reads the same numbers the world draws.

### M6 — The Shell (UX and engineering; 3–4 machine-days, 1 owner-hour)
First-run flow with no tutorial (the tablet, diegetically, as the thing that explains); menus
and settings that read at every resolution; Standard and Hardcore death modes (respawn at the
wake beach, the world persisting, what you carried lying where you fell); save slots; a
performance pass; crash safety soaked.
**Status 2026-09-04 17:08:** the menus are sized by content with a way back on every page (S1, on
main) and photographed by the machine (S2, the shell scenario, 37/37 on the PC; frames under
`Docs/frames/shell-track-ui-eb1f5c6`); the death modes exist on track-dev (the mode as a save fact,
Standard and Hardcore behaviour, a death scenario at 20/23 with two faults in fix). First-run flow,
the crash soak and the performance pass not started.

**Exit:** a two-hour sandbox soak with zero exceptions; a stranger installs and reaches the
beach without help; a death in each mode explained on screen.

### M7 — Content Complete: the arc runs end to end (QA; 2–3 machine-days)
A full-arc scenario (wake, drink, stone, cord, forecast, fire, night, camp, rain, the dawn
summary) in the corpus, run nightly in the sandbox by the machine, with frames; the Q1 corpus
grown to every beat of Acts I and II; B2 and B4 closed.
**Exit:** the arc scenario passes nightly for a week; the night watch posts its frames.

### M8 — Playtests (user research; 3 rounds over 2 weeks, 6 owner-hours)
Round 1: the owner, recorded, F10 on, no notes needed. Round 2: a friend, the showcase script,
the owner watching. Round 3: the Phase 2 gate itself — the bushcraft-knowledgeable stranger,
no documentation, 48 in-game hours. Each round produces a playback transcript, a feel review
and a fix list; the fix list is worked before the next round.
**Exit:** the stranger survives night one and builds a camp by day five; every playback signal
is explained or fixed.

### M9 — Beta lock, polish, release candidate (all; 1 week)
Feature and content lock; bug triage by severity (A: blocks the arc or loses data; B: breaks
a beat; C: cosmetic) with zero A and zero B at exit; the "juice" pass on what the playtests
loved; the changelog as release notes; the build handed over with `Docs/CHANGELOG.md`.
**Exit:** the owner says "ship it" to a private list.

**Total:** about 30–37 machine-days across two dev seats working in parallel (Sim polish on
the VPS, Scripts on the PC), 12–15 owner-hours, 5–6 calendar weeks with the machine running
nights. The critical path is M0 → M3 → M8; M1, M2, M4, M5, M6 can run in parallel once M0
lands.

## 4. Process

**2026-09-04, the owner's go:** the second machine's first full day is the evidence for a third
(`Docs/CONTRACT_HM5_THE_THIRD_MACHINE.md`): claims on the record, track branches on a remote,
items with a proof, a witness that builds and records every visual commit, findings that become
items, trains that run themselves, pause that kills nothing, a watchdog without a model. Built by
the idle Sim seat while the dev pairs continue.
 — what studios do, and how each maps onto the machine

| Studio practice | Purpose | Here |
|---|---|---|
| **Daily build** | the game always runs | Unity batch build nightly on the PC; a sandbox soak; the report on the board |
| **Vertical slice quality bar** | nothing placeholder in the slice | the recorder-frames law: no Scripts item lands without frames judged |
| **Telemetry** | know what players do | the recorder (one row a second) and playback's signals |
| **Playtest lab** | see the feeling, not the opinion | F10 marks, transcripts, feel reviews; three rounds (§3 M8) |
| **Code review** | two pairs of eyes | the two-key gate: REVIEW-SIM / REVIEW-DEV verdicts, FABLE merges |
| **Bug tracker and triage** | severity, ownership, age | the board: items with severity in the subject, aged by the night watch |
| **Feature freeze / content lock** | stop adding, start finishing | M9's lock; Phase 3 refused until the gate passes (D3 of the handoff) |
| **Definition of done** | no "done" without proof | STANDARDS §8 plus: frames for visuals, captures for audio, a scenario for verbs |
| **Milestone reviews** | the director signs | the owner signs each M's exit with his eyes; FABLE presents frames and numbers |
| **Risk register** | say what could sink it | §6 below, reviewed at each milestone |
| **Post-mortem** | learn per milestone | one review record per milestone in `Docs/reviews/` |

Two studio practices are deliberately inverted here. Studios verify with humans and automate
the rest; we automate first (the machine runs suites, scenarios, soaks and reviews every night)
and spend the human on the only thing it cannot do: feel. And studios lock the design early;
here the design is a constitution (`GAME_DESIGN.md`, `FOUNDERS_PATH.md`) already signed, so
the lock exists from day one and production is the act of reaching it.

## 5. The playtest protocol (the part no machine replaces)

1. The build is the nightly build; the recorder is on; the tester is told only "survive".
2. Nobody speaks. The tester presses F10 when something feels wrong; no words are asked for.
3. After the session, FABLE runs `playback.py`, reads the transcript and the sheets, and writes
   the feel review: every signal explained (design intent, defect, or missing feedback) and
   filed as items with a milestone.
4. The tester answers three questions afterwards, in writing: what were you trying to do when
   you were most confused; what surprised you; would you play again tomorrow. Nothing else.
5. The next round runs only after the fix list is landed and its frames judged.

## 6. Risks, in the order they would sink the slice

1. **No PC seat** (M0). Every Scripts row waits. Mitigation: M0 first, this week.
2. **The owner's time.** The slice needs 12–15 of his hours over five weeks, mostly looking at
   frames and playing. Mitigation: everything reaches him as frames and transcripts, never as
   code; sessions of fifteen minutes.
3. **Compile is not running.** The PC gate compiles Scripts; only the sandbox runs them.
   Mitigation: the nightly build and soak from M0 on; the arc scenario from M7.
4. **The flicker class of defect** (visible, unmeasured until today). Mitigation: playback's
   signals become exit criteria (M3).
5. **Scope pull from Phase 3.** Mitigation: D3 of the handoff stands; the changelog shows it.
6. **The machine's ceilings and spend.** ~USD 15/day today; two seats at pace ~USD 40/day.
   Mitigation: ceilings by cost (USD), reported each morning; the night watch.
7. **Placeholder art read as final.** The slice law: nothing placeholder. Mitigation: M1 before
   M8; the owner signs frames, not descriptions.

## 7. The first three days, concretely

- **Day 1 (2026-09-04):** M0 design and build begin (the board bridge over SSH; the PC
  roster; D1–D4 for the PC pair). Sim seat: W1d step 7's Sim half if any, else polish items
  from the feel review's Sim-side (moisture palette exposure, fauna page text). Owner: one
  fifteen-minute session with F10, on tonight's build.
- **Day 2:** M0 lands; first PC items posted: M1's sun and sky, M2's tablet page, M3's crosshair
  hysteresis (the flicker), in that order; REVIEW-DEV reviews with frames.
- **Day 3:** first frames judged by the owner; the feel review updated; the nightly build and
  soak running; the arc scenario drafted.

Every item this plan names is posted to the board with its milestone in the subject, its exit
criterion in the body, and its verification named. The board is the tracker; the changelog is
the record; the frames are the proof.

## 8. Acknowledged and placed, not in the slice

Debts the owner has named that the slice does not carry, each with its shape and its owner, so
they are on the record rather than in anyone's head.

- **There is no earth below the surface (owner, 2026-09-03).** The ground is a heightfield: a
  skin the founder walks on, with nothing under it. A shovel could not make a hole. The
  simulation, meanwhile, already knows what is down there: E3 computes soil depth from the
  production function against creep, `SurfaceGeology` holds the bedrock and its provinces, the
  water bodies know a water table. So the model of the ground as a column exists and the world
  does not draw or dig it. The honest shape of the fix, in order: (1) heightfield deformation
  with the soil profile's colours by depth, so a pit, a latrine, a fire pit and a dune dug into
  for a shelter are possible and look like the ground they are cut from; (2) water appearing
  at the water table when a hole reaches it; (3) a true volume (a signed-distance or voxel
  ground) only if the Phase 3 world systems need one. It is not in the slice because no beat of
  Acts I and II needs a hole and the slice law forbids a half-built one. **Owner: the terrain
  chair (GFX on the PC seat), Phase 3 — Living Earth, first slice.** Until then the design
  keeps digging as an act on a site (a tuber, a rhizome) rather than a change to the ground.
- **Low poly is the beta's look, not the game's (owner, 2026-09-03).** The owner names it as a
  large part of why the game feels incomplete; it is acceptable for the slice and not after.
  What "not low poly" means for a game that hand-authors no assets: a fidelity pass that the
  procedural pipeline generates - terrain mesh density and detail by distance, vegetation as
  real meshes with leaf and bark textures at the plant model's species and density, rock meshes
  shaped by the stone's fracture with normal maps, physically based materials on everything the
  founder can pick up, water with real refraction and foam, the sky and light already earned in
  M1. Nothing about the world's rules changes; every surface the player looks at does. It is the
  largest art investment in the project and the first item of the post-slice list, ahead of
  full E5 behaviour and hunting. **Owner: the rendering and technical-art chair, first slice
  after the beta.** Until then M1 lights the low-poly world properly, which will show how much
  of today's feeling is polygon count and how much is the absence of a sun.
