using System;
using System.Collections.Generic;
using EarthGame.Engine;
using NUnit.Framework;

namespace EarthGame.Tests.Engine
{
    /// <summary>
    /// The animals stood up round the founders (M1.7a promises 1 to 3): member by member where presence puts their groups,
    /// under ids made from what they are and outside the world's count, off the water, in the pose their hour gives them,
    /// kept to the margin and taken away beyond it, once a second.
    /// </summary>
    public sealed class AnimalStandUpTests
    {
        private const double ExtentM = 4000.0, CellM = 10.0;
        private const int Side = 401;
        private const ulong Seed = 1347UL;
        private const float RooPerKm2 = 30f, BirdPerKm2 = 10f;

        private static readonly Region Plain = new Region("fixture", "Fixture", Region.Bherwerre.CentreLatitudeDeg,
            Region.Bherwerre.CentreLongitudeDeg, ExtentM, 237, 8.0);

        private static readonly AnimalSpecies Roo = AnimalSpecies.EasternGreyKangaroo, Bird = AnimalSpecies.PiedOystercatcher;

        private static RegionRaster Layer(string name, Func<int, int, float> law, string unit = "m") => TestRasters.FromLaw(Side, CellM, ExtentM, name, law, unit);

        /// <summary>
        /// A plain rising gently east, four kilometres a side, at a local hour of the wake's day. Every kind is fed alike
        /// everywhere unless a law is given for the kangaroo; asked for a lake, the western half is one, its shore on the line
        /// east 0, and the oystercatcher is fed nowhere.
        /// </summary>
        private static WorldState World(double localHour, bool lake = false, Func<int, int, float> roo = null)
        {
            RegionRaster ground = Layer("plain", (row, col) => 20f + col * 0.01f);
            CapacitySquares capacity = new CapacitySquares(ExtentM);
            capacity.Add(Roo, Layer("capacity_roo", roo ?? ((row, col) => RooPerKm2), CapacitySquares.Unit));
            capacity.Add(Bird, Layer("capacity_bird", (row, col) => lake ? 0f : BirdPerKm2, CapacitySquares.Unit));
            capacity.Add(AnimalSpecies.SuperbFairyWren, Layer("capacity_wren", (row, col) => 250f, CapacitySquares.Unit));
            WorldWater water = null;
            if (lake)
            {
                RegionRaster surface = Layer("surface", (row, col) => 20f + col * 0.01f + (col < 200 ? 1.5f : 0f));
                RegionRaster classes = TestRasters.FromCodes(Side, CellM, ExtentM, "water", "water", (row, col) => col < 200 ? (uint)WaterClass.Lake : 0u, null);
                water = new WorldWater(surface, classes);
            }
            WorldClock clock = WorldClock.FromLocal(Plain.WakeDayOfYear, localHour, Plain.CentreLongitudeDeg);
            return new WorldState(Seed, Plain, clock, new Heightfield(ground), 0, null, water, capacity: capacity);
        }

        private static AnimalStandUp StandUp(WorldState world)
        {
            foreach (IFastSystem system in world.Systems)
                if (system is AnimalStandUp standUp) return standUp;
            throw new InvalidOperationException("the world has no stand-up");
        }

        /// <summary>The groups presence puts near a point at the world's hour, where every square feeds alike.</summary>
        private static List<AnimalSighting> Groups(WorldState world, AnimalSpecies species, float perKm2, double east, double north, double radiusM)
        {
            SolarClock sun = SolarClock.ForRegion(world.Region, world.Clock);
            return new AnimalPresence(world.Seed).Near(species, perKm2, east, north, radiusM, sun.HourOfDay, sun.DaylightHours, sun.DayOfYear);
        }

        [Test]
        public void EveryGroupNearAFounderStandsUpMemberByMemberAndNoOtherKind()
        {
            WorldState world = World(17.0);
            world.InterestPoints.Add(new Double3(300.0, 0.0, -200.0));
            world.Step(0.05);
            int expected = 0;
            foreach ((AnimalSpecies species, float perKm2) in new[] { (Roo, RooPerKm2), (Bird, BirdPerKm2) })
            {
                List<AnimalSighting> groups = Groups(world, species, perKm2, 300.0, -200.0, AnimalStandUp.StandUpRadiusM);
                Assert.That(groups.Count, Is.GreaterThan(0), species + ": there must be groups near the founder");
                foreach (AnimalSighting g in groups)
                    for (int m = 0; m < g.GroupSize; m++)
                    {
                        expected++;
                        Assert.That(world.Entities.TryGet(AnimalStandUp.IdOf(species, g.CellX, g.CellZ, m), out Entity e), Is.True,
                            species + " member " + m + " of square " + g.CellX + ", " + g.CellZ);
                        Assert.That(e.Definition, Is.SameAs(DefinitionCatalogue.AnimalOf(species)));
                        Assert.That(e.IsTransient && e.HasAnimal, Is.True);
                        double off = Math.Sqrt((e.Position.X - g.EastM) * (e.Position.X - g.EastM) + (e.Position.Z - g.NorthM) * (e.Position.Z - g.NorthM));
                        Assert.That(off, Is.LessThanOrEqualTo(AnimalStandUp.MemberSpacingM * Math.Sqrt(g.GroupSize) + 1e-9), "a member stands round its group's place");
                        Assert.That(e.Position.Y, Is.EqualTo(world.GroundAt(e.Position.X, e.Position.Z)), "on the ground");
                    }
            }
            Assert.That(world.Entities.Transient.Count, Is.EqualTo(expected), "the members of the groups within reach and nothing else: no fairy-wren, none farther off");
            Assert.That(world.Entities.All.Count, Is.Zero, "none of the world's own");
            Assert.That(world.Entities.NextId, Is.EqualTo(1UL), "and the world's count unmoved");
        }

        [Test]
        public void TheSameAnimalsStandUpAgainUnderTheSameIdsInTheSamePlaces()
        {
            WorldState world = World(7.5);
            AnimalStandUp standUp = StandUp(world);
            world.InterestPoints.Add(new Double3(0.0, 0.0, 0.0));
            standUp.Refresh(world);
            world.Entities.EndTick();
            Dictionary<ulong, (Double3, float)> first = new Dictionary<ulong, (Double3, float)>();
            foreach (Entity e in world.Entities.Transient) first[e.Id.Value] = (e.Position, e.YawDeg);
            Assert.That(first.Count, Is.GreaterThan(0));

            world.InterestPoints[0] = new Double3(0.0, 0.0, 1800.0);
            standUp.Refresh(world);
            world.Entities.EndTick();
            List<Entity> retired = new List<Entity>();
            world.Entities.DrainRetired(retired);
            Assert.That(retired.Count, Is.EqualTo(first.Count), "walked 1.8 km from, every one is taken away");
            foreach (Entity e in world.Entities.Transient) Assert.That(first.ContainsKey(e.Id.Value), Is.False, "and those standing there are others");

            world.InterestPoints[0] = new Double3(0.0, 0.0, 0.0);
            standUp.Refresh(world);
            world.Entities.EndTick();
            Assert.That(world.Entities.Transient.Count, Is.EqualTo(first.Count), "walked back, as many stand as did");
            foreach (Entity e in world.Entities.Transient)
            {
                Assert.That(first.TryGetValue(e.Id.Value, out (Double3 Position, float Yaw) was), Is.True, "the same animal, under the same id");
                Assert.That(e.Position.Equals(was.Position) && e.YawDeg == was.Yaw, Is.True, "in the same place, facing the same way");
            }
            Assert.That(world.Entities.NextId, Is.EqualTo(1UL));
        }

        [Test]
        public void NoMemberStandsInWaterAndNoGroupWhoseOwnPlaceIsWetStandsUp()
        {
            // Kangaroos along the shore alone, as thick as a square holds them: a group in every square from 100 m out in the
            // lake to 100 m inland, and in most of the squares beyond that. The founders keep well inside the region, so every
            // group they reach has its own place on the region's ground.
            WorldState world = World(7.5, lake: true, roo: (row, col) => col >= 190 && col <= 215 ? 800f : 0f);
            Func<int, int, double> shore = (cx, cz) => cx == -1 || cx == 0 ? 800.0 : cx == 1 ? 480.0 : 0.0;
            Double3[] walk = { new Double3(0, 0, -1200), new Double3(0, 0, -400), new Double3(0, 0, 400), new Double3(0, 0, 1200) };
            world.InterestPoints.AddRange(walk);
            StandUp(world).Refresh(world);
            world.Entities.EndTick();

            SolarClock sun = SolarClock.ForRegion(world.Region, world.Clock);
            AnimalPresence presence = new AnimalPresence(world.Seed);
            HashSet<long> squares = new HashSet<long>();
            int wetPlaces = 0, drifted = 0;
            foreach (Double3 founder in walk)
                foreach (AnimalSighting g in presence.Near(Roo, shore, founder.X, founder.Z, AnimalStandUp.StandUpRadiusM, sun.HourOfDay, sun.DaylightHours, sun.DayOfYear))
                {
                    if (!squares.Add(((long)g.CellX << 32) | (uint)g.CellZ)) continue;
                    bool standing = world.Entities.TryGet(AnimalStandUp.IdOf(Roo, g.CellX, g.CellZ, 0), out _);
                    if (g.BaseEastM < 0.0)
                    {
                        wetPlaces++;
                        Assert.That(standing, Is.False, "a group whose own place is in the lake is not stood up");
                        continue;
                    }
                    Assert.That(standing, Is.True, "a group whose own place is ashore is");
                    if (g.EastM < 0.0) drifted++;
                }
            Assert.That(wetPlaces, Is.GreaterThan(0), "there must be groups whose own place is wet");
            Assert.That(drifted, Is.GreaterThan(0), "and groups ashore that have wandered into the lake");
            Assert.That(world.Entities.Transient.Count, Is.GreaterThan(0));
            foreach (Entity e in world.Entities.Transient)
                Assert.That(e.Position.X, Is.GreaterThanOrEqualTo(0.0), "every member stands ashore: " + e.Id);
        }

        [Test]
        public void AMobLiesUpAtNoonAndIsUpAtDuskAndTheChangeIsStamped()
        {
            WorldState world = World(12.0);
            world.InterestPoints.Add(new Double3(-600.0, 0.0, 400.0));
            for (int i = 0; i < 20; i++) world.Step(0.05);
            Definition kangaroo = DefinitionCatalogue.AnimalOf(Roo);
            HashSet<ulong> atNoon = new HashSet<ulong>();
            int roos = 0, birds = 0;
            foreach (Entity e in world.Entities.Transient)
            {
                atNoon.Add(e.Id.Value);
                if (e.Definition == kangaroo)
                {
                    roos++;
                    Assert.That(e.Animal.Pose, Is.EqualTo(AnimalPose.Resting), "a kangaroo lies up at noon");
                }
                else
                {
                    birds++;
                    Assert.That(e.Animal.Pose, Is.EqualTo(AnimalPose.Grazing), "an oystercatcher works at noon");
                }
            }
            Assert.That(roos, Is.GreaterThan(0));
            Assert.That(birds, Is.GreaterThan(0));

            // On to sunset, less the step that takes the next tick there.
            SolarClock sun = SolarClock.ForRegion(world.Region, world.Clock);
            world.Clock.Advance((sun.SunsetHour - sun.HourOfDay) / 24.0 * WorldClock.RealSecondsPerDay - 0.05);
            world.Step(0.05);
            int changed = 0;
            foreach (Entity e in world.Entities.Transient)
            {
                Assert.That(e.Animal.Pose, Is.EqualTo(e.Definition == kangaroo ? AnimalPose.Grazing : AnimalPose.Resting),
                    e.Definition == kangaroo ? "a kangaroo is up at sunset" : "and an oystercatcher is resting");
                if (e.Definition != kangaroo || !atNoon.Contains(e.Id.Value)) continue;
                changed++;
                Assert.That(e.AnimalTick, Is.EqualTo(20L), "the change is stamped with the tick it was made in");
                Assert.That(e.ChangedSince(20) & EntityFields.Pose, Is.EqualTo(EntityFields.Pose));
            }
            Assert.That(changed, Is.GreaterThan(0), "there must be kangaroos that stood through the afternoon");
        }

        [Test]
        public void AGroupStandsUpWithinFiveHundredMetresAndIsTakenAwayBeyondFiveHundredAndFifty()
        {
            WorldState world = World(7.5);
            AnimalStandUp standUp = StandUp(world);
            AnimalSighting g = Groups(world, Roo, RooPerKm2, 0.0, 0.0, 1000.0)[0];
            ulong id = AnimalStandUp.IdOf(Roo, g.CellX, g.CellZ, 0);
            void FounderNorthOfTheGroup(double metres)
            {
                world.InterestPoints.Clear();
                world.InterestPoints.Add(new Double3(g.EastM, 0.0, g.NorthM + metres));
                standUp.Refresh(world);
                world.Entities.EndTick();
            }

            FounderNorthOfTheGroup(520.0);
            Assert.That(world.Entities.TryGet(id, out _), Is.False, "520 m off, a group not standing is not stood up");
            FounderNorthOfTheGroup(480.0);
            Assert.That(world.Entities.TryGet(id, out _), Is.True, "480 m off, it is");
            FounderNorthOfTheGroup(540.0);
            Assert.That(world.Entities.TryGet(id, out Entity kept), Is.True, "540 m off, a group standing is kept");
            FounderNorthOfTheGroup(560.0);
            Assert.That(world.Entities.TryGet(id, out _), Is.False, "560 m off, it is taken away");
            Assert.That(kept.Killed, Is.True);
            List<Entity> retired = new List<Entity>();
            world.Entities.DrainRetired(retired);
            Assert.That(retired.Exists(e => e.Id.Value == id), Is.True, "and retired, for its viewers to be told");
        }

        [Test]
        public void TheAnimalsAreStoodUpAndMovedOnTheFirstStepOfEachSecond()
        {
            WorldState world = World(7.5);
            world.InterestPoints.Add(new Double3(0.0, 0.0, 0.0));
            for (int i = 0; i < 30; i++) world.Step(0.05);
            Assert.That(world.Entities.Transient.Count, Is.GreaterThan(0));
            foreach (Entity e in world.Entities.Transient)
                Assert.That(e.PositionTick, Is.EqualTo(20L), "moved at tick 20 and not since: " + e.Id);
            for (int i = 0; i < 11; i++) world.Step(0.05);
            foreach (Entity e in world.Entities.Transient)
                Assert.That(e.PositionTick, Is.EqualTo(40L), "and again at tick 40: " + e.Id);
        }

        [Test]
        public void NoneStandWithoutAFounderOrTheWorldsCapacity()
        {
            WorldState bare = new WorldState(Seed, Plain, Plain.WakeClock());
            bare.InterestPoints.Add(new Double3(0.0, 0.0, 0.0));
            bare.Step(0.05);
            Assert.That(bare.Entities.Transient.Count, Is.Zero, "a world made before its capacity layers stands none up");

            WorldState world = World(7.5);
            world.Step(0.05);
            Assert.That(world.Entities.Transient.Count, Is.Zero, "nor one with no founder in it");
            world.InterestPoints.Add(new Double3(0.0, 0.0, 0.0));
            StandUp(world).Refresh(world);
            world.Entities.EndTick();
            Assert.That(world.Entities.Transient.Count, Is.GreaterThan(0));
            world.InterestPoints.Clear();
            StandUp(world).Refresh(world);
            world.Entities.EndTick();
            Assert.That(world.Entities.Transient.Count, Is.Zero, "and when the last founder goes, every one is taken away");
        }

        [Test]
        public void AnIdIsMadeFromTheKindTheSquareAndTheMember()
        {
            ulong id = AnimalStandUp.IdOf(Roo, -3, 7, 0);
            Assert.That(EntityId.IsTransientValue(id), Is.True);
            HashSet<ulong> ids = new HashSet<ulong>
            {
                id, AnimalStandUp.IdOf(Roo, -3, 7, 1), AnimalStandUp.IdOf(Roo, 7, -3, 0), AnimalStandUp.IdOf(Bird, -3, 7, 0),
                AnimalStandUp.IdOf(Roo, -4, 7, 0), AnimalStandUp.IdOf(Roo, -3, 8, 0),
            };
            Assert.That(ids.Count, Is.EqualTo(6), "each differs by what it is");
            Assert.That(AnimalStandUp.IdOf(Roo, -3, 7, 0), Is.EqualTo(id), "and the same animal has the same id");
            Assert.That(EntityId.IsTransientValue(AnimalStandUp.IdOf(Roo, -(1 << 23), (1 << 23) - 1, 255)), Is.True, "the squares at either end still pack");
            Assert.Throws<ArgumentOutOfRangeException>(() => AnimalStandUp.IdOf(Roo, 1 << 23, 0, 0), "a square beyond what an id packs");
            Assert.Throws<ArgumentOutOfRangeException>(() => AnimalStandUp.IdOf(Roo, 0, 0, 256), "a member beyond what an id packs");
            Assert.Throws<ArgumentException>(() => AnimalStandUp.IdOf(null, 0, 0, 0), "and no kind at all");
        }
    }
}
