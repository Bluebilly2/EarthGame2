using System;

namespace EarthGame.ClientCore
{
    /// <summary>
    /// Looking round without turning (CANON ruling 36, 2026-09-20): "let the alt key allow the camera to pan without moving
    /// the players body. inspired by the same mechanic in rust the survival game." While Alt is held, the mouse's turn goes
    /// to the view and not to the body, so the founder keeps walking the way they face and the moves the server is sent carry
    /// the body's facing; the crosshair follows the view, so a verb acts on what the freed view is looking at. Released,
    /// the view glides home over <see cref="ReturnSeconds"/> rather than snapping.
    ///
    /// <para>Only the turn left and right is freed: the view already pitches up and down without the body, which has no
    /// pitch to keep. Engine-free, so the rule is asserted without a camera.</para>
    /// </summary>
    public sealed class FreeLook
    {
        /// <summary>The furthest the view turns off the body's facing either way, degrees: past it a head does not turn, and the view does not either.</summary>
        public const double MostTurnDeg = 135.0;

        /// <summary>How long the view takes to come home once Alt is let go, s: a glide, not a snap.</summary>
        public const double ReturnSeconds = 0.25;

        /// <summary>How far the view stands off the body's facing, degrees, right positive; zero when not looking round.</summary>
        public double OffsetDeg { get; private set; }

        /// <summary>
        /// Takes this frame's seconds, whether Alt is held and the mouse's turn in degrees, and answers how much of that turn
        /// the body takes. Held, none of it; not held, all of it, while the view's offset glides home at a steady rate that
        /// covers the whole bound in <see cref="ReturnSeconds"/>.
        /// </summary>
        public double Step(double seconds, bool held, double turnDeg)
        {
            if (held)
            {
                OffsetDeg = Math.Max(-MostTurnDeg, Math.Min(MostTurnDeg, OffsetDeg + turnDeg));
                return 0.0;
            }
            if (OffsetDeg != 0.0)
            {
                double back = MostTurnDeg / ReturnSeconds * Math.Max(0.0, seconds);
                OffsetDeg = Math.Abs(OffsetDeg) <= back ? 0.0 : OffsetDeg - Math.Sign(OffsetDeg) * back;
            }
            return turnDeg;
        }
    }
}
