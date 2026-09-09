using System;
using EarthGame.Engine;
using NUnit.Framework;

namespace EarthGame.Tests.Engine
{
    /// <summary>
    /// v1's WaterTests W1–W6, ported with the drainage network (M1.2 promise 2), and the sea as a sink.
    ///
    /// <para>Where the water is, is never asserted. A heightfield goes in, the drainage falls out,
    /// and the tests check that what falls out obeys the things water obeys — it runs downhill,
    /// it accumulates, and it never carries less than it did upstream.</para>
    /// </summary>
    public sealed class DrainageTests
    {
        private const double CellM = 8.0;

        [Test]
        public void W1_WaterRunsDownhill()
        {
            DrainageNetwork net = Valley(64, 64);

            for (int z = 0; z < net.Height; z++)
            {
                for (int x = 0; x < net.Width; x++)
                {
                    if (!net.TryStepDownstream(x, z, out int nx, out int nz)) continue;
                    Assert.That(net.HeightAt(nx, nz), Is.LessThan(net.HeightAt(x, z)),
                        "every step downstream must lose height, and (" + x + "," + z + ") did not");
                }
            }
        }

        [Test]
        public void W2_ACellCarriesItsOwnGroundPlusEverythingAboveIt()
        {
            DrainageNetwork net = Valley(48, 48);

            double own = net.CellAreaM2;
            for (int z = 0; z < net.Height; z++)
                for (int x = 0; x < net.Width; x++)
                    Assert.That(net.CatchmentM2(x, z), Is.GreaterThanOrEqualTo(own - 1e-9), "a cell always carries at least its own ground");

            double total = net.Width * net.Height * own;
            double biggest = 0.0;
            for (int z = 0; z < net.Height; z++)
                for (int x = 0; x < net.Width; x++)
                    biggest = Math.Max(biggest, net.CatchmentM2(x, z));

            Assert.That(biggest, Is.GreaterThan(total * 0.25),
                "the outlet of a valley must carry a serious share of the valley, got " + (biggest / total * 100.0).ToString("F0") + "%");
        }

        [Test]
        public void W3_TheChannelIsOnTheValleyFloorAndTheRidgeIsDry()
        {
            DrainageNetwork net = Valley(64, 64);
            int floor = net.Width / 2;
            int z = net.Height - 6;

            Assert.That((int)net.ChannelAt(floor, z), Is.GreaterThanOrEqualTo((int)Channel.Trickle),
                "the valley floor must carry water, got " + net.ChannelAt(floor, z) + " with " + (net.CatchmentM2(floor, z) / 10000.0).ToString("F1") + " ha");
            Assert.That(net.ChannelAt(2, z), Is.EqualTo(Channel.None), "the ridge must be dry, got " + net.ChannelAt(2, z));
            Assert.That(net.CatchmentM2(floor, z), Is.GreaterThan(net.CatchmentM2(2, z) * 50.0), "and by a wide margin, not a whisker");
        }

        [Test]
        public void W4_FollowingTheWaterNeverLeadsToLessOfIt()
        {
            DrainageNetwork net = Valley(64, 64);

            int x = 40, z = 4;
            double previous = net.CatchmentM2(x, z);
            int steps = 0;

            while (net.TryStepDownstream(x, z, out int nx, out int nz) && steps++ < 500)
            {
                double next = net.CatchmentM2(nx, nz);
                Assert.That(next, Is.GreaterThanOrEqualTo(previous - 1e-6), "catchment fell going downstream, from " + previous + " to " + next);
                previous = next;
                x = nx; z = nz;
            }

            Assert.That(steps, Is.GreaterThan(5), "the walk downstream should actually go somewhere");
        }

        [Test]
        public void W5_TwoBranchesJoinIntoOneCarryingBoth()
        {
            const int w = 64, h = 64;
            var heights = new float[w * h];

            for (int z = 0; z < h; z++)
            {
                for (int x = 0; x < w; x++)
                {
                    float along = 40f - z * 0.5f;
                    float separation = Math.Max(0f, 14f - z * 0.35f);
                    float left = Math.Abs(x - (w * 0.5f - separation));
                    float right = Math.Abs(x - (w * 0.5f + separation));
                    float toTrough = Math.Min(left, right);
                    heights[z * w + x] = along + toTrough * 0.6f;
                }
            }

            var net = new DrainageNetwork(heights, w, h, CellM);

            double below = 0.0;
            for (int x = 0; x < w; x++) below = Math.Max(below, net.CatchmentM2(x, h - 2));

            double leftBranch = 0.0, rightBranch = 0.0;
            for (int x = 0; x < w / 2; x++) leftBranch = Math.Max(leftBranch, net.CatchmentM2(x, 20));
            for (int x = w / 2; x < w; x++) rightBranch = Math.Max(rightBranch, net.CatchmentM2(x, 20));

            Assert.That(leftBranch, Is.GreaterThan(0.0));
            Assert.That(rightBranch, Is.GreaterThan(0.0));
            Assert.That(below, Is.GreaterThan(leftBranch + rightBranch),
                "below the junction must carry both branches: " + below.ToString("F0") + " against " + leftBranch.ToString("F0") + " + " + rightBranch.ToString("F0"));
        }

        [Test]
        public void W6_ChannelsAreScarceAndAlwaysTheLowGround()
        {
            DrainageNetwork net = Valley(96, 96);

            int wet = 0, total = net.Width * net.Height;
            for (int z = 0; z < net.Height; z++)
            {
                for (int x = 0; x < net.Width; x++)
                {
                    if (net.ChannelAt(x, z) < Channel.Trickle) continue;
                    wet++;

                    bool lowerThanANeighbour = false;
                    for (int dx = -1; dx <= 1 && !lowerThanANeighbour; dx++)
                    {
                        for (int dz = -1; dz <= 1; dz++)
                        {
                            if (dx == 0 && dz == 0) continue;
                            if (!net.InBounds(x + dx, z + dz)) continue;
                            if (net.HeightAt(x + dx, z + dz) > net.HeightAt(x, z)) { lowerThanANeighbour = true; break; }
                        }
                    }
                    Assert.That(lowerThanANeighbour, Is.True, "water at (" + x + "," + z + ") is not below anything");
                }
            }

            Assert.That(wet, Is.GreaterThan(0), "a valley this size must carry water somewhere");
            Assert.That(wet / (double)total, Is.LessThan(0.10), "watercourses are a small part of a landscape, got " + (wet / (double)total * 100.0).ToString("F1") + "%");
        }

        [Test]
        public void TheSeaIsTheSinkAndCarriesNoChannel()
        {
            // A valley falling south into a flat sea at zero that fills the last twelve rows.
            const int w = 64, h = 64, seaRows = 12;
            var heights = new float[w * h];
            for (int z = 0; z < h; z++)
            {
                for (int x = 0; x < w; x++)
                {
                    float along = 30f - z * 0.6f;
                    float across = Math.Abs(x - (w - 1) * 0.5f) * 0.9f;
                    heights[z * w + x] = z >= h - seaRows ? 0f : Math.Max(0.5f, along + across);
                }
            }
            var net = new DrainageNetwork(heights, w, h, CellM, Heightfield.SeaLevelM);

            Assert.That(net.SeaCells, Is.EqualTo(w * seaRows));
            int floor = w / 2;
            int lastLand = h - seaRows - 1;
            Assert.That(net.IsSea(floor, lastLand + 1), Is.True);
            Assert.That(net.IsSea(floor, lastLand), Is.False);
            Assert.That(net.FlowDirection(floor, lastLand + 1), Is.EqualTo(-1), "the sea drains nowhere");
            for (int z = h - seaRows; z < h; z++)
                for (int x = 0; x < w; x++)
                    Assert.That(net.ChannelAt(x, z), Is.EqualTo(Channel.None), "no channel runs on the sea at (" + x + "," + z + ")");
            Assert.That(net.CatchmentM2(floor, h - 1), Is.EqualTo(net.CellAreaM2), "the sea accumulates nothing");
            Assert.That((int)net.ChannelAt(floor, lastLand), Is.GreaterThanOrEqualTo((int)Channel.Trickle), "the creek reaches the shore");
            Assert.That(net.CatchmentM2(floor, lastLand), Is.GreaterThan(w * (h - seaRows) * net.CellAreaM2 * 0.25), "the outlet at the shore carries the valley");

            // Without a sea the same grid reads the flat as a plain and runs a channel across it.
            var noSea = new DrainageNetwork(heights, w, h, CellM);
            int onTheFlat = 0;
            for (int z = h - seaRows + 1; z < h - 1; z++)
                for (int x = 0; x < w; x++)
                    if (noSea.ChannelAt(x, z) >= Channel.Trickle) onTheFlat++;
            Assert.That(onTheFlat, Is.GreaterThan(0), "which is what the sink is for");
        }

        [Test]
        public void TheNetworkIsTheSameEveryTime()
        {
            DrainageNetwork a = Valley(48, 48);
            DrainageNetwork b = Valley(48, 48);
            for (int z = 0; z < a.Height; z++)
                for (int x = 0; x < a.Width; x++)
                {
                    Assert.That(b.FlowDirection(x, z), Is.EqualTo(a.FlowDirection(x, z)));
                    Assert.That(b.CatchmentM2(x, z), Is.EqualTo(a.CatchmentM2(x, z)));
                    Assert.That(b.HeightAt(x, z), Is.EqualTo(a.HeightAt(x, z)));
                }
        }

        /// <summary>A V-shaped valley falling away to the south, with a ridge on each side.</summary>
        private static DrainageNetwork Valley(int w, int h)
        {
            var heights = new float[w * h];
            for (int z = 0; z < h; z++)
            {
                for (int x = 0; x < w; x++)
                {
                    float along = 60f - z * 0.6f;
                    float across = Math.Abs(x - (w - 1) * 0.5f) * 0.9f;
                    heights[z * w + x] = along + across;
                }
            }
            return new DrainageNetwork(heights, w, h, CellM);
        }
    }
}
