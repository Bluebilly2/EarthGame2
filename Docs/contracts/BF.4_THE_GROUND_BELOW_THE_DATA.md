# Contract BF.4 — The ground below the data

**Status:** drafted 2026-09-23, the fourth of the beta's foundations (`Docs/BETA_MAP.md` §7, F4; CANON ruling 41: "Treat
the world itself as a missing foundational system"). Stage one built and closed 2026-09-23 (6172bd5; its exit record below);
stage two built 2026-09-24 (its record below); stage three to come. Owner: Claude (the main session). Built in three stages, each
shippable alone: stage one, one ground with relief below the data; stage two, the ground underfoot; stage three, rock
that stands, the stone of the place and the country's things. William's lane: his eyes on the ground in frames and in
play, his hands on the walk, and his yes to any data the stone of the place needs.

## Why this next

William's direction of 2026-09-22: the world is "flat land covered in trees". At the data's scale the ground is right
(the Kangaroo Valley's escarpment proves it), but below the raster's 4 m there is nothing: a heath is a smooth green
plane, a forest floor a billiard table, a rock platform as flat as a beach (DEBTS 2026-09-10, "The country is empty and
smooth", its lack (ii)). Five of the beta's chains bottom out here (BETA_MAP §5: G2 reading the land, G3 the stone at the
creek, G6 the tinder under the tree, G9 choosing ground, G10 the camp's floor). And the ground is two grounds today,
which the survey of 2026-09-23 found: the server judges by the raster's bilinear heights in float metres, the client
walks a Terrain resampled at 1.953 m from the tiles' centimetres, and a dug hollow is the client's alone (DEBTS
2026-09-23). Relief built on two grounds would be two reliefs; so the first promise makes the ground one.

## The shape

One function of the ground's height at a point, `FineGround`, in the engine, read by the server wherever it asks for the
ground and by the client wherever it places, draws or walks on it. It is the raster's bilinear height over the posts the
tiles carry (the centimetres, so both sides start from the same numbers), plus a relief term computed from what both
sides hold (the cover byte, the stone code, the raster's own slope, the water's class and depth, the world's changes),
by whole-number hashing as the litter's placement already is. No layer is added and nothing new travels for stage one;
the client's Terrain posts sample the function, so what is drawn is what is walked and what the server judges.

## Promises

### Stage one: one ground, with relief below the data

1. **One ground.** `FineGround.At(east, north)` (engine) is the height every reader uses: `WorldState.GroundAt` and its
   callers on the server (placement, the fall of things, the validator, the animals, the verbs' reach and water), and on
   the client `TileGround`'s callers (the trunks, the litter and the tufts placed, the verbs' ground and water) and
   `TilePreparation.SamplePosts` (the Terrain's posts and its collider). The server rounds its heights to the centimetre
   at load, as the tiles do, so both compute from equal inputs. Proved by a test that sets a server's world and a
   client's held tiles side by side and finds them equal to a millimetre at ten thousand random points, and by
   `tile_check` staying green (the tiles themselves are unchanged).
2. **Relief below the data.** `Relief.At`: smooth value noise on seeded lattices no finer than 5 m (what the Terrain's
   1.953 m posts can carry), in two octaves, each turned off the region's axes so no row of humps lines up with the map; its
   amplitude and lattice set by each corner's cover and blended by the same bilinear weights as the heights so no cell edge
   shows. The table as built (the draft's is in "What changed on the way"), a model to be judged in frames:

   | Cover | Amplitude | Lattice | What it stands for |
   |---|---|---|---|
   | Forest floor | ±0.12 m | 6 m | root humps, old falls, the pits trees left |
   | Bracken | ±0.10 m | 6 m | the same floor under a fern |
   | Heath | ±0.10 m | 5 m | hummocks round the shrubs |
   | Grass, sedge | ±0.06 m | 5 m | tussock ground, smoothed to the posts |
   | Rock | ±0.10 m | 5 m | broken rock, a platform's benches |
   | Bare earth | ±0.05 m | 5 m | |
   | Swamp floor | ±0.04 m | 5 m | hummock and hollow |
   | Dune sand | ±0.04 m | 12 m | bare sand the wind smooths |
   | Beach sand | ±0.02 m | 12 m | a beach the swash smooths |
   | Sea, fresh water, a cover not known | none | | the water, the wading and the shore stay as they are |

   Toward water it fades by the smootherstep of the quad's dry share (the bilinear weights of its dry posts): a tenth left at
   a quarter dry, under a hundredth at a tenth. Bounded by tests: never more than 0.12 m from the raster (`Relief.MostM`);
   no more than 6° added to any slope (`Relief.SteepestDeg`); the Terrain's triangles within 5 cm of the function on relief
   alone (`Relief.TerrainStrayM`); deterministic by the seed; none where water stands at every post. **The walk is judged on
   the raster's slope as the Terrain's posts sample it** (`ClientGround.TryWalkNormalAt`, read by the client's ground probe
   on the Terrain), what it was judged by before the relief: the relief moves the feet and the eye, never makes ground the
   data calls walkable slide, and neither speeds nor slows the pace, so ground the raster calls walkable stays walkable by
   construction rather than by a bound on the relief.
3. **The hollow joins the ground.** A dug cell's depth (BF.3) is a term of `FineGround`, a bowl three-quarters of a cell
   wide, on both sides: the server's ground is dug where the client's is, and the client's `TerrainTileBuilder.Dig`
   samples the function again rather than keeping its own bowl. Pays DEBTS "The hollow is the client's alone".

### Stage two: the ground underfoot

4. **Underfoot.** `Underfoot.Of(cover, wetness quarter, water depth, slope)` (engine): firm, soft, loose, boggy or rock,
   with Pandolf, Givoni and Goldman's terrain coefficient for each, the table `Locomotion.TerrainFactor` already owns
   (made ground 1.0, light brush 1.2, heavy brush 1.5, loose 1.8). The walk: the speed at a given effort is the firm
   ground's over the coefficient's square root (their cost is the coefficient times the square of the speed), so loose
   sand is walked at about three-quarters of the firm pace; the client's mover and the server's ceiling read the same
   law. The body: the server's exertion counts the coefficient, so a founder toiling through sand warms and thirsts as
   the cost says. The footsteps (`Footing`) group the same answer for the ear, one owner of what lies underfoot.

   *Amended 2026-09-24, before it was built.* `Underfoot.Of(cover code, water depth)` gives the grounds of Pandolf,
   Givoni and Goldman's table (J Appl Physiol 43:577, 1977, from Soule and Goldman 1972), their own coefficients: made
   ground 1.0 (rock), a dirt road 1.1 (bare earth, and a beach's sand in the wettest quarter, which packs), light brush 1.2
   (grass, the forest floor, sedge in the drier half), heavy brush 1.5 (heath, bracken), a swampy bog 1.8 (a swamp's floor,
   sedge in the wetter half) and loose sand 2.1 (a dune, a beach's dry sand). The table held 1.8 for "sand, scree, deep
   mud", which are two of its rows. Water over the top of a foot leaves the walk to the wading law, and the slope is the
   walking table's already. The walk: at one effort, the speed whose moving cost is the same, the cost going as the
   coefficient times the speed squared, relative to light brush, the ground the walking table's speeds stand for in this
   country: loose sand 0.76 of today's pace, a bog 0.82, heavy brush 0.89, bare earth 1.04, rock 1.10. The client's mover
   takes the ground at its feet from the cover it holds; the server's ceiling takes the faster of the cells a report leaves
   and reaches, and a fall's ceiling the fastest ground. The body: effort is the gait's, so the server counts a founder's
   exertion by the speed they would make on light brush, and a runner slowed by sand is still running; the heat a second
   is the gait's, and the cost of a kilometre rises with the coefficient because the kilometre takes longer. The
   footsteps keep their own grouping of the same cover byte, since the ear's classes are not the legs' (bracken is heard
   as grass and walked as heavy brush); the cover byte and the water's depth are the one owner of what lies underfoot.

### Stage three: rock that stands, the stone of the place, the country's things

5. **Rock that stands.** Boulders on rock cover and where cobbles lie on thin soil, and ledges on crests and cliffs,
   placed per cell by a rule of the layers both sides hold (cover, stone, loose code, slope), sized by a hash; drawn by
   the client, given bodies as the trunks are (`TrunkBodies`' pattern), and kept clear by the server's placement (a thing
   let go never lands inside a boulder). A boulder is a knapper's anvil later (BF.2).
6. **The stone of the place.** A stone rule per region read from a real geological map in place of the lattice: for
   New South Wales, the Geological Survey's seamless geology, fetched only on William's yes to the exact files, its
   units mapped to `StoneType` with a source for each mapping. Pays DEBTS "The valley's stone is the coast's rule", and
   Bherwerre's lattice with it.
7. **The country's things.** A second loose layer (tile layer 9, protocol 21): driftwood on beaches and lake shores,
   bark and tinder under the trees that shed them, dead wood (fallen limbs, rotting logs) in forest by the stand's
   height, gravel in creek beds; each kind with its properties from its place as BF.1 gave the sticks, taken as a bit
   beside the layer (a new change layer in the region file, which its length-prefixed diffs allow without a version).

## Non-goals

No change to the raster, the bake or the region's extent (the side worker's WG.2b). No finer bake (the GA 5 m tiles
wait on William's word: DEBTS "The cliffs are thin"). No lake bed rule (its own row). No erosion, no ground that changes
by itself. No fire, no structures (BF.5, BF.6).

## How it is proved

- Tests first, red: `FineGroundTests` (the server and the client equal to a millimetre over held tiles; the rounding;
  the hollow on both sides), `ReliefTests` (the bounds: amplitude, walkability, the collider's triangles, water's taper,
  determinism, no seam at a cell edge), `UnderfootTests` (the classes by cover and wetness, the speed law against
  Pandolf's cost, the exertion), and stage three's placement tests.
- Sabotage, restored byte for byte: the server's ground without the relief; the relief under water; the hollow on the
  client alone.
- `dotnet test Engine/tests/EarthGame.Tests` redirected to a file with its exit code; the Unity-shaped compile; the
  edit-mode tests.
- In the built game: the corpus loop's join check with every N2 row green (the walk on one ground draws no more
  corrections than today's budget allows), the controls, carry, drink and changes scenarios, `tile_check` and
  `save_check`; frames at 1440p and 1080p from the vantages of both regions for William's eyes, low sun and midday, with
  and without the relief (`-eg-hide relief`), and his hands on the walk.
- The cost: `Relief.At` timed per call and per tile prepared, on the worker and on the server's step; stated in the exit
  record.
- Docs in the same commit: ARCHITECTURE §5 (the ground), §10 when a layer or the protocol changes (stage three), the
  decision log; DEBTS paid (the country's smoothness (ii), the hollow) and added (the relief's table as a stated model);
  BETA_MAP §7.

## Exit

The promises kept with the counts as run, or "What changed on the way" saying which was not and why.

### Stage one (2026-09-23)

**What was built.** `FineGround` (engine) is the one function: a quad of four posts (`GroundQuad`, each post's height as a
tile carries it by `TileCodec.PostMetres`, its cover byte and its dug depth), the raster's bilinear height, plus `Relief`,
less the hollow (a cone to each dug post's depth, three-quarters of a cell round it). The server builds its quads from its
rasters and changes (`FineGround.TryQuad`) and `WorldState.GroundAt` is the function; `RasterGroundAt` keeps the raster for
what water is measured over (`WaterAt`, the animals' dry test) and the openness keeps it too. The movement check judges the
feet against it (`FineGroundSource`). The client builds its quads from the tiles it holds (`ClientGround`): the Terrain's
posts are sampled from it on the worker with a copy of the tile's hollows and room below for the deepest hole
(`TilePreparation.DigRoomM`), a Terrain built before its cover is built again when the cover arrives, a dig resamples every
Terrain the hole reaches (`TerrainTileBuilder.Resample`, in place of BF.3's own bowl), the trees, the litter, the trunks'
bodies, the tufts and the crosshair's search for lying things stand on it, and a corrected founder is lifted onto it.
`-eg-hide relief` draws and stands the client on the ground without the relief, for frames beside ones with it.

**As run.** `dotnet test Engine/tests/EarthGame.Tests`: 832 passed, 0 failed, the side worker's uncommitted region tests
among them (fifteen new here: `FineGroundTests`, 8, `ReliefTests`, 7). The server's ground and the client's over the tiles of a made world of every cover, a lake, a creek, the
sea and three hollows (one on a tile's edge, one on four tiles' corner): equal at 10,000 random points, 2,002 points on the
tiles' shared edges and 192 round the hollows, the largest difference 0 m; the relief reached 0.108 m there. The largest
relief met by cover within its table (forest floor 0.110 of 0.12 m, heath 0.095 of 0.10, rock 0.094 of 0.10, beach 0.018
of 0.02); the steepest tilt the relief adds over half a metre 2.3°; the Terrain's triangles 1.7 to 2.0 cm from the relief
(forest floor, heath, rock); things on a tile within 0.04 mm of the server's ground, and the 122 past its west and south
edges within 0.006 mm. The cost in the suite's Debug build: the server's ground 449 ns a call, a tile's 269 ns, a kilometre
tile's 513 posts a side 82.5 ms on the worker. Three sabotages, each restored byte for byte: the server's ground without the
relief (nine of the fifteen red), the relief under water (two red), the hollow on the client alone (three red). The
Unity-shaped compile clean; the edit-mode tests 10 of 10 (the uncommitted review probe's own failure aside).

In the built game (Build/Harness, `BF.4-stage1`, the walk still on the raster's own slope, 2026-09-23): the changes scenario
0 errors, its one correction the panel's stand at the wake, every work done; the Terrain within 0.0148 m of the one ground
at 400 rays round the wake (0.0019 m on average), the relief there 0.080 m; the 10 cm hole 0.100 m deep in the one ground,
the Terrain 0.0376 m above its point; `save_check` and `tile_check` ok. The drink, the wade, the carry and the controls (40
checks) each 0 errors and 0 corrections. The dune scenario walked its 33.8° face down at 0.56 m/s and up at 0.43 m/s and
kept a sliding founder's feet (0.022 m under at the lowest), and was corrected nineteen times on its way to the slide's
face: see "What changed on the way", 7; the walk now reads the Terrain's squares, and the scenarios are run again on it.

On the corrected walk (Build/Harness `BF.4-stage1-walk`, 6172bd5 with the side worker's then uncommitted region files): the
dune scenario 0 corrections (down 28.7° at 0.57 m/s, up 29.5° at 0.44 m/s, the slide's feet 0.004 m under at the lowest);
the changes scenario and the controls (40 checks) as before, 0 errors. The corpus walk (`Artefacts/corpus/bf4-20260923T003203Z`:
two players at 100 ms ± 20 ms and 2 % loss, and one SOLO, ten minutes each): `join_check --only N2` 12 rows, 0 failed; three
corrections a player, all at one place, a dune face at the walk's limit at (-1420, 2970) where the walker stalls, slides
1.8 m and is corrected on landing, 0.89 m at the most (DEBTS, "A slide's landing is corrected"); none on the named
segments. Frames at the vantages of both places at 07:30 and noon, with the relief and without it (`-eg-hide relief`),
1440p and 1080p (`Artefacts/frames/bf4-relief-20260923T003203Z`, 108 pairs): the relief changes 1.2 to 9.8 per cent of a
frame's pixels by more than 8 levels in 256; no seam, no grid, no floating tree in the pairs looked at. His eyes on them are
DEBTS' first table's.

**What changed on the way.**
1. *The table.* The draft's heath at ±0.15 m on 4 m, dune sand at ±0.20 m on 8 m and a stepped ±0.30 m on steep rock gave
   way to the table above: the finest lattice 5 m, so the Terrain's triangles stray 2 cm rather than more than the draft's
   3 cm bound; dune sand is the bare sand the cover names (a vegetated dune is heath or grass by the cover's own rule, so the
   hummocks round its plants are theirs); ledges on steep rock are rock that stands, stage three. The largest amplitude is
   0.12 m, so the bound is 0.12 rather than 0.35.
2. *Walkable by construction.* The draft bounded the relief so walkable ground stayed walkable post pair by post pair. No
   relief worth drawing can promise that near the 35° limit: any bump steepens one side. A fade by the raster's slope would
   need each post's neighbours, which a tile's edge posts do not have in the tile, and the Terrains' shared edges would
   crack. So the walk reads the raster's own slope and the relief only the height (§ promise 2).
3. *The fade toward water.* The draft said none within 3 m of water. Built as the smootherstep of the quad's dry share, which
   is flat at both ends: the fourth power first tried fell steepest right beside the dry post, a 7° tilt from the fade alone.
4. *Rounding at the post, not at load.* The server rounds each post as the tile does when it reads it, so `Heightfield` and
   the raster are unchanged and a raster is not held twice.
5. *Things past a tile's edge.* A cell's layout puts some of its things up to half a cell outside its tile, on the tiles
   west and south; a worker holding the tile alone stood them on its edge carried on, up to 0.12 m off where the cover
   changes at the edge. The stand's worker is handed the one ground over the tile and those three (`GroundSnapshot`), and a
   tile is placed again when a neighbour's ground or cover arrives. A reader holding one tile only now carries its edge on at
   the edge's own slope rather than flat.
6. *The cover's water, not the water's class.* The relief fades toward posts whose cover is the sea or fresh water, which
   `GroundCovers.Of` sets from the water's class, so the client needs no tile of the class to grow the same relief.
7. *The walk's slope from the Terrain's squares, not the raster's cells.* The raster's own bilinear slope, the first built,
   is steeper than the Terrain ever was where a cell's surface folds: at the gate world's dune foot 37° where the square of
   Terrain posts round the same point reads 32° (twenty samples of the 221 m walk to the slide's face over the limit by the
   one and not the other). The dune scenario's founder slid there, landed at 7.9 m/s and was corrected nineteen times; no
   earlier dune run had a correction. The walk reads the raster over the square of the Terrain's posts, 1.95 m, as the
   Terrain's own triangles did before the relief (`TheWalkIsJudgedByTheRasterAsTheTerrainSamplesIt`).

### Stage two (2026-09-24)

**Built.** `Underfoot.Of` (engine) reads a cover code and the water over it as Pandolf, Givoni and Goldman's grounds, with
`GroundType` gaining Firm and Bog and loose sand taking its own 2.1. `Locomotion.GroundPace` is the pace at one effort
against light brush, and `SpeedMs`, `MaxHorizontalSpeedAt` and `Mover.Step` take a ground, the table's by default. The
server reads its cover layer for it (`WorldState.UnderfootAt`) and the client its cover tile (`Footing.GroundAt`, beside
the ear's reading of the same tiles, which is unchanged). The validator judges a report on the faster of the grounds it
leaves and reaches, and a fall on the fastest. The server counts exertion by the pace on the table's ground.

**As run.**
- `dotnet test Engine/tests/EarthGame.Tests`: 856 passed, 0 failed; `UnderfootTests` 7 of them. Among them, the pace on
  every ground costs what the table's walk costs by Pandolf's own equation, and a runner off rock onto dry sand is never
  corrected.
- Two sabotages, each restored byte for byte: every ground walked at the table's pace turned three red; a dune walked as
  grass, one.
- The Unity-shaped compile clean; the edit-mode tests 10 of 10.
- On the built game (Build/Harness, `BF.4s2` builds):
  - the dune scenario walked down 25.5° of dune sand at 0.49 m/s and up 27.7° at 0.36, the flat on that sand 1.05 m/s
    where the table's ground walks 1.39;
  - at the face, it kept its feet on 34.4° ground 2.2 m from a 36.8° face;
  - wading went 1.20 m/s on land and 0.69 in the water;
  - swimming, drinking, the controls (42 of 42), carrying, knapping, the looks and the changes scenarios all passed,
    with 0 errors each.
- The corpus walk (two players at 100 ms and 2 % loss, and one SOLO, ten minutes each), after the stride's allowance
  (below): `join_check --only N2` 12 rows, 0 failed, 0 corrections for all three walkers
  (`Artefacts/corpus/underfoot-stride-*`).

**What changed on the way.**
1. *The stride's allowance.* The first corpus walk on the new ground was taken on a build without it. The SOLO walker was
   corrected ten times in ten minutes, each a runner crossing onto a slower ground and braking: 3.71, then 3.51 m/s
   against dry sand's 3.37. A body slows at the brake's rate, not in one step. So the run a ground allowed is carried as a
   landing's fall is, less half the brake's work since (`PlayerSession.Stride`). With it, the same walk had no correction
   (`ARunnerOffRockOntoSandBrakesWithoutACorrection`). This was not in the amended promise.
2. *The dune scenario's bounds.* Its descent had to be faster than half a metre a second and slower than the flat. Those
   bounds were the table's, and dune sand walks at three-quarters of it, so they are now the face's own ground's.
3. *The scenarios' aim.* The changes scenario's founder now stopped a pace from where it had. It pulled another tuft and
   cleared another cell, whose bundles came to lie over the nearest stick, so the crosshair met a bundle three times. Its
   stick to point is now one with no thing lying within 2.5 m. A pick-up that does not find its thing under the crosshair
   now steps within a metre of it and looks down on it, as a person would. The game is unchanged.
4. *The footsteps* keep their own grouping of the cover (the amendment): the ear's classes are not the legs'.

What waits on William: his hands on the walk (DEBTS, his table: "The ground underfoot"); the model's stated parts are
DEBTS' "The ground underfoot is a stated model".
