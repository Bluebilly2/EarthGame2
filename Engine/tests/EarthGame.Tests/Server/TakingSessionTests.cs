using System;
using System.Collections.Generic;
using System.IO;
using EarthGame.ClientCore;
using EarthGame.Engine;
using EarthGame.Protocol;
using EarthGame.Server;
using EarthGame.Transport;
using NUnit.Framework;

namespace EarthGame.Tests.Server
{
    /// <summary>
    /// Taking what lies between founders over the in-memory transport (M1.5b promises 2 to 5): one founder's taking is told
    /// to the other, a cobble comes up as its cell's stone, the crosshair's search of the held tiles leaves out what was
    /// taken, and every taking survives a save and a load and is told to whoever joins after.
    /// </summary>
    public sealed class TakingSessionTests
    {
        private static readonly Region Fixture = new Region("fixture", "Fixture", Region.Bherwerre.CentreLatitudeDeg,
            Region.Bherwerre.CentreLongitudeDeg, TestRasters.MadeExtentM, 237, 8.0);
        private const string Now = "2026-09-11T12:00:00Z";

        /// <summary>The cell whose centre is (300, -300) on the made coast.</summary>
        private const int Row = 110, Col = 110;

        private Heightfield _ground;
        private string _dir;

        [SetUp]
        public void SetUp()
        {
            _ground = new Heightfield(TestRasters.MadeCoast());
            _dir = Path.Combine(Path.GetTempPath(), "EarthGame2.Tests", "taking", Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        private sealed class Rig
        {
            public GameServer Server;
            public IServerTransport ServerTransport;
            public readonly List<GameClient> Clients = new List<GameClient>();
            public long Ms;

            public void Pump(int rounds = 3, double dt = 0.05)
            {
                for (int i = 0; i < rounds; i++)
                {
                    foreach (GameClient c in Clients) c.Update(Ms);
                    Server.Update(dt);
                    foreach (GameClient c in Clients) c.Update(Ms);
                    Ms += (long)(dt * 1000);
                }
            }

            public GameClient Join(string name)
            {
                GameClient c = new GameClient(InMemoryTransport.CreateClient(ServerTransport));
                c.Connect("memory", 1, name, "");
                Clients.Add(c);
                return c;
            }
        }

        private static Rig Start(WorldState world)
        {
            InMemoryTransport.CreatePair(out IServerTransport st, out IClientTransport _);
            Rig rig = new Rig { ServerTransport = st };
            rig.Server = new GameServer(new ServerConfig { InterestRadiusM = 100.0, InterestMarginM = 50.0 }, st, world);
            rig.Server.Listen(1);
            return rig;
        }

        private static int StoneCode(StoneType stone)
        {
            for (int i = 0; i < StoneType.All.Count; i++)
                if (ReferenceEquals(StoneType.All[i], stone)) return i + 1;
            throw new ArgumentException(stone.Name);
        }

        /// <summary>The made coast with three sticks and two cobbles on one cell, and silcrete named everywhere.</summary>
        private WorldState World()
        {
            RegionRaster loose = TestRasters.FromCodes(TestRasters.MadeSide, TestRasters.MadeCellM, TestRasters.MadeExtentM, "made_loose", "loose",
                (row, col) => row == Row && col == Col ? LooseCodes.Pack(3, 2) : 0u, null);
            RegionRaster stone = TestRasters.FromCodes(TestRasters.MadeSide, TestRasters.MadeCellM, TestRasters.MadeExtentM, "made_stone", "stone",
                (row, col) => (uint)StoneCode(StoneType.Silcrete), null);
            return new WorldState(1347UL, Fixture, Fixture.WakeClock(), _ground, 0, null, null, null, null, loose, stone);
        }

        private SavedPlayer At(string name, double east, double north)
        {
            SavedPlayer p = default;
            p.Name = name;
            p.Body = MoverState.AtRest(east, _ground.HeightAt(east, north), north);
            p.Body.Grounded = true;
            return p;
        }

        private static IntentMessage Take(LyingThing thing) => new IntentMessage { Verb = Verb.PickUp, Target = IntentMessage.TargetLying, Lying = thing };

        /// <summary>Sends an intent, pumps until its answer is back, and returns the outcome.</summary>
        private static VerbOutcome Do(Rig rig, GameClient client, IntentMessage intent)
        {
            uint sequence = client.SendIntent(intent);
            Assert.That(sequence, Is.Not.EqualTo(0u), "sent while connected");
            for (int i = 0; i < 10 && client.LastIntentResult.Sequence != sequence; i++) rig.Pump(1);
            Assert.That(client.LastIntentResult.Sequence, Is.EqualTo(sequence), "answered");
            return client.LastIntentResult.Outcome;
        }

        [Test]
        public void AThingTakenFromTheGroundIsGoneForEveryoneAndStaysGoneThroughASave()
        {
            WorldState world = World();
            LyingThing stick = new LyingThing(Row, Col, StandLayout.Kind.Stick, 0);
            LyingThing cobble = new LyingThing(Row, Col, StandLayout.Kind.Cobble, 1);
            Assert.That(LyingThings.TryFind(world, stick, out Double3 stickAt), Is.True);
            Assert.That(LyingThings.TryFind(world, cobble, out Double3 cobbleAt), Is.True);
            Rig rig = Start(world);
            rig.Server.RememberPlayers(new[] { At("A", stickAt.X + 1.0, stickAt.Z), At("B", cobbleAt.X + 1.0, cobbleAt.Z) });
            GameClient a = rig.Join("A"), b = rig.Join("B");
            List<LooseTaken.Cell> toldB = new List<LooseTaken.Cell>();
            b.LooseTakenChanged += c => toldB.Add(c);
            rig.Pump(30);
            Assert.That(a.State, Is.EqualTo(ClientState.Connected));

            // The crosshair's search of the tiles A holds finds the cell's things where the server puts them.
            List<LyingNearby> near = new List<LyingNearby>();
            LyingNear.Find(stickAt.X, stickAt.Z, 8.0, a.Tiles, a.Grid, a.Taken, near);
            Assert.That(near.Exists(n => n.Thing.Equals(stick)), Is.True, "the stick, before it is taken");
            Assert.That(near.Count, Is.EqualTo(5), "three sticks and two cobbles");

            ulong next = world.Entities.NextId;
            Assert.That(Do(rig, a, Take(stick)), Is.EqualTo(VerbOutcome.Done));
            rig.Pump(2);
            Assert.That(a.Carrying.Things.Length, Is.EqualTo(1));
            Assert.That(a.Carrying.Things[0].Id, Is.EqualTo(next), "an id the store allocated");
            Assert.That(a.Carrying.Things[0].Definition, Is.SameAs(DefinitionCatalogue.Stick));
            Assert.That(b.Taken.IsTaken(stick), Is.True, "B is told");
            Assert.That(a.Taken.IsTaken(stick), Is.True, "and so is A");
            Assert.That(toldB.Count, Is.EqualTo(1));
            Assert.That(Do(rig, b, Take(stick)), Is.EqualTo(VerbOutcome.NotThere), "B cannot take what A took");

            near.Clear();
            LyingNear.Find(stickAt.X, stickAt.Z, 8.0, a.Tiles, a.Grid, a.Taken, near);
            Assert.That(near.Exists(n => n.Thing.Equals(stick)), Is.False, "the stick A took is not found again");
            LyingNearby beside = near.Find(n => n.Thing.Equals(new LyingThing(Row, Col, StandLayout.Kind.Stick, 1)));
            Assert.That(beside.Thing.Kind, Is.EqualTo(StandLayout.Kind.Stick), "the stick beside it is");
            Assert.That(LyingThings.TryFind(world, beside.Thing, out Double3 truth), Is.True);
            Assert.That(beside.Instance.East, Is.EqualTo((float)truth.X), "where the server puts it");
            Assert.That(beside.Instance.North, Is.EqualTo((float)truth.Z));

            Assert.That(Do(rig, b, Take(cobble)), Is.EqualTo(VerbOutcome.Done));
            rig.Pump(2);
            Assert.That(b.Carrying.Things[0].Definition, Is.SameAs(DefinitionCatalogue.CobbleOf(StoneType.Silcrete)), "a cobble comes up as its cell's stone");
            Assert.That(a.Taken.IsTaken(cobble), Is.True);

            string before = rig.Server.Digest();
            WorldSave.Write(_dir, rig.Server.World, rig.Server.PlayersToSave(), Now);
            WorldSaveInfo info = WorldSave.Read(_dir);
            Assert.That(info.Digest, Is.EqualTo(before), "the digest file names the takings");
            Rig loaded = Start(WorldSave.Restore(info, _ground, Fixture, null, null, null, world.Loose, world.Stone));
            loaded.Server.RememberPlayers(info.Players.Values);
            Assert.That(loaded.Server.Digest(), Is.EqualTo(before), "the same name after the load");
            GameClient c = loaded.Join("C");
            loaded.Pump(10);
            Assert.That(c.Taken.IsTaken(stick) && c.Taken.IsTaken(cobble), Is.True, "a joiner is told every taking");
            Assert.That(c.Taken.IsTaken(new LyingThing(Row, Col, StandLayout.Kind.Stick, 1)), Is.False, "and nothing more");
        }
    }
}
