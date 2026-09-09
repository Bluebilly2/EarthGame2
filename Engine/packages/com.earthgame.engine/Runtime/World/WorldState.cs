using System;

namespace EarthGame.Engine
{
    /// <summary>
    /// Everything the server is authoritative for, in one object: identity, time, the region and its ground. The
    /// entity store and the other layers arrive in M1.2 and M1.3. There is exactly one instance per running world
    /// and it is owned by the server; a client holds a read-only mirror, never a WorldState.
    /// </summary>
    public sealed class WorldState
    {
        /// <summary>The master seed every derived stream hangs off (see <see cref="SimRandom.DeriveSeed"/>).</summary>
        public ulong Seed { get; }

        /// <summary>The piece of the Earth this world is set in.</summary>
        public Region Region { get; }

        /// <summary>Identity of the region, for the wire and the save.</summary>
        public string RegionId => Region.Id;

        /// <summary>The one clock (§4.3 of the plan).</summary>
        public WorldClock Clock { get; }

        /// <summary>
        /// The ground the server validates movement against, or null for a world without region data loaded (the
        /// handshake tests, a server started without its data folder); with no terrain the server cannot say
        /// where the ground is and does not pretend to.
        /// </summary>
        public Heightfield Terrain { get; }

        /// <summary>Fast ticks completed since the world was created. Monotonic; never reset by a load.</summary>
        public long Tick { get; private set; }

        /// <summary>
        /// Where the world's founder wakes, in local metres east and north, as the world-creation pipeline scored it
        /// (M1.2); null for a world without layers, which wakes at the region's stated point.
        /// </summary>
        public Double3? Wake { get; }

        public WorldState(ulong seed, Region region, WorldClock clock, Heightfield terrain = null, long tick = 0, Double3? wake = null)
        {
            Seed = seed;
            Region = region ?? throw new ArgumentNullException(nameof(region));
            Clock = clock ?? new WorldClock();
            if (terrain != null && Math.Abs(terrain.Raster.ExtentM - region.ExtentM) > 1e-6)
                throw new ArgumentException("the terrain raster covers " + terrain.Raster.ExtentM + " m but the region is " + region.ExtentM
                                            + " m; one of them is not this world's", nameof(terrain));
            Terrain = terrain;
            Tick = tick;
            Wake = wake;
        }

        /// <summary>
        /// Where a new player's feet are put: the region's wake point, on the ground (or at the water's surface if
        /// the ground there is below it). The server owns this; the client is told in its Welcome.
        /// </summary>
        public Double3 SpawnPoint()
        {
            double east, north;
            if (Wake.HasValue)
            {
                east = Wake.Value.X;
                north = Wake.Value.Z;
            }
            else
            {
                LocalFrame frame = LocalFrame.ForRegion(Region);
                frame.FromLatLon(Region.WakeLatitudeDeg, Region.WakeLongitudeDeg, out east, out north);
            }
            double up = Terrain != null ? Math.Max(Heightfield.SeaLevelM, Terrain.HeightAt(east, north)) : Heightfield.SeaLevelM;
            return new Double3(east, up, north);
        }

        /// <summary>
        /// One fast tick of the world. Called only by the server's fixed-step loop; the argument is the step length
        /// in real seconds at the current time scale. Order of operations is the specification and grows here as
        /// systems arrive.
        /// </summary>
        public void Step(double realSeconds)
        {
            Clock.Advance(realSeconds);
            Tick++;
        }
    }
}
