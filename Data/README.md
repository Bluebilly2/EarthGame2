# Data/

Fetched inputs for world creation. Nothing here is committed except `fixtures/` (small files the tests read) and
this file; everything else is produced by the tools under `Tools/data/` and can be regenerated.

- `global/EarthElevation.r16` + `.json` — the 4096×2048 int16 global grid baked by v1 from AWS Terrain Tiles at
  zoom 5 (9.8 km/px). Used for terrain-class and maritime context, never for the ground the founder walks on.
  Copied from the predecessor on 2026-09-07; regenerate with `Tools/data/fetch_tiles.py --global`.
- `regions/<id>/` — the region rasters (`heights.r32` and its sidecar, later every baked layer) produced by
  `Tools/data/bake_region.py`. The first region is `bherwerre` (§4.2 of the plan).
- `cache/` — raw tiles as downloaded, so a re-bake needs no network.

A world folder embeds what it derived from these inputs; a client never reads `Data/`. Attribution for every
source is in `THIRD_PARTY_NOTICES.md`.
