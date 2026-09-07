# Slice E4 — Plants: communities, not a biome stamp

**Status:** contract, binding. Written before code. Implements `ECOSYSTEM.md` link 5.

## What it is

Species with real tolerances, competing for real sites. What grows on a given square metre is
decided by what that square metre *is* — how wet, how deep the soil, how steep, which way it
faces, how exposed — and by what else is already growing there.

There is no biome. There is no lookup that says "Southern Highlands → eucalypt". There is a list
of plants, each with the conditions it can stand, and an answer that falls out of comparing them.

## Why it is the next link

The forest currently has exactly one plant in it. Trees thin out on the spurs and thicken in the
gullies, which is real as far as it goes — but a real hillside does not thin the *same* tree, it
*changes species*: she-oak on the dry exposed crest, stringybark through the mid slope, ribbon gum
and wattle down where the water gathers, bracken in the shade under them. That zonation is the
single most legible thing about country, and it is the thing a person who can read country is
reading.

It also closes the honesty gap the art direction opened. The ground now changes colour with the
soil. If the plants standing on it do not change with the same soil, the colour is decoration.

## The transfer test (§3, §48 Level 4)

1. **Walk down a slope and the vegetation changes, in the right order.** Dry-country species on the
   crest, wet-country species on the floor, and the change is gradual rather than a line.
2. **Aspect matters.** In the southern hemisphere a north-facing slope takes more sun and is drier;
   it should carry drier-country plants than the south-facing slope across the same gully.
3. **Poor ground grows poor plants**, not fewer rich ones. Thin soil on a ridge is she-oak and
   grass tree country — not a sparse stand of gums.
4. **Shade is a resource.** Bracken grows under a canopy and not in the open; the understory follows
   the overstory.
5. **Nothing grows where nothing can.** Bare rock, water, and slopes past the angle of repose carry
   nothing, and that is the same test the soil model already applies.

## Domain spec (§35)

### A species is its tolerances (§8)

Nothing records where a species lives. It records what it can stand, and where it lives falls out.

| Property | Meaning |
|---|---|
| `MoistureOptimum` / `MoistureBreadth` | where on the wet-dry gradient it does best, and how far either side it persists |
| `MinSoilDepthM` | how much soil it needs to root in |
| `MaxSlope` | how steep a face it will hold on |
| `ShadeTolerance` | 0 needs full sun, 1 thrives under a canopy |
| `ExposureTolerance` | 0 needs shelter, 1 stands on an open crest |
| `Form` | Tree, SmallTree, Shrub, Herb, Grass — decides what it does to everything under it |
| `HeightRange` | how big it gets, which is also how much it shades |

Suitability at a site is the product of how well each condition is met, so **one condition that
cannot be met rules the species out** regardless of the others — which is how tolerance actually
works. A gum that needs a metre of soil does not grow in ten centimetres of it because the
moisture happens to be perfect.

```
moisture  = exp( -((wetness - optimum) / breadth)^2 )     a tolerance curve, not a band
soil      = clamp01((depth - min) / (min + 0.2))
slope     = clamp01((maxSlope - slope) / maxSlope)
light     = shaded ? shadeTolerance : (1 - 0.6 * (1 - exposureTolerance) * exposure)
suitability = moisture * soil * slope * light
```

### Competition

Sites are contested. The species that wins a site is drawn in proportion to `suitability^3`, so the
best-suited species dominates without ever being the only one present — which is what a real stand
looks like. Ties near the edges of ranges are what produces the gradual change from one community
to the next rather than a line on the ground.

The overstory is resolved first, because it changes the site for everything under it: where a tree
wins, the ground beneath becomes shaded, and the understory is then resolved against a site with
`shaded = true`. That single ordering is what makes bracken grow under gums and not beside them.

### The species

Real plants of the dry sclerophyll country around the wake point, with the chronicle's own list
(stringybark, grass tree, lomandra) among them.

| Species | Form | Moisture | Soil | Notes |
|---|---|---|---|---|
| Ribbon gum | Tree | wet (0.80) | deep | valley floors, the tallest thing here |
| Stringybark | Tree | mid (0.45) | moderate | the mid-slope dominant, and the cordage tree |
| Black she-oak | SmallTree | dry (0.15) | thin | exposed crests, poor ground, wind-hard |
| Silver wattle | SmallTree | wet-mid (0.65) | moderate | gully margins and disturbed ground, fast |
| Grass tree | Shrub | dry (0.25) | thin | sandy poor soil, full sun, the fire-drill plant |
| Bracken | Herb | wet-mid (0.70) | moderate | shade-demanding: understory only |
| Lomandra | Herb | wet (0.85) | any | creek edges, and the fibre plant |
| Tussock | Grass | mid (0.40) | thin | everywhere the trees are not |

## Exact API (engine-free, `EarthGame.Sim`)

```csharp
public enum PlantForm { Tree, SmallTree, Shrub, Herb, Grass }

public sealed class PlantSpecies
{
    public string Name { get; }
    public string DisplayName { get; }
    public PlantForm Form { get; }
    public double MoistureOptimum { get; }
    public double MoistureBreadth { get; }
    public double MinSoilDepthM { get; }
    public double MaxSlope { get; }
    public double ShadeTolerance { get; }
    public double ExposureTolerance { get; }
    public double MinHeightM { get; }
    public double MaxHeightM { get; }

    public double Suitability(in PlantSite site);

    public static readonly PlantSpecies RibbonGum, Stringybark, SheOak, SilverWattle,
                                        GrassTree, Bracken, Lomandra, Tussock;
    public static IReadOnlyList<PlantSpecies> All { get; }
    public static PlantSpecies ByName(string name);
}

public struct PlantSite
{
    public double Wetness;      // 0 dry spur .. 1 soak
    public double SoilDepthM;
    public double Slope;        // rise over run
    public double Exposure;     // 0 sheltered .. 1 open crest
    public bool Shaded;         // is there a canopy over it
}

public static class PlantCommunity
{
    /// <summary>The overstory species for a site, or null where nothing tall can stand.</summary>
    public static PlantSpecies Canopy(in PlantSite site, double roll);

    /// <summary>The understory species, resolved against the shade the canopy casts.</summary>
    public static PlantSpecies Understory(in PlantSite site, double roll);

    public static double TotalSuitability(in PlantSite site, PlantForm form);
}
```

## §36 validation — pre-written, binary

| # | Test | Criterion |
|---|---|---|
| P1 | **Zonation runs the right way** | walking a moisture gradient, the dominant canopy goes she-oak → stringybark → wattle/ribbon gum, and never backwards |
| P2 | **Tolerance is a veto** | a species below its soil minimum scores zero however perfect the moisture |
| P3 | **Shade sorts the understory** | bracken beats tussock under a canopy and loses to it in the open |
| P4 | **Exposure sorts the crest** | on an exposed thin-soil crest, she-oak and grass tree outscore both gums |
| P5 | **Nothing grows on nothing** | bare rock, past-repose slope, and no soil all return no canopy |
| P6 | Deterministic | the same site and roll give the same species |
| P7 | **Communities, not monocultures** | over a hundred sites of the same type, the dominant takes 40–85% — neither a lottery nor a stamp |
| P8 | Every species wins somewhere | across the real terrain, no species in the list is dead weight |

## Out of scope

Growth over time, seasons, fire history, seed dispersal, succession after disturbance, edible and
medicinal properties (those arrive when foraging does), and animals. This slice answers "what is
standing here", not "how it got here" or "what it is for".
