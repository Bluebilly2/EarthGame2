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

        /// <summary>
        /// The fast tick's systems, run in this order every step: the order is the specification. The items fall, then the
        /// animals round the founders are stood up, moved and taken away (M1.7a).
        /// </summary>
        public List<IFastSystem> Systems { get; } = new List<IFastSystem> { new ItemFall(), new AnimalStandUp() };

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

        /// <summary>
        /// Which stone lies on each cell (M1.2's layer, `StoneType.All` index + 1, 0 none), which a cobble taken up is made
        /// of (M1.5b); null on a world whose folder has none.
        /// </summary>
        public RegionRaster Stone { get; }

        /// <summary>The plant the understorey layer names on each cell (BF.3): what a tuft is, and what a dig turns up. A world from before the layer was read has none.</summary>
        public RegionRaster Understory { get; }

        /// <summary>The soil's depth on each cell, m (BF.3): how deep a dig can go. A world from before the layer was read has none.</summary>
        public RegionRaster SoilDepth { get; }

        /// <summary>What has been taken up of what lies loose (M1.5b), kept beside the layer, which never changes.</summary>
        public LooseTaken Taken { get; } = new LooseTaken();

        /// <summary>Every change to the generated world (BF.3): the takings, the tufts taken, the trunks' yield and cut, the ground cleared and dug.</summary>
        public WorldChanges Changes { get; }

        /// <summary>
        /// What each square of presence feeds, from the world folder's capacity layers (M1.7a); null on a world made before
        /// them, round whose founders no animal stands.
        /// </summary>
        public CapacitySquares Capacity { get; }

        /// <summary>
        /// Metres to the sea from each cell (M1.2's layer), for the salt wind's floor under a founder's exposure (FP.2); null
        /// on a world made without it, whose shore then blows as its ground's openness alone says.
        /// </summary>
        public RegionRaster ShoreDistance { get; }

        /// <summary>
        /// The world's climate (M1.8a), from the weather station's record this build holds for its region. Built when first
        /// asked for rather than with the world, so that a world of a region no record is held for (a test's fixture) is
        /// refused only by what asks it about the weather.
        /// </summary>
        public Climate Climate => _climate ?? (_climate = Climate.ForRegion(Region));

        /// <summary>
        /// The fronts this world's seed draws (M1.8a), built when first asked for. A client draws the same from the seed its
        /// Welcome carries, so nothing of the weather is sent.
        /// </summary>
        public Synoptic Synoptic => _synoptic ?? (_synoptic = new Synoptic(Seed));

        private Climate _climate;
        private Synoptic _synoptic;

        public WorldState(ulong seed, Region region, WorldClock clock, Heightfield terrain = null, long tick = 0, Double3? wake = null, WorldWater water = null, RegionRaster cover = null,
                          RegionRaster stand = null, RegionRaster loose = null, RegionRaster stone = null, CapacitySquares capacity = null,
                          RegionRaster shoreDistance = null, RegionRaster understory = null, RegionRaster soilDepth = null)
        {
            if (understory != null && !understory.IsIntegral) throw new ArgumentException("the understorey is " + understory.Dtype + ", not a code layer", nameof(understory));
            Understory = understory;
            SoilDepth = soilDepth;
            Seed = seed;
            Changes = new WorldChanges(Taken);
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
            if (stone != null && !stone.IsIntegral) throw new ArgumentException("the stone layer is " + stone.Dtype + ", not a code layer", nameof(stone));
            Stand = stand;
            Loose = loose;
            Stone = stone;
            if (capacity != null && Math.Abs(capacity.ExtentM - region.ExtentM) > 1e-6)
                throw new ArgumentException("the capacity squares cover " + capacity.ExtentM + " m but the region is " + region.ExtentM
                                            + " m; one of them is not this world's", nameof(capacity));
            Capacity = capacity;
            ShoreDistance = shoreDistance;
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
            double up = Terrain != null ? Math.Max(Heightfield.SeaLevelM, SurfaceAt(east, north)) : Heightfield.SeaLevelM;
            return new Double3(east, up, north);
        }

        /// <summary>
        /// The ground under a point as the server holds it (BF.4): the one ground both sides compute (<see cref="FineGround"/>),
        /// the raster to the centimetre the tiles carry with the relief below it and the hollows dug; the datum without terrain
        /// or beyond it. What a thing let go falls to, what an animal stands on and what a founder's feet are judged against.
        /// </summary>
        public double GroundAt(double east, double north)
        {
            if (Terrain == null || !Terrain.Contains(east, north)) return Heightfield.SeaLevelM;
            return FineGround.At(this, east, north);
        }

        /// <summary>
        /// What a foot or a thing meets at a point (BF.4 stage three): the one ground, or the top of a rock that stands there where
        /// that is higher. Where a founder is put, what a thing let go or falling comes to rest on, and what the movement check
        /// judges a founder's feet by.
        /// </summary>
        public double SurfaceAt(double east, double north) => StandingRocks.SurfaceAt(this, east, north);

        /// <summary>The cover code of the cell a point stands in, 0 (nothing said) without a cover layer (BF.4 stage two).</summary>
        public byte CoverCodeAt(double east, double north)
        {
            if (Cover == null) return 0;
            TileCodec.CellOf(Cover.ExtentM, Cover.CellM, east, north, out int row, out int col);
            if (row < 0 || col < 0 || row >= Cover.Height || col >= Cover.Width) return 0;
            return (byte)Cover.Code(row, col);
        }

        /// <summary>What a founder's feet stand on at a point, as the legs feel it (BF.4 stage two): the cover and the water over it.</summary>
        public GroundType UnderfootAt(double east, double north)
        {
            WaterAt(east, north, out double depthM);
            return Underfoot.Of(CoverCodeAt(east, north), depthM);
        }

        /// <summary>
        /// The raster's own ground under a point (BF.4): what a water's depth is measured over, on the server as the tiles carry
        /// it to the client, and what an animal is kept out of water by; the datum without terrain or beyond it.
        /// </summary>
        public double RasterGroundAt(double east, double north)
        {
            if (Terrain == null || !Terrain.Contains(east, north)) return Heightfield.SeaLevelM;
            return Terrain.HeightAt(east, north);
        }

        /// <summary>Water shallower than this stands nowhere a founder would call water, m: the one threshold for the server and the client's aim.</summary>
        public const double StandingWaterM = 0.02;

        private Heightfield _surface;

        /// <summary>
        /// The water standing at a point as the world's own layers have it (FP.1): its depth, the surface read between
        /// posts as the client reads the depth it is streamed, so the two agree where water stands; and its class, the
        /// nearest post's at the corners of the point's cell that is wet by its own depth, which is where the water
        /// between posts comes from. The first drink run's founder looked at a creek's edge and was told there was
        /// nothing there, the nearest post being dry by class; and until 2026-09-18 a post water by class but dry by
        /// depth lent its class to the water beside it, so at a creek's mouth the sea reaching into the stream's cell was
        /// served as fresh (the corpus of 2026-09-16 drank it four times). Where no corner is wet by its own depth (a
        /// surface read on another grid than the ground's) the nearest water-class post answers, as before. Dry where the
        /// surface stands no higher than the ground, beyond the frame, and on a world made without water.
        /// </summary>
        public WaterClass WaterAt(double east, double north, out double depthM)
        {
            depthM = 0.0;
            RegionRaster classes = Water?.Classes;
            if (classes == null || Water.Surface == null) return WaterClass.Dry;
            if (_surface == null) _surface = new Heightfield(Water.Surface);
            if (!_surface.Contains(east, north)) return WaterClass.Dry;
            double depth = _surface.HeightAt(east, north) - RasterGroundAt(east, north);
            if (depth <= StandingWaterM) return WaterClass.Dry;
            depthM = depth;
            double half = classes.ExtentM * 0.5;
            double fc = (east + half) / classes.CellM, fr = (half - north) / classes.CellM;
            WaterClass wet = WaterClass.Dry, byClass = WaterClass.Dry;
            double wetD = double.PositiveInfinity, byClassD = double.PositiveInfinity;
            int r0 = (int)Math.Floor(fr), c0 = (int)Math.Floor(fc);
            for (int r = r0; r <= r0 + 1; r++)
                for (int c = c0; c <= c0 + 1; c++)
                {
                    WaterClass wc = ClassAtPost(classes, r, c);
                    if (!IsWater(wc)) continue;
                    double d = (r - fr) * (r - fr) + (c - fc) * (c - fc);
                    if (d < byClassD)
                    {
                        byClassD = d;
                        byClass = wc;
                    }
                    if (d < wetD && WetByDepthAtPost(classes, r, c))
                    {
                        wetD = d;
                        wet = wc;
                    }
                }
            return wet != WaterClass.Dry ? wet : byClass;
        }

        /// <summary>Whether the water's surface stands over the ground by more than <see cref="StandingWaterM"/> at a post of the class raster.</summary>
        private bool WetByDepthAtPost(RegionRaster classes, int row, int col)
        {
            double half = classes.ExtentM * 0.5;
            double east = col * classes.CellM - half, north = half - row * classes.CellM;
            if (!_surface.Contains(east, north)) return false;
            return _surface.HeightAt(east, north) - RasterGroundAt(east, north) > StandingWaterM;
        }

        /// <summary>Open water a founder could stand in or drink from, as against damp ground, a trickle under the leaves or a swamp.</summary>
        public static bool IsWater(WaterClass wc) =>
            wc == WaterClass.Creek || wc == WaterClass.Stream || wc == WaterClass.Lake || wc == WaterClass.Sea;

        private static WaterClass ClassAtPost(RegionRaster classes, int row, int col) =>
            row < 0 || col < 0 || row >= classes.Height || col >= classes.Width ? WaterClass.Dry : (WaterClass)classes.Code(row, col);

        /// <summary>How many samples across the openness's radius the runtime reading takes each way; the layer takes every cell.</summary>
        private const int ExposureSamples = 12;

        private Heightfield _shore;

        /// <summary>
        /// How open the ground is at a point, 0 sheltered to 1 open (FP.2): the layer's own rule (<see cref="WorldLayers.SiteAt"/>),
        /// height above the mean of the surroundings within <see cref="WorldLayers.ExposureRadiusM"/> over
        /// <see cref="WorldLayers.ExposureFullAtM"/>, read from the terrain at a coarse grid of samples because the openness
        /// itself is not saved with a world, and floored by the salt wind within <see cref="WorldLayers.CoastWindReachM"/> of
        /// the sea from the shore-distance layer, which is: a beach lies below the mean of its dunes and would otherwise blow
        /// as a hollow (the first night run's founder, on the wake beach, stood in a third of the open's wind). Half open on a
        /// world without terrain.
        /// </summary>
        public double ExposureAt(double east, double north)
        {
            if (Terrain == null || !Terrain.Contains(east, north)) return 0.5;
            double coast = 0.0;
            if (ShoreDistance != null)
            {
                if (_shore == null) _shore = new Heightfield(ShoreDistance);
                if (_shore.Contains(east, north)) coast = SimMath.Clamp01(1.0 - _shore.HeightAt(east, north) / WorldLayers.CoastWindReachM);
            }
            double here = Terrain.HeightAt(east, north);
            double step = 2.0 * WorldLayers.ExposureRadiusM / ExposureSamples;
            double sum = 0.0;
            int n = 0;
            for (int i = 0; i <= ExposureSamples; i++)
                for (int j = 0; j <= ExposureSamples; j++)
                {
                    double de = -WorldLayers.ExposureRadiusM + i * step, dn = -WorldLayers.ExposureRadiusM + j * step;
                    if (de * de + dn * dn > WorldLayers.ExposureRadiusM * WorldLayers.ExposureRadiusM) continue;
                    double e = east + de, nn = north + dn;
                    if (!Terrain.Contains(e, nn)) continue;
                    sum += Terrain.HeightAt(e, nn);
                    n++;
                }
            if (n == 0) return Math.Max(0.5, coast);
            return Math.Max(SimMath.Clamp01((here - sum / n) / WorldLayers.ExposureFullAtM), coast);
        }

        /// <summary>
        /// The air, sky and sun at a founder's body (FP.2): the world's own weather (<see cref="Weather.At"/>) at the
        /// point's height, with the wind for the ground's openness there and the sun's elevation from the world's clock at
        /// the region's longitude, for a body standing in the open. One reading, so the body and any forecast agree.
        /// </summary>
        public Surroundings SurroundingsAt(double east, double up, double north)
        {
            SolarClock sun = SolarClock.ForRegion(Region, Clock);
            // A region this build holds no weather record for (a test's fixture) has no weather: the body lives in still air at
            // the temperature a naked body at rest neither cools nor sweats in, under no sky and no sun. Asking the climate
            // would refuse, as it should.
            if (!Climate.HasRecordFor(Region)) return new Surroundings(NoWeatherAirC, 0.0, 1.0, 0.7, -90.0, 0.0);
            Weather weather = Weather.At(Climate, Synoptic, sun, Math.Max(0.0, up), ExposureAt(east, north));
            return Surroundings.Of(weather, sun.SolarElevationDeg);
        }

        /// <summary>
        /// The air a body lives in on a world with no weather record, °C: what a naked body at rest makes (80 W) it loses to
        /// still air at about this, so nothing about it chills, cooks or sweats.
        /// </summary>
        public const double NoWeatherAirC = 30.0;

        /// <summary>
        /// Drops an item at a point: on the ground when no height is given or the height is below it, else in the
        /// air from that height, falling from the next step. The server's console, and M1.5's verbs, come here.
        /// </summary>
        public Entity SpawnItem(Definition definition, double east, double north, double? up = null, float yawDeg = 0f)
        {
            double ground = SurfaceAt(east, north);
            double at = up.HasValue ? Math.Max(up.Value, ground) : ground;
            Entity e = Entities.Spawn(definition, new Double3(east, at, north), yawDeg, Tick);
            ItemComponent item = default;
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
