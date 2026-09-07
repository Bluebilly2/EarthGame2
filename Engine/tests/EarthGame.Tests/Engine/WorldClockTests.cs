using EarthGame.Engine;
using NUnit.Framework;

namespace EarthGame.Tests.Engine
{
    public sealed class WorldClockTests
    {
        // The region owns its centre longitude and wake day; 150.675°E runs 10.045 hours ahead of Greenwich.
        private static readonly double BherwerreLon = Region.Bherwerre.CentreLongitudeDeg;
        private static readonly int Aug25 = Region.Bherwerre.WakeDayOfYear;

        [Test]
        public void FromLocalReadsBackTheSameLocalHour()
        {
            WorldClock c = WorldClock.FromLocal(Aug25, 8.0, BherwerreLon);
            Assert.That(c.LocalHourOfDay(BherwerreLon), Is.EqualTo(8.0).Within(1e-9));
            Assert.That(c.LocalDayOfYear(BherwerreLon), Is.EqualTo(Aug25));
        }

        [Test]
        public void EightAmAtBherwerreIsLatePreviousEveningInGreenwich()
        {
            WorldClock c = WorldClock.FromLocal(Aug25, 8.0, BherwerreLon);
            Assert.That(c.UtcHourOfDay, Is.EqualTo(8.0 - BherwerreLon / 15.0 + 24.0).Within(1e-9));
            Assert.That(c.UtcDayOfYear, Is.EqualTo(Aug25 - 1));
        }

        [Test]
        public void OneRealHalfHourIsOneWorldDay()
        {
            WorldClock c = WorldClock.FromLocal(Aug25, 8.0, BherwerreLon);
            c.Advance(WorldClock.RealSecondsPerDay);
            Assert.That(c.LocalHourOfDay(BherwerreLon), Is.EqualTo(8.0).Within(1e-9));
            Assert.That(c.LocalDayOfYear(BherwerreLon), Is.EqualTo(Aug25 + 1));
            Assert.That(c.DaysElapsed, Is.EqualTo(1));
        }

        [Test]
        public void NegativeAdvanceDoesNothing()
        {
            WorldClock c = new WorldClock(100.0);
            c.Advance(-5.0);
            Assert.That(c.TotalHours, Is.EqualTo(100.0));
        }

        [Test]
        public void ResumedCountsTheDaysAlreadyLived()
        {
            WorldClock c = WorldClock.Resumed(Aug25, 8.0, BherwerreLon, 5);
            Assert.That(c.DaysElapsed, Is.EqualTo(5));
        }

        [Test]
        public void RestoreIsExact()
        {
            WorldClock c = WorldClock.Resumed(Aug25, 8.0, BherwerreLon, 3);
            WorldClock r = WorldClock.Restore(c.TotalHours, c.StartedAtHours);
            Assert.That(r.TotalHours, Is.EqualTo(c.TotalHours));
            Assert.That(r.DaysElapsed, Is.EqualTo(3));
        }
    }
}
