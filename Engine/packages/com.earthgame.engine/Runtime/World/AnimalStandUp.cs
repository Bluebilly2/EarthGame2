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

        private readonly HashSet<long> _groups = new HashSet<long>();
        private readonly HashSet<ulong> _refreshed = new HashSet<ulong>();
        private AnimalPresence _presence;
        private ulong _memberSalt;
        private WorldWater _surfaceOf;
        private Heightfield _surface;

        public string Name => "animal stand-up";

        /// <summary>Refreshes the animals on the first step of each second.</summary>
        public void Step(WorldState world, double dt)
        {
            if (RefreshesAt(world.Tick, dt)) Refresh(world);
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
            bool standing = world.Entities.TryGet(IdOf(group.Species, group.CellX, group.CellZ, 0), out _);
            if (!standing && NearestFounderM(group, founders) > StandUpRadiusM) return;
            if (!Dry(world, group.BaseEastM, group.BaseNorthM)) return;
            Definition definition = DefinitionCatalogue.AnimalOf(group.Species);
            AnimalComponent animal;
            animal.Pose = group.Activity01 >= GrazingActivity ? AnimalPose.Grazing : AnimalPose.Resting;
            double spread = MemberSpacingM * Math.Sqrt(group.GroupSize);
            for (int m = 0; m < group.GroupSize; m++)
            {
                ulong id = IdOf(group.Species, group.CellX, group.CellZ, m);
                // Its own place round the group and its own turn, drawn from what it is.
                ulong h = StandLayout.Mix(id ^ _memberSalt);
                double reach = spread * Math.Sqrt(Unit(h));
                h = StandLayout.Mix(h);
                double bearing = 2.0 * Math.PI * Unit(h);
                h = StandLayout.Mix(h);
                float yaw = (float)Mod(group.HeadingDeg + (2.0 * Unit(h) - 1.0) * OwnTurnDeg, 360.0);
                double wantEast = group.EastM + reach * Math.Sin(bearing), wantNorth = group.NorthM + reach * Math.Cos(bearing);
                // Moved back towards the group's own place, an eighth of the way at a time, until the ground is dry; the own
                // place itself is dry, or the group would not be standing.
                double east = group.BaseEastM, north = group.BaseNorthM;
                for (int k = DryingSteps; k > 0; k--)
                {
                    double t = (double)k / DryingSteps;
                    double e = group.BaseEastM + t * (wantEast - group.BaseEastM), n = group.BaseNorthM + t * (wantNorth - group.BaseNorthM);
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
