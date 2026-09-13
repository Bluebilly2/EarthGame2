# WG.0b — The maps used to make a world must agree

**Opened 2026-09-13; owner: Codex.** Builds on WG.0a's finding that coordinates and source metadata must be
explicit, and the arbitrary-start proposal in WORLD_GENERATION_DESIGN. No data acquisition is needed.

## What was found

On main `e3e93c6`, `WorldLayers.Mapped` checks dimensions and integral storage only. Equally sized water and
height rasters can name different regions, origins, pitches or extents and still be combined by array index.
A different integral layer can also be taken for water-body IDs. The resulting lake, soils, plants and animal
habitat would describe an accidental mixture. The check occurs after lake-finding work has already begun.

## Promises

1. `WorldLayers.Compute` checks its inputs before allocation and before its first progress callback. Heights
   must be a `heights` layer in metres. Optional water outlines must be `water_bodies`, integral IDs, unit `id`,
   scale 1. The existing no-outline path remains supported.
2. Paired inputs must have identical region ID (ordinal), dimensions, cell pitch, extent and latitude/longitude
   origin. Numeric equality is exact: these are grids from the same bake, not independently registered surveys.
   Equivalent text such as `4`/`4.0` compares numerically. No silent resampling, longitude canonicalisation,
   unit conversion or default-region replacement. Numeric frame fields must be finite, pitch/extent positive,
   and origins in latitude [-90,90] and longitude [-180,180]. This is input validation, not new polar support.
3. A refusal is `InvalidDataException`, naming the offending layer/field and both actual and required values.
   The dedicated host must report failure with exit 2 and write no world. Valid input and absent optional water
   must still succeed. Existing raster/wire/save formats and all layer-generation rules retain their versions.
4. On the installed Bherwerre bake and seed 1347, before/after worlds must have exactly equal sets of generated
   raw-layer SHA-256 hashes and equal saved wake coordinates. In particular this covers `capacity_*`, `stand`,
   water and terrain. No changes to AnimalPresence or the scorer; no route or frame changes are expected.

## Proof

Engine tests first: shifted origins, wrong region, different pitch/extent, wrong layer roles/units/scale,
non-integral water, invalid frame numbers and valid/no-water cases. Refusal happens before any progress or
output. The old code must fail these tests; retain that red run as enforcement sabotage evidence.

`world_inputs_check.py` is independent: it writes synthetic `eg2.raster` v2 inputs itself, derives the physical
positions of grid posts with the documented frame equation, and drives the existing dedicated host through
`+server.data` and `+server.world`. It imports no engine or bake helper. Compare host exits and filesystem
outcomes to those input-derived expectations, printing both values. `WG.0b` gates the check and requires its
manifest entry. A deliberately shifted sidecar with unchanged raw bytes must fail the real host path.
For Bherwerre, hash raw bytes directly with Python and compare both runs; do not trust the writer's claimed hashes.
Read only existing contracted rasters and `eg2.world` v1; do not introduce another report schema.

Run the full engine suite and Unity compile before commits. All evidence goes in fresh `Artefacts/codex-*`
folders, with no deletes. Builds wait while a player is running. Baseline on main: 426 passed, 0 failed/skipped,
exit 0 (`Artefacts/codex-wg-baseline-20260913-160734/engine.log`). Record final counts as run below.

## Non-goals

Reprojecting inputs; new start selection; geographic truth or pre-human reconstruction; repairing coastline,
lake-bed or plant-placement debts; changing any valid world's layers, capacity rules, saved state or drawing;
validating all saved-layer combinations on continue; checking the requested `Region` against the primary bake.
The last two are separate input boundaries and must not be silently folded into this change.

## Exit record

Open. William was told the scope before implementation. There is no appearance or feel decision in this slice;
worldwide atlas acquisitions remain his to allow, and eventual landscapes his to judge.
