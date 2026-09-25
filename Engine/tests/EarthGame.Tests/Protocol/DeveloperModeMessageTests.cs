using EarthGame.Protocol;
using NUnit.Framework;

namespace EarthGame.Tests.Protocol
{
    /// <summary>The developer's switch goes out and comes back unchanged (M1.E promise 2, protocol v16).</summary>
    public sealed class DeveloperModeMessageTests
    {
        [Test]
        public void TheSwitchRoundTripsEveryWay()
        {
            foreach (bool on in new[] { false, true })
                foreach (bool refused in new[] { false, true })
                {
                    DeveloperModeMessage m = new DeveloperModeMessage { On = on, Refused = refused };
                    PacketWriter w = new PacketWriter();
                    m.Write(w);
                    Assert.That(MessageHeader.PeekKind(w.ToArray(), 0, w.Length), Is.EqualTo(MessageKind.DeveloperMode));
                    PacketReader r = new PacketReader(w.ToArray());
                    r.ReadByte();
                    DeveloperModeMessage back = DeveloperModeMessage.Read(r);
                    r.ExpectEnd();
                    Assert.That(back.On, Is.EqualTo(on));
                    Assert.That(back.Refused, Is.EqualTo(refused));
                }
        }

        [Test]
        public void AByteThatIsNeitherIsARefusal()
        {
            PacketWriter w = new PacketWriter();
            w.WriteByte((byte)MessageKind.DeveloperMode);
            w.WriteByte(7);
            PacketReader r = new PacketReader(w.ToArray());
            r.ReadByte();
            Assert.Throws<ProtocolException>(() => DeveloperModeMessage.Read(r), "a flag byte beyond the two it carries");
        }

        [Test]
        public void TheProtocolSaysItsVersion()
        {
            Assert.That(ProtocolInfo.Version, Is.EqualTo(21), "BF.3's changes are protocol 19, its standing targets 20, WG.2c's two-byte stand 21");
        }
    }
}
