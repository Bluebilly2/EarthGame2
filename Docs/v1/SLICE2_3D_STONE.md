# Slice 2.3d — Stone: the hammer, and why it is not the knife

**Status:** shipped. Contract and measurements together.
**Phase:** 2 — The First Survivor.

## Why

The food slice ends with the founder standing over a bracken rhizome they can roast and cannot
pound, holding a little over half the calories in it and able to feel the rest going past. Pounding
needs a stone. Which stone is the question this slice exists to answer, and it has to be answered by
the rock rather than by a recipe.

## The transfer test (§3)

**Nothing anywhere says what a stone is for.** `StoneType` holds density, Mohs hardness, grain size,
fracture toughness and how conchoidally it breaks (GAME_DESIGN §8). Everything a stone can do is
derived from those five numbers, so a founder who knows that a tough fine-grained cobble makes a
hammer and a glassy nodule makes a blade is right here — and was never told.

## Domain spec (§35)

`PoundingQuality` is the opposite requirement to `EdgeQuality`, out of the same numbers:

- **tough**, not hard — a brittle stone spends the blow breaking itself
- **not conchoidal** — anything that flakes readily flakes instead of transmitting the blow, and
  puts shards of itself through the food
- **fine-grained** — coarse crystalline rock comes apart along its own grain boundaries
- **heavy** — mass does the work

## What it came out as

| stone | hammer | edge | knap | abrasive |
|---|---|---|---|---|
| basalt | **0.55** | 0.13 | 0.18 | no |
| granite | 0.44 | 0.01 | 0.06 | yes |
| quartzite | 0.35 | 0.25 | 0.29 | no |
| sandstone | 0.28 | 0.03 | 0.07 | **yes** |
| chert | 0.08 | 0.81 | 0.66 | no |
| flint | 0.05 | **0.95** | 0.77 | no |
| shale | 0.03 | 0.05 | 0.14 | no |
| obsidian | 0.02 | 0.75 | 0.86 | no |

**The best blade in this country is one of the worst hammers in it.** That inversion is the whole
proof that the properties are doing the work: flint at 0.95 for an edge and 0.05 for a hammer,
basalt the other way round, and nobody wrote either down.

**Granite lost to basalt on grain.** The first version had granite winning — it is tougher than
basalt and less conchoidal, so on those two terms it should. It is also three-millimetre crystals
that spall apart under repeated blows, and a basalt cobble out of a creek is the classic hammerstone
for exactly that reason. Adding a grain-coherence term put them the right way round.

**Shale is good for nothing**, and the test that claimed every stone had a use was wrong rather than
the model. What the catalogue has to do is discriminate: something excels at each job, no stone wins
two, and at least one is a disappointment — or picking up rocks is not a decision.

## In the world

The founder pounds with whatever they are carrying. The heaviest usable stone is chosen for them,
and a poor one does not fail — it takes far longer, which is them feeling the difference between the
rock they have and the rock they want. Below 0.35 kg it is a pebble; below 0.12 quality it shatters.

Verified in the player: handed a flint flake, a basalt cobble and a shale plate, the founder reaches
for the basalt.

## Out of scope

Knapping as an action, hafting, ground-edge axes, and the grinding stone that grass seed needs.
