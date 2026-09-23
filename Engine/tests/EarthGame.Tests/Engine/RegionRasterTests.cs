using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
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
            Assert.That(r.Min, Is.EqualTo(100.0f));
            Assert.That(r.Max, Is.EqualTo(144.0f), "row 4, column 4: 100 + 4 + 40");
            Assert.That(r.Sha256.Length, Is.EqualTo(64));
            Assert.That(r.Layer, Is.EqualTo("heights"));
            Assert.That(r.Dtype, Is.EqualTo("f32"));
            Assert.That(r.Unit, Is.EqualTo("m"));
            Assert.That(r.Scale, Is.EqualTo(1.0));
            Assert.That(r.IsIntegral, Is.False);
            Assert.That(r.RawName, Is.EqualTo("tiny.r32"));
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
            Assert.That(() => RegionRaster.FromParts(sidecar.Replace("\"version\": 2", "\"version\": 3"), raw, "v3"),
                Throws.TypeOf<InvalidDataException>().With.Message.Contains("version"));
            Assert.That(() => RegionRaster.FromParts(sidecar.Replace("eg2.raster", "eg3.raster"), raw, "other"),
                Throws.TypeOf<InvalidDataException>().With.Message.Contains("format"));
            Assert.That(() => RegionRaster.FromParts(sidecar.Replace("\"width\": 5", "\"width\": 6"), raw, "wide"),
                Throws.TypeOf<InvalidDataException>().With.Message.Contains("cell-centre"));
            Assert.That(() => RegionRaster.FromParts("not json", raw, "junk"),
                Throws.TypeOf<InvalidDataException>().With.Message.Contains("JSON"));
        }

        [Test]
        public void AVersionOneSidecarReadsAsAHeightsLayerInMetres()
        {
            byte[] raw = File.ReadAllBytes(RegionRaster.RawPathFor(SidecarPath));
            JsonObject v2 = Json.ParseObject(File.ReadAllText(SidecarPath));
            string v1 = "{\"format\":\"eg2.raster\",\"version\":1,\"name\":\"tiny\",\"region\":\"fixture\",\"dtype\":\"f32\",\"byte_order\":\"little\","
                        + "\"width\":5,\"height\":5,\"cell_m\":10.0,\"extent_m\":40.0,\"centre_lat\":-35.14,\"centre_lon\":150.675,"
                        + "\"min_m\":100.0,\"max_m\":144.0,\"sha256\":\"" + v2.String("sha256") + "\"}";
            RegionRaster r = RegionRaster.FromParts(v1, raw, "v1");
            Assert.That(r.Layer, Is.EqualTo("heights"));
            Assert.That(r.Unit, Is.EqualTo("m"));
            Assert.That(r.Scale, Is.EqualTo(1.0));
            Assert.That(r.RawName, Is.EqualTo("tiny.r32"));
            Assert.That(r[2, 2], Is.EqualTo(127f));
            Assert.That(r.Min, Is.EqualTo(100f));
            Assert.That(() => RegionRaster.FromParts(v1.Replace("\"dtype\":\"f32\"", "\"dtype\":\"u16\""), raw, "v1 u16"),
                Throws.TypeOf<InvalidDataException>().With.Message.Contains("version-1"));
        }

        [Test]
        public void AnIntegerLayerReadsInItsUnitAndKeepsItsCodes()
        {
            RegionRaster soil = RegionRaster.Load(TestPaths.Fixture("raster", "tiny_soil.json"));
            Assert.That(soil.Layer, Is.EqualTo("soil_depth"));
            Assert.That(soil.Dtype, Is.EqualTo("u16"));
            Assert.That(soil.Scale, Is.EqualTo(0.01));
            Assert.That(soil.Unit, Is.EqualTo("m"));
            Assert.That(soil.IsIntegral, Is.True);
            Assert.That(soil.RawName, Is.EqualTo("tiny_soil.u16"));
            for (int row = 0; row < 5; row++)
                for (int col = 0; col < 5; col++)
                {
                    Assert.That(soil[row, col], Is.EqualTo((3 * row + 7 * col) / 100.0).Within(1e-6), "metres at " + row + "," + col);
                    Assert.That(soil.Code(row, col), Is.EqualTo((uint)(3 * row + 7 * col)), "centimetres at " + row + "," + col);
                }
            Assert.That(soil.Min, Is.EqualTo(0f));
            Assert.That(soil.Max, Is.EqualTo(0.40f).Within(1e-6));

            RegionRaster flags = RegionRaster.Load(TestPaths.Fixture("raster", "tiny_flags.json"));
            Assert.That(flags.Dtype, Is.EqualTo("u32"));
            Assert.That(flags.Unit, Is.EqualTo("flags"));
            for (int row = 0; row < 5; row++)
                for (int col = 0; col < 5; col++)
                    Assert.That(flags.Code(row, col), Is.EqualTo((1u << row) | (1u << (8 + col))));
            Assert.That(() => LoadTiny().Code(0, 0), Throws.InvalidOperationException, "an f32 layer has no codes");
        }

        /// <summary>
        /// A layer is held in the width it is stored in (WG.2b's W1, 2026-09-23): a byte a cell for u8, two for u16 and i16, four
        /// for u32 and f32. Until then every code layer was held as a float and a uint a cell, eight bytes for each one or two
        /// stored, and the ten layers a 32 km world runs on took 4.6 GB where 1.2 would do. Every cell still reads what the loader
        /// decoded before, bit for bit: the value (float)(code × scale) and the code, over each dtype's range, and the whole grid
        /// through <see cref="RegionRaster.Values"/> and <see cref="RegionRaster.Codes"/> as well as cell by cell.
        /// </summary>
        [Test]
        public void ALayerIsHeldInTheWidthItIsStoredInAndReadsAsItDidBefore()
        {
            const int side = 257;   // 66,049 cells: every u16 and i16 code, and the u8 codes 258 times over
            foreach ((string dtype, double scale) in new[] { ("u8", 1.0 / 255.0), ("u16", 0.01), ("i16", 0.5), ("u32", 1.0) })
            {
                int bytesPerCell = RegionRaster.BytesPerCell(dtype);
                int count = side * side;
                byte[] raw = new byte[count * bytesPerCell];
                uint[] codes = new uint[count];
                float[] values = new float[count];
                for (int i = 0; i < count; i++)
                {
                    uint code = dtype == "u8" ? (uint)(i % 256) : dtype == "u32" ? unchecked((uint)i * 2654435761u) : (uint)(i % 65536);
                    for (int b = 0; b < bytesPerCell; b++) raw[i * bytesPerCell + b] = (byte)(code >> (8 * b));
                    // What the loader decoded before W1, written out here as it stood: a signed code sign-extended.
                    codes[i] = dtype == "i16" ? unchecked((uint)(short)code) : code;
                    values[i] = dtype == "i16" ? (float)((short)code * scale) : (float)(code * scale);
                }
                RegionRaster layer = RegionRaster.FromParts(Sidecar(dtype, scale, side, raw), raw, dtype + " fixture");

                Assert.That(layer.BytesHeld, Is.EqualTo((long)count * bytesPerCell), dtype + " is held at its stored width");
                float[] whole = layer.Values.ToArray();
                uint[] wholeCodes = layer.Codes.ToArray();
                for (int i = 0; i < count; i++)
                {
                    int row = i / side, col = i % side;
                    if (BitConverter.SingleToInt32Bits(layer[row, col]) != BitConverter.SingleToInt32Bits(values[i])
                        || layer.Code(row, col) != codes[i] || BitConverter.SingleToInt32Bits(whole[i]) != BitConverter.SingleToInt32Bits(values[i])
                        || wholeCodes[i] != codes[i])
                        Assert.Fail(dtype + " cell " + i + ": read " + layer[row, col] + " code " + layer.Code(row, col) + " (whole grid " + whole[i]
                                    + ", " + wholeCodes[i] + "), decoded before as " + values[i] + " code " + codes[i]);
                }
            }
            Assert.That(LoadTiny().BytesHeld, Is.EqualTo(25L * 4), "an f32 layer, four bytes a cell as before");
        }

        /// <summary>A version-2 sidecar for a made layer of a dtype, its checksum the raw bytes'.</summary>
        private static string Sidecar(string dtype, double scale, int side, byte[] raw)
        {
            string sha;
            using (SHA256 s = SHA256.Create())
            {
                StringBuilder sb = new StringBuilder();
                foreach (byte b in s.ComputeHash(raw)) sb.Append(b.ToString("x2"));
                sha = sb.ToString();
            }
            return "{\"format\":\"eg2.raster\",\"version\":2,\"name\":\"made\",\"region\":\"fixture\",\"layer\":\"made\",\"dtype\":\"" + dtype
                   + "\",\"byte_order\":\"little\",\"raw\":\"made.bin\",\"scale\":" + scale.ToString("R", CultureInfo.InvariantCulture) + ",\"unit\":\"1\","
                   + "\"width\":" + side + ",\"height\":" + side + ",\"cell_m\":1.0,\"extent_m\":" + (side - 1)
                   + ",\"centre_lat\":-35.14,\"centre_lon\":150.675,\"min\":0,\"max\":1,\"sha256\":\"" + sha + "\"}";
        }

        [Test]
        public void TheEngineWritesWhatThePythonWriterWritesAndReadsItBack()
        {
            string dir = Path.Combine(Path.GetTempPath(), "EarthGame2.Tests", "raster", Guid.NewGuid().ToString("N"));
            try
            {
                RegionRaster like = LoadTiny();
                float[] depth = new float[25];
                uint[] codes = new uint[25];
                for (int i = 0; i < 25; i++)
                {
                    int row = i / 5, col = i % 5;
                    depth[i] = (3 * row + 7 * col) / 100f;
                    codes[i] = (1u << row) | (1u << (8 + col));
                }
                string soilSidecar = RegionRaster.Write(dir, "soil", like, "soil_depth", "u16", 0.01, "m", depth, "the fixture law", "RegionRasterTests", "2026-09-09T00:00:00Z");
                RegionRaster soil = RegionRaster.Load(soilSidecar);
                Assert.That(soil.RawName, Is.EqualTo("soil.u16"));
                Assert.That(soil[4, 4], Is.EqualTo(0.40f).Within(1e-6));
                Assert.That(soil.Code(4, 4), Is.EqualTo(40u));
                Assert.That(soil.Sha256, Is.EqualTo(RegionRaster.Load(TestPaths.Fixture("raster", "tiny_soil.json")).Sha256),
                    "the same law, quantised by the same rule, is the same bytes as the Python writer's");

                string flagSidecar = RegionRaster.WriteCodes(dir, "flags", like, "topology", "u32", "flags", codes, "the fixture law", "RegionRasterTests", "2026-09-09T00:00:00Z");
                RegionRaster flags = RegionRaster.Load(flagSidecar);
                Assert.That(flags.Code(3, 1), Is.EqualTo((1u << 3) | (1u << 9)));
                Assert.That(flags.Sha256, Is.EqualTo(RegionRaster.Load(TestPaths.Fixture("raster", "tiny_flags.json")).Sha256));

                string heightsSidecar = RegionRaster.Write(dir, "heights", like, "heights", "f32", 1.0, "m", like.Values.ToArray(), "copied", "RegionRasterTests", "2026-09-09T00:00:00Z");
                Assert.That(RegionRaster.Load(heightsSidecar).Sha256, Is.EqualTo(like.Sha256), "an f32 copy is byte-identical too");

                Assert.That(() => RegionRaster.Write(dir, "bad", like, "soil_depth", "u8", 0.001, "m", depth, "", "", ""),
                    Throws.ArgumentException, "0.40 m in millimetres does not fit a byte, and is refused rather than wrapped");
            }
            finally
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }
        }

        [Test]
        public void AMissingFileIsAFileError()
        {
            Assert.That(() => RegionRaster.Load(TestPaths.Fixture("raster", "absent.json")), Throws.TypeOf<FileNotFoundException>()
                .With.Message.Contains("absent.json"), "the refusal names the file: it is read on a failure screen and in a server log");
        }

        /// <summary>
        /// Every refusal from a file names that file (M1.4 loading, 2026-09-10). A sidecar missing a key the
        /// format requires used to arrive as "missing key 'width'" from the JSON reader, which says nothing about
        /// which of a world's twenty-one layers is at fault; the world's terrain is now refused rather than
        /// replaced, so this message is the whole of what the player and the operator are told.
        /// </summary>
        [Test]
        public void ARefusalFromAFileNamesTheFile()
        {
            string dir = Path.Combine(Path.GetTempPath(), "EarthGame2.Tests", "raster", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                string sidecar = Path.Combine(dir, "wetness.json");
                // Under both names the raw file can take: the one the sidecar states, and the .r32 beside it that
                // RawPathFor falls back to when the sidecar cannot be parsed at all.
                byte[] raw = File.ReadAllBytes(RegionRaster.RawPathFor(SidecarPath));
                File.WriteAllBytes(Path.Combine(dir, Path.GetFileName(RegionRaster.RawPathFor(SidecarPath))), raw);
                File.WriteAllBytes(Path.Combine(dir, "wetness.r32"), raw);
                File.WriteAllText(sidecar, File.ReadAllText(SidecarPath).Replace("\"width\"", "\"widht\""));
                Assert.That(() => RegionRaster.Load(sidecar), Throws.TypeOf<InvalidDataException>().With.Message.Contains("wetness.json"));
                Assert.That(() => RegionRaster.Load(sidecar), Throws.TypeOf<InvalidDataException>().With.Message.Contains("width"), "and the key it wanted");

                File.WriteAllText(sidecar, "{\"format\":\"eg2.raster\",\"version\":2");
                Assert.That(() => RegionRaster.Load(sidecar), Throws.TypeOf<InvalidDataException>().With.Message.Contains("wetness.json"));
            }
            finally
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }
        }
    }
}
