# Draft contract WG.0 — Can the atlas describe a chosen place?

**Status:** drafted 2026-09-13, unopened. Owner: Claude since 2026-09-18 (Codex, its author, retired: CANON ruling 26 as
amended). Acquisition requires William's permission for the exact
files; the contributor opens implementation contracts. The completed research is
[WORLD_GENERATION_DESIGN.md](../WORLD_GENERATION_DESIGN.md). The bounded inspection of data already present is
opened separately as [WG.0a](WG.0a_EXISTING_ELEVATION.md), and does not fulfil this multi-field atlas contract.

## What was found

The design proposal identifies plausible global inputs, specific coverage/licensing gaps and regional
assumptions in the current engine. See its §§3 and 10 for the evidence; this contract does not copy that register.
Reading the providers' documentation has not established actual coordinate results, download cost or runtime cost.

## What this slice would promise

1. **An acquisition manifest before data is fetched.** Exact dataset versions/files, stated or measured remote
   sizes, aggregate download estimate, licence terms, source links and intended transformations. Resolve the
   proposal's conditional products or omit them explicitly. Acquire only the files authorized by William, and
   add their notices when imported. No live data fetch is introduced into gameplay.
2. **An atlas inspection tool outside gameplay.** Given latitude/longitude, report source-backed geographic
   conditions and neighbouring context, with source/version, units, source resolution and missing-data status
   for every field. It must never substitute a default starter region for an unknown place.
3. **Eight recorded probes.** One in each of the proposal's three example regions; two immediately either side
   of the antimeridian; one near each pole; and one open-ocean point. Pin coordinates and independently established
   expectations before implementing lookup. These test lookup coverage; a valid missing-data result is not a
   claim that the location has complete terrain or gameplay support.
4. **Independent comparisons.** For each available selected field, compare the tool to a separately implemented
   GIS/source query. Print both values, units and source-appropriate tolerance. Categorical IDs/masks must match
   exactly away from an explicitly classified boundary; missing-data markers must never become valid zeroes.
   Continuous tolerances and boundary policies must be written before opening the implementation portion.
5. **Measured costs.** Record downloaded, expanded and prepared bytes, peak processing memory, and cold/warm
   query latency over the pinned probe set. This is feasibility measurement, not a promise of an unmeasured
   performance budget. Report unavailable fields and costly dependencies rather than declaring worldwide readiness.
6. **A recommendation from results.** State whether the selected atlas is sufficient for a first landscape
   experiment, what regional evidence is missing, and what minimum data package that experiment would require.

## Non-goals

Terrain generation; a playable start selector; Unity changes; changing the current region, scorer, catalogue,
wire protocol, existing world layers or saves; naturalizing all of Earth; a new planet renderer; guaranteeing
resources around a start; closing any beta debt solely by changing a reference criterion.

## Formats and checks touched

Existing game formats and gates remain unchanged in this slice. If the inspection tool writes machine-readable
reports, define/version that report and its independent verifier reference before implementation, following
ARCHITECTURE §10 and STANDARDS 19. Propose its filename/schema in an amendment; do not appropriate `run.jsonl`.
The manifest owns input provenance. Existing source/reference metadata is linked, not reauthored in two places.

## How it would be proved

Run the eight probes through both independent paths, capture their comparisons and exit codes, prove refusal
of missing required files and invalid coordinates, and measure the costs named above. A deliberate bad axis
order, unit scale or nodata conversion must be detected by the relevant check. Keep expected source facts
separate from the tool implementation. No test or measurement is claimed to have run by this draft.

Before opening: William chooses the acquisition scope; exact files, probe coordinates, reference procedure,
continuous tolerances and any report schema are added to this contract. Do not silently fill those in after a
failed check. William's instruction of 2026-09-13 authorizes independent contribution, but explicitly reserves
permission for each acquisition; no new data is authorized by this draft.

## Exit record

Unopened; no implementation or data-acquisition result.
