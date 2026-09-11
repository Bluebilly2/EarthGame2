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
        /// A development server's: a founder may fly (M1.5e, the owner's "flight i can use in dev"), so nothing but the
        /// region's edge and finite numbers is held. A SOLO game started with <c>-eg-dev</c> runs its server so, and a host
        /// with <c>+server.dev 1</c>; every other server corrects a flying founder back.
        /// </summary>
        public bool AllowFlight;
    }

    /// <summary>
    /// Whether a client's reported body state was possible from the last one the server accepted. Movement is
    /// client-authoritative and server-validated (ARCHITECTURE §7, §9): the server does not re-run the mover, it
    /// asks whether the report stays inside the region, inside the speed ceiling, and on the ground when it claims
    /// to be. A violation is answered with a Correction to the last accepted state.
    /// </summary>
    public static class MovementValidator
    {
        /// <summary>Null when the report is acceptable; otherwise the reason, in words a log can carry.</summary>
        public static string Check(in MoverState last, bool hasLast, in MoverState reported, double intervalSeconds,
                                   Heightfield ground, double halfExtentM, MoverConfig mover, MovementRules rules)
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
                double ceiling = mover.MaxHorizontalSpeed * rules.SpeedTolerance;
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

        private static string F(double v) => v.ToString("0.00", CultureInfo.InvariantCulture);
    }
}
