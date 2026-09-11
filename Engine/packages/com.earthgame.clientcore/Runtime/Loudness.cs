using System;

namespace EarthGame.ClientCore
{
    /// <summary>How loud the founder's own sounds are (M1.5c; v1's <c>Soundscape</c>, ported): quiet, and in proportion to what makes them.</summary>
    public static class Loudness
    {
        /// <summary>The loudest a footfall gets: a footstep should never be the loudest thing in the world (v1).</summary>
        public const double FootfallCeiling01 = 0.28;

        /// <summary>Below this a landing is not heard, J (v1).</summary>
        public const double LandingFloorJ = 0.02;

        /// <summary>The energy at which a landing is as loud as it gets, J (v1).</summary>
        public const double LandingFullJ = 12.0;

        /// <summary>
        /// How loud a footfall is at a speed, 0–1: quiet, and louder the faster the founder goes, which is the whole reason
        /// moving slowly is a thing people do when they do not want to be heard (v1). Silent below walking pace.
        /// </summary>
        public static double Footfall01(double speedMs)
        {
            if (!(speedMs >= Stride.WalkingMs)) return 0.0;
            return Math.Max(0.05, Math.Min(FootfallCeiling01, 0.06 + speedMs * 0.035));
        }

        /// <summary>
        /// How loud a landing is, from the energy it puts into the ground, 0–1: by its square root, because an ear does not
        /// hear energy in proportion, so a stone set down is heard and the same stone dropped from head height deafens
        /// nobody (v1).
        /// </summary>
        public static double Landing01(double joules)
        {
            if (!(joules >= LandingFloorJ)) return 0.0;
            return Math.Min(1.0, Math.Sqrt(joules / LandingFullJ));
        }
    }
}
