using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using EarthGame.Engine;
using EarthGame.Server;
using NUnit.Framework;

namespace EarthGame.Tests.Server
{
    public sealed class WorldPreparationTests
    {
        private string _root, _data, _world;
        private static readonly Region Region = new Region("fixture", "Fixture", -35.14, 150.675,
            TestRasters.MadeExtentM, 237, 8, -35.14, 150.675);
        private const string Now = "2026-09-10T00:00:00Z";

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "EarthGame2.Tests", "preparation", Guid.NewGuid().ToString("N"));
            _data = Path.Combine(_root, "data");
            _world = Path.Combine(_root, "world");
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }

        private void Bake()
        {
            RegionRaster raster = TestRasters.MadeCoast();
            RegionRaster.Write(_data, "heights", raster, "heights", "f32", 1, "m",
                raster.Values.ToArray(), "made coast", "test", Now);
        }

        [Test]
        public void NewWorldReportsRealStagesAndContinueNeedsNoBake()
        {
            Bake();
            var stages = new List<string>();
            var made = WorldPreparation.Load(_world, _data, Region, 1347, Now, stages.Add, CancellationToken.None);
            Assert.That(stages, Does.Contain("Tracing drainage"));
            Assert.That(stages, Does.Contain("Saved water"));
            Assert.That(stages[stages.Count - 1], Is.EqualTo("World ready"));
            Assert.That(made.Saved, Is.Null);
            Assert.That(made.World.Tick, Is.Zero);
            Assert.That(made.World.Wake.HasValue, Is.True);
            var back = WorldPreparation.Load(_world, Path.Combine(_root, "absent"), Region, 999, Now, null, CancellationToken.None);
            Assert.That(back.Saved, Is.Not.Null);
            Assert.That(back.World.Seed, Is.EqualTo(1347));
            Assert.That(back.World.Terrain.Raster.Sha256, Is.EqualTo(made.World.Terrain.Raster.Sha256));
            Assert.That(back.Checksums, Is.EquivalentTo(made.Checksums));
        }

        [Test]
        public void ProgressDoesNotChangeTheWorld()
        {
            Bake();
            var a = WorldPreparation.Load(_world, _data, Region, 1347, Now, _ => { }, CancellationToken.None);
            var b = WorldPreparation.Load(Path.Combine(_root, "second"), _data, Region, 1347, Now, null, CancellationToken.None);
            Assert.That(a.Checksums, Is.EquivalentTo(b.Checksums));
            Assert.That(a.Census, Is.EqualTo(b.Census));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void MissingDataAndCorruptSavedTerrainAreErrors(bool missing)
        {
            Assert.Throws<FileNotFoundException>(() => WorldPreparation.Load(_world, _data, Region, 1347, Now, null, CancellationToken.None));
            Assert.That(WorldSave.Exists(_world), Is.False);
            Bake();
            WorldPreparation.Load(_world, _data, Region, 1347, Now, null, CancellationToken.None);
            string savedTerrain = Path.Combine(_world, "layers", "heights.json");
            if (missing) File.Delete(savedTerrain);
            else File.WriteAllText(savedTerrain, "broken");
            Assert.Throws(missing ? typeof(FileNotFoundException) : typeof(InvalidDataException), () => WorldPreparation.Load(_world, _data, Region, 1347, Now, null, CancellationToken.None),
                "an intact bake must not hide a corrupt world terrain");
        }

        [TestCase("Tracing drainage")]
        [TestCase("Saved heights")]
        public void CancellationAtAStageLeavesNoLoadablePartialWorld(string stopAt)
        {
            Bake();
            using var cancel = new CancellationTokenSource();
            Assert.Throws<OperationCanceledException>(() => WorldPreparation.Load(_world, _data, Region, 1347, Now,
                stage => { if (stage == stopAt) cancel.Cancel(); }, cancel.Token));
            Assert.That(WorldSave.Exists(_world), Is.False);
        }
    }
}
