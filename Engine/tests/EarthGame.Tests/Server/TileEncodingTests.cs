using System;
using EarthGame.Engine;
using EarthGame.Server;
using NUnit.Framework;

namespace EarthGame.Tests.Server
{
    /// <summary>
    /// The dedicated server's start encodes every tile of the region across the machine's cores (WG.2b's W4, 2026-09-23), each
    /// made from the world's layers alone: every tile is byte for byte the one a request would have made on its own, over a
    /// 4 km world of sixteen tiles with every layer the tiles carry, the far forest's among them.
    /// </summary>
    public sealed class TileEncodingTests
    {
        private const double ExtentM = 4000.0, CellM = 10.0;
        private const int Side = 401;

        private static readonly Region FixtureRegion = new Region("fixture", "Fixture", Region.Bherwerre.CentreLatitudeDeg,
            Region.Bherwerre.CentreLongitudeDeg, ExtentM, 237, 8.0);

        private static float Ground(int row, int col) => 20f + col * 0.05f + (Side - 1 - row) * 0.03f + (float)Math.Sin(row * 0.3) * 0.7f;

        private static WorldState World()
        {
            RegionRaster ground = TestRasters.FromLaw(Side, CellM, ExtentM, "enc_ground", Ground);
            RegionRaster surface = TestRasters.FromLaw(Side, CellM, ExtentM, "enc_surface", (r, c) => Ground(r, c) + (c < 40 ? 1.5f : 0f));
            RegionRaster classes = TestRasters.FromCodes(Side, CellM, ExtentM, "enc_water", "water", (r, c) => c < 40 ? (uint)WaterClass.Lake : 0u, null);
            RegionRaster cover = TestRasters.FromCodes(Side, CellM, ExtentM, "enc_cover", "cover", (r, c) => (uint)(1 + (r * 7 + c * 3) % 12), null);
            RegionRaster stand = TestRasters.FromCodes(Side, CellM, ExtentM, "enc_stand", "stand",
                (r, c) => (r * 5 + c * 11) % 7 == 0 ? (1u << 5) | (uint)(8 + (r + c) % 20) : 0u, null);
            RegionRaster loose = TestRasters.FromCodes(Side, CellM, ExtentM, "enc_loose", "loose", (r, c) => (uint)((r + 2 * c) % 256), null);
            RegionRaster stone = TestRasters.FromCodes(Side, CellM, ExtentM, "enc_stone", "stone", (r, c) => (uint)(1 + (r / 50 + c / 50) % 3), null);
            return new WorldState(1, FixtureRegion, FixtureRegion.WakeClock(), new Heightfield(ground), 0, null, new WorldWater(surface, classes),
                cover, stand, loose, stone);
        }

        [Test]
        public void EveryTileEncodedAtTheStartIsTheOneARequestWouldHaveMade()
        {
            WorldState world = World();
            var atStart = new TileService(world);
            int count = atStart.EncodeAll();
            var onRequest = new TileService(world);
            int compared = 0, layers = 0;
            foreach (TileLayer layer in TileLayers.All)
            {
                if (!atStart.Serves(layer)) continue;
                layers++;
                for (int iz = 0; iz < atStart.Grid.TilesPerSide; iz++)
                    for (int ix = 0; ix < atStart.Grid.TilesPerSide; ix++)
                    {
                        var id = new TileId(ix, iz);
                        Assert.That(atStart.Encoded(id, layer).Bytes, Is.EqualTo(onRequest.Encoded(id, layer).Bytes), layer + " tile " + ix + "," + iz);
                        compared++;
                    }
            }
            Assert.That(layers, Is.EqualTo(TileLayers.All.Length), "every layer the tiles carry is served by this world");
            Assert.That(compared, Is.EqualTo(count), "and every tile the start made was compared");
        }
    }
}
