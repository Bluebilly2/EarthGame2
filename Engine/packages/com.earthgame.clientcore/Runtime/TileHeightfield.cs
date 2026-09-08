using System;
using System.Collections.Generic;
using EarthGame.Engine;

namespace EarthGame.ClientCore
{
    /// <summary>
    /// The ground as the client holds it: the streamed tiles, read bilinearly between their posts exactly as the
    /// server's <see cref="Heightfield"/> reads the raster the tiles were cut from, so a height asked of either
    /// side at the same point is the same number to the centimetre the wire carries. Where no tile is held the
    /// ground is unknown, and says so, rather than zero.
    /// </summary>
    public sealed class TileHeightfield : IHeightSource
    {
        private readonly TileGrid _grid;
        private readonly Dictionary<TileId, ReceivedTile> _tiles = new Dictionary<TileId, ReceivedTile>();

        public TileHeightfield(TileGrid grid)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
        }

        public TileGrid Grid => _grid;
        public int Count => _tiles.Count;

        public void Add(ReceivedTile tile)
        {
            if (tile == null) throw new ArgumentNullException(nameof(tile));
            _tiles[tile.Id] = tile;
        }

        public bool Remove(TileId id) => _tiles.Remove(id);

        public bool Holds(TileId id) => _tiles.ContainsKey(id);

        /// <summary>True when the tile under a point is held.</summary>
        public bool HasGroundAt(double east, double north) => _tiles.ContainsKey(_grid.ForPosition(east, north));

        /// <summary>The height at a point, or NaN where no tile is held.</summary>
        public double HeightAt(double east, double north)
        {
            double h;
            return TryHeightAt(east, north, out h) ? h : double.NaN;
        }

        public bool TryHeightAt(double east, double north, out double height)
        {
            ReceivedTile tile;
            if (!_tiles.TryGetValue(_grid.ForPosition(east, north), out tile))
            {
                height = double.NaN;
                return false;
            }
            int last = tile.Posts - 1;
            double fx = (east - tile.OriginEast) / tile.CellM;
            double fz = (north - tile.OriginNorth) / tile.CellM;
            if (fx < 0.0) fx = 0.0; else if (fx > last) fx = last;
            if (fz < 0.0) fz = 0.0; else if (fz > last) fz = last;
            int x0 = (int)Math.Floor(fx);
            int z0 = (int)Math.Floor(fz);
            if (x0 >= last) x0 = last - 1;
            if (z0 >= last) z0 = last - 1;
            double tx = fx - x0;
            double tz = fz - z0;
            float[,] h = tile.Heights;
            double south = h[z0, x0] + (h[z0, x0 + 1] - h[z0, x0]) * tx;
            double northRow = h[z0 + 1, x0] + (h[z0 + 1, x0 + 1] - h[z0 + 1, x0]) * tx;
            height = south + (northRow - south) * tz;
            return true;
        }
    }
}
