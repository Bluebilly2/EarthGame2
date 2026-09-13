using System;
using System.IO;
using EarthGame.Engine;
using EarthGame.Server;
using NUnit.Framework;

namespace EarthGame.Tests.Engine
{
    /// <summary>WG.0b: a world is made from maps of the same place, never merely arrays of the same size.</summary>
    public sealed class WorldInputTests
    {
        private static RegionRaster Copy(RegionRaster raster, Action<JsonObject> change)
        {
            JsonObject header = Json.ParseObject(Json.Write(raster.Sidecar));
            change(header);
            using var stream = new MemoryStream();
            using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true))
            {
                if (raster.IsIntegral)
                    foreach (uint value in raster.Codes) writer.Write((byte)value);
                else
                    foreach (float value in raster.Values) writer.Write(value);
            }
            return RegionRaster.FromParts(Json.Write(header), stream.ToArray(), "altered fixture");
        }

        private static void RefusesBeforeWork(RegionRaster heights, RegionRaster water, string field)
        {
            int stages = 0;
            var error = Assert.Throws<InvalidDataException>(() =>
                WorldLayers.Compute(heights, 1347UL, water, _ => stages++));
            Assert.That(error.Message, Does.Contain(field));
            Assert.That(error.Message, Does.Contain("actual"));
            Assert.That(error.Message, Does.Contain("required"));
            Assert.That(stages, Is.Zero, "a bad input must fail before generation starts");
        }

        [TestCase("centre_lat", -35.15)]
        [TestCase("centre_lon", 150.685)]
        [TestCase("extent_m", 1601.0)]
        [TestCase("scale", 2.0)]
        public void EqualSizedMapsWithDifferentNumericMeaningAreRefused(string field, double value)
        {
            RegionRaster water = Copy(TestRasters.MadeWaterBodies(), j => j.With(field, value));
            RefusesBeforeWork(TestRasters.MadeCoast(), water, field);
        }

        [TestCase("region", "somewhere_else")]
        [TestCase("layer", "overstory")]
        [TestCase("unit", "m")]
        public void WaterMustNameTheSameRegionAndTheRightKindOfData(string field, string value)
        {
            RegionRaster water = Copy(TestRasters.MadeWaterBodies(), j => j.With(field, value));
            RefusesBeforeWork(TestRasters.MadeCoast(), water, field);
        }

        [Test]
        public void MatchingArraySizeDoesNotMakeDifferentCellPitchesAgree()
        {
            RegionRaster water = Copy(TestRasters.MadeWaterBodies(),
                j => j.With("cell_m", 11.0).With("extent_m", 1760.0));
            RefusesBeforeWork(TestRasters.MadeCoast(), water, "cell_m");
        }

        [Test]
        public void DifferentDimensionsAreRefusedBeforeFindingLakes()
        {
            RegionRaster water = TestRasters.FromCodes(81, 20, 1600, "smaller", "water_bodies", (_, _) => 0, "");
            RefusesBeforeWork(TestRasters.MadeCoast(), water, "width");
        }

        [Test]
        public void WaterCodesCannotBeFloatingPointEvenWhenTheyAreWholeNumbers()
        {
            RegionRaster water = Copy(TestRasters.MadeCoast(), j => j.With("layer", "water_bodies").With("unit", "id"));
            RefusesBeforeWork(TestRasters.MadeCoast(), water, "dtype");
        }

        [TestCase("layer", "soil_depth")]
        [TestCase("unit", "ft")]
        public void ThePrimaryInputMustBeHeightsInMetres(string field, string value)
        {
            RefusesBeforeWork(Copy(TestRasters.MadeCoast(), j => j.With(field, value)), null, field);
        }

        [TestCase("centre_lat", 91.0)]
        [TestCase("centre_lon", -181.0)]
        public void MatchingButInvalidCoordinatesAreNotAPlace(string field, double value)
        {
            RefusesBeforeWork(Copy(TestRasters.MadeCoast(), j => j.With(field, value)),
                Copy(TestRasters.MadeWaterBodies(), j => j.With(field, value)), field);
        }

        [Test]
        public void MatchingMapsMayHaveDifferentDescriptiveNames()
        {
            RegionRaster heights = TestRasters.MadeCoast();
            RegionRaster water = TestRasters.MadeWaterBodies();
            WorldLayers original = WorldLayers.Compute(heights, 1347UL, water);
            WorldLayers renamed = WorldLayers.Compute(heights, 1347UL,
                Copy(water, j => j.With("name", "another_file_name")));
            Assert.That(renamed.Water, Is.EqualTo(original.Water));
            Assert.That(renamed.HeightsWithFloor, Is.EqualTo(original.HeightsWithFloor));
            Assert.That(renamed.Stand, Is.EqualTo(original.Stand));
            Assert.That(renamed.Capacity, Is.EqualTo(original.Capacity));
        }

        [Test]
        public void WaterOutlinesRemainOptional()
        {
            WorldLayers without = WorldLayers.Compute(TestRasters.MadeCoast(), 1347UL);
            Assert.That(without.Water[70 * TestRasters.MadeSide + 80], Is.EqualTo((byte)WaterClass.Lake));
        }

        [Test]
        public void RejectedInputsWriteNoWorldFolder()
        {
            string folder = Path.Combine(TestPaths.Root, "Artefacts", "codex-wg0b-tests", Guid.NewGuid().ToString("N"));
            RegionRaster water = Copy(TestRasters.MadeWaterBodies(), j => j.With("centre_lat", -34.0));
            Assert.Throws<InvalidDataException>(() => WorldCreation.Create(folder, Region.Bherwerre, 1347UL,
                TestRasters.MadeCoast(), "2026-09-13T00:00:00Z", water));
            Assert.That(Directory.Exists(folder), Is.False, "a refusal leaves no partial world to continue");
        }
    }
}
