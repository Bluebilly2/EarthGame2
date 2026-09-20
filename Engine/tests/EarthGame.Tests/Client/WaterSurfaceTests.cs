using System;
using System.Collections.Generic;
using EarthGame.ClientCore;
using EarthGame.Engine;
using NUnit.Framework;

namespace EarthGame.Tests.Client
{
    /// <summary>
    /// What a view draws of the water a client was streamed (M1.4c promises 1 to 3, the rule amended by M1.4g):
    /// rectangles whose every corner stands at its own post's surface, a dry corner on the ground itself, merged
    /// along a row wherever a body is flat. Four wet corners were required until 2026-09-20, which drew no creek at
    /// all; a level plate a cell wide replaced it for one build, which drew a creek as a staircase of panes.
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

        /// <summary>
        /// A shore: the ground climbs out of a flat body, and the water's skin tapers to the sand instead of lapping
        /// over it. The cell whose own posts are all dry is not drawn at all.
        /// </summary>
        [Test]
        public void AShoreTapersToTheSandAndStopsThere()
        {
            // Ground climbing a metre a post; the westmost post carries a metre of water, so the surface is 11.
            List<WaterQuad> quads = WaterSurface.Build(
                Tile(TileLayer.Ground, (z, x) => 10f + x),
                Tile(TileLayer.WaterDepth, (z, x) => x == 0 ? 1f : 0f));
            Assert.That(quads, Is.Not.Empty, "the water is there and must be drawn");
            foreach (WaterQuad quad in quads)
            {
                Assert.That(quad.EastTo, Is.LessThanOrEqualTo(-10.0 + 1e-6), "only the cell between the wet post and the first dry one");
                Assert.That(quad.UpSouthWest, Is.EqualTo(11f).Within(1e-4), "the wet corner is the water's own surface");
                Assert.That(quad.UpSouthEast, Is.LessThanOrEqualTo(11f), "the dry corner is its own ground, never the water's level");
                Assert.That(quad.UpSouthEast, Is.EqualTo(11f - 0.001f).Within(1e-4), "a millimetre under the sand it meets");
            }
        }

        /// <summary>
        /// A creek one post wide, running downhill (M1.4g promise 1). Until 2026-09-20 nothing along it was drawn at
        /// all; the first answer drew a level plate a cell wide, which stepped down the slope as a staircase of panes.
        /// The surface falls with the bed, and the cells share their corners, so the skin is continuous.
        /// </summary>
        [Test]
        public void AChannelOnePostWideFallsWithItsBedAndJoinsUp()
        {
            // The middle column of posts is a channel two metres under the bank, its bed falling 0.2 m a post north.
            List<WaterQuad> quads = WaterSurface.Build(
                Tile(TileLayer.Ground, (z, x) => x == 2 ? 8f - 0.2f * z : 10f),
                Tile(TileLayer.WaterDepth, (z, x) => x == 2 ? 0.2f : 0f));
            Assert.That(quads, Is.Not.Empty, "a creek a cell wide is water, and the eye must see it");

            // Every corner on the channel's post carries that post's own surface, and every dry corner its own ground.
            foreach (WaterQuad quad in quads)
            {
                int z = (int)Math.Round((quad.NorthFrom + 20.0) / Cell);
                bool channelIsWest = Math.Abs(quad.EastFrom - 0.0) < 1e-6;   // the cell east of the channel post
                float southChannel = 8f - 0.2f * z + 0.2f, northChannel = 8f - 0.2f * (z + 1) + 0.2f;
                float bank = 10f - 0.001f;
                if (channelIsWest)
                {
                    Assert.That(quad.UpSouthWest, Is.EqualTo(southChannel).Within(1e-4));
                    Assert.That(quad.UpNorthWest, Is.EqualTo(northChannel).Within(1e-4));
                    Assert.That(quad.UpSouthEast, Is.EqualTo(bank).Within(1e-4), "the dry bank corner sits on its ground");
                }
                Assert.That(quad.SurfaceUp, Is.LessThanOrEqualTo(10f + 1e-4), "nothing stands above the bank");
            }

            // The skin is continuous: the corner two cells share is one height, north to south along the channel.
            foreach (WaterQuad quad in quads)
            {
                foreach (WaterQuad other in quads)
                {
                    if (Math.Abs(other.NorthFrom - quad.NorthTo) > 1e-6 || Math.Abs(other.EastFrom - quad.EastFrom) > 1e-6) continue;
                    Assert.That(other.UpSouthWest, Is.EqualTo(quad.UpNorthWest).Within(1e-4), "the shared corner is one height, or the creek is a staircase");
                    Assert.That(other.UpSouthEast, Is.EqualTo(quad.UpNorthEast).Within(1e-4));
                }
            }
        }

        /// <summary>A cell of a body whose four posts are not at one height keeps each of them, rather than one for all.</summary>
        [Test]
        public void EveryCornerKeepsItsOwnPostsSurface()
        {
            // Ground rises east; the depth falls to match, so the surface is 12 m except at post 1 where it is 11.5.
            List<WaterQuad> quads = WaterSurface.Build(
                Tile(TileLayer.Ground, (z, x) => 10f + x),
                Tile(TileLayer.WaterDepth, (z, x) => x == 1 ? 0.5f : 2f));
            WaterQuad first = quads[0];
            Assert.That(first.EastFrom, Is.EqualTo(-20.0), "the westmost cell");
            Assert.That(first.UpSouthWest, Is.EqualTo(12f).Within(1e-4), "post 0's own surface");
            Assert.That(first.UpSouthEast, Is.EqualTo(11.5f).Within(1e-4), "post 1's own, not post 0's and not the lowest of all four");
            // Each corner against its own post, which is what a cornered quad promises: the old form of this check
            // held a whole quad to one post's surface, which only a flat plate could keep.
            foreach (WaterQuad quad in quads)
            {
                int west = (int)Math.Round((quad.EastFrom + 20.0) / Cell), east = (int)Math.Round((quad.EastTo + 20.0) / Cell);
                Assert.That(quad.UpSouthWest, Is.LessThanOrEqualTo(Post(west) + 1e-4), "the corner at post " + west);
                Assert.That(quad.UpNorthWest, Is.LessThanOrEqualTo(Post(west) + 1e-4), "the corner at post " + west);
                Assert.That(quad.UpSouthEast, Is.LessThanOrEqualTo(Post(east) + 1e-4), "the corner at post " + east);
                Assert.That(quad.UpNorthEast, Is.LessThanOrEqualTo(Post(east) + 1e-4), "the corner at post " + east);
            }

            static float Post(int x) => 10f + x + (x == 1 ? 0.5f : 2f);
        }

        /// <summary>Two bodies at different levels never merge across their boundary, however they meet.</summary>
        [Test]
        public void TwoLevelsAreTwoQuads()
        {
            List<WaterQuad> quads = WaterSurface.Build(
                Tile(TileLayer.Ground, (z, x) => 10f),
                Tile(TileLayer.WaterDepth, (z, x) => x <= 2 ? 2f : 5f));
            // Both levels are there, and no flat quad spans the two: a body at one height never swallows a body at
            // another. The cell where they meet is its own quad, sloping from one to the other, since M1.4g gave every
            // corner its own height; before that it was left undrawn.
            List<float> levels = new List<float>();
            foreach (WaterQuad quad in quads)
            {
                if (quad.Flat && !levels.Contains(quad.SurfaceUp)) levels.Add(quad.SurfaceUp);
                Assert.That(quad.Flat && quad.SurfaceUp > 12f + 1e-4 && quad.LowestUp < 15f - 1e-4, Is.False, "a flat quad across both levels");
            }
            levels.Sort();
            Assert.That(levels, Is.EqualTo(new[] { 12f, 15f }), "each level drawn at its own height");
            Assert.That(quads.Count, Is.EqualTo(3 * (Posts - 1)), "a run of each level in every row, and the cell between them");
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
