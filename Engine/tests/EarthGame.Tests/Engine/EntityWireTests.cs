using EarthGame.Engine;
using EarthGame.Protocol;
using NUnit.Framework;

namespace EarthGame.Tests.Engine
{
    /// <summary>The entity messages of protocol v4 (M1.3 promise 5) round-trip through the packet writer and reader.</summary>
    public sealed class EntityWireTests
    {
        private static PacketReader Reader(PacketWriter w)
        {
            byte[] bytes = w.Written.ToArray();
            PacketReader r = new PacketReader(bytes);
            r.ReadByte();
            return r;
        }

        [Test]
        public void TheProtocolIsVersionElevenAndItsKindsKeepTheirNumbers()
        {
            Assert.That(ProtocolInfo.Version, Is.EqualTo((ushort)11), "4 carried the entities, 5 the layer on each tile message, 6 the verbs, 7 the taking of what lies, 8 the far forest's layers, 9 an animal's pose and the interest radius, 10 a developer's settings and the clock on the pong, 11 the clock's scale on the pong");
            Assert.That((byte)MessageKind.EntitySpawn, Is.EqualTo((byte)14));
            Assert.That((byte)MessageKind.EntityState, Is.EqualTo((byte)15));
            Assert.That((byte)MessageKind.EntityGone, Is.EqualTo((byte)16));
        }

        [Test]
        public void ASpawnCarriesTheEntityInFull()
        {
            EntitySpawnMessage m;
            m.Id = 42;
            m.DefinitionId = DefinitionCatalogue.Cobble.Id.Value;
            m.ServerTick = 1000;
            m.East = -1352.25;
            m.Up = 3.5;
            m.North = 1904.125;
            m.YawDeg = 33.5f;
            m.HasItem = true;
            m.Item = new ItemComponent { Resting = false, FallSpeed = 2.5f };
            m.HasAnimal = false;
            m.Animal = default;
            PacketWriter w = new PacketWriter(64);
            m.Write(w);
            Assert.That(MessageHeader.PeekKind(w.Written.ToArray(), 0, w.Written.Length), Is.EqualTo(MessageKind.EntitySpawn));
            PacketReader r = Reader(w);
            EntitySpawnMessage back = EntitySpawnMessage.Read(r);
            r.ExpectEnd();
            Assert.That(back.Id, Is.EqualTo(42UL));
            Assert.That(DefinitionCatalogue.ById(new DefinitionId(back.DefinitionId)), Is.SameAs(DefinitionCatalogue.Cobble));
            Assert.That(back.ServerTick, Is.EqualTo(1000L));
            Assert.That(back.East, Is.EqualTo(-1352.25));
            Assert.That(back.Up, Is.EqualTo(3.5));
            Assert.That(back.North, Is.EqualTo(1904.125));
            Assert.That(back.YawDeg, Is.EqualTo(33.5f));
            Assert.That(back.HasItem, Is.True);
            Assert.That(back.Item.Resting, Is.False);
            Assert.That(back.Item.FallSpeed, Is.EqualTo(2.5f));
            Assert.That(w.Written.Length, Is.EqualTo(1 + 8 + 4 + 8 + 24 + 4 + 1 + 5), "the spawn's bytes are what the budget counts");
        }

        [Test]
        public void AStateCarriesOnlyTheFieldsItNames()
        {
            EntityStateMessage m = default;
            m.Id = 7;
            m.ServerTick = 55;
            m.Fields = EntityFields.Item | EntityFields.Yaw;
            m.YawDeg = 12f;
            m.Item = new ItemComponent { Resting = true, FallSpeed = 0f };
            m.East = 999;
            PacketWriter w = new PacketWriter(64);
            m.Write(w);
            Assert.That(w.Written.Length, Is.EqualTo(1 + 8 + 8 + 1 + 4 + 5), "no position on the wire");
            PacketReader r = Reader(w);
            EntityStateMessage back = EntityStateMessage.Read(r);
            r.ExpectEnd();
            Assert.That(back.Fields, Is.EqualTo(EntityFields.Item | EntityFields.Yaw));
            Assert.That(back.YawDeg, Is.EqualTo(12f));
            Assert.That(back.Item.Resting, Is.True);
            Assert.That(back.East, Is.EqualTo(0.0), "a field not named reads as nothing");

            EntityStateMessage all = default;
            all.Id = 7;
            all.Fields = EntityFields.All;
            all.East = 1;
            all.Up = 2;
            all.North = 3;
            w.Reset();
            all.Write(w);
            EntityStateMessage backAll = EntityStateMessage.Read(Reader(w));
            Assert.That(backAll.North, Is.EqualTo(3.0));

            w.Reset();
            w.WriteByte((byte)MessageKind.EntityState);
            w.WriteUInt64(7);
            w.WriteInt64(1);
            w.WriteByte(0x80);
            Assert.Throws<ProtocolException>(() => EntityStateMessage.Read(Reader(w)), "a field this build does not know is a refusal");
        }

        [Test]
        public void AnAnimalsSpawnCarriesItsPoseAndAStateItsChangedPose()
        {
            EntitySpawnMessage m = default;
            m.Id = EntityId.TransientBit | 77UL;
            m.DefinitionId = DefinitionCatalogue.ByKey("animal/eastern-grey-kangaroo").Id.Value;
            m.ServerTick = 400;
            m.East = 12.5;
            m.YawDeg = 270f;
            m.HasAnimal = true;
            m.Animal = new AnimalComponent { Pose = AnimalPose.Grazing };
            PacketWriter w = new PacketWriter(64);
            m.Write(w);
            Assert.That(w.Written.Length, Is.EqualTo(1 + 8 + 4 + 8 + 24 + 4 + 1 + 1), "an animal's spawn is an entity's and a byte of pose");
            PacketReader r = Reader(w);
            EntitySpawnMessage back = EntitySpawnMessage.Read(r);
            r.ExpectEnd();
            Assert.That(back.HasItem, Is.False);
            Assert.That(back.HasAnimal, Is.True);
            Assert.That(back.Animal.Pose, Is.EqualTo(AnimalPose.Grazing));
            Assert.That(back.Id, Is.EqualTo(EntityId.TransientBit | 77UL));

            EntityStateMessage s = default;
            s.Id = back.Id;
            s.ServerTick = 420;
            s.Fields = EntityFields.Pose;
            s.Animal = new AnimalComponent { Pose = AnimalPose.Resting };
            w.Reset();
            s.Write(w);
            Assert.That(w.Written.Length, Is.EqualTo(1 + 8 + 8 + 1 + 1), "a changed pose alone is a byte");
            PacketReader sr = Reader(w);
            EntityStateMessage sback = EntityStateMessage.Read(sr);
            sr.ExpectEnd();
            Assert.That(sback.Fields, Is.EqualTo(EntityFields.Pose));
            Assert.That(sback.Animal.Pose, Is.EqualTo(AnimalPose.Resting));

            w.Reset();
            w.WriteByte((byte)MessageKind.EntitySpawn);
            w.WriteUInt64(1);
            w.WriteUInt32(DefinitionCatalogue.Cobble.Id.Value);
            w.WriteInt64(0);
            w.WriteDouble(0); w.WriteDouble(0); w.WriteDouble(0);
            w.WriteSingle(0f);
            w.WriteByte(0x80);
            Assert.Throws<ProtocolException>(() => EntitySpawnMessage.Read(Reader(w)), "a component this build does not know is a refusal, not bytes read as something else");
        }

        [Test]
        public void AGoneNamesTheEntityAndWhy()
        {
            EntityGoneMessage m;
            m.Id = 9;
            m.Reason = EntityGoneMessage.Left;
            PacketWriter w = new PacketWriter(16);
            m.Write(w);
            PacketReader r = Reader(w);
            EntityGoneMessage back = EntityGoneMessage.Read(r);
            r.ExpectEnd();
            Assert.That(back.Id, Is.EqualTo(9UL));
            Assert.That(back.Reason, Is.EqualTo(EntityGoneMessage.Left));
        }
    }
}
