using System;
using System.Collections.Generic;
using EarthGame.Engine;
using NUnit.Framework;

namespace EarthGame.Tests.Engine
{
    /// <summary>The tiered tick (M1.3 promise 3): the fast systems in order, the slow layers by cell and distance.</summary>
    public sealed class TieredTickTests
    {
        private static readonly Region Fixture = new Region("fixture", "Fixture", Region.Bherwerre.CentreLatitudeDeg,
            Region.Bherwerre.CentreLongitudeDeg, TestRasters.MadeExtentM, 237, 8.0);

        private const double Dt = 0.05;

        private static WorldState World() => new WorldState(1347UL, Fixture, Fixture.WakeClock(), new Heightfield(TestRasters.MadeCoast()));

        [Test]
        public void ADroppedItemFallsAtGravityAndRestsOnTheGround()
        {
            WorldState world = World();
            double ground = world.GroundAt(300, -300);
            Entity e = world.SpawnItem(DefinitionCatalogue.Cobble, 300, -300, ground + 2.0);
            Assert.That(e.HasItem, Is.True);
            Assert.That(e.Item.Resting, Is.False);
            int steps = 0;
            while (!e.Item.Resting && steps < 40)
            {
                double before = e.Position.Y;
                world.Step(Dt);
                steps++;
                Assert.That(e.Position.Y, Is.LessThan(before), "it falls every step until it rests");
            }
            // sqrt(2 * 2 m / 9.81) = 0.64 s: thirteen steps of 0.05 s, the fourteenth lands it.
            Assert.That(steps, Is.InRange(12, 15), "steps to rest: " + steps);
            Assert.That(e.Position.Y, Is.EqualTo(ground).Within(1e-9), "on the ground the server holds");
            Assert.That(e.Item.FallSpeed, Is.EqualTo(0f));
            Assert.That(e.PositionTick, Is.EqualTo(steps - 1), "stamped with the tick it landed in");
            long landed = world.Tick;
            world.Step(Dt);
            Assert.That(e.PositionTick, Is.LessThan(landed), "at rest nothing is stamped");
            Assert.That(e.Initialised, Is.True);
        }

        [Test]
        public void AnItemDroppedOnTheGroundRestsAtOnceAndOneDroppedBelowItIsLifted()
        {
            WorldState world = World();
            double ground = world.GroundAt(300, -300);
            Entity onGround = world.SpawnItem(DefinitionCatalogue.Stick, 300, -300);
            Assert.That(onGround.Item.Resting, Is.True);
            Assert.That(onGround.Position.Y, Is.EqualTo(ground).Within(1e-9));
            Entity below = world.SpawnItem(DefinitionCatalogue.Stick, 310, -300, ground - 5.0);
            Assert.That(below.Position.Y, Is.EqualTo(world.GroundAt(310, -300)).Within(1e-9), "never under the ground");
            Assert.That(below.Item.Resting, Is.True);
        }

        [Test]
        public void AKilledItemLeavesAtTheEndOfTheStep()
        {
            WorldState world = World();
            Entity e = world.SpawnItem(DefinitionCatalogue.Cobble, 0, 0);
            world.Entities.Kill(e);
            Assert.That(world.Entities.Count, Is.EqualTo(1));
            world.Step(Dt);
            Assert.That(world.Entities.Count, Is.EqualTo(0));
            List<Entity> retired = new List<Entity>();
            Assert.That(world.Entities.DrainRetired(retired), Is.EqualTo(1));
            Assert.That(retired[0], Is.SameAs(e));
        }

        private sealed class CountingLayer : ISlowLayer
        {
            public readonly Dictionary<(int, int), List<double>> Calls = new Dictionary<(int, int), List<double>>();
            public string Name => "counting";

            public void AdvanceTo(int cellX, int cellZ, double worldHours)
            {
                if (!Calls.TryGetValue((cellX, cellZ), out List<double> list)) Calls[(cellX, cellZ)] = list = new List<double>();
                list.Add(worldHours);
            }
        }

        [Test]
        public void SlowLayersAdvanceNearCellsEveryTickAndFarCellsOnTheSlowCadenceCatchingUpInOneCall()
        {
            WorldState world = World();
            CountingLayer layer = new CountingLayer();
            world.Scheduler.Layers.Add(layer);
            world.Scheduler.InterestRadiusM = 100.0;
            world.InterestPoints.Add(new Double3(0, 0, 0));
            Assert.That(RegionCells.CountAcross(TestRasters.MadeExtentM), Is.EqualTo(4), "1600 m is four cells of 512 m, the last one partial");
            Assert.That(RegionCells.IndexOf(0.0, TestRasters.MadeExtentM), Is.EqualTo(1));
            Assert.That(RegionCells.DistanceTo(1, 1, 0, 0, TestRasters.MadeExtentM), Is.EqualTo(0.0));
            Assert.That(RegionCells.DistanceTo(0, 1, 0, 0, TestRasters.MadeExtentM), Is.EqualTo(288.0).Within(1e-9), "the next cell's edge is at -288 m");
            double hoursBefore = world.Clock.TotalHours;
            const int Ticks = 120;
            for (int i = 0; i < Ticks; i++) world.Step(Dt);
            double hoursPerTick = (world.Clock.TotalHours - hoursBefore) / Ticks;
            Assert.That(layer.Calls[(1, 1)].Count, Is.EqualTo(Ticks), "the cell the player stands in, every tick");
            Assert.That(layer.Calls[(1, 1)][Ticks - 1], Is.EqualTo(world.Clock.TotalHours));
            List<double> far = layer.Calls[(0, 0)];
            Assert.That(far.Count, Is.EqualTo(Ticks / SlowScheduler.FarCadenceTicks), "a far cell every sixty ticks");
            Assert.That(far[1] - far[0], Is.EqualTo(SlowScheduler.FarCadenceTicks * hoursPerTick).Within(1e-9), "one call spans the whole gap");
            Assert.That(world.Scheduler.TryAdvancedTo(0, 0, out double advanced), Is.True);
            Assert.That(advanced, Is.EqualTo(far[far.Count - 1]));
            int farTotal = 0;
            foreach (KeyValuePair<(int, int), List<double>> pair in layer.Calls)
                if (pair.Key != (1, 1)) farTotal += pair.Value.Count;
            Assert.That(farTotal, Is.EqualTo(15 * (Ticks / SlowScheduler.FarCadenceTicks)), "fifteen far cells, each on its own phase");
        }

        [Test]
        public void TheFastSystemsRunInTheStatedOrder()
        {
            WorldState world = World();
            Assert.That(world.Systems.Count, Is.EqualTo(1));
            Assert.That(world.Systems[0], Is.TypeOf<ItemFall>());
            Assert.That(world.Systems[0].Name, Is.EqualTo("item fall"));
        }
    }
}
