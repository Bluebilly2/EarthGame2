using System;
using EarthGame.ClientCore;
using EarthGame.Engine;
using NUnit.Framework;

namespace EarthGame.Tests.World
{
    /// <summary>
    /// A streamed tile's trees, sticks and cobbles placed for drawing (M1.6a promises 3 and 5): where the server put
    /// each, to the centimetre, on the ground the tile carries, and each cell drawn by one tile only.
    /// </summary>
    public sealed class StandPreparationTests
    {
        private static readonly TileGrid Grid = new TileGrid(8000.0);
        private const int Posts = 251;
        private const double Cell = 4.0;

        private static ReceivedTile Tile(TileId id, TileLayer layer, Func<int, int, byte> codes = null, Func<int, int, float> heights = null)
        {
            Grid.Origin(id, out double east, out double north);
            ReceivedTile tile = new ReceivedTile { Id = id, Layer = layer, Posts = Posts, CellM = Cell, OriginEast = east, OriginNorth = north, Crc32 = 7u };
            if (codes != null)
            {
                tile.Codes = new byte[Posts, Posts];
                for (int z = 0; z < Posts; z++)
                    for (int x = 0; x < Posts; x++) tile.Codes[z, x] = codes(z, x);
            }
            if (heights != null)
            {
                tile.Heights = new float[Posts, Posts];
                for (int z = 0; z < Posts; z++)
                    for (int x = 0; x < Posts; x++) tile.Heights[z, x] = heights(z, x);
            }
            return tile;
        }

        /// <summary>Ground rising a centimetre a metre to the east, which bilinear reading returns exactly.</summary>
        private static ReceivedTile Ground(TileId id) => Tile(id, TileLayer.Ground, heights: (z, x) => (float)(10.0 + 0.01 * x * Cell));

        [Test]
        public void ATreeIsDrawnWhereTheServerPutItToTheCentimetre()
        {
            TileId id = new TileId(3, 5);
            byte code = StandCodes.Pack(PlantSpecies.Blackbutt, 30.0);
            ReceivedTile stand = Tile(id, TileLayer.Stand, (z, x) => z == 20 && x == 10 ? code : (byte)0);
            PreparedStand prepared = StandPreparation.Prepare(stand, null, Ground(id), Grid);

            Assert.That(prepared.Trees.Length, Is.EqualTo(1));
            StandTree tree = prepared.Trees[0];
            TileCodec.CellOf(Grid.ExtentM, Cell, stand.OriginEast + 10 * Cell, stand.OriginNorth + 20 * Cell, out int row, out int col);
            StandLayout.Place(row, col, StandLayout.Kind.Trunk, 0, 400, out int eastCm, out int northCm, out int yaw);
            Assert.That(tree.East, Is.EqualTo((float)(stand.OriginEast + 10 * Cell + eastCm / 100.0)), "east, as the layout puts it");
            Assert.That(tree.North, Is.EqualTo((float)(stand.OriginNorth + 20 * Cell + northCm / 100.0)), "north");
            Assert.That(tree.YawDeg, Is.EqualTo((float)yaw));
            Assert.That(tree.Up, Is.EqualTo(10.0 + 0.01 * (tree.East - stand.OriginEast)).Within(1e-3), "on the ground the tile carries");
            Assert.That(tree.HeightM, Is.EqualTo(30.0f));
            Assert.That(tree.CrownM, Is.EqualTo((float)(PlantSpecies.Blackbutt.CrownShare * 30.0)).Within(1e-4), "the width the server spaced it by");
            Assert.That(StandCodes.Tall[tree.Tall], Is.SameAs(PlantSpecies.Blackbutt));
            Assert.That(tree.Variant, Is.InRange(0, StandPreparation.Variants - 1));
        }

        /// <summary>A tile's last row and column are the next tile's first, so only the region's last tiles draw theirs.</summary>
        [Test]
        public void EachCellIsDrawnByOneTileOnly()
        {
            byte code = StandCodes.Pack(PlantSpecies.CoastBanksia, 8.0);
            Func<int, int, byte> corners = (z, x) => (z == 0 && x == 0) || (z == Posts - 1 && x == Posts - 1) ? code : (byte)0;
            TileId inside = new TileId(3, 5), last = new TileId(Grid.TilesPerSide - 1, Grid.TilesPerSide - 1);
            Assert.That(StandPreparation.Prepare(Tile(inside, TileLayer.Stand, corners), null, Ground(inside), Grid).Trees.Length, Is.EqualTo(1),
                "the far corner belongs to the tile beyond");
            Assert.That(StandPreparation.Prepare(Tile(last, TileLayer.Stand, corners), null, Ground(last), Grid).Trees.Length, Is.EqualTo(2),
                "and at the region's edge no tile lies beyond");
        }

        [Test]
        public void SticksAndCobblesLieWhereTheLayoutSays()
        {
            TileId id = new TileId(2, 2);
            ReceivedTile stand = Tile(id, TileLayer.Stand, (z, x) => 0);
            ReceivedTile loose = Tile(id, TileLayer.Loose, (z, x) => z == 5 && x == 6 ? LooseCodes.Pack(3, 2) : (byte)0);
            PreparedStand prepared = StandPreparation.Prepare(stand, loose, Ground(id), Grid);
            Assert.That(prepared.Sticks.Length, Is.EqualTo(3));
            Assert.That(prepared.Cobbles.Length, Is.EqualTo(2));
            TileCodec.CellOf(Grid.ExtentM, Cell, stand.OriginEast + 6 * Cell, stand.OriginNorth + 5 * Cell, out int row, out int col);
            for (int k = 0; k < 3; k++)
            {
                StandLayout.Place(row, col, StandLayout.Kind.Stick, k, 400, out int e, out int n, out int yaw);
                Assert.That(prepared.Sticks[k].East, Is.EqualTo((float)(stand.OriginEast + 6 * Cell + e / 100.0)), "stick " + k);
                Assert.That(prepared.Sticks[k].North, Is.EqualTo((float)(stand.OriginNorth + 5 * Cell + n / 100.0)));
                Assert.That(prepared.Sticks[k].YawDeg, Is.EqualTo((float)yaw));
            }
            StandLayout.Place(row, col, StandLayout.Kind.Cobble, 1, 400, out int ce, out _, out _);
            Assert.That(prepared.Cobbles[1].East, Is.EqualTo((float)(stand.OriginEast + 6 * Cell + ce / 100.0)), "the second cobble of the cell");
            PreparedStand again = StandPreparation.Prepare(stand, loose, Ground(id), Grid);
            Assert.That(again.Sticks[2].East, Is.EqualTo(prepared.Sticks[2].East), "the same every time");
        }

        [Test]
        public void EveryTallPlantHasAFormAndNothingElseDoes()
        {
            for (int i = 0; i < StandCodes.Tall.Count; i++)
            {
                TreeForm form = StandForms.ForTall(i);
                Assert.That(form.Name, Is.EqualTo(StandCodes.Tall[i].Name));
                Assert.That(form.TrunkLength, Is.InRange(0.3f, 0.9f), form.Name);
                Assert.That(form.ClumpMin, Is.LessThanOrEqualTo(form.ClumpMax), form.Name);
                Assert.That(StandCodes.Tall[i].CrownShare, Is.GreaterThan(0.0), form.Name + " has a crown the world spaces it by");
            }
            Assert.That(StandForms.For(PlantSpecies.Lomandra), Is.Null, "a herb is not drawn as a tree");
        }

        [Test]
        public void TheStandAndTheGroundMustBeOneTiles()
        {
            ReceivedTile stand = Tile(new TileId(1, 1), TileLayer.Stand, (z, x) => 0);
            Assert.Throws<ArgumentException>(() => StandPreparation.Prepare(stand, null, Ground(new TileId(1, 2)), Grid));
        }
    }
}
