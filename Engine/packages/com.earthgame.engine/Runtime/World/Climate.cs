using System;

namespace EarthGame.Engine
{
    /// <summary>
    /// The air's temperature and the wind at a place and an hour, for a region this build holds a weather station's record
    /// for (M1.8a). Ported from v1 (`Sim/Body/Climate.cs`) and moved from Moss Vale in the highlands, v1's wake, to Point
    /// Perpendicular Lighthouse across the bay from Bherwerre.
    ///
    /// <para><b>Pre-human, and that is canon.</b> The owner's ruling of 2026-09-01: "if the player is going to be seeing
    /// megafauna roaming around, [the modern climate] wouldn't make sense." The world's climate is the station's record less
    /// what humanity has added to it (<see cref="AnthropogenicWarmingSinceRecordsC"/>), and the station's seasonal and
    /// daily shape binds whatever that level is.</para>
    ///
    /// <para><b>The station's own year, not a formula of latitude.</b> v1 worked a day's mean temperature out of latitude
    /// alone, which put Moss Vale within half a degree and puts no particular coast anywhere near. Here the record is the
    /// station's own table of monthly means of the day's highest and lowest readings, each month read as the average over
    /// its own days, which is what a station's monthly figure is. The day's mean is one yearly wave fitted to the twelve
    /// months, which leaves the worst months 0.7 °C from their own and August 0.1. The day's range is each month's own,
    /// eased from one mid-month to the next and solved so that every month's average is the one asked for: the yearly wave
    /// for the range this class first had (2026-09-13) left August's half a degree narrow, because the lighthouse's range
    /// widens 1.2 °C from July to August, and a second harmonic still left it 0.4.</para>
    ///
    /// <para><b>The curve is the day under the weather.</b> A station's months are means of what its thermometers read,
    /// fronts and all, and the fronts widen a day's extremes (<see cref="PointPerpendicular"/>), so the curve is fitted to
    /// the station's extremes less that widening, and the weather built on it (<see cref="Weather.At"/>) is what answers to
    /// the station.</para>
    ///
    /// <para><b>Nights coldest at dawn.</b> v1's one cosine put the night's coldest hour at 03:00, twelve hours from the
    /// afternoon's warmest, and its calibration record named what that cost: the tablet's night forecast read 05:00 and was
    /// three quarters of a degree warm on the one number a founder plans a night by. Here the day warms on a sine from just
    /// before sunrise to the early afternoon and the night cools on an exponential to the next dawn: Parton and Logan's
    /// model (1981, <i>Agricultural Meteorology</i> 23: 205–216), with their constants for air at 1.5 m as Savage quotes
    /// them (2016, <i>International Journal of Biometeorology</i> 60: 183–194). One departure: their night cools towards
    /// the next minimum without reaching it, ending about half a degree above it and stepping down at dawn, so the
    /// exponential here is scaled to meet the minimum exactly.</para>
    /// </summary>
    public sealed class Climate
    {
        // ---- the baseline ----

        /// <summary>
        /// How much of the instrument record is humanity's, °C: what separates a station's reading from the pre-human world
        /// the game is set in. Regional rather than global: Australia has warmed 1.51 ± 0.23 °C since national records began
        /// in 1910 (Bureau of Meteorology and CSIRO, <i>State of the Climate 2024</i>), where the planet has warmed nearer 1.1
        /// to 1.3 °C. Carried from v1, whose record names two refinements it leaves unapplied, each smaller than the
        /// uncertainty and pulling opposite ways.
        /// </summary>
        public const double AnthropogenicWarmingSinceRecordsC = 1.51;

        /// <summary>How much colder the air is a kilometre higher, °C: the standard atmosphere's lapse rate.</summary>
        public const double LapseRateCPerKm = 6.5;

        // ---- the day's shape: Parton and Logan, for air at 1.5 m ----

        /// <summary>
        /// How far the day's sine reaches past solar noon, hours: the day is warmest this long after noon, less the few
        /// minutes <see cref="MinimumAfterSunriseHours"/> sets the whole day earlier by.
        /// </summary>
        public const double MaximumAfterNoonHours = 1.86;

        /// <summary>How fast the night cools, as the exponent over the night's whole length.</summary>
        public const double NightCooling = 2.2;

        /// <summary>How long after sunrise the night's coldest moment falls, hours: negative, a few minutes before it.</summary>
        public const double MinimumAfterSunriseHours = -0.17;

        // ---- the stations ----

        private static readonly int[] DaysInMonth = { 31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31 };

        /// <summary>The middle day of each month, where the range's nodes stand; declared before the stations, which read it when made.</summary>
        private static readonly double[] MidMonthDay = MidMonths();

        /// <summary>
        /// A weather station's record, its height and its monthly means of the day's highest and lowest readings, and how far
        /// the weather's fronts widen those means beyond the day under them; from these the curve's mean and range are worked
        /// out for every day of the year when the station is made.
        /// </summary>
        private sealed class Station
        {
            public readonly double ElevationM;
            private readonly double[] _meanByDayC = new double[366];
            private readonly double[] _rangeByDayC = new double[366];

            public Station(double elevationM, double[] meanMaxC, double[] meanMinC, double[] frontsRaiseMaxC, double[] frontsLowerMinC)
            {
                ElevationM = elevationM;
                double[,] waveNormal = new double[3, 3];
                double[] waveTarget = new double[3];
                double[,] easing = new double[12, 12];
                double[] range = new double[12];
                int first = 1;
                for (int m = 0; m < 12; m++)
                {
                    // The month's averages of a level and of the yearly wave's two parts, and of the eased nodes' weights.
                    double[] wave = new double[3];
                    for (int day = first; day < first + DaysInMonth[m]; day++)
                    {
                        double angle = 2.0 * Math.PI * day / 365.0;
                        wave[0] += 1.0 / DaysInMonth[m];
                        wave[1] += Math.Cos(angle) / DaysInMonth[m];
                        wave[2] += Math.Sin(angle) / DaysInMonth[m];
                        MidMonthsAround(day, out int before, out int after, out double towardsAfter);
                        easing[m, before] += (1.0 - towardsAfter) / DaysInMonth[m];
                        easing[m, after] += towardsAfter / DaysInMonth[m];
                    }
                    double dayMaxC = meanMaxC[m] - frontsRaiseMaxC[m];
                    double dayMinC = meanMinC[m] + frontsLowerMinC[m];
                    double mean = 0.5 * (dayMaxC + dayMinC);
                    for (int i = 0; i < 3; i++)
                    {
                        waveTarget[i] += wave[i] * mean;
                        for (int j = 0; j < 3; j++) waveNormal[i, j] += wave[i] * wave[j];
                    }
                    range[m] = dayMaxC - dayMinC;
                    first += DaysInMonth[m];
                }
                // Least squares for the mean's wave; the range's nodes exactly, one month's average to each node.
                double[] level = Solve(waveNormal, waveTarget);
                double[] atMidMonth = Solve(easing, range);
                for (int day = 1; day <= 365; day++)
                {
                    double angle = 2.0 * Math.PI * day / 365.0;
                    _meanByDayC[day] = level[0] + level[1] * Math.Cos(angle) + level[2] * Math.Sin(angle);
                    MidMonthsAround(day, out int before, out int after, out double towardsAfter);
                    _rangeByDayC[day] = atMidMonth[before] * (1.0 - towardsAfter) + atMidMonth[after] * towardsAfter;
                }
            }

            /// <summary>The mean of the day under the weather at the station, °C.</summary>
            public double MeanC(int day) => _meanByDayC[day];

            /// <summary>The range of the day under the weather, from its lowest to its highest, °C.</summary>
            public double RangeC(int day) => _rangeByDayC[day];
        }

        /// <summary>
        /// Point Perpendicular Lighthouse, Bureau of Meteorology station 068034, 85 m up on the Beecroft Peninsula across
        /// Jervis Bay from Bherwerre, 1899–2004: its 1991–2004 monthly means of the day's highest and lowest readings, °C, as
        /// Wikipedia's Jervis Bay Village page reproduces them (the Bureau's own pages refuse scripts; DEBTS, "station
        /// normals are hand-pulled").
        ///
        /// <para>Then, month by month, how far the weather's fronts lift the mean of the day's highest reading above the day
        /// under them and lower the mean of the lowest below its dawn, °C, read as the Bureau reads its extremes: the highest
        /// in the 24 hours from 9 am, the lowest in the 24 hours to 9 am. A front that comes or goes between an afternoon and
        /// the next morning, or between an evening and the dawn after it, moves the air further than the day under it stands
        /// between those hours. Fitted to the lighthouse's extremes themselves, the curve left the weather's August range a
        /// quarter of a degree wide (2026-09-13). Measured on many world seeds' weather at the lighthouse's height;
        /// WeatherTests hold the weather's months to the lighthouse's, which keeps these honest if the fronts change.</para>
        /// </summary>
        private static readonly Station PointPerpendicular = new Station(85.0,
            new[] { 24.2, 24.4, 23.0, 21.1, 18.6, 16.6, 15.6, 16.8, 18.5, 20.1, 21.0, 23.1 },
            new[] { 17.9, 18.4, 17.1, 14.9, 12.8, 10.6, 9.5, 9.5, 11.4, 13.0, 14.3, 16.6 },
            new[] { 0.39, 0.38, 0.33, 0.23, 0.24, 0.21, 0.19, 0.18, 0.24, 0.26, 0.39, 0.41 },
            new[] { 0.13, 0.12, 0.16, 0.21, 0.31, 0.32, 0.31, 0.15, 0.09, 0.13, 0.09, 0.10 });

        private readonly Station _station;
        private readonly double _latitudeDeg;

        private Climate(Station station, double latitudeDeg)
        {
            _station = station;
            _latitudeDeg = latitudeDeg;
        }

        /// <summary>
        /// The climate of a region, from the station record this build holds for it. A region it holds none for is refused,
        /// rather than given another place's weather.
        /// </summary>
        public static Climate ForRegion(Region region)
        {
            if (region == null) throw new ArgumentNullException(nameof(region));
            if (!string.Equals(region.Id, Region.Bherwerre.Id, StringComparison.Ordinal))
                throw new ArgumentException("this build holds no weather station's record for region '" + region.Id + "'", nameof(region));
            return new Climate(PointPerpendicular, region.CentreLatitudeDeg);
        }

        /// <summary>The height above the sea of the weather station this climate stands on, metres: where the lapse rate takes nothing off.</summary>
        public double StationElevationM => _station.ElevationM;

        /// <summary>
        /// The air's temperature under the weather at a local solar hour (0 to 24) of a day of the year (1 to 365), at a height
        /// above the sea, °C: pre-human, on the day's sine from just before sunrise to sunset and on the night's exponential
        /// from sunset to the next dawn, colder with height above the station.
        /// </summary>
        public double AirTemperatureC(int dayOfYear, double hourOfDay, double altitudeM)
        {
            int day = WrapDay(dayOfYear);
            double height = -LapseRateCPerKm * (altitudeM - _station.ElevationM) / 1000.0;
            Sun(day, out double sunrise, out double sunset);
            double coldest = sunrise + MinimumAfterSunriseHours;
            if (hourOfDay >= coldest && hourOfDay <= sunset)
                return Daylight(day, hourOfDay, coldest, sunset - sunrise) + height;

            if (hourOfDay > sunset)
            {
                // The night after this day's sunset, cooling to tomorrow's minimum.
                int next = WrapDay(day + 1);
                Sun(next, out double nextSunrise, out _);
                double atSunset = Daylight(day, sunset, coldest, sunset - sunrise);
                return Night(atSunset, MinimumC(next), hourOfDay - sunset, 24.0 - sunset + nextSunrise + MinimumAfterSunriseHours) + height;
            }

            // The night before this day's dawn, cooling from yesterday's sunset to this day's minimum.
            int previous = WrapDay(day - 1);
            Sun(previous, out double previousSunrise, out double previousSunset);
            double atPreviousSunset = Daylight(previous, previousSunset, previousSunrise + MinimumAfterSunriseHours, previousSunset - previousSunrise);
            return Night(atPreviousSunset, MinimumC(day), hourOfDay + 24.0 - previousSunset, 24.0 - previousSunset + coldest) + height;
        }

        /// <summary>
        /// The wind in the open, m/s: gentle at night, freshening through the afternoon as the ground heats, and more of it on
        /// open ground than in shelter. v1's shape, carried unchanged until a wind record for this coast is read (M1.8a, owed).
        /// </summary>
        public double WindSpeedMs(double hourOfDay, double exposure01)
        {
            double diurnal = 1.5 + 2.5 * Math.Max(0.0, Math.Sin(Math.PI * (hourOfDay - 6.0) / 12.0));
            return diurnal * (0.35 + 0.65 * SimMath.Clamp01(exposure01));
        }

        /// <summary>The day's mean at the station's height, pre-human, °C.</summary>
        private double MeanC(int day) => _station.MeanC(day) - AnthropogenicWarmingSinceRecordsC;

        /// <summary>The day's range from its lowest to its highest, °C; a shape, which the pre-human offset leaves alone.</summary>
        private double RangeC(int day) => _station.RangeC(day);

        private double MinimumC(int day) => MeanC(day) - 0.5 * RangeC(day);

        private double MaximumC(int day) => MeanC(day) + 0.5 * RangeC(day);

        /// <summary>The day's sine from its coldest moment, reaching its maximum <see cref="MaximumAfterNoonHours"/> past noon.</summary>
        private double Daylight(int day, double hourOfDay, double coldest, double dayLength)
        {
            double low = MinimumC(day);
            return low + (MaximumC(day) - low) * Math.Sin(Math.PI * (hourOfDay - coldest) / (dayLength + 2.0 * MaximumAfterNoonHours));
        }

        /// <summary>The night's exponential, scaled so that it leaves sunset at the day's value and meets the minimum at dawn.</summary>
        private static double Night(double atSunset, double minimum, double sinceSunset, double nightLength)
        {
            double tail = Math.Exp(-NightCooling);
            double fall = (Math.Exp(-NightCooling * sinceSunset / nightLength) - tail) / (1.0 - tail);
            return minimum + (atSunset - minimum) * fall;
        }

        /// <summary>Sunrise and sunset at the region's latitude on a day, local solar hours, as the solar clock works them out.</summary>
        private void Sun(int day, out double sunrise, out double sunset)
        {
            double daylight = SolarClock.DaylightHoursFor(_latitudeDeg, SolarClock.DeclinationDegFor(day));
            sunrise = SolarClock.SunriseHourFor(daylight);
            sunset = SolarClock.SunsetHourFor(daylight);
        }

        private static int WrapDay(int dayOfYear) => ((dayOfYear - 1) % 365 + 365) % 365 + 1;

        private static double[] MidMonths()
        {
            double[] middle = new double[12];
            int first = 1;
            for (int m = 0; m < 12; m++)
            {
                middle[m] = first + (DaysInMonth[m] - 1) / 2.0;
                first += DaysInMonth[m];
            }
            return middle;
        }

        /// <summary>
        /// The two mid-months a day of the year falls between, December's and January's across the new year, and how far from
        /// the first towards the second it stands, 0 to 1.
        /// </summary>
        private static void MidMonthsAround(int day, out int before, out int after, out double towardsAfter)
        {
            after = 0;
            while (after < 12 && MidMonthDay[after] <= day) after++;
            double from, to;
            if (after == 0)
            {
                before = 11;
                from = MidMonthDay[11] - 365.0;
                to = MidMonthDay[0];
            }
            else if (after == 12)
            {
                before = 11;
                after = 0;
                from = MidMonthDay[11];
                to = MidMonthDay[0] + 365.0;
            }
            else
            {
                before = after - 1;
                from = MidMonthDay[before];
                to = MidMonthDay[after];
            }
            towardsAfter = (day - from) / (to - from);
        }

        /// <summary>A small square system solved by Gaussian elimination with partial pivoting: a station's fits have a dozen unknowns at most.</summary>
        private static double[] Solve(double[,] matrix, double[] rightHandSide)
        {
            int n = rightHandSide.Length;
            double[,] a = (double[,])matrix.Clone();
            double[] b = (double[])rightHandSide.Clone();
            for (int column = 0; column < n; column++)
            {
                int pivot = column;
                for (int row = column + 1; row < n; row++)
                    if (Math.Abs(a[row, column]) > Math.Abs(a[pivot, column])) pivot = row;
                if (Math.Abs(a[pivot, column]) < 1e-12) throw new InvalidOperationException("a station's record gave a fit with no single answer");
                if (pivot != column)
                {
                    for (int k = 0; k < n; k++)
                    {
                        double swap = a[column, k];
                        a[column, k] = a[pivot, k];
                        a[pivot, k] = swap;
                    }
                    double held = b[column];
                    b[column] = b[pivot];
                    b[pivot] = held;
                }
                for (int row = column + 1; row < n; row++)
                {
                    double factor = a[row, column] / a[column, column];
                    for (int k = column; k < n; k++) a[row, k] -= factor * a[column, k];
                    b[row] -= factor * b[column];
                }
            }
            double[] x = new double[n];
            for (int row = n - 1; row >= 0; row--)
            {
                double sum = b[row];
                for (int k = row + 1; k < n; k++) sum -= a[row, k] * x[k];
                x[row] = sum / a[row, row];
            }
            return x;
        }
    }
}
