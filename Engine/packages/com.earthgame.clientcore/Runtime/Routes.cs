namespace EarthGame.ClientCore
{
    /// <summary>
    /// The scripted routes the scenarios walk, in local metres of the Bherwerre region. Surveyed on the baked
    /// raster on 2026-09-08 from the wake (−2409.6, −2112.7; 13 m): the plain south of the wake drops from 16 m
    /// to 5 m near north −2400; a bank of 22–28° runs east–west at north −2565 between east −2010 and −1920;
    /// the water's edge (0.3 m) lies 680 m south of the wake at north −2790; the 20 m crest sits west-south-west
    /// at (−2560, −2160). Every leg is legal input over walkable ground, so every correction on it is a false
    /// positive (N2); the named divergence segments are the bank and the shore.
    /// </summary>
    public static class Routes
    {
        /// <summary>About 2.1 km: plain, bank, shore, return over the crest, back to the wake.</summary>
        public static Waypoint[] WakeLoop()
        {
            return new[]
            {
                new Waypoint(-2410.0, -2400.0, "plain", true),
                new Waypoint(-1990.0, -2540.0, "plain", true),
                new Waypoint(-1990.0, -2600.0, "bank", false),
                new Waypoint(-2233.0, -2771.0, "shore", false),
                new Waypoint(-2409.0, -2790.0, "shore", false),
                new Waypoint(-2560.0, -2160.0, "return", true),
                new Waypoint(-2409.6, -2112.7, "crest", false),
            };
        }
    }
}
