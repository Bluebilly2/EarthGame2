# Contract SBX1 — the sandbox: tests defined once, run without a window in your face

**Status: binding contract, written before code. Author: FABLE, 2026-09-01, on the owner's
direction (a board-driven sandbox so "you don't have to keep opening and closing the game";
tests defined by what/aim/how/conditions; "the board can run numerous tests at once if asked").
Implementation: MAIN DEV 1, slotted ahead of the sound layer — every track's verify loop and
the owner's own screen pay for its absence daily. Review: REVIEWER, against this document.**

## The thesis

Everything hard already exists: the input seam presses real keys, the scenario library is a
command vocabulary, probes are the readback, the recorder (post-`c589338`) is the camera. What
is missing is only *persistence* and *definition*: a game instance that stays alive and takes
work, and a test format that says what a run is for. SBX1 adds those two things and nothing else.

## SBX1a — the test definition, a file format (the owner's four questions)

A test is a file in `CI/board/tests/*.test.md`: front-matter plus the scenario script it runs.

    what:       one line naming the test
    aims:       what it exists to prove, in prose a reviewer can hold it to
    how:        the scenario steps (the existing script language, verbatim)
    conditions: the world it needs - region, hour, speed, mode flags, items given,
                sound (default muted; `sound: on` only when a test needs audio)

`conditions` compiles to the launch/setup flags the scenario system already takes. The format
is the contract between the board and the game: nothing in it invents a new capability, it
names existing ones. The six shipped scenarios get `.test.md` wrappers as the seed corpus, so
day one has a library and the format is proven against real content.

## SBX1b — the headless sandbox instance

`EarthGame.exe -eg-sandbox` runs the player **minimized and never focus-stealing** *(the
focus half qualified, measured 2026-09-01 at the step (1) launch: `SW_SHOWMINNOACTIVE` shows
without activating but does not deactivate a window Unity already activated — a live instance
read `MINIMIZED=True HAS_FOCUS=True`; nothing is painted over, but keyboard focus is taken.
Fix in flight, DEV 1; this note dies with it)* — batchmode
is settled NO (DEV 1's finding: the recorder captures need a real back buffer, and five of six
seeds take shots) — and long-lived: it idles at a loaded world, accepts
a test via a **file-drop channel** (`CI/board/sandbox/in/<name>.job` — the scenario text plus
conditions; polled each frame, cheap), executes it through the existing `Scenario` runner, and
writes `CI/board/sandbox/out/<name>.result.md` — pass/fail per check, probe values, frame
paths, wall time. File-drop, not sockets: it composes with everything this repo already trusts,
works when the board is down, and REVIEWER can audit a job by reading two files.

**Lifetime is specified, because a hidden long-lived process with no exit is an orphan
generator (DEV 1's finding, ruled):** every instance writes a heartbeat file
`CI/board/sandbox/live/<id>.md` (its PID, its current job, mtime refreshed each poll) — the
one enumerable place anything can list live sandboxes, kill an orphan by PID, or spot a stale
heartbeat; a **job timeout** (per-test `conditions: timeout:`, default 5 minutes) reds the
result rather than hanging the instance; an **idle timeout** (default 30 minutes without a
job) exits the instance cleanly and removes its heartbeat. `serve.py` dying mid-run loses
nothing: jobs and results are files, and the heartbeat outlives the panel.

**Isolation is the law that makes parallel legal.** `-eg-sandbox` routes EVERYTHING per
instance: `-logFile` per instance, saves to a sandbox slot namespace the menu can never reach
(the slot-9 convention, generalised), recordings to a per-instance subdirectory, **and audio
muted at bootstrap** (owner-directed 2026-09-01, verbatim: "make sure they are muted unless
sound is nescesarry"). Mechanism (REVIEWER's finding + shape, same date): the pre-existing
`-eg-mute` — our own, focus-independent, `AudioListener.volume = 0f; Impacts.Master = 0f` —
made *implicit*: the bootstrap's condition reads `-eg-mute` **or** `GamePaths.IsSandbox`, so
a sandbox cannot be launched loud by anyone, including a hand-typed command line. The
instance owns the fact, never the spawner (the `-eg-newgame` implication pattern from
`5772b21`). The `sound: on` conditions escape is **deferred until a seeded test actually
needs audio** — a named debt, not scope. A hidden window does not silence a player, so the
mute is its own explicit member of the isolation set, and the exemption attaches to it like
the rest. The
single-instance rule exists because shared logs and saves corrupt; a sandbox instance that
shares nothing may run beside the owner's own game, and **N sandboxes may run beside each
other** — that is what makes "numerous tests at once" true rather than brave. The launch lock
now governs only the owner-visible player: **the exemption is IN FORCE, by the owner's word,
2026-09-01, verbatim — "everyone has my permission to do everything with the sandbox, it is
100% go" — encoded in `CLAUDE.md`'s launch rule.** The exemption attaches to the isolation:
an instance missing any part of it is not a sandbox instance and has no exemption. Instance
names carry the id in the window title even when hidden. (The interim history — proposed,
withheld by REVIEWER, conceded, then granted — is in the board record of this date.)

## SBX1c — the board panel

`serve.py` gains: list the test library (parsed front-matter — what/aims/conditions shown);
run one or many (writes job files; spawns sandbox instances up to a small cap, default 3);
show results as they land (a result strip per run: green/red per check, probe values, links to
frames). `board.html` renders it in the house style. The owner's flow: open board → tick tests
→ run → read results and look at frames — the game never takes their screen.

## Rules

- **No new derivation anywhere**: the sandbox runs the same `Scenario` runner, the same
  probes, the same recorder. A result the sandbox produces and a result a hand-launched
  scenario produces must be the same result — that identity is a test (SBX1's own law 8 case:
  run one seed test both ways, byte-compare the outcome lines). **First acceptance, owner-
  directed 2026-09-01 ("run the same tests as the first few that go through the sandbox, to
  make sure that it gives reliable results"): all six seeds run both ways**, byte-compared —
  the sandbox earns trust by agreeing with the hand-launched path on its whole first corpus;
  thereafter the one-seed identity case suffices per change. The hand-launched halves wait
  for REVIEWER's post-acceptance owner-root re-check, because they write `Player.log` into
  the evidence window that re-check needs clean.
- The stale-build warning applies to sandbox instances doubly — a sandbox on an old player is
  a lie factory; refuse to start if the build predates the newest source change, loudly.
- Law 6/8 as always: every new step or probe sabotaged once; the job/result channel gets a
  malformed-input case (a bad job file must produce a red result, not a hang).
- Scripts-layer change set: `Scenario`/bootstrap/`SessionRecorder` touchpoints are DEV 1
  territory already; `serve.py`/`board.html` are FABLE-owned — DEV 1 lands the game side,
  FABLE lands the board side against the same contract, REVIEWER passes both.

## Explicitly out of scope

Editor play-mode integration; any new scenario capability beyond running existing ones;
distributed/remote execution; replacing the pre-push gate (the sandbox is a verify surface,
not a publication gate).

## Acceptance

The six seed tests run from the board, three at once, with the owner's game untouched and no
window stealing focus; results and frames land in the panel; the both-ways identity test
passes; suites green with the floor stated; REVIEWER verifies the isolation claims by running
a sandbox beside a hand-launched player and diffing both logs for cross-talk.
