using System;
using EarthGame.ClientCore;
using EarthGame.Engine;
using NUnit.Framework;

namespace EarthGame.Tests.ClientCore
{
    /// <summary>A footfall sounds of what the server says covers the ground, or of the water over it (M1.5c promise 4).</summary>
    public sealed class FootingTests
    {
        [Test]
        public void EveryCoverIsHeardAsAGroundAnEarKnows()
        {
            foreach (GroundCover cover in (GroundCover[])Enum.GetValues(typeof(GroundCover)))
                Assert.That(Enum.IsDefined(typeof(FootingSound), Footing.Of((byte)cover, 0.0)), Is.True, cover.ToString());
            Assert.That(Footing.Of((byte)GroundCover.Grass, 0.0), Is.EqualTo(FootingSound.Grass));
            Assert.That(Footing.Of((byte)GroundCover.ForestFloor, 0.0), Is.EqualTo(FootingSound.Litter));
            Assert.That(Footing.Of((byte)GroundCover.Rock, 0.0), Is.EqualTo(FootingSound.Rock));
            Assert.That(Footing.Of((byte)GroundCover.DuneSand, 0.0), Is.EqualTo(FootingSound.Sand));
            Assert.That(Footing.Of((byte)GroundCover.BareEarth, 0.0), Is.EqualTo(FootingSound.Soil));
            Assert.That(Footing.Of((byte)GroundCover.Sea, 0.0), Is.EqualTo(FootingSound.Water));
            Assert.That(Footing.Of((byte)GroundCover.Unknown, 0.0), Is.EqualTo(FootingSound.Soil), "nothing said is heard as soil");
        }

        [Test]
        public void TheWetnessQuarterChangesNothingAnEarHears()
        {
            foreach (GroundCover cover in (GroundCover[])Enum.GetValues(typeof(GroundCover)))
                for (int quarter = 1; quarter < GroundCovers.Quarters; quarter++)
                    Assert.That(Footing.Of((byte)((int)cover | quarter << 6), 0.0), Is.EqualTo(Footing.Of((byte)cover, 0.0)), cover + " in quarter " + quarter);
        }

        [Test]
        public void WaterOverTheFootIsHeardAsWater()
        {
            Assert.That(Footing.Of((byte)GroundCover.Grass, Footing.WetDepthM), Is.EqualTo(FootingSound.Water));
            Assert.That(Footing.Of((byte)GroundCover.Grass, Footing.WetDepthM * 0.5), Is.EqualTo(FootingSound.Grass), "a film of water is not a splash");
        }

        [Test]
        public void ThePointTakesItsNearestPostsCodeAndTheWaterOverIt()
        {
            ReceivedTile cover = new ReceivedTile { Layer = TileLayer.GroundCover, Posts = 3, CellM = 4.0, OriginEast = 100.0, OriginNorth = 200.0, Codes = new byte[3, 3] };
            cover.Codes[1, 2] = (byte)GroundCover.Sand;
            cover.Codes[0, 0] = (byte)GroundCover.ForestFloor;
            Assert.That(Footing.At(cover, null, 107.0, 205.0, false), Is.EqualTo(FootingSound.Sand), "nearest post: three east, one north");
            Assert.That(Footing.At(cover, null, 99.0, 199.0, false), Is.EqualTo(FootingSound.Litter), "past the tile's corner, its corner post");
            Assert.That(Footing.At(cover, null, 107.0, 205.0, true), Is.EqualTo(FootingSound.Water), "wading");
            Assert.That(Footing.At(null, null, 107.0, 205.0, false), Is.EqualTo(FootingSound.Soil), "no cover held");

            ReceivedTile depth = new ReceivedTile { Layer = TileLayer.WaterDepth, Posts = 3, CellM = 4.0, OriginEast = 100.0, OriginNorth = 200.0, Heights = new float[3, 3] };
            for (int z = 0; z < 3; z++)
                for (int x = 0; x < 3; x++)
                    depth.Heights[z, x] = 0.3f;
            Assert.That(Footing.At(cover, depth, 107.0, 205.0, false), Is.EqualTo(FootingSound.Water), "a creek over the sand");
        }
    }
}
