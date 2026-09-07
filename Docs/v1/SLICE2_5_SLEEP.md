# Slice 2.5 — Sleep: the night, passed rather than sat through

**Status:** contract, binding. Written before code.
**Phase:** 2 — The First Survivor.

## Why

A late-winter night at the wake point is about fourteen hours, and the world runs at thirty real
minutes to the day. A founder with a good shelter therefore has to **sit in the dark for a quarter
of an hour with nothing to do**, having already done the only thing the night asks of them. That is
the largest playability hole in the game, and it is entirely self-inflicted: the body model already
ticks on in-game seconds, so passing the night quickly costs nothing to simulate.

But a fast-forward button is not a mechanic. Sleep earns its place here because **not sleeping has
to cost something**, and because sleeping badly has to be survivable-but-punishing in the same way
everything else in this game is. So the slice adds the fourth need alongside warmth, water and food.

## The transfer test (§3)

1. **Sleeping does not skip the night, it lives it at speed.** Every hour of it is simulated: a
   founder who lies down in a bad shelter dies in their sleep, at the hour the physics says.
2. **The cold wakes you.** Nobody sleeps through hypothermia. Falling below a threshold wakes the
   founder, in the dark, with the night still to fix — which is the most honest failure state this
   game can produce.
3. **You get colder asleep.** Metabolic rate drops when sleeping, so the same shelter that holds at
   rest can fail once the founder stops moving. Lying down is the most dangerous thing they do.
4. **Being awake costs.** After about sixteen hours the founder starts losing capability, on the
   published sleep-deprivation curve; it compounds with thirst because both take from one body.
5. **Ground contact is the whole game.** Sleeping is lying down, so the bedding the shelter slice
   modelled is finally what it was built for.

## Domain spec (§35)

### What sleep does to the body

| Quantity | Awake | Asleep |
|---|---|---|
| Metabolic heat | basal 80 W | **0.90 × basal** — published sleeping metabolic rate |
| Ground contact | only in a shelter | **always** — you are lying down |
| Shivering | full | full; a sleeping body still shivers |
| Hours awake | accumulates | recovers at **2×** — eight hours' sleep clears sixteen awake |

### What being awake costs

Published sleep-deprivation figures, interpolated rather than fitted, the same way the dehydration
curve is:

| Hours awake | Alertness | Sense of it |
|---|---|---|
| 0–16 | 1.00 | rested |
| 20 | 0.80 | roughly a legal drink-drive limit of impairment |
| 24 | 0.65 | |
| 36 | 0.40 | |
| 48 | 0.22 | barely functional |

Alertness scales work capacity alongside thirst, because both are taking from the same person — and
it feeds shivering only partly (`0.6 + 0.4 × alertness`), because an exhausted body still shivers,
it simply does it worse.

### Waking

Sleep ends on whichever comes first:

- **Dawn** — the target, and the reason to lie down.
- **Cold** — core below 35.8 °C, or shivering above 70%. The founder wakes with the night unfinished.
- **Rested** — hours awake back to zero before dawn.
- **Choice** — the founder gets up.

### Time

Sleeping multiplies the world clock, and everything that runs on it — body, fire, weather — must
run on the same multiplied clock or they desynchronise. That means **one place decides how fast
time is moving**, and everything reads it: `GameSession.InGameSecondsThisFrame`.

Rate: **×12**, so fourteen hours of night take about seventy real seconds. Fast enough not to be a
wait, slow enough to watch the fire die and get up and do something about it.

## Exact API

```csharp
// BodyState additions
public double HoursAwake { get; }
public double Alertness01 { get; }        // 1 rested .. 0 collapsing
public FatigueLevel Fatigue { get; }
public bool NeedsSleep { get; }           // past the point where it is costing them

public void Tick(..., bool asleep = false);

// GameSession
public double TimeScale { get; set; }     // 1 awake, 12 asleep
public double InGameSecondsThisFrame { get; }   // the single source
```

## §36 validation

| # | Test | Criterion |
|---|---|---|
| S1 | **Sleep is lived, not skipped** | the same night simulated asleep at ×12 and at ×1 ends within 0.2 °C |
| S2 | **A bad shelter kills you in your sleep** | 20 kg bed + 15 kg walls survives awake, dies asleep |
| S3 | **Sleeping is colder** | heat production asleep is measurably below resting awake |
| S4 | Alertness follows the published curve | 20 h awake ≈ 0.80, 24 h ≈ 0.65, 48 h ≈ 0.22 |
| S5 | **Eight hours clears sixteen** | recovery runs at twice the rate of accumulation |
| S6 | Fatigue and thirst compound | both at once is worse than either alone |
| S7 | Waking thresholds | the cold wake-up fires before the core reaches hypothermia |
| S8 | Ground contact applies asleep | sleeping outside a shelter uses bare-ground conductance |

## Amendments after measurement

**S2 was written for 16 kg of walls and is 15.** The figure was a guess before the slice ran; a
sweep afterwards put the survives-awake/dies-asleep crossing between 14.5 and 15 kg, with 16 kg
survivable either way and 14 kg fatal either way. The window is about a kilogram of leaf litter
wide, which is the point the test exists to make: sleeping costs roughly one armful of debris.

**The slice found a hole in the body model and filled it.** Running the founder through two days
awake at a warm temperature killed them, and the reason was that *sweating cost water but shed no
heat*: `TickNeeds` scaled water loss by air temperature while the heat balance had no evaporative
term at all, so a warm day dried the founder out and cooked them at the same time. A resting body at
31 C — inside the naked thermoneutral zone — climbed to the fever clamp instead of sitting at 37.

Sweating is now the warm half of thermoregulation and is demand-driven, the same way shivering is
the cold half:

- the body sheds whatever surplus it is carrying, up to **1.5 L/h** at **2.43 MJ/L**
- water loss is **what was actually sweated**, not a function of the thermometer, so the same 30 C
  day is cheap sitting in shade and expensive carrying wood uphill
- dehydration blunts it, scaled against how far the founder is toward dying of thirst — which
  closes the loop that actually kills people in heat: less water, less sweat, hotter core, more
  sweat demanded

Four tests in `BodyTests` cover it. This was out of scope for sleep and in scope for §3: a summer
day at the wake point passes 31 C regularly, and the founder would have died of it.

## Out of scope

Dreams, sleep quality, circadian preference, naps as distinct from a night's sleep, and the tablet
telling you any of it — the last belongs with the oracle slice.
