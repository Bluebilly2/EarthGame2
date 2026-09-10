namespace EarthGame.ClientCore
{
    /// <summary>
    /// The scripted routes the scenarios walk, in local metres of the Bherwerre region. Every leg is legal input
    /// over walkable ground, so every correction on it is a false positive (N2); the named divergence segments are
    /// the bank and the shore.
    ///
    /// <para>The wake loop is laid around the wake the world-creation scorer chooses on the gate world (seed 1347,
    /// east −1352 north 1904), and the corpus runs every scenario on a copy of that world, so the founder wakes 66 m
    /// from the loop's first point. Surveyed on that world's layers on 2026-09-10 and checked every 2 m: no leg
    /// crosses water; the shore runs south along the bay 4 to 12 m from the sea on ground under 0.6 m; the bank is
    /// the slope east of the wake, a quarter of it at 22–28° and none of it over 28.5°, which is the bank the corpus
    /// was specified with on 2026-09-08; the plains stay under 12°, bending round the creeks that cut the ground.</para>
    ///
    /// <para>The first loop (2026-09-08) was laid around the region's stated wake point, which the corpus's server
    /// used as its spawn while it ran on the bare bake. M1.2 moved real worlds' wakes 4 km from that point and CANON
    /// ruling 20 withdrew it. A change to the layer rules can move the scorer's wake off this loop too, and
    /// join_check then says the route never reached the bank or the shore.</para>
    ///
    /// <para>The first re-laid bank ran along the scarp's face, 37% of it over 28° and up to 37.5°, and the server
    /// corrected founders on it about thirty times a minute over the wire and seven in SOLO, finding them "below the
    /// ground" by one to six metres (walk of 2026-09-10). That is the named defect class on steep ground, recorded in
    /// DEBTS.md; it is not what N2's bank measures, so the bank here is held to the slope N2 was specified on.</para>
    /// </summary>
    public static class Routes
    {
        /// <summary>About 1.35 km: south along the shore, round the creeks to the bank, down it, and home.</summary>
        public static Waypoint[] WakeLoop()
        {
            return new[]
            {
                new Waypoint(-1416.0, 1888.0, "return", true),
                new Waypoint(-1432.0, 1728.0, "shore", false),
                new Waypoint(-1320.0, 1776.0, "plain", true),
                new Waypoint(-1304.0, 1776.0, "plain", true),
                new Waypoint(-1288.0, 1760.0, "plain", true),
                new Waypoint(-1192.0, 1872.0, "plain", true),
                new Waypoint(-1176.0, 1920.0, "plain", true),
                new Waypoint(-1224.0, 2000.0, "plain", true),
                new Waypoint(-1192.0, 2064.0, "plain", true),
                new Waypoint(-1192.0, 2080.0, "plain", true),
                new Waypoint(-1208.0, 2096.0, "plain", true),
                new Waypoint(-1224.0, 2096.0, "plain", true),
                new Waypoint(-1240.0, 1936.0, "bank", false),
                new Waypoint(-1176.0, 1920.0, "plain", true),
                new Waypoint(-1192.0, 1872.0, "plain", true),
                new Waypoint(-1288.0, 1760.0, "plain", true),
            };
        }
    }
}
