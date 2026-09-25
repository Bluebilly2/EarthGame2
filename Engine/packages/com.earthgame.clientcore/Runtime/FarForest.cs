using System;
using System.Collections.Generic;
using EarthGame.Engine;

namespace EarthGame.ClientCore
{
    /// <summary>A tree of the far forest as the client draws it (M1.6d): one for each far square that holds any tree.</summary>
    public struct FarTree
    {
        public float East;
        public float Up;
        public float North;
        public float YawDeg;
        public float HeightM;
        /// <summary>How wide its crown is drawn, m: as wide as all its square's crowns together would cover.</summary>
        public float CrownM;
        /// <summary>The tall plant most of its square's trees are, as an index into <see cref="StandCodes.Tall"/>.</summary>
        public int Tall;
        /// <summary>
        /// The farthest level of thinning it is drawn at (M1.6f), 0 to <see cref="FarForest.MostLevel"/>, from its square's hash:
        /// a tree is drawn at a level no higher than its own, so one in 2^L stands at level L and every level's trees stand at
        /// the levels below it.
        /// </summary>
        public int Level;
    }

    /// <summary>
    /// The forest beyond the stand tiles a client holds (M1.6d), placed for drawing from the two far layers: for each far
    /// square that holds a tree, one far tree standing inside the square where a hash of its post puts it, as tall as the
    /// square's trees are on average, with a crown as wide as all their crowns would cover — never wider than the square
    /// allows — on whatever ground the client draws there. Each tile places its posts but its last row and column, which
    /// are the next tile's first, as a stand tile's trees are placed, so every square is placed by one tile.
    /// </summary>
    public static class FarForest
    {
        /// <summary>How much wider than its square a far crown may be drawn, so that a closed canopy shows no seams between squares.</summary>
        public const double MostCrownShare = 1.25;

        /// <summary>How far a far tree may stand from its square's post, m: a quarter of the square, so it never leaves its square.</summary>
        public static readonly double WanderM = TileLayers.FarCellM * 0.25;

        /// <summary>
        /// The highest level of thinning (M1.6f): past it one tree in 2^MostLevel stands, whatever the distance. Eight, one in 256
        /// past 16 km, where a crown spread sixteenfold is as wide on the screen as a whole one at a kilometre: the first cap,
        /// four, left the 32 km valley drawing 14,669 far trees at its escarpment, most of them past 4 km at one in sixteen.
        /// </summary>
        public const int MostLevel = 8;

        /// <summary>
        /// Within this of the eye the whole far forest is drawn, m (M1.6f); past it the level rises by one each time the
        /// distance grows by the root of two, so the trees drawn stand as densely on the screen at every distance.
        /// </summary>
        public const double FullToM = 1414.0;

        /// <summary>The level a block of the far forest is drawn at, from its distance from the eye, m.</summary>
        public static int LevelAt(double distanceM)
        {
            if (!(distanceM >= FullToM)) return 0;
            int level = 1 + (int)Math.Floor(2.0 * Math.Log(distanceM / FullToM, 2.0));
            return level > MostLevel ? MostLevel : level;
        }

        /// <summary>How much wider a crown is drawn at a level, so the one in 2^L kept covers what all of them did.</summary>
        public static double SpreadAt(int level) => Math.Sqrt(1 << Math.Max(0, Math.Min(MostLevel, level)));

        /// <summary>
        /// The far trees of one tile, added to a list, from its far stand and far count tiles and the ground they stand on. A
        /// tile whose two layers are not the same tile's places nothing.
        /// </summary>
        public static void Place(ReceivedTile farStand, ReceivedTile farCount, IHeightSource ground, TileGrid grid, List<FarTree> into)
        {
            if (farStand?.WideCodes == null || farCount?.Codes == null || grid == null || into == null) return;
            if (!farStand.Id.Equals(farCount.Id) || farStand.Posts != farCount.Posts || !(farStand.CellM > 0.0)) return;
            int posts = farStand.Posts;
            double cell = farStand.CellM;
            int lastX = farStand.Id.Ix == grid.TilesPerSide - 1 ? posts - 1 : posts - 2;
            int lastZ = farStand.Id.Iz == grid.TilesPerSide - 1 ? posts - 1 : posts - 2;
            for (int z = 0; z <= lastZ; z++)
                for (int x = 0; x <= lastX; x++)
                {
                    ushort code = farStand.WideCodes[z, x];
                    int count = farCount.Codes[z, x];
                    if (code == 0 || count <= 0) continue;
                    int tall = StandCodes.TallIndexOf(code);
                    if (tall < 0) continue;
                    PlantSpecies species = StandCodes.Tall[tall];
                    double postEast = farStand.OriginEast + x * cell;
                    double postNorth = farStand.OriginNorth + z * cell;
                    // The post's place in whole centimetres names the square in every tile and every client alike.
                    long eastCm = (long)Math.Round(postEast * 100.0), northCm = (long)Math.Round(postNorth * 100.0);
                    ulong h = StandLayout.Mix(((ulong)eastCm << 32) ^ (ulong)northCm ^ 0xFA7F0E57UL);
                    double east = postEast + WanderM * ((h & 0xFFFF) / 32767.5 - 1.0);
                    double north = postNorth + WanderM * (((h >> 16) & 0xFFFF) / 32767.5 - 1.0);
                    double up = ground != null ? ground.HeightAt(east, north) : 0.0;
                    if (double.IsNaN(up)) continue;
                    double height = StandCodes.HeightOf(code);
                    double crown = Math.Min(species.CrownShare * height * Math.Sqrt(count), cell * MostCrownShare);
                    into.Add(new FarTree
                    {
                        East = (float)east,
                        Up = (float)up,
                        North = (float)north,
                        YawDeg = (float)((h >> 32) % 360UL),
                        HeightM = (float)height,
                        CrownM = (float)crown,
                        Tall = tall,
                        Level = LevelOf(h),
                    });
                }
        }

        /// <summary>
        /// A square's level from a hash of its own, mixed again from the one its tree's place and yaw are drawn from so the
        /// two are not tied: how many of its low bits are zero from the lowest up, so a level of at least L comes once in 2^L,
        /// capped at <see cref="MostLevel"/>.
        /// </summary>
        private static int LevelOf(ulong h)
        {
            ulong own = StandLayout.Mix(h ^ 0x9E3779B97F4A7C15UL);
            int level = 0;
            while (level < MostLevel && (own & (1UL << level)) == 0) level++;
            return level;
        }
    }
}
