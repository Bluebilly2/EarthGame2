using System.Globalization;
using System.Text;

namespace EarthGame.Engine
{
    /// <summary>One day's sun at one place, as the engine computes it.</summary>
    public struct AlmanacDay
    {
        public int DayOfYear;
        public double LatitudeDeg;
        public double LongitudeDeg;
        public double DeclinationDeg;
        public double DaylightHours;
        /// <summary>Local mean solar hours (no zone, no equation of time), the basis every reading here shares.</summary>
        public double SunriseLocalHour;
        public double SunsetLocalHour;
        public double NoonElevationDeg;
        /// <summary>The sun's elevation and bearing at the region's wake hour, for a check against the sky the client draws.</summary>
        public double WakeElevationDeg;
        public double WakeAzimuthDeg;
    }

    /// <summary>
    /// The engine's sun, written out for someone who does not trust the engine. The owner's <c>solar_check.py</c>
    /// reads the <see cref="ToJsonLine"/> form (format <c>eg2.almanac</c>, version 1: a contracted format under
    /// ARCHITECTURE.md §10) and compares it against a formula of its own. The times are local mean solar hours,
    /// so an independent NOAA calculation must drop its zone offset and its equation of time before comparing;
    /// the line says so in its <c>time_basis</c> field rather than leaving the reader to discover it.
    /// </summary>
    public static class Almanac
    {
        public const string Format = "eg2.almanac";
        public const int Version = 1;
        public const string TimeBasis = "local mean solar time; no zone offset, no equation of time";

        public static AlmanacDay Compute(double latitudeDeg, double longitudeDeg, int dayOfYear, double wakeHour)
        {
            double declination = SolarClock.DeclinationDegFor(dayOfYear);
            double daylight = SolarClock.DaylightHoursFor(latitudeDeg, declination);
            SolarClock atWake = new SolarClock(latitudeDeg, longitudeDeg, dayOfYear, wakeHour);
            AlmanacDay d;
            d.DayOfYear = dayOfYear;
            d.LatitudeDeg = latitudeDeg;
            d.LongitudeDeg = longitudeDeg;
            d.DeclinationDeg = declination;
            d.DaylightHours = daylight;
            d.SunriseLocalHour = SolarClock.SunriseHourFor(daylight);
            d.SunsetLocalHour = SolarClock.SunsetHourFor(daylight);
            d.NoonElevationDeg = SolarClock.ElevationDegFor(latitudeDeg, declination, 12.0);
            d.WakeElevationDeg = atWake.SolarElevationDeg;
            d.WakeAzimuthDeg = atWake.SolarAzimuthDeg;
            return d;
        }

        /// <summary>The region's canonical wake day at its centre.</summary>
        public static AlmanacDay ForRegion(Region region, int dayOfYear)
            => Compute(region.CentreLatitudeDeg, region.CentreLongitudeDeg, dayOfYear, region.WakeLocalHour);

        /// <summary>One JSON object on one line, keys in a fixed order, numbers culture-invariant with six decimals.</summary>
        public static string ToJsonLine(AlmanacDay d, double wakeHour)
        {
            StringBuilder sb = new StringBuilder(320);
            sb.Append("{\"format\":\"").Append(Format).Append("\",\"version\":").Append(Version);
            sb.Append(",\"time_basis\":\"").Append(TimeBasis).Append('"');
            sb.Append(",\"day_of_year\":").Append(d.DayOfYear.ToString(CultureInfo.InvariantCulture));
            Field(sb, "latitude_deg", d.LatitudeDeg);
            Field(sb, "longitude_deg", d.LongitudeDeg);
            Field(sb, "declination_deg", d.DeclinationDeg);
            Field(sb, "daylight_hours", d.DaylightHours);
            Field(sb, "sunrise_local_hour", d.SunriseLocalHour);
            Field(sb, "sunset_local_hour", d.SunsetLocalHour);
            Field(sb, "noon_elevation_deg", d.NoonElevationDeg);
            Field(sb, "wake_local_hour", wakeHour);
            Field(sb, "wake_elevation_deg", d.WakeElevationDeg);
            Field(sb, "wake_azimuth_deg", d.WakeAzimuthDeg);
            sb.Append('}');
            return sb.ToString();
        }

        private static void Field(StringBuilder sb, string key, double value)
            => sb.Append(",\"").Append(key).Append("\":").Append(value.ToString("0.000000", CultureInfo.InvariantCulture));
    }
}
