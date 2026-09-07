using System;
using System.Collections.Generic;
using System.Globalization;
using EarthGame.Engine;

namespace EarthGame.Almanac
{
    /// <summary>
    /// Prints the engine's sun as eg2.almanac lines (format contract in ARCHITECTURE.md §10). Arguments follow the
    /// +key value convention of the server host: +region bherwerre (the default place and wake hour), or +lat and
    /// +lon with +hour for the wake reading; +day N, optionally +to M for a range of days. Exit 2 on a bad argument.
    /// </summary>
    public static class Program
    {
        public static int Main(string[] args)
        {
            Dictionary<string, string> a = ParseArgs(args);
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
    }
}
