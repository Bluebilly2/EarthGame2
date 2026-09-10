using System;
using EarthGame.ClientCore;
using EarthGame.Engine;
using NUnit.Framework;

namespace EarthGame.Tests.Client
{
    /// <summary>
    /// What the client paints the ground with (M1.4d promises 3 and 4): a colour for every cover the world can
    /// send, and the map a view lays over one tile.
    /// </summary>
    public sealed class GroundColourTests
    {
        private const int Posts = 5;

        private static ReceivedTile Cover(Func<int, int, byte> law, int posts = Posts) => new ReceivedTile
        {
            Id = new TileId(0, 0),
            Layer = TileLayer.GroundCover,
            Posts = posts,
            CellM = 10.0,
            OriginEast = -20.0,
            OriginNorth = -20.0,
            Codes = Fill(law, posts),
        };

        private static byte[,] Fill(Func<int, int, byte> law, int posts)
        {
            byte[,] codes = new byte[posts, posts];
            for (int z = 0; z < posts; z++)
                for (int x = 0; x < posts; x++) codes[z, x] = law(z, x);
            return codes;
        }

        [Test]
        public void EveryCoverHasAColourAndWetIsNeverBrighterThanDry()
        {
            foreach (GroundCover cover in GroundCovers.All)
            {
                GroundColour dry = GroundPalette.Of(cover, 0);
                GroundColour wet = GroundPalette.Of(cover, GroundCovers.Quarters - 1);
                Assert.That(dry.Luminance, Is.GreaterThan(0f), GroundCovers.NameOf(cover) + " has no colour at all");
                Assert.That(wet.Luminance, Is.LessThanOrEqualTo(dry.Luminance + 1e-6f),
                    GroundCovers.NameOf(cover) + " reads brighter wet than dry");
            }
        }

        /// <summary>A code from a later version paints something rather than nothing, and nothing throws.</summary>
        [Test]
        public void ACoverThisBuildDoesNotKnowStillPaints()
        {
            GroundColour unknown = GroundPalette.Of((GroundCover)61, 2);
            Assert.That(unknown.Luminance, Is.GreaterThan(0f));
            Assert.That(GroundPalette.Of(GroundCovers.Pack(GroundCover.Sand, 1)).Luminance,
                Is.GreaterThan(GroundPalette.Of(GroundCovers.Pack(GroundCover.SwampFloor, 1)).Luminance),
                "sand is brighter than a swamp floor at the same wetness, or the peninsula will read wrong");
        }

        /// <summary>Where every post is one cover the whole map is that colour, and nothing is blended into it.</summary>
        [Test]
        public void ATileOfOneCoverIsOneColour()
        {
            byte code = GroundCovers.Pack(GroundCover.Bracken, 2);
            byte[] map = GroundColourMap.Build(Cover((z, x) => code), 16);
            GroundColour want = GroundPalette.Of(code);
            Assert.That(map.Length, Is.EqualTo(16 * 16 * GroundColourMap.BytesPerTexel));
            for (int i = 0; i < map.Length; i += GroundColourMap.BytesPerTexel)
            {
                Assert.That(map[i], Is.EqualTo(Round(want.R)));
                Assert.That(map[i + 1], Is.EqualTo(Round(want.G)));
                Assert.That(map[i + 2], Is.EqualTo(Round(want.B)));
            }
        }

        /// <summary>
        /// Sand to the west and swamp to the east, and the map crosses between them rather than stepping: the
        /// promise is that a beach does not end on a line. Row order is south to north, columns west to east.
        /// </summary>
        [Test]
        public void ABoundaryBlendsAcrossItsPosts()
        {
            byte sand = GroundCovers.Pack(GroundCover.Sand, 0);
            byte swamp = GroundCovers.Pack(GroundCover.SwampFloor, 0);
            const int texels = 32;
            byte[] map = GroundColourMap.Build(Cover((z, x) => x <= 1 ? sand : swamp), texels);
            int row = texels / 2;
            float first = Red(map, texels, row, 0), last = Red(map, texels, row, texels - 1);
            Assert.That(first, Is.EqualTo(GroundPalette.Of(sand).R).Within(0.02f), "the western edge is sand");
            Assert.That(last, Is.EqualTo(GroundPalette.Of(swamp).R).Within(0.02f), "the eastern edge is swamp");
            int steps = 0;
            for (int col = 1; col < texels; col++)
            {
                float step = Math.Abs(Red(map, texels, row, col) - Red(map, texels, row, col - 1));
                Assert.That(step, Is.LessThan(0.20f), "a cliff of colour at column " + col);
                if (step > 0.001f) steps++;
            }
            Assert.That(steps, Is.GreaterThan(3), "the crossing is a blend, not a single step");
        }

        [Test]
        public void ATileWithNoCoverPaintsNothing()
        {
            Assert.That(GroundColourMap.Build(null), Is.Null);
            ReceivedTile empty = Cover((z, x) => 0);
            empty.Codes = null;
            Assert.That(GroundColourMap.Build(empty), Is.Null);
            ReceivedTile ground = Cover((z, x) => 0);
            ground.Layer = TileLayer.Ground;
            Assert.That(() => GroundColourMap.Build(ground), Throws.ArgumentException, "a ground tile is not a cover");
        }

        private static byte Round(float channel) => (byte)(int)(channel * 255f + 0.5f);

        private static float Red(byte[] map, int texels, int row, int col)
            => map[(row * texels + col) * GroundColourMap.BytesPerTexel] / 255f;
    }
}
