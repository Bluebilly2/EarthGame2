# Contract FP.1 — Thirst and water

**Status:** closed 2026-09-15, the exit record at the end; opened the same day, on CANON ruling 31 (the Founder's Path
comes first) and William's "sounds good for the survival story start point" to the beat proposed. Owner: Claude
(Fable 5.1). The first beat of the Founder's Path's Act I (`Docs/FOUNDERS_PATH.md`): thirst, the sea that will not
drink, and the water that will. The path's own name is used for this work and its beats (ruling 31).

## What the path says (binding, 2026-09-01)

- **Thirst.** "The sea will not drink. First tablet consult: why (salt, and what it costs a body)." The tablet's Q&A
  pattern: every "no" carries its mechanism.
- **Water.** "The creek, 174 m inland — findable by reading the land (the gully, the green). Drink."
- **Kill conditions** of Act I: cold, dehydration, "both fully explained on death".
- The lesson the body's clocks teach (v1's `BodyState`): warmth kills in hours, thirst in days, hunger in weeks.

## What this slice promises

1. **A founder's body holds water and loses it on the world's clock.** Ported from v1's `BodyState`, whose numbers
   are published physiology restated in code: 42 litres in a 70 kg adult, 2.4 litres a day lost at rest, death at a
   loss of 15% (a little under three days at rest). The loss runs on the world's clock, so a held clock (the panel's
   0) holds the body too, and a sped clock dries it faster. What the founder notices: nothing for the first hours,
   then the word.
2. **Thirst is a word, not a bar.** Under the clock, in the terms the founder would use: nothing while fine, then
   *thirsty* (1.5% lost), *very thirsty* (4%), *failing* (7%), *collapsing* (11%). The thresholds are v1's.
3. **Thirst weakens.** Work capacity falls by v1's dehydration table (2% lost costs a little, 4% a tenth, 6% a
   quarter, 10% half, 15% nearly all), and the founder walks as one with that capacity: the mover's own rule, on the
   client's body and in the server's ceiling alike, from the capacity the server last told the client.
4. **Fresh water can be drunk; the sea will not drink, and says why.** Looking at water within reach, the use key
   asks to drink. The server judges the point as it judges a put-down (reach, the region) and then the water there
   by the world's own layer: a creek, a stream or a lake gives up to a litre and a half a visit (v1's absorption
   limit, so water is somewhere the founder keeps going back to); the sea answers *salt* — "the sea will not drink:
   salt", the mechanism in the answer as the path asks, until the tablet exists to say more; dry ground answers that
   there is nothing to drink there.
5. **What a founder's body holds is saved with them** (the player file's next version) and told to their own client
   once a second and at every change that matters: a drink, a developer's setting, the join.
6. **The developer's panel moves it**: a slider for the founder's water, under "The founder", for the one who moves
   it.

## Non-goals

Death by thirst and what follows it (the wake, the carried things lying where they fell, the explanation): the next
beat, with cold, since the path binds the two kill conditions and their explanations together. Sweat, heat and
illness, which v1 charged water for: they come with the thermal model. Food. The tablet. Carrying water. Where the
water is: this world's wake stands 4 m from fresh water by the scorer's own choice, which is not the path's "findable
by reading the land"; a question for William, recorded in DEBTS, not decided here.

## How it is proved

`dotnet test`, each new test's reason broken once: the body's numbers against v1's constants restated in the test;
the words at their thresholds; the capacity table; a drink's litre and a half and its cap at full; the message's
round trip and its refusals; the verb over the in-memory wire on a world with a lake and a sea (drunk, refused salt,
out of reach, nothing there); the save's round trip; the panel's row; the validator's ceiling with a told capacity.
The Unity-layer compile. Then the built game: a `drink` scenario run by `Tools/world/drink.py`, the founder stood
by fresh water and the clock sped, recording the thirst as it comes, the drink and the sea's refusal to `run.jsonl`;
`Tools/verifiers/checks/thirst_check.py` holds the recorded loss to the resting rate from the constants it restates,
the drink's rise to the litre and a half, and the sea's answer. The word on screen is a visual change: frames, or
William's eyes.

## Exit record (2026-09-15)

**What exists.** `Hydration` (engine, `Runtime/Body/Hydration.cs`): the water against normal, the loss by days at
the resting rate, the words and the capacity as static tables for every reader, a drink capped at a visit's worth
and at full, hours to collapse, `IsAlive` that nothing yet acts on. `WorldClock.DaysFor` is the body's clock.
`PlayerSession.Hydration`, advanced by `GameServer.AdvanceFounders` every step, told by `SendFounderState` once a
second (`FounderStateMessage`, protocol 13, one number) and at a drink, a setting and the join's snapshot; saved as
`SavedPlayer.WaterLoss` in `eg2.player` version 4 and restored at the join. `Verb.Drink` (4) laid out as a put-down;
`GameServer.Drink` judges the reach and the region as `Hands.PutDown` does, then `WorldState.WaterAt`: the water's
surface read between posts as the client reads the depth it is streamed, so the two agree where water stands, and
the class from the nearest wet post (the first run's founder looked at a creek's edge and was told there was nothing
there, the nearest post being dry); `VerbOutcome.Salt` (7) and `NoWater` (8). The work capacity is the body's:
`Mover.Step` takes it beside the config, `MoverConfig.MaxHorizontalSpeedAt` gives the ceiling, and the validator is
handed the greater of the last two capacities the client was told. The client: `GameClient.FounderStateChanged` and
`LastWater01`; the word under the clock (`HudController.SetCondition`, from `Hydration.WordFor`); the walk's capacity
(`PlayerController.SetWorkCapacity`); the aim's water target (`VerbController.WaterAt`, the eye's ray walked out until
it goes under the streamed surface, the ground first being the bank), "water — drink" on the verb line, "the sea will
not drink: salt" and "nothing to drink there" as the answers. The panel's row "The founder's water, 1 full"
(`founder.water`, 0.8 to 1). The recorder's `drink` scenario, `Tools/world/drink.py` and `thirst_check.py`.

What William notices: after some hours of the world's day, "thirsty" under the clock, later "very thirsty",
"failing", "collapsing", and a slower walk; looking at water within reach, "water — drink"; the use key at a creek,
a stream or a lake takes the word away; at the sea, "the sea will not drink: salt".

**Tested, and how.**

- `dotnet test`: 595 passed, twenty-three of them new. `HydrationTests`: a day at rest loses 2.4 litres of 42 in any
  pieces and nothing backwards; lethal a little under three days in (2.625) with the hours to collapse to match; the
  words a hair either side of each threshold; the published capacity table at its points and straight between them;
  a drink a litre and a half at most, only what the body is short of, nothing from nothing; a restored body held to
  a fraction; the clock's days at the game's rate, sped and held. `FounderStateMessageTests`: the round trip, the
  kind 22 and the verb and outcomes' numbers, NaN, infinity and anything outside 0..1 refused, a drink laid out as
  a put-down and a NaN point refused. `DrinkWireTests` (in-memory wire, a lake down the western columns, the sea
  down the eastern, a dry creek bed between): the body loses on the world's clock (a held clock holds it; three
  seconds at sixty times is a tenth of a day) and is told once a second; a drink from the lake gives a litre and a
  half and is told at once; the sea answers salt, and dry ground and the empty creek bed nothing; out of reach and
  beyond the region refused as such; the ceiling is the capacity told, not the server's newer number; a saved
  founder's water comes back at the join; the water's edge judged by the water it stands in. `DevSettingTests`: the
  row moves the mover's own body, told at once, held to its least. `MovementValidationTests`: the ceiling falls with
  the told capacity by the mover's own scaling, and a full run from a body told half its capacity is corrected.
  `RegionSaveTests`: the water lost rides in the file. `EntityWireTests`: protocol 13.
- Each new test's reason was broken once and the tests went red: the body losing at twice the rate; a drink
  uncapped; very thirsty a percent late; the sea drinking like a lake; the ceiling ignoring the capacity; the ceiling
  taking the server's newest capacity; the reader taking any fraction; the join forgetting the saved water (the
  first aim, the file forgetting it, left the join's test green, since that test hands the server a saved player in
  memory); the nearest post judged wet or not; water judged to stand where the surface is no higher than the ground.
  Eleven sabotages, one of them re-aimed.
- The Unity-layer compile: 0 warnings, 0 errors. The host built Release.
- The built game (`Build/Player-FP1c`, the sources at rest) on the gate world: `Tools/world/drink.py` stood the
  founder at east 3280 north 1436, dry ground beside fresh water and within 150 m of the sea. The run: the panel's
  row set the body full; the clock at sixty times the game's rate; "thirsty" at 10.8 s (1.52% lost), "very thirsty"
  at 23.8 s (4.00%); the walk to the fresh water, the drink answered Done, the water 0.9599 to 0.9956; the walk to
  the sea, the drink answered Salt, the water unchanged; 0 errors, 0 corrections, 6 frames, exit 0.
  `thirst_check.py`: GREEN on every row — the loss's slope over the sped window 0.0019048 per real second against
  the restated physiology's 0.0019048 (2.4/42 a day, a day every thirty seconds); every word at its threshold; the
  drink's rise 0.03569 against 0.035714 (a litre and a half of forty-two, less the second's loss between two words);
  the sea's refusal and nothing gained by it. Two earlier runs are recorded for what they taught: the first
  (`Build/Player-FP1`) was refused at the creek's edge (the nearest-post judgement, fixed above); the second woke
  very thirsty from the first run's save and measured no drying (the scenario now sets the body full first).
- The frames (`Artefacts/frames/drink-20260915T102802Z/frames`): "thirsty" with *very thirsty* under the clock,
  "drank" with "water — drink" on the verb line and no word under the clock, "sea" at the shore; at night, the gate
  world's clock standing where the last run left it. The word's look is William's to judge, by the frames or by
  playing.

**Not exercised, or not holding.**

- Death by thirst is not a death (DEBTS): the body reports collapsing from 11% lost and `IsAlive` false from 15%, and
  nothing acts on either.
- The body loses water at rest alone (DEBTS): v1's sweat by the heat balance and the illness's cost wait for the
  thermal model.
- The wake stands four metres from fresh water by the scorer's choice (DEBTS, William's).
- The frames are at night; the scenario does not move the clock's hour. William's eyes on the word in play are owed.
- A founder's slower walk when thirsty was held by the mover's and the validator's tests, not walked in the built
  game; the corpus was not re-run (the wire's own tests cover the new message, and the corpus's clients hold protocol
  13 with the host by the same code).
- The client offers "drink" wherever the streamed water stands within reach; a swamp's wet ground, which the layer
  classes as no open water, streams no depth and is never offered.
