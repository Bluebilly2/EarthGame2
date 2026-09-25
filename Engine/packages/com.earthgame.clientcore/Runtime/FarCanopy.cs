using System;
using EarthGame.Engine;

namespace EarthGame.ClientCore
{
    /// <summary>
    /// The ground beyond the held tiles wearing its forest's colour (M1.6g): one picture of the whole region from the two far
    /// layers the client holds for all of it (M1.6d), a texel a 40 m square, rows south to north and columns west to east as a
    /// texture over a Terrain wants them. A square's colour is the ground's where no tree stands and its trees' foliage over it
    /// by the share of the square their crowns cover, so the far forest thinned by distance (M1.6f) reads as forest on its hills
    /// rather than slivers over bare ground. Engine-free, so what is painted can be asserted without a renderer.
    /// </summary>
    public static class FarCanopy
    {
        public const int BytesPerTexel = 3;

        /// <summary>The ground between the far trees: the colour of a ground whose cover is not known, which is the client's knowledge there.</summary>
        public static GroundColour Bare => GroundPalette.Of(GroundCover.Unknown, 0);

        /// <summary>Texels along a side of the region's picture: a 40 m square each.</summary>
        public static int TexelsFor(TileGrid grid) => grid == null ? 0 : Math.Max(1, (int)Math.Round(grid.ExtentM / TileLayers.FarCellM));

        /// <summary>
        /// How much of a far square its trees' crowns cover, 0 to 1: as many crowns as its count, each as wide as its trees'
        /// mean height and their species' crown share make it, over the square's area.
        /// </summary>
        public static double ShareOf(ushort farStandCode, int count)
        {
            if (farStandCode == 0 || count <= 0) return 0.0;
            PlantSpecies species = StandCodes.SpeciesOf(farStandCode);
            if (species == null) return 0.0;
            double crown = species.CrownShare * StandCodes.HeightOf(farStandCode);
            double share = count * Math.PI * 0.25 * crown * crown / (TileLayers.FarCellM * TileLayers.FarCellM);
            return share >= 1.0 ? 1.0 : share;
        }

        /// <summary>
        /// How much of the ground at a far square a view hides behind its crowns (M1.6g): far ground is only ever seen at a
        /// grazing angle, where the crowns in front stand before the ground between them, so a square a third covered from
        /// above reads four-fifths foliage. One less the bare share to <see cref="SeenPower"/>. The first build painted the
        /// share seen from above, and the whole valley's far hills read grey where their forest stood (2026-09-23).
        /// </summary>
        public static double SeenShare(double share)
        {
            if (!(share > 0.0)) return 0.0;
            if (share >= 1.0) return 1.0;
            return 1.0 - Math.Pow(1.0 - share, SeenPower);
        }

        /// <summary>How many crowns deep a grazing view of a far square looks through, for <see cref="SeenShare"/>.</summary>
        public const double SeenPower = 4.0;

        /// <summary>
        /// A far square's colour: the bare ground, and its trees' foliage over it by the share of it a view sees them hide. A tall
        /// plant with no form fails here by name (WG.2c, 2026-09-25): it was painted blackbutt's green, which would have hidden a
        /// new tree's missing row on every far hill.
        /// </summary>
        public static GroundColour ColourOf(ushort farStandCode, int count)
        {
            double share = SeenShare(ShareOf(farStandCode, count));
            if (!(share > 0.0)) return Bare;
            PlantSpecies species = StandCodes.SpeciesOf(farStandCode);
            TreeForm form = StandForms.For(species) ?? throw new InvalidOperationException(species.Name + " stands in a far square and has no form to be coloured by");
            Rgb foliage = form.Foliage;
            return GroundColour.Between(Bare, new GroundColour(foliage.R, foliage.G, foliage.B), (float)share);
        }

        /// <summary>
        /// The region's picture from the far tiles a client holds (<see cref="TileReceiver.Holding"/>): the far square at each
        /// texel's south-west corner; a square whose tiles are not held is bare.
        /// </summary>
        public static byte[] Build(TileGrid grid, Func<TileLayer, TileId, ReceivedTile> tiles, out int texels)
        {
            if (grid == null) throw new ArgumentNullException(nameof(grid));
            if (tiles == null) throw new ArgumentNullException(nameof(tiles));
            texels = TexelsFor(grid);
            byte[] rgb = new byte[texels * texels * BytesPerTexel];
            int perTile = (int)Math.Round(grid.TileSizeM / TileLayers.FarCellM);
            GroundColour bare = Bare;
            byte br = Byte(bare.R), bg = Byte(bare.G), bb = Byte(bare.B);
            for (int z = 0; z < texels; z++)
            {
                int iz = Math.Min(z / perTile, grid.TilesPerSide - 1), lz = z - iz * perTile;
                for (int x = 0; x < texels; x++)
                {
                    int ix = Math.Min(x / perTile, grid.TilesPerSide - 1), lx = x - ix * perTile;
                    TileId id = new TileId(ix, iz);
                    ReceivedTile stand = tiles(TileLayer.FarStand, id), count = tiles(TileLayer.FarCount, id);
                    int at = (z * texels + x) * BytesPerTexel;
                    if (stand?.WideCodes == null || count?.Codes == null || lz >= stand.Posts || lx >= stand.Posts || stand.Posts != count.Posts)
                    {
                        rgb[at] = br;
                        rgb[at + 1] = bg;
                        rgb[at + 2] = bb;
                        continue;
                    }
                    GroundColour c = ColourOf(stand.WideCodes[lz, lx], count.Codes[lz, lx]);
                    rgb[at] = Byte(c.R);
                    rgb[at + 1] = Byte(c.G);
                    rgb[at + 2] = Byte(c.B);
                }
            }
            return rgb;
        }

        private static byte Byte(float channel)
        {
            int v = (int)(channel * 255f + 0.5f);
            return v < 0 ? (byte)0 : v > 255 ? (byte)255 : (byte)v;
        }
    }
}
