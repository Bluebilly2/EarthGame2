# Contract WG.1 — Creeks with water

**Status:** opened and closed 2026-09-18 on William's word ("yes, do that" to the creeks given water first; CANON ruling 26 as
amended, world generation Claude's). Owner: Claude (Fable 5.1). The first world-generation slice after Codex's WG.0a–c.

## What the path says (binding)

The Founder's Path's first beat has the founder drink from the creek inland of the wake; ruling 32 makes the water near
the wake whatever the spawn gives, and the gate world's spawn gives a creek four metres off. The corpus of 2026-09-16
found that no creek or stream in the world held any water (DEBTS, "Creeks and streams carry no water"): the water layer
gave a surface to the sea and the mapped lakes alone, so the creek by the wake answered "nothing to drink there" and the
walkers died of thirst beside it.

## What this slice promises

1. **A creek or stream carries water over its bed by the flow's law.** `WorldLayers.ChannelDepthM(catchment)`: nothing
   under the creek's catchment (`DrainageNetwork.CreekM2`, twelve hectares, where a trickle runs under the leaves and the
   drink verb finds nothing, as before); `CreekDepthM` 0.15 m, ankle-deep, at it; deepening as the catchment to
   `ChannelDepthExponent` 0.4 (the downstream hydraulic geometry of Leopold and Maddock, USGS Professional Paper 252,
   1953, the discharge of these small coastal catchments taken as their area); held at `StreamDepthMaxM` 0.8 m. A stream
   at five times the creek's catchment runs about twice as deep. The surface layer carries it, so everything that reads
   the surface has it without a change: the drink verb and the client's aim (FP.1, the creek's mouth of 2026-09-18), the
   depth tiles and the drawn water (M1.4b), wading (M1.5d), the walker's search (M1.Bc), the wake's criteria.
2. **The drink at the wake's creek.** The drink scenario's stand, chosen by the tool as before (dry, gentle, a cell from
   standing fresh water, within reach of the sea, nearest the wake), lands by the creek beside the wake on a world made
   with this law, and the founder drinks there.
3. **A world made after this carries it; a world made before does not.** The layers are computed at creation and saved
   with the world (M1.2), so the gate world and the corpus's template are made again; a world saved earlier keeps its dry
   creeks until it is made again. Nothing on the wire or in a save's format changes.

## Non-goals

The channel's width (a cell of the raster, four metres, for any creek: the shape below the data's resolution is the
"empty and smooth" debt); the water's flow, colour or sound; seasonal drying; the creek's bed cut below the ground.

## How it is proved

`dotnet test` over the whole tree (`CreeksAndStreamsCarryWaterByTheirCatchment`: every creek and stream cell of the made
country stands in water by the law, no dry, damp, trickle or swamp cell does, the law's four points), its reason broken
once and seen red; `drainage_check.py` rows 4 and 5 on the gate world made again (the law restated with the check's own
catchment, and no water off the channels and lakes); the gate world made again by `create.py` and its fixtures placed by
`populate.py`, `save_check` green; the drink scenario run on it (`drink.py`, `thirst_check` green) with the stand by the
wake's creek; frames of the creek drawn; William's eyes.

## Exit record (2026-09-18)

- **The law, in the engine and held.** `WorldLayers.ChannelDepthM` and the surface written from it; 714 tests green over
  the whole tree (exit 0; `CreeksAndStreamsCarryWaterByTheirCatchment` new), the Unity-shaped compile and the host green;
  the test's reason broken twice and seen red (the creeks given the ground's surface again; a trickle given water), the
  generator restored byte for byte and the class green again.
- **The gate world made again** by `create.py` (the wake where it was, east −1392 north 2804) and its fixtures placed by
  `populate.py`; `save_check` green. `drainage_check.py` on it: the four rows of before unchanged (the engine's D8 and the
  check's agree at the creeks and the lakes), and the two new ones green: 13,609 of 13,666 creek and stream cells stand
  in water within 15 % or a centimetre of the law worked from the check's own catchment, the depths 0.15 to 0.75 m, the
  57 outside it cells the two D8s class differently at a divide (the check's law says nothing where its own catchment is
  under the creek's); no dry, damp, trickle or swamp cell of 3,081,408 has a surface off its ground, and no sea cell is
  off the datum.
- **The drink at the wake's creek, in the built game.** `drink.py` on the harness copy: the tool's stand fell on the wake
  itself (dry, gentle, a cell from standing fresh water, within reach of the sea), where before it had walked to a lake
  4.7 km off; very thirsty after 20 s at sixty times the game's rate, the drink from the creek Done (water 0.9595 to
  0.9952, a litre and a half of forty-two), the sea refused as salt; `thirst_check` green, nine rows
  (`Artefacts/frames/drink-20260918T010929Z`, six frames). The founder's feet found water underfoot thirteen times on the
  way: the creek is waded now, ankle-deep.
- **Not exercised.** The creek's look: the drink scenario takes its frames at the world's own hour, which stood at five
  past midnight, so they show a black ground and the verb line "water — drink" and nothing of the water (the FP.1 trap
  again); a daytime frame of the creek by the wake is owed, and William's eyes on it (a four-metre ribbon of water, one
  cell wide, the raster's own width). The corpus's walkers drinking from it (the next full corpus; the loop already
  passes within their search of the wake's creek); the streams' knee-deep wade; the drawn water's edge against a bank
  that is the ground's own slope rather than a lake's flat.

### The creek in daylight (2026-09-20)

The scenario no longer takes its frames at whatever hour the drying left the clock at: it pins mid-morning through the
panel's own clock setting before the first capture, as the looks scenario has since M1.7b, so two runs light the water
the same way and the owner is never handed a black frame (`Recorder.Drink.DrinkHour`, recorded as `local_hour` in the
run's end line). Run again on a copy of the gate world with the harness build of that morning
(`Artefacts/frames/drink-20260919T225356Z`, 10:01 of day 1): very thirsty after 20 s at sixty times the rate, the creek
drunk from (water 0.9594 to 0.9951), the sea refused as salt, no error, `thirst_check` green on all nine rows.

What the frames show is the finding, and it is not the one the run was for: **the water is there and does not look like
water.** At the creek's ankle depth the surface takes almost the ground's own green, with no edge, no sheen and no
movement, so it reads as lawn with sedge standing in it; the sea, deep enough to darken, is the only water in the three
frames that reads as water. That is now a debt of its own ("Shallow water does not read as water"), and it bears on the
path's first beat, which asks the founder to find water by reading the country.
