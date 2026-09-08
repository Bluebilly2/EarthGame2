using System;
using System.Collections.Generic;

namespace EarthGame.Engine
{
    /// <summary>One tile of the region's fixed grid, named by its column (east) and row (north) index.</summary>
    public readonly struct TileId : IEquatable<TileId>
    {
        public readonly int Ix;
        public readonly int Iz;

        public TileId(int ix, int iz)
        {
            Ix = ix;
            Iz = iz;
        }

        public bool Equals(TileId other) => Ix == other.Ix && Iz == other.Iz;
        public override bool Equals(object obj) => obj is TileId other && Equals(other);
        public override int GetHashCode() => (Ix * 397) ^ Iz;
        public override string ToString() => Ix + "_" + Iz;
        public static bool operator ==(TileId a, TileId b) => a.Equals(b);
        public static bool operator !=(TileId a, TileId b) => !a.Equals(b);
    }

    /// <summary>
    /// The region's fixed grid of layer tiles (ARCHITECTURE §8: kilometre tiles, the 3 × 3 around each player at
    /// full detail). Tile (ix, iz) has its south-west corner at east = −extent/2 + ix·size, north = −extent/2 +
    /// iz·size, so tile (0, 0) is the region's south-west corner and the grid is extent / size on a side. The
    /// grid is defined by the region alone; the server that streams a tile and the client that asks for it
    /// agree on which ground a name means without a message saying so.
    /// </summary>
    public sealed class TileGrid
    {
        public const double DefaultTileSizeM = 1000.0;

        /// <summary>
        /// The tile size a region streams in: the kilometre when the region divides into kilometres, else the
        /// whole region as one tile (the fixture regions of the tests are tens of metres). Both ends of the wire
        /// call this, so neither has to say it.
        /// </summary>
        public static double SizeFor(double extentM)
        {
            if (!(extentM > 0.0)) throw new ArgumentOutOfRangeException(nameof(extentM));
            double tiles = extentM / DefaultTileSizeM;
            return Math.Abs(tiles - Math.Round(tiles)) < 1e-9 && tiles >= 1.0 ? DefaultTileSizeM : extentM;
        }

        /// <summary>The grid of a region of this extent, in metres; the Welcome carries the extent for the client.</summary>
        public TileGrid(double extentM) : this(extentM, SizeFor(extentM)) { }

        public TileGrid(double extentM, double tileSizeM)
        {
            if (!(extentM > 0.0)) throw new ArgumentOutOfRangeException(nameof(extentM));
            if (!(tileSizeM > 0.0)) throw new ArgumentOutOfRangeException(nameof(tileSizeM));
            ExtentM = extentM;
            HalfExtentM = extentM * 0.5;
            TileSizeM = tileSizeM;
            TilesPerSide = (int)Math.Round(extentM / tileSizeM);
            if (Math.Abs(TilesPerSide * tileSizeM - extentM) > 1e-6)
                throw new ArgumentException("a " + extentM + " m region does not divide into " + tileSizeM + " m tiles", nameof(tileSizeM));
        }

        public double ExtentM { get; }
        public double HalfExtentM { get; }
        public double TileSizeM { get; }
        public int TilesPerSide { get; }

        public bool Contains(TileId id) => id.Ix >= 0 && id.Iz >= 0 && id.Ix < TilesPerSide && id.Iz < TilesPerSide;

        /// <summary>The tile a point on the plane lies in; points on the region's far edge belong to the last tile.</summary>
        public TileId ForPosition(double east, double north)
        {
            double half = HalfExtentM;
            int ix = (int)Math.Floor((east + half) / TileSizeM);
            int iz = (int)Math.Floor((north + half) / TileSizeM);
            if (ix >= TilesPerSide) ix = TilesPerSide - 1;
            if (iz >= TilesPerSide) iz = TilesPerSide - 1;
            if (ix < 0) ix = 0;
            if (iz < 0) iz = 0;
            return new TileId(ix, iz);
        }

        /// <summary>The south-west corner of a tile in local metres.</summary>
        public void Origin(TileId id, out double east, out double north)
        {
            east = -HalfExtentM + id.Ix * TileSizeM;
            north = -HalfExtentM + id.Iz * TileSizeM;
        }

        /// <summary>The tile and its eight neighbours, those that exist, the centre first.</summary>
        public List<TileId> Around(TileId centre)
        {
            List<TileId> tiles = new List<TileId>(9);
            if (Contains(centre)) tiles.Add(centre);
            for (int dz = -1; dz <= 1; dz++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dz == 0) continue;
                    TileId id = new TileId(centre.Ix + dx, centre.Iz + dz);
                    if (Contains(id)) tiles.Add(id);
                }
            return tiles;
        }
    }
}
