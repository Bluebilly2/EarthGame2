using System;

namespace EarthGame.Engine
{
    /// <summary>
    /// How finely the rock underfoot changes — taken from how finely it is actually mapped. Ported from v1 (slice
    /// E0a) with the lattice moved from the sphere to the region's own plane.
    ///
    /// <para>The NSW Seamless Geology (Geological Survey of NSW, 1:100 000) holds <b>49 mapped bodies of 10 named
    /// units</b> in a 10.0 × 9.8 km box centred on v1's wake point in the Southern Highlands. Mean body area
    /// 2.0 km², equivalent square edge 1,414 m. A second derivation from the same survey agrees: the Mittagong
    /// Formation is up to 15 m thick at its type area, and on a 5–10% hillslope a 15 m unit crops out in a band of
    /// 15/0.07 ≈ 210 m. The mapped minimum and the stratigraphic arithmetic land in the same place.</para>
    ///
    /// <para><b>This is a resolution ceiling, not a fact about the Earth.</b> It is one survey at one map scale in
    /// one region; Bherwerre's 1:25 000 sheet would resolve finer bodies and lower it, and when that sheet is read
    /// the lattice gives way to the map. What it replaced in v1 was one number doing untold damage: provinces
    /// 375 km across, so the whole playable world took one frozen draw of stone and the founder could not strike a
    /// flake anywhere.</para>
    ///
    /// <para>The lattice lies on the region's tangent plane here rather than on the sphere: a region is a
    /// bounded box on a plane (ARCHITECTURE §3), and a plane lattice with the same edge and the same warp gives
    /// the same-sized provinces without any trigonometry between a position and its cell.</para>
    /// </summary>
    public static class GeologyScale
    {
        /// <summary>
        /// Mapped bodies per square metre: 49 in a 10.0 × 9.8 km box, NSW Seamless Geology at
        /// 1:100 000. The measurement, kept as the measurement rather than as its consequence.
        /// </summary>
        public const double MappedBodiesPerSquareMetre = 49.0 / 98.0e6;

        /// <summary>The edge of a square of that mean area, m — about 1,414 m. The only place the number exists.</summary>
        public static readonly double ProvinceEdgeM = 1.0 / Math.Sqrt(MappedBodiesPerSquareMetre);

        /// <summary>
        /// How many provinces a disc of this radius sees, to within counting error. Not the area ratio: a disc
        /// laid over a grid touches every cell its interior covers and every cell its rim clips, so the perimeter
        /// term is real and at playable radii it is most of the answer.
        /// </summary>
        public static double ProvincesWithin(double discRadiusM)
        {
            double a = ProvinceEdgeM;
            double r = Math.Max(0.0, discRadiusM);
            return Math.PI * r * r / (a * a) + 4.0 * r / a + 1.0;
        }

        /// <summary>
        /// A stable hash of a point's province on the plane, warped so that contacts wander like contacts rather
        /// than lying along the grid: value noise off the same hash, amplitude a third of a cell, so no cell loses
        /// its size. Bit-exact for a given position and seed; the seed is the world's, so two worlds draw two
        /// geologies and one world draws the same one every time.
        /// </summary>
        public static uint ProvinceHash(double eastM, double northM, ulong seed)
        {
            double a = ProvinceEdgeM;
            int cx = (int)Math.Floor(eastM / a);
            int cz = (int)Math.Floor(northM / a);
            uint warpSeed = Avalanche(Mix(Mix(Mix(374761393u ^ (uint)seed, (uint)cx), (uint)cz), (uint)(seed >> 32)));
            double wx = ((warpSeed & 0xFFFF) / 65535.0 - 0.5) * 2.0;
            double wz = (((warpSeed >> 16) & 0xFFFF) / 65535.0 - 0.5) * 2.0;
            const double Amplitude = 0.3;
            double e = eastM + wx * Amplitude * a;
            double n = northM + wz * Amplitude * a;
            int px = (int)Math.Floor(e / a);
            int pz = (int)Math.Floor(n / a);
            return Avalanche(Mix(Mix(Mix(2166361261u ^ (uint)seed, (uint)px), (uint)pz), (uint)(seed >> 32)));
        }

        private static uint Mix(uint h, uint v) { unchecked { return (h ^ v) * 16777619u; } }

        /// <summary>Spreads a key across the whole word, so neighbouring cells do not return neighbouring numbers.</summary>
        private static uint Avalanche(uint h)
        {
            unchecked
            {
                h ^= h >> 13;
                h *= 0x5bd1e995u;
                h ^= h >> 15;
                return h;
            }
        }
    }
}
