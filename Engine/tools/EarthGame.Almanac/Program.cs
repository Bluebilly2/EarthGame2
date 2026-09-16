using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using EarthGame.Engine;

namespace EarthGame.Almanac
{
    /// <summary>
    /// Prints the engine's sun as eg2.almanac lines (format contract in ARCHITECTURE.md §10). Arguments follow the
    /// +key value convention of the server host: +region bherwerre (the default place and wake hour), or +lat and
    /// +lon with +hour for the wake reading; +day N, optionally +to M for a range of days. With +weather and a folder it
    /// writes years of the engine's weather at the region's centre there instead, as eg2.weather (M1.8a): +first the
    /// first world seed (1000), +seeds how many (256), +days (365), +steps a day (48), +altitude metres (the height of the
    /// weather station the region's climate stands on) and +exposure 0 to 1 (1, open ground). With +fit rain|cloud|depth|
    /// widening|all it fits the weather's settings to the lighthouse's record and prints them (<see cref="Fit"/>, M1.8c), on
    /// +first and +seeds world seeds (5000, 128). Exit 2 on a bad argument.
    /// </summary>
    public static class Program
    {
        public static int Main(string[] args)
        {
            Dictionary<string, string> a = ParseArgs(args);
            if (a.ContainsKey("fit")) return Fit.Run(a, ULong(a, "first", 5000UL), Int(a, "seeds", 128));
            if (a.ContainsKey("weather")) return WriteWeather(a);
            Region region = Region.ById(Str(a, "region", Region.Bherwerre.Id));
            if (region == null && !(a.ContainsKey("lat") && a.ContainsKey("lon")))
            {
                Console.Error.WriteLine("unknown region '" + Str(a, "region", "") + "'; known: " + Region.Bherwerre.Id + " (or give +lat and +lon)");
                return 2;
            }
            double lat = Dbl(a, "lat", region == null ? double.NaN : region.CentreLatitudeDeg);
            double lon = Dbl(a, "lon", region == null ? double.NaN : region.CentreLongitudeDeg);
            double hour = Dbl(a, "hour", region == null ? 8.0 : region.WakeLocalHour);
            int day = Int(a, "day", region == null ? 1 : region.WakeDayOfYear);
            int to = Int(a, "to", day);
            if (double.IsNaN(lat) || double.IsNaN(lon) || day < 1 || day > 365 || to < day || to > 365)
            {
                Console.Error.WriteLine("usage: +region bherwerre | +lat -35.14 +lon 150.675 [+hour 8] +day 1..365 [+to N]");
                return 2;
            }
            for (int d = day; d <= to; d++)
                Console.WriteLine(EarthGame.Engine.Almanac.ToJsonLine(EarthGame.Engine.Almanac.Compute(lat, lon, d, hour), hour));
            return 0;
        }

        private static int WriteWeather(Dictionary<string, string> a)
        {
            Region region = Region.ById(Str(a, "region", Region.Bherwerre.Id));
            if (region == null)
            {
                Console.Error.WriteLine("unknown region '" + Str(a, "region", "") + "'; known: " + Region.Bherwerre.Id);
                return 2;
            }
            Climate climate;
            try
            {
                climate = Climate.ForRegion(region);
            }
            catch (ArgumentException ex)
            {
                Console.Error.WriteLine(ex.Message);
                return 2;
            }
            string folder = a["weather"];
            ulong first = ULong(a, "first", 1000UL);
            int seeds = Int(a, "seeds", 256), days = Int(a, "days", 365), steps = Int(a, "steps", 48);
            double altitude = Dbl(a, "altitude", climate.StationElevationM);
            double exposure = Dbl(a, "exposure", 1.0);
            if (folder == "true" || seeds < 1 || days < 1 || steps < 1 || exposure < 0.0 || exposure > 1.0)
            {
                Console.Error.WriteLine("usage: +weather <folder> [+region bherwerre] [+first N] [+seeds N] [+days N] [+steps N a day] [+altitude metres] [+exposure 0..1]");
                return 2;
            }
            WeatherYears.Write(folder, region, first, seeds, days, steps, altitude, exposure);
            Console.WriteLine("wrote " + seeds + " seeds of " + days + " days at " + steps + " steps a day to " + Path.GetFullPath(folder));
            return 0;
        }

        private static Dictionary<string, string> ParseArgs(string[] args)
        {
            Dictionary<string, string> map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < args.Length; i++)
            {
                if (!args[i].StartsWith("+", StringComparison.Ordinal)) continue;
                bool hasValue = i + 1 < args.Length && !args[i + 1].StartsWith("+", StringComparison.Ordinal);
                map[args[i].Substring(1)] = hasValue ? args[++i] : "true";
            }
            return map;
        }

        private static string Str(Dictionary<string, string> a, string key, string fallback)
            => a.TryGetValue(key, out string v) ? v : fallback;

        private static double Dbl(Dictionary<string, string> a, string key, double fallback)
            => a.TryGetValue(key, out string v) && double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out double d) ? d : fallback;

        private static int Int(Dictionary<string, string> a, string key, int fallback)
            => a.TryGetValue(key, out string v) && int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out int i) ? i : fallback;

        private static ulong ULong(Dictionary<string, string> a, string key, ulong fallback)
            => a.TryGetValue(key, out string v) && ulong.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out ulong u) ? u : fallback;
    }
}
