using System;
using System.Collections.Generic;
using EarthGame.ClientCore;
using NUnit.Framework;

namespace EarthGame.Tests.ClientCore
{
    /// <summary>Footfalls come from the distance walked (M1.5c promise 1), and the eye's dip and sway stay small.</summary>
    public sealed class StrideTests
    {
        private const double Dt = 0.02;

        private static int Walk(Stride stride, double speedMs, double seconds, bool grounded = true)
        {
            int falls = 0;
            int frames = (int)Math.Round(seconds / Dt);
            for (int i = 0; i < frames; i++)
                if (stride.Advance(Dt, speedMs, grounded, out _)) falls++;
            return falls;
        }

        private static double StepM(double speedMs) => Stride.BaseStepM + Stride.StepPerSpeed * speedMs;

        [Test]
        public void AFootFallsForEveryStepWalked()
        {
            // 1.5 m/s for 10 s is 15 m of ground.
            Assert.That(Walk(new Stride(), 1.5, 10.0), Is.EqualTo((int)Math.Floor(15.0 / StepM(1.5))));
        }

        [Test]
        public void TheSameGroundWalkedSlowerTakesMoreSteps()
        {
            int brisk = Walk(new Stride(), 2.0, 10.0);
            int slow = Walk(new Stride(), 1.0, 20.0);
            Assert.That(brisk, Is.EqualTo((int)Math.Floor(20.0 / StepM(2.0))));
            Assert.That(slow, Is.EqualTo((int)Math.Floor(20.0 / StepM(1.0))));
            Assert.That(slow, Is.GreaterThan(brisk));
        }

        [Test]
        public void NoFootFallsStandingStillOrInTheAir()
        {
            Assert.That(Walk(new Stride(), Stride.WalkingMs * 0.8, 10.0), Is.Zero, "below walking pace");
            Assert.That(Walk(new Stride(), 3.0, 10.0, grounded: false), Is.Zero, "in the air");
            Assert.That(Walk(new Stride(), double.NaN, 1.0), Is.Zero, "a speed nobody knows");
        }

        [Test]
        public void ALandingIsAFootfallAndAFootLeavingTheGroundIsNot()
        {
            Stride stride = new Stride();
            Walk(stride, 1.5, 0.1, grounded: false);
            Assert.That(stride.Advance(Dt, 1.5, true, out _), Is.False, "a tenth of a second off the ground is a stride, not a landing");

            Walk(stride, 1.5, 0.6, grounded: false);
            Assert.That(stride.Advance(Dt, 0.0, true, out Footfall landing), Is.True);
            Assert.That(landing.Landing, Is.True);
            Assert.That(landing.Loudness01, Is.EqualTo(Loudness.FootfallCeiling01), "heard even landing from a standing jump");
        }

        [Test]
        public void TheDipIsSmallAndSpringsBackAndTheSwayStaysInside()
        {
            Stride stride = new Stride();
            double deepest = 0.0, widest = 0.0;
            for (int i = 0; i < 1000; i++)
            {
                stride.Advance(Dt, 4.0, true, out _);
                deepest = Math.Max(deepest, stride.DipNowM);
                widest = Math.Max(widest, Math.Abs(stride.SwayNowM));
            }
            Assert.That(stride.Count, Is.GreaterThan(0));
            Assert.That(deepest, Is.GreaterThan(Stride.DipM).And.LessThanOrEqualTo(Stride.DipM * 1.8), "a run dips more than a walk, and not much");
            Assert.That(widest, Is.GreaterThan(Stride.SwayM * 0.9).And.LessThanOrEqualTo(Stride.SwayM));

            Walk(stride, 0.0, 1.0);
            Assert.That(stride.DipNowM, Is.LessThan(1e-4), "a second standing and the eye is back");
            Assert.That(stride.SwayNowM, Is.EqualTo(0.0), "no sway standing");
        }

        [Test]
        public void TheFeetAlternateAndEverySoundOfAGroundIsHeard()
        {
            Stride stride = new Stride();
            List<Footfall> falls = new List<Footfall>();
            for (int i = 0; i < 5000 && falls.Count < 12; i++)
                if (stride.Advance(Dt, 1.5, true, out Footfall f)) falls.Add(f);
            Assert.That(falls.Count, Is.EqualTo(12));
            HashSet<int> variants = new HashSet<int>();
            for (int i = 0; i < falls.Count; i++)
            {
                variants.Add(falls[i].Variant);
                Assert.That(falls[i].Count, Is.EqualTo(i + 1));
                Assert.That(falls[i].Pitch, Is.InRange(0.9, 1.1));
                Assert.That(falls[i].Loudness01, Is.EqualTo(Loudness.Footfall01(1.5)));
                if (i == 0) continue;
                Assert.That(falls[i].Left, Is.Not.EqualTo(falls[i - 1].Left), "the feet alternate");
                Assert.That(falls[i].Variant, Is.Not.EqualTo(falls[i - 1].Variant), "never the same sound twice running");
            }
            Assert.That(variants.Count, Is.EqualTo(FootstepSynth.Variants));
        }

        [Test]
        public void AFootfallIsLouderTheFasterAndNeverLoud()
        {
            Assert.That(Loudness.Footfall01(Stride.WalkingMs * 0.5), Is.Zero);
            Assert.That(Loudness.Footfall01(double.NaN), Is.Zero);
            Assert.That(Loudness.Footfall01(3.0), Is.GreaterThan(Loudness.Footfall01(1.0)));
            Assert.That(Loudness.Footfall01(1000.0), Is.EqualTo(Loudness.FootfallCeiling01));
        }

        [Test]
        public void ALandingIsHeardByTheSquareRootOfItsEnergy()
        {
            Assert.That(Loudness.Landing01(Loudness.LandingFloorJ * 0.5), Is.Zero);
            Assert.That(Loudness.Landing01(double.NaN), Is.Zero);
            Assert.That(Loudness.Landing01(Loudness.LandingFullJ / 4.0), Is.EqualTo(0.5).Within(1e-12));
            Assert.That(Loudness.Landing01(Loudness.LandingFullJ * 10.0), Is.EqualTo(1.0));
        }
    }
}
