using System;
using System.Collections.Generic;
using EarthGame.ClientCore;
using EarthGame.Engine;
using NUnit.Framework;

namespace EarthGame.Tests.Client
{
    /// <summary>
    /// What a view draws of the water a client was streamed (M1.4c promises 1 to 3): rectangles at the surface
    /// the client puts back together, stopping at the last cell wholly under water, merged along a row while the
    /// surface holds at one height.
    /// </summary>
    public sealed class WaterSurfaceTests
    {
        private const int Posts = 5;
        private const double Cell = 10.0;

        private static ReceivedTile Tile(TileLayer layer, Func<int, int, float> law) => new ReceivedTile
        {
            Id = new TileId(0, 0),
            Layer = layer,
            Posts = Posts,
            CellM = Cell,
            OriginEast = -20.0,
            OriginNorth = -20.0,
            Heights = Fill(law),
        };

        private static float[,] Fill(Func<int, int, float> law)
        {
            float[,] values = new float[Posts, Posts];
            for (int z = 0; z < Posts; z++)
                for (int x = 0; x < Posts; x++) values[z, x] = law(z, x);
            return values;
        }

        /// <summary>A bowl at 10 m with two metres of water over all of it: flat water, flat answer.</summary>
        [Test]
        public void AFullyFloodedTileIsOneQuadARow()
        {
            List<WaterQuad> quads = WaterSurface.Build(Tile(TileLayer.Ground, (z, x) => 10f), Tile(TileLayer.WaterDepth, (z, x) => 2f));
            Assert.That(quads.Count, Is.EqualTo(Posts - 1), "four rows of cells, one quad each");
            foreach (WaterQuad quad in quads)
            {
                Assert.That(quad.SurfaceUp, Is.EqualTo(12f).Within(1e-4));
                Assert.That(quad.EastFrom, Is.EqualTo(-20.0));
                Assert.That(quad.EastTo, Is.EqualTo(20.0), "the whole width in one quad");
                Assert.That(quad.DepthM, Is.EqualTo(Cell), "one row of cells deep");
            }
            Assert.That(quads[0].NorthFrom, Is.EqualTo(-20.0));
            Assert.That(quads[Posts - 2].NorthTo, Is.EqualTo(20.0));
        }

        [Test]
        public void DryGroundDrawsNothingAndSoDoesAMillimetreOfNoise()
        {
            Assert.That(WaterSurface.Build(Tile(TileLayer.Ground, (z, x) => 10f), Tile(TileLayer.WaterDepth, (z, x) => 0f)), Is.Empty);
            Assert.That(WaterSurface.Build(Tile(TileLayer.Ground, (z, x) => 10f), Tile(TileLayer.WaterDepth, (z, x) => 0.004f)), Is.Empty,
                "under a centimetre is what the wire's rounding can invent");
        }

        /// <summary>Water over the two western columns of posts: a cell needs all four, so only the first column of cells is drawn.</summary>
        [Test]
        public void TheEdgeStopsAtTheLastCellWhollyUnderWater()
        {
            List<WaterQuad> quads = WaterSurface.Build(
                Tile(TileLayer.Ground, (z, x) => 10f),
                Tile(TileLayer.WaterDepth, (z, x) => x <= 1 ? 2f : 0f));
            Assert.That(quads.Count, Is.EqualTo(Posts - 1));
            foreach (WaterQuad quad in quads)
            {
                Assert.That(quad.EastFrom, Is.EqualTo(-20.0));
                Assert.That(quad.EastTo, Is.EqualTo(-10.0), "one cell wide: the cell between posts 0 and 1");
            }
        }

        /// <summary>A bank rising under the water: the cell takes the lowest of its four posts, never the highest.</summary>
        [Test]
        public void AnEdgeCellSitsAtTheLowestOfItsFourPosts()
        {
            // Ground rises east; the depth falls to match, so the surface is 12 m except at post 1 where it is 11.5.
            List<WaterQuad> quads = WaterSurface.Build(
                Tile(TileLayer.Ground, (z, x) => 10f + x),
                Tile(TileLayer.WaterDepth, (z, x) => x == 1 ? 0.5f : 2f));
            Assert.That(quads, Is.Not.Empty);
            WaterQuad first = quads[0];
            Assert.That(first.SurfaceUp, Is.EqualTo(11.5f).Within(1e-4), "the cell between posts 0 and 1 takes 11.5: the lowest, not the highest at 12 nor the average");
            // The cell beyond it shares that same low post, so it is at 11.5 too and the two merge. What must not
            // happen is a quad standing above a post it covers, so every quad is checked against its own posts.
            foreach (WaterQuad quad in quads)
                for (int x = 0; x < Posts; x++)
                {
                    double east = -20.0 + x * Cell;
                    if (east < quad.EastFrom - 1e-6 || east > quad.EastTo + 1e-6) continue;
                    float postSurface = 10f + x + (x == 1 ? 0.5f : 2f);
                    Assert.That(quad.SurfaceUp, Is.LessThanOrEqualTo(postSurface + 1e-4), "a quad standing above the post at east " + east);
                }
        }

        /// <summary>Two bodies at different levels never merge across their boundary, however they meet.</summary>
        [Test]
        public void TwoLevelsAreTwoQuads()
        {
            List<WaterQuad> quads = WaterSurface.Build(
                Tile(TileLayer.Ground, (z, x) => 10f),
                Tile(TileLayer.WaterDepth, (z, x) => x <= 2 ? 2f : 5f));
            Assert.That(quads.Count, Is.EqualTo(2 * (Posts - 1)), "each row is a quad of one level and a quad of the other");
            List<float> levels = new List<float>();
            foreach (WaterQuad quad in quads) if (!levels.Contains(quad.SurfaceUp)) levels.Add(quad.SurfaceUp);
            levels.Sort();
            Assert.That(levels, Is.EqualTo(new[] { 12f, 15f }));
        }

        /// <summary>The sea has its own plane; water at or below the datum is not this builder's business (promise 4).</summary>
        [Test]
        public void WaterAtTheDatumIsLeftToTheSeasOwnPlane()
        {
            ReceivedTile seabed = Tile(TileLayer.Ground, (z, x) => -3f);
            ReceivedTile seaDepth = Tile(TileLayer.WaterDepth, (z, x) => 3f);
            Assert.That(WaterSurface.Build(seabed, seaDepth), Is.Empty, "a surface at the datum is the sea's");
            Assert.That(WaterSurface.Build(seabed, Tile(TileLayer.WaterDepth, (z, x) => 4f)), Is.Not.Empty, "a metre above it is not");
        }

        /// <summary>
        /// A creek one post wide (M1.4g promise 1). WG.1 gives a channel cell its own water over a bed the 4 m raster
        /// does not cut, so only the channel's own posts are wet and no cell has four; until 2026-09-20 nothing was
        /// drawn anywhere along it, while the founder stood in it and drank from it.
        /// </summary>
        [Test]
        public void AChannelOnePostWideIsDrawn()
        {
            // A north-south channel down the middle column of posts: its bed two metres under the bank, ankle deep.
            List<WaterQuad> quads = WaterSurface.Build(
                Tile(TileLayer.Ground, (z, x) => x == 2 ? 8f : 10f),
                Tile(TileLayer.WaterDepth, (z, x) => x == 2 ? 0.2f : 0f));
            Assert.That(quads, Is.Not.Empty, "a creek a cell wide is water, and the eye must see it");
            foreach (WaterQuad quad in quads)
            {
                Assert.That(quad.SurfaceUp, Is.EqualTo(8.2f).Within(1e-4), "the channel's own surface, not the bank's ground");
                Assert.That(quad.EastFrom, Is.EqualTo(-10.0), "the cell each side of the wet post, and no further");
                Assert.That(quad.EastTo, Is.EqualTo(10.0));
            }
            Assert.That(quads.Count, Is.EqualTo(Posts - 1), "one run of two cells in each row");
        }

        /// <summary>
        /// The rule the four-corner test was there to keep (M1.4g promise 1): on a shore the water reaches the
        /// waterline's own cell and stops, and never stands above the sand it meets.
        /// </summary>
        [Test]
        public void AShelvingShoreKeepsTheWaterOffTheSand()
        {
            // Ground climbing a metre a post out of a flat body whose surface is 11; the posts above it are dry.
            List<WaterQuad> quads = WaterSurface.Build(
                Tile(TileLayer.Ground, (z, x) => 10f + x),
                Tile(TileLayer.WaterDepth, (z, x) => x == 0 ? 1f : 0f));
            Assert.That(quads, Is.Not.Empty);
            foreach (WaterQuad quad in quads)
            {
                Assert.That(quad.EastTo, Is.LessThanOrEqualTo(-10.0 + 1e-6), "only the cell whose own post is wet");
                Assert.That(quad.SurfaceUp, Is.LessThanOrEqualTo(11f + 1e-4), "never above the first dry post's ground");
            }
        }

        /// <summary>
        /// The clamp (M1.4g promise 1): where the layer leaves a wet post standing higher than the dry ground beside
        /// it, the sheet is held down to that ground rather than floating over it.
        /// </summary>
        [Test]
        public void WaterIsHeldDownToTheLowestDryGroundBesideIt()
        {
            // The channel's post stands 0.8 m of water at 10.8; the dry post east of it is ground at 10.2, under that
            // surface. West of the channel the bank is high, so that cell is not clamped and is left out of the check.
            List<WaterQuad> quads = WaterSurface.Build(
                Tile(TileLayer.Ground, (z, x) => x == 2 ? 10f : (x == 3 ? 10.2f : 12f)),
                Tile(TileLayer.WaterDepth, (z, x) => x == 2 ? 0.8f : 0f));
            Assert.That(quads, Is.Not.Empty);
            // Held down, the eastern cell sits at 10.2 and the western one at 10.8, and the two cannot merge. Unheld,
            // both stand at 10.8 and merge into one run, so asking for a quad at 10.2 is what the clamp answers for.
            bool held = false;
            foreach (WaterQuad quad in quads)
            {
                if (Math.Abs(quad.SurfaceUp - 10.2f) < 1e-4) held = true;
                Assert.That(quad.SurfaceUp, Is.LessThanOrEqualTo(10.8f + 1e-4), "nothing stands above the water's own surface");
            }
            Assert.That(held, Is.True, "10.8 would stand 0.6 m over the dry ground at post 3; the sheet is held down to it");
        }

        /// <summary>A wet post with nothing to show — the clamp puts the surface on its own bed — draws nothing.</summary>
        [Test]
        public void ACellWithNoDepthLeftToShowIsNotDrawn()
        {
            List<WaterQuad> quads = WaterSurface.Build(
                Tile(TileLayer.Ground, (z, x) => x == 2 ? 10f : 10f),
                Tile(TileLayer.WaterDepth, (z, x) => x == 2 ? 0.2f : 0f));
            Assert.That(quads, Is.Empty, "a flat shelf beside a wet post is not a channel: holding 10.2 down to 10 leaves nothing");
        }

        [Test]
        public void TheTwoTilesMustBeTheSameTileAndTheRightLayers()
        {
            ReceivedTile ground = Tile(TileLayer.Ground, (z, x) => 10f);
            ReceivedTile depth = Tile(TileLayer.WaterDepth, (z, x) => 2f);
            Assert.That(WaterSurface.Build(null, depth), Is.Empty);
            Assert.That(WaterSurface.Build(ground, null), Is.Empty);
            Assert.That(() => WaterSurface.Build(depth, depth), Throws.ArgumentException, "a depth is not a ground");
            Assert.That(() => WaterSurface.Build(ground, ground), Throws.ArgumentException, "nor a ground a depth");
            ReceivedTile elsewhere = Tile(TileLayer.WaterDepth, (z, x) => 2f);
            elsewhere.Id = new TileId(1, 0);
            Assert.That(() => WaterSurface.Build(ground, elsewhere), Throws.ArgumentException, "another tile's depth means nothing here");
        }
    }
}
