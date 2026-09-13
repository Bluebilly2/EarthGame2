using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace EarthGame.Engine
{
    /// <summary>
    /// Years of the engine's weather at a place, written out for someone who does not trust the engine (M1.8a). The owner's
    /// <c>weather_check.py</c> reads them and works a weather station's monthly figures out of the raw samples with its own
    /// arithmetic. Format <c>eg2.weather</c>, version 1, contracted under ARCHITECTURE.md §10: a JSON sidecar and a raw file
    /// of 32-bit little-endian floats, a year for each world seed in turn, each step of each day, each field in the
    /// sidecar's order.
    ///
    /// <para>Each sample is taken at the middle of its step, so that none falls on midnight, where an instant worked back
    /// from local solar hours to the world's universal clock and forward again can land a hair's breadth inside the day
    /// before, and so that a step's rain is its rate at the step's middle for the step's length.</para>
    /// </summary>
    public static class WeatherYears
    {
        public const string Format = "eg2.weather";
        public const int Version = 1;
        public const string SidecarFile = "weather.json";
        public const string RawFile = "weather.f32";

        /// <summary>What each sample holds, in the order it is written: the air's temperature, °C, and the rain, mm/h.</summary>
        public static readonly string[] Fields = { "air_c", "rain_mm_per_hour" };

        /// <summary>
        /// Writes <paramref name="days"/> days of <paramref name="stepsPerDay"/> samples from the first local solar day of the
        /// year for each of <paramref name="seeds"/> world seeds from <paramref name="firstSeed"/>, at the region's centre, a
        /// height and an openness of ground, into a folder, replacing what it held under the two names.
        /// </summary>
        public static void Write(string folder, Region region, ulong firstSeed, int seeds, int days, int stepsPerDay,
                                 double altitudeM, double exposure01)
        {
            if (folder == null) throw new ArgumentNullException(nameof(folder));
            if (region == null) throw new ArgumentNullException(nameof(region));
            if (seeds < 1 || days < 1 || stepsPerDay < 1)
                throw new ArgumentOutOfRangeException(nameof(seeds), "seeds " + seeds + ", days " + days + " and steps a day " + stepsPerDay + " must each be at least one");
            Climate climate = Climate.ForRegion(region);
            Directory.CreateDirectory(folder);
            WorldClock clock = new WorldClock();
            SolarClock sun = SolarClock.ForRegion(region, clock);
            double offsetHours = WorldClock.OffsetHours(region.CentreLongitudeDeg);
            double stepHours = 24.0 / stepsPerDay;
            using (FileStream stream = new FileStream(Path.Combine(folder, RawFile), FileMode.Create, FileAccess.Write))
            using (BinaryWriter raw = new BinaryWriter(stream))
            {
                for (int s = 0; s < seeds; s++)
                {
                    Synoptic synoptic = new Synoptic(firstSeed + (ulong)s);
                    for (int day = 0; day < days; day++)
                        for (int step = 0; step < stepsPerDay; step++)
                        {
                            clock.SetTotalHours(day * 24.0 + (step + 0.5) * stepHours - offsetHours);
                            Weather weather = Weather.At(climate, synoptic, sun, altitudeM, exposure01);
                            raw.Write((float)weather.AirC);
                            raw.Write((float)weather.RainRateMmPerHour);
                        }
                }
            }
            File.WriteAllText(Path.Combine(folder, SidecarFile), Sidecar(region, firstSeed, seeds, days, stepsPerDay, altitudeM, exposure01) + "\n");
        }

        /// <summary>The sidecar: one JSON object, keys in a fixed order, numbers culture-invariant.</summary>
        private static string Sidecar(Region region, ulong firstSeed, int seeds, int days, int stepsPerDay, double altitudeM, double exposure01)
        {
            StringBuilder sb = new StringBuilder(512);
            sb.Append("{\"format\":\"").Append(Format).Append("\",\"version\":").Append(Version);
            sb.Append(",\"region\":\"").Append(region.Id).Append('"');
            Number(sb, "latitude_deg", region.CentreLatitudeDeg);
            Number(sb, "longitude_deg", region.CentreLongitudeDeg);
            Number(sb, "altitude_m", altitudeM);
            Number(sb, "exposure", exposure01);
            sb.Append(",\"first_seed\":").Append(firstSeed.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"seeds\":").Append(seeds.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"first_local_day\":0");
            sb.Append(",\"days\":").Append(days.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"steps_per_day\":").Append(stepsPerDay.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"sampled_at\":\"the middle of each step\"");
            sb.Append(",\"time_basis\":\"").Append(Almanac.TimeBasis).Append('"');
            sb.Append(",\"fields\":[");
            for (int i = 0; i < Fields.Length; i++) sb.Append(i == 0 ? "\"" : ",\"").Append(Fields[i]).Append('"');
            sb.Append("],\"dtype\":\"f32\",\"byte_order\":\"little\",\"raw\":\"").Append(RawFile).Append("\"}");
            return sb.ToString();
        }

        private static void Number(StringBuilder sb, string key, double value)
            => sb.Append(",\"").Append(key).Append("\":").Append(value.ToString("0.######", CultureInfo.InvariantCulture));
    }
}
