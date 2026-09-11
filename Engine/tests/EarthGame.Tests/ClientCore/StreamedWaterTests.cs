using System;
using EarthGame.ClientCore;
using EarthGame.Engine;
using NUnit.Framework;

namespace EarthGame.Tests.ClientCore
{
    /// <summary>The water a client wades in is the server's own surface, put back together from the streamed ground and depth (M1.5d promise 1).</summary>
    public sealed class StreamedWaterTests
    {
        private const int Side = 41;
        private const double Cell = 4.0;
        private const double Extent = 160.0;

        /// <summary>A gentle plane with a bowl three metres deep in its middle.</summary>
        private static float GroundLaw(int row, int col)
        {
            double dr = row - 20, dc = col - 20;
            return (float)(10.0 + 0.05 * col - 0.02 * row - Math.Max(0.0, 3.0 - 0.4 * Math.Sqrt(dr * dr + dc * dc)));
        }

        /// <summary>A lake standing at 11 m in the bowl, and the ground's own height everywhere else.</summary>
        private static float SurfaceLaw(int row, int col)
        {
            double dr = row - 20, dc = col - 20;
            float ground = GroundLaw(row, col);
            return Math.Sqrt(dr * dr + dc * dc) < 10.0 && ground < 11f ? 11f : ground;
        }

        private static ReceivedTile Received(EncodedTile encoded, TileId id, TileLayer layer) => new ReceivedTile
        {
            Id = id,
            Layer = layer,
            Posts = encoded.Posts,
            CellM = encoded.CellM,
            OriginEast = encoded.OriginEast,
            OriginNorth = encoded.OriginNorth,
            Crc32 = encoded.Crc32,
            Heights = TileCodec.Unpack(encoded.Bytes, encoded.Posts),
        };

        /// <summary>The ground and depth tiles a client would hold of the whole of a small world.</summary>
        private static void Hold(RegionRaster groundRaster, RegionRaster surfaceRaster, out TileHeightfield ground, out TileHeightfield depth)
        {
            Heightfield server = new Heightfield(groundRaster);
            TileGrid grid = new TileGrid(Extent, 80.0);
            ground = new TileHeightfield(grid);
            depth = new TileHeightfield(grid);
            for (int ix = 0; ix < 2; ix++)
                for (int iz = 0; iz < 2; iz++)
                {
                    TileId id = new TileId(ix, iz);
                    ground.Add(Received(TileCodec.Encode(server, grid, id), id, TileLayer.Ground));
                    depth.Add(Received(TileCodec.EncodeDepth(surfaceRaster, server, grid, id), id, TileLayer.WaterDepth));
                }
        }

        [Test]
        public void TheWaterIsTheServersSurfaceToTheCentimetreAndNoneWhereItIsDry()
        {
            RegionRaster groundRaster = TestRasters.FromLaw(Side, Cell, Extent, "ground", GroundLaw);
            RegionRaster surfaceRaster = TestRasters.FromLaw(Side, Cell, Extent, "surface", SurfaceLaw);
            Hold(groundRaster, surfaceRaster, out TileHeightfield ground, out TileHeightfield depth);
            StreamedWater water = new StreamedWater(ground, depth);
            Heightfield serverGround = new Heightfield(groundRaster), serverSurface = new Heightfield(surfaceRaster);
            Random rng = new Random(7);
            int wet = 0, dry = 0;
            for (int i = 0; i < 2000; i++)
            {
                double east = -79.0 + rng.NextDouble() * 158.0, north = -79.0 + rng.NextDouble() * 158.0;
                double over = serverSurface.HeightAt(east, north) - serverGround.HeightAt(east, north);
                double client = water.HeightAt(east, north);
                if (over > 0.02)
                {
                    wet++;
                    Assert.That(client, Is.EqualTo(serverSurface.HeightAt(east, north)).Within(0.011), "the water at " + east + ", " + north);
                }
                else if (over <= 0.0)
                {
                    dry++;
                    Assert.That(double.IsNaN(client), Is.True, "dry at " + east + ", " + north + " yet water at " + client);
                }
            }
            Assert.That(wet, Is.GreaterThan(50), "the lake was asked about");
            Assert.That(dry, Is.GreaterThan(50), "and so was the dry ground round it");
        }

        [Test]
        public void WithoutADepthTileTheSeaStandsAtTheDatumAsBefore()
        {
            RegionRaster seabed = TestRasters.FromLaw(Side, Cell, Extent, "seabed", (row, col) => col < 20 ? -1.5f : 2f);
            Hold(seabed, seabed, out TileHeightfield ground, out TileHeightfield _);
            StreamedWater noDepth = new StreamedWater(ground, null);
            Assert.That(noDepth.HeightAt(-60.0, 0.0), Is.EqualTo(Heightfield.SeaLevelM), "ground below the datum, and no depth held: the sea");
            Assert.That(double.IsNaN(noDepth.HeightAt(60.0, 0.0)), Is.True, "ground above it is dry");
            StreamedWater emptyDepth = new StreamedWater(ground, new TileHeightfield(new TileGrid(Extent, 80.0)));
            Assert.That(emptyDepth.HeightAt(-60.0, 0.0), Is.EqualTo(Heightfield.SeaLevelM), "a depth field that holds none of this tile yet");
        }

        [Test]
        public void WhereNoGroundIsHeldThereIsNoWater()
        {
            TileGrid grid = new TileGrid(Extent, 80.0);
            StreamedWater water = new StreamedWater(new TileHeightfield(grid), new TileHeightfield(grid));
            Assert.That(double.IsNaN(water.HeightAt(0.0, 0.0)), Is.True);
            Assert.That(() => new StreamedWater(null, null), Throws.ArgumentNullException);
        }
    }
}
