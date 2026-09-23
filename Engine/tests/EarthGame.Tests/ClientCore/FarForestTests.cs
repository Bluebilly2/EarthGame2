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

        /// <summary>Every square of every tile of a region full of banksias: the far forest a thinning is judged over.</summary>
        private static List<FarTree> Everywhere()
        {
            byte banksia = StandCodes.Pack(PlantSpecies.CoastBanksia, 8.75);
            List<FarTree> trees = new List<FarTree>();
            for (int iz = 0; iz < Grid.TilesPerSide; iz++)
                for (int ix = 0; ix < Grid.TilesPerSide; ix++)
                {
                    TileId id = new TileId(ix, iz);
                    FarForest.Place(Tile(id, TileLayer.FarStand, (z, x) => banksia), Tile(id, TileLayer.FarCount, (z, x) => (byte)3), new Flat(), Grid, trees);
                }
            return trees;
        }

        [Test]
        public void EachLevelKeepsHalfTheTreesOfTheLevelBelowIt()
        {
            // M1.6f promise 1: a tree's level from its own square, one in 2^L kept at level L, the same every time.
            List<FarTree> trees = Everywhere();
            List<FarTree> again = Everywhere();
            Assert.That(again.Count, Is.EqualTo(trees.Count));
            int[] atLeast = new int[FarForest.MostLevel + 1];
            for (int i = 0; i < trees.Count; i++)
            {
                Assert.That(again[i].Level, Is.EqualTo(trees[i].Level), "a tree's level is its square's, every time");
                Assert.That(trees[i].Level, Is.InRange(0, FarForest.MostLevel));
                for (int l = 0; l <= trees[i].Level; l++) atLeast[l]++;
            }
            Assert.That(atLeast[0], Is.EqualTo(trees.Count), "every tree is drawn at level 0");
            for (int l = 1; l <= FarForest.MostLevel; l++)
            {
                double p = Math.Pow(0.5, l), expected = trees.Count * p, sigma = Math.Sqrt(trees.Count * p * (1 - p));
                TestContext.WriteLine("level " + l + ": " + atLeast[l] + " of " + trees.Count + " kept, " + expected.ToString("0") + " expected");
                Assert.That(Math.Abs(atLeast[l] - expected), Is.LessThan(3.0 * sigma), "one in " + (1 << l) + " kept at level " + l);
            }
        }

        [Test]
        public void TheSpreadCrownsCoverWhatTheWholeForestDid()
        {
            // The canopy's cover: the kept crowns, spread, cover the area the whole level-0 forest did, within three standard
            // deviations of how many a level happens to keep (the crowns here are one size, so the cover is the count's).
            List<FarTree> trees = Everywhere();
            double whole = 0.0;
            foreach (FarTree t in trees) whole += (double)t.CrownM * t.CrownM;
            for (int l = 1; l <= FarForest.MostLevel; l++)
            {
                double spread = FarForest.SpreadAt(l), kept = 0.0;
                foreach (FarTree t in trees)
                    if (t.Level >= l) kept += (double)t.CrownM * spread * t.CrownM * spread;
                double p = Math.Pow(0.5, l), sigma = Math.Sqrt((1 - p) / (trees.Count * p));
                TestContext.WriteLine("level " + l + ": the spread crowns cover " + (kept / whole).ToString("0.000") + " of the whole forest's (3 sigma " + (3 * sigma).ToString("0.000") + ")");
                Assert.That(kept / whole, Is.EqualTo(1.0).Within(3.0 * sigma), "level " + l);
            }
            Assert.That(FarForest.SpreadAt(0), Is.EqualTo(1.0));
        }

        [Test]
        public void TheLevelRisesByOneEachTimeTheDistanceGrowsByTheRootOfTwo()
        {
            Assert.That(FarForest.LevelAt(0.0), Is.EqualTo(0));
            Assert.That(FarForest.LevelAt(FarForest.FullToM - 1.0), Is.EqualTo(0), "the whole forest near the founder");
            for (int l = 1; l <= FarForest.MostLevel; l++)
            {
                double from = FarForest.FullToM * Math.Pow(Math.Sqrt(2.0), l - 1);
                Assert.That(FarForest.LevelAt(from + 0.5), Is.EqualTo(l), "level " + l + " from " + from.ToString("0") + " m");
                Assert.That(FarForest.LevelAt(from - 0.5), Is.EqualTo(l - 1));
            }
            Assert.That(FarForest.LevelAt(1e6), Is.EqualTo(FarForest.MostLevel), "and no further than the last");
        }
    }
}
