using System;
using EarthGame.ClientCore;
using EarthGame.Engine;
using NUnit.Framework;

namespace EarthGame.Tests.ClientCore
{
    /// <summary>A footstep is an impact and the ground's own answer to it (M1.5j): the numbers an ear would give, asserted.</summary>
    public sealed class FootstepImpactTests
    {
        private const int Rate = FootstepSynth.SampleRate;

        private static double Rms(float[] s, int from, int to)
        {
            from = Math.Max(0, from);
            to = Math.Min(s.Length, to);
            if (to <= from) return 0.0;
            double sum = 0.0;
            for (int i = from; i < to; i++) sum += (double)s[i] * s[i];
            return Math.Sqrt(sum / (to - from));
        }

        /// <summary>The loudest 5 ms window whose start lies between two times, and where it lies, s.</summary>
        private static double LoudestBetween(float[] s, double fromS, double toS, out double atS)
        {
            int window = Rate / 200;
            double best = 0.0;
            atS = fromS;
            for (int start = (int)(fromS * Rate); start + window <= Math.Min(s.Length, (int)(toS * Rate)); start += window / 5)
            {
                double rms = Rms(s, start, start + window);
                if (rms > best) { best = rms; atS = start / (double)Rate; }
            }
            return best;
        }

        /// <summary>Normalised autocorrelation of a stretch of a sound at a lag: one for a pure tone at that period, near zero for noise.</summary>
        private static double Correlation(float[] s, int from, int to, int lag)
        {
            double num = 0.0, den = 0.0;
            for (int i = from; i + lag < to; i++)
            {
                num += (double)s[i] * s[i + lag];
                den += (double)s[i] * s[i];
            }
            return den > 0.0 ? num / den : 0.0;
        }

        /// <summary>How many 1 ms windows are at least three times as loud as the 10 ms before them: the snaps and cracks an ear picks out.</summary>
        private static int Transients(float[] s)
        {
            int ms = Rate / 1000, count = 0, last = -ms * 5;
            for (int start = 10 * ms; start + ms <= s.Length; start += ms)
            {
                double before = Rms(s, start - 10 * ms, start);
                if (before > 1e-4 && Rms(s, start, start + ms) > 3.0 * before && start - last >= 5 * ms)
                {
                    count++;
                    last = start;
                }
            }
            return count;
        }

        /// <summary>When a stated share of a sound's energy has passed, s.</summary>
        private static double EnergyPassed(float[] s, double share)
        {
            double total = 0.0;
            foreach (float x in s) total += (double)x * x;
            double sum = 0.0;
            for (int i = 0; i < s.Length; i++)
            {
                sum += (double)s[i] * s[i];
                if (sum >= share * total) return i / (double)Rate;
            }
            return s.Length / (double)Rate;
        }

        [Test]
        public void AStepIsAHeelAndThenAForefoot()
        {
            foreach (FootingSound footing in (FootingSound[])Enum.GetValues(typeof(FootingSound)))
                for (int v = 0; v < FootstepSynth.Variants; v++)
                {
                    float[] s = FootstepSynth.Step(footing, v);
                    double heel = LoudestBetween(s, 0.0, 0.03, out _);
                    double toe = LoudestBetween(s, 0.06, 0.13, out double at);
                    double between = Rms(s, (int)(0.045 * Rate), (int)(0.058 * Rate));
                    Assert.That(toe, Is.GreaterThan(0.33 * heel), footing + " " + v + ": the forefoot follows the heel");
                    Assert.That(toe, Is.GreaterThan(1.3 * between), footing + " " + v + ": and is a strike of its own at " + at.ToString("0.000") + " s, not the heel's tail");
                }
        }

        [Test]
        public void RockRingsAtItsLowestModeAndSandDoesNot()
        {
            int lag = (int)Math.Round(Rate / FootstepSynth.RockRingHz);
            int from = (int)(0.008 * Rate), to = (int)(0.05 * Rate);
            Assert.That(Correlation(FootstepSynth.Step(FootingSound.Rock, 0), from, to, lag), Is.GreaterThan(0.3), "rock's tail is a tone at its ring");
            Assert.That(Correlation(FootstepSynth.Step(FootingSound.Sand, 0), from, to, lag), Is.LessThan(0.2), "sand's tail is not");
        }

        [Test]
        public void HeathSnapsWhereSandGrinds()
        {
            for (int v = 0; v < FootstepSynth.Variants; v++)
            {
                Assert.That(Transients(FootstepSynth.Step(FootingSound.Heath, v)), Is.GreaterThanOrEqualTo(8), "heath " + v + ": twigs snap");
                Assert.That(Transients(FootstepSynth.Step(FootingSound.Sand, v)), Is.LessThan(4), "sand " + v + ": no grain stands out");
            }
        }

        [Test]
        public void TheSplashOutlastsEveryDryStepAndItsWashIsHeard()
        {
            float[] water = FootstepSynth.Step(FootingSound.Water, 0);
            double splash = EnergyPassed(water, 0.95);
            foreach (FootingSound footing in (FootingSound[])Enum.GetValues(typeof(FootingSound)))
                if (footing != FootingSound.Water)
                    Assert.That(EnergyPassed(FootstepSynth.Step(footing, 0), 0.95), Is.LessThan(splash), footing + " is over before the splash");
            Assert.That(Rms(water, (int)(0.15 * Rate), (int)(0.2 * Rate)), Is.GreaterThan(0.02), "the wash is still heard at a fifth of a second");
        }

        [Test]
        public void HeathIsItsOwnSound()
        {
            byte heath = GroundCovers.Pack(GroundCover.Heath, 0), floor = GroundCovers.Pack(GroundCover.ForestFloor, 0);
            Assert.That(Footing.Of(heath, 0.0), Is.EqualTo(FootingSound.Heath));
            Assert.That(Footing.Of(floor, 0.0), Is.EqualTo(FootingSound.Litter));
            Assert.That(Footing.Of(heath, Footing.WetDepthM), Is.EqualTo(FootingSound.Water), "under water it is water");
            Assert.That(FootstepSynth.Step(FootingSound.Heath, 0), Is.Not.EqualTo(FootstepSynth.Step(FootingSound.Litter, 0)));
        }

        [Test]
        public void EveryStepPeaksInsideTheBand()
        {
            foreach (FootingSound footing in (FootingSound[])Enum.GetValues(typeof(FootingSound)))
                for (int v = 0; v < FootstepSynth.Variants; v++)
                {
                    float peak = 0f;
                    foreach (float x in FootstepSynth.Step(footing, v)) peak = Math.Max(peak, Math.Abs(x));
                    Assert.That(peak, Is.InRange(FootstepSynth.QuietestPeak, FootstepSynth.LoudestPeak), footing + " " + v);
                }
        }
    }
}
