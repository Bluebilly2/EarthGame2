# A living Earth: the biosphere, for after the beta arc

**Status:** an idea written down for later development, on William's word of 2026-09-13 (CANON ruling 27). Nothing
here is designed or contracted yet. It belongs to world generation, after the beta arc.

## What William said (2026-09-13)

- "once we get past the beta arc, one of the things we will do is design a fully functioning biosphere (earth). this
  is one thing that will come under the world generation."
- "make sure this idea of a full functioning biosphere is written down for later development"
- On the beta, in the same message: "i feel as though making animals that are too obscure for the beta arc is too
  much."
- And he asked: "when a world is generated, will the said biosphere need to be 'triggered'?"

## The idea

A world whose living things work as a whole, across the Earth. Plants and animals grow, feed, breed, age, die, spread
and retreat. They answer to the climate and the seasons, to fire and flood, and to what players do. What lives where
follows from the country, and keeps following from it as the country changes. It is the ecosystem ruling of
2026-08-25 ("i want to design a real ecosystem and let it form naturally") carried from one peninsula to the planet.

## How a world's biosphere starts: already alive, not triggered

The answer given to his question on 2026-09-13, to be designed properly when the work opens:

- **No trigger.** CANON ruling 22 says the world starts in a generated state and changes from there. A new world is
  generated already alive and in balance, with forests grown and animals at the numbers the land can feed, as if it
  had been running for a long time. Nobody sets it going, and the history that led there is not simulated.
- **Perhaps a warm-up.** After the first state is generated, the world could be run forward quickly for some simulated
  years while the world is being created. Animal numbers, their food and the plant cover would then settle together
  before a player arrives. Ecosystem and climate models call this a spin-up. Whether it is needed, and for how long,
  is a question for the design.
- **Running from the first moment, in two levels of detail.** The biosphere runs on the world's clock from creation.
  Where players are, it runs as individuals; elsewhere it runs as numbers, which catch up on the time that has passed
  when a player arrives. The beta's animals already work this way: `AnimalPresence` holds a group as a function of
  the hour until a founder comes near, and M1.7 stands the group up then.

## What already points this way

- The chain rock → landform → water → soil → plants → animals (`Docs/ECOSYSTEM.md`, the ruling of 2026-08-25).
- CANON ruling 22: a generated first state that changes as time passes and players act.
- Megafauna are canon (2026-08-25).
- The beta's animals:
  - numbers from what the ground feeds (`AnimalCapacity`)
  - where each group is at any hour (`AnimalPresence`)
  - individuals only near a founder (`Docs/contracts/M1.7_ANIMALS.md`)
- World generation's own design (`Docs/WORLD_GENERATION_DESIGN.md`, Codex): an Earth atlas with ecological regions
  and pools of species, and a plausible first state that the simulation then changes.

## Questions to settle when it is designed

- **What "fully functioning" includes:** who eats whom, the cycles of water and nutrients, fire, disease, migration and
  the seasons, and whether species change over time.
- **Scope:** how many species, and which regions come first. How each is researched and held against real records, as
  ruling 21 asks for the peninsula, at the scale of the Earth.
- **The pre-human baseline:** which plants and animals would live where without people, megafauna included, and how
  the evidence reconstructs them.
- **Cost:** how the world keeps running where no player is, and how much of it a server can afford.
- **Players:** how they change it (hunting, clearing, burning, planting) and how it recovers.
- **Start-up:** whether world creation needs the warm-up above, and how long it would take.
- **The tablet:** what it can tell a player about the living world round them.

## Not in the beta arc

The beta's animals are the well-known ones the country shows first: kangaroos and a shorebird (M1.7). The rest of the
peninsula's record, and everything on this page, waits for this work.
