using System;
using System.Collections.Generic;
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

        /// <summary>An f32 layer's cells in its unit; null for a code layer.</summary>
        private readonly float[] _values;
        /// <summary>
        /// A code layer's raw little-endian cells as the file stores them, one to four bytes each; null for an f32 layer. Until
        /// WG.2b (2026-09-23) a code layer was held as a float and a uint for every cell, eight bytes for each one or two stored,
        /// and the ten layers a 32 km world runs on took 4.6 GB where 1.2 hold them; a cell is now decoded when it is read, by the
        /// same arithmetic the loader used, so every value and code is what it was.
        /// </summary>
        private readonly byte[] _raw;
        private readonly int _bytesPerCell;
        private readonly bool _signed;

        private RegionRaster(JsonObject sidecar, float[] values, byte[] raw)
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
            _raw = raw;
            _bytesPerCell = raw == null ? 4 : BytesPerCell(Dtype);
            _signed = Dtype == "i16";
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
        public bool IsIntegral => _raw != null;

        /// <summary>Bytes the layer's cells take in memory: its stored width a cell, four for an f32 layer (WG.2b).</summary>
        public long BytesHeld => _raw != null ? _raw.LongLength : (long)_values.Length * sizeof(float);

        /// <summary>The value at a row (0 = north edge) and column (0 = west edge), in the layer's unit.</summary>
        public float this[int row, int col] => _values != null ? _values[row * Width + col] : ValueOf(row * Width + col);

        /// <summary>The raw code at a row and column; only for integral layers.</summary>
        public uint Code(int row, int col)
        {
            if (_raw == null) throw new InvalidOperationException("layer '" + Layer + "' is " + Dtype + ", not a code layer");
            return CodeOf(row * Width + col);
        }

        /// <summary>
        /// The whole grid in the layer's unit, row-major, for a caller that copies it somewhere; for a code layer, a copy made on
        /// each call (the layer holds its stored bytes, WG.2b), so a caller asks once.
        /// </summary>
        public ReadOnlySpan<float> Values
        {
            get
            {
                if (_values != null) return _values;
                float[] all = new float[_raw.Length / _bytesPerCell];
                for (int i = 0; i < all.Length; i++) all[i] = ValueOf(i);
                return all;
            }
        }

        /// <summary>The whole grid of raw codes, row-major, a copy made on each call; empty for an f32 layer.</summary>
        public ReadOnlySpan<uint> Codes
        {
            get
            {
                if (_raw == null) return ReadOnlySpan<uint>.Empty;
                uint[] all = new uint[_raw.Length / _bytesPerCell];
                for (int i = 0; i < all.Length; i++) all[i] = CodeOf(i);
                return all;
            }
        }

        /// <summary>A code layer's cell as the loader decoded it before WG.2b: an i16 sign-extended, every other width as stored.</summary>
        private uint CodeOf(int i)
        {
            int o = i * _bytesPerCell;
            switch (_bytesPerCell)
            {
                case 1: return _raw[o];
                case 2:
                    uint pair = (uint)(_raw[o] | (_raw[o + 1] << 8));
                    return _signed ? unchecked((uint)(short)pair) : pair;
                default: return (uint)(_raw[o] | (_raw[o + 1] << 8) | (_raw[o + 2] << 16) | (_raw[o + 3] << 24));
            }
        }

        /// <summary>A code layer's cell in its unit, by the expression the loader used: the code (signed for i16) times the scale.</summary>
        private float ValueOf(int i)
        {
            uint code = CodeOf(i);
            return _signed ? (float)(unchecked((int)code) * Scale) : (float)(code * Scale);
        }

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

        /// <summary>
        /// Loads a sidecar and the raw file beside it. Every refusal names the file: since a world's own terrain
        /// is refused rather than replaced by the region's bake (M1.4 loading, 2026-09-10), these messages are
        /// what a player reads on the failure screen and what an operator reads in the host's log, and "missing
        /// key 'width'" does not say which of a world's twenty-one layers to look at. A key the format requires
        /// but the sidecar lacks arrives here as a KeyNotFoundException from the JSON reader, which carries the
        /// key but not the file.
        /// </summary>
        public static RegionRaster Load(string sidecarPath)
        {
            if (!File.Exists(sidecarPath)) throw new FileNotFoundException("raster sidecar not found: " + sidecarPath, sidecarPath);
            string rawPath = RawPathFor(sidecarPath);
            if (!File.Exists(rawPath)) throw new FileNotFoundException("raster data not found beside its sidecar: " + rawPath, rawPath);
            try
            {
                return FromParts(File.ReadAllText(sidecarPath, Encoding.UTF8), File.ReadAllBytes(rawPath), sidecarPath, false);
            }
            catch (Exception ex) when (ex is KeyNotFoundException || ex is JsonException || ex is FormatException || ex is InvalidCastException)
            {
                throw new InvalidDataException(sidecarPath + ": " + ex.Message);
            }
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
        public static RegionRaster FromParts(string sidecarJson, byte[] raw, string describe) => FromParts(sidecarJson, raw, describe, true);

        /// <param name="copyRaw">Whether a code layer keeps a copy of the bytes rather than the array given: the caller's own
        /// array may change after (a test tampers with one to prove the checksum), while the loader's is read from the file for
        /// this layer alone and becomes it (WG.2b).</param>
        private static RegionRaster FromParts(string sidecarJson, byte[] raw, string describe, bool copyRaw)
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

            if (dtype != "f32")
                return new RegionRaster(sidecar, null, copyRaw ? (byte[])raw.Clone() : raw);
            int count = width * height;
            float[] values = new float[count];
            for (int i = 0; i < count; i++)
            {
                int o = i * 4;
                int bits = raw[o] | (raw[o + 1] << 8) | (raw[o + 2] << 16) | (raw[o + 3] << 24);
                float v = BitConverter.Int32BitsToSingle(bits);
                if (float.IsNaN(v) || float.IsInfinity(v))
                    throw new InvalidDataException(describe + ": cell " + i + " is " + v + "; the writers refuse NaN, so this file did not come from one");
                values[i] = v;
            }
            return new RegionRaster(sidecar, values, null);
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

            long lo = 0, hi;
            switch (dtype)
            {
                case "u8": hi = byte.MaxValue; break;
                case "u16": hi = ushort.MaxValue; break;
                case "i16": lo = short.MinValue; hi = short.MaxValue; break;
                default: hi = uint.MaxValue; break;
            }
            return WriteStreamed(dir, name, like, layer, dtype, scale, unit, (int i, out double stored) =>
            {
                float v = values[i];
                if (float.IsNaN(v) || float.IsInfinity(v)) throw new ArgumentException("cell " + i + " is " + v + "; a layer holds no NaN", nameof(values));
                if (dtype == "f32")
                {
                    stored = v;
                    return unchecked((uint)BitConverter.SingleToInt32Bits(v));
                }
                long q = (long)Math.Round(v / scale, MidpointRounding.ToEven);
                if (q < lo || q > hi)
                    throw new ArgumentException("layer " + layer + ": cell " + i + " = " + v + " " + unit + " is " + q + " units of " + scale + ", outside " + dtype, nameof(values));
                stored = q * scale;
                return unchecked((uint)q);
            }, source, writtenBy, writtenAtUtc);
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
            return WriteCodes(dir, name, like, layer, dtype, unit, i => codes[i], source, writtenBy, writtenAtUtc);
        }

        /// <summary>
        /// Writes a code layer whose code at each cell (row-major) a function gives, so a caller that holds its codes in another
        /// shape (a byte a cell, metres as floats) writes them without widening a copy of the layer first (WG.2b, 2026-09-23).
        /// </summary>
        public static string WriteCodes(string dir, string name, RegionRaster like, string layer, string dtype, string unit,
                                        Func<int, uint> codeAt, string source, string writtenBy, string writtenAtUtc)
        {
            if (like == null) throw new ArgumentNullException(nameof(like));
            if (codeAt == null) throw new ArgumentNullException(nameof(codeAt));
            int bytesPerCell = BytesPerCell(dtype);
            if (bytesPerCell == 0 || dtype == "f32") throw new ArgumentException("a code layer is u8, u16, i16 or u32, not " + dtype, nameof(dtype));
            uint ceiling = dtype == "u8" ? byte.MaxValue : dtype == "u16" ? ushort.MaxValue : dtype == "i16" ? ushort.MaxValue : uint.MaxValue;
            return WriteStreamed(dir, name, like, layer, dtype, 1.0, unit, (int i, out double stored) =>
            {
                uint code = codeAt(i);
                if (code > ceiling) throw new ArgumentException("layer " + layer + ": code " + code + " at cell " + i + " does not fit " + dtype, nameof(codeAt));
                stored = code;
                return code;
            }, source, writtenBy, writtenAtUtc);
        }

        /// <summary>A cell's raw bits for the file (a code, or an f32's bits) and the value it stores, for the sidecar's min and max.</summary>
        private delegate uint CellBits(int cell, out double stored);

        /// <summary>
        /// Writes a layer's raw file a band of cells at a time, through the SHA-256 as it goes, then its sidecar; the raw file lands
        /// whole or not at all (written beside itself as <c>.part</c>, then moved). Until WG.2b (2026-09-23) a layer was built whole
        /// as bytes before it was written, a 32 km layer of four bytes a cell being 256 MB of garbage at every save.
        /// </summary>
        private static string WriteStreamed(string dir, string name, RegionRaster like, string layer, string dtype, double scale, string unit,
                                            CellBits cellBits, string source, string writtenBy, string writtenAtUtc)
        {
            Directory.CreateDirectory(dir);
            int bytesPerCell = BytesPerCell(dtype);
            int count = like.Width * like.Height;
            string rawName = name + (dtype == "f32" ? ".r32" : "." + dtype);
            string rawPath = Path.Combine(dir, rawName);
            string part = rawPath + ".part";
            double min = double.PositiveInfinity, max = double.NegativeInfinity;
            string sha256;
            byte[] band = new byte[StreamBandBytes - StreamBandBytes % bytesPerCell];
            try
            {
                using (FileStream file = new FileStream(part, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 16))
                using (SHA256 sha = SHA256.Create())
                {
                    int filled = 0;
                    for (int i = 0; i < count; i++)
                    {
                        uint bits = cellBits(i, out double stored);
                        if (stored < min) min = stored;
                        if (stored > max) max = stored;
                        band[filled] = (byte)bits;
                        if (bytesPerCell > 1) band[filled + 1] = (byte)(bits >> 8);
                        if (bytesPerCell > 2)
                        {
                            band[filled + 2] = (byte)(bits >> 16);
                            band[filled + 3] = (byte)(bits >> 24);
                        }
                        filled += bytesPerCell;
                        if (filled == band.Length)
                        {
                            file.Write(band, 0, filled);
                            sha.TransformBlock(band, 0, filled, null, 0);
                            filled = 0;
                        }
                    }
                    file.Write(band, 0, filled);
                    sha.TransformFinalBlock(band, 0, filled);
                    sha256 = Hex(sha.Hash);
                }
            }
            catch
            {
                if (File.Exists(part)) File.Delete(part);
                throw;
            }
            if (File.Exists(rawPath)) File.Delete(rawPath);
            File.Move(part, rawPath);
            return WriteSidecar(dir, name, like, layer, dtype, scale, unit, rawName, min, max, sha256, source, writtenBy, writtenAtUtc);
        }

        /// <summary>How many bytes of a layer are gathered before they go to the file and the hash.</summary>
        private const int StreamBandBytes = 1 << 20;

        /// <summary>
        /// The sha256 of a written layer's raw file, read back from the disk a megabyte at a time and held against its sidecar's
        /// claim: what a world's manifest records once the layer is on disk (WG.2b; the world's creation loaded every layer whole
        /// again to learn it).
        /// </summary>
        public static string CheckedSha256(string sidecarPath)
        {
            JsonObject sidecar = Json.ParseObject(File.ReadAllText(sidecarPath, Encoding.UTF8));
            string rawPath = RawPathFor(sidecarPath);
            string actual;
            using (FileStream file = new FileStream(rawPath, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 16))
            using (SHA256 sha = SHA256.Create())
            {
                byte[] chunk = new byte[StreamBandBytes];
                int read;
                while ((read = file.Read(chunk, 0, chunk.Length)) > 0) sha.TransformBlock(chunk, 0, read, null, 0);
                sha.TransformFinalBlock(chunk, 0, 0);
                actual = Hex(sha.Hash);
            }
            string claimed = sidecar.String("sha256");
            if (!string.Equals(claimed, actual, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException(sidecarPath + ": sha256 of the raw file is " + actual + ", the sidecar says " + claimed
                                               + " (the file on disk is not the one written)");
            return actual;
        }

        private static string WriteSidecar(string dir, string name, RegionRaster like, string layer, string dtype, double scale, string unit,
                                           string rawName, double min, double max, string sha256, string source, string writtenBy, string writtenAtUtc)
        {
            string sidecarPath = Path.Combine(dir, name + ".json");
            JsonObject sidecar = new JsonObject()
                .With("format", Format).With("version", Version).With("name", name).With("region", like.RegionId)
                .With("layer", layer).With("dtype", dtype).With("byte_order", "little").With("raw", rawName)
                .With("scale", scale).With("unit", unit)
                .With("layout", "row-major; row 0 is the north edge, column 0 is the west edge; a value is raw * scale in the unit")
                .With("width", like.Width).With("height", like.Height).With("cell_m", like.CellM).With("extent_m", like.ExtentM)
                .With("centre_lat", like.CentreLatDeg).With("centre_lon", like.CentreLonDeg)
                .With("frame", "tangent plane, +east +north metres from the centre; small-angle mapping as Engine LocalFrame")
                .With("min", min).With("max", max).With("source", source ?? string.Empty).With("sha256", sha256)
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
                return Hex(sha.ComputeHash(bytes));
        }

        private static string Hex(byte[] hash)
        {
            StringBuilder sb = new StringBuilder(64);
            foreach (byte b in hash) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }
    }
}
