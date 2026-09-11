using System;
using EarthGame.Engine;

namespace EarthGame.ClientCore
{
    /// <summary>
    /// The founder's footsteps and a thing's landing, made from arithmetic rather than recorded (M1.5c; v1's
    /// <c>ProceduralAudio</c>, ported): a burst of filtered noise under a falling envelope, with a damped tone for anything
    /// that has a body. A sound is like what makes it because the numbers say so — grass a wide hiss with no tone in it,
    /// rock short and sharp with a knock, sand the dullest of all. Samples only; the client makes them into its engine's
    /// clips.
    /// </summary>
    public static class FootstepSynth
    {
        public const int SampleRate = 44100;

        /// <summary>How many of each footstep there are: the same step twice in a row is what gives a game away fastest (v1).</summary>
        public const int Variants = 4;

        /// <summary>
        /// One percussive sound: noise through a one-pole filter as bright as the material keeps it (soil and litter eat
        /// the top end, stone keeps it), and a damped tone, under a falling envelope; the last samples fade so it stops
        /// without a click.
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
        /// A footstep on a kind of ground, one of <see cref="Variants"/>: v1's numbers for grass, litter, rock, sand and
        /// soil; the splash of a wading step, new here, a long bright wash with no knock in it.
        /// </summary>
        public static float[] Step(FootingSound footing, int variant)
        {
            ulong v = (ulong)(((variant % Variants) + Variants) % Variants) * 17UL;
            switch (footing)
            {
                case FootingSound.Grass: return Percussive(0.20, 0.0, 0.0, 24.0, 0.80, 2100UL + v);
                case FootingSound.Litter: return Percussive(0.24, 0.0, 0.0, 19.0, 0.92, 2200UL + v);
                case FootingSound.Rock: return Percussive(0.14, 210.0, 0.25, 36.0, 0.62, 2300UL + v);
                case FootingSound.Sand: return Percussive(0.22, 0.0, 0.0, 22.0, 0.20, 2500UL + v);
                case FootingSound.Water: return Percussive(0.34, 0.0, 0.0, 11.0, 0.70, 2600UL + v);
                default: return Percussive(0.16, 0.0, 0.0, 30.0, 0.34, 2400UL + v);
            }
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
