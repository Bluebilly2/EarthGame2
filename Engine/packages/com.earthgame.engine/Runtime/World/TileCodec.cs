using System;
using System.IO;
using System.IO.Compression;

namespace EarthGame.Engine
{
    /// <summary>A tile's heights as they travel: the posts along one side, the ground pitch, and the compressed bytes.</summary>
    public sealed class EncodedTile
    {
        public TileId Id;
        public int Posts;
        public double CellM;
        public double OriginEast;
        public double OriginNorth;
        public byte[] Bytes;
        public uint Crc32;
    }

    /// <summary>
    /// How a tile of heights is packed for the wire and the disk cache (ARCHITECTURE §10, tile format v1): the
    /// posts of a tile sampled straight from the raster at its own pitch (a kilometre at 4 m is 251 posts a
    /// side, sharing an edge post with each neighbour), each height as centimetres in a signed 16-bit integer,
    /// each row written as its first value then the difference to the previous post, the whole run through
    /// deflate. Nine tiles of the Bherwerre raster come to a few hundred kilobytes; the join budget is built on
    /// that number (N1). The decoder refuses a length that is not a square of posts.
    /// </summary>
    public static class TileCodec
    {
        public const int Version = 1;
        /// <summary>The largest height a tile can carry, metres; the region raster is well inside it.</summary>
        public const double MaxHeightM = 327.0;

        /// <summary>Samples a tile from the heightfield at the raster's own cell pitch and encodes it.</summary>
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
                Posts = posts,
                CellM = cell,
                OriginEast = originEast,
                OriginNorth = originNorth,
                Bytes = Pack(heights, posts),
            };
            tile.Crc32 = Crc32.Compute(tile.Bytes);
            return tile;
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

        private static int ToCentimetres(float metres)
        {
            if (float.IsNaN(metres)) throw new InvalidDataException("a NaN height cannot be encoded");
            double clamped = SimMath.Clamp(metres, -MaxHeightM, MaxHeightM);
            return (int)Math.Round(clamped * 100.0);
        }
    }
}
