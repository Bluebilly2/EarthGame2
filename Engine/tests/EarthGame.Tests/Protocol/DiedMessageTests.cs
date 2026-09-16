using EarthGame.Engine;
using EarthGame.Protocol;
using NUnit.Framework;

namespace EarthGame.Tests.Protocol
{
    /// <summary>A death on the wire (FP.2, protocol v14): the numbers the sentence is made of, and nothing that is no death is taken.</summary>
    public sealed class DiedMessageTests
    {
        private static Death Sample() => new Death(CauseOfDeath.Cold, 3.15, 8.6, 2.34, 297.0, 80.0, 27.96, 0.06, -1392.0, 2804.0);

        [Test]
        public void TheNumbersRoundTripAndTheSentenceIsTheSameOnBothEnds()
        {
            DiedMessage m;
            m.Death = Sample();
            PacketWriter w = new PacketWriter(96);
            m.Write(w);
            PacketReader r = new PacketReader(w.Written.ToArray(), 0, w.Written.Length);
            Assert.That((MessageKind)r.ReadByte(), Is.EqualTo(MessageKind.Died));
            DiedMessage back = DiedMessage.Read(r);
            r.ExpectEnd();
            Assert.That(back.Death.Cause, Is.EqualTo(CauseOfDeath.Cold));
            Assert.That(back.Death.LocalHour, Is.EqualTo(3.15));
            Assert.That(back.Death.AirC, Is.EqualTo(8.6));
            Assert.That(back.Death.WindMs, Is.EqualTo(2.34));
            Assert.That(back.Death.LossW, Is.EqualTo(297.0));
            Assert.That(back.Death.ProductionW, Is.EqualTo(80.0));
            Assert.That(back.Death.CoreC, Is.EqualTo(27.96));
            Assert.That(back.Death.WaterLoss, Is.EqualTo(0.06));
            Assert.That(back.Death.East, Is.EqualTo(-1392.0));
            Assert.That(back.Death.North, Is.EqualTo(2804.0));
            Assert.That(back.Death.Explain(), Is.EqualTo(Sample().Explain()), "the one sentence, from the same numbers");
        }

        [Test]
        public void ACauseThisBuildDoesNotKnowIsRefused()
        {
            PacketWriter w = new PacketWriter(96);
            w.WriteByte((byte)MessageKind.Died);
            w.WriteByte(9);
            for (int i = 0; i < 9; i++) w.WriteDouble(1.0);
            PacketReader r = new PacketReader(w.Written.ToArray(), 1, w.Written.Length - 1);
            Assert.Throws<ProtocolException>(() => DiedMessage.Read(r));
        }

        [TestCase(1, double.NaN, 1.0)]
        [TestCase(1, 24.0, 1.0)]
        [TestCase(2, -0.5, 1.0)]
        [TestCase(2, 3.0, double.PositiveInfinity)]
        public void AnHourNoDayHasOrANumberThatIsNotOneIsRefused(int cause, double hour, double air)
        {
            PacketWriter w = new PacketWriter(96);
            w.WriteByte((byte)MessageKind.Died);
            w.WriteByte((byte)cause);
            w.WriteDouble(hour);
            w.WriteDouble(air);
            for (int i = 0; i < 7; i++) w.WriteDouble(1.0);
            PacketReader r = new PacketReader(w.Written.ToArray(), 1, w.Written.Length - 1);
            Assert.Throws<ProtocolException>(() => DiedMessage.Read(r));
        }
    }
}
