using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace EarthGame.Engine
{
    /// <summary>
    /// One baked layer of a region as the Python tools write it: a raw little-endian float32 grid, row-major with
    /// row 0 at the north edge and column 0 at the west edge, beside a JSON sidecar that is its header (format
    /// <c>eg2.raster</c>, version 1; the writer is Tools/data/raster_io.py and the format is contracted in
    /// ARCHITECTURE.md §10). Cell centres sit on the tangent plane at east = col · cell − extent/2 and north =
    /// extent/2 − row · cell, so the centre cell is exactly local (0, 0) and the grid is (extent / cell) + 1 on a
    /// side.
    ///
    /// <para>The loader refuses rather than guesses: a sidecar of another format or version, a raw file whose
    /// length disagrees with the sidecar, or a checksum that does not match is an <see cref="InvalidDataException"/>
    /// naming what disagreed. A version bump on the Python side is a renegotiation in writing, never a silent
    /// read (owner ruling 16).</para>
    /// </summary>
    public sealed class RegionRaster
    {
        public const string Format = "eg2.raster";
        public const int Version = 1;

        private readonly float[] _values;

        private RegionRaster(JsonObject sidecar, float[] values)
        {
            Name = sidecar.String("name");
            RegionId = sidecar.String("region");
            Width = sidecar.Int("width");
            Height = sidecar.Int("height");
            CellM = sidecar.Number("cell_m");
            ExtentM = sidecar.Number("extent_m");
            CentreLatDeg = sidecar.Number("centre_lat");
            CentreLonDeg = sidecar.Number("centre_lon");
            MinM = (float)sidecar.Number("min_m");
            MaxM = (float)sidecar.Number("max_m");
            Sha256 = sidecar.String("sha256");
            _values = values;
        }

        public string Name { get; }
        public string RegionId { get; }
        public int Width { get; }
        public int Height { get; }
        /// <summary>Distance between cell centres, metres.</summary>
        public double CellM { get; }
        /// <summary>Side of the square the cells cover, metres: the outermost centres are at ±ExtentM/2.</summary>
        public double ExtentM { get; }
        public double CentreLatDeg { get; }
        public double CentreLonDeg { get; }
        public float MinM { get; }
        public float MaxM { get; }
        /// <summary>Hex SHA-256 of the raw file, as the sidecar states and the loader verified.</summary>
        public string Sha256 { get; }

        /// <summary>The value at a row (0 = north edge) and column (0 = west edge).</summary>
        public float this[int row, int col] => _values[row * Width + col];

        /// <summary>The whole grid, row-major, for a caller that copies it somewhere (a terrain tile builder).</summary>
        public ReadOnlySpan<float> Values => _values;

        /// <summary>The raw file that belongs to a sidecar: the same stem with the .r32 extension.</summary>
        public static string RawPathFor(string sidecarPath) => Path.ChangeExtension(sidecarPath, ".r32");

        /// <summary>Loads a sidecar and the raw file beside it.</summary>
        public static RegionRaster Load(string sidecarPath)
        {
            if (!File.Exists(sidecarPath)) throw new FileNotFoundException("raster sidecar not found", sidecarPath);
            string rawPath = RawPathFor(sidecarPath);
            if (!File.Exists(rawPath)) throw new FileNotFoundException("raster data not found beside its sidecar", rawPath);
            return FromParts(File.ReadAllText(sidecarPath, Encoding.UTF8), File.ReadAllBytes(rawPath), sidecarPath);
        }

        /// <summary>Builds a raster from its two parts, checking everything the sidecar claims about the raw bytes.</summary>
        /// <param name="describe">How to name the source in an error: a path, or a fixture's name.</param>
        public static RegionRaster FromParts(string sidecarJson, byte[] raw, string describe)
        {
            JsonObject sidecar;
            try
            {
                sidecar = Json.ParseObject(sidecarJson);
            }
            catch (JsonException ex)
            {
                throw new InvalidDataException(describe + ": sidecar is not readable JSON: " + ex.Message);
            }

            string format = sidecar.StringOr("format", "(none)");
            if (format != Format)
                throw new InvalidDataException(describe + ": sidecar format is '" + format + "', this loader reads '" + Format + "'");
            int version = sidecar.Contains("version") ? sidecar.Int("version") : -1;
            if (version != Version)
                throw new InvalidDataException(describe + ": sidecar version is " + version + ", this loader reads version " + Version
                                               + "; a format change is a renegotiation in writing, not a silent read");
            string dtype = sidecar.String("dtype");
            string order = sidecar.String("byte_order");
            if (dtype != "f32" || order != "little")
                throw new InvalidDataException(describe + ": dtype " + dtype + " " + order + "-endian; this loader reads f32 little-endian");

            int width = sidecar.Int("width");
            int height = sidecar.Int("height");
            if (width < 2 || height < 2)
                throw new InvalidDataException(describe + ": a " + width + "x" + height + " raster has no cells to sample between");
            double cell = sidecar.Number("cell_m");
            double extent = sidecar.Number("extent_m");
            if (!(cell > 0.0) || !(extent > 0.0))
                throw new InvalidDataException(describe + ": cell_m and extent_m must be positive");
            int side = (int)Math.Round(extent / cell) + 1;
            if (width != side || height != side)
                throw new InvalidDataException(describe + ": " + width + "x" + height + " does not match extent " + extent + " m at " + cell
                                               + " m per cell (the cell-centre rule expects " + side + " on a side)");

            long expectedBytes = (long)width * height * 4;
            if (raw == null || raw.LongLength != expectedBytes)
                throw new InvalidDataException(describe + ": raw file is " + (raw == null ? 0 : raw.LongLength) + " bytes; the sidecar's "
                                               + width + "x" + height + " float32 needs " + expectedBytes);

            string claimed = sidecar.String("sha256");
            string actual = HexSha256(raw);
            if (!string.Equals(claimed, actual, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException(describe + ": sha256 of the raw file is " + actual + ", the sidecar says " + claimed
                                               + " (the two files are not from the same bake)");

            float[] values = new float[width * height];
            if (BitConverter.IsLittleEndian)
            {
                Buffer.BlockCopy(raw, 0, values, 0, raw.Length);
            }
            else
            {
                for (int i = 0; i < values.Length; i++)
                {
                    int bits = raw[i * 4] | (raw[i * 4 + 1] << 8) | (raw[i * 4 + 2] << 16) | (raw[i * 4 + 3] << 24);
                    values[i] = BitConverter.Int32BitsToSingle(bits);
                }
            }
            for (int i = 0; i < values.Length; i++)
            {
                if (float.IsNaN(values[i]) || float.IsInfinity(values[i]))
                    throw new InvalidDataException(describe + ": cell " + i + " is " + values[i] + "; the bake refuses NaN, so this file did not come from it");
            }
            return new RegionRaster(sidecar, values);
        }

        private static string HexSha256(byte[] bytes)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(bytes);
                StringBuilder sb = new StringBuilder(64);
                foreach (byte b in hash) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }
    }
}
