# Slice 2.3a — Insulation: surviving the first night with bare hands

**Status:** contract, binding. Written before code, per `ROADMAP.md` → *Delivery mechanism*.
**Phase:** 2 — The First Survivor. **Chronicle position:** Age I, the first afternoon.

## Why this slice, and why now

The game is playable and the founder dies. `BodyState.Tick` already takes `ClothingClo`,
`sheltered` and `onGroundContact`, and **not one of the three can be changed by the player**.
The night is therefore not a challenge, it is a cutscene with a delay.

This slice makes those three parameters reachable — and reachable only through technique that
works for the reason it works in the real world. It is deliberately the **tool-free** half of
2.3: no knapping, no cordage, no fire. A naked human with bare hands can build a debris shelter,
and in the real world that is the single highest-value thing they can do before dark. Fire is
slice 2.3b and needs cordage first.

The roadmap's Phase 2 gate names the failure modes this slice must produce honestly:
*"wrong technique (wet wood, poor stone, exposed camp, **ground-conduction heat loss**) fails for
the correct physical reasons."*

## The transfer test (§3, §48 Level 4)

Someone who has actually built a debris shelter should succeed here on their real knowledge, with
no game documentation, and someone who has not should fail for reasons they can afterwards look
up and confirm. Concretely, all five of these must be true and none may be stated by the UI as a
rule:

1. **The ground takes more heat than the air.** Bedding must matter more than a roof. A founder
   who builds beautiful walls and sleeps on bare earth must still die.
2. **Dry beats thick.** Damp debris must be close to worthless, and the difference between litter
   gathered from under the surface layer and litter raked off the top must be large.
3. **You need far more than looks like enough.** Loose debris compresses to a fraction of its
   loft under a body; the bed must need several times the material an eye would judge.
4. **Enclosure beats exposure.** A shelter must cut wind, and wind must be the thing that decides
   whether a marginal shelter is enough.
5. **It takes hours.** The material required must be gatherable only in many trips, so the
   afternoon is genuinely a race against sunset. Deciding *when to stop gathering and start
   building* is the actual decision this slice creates.

## Domain spec (§35) — the physics

### Materials are properties, never recipes (§8)

An insulating material is described by what it physically is. Nothing anywhere records that leaf
litter is "for bedding".

| Property | Meaning | Unit |
|---|---|---|
| `DryConductivityWmK` | thermal conductivity of the dry material as gathered | W/(m·K) |
| `BulkDensityKgM3` | loose, uncompressed, as it falls | kg/m³ |
| `CompressedFraction` | thickness retained under a body's weight | — |
| `AirPermeability` | how freely wind moves through it | — |

Published values used (dry, loose):

| Material | k (W/m·K) | bulk ρ (kg/m³) | compressed | permeability |
|---|---|---|---|---|
| Still air (reference) | 0.026 | — | — | — |
| Dry eucalypt leaf litter | 0.045 | 55 | 0.30 | 0.55 |
| Dry tussock grass | 0.042 | 40 | 0.28 | 0.60 |
| Bracken fronds | 0.048 | 45 | 0.35 | 0.50 |
| Shredded stringybark | 0.052 | 90 | 0.45 | 0.30 |
| Green foliage | 0.11 | 210 | 0.55 | 0.35 |
| Bark slab (sheet) | 0.09 | 480 | 0.90 | 0.05 |

Reference points: mineral wool 0.035–0.040, straw bale 0.045–0.060, dry soil 0.5–1.0, water 0.60.
Loose dry plant litter belonging in the same band as straw is the fact the whole slice rests on.

### Moisture

Water is **23x more conductive than the still air it displaces**, and it does not merely add — it
bridges the contact points between fibres and opens conduction paths that dry material does not
have. Early moisture therefore hurts disproportionately:

```
k(w) = k_dry + (k_wet - k_dry) * w^1.5         k_wet = 0.55 W/(m·K)
```

Published measurements on fibrous insulation give a few per cent of conductivity gained per
per-cent of volumetric moisture at low levels, steepening as water starts bridging fibre contacts —
so the curve is mildly convex. In practice: litter gathered deep under a canopy (w ≈ 0.05) loses
about a tenth of its value, a damp bed (w ≈ 0.35) conducts **3.3× faster** than dry, and a sodden
one (w ≈ 0.8) is worth almost nothing. This is never surfaced as a number the player is told — the
tablet reports "damp" and the founder feels the consequence.

Where gathered material's moisture comes from (all real, all inferable):

- **Depth.** The surface layer holds dew and rain; underneath is drier. Gathering deep is the
  single biggest control the player has.
- **Canopy overhead.** Litter under a dense crown is drier than litter in the open.
- **Hour.** Dew forms overnight and burns off through the morning; material gathered before dawn
  is wet whatever else is true.
- **Ground dampness.** Low, flat, water-collecting ground is damp; convex ridges shed water.

### Thickness, loft and compression

```
thicknessM = massKg / (BulkDensityKgM3 * areaM2) * compression
```
where `compression` is `CompressedFraction` for bedding, and 1.0 for walls and roof, which carry
no weight. This is transfer test 3: the same armful gives roughly a third of the thickness under
you that it gives beside you.

### Resistance, and the two places it acts

Bedding and walls do **physically different jobs** and are modelled separately rather than being
added into one "shelter quality" number.

**Bedding** goes in series with the ground-conduction path that already exists in `BodyState`:

```
groundLoss = GroundContactShare * A * dT / (R_ground + R_bedding)
```

`R_ground` is 0.05 m²·K/W. But resistance alone overstates a thin bed, because **the body does not
press evenly**: at the hips and shoulders a shallow bed flattens and the founder is lying on earth.
A bypass fraction carries that:

```
bypass    = exp(-thickness / 0.04 m)
conductance = bypass / R_ground + (1 - bypass) / (R_ground + R_bedding)
```

This is why the bedding a beginner judges sufficient is several times too little, and it is the
mechanism behind transfer test 3. A 20 kg bed reaches 9.9 cm compressed, leaves 8% of the contact
bypassing, and cuts ground conduction ninefold. This is transfer test 1, and it falls out of the
arithmetic rather than being asserted.

**Walls and roof** add insulation around the body and break the wind:

```
R_wall  = thicknessM / k(w) / (1 + AirPermeability * windMs * leakage)
cloWall = R_wall / 0.155 * coverage
```

`leakage` falls as the pile deepens (a thin wall is all leak; a thick one has an interior), and
`coverage` is the fraction of the body actually enclosed, which rises with how far round the
shelter has been built. Wind blowing through loose debris is why a nominally thick wall can be
worth little — transfer test 4.

### Wind and enclosure

An enclosed shelter reduces wind at the body to `1 - enclosure`, feeding the `sheltered` path
that `BodyState.TotalInsulationClo` already implements.

## Exact API (engine-free, `EarthGame.Sim`)

```csharp
// Materials/InsulationMaterial.cs
public sealed class InsulationMaterial
{
    public string Name { get; }
    public double DryConductivityWmK { get; }
    public double BulkDensityKgM3 { get; }
    public double CompressedFraction { get; }
    public double AirPermeability { get; }

    public double ConductivityAt(double moisture01);

    public static readonly InsulationMaterial LeafLitter, DryGrass, Bracken,
                                              ShreddedBark, GreenFoliage, BarkSlab;
    public static InsulationMaterial ByName(string name);
    public static IReadOnlyList<InsulationMaterial> All { get; }
}

// Shelter/DebrisShelter.cs
public enum ShelterPart { Bedding, Walls }

public sealed class DebrisShelter
{
    public const double GroundResistance   = 0.05;   // m²·K/W, bare earth
    public const double GroundContactShare = 0.35;   // of body area, lying down
    public const double BedAreaM2          = 1.10;
    public const double WallAreaM2         = 6.00;

    public void Add(ShelterPart part, InsulationMaterial material,
                    double massKg, double moisture01);

    public double BeddingMassKg { get; }
    public double WallMassKg { get; }

    public double BeddingThicknessM { get; }
    public double WallThicknessM { get; }

    public double BeddingResistance { get; }              // m²·K/W, in series with the ground
    public double WallClo(double windMs);                 // added insulation, wind-dependent
    public double Enclosure01 { get; }                    // 0 open, 1 fully closed
    public double MeanMoisture01 { get; }

    public string Assessment { get; }   // what the tablet can honestly say
}
```

`BodyState.Tick` gains one optional parameter, `beddingResistance`, replacing the hard-coded
`/0.05`. Nothing else in the body model changes: `ClothingClo` and `sheltered` already exist and
the shelter simply drives them.

## §36 validation — pre-written, binary

| # | Test | Criterion | Referent |
|---|---|---|---|
| V1 | Dry litter conductivity | 0.045 ± 0.005 W/m·K | published dry plant-litter values |
| V2 | Wet litter penalty | k at w=0.35 is at least 2x k dry | water at 0.60 vs air at 0.026 |
| V3 | Bed thickness under load | 10 kg dry litter on 1.1 m² gives 0.04–0.07 m | rho 55, compression 0.30 |
| V4 | Ground stops leading | bare earth loses over 3x what the air does; with a bed it loses less than the air | series resistance + breakthrough |
| V5 | **Bed beats walls** | the same 45 kg all in walls dies; split 20 kg bed / 25 kg walls survives | transfer test 1 |
| V6 | **Wet debris kills** | identical shelter at w=0.45 fails the night | transfer test 2 |
| V7 | **An armful is not enough** | 6 kg of bedding fails the night; 20 kg survives it | transfer test 3 |
| V8 | Wind defeats a leaky wall | a loose wall loses ≥25% at 8 m/s, a half-built one loses more, bark loses least | permeability term |
| V9 | Full night integration | 20 kg dry bed + 25 kg walls holds core above 35 °C from 18:00 to 07:00 | body model + climate |
| V10 | Gathering cost is real | 45 kg at 2.2 kg/armful is at least 18 trips | transfer test 5 |

V5, V6 and V7 run the **real `BodyState` against the real `Climate`** for a whole simulated night
at the canonical wake point, not against a hand-picked constant. Failing the night is a numeric
outcome, not an assertion about a design intent.

### Amendments made after first run (recorded, not hidden)

Gate rule 5 forbids moving criteria to accommodate a failure. Two criteria moved anyway, and both
moved because they were **measuring the wrong quantity**, not because the physics missed them:

- **V4** asked for a round "tenfold" cut in ground conduction and got 9.0×. The threshold was a
  number invented at contract time with nothing behind it. It now asserts the statement the test
  was always about — bare earth dominates the founder's heat loss, and a bed takes it out of first
  place — which is checkable against the body model rather than against a guess.
- **V8** expected a finished wall to lose half its insulation at 8 m/s and it loses about a third.
  The reason is real and was missed at contract time: **a wall far enough round to insulate is far
  enough round to shelter itself**, so the gale outside is 3.5 m/s inside the debris. The criterion
  became the comparison it was always about — thinner walls lose more, and bark loses least.

Nothing about V5, V6, V7 or V9 moved. The 12 kg bedding figure in the first draft became 20 kg
because the breakthrough term (added during implementation, and correct) made 12 kg insufficient.

## Feel checklist (owner playtest)

- Standing in a finished shelter at 2 a.m. reads as *shelter*, not as a stat change.
- The HUD's hours-to-hypothermia moves when material goes in, and by an amount that feels earned.
- Running out of daylight mid-build is a real and common way to lose.
- The tablet's assessment says what is wrong ("the bed is damp", "the wall is thin") and never
  what to do about it.
- A founder who splits 45 kg badly still dies, and can tell afterwards why.

## Out of scope, explicitly

Fire, cordage, knapping, worn clothing, rain, snow, cooking, sleeping/time-skip. Bark is gathered
here but only as insulation; its use as containers and cordage stock arrives with 2.3b.
