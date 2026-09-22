namespace EarthGame.Engine
{
    /// <summary>
    /// A bounded piece of the real Earth the game is set in: where it is, how big it is, and when the founder
    /// wakes there. Where in it they wake is the world's to choose (`WakeScorer`), not the region's: the point this
    /// class once named was withdrawn by CANON ruling 20 (2026-09-10). One owner for the numbers every other file used to repeat (the server host, the tests
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

        public Region(string id, string displayName, double centreLatitudeDeg, double centreLongitudeDeg,
                      double extentM, int wakeDayOfYear, double wakeLocalHour)
        {
            Id = id;
            DisplayName = displayName;
            CentreLatitudeDeg = centreLatitudeDeg;
            CentreLongitudeDeg = centreLongitudeDeg;
            ExtentM = extentM;
            WakeDayOfYear = wakeDayOfYear;
            WakeLocalHour = wakeLocalHour;
        }

        /// <summary>
        /// The first world: the southern shore of Jervis Bay, NSW (plan §4.2; CANON ruling 8). 8 × 8 km centred at
        /// 35.140°S 150.675°E; the founder wakes at 8 am local solar time on 25 August (day 237 of a non-leap year),
        /// wherever the world's wake scorer puts them.
        /// </summary>
        public static readonly Region Bherwerre = new Region(
            "bherwerre", "Bherwerre Peninsula, Jervis Bay", -35.140, 150.675, 8000.0, 237, 8.0);

        /// <summary>
        /// The beta's second region (CANON ruling 43, 2026-09-22; WG.2): the Kangaroo Valley under the Morton plateau's escarpment,
        /// Fitzroy Falls in its box, no coast. The same late-winter wake as Bherwerre's. Its station is Nowra's when the world is
        /// built; until then <c>Climate.HasRecordFor</c> says no and a world here has no weather.
        /// </summary>
        public static readonly Region KangarooValley = new Region(
            "kangaroo-valley", "Kangaroo Valley, Fitzroy Falls", -34.660, 150.500, 8000.0, 237, 8.0);

        /// <summary>The clock at the moment the founder wakes here.</summary>
        public WorldClock WakeClock() => WorldClock.FromLocal(WakeDayOfYear, WakeLocalHour, CentreLongitudeDeg);

        /// <summary>Half the side: positions are valid within ±this of the centre on each axis.</summary>
        public double HalfExtentM => ExtentM * 0.5;

        /// <summary>The region with this id, or null. Unknown ids are the caller's problem to report, never a default.</summary>
        public static Region ById(string id)
        {
            return id == Bherwerre.Id ? Bherwerre : id == KangarooValley.Id ? KangarooValley : null;
        }
    }
}
