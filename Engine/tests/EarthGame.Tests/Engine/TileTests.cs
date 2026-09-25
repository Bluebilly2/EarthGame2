using System;
using System.IO;
using EarthGame.Engine;
using NUnit.Framework;

namespace EarthGame.Tests.Engine
{
    /// <summary>The tile grid names ground unambiguously and the codec carries it to the centimetre.</summary>
    public sealed class TileTests
    {
        private sealed class Slope : IHeightSource
        {
            public double HeightAt(double east, double north) => 10.0 + east * 0.01 + Math.Sin(north * 0.05) * 3.0;
        }

        private static Heightfield Tiny() => new Heightfield(RegionRaster.Load(TestPaths.Fixture("raster", "tiny.json")));

        [Test]
        public void TheGridCoversTheRegionInWholeTiles()
        {
            TileGrid grid = new TileGrid(Region.Bherwerre.ExtentM);
            Assert.That(grid.TilesPerSide, Is.EqualTo(8));
            Assert.That(grid.ForPosition(-4000.0, -4000.0), Is.EqualTo(new TileId(0, 0)), "the south-west corner is tile 0,0");
            Assert.That(grid.ForPosition(3999.9, 3999.9), Is.EqualTo(new TileId(7, 7)));
            Assert.That(grid.ForPosition(4000.0, 4000.0), Is.EqualTo(new TileId(7, 7)), "the far edge belongs to the last tile");
            Assert.That(grid.ForPosition(-2409.6, -2112.7), Is.EqualTo(new TileId(1, 1)), "the wake");
            grid.Origin(new TileId(1, 1), out double east, out double north);
            Assert.That(east, Is.EqualTo(-3000.0));
            Assert.That(north, Is.EqualTo(-3000.0));
            Assert.That(grid.Around(new TileId(1, 1)).Count, Is.EqualTo(9));
            Assert.That(grid.Around(new TileId(0, 0)).Count, Is.EqualTo(4), "a corner tile has three neighbours");
            Assert.That(grid.Around(new TileId(1, 1))[0], Is.EqualTo(new TileId(1, 1)), "the centre first");
            Assert.That(() => new TileGrid(8000.0, 3000.0), Throws.ArgumentException, "tiles must divide the region");
            Assert.That(TileGrid.SizeFor(8000.0), Is.EqualTo(1000.0));
            Assert.That(TileGrid.SizeFor(40.0), Is.EqualTo(40.0), "a region smaller than a kilometre is one tile");
            Assert.That(new TileGrid(40.0).TilesPerSide, Is.EqualTo(1));
        }

        [Test]
        public void TheCodecRoundTripsToTheCentimetre()
        {
            const int posts = 251;
            float[,] heights = new float[posts, posts];
            Slope slope = new Slope();
            for (int z = 0; z < posts; z++)
                for (int x = 0; x < posts; x++)
                    heights[z, x] = (float)slope.HeightAt(x * 4.0, z * 4.0);
            byte[] packed = TileCodec.Pack(heights, posts);
            Assert.That(packed.Length, Is.LessThan(posts * posts * 2 / 4), "row deltas of smooth ground deflate well: " + packed.Length + " bytes");
            Assert.That(packed.Length, Is.GreaterThan(0));
            float[,] back = TileCodec.Unpack(packed, posts);
            for (int z = 0; z < posts; z += 7)
                for (int x = 0; x < posts; x += 5)
                    Assert.That(back[z, x], Is.EqualTo(heights[z, x]).Within(0.005f), "post " + z + "," + x);
        }

        [Test]
        public void AHeightOrAStepBeyondWhatATileCarriesIsRefusedNotClamped()
        {
            // Version 3 (WG.2): a plateau at 700 m and a trench at -4000 m are carried; version 2 stopped at 327 m either way.
            float[,] ends = { { 700.25f, 700.5f }, { -4000f, -3999.75f } };
            float[,] back = TileCodec.Unpack(TileCodec.Pack(ends, 2), 2);
            Assert.That(back[0, 0], Is.EqualTo(700.25f).Within(0.005f), "the Kangaroo Valley's plateau is carried");
            Assert.That(back[0, 1], Is.EqualTo(700.5f).Within(0.005f));
            Assert.That(back[1, 0], Is.EqualTo(-4000f).Within(0.005f), "and the sea's floor");
            Assert.That(() => TileCodec.Pack(new float[,] { { 12000.5f, 12000.5f }, { 0f, 0f } }, 2), Throws.TypeOf<InvalidDataException>(), "a height that is no height on Earth");
            Assert.That(() => TileCodec.Pack(new float[,] { { 0f, 0f }, { -12001f, -12001f } }, 2), Throws.TypeOf<InvalidDataException>(), "a depth that is none");
            Assert.That(() => TileCodec.Pack(new float[,] { { 200f, -200f }, { 0f, 0f } }, 2), Throws.TypeOf<InvalidDataException>(),
                        "a step between neighbours that no 16-bit number of centimetres holds, though both heights are in range");
            Assert.That(() => TileCodec.Pack(new float[,] { { float.NaN, 0f }, { 0f, 0f } }, 2), Throws.TypeOf<InvalidDataException>());
        }

        [Test]
        public void EncodeSamplesTheRasterAtItsOwnCells()
        {
            // A 40 m region of 10 m cells, tiles of 20 m: tile (1, 0) starts at east 0, north -20 and has 3 posts.
            TileGrid grid = new TileGrid(40.0, 20.0);
            Heightfield tiny = Tiny();
            EncodedTile tile = TileCodec.Encode(tiny, grid, new TileId(1, 0));
            Assert.That(tile.Posts, Is.EqualTo(3));
            Assert.That(tile.OriginEast, Is.EqualTo(0.0));
            Assert.That(tile.OriginNorth, Is.EqualTo(-20.0));
            Assert.That(tile.Crc32, Is.EqualTo(Crc32.Compute(tile.Bytes)));
            float[,] back = TileCodec.Unpack(tile.Bytes, tile.Posts);
            // Fixture law: 100 + row + 10·col; tile (1,0)'s south-west post is raster row 4, col 2.
            Assert.That(back[0, 0], Is.EqualTo(100f + 4f + 20f).Within(0.005f));
            Assert.That(back[2, 2], Is.EqualTo(100f + 2f + 40f).Within(0.005f), "row 2, col 4: two posts north and east of the origin");
            Assert.That(back[2, 0], Is.EqualTo(100f + 2f + 20f + 5f).Within(0.005f), "the bump at the raster's centre cell is this tile's north-west post");
            Assert.That(() => TileCodec.Encode(tiny, grid, new TileId(2, 0)), Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        /// <summary>A pond 1.25 m deep over part of the made coast's plain; the surface is the ground everywhere else.</summary>
        private static float PondSurface(int row, int col)
        {
            float ground = TestRasters.MadeCoastHeight(row, col);
            bool pond = row >= 60 && row <= 80 && col >= 60 && col <= 80;
            return pond ? ground + 1.25f : ground;
        }

        /// <summary>
        /// The water travels as the depth over the tile's own ground (M1.4b promise 2): zero wherever the ground is
        /// dry, so deflate is left with the wet part alone, and the client puts the surface back to the centimetre.
        /// </summary>
        [Test]
        public void WaterTravelsAsDepthOverTheGroundAndCostsAFractionOfASurface()
        {
            Assert.That(TileCodec.Version, Is.EqualTo(4), "version 3 carries any height on Earth (WG.2), version 4 the stand's two-byte codes (WG.2c)");
            RegionRaster groundRaster = TestRasters.MadeCoast();
            RegionRaster surface = TestRasters.FromLaw(TestRasters.MadeSide, TestRasters.MadeCellM, TestRasters.MadeExtentM, "surface", PondSurface);
            Heightfield ground = new Heightfield(groundRaster);
            TileGrid grid = new TileGrid(TestRasters.MadeExtentM);
            TileId id = new TileId(0, 0);

            EncodedTile depthTile = TileCodec.EncodeDepth(surface, ground, grid, id);
            Assert.That(depthTile.Layer, Is.EqualTo(TileLayer.WaterDepth));
            Assert.That(depthTile.Posts, Is.EqualTo(TestRasters.MadeSide));
            float[,] depth = TileCodec.Unpack(depthTile.Bytes, depthTile.Posts);
            float[,] groundPosts = TileCodec.Unpack(TileCodec.Encode(ground, grid, id).Bytes, depthTile.Posts);

            // The tile's rows run north from its origin; the raster's run south from its top.
            int pondRow = TestRasters.MadeSide - 1 - 70, dryRow = TestRasters.MadeSide - 1 - 100;
            Assert.That(depth[pondRow, 70], Is.EqualTo(1.25f).Within(0.01f), "the pond is as deep as it was made");
            Assert.That(depth[dryRow, 70], Is.EqualTo(0f), "dry ground carries no depth at all");
            double worst = 0.0;
            for (int z = 0; z < depthTile.Posts; z++)
                for (int x = 0; x < depthTile.Posts; x++)
                {
                    double put = groundPosts[z, x] + depth[z, x];
                    double truth = PondSurface(TestRasters.MadeSide - 1 - z, x);
                    worst = Math.Max(worst, Math.Abs(put - truth));
                }
            Assert.That(worst, Is.LessThanOrEqualTo(0.02), "the surface the client puts back is off by " + worst.ToString("0.000") + " m");

            EncodedTile asItsOwnSurface = TileCodec.Encode(new Heightfield(surface), grid, id);
            Assert.That(depthTile.Bytes.Length * 4, Is.LessThan(asItsOwnSurface.Bytes.Length),
                "depth " + depthTile.Bytes.Length + " bytes against a surface of its own at " + asItsOwnSurface.Bytes.Length);
        }

        /// <summary>A code layer is carried as codes, never interpolated: a lake's id averaged with the dry ground beside it is neither.</summary>
        [Test]
        public void ACodeLayerRoundTripsExactlyAndPacksSmall()
        {
            RegionRaster water = TestRasters.FromCodes(TestRasters.MadeSide, TestRasters.MadeCellM, TestRasters.MadeExtentM, "made_water", "water",
                (row, col) => row >= 150 ? 7u : (row >= 60 && row <= 80 && col >= 60 && col <= 80 ? 5u : 0u), null);
            TileGrid grid = new TileGrid(TestRasters.MadeExtentM);
            TileId id = new TileId(0, 0);

            EncodedTile tile = TileCodec.EncodeCodes(water, TileLayer.WaterClass, grid, id);
            Assert.That(tile.Layer, Is.EqualTo(TileLayer.WaterClass));
            byte[,] codes = TileCodec.UnpackCodes(tile.Bytes, tile.Posts);
            int lake = 0, sea = 0, dry = 0;
            for (int z = 0; z < tile.Posts; z++)
                for (int x = 0; x < tile.Posts; x++)
                {
                    uint truth = water.Code(TestRasters.MadeSide - 1 - z, x);
                    Assert.That(codes[z, x], Is.EqualTo((byte)truth), "post " + z + "," + x);
                    if (truth == 5) lake++;
                    else if (truth == 7) sea++;
                    else dry++;
                }
            Assert.That(lake, Is.EqualTo(21 * 21));
            Assert.That(sea, Is.GreaterThan(0));
            Assert.That(dry, Is.GreaterThan(0));
            Assert.That(tile.Bytes.Length, Is.LessThan(TileCodec.Encode(new Heightfield(TestRasters.MadeCoast()), grid, id).Bytes.Length / 4),
                "codes with long runs pack far smaller than metres: " + tile.Bytes.Length + " bytes");
            Assert.That(() => TileCodec.EncodeCodes(TestRasters.MadeCoast(), TileLayer.WaterClass, grid, id), Throws.ArgumentException,
                "a layer of metres is not a code layer");
            Assert.That(() => TileCodec.UnpackCodes(tile.Bytes, tile.Posts - 1), Throws.TypeOf<InvalidDataException>(), "a square that is not the one packed");
        }

        [Test]
        public void CorruptOrShortDataIsRefused()
        {
            float[,] heights = new float[3, 3];
            byte[] packed = TileCodec.Pack(heights, 3);
            Assert.That(() => TileCodec.Unpack(packed, 4), Throws.TypeOf<InvalidDataException>(), "too few posts in the data");
            Assert.That(() => TileCodec.Unpack(packed, 2), Throws.TypeOf<InvalidDataException>(), "too many posts in the data");
            heights[1, 1] = float.NaN;
            Assert.That(() => TileCodec.Pack(heights, 3), Throws.TypeOf<InvalidDataException>());
        }

        [Test]
        public void Crc32MatchesThePublishedCheckValue()
        {
            Assert.That(Crc32.Compute(System.Text.Encoding.ASCII.GetBytes("123456789")), Is.EqualTo(0xCBF43926u));
            Assert.That(Crc32.Compute(Array.Empty<byte>()), Is.EqualTo(0u));
        }
    }
}
