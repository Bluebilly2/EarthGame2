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
        public void TheProtocolIsEighteen()
        {
            Assert.That(ProtocolInfo.Version, Is.EqualTo((ushort)19), "BF.2's work was protocol 18; BF.3's changes are 19");
            Assert.That((byte)MessageKind.WorkState, Is.EqualTo((byte)25));
            Assert.That((byte)Verb.Work, Is.EqualTo((byte)6));
            Assert.That((byte)Verb.StopWork, Is.EqualTo((byte)7));
            Assert.That((byte)VerbOutcome.NoTool, Is.EqualTo((byte)15));
            Assert.That((byte)VerbOutcome.WontWork, Is.EqualTo((byte)16));
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
