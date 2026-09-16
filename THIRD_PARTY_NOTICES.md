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

Elevation data used to bake the global grid (`Data/global/EarthElevation.r16`, zoom 5) and the region rasters
(zoom 14–15). Required attribution, reproduced from the dataset's terms:

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

The outlines of the region's water bodies (lakes, waterholes, swamps) are fetched from OpenStreetMap through the
Overpass API by `Tools/data/bake_water.py` and rasterised to `Data/regions/<region>/water_bodies.u8` (a dataset
under `Data/`, fetched and never committed; the Overpass response is cached under `Data/cache/osm/`). The
sidecar names every way used. Data (c) OpenStreetMap contributors, made available under the Open Database
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

Where Bherwerre's twelve plants have been recorded is fetched from the Atlas of Living Australia's occurrence web
service (biocache, https://biocache-ws.ala.org.au/ws/) by `Tools/data/fetch_ala.py` and cached under
`Data/cache/ala/` (a dataset under `Data/`, fetched and never committed, and never shipped). It is read only by
`Tools/verifiers/checks/species_check.py`, to check the world against it; nothing in a world or a build is derived
from it. Every record keeps the licence and the name of the data resource it came from beside it in the cache: at
the fetch of 2026-09-10 most were CC-BY 4.0, others CC-BY-NC 4.0, CC-BY 3.0 AU, CC0 or CC-BY-SA, and some
resources state none. Attribution: the Atlas of Living Australia (https://www.ala.org.au/) and the data providers
named in each record's `resource` field.
