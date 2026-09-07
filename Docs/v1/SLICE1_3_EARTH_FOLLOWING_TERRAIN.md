# Slice 1.3 — Earth-Following Procedural Terrain

Phase 1, slice 3 of 4. Re-scoped 2026-08-25 on the owner's direction: *"since it is realistically
impossible to accurately map the whole world, use procedural generation that follows earth, and
what is known to be in certain regions."*

**Goal.** Real data stays the skeleton; procedural generation invents everything below its
resolution, conditioned on what is known about each place. The ground stops being noise draped
over a heightmap and starts being *this kind of landscape, here, for a reason*.

## Why this rather than more data

The baked dataset resolves ~10 km. Real elevation at the resolution a walker notices is not
shippable: 30 m global is terabytes, 1 m is a planetary archive. Even a single pilot region at
30 m only moves the problem — walk out of it and the ground goes flat again.

More importantly it is the wrong shape of solution for this project. GAME_DESIGN §6 says model
the reasons a thing works rather than authoring the thing; §24 sets the target as *causal
fidelity*, not stored detail. A landscape has its shape because of uplift, erosion, drainage,
glaciation and lithology. Encoding a little of that generalises everywhere and improves with the
algorithm. A downloaded heightmap improves only with the download.

**The contract with reality stays absolute:** procedural generation may never move a coastline,
a mountain range, a basin or a summit that the data knows about. It only fills the gaps between
what the data can see.

## Architecture

Four layers, each a pure function of position (plus the dataset). No storage, no streaming.

### 1. Skeleton — the dataset (unchanged)
`EarthElevation` bilinear elevation, ~10 km resolution. Absolute truth for anything it resolves.

### 2. Context — what is known about this place
A cheap struct derived per sample point, all of it from data already present:

```csharp
// Assets/EarthGame/Sim/World/TerrainContext.cs — namespace EarthGame.Sim
public readonly struct TerrainContext
{
    public readonly float ElevationM;      // dataset elevation
    public readonly float RuggednessM;     // local relief across the surrounding cells
    public readonly float SlopeGrade;      // magnitude of the local gradient
    public readonly float LatitudeDeg;     // climate proxy until Phase 3 brings real climate
    public readonly float CoastDistanceM;  // estimated, from the local sea-level crossing
    public readonly float SeaFloorDepthM;  // 0 on land
}
```

Later slices widen this without changing its consumers: real lithology (4.1), real climate and
precipitation (3.1), drainage (3.2). That is the point of putting a named struct here.

### 3. Classification — terrain character
`TerrainClass` weights, summing to 1, so classes blend rather than tile:

| Class | Recognised by | Character |
|---|---|---|
| `AbyssalPlain` | deep, low ruggedness | almost flat, rare seamounts |
| `ContinentalShelf` | shallow, submerged | very gentle seaward slope |
| `CoastalPlain` | low, near coast, flat | subtle, long-wavelength swells |
| `Plateau` | elevated, low ruggedness | flat-topped, incised by valleys |
| `Hills` | moderate ruggedness | rounded, fractal, drainage-veined |
| `Escarpment` | high slope grade | steep faces, benched |
| `Alpine` | high elevation + rugged | ridged crests, U-shaped valleys |
| `DuneDesert` | low latitude, arid proxy, flat | anisotropic wind-aligned waves |

Classification is deliberately *soft*: a point is 0.6 Hills, 0.3 Plateau, 0.1 Escarpment, and its
terrain is the weighted sum. No seams, and adding a class never creates a hard boundary.

### 4. Synthesis — per-class shaping
Each class supplies fractal parameters and an operator:

- **Amplitude and base wavelength** per class (alpine 600 m at 2 km; coastal plain 15 m at 3 km).
- **Operator**: `Fbm` (rounded), `Ridged` (crests), `Billow` (dunes), `Terraced` (benched
  escarpments), `Eroded` (Fbm biased downward by local slope, cutting valleys rather than
  adding hills).
- **Anisotropy**: dunes and ridges align to a direction derived from context, not to the
  noise lattice.

All of it clamps to the planet's legal band and is suppressed within ±40 m of sea level, so
coastline remains the data's alone.

## Determinism and cost

Same position ⇒ same terrain, forever, with no seed drift: the generator is a pure function of
position and dataset. Budget: terrain sampling already dominates chunk building, and this adds
one context evaluation (≈6 grid lookups) plus one weighted-fractal evaluation per sample. Target
is no worse than a 1.5× increase in chunk build time; measured with `WorldProbe`, and the LOD
build budget already spreads the cost across frames.

## Validation tests (§36)

1. **Data is never overridden** — across 2,000 random points, the synthesised height stays within
   the class's stated amplitude band of the dataset elevation, and the sign of elevation (land vs
   sea) is unchanged at every one.
2. **Coastlines are untouched** — for 500 points within ±40 m of sea level, synthesis contributes
   under 1 m.
3. **Classification is sane at known places** — the Himalaya classify Alpine-dominant, the
   Nullarbor and the Sahara flat-dominant, the abyssal Pacific AbyssalPlain-dominant, the NSW
   escarpment Escarpment-present. Owner picks additions at gate time.
4. **Weights partition** — class weights sum to 1 ± 1e-4 everywhere, so no point is unclassified
   or double-counted.
5. **Continuity** — no step greater than the local amplitude between samples 1 m apart, over a
   10 km transect crossing a class boundary. This is the anti-seam test.
6. **Determinism** — identical results across runs and across floating-origin rebases.
7. **Relief is actually there** — over a 2 km walk in each class, measured relief falls inside a
   per-class band (hills 20–200 m, coastal plain 2–30 m, alpine 150–900 m). This is the test that
   would have caught the billiard-table ground directly.

## Gate contribution

1. The owner picks any three real places; each *feels like that kind of place* — the Southern
   Highlands roll, the Nullarbor is flat, an alpine region has ridges and valleys.
2. Walking 2 km produces terrain worth walking: slopes, crests, hollows, somewhere to aim for.
3. Coastlines and mountain ranges still match a real map (slice 1.1's gate must not regress).
4. Automated: the suite above, green headless.

## Deliberately deferred

Real rivers and drainage networks (3.2) · real climate driving aridity (3.1) · real lithology
(4.1) · caves and overhangs, which the heightfield cannot express at all · glacial modelling
beyond a shaping operator · any high-resolution DEM overlay.
