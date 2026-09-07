# Slice 1.1 — Real Earth Elevation

Phase 1 (*Real Earth, Really There*), slice 1 of 4. See `Docs/ROADMAP.md` for the phase gate.
Implements `Docs/GAME_DESIGN.md` §2: the planet stops being invented and becomes Earth.

**Goal.** Replace procedural noise with real global elevation data behind the existing seam
`TerrainNoise.SurfaceRadius(dir)`, so the continents, coastlines, mountain ranges and ocean
basins are the real ones. Nothing else changes: the planet stays at its 2 km prototype radius
and float precision. Scale is slice 1.2's problem, and doing shape first means 1.2's gate can be
verified against reality rather than against noise.

## Data source

**AWS Terrain Tiles** (`elevation-tiles-prod`), Terrarium-encoded PNG, XYZ/Web-Mercator.
Chosen over ETOPO 2022 for four reasons: tiled like our quadtree, PNG (Unity decodes natively —
no GeoTIFF/netCDF parser, no Python dependency), includes ocean bathymetry, and needs no account.
NOAA NCEI was returning 503 during this slice's development, which also made it a poor dependency.

- Tile URL: `https://s3.amazonaws.com/elevation-tiles-prod/terrarium/{z}/{x}/{y}.png`
- Decode: `elevation_m = (R * 256 + G + B / 256) - 32768`
- Underlying sources include SRTM, GMTED2010, ETOPO1, NED and others; **attribution required**
  and recorded in `Docs/DATA_SOURCES.md`. Raw tiles and the baked artifact are **not committed**
  (data policy, Phase 0.1).

Validated before adoption — tile `6/58/38`, decoded independently of the game:
wake point 34.5°S 150.4°E → **558 m** (Southern Highlands, ~2 km-averaged; chronicle says ~700 m),
neighbouring pixels 278–558 m (real escarpment relief), Tasman Sea offshore → **−1928 m**.

## Constraints

- Fetch/bake is **editor-side only** (`Assets/EarthGame/Editor/`), batch-callable for CI.
  Runtime code never downloads anything.
- Runtime sampling must be allocation-free per call and fast enough for mesh building
  (`SurfaceRadius` is called ~300 times per chunk, thousands of times per frame while refining).
- `TerrainNoise`'s public API is unchanged — `SurfaceRadius`, `TerrainHeight`, `SurfacePoint`,
  `SurfaceColor` keep their exact signatures. Every existing caller (meshing, spawning, scatter,
  collision, the player's fall-through safety check) must keep working untouched.
- **Graceful degradation:** with no baked dataset present the game must still run on procedural
  noise, logging one clear warning naming the fetch command. A missing 17 MB file must never be
  a black screen.
- Determinism unchanged: same seed + same dataset ⇒ identical terrain.

## Baked artifact format

Offline bake reprojects Web-Mercator tiles to a plate-carrée (equirectangular) grid.

- Path: `Assets/StreamingAssets/EarthElevation.r16` (+ `EarthElevation.json` sidecar)
- Grid: **4096 × 2048**, `int16` little-endian, metres, row 0 = north (+90°), column 0 = −180°
- Size: 16.8 MB. Justification: at the prototype's 2 km radius the whole planet is 50 km² of
  surface, so 8.4 M samples is already far finer than the depth-7 chunk mesh (~1.5 m/vertex).
  Real-world resolution is ~9.8 km/px — coarse in absolute terms, ample at 1/3186 scale.
- Source zoom: **z5** (1024 tiles, 8192 px wide) box-averaged down, so latitude bands that
  Mercator stretches are averaged rather than point-sampled.
- Sidecar records: source URL template, zoom, grid dimensions, bake date, licence note,
  and min/max elevation found (a bake that reports max ≈ 0 is a failed bake).

## Poles

Web Mercator covers ±85.0511° only. Beyond that the bake **clamps to the nearest sampled row**,
so both ice caps become flat plateaus at their ~85° elevation. Documented distortion; acceptable
because the caps are ice sheets of roughly uniform height anyway, and Antarctica's interior is
genuinely a high plateau. Revisited in 1.3 if a pilot region ever goes polar.

## Vertical exaggeration (temporary, 1.1 only)

The planet is 1/3186 real scale, so Everest's 8848 m would stand 2.8 m tall — invisible.
`PlanetConfig.ElevationExaggeration = 60` scales real metres into prototype metres:

- Everest 8848 m → 167 m · Mariana −10 935 m → −206 m · wake point 558 m → 10.5 m

This is an **intentional, explicitly-flagged distortion that slice 1.2 deletes** when the planet
becomes 6 371 km and exaggeration is no longer needed (§47: abstraction is fine, contradiction is
not — this one is loudly labelled, not hidden). `MaxTerrainHeight`/`MinTerrainHeight` widen to
suit, and the ocean sphere stays exactly at sea level so coastlines land where the data says.

## Sub-resolution detail

Real data supplies everything above ~10 km wavelength; below that the dataset is flat and would
look like melted plastic underfoot. The existing noise is **demoted to a detail layer**:
amplitude ≤ 6 prototype metres, added on top of real elevation, suppressed toward zero within
±40 real metres of sea level so it cannot manufacture or erase coastline. Noise never moves a
coastline; it only roughens ground that is already unambiguously land or seabed.

## API

```csharp
// Assets/EarthGame/Scripts/Core/EarthElevation.cs — namespace EarthGame
public static class EarthElevation
{
    public static bool IsLoaded { get; }          // false => callers fall back to noise
    public static string StatusMessage { get; }   // why it is or isn't loaded (for HUD/log)
    public static int Width { get; }
    public static int Height { get; }
    public static float MinElevationM { get; }
    public static float MaxElevationM { get; }

    public static void EnsureLoaded();                             // idempotent, safe from any thread-free context
    public static float SampleMetres(float latDeg, float lonDeg);  // bilinear, wraps longitude
    public static float SampleMetres(Vector3 dir);                 // unit direction -> metres
    public static void DirectionToLatLon(Vector3 dir, out float latDeg, out float lonDeg);
    public static Vector3 LatLonToDirection(float latDeg, float lonDeg);
}
```

**Coordinate convention (binding).** Unity +Y = north pole, +X = (0° N, 0° E), +Z = (0° N, 90° E).
`lat = asin(dir.y)`, `lon = atan2(dir.z, dir.x)`, both in degrees. Every future slice that talks
about a real place uses this convention.

```csharp
// Assets/EarthGame/Editor/ElevationDataTool.cs — namespace EarthGame.Editor
public static class ElevationDataTool
{
    [MenuItem("EarthGame/Data/Fetch Global Elevation")]
    public static void FetchAndBake();       // batch-callable; exits 1 on failure
}
```
Fetch is resumable (skips tiles already cached), caches raw tiles under
`<project>/../EarthGameData/tiles/` — outside the repo per the Phase 0.1 data policy — and
reports progress. A tile that 404s is filled with sea level and counted; more than 1 % missing
tiles fails the bake.

## Validation tests (§36)

New `Assets/EarthGame/Tests/EarthElevationTests.cs`, plus the headless CI mirror. All are skipped
with an explicit inconclusive result — never a silent pass — when the dataset is absent.

1. **Known elevations** within stated tolerance of published values, sampled through the full
   public path (`SampleMetres(lat, lon)`):
   | Place | Lat, Lon | Published | Tolerance |
   |---|---|---|---|
   | Wake point, Southern Highlands | −34.5, 150.4 | ~600 m | ±400 m |
   | Mount Everest region | 27.99, 86.93 | 8848 m | ±3000 m |
   | Dead Sea shore | 31.5, 35.5 | −420 m | ±400 m |
   | Tasman Sea, off Sydney | −34.0, 152.0 | ~−2000 m | ±1500 m |
   | Sahara, central Algeria | 25.0, 5.0 | ~500 m | ±500 m |
   Tolerances are wide on purpose: at ~10 km resolution a single sample is an area average, and
   the test's job is to prove *this is Earth*, not to certify a survey. Everest's ±3000 m still
   fails instantly if the Himalaya are missing, mislocated, or the longitude sign is flipped.
2. **Land/sea classification** at 12 unambiguous points (6 deep ocean, 6 continental interior) —
   every one on the correct side of sea level. This is the coastline-orientation test: any
   transposition, flip, or 180° longitude error fails it.
3. **Coordinate round-trip**: `LatLonToDirection` → `DirectionToLatLon` recovers input within
   1e-3° over a 200-point spread, including both poles and the ±180° seam.
4. **Longitude wrap**: sampling at −180° and +180° agree to within 1 m.
5. **Interpolation sanity**: no NaN/Inf anywhere on a 500-point pseudo-random sweep; every
   sample within `[MinElevationM, MaxElevationM]`.
6. **Fallback**: with the dataset unloaded, `TerrainNoise.SurfaceRadius` still returns finite
   values in the legal radius band (the graceful-degradation guarantee, testable without data).

## Gate contribution (owner-verifiable)

Slice 1.1 contributes to the Phase 1 gate but does not complete it — 1.2–1.4 are still owed.
What must be true at the end of *this* slice:

1. Fly to space and the planet is **recognisably Earth** — Australia, Africa, the Americas
   identifiable without being told, in the owner's own screenshot.
2. Spawn at the wake point (34.5°S 150.4°E) and be on **land**, near the real coast, at a
   plausible relative elevation, with the Tasman Sea in the correct direction (east).
3. The owner picks any coastline and the shape matches a real map at continental scale.
4. Automated: the elevation test suite green, headless and in-editor.
5. No performance regression: chunk meshing stays within the frame budget the prototype had.

## Deliberately deferred

Real scale and precision (1.2) · 30 m DEM detail (1.3) · lat/lon spawn picker and compass HUD
(1.4) · rivers, climate, biomes (Phase 3) · pole reprojection · runtime tile streaming ·
removing vertical exaggeration (1.2).
