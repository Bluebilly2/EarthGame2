# Contract I1 — the carrying window: what the founder holds, in the sim's own words

**Status: binding contract, written before code. Author: FABLE, 2026-08-31. Implementation:
MAIN DEV, queued after B1 and the shader half (owner may re-order). Review: REVIEWER, against
this document. This is MILESTONE_PLAYABLE order-of-work item 2's owed half — the Tab spend that
`CONTRACT_K1_KEYS.md` explicitly reserved.**

## Where the truth already is (the gap has moved since the milestone was written)

The milestone's gap table says "a text line and number keys". That is stale in the right
direction: `FlatHUD.DrawInventory` now draws every carried item as an icon slot with the
selected one raised and lit, plus a `carrying X kg · n/8` line (`FlatHUD.cs:664-739`), and
`HeldItemView` puts the selected thing in the founder's hands. Storage needs no window —
capacity is **8 items, one list, all always visible** (`GameState.InventoryCapacity`,
`GameState.cs:351-352`: "before any container exists").

What no surface shows is **condition** — and condition is this game's entire materials thesis.
`ItemState` (`GameState.cs:8-64`) carries per-item facts the sim already prices and the hotbar
icon cannot render: `Moisture01` (tinder selection reads it), `Edge01` ("two flakes off the same
core are not the same tool"), `ThicknessM` (fuel class — the field that exists because `SizeM`
silently carried two meanings), `Preparation` (how far food has come), `PlatformAngleDeg` ("the
founder's own history with that particular stone"). Today the founder learns a flake's edge by
trying to cut with it. Tab answers the question the milestone assigns it — *what am I carrying* —
with those facts, in numbers.

## The shape: a read-only overlay, not a mode

**The window takes no input surface at all.** This is the load-bearing design decision and the
reason I1 is small:

- **Tab toggles it.** One new registered binding (group "Carrying"), declared in `Keybinds.cs`
  per K1d's mint rule, added to `All`. Nothing else: no mouse interaction, no cursor unlock, no
  drag, no reorder, no Escape listener (Escape keeps its five owners; the pause menu simply
  draws over the window like everything else).
- **The world keeps running.** Rummaging while the night comes is the game working. No pause, no
  input suspension.
- **Selection stays where it lives.** Wheel and 1–8 keep doing exactly what `Interactor.cs:64-84`
  says; the window is a live view (subscribe `Inventory.OnChanged`, `Inventory.cs:43`), so the
  selection highlight moves in it while it is open. The window never writes selection.
- **The tablet outranks it.** While `Tablet.HasTheMouse` is true the window does not draw — one
  reading surface at a time, and the predicate already has one name and a doc comment. Do not
  add a rival spelling.

Law 7 ownership table for the record: Tab in Playing → this window's toggle; Tab in any menu
or while rebinding → the menu's (untouched); every other key and the cursor → unchanged in
every state.

## What it shows, and who owns each fact

One row per carried item, reading `Inventory.Instance` (the live copy — `GameState` is the
mirror that `CaptureLiveState` refreshes; board finding (G) is why this sentence exists):

- **Name** — `ItemState.DisplayName`, which already owns naming. Never re-derive.
- **Mass** — `MassKg`, in kg. **Size** — `SizeM`, and `ThicknessM` when nonzero.
- **Damp** — `Moisture01`, shown as a percentage.
- **Edge** — `Edge01` when nonzero. **Worked angle** — `PlatformAngleDeg` when nonzero.
- **Preparation** — the `Preparation` name when non-empty, verbatim.
- **Fuel class** — only by calling the same `Fire.ClassOf` the fire calls, with the same
  argument the fire passes. A second derivation of fuel class in the Scripts layer is law 3's
  worst case, and `ClassOf(SizeM)` vs `ClassOf(ThicknessM)` is precisely the bug this repo
  already paid for.
- **Footer** — `TotalMassKg` and `Count`/`GameState.InventoryCapacity` (read the const, never
  copy the 8), plus the armful row when `ShelterBuilder.HasArmful`: `ArmfulMaterial` at
  `ArmfulMassKg` ("one armful at a time, because there are two arms and no container yet" —
  quote the state, keep the fiction).

Numbers are printed with units, from the fields that own them, never typed as literals. Words
that make checkable claims about mechanism are not written (law 9); the window states facts and
lets the tablet keep the "why" monopoly.

## Rendering law (7) applied

`UiScale.BeginFrame()` first statement, above every early return. All sizes off the `UiScale`
ramp; style caches compare `UiScale.Generation`. **No per-frame string building**: compose the
window's text model only on `Inventory.OnChanged` and on `UiScale.Generation` change, cache it,
and render the cache — the event exists for exactly this. No file I/O, no texture/style
allocation inside `OnGUI`. Icons come from `ItemIcons.For` (already cached and generation-aware).

## Proof (laws 6 and 8)

- **Scenario:** extend `-eg-scenario controls` (or a sibling `carry` scenario if `controls` is
  crowded): `give` items with known state, press the real Tab through the seam (K1b's
  registry-derived map makes the binding drivable by id the day it is declared), then probe.
- **Probes read the window's own built text model — never `Inventory` directly.** A probe that
  reads `Inventory.TotalMassKg` and compares it to the window's source is the `tablet.atbottom`
  tautology again: it proves the copy, not the display. `inventory.open` (window visible),
  `inventory.rows` (count of composed rows), `inventory.shows` (substring over the composed
  text: assert the given flake's DisplayName, its mass rendered to the shown precision, its damp
  percentage). Sabotage each per law 8: a window that renders nothing, a row builder that drops
  the mass, a stale cache that misses `OnChanged` — each must turn its probe red before the
  probe is trusted.
- **Tab's registration is covered mechanically the day it lands:** K1d's mint test forces the
  declaration into `Keybinds.cs`; the page↔listener tests bind the controls page both ways
  (`SpelledOutKeys` already maps the word "Tab", `KeybindSourceTests.cs:127`).
- **Visual check:** screenshot with a full carry — owner's eyes or recorder frame; reading the
  code is not proof.

## Explicitly out of scope

- **Containers, stockpiles, flows** — GAME_DESIGN §41's growth path; this window is the
  pre-container view and says so.
- **Reordering, dragging, any mouse interaction** — selection semantics belong to the wheel and
  number keys; a manipulation surface would re-open the cursor-ownership question for no
  milestone value. If a real need appears it arrives as its own contract.
- **Load-cost prose** ("this slows you by…") — that is a model quote and the tablet's job; the
  window shows facts only.
- **Any new save field.** The window is a view over existing state; nothing enters
  `StateDigest`. If implementation finds it wants state, that is a contract violation to
  escalate, not a field to add.

## Acceptance

Headless floor holds (count stated as run); `CI/Scripts` clean; the scenario drives Tab through
the real seam and every probe has been sabotaged red once; screenshot of a full carry with
condition columns; the K1a-pattern doc comment on the new binding states why Tab and what it
displaced (nothing — the reservation is being spent as reserved). REVIEWER verifies the probes'
independence from `Inventory` specifically — that is this contract's own named bug shape.
