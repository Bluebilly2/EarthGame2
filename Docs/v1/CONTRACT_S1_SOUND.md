# Contract S1 — the world audible: procedural sound from the models that run it

**Status: binding contract, written before code. Author: FABLE, 2026-09-01, on the owner's
seat expansion ("both"). Implementation: SOUND DEV, worktree
`C:\Users\willi\projects\earth-game-sound`, branch `track-sound`. Review: REVIEWER. The
owner's standing direction: procedural-first (ruled at the FOUNDERS_PATH questions);
"muted unless sound is nescesarry" governs every launch this work makes.**

## The thesis

Sound obeys the house rule everything obeys: **derived, never placed.** No stock loops, no
triggered stingers. The soundscape reads the same models the world runs — footsteps read the
actual ground (the E-chain knows sand from soil from rock), fire reads its own combustion
state, wind reads the exposure the erosion carved, rain reads `Weather.At`, and the birds
read `AnimalPresence` on the day's own clock — dawn chorus because the presence function
says the birds are there, not because a timer looped a file. A player who closes their eyes
should be able to read country by ear, and be right, because the ear and the eye consume one
world.

## The work

- **Audit first:** `Scripts/Audio/` already holds `ProceduralAudio`, `Ambience`, `Impacts`,
  `Footsteps`, `SoundSelfTest`. S1a is a written audit on the board: what exists, what it
  reads, what fakes. Extend what derives; replace what fakes; delete nothing without the
  absence claimed.
- **S1b — the beta soundscape (FOUNDERS_PATH's own list):** footsteps by surface, fire by
  its state, wind by exposure, rain by the sky, birds by presence. Parameters live as pure
  functions (Sim-side derivations go in `Sim/World/Soundscape.cs` — a new file is yours; any
  OTHER `Sim/**` or `Assets/**` file outside `Scripts/Audio/**` is claimed on the board
  first, DEV 2's Sim seat respected).
- **S1c — the `sound: on` escape comes due.** The sandbox contract deferred it "until a
  seeded test actually needs audio" — yours are the first. Implement the conditions escape
  (debt C9-audio in the handoff, wants a contract amendment to SBX1 which FABLE lands);
  a sound test asks for audio explicitly, everything else stays silent by law.

**(Partition amended 2026-09-01, dispute 4 granted whole: `Scripts/World/Ocean.cs` (surf
volume), `Scripts/Player/Footsteps.cs` (classification moves to `Soundscape`),
`Scripts/Game/SoundSelfTest.cs`, new `Tests/SoundscapeTests.cs`; `Scripts/Fire/FireSite.cs`
for the one crackle line, sequenced ahead of GFX; `Scripts/Flat/FlatWorldBootstrap.cs` for
S1c only, by claim. And the un-mute's second key, confirmed 2026-09-01 after SOUND's
refinement survived the argument: not a command-line flag — **an owner-declared listening
window that exists and has not expired**. A flag can be forged by any launch line and cannot
expire; a window covers the walk-away failure, because unattended noise is the actual
failure mode. Key one stays the test's own `sound: on`; both keys or silence.)**

## Verification, without taking the owner's speakers

Parameter curves are headless (the `SoundSelfTest` pattern — assert the numbers, sabotage
once each). For the audible truth: **capture, don't broadcast** — render probe WAVs where
feasible and put file paths on the board; the owner listens on his schedule. A launch that
must be audible on speakers happens only inside an **owner-declared listening window**
(asymmetry-doctrine shape: ask through FABLE, fly on the informed yes). Mute and display
laws bind every other launch this track makes.

## Rules

Law 6/8 as everywhere; the oracle/tablet never gains a second derivation of anything audio
knows; no new save fields; commits on `track-sound`, two-key gate, REVIEWER passes, FABLE
merges. Match the house style.

## Explicitly out of scope

Music; voice; UI bleeps beyond what readability already ruled; spatializer middleware; any
third-party audio package.

## Acceptance

The audit posted; the five beta sources deriving from their true models with the derivations
tested headless; the `sound: on` escape working and sabotaged; a probe-WAV set the owner has
heard and not vetoed; suites green with the floor stated; REVIEWER's pass on `track-sound`.
