using System;

namespace EarthGame.Engine
{
    /// <summary>
    /// A developer's flight (M1.5e, the owner's "flight i can use in dev"; v1's fly mode and its numbers): the body goes
    /// where the view points, rising and sinking as asked, its velocity eased rather than jerked, with every rule of the
    /// mover set aside and the ground among them — it is passed through as the air is (CANON ruling 25, 2026-09-12:
    /// "dev flight should be toggleable noclip"). Only a development game lets a founder fly (<c>-eg-dev</c>), and only a
    /// development server's rules let the flight stand (<c>MovementRules.AllowFlight</c>).
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
        /// One step of flight. The stick is in the founder's own frame (right, forward), and forward is where the view points:
        /// yaw clockwise from north and pitch positive looking down, as the camera's. Rise and sink are held buttons. Nothing
        /// stands in the way: a flying body goes through the ground, and is never grounded, wading or swimming.
        /// </summary>
        public static MoverState Step(MoverState s, double right, double forward, bool rise, bool sink, bool run,
                                      double yawDeg, double pitchDeg, double dt)
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
            s.East += s.VelEast * dt;
            s.Up += s.VelUp * dt;
            s.North += s.VelNorth * dt;
            s.Grounded = false;
            s.Wading = false;
            s.Swimming = false;
            s.Stance = Stance.Standing;
            return s;
        }
    }
}
