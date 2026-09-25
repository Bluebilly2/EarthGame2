using System;
using System.Collections.Generic;
using EarthGame.Engine;
using NUnit.Framework;

namespace EarthGame.Tests.Engine
{
    /// <summary>
    /// The animals notice founders (M1.7c promises 1 to 4 and 8): a group a founder comes within its kind's distance of runs
    /// straight away from them at its speed, its members keeping their places and moved every step; runs its length, stands,
    /// walks back, and is presence's again; keeps to dry ground, turning along a shore; is sent off again by a founder who
    /// comes near again; and forgets its flight when it is taken away. On the stand-up tests' plain, at dusk, when a mob is up.
    /// </summary>
    public sealed class AnimalFlightTests
    {
        private const double Dt = 0.05;
        private static readonly AnimalSpecies Roo = AnimalStandUpTests.Roo;

        /// <summary>
        /// A mob near the plain's middle, with the founder stood a distance north of its wandered place, and the world stepped
        /// once so it stands: a flight can begin in that very step, so the listener is joined first. The clock is held still
        /// (a developer's rate of nought), so presence's places stand while a mob runs; running, on the thirty-minute day
        /// before ruling 52 (2026-09-25), the world went forty-eight times as fast as the test's seconds and the wander alone
        /// moved a mob's home six metres in a run.
        /// </summary>
        private static (WorldState World, AnimalStandUp StandUp, AnimalSighting Group) AMobWithAFounder(double northOfItM, List<AnimalFlight> flights = null)
        {
            WorldState world = AnimalStandUpTests.World(17.0);
            world.Clock.Scale = 0.0;
            AnimalStandUp standUp = AnimalStandUpTests.StandUp(world);
            if (flights != null) standUp.Fled += flights.Add;
            List<AnimalSighting> groups = AnimalStandUpTests.Groups(world, Roo, AnimalStandUpTests.RooPerKm2, 0.0, 0.0, 1000.0);
            Assert.That(groups.Count, Is.GreaterThan(0), "there must be mobs near the middle");
            AnimalSighting g = groups[0];
            world.InterestPoints.Add(new Double3(g.EastM, 0.0, g.NorthM + northOfItM));
            world.Step(Dt);
            Assert.That(world.Entities.TryGet(AnimalStandUp.IdOf(Roo, g.CellX, g.CellZ, 0), out _), Is.True, "the mob stands");
            return (world, standUp, g);
        }

        private static List<Entity> Members(WorldState world, AnimalSighting g)
        {
            List<Entity> members = new List<Entity>();
            for (int m = 0; m < g.GroupSize; m++)
                if (world.Entities.TryGet(AnimalStandUp.IdOf(Roo, g.CellX, g.CellZ, m), out Entity e) && !e.Killed) members.Add(e);
            return members;
        }

        private static Double3 Centre(List<Entity> members)
        {
            double east = 0.0, north = 0.0;
            foreach (Entity e in members)
            {
                east += e.Position.X;
                north += e.Position.Z;
            }
            return new Double3(east / members.Count, 0.0, north / members.Count);
        }

        [Test]
        public void AMobRunsFromAFounderWithinItsDistanceAndNotFromOneBeyondIt()
        {
            List<AnimalFlight> flights = new List<AnimalFlight>();
            (WorldState world, AnimalStandUp standUp, AnimalSighting g) = AMobWithAFounder(100.0, flights);
            List<Entity> members = Members(world, g);
            Assert.That(members.Count, Is.EqualTo(g.GroupSize));
            Dictionary<ulong, Double3> before = new Dictionary<ulong, Double3>();
            foreach (Entity e in members) before[e.Id.Value] = e.Position;
            world.Step(Dt);
            foreach (Entity e in members)
            {
                Assert.That(e.Animal.Pose, Is.Not.EqualTo(AnimalPose.Fleeing), "a founder a hundred metres off startles nothing");
                Assert.That(e.Position.Equals(before[e.Id.Value]), Is.True, "and a standing member is not moved between refreshes");
            }
            Assert.That(flights, Is.Empty);
            Assert.That(standUp.HasFlight(Roo, g.CellX, g.CellZ), Is.False);

            // The founder comes to sixty metres: within eighty of the nearest member, however the mob is spread.
            world.InterestPoints[0] = new Double3(g.EastM, 0.0, g.NorthM + 60.0);
            world.Step(Dt);
            Assert.That(flights.Count, Is.EqualTo(1), "one flight, recorded as it begins");
            Assert.That(flights[0].Species, Is.SameAs(Roo));
            Assert.That(flights[0].DistanceM, Is.LessThanOrEqualTo(AnimalFlightRules.KangarooFleeWithinM), "the founder was within the kind's distance of a member");
            Assert.That(Math.Abs(flights[0].BearingDeg - 180.0), Is.LessThan(30.0), "it runs south, away from a founder to the north");
            Assert.That(standUp.IsRunning(Roo, g.CellX, g.CellZ), Is.True);
            foreach (Entity e in members)
            {
                Assert.That(e.Animal.Pose, Is.EqualTo(AnimalPose.Fleeing), "every member is fleeing");
                Assert.That(e.AnimalTick, Is.EqualTo(world.Tick - 1), "stamped with the step it began in");
                Assert.That(e.PositionTick, Is.EqualTo(world.Tick - 1), "and moved in it");
                Double3 was = before[e.Id.Value];
                Assert.That(was.Z - e.Position.Z, Is.GreaterThan(0.0), "each member has moved south");
                double moved = Math.Sqrt((e.Position.X - was.X) * (e.Position.X - was.X) + (e.Position.Z - was.Z) * (e.Position.Z - was.Z));
                Assert.That(moved, Is.EqualTo(AnimalFlightRules.KangarooRunMs * Dt).Within(1e-6), "by one step at the mob's speed");
            }
        }

        [Test]
        public void AMobRunsItsLengthAtItsSpeedStandsAWhileThenWalksBackAndIsPresencesAgain()
        {
            List<AnimalFlight> flights = new List<AnimalFlight>();
            (WorldState world, AnimalStandUp standUp, AnimalSighting g) = AMobWithAFounder(60.0, flights);
            List<Entity> members = Members(world, g);
            Assert.That(flights.Count, Is.EqualTo(1), "the flight began in the step that stood the mob up");
            // Where presence puts the mob: its members' places less the one step already run.
            Double3 first = Centre(members);
            double bearing = flights[0].BearingDeg * GeoMath.DegToRad;
            Double3 start = new Double3(first.X - Math.Sin(bearing) * AnimalFlightRules.KangarooRunMs * Dt, 0.0, first.Z - Math.Cos(bearing) * AnimalFlightRules.KangarooRunMs * Dt);
            Assert.That(standUp.IsRunning(Roo, g.CellX, g.CellZ), Is.True);
            // The founder steps well back, so the mob's return is not another flight, and stays within the take-away.
            world.InterestPoints[0] = new Double3(g.EastM, 0.0, g.NorthM + 300.0);

            // Two seconds on: fourteen metres at seven a second.
            for (int i = 0; i < 40; i++) world.Step(Dt);
            double ran = Double3.Distance(Centre(members), start);
            Assert.That(ran, Is.EqualTo(AnimalFlightRules.KangarooRunMs * 41 * Dt).Within(0.5), "two seconds of running: " + ran.ToString("0.0") + " m");
            Assert.That(standUp.IsRunning(Roo, g.CellX, g.CellZ), Is.True);

            // On until it has run its length: it stands, grazing, its members no longer moved every step.
            int steps = 0;
            while (standUp.IsRunning(Roo, g.CellX, g.CellZ) && steps++ < 2000) world.Step(Dt);
            Assert.That(steps, Is.LessThan(2000), "the run ends");
            double away = Double3.Distance(Centre(members), start);
            Assert.That(away, Is.EqualTo(AnimalFlightRules.KangarooRunM).Within(0.5), "it ran its length: " + away.ToString("0.0") + " m");
            Assert.That(standUp.HasFlight(Roo, g.CellX, g.CellZ), Is.True, "and stands away from where presence puts it");
            foreach (Entity e in members) Assert.That(e.Animal.Pose, Is.EqualTo(AnimalPose.Grazing), "grazing where it stopped");
            // A step no refresh falls on moves no standing member.
            if (AnimalStandUp.RefreshesAt(world.Tick, Dt)) world.Step(Dt);
            long quietTick = world.Tick;
            world.Step(Dt);
            foreach (Entity e in members) Assert.That(e.PositionTick, Is.Not.EqualTo(quietTick), "a standing member is moved only by a refresh");

            // After its stand it walks back, and presence has it again where it was.
            steps = 0;
            while (standUp.HasFlight(Roo, g.CellX, g.CellZ) && steps++ < 6000) world.Step(Dt);
            Assert.That(steps, Is.LessThan(6000), "it walks all the way back");
            double back = Double3.Distance(Centre(members), start);
            Assert.That(back, Is.LessThan(0.5), "back where presence puts it: " + back.ToString("0.00") + " m off");
        }

        [Test]
        public void AFounderWhoComesNearAgainSendsAStandingMobOffAgain()
        {
            List<AnimalFlight> flights = new List<AnimalFlight>();
            (WorldState world, AnimalStandUp standUp, AnimalSighting g) = AMobWithAFounder(60.0, flights);
            world.InterestPoints[0] = new Double3(g.EastM, 0.0, g.NorthM + 300.0);
            int steps = 0;
            while (standUp.IsRunning(Roo, g.CellX, g.CellZ) && steps++ < 2000) world.Step(Dt);
            Assert.That(flights.Count, Is.EqualTo(1));
            // The founder walks up to the standing mob: it runs again, from where it stood.
            Double3 centre = Centre(Members(world, g));
            world.InterestPoints[0] = new Double3(centre.X, 0.0, centre.Z + 50.0);
            world.Step(Dt);
            Assert.That(flights.Count, Is.EqualTo(2), "sent off again");
            Assert.That(standUp.IsRunning(Roo, g.CellX, g.CellZ), Is.True);
            Assert.That(flights[1].DistanceM, Is.LessThanOrEqualTo(AnimalFlightRules.KangarooFleeWithinM));
        }

        [Test]
        public void AMobTakenAwayForgetsItsFlightAndStandsUpAgainWherePresencePutsIt()
        {
            (WorldState world, AnimalStandUp standUp, AnimalSighting g) = AMobWithAFounder(60.0);
            Double3 start = Centre(Members(world, g));
            for (int i = 0; i < 100; i++) world.Step(Dt);
            Assert.That(Double3.Distance(Centre(Members(world, g)), start), Is.GreaterThan(30.0), "well away by now");
            Assert.That(standUp.IsRunning(Roo, g.CellX, g.CellZ), Is.True);
            // Walked two kilometres off: the mob is taken away at the next refresh, its flight with it.
            world.InterestPoints[0] = new Double3(g.EastM, 0.0, g.NorthM + 2000.0);
            for (int i = 0; i < 21; i++) world.Step(Dt);
            Assert.That(world.Entities.TryGet(AnimalStandUp.IdOf(Roo, g.CellX, g.CellZ, 0), out _), Is.False, "taken away");
            Assert.That(standUp.HasFlight(Roo, g.CellX, g.CellZ), Is.False, "and its flight forgotten");
            // Back at a distance that stands it up without startling it: it stands where presence puts it, not where it ran to.
            world.InterestPoints[0] = new Double3(g.EastM, 0.0, g.NorthM + 300.0);
            for (int i = 0; i < 21; i++) world.Step(Dt);
            List<Entity> again = Members(world, g);
            Assert.That(again.Count, Is.EqualTo(g.GroupSize), "stood up again");
            Assert.That(Double3.Distance(Centre(again), start), Is.LessThan(1.0), "where presence puts it, not where it ran to: " + Double3.Distance(Centre(again), start).ToString("0.0") + " m off");
            Assert.That(standUp.HasFlight(Roo, g.CellX, g.CellZ), Is.False);
        }

        [Test]
        public void AMobRunningAtALakeTurnsAlongTheShoreAndNeverEntersIt()
        {
            // Kangaroos along the shore alone, thick enough that a mob stands within a hundred metres of it; the founder comes at
            // one from the east, so it runs west, at the lake.
            WorldState world = AnimalStandUpTests.World(17.0, lake: true, roo: (row, col) => col >= 200 && col <= 215 ? 800f : 0f);
            world.Clock.Scale = 0.0;
            AnimalStandUp standUp = AnimalStandUpTests.StandUp(world);
            SolarClock sun = SolarClock.ForRegion(world.Region, world.Clock);
            AnimalSighting? chosen = null;
            foreach (AnimalSighting g in new AnimalPresence(world.Seed).Near(Roo, (cx, cz) => cx == 0 ? 800.0 : 0.0, 50.0, 0.0, 600.0, sun.HourOfDay, sun.DaylightHours, sun.DayOfYear))
                if (g.BaseEastM >= 20.0 && g.BaseEastM <= 100.0 && Math.Abs(g.BaseNorthM) < 400.0)
                {
                    chosen = g;
                    break;
                }
            Assert.That(chosen.HasValue, Is.True, "a mob stands within a hundred metres of the shore");
            AnimalSighting mob = chosen.Value;
            world.InterestPoints.Add(new Double3(mob.EastM + 300.0, 0.0, mob.NorthM));
            world.Step(Dt);
            Assert.That(world.Entities.TryGet(AnimalStandUp.IdOf(Roo, mob.CellX, mob.CellZ, 0), out _), Is.True, "the mob stands");
            world.InterestPoints[0] = new Double3(mob.EastM + 60.0, 0.0, mob.NorthM);
            List<AnimalFlight> flights = new List<AnimalFlight>();
            standUp.Fled += flights.Add;
            world.Step(Dt);
            // The shore is thick with mobs, and the founder may have sent several off: the chosen mob's flight is the one held.
            int own = flights.FindIndex(f => f.CellX == mob.CellX && f.CellZ == mob.CellZ);
            Assert.That(own, Is.GreaterThanOrEqualTo(0), "the chosen mob took flight (" + flights.Count + " flight(s) began)");
            Assert.That(Math.Abs(flights[own].BearingDeg - 270.0), Is.LessThan(30.0), "it runs west, at the lake");
            List<Entity> members = Members(world, mob);
            Double3 start = Centre(members);
            int steps = 0;
            while (standUp.IsRunning(Roo, mob.CellX, mob.CellZ) && steps++ < 2000)
            {
                world.Step(Dt);
                foreach (Entity e in members) Assert.That(e.Position.X, Is.GreaterThanOrEqualTo(0.0), "a member ran into the lake at step " + steps);
            }
            Assert.That(steps, Is.LessThan(2000));
            Assert.That(Double3.Distance(Centre(members), start), Is.GreaterThan(60.0), "turned along the shore, it still ran");
        }
    }
}
