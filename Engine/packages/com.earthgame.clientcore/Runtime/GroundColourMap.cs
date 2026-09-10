using System;
using EarthGame.Engine;

namespace EarthGame.ClientCore
{
    /// <summary>
    /// A tile's cover as the picture a view lays over that tile's ground (M1.4d promise 4). Engine-free, so what
    /// is painted can be asserted without a renderer, exactly as the water's quads are.
    ///
    /// <para>The colour of a texel is the four posts around it blended, so a beach does not end on a line and a
    /// gully darkens into its spur. Two texels a post is what a blend needs and no more: a metre a texel over a
    /// kilometre tile costs four times the work and shows the same 4 m raster.</para>
    ///
    /// <para>Rows run south to north and columns west to east, three bytes a texel, which is what a texture over
    /// a terrain tile wants: the tile's origin is its south-west corner.</para>
    /// </summary>
    public static class GroundColourMap
    {
        /// <summary>Texels along one side of a tile's map: two per 4 m post of a kilometre tile.</summary>
        public const int Texels = 512;

        public const int BytesPerTexel = 3;

        /// <summary>
        /// The map for a tile of cover, or null when there is nothing to paint. The tile must be a layer of
        /// codes: a ground tile carries metres and means nothing here.
        /// </summary>
        public static byte[] Build(ReceivedTile cover, int texels = Texels)
        {
            if (cover == null) return null;
            if (cover.Layer != TileLayer.GroundCover)
                throw new ArgumentException("a tile's cover is layer " + TileLayer.GroundCover + ", not " + cover.Layer, nameof(cover));
            if (cover.Codes == null) return null;
            if (texels < 1) throw new ArgumentOutOfRangeException(nameof(texels), "a map is at least one texel a side");

            int posts = cover.Posts;
            byte[] rgb = new byte[texels * texels * BytesPerTexel];
            // A texel stands for a square of the tile; its centre in posts is where its colour is read.
            double perTexel = (posts - 1) / (double)texels;
            int at = 0;
            for (int row = 0; row < texels; row++)
            {
                double fz = (row + 0.5) * perTexel;
                int z0 = (int)fz;
                if (z0 > posts - 2) z0 = Math.Max(0, posts - 2);
                float tz = (float)(fz - z0);
                for (int col = 0; col < texels; col++)
                {
                    double fx = (col + 0.5) * perTexel;
                    int x0 = (int)fx;
                    if (x0 > posts - 2) x0 = Math.Max(0, posts - 2);
                    float tx = (float)(fx - x0);
                    int x1 = Math.Min(x0 + 1, posts - 1), z1 = Math.Min(z0 + 1, posts - 1);
                    GroundColour south = Between(cover.Codes[z0, x0], cover.Codes[z0, x1], tx);
                    GroundColour north = Between(cover.Codes[z1, x0], cover.Codes[z1, x1], tx);
                    GroundColour c = GroundColour.Between(south, north, tz);
                    rgb[at++] = Byte(c.R);
                    rgb[at++] = Byte(c.G);
                    rgb[at++] = Byte(c.B);
                }
            }
            return rgb;
        }

        private static GroundColour Between(byte a, byte b, float t)
            => GroundColour.Between(GroundPalette.Of(a), GroundPalette.Of(b), t);

        private static byte Byte(float channel)
        {
            int v = (int)(channel * 255f + 0.5f);
            return v < 0 ? (byte)0 : v > 255 ? (byte)255 : (byte)v;
        }
    }
}
