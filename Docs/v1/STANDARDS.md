# STANDARDS.md — the laws of this repository

**Status: binding, alongside `GAME_DESIGN.md`, `ROADMAP.md`, `ECOSYSTEM.md`, `MILESTONE_PLAYABLE.md`.**
Owner: the FABLE session (architect). Changes to this file go through FABLE; every other session
follows it as written. These laws are *codified existing practice* — each one was learned here,
usually the hard way — so a law without its enforcement listed is a law waiting for one.

The table at the end says how each law is enforced. A law enforced by "review" is the weakest
kind; when a law is broken twice, its enforcement gets promoted to a test or a compile.

---

## 1. Layering law

- `Assets/EarthGame/Sim/**` never imports UnityEngine, directly or transitively. It is
  deterministic, engine-free C#, and it is where every rule about the world lives.
- `Assets/EarthGame/Scripts/**` may import Sim and UnityEngine. It renders, wires, and feeds the
  Sim; it never *re-derives* what the Sim already decides (see law 3).
- `Assets/EarthGame/Tests/**` imports Sim and NUnit only — the same files must compile in Unity's
  test runner and in `CI/Tests/EarthGame.Sim.Tests.Headless.csproj`.
- Editor-only code lives in `Assets/EarthGame/Editor/**`. Shaders in `Assets/EarthGame/Shaders/`.
- Language level is **C# 9** everywhere (Unity's level; the headless csprojs pin `LangVersion 9`).
  A newer language feature that compiles in one place and not the other is a build break.

## 2. Repository law

- One public type per file; the file bears the type's name. Small private helper types may live
  with their single user.
- Every asset has its `.meta`; a new file and its `.meta` land in the same commit.
- Temporary scripts, scratch output and experiment files never enter `Assets/` or `Docs/` — they
  go to the session scratchpad and die there.
- Docs layout: binding documents at `Docs/*.md` top level; slice contracts as `Docs/SLICE*.md`
  written **before** the code they govern; reviews under `Docs/reviews/`; the message board is
  `Docs/SESSIONS.md`. Debts are recorded in a Doc with an owner — **never as a TODO comment**
  (the repo has zero and keeps it that way).

## 3. Fact-ownership law (the named bug shape)

A fact about the world lives in exactly one place, and everything else reads it there.

- The recurring failure this repo produces is a fact in two places with nothing forcing
  agreement: a duplicated rule, a promise with no enforcement, a field written and never read, a
  claim measured against the wrong source. Treat every new constant, rule, and derivation as a
  potential instance.
- Finding a second copy obliges you to alias it or delete it **in the same commit**
  (`BodyState.ClearSkyDepressionK = Climate.ClearSkyDepressionK` is the alias pattern).
- The oracle/tablet may only *quote* the models the world runs. A "second opinion" computed in
  the Knowledge layer or the Scripts layer is the worst class of bug this game can have.
- A number quoted in player-facing prose is interpolated from the constant that owns it, never
  typed as a literal.

## 4. Determinism law

- Nothing in Sim reads wall-clock time, environment state, or an unseeded random source. The same
  inputs produce the same world, bit-for-bit, across runs and across a floating-origin rebase.
- Values near a quantisation edge are snapped before flooring (see `SurfaceGeology.ProvinceHash`);
  anything asked per-frame or per-object must be stable under recomputation.
- The digest comparison budget is twelve significant figures (`StateDigest.Precision`, §37).

## 5. Save law

- Save-format changes are **additive fields with initialisers** — never a version bump to fix
  what an initialiser fixes. The initialiser must reproduce the value every pre-existing save
  actually played with.
- Anything added to `ItemState`, `PlacedItem`, `GameState` or the body block appears in
  `StateDigest.Of` **in the same commit**.
- `SaveSystem` is the only code that writes slot files. Writes are atomic (`File.Replace`).
  Anything that writes a slot outside `Save`/`Delete` must invalidate the summary cache.

## 6. Input law

- Every key and mouse read the *game* makes goes through `Keys`, so a scenario can press it.
  Menus, dev tools and the recorder read `Input` directly, deliberately — `OwnershipTests`'
  source rules (`Input.*` and the wheel) hold that boundary, with `KeybindSourceTests` policing
  the registry side; do not move it in either direction without a ruling. (This law and the
  table below previously named `InputSeamTests` — a test that never existed. Nobody measured
  the claim until MAIN DEV did, 2026-09-01; law 9 applied to this file's own text.)
- A new verb or interactive surface ships with a scenario step or probe that can drive it
  (`Scenario` — press/tap/point and `expect`). "Reachable by a player" is proven by a script
  pressing the real buttons, not by argument.

## 7. UI law

- IMGUI (`OnGUI`) is the UI until Phase 4's scheduled reckoning. No uGUI, no prefabs, no scene
  objects — everything is built from code at runtime.
- `UiScale.BeginFrame()` is the first statement of every `OnGUI`, above every early return.
  Style caches compare `UiScale.Generation`, not "have I built these yet". New font sizes and
  spacing come off the `UiScale` ramp, not typed numbers.
- No file I/O and no per-frame allocation of textures/styles inside `OnGUI`; measure text with a
  non-wrapping style (`UiScale.Measure` refuses wrapping styles for a reason).
- One owner per input surface at any moment (the cursor, the wheel, Escape): before adding a
  reader, name who owns it in each game state, and write it down where the readers are.

## 8. Test law — what proof each change requires

| Change | Minimum proof before it is called done |
|---|---|
| Sim behaviour | headless NUnit test that fails without the change; suite green |
| Scripts behaviour | compiles (`CI/Scripts` csproj) **and** an in-player check: self-test, scenario, or owner eyes |
| A new verb / input path | a scenario drives it end-to-end |
| Visual change | recorder frame or owner eyes — reading the code is not proof |
| Claimed removal | a source-text test asserting the absence (`KeybindSourceTests` pattern) |
| Save-format change | digest covers it; persistence round-trip green |
| Physics constant / formula | validated against the published figure named in a comment or Doc |
| Player build freshness | `Assembly-CSharp.dll`'s mtime or the `CIBuild: built` line — NEVER the exe stub, which Unity copies once and never rewrites (it read Aug 25 through a dozen same-night rebuilds); `spawn-sandbox.cmd --check` answers authoritatively by asking the game to run its own refusal |

- **A test names the guarantee, not the mechanism** (DEV 1, 2026-09-01; the day's most
  cross-cutting lesson — it independently explained findings from four seats: a rule keyed on
  a variable's spelling died to an alias, a test asserted a wrong constant as its own
  specification, a staleness sweep reasoned about top-level folder names and missed the nested
  case, and an audio test asked "is this automated?" when the guarantee was "is this a
  sandbox?"). The screen: ask what the test would still catch after an innocent refactor — if
  the answer is nothing, it tested the mechanism, and the guarantee is unguarded.

- **The content/trigger gap** (SOUND DEV, 2026-09-01, found in its own rules after SHELL's
  mute hole showed the shape): a rule set can be complete on a mechanism's CONTENT — every
  clause pinned, every word of the body asserted — and silent on its TRIGGER, staying fully
  green while whole categories of run never reach the mechanism at all. The blind spot is
  structural, not careless: rules written by reading a function assert everything visible from
  inside it, and "who calls this, and when" is the one fact that is not. The screen is one
  question per rule: *for every rule about what a thing does, is there anything asserting that
  it runs?* Rider, from the fix's own sabotage, later generalized by its author (c09d441 —
  "the third costume of one error"): **region-true is not point-true.** A predicate true of a
  REGION — the attribute text anywhere in the file, any one call site satisfied, a body that
  is merely non-empty — is the cheap assertion to write and almost never the claim meant,
  which is about one POINT inside it. Anywhere a rule names a file, a set, or a range, ask
  whether the claim is actually about one place inside it; walk every site and require each,
  or pin the point itself.
- **An assertion that cannot tell code from prose is not an assertion** (BOARD DEV,
  2026-09-02, the third time in one session: a launcher's `shift` comment matched a no-shift
  rule, a REM line was parsed by cmd as batch syntax, and a discovery assertion matched its
  own explanatory comment). A source-text rule strips comments before matching, or matches on
  something that cannot appear in English; and a source-text rule about a gate is proven by
  running the gate against a deliberately red neighbour, because a pattern matching nothing
  is green forever.
- **A gating test asserts only what a pin can name** (REVIEWER's E10 ruling 2026-09-01,
  re-proven by SOUND 2026-09-02 with every pin held — head, suite-inputs, and the stat-pinned
  dataset — while the verdict flipped: five runs on one commit, a 40% spread, both verdicts
  inside twenty minutes, because six seats shared the CPU). Wall-clock and CPU time are inputs
  no pin can fix, so a gate never asserts them: assert the work done (iterations, cells,
  steps) and log the timing as diagnostics. A red a gate hands out for the machine's load
  belongs to nobody and teaches nothing.
- **A Sim test's fixture is not the game's object graph** (DEV 2, 2026-09-02, on REVIEWER's
  WITHHELD of the Weathering trio: `StepAll` wet a save snapshot that `CaptureLiveState`
  reallocates from live shelters on every save, so the bed the night is billed against never
  got wet while a hand-built fixture, a step counter, and an exactly-once test all reported
  success — the fourth guarantee that seat shipped with nothing holding it, and the common
  factor was verifying the MODEL and never the WIRING). A headless test proves the law; it
  cannot see a Scripts-layer copy semantic. **The general form, DEV 2's own tightening an hour
  later when the same disease turned up in dropped items: a SAVE RECORD IS NOT THE LIVE
  OBJECT — a pass that walks persistence state (`GameState`, a snapshot `CaptureLiveState`
  rebuilds from the world) is testing the save, not the world, and this generalises past
  moisture to anything anyone ever steps.** Any claim about what the WORLD does — "the bed
  dries", "the shelter wets" — needs a test or a scenario that reaches the live objects the
  world runs, and a commit message that claims the world changed while only a fixture or a
  snapshot did is a false claim under §9.
- A test that cannot fail is worse than no test: no tolerance so wide it always holds, no
  assertion loop whose body can be skipped entirely (count the cases and assert the count), no
  test that recomputes production's formula and compares production to itself.
- **Three ways a green run lies, each measured by BOARD-LOCKS on 2026-09-02:** a thread that
  raises inside a test dies without `unittest` knowing, and the assertions pass over the hole —
  every threaded test joins its threads and re-raises what they caught; a same-length mutation
  restored inside one second leaves a `.pyc` Python still trusts, so the restored source ran RED
  against correct code — a mutation harness invalidates bytecode (or touches the mtime forward)
  between plant and restore; an LF anchor finds nothing in a CRLF working copy and says so
  silently — a source-text rule normalises line endings before it matches, and a harness that
  finds no anchor fails loudly rather than reporting nothing changed.
- **Unjudged is not refuted** (QA DEV, 2026-09-02, on its own adversarial harness: 82 of 141
  verifier agents died on a usage limit, its scorer counted "did not refute" as zero for the
  dead, and 41 findings printed as "killed" — one of them a real defect another lens confirmed
  3/3; REVIEWER's sweep lost 103 of 161 the same hour). A verification harness scores on the
  votes it actually received and reports a finding whose judges died as OPEN, never as
  refuted; a sweep with partial coverage says which part, and its verdict rests on what was
  read, not on the agent count. The same disease as a green run that proves nothing, wearing a
  refuter's costume.
- **On a race, green is weak evidence and only red is proof** (BOARD-GATE, 2026-09-02: its
  80-writer append test passed twice and still passes, while BOARD-LOCKS measured the same
  writer losing 244 of 500 lines — the test was not detecting the bug, it was failing to hit
  it). A test of a concurrent property earns trust only by having been made to fail against
  the unguarded code; an equality assertion that passes most runs is flaky-green and reads as
  solved in between, so until the guard exists the loss is STATED (printed per run, written
  in the docstring) and the assertion is committed commented, to be uncommented in the commit
  that guards the writer.
- **A layer that parses what another program will execute must never assume it parses it
  the same way** (BOARD-GATE's adversarial pass, 2026-09-02, three of four holes one shape:
  `shlex` ends a word at `#` where bash starts a comment only at a word's start, so a
  forbidden tail was not refused but INVISIBLE; an option key of `-Osh` was in no set, so
  `git grep -Osh` ran `sh` through a read-only verb; `spawn-sandbox.cmd::$DATA` was a
  different string and the same NTFS file). Such a layer is defence in depth, never the
  gate; it refuses what it cannot parse rather than stripping to a guess; and an ambiguity
  between its parser and the executor's is a red finding, not an edge case.
- **Generated test source is source nobody reads** (BOARD-LOCKS, 2026-09-02: an unescaping
  step ate a backslash, `"wt
owner"` became `"wtowner"`, and the test planted no newline
  and asserted against a forgery never attempted — red for an unrelated reason, which is
  worse than failing loudly; and a rename that ran before the tests it renamed were inserted
  produced four confident REDs that meant nothing). A test that was generated is re-read by
  hand before it is trusted, and a patch script's order of operations is stated in its
  output, because it is a fact about the script that its result does not show.
- An enforcement test is **proven by sabotage before it is trusted**: plant the violation it
  exists to catch, watch it fail, remove the plant, watch it pass. Asserting a test works is how
  a regex that matches everything ships green (it happened — a heredoc silently halved the
  backslashes out of a word boundary, and only the sabotage check noticed).
- Test counts are stated in commit messages ("338 headless green; Death, Tablet pass in the
  player") — and stated only when actually run.

## 9. Comment and commit law

- Doc comments explain **mechanism and history** — why the code is shaped this way and what it
  cost to learn. They are the project's institutional memory and are load-bearing.
- Therefore: a comment that makes a checkable claim is either enforced by a test or not written.
  A false confident comment is worse than none. When a commit message claims something was
  removed, retired, or fixed, the claim ships with its enforcement.
- **A doc comment documents the declaration that immediately follows it** (ruled 2026-09-02
  on REVIEWER 2's eleven instances and REVIEWER's twelfth, `FireBuilder.cs:86-98`, where two
  stacked summaries left a field carrying a method's paragraph and the method documented by
  nothing). Inserting a member between a `///` block and its subject re-attaches the comment
  silently and compiler-invisibly. Nothing stands between a `///` block and its declaration but
  further `///` lines and attributes, and a second `<summary>` may not open before a declaration
  has consumed the first. A source-rule test over every `.cs` under `Assets/EarthGame` enforces
  it (DEV 1 owns it; CRLF-normalised; loud when it finds no `///` at all).
- **Date the ruling, never the state** (SOUND DEV, 2026-09-01). A comment may cite a decision
  and its date; it must not assert a count, a copy-number, or any other fact about the tree,
  because the tree changes and the sentence does not. "The third copy of this formula was mine"
  went false in the first merged state that could falsify it — both parents true, the merge a
  liar. (Comments matching `^\s*//.*<date>` numbered eight when this was written, `///` alone
  six — both counts true, and the pattern sits beside each because the first draft of this very
  bullet committed the invented-scope offence one bullet above its law.)
- **The manufactured-rationale screen** (SOUND DEV + REVIEWER, same date): before writing an
  explanatory sentence, ask *"would I have written this before seeing the behaviour?"* A
  rationale invented to fit an observed result reads exactly like a derived one and defends the
  wrong thing forever.
- **The invented scope** (same authors, same date; a sibling of the rationale screen and kept
  apart at SOUND DEV's own insistence — collapsing near-alike findings is itself the error one
  of them names): a number stated without the pattern or frame that produced it verifies
  nothing ("eight comments" — matching what?). The scope goes beside the number, on the page,
  every time; two true counts with absent scopes read as one contradiction.
- **The unfalsifiable-today claim** (SOUND DEV, 2026-09-01, found when DEV 2 built the fact
  rather than by reading the comment): a claim about a fact the world does not yet have is not
  safe merely because nothing can currently disprove it. It passes every review by
  construction — there is nothing to check it against — and comes due the day somebody builds
  the missing model, in a file they have never read ("a dawn chorus happens because the birds
  are up" was always false; the world gaining a singing bird is what made it testable, and it
  failed the instant it could). Before asserting how an absent model will behave, either mark
  the sentence as conjecture with its date, or do not write it.
- **The seam form** (DEV 1, same date, verified by REVIEWER): a source-text rule is a fallback,
  and needing one usually means the logic sits on the wrong side of a seam. Before writing the
  rule, ask whether moving the fact makes the rule unnecessary — an unwritable violation beats a
  caught one.
- Commits are essays in the house voice: what changed, the mechanism, what it cost, proof at the
  end. The owner commits; sessions leave the tree clean and explained unless asked to commit.

## 10. Process law

- Three sessions (`CLAUDE.md`): FABLE leads (project head — priorities, contracts, sign-off,
  final arbitration), MAIN DEV edits, REVIEWER audits read-only. Cross-session traffic goes on
  `Docs/SESSIONS.md`, dated and addressed.
- Every substantive change set gets an adversarial review, enforced at the push (the two keys
  guard the publication boundary — see `CLAUDE.md`): unreviewed local commits stack at most
  two deep and only with their review already requested; nothing unreviewed is ever pushed.
  Review findings are VERIFIED or SUSPECTED, with file:line and quoted code, never one dressed
  as the other.
- A landed commit that touches a player-facing seam gets a **second pass after landing**:
  candidates re-derived from scratch, each handed to a reader instructed to refute it. The gate
  reviews the change; the second pass reviews the belief the gate formed. Standing since
  2026-08-31, when the reviewer invented it unprompted and it caught, within the hour, a wheel
  regression (`abcba3b`) both keys had passed — the sign of a conversion was checked and its
  unit never asked about. A conversion crossing a boundary is two questions, sign and unit.
- Escalation is mandatory, not shameful: two failed attempts at the same bug, any dispute, any
  cross-seam refactor → FABLE. Escalation-gated items on the board are not started solo.
- Concurrency: one `EarthGame.exe`, one `dotnet test`, one Unity editor at a time, owner-
  coordinated.

---

## Enforcement table

| Law | Enforced by | Strength |
|---|---|---|
| 1 Layering | headless csprojs fail to compile a violation | compile |
| 1 C# 9 | `LangVersion 9` in both csprojs | compile |
| 2 Repository | review | **review only** |
| 3 Fact ownership | `OwnershipTests` (named constants); otherwise review | partial |
| 4 Determinism | Sim test suite; `GeologyTests` stability cases | test |
| 5 Save | `StateDigest` reflection test catches uncovered fields | test |
| 6 Input seam | `OwnershipTests` source rules (`Input.*`, wheel) + `KeybindSourceTests` | test |
| 7 UI / UiScale | `UiScaleTests` (source-text); otherwise review | partial |
| 8 Test law | REVIEWER hunts vacuous tests each review | review |
| 9 Comment claims | source-text tests where written; otherwise review | partial |
| 10 Process | `CLAUDE.md` + hooks surfacing the board | process |

Promotion rule: when a "review only" law is broken twice, MAIN DEV adds a mechanical enforcement
(source-text test, csproj setting, or hook) and records it here. This table is the honest map of
where the project is still trusting instead of checking.
