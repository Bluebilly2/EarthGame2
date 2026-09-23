using System;
using System.Collections.Generic;
using EarthGame.ClientCore;
using EarthGame.Engine;
using EarthGame.Server;
using NUnit.Framework;

namespace EarthGame.Tests.Engine
{
    /// <summary>
    /// A world made for the ground (BF.4): two kilometres at 4 m in four tiles, rolling ground with a scarp, the sea below the
    /// datum in the south, a lake and a one-cell creek, and a mosaic of every cover in 40 m blocks, so the relief, its taper
    /// toward water and the tiles' shared edges are all met at random points.
    /// </summary>
    internal static class FineWorld
    {
        public const double ExtentM = 2000.0, CellM = 4.0;
        public const int Side = 501;
        public const ulong Seed = 1347UL;

        public static readonly Region Region = new Region("fixture", "Fixture", Region.Bherwerre.CentreLatitudeDeg,
            Region.Bherwerre.CentreLongitudeDeg, ExtentM, 237, 8.0);

        private static readonly GroundCover[] Mosaic =
        {
            GroundCover.ForestFloor, GroundCover.Bracken, GroundCover.Heath, GroundCover.Grass, GroundCover.Sedge, GroundCover.Rock,
            GroundCover.BareEarth, GroundCover.SwampFloor, GroundCover.DuneSand, GroundCover.Sand, GroundCover.Unknown,
        };

        public static double EastOf(int col) => col * CellM - ExtentM * 0.5;
        public static double NorthOf(int row) => ExtentM * 0.5 - row * CellM;

        private static bool InSea(double north) => north < -800.0;
        private static bool InLake(double east, double north) => (east - 300.0) * (east - 300.0) + (north - 200.0) * (north - 200.0) < 60.0 * 60.0;
        private static bool InCreek(int col, double north) => col == 250 && north > 0.0 && north < 700.0;

        public static float Height(int row, int col)
        {
            double e = EastOf(col), n = NorthOf(row);
            double h = 30.0 + 12.0 * Math.Sin(e / 140.0) * Math.Cos(n / 110.0) + 3.0 * Math.Sin(e / 23.0 + n / 31.0) + 0.37 * Math.Sin(e / 7.3 - n / 5.1);
            // A scarp forty metres high across forty metres, west of the middle.
            if (e > -600.0 && e < -560.0) h += e + 600.0;
            else if (e >= -560.0) h += 40.0;
            if (InLake(e, n)) h = Math.Min(h, 58.0);
            if (InSea(n)) h = -2.0 - (-800.0 - n) * 0.05;
            return (float)h;
        }

        public static uint CoverAt(int row, int col)
        {
            double e = EastOf(col), n = NorthOf(row);
            if (InSea(n)) return GroundCovers.Pack(GroundCover.Sea, 3);
            if (InLake(e, n) || InCreek(col, n)) return GroundCovers.Pack(GroundCover.FreshWater, 3);
            int block = (row / 10) * 7 + (col / 10) * 3;
            return GroundCovers.Pack(Mosaic[block % Mosaic.Length], (row / 10 + col / 10) % 4);
        }

        public static RegionRaster Heights() => TestRasters.FromLaw(Side, CellM, ExtentM, "fine", Height);
        public static RegionRaster Cover() => TestRasters.FromCodes(Side, CellM, ExtentM, "fine_cover", "cover", CoverAt, null);

        public static WorldState Make(ulong seed = Seed, bool withCover = true, RegionRaster stand = null, RegionRaster loose = null) =>
            new WorldState(seed, Region, Region.WakeClock(), new Heightfield(Heights()), 0, null, null, withCover ? Cover() : null, stand, loose);

        public static TileGrid Grid() => new TileGrid(ExtentM);

        /// <summary>A tile of a layer of the world, encoded and decoded as the wire carries it.</summary>
        public static ReceivedTile Tile(WorldState world, TileLayer layer, TileId id)
        {
            TileGrid grid = Grid();
            EncodedTile encoded;
            switch (layer)
            {
                case TileLayer.Ground: encoded = TileCodec.Encode(world.Terrain, grid, id); break;
                case TileLayer.GroundCover: encoded = TileCodec.EncodeCodes(world.Cover, layer, grid, id); break;
                case TileLayer.Stand: encoded = TileCodec.EncodeCodes(world.Stand, layer, grid, id); break;
                case TileLayer.Loose: encoded = TileCodec.EncodeCodes(world.Loose, layer, grid, id); break;
                default: throw new ArgumentOutOfRangeException(nameof(layer));
            }
            ReceivedTile tile = new ReceivedTile
            {
                Id = id,
                Layer = layer,
                Posts = encoded.Posts,
                CellM = encoded.CellM,
                OriginEast = encoded.OriginEast,
                OriginNorth = encoded.OriginNorth,
                Crc32 = encoded.Crc32,
            };
            if (TileLayers.CarriesCodes(layer)) tile.Codes = TileCodec.UnpackCodes(encoded.Bytes, encoded.Posts);
            else tile.Heights = TileCodec.Unpack(encoded.Bytes, encoded.Posts);
            return tile;
        }

        /// <summary>Every tile of the world's ground and cover, as a client that holds them all.</summary>
        public static Dictionary<(TileLayer, TileId), ReceivedTile> AllTiles(WorldState world)
        {
            TileGrid grid = Grid();
            Dictionary<(TileLayer, TileId), ReceivedTile> held = new Dictionary<(TileLayer, TileId), ReceivedTile>();
            for (int iz = 0; iz < grid.TilesPerSide; iz++)
                for (int ix = 0; ix < grid.TilesPerSide; ix++)
                {
                    TileId id = new TileId(ix, iz);
                    held[(TileLayer.Ground, id)] = Tile(world, TileLayer.Ground, id);
                    if (world.Cover != null) held[(TileLayer.GroundCover, id)] = Tile(world, TileLayer.GroundCover, id);
                }
            return held;
        }

        /// <summary>A client's ground over every tile of the world, its changes the world's own.</summary>
        public static ClientGround Client(WorldState world, ulong? seed = null)
        {
            Dictionary<(TileLayer, TileId), ReceivedTile> held = AllTiles(world);
            return new ClientGround(Grid(), (layer, id) => held.TryGetValue((layer, id), out ReceivedTile t) ? t : null, seed ?? world.Seed, world.Changes);
        }
    }

    /// <summary>
    /// One ground (BF.4 promises 1 and 3): the server's <see cref="WorldState.GroundAt"/> and the client's
    /// <see cref="ClientGround"/> are one function of what both hold, the heights to the centimetre the tiles carry, the relief
    /// below them and the hollows dug; the Terrain's posts, the trees and the litter stand on it; the movement check judges by
    /// it; the water and the walk's slope are measured on the raster as they were.
    /// </summary>
    public sealed class FineGroundTests
    {
        [Test]
        public void TheServerAndTheClientStandOnOneGround()
        {
            WorldState world = FineWorld.Make();
            // Hollows on both sides: one inside a tile, one on the posts two tiles share, one on the corner four share.
            world.Changes.Dig(120, 130, 40);
            world.Changes.Dig(250, 77, 90);
            world.Changes.Dig(250, 250, 30);
            ClientGround client = FineWorld.Client(world);
            Random rng = new Random(23);
            double worst = 0.0, reliefSeen = 0.0;
            for (int i = 0; i < 10000; i++)
            {
                double east = -999.9 + rng.NextDouble() * 1999.8, north = -999.9 + rng.NextDouble() * 1999.8;
                double server = world.GroundAt(east, north), there = client.HeightAt(east, north);
                worst = Math.Max(worst, Math.Abs(server - there));
                reliefSeen = Math.Max(reliefSeen, Math.Abs(server - world.RasterGroundAt(east, north)));
            }
            // On the edges two tiles share, and round each hollow, where the two sides read different quads.
            for (int k = -500; k <= 500; k++)
            {
                double along = k * 1.9531;
                worst = Math.Max(worst, Math.Abs(world.GroundAt(0.0, along) - client.HeightAt(0.0, along)));
                worst = Math.Max(worst, Math.Abs(world.GroundAt(along, 0.0) - client.HeightAt(along, 0.0)));
            }
            foreach ((int row, int col) in new[] { (120, 130), (250, 77), (250, 250) })
                for (int k = 0; k < 64; k++)
                {
                    double a = k * Math.PI / 32.0, r = 3.4 * (k % 4) / 3.0;
                    double east = FineWorld.EastOf(col) + r * Math.Cos(a), north = FineWorld.NorthOf(row) + r * Math.Sin(a);
                    worst = Math.Max(worst, Math.Abs(world.GroundAt(east, north) - client.HeightAt(east, north)));
                }
            TestContext.WriteLine("server against client: the largest difference " + worst + " m; the relief reached " + reliefSeen + " m");
            Assert.That(worst, Is.LessThan(0.001), "one ground to a millimetre");
            Assert.That(reliefSeen, Is.GreaterThan(0.5 * Relief.MostM), "and it is not the raster alone");
        }

        [Test]
        public void TheServerRoundsItsGroundAsTheTilesDo()
        {
            WorldState world = FineWorld.Make(withCover: false);
            ClientGround client = FineWorld.Client(world);
            RegionRaster raw = world.Terrain.Raster;
            double worstToRaw = 0.0;
            for (int row = 0; row < FineWorld.Side; row += 7)
                for (int col = 0; col < FineWorld.Side; col += 5)
                {
                    double east = FineWorld.EastOf(col), north = FineWorld.NorthOf(row);
                    float carried = TileCodec.PostMetres(raw[row, col]);
                    Assert.That(world.GroundAt(east, north), Is.EqualTo((double)carried), "post (" + row + ", " + col + ") as the tile carries it");
                    Assert.That(client.HeightAt(east, north), Is.EqualTo((double)carried), "and as the client holds it");
                    worstToRaw = Math.Max(worstToRaw, Math.Abs(carried - raw[row, col]));
                }
            Assert.That(worstToRaw, Is.LessThanOrEqualTo(0.005 + 1e-6), "half a centimetre from the raster at most");
            // Without a cover there is no relief: between posts it is the tiles' own bilinear ground.
            TileHeightfield tiles = new TileHeightfield(FineWorld.Grid());
            foreach (KeyValuePair<(TileLayer, TileId), ReceivedTile> held in FineWorld.AllTiles(world)) tiles.Add(held.Value);
            Random rng = new Random(5);
            for (int i = 0; i < 2000; i++)
            {
                double east = -999.0 + rng.NextDouble() * 1998.0, north = -999.0 + rng.NextDouble() * 1998.0;
                Assert.That(world.GroundAt(east, north), Is.EqualTo(tiles.HeightAt(east, north)).Within(1e-9));
            }
        }

        [Test]
        public void TheHollowIsOneGround()
        {
            WorldState world = FineWorld.Make();
            const int row = 140, col = 188;
            double east = FineWorld.EastOf(col), north = FineWorld.NorthOf(row);
            double before = world.GroundAt(east, north);
            double beside = world.GroundAt(east + 0.8 * FineWorld.CellM, north);
            double half = world.GroundAt(east + 0.375 * FineWorld.CellM, north);
            world.Changes.Dig(row, col, 40);
            ClientGround client = FineWorld.Client(world);
            Assert.That(world.GroundAt(east, north), Is.EqualTo(before - 0.40).Within(1e-9), "the post of the dug cell forty centimetres down");
            Assert.That(world.GroundAt(east + 0.375 * FineWorld.CellM, north), Is.EqualTo(half - 0.20).Within(1e-9), "half way out, half as deep");
            Assert.That(world.GroundAt(east + 0.8 * FineWorld.CellM, north), Is.EqualTo(beside).Within(1e-12), "past three-quarters of a cell, untouched");
            Assert.That(client.HeightAt(east, north), Is.EqualTo(world.GroundAt(east, north)).Within(1e-9), "and the client's is dug where the server's is");
            // A digging goes on: the client's ground reads the changes as they are told.
            world.Changes.Dig(row, col, 70);
            Assert.That(client.HeightAt(east, north), Is.EqualTo(before - 0.70).Within(1e-9));
        }

        [Test]
        public void TheTerrainsPostsAreSampledFromTheGround()
        {
            WorldState world = FineWorld.Make();
            world.Changes.Dig(200, 300, 50);
            TileGrid grid = FineWorld.Grid();
            TileId id = new TileId(1, 1);
            ReceivedTile ground = FineWorld.Tile(world, TileLayer.Ground, id);
            ReceivedTile cover = FineWorld.Tile(world, TileLayer.GroundCover, id);
            Func<int, int, byte> dug = ClientGround.DugIn(world.Changes, ground, grid);
            Assert.That(dug, Is.Not.Null, "the tile holds a hollow");
            PreparedTile prepared = TilePreparation.Prepare(ground, 257, cover, 16, null, world.Seed, dug, grid);
            double spacing = prepared.SizeM / 256.0, worst = 0.0;
            for (int z = 0; z < 257; z += 3)
                for (int x = 0; x < 257; x += 3)
                {
                    double east = prepared.OriginEast + x * spacing, north = prepared.OriginNorth + z * spacing;
                    double drawn = prepared.BaseM + prepared.Normalised[z, x] * prepared.RangeM;
                    worst = Math.Max(worst, Math.Abs(drawn - world.GroundAt(east, north)));
                }
            Assert.That(worst, Is.LessThan(0.001), "the Terrain is the ground at its posts");
            Assert.That(prepared.ReliefCrc, Is.EqualTo(cover.Crc32), "and it says which cover its relief came from");
            Assert.That(prepared.BaseM, Is.LessThanOrEqualTo(MinOf(prepared) - TilePreparation.DigRoomM + 1e-4), "with room below for a hole");
        }

        private static double MinOf(PreparedTile prepared)
        {
            double min = double.MaxValue;
            foreach (float f in prepared.Normalised) min = Math.Min(min, prepared.BaseM + f * prepared.RangeM);
            return min;
        }

        [Test]
        public void TheTreesAndTheLitterStandOnTheGround()
        {
            RegionRaster stand = TestRasters.FromCodes(FineWorld.Side, FineWorld.CellM, FineWorld.ExtentM, "fine_stand", "stand",
                (row, col) => row % 9 == 0 && col % 7 == 0 ? StandCodes.Pack(PlantSpecies.OldManBanksia, 11.0) : 0u, null);
            RegionRaster loose = TestRasters.FromCodes(FineWorld.Side, FineWorld.CellM, FineWorld.ExtentM, "fine_loose", "loose",
                (row, col) => row % 5 == 0 && col % 3 == 0 ? LooseCodes.Pack(2, 1) : 0u, null);
            WorldState world = FineWorld.Make(stand: stand, loose: loose);
            world.Changes.Dig(300, 60, 80);
            TileGrid grid = FineWorld.Grid();
            // The tile north of the middle row, whose things of its south edge lie on the tile south of it.
            TileId id = new TileId(1, 1);
            ReceivedTile ground = FineWorld.Tile(world, TileLayer.Ground, id);
            GroundSnapshot fine = FineWorld.Client(world).SnapshotFor(id);
            PreparedStand placed = StandPreparation.Prepare(FineWorld.Tile(world, TileLayer.Stand, id), FineWorld.Tile(world, TileLayer.Loose, id), ground, grid,
                null, null, null, fine);
            Assert.That(placed.ReliefCrc, Is.EqualTo(fine.Crc), "it says which ground it was stood on");
            Assert.That(placed.Trees.Length, Is.GreaterThan(100));
            Assert.That(placed.Sticks.Length, Is.GreaterThan(100));
            // Each foot is on the ground the server holds: inside the tile, and past its west or south edge, where its cells'
            // layout puts some of them, on the ground of the tile beyond.
            double worst = 0.0, beyond = 0.0, reliefSeen = 0.0;
            int inside = 0, outside = 0;
            void Judge(float east, float up, float north)
            {
                // Past the region's edge the server holds no ground at all.
                if (Math.Abs(east) > FineWorld.ExtentM * 0.5 || Math.Abs(north) > FineWorld.ExtentM * 0.5) return;
                double d = Math.Abs(up - world.GroundAt(east, north));
                if (east >= ground.OriginEast && north >= ground.OriginNorth)
                {
                    worst = Math.Max(worst, d);
                    inside++;
                }
                else
                {
                    beyond = Math.Max(beyond, d);
                    outside++;
                }
            }
            foreach (StandTree tree in placed.Trees)
            {
                Judge(tree.East, tree.Up, tree.North);
                reliefSeen = Math.Max(reliefSeen, Math.Abs(tree.Up - world.RasterGroundAt(tree.East, tree.North)));
            }
            foreach (LooseInstance stick in placed.Sticks) Judge(stick.East, stick.Up, stick.North);
            foreach (LooseInstance cobble in placed.Cobbles) Judge(cobble.East, cobble.Up, cobble.North);
            TestContext.WriteLine(inside + " things inside the tile, the worst " + worst + " m from the server's ground; " + outside + " past its edge, the worst " + beyond + " m");
            Assert.That(worst, Is.LessThan(0.001), "each foot on the ground the server holds");
            Assert.That(outside, Is.GreaterThan(0), "the fixture puts things past the edge");
            Assert.That(beyond, Is.LessThan(0.001), "and past the edge too");
            Assert.That(reliefSeen, Is.GreaterThan(0.03), "which is not the raster alone");
            // A snapshot without the tiles beyond carries its own tile's ground on, near but not exact.
            Dictionary<(TileLayer, TileId), ReceivedTile> alone = FineWorld.AllTiles(world);
            alone.Remove((TileLayer.Ground, new TileId(1, 0)));
            GroundSnapshot lonely = new ClientGround(grid, (layer, tid) => alone.TryGetValue((layer, tid), out ReceivedTile t) ? t : null, world.Seed, world.Changes).SnapshotFor(id);
            Assert.That(lonely.Crc, Is.Not.EqualTo(fine.Crc), "a snapshot knows what it lacked");
            PreparedStand near = StandPreparation.Prepare(FineWorld.Tile(world, TileLayer.Stand, id), FineWorld.Tile(world, TileLayer.Loose, id), ground, grid, null, null, null, lonely);
            double off = 0.0;
            foreach (LooseInstance stick in near.Sticks)
                if (stick.North < ground.OriginNorth) off = Math.Max(off, Math.Abs(stick.Up - world.GroundAt(stick.East, stick.North)));
            Assert.That(off, Is.LessThan(0.25), "carried on from its own posts, within a quarter metre");
        }

        [Test]
        public void TheMovementCheckJudgesTheOneGround()
        {
            WorldState world = FineWorld.Make();
            // A point on the forest floor where the relief lies well below the raster.
            double east = double.NaN, north = double.NaN;
            Random rng = new Random(3);
            for (int i = 0; i < 200000 && double.IsNaN(east); i++)
            {
                double e = -900.0 + rng.NextDouble() * 1800.0, n = -700.0 + rng.NextDouble() * 1600.0;
                if (world.GroundAt(e, n) - world.RasterGroundAt(e, n) < -0.08) { east = e; north = n; }
            }
            Assert.That(double.IsNaN(east), Is.False, "a hollow of the relief eight centimetres deep");
            MovementRules rules = new MovementRules();
            MoverState report = MoverState.AtRest(east, world.RasterGroundAt(east, north) + rules.GroundTolerance - 0.05, north);
            report.Grounded = true;
            double half = FineWorld.ExtentM * 0.5;
            Assert.That(MovementValidator.Check(default, false, report, 0.05, world.Terrain, half, MoverConfig.Default, rules), Is.Null,
                "within the tolerance of the raster");
            Assert.That(MovementValidator.Check(default, false, report, 0.05, new FineGroundSource(world), half, MoverConfig.Default, rules), Does.StartWith("grounded"),
                "and beyond it of the ground the server holds");
        }

        [Test]
        public void WaterIsMeasuredOverTheRaster()
        {
            // A pond a metre deep over flat heath, its cells fresh water by cover and lake by class: the surface a metre over the
            // raster wherever the class is lake.
            const int side = 101;
            const double cell = 4.0, extent = 400.0;
            Func<int, int, bool> pond = (row, col) => row >= 40 && row <= 60 && col >= 40 && col <= 60;
            RegionRaster heights = TestRasters.FromLaw(side, cell, extent, "pond", (row, col) => 10.0f);
            RegionRaster surface = TestRasters.FromLaw(side, cell, extent, "pond_surface", (row, col) => pond(row, col) ? 11.0f : 10.0f);
            RegionRaster classes = TestRasters.FromCodes(side, cell, extent, "pond_class", "water_class", (row, col) => pond(row, col) ? (uint)WaterClass.Lake : 0u, null);
            RegionRaster cover = TestRasters.FromCodes(side, cell, extent, "pond_cover", "cover",
                (row, col) => pond(row, col) ? GroundCovers.Pack(GroundCover.FreshWater, 3) : GroundCovers.Pack(GroundCover.Heath, 2), null);
            Region region = new Region("fixture", "Fixture", Region.Bherwerre.CentreLatitudeDeg, Region.Bherwerre.CentreLongitudeDeg, extent, 237, 8.0);
            WorldState world = new WorldState(9UL, region, region.WakeClock(), new Heightfield(heights), 0, null, new WorldWater(surface, classes), cover);
            Heightfield water = new Heightfield(surface);
            Random rng = new Random(8);
            int wet = 0, reliefAtEdge = 0;
            for (int i = 0; i < 4000; i++)
            {
                // Round the pond's edge, where the quads are part water and part heath.
                double east = -44.0 + rng.NextDouble() * 88.0, north = -44.0 + rng.NextDouble() * 88.0;
                WaterClass at = world.WaterAt(east, north, out double depth);
                double expected = water.HeightAt(east, north) - world.Terrain.HeightAt(east, north);
                if (expected > WorldState.StandingWaterM)
                {
                    wet++;
                    Assert.That(at, Is.EqualTo(WaterClass.Lake));
                    Assert.That(depth, Is.EqualTo(expected).Within(1e-12), "the depth over the raster, whatever the relief");
                    if (Math.Abs(world.GroundAt(east, north) - world.RasterGroundAt(east, north)) > 1e-6) reliefAtEdge++;
                }
            }
            Assert.That(wet, Is.GreaterThan(500));
            Assert.That(reliefAtEdge, Is.GreaterThan(0), "the relief does reach into the pond's edge quads, and the depth ignores it");
            // Deep in the pond no relief stands at all.
            Assert.That(world.GroundAt(0.3, -0.7), Is.EqualTo(world.RasterGroundAt(0.3, -0.7)));
        }

        [Test]
        public void TheWalkIsJudgedByTheRasterAsTheTerrainSamplesIt()
        {
            WorldState world = FineWorld.Make();
            ClientGround client = FineWorld.Client(world);
            WorldState bare = FineWorld.Make(withCover: false);
            ClientGround plain = FineWorld.Client(bare);
            const double spacing = 1000.0 / 512.0;
            Random rng = new Random(17);
            for (int i = 0; i < 2000; i++)
            {
                double east = -990.0 + rng.NextDouble() * 1980.0, north = -990.0 + rng.NextDouble() * 1980.0;
                Assert.That(client.TryWalkNormalAt(east, north, spacing, out Double3 walk), Is.True);
                Assert.That(plain.TryWalkNormalAt(east, north, spacing, out Double3 without), Is.True);
                Assert.That(walk.X, Is.EqualTo(without.X).Within(1e-12), "the relief takes no part in the walk's slope");
                Assert.That(walk.Z, Is.EqualTo(without.Z).Within(1e-12));
                // The square of the Terrain's posts round the point, each post the raster there, and its slope at the point.
                double tileWest = Math.Floor((east + 1000.0) / 1000.0) * 1000.0 - 1000.0, tileSouth = Math.Floor((north + 1000.0) / 1000.0) * 1000.0 - 1000.0;
                double x0 = Math.Floor((east - tileWest) / spacing), z0 = Math.Floor((north - tileSouth) / spacing);
                double e0 = tileWest + x0 * spacing, n0 = tileSouth + z0 * spacing, tx = (east - e0) / spacing, tz = (north - n0) / spacing;
                double h00 = bare.GroundAt(e0, n0), h10 = bare.GroundAt(e0 + spacing, n0), h01 = bare.GroundAt(e0, n0 + spacing), h11 = bare.GroundAt(e0 + spacing, n0 + spacing);
                double dEast = ((h10 - h00) * (1 - tz) + (h11 - h01) * tz) / spacing, dNorth = ((h01 - h00) * (1 - tx) + (h11 - h10) * tx) / spacing;
                Double3 expected = new Double3(-dEast, 1.0, -dNorth).Normalized;
                Assert.That(walk.X, Is.EqualTo(expected.X).Within(1e-9), "at " + east + ", " + north);
                Assert.That(walk.Z, Is.EqualTo(expected.Z).Within(1e-9));
            }
            Assert.That(client.TryWalkNormalAt(5000.0, 5000.0, spacing, out Double3 beyond), Is.True, "clamped into the last tile");
            Assert.That(beyond.Y, Is.GreaterThan(0.0), "and never undefined");
            ClientGround empty = new ClientGround(FineWorld.Grid(), (layer, id) => null, world.Seed, (WorldChanges)null);
            Assert.That(empty.TryWalkNormalAt(0.0, 0.0, spacing, out _), Is.False, "no tile held, no judgement: the caller keeps the Terrain's own");
        }
    }
}
