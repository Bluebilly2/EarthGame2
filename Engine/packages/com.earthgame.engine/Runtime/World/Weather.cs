using System;

namespace EarthGame.Engine
{
    /// <summary>
    /// The sky at a place and an instant (M1.8a): the air with its cold snap in it, the wind and where it blows from, the
    /// cloud, the rain and the humidity, worked out once and read by everything that needs any of them. Ported from v1
    /// (`Sim/World/Weather.cs`).
    ///
    /// <para><b>One assembly.</b> v1's night forecast and the night its founder lived once disagreed, not about the
    /// arithmetic, which they shared, but about the readings each side gathered for it. The weather has the same shape of
    /// risk and takes the same cure: a reading of the sky is this struct, made only by <see cref="At"/>, so two readers
    /// cannot come to disagree about whether it is raining.</para>
    ///
    /// <para><b>A function of the world's seed and its clock.</b> <see cref="Climate"/> gives the curves and
    /// <see cref="Synoptic"/> the fronts, and both are pure, so the sky can be asked about an hour that has not happened (a
    /// warning before dark asks about five o'clock tomorrow), and a client holding the seed, the region and the clock its
    /// Welcome carried works out the sky the server does. Nothing of the weather is sent or saved.</para>
    /// </summary>
    public readonly struct Weather
    {
        /// <summary>The air's temperature, °C: the climate's curve with the front's cold snap in it.</summary>
        public readonly double AirC;

        /// <summary>
        /// What the front has done to the air, °C, negative behind a cold front. Carried beside <see cref="AirC"/>, which
        /// already holds it, because a night colder than this place usually is, and not merely cold, is weather and not a
        /// number.
        /// </summary>
        public readonly double AnomalyC;

        /// <summary>The wind at the place, m/s, before anything built has touched it.</summary>
        public readonly double WindMs;

        /// <summary>Where the wind blows from, degrees clockwise from north.</summary>
        public readonly double WindFromDeg;

        /// <summary>How much of the sky is covered, 0 clear to 1 overcast.</summary>
        public readonly double CloudCover01;

        /// <summary>Rain reaching the ground, mm/h; zero most of the time.</summary>
        public readonly double RainRateMmPerHour;

        /// <summary>The air's relative humidity, 0 to 1.</summary>
        public readonly double RelativeHumidity01;

        private Weather(double airC, double anomalyC, double windMs, double windFromDeg, double cloudCover01,
                        double rainRateMmPerHour, double relativeHumidity01)
        {
            AirC = airC;
            AnomalyC = anomalyC;
            WindMs = Math.Max(0.0, windMs);
            WindFromDeg = windFromDeg;
            CloudCover01 = SimMath.Clamp01(cloudCover01);
            RainRateMmPerHour = Math.Max(0.0, rainRateMmPerHour);
            RelativeHumidity01 = SimMath.Clamp01(relativeHumidity01);
        }

        /// <summary>Whether rain is reaching the ground at all.</summary>
        public bool IsRaining => RainRateMmPerHour > 0.0;

        /// <summary>
        /// The sky at an instant as seen from a place (<paramref name="sun"/>, the world's clock at a longitude), at a height
        /// above the sea and on ground as open as <paramref name="exposure01"/> (0 sheltered to 1 open), which only the wind
        /// reads.
        /// </summary>
        public static Weather At(Climate climate, Synoptic synoptic, SolarClock sun, double altitudeM, double exposure01)
        {
            if (climate == null) throw new ArgumentNullException(nameof(climate));
            if (synoptic == null) throw new ArgumentNullException(nameof(synoptic));
            if (sun == null) throw new ArgumentNullException(nameof(sun));
            double day = sun.World.LocalHours(sun.LongitudeDeg) / 24.0;
            // The cold snap is one term added to the climate's curve rather than a second model of the temperature, so
            // everything that reads the air inherits it.
            double anomaly = synoptic.AirAnomalyC(day);
            double air = climate.AirTemperatureC(sun.DayOfYear, sun.HourOfDay, altitudeM) + anomaly;
            double wind = climate.WindSpeedMs(sun.HourOfDay, exposure01) * synoptic.WindFactor(day);
            return new Weather(air, anomaly, wind, synoptic.WindFromDeg(day), synoptic.CloudCover01(day),
                               synoptic.RainRateMmPerHour(day), synoptic.RelativeHumidity01(day));
        }
    }
}
