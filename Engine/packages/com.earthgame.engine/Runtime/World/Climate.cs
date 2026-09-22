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
    /// its own days, which is what a station's monthly figure is: since M1.8c (2026-09-16) the Bureau's own all-years table,
    /// read in the owner's browser and kept under <c>Data/stations/</c>, in place of the fourteen-year reproduction M1.8a
    /// stood on. The day's mean is one yearly wave fitted to the twelve
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

        /// <summary>
        /// The years the warming figure spans: national records began in 1910, and <i>State of the Climate 2024</i> reads to
        /// 2023. The figure is a trend across those years, so what of it lies inside a station's table is the trend's value at
        /// the table's middle year (<see cref="WarmingInsideRecordC"/>).
        /// </summary>
        public const int WarmingTrendFromYear = 1910, WarmingTrendToYear = 2023;

        /// <summary>
        /// How much of a station's table is humanity's, °C: the warming trend's value at the middle of the years the table
        /// averages over (M1.8c, 2026-09-16). The lighthouse's 1907–2004 table stands 0.61 °C above the pre-human coast, and a
        /// 1991–2004 table 1.17; M1.8a took the whole 1.51 off a fourteen-year table, a third of a degree too much. The trend is
        /// read as its source states it, straight; the century's warming was slower at first and faster of late, which would
        /// make an old table's share smaller still, and no series for this coast has been read to say by how much (DEBTS).
        /// </summary>
        public static double WarmingInsideRecordC(int firstYear, int lastYear)
        {
            double middle = 0.5 * (firstYear + lastYear);
            return AnthropogenicWarmingSinceRecordsC * SimMath.Clamp01((middle - WarmingTrendFromYear) / (WarmingTrendToYear - WarmingTrendFromYear));
        }

        // ---- the wind and the moisture at a founder ----

        /// <summary>
        /// How much of a station's wind, read at the anemometer's 10 m, blows at a founder's height of 1.5 m over open ground:
        /// the logarithmic profile over short grass, ln(1.5 / 0.03) over ln(10 / 0.03), about two thirds; sand and short
        /// heath are smoother and would give 0.72. Grass is taken, and the tests restate the same line.
        /// </summary>
        public static readonly double BodyHeightWindShare = Math.Log(1.5 / 0.03) / Math.Log(10.0 / 0.03);

        /// <summary>How much of the open's wind blows on ground with no openness at all, and the rest scales with the openness: v1's shelter.</summary>
        public const double ShelteredWindShare = 0.35;

        /// <summary>
        /// The wind's day: a floor through the night, a rise from this hour to a peak at <see cref="WindPeakHour"/> and back to
        /// the floor by midnight, as a half sine, so that a station's two readings a day fix it: at nine in the morning the
        /// rise is half made, at three in the afternoon whole.
        /// </summary>
        public const double WindRiseFromHour = 6.0, WindPeakHour = 15.0;

        /// <summary>
        /// The pressure of water vapour that saturates air at a temperature, kPa: the Magnus form with Alduchov and Eskridge's
        /// constants (1996, <i>Journal of Applied Meteorology</i> 35: 601–609). One owner for the humidity (M1.8c) and the
        /// breath's latent loss (FP.2), which had its own copy of the same line.
        /// </summary>
        public static double SaturationVapourKPa(double airC) => 0.61094 * Math.Exp(17.625 * airC / (airC + 243.04));

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

        /// <summary>
        /// How far below the air a clear night sky radiates, K (FP.2, v1's figure): the sky the body loses heat to. One
        /// figure for the body's sky term and any forecast's; v1 once declared it twice and the two skies drifted apart.
        /// </summary>
        public const double ClearSkyDepressionK = 16.0;

        /// <summary>Solar constant at the top of the atmosphere, W/m².</summary>
        public const double SolarConstantWm2 = 1361.0;

        /// <summary>
        /// How much of the direct beam a wholly covered sky takes, how much diffuse light a clear sky adds as a share of the
        /// beam's fall on level ground, and how much of the sun above the atmosphere an overcast sky lets through as diffuse
        /// light. v1 had 0.85, 0.10 and 0.28, and under the weather's own cloud they left every month's energy on the ground
        /// 5 to 14 per cent short of the Bureau's satellite figure for the site; these three (M1.8c, 2026-09-16) hold all
        /// twelve months within a fifteenth, the cloud an observer reads in oktas not taking the whole beam on average and a
        /// clear sky scattering more than a tenth.
        /// </summary>
        public const double BeamLostToFullCloud = 0.75, ClearSkyDiffuseShare = 0.15, OvercastTransmission = 0.28;

        /// <summary>
        /// Direct-beam solar irradiance reaching the ground, W/m², by the Meinel approximation I = 1.353 · 0.7^(AM^0.678)
        /// kW/m² with the air mass from the sun's elevation (FP.2, v1's). At a winter noon here the sun is about 32° up,
        /// an air mass near two, around 780 W/m²: the 600-800 that is measured. Cloud cuts the beam (<see cref="BeamLostToFullCloud"/>)
        /// long before the light.
        /// </summary>
        public static double DirectSolarWm2(double solarElevationDeg, double cloudCover01)
        {
            if (solarElevationDeg <= 0.5) return 0.0;
            double sinE = Math.Sin(solarElevationDeg * Math.PI / 180.0);
            double airMass = 1.0 / Math.Max(0.05, sinE);
            double clear = 1353.0 * Math.Pow(0.7, Math.Pow(airMass, 0.678));
            return clear * (1.0 - BeamLostToFullCloud * SimMath.Clamp01(cloudCover01));
        }

        /// <summary>
        /// Diffuse sky irradiance on the ground, W/m²: a share of the beam's fall under a clear sky and most of what is left
        /// under an overcast one, which is why an overcast day is bright and not warm (FP.2, v1's shape; M1.8c's shares).
        /// </summary>
        public static double DiffuseSolarWm2(double solarElevationDeg, double cloudCover01)
        {
            if (solarElevationDeg <= 0.5) return 0.0;
            double sinE = Math.Sin(solarElevationDeg * Math.PI / 180.0);
            double cloud = SimMath.Clamp01(cloudCover01);
            double clearSky = ClearSkyDiffuseShare * DirectSolarWm2(solarElevationDeg, 0.0) * sinE;
            double overcast = OvercastTransmission * SolarConstantWm2 * sinE;
            return clearSky * (1.0 - cloud) + overcast * cloud;
        }

        // ---- the stations ----

        private static readonly int[] DaysInMonth = { 31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31 };

        /// <summary>The middle day of each month, where the range's nodes stand; declared before the stations, which read it when made.</summary>
        private static readonly double[] MidMonthDay = MidMonths();

        /// <summary>
        /// A weather station's record: its height; its monthly means of the day's highest and lowest readings with the years
        /// they average over, and how far the weather's fronts widen those means beyond the day under them, from which the
        /// curve's mean and range are worked out for every day of the year when the station is made; its 9 am and 3 pm dew
        /// points by month; and its 9 am and 3 pm wind speeds by month, km/h at the anemometer's 10 m.
        /// </summary>
        private sealed class Station
        {
            public readonly double ElevationM;
            /// <summary>What of the temperature table is humanity's, °C: the trend's share inside its years.</summary>
            public readonly double WarmingC;
            private readonly double[] _meanByDayC = new double[366];
            private readonly double[] _rangeByDayC = new double[366];
            private readonly double[] _dewPointC = new double[12];
            private readonly double[] _windFloorMs = new double[12];
            private readonly double[] _windPeakMs = new double[12];

            public Station(double elevationM, int firstYear, int lastYear, double[] meanMaxC, double[] meanMinC, double[] frontsRaiseMaxC, double[] frontsLowerMinC,
                           double[] dewPoint9amC, double[] dewPoint3pmC, double[] wind9amKmh, double[] wind3pmKmh)
            {
                ElevationM = elevationM;
                WarmingC = WarmingInsideRecordC(firstYear, lastYear);
                for (int m = 0; m < 12; m++)
                {
                    // The dew point comes down with the air by the same amount, which keeps the humidity the record's: the
                    // moisture the pre-human air held is what gave the same dampness at a cooler temperature.
                    _dewPointC[m] = 0.5 * (dewPoint9amC[m] + dewPoint3pmC[m]) - WarmingC;
                    // Two readings a day fix the day's floor and peak (WindRiseFromHour, WindPeakHour): at nine the rise is
                    // half made, so the floor is twice the nine o'clock reading less the three o'clock one; a floor under a
                    // quarter of the peak is not a coast's night, and is held there.
                    double at9 = wind9amKmh[m] / 3.6 * BodyHeightWindShare, at15 = wind3pmKmh[m] / 3.6 * BodyHeightWindShare;
                    _windPeakMs[m] = at15;
                    _windFloorMs[m] = Math.Max(0.25 * at15, 2.0 * at9 - at15);
                }
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

            /// <summary>The mean of the day under the weather at the station, °C, as the table has it: the warming still in it.</summary>
            public double MeanC(int day) => _meanByDayC[day];

            /// <summary>The range of the day under the weather, from its lowest to its highest, °C.</summary>
            public double RangeC(int day) => _rangeByDayC[day];

            /// <summary>The month's dew point, pre-human, °C, eased between mid-months.</summary>
            public double DewPointC(int day) => Eased(_dewPointC, day);

            /// <summary>The night's wind in the open at a founder's height on a day, m/s, eased between mid-months.</summary>
            public double WindFloorMs(int day) => Eased(_windFloorMs, day);

            /// <summary>The afternoon's peak wind in the open at a founder's height on a day, m/s, eased between mid-months.</summary>
            public double WindPeakMs(int day) => Eased(_windPeakMs, day);

            /// <summary>A monthly figure read on a day of the year: straight between the two mid-months around it. Good to a few per cent of a smooth year; the temperature's range, held to a tenth of a degree, is solved for its exact monthly averages instead.</summary>
            private static double Eased(double[] byMonth, int day)
            {
                MidMonthsAround(day, out int before, out int after, out double towardsAfter);
                return byMonth[before] * (1.0 - towardsAfter) + byMonth[after] * towardsAfter;
            }
        }

        /// <summary>
        /// Point Perpendicular Lighthouse, Bureau of Meteorology station 068034, 85 m up on the Beecroft Peninsula across
        /// Jervis Bay from Bherwerre, 1899–2004. Its monthly means of the day's highest and lowest readings, °C, over
        /// 1907–2004, from the Bureau's own "Climate statistics for Australian locations" table (product IDCJCM0037, all years
        /// of record), read in the owner's browser on 2026-09-16 and kept under <c>Data/stations/</c> (M1.8c; the Bureau
        /// serves browsers and refuses scripts). Until then the station stood on the 1991–2004 means as Wikipedia's Jervis Bay
        /// Village page reproduced them (M1.8a): August's day 16.8 °C against these 16.1, a short warm record's.
        ///
        /// <para>Then, month by month, how far the weather's fronts lift the mean of the day's highest reading above the day
        /// under them and lower the mean of the lowest below its dawn, °C, read as the Bureau reads its extremes: the highest
        /// in the 24 hours from 9 am, the lowest in the 24 hours to 9 am. A front that comes or goes between an afternoon and
        /// the next morning, or between an evening and the dawn after it, moves the air further than the day under it stands
        /// between those hours. Fitted to the lighthouse's extremes themselves, the curve left the weather's August range a
        /// quarter of a degree wide (2026-09-13). Measured on many world seeds' weather at the lighthouse's height by the
        /// almanac tool's <c>+fit widening</c>, and measured again whenever the cold snap's depth changes (M1.8c set it from
        /// the record's deciles); WeatherTests hold the weather's months to the lighthouse's, which keeps these honest.</para>
        ///
        /// <para>Then the 9 am and the 3 pm mean dew points by month, °C (1957–2004; the air table's warming comes off
        /// them, so the humidity stays the record's), and the 9 am and the 3 pm mean wind speeds by month, km/h at 10 m,
        /// 1957–2004, from the same table. Nowra RAN Air Station AWS (068072), 29 km inland at
        /// 109 m, reads the same open wind within a few tenths of a metre a second (9 am 14.3 and 3 pm 20.0 km/h over the year
        /// against the lighthouse's 15.8 and 20.0), so this is the region's open wind and not a headland's alone.</para>
        /// </summary>
        private static readonly Station PointPerpendicular = new Station(85.0, 1907, 2004,
            new[] { 23.8, 23.9, 23.0, 20.7, 18.2, 15.9, 15.1, 16.1, 17.9, 19.8, 21.2, 22.9 },
            new[] { 17.5, 18.0, 17.1, 14.9, 12.4, 10.4, 9.2, 9.6, 11.2, 12.9, 14.5, 16.3 },
            new[] { 0.16, 0.15, 0.09, 0.07, 0.06, 0.05, 0.06, 0.05, 0.07, 0.09, 0.13, 0.16 },
            new[] { 0.02, 0.03, 0.06, 0.07, 0.06, 0.14, 0.08, 0.05, 0.02, 0.04, 0.02, 0.01 },
            new[] { 16.2, 16.8, 15.5, 12.9, 10.2, 8.1, 6.6, 6.9, 8.5, 10.6, 12.8, 14.7 },
            new[] { 16.8, 17.3, 16.0, 13.3, 10.7, 8.4, 6.9, 7.3, 9.1, 11.3, 13.4, 15.3 },
            new[] { 14.2, 14.0, 13.5, 15.0, 16.8, 19.1, 17.7, 17.1, 16.2, 15.2, 15.6, 14.8 },
            new[] { 20.5, 19.3, 18.3, 17.9, 17.9, 19.1, 19.4, 20.5, 21.3, 21.2, 22.5, 21.9 });

        /// <summary>
        /// Nowra RAN Air Station AWS (068072), 109 m, 34.95°S 150.54°E: the Kangaroo Valley's station (WG.2, CANON ruling 43,
        /// 2026-09-22), twenty kilometres east of the valley floor at much the same height. The Bureau's all-years table as William
        /// pasted it on 2026-09-16 (<c>Data/stations/068072_nowra_ran_air_station_aws_all_years.txt</c>): the mean maxima and minima
        /// by month, 2000–2026; the 9 am and 3 pm dew points and wind speeds by month, 2000–2010. The fronts' widening of the day's
        /// extremes is the lighthouse's, not yet fitted to Nowra's own deciles, and so are the rain, the cloud and the cold snap's
        /// depth in <see cref="Synoptic"/> (DEBTS 2026-09-22); the valley's colder nights under the escarpment are the inland
        /// gradient's debt as Bherwerre's are.
        /// </summary>
        private static readonly Station Nowra = new Station(109.0, 2000, 2026,
            new[] { 27.6, 26.5, 25.3, 22.9, 19.7, 17.0, 16.8, 18.3, 21.2, 23.2, 24.8, 26.3 },
            new[] { 16.8, 16.7, 15.3, 12.5, 9.4, 7.7, 6.8, 7.1, 8.8, 10.9, 13.5, 15.0 },
            new[] { 0.16, 0.15, 0.09, 0.07, 0.06, 0.05, 0.06, 0.05, 0.07, 0.09, 0.13, 0.16 },
            new[] { 0.02, 0.03, 0.06, 0.07, 0.06, 0.14, 0.08, 0.05, 0.02, 0.04, 0.02, 0.01 },
            new[] { 14.9, 15.9, 14.1, 11.3, 8.3, 6.5, 5.1, 4.6, 6.6, 8.0, 12.3, 12.8 },
            new[] { 15.3, 16.2, 14.3, 11.3, 8.2, 6.4, 4.9, 4.1, 6.1, 8.4, 12.5, 13.2 },
            new[] { 11.5, 11.7, 11.7, 13.7, 14.8, 16.8, 16.8, 18.1, 16.8, 14.7, 12.7, 12.2 },
            new[] { 21.4, 19.5, 19.2, 18.1, 16.7, 16.7, 18.2, 21.8, 22.9, 21.7, 21.3, 22.0 });

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
            Station station = StationFor(region);
            if (station == null)
                throw new ArgumentException("this build holds no weather station's record for region '" + region.Id + "'", nameof(region));
            return new Climate(station, region.CentreLatitudeDeg);
        }

        /// <summary>The station a region's climate stands on: the lighthouse for Bherwerre, Nowra for the Kangaroo Valley (WG.2); null for a region this build holds none for.</summary>
        private static Station StationFor(Region region)
        {
            if (region == null) return null;
            if (string.Equals(region.Id, Region.Bherwerre.Id, StringComparison.Ordinal)) return PointPerpendicular;
            if (string.Equals(region.Id, Region.KangarooValley.Id, StringComparison.Ordinal)) return Nowra;
            return null;
        }

        /// <summary>Whether this build holds a station's record for the region: the one question, asked before the refusal above.</summary>
        public static bool HasRecordFor(Region region) => StationFor(region) != null;

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
        /// The wind in the open at a founder's height, m/s, on a day of the year and a local solar hour (M1.8c): the station's
        /// 9 am and 3 pm means by month brought from the anemometer's 10 m to 1.5 m (<see cref="BodyHeightWindShare"/>), read
        /// through the day as a floor through the night and a half-sine rise from six in the morning to a peak at three in the
        /// afternoon and back by midnight (<see cref="WindRiseFromHour"/>, <see cref="WindPeakHour"/>), which the two readings
        /// fix. In winter the lighthouse's mornings blow as hard as its afternoons and the floor is the peak; in summer the
        /// afternoon's sea breeze stands well over a quieter night. Less of it on sheltered ground (<see cref="ShelteredWindShare"/>).
        /// v1's shape, carried through M1.8a, peaked at noon at four metres a second and fell to one and a half by night: a
        /// third low against the record in the afternoon and early.
        /// </summary>
        public double WindSpeedMs(int dayOfYear, double hourOfDay, double exposure01)
        {
            int day = WrapDay(dayOfYear);
            double floor = _station.WindFloorMs(day), peak = _station.WindPeakMs(day);
            double rise = hourOfDay <= WindRiseFromHour ? 0.0
                : Math.Max(0.0, Math.Sin(Math.PI * (hourOfDay - WindRiseFromHour) / (2.0 * (WindPeakHour - WindRiseFromHour))));
            double open = floor + (peak - floor) * rise;
            return open * (ShelteredWindShare + (1.0 - ShelteredWindShare) * SimMath.Clamp01(exposure01));
        }

        /// <summary>
        /// The dew point of the settled air on a day of the year, pre-human, °C (M1.8c): the station's month, the mean of its
        /// 9 am and 3 pm readings, which stand within half a degree of each other in every month because the air's moisture
        /// does not follow the day's heat as its temperature does. The fronts move it (<see cref="Synoptic.DewPointShiftC"/>)
        /// and the humidity follows from it and the air (<see cref="Synoptic.RelativeHumidity01"/>).
        /// </summary>
        public double DewPointC(int dayOfYear) => _station.DewPointC(WrapDay(dayOfYear));

        /// <summary>The day's mean at the station's height, pre-human, °C: the table's less the warming inside its years.</summary>
        private double MeanC(int day) => _station.MeanC(day) - _station.WarmingC;

        /// <summary>What of this climate's station table is humanity's, °C: what the tests and the check take off the table to reach the world's level.</summary>
        public double WarmingInsideStationRecordC => _station.WarmingC;

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
