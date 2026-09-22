using System;
using EarthGame.Engine;

namespace EarthGame.ClientCore
{
    /// <summary>
    /// The founder's footsteps, a thing's landing and a swimming stroke, made from arithmetic rather than recorded
    /// (M1.5c; v1's <c>ProceduralAudio</c>). Since M1.5j a footstep is an impact and the ground's own answer to it: the
    /// heel comes down and the forefoot follows it, and each strike is answered three ways by the ground's numbers — a
    /// body (the damped low thump of the foot's weight), modes (damped tones at a hard ground's own pitches: rock's knock
    /// and ring) and grains (many small impacts scattered after the strike, dense and dull on sand, fewer and crisper on
    /// litter, sparse loud snaps on heath's twigs). Grass is a swish of blades brushed aside; a wading step is a splash,
    /// a wash and bubbles that chirp. M1.5c's one burst of filtered noise under a falling envelope, which William heard on
    /// every ground as "a static sounding noise" (ruling 37, 2026-09-20), remains only for the strokes and the landings.
    /// Samples only; the client makes them into its engine's clips.
    /// </summary>
    public static class FootstepSynth
    {
        public const int SampleRate = 44100;

        /// <summary>How many of each footstep there are: the same step twice in a row is what gives a game away fastest (v1).</summary>
        public const int Variants = 4;

        /// <summary>The band every step's peak is held inside (M1.5j promise 4): a ground is as loud as its own number says, inside these.</summary>
        public const float QuietestPeak = 0.45f;
        public const float LoudestPeak = 0.92f;

        /// <summary>The lowest pitch a rock step rings at, Hz: a stone under a boot, not a slab. The ring test reads it from here.</summary>
        public const double RockRingHz = 1100.0;

        /// <summary>How a ground answers a strike: the numbers a footing's step is made from, each a weight unless it says otherwise.</summary>
        private struct Ground
        {
            /// <summary>The clip's length, s.</summary>
            public double Seconds;
            /// <summary>The thump of the foot's weight: its pitch, Hz, its weight and how fast it dies, per second.</summary>
            public double BodyHz, Body, BodyDecay;
            /// <summary>The strike's own click: its weight, how bright it is (0 dull to 1 sharp) and how fast it dies, per second.</summary>
            public double Knock, KnockBright, KnockDecay;
            /// <summary>A hard ground's ring: its weight, its lowest pitch, Hz, and how fast it dies, per second.</summary>
            public double Modes, ModeHz, ModeDecay;
            /// <summary>Small impacts after the strike: how many, their size, their brightness, and the time they thin out over, s.</summary>
            public int Grains;
            public double GrainSize, GrainBright, GrainSpreadS;
            /// <summary>Twigs snapping under the whole step, from heel to settling: how many, how far apart, s, and how loud.</summary>
            public int Snaps;
            public double SnapGapS, Snap;
            /// <summary>Blades brushed aside: its weight, how long it takes to swell, s, and how fast it dies, per second.</summary>
            public double Swish, SwishAttackS, SwishDecay;
            /// <summary>A wading step: the burst, the wash and its decay per second, and the bubbles, how many and how loud.</summary>
            public double Splash, Wash, WashDecay;
            public int Bubbles;
            public double Bubble;
            /// <summary>Where the clip's peak is put: the ground's own loudness, inside the band.</summary>
            public double Peak;
            public ulong Seed;
        }

        private static Ground Of(FootingSound footing)
        {
            switch (footing)
            {
                case FootingSound.Grass:
                    return new Ground { Seconds = 0.28, BodyHz = 90, Body = 0.45, BodyDecay = 30, Knock = 0.15, KnockBright = 0.3, KnockDecay = 1200,
                                        Swish = 1.0, SwishAttackS = 0.012, SwishDecay = 24, Peak = 0.50, Seed = 2100 };
                case FootingSound.Litter:
                    return new Ground { Seconds = 0.30, BodyHz = 85, Body = 0.5, BodyDecay = 35, Knock = 0.5, KnockBright = 0.5, KnockDecay = 1200,
                                        Grains = 70, GrainSize = 0.55, GrainBright = 0.55, GrainSpreadS = 0.045, Snaps = 5, SnapGapS = 0.055, Snap = 0.5, Peak = 0.75, Seed = 2200 };
                case FootingSound.Rock:
                    return new Ground { Seconds = 0.24, BodyHz = 110, Body = 0.35, BodyDecay = 45, Knock = 2.5, KnockBright = 0.95, KnockDecay = 600,
                                        Modes = 0.8, ModeHz = RockRingHz, ModeDecay = 70, Grains = 6, GrainSize = 0.2, GrainBright = 0.7, GrainSpreadS = 0.02, Peak = 0.90, Seed = 2300 };
                case FootingSound.Sand:
                    return new Ground { Seconds = 0.28, BodyHz = 80, Body = 0.08, BodyDecay = 70, Knock = 0.15, KnockBright = 0.12, KnockDecay = 1200,
                                        Grains = 520, GrainSize = 0.7, GrainBright = 0.09, GrainSpreadS = 0.045, Peak = 0.55, Seed = 2500 };
                case FootingSound.Water:
                    return new Ground { Seconds = 0.50, Splash = 1.6, Wash = 1.2, WashDecay = 5, Bubbles = 14, Bubble = 0.35, Peak = 0.85, Seed = 2600 };
                case FootingSound.Heath:
                    return new Ground { Seconds = 0.32, BodyHz = 90, Body = 0.15, BodyDecay = 40, Knock = 0.9, KnockBright = 0.7, KnockDecay = 1200,
                                        Grains = 30, GrainSize = 0.08, GrainBright = 0.5, GrainSpreadS = 0.06, Snaps = 24, SnapGapS = 0.013, Snap = 0.9, Peak = 0.85, Seed = 2800 };
                default:
                    // Soil: bare earth, a swamp's floor, and a cover nothing has said.
                    return new Ground { Seconds = 0.26, BodyHz = 95, Body = 1.0, BodyDecay = 32, Knock = 0.6, KnockBright = 0.2, KnockDecay = 1200,
                                        Grains = 40, GrainSize = 0.35, GrainBright = 0.3, GrainSpreadS = 0.03, Peak = 0.60, Seed = 2400 };
            }
        }

        /// <summary>
        /// A footstep on a kind of ground, one of <see cref="Variants"/>: the heel's strike, then the forefoot's 70 to 110 ms
        /// later by the variant, each answered by the ground (M1.5j promises 1 and 2), and the whole put at the ground's own peak.
        /// </summary>
        public static float[] Step(FootingSound footing, int variant)
        {
            int v = ((variant % Variants) + Variants) % Variants;
            Ground g = Of(footing);
            ulong seed = g.Seed + (ulong)v * 17UL;
            int count = Math.Max(16, (int)Math.Round(g.Seconds * SampleRate));
            double[] mix = new double[count];
            Strike(mix, 0, g, 1.0, seed);
            Strike(mix, (int)((0.07 + 0.013 * v) * SampleRate), g, 0.65, seed ^ 0xA5A5UL);
            if (g.Snaps > 0) AddSnaps(mix, g.Snaps, g.SnapGapS, g.Snap, seed ^ 0x33UL);
            return Finish(mix, g.Peak);
        }

        /// <summary>One strike of the foot at a sample, weighted (the forefoot lands lighter than the heel), answered by the ground's numbers.</summary>
        private static void Strike(double[] mix, int at, Ground g, double weight, ulong seed)
        {
            if (g.Body > 0.0) AddTone(mix, at, g.BodyHz, weight * g.Body, g.BodyDecay, 0.0);
            if (g.Knock > 0.0) AddNoise(mix, at, weight * g.Knock, Cut(g.KnockBright), 0.0, 0.0, g.KnockDecay, 8.0 / g.KnockDecay, seed ^ 0x11UL, 1);
            if (g.Modes > 0.0)
            {
                // A hard ground rings at its own pitches: the lowest and two above it, the higher dying sooner.
                AddTone(mix, at, g.ModeHz, weight * g.Modes, g.ModeDecay, 0.0);
                AddTone(mix, at, g.ModeHz * 2.27, weight * g.Modes * 0.7, g.ModeDecay * 1.6, 0.0);
                AddTone(mix, at, g.ModeHz * 4.53, weight * g.Modes * 0.5, g.ModeDecay * 2.4, 0.0);
            }
            if (g.Grains > 0) AddGrains(mix, at, g.Grains, weight * g.GrainSize, g.GrainBright, g.GrainSpreadS, seed ^ 0x22UL);
            if (g.Swish > 0.0) AddNoise(mix, at, weight * g.Swish, 0.35, 0.05, g.SwishAttackS, g.SwishDecay, 0.3, seed ^ 0x44UL, 1);
            if (g.Splash > 0.0)
            {
                AddNoise(mix, at, weight * g.Splash, 0.7, 0.0, 0.0, 350.0, 0.015, seed ^ 0x55UL, 1);
                AddNoise(mix, at, weight * g.Wash, 0.12, 0.0, 0.01, g.WashDecay, 0.5, seed ^ 0x66UL, 1);
                for (int k = 0; k < g.Bubbles; k++)
                {
                    // A bubble is a short tone whose pitch rises as it shrinks: a chirp, scattered through the wash.
                    ulong h = StandLayout.Mix(seed ^ 0x77UL ^ ((ulong)(k + 1) * 0x9E3779B97F4A7C15UL));
                    double off = 0.02 + 0.23 * Unit(h), hz = 450.0 + 950.0 * Unit(h >> 20);
                    AddTone(mix, at + (int)(off * SampleRate), hz, weight * g.Bubble * (0.5 + 0.5 * Unit(h >> 40)), 90.0, 25.0);
                }
            }
        }

        /// <summary>
        /// Small impacts scattered after a strike, dense at first and thinning out (their offsets fall exponentially over the
        /// spread), each a burst of one to four milliseconds as bright as the ground keeps it.
        /// </summary>
        private static void AddGrains(double[] mix, int at, int count, double size, double bright, double spreadS, ulong seed)
        {
            for (int k = 0; k < count; k++)
            {
                ulong h = StandLayout.Mix(seed ^ ((ulong)(k + 1) * 0x9E3779B97F4A7C15UL));
                double u1 = Unit(h), u2 = Unit(h >> 20), u3 = Unit(h >> 40);
                // Dense at first and thinning out, but never trailing alone into the quiet: the latest grain falls at
                // two and a third spreads, where the ground has settled.
                double off = -spreadS * Math.Log(1.0 - 0.9 * u1);
                int start = at + (int)(off * SampleRate);
                double amp = size * (0.35 + 0.65 * u2) * Math.Exp(-off / (2.0 * spreadS));
                double lenS = 0.001 + 0.003 * u3;
                // Two poles, so a dull ground's grains are dull (one pole alone lets a quarter of the top end through), less
                // the rumble under 150 Hz, which hundreds of grains otherwise add up into slow swings a grain never made.
                AddNoise(mix, start, amp, Cut(bright), 0.02, 0.0, 3.0 / lenS, lenS * 2.0, h, 2);
            }
        }

        /// <summary>
        /// Twigs snapping under the step, from the heel's strike to the foot's settling: one every gap, jittered by a third of it
        /// so no two ever fall together, each a bright crack of a millisecond or two with a ping at a twig's pitch, the later
        /// ones lighter as the foot comes to rest.
        /// </summary>
        private static void AddSnaps(double[] mix, int count, double gapS, double weight, ulong seed)
        {
            for (int k = 0; k < count; k++)
            {
                ulong h = StandLayout.Mix(seed ^ ((ulong)(k + 1) * 0x9E3779B97F4A7C15UL));
                double u1 = Unit(h), u2 = Unit(h >> 20), u3 = Unit(h >> 40);
                double at = (k + 0.5 + 0.33 * (u1 - 0.5)) * gapS;
                int start = (int)(at * SampleRate);
                if (start >= mix.Length) break;
                double amp = weight * (0.4 + 0.6 * u2) * (1.0 - 0.6 * start / (double)mix.Length);
                double lenS = 0.001 + 0.0015 * u3;
                AddNoise(mix, start, amp, Cut(1.0), 0.02, 0.0, 3.0 / lenS, lenS * 2.0, h, 2);
                AddTone(mix, start, 1800.0 + 2400.0 * u2, amp * 0.6, 700.0, 0.0);
            }
        }

        /// <summary>A damped tone from a sample on; its pitch rises by <paramref name="chirp"/> of itself a second when it is a bubble's.</summary>
        private static void AddTone(double[] mix, int at, double hz, double weight, double decay, double chirp)
        {
            if (at < 0 || at >= mix.Length || weight <= 0.0) return;
            double phase = 0.0;
            for (int i = at; i < mix.Length; i++)
            {
                double t = (double)(i - at) / SampleRate;
                double env = Math.Exp(-decay * t);
                if (env < 1e-4) break;
                phase += 2.0 * Math.PI * hz * (1.0 + chirp * t) / SampleRate;
                mix[i] += weight * env * Math.Sin(phase);
            }
        }

        /// <summary>
        /// Noise from a sample on, through a low-pass at <paramref name="cutHi"/> of one pole or two (less a second one-pole at
        /// <paramref name="cutLo"/> when that is above zero, which leaves a band), under an envelope that swells over the attack
        /// and dies at the decay, for at most <paramref name="mostS"/>.
        /// </summary>
        private static void AddNoise(double[] mix, int at, double weight, double cutHi, double cutLo, double attackS, double decay, double mostS, ulong seed, int poles)
        {
            if (at < 0 || at >= mix.Length || weight <= 0.0) return;
            int end = Math.Min(mix.Length, at + (int)(mostS * SampleRate));
            double hi = 0.0, hi2 = 0.0, lo = 0.0;
            for (int i = at; i < end; i++)
            {
                double t = (double)(i - at) / SampleRate;
                double n = Noise(seed, i - at);
                hi += cutHi * (n - hi);
                hi2 += cutHi * (hi - hi2);
                double x = poles > 1 ? hi2 : hi;
                if (cutLo > 0.0)
                {
                    lo += cutLo * (n - lo);
                    x = hi - lo;
                }
                double env = Math.Exp(-decay * t);
                if (attackS > 0.0) env *= 1.0 - Math.Exp(-t / attackS);
                mix[i] += weight * env * x;
            }
        }

        /// <summary>The mix put at its peak and faded over its last samples so it stops without a click, as the engine's clip.</summary>
        private static float[] Finish(double[] mix, double peak)
        {
            double loudest = 0.0;
            foreach (double x in mix) loudest = Math.Max(loudest, Math.Abs(x));
            double scale = loudest > 0.0 ? peak / loudest : 0.0;
            float[] data = new float[mix.Length];
            for (int i = 0; i < mix.Length; i++) data[i] = (float)(mix[i] * scale);
            int fade = Math.Min(256, data.Length / 8);
            for (int i = 0; i < fade; i++) data[data.Length - 1 - i] *= (float)i / fade;
            return data;
        }

        /// <summary>A one-pole filter's cut for a brightness: 0 a dull thud, 1 a bright crack.</summary>
        private static double Cut(double bright) => 0.04 + 0.71 * Clamp01(bright);

        /// <summary>A number in [0, 1) from the low bits of a hash.</summary>
        private static double Unit(ulong h) => (h & 0xFFFFFFUL) / 16777216.0;

        /// <summary>
        /// One percussive sound as M1.5c made every sound: noise through a one-pole filter as bright as the material keeps
        /// it (soil and litter eat the top end, stone keeps it), and a damped tone, under a falling envelope; the last
        /// samples fade so it stops without a click. Since M1.5j the footsteps are made above; the strokes and the
        /// landings still come from here.
        /// </summary>
        /// <param name="toneHz">The pitch of its body, Hz; zero for pure noise.</param>
        /// <param name="tone01">How much of it is that tone rather than noise.</param>
        /// <param name="decay">How fast it dies, per second: bigger is sharper.</param>
        /// <param name="brightness01">0 a dull thud, 1 a bright crack.</param>
        public static float[] Percussive(double seconds, double toneHz, double tone01, double decay, double brightness01, ulong seed)
        {
            int count = Math.Max(16, (int)Math.Round(seconds * SampleRate));
            float[] data = new float[count];
            double cut = 0.04 + (0.75 - 0.04) * Clamp01(brightness01);
            double mix = Clamp01(tone01);
            double step = toneHz > 0.0 ? 2.0 * Math.PI * toneHz / SampleRate : 0.0;
            double low = 0.0, phase = 0.0;
            for (int i = 0; i < count; i++)
            {
                double t = (double)i / SampleRate;
                low += cut * (Noise(seed, i) - low);
                double tone = 0.0;
                if (step > 0.0)
                {
                    phase += step;
                    tone = Math.Sin(phase) * Math.Exp(-decay * 1.6 * t);
                }
                double sample = Math.Exp(-decay * t) * (low + (tone - low) * mix);
                data[i] = (float)(sample < -1.0 ? -1.0 : sample > 1.0 ? 1.0 : sample);
            }
            int fade = Math.Min(256, count / 8);
            for (int i = 0; i < fade; i++) data[count - 1 - i] *= (float)i / fade;
            return data;
        }

        /// <summary>
        /// A swimming stroke (M1.5e), one of <see cref="Variants"/>: water pulled and let go, a wash that swells over its first
        /// eighth of a second and falls away over half a second, duller than a wading step's splash and with no knock in it.
        /// </summary>
        public static float[] Stroke(int variant)
        {
            ulong v = (ulong)(((variant % Variants) + Variants) % Variants) * 17UL;
            float[] data = Percussive(0.55, 0.0, 0.0, 5.0, 0.45, 2700UL + v);
            int swell = Math.Min(data.Length, (int)(0.12 * SampleRate));
            for (int i = 0; i < swell; i++) data[i] *= (float)i / swell;
            return data;
        }

        /// <summary>A stone landing: short, low, and over (v1).</summary>
        public static float[] StoneLanding() => Percussive(0.22, 140.0, 0.35, 28.0, 0.30, 1201UL);

        /// <summary>Wood landing: longer, mid-pitched and rattly (v1).</summary>
        public static float[] WoodLanding() => Percussive(0.28, 320.0, 0.45, 18.0, 0.55, 1202UL);

        /// <summary>White noise in [-1, 1) from a seed and a sample's index: whole numbers only, so a sound is the same sound in every build.</summary>
        private static double Noise(ulong seed, int i) =>
            (StandLayout.Mix(seed * 0x9E3779B97F4A7C15UL ^ (uint)i) >> 11) * (2.0 / 9007199254740992.0) - 1.0;

        private static double Clamp01(double x) => x < 0.0 ? 0.0 : x > 1.0 ? 1.0 : x;
    }
}
