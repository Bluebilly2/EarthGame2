# Slice 2.3b — Fire: heat you made yourself

**Status:** contract, binding. Written before code, per `ROADMAP.md` → *Delivery mechanism*.
**Phase:** 2 — The First Survivor. **Chronicle position:** Age I, step 3.

## Why this, and why now

`SLICE2_3_INSULATION.md` made the night survivable. It did not make it *warm*, and it did not
give the founder anything they can carry forward: a debris shelter is a hole you lie in until
morning. Fire is the first thing the founder makes that does work for them — heat on demand, at a
place they choose, from fuel they gathered.

The chronicle is explicit about the order and the method:

> **Fire.** Hand-drill friction fire, with the tablet coaching wood selection and form — dead
> grass-tree flower stalk as the drill. Triple the firewood that seems necessary; a winter night
> is fourteen hours long. If fire fails, the fallback is burrowing into the debris pile like a
> compost heap.

That last sentence is why this slice comes *after* insulation and not before: the fallback has to
exist before the thing it is a fallback from. Cordage — and with it the bow drill that "retires
the brutal hand-drill forever" — is Age II and belongs to a later slice. **The hand drill is the
only ignition method here, and it is supposed to be hard.**

## The transfer test (§3, §48 Level 4)

Someone who has actually made friction fire should succeed on real technique, and someone who has
not should fail for reasons they can look up afterwards. All six must be true, and none may be
stated by the UI as a rule:

1. **Wet wood will never light.** Not slowly — never. Above roughly 15% moisture the heat goes
   into boiling water out of the contact patch and the temperature stops climbing.
2. **Species matters more than effort.** A soft, dry, straight grass-tree stalk on a soft hearth
   beats furious work with a hard eucalypt spindle. Someone who knows why softwoods are used can
   act on it; someone who grabs the nearest stick cannot brute-force past it.
3. **You cannot stop.** Heat leaks out of the contact patch in seconds. Pausing to rest loses
   most of what was gained, so the run has to be continuous — and it costs real energy.
4. **An ember is not a fire.** The coal has to reach tinder that is fine and dry enough to catch,
   and the tinder has to reach fuel that is small enough to light from it.
5. **Triple the firewood.** A fire that is not fed goes out. Fourteen hours of winter night needs
   an amount of wood that is genuinely surprising, and gathering it is the afternoon's second job.
6. **Fire is directional.** It warms the side of you that faces it. It does not replace bedding
   and it does not replace walls; it is a third thing, and the three together are what comfort is.

## Domain spec (§35) — the physics

### Wood is properties (§8)

| Property | Meaning | Unit |
|---|---|---|
| `DensityKgM3` | oven-dry density | kg/m³ |
| `IgnitionC` | temperature at which its dust forms a self-sustaining coal | °C |
| `FrictionCoefficient` | wood on wood, dry | — |
| `AbrasionRate` | how readily it gives up dust under load | — |
| `EnergyMJPerKg` | heat of combustion, dry | MJ/kg |

| Wood | ρ (kg/m³) | ignition (°C) | μ | abrasion | MJ/kg |
|---|---|---|---|---|---|
| Grass-tree flower stalk | 190 | 380 | 0.48 | 1.00 | 17.5 |
| Dead softwood branch | 380 | 400 | 0.42 | 0.62 | 18.0 |
| Eucalypt deadwood | 780 | 430 | 0.32 | 0.22 | 19.2 |
| Stringybark fibre | 240 | 350 | — | — | 17.0 |

Grass-tree (*Xanthorrhoea*) flower stalk is the canonical Australian hand-drill spindle for real
reasons that are all in that row: very low density, high friction, and it sheds dust freely.
Eucalypt is the opposite in every column, which is why the obvious branch on the ground fails.

### Ignition — a heat balance at the contact patch

Not a progress bar. Mechanical work goes in, heat leaks out, and the patch either climbs to the
ignition point or settles below it forever:

```
v      = pi * spindleDiameter * rpm / 60            // rubbing speed, m/s
P_in   = mu * downforce * v                          // W into a patch of ~50 mm²
P_wet  = evaporationLoss(moisture) * P_in            // water boiled out of the patch
P_loss = conductance * (T - T_air)                   // into the wood and the air
dT/dt  = (P_in - P_wet - P_loss) / heatCapacity
```

`P_wet` is why point 1 is absolute rather than gradual: water leaving the patch at 100 °C
consumes 2.26 MJ per kilogram, and above a threshold moisture it swallows the entire input. The
patch then sits at a plateau below ignition no matter how long the founder works.

Dust accumulates in proportion to `AbrasionRate * P_in`. A coal needs **both** the temperature and
enough dust to sustain itself, so a hard dense wood can reach temperature and still never produce
an ember — the failure that sends people back to the species table.

Stopping is punished by the same equation running with `P_in = 0`: the patch sheds heat in
seconds, so an interrupted run is very nearly a lost run.

### Burning

A fire is a stack of fuel in size classes, because size is what decides whether something catches:

| Class | Diameter | Catches from | Burn rate |
|---|---|---|---|
| Tinder | fibre | an ember | seconds |
| Kindling | under 10 mm | tinder | minutes |
| Fuel | 10–60 mm | kindling | tens of minutes |

Nothing is labelled "kindling" anywhere. A branch's class follows from its diameter, which follows
from the object in the world (§8).

```
massBurnRate = f(fireSize, fuelMoisture)            kg/s
P_total      = massBurnRate * EnergyMJPerKg * 1e6   W
P_radiant    = 0.35 * P_total                       // the rest goes up as hot gas
```

A campfire burning ~1.5 kg/h of dry wood at 19 MJ/kg is about 8 kW total and **2.8 kW radiant**.
Over a fourteen-hour night that is **21 kg of wood** — transfer test 5, arrived at by arithmetic.

### What the body feels

Radiation, not air temperature. The fire does not warm the clearing, it warms the side of the
founder that faces it:

```
irradiance = P_radiant / (4 * pi * d²)              W/m²
gain       = irradiance * FacingArea                // ~0.4 of skin area, one side
```

At 1.2 m from a 2.8 kW fire that is **about 120 W** onto the facing side — comparable to a hard
walk, and it arrives without moving. It is added to `BodyState` as a heat *input*, so it composes
with insulation instead of replacing it. A founder with a fire and no bedding still loses the
night to the ground.

## Exact API (engine-free, `EarthGame.Sim`)

```csharp
// Materials/WoodType.cs
public sealed class WoodType
{
    public string Name { get; }
    public double DensityKgM3 { get; }
    public double IgnitionC { get; }
    public double FrictionCoefficient { get; }
    public double AbrasionRate { get; }
    public double EnergyMJPerKg { get; }

    public static readonly WoodType GrassTreeStalk, SoftwoodBranch, EucalyptDeadwood, BarkFibre;
    public static WoodType ByName(string name);
    public static IReadOnlyList<WoodType> All { get; }
}

// Fire/FrictionDrill.cs
public sealed class FrictionDrill
{
    public const double SpindleDiameterM = 0.009;
    public const double DownforceN       = 45.0;
    public const double Rpm              = 600.0;
    public const double PatchAreaM2      = 5.0e-5;

    public FrictionDrill(WoodType spindle, WoodType hearth,
                         double spindleMoisture, double hearthMoisture);

    public double TemperatureC { get; }
    public double DustGrams { get; }
    public bool HasEmber { get; }
    public bool IsSmoking { get; }

    /// <summary>One step of work. effort01 is 0 when the founder has stopped.</summary>
    public void Work(double seconds, double airTemperatureC, double effort01);

    /// <summary>What is going wrong, in the terms a person would use. Empty when it is working.</summary>
    public string Diagnosis { get; }
}

// Fire/Fire.cs
public enum FuelClass { Tinder, Kindling, Fuel }

public sealed class Fire
{
    public bool IsBurning { get; }
    public double FuelKg { get; }
    public double RadiantPowerW { get; }

    public bool Light(WoodType tinder, double tinderMassKg, double tinderMoisture);
    public void Add(WoodType wood, double massKg, double moisture01, FuelClass fuelClass);
    public void Tick(double seconds, double windMs);

    public double IrradianceAt(double distanceM);
    public double HeatToBodyW(double distanceM);

    public static FuelClass ClassOf(double diameterM);
    public string Assessment { get; }
}
```

`BodyState.Tick` gains one optional parameter, `radiantGainW`, added to metabolic production.

## §36 validation — pre-written, binary

| # | Test | Criterion | Referent |
|---|---|---|---|
| F1 | Grass-tree beats eucalypt | dry grass-tree stalk reaches an ember; dry eucalypt never does | the species table |
| F2 | **Wet wood never lights** | at 25% moisture the patch plateaus below ignition for 10 minutes of work | latent heat of vaporisation |
| F3 | Stopping loses the run | a 5 s pause costs more than half the temperature gained | patch heat capacity |
| F4 | Dust as well as heat | a wood that reaches ignition with low abrasion still yields no ember | ember needs a coal, not a hot spot |
| F5 | Fuel class from diameter | 6 mm is kindling, 40 mm is fuel, and nothing says so | §8 |
| F6 | **A night costs 21 kg** | burning from 18:00 to 07:00 consumes 18–26 kg of dry wood | 1.5 kg/h at 19 MJ/kg |
| F7 | Radiant heat is real and directional | ~120 W at 1.2 m from a 2.8 kW fire, quartering by 2.4 m | inverse square |
| F8 | **Fire does not replace bedding** | fire + walls + no bed still dies; fire + bed + walls is comfortable | body model + climate |
| F9 | Fire and shelter compose | core temperature with both is higher than with either | body model |
| F10 | An unfed fire goes out | no fuel added for 40 minutes and it is dead | burn rate |

F6, F8 and F9 run the **real `BodyState` against the real `Climate`** for a whole night at the
wake point.

## Feel checklist (owner playtest)

- The first successful ember feels earned and the failures feel diagnosable.
- Smoke appears before the ember and means something.
- Sitting by a fire in a finished shelter at 2 a.m. is the first moment the game feels safe.
- Running out of wood at 3 a.m. is a real and common way to lose.

## Out of scope, explicitly

Cordage and the bow drill (Age II), cooking, boiling, charcoal, torches, fire spread, smoke
inhalation, knapping. Bark is used here as tinder only.
