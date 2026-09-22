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

## Acquisition manifest (2026-09-20), approved whole by William on 2026-09-21 ("all six")

Promise 1 of this contract, written before anything is fetched. William asked for it on 2026-09-20 ("2. yes") under
ruling 26 as amended: the head developer writes the list; **nothing is downloaded until he approves these exact
files.** Provider pages were read on 2026-09-20 (documentation and directory indexes only, no data): where a page
states no size, this says so rather than estimating.

**The packaging rule that makes most of the licences simple.** The raw datasets are never shipped. They are read once,
offline, into the atlas's own derived form — the same way the region's elevation is baked today — and only that
derived form travels with the game. A licence therefore has to permit our *use* and the distribution of a *derivative*;
only one candidate below fails that, and it is the one left out.

| # | Dataset | What it answers for a chosen coordinate | Files | Size as stated | Licence | Take? |
|---|---|---|---|---|---|---|
| 1 | **Natural Earth 1:10m physical**, v5.1.1 | Where the coasts, islands, lakes and glaciers are, for choosing a place on a map | "Download all 10m physical themes" | 49.99 MB | Public domain; credit optional | **Yes** |
| 2 | **NOAA ETOPO 2022**, 30 arc-second surface | The relief and the sea floor of anywhere on Earth, at about 900 m | `ETOPO_2022_v1_30s_N90W180_surface.tif` | 1.5 GB (directory index; the product page states none) | The product page states none; the dataset's metadata record lists CC0, and the DOI citation is required | **Yes** |
| 3 | **HydroLAKES v1** | Every lake over 10 ha, with its pour point | `HydroLAKES_polys_v10.gdb.zip` | 763 MB | CC-BY 4.0; redistribution and commercial use allowed | **Yes** |
| 4 | **RESOLVE Ecoregions 2017** | Which ecological region a place belongs to, and so what could grow there | `Ecoregions2017.zip` | 150 MB | CC-BY 4.0 | **Yes** |
| 5 | **GLiM v1.0**, PANGAEA gridded release | The rock family under a place, at half a degree | one file at `hdl.handle.net/epic.39939.d001` | 37.8 kB | CC-BY 3.0 | **Yes** (it is tiny; it is also coarse, and it is *not* the polygon map its abstract describes) |
| 6 | **CHELSA v2.1** climatologies 1981–2010 | Month-by-month temperature and rainfall at about 1 km | `tas` and `pr`, twelve months each, COG | **No size stated anywhere on the provider's pages** | CC0 | **Yes, with a ceiling**: the sizes are asked of the server before anything is fetched, and the fetch stops if the twenty-four files exceed 8 GB |
| 7 | **SoilGrids v2** (250 m) | Soil texture and carbon by depth | a property × depth × statistic choice; `.vrt` plus tile folders | Not stated | CC-BY 4.0 | **Not yet.** Twelve properties × six depths × five statistics is a large choice with no stated size, and the region's soil is already modelled from its own rules. Buy it when the generator needs it |
| 8 | **HydroBASINS v1 / HydroRIVERS v1** | Basins and river reaches worldwide | 9 continental archives / `HydroRIVERS_v10.gdb.zip` | 618 MB global rivers; basin files' sizes not shown | **The one that fails.** Free for commercial use, but its agreement (Technical Documentation v1.4, Appendix A) allows distribution only "incorporated into … Derivative Works" under an end-user licence "at least as protective", forbids standalone distribution, keeps all improvements as WWF's intellectual property, and prescribes a long copyright notice. The site's own Terms of Use contradict the product pages by saying "personal, non-commercial use only" | **No.** Coarse drainage is derived from ETOPO instead, by the same D8 method the region already uses (`DrainageNetwork`), which costs development and owes nobody |
| 9 | **WorldClim 2.1** | A second opinion on climate | — | — | Requires prior permission for commercial use *or* redistribution | **No**, as the design already advised |

**What approving this costs:** about 2.5 GB, plus CHELSA's twenty-four files, whose size the provider does not publish
and which are measured before they are fetched. Nothing is downloaded before William names the lines he approves, and
each file's notice goes into `THIRD_PARTY_NOTICES.md` in the commit that imports it (STANDARDS 16).

**Two things to know before saying yes.** ETOPO at 30 arc-second is global context, not ground to walk on: a region
that is actually played is still baked from fine data fetched for that region, as Bherwerre is. And ETOPO's own product
page carries no licence text at all — the CC0 comes from its metadata record, so the first acquisition step records
which page the term was read from, with the date.

## Exit record

Unopened; no implementation or data-acquisition result. The acquisition manifest above is written, and on 2026-09-21
William approved it whole ("all six"): the six datasets it names, and no other, may be fetched. Fetched the same day by
`Tools/atlas/acquire.py` into `Data/global/`: 29 files, 6.4 GB, every file's size and SHA-256 in `Data/global/manifest.json`
(`--verify` recomputes them; 29 of 29 agreed), each dataset's notice in `THIRD_PARTY_NOTICES.md`. CHELSA's twenty-four came to
4.0 GB against the 8 GB ceiling; ETOPO's address is NOAA's data directory, not its THREDDS server, which answers 404;
PANGAEA's handle for GLiM closes the connection after a HEAD, and the tool asks once more on a fresh one. Promise 1 is
kept; the reading of the datasets into the atlas's derived form is WG.0b, built 2026-09-22 (`WG.0b_THE_ATLAS_READ.md`).
