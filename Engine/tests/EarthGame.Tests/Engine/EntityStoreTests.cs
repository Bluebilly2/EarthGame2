using System;
using System.Collections.Generic;
using EarthGame.Engine;
using NUnit.Framework;

namespace EarthGame.Tests.Engine
{
    /// <summary>The entity store (M1.3 promise 2): ids in order and never reused, kills at the end of the tick, changes stamped per field.</summary>
    public sealed class EntityStoreTests
    {
        private static readonly Definition Cobble = DefinitionCatalogue.Cobble;

        [Test]
        public void IdsAreAllocatedInOrderAndNeverReused()
        {
            EntityStore store = new EntityStore();
            Entity a = store.Spawn(Cobble, new Double3(0, 0, 0), 0f, 1);
            Entity b = store.Spawn(Cobble, new Double3(1, 0, 0), 0f, 1);
            Entity c = store.Spawn(Cobble, new Double3(2, 0, 0), 0f, 1);
            Assert.That(a.Id.Value, Is.EqualTo(1UL));
            Assert.That(b.Id.Value, Is.EqualTo(2UL));
            Assert.That(c.Id.Value, Is.EqualTo(3UL));
            Assert.That(a.Initialised, Is.False, "not until the end of the tick");
            store.Kill(b);
            Assert.That(store.Count, Is.EqualTo(3), "a kill takes effect at the end of the tick");
            Assert.That(b.Killed, Is.True);
            store.EndTick();
            Assert.That(store.Count, Is.EqualTo(2));
            Assert.That(a.Initialised, Is.True);
            Assert.That(store.TryGet(2UL, out _), Is.False);
            List<Entity> retired = new List<Entity>();
            Assert.That(store.DrainRetired(retired), Is.EqualTo(1));
            Assert.That(retired[0], Is.SameAs(b));
            Assert.That(store.DrainRetired(retired), Is.EqualTo(0), "drained once");
            Entity d = store.Spawn(Cobble, new Double3(3, 0, 0), 0f, 2);
            Assert.That(d.Id.Value, Is.EqualTo(4UL), "2 is never used again");
            Assert.That(store.NextId, Is.EqualTo(5UL));
            Assert.That(store.All[0], Is.SameAs(a));
            Assert.That(store.All[1], Is.SameAs(c));
            Assert.That(store.All[2], Is.SameAs(d));
        }

        [Test]
        public void OnlyASpawnableDefinitionSpawns()
        {
            EntityStore store = new EntityStore();
            Assert.Throws<ArgumentException>(() => store.Spawn(DefinitionCatalogue.Player, default, 0f, 0));
            Assert.Throws<ArgumentException>(() => store.Spawn(DefinitionCatalogue.ByKey("plant/blackbutt"), default, 0f, 0));
        }

        [Test]
        public void ChangesAreStampedPerFieldSoAViewerReadsWhatItMissed()
        {
            EntityStore store = new EntityStore();
            Entity e = store.Spawn(Cobble, new Double3(0, 5, 0), 0f, 10);
            ItemComponent item;
            item.Resting = false;
            item.FallSpeed = 0f;
            e.SetItem(item, 10);
            Assert.That(e.ChangedSince(10), Is.EqualTo(EntityFields.All), "a spawn stamps every field");
            Assert.That(e.ChangedSince(11), Is.EqualTo(EntityFields.None));
            e.Move(new Double3(0, 4, 0), 12);
            Assert.That(e.ChangedSince(11), Is.EqualTo(EntityFields.Position));
            Assert.That(e.ChangedSince(12), Is.EqualTo(EntityFields.Position), "at the stamp counts as unseen");
            Assert.That(e.ChangedSince(13), Is.EqualTo(EntityFields.None));
            e.Turn(90f, 13);
            item.Resting = true;
            e.SetItem(item, 14);
            Assert.That(e.ChangedSince(13), Is.EqualTo(EntityFields.Yaw | EntityFields.Item));
            Assert.That(e.ChangedSince(0), Is.EqualTo(EntityFields.All));
            EntityRecord r = e.Record();
            Assert.That(r.Key, Is.EqualTo("item/cobble"));
            Assert.That(r.Item.Resting, Is.True);
            Assert.That(r.YawDeg, Is.EqualTo(90f));
        }

        [Test]
        public void WithinFindsByHorizontalDistanceInIdOrder()
        {
            EntityStore store = new EntityStore();
            store.Spawn(Cobble, new Double3(100, 0, 100), 0f, 0);
            store.Spawn(Cobble, new Double3(103, 50, 104), 0f, 0);
            store.Spawn(Cobble, new Double3(200, 0, 100), 0f, 0);
            List<Entity> near = new List<Entity>();
            store.Within(100, 100, 10.0, near);
            Assert.That(near.Count, Is.EqualTo(2));
            Assert.That(near[0].Id.Value, Is.EqualTo(1UL));
            Assert.That(near[1].Id.Value, Is.EqualTo(2UL), "height does not count");
        }

        [Test]
        public void RestoreKeepsIdsInOrderAndTheNextIdMovesPastThem()
        {
            EntityStore store = new EntityStore();
            store.Restore(7, Cobble, new Double3(0, 0, 0), 0f, 3);
            store.Restore(2, Cobble, new Double3(1, 0, 0), 0f, 1);
            Assert.That(store.All[0].Id.Value, Is.EqualTo(2UL));
            Assert.That(store.All[1].Id.Value, Is.EqualTo(7UL));
            Assert.That(store.NextId, Is.EqualTo(8UL));
            Assert.That(store.All[0].Initialised, Is.True, "a restored entity has lived before");
            Assert.Throws<ArgumentException>(() => store.Restore(7, Cobble, default, 0f, 0), "an id twice");
            Assert.Throws<ArgumentException>(() => store.SetNextId(7), "would reuse 7");
            store.SetNextId(20);
            Assert.That(store.Spawn(Cobble, default, 0f, 0).Id.Value, Is.EqualTo(20UL));
        }
    }
}
