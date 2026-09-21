using System;

namespace EarthGame.ClientCore
{
    /// <summary>
    /// Looking round without turning (CANON ruling 36, 2026-09-20): "let the alt key allow the camera to pan without moving
    /// the players body. inspired by the same mechanic in rust the survival game." While Alt is held, the mouse's turn and
    /// tilt go to the view and not to the body, so the founder keeps walking the way they face and the moves the server is
    /// sent carry the body's facing and pitch; the crosshair follows the view, so a verb acts on what the freed view is
    /// looking at. Released, the view glides home over <see cref="ReturnSeconds"/> rather than snapping, on both axes, by
    /// William's word of 2026-09-21 ("the x and y should both snap back, not just the x"); until then only the turn was
    /// freed and the tilt stayed where the look had left it.
    ///
    /// <para>Engine-free, so the rule is asserted without a camera.</para>
    /// </summary>
    public sealed class FreeLook
    {
        /// <summary>The furthest the view turns off the body's facing either way, degrees: past it a head does not turn, and the view does not either.</summary>
        public const double MostTurnDeg = 135.0;

        /// <summary>The furthest the view tilts off the body's pitch either way, degrees: the view's own limit, straight up or down.</summary>
        public const double MostTiltDeg = 89.0;

        /// <summary>How long the view takes to come home once Alt is let go, s: a glide, not a snap, on both axes.</summary>
        public const double ReturnSeconds = 0.25;

        /// <summary>How far the view stands off the body's facing, degrees, right positive; zero when not looking round.</summary>
        public double OffsetDeg { get; private set; }

        /// <summary>How far the view stands off the body's pitch, degrees, down positive as the body's pitch is; zero when not looking round.</summary>
        public double PitchOffsetDeg { get; private set; }

        /// <summary>What the body takes of a frame's look: its turn and its tilt in degrees.</summary>
        public readonly struct BodyShare
        {
            public readonly double TurnDeg;
            public readonly double PitchDeg;
            public BodyShare(double turnDeg, double pitchDeg) { TurnDeg = turnDeg; PitchDeg = pitchDeg; }
        }

        /// <summary>
        /// Takes this frame's seconds, whether Alt is held and the mouse's turn and tilt in degrees, and answers how much of
        /// each the body takes. Held, none; not held, all, while the view's offsets glide home at steady rates that cover
        /// each whole bound in <see cref="ReturnSeconds"/>.
        /// </summary>
        public BodyShare Step(double seconds, bool held, double turnDeg, double pitchDeg)
        {
            if (held)
            {
                OffsetDeg = Math.Max(-MostTurnDeg, Math.Min(MostTurnDeg, OffsetDeg + turnDeg));
                PitchOffsetDeg = Math.Max(-MostTiltDeg, Math.Min(MostTiltDeg, PitchOffsetDeg + pitchDeg));
                return new BodyShare(0.0, 0.0);
            }
            OffsetDeg = Home(OffsetDeg, MostTurnDeg, seconds);
            PitchOffsetDeg = Home(PitchOffsetDeg, MostTiltDeg, seconds);
            return new BodyShare(turnDeg, pitchDeg);
        }

        /// <summary>An offset a frame nearer home, at the steady rate that covers its whole bound in <see cref="ReturnSeconds"/>; home when within a frame of it.</summary>
        private static double Home(double offset, double bound, double seconds)
        {
            if (offset == 0.0) return 0.0;
            double back = bound / ReturnSeconds * Math.Max(0.0, seconds);
            return Math.Abs(offset) <= back ? 0.0 : offset - Math.Sign(offset) * back;
        }
    }
}
