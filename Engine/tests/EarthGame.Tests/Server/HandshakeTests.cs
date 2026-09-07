using EarthGame.ClientCore;
using EarthGame.Engine;
using EarthGame.Protocol;
using EarthGame.Server;
using EarthGame.Transport;
using NUnit.Framework;

namespace EarthGame.Tests.Server
{
    /// <summary>
    /// A client and a server in one process over the in-memory wire — which is what single player is.
    /// These tests are the first "headless session"; the two-client soak of N4 grows from the same shape.
    /// </summary>
    public sealed class HandshakeTests
    {
        private static WorldState NewWorld() =>
            new WorldState(1347, Region.Bherwerre, Region.Bherwerre.WakeClock());

        private static void Pump(GameServer server, GameClient client, int rounds = 20, double dt = 0.05)
        {
            for (int i = 0; i < rounds; i++)
            {
                client.Update(i * 50);
                server.Update(dt);
                client.Update(i * 50 + 25);
            }
        }

        [Test]
        public void HelloIsAnsweredWithWelcome()
        {
            InMemoryTransport.CreatePair(out IServerTransport st, out IClientTransport ct);
            WorldState world = NewWorld();
            GameServer server = new GameServer(new ServerConfig(), st, world);
            server.Listen(28015);
            GameClient client = new GameClient(ct);
            client.Connect("memory", 28015, "William", "");
            Pump(server, client);

            Assert.That(client.State, Is.EqualTo(ClientState.Connected));
            Assert.That(client.Welcome.Seed, Is.EqualTo(1347UL));
            Assert.That(client.Welcome.RegionId, Is.EqualTo(Region.Bherwerre.Id));
            Assert.That(client.Welcome.TickRate, Is.EqualTo(20));
            Assert.That(server.Sessions.Count, Is.EqualTo(1));
            Assert.That(server.Sessions[0].Name, Is.EqualTo("William"));
        }

        [Test]
        public void WrongProtocolVersionIsRefusedWithTheReason()
        {
            InMemoryTransport.CreatePair(out IServerTransport st, out IClientTransport ct);
            GameServer server = new GameServer(new ServerConfig(), st, NewWorld());
            server.Listen(1);
            ct.Connect("memory", 1);
            ct.Update();
            TransportEvent evt;
            Assert.That(ct.Poll(out evt) && evt.Kind == TransportEventKind.Connected, Is.True);

            HelloMessage hello;
            hello.ProtocolVersion = (ushort)(ProtocolInfo.Version + 1);
            hello.PlayerName = "old";
            hello.Password = "";
            PacketWriter w = new PacketWriter();
            hello.Write(w);
            ct.Connection.Send(w.Written, Delivery.Reliable);

            server.Update(0.05);
            ct.Update();
            string reason = null;
            while (ct.Poll(out evt))
            {
                if (evt.Kind == TransportEventKind.Data && MessageHeader.PeekKind(evt.Data, evt.Offset, evt.Count) == MessageKind.Refused)
                {
                    PacketReader r = new PacketReader(evt.Data, evt.Offset, evt.Count);
                    r.ReadByte();
                    reason = RefusedMessage.Read(r).Reason;
                }
            }
            Assert.That(reason, Does.Contain("protocol version"));
            Assert.That(server.Sessions.Count, Is.EqualTo(0));
            Assert.That(ct.Connection.IsOpen, Is.False);
        }

        [Test]
        public void PasswordMismatchIsRefused()
        {
            InMemoryTransport.CreatePair(out IServerTransport st, out IClientTransport ct);
            ServerConfig cfg = new ServerConfig { Password = "secret" };
            GameServer server = new GameServer(cfg, st, NewWorld());
            server.Listen(1);
            GameClient client = new GameClient(ct);
            client.Connect("memory", 1, "William", "wrong");
            Pump(server, client);
            Assert.That(client.State, Is.EqualTo(ClientState.Refused));
            Assert.That(client.LastReason, Is.EqualTo("password rejected"));
        }

        [Test]
        public void ServerFullIsRefused()
        {
            InMemoryTransport.CreatePair(out IServerTransport st, out IClientTransport ct1);
            GameServer server = new GameServer(new ServerConfig { MaxPlayers = 1 }, st, NewWorld());
            server.Listen(1);
            GameClient c1 = new GameClient(ct1);
            c1.Connect("memory", 1, "one", "");
            Pump(server, c1);
            Assert.That(c1.State, Is.EqualTo(ClientState.Connected));

            IClientTransport ct2 = InMemoryTransport.CreateClient(st);
            GameClient c2 = new GameClient(ct2);
            c2.Connect("memory", 1, "two", "");
            Pump(server, c2);
            Assert.That(c2.State, Is.EqualTo(ClientState.Refused));
            Assert.That(c2.LastReason, Does.Contain("full"));
            Assert.That(server.Sessions.Count, Is.EqualTo(1));
        }

        [Test]
        public void TwoClientsShareOneWorld()
        {
            InMemoryTransport.CreatePair(out IServerTransport st, out IClientTransport ct1);
            GameServer server = new GameServer(new ServerConfig(), st, NewWorld());
            server.Listen(1);
            IClientTransport ct2 = InMemoryTransport.CreateClient(st);
            GameClient c1 = new GameClient(ct1);
            GameClient c2 = new GameClient(ct2);
            c1.Connect("memory", 1, "one", "");
            c2.Connect("memory", 1, "two", "");
            for (int i = 0; i < 20; i++)
            {
                c1.Update(i); c2.Update(i);
                server.Update(0.05);
            }
            Assert.That(c1.State, Is.EqualTo(ClientState.Connected));
            Assert.That(c2.State, Is.EqualTo(ClientState.Connected));
            Assert.That(c1.Welcome.Seed, Is.EqualTo(c2.Welcome.Seed));
            Assert.That(c1.Welcome.SessionId, Is.Not.EqualTo(c2.Welcome.SessionId));
            Assert.That(server.Sessions.Count, Is.EqualTo(2));
        }

        [Test]
        public void ClientLeavingRemovesItsSession()
        {
            InMemoryTransport.CreatePair(out IServerTransport st, out IClientTransport ct);
            GameServer server = new GameServer(new ServerConfig(), st, NewWorld());
            server.Listen(1);
            GameClient client = new GameClient(ct);
            client.Connect("memory", 1, "William", "");
            Pump(server, client);
            Assert.That(server.Sessions.Count, Is.EqualTo(1));
            string leftReason = null;
            server.SessionLeft += (s, reason) => leftReason = reason;
            client.Disconnect("going to bed");
            server.Update(0.05);
            Assert.That(server.Sessions.Count, Is.EqualTo(0));
            Assert.That(leftReason, Is.EqualTo("going to bed"));
        }

        [Test]
        public void PingIsAnsweredWithTheServerTick()
        {
            InMemoryTransport.CreatePair(out IServerTransport st, out IClientTransport ct);
            GameServer server = new GameServer(new ServerConfig(), st, NewWorld());
            server.Listen(1);
            GameClient client = new GameClient(ct);
            client.Connect("memory", 1, "William", "");
            Pump(server, client);
            client.Ping(1000);
            long tickWhenAnswered = server.World.Tick; // the server handles input before it steps the world
            server.Update(0.05);
            client.Update(1037);
            Assert.That(client.LastRttMs, Is.EqualTo(37));
            Assert.That(client.LastServerTick, Is.EqualTo(tickWhenAnswered));
            Assert.That(server.World.Tick, Is.EqualTo(tickWhenAnswered + 1));
        }

        [Test]
        public void MalformedFirstMessageIsRefusedNotCrashed()
        {
            InMemoryTransport.CreatePair(out IServerTransport st, out IClientTransport ct);
            GameServer server = new GameServer(new ServerConfig(), st, NewWorld());
            server.Listen(1);
            ct.Connect("memory", 1);
            ct.Update();
            TransportEvent evt;
            ct.Poll(out evt);
            byte[] garbage = { (byte)MessageKind.Hello, 0x01 }; // Hello kind, then a truncated body
            ct.Connection.Send(garbage, Delivery.Reliable);
            server.Update(0.05);
            ct.Update();
            bool refused = false;
            while (ct.Poll(out evt))
                if (evt.Kind == TransportEventKind.Data && MessageHeader.PeekKind(evt.Data, evt.Offset, evt.Count) == MessageKind.Refused) refused = true;
            Assert.That(refused, Is.True);
            Assert.That(server.Sessions.Count, Is.EqualTo(0));
        }
    }
}
