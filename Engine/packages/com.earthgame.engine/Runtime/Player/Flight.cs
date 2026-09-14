using System;

namespace EarthGame.Engine
{
    /// <summary>
    /// A developer's flight (M1.5e, the owner's "flight i can use in dev"; v1's fly mode and its numbers): the body goes
    /// where the view points, rising and sinking as asked, its velocity eased rather than jerked, with every rule of the
    /// mover set aside and, given no world, the ground among them — it is passed through as the air is (CANON ruling 25,
    /// 2026-09-12: "dev flight should be toggleable noclip"). Given a world (M1.D, ruling 30's noclip switch off), the body
    /// is swept through it as the walk is, stopped by the ground and the trunks and sliding along them, and never left under
    /// the ground. Only a development game lets a founder fly (<c>-eg-dev</c>), and only a development server's rules let the
    /// flight stand (<c>MovementRules.AllowFlight</c>).
    /// </summary>
    public static class Flight
    {
        /// <summary>How fast a flying founder goes, m/s (v1's).</summary>
        public const double SpeedMs = 25.0;

        /// <summary>How many times faster running (v1's).</summary>
        public const double RunFactor = 6.0;

        /// <summary>How quickly the velocity eases toward what is asked for, per second (v1's).</summary>
        public const double EasePerSecond = 4.0;

        /// <summary>
        /// How far up the normal of what stopped a flight must point for it to be ground the flight skims, rather than a
        /// face or a trunk it slides along (M1.D): cos 60°, the steepest ground the setting-on-the-ground below reaches over.
        /// </summary>
        public const double GroundNormalUp = 0.5;

        /// <summary>
        /// One step of flight. The stick is in the founder's own frame (right, forward), and forward is where the view points:
        /// yaw clockwise from north and pitch positive looking down, as the camera's. Rise and sink are held buttons. With no
        /// <paramref name="world"/> nothing stands in the way: a flying body goes through the ground. With one, the body is
        /// swept through it and stopped by what the walk is stopped by, and feet that end under the ground are set on it.
        /// Either way it is never grounded, wading or swimming.
        /// </summary>
        public static MoverState Step(MoverState s, double right, double forward, bool rise, bool sink, bool run,
                                      double yawDeg, double pitchDeg, double dt, IWorldCollision world = null, MoverConfig cfg = null)
        {
            if (!(dt > 0.0) || !s.IsFinite) return s;
            if (double.IsNaN(right) || double.IsInfinity(right)) right = 0.0;
            if (double.IsNaN(forward) || double.IsInfinity(forward)) forward = 0.0;
            double yaw = yawDeg * GeoMath.DegToRad, pitch = pitchDeg * GeoMath.DegToRad;
            double sy = Math.Sin(yaw), cy = Math.Cos(yaw), sp = Math.Sin(pitch), cp = Math.Cos(pitch);
            // Forward along the view, right level across it, and up straight up.
            double wishEast = sy * cp * forward + cy * right;
            double wishUp = -sp * forward + (rise ? 1.0 : 0.0) - (sink ? 1.0 : 0.0);
            double wishNorth = cy * cp * forward - sy * right;
            double length = Math.Sqrt(wishEast * wishEast + wishUp * wishUp + wishNorth * wishNorth);
            if (length > 1.0)
            {
                wishEast /= length;
                wishUp /= length;
                wishNorth /= length;
            }
            double speed = SpeedMs * (run ? RunFactor : 1.0);
            double k = Math.Min(1.0, EasePerSecond * dt);
            s.VelEast += (wishEast * speed - s.VelEast) * k;
            s.VelUp += (wishUp * speed - s.VelUp) * k;
            s.VelNorth += (wishNorth * speed - s.VelNorth) * k;
            if (world == null)
            {
                s.East += s.VelEast * dt;
                s.Up += s.VelUp * dt;
                s.North += s.VelNorth * dt;
            }
            else
            {
                cfg = cfg ?? MoverConfig.Default;
                Double3 feet = s.Feet, vel = s.Velocity, delta = vel * dt;
                double stepLength = delta.Length;
                // Swept and slid as the walk is (Mover, step 4), without the step-up: a flight climbs by rising.
                for (int i = 0; i < Mover.MaxSlideIterations && delta.SqrLength > Mover.Epsilon * Mover.Epsilon; i++)
                {
                    if (!world.SweepCapsule(feet, cfg.CapsuleRadius, cfg.StandingHeight, delta, out double fraction, out Double3 normal))
                    {
                        feet = feet + delta;
                        break;
                    }
                    feet = feet + delta * fraction;
                    Double3 remaining = delta * (1.0 - fraction);
                    if (normal.Y > GroundNormalUp)
                    {
                        // Ground under the flight: it skims, keeping its way across and losing its fall, as the walk keeps its
                        // way and is set on the ground. Slid along the normal instead, a flight pointed steeply into a rise
                        // was sent back down it (the first run of the test below).
                        remaining = new Double3(remaining.X, Math.Max(0.0, remaining.Y), remaining.Z);
                        vel = new Double3(vel.X, Math.Max(0.0, vel.Y), vel.Z);
                    }
                    else
                    {
                        // A face or a trunk: slid along, as the walk slides.
                        double into = Double3.Dot(remaining, normal);
                        if (into < 0.0) remaining = remaining - normal * into;
                        double velInto = Double3.Dot(vel, normal);
                        if (velInto < 0.0) vel = vel - normal * velInto;
                    }
                    delta = remaining;
                }
                // A rise gentler than a face is not what the sweep stops at (the walk steps up onto it): feet that ended under
                // the ground within twice the step's length are set on it, so a flight skims a hill rather than entering it.
                if (world.ProbeGround(feet, cfg.CapsuleRadius, Math.Max(cfg.StepHeight, 2.0 * stepLength), 0.0, out double ground, out _) && feet.Y < ground)
                    feet = new Double3(feet.X, ground, feet.Z);
                s.East = feet.X;
                s.Up = feet.Y;
                s.North = feet.Z;
                s.VelEast = vel.X;
                s.VelUp = vel.Y;
                s.VelNorth = vel.Z;
            }
            s.Grounded = false;
            s.Wading = false;
            s.Swimming = false;
            s.Stance = Stance.Standing;
            return s;
        }
    }
}
