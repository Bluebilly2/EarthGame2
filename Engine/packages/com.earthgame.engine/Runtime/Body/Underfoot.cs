namespace EarthGame.Engine
{
    /// <summary>
    /// What a founder's feet stand on, as the legs feel it (BF.4 stage two, 2026-09-24): the cover the world says covers the
    /// cell, with its wetness quarter, and the water over it, read as the grounds of Pandolf, Givoni and Goldman's table
    /// (<see cref="Locomotion.TerrainFactor"/>). Rock is made ground; bare earth and a beach's sand in the wettest quarter,
    /// which packs, are a dirt road; grass, the forest floor and sedge in the drier half are light brush; heath and bracken
    /// heavy brush; a swamp's floor and sedge in the wetter half a bog; a dune and a beach's dry sand loose sand. Water over
    /// the top of a foot leaves the walk to the wading law, and the slope is the walking table's already. The server reads
    /// its cover layer here and the client its cover tile, the same codes, so the two walk one ground. The footsteps keep an
    /// ear's grouping of the same codes (<c>Footing</c>): bracken is heard as grass and walked as heavy brush.
    /// </summary>
    public static class Underfoot
    {
        /// <summary>Water at least this deep leaves the walk to the wading law, m: over the top of a foot, as the ear hears it.</summary>
        public const double WadingFromM = 0.05;

        /// <summary>The ground under a cell of this cover code, with water of this depth standing over it.</summary>
        public static GroundType Of(byte coverCode, double waterDepthM)
        {
            if (waterDepthM >= WadingFromM) return Locomotion.TableGround;
            int quarter = GroundCovers.QuarterOf(coverCode);
            switch (GroundCovers.CoverOf(coverCode))
            {
                case GroundCover.Rock:
                    return GroundType.Made;
                case GroundCover.BareEarth:
                    return GroundType.Firm;
                case GroundCover.Sand:
                    return quarter >= GroundCovers.Quarters - 1 ? GroundType.Firm : GroundType.Loose;
                case GroundCover.DuneSand:
                    return GroundType.Loose;
                case GroundCover.Heath:
                case GroundCover.Bracken:
                    return GroundType.HeavyBrush;
                case GroundCover.Sedge:
                    return quarter >= GroundCovers.Quarters / 2 ? GroundType.Bog : GroundType.LightBrush;
                case GroundCover.SwampFloor:
                    return GroundType.Bog;
                default:
                    // Grass, the forest floor, the edge of a water's own cells, and a cover nothing has said.
                    return Locomotion.TableGround;
            }
        }
    }
}
