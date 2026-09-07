# Contract K1 — no key off the books, every key drivable, the page provably honest

**Status: binding contract, written before code. Author: FABLE, 2026-08-31, on the owner's
direction ("work on the controls/keybinds"). Implementation: MAIN DEV, queued after finding E
(owner-directed — B1 slides one slot). Review: REVIEWER, against this document.**

## Where the truth already is

`Keybinds` is genuinely good: 17 bindings with groups, defaults, clash detection that shows
rather than prevents, PlayerPrefs persistence with Dirty/Revert semantics, and non-rebindable
literals that explain themselves (Escape's five listeners, the mouse buttons that "mean whatever
the situation says"). The controls page derives from `Keybinds.All`, and the source tests now
check page→listener and listener→page. This contract is not a rework — it closes the four gaps
a grounded audit just found.

## The findings this contract closes

1. **`Sleeping.cs:35` — `public const KeyCode LieDownKey = KeyCode.Z;`** A live gameplay key,
   raw, outside the registry: not on the controls page, not rebindable, invisible to a player.
   This is the exact disease `Keybinds`' own doc comment describes ("a written list… is worse
   than nothing"), except worse — Z is on **no** list at all.
2. **`FlatPlayerController.cs:296` — `Keys.Held(KeyCode.LeftControl)`** for fly-descend. Dev
   group, described only inside `Fly.Does` prose; the read is raw.
3. **`Scenario.DownNow`/`Up` hard-code a six-name map** (work/use/drop/jump/run/tablet). A
   mirror of the registry maintained by hand — the named shape. Any binding not in the map is
   undriveable by name, silently.
4. **Nothing mechanically forbids the next raw key.** `Tablet.cs`'s hard-coded Tab survived one
   commit claiming its removal; `Sleeping.Z` has survived since sleep shipped. The registry's
   monopoly is enforced by review only — law 2's weakest tier.

## The work

### K1a — register or retire every raw key (the one-owner law applied to input)

- **`Sleeping.Z`:** decide by ownership, not convenience. Read `Sleeping.cs` first: if lying
  down is (or should be) a **Use** verb on the bed — which `Keybinds.Use`'s own description
  already claims ("lie down") — then Z-to-sleep is a duplicated rule and **dies**; waking stays
  on Escape, which the `Pause` row already documents as "backs out of anything open". If there
  is a real reason sleep needs its own key (there may be — asleep, the crosshair points at the
  inside of eyelids), it becomes a proper rebindable `Binding` in a "Resting" or existing group,
  with `Does` text, and the const is deleted either way. State which branch was taken and why in
  the commit.
- **Fly-descend Ctrl:** stays raw-read but becomes *registered prose*: `Fly.Does` already names
  it; add it to the exemption list of K1d's source test with a comment naming `Fly` as its row.
  (A dev-group literal row would be noise; the exemption list is the honest register for it.)
- **The planet stack (`FirstPersonPlanetController`) is exempt wholesale** — it is the frozen
  fork; K1d's test excludes it by path with a comment saying why.

### K1b — the scenario map derives from the registry

`Scenario.DownNow`/`Up` stop hand-mapping names. Lookup order: the six existing aliases stay as
aliases for script compatibility (`work`, `use`, `left`, `right` are mouse and stay literal),
then **any `Binding.Id` from `Keybinds.All` resolves to `Keys.Press/Release(binding)`**, then
the existing `Enum.TryParse` fallback. Delete nothing a script depends on; add the general path
so the next binding is drivable the day it is declared. The hotbar stays drivable as it already
is (`Alpha1..8` through the seam at `Interactor.cs:82`).

### K1c — a controls-coverage scenario

New built-in `-eg-scenario controls`: through the real seam, in one run —
- `tap tablet` → `expect tablet.open == true`, tap again → false;
- `give` a stone, select it **by pressing the real `2` key** (not `select`-by-name — this
  scenario exists to prove the input path), `expect held.kind == Stone`;
- `press drop` → `expect items` decreased / `count.Stone` on the ground probe if one exists;
- `press jump` with a `moved`/height probe if cheap, else jump is covered by the existing walk
  scenario's locomotion and may be skipped **with a comment saying so**;
- **wheel-cycles the hotbar while the tablet is down** — the gap E named: two items carried,
  `wheel` steps, expect `held` changes each notch and wraps at the ends the way the hand does;
  this is `Interactor`'s untouched branch of the new seam read, and it gets exercised here.
  **Author this against the corrected sign comment only**: the second pass on `abcba3b` found
  `Scenario.cs:505-506` states the hardware direction backwards, and the wheel-truth commit
  (which fixes that comment and the detent unit) lands before this scenario is written;
- if K1a kept a sleep key: drive it and expect the sleep state probe.
Every expectation must be able to fail: sabotage each probe once during development (law 8).

### K1d — mechanical enforcement (promote law 2 for input)

*(Amended on MAIN DEV's audit, which sharpened the rule: policing read sites would flag the ten
legitimate `Keys.Down(Keybinds.X.Key)` indirections to catch one violation. The clean cut is at
the declaration. Ratified by FABLE 2026-08-31 — the audit is right, and this text now states the
ratified rule.)* New source-text test in the `KeybindSourceTests` family: **no file but
`Keybinds.cs` may declare a `KeyCode`** in `Assets/EarthGame/Scripts/**` — no consts, no fields,
no locals initialised from a `KeyCode.` literal. Expression-bodied aliases pointing back at the
registry (`=> Keybinds.X.Key`) are the pattern working and stay legal. Exemptions only as a
measured need appears: the audit measured the whole tree at exactly **one** violator
(`Sleeping.cs:35`), so the list starts at the owner alone — the read-site exemptions the original
text pre-granted (fly-descend Ctrl, the planet stack, the menu's capture) are facts a declaration
cut never touches, and an exemption invented in advance is the rule quietly widening
(`b34017f`'s lesson, re-learned the same day on the wheel fact's five unexercised owners). The
test goes green the moment K1a retires the constant. Prove by sabotage: a planted
`const KeyCode` in a gameplay file must turn it red. This is the test that would have caught
both the Tab ghost and `Sleeping.Z` years early.

### K1e — page-prose hardening (REVIEWER's residual, adopted)

The page→listener test gains the reviewer's suggested `word + "\s+key\b"` pattern so "the tab
key switches page" phrasing is caught while "tab" the noun is not. Sabotage both directions.

### K1f — the seam's liveness checks made falsifiable (second-pass finding 4, adopted)

`OwnershipTests.TheSeamStillReachesTheRealInput` asserts six fall-throughs by substring, and
three are strictly implied by their longer siblings: `Input.GetKey` by `Input.GetKeyDown`,
`Input.GetMouseButton` by `Input.GetMouseButtonDown`, `Input.GetAxis` by `Input.GetAxisRaw`.
The three that cannot fail are the three carrying continuous input — movement, held verbs,
mouse-look, and now the tablet's wheel. REVIEWER demonstrated it: a `Keys.cs` with all three
subsumed fall-throughs severed passes all six checks. Fix: anchor each needle so no sibling
implies it — the call-site paren is the cheapest cut (`"Input.GetKey("` is not a substring of
`"Input.GetKeyDown("`, and all six appear as real calls) — and prove it by the same
severed-fall-through sabotage, re-planted by MAIN DEV, since the reviewer's demonstration
cannot be the commit's proof (law 8). May ride the open K1a+K1d change set or the next commit,
dev's choice; the message claims what it contains either way.

## Explicitly out of scope

- **Tab as "what am I carrying"** — reserved for the inventory-window slice
  (MILESTONE_PLAYABLE order-of-work item 2); do not spend the key here.
- Gamepad/controller support — not roadmapped.
- Any change to the rebind page UX itself — it is good; K1 makes what it displays *provably*
  complete, not prettier.

## Acceptance

343+ headless green including the new source test; `-eg-scenario controls` green in the player
plus the existing five; Keybind self-test still green; every K1a decision stated in the commit
message with its reasoning; REVIEWER verifies K1d, K1e and K1f by planting the violations themselves
in a scratch copy (configuration-level verification acceptable where their read-only role
forbids planting, per their own established practice).
