# Contract BF.5 — Fire

**Status:** part one, the fire's physics in the engine, drafted and built by a cloud session on the branch `cloud/bf5-fire-physics` on 2026-09-25 and taken into main the same day, as written, with main's answers to its questions below. The parts after it are named here and not built; part two waits on William's word on the clock (For main to decide, 1). Owner: Claude (the main session). William's lane: his word on the clock; his eyes on the fire drawn and his hands on the drill when parts three and four stand.

## Why this next

BETA_MAP §7 puts BF.5 after the ground. Its goals are G6, fire before dark from found wood and judged tinder, and G7, the
first night lived through at the fire; F5 is depended on by G6, G7, G8 (roasting), G10 (the fire ring), G11 (drying) and G12
(the night slept warm). Ruling 33's bridge holds the cold's death off "until fire and shelter exist": fire is half of what
takes it down. And of the five things a developer must see, one is fire's own: *damp tinder fails and says why*
(FOUNDERS_PATH). GAME_DESIGN §8 says how: what burns, how fast and how hot must come out of the wood's properties, its
density, its thickness and its water, and never out of a label ("the fire-drill wood", "kindling"). Here even the fuel
classes are only words: that tinder catches from a coal, kindling from tinder's flame and fuel from kindling's fire falls out
of each piece's thickness and water.

## The shape

Fire is five parts, each shippable alone:

1. **The fire's physics** (this part): pure functions and one stepped model in the engine, from published figures, with the
   estimates named. Nothing on the wire, in a save or on a screen.
2. **The fire in the world**: a fire as a thing the server steps and saves and tells its viewers of, burning in the weather's
   wind; the body's warmth taking its term (the hook, For main to decide 2).
3. **The verbs**: the kit made by work, the drill held, the coal laid in tinder and blown, the fire laid and fed.
4. **The fire seen and heard**: flame, glow, smoke and embers by phase and heat, the fire's light, its sound.
5. **The bridge taken down**: when a night can be lived through by doing things (fire here, shelter in BF.6).

## Part one: the promises (built on the branch)

1. **The heat of wood, less what its water costs.** `Combustion`: the table's gross heat (`Wood.HeatMJPerKgDry`, 19.0 MJ/kg)
   less its hydrogen's water by the solid-fuel standards' equation (212.2 J/g for each per cent of hydrogen, 0.8 for each of
   oxygen and nitrogen; ISO 1928's form, which ISO 18125 takes for biofuels; wood 6 % hydrogen and 44 % oxygen, Rowell et al.
   2013): 17.69 MJ/kg net; less 2.443 MJ for each kilogram of its own water (ISO 16993's 24.43 J/g per per cent). As cut at 20
   per cent of its wet mass, 13.66 MJ/kg.
2. **Char and gas.** A fifth of the wood the flame passes stays as char, the embers (Tran 1992; 0.16 to 0.23 in NIST's
   microscale calorimetry), at 32.6 MJ/kg (NIST's Douglas fir char, Leventon, De Lannoye and Greene 2025); the rest burns as
   gas carrying the remainder, 13.96 MJ/kg (NIST measured Douglas fir's gas at 12.8 ± 0.9 and western red cedar's at 13.9), so a
   piece burnt to ash gives exactly its net heat. An open flame burns 0.95 of its gas, an estimate placed so a kilogram lost in
   flame gives 13.3 MJ, inside the cone calorimeter's effective heat of combustion for wood (13.0 to 14.7 MJ/kg, Tran 1992). A
   piece's char takes half the room of the wood it came from (an estimate) and glows on that surface at 1.5 g/(m²·s) in still
   air, times √(1 + U / 0.5 m/s) in a wind (an estimate, held to Ellis's measured glow of eucalypt bark brands, 2015).
3. **How fast a piece burns, by its thickness and its water.** The crib's burning rate by the thickness a stick was laid at,
   R / A_s = 1.08 × 10⁻³ b^−0.5 kg/(m²·s) with b in metres (McAllister and Finney 2015's 1.08 × 10⁻³ g/(s·cm^1.5): 10.8
   g/(m²·s) for a centimetre stick), its surface moving in at that over the wood's density. At a going fire's heat a 4 mm twig
   of coast banksia (640 kg/m³) burns through in 1.2 min, a 10 mm stick in 5, an inch-thick one in 20, a wrist-thick one in 55,
   a 10 cm log in 2.6 h and a 20 cm log in 7.4 h; blackbutt (900 kg/m³) takes two-fifths as long again (a wrist-thick log 78
   min), which is why dense wood is the long-burning fuel. Babrauskas's COMPF2 rate (1.7 × 10⁻⁶ D^−0.6 m/s, 1979) agrees within
   a fifth from 4 mm to 10 cm at 450 kg/m³, and Eurocode 5's furnace charring rates (0.50 to 0.65 mm a minute) bound a thick
   log's. Slowed by water as L / (L + u × 2.59 MJ/kg) against air-dry wood at 8 per cent, L = 3.2 MJ/kg fitted to the one
   published point found (8.3 per cent slower at 20 per cent than at 8, Babrauskas 2005): green wood (0.6) at 0.72 of an air-dry
   stick's pace. Sped by the wind as √(1 + U / 1 m/s), 1.3 at 0.7 m/s, inside the 6 to 62 per cent McAllister and Finney
   measured for cribs there (2016); an estimate beyond.
4. **A fire of pieces that builds and dies as fires do.** `Fire` and `FuelPiece` (a stick, or a bundle of fibre that burns as
   fast as a flame crosses it). A piece's heat is the share of its view the other burning pieces fill, A_others / (A_others +
   A_self) (a stated rule), at 60 kW/m², the low end of the 60 to 80 measured inside open cribs (Thomas 1967), and its own
   flame's (25 kW/m² at 6 mm, going as the inverse root of the thickness: an estimate); it catches by Babrauskas's correlation or
   the thin-piece limit, slowed by water as McAllister, Finney and Cohen measured (1.47 times as long at 18.5 per cent); it flames
   while its gas passes the critical mass flux for its water and the wind (their 1.3 to 1.9 g/(m²·s) dry, 56 per cent more at
   18.5), and glows or smoulders if not; glowing with nothing round it and no char left, it goes out. `Advance(seconds, air)`
   walks any span in steps of at most a second. The tests' lay (10 g of paperbark tinder, twenty 4 mm twigs, ten 8 mm and six
   15 mm sticks of coast banksia, three 5 cm blackbutt logs: 3.1 kg dry) catches its logs in a minute, peaks near 34 kW as the
   kindling flares, flames for nearly two hours at a mean of 5 kW, and is out, embers and all, at a little over three, having
   given 52 MJ, 16.8 MJ for each kilogram of its dry wood. A lone 5 mm stick flames for six minutes; a lone 25 mm one holds no
   flame; a tinder bundle under logs with no kindling between lights nothing.
5. **Phases and why.** `FirePhase` (Out, Smouldering, Flaming, Embers), `Fire.Describe()` and `Fire.WhyNot(piece, air)`: "the
   fuel wood smoulders: it is sodden (60 % water) and its steam smothers the flame", "the tinder smokes and will not flame: it is
   damp (27 % water), and a flame will not cross fibre wetter than 24 %", "the fuel wood is heating: it should catch in about 80
   s if the fire holds", "the fuel wood smoulders: too little fire around it to keep a flame", "the wind strips its flame";
   "a fire in flame, about 19 kW: 37 pieces alight, 3 not yet caught".
6. **Fire by friction.** `FrictionFire.Judge(method, drill, its water and tip, hearth, its water, the body's capacity)` for the
   hand drill, the fire plough and the bow drill, as a chain a tablet can explain:
   - *the power in*, friction's μ = 0.25 (Duncan 2021) times force times speed, two-thirds of the rim's speed for a spun tip:
     6 W for a hand drill, 30 W for a plough, 18.6 W for Duncan's bow drill (his "about 21 W"), scaled by thirst's capacity;
   - *the heat reaching the hearth*, the woods' effusivities √(kρc) parting it (the Wood Handbook's k and c): two-thirds into
     a coast banksia hearth under a grass-tree drill;
   - *the temperature*, the rim of a spun tip heating at 1.5 times the tip's mean (the notch is cut there) as a semi-infinite
     solid's surface does, its water's latent heat on the way: it smokes at 250 °C and glows at 340 °C (GESTIS-DUST-EX's wood
     dust layers, 310 to 340);
   - *the coal*, 0.1 g of hot dust gathered (Duncan's heated mass), the dust's yield by the hearth's density band (a dense wood
     polishes, a punky one crumbles); every grain ground off brings its water, and past 20 per cent boiling it takes more than
     the rub gives;
   - *the arms*, a minute at a hand drill's or a plough's pace, two at a bow's.
   A grass-tree drill on coast banksia makes a coal in 25 s air-dry (Hough 1890: "fire in thirty seconds by the twirling
   sticks"), 30 s at 15 per cent, and at 22 per cent "the dust steams and will not glow … air-dry it would have taken a coal in
   23 s"; on grass tree 16 s; on blackbutt "too hard (900 kg/m³): the rub polishes it to a glaze"; a 20 mm drill tires the arms
   first and says its broad tip spreads the heat; the plough on a grass-tree board 41 s (Hough's Samoan plough, forty seconds),
   on banksia the arms give out; a bow on banksia 7 s (Hough's bow drill, five); Duncan's own bow drill 33 s against his measured
   23 to 24.
7. **Tinder from a coal.** `Tinder.Catch(coal, bundle, blowing, wind)`: the coal glows at the char's rate (0.1 g gives 13 W for
   four minutes left alone, 34 W for 96 s blown); the nest round it must be heated and dried before the coal is spent; a flame
   will not cross fibre wetter than Rothermel and Anderson's 23.6 per cent (1966, INT-30), and a smoulder turns to flame only in
   moving air (1.2 m/s, Salehizadeh et al. 2021). Blown, dry tinder flames in 12 s, at 15 per cent in 16, at 20 in 19, at 23 in
   36; at 25 per cent "the tinder smoked round the coal and would not flame: it is damp (25 % water), and a flame will not cross
   fibre wetter than 24 %"; unblown "blow on it to bring the flame"; a coal too small dies; a solid twig is too coarse to take a
   coal.
8. **The fire's warmth on a body.** `FireWarmth.On(flame heat, glow heat, fire width, distance, posture, bearing)`: the point
   source q = χQ / (4πR²) (Modak 1977) with χ 0.30 for flame (0.21 to a third measured) and 0.5 for glow; the body's area square
   to the fire by Fanger's projected area factors, standing or sitting, by the fire's angle below the body's middle and its
   bearing from the body's front (Fanger 1970, as ASHRAE 55-2020 Addendum d carries them), times the share of the skin that
   radiates (0.725, 0.696) and `Warmth.SkinAreaM2`; the skin takes 0.95 of it; past 2.5 kW/m² "too hot to bear this close". On a
   clear 6 °C night with 1.5 m/s at the body, a naked founder at rest runs 240 W short by `Warmth`'s own terms; the warming fire
   (14.0 kW) gives a seated founder 287 W at 0.6 m (773 W/m² at the body), 224 W at 0.7, 178 at 0.8 (125 with the back to it)
   and 120 at a metre, and a standing one 69 to 92 W over the same distances, the fire sitting far below a standing body's
   middle; at 10 m about a watt. That absorbed heat is the term the hook would add.
9. **The night's arithmetic.** `FireFuel`: a small fire eats 0.57 kg of dry wood an hour (the three-stone fire at simmer in the
   Water Boiling Test, 9.49 g a minute), a cooking fire 1.5 (at high power, 24.1 to 25.6 g), a warming fire 3.0 (a fireplace's
   typical burn, US EPA); carried at 15 per cent water, 0.65, 1.73 and 3.45 kg an hour, and a thirteen-hour winter night at the
   warming fire 45 kg. Their heat by this model's own accounting is 2.7, 7.0 and 14.0 kW: the accounting the `Fire` releases, so
   a fire of so many watts eats what this says (the lay of promise 4 gave 16.8 MJ for each kilogram of its dry wood, the same
   figure).
10. **Every number carries its source; an estimate says it is one.** In the doc comments, author, year and title or URL; the
    estimates are gathered in DEBTS rows proposed below.

## The parts after it (named, not built)

- **Part two, the fire in the world.** A fire is a thing where it was lit: an entity whose component holds its pieces (a list,
  which the store's components do not yet have), stepped by the server as a slow layer (`ISlowLayer.AdvanceTo`), every tick
  near a founder and in one catch-up far off, which `Fire.Advance` already allows. Its state saved in the region file (a new
  change layer or component: the region file is at 5) and told to viewers (protocol 20 to 21) at a sensible rate: its phase,
  its heat and each piece's remains. The weather's wind at the fuel's height; rain on the fire and wetting its pieces with
  BF.7's wetting; the fire's warmth in the body's balance through the hook, from every fire within 10 m of a founder (at 10 m
  the warming fire gives about a watt). The fire's hollow in the ground, when it has one, is DEBTS 2026-09-23's hole.
- **Part three, the verbs.** In BF.2's model: split a stick, notch a hearth, carve a drill (the named kinds BF.2 left for this),
  shred bark to tinder (a bundle of fibre from a strip, `FuelPiece.Bundle`), the friction work as a hold whose offer is
  `FrictionFire.Judge` and whose words are its words, lay the coal in the tinder and blow, lay the fire and feed it, and shelter
  a lighting from the wind (For main to decide 13); the kit's pieces are BF.1 things read by `FuelPiece.OfStick`. The fire
  kit's scenario that knaps a flake and carves a drill pays DEBTS 2026-09-22's "the point is not in a built run".
- **Part four, the fire seen and heard.** Flames by the flame's heat, glow and embers by the char, smoke when it smoulders, the
  fire's light on the world at night, and its sound (F10).
- **Part five, the bridge down.** When fire (this) and shelter (BF.6) stand, `ServerConfig.BetaArcBridge` comes off and the
  cold kills under the Standard death, as DEBTS 2026-09-16 and ruling 33 say.

## Non-goals of part one

No wire, save, server system or client. No rain on a fire and no drying of wood by the fire or the sun (BF.7's wetting and
drying). No smoke as a thing, no fire spreading to the litter or a tree, no cooking, no charcoal making. No change to
`Warmth`, `Surroundings`, `ThingWords`, `Wood` or any other file that exists.

## How it is proved

- Tests, the numbers written again in them rather than read from the classes: `FireBurningTests`, `FrictionFireTests`,
  `FireWarmthTests`. Each published figure against its source, and a second source wherever one was found: Krajnc's FAO
  table of firewood's heat by its water beside the standards' equation; NIST's char and gas heats and Tran's effective heat of
  combustion beside the char's share and the flame's efficiency; Babrauskas's COMPF2 rate and Eurocode 5's charring rates
  beside McAllister and Finney's crib law; Babrauskas 2005's charring point beside the water's slowing; McAllister, Finney and
  Cohen's measured ignition times beside Babrauskas's correlation; Rothermel and Anderson's spread and extinction for the
  tinder; the published fit for a standing body (Jendritzky and VDI, via Di Napoli et al. 2020) beside Fanger's table;
  `Warmth`'s own sun share beside Fanger's; Duncan's measured bow drill and Hough's timings for the friction chain; the Water
  Boiling Test's firepower beside the cooking fire's heat.
- The behaviour a fire must have, as tests: the ladder from tinder to logs, the lone stick that goes out, the log over a
  handful of bark that never catches, green wood that smoulders and says why, damp tinder that will not flame and says why,
  wind that feeds a going fire and hurries a tinder bundle's flame (half a minute still, three seconds in 3 m/s), the phases to
  out, each piece's account equal to the fire's and within the wood's heat less its smoke and water, the same fire at a
  quarter-second step and a minute's step, the same fire twice.
- `dotnet test Engine/tests/EarthGame.Tests > test-results.txt 2>&1; echo $?`, with the counts in the exit record; the suite's
  source scans (no Unity, no `#if`, no clock or unseeded random, no to-do markers) run over the new files.
- Sabotage, each restored byte for byte, in the exit record.
- Not run here, as the brief says: the Unity-shaped compile (it needs Unity's own libraries), the edit-mode tests and the built
  game. The new files are C# 9 for .NET Standard 2.1 with no Unity and no `#if`, and dotnet's build refuses at LangVersion 9 with
  warnings as errors what Unity's would.

## For main to decide

1. **The clock the fire burns on.** The world runs a day in thirty real minutes, forty-eight times the real rate, and the body
   lives on that clock. A fire must burn on it too, or the night's arithmetic lies to the body: the warming fire then eats its
   3.45 kg of an hour in 75 real seconds and a night's 45 kg in sixteen real minutes, a wrist-thick log every twenty-odd real
   seconds. Friction is a work in the player's real seconds (BF.2's clock), and a blown coal's 96 s of world time is two seconds
   of real time. Recommended: the fire on the world's clock with the body; the coal made, laid in tinder and blown as one work
   in real seconds, so it cannot die in the player's hands between the drill and the nest. Whether feeding a fire that often
   through a night is the game is William's taste.
2. **The hook in the body's balance.** Proposed: `Surroundings` gains `FireGainW`, the heat the body takes from fires near it
   (zero where there is none), and a posture; `Warmth.Tick` adds `FireGainW` to the surplus beside `SolarGainW`, so a founder
   too near a big fire sweats it off as the sun's surplus is shed; `Warmth` shows it (`FireGainW`) beside its other terms for the
   log and the tablet; the server works it out with the surroundings once a second from `FireWarmth.On`. A seated body also
   sees less of the sky than `Warmth.StandingSkyView01`. One assembly, as FP.2 kept it: the forecast and the body read the same.
3. **One owner of the body's projected area.** `Warmth`'s sun uses its own share, 0.25 of the skin falling to a twelfth
   overhead; `FireWarmth` uses Fanger's tables. They agree within a third at every altitude (a test holds them together), and
   should be one: `FireWarmth.ProjectedAreaFactor` moved to the body (a `BodyProjection`) and read by the sun too, which would
   give the sun a seated body as well.
4. **The wood table's re-read** (DEBTS 2026-09-22, owed "before BF.5's fire physics reads a hearth's density and hardness").
   Checked here against second sources: blackbutt 900 kg/m³ (WoodSolutions 900 air-dry; Business Queensland 930 at 12 per
   cent, citing Bootle 2005; the Global Wood Density Database's basic 0.76), Janka 8.9 kN and a modulus of rupture of 144 MPa
   dry (WoodSolutions) against the table's 9.1 and 116; bangalay (WoodSolutions' southern mahogany) 920, 9.0, 130 against 900,
   8.5, 100; old man banksia basic 0.57 (about 700 air-dry, the table's); coast banksia basic 0.48 (about 580 air-dry, the
   table 640, ten per cent heavy); the paperbark, Melaleuca quinquenervia, basic 0.58 (about 700, the table's), with
   WoodSolutions' broad-leaved tea tree (M. leucadendron) at 745, 7.3 and 97; the "swamp paperbark" of the south coast may
   rather be M. ericifolia, a naming for main to settle. The gross heat, 19.0, is low against the Wood Handbook's "around
   20 MJ/kg" (ch. 18) and White's 19.6 to 21.5 for hardwoods; eucalyptus wood measures 19.22 (Phyllis2). Fire leans on the
   densities (the friction band, the hearth's share, a stick's mass, and now how fast a stick burns inward) and on the heat;
   coast banksia at 580 would burn a tenth faster than at 640, and none of these differences moves a conclusion of part one.
   The table is not changed here.
5. **The table's density is air-dry.** `Wood.DensityDryKgM3` says "air-dry density (about 12 % moisture)" and is read as dry
   mass per volume by `LyingProperties` and here, so a stick carries 12 per cent more dry wood than it has. One reading, one
   fix, in the table or in its readers.
6. **"Dry" in the words.** `ThingWords.MoistureWord` calls wood dry under 0.20 of its dry mass. Friction makes a coal only
   under 0.20 and a flame crosses tinder only under 0.236; the lying sticks of the driest ground (0.15, `LyingProperties`'
   first quarter) make fire and the second quarter's (0.22) do not. The words hold, and the failures say "dry to the hand and
   not dry enough for friction fire" when a "dry" wood fails; an "air-dry" or "bone-dry" word may serve the player better.
7. **The tinder's moisture limit.** The model takes Rothermel and Anderson's 23.6 per cent, the moisture past which a flame
   will not cross a bed of fine fuel in still air. Lone glowing brands failed to light beds of grass and mulch at 11 per cent
   (Manzello et al. 2006), and Blackmarr's limits run 16 to 40 per cent by the igniter (1972). A blown coal in a nest is
   nearer the first; main may want a stricter limit.
8. **The stated estimates.** Each is named in its doc comment and would be a DEBTS row to measure: the view rule, and the low
   end of Thomas's heat inside a crib taken for a campfire's looser lay; a flame's own heat by thickness; the flame's
   efficiency; the char's glow and its room; the wind's part past the measured 0.7 m/s (burning) and 1 m/s (holding a flame);
   the green wood past McAllister's 18.5 per cent; the bundle's blow-out wind; the cooling of a piece that loses its heat; the
   coal's density, the nest's reach and its share of the coal's heat; the tinder's millimetre; the hand drill's and the
   plough's forces, speeds and endurances; the dust's yield and its hot grind; the bodies' heights; the glow's radiant share.
9. **Distributions (§9).** Part one is deterministic. An attempt at friction fire has real uncertainty in the grain, the notch
   and the hands (§21); part three's work could spread the coal's time with a salted draw, as `Work.Apply` does.
10. **The fire's state for the wire and the save** (part two): per piece its thickness, dry wood, water, char, progress to
    catching and phase, and its wood by species; which record carries a list of pieces.
11. **The Fanger tables' notice.** The two tables of projected area factors (thirteen bearings by seven altitudes, standing
    and seated) were copied into `FireWarmth` as published data from the pythermalcomfort library's `solar_gain.py`,
    identical to ASHRAE 55-2020 Addendum d's. The download, recorded as ruling 49 asks: `https://raw.githubusercontent.com/
    CenterForTheBuiltEnvironment/pythermalcomfort/master/pythermalcomfort/models/solar_gain.py`, fetched 2026-09-25 from the
    library's master branch, 14,300 bytes, SHA-256 `0a79e87c23bdd92e4f17a73699e250f07ee0ed08710b7d51f1c8331ed43346de`, MIT
    licence (© 2019 Federico Tartarini, the repository's `LICENSE`); the same URL fetches it again, though the branch moves
    and the hash is of this copy. What lands in the repository is the tables' numbers, not the file. Whether
    `THIRD_PARTY_NOTICES.md` wants an entry for them (the house rule and ruling 49: a vendored file lands with its notice) is
    main's, since this proposal changes no existing file. The papers read for the sources were downloaded to the session's
    scratch space only.
12. **The LFS fixture.** `Data/fixtures/raster/tiny.r32` is committed as a Git LFS pointer whose object the server does not
    have (it answers 404), so every clone without the owner's own LFS store fails 39 tests. This session rebuilt the 100 bytes
    from its sidecar's law (`Tools/data/write_fixtures.py`), matched the pointer's sha256, and put them in its working tree only.
    A `git lfs push --all origin` from the owner's machine, or the fixture committed as plain binary, mends it for every clone.
13. **Lighting in the wind.** With the measured heat inside a fire, a well-laid fire lit in a 3 m/s breeze takes: the tinder's
    flame is gone in three seconds, but its glowing char carries the twigs to catching. An earlier form of the model (see "What
    changed on the way", 5) had the breeze defeat the lighting. Whether a breeze should defeat a lighting more often than this,
    and the lee (a body, a stone or a pit as a windbreak) as part three's verb, is main's; the model puts a tinder bundle's flame
    out only at 8 m/s, an estimate.

### Main's answers (2026-09-25, on taking part one)

Read and taken into main as written; the answers by CANON 46, each the proposal's own recommendation unless it says otherwise.

1. **The clock** is William's (asked 2026-09-25); part two waits on his word. The proposal's recommendation, the fire on the
   world's clock with the body and the coal-to-flame as one work in real seconds, is main's too.
2. **The hook** as proposed (`Surroundings.FireGainW` and a posture; `Warmth.Tick` adding it beside the sun), in part two.
3. **One owner of the body's projected area**: yes, `BodyProjection` in the body, read by the sun and the fire, in part two.
4. and 5. **The wood table**: one change to the table and its readers after this merge, taking the second sources the re-read
   names where they differ by more than a tenth, and naming the density the readers mean (dry mass over volume) with the
   air-dry one beside it; the DEBTS row stays until that lands.
6. **"Dry" in the words**: as they are; the failures already say "not dry enough for friction fire". The tablet (BF.8)
   retunes the words with William's eyes.
7. **The tinder's limit** stays Rothermel and Anderson's 23.6 per cent: a blown coal in a nest is not a lone brand on a bed.
8. **The stated estimates**: DEBTS rows, one a group, in the same commit as the merge.
9. **Distributions** in part three's work, as `Work.Apply` salts its draws.
10. Part two's.
11. **The Fanger tables**: a `THIRD_PARTY_NOTICES.md` entry in the merge (the numbers are ASHRAE 55-2020 Addendum d's, taken
    from pythermalcomfort's MIT-licensed copy, with the fetch's hash as recorded above).
12. **The LFS fixture**: mended on main before the merge (c36f3a2): the fixtures live in git itself, and a fresh clone reads
    the raster.
13. **Lighting in the wind**: as the model has it; the lee is part three's verb.

## What part one asks of existing files (not changed here)

- `Docs/ARCHITECTURE.md` §5: a paragraph on fire (the engine's `Fire`, the pieces, the physics' owner `Combustion`, what
  reads what), and the decision log's entry for the view rule and the clock.
- `Docs/DEBTS.md`: the stated estimates of For main to decide 8; the wood table's re-read carried or paid by 4; "the body knows
  no roof, no ground, no fire" (2026-09-16) paid for fire when the hook lands; the LFS fixture of 12.
- `Docs/BETA_MAP.md` §7: BF.5 drafted, part one built on the branch.
- `Warmth` and `Surroundings`: the hook (2); `Wood`: the re-read (4, 5); `ThingWords`: the words (6); `THIRD_PARTY_NOTICES.md`
  (11).

## What changed on the way

1. *A lone stick and a crib.* A crib's burning rate is a stick's among others; a lone thick log given that rate burnt on its
   own. The fire's heat on a piece became the share of its view the other burning pieces fill, and a stick's own flame's heat
   was given the inverse root of its thickness, so a lone stick thinner than a pencil burns and a thumb-thick one does not.
2. *A charred stick's surface.* A stick burnt down to its char had no surface left to glow on; its char was given half the
   room of the wood it came from.
3. *The ladder.* A first lay with eight twigs lit nothing above them: the twigs burnt out before the next size caught, as
   happens to anyone who lays too little kindling. The tests' lay is twenty twigs, ten pencil sticks and six thumb-thick ones
   under three logs.
4. *Green wood.* The critical mass flux was first held at its value for the fibre saturation point; green blackbutt then
   flamed on a small fire. McAllister's trend carried on past his measurements makes it smoulder there and flame only in a
   hot fire, which is what green wood does.
5. *Wind and lighting.* At the first heat inside a fire (40 kW/m², an estimate), a lay lit in a 3 m/s breeze never took: the
   tinder's flame was gone in three seconds, before the twigs caught, and a test held it ("a fire is lit in the lee"). At the
   measured 60 kW/m² (11) the tinder's glowing char carries the twigs and the fire takes. A finding that turns on one
   estimate is not a promise; the test is now of the tinder's own flame (Rothermel and Anderson's exponential: half a minute
   in still air, three seconds in the breeze), and the question is For main to decide 13.
6. *Friction's footing.* The first chain (μ 0.45, the tip's mean flux, half a milligram of dust a joule) failed most good pairs.
   Duncan's measured bow drill (reported by the research for this contract) gave μ 0.25, his force and speed, his 0.8 g pile and
   his 0.1 g coal; the rim's 1.5 times the mean flux, where the notch is cut, is what brings the chain to his 23 to 24 s and
   Hough's times. The bow drill was added as a method because it is the one measured.
7. *Friction and water.* Recalibrated, the chain made a coal from wood at the fibre saturation point. Every grain the rub grinds
   off brings its water with it: a heat budget per kilogram ground (one stated number, the hot grind) sets the wettest wood that
   can make a coal, placed at 20 per cent where the words begin to call wood damp.
8. *A coal on a twig.* Counting all of a coal's heat into whatever it touches let a blown coal light a 4 mm twig. A nest wraps a
   coal and keeps its heat; a solid stick takes only what its breadth subtends and loses heat from its bare wood, and does not
   catch.
9. *The fire sizes.* First set as heats, then as their published burn rates, with the heat worked out by this model's own
   accounting, so the kilograms are the sourced fact.
10. *The burning rate re-read at its sources.* The first crib rate, v = 2.2 × 10⁻⁶ b^−0.6 m/s credited to Babrauskas, was not
    found in its source: COMPF2 prints 1.7 × 10⁻⁶ in its text (and 2.48 × 10⁻⁶ in its code). McAllister and Finney's mass flux
    replaced it, with COMPF2 kept as the second source; the rate became a mass flux over the wood's density, so dense wood burns
    inward slower, and every burn-through came out one and a half to two times the first's (a 5 cm banksia stick 31 min became
    55).
11. *The heats re-read.* The heat inside a fire, first 40 kW/m² as an estimate, is Thomas's measured 60; the char's heat, first
    30 MJ/kg, is NIST's 32.6; the flame's efficiency, first 0.9, is 0.95, since with the char's heat re-read 0.9 put a
    kilogram's flame heat at 12.6 MJ, below Tran's effective heat of 13.0 to 14.7; the heat of gasification that slowed wet
    wood (1.8 MJ/kg) could not be read at its source and was replaced by a fit to the one published point on moisture and
    burning rate (3.2). Green wood's pace moved from 0.6 to 0.72 of air-dry wood's; the coal's glow from 16 W for three minutes
    to 13 W for four.
12. *The work.* The brief named this branch `cloud/bf5-fire-physics`; the harness's own branch name was another. The brief's
    was used.

## Where the sources disagree

Found while checking each number against a second source; the model's choice is named in each.

- *The net heat of dry wood.* The table's 19.0 gross gives 17.69 net; Phyllis2 measures eucalyptus wood at 17.96, and the
  firewood tables start from 18.5 (Krajnc, FAO 2015) and 19 (Forest Research), so their heats as cut sit up to 8 per cent above
  this model's. The standards' hydrogen term is 212.2 J/g per per cent (ISO, with the change to constant pressure), 218.3
  (Phyllis2) or 219.8 (Wong et al., 9 × 24.42): a few hundredths of a megajoule.
- *Green wood.* The standards' arithmetic gives green blackbutt (0.6 of its dry mass in water) about two-thirds of what air-dry
  wood gives, kilogram for kilogram as cut; Victoria's Agriculture Note AG1150 (Brock 2004) says green firewood gives "about
  40%". In the fire model green wood that only smoulders loses its gas as smoke and keeps only its char's heat, about a fifth of
  what air-dry wood burnt in flame gives, kilogram for kilogram as cut; the note's figure sits between the two.
- *The crib law.* McAllister and Finney's exponent is −0.5, Babrauskas's −0.6; COMPF2's constant is 1.7 × 10⁻⁶ in its text and
  2.48 × 10⁻⁶ in its code. Within a fifth of each other from 4 mm to 10 cm; at 3 mm the crib law is 21 per cent below COMPF2.
- *Wind on burning cribs.* Half-inch cribs burnt 6 to 62 per cent faster at 0.7 m/s and quarter-inch ones slower (McAllister and
  Finney 2016); the model speeds every flaming stick alike.
- *The char.* A fifth of the wood (Tran), 0.16 to 0.23 by species (NIST).
- *The critical flux.* 11 kW/m² (Babrauskas's fit, 9.0 to 12.2 by orientation), 10 to 13 (Wood Handbook), 12 ± 2 (Bartlett et
  al.).
- *Catching.* Babrauskas's correlation gives 53 s at 30 kW/m² for wood of 450 kg/m³; McAllister, Finney and Cohen's poplar caught
  in 28 s (14 s against their 9.7 at 50). The correlation's own scatter is 64 per cent. Water slows it 1.47 times at 18.5 per cent
  in their measurements (the model's), where Mikkola's (1 + 4w)² gives 3.0 and Moghtaderi et al. found three times at 30 per cent.
- *The tinder's moisture limit.* 23.6 per cent (Rothermel and Anderson, the model's), 11 (Manzello et al.'s brands into grass),
  16 to 40 by the igniter (Blackmarr): For main to decide 7.
- *The radiant share of a wood fire.* 0.21 (McCarter and Broido) to a third (Sunahara et al.), 0.29 to 0.30 for trees burnt whole
  at NIST, and 0.41 falling to 0.15 as the wood's water rises (Sung et al. 2025); the model takes 0.30 whatever the water.
- *Close to a fire.* The point source holds to 5 per cent beyond two and a half widths (Modak); nearer, the sources differ on
  whether it overstates or understates, and the words say "the reckoning overstates" there.
- *The skin.* Absorptance 0.95 for the body's long-wave (ASHRAE 55's SolarCal), where its emissivity is 0.97 to 0.98.
- *Friction.* Wood on wood slides at 0.17 (Aira et al., Scots pine across the grain), 0.2 to 0.3 (Duncan, the model's 0.25) and
  0.25 to 0.5 (engineering tables). Wood dust glows at 310 to 340 °C on a hot plate (GESTIS-DUST-EX, the model's 340), 330 to 350
  (Pastier et al.), and Duncan takes 370 within his 340 to 430. The chain gives Duncan's bow drill a coal in 33 s against his
  measured 23 to 24.
- *The wood table.* As For main to decide 4 sets out.

## Exit

The promises kept with the counts as run, or "What changed on the way" saying which was not and why.

### Part one (2026-09-25, on the branch)

**What was built.** Seven new files under `Runtime/Fire/`, each with its `.meta`, and the folder's `.meta`: `Combustion` (the
heat, the char and the gas, the crib rate, the water's and the wind's parts, catching, the critical mass flux, the fire's heat on
its pieces, the char's glow, the tinder's spread and extinction), `FuelPiece` (a stick or a bundle, its fuel class by
thickness, and a BF.1 stick read as a piece), `Fire` (the pieces stepped, the phases, the words), `Tinder` (a coal and its
nest), `FrictionFire` (the three methods' chain and its failures), `FireWarmth` (the point source and Fanger's areas) and
`FireFuel` (the three fires' kilograms and heat). Three test files: `FireBurningTests`, 17; `FrictionFireTests`, 15;
`FireWarmthTests`, 10.

**As run.** .NET SDK 10.0.401 (installed by the official script, channel 10.0). `dotnet test Engine/tests/EarthGame.Tests >
test-results.txt 2>&1; echo $?` printed 0: 911 passed, 0 failed, of 911 (869 before this branch, 42 new); the two tests marked
for Windows only are not run on Linux. The 869 of main passed on this clone only once the LFS fixture was rebuilt (For main to
decide 12): before it, 830 passed and 39 failed. Main moved on while the branch was open (to 091d029: the geology, the day's
fixes, ruling 49); the branch merged with it in a throwaway worktree, not pushed, ran 911 passed, 0 failed, as well. The suite's
source scans read the new files (no Unity, no `#if`, no clock or unseeded random, no to-do markers). Five sabotages, each
restored byte for byte (sha256 checked) and each run over the three fire test classes:

| Sabotage | Red |
|---|---|
| The water taken out of the critical mass flux | 2 of 42: `AFlameWantsGasEnoughAndWaterRaisesTheWant`, `GreenWoodSmouldersWhereAirDryWoodFlamesAndTheWordsSayWhy` |
| A spun tip's rim heated at its mean (the factor 1.5 set to 1) | 2 of 42: `TheChainMeetsTheTimesItWasMeasuredAndTimedAt`, `TheSurfaceRisesAsASemiInfiniteSolidsDoesAtTheRimWhereTheRubIsFastest` |
| The dust's heat budget skipped | 1 of 42: `DampWoodTakesTheHeatAndSaysSo` |
| The tinder's extinction moisture raised to 0.40 | 3 of 42: `AFlameCrossesTinderAsRothermelAndAndersonMeasuredAndNotWhenDamp`, `DampTinderSmokesAndWillNotFlameAndSaysWhy`, `DampTinderWillNotFlameAndSaysWhy` |
| Every piece's view share forced to one | 1 of 42: `TinderAloneUnderLogsLightsNothing` |

**The numbers as run**, from a probe of the built classes: net heat 17.69 MJ/kg dry, 13.66 as cut at 20 per cent wet basis
(Krajnc's 14.31), 16.77 given to a fire per kilogram of dry wood at 15 per cent, the gas 13.96; the crib's burn-through at 640
kg/m³ 1.2 min at 4 mm, 4.9 at 10, 20.0 at 25, 55.2 at 50, 156 at 100 and 442 at 200; catching at 30 kW/m² 52.9 s (McAllister's
28.0), at 50 kW/m² 14.3 s (9.7); the ladder's logs caught at 1.0 min, flames to 117 min, the peak 34.3 kW, 5.0 kW on average
while flaming, out at 188 min, 52.2 MJ (16.8 MJ/kg dry); green logs smouldering at 20 min; lit in 3 m/s the logs flame; a lone
5 mm stick flames 5.7 min and a lone 25 mm one will not; friction as promise 6 gives it, Duncan's bow drill 33.2 s; tinder
blown 12.4, 15.6, 19.3 and 35.8 s at 8, 15, 20 and 23 per cent, too damp at 25; the coal 12.8 W for 254 s, blown 34.0 W for 96 s;
the warming fire 14.0 kW and its warmth as promise 8 gives it; the fires' fuel as promise 9 gives it.

**Not run here**, as the brief says: the Unity-shaped compile, the edit-mode tests and the built game. Nothing in part one is
drawn, heard or felt.
