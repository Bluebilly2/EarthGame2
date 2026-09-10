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
