using System;
using EarthGame.Engine;
using NUnit.Framework;

namespace EarthGame.Tests.Engine
{
    /// <summary>
    /// The weather against the lighthouse across the bay (M1.8a promises 3 to 5; M1.8c, the lighthouse's own record): v1's
    /// harness, moved from Moss Vale to Point Perpendicular. The rain's totals, days and season against the station's record
    /// with the drying undone; the wind, the cloud, the humidity and the sun against its 9 am and 3 pm readings and its
    /// satellite's days; the spread of August's nights against its deciles; fronts with dry spells between, showers and not
    /// drizzle, and most hours dry; one sky whose readings agree; a cold snap that is an air mass; the rain's season kept off
    /// the temperature; and the weather a function of the seed and the time, with no seam at the new year.
    /// </summary>
    public sealed class WeatherTests
    {
        // ---- the referent, restated and never tuned: Point Perpendicular Lighthouse, Bureau station 068034, the Bureau's own
        // "Climate statistics for Australian locations" table, all years of record, as Data/stations/ holds it (M1.8c) ----

        /// <summary>The station's rain by month, mm, 1899–2004, and its days of rain at the Bureau's stated thresholds: 1 mm or more, any rain (0.2 mm or more), 10 mm or more.</summary>
        private static readonly double[] StationRainMm = { 97.2, 98.5, 122.4, 131.8, 134.1, 128.8, 107.2, 89.7, 79.2, 85.3, 84.3, 83.1 };
        private static readonly double[] StationRainDays = { 9.2, 9.1, 10.1, 9.5, 10.2, 9.8, 8.9, 8.3, 8.6, 9.1, 9.1, 8.6 };
        private static readonly double[] StationRainDaysAny = { 11.7, 11.5, 12.7, 11.9, 12.2, 11.6, 10.6, 10.1, 10.4, 11.2, 11.3, 11.3 };
        private static readonly double[] StationRainDays10 = { 2.7, 2.5, 3.2, 3.4, 3.5, 3.6, 3.2, 2.5, 2.1, 2.5, 2.4, 2.4 };
        private static readonly int[] DaysInMonth = { 31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31 };
        private const int August = 7;
        private const int FirstOfAugust = 213;
        private const int RecordFirstYear = 1899, RecordLastYear = 2004;
        private const double StationElevationM = 85.0;

        /// <summary>
        /// The cool season's drying, restated from its source (Bureau of Meteorology and CSIRO, <i>State of the Climate 2024</i>):
        /// April to October's rain in the south-east has fallen by about nine per cent since 1994. No figure covers the warm
        /// months, which are left as the record has them.
        /// </summary>
        private const double CoolSeasonDrying = 0.09;
        private const int DryingSinceYear = 1994;
        private const int April = 3, October = 9;

        /// <summary>How far a modelled total may sit from the pre-human one: the contract's tenth.</summary>
        private const double TotalsTolerance = 0.10;

        /// <summary>
        /// The world seeds averaged over, since one seed is one draw of a climate and not the climate: a single world's August
        /// rain runs from a quarter of the month's mean to twice it. The rain's settings were fitted on other seeds.
        /// </summary>
        private static readonly ulong[] Seeds = SeedsFrom(1UL, 256);

        private static Climate Bherwerre => Climate.ForRegion(Region.Bherwerre);

        // ---- the air, against the lighthouse's months less the warming inside their years ----

        /// <summary>The station's monthly means of the day's highest and lowest readings, °C, 1907–2004.</summary>
        private static readonly double[] StationMeanMaxC = { 23.8, 23.9, 23.0, 20.7, 18.2, 15.9, 15.1, 16.1, 17.9, 19.8, 21.2, 22.9 };
        private static readonly double[] StationMeanMinC = { 17.5, 18.0, 17.1, 14.9, 12.4, 10.4, 9.2, 9.6, 11.2, 12.9, 14.5, 16.3 };
        private const int AirRecordFirstYear = 1907, AirRecordLastYear = 2004;

        /// <summary>The first and the ninth decile of August's daily minima and maxima, °C, 1946–2004: how far its nights and its days spread about their means.</summary>
        private const double AugustMinDecile1C = 7.0, AugustMinDecile9C = 12.2, AugustMaxDecile1C = 13.3, AugustMaxDecile9C = 19.4;
        private const int DecileRecordFirstYear = 1946, DecileRecordLastYear = 2004;

        /// <summary>
        /// The region's warming trend since national records began, °C over 1910 to 2023, and its published uncertainty,
        /// restated from their source (Bureau of Meteorology and CSIRO, <i>State of the Climate 2024</i>) rather than read from
        /// the climate; what of it lies inside a table is the trend's value at the middle of the table's years.
        /// </summary>
        private const double WarmingSinceRecordsC = 1.51, WarmingUncertaintyC = 0.23;
        private const int WarmingFromYear = 1910, WarmingToYear = 2023;
        private static double WarmingInside(int firstYear, int lastYear) => WarmingSinceRecordsC * (0.5 * (firstYear + lastYear) - WarmingFromYear) / (WarmingToYear - WarmingFromYear);
        private static readonly double AirWarmingC = WarmingInside(AirRecordFirstYear, AirRecordLastYear);

        // ---- the wind, the cloud, the moisture and the sun, against the lighthouse's 9 am and 3 pm readings and its satellite ----

        /// <summary>Mean wind speed at 9 am and at 3 pm by month, km/h at the anemometer's 10 m, 1957–2004.</summary>
        private static readonly double[] StationWind9amKmh = { 14.2, 14.0, 13.5, 15.0, 16.8, 19.1, 17.7, 17.1, 16.2, 15.2, 15.6, 14.8 };
        private static readonly double[] StationWind3pmKmh = { 20.5, 19.3, 18.3, 17.9, 17.9, 19.1, 19.4, 20.5, 21.3, 21.2, 22.5, 21.9 };
        /// <summary>The share of a 10 m wind that blows at 1.5 m over short grass: the logarithmic profile with a roughness of 3 cm, restated.</summary>
        private static readonly double BodyHeightWindShare = Math.Log(1.5 / 0.03) / Math.Log(10.0 / 0.03);
        /// <summary>Mean cloud at 9 am and at 3 pm by month, oktas (eighths of the sky), 1907–2004 and 1912–2004.</summary>
        private static readonly double[] StationCloud9amOktas = { 5.0, 5.0, 4.8, 4.4, 4.3, 4.3, 3.8, 3.7, 3.9, 4.4, 4.7, 4.9 };
        private static readonly double[] StationCloud3pmOktas = { 4.6, 4.5, 4.3, 4.2, 4.4, 4.2, 3.9, 3.8, 3.8, 4.3, 4.6, 4.5 };
        /// <summary>Mean relative humidity at 9 am and at 3 pm by month, per cent, 1907–2004 and 1909–2004.</summary>
        private static readonly double[] StationHumidity9am = { 76, 77, 77, 74, 75, 75, 74, 71, 69, 70, 72, 74 };
        private static readonly double[] StationHumidity3pm = { 71, 72, 71, 68, 67, 65, 63, 61, 64, 66, 69, 70 };
        /// <summary>Mean daily solar exposure by month, MJ/m², 1990–2026, the Bureau's satellite-derived figure for the site.</summary>
        private static readonly double[] StationSolarMJ = { 22.9, 20.2, 16.8, 13.2, 9.8, 8.1, 9.3, 12.6, 16.6, 20.1, 21.8, 23.5 };
        /// <summary>Mean temperature at 9 am and at 3 pm by month, °C, 1907–2004 and 1909–2004: the observers' readings of the day's shape.</summary>
        private static readonly double[] StationTemp9amC = { 20.6, 20.8, 19.7, 17.5, 14.8, 12.5, 11.4, 12.3, 14.4, 16.5, 18.0, 19.7 };
        private static readonly double[] StationTemp3pmC = { 22.3, 22.7, 21.7, 19.6, 17.2, 15.1, 14.3, 15.1, 16.6, 18.0, 19.5, 21.2 };

        /// <summary>
        /// The Bureau reads a day's extremes at 9 am, the lowest reading in the 24 hours to it and the highest in the 24 hours
        /// from it, and a station's months are means of those readings; the sky is read the same way, every half hour, for
        /// the first of the seeds. Half-hour readings put a day's sampled extremes within a few hundredths of a degree of the
        /// continuous ones; the ninth hour's readings begin at the eighteenth step. A month's mean range moves by about a
        /// twentieth of a degree from one draw of a few dozen seeds to the next, which carried February past the range's
        /// bound on one such draw (2026-09-13); four times as many seeds halve that.
        /// </summary>
        private const int ReadingsADay = 48, ObservationStep = 18, AirSeeds = 128;

        [Test]
        public void EveryMonthOfTheWeatherIsTheLighthousesLessTheRegionsWarming()
        {
            for (int m = 0; m < 12; m++)
            {
                MonthAir(m, out double meanMax, out double meanMin);
                double targetMax = StationMeanMaxC[m] - AirWarmingC;
                double targetMin = StationMeanMinC[m] - AirWarmingC;
                // One yearly wave for the day's mean leaves the worst months well under a degree from their own: no month may
                // stand further off than a degree.
                Assert.That(meanMax, Is.EqualTo(targetMax).Within(1.0),
                    "month " + (m + 1) + ": a mean maximum of " + meanMax.ToString("F2") + " °C against " + targetMax.ToString("F2"));
                Assert.That(meanMin, Is.EqualTo(targetMin).Within(1.0),
                    "month " + (m + 1) + ": a mean minimum of " + meanMin.ToString("F2") + " °C against " + targetMin.ToString("F2"));
            }
        }

        /// <summary>
        /// The month the founder wakes into, held tighter than the rest: the table less the warming inside its years (the
        /// trend's value at 1955, the middle of 1907–2004, 0.61 °C), within a third of a degree, which is what one yearly wave
        /// leaves August by. M1.8a took the whole 1.51 °C off a 1991–2004 table and had this night a degree colder.
        /// </summary>
        [Test]
        public void AugustIsThePreHumanLateWinterTheFounderWakesInto()
        {
            Assert.That(AirWarmingC, Is.EqualTo(0.608).Within(0.001), "the trend's value at the middle of 1907–2004");
            Assert.That(Bherwerre.WarmingInsideStationRecordC, Is.EqualTo(AirWarmingC).Within(1e-9), "and the climate takes that much off its table");
            MonthAir(August, out double meanMax, out double meanMin);
            double targetMax = StationMeanMaxC[August] - AirWarmingC;
            double targetMin = StationMeanMinC[August] - AirWarmingC;
            Assert.That(meanMax, Is.EqualTo(targetMax).Within(0.35),
                "August's mean maximum was " + meanMax.ToString("F2") + " °C against a pre-human " + targetMax.ToString("F2")
                + " (the lighthouse's " + StationMeanMaxC[August].ToString("F1") + " less " + AirWarmingC.ToString("F2") + ")");
            Assert.That(meanMin, Is.EqualTo(targetMin).Within(0.35),
                "and its mean minimum " + meanMin.ToString("F2") + " °C against " + targetMin.ToString("F2"));
            Assert.That(meanMin, Is.GreaterThan(7.0).And.LessThan(10.5), "a late-August night on this coast is milder than the highlands' by some way");
        }

        /// <summary>
        /// Warming lifts a level and leaves the day's shape, so each month's range answers to the lighthouse without the
        /// warming's excuse, to v1's 0.15 °C. A yearly wave for the range left August's half a degree narrow; the curve fitted
        /// to the lighthouse's extremes without the fronts' widening taken back out left the weather's a quarter of a degree wide.
        /// </summary>
        [Test]
        public void EveryMonthsDailyRangeIsTheLighthousesWhateverTheLevel()
        {
            for (int m = 0; m < 12; m++)
            {
                MonthAir(m, out double meanMax, out double meanMin);
                double station = StationMeanMaxC[m] - StationMeanMinC[m];
                Assert.That(meanMax - meanMin, Is.EqualTo(station).Within(0.15),
                    "month " + (m + 1) + ": a daily range of " + (meanMax - meanMin).ToString("F2") + " °C against the lighthouse's " + station.ToString("F1"));
            }
        }

        // ---- the rain's totals, against the record with the drying undone ----

        /// <summary>
        /// The derivation, asserted rather than assumed: the lighthouse's record with the drying undone is the world's rain.
        /// v1's station window lay wholly inside the drying; eleven of the lighthouse's fourteen years do, and three before.
        /// </summary>
        [Test]
        public void ThePreHumanRainIsTheLighthousesWithTheDryingUndone()
        {
            double inside = (RecordLastYear - DryingSinceYear + 1) / (double)(RecordLastYear - RecordFirstYear + 1);
            Assert.That(inside, Is.EqualTo(11.0 / 106.0).Within(1e-12), "eleven of the record's hundred and six years lie inside the drying: a hundredth of the cool season's rain to undo");
            Assert.That(August, Is.InRange(April, October), "August lies inside the cool season the drying covers");

            double augustTarget = PreHumanRainMm(August);
            Assert.That(augustTarget * (1.0 - CoolSeasonDrying * inside), Is.EqualTo(StationRainMm[August]).Within(1e-9),
                "drying the pre-human August by what the record's years saw must give back the lighthouse's own August");
            Assert.That(PreHumanRainMm(0), Is.EqualTo(StationRainMm[0]), "and a warm month, which no figure covers, is the record's");

            double modelled = MeanOverSeeds(s => RainMm(s, FirstDayOf(August), DaysInMonth[August]));
            Assert.That(modelled, Is.EqualTo(augustTarget).Within(augustTarget * TotalsTolerance),
                "a modelled August gave " + modelled.ToString("F1") + " mm against the pre-human " + augustTarget.ToString("F1")
                + " (the lighthouse's " + StationRainMm[August].ToString("F1") + " undried)");
        }

        [Test]
        public void AModelledYearGivesThePreHumanYearsRain()
        {
            double record = 0.0, target = 0.0;
            for (int m = 0; m < 12; m++)
            {
                record += StationRainMm[m];
                target += PreHumanRainMm(m);
            }
            Assert.That(target, Is.GreaterThan(record).And.LessThan(record * (1.0 + CoolSeasonDrying)),
                "the pre-human year is wetter than the record, by less than the whole drying, which covers only part of the year");

            double modelled = MeanOverSeeds(s => RainMm(s, 1, 365));
            Assert.That(modelled, Is.EqualTo(target).Within(target * TotalsTolerance),
                "a modelled year gave " + modelled.ToString("F1") + " mm against the pre-human " + target.ToString("F1"));
        }

        /// <summary>
        /// The station's days of rain at the thresholds its own table states (M1.8c): 1 mm or more, and any rain, which the
        /// Bureau counts from 0.2 mm; for the year and for August, the month the founder wakes into, each within a tenth.
        /// M1.8a's reproduction stated no threshold, and its August had 6.0 days where the long record has 8.3: a short
        /// record's noise, which the model had bent its season to.
        /// </summary>
        [Test]
        public void TheRainDaysAtAMillimetreAndAtAnyRainAreTheLighthouses()
        {
            double year1 = 0.0, yearAny = 0.0;
            for (int m = 0; m < 12; m++)
            {
                year1 += StationRainDays[m];
                yearAny += StationRainDaysAny[m];
            }
            double model1 = MeanOverSeeds(s => RainDays(s, 1, 365, 1.0));
            double modelAny = MeanOverSeeds(s => RainDays(s, 1, 365, 0.2));
            Assert.That(model1, Is.EqualTo(year1).Within(year1 * TotalsTolerance),
                "days of 1 mm or more in a year: the model's " + model1.ToString("F1") + " against the lighthouse's " + year1.ToString("F1"));
            Assert.That(modelAny, Is.EqualTo(yearAny).Within(yearAny * TotalsTolerance),
                "days of any rain in a year: " + modelAny.ToString("F1") + " against " + yearAny.ToString("F1"));

            double august1 = MeanOverSeeds(s => RainDays(s, FirstOfAugust, DaysInMonth[August], 1.0));
            double augustAny = MeanOverSeeds(s => RainDays(s, FirstOfAugust, DaysInMonth[August], 0.2));
            Assert.That(august1, Is.EqualTo(StationRainDays[August]).Within(StationRainDays[August] * TotalsTolerance),
                "August's days of 1 mm or more: " + august1.ToString("F2") + " against " + StationRainDays[August].ToString("F1"));
            Assert.That(augustAny, Is.EqualTo(StationRainDaysAny[August]).Within(StationRainDaysAny[August] * TotalsTolerance),
                "and of any rain: " + augustAny.ToString("F2") + " against " + StationRainDaysAny[August].ToString("F1"));
        }

        /// <summary>
        /// The heavy days: the table counts each month's days of 10 mm or more (M1.8c), which say how a month's rain is shared
        /// out among its days, a thing its total cannot. No month a day off, and the year within a fifth.
        /// </summary>
        [Test]
        public void TheHeavyDaysAreTheLighthousesToADay()
        {
            double year = 0.0, model = 0.0;
            for (int m = 0; m < 12; m++)
            {
                int month = m;
                double days = MeanOverSeeds(s => RainDays(s, FirstDayOf(month), DaysInMonth[month], 10.0));
                year += StationRainDays10[m];
                model += days;
                Assert.That(days, Is.EqualTo(StationRainDays10[m]).Within(1.0),
                    "month " + (m + 1) + ": " + days.ToString("F2") + " days of 10 mm or more against the lighthouse's " + StationRainDays10[m].ToString("F1"));
            }
            Assert.That(model, Is.EqualTo(year).Within(0.2 * year), "the year's heavy days: " + model.ToString("F1") + " against " + year.ToString("F1"));
        }

        // ---- the wind, the cloud, the moisture and the sun, read as the observers read them (M1.8c) ----

        /// <summary>
        /// The wind in the open at a founder's height at nine in the morning and at three in the afternoon, month by month,
        /// is the lighthouse's 10 m reading brought down by the profile over grass, within a twelfth: the fronts' factor
        /// averages a hundredth over one, and the months are eased straight between their middles.
        /// </summary>
        [Test]
        public void TheWindIsTheLighthousesAtNineAndAtThree()
        {
            for (int m = 0; m < 12; m++)
            {
                double at9 = MonthlyAt(m, 9.0, sky => sky.WindMs), at15 = MonthlyAt(m, 15.0, sky => sky.WindMs);
                double record9 = StationWind9amKmh[m] / 3.6 * BodyHeightWindShare, record15 = StationWind3pmKmh[m] / 3.6 * BodyHeightWindShare;
                Assert.That(at9, Is.EqualTo(record9).Within(record9 / 12.0),
                    "month " + (m + 1) + " at nine: " + at9.ToString("F2") + " m/s against " + record9.ToString("F2") + " (the lighthouse's " + StationWind9amKmh[m].ToString("F1") + " km/h at 10 m)");
                Assert.That(at15, Is.EqualTo(record15).Within(record15 / 12.0),
                    "month " + (m + 1) + " at three: " + at15.ToString("F2") + " m/s against " + record15.ToString("F2") + " (" + StationWind3pmKmh[m].ToString("F1") + " km/h at 10 m)");
                Assert.That(at15, Is.GreaterThanOrEqualTo(at9 * 0.95), "the afternoon blows at least as hard as the morning, as every month of the record does");
            }
        }

        /// <summary>
        /// The air at nine and at three against the observers' readings, less the warming inside their years. The day's curve
        /// runs about a degree warm at both hours in every month: the record's mean daily maximum is the average of each day's
        /// own, which sunny days lift and cloudy days lower, so the mean cycle's afternoon sits a degree under the mean
        /// maximum where the model's, with no cloud in its daily range, sits close to it (DEBTS, "the days spread less than
        /// the record's"). Held to two degrees so the shape cannot drift further while that is owed, and the bias is printed.
        /// </summary>
        [Test]
        public void TheAirAtNineAndAtThreeRunsNearTheObserversReadings()
        {
            double worst = 0.0;
            for (int m = 0; m < 12; m++)
            {
                double at9 = MonthlyAt(m, 9.0, sky => sky.AirC), at15 = MonthlyAt(m, 15.0, sky => sky.AirC);
                double record9 = StationTemp9amC[m] - AirWarmingC, record15 = StationTemp3pmC[m] - AirWarmingC;
                worst = Math.Max(worst, Math.Max(Math.Abs(at9 - record9), Math.Abs(at15 - record15)));
                Assert.That(at9, Is.EqualTo(record9).Within(2.0), "month " + (m + 1) + " at nine: " + at9.ToString("F2") + " °C against the observers' " + record9.ToString("F2"));
                Assert.That(at15, Is.EqualTo(record15).Within(2.0), "month " + (m + 1) + " at three: " + at15.ToString("F2") + " °C against " + record15.ToString("F2"));
            }
            TestContext.Out.WriteLine(TestContext.CurrentContext.Test.Name + ": the day's curve is at worst " + worst.ToString("F2") + " °C from the observers' readings at nine and at three");
        }

        /// <summary>The cloud at nine and at three, month by month, is the lighthouse's in oktas within half an okta.</summary>
        [Test]
        public void TheCloudIsTheLighthousesOktasAtNineAndAtThree()
        {
            for (int m = 0; m < 12; m++)
            {
                double at9 = MonthlyAt(m, 9.0, sky => sky.CloudCover01), at15 = MonthlyAt(m, 15.0, sky => sky.CloudCover01);
                Assert.That(at9 * 8.0, Is.EqualTo(StationCloud9amOktas[m]).Within(0.5),
                    "month " + (m + 1) + " at nine: " + (at9 * 8.0).ToString("F2") + " oktas against the lighthouse's " + StationCloud9amOktas[m].ToString("F1"));
                Assert.That(at15 * 8.0, Is.EqualTo(StationCloud3pmOktas[m]).Within(0.5),
                    "month " + (m + 1) + " at three: " + (at15 * 8.0).ToString("F2") + " oktas against " + StationCloud3pmOktas[m].ToString("F1"));
            }
        }

        /// <summary>
        /// The humidity at nine and at three, month by month, is the lighthouse's within eight points: damp mornings near the
        /// night's minimum and drier afternoons with the same moisture in the air, which one dew point a month and the day's
        /// temperature give, where one level off the index could not. The model runs three to seven points dry, worst in
        /// November's afternoon, because its day's curve is a degree warm at those hours (DEBTS, "the days spread less than
        /// the record's"): the dew points are the record's, and the humidity will close when the day's shape does.
        /// </summary>
        [Test]
        public void TheHumidityIsTheLighthousesAtNineAndAtThree()
        {
            for (int m = 0; m < 12; m++)
            {
                double at9 = MonthlyAt(m, 9.0, sky => sky.RelativeHumidity01), at15 = MonthlyAt(m, 15.0, sky => sky.RelativeHumidity01);
                Assert.That(at9 * 100.0, Is.EqualTo(StationHumidity9am[m]).Within(8.0),
                    "month " + (m + 1) + " at nine: " + (at9 * 100.0).ToString("F1") + "% against the lighthouse's " + StationHumidity9am[m].ToString("F0"));
                Assert.That(at15 * 100.0, Is.EqualTo(StationHumidity3pm[m]).Within(8.0),
                    "month " + (m + 1) + " at three: " + (at15 * 100.0).ToString("F1") + "% against " + StationHumidity3pm[m].ToString("F0"));
                Assert.That(at9, Is.GreaterThan(at15), "the morning is damper than the afternoon, as every month of the record is");
            }
        }

        /// <summary>
        /// The sun's energy on the ground over a month's days, the Meinel beam on the horizontal and the diffuse sky under the
        /// weather's own cloud, integrated every half hour, is the Bureau's satellite-derived daily exposure for the site
        /// within an eighth: the check on the term that gets a naked founder through a winter day (FP.2), and on the cloud.
        /// </summary>
        [Test]
        public void TheSunOverAMonthGivesTheSatellitesDailyExposure()
        {
            const int SunSeeds = 24, Steps = 48;
            Climate climate = Bherwerre;
            for (int m = 0; m < 12; m++)
            {
                double joules = 0.0;
                int first = FirstDayOf(m);
                for (int s = 0; s < SunSeeds; s++)
                {
                    Synoptic synoptic = new Synoptic(Seeds[s]);
                    for (int day = first; day < first + DaysInMonth[m]; day++)
                        for (int step = 0; step < Steps; step++)
                        {
                            SolarClock sun = Sun(day, (step + 0.5) * 24.0 / Steps);
                            double elevation = sun.SolarElevationDeg;
                            if (elevation <= 0.0) continue;
                            double cloud = Weather.At(climate, synoptic, sun, StationElevationM, 1.0).CloudCover01;
                            double onTheGround = Climate.DirectSolarWm2(elevation, cloud) * Math.Sin(elevation * Math.PI / 180.0) + Climate.DiffuseSolarWm2(elevation, cloud);
                            joules += onTheGround * 86400.0 / Steps;
                        }
                }
                double megajoulesADay = joules / (SunSeeds * DaysInMonth[m]) / 1e6;
                Assert.That(megajoulesADay, Is.EqualTo(StationSolarMJ[m]).Within(StationSolarMJ[m] / 8.0),
                    "month " + (m + 1) + ": " + megajoulesADay.ToString("F1") + " MJ/m² a day against the satellite's " + StationSolarMJ[m].ToString("F1"));
            }
        }

        /// <summary>
        /// August's nights spread as the lighthouse's do (M1.8c): the ninth decile of the daily minima over the first within
        /// six tenths of the record's 5.2 °C, which is what the cold snap's depth is set by, and each decile at the record's
        /// level less the warming inside 1946–2004 within a degree. The days' spread the record has wider (6.1) than the
        /// nights', which a model with no cloud in its daily range cannot give (DEBTS), so only the nights' is held.
        /// </summary>
        [Test]
        public void AugustsNightsSpreadAsTheLighthousesDecilesSay()
        {
            System.Collections.Generic.List<double> minima = new System.Collections.Generic.List<double>();
            int first = FirstOfAugust - 1;
            for (int s = 0; s < AirSeeds; s++)
                for (int day = first; day < first + DaysInMonth[August]; day++)
                {
                    double low = AirExtremes.Value[s, day, 1];
                    if (!double.IsNaN(low)) minima.Add(low);
                }
            minima.Sort();
            double decile1 = minima[(int)Math.Round(0.1 * (minima.Count - 1))], decile9 = minima[(int)Math.Round(0.9 * (minima.Count - 1))];
            double warming = WarmingInside(DecileRecordFirstYear, DecileRecordLastYear);
            string story = "August's minima: the first decile " + decile1.ToString("F2") + " and the ninth " + decile9.ToString("F2") + " °C over " + minima.Count + " nights";
            Assert.That(decile9 - decile1, Is.EqualTo(AugustMinDecile9C - AugustMinDecile1C).Within(0.6), story + "; the record's spread is " + (AugustMinDecile9C - AugustMinDecile1C).ToString("F1"));
            Assert.That(decile1, Is.EqualTo(AugustMinDecile1C - warming).Within(1.0), story + "; the record's first decile less its years' warming is " + (AugustMinDecile1C - warming).ToString("F2"));
            Assert.That(decile9, Is.EqualTo(AugustMinDecile9C - warming).Within(1.0), story + "; the record's ninth less its years' warming is " + (AugustMinDecile9C - warming).ToString("F2"));
        }

        /// <summary>
        /// The lighthouse's cool half of the year, March to August, rains more than the warm half, and harder on each rain day,
        /// and so must the model: a year with no season in it gives about one for both. This is what the season's pull on
        /// how hard it rains is for; without it the rain per rain day comes out alike in both halves.
        /// </summary>
        [Test]
        public void TheCoolHalfOfTheYearRainsMoreAndHarderAsTheLighthouseDoes()
        {
            int[] cool = { 2, 3, 4, 5, 6, 7 }, warm = { 8, 9, 10, 11, 0, 1 };
            double coolRain = 0.0, warmRain = 0.0, coolDays = 0.0, warmDays = 0.0, modelCoolRain = 0.0, modelWarmRain = 0.0, modelCoolDays = 0.0, modelWarmDays = 0.0;
            for (int i = 0; i < 6; i++)
            {
                coolRain += PreHumanRainMm(cool[i]);
                warmRain += PreHumanRainMm(warm[i]);
                coolDays += StationRainDays[cool[i]];
                warmDays += StationRainDays[warm[i]];
                int c = cool[i], w = warm[i];
                modelCoolRain += MeanOverSeeds(s => RainMm(s, FirstDayOf(c), DaysInMonth[c]));
                modelWarmRain += MeanOverSeeds(s => RainMm(s, FirstDayOf(w), DaysInMonth[w]));
                modelCoolDays += MeanOverSeeds(s => RainDays(s, FirstDayOf(c), DaysInMonth[c], 1.0));
                modelWarmDays += MeanOverSeeds(s => RainDays(s, FirstDayOf(w), DaysInMonth[w], 1.0));
            }
            double more = coolRain / warmRain, modelMore = modelCoolRain / modelWarmRain;
            Assert.That(modelMore, Is.EqualTo(more).Within(more * TotalsTolerance),
                "March to August over September to February: the model's " + modelMore.ToString("F3") + " against the pre-human record's " + more.ToString("F3"));
            double harder = (coolRain / coolDays) / (warmRain / warmDays), modelHarder = (modelCoolRain / modelCoolDays) / (modelWarmRain / modelWarmDays);
            Assert.That(modelHarder, Is.EqualTo(harder).Within(harder * TotalsTolerance),
                "and the rain on each rain day at 1 mm, the cool half's over the warm: " + modelHarder.ToString("F3") + " against " + harder.ToString("F3"));
        }

        // ---- what a total cannot see ----

        /// <summary>
        /// Rain arrives in fronts, with dry spells to build a camp in. A model that drizzled a little every day of August would
        /// hit the month's total and be wrong about every day in it. v1 held two to twelve wet spells in each of its eight
        /// Augusts; over many worlds a dry August comes round, as it does on this coast, so the drizzle and the stuck front
        /// are held in every August and the fronts coming and going in most.
        /// </summary>
        [Test]
        public void RainArrivesInFrontsWithDrySpellsBetween()
        {
            int twoOrMore = 0;
            for (int s = 0; s < Seeds.Length; s++)
            {
                int spells = 0, wetRun = 0, dryRun = 0, longestWet = 0, longestDry = 0;
                bool wasWet = false;
                for (int day = FirstOfAugust; day < FirstOfAugust + DaysInMonth[August]; day++)
                {
                    bool wet = RainMm(s, day, 1) >= 1.0;
                    if (wet && !wasWet) spells++;
                    wetRun = wet ? wetRun + 1 : 0;
                    dryRun = wet ? 0 : dryRun + 1;
                    longestWet = Math.Max(longestWet, wetRun);
                    longestDry = Math.Max(longestDry, dryRun);
                    wasWet = wet;
                }
                string seed = "seed " + Seeds[s] + "'s August";
                Assert.That(spells, Is.AtMost(12), seed + " had " + spells + " wet spells; thirty short ones is drizzle");
                Assert.That(longestWet, Is.LessThan(10), seed + " rained for " + longestWet + " days on end; a front passes");
                Assert.That(longestDry, Is.AtLeast(3), seed + "'s longest dry run was " + longestDry + " days, and a founder must be able to build a camp in one");
                if (spells >= 2) twoOrMore++;
            }
            Assert.That(twoOrMore, Is.AtLeast(Seeds.Length * 3 / 4),
                twoOrMore + " of " + Seeds.Length + " Augusts had fronts come and go twice or more; most Augusts must");
        }

        /// <summary>
        /// A wet day is showers with breaks in them, not one long drizzle: v1's frontal band alone gave a wet August day
        /// eighteen hours of continuous gentle rain, and the shower band is what cured it.
        /// </summary>
        [Test]
        public void RainOnAWetDayComesInShowersNotDrizzle()
        {
            double rainingHours = 0.0;
            int wetDays = 0, broken = 0;
            for (int s = 0; s < Seeds.Length; s++)
            {
                Synoptic synoptic = new Synoptic(Seeds[s]);
                for (int day = FirstOfAugust; day < FirstOfAugust + DaysInMonth[August]; day++)
                {
                    if (RainMm(s, day, 1) < 1.0) continue;
                    wetDays++;
                    int bursts = 0;
                    bool wasRaining = false;
                    for (int hour = 0; hour < 24; hour++)
                    {
                        bool raining = synoptic.RainRateMmPerHour(day - 1 + hour / 24.0) > 0.0;
                        if (raining) rainingHours++;
                        if (raining && !wasRaining) bursts++;
                        wasRaining = raining;
                    }
                    if (bursts >= 2) broken++;
                }
            }
            Assert.That(wetDays, Is.GreaterThan(100), "there must be wet days to measure the shape of");
            double mean = rainingHours / wetDays;
            Assert.That(mean, Is.InRange(2.0, 14.0),
                "a wet August day rained for " + mean.ToString("F1") + " hours on average: less than two is a passing shower, not a rain day, and more than fourteen is drizzle wearing a front's clothes");
            // The hours alone no longer catch the defect: at the lighthouse's threshold the frontal band without its showers
            // rains about eleven hours on a wet August day, inside v1's bound, but always in one unbroken spell (measured
            // 2026-09-13). With the showers a wet day's rain stops and comes again on about four days in ten.
            Assert.That(broken / (double)wetDays, Is.GreaterThan(0.25),
                "rain stopped and came again on " + (100.0 * broken / wetDays).ToString("F0") + "% of wet August days; frontal rain arrives in bands with breaks between, and a day of one unbroken rain is drizzle's shape");
        }

        [Test]
        public void MostHoursAreDry()
        {
            int dry = 0, hours = 0;
            for (int s = 0; s < 16; s++)
            {
                Synoptic synoptic = new Synoptic(Seeds[s]);
                for (int day = FirstOfAugust; day < FirstOfAugust + DaysInMonth[August]; day++)
                    for (int hour = 0; hour < 24; hour++, hours++)
                        if (synoptic.RainRateMmPerHour(day - 1 + hour / 24.0) <= 0.0) dry++;
            }
            Assert.That(dry / (double)hours, Is.GreaterThan(0.75), "only " + (100.0 * dry / hours).ToString("F0") + "% of August's hours were dry");
        }

        // ---- the assembly ----

        /// <summary>
        /// The sky is worked out once and its readings agree: the front's cold snap is in the air and not beside it, and it
        /// cannot rain out of a clear sky or into dry air.
        /// </summary>
        [Test]
        public void TheSkyIsAssembledOnceAndItsReadingsAgree()
        {
            Climate climate = Bherwerre;
            int raining = 0, dry = 0;
            for (int s = 0; s < 8; s++)
            {
                Synoptic synoptic = new Synoptic(Seeds[s]);
                for (int day = FirstOfAugust; day < FirstOfAugust + DaysInMonth[August]; day++)
                    for (int hour = 0; hour < 24; hour++)
                    {
                        Weather sky = Weather.At(climate, synoptic, Sun(day, hour), StationElevationM, 1.0);
                        Assert.That(sky.AirC - sky.AnomalyC, Is.EqualTo(climate.AirTemperatureC(day, hour, StationElevationM)).Within(1e-9),
                            "taking the front's cold snap back out of the air must leave the climate's curve");
                        Assert.That(sky.RelativeHumidity01, Is.InRange(0.0, 1.0));
                        Assert.That(sky.CloudCover01, Is.InRange(0.0, 1.0));
                        Assert.That(sky.WindMs, Is.AtLeast(0.0));
                        if (sky.IsRaining)
                        {
                            raining++;
                            Assert.That(sky.CloudCover01, Is.GreaterThan(0.5), "it rained under " + sky.CloudCover01.ToString("F2") + " cloud on day " + day + " at " + hour + ":00");
                            Assert.That(sky.RelativeHumidity01, Is.GreaterThan(0.85), "and the air must be near saturated while it rains");
                        }
                        else
                        {
                            dry++;
                            Assert.That(sky.RainRateMmPerHour, Is.EqualTo(0.0));
                        }
                    }
            }
            Assert.That(raining, Is.GreaterThan(0), "August must hold some rain to check");
            Assert.That(dry, Is.GreaterThan(raining), "and most of it must be dry");
        }

        /// <summary>A front brings cold and wind with its rain, because one index drives all three: a warm, still downpour would be four models wearing one name.</summary>
        [Test]
        public void AFrontBringsItsColdAndItsWindWithIt()
        {
            Climate climate = Bherwerre;
            double wetAnomaly = 0.0, dryAnomaly = 0.0, wetWind = 0.0, dryWind = 0.0;
            int wet = 0, dry = 0;
            for (int s = 0; s < 4; s++)
            {
                Synoptic synoptic = new Synoptic(Seeds[s]);
                for (int day = 1; day <= 365; day++)
                    for (int hour = 0; hour < 24; hour += 2)
                    {
                        Weather sky = Weather.At(climate, synoptic, Sun(day, hour), StationElevationM, 1.0);
                        if (sky.IsRaining) { wetAnomaly += sky.AnomalyC; wetWind += sky.WindMs; wet++; }
                        else { dryAnomaly += sky.AnomalyC; dryWind += sky.WindMs; dry++; }
                    }
            }
            Assert.That(wet, Is.GreaterThan(50), "there must be enough wet hours to average");
            Assert.That(wetAnomaly / wet, Is.LessThan(dryAnomaly / dry),
                "wet hours must run colder than dry ones: " + (wetAnomaly / wet).ToString("F2") + " against " + (dryAnomaly / dry).ToString("F2") + " °C");
            Assert.That(wetWind / wet, Is.GreaterThan(dryWind / dry),
                "and windier: " + (wetWind / wet).ToString("F2") + " against " + (dryWind / dry).ToString("F2") + " m/s");
        }

        /// <summary>
        /// A cold snap is an air mass, so it turns around over days and not hours. v1's anomaly read the whole index, showers
        /// and all, and swung four degrees within six hours, putting a late-August afternoon at 16.4 °C; no test of the
        /// climate's curve could see it, since none reads the anomaly. The measure is reversals, not swing: a southerly
        /// change can drop the air several degrees in a day, but an air mass does not change its mind hourly.
        /// </summary>
        [Test]
        public void AColdSnapIsAnAirMassAndMovesOverDaysNotHours()
        {
            Climate climate = Bherwerre;
            Synoptic synoptic = new Synoptic(2026UL);
            double reversals = 0.0, wholeIndexReversals = 0.0, warmest = double.MinValue, coldest = double.MaxValue;
            int days = 0;
            for (int day = FirstOfAugust; day < FirstOfAugust + DaysInMonth[August]; day++, days++)
            {
                int d = day;
                reversals += Reversals(hour => synoptic.AirAnomalyC(d - 1 + hour / 24.0));
                wholeIndexReversals += Reversals(hour => -Synoptic.ColdSnapDepthC * synoptic.Index(d - 1 + hour / 24.0));
                for (int hour = 0; hour < 24; hour++)
                {
                    double anomaly = Weather.At(climate, synoptic, Sun(day, hour), StationElevationM, 1.0).AnomalyC;
                    warmest = Math.Max(warmest, anomaly);
                    coldest = Math.Min(coldest, anomaly);
                }
            }
            reversals /= days;
            wholeIndexReversals /= days;
            Assert.That(reversals, Is.LessThan(1.5), "the cold snap turned around " + reversals.ToString("F2") + " times a day on average");
            Assert.That(wholeIndexReversals, Is.GreaterThan(1.8 * reversals),
                "and reading the whole index would turn it " + wholeIndexReversals.ToString("F2") + " times a day: if those are alike, the showers are in the air");
            Assert.That(warmest - coldest, Is.GreaterThan(3.0), "across August the air must have warm spells and cold snaps, not a flat month");
            Assert.That(Math.Max(Math.Abs(warmest), Math.Abs(coldest)), Is.LessThan(2.2 * Synoptic.ColdSnapDepthC),
                "an hour sat " + Math.Max(Math.Abs(warmest), Math.Abs(coldest)).ToString("F1") + " °C off the climate's curve; departures that large are not weather");
        }

        /// <summary>
        /// The seasons are the climate's, and the weather holds no second opinion about them: the season shifts how often and
        /// how hard it rains, and v1, letting that shift into the air, damped the year's warming and cooling by 0.7 °C.
        /// </summary>
        [Test]
        public void TheRainsSeasonDoesNotMoveTheTemperature()
        {
            double summer = 0.0, winter = 0.0;
            int samples = 0;
            for (int s = 0; s < 64; s++)
            {
                Synoptic synoptic = new Synoptic(Seeds[s]);
                for (int day = 0; day < 30; day++)
                    for (int hour = 0; hour < 24; hour += 6, samples++)
                    {
                        summer += synoptic.AirAnomalyC(14 + day + hour / 24.0);
                        winter += synoptic.AirAnomalyC(196 + day + hour / 24.0);
                    }
            }
            summer /= samples;
            winter /= samples;
            Assert.That(summer - winter, Is.EqualTo(0.0).Within(1.0),
                "averaged over many worlds, midsummer's cold snap (" + summer.ToString("F2") + " °C) and midwinter's (" + winter.ToString("F2") + ") must not differ by season");
        }

        // ---- a function of the seed and the time ----

        /// <summary>
        /// The same seed gives the same weather at the same instant, asked in any order, and another seed another weather: a
        /// function of time and not a state stepped forward, which a warning before dark depends on.
        /// </summary>
        [Test]
        public void TheWeatherIsAFunctionOfTimeAndOfTheSeed()
        {
            Synoptic a = new Synoptic(7UL), b = new Synoptic(7UL), other = new Synoptic(8UL);
            for (double day = 200.0; day < 205.0; day += 0.37)
                Assert.That(b.Index(day), Is.EqualTo(a.Index(day)), "same seed, same instant, the same weather bit for bit");
            for (double day = 204.9; day > 200.0; day -= 0.37)
                Assert.That(a.Index(day), Is.EqualTo(b.Index(day)), "and asked in the other order");

            Weather first = Weather.At(Bherwerre, a, Sun(237, 17.0), 20.0, 0.6);
            Weather again = Weather.At(Climate.ForRegion(Region.Bherwerre), new Synoptic(7UL), Sun(237, 17.0), 20.0, 0.6);
            Assert.That(again.AirC, Is.EqualTo(first.AirC));
            Assert.That(again.RainRateMmPerHour, Is.EqualTo(first.RainRateMmPerHour));
            Assert.That(again.WindMs, Is.EqualTo(first.WindMs));
            Assert.That(again.CloudCover01, Is.EqualTo(first.CloudCover01));

            bool differs = false;
            for (double day = 200.0; day < 210.0 && !differs; day += 0.5)
                differs = Math.Abs(other.Index(day) - a.Index(day)) > 1e-9;
            Assert.That(differs, Is.True, "another seed must give another weather");
        }

        /// <summary>
        /// The weather runs on across the new year and the years do not repeat. v1 read the index at the day of the year, so at
        /// every new year's midnight the weather jumped to another front and every year was the one before.
        /// </summary>
        [Test]
        public void TheNewYearIsNoSeamAndNoYearRepeatsTheLast()
        {
            Synoptic synoptic = new Synoptic(2026UL);
            // A second either side: the showers move the index by at most a few thousandths in two seconds, where v1's seam
            // jumped it to another front, about a whole unit.
            const double Second = 1.0 / 86400.0;
            Assert.That(Math.Abs(synoptic.EffectiveIndex(365.0 + Second) - synoptic.EffectiveIndex(365.0 - Second)), Is.LessThan(0.01),
                "a second either side of the new year's midnight the weather is the same weather");
            Assert.That(Synoptic.SeasonDay(365.0 + Second), Is.EqualTo(1.0 + Second).Within(1e-9), "and the season has begun again");
            int alike = 0;
            for (int day = 0; day < 365; day++)
                if (Math.Abs(synoptic.Index(day + 0.5) - synoptic.Index(365.0 + day + 0.5)) < 1e-6) alike++;
            Assert.That(alike, Is.LessThan(5), "the second year is not the first again");
        }

        // ---- machinery ----

        /// <summary>
        /// For each of the first seeds and each day of the year, the sky's highest reading from 9 am and its lowest to 9 am;
        /// NaN where the year's ends cut a window short. Worked out once for the tests that read it.
        /// </summary>
        private static readonly Lazy<double[,,]> AirExtremes = new Lazy<double[,,]>(() =>
        {
            Climate climate = Climate.ForRegion(Region.Bherwerre);
            double offsetHours = WorldClock.OffsetHours(Region.Bherwerre.CentreLongitudeDeg);
            double[,,] extremes = new double[AirSeeds, 365, 2];
            for (int s = 0; s < AirSeeds; s++)
            {
                for (int day = 0; day < 365; day++)
                {
                    extremes[s, day, 0] = day < 364 ? double.MinValue : double.NaN;
                    extremes[s, day, 1] = day > 0 ? double.MaxValue : double.NaN;
                }
                Synoptic synoptic = new Synoptic(Seeds[s]);
                for (int step = ObservationStep; step < 365 * ReadingsADay; step++)
                {
                    WorldClock clock = new WorldClock((step + 0.5) * 24.0 / ReadingsADay - offsetHours);
                    double air = Weather.At(climate, synoptic, SolarClock.ForRegion(Region.Bherwerre, clock), StationElevationM, 1.0).AirC;
                    int from = (step - ObservationStep) / ReadingsADay;   // the 9 am this reading follows
                    if (from < 364) extremes[s, from, 0] = Math.Max(extremes[s, from, 0], air);
                    if (from + 1 < 365) extremes[s, from + 1, 1] = Math.Min(extremes[s, from + 1, 1], air);
                }
            }
            return extremes;
        });

        /// <summary>The mean over some seeds and a month's days of a reading of the sky at a local solar hour, at the station's height in the open.</summary>
        private static double MonthlyAt(int month, double hour, Func<Weather, double> read, int seeds = 64)
        {
            Climate climate = Bherwerre;
            double sum = 0.0;
            int count = 0, first = FirstDayOf(month);
            for (int s = 0; s < seeds; s++)
            {
                Synoptic synoptic = new Synoptic(Seeds[s]);
                for (int day = first; day < first + DaysInMonth[month]; day++)
                {
                    sum += read(Weather.At(climate, synoptic, Sun(day, hour), StationElevationM, 1.0));
                    count++;
                }
            }
            return sum / count;
        }

        /// <summary>The mean over the first seeds and a month's days of the sky's highest reading from 9 am and its lowest to 9 am, °C.</summary>
        private static void MonthAir(int month, out double meanMax, out double meanMin)
        {
            double maxSum = 0.0, minSum = 0.0;
            int maxCount = 0, minCount = 0;
            int first = FirstDayOf(month) - 1;
            for (int s = 0; s < AirSeeds; s++)
                for (int day = first; day < first + DaysInMonth[month]; day++)
                {
                    double high = AirExtremes.Value[s, day, 0], low = AirExtremes.Value[s, day, 1];
                    if (!double.IsNaN(high)) { maxSum += high; maxCount++; }
                    if (!double.IsNaN(low)) { minSum += low; minCount++; }
                }
            meanMax = maxSum / maxCount;
            meanMin = minSum / minCount;
        }

        /// <summary>Each seed's rain on each day of the year, mm, from a reading at the start of each hour: worked out once for the tests that read it.</summary>
        private static readonly Lazy<double[,]> DailyRainMm = new Lazy<double[,]>(() =>
        {
            double[,] daily = new double[Seeds.Length, 365];
            for (int s = 0; s < Seeds.Length; s++)
            {
                Synoptic synoptic = new Synoptic(Seeds[s]);
                for (int day = 0; day < 365; day++)
                    for (int hour = 0; hour < 24; hour++)
                        daily[s, day] += synoptic.RainRateMmPerHour(day + hour / 24.0);
            }
            return daily;
        });

        /// <summary>A month's rain before the drying: the record's own, undried in the cool season for the share of its years the drying covers.</summary>
        private static double PreHumanRainMm(int month)
        {
            if (month < April || month > October) return StationRainMm[month];
            double inside = (RecordLastYear - DryingSinceYear + 1) / (double)(RecordLastYear - RecordFirstYear + 1);
            return StationRainMm[month] / (1.0 - CoolSeasonDrying * inside);
        }

        private static ulong[] SeedsFrom(ulong first, int count)
        {
            ulong[] seeds = new ulong[count];
            for (int i = 0; i < count; i++) seeds[i] = first + (ulong)i;
            return seeds;
        }

        private static int FirstDayOf(int month)
        {
            int first = 1;
            for (int m = 0; m < month; m++) first += DaysInMonth[m];
            return first;
        }

        private static double RainMm(int seed, int firstDay, int days)
        {
            double mm = 0.0;
            for (int day = firstDay; day < firstDay + days; day++) mm += DailyRainMm.Value[seed, day - 1];
            return mm;
        }

        private static double RainDays(int seed, int firstDay, int days, double thresholdMm)
        {
            int count = 0;
            for (int day = firstDay; day < firstDay + days; day++)
                if (DailyRainMm.Value[seed, day - 1] >= thresholdMm) count++;
            return count;
        }

        private static double MeanOverSeeds(Func<int, double> perSeed)
        {
            double sum = 0.0;
            for (int s = 0; s < Seeds.Length; s++) sum += perSeed(s);
            return sum / Seeds.Length;
        }

        /// <summary>The sun over Bherwerre at a day of the year and a local solar hour, on a clock of its own.</summary>
        private static SolarClock Sun(int dayOfYear, double hour)
            => SolarClock.ForRegion(Region.Bherwerre, WorldClock.FromLocal(dayOfYear, hour, Region.Bherwerre.CentreLongitudeDeg));

        /// <summary>How many times a curve turns around across one day, read hourly.</summary>
        private static int Reversals(Func<int, double> at)
        {
            int turns = 0, lastSign = 0;
            for (int hour = 1; hour < 24; hour++)
            {
                double delta = at(hour) - at(hour - 1);
                int sign = delta > 1e-12 ? 1 : delta < -1e-12 ? -1 : 0;
                if (sign == 0) continue;
                if (lastSign != 0 && sign != lastSign) turns++;
                lastSign = sign;
            }
            return turns;
        }
    }
}
