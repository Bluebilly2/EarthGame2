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

        /// <summary>
        /// A flat region with a bangalay 15 m tall on a scatter of cells; with <paramref name="oneByte"/>, its stand in the one-byte
        /// layout of a world made before WG.2c (plant 2 in the top three bits, twelve steps of 1.25 m in the low five).
        /// </summary>
        private static WorldState World(bool oneByte = false)
        {
            RegionRaster heights = TestRasters.FromLaw(Side, CellM, ExtentM, "wide", (row, col) => 12f);
            uint tree = oneByte ? (2u << 5) | 12u : StandCodes.Pack(PlantSpecies.Bangalay, 15.0);
            RegionRaster stand = TestRasters.FromCodes(Side, CellM, ExtentM, "wide_stand", "stand",
                (row, col) => (row * 7 + col * 3) % 29 == 0 ? tree : 0u, null, oneByte ? "u8" : "u16");
            return new WorldState(1347UL, Wide, Wide.WakeClock(), new Heightfield(heights), 0, null, null, null, stand);
        }

        /// <summary>A client joined to a server of the world, three seconds of updates later.</summary>
        private static GameClient Joined(WorldState world)
        {
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
            return client;
        }

        [Test]
        public void AClientIsSentTheWholeRegionsFarLayersAndKeepsThem()
        {
            WorldState world = World();
            GameClient client = Joined(world);
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
                                if (far.CodeAt(z, x) != expected) wrong++;
                                if (which == TileLayer.FarCount) trees += far.CodeAt(z, x);
                            }
                    }
            Assert.That(wrong, Is.Zero, "post for post, what arrived is what the stand's squares hold");
            Assert.That(trees, Is.GreaterThan(0), "and there are trees in it");
        }

        /// <summary>
        /// A world made before WG.2c, its stand one byte a cell, sends its trees in the two-byte layout: every stand tile and every
        /// far tile a client holds of it is, to the byte, the tile its twin made with the two-byte stand sends.
        /// </summary>
        [Test]
        public void AWorldMadeBeforeSendsItsTreesAsItsTwinMadeAfterDoes()
        {
            GameClient old = Joined(World(oneByte: true)), twin = Joined(World());
            Assert.That(old.State, Is.EqualTo(ClientState.Connected));
            Assert.That(twin.State, Is.EqualTo(ClientState.Connected));
            int compared = 0;
            foreach (TileLayer which in new[] { TileLayer.Stand, TileLayer.FarStand, TileLayer.FarCount })
                for (int iz = 0; iz < twin.Grid.TilesPerSide; iz++)
                    for (int ix = 0; ix < twin.Grid.TilesPerSide; ix++)
                    {
                        TileId id = new TileId(ix, iz);
                        ReceivedTile theirs = twin.Tiles.Holding(which, id), ours = old.Tiles.Holding(which, id);
                        if (theirs == null)
                        {
                            Assert.That(ours, Is.Null, which + " " + id + ": held by one client and not the other");
                            continue;
                        }
                        Assert.That(ours, Is.Not.Null, which + " " + id);
                        Assert.That(ours.Crc32, Is.EqualTo(theirs.Crc32), which + " " + id + ": the same bytes");
                        compared++;
                    }
            Assert.That(compared, Is.GreaterThan(2 * twin.Grid.TilesPerSide * twin.Grid.TilesPerSide), "every far tile of both layers and the stand round the founder");
        }

        [Test]
        public void AStandWhoseCellsDoNotMakeAFarSquareWholeServesNoFarLayer()
        {
            // Cells of 15 m make no 40 m square whole, so a far post could not take whole cells without counting a tree twice.
            RegionRaster heights = TestRasters.FromLaw(401, 15.0, ExtentM, "fifteen", (row, col) => 12f);
            RegionRaster stand = TestRasters.FromCodes(401, 15.0, ExtentM, "fifteen_stand", "stand", (row, col) => 0u, null, "u16");
            TileService tiles = new TileService(new WorldState(1347UL, Wide, Wide.WakeClock(), new Heightfield(heights), 0, null, null, null, stand));
            Assert.That(tiles.Serves(TileLayer.Stand), Is.True, "the stand itself is served");
            Assert.That(tiles.Serves(TileLayer.FarStand), Is.False);
            Assert.That(tiles.Serves(TileLayer.FarCount), Is.False);
            Assert.That(TileCodec.FarSpan(stand), Is.Zero);
            Assert.That(TileCodec.FarSpan(World().Stand), Is.EqualTo(4), "where cells of 10 m make it four a side");
        }
    }
}
