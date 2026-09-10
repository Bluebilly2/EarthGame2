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
    /// </summary>
    public static class TileCodec
    {
        public const int Version = 2;
        /// <summary>The largest height a tile can carry, metres; the region raster is well inside it.</summary>
        public const double MaxHeightM = 327.0;

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
            double half = raster.ExtentM * 0.5;
            col = (int)Math.Round((east + half) / raster.CellM);
            row = (int)Math.Round((half - north) / raster.CellM);
            col = Math.Min(raster.Width - 1, Math.Max(0, col));
            row = Math.Min(raster.Height - 1, Math.Max(0, row));
        }

        private static float ValueAt(RegionRaster raster, double east, double north)
        {
            CellAt(raster, east, north, out int row, out int col);
            return raster[row, col];
        }

        /// <summary>Packs a square of heights: centimetres, row deltas, deflate.</summary>
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
                            w.Write((short)(x == 0 ? cm : cm - previous));
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
                        short v;
                        try
                        {
                            v = r.ReadInt16();
                        }
                        catch (EndOfStreamException)
                        {
                            throw new InvalidDataException("tile data ends after " + (z * posts + x) + " of " + (posts * posts) + " posts");
                        }
                        int cm = x == 0 ? v : previous + v;
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
            double clamped = SimMath.Clamp(metres, -MaxHeightM, MaxHeightM);
            return (int)Math.Round(clamped * 100.0);
        }
    }
}
