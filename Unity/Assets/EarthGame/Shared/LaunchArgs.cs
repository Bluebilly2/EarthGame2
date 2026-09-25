using System;
using System.Collections.Generic;
using System.Globalization;

namespace EarthGame.Shared
{
    /// <summary>
    /// The process's <c>-eg-*</c> command-line arguments, parsed once. A key followed by a value takes it; a key
    /// followed by another key (or nothing) is a flag. Everything the bootstrap, the client and the recorder read
    /// from the command line comes through here, so the set of arguments has one owner and one spelling.
    ///
    /// <para>A dash before a digit is a sign, not a key: <c>-eg-rock -1595.20,3452.04</c> takes the west half's negative
    /// east as its value (BF.4 stage three, 2026-09-25, when the rocks scenario's first run lost its rock to the rule that any
    /// word with a leading dash starts the next key). No key of this game's or Unity's starts with a dash and a digit.</para>
    /// </summary>
    public static class LaunchArgs
    {
        private static Dictionary<string, string> _map;

        private static Dictionary<string, string> Map
        {
            get
            {
                if (_map == null) _map = Parse(Environment.GetCommandLineArgs());
                return _map;
            }
        }

        /// <summary>Parses an argument vector; public so a test can feed one in.</summary>
        public static Dictionary<string, string> Parse(string[] args)
        {
            Dictionary<string, string> map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (args == null) return map;
            for (int i = 0; i < args.Length; i++)
            {
                string key = args[i];
                if (!key.StartsWith("-eg-", StringComparison.OrdinalIgnoreCase)) continue;
                bool hasValue = i + 1 < args.Length && (!args[i + 1].StartsWith("-", StringComparison.Ordinal) || IsSigned(args[i + 1]));
                map[key.Substring(4)] = hasValue ? args[++i] : "true";
            }
            return map;
        }

        /// <summary>Whether a word is a negative number rather than a key: a dash and then a digit.</summary>
        private static bool IsSigned(string word) => word.Length > 1 && word[0] == '-' && char.IsDigit(word[1]);

        public static bool Has(string key) => Map.ContainsKey(key);

        public static string Get(string key, string fallback) => Map.TryGetValue(key, out string v) ? v : fallback;

        public static int GetInt(string key, int fallback)
            => Map.TryGetValue(key, out string v) && int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out int i) ? i : fallback;

        public static double GetDouble(string key, double fallback)
            => Map.TryGetValue(key, out string v) && double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out double d) ? d : fallback;

        public static ulong GetULong(string key, ulong fallback)
            => Map.TryGetValue(key, out string v) && ulong.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out ulong u) ? u : fallback;
    }
}
