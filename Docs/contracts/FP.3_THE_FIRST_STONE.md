# Contract FP.3 — The first stone

**Status:** drafted 2026-09-16 while the animals' looks were being made by an agent and FP.2 waited on the tree; opened
on ruling 31 (the Founder's Path comes first). Owner: Claude (Fable 5.1). The third beat of Act I: "Cobbles at the
tideline, driftwood on the sand, fibre plants at the dune's foot. Knap an edge (the wrong stone teaches as much as the
right one), cut fibre, lay cord. The country as the only toolshop; looking at a material names what it could become."

## What the path says (binding)

- **First stone.** Knap an edge; the wrong stone teaches as much as the right one. Materials over recipes: "two flakes
  off one core are different tools" is one of the five things a developer must see in the demo.
- **Fire** waits on it: the drill and hearth board are carved from found wood, and a flake is what carves.
- The build list: "first materials in the country: tool-stone cobbles, driftwood, reachable fibre" (the cobbles of the
  beach's own stone exist since M1.5b; a cobble taken up is of the stone its cell names).

## What this slice promises

1. **A blow on stone does what the stone does**, by v1's `Knapping` ported term by term: Auerbach's law sets the energy a
   blow must carry to start a fracture, rising as the square of the hammer's radius, so a small hammer does the fine
   work; the cone's angle is fixed, so all the force behind a blow buys is size; too much and the core shatters; a
   hammer whose smallest bite is more than a spent core can spare destroys it; the edge a flake carries is the stone's
   own (`StoneType.EdgeQuality`, from its mineralogy) docked for violence; every flake works the platform toward ninety
   degrees, and a dead platform crushes until the core is turned, which costs stone. Sandstone crumbles and never takes
   an edge; quartzite gives a crude edge from a controlled blow and nothing from a wild one; silcrete and rhyolite, the
   coast's own, knap well. The arm's ceiling makes a hammer's size a choice.
2. **The verb.** With a cobble in hand and another cobble within reach (lying, or held in another place), the Work key
   strikes: a tap is a light blow, a held press winds up to a hard one, and what thirst has left of the founder's work
   goes into the swing. The server commits the blow on the world it holds and answers in the words the model gives
   ("The hammer bounced. Not enough behind it."; "A clean flake, and it is sharp."); the client only asks.
3. **A flake is a thing of its own.** It lies where it fell, is picked up, carried, put down and saved like a stick, and
   is of its stone; it carries its own mass and edge as struck, so two flakes off one core are different tools. The
   core keeps its mass, its platform and its count of flakes; a spent core is gone.
4. **What a flake is for** is the next beats' (cutting fibre, carving the drill and hearth board): this slice makes the
   edge and holds what it is worth (`Knapping.CuttingMinutes`), and nothing yet uses it.

## What it needs of the item model (the wire and the save)

A thing's mass and edge live on its definition today (a cobble weighs by its stone's density; a stick is 0.3 kg). A
flake's mass is what the blow took and its edge what the stone and the blow made; a core's mass falls with every flake.
So an item gains state of its own: mass (kg, as struck), edge (0 to 1), and for a core the platform angle and the flakes
taken. It travels in `ItemComponent` (protocol 15, EntitySpawn and EntityState) and in the region file (version 3), with
the definition's mass as the default for a thing that has none of its own, so every stick and cobble written before this
reads as it was.

## Non-goals

Cutting, carving and cordage (the fibre's beat); the hammer's own wear; heat treatment; the tablet's advice on which
stone (`Knapping.BestHammer` in v1 chose the lightest stone that could start a fracture, and the tablet quoted it: both
come with the tablet); a flake's look beyond a flat shard of its stone's colour.

## How it is proved

`dotnet test`: the fracture mechanics against v1's constants restated (`KnappingTests`: the critical energy of flint at a
kilo and a half about six joules and quartzite twice; the bounce below, the flake by the excess above, the shatter, the
heavy hammer on a spent core, sandstone's crumbling, the dead platform and the turn, the arm's ceiling, the edge docked
for violence, the cutting minutes; the stones' qualities from their mineralogy); the verb over the in-memory wire
(struck, the flake spawned with its mass and edge, the core lighter, the answers' words; refused out of reach, without a
hammer, on no stone); the item state's round trip on the wire and in the file. The Unity-layer compile. Then the built
game: a `knap` scenario that spawns two cobbles, takes one up, strikes the other with a tap and a held blow, and records
the answers and the flakes; frames of a flake lying and in hand, for William's eyes.

## State of the draft (2026-09-16)

`Knapping.cs` (engine, `Materials/`) is written with `KnappingTests`, on `StoneType`'s own `EdgeQuality`, `Knappability`
and `PoundingQuality` (v1's, ported with the stones); the verb, the item state, the wire, the save, the looks and the
scenario are not begun.
