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
