using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using EarthGame.Engine;
using EarthGame.Protocol;
using EarthGame.Server;
using NUnit.Framework;

namespace EarthGame.Tests.Server
{
    /// <summary>The entity save (M1.3 promise 4): region files by cell, the player file's second version, the digest, the save law.</summary>
    public sealed class RegionSaveTests
    {
        private static readonly Region Fixture = new Region("fixture", "Fixture", Region.Bherwerre.CentreLatitudeDeg,
            Region.Bherwerre.CentreLongitudeDeg, TestRasters.MadeExtentM, 237, 8.0);
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
            SavedPlayer p = default;
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
            List<CarrierRecord> carriers = new List<CarrierRecord>();
            foreach (SavedPlayer p in players)
            {
                bodies.Add(new KeyValuePair<string, MoverState>(p.Name, p.Body));
                carriers.Add(new CarrierRecord { Name = p.Name, Hand = p.Hand, Things = p.Carried });
            }
            return WorldDigest.World(w, bodies, carriers);
        }

        [Test]
        public void AWorldSavedWithAnimalsRoundItsFoundersIsTheWorldSavedWithoutThem()
        {
            CapacitySquares capacity = new CapacitySquares(TestRasters.MadeExtentM);
            capacity.Add(AnimalSpecies.EasternGreyKangaroo, TestRasters.FromLaw(TestRasters.MadeSide, TestRasters.MadeCellM, TestRasters.MadeExtentM, "capacity_roo", (row, col) => 30f, CapacitySquares.Unit));
            capacity.Add(AnimalSpecies.PiedOystercatcher, TestRasters.FromLaw(TestRasters.MadeSide, TestRasters.MadeCellM, TestRasters.MadeExtentM, "capacity_bird", (row, col) => 10f, CapacitySquares.Unit));
            WorldState w = new WorldState(1347UL, Fixture, Fixture.WakeClock(), _terrain, capacity: capacity);
            w.SpawnItem(DefinitionCatalogue.Cobble, 310, -305);
            SavedPlayer[] players = { Player("William", 300, -300) };
            w.InterestPoints.Add(players[0].Body.Feet);
            w.Step(0.05);
            Assert.That(w.Entities.Transient.Count, Is.GreaterThan(0), "animals stand round the founder");
            string named = Digest(w, players);
            string with = Path.Combine(_dir, "with"), without = Path.Combine(_dir, "without");
            WorldSave.Write(with, w, players, Now);

            w.InterestPoints.Clear();
            ((AnimalStandUp)w.Systems[1]).Refresh(w);
            w.Entities.EndTick();
            Assert.That(w.Entities.Transient.Count, Is.Zero, "with the founder gone, every one is taken away");
            Assert.That(Digest(w, players), Is.EqualTo(named), "and the world's name never named them");
            WorldSave.Write(without, w, players, Now);

            string[] files = Directory.GetFiles(with, "*", SearchOption.AllDirectories);
            Assert.That(Directory.GetFiles(without, "*", SearchOption.AllDirectories).Length, Is.EqualTo(files.Length), "the same files");
            foreach (string file in files)
                Assert.That(File.ReadAllBytes(Path.Combine(without, file.Substring(with.Length + 1))), Is.EqualTo(File.ReadAllBytes(file)), "the same bytes: " + file);
            WorldSaveInfo info = WorldSave.Read(with);
            Assert.That(info.Entities.Count, Is.EqualTo(1), "the cobble alone");
            Assert.That(info.NextEntityId, Is.EqualTo(2UL), "and the count the cobble moved it to");
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
                ("a carried thing", null, p => { p.Carried = new[] { new CarriedThing { Id = 9, Definition = DefinitionCatalogue.Stick, Place = 1 } }; p.Hand = 1; return p; }),
                ("a thing taken from the ground", w => w.Taken.Take(new LyingThing(1, 1, StandLayout.Kind.Stick, 0)), null),
                ("the hand", null, p => { p.Hand = 3; return p; }),
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
            List<SavedEntity> ok = RegionFile.Decode(bytes, out int cx, out int cz, out _);
            Assert.That((cx, cz), Is.EqualTo((2, 2)));
            Assert.That(ok.Count, Is.EqualTo(2));
            byte[] corrupt = (byte[])bytes.Clone();
            corrupt[RegionFile.HeaderBytes + 20] ^= 0xFF;
            Assert.Throws<InvalidDataException>(() => RegionFile.Decode(corrupt, out _, out _, out _), "a flipped byte fails the CRC");
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

        [Test]
        public void WhatIsCarriedRoundTripsAndAVersionTwoFileIsStillRead()
        {
            SavedPlayer p = Player("William", 300, -300);
            p.Hand = 4;
            p.Carried = new[]
            {
                new CarriedThing { Id = 3, Definition = DefinitionCatalogue.Cobble, SpawnTick = 50, Place = 1 },
                new CarriedThing { Id = 11, Definition = DefinitionCatalogue.Stick, SpawnTick = 60, Place = 4 },
            };
            p.WaterLoss = 0.0625;
            p.CoreDeficitC = 1.75;
            SavedPlayer back = PlayerFile.Decode(PlayerFile.Encode(p));
            Assert.That(back.WaterLoss, Is.EqualTo(0.0625), "the water lost rides in the file since version 4 (FP.1)");
            Assert.That(back.CoreDeficitC, Is.EqualTo(1.75), "and how far below normal the core was since version 5 (FP.2)");
            Assert.That(back.Hand, Is.EqualTo((byte)4));
            Assert.That(back.Carried.Length, Is.EqualTo(2));
            Assert.That(back.Carried[1].Id, Is.EqualTo(11UL));
            Assert.That(back.Carried[1].Definition, Is.SameAs(DefinitionCatalogue.Stick));
            Assert.That(back.Carried[1].SpawnTick, Is.EqualTo(60L), "the file keeps what the wire does not");
            Assert.That(back.Carried[1].Place, Is.EqualTo((byte)4));

            // Version 2, as M1.3 wrote it: no hands.
            PacketWriter w = new PacketWriter(64);
            foreach (char c in "EG2P") w.WriteByte((byte)c);
            w.WriteUInt16(2);
            w.WriteString("Old");
            w.WriteDouble(300.0);
            w.WriteDouble(12.0);
            w.WriteDouble(-300.0);
            w.WriteSingle(90f);
            w.WriteSingle(-3f);
            w.WriteByte(1 | 4);
            w.WriteInt64(9);
            SavedPlayer old = PlayerFile.Decode(WithCrc(w.Written.ToArray()));
            Assert.That(old.Name, Is.EqualTo("Old"));
            Assert.That(old.Body.Stance, Is.EqualTo(Stance.Crouching));
            Assert.That(old.SavedTick, Is.EqualTo(9L));
            Assert.That(old.Carried, Is.Null, "version 2 carried nothing");
            Assert.That(old.WaterLoss, Is.EqualTo(0.0), "a full body, the file saying nothing of water");
            Assert.That(old.CoreDeficitC, Is.EqualTo(0.0), "a normal core, the file saying nothing of it");
            Assert.That(old.Hand, Is.EqualTo((byte)0));
        }

        [Test]
        public void AFolderThatCarriesWhatNoWorldCouldIsRefused()
        {
            SavedPlayer p = Player("William", 300, -300);
            p.Hand = 1;
            p.Carried = new[] { new CarriedThing { Id = 3, Definition = DefinitionCatalogue.Stick, SpawnTick = 50, Place = 1 } };
            byte[] unknown = Rekeyed(PlayerFile.Encode(p), "item/stick", "item/stock");
            Assert.That(() => PlayerFile.Decode(unknown), Throws.TypeOf<InvalidDataException>().With.Message.Contains("item/stock"));

            WorldState w = Make();
            p.Carried[0].Id = 2;
            WorldSave.Write(Path.Combine(_dir, "both"), w, new[] { p }, Now);
            Assert.That(() => WorldSave.Read(Path.Combine(_dir, "both")), Throws.TypeOf<InvalidDataException>().With.Message.Contains("entity 2"),
                "a thing lying in the world and in the hands at once");
            p.Carried[0].Id = 40;
            WorldSave.Write(Path.Combine(_dir, "ahead"), w, new[] { p }, Now);
            Assert.That(() => WorldSave.Read(Path.Combine(_dir, "ahead")), Throws.TypeOf<InvalidDataException>().With.Message.Contains("entity 40"),
                "an id the world has not allocated");
        }

        /// <summary>The made coast's loose layer, with the same things on every cell.</summary>
        private static RegionRaster Loose(int sticks, int cobbles) =>
            TestRasters.FromCodes(TestRasters.MadeSide, TestRasters.MadeCellM, TestRasters.MadeExtentM, "made_loose", "loose", (row, col) => LooseCodes.Pack(sticks, cobbles), null);

        /// <summary>What is taken from the ground rides the region files as layer diffs and is named by the digest (M1.5b promise 3).</summary>
        [Test]
        public void TakingsRideTheRegionFilesAndEnterTheDigest()
        {
            RegionRaster loose = Loose(3, 2);
            WorldState w = new WorldState(1347UL, Fixture, Fixture.WakeClock(), _terrain, 0, null, null, null, null, loose);
            w.SpawnItem(DefinitionCatalogue.Cobble, -500, -500);
            string before = Digest(w, new SavedPlayer[0]);
            Assert.That(w.Taken.Take(new LyingThing(110, 110, StandLayout.Kind.Stick, 2)), Is.True);
            Assert.That(w.Taken.Take(new LyingThing(3, 150, StandLayout.Kind.Cobble, 1)), Is.True);
            string expected = Digest(w, new SavedPlayer[0]);
            Assert.That(expected, Is.Not.EqualTo(before), "a taking enters the digest");
            WorldSave.Write(_dir, w, new SavedPlayer[0], Now);
            Assert.That(File.Exists(Path.Combine(_dir, "regions", "r.2.3.egr")), Is.True, "the cell (700, 770) lies in, a file of takings alone");
            Assert.That(File.Exists(Path.Combine(_dir, "regions", "r.2.0.egr")), Is.True, "and (300, -300)'s");

            WorldSaveInfo info = WorldSave.Read(_dir);
            Assert.That(info.Digest, Is.EqualTo(expected));
            Assert.That(info.Taken.Count, Is.EqualTo(2));
            WorldState back = WorldSave.Restore(info, _terrain, Fixture, null, null, null, loose);
            Assert.That(Digest(back, new SavedPlayer[0]), Is.EqualTo(expected), "the same name after the round trip");
            Assert.That(back.Taken.IsTaken(new LyingThing(110, 110, StandLayout.Kind.Stick, 2)), Is.True);
            Assert.That(LyingThings.TryFind(back, new LyingThing(110, 110, StandLayout.Kind.Stick, 1), out _), Is.True, "the stick beside it is still there");
        }

        [Test]
        public void AVersionOneRegionFileIsStillReadAndATakingNoCellHeldIsRefused()
        {
            // Version 1, as M1.3 wrote it: the same header and records, and no diffs.
            byte[] v1 = RegionFile.Encode(1, 2, new[] { SavedEntity.Of(Make().Entities.All[0]) });
            v1[4] = 1;
            v1[5] = 0;
            List<SavedEntity> read = RegionFile.Decode(v1, out int cx, out int cz, out List<LooseTaken.Cell> taken);
            Assert.That((cx, cz), Is.EqualTo((1, 2)));
            Assert.That(read.Count, Is.EqualTo(1));
            Assert.That(taken, Is.Empty);
            byte[] withDiff = RegionFile.Encode(1, 2, new SavedEntity[0], new[] { new LooseTaken.Cell { Row = 1, Col = 1, Sticks = 1 } });
            withDiff[4] = 1;
            Assert.Throws<InvalidDataException>(() => RegionFile.Decode(withDiff, out _, out _, out _), "version 1 had no diffs");

            // A taking of a stick its cell never held: a folder this world's server did not write.
            WorldState w = new WorldState(1347UL, Fixture, Fixture.WakeClock(), _terrain, 0, null, null, null, null, Loose(1, 0));
            w.Taken.Take(new LyingThing(110, 110, StandLayout.Kind.Stick, 0));
            WorldSave.Write(_dir, w, new SavedPlayer[0], Now);
            WorldSaveInfo info = WorldSave.Read(_dir);
            Assert.That(() => WorldSave.Restore(info, _terrain, Fixture, null, null, null, Loose(0, 0)),
                Throws.TypeOf<InvalidDataException>().With.Message.Contains("held 0 sticks"));
            Assert.That(() => WorldSave.Restore(info, _terrain, Fixture), Throws.TypeOf<InvalidDataException>().With.Message.Contains("does not have"),
                "nor from a world with no loose layer at all");
        }

        /// <summary>A player file's body with its CRC appended, as <see cref="PlayerFile"/> lays it out.</summary>
        private static byte[] WithCrc(byte[] body)
        {
            uint crc = Crc32.Compute(body);
            byte[] file = new byte[body.Length + 4];
            Buffer.BlockCopy(body, 0, file, 0, body.Length);
            file[body.Length] = (byte)crc;
            file[body.Length + 1] = (byte)(crc >> 8);
            file[body.Length + 2] = (byte)(crc >> 16);
            file[body.Length + 3] = (byte)(crc >> 24);
            return file;
        }

        /// <summary>A player file with one key's bytes swapped for another's of the same length, and its CRC made good again.</summary>
        private static byte[] Rekeyed(byte[] file, string from, string to)
        {
            byte[] body = new byte[file.Length - 4];
            Buffer.BlockCopy(file, 0, body, 0, body.Length);
            byte[] a = Encoding.UTF8.GetBytes(from), b = Encoding.UTF8.GetBytes(to);
            Assert.That(b.Length, Is.EqualTo(a.Length));
            for (int i = 0; i + a.Length <= body.Length; i++)
            {
                bool match = true;
                for (int j = 0; j < a.Length && match; j++) match = body[i + j] == a[j];
                if (!match) continue;
                Buffer.BlockCopy(b, 0, body, i, b.Length);
                return WithCrc(body);
            }
            throw new AssertionException("'" + from + "' is not in the file");
        }
    }
}
