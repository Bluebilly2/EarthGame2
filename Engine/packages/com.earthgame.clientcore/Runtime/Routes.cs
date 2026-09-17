namespace EarthGame.ClientCore
{
    /// <summary>
    /// The scripted routes the scenarios walk, in local metres of the Bherwerre region. Every leg is legal input
    /// over walkable ground or shallow water, so every correction on it is a false positive (N2); the named divergence
    /// segments are the bank and the shore, and since M1.5d the cliff's top edge, the shore platform and the wade into
    /// the sea, the three places CANON ruling 13 names.
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
    ///
    /// <para>Since 2026-09-16 the loop also passes within a mob's and a pair's flight distance of where presence puts an
    /// animal group (the "mob" and "pair" waypoints: <c>lay_loop.py --pass</c>, the places read from a soak's own fauna
    /// records), so a soak startles animals (DEBTS, "The corpus's loop startles no animal"); the tool budgets the walk to
    /// the last of N2's named legs rather than the whole lap, since the passes lie 600 m from the wake. The same laying
    /// walks the shore, the bank, the platform and the wade the other way round and a different cliff leg beside the bank,
    /// so N2 is owed a walk on it. No water leg could be laid: no creek or stream on the gate world carries a surface, and
    /// the nearest lake with water in it is 1,977 m from the wake, so a walker told thirsty finds nothing to drink on this
    /// loop (<see cref="Drinking"/>).</para>
    /// </summary>
    public static class Routes
    {
        /// <summary>About 1.86 km: the shore, the bank, a cliff's top edge, a shore platform and a wade into the sea, a pass by a kangaroo mob and by an oystercatcher pair, and the flat ways between them.</summary>
        public static Waypoint[] WakeLoop()
        {
            return new[]
            {
                new Waypoint(-1404.0, 2840.0, "plain", true),
                new Waypoint(-1324.0, 2824.0, "bank", false),
                new Waypoint(-1340.0, 2840.0, "plain", true),
                new Waypoint(-1364.0, 2848.0, "cliff", false),
                new Waypoint(-1372.0, 2920.0, "plain", true),
                new Waypoint(-1452.0, 2968.0, "plain", true),
                new Waypoint(-1468.0, 3000.0, "plain", true),
                new Waypoint(-1516.0, 3016.0, "plain", true),
                new Waypoint(-1564.0, 3016.0, "platform", false),
                new Waypoint(-1592.0, 3024.0, "wade", false),
                new Waypoint(-1564.0, 3016.0, "wade", false),
                new Waypoint(-1596.0, 3128.0, "shore", false),
                new Waypoint(-1500.0, 3240.0, "plain", true),
                new Waypoint(-1484.0, 3192.0, "plain", true),
                new Waypoint(-1468.0, 3192.0, "plain", true),
                new Waypoint(-1372.0, 3240.0, "plain", true),
                new Waypoint(-1388.0, 3288.0, "plain", true),
                new Waypoint(-1436.0, 3416.0, "plain", true),
                new Waypoint(-1436.0, 3416.0, "mob", false),
                new Waypoint(-1436.0, 3368.0, "plain", true),
                new Waypoint(-1452.0, 3352.0, "plain", true),
                new Waypoint(-1468.0, 3352.0, "plain", true),
                new Waypoint(-1505.0, 3379.0, "plain", true),
                new Waypoint(-1505.0, 3379.0, "pair", false),
                new Waypoint(-1388.0, 3288.0, "plain", true),
                new Waypoint(-1372.0, 3240.0, "plain", true),
                new Waypoint(-1372.0, 3112.0, "plain", true),
                new Waypoint(-1388.0, 3032.0, "plain", true),
                new Waypoint(-1428.0, 2976.0, "plain", true),
                new Waypoint(-1428.0, 2968.0, "plain", true),
                new Waypoint(-1420.0, 2920.0, "plain", true),
            };
        }
    }
}
