using System;

namespace EarthGame.ClientCore
{
    /// <summary>
    /// How the hand moves (M1.5c; v1's <c>HandSway</c> and <c>HeldItemView</c>, ported): the share of the head's pitch a
    /// shoulder passes on, the walk in the hand, the lag that is what weight feels like, and the swing of a use. Numbers
    /// only; the client turns them into where the thing in hand sits in front of the camera.
    /// </summary>
    public sealed class HandMotion
    {
        /// <summary>
        /// How much of the camera's pitch reaches the hand. An arm hangs off a shoulder, not off the eyes: parented
        /// straight to the camera a held thing takes every degree, so looking at the boots drives it into the ground (v1).
        /// </summary>
        public const double PitchFollow = 0.35;

        /// <summary>How long a use's swing takes, s (v1's strike).</summary>
        public const double StrikeSeconds = 0.28;

        private double _bobPhase;
        private double _strikeLeft;

        /// <summary>
        /// The walk in the hand this frame, m, sideways and down: small, and only while moving. The phase wraps rather than
        /// growing, because a sine of a phase grown over hours of walking goes gritty (v1).
        /// </summary>
        public void Bob(double dt, double speedMs, out double sideM, out double downM)
        {
            double speed = speedMs > 0.0 ? speedMs : 0.0;
            if (dt > 0.0) _bobPhase = (_bobPhase + dt * (4.0 + speed * 1.6)) % (2.0 * Math.PI);
            double amount = Math.Min(speed, 4.0) * 0.006;
            sideM = Math.Sin(_bobPhase) * amount;
            downM = Math.Abs(Math.Cos(_bobPhase)) * amount;
        }

        /// <summary>How far to turn the hand back against the head's pitch, degrees: all of it but the shoulder's share.</summary>
        public static double CounterPitchDeg(double cameraPitchDeg) => -(1.0 - PitchFollow) * cameraPitchDeg;

        /// <summary>
        /// How much of the way to where it should be a hand gets in a frame: thirty grams of bark tracks the head, a heavy
        /// stone trails behind it and settles late, which is most of what carrying one is like (v1).
        /// </summary>
        public static double Follow(double dt, double massKg)
        {
            double smoothSeconds = 0.035 + Math.Min(Math.Max(massKg, 0.0), 6.0) * 0.012;
            return 1.0 - Math.Exp(-dt / Math.Max(0.01, smoothSeconds));
        }

        /// <summary>A use: the hand swings through and back.</summary>
        public void Strike() => _strikeLeft = StrikeSeconds;

        /// <summary>
        /// How far through its swing the hand is this frame, 0 out to 1 and back to 0: a quick out and a slower return, so
        /// it lands rather than wobbles (v1); 0 when no swing is playing.
        /// </summary>
        public double Swing(double dt)
        {
            if (_strikeLeft <= 0.0) return 0.0;
            _strikeLeft = Math.Max(0.0, _strikeLeft - Math.Max(dt, 0.0));
            double t = 1.0 - _strikeLeft / StrikeSeconds;
            return t < 0.35 ? t / 0.35 : 1.0 - (t - 0.35) / 0.65;
        }
    }
}
