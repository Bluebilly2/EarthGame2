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

        /// <summary>An item's component on the wire: its rest and fall, and since protocol 15 (FP.3) the fourteen bytes of the state a blow gave it.</summary>
        /// <summary>Rest, fall and the mask of a thing with nothing of its own (BF.1).</summary>
        private const int ItemBytes = 1 + 4 + 2;

        [Test]
        public void TheProtocolIsVersionSixteenAndItsKindsKeepTheirNumbers()
        {
            Assert.That(ProtocolInfo.Version, Is.EqualTo((ushort)19), "4 carried the entities, 5 the layer on each tile message, 6 the verbs, 7 the taking of what lies, 8 the far forest's layers, 9 an animal's pose and the interest radius, 10 a developer's settings and the clock on the pong, 11 the clock's scale on the pong, 12 the fleeing pose, 13 the founder's water and the drink, 14 the founder's core and the death, 15 the stone's state on an item, the knap and the answer's words, 16 the developer's switch, 17 a thing's own state as one record and the carrying with it, 18 work, 19 the world's changes");
            Assert.That((byte)MessageKind.EntitySpawn, Is.EqualTo((byte)14));
            Assert.That((byte)MessageKind.EntityState, Is.EqualTo((byte)15));
            Assert.That((byte)MessageKind.EntityGone, Is.EqualTo((byte)16));
            Assert.That((byte)MessageKind.DeveloperMode, Is.EqualTo((byte)24), "M1.E's switch takes the next number and keeps it");
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
            Assert.That(back.Item.HasOwnState, Is.False, "a cobble never struck has no state of its own");
            Assert.That(w.Written.Length, Is.EqualTo(1 + 8 + 4 + 8 + 24 + 4 + 1 + ItemBytes), "the spawn's bytes are what the budget counts");
        }

        /// <summary>The state a blow gave a stone rides in the item's component (protocol 15, FP.3), and one no stone could have is refused.</summary>
        [Test]
        public void AStruckStonesStateRidesInItsItemAndAnImpossibleOneIsRefused()
        {
            EntitySpawnMessage m = default;
            m.Id = 9;
            m.DefinitionId = DefinitionCatalogue.FlakeOf(StoneType.Silcrete).Id.Value;
            m.HasItem = true;
            m.Item = new ItemComponent { Resting = true, FallSpeed = 0f, MassKg = 0.0207f, Edge01 = 0.545f, PlatformDeg = 0f, FlakesTaken = 0 };
            PacketWriter w = new PacketWriter(64);
            m.Write(w);
            EntitySpawnMessage back = EntitySpawnMessage.Read(Reader(w));
            Assert.That(back.Item.MassKg, Is.EqualTo(0.0207f));
            Assert.That(back.Item.Edge01, Is.EqualTo(0.545f));
            Assert.That(back.Item.HasOwnState, Is.True);
            Assert.That(DefinitionCatalogue.ById(new DefinitionId(back.DefinitionId)), Is.SameAs(DefinitionCatalogue.FlakeOf(StoneType.Silcrete)));

            EntityStateMessage core = default;
            core.Id = 3;
            core.Fields = EntityFields.Item;
            core.Item = new ItemComponent { Resting = true, MassKg = 0.5793f, PlatformDeg = 80.2f, FlakesTaken = 2 };
            w.Reset();
            core.Write(w);
            Assert.That(w.Written.Length, Is.EqualTo(1 + 8 + 8 + 1 + 1 + 4 + 2 + 4 + 4 + 2), "rest, fall, the mask, then the mass, the platform and the flakes it has of its own (BF.1)");
            EntityStateMessage coreBack = EntityStateMessage.Read(Reader(w));
            Assert.That(coreBack.Item.PlatformDeg, Is.EqualTo(80.2f));
            Assert.That(coreBack.Item.FlakesTaken, Is.EqualTo((ushort)2));
            Assert.That(coreBack.Item.MassKg, Is.EqualTo(0.5793f));

            foreach ((float mass, float edge, float platform, string what) in new[]
            {
                (float.NaN, 0f, 0f, "a mass that is not a number"),
                (-0.1f, 0f, 0f, "a mass below nothing"),
                (0.1f, 1.5f, 0f, "an edge past one"),
                (0.1f, 0.5f, 200f, "a platform past a flat face"),
                (0.1f, 0.5f, float.PositiveInfinity, "an angle that is not a number"),
            })
            {
                w.Reset();
                w.WriteByte((byte)MessageKind.EntityState);
                w.WriteUInt64(3);
                w.WriteInt64(1);
                w.WriteByte((byte)EntityFields.Item);
                w.WriteBool(true);
                w.WriteSingle(0f);
                w.WriteUInt16((ushort)(ThingFields.Mass | ThingFields.Edge | ThingFields.Platform));
                w.WriteSingle(mass);
                w.WriteSingle(edge);
                w.WriteSingle(platform);
                Assert.Throws<ProtocolException>(() => EntityStateMessage.Read(Reader(w)), what);
            }
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
            Assert.That(w.Written.Length, Is.EqualTo(1 + 8 + 8 + 1 + 4 + ItemBytes), "no position on the wire");
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
