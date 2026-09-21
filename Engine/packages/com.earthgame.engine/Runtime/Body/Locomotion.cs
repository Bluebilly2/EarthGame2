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
    /// <para>Published functions do the work here, and none was invented for this game. Tobler's hiking function says
    /// how fast a journey goes over a slope, an hour's average with the pauses and the route-finding in it; the
    /// Pandolf equation says what moving costs carrying something; and since M1.5h (CANON ruling 34, 2026-09-21) the
    /// walker's own speed on a slope, second by second, is a table of what people are measured doing when they walk
    /// on slopes, because Tobler used as a stepping pace walked a founder down a dune slower than a stroll. Ported
    /// from v1 (Assets/EarthGame/Sim/Body/Locomotion.cs) with Tobler and Pandolf untouched; the gait multipliers
    /// are the code's, not the v1 contract's, and that disagreement is a recorded debt for the owner's hands.</para>
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

        /// <summary>Tobler's journey speed on this slope, m/s: an hour's average over country, pauses and route-finding included. Slope is rise over run, positive uphill.</summary>
        public static double JourneySpeedMs(double slope)
            => ToblerBaseMs * Math.Exp(-ToblerExponent * Math.Abs(slope - ToblerPeakSlope));

        // ---- The walker's own speed (M1.5h, CANON ruling 34) ----
        /// <summary>The tangent of 24° and of 35°: the steepest gradient people were measured walking, and dry sand's angle of repose.</summary>
        private const double Tan24 = 0.44523, Tan35 = 0.70021;
        /// <summary>
        /// The slopes (rise over run, positive uphill) at which the walker's speed is known, and the speeds, m/s, as people
        /// are measured walking them: the hiking speeds a treadmill study set for its grades (Applied Sciences 14 (2024)
        /// 4383: 5.0 km/h on the level and at −10 %, 3.5 km/h at +10 % and at −20 %, 2.5 km/h at +20 %); a gentle descent
        /// walked no slower than the flat (Sun, Walters, Svensson and Lloyd 1996, Ergonomics 39:677, 2 400 pedestrians on a
        /// ramp of up to 9°); beyond ±20 % the speed falling as the step shortens (Kawamura, Tokuhiro and Takechi 1991,
        /// Acta Med Okayama 45:179, 3° to 12°: slower both ways at 12°, the step shorter the steeper the descent) to the
        /// pace people were measured holding on a 24° descent (0.69 m/s, the slowest trial Minetti, Moia, Roi, Susta and
        /// Ferretti 2002, J Appl Physiol 93:1039, walked at every gradient to ±0.45) and to a careful pace at 35°, past
        /// which nothing loose stands and the mover slides. The 24° ascent and both 35° points are this slice's own
        /// continuation of the measured trend, not measurements, and are marked for the owner's hands (DEBTS, ruling 12).
        /// Between the points the speed is a straight line; past the ends it is the end's.
        /// </summary>
        private static readonly double[] WalkSlopes = { -Tan35, -Tan24, -0.20, -0.10, 0.0, 0.10, 0.20, Tan24, Tan35 };
        private static readonly double[] WalkSpeeds = { 0.40, 0.69, 0.97, 1.39, 1.39, 0.97, 0.69, 0.50, 0.35 };
        /// <summary>A slope on which the walker is fastest: the level and the gentle descent are one plateau in the table, and this is on it.</summary>
        public const double FastestWalkSlope = -0.05;
        /// <summary>How fast a person walks on this slope, m/s: the walker's own pace, not the journey's. Slope is rise over run, positive uphill.</summary>
        public static double WalkingSpeedMs(double slope)
        {
            if (double.IsNaN(slope)) slope = 0.0;
            if (slope <= WalkSlopes[0]) return WalkSpeeds[0];
            int last = WalkSlopes.Length - 1;
            if (slope >= WalkSlopes[last]) return WalkSpeeds[last];
            int i = 1;
            while (WalkSlopes[i] < slope) i++;
            double t = (slope - WalkSlopes[i - 1]) / (WalkSlopes[i] - WalkSlopes[i - 1]);
            return WalkSpeeds[i - 1] + (WalkSpeeds[i] - WalkSpeeds[i - 1]) * t;
        }

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
            // The table's numbers are stepping speeds already: no correction from an hour-average, and no floor, since the
            // table ends at the repose with a pace of its own and past it the mover slides (M1.5h; until then Tobler's
            // hour-average was scaled by 1.3 and floored at 0.45 m/s, which is what crawled a founder down a dune).
            return WalkingSpeedMs(slope) * multiplier * capacity;
        }

        // ---- Swimming (Sugiyama & Katamoto 1992) ----

        /// <summary>
        /// The breaststroke's steady pace, m/s: the fastest of the paces Sugiyama and Katamoto held six college swimmers to
        /// in a flume for it (0.3, 0.5 and 0.7 m/s; Annals of Physiological Anthropology 11: 635–640, 1992). The stroke a
        /// founder swims unless they push.
        /// </summary>
        public const double BreaststrokeMs = 0.7;

        /// <summary>
        /// The front crawl's, m/s: the fastest of theirs for the crawl (0.3 to 0.9 m/s), whose oxygen cost rose less
        /// steeply than the breaststroke's past 0.49 m/s. The stroke a founder pushing (sprinting) swims.
        /// </summary>
        public const double FrontCrawlMs = 0.9;

        /// <summary>How fast the founder swims, m/s: the breaststroke, or the crawl when pushed, under the body's state as walking is.</summary>
        public static double SwimmingSpeedMs(bool crawl, double workCapacity01)
        {
            double capacity = 0.45 + 0.55 * SimMath.Clamp01(workCapacity01);
            return (crawl ? FrontCrawlMs : BreaststrokeMs) * capacity;
        }

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
