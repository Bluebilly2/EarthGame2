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
            TestRasters.MadeExtentM, 237, 8);
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
            made.Dispose();
            using var back = WorldPreparation.Load(_world, Path.Combine(_root, "absent"), Region, 999, Now, null, CancellationToken.None);
            Assert.That(back.Saved, Is.Not.Null);
            Assert.That(back.World.Seed, Is.EqualTo(1347));
            Assert.That(back.World.Terrain.Raster.Sha256, Is.EqualTo(made.World.Terrain.Raster.Sha256));
            Assert.That(back.Checksums, Is.EquivalentTo(made.Checksums));
        }

        [Test]
        public void TheSquaresFeedTheMeanOfTheWorldsOwnCapacityLayersMadeOrRestored()
        {
            Bake();
            var made = WorldPreparation.Load(_world, _data, Region, 1347, Now, null, CancellationToken.None);
            made.Dispose();
            using var back = WorldPreparation.Load(_world, Path.Combine(_root, "absent"), Region, 1347, Now, null, CancellationToken.None);
            Assert.That(back.Saved, Is.Not.Null, "the second load restores");
            int fed = 0;
            foreach (AnimalSpecies species in AnimalSpecies.All)
            {
                // Read back by hand: each cell's centre, 10 m apart from the north-west corner, in the 100 m square it lies in.
                RegionRaster layer = RegionRaster.Load(Path.Combine(_world, "layers", "capacity_" + species.Name.ToLowerInvariant() + ".json"));
                var sums = new Dictionary<(int, int), (double Sum, int Count)>();
                for (int row = 0; row < layer.Height; row++)
                    for (int col = 0; col < layer.Width; col++)
                    {
                        double east = -layer.ExtentM / 2 + col * layer.CellM, north = layer.ExtentM / 2 - row * layer.CellM;
                        var square = ((int)Math.Floor(east / 100.0), (int)Math.Floor(north / 100.0));
                        sums.TryGetValue(square, out var s);
                        sums[square] = (s.Sum + layer[row, col], s.Count + 1);
                    }
                Assert.That(sums.Count, Is.EqualTo(17 * 17), "the made coast's squares, 800 m either side of its centre");
                foreach (var pair in sums)
                {
                    double mean = pair.Value.Sum / pair.Value.Count;
                    if (mean > 0.0) fed++;
                    Assert.That(made.World.Capacity.PerKm2(species, pair.Key.Item1, pair.Key.Item2), Is.EqualTo(mean).Within(1e-9), species + " square " + pair.Key);
                    Assert.That(back.World.Capacity.PerKm2(species, pair.Key.Item1, pair.Key.Item2), Is.EqualTo(mean).Within(1e-9), "restored, " + species + " square " + pair.Key);
                }
            }
            Assert.That(fed, Is.GreaterThan(0), "some square feeds something");
        }

        [Test]
        public void ProgressDoesNotChangeTheWorld()
        {
            Bake();
            using var a = WorldPreparation.Load(_world, _data, Region, 1347, Now, _ => { }, CancellationToken.None);
            using var b = WorldPreparation.Load(Path.Combine(_root, "second"), _data, Region, 1347, Now, null, CancellationToken.None);
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
            WorldPreparation.Load(_world, _data, Region, 1347, Now, null, CancellationToken.None).Dispose();
            string savedTerrain = Path.Combine(_world, "layers", "heights.json");
            if (missing) File.Delete(savedTerrain);
            else File.WriteAllText(savedTerrain, "broken");
            Assert.Throws(missing ? typeof(FileNotFoundException) : typeof(InvalidDataException), () => WorldPreparation.Load(_world, _data, Region, 1347, Now, null, CancellationToken.None),
                "an intact bake must not hide a corrupt world terrain");
        }

        /// <summary>
        /// A world owns the region it is set in (2026-09-10). The caller passes the region it was launched with,
        /// so without this a world of one region opened as another of the same extent would come back on another
        /// coast at the same local metres, and only its census would show it.
        /// </summary>
        [Test]
        public void AWorldOfAnotherRegionIsRefusedNotReinterpreted()
        {
            Bake();
            WorldPreparation.Load(_world, _data, Region, 1347, Now, null, CancellationToken.None).Dispose();
            Region elsewhere = new Region("elsewhere", "Elsewhere", -35.14, 150.675,
                TestRasters.MadeExtentM, 237, 8);
            InvalidDataException refused = Assert.Throws<InvalidDataException>(
                () => WorldPreparation.Load(_world, _data, elsewhere, 1347, Now, null, CancellationToken.None));
            Assert.That(refused.Message, Does.Contain("fixture").And.Contain("elsewhere"), "the refusal names both");
            using (WorldPreparation.Result own = WorldPreparation.Load(_world, _data, Region, 1347, Now, null, CancellationToken.None))
                Assert.That(own.Saved, Is.Not.Null, "and its own region still opens it");
        }

        /// <summary>
        /// A world one program holds is refused to a second before the second reads or changes anything (M1.3d): what the
        /// first one's save has written aside stays, where the second's recovery of the folder would have deleted it.
        /// </summary>
        [Test]
        public void AWorldOneProgramHoldsIsRefusedToASecondBeforeItTouchesAnything()
        {
            Bake();
            WorldPreparation.Load(_world, _data, Region, 1347, Now, null, CancellationToken.None).Dispose();
            string aside = Path.Combine(_world, WorldSave.WorldFile + ".part");
            using (WorldPreparation.Result first = WorldPreparation.Load(_world, _data, Region, 1347, Now, null, CancellationToken.None))
            {
                File.WriteAllText(aside, "a save the first program is writing");
                IOException refused = Assert.Throws<IOException>(() => WorldPreparation.Load(_world, _data, Region, 1347, Now, null, CancellationToken.None));
                Assert.That(refused.Message, Does.Contain("already open"));
                Assert.That(File.Exists(aside), Is.True, "the second program touched nothing of the first one's save");
            }
            using (WorldPreparation.Result again = WorldPreparation.Load(_world, _data, Region, 1347, Now, null, CancellationToken.None))
            {
                Assert.That(again.Saved, Is.Not.Null, "once the first lets it go, the world opens");
                Assert.That(File.Exists(aside), Is.False, "and what was left aside is cleared");
            }
            Assert.That(File.Exists(Path.Combine(_world, WorldLock.FileName)), Is.False, "and nothing of the hold is left in the folder");
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
