using System;
using EarthGame.ClientCore;
using EarthGame.Engine;
using NUnit.Framework;

namespace EarthGame.Tests.ClientCore
{
    /// <summary>The client's ground reads the tiles as the server reads the raster, and the route follower walks a route.</summary>
    public sealed class ClientGroundTests
    {
        private static Heightfield Tiny() => new Heightfield(RegionRaster.Load(TestPaths.Fixture("raster", "tiny.json")));

        private static ReceivedTile TileOf(Heightfield source, TileGrid grid, TileId id)
        {
            EncodedTile encoded = TileCodec.Encode(source, grid, id);
            return new ReceivedTile
            {
                Id = id,
                Posts = encoded.Posts,
                CellM = encoded.CellM,
                OriginEast = encoded.OriginEast,
                OriginNorth = encoded.OriginNorth,
                Crc32 = encoded.Crc32,
                Heights = TileCodec.Unpack(encoded.Bytes, encoded.Posts),
            };
        }

        [Test]
        public void TheTileGroundAgreesWithTheRasterToTheCentimetre()
        {
            Heightfield raster = Tiny();
            TileGrid grid = new TileGrid(40.0, 20.0);
            TileHeightfield ground = new TileHeightfield(grid);
            Assert.That(ground.HasGroundAt(5.0, 5.0), Is.False);
            Assert.That(double.IsNaN(ground.HeightAt(5.0, 5.0)), Is.True, "unknown ground is NaN, never zero");
            for (int ix = 0; ix < 2; ix++)
                for (int iz = 0; iz < 2; iz++)
                    ground.Add(TileOf(raster, grid, new TileId(ix, iz)));
            Assert.That(ground.Count, Is.EqualTo(4));
            Random rng = new Random(11);
            for (int i = 0; i < 200; i++)
            {
                double east = -20.0 + rng.NextDouble() * 40.0;
                double north = -20.0 + rng.NextDouble() * 40.0;
                Assert.That(ground.HeightAt(east, north), Is.EqualTo(raster.HeightAt(east, north)).Within(0.006), "at " + east + ", " + north);
            }
            Assert.That(ground.HeightAt(0.0, 0.0), Is.EqualTo(127.0).Within(0.006), "the bump at the centre post");
            Assert.That(ground.HeightAt(20.0, 20.0), Is.EqualTo(raster.HeightAt(20.0, 20.0)).Within(0.006), "the far corner belongs to the last tile");
        }

        [Test]
        public void TheFollowerFacesEachWaypointAndLoops()
        {
            Waypoint[] route =
            {
                new Waypoint(0.0, 100.0, "north leg", false),
                new Waypoint(100.0, 100.0, "east leg", true),
            };
            RouteFollower follower = new RouteFollower(route, loop: true, reachM: 1.0);
            Assert.That(follower.Advance(0.0, 0.0, 0.05, out double yaw, out bool sprint), Is.True);
            Assert.That(yaw, Is.EqualTo(0.0).Within(1e-9), "north");
            Assert.That(sprint, Is.False);
            Assert.That(follower.Segment, Is.EqualTo("north leg"));
            follower.Advance(0.0, 100.0, 0.05, out yaw, out sprint);
            Assert.That(follower.Index, Is.EqualTo(1), "reached: on to the next");
            Assert.That(yaw, Is.EqualTo(90.0).Within(1e-9), "east");
            Assert.That(sprint, Is.True);
            follower.Advance(99.9, 100.0, 0.05, out yaw, out sprint);
            Assert.That(follower.Laps, Is.EqualTo(1));
            Assert.That(follower.Index, Is.EqualTo(0));
            Assert.That(yaw, Is.EqualTo(270.0).Within(1e-9), "back west to the first point");
        }

        [Test]
        public void AWaypointThatCannotBeReachedIsSkippedAfterTheStatedWhile()
        {
            Waypoint[] route = { new Waypoint(0.0, 100.0, "wall", false), new Waypoint(50.0, 0.0, "after", false) };
            RouteFollower follower = new RouteFollower(route, loop: false, reachM: 1.0, stuckSeconds: 10.0);
            // The first step records the distance as progress; 199 more without any are 9.95 s of standing still.
            for (int i = 0; i < 200; i++) follower.Advance(0.0, 50.0, 0.05, out _, out _);
            Assert.That(follower.Index, Is.EqualTo(0));
            follower.Advance(0.0, 50.0, 0.05, out double yaw, out _);
            Assert.That(follower.Skipped, Is.EqualTo(1));
            Assert.That(follower.Index, Is.EqualTo(1));
            Assert.That(yaw, Is.EqualTo(135.0).Within(1e-9), "south-east to the point after the wall");
            follower.Advance(50.0, 0.5, 0.05, out _, out _);
            Assert.That(follower.Finished, Is.True);
            Assert.That(follower.Advance(50.0, 0.5, 0.05, out _, out _), Is.False);
        }
    }
}
