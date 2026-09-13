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
    /// alone, which put Moss Vale within half a degree and puts no particular coast anywhere near. Here the lighthouse's
    /// twelve months are fitted, each month's days and nights averaged at mid-month, as one yearly wave for the day's mean
    /// and one for its range (least squares, 2026-09-13): the worst month, November, stands 0.7 °C off its own mean, and
    /// August, when the founder wakes, 0.1.</para>
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

        /// <summary>A weather station's record, as the day's mean and range fitted to its twelve months as yearly waves.</summary>
        private sealed class Station
        {
            public readonly double ElevationM;
            public readonly double AnnualMeanC, SeasonalAmplitudeC, WarmestDay;
            public readonly double MeanRangeC, RangeAmplitudeC, WidestRangeDay;

            public Station(double elevationM, double annualMeanC, double seasonalAmplitudeC, double warmestDay,
                           double meanRangeC, double rangeAmplitudeC, double widestRangeDay)
            {
                ElevationM = elevationM;
                AnnualMeanC = annualMeanC;
                SeasonalAmplitudeC = seasonalAmplitudeC;
                WarmestDay = warmestDay;
                MeanRangeC = meanRangeC;
                RangeAmplitudeC = rangeAmplitudeC;
                WidestRangeDay = widestRangeDay;
            }
        }

        /// <summary>
        /// Point Perpendicular Lighthouse, Bureau of Meteorology station 068034, 85 m up on the Beecroft Peninsula across
        /// Jervis Bay from Bherwerre, 1899–2004. Its 1991–2004 monthly means of the day's highest and lowest readings, as
        /// Wikipedia's Jervis Bay Village page reproduces them (the Bureau's own pages refuse scripts; DEBTS, "station
        /// normals are hand-pulled"), give a day's mean of 17.02 °C over the year, 4.18 °C either way, warmest at day 26.8,
        /// and a daily range of 6.42 °C, 0.61 either way, widest at day 278.6.
        /// </summary>
        private static readonly Station PointPerpendicular = new Station(85.0, 17.020, 4.183, 26.8, 6.419, 0.613, 278.6);

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

        /// <summary>
        /// The air's temperature at a local solar hour (0 to 24) of a day of the year (1 to 365), at a height above the sea,
        /// °C: pre-human, on the day's sine from just before sunrise to sunset and on the night's exponential from sunset to
        /// the next dawn, colder with height above the station.
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
        private double MeanC(int day)
            => _station.AnnualMeanC + _station.SeasonalAmplitudeC * Math.Cos(2.0 * Math.PI * (day - _station.WarmestDay) / 365.0)
               - AnthropogenicWarmingSinceRecordsC;

        /// <summary>The day's range from its lowest to its highest, °C; a shape, which the pre-human offset leaves alone.</summary>
        private double RangeC(int day)
            => _station.MeanRangeC + _station.RangeAmplitudeC * Math.Cos(2.0 * Math.PI * (day - _station.WidestRangeDay) / 365.0);

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
    }
}
