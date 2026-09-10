using EarthGame.Engine;

namespace EarthGame.ClientCore
{
    /// <summary>A colour the client paints with: three channels, zero to one, in the space the renderer takes.</summary>
    public struct GroundColour
    {
        public float R, G, B;

        public GroundColour(float r, float g, float b)
        {
            R = r;
            G = g;
            B = b;
        }

        /// <summary>How bright it reads, by the usual weights; the palette's own test leans on this.</summary>
        public float Luminance => 0.2126f * R + 0.7152f * G + 0.0722f * B;

        public static GroundColour Between(GroundColour dry, GroundColour wet, float t) => new GroundColour(
            dry.R + (wet.R - dry.R) * t,
            dry.G + (wet.G - dry.G) * t,
            dry.B + (wet.B - dry.B) * t);
    }

    /// <summary>
    /// What each ground looks like (M1.4d promise 3). The single owner of the ground's colour on the client: a
    /// tuft of grass or a handful of litter added later asks this and cannot disagree with the ground it stands
    /// on, which is how v1 kept them together.
    ///
    /// <para>Each cover names the colour it has dry and the colour it has wet, and the wetness quarter the world
    /// sent sits between them: a spur that sheds its water is straw and the gully below it is green, and both are
    /// grass. The colours are of this coast — Jervis Bay's sand is nearly white, its heath grey-green, its
    /// kangaroo grass gold by late summer — and they are art direction, so they are the owner's to judge.</para>
    /// </summary>
    public static class GroundPalette
    {
        private static readonly GroundColour[] Dry = new GroundColour[64];
        private static readonly GroundColour[] Wet = new GroundColour[64];

        static GroundPalette()
        {
            // Anything this build has no colour for reads as the ground did before there was a palette.
            for (int i = 0; i < Dry.Length; i++)
            {
                Dry[i] = new GroundColour(0.55f, 0.51f, 0.44f);
                Wet[i] = new GroundColour(0.44f, 0.41f, 0.35f);
            }
            Set(GroundCover.Unknown, 0.55f, 0.51f, 0.44f, 0.44f, 0.41f, 0.35f);
            Set(GroundCover.Sea, 0.10f, 0.20f, 0.24f, 0.08f, 0.16f, 0.20f);
            Set(GroundCover.FreshWater, 0.16f, 0.20f, 0.16f, 0.11f, 0.15f, 0.13f);
            Set(GroundCover.Sand, 0.87f, 0.84f, 0.76f, 0.60f, 0.57f, 0.52f);
            Set(GroundCover.DuneSand, 0.82f, 0.77f, 0.65f, 0.58f, 0.55f, 0.47f);
            Set(GroundCover.Rock, 0.63f, 0.57f, 0.47f, 0.40f, 0.37f, 0.33f);
            Set(GroundCover.BareEarth, 0.47f, 0.39f, 0.30f, 0.29f, 0.24f, 0.19f);
            Set(GroundCover.Heath, 0.40f, 0.42f, 0.30f, 0.28f, 0.34f, 0.24f);
            Set(GroundCover.Bracken, 0.36f, 0.40f, 0.22f, 0.24f, 0.33f, 0.18f);
            Set(GroundCover.Sedge, 0.42f, 0.44f, 0.28f, 0.23f, 0.33f, 0.21f);
            Set(GroundCover.Grass, 0.64f, 0.58f, 0.35f, 0.37f, 0.45f, 0.24f);
            Set(GroundCover.ForestFloor, 0.42f, 0.35f, 0.25f, 0.27f, 0.24f, 0.18f);
            Set(GroundCover.SwampFloor, 0.33f, 0.35f, 0.23f, 0.19f, 0.24f, 0.16f);
        }

        private static void Set(GroundCover cover, float dr, float dg, float db, float wr, float wg, float wb)
        {
            Dry[(byte)cover & GroundCovers.CoverMask] = new GroundColour(dr, dg, db);
            Wet[(byte)cover & GroundCovers.CoverMask] = new GroundColour(wr, wg, wb);
        }

        /// <summary>The colour of a cover in one of the land's wetness quarters; the driest quarter is the dry colour and the wettest the wet one.</summary>
        public static GroundColour Of(GroundCover cover, int quarter)
        {
            int i = (byte)cover & GroundCovers.CoverMask;
            int q = quarter < 0 ? 0 : quarter >= GroundCovers.Quarters ? GroundCovers.Quarters - 1 : quarter;
            return GroundColour.Between(Dry[i], Wet[i], q / (float)(GroundCovers.Quarters - 1));
        }

        /// <summary>The colour of a code as it travelled.</summary>
        public static GroundColour Of(byte code) => Of(GroundCovers.CoverOf(code), GroundCovers.QuarterOf(code));
    }
}
