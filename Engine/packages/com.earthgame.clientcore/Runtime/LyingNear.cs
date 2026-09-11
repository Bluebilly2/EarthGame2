using System;
using System.Collections.Generic;
using EarthGame.Engine;

namespace EarthGame.ClientCore
{
    /// <summary>A thing lying near a point, as the client draws it: its name, and where and how it lies.</summary>
    public struct LyingNearby
    {
        public LyingThing Thing;
        public LooseInstance Instance;
    }

    /// <summary>
    /// The things lying round a point in the loose tiles a client holds, less what has been taken (M1.5b), each placed as
    /// <see cref="StandPreparation"/> draws it, so the crosshair finds a stick where the stick is drawn.
    /// </summary>
    public static class LyingNear
    {
        /// <summary>
        /// Every thing on a cell whose centre lies within a distance of a point across the ground, added to a list. A thing
        /// lies up to half a cell from its cell's centre, so a caller that wants all within a reach asks for that much more.
        /// </summary>
        public static void Find(double east, double north, double radiusM, TileReceiver tiles, TileGrid grid, LooseTaken taken, List<LyingNearby> into)
        {
            if (tiles == null || grid == null || into == null || radiusM <= 0.0) return;
            ReceivedTile here = tiles.Holding(TileLayer.Loose, grid.ForPosition(east, north));
            if (here == null || here.Codes == null) return;
            double cell = here.CellM;
            TileCodec.CellOf(grid.ExtentM, cell, east - radiusM, north + radiusM, out int rowMin, out int colMin);
            TileCodec.CellOf(grid.ExtentM, cell, east + radiusM, north - radiusM, out int rowMax, out int colMax);
            double r2 = radiusM * radiusM;
            for (int row = rowMin; row <= rowMax; row++)
                for (int col = colMin; col <= colMax; col++)
                {
                    StandLayout.CellCentre(row, col, cell, grid.ExtentM, out double centreEast, out double centreNorth);
                    double dx = centreEast - east, dz = centreNorth - north;
                    if (dx * dx + dz * dz > r2) continue;
                    TileId id = grid.ForPosition(centreEast, centreNorth);
                    ReceivedTile loose = tiles.Holding(TileLayer.Loose, id);
                    ReceivedTile ground = tiles.Holding(TileLayer.Ground, id);
                    if (loose?.Codes == null || ground?.Heights == null || ground.Posts != loose.Posts) continue;
                    int x = (int)Math.Round((centreEast - loose.OriginEast) / cell);
                    int z = (int)Math.Round((centreNorth - loose.OriginNorth) / cell);
                    if (x < 0 || z < 0 || x >= loose.Posts || z >= loose.Posts) continue;
                    byte code = loose.Codes[z, x];
                    if (code == 0) continue;
                    Add(row, col, StandLayout.Kind.Stick, LooseCodes.SticksOf(code), ground, cell, grid.ExtentM, taken, into);
                    Add(row, col, StandLayout.Kind.Cobble, LooseCodes.CobblesOf(code), ground, cell, grid.ExtentM, taken, into);
                }
        }

        private static void Add(int row, int col, StandLayout.Kind kind, int count, ReceivedTile ground, double cellM, double extentM, LooseTaken taken, List<LyingNearby> into)
        {
            for (int k = 0; k < count; k++)
            {
                LyingThing thing = new LyingThing(row, col, kind, k);
                if (taken != null && taken.IsTaken(thing)) continue;
                into.Add(new LyingNearby { Thing = thing, Instance = StandPreparation.Lying(thing, ground, cellM, extentM) });
            }
        }
    }
}
