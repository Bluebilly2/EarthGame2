using EarthGame.Engine;
using EarthGame.Protocol;
using NUnit.Framework;

namespace EarthGame.Tests.Protocol
{
    /// <summary>The founder's state on the wire (FP.1, protocol v13): one number, a fraction of a body, and nothing else is taken.</summary>
    public sealed class FounderStateMessageTests
    {
        private static FounderStateMessage RoundTrip(double water01, double coreC = 37.0)
        {
            FounderStateMessage m;
            m.Water01 = water01;
            m.CoreC = coreC;
            PacketWriter w = new PacketWriter(16);
            m.Write(w);
            PacketReader r = new PacketReader(w.Written.ToArray(), 0, w.Written.Length);
            Assert.That((MessageKind)r.ReadByte(), Is.EqualTo(MessageKind.FounderState));
            FounderStateMessage back = FounderStateMessage.Read(r);
            r.ExpectEnd();
            return back;
        }

        [Test]
        public void TheWaterRoundTripsToTheDouble()
        {
            Assert.That(RoundTrip(1.0).Water01, Is.EqualTo(1.0));
            Assert.That(RoundTrip(0.9285714285714286).Water01, Is.EqualTo(0.9285714285714286));
            Assert.That(RoundTrip(0.0).Water01, Is.EqualTo(0.0));
            Assert.That(RoundTrip(1.0, 34.25).CoreC, Is.EqualTo(34.25), "and the core since protocol 14");
        }

        [TestCase(double.NaN)]
        [TestCase(19.9)]
        [TestCase(41.1)]
        public void ACoreNoBodyHasIsRefusedByTheReader(double coreC)
        {
            PacketWriter w = new PacketWriter(24);
            w.WriteByte((byte)MessageKind.FounderState);
            w.WriteDouble(1.0);
            w.WriteDouble(coreC);
            PacketReader r = new PacketReader(w.Written.ToArray(), 1, w.Written.Length - 1);
            Assert.Throws<ProtocolException>(() => FounderStateMessage.Read(r));
        }

        [Test]
        public void TheKindIsTwentyTwo()
        {
            Assert.That((byte)MessageKind.FounderState, Is.EqualTo((byte)22));
            Assert.That((byte)MessageKind.Died, Is.EqualTo((byte)23));
            Assert.That((byte)Verb.Drink, Is.EqualTo((byte)4), "the fourth verb, wire-visible and never renumbered");
            Assert.That((byte)VerbOutcome.Salt, Is.EqualTo((byte)7));
            Assert.That((byte)VerbOutcome.NoWater, Is.EqualTo((byte)8));
        }

        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(1.0000001)]
        [TestCase(-0.0001)]
        public void WhatIsNoFractionOfABodyIsRefusedByTheReader(double water01)
        {
            PacketWriter w = new PacketWriter(24);
            w.WriteByte((byte)MessageKind.FounderState);
            w.WriteDouble(water01);
            w.WriteDouble(37.0);
            PacketReader r = new PacketReader(w.Written.ToArray(), 1, w.Written.Length - 1);
            Assert.Throws<ProtocolException>(() => FounderStateMessage.Read(r));
        }

        [Test]
        public void ADrinkIsLaidOutAsAPutDownIsAndRefusesANaNPoint()
        {
            IntentMessage m = default;
            m.Sequence = 9;
            m.Verb = Verb.Drink;
            m.East = -1392.5;
            m.Up = 2.0;
            m.North = 2804.25;
            PacketWriter w = new PacketWriter(64);
            m.Write(w);
            PacketReader r = new PacketReader(w.Written.ToArray(), 1, w.Written.Length - 1);
            IntentMessage back = IntentMessage.Read(r);
            Assert.That(back.Verb, Is.EqualTo(Verb.Drink));
            Assert.That(back.Sequence, Is.EqualTo(9u));
            Assert.That(back.East, Is.EqualTo(-1392.5));
            Assert.That(back.Up, Is.EqualTo(2.0));
            Assert.That(back.North, Is.EqualTo(2804.25));

            PacketWriter bad = new PacketWriter(64);
            bad.WriteByte((byte)MessageKind.Intent);
            bad.WriteUInt32(9);
            bad.WriteByte((byte)Verb.Drink);
            bad.WriteDouble(double.NaN);
            bad.WriteDouble(2.0);
            bad.WriteDouble(2804.25);
            PacketReader rb = new PacketReader(bad.Written.ToArray(), 1, bad.Written.Length - 1);
            Assert.Throws<ProtocolException>(() => IntentMessage.Read(rb));
        }
    }
}
