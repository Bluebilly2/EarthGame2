# Slice 1.4 — Planet Presentation Rewrite

Phase 1, final slice. Replaces the retrofitted planet rendering layer with one written for a real
Earth from the first line. Binding contract; `Docs/COORDINATES.md` governs every position in it.

## Why

The planet layer was written for a 2 km float world and migrated to a 6,371 km double world.
Seven bugs followed, none of them logic errors — all leftover assumptions from the old model, and
every one found by the owner in play rather than by a test. The architecture is a retrofit and it
keeps leaking.

The valuable work is elsewhere and survives untouched: `EarthGame.Sim` (585 lines, engine-free,
39 passing tests), the bolt-factory simulation (1,883), the test suites (1,265), the elevation
pipeline, the recorder and CI. This slice replaces ~2,765 lines of presentation code.

**The point is not to fix seven bugs. It is to make that class of bug impossible**, via rule 4 of
COORDINATES.md and the scale-invariance suite below.

## Scope

**Rewritten:** `PlanetGenerator`, `PlanetChunk`, `ChunkMeshBuilder`, `ChunkOceanBuilder`,
`WorldOrigin`, `TerrainNoise`, `TerrainSynthesis`, `AtmosphereController`,
`FirstPersonPlanetController`, `PlayerSpawner`.

**Untouched:** everything in `Assets/EarthGame/Sim/`, `Assets/EarthGame/Tests/`, the Workshop,
`ElevationDataTool`, `SessionRecorder`, `CIBuild`, `WorldProbe`, all shaders except where a
uniform contract changes.

**Behaviour that must survive exactly:** wake at 34.5°S 150.4°E at 8 a.m.; WASD/Space/F/Shift/
Ctrl/R/Esc/B controls; the workshop; real elevation driving terrain; the terrain classifier and
its synthesis; procedural fallback when no dataset is present; recording.

## New coordinate types (`EarthGame.Sim`, engine-free)

```csharp
public enum FrameId { EarthFixed = 0 }   // one frame today; the graph arrives with orbits

/// <summary>A place: a frame plus an offset within it. A bare Double3 is never a place.</summary>
public readonly struct WorldPoint
{
    public readonly FrameId Frame;
    public readonly Double3 Position;          // metres from the frame's own origin

    public WorldPoint(FrameId frame, Double3 position);
    public double Radius { get; }              // distance from the frame origin
    public Double3 Up { get; }                 // radial direction; (0,1,0) at the centre
    public double AltitudeAbove(double seaLevelRadius);
    public static Double3 Displacement(WorldPoint from, WorldPoint to);   // throws on frame mismatch
    public static double Distance(WorldPoint a, WorldPoint b);
    public WorldPoint Offset(Double3 delta);
    public void ToLatLon(out double latDeg, out double lonDeg);
    public static WorldPoint FromLatLonAltitude(FrameId frame, double lat, double lon,
                                                double radiusAtSurface, double altitude);
}
```

Mixing frames is a hard error, not a silent wrong answer.

## Scale-derived constants (`EarthGame.Sim`)

Rule 4 made executable. Nothing in the render layer may hold a distance literal.

```csharp
public sealed class PlanetScale
{
    public PlanetScale(double radiusM, double surfaceGravity);
    public double RadiusM { get; }
    public double SurfaceGravity { get; }

    public double RootChunkEdgeM { get; }                 // pi * R / 2
    public double ChunkEdgeAtDepth(int depth);
    public double VertexSpacingAtDepth(int depth, int vertsPerEdge);
    public int DepthForVertexSpacing(double metres, int vertsPerEdge);

    public double HorizonDistance(double altitude);       // sqrt(2*R*h + h*h)
    public double AtmosphereHeightM { get; }              // R / 63.71  (about 100 km on Earth)
    public double FogDensityAtSeaLevel { get; }           // ~40 km visibility, derived
    public double RebaseThresholdM { get; }               // R / 3185   (about 2 km on Earth)
    public double NearFarCameraSplitM { get; }            // derived from horizon at altitude
    public int MinDepthForWater { get; }                  // where sea-level chord sag < 10 m
    public double ColliderRangeM { get; }
    public int ColliderMinDepth { get; }
    public double FlyMaxSpeed { get; }                    // circumference / 1600 s
}
```

Every consumer takes a `PlanetScale`. Changing the radius changes every threshold coherently,
which is exactly what did not happen last time.

## Rendering rules

- **One conversion boundary.** `OriginShift` (Unity side) is the only component that turns a
  `WorldPoint` into a Unity `Vector3`. It owns rebasing and publishes `_EG_PlanetCenter`.
- **Chunk-local meshes**, holder placed by `OriginShift`, chunk positions recomputed from stored
  `WorldPoint`s on rebase (never accumulated).
- **Camera ownership is explicit.** A single `PlanetCameraRig` owns both cameras, their clip
  planes and **their clear flags**. No other component may set `clearFlags`, and the rig asserts
  in `LateUpdate` that nothing has. This is a direct guard against the bug where the near camera
  erased the far camera every frame.
- **Ocean inside the terrain LOD**, tessellation and inclusion derived from
  `PlanetScale.MinDepthForWater`, so chord sag can never produce a second globe.
- **Atmosphere shell** positioned from the planet's `WorldPoint` every frame, never from a
  cached transform.

## Validation

### Scale-invariance suite (the reason for this slice)

Engine-free, in `EarthGame.Sim.Tests`. Runs the derived-constant and geometry stack at radii of
**2 km, 6,371 km and 60,000 km** and asserts the same qualitative results at each:

1. Horizon distance from 2 m eye height is between 0.001·R and 0.01·R at every radius.
2. `DepthForVertexSpacing(1 m)` yields sub-2 m vertices at every radius, and `MaxDepth` is
   sufficient for it.
3. Fog visibility is between 5 % and 30 % of horizon distance at every radius — the test that
   would have caught fog tuned for a 2 km planet.
4. Sea-level chord sag at `MinDepthForWater` is under 10 m at every radius — the test that would
   have caught the second Earth.
5. Collider chunks are finer than 5 m of vertex spacing at every radius — would have caught
   `ColliderMinDepth = 6`.
6. Fly speed crosses the circumference in 20–40 minutes at every radius, **after** the boost
   multiplier — would have caught both speed bugs.
7. The near/far camera split exceeds the horizon distance at 1 % of R altitude — would have
   caught the 12 km cut-off.
8. Rebase threshold keeps Unity coordinates under 1e4 m at every radius, so float precision is
   always better than 1 mm.

### Coordinate correctness
9. `WorldPoint` round-trips through lat/lon/altitude within 1 mm at all three radii.
10. Mixing frames throws.
11. A rebase leaves every world position unchanged to within 1 µm.
12. No render-layer source file contains a bare distance literal (a source scan, allowlisted for
    genuinely dimensionless numbers).

### Regression tests for all seven bugs
13. Fall-through recovery lands within 5 m of the surface, at any radius and any origin offset.
14. `PlanetCameraRig` detects and fails on any external `clearFlags` change.
15. Terrain sampled through a rebase is identical either side of it.
16. HUD-facing lat/lon derives from `WorldPoint`, verified at 6 real coordinates.

## Gate

1. Wake at the wake point in morning light; walk 1 km and the coordinates match the real geodesic.
2. Fly to 400 km: **one** Earth, recognisable, with atmosphere rim, at a controllable speed.
3. Descend and land without being thrown anywhere.
4. Whole suite green headless, including scale-invariance at all three radii.
5. The owner completes the flight in item 2 without encountering anything they must describe in
   words — and if they do, the recording shows it.

## Deliberately deferred

Real frames beyond `EarthFixed`, Earth rotation and ECI (Phase 5–6) · atmospheric scattering ·
terrain materials and physics surfaces · the 7-second startup hitch (tracked, not this slice).
