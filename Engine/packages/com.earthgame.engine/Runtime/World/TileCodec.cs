using System;
using System.IO;
using System.IO.Compression;

namespace EarthGame.Engine
{
    /// <summary>
    /// Which of a world's layers a tile carries. Wire-visible (the tile messages name it) and never renumbered:
    /// a retired layer keeps its number. The water's surface is not among them, because a client that holds a
    /// tile's ground already holds most of it — what travels is the depth standing over that ground.
    /// </summary>
    public enum TileLayer : byte
    {
        /// <summary>The ground, metres.</summary>
        Ground = 0,
        /// <summary>How deep the water stands over the ground of the same tile, metres; zero where none stands.</summary>
        WaterDepth = 1,
        /// <summary>Each post's <see cref="WaterClass"/> as a code.</summary>
        WaterClass = 2,
        /// <summary>Each post's <see cref="GroundCovers"/> code: what covers it, and which quarter of the land's wetness it is in.</summary>
        GroundCover = 3,
        /// <summary>Each post's <see cref="StandCodes"/> code: which tall plant stands on its cell, and how tall (M1.6a).</summary>
        Stand = 4,
        /// <summary>Each post's <see cref="LooseCodes"/> code: how many sticks and cobbles lie on its cell (M1.6a).</summary>
        Loose = 5,
        /// <summary>
        /// Each far post's <see cref="StandCodes"/> code for the square of stand cells round it: the tall plant most of its
        /// trees are, at their mean height (M1.6d). Posts <see cref="TileLayers.FarCellM"/> apart.
        /// </summary>
        FarStand = 6,
        /// <summary>How many trees stand in the square of stand cells round each far post, to a byte (M1.6d).</summary>
        FarCount = 7,
        /// <summary>Each post's stone as the world's stone layer codes it (BF.1): <see cref="StoneType.All"/>'s index plus one, zero for none, so a client can name a cobble's stone before it is taken up.</summary>
        Stone = 8,
    }

    /// <summary>Every layer a tile can carry. Both ends walk this, so neither has to be told the set.</summary>
    public static class TileLayers
    {
        public static readonly TileLayer[] All =
        {
            TileLayer.Ground, TileLayer.WaterDepth, TileLayer.WaterClass, TileLayer.GroundCover, TileLayer.Stand, TileLayer.Loose,
            TileLayer.FarStand, TileLayer.FarCount, TileLayer.Stone,
        };

        /// <summary>
        /// The side of a far layer's square, m (M1.6d): ten of the world's 4 m cells, so a kilometre tile holds twenty-five
        /// squares and every far post stands on a stand cell's centre.
        /// </summary>
        public const double FarCellM = 40.0;

        /// <summary>
        /// Whether a layer is a far layer (M1.6d): asked for over the region's whole grid once, at the join, and never let
        /// go, where every other layer is asked for round the founder and trimmed to the nearest tiles.
        /// </summary>
        public static bool IsFar(TileLayer layer) => layer == TileLayer.FarStand || layer == TileLayer.FarCount;

        /// <summary>
        /// Whether a layer travels as codes — raw bytes through deflate — rather than as metres. One owner: the
        /// receiver asked this of the layer's number instead and dropped every cover tile it was sent, silently,
        /// because a byte a post is not a square of int16s (2026-09-10).
        /// </summary>
        public static bool CarriesCodes(TileLayer layer)
        {
            switch (layer)
            {
                case TileLayer.WaterClass:
                case TileLayer.GroundCover:
                case TileLayer.Stand:
                case TileLayer.Loose:
                case TileLayer.FarStand:
                case TileLayer.FarCount:
                case TileLayer.Stone: return true;
                default: return false;
            }
        }

        /// <summary>Whether a byte off the wire names a layer this build knows.</summary>
        public static bool IsKnown(byte value)
        {
            foreach (TileLayer layer in All) if ((byte)layer == value) return true;
            return false;
        }

        /// <summary>
        /// The folder a layer's tiles are cached under, and the name a verifier reads. One owner: a name that
        /// lived in the cache's path and again in a check would be the named bug shape.
        /// </summary>
        public static string FolderOf(TileLayer layer)
        {
            switch (layer)
            {
                case TileLayer.Ground: return "ground";
                case TileLayer.WaterDepth: return "water-depth";
                case TileLayer.WaterClass: return "water-class";
                case TileLayer.GroundCover: return "ground-cover";
                case TileLayer.Stand: return "stand";
                case TileLayer.Loose: return "loose";
                case TileLayer.FarStand: return "far-stand";
                case TileLayer.FarCount: return "far-count";
                case TileLayer.Stone: return "stone";
                default: throw new ArgumentOutOfRangeException(nameof(layer), "no such layer: " + layer);
            }
        }
    }

    /// <summary>A tile of one layer as it travels: the posts along one side, the ground pitch, and the compressed bytes.</summary>
    public sealed class EncodedTile
    {
        public TileId Id;
        public TileLayer Layer;
        public int Posts;
        public double CellM;
        public double OriginEast;
        public double OriginNorth;
        public byte[] Bytes;
        public uint Crc32;
    }

    /// <summary>
    /// How a tile of one layer is packed for the wire and the disk cache (ARCHITECTURE §10, tile format v2): the
    /// posts of a tile sampled straight from the raster at its own pitch (a kilometre at 4 m is 251 posts a
    /// side, sharing an edge post with each neighbour). A layer of metres — the ground, and the water's depth
    /// over it — writes each post as centimetres in a signed 16-bit integer, each row its first value then the
    /// difference to the previous post, the whole run through deflate; nine tiles of the Bherwerre ground come to
    /// a few hundred kilobytes and the join budget is built on that number (N1). A layer of codes writes its raw
    /// bytes through deflate, which is what an id layer with long runs of one value wants. The decoder refuses a
    /// length that is not a square of posts.
    ///
    /// <para>Version 2 (M1.4b, 2026-09-10) added the layer and the code packing. Water travels as depth rather
    /// than as its own surface because the client already holds the ground: measured on the Bherwerre world, the
    /// nine tiles around the wake are 277 KB as a surface of their own against 31 KB as depth over that ground,
    /// which is zero everywhere the ground is dry.</para>
    ///
    /// <para>Version 3 (WG.2, 2026-09-22): each row of a metres layer begins with a 32-bit number of centimetres and
    /// continues in 16-bit steps between neighbouring posts, so a tile carries any height on Earth. Version 2 held every
    /// height as a 16-bit number of centimetres, ±327 m of the datum, which the sea's coast never reached and the Kangaroo
    /// Valley's plateau, at 700 m, broke on the first encode.</para>
    /// </summary>
    public static class TileCodec
    {
        public const int Version = 3;
        /// <summary>
        /// The largest height a tile will carry, metres either side of the datum: past the deepest trench and the highest
        /// summit, so only a number that is no height on Earth is refused. A height beyond it, and a step between neighbouring
        /// posts too long for a signed 16-bit number of centimetres, are refused at encode, as ARCHITECTURE §10 states; until
        /// 2026-09-13 the height was clamped and the step wrapped without a word, which would have cut a mountain's summit off
        /// and turned a cliff's foot into its top; until version 3 the bound was 327 m, a 16-bit number of centimetres.
        /// </summary>
        public const double MaxHeightM = 12000.0;

        /// <summary>Samples a tile of ground from the heightfield at the raster's own cell pitch and encodes it.</summary>
        public static EncodedTile Encode(Heightfield heightfield, TileGrid grid, TileId id)
        {
            if (heightfield == null) throw new ArgumentNullException(nameof(heightfield));
            if (grid == null) throw new ArgumentNullException(nameof(grid));
            if (!grid.Contains(id)) throw new ArgumentOutOfRangeException(nameof(id), "tile " + id + " is outside the grid");
            double cell = heightfield.CellM;
            int posts = (int)Math.Round(grid.TileSizeM / cell) + 1;
            grid.Origin(id, out double originEast, out double originNorth);
            float[,] heights = new float[posts, posts];
            for (int z = 0; z < posts; z++)
                for (int x = 0; x < posts; x++)
                    heights[z, x] = (float)heightfield.HeightAt(originEast + x * cell, originNorth + z * cell);
            EncodedTile tile = new EncodedTile
            {
                Id = id,
                Layer = TileLayer.Ground,
                Posts = posts,
                CellM = cell,
                OriginEast = originEast,
                OriginNorth = originNorth,
                Bytes = Pack(heights, posts),
            };
            tile.Crc32 = Crc32.Compute(tile.Bytes);
            return tile;
        }

        /// <summary>
        /// A tile of the water standing over the same tile's ground, metres: the world's surface layer less the
        /// ground the client is sent, which is zero wherever the ground is dry. The two are read at the same
        /// posts, so the surface the client puts back together is exact to the centimetre each was quantised to.
        /// </summary>
        public static EncodedTile EncodeDepth(RegionRaster surface, Heightfield ground, TileGrid grid, TileId id)
        {
            if (surface == null) throw new ArgumentNullException(nameof(surface));
            if (ground == null) throw new ArgumentNullException(nameof(ground));
            float[,] depth = Sampled(grid, id, ground.CellM, out int posts, out double originEast, out double originNorth,
                (east, north) => (float)(ValueAt(surface, east, north) - ground.HeightAt(east, north)));
            EncodedTile tile = new EncodedTile
            {
                Id = id,
                Layer = TileLayer.WaterDepth,
                Posts = posts,
                CellM = ground.CellM,
                OriginEast = originEast,
                OriginNorth = originNorth,
                Bytes = Pack(depth, posts),
            };
            tile.Crc32 = Crc32.Compute(tile.Bytes);
            return tile;
        }

        /// <summary>A tile of a code layer: the raster's own codes at each post, refused if any exceeds a byte.</summary>
        public static EncodedTile EncodeCodes(RegionRaster layer, TileLayer which, TileGrid grid, TileId id)
        {
            if (layer == null) throw new ArgumentNullException(nameof(layer));
            if (!layer.IsIntegral) throw new ArgumentException("layer '" + layer.Layer + "' is " + layer.Dtype + ", not a code layer", nameof(layer));
            double cell = layer.CellM;
            byte[,] codes = null;
            float[,] widened = Sampled(grid, id, cell, out int posts, out double originEast, out double originNorth, (east, north) =>
            {
                CellAt(layer, east, north, out int row, out int col);
                uint code = layer.Code(row, col);
                if (code > byte.MaxValue) throw new InvalidDataException("layer '" + layer.Layer + "' has the code " + code + ", which a tile carries as one byte");
                return code;
            });
            codes = new byte[posts, posts];
            for (int z = 0; z < posts; z++)
                for (int x = 0; x < posts; x++) codes[z, x] = (byte)widened[z, x];
            EncodedTile tile = new EncodedTile
            {
                Id = id,
                Layer = which,
                Posts = posts,
                CellM = cell,
                OriginEast = originEast,
                OriginNorth = originNorth,
                Bytes = PackCodes(codes, posts),
            };
            tile.Crc32 = Crc32.Compute(tile.Bytes);
            return tile;
        }

        /// <summary>
        /// A far layer's tile (M1.6d): for each post <see cref="TileLayers.FarCellM"/> apart, what the square of stand cells
        /// round it holds (<see cref="FarSquare"/>) — how many trees, for the far count, and for the far stand the tall
        /// plant most of them are at their mean height. Worked out from the world's stand when a tile is first asked for.
        /// </summary>
        public static EncodedTile EncodeFar(RegionRaster stand, TileLayer which, TileGrid grid, TileId id)
        {
            if (stand == null) throw new ArgumentNullException(nameof(stand));
            if (!stand.IsIntegral) throw new ArgumentException("the stand is " + stand.Dtype + ", not a code layer", nameof(stand));
            if (!TileLayers.IsFar(which)) throw new ArgumentException(which + " is not a far layer", nameof(which));
            int span = FarSpan(stand);
            if (span == 0)
                throw new ArgumentException("a far square of " + TileLayers.FarCellM + " m is not a whole number of the stand's " + stand.CellM + " m cells", nameof(stand));
            float[,] read = Sampled(grid, id, TileLayers.FarCellM, out int posts, out double originEast, out double originNorth, (east, north) =>
            {
                CellAt(stand, east, north, out int row, out int col);
                return FarSquare(stand, row, col, span, which);
            });
            byte[,] codes = new byte[posts, posts];
            for (int z = 0; z < posts; z++)
                for (int x = 0; x < posts; x++) codes[z, x] = (byte)read[z, x];
            EncodedTile tile = new EncodedTile
            {
                Id = id,
                Layer = which,
                Posts = posts,
                CellM = TileLayers.FarCellM,
                OriginEast = originEast,
                OriginNorth = originNorth,
                Bytes = PackCodes(codes, posts),
            };
            tile.Crc32 = Crc32.Compute(tile.Bytes);
            return tile;
        }

        /// <summary>
        /// How many of the stand's cells make a far square's side (M1.6d), or zero when they do not make it whole: a far
        /// post takes whole cells, so that no tree is counted by two, and a stand of cells that cannot is served no far layer.
        /// </summary>
        public static int FarSpan(RegionRaster stand)
        {
            if (stand == null || !(stand.CellM > 0.0)) return 0;
            int span = (int)Math.Round(TileLayers.FarCellM / stand.CellM);
            return span >= 1 && Math.Abs(span * stand.CellM - TileLayers.FarCellM) <= 1e-6 ? span : 0;
        }

        /// <summary>
        /// What the square of stand cells round a cell holds (M1.6d): the span of cells from half a span before it to just
        /// short of half a span after, in rows and in columns, so that two neighbouring far posts never count one tree
        /// twice. For the far count, how many trees stand in it, to a byte; for the far stand, the tall plant most of them
        /// are — the first in <see cref="StandCodes.Tall"/>'s order on a tie — packed by <see cref="StandCodes.Pack"/> at
        /// those trees' mean height, or zero where no tree stands.
        /// </summary>
        public static uint FarSquare(RegionRaster stand, int row, int col, int span, TileLayer which)
        {
            if (stand == null) throw new ArgumentNullException(nameof(stand));
            int tall = StandCodes.Tall.Count;
            int[] trees = new int[tall + 1];
            double[] metres = new double[tall + 1];
            int count = 0;
            int before = span / 2;
            for (int r = row - before; r < row - before + span; r++)
            {
                if (r < 0 || r >= stand.Height) continue;
                for (int c = col - before; c < col - before + span; c++)
                {
                    if (c < 0 || c >= stand.Width) continue;
                    uint code = stand.Code(r, c);
                    int index = (int)(code >> StandCodes.SpeciesShift);
                    if (code == 0 || index < 1 || index > tall) continue;
                    count++;
                    trees[index]++;
                    metres[index] += StandCodes.HeightOf((byte)code);
                }
            }
            if (which == TileLayer.FarCount) return (uint)Math.Min(count, byte.MaxValue);
            if (count == 0) return 0u;
            int most = 1;
            for (int i = 2; i <= tall; i++)
                if (trees[i] > trees[most]) most = i;
            return StandCodes.Pack(StandCodes.Tall[most - 1], metres[most] / trees[most]);
        }

        /// <summary>Walks a tile's posts, north-then-east as the packing does, and collects what a reader returns.</summary>
        private static float[,] Sampled(TileGrid grid, TileId id, double cellM, out int posts, out double originEast, out double originNorth, Func<double, double, float> read)
        {
            if (grid == null) throw new ArgumentNullException(nameof(grid));
            if (!grid.Contains(id)) throw new ArgumentOutOfRangeException(nameof(id), "tile " + id + " is outside the grid");
            posts = (int)Math.Round(grid.TileSizeM / cellM) + 1;
            grid.Origin(id, out originEast, out originNorth);
            float[,] values = new float[posts, posts];
            for (int z = 0; z < posts; z++)
                for (int x = 0; x < posts; x++)
                    values[z, x] = read(originEast + x * cellM, originNorth + z * cellM);
            return values;
        }

        /// <summary>
        /// The raster cell a post sits on. A tile's posts land on cell centres, so this is a lookup rather than a
        /// sample: a code layer must not be interpolated, and a lake's id averaged with the dry ground beside it
        /// would be neither.
        /// </summary>
        private static void CellAt(RegionRaster raster, double east, double north, out int row, out int col)
        {
            CellOf(raster.ExtentM, raster.CellM, east, north, out row, out col);
            col = Math.Min(raster.Width - 1, Math.Max(0, col));
            row = Math.Min(raster.Height - 1, Math.Max(0, row));
        }

        /// <summary>
        /// The raster cell a point of a region sits on, for a reader holding a tile rather than the raster: a client
        /// placing what stands on a post (M1.6a) reads a post's cell here, so the tile's writer and its reader agree on
        /// it. A tile's posts land on cell centres, and a post's east and north are whole multiples of the cell, so this
        /// is exact.
        /// </summary>
        public static void CellOf(double extentM, double cellM, double east, double north, out int row, out int col)
        {
            double half = extentM * 0.5;
            col = (int)Math.Round((east + half) / cellM);
            row = (int)Math.Round((half - north) / cellM);
        }

        private static float ValueAt(RegionRaster raster, double east, double north)
        {
            CellAt(raster, east, north, out int row, out int col);
            return raster[row, col];
        }

        /// <summary>Packs a square of heights: centimetres, each row a 32-bit first post then 16-bit steps between neighbours, deflate (version 3).</summary>
        public static byte[] Pack(float[,] heights, int posts)
        {
            using (MemoryStream output = new MemoryStream())
            {
                using (DeflateStream deflate = new DeflateStream(output, CompressionLevel.Optimal, leaveOpen: true))
                using (BinaryWriter w = new BinaryWriter(deflate))
                {
                    for (int z = 0; z < posts; z++)
                    {
                        int previous = 0;
                        for (int x = 0; x < posts; x++)
                        {
                            int cm = ToCentimetres(heights[z, x]);
                            if (x == 0)
                            {
                                w.Write(cm);
                                previous = cm;
                                continue;
                            }
                            int step = cm - previous;
                            if (step < short.MinValue || step > short.MaxValue)
                                throw new InvalidDataException("a step of " + step / 100.0 + " m between neighbouring posts is longer than a tile can carry");
                            w.Write((short)step);
                            previous = cm;
                        }
                    }
                }
                return output.ToArray();
            }
        }

        /// <summary>Unpacks a square of heights, metres, indexed [north, east].</summary>
        public static float[,] Unpack(byte[] bytes, int posts)
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));
            if (posts < 2) throw new ArgumentOutOfRangeException(nameof(posts));
            float[,] heights = new float[posts, posts];
            using (MemoryStream input = new MemoryStream(bytes))
            using (DeflateStream inflate = new DeflateStream(input, CompressionMode.Decompress))
            using (BinaryReader r = new BinaryReader(inflate))
            {
                for (int z = 0; z < posts; z++)
                {
                    int previous = 0;
                    for (int x = 0; x < posts; x++)
                    {
                        int cm;
                        try
                        {
                            cm = x == 0 ? r.ReadInt32() : previous + r.ReadInt16();
                        }
                        catch (EndOfStreamException)
                        {
                            throw new InvalidDataException("tile data ends after " + (z * posts + x) + " of " + (posts * posts) + " posts");
                        }
                        heights[z, x] = cm / 100f;
                        previous = cm;
                    }
                }
                if (inflate.ReadByte() != -1)
                    throw new InvalidDataException("tile data has bytes beyond " + (posts * posts) + " posts");
            }
            return heights;
        }

        /// <summary>Packs a square of codes: the raw bytes through deflate.</summary>
        public static byte[] PackCodes(byte[,] codes, int posts)
        {
            using (MemoryStream output = new MemoryStream())
            {
                using (DeflateStream deflate = new DeflateStream(output, CompressionLevel.Optimal, leaveOpen: true))
                {
                    for (int z = 0; z < posts; z++)
                        for (int x = 0; x < posts; x++) deflate.WriteByte(codes[z, x]);
                }
                return output.ToArray();
            }
        }

        /// <summary>Unpacks a square of codes, indexed [north, east].</summary>
        public static byte[,] UnpackCodes(byte[] bytes, int posts)
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));
            if (posts < 2) throw new ArgumentOutOfRangeException(nameof(posts));
            byte[,] codes = new byte[posts, posts];
            using (MemoryStream input = new MemoryStream(bytes))
            using (DeflateStream inflate = new DeflateStream(input, CompressionMode.Decompress))
            {
                for (int z = 0; z < posts; z++)
                    for (int x = 0; x < posts; x++)
                    {
                        int b = inflate.ReadByte();
                        if (b < 0) throw new InvalidDataException("tile data ends after " + (z * posts + x) + " of " + (posts * posts) + " posts");
                        codes[z, x] = (byte)b;
                    }
                if (inflate.ReadByte() != -1)
                    throw new InvalidDataException("tile data has bytes beyond " + (posts * posts) + " posts");
            }
            return codes;
        }

        private static int ToCentimetres(float metres)
        {
            if (float.IsNaN(metres)) throw new InvalidDataException("a NaN height cannot be encoded");
            if (Math.Abs(metres) > MaxHeightM)
                throw new InvalidDataException("a height of " + metres + " m is beyond the " + MaxHeightM + " m either side of the datum that a tile can carry");
            return (int)Math.Round(metres * 100.0);
        }
    }
}
