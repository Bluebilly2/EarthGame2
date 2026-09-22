# WG.0b — The atlas read into its derived form

**Status:** drafted 2026-09-22 on William's "continue", as WG.0's second promise (the first, the six datasets fetched, is
kept). Owner: Claude (ruling 26 as amended). Not opened: the draft names what the derived form is and how the six datasets
are read, and starts on his word.

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
2. **Six layers at a twentieth of a degree** (4320 × 2160, about 5 km at the equator), each an array beside its header:
   elevation (ETOPO's mean over the cell, metres, int16); rain and temperature for each month (CHELSA, twelve and twelve,
   int16 in the dataset's units); lithology class (GLiM, uint8, the class list in the header); ecoregion (RESOLVE,
   uint16, the names in a table beside it); distance to the coast and to the nearest lake (Natural Earth, uint16 km).
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

## Exit record

Drafted; not yet opened.
