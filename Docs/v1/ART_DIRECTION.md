# Art direction - Faceted Highlands

**Status:** approved by the owner 2026-08-25, binding for the render layer. Produced by a
three-way design panel (faceted / painterly / naturalist) judged against this project's hard
constraints; the faceted lens won, with grafts from both others.

The owner's direction that prompted it: *"i think we need to totally reimagine the graphics and
stuff. it looks way too smooth and poly. how does a proper game look better?"*

The strategy that frames it: this is **Stage 1** - free, code-only, and none of it is wasted
later. Palette, lighting, fog, grading and the simulation-drives-placement wiring all carry
forward if authored assets are ever bought; only the meshes would swap. The render layer stays a
thin reader of the simulation, so that remains a bounded change rather than a rewrite.

The one rule above all the numbers: **colour is data.** Everything visible reads the simulation
through `FlatTerrain.GroundColourAt` and the soil model behind it, so a player who can read
country is reading the actual model rather than a decorated version of it (GAME_DESIGN section 3).

=====================================================================
FINAL ART DIRECTION BRIEF — "FACETED HIGHLANDS"
Earth Game. Unity Built-in RP. 100% runtime-generated meshes, shaders, materials. Zero assets on disk.
=====================================================================

**1. THE LOOK AND ITS REFERENCES**

Name: FACETED HIGHLANDS. Every surface is built from big, hard-edged planes that catch the sun individually. Colour is carried entirely in vertex colour driven by the soil/geology data; light does the rest. No textures anywhere — detail = facet variation + silhouette + per-face value jitter. Density is part of the style: grass, litter, and distant canopy are instanced by the thousands, every instance coloured by the data it stands on.

References: Valheim (smooth macro landform, faceted light, continuous colour blends — our exact terrain recipe); Grow Home (chunky faceted vegetation, flat water with facet glints); Synty-built games e.g. Muck (per-face value jitter, one material for everything).

Style thesis: facets are honest. A facet has one slope and one aspect, so a sun-facing dry facet and a shaded damp facet read as different objects. The player can point at one plane of ground and say "that facet is dry" — which is the game.

**2. SHADING MODEL PER ELEMENT**

TERRAIN — smooth data, faceted light. Keep Landform.Smooth() and FlatTerrain.BuildMesh's normal/collider path untouched. In the fragment shader of EarthGame/TerrainVertexColor:

```
float3 n = normalize(cross(ddy(i.worldPos), ddx(i.worldPos)));
if (n.y < 0) n = -n;   // derivative-handedness guard; terrain never overhangs
```

Facet size = vertex spacing: 4 m at LOD0, 8/16/32 m outward; big distant facets under fog read as painted hills. Vertex COLOUR stays smoothly interpolated — do not posterize the wetness ramp; Valheim-style smooth colour over hard light.

NEW (graft from B): in BuildMesh, pack `colors[i].a = Mathf.Clamp01(landform.WetnessAt(east, north))`. Fragment: `albedo.rgb *= lerp(1.0, 0.82, a)` plus a sun-only Blinn term from `_EG_SunDir`: power `lerp(8, 96, a*a)`, strength `0.25 * a * a`, sun colour. Computed with the faceted normal, so wet gully facets glint plate-by-plate toward the sun; dry spurs stay matte.

PROPS (trees, stones, grass trees, litter, dropped items) — true hard normals: duplicate vertices per triangle, `n = normalize(cross(b - a, c - a))` (Unity clockwise winding). Bake per-FACE value jitter into vertex colour: `base * (1 + (hash(faceIndex, seed) - 0.5) * 0.10)`. One new shader for all props, EarthGame/FacetVertexColor: surface shader, Lambert lighting model, vertex-colour albedo, zero specular, casts + receives shadows, fog on, no _MainTex. Delete every Standard-shader MakeMaterial() path in ScatterField and GroundCover; colour lives in the mesh, or in a MaterialPropertyBlock tint for instanced/dropped items.

GRASS — single-triangle blades: one triangle = one facet, flat-shaded by construction (section 5).

WATER — flat-faceted moving surface. 2 m quad grid over each WaterBodies surface, vertices displaced ±0.03 m by two drifting sine octaves of (worldPos, time); fragment normal from the same ddx/ddy cross. Colour: `lerp(WaterTannin, currentHorizonColour, pow(1 - saturate(dot(n, viewDir)), 3))` + one Blinn-Phong sun glint from `_EG_SunDir`, power 220, sun colour. Sparkle, not gloss.

**3. PALETTE (RGB 0–1, authored for the post grade in section 7)**

```
StrawDry        (0.62, 0.55, 0.33)   dry tussock on shedding spurs (replaces Straw)
GrassGreen      (0.36, 0.45, 0.22)   winter-green flats (replaces Grass)
GullyWet        (0.22, 0.30, 0.17)   soaks, rush beds, gathered ground (replaces Rushes)
CanopyOlive     (0.33, 0.38, 0.22)   primary eucalypt crown
CanopyGreyGreen (0.44, 0.49, 0.36)   secondary grey-blue crown
TrunkCream      (0.80, 0.76, 0.68)   smooth gum bark — the signature colour
TrunkRough      (0.42, 0.36, 0.30)   rough bark stocking, lower trunk
LitterGreyBrown (0.40, 0.33, 0.22)   eucalypt litter
RockSandstone   (0.71, 0.60, 0.44)   pale warm country rock
RockShale       (0.33, 0.34, 0.35)   cool dark secondary stone
WaterTannin     (0.13, 0.18, 0.16)   tea-stained creek water
SkyHorizonNoon  (0.78, 0.86, 0.93)   also the fog colour
SkyZenithNoon   (0.29, 0.50, 0.82)
SunNoon         (1.00, 0.97, 0.90)   cool clear winter sun, intensity 1.25
```

Wiring: extract FlatTerrain.GroundColour into a public static `GroundColourAt(double east, double north)` (graft from C) keeping its exact wetness^0.6 / 0.55-breakpoint / depth / rock logic, with Straw→StrawDry, Grass→GrassGreen, Rushes→GullyWet. Terrain, grass tufts (section 5), and litter (section 5) ALL call this one function — the ground and everything standing on it cannot disagree. FlatSky DayHorizon/DayZenith/DayLight → the three sky/sun values above. Existing dusk/night ramps stay.

**4. TREE RECIPE — PROCEDURAL EUCALYPT**

Kill the cylinder+spheres. 12 pre-built mesh variants at startup (seeds 0..11), assigned per instance by CellRandom (draw order preserved so harvest IDs stay stable). GameObject per tree stays (colliders, WorldObject interaction); all trees share 12 meshes + 1 FacetVertexColor material. After a cell builds, CombineMeshes its tree renderers into one mesh on the cell holder, colliders kept as separate children (graft from C) — 1 render draw per scatter cell.

TRUNK: 5-sided tube (pentagon = visibly faceted), 5 stacked segments. Base radius 0.030 × height tapering to 0.35× at top. Each segment direction = previous rotated 3–9° around a random horizontal axis; whole-tree lean 2–10°. Never a straight tree.

LIMBS: 1–2, forking at 55–75% of height, 5-sided, 3 segments, splayed 25–50° off trunk, radius 0.45× trunk radius at fork. Visible bare limb between trunk and crown is what makes it not-a-pine.

CANOPY: 3–6 separate clumps at limb/trunk tips — never one blob; sky must show between clumps. Each clump: icosahedron (20 faces), vertices pushed radially by `(0.65 + 0.7 * hash01) * clumpRadius`, scaled (1.0, 0.7, 1.0), offset 10–20% sideways off its tip. clumpRadius = height × (0.10–0.17). Hard normals, 60 verts/clump.

COLOUR (vertex colour): trunk TrunkCream, bottom 2 rings TrunkRough (blend height jittered ±0.5 m per tree). Canopy = `lerp(CanopyOlive, CanopyGreyGreen, perTreeRandom)`, per-clump value jitter ±8%, per-face ±5%, per-tree G nudge ±0.03.

DATA HONESTY (graft from C): MaxTreesPerCell 4 → 7; per-tree keep-chance = `0.35 + 0.65 * smoothstep(0.15, 0.55, wetnessAtPlantPoint)`. Height range 6–16 m, scaled ×`lerp(0.8, 1.15, wetness)`; when wetness > 0.6, bias canopy colour 20% toward GullyWet. Gullies close over with taller, darker trees; spurs carry scattered straw-country trees — readable from a ridge.

BUDGET: ≈480–700 verts, 160–230 tris per tree.

STONES: jittered icosahedron (20 faces, 60 flat verts), vertices pushed ±25% by hash(id), bottom third flattened (`y = max(y, -0.15 * size)`), per-face jitter ±6%. Existing StoneColour() via MaterialPropertyBlock tint. GRASS TREE: keep proportions; skirt = cone of 24 single-triangle spikes (72 verts).

**5. GROUND COVER — GRASS TUFTS**

MESH: one shared tuft. 7 blades, each ONE triangle: base width 0.05 m, height 0.25–0.5 m, splayed 15–40° from vertical, tips randomly rotated. 21 verts, 7 tris. Vertex alpha = 0 at base, 1 at tip (wind weight).

INSTANCING: Graphics.DrawMeshInstanced, ≤1023/batch, ShadowCastingMode.Off, receives shadows. 16 m cells within 70 m of player, built on cell entry, ≤2 cells/frame (ScatterField pattern), matrices cached; per-instance colour via MaterialPropertyBlock.SetVectorArray.

DENSITY AND COLOUR FROM THE DATA: per candidate — count = `40/cell * clamp01(SoilDepthAt / 0.25) * (1 - slope01 * 0.6)`; skip if UnderWater, height < 1 m, or SoilDepthAt < 0.05 (graft from C). Colour = `GroundColourAt(root)` ± 6% value jitter — the tufts are literally the terrain colour standing up. Instance y-scale = `lerp(0.6, 1.3, wetness)` (graft from C): tall rank tussock in moist flats, short sparse straw on spurs. Budget ~10–15k live instances, ~100k tris, 10–15 draws.

WIND: vertex shader, `offset.xz += sin(_Time.y * 1.7 + worldPos.x * 0.35 + worldPos.z * 0.27) * 0.06 * colour.a`. Nothing else moves; late-winter still air.

LITTER: GroundCover keeps its ragged discs; colour = `GroundColourAt(patch) * 0.82` blended 50% toward LitterGreyBrown; both materials move onto FacetVertexColor.

FAR CANOPY (graft from B, faceted): new FarCanopy component. Terrain tiles rings 1–6: hash-scatter 30–50 single-clump impostors per tile — the jittered icosahedron clump mesh (60 flat verts, 20 tris), no trunk, tinted `CanopyGreyGreen * 0.92`, y-scale 0.7 — via DrawMeshInstanced, positioned by SampleHeight, skipped underwater, no shadows. Same hash family as ScatterField so a distant clump becomes a near tree in roughly the same place. ~3–4k instances, ~64 draws, ~70k tris. Fog paints them into ridgeline layers.

**6. ATMOSPHERE**

FOG (superseded 2026-09-03, M1 First Light; this paragraph described a derivation that no longer exists): the density was `lerp(0.0008, 0.0015, dewFactor)` off a cosine dew curve copied from GroundCover.MoistureAt, written in FlatSky. It is now `FlatScale.HazeFogMode` + `FlatScale.FogDensityFor(cloud, humidity)`, and FlatSky owns neither: **FogMode.Exponential** (Beer–Lambert, so Unity's density is the extinction coefficient a forecast quotes), with the visual range shortened from 10 km at half saturation by Kasten's `(1-RH)^-0.6` hygroscopic growth, floored at the clear-air seed and capped at the WMO's 1.5 km mist floor. The humidity and cloud are the Sim's own weather, not a curve drawn here. The fog and the wet grass are still the same fact — they are the same humidity now rather than the same hand-drawn cosine. Fog colour is unchanged: `lerp(horizonColour, sunColour, 0.12 * dayFactor)`, set every frame in FlatSky and pushed as global `_EG_FogColor`.

SKY WELD (graft from B): in EarthGame/PlanetSkybox, the lowest ~8° of sky outputs `lerp(zenithGradient, _EG_FogColor, pow(1 - saturate(up), 3.0))`. Distant faceted ridges dissolve into an identical value with no seam — flat pale cutouts stacked in depth, the strongest single image this style produces. No per-pixel aerial-perspective pass.

LIGHT: sun = SunNoon, intensity 1.25, shadowStrength 0.85, soft shadows. Ambient stays trilight (the mode is set once in FlatWorldBootstrap, on the sphere by SunController; FlatSky writes the three colours it makes readable, every frame); raise ambientGroundColor to `(0.26, 0.22, 0.17) * dayFactor` so unlit facets don't go mud. Late August at 34.5°S gives ~43° noon sun and long shadows from the real solar maths already in the game — the facets finally make it visible.

**7. POST — ONE OnRenderImage SHADER ("EarthGame/PostGrade")**

Camera.allowHDR = true. Ops in order:
1. Exposure ×1.15
2. Warm tint ×(1.03, 1.00, 0.96)
3. ACES-ish tonemap (Narkowicz): `c*(2.51c+0.03)/(c*(2.43c+0.59)+0.14)`, saturate
4. Contrast 1.08 around pivot 0.45
5. Saturation 1.10 (lerp from luma)
6. Vignette 0.22: `c *= 1 - 0.22 * smoothstep(0.55, 1.0, length(uv - 0.5) * 1.414)`
7. Dither `+ (hash(uv * screenSize) - 0.5) / 255` — non-optional; the sky gradient bands without it.

No bloom, no LUT, no depth pass. The palette is muted because this grade lands it on "cool clear winter".

**8. PERFORMANCE (added cost target < 2 ms at 1080p midrange)**

- Terrain facet + wet gloss: ~0.1 ms (ddx/ddy replaces normal read; ~10 ALU added; mesh/collider path untouched).
- Trees: ~340 visible × ≤700 verts ≈ 240k verts, combined per cell → ≤~170 draws total. Less geometry than today's primitive spheres. ~0.2 ms.
- Grass: 10–15k instances, 7 tris, no shadows, 10–15 draws: 0.5–0.8 ms — the biggest line; tune via 70 m radius and 40/cell density.
- FarCanopy: ~4k × 20 tris, ~64 instanced draws, no shadows: ~0.15 ms.
- Water: coarse grid per visible body: 0.1–0.2 ms. Post: ~0.25 ms.
- Total ≈ 1.3–1.7 ms. Degrade order: grass radius 70→55 m, FarCanopy 50→30/tile, grass 40→28/cell.

RULES: no per-frame mesh rebuilds (grass/FarCanopy cells build on entry, ≤2/frame); all colour variation via vertex colour or MPB, never material instances; the only new materials in the project are FacetVertexColor, the water shader, and PostGrade. Deliberately absent: textures, normal maps, tri-planar, particles, bloom, per-pixel colour noise, soft canopy normals — the budget is spent on facets, silhouettes, and data-coloured density, because those are cheap, procedural, and legible.

FILE-BY-FILE
- FlatTerrain.cs: extract static GroundColourAt; 3 palette constants; write wetness to vertex alpha.
- TerrainVertexColor.shader: faceted ddx/ddy normal; wetness darkening + sun-only gloss from alpha.
- ScatterField.cs: BuildTree body → EucalyptBuilder (new static class, 12 variants); stone icosahedron; MaxTreesPerCell 7 + wetness keep-chance; per-cell CombineMeshes; CellRandom draw order preserved.
- FlatSky.cs: sky/sun constants, fog tint, `_EG_FogColor` global. NOT the fog density or the fog law — since 2026-09-03 both are `FlatScale`'s, asked for with the Sim's cloud and humidity; the dew curve that used to live here is gone.
- PlanetSkybox.shader: horizon weld to `_EG_FogColor`.
- GroundCover.cs: litter colours from GroundColourAt; materials → FacetVertexColor.
- New: FacetVertexColor.shader, GrassField.cs, FarCanopy.cs, EucalyptBuilder.cs, PostGrade.cs + shader, faceted Water.shader update.

Test of success: stand on a spur at 3 pm. Hard-lit facets grade plane-by-plane down the hillside; wet gully facets glint toward the sun under taller, darker trees; sparse straw tufts underfoot thicken into rank green tussock toward the soak; four pale ridgeline layers dissolve seamlessly into the sky. Every one of those reads is the simulation.