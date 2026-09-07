# G2 direction notes — seeds for the world-look pass (non-binding)

**Status: notes, not a contract. Collected by FABLE from owner conversation, 2026-09-01.
G2 (world presentation) begins after G1 (readability) closes; its gate is a visual-direction
page the owner marks up. These seeds go into that conversation so they are not lost.**

## The CAD seed (owner, 2026-09-01)

The owner drew the distinction between Blender-style modelling — polygons frozen into an
approximation at authoring time — and CAD — the surface as mathematics, sampled at whatever
resolution the view demands. Ruled direction, with the reasoning on the board that day:

- **Parametric truth is already this project's law at the model layer** (the ocean is a formula
  in metres from the waterline; terrain is erosion math; item geometry generates from measured
  dimensions). G2 extends that honesty toward the eye rather than importing sculpted meshes.
- **Adopt selectively:** screen-error tessellation for terrain and water refinement (silhouettes
  that stay curved under approach), post-beta; no raymarched/SDF world rewrite — engine-scale
  detour, wrong era for it.
- **The deep alignment, for the manufacturing era:** GAME_DESIGN already says a manufactured
  part IS its parameters (dimensions, tolerances, distributions). When manufacturing arrives,
  made objects should render by tessellating their engineering definition — CAD representation
  as gameplay truth, not art style. The bolt drawn from its own tolerance record.
- Organic life stays mesh/procedural — CAD never had to walk a wallaby over its surfaces.

## Standing constraints any G2 work inherits

Law 7 until Phase 4 (IMGUI, everything from code, no asset pipeline); the frame-pair currency
and owner-judged batches from G1; PostGrade is the one full-screen pass and any new grading
composes with it rather than beside it.
