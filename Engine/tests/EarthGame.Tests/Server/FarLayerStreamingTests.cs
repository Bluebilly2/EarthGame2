using System;
using EarthGame.ClientCore;
using EarthGame.Engine;
using EarthGame.Server;
using EarthGame.Transport;
using NUnit.Framework;

namespace EarthGame.Tests.Server
{
    /// <summary>
    /// The far layers over the wire (M1.6d promises 2 and 7): a client joining a region of more tiles than it keeps of any
    /// layer is sent the far stand and the far count of every one of them, keeps them all, and holds what the world's
    /// stand says post for post.
    /// </summary>
    public sealed class FarLayerStreamingTests
    {
        private const double ExtentM = 6000.0;
        private const double CellM = 10.0;
        private const int Side = 601;

        private static readonly Region Wide = new Region("wide", "Wide", Region.Bherwerre.CentreLatitudeDeg,
            Region.Bherwerre.CentreLongitudeDeg, ExtentM, 237, 8.0);

        private static WorldState World()
        {
            RegionRaster heights = TestRasters.FromLaw(Side, CellM, ExtentM, "wide", (row, col) => 12f);
            RegionRaster stand = TestRasters.FromCodes(Side, CellM, ExtentM, "wide_stand", "stand",
                (row, col) => (row * 7 + col * 3) % 29 == 0 ? StandCodes.Pack(PlantSpecies.Bangalay, 15.0) : 0u, null);
            return new WorldState(1347UL, Wide, Wide.WakeClock(), new Heightfield(heights), 0, null, null, null, stand);
        }

        [Test]
        public void AClientIsSentTheWholeRegionsFarLayersAndKeepsThem()
        {
            WorldState world = World();
            InMemoryTransport.CreatePair(out IServerTransport st, out IClientTransport _);
            GameServer server = new GameServer(new ServerConfig(), st, world);
            server.Listen(1);
            GameClient client = new GameClient(InMemoryTransport.CreateClient(st));
            client.Connect("memory", 1, "William", "");
            long ms = 0;
            for (int i = 0; i < 60; i++)
            {
                client.Update(ms);
                server.Update(0.05);
                client.Update(ms);
                ms += 50;
            }
            Assert.That(client.State, Is.EqualTo(ClientState.Connected));

            int tiles = client.Grid.TilesPerSide * client.Grid.TilesPerSide;
            Assert.That(tiles, Is.GreaterThan(client.Tiles.MaxTilesPerLayer), "a region of more tiles than a client keeps of a layer round it");
            Assert.That(client.Tiles.CountOf(TileLayer.FarStand), Is.EqualTo(tiles), "every tile's far stand, kept through every trim");
            Assert.That(client.Tiles.CountOf(TileLayer.FarCount), Is.EqualTo(tiles), "and every tile's far count");
            Assert.That(client.Tiles.CountOf(TileLayer.Stand), Is.LessThanOrEqualTo(9), "while the stand is the nine tiles round the founder");

            int span = (int)Math.Round(TileLayers.FarCellM / CellM);
            int wrong = 0, trees = 0;
            foreach (TileLayer which in new[] { TileLayer.FarStand, TileLayer.FarCount })
                for (int iz = 0; iz < client.Grid.TilesPerSide; iz++)
                    for (int ix = 0; ix < client.Grid.TilesPerSide; ix++)
                    {
                        ReceivedTile far = client.Tiles.Holding(which, new TileId(ix, iz));
                        for (int z = 0; z < far.Posts; z++)
                            for (int x = 0; x < far.Posts; x++)
                            {
                                TileCodec.CellOf(ExtentM, CellM, far.OriginEast + x * far.CellM, far.OriginNorth + z * far.CellM, out int row, out int col);
                                uint expected = TileCodec.FarSquare(world.Stand, row, col, span, which);
                                if (far.Codes[z, x] != expected) wrong++;
                                if (which == TileLayer.FarCount) trees += far.Codes[z, x];
                            }
                    }
            Assert.That(wrong, Is.Zero, "post for post, what arrived is what the stand's squares hold");
            Assert.That(trees, Is.GreaterThan(0), "and there are trees in it");
        }

        [Test]
        public void AStandWhoseCellsDoNotMakeAFarSquareWholeServesNoFarLayer()
        {
            // Cells of 15 m make no 40 m square whole, so a far post could not take whole cells without counting a tree twice.
            RegionRaster heights = TestRasters.FromLaw(401, 15.0, ExtentM, "fifteen", (row, col) => 12f);
            RegionRaster stand = TestRasters.FromCodes(401, 15.0, ExtentM, "fifteen_stand", "stand", (row, col) => 0u, null);
            TileService tiles = new TileService(new WorldState(1347UL, Wide, Wide.WakeClock(), new Heightfield(heights), 0, null, null, null, stand));
            Assert.That(tiles.Serves(TileLayer.Stand), Is.True, "the stand itself is served");
            Assert.That(tiles.Serves(TileLayer.FarStand), Is.False);
            Assert.That(tiles.Serves(TileLayer.FarCount), Is.False);
            Assert.That(TileCodec.FarSpan(stand), Is.Zero);
            Assert.That(TileCodec.FarSpan(World().Stand), Is.EqualTo(4), "where cells of 10 m make it four a side");
        }
    }
}
