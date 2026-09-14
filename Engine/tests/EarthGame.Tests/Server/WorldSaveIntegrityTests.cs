using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using EarthGame.Engine;
using EarthGame.Server;
using NUnit.Framework;

namespace EarthGame.Tests.Server
{
    public sealed class WorldSaveIntegrityTests
    {
        private string _root, _world;
        private const string Now = "2026-09-14T00:00:00Z";
        private static readonly Region Region = new Region("fixture", "Fixture", -35.14, 150.675, TestRasters.MadeExtentM, 237, 8);

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "eg2-integrity-" + Guid.NewGuid().ToString("N"));
            _world = Path.Combine(_root, "world");
        }

        [TearDown]
        public void CleanUp()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }

        [TestCase(0UL)]
        [TestCase(9007199254740991UL)]
        [TestCase(9007199254740992UL)]
        [TestCase(9007199254740993UL)]
        [TestCase(18446744073709551615UL)]
        public void ASeedIsTheSameAfterSaveReadAndRestore(ulong seed)
        {
            var original = new WorldState(seed, Region, Region.WakeClock());
            WorldSave.Write(_world, original, null, Now);
            var saved = WorldSave.Read(_world);
            Assert.That(saved.Seed, Is.EqualTo(seed), "every digit of the seed survives");
            var restored = WorldSave.Restore(saved, null, Region);
            Assert.That(restored.Seed, Is.EqualTo(seed));
            WorldSave.Write(_world, restored, null, Now);
            Assert.That(WorldSave.Read(_world).Seed, Is.EqualTo(seed), "a second save stays exact");
        }

        [Test]
        public void LargeTickAndNextEntityIdSurvive()
        {
            const long tick = 9007199254740993L;
            const ulong next = 9007199254740995UL;
            var original = new WorldState(1, Region, Region.WakeClock(), null, tick);
            original.Entities.SetNextId(next);
            WorldSave.Write(_world, original, null, Now);
            var saved = WorldSave.Read(_world);
            Assert.That(saved.Tick, Is.EqualTo(tick));
            Assert.That(saved.NextEntityId, Is.EqualTo(next));
        }

        private void Create()
        {
            var made = WorldCreation.Create(_world, Region, 1347, TestRasters.MadeCoast(), Now);
            WorldSave.Write(_world, new WorldState(1347, Region, Region.WakeClock()), null, Now, made.Checksums);
        }

        private WorldPreparation.Result Load()
            => WorldPreparation.Load(_world, Path.Combine(_root, "no-bake"), Region, 99, Now, null, CancellationToken.None);

        private string Header(string name) => Path.Combine(_world, "layers", name + ".json");

        [TestCase("stand")]
        [TestCase("cover")]
        [TestCase("loose")]
        [TestCase("stone")]
        [TestCase("surface")]
        [TestCase("soil_depth")]
        public void EveryPromisedLayerIsRequired(string name)
        {
            Create();
            File.Delete(Header(name));
            Assert.That(() => Load(), Throws.TypeOf<FileNotFoundException>().With.Message.Contains(name));
        }

        [Test]
        public void MissingBothWaterLayersIsNotALegacyWorld()
        {
            Create();
            File.Delete(Header("water"));
            File.Delete(Header("surface"));
            Assert.That(() => Load(), Throws.TypeOf<FileNotFoundException>());
        }

        [Test]
        public void MissingPromisedCapacityIsAnError()
        {
            Create();
            string name = WorldCreation.CapacityLayer(AnimalSpecies.All[0]);
            File.Delete(Header(name));
            Assert.That(() => Load(), Throws.TypeOf<FileNotFoundException>().With.Message.Contains(name));
        }

        [TestCase("stand")]
        [TestCase("soil_depth")]
        public void AReplacementWithItsOwnValidChecksumStillBelongsToAnotherWorld(string name)
        {
            Create();
            RegionRaster layer = RegionRaster.Load(Header(name));
            float[] changed = layer.Values.ToArray();
            changed[0] = changed[0] == 0 ? 1 : 0;
            RegionRaster.Write(Path.GetDirectoryName(Header(name)), name, layer, name, layer.Dtype, layer.Scale, layer.Unit,
                changed, "different world", "test", Now);
            var error = Assert.Throws<InvalidDataException>(() => Load());
            Assert.That(error.Message, Does.Contain(name).And.Contain("manifest"));
        }

        [TestCase("region", "elsewhere")]
        [TestCase("centre_lat", -34.0)]
        [TestCase("centre_lon", 151.0)]
        [TestCase("extent_m", 1700.0)]
        public void ALayerCannotQuietlyChangeItsGeographicFrame(string key, object value)
        {
            Create();
            JsonObject header = Json.ParseObject(File.ReadAllText(Header("stand")));
            header[key] = value;
            File.WriteAllText(Header("stand"), Json.Write(header));
            Assert.That(() => Load(), Throws.TypeOf<InvalidDataException>().With.Message.Contains("stand"));
        }

        [Test]
        public void AWorldBeforeOptionalLayersStillLoads()
        {
            Directory.CreateDirectory(_world);
            RegionRaster heights = TestRasters.MadeCoast();
            RegionRaster.Write(Path.Combine(_world, "layers"), "heights", heights, "heights", "f32", 1, "m",
                heights.Values.ToArray(), "legacy", "test", Now);
            WorldSave.Write(_world, new WorldState(1347, Region, Region.WakeClock()), null, Now);
            using var loaded = Load();
            Assert.That(loaded.World.Seed, Is.EqualTo(1347));
            Assert.That(loaded.World.Stand, Is.Null);
        }

        [Test]
        public void DifferentSamplingPitchStillDescribesTheSamePlace()
        {
            Create();
            var coarser = TestRasters.FromLaw(81, 20, TestRasters.MadeExtentM, "coarser", (r, c) => 1);
            RegionRaster.Write(Path.Combine(_world, "layers"), "soil_depth", coarser, "soil_depth", "u16", 0.01, "m",
                coarser.Values.ToArray(), "coarser", "test", Now);
            string worldFile = Path.Combine(_world, WorldSave.WorldFile);
            JsonObject world = Json.ParseObject(File.ReadAllText(worldFile));
            world.Object("layers")["soil_depth"] = RegionRaster.Load(Header("soil_depth")).Sha256;
            File.WriteAllText(worldFile, Json.Write(world));
            using var loaded = Load();
            Assert.That(loaded.World.Seed, Is.EqualTo(1347));
        }

        [Test]
        public void APrimaryBakeMustBelongToTheRequestedPlaceBeforeAnythingIsWritten()
        {
            var elsewhere = new Region("elsewhere", "Elsewhere", -34, 151, TestRasters.MadeExtentM, 237, 8);
            Assert.That(() => WorldCreation.Create(_world, elsewhere, 1, TestRasters.MadeCoast(), Now),
                Throws.TypeOf<InvalidDataException>());
            Assert.That(Directory.Exists(_world), Is.False);
        }
    }
}
