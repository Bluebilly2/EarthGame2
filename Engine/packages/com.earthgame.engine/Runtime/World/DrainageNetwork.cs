using System;

namespace EarthGame.Engine
{
    /// <summary>What a cell of ground carries, from how much land drains through it.</summary>
    public enum Channel
    {
        None,

        /// <summary>Wet ground. No free water, but the litter here will never be dry.</summary>
        Damp,

        /// <summary>A thread of water somewhere under the leaves.</summary>
        Trickle,

        /// <summary>Running water. You can drink from it.</summary>
        Creek,

        /// <summary>A watercourse.</summary>
        Stream,

        /// <summary>
        /// Standing water in a hollow that has no way out. Listed last so that everything from
        /// <see cref="Creek"/> upward is water you can drink.
        /// </summary>
        Pond,
    }

    /// <summary>
    /// Where water goes on a piece of terrain, worked out from the shape of the terrain. Ported from v1
    /// (slice 2.3c, 2026) with one adjustment named below: the sea.
    ///
    /// <para>Nothing here is stored or authored. Every cell is asked which of its eight neighbours
    /// is furthest downhill, and then the area of every cell is pushed down that chain from the
    /// highest ground to the lowest. A creek appears where enough hillside has converged on one
    /// line to keep it running — which is what a creek <b>is</b>, and is why someone who can read
    /// a slope can find water here using nothing but that (GAME_DESIGN §6, §3).</para>
    ///
    /// <para>The heightfield is filled before any of that happens, and it has to be. Terrain is
    /// covered in pits a few centimetres deep, and every one of them is a place where water stops:
    /// without filling, v1's landscape produced <b>no creeks at all</b>, because flow never survived
    /// more than a few cells before falling into a hole. Filling depressions is the first step of
    /// every real analysis of a real elevation model, for exactly this reason.</para>
    ///
    /// <para>The adjustment: on a real coast the sea is the base level, not the edge of the grid.
    /// Every cell at or below <see cref="SeaLevelM"/> is a sink — the fill floods outward from it as
    /// well as from the boundary, nothing drains across it, and it carries no channel — so that the
    /// flat sea of the bake is not read as a plain with rivers on it and a coastal hollow fills to the
    /// sea's lip rather than the grid's. Without a sea (the default) the network is v1's.</para>
    ///
    /// <para>It is a pure function of the heightfield, so the network never needs saving. It comes
    /// back identical every time, like the rest of the world (M1.2 promise 10).</para>
    /// </summary>
    public sealed class DrainageNetwork
    {
        /// <summary>Catchment at which ground stays wet, m². Four tenths of a hectare.</summary>
        public const double DampM2 = 4_000.0;

        /// <summary>Catchment at which water first shows, m². Two hectares.</summary>
        public const double TrickleM2 = 20_000.0;

        /// <summary>Catchment that keeps a creek running, m². Twelve hectares.</summary>
        public const double CreekM2 = 120_000.0;

        /// <summary>Catchment of a proper watercourse, m². Sixty hectares.</summary>
        public const double StreamM2 = 600_000.0;

        // The eight neighbours, in order, and how far away each is in cell widths.
        private static readonly int[] OffsetX = { 1, 1, 0, -1, -1, -1, 0, 1 };
        private static readonly int[] OffsetZ = { 0, 1, 1, 1, 0, -1, -1, -1 };

        /// <summary>
        /// How deep a filled hollow has to be before it counts as holding water, m. Below this it
        /// is a damp dip rather than a pond.
        /// </summary>
        public const float PondDepthM = 2.0f;

        /// <summary>
        /// The smallest thing that counts as standing water, m². A basin one cell across is not a
        /// pond, it is a dent — and treating every dent as a pond scattered puddles across open
        /// hillsides where no water would ever sit.
        /// </summary>
        public const double MinPondAreaM2 = 20_000.0;

        /// <summary>
        /// The slope left across a filled depression, m per cell. Water has to be able to cross a
        /// filled hollow, so the fill leaves a whisper of gradient behind it rather than a
        /// perfectly level floor with nowhere to go.
        /// </summary>
        public const float FillGradientM = 1e-3f;

        private readonly float[] _height;
        private readonly float[] _ground;
        private readonly bool[] _pond;
        private readonly bool[] _sea;
        private readonly sbyte[] _flow;
        private readonly double[] _catchment;

        public int Width { get; }
        public int Height { get; }
        public double CellSizeM { get; }

        /// <summary>The level at or below which a cell is sea; negative infinity when the grid has no sea.</summary>
        public double SeaLevelM { get; }

        /// <summary>Area one cell contributes to whatever it drains into, m².</summary>
        public double CellAreaM2 => CellSizeM * CellSizeM;

        /// <summary>How many cells are sea.</summary>
        public int SeaCells { get; }

        /// <summary>The filled surface, the raw ground and the sea mask, for the soil model that reads them whole.</summary>
        internal float[] FilledArray => _height;
        internal float[] GroundArray => _ground;
        internal bool[] SeaArray => _sea;

        public DrainageNetwork(float[] heights, int width, int height, double cellSizeM, double seaLevelM = double.NegativeInfinity)
        {
            if (heights == null) throw new ArgumentNullException(nameof(heights));
            if (width < 2 || height < 2) throw new ArgumentException("grid too small");
            if (heights.Length < width * height) throw new ArgumentException("heights too short");
            if (cellSizeM <= 0.0) throw new ArgumentException("cell size must be positive");

            Width = width;
            Height = height;
            CellSizeM = cellSizeM;
            SeaLevelM = seaLevelM;

            _height = (float[])heights.Clone();
            _ground = heights;
            _pond = new bool[width * height];
            _sea = new bool[width * height];
            _flow = new sbyte[width * height];
            _catchment = new double[width * height];

            int seaCells = 0;
            for (int i = 0; i < width * height; i++)
            {
                if (heights[i] <= seaLevelM)
                {
                    _sea[i] = true;
                    seaCells++;
                }
            }
            SeaCells = seaCells;

            FillDepressions();
            ComputeFlowDirections();
            Accumulate();
            FindPonds();
        }

        /// <summary>The network of a raster, the sea at the bake's datum.</summary>
        public static DrainageNetwork Of(RegionRaster raster)
        {
            if (raster == null) throw new ArgumentNullException(nameof(raster));
            return new DrainageNetwork(raster.Values.ToArray(), raster.Width, raster.Height, raster.CellM, Heightfield.SeaLevelM);
        }

        /// <summary>
        /// The hydrologically corrected surface: the terrain with its pits filled. This is the
        /// surface water actually sees, which is why it is the one flow is computed on.
        /// </summary>
        public float HeightAt(int x, int z) => _height[Index(x, z)];

        /// <summary>The ground as the terrain actually shapes it, before any filling.</summary>
        public float GroundAt(int x, int z) => _ground[Index(x, z)];

        /// <summary>
        /// How deep the standing water is here, m.
        ///
        /// <para>This is the fill depth, and it is not a fudge: the level a hollow fills to before
        /// it spills is exactly the level a pond in it would sit at. So the correction that makes
        /// the drainage work also tells you where the water is standing.</para>
        /// </summary>
        public float WaterDepthAt(int x, int z) => _height[Index(x, z)] - _ground[Index(x, z)];

        /// <summary>Whether this cell is sea: the base level everything drains to, carrying no channel of its own.</summary>
        public bool IsSea(int x, int z) => _sea[Index(x, z)];

        /// <summary>Whether the founder could drink here.</summary>
        public static bool IsDrinkable(Channel channel) => channel >= Channel.Creek;

        /// <summary>Whether there is water here worth drawing.</summary>
        public static bool IsVisible(Channel channel) => channel >= Channel.Trickle;

        /// <summary>Which of the eight neighbours this cell drains to, or -1 if it drains nowhere.</summary>
        public int FlowDirection(int x, int z) => _flow[Index(x, z)];

        /// <summary>How much land drains through this cell, m², including the cell itself.</summary>
        public double CatchmentM2(int x, int z) => _catchment[Index(x, z)];

        /// <summary>What this cell carries.</summary>
        public Channel ChannelAt(int x, int z)
        {
            int i = Index(x, z);
            if (_sea[i]) return Channel.None;
            double area = _catchment[i];

            // A hollow with no way out holds what drains into it. The founder who walks downhill
            // and ends up in a bowl has not made a mistake; they have found the water.
            if (_pond[i]) return Channel.Pond;

            if (area >= StreamM2) return Channel.Stream;
            if (area >= CreekM2) return Channel.Creek;
            if (area >= TrickleM2) return Channel.Trickle;
            if (area >= DampM2) return Channel.Damp;
            return Channel.None;
        }

        /// <summary>Catchment a channel of this kind needs, m².</summary>
        public static double CatchmentFor(Channel channel)
        {
            switch (channel)
            {
                case Channel.Stream: return StreamM2;
                case Channel.Creek: return CreekM2;
                case Channel.Trickle: return TrickleM2;
                case Channel.Damp: return DampM2;
                default: return 0.0;
            }
        }

        /// <summary>Follows the water one step. False at a sink, at the sea, or off the edge.</summary>
        public bool TryStepDownstream(int x, int z, out int nx, out int nz)
        {
            nx = x;
            nz = z;

            int dir = FlowDirection(x, z);
            if (dir < 0) return false;

            nx = x + OffsetX[dir];
            nz = z + OffsetZ[dir];
            return InBounds(nx, nz);
        }

        public bool InBounds(int x, int z) => x >= 0 && z >= 0 && x < Width && z < Height;

        private int Index(int x, int z)
        {
            if (!InBounds(x, z)) throw new ArgumentOutOfRangeException(nameof(x), "outside the grid");
            return z * Width + x;
        }

        /// <summary>
        /// Finds the basins big enough to hold water.
        ///
        /// <para>Flooded cells are grouped into connected basins and each basin is kept or
        /// discarded whole, because that is what a pond is: one body of water, not a scatter of
        /// independently wet cells. Anything smaller than <see cref="MinPondAreaM2"/> is a dent in
        /// the ground rather than somewhere water sits.</para>
        /// </summary>
        private void FindPonds()
        {
            int count = Width * Height;
            var seen = new bool[count];
            var stack = new int[count];
            var basin = new int[count];

            for (int start = 0; start < count; start++)
            {
                if (seen[start]) continue;
                seen[start] = true;
                if (_sea[start] || _height[start] - _ground[start] < PondDepthM) continue;

                int size = 0, top = 0;
                stack[top++] = start;

                while (top > 0)
                {
                    int i = stack[--top];
                    basin[size++] = i;

                    int x = i % Width;
                    int z = i / Width;

                    for (int d = 0; d < 8; d++)
                    {
                        int nx = x + OffsetX[d];
                        int nz = z + OffsetZ[d];
                        if (!InBounds(nx, nz)) continue;

                        int n = nz * Width + nx;
                        if (seen[n]) continue;
                        seen[n] = true;
                        if (_sea[n] || _height[n] - _ground[n] < PondDepthM) continue;

                        stack[top++] = n;
                    }
                }

                if (size * CellAreaM2 < MinPondAreaM2) continue;
                for (int k = 0; k < size; k++) _pond[basin[k]] = true;
            }
        }

        /// <summary>
        /// Priority-Flood: raises every interior pit to the lowest lip that lets it escape, so
        /// that every cell has a path to the edge of the grid or to the sea.
        ///
        /// <para>Working outward from the boundary and the sea in height order means each cell is
        /// reached for the first time by the lowest route into it, which is by definition the level
        /// its hollow would fill to before spilling. One pass, no iteration.</para>
        /// </summary>
        private void FillDepressions()
        {
            int count = Width * Height;
            var closed = new bool[count];
            var queue = new MinHeap(count);

            for (int i = 0; i < count; i++)
            {
                if (!_sea[i]) continue;
                closed[i] = true;
                queue.Push(i, _height[i]);
            }
            for (int x = 0; x < Width; x++)
            {
                Seed(queue, closed, x, 0);
                Seed(queue, closed, x, Height - 1);
            }
            for (int z = 1; z < Height - 1; z++)
            {
                Seed(queue, closed, 0, z);
                Seed(queue, closed, Width - 1, z);
            }

            while (queue.Count > 0)
            {
                int i = queue.Pop();
                int x = i % Width;
                int z = i / Width;

                for (int d = 0; d < 8; d++)
                {
                    int nx = x + OffsetX[d];
                    int nz = z + OffsetZ[d];
                    if (!InBounds(nx, nz)) continue;

                    int n = nz * Width + nx;
                    if (closed[n]) continue;

                    // Never lower ground; only raise a hollow to the level it would fill to.
                    float floor = _height[i] + FillGradientM;
                    if (_height[n] < floor) _height[n] = floor;

                    closed[n] = true;
                    queue.Push(n, _height[n]);
                }
            }
        }

        private void Seed(MinHeap queue, bool[] closed, int x, int z)
        {
            int i = z * Width + x;
            if (closed[i]) return;
            closed[i] = true;
            queue.Push(i, _height[i]);
        }

        /// <summary>A binary heap over cell indices, keyed by height.</summary>
        private sealed class MinHeap
        {
            private readonly int[] _items;
            private readonly float[] _keys;

            public int Count { get; private set; }

            public MinHeap(int capacity)
            {
                _items = new int[capacity + 1];
                _keys = new float[capacity + 1];
            }

            public void Push(int item, float key)
            {
                int i = ++Count;
                _items[i] = item;
                _keys[i] = key;

                while (i > 1 && _keys[i >> 1] > _keys[i])
                {
                    Swap(i, i >> 1);
                    i >>= 1;
                }
            }

            public int Pop()
            {
                int top = _items[1];
                _items[1] = _items[Count];
                _keys[1] = _keys[Count];
                Count--;

                int i = 1;
                while (true)
                {
                    int left = i << 1, right = left + 1, smallest = i;
                    if (left <= Count && _keys[left] < _keys[smallest]) smallest = left;
                    if (right <= Count && _keys[right] < _keys[smallest]) smallest = right;
                    if (smallest == i) break;
                    Swap(i, smallest);
                    i = smallest;
                }
                return top;
            }

            private void Swap(int a, int b)
            {
                int item = _items[a]; _items[a] = _items[b]; _items[b] = item;
                float key = _keys[a]; _keys[a] = _keys[b]; _keys[b] = key;
            }
        }

        /// <summary>
        /// Steepest descent, by gradient rather than by drop: the diagonal neighbours are further
        /// away, so a bigger fall to one of them is not necessarily a steeper slope. The sea drains
        /// nowhere.
        /// </summary>
        private void ComputeFlowDirections()
        {
            for (int z = 0; z < Height; z++)
            {
                for (int x = 0; x < Width; x++)
                {
                    int i = z * Width + x;
                    if (_sea[i])
                    {
                        _flow[i] = -1;
                        continue;
                    }
                    float here = _height[i];

                    int best = -1;
                    double steepest = 0.0;

                    for (int d = 0; d < 8; d++)
                    {
                        int nx = x + OffsetX[d];
                        int nz = z + OffsetZ[d];
                        if (!InBounds(nx, nz)) continue;

                        double drop = here - _height[nz * Width + nx];
                        if (drop <= 0.0) continue;

                        double distance = (OffsetX[d] != 0 && OffsetZ[d] != 0)
                            ? CellSizeM * 1.4142135623730951
                            : CellSizeM;

                        double gradient = drop / distance;
                        if (gradient > steepest) { steepest = gradient; best = d; }
                    }

                    _flow[i] = (sbyte)best;
                }
            }
        }

        /// <summary>
        /// Pushes every cell's area downstream, highest ground first. Working in height order
        /// means a cell is only ever handed on once everything above it has already drained into
        /// it, so one pass is enough and no cell can be counted twice. Land that reaches the sea
        /// stops there: the last land cell is the outlet, and the sea accumulates nothing.
        /// </summary>
        private void Accumulate()
        {
            int count = Width * Height;
            var order = new int[count];
            var keys = new float[count];
            for (int i = 0; i < count; i++)
            {
                order[i] = i;
                keys[i] = -_height[i];
                _catchment[i] = CellAreaM2;
            }

            // Keyed sort rather than a comparison delegate: four million cells at 4 m.
            Array.Sort(keys, order);

            for (int k = 0; k < count; k++)
            {
                int i = order[k];
                int dir = _flow[i];
                if (dir < 0) continue;

                int x = i % Width;
                int z = i / Width;
                int nx = x + OffsetX[dir];
                int nz = z + OffsetZ[dir];
                if (!InBounds(nx, nz)) continue;

                int n = nz * Width + nx;
                if (_sea[n]) continue;
                _catchment[n] += _catchment[i];
            }
        }
    }
}
