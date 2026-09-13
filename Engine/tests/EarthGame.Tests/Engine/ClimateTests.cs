using System;
using EarthGame.Engine;
using NUnit.Framework;

namespace EarthGame.Tests.Engine
{
    /// <summary>
    /// The climate's own curve, the day under the weather (M1.8a promise 2): nights coldest at dawn and days warmest in the
    /// early afternoon, no seam where the day and the night meet, the air colder with height above the station, and no
    /// climate for a region this build holds no record for. Whether the weather built on the curve gives the lighthouse's
    /// months is WeatherTests' to hold, since a station's months are its weather's, fronts and all.
    /// </summary>
    public sealed class ClimateTests
    {
        private const double StationElevationM = 85.0;

        private static Climate Bherwerre => Climate.ForRegion(Region.Bherwerre);

        [Test]
        public void NightsAreColdestAtDawnAndDaysWarmestInTheEarlyAfternoon()
        {
            Climate climate = Bherwerre;
            foreach (int day in new[] { 15, 105, 196, 237, 288, 350 })
            {
                double daylight = SolarClock.DaylightHoursFor(Region.Bherwerre.CentreLatitudeDeg, SolarClock.DeclinationDegFor(day));
                double sunrise = SolarClock.SunriseHourFor(daylight);
                int coldest = 0, warmest = 0;
                double low = double.MaxValue, high = double.MinValue;
                for (int minute = 0; minute < 24 * 60; minute++)
                {
                    double t = climate.AirTemperatureC(day, minute / 60.0, StationElevationM);
                    if (t < low) { low = t; coldest = minute; }
                    if (t > high) { high = t; warmest = minute; }
                }
                Assert.That(coldest / 60.0, Is.EqualTo(sunrise).Within(0.25),
                    "day " + day + ": the coldest minute at " + (coldest / 60.0).ToString("F2") + " h, within a quarter of an hour of sunrise at "
                    + sunrise.ToString("F2"));
                Assert.That(warmest / 60.0, Is.InRange(13.0, 14.5),
                    "day " + day + ": the warmest minute at " + (warmest / 60.0).ToString("F2") + " h, in the early afternoon");
            }
        }

        [Test]
        public void TheDayAndTheNightMeetWithoutASeam()
        {
            Climate climate = Bherwerre;
            for (int day = 1; day <= 365; day++)
            {
                double previous = climate.AirTemperatureC(day, 0.0, 0.0);
                for (int second = 30; second < 24 * 3600; second += 30)
                {
                    double t = climate.AirTemperatureC(day, second / 3600.0, 0.0);
                    Assert.That(Math.Abs(t - previous), Is.LessThan(0.02),
                        "day " + day + " at " + (second / 3600.0).ToString("F3") + " h stepped " + (t - previous).ToString("F3") + " °C in thirty seconds");
                    previous = t;
                }
                int next = day == 365 ? 1 : day + 1;
                Assert.That(Math.Abs(climate.AirTemperatureC(next, 0.0, 0.0) - previous), Is.LessThan(0.02),
                    "and midnight into day " + next + " is no seam either");
            }
        }

        [Test]
        public void TheAirIsColderByTheLapseRateWithHeightAboveTheStation()
        {
            Climate climate = Bherwerre;
            double atStation = climate.AirTemperatureC(237, 9.0, StationElevationM);
            Assert.That(climate.AirTemperatureC(237, 9.0, 0.0) - atStation, Is.EqualTo(StationElevationM / 1000.0 * Climate.LapseRateCPerKm).Within(1e-9),
                "warmer at the sea than at the lighthouse's 85 m");
            Assert.That(atStation - climate.AirTemperatureC(237, 9.0, StationElevationM + 500.0), Is.EqualTo(0.5 * Climate.LapseRateCPerKm).Within(1e-9),
                "and half a kilometre higher, colder by half the rate a kilometre");
            Assert.That(Climate.LapseRateCPerKm, Is.EqualTo(6.5), "the standard atmosphere's lapse rate");
            Assert.That(climate.StationElevationM, Is.EqualTo(StationElevationM), "the height the climate takes nothing off at is the lighthouse's");
        }

        [Test]
        public void ARegionThisBuildHoldsNoStationRecordForIsRefused()
        {
            Region elsewhere = new Region("fixture", "Fixture", Region.Bherwerre.CentreLatitudeDeg, Region.Bherwerre.CentreLongitudeDeg, 1600.0, 237, 8.0);
            Assert.Throws<ArgumentException>(() => Climate.ForRegion(elsewhere), "not given another place's weather");
            Assert.Throws<ArgumentNullException>(() => Climate.ForRegion(null));
        }
    }
}
