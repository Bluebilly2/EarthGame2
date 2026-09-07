# Contract W1d — the world wets: a pass that walks the world instead of the save

**Owner:** DEV 2 (sim track). **Opened:** 2026-09-02, on FABLE's ruling (board 170415) accepting
DEV 2's scope verdict (board 170232) after REVIEWER 2's WITHHELD (board 163414).

W1c shipped a drying pass and a caller for a law that had none. Two independent reviews then found
that the caller calls a snapshot. This contract replaces the pass's signature so that it walks the
live world, and pays the five holders W1c did not reach.

## The finding, stated once

`Weathering.StepAll(GameState, …)` takes the **save format** as if it were the world. `GameState` is
a serialisation snapshot reconciled with the live world only inside `GameSession.CaptureLiveState`,
whose one ordinary-play caller is `SaveNow`. So the pass is typed on a copy, and it touches the
object the game reads only where that copy happens to share a reference.

The consequence in the founder's terms, which is the sentence that goes to the owner:

> **In an unsaved run, nothing the founder carries, holds, builds or drops gets wet or dry at all —
> and saving the game is a drying mechanic.**

Six holders, measured rather than reasoned:

| Holder | Today | Why |
|---|---|---|
| carried items | **broken, works by accident** | Three regimes: never captured (stepped **zero** times — a fresh run until the first manual save, and anything picked, harvested, knapped, corded or foraged since); captured and still carried (**genuinely dries** — the only working case, and the minority one); captured then removed (a ghost, stepped forever). |
| dropped items | **right physics, wrong object** | `PlacedItem`'s constructor copies `Moisture01` by value. Worse: if the item was in the last capture, `state.Inventory[i]` **is** `WorldObject.Item`, so the stick lying in the rain is stepped at *the founder's* point. It is not frozen — it is **teleported to the founder's sky**. Meanwhile the `PlacedItem` copy beside it is stepped correctly and nothing reads it. |
| shelter layers | **no-op, and the instrument lies** | `Layer` is a private struct in a private list; `Save()` mints fresh `ShelterLayerState`; `CaptureLiveState` reallocates `State.Shelters` from the live sites immediately before every disk write. The stepped values are unreachable in-session and destroyed on the way to the file — and still counted into `MoistureStepped`. |
| the armful | **no-op twice over** | Before any capture `HasArmful` is false and the branch is skipped. After one it steps a double that `ShelterBuilder.SaveInto` overwrites from the live floats. |
| ground litter | **nothing to reach** | `StrippedGround` has no wetness field at all. `GroundCover.MoistureAt` mints litter moisture from hour-of-day, curvature and canopy, and **reads no rain and no humidity** — a third independent owner of "how wet is this". |
| fire fuel bed | **not in the pass** | `Fire.Moisture01` round-trips through `FireState` and is never stepped. Rain cannot wet a lit fire's fuel. |

## Rulings made, and by whom

1. **Exposure is per thing, not per kind of place** — FABLE, 2026-09-02, already law in `19a5901`.
   W1d applies it to the **enumeration** as well as the target, which is where it was missing.
2. **The fire's fuel bed is FIRE's fact, and enters the census as a holder** — FABLE, board 170415.
   One law per tick: **while lit, the flame owns it and weathering steps zero; while unlit it is an
   ordinary pile.** Drying things *near* a fire is the radiant term and stays behind this contract.
3. **`GroundCover`'s third owner joins this same census, in this contract** — FABLE, board 170415.
   Not deferred.
4. **A save record is not the live object** — STANDARDS S8, minted from this finding. A pass that
   walks persistence state is testing the save, not the world.

## The shape

The caller hands Sim a census of **live** holders, each with **its own point**.

```csharp
// Sim/World/MoistureCensus.cs — new
public sealed class MoistureCensus
{
    public bool Add(ItemState item, Double3 at);        // carried, dropped, racked — one call
    public bool Add(DebrisShelter shelter, Double3 at);
    public bool Add(Fire fire, Double3 at);
    public bool Add(GroundPatch patch, Double3 at);
    public int Count { get; }        // enrolled
    public int Duplicates { get; }   // Adds refused — the reference was already present
    public void Clear();             // reused per tick; no per-frame allocation
}

// Sim/World/Weathering.cs
public static int StepAll(MoistureCensus world, in MoistureField field, double seconds);
```

`GameState` leaves the drying pass **entirely**, and becomes what it should always have been: what
`CaptureLiveState` writes *out of* the live world, never an input to physics.

Sky view is already per point (`MoistureField.SkyViewAt`). Nothing about the census needs
UnityEngine: `ItemState`, `DebrisShelter` and `Fire` are all Sim types.

## The work

1. **`MoistureCensus`** — reference-identity set plus parallel `(holder, point)` lists. `Add` returns
   false on a repeat. Roughly eighty lines.
2. **`Weathering.StepAll`** — takes the census. The four `state.*` loops go, with
   `ThicknessMmOf(PlacedItem)`. The class doc's `GameState.Placed` paragraph describes a design that
   will no longer exist and must be rewritten rather than trimmed.
3. **`DebrisShelter`** — storage-only layer accessors: `LayerCount`, `MoistureOfLayer(int)`,
   `ParticleThicknessMmOfLayer(int)`, `SetLayerMoisture(int, double)`, bedding then walls on one flat
   index. **No arithmetic** — `Weathering` keeps every bit of it, so no second stepper appears.
4. **The armful becomes one `ItemState`** on `ShelterBuilder`, rather than three floats. Then it
   enrols like everything else and `StepAll`'s `HasArmful` special case is **deleted rather than
   fixed**.
5. **The fire** — enrolled always, stepped only when `!IsBurning`, per ruling 2. A lit fire is
   enrolled and not stepped, so `Count` and the stepped total legitimately differ; the instrument
   must say which it is reporting.
6. **Ground litter gains a moisture with one owner** — a per-patch wetness that the census steps and
   `GroundCover.MoistureAt` **reads** instead of minting. Additive save field with an initialiser, into
   `StateDigest.Of` in the same commit (save law). `GroundCover` stops being a second opinion about
   the sky.
7. **A dropped-object registry** on `ScatterField`, maintained on spawn and destroy. None exists
   today. It is what lets the census reach dropped things *and* lets `CaptureLiveState` rebuild
   `Placed`.
8. **`CaptureLiveState` rebuilds `State.Placed`** from that registry, as it already does for fires,
   shelters and inventory. This makes `Placed` a save record instead of a parallel fact — **and
   deletes the 4 m nearest-match bug in `RemoveNearestPlaced` in the same breath**, because records
   are regenerated rather than surgically edited. That bug cannot be fixed any other way while
   records are edited by proximity.
9. **`Interactor.cs:120` is deleted** — it overwrites a weathered item's moisture with a number that
   has never heard of rain. `MoistureOnTheGround` survives only for the `obj.Item == null` branch:
   scenery the world grew, which has never been weathered and needs a first value.
10. **Hand-written `state.Placed.Add(...)`** goes from `Knapper`, `WorkshopSite` and `DevTool`.
    Capture owns it.
11. **`Survival`** builds the census from the live world each tick and passes it. Still exactly one
    call site. Publishes `MoistureDuplicates` beside `MoistureStepped`.

## Order of proof (the B1 pattern, demanded not suggested)

Sim first and headless, then the seam, then the world:

1. `MoistureCensus` + `Weathering` + `DebrisShelter` accessors — all Sim, all testable headless.
2. `Survival` + the `ScatterField` registry.
3. `Interactor` / `GameSession` / `ShelterBuilder` / `Knapper` / `WorkshopSite` / `DevTool`.
4. The `weather` scenario.

Each step commits atomically. Steps 2–4 need Scripts files claimed on the board first.

## Acceptance

Six musts, set by DEV 2 **before** the design was seen (board 163908) and unchanged by it:

1. **A shelter in a running world gets wet and dries** — the live `DebrisShelter` whose layers
   `BeddingResistance` bills the night against, not a `ShelterState` built by a test.
2. **The disconnect sabotage goes red.** Cut the live path and a shelter in a running world stays dry
   through rain.
3. **A dropped stick has one moisture, not two.**
4. **Exactly-once is true rather than assumed.** No-double-step becomes **structural** — reference
   identity, checked every tick, with `Duplicates` published as the proof, and a scenario asserting
   it is zero. **No-miss cannot be made structural and this contract does not claim it is**: no
   signature can assert that the caller enumerated the world. The tripwire instead is one line —
   with a non-empty pack, `MoistureStepped >= Inventory.Count`. Red today.
5. **Exactly one stepper survives, unweakened** — and gains a third clause: no file outside
   `Weathering` may **assign** `.Moisture01` except a named, closed list of minting sites. A second
   *writer* is the same bug as a second stepper in different clothes.
6. **`MoistureStepped` stops lying.** It counts real work, or it goes.

And the rule that catches this class, for the tests themselves:

> **A moisture test names a reader — `GroundConductanceWm2K`, `BestTinder`, `WallClo` — never a save
> field.** Today's shelter test asserts on storage. The reader-side assertion *fails on shipped
> code*, because shipped code cannot move a live shelter's conductance by any path.

The scenario that would have caught the whole thing, and which no headless test can:
`-eg-scenario weather` — **new game, never saved**, harvest bark fibre off wet ground, stand in dry
air six in-game hours, assert the item's moisture fell below `Fire.MaxTinderMoisture` **and**
`FireBuilder.FindTinder()` is not null. It fails on frame one today.

## Out of scope

- **The radiant term** — a fire drying what is *near* it. Ruled to sit behind this contract. It is
  the thing a drying rack wants, and it is a debt of DEV 2's, disclosed four times now.
- **`DroppedBody.Track` on restored items** — the two-argument `SpawnDropped` overload never calls
  it, so items restored from a save do not write back their resting place. Step 8 makes this
  harmless rather than fixed; unifying the overloads is its own small change.
- **Whether `Fire.Moisture01` should also fall while lit** — ruling 2 says the flame owns it while
  burning. Any evaporation model at the fire belongs to the combustion side, not here.

## What this contract must say about its own predecessor

`b5bd205`'s message claims a drying pass. When W1d lands, the record is corrected in the open rather
than quietly improved: until this contract, an unsaved run wets and dries nothing at all, and saving
was the mechanic. Four commits stand WITHHELD behind that — `b5bd205`, `578b65e`, `19a5901`,
`e0b840e` — and `e0b840e`'s exactly-once test is tautological on the old signature, which is why it
waits with them rather than riding ahead.
