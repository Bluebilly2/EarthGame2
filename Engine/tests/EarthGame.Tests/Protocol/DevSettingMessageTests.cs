using EarthGame.Protocol;
using NUnit.Framework;

namespace EarthGame.Tests.Protocol
{
    /// <summary>
    /// A developer's setting goes out through the writer and comes back through the reader unchanged (M1.D), one with no
    /// name or no number is refused where it is read, and the table both sides read finds every setting by its name and
    /// holds a number to its range.
    /// </summary>
    public sealed class DevSettingMessageTests
    {
        private static PacketReader ReaderOver(PacketWriter w)
        {
            byte[] bytes = w.ToArray();
            PacketReader r = new PacketReader(bytes);
            r.ReadByte();
            return r;
        }

        [Test]
        public void ASettingRoundTrips()
        {
            DevSettingMessage m = new DevSettingMessage { Name = DevSettings.AnimalsStandUpM, Value = 321.5 };
            PacketWriter w = new PacketWriter();
            m.Write(w);
            Assert.That(MessageHeader.PeekKind(w.ToArray(), 0, w.Length), Is.EqualTo(MessageKind.DevSetting));
            PacketReader r = ReaderOver(w);
            DevSettingMessage back = DevSettingMessage.Read(r);
            r.ExpectEnd();
            Assert.That(back.Name, Is.EqualTo(DevSettings.AnimalsStandUpM));
            Assert.That(back.Value, Is.EqualTo(321.5));
        }

        [Test]
        public void ASettingWithNoNameOrNoNumberIsARefusal()
        {
            Assert.Throws<ProtocolException>(() => new DevSettingMessage { Name = "", Value = 1.0 }.Write(new PacketWriter()), "written with no name");
            PacketWriter w = new PacketWriter();
            w.WriteByte((byte)MessageKind.DevSetting);
            w.WriteString(DevSettings.ClockLocalHour);
            w.WriteDouble(double.NaN);
            Assert.Throws<ProtocolException>(() => DevSettingMessage.Read(ReaderOver(w)), "a number of NaN");
            w.Reset();
            w.WriteByte((byte)MessageKind.DevSetting);
            w.WriteString("");
            w.WriteDouble(1.0);
            Assert.Throws<ProtocolException>(() => DevSettingMessage.Read(ReaderOver(w)), "read with no name");
        }

        [Test]
        public void ThePongCarriesTheServersClockAndRefusesOneThatIsNotANumber()
        {
            PongMessage m;
            m.ClientTimeMs = 1234;
            m.ServerTick = 99;
            m.ServerTotalHours = 5675.25;
            PacketWriter w = new PacketWriter();
            m.Write(w);
            PacketReader r = ReaderOver(w);
            PongMessage back = PongMessage.Read(r);
            r.ExpectEnd();
            Assert.That(back.ServerTotalHours, Is.EqualTo(5675.25));
            w.Reset();
            w.WriteByte((byte)MessageKind.Pong);
            w.WriteInt64(1);
            w.WriteInt64(2);
            w.WriteDouble(double.PositiveInfinity);
            Assert.Throws<ProtocolException>(() => PongMessage.Read(ReaderOver(w)));
        }

        [Test]
        public void EverySettingInTheTableIsFoundByItsNameAndHeldToItsRange()
        {
            foreach (DevSetting s in DevSettings.All)
            {
                Assert.That(DevSettings.Find(s.Name), Is.SameAs(s), s.Name);
                if (s.IsDeed) continue;
                Assert.That(DevSettings.Held(s, s.Least - 1.0), Is.EqualTo(s.Least), s.Name + " below its least");
                Assert.That(DevSettings.Held(s, s.Most + 1.0), Is.EqualTo(s.Most), s.Name + " above its most");
                Assert.That(double.IsNaN(s.Initial) || (s.Initial >= s.Least && s.Initial <= s.Most), Is.True, s.Name + " starts inside its range");
            }
            Assert.That(DevSettings.Find("nothing.of.the.kind"), Is.Null);
        }
    }
}
