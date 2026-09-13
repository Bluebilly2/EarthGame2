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

Open. Counts and evidence will be recorded from the completed runs.
