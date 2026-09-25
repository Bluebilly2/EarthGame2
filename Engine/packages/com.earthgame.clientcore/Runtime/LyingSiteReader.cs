using System;
using EarthGame.Engine;

namespace EarthGame.ClientCore
{
    /// <summary>
    /// A lying thing's site read from the tiles a client holds (BF.1): the stand codes of its cell and the eight round it,
    /// its cover byte and its stone, handed to the engine's own rule (<see cref="LyingSites"/>), so the words the crosshair
    /// shows for a stick on the ground are the ones the server would give it. A cell whose tile is not held reads as
    /// nothing stands there, and a stone whose tile is not held as no stone.
    /// </summary>
    public static class LyingSiteReader
    {
        /// <summary>The region's raster pitch, which every code layer is cut at; a held tile's own says the same.</summary>
        private const double RegionCellM = 4.0;

        public static LyingSite Of(TileReceiver tiles, TileGrid grid, in LyingThing thing)
        {
            if (tiles == null || grid == null) return new LyingSite(null, 0, null);
            double extent = grid.ExtentM;
            double cellM = CellSize(tiles, grid, thing.Row, thing.Col, extent);
            int StandAt(int row, int col) => CodeAt(tiles, grid, TileLayer.Stand, row, col, cellM, extent);
            int cover = CodeAt(tiles, grid, TileLayer.GroundCover, thing.Row, thing.Col, cellM, extent);
            int stoneCode = CodeAt(tiles, grid, TileLayer.Stone, thing.Row, thing.Col, cellM, extent);
            StoneType stone = stoneCode >= 1 && stoneCode <= StoneType.All.Count ? StoneType.All[stoneCode - 1] : null;
            return LyingSites.Of(thing, cellM, StandAt, (byte)Math.Max(0, cover), stone);
        }

        private static double CellSize(TileReceiver tiles, TileGrid grid, int row, int col, double extentM)
        {
            StandLayout.CellCentre(row, col, RegionCellM, extentM, out double east, out double north);
            ReceivedTile loose = tiles.Holding(TileLayer.Loose, grid.ForPosition(east, north));
            return loose != null ? loose.CellM : RegionCellM;
        }

        /// <summary>A code layer's code at a world cell, of either width, or −1 where no tile of that layer is held for it.</summary>
        public static int CodeAt(TileReceiver tiles, TileGrid grid, TileLayer layer, int row, int col, double cellM, double extentM)
        {
            StandLayout.CellCentre(row, col, cellM, extentM, out double east, out double north);
            ReceivedTile tile = tiles.Holding(layer, grid.ForPosition(east, north));
            if (tile == null || !tile.HasCodes) return -1;
            int x = (int)Math.Round((east - tile.OriginEast) / tile.CellM);
            int z = (int)Math.Round((north - tile.OriginNorth) / tile.CellM);
            if (x < 0 || z < 0 || x >= tile.Posts || z >= tile.Posts) return -1;
            return tile.CodeAt(z, x);
        }
    }
}
