using System.Diagnostics;
using System.Threading;
using EarthGame.ClientCore;
using EarthGame.Engine;
using EarthGame.Protocol;
using EarthGame.Server;
using EarthGame.Transport;
using NUnit.Framework;

namespace EarthGame.Tests.Transport
{
    /// <summary>
    /// The send cap is a harness condition (CANON ruling 11): a token bucket per connection that holds payloads
    /// back and lets them go as the elapsed time the caller reports refills it. Proved over a loopback socket,
    /// because the bucket sits on a real peer.
    /// </summary>
    public sealed class SendCapTests
    {
        private const int Port = 28925;

        [Test]
        public void ABurstAboveTheCapWaitsForTheBucketToRefill()
        {
            UdpOptions capped = new UdpOptions { SendCapBytesPerSecond = 100000 };
            using (UdpServerTransport st = new UdpServerTransport())
            using (UdpClientTransport ct = new UdpClientTransport(capped))
            {
                Stopwatch clock = Stopwatch.StartNew();
                GameServer server = new GameServer(new ServerConfig(), st, new WorldState(1, Region.Bherwerre, Region.Bherwerre.WakeClock()));
                server.Listen(Port);
                GameClient client = new GameClient(ct);
                client.Connect("127.0.0.1", Port, "William", "");
                long deadline = clock.ElapsedMilliseconds + 5000;
                double last = 0.0;
                while (client.State != ClientState.Connected && clock.ElapsedMilliseconds < deadline)
                {
                    double now = clock.Elapsed.TotalSeconds;
                    client.Update(clock.ElapsedMilliseconds);
                    server.Update(now - last);
                    last = now;
                    Thread.Sleep(5);
                }
                Assert.That(client.State, Is.EqualTo(ClientState.Connected));

                UdpConnection connection = (UdpConnection)ct.Connection;
                ct.Update(1.0); // the handshake's own traffic (Hello, the tile request) spent tokens; refill to the burst size
                long sentBefore = connection.BytesSent;
                // Ten payloads of 16 KB the server ignores (a TileChunk from a client is out of place, not fatal).
                byte[] payload = new byte[16384];
                payload[0] = (byte)MessageKind.TileChunk;
                for (int i = 0; i < 10; i++) connection.Send(payload, Delivery.Reliable);
                Assert.That(connection.PendingCount, Is.EqualTo(6), "the 64 KB burst allowance took four; six wait");
                Assert.That(connection.BytesSent - sentBefore, Is.EqualTo(4 * 16384));

                ct.Update(0.5); // +50,000 tokens: three more payloads
                Assert.That(connection.PendingCount, Is.EqualTo(3));
                ct.Update(1.0); // the bucket refills to its burst size: the rest go
                Assert.That(connection.PendingCount, Is.EqualTo(0));
                Assert.That(connection.BytesSent - sentBefore, Is.EqualTo(10 * 16384));
            }
        }
    }
}
