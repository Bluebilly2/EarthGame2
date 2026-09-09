using System;
using EarthGame.Engine;
using NUnit.Framework;

namespace EarthGame.Tests.Engine
{
    /// <summary>
    /// v1's SoilTests, ported with the soil model (M1.2 promise 3). v1 eroded its hummocks first; v2 has no
    /// landform evolution on real data (decision 2026-09-07), so the same hummocks are read raw, and the land
    /// that gathers is found by the drainage network rather than by the erosion's own accumulation.
    ///
    /// <para>Where the soil is deep is never asserted. A landscape goes in, the soil model is asked what it
    /// leaves, and the tests check the pattern every field soil survey finds: thin on the ridges, deep in the
    /// hollows, bare on the steep ground, wet where the land converges.</para>
    /// </summary>
    public sealed class SoilTests
    {
        private const int Side = 96;
        private const double CellM = 16.0;

        [Test]
        public void RidgesAreStonyAndHollowsAreNot()
        {
            float[] land = Hummocks(Side, Side, 1234);
            SoilModel soil = new SoilModel(new DrainageNetwork(land, Side, Side, CellM));

            double convexDepth = 0.0, concaveDepth = 0.0;
            int convex = 0, concave = 0;
            for (int z = 4; z < Side - 4; z++)
            {
                for (int x = 4; x < Side - 4; x++)
                {
                    double curvature = Curvature(land, x, z);
                    if (curvature < -2e-4) { convexDepth += soil.DepthAt(x, z); convex++; }
                    else if (curvature > 2e-4) { concaveDepth += soil.DepthAt(x, z); concave++; }
                }
            }

            Assert.That(convex, Is.GreaterThan(50), "there should be convex ground to measure");
            Assert.That(concave, Is.GreaterThan(50), "and concave ground");
            double onRidges = convexDepth / convex;
            double inHollows = concaveDepth / concave;
            Assert.That(onRidges, Is.LessThan(inHollows),
                "soil must be thinner where the ground sheds it: ridges " + onRidges.ToString("F2") + " m against hollows " + inHollows.ToString("F2") + " m");
        }

        [Test]
        public void SoilDepthIsInTheRangeFieldSurveysFind()
        {
            SoilModel soil = new SoilModel(new DrainageNetwork(Hummocks(Side, Side, 1234), Side, Side, CellM));

            double deepest = 0.0, total = 0.0;
            int cells = 0;
            for (int z = 4; z < Side - 4; z++)
            {
                for (int x = 4; x < Side - 4; x++)
                {
                    double d = soil.DepthAt(x, z);
                    deepest = Math.Max(deepest, d);
                    total += d;
                    cells++;
                }
            }
            double mean = total / cells;
            Assert.That(mean, Is.InRange(0.1, 1.5), "mean soil depth on a hillslope landscape is a few tens of centimetres, got " + mean.ToString("F2") + " m");
            Assert.That(deepest, Is.LessThanOrEqualTo(SoilModel.MaxDepthM + 1e-6));
        }

        [Test]
        public void WeatheringSlowsUnderItsOwnBlanket()
        {
            const double D = SoilModel.HillslopeDiffusivityM2PerYear;
            double gentle = SoilModel.SteadyDepth(D, -0.002);
            double sharp = SoilModel.SteadyDepth(D, -0.02);
            double savage = SoilModel.SteadyDepth(D, -0.2);

            Assert.That(gentle, Is.GreaterThan(sharp), "gentler convexity should hold more soil");
            Assert.That(sharp, Is.GreaterThan(savage));
            Assert.That(savage, Is.EqualTo(0.0), "and a fast-stripping site is bare rock");
            Assert.That(SoilModel.SteadyDepth(D, 0.01), Is.EqualTo(SoilModel.MaxDepthM), "convergent ground collects instead of shedding");
        }

        [Test]
        public void NothingStaysOnGroundTooSteepToHoldIt()
        {
            var surface = new float[Side * Side];
            for (int z = 0; z < Side; z++)
                for (int x = 0; x < Side; x++)
                    surface[z * Side + x] = (float)(100.0 - z * CellM * 0.9);

            var soil = new SoilModel(new DrainageNetwork(surface, Side, Side, CellM));
            for (int z = 2; z < Side - 2; z++)
                for (int x = 2; x < Side - 2; x++)
                    Assert.That(soil.DepthAt(x, z), Is.EqualTo(0f).Within(1e-6), "a slope past the angle of repose carries no soil");
        }

        [Test]
        public void WetnessRisesWhereTheLandGathers()
        {
            float[] land = Hummocks(Side, Side, 1234);
            DrainageNetwork drainage = new DrainageNetwork(land, Side, Side, CellM);
            SoilModel soil = new SoilModel(drainage);

            double wetHigh = 0.0, wetLow = 0.0;
            int high = 0, low = 0;
            for (int z = 4; z < Side - 4; z++)
            {
                for (int x = 4; x < Side - 4; x++)
                {
                    double gathered = drainage.CatchmentM2(x, z);
                    if (gathered > 2e5) { wetHigh += soil.WetnessAt(x, z); high++; }
                    else if (gathered < 1e3) { wetLow += soil.WetnessAt(x, z); low++; }
                }
            }

            Assert.That(high, Is.GreaterThan(5), "there should be gathering ground");
            Assert.That(low, Is.GreaterThan(50), "and shedding ground");
            Assert.That(wetHigh / high, Is.GreaterThan(wetLow / low + 0.15),
                "wetness must rise where the land converges: " + (wetHigh / high).ToString("F2") + " against " + (wetLow / low).ToString("F2"));
        }

        [Test]
        public void TheSeaIsNoSoilAndDoesNotSetTheScale()
        {
            // The hummocks with their south half drowned: the land's wetness scale must be the land's own.
            float[] land = Hummocks(Side, Side, 1234);
            float[] coast = (float[])land.Clone();
            for (int z = Side / 2; z < Side; z++)
                for (int x = 0; x < Side; x++)
                    coast[z * Side + x] = -5f;
            SoilModel dry = new SoilModel(new DrainageNetwork(land, Side, Side, CellM));
            SoilModel wet = new SoilModel(new DrainageNetwork(coast, Side, Side, CellM, Heightfield.SeaLevelM));

            Assert.That(wet.IsSea(10, Side - 5), Is.True);
            Assert.That(wet.DepthAt(10, Side - 5), Is.EqualTo(0f));
            Assert.That(wet.WetnessAt(10, Side - 5), Is.EqualTo(1f));
            // Had the sea's flat, high index sat inside the percentiles, the land would all have read dry.
            int spurs = 0, soaks = 0;
            for (int z = 4; z < Side / 2 - 8; z++)
                for (int x = 4; x < Side - 4; x++)
                {
                    if (wet.WetnessAt(x, z) < 0.2f) spurs++;
                    if (wet.WetnessAt(x, z) > 0.5f) soaks++;
                }
            Assert.That(spurs, Is.GreaterThan(20), "with the sea out of the percentiles the land still has dry spurs");
            Assert.That(soaks, Is.GreaterThan(100), "and wet ground of its own");
            Assert.That(wet.IsSea(10, 10), Is.False);
            Assert.That(wet.DepthAt(10, 10), Is.EqualTo(dry.DepthAt(10, 10)).Within(1e-6), "the sea changes the soil far from it not at all");
        }

        [Test]
        public void SoilTakesAfterTheRockItCameFrom()
        {
            double sandstone = SoilModel.SandFraction(StoneType.Sandstone);
            double shale = SoilModel.SandFraction(StoneType.Shale);
            double obsidian = SoilModel.SandFraction(StoneType.Obsidian);

            Assert.That(sandstone, Is.GreaterThan(shale), "sandstone should weather sandier than shale: " + sandstone.ToString("F2") + " against " + shale.ToString("F2"));
            Assert.That(obsidian, Is.LessThan(0.3), "glassy rock makes no sand");

            double shaleSoil = SoilModel.Fertility(StoneType.Shale, 1.0, 0.5);
            double quartziteSoil = SoilModel.Fertility(StoneType.Quartzite, 1.0, 0.5);
            Assert.That(shaleSoil, Is.GreaterThan(quartziteSoil), "soft fine rock should feed better than hard coarse rock: " + shaleSoil.ToString("F2") + " against " + quartziteSoil.ToString("F2"));
        }

        [Test]
        public void ThinSoilAndDeepSoilAreNotEquallyFertile()
        {
            double thin = SoilModel.Fertility(StoneType.Shale, 0.05, 0.5);
            double deep = SoilModel.Fertility(StoneType.Shale, 1.2, 0.5);
            Assert.That(deep, Is.GreaterThan(thin * 2.0), "there is not much growing in five centimetres");

            double sour = SoilModel.Fertility(StoneType.Shale, 1.2, 1.0);
            Assert.That(sour, Is.LessThan(deep), "and waterlogged ground is sour rather than rich");
        }

        [Test]
        public void TheSameGroundGivesTheSameSoilEveryTime()
        {
            SoilModel a = new SoilModel(new DrainageNetwork(Hummocks(Side, Side, 1234), Side, Side, CellM));
            SoilModel b = new SoilModel(new DrainageNetwork(Hummocks(Side, Side, 1234), Side, Side, CellM));
            for (int i = 0; i < a.DepthM.Length; i++)
            {
                Assert.That(b.DepthM[i], Is.EqualTo(a.DepthM[i]));
                Assert.That(b.Wetness01[i], Is.EqualTo(a.Wetness01[i]));
            }
        }

        [Test]
        public void TheBherwerreStonesKnapAndTheSandstoneGrinds()
        {
            // v1's open item E0: the stones of this coast, not flint. Silcrete and rhyolite flake; quartz flakes
            // badly but flakes; sandstone will not, and abrades instead.
            Assert.That(StoneType.Silcrete.Knappability, Is.GreaterThan(0.5), "silcrete " + StoneType.Silcrete.Knappability.ToString("F2"));
            Assert.That(StoneType.Rhyolite.Knappability, Is.GreaterThan(0.4), "rhyolite " + StoneType.Rhyolite.Knappability.ToString("F2"));
            Assert.That(StoneType.Quartz.Knappability, Is.InRange(0.25, 0.5), "quartz " + StoneType.Quartz.Knappability.ToString("F2"));
            Assert.That(StoneType.Silcrete.EdgeQuality, Is.GreaterThan(StoneType.Quartz.EdgeQuality), "a silcrete edge is the finer of the two");
            Assert.That(StoneType.Flint.EdgeQuality, Is.GreaterThan(StoneType.Silcrete.EdgeQuality), "and flint's finer still, which is why it is missed");
            Assert.That(StoneType.Sandstone.Knappability, Is.LessThan(0.2));
            Assert.That(StoneType.Sandstone.IsAbrasive, Is.True);
            Assert.That(StoneType.Rhyolite.PoundingQuality, Is.GreaterThan(StoneType.Silcrete.PoundingQuality), "the tougher pebble is the better hammer");
            Assert.That(StoneType.ByName("silcrete"), Is.SameAs(StoneType.Silcrete));
        }

        // ---- helpers ----

        private static double Curvature(float[] land, int x, int z)
        {
            double dx2 = CellM * CellM;
            return (land[z * Side + x - 1] + land[z * Side + x + 1] + land[(z - 1) * Side + x] + land[(z + 1) * Side + x] - 4.0 * land[z * Side + x]) / dx2;
        }

        private static float[] Hummocks(int width, int height, int seed)
        {
            var grid = new float[width * height];
            for (int z = 0; z < height; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    double v = 0.0, amplitude = 24.0, frequency = 1.0 / 40.0;
                    for (int octave = 0; octave < 7; octave++)
                    {
                        double s = Math.Sin(x * frequency * 1.7 + (seed + octave) * 0.37)
                                 * Math.Cos(z * frequency * 1.3 - (seed + octave) * 0.21)
                                 + Math.Sin((x + z) * frequency * 0.9 + (seed + octave) * 0.11);
                        v += amplitude * s * 0.5;
                        amplitude *= 0.68;
                        frequency *= 2.1;
                    }
                    grid[z * width + x] = (float)(60.0 + v);
                }
            }
            return grid;
        }
    }
}
