using System;

namespace EarthGame.Engine
{
    /// <summary>The ground as a height over the tangent plane: anything that can say how high the surface is at (east, north).</summary>
    public interface IHeightSource
    {
        double HeightAt(double east, double north);
    }

    /// <summary>
    /// The surface a <see cref="RegionRaster"/> describes, sampled anywhere on the plane. Bilinear between the four
    /// surrounding cell centres; beyond the outermost centres the edge value continues flat, which is never reached
    /// in play because the server clamps positions inside the extent. This is the <b>server's</b> ground (the
    /// heightfield clamp of ARCHITECTURE §9); the client's ground is PhysX over a Terrain built from the same
    /// posts, and the two disagree most where the surface is steepest, which is the named defect class in DEBTS.md.
    /// </summary>
    public sealed class Heightfield : IHeightSource
    {
        /// <summary>
        /// The datum the bake's heights are stated against, metres: Terrarium's zero is mean sea level, and
        /// every sea cell in the bake is at it (the sea has no floor yet, DEBTS.md). The one place the number
        /// lives: the drainage's sink, the spawn's floor and the client's water surface all read it here.
        /// </summary>
        public const double SeaLevelM = 0.0;

        private readonly RegionRaster _raster;

        public Heightfield(RegionRaster raster)
        {
            _raster = raster ?? throw new ArgumentNullException(nameof(raster));
            CellM = raster.CellM;
            HalfExtentM = raster.ExtentM * 0.5;
        }

        public RegionRaster Raster => _raster;
        public double CellM { get; }
        public double HalfExtentM { get; }

        /// <summary>True inside the square the cell centres cover.</summary>
        public bool Contains(double east, double north)
            => east >= -HalfExtentM && east <= HalfExtentM && north >= -HalfExtentM && north <= HalfExtentM;

        /// <summary>Height of the surface at a point on the plane, metres above sea level.</summary>
        public double HeightAt(double east, double north)
        {
            double col = (east + HalfExtentM) / CellM;
            double row = (HalfExtentM - north) / CellM;
            int maxCol = _raster.Width - 1;
            int maxRow = _raster.Height - 1;
            if (col < 0.0) col = 0.0; else if (col > maxCol) col = maxCol;
            if (row < 0.0) row = 0.0; else if (row > maxRow) row = maxRow;
            int c0 = (int)Math.Floor(col);
            int r0 = (int)Math.Floor(row);
            if (c0 >= maxCol) c0 = maxCol - 1;
            if (r0 >= maxRow) r0 = maxRow - 1;
            double tc = col - c0;
            double tr = row - r0;
            double h00 = _raster[r0, c0];
            double h01 = _raster[r0, c0 + 1];
            double h10 = _raster[r0 + 1, c0];
            double h11 = _raster[r0 + 1, c0 + 1];
            double top = h00 + (h01 - h00) * tc;
            double bottom = h10 + (h11 - h10) * tc;
            return top + (bottom - top) * tr;
        }

        /// <summary>
        /// Rise over run along a horizontal direction, from the surface one cell either side of the point.
        /// Positive uphill in that direction; the number Tobler's function wants.
        /// </summary>
        public double SlopeAlong(double east, double north, double dirEast, double dirNorth)
        {
            double len = Math.Sqrt(dirEast * dirEast + dirNorth * dirNorth);
            if (len <= 1e-9) return 0.0;
            double de = dirEast / len * CellM;
            double dn = dirNorth / len * CellM;
            double ahead = HeightAt(east + de, north + dn);
            double behind = HeightAt(east - de, north - dn);
            return (ahead - behind) / (2.0 * CellM);
        }

        /// <summary>Unit normal of the surface at a point, by central differences one cell wide. Up on flat ground.</summary>
        public Double3 NormalAt(double east, double north)
        {
            double dhde = (HeightAt(east + CellM, north) - HeightAt(east - CellM, north)) / (2.0 * CellM);
            double dhdn = (HeightAt(east, north + CellM) - HeightAt(east, north - CellM)) / (2.0 * CellM);
            return new Double3(-dhde, 1.0, -dhdn).Normalized;
        }

        /// <summary>The steepest slope at a point as an angle from the horizontal, degrees.</summary>
        public double SlopeAngleDeg(double east, double north)
        {
            Double3 n = NormalAt(east, north);
            return Math.Acos(SimMath.Clamp(n.Y, -1.0, 1.0)) * GeoMath.RadToDeg;
        }
    }
}
