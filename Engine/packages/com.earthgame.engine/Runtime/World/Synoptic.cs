using System;

namespace EarthGame.Engine
{
    /// <summary>
    /// The rhythm of the weather at Bherwerre (M1.8a): fronts arriving and passing, the showers inside them and the dry
    /// spells between, as one seeded index read as a function of time. Ported from v1 (`Sim/World/Synoptic.cs`), with its
    /// season and its rain re-solved against Point Perpendicular Lighthouse.
    ///
    /// <para><b>Seeded, and a function of time rather than a state stepped forward.</b> A warning before dark asks about
    /// five o'clock tomorrow, and a model that evolved a state could not answer it. The same seed and instant give the same
    /// weather asked in any order, and a client holding the world's seed and clock works out what the server does.</para>
    ///
    /// <para><b>A sum of sinusoids, not noise.</b> Weather in these latitudes arrives on a rhythm of days as fronts cross,
    /// and a handful of harmonics with seeded periods and phases in that band gives what that looks like: runs of wet days,
    /// longer dry spells, and no two months alike. Noise would drizzle every other day and hit a month's total while being
    /// wrong about every day in it. Inside the fronts runs a second band of hours, the showers.</para>
    ///
    /// <para><b>One index, read four ways.</b> It is how deep in a frontal system the place is, and it drives the rain, the
    /// cloud, the cold behind a front and the wind with it, so those cannot disagree about whether there is weather.</para>
    ///
    /// <para><b>The world's own time, continuous.</b> Every reading takes local solar days since the world's epoch, as
    /// <see cref="WorldClock.LocalHours"/> counts them, divided by 24. v1 read the index at the day of the year plus the hour,
    /// so at every new year's midnight the weather jumped to another front and every year repeated the one before; here the
    /// years run on from each other.</para>
    /// </summary>
    public sealed class Synoptic
    {
        /// <summary>
        /// How many harmonics make the frontal band: enough that the sum is not plainly periodic, few enough that it keeps
        /// weather's lumpiness, which many would smooth towards a bell curve.
        /// </summary>
        public const int HarmonicCount = 6;

        /// <summary>
        /// The rain's settings as one value, so that the almanac tool's fit can ask the engine's own formulas with candidates
        /// and the engine's constants stay the one owner of the numbers (M1.8c): <see cref="Default"/> is those constants.
        /// </summary>
        public readonly struct RainSettings
        {
            public readonly double Threshold, ScaleMmPerHour, FrequencyAmplitude, MostFrontsDay, HeavierAmplitude, HeaviestRainDay;

            public RainSettings(double threshold, double scaleMmPerHour, double frequencyAmplitude, double mostFrontsDay, double heavierAmplitude, double heaviestRainDay)
            {
                Threshold = threshold;
                ScaleMmPerHour = scaleMmPerHour;
                FrequencyAmplitude = frequencyAmplitude;
                MostFrontsDay = mostFrontsDay;
                HeavierAmplitude = heavierAmplitude;
                HeaviestRainDay = heaviestRainDay;
            }

            public static readonly RainSettings Default = new RainSettings(RainThreshold, RainScaleMmPerHour, Synoptic.FrequencyAmplitude, Synoptic.MostFrontsDay, Synoptic.HeavierAmplitude, Synoptic.HeaviestRainDay);
        }

        /// <summary>The cloud's settings as one value, for the same fit; <see cref="Default"/> is the engine's constants.</summary>
        public readonly struct CloudSettings
        {
            public readonly double Mean, Amplitude, CloudiestDay, IndexGain;

            public CloudSettings(double mean, double amplitude, double cloudiestDay, double indexGain)
            {
                Mean = mean;
                Amplitude = amplitude;
                CloudiestDay = cloudiestDay;
                IndexGain = indexGain;
            }

            public static readonly CloudSettings Default = new CloudSettings(Synoptic.CloudMean, Synoptic.CloudAmplitude, Synoptic.CloudiestDay, Synoptic.CloudIndexGain);
        }

        /// <summary>The frontal band's shortest and longest periods, days: systems cross these latitudes on this rhythm.</summary>
        public const double MinPeriodDays = 2.5;
        public const double MaxPeriodDays = 14.0;

        /// <summary>
        /// The shower band's shortest and longest periods, days: four hours to a day and a half. With the frontal band alone
        /// the index moves so slowly that once it crosses the rain's threshold it stays across for the rest of the day, and v1
        /// measured a wet August day as eighteen hours of continuous gentle rain. Frontal rain arrives in bands with breaks
        /// between, and that decides whether a founder can gather between showers.
        /// </summary>
        public const double MinShowerPeriodDays = 0.17;
        public const double MaxShowerPeriodDays = 1.5;

        public const int ShowerHarmonicCount = 5;

        /// <summary>
        /// How the index's variance splits between the bands. The frontal band decides whether a system is overhead and the
        /// shower band whether it is raining on you now, and the showers need enough of it to carry the index back under the
        /// threshold between bands, or they only ripple the rate and never stop the rain.
        /// </summary>
        public const double FrontalVarianceShare = 0.62;

        // ---- the rain, re-solved against the lighthouse (2026-09-13, and again 2026-09-16 against its own table) ----

        /// <summary>
        /// The index above which it rains, in the index's standard deviations: how often. It and the five settings below were
        /// fitted together on many world seeds by the almanac tool's <c>+fit rain</c> (M1.8c) to the lighthouse's own twelve
        /// months of rain, of days of 1 mm or more, of days of any rain and of days of 10 mm or more, 1899–2004; M1.8a fitted
        /// them to a fourteen-year reproduction whose rain days stated no threshold and had August at 6.0 days, which the
        /// long record gives 8.3: a short record's noise, as M1.8a suspected. Seeds the fit never saw keep the tests' bounds.
        /// </summary>
        public const double RainThreshold = 1.4180;

        /// <summary>Millimetres an hour for each unit of index above the threshold, where the season neither hardens nor softens the rain: how hard.</summary>
        public const double RainScaleMmPerHour = 4.0139;

        /// <summary>
        /// How much more often than on average fronts reach the coast at <see cref="MostFrontsDay"/>, as a shift of the index,
        /// and how much less often half a year away: most in late summer, fewest in late winter, when the lighthouse counts
        /// its fewest rain days of the year.
        /// </summary>
        public const double FrequencyAmplitude = 0.0694;

        /// <summary>The day of the year fronts reach the coast most often.</summary>
        public const double MostFrontsDay = 66.15;

        /// <summary>
        /// How much harder than <see cref="RainScaleMmPerHour"/> rain falls at <see cref="HeaviestRainDay"/>, as a share of
        /// it, and how much lighter half a year away. v1 had no such term: its season changed only how often fronts came,
        /// because Moss Vale's August had fewer rain days and less rain in about the same ratio. The lighthouse's year does
        /// not work that way: from March to August its rain falls on about as many days as from September to February, and
        /// about four tenths more of it on each, and no shift in how often fronts come can give both.
        /// </summary>
        public const double HeavierAmplitude = 0.2393;

        /// <summary>The day of the year rain falls hardest when it falls.</summary>
        public const double HeaviestRainDay = 164.01;

        /// <summary>
        /// How far a front pulls the air below the climate's curve, °C for each unit of the frontal index. v1's depth, 3.5, was
        /// set against Moss Vale in the highlands and carried through M1.8a; M1.8c (2026-09-16) sets it by the almanac tool's
        /// <c>+fit depth</c> so that August's daily minima at the lighthouse spread as the Bureau's deciles say they do, the
        /// ninth 5.2 °C above the first over 1946–2004, the sea holding a coast's air closer to its mean than a highland's.
        /// </summary>
        public const double ColdSnapDepthC = 2.58;

        // ---- the cloud, against the lighthouse's oktas (2026-09-16) ----

        /// <summary>
        /// The cloud's year: how much of the sky the settled air covers on average, and how far the year's wave takes it
        /// either way, most on <see cref="CloudiestDay"/>. Fitted by the almanac tool's <c>+fit cloud</c> (M1.8c) so that the
        /// weather's cloud at 9 am and 3 pm, read as the Bureau's observers read it in oktas, gives the lighthouse's months:
        /// five oktas in summer, under four in late winter, 1907–2004. M1.8a had one level off the index alone.
        /// </summary>
        public const double CloudMean = 0.5675, CloudAmplitude = 0.0948, CloudiestDay = 28.17;

        /// <summary>How much of the sky a unit of the index covers or clears: cloud comes before the rain and clears after it, so it is read further down than the rain's threshold.</summary>
        public const double CloudIndexGain = 0.55;

        /// <summary>
        /// How far a unit of the index moves the dew point, °C (M1.8c): moister in the air a front brings in ahead of it, drier
        /// in the settled air between. A stated assumption, not a fit; the record gives the dew point's months and nothing of
        /// its day-to-day spread (DEBTS).
        /// </summary>
        public const double DewPointIndexGainC = 1.5;

        private readonly double[] _periodDays = new double[HarmonicCount];
        private readonly double[] _phase = new double[HarmonicCount];
        private readonly double _amplitude;

        private readonly double[] _showerPeriodDays = new double[ShowerHarmonicCount];
        private readonly double[] _showerPhase = new double[ShowerHarmonicCount];
        private readonly double _showerAmplitude;

        /// <summary>The world seed the fronts are drawn from.</summary>
        public ulong Seed { get; }

        public Synoptic(ulong seed)
        {
            Seed = seed;
            SimRandom random = new SimRandom(SimRandom.DeriveSeed(seed, "synoptic"));
            for (int i = 0; i < HarmonicCount; i++)
            {
                // Log-uniform across the band, so that short and long systems are equally represented rather than the long
                // ones crowding the draw.
                _periodDays[i] = MinPeriodDays * Math.Pow(MaxPeriodDays / MinPeriodDays, random.NextDouble());
                _phase[i] = random.NextDouble() * 2.0 * Math.PI;
            }
            for (int i = 0; i < ShowerHarmonicCount; i++)
            {
                _showerPeriodDays[i] = MinShowerPeriodDays * Math.Pow(MaxShowerPeriodDays / MinShowerPeriodDays, random.NextDouble());
                _showerPhase[i] = random.NextDouble() * 2.0 * Math.PI;
            }
            // Sinusoids with independent phases add their variances at A²/2 each, so A = sqrt(2·share/K) gives the whole index
            // unit variance with each band its share, and the threshold stays a number of standard deviations.
            _amplitude = Math.Sqrt(2.0 * FrontalVarianceShare / HarmonicCount);
            _showerAmplitude = Math.Sqrt(2.0 * (1.0 - FrontalVarianceShare) / ShowerHarmonicCount);
        }

        /// <summary>
        /// The index at a local solar day since the epoch: zero-mean, unit variance and smooth, the front and the showers
        /// inside it. A fractional day is how rain starts and stops part-way through one.
        /// </summary>
        public double Index(double localDay)
        {
            double shower = 0.0;
            for (int i = 0; i < ShowerHarmonicCount; i++)
                shower += Math.Sin(2.0 * Math.PI * localDay / _showerPeriodDays[i] + _showerPhase[i]);
            return FrontalIndex(localDay) + shower * _showerAmplitude;
        }

        /// <summary>
        /// The frontal band alone: which air mass is over the place, without the showers inside it. The air follows the front
        /// and not the showers, since an air mass does not warm up between two of them: v1 read the whole index for its cold
        /// snap and measured the air swinging four degrees either way within six hours.
        /// </summary>
        public double FrontalIndex(double localDay)
        {
            double frontal = 0.0;
            for (int i = 0; i < HarmonicCount; i++)
                frontal += Math.Sin(2.0 * Math.PI * localDay / _periodDays[i] + _phase[i]);
            return frontal * _amplitude;
        }

        /// <summary>The day of the year a local day falls in, continuous: 1 at the year's first midnight, just short of 366 at its last.</summary>
        public static double SeasonDay(double localDay)
        {
            double intoYear = localDay % 365.0;
            return 1.0 + (intoYear < 0.0 ? intoYear + 365.0 : intoYear);
        }

        /// <summary>How the season shifts the index at a local day: positive when fronts reach the coast more often than on average.</summary>
        public static double SeasonalShift(double localDay) => SeasonalShift(localDay, RainSettings.Default);

        public static double SeasonalShift(double localDay, in RainSettings s)
            => s.FrequencyAmplitude * Math.Cos(2.0 * Math.PI * (SeasonDay(localDay) - s.MostFrontsDay) / 365.0);

        /// <summary>How hard the season makes rain fall at a local day, as a factor on <see cref="RainScaleMmPerHour"/>.</summary>
        public static double SeasonalIntensity(double localDay) => SeasonalIntensity(localDay, RainSettings.Default);

        public static double SeasonalIntensity(double localDay, in RainSettings s)
            => 1.0 + s.HeavierAmplitude * Math.Cos(2.0 * Math.PI * (SeasonDay(localDay) - s.HeaviestRainDay) / 365.0);

        /// <summary>The index as the season leaves it, which the rain reads.</summary>
        public double EffectiveIndex(double localDay) => EffectiveIndex(localDay, RainSettings.Default);

        public double EffectiveIndex(double localDay, in RainSettings s) => Index(localDay) + SeasonalShift(localDay, s);

        /// <summary>
        /// Rain reaching the ground, mm/h: none unless a front is over the place, building and fading with it rather than
        /// switching on, and harder in the season of heavy rain.
        /// </summary>
        public double RainRateMmPerHour(double localDay) => RainRateMmPerHour(localDay, RainSettings.Default);

        public double RainRateMmPerHour(double localDay, in RainSettings s)
        {
            double excess = EffectiveIndex(localDay, s) - s.Threshold;
            return excess <= 0.0 ? 0.0 : excess * s.ScaleMmPerHour * SeasonalIntensity(localDay, s);
        }

        /// <summary>
        /// How much of the sky is covered, 0 clear to 1 overcast: the year's settled cover (<see cref="CloudMean"/>,
        /// <see cref="CloudAmplitude"/>) and the index on top of it, because a raining sky is an overcast one, read further
        /// down than the rain's threshold since cloud comes before the rain and clears after it. The index here is the
        /// weather's own, without the rain's seasonal shift: the cloud's own year is fitted to the record (M1.8c).
        /// </summary>
        public double CloudCover01(double localDay) => CloudCover01(localDay, CloudSettings.Default);

        public double CloudCover01(double localDay, in CloudSettings c)
            => SimMath.Clamp01(c.Mean + c.Amplitude * Math.Cos(2.0 * Math.PI * (SeasonDay(localDay) - c.CloudiestDay) / 365.0) + c.IndexGain * Index(localDay));

        /// <summary>
        /// How far the front pulls the air off the climate's curve, °C, negative behind a front: one term added to the curve
        /// rather than a second temperature model. It reads the frontal band alone and not the season's shift: the year's
        /// warming and cooling is the climate's, and v1, feeding the rain's season in here, damped it by 0.7 °C.
        /// </summary>
        public double AirAnomalyC(double localDay) => AirAnomalyC(localDay, ColdSnapDepthC);

        public double AirAnomalyC(double localDay, double depthC) => -depthC * FrontalIndex(localDay);

        /// <summary>
        /// What the front does to the wind, as a factor on the climate's daily figure: a front is windy, and the still days are
        /// between. The frontal band alone, without the showers and without the rain's seasonal shift (M1.8c): the wind is the
        /// air mass's, and the record's months, which the climate's wind now carries, already hold the seasons' fronts. Its
        /// mean over the weather is one and about a hundredth, the floor taking a little off the calmest days.
        /// </summary>
        public double WindFactor(double localDay) => Math.Max(0.35, 1.0 + 0.55 * FrontalIndex(localDay));

        /// <summary>How far the fronts move the dew point off the climate's month, °C (<see cref="DewPointIndexGainC"/>).</summary>
        public double DewPointShiftC(double localDay) => DewPointIndexGainC * Index(localDay);

        /// <summary>
        /// Where the wind blows from, degrees clockwise from north: a front's passage in the southern hemisphere, the wind in
        /// the north-west ahead of it and swinging to the south-west behind. Which side of the front the place is on is
        /// whether the index is rising or falling, a centred difference over six hours that needs no state.
        /// </summary>
        public double WindFromDeg(double localDay)
        {
            const double HalfSpanDays = 0.125;
            double slope = Index(localDay + HalfSpanDays) - Index(localDay - HalfSpanDays);
            return slope >= 0.0 ? 315.0 : 225.0;
        }

        /// <summary>
        /// Relative humidity, 0 to 1, from the air's temperature and the dew point at the instant (M1.8c): the saturation
        /// pressure at the dew point over that at the air's, so a morning near the night's minimum is damp and an afternoon
        /// dry with the same moisture in the air, as the lighthouse's 9 am and 3 pm readings are; near saturated in rain. M1.8a
        /// read one level off the index, the same at dawn and at three.
        /// </summary>
        public double RelativeHumidity01(double localDay, double airC, double dewPointC)
        {
            double humidity = Climate.SaturationVapourKPa(Math.Min(dewPointC, airC)) / Climate.SaturationVapourKPa(airC);
            double rain = RainRateMmPerHour(localDay);
            if (rain > 0.0) humidity = Math.Max(humidity, 0.92 + 0.08 * Math.Min(1.0, rain));
            return SimMath.Clamp01(humidity);
        }
    }
}
