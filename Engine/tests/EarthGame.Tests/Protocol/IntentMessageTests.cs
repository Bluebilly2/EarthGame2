using EarthGame.Engine;
using EarthGame.Protocol;
using NUnit.Framework;

namespace EarthGame.Tests.Protocol
{
    /// <summary>The verbs' messages of protocol v6 (M1.5a promise 2) round-trip, and what this build does not know is refused.</summary>
    public sealed class IntentMessageTests
    {
        private static PacketReader Reader(PacketWriter w)
        {
            byte[] bytes = w.Written.ToArray();
            PacketReader r = new PacketReader(bytes);
            r.ReadByte();
            return r;
        }

        [Test]
        public void TheVerbsAndTheirKindsKeepTheirNumbers()
        {
            Assert.That((byte)MessageKind.Intent, Is.EqualTo((byte)17));
            Assert.That((byte)MessageKind.IntentResult, Is.EqualTo((byte)18));
            Assert.That((byte)MessageKind.Carrying, Is.EqualTo((byte)19));
            Assert.That(EntityGoneMessage.TakenUp, Is.EqualTo((byte)3));
            Assert.That((byte)Verb.PickUp, Is.EqualTo((byte)1));
            Assert.That((byte)Verb.PutDown, Is.EqualTo((byte)2));
            Assert.That((byte)Verb.Hold, Is.EqualTo((byte)3));
            Assert.That((byte)VerbOutcome.NotNow, Is.EqualTo((byte)6));
        }

        [Test]
        public void EachVerbCarriesItsTargetAndNothingElse()
        {
            PacketWriter w = new PacketWriter(64);
            IntentMessage pick = new IntentMessage { Sequence = 7, Verb = Verb.PickUp, Target = IntentMessage.TargetEntity, EntityId = 123456789012UL, East = 99 };
            pick.Write(w);
            Assert.That(w.Written.Length, Is.EqualTo(1 + 4 + 1 + 1 + 8));
            Assert.That(MessageHeader.PeekKind(w.Written.ToArray(), 0, w.Written.Length), Is.EqualTo(MessageKind.Intent));
            PacketReader r = Reader(w);
            IntentMessage back = IntentMessage.Read(r);
            r.ExpectEnd();
            Assert.That(back.Sequence, Is.EqualTo(7u));
            Assert.That(back.Verb, Is.EqualTo(Verb.PickUp));
            Assert.That(back.EntityId, Is.EqualTo(123456789012UL));
            Assert.That(back.East, Is.EqualTo(0.0), "a pick-up names no point");

            w.Reset();
            IntentMessage put = new IntentMessage { Sequence = 8, Verb = Verb.PutDown, East = -1352.25, Up = 3.5, North = 1904.125 };
            put.Write(w);
            Assert.That(w.Written.Length, Is.EqualTo(1 + 4 + 1 + 24));
            r = Reader(w);
            back = IntentMessage.Read(r);
            r.ExpectEnd();
            Assert.That(back.Verb, Is.EqualTo(Verb.PutDown));
            Assert.That(back.East, Is.EqualTo(-1352.25));
            Assert.That(back.Up, Is.EqualTo(3.5));
            Assert.That(back.North, Is.EqualTo(1904.125));

            w.Reset();
            IntentMessage hold = new IntentMessage { Sequence = uint.MaxValue, Verb = Verb.Hold, Place = 9 };
            hold.Write(w);
            Assert.That(w.Written.Length, Is.EqualTo(1 + 4 + 1 + 1));
            r = Reader(w);
            back = IntentMessage.Read(r);
            r.ExpectEnd();
            Assert.That(back.Sequence, Is.EqualTo(uint.MaxValue));
            Assert.That(back.Place, Is.EqualTo((byte)9));
        }

        [Test]
        public void AVerbOrATargetThisBuildDoesNotKnowIsARefusal()
        {
            PacketWriter w = new PacketWriter(32);
            Assert.Throws<ProtocolException>(() => new IntentMessage { Verb = Verb.None }.Write(w), "no layout for no verb");
            w.Reset();
            w.WriteByte((byte)MessageKind.Intent);
            w.WriteUInt32(1);
            w.WriteByte(4);
            Assert.Throws<ProtocolException>(() => IntentMessage.Read(Reader(w)), "verb 4");
            w.Reset();
            w.WriteByte((byte)MessageKind.Intent);
            w.WriteUInt32(1);
            w.WriteByte((byte)Verb.PickUp);
            w.WriteByte(3);
            w.WriteUInt64(1);
            Assert.Throws<ProtocolException>(() => IntentMessage.Read(Reader(w)), "a target of kind 3");
        }

        /// <summary>A put-down at a point that is not a number is refused by the reader (M1.5g): a NaN passes every comparison after it.</summary>
        [Test]
        public void APutDownPointThatIsNotANumberIsARefusal()
        {
            foreach ((double east, double up, double north, string what) in new[]
            {
                (double.NaN, 1.0, 2.0, "an east of NaN"),
                (0.0, double.NaN, 2.0, "a height of NaN"),
                (0.0, 1.0, double.PositiveInfinity, "a north of infinity"),
            })
            {
                PacketWriter w = new PacketWriter(64);
                w.WriteByte((byte)MessageKind.Intent);
                w.WriteUInt32(1);
                w.WriteByte((byte)Verb.PutDown);
                w.WriteDouble(east);
                w.WriteDouble(up);
                w.WriteDouble(north);
                Assert.Throws<ProtocolException>(() => IntentMessage.Read(Reader(w)), what);
            }
        }

        /// <summary>A pick-up of a thing lying names it by its place (protocol v7, M1.5b).</summary>
        [Test]
        public void APickUpNamesAThingLyingByItsPlace()
        {
            PacketWriter w = new PacketWriter(32);
            IntentMessage pick = new IntentMessage { Sequence = 3, Verb = Verb.PickUp, Target = IntentMessage.TargetLying, Lying = new LyingThing(1999, 42, StandLayout.Kind.Cobble, 14) };
            pick.Write(w);
            Assert.That(w.Written.Length, Is.EqualTo(1 + 4 + 1 + 1 + 2 + 2 + 1 + 1));
            PacketReader r = Reader(w);
            IntentMessage back = IntentMessage.Read(r);
            r.ExpectEnd();
            Assert.That(back.Target, Is.EqualTo(IntentMessage.TargetLying));
            Assert.That(back.Lying, Is.EqualTo(new LyingThing(1999, 42, StandLayout.Kind.Cobble, 14)));
            Assert.That(back.EntityId, Is.EqualTo(0UL), "a thing lying is no entity yet");

            w.Reset();
            w.WriteByte((byte)MessageKind.Intent);
            w.WriteUInt32(1);
            w.WriteByte((byte)Verb.PickUp);
            w.WriteByte(IntentMessage.TargetLying);
            w.WriteUInt16(1);
            w.WriteUInt16(1);
            w.WriteByte((byte)StandLayout.Kind.Trunk);
            w.WriteByte(0);
            Assert.Throws<ProtocolException>(() => IntentMessage.Read(Reader(w)), "a trunk is not a thing lying");
            Assert.Throws<ProtocolException>(() => new IntentMessage { Verb = Verb.PickUp }.Write(new PacketWriter(16)), "a pick-up names what it picks up");
        }

        /// <summary>The takings travel by the cell, and what no code counts is refused (protocol v7, M1.5b).</summary>
        [Test]
        public void TheTakingsTravelByCellAndWhatNoCodeCountsIsARefusal()
        {
            LooseTakenMessage m;
            m.Cells = new[]
            {
                new LooseTaken.Cell { Row = 1234, Col = 567, Sticks = 5, Cobbles = 0 },
                new LooseTaken.Cell { Row = 0, Col = 2000, Sticks = 0, Cobbles = 0x4000 },
            };
            PacketWriter w = new PacketWriter(64);
            m.Write(w);
            Assert.That(w.Written.Length, Is.EqualTo(1 + 2 + 2 * 8));
            Assert.That((byte)MessageKind.LooseTaken, Is.EqualTo((byte)20));
            PacketReader r = Reader(w);
            LooseTakenMessage back = LooseTakenMessage.Read(r);
            r.ExpectEnd();
            Assert.That(back.Cells.Length, Is.EqualTo(2));
            Assert.That(back.Cells[0].Row, Is.EqualTo(1234));
            Assert.That(back.Cells[0].Sticks, Is.EqualTo((ushort)5));
            Assert.That(back.Cells[1].Col, Is.EqualTo(2000));
            Assert.That(back.Cells[1].Cobbles, Is.EqualTo((ushort)0x4000));

            w.Reset();
            w.WriteByte((byte)MessageKind.LooseTaken);
            w.WriteUInt16(1);
            w.WriteUInt16(1);
            w.WriteUInt16(1);
            w.WriteUInt16(0x8000);
            w.WriteUInt16(0);
            Assert.Throws<ProtocolException>(() => LooseTakenMessage.Read(Reader(w)), "a sixteenth stick");
            w.Reset();
            w.WriteByte((byte)MessageKind.LooseTaken);
            w.WriteUInt16((ushort)(LooseTakenMessage.MaxCells + 1));
            Assert.Throws<ProtocolException>(() => LooseTakenMessage.Read(Reader(w)), "more cells than one message carries");
        }

        [Test]
        public void AnAnswerNamesItsIntentAndAnOutcomeThisBuildKnows()
        {
            PacketWriter w = new PacketWriter(16);
            IntentResultMessage m;
            m.Sequence = 41;
            m.Outcome = VerbOutcome.HandsFull;
            m.Write(w);
            Assert.That(w.Written.Length, Is.EqualTo(1 + 4 + 1));
            PacketReader r = Reader(w);
            IntentResultMessage back = IntentResultMessage.Read(r);
            r.ExpectEnd();
            Assert.That(back.Sequence, Is.EqualTo(41u));
            Assert.That(back.Outcome, Is.EqualTo(VerbOutcome.HandsFull));
            w.Reset();
            w.WriteByte((byte)MessageKind.IntentResult);
            w.WriteUInt32(41);
            // Past the last outcome this build knows (8, NoWater, since FP.1): refused as unknown.
            w.WriteByte(9);
            Assert.Throws<ProtocolException>(() => IntentResultMessage.Read(Reader(w)));
        }

        [Test]
        public void TheHandsTravelByPlaceWithoutTheSpawnTick()
        {
            CarryingMessage m;
            m.Hand = 4;
            m.Things = new[]
            {
                new CarriedThing { Id = 3, Definition = DefinitionCatalogue.Cobble, SpawnTick = 50, Place = 1 },
                new CarriedThing { Id = 11, Definition = DefinitionCatalogue.Stick, SpawnTick = 60, Place = 4 },
            };
            PacketWriter w = new PacketWriter(64);
            m.Write(w);
            Assert.That(w.Written.Length, Is.EqualTo(1 + 1 + 1 + 2 * (1 + 8 + 4)));
            PacketReader r = Reader(w);
            CarryingMessage back = CarryingMessage.Read(r);
            r.ExpectEnd();
            Assert.That(back.Hand, Is.EqualTo((byte)4));
            Assert.That(back.Things.Length, Is.EqualTo(2));
            Assert.That(back.Things[0].Definition, Is.SameAs(DefinitionCatalogue.Cobble));
            Assert.That(back.Things[1].Id, Is.EqualTo(11UL));
            Assert.That(back.Things[1].Place, Is.EqualTo((byte)4));
            Assert.That(back.Things[1].Definition, Is.SameAs(DefinitionCatalogue.Stick));
            Assert.That(back.Things[1].SpawnTick, Is.EqualTo(0L), "the spawn tick stays with the server");

            CarryingMessage empty = default;
            w.Reset();
            empty.Write(w);
            back = CarryingMessage.Read(Reader(w));
            Assert.That(back.Things.Length, Is.EqualTo(0));
            Assert.That(back.Hand, Is.EqualTo((byte)0));
        }

        [Test]
        public void MoreThanTheHandsHoldOrAThingThisBuildDoesNotKnowIsARefusal()
        {
            PacketWriter w = new PacketWriter(64);
            w.WriteByte((byte)MessageKind.Carrying);
            w.WriteByte(1);
            w.WriteByte((byte)(Hands.Places + 1));
            Assert.Throws<ProtocolException>(() => CarryingMessage.Read(Reader(w)), "one thing more than the places");

            w.Reset();
            w.WriteByte((byte)MessageKind.Carrying);
            w.WriteByte((byte)(Hands.Places + 1));
            w.WriteByte(0);
            Assert.Throws<ProtocolException>(() => CarryingMessage.Read(Reader(w)), "a hand beyond the last place");

            w.Reset();
            w.WriteByte((byte)MessageKind.Carrying);
            w.WriteByte(1);
            w.WriteByte(1);
            w.WriteByte(0);
            w.WriteUInt64(5);
            w.WriteUInt32(DefinitionCatalogue.Cobble.Id.Value);
            Assert.Throws<ProtocolException>(() => CarryingMessage.Read(Reader(w)), "place 0 is no place");

            w.Reset();
            w.WriteByte((byte)MessageKind.Carrying);
            w.WriteByte(1);
            w.WriteByte(1);
            w.WriteByte(1);
            w.WriteUInt64(5);
            w.WriteUInt32(0xFFFFFFF0u);
            Assert.Throws<ProtocolException>(() => CarryingMessage.Read(Reader(w)), "a definition this build does not know");
        }
    }
}
