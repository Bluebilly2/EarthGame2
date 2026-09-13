using System;
using System.Collections.Generic;
using EarthGame.ClientCore;
using EarthGame.Engine;
using NUnit.Framework;

namespace EarthGame.Tests.ClientCore
{
    /// <summary>
    /// The far forest placed from the two far layers (M1.6d promises 3 and 7): one tree for each square that holds any,
    /// inside its square, as tall as its trees and as wide as their crowns would cover, and each square placed by one tile.
    /// </summary>
    public sealed class FarForestTests
    {
        private static readonly TileGrid Grid = new TileGrid(8000.0);
        private static readonly int Posts = (int)Math.Round(Grid.TileSizeM / TileLayers.FarCellM) + 1;

        private sealed class Flat : IHeightSource
        {
            public double HeightAt(double east, double north) => 12.0;
        }

        private static ReceivedTile Tile(TileId id, TileLayer layer, Func<int, int, byte> codes)
        {
            Grid.Origin(id, out double east, out double north);
            ReceivedTile tile = new ReceivedTile
            {
                Id = id, Layer = layer, Posts = Posts, CellM = TileLayers.FarCellM, OriginEast = east, OriginNorth = north, Crc32 = 7u,
                Codes = new byte[Posts, Posts],
            };
            for (int z = 0; z < Posts; z++)
                for (int x = 0; x < Posts; x++) tile.Codes[z, x] = codes(z, x);
            return tile;
        }

        [Test]
        public void ASquareStandsOneTreeInsideItAsWideAsItsTreesCrownsWouldCover()
        {
            TileId id = new TileId(3, 4);
            byte blackbutt = StandCodes.Pack(PlantSpecies.Blackbutt, 30.0);
            ReceivedTile stand = Tile(id, TileLayer.FarStand, (z, x) => z == 5 && x == 7 ? blackbutt : (byte)0);
            ReceivedTile count = Tile(id, TileLayer.FarCount, (z, x) => z == 5 && x == 7 ? (byte)16 : (byte)0);
            List<FarTree> trees = new List<FarTree>();
            FarForest.Place(stand, count, new Flat(), Grid, trees);

            Assert.That(trees.Count, Is.EqualTo(1));
            FarTree tree = trees[0];
            double postEast = stand.OriginEast + 7 * TileLayers.FarCellM, postNorth = stand.OriginNorth + 5 * TileLayers.FarCellM;
            Assert.That(Math.Abs(tree.East - postEast), Is.LessThanOrEqualTo(FarForest.WanderM + 1e-3), "inside its square");
            Assert.That(Math.Abs(tree.North - postNorth), Is.LessThanOrEqualTo(FarForest.WanderM + 1e-3));
            Assert.That(tree.Up, Is.EqualTo(12f), "on the ground it is drawn over");
            Assert.That(tree.HeightM, Is.EqualTo(30f), "as tall as its trees");
            double crowns = PlantSpecies.Blackbutt.CrownShare * 30.0 * Math.Sqrt(16.0);
            Assert.That(tree.CrownM, Is.EqualTo((float)Math.Min(crowns, TileLayers.FarCellM * FarForest.MostCrownShare)).Within(1e-3),
                "as wide as sixteen crowns would cover, and no wider than the square allows");
            Assert.That(StandCodes.Tall[tree.Tall], Is.SameAs(PlantSpecies.Blackbutt));

            List<FarTree> again = new List<FarTree>();
            FarForest.Place(stand, count, new Flat(), Grid, again);
            Assert.That(again[0].East, Is.EqualTo(tree.East), "and in the same place every time");
            Assert.That(again[0].North, Is.EqualTo(tree.North));
        }

        [Test]
        public void EachSquareIsPlacedByOneTileAndAnEmptySquareByNone()
        {
            byte banksia = StandCodes.Pack(PlantSpecies.CoastBanksia, 8.75);
            TileId inside = new TileId(2, 2), last = new TileId(Grid.TilesPerSide - 1, Grid.TilesPerSide - 1);
            List<FarTree> trees = new List<FarTree>();
            FarForest.Place(Tile(inside, TileLayer.FarStand, (z, x) => banksia), Tile(inside, TileLayer.FarCount, (z, x) => (byte)3), new Flat(), Grid, trees);
            Assert.That(trees.Count, Is.EqualTo((Posts - 1) * (Posts - 1)), "a tile's last row and column are the next tile's first");
            trees.Clear();
            FarForest.Place(Tile(last, TileLayer.FarStand, (z, x) => banksia), Tile(last, TileLayer.FarCount, (z, x) => (byte)3), new Flat(), Grid, trees);
            Assert.That(trees.Count, Is.EqualTo(Posts * Posts), "and at the region's edge no tile lies beyond");
            trees.Clear();
            FarForest.Place(Tile(inside, TileLayer.FarStand, (z, x) => banksia), Tile(inside, TileLayer.FarCount, (z, x) => (byte)0), new Flat(), Grid, trees);
            Assert.That(trees, Is.Empty, "a square with no tree counted stands none");
            FarForest.Place(Tile(inside, TileLayer.FarStand, (z, x) => banksia), Tile(new TileId(2, 3), TileLayer.FarCount, (z, x) => (byte)3), new Flat(), Grid, trees);
            Assert.That(trees, Is.Empty, "and two tiles' layers are not one tile's");
        }
    }
}
