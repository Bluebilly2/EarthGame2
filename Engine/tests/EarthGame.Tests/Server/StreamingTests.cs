using System;
using System.IO;
using EarthGame.ClientCore;
using EarthGame.Engine;
using EarthGame.Server;
using EarthGame.Transport;
using NUnit.Framework;

namespace EarthGame.Tests.Server
{
    /// <summary>
    /// M1.B over the in-memory transport: tiles stream on join, a rejoin is answered from the cache, a dropped
    /// session keeps its body, the interest radius filters, PlayerLeft drops the mirror, and the digests the
    /// two ends compute agree. The ground is the tiny fixture (one 40 m tile of five posts).
    /// </summary>
    public sealed class StreamingTests
    {
        private static readonly Region FixtureRegion = new Region("fixture", "Fixture", Region.Bherwerre.CentreLatitudeDeg,
            Region.Bherwerre.CentreLongitudeDeg, 40.0, 237, 8.0);

        private static Heightfield Ground() => new Heightfield(RegionRaster.Load(TestPaths.Fixture("raster", "tiny.json")));

        private static WorldState World() => new WorldState(1, FixtureRegion, FixtureRegion.WakeClock(), Ground());

        private string _cacheDir;

        [SetUp]
        public void SetUp()
        {
            _cacheDir = Path.Combine(Path.GetTempPath(), "EarthGame2.Tests", "tiles", Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_cacheDir)) Directory.Delete(_cacheDir, true);
        }

        private sealed class Rig
        {
            public GameServer Server;
            public IServerTransport ServerTransport;
            public GameClient A;
            public GameClient B;
            public long Ms;

            public void Pump(int rounds = 3, double dt = 0.05)
            {
                for (int i = 0; i < rounds; i++)
                {
                    A?.Update(Ms);
                    B?.Update(Ms);
                    Server.Update(dt);
                    A?.Update(Ms);
                    B?.Update(Ms);
                    Ms += (long)(dt * 1000);
                }
            }

            public GameClient Join(string name, ITileCache cache = null)
            {
                IClientTransport ct = InMemoryTransport.CreateClient(ServerTransport);
                GameClient c = new GameClient(ct, cache);
                c.Connect("memory", 1, name, "");
                return c;
            }
        }

        private static Rig Start(WorldState world, ServerConfig config = null)
        {
            InMemoryTransport.CreatePair(out IServerTransport st, out IClientTransport ct);
            Rig rig = new Rig { ServerTransport = st };
            rig.Server = new GameServer(config ?? new ServerConfig(), st, world);
            rig.Server.Listen(1);
            return rig;
        }

        private static MoverState GroundedAt(Heightfield ground, double east, double north)
        {
            MoverState s = MoverState.AtRest(east, ground.HeightAt(east, north), north);
            s.Grounded = true;
            return s;
        }

        [Test]
        public void TilesStreamOnJoinAndTheClientBecomesInteractive()
        {
            Rig rig = Start(World());
            rig.A = rig.Join("William");
            rig.Pump(5);
            Assert.That(rig.A.State, Is.EqualTo(ClientState.Connected));
            Assert.That(rig.A.SnapshotApplied, Is.True, "the empty snapshot still ends");
            Assert.That(rig.A.TilesRequested, Is.True);
            Assert.That(rig.A.TilesOutstanding, Is.EqualTo(0));
            Assert.That(rig.A.IsInteractive, Is.True);
            Assert.That(rig.A.Tiles.Held.Count, Is.EqualTo(1), "a 40 m region is one tile");
            ReceivedTile tile = rig.A.Tiles.Held[new TileId(0, 0)];
            Assert.That(tile.Posts, Is.EqualTo(5));
            Assert.That(tile.CellM, Is.EqualTo(10.0).Within(1e-6));
            Assert.That(tile.OriginEast, Is.EqualTo(-20.0));
            Assert.That(tile.Heights[2, 2], Is.EqualTo(127f).Within(0.005f), "the bump at the centre, to the centimetre");
            Assert.That(tile.FromCache, Is.False);
            Assert.That(rig.Server.Tiles.BytesServed, Is.GreaterThan(0));
            Assert.That(rig.A.BytesReceived, Is.EqualTo(rig.Server.Sessions[0].Connection.BytesSent), "both ends count the same payload bytes");
        }

        /// <summary>The tiny fixture's own law, as its sidecar states it: 100 + row + 10 x col, and five more at the centre cell.</summary>
        private static float TinyGround(int row, int col) => 100f + row + 10f * col + (row == 2 && col == 2 ? 5f : 0f);

        /// <summary>The tiny fixture with two metres of water standing over its two western columns.</summary>
        private static WorldWater Water()
        {
            RegionRaster surface = TestRasters.FromLaw(5, 10.0, 40.0, "tiny_surface",
                (row, col) => TinyGround(row, col) + (col <= 1 ? 2f : 0f));
            RegionRaster classes = TestRasters.FromCodes(5, 10.0, 40.0, "tiny_water", "water",
                (row, col) => col <= 1 ? (uint)WaterClass.Lake : 0u, null);
            return new WorldWater(surface, classes);
        }

        /// <summary>The tiny fixture's cover: fresh water where the lake stands, grass elsewhere, wetter to the east.</summary>
        private static RegionRaster Cover() => TestRasters.FromCodes(5, 10.0, 40.0, "tiny_cover", "cover",
            (row, col) => GroundCovers.Pack(col <= 1 ? GroundCover.FreshWater : GroundCover.Grass, col % GroundCovers.Quarters), null);

        /// <summary>The tiny fixture's stand (M1.6a): one blackbutt on the centre row, a cell east of the middle.</summary>
        private static RegionRaster Stand() => TestRasters.FromCodes(5, 10.0, 40.0, "tiny_stand", "stand",
            (row, col) => row == 2 && col == 3 ? StandCodes.Pack(PlantSpecies.Blackbutt, 30.0) : 0u, null, "u16");

        /// <summary>What lies on it: sticks on the blackbutt's cell and its four neighbours, a cobble down the middle column.</summary>
        private static RegionRaster Loose() => TestRasters.FromCodes(5, 10.0, 40.0, "tiny_loose", "loose",
            (row, col) => LooseCodes.Pack(Math.Abs(row - 2) + Math.Abs(col - 3) <= 1 ? 3 : 0, col == 2 ? 1 : 0), null);

        /// <summary>Silcrete on every cell: the stone layer travels since BF.1, and a world with every layer has it.</summary>
        private static RegionRaster Stone() => TestRasters.FromCodes(5, 10.0, 40.0, "tiny_stone", "stone", (row, col) => 9u, null);

        private static WorldState WateredWorld() =>
            new WorldState(1, FixtureRegion, FixtureRegion.WakeClock(), Ground(), 0, null, Water(), Cover(), Stand(), Loose(), Stone());

        /// <summary>
        /// The water reaches the client beside the ground (M1.4b promises 3 to 5): the depth standing over the
        /// tile's own posts and the class of each, with the surface put back together where it stands. The join
        /// waits for the ground alone, which is what CANON ruling 11 measures.
        /// </summary>
        [Test]
        public void TheWaterArrivesBesideTheGroundAndTheSurfacePutsBackTogether()
        {
            Rig rig = Start(WateredWorld());
            DiskTileCache cache = new DiskTileCache(_cacheDir);
            rig.A = rig.Join("William", cache);
            rig.Pump(6);
            Assert.That(rig.A.IsInteractive, Is.True);
            Assert.That(rig.A.Tiles.RefusedCount, Is.Zero, "this world has every layer");
            Assert.That(rig.A.Tiles.CountOf(TileLayer.Ground), Is.EqualTo(1));
            Assert.That(rig.A.Tiles.CountOf(TileLayer.WaterDepth), Is.EqualTo(1));
            Assert.That(rig.A.Tiles.CountOf(TileLayer.WaterClass), Is.EqualTo(1));
            Assert.That(rig.A.Tiles.CountOf(TileLayer.GroundCover), Is.EqualTo(1));

            TileId id = new TileId(0, 0);
            ReceivedTile ground = rig.A.Tiles.Held[id];
            ReceivedTile depth = rig.A.Tiles.Holding(TileLayer.WaterDepth, id);
            ReceivedTile classes = rig.A.Tiles.Holding(TileLayer.WaterClass, id);
            Assert.That(depth.Heights, Is.Not.Null, "a layer of metres");
            Assert.That(classes.Codes, Is.Not.Null, "a layer of codes");
            Assert.That(classes.Heights, Is.Null);
            for (int z = 0; z < ground.Posts; z++)
                for (int x = 0; x < ground.Posts; x++)
                {
                    bool wet = x <= 1;
                    Assert.That(depth.Heights[z, x], Is.EqualTo(wet ? 2f : 0f).Within(0.01f), "depth at " + z + "," + x);
                    Assert.That(classes.Codes[z, x], Is.EqualTo(wet ? (byte)WaterClass.Lake : (byte)0), "class at " + z + "," + x);
                    double surface = ground.Heights[z, x] + depth.Heights[z, x];
                    double truth = TinyGround(ground.Posts - 1 - z, x) + (wet ? 2.0 : 0.0);
                    Assert.That(surface, Is.EqualTo(truth).Within(0.02), "the surface put back at " + z + "," + x);
                }

            // The wire carried the water for a fraction of what the ground cost, because dry posts are zero.
            Assert.That(depth.Crc32, Is.Not.EqualTo(ground.Crc32));
            Assert.That(Directory.Exists(Path.Combine(_cacheDir, "fixture", "water-depth")), Is.True, "each layer caches under its own name");
            Assert.That(cache.KnownCrc("fixture", TileLayer.WaterClass, id), Is.EqualTo(classes.Crc32));
        }

        /// <summary>
        /// What stands and what lies loose reach the client as layers of codes (M1.6a promise 4), post for post as the
        /// world holds them, each cached under its own name.
        /// </summary>
        [Test]
        public void WhatStandsAndLiesArrivesPostForPost()
        {
            Rig rig = Start(WateredWorld());
            DiskTileCache cache = new DiskTileCache(_cacheDir);
            rig.A = rig.Join("William", cache);
            rig.Pump(6);
            Assert.That(rig.A.Tiles.RefusedCount, Is.Zero, "this world has every layer");
            TileId id = new TileId(0, 0);
            ReceivedTile stand = rig.A.Tiles.Holding(TileLayer.Stand, id);
            ReceivedTile loose = rig.A.Tiles.Holding(TileLayer.Loose, id);
            Assert.That(stand.WideCodes, Is.Not.Null, "a layer of codes, two bytes a post");
            Assert.That(loose.Codes, Is.Not.Null, "a layer of codes");
            RegionRaster standTruth = Stand(), looseTruth = Loose();
            for (int z = 0; z < stand.Posts; z++)
                for (int x = 0; x < stand.Posts; x++)
                {
                    int row = stand.Posts - 1 - z;   // a tile's first row is its south edge, a raster's its north
                    Assert.That(stand.WideCodes[z, x], Is.EqualTo((ushort)standTruth.Code(row, x)), "stand at " + z + "," + x);
                    Assert.That(loose.Codes[z, x], Is.EqualTo((byte)looseTruth.Code(row, x)), "loose at " + z + "," + x);
                }
            Assert.That(StandCodes.SpeciesOf(stand.WideCodes[2, 3]), Is.SameAs(PlantSpecies.Blackbutt), "the blackbutt on the centre row");
            Assert.That(StandCodes.HeightOf(stand.WideCodes[2, 3]), Is.EqualTo(30.0).Within(StandCodes.HeightStepM));
            Assert.That(Directory.Exists(Path.Combine(_cacheDir, "fixture", "stand")), Is.True, "each layer caches under its own name");
            Assert.That(Directory.Exists(Path.Combine(_cacheDir, "fixture", "loose")), Is.True);
        }

        /// <summary>A world whose folder held no water: the client is told once and stops asking as it walks.</summary>
        [Test]
        public void AWorldWithoutWaterRefusesItOnceAndIsNotAskedAgain()
        {
            Rig rig = Start(World());
            rig.A = rig.Join("William");
            rig.Pump(6);
            Assert.That(rig.A.IsInteractive, Is.True, "the ground alone still makes a client interactive");
            Assert.That(rig.A.Tiles.CountOf(TileLayer.Ground), Is.EqualTo(1));
            Assert.That(rig.A.Tiles.CountOf(TileLayer.WaterDepth), Is.Zero);
            Assert.That(rig.A.Tiles.RefusedCount, Is.EqualTo(TileLayers.All.Length - 1), "every layer but the ground, of the one tile, once each");
            long served = rig.Server.Tiles.BytesServed;
            rig.A.RequestTilesAround(0.0, 0.0);
            rig.Pump(3);
            Assert.That(rig.Server.Tiles.BytesServed, Is.EqualTo(served), "nothing was asked for a second time");
            Assert.That(rig.A.Tiles.RefusedCount, Is.EqualTo(TileLayers.All.Length - 1));
        }

        [Test]
        public void ARejoinWithCachedTilesIsAnsweredByHeadersAlone()
        {
            Rig rig = Start(World());
            DiskTileCache cache = new DiskTileCache(_cacheDir);
            rig.A = rig.Join("William", cache);
            rig.Pump(5);
            Assert.That(rig.A.IsInteractive, Is.True);
            long servedFirst = rig.Server.Tiles.BytesServed;
            Assert.That(cache.KnownCrc("fixture", TileLayer.Ground, new TileId(0, 0)), Is.Not.EqualTo(0u), "the tile is on disk");

            rig.A.Disconnect("cable");
            rig.Pump(3);
            rig.A = rig.Join("William", cache);
            rig.Pump(5);
            Assert.That(rig.A.IsInteractive, Is.True);
            Assert.That(rig.A.Tiles.Held[new TileId(0, 0)].FromCache, Is.True);
            Assert.That(rig.A.Tiles.BytesReceived, Is.EqualTo(0), "no chunk crossed the wire");
            // One header a layer: the ground this client already holds, and every other layer, which a world built
            // from a bare heightfield has none of. No chunk of any of them crosses the wire.
            Assert.That(rig.Server.Tiles.BytesServed - servedFirst, Is.LessThan(50 * TileLayers.All.Length), "headers, nothing more");
        }

        [Test]
        public void EveryTileCanBeEncodedAheadOfTheFirstJoin()
        {
            Rig rig = Start(World());
            Assert.That(rig.Server.Tiles.EncodeAll(), Is.EqualTo(1), "a 40 m region is one tile");
            Assert.That(new GameServer(new ServerConfig(), rig.ServerTransport, new WorldState(7, Region.Bherwerre, Region.Bherwerre.WakeClock())).Tiles.EncodeAll(), Is.EqualTo(0), "no terrain, nothing to encode");
            rig.A = rig.Join("William");
            rig.Pump(5);
            Assert.That(rig.A.IsInteractive, Is.True);
        }

        [Test]
        public void AWorldWithoutGroundRefusesTilesAndStillBecomesInteractive()
        {
            Rig rig = Start(new WorldState(7, Region.Bherwerre, Region.Bherwerre.WakeClock()));
            rig.A = rig.Join("William");
            rig.Pump(5);
            // The far layers are asked for over the region's whole grid (M1.6d), every other layer over the nine tiles.
            int far = 0, near = 0;
            foreach (TileLayer layer in TileLayers.All)
                if (TileLayers.IsFar(layer)) far++; else near++;
            int tiles = rig.A.Grid.TilesPerSide * rig.A.Grid.TilesPerSide;
            Assert.That(rig.A.Tiles.RefusedCount, Is.EqualTo(9 * near + tiles * far), "the nine tiles around the wake of every near layer, and the whole region of every far one, refused");
            Assert.That(rig.A.Tiles.Held.Count, Is.EqualTo(0));
            Assert.That(rig.A.IsInteractive, Is.True, "refused is answered; the client does not wait");
        }

        [Test]
        public void ADroppedSessionKeepsItsBodyAndTheSameNameWakesThere()
        {
            Rig rig = Start(World());
            Heightfield ground = Ground();
            rig.A = rig.Join("William");
            rig.Pump(5);
            rig.A.SendMove(MoverInput.None, 45f, 0f, GroundedAt(ground, 2.0, 3.0));
            rig.Pump(3);
            Assert.That(rig.Server.Sessions[0].Corrections, Is.EqualTo(0));
            string digestBefore = rig.Server.Digest();

            rig.A.Disconnect("cable");
            rig.Pump(3);
            Assert.That(rig.Server.Sessions.Count, Is.EqualTo(0));
            Assert.That(rig.Server.PlayersToSave().Count, Is.EqualTo(1), "the body is remembered by name");
            Assert.That(rig.Server.PlayersToSave()[0].Body.East, Is.EqualTo(2.0));

            rig.A = rig.Join("William");
            rig.Pump(5);
            Assert.That(rig.A.Welcome.SpawnEast, Is.EqualTo(2.0));
            Assert.That(rig.A.Welcome.SpawnNorth, Is.EqualTo(3.0));
            Assert.That(rig.Server.Sessions[0].HasBody, Is.True);
            Assert.That(rig.Server.Sessions[0].YawDeg, Is.EqualTo(45f));
            Assert.That(rig.Server.Sessions[0].SessionId, Is.EqualTo(2u), "a new session, never the old id");
            Assert.That(digestBefore, Is.Not.EqualTo(rig.Server.Digest()), "the clock and the tick moved on");
        }

        [Test]
        public void ASecondHelloWithTheSameNameSupersedesTheFirst()
        {
            Rig rig = Start(World());
            rig.A = rig.Join("William");
            rig.Pump(5);
            Assert.That(rig.A.State, Is.EqualTo(ClientState.Connected));
            rig.B = rig.Join("William");
            rig.Pump(5);
            Assert.That(rig.Server.Sessions.Count, Is.EqualTo(1));
            Assert.That(rig.Server.Sessions[0].SessionId, Is.EqualTo(2u));
            Assert.That(rig.B.State, Is.EqualTo(ClientState.Connected));
            Assert.That(rig.A.State, Is.EqualTo(ClientState.Disconnected));
            Assert.That(rig.A.LastReason, Does.Contain("superseded"));
        }

        [Test]
        public void PlayerLeftDropsTheMirror()
        {
            Rig rig = Start(World());
            Heightfield ground = Ground();
            rig.A = rig.Join("William");
            rig.B = rig.Join("Guest");
            rig.Pump(5);
            rig.A.SendMove(MoverInput.None, 0f, 0f, GroundedAt(ground, 2.0, 3.0));
            rig.B.SendMove(MoverInput.None, 0f, 0f, GroundedAt(ground, -4.0, 1.0));
            rig.Pump(3);
            uint guest = rig.B.Welcome.SessionId;
            Assert.That(rig.A.Mirrors.ContainsKey(guest), Is.True);
            uint leftId = 0;
            rig.A.PlayerLeft += id => leftId = id;
            rig.B.Disconnect("bye");
            rig.Pump(3);
            Assert.That(leftId, Is.EqualTo(guest));
            Assert.That(rig.A.Mirrors.ContainsKey(guest), Is.False);
            Assert.That(rig.A.Others.ContainsKey(guest), Is.False);
        }

        [Test]
        public void BodiesBeyondTheInterestRadiusAreNotSent()
        {
            Rig rig = Start(World(), new ServerConfig { InterestRadiusM = 5.0 });
            Heightfield ground = Ground();
            rig.A = rig.Join("William");
            rig.B = rig.Join("Guest");
            rig.Pump(5);
            rig.A.SendMove(MoverInput.None, 0f, 0f, GroundedAt(ground, 2.0, 3.0));
            rig.B.SendMove(MoverInput.None, 0f, 0f, GroundedAt(ground, -4.0, 1.0));
            rig.Pump(4);
            Assert.That(rig.A.Others.ContainsKey(rig.B.Welcome.SessionId), Is.False, "6.3 m apart with a 5 m radius");
            Assert.That(rig.B.Others.ContainsKey(rig.A.Welcome.SessionId), Is.False);
            // Four metres east over forty reports, one per tick as the client sends them: a 2 m/s walk.
            for (int i = 1; i <= 40; i++)
            {
                rig.B.SendMove(MoverInput.Walk(1.0, 0.0), 90f, 0f, GroundedAt(ground, -4.0 + 0.1 * i, 1.0));
                rig.Pump(1);
            }
            rig.Pump(3);
            Assert.That(rig.Server.Sessions[1].Corrections, Is.EqualTo(0), "a 2 m/s walk is legal");
            Assert.That(rig.A.Others.ContainsKey(rig.B.Welcome.SessionId), Is.True, "2.8 m apart: seen");
            Assert.That(rig.B.Others.ContainsKey(rig.A.Welcome.SessionId), Is.True);
        }

        [Test]
        public void TheSnapshotCarriesTheBodiesThatExistedAtTheWelcome()
        {
            Rig rig = Start(World());
            Heightfield ground = Ground();
            rig.A = rig.Join("William");
            rig.Pump(5);
            rig.A.SendMove(MoverInput.None, 30f, 0f, GroundedAt(ground, 2.0, 3.0));
            rig.Pump(3);
            rig.B = rig.Join("Guest");
            // A paused server releases no step and broadcasts nothing, but still answers a Welcome with its
            // snapshot at once, so the only source of A's body here is the snapshot.
            rig.Server.Paused = true;
            rig.B.Update(rig.Ms);
            rig.Server.Update(0.05);
            rig.B.Update(rig.Ms);
            rig.Server.Update(0.05);
            rig.B.Update(rig.Ms);
            Assert.That(rig.B.State, Is.EqualTo(ClientState.Connected));
            Assert.That(rig.B.SnapshotApplied, Is.True);
            Assert.That(rig.B.Others.ContainsKey(rig.A.Welcome.SessionId), Is.True, "A's body came in the snapshot");
            Assert.That(rig.B.Others[rig.A.Welcome.SessionId].YawDeg, Is.EqualTo(30f));
        }

        [Test]
        public void TheSnapshotWaitsForTheStepWhoseDigestNamesIt()
        {
            // A's move arrives in the same update as B's Hello, before the step: the snapshot B receives must carry
            // the body as of the step that follows, stamped with that tick, or the digests of the two ends disagree.
            Rig rig = Start(World());
            Heightfield ground = Ground();
            rig.A = rig.Join("William");
            rig.Pump(5);
            rig.A.SendMove(MoverInput.None, 0f, 0f, GroundedAt(ground, 2.0, 3.0));
            rig.Pump(3);
            rig.B = rig.Join("Guest");
            rig.B.Update(rig.Ms);
            rig.A.SendMove(MoverInput.Walk(1.0, 0.0), 90f, 0f, GroundedAt(ground, 2.2, 3.0));
            rig.Server.Update(0.0); // the Hello and the move handled; no step yet
            rig.B.Update(rig.Ms);
            Assert.That(rig.B.State, Is.EqualTo(ClientState.Connected));
            Assert.That(rig.B.SnapshotApplied, Is.False, "no step has named the bodies yet");
            rig.Server.Update(0.05);
            rig.B.Update(rig.Ms);
            Assert.That(rig.B.SnapshotApplied, Is.True);
            PlayerSession william = rig.Server.Sessions[0];
            Assert.That(rig.B.Others[william.SessionId].ServerTick, Is.EqualTo(rig.Server.World.Tick));
            Assert.That(rig.B.MirrorDigest(william.SessionId), Is.EqualTo(rig.Server.BodyDigest(william)));
        }

        [Test]
        public void TheMirrorDigestMatchesTheServersRecordAndTheTickEstimateRuns()
        {
            Rig rig = Start(World());
            Heightfield ground = Ground();
            rig.A = rig.Join("William");
            rig.B = rig.Join("Guest");
            rig.Pump(5);
            rig.A.SendMove(MoverInput.None, 0f, 0f, GroundedAt(ground, 2.0, 3.0));
            rig.B.SendMove(MoverInput.None, 0f, 0f, GroundedAt(ground, -4.0, 1.0));
            rig.Pump(3);
            PlayerSession guest = rig.Server.Sessions[1];
            Assert.That(rig.A.MirrorDigest(guest.SessionId), Is.EqualTo(rig.Server.BodyDigest(guest)));
            Assert.That(rig.A.MirrorDigest(guest.SessionId), Is.Not.EqualTo(rig.Server.BodyDigest(rig.Server.Sessions[0])));
            Assert.That(rig.Server.Digest(), Is.EqualTo(rig.Server.Digest()), "stable between steps");

            long tick = rig.A.LastServerTick;
            Assert.That(tick, Is.GreaterThan(0));
            Assert.That(rig.A.EstimatedServerTick(rig.Ms + 1000), Is.EqualTo(tick + 20.0).Within(1.0), "a second on is twenty ticks on at 20 Hz");
            MirrorSample sample;
            Assert.That(rig.A.TrySampleMirror(guest.SessionId, rig.Ms, out sample), Is.True);
            Assert.That(sample.East, Is.EqualTo(-4.0).Within(1e-9));
        }
    }
}
