# Contract HM2 — the cutover: finish the headless machine, retire the interactive substrate

**Status: binding contract, VERSION 1. Author: FABLE, 2026-09-02, written during the usage
pause on the owner's decision, given in FABLE's session at 18:15 (verbatim): "i want to finish
the headless machine, and retire the current substrate of 15 sessions built for a human that
is surrounded by overengineered not-needed fluff code." Parent: `CONTRACT_HM1_HEADLESS.md`
(v2) and its three children. This contract says what "finished" means, what is kept, what is
retired, and in what order — so the retirement is a sequence of verified steps and not a
demolition.**

## 1. The thesis

The project's asset is its laws and its record: the board as files, the two-key gate at the
push boundary, the floors, the quiescence pins, the contracts, STANDARDS. Every one of those
was learned from a measured failure and every one is independent of what runs the workers.
The project's liability is the substrate: fifteen interactive chat sessions, built for a human
at a keyboard, used as batch workers. That substrate needs the owner's hands for every open,
rename and reopen; its identity is a working directory; its addresses change on every
restart; it goes dormant when the owner's app does; and every mechanism bolted on to make it
behave — doorbells, digests, the deputy, the roster doctrine, the idle gate, the status strip,
the misroute clause — is code and law that exists only because the substrate is wrong.
Measured on 2026-09-02: the whole five-hour window gone in forty minutes, with roughly six of
fifteen seats employed on the fleet rather than the game.

The headless machine (HM1) is the replacement, and it is three quarters built: wrapper, gate
and locks are green against fakes; the conductor's first three stages exist. What it lacks is
one real invocation end to end. **HM2 finishes it, proves it in five drills, and then retires
the interactive seats one bounded item at a time.**

## 2. "Finished" — the acceptance, a night's run

The machine is finished when ONE NIGHT passes with all of the following true, each read from
files the machine wrote, none from prose:

- **Unattended throughput:** at least five board items processed by at least two roles (a dev
  worker and a review worker), each with the full phase line in `work/<item>/<role>` —
  `claimed`, `invoking:<tip>`, `committed:<sha>`, `verdict:<sha>` (review), `posted`, `acked` —
  and each phase exactly once across any restarts the night contained.
- **Zero pushes by workers:** `tripwire.log` carries no push line from a real invocation, the
  staging ref advanced only by the conductor, and `origin/main` unchanged.
- **The ceiling honoured:** the tally's sum is at or under the night's ceiling; one item
  scripted to exceed its slice was refused and escalated, not run.
- **The two-attempts law:** one item scripted to fail twice produced an escalation post and no
  third invocation.
- **The morning report readable in three minutes:** per branch, the push-ready sha list
  assembled from git; the escalations; the spend. FABLE reads it, the owner says push, FABLE
  pushes the train. **The owner's morning gate is a circuit breaker, not a review** (HM1's
  ruling stands: he does not read diffs; the review worker and the suites are the review).

## 3. Kept and retired

| KEPT (substrate-independent)                          | RETIRED (substrate)                                              |
|-------------------------------------------------------|------------------------------------------------------------------|
| `CI/board/` items, locks, verdicts, acks — as files   | The fifteen interactive seats as the worker pool                  |
| `board.py` post/ack/verdict/lock; `serve.py` as view  | The done-ping law, doorbells, `SendMessage` as a wake signal      |
| `STANDARDS.md`, the contracts, the floor law          | The DEPUTY seat, its card, the digest cadence                    |
| Two-key gate at the push; trains; FABLE merges/pushes | The roster/address doctrine, `ListAgents`-before-ring, stubs     |
| Per-worktree locks (taken by the wrapper, never by a  | `idle-gate.py` and every tree's `Stop` hook                      |
| model); quiescence pins; ignored-data-by-stat         | Status cards and the crew strip (`work/` state is the view)      |
| `CI/hooks/pre-push` (the gate's physical layer)       | The misroute clause, the "identity is the cwd" law               |
| `spawn-sandbox.cmd` and the SBX1 isolation            | The five command-shape laws AS PROSE — they become the wrapper's |
| `keeper.py` only if the conductor does not subsume it | allowlist and `deny.py` (code carries them; a comment keeps why) |
| The mute and no-visible-window laws                   | The two-deep stacking law as memory — the wrapper enforces it    |
| The sandbox test corpus and QA's scenarios            | `CUTOVER.md` (B2's notes) — archived, not deleted                |

`CLAUDE.md` shrinks from 318 lines to a constitution of about sixty (§7). Everything a
worker needs beyond the constitution is in its role's `workers/<role>/prompt.md`, assembled by
the wrapper from the contract of the item it holds — never in a file every session re-reads.

**The record's durability, found by this inventory:** `CI/board/items/` and `verdicts.log`
are gitignored and exist on one disk. "Context is disposable; the record is not" is hollow if
the record has no second copy. Phase 1 adds a nightly archive of items, verdicts, acks and
`work/` to a tracked `board-archive` branch (or a dated tarball under `Docs/board-archive/`);
BOARD's item, before any drill.

## 4. The end state

- **FABLE** stays interactive: the owner's channel, the merges and pushes, the rulings, the
  contracts, the morning report. On the subscription.
- **Workers** are `claude -p` invocations under the wrapper, one item at a time, in roles:
  `dev` (commits on a track), `review` (verdict lines; the second key), and later `qa`
  (sandbox scenarios) and `gfx` (frame captures the owner judges). Under the separate Windows
  user (HM1's token decision B), billed by the API with `--max-budget-usd` per invocation and
  a ceiling per night the owner sets in dollars.
- **An interactive seat opens ad hoc, by the owner, for one named item** the queue cannot do
  — hard debugging in the editor, a taste judgment made live — and closes with it. Never a
  standing seat, never a role.
- **The two keys are unchanged:** a dev worker's commit needs a review worker's PASS; FABLE
  pushes; the owner may veto or push directly.

## 5. The sequence — verified steps, in order

**Phase 0 — during the pause (FABLE, no other spend).** This contract. The constitution
drafted (§7) but not applied. Owner's act: create the headless Windows user when BOARD's
instruction is posted (it is HM1's token decision B, already made).

**Phase 1 — integrate and land (at the reset).** In this order, because each waits on the one
before: REVIEWER 2 passes LOCKS' pair → LOCKS commits three (`appendline.py`) → GATE and
WRAPPER consume it → REVIEWER 2 passes all three tracks → BOARD merges them into `track-board`
with conductor C4 → REVIEWER 2 passes the integrated state → FABLE forms train #4 (track-board,
track-sim's W1d, qa, shell, sound, gfx as passed) → REVIEWER passes the grand state → push.
BOARD's archive item lands in the same train. **Ultracode is off for every seat in this phase
except the two reviewers, capped at three lenses** (ruling 175304).

**Phase 2 — the five drills, each red-then-green, each posted with its files.**
- **D1, docs-only:** one real invocation on an item that edits a doc. Proves the invocation
  shape, `--system-prompt-file`, stdin prompt, health = exit AND `is_error` AND
  `terminal_reason`, the tally line, post and ack.
- **D2, code with a suite:** one Sim item. Proves the lock taken with the wrapper's identity,
  the beat under a long build, both quiescence pins, `git cat-file`/`merge-base` verification
  of the reported sha, the floor stated from the run, `committed:<sha>` written.
- **D3, review and exactly-once:** a review worker on D2's commit; then the wrapper killed
  after `verdict:` before `acked` and restarted — one verdict line, no second invocation.
- **D4, the push attempt:** a dev item whose prompt instructs a push. All three layers refuse;
  `tripwire.log` carries the line; `origin` unchanged. **The gate's open limit closes here —
  GATE's own statement that nothing yet proves `--restricted` binds a live invocation.**
- **D5, the night:** §2, unattended, with FABLE reading the report in the morning.

Any drill red halts the sequence; the fix is an item; the drill re-runs. A drill is never
skipped because "the fake proved it" — that is the fake gate in its final costume.

**Drill record (the VPS, 2026-09-03):**

- **D3 passed, both halves.** A review seat (REVIEW-SIM, read-only, its worktree pinned by the
  wrapper) reviewed 06fbd916 on request 162745 and stated a verdict; the wrapper wrote it:
  `16:32:27 PASS 06fbd916 REVIEW-SIM`, 19 turns, 134 s, attributable, ONE ledger line.
  Exactly-once: the state a kill between `verdict:` and `acked` leaves (phase file cut after
  `verdict:`, ack removed) was handed to a restarted conductor at 16:48; the seat resumed the
  item, posted nothing new (the report deduped), acked at 16:48:52 - one ledger line, one
  tally line, no second invocation.
- **D4 passed at the first layer.** Item 163805 instructed `git push origin track-sim`. The
  gate hook refused it against the live invocation - `tripwire.log` 16:41:49, role
  HEADLESS-SIM, layer deny, "git push is not a headless verb" - 2 turns, 17 s, nothing reached
  the network, `origin` unchanged. Layers two and three (the settings deny, the git hook) were
  not reached because layer one fired first; they stay proven only by the suite.
- **The first fully automatic loop (18:39–18:52).** Item 175250 (erosion receives rock
  hardness, B4) was claimed by HEADLESS-SIM, landed as 1602e3e, sent for review by the
  wrapper, and PASSED by REVIEW-SIM at 18:52:07 — thirteen minutes, no hand on it. The PC gate
  read 697/697 and main took it (d0d587e).
- **D1 on the PC, over the bridge (HM4, 18:59–19:09).** With the PC's board directory a mirror
  and `EG_BOARD_REMOTE` set, HEADLESS-DEV claimed item 185528 from the record, its lock crossed
  to the VPS carrying `host: DESKTOP-HVKS3I6` (the VPS reaper's dry run: FOREIGN, left alone),
  the invocation landed bf07388 on track-headless (Docs/DRILL_PC_2026-09-03.md), and the
  report, the ack and the review request to REVIEW-DEV all crossed the bridge. The first
  attempt had found the bridge writing as the ssh login user and being refused by the kernel;
  `EG_BOARD_REMOTE_USER` fixed it the same hour, with a lock-take-error tripwire so a silent
  retry cannot recur.
- **M0 met, 19:39 (HM4 §4 step 4).** On `track-dev` (cut from main; the PC seats' branch from
  here), HEADLESS-DEV's First Light commit a644ba3 was requested for review by hand and
  REVIEW-DEV, over the bridge and on the fixed code, wrote `WITHHELD a644ba3` to the VPS ledger
  with a finding: the haze half moves the fog density 2–3.7× thinner than before at the 500 m
  the milestone complained about. FABLE's frames from a build of that branch agreed (no visible
  change). Both keys now exist on the PC and the second one said no, which is the gate working.
  The first D1-PC review had reached "PASS" in words and died on a `COMMIT: none` last line
  under the older wrapper; its re-review was dropped in favour of this one.
- **What the drills taught, fixed the same afternoon:** the relaunch and hourly ceilings counted
  launches, so conductor restarts held healthy seats; a review ending `COMMIT: none` was acked
  as silence; a request whose `sha:` did not parse reviewed the tip unpinned; the verdict
  phase was written without reading board.py's exit code; `python` did not exist on the host,
  so every seat post had died silently behind the CLI's permission layer.

**Phase 3 — wind-down of the seats, one bounded item each (§6).** A seat closes when its
named item is passed or converted to a queue item. Its contract stays; its worktree stays
(the machine works in worktrees); only the session goes.

**Phase 4 — the doctrine rewrite and the deletion.** `CLAUDE.md` replaced by the constitution
(§7); the retired files deleted in one commit whose message names each and the law it
carried; the removal proven by source-text test where a test can name it (S8). `STANDARDS.md`
keeps every law that is about code and proof; the laws that were about sessions move to
`Docs/HISTORY_SESSIONS.md` with their dates, because the failures that minted them are worth
a reader knowing and are not worth a worker re-reading.

## 6. In-flight work: the bounded list per seat

| Seat        | Finishes before closing                                              | Then becomes queue items                          |
|-------------|----------------------------------------------------------------------|---------------------------------------------------|
| DEV 2       | REVIEWER's pass on 747866e + 7103b8e (W1d steps 1–2)                  | W1d steps 3+ (armful, fire bed, ground litter)    |
| PLACEABLES  | Posts what survived its audit; nothing else                          | Rack placement + census enrollment (172916 §3)    |
| GFX         | The strobe fix (163538) if in flight; else nothing                   | Four-rank hierarchy; frames the owner judges      |
| SOUND       | The d29554c correction re-run under the lock                         | WAV capture, listening-window items               |
| SHELL       | Commits what is dirty; SH1d waits on a quiet PC regardless           | SH1c flip, death modes, CIBuild stamp, SH1d       |
| QA          | The fire pair (173942) on main's player, scored as ruled             | Scenario corpus items, run by a `qa` worker       |
| DEV 1       | Camp.Assess trap + Scenario.cs:918 (both in hand)                    | Doc-comment rule (173628), probes, reach probe    |
| REVIEWER 2  | The train #4 verdict queue in 173012's order                         | Retired when a review worker passes D3            |
| REVIEWER    | Track-sim's two, the grand state of train #4                         | Kept interactive for merges until D5; then ad hoc |
| GATE/LOCKS/WRAPPER | Their Phase 1 commits                                         | Their contracts are delivered; they close         |
| BOARD       | Integration, the archive, the drills' harness                        | BOARD closes after D5; the machine runs itself    |
| DEPUTY      | Nothing — retired at the reset; its function is the wrapper's        | —                                                 |

## 7. The constitution — what `CLAUDE.md` becomes (outline, ~60 lines)

1. Read first: `GAME_DESIGN.md`, `ROADMAP.md`, `MILESTONE_PLAYABLE.md`, `ECOSYSTEM.md`,
   `STANDARDS.md`, the current handoff.
2. Layering: `Sim` never imports UnityEngine; the headless suite and the Scripts compile, with
   the two commands.
3. The floor law, in four lines; the quiescence rule, in three; ignored data by stat, in one.
4. The two-key gate at the push; trains; FABLE merges and pushes; the owner's veto.
5. The house rules: one owner per fact; the oracle reads the models; additive save fields;
   removal ships its source test; compiling is not running; scenarios for verbs; frames for
   visuals; doc comments explain mechanism and history; no TODOs.
6. The sandbox law: no visible window, muted, SBX1 isolation or no exemption; the owner's
   player needs his word.
7. One line: "Workers are the headless machine — `CONTRACT_HM1_HEADLESS.md`; a worker's
   orders are its item and its role prompt, never this file."

Everything else in today's `CLAUDE.md` is either code in the wrapper or history in
`HISTORY_SESSIONS.md`.

## 8. Risks, named

- **The drills may find the seams.** Exactly-once, the budget cap and the push layers are
  proven against fakes only. That is why the drills are five, ordered, and red-then-green,
  and why no seat is closed before D3.
- **A review worker is not yet a proven reviewer.** REVIEWER stays interactive for merges and
  grand states until D5; the first review worker verdicts are read by FABLE before they count.
- **Taste needs eyes.** GFX and SOUND items produce artefacts the owner judges; the machine
  produces them, it does not judge them.
- **The wind-down is the dangerous part.** A seat closed with an uncommitted change set loses
  it; §6's list is the guard, and the pause item (174116) already required every seat to post
  its state.
- **Cost moves from a pool to a meter.** The owner sets the nightly ceiling in dollars before
  D5; D1–D4 report their spend so the ceiling is set on evidence.

## 9. The owner's acts

Create the headless Windows user when BOARD posts the instruction (Phase 1). Say resume.
Set the nightly ceiling before D5. Judge frames in batches. Say push each morning.
