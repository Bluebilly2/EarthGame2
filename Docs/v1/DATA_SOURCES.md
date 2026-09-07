# Data Sources and Attribution

Every real-world dataset the game uses, with its licence and attribution obligations. Raw data and
baked artifacts are **not committed** (Phase 0.1 data policy): they are fetched on demand into
`Assets/StreamingAssets/` (baked) and `../EarthGameData/` (raw cache), both gitignored.

Add a row here **before** a slice starts depending on a dataset. If a source's licence forbids
redistribution, that constraint must reach `Docs/DEVELOPMENT.md` too, so nobody ships it by
accident in a build.

---

## Elevation — AWS Terrain Tiles (slice 1.1)

| | |
|---|---|
| **Used by** | `Assets/EarthGame/Editor/ElevationDataTool.cs` → `Assets/StreamingAssets/EarthElevation.r16` |
| **Endpoint** | `https://s3.amazonaws.com/elevation-tiles-prod/terrarium/{z}/{x}/{y}.png` |
| **Format** | Terrarium-encoded PNG: `elevation_m = (R × 256 + G + B ÷ 256) − 32768` |
| **Projection** | Web Mercator (EPSG:3857), XYZ tiles; valid to ±85.0511° |
| **Zoom used** | z5 (1024 tiles) baked to a 4096×2048 equirectangular int16 grid |
| **Licence** | Open data (AWS Registry of Open Data). Attribution required for the underlying sources below. |

Underlying sources, per the dataset's own attribution guidance:

- **SRTM** — NASA Shuttle Radar Topography Mission
- **GMTED2010** — USGS / NGA Global Multi-resolution Terrain Elevation Data
- **ETOPO1** — NOAA National Centers for Environmental Information
- **NED / 3DEP** — USGS National Elevation Dataset
- **GEBCO** — General Bathymetric Chart of the Oceans (bathymetry)
- **Natural Resources Canada, Geoscience Australia, LINZ (New Zealand), Kartverket (Norway)** —
  national elevation programmes contributing regional coverage

**Attribution string** to carry in any shipped build's credits:

> Elevation data: AWS Terrain Tiles, derived from NASA SRTM, USGS GMTED2010/NED, NOAA ETOPO1,
> GEBCO, Natural Resources Canada, Geoscience Australia, LINZ and Kartverket.

**Why this and not ETOPO 2022 directly.** ETOPO 2022 (NOAA NCEI) is the more authoritative single
product and remains the right long-term source for the global base. It was rejected for slice 1.1
because: NCEI was returning HTTP 503 throughout development; its GeoTIFF/netCDF formats would need
a parser or a Python/GDAL toolchain the project does not otherwise require; and it is not tiled,
so it fits neither the quadtree nor incremental fetching. Terrarium PNG needs no parser (Unity
decodes PNG natively), tiles like our terrain, and carries bathymetry. Revisit at slice 1.3, where
higher-resolution regional DEMs enter and a proper raster pipeline may become worth its weight.

---

## Planned (not yet used)

Listed so licence surprises are found before a slice depends on them.

| Dataset | Planned slice | Licence note |
|---|---|---|
| Copernicus DEM GLO-30 (30 m) | 1.3 | Free; attribution to ESA/Copernicus required |
| WorldClim 2 / ERA5 climatology | 3.1 | WorldClim: free for academic and general use; ERA5: Copernicus licence |
| HydroSHEDS / HydroRIVERS / HydroLAKES | 3.2 | Free; attribution to WWF/USGS required |
| Köppen–Geiger published classification map | 3.3 | Used as a **validation reference**, not shipped data |
| Bright-star catalogue (Yale BSC or Hipparcos subset) | 3.5 | Public domain / freely redistributable |
| USGS MRDS and geological maps | 4.1 | US Government work, public domain |
