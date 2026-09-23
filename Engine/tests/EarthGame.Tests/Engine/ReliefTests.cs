using System;
using System.Diagnostics;
using EarthGame.ClientCore;
using EarthGame.Engine;
using NUnit.Framework;

namespace EarthGame.Tests.Engine
{
    /// <summary>
    /// The relief below the data (BF.4 promise 2): as large as its table and no larger, none where water stands and tapering
    /// toward it, no seam where cells or tiles meet, the same from the same seed, gentle enough to add only a few degrees to
    /// any slope, carried by the Terrain's posts to a few centimetres, and cheap.
    /// </summary>
    public sealed class ReliefTests
    {
        private static GroundQuad Uniform(byte cover, double westEast = 0.0, double southNorth = 0.0, double cellM = 4.0)
        {
            GroundQuad q = default;
            q.WestEast = westEast;
            q.SouthNorth = southNorth;
            q.CellM = cellM;
            q.SW.Cover = q.SE.Cover = q.NW.Cover = q.NE.Cover = cover;
            q.SW.HeightM = q.SE.HeightM = q.NW.HeightM = q.NE.HeightM = 20f;
            return q;
        }

        [Test]
        public void TheReliefIsAsLargeAsItsTableAndNoLarger()
        {
            Random rng = new Random(41);
            foreach (GroundCover cover in GroundCovers.All)
            {
                byte code = GroundCovers.Pack(cover, 1);
                bool has = Relief.Of(code, out double amplitude, out int lattice);
                double most = 0.0;
                for (int i = 0; i < 20000; i++)
                {
                    double east = -3000.0 + rng.NextDouble() * 6000.0, north = -3000.0 + rng.NextDouble() * 6000.0;
                    double cellWest = Math.Floor(east / 4.0) * 4.0, cellSouth = Math.Floor(north / 4.0) * 4.0;
                    GroundQuad q = Uniform(code, cellWest, cellSouth);
                    double relief = FineGround.At(q, east, north, 1347UL) - FineGround.RasterAt(q, (east - cellWest) / 4.0, (north - cellSouth) / 4.0);
                    most = Math.Max(most, Math.Abs(relief));
                }
                TestContext.WriteLine(GroundCovers.NameOf(cover) + ": the table's " + amplitude + " m on the " + (has ? Relief.SpacingOf(lattice) + " m lattice" : "none") + ", the largest met " + most.ToString("0.0000") + " m");
                if (!has)
                {
                    Assert.That(most, Is.EqualTo(0.0), GroundCovers.NameOf(cover) + " has no relief");
                    continue;
                }
                Assert.That(amplitude, Is.LessThanOrEqualTo(Relief.MostM));
                Assert.That(most, Is.LessThanOrEqualTo(amplitude + 1e-12), GroundCovers.NameOf(cover) + " no larger than its table");
                Assert.That(most, Is.GreaterThan(0.5 * amplitude), GroundCovers.NameOf(cover) + " and really there");
            }
        }

        [Test]
        public void NoReliefStandsInWaterAndItFallsAwayTowardIt()
        {
            byte heath = GroundCovers.Pack(GroundCover.Heath, 2), sea = GroundCovers.Pack(GroundCover.Sea, 3), fresh = GroundCovers.Pack(GroundCover.FreshWater, 3);
            GroundQuad drowned = Uniform(sea);
            drowned.NE.Cover = fresh;
            Random rng = new Random(2);
            for (int i = 0; i < 2000; i++)
            {
                double east = rng.NextDouble() * 4.0, north = rng.NextDouble() * 4.0;
                Assert.That(Relief.At(drowned, east / 4.0, north / 4.0, east, north, 7UL), Is.EqualTo(0.0), "none under water");
            }
            // One post dry: nothing at the three wet posts, and no more than the taper of the dry share allows between.
            GroundQuad shore = Uniform(sea);
            shore.SW.Cover = heath;
            Relief.Of(heath, out double amplitude, out _);
            Assert.That(Relief.At(shore, 1.0, 0.0, 4.0, 0.0, 7UL), Is.EqualTo(0.0));
            Assert.That(Relief.At(shore, 0.0, 1.0, 0.0, 4.0, 7UL), Is.EqualTo(0.0));
            Assert.That(Relief.At(shore, 1.0, 1.0, 4.0, 4.0, 7UL), Is.EqualTo(0.0));
            double atDry = 0.0;
            for (int i = 0; i < 4000; i++)
            {
                double tx = rng.NextDouble(), tz = rng.NextDouble();
                double dry = (1.0 - tx) * (1.0 - tz);
                double relief = Relief.At(shore, tx, tz, tx * 4.0, tz * 4.0, 7UL);
                Assert.That(Math.Abs(relief), Is.LessThanOrEqualTo(amplitude * dry * Relief.Taper(dry) + 1e-12));
                if (dry < 0.1) Assert.That(Math.Abs(relief), Is.LessThan(0.001 * amplitude), "next to the water, almost nothing");
                if (dry > 0.9) atDry = Math.Max(atDry, Math.Abs(relief));
            }
            Assert.That(atDry, Is.GreaterThan(0.0), "the dry corner keeps some");
        }

        [Test]
        public void NoSeamWhereCellsOrTilesMeet()
        {
            WorldState world = FineWorld.Make();
            world.Changes.Dig(100, 100, 60);
            world.Changes.Dig(250, 250, 45);
            // Along every kind of edge the server's quads on either side give one height: read a hair either side of it.
            Random rng = new Random(9);
            double worst = 0.0;
            for (int i = 0; i < 20000; i++)
            {
                int row = 1 + rng.Next(FineWorld.Side - 2), col = 1 + rng.Next(FineWorld.Side - 2);
                double t = rng.NextDouble() * FineWorld.CellM;
                double east = FineWorld.EastOf(col), north = FineWorld.NorthOf(row);
                const double hair = 1e-7;
                // A north-south edge through the post, and an east-west one.
                worst = Math.Max(worst, Math.Abs(world.GroundAt(east - hair, north - t) - world.GroundAt(east + hair, north - t)));
                worst = Math.Max(worst, Math.Abs(world.GroundAt(east + t, north - hair) - world.GroundAt(east + t, north + hair)));
            }
            Assert.That(worst, Is.LessThan(1e-4), "the largest step across a cell's edge");
            // A tile's own posts give the same ground on the edge it shares with the next, as each tile is prepared alone.
            TileGrid grid = FineWorld.Grid();
            ReceivedTile westGround = FineWorld.Tile(world, TileLayer.Ground, new TileId(0, 1)), eastGround = FineWorld.Tile(world, TileLayer.Ground, new TileId(1, 1));
            ReceivedTile westCover = FineWorld.Tile(world, TileLayer.GroundCover, new TileId(0, 1)), eastCover = FineWorld.Tile(world, TileLayer.GroundCover, new TileId(1, 1));
            Func<int, int, byte> westDug = ClientGround.DugIn(world.Changes, westGround, grid), eastDug = ClientGround.DugIn(world.Changes, eastGround, grid);
            for (int k = 0; k <= 1000; k++)
            {
                double north = k * 0.999;
                double fromWest = ClientGround.HeightAt(westGround, westCover, world.Seed, FineWorld.ExtentM, westDug, 0.0, north);
                double fromEast = ClientGround.HeightAt(eastGround, eastCover, world.Seed, FineWorld.ExtentM, eastDug, 0.0, north);
                Assert.That(fromWest, Is.EqualTo(fromEast).Within(1e-9), "on the shared edge at " + north + " m north");
            }
        }

        [Test]
        public void TheSameSeedGrowsTheSameGround()
        {
            WorldState one = FineWorld.Make(1347UL), again = FineWorld.Make(1347UL), other = FineWorld.Make(1348UL);
            Random rng = new Random(4);
            int differ = 0, grown = 0;
            for (int i = 0; i < 5000; i++)
            {
                double east = -990.0 + rng.NextDouble() * 1980.0, north = -790.0 + rng.NextDouble() * 1780.0;
                Assert.That(again.GroundAt(east, north), Is.EqualTo(one.GroundAt(east, north)), "the same seed, the same ground");
                double relief = one.GroundAt(east, north) - one.RasterGroundAt(east, north);
                if (Math.Abs(relief) < 1e-3) continue;
                grown++;
                if (Math.Abs(other.GroundAt(east, north) - one.GroundAt(east, north)) > 1e-4) differ++;
            }
            Assert.That(differ, Is.GreaterThan(grown * 9 / 10), "another seed, other humps");
        }

        [Test]
        public void TheReliefAddsOnlyAFewDegreesToAnySlope()
        {
            // The steepest the relief alone tilts the ground, measured over the made world at a stride's length.
            WorldState world = FineWorld.Make();
            Random rng = new Random(12);
            double steepest = 0.0;
            const double stride = 0.5;
            for (int i = 0; i < 40000; i++)
            {
                double east = -990.0 + rng.NextDouble() * 1980.0, north = -790.0 + rng.NextDouble() * 1780.0;
                double a = rng.NextDouble() * 2.0 * Math.PI;
                double de = Math.Cos(a) * stride, dn = Math.Sin(a) * stride;
                double r0 = world.GroundAt(east, north) - world.RasterGroundAt(east, north);
                double r1 = world.GroundAt(east + de, north + dn) - world.RasterGroundAt(east + de, north + dn);
                steepest = Math.Max(steepest, Math.Abs(r1 - r0) / stride);
            }
            double degrees = Math.Atan(steepest) * 180.0 / Math.PI;
            TestContext.WriteLine("the relief's steepest tilt over half a metre: " + degrees.ToString("0.00") + "°");
            Assert.That(degrees, Is.LessThan(Relief.SteepestDeg), "a few degrees at most");
        }

        [Test]
        public void TheTerrainsPostsCarryTheRelief()
        {
            // The client's Terrain is the ground at posts 1.953 m apart, flat between them: over forest floor, the most relief
            // at the finest lattice but one, and heath, the finest, how far that lies from the function.
            foreach (GroundCover cover in new[] { GroundCover.ForestFloor, GroundCover.Heath, GroundCover.Rock })
            {
                byte code = GroundCovers.Pack(cover, 1);
                const double spacing = 1000.0 / 512.0;
                Random rng = new Random(33);
                double worst = 0.0;
                for (int i = 0; i < 20000; i++)
                {
                    double east = rng.NextDouble() * 2000.0, north = rng.NextDouble() * 2000.0;
                    double x0 = Math.Floor(east / spacing) * spacing, z0 = Math.Floor(north / spacing) * spacing;
                    double tx = (east - x0) / spacing, tz = (north - z0) / spacing;
                    double h00 = Flat(code, x0, z0), h10 = Flat(code, x0 + spacing, z0), h01 = Flat(code, x0, z0 + spacing), h11 = Flat(code, x0 + spacing, z0 + spacing);
                    // Either of the two triangles a Terrain may cut the square into, whichever strays further.
                    double lower = tx + tz <= 1.0 ? h00 + (h10 - h00) * tx + (h01 - h00) * tz : h11 + (h01 - h11) * (1.0 - tx) + (h10 - h11) * (1.0 - tz);
                    double other = tx >= tz ? h00 + (h10 - h00) * tx + (h11 - h10) * tz : h00 + (h11 - h01) * tx + (h01 - h00) * tz;
                    double truth = Flat(code, east, north);
                    worst = Math.Max(worst, Math.Max(Math.Abs(lower - truth), Math.Abs(other - truth)));
                }
                TestContext.WriteLine(GroundCovers.NameOf(cover) + ": the Terrain's triangles stray " + worst.ToString("0.0000") + " m from the relief");
                Assert.That(worst, Is.LessThan(Relief.TerrainStrayM), GroundCovers.NameOf(cover));
            }
        }

        private static double Flat(byte code, double east, double north)
        {
            double west = Math.Floor(east / 4.0) * 4.0, south = Math.Floor(north / 4.0) * 4.0;
            return FineGround.At(Uniform(code, west, south), east, north, 1347UL);
        }

        [Test]
        public void TheGroundIsCheap()
        {
            WorldState world = FineWorld.Make();
            ClientGround client = FineWorld.Client(world);
            TileId id = new TileId(1, 1);
            ReceivedTile ground = FineWorld.Tile(world, TileLayer.Ground, id), cover = FineWorld.Tile(world, TileLayer.GroundCover, id);
            Random rng = new Random(1);
            double[] east = new double[200000], north = new double[200000];
            for (int i = 0; i < east.Length; i++)
            {
                east[i] = rng.NextDouble() * 999.0;
                north[i] = rng.NextDouble() * 999.0;
            }
            double sum = 0.0;
            for (int i = 0; i < 2000; i++) sum += world.GroundAt(east[i], north[i]) + ClientGround.HeightAt(ground, cover, world.Seed, FineWorld.ExtentM, null, east[i], north[i]);
            Stopwatch clock = Stopwatch.StartNew();
            for (int i = 0; i < east.Length; i++) sum += world.GroundAt(east[i], north[i]);
            double serverNs = clock.Elapsed.TotalMilliseconds * 1e6 / east.Length;
            clock.Restart();
            for (int i = 0; i < east.Length; i++) sum += ClientGround.HeightAt(ground, cover, world.Seed, FineWorld.ExtentM, null, east[i], north[i]);
            double tileNs = clock.Elapsed.TotalMilliseconds * 1e6 / east.Length;
            clock.Restart();
            PreparedTile prepared = TilePreparation.Prepare(ground, 513, cover, 16, null, world.Seed, null, null);
            double tileMs = clock.Elapsed.TotalMilliseconds;
            TestContext.WriteLine("the server's ground " + serverNs.ToString("0") + " ns a call; a tile's " + tileNs.ToString("0") + " ns; a kilometre tile's 513 posts a side "
                                  + tileMs.ToString("0.0") + " ms (" + sum.ToString("0") + ")");
            Assert.That(prepared.Posts, Is.EqualTo(513));
            Assert.That(serverNs, Is.LessThan(5000.0), "microseconds, not more");
            Assert.That(tileNs, Is.LessThan(5000.0));
        }
    }
}
