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

Completed 2026-09-13 on the `e3e93c6` baseline. `WorldLayers` now owns input validation before generation;
the later dimension/type checks in `Mapped` were removed, leaving one owner. The server's existing error path
handles `InvalidDataException`, so a mismatched bake produces a useful refusal instead of a partly made world.
No file format or generator rule changed. Primary-region and saved-layer checks remain a named Codex debt.

Evidence below is in `C:/Users/willi/projects/EarthGame2-world-generation-design/Artefacts/`.

| Check | Observed | Required / reference |
|---|---|---|
| Tests before implementation, `dotnet test Engine/EarthGame.slnx -c Debug --nologo --no-restore --filter FullyQualifiedName~WorldInputTests` | 15 failed, 2 passed; exit 1 | A red run: the old code accepts bad inputs; exit nonzero |
| Same input tests after implementation | 17 passed, 0 failed/skipped; exit 0 | 17 passed; 0 failed/skipped; exit 0 |
| Final full engine suite, `dotnet test Engine/EarthGame.slnx -c Debug --nologo --no-restore` | 443 passed, 0 failed/skipped; exit 0 | 0 failed/skipped; exit 0 |
| Unity-layer compile | 0 warnings, 0 errors; exit 0 | 0 warnings/errors; exit 0 |
| `python Tools/gate/run_gate.py WG.0b` | exit 0 | exit 0 |
| Independent dedicated-host cases | 16 outcomes agree: 2 valid, 14 invalid | Reference from synthetic inputs: 2 valid, 14 invalid |
| Aligned inputs and absent optional outlines | each exit 0; world saved | each exit 0; world saved |
| Invalid inputs, including shifted-origin sidecars retaining identical raw bytes | each exit 2; 0 partial world folders | each exit 2; 0 partial world folders |
| Bherwerre before/after raw-layer comparison, seed 1347 | 24 hashes agree; 0 mismatches | Same 24 layer names; 0 mismatches |
| Saved wake, east / up / north metres | -1392 / 0 / 2804 | Before change: -1392 / 0 / 2804 |

The source of each geographic expectation is the verifier's own synthetic sidecar, converted to post positions
by the documented frame equation; it does not call `WorldLayers` or the bake's reader. The real-world comparison
uses Python SHA-256 over raw file bytes, not the hashes claimed in their sidecars. It includes every capacity,
stand, loose, cover, water and height layer. Every comparison prints actual and required values.

- Red and focused green logs: `codex-wg0b-red-20260913-161409/tests.log` and
  `codex-wg0b-tests-20260913-161546/tests.log`.
- Full suite and compile: `codex-wg0b-final-20260913-162136/{engine,unity}.log`. Compile command:
  `dotnet build Engine/EarthGame.Unity.Compile.csproj -c Debug --nologo --no-restore`, with
  `-p:UnityScriptAssemblies=<main>/Unity/Library/ScriptAssemblies` and
  `-p:UnityPackageCache=<main>/Unity/Library/PackageCache`, using the existing import without launching Unity.
- Gate: `codex-wg0b-gate-20260913-161751/gate.log`.
- Before world and host log: `codex-wg0b-before-20260913-161245/`. Both worlds use the existing host's
  `+server.data <main>/Data/regions/bherwerre +server.seed 1347 +server.port 0 +server.seconds 1` and fresh
  `+server.world` folders. No data was fetched and no player was launched.
- Comparison: `codex-wg0b-real-20260913-161819/comparison.log`; run as
  `python Tools/verifiers/checks/world_inputs_check.py --data <main>/Data/regions/bherwerre --before
  Artefacts/codex-wg0b-before-20260913-161245/world`. The after world and per-case host logs are under
  `codex-world-inputs-668f07a6af784eb4a66c1f2c9a3f79a5/`.

No lake, coast, plant or animal-capacity debt is claimed paid; valid layers and the wake did not change, so the
corpus route is not re-laid. There is no new rendered appearance or feel claim. William was told the scope
before implementation; atlas acquisitions remain his to allow, and eventual landscapes his to judge.
