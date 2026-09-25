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

        /// <summary>A stand of the two-byte layout, as a world made since WG.2c holds it.</summary>
        private static RegionRaster Stand(Func<int, int, uint> law) =>
            TestRasters.FromCodes(TestRasters.MadeSide, TestRasters.MadeCellM, TestRasters.MadeExtentM, "far_stand", "stand", law, null, "u16");

        [Test]
        public void AFarSquareCountsItsTreesAndNamesTheCommonestAtTheirMeanHeight()
        {
            ushort thirty = StandCodes.Pack(PlantSpecies.Blackbutt, 30.0), twentyFive = StandCodes.Pack(PlantSpecies.Blackbutt, 25.0);
            ushort bangalay = StandCodes.Pack(PlantSpecies.Bangalay, 12.5);
            // The square round cell (80, 80) runs from two cells before it to one after, in rows and in columns.
            RegionRaster stand = Stand((row, col) => row == 78 && col == 78 ? thirty : row == 81 && col == 81 ? twentyFive : row == 79 && col == 80 ? bangalay : 0u);
            Assert.That(TileCodec.FarSquare(stand, 80, 80, Span, TileLayer.FarCount), Is.EqualTo(3u));
            uint far = TileCodec.FarSquare(stand, 80, 80, Span, TileLayer.FarStand);
            Assert.That(StandCodes.SpeciesOf((ushort)far), Is.SameAs(PlantSpecies.Blackbutt), "two blackbutts to one bangalay");
            Assert.That(StandCodes.HeightOf((ushort)far), Is.EqualTo(27.5), "at the blackbutts' mean height");

            // One of each: the tie goes to the first tall plant in the catalogue's order.
            RegionRaster tie = Stand((row, col) => row == 78 && col == 78 ? bangalay : row == 79 && col == 79 ? thirty : 0u);
            Assert.That(StandCodes.SpeciesOf((ushort)TileCodec.FarSquare(tie, 80, 80, Span, TileLayer.FarStand)), Is.SameAs(PlantSpecies.Blackbutt), "a tie to the first in the catalogue's order");

            RegionRaster justPast = Stand((row, col) => row == 82 && col == 80 ? thirty : 0u);
            Assert.That(TileCodec.FarSquare(justPast, 80, 80, Span, TileLayer.FarCount), Is.Zero, "a tree just past a square is not its");
            Assert.That(TileCodec.FarSquare(justPast, 84, 80, Span, TileLayer.FarCount), Is.EqualTo(1u), "it is the next square's");
            Assert.That(TileCodec.FarSquare(Stand((row, col) => 0u), 80, 80, Span, TileLayer.FarStand), Is.Zero, "no tree, no plant");
        }

        [Test]
        public void EveryTreeIsCountedByOneFarPostAndTheTileCarriesWhatItsSquaresHold()
        {
            ushort banksia = StandCodes.Pack(PlantSpecies.CoastBanksia, 8.75);
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
                bool wide = TileLayers.CodeBytes(which) == 2;
                Assert.That(wide, Is.EqualTo(which == TileLayer.FarStand), "the far stand two bytes a post, the far count one");
                ushort[,] wideCodes = wide ? TileCodec.UnpackWideCodes(tile.Bytes, tile.Posts) : null;
                byte[,] codes = wide ? null : TileCodec.UnpackCodes(tile.Bytes, tile.Posts);
                int wrong = 0;
                for (int z = 0; z < tile.Posts; z++)
                    for (int x = 0; x < tile.Posts; x++)
                    {
                        TileCodec.CellOf(TestRasters.MadeExtentM, TestRasters.MadeCellM, tile.OriginEast + x * tile.CellM, tile.OriginNorth + z * tile.CellM,
                                         out int row, out int col);
                        uint carried = wide ? wideCodes[z, x] : codes[z, x];
                        if (carried != TileCodec.FarSquare(stand, row, col, Span, which)) wrong++;
                    }
                Assert.That(wrong, Is.Zero, which + ": every post carries its square");
            }
            Assert.That(() => TileCodec.EncodeFar(stand, TileLayer.Stand, grid, id), Throws.ArgumentException, "only a far layer is encoded far");
        }

        /// <summary>A world made before WG.2c, its stand one byte a cell, gives the far squares the same world made after gives.</summary>
        [Test]
        public void AFarSquareOfAOneByteStandIsTheSameAsOfItsTwoByteTwin()
        {
            byte oldBlackbutt = (byte)((1 << 5) | 24), oldBangalay = (byte)((2 << 5) | 10);
            Func<int, int, bool> blackbuttAt = (row, col) => (row * 7 + col * 3) % 11 == 0, bangalayAt = (row, col) => (row + col) % 13 == 0;
            RegionRaster old = TestRasters.FromCodes(TestRasters.MadeSide, TestRasters.MadeCellM, TestRasters.MadeExtentM, "old_stand", "stand",
                (row, col) => blackbuttAt(row, col) ? oldBlackbutt : bangalayAt(row, col) ? oldBangalay : 0u, null);
            RegionRaster twin = Stand((row, col) => blackbuttAt(row, col) ? StandCodes.Pack(PlantSpecies.Blackbutt, 30.0)
                                                  : bangalayAt(row, col) ? StandCodes.Pack(PlantSpecies.Bangalay, 12.5) : 0u);
            int compared = 0;
            for (int row = 0; row < old.Height; row += Span)
                for (int col = 0; col < old.Width; col += Span)
                    foreach (TileLayer which in new[] { TileLayer.FarStand, TileLayer.FarCount })
                    {
                        Assert.That(TileCodec.FarSquare(old, row, col, Span, which), Is.EqualTo(TileCodec.FarSquare(twin, row, col, Span, which)), which + " at " + row + ", " + col);
                        compared++;
                    }
            Assert.That(compared, Is.GreaterThan(100));
        }
    }
}
