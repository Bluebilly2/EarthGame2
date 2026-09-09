using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using EarthGame.Engine;

namespace EarthGame.Tests
{
    /// <summary>
    /// Rasters the tests build in memory, so the world layers and the world-creation pipeline can be run on a
    /// coast whose every feature the test can name.
    /// </summary>
    public static class TestRasters
    {
        public const int MadeSide = 161;          // 1.6 km at 10 m
        public const double MadeCellM = 10.0;
        public const double MadeExtentM = 1600.0;

        /// <summary>
        /// Rows 0 (north) to 160 (south): a low northern plain, a cliff up to a hill crest at row 20 (40 m),
        /// a plain falling gently from 15.5 m to 12 m (so that its swale drains rather than pools), a lake
        /// hollow at row 70 (6 m floor), the plain falling to a beach at row 140 and the sea from row 150; a
        /// shallow swale down the plain at column 30 so a creek can gather.
        /// </summary>
        public static float MadeCoastHeight(int row, int col)
        {
            double h;
            if (row >= 150) h = 0.0;
            else if (row >= 140) h = (150 - row) * 0.6;
            else if (row >= 110) h = 6.0 + (140 - row) * 0.2;
            else if (row >= 40) h = 12.0 + (110 - row) * 0.05;
            else h = 12.0 + (40 - row) * 1.4;
            if (row < 20) h = 40.0 - (20 - row) * 12.0;
            if (row < 17) h = 4.0 + (17 - row) * 0.1;
            double dr = row - 70, dc = col - 80;
            double lakeRadius = Math.Sqrt(dr * dr + dc * dc);
            if (lakeRadius < 18) h = 6.0;
            else if (lakeRadius < 24) h = 6.0 + (lakeRadius - 18) * 1.0;
            double swale = Math.Exp(-((col - 30) * (col - 30)) / 50.0) * 1.5;
            if (row >= 40 && row < 140) h -= swale;
            return (float)h;
        }

        /// <summary>The made coast as a version-2 heights raster of the fixture region.</summary>
        public static RegionRaster MadeCoast() => FromLaw(MadeSide, MadeCellM, MadeExtentM, "made", MadeCoastHeight);

        /// <summary>
        /// Mapped water bodies for the made coast, as the bake would draw them: outline 1, "Made Lake", a rectangle
        /// on the sloping plain (rows 45 to 55, columns 110 to 130) whose lower half will stand under the median
        /// level; outline 2, "Made Swamp", a wetland on the fall to the beach (rows 120 to 130, columns 20 to 40).
        /// </summary>
        public static RegionRaster MadeWaterBodies()
        {
            string legend = "\"bodies\":[{\"code\":1,\"osm\":\"way/1\",\"name\":\"Made Lake\",\"kind\":\"lake\"},{\"code\":2,\"osm\":\"way/2\",\"name\":\"Made Swamp\",\"kind\":\"wetland\"}]";
            return FromCodes(MadeSide, MadeCellM, MadeExtentM, "made_water", "water_bodies", (row, col) =>
                row >= 45 && row <= 55 && col >= 110 && col <= 130 ? 1u : row >= 120 && row <= 130 && col >= 20 && col <= 40 ? 2u : 0u, legend);
        }

        /// <summary>A u8 id raster from a code law, with a sidecar the loader accepts and any extra keys given as JSON members.</summary>
        public static RegionRaster FromCodes(int side, double cellM, double extentM, string name, string layer, Func<int, int, uint> law, string extraJson)
        {
            byte[] raw = new byte[side * side];
            uint min = uint.MaxValue, max = 0;
            for (int row = 0; row < side; row++)
                for (int col = 0; col < side; col++)
                {
                    uint code = law(row, col);
                    if (code > 255) throw new ArgumentOutOfRangeException(nameof(law), "a u8 code");
                    if (code < min) min = code;
                    if (code > max) max = code;
                    raw[row * side + col] = (byte)code;
                }
            string sha;
            using (SHA256 s = SHA256.Create())
            {
                StringBuilder sb = new StringBuilder();
                foreach (byte b in s.ComputeHash(raw)) sb.Append(b.ToString("x2"));
                sha = sb.ToString();
            }
            string sidecar = "{\"format\":\"eg2.raster\",\"version\":2,\"name\":\"" + name + "\",\"region\":\"fixture\",\"layer\":\"" + layer + "\",\"dtype\":\"u8\",\"byte_order\":\"little\",\"raw\":\"" + name + ".u8\",\"scale\":1.0,\"unit\":\"id\","
                             + "\"width\":" + side + ",\"height\":" + side + ",\"cell_m\":" + cellM.ToString(CultureInfo.InvariantCulture) + ",\"extent_m\":" + extentM.ToString(CultureInfo.InvariantCulture)
                             + ",\"centre_lat\":-35.14,\"centre_lon\":150.675,\"min\":" + min + ",\"max\":" + max + ",\"sha256\":\"" + sha + "\"" + (string.IsNullOrEmpty(extraJson) ? "" : "," + extraJson) + "}";
            return RegionRaster.FromParts(sidecar, raw, name);
        }

        /// <summary>A raster from a height law, with a sidecar the loader accepts.</summary>
        public static RegionRaster FromLaw(int side, double cellM, double extentM, string name, Func<int, int, float> law)
        {
            byte[] raw = new byte[side * side * 4];
            float min = float.MaxValue, max = float.MinValue;
            for (int row = 0; row < side; row++)
                for (int col = 0; col < side; col++)
                {
                    float h = law(row, col);
                    if (h < min) min = h;
                    if (h > max) max = h;
                    int bits = BitConverter.SingleToInt32Bits(h);
                    int o = (row * side + col) * 4;
                    raw[o] = (byte)bits; raw[o + 1] = (byte)(bits >> 8); raw[o + 2] = (byte)(bits >> 16); raw[o + 3] = (byte)(bits >> 24);
                }
            string sha;
            using (SHA256 s = SHA256.Create())
            {
                StringBuilder sb = new StringBuilder();
                foreach (byte b in s.ComputeHash(raw)) sb.Append(b.ToString("x2"));
                sha = sb.ToString();
            }
            string sidecar = "{\"format\":\"eg2.raster\",\"version\":2,\"name\":\"" + name + "\",\"region\":\"fixture\",\"layer\":\"heights\",\"dtype\":\"f32\",\"byte_order\":\"little\",\"raw\":\"" + name + ".r32\",\"scale\":1.0,\"unit\":\"m\","
                             + "\"width\":" + side + ",\"height\":" + side + ",\"cell_m\":" + cellM.ToString(CultureInfo.InvariantCulture) + ",\"extent_m\":" + extentM.ToString(CultureInfo.InvariantCulture)
                             + ",\"centre_lat\":-35.14,\"centre_lon\":150.675,\"min\":" + min.ToString(CultureInfo.InvariantCulture) + ",\"max\":" + max.ToString(CultureInfo.InvariantCulture) + ",\"sha256\":\"" + sha + "\"}";
            return RegionRaster.FromParts(sidecar, raw, name);
        }
    }
}
