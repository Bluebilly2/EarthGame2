using System;
using EarthGame.Engine;

namespace EarthGame.ClientCore
{
    /// <summary>One footfall, or one stroke swum, as the stride gives it and the client sounds it (M1.5c, M1.5e).</summary>
    public struct Footfall
    {
        /// <summary>How many feet have fallen since the stride began, this one included; a stroke leaves it as it was.</summary>
        public int Count;
        /// <summary>Which foot, or which arm: they alternate.</summary>
        public bool Left;
        /// <summary>A landing from the air rather than a step: on the ground a footfall, into the water a splash.</summary>
        public bool Landing;
        /// <summary>A stroke swum rather than a foot set down (M1.5e).</summary>
        public bool Stroke;
        /// <summary>Which of a ground's <see cref="FootstepSynth.Variants"/> sounds to play, or of the strokes'.</summary>
        public int Variant;
        /// <summary>How loud, 0–1 (<see cref="Loudness.Footfall01"/>, <see cref="Loudness.Stroke01"/>).</summary>
        public double Loudness01;
        /// <summary>How much faster than it was made the sound is played: the feet sit a little differently, and no two steps are quite the same (v1).</summary>
        public double Pitch;
    }

    /// <summary>
    /// Footfalls from the distance walked rather than from a timer (M1.5c; v1's <c>Footsteps</c>, ported). A step is
    /// <see cref="BaseStepM"/> and <see cref="StepPerSpeed"/> more for each metre a second, so the steps come closer
    /// together up a hill because Tobler has already slowed the founder, and lengthen at a run. Each footfall drops the
    /// eye a little and lets it spring back, and a sway rides across the stride: v1 found that without them walking felt
    /// like sliding a camera over a heightfield. Distance is walked only on the ground, so a jump is silent until it
    /// lands, and a landing is a footfall. In the water (M1.5e) the distance is swum, a stroke each <see cref="StrokeM"/>,
    /// and a fall into it splashes. Nothing here reads a clock; the client passes each frame's seconds.
    /// </summary>
    public sealed class Stride
    {
        /// <summary>A step's length standing still, m: the constant part of the stride (v1).</summary>
        public const double BaseStepM = 0.55;

        /// <summary>How much longer a step is for each metre a second (v1).</summary>
        public const double StepPerSpeed = 0.22;

        /// <summary>How far a footfall drops the eye at a walk, m: small on purpose (v1).</summary>
        public const double DipM = 0.022;

        /// <summary>How far the eye sways to either side over a stride, m (v1).</summary>
        public const double SwayM = 0.014;

        /// <summary>How fast the dip springs back, per second: v1's rate, here the same at any frame rate.</summary>
        public const double RecoveryPerSecond = 12.0;

        /// <summary>Below this no walking is happening, m/s. A footfall's loudness keys off the same number: v1 had one of it, and so does this.</summary>
        public const double WalkingMs = 0.25;

        /// <summary>
        /// How long in the air makes the next touch of the ground a landing, s: longer than feet leave uneven ground in a
        /// stride, shorter than any jump.
        /// </summary>
        public const double LandingAfterS = 0.2;

        /// <summary>
        /// How far a stroke carries a swimmer, m (M1.5e): chosen, not measured, between a breaststroke's whole stroke and one
        /// arm's pull of the crawl, so that at the strokes' paces one comes every second and a quarter to second and a half.
        /// </summary>
        public const double StrokeM = 1.1;

        /// <summary>How far a stroke lowers the eye, m (M1.5e): the head settles back towards the water after the breath.</summary>
        public const double StrokeDipM = 0.03;

        /// <summary>Below this no swimming is happening, m/s (M1.5e): treading water makes no stroke.</summary>
        public const double SwimmingMs = 0.15;

        /// <summary>The most a footfall's dip grows with speed, as a share of <see cref="DipM"/> (v1); a landing comes down this hard.</summary>
        private const double MostDip = 1.8;

        private double _sinceStepM;
        private double _phase;
        private double _speedMs;
        private double _airborneS;
        private bool _left;

        /// <summary>How many feet have fallen.</summary>
        public int Count { get; private set; }

        /// <summary>How many strokes have been swum (M1.5e).</summary>
        public int Strokes { get; private set; }

        /// <summary>How far below where it would be the eye is now, m.</summary>
        public double DipNowM { get; private set; }

        /// <summary>How far to the right of where it would be the eye is now, m; to the left when negative. None standing still.</summary>
        public double SwayNowM => Math.Sin(_phase * 2.0 * Math.PI) * SwayM * Math.Min(1.0, _speedMs / 2.0);

        /// <summary>A frame on land: <see cref="Advance(double, double, bool, bool, out Footfall)"/> out of the water.</summary>
        public bool Advance(double dt, double speedMs, bool grounded, out Footfall footfall) => Advance(dt, speedMs, grounded, false, out footfall);

        /// <summary>
        /// A frame of the founder's motion: the seconds it lasted, how fast they went across the ground or through the water,
        /// m/s, whether their feet were on the ground, and whether they were swimming (M1.5e). True, with the footfall, when a
        /// foot fell in it or a stroke was swum.
        /// </summary>
        public bool Advance(double dt, double speedMs, bool grounded, bool swimming, out Footfall footfall)
        {
            footfall = default;
            if (!(dt > 0.0)) return false;
            if (!(speedMs > 0.0)) speedMs = 0.0;
            DipNowM *= Math.Exp(-RecoveryPerSecond * dt);
            if (swimming)
            {
                // A fall into the water is a landing, and splashes; then a stroke for each StrokeM swum. The sway is the
                // stride's, and a swimmer has none.
                bool splashed = _airborneS >= LandingAfterS;
                _airborneS = 0.0;
                _speedMs = 0.0;
                if (splashed)
                {
                    _sinceStepM = 0.0;
                    footfall = Fall(speedMs, true);
                    return true;
                }
                if (!(speedMs > SwimmingMs)) return false;
                _sinceStepM += speedMs * dt;
                if (_sinceStepM < StrokeM) return false;
                _sinceStepM = (_sinceStepM - StrokeM) % StrokeM;
                footfall = Pull(speedMs);
                return true;
            }
            if (!grounded)
            {
                _airborneS += dt;
                _speedMs = 0.0;
                return false;
            }
            bool landed = _airborneS >= LandingAfterS;
            _airborneS = 0.0;
            _speedMs = speedMs;
            if (landed)
            {
                // The stride starts again from the foot that landed.
                _sinceStepM = 0.0;
                footfall = Fall(speedMs, true);
                return true;
            }
            if (!(speedMs > WalkingMs))
            {
                // Standing, a half-taken step is let go of slowly (v1).
                _sinceStepM = Math.Max(0.0, _sinceStepM - dt * 0.5);
                return false;
            }
            double stepM = BaseStepM + StepPerSpeed * speedMs;
            _sinceStepM += speedMs * dt;
            _phase = (_phase + speedMs * dt / (2.0 * stepM)) % 1.0;
            if (_sinceStepM < stepM) return false;
            // One foot a frame: a frame long enough to hold several steps drops the rest rather than sounding a burst.
            _sinceStepM = (_sinceStepM - stepM) % stepM;
            footfall = Fall(speedMs, false);
            return true;
        }

        private Footfall Fall(double speedMs, bool landing)
        {
            Count++;
            _left = !_left;
            DipNowM = DipM * (landing ? MostDip : Math.Max(0.5, Math.Min(MostDip, speedMs / 1.8)));
            double jitter = (StandLayout.Mix((ulong)Count) >> 11) * (1.0 / 9007199254740992.0);
            return new Footfall
            {
                Count = Count,
                Left = _left,
                Landing = landing,
                // A ground's sounds turned over so that the same one never falls twice running (v1).
                Variant = (Count + (_left ? 0 : 2)) % FootstepSynth.Variants,
                Loudness01 = landing ? Loudness.FootfallCeiling01 : Loudness.Footfall01(speedMs),
                Pitch = (_left ? 0.97 : 1.04) * (0.97 + 0.06 * jitter),
            };
        }

        /// <summary>A stroke (M1.5e): the arms alternate as the feet do, and the same stroke never sounds twice running.</summary>
        private Footfall Pull(double speedMs)
        {
            Strokes++;
            _left = !_left;
            DipNowM = StrokeDipM;
            double jitter = (StandLayout.Mix(0x57A0UL + (ulong)Strokes) >> 11) * (1.0 / 9007199254740992.0);
            return new Footfall
            {
                Count = Count,
                Left = _left,
                Stroke = true,
                Variant = Strokes % FootstepSynth.Variants,
                Loudness01 = Loudness.Stroke01(speedMs),
                Pitch = 0.95 + 0.1 * jitter,
            };
        }
    }
}
