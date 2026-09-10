namespace EarthGame.ClientCore
{
    /// <summary>
    /// The scripted routes the scenarios walk, in local metres of the Bherwerre region. Every leg is legal input
    /// over walkable ground, so every correction on it is a false positive (N2); the named divergence segments are
    /// the bank and the shore.
    ///
    /// <para>The wake loop is laid by <c>Tools/corpus/lay_loop.py</c> round the wake the world-creation scorer
    /// chooses on the gate world (seed 1347). The tool checks every leg every 2 m against the criteria it states and
    /// prints its survey, and the corpus runs every scenario on a copy of that world, so founders wake beside the
    /// loop. A change to the layer rules can move the wake off it (M1.2b moved it 900 m north, 2026-09-10), and
    /// join_check then says the route never reached the bank or the shore: lay it again, and rebuild the player.</para>
    ///
    /// <para>The first loop (2026-09-08) was laid around the region's stated wake point, which the corpus's server
    /// used as its spawn while it ran on the bare bake. M1.2 moved real worlds' wakes 4 km from that point and CANON
    /// ruling 20 withdrew it.</para>
    ///
    /// <para>The first re-laid bank ran along the scarp's face, 37% of it over 28° and up to 37.5°, and the server
    /// corrected founders on it about thirty times a minute over the wire and seven in SOLO, finding them "below the
    /// ground" by one to six metres (walk of 2026-09-10). That is the named defect class on steep ground, recorded in
    /// DEBTS.md; it is not what N2's bank measures, so the bank here is held to the slope N2 was specified on.</para>
    /// </summary>
    public static class Routes
    {
        /// <summary>About 1.09 km: along the water's edge, the flat way round to the bank, down it, and the flat way home.</summary>
        public static Waypoint[] WakeLoop()
        {
            return new[]
            {
                new Waypoint(-1360.0, 2804.0, "plain", true),
                new Waypoint(-1328.0, 2788.0, "creek", false),
                new Waypoint(-1264.0, 2740.0, "bank", false),
                new Waypoint(-1296.0, 2788.0, "plain", true),
                new Waypoint(-1328.0, 2836.0, "plain", true),
                new Waypoint(-1376.0, 2916.0, "plain", true),
                new Waypoint(-1392.0, 2932.0, "plain", true),
                new Waypoint(-1424.0, 2948.0, "plain", true),
                new Waypoint(-1488.0, 3012.0, "plain", true),
                new Waypoint(-1568.0, 3028.0, "return", true),
                new Waypoint(-1600.0, 3140.0, "shore", false),
                new Waypoint(-1472.0, 2996.0, "plain", true),
            };
        }
    }
}
