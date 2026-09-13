using System;
using System.Collections.Generic;

namespace EarthGame.Engine
{
    /// <summary>
    /// What each square of presence feeds (M1.7a): for each kind, the mean of the world's own capacity layer over the cells
    /// whose centres lie in the square, worked out once when the world loads. Presence draws each square at the capacity of
    /// the ground it covers rather than at one figure for the whole world, and a square beyond the layer feeds nothing.
    ///
    /// <para>The squares are presence's own (<see cref="AnimalPresence.CellSizeM"/>), counted from the region's centre as
    /// its metres are, and a cell's centre is where <see cref="StandLayout.CellCentre"/> puts it. A layer is averaged and
    /// let go: the server keeps the means, not the raster.</para>
    /// </summary>
    public sealed class CapacitySquares
    {
        private readonly Dictionary<AnimalSpecies, double[]> _perKm2 = new Dictionary<AnimalSpecies, double[]>();
        private readonly int _min;
        private readonly int _across;

        /// <param name="extentM">The side of the region the layers cover, metres.</param>
        public CapacitySquares(double extentM)
        {
            if (!(extentM > 0.0)) throw new ArgumentOutOfRangeException(nameof(extentM), "a region is " + extentM + " m across");
            ExtentM = extentM;
            _min = SquareOf(-extentM * 0.5);
            _across = SquareOf(extentM * 0.5) - _min + 1;
        }

        /// <summary>The unit a capacity layer is in, animals per square kilometre: what creation writes a layer in and <see cref="Add"/> holds one to.</summary>
        public const string Unit = "1/km2";

        /// <summary>The side of the region the squares cover, metres.</summary>
        public double ExtentM { get; }

        /// <summary>
        /// Adds a kind's capacity layer, averaged over each square. A layer in another unit than <see cref="Unit"/>, one of
        /// another extent, and a kind added twice are refused.
        /// </summary>
        public void Add(AnimalSpecies species, RegionRaster layer)
        {
            if (species == null) throw new ArgumentNullException(nameof(species));
            if (layer == null) throw new ArgumentNullException(nameof(layer));
            if (!string.Equals(layer.Unit, Unit, StringComparison.Ordinal))
                throw new ArgumentException("the capacity of " + species.Name + " is in '" + layer.Unit + "', not '" + Unit + "'", nameof(layer));
            if (Math.Abs(layer.ExtentM - ExtentM) > 1e-6)
                throw new ArgumentException("the capacity of " + species.Name + " covers " + layer.ExtentM + " m, and the squares " + ExtentM + " m", nameof(layer));
            if (_perKm2.ContainsKey(species)) throw new ArgumentException("the capacity of " + species.Name + " is already added", nameof(species));
            double[] means = new double[_across * _across];
            int[] counts = new int[means.Length];
            for (int row = 0; row < layer.Height; row++)
                for (int col = 0; col < layer.Width; col++)
                {
                    StandLayout.CellCentre(row, col, layer.CellM, layer.ExtentM, out double east, out double north);
                    int i = Index(SquareOf(east), SquareOf(north));
                    if (i < 0) continue;
                    means[i] += layer[row, col];
                    counts[i]++;
                }
            for (int i = 0; i < means.Length; i++) means[i] = counts[i] > 0 ? means[i] / counts[i] : 0.0;
            _perKm2[species] = means;
        }

        /// <summary>Whether a kind's capacity was added.</summary>
        public bool Feeds(AnimalSpecies species) => species != null && _perKm2.ContainsKey(species);

        /// <summary>Animals of a kind a square feeds, per square kilometre: the layer's mean over it, and nothing beyond the region or for a kind not added.</summary>
        public double PerKm2(AnimalSpecies species, int cellX, int cellZ)
        {
            if (species == null || !_perKm2.TryGetValue(species, out double[] means)) return 0.0;
            int i = Index(cellX, cellZ);
            return i < 0 ? 0.0 : means[i];
        }

        private static int SquareOf(double metres) => (int)Math.Floor(metres / AnimalPresence.CellSizeM);

        private int Index(int cellX, int cellZ)
        {
            int x = cellX - _min, z = cellZ - _min;
            if (x < 0 || z < 0 || x >= _across || z >= _across) return -1;
            return z * _across + x;
        }
    }
}
