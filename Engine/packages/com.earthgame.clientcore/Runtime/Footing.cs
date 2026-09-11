using System;
using EarthGame.Engine;

namespace EarthGame.ClientCore
{
    /// <summary>What a footfall sounds of (M1.5c): the grounds an ear tells apart, v1's five and water.</summary>
    public enum FootingSound : byte
    {
        Soil = 0,
        Grass = 1,
        Litter = 2,
        Rock = 3,
        Sand = 4,
        Water = 5,
    }

    /// <summary>
    /// What is underfoot, in the terms an ear cares about (M1.5c): read off the cover the server streams (M1.4d), so the
    /// ground sounds like what it is drawn as, and off the water standing over it. v1 worked its surface out in the
    /// client from the ecology (sand first, then thin soil, then shade, then what grows); here the server has already said
    /// what covers each cell, and this is only the ear's grouping of it.
    /// </summary>
    public static class Footing
    {
        /// <summary>Water deeper than this over the ground is heard underfoot, m: over the top of a foot.</summary>
        public const double WetDepthM = 0.05;

        /// <summary>The sound of a cover code (the wetness quarter beside it changes nothing an ear hears) with water of a depth standing over it.</summary>
        public static FootingSound Of(byte coverCode, double waterDepthM)
        {
            if (waterDepthM >= WetDepthM) return FootingSound.Water;
            switch (GroundCovers.CoverOf(coverCode))
            {
                case GroundCover.Sea:
                case GroundCover.FreshWater:
                    return FootingSound.Water;
                case GroundCover.Sand:
                case GroundCover.DuneSand:
                    return FootingSound.Sand;
                case GroundCover.Rock:
                    return FootingSound.Rock;
                case GroundCover.Grass:
                case GroundCover.Sedge:
                case GroundCover.Bracken:
                    return FootingSound.Grass;
                case GroundCover.ForestFloor:
                case GroundCover.Heath:
                    return FootingSound.Litter;
                default:
                    // Bare earth, a swamp's floor, and a cover nothing has said or this build does not know.
                    return FootingSound.Soil;
            }
        }

        /// <summary>
        /// The sound underfoot at a point, from the tile of cover and the tile of the water's depth that hold it (either may
        /// be missing), and whether the founder is wading.
        /// </summary>
        public static FootingSound At(ReceivedTile cover, ReceivedTile depth, double east, double north, bool wading)
        {
            if (wading) return FootingSound.Water;
            double metres = depth?.Heights != null ? TileGround.HeightAt(depth, east, north) : 0.0;
            return Of(CodeAt(cover, east, north), metres);
        }

        /// <summary>
        /// The code at a point of a tile of codes: its nearest post's, since a code is its cell's and is never blended; 0
        /// (nothing said) with no tile.
        /// </summary>
        public static byte CodeAt(ReceivedTile tile, double east, double north)
        {
            if (tile?.Codes == null || tile.Posts <= 0 || !(tile.CellM > 0.0)) return 0;
            int last = tile.Posts - 1;
            int x = (int)Math.Round((east - tile.OriginEast) / tile.CellM);
            int z = (int)Math.Round((north - tile.OriginNorth) / tile.CellM);
            if (x < 0) x = 0; else if (x > last) x = last;
            if (z < 0) z = 0; else if (z > last) z = last;
            return tile.Codes[z, x];
        }
    }
}
