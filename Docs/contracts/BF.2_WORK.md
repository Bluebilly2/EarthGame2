# Contract BF.2 — Work

**Status:** drafted and built 2026-09-22, the second of the beta's foundations (`Docs/BETA_MAP.md` §7, CANON ruling 41), on BF.1.
Owner: Claude. William's lane: the frames of the new things (bark strips, cord, a pointed stick) and, when he plays, the
feel of a hold that does something over seconds.

## Why this next

Everything the path makes is made by work: an edge cuts fibre, hands lay cord, an edge carves a drill and a hearth, a
stick is broken to length, bark is stripped for tinder. Today the only work is the knap, one blow answered at once, and
every verb is its own case in three switches; nothing continues over ticks, nothing has a requirement or a rate, and the
crosshair's offer is a hand-written cascade. GAME_DESIGN §7B: a transformation has inputs, outputs, conditions,
requirements, rates, constraints, byproducts and failure modes; §9: it produces a distribution, not a perfect item; the
verb rule (CANON 2026-08-26): what an action is comes from what you are looking at and what is in your hand, and holding
the left button is *work on it*. The map's chains bottom out here six times.

## Promises

1. **One model of work.** `Work` (engine, `World/Work.cs`): a kind of work (`WorkKind`: break, strip, point, twist; split
   and notch come with the fire's kit, BF.5), judged by one function, `Work.Judge(kind, tool, toolState, target,
   targetState)`, from properties alone — never from a key — into a `WorkOffer`: whether it can be done, why not in words
   when it cannot (`VerbOutcome.NoTool`: the hand lacks what the work needs; `VerbOutcome.WontWork`: the thing's
   properties refuse it), how many seconds it takes at full capacity, and the words for it ("break the stick over your
   knee", "point the stick with the flake, about 20 s"). `Work.Offers(tool, toolState, target, targetState)` lists what the
   crosshair can offer for what is looked at and what is in hand, in a stated order, so the client and the server agree on
   the first. Applying a finished work is one function too, `Work.Apply`, whose result is a list of things made, things
   changed and things spent, which the server commits and the tests read.
2. **The four works, by physics or by a stated model.**
   - *Break* (empty hand, a stick): a stick snaps over the knee when the moment a person puts through it, 60 N·m (150 N at
     0.4 m: a stated model), exceeds the wood's, π d³ σ / 32 with σ the species' modulus of rupture (`Wood.RuptureMPa`;
     the plain stick's 60 MPa); so a thumb-thick blackbutt stick breaks and a wrist-thick one refuses in words. Two
     seconds. It leaves two sticks of the same wood, thickness and water, their lengths a hashed 0.35–0.65 split of the
     one, their masses by length.
   - *Strip* (empty hand, a stick of a tree whose bark strips: `PlantSpecies.StrippableBarkM` > 0, not yet stripped):
     six seconds a metre. It leaves the stick lighter by its bark and marked stripped, and one bark strip
     (`item/bark-<tree>`, `Substance.Bark`) for every 0.3 m of the stick's length, each 0.3 m long, as wet as the stick,
     weighing what a strip of that bark's thickness weighs at 600 kg/m³.
   - *Point* (an edge in hand: a stone with `Edge01` ≥ `Knapping.UsableEdge`; a stick thinner than 30 mm): carving a
     point takes off a cone of wood three diameters long, at 0.5 cm³ a second at a full edge and a full softness,
     scaled by the edge and by 0.3 + 0.7 × the wood's `Softness01` (a stated rate, a DEBTS row to measure); so a
     banksia stick points in tens of seconds and a blackbutt one in minutes. It marks the stick pointed and wears the
     edge by 0.05 for every 10 cm³ carved, more in harder wood.
   - *Twist* (a bark strip or a cord in hand, a bark strip in another place of the hands): fifteen seconds a join. It
     leaves one cord (`item/cord`, `Substance.Cord`) in the hand, 0.45 m of cord for every metre of strip laid into it,
     as wet as its strips, weighing what they weighed; a cord in hand grows by the strip. The strip is spent.
3. **Work takes time on the server, and stops when it should.** `Verb.Work` (6) starts a work: the kind and its target
   (an entity, a lying thing or a place of the hands); the server judges it as the client did and answers the offer's
   outcome with its seconds and its words (`IntentResult` carries `Seconds` since protocol 18). `Verb.StopWork` (7) lets go.
   A session's work in progress (`PlayerSession.Work`) advances every server step by the step scaled by the body's work
   capacity (`Hydration.WorkCapacity01`, the same throttle the swing has), and stops, saying why, when the founder moves
   more than half a metre from where they started, when the target is no longer there or in reach, or when the hand
   changes. `WorkState` (message 25, protocol 18) tells the client the kind, the progress and the seconds left once a
   second while it runs, and once more when it ends, done or not, with the words. A work in progress is not saved: a
   founder who leaves mid-work starts again.
4. **The client holds, and sees.** The work button held on a thing with an offer starts it; let go, stops it (the knap's
   wind-up stays the knap's: with a stone in hand on stone, the button is the blow). The verb line says the offer ("hold
   to strip the bark") and, while working, the kind and a bar of the progress with the seconds left; the hand moves as it
   works. The Tab window names the new things by `ThingWords` (a bark strip by its tree, its length and its wetness; a
   cord by its length).
5. **New things have looks of their own.** `StandMeshes.Strip` (a thin curled ribbon, 0.3 m) and `StandMeshes.Cord` (a
   coil) made in code as the litter's are, in `StandLayout.Looks` variants; `ItemLooks` draws bark by `Substance.Bark` and
   cord by `Substance.Cord`; the pointed stick is drawn as the stick it was (its point is in its state, seen in words:
   the mark's look is a DEBTS row). Frames from the work scenario at 1440p and 1080p, for William's eyes.
6. **Proved in the built game.** A `work` scenario (the recorder, `-eg-scenario work`, run by `carry.py --scenario work`)
   on a copy of the gate world: takes a stick of the litter, strips it, twists two strips into cord, breaks a thin stick,
   knaps a flake off a silcrete cobble of `-eg-items` and points a stick with it; its log carries each work's words and
   seconds and the things made; `save_check.py` GREEN on its world (the new kinds in a region and a player file).

## Non-goals

No work on standing plants or the ground (fibre from a Lomandra, bark from a trunk, digging: BF.3 opens the world's
changes); no split, notch or drill (BF.5); no fire; no strength for cord beyond its length (a cord's tensile strength
arrives with the first thing that pulls on it, BF.6); no hands' limits; no saving of a work in progress; no change to
the knap.

## How it is proved

- Tests first, red: `WorkTests` (each work's judgement from properties: the thumb-thick stick breaks, the wrist-thick
  refuses with the words; strip needs bark; point needs an edge and takes longer in harder wood; twist needs a strip in
  another place; `Offers` in the stated order; `Apply`'s results: two sticks whose lengths sum to the one, strips by the
  length, a cord that grows), `WorkWireTests` (the two verbs' layouts, `WorkState`, protocol 18), `WorkServerTests` (a
  work advances by the step and the capacity, stops on a move, on a vanished target and on a hand change, and applies at
  the end with the things in the store, the hands and the takings as promised; a second client sees the things made),
  `DefinitionTests` (the bark and the cord kinds), the edit-mode `ItemLooksTests` (a strip and a cord have looks).
- Sabotage, restored byte for byte: the moment limit doubled (a wrist-thick stick breaks); the capacity ignored in the
  advance; the strip spent but no cord made.
- `dotnet test Engine/EarthGame.slnx > test.log 2>&1; echo $?` with the count as run; the Unity-shaped compile; the
  edit-mode tests; promise 6's run, its frames and `save_check.py`.
- Docs in the same commit: ARCHITECTURE §5 (work), §10 (protocol 18, the `WorkState` message, the verbs); DEBTS: the
  carving rate to measure, the pointed stick's look; BETA_MAP §7 marks BF.2 built.

## What changed on the way

- **The built run strips, twists and breaks; it does not knap or point.** The point wants an edge in hand, which wants a
  knap first, and the scenario's wake has no silcrete to hand; the point is proved by the server's tests
  (`WorkServerTests`, `WorkTests`) and its built run is a DEBTS row for the fire kit's scenario, which knaps and carves.
  The break found nothing thin enough within reach of the wake (the swamp paperbark sticks there are wrist-thick) and
  said so in the log rather than failing: the break is proved by the tests, and the scenario breaks when a thin stick lies
  near.
- A record's own key is `kind`; the work records name their work under `work`.
- Bark is named by its dryness and cord by its length (`ThingWords`), so the hand's cord says "forearm-long".

## Exit record (2026-09-22)

1. `Work.Judge`, `Offers`, `First`, `Apply` (`WorkTests`, 6 tests): the bangalay stick breaks at 18 mm and not at 35, in
   words; bark strips only from a tree whose bark strips and only once; a point wants an edge ≥ 0.2 and a stick under
   30 mm and takes 1.8× longer in blackbutt than in coast banksia; cord wants a strip in another place and grows by the
   strip; the offers come in the stated order.
2. The four works by their numbers as promised; the moment, the rates and the shares as stated in the contract.
3. `Verb.Work` (6), `Verb.StopWork` (7), `IntentResult.Seconds`, `WorkState` (25), outcomes 15 and 16, protocol 18
   (`WorkWireTests`, 3 tests); `WorkInProgress` on the session, `AdvanceWork` at the body's capacity, stopped on a move
   of half a metre, a vanished target and a changed hand, told once a second, applied at the end (`WorkServerTests`,
   5 tests: two sticks from one where it lay; strips beside a stick of the litter taken from the layer and lying on
   stripped; cord in the hand where the strip was and told to the client with its length).
4. The client's press and release, the offer's words, the bar, the twist with nothing aimed at (`VerbController`).
5. `StandMeshes.Strip` and `Cord`, drawn by `ItemLooks` (`ItemLooksTests`, edit mode, passed); frames from the work run
   at 1440p and 1080p (`Artefacts/frames/work-20260922T094504Z/frames/`), for William's eyes.
6. The work run on the harness build (BF.2, 16e8e17e6+dirty): a swamp paperbark stick of the litter, "a pace long,
   wrist-thick, sodden", stripped in 5.6 s into three strips, two of them laid into cord in 15 s, "two strips laid into
   cord, forearm-long", the hand holding "a cord, forearm-long"; 0 errors, 0 corrections, 4 frames; `save_check.py`
   GREEN on its world (one thing carried, one taken), `tile_check.py` GREEN on its cache.

Counts as run: `dotnet test Engine/tests/EarthGame.Tests` 795 passed, 0 failed (780 before; 15 new); sabotage three of
three caught (the knee's moment doubled: 1 of 17 failed; the capacity ignored: 1 of 5; the strip spent with no cord: 2
of 22), each file restored byte for byte; the Unity-shaped compile green; edit mode 10 of the project's 10 (the untracked
probe's failure as before).
