namespace EarthGame.Engine
{
    /// <summary>
    /// Everything the server is authoritative for, in one object. M1.0 holds only identity and time; the
    /// region layers, entity store and definitions arrive in M1.2 and M1.3. There is exactly one instance per
    /// running world and it is owned by the server — a client holds a read-only mirror, never a WorldState.
    /// </summary>
    public sealed class WorldState
    {
        /// <summary>The master seed every derived stream hangs off (see <see cref="SimRandom.DeriveSeed"/>).</summary>
        public ulong Seed { get; }

        /// <summary>Identity of the region this world is set in (for example "bherwerre").</summary>
        public string RegionId { get; }

        /// <summary>The one clock (§4.3 of the plan).</summary>
        public WorldClock Clock { get; }

        /// <summary>Fast ticks completed since the world was created. Monotonic; never reset by a load.</summary>
        public long Tick { get; private set; }

        public WorldState(ulong seed, string regionId, WorldClock clock, long tick = 0)
        {
            Seed = seed;
            RegionId = regionId ?? string.Empty;
            Clock = clock ?? new WorldClock();
            Tick = tick;
        }

        /// <summary>
        /// One fast tick of the world. Called only by the server's fixed-step loop; the argument is the
        /// step length in real seconds at the current time scale. Order of operations is the specification
        /// and grows here as systems arrive.
        /// </summary>
        public void Step(double realSeconds)
        {
            Clock.Advance(realSeconds);
            Tick++;
        }
    }
}
