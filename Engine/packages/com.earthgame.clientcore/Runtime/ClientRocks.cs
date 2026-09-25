using System;
using EarthGame.Engine;

namespace EarthGame.ClientCore
{
    /// <summary>
    /// The rocks that stand (BF.4 stage three) as a client holds the world: the engine's own rule (<see cref="StandingRocks"/>)
    /// over the codes of the tiles held and the one ground without its hollows, so the rock a founder's body is stopped by, the
    /// crosshair names and a stick is moved off is the rock the server holds. A cell whose cover, loose, stone or stand tile is
    /// not held has no rock yet.
    /// </summary>
    public static class ClientRocks
    {
        /// <summary>The rock standing on a cell, and true; false where none does, or where a tile it is decided from is not held.</summary>
        /// <param name="tiles">The tile a client holds of a layer, or null: <see cref="TileReceiver.Holding"/>.</param>
        public static bool TryOfCell(Func<TileLayer, TileId, ReceivedTile> tiles, TileGrid grid, IHeightSource undug, double cellM, int row, int col, out StandingRock rock)
        {
            rock = default;
            if (tiles == null || grid == null || undug == null || !(cellM > 0.0)) return false;
            int cover = CodeAt(tiles, grid, TileLayer.GroundCover, row, col, cellM);
            int stone = CodeAt(tiles, grid, TileLayer.Stone, row, col, cellM);
            int loose = CodeAt(tiles, grid, TileLayer.Loose, row, col, cellM);
            int stand = CodeAt(tiles, grid, TileLayer.Stand, row, col, cellM);
            if (cover < 0 || stone < 0 || loose < 0 || stand < 0) return false;
            return StandingRocks.TryDecide(row, col, (byte)cover, (byte)loose, (byte)stone, (ushort)stand, cellM, grid.ExtentM, undug, out rock);
        }

        /// <summary>The rock of the cell a point lies in, and true; false where none stands there or its tiles are not held.</summary>
        public static bool TryAt(Func<TileLayer, TileId, ReceivedTile> tiles, TileGrid grid, IHeightSource undug, double cellM, double east, double north, out StandingRock rock)
        {
            rock = default;
            if (grid == null || !(cellM > 0.0)) return false;
            TileCodec.CellOf(grid.ExtentM, cellM, east, north, out int row, out int col);
            return TryOfCell(tiles, grid, undug, cellM, row, col, out rock);
        }

        /// <summary>A code layer's code at a world cell, of either width, from the tile that draws the cell, or −1 where no such tile is held.</summary>
        private static int CodeAt(Func<TileLayer, TileId, ReceivedTile> tiles, TileGrid grid, TileLayer layer, int row, int col, double cellM)
        {
            StandLayout.CellCentre(row, col, cellM, grid.ExtentM, out double east, out double north);
            ReceivedTile tile = tiles(layer, grid.ForPosition(east, north));
            if (tile == null || !tile.HasCodes) return -1;
            int x = (int)Math.Round((east - tile.OriginEast) / tile.CellM);
            int z = (int)Math.Round((north - tile.OriginNorth) / tile.CellM);
            if (x < 0 || z < 0 || x >= tile.Posts || z >= tile.Posts) return -1;
            return tile.CodeAt(z, x);
        }
    }
}
