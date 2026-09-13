using System;
using EarthGame.Engine;
using NUnit.Framework;

namespace EarthGame.Tests.World
{
    /// <summary>
    /// The far layers, worked out from the world's stand (M1.6d promises 1 and 7): a far square counts its trees and names
    /// the commonest at their mean height, every tree is counted by exactly one far post, and a far tile carries what its
    /// posts' squares hold.
    /// </summary>
    public sealed class FarLayerTests
    {
        /// <summary>The made coast is 10 m a cell, so a far square of 40 m is four cells a side.</summary>
        private static readonly int Span = (int)Math.Round(TileLayers.FarCellM / TestRasters.MadeCellM);

        private static RegionRaster Stand(Func<int, int, uint> law) =>
            TestRasters.FromCodes(TestRasters.MadeSide, TestRasters.MadeCellM, TestRasters.MadeExtentM, "far_stand", "stand", law, null);

        [Test]
        public void AFarSquareCountsItsTreesAndNamesTheCommonestAtTheirMeanHeight()
        {
            byte thirty = StandCodes.Pack(PlantSpecies.Blackbutt, 30.0), twentyFive = StandCodes.Pack(PlantSpecies.Blackbutt, 25.0);
            byte bangalay = StandCodes.Pack(PlantSpecies.Bangalay, 12.5);
            // The square round cell (80, 80) runs from two cells before it to one after, in rows and in columns.
            RegionRaster stand = Stand((row, col) => row == 78 && col == 78 ? thirty : row == 81 && col == 81 ? twentyFive : row == 79 && col == 80 ? bangalay : 0u);
            Assert.That(TileCodec.FarSquare(stand, 80, 80, Span, TileLayer.FarCount), Is.EqualTo(3u));
            uint far = TileCodec.FarSquare(stand, 80, 80, Span, TileLayer.FarStand);
            Assert.That(StandCodes.SpeciesOf((byte)far), Is.SameAs(PlantSpecies.Blackbutt), "two blackbutts to one bangalay");
            Assert.That(StandCodes.HeightOf((byte)far), Is.EqualTo(27.5), "at the blackbutts' mean height");

            RegionRaster justPast = Stand((row, col) => row == 82 && col == 80 ? thirty : 0u);
            Assert.That(TileCodec.FarSquare(justPast, 80, 80, Span, TileLayer.FarCount), Is.Zero, "a tree just past a square is not its");
            Assert.That(TileCodec.FarSquare(justPast, 84, 80, Span, TileLayer.FarCount), Is.EqualTo(1u), "it is the next square's");
            Assert.That(TileCodec.FarSquare(Stand((row, col) => 0u), 80, 80, Span, TileLayer.FarStand), Is.Zero, "no tree, no plant");
        }

        [Test]
        public void EveryTreeIsCountedByOneFarPostAndTheTileCarriesWhatItsSquaresHold()
        {
            byte banksia = StandCodes.Pack(PlantSpecies.CoastBanksia, 8.75);
            RegionRaster stand = Stand((row, col) => row % 5 == 0 && col % 5 == 0 ? banksia : 0u);
            long counted = 0;
            for (int row = 0; row < stand.Height; row += Span)
                for (int col = 0; col < stand.Width; col += Span)
                    counted += TileCodec.FarSquare(stand, row, col, Span, TileLayer.FarCount);
            int trees = ((stand.Height - 1) / 5 + 1) * ((stand.Width - 1) / 5 + 1);
            Assert.That(counted, Is.EqualTo(trees), "the squares round the far posts cover every cell once");

            TileGrid grid = new TileGrid(TestRasters.MadeExtentM);
            TileId id = new TileId(0, 0);
            foreach (TileLayer which in new[] { TileLayer.FarStand, TileLayer.FarCount })
            {
                EncodedTile tile = TileCodec.EncodeFar(stand, which, grid, id);
                Assert.That(tile.Posts, Is.EqualTo((int)Math.Round(grid.TileSizeM / TileLayers.FarCellM) + 1));
                Assert.That(tile.CellM, Is.EqualTo(TileLayers.FarCellM));
                byte[,] codes = TileCodec.UnpackCodes(tile.Bytes, tile.Posts);
                int wrong = 0;
                for (int z = 0; z < tile.Posts; z++)
                    for (int x = 0; x < tile.Posts; x++)
                    {
                        TileCodec.CellOf(TestRasters.MadeExtentM, TestRasters.MadeCellM, tile.OriginEast + x * tile.CellM, tile.OriginNorth + z * tile.CellM,
                                         out int row, out int col);
                        if (codes[z, x] != TileCodec.FarSquare(stand, row, col, Span, which)) wrong++;
                    }
                Assert.That(wrong, Is.Zero, which + ": every post carries its square");
            }
            Assert.That(() => TileCodec.EncodeFar(stand, TileLayer.Stand, grid, id), Throws.ArgumentException, "only a far layer is encoded far");
        }
    }
}
