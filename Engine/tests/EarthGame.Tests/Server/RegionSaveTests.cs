using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using EarthGame.Engine;
using EarthGame.Server;
using NUnit.Framework;

namespace EarthGame.Tests.Server
{
    /// <summary>The entity save (M1.3 promise 4): region files by cell, the player file's second version, the digest, the save law.</summary>
    public sealed class RegionSaveTests
    {
        private static readonly Region Fixture = new Region("fixture", "Fixture", Region.Bherwerre.CentreLatitudeDeg,
            Region.Bherwerre.CentreLongitudeDeg, TestRasters.MadeExtentM, 237, 8.0, Region.Bherwerre.CentreLatitudeDeg, Region.Bherwerre.CentreLongitudeDeg);
        private const string Now = "2026-09-09T10:00:00Z";

        private string _dir;
        private Heightfield _terrain;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "EarthGame2.Tests", "regions", Guid.NewGuid().ToString("N"));
            _terrain = new Heightfield(TestRasters.MadeCoast());
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        /// <summary>A world with a cobble in the south-west cell, and a stick falling and a cobble resting in cell (2, 2).</summary>
        private WorldState Make()
        {
            WorldState w = new WorldState(1347UL, Fixture, Fixture.WakeClock(), _terrain);
            w.SpawnItem(DefinitionCatalogue.Cobble, -500, -500);
            w.SpawnItem(DefinitionCatalogue.Stick, 300, 300, w.GroundAt(300, 300) + 3.0);
            w.SpawnItem(DefinitionCatalogue.Cobble, 310, 305, null, 45f);
            return w;
        }

        private SavedPlayer Player(string name, double east, double north, bool wading = false, Stance stance = Stance.Standing)
        {
            SavedPlayer p;
            p.Name = name;
            p.Body = MoverState.AtRest(east, _terrain.HeightAt(east, north), north);
            p.Body.Grounded = true;
            p.Body.Wading = wading;
            p.Body.Stance = stance;
            p.YawDeg = 12f;
            p.PitchDeg = -3f;
            p.SavedTick = 5;
            return p;
        }

        private static string Digest(WorldState w, IReadOnlyList<SavedPlayer> players)
        {
            List<KeyValuePair<string, MoverState>> bodies = new List<KeyValuePair<string, MoverState>>();
            foreach (SavedPlayer p in players) bodies.Add(new KeyValuePair<string, MoverState>(p.Name, p.Body));
            return WorldDigest.World(w, bodies);
        }

        [Test]
        public void TheWorldRoundTripsWithItsEntitiesTheNextIdAndThePlayersFlags()
        {
            WorldState w = Make();
            for (int i = 0; i < 3; i++) w.Step(0.05);
            SavedPlayer[] players = { Player("William", 300, -300, wading: true, stance: Stance.Crouching) };
            string expected = Digest(w, players);
            WorldSave.Write(_dir, w, players, Now);
            Assert.That(File.Exists(Path.Combine(_dir, "regions", "r.0.0.egr")), Is.True, "the south-west cell");
            Assert.That(File.Exists(Path.Combine(_dir, "regions", "r.2.2.egr")), Is.True, "the cell 300 m east and north of the centre");
            Assert.That(Directory.GetFiles(Path.Combine(_dir, "regions")).Length, Is.EqualTo(2));
            Assert.That(File.Exists(Path.Combine(_dir, "players", "William.egp")), Is.True);
            Assert.That(File.ReadAllText(Path.Combine(_dir, "digest.txt")).Trim(), Is.EqualTo(expected));

            WorldSaveInfo info = WorldSave.Read(_dir);
            Assert.That(info.Entities.Count, Is.EqualTo(3));
            Assert.That(info.Entities[0].Id, Is.EqualTo(1UL));
            Assert.That(info.Entities[1].Key, Is.EqualTo("item/stick"));
            Assert.That(info.Entities[1].Item.Resting, Is.False, "still falling when saved");
            Assert.That(info.Entities[1].Item.FallSpeed, Is.GreaterThan(0f));
            Assert.That(info.Entities[2].YawDeg, Is.EqualTo(45f));
            Assert.That(info.NextEntityId, Is.EqualTo(4UL));
            Assert.That(info.Digest, Is.EqualTo(expected));
            SavedPlayer back = info.Players["William"];
            Assert.That(back.Body.Wading, Is.True, "version 2 keeps wading");
            Assert.That(back.Body.Stance, Is.EqualTo(Stance.Crouching), "and the stance");
            Assert.That(back.Body.Grounded, Is.True);
            Assert.That(back.PitchDeg, Is.EqualTo(-3f));

            WorldState restored = WorldSave.Restore(info, _terrain, Fixture);
            Assert.That(Digest(restored, new[] { back }), Is.EqualTo(expected), "the same name after the round trip");
            Assert.That(restored.Entities.NextId, Is.EqualTo(4UL));
            Assert.That(restored.SpawnItem(DefinitionCatalogue.Stick, 0, 0).Id.Value, Is.EqualTo(4UL), "no id is reused after a load");
            Assert.That(restored.Entities.All[1].Initialised, Is.True);
        }

        [Test]
        public void AnEmptiedCellsFileIsRemovedAndTwoSavesOfOneStateAreByteIdentical()
        {
            WorldState w = Make();
            SavedPlayer[] players = { Player("William", 300, -300) };
            string a = Path.Combine(_dir, "a"), b = Path.Combine(_dir, "b");
            WorldSave.Write(a, w, players, Now);
            w.Entities.Kill(w.Entities.All[0]);
            w.Step(0.05);
            WorldSave.Write(a, w, players, Now);
            Assert.That(File.Exists(Path.Combine(a, "regions", "r.0.0.egr")), Is.False, "the cell holds nothing now");
            WorldSave.Write(b, w, players, Now);
            foreach (string file in Directory.GetFiles(a, "*", SearchOption.AllDirectories))
            {
                string twin = Path.Combine(b, file.Substring(a.Length + 1));
                Assert.That(File.Exists(twin), Is.True, twin);
                Assert.That(File.ReadAllBytes(twin), Is.EqualTo(File.ReadAllBytes(file)), file + " differs between the two folders");
            }
            Assert.That(Directory.GetFiles(a, "*", SearchOption.AllDirectories).Length, Is.EqualTo(Directory.GetFiles(b, "*", SearchOption.AllDirectories).Length));
        }

        [Test]
        public void AVersionOnePlayerFileIsReadAndThenReplaced()
        {
            WorldState w = Make();
            Directory.CreateDirectory(Path.Combine(_dir, "players"));
            File.WriteAllText(Path.Combine(_dir, "players", "Old.json"),
                "{\"format\":\"eg2.player\",\"version\":1,\"name\":\"Old\",\"east\":300.0,\"up\":12.5,\"north\":-300.0,\"yaw_deg\":90.0,\"pitch_deg\":0.0,\"grounded\":true,\"saved_tick\":9}",
                new UTF8Encoding(false));
            WorldSave.Write(_dir, w, new SavedPlayer[0], Now);
            WorldSaveInfo info = WorldSave.Read(_dir);
            Assert.That(info.Players.ContainsKey("Old"), Is.True, "version 1 is still read");
            Assert.That(info.Players["Old"].Body.East, Is.EqualTo(300.0));
            Assert.That(info.Players["Old"].Body.Wading, Is.False, "version 1 never carried it");
            WorldSave.Write(_dir, w, new[] { info.Players["Old"] }, Now);
            Assert.That(File.Exists(Path.Combine(_dir, "players", "Old.egp")), Is.True);
            Assert.That(File.Exists(Path.Combine(_dir, "players", "Old.json")), Is.False, "superseded, not left to disagree");
        }

        [Test]
        public void EveryPersistedFieldEntersTheDigest()
        {
            string baseline = Digest(Make(), new[] { Player("William", 300, -300) });
            var sabotage = new List<(string, Action<WorldState>, Func<SavedPlayer, SavedPlayer>)>
            {
                ("position", w => w.Entities.All[0].Move(new Double3(-501, w.Entities.All[0].Position.Y, -500), 1), null),
                ("yaw", w => w.Entities.All[0].Turn(1f, 1), null),
                ("resting", w => w.Entities.All[1].SetItem(new ItemComponent { Resting = true, FallSpeed = 0f }, 1), null),
                ("fall speed", w => w.Entities.All[1].SetItem(new ItemComponent { Resting = false, FallSpeed = 1f }, 1), null),
                ("next id", w => w.Entities.SetNextId(100), null),
                ("tick", w => w.Step(0.05), null),
                ("clock", w => w.Clock.SetTotalHours(w.Clock.TotalHours + 1.0), null),
                ("an entity killed", w => { w.Entities.Kill(w.Entities.All[2]); w.Entities.EndTick(); }, null),
                ("a body's wading", null, p => { p.Body.Wading = true; return p; }),
                ("a body's stance", null, p => { p.Body.Stance = Stance.Crouching; return p; }),
                ("a body's grounded", null, p => { p.Body.Grounded = false; return p; }),
                ("a body's position", null, p => { p.Body.East += 0.001; return p; }),
            };
            foreach ((string name, Action<WorldState> mutate, Func<SavedPlayer, SavedPlayer> mutatePlayer) in sabotage)
            {
                WorldState w = Make();
                SavedPlayer p = Player("William", 300, -300);
                mutate?.Invoke(w);
                if (mutatePlayer != null) p = mutatePlayer(p);
                Assert.That(Digest(w, new[] { p }), Is.Not.EqualTo(baseline), name + " left the digest unchanged");
            }
        }

        [Test]
        public void ACorruptOrMisplacedRegionFileIsRefused()
        {
            WorldState w = Make();
            WorldSave.Write(_dir, w, new SavedPlayer[0], Now);
            string path = Path.Combine(_dir, "regions", "r.2.2.egr");
            byte[] bytes = File.ReadAllBytes(path);
            List<SavedEntity> ok = RegionFile.Decode(bytes, out int cx, out int cz);
            Assert.That((cx, cz), Is.EqualTo((2, 2)));
            Assert.That(ok.Count, Is.EqualTo(2));
            byte[] corrupt = (byte[])bytes.Clone();
            corrupt[RegionFile.HeaderBytes + 20] ^= 0xFF;
            Assert.Throws<InvalidDataException>(() => RegionFile.Decode(corrupt, out _, out _), "a flipped byte fails the CRC");
            File.Move(path, Path.Combine(_dir, "regions", "r.1.1.egr"));
            Assert.Throws<InvalidDataException>(() => WorldSave.Read(_dir), "a file named for another cell");
        }

        [Test]
        public void ThePlayerFileRoundTripsAndRefusesCorruption()
        {
            SavedPlayer p = Player("Ngā mihi", 12.5, -7.25, wading: true);
            byte[] bytes = PlayerFile.Encode(p);
            SavedPlayer back = PlayerFile.Decode(bytes);
            Assert.That(back.Name, Is.EqualTo("Ngā mihi"));
            Assert.That(back.Body.East, Is.EqualTo(12.5));
            Assert.That(back.Body.Wading, Is.True);
            Assert.That(back.SavedTick, Is.EqualTo(5L));
            bytes[10] ^= 1;
            Assert.Throws<InvalidDataException>(() => PlayerFile.Decode(bytes));
        }
    }
}
