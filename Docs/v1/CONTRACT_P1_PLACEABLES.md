# Contract P1 — placeable components: the founder chooses ground and the models judge it

**Status: binding contract, written before code. Author: FABLE, 2026-09-01, opening the
critical path early on the owner's acceleration directive. Implementation: PLACEABLES DEV,
worktree `C:\Users\willi\projects\earth-game-placeables`, branch `track-placeables`. Review:
REVIEWER. This is FOUNDERS_PATH's largest unstarted beta item — "Placeable components:
windbreak, bedding pile, fire ring, drying rack (L)" — and the spine of Act II.**

## The thesis

FOUNDERS_PATH Act II, verbatim: the founder *"chooses any defensible spot (creek-near,
wind-shadowed, the player's own read of the country) and raises a camp from parts"*, and
*"the existing shelter/thermal model judges every choice honestly."* Nothing here invents
physics. A placeable is a way of ASKING the models that already exist — a windbreak asks the
wind model from a new position; bedding asks conductance; the drying rack asks the same EMC
curve the fuel obeys. The judgment is never new; only the asking is.

## The work

- **P1a — the placement grammar, once:** carried materials become a placed component at a
  founder-chosen spot; legality is physical (ground slope, space) not zonal ("fire ring
  anywhere legal" — the arc's words); placement is a verb through the existing seam; every
  component persists additively (`StateDigest` same commit, the save law as always).
- **P1b — the four, in arc order:** windbreak (reads the wind the body already feels, from
  its own position and orientation — the carved exposure lands here); bedding pile (mass +
  loft into the conductance the sleep model already bills); fire ring (a hearth site the fire
  model accepts anywhere the ground does); drying rack (hung mass drying on the fuel-moisture
  curve — the same `dm/dt` the tinder obeys, pointed at cordage-hung stores).
- **P1c — the camp reads them:** `Camp.Assess`'s four conditions consume placed components
  through the models, never by tag — a windbreak that does not actually break wind does not
  count, which is the honesty the whole game runs on.

## Rules

Partition: `Assets/EarthGame/Scripts/Shelter/**` and `Assets/EarthGame/Sim/Shelter/**` are
yours; `Interactor`/verb wiring is DEV 1's seam, claimed per change set, narrow and loud;
`ScatterField`/world spawn stays DEV 2's. Every board law binds from first prompt. Each
component ships with its headless tests (the model side), one sabotage each, and a scenario
(`.test.md`) QA can run — vocabulary gaps filed to DEV 1 in Q1b's shape, never built here.
The oracle answers placeables through the same models (no second derivation); GFX draws them
via C11's channel when it lands — you expose facts, never draw.

**(Amended 2026-09-01 on the seat's arrival disputes, both upheld: (1) "no new physics"
means no INVENTED physics — a published, cited shelterbelt/porous-fence curve imported into
`Sim/Shelter/` with one owner and its own tests is the Simard precedent, the house's way of
asking reality itself; the windbreak is GRANTED its curve, porosity and barrier-heights and
lee recovery, sources named in the doc comment. (2) The partition's reach was understated:
`Camp.cs`, `Oracle.cs`/`Situation`, `StateDigest.cs`, `GameState.cs`, `Survival.cs` and the
verb seam go by CLAIM, one change set at a time, named owner answering or the 30-minute
silence granting. The stale "carved exposure lands here" line is acknowledged — it landed in
`47bd3b4` by another seat; consume, never re-derive. The seat's stated order — P1a pure-Sim
first, windbreak after the ruling — is approved as its own.)**

## Explicitly out of scope

Freeform stacking (post-beta by the arc's own words); deadfall and fish trap (post-beta
components); decay/weathering of placed things; any INVENTED physics (see the amendment —
published cited law with one owner is asking, not inventing).

## Acceptance

All four placeable through the verb seam; each judged by the pre-existing model it asks
(shown by a test that moves the input and watches the judgment move); persistence
round-trips; `Camp.Assess` satisfied by a real camp built in a scenario end-to-end; suites
green with the floor stated; REVIEWER's pass on `track-placeables`.
