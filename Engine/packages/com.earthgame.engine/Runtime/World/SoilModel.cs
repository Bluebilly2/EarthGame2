using System;

namespace EarthGame.Engine
{
    /// <summary>
    /// The soil, as what is left over from the same history that made the landform. Ported from v1 with four
    /// adjustments named below: the diffusivity is a constant of the model rather than the erosion's, the sea is
    /// masked, the sort is keyed, and the water is spread over the drainage's filled surface.
    ///
    /// <para>Nothing here is placed either. Bedrock weathers into soil at a rate that <b>slows as
    /// the soil gets deeper</b>, because a thick blanket protects the rock underneath it — the
    /// soil production function, measured in the field and published (Heimsath 1997). Soil then
    /// creeps downhill by the same diffusion that rounds the hillslopes. Where the ground is
    /// convex the creep carries more away than weathering makes, and the soil is thin or the rock
    /// is bare; where it is concave the creep brings more in than leaves, and the soil is deep.
    /// That is why ridges are stony and gullies are not.</para>
    ///
    /// <para>Wetness is the topographic wetness index, <c>ln(a / tanβ)</c> — how much land drains
    /// through a place against how fast it drains away again. It is the standard measure, and it
    /// is what a person is using when they say a hollow holds water and a spur does not.</para>
    ///
    /// <para>The area that goes into it is accumulated by <b>multiple flow direction</b>, not by
    /// the single-direction routing the channel network uses. Water leaving a cell is shared among
    /// every neighbour below it, in proportion to how steeply each falls away. Forcing it down one
    /// of eight directions instead is fine for a creek, which really is a single thread, and wrong
    /// for a hillside, which is not: it gathers the whole slope into parallel lines and lays a
    /// plaid of wet and dry stripes across country that has neither.</para>
    ///
    /// <para>The adjustments. v1 handed the model the diffusivity its erosion ran with; v2 has no
    /// landform evolution on real data (decision 2026-09-07), so the hillslope diffusivity is the
    /// model's own constant. The sea is no soil at all: depth zero, wetness one, and left out of the
    /// percentiles the wetness is scaled by, since a quarter of the Bherwerre raster is flat sea that
    /// would otherwise define "wet". The height-ordered pass sorts by key rather than by delegate,
    /// because there are four million cells. And the water is spread over the <see cref="DrainageNetwork"/>'s
    /// filled surface rather than the raw ground: v1's eroded land had no closed hollows, real land
    /// has, and water crosses a filled hollow the way the channel network already knows; the soil's
    /// depth still reads the raw ground's curvature and slope, since that is where the soil lies.
    /// The drainage is therefore the model's input, and the one owner of the fill and the sea.</para>
    ///
    /// <para>Both are things the founder can see and act on: deep soil for digging, wet ground for
    /// what grows there, bare rock for stone. None of them is a rule (§3, §6).</para>
    /// </summary>
    public sealed class SoilModel
    {
        /// <summary>Bare-rock weathering rate, m/yr. About 0.08 mm a year.</summary>
        public const double ProductionRate = 7.7e-5;

        /// <summary>Depth over which weathering slows by e. Half a metre.</summary>
        public const double ProductionDecayM = 0.5;

        /// <summary>Rock over soil, by density. Rock 2,700; soil 1,350.</summary>
        public const double DensityRatio = 2.0;

        /// <summary>The most soil a hollow will hold before creep carries it on, m.</summary>
        public const double MaxDepthM = 2.5;

        /// <summary>Above this slope soil does not stay on the hill at all.</summary>
        public const double SlopeOfRepose = 0.7;

        /// <summary>
        /// Hillslope soil creep, m²/yr: the diffusivity the depth balance is struck against. Field values on
        /// soil-mantled hillslopes run from about 0.001 to 0.01 (Heimsath and others on the Oregon Coast Range
        /// and the Australian tablelands); v1's erosion ran at 0.005 and its soil tests were tuned to it.
        /// </summary>
        public const double HillslopeDiffusivityM2PerYear = 0.005;

        /// <summary>
        /// So little soil that the rock is showing, m.
        ///
        /// <para>Named rather than written into <see cref="IsBareRock"/> alone because a second
        /// model asks the same question — v1's animal tracks wanted to know whether there was
        /// anything to press a print into, which is this threshold and not a new one. Where rock
        /// begins is one fact and belongs to the soil.</para>
        /// </summary>
        public const double BareRockDepthM = 0.06;

        private readonly float[] _depth;
        private readonly float[] _wetness;
        private readonly bool[] _sea;

        public int Width { get; }
        public int Height { get; }
        public double CellSizeM { get; }

        /// <summary>Soil thickness in metres, per cell.</summary>
        public float[] DepthM => _depth;

        /// <summary>Topographic wetness, normalised 0 (a dry spur) to 1 (a soak).</summary>
        public float[] Wetness01 => _wetness;

        /// <param name="drainage">The drainage of the ground: its raw surface, its filled surface and its sea.</param>
        /// <param name="diffusivity">Hillslope creep, m²/yr; the model's own constant unless a test says otherwise.</param>
        public SoilModel(DrainageNetwork drainage, double diffusivity = HillslopeDiffusivityM2PerYear)
        {
            if (drainage == null) throw new ArgumentNullException(nameof(drainage));
            if (drainage.Width < 3 || drainage.Height < 3) throw new ArgumentException("grid too small");

            Width = drainage.Width;
            Height = drainage.Height;
            CellSizeM = drainage.CellSizeM;
            int width = Width, height = Height;

            _depth = new float[width * height];
            _wetness = new float[width * height];
            _sea = drainage.SeaArray;
            float[] surface = drainage.GroundArray;

            Build(surface, SpreadFlow(drainage.FilledArray), diffusivity);
            NormaliseWetness();

            // Soil depth and moisture are continuous fields; the cell-by-cell scatter that comes
            // out of a flow accumulation is not something the ground actually does. Smoothing also
            // keeps the terrain mesh from aliasing them when it samples at coarse detail.
            Blur(_depth, 2);
            Blur(_wetness, 2);

            for (int i = 0; i < width * height; i++)
            {
                if (!_sea[i]) continue;
                _depth[i] = 0f;
                _wetness[i] = 1f;
            }
        }

        /// <summary>The soil of a raster, over its drainage with the sea at the bake's datum.</summary>
        public static SoilModel Of(RegionRaster raster) => new SoilModel(DrainageNetwork.Of(raster));

        /// <summary>
        /// Rescales the wetness index onto 0..1 against the range this landscape actually
        /// occupies, taking the tenth and ninetieth percentiles of the land as the ends.
        ///
        /// <para>Fixed thresholds cannot do this job. The topographic wetness index is famously
        /// resolution-dependent - the same hillside indexes differently at 16 m than at 1 m,
        /// because the contributing area per cell changes with the cell - so a constant borrowed
        /// from a published field study described a different grid than this one. Calibrating
        /// against the landscape's own distribution was the difference between a catchment that
        /// read 82% dry-grass and one with dry spurs, green flats and rank soaks in it.</para>
        ///
        /// <para>The ordering is untouched: wetter ground is still wetter ground, and every test
        /// that compares one place against another holds. What changes is only where the ends of
        /// the scale sit, and the percentiles rather than the extremes are used so that one
        /// freakish gully cannot flatten everything else.</para>
        /// </summary>
        private void NormaliseWetness()
        {
            int land = 0;
            for (int i = 0; i < _wetness.Length; i++) if (!_sea[i]) land++;
            if (land == 0) return;
            var sorted = new float[land];
            int k = 0;
            for (int i = 0; i < _wetness.Length; i++) if (!_sea[i]) sorted[k++] = _wetness[i];
            Array.Sort(sorted);

            float low = sorted[(int)(sorted.Length * 0.10)];
            float high = sorted[(int)(sorted.Length * 0.90)];
            if (high - low < 1e-4f) { high = low + 1e-4f; }

            for (int i = 0; i < _wetness.Length; i++)
                _wetness[i] = (float)SimMath.Clamp01((_wetness[i] - low) / (high - low));
        }

        /// <summary>A separable box blur over one of the fields, in place.</summary>
        private void Blur(float[] grid, int passes)
        {
            var scratch = new float[grid.Length];

            for (int pass = 0; pass < passes; pass++)
            {
                for (int z = 0; z < Height; z++)
                {
                    for (int x = 0; x < Width; x++)
                    {
                        int a = Math.Max(0, x - 1), b = Math.Min(Width - 1, x + 1);
                        scratch[z * Width + x] =
                            (grid[z * Width + a] + grid[z * Width + x] + grid[z * Width + b]) / 3f;
                    }
                }

                for (int z = 0; z < Height; z++)
                {
                    int a = Math.Max(0, z - 1), b = Math.Min(Height - 1, z + 1);
                    for (int x = 0; x < Width; x++)
                    {
                        grid[z * Width + x] = (scratch[a * Width + x] + scratch[z * Width + x]
                                             + scratch[b * Width + x]) / 3f;
                    }
                }
            }
        }

        public float DepthAt(int x, int z) => _depth[z * Width + x];
        public float WetnessAt(int x, int z) => _wetness[z * Width + x];
        public bool IsSea(int x, int z) => _sea[z * Width + x];

        /// <summary>Whether there is so little soil that the rock is showing.</summary>
        public bool IsBareRock(int x, int z) => _depth[z * Width + x] < BareRockDepthM;

        /// <summary>
        /// Steady-state soil depth where the ground sheds soil: weathering makes it as fast as
        /// creep carries it away. Returns 0 where creep wins outright, which is bare rock.
        /// </summary>
        public static double SteadyDepth(double diffusivity, double curvature)
        {
            // Convex ground exports soil; the export rate is D times the curvature.
            double export = -diffusivity * curvature;
            if (export <= 0.0) return MaxDepthM;                 // convergent: it collects instead

            double supply = DensityRatio * ProductionRate;
            if (export >= supply) return 0.0;                    // stripped faster than made

            return -ProductionDecayM * Math.Log(export / supply);
        }

        /// <summary>
        /// Multiple-flow-direction accumulation: every cell gives what it carries to all of its
        /// lower neighbours, split by how steeply each one falls. Highest ground first, so nothing
        /// is counted twice. The sea receives, and passes nothing on.
        /// </summary>
        private double[] SpreadFlow(float[] surface)
        {
            int count = Width * Height;
            var area = new double[count];
            double cellArea = CellSizeM * CellSizeM;
            for (int i = 0; i < count; i++) area[i] = cellArea;

            var order = new int[count];
            var keys = new float[count];
            for (int i = 0; i < count; i++)
            {
                order[i] = i;
                keys[i] = -surface[i];
            }
            Array.Sort(keys, order);

            var weight = new double[8];
            int[] dx = { 1, 1, 0, -1, -1, -1, 0, 1 };
            int[] dz = { 0, 1, 1, 1, 0, -1, -1, -1 };

            // Freeman's exponent: a little over one, so the steepest way takes rather more than
            // its share without taking all of it.
            const double exponent = 1.1;

            for (int k = 0; k < count; k++)
            {
                int i = order[k];
                if (_sea[i]) continue;
                int x = i % Width, z = i / Width;

                double total = 0.0;
                for (int d = 0; d < 8; d++)
                {
                    weight[d] = 0.0;
                    int nx = x + dx[d], nz = z + dz[d];
                    if (nx < 0 || nz < 0 || nx >= Width || nz >= Height) continue;

                    double drop = surface[i] - surface[nz * Width + nx];
                    if (drop <= 0.0) continue;

                    double distance = (dx[d] != 0 && dz[d] != 0)
                        ? CellSizeM * 1.4142135623730951 : CellSizeM;

                    weight[d] = Math.Pow(drop / distance, exponent);
                    total += weight[d];
                }

                if (total <= 0.0) continue;

                for (int d = 0; d < 8; d++)
                {
                    if (weight[d] <= 0.0) continue;
                    area[(z + dz[d]) * Width + (x + dx[d])] += area[i] * weight[d] / total;
                }
            }

            return area;
        }

        private void Build(float[] surface, double[] catchment, double diffusivity)
        {
            double dx2 = CellSizeM * CellSizeM;

            for (int z = 0; z < Height; z++)
            {
                for (int x = 0; x < Width; x++)
                {
                    int i = z * Width + x;

                    int xm = Math.Max(0, x - 1), xp = Math.Min(Width - 1, x + 1);
                    int zm = Math.Max(0, z - 1), zp = Math.Min(Height - 1, z + 1);

                    double curvature = (surface[z * Width + xm] + surface[z * Width + xp]
                                      + surface[zm * Width + x] + surface[zp * Width + x]
                                      - 4.0 * surface[i]) / dx2;

                    // Divide by the distance actually spanned. At the rim the neighbour indices
                    // clamp, so a central difference there is over one cell rather than two - and
                    // halving the slope made the edge of a cliff look like ground that holds soil.
                    double gx = (surface[z * Width + xp] - surface[z * Width + xm])
                              / ((xp - xm) * CellSizeM);
                    double gz = (surface[zp * Width + x] - surface[zm * Width + x])
                              / ((zp - zm) * CellSizeM);
                    double slope = Math.Sqrt(gx * gx + gz * gz);

                    double depth = Math.Min(MaxDepthM, SteadyDepth(diffusivity, curvature));

                    // Nothing stays on a slope steeper than it can rest on.
                    if (slope >= SlopeOfRepose) depth = 0.0;
                    else depth *= 1.0 - slope / SlopeOfRepose;

                    _depth[i] = (float)depth;

                    // ln(a / tanB): the land draining through, against how fast it drains away.
                    double a = Math.Max(CellSizeM, catchment[i] / CellSizeM);
                    double twi = Math.Log(a / Math.Max(0.005, slope));

                    // Stored raw for now; the range it actually occupies is measured below.
                    _wetness[i] = (float)twi;
                }
            }
        }

        /// <summary>
        /// What the rock weathers into. Coarse rock makes sandy soil that drains and holds
        /// nothing; fine rock makes clay that holds both water and nutrients. Derived from the
        /// stone's own grain size and hardness, never listed per rock (§8).
        /// </summary>
        public static double SandFraction(StoneType stone)
        {
            if (stone == null) return 0.5;

            // Grain size runs from glassy to gritty over about three orders of magnitude.
            double coarse = (Math.Log10(Math.Max(0.001, stone.GrainSizeMm)) + 3.0) / 4.0;
            return SimMath.Clamp01(coarse);
        }

        /// <summary>
        /// How much a soil will feed. Clay holds nutrients where sand loses them, and soft rock
        /// gives them up where hard rock keeps them locked away.
        /// </summary>
        public static double Fertility(StoneType stone, double depthM, double wetness01)
        {
            if (stone == null) return 0.3;

            double clay = 1.0 - SandFraction(stone);
            double weatherable = SimMath.Clamp01((7.0 - stone.MohsHardness) / 5.0);
            double body = SimMath.Clamp01(depthM / 1.0);

            // Waterlogged ground is not fertile either; it is sour.
            double drainage = 1.0 - SimMath.Clamp01((wetness01 - 0.8) / 0.2) * 0.6;

            return SimMath.Clamp01(0.15 + 0.85 * clay * weatherable * body * drainage);
        }
    }
}
