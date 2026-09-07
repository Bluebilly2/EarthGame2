using EarthGame.Engine;
using NUnit.Framework;

namespace EarthGame.Tests.Engine
{
    /// <summary>
    /// The sun is the founder's clock, calendar and compass before anything else exists, and the chronicle opens
    /// by using it: "Morning sun in the north-east." That has to be true, and checkable against a published solar
    /// calculator, or none of the navigation and timekeeping that follows can be honest (GAME_DESIGN §3). Ported
    /// from v1 and moved to Bherwerre; the owner's solar_check is the independent check these tests are not.
    /// </summary>
    public sealed class SolarClockTests
    {
        private const int August25 = 237;

        private static SolarClock At(double hour)
            => new SolarClock(Region.Bherwerre.CentreLatitudeDeg, Region.Bherwerre.CentreLongitudeDeg, August25, hour);

        [Test]
        public void DeclinationIsRightForLateAugust()
        {
            double dec = At(12.0).DeclinationDeg;
            Assert.That(dec, Is.EqualTo(10.9).Within(1.0), "the sun is about 11 degrees north of the equator on 25 August, got " + dec.ToString("F2"));
        }

        [Test]
        public void TheSunIsUpWhenTheFounderWakes()
        {
            SolarClock clock = At(Region.Bherwerre.WakeLocalHour);
            double elevation = clock.SolarElevationDeg;
            Assert.That(elevation, Is.GreaterThan(0.0), "the sun must be above the horizon at the wake, got " + elevation.ToString("F1"));
            Assert.That(elevation, Is.EqualTo(18.0).Within(6.0), "at 08:00 the sun is roughly 18 degrees up at 35 S, got " + elevation.ToString("F1"));
            Assert.That(clock.IsDaylight, Is.True, "08:00 must count as daylight");
        }

        [Test]
        public void MorningSunIsInTheNorthEast()
        {
            // Southern hemisphere: the winter sun tracks through the north. The chronicle's first navigational fact.
            double azimuth = At(8.0).SolarAzimuthDeg;
            Assert.That(azimuth, Is.EqualTo(60.0).Within(20.0), "morning sun should bear north-east, got " + azimuth.ToString("F0"));
        }

        [Test]
        public void NoonSunIsDueNorthAndHighest()
        {
            SolarClock noon = At(12.0);
            double azimuth = noon.SolarAzimuthDeg;
            Assert.That(azimuth, Is.EqualTo(0.0).Within(3.0).Or.EqualTo(360.0).Within(3.0), "at local noon in the southern hemisphere the sun bears due north, got " + azimuth.ToString("F0"));
            Assert.That(noon.SolarElevationDeg, Is.GreaterThan(At(9.0).SolarElevationDeg), "noon is higher than morning");
            Assert.That(noon.SolarElevationDeg, Is.GreaterThan(At(15.0).SolarElevationDeg), "noon is higher than afternoon");
        }

        [Test]
        public void AfternoonSunIsInTheNorthWest()
        {
            double azimuth = At(16.0).SolarAzimuthDeg;
            Assert.That(azimuth, Is.EqualTo(300.0).Within(20.0), "afternoon sun bears north-west, got " + azimuth.ToString("F0"));
        }

        [Test]
        public void NightIsDarkAndDawnIsNot()
        {
            Assert.That(At(2.0).IsNight, Is.True, "two in the morning is night");
            Assert.That(At(2.0).IsDaylight, Is.False);
            Assert.That(At(2.0).SolarElevationDeg, Is.LessThan(-6.0), "the sun is well below the horizon at 02:00");
            Assert.That(At(12.0).IsDaylight, Is.True, "midday is daylight");
        }

        [Test]
        public void LateAugustGivesAboutElevenHoursOfLight()
        {
            double hours = At(12.0).DaylightHours;
            Assert.That(hours, Is.EqualTo(11.1).Within(0.6), "25 August at 35 S gives about eleven hours of daylight, got " + hours.ToString("F2"));
        }

        [Test]
        public void SunriseAndSunsetSitEitherSideOfNoon()
        {
            SolarClock clock = At(12.0);
            Assert.That(clock.SunriseHour + clock.SunsetHour, Is.EqualTo(24.0).Within(1e-9), "local solar noon is noon, so the two are symmetric");
            Assert.That(clock.SunsetHour - clock.SunriseHour, Is.EqualTo(clock.DaylightHours).Within(1e-9));
            Assert.That(clock.SunriseHour, Is.EqualTo(6.45).Within(0.3), "sunrise about a quarter to seven, local solar, got " + clock.SunriseHour.ToString("F2"));
            // Just before sunrise the sun is below the refracted horizon; just after, above it.
            Assert.That(At(clock.SunriseHour - 0.05).IsDaylight, Is.False);
            Assert.That(At(clock.SunriseHour + 0.05).IsDaylight, Is.True);
        }

        [Test]
        public void TheDeadlineCountsDown()
        {
            double atEight = At(8.0).HoursUntilSunset;
            double atNoon = At(12.0).HoursUntilSunset;
            Assert.That(atEight, Is.GreaterThan(atNoon), "there is less light left at noon than at eight");
            Assert.That(atEight, Is.EqualTo(9.55).Within(0.6), "about nine and a half hours of light remain at 08:00, got " + atEight.ToString("F2"));
            Assert.That(At(20.0).HoursUntilSunset, Is.EqualTo(0.0), "after sunset nothing remains");
        }

        [Test]
        public void TimeAdvancesAndRollsOverIntoTheNextDay()
        {
            SolarClock clock = At(8.0);
            clock.Advance(SolarClock.RealSecondsPerDay * 0.5);
            Assert.That(clock.HourOfDay, Is.EqualTo(20.0).Within(0.01), "half a day later is 20:00");
            Assert.That(clock.DaysElapsed, Is.EqualTo(0), "still the first day");
            clock.Advance(SolarClock.RealSecondsPerDay * 0.5);
            Assert.That(clock.HourOfDay, Is.EqualTo(8.0).Within(0.01));
            Assert.That(clock.DaysElapsed, Is.EqualTo(1), "a full day has passed");
            Assert.That(clock.DayOfYear, Is.EqualTo(August25 + 1), "and the date has moved on");
        }

        [Test]
        public void SeasonsAreOppositeAcrossTheEquator()
        {
            SolarClock south = new SolarClock(-35.14, 150.675, August25, 12.0);
            SolarClock north = new SolarClock(35.14, 150.675, August25, 12.0);
            Assert.That(south.DaylightHours, Is.LessThan(north.DaylightHours), "August is winter in the south and summer in the north");
        }

        [Test]
        public void AResumedClockRemembersHowManyDaysHavePassed()
        {
            // In v1, loading a save reset the day count to zero and then wrote that zero back over the saved value.
            SolarClock fresh = new SolarClock(-35.14, 150.675, 237, 8.0);
            Assert.That(fresh.DaysElapsed, Is.EqualTo(0), "a new run starts on day zero");
            SolarClock resumed = new SolarClock(-35.14, 150.675, WorldClock.Resumed(237, 8.0, 150.675, 5));
            Assert.That(resumed.DaysElapsed, Is.EqualTo(5), "a resumed one starts where it left off");
            Assert.That(resumed.HourOfDay, Is.EqualTo(fresh.HourOfDay).Within(1e-9), "without moving the time of day");
            Assert.That(resumed.DayOfYear, Is.EqualTo(fresh.DayOfYear), "or the date, which is what the sun is computed from");
            resumed.World.Advance(WorldClock.RealSecondsPerDay * 2.0);
            Assert.That(resumed.DaysElapsed, Is.EqualTo(7), "two more days makes seven, not two");
        }

        [Test]
        public void ResumingWithNonsenseDoesNotRunTheClockBackwards()
        {
            SolarClock clock = new SolarClock(-35.14, 150.675, WorldClock.Resumed(237, 8.0, 150.675, -3));
            Assert.That(clock.DaysElapsed, Is.EqualTo(0), "a negative day count is treated as none");
        }

        [Test]
        public void TheRegionsWakeIsTheSameSunAsTheClockBuiltByHand()
        {
            SolarClock viaRegion = SolarClock.AtWake(Region.Bherwerre);
            SolarClock byHand = At(Region.Bherwerre.WakeLocalHour);
            Assert.That(viaRegion.HourOfDay, Is.EqualTo(byHand.HourOfDay).Within(1e-9));
            Assert.That(viaRegion.DayOfYear, Is.EqualTo(byHand.DayOfYear));
            Assert.That(viaRegion.SolarElevationDeg, Is.EqualTo(byHand.SolarElevationDeg).Within(1e-9));
        }

        /// <summary>
        /// Stepping the absolute clock back an hour at a time and asking for the local reading walks the founder's
        /// own hours backwards. v1's fauna binding once split the absolute hours into a day and an hour instead, so
        /// at 150.76 E (ten hours ahead of Greenwich) a day-long series for 06:00 began at 19:57 the evening before.
        /// </summary>
        [Test]
        public void SteppingBackAnHourWalksTheFoundersOwnLocalHours()
        {
            const double Lon = 150.76;
            WorldClock now = WorldClock.FromLocal(August25, 6.0, Lon);
            Assert.That(now.UtcDayOfYear, Is.EqualTo(August25 - 1), "06:00 at the wake beach is still the previous day at Greenwich");
            Assert.That(now.UtcHourOfDay, Is.EqualTo(19.95).Within(0.05), "and about 20:00 there");
            for (int back = 0; back < 24; back++)
            {
                WorldClock then = new WorldClock(now.TotalHours - back);
                double expected = ((6.0 - back) % 24.0 + 24.0) % 24.0;
                Assert.That(then.LocalHourOfDay(Lon), Is.EqualTo(expected).Within(1e-6), back + " hours before 06:00 local");
            }
            Assert.That(new WorldClock(now.TotalHours - 5).LocalDayOfYear(Lon), Is.EqualTo(August25), "01:00 local is still that day");
            Assert.That(new WorldClock(now.TotalHours - 7).LocalDayOfYear(Lon), Is.EqualTo(August25 - 1), "23:00 local is the day before");
        }
    }
}
