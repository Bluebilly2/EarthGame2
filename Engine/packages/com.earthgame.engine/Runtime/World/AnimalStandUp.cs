using System;
using System.Collections.Generic;

namespace EarthGame.Engine
{
    /// <summary>
    /// The animals presence puts round the founders, stood up as entities (M1.7a). Once a second every group of a kind a
    /// founder sees, whose place is within <see cref="StandUpRadiusM"/> of a founder, is stood up member by member under ids
    /// of the reserved range; those already standing are moved to where presence has them now, in the pose its hour gives
    /// them; and every one whose group no founder is within <see cref="TakeAwayRadiusM"/> of is taken away, to be told as
    /// having left. This system is the one owner of the reserved range: what it stands up is presence made visible, not the
    /// world's state, and is never saved or digested.
    ///
    /// <para>Where a member stands is worked out afresh each time from what it is: its group's place, a place of its own
    /// round the group, and a turn of its own from the way the group wanders. Nothing about it is remembered, so a mob walked
    /// away from and back to is the same mob, each animal in its place round it under the same id.</para>
    ///
    /// <para>Since M1.7c a standing group notices founders. Every step, a group any of whose members a founder has come
    /// within its kind's distance of takes flight (<see cref="AnimalFlightRules"/>): it runs straight away from that founder,
    /// its members keeping their places round it and moved every step, keeping to dry ground inside the region by turning
    /// along a shore or the edge and stopping where it cannot turn; having run its length it stands a while, then walks back
    /// to where presence puts it, and from there presence has it again. What a group is doing is held here while it stands,
    /// as its offset from presence's place, and forgotten when it is taken away; nothing of it is saved or digested.</para>
    /// </summary>
    public sealed class AnimalStandUp : IFastSystem
    {
        /// <summary>How near a founder a group's place must be for the group to be stood up, m, as built.</summary>
        public const double DefaultStandUpRadiusM = 500.0;

        /// <summary>
        /// How near a founder a standing group's place must stay for it to be kept, m, as built: farther than it took to stand
        /// it up, so a group on the edge is not stood up and taken away by turns.
        /// </summary>
        public const double DefaultTakeAwayRadiusM = 550.0;

        /// <summary>
        /// The two distances as this world runs them: the defaults, until a development server's developer moves them (M1.D,
        /// CANON ruling 30), which keeps the take-away no nearer than the stand-up.
        /// </summary>
        public double StandUpRadiusM = DefaultStandUpRadiusM;
        public double TakeAwayRadiusM = DefaultTakeAwayRadiusM;

        /// <summary>How often the animals are stood up, moved and taken away, seconds.</summary>
        public const double EverySeconds = 1.0;

        /// <summary>The activity from which an animal is shown grazing rather than resting: a mob is up at dawn and dusk and lying up at noon.</summary>
        public const double GrazingActivity = 0.25;

        /// <summary>
        /// About how far apart a group's members stand, m. A group stands within this times the root of its size of its place:
        /// a mob of eight within about fourteen metres, a pair within about seven. Not a citation: a looseness for M1.7b's eyes
        /// to judge.
        /// </summary>
        public const double MemberSpacingM = 5.0;

        /// <summary>How far either way a member's own turn takes it from the way its group wanders, degrees.</summary>
        public const double OwnTurnDeg = 40.0;

        /// <summary>How many steps a member's place is moved back by towards its group's own place, looking for dry ground.</summary>
        public const int DryingSteps = 8;

        /// <summary>The kinds stood up, which are the kinds a founder sees. The fairy-wren is heard, not seen, and waits for M1.8's soundscape.</summary>
        public static readonly IReadOnlyList<AnimalSpecies> Seen = new[] { AnimalSpecies.EasternGreyKangaroo, AnimalSpecies.PiedOystercatcher };

        /// <summary>What a square's index is moved by before it is packed into an id, so a square west or south of the centre packs.</summary>
        private const long SquareOffset = 1L << 23;

        /// <summary>The turns a running group tries, in order, when the way ahead is wet or off the region, degrees either way.</summary>
        private static readonly double[] Turns = { 45.0, -45.0, 90.0, -90.0, 135.0, -135.0 };

        private readonly HashSet<long> _groups = new HashSet<long>();
        private readonly HashSet<ulong> _refreshed = new HashSet<ulong>();
        /// <summary>Every group standing after the last refresh, by the id of its first member, with what presence said of it then.</summary>
        private readonly Dictionary<ulong, AnimalSighting> _standing = new Dictionary<ulong, AnimalSighting>();
        /// <summary>What each group that has taken flight is doing (M1.7c), by the id of its first member; none for a group presence has.</summary>
        private readonly Dictionary<ulong, Flight> _flights = new Dictionary<ulong, Flight>();
        private readonly Dictionary<AnimalSpecies, AnimalFlightRules> _rules = new Dictionary<AnimalSpecies, AnimalFlightRules>();
        private readonly List<ulong> _gone = new List<ulong>();
        private AnimalPresence _presence;
        private ulong _memberSalt;
        private WorldWater _surfaceOf;
        private Heightfield _surface;

        public AnimalStandUp()
        {
            for (int i = 0; i < Seen.Count; i++) _rules[Seen[i]] = AnimalFlightRules.DefaultsFor(Seen[i]);
        }

        public string Name => "animal stand-up";

        /// <summary>Raised when a group takes flight (M1.7c), for the host to record.</summary>
        public event Action<AnimalFlight> Fled;

        /// <summary>The rules of a kind's flight, as this world runs them: the defaults until a developer's setting moves them.</summary>
        public AnimalFlightRules RulesFor(AnimalSpecies species) => _rules.TryGetValue(species, out AnimalFlightRules rules) ? rules : null;

        /// <summary>Whether a group is away from where presence puts it: running, standing after a run, or walking back.</summary>
        public bool HasFlight(AnimalSpecies species, int cellX, int cellZ) => _flights.ContainsKey(IdOf(species, cellX, cellZ, 0));

        /// <summary>Whether a group is running now.</summary>
        public bool IsRunning(AnimalSpecies species, int cellX, int cellZ)
            => _flights.TryGetValue(IdOf(species, cellX, cellZ, 0), out Flight flight) && flight.Phase == Phase.Running;

        /// <summary>Refreshes the animals on the first step of each second, and every step moves the groups that have taken flight.</summary>
        public void Step(WorldState world, double dt)
        {
            if (RefreshesAt(world.Tick, dt)) Refresh(world);
            Flee(world, dt);
        }

        private enum Phase { Running, Standing, WalkingBack }

        /// <summary>A group's flight: how far it has gone from where presence puts it, which way it runs, and what is left of its run or its stand.</summary>
        private sealed class Flight
        {
            public Phase Phase;
            public double OffsetEast, OffsetNorth;
            public double DirEast, DirNorth;
            public double RunLeftM;
            public double StandLeftS;
        }

        /// <summary>
        /// Whether the step of a tick refreshes the animals, for steps of a length: on the ticks the step length divides a
        /// second into. A host timing its updates asks it to tell the ones that stood animals up.
        /// </summary>
        public static bool RefreshesAt(long tick, double stepSeconds)
        {
            long every = stepSeconds > 0.0 ? Math.Max(1L, (long)Math.Round(EverySeconds / stepSeconds)) : 1L;
            return tick % every == 0;
        }

        /// <summary>
        /// Stands up, moves and takes away the animals round the founders the world holds now (<see cref="WorldState.InterestPoints"/>),
        /// at its clock's hour: what a step does once a second, and what a test does when it chooses. A world without capacity
        /// squares, or without a founder, keeps none standing.
        /// </summary>
        public void Refresh(WorldState world)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            _refreshed.Clear();
            IReadOnlyList<Double3> founders = world.InterestPoints;
            CapacitySquares capacity = world.Capacity;
            _standing.Clear();
            if (capacity != null && founders.Count > 0)
            {
                if (_presence == null || _presence.Seed != world.Seed)
                {
                    _presence = new AnimalPresence(world.Seed);
                    _memberSalt = SimRandom.DeriveSeed(world.Seed, "animal members");
                }
                SolarClock sun = SolarClock.ForRegion(world.Region, world.Clock);
                double hour = sun.HourOfDay, daylight = sun.DaylightHours, day = sun.DayOfYear;
                for (int s = 0; s < Seen.Count; s++)
                {
                    AnimalSpecies species = Seen[s];
                    if (!capacity.Feeds(species)) continue;
                    Func<int, int, double> feeds = (cellX, cellZ) => capacity.PerKm2(species, cellX, cellZ);
                    _groups.Clear();
                    for (int f = 0; f < founders.Count; f++)
                        foreach (AnimalSighting group in _presence.Near(species, feeds, founders[f].X, founders[f].Z, TakeAwayRadiusM, hour, daylight, day))
                            if (_groups.Add(((long)group.CellX << 32) | (uint)group.CellZ))
                                Stand(world, group, founders);
                }
            }
            IReadOnlyList<Entity> standing = world.Entities.Transient;
            for (int i = 0; i < standing.Count; i++)
                if (!standing[i].Killed && !_refreshed.Contains(standing[i].Id.Value)) world.Entities.Kill(standing[i]);
            // A group taken away forgets its flight: stood up again, it is where presence puts it.
            _gone.Clear();
            foreach (ulong key in _flights.Keys)
                if (!_standing.ContainsKey(key)) _gone.Add(key);
            for (int i = 0; i < _gone.Count; i++) _flights.Remove(_gone[i]);
        }

        /// <summary>
        /// The id of one member of one group, of the reserved range: the top bit; the kind's place in <see cref="AnimalSpecies.All"/>,
        /// counting from one, in the next seven bits; the square's east and north indices in twenty-four bits each; and the
        /// member in the low eight. Made from what the animal is, so the same animal stood up again has the same id.
        /// </summary>
        public static ulong IdOf(AnimalSpecies species, int cellX, int cellZ, int member)
        {
            int kind = KindOf(species);
            if (member < 0 || member > byte.MaxValue) throw new ArgumentOutOfRangeException(nameof(member), "member " + member + " of a group is beyond what an id packs");
            long x = cellX + SquareOffset, z = cellZ + SquareOffset;
            if (x < 0 || z < 0 || x >= 1L << 24 || z >= 1L << 24)
                throw new ArgumentOutOfRangeException(nameof(cellX), "square (" + cellX + ", " + cellZ + ") is beyond what an id packs");
            return EntityId.TransientBit | ((ulong)kind << 56) | ((ulong)x << 32) | ((ulong)z << 8) | (uint)member;
        }

        /// <summary>
        /// A group presence found within the take-away radius of a founder: stood up when a founder is within the stand-up
        /// radius, else kept only if it already stands, and never where its own place is wet or off the region's ground.
        /// </summary>
        private void Stand(WorldState world, AnimalSighting group, IReadOnlyList<Double3> founders)
        {
            ulong first = IdOf(group.Species, group.CellX, group.CellZ, 0);
            bool standing = world.Entities.TryGet(first, out _);
            if (!standing && NearestFounderM(group, founders) > StandUpRadiusM) return;
            if (!Dry(world, group.BaseEastM, group.BaseNorthM)) return;
            _standing[first] = group;
            // A group away from where presence puts it (M1.7c) stands as far from it as its flight has taken it, and faces
            // the way it runs.
            _flights.TryGetValue(first, out Flight flight);
            double offEast = flight != null ? flight.OffsetEast : 0.0, offNorth = flight != null ? flight.OffsetNorth : 0.0;
            bool running = flight != null && flight.Phase == Phase.Running;
            Definition definition = DefinitionCatalogue.AnimalOf(group.Species);
            AnimalComponent animal;
            animal.Pose = running ? AnimalPose.Fleeing : group.Activity01 >= GrazingActivity ? AnimalPose.Grazing : AnimalPose.Resting;
            double spread = MemberSpacingM * Math.Sqrt(group.GroupSize);
            double baseEast = group.BaseEastM + offEast, baseNorth = group.BaseNorthM + offNorth;
            for (int m = 0; m < group.GroupSize; m++)
            {
                ulong id = IdOf(group.Species, group.CellX, group.CellZ, m);
                // Its own place round the group and its own turn, drawn from what it is.
                ulong h = StandLayout.Mix(id ^ _memberSalt);
                double reach = spread * Math.Sqrt(Unit(h));
                h = StandLayout.Mix(h);
                double bearing = 2.0 * Math.PI * Unit(h);
                h = StandLayout.Mix(h);
                float yaw = running ? (float)Bearing(flight.DirEast, flight.DirNorth) : (float)Mod(group.HeadingDeg + (2.0 * Unit(h) - 1.0) * OwnTurnDeg, 360.0);
                double wantEast = group.EastM + offEast + reach * Math.Sin(bearing), wantNorth = group.NorthM + offNorth + reach * Math.Cos(bearing);
                // Moved back towards the group's own place, an eighth of the way at a time, until the ground is dry; the own
                // place itself is dry, or the group would not be standing (a flight keeps its own place dry as it goes).
                double east = baseEast, north = baseNorth;
                for (int k = DryingSteps; k > 0; k--)
                {
                    double t = (double)k / DryingSteps;
                    double e = baseEast + t * (wantEast - baseEast), n = baseNorth + t * (wantNorth - baseNorth);
                    if (!Dry(world, e, n)) continue;
                    east = e;
                    north = n;
                    break;
                }
                Double3 at = new Double3(east, world.GroundAt(east, north), north);
                if (world.Entities.TryGet(id, out Entity member))
                {
                    // Moved and turned every second even to where it already stands, so a move its viewers lost is put
                    // right a second later instead of lasting while the animal stands still (a member held at its group's
                    // own place by the water does); its pose is stamped only when it changes, and goes reliably.
                    member.Move(at, world.Tick);
                    member.Turn(yaw, world.Tick);
                    if (member.Animal.Pose != animal.Pose) member.SetAnimal(animal, world.Tick);
                }
                else world.Entities.StandUp(id, definition, at, yaw, animal, world.Tick);
                _refreshed.Add(id);
            }
        }

        /// <summary>
        /// Whether an animal can stand at a point: on the region's ground; not in a creek, a stream, a lake or the sea by the
        /// water's class at the nearest cell; and not where the world's water surface stands above its ground. A world without
        /// water layers has the sea wherever its ground lies below the datum, as the collision's own rule has it.
        /// </summary>
        private bool Dry(WorldState world, double east, double north)
        {
            Heightfield terrain = world.Terrain;
            if (terrain != null && !terrain.Contains(east, north)) return false;
            WorldWater water = world.Water;
            if (water == null) return world.GroundAt(east, north) >= Heightfield.SeaLevelM;
            RegionRaster classes = water.Classes;
            TileCodec.CellOf(classes.ExtentM, classes.CellM, east, north, out int row, out int col);
            row = Math.Min(classes.Height - 1, Math.Max(0, row));
            col = Math.Min(classes.Width - 1, Math.Max(0, col));
            WaterClass wet = (WaterClass)classes.Code(row, col);
            if (wet == WaterClass.Creek || wet == WaterClass.Stream || wet == WaterClass.Lake || wet == WaterClass.Sea) return false;
            if (_surfaceOf != water)
            {
                _surface = new Heightfield(water.Surface);
                _surfaceOf = water;
            }
            return !(_surface.HeightAt(east, north) > world.GroundAt(east, north));
        }

        /// <summary>
        /// Every step (M1.7c): a standing group not running that a founder has come within its kind's distance of any member
        /// of takes flight, away from that founder; a running group runs on, keeping to dry ground; one that has run its
        /// length stands a while; one that has stood walks back to where presence puts it, and is presence's again.
        /// </summary>
        private void Flee(WorldState world, double dt)
        {
            if (dt <= 0.0 || _standing.Count == 0) return;
            IReadOnlyList<Double3> founders = world.InterestPoints;
            foreach (KeyValuePair<ulong, AnimalSighting> pair in _standing)
            {
                ulong first = pair.Key;
                AnimalSighting group = pair.Value;
                if (!_rules.TryGetValue(group.Species, out AnimalFlightRules rules)) continue;
                _flights.TryGetValue(first, out Flight flight);
                if (flight == null || flight.Phase != Phase.Running)
                {
                    if (Startles(world, group, founders, rules, out Double3 founder, out double distance))
                    {
                        double centreEast = group.EastM + (flight != null ? flight.OffsetEast : 0.0);
                        double centreNorth = group.NorthM + (flight != null ? flight.OffsetNorth : 0.0);
                        double dirEast = centreEast - founder.X, dirNorth = centreNorth - founder.Z;
                        double length = Math.Sqrt(dirEast * dirEast + dirNorth * dirNorth);
                        if (length < 1e-9)
                        {
                            // A founder on the group's own place: it runs the way it was wandering.
                            dirEast = Math.Sin(group.HeadingDeg * GeoMath.DegToRad);
                            dirNorth = Math.Cos(group.HeadingDeg * GeoMath.DegToRad);
                        }
                        else
                        {
                            dirEast /= length;
                            dirNorth /= length;
                        }
                        if (flight == null) _flights[first] = flight = new Flight();
                        flight.Phase = Phase.Running;
                        flight.DirEast = dirEast;
                        flight.DirNorth = dirNorth;
                        flight.RunLeftM = rules.RunM;
                        Pose(world, group, AnimalPose.Fleeing);
                        Fled?.Invoke(new AnimalFlight(group.Species, group.CellX, group.CellZ, centreEast, centreNorth, founder.X, founder.Z, distance, Bearing(dirEast, dirNorth)));
                    }
                    else if (flight == null) continue;
                }
                switch (flight.Phase)
                {
                    case Phase.Running:
                        Run(world, group, flight, rules, dt);
                        break;
                    case Phase.Standing:
                        flight.StandLeftS -= dt;
                        if (flight.StandLeftS <= 0.0) flight.Phase = Phase.WalkingBack;
                        break;
                    case Phase.WalkingBack:
                        WalkBack(world, group, flight, rules, dt);
                        break;
                }
            }
            _gone.Clear();
            foreach (KeyValuePair<ulong, Flight> pair in _flights)
                if (pair.Value.Phase == Phase.WalkingBack && pair.Value.OffsetEast == 0.0 && pair.Value.OffsetNorth == 0.0) _gone.Add(pair.Key);
            for (int i = 0; i < _gone.Count; i++) _flights.Remove(_gone[i]);
        }

        /// <summary>Whether a founder is within the kind's distance of any standing member, and which founder is nearest to one.</summary>
        private static bool Startles(WorldState world, AnimalSighting group, IReadOnlyList<Double3> founders, AnimalFlightRules rules,
                                     out Double3 founder, out double distanceM)
        {
            founder = default;
            distanceM = double.PositiveInfinity;
            for (int m = 0; m < group.GroupSize; m++)
            {
                if (!world.Entities.TryGet(IdOf(group.Species, group.CellX, group.CellZ, m), out Entity member) || member.Killed) continue;
                for (int f = 0; f < founders.Count; f++)
                {
                    double dx = member.Position.X - founders[f].X, dz = member.Position.Z - founders[f].Z;
                    double d = Math.Sqrt(dx * dx + dz * dz);
                    if (d < distanceM)
                    {
                        distanceM = d;
                        founder = founders[f];
                    }
                }
            }
            return distanceM <= rules.FleeWithinM;
        }

        /// <summary>
        /// One step of a run: the group's place moves along its way at its speed, turning along a shore or the region's edge
        /// when the way ahead is wet or off the ground, and stopping where no turn finds dry ground; its members move with it.
        /// </summary>
        private void Run(WorldState world, AnimalSighting group, Flight flight, AnimalFlightRules rules, double dt)
        {
            double step = Math.Min(flight.RunLeftM, rules.RunMs * dt);
            double centreEast = group.EastM + flight.OffsetEast, centreNorth = group.NorthM + flight.OffsetNorth;
            bool moved = false;
            if (step > 0.0)
            {
                if (Clear(world, group, centreEast, centreNorth, flight.DirEast, flight.DirNorth, step)) moved = true;
                else
                    for (int t = 0; t < Turns.Length && !moved; t++)
                    {
                        double turn = Turns[t] * GeoMath.DegToRad;
                        double cos = Math.Cos(turn), sin = Math.Sin(turn);
                        // Turned clockwise by a positive angle, as a bearing turns.
                        double dirEast = flight.DirEast * cos + flight.DirNorth * sin, dirNorth = flight.DirNorth * cos - flight.DirEast * sin;
                        if (!Clear(world, group, centreEast, centreNorth, dirEast, dirNorth, step)) continue;
                        flight.DirEast = dirEast;
                        flight.DirNorth = dirNorth;
                        moved = true;
                    }
            }
            if (moved)
            {
                double dEast = flight.DirEast * step, dNorth = flight.DirNorth * step;
                flight.OffsetEast += dEast;
                flight.OffsetNorth += dNorth;
                flight.RunLeftM -= step;
                Shift(world, group, dEast, dNorth, (float)Bearing(flight.DirEast, flight.DirNorth));
            }
            if (!moved || flight.RunLeftM <= 1e-9)
            {
                flight.Phase = Phase.Standing;
                flight.StandLeftS = AnimalFlightRules.SettleSeconds;
                Pose(world, group, group.Activity01 >= GrazingActivity ? AnimalPose.Grazing : AnimalPose.Resting);
            }
        }

        /// <summary>
        /// Whether a step a way is dry for the group's place and for every standing member: a mob spread fourteen metres round
        /// a dry place put a member into the lake when the place alone was asked (the shore test's first run).
        /// </summary>
        private bool Clear(WorldState world, AnimalSighting group, double centreEast, double centreNorth, double dirEast, double dirNorth, double step)
        {
            if (!Dry(world, centreEast + dirEast * step, centreNorth + dirNorth * step)) return false;
            for (int m = 0; m < group.GroupSize; m++)
                if (world.Entities.TryGet(IdOf(group.Species, group.CellX, group.CellZ, m), out Entity member) && !member.Killed
                    && !Dry(world, member.Position.X + dirEast * step, member.Position.Z + dirNorth * step)) return false;
            return true;
        }

        /// <summary>One step of the walk back: the group's place moves towards presence's at the walking pace, and arrives.</summary>
        private void WalkBack(WorldState world, AnimalSighting group, Flight flight, AnimalFlightRules rules, double dt)
        {
            double away = Math.Sqrt(flight.OffsetEast * flight.OffsetEast + flight.OffsetNorth * flight.OffsetNorth);
            if (away <= 1e-9)
            {
                flight.OffsetEast = flight.OffsetNorth = 0.0;
                return;
            }
            double step = Math.Min(away, rules.WalkMs * dt);
            double dEast = -flight.OffsetEast / away * step, dNorth = -flight.OffsetNorth / away * step;
            flight.OffsetEast += dEast;
            flight.OffsetNorth += dNorth;
            if (Math.Sqrt(flight.OffsetEast * flight.OffsetEast + flight.OffsetNorth * flight.OffsetNorth) <= 1e-6) flight.OffsetEast = flight.OffsetNorth = 0.0;
            Shift(world, group, dEast, dNorth, (float)Bearing(dEast, dNorth));
        }

        /// <summary>Moves every standing member of a group by the same step and turns it the way the group goes.</summary>
        private void Shift(WorldState world, AnimalSighting group, double dEast, double dNorth, float yaw)
        {
            for (int m = 0; m < group.GroupSize; m++)
            {
                if (!world.Entities.TryGet(IdOf(group.Species, group.CellX, group.CellZ, m), out Entity member) || member.Killed) continue;
                double east = member.Position.X + dEast, north = member.Position.Z + dNorth;
                member.Move(new Double3(east, world.GroundAt(east, north), north), world.Tick);
                member.Turn(yaw, world.Tick);
            }
        }

        /// <summary>Gives every standing member of a group a pose, stamped only where it changes.</summary>
        private static void Pose(WorldState world, AnimalSighting group, byte pose)
        {
            AnimalComponent animal;
            animal.Pose = pose;
            for (int m = 0; m < group.GroupSize; m++)
                if (world.Entities.TryGet(IdOf(group.Species, group.CellX, group.CellZ, m), out Entity member) && !member.Killed && member.Animal.Pose != pose)
                    member.SetAnimal(animal, world.Tick);
        }

        /// <summary>A direction's bearing, degrees clockwise from north.</summary>
        private static double Bearing(double east, double north) => Mod(Math.Atan2(east, north) * 180.0 / Math.PI, 360.0);

        private static double NearestFounderM(AnimalSighting group, IReadOnlyList<Double3> founders)
        {
            double nearest = double.PositiveInfinity;
            for (int i = 0; i < founders.Count; i++)
            {
                double dx = group.EastM - founders[i].X, dz = group.NorthM - founders[i].Z;
                nearest = Math.Min(nearest, dx * dx + dz * dz);
            }
            return Math.Sqrt(nearest);
        }

        private static int KindOf(AnimalSpecies species)
        {
            IReadOnlyList<AnimalSpecies> all = AnimalSpecies.All;
            for (int i = 0; i < all.Count; i++)
                if (all[i] == species) return i + 1;
            throw new ArgumentException("'" + species + "' is not a kind of animal this build knows", nameof(species));
        }

        /// <summary>A draw as a double in [0, 1), from its top 53 bits.</summary>
        private static double Unit(ulong h) => (h >> 11) * (1.0 / 9007199254740992.0);

        private static double Mod(double v, double period)
        {
            double r = v % period;
            return r < 0.0 ? r + period : r;
        }
    }
}
