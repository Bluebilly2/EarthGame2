using EarthGame.ClientCore;
using EarthGame.Engine;
using EarthGame.Protocol;
using EarthGame.Server;
using EarthGame.Transport;
using NUnit.Framework;

namespace EarthGame.Tests.Server
{
    /// <summary>
    /// Client-authoritative movement, server-validated (ARCHITECTURE §7, §9), over the in-memory transport with
    /// every message serialised. The ground is the tiny fixture raster and the region is a 40 m square around it,
    /// so the spawn, the ground height and the edge are all numbers this file can state.
    /// </summary>
    public sealed class MovementValidationTests
    {
        /// <summary>A region the size of the fixture raster, waking at its centre: spawn is (0, 127, 0), the bump cell.</summary>
        private static readonly Region FixtureRegion = new Region("fixture", "Fixture", Region.Bherwerre.CentreLatitudeDeg,
            Region.Bherwerre.CentreLongitudeDeg, 40.0, 237, 8.0);

        private static Heightfield Ground() => new Heightfield(RegionRaster.Load(TestPaths.Fixture("raster", "tiny.json")));

        private static WorldState World() => new WorldState(1, FixtureRegion, FixtureRegion.WakeClock(), Ground());

        private sealed class Rig
        {
            public GameServer Server;
            public GameClient Client;
            public IServerTransport ServerTransport;
            public long Ms;

            public void Pump(int rounds = 3, double dt = 0.05)
            {
                for (int i = 0; i < rounds; i++)
                {
                    Client.Update(Ms);
                    Server.Update(dt);
                    Client.Update(Ms);
                    Ms += (long)(dt * 1000);
                }
            }

            public PlayerSession Session => Server.Sessions[0];
        }

        private static Rig Connect(WorldState world = null)
        {
            InMemoryTransport.CreatePair(out IServerTransport st, out IClientTransport ct);
            Rig rig = new Rig();
            rig.ServerTransport = st;
            rig.Server = new GameServer(new ServerConfig(), st, world ?? World());
            rig.Server.Listen(1);
            rig.Client = new GameClient(ct);
            rig.Client.Connect("memory", 1, "William", "");
            rig.Pump(5);
            Assert.That(rig.Client.State, Is.EqualTo(ClientState.Connected));
            return rig;
        }

        private static MoverState GroundedAt(Heightfield ground, double east, double north)
        {
            MoverState s = MoverState.AtRest(east, ground.HeightAt(east, north), north);
            s.Grounded = true;
            return s;
        }

        [Test]
        public void WelcomeCarriesTheSpawnOnTheGround()
        {
            Rig rig = Connect();
            Assert.That(rig.Client.Welcome.SpawnEast, Is.EqualTo(0.0).Within(1e-6));
            Assert.That(rig.Client.Welcome.SpawnNorth, Is.EqualTo(0.0).Within(1e-6));
            Assert.That(rig.Client.Welcome.SpawnUp, Is.EqualTo(127.0).Within(1e-6), "the centre cell of the fixture, bump included");
        }

        [Test]
        public void ALegalWalkIsAcceptedWithoutCorrection()
        {
            Rig rig = Connect();
            Heightfield ground = Ground();
            MoverInput input = MoverInput.Walk(1.0, 0.0);
            for (int i = 1; i <= 20; i++)
            {
                double east = i * 0.05;
                rig.Client.SendMove(input, 90f, 0f, GroundedAt(ground, east, 0.0));
                rig.Pump(1);
            }
            Assert.That(rig.Session.MovesAccepted, Is.EqualTo(20));
            Assert.That(rig.Session.Corrections, Is.EqualTo(0));
            Assert.That(rig.Client.CorrectionCount, Is.EqualTo(0));
            Assert.That(rig.Session.Body.East, Is.EqualTo(1.0).Within(1e-9));
            Assert.That(rig.Session.YawDeg, Is.EqualTo(90f));
        }

        [Test]
        public void ReportsBunchedByJitterAreJudgedByTheirOwnSpacing()
        {
            // Three reports of a 4 m/s walk arrive in one server update, as 100 ms of jitter delivers them; measured
            // over one tick each they were 8 and 12 m/s against a 7 m/s ceiling (the first corpus run, 2026-09-08).
            Rig rig = Connect();
            Heightfield ground = Ground();
            rig.Client.SendMove(MoverInput.None, 0f, 0f, GroundedAt(ground, 0.0, 0.0));
            rig.Pump(2);
            for (int i = 1; i <= 3; i++)
                rig.Client.SendMove(MoverInput.Walk(1.0, 0.0), 90f, 0f, GroundedAt(ground, i * 0.2, 0.0));
            rig.Pump(2);
            Assert.That(rig.Session.Corrections, Is.EqualTo(0));
            Assert.That(rig.Session.MovesAccepted, Is.EqualTo(4));
            Assert.That(rig.Session.Body.East, Is.EqualTo(0.6).Within(1e-9));
        }

        [Test]
        public void AClaimedGapBuysNoMoreTimeThanReallyPassed()
        {
            // A client that skips forty sequence numbers per report claims two seconds of walking each time (ten
            // metres, back and forth inside the 40 m fixture); the credit it banked covers the first two claims,
            // and the third is judged over the second and a bit that really passed.
            InMemoryTransport.CreatePair(out IServerTransport st, out IClientTransport ct);
            GameServer server = new GameServer(new ServerConfig(), st, World());
            server.Listen(1);
            GameClient client = new GameClient(ct);
            client.Connect("memory", 1, "William", "");
            long ms = 0;
            for (int i = 0; i < 5; i++)
            {
                client.Update(ms);
                server.Update(0.05);
                client.Update(ms);
                ms += 50;
            }
            Assert.That(client.State, Is.EqualTo(ClientState.Connected));
            Heightfield ground = Ground();
            PacketWriter w = new PacketWriter();
            uint sequence = 0;
            for (int i = 0; i <= 3; i++)
            {
                PlayerMoveMessage move;
                move.Sequence = sequence += 40;
                move.Input = MoverInput.Walk(1.0, 0.0);
                move.YawDeg = 90f;
                move.PitchDeg = 0f;
                move.Body = GroundedAt(ground, i % 2 == 0 ? 0.0 : 10.0, 0.0);
                w.Reset();
                move.Write(w);
                ct.Connection.Send(w.Written, Delivery.Unreliable);
                client.Update(ms);
                server.Update(0.05);
                client.Update(ms);
                ms += 50;
            }
            PlayerSession session = server.Sessions[0];
            Assert.That(session.MovesAccepted, Is.EqualTo(3), "the first report and two seconds' worth twice");
            Assert.That(session.Corrections, Is.EqualTo(1));
            Assert.That(client.LastCorrection.Reason, Does.StartWith("speed"));
            Assert.That(session.Body.East, Is.EqualTo(0.0).Within(1e-9), "held where the last honest report left it");
        }

        [Test]
        public void ATeleportIsCorrectedToTheLastAcceptedPlace()
        {
            Rig rig = Connect();
            Heightfield ground = Ground();
            rig.Client.SendMove(MoverInput.None, 0f, 0f, GroundedAt(ground, 0.0, 0.0));
            rig.Pump(1);
            uint offending = rig.Client.SendMove(MoverInput.Walk(1.0, 0.0), 0f, 0f, GroundedAt(ground, 15.0, 0.0));
            rig.Pump(2);
            Assert.That(rig.Session.Corrections, Is.EqualTo(1));
            Assert.That(rig.Client.CorrectionCount, Is.EqualTo(1));
            Assert.That(rig.Client.LastCorrection.Sequence, Is.EqualTo(offending), "the correction names the move it answers");
            Assert.That(rig.Client.LastCorrection.Reason, Does.Contain("speed"));
            Assert.That(rig.Client.LastCorrection.Body.East, Is.EqualTo(0.0).Within(1e-9), "back to the last accepted place");
            Assert.That(rig.Client.LastCorrection.Body.Grounded, Is.True);
            Assert.That(rig.Session.Body.East, Is.EqualTo(0.0).Within(1e-9), "the server did not move the player");
        }

        [Test]
        public void FloatingWhileClaimingGroundedIsCorrected()
        {
            Rig rig = Connect();
            Heightfield ground = Ground();
            MoverState floating = GroundedAt(ground, 0.0, 0.0);
            floating.Up += 5.0;
            rig.Client.SendMove(MoverInput.None, 0f, 0f, floating);
            rig.Pump(2);
            Assert.That(rig.Client.CorrectionCount, Is.EqualTo(1));
            Assert.That(rig.Client.LastCorrection.Reason, Does.Contain("ground"));
            Assert.That(rig.Client.LastCorrection.Body.Up, Is.EqualTo(127.0).Within(1e-9), "with no accepted body yet, the correction is the spawn");
        }

        [Test]
        public void OutsideTheRegionIsCorrected()
        {
            Rig rig = Connect();
            Heightfield ground = Ground();
            rig.Client.SendMove(MoverInput.None, 0f, 0f, GroundedAt(ground, 25.0, 0.0));
            rig.Pump(2);
            Assert.That(rig.Client.LastCorrection.Reason, Does.Contain("outside"));
        }

        [Test]
        public void BelowTheGroundIsCorrectedEvenWhenAirborne()
        {
            Rig rig = Connect();
            Heightfield ground = Ground();
            MoverState buried = MoverState.AtRest(0.0, ground.HeightAt(0.0, 0.0) - 3.0, 0.0);
            rig.Client.SendMove(MoverInput.None, 0f, 0f, buried);
            rig.Pump(2);
            Assert.That(rig.Client.LastCorrection.Reason, Does.Contain("below"));
        }

        [Test]
        public void AnAirborneReportAboveTheGroundIsFine()
        {
            Rig rig = Connect();
            Heightfield ground = Ground();
            MoverState falling = MoverState.AtRest(0.0, ground.HeightAt(0.0, 0.0) + 3.0, 0.0);
            falling.VelUp = -5.0;
            rig.Client.SendMove(MoverInput.None, 0f, 0f, falling);
            rig.Pump(2);
            Assert.That(rig.Client.CorrectionCount, Is.EqualTo(0), "being in the air over the ground is not a violation");
            Assert.That(rig.Session.HasBody, Is.True);
        }

        [Test]
        public void WithoutTerrainTheServerStillHoldsTheEdgeAndTheSpeed()
        {
            WorldState bare = new WorldState(1, Region.Bherwerre, Region.Bherwerre.WakeClock());
            Rig rig = Connect(bare);
            MoverState anywhere = MoverState.AtRest(100.0, 50.0, -200.0);
            anywhere.Grounded = true;
            rig.Client.SendMove(MoverInput.None, 0f, 0f, anywhere);
            rig.Pump(2);
            Assert.That(rig.Client.CorrectionCount, Is.EqualTo(0), "no ground to check against, so grounded at 50 m is taken on trust");
            rig.Client.SendMove(MoverInput.None, 0f, 0f, MoverState.AtRest(Region.Bherwerre.HalfExtentM + 1.0, 0.0, 0.0));
            rig.Pump(2);
            Assert.That(rig.Client.LastCorrection.Reason, Does.Contain("outside"));
        }

        [Test]
        public void ATerrainOfAnotherSizeIsNotThisWorlds()
        {
            Assert.That(() => new WorldState(1, Region.Bherwerre, Region.Bherwerre.WakeClock(), Ground()),
                Throws.ArgumentException.With.Message.Contains("40"));
        }

        [Test]
        public void TwoClientsSeeEachOthersBodiesAndNotTheirOwn()
        {
            InMemoryTransport.CreatePair(out IServerTransport st, out IClientTransport ct1);
            GameServer server = new GameServer(new ServerConfig(), st, World());
            server.Listen(1);
            GameClient c1 = new GameClient(ct1);
            c1.Connect("memory", 1, "William", "");
            IClientTransport ct2 = InMemoryTransport.CreateClient(st);
            GameClient c2 = new GameClient(ct2);
            c2.Connect("memory", 1, "Guest", "");
            for (int i = 0; i < 6; i++)
            {
                c1.Update(i);
                c2.Update(i);
                server.Update(0.05);
                c1.Update(i);
                c2.Update(i);
            }
            Assert.That(c1.State, Is.EqualTo(ClientState.Connected));
            Assert.That(c2.State, Is.EqualTo(ClientState.Connected));
            Heightfield ground = Ground();
            c1.SendMove(MoverInput.None, 10f, 0f, GroundedAt(ground, 2.0, 3.0));
            c2.SendMove(MoverInput.None, 20f, 0f, GroundedAt(ground, -4.0, 1.0));
            for (int i = 0; i < 4; i++)
            {
                c1.Update(i);
                c2.Update(i);
                server.Update(0.05);
                c1.Update(i);
                c2.Update(i);
            }
            Assert.That(c1.Others.ContainsKey(c2.Welcome.SessionId), Is.True, "client 1 holds client 2's body");
            Assert.That(c1.Others.ContainsKey(c1.Welcome.SessionId), Is.False, "and not its own");
            Assert.That(c1.Others[c2.Welcome.SessionId].Body.East, Is.EqualTo(-4.0).Within(1e-9));
            Assert.That(c1.Others[c2.Welcome.SessionId].YawDeg, Is.EqualTo(20f));
            Assert.That(c2.Others[c1.Welcome.SessionId].Body.North, Is.EqualTo(3.0).Within(1e-9));
        }
    }
}
