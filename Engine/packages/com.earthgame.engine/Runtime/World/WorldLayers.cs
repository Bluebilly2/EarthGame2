using System;
using System.Collections.Generic;

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
        /// <summary>Species index into <see cref="PlantSpecies.All"/> plus one; zero for nothing.</summary>
        public byte[] Overstory { get; }
        public byte[] Understory { get; }
        /// <summary>The winning canopy's suitability, 0 to 1.</summary>
        public float[] Suitability { get; }
        public uint[] TopologyMask { get; }
        /// <summary>Stone index into <see cref="StoneType.All"/> plus one; zero for none.</summary>
        public byte[] Stone { get; }
        /// <summary>Per species in <see cref="AnimalSpecies.All"/>, animals per km².</summary>
        public float[][] Capacity { get; }

        private WorldLayers(RegionRaster heights, RegionRaster waterBodies, Action<string> progress)
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
            Capacity = new float[AnimalSpecies.All.Count][];
            for (int s = 0; s < Capacity.Length; s++) Capacity[s] = new float[count];
            progress?.Invoke("Finding lakes and wetlands");
            _lake = Lakes(heights, waterBodies);
            progress?.Invoke("Tracing drainage");
            Drainage = DrainageNetwork.Of(heights, _lake);
            progress?.Invoke("Forming soil");
            Soil = new SoilModel(Drainage);
        }

        private readonly bool[] _lake;
        private readonly bool[] _wetland;
        private readonly float[] _level;

        /// <summary>
        /// Runs the chain. Seconds for the region; the seed varies only the draws (the community, the stone). The
        /// water bodies are the bake's outlines (<c>Tools/data/bake_water.py</c>, OpenStreetMap), optional: without
        /// them the lakes are read off the ground alone.
        /// </summary>
        public static WorldLayers Compute(RegionRaster heights, ulong seed, RegionRaster waterBodies = null, Action<string> progress = null)
        {
            if (heights == null) throw new ArgumentNullException(nameof(heights));
            WorldLayers w = new WorldLayers(heights, waterBodies, progress);
            progress?.Invoke("Reading slopes and wind exposure");
            w.SlopeAndExposure();
            progress?.Invoke("Measuring the shore");
            w.Distances();
            progress?.Invoke("Preparing the sea floor");
            w.SeaFloor();
            progress?.Invoke("Preparing water");
            w.WaterBodies();
            progress?.Invoke("Growing plant communities");
            w.Community(seed);
            progress?.Invoke("Reading landforms");
            w.Topology();
            progress?.Invoke("Finding stone");
            w.Stones(seed);
            progress?.Invoke("Calculating animal habitat");
            w.Capacities();
            return w;
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
        /// Found before the drainage runs, because a lake is a sink for it (see <see cref="DrainageNetwork"/>):
        /// the fill that had to spill read Windermere's closed basin as a pond thirty metres deep.
        /// </summary>
        private bool[] Lakes(RegionRaster heights, RegionRaster waterBodies)
        {
            int count = Width * Height;
            ReadOnlySpan<float> z = heights.Values;
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
                    flat[Index(r, c)] = hi - lo < LakeFlatnessM;
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

        /// <summary>
        /// The bake's outlines. The tiles are noise over these lakes (Windermere's outline holds ground from 12 m to
        /// 50 m, McKenzie's a bowl from 21 m to 57 m; 2026-09-09), so a lake's level is the median of the ground
        /// inside its outline and its water is the outline's cells at or below that level, the rest being margin;
        /// a wetland is swamp throughout its outline; a salt body is recorded and left to the sea's own rule.
        /// </summary>
        private void Mapped(ReadOnlySpan<float> z, RegionRaster waterBodies, bool[] lake)
        {
            if (waterBodies.Width != Width || waterBodies.Height != Height)
                throw new ArgumentException("the water bodies are " + waterBodies.Width + "x" + waterBodies.Height + ", the heights " + Width + "x" + Height, nameof(waterBodies));
            if (!waterBodies.IsIntegral) throw new ArgumentException("the water bodies must be an id layer", nameof(waterBodies));
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
            for (int i = 0; i < count; i++)
                Surface[i] = Water[i] == (byte)WaterClass.Sea ? (float)Heightfield.SeaLevelM : _lake[i] ? _level[i] : z[i];
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
                      .Append(" m inside an outline of ").Append(Ha(b.OutlineCells * ha)).Append(" (").Append(b.Source).Append(")").Append('\n');
                }
                else if (b.Kind == "wetland")
                    sb.Append("  ").Append(name).Append(": ").Append(Ha(b.WaterCells * ha)).Append(" of swamp (").Append(b.Source).Append(")").Append('\n');
                else
                    sb.Append("  ").Append(name).Append(": an outline of ").Append(Ha(b.OutlineCells * ha)).Append(", ").Append(b.Kind).Append(", not used (").Append(b.Source).Append(")").Append('\n');
            }
            sb.Append("  lakes read off the ground alone: ").Append(Ha(Math.Max(0, cells[(int)WaterClass.Lake] - mappedLake) * ha)).Append('\n');
            return sb.ToString();
        }

        private static string Ha(double hectares) => hectares.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " ha";

        /// <summary>Whether the founder could drink here: a creek, a stream or a lake.</summary>
        public static bool IsFresh(WaterClass wc) => wc == WaterClass.Creek || wc == WaterClass.Stream || wc == WaterClass.Lake;

        // ---- the site a cell offers a plant ----

        /// <summary>The site at a cell, before any canopy: wetness and depth from the soil, slope and openness from the ground, salt wind from the coast.</summary>
        public PlantSite SiteAt(int row, int col)
        {
            int i = Index(row, col);
            double coastWind = SimMath.Clamp01(1.0 - ShoreDistanceM[i] / CoastWindReachM);
            return new PlantSite
            {
                Wetness = Soil.WetnessAt(col, row),
                SoilDepthM = Soil.DepthAt(col, row),
                Slope = Slope[i],
                Exposure = Math.Max(Exposure[i], coastWind),
                Shaded = false,
            };
        }

        private void Community(ulong seed)
        {
            ulong stream = SimRandom.DeriveSeed(seed, "community");
            for (int r = 0; r < Height; r++)
                for (int c = 0; c < Width; c++)
                {
                    int i = Index(r, c);
                    if (Drainage.IsSea(c, r) || Water[i] == (byte)WaterClass.Lake) continue;
                    PlantSite site = SiteAt(r, c);
                    var rng = new SimRandom(SimRandom.DeriveSeed(stream, i.ToString()));
                    double rollCanopy = rng.NextDouble();
                    double rollUnder = rng.NextDouble();
                    PlantSpecies canopy = PlantCommunity.Canopy(site, rollCanopy);
                    Overstory[i] = (byte)(canopy == null ? 0 : IndexOf(canopy) + 1);
                    Suitability[i] = canopy == null ? 0f : (float)canopy.Suitability(site);
                    PlantSite under = site;
                    under.Shaded = canopy != null;
                    PlantSpecies floor = PlantCommunity.Understory(under, rollUnder);
                    Understory[i] = (byte)(floor == null ? 0 : IndexOf(floor) + 1);
                }
        }

        private static int IndexOf(PlantSpecies species)
        {
            IReadOnlyList<PlantSpecies> all = PlantSpecies.All;
            for (int i = 0; i < all.Count; i++) if (ReferenceEquals(all[i], species)) return i;
            return -1;
        }

        public PlantSpecies OverstoryAt(int row, int col)
        {
            byte id = Overstory[Index(row, col)];
            return id == 0 ? null : PlantSpecies.All[id - 1];
        }

        public PlantSpecies UnderstoryAt(int row, int col)
        {
            byte id = Understory[Index(row, col)];
            return id == 0 ? null : PlantSpecies.All[id - 1];
        }

        // ---- topology ----

        private void Topology()
        {
            ReadOnlySpan<float> z = Heights.Values;
            int reach = Math.Max(1, (int)Math.Round(HardCoastReachM / CellM));
            int crest = Math.Max(1, (int)Math.Round(CrestReachM / CellM));
            for (int r = 0; r < Height; r++)
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
                    PlantSpecies canopy = OverstoryAt(r, c);
                    PlantSpecies floor = UnderstoryAt(r, c);
                    if (canopy != null && canopy.Form == PlantForm.Tree) bits |= (uint)Engine.Topology.Forest;
                    else if (floor != null && floor.Form == PlantForm.Shrub) bits |= (uint)Engine.Topology.Heath;
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
        }

        public bool Has(int row, int col, Topology bit) => (TopologyMask[Index(row, col)] & (uint)bit) != 0;

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
            ReadOnlySpan<float> z = Heights.Values;
            for (int r = 0; r < Height; r++)
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

        // ---- the animals ----

        private void Capacities()
        {
            IReadOnlyList<AnimalSpecies> species = AnimalSpecies.All;
            for (int r = 0; r < Height; r++)
                for (int c = 0; c < Width; c++)
                {
                    int i = Index(r, c);
                    if (Drainage.IsSea(c, r)) continue;
                    PlantSite site = SiteAt(r, c);
                    for (int s = 0; s < species.Count; s++)
                        Capacity[s][i] = (float)AnimalCapacity.PerKm2(species[s], site, FreshWaterDistanceM[i], ShoreDistanceM[i]);
                }
        }
    }
}
