using System;
using System.Globalization;
using EarthGame.Engine;

namespace EarthGame.Server
{
    /// <summary>The tolerances the server allows a client's reported movement, beyond what the mover itself can do.</summary>
    public sealed class MovementRules
    {
        /// <summary>Reported horizontal speed may exceed the mover's ceiling by this factor before it is a violation (timing jitter).</summary>
        public double SpeedTolerance = 1.25;
        /// <summary>
        /// How far from the heightfield grounded feet may be. Generous on purpose: the client's PhysX Terrain
        /// interpolates between posts where the server samples them, and the two differ most on steep ground
        /// (DEBTS.md, the named defect class; N2 budgets the corrections this causes).
        /// </summary>
        public double GroundTolerance = 1.0;
        /// <summary>Fastest vertical rate a report may imply, m/s; a terminal-velocity fall is well inside it.</summary>
        public double MaxVerticalSpeed = 60.0;
        /// <summary>Shortest interval two reports are measured over: a server tick, so two reports in one tick are not infinite speed.</summary>
        public double MinIntervalSeconds = 0.05;
        /// <summary>
        /// The most time a client may bank between reports. A report is measured over its sequence spacing (the
        /// client sends one report per tick interval, so the gap in sequence numbers is the time it moved for),
        /// bounded by the real time the server has seen pass since the reports it accepted: jitter that bunches
        /// two reports into one server tick does not double their speed, and a client that claims time it did
        /// not have runs out of credit. Five seconds covers a held tick of that length; the burst it permits is
        /// five seconds of running, the same average speed as honest play. The first corpus run (2026-09-08)
        /// corrected a legal sprint thirty-one times in forty-five seconds at 100 ms ± 20 ms before this rule.
        /// </summary>
        public double MoveCreditCapSeconds = 5.0;

        /// <summary>
        /// The one mark of a development server, set for every SOLO game and by a host's <c>+server.dev 1</c>: a server that
        /// may grant developer mode. Since M1.E (2026-09-20, CANON ruling 39) it grants it player by player, to whoever
        /// switches it on with F2; a player who has not is held to <see cref="WithoutFlight"/>. With it on, a founder may fly
        /// (M1.5e, the owner's "flight i can use in dev"), so nothing but the region's edge and finite numbers is held on
        /// their moves, and their developer's settings are taken (M1.D). A server without the mark refuses the switch.
        /// </summary>
        public bool AllowFlight;

        /// <summary>These rules with flight taken away: what a development server holds a player to until they switch developer mode on (M1.E).</summary>
        public MovementRules WithoutFlight() => new MovementRules
        {
            SpeedTolerance = SpeedTolerance,
            GroundTolerance = GroundTolerance,
            MaxVerticalSpeed = MaxVerticalSpeed,
            MinIntervalSeconds = MinIntervalSeconds,
            MoveCreditCapSeconds = MoveCreditCapSeconds,
            AllowFlight = false,
        };
    }

    /// <summary>
    /// Whether a client's reported body state was possible from the last one the server accepted. Movement is
    /// client-authoritative and server-validated (ARCHITECTURE §7, §9): the server does not re-run the mover, it
    /// asks whether the report stays inside the region, inside the speed ceiling, and on the ground when it claims
    /// to be. A violation is answered with a Correction to the last accepted state.
    /// </summary>
    public static class MovementValidator
    {
        /// <summary>
        /// What a founder who has just come down from a fall or a slide may still be moving at on their feet (2026-09-23, DEBTS
        /// "A slide's landing is corrected"): the speed the fall allowed at the landing, less what the mover's brake has taken
        /// off since. The corpus's walkers slid 1.8 m off a dune face at the walk's limit, landed faster than a run and were
        /// corrected three times each while they braked.
        /// </summary>
        public struct Landing
        {
            /// <summary>The fastest the body could be crossing the ground as it landed, m/s (<see cref="FallCeiling"/>).</summary>
            public double CeilingMs;
            /// <summary>The report the body landed in.</summary>
            public uint Sequence;

            /// <summary>
            /// What the landing still allows at a later report, m/s: its ceiling less half the brake's work over the reports'
            /// spacing, nothing once braked away. Half, because the body brakes at the full rate and its reports come unevenly
            /// (0.04 and 0.06 s apart at 50 Hz steps, a fifth over the spacing they are judged by): at the full rate the test's
            /// founder, landing at 9.8 m/s against a fall's 9.9, was refused 0.6 s into the braking by a hundredth.
            /// </summary>
            public double AllowanceAt(uint sequence, double stepSeconds, MoverConfig mover)
            {
                if (!(CeilingMs > 0.0) || sequence < Sequence) return 0.0;
                double left = CeilingMs - 0.5 * mover.BrakeMs2 * (sequence - Sequence) * stepSeconds;
                return left > 0.0 ? left : 0.0;
            }
        }

        /// <summary>
        /// The fastest a body off its feet can cross the ground, m/s: a run with all the height lost since it last stood
        /// (M1.5f), the run taken on the fastest ground (BF.4 stage two), since a body leaving rock at a run carries rock's pace.
        /// </summary>
        public static double FallCeiling(double stoodUp, double up, MoverConfig mover, double workCapacity01 = 1.0)
        {
            double run = mover.MaxHorizontalSpeedAt(workCapacity01, Locomotion.FastestGround);
            double drop = double.IsNaN(stoodUp) ? 0.0 : Math.Max(0.0, stoodUp - up);
            return Math.Sqrt(run * run + 2.0 * mover.Gravity * drop);
        }

        /// <summary>Null when the report is acceptable; otherwise the reason, in words a log can carry.</summary>
        /// <param name="stoodUp">The height of the last body this player reported standing on the ground; NaN before any.</param>
        /// <param name="landedMs">What a recent landing still allows a body on its feet, m/s (<see cref="Landing.AllowanceAt"/>); zero for none.</param>
        /// <param name="underfoot">The ground the report is judged on (BF.4 stage two): the faster of those it leaves and reaches.</param>
        public static string Check(in MoverState last, bool hasLast, in MoverState reported, double intervalSeconds,
                                   IHeightSource ground, double halfExtentM, MoverConfig mover, MovementRules rules, double stoodUp = double.NaN,
                                   double workCapacity01 = 1.0, double landedMs = 0.0, GroundType underfoot = Locomotion.TableGround)
        {
            if (!reported.IsFinite) return "non-finite numbers in the report";
            if (Math.Abs(reported.East) > halfExtentM || Math.Abs(reported.North) > halfExtentM)
                return "outside the region (" + F(reported.East) + ", " + F(reported.North) + " m from the centre; the edge is " + F(halfExtentM) + ")";
            if (rules.AllowFlight) return null;

            if (hasLast)
            {
                double dt = Math.Max(intervalSeconds, rules.MinIntervalSeconds);
                double de = reported.East - last.East;
                double dn = reported.North - last.North;
                double horizontal = Math.Sqrt(de * de + dn * dn) / dt;
                // A report that lands, the last one off its feet and this one on them, is the end of the fall and judged as it.
                bool landing = !last.Grounded && reported.Grounded;
                double ceiling = HorizontalCeiling(reported, stoodUp, mover, workCapacity01, landedMs, landing, underfoot) * rules.SpeedTolerance;
                if (horizontal > ceiling)
                    return "speed " + F(horizontal) + " m/s exceeds the ceiling " + F(ceiling);
                double vertical = Math.Abs(reported.Up - last.Up) / dt;
                if (vertical > rules.MaxVerticalSpeed)
                    return "vertical speed " + F(vertical) + " m/s exceeds " + F(rules.MaxVerticalSpeed);
            }

            if (ground != null)
            {
                double g = ground.HeightAt(reported.East, reported.North);
                if (reported.Grounded && Math.Abs(reported.Up - g) > rules.GroundTolerance)
                    return "grounded " + F(reported.Up - g) + " m from the ground";
                if (reported.Up < g - rules.GroundTolerance)
                    return "below the ground by " + F(g - reported.Up) + " m";
            }
            return null;
        }

        /// <summary>
        /// The fastest a body can be crossing the ground, m/s (M1.5f). On its feet, the mover's run. Off them, falling or
        /// sliding down a face too steep to stand on, nothing drives it but gravity, so it is no faster than a run together
        /// with all the height it has lost since it last stood: v² = run² + 2 g drop. The bug hunt of 2026-09-13 found a
        /// slide down a steep face outrunning a run's ceiling and corrected, as a cheat is, all the way down. Height gained
        /// gives nothing, and the allowance is measured from where the founder stood rather than from their last report, so
        /// it cannot grow report by report.
        /// </summary>
        /// <param name="landedMs">What a recent landing still allows a body on its feet, m/s (<see cref="Landing"/>): a run is the least a founder on their feet is allowed, and a founder braking from a fall is allowed the fall's speed less the brake's work.</param>
        /// <param name="landing">Whether this report is the one that lands: judged as the fall it ends.</param>
        /// <param name="underfoot">The ground a body on its feet runs on (BF.4 stage two); a fall's run is the fastest ground's.</param>
        public static double HorizontalCeiling(in MoverState reported, double stoodUp, MoverConfig mover, double workCapacity01 = 1.0,
                                               double landedMs = 0.0, bool landing = false, GroundType underfoot = Locomotion.TableGround)
        {
            // The run is the body's (FP.1): a thirsty founder is held to what their capacity allows, as their own mover is,
            // on the ground under them (BF.4 stage two).
            double run = mover.MaxHorizontalSpeedAt(workCapacity01, underfoot);
            if (double.IsNaN(stoodUp)) return Math.Max(run, landedMs);
            if (reported.Grounded && !landing) return Math.Max(run, landedMs);
            return FallCeiling(stoodUp, reported.Up, mover, workCapacity01);
        }

        private static string F(double v) => v.ToString("0.00", CultureInfo.InvariantCulture);
    }
}
