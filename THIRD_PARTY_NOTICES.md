# Third-party notices

Every file in this repository that was not written for EarthGame2 is listed here with its licence, and every
dataset with its attribution. A new vendored file, asset or dataset lands with its entry in the same commit.

## LiteNetLib 2.1.4 — MIT

Vendored verbatim under `Engine/packages/com.earthgame.transport/Runtime/LiteNetLib/` and compiled from those
files by both Unity and dotnet (one copy in the repository). Source: https://github.com/RevenantX/LiteNetLib

```
MIT License

Copyright (c) 2025 Ruslan Pyrch

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

## AWS Terrain Tiles (Terrarium) — open data

Elevation data used to bake the global grid (`Data/global/EarthElevation.r16`, zoom 5) and each region's rasters
(zoom 14 for the 8 km box, zoom 11 for the 64 km surround): Bherwerre's since 2026-09-08 and the Kangaroo Valley's
since 2026-09-22 (36 tiles at zoom 14, x 15038–15043 y 9872–9877, and 25 at zoom 11, x 1878–1882 y 1232–1236, fetched
by `Tools/data/fetch_tiles.py` on William's approval of that manifest, `Docs/contracts/WG.2_A_SECOND_PLACE.md`); and the
whole valley's since 2026-09-23 (CANON ruling 45: 306 tiles at zoom 14 for its 32 km box and a kilometre, x 15037–15053
y 9869–9886, 270 of them fetched, 25.9 MB, and 81 at zoom 11 for its 128 km surround, x 1876–1884 y 1230–1238, 42 fetched,
1.5 MB, on his yes to `Docs/contracts/WG.2b_THE_WHOLE_VALLEY.md`'s manifest). Under the valley the tiles carry SRTM at
30 m, with voids the bake despikes. Required attribution, reproduced from the
dataset's terms:

> Elevation data: AWS Terrain Tiles, derived from NASA SRTM, USGS GMTED2010/NED, NOAA ETOPO1, GEBCO, Natural
> Resources Canada, Geoscience Australia, LINZ and Kartverket.

Source: https://registry.opendata.aws/terrain-tiles/ — see `Docs/v1/DATA_SOURCES.md` for the decode and the
reasons this source was chosen.

## Unity packages

Unity Engine and the packages named in `Unity/Packages/manifest.json` are used under the Unity Personal licence and
their respective package licences (`Unity/Library/PackageCache/*/LICENSE.md`); none is redistributed here.

## Meshes, textures and audio

None yet. Each sourced asset gets an entry here (name, author, licence, URL) in the commit that adds it.

## OpenStreetMap data — ODbL 1.0

The outlines of each region's water bodies (lakes, waterholes, swamps) are fetched from OpenStreetMap through the
Overpass API by `Tools/data/bake_water.py` and rasterised to `Data/regions/<region>/water_bodies.u8` (a dataset
under `Data/`, fetched and never committed; the Overpass response is cached under `Data/cache/osm/`): Bherwerre's
on 2026-09-09, the Kangaroo Valley's on 2026-09-22 and the whole valley's on 2026-09-23 (each one query, the box padded
by a kilometre; the whole valley's 195 elements, 273 KB). The sidecar names
every way used and, since 2026-09-22, every way left out as humanity's (reservoirs and ponds). Data (c) OpenStreetMap contributors, made available under the Open Database
Licence 1.0: https://www.openstreetmap.org/copyright and https://opendatacommons.org/licenses/odbl/1-0/. Any
world folder that carries layers derived from it carries this attribution in the layer's sidecar.

## Bureau of Meteorology climate statistics — CC BY 3.0 AU

`Data/stations/068034_point_perpendicular_lighthouse_all_years.txt` and
`Data/stations/068072_nowra_ran_air_station_aws_all_years.txt` are the text of the Bureau of Meteorology's "Climate
statistics for Australian locations" pages (product IDCJCM0037) for the two stations, all years of record, copied from
http://www.bom.gov.au/climate/averages/tables/cw_068034_All.shtml and
http://www.bom.gov.au/climate/averages/tables/cw_068072_All.shtml in the owner's browser on 2026-09-16. © Commonwealth
of Australia 2026, Bureau of Meteorology. Reproduced under the Creative Commons Attribution 3.0 Australia licence the
Bureau's copyright notice (http://www.bom.gov.au/other/copyright.shtml) applies to material on its website unless a
product states otherwise; the monthly figures `Climate` carries as constants are derived from them and carry the same
attribution in their doc comments. Nothing in a world or a build reproduces the tables.

## Atlas of Living Australia occurrence records — each record under its resource's licence

Where each region's plants have been recorded is fetched from the Atlas of Living Australia's occurrence web
service (biocache, https://biocache-ws.ala.org.au/ws/) by `Tools/data/fetch_ala.py` and cached under
`Data/cache/ala/` (a dataset under `Data/`, fetched and never committed, and never shipped): Bherwerre's twelve
plants on 2026-09-10, flat under that folder, and the Kangaroo Valley's sixteen (the coast's twelve, to confirm which
are absent, and Sydney blue gum, river oak, cabbage tree palm, silvertop ash and scribbly gum) on 2026-09-22 under
`Data/cache/ala/kangaroo-valley/`, and the same sixteen over the whole valley's 32 km box on 2026-09-23 under
`Data/cache/ala/kangaroo-valley-whole/` (3,029 records, 0.64 MB). It is read only by
`Tools/verifiers/checks/species_check.py`, to check the world against it; nothing in a world or a build is derived
from it. Every record keeps the licence and the name of the data resource it came from beside it in the cache: at
the fetch of 2026-09-10 most were CC-BY 4.0, others CC-BY-NC 4.0, CC-BY 3.0 AU, CC0 or CC-BY-SA, and some
resources state none. Attribution: the Atlas of Living Australia (https://www.ala.org.au/) and the data providers
named in each record's `resource` field.

## NSW Seamless Geology, rock unit polygons — CC BY 4.0

The rock units under each region are fetched from the Geological Survey of NSW's public map service (WFS 2.0, layer
`geology:rock_units_nsw`, https://gs-seamless.geoscience.nsw.gov.au/geoserver/ows) by `Tools/data/fetch_geology.py`
and cached under `Data/cache/nsw-seamless-geology/` (a dataset under `Data/`, fetched and never committed, and never
shipped), on 2026-09-25 on William's yes to the two files, whose sizes and SHA-256s `manifest.json` keeps beside them:

- `bherwerre-rock-units.geojson`: 142 units touching Bherwerre's box, 929,233 bytes, SHA-256
  `adbc6aa108f42b8c49b381450b3688102f63a382bee775104382ba7d350de300`
- `kangaroo-valley-whole-rock-units.geojson`: 803 units touching the whole Kangaroo Valley's box, 10,309,468 bytes,
  SHA-256 `4573845380231a86fea818123ae270b90d42f6d724491a1a926c6a6dde16029d`

`Tools/data/bake_geology.py` fills them onto a region's grid as `Data/regions/<region>/geology.u8` with a legend (never
committed either). Nothing in a world or a build is derived from them yet; once BF.4's promise 6 is built, a world's
stone code is. Licence: Creative Commons Attribution 4.0 (https://creativecommons.org/licenses/by/4.0/). Attribution,
as the publisher asks:

> (c) State Government of NSW and Department of Primary Industries and Regional Development (DPIRD) 2025, NSW
> Seamless Geology, accessed from The Sharing and Enabling Environmental Data Portal
> [https://datasets.seed.nsw.gov.au/dataset/32ce9b05-0a22-4741-b292-64bcef50770f]

## The coordinate atlas's datasets (WG.0), fetched by `Tools/atlas/acquire.py` — each under its own licence

Six global datasets, approved whole by William on 2026-09-21 ("all six") from the acquisition manifest in
`Docs/contracts/WG.0_COORDINATE_ATLAS.md`, fetched into `Data/global/<dataset>/` (never committed) with each file's size
and SHA-256 recorded in `Data/global/manifest.json`. The raw datasets are never shipped: they are read once, offline,
into the atlas's own derived form, and only that derived form travels with the game. The licence pages were read on
2026-09-20; ETOPO's product page carries no licence text, so its term is the one its metadata record states.

### Natural Earth 1:10m physical, v5.1.1 — Public domain

1 file(s), 50.0 MB, under `Data/global/naturalearth/`. Licence page: https://www.naturalearthdata.com/about/terms-of-use/. Citation: Made with Natural Earth. Free vector and raster map data @ naturalearthdata.com.

- `10m_physical.zip` (50.0 MB, SHA-256 `a79cc39162f29832b567de5e24e8770f04a0b997eefd8d067ae4c9df40d21d2a`) from https://naciscdn.org/naturalearth/10m/physical/10m_physical.zip

### NOAA ETOPO 2022, 30 arc-second surface — CC0 (the metadata record; the product page states none)

1 file(s), 1.5 GB, under `Data/global/etopo/`. Licence page: https://www.ncei.noaa.gov/metadata/geoportal/rest/metadata/item/gov.noaa.ngdc.mgg.dem%3Aetopo_2022/html. Citation: NOAA National Centers for Environmental Information. 2022: ETOPO 2022 15 Arc-Second Global Relief Model. NOAA NCEI. DOI: 10.25921/fd45-gt74.

- `ETOPO_2022_v1_30s_N90W180_surface.tif` (1.5 GB, SHA-256 `8630abc401cc6bdd30b507a68d3eb9eda5b65f5636f7199e4b1eefd476b5a9e2`) from https://www.ngdc.noaa.gov/mgg/global/relief/ETOPO2022/data/30s/30s_surface_elev_gtif/ETOPO_2022_v1_30s_N90W180_surface.tif

### HydroLAKES v1.0 — CC-BY 4.0

1 file(s), 727.2 MB, under `Data/global/hydrolakes/`. Licence page: https://www.hydrosheds.org/products/hydrolakes. Citation: Messager, M.L., Lehner, B., Grill, G., Nedeva, I., Schmitt, O. (2016): Estimating the volume and age of water stored in global lakes using a geo-statistical approach. Nature Communications 7: 13603. doi:10.1038/ncomms13603.

- `HydroLAKES_polys_v10.gdb.zip` (727.2 MB, SHA-256 `1c1303a4882c597b769f4a2beae6c72804c52ad418a0b4078817cf1062116643`) from https://data.hydrosheds.org/file/hydrolakes/HydroLAKES_polys_v10.gdb.zip

### RESOLVE Ecoregions 2017 — CC-BY 4.0

1 file(s), 142.3 MB, under `Data/global/ecoregions/`. Licence page: https://ecoregions.appspot.com/. Citation: Dinerstein, E. et al. (2017): An Ecoregion-Based Approach to Protecting Half the Terrestrial Realm. BioScience 67(6): 534-545. doi:10.1093/biosci/bix014.

- `Ecoregions2017.zip` (142.3 MB, SHA-256 `be36d6209e443038d02e309f0447c6e7f2a62f5fe60c605ffe90d064952f2a60`) from https://storage.googleapis.com/teow2016/Ecoregions2017.zip

### GLiM v1.0, PANGAEA gridded release (0.5 degree) — CC-BY 3.0

1 file(s), 37.8 kB, under `Data/global/glim/`. Licence page: https://doi.org/10.1594/PANGAEA.788537. Citation: Hartmann, J., Moosdorf, N. (2012): The new global lithological map database GLiM: A representation of rock properties at the Earth surface. Geochemistry, Geophysics, Geosystems 13: Q12004. doi:10.1029/2012GC004370.

- `glim_gridded_0point5deg.zip` (37.8 kB, SHA-256 `43b4ce3276b155d804db8ff9fb227d620b4c35015a4cf564eac4d06d2b69d88e`) from https://hdl.handle.net/10013/epic.39939.d001

### CHELSA v2.1 climatologies 1981-2010, tas and pr, monthly — CC0

24 file(s), 4.0 GB, under `Data/global/chelsa/`. Licence page: https://chelsa-climate.org/downloads/. Citation: Karger, D.N. et al. (2017): Climatologies at high resolution for the earth's land surface areas. Scientific Data 4: 170122. doi:10.1038/sdata.2017.122. Version 2.1: doi:10.16904/envidat.228.v2.1.

- `CHELSA_tas_01_1981-2010_V.2.1.tif` … `CHELSA_pr_12_1981-2010_V.2.1.tif` (24 files from https://os.zhdk.cloud.switch.ch/chelsav2/GLOBAL/climatologies/1981-2010/, each SHA-256 in `manifest.json`)

## The atlas's derived layers (WG.0b, 2026-09-22)

`Data/global/atlas/` holds layers derived by `Tools/atlas/derive.py` from the six datasets above and from nothing else: the
elevation from NOAA ETOPO 2022 (public domain); the monthly rain and temperature from CHELSA V2.1 (CC0/CC-BY as the dataset's
page states); the lithology from GLiM (CC-BY); the ecoregion from RESOLVE Ecoregions 2017 (CC-BY 4.0); the distances to the
coast and to lakes from Natural Earth (public domain). Each layer's header names the source file and its SHA-256 from
`Data/global/manifest.json`. What travels with the game is these derived layers, never the raw files; the credits above
stand for them as for the datasets they are derived from.
