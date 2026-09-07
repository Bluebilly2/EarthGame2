# Earth Game — Prototype Architecture Specification

> Scope note: this document specifies the current first-person planet-walker PROTOTYPE only.
> The project's governing design brief is `Docs/GAME_DESIGN.md` — real Earth, causal-fidelity
> simulation, vertical-slice development. This prototype is the presentation-layer walking
> skeleton (first-person embodiment on a spherical world, ground-to-space transition); its
> small procedural planet is a placeholder that GAME_DESIGN.md Section 2 eventually replaces.

A first-person planet exploration game in Unity. You spawn standing on a small Earth-like planet.
You can run around the entire planet (gravity always points to the planet core), jump, and toggle
fly mode to ascend through the atmosphere into space, where you see the whole planet below you.
Style target: Outer Wilds — a scaled-down planet so ground-to-space is one seamless experience.

## Hard constraints (every file MUST follow these)

- **Unity 6 (6000.0 LTS), Built-in Render Pipeline.** No URP/HDRP APIs, no packages beyond default modules.
- **Legacy input** (`UnityEngine.Input`): `Input.GetAxis("Mouse X"/"Mouse Y"/"Horizontal"/"Vertical")`, `Input.GetKey(Down)(KeyCode...)`. NOT the new Input System.
- **No scene assets.** Everything is created at runtime by `GameBootstrap` via `[RuntimeInitializeOnLoadMethod]`. Scripts must not assume any object exists unless this spec says GameBootstrap creates it.
- All C# in namespace `EarthGame`. One public top-level class per file, filename = class name.
- No `async/await`, no Jobs/Burst, no unsafe. Coroutines and per-frame budgets for spreading work.
- Shaders: Built-in RP, `CGPROGRAM` style. Referenced by `Shader.Find("EarthGame/<Name>")` — the shader name line must be `Shader "EarthGame/<Name>"`.
- Determinism: all terrain/scatter randomness must derive from `PlanetConfig.Seed` via hashing, never `UnityEngine.Random` (except cosmetic-only cases explicitly noted).
- Scripts must compile with zero warnings-as-errors issues; no unused `using`s beyond `UnityEngine`/`System.Collections.Generic` where needed.
- Planet center is the world origin `(0,0,0)` and never moves. Planet does not rotate; the sun orbits instead.
- Units: meters, seconds. Max play altitude ~30 km → float precision is fine, no floating origin.

## File layout (write files exactly here)

```
Assets/EarthGame/Scripts/Core/PlanetConfig.cs
Assets/EarthGame/Scripts/Core/Noise3D.cs
Assets/EarthGame/Scripts/Core/TerrainNoise.cs
Assets/EarthGame/Scripts/Core/GameBootstrap.cs
Assets/EarthGame/Scripts/Planet/PlanetGenerator.cs
Assets/EarthGame/Scripts/Planet/PlanetChunk.cs
Assets/EarthGame/Scripts/Planet/ChunkMeshBuilder.cs
Assets/EarthGame/Scripts/Planet/DetailScatter.cs
Assets/EarthGame/Scripts/Player/FirstPersonPlanetController.cs
Assets/EarthGame/Scripts/Player/PlayerSpawner.cs
Assets/EarthGame/Scripts/Environment/SunController.cs
Assets/EarthGame/Scripts/Environment/AtmosphereController.cs
Assets/EarthGame/Scripts/Environment/OceanSphere.cs
Assets/EarthGame/Scripts/UI/HUDController.cs
Assets/EarthGame/Shaders/TerrainVertexColor.shader
Assets/EarthGame/Shaders/Water.shader
Assets/EarthGame/Shaders/AtmosphereRim.shader
Assets/EarthGame/Shaders/PlanetSkybox.shader
Assets/EarthGame/Editor/SceneBuilder.cs
Packages/manifest.json
ProjectSettings/ProjectVersion.txt
```

Do NOT write `.meta` files — Unity generates them.

## World parameters (single source of truth: `PlanetConfig`)

`PlanetConfig` is a **static class** of `const`/`static readonly` fields:

```csharp
namespace EarthGame {
  public static class PlanetConfig {
    public const int Seed = 1347;
    public const float Radius = 2000f;          // sea-level radius, meters
    public const float MaxTerrainHeight = 180f; // peaks above sea level
    public const float MinTerrainHeight = -80f; // ocean floor below sea level
    public const float AtmosphereHeight = 1200f; // fades to space over this altitude
    public const float Gravity = 12f;            // m/s^2, snappier than 9.81 for game feel
    public const float SeaLevelRadius = Radius; // convenience alias
    public const float TreeLineHeight = 90f;    // no trees above this altitude
    public const float DayLengthSeconds = 480f; // full day/night cycle
    // Colors (static readonly Color): DeepWater, ShallowWater, Sand, Grass, Forest, Rock, Snow,
    // SkyDay, SkyHorizon, SkyNight, SunColor, FogColor
  }
}
```

Choose pleasing low-poly-Earth colors (blue oceans, sandy shores, green lowlands, grey-brown rock,
white snowcaps). Exact values are the Core agent's choice; everyone else references them by name.

## Core

### `Noise3D` (static class)
Self-contained 3D simplex-style noise, seeded, no UnityEngine dependency beyond `Mathf`/`Vector3`.
```csharp
public static float Sample(Vector3 p);                       // single octave, range [-1,1]
public static float Fbm(Vector3 p, int octaves, float lacunarity, float gain); // range ~[-1,1]
public static float Ridged(Vector3 p, int octaves, float lacunarity, float gain); // range [0,1]
public static uint Hash(int x, int y, int z);                // deterministic uint hash
public static float Hash01(int x, int y, int z);             // [0,1)
```
Implementation: gradient noise on an integer lattice (classic Perlin-style with smoothstep fade is
acceptable) — must be continuous, deterministic from `PlanetConfig.Seed`, and artifact-free enough
for terrain. Include the permutation/seeding internally.

### `TerrainNoise` (static class)
The planet's height field and coloring. **This is the shared contract between terrain meshing,
spawning, scatter, and collision — everyone calls these:**
```csharp
// dir: normalized direction from planet center.
// Returns surface radius (distance from center) at that direction. Continuous everywhere.
public static float SurfaceRadius(Vector3 dir);
// Altitude of terrain above sea level at dir (SurfaceRadius - PlanetConfig.Radius).
public static float TerrainHeight(Vector3 dir);
// World-space surface point for a direction.
public static Vector3 SurfacePoint(Vector3 dir);
// Vertex color for terrain: height = altitude above sea level, slope01 = 0 flat .. 1 cliff.
public static Color SurfaceColor(float height, float slope01, Vector3 dir);
```
Composition: continents from low-frequency FBM (so there ARE large oceans and large landmasses),
mountains from ridged noise masked to continent interiors, small detail octaves. Ocean floor drops
to `MinTerrainHeight`. `SurfaceColor` bands: deep/shallow water tint under sea level (used on the
terrain under the ocean), sand near 0..4 m, grass, forest tint, rock on steep slopes (slope01 > ~0.55)
and above ~110 m, snow above ~140 m blending by height and `dir.y` (poles: |dir.y| > 0.8 lowers snow line).

### `GameBootstrap` (static class)
```csharp
[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
static void Boot();
```
Order of operations in `Boot()`:
1. If an object named `"EarthGameRoot"` already exists, return (idempotent under domain-reload-off).
2. Destroy any pre-existing `Camera` or `Light` objects in the scene (the default untitled scene has them).
3. Create root `GameObject "EarthGameRoot"`.
4. Create `"Planet"` child at origin with `PlanetGenerator`.
5. Create `"Ocean"` child at origin with `OceanSphere`.
6. Create `"Sun"` child with `Light` (directional) + `SunController`.
7. Create `"Player"` via `PlayerSpawner.Spawn()` (returns the player GameObject; camera child
   `"PlayerCamera"` tagged `MainCamera`, includes `AudioListener`).
8. Add `AtmosphereController` to the camera GameObject.
9. Create `"HUD"` child with `HUDController`.
10. Set `Physics.gravity = Vector3.zero` (gravity is custom per-body), `Application.targetFrameRate = -1`,
    `QualitySettings.vSyncCount = 1`, `Physics.defaultSolverIterations` default ok.

`GameBootstrap` is the ONLY place objects are created at startup and the ONLY class that wires
cross-references. Components must find each other via the static singletons below, all set in `Awake()`:
- `PlanetGenerator.Instance`, `OceanSphere.Instance`, `SunController.Instance`,
  `FirstPersonPlanetController.Instance`, `AtmosphereController.Instance`.
  Each: `public static <T> Instance { get; private set; }` assigned in `Awake`, cleared in `OnDestroy`.
  Consumers must null-check (`if (PlanetGenerator.Instance == null) return;`) — startup order inside
  one frame is not guaranteed beyond GameBootstrap's creation order above.

## Planet (chunked LOD cube-sphere)

### `PlanetGenerator : MonoBehaviour`
Owns 6 root `PlanetChunk` quadtrees (one per cube face), materials, and the per-frame update loop.
```csharp
public static PlanetGenerator Instance { get; private set; }
public Material TerrainMaterial { get; }   // Shader.Find("EarthGame/TerrainVertexColor")
public Transform ChunkParent { get; }      // parent transform for all chunk objects
public static readonly Vector3[] FaceNormals; // 6 cube face normals
// Called by chunks: which world position drives LOD? (player camera, falls back to Camera.main)
public Vector3 GetLodFocusPosition();
```
Behavior:
- `Awake`: set Instance, create material.
- `Start`: create 6 root chunks (depth 0), then call a full initial refine synchronously **around
  the spawn point** so the ground exists before the player lands: keep refining chunks containing
  the spawn direction until max depth, building meshes immediately.
- `Update`: walk all quadtrees; split leaves closer than `SplitDistance(depth)`, merge parents whose
  children are all farther than `MergeDistance(depth)`; obey a mesh-build budget of at most
  **4 chunk meshes per frame** (unbuilt chunks queue). Never leave holes: a parent's renderer stays
  enabled until all 4 children have built meshes; children disabled until then.
- LOD scheme: `MaxDepth = 7`. Root chunk edge ≈ πR/2 ≈ 3140 m. `SplitDistance(depth) = chunkEdgeLength * 1.6f`,
  measured from `GetLodFocusPosition()` to chunk's closest surface point approximation (use chunk
  center direction * SurfaceRadius). Hysteresis: `MergeDistance = SplitDistance * 1.25f`.
- Colliders: only leaf chunks at `depth >= 6` within 300 m of the focus get a `MeshCollider`
  (shared mesh = render mesh). Add/remove as chunks build/destroy; at most 1 collider assignment
  per frame (they're expensive to cook).
- Chunk GameObjects: layer default, `MeshRenderer.shadowCastingMode = On` for depth >= 5, Off for
  coarser (planet-scale shadow acne prevention), `receiveShadows = true`.

### `PlanetChunk` (plain class, NOT MonoBehaviour)
Quadtree node. Fields: face normal, depth, uv rect on the face (Vector2 uvMin/uvMax in [-1,1] cube
face coordinates), children array, built `GameObject`/`Mesh` refs, state enum.
Public API used by PlanetGenerator:
```csharp
public PlanetChunk(Vector3 faceNormal, Vector2 uvMin, Vector2 uvMax, int depth, PlanetChunk parent);
public void UpdateLod(Vector3 focusPos, System.Action<PlanetChunk> requestMeshBuild); // recursive
public void SetMesh(Mesh mesh); // called when its queued build completes; creates the GameObject
public void DestroySelfAndChildren();
public Vector3 CenterDirection { get; }  // normalized dir of chunk center
public float EdgeLength { get; }          // world-space edge estimate for its depth
public bool IsLeaf { get; }
public int Depth { get; }
public GameObject Holder { get; }
```
Chunk → world mapping: point on cube face = `faceNormal + u*tangentA + v*tangentB` (u,v in the
chunk's uv rect), normalized to unit sphere (use the "spherified cube" mapping:
`s = cube.normalized` is acceptable), then `* TerrainNoise.SurfaceRadius(s)`.
Events for scatter: `public static event System.Action<PlanetChunk> OnLeafReady;` fired when a
leaf at `MaxDepth` gets its mesh, and `OnLeafDestroyed` fired before its GameObject is destroyed.
`DetailScatter` subscribes to these.

### `ChunkMeshBuilder` (static class)
```csharp
public const int VertsPerEdge = 17; // 17x17 grid => 16x16 quads
public static Mesh Build(Vector3 faceNormal, Vector2 uvMin, Vector2 uvMax, int depth);
```
- Grid of `VertsPerEdge²` vertices mapped as above; positions are world-space (planet center origin)
  — chunk GameObjects all sit at identity transform, so vertex positions ARE world positions.
- Normals: computed from the height field via central differences on the sphere (sample
  `SurfaceRadius` at small angular offsets), NOT `RecalculateNormals` (avoids seams between chunks).
- Colors: `TerrainNoise.SurfaceColor(height, slope01, dir)`; slope01 from the normal vs. up:
  `slope01 = 1 - saturate(dot(normal, dir))` remapped by `slope01 = Mathf.InverseLerp(0.02f, 0.35f, rawSlope)` clamped.
- **Skirts**: one extra ring of vertices around the grid, pushed toward planet center by
  `8m * (MaxDepth - depth + 1)` to hide LOD cracks between neighboring depths. Skirt verts reuse the
  edge vertex's normal/color.
- `mesh.RecalculateBounds()`, `mesh.indexFormat = UInt16` (17² + skirt fits), return mesh.

### `DetailScatter : MonoBehaviour` (added to the Planet object by PlanetGenerator.Awake)
Subscribes to `PlanetChunk.OnLeafReady/OnLeafDestroyed`. For each max-depth leaf on land:
- Deterministic per-chunk: `Noise3D.Hash01` on quantized chunk center → place ~0–25 trees and ~0–8
  rocks at random surface points inside the chunk (rejection-sample: skip underwater, skip
  `height > TreeLineHeight`, skip slope01 > 0.5).
- Trees: ONE combined `Mesh` per chunk (build cones+cylinder trunks into one vertex-colored mesh,
  same terrain material) on a child GameObject. Pines: brown trunk cylinder (5 sides), 2–3 stacked
  dark-green cones (6 sides). Height 4–8 m. Rocks: small displaced icosahedra, grey, in the same
  combined mesh. No colliders on scatter.
- On `OnLeafDestroyed`: destroy that chunk's scatter GameObject and Destroy() its mesh.

## Player

### `FirstPersonPlanetController : MonoBehaviour`
Rigidbody capsule FPS controller with radial gravity and fly mode. Created by `PlayerSpawner`.
```csharp
public static FirstPersonPlanetController Instance { get; private set; }
public bool IsFlying { get; private set; }
public float CurrentSpeed { get; }        // world speed, m/s (for HUD)
public float Altitude { get; }            // above sea level = position.magnitude - PlanetConfig.Radius
public Camera PlayerCamera { get; set; }  // assigned by PlayerSpawner
```
Setup expectations (PlayerSpawner does this): `Rigidbody` (mass 70, `useGravity = false`,
`interpolation = Interpolate`, `constraints = FreezeRotation`, `collisionDetectionMode = Continuous`),
`CapsuleCollider` (height 1.8, radius 0.35, center y 0), a `PhysicsMaterial`with zero friction
(use the `PhysicMaterial` class name valid in Unity 6: it is `PhysicsMaterial` in 6000.0 — write
`PhysicsMaterial` and the Editor agent must confirm; if compile fails the fixer changes it).
Camera child at local (0, 0.72, 0).

Behavior (all in `FixedUpdate` for physics, mouse look in `Update`):
- **Up vector**: `up = transform.position.normalized`. Body rotation continuously slerped so its
  local +Y matches `up` (rate ~10/s on ground, ~2.5/s flying for a floatier feel), yaw preserved.
- **Mouse look**: yaw rotates body around `up`; pitch rotates camera locally, clamped ±89°.
  Sensitivity 2.2. `Cursor.lockState = Locked` on start; Escape unlocks, click re-locks.
- **Grounded check**: `SphereCast` from capsule center toward `-up`, radius 0.3, distance 1.05.
- **Walk mode**: WASD accelerates along the tangent plane (project camera-forward onto plane ⊥ up).
  Walk 6 m/s, sprint (LeftShift) 11 m/s. Ground acceleration 60 m/s², air control 8 m/s².
  Space jumps (impulse for ~2.2 m apex). Custom gravity: `rb.AddForce(-up * PlanetConfig.Gravity, ForceMode.Acceleration)`.
  Velocity damping on ground for snappy stops (counter tangential velocity when no input).
- **Fly mode**: toggle with **F**. Flying: no gravity applied; full 6-DOF — camera-forward thrust
  with WASD, Space up / LeftCtrl down along `up`, LeftShift boost ×4.
  Speed scales with altitude: `flySpeed = Mathf.Lerp(18, 600, Mathf.InverseLerp(0, 25000, Altitude))`
  (so leaving the atmosphere is quick but ground flying is controllable). Velocity eased toward
  target (lerp factor ~4/s) for a smooth glide feel. Entering fly gives slight upward pop; exiting
  restores gravity.
- **Safety**: if `position.magnitude < TerrainNoise.SurfaceRadius(position.normalized) - 5` (fell
  through terrain), teleport to `SurfacePoint + up*2`, zero velocity.
- Also: R key = respawn at original spawn point. 

### `PlayerSpawner` (static class)
```csharp
public static GameObject Spawn(); // builds the player + camera rig, returns root
```
- Spawn direction search: start from `dir0 = new Vector3(1, 0.15f, 0.2f).normalized`; walk a spiral of
  test directions until `TerrainNoise.TerrainHeight(dir) > 8f` (dry land, above beach), use first hit.
- Spawn at `TerrainNoise.SurfacePoint(dir) + dir * 1.5f`.
- Camera: FOV 75, near 0.08, far 60000. Add `AudioListener`. Tag `MainCamera`.
- Stores the spawn point in a public static field for respawn.

## Environment

### `SunController : MonoBehaviour` (on the "Sun" object with a directional Light)
```csharp
public static SunController Instance { get; private set; }
public Vector3 SunDirection { get; }  // normalized, from scene toward the sun (i.e. -light.forward)
public float DayPhase { get; }        // 0..1
```
- Light: directional, intensity 1.15, color `PlanetConfig.SunColor`, shadows Soft,
  `shadowStrength 0.85`, reasonable shadow distance (QualitySettings.shadowDistance = 400).
- Rotates around world X+Z diagonal axis over `DayLengthSeconds`, starting at mid-morning phase so
  the spawn point is lit (start rotation chosen so the sun shines roughly along `-dir0` of the
  spawn direction: just start with light.forward ≈ (-1, -0.3, -0.2).normalized and orbit from there).
- Sets `RenderSettings.sun = light`, `RenderSettings.ambientMode = Trilight` and updates ambient
  sky/equator/ground colors through the day (dim blue at night).

### `AtmosphereController : MonoBehaviour` (on the player camera)
Owns the visual ground↔space transition. In `LateUpdate`:
- `float t = Mathf.InverseLerp(0, PlanetConfig.AtmosphereHeight, altitude)` (0 ground, 1 space).
- Skybox: create material from `Shader.Find("EarthGame/PlanetSkybox")` in Awake, assign to
  `RenderSettings.skybox`. Update uniforms: `_SunDir` (from SunController), `_SpaceBlend` = t,
  plus day/night from `SunController.DayPhase`.
- Fog: `RenderSettings.fog = true`, ExponentialSquared; density lerps `8e-4 → 0` as t→1; color =
  horizon color blended day/night.
- Creates the **atmosphere rim shell**: in `Start`, a sphere mesh (can use
  `GameObject.CreatePrimitive(PrimitiveType.Sphere)` scaled to `(Radius + AtmosphereHeight*0.85f)*2` with
  its `SphereCollider` destroyed) at origin, material `Shader.Find("EarthGame/AtmosphereRim")`,
  render queue Transparent+10. Visible only meaningfully from outside (fresnel additive glow).
- Camera far plane already 60000; set `camera.backgroundColor` fallback black, clear flags Skybox.

### `OceanSphere : MonoBehaviour`
- Builds a cube-sphere mesh (6 faces × 65×65 verts (`indexFormat = UInt32` for safety), radius
  `PlanetConfig.Radius`) as ONE mesh on its own GameObject at origin, material
  `Shader.Find("EarthGame/Water")`.
- No collider. Water look lives in the shader (moving normal-ish wobble, fresnel, depth-ish tint by
  facing). Provide `public static OceanSphere Instance`.

## UI

### `HUDController : MonoBehaviour` — immediate mode `OnGUI`, no UGUI/TMP.
- Top-left box: `ALT 1,234 m   SPD 12 m/s   [WALKING|FLYING]  TIME 09:40`.
  (time of day derived from `SunController.DayPhase`).
- Bottom-left controls hint, fades out (alpha to 0) after 20 s, reappears for 5 s on mode toggle:
  `WASD move · Mouse look · Space jump · F fly · Shift sprint/boost · Ctrl descend · R respawn · Esc cursor`.
- Small center-dot crosshair (4 px, semi-transparent white, only when cursor locked).
- Style: `GUI.Box`-free — draw with `GUI.Label` + one translucent black `GUIStyle` background via
  `Texture2D` (1×1, created in code). Monospace-ish via `GUI.skin.label` with fontSize 14.

## Shaders (Built-in RP)

### `TerrainVertexColor.shader` — `Shader "EarthGame/TerrainVertexColor"`
Surface shader: `#pragma surface surf Standard vertex:vert fullforwardshadows addshadow`, reads
vertex color into Albedo, uses mesh normals (`SurfaceOutputStandard o; o.Albedo = IN.color.rgb;`),
smoothness 0.05 (0.35 where color is water-ish blue is unnecessary — keep it simple), metallic 0.

### `Water.shader` — `Shader "EarthGame/Water"`
Transparent queue surface shader (`alpha:fade`), animated: vertex shader adds small radial sin-wave
displacement (`sin(worldPos * freq + _Time.y * speed)` combined on 2 axes, amplitude ~0.35 m);
normal perturbed in surf by two scrolling 3D noise-ish sin functions for sparkle; color lerps
`_ShallowColor`→`_DeepColor` by fresnel; `o.Smoothness = 0.92`, alpha ~0.86 with fresnel boost at
grazing angles. Properties: `_DeepColor`, `_ShallowColor` (defaults from PlanetConfig set by OceanSphere).

### `AtmosphereRim.shader` — `Shader "EarthGame/AtmosphereRim"`
Unlit CG, `Blend One One` additive, `Cull Front` (render the inside of the shell so it glows as a
rim around the planet seen from space, and as a sky halo from inside), ZWrite Off,
`Queue = Transparent+10`. Color: fresnel-powered blue (`pow(1 - saturate(dot(viewDir, -normal)), 3)`)
tinted by sun angle (dimmer on night side: factor `saturate(dot(normal, _SunDir)*0.6+0.45)`).
`_SunDir` set globally by AtmosphereController via `Shader.SetGlobalVector("_EG_SunDir", ...)` —
both this shader and the skybox read `_EG_SunDir` as a global; do NOT declare it in Properties.

### `PlanetSkybox.shader` — `Shader "EarthGame/PlanetSkybox"`
Procedural skybox (`Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }`, Cull Off ZWrite Off):
- Inputs: global `_EG_SunDir`; material floats `_SpaceBlend` (0 ground → 1 space), `_NightBlend` (0 day → 1 night).
- Day sky: gradient from `_HorizonColor` at horizon to `_ZenithColor` up; sun disc
  (`pow(saturate(dot(viewDir, _EG_SunDir)), 900) * 4` additive white-yellow + softer glow term).
- Night/space: near-black with **procedural stars**: hash the view direction on a coarse 3D grid
  (`frac(sin(dot(cell, k))*43758.5)` style), tiny bright points, subtle twinkle via `_Time`; star
  intensity = `max(_NightBlend, _SpaceBlend)`; in full space (`_SpaceBlend`→1) the day gradient
  disappears entirely (sky is black + stars + sun disc).
- Sunset warmth: when sun near horizon (`abs(_EG_SunDir.y-ish)` small... use
  `saturate(1 - abs(dot(_EG_SunDir, float3(0,1,0))))`— note the planet is a sphere; acceptable to
  drive sunset tint from `_NightBlend` transition edges (script computes and passes `_SunsetBlend` float).
  Keep it simple: script computes all blend factors; shader just mixes colors.

## Editor

### `SceneBuilder.cs` (in `Assets/EarthGame/Editor/`, wrapped in `#if UNITY_EDITOR`)
Menu item `EarthGame/Create Main Scene`: creates a new empty scene, saves as
`Assets/EarthGame/Main.unity`, adds to Build Settings. (Runtime bootstrap builds everything, so the
scene stays empty — this is just so builds have a scene.) Also menu item `EarthGame/Play From Here`
optional — skip. Keep this file minimal and compile-safe.

## Packages/manifest.json
Default Unity 6 module set only (no registry packages). Include at least: ai.navigation excluded fine;
must include `com.unity.modules.physics`, `imageconversion`, `imgui`, `jsonserialize`, `particlesystem`,
`ui`, `audio`, `terrain` not needed but harmless. Use the standard minimal manifest:
`{ "dependencies": { "com.unity.modules.audio": "1.0.0", ... } }` — writer picks the standard set.

## ProjectSettings/ProjectVersion.txt
`m_EditorVersion: 6000.0.58f1` on the first line (will be adjusted to the installed editor later —
content placeholder is fine).

## Feel checklist (what "done" looks like)
- Press Play in an empty scene → within ~2 s you're standing on grass near a coastline, sun shining,
  ocean visible, mountains on the horizon, trees around, HUD showing ALT/SPD.
- Run: snappy FPS movement, can sprint up hills, jump feels weighty (12 m/s² gravity).
- Walk around the whole planet: gravity always down-to-core; no falling through ground while chunks refine.
- Press F, hold Space + Shift: lift off, terrain LOD coarsens below, fog thins, sky darkens to black,
  stars appear, the planet becomes a beautiful ball with a blue atmosphere rim and specular ocean.
- Fly back down: atmosphere returns, terrain refines, land, F again, keep walking. Day/night cycles.
