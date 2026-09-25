using System;
using EarthGame.Engine;

namespace EarthGame.ClientCore
{
    /// <summary>
    /// The loading screen's map of a world as it is made (William chose the phases' bars "and a map beside them", 2026-09-25):
    /// the land's shape at once, then the rivers, lakes and sea, then the forest, then the ground's cover, and where the
    /// founder will wake. It watches the making (<see cref="IMakingWatcher"/>) and is told the making's reports, and paints only
    /// what a report has said is finished.
    ///
    /// <para><b>What is finished when.</b> A layer's array is whole once the step that fills it has ended, which the next step's
    /// report says: the water's classes when "Reading landforms" begins (after "Preparing water"), the canopy when "Reading
    /// the ground cover" begins (after "Growing plant communities"), the cover when "Finding stone" begins (after "Reading the
    /// ground cover"). <c>MakingMapTests</c> holds each array, as it stood at its report, to the array the making ended with.</para>
    ///
    /// <para><b>The picture.</b> A square of <see cref="Size"/> pixels over the region, row 0 at the south as a texture takes
    /// it, each pixel the land at its centre: shaded by the slope under a light from the north-west, tinted by height until
    /// the cover is known and then in the ground's own colour (<see cref="GroundPalette"/>), under the foliage of the tree
    /// the canopy names there (<see cref="StandForms"/>), and blue wherever any cell of the pixel is a lake or the sea, or a
    /// creek or a stream draining at least <see cref="RiverDrainsPixels"/> pixels' worth of the land, so the watercourses drawn
    /// are as many for the map's scale on any region: on the whole Kangaroo Valley, where a pixel is 83 m, every creek drawn
    /// turned the map blue (2026-09-25), and the rule draws the ones that drain 2.8 km² and more; on Bherwerre, 21 m a pixel,
    /// those that drain 17 ha. Colours are the palettes' own, in the space a texture is read in.</para>
    /// </summary>
    public sealed class MakingMap : IMakingWatcher
    {
        /// <summary>The report that says the water's classes are whole: the step after "Preparing water" begins.</summary>
        public const string WaterEndsAt = "Reading landforms";

        /// <summary>The report that says the canopy is whole: the step after "Growing plant communities" begins.</summary>
        public const string CanopyEndsAt = "Reading the ground cover";

        /// <summary>The report that says the ground's cover is whole: the step after "Reading the ground cover" begins.</summary>
        public const string CoverEndsAt = "Finding stone";

        /// <summary>What the map can show: nothing, the land, then the water, the canopy and the cover as each is finished.</summary>
        public const int Nothing = 0, Land = 1, Water = 2, Canopy = 3, Cover = 4;

        private static readonly GroundColour Sea = new GroundColour(0.16f, 0.33f, 0.47f), Lake = new GroundColour(0.22f, 0.42f, 0.58f),
            River = new GroundColour(0.33f, 0.58f, 0.78f), LowLand = new GroundColour(0.52f, 0.54f, 0.44f), HighLand = new GroundColour(0.72f, 0.67f, 0.57f);

        /// <summary>The height the tint by height reaches its highest colour at, m: the valley's plateau.</summary>
        private const double TintTopM = 800.0;

        /// <summary>How much of a forested pixel the canopy's foliage covers, over the ground's colour.</summary>
        private const float CanopyShare = 0.85f;

        /// <summary>A creek or a stream is drawn where it drains at least this many pixels' area of the land.</summary>
        public const double RiverDrainsPixels = 400.0;

        private volatile WorldLayers _layers;
        private readonly object _wakeLock = new object();
        private double _wakeEast = double.NaN, _wakeNorth = double.NaN;
        private int _reported;

        public MakingMap(int size)
        {
            if (size < 8) throw new ArgumentOutOfRangeException(nameof(size));
            Size = size;
        }

        /// <summary>The picture's side, pixels.</summary>
        public int Size { get; }

        /// <summary>What the map can show now (<see cref="Land"/> to <see cref="Cover"/>), rising as the making goes on.</summary>
        public int Level => _layers == null ? Nothing : Math.Max(Land, _reported);

        /// <inheritdoc/>
        public void Began(WorldLayers layers) => _layers = layers;

        /// <inheritdoc/>
        public void WakeChosen(double east, double north)
        {
            lock (_wakeLock)
            {
                _wakeEast = east;
                _wakeNorth = north;
            }
        }

        /// <summary>A report of the making, as the loading screen receives it; the arrays it says are finished may be painted.</summary>
        public void Reported(string stage)
        {
            if (stage == WaterEndsAt) _reported = Math.Max(_reported, Water);
            else if (stage == CanopyEndsAt) _reported = Math.Max(_reported, Canopy);
            else if (stage == CoverEndsAt) _reported = Math.Max(_reported, Cover);
        }

        /// <summary>Where the founder will wake on the picture, 0 to 1 from its west edge and from its south; false until chosen.</summary>
        public bool TryWake(out float fromWest, out float fromSouth)
        {
            fromWest = fromSouth = 0f;
            WorldLayers layers = _layers;
            double east, north;
            lock (_wakeLock)
            {
                east = _wakeEast;
                north = _wakeNorth;
            }
            if (layers == null || double.IsNaN(east)) return false;
            double half = (layers.Width - 1) * layers.CellM / 2.0;
            fromWest = (float)((east + half) / (2.0 * half));
            fromSouth = (float)((north + half) / (2.0 * half));
            return true;
        }

        /// <summary>
        /// The picture at a level no higher than <see cref="Level"/> was when it was read: RGBA, four bytes a pixel, row 0 at the
        /// south. Null before the land is known. Reads only the arrays the level says are finished, so it may run on any thread.
        /// </summary>
        public byte[] Paint(int level)
        {
            WorldLayers layers = _layers;
            if (layers == null || level < Land) return null;
            int n = Size, width = layers.Width, height = layers.Height;
            double cell = layers.CellM, half = (width - 1) * cell / 2.0, step = 2.0 * half / n;
            RegionRaster heights = layers.Heights;

            // The land's height at each pixel's centre, row 0 at the south, for its tint and its shading.
            var h = new float[n * n];
            var rowOf = new int[n];
            var colOf = new int[n];
            for (int i = 0; i < n; i++)
            {
                colOf[i] = Clamp((int)Math.Round((-half + (i + 0.5) * step + half) / cell), width - 1);
                rowOf[i] = Clamp((int)Math.Round((half - (-half + (i + 0.5) * step)) / cell), height - 1);
            }
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                    h[y * n + x] = heights[rowOf[y], colOf[x]];

            var rgba = new byte[n * n * 4];
            byte[] water = level >= Water ? layers.Water : null, overstory = level >= Canopy ? layers.Overstory : null, cover = level >= Cover ? layers.Cover : null;
            int reach = Math.Max(0, (int)Math.Floor(step / cell / 2.0));
            DrainageNetwork drainage = level >= Water ? layers.Drainage : null;
            double riverM2 = RiverDrainsPixels * step * step;
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    int row = rowOf[y], col = colOf[x], at = row * width + col;
                    float elevation = h[y * n + x];
                    GroundColour colour;
                    if (cover != null) colour = GroundPalette.Of(cover[at]);
                    else colour = GroundColour.Between(LowLand, HighLand, (float)Math.Max(0.0, Math.Min(1.0, elevation / TintTopM)));
                    if (overstory != null)
                    {
                        PlantSpecies canopy = PlantSpecies.ByNumber(overstory[at]);
                        TreeForm form = StandCodes.IsTall(canopy) ? StandForms.For(canopy) : null;
                        if (form != null)
                            colour = GroundColour.Between(colour, new GroundColour(form.Foliage.R, form.Foliage.G, form.Foliage.B), CanopyShare);
                    }
                    float shade = Shade(h, n, x, y, step);
                    colour = new GroundColour(colour.R * shade, colour.G * shade, colour.B * shade);
                    if (water != null)
                    {
                        WaterClass wet = WettestIn(water, drainage, riverM2, width, height, row, col, reach);
                        if (wet == WaterClass.Sea) colour = Sea;
                        else if (wet == WaterClass.Lake) colour = Lake;
                        else if (wet == WaterClass.Creek || wet == WaterClass.Stream) colour = River;
                    }
                    else if (elevation <= Heightfield.SeaLevelM)
                    {
                        colour = Sea;
                    }
                    int o = (y * n + x) * 4;
                    rgba[o] = Byte(colour.R);
                    rgba[o + 1] = Byte(colour.G);
                    rgba[o + 2] = Byte(colour.B);
                    rgba[o + 3] = 255;
                }
            }
            return rgba;
        }

        /// <summary>The light on a pixel's slope from the north-west, 45 degrees up, as a factor of its colour: 0.55 in its own shadow, 1 facing it.</summary>
        private static float Shade(float[] h, int n, int x, int y, double step)
        {
            float west = h[y * n + Math.Max(0, x - 1)], east = h[y * n + Math.Min(n - 1, x + 1)];
            float south = h[Math.Max(0, y - 1) * n + x], north = h[Math.Min(n - 1, y + 1) * n + x];
            double dx = (east - west) / (2.0 * step), dy = (north - south) / (2.0 * step);
            // The surface's normal (-dx, -dy, 1), and the light from the north-west (-1, 1) at 45 degrees up.
            double nx = -dx, ny = -dy, nz = 1.0, length = Math.Sqrt(nx * nx + ny * ny + nz * nz);
            double lx = -0.5, ly = 0.5, lz = Math.Sqrt(0.5);
            double lit = (nx * lx + ny * ly + nz * lz) / length;
            return (float)(0.55 + 0.45 * Math.Max(0.0, Math.Min(1.0, lit / lz)));
        }

        /// <summary>
        /// The wettest open water among a pixel's cells: the sea over a lake over a stream or a creek that drains at least
        /// <paramref name="riverM2"/>, or dry.
        /// </summary>
        private static WaterClass WettestIn(byte[] water, DrainageNetwork drainage, double riverM2, int width, int height, int row, int col, int reach)
        {
            WaterClass wettest = WaterClass.Dry;
            for (int r = Math.Max(0, row - reach); r <= Math.Min(height - 1, row + reach); r++)
            {
                for (int c = Math.Max(0, col - reach); c <= Math.Min(width - 1, col + reach); c++)
                {
                    var wet = (WaterClass)water[r * width + c];
                    if (wet == WaterClass.Sea) return WaterClass.Sea;
                    if (wet == WaterClass.Lake) wettest = WaterClass.Lake;
                    else if ((wet == WaterClass.Creek || wet == WaterClass.Stream) && wettest != WaterClass.Lake
                             && (drainage == null || drainage.CatchmentM2(c, r) >= riverM2))
                        wettest = wet;
                }
            }
            return wettest;
        }

        private static int Clamp(int value, int most) => value < 0 ? 0 : value > most ? most : value;

        private static byte Byte(float channel) => (byte)Math.Max(0, Math.Min(255, (int)Math.Round(channel * 255f)));
    }
}
