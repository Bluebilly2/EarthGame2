using System;
using EarthGame.Engine;

namespace EarthGame.ClientCore
{
    /// <summary>
    /// Everything a streamed tile needs worked out before a renderer is asked for anything: the posts sampled and
    /// normalised, and the colour of its ground. Produced on a worker and handed to the main thread (M1.4e).
    /// </summary>
    public sealed class PreparedTile
    {
        public TileId Id;
        /// <summary>The checksum of the ground tile this was prepared from, so a stale result is recognised.</summary>
        public uint GroundCrc;
        /// <summary>The checksum of the cover it was coloured from; zero when no cover had arrived.</summary>
        public uint CoverCrc;
        public double OriginEast;
        public double OriginNorth;
        /// <summary>The tile's side in metres.</summary>
        public float SizeM;
        /// <summary>Posts along one side of the built ground.</summary>
        public int Posts;
        /// <summary>Heights as a fraction of <see cref="RangeM"/> above <see cref="BaseM"/>, indexed [north, east].</summary>
        public float[,] Normalised;
        public float BaseM;
        /// <summary>Never zero: a flat tile would otherwise be a division by zero.</summary>
        public float RangeM;
        /// <summary>The colour map, three bytes a texel, or null when no cover had arrived.</summary>
        public byte[] ColourMap;
        public int ColourTexels;
        /// <summary>What this cost off the main thread, milliseconds.</summary>
        public double WorkerMs;
    }

    /// <summary>
    /// The work a tile needs that a renderer is not required for. Pure over what the client already holds, so it
    /// runs on a worker: measured in Release on 2026-09-10, sampling a kilometre tile at 513 posts is 21.9 ms and
    /// its colour map 7.1 ms, against ARCHITECTURE §8's 1.5 ms of streaming a frame. What is left on the main
    /// thread is only what Unity will not do anywhere else.
    /// </summary>
    public static class TilePreparation
    {
        /// <summary>
        /// The posts of a square over one tile, as [north, east], read bilinearly between the posts the wire
        /// carried. The tile's own posts and no neighbour's: see <see cref="TileGround"/>.
        /// </summary>
        public static float[,] SamplePosts(ReceivedTile tile, int posts, double spacing, out float minM, out float maxM)
        {
            if (tile == null) throw new ArgumentNullException(nameof(tile));
            if (posts < 2) throw new ArgumentOutOfRangeException(nameof(posts), "a square has at least two posts a side");
            float[,] heights = new float[posts, posts];
            minM = float.MaxValue;
            maxM = float.MinValue;
            for (int z = 0; z < posts; z++)
            {
                double north = tile.OriginNorth + z * spacing;
                for (int x = 0; x < posts; x++)
                {
                    float h = (float)TileGround.HeightAt(tile, tile.OriginEast + x * spacing, north);
                    heights[z, x] = h;
                    if (h < minM) minM = h;
                    if (h > maxM) maxM = h;
                }
            }
            return heights;
        }

        /// <summary>
        /// Prepares one tile from the tile itself, so nothing the main thread owns is read while it runs.
        /// <paramref name="cover"/> may be null, and then the tile is prepared without a colour.
        /// </summary>
        /// <param name="elapsedMs">The worker's own clock, for the cost this records; a monotonic millisecond count.</param>
        public static PreparedTile Prepare(ReceivedTile tile, int posts, ReceivedTile cover, int texels, Func<double> elapsedMs = null)
        {
            if (tile == null) throw new ArgumentNullException(nameof(tile));
            if (tile.Layer != TileLayer.Ground) throw new ArgumentException("a tile is prepared from its ground, not from " + tile.Layer, nameof(tile));
            double started = elapsedMs != null ? elapsedMs() : 0.0;
            float sizeM = (float)((tile.Posts - 1) * tile.CellM);
            double spacing = sizeM / (double)(posts - 1);
            float[,] sampled = SamplePosts(tile, posts, spacing, out float minM, out float maxM);
            float range = Math.Max(1f, maxM - minM);
            float[,] normalised = new float[posts, posts];
            for (int z = 0; z < posts; z++)
                for (int x = 0; x < posts; x++) normalised[z, x] = (sampled[z, x] - minM) / range;
            PreparedTile prepared = new PreparedTile
            {
                Id = tile.Id,
                GroundCrc = tile.Crc32,
                OriginEast = tile.OriginEast,
                OriginNorth = tile.OriginNorth,
                SizeM = sizeM,
                Posts = posts,
                Normalised = normalised,
                BaseM = minM,
                RangeM = range,
                ColourTexels = texels,
            };
            if (cover != null && cover.Codes != null && cover.Id.Equals(tile.Id))
            {
                prepared.ColourMap = GroundColourMap.Build(cover, texels);
                prepared.CoverCrc = cover.Crc32;
            }
            prepared.WorkerMs = elapsedMs != null ? elapsedMs() - started : 0.0;
            return prepared;
        }
    }
}
