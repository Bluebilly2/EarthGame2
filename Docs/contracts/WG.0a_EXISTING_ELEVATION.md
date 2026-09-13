# WG.0a — What the existing global elevation bake knows

**Opened 2026-09-13; owner: Codex.** The atlas proposal is in
[WORLD_GENERATION_DESIGN.md](../WORLD_GENERATION_DESIGN.md); multi-field acquisition stays in WG.0.

## What was found

`Data/global/EarthElevation.r16` and its JSON sidecar already exist on this machine. The legacy bake is a
cell-centred equirectangular grid, although its sidecar's shorthand says row zero is +90 degrees. Its writer
(`Earth Game/Assets/EarthGame/Editor/ElevationDataTool.cs`, inspected locally, `Reproject`) clamps the source
latitude to ±85.0511288 degrees before averaging. Polar output is repeated edge data, not polar observation.
The existing notice is the AWS Terrain Tiles entry in `THIRD_PARTY_NOTICES.md`; no files are acquired here.

## Promises

1. A Python standard-library CLI `Tools/world/atlas_probe.py --source <sidecar> --point <lat> <lon>`
   (repeat `--point`) reads only the existing legacy global bake. No default region, network, game code, or
   writes to the input. Latitude must be finite in [-90,90], longitude finite in [-180,180]; +180 aliases -180.
2. Select the containing cell, with north/west edges inclusive and south/east edges exclusive; the south pole
   belongs to the last row. Cell centres are west + half a cell and north - half a cell. Return its stored
   signed little-endian integer metres, **without interpolation**, only when the cell's entire latitude span
   is within source coverage. Otherwise return null and `source-edge-or-outside`. This is coarse source
   inspection, not a local heightfield, a land/sea classifier, or evidence that a start is supported.
3. Refuse missing files, wrong byte length, unsupported format/source, non-positive or non-integral dimensions,
   non-2:1 grids, or metadata reporting missing source tiles. Error: stderr, exit 2, no JSON on stdout.
   Accept the legacy format/source described below only; do not silently interpret future atlas formats.
4. Emit the versioned report below, including both input hashes and explicit absent fields. Eight real probes:
   (-35.140,150.675), (-25,137), (27.9881,86.9250), (0,179.999), (0,-179.999), (89,0), (-89,0), (0,-140).
   They are locations to inspect, not pre-claimed geographic expectations. Check their selected cell values
   against a separate reader; do not claim that agreement validates the original source's geographic accuracy.
5. The independent verifier enumerates latitude/longitude cell intervals and decodes signed values by byte
   arithmetic; the tool uses direct index arithmetic and `struct`. Print observed and expected beside every
   check (exact integer/status/hash comparison, centre/bounds tolerance 1e-10 degrees). Synthetic signed,
   asymmetric values expose axis/order mistakes; edge, seam and pole cases expose registration mistakes.
   Deliberately alter one result and prove the verifier exits nonzero. Gate `WG.0a` requires this verifier.

## Diagnostic format (`eg2.atlas_probe`, version 1)

One JSON object on stdout. `source`: `sidecar_sha256`, `raw_sha256`, `raw_bytes`, `width`, `height`,
`source_url`, `source_zoom`, `cell_degrees` {`latitude`, `longitude`}, `source_latitude_limit_deg`,
`sampling` = `containing-cell`, `registration` = `cell-centres`.
`unavailable_fields` = [`land_water_class`, `climate`, `geology`, `soils`, `ecology`].
`points`: input order, each with `latitude_deg`, canonical `longitude_deg`, `row`, `column`,
`cell_centre` {`latitude_deg`, `longitude_deg`}, `cell_bounds` {`north`, `south`, `west`, `east`} in degrees,
`elevation_m` (integer or null), `status` (`available` or `source-edge-or-outside`).
No timestamp or absolute input path: identical bytes and arguments produce identical stdout.
`format` and `version` are required top-level fields; new semantics require a new version.

Supported input is the legacy sidecar's exact `format` string
`int16 little-endian, metres, row 0 = +90N, col 0 = -180E` and `sourceUrl`
`https://s3.amazonaws.com/elevation-tiles-prod/terrarium/{0}/{1}/{2}.png`; integer `sourceZoom` in [0,15],
`totalTiles` = 4^sourceZoom, `missingTiles` = 0. Raw sibling has the sidecar stem and `.r16` suffix.
The sidecar is legacy and unversioned; recognising it is an explicit adapter, not a new writer of that format.

## Proof and exclusions

Run `python Tools/gate/run_gate.py WG.0a`; also run the verifier with `--source <existing sidecar>` for the eight
real probes. Preserve reports and comparisons in fresh `Artefacts/codex-*` folders. Before committing, run the
engine suite and Unity compile under WORKING's checklist. No new dataset, package, generation, start UI,
streamed/saved/drawn format, visual or feel change. Source fidelity, pre-human reconstruction and all missing
atlas fields remain future work; William judges the eventual landscapes, not these arithmetic checks.

## Exit record

Completed 2026-09-13, rebased on main `179bf8d`. The probe, its independent verifier and `WG.0a` gate landed
with the design proposal brought current with protocol 8, far layers 6/7 and main's height-range refusal.
No beta code, game format or acquired data changed. Existing AWS attribution already covers the inspected bake.

Evidence is in the `EarthGame2-world-generation-design` worktree:
`Artefacts/codex-wg0a-final-20260913-121150/{engine,unity,gate,existing}.log`;
the source report and sabotage evidence are in
`Artefacts/codex-atlas-check-3329131d2cd14c68910e7fbd6b2b58b0/`.
The suite before changes on main was 395 passed, 0 failed, exit 0. Main added a tile-range test during this
slice; the final suite below is the rebased tree's own run, not a count attributed to the Python work.

| Check | Observed | Reference / required |
|---|---|---|
| Engine suite, `dotnet test Engine/EarthGame.slnx -c Debug --nologo --no-restore` | 396 passed; 0 failed; 0 skipped; exit 0 | 0 failures/skips; exit 0 |
| Unity-layer compile, `dotnet build Engine/EarthGame.Unity.Compile.csproj -c Debug --nologo --no-restore` | 0 warnings; 0 errors; exit 0 | 0 warnings/errors; exit 0 |
| `python Tools/gate/run_gate.py WG.0a` | exit 0 | exit 0 |
| Synthetic coordinate probes, including cell edges, centres, seam and poles | 56; no mismatches | 56; exact integers/status; geometry within 1e-10 degrees |
| Invalid input cases (6 coordinates, 9 metadata, 2 byte lengths, 2 missing files) | 19 refusals, each exit 2 and empty stdout | 19 refusals, each exit 2 and empty stdout |
| Corrupted report, separate verifier process | exit 1; elevation -299 m | exit 1; source elevation -300 m |
| Existing-source probes, `atlas_probe_check.py --source <main>/Data/global/EarthElevation.json` | 8; no mismatches; exit 0 | 8; no mismatches; exit 0 |
| Identical-query reruns, fixture and existing source | stdout identical in both | stdout identical in both |
| Fixture raw and sidecar after all queries | unchanged / unchanged | unchanged / unchanged |

The worktree uses main's existing Unity import via `-p:UnityScriptAssemblies=<main>/Unity/Library/ScriptAssemblies`
and `-p:UnityPackageCache=<main>/Unity/Library/PackageCache`. Its initial NuGet restore used a config with all
package sources cleared, resolving installed packages only; no package acquisition was required.

| Coordinate (latitude, longitude) | Probe elevation m | Independent source-cell elevation m |
|---|---|---|
| -35.140, 150.675 | 33 | 33 |
| -25, 137 | 123 | 123 |
| 27.9881, 86.9250 | 6208 | 6208 |
| 0, 179.999 | -5100 | -5100 |
| 0, -179.999 | -5212 | -5212 |
| 89, 0 | null: source-edge-or-outside | null: source-edge-or-outside |
| -89, 0 | null: source-edge-or-outside | null: source-edge-or-outside |
| 0, -140 | -4284 | -4284 |

The raw file is 16,777,216 bytes; SHA-256 `1998447c62a53b017eef05fe2a101cf598046669ca92ae51ba8c74c2a8e9354b`.
The report records the sidecar hash too. Each cell spans 0.087890625 degrees in latitude and longitude.
These are coarse cell averages, including the mountain probe; none is a claim about exact point height or a
playable surface. Negative elevation does not establish that a cell is ocean. The two dateline probes are
different neighbouring cells; +180/-180 identity is tested separately on the synthetic fixture.

**What changed on the way:** the verifier's first run overwrote its own temporary fixture sidecar with its
report and failed. Report filenames now end `.report.json`, and both input files are checked unchanged. The
initial test-before-tool run failed (exit 1), and the intentional one-metre report corruption was also rejected.
The gate's passing run does not count that expected sabotage failure as a passing source comparison.

**Judgement:** retain this bake as a coarse relief diagnostic. It is insufficient to drive the first landscape
experiment alone. The next data work is WG.0's exact acquisition manifest for land/water, climate and terrain
context, with explicit permission before any fetch. Missing atlas coverage is recorded under Codex in DEBTS.
No frames or hands-on verdict is claimed: this slice changes neither appearance nor feel.
