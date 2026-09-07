# Slice 2.3c — Water: read the land, find the creek

**Status:** contract, binding. Written before code.
**Phase:** 2 — The First Survivor. **Chronicle position:** Age I, step 4.

## Why this, and why now

`BodyState.Hydration01` has been ticking down since the body model was built. Nothing reads it,
nothing can refill it, and nothing happens when it empties. It is the same hole cold had before
slice 2.3a: a number that moves, cannot be acted on, and has no consequence.

The chronicle's triage is orient → insulate → fire → **water** → food, and it is in that order for
a physiological reason the game should reproduce exactly: warmth kills in hours, thirst in days,
hunger in weeks. Two of those three are now real.

## The transfer test (§3, §48 Level 4)

1. **Water is downhill, and it is where the land says it is.** Someone who can read terrain —
   follow the fall line, look for the convergence of two slopes, prefer the valley floor to the
   gully wall — must find water faster than someone wandering. Someone who searches uphill must
   fail, and be able to see afterwards why.
2. **Small creeks join into bigger ones.** Following water downstream must reliably lead to more
   water. That is not a rule; it is what a drainage network is.
3. **Ridges are dry.** A camp sited on a spur for the view is a camp with a walk to every drink.
4. **Thirst is slower than cold and faster than hunger**, and all three run at once.
5. **You cannot carry it.** Without a container, water is a place you go, not a thing you have —
   so where the shelter goes is a real decision with a real cost either way.

## Domain spec (§35)

### Where water is: drainage from the terrain itself

No hydrography dataset is used here — that is Phase 3.2. Water goes where **this terrain** sends
it, computed by standard D8 flow accumulation over the heightfield the player is walking on:

```
for each cell: steepest downhill neighbour of the eight   (D8 flow direction)
process cells from highest to lowest, pushing accumulated area downstream
accumulation(cell) = own area + sum of accumulation of cells draining into it
```

A cell carries water when its accumulated catchment exceeds a threshold, because that is what a
creek is: the point where enough hillside drains through one line to keep it wet.

| Channel | Catchment | What it is |
|---|---|---|
| pond | any hollow ≥ 0.5 m deep with ≥ 0.4 ha above it | standing water in a basin with no outlet |
| damp | ≥ 0.4 ha | a soak — wet ground, no free water |
| trickle | ≥ 2 ha | a thread of water in the leaf litter |
| creek | ≥ 12 ha | running water you can drink from |
| stream | ≥ 60 ha | a watercourse |

Thresholds are catchment areas, not distances, so they hold at any grid resolution — and they are
the same quantities a hydrologist would use. This is GAME_DESIGN §6 exactly: model the reason a
creek is where it is, do not store where the creeks are.

### Filling the pits, and what that turned up

Flow accumulation on a raw heightfield does nothing. The first run produced **zero creeks**,
because procedural terrain is covered in small closed hollows and water dies in every one. Filling
depressions is the first step of every real analysis of a real elevation model, and it is here too
(Priority-Flood: work outward from the boundary in height order, raising each hollow to the level
it would spill at).

Measuring the fill said something about the terrain worth writing down:

> pit filling raised **16.6% of cells**, mean **3.12 m**, max **18.67 m**

That is a landscape with a great many closed basins, some of them deep. Real hillsides outside
karst and arid endorheic country are almost entirely drained, so this is a **terrain-generation
fidelity gap**, and it belongs to Phase 3.2 with the rest of the hydrography: procedural terrain
that has never been eroded does not drain like terrain that has.

It is not papered over here. A hollow with no outlet **holds water**, which is what a hollow with
no outlet does, and the level it fills to before spilling — the number the drainage needed
anyway — is exactly the surface level of the pond sitting in it. So the correction that makes the
drainage work is the same calculation that says where the standing water is. At the wake point
that comes to 2.9% of the ground under water, and it is why a founder who walks downhill and finds
themselves in a bowl has not made a mistake: they have found the water.

Because accumulation is a pure function of the heightfield, and the heightfield is a pure function
of position, **the drainage network never needs saving**. It regenerates identically, like
everything else in the world.

### What thirst does

Body water is about 42 L in a 70 kg adult, and the founder loses ~2.4 L a day at rest. The
published consequences, by fraction of body water lost:

| Loss | Effect |
|---|---|
| 2% | thirst; the first measurable drop in work capacity |
| 4% | ~10% of endurance gone |
| 6% | headache, sleepiness; ~25% gone |
| 10% | confusion; thermoregulation failing |
| 15% | usually fatal |

Two of those have teeth here, and both are things the founder feels rather than reads:

- **Work capacity** scales down with deficit, which reduces both exertion and — the one that
  matters on a cold night — the maximum heat shivering can produce. A dehydrated founder cannot
  shiver their way through the small hours.
- **Death** at 15% loss, which at 2.4 L/day is a little under three days. That is the real figure,
  and it is why water is the fourth thing on the list and not the first.

## Exact API (engine-free, `EarthGame.Sim`)

```csharp
// World/DrainageNetwork.cs
public enum Channel { None, Damp, Trickle, Creek, Stream }

public sealed class DrainageNetwork
{
    public DrainageNetwork(float[] heights, int width, int height, double cellSizeM);

    public int Width { get; }
    public int Height { get; }
    public double CellSizeM { get; }

    public double CatchmentM2(int x, int z);
    public Channel ChannelAt(int x, int z);
    public int FlowDirection(int x, int z);          // 0-7, or -1 for a sink
    public bool TryStepDownstream(int x, int z, out int nx, out int nz);

    public static double CatchmentFor(Channel channel);
}

// Body/BodyState.cs additions
public double WaterDeficit01 { get; }        // 0 fully hydrated, 1 dead
public double WorkCapacity01 { get; }        // what thirst leaves of them
public double HoursToCollapse { get; }       // at the current rate
public ThirstLevel Thirst { get; }
```

## §36 validation — pre-written, binary

| # | Test | Criterion | Referent |
|---|---|---|---|
| W1 | Water runs downhill | every flow step loses height | D8 |
| W2 | Catchments sum | a cell's catchment equals its own plus everything upstream | conservation of area |
| W3 | Valleys carry, ridges do not | on a V-shaped valley the channel is on the floor; a ridge crest is dry | terrain shape |
| W4 | Downstream is wetter | following flow, catchment never decreases | definition |
| W5 | Confluences add | two equal branches join into one carrying both | conservation |
| W6 | **Real terrain has real creeks** | on the wake-point heightfield, channels are a small fraction of cells and all of them are downhill of their neighbours | the actual world |
| W7 | Three days without water | from full to death takes 60–80 h at rest | 15% of 42 L at 2.4 L/day |
| W8 | Thirst weakens shivering | at 6% loss, maximum shivering heat is measurably down | published work-capacity curve |
| W9 | **Dehydration loses a night that hydration survives** | same shelter, same night: thirsty founder dies, watered one lives | body model + climate |
| W10 | Drinking restores | a litre moves the deficit by 1/42 and nothing else | body water |

## Out of scope

Containers, boiling, water-borne disease, salt, rivers from real hydrography (Phase 3.2), rain,
snowmelt, springs and aquifers.
