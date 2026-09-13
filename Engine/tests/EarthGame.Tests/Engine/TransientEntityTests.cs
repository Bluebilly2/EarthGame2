using System;
using System.Collections.Generic;
using EarthGame.Engine;
using NUnit.Framework;

namespace EarthGame.Tests.Engine
{
    /// <summary>
    /// Animals held apart from the world's own entities (M1.7a promise 2): their ids come from a reserved range and never
    /// move the store's counter, the world's digest never names them, and one taken away is retired as having left.
    /// </summary>
    public sealed class TransientEntityTests
    {
        private static readonly Definition Roo = DefinitionCatalogue.ByKey("animal/eastern-grey-kangaroo");
        private const ulong RooId = EntityId.TransientBit | 12345UL;

        private static AnimalComponent Grazing()
        {
            AnimalComponent a;
            a.Pose = AnimalPose.Grazing;
            return a;
        }

        [Test]
        public void AnAnimalTakesAReservedIdAndTheWorldsOwnCountMovesOn()
        {
            EntityStore store = new EntityStore();
            Entity cobble = store.Spawn(DefinitionCatalogue.Cobble, new Double3(0, 0, 0), 0f, 1);
            Entity roo = store.StandUp(RooId, Roo, new Double3(3, 0, 4), 90f, Grazing(), 1);
            Assert.That(roo.Id.Value, Is.EqualTo(RooId));
            Assert.That(roo.IsTransient, Is.True);
            Assert.That(cobble.IsTransient, Is.False);
            Assert.That(store.NextId, Is.EqualTo(2UL), "an animal never moves the world's count");
            Assert.That(store.All.Count, Is.EqualTo(1), "the world's own entities are the cobble alone");
            Assert.That(store.Transient.Count, Is.EqualTo(1));
            Assert.That(store.TryGet(RooId, out Entity found), Is.True);
            Assert.That(found, Is.SameAs(roo));
            Assert.That(store.Spawn(DefinitionCatalogue.Cobble, default, 0f, 2).Id.Value, Is.EqualTo(2UL), "the next of the world's own takes the next id");
            store.SetNextId(3);
            Assert.That(store.NextId, Is.EqualTo(3UL), "and the counter is set past the world's own, not past the animal");

            List<Entity> near = new List<Entity>();
            store.Within(0, 0, 10.0, near);
            Assert.That(near.Count, Is.EqualTo(3), "a search for what is near finds both kinds");
            Assert.That(near[near.Count - 1], Is.SameAs(roo), "the world's own first, then the animals");
            Assert.That(roo.HasAnimal, Is.True);
            Assert.That(roo.Record().HasAnimal, Is.True);
            Assert.That(roo.Record().Animal.Pose, Is.EqualTo(AnimalPose.Grazing));
            Assert.That(roo.ChangedSince(1), Is.EqualTo(EntityFields.Position | EntityFields.Yaw | EntityFields.Pose), "a stand-up stamps its fields");
        }

        [Test]
        public void OnlyAnAnimalOfTheReservedRangeStandsUp()
        {
            EntityStore store = new EntityStore();
            Assert.Throws<ArgumentException>(() => store.StandUp(12345UL, Roo, default, 0f, Grazing(), 0), "an id outside the reserved range");
            Assert.Throws<ArgumentException>(() => store.StandUp(RooId, DefinitionCatalogue.Cobble, default, 0f, Grazing(), 0), "a thing that is not an animal");
            store.StandUp(RooId, Roo, default, 0f, Grazing(), 0);
            Assert.Throws<ArgumentException>(() => store.StandUp(RooId, Roo, default, 0f, Grazing(), 0), "an id already standing");
            Assert.Throws<ArgumentException>(() => store.Spawn(Roo, default, 0f, 0), "and an animal is still not spawnable as the world's own");
        }

        [Test]
        public void AnAnimalTakenAwayLeavesAtTheEndOfTheTickAsHavingLeft()
        {
            EntityStore store = new EntityStore();
            Entity roo = store.StandUp(RooId, Roo, new Double3(3, 0, 4), 0f, Grazing(), 5);
            store.EndTick();
            Assert.That(roo.Initialised, Is.True);
            store.Kill(roo);
            Assert.That(store.Transient.Count, Is.EqualTo(1), "not until the end of the tick");
            store.EndTick();
            Assert.That(store.Transient.Count, Is.Zero);
            Assert.That(store.TryGet(RooId, out _), Is.False);
            List<Entity> retired = new List<Entity>();
            Assert.That(store.DrainRetired(retired), Is.EqualTo(1));
            Assert.That(retired[0].IsTransient, Is.True, "the server tells its viewers it left rather than died");
            Entity again = store.StandUp(RooId, Roo, new Double3(3, 0, 4), 0f, Grazing(), 9);
            Assert.That(again.Id.Value, Is.EqualTo(RooId), "the same animal stood up again has the same id");
        }

        [Test]
        public void TheWorldsDigestNeverNamesAnAnimal()
        {
            WorldState world = new WorldState(1347UL, Region.Bherwerre, Region.Bherwerre.WakeClock());
            world.SpawnItem(DefinitionCatalogue.Cobble, 10.0, 20.0);
            string without = WorldDigest.World(world, Array.Empty<KeyValuePair<string, MoverState>>());
            world.Entities.StandUp(RooId, Roo, new Double3(3, 0, 4), 45f, Grazing(), world.Tick);
            string with = WorldDigest.World(world, Array.Empty<KeyValuePair<string, MoverState>>());
            Assert.That(with, Is.EqualTo(without), "an animal is presence, not the world's state");

            EntityRecord record = world.Entities.Transient[0].Record();
            string grazing = WorldDigest.Entities(new[] { record });
            record.Animal.Pose = AnimalPose.Resting;
            Assert.That(WorldDigest.Entities(new[] { record }), Is.Not.EqualTo(grazing), "while a client's mirror of it is named with its pose");
        }
    }
}
