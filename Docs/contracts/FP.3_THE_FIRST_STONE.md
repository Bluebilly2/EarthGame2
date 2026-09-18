# Contract FP.3 — The first stone

**Status:** drafted 2026-09-16 while the animals' looks were being made by an agent and FP.2 waited on the tree; opened
on ruling 31 (the Founder's Path comes first); built 2026-09-16 (the game side, below) by an agent whose session ended
before it could prove it, and parked unproven on `wip/agents-2026-09-16`; taken up 2026-09-18 on `agent/fp3-2026-09-18`
from that branch, its server and recorder lines applied by hand onto main's moved copies, and proved headless as "How it
is proved" says. Merged to main on 2026-09-18 (4a5086d) and run in the built game the same day: the exit record below. Owner:
Claude (Fable 5.1). The third beat of Act I: "Cobbles at the tideline, driftwood on the sand, fibre plants at the dune's foot.
Knap an edge (the wrong stone teaches as much as the right one), cut fibre, lay cord. The country as the only toolshop;
looking at a material names what it could become."

## What the path says (binding)

- **First stone.** Knap an edge; the wrong stone teaches as much as the right one. Materials over recipes: "two flakes
  off one core are different tools" is one of the five things a developer must see in the demo.
- **Fire** waits on it: the drill and hearth board are carved from found wood, and a flake is what carves.
- The build list: "first materials in the country: tool-stone cobbles, driftwood, reachable fibre" (the cobbles of the
  beach's own stone exist since M1.5b; a cobble taken up is of the stone its cell names).
- Ruling 33: no lesson anywhere. The stone does what stone does and says so; nothing in it is there to teach.

## What this slice promises

1. **A blow on stone does what the stone does**, by v1's `Knapping` ported term by term: Auerbach's law sets the energy a
   blow must carry to start a fracture, rising as the square of the hammer's radius, so a small hammer does the fine
   work; the cone's angle is fixed, so all the force behind a blow buys is size; too much and the core shatters; a
   hammer whose smallest bite is more than a spent core can spare destroys it; a stone under 150 g drives no cone at
   all; the edge a flake carries is the stone's own (`StoneType.EdgeQuality`, from its mineralogy) docked for violence;
   every flake works the platform toward ninety degrees, and a dead platform crushes until the core is turned, which
   costs stone. Sandstone crumbles and never takes an edge; quartzite gives a crude edge from a controlled blow and
   nothing from a wild one; silcrete and rhyolite, the coast's own, knap well. The arm's ceiling makes a hammer's size a
   choice.
2. **The verb.** With a stone in hand and another stone under the crosshair within reach (an item lying, or a cobble of
   the litter), the work button strikes: a tap is a light blow, a press held winds the arm up to a full swing over a
   second, and what thirst has left of the founder's work goes into the swing. The server commits the blow on the world
   it holds and answers in the words the model gives ("The hammer bounced. Not enough behind it."; "A clean flake, and
   it is sharp."), which the verb line shows; the client only asks. A core held in another place of the hands can be
   struck too (the wire and the server take it); the client offers no gesture for it yet (below).
3. **A flake is a thing of its own.** It lies where it fell, beside the core (a step towards the founder, and to one side
   then the other as the flakes come off) or at the founder's feet off a held core; it is picked up, carried, put down
   and saved like a stick, and is of its stone (`item/flake-<stone>`); it carries its own mass and edge as struck, so two
   flakes off one core are different tools. The core keeps its mass, its platform and its count of flakes; a spent core
   is gone (an item killed, a held one dropped from the hands, a litter cobble taken from the layer and not returned).
4. **What a flake is for** is the next beats' (cutting fibre, carving the drill and hearth board): this slice makes the
   edge and holds what it is worth (`Knapping.CuttingMinutes`), and nothing yet uses it.

## What it needs of the item model (the wire and the save), as built

A thing's mass and edge live on its definition until a blow gives it its own. `ItemComponent` gained the state a blow
leaves: the thing's own mass (kg), its edge (0 to 1), and for a core the platform's angle (degrees) and the flakes taken;
zero means "none of its own", so a thing weighs what its definition says and a stone never struck presents a fresh
cobble's platform (`KnappingItems` is the one reader and writer of that rule). It travels on every `EntitySpawn` and
`EntityState` that carries an item (protocol 15: f32 mass, f32 edge, f32 platform, u16 flakes after the rest and the
fall speed), rides in the region file (version 3) and, for a thing carried, with the thing in the hands (`CarriedThing.Item`)
and in the player file (version 6), and enters the world's digest only when the thing has state of its own, so every
stick and cobble written before this reads as it was and every older save keeps its name. The `Carrying` message does
not carry it: the client shows a thing's name, and what a hand holds is the server's.

## What changed on the way

- **The plain cobble will not knap.** `item/cobble`, the cobble of no named stone (the `-eg-items` drops and the panel's
  "A cobble, two metres ahead"), has a mass and a radius and so can be swung as a hammer, but as a core it answers "that
  is no stone to knap": the physics reads a stone's toughness and fracture, and the plain cobble has none to read. In the
  world every litter cobble is of the stone its cell names (M1.5b), so this meets a founder only in a development game.
  Rather than invent a stone for it, the panel gained one deed, "A silcrete cobble, two metres ahead (it knaps)"
  (`spawn.silcrete_cobble`), which the scenario uses and a developer can strike a flake from to look at.
- **A held core has no gesture on the client.** The verb rule gives the crosshair and the hand, and a core in another
  place is under neither; the wire's target kind 3 and the server's judgement exist and are tested over the in-memory
  wire, and the client offers the blow on the stone aimed at alone. Owed in DEBTS until a gesture is chosen.
- **A flake's look.** The client draws every item from the stand's own meshes (`ItemLooks`), and a flake lying is drawn as
  a cobble of a cobble's size until it has a shard of its own; in the hand it is drawn pressed flat, a plate about nine
  centimetres by seven (`HandView`). Owed in DEBTS; the frames say what it is.
- **A litter cobble a blow changes becomes an item where it lay**, by M1.5b's rule for a thing moved: taken from the
  layer for good, returned to the world under a new id with what the blow made of it, resting; a blow that bounces leaves
  it in the layer. Its yaw is not kept (the litter's shape debt of M1.5b has the same cause).
- **A stone too light to drive a cone** (under `Knapping.MinimumHammerKg`, 150 g: a pebble, a flake) does nothing to any
  core, not even sandstone's crumbling: a refusal the ported constant declared and nothing read.
- **The tool runs the harness's player** (2026-09-18). `knap.py` was written for a folder of its own (`Build/Player-Knap`)
  and a Unity build of its own; M1.Bb (2026-09-16) put the game in two folders replaced in place, so it now runs
  `Build/Harness/EarthGame2.exe` and its `--build` goes through `Tools/build/install.py --into harness --label knap`, as
  the drink and night tools do.
- **Main moved under the branch** (2026-09-18). Between the branch's base (4794028) and its taking up, main changed the
  thirst's pricing in `GameServer.AdvanceFounders` (ab46727) and the recorder's list of scenarios, so the knapping lines of
  `GameServer.cs` (the knap verb, the flake's fall, the silcrete deed, `Ahead` as a point) and of `Recorder.cs` (the `knap`
  scenario known and dispatched) were applied by hand onto main's copies rather than the branch's files taken whole;
  every other file of the slice came from the branch as it was.

## Non-goals

Cutting, carving and cordage (the fibre's beat); the hammer's own wear (`Knapping.MinimumPoundingQuality` is declared and
unread until it); heat treatment; the tablet's advice on which stone (`Knapping.BestHammer` in v1 chose the lightest stone
that could start a fracture, and the tablet quoted it: both come with the tablet); a flake's look beyond a flat shard of
its stone's colour; turning a core (`StoneCore.Turn` exists in the physics and no verb reaches it yet).

## How it is proved

**Headless, as run on 2026-09-18 in the worktree** (`agent/fp3-2026-09-18`, cut from main at c5b312d):

- `dotnet test Engine/EarthGame.slnx -c Debug --nologo`, redirected to a file: 700 passed, 0 failed, 0 skipped, exit 0,
  in 33 s. The suite holds the fracture mechanics against v1's constants restated (`KnappingTests`: the critical energy of
  flint at a kilo and a half about six joules and quartzite twice; the bounce below, the flake by the excess above, the
  shatter, the heavy hammer on a spent core, sandstone's crumbling, the dead platform and the turn, the arm's ceiling, the
  edge docked for violence, the cutting minutes; the stones' qualities from their mineralogy; the light hammer; the item's
  state read and written by one owner); the verb over the in-memory wire (`KnapWireTests`: struck, the flake spawned with
  its mass and edge and the core lighter, both in the client's mirror and the two ends' digests equal, two flakes off one
  core differing; a tap bouncing and changing nothing; refused without a hammer, with a stick, on no stone, out of reach,
  on nothing and while the world is held; a core held in another place; a cobble of the litter struck into an item; a
  spent core gone and its viewers told; the state saved and loaded, lying and carried); the messages
  (`IntentMessageTests`: the knap's three targets and its wind-up, a place refused for a pick-up, the answer's words, the
  outcomes' numbers; `EntityWireTests`: protocol 15, the state on an item, an impossible state refused); the save
  (`RegionSaveTests`: the state in the digest, in the region file and the player file, a version-2 region file and a
  version-5 player file still read as they were); the hands (`HandsTests`); the deed (`DevSettingTests`); the catalogue
  (`DefinitionTests`).
- **Each new test's reason broken once, seen red, the file restored byte for byte** (a script in the session's scratch,
  not in the tree; each case edits one line to something that still builds, runs that test alone, writes the original
  bytes back and reads them again to prove it): nineteen cases, nineteen seen red, every file restored. The cases: the
  core not made lighter (`AFullSwing…`, 1 failed); a bounce answered as crushed (`ATapBounces…`); a stick in hand answered
  as no stone (`WithoutAHammer…`); a held core's flake falling 1.5 m ahead instead of 0.5 (`ACoreHeldInAnotherPlace…`); a
  struck litter cobble left in the layer (`ACobbleOfTheLitter…`); a spent core kept instead of killed (`ASpentCore…`); the
  carried stone's state written to the player file as zeros (`WhatABlowMadeOfAStone…` and `WhatIsCarriedRoundTrips…`, 2
  failed); the light-hammer floor removed (`AStoneTooLight…`); the flake count not written back (`TheItemsState…`); the
  place target refused for a knap (`AKnapNamesItsCore…`); `NotStone` renumbered (`TheVerbsAndTheirKinds…`); the answer's
  words not read (`AnAnswerNames…`); an edge past one accepted (`AStruckStonesStateRidesInItsItem…`); the protocol at 16
  (`TheProtocolIsVersionFifteen…`); a version-2 region file read with the stone's state (`AStruckStonesStateRidesTheFiles…`);
  the stone's state left out of the digest (`EveryPersistedFieldEntersTheDigest`); the state dropped on pick-up
  (`AStruckStonesStateGoesThroughTheHands…`); the silcrete deed setting down quartzite (`ASpawnSetsAThing…`); a flake's
  catalogue mass at 30 g (`AFlakeIsOfItsStone…`). The suite run again on the restored tree is the last line of this list's
  proof, and a grep for every sabotage's own text found none.
- `dotnet build Engine/EarthGame.Unity.Compile.csproj -c Debug`: 0 warnings, 0 errors, exit 0 (the Unity layer against the
  editor's assemblies: `VerbController`, `HandView`, `Recorder.Knap`). `dotnet build Engine/tools/EarthGame.ServerHost -c
  Release`: exit 0.
- `save_check.py` on a real save with a struck stone in it (the knap wire test's own save folder, kept for one run by
  editing its tear-down and restoring it): every row ok, "1 players (by version: 6: 1)", the digest rebuilt by hand equal
  to the server's over 8 lines, a flake lying with its own mass and edge and a worked core carried, exit 0.
- **The three tests reported red on the parked branch** (`AFullSwing…`, `ACoreHeldInAnotherPlace…`,
  `WhatABlowMadeOfAStone…`) could not be made red on 2026-09-18: they pass in the worktree; they pass on the branch's own
  `Engine` tree extracted to a scratch folder, run alone (19 of 19 in their two classes) and in that tree's whole suite
  (whose only reds beyond the fixtures the scratch copy lacked were the five animal-shape tests of the other agent); and
  they pass with the branch's server compiled over main's changed water pricing. Whatever state showed them red was not
  the branch's code as committed; nothing in the code or the tests was changed for them.

**The built game** (owed; the head developer's, after the merge, with the harness copy built through the one install
tool): `python Tools/build/install.py --into harness --label FP.3`; `python Tools/world/knap.py --player
Build/Harness/EarthGame2.exe --world Artefacts/worlds/gate`, which runs the `knap` scenario as a development game on that
world (the founder stood at the wake, two silcrete cobbles set down a pace apart, one picked up, the other faced, a tap, a
measured blow and a full swing, each a `blow` record; frames of the flakes lying beside the core and of a flake in hand,
for William's eyes); `python Tools/verifiers/checks/knap_check.py <the run folder it prints>`, which restates Auerbach's
law, the swing, the flake's size and edge and the platform from their named sources and holds every recorded blow to
them, printing actual beside required (its arithmetic agrees with the engine's on the full swing of one fresh silcrete
cobble on another to the float's seven figures: a core of 0.579380 kg after); and `python
Tools/verifiers/checks/save_check.py Artefacts/worlds/gate` after it, the world having been saved with the flakes lying
and a flake carried. By the physics, a 0.6 kg silcrete cobble swung at another with a full body: the tap bounces ("The
hammer bounced. Not enough behind it."), the measured blow takes a 6 g flake with an edge of about 0.65 ("A clean flake,
and it is sharp."), and the full swing a 21 g flake with an edge of about 0.55 ("A flake, but a coarse one."), the core
left at 0.573 kg with its platform worked from 68° toward 80°.

## Exit record

Owed: written by the head developer after the built game has run and the check has read it.

## Exit record (2026-09-18)

- **Landed.** The game side merged to main as 4a5086d after the head developer's review (700 tests green in the agent's
  worktree, 713 on the merged tree with the animals' skin, the Unity-shaped compile and the host green; 19 sabotage cases
  seen red and restored; `save_check` green on a save with a struck stone). The harness copy built from 6f73ac7 by
  `install.py`, twice more with the recorder's two changes below.
- **Run in the built game.** `knap.py` on the harness copy, `Artefacts/frames/knap-20260917T235217Z` (exit 0), the
  founder at the wake of the gate world with a full body: two silcrete cobbles set down by the panel's deed, the first
  taken up as the hammer, the second faced and the verb line offering "a silcrete cobble — knap (hold to strike harder),
  or pick up". Three blows with the work button. The tap (wind-up 0.039, 0.78 J against the 4.50 J that starts a fracture
  in silcrete) bounced: "The hammer bounced. Not enough behind it.", the core 0.600 kg at 68.0°, as it was. The measured
  hold (0.549, 5.71 J) took a flake of 6.0 g with an edge of 0.651, "A clean flake, and it is sharp."; the core 0.594 kg
  at 72.8°. The full swing (1.000, 13.87 J) took a flake of 20.6 g with an edge of 0.545, "A flake, but a coarse one.";
  the core 0.573 kg at 79.9°, two flakes off it. Two flakes off one core, different tools (the path's demand). Then a
  flake taken up and made the hand: the hand was the flake's place (2) by the frame. `knap_check.py`: 23 rows green,
  the flakes' masses within 2 × 10⁻⁵ kg and their edges within 2 × 10⁻⁴ of the mechanics restated from Auerbach and v1,
  the platforms within 0.02°. The same run on 2026-09-17T23:05Z gave the same numbers (the physics is the server's on
  a held clock), red only on the recorder's row below.
- **The frames** (1440p and 1080p, four). `flake-lying`: the two flakes as small pale stones a step from the core
  towards the founder, one to each side, the verb line holding the last words; the hammer in hand fills the right of the
  view, a cobble drawn half a metre from the eye. `flake-held`: the silcrete flake in hand, a flat plate pinched near the
  eye, and the line "put down the silcrete flake". A flake lying is still a small cobble (DEBTS). William's eyes owed.
- **Found on the way.** (i) The recorder wrote a bounced blow's core after as the raw zeros that mean "none of its own",
  where it had written the resolved 0.6 kg before the blow; it now reads the after as the before, and the check's row
  for a bounce is green. (ii) The scenario is not repeatable on a shared world: it leaves the struck core and a flake at
  the wake and the founder holding the hammer and a flake, and the second run on `Artefacts/worlds/gate` found the first
  run's core under the crosshair where it had just set its own down ("the core was not under the crosshair" with the line
  offering a silcrete cobble). `knap.py` now runs on a copy of the world under its run's folder; the gate's dirtied save
  was set aside (`gate-dirty-20260918`) and its fixtures placed again by `populate.py` (WORKING.md). (iii) The first
  run's `flake-held` frame showed the hammer still in hand though the flake was carried; the scenario now checks that
  the hand is the flake's place and writes both to the end record, and the third run's frame shows the flake. Why the
  first did not is not known (the hold's answer is not logged).
- **Not exercised.** A blow on a cobble of the litter in the built game (the scenario strikes items set down by the
  deed; the litter path is proved over the in-memory wire); a core held in another place (no gesture, DEBTS); the arm's
  ceiling against a heavier hammer; a founder thirsty enough that the work capacity shortens the swing; the hammer's own
  wear (declared, unread); the plain cobble's refusal in the built game; the look of the wind-up and the swing under a
  hand's own eye.

## The open questions, decided (2026-09-18)

Asked of William with the exit record and answered by the head developer on his word ("answer the 5 questions to the
best of your ability, i will correct you if i dont like what you pick"); each stands until he corrects it.

1. **A flake struck with a cobble is smashed, and stays so.** Stone does what stone does: a hammer whose smallest bite is
   more than a small stone can spare destroys it, which is what a cobble does to a flake. Making flakes unstrikeable
   would be a guard with a lesson in it (ruling 33). The blow is the left mouse with a stone in hand, the pick-up the
   right, and the verb line says "knap" before the button is pressed. A flake refined by a small hammer (retouch) is a
   later beat, not a guard now.
2. **The left mouse stays silent when there is nothing to strike.** Every verb here changes the world and the client only
   asks; a swing at nothing would be motion with no consequence. The verb line already says what the mouse would do and
   says nothing when nothing would happen. When more work verbs come to the left mouse (cutting fibre, carving), it
   becomes "work with the thing in hand on the thing aimed at", and the hand's motion for an empty swing is decided then.
3. **A flake gets a shape of its own after fire.** Ruling 31 puts the path's beats before polish: fire and the carving
   that needs a flake's edge come first, and the shard's look lands with the beat where the edge is seen doing work. Until
   then a flake in hand is drawn pressed flat and a flake lying is a small cobble (DEBTS, "A flake is drawn as a cobble").
