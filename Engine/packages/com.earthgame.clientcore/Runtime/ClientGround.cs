using System;
using System.Collections.Generic;
using EarthGame.Engine;

namespace EarthGame.ClientCore
{
    /// <summary>
    /// The ground as the client holds it (BF.4): the engine's one function (<see cref="FineGround"/>) over the ground and cover
    /// tiles the client was streamed, the hollows the world's changes tell of, and the world's seed, so a height asked of the
    /// client at a point is the server's to the rounding of a double. What the Terrain's posts are sampled from, what trees,
    /// litter and tufts stand on, and what a founder corrected under the ground is lifted onto. Where no ground tile is held
    /// the ground is unknown, and says so, rather than zero.
    ///
    /// <para>The water is not measured against it: a depth tile is the water over the raster (<see cref="StreamedWater"/>
    /// adds it to <see cref="TileHeightfield"/>, the raster as streamed), and neither is the slope a founder walks by
    /// (<see cref="TryWalkNormalAt"/>).</para>
    /// </summary>
    public sealed class ClientGround : IHeightSource
    {
        private readonly TileGrid _grid;
        private readonly Func<TileLayer, TileId, ReceivedTile> _tiles;
        private readonly Func<WorldChanges> _changes;
        private readonly Func<int, int, byte> _dug;

        /// <param name="tiles">The tile a client holds of a layer, or null: <see cref="TileReceiver.Holding"/>.</param>
        /// <param name="seed">The world's seed, as the Welcome carries it: what the relief is hashed from.</param>
        /// <param name="changes">The world's changes as the client mirrors them, for the hollows; null for none.</param>
        public ClientGround(TileGrid grid, Func<TileLayer, TileId, ReceivedTile> tiles, ulong seed, WorldChanges changes)
            : this(grid, tiles, seed, changes != null ? () => changes : (Func<WorldChanges>)null)
        {
        }

        /// <param name="changes">The world's changes as the client mirrors them now, asked each time, since a client that reconnects mirrors them anew; null for none.</param>
        public ClientGround(TileGrid grid, Func<TileLayer, TileId, ReceivedTile> tiles, ulong seed, Func<WorldChanges> changes)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _tiles = tiles ?? throw new ArgumentNullException(nameof(tiles));
            Seed = seed;
            _changes = changes;
            if (changes != null)
                _dug = (row, col) =>
                {
                    WorldChanges now = _changes();
                    return now != null ? now.GroundOf(row, col).DugCm : (byte)0;
                };
        }

        public TileGrid Grid => _grid;
        public ulong Seed { get; }

        /// <summary>
        /// A copy of what the ground of a tile and of the tiles its things spill into is read from, for a worker (BF.4): see
        /// <see cref="GroundSnapshot"/>.
        /// </summary>
        public GroundSnapshot SnapshotFor(TileId id) => new GroundSnapshot(_grid, _tiles, Seed, _changes?.Invoke(), id);

        /// <summary>The depth dug on a cell by its world row and column, as the changes hold it now; null without changes.</summary>
        public Func<int, int, byte> DugCm => _dug;

        /// <summary>The ground at a point, or NaN where no ground tile is held.</summary>
        public double HeightAt(double east, double north)
        {
            TileId id = _grid.ForPosition(east, north);
            ReceivedTile ground = _tiles(TileLayer.Ground, id);
            if (ground?.Heights == null) return double.NaN;
            return HeightAt(ground, _tiles(TileLayer.GroundCover, id), Seed, _grid.ExtentM, _dug, east, north);
        }

        /// <summary>The ground at a point as if nothing had been dug (BF.4): what a hole is measured down from; NaN where no ground tile is held.</summary>
        public double UndugAt(double east, double north)
        {
            TileId id = _grid.ForPosition(east, north);
            ReceivedTile ground = _tiles(TileLayer.Ground, id);
            if (ground?.Heights == null) return double.NaN;
            return HeightAt(ground, _tiles(TileLayer.GroundCover, id), Seed, _grid.ExtentM, null, east, north);
        }

        /// <summary>
        /// The ground at a point of one tile, from what a worker was handed (M1.4e): the tile's ground, its cover (null for none
        /// yet, and then no relief), the seed and the dug depth by cell (null for none). The tile's own posts and no neighbour's,
        /// as <see cref="TileGround"/> reads them, so a tile at the edge of what a client holds is prepared whole.
        /// </summary>
        public static double HeightAt(ReceivedTile ground, ReceivedTile cover, ulong seed, double extentM, Func<int, int, byte> dugCm, double east, double north)
        {
            QuadAt(ground, cover, extentM, dugCm, east, north, out GroundQuad q);
            return FineGround.At(q, east, north, seed);
        }

        /// <summary>The quad round a point of one tile, its corners' cover taken from the cover tile when it lies on the same posts.</summary>
        public static void QuadAt(ReceivedTile ground, ReceivedTile cover, double extentM, Func<int, int, byte> dugCm, double east, double north, out GroundQuad q)
        {
            if (ground == null) throw new ArgumentNullException(nameof(ground));
            if (ground.Heights == null) throw new ArgumentException("tile " + ground.Id + " carries no heights", nameof(ground));
            byte[,] covers = SamePosts(ground, cover) ? cover.Codes : null;
            TileCodec.CellOf(extentM, ground.CellM, ground.OriginEast, ground.OriginNorth, out int southRow, out int westCol);
            FineGround.QuadIn(ground.Heights, covers, ground.Posts, ground.CellM, ground.OriginEast, ground.OriginNorth, southRow, westCol, dugCm, east, north, out q);
        }

        /// <summary>Whether a cover tile is the ground tile's own: the same tile, on the same posts.</summary>
        public static bool SamePosts(ReceivedTile ground, ReceivedTile cover) =>
            ground != null && cover?.Codes != null && cover.Id.Equals(ground.Id) && cover.Posts == ground.Posts && Math.Abs(cover.CellM - ground.CellM) < 1e-9;

        /// <summary>
        /// The depths dug on a tile's cells, copied (BF.4): what a worker preparing the tile reads while the main thread goes on
        /// hearing of more; null where nothing is dug, which reads as undug.
        /// </summary>
        public static Func<int, int, byte> DugIn(WorldChanges changes, ReceivedTile tile, TileGrid grid)
        {
            if (changes == null || tile == null || grid == null) return null;
            Dictionary<long, byte> copy = null;
            foreach (CellChange cell in changes.Cells())
            {
                if (cell.DugCm == 0 || !StandPreparation.Covers(tile, grid, cell.Row, cell.Col)) continue;
                if (copy == null) copy = new Dictionary<long, byte>();
                copy[LooseTaken.Key(cell.Row, cell.Col)] = cell.DugCm;
            }
            if (copy == null) return null;
            return (row, col) => copy.TryGetValue(LooseTaken.Key(row, col), out byte cm) ? cm : (byte)0;
        }

        /// <summary>The ground at a point from the one ground where its tile is held, else from one tile's own posts (a worker's fallback).</summary>
        public double HeightAtOr(ReceivedTile tile, double east, double north)
        {
            double h = HeightAt(east, north);
            return double.IsNaN(h) ? TileGround.HeightAt(tile, east, north) : h;
        }

        /// <summary>
        /// The normal a founder's walk is judged by at a point (BF.4), and true, where its ground tile is held; false where it is
        /// not, and the caller keeps what it had. It is the raster's slope as the Terrain's posts, <paramref name="spacingM"/>
        /// apart, sample it, the relief left out: what the walk was judged by until BF.4, when the Terrain was the raster
        /// sampled. The relief moves the feet and the eye; it does not make ground the data calls walkable slide, nor speed or
        /// slow the pace. The raster's own bilinear slope was tried first and walked the dune scenario's founder into a slide at
        /// the dune's foot: read within one 4 m cell it is 37° where a square of 1.95 m reads 32°, a saddle's steepest corner.
        /// </summary>
        public bool TryWalkNormalAt(double east, double north, double spacingM, out Double3 normal)
        {
            ReceivedTile ground = _tiles(TileLayer.Ground, _grid.ForPosition(east, north));
            if (ground?.Heights == null)
            {
                normal = Double3.Up;
                return false;
            }
            normal = WalkNormalAt(ground, east, north, spacingM);
            return true;
        }

        /// <summary>
        /// The walk's normal at a point of one tile (<see cref="TryWalkNormalAt"/>): the gradient, at the point, of the square
        /// of posts <paramref name="spacingM"/> apart from the tile's corner round it, each post the tile's raster there.
        /// </summary>
        public static Double3 WalkNormalAt(ReceivedTile ground, double east, double north, double spacingM)
        {
            if (ground?.Heights == null) throw new ArgumentException("no ground to walk on", nameof(ground));
            if (!(spacingM > 0.0)) throw new ArgumentOutOfRangeException(nameof(spacingM));
            int squares = Math.Max(1, (int)Math.Round((ground.Posts - 1) * ground.CellM / spacingM));
            double fx = (east - ground.OriginEast) / spacingM, fz = (north - ground.OriginNorth) / spacingM;
            if (fx < 0.0) fx = 0.0; else if (fx > squares) fx = squares;
            if (fz < 0.0) fz = 0.0; else if (fz > squares) fz = squares;
            int x0 = Math.Min(squares - 1, (int)Math.Floor(fx)), z0 = Math.Min(squares - 1, (int)Math.Floor(fz));
            double tx = fx - x0, tz = fz - z0;
            double e0 = ground.OriginEast + x0 * spacingM, n0 = ground.OriginNorth + z0 * spacingM;
            double h00 = TileGround.HeightAt(ground, e0, n0), h10 = TileGround.HeightAt(ground, e0 + spacingM, n0);
            double h01 = TileGround.HeightAt(ground, e0, n0 + spacingM), h11 = TileGround.HeightAt(ground, e0 + spacingM, n0 + spacingM);
            double dEast = ((h10 - h00) * (1.0 - tz) + (h11 - h01) * tz) / spacingM;
            double dNorth = ((h01 - h00) * (1.0 - tx) + (h11 - h10) * tx) / spacingM;
            return new Double3(-dEast, 1.0, -dNorth).Normalized;
        }
    }

    /// <summary>
    /// The one ground over a tile and the tiles west, south and south-west of it, copied for a worker (BF.4): the ground and
    /// cover tiles held (a received tile is never changed once held, so the references are the copy), the depths dug on their
    /// cells, and the seed. A tile's things lie up to half a cell from their cells' centres, so the things of its west and
    /// south edges lie on those tiles' ground; read from the tile alone they sat on its edge carried on, up to a tenth of a
    /// metre off where the cover changes at the edge. A point on a tile the snapshot does not hold is read from the tile's own
    /// posts carried on, as before.
    /// </summary>
    public sealed class GroundSnapshot : IHeightSource
    {
        private readonly TileGrid _grid;
        private readonly ulong _seed;
        private readonly TileId[] _ids = new TileId[4];
        private readonly ReceivedTile[] _ground = new ReceivedTile[4];
        private readonly ReceivedTile[] _cover = new ReceivedTile[4];
        private readonly Func<int, int, byte> _dug;

        internal GroundSnapshot(TileGrid grid, Func<TileLayer, TileId, ReceivedTile> tiles, ulong seed, WorldChanges changes, TileId own)
        {
            _grid = grid;
            _seed = seed;
            Own = own;
            _ids[0] = own;
            _ids[1] = new TileId(own.Ix - 1, own.Iz);
            _ids[2] = new TileId(own.Ix, own.Iz - 1);
            _ids[3] = new TileId(own.Ix - 1, own.Iz - 1);
            uint crc = 2166136261u;
            Dictionary<long, byte> dug = null;
            List<CellChange> cells = changes?.Cells();
            for (int i = 0; i < 4; i++)
            {
                if (!grid.Contains(_ids[i])) continue;
                _ground[i] = tiles(TileLayer.Ground, _ids[i]);
                if (_ground[i]?.Heights == null)
                {
                    _ground[i] = null;
                    continue;
                }
                ReceivedTile cover = tiles(TileLayer.GroundCover, _ids[i]);
                _cover[i] = ClientGround.SamePosts(_ground[i], cover) ? cover : null;
                crc = (crc ^ _ground[i].Crc32) * 16777619u;
                crc = (crc ^ (_cover[i] != null ? _cover[i].Crc32 : 0u) ^ (uint)(i + 1)) * 16777619u;
                if (cells == null) continue;
                foreach (CellChange cell in cells)
                {
                    if (cell.DugCm == 0 || !StandPreparation.Covers(_ground[i], grid, cell.Row, cell.Col)) continue;
                    if (dug == null) dug = new Dictionary<long, byte>();
                    dug[LooseTaken.Key(cell.Row, cell.Col)] = cell.DugCm;
                    crc = (crc ^ (uint)cell.Row ^ ((uint)cell.Col << 16) ^ ((uint)cell.DugCm << 8)) * 16777619u;
                }
            }
            if (dug != null) _dug = (row, col) => dug.TryGetValue(LooseTaken.Key(row, col), out byte cm) ? cm : (byte)0;
            Crc = crc;
        }

        /// <summary>The tile the snapshot is for.</summary>
        public TileId Own { get; }

        /// <summary>A checksum of everything it was copied from: the tiles held and their covers, and the hollows; a snapshot of the same checksum reads the same ground.</summary>
        public uint Crc { get; }

        /// <summary>The ground at a point, from the tile of the four the point lies on, or the own tile's posts carried on; NaN if even the own tile's ground is not held.</summary>
        public double HeightAt(double east, double north)
        {
            TileId id = _grid.ForPosition(east, north);
            for (int i = 0; i < 4; i++)
                if (_ground[i] != null && _ids[i].Equals(id))
                    return ClientGround.HeightAt(_ground[i], _cover[i], _seed, _grid.ExtentM, _dug, east, north);
            return _ground[0] != null ? ClientGround.HeightAt(_ground[0], _cover[0], _seed, _grid.ExtentM, _dug, east, north) : double.NaN;
        }
    }
}
