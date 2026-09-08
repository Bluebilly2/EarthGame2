using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace EarthGame.Engine
{
    /// <summary>
    /// A short name for a state, so two copies of it can be compared by a harness that never sees either
    /// (ARCHITECTURE §6). Every number is written as an integer at a fixed resolution — metres in micrometres,
    /// hours in nanohours — and the lines hashed with FNV-1a 64; what the lines are is stated here and nowhere
    /// else, so a client and a server that build the same lines get the same name. Sorted by key, so order of
    /// arrival does not enter. Integers, not a formatted double: the first full corpus (2026-09-08) found one
    /// mirror digest in 310 disagreeing with the server's for a body whose bits were identical on both ends,
    /// because the player's runtime prints the twelfth significant figure of some doubles differently from the
    /// server's, and a name that depends on who prints it is no name.
    /// </summary>
    public static class WorldDigest
    {
        /// <summary>Metres are named to the micrometre; hours to the nanohour (thirty-six microseconds).</summary>
        public const double MetreResolution = 1e-6;
        public const double HourResolution = 1e-9;

        /// <summary>The world: its clock, its tick, and every remembered body by player name.</summary>
        public static string World(WorldState world, IEnumerable<KeyValuePair<string, MoverState>> bodiesByName)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("clock ").Append(Fixed(world.Clock.TotalHours, HourResolution)).Append('\n');
            sb.Append("tick ").Append(world.Tick.ToString(CultureInfo.InvariantCulture)).Append('\n');
            List<KeyValuePair<string, MoverState>> sorted = new List<KeyValuePair<string, MoverState>>(bodiesByName);
            sorted.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
            foreach (KeyValuePair<string, MoverState> pair in sorted)
                AppendBody(sb, pair.Key, pair.Value);
            return Hex(Fnv1a64(sb.ToString()));
        }

        /// <summary>A set of bodies by session id, as a client's mirror and the server's record both hold them.</summary>
        public static string Bodies(IEnumerable<KeyValuePair<uint, MoverState>> bodiesBySession)
        {
            StringBuilder sb = new StringBuilder();
            List<KeyValuePair<uint, MoverState>> sorted = new List<KeyValuePair<uint, MoverState>>(bodiesBySession);
            sorted.Sort((a, b) => a.Key.CompareTo(b.Key));
            foreach (KeyValuePair<uint, MoverState> pair in sorted)
                AppendBody(sb, pair.Key.ToString(CultureInfo.InvariantCulture), pair.Value);
            return Hex(Fnv1a64(sb.ToString()));
        }

        /// <summary>The body's position and flags. Velocity is left out: it travels as float and is not state the world keeps.</summary>
        private static void AppendBody(StringBuilder sb, string key, MoverState body)
        {
            sb.Append(key).Append(' ')
              .Append(Fixed(body.East, MetreResolution)).Append(' ').Append(Fixed(body.Up, MetreResolution)).Append(' ').Append(Fixed(body.North, MetreResolution))
              .Append(body.Grounded ? " g" : " a").Append(body.Wading ? " w" : " d").Append(body.Stance == Stance.Crouching ? " c" : " s")
              .Append('\n');
        }

        /// <summary>The value as a whole number of resolution units, rounded to even; a NaN or an infinity names itself.</summary>
        public static string Fixed(double value, double resolution)
        {
            if (double.IsNaN(value)) return "nan";
            if (double.IsInfinity(value)) return value > 0 ? "inf" : "-inf";
            return ((long)Math.Round(value / resolution, MidpointRounding.ToEven)).ToString(CultureInfo.InvariantCulture);
        }

        public static ulong Fnv1a64(string text)
        {
            const ulong Offset = 14695981039346656037UL;
            const ulong Prime = 1099511628211UL;
            ulong hash = Offset;
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            for (int i = 0; i < bytes.Length; i++)
            {
                hash ^= bytes[i];
                hash *= Prime;
            }
            return hash;
        }

        public static string Hex(ulong value) => value.ToString("x16", CultureInfo.InvariantCulture);
    }
}
