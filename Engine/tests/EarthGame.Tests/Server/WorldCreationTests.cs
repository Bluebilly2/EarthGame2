using System;
using System.IO;
using EarthGame.Engine;
using EarthGame.Server;
using NUnit.Framework;

namespace EarthGame.Tests.Server
{
    /// <summary>The world folder as the pipeline writes it (M1.2 promises 9 and 10): every layer, the wake, the census; byte-identical twice.</summary>
    public sealed class WorldCreationTests
    {
        private static readonly Region FixtureRegion = new Region("fixture", "Fixture", Region.Bherwerre.CentreLatitudeDeg,
            Region.Bherwerre.CentreLongitudeDeg, TestRasters.MadeExtentM, 237, 8.0, Region.Bherwerre.CentreLatitudeDeg, Region.Bherwerre.CentreLongitudeDeg);

        private string _dir;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "EarthGame2.Tests", "worlds", Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        [Test]
        public void CreationWritesEveryLayerTheWakeAndTheCensus()
        {
            RegionRaster heights = TestRasters.MadeCoast();
            WorldCreation.Result r = WorldCreation.Create(Path.Combine(_dir, "a"), FixtureRegion, 1347UL, heights, "2026-09-09T00:00:00Z", TestRasters.MadeWaterBodies());
            string[] expected = { "heights", "surface", "soil_depth", "wetness", "suitability", "water", "overstory", "understory", "topology", "cover", "stone", "shore_distance", "fresh_water_distance",
                                  "capacity_easterngreykangaroo", "capacity_piedoystercatcher", "capacity_superbfairywren",
                                  "stone_distance", "fibre_distance", "firewood_distance", "shelter_distance", "wake_score", "catchment" };
            foreach (string name in expected)
            {
                Assert.That(r.Layers.ContainsKey(name), Is.True, name + " was written");
                Assert.That(File.Exists(r.Layers[name]), Is.True, r.Layers[name]);
                Assert.That(r.Checksums[name].Length, Is.EqualTo(64));
            }
            Assert.That(r.Layers.Count, Is.EqualTo(expected.Length));
            RegionRaster water = RegionRaster.Load(r.Layers["water"]);
            Assert.That(water.IsIntegral, Is.True);
            Assert.That(water.Code(155, 80), Is.EqualTo((uint)WaterClass.Sea));
            Assert.That(water.Code(70, 80), Is.EqualTo((uint)WaterClass.Lake));
            RegionRaster soil = RegionRaster.Load(r.Layers["soil_depth"]);
            Assert.That(soil.Unit, Is.EqualTo("m"));
            Assert.That(soil.Scale, Is.EqualTo(0.01));
            RegionRaster floor = RegionRaster.Load(r.Layers["heights"]);
            Assert.That(floor[152, 80], Is.EqualTo(-1.5f).Within(1e-3), "the heights layer carries the sea floor");
            RegionRaster distance = RegionRaster.Load(r.Layers["fresh_water_distance"]);
            Assert.That(distance.Code(70, 80), Is.EqualTo(0u));
            RegionRaster catchment = RegionRaster.Load(r.Layers["catchment"]);
            Assert.That(catchment.Unit, Is.EqualTo("cells"));
            uint gathered = 0, lakeCells = 0;
            for (int row = 0; row < TestRasters.MadeSide; row++)
                for (int col = 0; col < TestRasters.MadeSide; col++)
                    if (water.Code(row, col) == (uint)WaterClass.Lake) { gathered += catchment.Code(row, col); lakeCells++; }
            Assert.That(gathered - lakeCells, Is.GreaterThan(700u), "the lake, a sink, gathers the ring around it (about 790 cells) and the plain above: " + gathered + " cells into " + lakeCells);
            Assert.That(r.Wake.Score, Is.GreaterThan(0.0));
            string census = File.ReadAllText(Path.Combine(_dir, "a", WorldCreation.CensusFile));
            Assert.That(census, Does.Contain("the region's stated wake is"));
            Assert.That(census, Does.Contain("Made Lake: "));
            Assert.That(census, Is.EqualTo(r.Census));
            RegionRaster surface = RegionRaster.Load(r.Layers["surface"]);
            Assert.That(surface[54, 120], Is.EqualTo(r.Computed.Bodies[0].LevelM).Within(1e-3));
            Assert.That(surface[155, 80], Is.EqualTo(0f));

            // What the folder's heights layer carries; that a missing or corrupt one is refused rather than
            // replaced by the bake is WorldPreparationTests'.
            Heightfield terrain = new Heightfield(RegionRaster.Load(r.Layers["heights"]));
            Assert.That(terrain.HeightAt(0.0, 0.0), Is.EqualTo(r.Computed.HeightsWithFloor[80 * TestRasters.MadeSide + 80]).Within(1e-6));
        }

        [Test]
        public void TwoCreationsOfTheSameWorldAreByteIdentical()
        {
            RegionRaster heights = TestRasters.MadeCoast();
            WorldCreation.Result a = WorldCreation.Create(Path.Combine(_dir, "a"), FixtureRegion, 1347UL, heights, "2026-09-09T00:00:00Z");
            WorldCreation.Result b = WorldCreation.Create(Path.Combine(_dir, "b"), FixtureRegion, 1347UL, heights, "2026-09-09T00:00:00Z");
            foreach (var pair in a.Checksums)
                Assert.That(b.Checksums[pair.Key], Is.EqualTo(pair.Value), pair.Key + " differs between two creations");
            Assert.That(b.Wake.Row, Is.EqualTo(a.Wake.Row));
            Assert.That(b.Wake.Col, Is.EqualTo(a.Wake.Col));
            Assert.That(b.Census, Is.EqualTo(a.Census));
            WorldCreation.Result c = WorldCreation.Create(Path.Combine(_dir, "c"), FixtureRegion, 99UL, heights, "2026-09-09T00:00:00Z");
            Assert.That(c.Checksums["overstory"], Is.Not.EqualTo(a.Checksums["overstory"]), "another seed draws another community");
            Assert.That(c.Checksums["water"], Is.EqualTo(a.Checksums["water"]), "and the same water");
        }

        [Test]
        public void TheSaveCarriesTheWakeAndTheLayersForward()
        {
            RegionRaster heights = TestRasters.MadeCoast();
            string dir = Path.Combine(_dir, "w");
            WorldCreation.Result r = WorldCreation.Create(dir, FixtureRegion, 1347UL, heights, "2026-09-09T00:00:00Z");
            Double3 wake = new Double3(r.Wake.East, 0.0, r.Wake.North);
            WorldState world = new WorldState(1347UL, FixtureRegion, FixtureRegion.WakeClock(), new Heightfield(RegionRaster.Load(r.Layers["heights"])), 0, wake);
            WorldSave.Write(dir, world, null, "2026-09-09T00:00:00Z", r.Checksums);
            WorldSaveInfo info = WorldSave.Read(dir);
            Assert.That(info.Wake.HasValue, Is.True);
            Assert.That(info.Wake.Value.X, Is.EqualTo(r.Wake.East));
            Assert.That(info.Wake.Value.Z, Is.EqualTo(r.Wake.North));
            Assert.That(info.Layers.Count, Is.EqualTo(r.Checksums.Count));
            Assert.That(info.Layers["topology"], Is.EqualTo(r.Checksums["topology"]));

            Heightfield saved = new Heightfield(RegionRaster.Load(Path.Combine(dir, WorldCreation.LayersFolder, "heights.json")));
            WorldState back = WorldSave.Restore(info, saved, FixtureRegion);
            Assert.That(back.Wake.HasValue, Is.True);
            Double3 spawn = back.SpawnPoint();
            Assert.That(spawn.X, Is.EqualTo(r.Wake.East));
            Assert.That(spawn.Z, Is.EqualTo(r.Wake.North));
            Assert.That(spawn.Y, Is.GreaterThanOrEqualTo(WakeScorer.MinHeightM), "the wake stands on its ground");

            // A later save without the manifest carries it forward from the file.
            WorldSave.Write(dir, back, null, "2026-09-09T01:00:00Z");
            WorldSaveInfo again = WorldSave.Read(dir);
            Assert.That(again.Layers["water"], Is.EqualTo(r.Checksums["water"]));
            Assert.That(again.Wake.HasValue, Is.True);
        }
    }
}
