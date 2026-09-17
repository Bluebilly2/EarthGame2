# Contract FP.2 — The night's cold, and death

**Status:** open 2026-09-15, the second beat of the Founder's Path (ruling 31), on William's "continue with whats
next" after FP.1; closed 2026-09-16 (exit record below). Owner: Claude (Fable 5.1). Act I's two kill conditions, cold
and dehydration, "both fully explained on death", and what death is under the Standard mode the owner settled on
2026-09-01.

## What the path says (binding)

- **Night 1:** "At the fire behind the dune: feed it, watch the stars turn, make it to dawn." The enemy of the first
  day is the night. **Kill conditions: cold (the canon ~03:09 death), dehydration. Both fully explained on death.**
- **Death (settled 2026-09-01):** Standard — death returns the founder to the wake beach; the world persists;
  everything carried lies where they fell. Hardcore — one life.
- The body's clocks: warmth kills in hours, thirst in days, hunger in weeks. v1's calibration of the first day
  (`CALIBRATION_B1_DAY_ONE.md`): the loss terms match their published referents; what killed a founder standing still
  was the shivering reserve running out, and a day one is not survivable to someone who stands still.

## What this slice promises

1. **A founder's body has a core temperature and a heat balance**, v1's `BodyState` ported term by term and its
   numbers published physiology restated: 70 kg, 1.8 m² of skin, 80 W at rest, 180 W more walking and 420 more
   running, up to 350 W of shivering for three hours' worth, the tissue's insulation tripling as the cold constricts
   it, the still-air layer on bare skin thinned by the square root of the wind, the clear night sky sixteen degrees
   colder than the air and cloud closing that gap, breathing by Fanger's form, the sun by the Meinel beam on the
   body's projected area. The founder is naked (clothing 0 clo) and stands or walks in the open, with no shelter, no
   fire, no ground contact and no sleep: those are later beats' terms, and this balance is written so they add
   without rewriting it.
2. **The surroundings are the world's own weather at the founder**: `Weather.At` (M1.8a) with the sun's elevation,
   the wind at the body from the ground's openness where they stand (the layer's own rule, read from the terrain),
   and the sky view of a body in the open. The sweat the balance sheds costs water in the thirst's account, and
   exertion raises the water's loss as v1 priced it: the beat pays FP.1's "rest alone" debt except illness. (Amended
   2026-09-16: v1's pricing, a multiple of the whole resting loss, was wrong by physiology and had a walking founder
   dead in fourteen hours; the loss is the breath's water above rest, from the balance's own respiratory term, and the
   sweat. `Hydration.Advance`'s history has the reason.)
3. **The cold is a word, not a bar**, beside the thirst's under the clock: nothing while well, then *chilly* (core
   under 36.7), *cold* (36), *hypothermic* (35), *severely hypothermic* (32). Shivering is involuntary and is not
   shown; the slower walk of a cold body is not modelled here (v1 had none).
4. **Death, and its explanation.** A core at 28 °C, or 15% of the body's water lost, is death. Under the Standard
   mode: everything carried is let go where the founder fell; a new founder wakes at the wake with a full body; the
   client is told what killed them, when and by what numbers, and shows the mechanism in a sentence made in one
   place for the screen and the log alike. The Hardcore mode is not built (new game has no choice of mode yet).
5. **Told, saved and moved as the water is**: the core temperature rides in the founder's state (protocol 14) and in
   the player file (version 5); the panel has a row for it, for the founder who moves it.
6. **The beta arc's bridge** (added 2026-09-16, William: "the first night is no different to any other night, the only
   difference is that the player will need to do things to avoid dying. those things are just not in the game/beta arc.
   maybe a 'beta arc mode' to bridge the unsurvivable night"): while fire and shelter do not exist, the cold does not kill.
   The body cools and the words come, down to severely hypothermic; the core is held a hair above the lethal
   (`GameServer.BridgeCoreC`) and the sun brings it back at dawn. Thirst still kills, since water can be drunk. On for
   every game (`ServerConfig.BetaArcBridge`); down for the scenario that proves death (`-eg-no-bridge`, `+server.bridge
   0`). There is no lesson in any of it (the same ruling): the night is a night, and the body does what a body does.

## Non-goals

Fire, shelter, clothing, bedding, sleep, lying down (ground contact), canopy shade and sky view under trees, illness,
food: each a later beat's term, added to the balance, not this one's. The Hardcore mode and the choice at new game.
A forecast (the tablet's night question): the balance is written so it can be asked, not asked yet.

## How it is proved

`dotnet test`, each new test's reason broken once: the balance's terms against v1's constants restated in the tests
(a naked body at 9 °C in a 2 m/s wind loses what the published clo figures say; the sky term at freezing is about
39 W in the open and nothing under cloud; the sun at a winter noon gives a bare body about what v1 measured; the
words at their thresholds; shivering's three hours); a night at the wake's own date and place lived through in
the engine (hours to hypothermia, the death's hour, walking buying hours); death over the in-memory wire (the things
let go where the founder fell, the new founder at the wake with a full body, the client told, the sentence); the
save's round trip; the panel's row; the bridge holding a core the panel puts below the lethal, and thirst killing under
it. The Unity-layer compile. Then the built game with the bridge down (`-eg-no-bridge`): a `night` scenario run by
`Tools/world/night.py`, the founder stood at the wake with a stick in hand and walked a dozen metres off, the clock put to
the evening and sped sixty times, recording the core and the words with the sky and the world's hours as the night goes
by, the first "cold" a frame and the dawn a frame; then, unless the night itself killed, the core moved down by the panel's
row to the death, the respawn and the stick lying where they fell, a frame of the sentence; `Tools/verifiers/checks/cold_check.py`
lives the recorded night again in Python, once in the open's wind and once in still air, and holds the recorded core
between the two, the words to their thresholds and the death to its rules. The words and the sentence on screen are
visual changes: frames, or William's eyes.

## Exit record (2026-09-16)

- **The suite**, whole tree: 674 tests green (`dotnet test Engine/EarthGame.slnx`), the Unity-shaped compile and the host
  green. Every new test's reason broken once and seen red, then restored byte for byte: sixteen of FP.2's (thirteen on
  the body, the wire, the save and the panel; then the bridge's floor, the salt wind's floor and the no-record sky), none
  alongside a Unity build (WORKING.md's trap).
- **The built game** (`Build/Player-FP2c`, the third night run, `Artefacts/frames/night-20260916T073045Z`, with the bridge
  down): the founder stood at the wake with a full body, a stick spawned two metres ahead, faced and picked up, walked a
  dozen metres into the forest behind the beach and stood still. The clock put to 22:00 and sped sixty times: *chilly* and
  then *cold* within the first world hour (the "cold" frame at 22:26), *hypothermic* before one, *severely hypothermic*
  after three; and the night itself killed the founder at 05:47, forty minutes before the sun, the sentence on screen:
  "You died of the cold at 05:47. A bare body in 6° air and a 1.4 m/s wind loses about 240 W and makes 113; shivering
  held the core for a while and ran out, and at 28.0° the heart stops. A new founder wakes on the beach; what you carried
  lies where you fell." The new founder stood at the wake (0.0 m off) and the stick lay where the last fell (0.0 m off);
  six frames (cold, dawn, died at 1440p and 1080p), no errors. The panel's death was not needed: the night was death
  enough, and the scenario takes either.
- **The check** (`cold_check.py`) GREEN, every row: the recorded core lay between the still-air and the open-wind
  curves over the ten words of the natural night (7.3 world hours), never under the floor; the words at their thresholds;
  the death's core below the lethal (27.97) with the last word told above it (28.42); the wind the death names (1.4 m/s)
  within the open's (1.5); the wake and the stick as above.
- **What the run taught, and changed.** The second run died in the recorder: the stick was never picked up (a fixed
  pitch of the crosshair passed half a metre over a stick two metres off; the founder now looks at the thing itself, as
  the carry scenario does) and the missing stick left a NaN for a record, which the JSON writer rightly refuses (a number
  that is not one is now left out of a record). The first check timed the night by real seconds at the clock's rate,
  which a frame's hitch stretches (the in-process server catches its steps up); it times it by the server's ticks now,
  the body being stepped once a tick and told every twentieth. The client's own clock lagged the change of rate by a
  word and under-timed the first hour by half (DEBTS: the HUD's clock and a death's hour can disagree by three minutes
  after the panel changes the rate).
- **What this world's night is.** Standing still and naked in the forest behind the wake beach, a founder dies of the
  cold before dawn on an ordinary late-winter night here (air 6 °C by morning, 1.4 m/s at the body); in the engine's
  test of the shore itself the same night left a standing founder at 28.7 °C and alive under M1.8a's weather, and kills
  them at about dawn under the lighthouse's own record, whose night wind is the coast's (M1.8c, 2026-09-16). Under the
  beta arc's bridge (every game until fire and shelter exist, ruling 33) the same founder is held at 28.5 °C, severely
  hypothermic, and warmed by the sun; walking, they are never cold.
- **Owed** (DEBTS): the bridge's removal when fire and shelter exist; the Hardcore mode and the choice at new game;
  the body's missing terms (shelter, fire, clothing, bedding, the ground, sleep, canopy); illness; the client's clock
  after a change of rate. William's eyes on the words, the sentence and the panel's row are still owed: no visible
  window was opened.
