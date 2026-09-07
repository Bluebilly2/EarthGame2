# Contract Q1 — the arc, executable: a corpus that proves the beta every night

**Status: binding contract, written before code. Author: FABLE, 2026-09-01, on the owner's
seat expansion ("both"). Implementation: QA DEV, worktree
`C:\Users\willi\projects\earth-game-qa`, branch `track-qa`. Review: REVIEWER (of the corpus
as code). QA DEV holds no verdict authority — findings go to the board; REVIEWER's second
key and FABLE's arbitration are untouched.**

## The thesis

The owner signed a beta arc; nothing yet proves the arc *stays* walkable as five tracks
change the world under it. The sandbox exists precisely to make that proof cheap: **QA DEV
turns FOUNDERS_PATH into `.test.md` scenarios, beat by beat, and runs the corpus through the
sandbox continuously** — so "a stranger can reach bed, fire, and tools" is a fact the board
demonstrates nightly, not a hope the demo re-risks each time. Its currency is red: a
regression caught as a failing test file is worth more than a week of green.

## The work

- **Q1a — the corpus grows along the arc.** From the six seeds toward the path's beats:
  thirst refused at the sea with the reason shown; the creek found; first stone knapped and
  the wrong stone teaching; the forecast consulted; fire before dark; night survived; the
  Act II camp beats as the systems land. Every scenario uses the **existing** command
  vocabulary and probes; each `.test.md` states what/aims/how/conditions per SBX1a.
- **Q1b — vocabulary gaps are filed, not built.** A beat the vocabulary cannot express
  becomes a precise change request on the board to DEV 1 (the seam's owner): the beat, the
  missing verb or probe, the smallest addition that would express it. QA DEV touches no
  `Assets/**`.
- **Q1c — the nightly proof.** A corpus run (sandbox, muted, capped per SBX1) whose result
  lands on the board as one line per scenario plus deltas since the last run. Any red gets a
  minimal repro `.test.md` and a board item naming the commit range that broke it.

## Rules

- Partition: `CI/board/tests/**` only. Launches: `-eg-sandbox` instances exclusively —
  muted, minimized, isolated, capped — under the standing exemption; QA DEV never launches
  an owner-visible player.
- The both-ways identity discipline applies to any scenario whose result looks suspicious:
  rerun hand-launched (muted, non-primary) only after announcing on the board.
- A finding names its evidence (result file, log line, frame) — the house does not accept
  asserted references, its own QA least of all.
- Law 8 for the corpus itself: a new scenario ships with one deliberate sabotage run proving
  it can fail (a test that cannot red is not a test — this project caught four of those
  today).

## Explicitly out of scope

Verdicts on commits (REVIEWER's); fixing what it finds (the owning dev's); new scenario
capabilities (DEV 1's, by filed request); performance benchmarking (later, by its own
contract).

## Acceptance

The Act I beats runnable as scenarios with sabotage-proofs; the nightly line posting; at
least one vocabulary gap filed in the proper shape (there will be gaps); REVIEWER's pass on
the corpus commits; and the first real regression it catches, whenever the world provides
one — the contract completes on demonstrated red, not on green.
