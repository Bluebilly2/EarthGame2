# Slice 1.2 — True Scale

Phase 1 (*Real Earth, Really There*), slice 2 of 4. See `Docs/ROADMAP.md` for the phase gate.

**Goal.** The planet becomes 6,371 km with 9.81 m/s² gravity and **no vertical exaggeration** —
Everest is 8,848 m above sea level because that is how tall it is. This is the precision
migration: float32 dies about 10 km from the origin (ULP at 6.37e6 m is ~0.5 m), so world
positions move to double precision and rendering becomes camera-relative via a floating origin.

Done now, before any content exists, because every later slice pins things to real coordinates.
Doing it after Phase 2 would invalidate every survival site, prospect and factory placement.

## What changes and what must not

**Changes:** planet radius, gravity, exaggeration (deleted), quadtree depth, chunk mesh vertex
space, world-position representation, ocean construction, camera depth handling.

**Must not change:** the public shape of `TerrainNoise` (`SurfaceRadius`, `TerrainHeight`,
`SurfacePoint`, `SurfaceColor` keep their names and roles), the wake point, the elevation dataset
or its sampling, the controls, the Workshop, or the fallback-to-procedural guarantee.

## The precision model (binding)

```
worldPosition (Double3, metres, ECEF)  =  WorldOrigin  +  unityPosition (Vector3)
```

- **`WorldOrigin`** is the ECEF point that Unity's `(0,0,0)` currently represents. It moves.
- The **planet centre is never a Unity position** — at real scale it is unrepresentable near the
  player. Anything needing "up" uses `normalize(worldPosition)` computed in double.
- **Rebase rule:** when the player's Unity position exceeds `RebaseThreshold = 2000 m`,
  `WorldOrigin += player.unityPosition` and every registered root transform is shifted by the
  negation. Rigidbody velocities are untouched; only positions move.
- Rebasing happens in `LateUpdate` after physics has settled, at most once per frame.
- **Chunk meshes are chunk-local:** vertex positions are relative to the chunk's own centre, so
  their magnitudes never exceed the chunk's edge length. The chunk GameObject carries the offset.
  This is what keeps a 0.6 m vertex spacing meaningful on a 6,371 km sphere.

### Precision budget (§37)
Double has 15–16 significant digits, so at 6.4e6 m the representable step is ~1e-9 m. Float chunk
vertices are bounded by chunk edge (≤ 9.5 m at max depth), where ULP is ~1e-6 m. Camera-relative
positions stay under 2 km, ULP ~1e-4 m. **Target: no visible jitter at eye height anywhere on
Earth, including the antipode of wherever the session started.**

## Constants (`PlanetConfig`)

| Constant | Was | Becomes |
|---|---|---|
| `Radius` | 2,000 m | **6,371,000 m** |
| `Gravity` | 12 m/s² | **9.81 m/s²** |
| `MaxTerrainHeight` | 220 m | **9,000 m** |
| `MinTerrainHeight` | −280 m | **−11,000 m** |
| `ElevationExaggeration` | 60 | **deleted** — `ElevationScale` becomes 1 |
| `AtmosphereHeight` | 1,200 m | **100,000 m** |
| `TreeLineHeight` | 90 m | **2,000 m** (real tree line, temperate) |
| `DayLengthSeconds` | 480 | **1,200** (20 min — a real 24 h day is unplayable, and this is a stated abstraction, not a hidden one) |

`PlanetGenerator.MaxDepth`: **7 → 20**. Root chunk edge is πR/2 ≈ 10,008 km; at depth 20 that is
9.5 m per chunk, ≈ 0.6 m per vertex. Split distance stays `edgeLength * 1.6`, so the tree only
deepens near the camera and node count stays bounded.

## New types

```csharp
// Assets/EarthGame/Sim/World/Double3.cs — namespace EarthGame.Sim (engine-free)
public readonly struct Double3
{
    public readonly double X, Y, Z;
    public Double3(double x, double y, double z);
    public double Length { get; }
    public double SqrLength { get; }
    public Double3 Normalized { get; }          // zero-safe: returns (0,1,0) for a zero vector
    public static Double3 Zero { get; }
    public static Double3 operator +(Double3 a, Double3 b);
    public static Double3 operator -(Double3 a, Double3 b);
    public static Double3 operator *(Double3 a, double s);
    public static double Dot(Double3 a, Double3 b);
    public static Double3 Cross(Double3 a, Double3 b);
    /// <summary>Great-circle surface distance in metres between two directions on a sphere.</summary>
    public static double GeodesicDistance(Double3 dirA, Double3 dirB, double radius);
}
```

```csharp
// Assets/EarthGame/Scripts/Core/WorldOrigin.cs — namespace EarthGame
public static class WorldOrigin
{
    public static Double3 Current { get; }
    public static event System.Action<Vector3> OnRebased;   // argument = the shift applied
    public static Double3 ToWorld(Vector3 unityPos);
    public static Vector3 ToUnity(Double3 worldPos);        // clamped-safe; far points lose precision by design
    public static void Register(Transform root);            // shifted on every rebase
    public static void Unregister(Transform root);
    public static void RebaseTo(Double3 newOrigin);         // shifts registered roots
    public static void MaybeRebase(Vector3 playerUnityPos); // rebases past RebaseThreshold
    public static void ResetForTests();
}
```

`TerrainNoise` gains double-precision entry points; the float ones remain and delegate, so no
existing caller breaks:

```csharp
public static double SurfaceRadiusD(Double3 dir);   // metres from planet centre
public static double TerrainHeightD(Double3 dir);   // metres above sea level
```

`EarthElevation` gains `SampleMetres(Double3 dir)`.

## Ocean

A 6,371 km sphere cannot be one mesh, and a camera-following patch cannot also be a globe seen
from space. **The ocean joins the quadtree**: every chunk whose area reaches below sea level also
builds a sea-surface mesh at exactly `Radius`, chunk-local like the terrain, with the same LOD and
the same lifetime. `OceanSphere` is deleted; the water material moves to `PlanetGenerator`.

Shaders can no longer assume the planet centre is the world origin. `AtmosphereController`
publishes `Shader.SetGlobalVector("_EG_PlanetCenter", WorldOrigin.ToUnity(Double3.Zero))` every
frame, and `Water`/`AtmosphereRim`/`PlanetSkybox` use `normalize(worldPos - _EG_PlanetCenter)`
instead of `normalize(worldPos)`. The centre is ~6.4e6 units away, where float ULP is ~0.5 m —
irrelevant for a direction taken over a 6,371 km baseline.

## Cameras

Real scale needs a far plane around 3e7 m to see the planet from space; with a 0.08 m near plane
that depth range shreds the depth buffer. **Two cameras**, both children of the player rig,
sharing position and rotation:

| Camera | Depth | Clear | Near | Far |
|---|---|---|---|---|
| `FarCamera` | 0 | Skybox | 12,000 | 40,000,000 |
| `PlayerCamera` | 1 | Depth only | 0.08 | 12,000 |

The split is exact (near camera's far == far camera's near), so nothing renders twice.
`AudioListener` and `AtmosphereController` stay on `PlayerCamera`, which remains the `MainCamera`.

## Sub-resolution detail

The dataset resolves ~10 km, so between samples the ground would be glass. Detail noise now works
in **real metres**: fractal octaves from ~2 km wavelength (≈40 m amplitude) down to ~30 m
wavelength (≈1 m amplitude), amplitude scaled by the local data-derived slope so plains stay flat
and mountains stay rough. Still suppressed within ±40 m of sea level so it cannot invent or erase
coastline. Real 30 m terrain arrives in slice 1.3; this is the stopgap that makes ground walkable.

## Validation tests (§36)

Engine-free additions to `Assets/EarthGame/Tests/` (headless in CI):

1. **Double3 algebra** — normalisation, dot/cross identities, zero-safety.
2. **Geodesic distance** — quarter-circumference between orthogonal directions equals
   `πR/2` within 1 m; equator-to-pole equals 10,007,543 m within 10 m.
3. **Known real distances** within 0.5 %: Sydney→Melbourne 713 km, London→New York 5,570 km,
   wake point→Sydney ~110 km. These fail if the radius, the coordinate convention, or the
   geodesic maths is wrong.
4. **Precision floor** — at 6.4e6 m, adding 1 mm to a double changes it (float would not).
5. **Origin round-trip** — `ToUnity(ToWorld(v)) == v` to 1e-4 m across rebases; a rebase leaves
   every registered transform's *world* position unchanged.
6. **Elevation is unexaggerated** — `TerrainHeightD` at the Everest sample equals the dataset's
   metres within 1 m (proves exaggeration is gone, not merely reduced).

Unity-side behaviour (jitter, LOD, frame budget) is verified by the owner playtest, per the gate.

## Gate contribution (owner-verifiable)

1. Stand at the wake point: HUD reads ≈34.50S 150.39E and an altitude near **687 m** — real
   metres, not exaggerated ones.
2. **Walk 1,000 m on the HUD and it is 1,000 real metres**: latitude/longitude change matches the
   real geodesic (≈0.009° of latitude due north).
3. **No jitter** at eye height, including far from where the session started.
4. Fly up: the transition to space still works and the planet is still recognisably Earth.
5. Hills have real gradients — the coast road up an escarpment is a walk, not a wall.
6. Automated: the whole validation suite green, headless and in-editor.

## Deliberately deferred

30 m DEM detail and streaming (1.3) · lat/lon spawn picker and compass (1.4) · axial tilt and
seasons (3.5) · atmospheric scattering worthy of real scale · terrain physics materials ·
performance tuning beyond "no regression on owner hardware".
