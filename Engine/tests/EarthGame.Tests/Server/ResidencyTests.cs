using System;
using System.Collections.Generic;
using System.IO;
using EarthGame.ClientCore;
using EarthGame.Engine;
using EarthGame.Server;
using EarthGame.Transport;
using NUnit.Framework;

namespace EarthGame.Tests.Server
{
    /// <summary>
    /// What a client keeps as it walks (M1.4b promise 6). A four-kilometre region of sixteen tiles, so a founder
    /// can cross it and leave tiles behind: the bound holds, the ground underfoot is never let go, and a tile
    /// walked back to is answered from the disk cache rather than the wire.
    /// </summary>
    public sealed class ResidencyTests
    {
        private const double ExtentM = 4000.0, CellM = 10.0;
        private const int Side = 401;

        private static readonly Region FixtureRegion = new Region("fixture", "Fixture", Region.Bherwerre.CentreLatitudeDeg,
            Region.Bherwerre.CentreLongitudeDeg, ExtentM, 237, 8.0, Region.Bherwerre.CentreLatitudeDeg, Region.Bherwerre.CentreLongitudeDeg);

        /// <summary>A plain that rises east and north, so no two tiles pack alike.</summary>
        private static float GroundLaw(int row, int col) => 20f + col * 0.05f + (Side - 1 - row) * 0.03f;

        private static RegionRaster GroundRaster() => TestRasters.FromLaw(Side, CellM, ExtentM, "wide", GroundLaw);

        private static WorldState World()
        {
            RegionRaster ground = GroundRaster();
            RegionRaster surface = TestRasters.FromLaw(Side, CellM, ExtentM, "wide_surface", (row, col) => GroundLaw(row, col) + (col < 40 ? 1.5f : 0f));
            RegionRaster classes = TestRasters.FromCodes(Side, CellM, ExtentM, "wide_water", "water", (row, col) => col < 40 ? (uint)WaterClass.Lake : 0u, null);
            return new WorldState(1, FixtureRegion, FixtureRegion.WakeClock(), new Heightfield(ground), 0, null, new WorldWater(surface, classes));
        }

        private string _cacheDir;
        private GameServer _server;
        private GameClient _client;
        private long _ms;

        [SetUp]
        public void SetUp()
        {
            _cacheDir = Path.Combine(Path.GetTempPath(), "EarthGame2.Tests", "residency", Guid.NewGuid().ToString("N"));
            InMemoryTransport.CreatePair(out IServerTransport st, out IClientTransport ct);
            _server = new GameServer(new ServerConfig(), st, World());
            _server.Listen(1);
            _client = new GameClient(InMemoryTransport.CreateClient(st), new DiskTileCache(_cacheDir));
            _client.Connect("memory", 1, "William", "");
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_cacheDir)) Directory.Delete(_cacheDir, true);
        }

        private void Pump(int rounds = 4)
        {
            for (int i = 0; i < rounds; i++)
            {
                _client.Update(_ms);
                _server.Update(0.05);
                _client.Update(_ms);
                _ms += 50;
            }
        }

        [Test]
        public void WalkingTheRegionKeepsTheBoundAndNeverDropsTheGroundUnderfoot()
        {
            Pump(8);
            Assert.That(_client.Grid.TilesPerSide, Is.EqualTo(4), "four kilometres is four tiles a side");
            _client.Tiles.MaxTilesPerLayer = 9;

            List<(TileLayer Layer, TileId Id)> dropped = new List<(TileLayer, TileId)>();
            _client.TileDropped += (layer, id) => dropped.Add((layer, id));

            // West to east along the middle, asking for the nine tiles around each step as a walk would.
            int mostGroundHeld = 0;
            for (double east = -1500.0; east <= 1500.0; east += 250.0)
            {
                _client.RequestTilesAround(east, 0.0);
                Pump(6);
                TileId under = _client.Grid.ForPosition(east, 0.0);
                Assert.That(_client.Tiles.Holds(under), Is.True, "the ground under the founder at east " + east);
                foreach (TileLayer layer in TileLayers.All)
                    Assert.That(_client.Tiles.CountOf(layer), Is.LessThanOrEqualTo(9), layer + " held beyond the bound at east " + east);
                mostGroundHeld = Math.Max(mostGroundHeld, _client.Tiles.CountOf(TileLayer.Ground));
            }
            Assert.That(mostGroundHeld, Is.EqualTo(9), "a walk across sixteen tiles fills the bound");
            Assert.That(dropped, Is.Not.Empty, "and lets tiles go behind it");
            foreach ((TileLayer layer, TileId id) in dropped)
                Assert.That(TileLayers.All, Does.Contain(layer), "every dropped tile names its layer: " + id);
        }

        [Test]
        public void ATileWalkedBackToComesFromTheDiskCache()
        {
            Pump(8);
            _client.Tiles.MaxTilesPerLayer = 9;
            _client.RequestTilesAround(-1500.0, 0.0);
            Pump(6);
            TileId west = _client.Grid.ForPosition(-1500.0, 0.0);
            Assert.That(_client.Tiles.Holds(west), Is.True);

            // Away, far enough that the western tile is let go, then back to it.
            _client.RequestTilesAround(1500.0, 0.0);
            Pump(8);
            Assert.That(_client.Tiles.Holds(west), Is.False, "the tile behind the founder was let go");

            long receivedBefore = _client.Tiles.BytesReceived;
            long servedBefore = _server.Tiles.BytesServed;
            _client.RequestTilesAround(-1500.0, 0.0);
            Pump(8);
            Assert.That(_client.Tiles.Holds(west), Is.True, "walking back holds it again");
            Assert.That(_client.Tiles.Held[west].FromCache, Is.True, "from the disk, not the wire");
            Assert.That(_client.Tiles.BytesReceived, Is.EqualTo(receivedBefore), "no chunk crossed the wire");
            Assert.That(_server.Tiles.BytesServed - servedBefore, Is.GreaterThan(0), "headers did");
        }
    }
}
