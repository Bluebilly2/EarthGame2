# Contract BF.7 — The body's rest

**Status:** proposal, drafted by a cloud session on 2026-09-25 for main's read. Part one, hunger, food, wetness, the ground
and sleep as physics in the engine, is written as a proposal with code on the branch `cloud/bf7-body-rest`, from main at
36c7acb: new files only (`Engine/packages/com.earthgame.engine/Runtime/Body/Rest/` and five test files under
`Engine/tests/EarthGame.Tests/Engine/`), no existing file changed, so nothing on main moves until main takes it. The parts
after it are named here and not built. Owner: Claude (the main session, when it takes this). William's lane: nothing in part
one wants his eyes or his hands; his word is wanted on two questions of taste (For main to decide, 1 and 2: the clock while
the founder sleeps, and the founder's body), and the parts after it want his eyes on the words and his hands on a night slept.

## Why this next

BETA_MAP §7 puts BF.7 after fire and the camp, for F7: "Hunger as an energy balance with a store and words; food eaten;
wetness from rain and water, drying; sleep as a process with the clock run on; the wet cold." Three of the beta's goals stand
on it. G8, eating, begins with "hunger: the body's energy balance (`Warmth` already spends watts; the store, the deficit, the
words, the work capacity)" and ends with eating. G11, the first rain, wants "rain on the body: wet skin, insulation lost,
evaporation" and "the wet cold's death". G12, the night that ends the arc, is "lying down on bedding: a process over hours, the
clock run on while the balance is checked every step, waking at cold, thirst or dawn". FOUNDERS_PATH's Act II names its kill
conditions "cold compounded by wet; hunger's slow arithmetic", and the arc completes the first night the founder "sleeps a full
night" in a camp of their own. DEBTS 2026-09-16 owes it: "The body knows no roof, no ground, no fire, no clothes and no
sleep." GAME_DESIGN §8 says how: what a food gives must come out of what it is made of (its water, starch, sugar, protein, fat
and fibre) and how it was made ready, never out of a label; how cold a wet body gets must come out of the water on it and the
air around it; and a bed's warmth out of its mass, its density and the ground under it.

## The shape

The body's rest is five parts, each shippable alone:

1. **The physics** (this part): hunger, food, wetness, the ground and sleep as pure functions and stepped models in the
   engine, from published figures, with the estimates named. Nothing on the wire, in a save or on a screen.
2. **The body in the server**: the models stepped beside `Warmth` and `Hydration` in the server's founder step, their terms
   in the heat balance through the hooks (For main to decide 3), one work capacity (4), death by starvation (5), their state in
   the player file and on the wire (6).
3. **The verbs**: eat; lie down and get up; dig where the plant stands with a digging stick (BF.2's work, a hole left);
   roast in a fire's coals (BF.5's fire); pound with a stone; lay a bed (BF.6's bedding pile read as `GroundContact`'s bed).
4. **Seen, heard and told**: the words on the tablet and the body's bars, a wet body drawn and dripping, rain heard on skin,
   the dawn's summary of a night slept.
5. **Food in the world**: each plant's edible part, its yield by season and place, the catalogue's missing species, and
   whatever harm a source finds in a food.

## Part one: the promises (built on the branch)

1. **Hunger as an energy balance.** `Hunger` keeps the body's fuel as Cahill tabled it for a 70 kg man (liver glycogen 0.07
   kg, muscle glycogen 0.40, protein 6; 1983, table 1), with the fat at the Hadza men's 13.5 per cent of body mass, 9.45 kg
   (Pontzer et al. 2012), each at Chow and Hall's energy (glycogen 17.6, fat 39.5, protein 19.7 MJ/kg; 2008). The spending is
   not its own: `Advance(seconds, spendW, restingW)` takes the heat balance's metabolic watts (`Warmth.ProductionW`) and the
   part of them spent at rest, so no step is priced twice. Food being absorbed pays first (a time constant of 3 hours, an
   estimate within Cahill's "1–8 hours"); then the liver's glycogen (a time constant of 24 hours: 2.9 g an hour at first,
   Rothman et al.'s 4.3 µmol/(kg·min) by NMR); then the muscles' for half of any work above rest (an estimate, after Vallerand
   and Jacobs and Haman et al.); then fat and protein, protein paying 0.18 of it at first (Cahill et al. 1966 measured 14 per
   cent over eight days, Levanzin's fast 16 to 19, Cahill 1983 "75 g daily") and 0.12 once the fast has adapted (Kerndt et al.'s
   lean faster, 5 to 7 g of nitrogen a day after the third week), fat down to a floor at 3 per cent of body mass (an estimate:
   the Minnesota men lived with 2.67 kg). The fast adapts, with a time constant of 7 days, only while the liver's glycogen is
   below two-fifths of its store, past the postabsorptive phase ("6 to 24 hours", Felig's stages as Kerndt et al. give them),
   and is undone, with a time constant of a day (an estimate), while the liver is full again. A surplus refills the glycogen,
   gives back protein lost in the share the deficit took it ("for a given individual, the P-ratio during refeeding is strongly
   correlated with the P-ratio during semi-starvation", Dulloo, Jacquet and Girardier 1996), and lays the rest down as fat.
   - *A total fast at rest* (the basal rate as the starving body makes it) leaves 28 g of the liver's glycogen at 22 hours and
     takes 93 per cent of it by 64 (Rothman's fasters had lost 83); costs 58 g of protein on the second day (Cahill et al.'s six
     men, 62 to 81) and 33 g a day in the fourth week (Kerndt's lean man, 31 to 44); loses 0.61 kg a day in the first week and
     0.20 in the fourth (Kerndt: "0.9 kg per day ... slowing to 0.3"); and kills after 67.3 days, 3.0 kg of protein gone and
     the burnable fat spent (the ten hunger strikers of 1981 died after 46 to 73 days, mean 62; CAIN, Ulster University).
   - *A founder who eats every day* never enters the fast: one meal a day of 12.5 MJ with eight hours' walking spends 55.8 g
     of protein each night and has it back each morning, the same on the 10th day as on the 30th.
   - *A founder on the Minnesota ration*, 6.57 MJ a day in two meals, with six hours' work a day at 150 W above rest, lives
     through the 24 weeks having lost 20.4 per cent of body weight (the Minnesota men 24), with 2.88 kg of fat left (2.67) and
     2.91 kg of protein gone (Keys et al.'s "roughly 2.5 kg"), the basal heat at 0.61 of a fed body's (their 38.89 per cent fall).
   - *What the deficit costs.* The work left by the share of weight lost: whole below 5 per cent, 0.95 at 10, 0.76 at 16 (the
     Ranger-I study's maximal lift, Friedl 1995), 0.645 at 24 (the Minnesota men's back strength and aerobic capacity) and, an
     estimate, 0.4 at 30. The cold: `BasalShare01`, the basal heat left, the active tissue lost (Keys' 10.11 kg of it for
     "roughly 2.5 kg. of protein") times the adapted fall of 15.5 per cent per unit of what is left (Keys, p. 329), as far on as
     the fast's adaptation or the fat spent, whichever is further (the fall stayed with the fat on refeeding, Dulloo et al.
     1996); `TissueInsulationShare01`, the fat's shell of 12.5 per cent of the tissue's insulation going with the fat
     (Veicsteinas, Ferretti and Rennie 1982). Both are hooks for `Warmth` (For main to decide 3).
   - *The words.* `HungerLevel`: fed; hungry once the liver has given a fifth of its glycogen (the sixth hour); very hungry at
     three-fifths (the 22nd); weak with hunger once the fast is a third adapted (the fourth day, ketosis having subdued "the
     voracious sensation of hunger", World Medical Association 2006); starving at a tenth of body weight lost (after 18.6 days
     of a fast at rest; Gétaz et al. 2012's point for close monitoring); wasting at 18 per cent (after 47.3; "serious medical
     problems begin"). Death is `IsAlive` false past half the body's protein, the top of Kerndt et al.'s "a third to a half", since the
     Minnesota men lived with 2.5 kg of Cahill's 6 gone.
2. **Food eaten, raw and roasted.** `Food` carries six foods of chain G8 as their published composition per 100 g at their
   water (Lomandra leaf bases, bracken rhizome, the long yam's tuber, a greenhood orchid's tubers, pigface fruit, banksia
   nectar), each row naming whose numbers it carries and which are estimates. Their energy is the Australian Food Composition
   Database's own equation (protein 17, fat 37, sugars 16, starch 17, fibre 8 kJ/g; FSANZ, AFCD Release 3) taken at what the
   gut digests: a tuber's starch 0.50 raw and 0.97 cooked (Carmody and Wrangham 2009, table 3: green banana and potato), what
   escapes fermented in the colon at 8 kJ/g (FAO/WHO 1998); protein 0.65 raw and 0.91 cooked (egg, Evenepoel et al. 1998, the
   one food measured both ways in people); bracken's fibre spat out (McGlone, Wilmshurst and Leach 2005, the "fibre wad");
   every term scaled by the food's water, so a rhizome dried in the shade is richer by the kilogram. As found, per 100 g:
   Lomandra 311 kJ raw, 387 roasted; bracken rhizome 65 raw, 86 roasted, eleven kilograms roasted for a day's 9.5 MJ; long yam
   337 raw, 438 roasted (the AFCD's own cooked wild yam, 448, checks the equation); greenhood tubers 194 raw, 256 roasted;
   pigface fruit 230; banksia nectar 560, a spike's standing 1.9 g a mouthful of some ten kilojoules. `Food.Describe`: "0.5 kg
   of roasted bracken rhizome, about 430 kJ". `Hunger.Eat` takes `Food.EnergyJ`.
3. **Water on the skin.** `Wetness` keeps the water on a naked founder: the film a wet skin holds, 50 g/m², 90 g over the
   body (US EPA 2011's hand film; Pitol, Kohn and Julian 2020, 38 to 78); rain on the top of a standing body and, driven by
   the wind, on its front by Lacy's relation, 0.222 U R^0.88 (Blocken and Carmeliet 2004), over Fanger's projected areas read
   from `FireWarmth` (0.77 kg an hour in a drizzle of 2 mm/h at 3 m/s, 4.92 in 10 mm/h at 5 m/s); wading and swimming as
   `Immerse(share)`. It dries by evaporation at the Lewis relation (16.5 K/kPa; ASHRAE 2017, ch. 9) with de Dear et al.'s
   convective coefficient for a standing naked body (10.4 v^0.56, 3.4 in still air; 1997), faster in wind, dry air and sun.
   The skin's temperature is read off `Warmth`'s own numbers (the sensible loss through the still-air layer) and solved with
   the evaporation by bisection, so the two cannot come to disagree about how warm the skin is; of the heat the water takes, the
   core pays the air layer's share, R_b / (R_t + R_b). A naked founder wet through at 10 °C in 3 m/s at 80 per cent humidity:
   skin 12.2 °C, evaporation 249 W, the core paying 71 W more than the 249 W it loses dry, dry again in 22 minutes; at 5 °C in
   5 m/s, 63 W more; at 20 °C in 2 m/s at 50 per cent, 106 W more and dry in 15 minutes. In 8 mm/h of rain at 4 m/s it is
   soaked in two minutes and stays so, the core paying 58 W. `WetLevel`: dry, damp, wet, soaked. Soaked clothing keeps 0.68
   of its insulation (Castellani et al. 2001's 0.75 of 1.1 clo), for when there is clothing.
4. **Sleep.** `Sleep` keeps Borbély's Process S as Daan, Beersma and Borbély fitted it (rising with a time constant of 18.2
   hours awake, falling with 4.2 asleep, sleep at 0.67 and waking at 0.17; Skeldon and Dijk 2025 restate the set): sleep comes
   after 16.8 hours awake from rested and a night's 5.8 hours spend it, exactly for any step. `Tiredness`: rested; tired past
   0.67; very tired a night without sleep (24 hours); exhausted a day and a half (36). What a short night costs: the work left
   is whole until sleep is due, 0.945 at 24 hours awake, 0.89 at 36 (Martin 1981: 11 per cent less endurance after 36 hours
   awake) and 0.86 at 48, about half a per cent an hour (Craven et al. 2022: "~0.4%"). A sleeper wakes, by the first rule that
   holds: at a core of 35.5 °C (Kreider and Iampietro 1959, the limit "compatible with substantially continuous sleep"); very
   thirsty (this model's rule); at civil dawn, the sun at −6°, from a sleep begun in the dark once the pressure is below 0.67
   (Yetish et al. 2015's foragers woke about an hour before sunrise); or, napping in daylight, when the pressure is spent. The
   sleeping body makes 0.95 of its basal heat (Goldberg et al. 1988; Seale and Conway 1999 found the night's rate equal to the
   basal): the hook's number.
5. **A lying body and the ground.** `GroundContact` and `SoilHeat`: 17 per cent of the skin pressed to the ground (Sanak et
   al. 2025, 0.34 of 1.96 m²); soils by Kersten's equations in Farouki's metric form (1981): dry sand 0.288 W/(m·K) (Farouki's
   table 22), moist sand at 1600 kg/m³ and a tenth water 1.58, a loam at 1300 and a fifth 0.91; the ground's pull a
   semi-infinite solid's, e ΔT / √(πt), until it settles to the steady flow from a disc, 4Rk (Lienhard and Lienhard 2024).
   Sanak's measured 385 W/m² after twenty minutes on a board lies between dry sand's 225 and moist sand's 661. A bed is its
   pressed thickness over its conductivity: loose plant fibre at Costes et al.'s straw, 0.0444 + 2.72 × 10⁻⁴ ρ W/(m·K) (2017),
   0.3 of its loose depth kept under the body (an estimate; the army's 15 to 30 cm bough bed lies 5 to 10 cm thick under a
   body): 20 kg of bracken over 1.2 m² is 1.55 m²·K/W, ten clo. A cold-constricted body on 10 °C ground loses 41 W to moist sand
   in the first hour and 28 W in the eighth, 38 and 23 to loam, 26 and 13 to dry sand, and 4.5 W on the bracken bed.
6. **Every number carries its source; an estimate says it is one.** In the doc comments, author, year and title or journal;
   the estimates are gathered in For main to decide 17.

## The parts after it (named, not built)

- **Part two, the body in the server.** The server's founder step advances, after `Warmth` and `Hydration`, `Hunger` with
  `Warmth.ProductionW` and the resting part of it, `Wetness` with the weather's rain (`Weather.RainRateMmPerHour`), the
  mover's wading and swimming (`MoverState`) and `Warmth`'s own sensible loss and air layer, and `Sleep` with the sun; the
  terms land in `Warmth` through the hooks (3); one capacity (4); death by starvation (5); the state saved and sent (6). The
  ground under a lying body is read off the world's soil (`SoilModel`'s wetness and sand into `SoilHeat`'s Kersten form).
- **Part three, the verbs.** In BF.2's model of work: eat what is in hand (`Food` by its kilograms and preparation); lie down
  and get up; dig a tuber or a rhizome where the plant stands (a process on the ground yielding by the plant and leaving a
  hole, BF.4's cells); roast it in a fire's coals (BF.5's `Fire`); pound it with a stone; lay a bed of a mass of bracken,
  grass or paperbark (BF.6's bedding pile read as `GroundContact.BeddingResistanceM2KPerW`).
- **Part four, seen, heard and told.** The words on the tablet (BF.8) and the body's bars (M1.F) for hunger and tiredness; a
  wet body drawn and dripping; rain heard on the skin (F10); why a founder woke, and the night slept in the dawn's summary.
- **Part five, food in the world.** Each species' edible part, its yield by season and place and its time to dig (the
  catalogue's plants and their stands), the species the catalogue lacks (13), protein in food as its own flow (15), and harm
  where a source finds it (12).

## Non-goals of part one

No wire, save, server system or client. No change to `Warmth`, `Hydration`, `Locomotion`, `Death`, the player file or any
other file that exists. No clothing (the founder wakes naked; `Wetness.WetClothingClo` waits for clothing's beat). No heat lost
in water while wading or swimming (10). No circadian process: the night stands for it. No harm from food. No protein in food
apart from its energy (15).

## How it is proved

- Tests, the published numbers written again in them rather than read from the classes: `HungerTests`, `FoodTests`,
  `WetnessTests`, `SleepTests`, `GroundContactTests`. Each figure against its source and a second source wherever one was
  found: the fast against the hunger strikers' days, Kerndt's weights and nitrogen, Cahill's protein and Rothman's liver; the
  basal rate against the Minnesota men's; the AFCD's equation against its own cooked yam; the cooked tuber's gain against
  Carmody and Wrangham's 30 per cent; bracken against its dry analysis and its measured starch; the film against Pitol et al.
  beside the EPA's; Lacy's rain and de Dear's coefficient against ASHRAE's; Kersten's soils against Lienhard's tables; the
  ground's pull against Sanak's measured flux; the bed against Costes' measured band; the sleep pressure against the
  two-process model's own day and night; the cost of 36 hours awake against Martin and Craven et al.
- The behaviour the body must have, as tests: the fast the same whether its time is cut in hours or minutes, and the night
  likewise; the words at their stages; eating that fills the liver first, gives protein back and ends the fast, and a founder
  fed each day who never starves; work that draws the muscles' glycogen; a wet skin colder than a dry one but not below the
  air, drying faster in the sun and slower in rain; a nap that ends of itself and a night that ends at the dawn; the cold
  waking a sleeper before thirst does.
- `dotnet test Engine/tests/EarthGame.Tests > test-results.txt 2>&1; echo $?`, with the counts in the exit record; the suite's
  source scans (no Unity, no `#if`, no clock or unseeded random, no to-do markers) over the new files.
- Sabotage, each restored byte for byte, in the exit record.
- Not run here, as the brief says: the Unity-shaped compile (it needs Unity's own libraries), the edit-mode tests and the
  built game. The new files are C# 9 for .NET Standard 2.1 with no Unity and no `#if`, and dotnet's build refuses at
  LangVersion 9 with warnings as errors what Unity's would.

## For main to decide

1. **The clock while the founder sleeps (William's).** The world runs a day in thirty real minutes, so ten hours asleep pass
   in twelve and a half real minutes. v1 lived the night at twelve times the world's rate (`Docs/v1/SLICE2_5_SLEEP.md`:
   "Sleeping does not skip the night, it lives it at speed"). Everything on the world's clock (the body, a fire's fuel, the
   weather) would run at that rate, and with two founders in one world the clock is shared. Recommended: the world's own
   rate, the clock BF.5's answer recommends for the fire with the body (his word awaited there too), and a speed-up only while
   every founder in the world is asleep. Whether a night at the game's rate is the game is William's taste.
2. **The founder's body (William's).** The model is one founder of 70 kg (`Warmth.MassKg`) with the Hadza men's 13.5 per
   cent fat. The Hadza women carry about a fifth; a heavier or fatter founder lives longer without food and keeps warmer. Who
   the founder is, and whether that is chosen at a new game, is his.
3. **The hooks in the body's balance** (part two), proposed:
   - `Warmth`'s basal heat times `Hunger.BasalShare01`, and times `Sleep.SleepingMetabolicShare` asleep;
   - no shivering asleep: near-naked men shivered awake at 21 and 24 °C and "only occasionally during stages 1 and 2 sleep"
     (Haskell et al. 1981), and a sleeper's defence is waking at 35.5 °C;
   - the tissue's insulation, warm and constricted, times `Hunger.TissueInsulationShare01`;
   - `Wetness.CoreLossW` as a loss in `Warmth.Tick` beside the sensible and the breath's;
   - lying: `GroundContact.LossW` as a loss, and the skin exchanging with the air less by the share that touches the ground
     or itself (15.6 per cent supine, Kurazumi et al. 2004);
   - `Hunger.Advance` spending `Warmth.ProductionW`, with the basal part as its resting watts.
   `Warmth` would show each term beside its others for the log and the tablet, as BF.5's `FireGainW` will.
4. **One work capacity.** `Hydration.WorkCapacity01` is passed today wherever a capacity is wanted. With hunger and sleep there
   are three; proposed: their product, owned by one function the body keeps, since each measures a different loss.
5. **Death by starvation.** `CauseOfDeath.Starvation = 3`, its words in `Death.Explain` (the protein burnt and the days
   without food), and `DiedMessage` accepting the byte; the Standard death as for cold and thirst.
6. **The save and the wire.** Player file v8: the liver's and the muscles' glycogen, the fat, the protein lost, the fast's
   adaptation and the food not yet absorbed; the water on the skin; the sleep's pressure, whether asleep and whether this
   sleep began in the dark (each class has its `Restore`), with the digest and a round trip. `FounderStateMessage` carrying
   the three levels as bytes and a wake's reason; the protocol from 21 (main's now) to 22.
7. **Two owners of walking's cost.** `Warmth` bills walking 180 W above the basal 80; `Locomotion`'s Pandolf gives 348.5 W at
   1.39 m/s on light brush. Hunger takes `Warmth`'s by design, so the hunger's arithmetic inherits whichever main makes the
   owner. Proposed: `Warmth`'s exertion from `Locomotion`'s watts.
8. **`Warmth`'s air layer in wind.** Its 0.7 / (1 + 0.55 √v) clo gives 0.45 at 1 m/s, where ISO 9920 and ISO 7933 give 0.47,
   but 0.36 at 3 m/s against their 0.27. The wet skin's temperature is read off it.
9. **`Warmth`'s skin.** Read off its own numbers as `Wetness` reads them, a dry, constricted, naked skin sits at 13.3 °C in
   1 °C air at 0.8 m/s; people measured naked at 1 °C had mean skins of 18.8 to 19.4 °C after an hour (Launay et al. 2006) and
   21.4 to 27.6 in 0.8 m/s (Savourey and Bittel 1994). The one-node balance's split between tissue and air layer is where the
   difference lies, and a colder skin evaporates less.
10. **Water.** `Warmth`'s constants in water do not cool a body: in 10 °C water, with water's heat transfer taken at the air's,
    the loss stays under the heat made, where Hayward, Eckerson and Collis's equation, fitted to their measurements, has the
    core falling 2.67 °C an hour (1975).
    Water takes heat at 43 to 107 W/(m²·K) (Boutelier 1977; English and Hemmerling 2008). Proposed: a water term in `Warmth`
    when `MoverState` says wading or swimming, part two, with Hayward's cooling as its check.
11. **The yam.** The one yam of New South Wales, *Dioscorea transversa*, grows "north from Stanwell Tops" (PlantNET): none on
    the South Coast. Its row stays for the equation's check against the AFCD and for a northern region; or goes.
12. **Bracken's harm.** v1 made raw bracken "a bad idea", doing "measurable harm", with roasting and pounding its cure
    (`Docs/v1/SLICE2_6_FOOD.md`). The sources do not support heat as a cure: Leach et al. found raw, roasted, stored and leached
    rhizomes "equally toxic" (2023), and the harms (thiaminase, ptaquiloside) are slow, over weeks. Part five's, if at all, as
    a slow state.
13. **The catalogue.** Pigface, a greenhood orchid and the yam are not `PlantSpecies`; their rows carry no species. Pigface
    (*Carpobrotus glaucescens*) and an orchid for the dunes and the heath would be BF.4's catalogue's.
14. **Fanger's tables.** `Wetness` reads its rain areas from `FireWarmth.ProjectedAreaM2`, the tables' one owner on main;
    `BodyProjection` (BF.5, main's answer 3) takes them with it when it lands.
15. **Protein in food.** `Eat` takes energy; the protein given back is the P-ratio's share whatever was eaten, though bracken
    carries 0.2 g of protein in 100 g and nectar none. A diet of rhizome and nectar would not truly rebuild lean tissue.
16. **Semi-starvation.** Out of a fast's ketosis, protein pays 0.18 of the deficit, the fast's early share. Keys found protein
    "about 25 per cent" of the oxygen used in semi-starvation, and a lean body's share rises as its fat falls (Forbes' curve;
    Dulloo and Jacquet 1999). A share set by the fat left would be truer, later.
17. **The stated estimates**, each named in its doc comment, as DEBTS rows:
    - hunger: the gut's three hours, the muscles' half of work, the adaptation's week and the refeeding's day, the fat floor,
      the words' marks and the work left at 30 per cent lost;
    - food: Lomandra's composition (the class mean of pith and stalks), the greenhood's water, pigface's carbohydrate as
      sugars, the banksia's 35° Brix and 1.9 g a spike, bracken's carbohydrate as starch (an upper bound), the yam's starch,
      sugars and fibre from potato, raw protein's digestibility from egg;
    - water: the hand's film over the whole skin, the words' marks, driving rain on a body as on a wall, the sun's warmth on
      a wet skin;
    - sleep and the ground: the dawn at civil twilight, thirst's waking, the capacity's slope from Martin's one point, the bed
      kept at 0.3 under a body, straw for bracken, grass and paperbark, a curled body's contact.
18. **Distributions (§9).** Part one is deterministic. A dig's yield and a tuber's size have real spread; part three's work
    could draw them as `Work.Apply` salts its draws.

## What part one asks of existing files (not changed here)

- `Warmth`: the hooks (3), the water term (10), and the air layer and the skin (8, 9) if main takes them up.
- `Death` and `DiedMessage` (5); the player file and `SavedPlayer` (6); `FounderStateMessage` and the protocol (6); the
  server's founder step (part two); `Locomotion` or `Warmth` for walking's one cost (7); `PlantSpecies` (13).
- `Docs/ARCHITECTURE.md` §5: a paragraph on the body's rest (the four models, what each reads and what reads them), and the
  decision log's entries for the hooks and the capacity.
- `Docs/DEBTS.md`: the estimates (17); "the body knows no roof, no ground, no fire, no clothes and no sleep" (2026-09-16) paid
  for the ground and sleep when the hooks land, clothes left.
- `Docs/BETA_MAP.md` §7: BF.7 drafted, part one built on the branch.
- `THIRD_PARTY_NOTICES.md`: nothing; no file was vendored. The downloads are recorded as ruling 49 asks in
  `BF.7_THE_BODYS_REST_DOWNLOADS.md`, beside this contract.

## What changed on the way

1. *A nap and the dawn.* The first dawn rule woke any sleeper once the sun was up and the pressure below 0.67, so a nap at
   noon ended as it began. A sleep now knows whether it began in the dark, and only a night's sleep ends at the dawn; a nap
   ends when the pressure is spent.
2. *The wet skin's temperature.* A fixed-point iteration of the skin against its evaporation oscillated in a cold wind (the
   step's factor about 1.13). The balance falls monotonically as the skin warms, and a bisection on it replaced the iteration.
3. *Protein's share.* A pure Forbes partition of the fast's deficit killed a founder at rest in about 35 days. The fasting body's
   measured shares (0.18 falling to 0.12) with a floor of fat that cannot burn brought death to the hunger strikers' range.
4. *The founder fed each day.* The refeeding test failed, and the failure was the model's. The fast adapted in any deficit, so
   every night's hours before breakfast built it, and the protein each night spent was never given back. By a line-for-line
   mirror of the class in Python (which gave the class's own fast to the day), a founder eating 12 MJ once a day and walking
   eight hours sat at a fifth of the fast's adaptation and burnt 2.8 kg of protein in 60 days, dying of starvation with more fat
   than they began with. Five changes, each from a source already read:
   - the fast adapts only while the liver is low, and is undone while it is full (Felig's stages);
   - a surplus gives protein back in the deficit's share (Dulloo et al. 1996);
   - the adapted fall of the basal rate goes as far as the fast's adaptation or the fat spent (the same);
   - the basal rate's tissue is Keys' active tissue, 4.04 kg of it for a kilogram of protein, where it had been Hall's lean
     tissue, 2.6;
   - death at half the body's protein rather than five-twelfths, since the Minnesota men lived with 2.5 kg gone.
   A fast at rest now kills after 67.3 days where it had after 61.9; the fed founder spends 56 g of protein a night and has it
   back by morning.
5. *Main moved.* Main took BF.5 part one in (815f2b3) while this was built. The branch was restarted from the newest main
   (36c7acb) before its first commit, and `Wetness` now reads Fanger's areas from `FireWarmth` where it had kept its own copy
   of three of the tables' numbers.

## Where the sources disagree

Found while checking each number against a second source; the model's choice is named in each.

- *Glycogen.* Muscle 0.40 and liver 0.07 kg (Cahill 1983, table 1, the model's); muscle 0.15 and liver 0.075 (Kerndt et al.,
  after Cahill); 0.5 and 0.08 in fed, trained people (Murray and Rosenbloom 2018): 225 to 600 g in all.
- *How fast the liver empties.* Blood glucose "for 12-16 hours" (Cahill 1983); "the first 18 to 24 hours" (Kerndt et al.);
  83 per cent gone only at 64 hours by NMR (Rothman et al., the model's 93); "exhausted by about day 10-14" (World Medical
  Association 2006, the outlier).
- *Protein late in a fast.* 1 g of nitrogen a day (Cahill et al. 1966), 3 to 4 (Cahill 1983), 3.7 to 4.7 in the obese (Owen
  et al.), 5 to 7 in Kerndt's lean man (the model's 33 g of protein a day). The lean lose faster than the obese most figures
  come from.
- *Protein's share.* Levanzin's fast "30 per cent of the total caloric expenditure" in Keys' text, 16 to 19 per cent by his
  table; 14 (Cahill et al. 1966); "only about 10%" (Gétaz et al. 2012).
- *Days to death.* 46 to 73, mean 62 (CAIN's list, the model's check); "45 to 61" (Kerndt et al., which cannot hold four of
  CAIN's ten); "between 55 and 75" and "survival after ten weeks ... practically impossible" (World Medical Association 2006),
  against deaths at 71, 73, 74 and 76 days.
- *The basal rate early in a fast.* Up 14 per cent by the third day (Zauner et al. 2000); not changed (Friedl 1995); down 12
  per cent at ten days (Laurens et al. 2021) and "about 10-15" (Cahill 1983). The model has no rise.
- *Minnesota's fall.* 38.89 per cent a man (Keys) against "40 percent" (Friedl); its adaptive part 15.5 per cent per unit of
  active tissue (Keys, the model's) against "15–25 percent" of the fall (Friedl reading Keys).
- *Bracken's carbohydrate.* 47.6 g per 100 g dry by difference (Brand Miller et al. 1993, the model's, an upper bound) against
  10 to 30 per cent of the dry weight as measured starch (McGlone et al., citing Williams and Foley and Al-Jaff et al.).
- *Raw bracken.* "Eaten both raw and roasted" (Maiden 1889); "must be roasted first to destroy the toxins" (Cherikoff and
  Isaacs); roasted and raw "equally toxic" (Leach et al. 2023).
- *Raw yam.* Its "small young tubers ... eaten ... without any preparation" (Thozet in Maiden) against "eaten after cooking"
  (Low 1988).
- *Pigface's water.* 84.4 per cent in *C. rossii* (Njume 2020, the model's), 77.6 to 90.3 across five South African species
  (Broomhead 2020).
- *Energy factors.* 16 kJ/g for sugars and 17 for starch (AFCD, the model's), 17 for all carbohydrate (the Food Standards
  Code); fibre 8 kJ/g as metabolisable energy and 6 as net (FAO 2003).
- *The skin's film.* 78 µm on leaving the water and 38 after ten seconds (Pitol et al. 2020) against 49.9 after thirty
  seconds' dripping (the EPA's, the model's), whose data Pitol et al. could not reproduce.
- *A standing body's convection.* 10.4 v^0.56 (de Dear et al., the model's), 14.8 v^0.69 (ASHRAE's standing, Seppänen, valid
  to 1.5 m/s), 8.7 v^0.6 (ISO 7933): at 3 m/s 19.2, 31.6 and 16.8 W/(m²·K).
- *Soaked clothing.* 0.68 of its insulation kept (Castellani et al. 2001, the model's), 84 to 91 per cent for one wet layer's
  conduction alone (Bröde et al. 2008), and "nearly" nothing in Pugh's accounts of walkers in wet wind (not read).
- *Water's pull.* 43 to 54 W/(m²·K) in still water (Boutelier 1977) against 107 (English and Hemmerling 2008).
- *The ground's share of a lying body's heat.* "As much as 80 percent" (FM 21-76, no source given) against "approximately
  one-fifth" measured (Sanak et al. 2025, the model's physics).
- *Dry soil.* 0.288 W/(m·K) for oven-dry quartz sand (Farouki, the model's) against 1.0 for "dry" mineral soil and 0.78 for
  dry sand (Lienhard's table A.2): an effusivity of 585 against 1661, the tables' "dry" most likely air-dry.
- *Straw.* 0.045 to 0.08 W/(m·K) across the studies Costes et al. tabulate; 0.072 at 100 kg/m³ by their fit (the model's).
- *Sleep's pressure.* Rising with 18.2 and falling with 4.2 hours (Daan et al.'s standard set, the model's) against 14.1 to
  26.4 and 1.2 to 2.9 in eight men fitted one by one (Rusterholz et al. 2010): every fitted fall faster.
- *The sleeping metabolism.* 0.95 of the basal (Goldberg et al. 1988, the model's), equal to it (Seale and Conway 1999), 0.97
  overnight and 0.88 asleep (Bingham et al. 1989).
- *Cold and sleep.* Oxygen use rose in sleep as the air cooled from 29 °C (Haskell et al. 1981) and did not differ in the cold
  (Kreider and Iampietro 1959).
- *Sleep need.* 7 to 9 hours recommended (the National Sleep Foundation); 5.7 to 7.1 slept by foragers without clocks (Yetish
  et al. 2015), the model's 5.8 between them.

## Exit

The promises kept with the counts as run, or "What changed on the way" saying which was not and why.
