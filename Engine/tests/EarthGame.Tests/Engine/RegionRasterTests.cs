using System;
using System.IO;
using EarthGame.Engine;
using NUnit.Framework;

namespace EarthGame.Tests.Engine
{
    /// <summary>
    /// The loader reads a fixture the Python writer produced (Tools/data/write_fixtures.py; the same writer as the
    /// real bake), whose heights are a stated law of (row, col): 100 + row + 10·col, plus 5 at the centre cell.
    /// So every cell has a value this file can name, and the frame rule (row 0 north, column 0 west, centre cell
    /// at local zero) is asserted against numbers rather than against the sidecar's prose.
    /// </summary>
    public sealed class RegionRasterTests
    {
        private static string SidecarPath => TestPaths.Fixture("raster", "tiny.json");

        private static double Law(int row, int col) => 100.0 + row + 10.0 * col + (row == 2 && col == 2 ? 5.0 : 0.0);

        private static RegionRaster LoadTiny()
        {
            Assert.That(File.Exists(SidecarPath), Is.True, "fixture missing: " + SidecarPath + " (run python Tools/data/write_fixtures.py with the Tools venv)");
            return RegionRaster.Load(SidecarPath);
        }

        [Test]
        public void TheFixtureLoadsWithItsHeader()
        {
            RegionRaster r = LoadTiny();
            Assert.That(r.Name, Is.EqualTo("tiny"));
            Assert.That(r.Width, Is.EqualTo(5));
            Assert.That(r.Height, Is.EqualTo(5));
            Assert.That(r.CellM, Is.EqualTo(10.0));
            Assert.That(r.ExtentM, Is.EqualTo(40.0));
            Assert.That(r.CentreLatDeg, Is.EqualTo(Region.Bherwerre.CentreLatitudeDeg).Within(1e-12));
            Assert.That(r.MinM, Is.EqualTo(100.0f));
            Assert.That(r.MaxM, Is.EqualTo(144.0f), "row 4, column 4: 100 + 4 + 40");
            Assert.That(r.Sha256.Length, Is.EqualTo(64));
        }

        [Test]
        public void EveryCellReadsTheLawTheWriterUsed()
        {
            RegionRaster r = LoadTiny();
            for (int row = 0; row < 5; row++)
                for (int col = 0; col < 5; col++)
                    Assert.That(r[row, col], Is.EqualTo((float)Law(row, col)), "row " + row + ", col " + col);
        }

        [Test]
        public void CellCentresAreLocalMetresWithRowZeroNorthAndColumnZeroWest()
        {
            Heightfield hf = new Heightfield(LoadTiny());
            // Column 0 is the west edge at east = -20; row 0 is the north edge at north = +20.
            Assert.That(hf.HeightAt(-20.0, 20.0), Is.EqualTo(Law(0, 0)).Within(1e-9), "north-west corner");
            Assert.That(hf.HeightAt(20.0, 20.0), Is.EqualTo(Law(0, 4)).Within(1e-9), "north-east corner");
            Assert.That(hf.HeightAt(-20.0, -20.0), Is.EqualTo(Law(4, 0)).Within(1e-9), "south-west corner");
            Assert.That(hf.HeightAt(0.0, 0.0), Is.EqualTo(Law(2, 2)).Within(1e-9), "the centre cell is local zero, bump included");
            // Heights rise to the east (10 per column) and to the south (1 per row): the mirror checks.
            Assert.That(hf.HeightAt(10.0, 20.0), Is.GreaterThan(hf.HeightAt(-10.0, 20.0)), "east is higher in this fixture");
            Assert.That(hf.HeightAt(-20.0, -10.0), Is.GreaterThan(hf.HeightAt(-20.0, 10.0)), "south is higher in this fixture");
        }

        [Test]
        public void SamplingIsBilinearBetweenCentresAndFlatBeyondTheEdge()
        {
            Heightfield hf = new Heightfield(LoadTiny());
            Assert.That(hf.HeightAt(-15.0, 20.0), Is.EqualTo((Law(0, 0) + Law(0, 1)) * 0.5).Within(1e-9), "halfway between two cells on the north edge");
            Assert.That(hf.HeightAt(5.0, 0.0), Is.EqualTo((Law(2, 2) + Law(2, 3)) * 0.5).Within(1e-9), "halfway east of the bump");
            Assert.That(hf.HeightAt(-15.0, 15.0), Is.EqualTo((Law(0, 0) + Law(0, 1) + Law(1, 0) + Law(1, 1)) * 0.25).Within(1e-9), "the middle of four cells");
            Assert.That(hf.HeightAt(-100.0, 20.0), Is.EqualTo(Law(0, 0)).Within(1e-9), "beyond the west edge the edge value continues");
            Assert.That(hf.HeightAt(0.0, 100.0), Is.EqualTo(Law(0, 2)).Within(1e-9), "and beyond the north edge");
            Assert.That(hf.Contains(20.0, -20.0), Is.True);
            Assert.That(hf.Contains(20.01, 0.0), Is.False);
        }

        [Test]
        public void SlopesAndNormalsFollowTheSurface()
        {
            Heightfield hf = new Heightfield(LoadTiny());
            // Away from the bump the surface is a plane: +1 m per metre east, -0.1 m per metre north.
            Assert.That(hf.SlopeAlong(-10.0, 10.0, 1.0, 0.0), Is.EqualTo(1.0).Within(1e-9), "rise over run eastward");
            Assert.That(hf.SlopeAlong(-10.0, 10.0, 0.0, 1.0), Is.EqualTo(-0.1).Within(1e-9), "northward it falls");
            Assert.That(hf.SlopeAlong(-10.0, 10.0, -1.0, 0.0), Is.EqualTo(-1.0).Within(1e-9), "westward is downhill");
            Double3 n = hf.NormalAt(-10.0, 10.0);
            Assert.That(n.Y, Is.GreaterThan(0.0));
            Assert.That(n.X, Is.LessThan(0.0), "ground rising to the east tilts the normal west");
            Assert.That(n.Z, Is.GreaterThan(0.0), "ground falling to the north tilts the normal north");
            Assert.That(n.Length, Is.EqualTo(1.0).Within(1e-12));
            Assert.That(hf.SlopeAngleDeg(-10.0, 10.0), Is.EqualTo(45.14).Within(0.1), "a 1:1 slope with a little cross-fall");
        }

        [Test]
        public void ARasterFromAnotherBakeIsRefusedByItsChecksum()
        {
            byte[] raw = File.ReadAllBytes(RegionRaster.RawPathFor(SidecarPath));
            string sidecar = File.ReadAllText(SidecarPath);
            raw[7] ^= 0x01;
            Assert.That(() => RegionRaster.FromParts(sidecar, raw, "tampered"), Throws.TypeOf<InvalidDataException>().With.Message.Contains("sha256"));
        }

        [Test]
        public void ATruncatedRawFileIsRefusedByItsLength()
        {
            byte[] raw = File.ReadAllBytes(RegionRaster.RawPathFor(SidecarPath));
            string sidecar = File.ReadAllText(SidecarPath);
            byte[] shorter = new byte[raw.Length - 4];
            Array.Copy(raw, shorter, shorter.Length);
            Assert.That(() => RegionRaster.FromParts(sidecar, shorter, "short"), Throws.TypeOf<InvalidDataException>().With.Message.Contains("bytes"));
        }

        [Test]
        public void AnotherVersionOrFormatIsRefusedNotGuessed()
        {
            byte[] raw = File.ReadAllBytes(RegionRaster.RawPathFor(SidecarPath));
            string sidecar = File.ReadAllText(SidecarPath);
            Assert.That(() => RegionRaster.FromParts(sidecar.Replace("\"version\": 1", "\"version\": 2"), raw, "v2"),
                Throws.TypeOf<InvalidDataException>().With.Message.Contains("version"));
            Assert.That(() => RegionRaster.FromParts(sidecar.Replace("eg2.raster", "eg3.raster"), raw, "other"),
                Throws.TypeOf<InvalidDataException>().With.Message.Contains("format"));
            Assert.That(() => RegionRaster.FromParts(sidecar.Replace("\"width\": 5", "\"width\": 6"), raw, "wide"),
                Throws.TypeOf<InvalidDataException>().With.Message.Contains("cell-centre"));
            Assert.That(() => RegionRaster.FromParts("not json", raw, "junk"),
                Throws.TypeOf<InvalidDataException>().With.Message.Contains("JSON"));
        }

        [Test]
        public void AMissingFileIsAFileError()
        {
            Assert.That(() => RegionRaster.Load(TestPaths.Fixture("raster", "absent.json")), Throws.TypeOf<FileNotFoundException>());
        }
    }
}
