using System;

namespace EarthGame.Engine
{
    /// <summary>How the founder is moving.</summary>
    public enum Gait
    {
        Standing,
        Walking,
        Jogging,
        Running,
    }

    /// <summary>
    /// What is underfoot, which decides how much of each step is wasted. v1 called this enum <c>Terrain</c>; renamed
    /// in the port because the client layer uses both this namespace and Unity's own, where Terrain is a class.
    /// </summary>
    public enum GroundType
    {
        /// <summary>A path, or ground that has been walked before.</summary>
        Made,
        /// <summary>Open grass and light litter: most of this country.</summary>
        LightBrush,
        /// <summary>Bracken to the waist, fallen branches, tussock.</summary>
        HeavyBrush,
        /// <summary>Sand, scree, deep mud. Every step gives some of itself back.</summary>
        Loose,
    }

    /// <summary>
    /// Moving, and what it costs.
    ///
    /// <para>Two published functions do all of the work here, and neither was invented for this game. Tobler's
    /// hiking function says how fast a person walks on a slope; the Pandolf equation says what it costs them to do
    /// it carrying something. Both are used unchanged, which is the point: a founder who knows to contour around a
    /// spur instead of going over it is right, because the same curve that makes real people do that is running
    /// here. Ported from v1 (Assets/EarthGame/Sim/Body/Locomotion.cs) with the algorithm untouched; the gait
    /// multipliers are the code's, not the v1 contract's, and that disagreement is a recorded debt for the owner's
    /// hands to settle.</para>
    /// </summary>
    public static class Locomotion
    {
        // ---- Tobler (1993): W = 6 exp(-3.5 |S + 0.05|) km/h ----

        /// <summary>The 6 km/h in Tobler's function, as m/s.</summary>
        public const double ToblerBaseMs = 6000.0 / 3600.0;

        /// <summary>The 3.5 exponent.</summary>
        public const double ToblerExponent = 3.5;

        /// <summary>
        /// The offset that puts the peak on a gentle descent rather than on the flat. This is the bit of the
        /// function that surprises people, and it is why real traverses zigzag.
        /// </summary>
        public const double ToblerPeakSlope = -0.05;

        /// <summary>How fast a person walks on this slope, m/s. Slope is rise over run: positive uphill.</summary>
        public static double WalkingSpeedMs(double slope)
            => ToblerBaseMs * Math.Exp(-ToblerExponent * Math.Abs(slope - ToblerPeakSlope));

        /// <summary>What each gait multiplies the sustainable walking speed by.</summary>
        public static double GaitMultiplier(Gait gait)
        {
            switch (gait)
            {
                case Gait.Walking: return 1.0;
                case Gait.Jogging: return 1.7;
                case Gait.Running: return 2.6;
                default: return 0.0;
            }
        }

        /// <summary>
        /// How fast the founder actually moves: the ground, the gait, and the state of the body. Work capacity
        /// already carries thirst, exhaustion, hunger and illness, so a founder who has not eaten or slept moves
        /// like one. It is applied at less than full weight because a person in trouble still walks; they walk
        /// badly.
        /// </summary>
        public static double SpeedMs(double slope, Gait gait, double workCapacity01)
        {
            double multiplier = GaitMultiplier(gait);
            if (multiplier <= 0.0) return 0.0;
            double capacity = 0.45 + 0.55 * SimMath.Clamp01(workCapacity01);
            double speed = WalkingSpeedMs(slope) * TravelPaceFactor * multiplier * capacity;
            // Steep ground is slow. It is not a wall: Tobler's exponential keeps falling forever and hands back
            // 0.14 m/s on a 33-degree slope, which in a game is indistinguishable from being unable to move.
            return Math.Max(MinimumSpeedMs * multiplier, speed);
        }

        /// <summary>
        /// Tobler's function against the pace a person's legs actually keep. Tobler is fitted to <b>journey</b>
        /// times across terrain, so its 1.4 m/s on the flat already has the pauses, the route-finding and the
        /// picking-your-way baked in. Somebody actually walking moves faster than their own hour-average, and this
        /// is the difference between the two.
        /// </summary>
        public const double TravelPaceFactor = 1.3;

        /// <summary>Slowest a founder moves on ground they can stand on at all, m/s.</summary>
        public const double MinimumSpeedMs = 0.45;

        /// <summary>The fastest gait the founder can hold. A body at half capacity is not running anywhere.</summary>
        public static Gait FastestGait(double workCapacity01)
        {
            double c = SimMath.Clamp01(workCapacity01);
            if (c >= 0.75) return Gait.Running;
            if (c >= 0.45) return Gait.Jogging;
            return Gait.Walking;
        }

        // ---- Pandolf, Givoni & Goldman (1977) ----

        /// <summary>How much of each step a surface gives back. Pandolf's terrain coefficients.</summary>
        public static double TerrainFactor(GroundType ground)
        {
            switch (ground)
            {
                case GroundType.Made: return 1.0;
                case GroundType.LightBrush: return 1.2;
                case GroundType.HeavyBrush: return 1.5;
                case GroundType.Loose: return 1.8;
                default: return 1.0;
            }
        }

        /// <summary>
        /// Metabolic cost of moving, watts: <c>M = 1.5W + 2.0(W+L)(L/W)² + η(W+L)(1.5V² + 0.35VG)</c>, with
        /// Santee's correction on descent, where the bare equation over-predicts badly. Walking downhill is cheaper
        /// than walking on the flat until it gets steep enough that braking costs more than the grade saves.
        /// </summary>
        public static double MetabolicCostW(double bodyMassKg, double loadKg, double speedMs, double slope,
                                            GroundType ground = GroundType.LightBrush)
        {
            double w = Math.Max(1.0, bodyMassKg);
            double l = Math.Max(0.0, loadKg);
            double v = Math.Max(0.0, speedMs);
            double g = slope * 100.0; // Pandolf takes grade in per cent
            double eta = TerrainFactor(ground);

            // Santee's downhill correction was fitted on descents to about -20% grade. Past that its (G+6)² term
            // runs away: at -40% the pair of equations predict 1877 W to walk slowly down a hill, more than
            // sprinting on the flat. Held at the edge of the fitted range, with a braking term for what is
            // genuinely harder about steeper ground than that.
            const double SanteeLimitGrade = -20.0;
            double modelled = Math.Max(g, SanteeLimitGrade);

            double standing = 1.5 * w;
            double carrying = 2.0 * (w + l) * (l / w) * (l / w);
            double moving = eta * (w + l) * (SpeedTerm(v) + 0.35 * v * modelled);
            double total = standing + carrying + moving;

            if (modelled < 0.0)
            {
                // Without this the grade term takes the whole equation below resting, which is not a thing bodies
                // do: walking downhill is cheaper than walking on the flat, not free and not negative.
                double correction = eta * ((modelled * (w + l)) / 3.5
                                           - ((w + l) * (modelled + 6.0) * (modelled + 6.0)) / w
                                           + (25.0 - v * v));
                total -= correction;
            }

            // Below the fitted range, every extra degree of steepness is braking: eccentric work, which is what
            // makes a long descent hurt the next day.
            if (g < SanteeLimitGrade)
                total += eta * (w + l) * 0.02 * (SanteeLimitGrade - g);

            // Nothing about moving can cost less than standing still does.
            return Math.Max(standing, total);
        }

        /// <summary>
        /// Where walking stops being walking. Pandolf's <c>1.5V²</c> is a load-carriage model fitted on people
        /// walking; above about 2 m/s it runs away with itself. Running is not fast walking: its cost per metre is
        /// roughly constant, so its cost per second is linear in speed. The two meet exactly at 2 m/s
        /// (1.5 × 2² = 6 and 3.0 × 2 = 6), so the founder never feels a step in the cost as they break into a run.
        /// </summary>
        public const double GaitChangeMs = 2.0;

        private static double SpeedTerm(double v) => v <= GaitChangeMs ? 1.5 * v * v : 3.0 * v;

        /// <summary>Cost above simply standing there, watts. What the movement itself is worth.</summary>
        public static double ActivityCostW(double bodyMassKg, double loadKg, double speedMs, double slope,
                                           GroundType ground = GroundType.LightBrush)
            => Math.Max(0.0, MetabolicCostW(bodyMassKg, loadKg, speedMs, slope, ground) - 1.5 * Math.Max(1.0, bodyMassKg));

        /// <summary>
        /// Hours to cover a distance at a steady gait on a given slope: what the founder is really deciding when
        /// they look at a hill and think about going round it.
        /// </summary>
        public static double HoursToCover(double metres, double slope, Gait gait, double workCapacity01)
        {
            double speed = SpeedMs(slope, gait, workCapacity01);
            return speed <= 0.0 ? double.PositiveInfinity : metres / speed / 3600.0;
        }
    }
}
