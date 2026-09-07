using EarthGame.Protocol;
using NUnit.Framework;

namespace EarthGame.Tests.Protocol
{
    /// <summary>Every message type goes out through the writer and comes back through the reader unchanged.</summary>
    public sealed class MessageRoundTripTests
    {
        private static PacketReader ReaderOver(PacketWriter w)
        {
            byte[] bytes = w.ToArray();
            PacketReader r = new PacketReader(bytes);
            r.ReadByte(); // kind
            return r;
        }

        [Test]
        public void HelloRoundTrips()
        {
            HelloMessage m;
            m.ProtocolVersion = 1;
            m.PlayerName = "William";
            m.Password = "swamp-lake-cliff";
            PacketWriter w = new PacketWriter();
            m.Write(w);
            Assert.That(MessageHeader.PeekKind(w.ToArray(), 0, w.Length), Is.EqualTo(MessageKind.Hello));
            HelloMessage back = HelloMessage.Read(ReaderOver(w));
            Assert.That(back.ProtocolVersion, Is.EqualTo(1));
            Assert.That(back.PlayerName, Is.EqualTo("William"));
            Assert.That(back.Password, Is.EqualTo("swamp-lake-cliff"));
        }

        [Test]
        public void WelcomeRoundTripsWithUnicodeAndExtremes()
        {
            WelcomeMessage m;
            m.SessionId = uint.MaxValue;
            m.Seed = 0xDEADBEEFCAFEF00DUL;
            m.RegionId = "Bherwerre — 35.14°S 150.675°E";
            m.TotalHours = 5663.9583333333;
            m.Tick = long.MaxValue;
            m.TickRate = 20;
            m.SpawnEast = -2410.5;
            m.SpawnUp = 3.25;
            m.SpawnNorth = -2113.75;
            PacketWriter w = new PacketWriter();
            m.Write(w);
            WelcomeMessage back = WelcomeMessage.Read(ReaderOver(w));
            Assert.That(back.SpawnEast, Is.EqualTo(-2410.5));
            Assert.That(back.SpawnUp, Is.EqualTo(3.25));
            Assert.That(back.SpawnNorth, Is.EqualTo(-2113.75));
            Assert.That(back.SessionId, Is.EqualTo(uint.MaxValue));
            Assert.That(back.Seed, Is.EqualTo(0xDEADBEEFCAFEF00DUL));
            Assert.That(back.RegionId, Is.EqualTo(m.RegionId));
            Assert.That(back.TotalHours, Is.EqualTo(m.TotalHours));
            Assert.That(back.Tick, Is.EqualTo(long.MaxValue));
            Assert.That(back.TickRate, Is.EqualTo(20));
        }

        [Test]
        public void RefusedPingPongRoundTrip()
        {
            PacketWriter w = new PacketWriter();
            RefusedMessage r; r.Reason = "server is full (8 players)"; r.Write(w);
            Assert.That(RefusedMessage.Read(ReaderOver(w)).Reason, Is.EqualTo("server is full (8 players)"));

            w.Reset();
            PingMessage p; p.ClientTimeMs = 123456789012; p.Write(w);
            Assert.That(PingMessage.Read(ReaderOver(w)).ClientTimeMs, Is.EqualTo(123456789012));

            w.Reset();
            PongMessage q; q.ClientTimeMs = -5; q.ServerTick = 42; q.Write(w);
            PongMessage back = PongMessage.Read(ReaderOver(w));
            Assert.That(back.ClientTimeMs, Is.EqualTo(-5));
            Assert.That(back.ServerTick, Is.EqualTo(42));
        }

        [Test]
        public void NullStringsTravelAsEmpty()
        {
            HelloMessage m;
            m.ProtocolVersion = 1;
            m.PlayerName = null;
            m.Password = null;
            PacketWriter w = new PacketWriter();
            m.Write(w);
            HelloMessage back = HelloMessage.Read(ReaderOver(w));
            Assert.That(back.PlayerName, Is.EqualTo(string.Empty));
            Assert.That(back.Password, Is.EqualTo(string.Empty));
        }

        [Test]
        public void TruncatedMessageIsAProtocolExceptionNotACrash()
        {
            WelcomeMessage m;
            m.SessionId = 1; m.Seed = 2; m.RegionId = "bherwerre"; m.TotalHours = 3; m.Tick = 4; m.TickRate = 20;
            m.SpawnEast = 0; m.SpawnUp = 0; m.SpawnNorth = 0;
            PacketWriter w = new PacketWriter();
            m.Write(w);
            byte[] bytes = w.ToArray();
            PacketReader r = new PacketReader(bytes, 0, bytes.Length - 3);
            r.ReadByte();
            Assert.Throws<ProtocolException>(() => WelcomeMessage.Read(r));
        }

        [Test]
        public void TrailingBytesAreALayoutMismatch()
        {
            PacketWriter w = new PacketWriter();
            PingMessage p; p.ClientTimeMs = 1; p.Write(w);
            w.WriteByte(0xFF);
            PacketReader r = ReaderOver(w);
            PingMessage.Read(r);
            Assert.Throws<ProtocolException>(() => r.ExpectEnd());
        }

        [Test]
        public void OversizedStringIsRefusedByTheWriter()
        {
            PacketWriter w = new PacketWriter();
            string huge = new string('x', ProtocolInfo.MaxStringBytes + 1);
            Assert.Throws<ProtocolException>(() => w.WriteString(huge));
        }

        [Test]
        public void WriterGrowsAndResets()
        {
            PacketWriter w = new PacketWriter(16);
            for (int i = 0; i < 1000; i++) w.WriteInt32(i);
            Assert.That(w.Length, Is.EqualTo(4000));
            PacketReader r = new PacketReader(w.ToArray());
            for (int i = 0; i < 1000; i++) Assert.That(r.ReadInt32(), Is.EqualTo(i));
            r.ExpectEnd();
            w.Reset();
            Assert.That(w.Length, Is.EqualTo(0));
        }
    }
}
