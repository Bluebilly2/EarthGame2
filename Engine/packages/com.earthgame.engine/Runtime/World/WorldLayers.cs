using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using System.Globalization;
using System.IO;

namespace EarthGame.Engine
{
    /// <summary>What a cell of ground carries, as a class the water layer stores.</summary>
    public enum WaterClass : byte
    {
        Dry = 0,
        Damp = 1,
        Trickle = 2,
        Creek = 3,
        Stream = 4,
        /// <summary>Standing fresh water: a pond, a lake.</summary>
        Lake = 5,
        /// <summary>Wet ground that is not open water: the swamp and the sedgeland.</summary>
        Swamp = 6,
        /// <summary>Salt: the bay and the ocean.</summary>
        Sea = 7,
    }

    /// <summary>The topology bits, one per kind of ground a person names when reading country.</summary>
    [Flags]
    public enum Topology : uint
    {
        None = 0,
        Sea = 1,
        Beach = 2,
        Dune = 4,
        Wetland = 8,
        Forest = 16,
        Heath = 32,
        Crest = 64,
        Cliff = 128,
        ShorePlatform = 256,
        Lake = 512,
        Creek = 1024,
    }

    /// <summary>
    /// The chain over the whole region, computed once at world creation from the baked heights and the world's
    /// seed (ARCHITECTURE §3, M1.2 promises 6 to 9): drainage, soil, water bodies with the fresh flag, the plant
    /// community, the animals' capacity, the topology mask, the stone, and the distances the wake-point scorer
    /// asks about. Every layer is a grid the size of the heights, row 0 north and column 0 west, and every rule
    /// that turns one layer into the next is stated here with its number, so a person who knows the country can
    /// call a rule wrong by name.
    ///
    /// <para>The sea gets a floor by a stated rule until the bathymetry arrives (DEBTS.md, "the sea has no
    /// floor"): the bake's sea is flat at the datum, and the founder walking into it walked on water. The floor
    /// falls at one in twenty from the water's edge to thirty metres, which is the order of the shelf off this
    /// coast and lets wading begin at the shore.</para>
    /// </summary>
    public sealed class WorldLayers
    {
        /// <summary>The sea floor falls this far per metre from the shore, until <see cref="SeaFloorMaxDepthM"/>.</summary>
        public const double SeaFloorGradient = 0.05;
        public const double SeaFloorMaxDepthM = 30.0;

        /// <summary>A one-level flat with a rim this large is a lake rather than a dent; Blacks Waterhole, the smallest named, is 1.4 ha.</summary>
        public const double MinLakeAreaM2 = 10_000.0;
        /// <summary>A lake in the bake is a flat surface: relief across a 5 × 5 window below this, m.</summary>
        public const float LakeFlatnessM = 0.6f;
        /// <summary>
        /// And one level: a patch is grown only through flat cells within this of its lowest, m. Without the band
        /// the sand plain, flat cell by cell, joined into one lake thirty metres high (the second probe world of
        /// 2026-09-09: 106 ha from 12 m to 44 m where Lake Windermere is 31 ha).
        /// </summary>
        public const float LakeLevelBandM = 0.5f;
        /// <summary>
        /// And rimmed: of the ground around the patch that lies outside its band, at least this share stands
        /// above it. A strip of a slope has a lower side and fails; a level valley floor does not, and is a trough
        /// water would pool in. The lake's own edge, level but not flat by the window, is neither in nor rim.
        /// </summary>
        public const double LakeRimFraction = 0.85;

        /// <summary>Wetness at and above which flat ground is a swamp.</summary>
        public const float SwampWetness = 0.9f;
        public const double SwampMaxSlope = 0.05;

        /// <summary>A beach or a platform lies within this of the sea, m; a dune within <see cref="DuneReachM"/>.</summary>
        public const double ShoreReachM = 60.0;
        public const double DuneReachM = 600.0;
        public const double BeachMaxHeightM = 6.0;
        public const double PlatformMaxHeightM = 3.0;
        /// <summary>A hard coast rises this much within <see cref="HardCoastReachM"/> of the shore: a platform with a cliff behind it, not a beach with a dune.</summary>
        public const double HardCoastRiseM = 15.0;
        public const double HardCoastReachM = 150.0;
        public const double CliffSlope = 0.58;       // 30 degrees
        public const double CliffSeaReachM = 250.0;
        public const double SteepCliffSlope = 1.0;    // 45 degrees anywhere
        public const double CrestReachM = 60.0;

        /// <summary>The scale of the openness that feeds a site's exposure: height above the mean of this radius.</summary>
        public const double ExposureRadiusM = 200.0;
        public const double ExposureFullAtM = 12.0;
        /// <summary>Salt wind: exposure is at least this within a kilometre of the sea, falling with distance.</summary>
        public const double CoastWindReachM = 1000.0;

        /// <summary>
        /// Trunks to a crown's area of canopy (M1.6a). One to a crown's area would leave about a third of a canopy's
        /// ground under no crown, as scattered discs do; half as many again is the design, and `stand_check` prints
        /// the cover it comes to.
        /// </summary>
        public const double StandDensity = 1.5;

        /// <summary>How much of their two radii two crowns may overlap; nearer than that, the later trunk is not placed.</summary>
        public const double CrownOverlap = 0.4;

        /// <summary>Soil thinner than this over the older surfaces lets their stone lie loose, m.</summary>
        public const double ThinSoilM = 0.25;

        public RegionRaster Heights { get; }
        public DrainageNetwork Drainage { get; }
        public SoilModel Soil { get; }
        public int Width { get; }
        public int Height { get; }
        public double CellM { get; }

        /// <summary>A mapped water body from the bake's water_bodies layer (OpenStreetMap): its outline and kind, and for a lake the level it stands at and the cells under it.</summary>
        public sealed class WaterBody
        {
            public int Code;
            public string Name;
            /// <summary>lake, wetland or salt, as the bake tagged it.</summary>
            public string Kind;
            public string Source;
            public int OutlineCells;
            /// <summary>A lake's level: the median of the bake's ground inside its outline, m; NaN for anything else.</summary>
            public double LevelM = double.NaN;
            /// <summary>A lake's cells at or below its level; a wetland's cells above the sea.</summary>
            public int WaterCells;
        }

        /// <summary>The mapped bodies in the bake's order; empty without the layer.</summary>
        public IReadOnlyList<WaterBody> Bodies { get; private set; } = new List<WaterBody>();

        /// <summary>The heights with the sea floor applied, m.</summary>
        public float[] HeightsWithFloor { get; }
        /// <summary>The surface: the water's where water stands (the sea at its datum, a lake at its level), else the ground's, m.</summary>
        public float[] Surface { get; }
        public byte[] Water { get; }
        public float[] ShoreDistanceM { get; }
        public float[] FreshWaterDistanceM { get; }
        public float[] Slope { get; }
        public float[] Exposure { get; }
        /// <summary>The plant's number (<see cref="PlantSpecies.NumberOf"/>); zero for nothing.</summary>
        public byte[] Overstory { get; }
        public byte[] Understory { get; }
        /// <summary>The winning canopy's suitability, 0 to 1.</summary>
        public float[] Suitability { get; }
        public uint[] TopologyMask { get; }
        /// <summary>Stone index into <see cref="StoneType.All"/> plus one; zero for none.</summary>
        public byte[] Stone { get; }
        /// <summary>What covers each cell and how wet it is, packed as <see cref="GroundCovers"/> states.</summary>
        public byte[] Cover { get; }
        /// <summary>What stands on each cell (M1.6a): a <see cref="StandCodes"/> code, two bytes since WG.2c; zero where no trunk stands.</summary>
        public ushort[] Stand { get; }
        /// <summary>What lies loose on each cell (M1.6a): a <see cref="LooseCodes"/> code.</summary>
        public byte[] Loose { get; }
        /// <summary>Per species in <see cref="AnimalSpecies.All"/>, animals per km².</summary>
        public float[][] Capacity { get; }

        private WorldLayers(RegionRaster heights, RegionRaster waterBodies, Action<string> progress, Region region)
        {
            Heights = heights;
            Width = heights.Width;
            Height = heights.Height;
            CellM = heights.CellM;
            int count = Width * Height;
            HeightsWithFloor = new float[count];
            Surface = new float[count];
            _wetland = new bool[count];
            _level = new float[count];
            Water = new byte[count];
            ShoreDistanceM = new float[count];
            FreshWaterDistanceM = new float[count];
            Slope = new float[count];
            Exposure = new float[count];
            Overstory = new byte[count];
            Understory = new byte[count];
            Suitability = new float[count];
            TopologyMask = new uint[count];
            Stone = new byte[count];
            Cover = new byte[count];
            Stand = new ushort[count];
            Loose = new byte[count];
            Capacity = new float[AnimalSpecies.All.Count][];
            for (int s = 0; s < Capacity.Length; s++) Capacity[s] = new float[count];
            progress?.Invoke("Finding lakes and wetlands");
            _lake = Lakes(heights, waterBodies);
            progress?.Invoke("Finding where the lakes spill");
            bool[] held = HeldLakes(waterBodies, region);
            float[] ground = heights.Values.ToArray();
            float[] routing = Outlets(ground, held, region, waterBodies);
            progress?.Invoke("Tracing drainage");
            Drainage = new DrainageNetwork(ground, routing, Width, Height, CellM, Heightfield.SeaLevelM, held);
            progress?.Invoke("Forming soil");
            Soil = new SoilModel(Drainage);
        }

        private readonly bool[] _lake;
        private readonly bool[] _wetland;
        private readonly float[] _level;
        /// <summary>The water bake's codes of the lakes its region names as holding their water, and what the drainage was
        /// told of dams and ways out, for the census (WG.1b).</summary>
        private readonly HashSet<int> _heldCodes = new HashSet<int>();
        private readonly List<string> _waysOut = new List<string>();

        /// <summary>
        /// Runs the chain. Seconds for the region; the seed varies only the draws (the community, the stone). The
        /// water bodies are the bake's outlines (<c>Tools/data/bake_water.py</c>, OpenStreetMap), optional: without
        /// them the lakes are read off the ground alone. The region names the lakes that hold their water and the dams
        /// (WG.1b); by default the one the heights' sidecar names.
        /// </summary>
        public static WorldLayers Compute(RegionRaster heights, ulong seed, RegionRaster waterBodies = null, Action<string> progress = null, Region region = null)
        {
            if (heights == null) throw new ArgumentNullException(nameof(heights));
            ValidateInputs(heights, waterBodies);
            WorldLayers w = new WorldLayers(heights, waterBodies, progress, region ?? Region.ById(heights.RegionId));
            progress?.Invoke("Reading slopes and wind exposure");
            w.SlopeAndExposure();
            progress?.Invoke("Measuring the shore");
            w.Distances();
            progress?.Invoke("Preparing the sea floor");
            w.SeaFloor();
            progress?.Invoke("Preparing water");
            w.WaterBodies();
            progress?.Invoke("Reading landforms");
            w.Landforms();
            progress?.Invoke("Growing plant communities");
            w.Community(seed);
            w.Stands();
            progress?.Invoke("Reading the ground cover");
            w.Covers();
            progress?.Invoke("Finding stone");
            w.Stones(seed);
            progress?.Invoke("Standing the trees");
            w.StandTrees(seed);
            progress?.Invoke("Laying what lies on the ground");
            w.LayLoose(seed);
            progress?.Invoke("Calculating animal habitat");
            w.Capacities();
            return w;
        }

        /// <summary>
        /// WG.0b (2026-09-13): mapped outlines are joined to the heights by array index, so their cells must
        /// name the same places. Matching dimensions alone once let another region's lakes shape the soil
        /// and habitat. Refuse before allocating the layer chain; reprojecting is the bake's job.
        /// </summary>
        private static void ValidateInputs(RegionRaster heights, RegionRaster waterBodies)
        {
            Require(heights, "layer", heights.Layer, "heights");
            Require(heights, "unit", heights.Unit, "m");
            Frame(heights);
            if (waterBodies == null) return;
            Require(waterBodies, "layer", waterBodies.Layer, "water_bodies");
            Require(waterBodies, "unit", waterBodies.Unit, "id");
            Require(waterBodies, "scale", waterBodies.Scale, 1.0);
            if (!waterBodies.IsIntegral) Refuse(waterBodies, "dtype", waterBodies.Dtype, "integral IDs");
            Frame(waterBodies);
            Require(waterBodies, "region", waterBodies.RegionId, heights.RegionId);
            Require(waterBodies, "width", waterBodies.Width, heights.Width);
            Require(waterBodies, "height", waterBodies.Height, heights.Height);
            Require(waterBodies, "cell_m", waterBodies.CellM, heights.CellM);
            Require(waterBodies, "extent_m", waterBodies.ExtentM, heights.ExtentM);
            Require(waterBodies, "centre_lat", waterBodies.CentreLatDeg, heights.CentreLatDeg);
            Require(waterBodies, "centre_lon", waterBodies.CentreLonDeg, heights.CentreLonDeg);

            void Frame(RegionRaster raster)
            {
                if (!(raster.CellM > 0) || double.IsInfinity(raster.CellM))
                    Refuse(raster, "cell_m", raster.CellM, "finite and positive metres");
                if (!(raster.ExtentM > 0) || double.IsInfinity(raster.ExtentM))
                    Refuse(raster, "extent_m", raster.ExtentM, "finite and positive metres");
                if (!(raster.CentreLatDeg >= -90 && raster.CentreLatDeg <= 90))
                    Refuse(raster, "centre_lat", raster.CentreLatDeg, "finite degrees in [-90, 90]");
                if (!(raster.CentreLonDeg >= -180 && raster.CentreLonDeg <= 180))
                    Refuse(raster, "centre_lon", raster.CentreLonDeg, "finite degrees in [-180, 180]");
            }

            void Require<T>(RegionRaster raster, string field, T actual, T required)
            {
                if (!EqualityComparer<T>.Default.Equals(actual, required)) Refuse(raster, field, actual, required);
            }

            void Refuse(RegionRaster raster, string field, object actual, object required)
            {
                throw new InvalidDataException("World input '" + raster.Name + "' (" + raster.Layer + "), " + field
                    + ": actual '" + Convert.ToString(actual, CultureInfo.InvariantCulture)
                    + "'; required '" + Convert.ToString(required, CultureInfo.InvariantCulture) + "'.");
            }
        }

        private int Index(int row, int col) => row * Width + col;

        // ---- slope and exposure ----

        private void SlopeAndExposure()
        {
            ReadOnlySpan<float> z = Heights.Values;
            for (int r = 0; r < Height; r++)
            {
                int rm = Math.Max(0, r - 1), rp = Math.Min(Height - 1, r + 1);
                for (int c = 0; c < Width; c++)
                {
                    int cm = Math.Max(0, c - 1), cp = Math.Min(Width - 1, c + 1);
                    double gx = (z[Index(r, cp)] - z[Index(r, cm)]) / ((cp - cm) * CellM);
                    double gz = (z[Index(rp, c)] - z[Index(rm, c)]) / ((rp - rm) * CellM);
                    Slope[Index(r, c)] = (float)Math.Sqrt(gx * gx + gz * gz);
                }
            }
            // Openness: how far a cell stands above the mean of its surroundings, by a separable box mean.
            int radius = Math.Max(1, (int)Math.Round(ExposureRadiusM / CellM));
            float[] mean = BoxMean(z, radius);
            for (int i = 0; i < z.Length; i++)
                Exposure[i] = (float)SimMath.Clamp01((z[i] - mean[i]) / ExposureFullAtM);
        }

        private float[] BoxMean(ReadOnlySpan<float> z, int radius)
        {
            float[] rows = new float[z.Length];
            float[] result = new float[z.Length];
            for (int r = 0; r < Height; r++)
            {
                double sum = 0.0;
                int n = 0;
                for (int c = 0; c <= radius && c < Width; c++) { sum += z[Index(r, c)]; n++; }
                for (int c = 0; c < Width; c++)
                {
                    rows[Index(r, c)] = (float)(sum / n);
                    int leaving = c - radius, entering = c + radius + 1;
                    if (leaving >= 0) { sum -= z[Index(r, leaving)]; n--; }
                    if (entering < Width) { sum += z[Index(r, entering)]; n++; }
                }
            }
            for (int c = 0; c < Width; c++)
            {
                double sum = 0.0;
                int n = 0;
                for (int r = 0; r <= radius && r < Height; r++) { sum += rows[Index(r, c)]; n++; }
                for (int r = 0; r < Height; r++)
                {
                    result[Index(r, c)] = (float)(sum / n);
                    int leaving = r - radius, entering = r + radius + 1;
                    if (leaving >= 0) { sum -= rows[Index(leaving, c)]; n--; }
                    if (entering < Height) { sum += rows[Index(entering, c)]; n++; }
                }
            }
            return result;
        }

        // ---- distances ----

        /// <summary>Chessboard-with-diagonals distance from a set of source cells, in metres, by a breadth-first sweep.</summary>
        private float[] DistanceFrom(bool[] source)
        {
            int count = Width * Height;
            float[] d = new float[count];
            for (int i = 0; i < count; i++) d[i] = float.PositiveInfinity;
            var queue = new Queue<int>();
            for (int i = 0; i < count; i++)
                if (source[i]) { d[i] = 0f; queue.Enqueue(i); }
            float straight = (float)CellM, diagonal = (float)(CellM * 1.4142135623730951);
            int[] dx = { 1, 1, 0, -1, -1, -1, 0, 1 };
            int[] dz = { 0, 1, 1, 1, 0, -1, -1, -1 };
            while (queue.Count > 0)
            {
                int i = queue.Dequeue();
                int c = i % Width, r = i / Width;
                for (int k = 0; k < 8; k++)
                {
                    int nc = c + dx[k], nr = r + dz[k];
                    if (nc < 0 || nr < 0 || nc >= Width || nr >= Height) continue;
                    int n = nr * Width + nc;
                    float step = (dx[k] != 0 && dz[k] != 0) ? diagonal : straight;
                    if (d[i] + step < d[n] - 1e-3f)
                    {
                        d[n] = d[i] + step;
                        queue.Enqueue(n);
                    }
                }
            }
            return d;
        }

        private void Distances()
        {
            int count = Width * Height;
            bool[] shore = new bool[count];
            for (int r = 0; r < Height; r++)
                for (int c = 0; c < Width; c++)
                {
                    if (Drainage.IsSea(c, r)) continue;
                    bool nextToSea = false;
                    for (int dr = -1; dr <= 1 && !nextToSea; dr++)
                        for (int dc = -1; dc <= 1; dc++)
                        {
                            int nr = r + dr, nc = c + dc;
                            if (nr < 0 || nc < 0 || nr >= Height || nc >= Width) continue;
                            if (Drainage.IsSea(nc, nr)) { nextToSea = true; break; }
                        }
                    shore[Index(r, c)] = nextToSea;
                }
            float[] fromShore = DistanceFrom(shore);
            Array.Copy(fromShore, ShoreDistanceM, count);
        }

        private void SeaFloor()
        {
            ReadOnlySpan<float> z = Heights.Values;
            for (int r = 0; r < Height; r++)
                for (int c = 0; c < Width; c++)
                {
                    int i = Index(r, c);
                    if (!Drainage.IsSea(c, r)) { HeightsWithFloor[i] = z[i]; continue; }
                    double depth = Math.Min(SeaFloorMaxDepthM, SeaFloorGradient * ShoreDistanceM[i]);
                    HeightsWithFloor[i] = (float)(Heightfield.SeaLevelM - depth);
                }
        }

        // ---- water bodies ----

        /// <summary>
        /// The lakes: the ground's flats (a flat surface, one level, with a rim) and the bake's mapped outlines.
        /// Found before the drainage runs, because a lake its region holds is a sink for it and every other lake's way out
        /// is cut before it runs (<see cref="Outlets"/>, WG.1b): the fill that had to spill read Windermere's closed basin
        /// as a pond thirty metres deep.
        /// </summary>
        private bool[] Lakes(RegionRaster heights, RegionRaster waterBodies)
        {
            int count = Width * Height;
            ReadOnlySpan<float> z = heights.Values;
            // An outline the water bake left out as humanity's (a dam's lake, a farm's pond) holds no lake of the ground's own
            // making (WG.2b, 2026-09-23): the tiles carry a dam's water surface as flat ground at its level, and the flat rule
            // below filled it again, Fitzroy Falls Reservoir's 131 ha at 662 m in the whole valley with the wake set beside it.
            // Its cells seed no flat and join no patch; the drainage runs across them as it runs across any ground.
            bool[] leftOutCodes = HumanitysCodes(waterBodies);
            bool LeftOut(int row, int col)
            {
                if (leftOutCodes == null) return false;
                uint code = waterBodies.Code(row, col);
                return code < leftOutCodes.Length && leftOutCodes[code];
            }
            bool[] flat = new bool[count];
            for (int r = 0; r < Height; r++)
                for (int c = 0; c < Width; c++)
                {
                    if (z[Index(r, c)] <= Heightfield.SeaLevelM) continue;
                    float lo = float.MaxValue, hi = float.MinValue;
                    for (int dr = -2; dr <= 2; dr++)
                        for (int dc = -2; dc <= 2; dc++)
                        {
                            int nr = Math.Min(Height - 1, Math.Max(0, r + dr)), nc = Math.Min(Width - 1, Math.Max(0, c + dc));
                            float v = z[Index(nr, nc)];
                            if (v < lo) lo = v;
                            if (v > hi) hi = v;
                        }
                    flat[Index(r, c)] = hi - lo < LakeFlatnessM && !LeftOut(r, c);
                }
            // Grown from the lowest flat cell up, through the cells within the band of that level, so a lake is
            // one level; the rim is what makes it a lake: nearly all the ground around it stands higher.
            bool[] lake = new bool[count];
            bool[] seen = new bool[count];
            var stack = new Stack<int>();
            var patch = new List<int>();
            int flats = 0;
            for (int i = 0; i < count; i++) if (flat[i]) flats++;
            int[] order = new int[flats];
            float[] keys = new float[flats];
            for (int i = 0, k = 0; i < count; i++) if (flat[i]) { order[k] = i; keys[k] = z[i]; k++; }
            Array.Sort(keys, order);
            foreach (int start in order)
            {
                if (seen[start]) continue;
                float level = z[start];
                patch.Clear();
                stack.Push(start);
                seen[start] = true;
                int rim = 0, higher = 0;
                while (stack.Count > 0)
                {
                    int i = stack.Pop();
                    patch.Add(i);
                    int c = i % Width, r = i / Width;
                    for (int dr = -1; dr <= 1; dr++)
                        for (int dc = -1; dc <= 1; dc++)
                        {
                            int nr = r + dr, nc = c + dc;
                            if (nr < 0 || nc < 0 || nr >= Height || nc >= Width) continue;
                            if (LeftOut(nr, nc)) continue;
                            int n = Index(nr, nc);
                            bool inside = z[n] >= level && z[n] - level <= LakeLevelBandM;
                            if (!inside)
                            {
                                rim++;
                                if (z[n] - level > LakeLevelBandM) higher++;
                                continue;
                            }
                            if (seen[n]) continue;
                            seen[n] = true;
                            stack.Push(n);
                        }
                }
                if (patch.Count * CellM * CellM < MinLakeAreaM2) continue;
                if (rim == 0 || higher < LakeRimFraction * rim) continue;
                var levels = new List<float>(patch.Count);
                foreach (int i in patch) levels.Add(z[i]);
                levels.Sort();
                float surface = levels[levels.Count / 2];
                // The flat's extent says there is a lake here; its level says how much of the flat is under water.
                // A cell of the flat standing above that level is its margin, as it is for a mapped outline: until
                // 2026-09-10 every cell of the patch was made water at the median of the patch, so about half of a
                // dished flat held water below its own bed — 5.6 ha of the Bherwerre world, by up to half a metre.
                foreach (int i in patch)
                    if (z[i] <= surface) { lake[i] = true; _level[i] = surface; }
            }
            if (waterBodies != null) Mapped(z, waterBodies, lake);
            return lake;
        }

        /// <summary>Whether an outline's kind is humanity's water, which the water bake leaves out: a dam's lake or a pond (WG.2b).</summary>
        public static bool IsHumanitys(string kind) => kind == "reservoir" || kind == "pond";

        /// <summary>The codes of the outlines left out as humanity's, as a lookup by code; null when the bake names none.</summary>
        private static bool[] HumanitysCodes(RegionRaster waterBodies)
        {
            if (waterBodies == null || !waterBodies.Sidecar.Contains("bodies")) return null;
            bool[] codes = null;
            foreach (object entry in waterBodies.Sidecar.Array("bodies"))
            {
                if (!(entry is JsonObject j) || !IsHumanitys(j.StringOr("kind", "lake"))) continue;
                int code = j.Int("code");
                if (code <= 0) continue;
                if (codes == null) codes = new bool[256];
                if (code >= codes.Length) Array.Resize(ref codes, code + 1);
                codes[code] = true;
            }
            return codes;
        }

        /// <summary>
        /// The lake cells of every patch that touches the outline of a lake its region names as holding its water (WG.1b,
        /// 2026-09-24; <see cref="Region.HeldLakes"/>): a patch is the lake cells joined by any of the eight neighbours, so a lake
        /// the ground's flats and the bake's outline both found is held whole.
        /// </summary>
        private bool[] HeldLakes(RegionRaster waterBodies, Region region)
        {
            var held = new bool[Width * Height];
            if (region == null || region.HeldLakes.Count == 0 || waterBodies == null || !waterBodies.Sidecar.Contains("bodies")) return held;
            var codes = new HashSet<int>();
            foreach (object entry in waterBodies.Sidecar.Array("bodies"))
            {
                if (!(entry is JsonObject j)) continue;
                string name = j.StringOr("name", "");
                foreach (string heldName in region.HeldLakes)
                    if (string.Equals(heldName, name, StringComparison.OrdinalIgnoreCase)) codes.Add(j.Int("code"));
            }
            _heldCodes.UnionWith(codes);
            var stack = new Stack<int>();
            for (int r = 0; r < Height; r++)
                for (int c = 0; c < Width; c++)
                {
                    int i = Index(r, c);
                    if (_lake[i] && codes.Contains((int)waterBodies.Code(r, c))) { held[i] = true; stack.Push(i); }
                }
            while (stack.Count > 0)
            {
                int i = stack.Pop();
                int c0 = i % Width, r0 = i / Width;
                for (int dr = -1; dr <= 1; dr++)
                    for (int dc = -1; dc <= 1; dc++)
                    {
                        int nr = r0 + dr, nc = c0 + dc;
                        if (nr < 0 || nc < 0 || nr >= Height || nc >= Width) continue;
                        int n = Index(nr, nc);
                        if (!_lake[n] || held[n]) continue;
                        held[n] = true;
                        stack.Push(n);
                    }
            }
            return held;
        }

        /// <summary>How far below its own level an open lake's way out is cut, over the length of the cut, m.</summary>
        public const float OutletDropM = 0.01f;

        /// <summary>
        /// How far a dam's way may stray, m: to its reservoir, within three times this of the wall's point; down its creek,
        /// within the ellipse whose foci are the wall's point and the point below and whose distances to them sum to theirs and
        /// twice this. Without the bound the lowest way from Fitzroy Falls Dam to the falls runs round through the reservoir, over
        /// its saddle, down the escarpment and back up the gorge below the falls, none of which stands above the reservoir.
        /// </summary>
        public const double DamReachM = 100.0;

        /// <summary>
        /// The surface the water is routed on (WG.1b, 2026-09-24): the ground with each dam's wall let through and each open
        /// lake's way out cut where the ground holds it below its basin's lip. The ground itself is not changed.
        ///
        /// <para>Why a way out is cut and not filled: the tiles' source is SRTM, a surface model. Geoscience Australia made its
        /// 1-second ground model from it "by automatically removing vegetation offsets" and its hydrologically enforced model by
        /// enforcing mapped streams through that; the tiles keep the offsets, so a river under forest in a gorge narrower than a
        /// cell reads as high ground across it. The Kangaroo River's bed is lost 681 m below Hampden Bridge, 13.5 m above the pond
        /// its water ended in, and a basin filled to that lip would stand the village's floor 13.5 m deep.</para>
        ///
        /// <para>A dam's reservoir drains across its wall to the creek below, as the creek ran before it: the lowest way from the
        /// wall's published point to the reservoir (the nearest lake cell, or cell of an outline the water bake left out as
        /// humanity's) and on from the wall to the creek's published point below it, each kept near the wall
        /// (<see cref="DamReachM"/>), is cut to descend from the reservoir's ground to the point below. The level fill of that surface,
        /// the sea and the held lakes its sinks, then gives each cell the level its water spills at; an open lake whose basin
        /// spills above its own level has its lowest way out (the path out whose highest cell is lowest) cut to descend from its
        /// level to the first cell that spills lower, the grid's edge, or a way already cut. Lower lakes go first, so a lake
        /// above another in the same basin runs into the way cut for it.</para>
        /// </summary>
        private float[] Outlets(float[] ground, bool[] held, Region region, RegionRaster waterBodies)
        {
            int count = Width * Height;
            float[] routing = (float[])ground.Clone();
            if (region != null && region.Dams.Count > 0)
            {
                bool[] leftOutCodes = HumanitysCodes(waterBodies);
                bool Reservoir(int i)
                {
                    if (_lake[i]) return true;
                    if (leftOutCodes == null) return false;
                    uint code = waterBodies.Code(i / Width, i % Width);
                    return code < leftOutCodes.Length && leftOutCodes[code];
                }
                var frame = new LocalFrame(Heights.CentreLatDeg, Heights.CentreLonDeg, GeoMath.EarthRadiusM);
                var searched = new DrainageNetwork.CellHeap();
                var from = new Dictionary<int, int>();
                foreach (Dam dam in region.Dams)
                {
                    int wall = CellAt(frame, dam.LatitudeDeg, dam.LongitudeDeg), below = CellAt(frame, dam.BelowLatitudeDeg, dam.BelowLongitudeDeg);
                    if (wall < 0 || below < 0) continue;
                    double straight = Apart(wall, below);
                    List<int> up = LowestWay(routing, wall, i => i != wall && Reservoir(i), i => Apart(i, wall) <= 3.0 * DamReachM, searched, from);
                    List<int> down = LowestWay(routing, wall, i => i == below, i => Apart(i, wall) + Apart(i, below) <= straight + 2.0 * DamReachM, searched, from);
                    if (up == null || down == null) continue;
                    up.Reverse();
                    for (int k = 1; k < down.Count; k++) up.Add(down[k]);
                    float top = ground[up[0]], bottom = Math.Min(ground[below], top - OutletDropM);
                    for (int k = 0; k < up.Count; k++)
                    {
                        float cut = top - (top - bottom) * (k + 1) / up.Count;
                        if (routing[up[k]] > cut) routing[up[k]] = cut;
                    }
                    _waysOut.Add(dam.Name + ": its reservoir let across the wall and down the creek, a way of " + up.Count + " cells from "
                                 + top.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " m to " + bottom.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " m");
                }
            }
            var sinks = new bool[count];
            for (int i = 0; i < count; i++) sinks[i] = held[i] || ground[i] <= Heightfield.SeaLevelM;
            float[] spill = DrainageNetwork.LevelFill(routing, Width, Height, sinks);
            sinks = null;

            // The open lakes, each a patch with its level and its lowest cell, lowest level first.
            var seen = new bool[count];
            var lakes = new List<KeyValuePair<float, int[]>>();
            var stack = new Stack<int>();
            var cells = new List<int>();
            for (int s = 0; s < count; s++)
            {
                if (!_lake[s] || held[s] || seen[s]) continue;
                cells.Clear();
                seen[s] = true;
                stack.Push(s);
                float level = float.MinValue;
                int lowest = s;
                while (stack.Count > 0)
                {
                    int i = stack.Pop();
                    cells.Add(i);
                    if (_level[i] > level) level = _level[i];
                    if (ground[i] < ground[lowest]) lowest = i;
                    int c0 = i % Width, r0 = i / Width;
                    for (int dr = -1; dr <= 1; dr++)
                        for (int dc = -1; dc <= 1; dc++)
                        {
                            int nr = r0 + dr, nc = c0 + dc;
                            if (nr < 0 || nc < 0 || nr >= Height || nc >= Width) continue;
                            int n = Index(nr, nc);
                            if (!_lake[n] || held[n] || seen[n]) continue;
                            seen[n] = true;
                            stack.Push(n);
                        }
                }
                if (spill[lowest] > level + OutletDropM) lakes.Add(new KeyValuePair<float, int[]>(level, cells.ToArray()));
            }
            seen = null;
            if (lakes.Count == 0) return routing;
            // Lowest level first; lakes at one level in the order their first cells lie in the grid (each patch's first cell is
            // its lowest index, the scan's), so the order is the stated one whatever the sort's.
            lakes.Sort((a, b) => a.Key != b.Key ? a.Key.CompareTo(b.Key) : a.Value[0].CompareTo(b.Value[0]));

            int[] came = new int[count];
            for (int i = 0; i < count; i++) came[i] = -2;
            var touched = new List<int>();
            var frontier = new DrainageNetwork.CellHeap();
            var path = new List<int>();
            int cutCount = 0, longest = 0;
            foreach (KeyValuePair<float, int[]> lake in lakes)
            {
                float level = lake.Key;
                frontier.Clear();
                touched.Clear();
                foreach (int i in lake.Value)
                {
                    came[i] = -1;
                    touched.Add(i);
                    frontier.Push(i, routing[i], routing[i]);
                }
                int end = -1;
                while (frontier.Count > 0)
                {
                    int i = frontier.Pop(out float key);
                    int c0 = i % Width, r0 = i / Width;
                    if (came[i] != -1 && (spill[i] < level - OutletDropM || (routing[i] < ground[i] && routing[i] < level - OutletDropM)
                                          || r0 == 0 || c0 == 0 || r0 == Height - 1 || c0 == Width - 1))
                    {
                        end = i;
                        break;
                    }
                    for (int dr = -1; dr <= 1; dr++)
                        for (int dc = -1; dc <= 1; dc++)
                        {
                            int nr = r0 + dr, nc = c0 + dc;
                            if (nr < 0 || nc < 0 || nr >= Height || nc >= Width) continue;
                            int n = Index(nr, nc);
                            if (came[n] != -2) continue;
                            came[n] = i;
                            touched.Add(n);
                            // Among ways out over the same highest cell the lower ground first: past the lip the way drops
                            // into the channel below it rather than running along the slope beside it.
                            frontier.Push(n, Math.Max(key, routing[n]), routing[n]);
                        }
                }
                if (end >= 0)
                {
                    path.Clear();
                    for (int i = end; i != -1; i = came[i]) path.Add(i);
                    path.Reverse();
                    for (int k = 0; k < path.Count; k++)
                    {
                        float cut = level - OutletDropM * (k + 1) / path.Count;
                        if (routing[path[k]] > cut) routing[path[k]] = cut;
                    }
                    cutCount++;
                    longest = Math.Max(longest, path.Count);
                }
                foreach (int i in touched) came[i] = -2;
            }
            _waysOut.Add("the ways out cut for the lakes the ground holds below their lip: " + cutCount + ", the longest " + longest + " cells");
            return routing;
        }

        /// <summary>The cell a published point falls in, by the sidecar's frame rule; -1 outside the grid.</summary>
        private int CellAt(LocalFrame frame, double latitudeDeg, double longitudeDeg)
        {
            frame.FromLatLon(latitudeDeg, longitudeDeg, out double east, out double north);
            double half = Heights.ExtentM / 2.0;
            int r = (int)Math.Round((half - north) / CellM), c = (int)Math.Round((east + half) / CellM);
            return r < 0 || c < 0 || r >= Height || c >= Width ? -1 : Index(r, c);
        }

        /// <summary>The distance between two cells' centres, m.</summary>
        private double Apart(int a, int b)
        {
            int dr = a / Width - b / Width, dc = a % Width - b % Width;
            return CellM * Math.Sqrt((double)dr * dr + (double)dc * dc);
        }

        /// <summary>
        /// The lowest way from a cell to the first cell that ends it, through the cells allowed: the search takes the way whose
        /// highest cell is lowest, then the lower ground, then the lower cell number, as <see cref="Outlets"/>' does. The path,
        /// start first, or null when nothing allowed ends it.
        /// </summary>
        private List<int> LowestWay(float[] routing, int start, Func<int, bool> ends, Func<int, bool> allowed, DrainageNetwork.CellHeap frontier, Dictionary<int, int> came)
        {
            frontier.Clear();
            came.Clear();
            came[start] = -1;
            frontier.Push(start, routing[start], routing[start]);
            while (frontier.Count > 0)
            {
                int i = frontier.Pop(out float key);
                if (ends(i))
                {
                    var path = new List<int>();
                    for (int j = i; j != -1; j = came[j]) path.Add(j);
                    path.Reverse();
                    return path;
                }
                int c0 = i % Width, r0 = i / Width;
                for (int dr = -1; dr <= 1; dr++)
                    for (int dc = -1; dc <= 1; dc++)
                    {
                        int nr = r0 + dr, nc = c0 + dc;
                        if (nr < 0 || nc < 0 || nr >= Height || nc >= Width) continue;
                        int n = Index(nr, nc);
                        if (came.ContainsKey(n) || !allowed(n)) continue;
                        came[n] = i;
                        frontier.Push(n, Math.Max(key, routing[n]), routing[n]);
                    }
            }
            return null;
        }

        /// <summary>
        /// The bake's outlines. The tiles are noise over these lakes (Windermere's outline holds ground from 12 m to
        /// 50 m, McKenzie's a bowl from 21 m to 57 m; 2026-09-09), so a lake's level is the median of the ground
        /// inside its outline and its water is the outline's cells at or below that level, the rest being margin;
        /// a wetland is swamp throughout its outline; a salt body is recorded and left to the sea's own rule.
        /// </summary>
        private void Mapped(ReadOnlySpan<float> z, RegionRaster waterBodies, bool[] lake)
        {
            var bodies = new List<WaterBody>();
            var byCode = new Dictionary<int, WaterBody>();
            if (waterBodies.Sidecar.Contains("bodies"))
                foreach (object entry in waterBodies.Sidecar.Array("bodies"))
                {
                    if (!(entry is JsonObject j)) continue;
                    var body = new WaterBody { Code = j.Int("code"), Name = j.StringOr("name", ""), Kind = j.StringOr("kind", "lake"), Source = j.StringOr("osm", "") };
                    bodies.Add(body);
                    byCode[body.Code] = body;
                }
            var cells = new Dictionary<int, List<int>>();
            ReadOnlySpan<uint> codes = waterBodies.Codes;
            for (int i = 0; i < codes.Length; i++)
            {
                int code = (int)codes[i];
                if (code == 0) continue;
                if (!cells.TryGetValue(code, out List<int> list)) cells[code] = list = new List<int>();
                list.Add(i);
            }
            foreach (KeyValuePair<int, List<int>> pair in cells)
            {
                if (!byCode.TryGetValue(pair.Key, out WaterBody body))
                {
                    body = new WaterBody { Code = pair.Key, Name = "", Kind = "lake", Source = "" };
                    bodies.Add(body);
                    byCode[pair.Key] = body;
                }
                body.OutlineCells = pair.Value.Count;
                if (body.Kind == "wetland")
                {
                    foreach (int i in pair.Value)
                        if (z[i] > Heightfield.SeaLevelM && !lake[i]) { _wetland[i] = true; body.WaterCells++; }
                    continue;
                }
                if (body.Kind != "lake") continue;
                var ground = new List<float>(pair.Value.Count);
                foreach (int i in pair.Value) if (z[i] > Heightfield.SeaLevelM) ground.Add(z[i]);
                if (ground.Count == 0) continue;
                ground.Sort();
                float level = ground[ground.Count / 2];
                body.LevelM = level;
                foreach (int i in pair.Value)
                    if (z[i] > Heightfield.SeaLevelM && z[i] <= level) { lake[i] = true; _level[i] = level; body.WaterCells++; }
            }
            bodies.Sort((a, b) => a.Code.CompareTo(b.Code));
            Bodies = bodies;
        }

        private void WaterBodies()
        {
            int count = Width * Height;
            bool[] fresh = new bool[count];
            for (int r = 0; r < Height; r++)
                for (int c = 0; c < Width; c++)
                {
                    int i = Index(r, c);
                    WaterClass wc;
                    if (Drainage.IsSea(c, r)) wc = WaterClass.Sea;
                    else if (_lake[i]) wc = WaterClass.Lake;
                    else if (_wetland[i]) wc = WaterClass.Swamp;
                    else
                    {
                        Channel ch = Drainage.ChannelAt(c, r);
                        if (ch == Channel.Stream) wc = WaterClass.Stream;
                        else if (ch == Channel.Creek) wc = WaterClass.Creek;
                        else if (Soil.WetnessAt(c, r) >= SwampWetness && Slope[i] < SwampMaxSlope) wc = WaterClass.Swamp;
                        else if (ch == Channel.Trickle) wc = WaterClass.Trickle;
                        else if (ch == Channel.Damp) wc = WaterClass.Damp;
                        else wc = WaterClass.Dry;
                    }
                    Water[i] = (byte)wc;
                    fresh[i] = wc == WaterClass.Creek || wc == WaterClass.Stream || wc == WaterClass.Lake;
                }
            float[] fromFresh = DistanceFrom(fresh);
            Array.Copy(fromFresh, FreshWaterDistanceM, count);
            ReadOnlySpan<float> z = Heights.Values;
            for (int r = 0; r < Height; r++)
                for (int c = 0; c < Width; c++)
                {
                    int i = Index(r, c);
                    if (Water[i] == (byte)WaterClass.Sea) Surface[i] = (float)Heightfield.SeaLevelM;
                    else if (_lake[i]) Surface[i] = _level[i];
                    else if (Water[i] == (byte)WaterClass.Creek || Water[i] == (byte)WaterClass.Stream) Surface[i] = z[i] + (float)ChannelDepthM(Drainage.CatchmentM2(c, r));
                    else Surface[i] = z[i];
                }
        }

        /// <summary>How deep a creek runs at the catchment that first keeps one running (<see cref="DrainageNetwork.CreekM2"/>), m: ankle-deep.</summary>
        public const double CreekDepthM = 0.15;

        /// <summary>
        /// How a channel deepens with its catchment: the depth grows as the catchment to this power, the downstream hydraulic
        /// geometry of Leopold and Maddock (USGS Professional Paper 252, 1953: depth as the discharge to about 0.4, the
        /// discharge of these small coastal catchments taken as their area).
        /// </summary>
        public const double ChannelDepthExponent = 0.4;

        /// <summary>The deepest a stream runs, m: waist-deep, past which the raster's four metres hold no channel's shape anyway.</summary>
        public const double StreamDepthMaxM = 0.8;

        /// <summary>
        /// The water a creek or stream carries over its bed, m, from the catchment through the cell (2026-09-18, on William's
        /// word that the creeks get water first, CANON ruling 26 as amended): nothing under the creek's catchment, where a
        /// trickle runs under the leaves and the drink verb finds nothing; <see cref="CreekDepthM"/> at it; deepening as the
        /// catchment to <see cref="ChannelDepthExponent"/>; held at <see cref="StreamDepthMaxM"/>. Until then every creek and
        /// stream cell's surface was its ground, and the path's first drink, from the creek by the wake, could not be had.
        /// </summary>
        public static double ChannelDepthM(double catchmentM2)
        {
            if (catchmentM2 < DrainageNetwork.CreekM2) return 0.0;
            return Math.Min(StreamDepthMaxM, CreekDepthM * Math.Pow(catchmentM2 / DrainageNetwork.CreekM2, ChannelDepthExponent));
        }

        /// <summary>
        /// The water read out as country, for the census: how much of each class there is, and each mapped body by
        /// name with the area that stands under its level, so a person who knows the lakes can say which is wrong.
        /// </summary>
        public string WaterCensus()
        {
            int count = Width * Height;
            int[] cells = new int[8];
            for (int i = 0; i < count; i++) cells[Water[i]]++;
            double ha = CellM * CellM / 10000.0;
            var sb = new System.Text.StringBuilder();
            sb.Append("the water: sea ").Append(Ha(cells[(int)WaterClass.Sea] * ha)).Append(" (salt); lakes ").Append(Ha(cells[(int)WaterClass.Lake] * ha))
              .Append(", swamp ").Append(Ha(cells[(int)WaterClass.Swamp] * ha)).Append(", streams ").Append(Ha(cells[(int)WaterClass.Stream] * ha))
              .Append(", creeks ").Append(Ha(cells[(int)WaterClass.Creek] * ha)).Append(", trickles ").Append(Ha(cells[(int)WaterClass.Trickle] * ha)).Append(" (fresh)").Append('\n');
            int mappedLake = 0;
            foreach (WaterBody b in Bodies)
            {
                string name = b.Name.Length > 0 ? b.Name : "an unnamed " + b.Kind;
                if (b.Kind == "lake" && !double.IsNaN(b.LevelM))
                {
                    mappedLake += b.WaterCells;
                    sb.Append("  ").Append(name).Append(": ").Append(Ha(b.WaterCells * ha)).Append(" of water at ").Append(b.LevelM.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture))
                      .Append(" m inside an outline of ").Append(Ha(b.OutlineCells * ha)).Append(_heldCodes.Contains(b.Code) ? ", holding its water" : "")
                      .Append(" (").Append(b.Source).Append(")").Append('\n');
                }
                else if (b.Kind == "wetland")
                    sb.Append("  ").Append(name).Append(": ").Append(Ha(b.WaterCells * ha)).Append(" of swamp (").Append(b.Source).Append(")").Append('\n');
                else if (IsHumanitys(b.Kind))
                    sb.Append("  ").Append(name).Append(": an outline of ").Append(Ha(b.OutlineCells * ha)).Append(", a ").Append(b.Kind)
                      .Append(" left out as humanity's, no lake in it (").Append(b.Source).Append(")").Append('\n');
                else
                    sb.Append("  ").Append(name).Append(": an outline of ").Append(Ha(b.OutlineCells * ha)).Append(", ").Append(b.Kind).Append(", not used (").Append(b.Source).Append(")").Append('\n');
            }
            sb.Append("  lakes read off the ground alone: ").Append(Ha(Math.Max(0, cells[(int)WaterClass.Lake] - mappedLake) * ha)).Append('\n');
            foreach (string line in _waysOut) sb.Append("  ").Append(line).Append('\n');
            return sb.ToString();
        }

        private static string Ha(double hectares) => hectares.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " ha";

        /// <summary>Whether the founder could drink here: a creek, a stream or a lake.</summary>
        public static bool IsFresh(WaterClass wc) => wc == WaterClass.Creek || wc == WaterClass.Stream || wc == WaterClass.Lake;

        // ---- the site a cell offers a plant ----

        /// <summary>The site at a cell, before any canopy: wetness from the soil, the soil a root can use, slope and openness from the ground, salt wind from the coast.</summary>
        public PlantSite SiteAt(int row, int col)
        {
            int i = Index(row, col);
            double coastWind = SimMath.Clamp01(1.0 - ShoreDistanceM[i] / CoastWindReachM);
            return new PlantSite
            {
                Wetness = Soil.WetnessAt(col, row),
                SoilDepthM = RootingDepthM(i, coastWind),
                Slope = Slope[i],
                Exposure = Math.Max(Exposure[i], coastWind),
                Shaded = false,
            };
        }

        /// <summary>
        /// The soil a root can use, m: the soil model's depth, except on the sand the sea has laid down (M1.2b,
        /// 2026-09-10).
        ///
        /// <para>The soil model measures the loose material over the rock, and under a beach or a dune that is sand
        /// piled up, not soil formed. Soil forms on sand only where plants hold it long enough to build it, and the
        /// salt wind is what stops them: so the beach, which the waves still rework, has none, and a dune has the
        /// soil model's depth less the share of it the salt wind reaches. The young dune is left to the sand-binder,
        /// and the plants that want soil start where it has formed. The animals are handed the same site, so a
        /// burrower's ground thins on the young dune with the plants'.</para>
        ///
        /// <para>Found by holding the world against the Atlas of Living Australia's records: read as soil, the loose
        /// sand kept spinifex off every dune (its ceiling is 20 cm) and grew bracken, a plant of the forest floor,
        /// over half the ground within 100 m of the sea.</para>
        /// </summary>
        private double RootingDepthM(int i, double coastWind)
        {
            uint bits = TopologyMask[i];
            if ((bits & (uint)Engine.Topology.Beach) != 0) return 0.0;
            double depth = Soil.DepthM[i];
            return (bits & (uint)Engine.Topology.Dune) != 0 ? depth * (1.0 - coastWind) : depth;
        }

        private void Community(ulong seed)
        {
            ulong stream = SimRandom.DeriveSeed(seed, "community");
            Rows(r =>
            {
                for (int c = 0; c < Width; c++)
                {
                    int i = Index(r, c);
                    if (Drainage.IsSea(c, r) || Water[i] == (byte)WaterClass.Lake) continue;
                    PlantSite site = SiteAt(r, c);
                    var rng = new SimRandom(SimRandom.DeriveSeed(stream, i.ToString()));
                    double rollCanopy = rng.NextDouble();
                    double rollUnder = rng.NextDouble();
                    // Drawn third, so the two draws every world has made since M1.2 keep their values.
                    double rollStand = rng.NextDouble();
                    PlantSpecies canopy = PlantCommunity.Canopy(site, rollStand, rollCanopy);
                    Overstory[i] = (byte)PlantSpecies.NumberOf(canopy);
                    Suitability[i] = canopy == null ? 0f : (float)canopy.Suitability(site);
                    PlantSite under = site;
                    under.Shaded = canopy != null;
                    PlantSpecies floor = PlantCommunity.Understory(under, rollUnder);
                    Understory[i] = (byte)PlantSpecies.NumberOf(floor);
                }
            });
        }

        public PlantSpecies OverstoryAt(int row, int col) => PlantSpecies.ByNumber(Overstory[Index(row, col)]);

        public PlantSpecies UnderstoryAt(int row, int col) => PlantSpecies.ByNumber(Understory[Index(row, col)]);

        // ---- topology ----

        /// <summary>
        /// The ground's own shapes: the sea, the shore, the beach and the dune, the cliffs, the water and the crests.
        /// Read before anything grows, because the plants read them — nothing roots in a beach — and the plants' own
        /// marks, the forest and the heath, are added by <see cref="Stands"/> once they have grown.
        /// </summary>
        private void Landforms()
        {
            int reach = Math.Max(1, (int)Math.Round(HardCoastReachM / CellM));
            int crest = Math.Max(1, (int)Math.Round(CrestReachM / CellM));
            Rows(r =>
            {
                ReadOnlySpan<float> z = Heights.Values;
                for (int c = 0; c < Width; c++)
                {
                    int i = Index(r, c);
                    uint bits = 0;
                    if (Drainage.IsSea(c, r)) { TopologyMask[i] = (uint)Engine.Topology.Sea; continue; }
                    float h = z[i];
                    double slope = Slope[i];
                    double shore = ShoreDistanceM[i];
                    bool hardCoast = false;
                    if (shore <= DuneReachM)
                    {
                        float highest = h;
                        for (int dr = -reach; dr <= reach; dr++)
                            for (int dc = -reach; dc <= reach; dc++)
                            {
                                int nr = r + dr, nc = c + dc;
                                if (nr < 0 || nc < 0 || nr >= Height || nc >= Width) continue;
                                float v = z[Index(nr, nc)];
                                if (v > highest) highest = v;
                            }
                        hardCoast = highest - h >= HardCoastRiseM && shore <= ShoreReachM;
                    }
                    if (shore <= ShoreReachM && h <= PlatformMaxHeightM && hardCoast) bits |= (uint)Engine.Topology.ShorePlatform;
                    else if (shore <= ShoreReachM && h <= BeachMaxHeightM && Water[i] != (byte)WaterClass.Lake) bits |= (uint)Engine.Topology.Beach;
                    if ((slope >= CliffSlope && shore <= CliffSeaReachM) || slope >= SteepCliffSlope) bits |= (uint)Engine.Topology.Cliff;
                    if (Water[i] == (byte)WaterClass.Lake) bits |= (uint)Engine.Topology.Lake;
                    if (Water[i] == (byte)WaterClass.Swamp) bits |= (uint)Engine.Topology.Wetland;
                    if (Water[i] == (byte)WaterClass.Creek || Water[i] == (byte)WaterClass.Stream) bits |= (uint)Engine.Topology.Creek;
                    if (shore <= DuneReachM && shore > ShoreReachM && h > PlatformMaxHeightM && !hardCoast && (bits & (uint)Engine.Topology.Wetland) == 0 && (bits & (uint)Engine.Topology.Lake) == 0)
                        bits |= (uint)Engine.Topology.Dune;
                    bool top = true;
                    for (int dr = -crest; dr <= crest && top; dr++)
                        for (int dc = -crest; dc <= crest; dc++)
                        {
                            int nr = r + dr, nc = c + dc;
                            if (nr < 0 || nc < 0 || nr >= Height || nc >= Width) continue;
                            if (z[Index(nr, nc)] > h) { top = false; break; }
                        }
                    if (top && Exposure[i] > 0.3f) bits |= (uint)Engine.Topology.Crest;
                    TopologyMask[i] = bits;
                }
            });
        }

        /// <summary>The marks the plants leave on the landforms once they have grown: forest where a tree stands, heath where a shrub is the ground layer.</summary>
        private void Stands()
        {
            for (int r = 0; r < Height; r++)
                for (int c = 0; c < Width; c++)
                {
                    PlantSpecies canopy = OverstoryAt(r, c);
                    PlantSpecies floor = UnderstoryAt(r, c);
                    if (canopy != null && canopy.Form == PlantForm.Tree) TopologyMask[Index(r, c)] |= (uint)Engine.Topology.Forest;
                    else if (floor != null && floor.Form == PlantForm.Shrub) TopologyMask[Index(r, c)] |= (uint)Engine.Topology.Heath;
                }
        }

        public bool Has(int row, int col, Topology bit) => (TopologyMask[Index(row, col)] & (uint)bit) != 0;

        // ---- ground cover ----

        /// <summary>
        /// What covers each cell (M1.4d): the one thing the client is sent about the ground's appearance, because
        /// the fields it comes from cost more on the wire than the ground itself. The rule is
        /// <see cref="GroundCovers.Of"/>; this only feeds it what this world made.
        /// </summary>
        private void Covers()
        {
            for (int r = 0; r < Height; r++)
                for (int c = 0; c < Width; c++)
                {
                    int i = Index(r, c);
                    GroundCover cover = GroundCovers.Of((WaterClass)Water[i], TopologyMask[i], UnderstoryAt(r, c), OverstoryAt(r, c), Soil.DepthM[i]);
                    Cover[i] = GroundCovers.Pack(cover, GroundCovers.QuarterFor(Soil.Wetness01[i]));
                }
        }

        // ---- stone ----

        /// <summary>
        /// What stone lies about at a cell, from where the cell is: beach pebbles on the platforms and the beaches,
        /// silcrete and quartz on the old sand surfaces, the sandstone of the cliffs with silcrete caps and quartz
        /// veins on the higher ground, and nothing in the swamps and lakes. Which of a place's stones a province
        /// draws is the lattice's; that a place has those stones is the topology's (ECOSYSTEM.md, the stones).
        /// </summary>
        private void Stones(ulong seed)
        {
            double half = Heights.ExtentM * 0.5;
            Rows(r =>
            {
                ReadOnlySpan<float> z = Heights.Values;
                for (int c = 0; c < Width; c++)
                {
                    int i = Index(r, c);
                    uint bits = TopologyMask[i];
                    if ((bits & (uint)(Engine.Topology.Sea | Engine.Topology.Lake | Engine.Topology.Wetland)) != 0) { Stone[i] = 0; continue; }
                    double east = c * CellM - half, north = half - r * CellM;
                    double roll = (GeologyScale.ProvinceHash(east, north, seed) & 0xFFFF) / 65535.0;
                    StoneType stone;
                    if ((bits & (uint)(Engine.Topology.ShorePlatform | Engine.Topology.Beach)) != 0)
                        stone = roll < 0.45 ? StoneType.Rhyolite : roll < 0.80 ? StoneType.Quartz : StoneType.Quartzite;
                    else if ((bits & (uint)Engine.Topology.Cliff) != 0)
                        stone = roll < 0.80 ? StoneType.Sandstone : StoneType.Quartz;
                    else if ((bits & (uint)Engine.Topology.Dune) != 0 || z[i] < 30f)
                        stone = roll < 0.50 ? StoneType.Quartz : roll < 0.80 ? StoneType.Silcrete : StoneType.Sandstone;
                    else
                        stone = roll < 0.55 ? StoneType.Sandstone : roll < 0.85 ? StoneType.Silcrete : StoneType.Quartz;
                    Stone[i] = (byte)(IndexOfStone(stone) + 1);
                }
            });
        }

        private static int IndexOfStone(StoneType stone)
        {
            IReadOnlyList<StoneType> all = StoneType.All;
            for (int i = 0; i < all.Count; i++) if (ReferenceEquals(all[i], stone)) return i;
            return -1;
        }

        public StoneType StoneAt(int row, int col)
        {
            byte id = Stone[Index(row, col)];
            return id == 0 ? null : StoneType.All[id - 1];
        }

        // ---- what stands and lies on the ground ----

        /// <summary>
        /// Individual trees, from the canopy (M1.6a, 2026-09-10). Every cell a canopy stands on is a candidate trunk of
        /// the canopy's species, as tall as that species grows there; the candidates are taken in an order the seed
        /// draws, and each is placed with the chance that <see cref="StandDensity"/> trunks to a crown's area of canopy
        /// gives, unless a trunk already stands nearer than their two crowns allow (<see cref="CrownOverlap"/>, cell
        /// centre to cell centre). So the crowns cover about the ground the canopy layer says is covered, the big trees
        /// stand farther apart than the small, and no trunk stands where no canopy does. Nothing is a spawn table:
        /// where a tree stands follows from where the canopy is, and the canopy from the country (CANON ruling 22).
        /// </summary>
        private void StandTrees(ulong seed)
        {
            int count = Width * Height;
            ulong stream = SimRandom.DeriveSeed(seed, "stand");
            int candidates = 0;
            for (int i = 0; i < count; i++) if (Overstory[i] != 0) candidates++;
            // The seed's order: the top half of each key is the cell's hash, the bottom its index, so no two keys tie
            // and every sort of them agrees.
            ulong[] order = new ulong[candidates];
            int n = 0;
            for (int i = 0; i < count; i++)
                if (Overstory[i] != 0) order[n++] = (CellHash(stream, i, 0) & 0xFFFFFFFF00000000UL) | (uint)i;
            Array.Sort(order);

            double largest = 0.0;
            foreach (PlantSpecies tall in StandCodes.Tall) largest = Math.Max(largest, 0.5 * tall.CrownShare * tall.MaxHeightM);
            float[] crown = new float[count];
            double cellArea = CellM * CellM;
            foreach (ulong key in order)
            {
                int i = (int)(key & 0xFFFFFFFFUL);
                int r = i / Width, c = i % Width;
                PlantSpecies species = OverstoryAt(r, c);
                if (!StandCodes.IsTall(species) || species.CrownShare <= 0.0) continue;
                ushort code = StandCodes.Pack(species, species.HeightAt(SiteAt(r, c), Unit(CellHash(stream, i, 2))));
                double radius = 0.5 * species.CrownShare * StandCodes.HeightOf(code);
                if (Unit(CellHash(stream, i, 1)) >= StandDensity * cellArea / (Math.PI * radius * radius)) continue;
                if (Crowded(r, c, radius, largest, crown)) continue;
                crown[i] = (float)radius;
                Stand[i] = code;
            }
        }

        /// <summary>Whether a trunk already stands nearer to this cell than the two crowns allow.</summary>
        private bool Crowded(int row, int col, double radius, double largest, float[] crown)
        {
            int reach = (int)Math.Ceiling((1.0 - CrownOverlap) * (radius + largest) / CellM);
            for (int dr = -reach; dr <= reach; dr++)
            {
                int r = row + dr;
                if (r < 0 || r >= Height) continue;
                for (int dc = -reach; dc <= reach; dc++)
                {
                    int c = col + dc;
                    if (c < 0 || c >= Width) continue;
                    float other = crown[Index(r, c)];
                    if (other <= 0f) continue;
                    if (CellM * Math.Sqrt(dr * dr + dc * dc) < (1.0 - CrownOverlap) * (radius + other)) return true;
                }
            }
            return false;
        }

        /// <summary>
        /// What lies loose on each cell (M1.6a): the sticks each tree has dropped inside its crown, more under a taller
        /// tree and under the species that shed more, and cobbles where the ground says stone lies loose — a shore
        /// platform, a cliff, a creek's or a stream's bed, now and then a beach, and the thin soil over the stone of
        /// the older surfaces; none on a dune, on deep soil, or under the sea, a lake or a swamp. Which stone a cobble
        /// is, is the stone layer's.
        /// </summary>
        private void LayLoose(ulong seed)
        {
            int count = Width * Height;
            ulong stream = SimRandom.DeriveSeed(seed, "loose");
            int[] sticks = new int[count];
            for (int i = 0; i < count; i++)
            {
                ushort code = Stand[i];
                if (code == 0) continue;
                PlantSpecies species = StandCodes.SpeciesOf(code);
                double height = StandCodes.HeightOf(code);
                double radius = 0.5 * species.CrownShare * height;
                int shed = (int)Math.Round(species.SticksPerMetre * height);
                int row = i / Width, col = i % Width;
                for (int k = 0; k < shed; k++)
                {
                    // Uniform over the crown's disc: the square root spreads the sticks out to the rim.
                    double d = radius * Math.Sqrt(Unit(CellHash(stream, i, (ulong)(2 * k + 1))));
                    double a = 2.0 * Math.PI * Unit(CellHash(stream, i, (ulong)(2 * k + 2)));
                    int r = row - (int)Math.Round(d * Math.Cos(a) / CellM);
                    int c = col + (int)Math.Round(d * Math.Sin(a) / CellM);
                    if (r < 0 || c < 0 || r >= Height || c >= Width) continue;
                    int j = Index(r, c);
                    if (HoldsLoose(j)) sticks[j]++;
                }
            }
            for (int i = 0; i < count; i++)
                Loose[i] = LooseCodes.Pack(sticks[i], HoldsLoose(i) ? CobblesAt(i, CellHash(stream, i, 0)) : 0);
        }

        /// <summary>Whether anything can lie loose on a cell: not under the sea, a lake or a swamp.</summary>
        private bool HoldsLoose(int i)
        {
            WaterClass water = (WaterClass)Water[i];
            return water != WaterClass.Sea && water != WaterClass.Lake && water != WaterClass.Swamp;
        }

        /// <summary>
        /// How many cobbles lie on a cell, from what its ground is. A dune is asked first: it is sand whatever runs across
        /// it, and a creek cutting the dune has a bed of sand, not stone (the made coast's swale, 2026-09-11).
        /// </summary>
        private int CobblesAt(int i, ulong roll)
        {
            uint bits = TopologyMask[i];
            WaterClass water = (WaterClass)Water[i];
            if ((bits & (uint)Engine.Topology.Dune) != 0) return 0;
            if ((bits & (uint)Engine.Topology.ShorePlatform) != 0) return 2 + (int)(roll % 4);
            if ((bits & (uint)Engine.Topology.Cliff) != 0) return 1 + (int)(roll % 2);
            if (water == WaterClass.Creek || water == WaterClass.Stream) return 1 + (int)(roll % 3);
            if ((bits & (uint)Engine.Topology.Beach) != 0) return roll % 8 == 0 ? 1 : 0;
            return Soil.DepthM[i] < ThinSoilM ? (int)(roll % 3) : 0;
        }

        /// <summary>A cell's own draw from a stream, by salt: whole numbers, so the same seed gives the same world.</summary>
        private static ulong CellHash(ulong stream, int i, ulong salt) => StandLayout.Mix(StandLayout.Mix(stream ^ (uint)i) ^ salt);

        /// <summary>A draw as a double in [0, 1), from its top 53 bits.</summary>
        private static double Unit(ulong h) => (h >> 11) * (1.0 / 9007199254740992.0);

        // ---- the animals ----

        private void Capacities()
        {
            IReadOnlyList<AnimalSpecies> species = AnimalSpecies.All;
            Rows(r =>
            {
                for (int c = 0; c < Width; c++)
                {
                    int i = Index(r, c);
                    if (Drainage.IsSea(c, r)) continue;
                    PlantSite site = SiteAt(r, c);
                    for (int s = 0; s < species.Count; s++)
                        Capacity[s][i] = (float)AnimalCapacity.PerKm2(species[s], site, FreshWaterDistanceM[i], ShoreDistanceM[i]);
                }
            });
        }

        /// <summary>
        /// A stage's rows worked across the machine's cores (WG.2b's W3, 2026-09-23), for a stage whose every cell is written by
        /// itself alone from layers the stage does not write, and whose draws are the cell's own (a stream derived from the cell's
        /// index), so the order the rows are worked in cannot change a cell. A new 32 km world took 375.6 s to make inside the game,
        /// and these four stages were over half of it. A row that throws fails the stage with its own exception.
        /// </summary>
        private void Rows(Action<int> row)
        {
            try
            {
                Parallel.For(0, Height, row);
            }
            catch (AggregateException ex) when (ex.InnerExceptions.Count > 0)
            {
                ExceptionDispatchInfo.Capture(ex.InnerExceptions[0]).Throw();
                throw;
            }
        }
    }
}
