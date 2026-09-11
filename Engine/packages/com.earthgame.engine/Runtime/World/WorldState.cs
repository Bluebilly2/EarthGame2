using System;
using System.Collections.Generic;

namespace EarthGame.Engine
{
    /// <summary>
    /// The water a world can stream beside its ground (M1.4b): the surface it stands at, and its class per cell,
    /// both as the world folder's own layers. Null on a world whose folder has neither — any world saved before
    /// M1.2 — and such a server streams the ground alone and says so.
    /// </summary>
    public sealed class WorldWater
    {
        public WorldWater(RegionRaster surface, RegionRaster classes)
        {
            Surface = surface ?? throw new ArgumentNullException(nameof(surface));
            Classes = classes ?? throw new ArgumentNullException(nameof(classes));
            if (classes.Width != surface.Width || classes.Height != surface.Height)
                throw new ArgumentException("the water's class is " + classes.Width + "x" + classes.Height + " and its surface " + surface.Width + "x" + surface.Height, nameof(classes));
            if (!classes.IsIntegral) throw new ArgumentException("the water's class is " + classes.Dtype + ", not a code layer", nameof(classes));
        }

        /// <summary>Where the water's surface stands, metres; the ground itself where none stands.</summary>
        public RegionRaster Surface { get; }
        /// <summary>Each cell's <see cref="WaterClass"/> as a code.</summary>
        public RegionRaster Classes { get; }
    }

    /// <summary>
    /// Everything the server is authoritative for, in one object: identity, time, the region and its ground, the
    /// entities and the systems that move them. There is exactly one instance per running world and it is owned
    /// by the server; a client holds a read-only mirror, never a WorldState.
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

        /// <summary>The entities (M1.3, ARCHITECTURE §5).</summary>
        public EntityStore Entities { get; } = new EntityStore();

        /// <summary>The fast tick's systems, run in this order every step: the order is the specification.</summary>
        public List<IFastSystem> Systems { get; } = new List<IFastSystem> { new ItemFall() };

        /// <summary>The slow layers and the scheduler that advances them by cell and distance.</summary>
        public SlowScheduler Scheduler { get; } = new SlowScheduler();

        /// <summary>Where the players are, for the scheduler's distances; the server fills it before each update.</summary>
        public List<Double3> InterestPoints { get; } = new List<Double3>();

        /// <summary>The water this world can stream, or null for a world whose folder holds none.</summary>
        public WorldWater Water { get; }

        /// <summary>What covers each cell of this world (M1.4d), as the folder's own layer; null on a world made before it.</summary>
        public RegionRaster Cover { get; }

        /// <summary>Where this world's trees stand (M1.6a), as the folder's own layer of <see cref="StandCodes"/>; null on a world made before it.</summary>
        public RegionRaster Stand { get; }

        /// <summary>What lies loose on this world's ground (M1.6a), as the folder's own layer of <see cref="LooseCodes"/>; null on a world made before it.</summary>
        public RegionRaster Loose { get; }

        public WorldState(ulong seed, Region region, WorldClock clock, Heightfield terrain = null, long tick = 0, Double3? wake = null, WorldWater water = null, RegionRaster cover = null,
                          RegionRaster stand = null, RegionRaster loose = null)
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
            Water = water;
            if (cover != null && !cover.IsIntegral) throw new ArgumentException("the ground cover is " + cover.Dtype + ", not a code layer", nameof(cover));
            Cover = cover;
            if (stand != null && !stand.IsIntegral) throw new ArgumentException("the stand is " + stand.Dtype + ", not a code layer", nameof(stand));
            if (loose != null && !loose.IsIntegral) throw new ArgumentException("the loose layer is " + loose.Dtype + ", not a code layer", nameof(loose));
            Stand = stand;
            Loose = loose;
        }

        /// <summary>
        /// Where a new player's feet are put: the world's wake, on the ground (or at the water's surface if the
        /// ground there is below it). A world made without one — a test's bare world, a save from before M1.2 —
        /// puts them at the region's centre, the origin of its frame: the region names no point of its own since
        /// CANON ruling 20 withdrew the one it had. The server owns this; the client is told in its Welcome.
        /// </summary>
        public Double3 SpawnPoint()
        {
            double east = Wake.HasValue ? Wake.Value.X : 0.0;
            double north = Wake.HasValue ? Wake.Value.Z : 0.0;
            double up = Terrain != null ? Math.Max(Heightfield.SeaLevelM, Terrain.HeightAt(east, north)) : Heightfield.SeaLevelM;
            return new Double3(east, up, north);
        }

        /// <summary>The ground under a point as the server holds it: the terrain's height, or the datum without terrain or beyond it.</summary>
        public double GroundAt(double east, double north)
        {
            if (Terrain == null || !Terrain.Contains(east, north)) return Heightfield.SeaLevelM;
            return Terrain.HeightAt(east, north);
        }

        /// <summary>
        /// Drops an item at a point: on the ground when no height is given or the height is below it, else in the
        /// air from that height, falling from the next step. The server's console, and M1.5's verbs, come here.
        /// </summary>
        public Entity SpawnItem(Definition definition, double east, double north, double? up = null, float yawDeg = 0f)
        {
            double ground = GroundAt(east, north);
            double at = up.HasValue ? Math.Max(up.Value, ground) : ground;
            Entity e = Entities.Spawn(definition, new Double3(east, at, north), yawDeg, Tick);
            ItemComponent item;
            item.Resting = at <= ground + 1e-9;
            item.FallSpeed = 0f;
            e.SetItem(item, Tick);
            return e;
        }

        /// <summary>
        /// One fast tick of the world. Called only by the server's fixed-step loop; the argument is the step length
        /// in real seconds at the current time scale. Order of operations is the specification: the clock, the
        /// fast systems in order, the slow layers due this tick, the kills, then the tick count.
        /// </summary>
        public void Step(double realSeconds)
        {
            Clock.Advance(realSeconds);
            for (int i = 0; i < Systems.Count; i++) Systems[i].Step(this, realSeconds);
            Scheduler.Tick(this, Tick);
            Entities.EndTick();
            Tick++;
        }
    }
}
