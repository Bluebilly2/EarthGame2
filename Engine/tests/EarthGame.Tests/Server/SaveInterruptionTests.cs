using System;
using System.Collections.Generic;
using System.IO;
using EarthGame.Engine;
using EarthGame.Server;
using NUnit.Framework;

namespace EarthGame.Tests.Server
{
    /// <summary>
    /// A save survives its interruption (M1.3b promises 1 to 5): stopped after any one of its steps, the folder reads as the
    /// save before it or the save being made, whole, with nothing left over; a replacement cut short before the record is
    /// placed leaves the save before; a record that names a damaged file is refused with the file's name; and a world whose
    /// first save a crash stopped after its record is still a world.
    /// </summary>
    public sealed class SaveInterruptionTests
    {
        private static readonly Region Fixture = new Region("fixture", "Fixture", Region.Bherwerre.CentreLatitudeDeg,
            Region.Bherwerre.CentreLongitudeDeg, TestRasters.MadeExtentM, 237, 8.0);
        private const string Before = "2026-09-13T10:00:00Z", After = "2026-09-13T10:00:30Z";

        private string _dir;
        private Heightfield _terrain;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "EarthGame2.Tests", "interrupted", Guid.NewGuid().ToString("N"));
            _terrain = new Heightfield(TestRasters.MadeCoast());
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        /// <summary>What a test throws where a crash would stop the process: nothing of the save runs after it.</summary>
        private sealed class Crash : Exception
        {
            public Crash(string step) : base("stopped after " + step) { }
        }

        /// <summary>A world saved once, and the same world changed since, with the names of both.</summary>
        private sealed class Story
        {
            public WorldState World;
            public SavedPlayer[] Now;
            public string DigestThen, DigestNow;
        }

        /// <summary>
        /// A world saved into a folder, then changed the ways that part a save's files: William picks up the cobble lying alone
        /// in the south-west cell, which empties; the stick crosses into a cell that held nothing; and a cobble is set down in
        /// another such cell.
        /// </summary>
        private Story Tell(string savedFolder)
        {
            WorldState w = new WorldState(1347UL, Fixture, Fixture.WakeClock(), _terrain);
            Entity alone = w.SpawnItem(DefinitionCatalogue.Cobble, -500, -500);
            Entity stick = w.SpawnItem(DefinitionCatalogue.Stick, 300, 300);
            w.SpawnItem(DefinitionCatalogue.Cobble, 310, 305, null, 45f);
            w.Step(0.05);
            SavedPlayer[] then = { Player("William", 300, -300) };
            Story story = new Story { DigestThen = Digest(w, then) };
            WorldSave.Write(savedFolder, w, then, Before);

            SavedPlayer carrying = Player("William", 300, -300);
            carrying.Hand = 1;
            carrying.Carried = new[] { new CarriedThing { Id = alone.Id.Value, Definition = alone.Definition, SpawnTick = alone.SpawnTick, Place = 1 } };
            w.Entities.Take(alone);
            stick.Move(new Double3(-300.0, w.GroundAt(-300.0, 300.0), 300.0), w.Tick);
            w.SpawnItem(DefinitionCatalogue.Cobble, 500, -500);
            w.Step(0.05);
            story.World = w;
            story.Now = new[] { carrying };
            story.DigestNow = Digest(w, story.Now);
            Assert.That(story.DigestNow, Is.Not.EqualTo(story.DigestThen), "the story must change the world");
            return story;
        }

        [Test]
        public void ASaveStoppedAfterAnyStepReadsAsTheSaveBeforeOrTheSaveMadeWhole()
        {
            string then = Path.Combine(_dir, "then");
            Story story = Tell(then);

            List<string> steps = new List<string>();
            string whole = Copy(then, "whole");
            WorldSave.Write(whole, story.World, story.Now, After, null, steps.Add);
            Assert.That(ReadBack(whole), Is.EqualTo(story.DigestNow), "the save made whole is the world as it is now");
            int placed = steps.IndexOf("placed " + WorldSave.CommitFile);
            Assert.That(placed, Is.GreaterThan(0), "the files are written aside before the record is placed: " + string.Join(", ", steps));
            Assert.That(steps.Count, Is.GreaterThan(placed + 1), "and put in place after it");
            Assert.That(steps, Does.Contain("removed regions/r.0.0.egr"), "the emptied cell's file goes, inside the save");

            for (int stop = 0; stop < steps.Count; stop++)
            {
                string folder = Copy(then, "stopped-" + stop);
                int done = 0, at = stop;
                Assert.Throws<Crash>(() => WorldSave.Write(folder, story.World, story.Now, After, null, step =>
                {
                    if (done++ == at) throw new Crash(step);
                }));
                bool committed = stop >= placed;
                Assert.That(ReadBack(folder), Is.EqualTo(committed ? story.DigestNow : story.DigestThen),
                    "stopped after '" + steps[stop] + "', the folder must read as " + (committed ? "the save being made" : "the save before it"));
                Assert.That(LeftOver(folder), Is.Empty, "stopped after '" + steps[stop] + "', nothing of the save may be left once the folder is read");
            }
        }

        [Test]
        public void AReplacementCutShortBeforeTheRecordIsPlacedLeavesTheSaveBefore()
        {
            string then = Path.Combine(_dir, "then");
            Story story = Tell(then);
            List<string> steps = new List<string>();
            WorldSave.Write(Copy(then, "whole"), story.World, story.Now, After, null, steps.Add);
            int lastAside = steps.IndexOf("placed " + WorldSave.CommitFile) - 1;

            string folder = Copy(then, "torn");
            int done = 0;
            Assert.Throws<Crash>(() => WorldSave.Write(folder, story.World, story.Now, After, null, step =>
            {
                if (done++ == lastAside) throw new Crash(step);
            }));
            string written = steps[lastAside].Substring("wrote ".Length);
            string torn = Path.Combine(folder, written.Replace('/', Path.DirectorySeparatorChar));
            byte[] bytes = File.ReadAllBytes(torn);
            File.WriteAllBytes(torn, new ArraySegment<byte>(bytes, 0, bytes.Length / 2).ToArray());

            Assert.That(ReadBack(folder), Is.EqualTo(story.DigestThen), "a save that never placed its record leaves the save before it, whatever it wrote aside");
            Assert.That(LeftOver(folder), Is.Empty);
        }

        [Test]
        public void ARecordThatNamesADamagedFileIsRefusedWithTheFilesName()
        {
            string then = Path.Combine(_dir, "then");
            Story story = Tell(then);
            string folder = Copy(then, "damaged");
            Assert.Throws<Crash>(() => WorldSave.Write(folder, story.World, story.Now, After, null, step =>
            {
                if (step == "placed " + WorldSave.CommitFile) throw new Crash(step);
            }));
            string part = Path.Combine(folder, WorldSave.WorldFile + ".part");
            byte[] bytes = File.ReadAllBytes(part);
            bytes[bytes.Length / 2] ^= 0x20;
            File.WriteAllBytes(part, bytes);

            Assert.That(() => WorldSave.Read(folder), Throws.TypeOf<InvalidDataException>().With.Message.Contains(WorldSave.WorldFile + ".part"),
                "a damaged file the record names is refused, never read as the world");
        }

        [Test]
        public void AWorldWhoseFirstSaveWasStoppedAfterItsRecordIsStillAWorld()
        {
            WorldState w = new WorldState(1347UL, Fixture, Fixture.WakeClock(), _terrain);
            w.SpawnItem(DefinitionCatalogue.Cobble, -500, -500);
            w.Step(0.05);
            SavedPlayer[] players = { Player("William", 300, -300) };
            string folder = Path.Combine(_dir, "first");
            Assert.Throws<Crash>(() => WorldSave.Write(folder, w, players, Before, null, step =>
            {
                if (step == "placed " + WorldSave.CommitFile) throw new Crash(step);
            }));
            Assert.That(File.Exists(Path.Combine(folder, WorldSave.WorldFile)), Is.False, "the world file was never put in place");
            Assert.That(WorldSave.Exists(folder), Is.True, "and the folder is still a world, not one to make a new world over");
            Assert.That(ReadBack(folder), Is.EqualTo(Digest(w, players)), "which reads as the save that was being made");
        }

        private string ReadBack(string folder)
        {
            WorldSaveInfo info = WorldSave.Read(folder);
            WorldState restored = WorldSave.Restore(info, _terrain, Fixture);
            return Digest(restored, new List<SavedPlayer>(info.Players.Values));
        }

        private string Copy(string from, string name)
        {
            string to = Path.Combine(_dir, name);
            foreach (string file in Directory.GetFiles(from, "*", SearchOption.AllDirectories))
            {
                string target = Path.Combine(to, file.Substring(from.Length + 1));
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                File.Copy(file, target);
            }
            return to;
        }

        private static List<string> LeftOver(string folder)
        {
            List<string> left = new List<string>();
            foreach (string file in Directory.GetFiles(folder, "*", SearchOption.AllDirectories))
                if (file.EndsWith(".part", StringComparison.Ordinal) || Path.GetFileName(file) == WorldSave.CommitFile) left.Add(file);
            return left;
        }

        private SavedPlayer Player(string name, double east, double north)
        {
            SavedPlayer p = default;
            p.Name = name;
            p.Body = MoverState.AtRest(east, _terrain.HeightAt(east, north), north);
            p.Body.Grounded = true;
            p.YawDeg = 12f;
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
                carriers.Add(new CarrierRecord { Name = p.Name, Hand = p.Hand, Things = p.Carried ?? Array.Empty<CarriedThing>() });
            }
            return WorldDigest.World(w, bodies, carriers);
        }
    }
}
