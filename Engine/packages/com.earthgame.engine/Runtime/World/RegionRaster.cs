using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace EarthGame.Engine
{
    /// <summary>
    /// One layer of a region as a raw little-endian grid beside a JSON sidecar that is its header (format
    /// <c>eg2.raster</c>; the Python writer is Tools/data/raster_io.py, the C# writer is <see cref="Write"/>, and
    /// the format is contracted in ARCHITECTURE.md §10). Row-major, row 0 at the north edge and column 0 at the
    /// west edge; cell centres sit on the tangent plane at east = col · cell − extent/2 and north = extent/2 −
    /// row · cell, so the centre cell is exactly local (0, 0) and the grid is (extent / cell) + 1 on a side.
    ///
    /// <para>Version 1 carried one float32 grid of metres. Version 2 (M1.2, 2026-09-09) names the layer and its
    /// dtype (f32, u8, u16, i16, u32) and states a scale and a unit, so that soil depth travels as u16
    /// centimetres, wetness as u8 in 1/255, an id or a flag mask as whole codes: <see cref="Values"/> is always
    /// the layer in its unit (raw × scale) and <see cref="Codes"/> the raw integers where the dtype is one. A
    /// version-1 file reads as a version-2 heights layer in metres.</para>
    ///
    /// <para>The loader refuses rather than guesses: a sidecar of another format or version, a raw file whose
    /// length disagrees with the sidecar, or a checksum that does not match is an <see cref="InvalidDataException"/>
    /// naming what disagreed. A version bump is a renegotiation in writing, never a silent read (owner ruling 16).</para>
    /// </summary>
    public sealed class RegionRaster
    {
        public const string Format = "eg2.raster";
        public const int Version = 2;

        private readonly float[] _values;
        private readonly uint[] _codes;

        private RegionRaster(JsonObject sidecar, float[] values, uint[] codes)
        {
            Name = sidecar.String("name");
            RegionId = sidecar.String("region");
            Sidecar = sidecar;
            Layer = sidecar.StringOr("layer", "heights");
            Dtype = sidecar.StringOr("dtype", "f32");
            Scale = sidecar.Contains("scale") ? sidecar.Number("scale") : 1.0;
            Unit = sidecar.StringOr("unit", "m");
            RawName = sidecar.StringOr("raw", Name + ".r32");
            Width = sidecar.Int("width");
            Height = sidecar.Int("height");
            CellM = sidecar.Number("cell_m");
            ExtentM = sidecar.Number("extent_m");
            CentreLatDeg = sidecar.Number("centre_lat");
            CentreLonDeg = sidecar.Number("centre_lon");
            Min = (float)(sidecar.Contains("min") ? sidecar.Number("min") : sidecar.Number("min_m"));
            Max = (float)(sidecar.Contains("max") ? sidecar.Number("max") : sidecar.Number("max_m"));
            Sha256 = sidecar.String("sha256");
            _values = values;
            _codes = codes;
        }

        public string Name { get; }
        /// <summary>The sidecar as read, for the keys a layer adds beyond the contracted ones (a legend, say).</summary>
        public JsonObject Sidecar { get; }
        public string RegionId { get; }
        /// <summary>What the grid is: heights, soil_depth, wetness, overstory, understory, suitability, water, topology.</summary>
        public string Layer { get; }
        /// <summary>How the raw file stores a cell: f32, u8, u16, i16 or u32.</summary>
        public string Dtype { get; }
        /// <summary>A value is raw × Scale in <see cref="Unit"/>; 1 for f32 layers.</summary>
        public double Scale { get; }
        /// <summary>m, 1 (a fraction), id or flags.</summary>
        public string Unit { get; }
        /// <summary>The raw file's name beside the sidecar.</summary>
        public string RawName { get; }
        public int Width { get; }
        public int Height { get; }
        /// <summary>Distance between cell centres, metres.</summary>
        public double CellM { get; }
        /// <summary>Side of the square the cells cover, metres: the outermost centres are at ±ExtentM/2.</summary>
        public double ExtentM { get; }
        public double CentreLatDeg { get; }
        public double CentreLonDeg { get; }
        /// <summary>The least and greatest value in the layer's unit, as the sidecar states them.</summary>
        public float Min { get; }
        public float Max { get; }
        /// <summary>Hex SHA-256 of the raw file, as the sidecar states and the loader verified.</summary>
        public string Sha256 { get; }

        /// <summary>True when the raw file holds whole codes (an id or a flag mask) rather than a measured quantity.</summary>
        public bool IsIntegral => _codes != null;

        /// <summary>The value at a row (0 = north edge) and column (0 = west edge), in the layer's unit.</summary>
        public float this[int row, int col] => _values[row * Width + col];

        /// <summary>The raw code at a row and column; only for integral layers.</summary>
        public uint Code(int row, int col)
        {
            if (_codes == null) throw new InvalidOperationException("layer '" + Layer + "' is " + Dtype + ", not a code layer");
            return _codes[row * Width + col];
        }

        /// <summary>The whole grid in the layer's unit, row-major, for a caller that copies it somewhere.</summary>
        public ReadOnlySpan<float> Values => _values;

        /// <summary>The whole grid of raw codes, row-major; empty for an f32 layer.</summary>
        public ReadOnlySpan<uint> Codes => _codes == null ? ReadOnlySpan<uint>.Empty : _codes;

        /// <summary>The raw file that belongs to a sidecar: named by the sidecar, else the same stem with .r32.</summary>
        public static string RawPathFor(string sidecarPath)
        {
            if (File.Exists(sidecarPath))
            {
                try
                {
                    JsonObject sidecar = Json.ParseObject(File.ReadAllText(sidecarPath, Encoding.UTF8));
                    string raw = sidecar.StringOr("raw", null);
                    if (!string.IsNullOrEmpty(raw)) return Path.Combine(Path.GetDirectoryName(sidecarPath) ?? string.Empty, raw);
                }
                catch (JsonException)
                {
                    // Unreadable sidecars are refused by FromParts with the reason; the path rule below stands.
                }
            }
            return Path.ChangeExtension(sidecarPath, ".r32");
        }

        /// <summary>Loads a sidecar and the raw file beside it.</summary>
        public static RegionRaster Load(string sidecarPath)
        {
            if (!File.Exists(sidecarPath)) throw new FileNotFoundException("raster sidecar not found", sidecarPath);
            string rawPath = RawPathFor(sidecarPath);
            if (!File.Exists(rawPath)) throw new FileNotFoundException("raster data not found beside its sidecar", rawPath);
            return FromParts(File.ReadAllText(sidecarPath, Encoding.UTF8), File.ReadAllBytes(rawPath), sidecarPath);
        }

        /// <summary>Bytes per cell for a dtype the format names, or zero for one it does not.</summary>
        public static int BytesPerCell(string dtype)
        {
            switch (dtype)
            {
                case "f32": return 4;
                case "u8": return 1;
                case "u16": return 2;
                case "i16": return 2;
                case "u32": return 4;
                default: return 0;
            }
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
            if (version != 1 && version != Version)
                throw new InvalidDataException(describe + ": sidecar version is " + version + ", this loader reads versions 1 and " + Version
                                               + "; a format change is a renegotiation in writing, not a silent read");
            string dtype = sidecar.StringOr("dtype", "f32");
            string order = sidecar.StringOr("byte_order", "little");
            int bytesPerCell = BytesPerCell(dtype);
            if (bytesPerCell == 0 || order != "little")
                throw new InvalidDataException(describe + ": dtype " + dtype + " " + order + "-endian; this loader reads f32, u8, u16, i16 and u32 little-endian");
            if (version == 1 && dtype != "f32")
                throw new InvalidDataException(describe + ": a version-1 sidecar carries f32 only, not " + dtype);

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
            double scale = sidecar.Contains("scale") ? sidecar.Number("scale") : 1.0;
            if (!(scale > 0.0) || double.IsInfinity(scale))
                throw new InvalidDataException(describe + ": scale must be a positive number");
            if (dtype == "f32" && scale != 1.0)
                throw new InvalidDataException(describe + ": an f32 layer is stored in its unit; its scale must be 1");

            long expectedBytes = (long)width * height * bytesPerCell;
            if (raw == null || raw.LongLength != expectedBytes)
                throw new InvalidDataException(describe + ": raw file is " + (raw == null ? 0 : raw.LongLength) + " bytes; the sidecar's "
                                               + width + "x" + height + " " + dtype + " needs " + expectedBytes);

            string claimed = sidecar.String("sha256");
            string actual = HexSha256(raw);
            if (!string.Equals(claimed, actual, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException(describe + ": sha256 of the raw file is " + actual + ", the sidecar says " + claimed
                                               + " (the two files are not from the same bake)");

            int count = width * height;
            float[] values = new float[count];
            uint[] codes = dtype == "f32" ? null : new uint[count];
            for (int i = 0; i < count; i++)
            {
                int o = i * bytesPerCell;
                switch (dtype)
                {
                    case "f32":
                    {
                        int bits = raw[o] | (raw[o + 1] << 8) | (raw[o + 2] << 16) | (raw[o + 3] << 24);
                        float v = BitConverter.Int32BitsToSingle(bits);
                        if (float.IsNaN(v) || float.IsInfinity(v))
                            throw new InvalidDataException(describe + ": cell " + i + " is " + v + "; the writers refuse NaN, so this file did not come from one");
                        values[i] = v;
                        break;
                    }
                    case "u8":
                        codes[i] = raw[o];
                        values[i] = (float)(codes[i] * scale);
                        break;
                    case "u16":
                        codes[i] = (uint)(raw[o] | (raw[o + 1] << 8));
                        values[i] = (float)(codes[i] * scale);
                        break;
                    case "i16":
                    {
                        short signed = (short)(raw[o] | (raw[o + 1] << 8));
                        codes[i] = unchecked((uint)signed);
                        values[i] = (float)(signed * scale);
                        break;
                    }
                    default:
                        codes[i] = (uint)(raw[o] | (raw[o + 1] << 8) | (raw[o + 2] << 16) | (raw[o + 3] << 24));
                        values[i] = (float)(codes[i] * scale);
                        break;
                }
            }
            return new RegionRaster(sidecar, values, codes);
        }

        /// <summary>
        /// Writes a measured layer in the version-2 format beside its sidecar, atomically: the values in the
        /// layer's unit, stored as f32 or quantised to an integer dtype by the scale (a value the dtype cannot
        /// hold is refused). The geometry is copied from a raster of the same region, so no two layers of a world
        /// can disagree about the grid. The date is the caller's: the engine reads no clock.
        /// </summary>
        public static string Write(string dir, string name, RegionRaster like, string layer, string dtype, double scale, string unit,
                                   float[] values, string source, string writtenBy, string writtenAtUtc)
        {
            if (like == null) throw new ArgumentNullException(nameof(like));
            if (values == null || values.Length != like.Width * like.Height)
                throw new ArgumentException("values must hold " + like.Width * like.Height + " cells", nameof(values));
            int bytesPerCell = BytesPerCell(dtype);
            if (bytesPerCell == 0) throw new ArgumentException("unknown dtype " + dtype, nameof(dtype));
            if (dtype == "f32" && scale != 1.0) throw new ArgumentException("an f32 layer is stored in its unit; scale must be 1", nameof(scale));
            if (!(scale > 0.0)) throw new ArgumentException("scale must be positive", nameof(scale));

            byte[] raw = new byte[values.Length * bytesPerCell];
            double min = double.PositiveInfinity, max = double.NegativeInfinity;
            for (int i = 0; i < values.Length; i++)
            {
                float v = values[i];
                if (float.IsNaN(v) || float.IsInfinity(v)) throw new ArgumentException("cell " + i + " is " + v + "; a layer holds no NaN", nameof(values));
                int o = i * bytesPerCell;
                double stored;
                if (dtype == "f32")
                {
                    int bits = BitConverter.SingleToInt32Bits(v);
                    raw[o] = (byte)bits; raw[o + 1] = (byte)(bits >> 8); raw[o + 2] = (byte)(bits >> 16); raw[o + 3] = (byte)(bits >> 24);
                    stored = v;
                }
                else
                {
                    long q = (long)Math.Round(v / scale, MidpointRounding.ToEven);
                    long lo, hi;
                    switch (dtype)
                    {
                        case "u8": lo = 0; hi = byte.MaxValue; break;
                        case "u16": lo = 0; hi = ushort.MaxValue; break;
                        case "i16": lo = short.MinValue; hi = short.MaxValue; break;
                        default: lo = 0; hi = uint.MaxValue; break;
                    }
                    if (q < lo || q > hi)
                        throw new ArgumentException("layer " + layer + ": cell " + i + " = " + v + " " + unit + " is " + q + " units of " + scale + ", outside " + dtype, nameof(values));
                    PutCode(raw, o, bytesPerCell, unchecked((uint)q));
                    stored = q * scale;
                }
                if (stored < min) min = stored;
                if (stored > max) max = stored;
            }
            return WriteParts(dir, name, like, layer, dtype, scale, unit, raw, min, max, source, writtenBy, writtenAtUtc);
        }

        /// <summary>Writes a code layer (an id or a flag mask) in the version-2 format: whole numbers, scale 1.</summary>
        public static string WriteCodes(string dir, string name, RegionRaster like, string layer, string dtype, string unit,
                                        uint[] codes, string source, string writtenBy, string writtenAtUtc)
        {
            if (like == null) throw new ArgumentNullException(nameof(like));
            if (codes == null || codes.Length != like.Width * like.Height)
                throw new ArgumentException("codes must hold " + like.Width * like.Height + " cells", nameof(codes));
            int bytesPerCell = BytesPerCell(dtype);
            if (bytesPerCell == 0 || dtype == "f32") throw new ArgumentException("a code layer is u8, u16, i16 or u32, not " + dtype, nameof(dtype));
            uint ceiling = dtype == "u8" ? byte.MaxValue : dtype == "u16" ? ushort.MaxValue : dtype == "i16" ? ushort.MaxValue : uint.MaxValue;
            byte[] raw = new byte[codes.Length * bytesPerCell];
            uint min = uint.MaxValue, max = 0;
            for (int i = 0; i < codes.Length; i++)
            {
                if (codes[i] > ceiling) throw new ArgumentException("layer " + layer + ": code " + codes[i] + " at cell " + i + " does not fit " + dtype, nameof(codes));
                PutCode(raw, i * bytesPerCell, bytesPerCell, codes[i]);
                if (codes[i] < min) min = codes[i];
                if (codes[i] > max) max = codes[i];
            }
            return WriteParts(dir, name, like, layer, dtype, 1.0, unit, raw, min, max, source, writtenBy, writtenAtUtc);
        }

        private static void PutCode(byte[] raw, int offset, int bytesPerCell, uint code)
        {
            raw[offset] = (byte)code;
            if (bytesPerCell > 1) raw[offset + 1] = (byte)(code >> 8);
            if (bytesPerCell > 2)
            {
                raw[offset + 2] = (byte)(code >> 16);
                raw[offset + 3] = (byte)(code >> 24);
            }
        }

        private static string WriteParts(string dir, string name, RegionRaster like, string layer, string dtype, double scale, string unit,
                                         byte[] raw, double min, double max, string source, string writtenBy, string writtenAtUtc)
        {
            Directory.CreateDirectory(dir);
            string rawName = name + (dtype == "f32" ? ".r32" : "." + dtype);
            string rawPath = Path.Combine(dir, rawName);
            string sidecarPath = Path.Combine(dir, name + ".json");
            WriteAtomic(rawPath, raw);
            JsonObject sidecar = new JsonObject()
                .With("format", Format).With("version", Version).With("name", name).With("region", like.RegionId)
                .With("layer", layer).With("dtype", dtype).With("byte_order", "little").With("raw", rawName)
                .With("scale", scale).With("unit", unit)
                .With("layout", "row-major; row 0 is the north edge, column 0 is the west edge; a value is raw * scale in the unit")
                .With("width", like.Width).With("height", like.Height).With("cell_m", like.CellM).With("extent_m", like.ExtentM)
                .With("centre_lat", like.CentreLatDeg).With("centre_lon", like.CentreLonDeg)
                .With("frame", "tangent plane, +east +north metres from the centre; small-angle mapping as Engine LocalFrame")
                .With("min", min).With("max", max).With("source", source ?? string.Empty).With("sha256", HexSha256(raw))
                .With("written_by", writtenBy ?? string.Empty).With("baked_at_utc", writtenAtUtc ?? string.Empty);
            WriteAtomic(sidecarPath, Encoding.UTF8.GetBytes(Json.Write(sidecar, indent: true) + "\n"));
            return sidecarPath;
        }

        private static void WriteAtomic(string path, byte[] bytes)
        {
            string part = path + ".part";
            File.WriteAllBytes(part, bytes);
            if (File.Exists(path)) File.Delete(path);
            File.Move(part, path);
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
