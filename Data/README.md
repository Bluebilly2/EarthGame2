# Data/

Fetched inputs for world creation. Nothing here is committed except `fixtures/` (small files the tests read) and
this file; everything else is produced by the tools under `Tools/data/` and `Tools/atlas/` and can be regenerated.

- `global/EarthElevation.r16` + `.json` — the 4096×2048 int16 global grid baked by v1 from AWS Terrain Tiles at
  zoom 5 (9.8 km/px). Used for terrain-class and maritime context, never for the ground the founder walks on.
  Copied from the predecessor on 2026-09-07; regenerate with `Tools/data/fetch_tiles.py --global`.
- `global/<dataset>/` — the coordinate atlas's six datasets (WG.0's manifest, approved by William on 2026-09-21):
  Natural Earth 10m physical, ETOPO 2022 at 30 arc-seconds, HydroLAKES, RESOLVE Ecoregions 2017, GLiM's gridded
  release and CHELSA v2.1's monthly temperature and rain, fetched by `Tools/atlas/acquire.py` with every file's size
  and SHA-256 in `global/manifest.json`; `--verify` recomputes them. Read once, offline, into the atlas's derived form;
  never shipped.
- `regions/<id>/` — the region rasters (`heights.r32` and its sidecar, later every baked layer) produced by
  `Tools/data/bake_region.py`. The first region is `bherwerre` (§4.2 of the plan).
- `cache/` — raw tiles as downloaded, so a re-bake needs no network.
- `stations/` — committed, the exception: the Bureau of Meteorology's climate statistics for the two stations the
  region's weather stands on, Point Perpendicular Lighthouse (068034) and Nowra RAN Air Station AWS (068072), all
  years of record, copied as text from the Bureau's pages in William's browser on 2026-09-16 (the Bureau serves
  browsers and refuses scripts). `Climate`'s station record, `WeatherTests` and `weather_check.py` each restate the
  numbers they use from these files, which are the source; nothing reads the files at run time.

A world folder embeds what it derived from these inputs; a client never reads `Data/`. Attribution for every
source is in `THIRD_PARTY_NOTICES.md`.
