# Slice 2.6 — Food: the hungry season

**Status:** contract, binding. Written before code.
**Phase:** 2 — The First Survivor.

## Why

The founder has warmth, water and sleep. Hunger is already ticking — `Energy01` falls at about
1700 kcal a day and there has never been anything to do about it. It is the last of the four needs
and the only one with nothing behind it.

It is also the need that makes the world matter. Warmth is solved with debris that is everywhere.
Water is solved at the creek. **Food is solved only by knowing what is growing in front of you**,
and that is the first time the ecosystem stops being scenery.

## The transfer test (§3)

1. **Food is the plants, not a resource node.** Everything edible is a part of one of the species
   `E4` already places by soil, water and light. Find the ground the plant likes and you find the
   food; there is no separate spawn table to learn.
2. **August is the hungry month.** The founder wakes in late winter, which in this country is the
   bottom of the year: no fruit, no seed, no eggs. What is left is underground — roots and tubers,
   which is exactly why the digging stick is the oldest tool there is.
3. **You cannot eat the staple until you can cook.** Bracken rhizome is the abundant food here and
   it is starch locked in fibre, mildly poisonous raw. Roasting and pounding are not a recipe the
   game teaches; they are what the rhizome physically requires.
4. **Calories are work.** A day's energy is kilograms of root out of cold ground. The founder can
   dig all day and lose ground, and that is not a difficulty setting, it is late winter.

## Domain spec (§35)

### What is edible, and when

Nine species, each contributing the parts it actually has. Availability is a curve over the year,
not a season flag — a tuber fattens and is spent.

| Plant | Part | Best | Late Aug | kcal/kg | Processing |
|---|---|---|---|---|---|
| yam daisy | tuber | Jun–Oct | **peak** | 1050 | roast (raw is edible, worse) |
| bracken | rhizome | year round | **good** | 700 | roast **and** pound |
| grass tree | leaf base / heart | year round | fair | 400 | none |
| silver wattle | gum | Nov–Mar | poor | 1200 | none |
| lomandra | leaf base | year round | poor | 180 | none |
| tussock | seed | Dec–Feb | none | 3400 | winnow, grind |
| she-oak | young cone | Sep–Dec | none | 150 | none |
| ribbon gum | manna | Oct–Feb | none | 3600 | none |
| stringybark | manna | Oct–Feb | none | 3600 | none |

The founder wakes with two foods worth walking for and seven that are out of season. That is the
correct picture of 25 August at 34.5°S, and it is the slice's whole argument.

### Yam daisy

`Microseris walteri` joins the eight as a ninth species: a small forb of open grassy woodland on
moderate soils, shade-intolerant, which is why it lives in the gaps between the trees. It was the
staple root vegetable of southeastern Australia and it is at its fattest now.

### Processing

Each part declares what it needs. Eating it short of that is allowed and costs:

| | Calories | Harm |
|---|---|---|
| as required | 100% | none |
| under-processed | 25–60% | 0 to 0.4 |
| raw, when it must be cooked | 25% | 0.4 |

Bracken raw is the case that matters: a quarter of the calories and it makes the founder ill, which
is thiaminase and ptaquiloside doing what they do. Fire is not a convenience here — it is what turns
the only abundant food in the landscape into food.

### The arithmetic the founder is up against

Resting costs 1700 kcal a day; digging is work, so a real day is nearer 2600. Bracken rhizome runs
about 700 kcal/kg processed, so **a day is roughly 3.7 kg of rhizome** out of cold ground with a
stick — several hours, and only where bracken grows. This is not tuned. It is what the numbers say.

## Exact API

```csharp
public enum FoodPart { Tuber, Rhizome, LeafBase, Gum, Seed, Cone, Manna }
public enum Preparation { Raw, Roasted, Pounded, RoastedAndPounded, Ground }

public sealed class EdiblePart
{
    PlantSpecies Species; FoodPart Part; string DisplayName;
    double KcalPerKg;                       // fully prepared
    double YieldKg;                         // one plant, at full vigour
    double GatherMinutes;                   // at full working capacity
    double AvailabilityAt(int dayOfYear);   // 0-1
    Preparation Requires;
    double YieldFraction(Preparation had);  // calories actually released
    double HarmFrom(Preparation had);       // 0-1 illness
}

public static class Forage
{
    IReadOnlyList<EdiblePart> From(PlantSpecies species);
    double HarvestKg(EdiblePart part, in PlantSite site, int dayOfYear, double roll);
    double MinutesToGather(EdiblePart part, double workCapacity01);
}
```

## §36 validation

| # | Test | Criterion |
|---|---|---|
| F1 | **August is lean** | on day 237 no more than three parts are above 0.3 availability |
| F2 | **and summer is not** | on day 17 at least five are, and total kcal available is far higher |
| F3 | **Food follows the ecology** | a site the yam daisy scores 0 on yields no yam daisy |
| F4 | **Raw bracken is a bad idea** | raw yields ≤ 30% of prepared and does measurable harm |
| F5 | **Roasting alone is not enough** | roasted-only bracken sits between raw and pounded |
| F6 | **A day's calories is a day's work** | 2600 kcal of bracken takes over three hours to gather |
| F7 | Nothing is free | every part has a positive gather time and a finite yield |
| F8 | Yield follows vigour | the same species yields more on ground that suits it |

## What it came out as

Measured on the real landscape, 3000 spots across 1.6 km on 25 August:

| | share of ground | per plant |
|---|---|---|
| nothing edible | **53%** | |
| grass tree heart | 38.7% | 22 g — about 9 kcal, a mouthful |
| bracken rhizome | 4.6% | 93 g — 65 kcal prepared |
| yam daisy tuber | 2.4% | 18 g — 19 kcal |
| lomandra leaf base | 0.4% | 19 g |

Half the country has nothing on it, a third has a snack, and **the food worth digging is on
seven per cent of the ground**. Living on bracken alone works out at 4.8 hours of digging and
processing a day. Nobody was told any of that; it is what falls out of where the plants grow.

### Amendments after measurement

**Processing time was missing entirely from the first model**, and it changed the shape of the
slice. Without it a day's food came to 2.75 hours and bracken was the best return in the country;
with it, roasting and pounding four kilograms is nearly two hours on its own and the grass seed
that is the densest food here is worthless without a grinding stone. `ProcessMinutesPerKg` is now
on every part, and bracken's dig time went from 11 minutes to 20 — 300 g of rhizome out of cold
ground with a stick is not a ten-minute job.

**F1 and F2 were rewritten around what the table actually says.** The prose claimed late August was
lean in everything; the numbers say it is lean in *variety* — no seed, no gum, no manna — while the
yam daisy is at its annual peak and is the best return in the country. That is a better story and
it is the true one: the founder wakes into a landscape with one good food in it and has to know
where it grows.

**Hunger had no consequence until this slice gave it one.** `Energy01` fed nothing but shivering
capacity, so a founder could starve for a month and work at full speed. `Strength01` now winds down
from about a fortnight in, and it multiplies work capacity alongside thirst and exhaustion — which
makes a bad forage compound rather than merely cost a day.

## Out of scope

Animals and anything that moves (E5), cooking as a station, spoilage, water content, nutrition
beyond calories, and the tablet identifying plants for the founder — the last belongs with the
oracle.
