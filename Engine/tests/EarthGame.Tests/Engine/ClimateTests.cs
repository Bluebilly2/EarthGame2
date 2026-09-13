using System;
using EarthGame.Engine;
using NUnit.Framework;

namespace EarthGame.Tests.Engine
{
    /// <summary>
    /// Bherwerre's climate against the weather station across the bay (M1.8a promises 1 and 2): Point Perpendicular
    /// Lighthouse's twelve months less the region's warming since records began, the air colder with height above the
    /// station, and nights coldest at dawn with no seam where the day and the night meet.
    /// </summary>
    public sealed class ClimateTests
    {
        // ---- the referent, restated and never tuned: Point Perpendicular Lighthouse, Bureau station 068034, 85 m, the
        // Bureau's 1991–2004 figures as reproduced on Wikipedia's Jervis Bay Village page ----

        private static readonly double[] StationMeanMaxC = { 24.2, 24.4, 23.0, 21.1, 18.6, 16.6, 15.6, 16.8, 18.5, 20.1, 21.0, 23.1 };
        private static readonly double[] StationMeanMinC = { 17.9, 18.4, 17.1, 14.9, 12.8, 10.6, 9.5, 9.5, 11.4, 13.0, 14.3, 16.6 };
        private static readonly int[] DaysInMonth = { 31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31 };
        private const int August = 7;
        private const int FirstOfAugust = 213;
        private const double StationElevationM = 85.0;

        /// <summary>
        /// The region's warming since national records began, °C, restated from its source (Bureau of Meteorology and CSIRO,
        /// <i>State of the Climate 2024</i>) rather than read from the climate, so a changed constant there cannot pass here.
        /// </summary>
        private const double WarmingSinceRecordsC = 1.51;

        /// <summary>The published uncertainty of that warming, °C; August is held to twice it.</summary>
        private const double WarmingUncertaintyC = 0.23;

        private static Climate Bherwerre => Climate.ForRegion(Region.Bherwerre);

        [Test]
        public void EveryMonthIsTheLighthousesLessTheRegionsWarming()
        {
            Climate climate = Bherwerre;
            int first = 1;
            for (int m = 0; m < 12; m++)
            {
                MonthExtremes(climate, first, DaysInMonth[m], out double meanMax, out double meanMin);
                double targetMax = StationMeanMaxC[m] - WarmingSinceRecordsC;
                double targetMin = StationMeanMinC[m] - WarmingSinceRecordsC;
                // A yearly wave for the day's mean and one for its range, fitted to the twelve months, leave the worst month
                // (November) 0.7 °C from its own mean: no month may stand further off than a degree.
                Assert.That(meanMax, Is.EqualTo(targetMax).Within(1.0),
                    "month " + (m + 1) + ": a mean maximum of " + meanMax.ToString("F2") + " °C against " + targetMax.ToString("F2"));
                Assert.That(meanMin, Is.EqualTo(targetMin).Within(1.0),
                    "month " + (m + 1) + ": a mean minimum of " + meanMin.ToString("F2") + " °C against " + targetMin.ToString("F2"));
                first += DaysInMonth[m];
            }
        }

        [Test]
        public void AugustIsThePreHumanLateWinterTheFounderWakesInto()
        {
            MonthExtremes(Bherwerre, FirstOfAugust, DaysInMonth[August], out double meanMax, out double meanMin);
            double targetMax = StationMeanMaxC[August] - WarmingSinceRecordsC;
            double targetMin = StationMeanMinC[August] - WarmingSinceRecordsC;
            Assert.That(meanMax, Is.EqualTo(targetMax).Within(2.0 * WarmingUncertaintyC),
                "August's mean maximum was " + meanMax.ToString("F2") + " °C against a pre-human " + targetMax.ToString("F2")
                + " (the lighthouse's " + StationMeanMaxC[August].ToString("F1") + " less " + WarmingSinceRecordsC.ToString("F2") + ")");
            Assert.That(meanMin, Is.EqualTo(targetMin).Within(2.0 * WarmingUncertaintyC),
                "and its mean minimum " + meanMin.ToString("F2") + " °C against " + targetMin.ToString("F2"));
            Assert.That(meanMin, Is.GreaterThan(6.0).And.LessThan(10.0), "a late-August night on this coast is milder than the highlands' by some way");
        }

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
        }

        [Test]
        public void ARegionThisBuildHoldsNoStationRecordForIsRefused()
        {
            Region elsewhere = new Region("fixture", "Fixture", Region.Bherwerre.CentreLatitudeDeg, Region.Bherwerre.CentreLongitudeDeg, 1600.0, 237, 8.0);
            Assert.Throws<ArgumentException>(() => Climate.ForRegion(elsewhere), "not given another place's weather");
            Assert.Throws<ArgumentNullException>(() => Climate.ForRegion(null));
        }

        /// <summary>The mean of each day's highest reading and each night's lowest across a month, a minute apart, as a station keeps them.</summary>
        private static void MonthExtremes(Climate climate, int firstDay, int days, out double meanMax, out double meanMin)
        {
            double maxSum = 0.0, minSum = 0.0;
            for (int day = firstDay; day < firstDay + days; day++)
            {
                double high = double.MinValue, low = double.MaxValue;
                for (int minute = 0; minute < 24 * 60; minute++)
                {
                    double t = climate.AirTemperatureC(day, minute / 60.0, StationElevationM);
                    if (minute < 12 * 60) low = Math.Min(low, t);
                    else high = Math.Max(high, t);
                }
                maxSum += high;
                minSum += low;
            }
            meanMax = maxSum / days;
            meanMin = minSum / days;
        }
    }
}
