using EarthGame.Engine;
using NUnit.Framework;

namespace EarthGame.Tests.Engine
{
    /// <summary>
    /// The water in a founder's body (FP.1), held to the physiology v1's BodyState restated: 42 litres in the body, 2.4
    /// lost a day at rest, death at 15% lost, a litre and a half a drink. The numbers here are the published ones written
    /// again, not read from the class, so a slip in the class is a red test.
    /// </summary>
    public sealed class HydrationTests
    {
        private const double Litres = 42.0, LossPerDay = 2.4;

        [Test]
        public void ADayAtRestLosesTwoPointFourLitresOfFortyTwo()
        {
            Hydration body = new Hydration();
            Assert.That(body.Water01, Is.EqualTo(1.0));
            body.Advance(1.0);
            Assert.That(body.Water01, Is.EqualTo(1.0 - LossPerDay / Litres).Within(1e-12));
            Assert.That(body.DeficitL, Is.EqualTo(LossPerDay).Within(1e-9));
            body.Advance(0.5);
            Assert.That(body.DeficitL, Is.EqualTo(1.5 * LossPerDay).Within(1e-9), "the loss is by the day, in any pieces");
            body.Advance(0.0);
            body.Advance(-3.0);
            Assert.That(body.DeficitL, Is.EqualTo(1.5 * LossPerDay).Within(1e-9), "no day, or a day backwards, moves nothing");
        }

        [Test]
        public void ThirstIsLethalALittleUnderThreeDaysInAtRest()
        {
            Hydration body = new Hydration();
            double days = 0.15 * Litres / LossPerDay;
            Assert.That(days, Is.EqualTo(2.625).Within(1e-9), "the published fraction at the published rate");
            Assert.That(body.HoursToCollapse, Is.EqualTo(days * 24.0).Within(1e-9));
            body.Advance(days - 0.01);
            Assert.That(body.IsAlive, Is.True);
            Assert.That(body.HoursToCollapse, Is.EqualTo(0.24).Within(1e-9));
            body.Advance(0.02);
            Assert.That(body.IsAlive, Is.False);
            Assert.That(body.HoursToCollapse, Is.EqualTo(0.0));
            Assert.That(body.HoursToCollapseAt(0.0), Is.EqualTo(0.0), "past the lethal loss there are no hours left, whatever the rate");
            Assert.That(new Hydration().HoursToCollapseAt(0.0), Is.EqualTo(double.PositiveInfinity), "a body losing nothing has forever");
        }

        // A hair either side of each threshold: at the threshold itself, 1 − (1 − x) is not always x in floating point.
        [TestCase(0.0000, ThirstLevel.Fine)]
        [TestCase(0.0149, ThirstLevel.Fine)]
        [TestCase(0.0151, ThirstLevel.Thirsty)]
        [TestCase(0.0399, ThirstLevel.Thirsty)]
        [TestCase(0.0401, ThirstLevel.VeryThirsty)]
        [TestCase(0.0699, ThirstLevel.VeryThirsty)]
        [TestCase(0.0701, ThirstLevel.Failing)]
        [TestCase(0.1099, ThirstLevel.Failing)]
        [TestCase(0.1101, ThirstLevel.Collapsing)]
        [TestCase(0.1500, ThirstLevel.Collapsing)]
        public void TheWordsComeAtTheirThresholds(double loss, ThirstLevel expected)
        {
            Assert.That(Hydration.LevelOf(1.0 - loss), Is.EqualTo(expected));
            Hydration body = new Hydration();
            body.Restore(1.0 - loss);
            Assert.That(body.Thirst, Is.EqualTo(expected));
        }

        [Test]
        public void TheWordsAreTheFoundersOwnAndFineSaysNothing()
        {
            Assert.That(Hydration.WordFor(ThirstLevel.Fine), Is.Empty);
            Assert.That(Hydration.WordFor(ThirstLevel.Thirsty), Is.EqualTo("thirsty"));
            Assert.That(Hydration.WordFor(ThirstLevel.VeryThirsty), Is.EqualTo("very thirsty"));
            Assert.That(Hydration.WordFor(ThirstLevel.Failing), Is.EqualTo("failing"));
            Assert.That(Hydration.WordFor(ThirstLevel.Collapsing), Is.EqualTo("collapsing"));
        }

        [TestCase(0.00, 1.00)]
        [TestCase(0.02, 0.97)]
        [TestCase(0.04, 0.90)]
        [TestCase(0.06, 0.75)]
        [TestCase(0.10, 0.50)]
        [TestCase(0.15, 0.15)]
        [TestCase(0.20, 0.15)]
        public void WhatThirstLeavesIsThePublishedTable(double loss, double capacity)
        {
            Assert.That(Hydration.CapacityOf(1.0 - loss), Is.EqualTo(capacity).Within(1e-9));
        }

        [Test]
        public void BetweenTheTablesPointsTheCapacityIsAStraightLine()
        {
            Assert.That(Hydration.CapacityOf(1.0 - 0.03), Is.EqualTo(0.935).Within(1e-9), "halfway from 2% to 4% is halfway from 0.97 to 0.90");
            Assert.That(Hydration.CapacityOf(1.0 - 0.08), Is.EqualTo(0.625).Within(1e-9), "halfway from 6% to 10% is halfway from 0.75 to 0.50");
        }

        [Test]
        public void ADrinkIsALitreAndAHalfAtMostAndNeverPastFull()
        {
            Hydration body = new Hydration();
            body.Advance(2.0);
            double before = body.Water01;
            Assert.That(body.Drink(10.0), Is.EqualTo(1.5).Within(1e-12), "a visit's worth, however much is offered");
            Assert.That(body.Water01, Is.EqualTo(before + 1.5 / Litres).Within(1e-12));
            Assert.That(body.Drink(0.5), Is.EqualTo(0.5).Within(1e-12), "a smaller offer is taken whole");
            Assert.That(body.Drink(-1.0), Is.EqualTo(0.0), "nothing is taken from nothing");
            body.Restore(1.0 - 0.5 / Litres);
            Assert.That(body.Drink(1.5), Is.EqualTo(0.5).Within(1e-12), "only what the body is short of");
            Assert.That(body.Water01, Is.EqualTo(1.0));
        }

        [Test]
        public void ARestoredBodyIsHeldToAFractionAndNaNIsFull()
        {
            Hydration body = new Hydration();
            body.Restore(0.9);
            Assert.That(body.Water01, Is.EqualTo(0.9));
            body.Restore(1.7);
            Assert.That(body.Water01, Is.EqualTo(1.0));
            body.Restore(-0.2);
            Assert.That(body.Water01, Is.EqualTo(0.0));
            body.Restore(double.NaN);
            Assert.That(body.Water01, Is.EqualTo(1.0));
        }

        [Test]
        public void TheClockSaysHowManyDaysTheBodyLives()
        {
            WorldClock clock = new WorldClock(0.0);
            Assert.That(clock.DaysFor(WorldClock.RealSecondsPerDay), Is.EqualTo(1.0).Within(1e-12), "a day of real seconds at the game's rate");
            clock.Scale = 2880.0;
            Assert.That(clock.DaysFor(30.0), Is.EqualTo(1.0).Within(1e-12), "sped 2,880 times, the scenarios' pace, thirty seconds is a day");
            clock.Scale = 0.0;
            Assert.That(clock.DaysFor(30.0), Is.EqualTo(0.0), "a held clock holds the body");
            clock.Scale = 1.0;
            Assert.That(clock.DaysFor(-1.0), Is.EqualTo(0.0));
        }
    }
}
