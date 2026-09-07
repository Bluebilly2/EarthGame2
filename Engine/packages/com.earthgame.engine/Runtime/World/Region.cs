namespace EarthGame.Engine
{
    /// <summary>
    /// A bounded piece of the real Earth the game is set in: where it is, how big it is, and when and where the
    /// founder wakes there. One owner for the numbers every other file used to repeat (the server host, the tests
    /// and the Unity bootstrap each carried "150.675" before this class existed; three copies of one fact is the
    /// named bug shape). The extent is stated here and nowhere else; a source scan enforces that no other file
    /// spells out a metre literal for the world's size (STANDARDS 2; plan §4.2).
    /// </summary>
    public sealed class Region
    {
        /// <summary>Stable id used in saves, the wire and the region raster folder.</summary>
        public string Id { get; }
        /// <summary>What the place is called, using landform names (CANON ruling 8).</summary>
        public string DisplayName { get; }
        /// <summary>The tangent plane's origin, degrees, north and east positive.</summary>
        public double CentreLatitudeDeg { get; }
        public double CentreLongitudeDeg { get; }
        /// <summary>Side of the square region in metres; the world ends here.</summary>
        public double ExtentM { get; }
        /// <summary>The canonical wake: day of the year (non-leap) and local solar hour.</summary>
        public int WakeDayOfYear { get; }
        public double WakeLocalHour { get; }
        /// <summary>Where the founder wakes, degrees. The world-creation scorer (M1.2) may move it within the criteria.</summary>
        public double WakeLatitudeDeg { get; }
        public double WakeLongitudeDeg { get; }

        public Region(string id, string displayName, double centreLatitudeDeg, double centreLongitudeDeg,
                      double extentM, int wakeDayOfYear, double wakeLocalHour, double wakeLatitudeDeg, double wakeLongitudeDeg)
        {
            Id = id;
            DisplayName = displayName;
            CentreLatitudeDeg = centreLatitudeDeg;
            CentreLongitudeDeg = centreLongitudeDeg;
            ExtentM = extentM;
            WakeDayOfYear = wakeDayOfYear;
            WakeLocalHour = wakeLocalHour;
            WakeLatitudeDeg = wakeLatitudeDeg;
            WakeLongitudeDeg = wakeLongitudeDeg;
        }

        /// <summary>
        /// The first world: the southern shore of Jervis Bay, NSW (plan §4.2; CANON ruling 8). 8 × 8 km centred at
        /// 35.140°S 150.675°E; the founder wakes at 8 am local solar time on 25 August (day 237 of a non-leap year)
        /// in the swale behind Cave Beach, about 35.159°S 150.6485°E.
        /// </summary>
        public static readonly Region Bherwerre = new Region(
            "bherwerre", "Bherwerre Peninsula, Jervis Bay", -35.140, 150.675, 8000.0, 237, 8.0, -35.159, 150.6485);

        /// <summary>The clock at the moment the founder wakes here.</summary>
        public WorldClock WakeClock() => WorldClock.FromLocal(WakeDayOfYear, WakeLocalHour, CentreLongitudeDeg);

        /// <summary>Half the side: positions are valid within ±this of the centre on each axis.</summary>
        public double HalfExtentM => ExtentM * 0.5;

        /// <summary>The region with this id, or null. Unknown ids are the caller's problem to report, never a default.</summary>
        public static Region ById(string id)
        {
            return id == Bherwerre.Id ? Bherwerre : null;
        }
    }
}
