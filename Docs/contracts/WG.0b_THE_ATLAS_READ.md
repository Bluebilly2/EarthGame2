# WG.0b — The atlas read into its derived form

**Status:** drafted 2026-09-22 on William's "continue" and opened the same day on his "continue with WG.0b", as WG.0's
second promise (the first, the six datasets fetched, is kept). Owner: Claude (ruling 26 as amended). Built; the exit record
below.

**The one sentence:** the raw datasets are read once, offline, into small layers of the atlas's own, and only those travel.

## What was found before it was designed (2026-09-22)

- **The formats.** ETOPO 2022 (43200 × 21600, 32-bit floats, metres) and CHELSA (43200 × 20880, 16-bit, twelve months of
  rain and twelve of temperature) are classic GeoTIFFs compressed with DEFLATE and a predictor, ETOPO tiled at 256 and CHELSA
  in strips: readable with `zlib` and numpy alone, and nothing the tools' Python lacks. GLiM is an ASCII grid at half a
  degree with a class list. RESOLVE Ecoregions and Natural Earth's physical themes are shapefiles, readable with `struct`.
  HydroLAKES is a File Geodatabase, which nothing short of GDAL reads; Natural Earth's lakes serve the choosing scale, and
  HydroLAKES waits for a reader or for a need at the region's scale.
- **The one derived layer that exists**, `Data/global/EarthElevation` (4096 × 2048 int16 from AWS terrain tiles, 2026-08),
  is the shape to follow: a raw array beside a JSON header that says its grid, its unit and its source.
- **What the game asks of a chosen coordinate** (GAME_DESIGN, WG.0): the relief and the sea floor, the year's rain and
  warmth month by month, the rock underfoot, the ecological region, and where the coast and the lakes are.

## Promises (a draft)

1. **A reader of the project's own** for DEFLATE GeoTIFFs (tiles and strips, the two predictors, the geo tags that place a
   pixel) and for shapefiles (polygons and their attributes), in `Tools/atlas/`, tested on a synthetic file each.
2. **The layers, at a twelfth of a degree** (4320 × 2160, about 9 km at the equator, which the sources' 1/120 degree
   divides evenly), each an array beside its header: elevation (ETOPO's mean over the cell, metres, int16); lithology
   class (GLiM, uint8, the class list in the header); ecoregion (RESOLVE, uint16, the names in a table beside it);
   distance to the coast and to the nearest lake (Natural Earth, uint16 km). Rain and temperature for each month
   (CHELSA, twelve and twelve, int16 in tenths of a millimetre and of a degree) at a sixth of a degree, since
   twenty-four layers at a twelfth would be 450 MB against the budget below and a choice of place needs no finer.
3. **A verifier** independent of the reader: `atlas_check.py` reads a handful of places by other means — Point
   Perpendicular's rain and temperature against the lighthouse's own record in `Data/stations` (M1.8c), the sea floor off
   the coast against a charted sounding, a named lake and a named desert — and prints the two numbers beside each verdict.
4. **The bake reads the atlas, not the raw files**, once the layers exist, and the raw files can be deleted without loss
   to the game; the notices in `THIRD_PARTY_NOTICES.md` say what each derived layer is derived from.
5. **A budget:** the six layers together under 400 MB on disk, and the derivation runs offline in under an hour.

## Non-goals

HydroLAKES (a reader or a need at the region's scale first); anything finer than a twentieth of a degree (the region's
own bake keeps its metres); any new download.

## How it is proved

The readers on synthetic files with known contents; the verifier's places; the sizes and the run's time in the exit
record; the notices in the same commit as the layers.

## Exit record (2026-09-22)

**What exists.** `Tools/atlas/readers.py`: a classic-GeoTIFF reader (DEFLATE, the horizontal and the floating-point
predictors, tiles and strips, the geo tags), a shapefile and .dbf reader with a scanline rasteriser that keeps holes as
holes, and an ASCII grid reader; its `--self-test` writes each format from known numbers and reads it back, 11 checks.
`Tools/atlas/derive.py`: 29 layers under `Data/global/atlas/` (elevation, lithology, ecoregion, coast_km and lake_km at a
twelfth of a degree; pr_01..12 and tas_01..12 at a sixth), each a raw array beside an `eg2.atlas` header (ARCHITECTURE
§10) naming its source file and checksum, with `ecoregion_names.json` beside the ecoregion. `Tools/verifiers/checks/
atlas_check.py`: fourteen places and facts by other means.

**Tested, and how.**

- The readers' self-test: 11 checks. The floating-point predictor first undid its differencing plane by plane, which the
  self-test could not see because the writer made the same mistake; the real ETOPO rows showed a maximum of 8192 m and a
  minimum of 12 km, and the rule (the differencing runs across the whole row of rearranged bytes, TIFF Technical Note 3)
  was put right in both. The rasteriser first filled each ring on its own, so a hole was filled as a polygon.
- The derivation on the six datasets: 234 s in all (the elevation 32, the climate 194, the ecoregions 6, the distances 2),
  188 MB on disk against the 400 MB budget, the raw files untouched.
- `atlas_check.py`: 14 of 14 GREEN. Point Perpendicular's year of rain 1238 mm against the Bureau's 1242 (all years
  1899-2004 against CHELSA's 1981-2010), the wettest month May both ways, January's mean 21.1 °C between the Bureau's
  17.5 and 23.8 and July's 12.5 between 9.2 and 15.1; Kosciuszko's highest cell within 20 km 1917 m against the summit's
  2228; Lake Eyre -15 m; Nowra's cell 91 m against the station's 109; the Nullarbor carbonate; Nowra in the Eastern
  Australian temperate forests; the mid-Pacific in none; the lighthouse 9 km from the coast and Alice Springs 925;
  Lake Eyre a lake and Alice Springs 173 km from one. Two first checks were wrong about the country, not the atlas: a
  9 km cell at a headland's tip is mostly sea (-45 m, no ecoregion), so the land checks moved inland to Nowra.

**Open (DEBTS):** HydroLAKES, fetched and unread; the bake and the choosing of a place reading these layers is WG.0's
own next step, not this slice's.
