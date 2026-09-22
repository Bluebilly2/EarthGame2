# Contract BF.1 — Things with properties

**Status:** drafted and built 2026-09-22, the first of the beta's foundations (`Docs/BETA_MAP.md` §7, CANON ruling 41). Owner:
Claude. William's lane: nothing in this slice needs his eyes or hands; the built run is the proof.

## Why this first

Every thing the Founder's Path makes — an edge, cord, a drill and a hearth board, tinder, a windbreak's sticks, a
bedding pile, food — is judged by what it is: its species, its size, its dryness, its edge. Today a thing is a kind
with a nominal mass (`Definition`) and, for stone alone, four fields of state (`ItemComponent`) written by hand in
three formats and dropped from the carrying message; a stick lying in the litter has no properties at all, and taking
it up changes its shape (DEBTS 2026-09-11). GAME_DESIGN §8: *a material's behaviour should arise from its represented
properties, not from arbitrary item-specific rules.* The map's chains (BETA_MAP §4) bottom out here seven times.

## Promises

1. **Wood is a material with published numbers.** `Wood` (engine, `Materials/Wood.cs`), a row per woody species in
   the plant table: air-dry density (kg/m³), the moisture of green wood as a share of its dry mass, Janka hardness
   (kN), modulus of rupture (MPa), the dry heat of combustion (MJ/kg, one figure for wood, 19.0, the Wood Handbook's
   range 18–21), a description and a **source string**; derived, never authored: `Softness01` (from the Janka value,
   the axis a drill and a hearth are chosen on) and `FrictionFire01` (the density band 300–650 kg/m³ that takes a coal
   by friction, falling to nothing past 850). Rows for the five tall plants (blackbutt, bangalay, old man banksia,
   coast banksia, swamp paperbark) and the grass tree's flower stalk, the coast's own fire drill. Every row's numbers
   are from Bootle's *Wood in Australia* (2005) for the commercial eucalypts and the paperbark and from Ilic et al.
   (2000, *Woody density of Australian plants*, CSIRO) for the banksias, read from memory of those tables and so
   recorded as a debt to re-read against the tables themselves before a fire's physics leans on them; the grass tree's
   stalk carries an estimate and says so in its source. A test holds every tall species to a wood row with a source.
2. **A thing's own state is one record with one owner.** `ThingState` (engine): a mask (`ThingFields`) saying which
   fields the thing carries of its own — mass, length, diameter, moisture, edge, platform angle, flakes taken, look,
   condition, marks — and the fields. `ThingWire.Write`/`Read` is the one layout (u16 mask, then each present field
   in mask order: f32 mass kg, f32 length m, f32 diameter m, f32 moisture as a share of dry mass, f32 edge 0–1, f32
   platform degrees, u16 flakes, u8 look, f32 condition 0–1, u16 marks), refusing an unknown bit, a number that is not
   one, a negative, an edge or condition over 1, a platform over 180. `ItemComponent` becomes rest, fall and a
   `ThingState`. The entity wire (protocol 17), the carrying message (protocol 17, which now carries every thing's
   state: DEBTS 2026-09-16, "a struck hammer's mass is not told", paid), the region file (version 4), the player file
   (version 7) and the digest write and read it through `ThingWire` and nothing else. Region files 1–3 and player files
   2–6 still read: a stone's four fields become a state whose mask says mass, edge, platform and flakes when it had a
   mass of its own, and nothing when it had none. `save_check.py` restates the layout and the digest's line.
3. **A lying thing's properties come from its place, as its position does.** `LyingProperties.StateOf(thing, site,
   cellM)` (engine, pure, whole-number hashing by `StandLayout`'s mixer with a salt of its own): a stick's species is
   the tall plant on its cell or the nearest trunk in the cell's 3 × 3 (none: the plain stick, as driftwood will be);
   its length is 0.4 to 1.6 m and its diameter 12 to 45 mm, hashed; its moisture is the cover's wetness quarter read as
   a share of dry mass (0.15, 0.22, 0.32, 0.45 — the fibre saturation point is about 0.30, so the wettest quarter's
   sticks are sodden); its mass is the wood's dry density × π/4 × d² × L × (1 + moisture); its look is the layout's
   variant, the shape it lay in. A cobble's stone is the stone layer's; its mass is 0.35 to 1.0 kg hashed, weighted by
   the stone's density against 2600; its diameter follows from its mass as a sphere's × 1.15; its look the variant. A
   thing taken up keeps this state (`Hands.PickUpLying`), and so does a litter cobble a blow turns into an item where it
   lay, with the layout's yaw rather than zero (DEBTS 2026-09-16, "a litter cobble struck loses its yaw", paid). The
   server reads the site from its rasters (`LyingSites.Of`); the client from its tiles through the same function, so
   the words it shows for a thing on the ground are the server's.
4. **A stick is of its tree.** `DefinitionCatalogue.StickOf(species)`: `item/stick-blackbutt` and one for every tall
   plant, each carrying its `Wood`; the plain `item/stick` stays for a stick of no tree. `Definition` gains
   `Substance` (stone, wood, none) and `WoodOf`, so a rule asks a thing's properties, never its key.
5. **A thing is called by what it is.** `ThingWords.Describe(definition, state)` (engine): "a blackbutt stick, arm-long,
   thumb-thick, dry", "a silcrete flake, sharp, 20 g", "a quartz cobble, 0.6 kg"; the thresholds (hand-long under
   0.25 m, forearm to 0.5, arm-long to 0.9, a pace to 1.5, long beyond; finger-thick under 15 mm, thumb to 30, wrist to
   60, arm beyond; dry under 0.20, damp to 0.30, wet to 0.40, sodden beyond; dull under 0.2 of edge, keen to 0.5, sharp
   beyond) live there and nowhere else. The verb line names a thing looked at by it, and the Tab window by it with the
   thing's own mass; `HudController` and `VerbController` hold no words of their own for a thing.
6. **A thing keeps its shape.** `ItemLooks.TryLook(definition, id, state)`: the state's look when it has one, else the
   id's, so a stick put down is the stick that lay there (DEBTS 2026-09-11 paid); a stick of any tree is drawn as a
   stick. The flake's own shard stays owed (DEBTS 2026-09-16).
7. **Proved in the built game.** `carry.py --scenario controls` on the harness build picks up a stick and a cobble,
   puts them down and saves; `save_check.py` on that world reads a version-4 region file and a version-7 player file
   and is GREEN; the run's log carries the stick's words.

## Non-goals

No process or work (BF.2); no new kinds of lying thing and no driftwood (BF.3 opens the layer, BF.4 places them); no
weather in a lying thing's moisture and no drying (BF.7); no change to what a stick or a cobble looks like beyond the
kept shape; no limit on the hands' weight or bulk; no change to the knap's physics (it reads the same fields through
the new record).

## How it is proved

- Tests first, red: `ThingStateTests` (the round trip of every field; an empty mask is two bytes; an unknown bit, a
  NaN, a negative, an edge over one refused), `WoodTests` (every tall species has a row with a source; softness and
  friction fall out of the numbers as stated), `LyingPropertiesTests` (deterministic; the species from the cell and
  from the 3 × 3; the mass from the density, the size and the moisture; the look the layout's variant; the cobble's
  mass by the stone's density), `ThingWordsTests`, `HandsTests` (a stick taken keeps its state), `EntityWireTests`
  (protocol 17; a carried flake's edge crosses the wire), `RegionSaveTests` (version 4 round trip; versions 1–3 and
  2–6 read as before; the digest names the new fields), the Unity edit-mode `ItemLooksTests` (a state's look wins).
- Sabotage, restored byte for byte: the mask's field order swapped in `ThingWire.Write`; the state dropped from the
  carrying message; the species ignored in `StateOf`.
- `dotnet test Engine/EarthGame.slnx > test.log 2>&1; echo $?` with the count as run; the Unity-shaped compile; the
  edit-mode tests.
- Promise 7's run and `python Tools/verifiers/checks/save_check.py <that world>`; `tile_check.py` on the gate world
  (the tile format is unchanged and must say so).
- Docs in the same commit: ARCHITECTURE §5 (things, the record, the lying thing's properties) and §10 (protocol 17,
  region 4, player 7, the digest's line); DEBTS: 2026-09-11 (shape) and 2026-09-16 (yaw and mass told) paid, the wood
  table's re-reading opened; BETA_MAP §7 marks BF.1 built.

## What changed on the way

- **The stone travels as a tile.** Promise 3 says a client reads a lying thing's site from its tiles through the server's own
  rule; no tile carried the stone, so a cobble on the ground could not be named. `TileLayer.Stone` (8, a code layer like the
  cover) was added, served by `TileService`, asked for with the rest, checked by `tile_check.py`'s new row; the tile format's
  version is unchanged, a layer byte being additive. A test world that "has every layer" now has a stone raster.
- **FP.3's four names stay.** `ItemComponent.MassKg`, `Edge01`, `PlatformDeg` and `FlakesTaken` are properties that read and
  write the record and mark the field the thing's own, so the knap's arithmetic and its tests are written as they were;
  `HasStoneState` became `HasOwnState`, since a stick as it lay has state of its own too.
- **The built run is the litter scenario, not the controls scenario.** The controls scenario's stick and cobble are spawned by
  `-eg-items` and lie nowhere, so they have nothing of their own to show; the litter scenario takes a stick of the world's own
  litter, and its recorder now writes the words the crosshair gave the stick on the ground and the words the hand gives it.
  Both scenarios were run; both worlds were verified.
- The Unity edit-mode run reports one failure, `ReviewOpusProbe.ObserveEviction`, from the untracked probe file in the working
  tree that is never committed; the project's own ten tests, `ItemLooksTests` among them, pass.

## Exit record (2026-09-22)

1. `Wood`: six rows (the five tall plants and the grass tree's stalk), each with a source; `WoodTests` holds every tall plant to
   a row and the derived softness and friction-fire band to the numbers. The re-reading of the numbers against the tables is a
   DEBTS row.
2. `ThingState`, `ThingFields`, `ThingWire`: the one layout in the entity wire, the carrying message, the region file (4), the
   player file (7) and the digest; region files 1 to 3 and player files 2 to 6 read as before (`RegionSaveTests`' fixtures).
   `save_check.py` restates the record and the digest's line.
3. `LyingProperties.StateOf`, `LyingSites` (the server's rasters), `LyingSiteReader` (a client's tiles): the same words on both
   sides — the litter run's crosshair and hand both said "a swamp paperbark stick, a pace long, wrist-thick, sodden"; a taken
   stick keeps its state and a struck litter cobble its yaw (`LyingPropertiesTests`).
4. `DefinitionCatalogue.StickOf`, `Substance`, `WoodOf`: five stick definitions, 56 definitions in all (`DefinitionTests`).
5. `ThingWords.Describe` and `MassOf`: the verb line and the Tab window name a thing by them (`ThingWordsTests`).
6. `ItemLooks.TryLook` draws the state's look; `StandLayout.LookOf` is the one rule (`ItemLooksTests`, edit mode, passed).
7. The built runs on the harness build (`Build/Harness`, BF.1, e71072f2e+dirty): `carry.py --scenario controls`, 40 checks, none
   failed, 10 answers Done, 0 errors, 0 corrections; `carry.py --scenario litter`, picked up, put down and kept carried, 3
   answers Done, 0 errors, the words above in its `run.jsonl`. `save_check.py` GREEN on both worlds (a version-4 region file and
   a version-7 player file each; the digest rebuilt equal); `tile_check.py` GREEN on both caches, 191 files, 9 stone tiles
   agreeing post for post.

Counts as run: `dotnet test Engine/tests/EarthGame.Tests` 780 passed, 0 failed (762 before; 18 new); sabotage three of three
caught (the mask's field order swapped: 2 of 6 failed; the state dropped from the carrying message: 2 of 16; the species
ignored: 1 of 7), each file restored byte for byte; the Unity-shaped compile green; edit mode 10 of the project's 10.
