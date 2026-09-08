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
            float[,] back = TileCodec.Unpack(packed, posts);
            for (int z = 0; z < posts; z += 7)
                for (int x = 0; x < posts; x += 5)
                    Assert.That(back[z, x], Is.EqualTo(heights[z, x]).Within(0.005f), "post " + z + "," + x);
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
