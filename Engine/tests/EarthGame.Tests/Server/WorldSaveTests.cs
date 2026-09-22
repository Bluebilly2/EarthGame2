using System;
using System.IO;
using EarthGame.ClientCore;
using EarthGame.Engine;
using EarthGame.Server;
using EarthGame.Transport;
using NUnit.Framework;

namespace EarthGame.Tests.Server
{
    /// <summary>
    /// A world written and read back is the same world: the seed, the clock's two numbers (so a resumed run still
    /// knows how many days it has lived; v1 lost that), the tick, and where each player was.
    /// </summary>
    public sealed class WorldSaveTests
    {
        private string _dir;

        [SetUp]
        public void MakeFolder()
        {
            _dir = Path.Combine(Path.GetTempPath(), "eg2-worldsave-" + Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void RemoveFolder()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        [Test]
        public void TheWorldRoundTripsThroughItsFolder()
        {
            WorldClock clock = WorldClock.Resumed(237, 8.0, Region.Bherwerre.CentreLongitudeDeg, 5);
            WorldState world = new WorldState(0xC0FFEEUL, Region.Bherwerre, clock, null, 4321);
            world.Step(0.05);
            WorldSave.Write(_dir, world, null, "2026-09-08T10:00:00Z");
            Assert.That(WorldSave.Exists(_dir), Is.True);

            WorldSaveInfo info = WorldSave.Read(_dir);
            Assert.That(info.Seed, Is.EqualTo(0xC0FFEEUL));
            Assert.That(info.RegionId, Is.EqualTo("bherwerre"));
            Assert.That(info.Tick, Is.EqualTo(4322));
            Assert.That(info.CreatedUtc, Is.EqualTo("2026-09-08T10:00:00Z"));
            WorldState back = WorldSave.Restore(info, null);
            Assert.That(back.Clock.TotalHours, Is.EqualTo(world.Clock.TotalHours));
            Assert.That(back.Clock.DaysElapsed, Is.EqualTo(5), "the days already lived survive the save");
            Assert.That(back.Tick, Is.EqualTo(4322));
            Assert.That(back.Region, Is.SameAs(Region.Bherwerre));
        }

        /// <summary>
        /// A world owns which piece of the Earth it is (WG.2, 2026-09-22): a saved valley world opened by a tool that named no
        /// region was refused as "opened as 'bherwerre'". The one reading of a saved world's region, for the shell and the host.
        /// </summary>
        [Test]
        public void ASavedWorldNamesItsOwnRegion()
        {
            Assert.That(WorldSave.RegionOf(_dir), Is.Null, "no world here yet");
            WorldClock clock = WorldClock.Resumed(Region.KangarooValley.WakeDayOfYear, Region.KangarooValley.WakeLocalHour, Region.KangarooValley.CentreLongitudeDeg, 0);
            WorldState world = new WorldState(7UL, Region.KangarooValley, clock, null, 1);
            WorldSave.Write(_dir, world, null, "2026-09-22T11:30:00Z");
            Assert.That(WorldSave.RegionOf(_dir), Is.SameAs(Region.KangarooValley));
            string text = File.ReadAllText(Path.Combine(_dir, "world.json"));
            File.WriteAllText(Path.Combine(_dir, "world.json"), text.Replace("kangaroo-valley", "atlantis"));
            Assert.That(WorldSave.RegionOf(_dir), Is.Null, "a region this build does not know is null, for the preparation to refuse with its reason");
        }

        [Test]
        public void TheCreationDateIsWrittenOnceAndCarriedForward()
        {
            WorldState world = new WorldState(1, Region.Bherwerre, Region.Bherwerre.WakeClock());
            WorldSave.Write(_dir, world, null, "2026-09-08T10:00:00Z");
            WorldSave.Write(_dir, world, null, "2026-09-09T11:00:00Z");
            WorldSaveInfo info = WorldSave.Read(_dir);
            Assert.That(info.CreatedUtc, Is.EqualTo("2026-09-08T10:00:00Z"));
            string text = File.ReadAllText(Path.Combine(_dir, WorldSave.WorldFile));
            Assert.That(text, Does.Contain("\"saved_utc\": \"2026-09-09T11:00:00Z\""));
            Assert.That(Directory.GetFiles(_dir, "*.part").Length, Is.EqualTo(0), "no half-written file is left behind");
        }

        [Test]
        public void AnotherVersionIsRefused()
        {
            WorldState world = new WorldState(1, Region.Bherwerre, Region.Bherwerre.WakeClock());
            WorldSave.Write(_dir, world, null, "");
            string path = Path.Combine(_dir, WorldSave.WorldFile);
            File.WriteAllText(path, File.ReadAllText(path).Replace("\"version\": 1", "\"version\": 7"));
            Assert.That(() => WorldSave.Read(_dir), Throws.TypeOf<InvalidDataException>().With.Message.Contains("version"));
        }

        [Test]
        public void APlayerWakesWhereTheyWereSaved()
        {
            // A server with one player who walked somewhere, saved; a new server from the folder; the same name joins.
            InMemoryTransport.CreatePair(out IServerTransport st, out IClientTransport ct);
            WorldState world = new WorldState(9, Region.Bherwerre, Region.Bherwerre.WakeClock());
            GameServer server = new GameServer(new ServerConfig(), st, world);
            server.Listen(1);
            GameClient client = new GameClient(ct);
            client.Connect("memory", 1, "William", "");
            Pump(server, client, 5);
            MoverState there = MoverState.AtRest(-2000.0, 12.5, 1500.0);
            there.Grounded = true;
            client.SendMove(MoverInput.None, 270f, -5f, there);
            Pump(server, client, 3);
            Assert.That(server.Sessions[0].HasBody, Is.True);
            server.Save(_dir, "2026-09-08T12:00:00Z");
            Assert.That(File.Exists(Path.Combine(_dir, WorldSave.PlayersFolder, "William.egp")), Is.True, "the player file's second version (M1.3)");

            WorldSaveInfo info = WorldSave.Read(_dir);
            Assert.That(info.Players.ContainsKey("William"), Is.True);
            Assert.That(info.Players["William"].Body.East, Is.EqualTo(-2000.0));
            Assert.That(info.Players["William"].YawDeg, Is.EqualTo(270f));

            InMemoryTransport.CreatePair(out IServerTransport st2, out IClientTransport ct2);
            GameServer resumed = new GameServer(new ServerConfig(), st2, WorldSave.Restore(info, null));
            resumed.RememberPlayers(info.Players.Values);
            resumed.Listen(1);
            GameClient again = new GameClient(ct2);
            again.Connect("memory", 1, "William", "");
            Pump(resumed, again, 5);
            Assert.That(again.State, Is.EqualTo(ClientState.Connected));
            Assert.That(again.Welcome.SpawnEast, Is.EqualTo(-2000.0), "welcomed back to the saved place");
            Assert.That(again.Welcome.SpawnUp, Is.EqualTo(12.5));
            Assert.That(again.Welcome.SpawnNorth, Is.EqualTo(1500.0));
            Assert.That(resumed.Sessions[0].HasBody, Is.True, "and the server already holds that body for validation");

            InMemoryTransport.CreatePair(out IServerTransport st3, out IClientTransport ct3);
            GameServer fresh = new GameServer(new ServerConfig(), st3, WorldSave.Restore(info, null));
            fresh.RememberPlayers(info.Players.Values);
            fresh.Listen(1);
            GameClient stranger = new GameClient(ct3);
            stranger.Connect("memory", 1, "Guest", "");
            Pump(fresh, stranger, 5);
            Assert.That(stranger.Welcome.SpawnEast, Is.Not.EqualTo(-2000.0), "a name the save never saw wakes at the world's wake");
        }

        [Test]
        public void NamesBecomeSafeFileNames()
        {
            Assert.That(WorldSave.FileNameFor("William"), Is.EqualTo("William"));
            Assert.That(WorldSave.FileNameFor("../x/y:z"), Is.EqualTo("___x_y_z"));
            Assert.That(WorldSave.FileNameFor(""), Is.EqualTo("_"));
            Assert.That(WorldSave.FileNameFor("Con"), Is.EqualTo("_Con"), "a device's name is not a file's on Windows");
            Assert.That(WorldSave.FileNameFor("lpt1"), Is.EqualTo("_lpt1"));
            Assert.That(WorldSave.FileNameFor("Console"), Is.EqualTo("Console"));
        }

        /// <summary>A save that would write two founders to one file is refused before it writes anything (M1.3d), and the save before it stands.</summary>
        [Test]
        public void TwoFoundersWhoseFilesWouldBeOneAreRefusedBeforeAnythingIsWritten()
        {
            WorldState world = new WorldState(1, Region.Bherwerre, Region.Bherwerre.WakeClock());
            WorldSave.Write(_dir, world, new[] { Founder("William", 10.0) }, "2026-09-14T10:00:00Z");
            string file = Path.Combine(_dir, WorldSave.PlayersFolder, "William.egp");
            byte[] before = File.ReadAllBytes(file);

            InvalidOperationException refused = Assert.Throws<InvalidOperationException>(
                () => WorldSave.Write(_dir, world, new[] { Founder("William", 20.0), Founder("william", 30.0) }, "2026-09-14T10:00:30Z"));
            Assert.That(refused.Message, Does.Contain("'William'").And.Contain("'william'"));
            Assert.That(File.ReadAllBytes(file), Is.EqualTo(before), "the save before stands");
            Assert.That(Directory.GetFiles(_dir, "*.part", SearchOption.AllDirectories), Is.Empty, "and nothing was written aside");
            Assert.That(WorldSave.ShareAFile("Jo Jo", "Jo_Jo"), Is.True, "marks become underscores");
            Assert.That(WorldSave.ShareAFile("William", "William"), Is.False, "a name's file is its own");
        }

        /// <summary>A founder named for a Windows device is written to a file of their own and read back (M1.3d).</summary>
        [Test]
        public void AFounderNamedForADeviceIsWrittenAndReadBack()
        {
            WorldState world = new WorldState(1, Region.Bherwerre, Region.Bherwerre.WakeClock());
            WorldSave.Write(_dir, world, new[] { Founder("Con", 42.0) }, "2026-09-14T10:00:00Z");
            WorldSaveInfo info = WorldSave.Read(_dir);
            Assert.That(info.Players.ContainsKey("Con"), Is.True);
            Assert.That(info.Players["Con"].Body.East, Is.EqualTo(42.0));
        }

        private static SavedPlayer Founder(string name, double east)
        {
            SavedPlayer p = default;
            p.Name = name;
            p.Body = MoverState.AtRest(east, 0.0, 0.0);
            return p;
        }

        private static void Pump(GameServer server, GameClient client, int rounds)
        {
            for (int i = 0; i < rounds; i++)
            {
                client.Update(i);
                server.Update(0.05);
                client.Update(i);
            }
        }
    }
}
