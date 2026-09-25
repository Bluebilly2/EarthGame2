using System;
using System.Collections.Generic;
using EarthGame.ClientCore;
using EarthGame.Engine;
using NUnit.Framework;

namespace EarthGame.Tests.ClientCore
{
    /// <summary>
    /// The ground beyond the held tiles wearing its forest's colour (M1.6g promise 1): a square full of trees its species'
    /// foliage, a square of none the bare ground, one between by the share its crowns cover; each texel the square it stands
    /// for, rows south to north; a region whose far tiles are not held bare.
    /// </summary>
    public sealed class FarCanopyTests
    {
        private static readonly TileGrid Grid = new TileGrid(8000.0);
        private static readonly int Posts = (int)Math.Round(Grid.TileSizeM / TileLayers.FarCellM) + 1;

        /// <summary>A far tile as a client holds it: the far stand's codes two bytes a post, the far count's one.</summary>
        private static ReceivedTile Tile(TileId id, TileLayer layer, Func<int, int, ushort> codes)
        {
            Grid.Origin(id, out double east, out double north);
            bool wide = TileLayers.CodeBytes(layer) == 2;
            ReceivedTile tile = new ReceivedTile
            {
                Id = id, Layer = layer, Posts = Posts, CellM = TileLayers.FarCellM, OriginEast = east, OriginNorth = north, Crc32 = 7u,
                Codes = wide ? null : new byte[Posts, Posts],
                WideCodes = wide ? new ushort[Posts, Posts] : null,
            };
            for (int z = 0; z < Posts; z++)
                for (int x = 0; x < Posts; x++)
                {
                    if (wide) tile.WideCodes[z, x] = codes(z, x);
                    else tile.Codes[z, x] = (byte)codes(z, x);
                }
            return tile;
        }

        private static void AssertColour(byte[] map, int texels, int x, int z, GroundColour expected, string what)
        {
            int i = (z * texels + x) * FarCanopy.BytesPerTexel;
            Assert.That(map[i] / 255.0, Is.EqualTo(expected.R).Within(0.5 / 255 + 1e-6), what + ", red");
            Assert.That(map[i + 1] / 255.0, Is.EqualTo(expected.G).Within(0.5 / 255 + 1e-6), what + ", green");
            Assert.That(map[i + 2] / 255.0, Is.EqualTo(expected.B).Within(0.5 / 255 + 1e-6), what + ", blue");
        }

        [Test]
        public void ASquareWearsItsTreesFoliageByTheShareTheirCrownsCover()
        {
            ushort blackbutt = StandCodes.Pack(PlantSpecies.Blackbutt, 30.0);
            Assert.That(FarCanopy.ShareOf(blackbutt, 0), Is.EqualTo(0.0), "no tree, no cover");
            double one = FarCanopy.ShareOf(blackbutt, 1), four = FarCanopy.ShareOf(blackbutt, 4);
            Assert.That(one, Is.GreaterThan(0.0).And.LessThan(1.0));
            Assert.That(four, Is.EqualTo(Math.Min(1.0, 4.0 * one)).Within(1e-12), "four crowns cover four times one, to the whole square");
            Assert.That(FarCanopy.ShareOf(blackbutt, 200), Is.EqualTo(1.0), "and never more than the square");
            Rgb foliage = StandForms.For(PlantSpecies.Blackbutt).Foliage;
            GroundColour full = FarCanopy.ColourOf(blackbutt, 200);
            Assert.That(full.R, Is.EqualTo(foliage.R).Within(1e-6));
            Assert.That(full.G, Is.EqualTo(foliage.G).Within(1e-6));
            Assert.That(full.B, Is.EqualTo(foliage.B).Within(1e-6));
            GroundColour bare = FarCanopy.ColourOf(0, 0);
            Assert.That(bare.R, Is.EqualTo(FarCanopy.Bare.R).Within(1e-6), "a square of no tree is the bare ground");
            GroundColour between = FarCanopy.ColourOf(blackbutt, 1);
            double seen = FarCanopy.SeenShare(one);
            Assert.That(seen, Is.GreaterThan(one).And.LessThan(1.0), "a grazing view sees more of the crowns than their share from above");
            Assert.That(seen, Is.EqualTo(1.0 - Math.Pow(1.0 - one, FarCanopy.SeenPower)).Within(1e-12));
            Assert.That(between.G, Is.EqualTo(FarCanopy.Bare.G + (foliage.G - FarCanopy.Bare.G) * (float)seen).Within(1e-5), "the seen share of the way");
            Assert.That(FarCanopy.SeenShare(0.0), Is.EqualTo(0.0));
            Assert.That(FarCanopy.SeenShare(1.0), Is.EqualTo(1.0));
        }

        [Test]
        public void EachTexelIsTheSquareItStandsForSouthToNorth()
        {
            // One square of trees in one tile: its texel wears them, its neighbours are bare.
            TileId id = new TileId(3, 4);
            ushort blackbutt = StandCodes.Pack(PlantSpecies.Blackbutt, 30.0);
            Dictionary<(TileLayer, TileId), ReceivedTile> held = new Dictionary<(TileLayer, TileId), ReceivedTile>();
            for (int iz = 0; iz < Grid.TilesPerSide; iz++)
                for (int ix = 0; ix < Grid.TilesPerSide; ix++)
                {
                    TileId t = new TileId(ix, iz);
                    bool it = t.Equals(id);
                    held[(TileLayer.FarStand, t)] = Tile(t, TileLayer.FarStand, (z, x) => it && z == 5 && x == 7 ? blackbutt : (ushort)0);
                    held[(TileLayer.FarCount, t)] = Tile(t, TileLayer.FarCount, (z, x) => it && z == 5 && x == 7 ? (ushort)200 : (ushort)0);
                }
            byte[] map = FarCanopy.Build(Grid, (layer, t) => held.TryGetValue((layer, t), out ReceivedTile r) ? r : null, out int texels);
            Assert.That(texels, Is.EqualTo(FarCanopy.TexelsFor(Grid)));
            Assert.That(texels, Is.EqualTo(200), "a texel a 40 m square over 8 km");
            Assert.That(map.Length, Is.EqualTo(texels * texels * FarCanopy.BytesPerTexel));
            int tx = 3 * (Posts - 1) + 7, tz = 4 * (Posts - 1) + 5;
            AssertColour(map, texels, tx, tz, FarCanopy.ColourOf(blackbutt, 200), "the square of trees");
            AssertColour(map, texels, tx + 1, tz, FarCanopy.Bare, "east of it");
            AssertColour(map, texels, tx, tz + 1, FarCanopy.Bare, "north of it");
            AssertColour(map, texels, tx, tz - 1, FarCanopy.Bare, "south of it");
            AssertColour(map, texels, 0, 0, FarCanopy.Bare, "the region's south-west corner");
        }

        [Test]
        public void ARegionWhoseFarTilesAreNotHeldIsBare()
        {
            byte[] map = FarCanopy.Build(Grid, (layer, t) => null, out int texels);
            for (int z = 0; z < texels; z += 17)
                for (int x = 0; x < texels; x += 13) AssertColour(map, texels, x, z, FarCanopy.Bare, "texel " + x + ", " + z);
        }
    }
}
