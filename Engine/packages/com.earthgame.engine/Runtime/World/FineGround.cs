using System;

namespace EarthGame.Engine
{
    /// <summary>One corner of the quad a point lies in (BF.4): a post of the ground raster, as both sides hold it.</summary>
    public struct GroundCorner
    {
        /// <summary>The post's height as a tile carries it: rounded to the centimetre (<see cref="TileCodec.PostMetres"/>).</summary>
        public float HeightM;
        /// <summary>The post's cover byte (<see cref="GroundCovers"/>): what the relief there is made of; zero for none known.</summary>
        public byte Cover;
        /// <summary>How deep the post's cell is dug, cm (BF.3).</summary>
        public byte DugCm;
    }

    /// <summary>The four posts round a point (BF.4), south-west, south-east, north-west and north-east, and where the quad lies.</summary>
    public struct GroundQuad
    {
        /// <summary>East of the quad's west posts, m.</summary>
        public double WestEast;
        /// <summary>North of the quad's south posts, m.</summary>
        public double SouthNorth;
        public double CellM;
        public GroundCorner SW, SE, NW, NE;
    }

    /// <summary>
    /// The ground (BF.4, 2026-09-23): one function of its height at a point, read by the server wherever it asks for the
    /// ground and by the client wherever it places, draws or stands on it. It is the raster's bilinear height over the four
    /// posts round the point as the tiles carry them (centimetres, so both sides compute from equal numbers), plus the
    /// relief below the raster's cell (<see cref="Relief"/>), less the hollow a dig has made (BF.3). Until BF.4 the server
    /// stood on the raster in float metres, the client walked a Terrain resampled from the tiles, and a dug hollow was the
    /// client's alone.
    ///
    /// <para>Two things are not measured against it. Water: its depth is its surface over the raster, on both sides, as it
    /// always was (the tiles carry it so), and the relief falls to nothing toward water, so the two do not meet. And the
    /// slope a founder walks by: the client's mover judges walkable ground and the pace by the raster as the Terrain's posts
    /// sample it, as before BF.4, so the relief moves the feet and the eye and never turns ground the data calls walkable into
    /// ground that slides.</para>
    /// </summary>
    public static class FineGround
    {
        /// <summary>A dug cell's hollow reaches this share of a cell from its centre: a cone, as the client first drew it (BF.3).</summary>
        public const double HollowReachCells = 0.75;

        /// <summary>
        /// The height of the ground at a point in its quad. A point beyond the quad, which only a reader holding one tile asks
        /// for (a thing lying past its tile's west or south edge, as its cell's layout may put it), is given the raster carried
        /// on at the quad's own slope, where until BF.4 the tile's edge height was carried on flat.
        /// </summary>
        public static double At(in GroundQuad q, double east, double north, ulong seed)
        {
            double ux = (east - q.WestEast) / q.CellM, uz = (north - q.SouthNorth) / q.CellM;
            Weights(q, east, north, out double tx, out double tz);
            return RasterAt(q, ux, uz) + Relief.At(q, tx, tz, east, north, seed) - Hollow(q, east, north);
        }

        /// <summary>The raster's bilinear height between the quad's posts, at weights already known.</summary>
        public static double RasterAt(in GroundQuad q, double tx, double tz)
        {
            double south = q.SW.HeightM + (q.SE.HeightM - q.SW.HeightM) * tx;
            double northRow = q.NW.HeightM + (q.NE.HeightM - q.NW.HeightM) * tx;
            return south + (northRow - south) * tz;
        }

        /// <summary>How far across the quad a point lies, east and north, each held to it.</summary>
        public static void Weights(in GroundQuad q, double east, double north, out double tx, out double tz)
        {
            tx = (east - q.WestEast) / q.CellM;
            tz = (north - q.SouthNorth) / q.CellM;
            if (tx < 0.0) tx = 0.0; else if (tx > 1.0) tx = 1.0;
            if (tz < 0.0) tz = 0.0; else if (tz > 1.0) tz = 1.0;
        }

        /// <summary>
        /// How far below the rest of the ground the dug cells at the quad's corners have taken it at a point, m: a cone to each
        /// dug post's depth, <see cref="HollowReachCells"/> of a cell round it. No cone reaches past the quads round its own
        /// post, so the quads on either side of an edge agree on it.
        /// </summary>
        public static double Hollow(in GroundQuad q, double east, double north)
        {
            if ((q.SW.DugCm | q.SE.DugCm | q.NW.DugCm | q.NE.DugCm) == 0) return 0.0;
            double reach = HollowReachCells * q.CellM;
            return Cone(q.SW.DugCm, q.WestEast, q.SouthNorth, east, north, reach)
                 + Cone(q.SE.DugCm, q.WestEast + q.CellM, q.SouthNorth, east, north, reach)
                 + Cone(q.NW.DugCm, q.WestEast, q.SouthNorth + q.CellM, east, north, reach)
                 + Cone(q.NE.DugCm, q.WestEast + q.CellM, q.SouthNorth + q.CellM, east, north, reach);
        }

        private static double Cone(byte dugCm, double postEast, double postNorth, double east, double north, double reach)
        {
            if (dugCm == 0) return 0.0;
            double de = east - postEast, dn = north - postNorth;
            double share = 1.0 - Math.Sqrt(de * de + dn * dn) / reach;
            return share > 0.0 ? dugCm / 100.0 * share : 0.0;
        }

        /// <summary>Whether a cover byte is water: the sea, or fresh water (a creek, a stream, a lake), as the cover's own rule has it (<see cref="GroundCovers.Of"/>).</summary>
        public static bool IsWater(byte cover)
        {
            GroundCover c = GroundCovers.CoverOf(cover);
            return c == GroundCover.Sea || c == GroundCover.FreshWater;
        }

        /// <summary>
        /// The quad round a point of a square of posts, as a tile holds them: heights [north, east] from the south-west post
        /// at an origin, the cover's codes on the same posts (null for none), and the depth dug on each post's cell by its
        /// world row and column (null for none). Clamped to the square as <see cref="Heightfield.HeightAt"/> clamps to the
        /// raster, so neighbouring squares, which share their edge posts, agree on their edges.
        /// </summary>
        /// <param name="southRow">The world raster's row of the square's south-west post; rows run north to south.</param>
        /// <param name="westCol">The world raster's column of the square's south-west post.</param>
        public static void QuadIn(float[,] heights, byte[,] covers, int posts, double cellM, double originEast, double originNorth,
                                  int southRow, int westCol, Func<int, int, byte> dugCm, double east, double north, out GroundQuad q)
        {
            int last = posts - 1;
            double fx = (east - originEast) / cellM, fz = (north - originNorth) / cellM;
            if (fx < 0.0) fx = 0.0; else if (fx > last) fx = last;
            if (fz < 0.0) fz = 0.0; else if (fz > last) fz = last;
            int x0 = (int)Math.Floor(fx), z0 = (int)Math.Floor(fz);
            if (x0 >= last) x0 = last - 1;
            if (z0 >= last) z0 = last - 1;
            q = default;
            q.CellM = cellM;
            q.WestEast = originEast + x0 * cellM;
            q.SouthNorth = originNorth + z0 * cellM;
            q.SW = Post(heights, covers, z0, x0, southRow, westCol, dugCm);
            q.SE = Post(heights, covers, z0, x0 + 1, southRow, westCol, dugCm);
            q.NW = Post(heights, covers, z0 + 1, x0, southRow, westCol, dugCm);
            q.NE = Post(heights, covers, z0 + 1, x0 + 1, southRow, westCol, dugCm);
        }

        private static GroundCorner Post(float[,] heights, byte[,] covers, int z, int x, int southRow, int westCol, Func<int, int, byte> dugCm)
        {
            GroundCorner c = default;
            c.HeightM = heights[z, x];
            if (covers != null) c.Cover = covers[z, x];
            if (dugCm != null) c.DugCm = dugCm(southRow - z, westCol + x);
            return c;
        }

        /// <summary>
        /// The quad round a point as the server holds the world: its heights raster (each post rounded as a tile rounds it),
        /// its cover where the cover lies on the same posts, and its changes; false without terrain. Clamped at the raster's
        /// edge as <see cref="Heightfield.HeightAt"/> is.
        /// </summary>
        public static bool TryQuad(WorldState world, double east, double north, out GroundQuad q)
        {
            q = default;
            Heightfield terrain = world?.Terrain;
            if (terrain == null) return false;
            RegionRaster heights = terrain.Raster;
            double cell = heights.CellM, half = heights.ExtentM * 0.5;
            double col = (east + half) / cell, row = (half - north) / cell;
            int maxCol = heights.Width - 1, maxRow = heights.Height - 1;
            if (col < 0.0) col = 0.0; else if (col > maxCol) col = maxCol;
            if (row < 0.0) row = 0.0; else if (row > maxRow) row = maxRow;
            int c0 = (int)Math.Floor(col), r0 = (int)Math.Floor(row);
            if (c0 >= maxCol) c0 = maxCol - 1;
            if (r0 >= maxRow) r0 = maxRow - 1;
            RegionRaster cover = world.Cover;
            if (cover != null && (cover.Width != heights.Width || cover.Height != heights.Height || Math.Abs(cover.CellM - cell) > 1e-9)) cover = null;
            q.CellM = cell;
            q.WestEast = c0 * cell - half;
            q.SouthNorth = half - (r0 + 1) * cell;
            q.NW = Corner(heights, cover, world.Changes, r0, c0);
            q.NE = Corner(heights, cover, world.Changes, r0, c0 + 1);
            q.SW = Corner(heights, cover, world.Changes, r0 + 1, c0);
            q.SE = Corner(heights, cover, world.Changes, r0 + 1, c0 + 1);
            return true;
        }

        private static GroundCorner Corner(RegionRaster heights, RegionRaster cover, WorldChanges changes, int row, int col)
        {
            GroundCorner c = default;
            c.HeightM = TileCodec.PostMetres(heights[row, col]);
            if (cover != null) c.Cover = (byte)cover.Code(row, col);
            if (changes != null) c.DugCm = changes.GroundOf(row, col).DugCm;
            return c;
        }

        /// <summary>The ground at a point as the server holds the world; the datum without terrain.</summary>
        public static double At(WorldState world, double east, double north) =>
            TryQuad(world, east, north, out GroundQuad q) ? At(q, east, north, world.Seed) : Heightfield.SeaLevelM;

        /// <summary>
        /// The ground at a point as the server holds the world, as if nothing had been dug (BF.4 stage three): what a rock that
        /// stands is seated on, so a dig beside it does not move it; the datum without terrain or beyond it.
        /// </summary>
        public static double UndugAt(WorldState world, double east, double north)
        {
            if (world?.Terrain == null || !world.Terrain.Contains(east, north) || !TryQuad(world, east, north, out GroundQuad q)) return Heightfield.SeaLevelM;
            q.SW.DugCm = q.SE.DugCm = q.NW.DugCm = q.NE.DugCm = 0;
            return At(q, east, north, world.Seed);
        }
    }

    /// <summary>The ground as a height source (BF.4): the server's <see cref="FineGround"/>, for what judges a founder against it.</summary>
    public sealed class FineGroundSource : IHeightSource
    {
        private readonly WorldState _world;

        public FineGroundSource(WorldState world)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
        }

        public double HeightAt(double east, double north) => _world.GroundAt(east, north);
    }

    /// <summary>
    /// What a founder's feet meet as a height source (BF.4 stage three): the one ground, or the top of a rock that stands where
    /// that is higher (<see cref="StandingRocks.SurfaceAt"/>). What the movement check judges a report against, so a founder
    /// standing on a boulder is standing, not a metre off the ground.
    /// </summary>
    public sealed class SurfaceSource : IHeightSource
    {
        private readonly WorldState _world;

        public SurfaceSource(WorldState world)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
        }

        public double HeightAt(double east, double north) => _world.SurfaceAt(east, north);
    }

    /// <summary>
    /// The relief below the raster (BF.4 promise 2): smooth value noise on lattices no finer than 5 m, so the client's Terrain
    /// posts, 1.953 m apart, carry it; its amplitude and lattice set by each corner's cover and blended by the corners' own
    /// bilinear weights, so no cell edge shows; falling to nothing toward any post under water, by the smootherstep of the
    /// quad's dry share (<see cref="Taper"/>). Each lattice is turned off the region's axes and read in two octaves, so no row of humps lines up
    /// with the map. Hashed from whole numbers (<see cref="StandLayout.Mix"/>), the lattice, the octave and the seed, so the
    /// server and every client grow the same humps.
    ///
    /// <para>The table is a first model, to be judged in frames (DEBTS): what a forest floor, a heath or a dune is like
    /// underfoot at a few metres, stated so it can be disputed line by line. Ledges on cliffs, and boulders, are rock that
    /// stands (stage three), not relief.</para>
    /// </summary>
    public static class Relief
    {
        /// <summary>The relief never lies further than this from the raster, m: the table's largest amplitude, and the tests hold it.</summary>
        public const double MostM = 0.12;

        /// <summary>
        /// The steepest the relief alone tilts the ground, degrees, as the tests measure it over a world of every cover beside
        /// water: the noise's own slope, the step from one cover's amplitude to the next across a cell, and the taper toward
        /// water, together. The walk is judged on the raster's slope and never meets it (<see cref="FineGround"/>).
        /// </summary>
        public const double SteepestDeg = 6.0;

        /// <summary>
        /// How far the client's Terrain, the ground sampled at posts 1.953 m apart and flat between them, strays from the relief,
        /// m, as the tests measure it on the finest lattice: what the collider and the drawn ground differ from the server's by,
        /// on relief alone.
        /// </summary>
        public const double TerrainStrayM = 0.05;

        /// <summary>The lattices the table uses, by their spacing, m; the coarse octave's is this times <see cref="CoarseOctave"/>.</summary>
        private static readonly double[] Spacings = { 5.0, 6.0, 12.0 };

        /// <summary>
        /// How far each lattice's octaves are turned off the region's axes: the cosine and sine of 23.6°, 61.5° and 126.4° for
        /// the fine octaves and 76.7°, 115.6° and 36.1° for the coarse, none near a quarter turn. Written out rather than
        /// computed, so no runtime's own cosine can grow a different hump on another machine.
        /// </summary>
        private static readonly double[] FineCos = { 0.916521877288114, 0.47740260482565433, -0.5940656889619009 };
        private static readonly double[] FineSin = { 0.39998456026735746, 0.8786846720557268, 0.804416532151237 };
        private static readonly double[] CoarseCos = { 0.22962885113282602, -0.4317244262253331, 0.8074969508504661 };
        private static readonly double[] CoarseSin = { 0.9732782699348724, 0.9020055541959855, 0.5898717439979643 };

        /// <summary>The coarse octave's spacing, as a share of the lattice's, and the share of the relief each octave carries.</summary>
        public const double CoarseOctave = 2.3;
        public const double FineShare = 0.7, CoarseShare = 0.3;

        /// <summary>
        /// What the relief is where a cover grows: its amplitude, m, and its lattice (an index of the spacings); false for a
        /// cover with none (water, and a cover not known).
        /// </summary>
        public static bool Of(byte coverCode, out double amplitudeM, out int lattice)
        {
            switch (GroundCovers.CoverOf(coverCode))
            {
                case GroundCover.ForestFloor: amplitudeM = 0.12; lattice = 1; return true;  // root humps, old falls, the pits trees left
                case GroundCover.Bracken: amplitudeM = 0.10; lattice = 1; return true;      // the same floor under a fern
                case GroundCover.Heath: amplitudeM = 0.10; lattice = 0; return true;        // hummocks round the shrubs
                case GroundCover.Grass:
                case GroundCover.Sedge: amplitudeM = 0.06; lattice = 0; return true;        // tussock ground, smoothed to the posts
                case GroundCover.Rock: amplitudeM = 0.10; lattice = 0; return true;         // broken rock, a platform's benches
                case GroundCover.BareEarth: amplitudeM = 0.05; lattice = 0; return true;
                case GroundCover.SwampFloor: amplitudeM = 0.04; lattice = 0; return true;   // hummock and hollow
                case GroundCover.DuneSand: amplitudeM = 0.04; lattice = 2; return true;     // bare sand the wind smooths
                case GroundCover.Sand: amplitudeM = 0.02; lattice = 2; return true;         // a beach the swash smooths
                default: amplitudeM = 0.0; lattice = 0; return false;
            }
        }

        /// <summary>A lattice's spacing, m.</summary>
        public static double SpacingOf(int lattice) => Spacings[lattice];

        /// <summary>The relief at a point of a quad whose weights are already known, m.</summary>
        public static double At(in GroundQuad q, double tx, double tz, double east, double north, ulong seed)
        {
            double wSW = (1.0 - tx) * (1.0 - tz), wSE = tx * (1.0 - tz), wNW = (1.0 - tx) * tz, wNE = tx * tz;
            bool dSW = !FineGround.IsWater(q.SW.Cover), dSE = !FineGround.IsWater(q.SE.Cover);
            bool dNW = !FineGround.IsWater(q.NW.Cover), dNE = !FineGround.IsWater(q.NE.Cover);
            double dry = (dSW ? wSW : 0.0) + (dSE ? wSE : 0.0) + (dNW ? wNW : 0.0) + (dNE ? wNE : 0.0);
            if (dry <= 0.0) return 0.0;
            // Each lattice's noise is read once, however many corners share it.
            double n0 = double.NaN, n1 = double.NaN, n2 = double.NaN;
            double relief = (dSW ? Term(q.SW.Cover, wSW, east, north, seed, ref n0, ref n1, ref n2) : 0.0)
                          + (dSE ? Term(q.SE.Cover, wSE, east, north, seed, ref n0, ref n1, ref n2) : 0.0)
                          + (dNW ? Term(q.NW.Cover, wNW, east, north, seed, ref n0, ref n1, ref n2) : 0.0)
                          + (dNE ? Term(q.NE.Cover, wNE, east, north, seed, ref n0, ref n1, ref n2) : 0.0);
            return relief * Taper(dry);
        }

        /// <summary>
        /// How much of the relief stands in a quad with water at some of its posts, from its dry share (the weights of its dry
        /// posts at the point): the smootherstep, flat at both ends, so the relief is all there beside dry posts and fades with
        /// a slope no steeper than 1.875 across the cell; at a quarter dry a tenth is left, at a tenth dry under a hundredth.
        /// </summary>
        public static double Taper(double dry)
        {
            if (dry >= 1.0) return 1.0;
            if (dry <= 0.0) return 0.0;
            return dry * dry * dry * (10.0 + dry * (-15.0 + 6.0 * dry));
        }

        private static double Term(byte cover, double weight, double east, double north, ulong seed, ref double n0, ref double n1, ref double n2)
        {
            if (weight <= 0.0 || !Of(cover, out double amplitude, out int lattice)) return 0.0;
            double n;
            switch (lattice)
            {
                case 0: if (double.IsNaN(n0)) n0 = Noise(east, north, 0, seed); n = n0; break;
                case 1: if (double.IsNaN(n1)) n1 = Noise(east, north, 1, seed); n = n1; break;
                default: if (double.IsNaN(n2)) n2 = Noise(east, north, 2, seed); n = n2; break;
            }
            return weight * amplitude * n;
        }

        /// <summary>
        /// A lattice's noise at a point, -1 to 1: its fine octave and its coarse one, each turned its own way, anchored at the
        /// region's origin so it is the same wherever it is asked.
        /// </summary>
        public static double Noise(double east, double north, int lattice, ulong seed)
        {
            double spacing = Spacings[lattice];
            return FineShare * Octave(east, north, spacing, FineCos[lattice], FineSin[lattice], (ulong)(2 * lattice + 1), seed)
                 + CoarseShare * Octave(east, north, spacing * CoarseOctave, CoarseCos[lattice], CoarseSin[lattice], (ulong)(2 * lattice + 2), seed);
        }

        /// <summary>
        /// Value noise on one turned lattice, -1 to 1: a hashed value at every lattice point, eased between by the smoothstep,
        /// so it is continuous with a continuous slope and no steeper than 1.5 times two values over the spacing.
        /// </summary>
        private static double Octave(double east, double north, double spacing, double c, double s, ulong salt, ulong seed)
        {
            double gx = (east * c + north * s) / spacing, gz = (north * c - east * s) / spacing;
            double fi = Math.Floor(gx), fj = Math.Floor(gz);
            long i = (long)fi, j = (long)fj;
            double fx = gx - fi, fz = gz - fj;
            double sx = fx * fx * (3.0 - 2.0 * fx), sz = fz * fz * (3.0 - 2.0 * fz);
            double v00 = Node(i, j, salt, seed), v10 = Node(i + 1, j, salt, seed);
            double v01 = Node(i, j + 1, salt, seed), v11 = Node(i + 1, j + 1, salt, seed);
            double south = v00 + (v10 - v00) * sx;
            double northRow = v01 + (v11 - v01) * sx;
            return south + (northRow - south) * sz;
        }

        private static double Node(long i, long j, ulong salt, ulong seed)
        {
            ulong h = StandLayout.Mix(((ulong)(uint)i << 32) | (uint)j);
            h = StandLayout.Mix(h ^ seed ^ (salt << 56) ^ 0x6A09E667F3BCC909UL);
            return (h >> 11) * (2.0 / 9007199254740991.0) - 1.0;
        }
    }
}
