# Contract BF.3 — The world changes

**Status:** drafted 2026-09-22, the third of the beta's foundations; stage one (promise 1, the record of change) built the same day (`Docs/BETA_MAP.md` §7, CANON ruling 41), on BF.1
and BF.2; stage two (promises 2 to 5 on the server: standing targets, the works on them, the dig, the cut and the fall, the lift limit) built 2026-09-23; stage three (promise 6, the client's side, and promise 7, the built game) built the same day: the exit record below. Owner: Claude. William's lane: the frames of a cleared patch, a stripped trunk and a cut tuft; his hands on
the work when he plays.

## Why this next

CANON ruling 22: the world starts in a generated state and changes from there, as time passes and as players act. Today
the only change a player can make to the generated world is to take a stick or a cobble (a bit beside the loose layer,
M1.5b): a tree cannot be touched, a tuft cannot be cut, the ground cannot be cleared or dug, and every tile is encoded
once and kept for ever. The path's next beats need the standing world to give: fibre from a Lomandra for cord (G4),
bark and tinder from a trunk for fire (G6), a patch cleared for a camp (G9, G10), tubers dug where they grow (G8), and
later a tree felled for its wood. GAME_DESIGN §7B–C: transformations with constraints; the map's chains bottom out here
five times.

## The shape

The layers a world was made with never change, as M1.5b chose: what changes is kept beside them as **diffs by cell**,
told to every client and saved in the region files, and both sides draw and judge from the layer plus its diffs. One
record holds every kind of change (`WorldChanges`, replacing `LooseTaken` as the owner and keeping its bits): a cell's
diffs are a set of **change layers**, each a small fixed payload — the loose things taken (sticks and cobbles, bits, as
now), the tufts taken (bits by index), the trunk's yield taken (bark and limbs, bits), the trunk cut (a byte of
progress and a felled bit), the ground cleared (a bit) and the ground dug (a byte of centimetres). Nothing here changes
a tile: the tile service still encodes once; a client applies the diffs it is told as it applies takings today.

## Promises

1. **One record of change.** `WorldChanges` (engine): cells keyed by row and column, each holding the change layers it
   has (`ChangeLayer`: Loose 5 as the tile byte, Tuft 9, Trunk 10, Ground 11; never renumbered), merged monotonically
   where a change is a taking (bits set, never cleared) and by the latest value where it is a measure (cut progress,
   dug depth). `LooseTaken`'s API stays as a view over it. The region file (version 5) writes each diff as u8 layer, u16
   row, u16 col, u8 length and the payload, so a reader that does not know a layer skips it by its length rather than
   refusing; versions 1–4 read as before. The wire carries changes as `Changes` (message 26, protocol 19), every cell at
   the join and a cell again when it changes; `LooseTaken` (20) is retired from sending and refused on reading. The
   digest names every change layer's cells. `save_check.py` restates the layout.
2. **Standing things are targets.** An intent's target has two more kinds: a trunk (kind 4: the stand cell's row and
   col) and a tuft (kind 5: row, col, index as the understorey draws it). The client aims at a trunk through its
   capsule (`TrunkBodies`) and at a tuft through the same placement the understorey draws it by
   (`Understorey.Find`'s instances, hit-tested as the litter is); the server finds both from its rasters and the
   changes, refuses one that is gone, and names both by `ThingWords` ("a blackbutt, 22 m", "a Lomandra tuft").
3. **Work on what stands.** Three more works in `Work` (BF.2's model, judged from properties):
   - *Strip bark from a trunk* (empty hand; a tree whose bark strips; not yet stripped): thirty seconds; it leaves the
     trunk's bark taken (a bit, the trunk drawn pale below head height) and strips on the ground by the trunk's girth
     at breast height (`StandForms.TrunkRadiusAt`) — one 0.3 m strip for every 0.1 m of girth, as dry as the cover's
     quarter says, weighing what BF.2's strips weigh at that bark's thickness.
   - *Cut fibre from a tuft* (an edge in hand; a Lomandra or a saw-sedge tuft): twelve seconds; it leaves the tuft
     taken (not drawn, not aimed at) and fibre strips (`item/fibre-<plant>`, `Substance.Fibre`), three of 0.6 m, as wet
     as the ground, 15 g each; *Twist* takes fibre as it takes bark, and cord from fibre is the cord the path names.
   - *Pull a tuft* (empty hand; a grass, bracken or sedge tuft): four seconds; it leaves the tuft taken and a bundle
     (`item/bundle-<plant>`, `Substance.Plant`), 0.3 kg, as wet as the ground: the bedding's material (BF.6).
   - *Clear the ground* (empty hand; the cell looked at within reach, with tufts on it): two seconds a tuft; it leaves
     every tuft of the cell taken and the cell cleared (a bit: bare earth drawn), and the bundles on it.
4. **Dig** (a pointed stick in hand: `ThingMarks.Pointed`; the ground looked at within reach, soil deeper than 0.1 m
   there): ten seconds a decimetre in sand, twenty in soil, refused in rock, wet swamp and under water; it leaves the
   cell dug by that much (drawn as a hollow in the ground by the client, the server's ground unchanged: DEBTS) and,
   where a tuber-bearing plant grows on the cell (Lomandra's base, bracken's rhizome, the bulbine's bulb when the
   table has it), a tuber (`item/tuber-<plant>`, `Substance.Food`, its mass by the plant) on the ground: the digging
   stick's beat (G8), with what is eaten arriving in BF.7.
5. **A tree cut, and felled** (an edge in hand of at least 0.4 and a mass of at least 0.3 kg — a hafted axe when one
   exists; until then a heavy flake cuts slowly): cutting a trunk takes a stated rate by the trunk's area at the cut and
   the wood's hardness (a first model, DEBTS: hours for a blackbutt with a flake, which is the truth of it), its
   progress kept in the cell's change; at the end the tree is felled: the cell's trunk is gone from the stand's drawing
   and its capsule, and the wood lies where it fell — the trunk as logs (`item/log-<tree>`, `Substance.Wood`, 2 m each
   by the taper, their mass by the wood, more than the hands lift) and its limbs as sticks and limbs of the tree, laid
   down the fall's line from the stump. **The hands lift at most 25 kg** (`Hands.MaxLiftKg`, NIOSH's lifting limit, a
   published figure): a heavier thing answers `TooHeavy` (outcome 17) in words; moving logs is later work.
6. **Both sides agree.** The client draws the stand less its felled trunks and stripped ones pale, the understorey less
   its taken tufts and cleared cells bare, the ground's hollows; `TrunkBodies` drops a felled trunk's capsule; a
   founder cannot aim at what is gone. The corpus's join check sees the same changes on both clients.
7. **Proved in the built game.** A `changes` scenario: strips a trunk, cuts a Lomandra tuft, twists fibre into cord,
   pulls a bracken tuft, clears a cell, digs with a pointed stick (pointing one first with a flake the panel sets down),
   and cuts a small tree through; frames of each; `save_check.py` GREEN on its world (region file 5 with every change
   layer); `tile_check.py` GREEN (the tiles unchanged).

## Non-goals

No fire (BF.5), no structures (BF.6), no eating (BF.7); no regrowth; no moving of logs; no tree falling on anything
(the fall's consequences beyond where the wood lies); no change to the server's ground from a hole (the client draws
it; the mover walks the raster); no new lying kinds in the loose layer (driftwood and tinder are BF.4's placing).

## How it is proved

- Tests first, red: `WorldChangesTests` (the record's merge rules, the region file's length-prefixed diffs and the older
  versions, the message, the digest), `StandingTargetsTests` (a trunk and a tuft found and refused when gone),
  `WorkTests` (the new works judged and applied), `WorkServerTests` (each committed as promised; the felled trunk's wood
  down the fall's line; a heavier thing than the hands lift refused), `HandsTests` (the lift limit), the client's
  edit-mode tests for the new looks (fibre, bundle, tuber, log).
- Sabotage, restored byte for byte: the cut's progress not kept; the lift limit removed; a taken tuft still found.
- `dotnet test Engine/tests/EarthGame.Tests > test.log 2>&1; echo $?` with the count as run; the Unity-shaped compile;
  the edit-mode tests; promise 7's run and its verifiers; a walk of the corpus loop's join check for the changes on
  two clients.
- Docs in the same commit: ARCHITECTURE §5 (changes, standing targets, the works), §6 (the region file), §10 (protocol
  19, `Changes`, region file 5, the digest); DEBTS: the cut's rate, the hollow the server does not walk, the tuber table;
  BETA_MAP §7 marks BF.3 built.

## Exit

The promises kept with the counts as run, or "What changed on the way" saying which was not and why.

### Exit record (2026-09-23)

**Kept.** Promise 1 (stage one, 3c0c8be). Promises 2 to 5 (stage two, 5dc207f): `StandingThingsTests` (a trunk and a tuft
found where the layout puts them and refused when felled, taken, cleared, inside a trunk or past the sixteen; every tuft
the client draws found by the server to the centimetre), `WorkStandingTests`, `StandingWorkServerTests` and the wire;
816 green; sabotaged in turn (the cut not kept, the lift limit removed, a taken tuft still found), each red, each
restored byte for byte. Promises 6 and 7 (stage three): the `changes` scenario in the built game
(`Artefacts/frames/changes-20260922T222454Z`), 0 errors, 1 correction, player exit 0: a 5 m swamp paperbark stripped
("the bark came away in 6 strips round the trunk", the strips in a ring at its foot, the trunk drawn pale), a lomandra
clump cut with the panel's keen flake ("3 strips of lomandra fibre cut", the clump gone), two strips laid into cord
("arm-long"), a bracken frond pulled ("a bundle"), the cell under the crosshair cleared with empty hands ("4 bundles lie
on it", 8 s), a bangalay stick pointed with the flake (39.4 s, "the edge is sharp"), the cleared cell dug with it (20 s,
"0.1 m down; a bracken tuber came up", the hollow in the client's ground) and a 4 m swamp paperbark cut through with the
panel's chopper (118.3 s, the cut kept in the cell as it went, "1 log and 1 limb lie where it fell", the trunk gone from
the stand and its body); 126 change records told to the client; the crosshair's own lines recorded ("a swamp paperbark,
5 m — hold to strip the bark off the swamp paperbark, about 30 s"). `save_check` GREEN on the world the run saved
(the digest rebuilt by hand equal to the server's, 38 lines) and `tile_check` GREEN on the run's cache (4,055,591 posts
agree: no tile changed). Twelve frames at 1440p and 1080p. The Unity edit-mode tests 10 of 10 (the item looks audited).

**What changed on the way.** (1) The hands lift 23 kg, NIOSH's load constant, not the 25 the contract rounded to.
(2) A third target kind, a cell of the ground (6), since clearing and digging name a cell and not a thing. (3) Strip and
pull are done with whatever is in hand, as BF.2's break is; only clearing wants empty hands, because it is offered before
the dig on the same cell. (4) The tuft rule and the trees' geometry moved into the engine so the server could find and
measure what the client draws; the fixtures' 10 m cells showed the sixteen-tuft cap (DEBTS). (5) The felling rate came out
at days for a stone chopper against a blackbutt as stout as the stand draws it (DEBTS: the trees' stoutness), so the
scenario fells a 4 m paperbark with a heavy keen core the panel sets down; a founder's own knapped core would take an
hour on the same tree. (6) The stripped trunk is pale from the foot to the crown, a cleared cell keeps its cover's colour,
and the new things wear borrowed looks (DEBTS); in the run's felled frame the log lies as a stick, because the stick's look
answered for every wood before the log's, put right after the run and not re-run. (7) The hollow is the client's terrain alone (DEBTS, as the contract
foresaw). (8) The corpus loop's join check was not walked with a work in it (DEBTS): two clients agreeing is shown by the
server tests' client record and the scenario's client, not over a lossy wire. (9) A crosshair rule found in the run: a
thing lying among tufts is offered before the tuft whose bounds hide it, or nothing put down in bracken could be taken
up again. (10) Found on reading stage three again after the run, before William played it: a tuft's bounds hid the ground
and the water behind them, so in grass the put-down, the drink and the cord's twist were often not offered (a tuft now
hides nothing, and the twist, which is about the hands, comes before the standing world); a clearing counted and bundled
the tufts the place keeps from standing (inside a trunk, in water) and laid its bundles as if every cell were 4 m (the
ground site now carries the cell's size and those tufts: tests red first, then green, both rules sabotaged and restored);
and a felling cut re-placed its tile's whole stand at every told second (a change is now drawn again only when what it
changes is drawn). Proved by the suite, 816 green, and the Unity-shaped compile; the run in the built game waits until
William's game is closed.
