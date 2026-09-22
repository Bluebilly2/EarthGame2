# Contract BF.4 — The ground below the data

**Status:** drafted 2026-09-23, the fourth of the beta's foundations (`Docs/BETA_MAP.md` §7, F4; CANON ruling 41: "Treat
the world itself as a missing foundational system"). Owner: Claude (the main session). Built in three stages, each
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
2. **Relief below the data.** `Relief.At(east, north, site)`: smooth value noise on seeded lattices no finer than 4 m
   (what the Terrain's 1.953 m posts can carry), its amplitude and wavelength set by the site, blended across cells by the
   same bilinear weights as the heights so no cell edge shows. A first table, stated as a model to be judged in frames:

   | Where | Amplitude | Wavelength | What it stands for |
   |---|---|---|---|
   | Forest floor, bracken | ±0.10 m | 6 m | root humps, old falls, ruts |
   | Heath | ±0.15 m | 4 m | hummocks round the shrubs |
   | Grass, sedge | ±0.06 m | 5 m | tussock ground |
   | Dune sand | ±0.20 m | 8 m | the hummocks a vegetated dune piles round its plants |
   | Beach sand | ±0.02 m | 12 m | ground the swash smooths |
   | Rock, steeper than 30° by the raster | ±0.30 m, stepped | 1 m of height | ledges on a cliff face, as a softened staircase |
   | Rock, gentler | ±0.08 m | 4 m | the benches of a platform, thin soil over rock |
   | Bare earth, swamp floor | ±0.05 m, ±0.04 m | 5 m | |
   | Water standing, and 3 m from it | none, tapering | | the water, the wading and the shore stay as they are |

   Bounded by tests: never more than 0.35 m from the raster; ground the raster's own triangles call walkable (35° or
   less) stays walkable post pair by post pair; the collider's triangles and the function differ by 3 cm at most on
   relief alone; deterministic by the seed; zero wherever water stands.
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
