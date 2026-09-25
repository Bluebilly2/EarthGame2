using EarthGame.Engine;
using EarthGame.Protocol;
using NUnit.Framework;

namespace EarthGame.Tests.Protocol
{
    /// <summary>The work's wire (BF.2 promise 3, protocol 18): a start names its kind and its target, a stop names nothing, the answer carries its seconds, and the work's state travels once a second.</summary>
    public sealed class WorkWireTests
    {
        private static PacketReader Reader(PacketWriter w) => new PacketReader(w.Written.ToArray(), 1, w.Written.Length - 1);

        [Test]
        public void TheProtocolIsTwentyOne()
        {
            Assert.That(ProtocolInfo.Version, Is.EqualTo((ushort)21), "BF.2's work was protocol 18, BF.3's changes 19, its standing targets 20 and WG.2c's two-byte stand 21");
            Assert.That((byte)MessageKind.WorkState, Is.EqualTo((byte)25));
            Assert.That((byte)Verb.Work, Is.EqualTo((byte)6));
            Assert.That((byte)Verb.StopWork, Is.EqualTo((byte)7));
            Assert.That((byte)VerbOutcome.NoTool, Is.EqualTo((byte)15));
            Assert.That((byte)VerbOutcome.WontWork, Is.EqualTo((byte)16));
            Assert.That((byte)VerbOutcome.TooHeavy, Is.EqualTo((byte)17));
            Assert.That((byte)WorkKind.StripTrunk, Is.EqualTo((byte)5));
            Assert.That((byte)WorkKind.CutTrunk, Is.EqualTo((byte)10));
            Assert.That(IntentMessage.TargetTrunk, Is.EqualTo((byte)4));
            Assert.That(IntentMessage.TargetTuft, Is.EqualTo((byte)5));
            Assert.That(IntentMessage.TargetGround, Is.EqualTo((byte)6));
        }

        [Test]
        public void AWorkNamesATrunkATuftOrACellOfTheGroundAndAPickUpOrAKnapMayNot()
        {
            PacketWriter w = new PacketWriter(64);
            new IntentMessage { Sequence = 20, Verb = Verb.Work, Kind = WorkKind.StripTrunk, Target = IntentMessage.TargetTrunk, Row = 1200, Col = 977 }.Write(w);
            Assert.That(w.Written.Length, Is.EqualTo(1 + 4 + 1 + 1 + 2 + 2 + 1), "kind, sequence, verb, target, row, col, work kind");
            IntentMessage trunk = IntentMessage.Read(Reader(w));
            Assert.That(trunk.Target, Is.EqualTo(IntentMessage.TargetTrunk));
            Assert.That(trunk.Row, Is.EqualTo(1200));
            Assert.That(trunk.Col, Is.EqualTo(977));
            Assert.That(trunk.Kind, Is.EqualTo(WorkKind.StripTrunk));

            w.Reset();
            new IntentMessage { Sequence = 21, Verb = Verb.Work, Kind = WorkKind.CutFibre, Target = IntentMessage.TargetTuft, Row = 3, Col = 4, Index = 11 }.Write(w);
            Assert.That(w.Written.Length, Is.EqualTo(1 + 4 + 1 + 1 + 2 + 2 + 1 + 1), "and the tuft's index");
            IntentMessage tuft = IntentMessage.Read(Reader(w));
            Assert.That(tuft.Target, Is.EqualTo(IntentMessage.TargetTuft));
            Assert.That(tuft.Index, Is.EqualTo(11));
            Assert.That(tuft.Kind, Is.EqualTo(WorkKind.CutFibre));

            w.Reset();
            new IntentMessage { Sequence = 22, Verb = Verb.Work, Kind = WorkKind.Dig, Target = IntentMessage.TargetGround, Row = 5, Col = 6 }.Write(w);
            IntentMessage ground = IntentMessage.Read(Reader(w));
            Assert.That(ground.Target, Is.EqualTo(IntentMessage.TargetGround));
            Assert.That(ground.Row, Is.EqualTo(5));
            Assert.That(ground.Kind, Is.EqualTo(WorkKind.Dig));

            w.Reset();
            Assert.Throws<ProtocolException>(() => new IntentMessage { Sequence = 23, Verb = Verb.PickUp, Target = IntentMessage.TargetTrunk, Row = 1, Col = 1 }.Write(w), "a pick-up names no trunk");
            w.Reset();
            Assert.Throws<ProtocolException>(() => new IntentMessage { Sequence = 24, Verb = Verb.Knap, Target = IntentMessage.TargetTuft, Row = 1, Col = 1 }.Write(w), "a knap names no tuft");
            w.Reset();
            w.WriteByte((byte)MessageKind.Intent);
            w.WriteUInt32(25);
            w.WriteByte((byte)Verb.PickUp);
            w.WriteByte(IntentMessage.TargetGround);
            w.WriteUInt16(1);
            w.WriteUInt16(1);
            Assert.Throws<ProtocolException>(() => IntentMessage.Read(Reader(w)), "nor does a pick-up read one");

            w.Reset();
            new IntentResultMessage { Sequence = 26, Outcome = VerbOutcome.TooHeavy, Note = "a log" }.Write(w);
            Assert.That(IntentResultMessage.Read(Reader(w)).Outcome, Is.EqualTo(VerbOutcome.TooHeavy), "the heavy outcome is known");
            w.Reset();
            new WorkStateMessage { Kind = WorkKind.CutTrunk, Progress01 = 0.25f, SecondsLeft = 900f, Ended = WorkStateMessage.Running, Note = "cutting" }.Write(w);
            Assert.That(WorkStateMessage.Read(Reader(w)).Kind, Is.EqualTo(WorkKind.CutTrunk), "the new kinds travel in the state");
        }

        [Test]
        public void AStartNamesItsKindAndItsTargetOfAnyKindAndAStopNamesNothing()
        {
            IntentMessage m = new IntentMessage { Sequence = 9, Verb = Verb.Work, Kind = WorkKind.Point, Target = IntentMessage.TargetPlace, Place = 3 };
            PacketWriter w = new PacketWriter(64);
            m.Write(w);
            Assert.That(w.Written.Length, Is.EqualTo(1 + 4 + 1 + 1 + 1 + 1), "kind, sequence, verb, target, place, work kind");
            IntentMessage back = IntentMessage.Read(Reader(w));
            Assert.That(back.Verb, Is.EqualTo(Verb.Work));
            Assert.That(back.Kind, Is.EqualTo(WorkKind.Point));
            Assert.That(back.Target, Is.EqualTo(IntentMessage.TargetPlace));
            Assert.That(back.Place, Is.EqualTo((byte)3));

            IntentMessage lying = new IntentMessage { Sequence = 10, Verb = Verb.Work, Kind = WorkKind.Strip, Target = IntentMessage.TargetLying, Lying = new LyingThing(4, 5, StandLayout.Kind.Stick, 1) };
            w.Reset();
            lying.Write(w);
            IntentMessage lyingBack = IntentMessage.Read(Reader(w));
            Assert.That(lyingBack.Lying, Is.EqualTo(new LyingThing(4, 5, StandLayout.Kind.Stick, 1)));
            Assert.That(lyingBack.Kind, Is.EqualTo(WorkKind.Strip));

            w.Reset();
            new IntentMessage { Sequence = 11, Verb = Verb.StopWork }.Write(w);
            Assert.That(w.Written.Length, Is.EqualTo(1 + 4 + 1));
            Assert.That(IntentMessage.Read(Reader(w)).Verb, Is.EqualTo(Verb.StopWork));

            w.Reset();
            w.WriteByte((byte)MessageKind.Intent);
            w.WriteUInt32(12);
            w.WriteByte((byte)Verb.Work);
            w.WriteByte(IntentMessage.TargetPlace);
            w.WriteByte(2);
            w.WriteByte(99);
            Assert.Throws<ProtocolException>(() => IntentMessage.Read(Reader(w)), "a kind of work this build does not know");
        }

        [Test]
        public void TheAnswerCarriesTheWorksSecondsAndTheStateTravelsWithItsWords()
        {
            IntentResultMessage a = new IntentResultMessage { Sequence = 5, Outcome = VerbOutcome.Done, Note = "strip the bark", Seconds = 7.2f };
            PacketWriter w = new PacketWriter(64);
            a.Write(w);
            IntentResultMessage back = IntentResultMessage.Read(Reader(w));
            Assert.That(back.Seconds, Is.EqualTo(7.2f));
            Assert.That(back.Note, Is.EqualTo("strip the bark"));
            Assert.That(back.Outcome, Is.EqualTo(VerbOutcome.Done));
            w.Reset();
            new IntentResultMessage { Sequence = 6, Outcome = VerbOutcome.WontWork, Note = "too thick to break over the knee" }.Write(w);
            Assert.That(IntentResultMessage.Read(Reader(w)).Outcome, Is.EqualTo(VerbOutcome.WontWork), "the new outcomes are known");

            WorkStateMessage s = new WorkStateMessage { Kind = WorkKind.Twist, Progress01 = 0.5f, SecondsLeft = 7.5f, Ended = WorkStateMessage.Running, Note = "laying cord" };
            w.Reset();
            s.Write(w);
            Assert.That(MessageHeader.PeekKind(w.Written.ToArray(), 0, w.Written.Length), Is.EqualTo(MessageKind.WorkState));
            WorkStateMessage sBack = WorkStateMessage.Read(Reader(w));
            Assert.That(sBack.Kind, Is.EqualTo(WorkKind.Twist));
            Assert.That(sBack.Progress01, Is.EqualTo(0.5f).Within(1.0 / 255.0), "a byte of progress");
            Assert.That(sBack.SecondsLeft, Is.EqualTo(7.5f));
            Assert.That(sBack.Ended, Is.EqualTo(WorkStateMessage.Running));
            Assert.That(sBack.Note, Is.EqualTo("laying cord"));
            w.Reset();
            new WorkStateMessage { Kind = WorkKind.Break, Progress01 = 1f, SecondsLeft = 0f, Ended = WorkStateMessage.Done, Note = "the stick broke in two" }.Write(w);
            Assert.That(WorkStateMessage.Read(Reader(w)).Ended, Is.EqualTo(WorkStateMessage.Done));
            w.Reset();
            w.WriteByte((byte)MessageKind.WorkState);
            w.WriteByte((byte)WorkKind.Break);
            w.WriteByte(255);
            w.WriteSingle(0f);
            w.WriteByte(7);
            w.WriteString("");
            Assert.Throws<ProtocolException>(() => WorkStateMessage.Read(Reader(w)), "an ending this build does not know");
        }
    }
}
