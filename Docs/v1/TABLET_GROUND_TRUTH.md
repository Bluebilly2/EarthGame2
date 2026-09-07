# The Tablet — ground truth for a design session

**Purpose: this is the input to a Fable design session the owner is opening to design the
tablet "incredibly well." It states what the tablet IS in this project — its doctrine, its
data model, its current surfaces, its hard constraints, and what is in flight — so the design is
made against reality and not against a guess. Written by FABLE, 2026-09-02, every code claim
verified at source that day (file:line given); claims marked (per X's item) come from a seat's
board post and were not independently re-read. Where this document does not know something,
it says so and names the question. The design session's OUTPUT is a design document, which
FABLE ratifies into `CONTRACT_T1_TABLET.md` for seat 15 to build after train #3.**

## 1. Doctrine — the four sentences the design cannot break

1. **The tablet is a physical oracle.** Canon: it is an object in the founder's hand, a slab in
   the world, foreshortened at rest and raised to read. It is not a HUD, not a menu, not a
   quest log. It reads; it does not narrate.
2. **It reads the same models the world runs. Never a second derivation of a rule.** (`CLAUDE.md`
   house rules; `STANDARDS.md`.) Everything the tablet says is the world's own arithmetic asked a
   question — the thermal model, the fire model, the camp assessment. A tablet page that
   computes its own version of "will I be warm" is the named bug shape and is forbidden.
3. **The Deaf Machine.** The world says nothing; it never prompts, hints, or scripts. The tablet
   is the one place the player *reads* the world's state, on their own initiative. FOUNDERS_PATH
   exists precisely because a "path" can exist in a game that forbids scripts only if it is
   *derived* from the models rather than authored — the tablet's objective surface reads the
   arc's own sentence and asks the models whether it is satisfied.
4. **Honesty over encouragement.** A camp that does not keep the founder warm is reported as
   not keeping them warm. A windbreak that does not break wind does not count. The tablet
   cannot flatter.

## 2. The data model — what the tablet is actually handed (verified at source)

All in `Assets/EarthGame/Sim/Knowledge/` — pure Sim, no Unity.

**`Oracle.cs`**
- `Requirement` (`:10-20`): four readonly fields — **`Need`** (string: what stands in the way,
  e.g. "something between you and the ground"), **`Met`** (bool), **`Reading`** (string: the
  measurement, e.g. "lying on bare ground at 20 W/m²K"), **`Because`** (string: the mechanism,
  e.g. "Earth conducts heat away far faster..."). `Because` is not drawn when `Met` is true —
  the model already ranks it lowest.
- `CapabilityReport` (`:30-36`): **`Goal`** (string), **`Method`** (string), and a list of
  `Requirement`s. Example pairing per GFX's item: "A night that does not kill you — Nothing
  built".
- `Situation` (`:91`): the founder's whole readable state — `Carried` (items), `Body`
  (`BodyState`), and the shelter/wind/bedding facts the models expose (e.g. the bedding depth
  under the founder, now ranging over both a built shelter and a placed bedding pile after
  P1c). **This is the tablet's single input.**
- `Oracle.Assess(Situation) → List<CapabilityReport>` (`:210`) and
  `Oracle.MostPressing(reports)` (`:225`): the tablet asks the oracle, and the oracle answers
  with capability reports; one is most pressing.
- Night memory: `LastBareNight` / `LastActualNight` (`HeatBalance`, `:724-727`) and
  `LastNightConditions` (`:738`) — what the last night cost, bare versus as-built, which is what
  B1 calibrated and what a dawn summary would read.

**`Camp.cs`** (`:31-43`): `Camp.Goal = "A camp of your own"` — FOUNDERS_PATH's own sentence —
and `Camp.Assess(Situation) → CapabilityReport`, the objective surface for Act II: four
conditions asked of the models (through `Situation`, never by tag), and `Camp.Ready` plus the
existing `Sleeping.WakeReason == Dawn` is the arc's end predicate (DEV 1, 1ce7e0d). The
finale is inert until P1c (placed components reaching `Situation`) merges — it has, on
track-placeables, and rides train #3.

**Four semantic ranks, one drawing (per GFX, board 163015):** the data carries four distinct
kinds of statement — goal/method, need (met/unmet), reading, mechanism — and the current page
concatenates all four into one string in one style. *The hierarchy is not something the tablet
needs invented; it is already in the model and the drawing discards it.* The number of levels
is four because the model says four.

## 3. The current surfaces — what exists on screen (verified at source)

`Assets/EarthGame/Scripts/Player/TabletScreen.cs` (paint — **GFX's partition**):
- Two modes: **reading** (the raised page, `PageText` `:766` → `DrawPage` `:584`) and
  **glance** (the at-rest slab, `GlanceText` → `DrawGlance` `:748`), chosen at `:410`.
- Styles `_title, _text, _glance, _tab, _measure` (`:97`); per GFX, `_title` is built and used
  only as a "styles ready" sentinel — a head style exists and nothing renders through it.
- `_glance.wordWrap` is true; `DrawGlance` insets to 84% of the page; overflow is not possible.
- A first-run controls hint fades after `HintFadeStart` = 30 s (a teaching aid, not content).
- Three files in the repo deliberately set `richText = false` (per GFX) — the obvious answer to
  hierarchy (inline markup) is arguable, not obvious.

`Assets/EarthGame/Scripts/Core/UiScale.cs`: `UiScale.Measure` and, since d764e68 (GFX, today),
`UiScale.ColumnChars` = **66** — the one owner of the line measure, read by both the HUD and
the tablet (the tablet ran ~125 characters per line before; a portrait slab is wide, and
"fitting the page is not the same as readable").

The C11 channel (`ActionDriver.Current`, GFX, b0045fe): the driver republishes every frame what
verb is in flight, on what, with progress and caption — the tablet could read this, and today
does not.

Readability law (G1, `CONTRACT_G1_READABILITY.md`): every visual change ships named
before/after recorder frames at 1920×1080 and 1280×720; the owner judges taste in batches.

## 4. Hard constraints the design inherits

- **Partition.** Paint (`TabletScreen.cs`, `ItemIcons`, draw paths) is GFX's. `Oracle.cs`,
  `Camp.cs`, `Situation` are claim-based Sim (currently DEV 2's and PLACEABLES' by claim). A
  tablet seat (seat 15) will be given: the tablet's **information architecture, pages, and
  oracle prose** — the content path from `Situation` to text — with GFX keeping the draw side.
  The design must be expressible in that split.
- **No scripts, no hand-authored progression.** `GAME_DESIGN.md` is the constitution. The tablet
  may show what the models say is next; it may not author a tutorial.
- **Save law.** Any new persisted fact is additive with an initialiser and enters
  `StateDigest.Of` in the same commit.
- **Every claim in a page is checkable against a model.** A sentence the models cannot back is
  a false confident comment, one level up.
- **The beta's purpose (owner-ruled, FOUNDERS_PATH §"What the beta is for"):** a proof of
  concept for another developer — the demo lens "what a developer must see." The tablet is
  most of what a developer *sees* of the simulation's depth.
- **Two resolutions**, 1920×1080 and 1280×720, both proven by frame capture.

## 5. In flight right now (do not design against yesterday's tablet)

- P1c (camp reads placed components) — passed, rides train #3.
- The arc-end / dawn recognition (DEV 1, 1ce7e0d) — passed on main; reachable after P1c merges.
- The four-rank hierarchy and the 66-character measure (GFX) — measure landed; hierarchy in
  design with four approaches under adversarial judging.
- W1c weather: items wet and dry live; **the bed does not yet** (REVIEWER's WITHHELD 163414,
  DEV 2 fixing) — a dawn summary that reports bed moisture is waiting on that.
- Tracks (E5c) ratified: evidence an animal passed, readable from the ground — a future page's
  content, not yet built.

## 6. What the design must answer (the questions, not the answers)

1. **Information architecture.** What are the pages? Today: one page, one glance. The data
   suggests at least: the pressing capability (goal → needs → readings → why), the camp's
   state, last night's accounting, the country (what the ground and the wind say). What is the
   *order* a founder needs them in, and does the tablet turn pages or does the founder?
2. **When the tablet speaks.** Glance versus reading: what belongs at a glance (raised for one
   second) and what only on the page? The Deaf Machine forbids the tablet interrupting — so what
   does it show *unasked*, and is that already too much?
3. **Hierarchy as typography.** Four ranks, no rich text: weight, size, indent, rule, whitespace
   — what makes goal/need/reading/why legible at 66 characters on a foreshortened slab at
   1280×720?
4. **The dawn summary.** FOUNDERS_PATH's beta list names "the dawn summary" beside the objective
   surface. What does the tablet say at dawn, from `LastActualNight` versus `LastBareNight`,
   that teaches a developer the model is real without narrating?
5. **Teaching without scripting.** How does a stranger learn what the tablet is *for* in the
   first minute, when nothing may tell them? (The 30-second controls hint is the only precedent
   and it is deliberately temporary.)
6. **The physical object.** What is it, in the fiction? Its weight, its light, its wear — does
   it read differently at night, in rain, with cold hands? (Constraint: everything visual is
   GFX's to draw; the design specifies, GFX renders.)
7. **What it must never become:** a quest log, a compass, a minimap, a crafting menu, a hint
   system. Name the lines.

## 7. Real-world research the session should do (the tablet has ancestors)

Field guides and naturalists' notebooks (how a reading is recorded beside its cause); almanacs
and tide tables (a page that is true every day without a narrator); instrument panels and
survey forms (what a measurement looks like next to its threshold); the best games' "reading
the world" surfaces — the ones that respected the player's inference (Outer Wilds' ship log,
Subnautica's PDA at its most restrained, Return of the Obra Dinn's book) and the ones that
collapsed into quest logs — and why. The session should come back with references, not just
taste.

## 8. Deliverable

A design document, contract-grade: the pages and their content model (what each says, from
which `Situation` fact, through which model), the glance/reading rule, the typographic
hierarchy specified for GFX to render, the dawn summary's exact sources, the "never becomes"
list, and acceptance criteria a headless test or a frame capture can prove. FABLE ratifies it
into `CONTRACT_T1_TABLET.md`; seat 15 builds under it after train #3; REVIEWER 2 holds its
verdicts; the owner judges the frames.

## 9. Open questions for FABLE (ask, do not assume)

- Exact `Situation` fields available today (a full list can be extracted on request).
- Whether `Oracle.Assess` returns more than one report in practice, and what orders them.
- What the glance currently shows, verbatim, at rest.
- Whether a "page turn" input exists or is a new verb (the verb seam is DEV 1's).
