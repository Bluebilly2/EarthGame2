# Slice 2.7 — The oracle's first words

**Status:** shipped. Contract and measurements together.
**Phase:** 2 — The First Survivor. This is the last gate item.

## Why

The founder wakes holding a tablet that knows everything and lifts nothing. Until now it has been
able to name what is under the crosshair and nothing else. Two things were missing, and the Phase 2
gate turns on both:

- **What can I attempt, and what is stopping me?** (GAME_DESIGN §5)
- **Why did that fail?** (GAME_DESIGN §23, and the gate's own wording: wrong technique must fail
  *for the correct physical reasons with causal-log explanations*)

Without the second, a wet hearth board and a hopeless one look identical from the outside — an hour
of drilling and no fire — and the founder learns nothing from either. That is the difference between
a game that is hard and a game that is unfair.

## The design constraint that shapes everything

**The oracle cannot disagree with the world.**

Every answer it gives is a live query against the same model that decides whether the attempt
succeeds. It stores nothing, unlocks nothing, and has no knowledge of its own. Ask it whether a
stone will flake and it asks `StoneType.Knappability`; ask whether a fire will light and it builds
the actual `FrictionDrill` and reads its plateau against its ignition point.

This is not a technology tree (§6). Nothing is authored except the *questions*. If the mineralogy
changes, the advice changes with it, and nobody edits a file.

Two tests in the suite exist purely to hold this line, and they are the most important tests in the
slice: they walk every stone in the catalogue and every moisture from 0 to 30%, put the tablet's
verdict beside what actually happens, and demand they agree.

It also forced a real refactor. The body's heat loss lived inside `BodyState.Tick`, so a forecast
would have had to be a second copy of it — one that agreed today and drifted within a month. The
loss terms are now `SensibleLossW`, called by both `Tick` and the new `Forecast`. **An oracle that
quietly stops matching the world is worse than no oracle at all.**

## What it says

Five capabilities, each assembled from live readings. Every requirement carries three things: what
is needed *in physical terms* rather than as an item name, the measurement, and the mechanism.

| goal | method | preconditions |
|---|---|---|
| A cutting edge | struck flake, hard hammer | conchoidal stone · a tough hammerstone · arm enough for the critical load · a hammer small enough for the work |
| Cordage | reverse-wrapped two-ply | a plant with long fibres · retting time (bark only) · dressed · enough to lay |
| Fire | hand drill | a light soft spindle · a board · wood dry enough to pass the water in it |
| A night that does not kill you | whatever has been built | something under you · a roof · more heat than the night takes |
| Water | running fresh water | no salt in it · close enough to be a supply |

Sample output, measured in the player at the beach spawn on day 1:

> ✗ **something between you and the ground**
> lying on bare ground at 20 W/m²K
> *Earth conducts heat away far faster than air does, and it never warms up. Most people who die of
> cold outdoors die through the ground.*

## The night, which is the one that matters

The most useful thing the tablet can do is answer *before dark* whether the afternoon's work was
enough — a question the founder cannot answer by looking, and finds out the hard way at 3 a.m.

Measured in the player, naked on bare ground at the beach spawn:

| | |
|---|---|
| tonight's minimum | 6.5 °C (read at 05:00, when it actually bottoms out) |
| leaving | **748 W** |
| arriving | 422 W (80 resting + 342 shivering) |
| short | **326 W** |
| cooling | 4.8 °C an hour |
| to hypothermia | 0.4 h |
| shivering covers | 350 W, for 3.0 hours, and then stops |

That last line is the one that teaches. Shivering is the body's only lever and it runs out; anything
not covered by insulation before then is not covered at all. It is exactly the trap that kills
people who felt fine at midnight.

The forecast assumes a **clear sky** where it has no reading, because a forecast is about a night
that has not happened and clear is the assumption that kills. `BodyState.ClearSkyDepressionK` — 16 K
below ambient — had been a number in a comment; it is a constant now.

## Why did that fail?

The existing `CausalLog`/`CausalEvent` graph and its `ExplainTree` renderer were already in the
codebase for §23 and had nothing filling them for the survival systems. `Diagnosis` fills them, and
enforces one rule: **a description must carry a number.** `Diagnosis.Measured` refuses one that does
not, and every builder goes through it.

Actual output from the player, from a blow with not enough behind it:

```
Nothing came off: 0 g removed from a flint core
  ← The blow was under the critical load: 3.4 J against the 6.0 J this pairing needs
    — a 1.4 kg hammer presents a 4.9 cm radius, and critical load rises with the square of it
```

And from a night with nothing built:

```
The night is not covered: short 326 W, which is 4.8 °C an hour and 0.4 hours to
hypothermia; shivering can cover 350 W of it for 3.0 hours and then stops
  ← The ground: 0 mm of bedding — lying straight on earth that conducts at 20 W/m²K
  ← The sky: open, and radiating as though it were 16 K below the 7 °C air
```

The observed failure sits on the first line and the root cause at the bottom, which is what makes it
read as an explanation rather than a list.

## What the tablet does not know

Its sensors are cameras and microphones (ROADMAP), so it knows what it can see and what the founder
is carrying. Where the world has told it nothing — the distance to fresh water in country it has not
walked — it carries infinity rather than a guess. This matters more later, when latent state
(§14, §26) starts deciding outcomes, and it is set up now rather than retrofitted.

It also never says what to do. It identifies, measures and explains; the decision is the founder's,
and taking it from them would be taking the game from them.

## The claim was violated within an hour of being written

Writing the fire capability, the oracle picked a spindle and a hearth board by density thresholds —
under 350 kg/m³ for the spindle, under 700 for the board. `FireBuilder.BestPair`, which is what the
founder's hands actually use, does something else entirely: it sorts every piece of wood carried by
density and takes the two lightest, with no thresholds anywhere.

So the tablet could have said *you have a kit* about a pairing the drill would never pick up, and
said *you have no kit* to a founder carrying two pieces of ironbark who could have tried and failed
for an honest reason. Two rules for one question, which is the precise failure this slice exists to
be incapable of — and no test caught it, because both were self-consistent.

The rule now lives in `FireDrillKit.BestPair` in the Sim assembly and both call it. This is the
general lesson: **the oracle cannot be trusted to ask the right question; the question has to be
somewhere only one copy of it can exist.** Every capability that could be asked through the world's
own function now is.

Writing the test that locks that agreement in then turned up a bug in the rule itself. It considered
anything made of wood, and bark fibre at 240 kg/m³ is lighter than a softwood branch — so the
founder's **tinder was being chosen as the hearth board**, and consumed as part of the kit on a
successful fire, leaving the coal nothing to be dropped into. It had gone unnoticed because bark
fibre happens to have drill-friendly properties, so the fire lit anyway and the test passed. You
cannot drill a notch in a wad of fibre; the rule now takes solid wood only.

## A model gap the tests found

`ItQuotesAuerbachWhenTheHammerIsTooBigToSwing` failed, and it was right to. Swing energy went as the
hammer's mass while Auerbach's critical load goes only as mass^(2/3) — so a heavier hammer was
*always* better and the law had no teeth. A person cannot accelerate a heavy stone the way they can
a light one, so there is now an `ArmEnergyCeilingJ` of 40 J. Below about 2 kg nothing changes (a
1.4 kg full swing is 32 J, as documented in Slice 2.3e); above it, the ceiling is what makes a
hammerstone's size a real choice rather than a number to maximise.

## In the world

**T** opens it, **Tab** switches page, **T** puts it away. It takes the whole screen, because
looking at it is a thing the founder is doing instead of looking at the world.

The shelter it reports is the one the founder is standing in, or failing that the one they have
built and are standing beside — planning the night happens *outside* the shelter, in the light,
while there is still time to add to it.

Verified rendered, with `-eg-tablet-open` and a recorder frame. Three things only a picture could
have caught: the backdrop was transparent enough that pale sand showed through a wall of small text,
the panel stopped well short of its own footer, and long mechanism sentences wrapped back to the
panel's left edge instead of holding their indent, so every two-line reason ended in what looked
like a stray fragment. The reasons are measured and wrapped against the style itself now, since the
font is proportional and a character count would be a guess.

## Validation (§36)

25 tests in `Assets/EarthGame/Tests/OracleTests.cs`; suite at **280 passing**.

| claim | check |
|---|---|
| it cannot disagree about stone | every stone in the catalogue: tablet's verdict against an actual blow |
| it cannot disagree about fire | moisture swept 0–30%: tablet's verdict against the drill's plateau |
| and it picks the kit the hands pick | a mixed bag including tinder: same spindle, same board, same verdict |
| the forecast matches a lived night | forecast cooling rate against 60 minutes of real `Tick`, within a factor of 2 |
| nothing is a verdict | every requirement in every report carries a reading and a mechanism |
| the night outranks the rest | a naked founder at 4 °C is told about the night, not about cordage |
| one stone is not a kit | flint alone is blocked on the hammerstone |
| bedding is worth more than walls | > 40 W saved, measured through the shelter model |
| a roof is worth real watts | > 20 W at 4 °C, taken as the difference between the same two forecasts |
| bark cannot be hurried | retting reported as days submerged, not as work outstanding |
| the sea is never water | refused, and for the osmotic reason |
| a cause must be measured | `Diagnosis.Measured` throws on a description with no number |
| failures explain themselves | drill chain reaches the water in the wood; blow chain reaches the hammer's radius |
| root cause last | the observed failure appears above its cause in the rendered tree |

Wiring proved in the player by `TabletSelfTest` (`-eg-newgame -eg-tablet-test`): the air temperature
it forecasts from is the body's own to 0.01 °C, it holds the body **instance** rather than a copy,
picking two stones off the bench flips a blocked capability to ready and the knapper agrees, and a
wasted blow reaches the log with the hammer's radius in it.

## Something else it turned up

`SleepSelfTest` has been failing three checks, and neither the code nor the model was at fault. Its
`TiredHours` constant carries a comment saying the test should *"start at dusk instead"* — but the
run instruction four lines above it still said `-eg-hour 8`. Launched in the morning it runs 1.5
hours forward, arrives at 09:30, and correctly reports that this is not the evening. The instruction
now says `-eg-hour 18` and it passes. Nothing was wrong except what the file told you to type.

## Out of scope

The dependency graph beyond Phase 2 (§5's electricity example), the tablet as a physical object with
ports, plant identification through the camera as distinct from proximity, and anything that lets it
act.
