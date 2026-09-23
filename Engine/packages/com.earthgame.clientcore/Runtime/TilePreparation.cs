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
        /// <summary>The checksum of the cover its heights' relief was grown from (BF.4); zero when none had arrived, and then it has none.</summary>
        public uint ReliefCrc;
        public double OriginEast;
        public double OriginNorth;
        /// <summary>The tile's side in metres.</summary>
        public float SizeM;
        /// <summary>Posts along one side of the built ground.</summary>
        public int Posts;
        /// <summary>Heights as a fraction of <see cref="RangeM"/> above <see cref="BaseM"/>, indexed [north, east].</summary>
        public float[,] Normalised;
        public float BaseM;
        /// <summary>Never less than <see cref="TilePreparation.DigRoomM"/>, the room below the lowest post for a hole (BF.4), so never the division by zero a flat tile once was.</summary>
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
    /// thread is only what Unity will not do anywhere else. Since BF.4 the posts are the one ground's
    /// (<see cref="ClientGround"/>): the tile's heights with the relief its cover grows and the hollows dug in it.
    /// </summary>
    public static class TilePreparation
    {
        /// <summary>
        /// How far below its lowest post a tile's Terrain can reach, m (BF.4): room for the deepest hole a dig makes, a byte of
        /// centimetres, so a hollow resampled into the Terrain is never clipped at its floor.
        /// </summary>
        public const float DigRoomM = 2.6f;

        /// <summary>
        /// The posts of a square over one tile, as [north, east], read bilinearly between the posts the wire
        /// carried. The tile's own posts and no neighbour's: see <see cref="TileGround"/>.
        /// </summary>
        public static float[,] SamplePosts(ReceivedTile tile, int posts, double spacing, out float minM, out float maxM)
            => SamplePosts(tile, null, 0UL, null, 0.0, posts, spacing, out minM, out maxM);

        /// <summary>
        /// The posts of a square over one tile, as [north, east], from the one ground (BF.4): the tile's heights with the
        /// relief its cover grows (none without a cover) and the hollows dug in it (none without <paramref name="dugCm"/>).
        /// The tile's own posts and no neighbour's, as <see cref="TileGround"/> reads them.
        /// </summary>
        public static float[,] SamplePosts(ReceivedTile tile, ReceivedTile cover, ulong seed, Func<int, int, byte> dugCm, double extentM,
                                           int posts, double spacing, out float minM, out float maxM)
        {
            if (tile == null) throw new ArgumentNullException(nameof(tile));
            if (posts < 2) throw new ArgumentOutOfRangeException(nameof(posts), "a square has at least two posts a side");
            if (dugCm != null && !(extentM > 0.0)) throw new ArgumentException("the hollows are found by the region's rows and columns, and no region was given", nameof(extentM));
            float[,] heights = new float[posts, posts];
            minM = float.MaxValue;
            maxM = float.MinValue;
            for (int z = 0; z < posts; z++)
            {
                double north = tile.OriginNorth + z * spacing;
                for (int x = 0; x < posts; x++)
                {
                    float h = (float)ClientGround.HeightAt(tile, cover, seed, extentM, dugCm, tile.OriginEast + x * spacing, north);
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
            => Prepare(tile, posts, cover, texels, elapsedMs, 0UL, null, null);

        /// <param name="seed">The world's seed, which the relief is grown from.</param>
        /// <param name="dugCm">The depth dug on each cell of the tile, a copy the worker alone reads (<see cref="ClientGround.DugIn"/>); null for none.</param>
        /// <param name="grid">The region's tiles, whose rows and columns the hollows are kept by; needed only with <paramref name="dugCm"/>.</param>
        /// <param name="relief">False leaves the relief out of the posts while the cover still colours them (<c>-eg-hide relief</c>, BF.4), for a frame beside one with it.</param>
        public static PreparedTile Prepare(ReceivedTile tile, int posts, ReceivedTile cover, int texels, Func<double> elapsedMs, ulong seed, Func<int, int, byte> dugCm, TileGrid grid,
                                           bool relief = true)
        {
            if (tile == null) throw new ArgumentNullException(nameof(tile));
            if (tile.Layer != TileLayer.Ground) throw new ArgumentException("a tile is prepared from its ground, not from " + tile.Layer, nameof(tile));
            double started = elapsedMs != null ? elapsedMs() : 0.0;
            float sizeM = (float)((tile.Posts - 1) * tile.CellM);
            double spacing = sizeM / (double)(posts - 1);
            ReceivedTile grown = relief && ClientGround.SamePosts(tile, cover) ? cover : null;
            float[,] sampled = SamplePosts(tile, grown, seed, dugCm, grid != null ? grid.ExtentM : 0.0, posts, spacing, out float minM, out float maxM);
            // Room below the lowest post for a hole dug later (BF.4): the Terrain holds heights above its base and no lower. Until
            // then the range was held to a metre at least, which the room now always is.
            minM -= DigRoomM;
            float range = maxM - minM;
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
                ReliefCrc = grown != null ? grown.Crc32 : 0u,
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
