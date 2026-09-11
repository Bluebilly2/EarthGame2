using System;
using System.Collections.Generic;
using EarthGame.Engine;
using NUnit.Framework;

namespace EarthGame.Tests.Engine
{
    /// <summary>
    /// The hands (M1.5a promise 4): a thing within reach of the eye is taken out of the world into the first free place,
    /// put down with the id it always had and falls to the ground; what is out of reach, missing or one too many is
    /// refused, and a return is never a new thing.
    /// </summary>
    public sealed class HandsTests
    {
        private static readonly Region Fixture = new Region("fixture", "Fixture", Region.Bherwerre.CentreLatitudeDeg,
            Region.Bherwerre.CentreLongitudeDeg, TestRasters.MadeExtentM, 237, 8.0);

        private WorldState _world;

        [SetUp]
        public void SetUp() => _world = new WorldState(1347UL, Fixture, Fixture.WakeClock(), new Heightfield(TestRasters.MadeCoast()));

        /// <summary>A standing founder's eye over a point of the made coast's plain.</summary>
        private Double3 EyeAt(double east, double north) => new Double3(east, _world.GroundAt(east, north) + MoverConfig.Default.EyeHeight(Stance.Standing), north);

        private Double3 Ground(double east, double north) => new Double3(east, _world.GroundAt(east, north), north);

        [Test]
        public void AThingWithinReachIsTakenOutOfTheWorldAndPutBackWithItsOwnId()
        {
            Entity cobble = _world.SpawnItem(DefinitionCatalogue.Cobble, 301, -300);
            _world.Step(0.05);
            Hands hands = new Hands();
            Double3 eye = EyeAt(300, -300);
            Assert.That(hands.PickUp(_world, cobble.Id.Value, eye), Is.EqualTo(VerbOutcome.Done));
            Assert.That(cobble.Taken && cobble.Killed, Is.True, "taken, so it leaves at the end of the tick");
            Assert.That(hands.Things.Count, Is.EqualTo(1));
            Assert.That(hands.Things[0].Place, Is.EqualTo((byte)1));
            Assert.That(hands.Hand, Is.EqualTo((byte)1), "an empty hand takes what is picked up");
            Assert.That(hands.PutDown(_world, Ground(300, -298), eye, 0f), Is.EqualTo(VerbOutcome.NotNow), "still in the store until the tick ends");
            _world.Step(0.05);
            Assert.That(_world.Entities.TryGet(cobble.Id, out _), Is.False);
            List<Entity> retired = new List<Entity>();
            _world.Entities.DrainRetired(retired);
            Assert.That(retired, Is.EquivalentTo(new[] { cobble }));

            Assert.That(hands.PutDown(_world, Ground(310, -300), eye, 0f), Is.EqualTo(VerbOutcome.OutOfReach));
            Assert.That(hands.PutDown(_world, Ground(300, -298), eye, 45f), Is.EqualTo(VerbOutcome.Done));
            Assert.That(hands.Things.Count, Is.EqualTo(0));
            Assert.That(hands.Hand, Is.EqualTo((byte)1), "the hand stays where it was, empty");
            Assert.That(_world.Entities.TryGet(cobble.Id, out Entity back), Is.True, "the same id");
            Assert.That(back, Is.Not.SameAs(cobble));
            Assert.That(back.SpawnTick, Is.EqualTo(cobble.SpawnTick));
            Assert.That(back.YawDeg, Is.EqualTo(45f));
            double ground = _world.GroundAt(300, -298);
            Assert.That(back.Position.Y, Is.EqualTo(ground + Hands.ReleaseM).Within(1e-9), "let go above the ground");
            Assert.That(back.Item.Resting, Is.False);
            for (int i = 0; i < 20 && !back.Item.Resting; i++) _world.Step(0.05);
            Assert.That(back.Item.Resting, Is.True, "it fell and came to rest");
            Assert.That(back.Position.Y, Is.EqualTo(ground).Within(1e-9));
            Assert.That(hands.PutDown(_world, Ground(300, -298), eye, 0f), Is.EqualTo(VerbOutcome.NothingInHand));
        }

        [Test]
        public void WhatIsOutOfReachMissingOrOneTooManyIsRefused()
        {
            Hands hands = new Hands();
            Double3 eye = EyeAt(300, -300);
            Entity far = _world.SpawnItem(DefinitionCatalogue.Stick, 305, -305);
            List<Entity> near = new List<Entity>();
            for (int i = 0; i < Hands.Places + 1; i++) near.Add(_world.SpawnItem(DefinitionCatalogue.Cobble, 300 + 0.2 * i, -299));
            _world.Step(0.05);
            Assert.That(hands.PickUp(_world, far.Id.Value, eye), Is.EqualTo(VerbOutcome.OutOfReach), "7 m from the eye");
            Assert.That(hands.PickUp(_world, 999, eye), Is.EqualTo(VerbOutcome.NotThere), "no such entity");
            for (int i = 0; i < Hands.Places; i++)
                Assert.That(hands.PickUp(_world, near[i].Id.Value, eye), Is.EqualTo(VerbOutcome.Done), "place " + (i + 1));
            Assert.That(hands.PickUp(_world, near[0].Id.Value, eye), Is.EqualTo(VerbOutcome.NotThere), "already taken");
            Assert.That(hands.PickUp(_world, near[Hands.Places].Id.Value, eye), Is.EqualTo(VerbOutcome.HandsFull));
            Assert.That(hands.FreePlace(), Is.EqualTo((byte)0));
            Assert.That(hands.Hand, Is.EqualTo((byte)1), "the hand took the first and kept it");
            Assert.That(hands.Hold((byte)(Hands.Places + 1)), Is.EqualTo(VerbOutcome.NoSuchPlace));
            Assert.That(hands.Hold(3), Is.EqualTo(VerbOutcome.Done));
            _world.Step(0.05);
            Assert.That(hands.PutDown(_world, Ground(300, -298), eye, 0f), Is.EqualTo(VerbOutcome.Done), "the thing in place 3");
            Assert.That(hands.TryAt(3, out _), Is.False);
            Assert.That(hands.FreePlace(), Is.EqualTo((byte)3), "the first free place is the one just emptied");
            Assert.That(hands.PickUp(_world, near[Hands.Places].Id.Value, eye), Is.EqualTo(VerbOutcome.Done));
            Assert.That(hands.TryAt(3, out CarriedThing last) && last.Id == near[Hands.Places].Id.Value, Is.True);
            Assert.That(hands.Hold(0), Is.EqualTo(VerbOutcome.Done));
            Assert.That(hands.PutDown(_world, Ground(300, -298), eye, 0f), Is.EqualTo(VerbOutcome.NothingInHand), "an empty hand puts nothing down");
        }

        [Test]
        public void RestoringTheHandsRefusesWhatNoSaveCouldHold()
        {
            Hands hands = new Hands();
            CarriedThing a = new CarriedThing { Id = 1, Definition = DefinitionCatalogue.Cobble, Place = 2 };
            CarriedThing b = new CarriedThing { Id = 2, Definition = DefinitionCatalogue.Stick, Place = 2 };
            Assert.Throws<ArgumentException>(() => hands.Restore(new[] { a, b }, 2), "two in one place");
            Assert.Throws<ArgumentException>(() => hands.Restore(new[] { new CarriedThing { Id = 1, Definition = DefinitionCatalogue.Cobble, Place = 0 } }, 0), "place 0 is no place");
            Assert.Throws<ArgumentException>(() => hands.Restore(new[] { new CarriedThing { Id = 1, Place = 1 } }, 1), "no definition");
            b.Place = 7;
            hands.Restore(new[] { b, a }, 7);
            Assert.That(hands.Things[0].Place, Is.EqualTo((byte)2), "kept in order of place");
            Assert.That(hands.Things[1].Place, Is.EqualTo((byte)7));
            Assert.That(hands.Hand, Is.EqualTo((byte)7));
            Assert.That(hands.Record("William").Things.Count, Is.EqualTo(2));
            hands.Restore(null, 0);
            Assert.That(hands.Things.Count, Is.EqualTo(0), "a save from before the hands carries nothing");
        }

        [Test]
        public void AReturnIsNeverANewThing()
        {
            EntityStore store = new EntityStore();
            store.Spawn(DefinitionCatalogue.Cobble, new Double3(0, 0, 0), 0f, 1);
            Entity b = store.Spawn(DefinitionCatalogue.Cobble, new Double3(1, 0, 0), 0f, 1);
            store.Spawn(DefinitionCatalogue.Cobble, new Double3(2, 0, 0), 0f, 1);
            store.Take(b);
            Assert.That(b.Killed && b.Taken, Is.True);
            Assert.Throws<ArgumentException>(() => store.Return(2, DefinitionCatalogue.Cobble, default, 0f, 1, 5), "still in the store this tick");
            store.EndTick();
            Assert.Throws<ArgumentException>(() => store.Return(0, DefinitionCatalogue.Cobble, default, 0f, 1, 5), "id 0");
            Assert.Throws<ArgumentException>(() => store.Return(4, DefinitionCatalogue.Cobble, default, 0f, 1, 5), "never allocated");
            Assert.Throws<ArgumentException>(() => store.Return(1, DefinitionCatalogue.Cobble, default, 0f, 1, 5), "present");
            Entity back = store.Return(2, DefinitionCatalogue.Cobble, new Double3(5, 1, 5), 30f, 1, 5);
            Assert.That(store.All[1], Is.SameAs(back), "in id order");
            Assert.That(back.Initialised, Is.False, "not until the end of the tick, as a spawn");
            Assert.That(back.ChangedSince(5), Is.EqualTo(EntityFields.Position | EntityFields.Yaw), "stamped with the tick it came back");
            Assert.That(back.SpawnTick, Is.EqualTo(1L));
            Assert.That(store.NextId, Is.EqualTo(4UL), "no id allocated");
            store.EndTick();
            Assert.That(back.Initialised, Is.True);
        }
    }
}
