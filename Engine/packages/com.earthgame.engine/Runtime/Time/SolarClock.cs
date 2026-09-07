using System;
using System.Globalization;

namespace EarthGame.Engine
{
    /// <summary>
    /// Where the sun is, and therefore what time it is and how cold it is about to get.
    ///
    /// <para>The founder wakes at 8 a.m. on 25 August at 35°S: late winter, about eleven hours of daylight left and
    /// an overnight low near freezing. That is not set dressing: it is the clock every survival pressure runs on,
    /// and the reason the first day is a race.</para>
    ///
    /// <para>Solar position follows the standard NOAA equations (declination, hour angle) so sunrise, sunset and
    /// the sun's height are right for the real date and latitude, which means a player who can read the sun can
    /// navigate and tell the time by it (GAME_DESIGN §3). Time is <b>local mean solar time</b>: noon is when the
    /// sun is highest here, there are no zones and no equation of time (see <see cref="WorldClock"/>). Ported
    /// from v1 (Assets/EarthGame/Sim/World/SolarClock.cs); the owner's solar_check verifier compares an
    /// independent formula against the <see cref="Almanac"/> line this class feeds.</para>
    /// </summary>
    public sealed class SolarClock
    {
        /// <summary>Real seconds per in-game day. Stated here for readers of this class; the value lives on <see cref="WorldClock"/>.</summary>
        public const double RealSecondsPerDay = WorldClock.RealSecondsPerDay;

        /// <summary>
        /// The absolute instant this is a view of. Every reading below is derived from it and this longitude; the
        /// clock holds no time of its own, so there is exactly one time in the game and every place is a different
        /// window onto it.
        /// </summary>
        public WorldClock World { get; }

        public double LatitudeDeg { get; }
        public double LongitudeDeg { get; }

        /// <summary>Day of the year, 1–365. 237 is 25 August.</summary>
        public int DayOfYear => World.LocalDayOfYear(LongitudeDeg);

        /// <summary>Local solar time in hours, 0–24.</summary>
        public double HourOfDay => World.LocalHourOfDay(LongitudeDeg);

        /// <summary>Whole days elapsed since waking.</summary>
        public int DaysElapsed => World.DaysElapsed;

        /// <summary>
        /// A clock at a place, started from a <b>local</b> reading, which is how a game begins: "eight in the
        /// morning at the wake point" is a local statement and the absolute instant is worked back out of it.
        /// </summary>
        public SolarClock(double latitudeDeg, double longitudeDeg, int startDayOfYear = 237, double startHour = 8.0)
            : this(latitudeDeg, longitudeDeg, WorldClock.FromLocal(startDayOfYear, startHour, longitudeDeg))
        {
        }

        /// <summary>
        /// A view of an existing world clock from a place. Two of these on one <see cref="WorldClock"/> are two
        /// places in the same instant, which is the whole reason the universal clock exists.
        /// </summary>
        public SolarClock(double latitudeDeg, double longitudeDeg, WorldClock world)
        {
            LatitudeDeg = latitudeDeg;
            LongitudeDeg = longitudeDeg;
            World = world ?? new WorldClock();
        }

        /// <summary>The sun as seen from a region's centre, on the world's one clock.</summary>
        public static SolarClock ForRegion(Region region, WorldClock world)
            => new SolarClock(region.CentreLatitudeDeg, region.CentreLongitudeDeg, world);

        /// <summary>The sun at a region's canonical wake.</summary>
        public static SolarClock AtWake(Region region) => ForRegion(region, region.WakeClock());

        /// <summary>
        /// Advances by real seconds, which advances the world, not this view of it. Rolling over midnight is
        /// arithmetic on the absolute instant rather than a loop here, so a step larger than a day cannot skip a
        /// day the way the old loop could.
        /// </summary>
        public void Advance(double realSeconds) => World.Advance(realSeconds);

        /// <summary>Solar declination in degrees: the sun's tilt north or south, driving the seasons.</summary>
        public double DeclinationDeg => DeclinationDegFor(DayOfYear);

        /// <summary>Declination for a day of the year (Spencer's Fourier fit, as NOAA publishes it).</summary>
        public static double DeclinationDegFor(int dayOfYear)
        {
            double g = 2.0 * Math.PI / 365.0 * (dayOfYear - 1);
            return 0.396372
                   - 22.91327 * Math.Cos(g) + 4.02543 * Math.Sin(g)
                   - 0.387205 * Math.Cos(2 * g) + 0.051967 * Math.Sin(2 * g)
                   - 0.154527 * Math.Cos(3 * g) + 0.084798 * Math.Sin(3 * g);
        }

        /// <summary>Sun's height above the horizon in degrees. Negative is below it.</summary>
        public double SolarElevationDeg => ElevationDegFor(LatitudeDeg, DeclinationDeg, HourOfDay);

        /// <summary>Elevation for a latitude, a declination and a local solar hour.</summary>
        public static double ElevationDegFor(double latitudeDeg, double declinationDeg, double hourOfDay)
        {
            double lat = latitudeDeg * GeoMath.DegToRad;
            double dec = declinationDeg * GeoMath.DegToRad;
            double hourAngle = (hourOfDay - 12.0) * 15.0 * GeoMath.DegToRad;
            double sinAlt = Math.Sin(lat) * Math.Sin(dec) + Math.Cos(lat) * Math.Cos(dec) * Math.Cos(hourAngle);
            return Math.Asin(SimMath.Clamp(sinAlt, -1.0, 1.0)) * GeoMath.RadToDeg;
        }

        /// <summary>Compass bearing of the sun in degrees, 0 = north, 90 = east.</summary>
        public double SolarAzimuthDeg
        {
            get
            {
                double lat = LatitudeDeg * GeoMath.DegToRad;
                double dec = DeclinationDeg * GeoMath.DegToRad;
                double hourAngle = (HourOfDay - 12.0) * 15.0 * GeoMath.DegToRad;
                double alt = SolarElevationDeg * GeoMath.DegToRad;
                double cosAz = (Math.Sin(dec) - Math.Sin(alt) * Math.Sin(lat)) / (Math.Cos(alt) * Math.Cos(lat));
                double az = Math.Acos(SimMath.Clamp(cosAz, -1.0, 1.0)) * GeoMath.RadToDeg;
                return hourAngle > 0.0 ? 360.0 - az : az;
            }
        }

        /// <summary>The sun's elevation at which the day is counted as begun or ended: the refracted upper limb.</summary>
        public const double HorizonElevationDeg = -0.833;

        public bool IsDaylight => SolarElevationDeg > HorizonElevationDeg;
        public bool IsCivilTwilight => SolarElevationDeg <= HorizonElevationDeg && SolarElevationDeg > -6.0;
        public bool IsNight => SolarElevationDeg <= -6.0;

        /// <summary>Hours of daylight today, from the sunrise hour angle.</summary>
        public double DaylightHours => DaylightHoursFor(LatitudeDeg, DeclinationDeg);

        /// <summary>Daylight for a latitude and a declination; 0 in polar night, 24 under the midnight sun.</summary>
        public static double DaylightHoursFor(double latitudeDeg, double declinationDeg)
        {
            double lat = latitudeDeg * GeoMath.DegToRad;
            double dec = declinationDeg * GeoMath.DegToRad;
            double cosH = (Math.Sin(HorizonElevationDeg * GeoMath.DegToRad) - Math.Sin(lat) * Math.Sin(dec)) / (Math.Cos(lat) * Math.Cos(dec));
            if (cosH >= 1.0) return 0.0;
            if (cosH <= -1.0) return 24.0;
            return 2.0 * Math.Acos(cosH) * GeoMath.RadToDeg / 15.0;
        }

        /// <summary>
        /// Local hour the sun comes up, and the hour it goes down. Solar noon here is noon (that is what local
        /// solar time means), so both fall out of the day's length, half of it either side. In v1 this arithmetic
        /// was written out by hand in three places, each keeping only the half it needed; three copies of one
        /// fact, free to drift the moment the day length was computed differently.
        /// </summary>
        public double SunriseHour => SunriseHourFor(DaylightHours);

        /// <inheritdoc cref="SunriseHour"/>
        public double SunsetHour => SunsetHourFor(DaylightHours);

        /// <summary>The same two for a caller with a day length but no clock (the animals' rhythms take one).</summary>
        public static double SunriseHourFor(double daylightHours) => 12.0 - daylightHours * 0.5;

        /// <inheritdoc cref="SunriseHourFor"/>
        public static double SunsetHourFor(double daylightHours) => 12.0 + daylightHours * 0.5;

        /// <summary>Hours until the sun sets. Zero once it has.</summary>
        public double HoursUntilSunset => Math.Max(0.0, SunsetHour - HourOfDay);

        /// <summary>
        /// How many days on the next dawn is from this instant: none if the sun has not come up yet today, one if
        /// it already has. Written for the wake after a death in the small hours, which in v1 moved the calendar on
        /// by a day whatever the hour was, so a run that woke from it counted a day it had not lived.
        /// </summary>
        public int DaysToNextDawn => DaysToNextDawnFrom(HourOfDay, DaylightHours);

        /// <inheritdoc cref="DaysToNextDawn"/>
        public static int DaysToNextDawnFrom(double hourOfDay, double daylightHours)
            => hourOfDay < SunriseHourFor(daylightHours) ? 0 : 1;

        /// <summary>The local time as a founder might say it: HH:MM.</summary>
        public string ClockText
        {
            get
            {
                int h = (int)HourOfDay;
                int m = (int)((HourOfDay - h) * 60.0);
                return h.ToString("00", CultureInfo.InvariantCulture) + ":" + m.ToString("00", CultureInfo.InvariantCulture);
            }
        }
    }
}
