using System.Diagnostics;
using System.Threading;
using EarthGame.ClientCore;
using EarthGame.Engine;
using EarthGame.Server;
using EarthGame.Transport;
using NUnit.Framework;

namespace EarthGame.Tests.Transport
{
    /// <summary>
    /// The same handshake as the in-memory tests, over a real UDP socket on localhost. The in-memory path proves
    /// the game logic; this proves the bytes survive a wire, and it is where the N1 simulator gets exercised.
    /// </summary>
    public sealed class UdpLoopbackTests
    {
        private const int Port = 28915;

        /// <summary>The suite's servers are for this machine alone, so the suite never asks the firewall (UdpBindTests).</summary>
        private static UdpOptions Local => new UdpOptions { LocalOnly = true };

        private static WorldState NewWorld() =>
            new WorldState(1347, Region.Bherwerre, Region.Bherwerre.WakeClock());

        /// <summary>
        /// Pumps both ends against ONE clock. The client is handed that clock's milliseconds on every update and
        /// must send its pings by the same clock, or a round trip comes out as "now minus a number from somewhere
        /// else" — which is how the first version of these tests reported a negative RTT and waited forever.
        /// </summary>
        private static bool PumpUntil(Stopwatch clock, GameServer server, GameClient client, System.Func<bool> done, int timeoutMs)
        {
            long deadline = clock.ElapsedMilliseconds + timeoutMs;
            double last = clock.Elapsed.TotalSeconds;
            while (clock.ElapsedMilliseconds < deadline)
            {
                double now = clock.Elapsed.TotalSeconds;
                client.Update(clock.ElapsedMilliseconds);
                server.Update(now - last);
                last = now;
                if (done()) return true;
                Thread.Sleep(5);
            }
            return done();
        }

        [Test]
        public void HandshakeOverLocalhost()
        {
            using (UdpServerTransport st = new UdpServerTransport(Local))
            using (UdpClientTransport ct = new UdpClientTransport())
            {
                Stopwatch clock = Stopwatch.StartNew();
                GameServer server = new GameServer(new ServerConfig(), st, NewWorld());
                server.Listen(Port);
                GameClient client = new GameClient(ct);
                client.Connect("127.0.0.1", Port, "William", "");
                bool ok = PumpUntil(clock, server, client, () => client.State == ClientState.Connected, 5000);
                Assert.That(ok, Is.True, "client never reached Connected; state " + client.State + " reason '" + client.LastReason + "'");
                Assert.That(client.Welcome.Seed, Is.EqualTo(1347UL));
                Assert.That(server.Sessions.Count, Is.EqualTo(1));

                client.Ping(clock.ElapsedMilliseconds);
                Assert.That(PumpUntil(clock, server, client, () => client.LastRttMs >= 0, 3000), Is.True, "no Pong within 3 s");
                Assert.That(client.LastRttMs, Is.LessThan(1000), "a localhost round trip took " + client.LastRttMs + " ms");
                client.Disconnect("done");
                Assert.That(PumpUntil(clock, server, client, () => server.Sessions.Count == 0, 5000), Is.True);
            }
        }

        [Test]
        public void HandshakeSurvivesSimulatedLossAndLatency()
        {
            UdpOptions shaped = new UdpOptions
            {
                SimulatedMinLatencyMs = 40,
                SimulatedMaxLatencyMs = 60,
                SimulatedPacketLossPercent = 2,
                LocalOnly = true,
            };
            using (UdpServerTransport st = new UdpServerTransport(shaped))
            using (UdpClientTransport ct = new UdpClientTransport(shaped))
            {
                Stopwatch clock = Stopwatch.StartNew();
                GameServer server = new GameServer(new ServerConfig(), st, NewWorld());
                server.Listen(Port + 1);
                GameClient client = new GameClient(ct);
                client.Connect("127.0.0.1", Port + 1, "William", "");
                bool ok = PumpUntil(clock, server, client, () => client.State == ClientState.Connected, 10000);
                Assert.That(ok, Is.True, "client never reached Connected under 2% loss / ~100 ms RTT; state " + client.State);
            }
        }

        [Test]
        public void RefusalReasonCrossesTheWire()
        {
            using (UdpServerTransport st = new UdpServerTransport(Local))
            using (UdpClientTransport ct = new UdpClientTransport())
            {
                Stopwatch clock = Stopwatch.StartNew();
                GameServer server = new GameServer(new ServerConfig { Password = "lake" }, st, NewWorld());
                server.Listen(Port + 2);
                GameClient client = new GameClient(ct);
                client.Connect("127.0.0.1", Port + 2, "William", "swamp");
                // Over UDP the server's close can overtake its Refused message; either way the client must land in
                // Refused with the server's stated reason, never in a bare Disconnected (see TransportEvent.ReasonFromPeer).
                bool ok = PumpUntil(clock, server, client, () => client.State == ClientState.Refused, 5000);
                Assert.That(ok, Is.True, "expected Refused, got " + client.State + " with reason '" + client.LastReason + "'");
                Assert.That(client.LastReason, Is.EqualTo("password rejected"));
            }
        }
    }
}
