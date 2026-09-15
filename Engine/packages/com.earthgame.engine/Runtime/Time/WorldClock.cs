using System;

namespace EarthGame.Engine
{
    /// <summary>
    /// The one clock. Universal, absolute, and never shown to the founder.
    ///
    /// <para>Everything in the world reads its time from here. What the founder sees is a
    /// <b>local</b> time derived from this instant and where they are standing — because on a real
    /// Earth those are different things, and the difference is the point. Local noon is when the
    /// sun is highest <i>where you are</i>; walk four hundred kilometres east and it happens
    /// twenty-six minutes earlier. Only an absolute instant can be the shared truth two places
    /// disagree about.</para>
    ///
    /// <para>Held as UTC, because that is what an absolute instant on Earth is. The founder will
    /// never encounter it: they have the sun. It exists so that when there are two settlements, or
    /// a traveller and a home camp, or a machine logging when something happened, they are all
    /// talking about the same moment. In v2 it is also the server's clock: clients never own time.</para>
    ///
    /// <para>The local view is the solar clock, which is a window onto this and holds no time of its own.
    /// Ported verbatim from v1 (Assets/EarthGame/Sim/World/WorldClock.cs).</para>
    /// </summary>
    public sealed class WorldClock
    {
        /// <summary>Real seconds per in-game day at a time scale of one. Thirty minutes.</summary>
        public const double RealSecondsPerDay = 1800.0;

        /// <summary>Hours of longitude per degree: the Earth turns 15° an hour.</summary>
        public const double DegreesPerHour = 15.0;

        /// <summary>
        /// Hours since the epoch — 00:00 UTC on day one of the year. Monotonic, and the only
        /// piece of state in here.
        /// </summary>
        public double TotalHours { get; private set; }

        /// <summary>Where the clock was started, so elapsed days can be counted from it.</summary>
        public double StartedAtHours { get; private set; }

        public WorldClock(double totalHours = 0.0)
        {
            TotalHours = totalHours;
            StartedAtHours = totalHours;
        }

        /// <summary>
        /// A clock reading a given <b>local solar</b> time at a given longitude — which is how a
        /// game actually starts, because "eight in the morning at the wake point" is a local
        /// statement and the absolute instant has to be worked back out of it.
        /// </summary>
        public static WorldClock FromLocal(int dayOfYear, double localHour, double longitudeDeg)
        {
            double local = (dayOfYear - 1) * 24.0 + localHour;
            return new WorldClock(local - longitudeDeg / DegreesPerHour);
        }

        /// <summary>
        /// The same, for a run that is already <paramref name="daysElapsed"/> days old.
        ///
        /// <para><see cref="DaysElapsed"/> is measured from where the clock was started, so a
        /// reloaded game began counting again from zero: in v1 a founder five days in was told Day 1,
        /// and the next save wrote that zero back over the five.</para>
        ///
        /// <para>Winding the start back rather than storing a separate counter keeps the one piece
        /// of state this class has, and keeps elapsed days a fact about the clock rather than a
        /// number kept beside it that can disagree.</para>
        /// </summary>
        public static WorldClock Resumed(int dayOfYear, double localHour, double longitudeDeg,
                                         int daysElapsed)
        {
            WorldClock clock = FromLocal(dayOfYear, localHour, longitudeDeg);
            clock.StartedAtHours -= Math.Max(0, daysElapsed) * 24.0;
            return clock;
        }

        /// <summary>Restores a clock from its two persisted numbers.</summary>
        public static WorldClock Restore(double totalHours, double startedAtHours)
        {
            WorldClock clock = new WorldClock(totalHours);
            clock.StartedAtHours = startedAtHours;
            return clock;
        }

        /// <summary>
        /// How many times faster than the game's own rate the clock runs, for the dev tools (M1.D): one as the game runs
        /// it, nought holds the sky still. A developer's setting lasts the session; it is never saved.
        /// </summary>
        public double Scale { get; set; } = 1.0;

        /// <summary>Advances by real seconds at the game's rate times <see cref="Scale"/>. The single place in-game time moves forward.</summary>
        public void Advance(double realSeconds)
        {
            if (realSeconds <= 0.0 || Scale <= 0.0) return;
            TotalHours += realSeconds * Scale * (24.0 / RealSecondsPerDay);
        }

        /// <summary>
        /// The world's days that <paramref name="realSeconds"/> come to at the game's rate times <see cref="Scale"/>: a
        /// founder's body runs on this clock (FP.1), so a held clock holds the body and a sped one dries it faster.
        /// </summary>
        public double DaysFor(double realSeconds) =>
            realSeconds <= 0.0 || Scale <= 0.0 ? 0.0 : realSeconds * Scale / RealSecondsPerDay;

        /// <summary>Moves the clock to an absolute instant. For the dev tools, not for the game.</summary>
        public void SetTotalHours(double totalHours) => TotalHours = totalHours;

        // ---- the universal reading ----

        /// <summary>Day of the year at Greenwich, 1–365.</summary>
        public int UtcDayOfYear => (int)Math.Floor(TotalHours / 24.0) % 365 + 1;

        /// <summary>Hour of the day at Greenwich, 0–24.</summary>
        public double UtcHourOfDay => Mod(TotalHours, 24.0);

        /// <summary>Whole days since the clock was started, wherever it was started.</summary>
        public int DaysElapsed => (int)Math.Floor((TotalHours - StartedAtHours) / 24.0);

        /// <summary>The absolute instant, written out. Dev tools only.</summary>
        public string UtcText
        {
            get
            {
                int h = (int)UtcHourOfDay;
                int m = (int)((UtcHourOfDay - h) * 60.0);
                return "day " + UtcDayOfYear + "  " + h.ToString("00") + ":" + m.ToString("00")
                     + " UTC";
            }
        }

        // ---- the local reading ----

        /// <summary>
        /// Local apparent solar hours at a longitude, as a continuous count from the epoch.
        ///
        /// <para>Mean solar time, not clock time: no time zones, no daylight saving, and no
        /// equation of time. The founder has no watch — they have the sun, and noon is when it is
        /// highest. Zones are a nineteenth-century railway invention and there are no railways
        /// yet.</para>
        /// </summary>
        public double LocalHours(double longitudeDeg)
            => TotalHours + longitudeDeg / DegreesPerHour;

        /// <summary>Local hour of the day at a longitude, 0–24.</summary>
        public double LocalHourOfDay(double longitudeDeg) => Mod(LocalHours(longitudeDeg), 24.0);

        /// <summary>Local day of the year at a longitude, 1–365.</summary>
        public int LocalDayOfYear(double longitudeDeg)
        {
            double days = Math.Floor(LocalHours(longitudeDeg) / 24.0);
            return (int)Mod(days, 365.0) + 1;
        }

        /// <summary>
        /// How far ahead of Greenwich a longitude runs, in hours. Positive to the east.
        /// </summary>
        public static double OffsetHours(double longitudeDeg) => longitudeDeg / DegreesPerHour;

        private static double Mod(double value, double period)
        {
            double r = value % period;
            return r < 0.0 ? r + period : r;
        }
    }
}
