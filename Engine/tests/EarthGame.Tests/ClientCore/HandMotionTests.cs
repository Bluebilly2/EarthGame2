using System;
using EarthGame.ClientCore;
using NUnit.Framework;

namespace EarthGame.Tests.ClientCore
{
    /// <summary>The thing in hand hangs off a shoulder, trails by its weight, walks with the stride and swings on a use (M1.5c promises 2 and 3).</summary>
    public sealed class HandMotionTests
    {
        private const double Frame = 1.0 / 60.0;

        [Test]
        public void AHeavyThingTrailsTheHeadAndALightOneKeepsUp()
        {
            double bark = HandMotion.Follow(Frame, 0.03);
            double stone = HandMotion.Follow(Frame, 3.0);
            Assert.That(bark, Is.InRange(0.0, 1.0));
            Assert.That(stone, Is.GreaterThan(0.0).And.LessThan(bark));
            Assert.That(HandMotion.Follow(Frame, 600.0), Is.EqualTo(HandMotion.Follow(Frame, 6.0)), "past a load an arm holds, all trail alike");
            Assert.That(HandMotion.Follow(2 * Frame, 3.0), Is.GreaterThan(stone), "a longer frame gets further");
        }

        [Test]
        public void AShoulderPassesOnItsShareOfThePitch()
        {
            foreach (double pitch in new[] { -80.0, -20.0, 0.0, 36.0, 89.0 })
                Assert.That(pitch + HandMotion.CounterPitchDeg(pitch), Is.EqualTo(HandMotion.PitchFollow * pitch).Within(1e-9));
        }

        [Test]
        public void AUseSwingsOutQuicklyAndBackSlowly()
        {
            HandMotion hand = new HandMotion();
            Assert.That(hand.Swing(Frame), Is.Zero, "no swing before a use");
            hand.Strike();
            double peak = 0.0, last = 0.0;
            int peakAt = -1, frames = 0;
            for (double t = 0.0; t < HandMotion.StrikeSeconds + 0.1; t += Frame, frames++)
            {
                last = hand.Swing(Frame);
                Assert.That(last, Is.InRange(0.0, 1.0));
                if (last > peak)
                {
                    peak = last;
                    peakAt = frames;
                }
            }
            Assert.That(peak, Is.GreaterThan(0.9));
            Assert.That(peakAt * Frame, Is.LessThan(HandMotion.StrikeSeconds / 2.0), "out in the first half");
            Assert.That(last, Is.Zero, "and home again");
        }

        [Test]
        public void TheHandIsStillAtRestAndWalksSmall()
        {
            HandMotion hand = new HandMotion();
            hand.Bob(Frame, 0.0, out double side, out double down);
            Assert.That(side, Is.Zero);
            Assert.That(down, Is.Zero);
            double most = 0.0;
            for (int i = 0; i < 600; i++)
            {
                hand.Bob(Frame, 12.0, out side, out down);
                Assert.That(down, Is.GreaterThanOrEqualTo(0.0));
                most = Math.Max(most, Math.Max(Math.Abs(side), down));
            }
            Assert.That(most, Is.GreaterThan(0.0).And.LessThanOrEqualTo(4.0 * 0.006 + 1e-12), "a few centimetres at most, however fast");
            hand.Bob(Frame, double.NaN, out side, out down);
            Assert.That(double.IsNaN(side) || double.IsNaN(down), Is.False);
        }
    }
}
