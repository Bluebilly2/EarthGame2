using System;
using EarthGame.Engine;
using NUnit.Framework;

namespace EarthGame.Tests.Engine
{
    /// <summary>
    /// A lake passes its water on unless its region names it as one that holds it, and where the ground holds an open lake
    /// below its basin's lip the lowest way out is cut in the surface the water is routed on, the ground left as it is; a dam's
    /// wall is crossed at its published point (WG.1b, 2026-09-24). The made valley's floor falls east along row 80 from 60 m to
    /// 20 m and its sides rise 2 m a cell, steeper than the lake rule's level band, so a flat cut into the floor is a lake and
    /// does not leak along the sides' contour; everything asserted is asked of the layers, and a catchment is held against the
    /// same valley's without the thing under test.
    /// </summary>
    public sealed class RiversThatRunThroughTests
    {
        private const int Side = TestRasters.MadeSide;
        private const float Pond = 42.5f;
        private const int Below = 100;

        private static float Valley(int row, int col) => 60.0f - 0.25f * col + 2.0f * Math.Abs(row - 80);

        /// <summary>A flat cut into the floor at the level the floor has at column 70 (columns 50 to 70, rows 72 to 88: 3.6 ha, a
        /// lake by the flat rule), its outlet the floor below it.</summary>
        private static float WithPond(int row, int col) => row >= 72 && row <= 88 && col >= 50 && col <= 70 ? Pond : Valley(row, col);

        /// <summary>The pond behind a wall across the valley at columns 74 and 75, 5 m above it: as a bridge's deck or a gorge's
        /// canopy stands across a river in the tiles.</summary>
        private static float BehindAWall(int row, int col) => col == 74 || col == 75 ? Math.Max(WithPond(row, col), Pond + 5f) : WithPond(row, col);

        /// <summary>The pond as a reservoir behind a dam 8 m above it at columns 74 and 75, with a saddle a metre above it on its
        /// north shore (columns 58 to 62) where a gully falls north to the grid's edge: the lowest way out is the gully's.</summary>
        private static float BehindADam(int row, int col)
        {
            float h = col == 74 || col == 75 ? Math.Max(WithPond(row, col), Pond + 8f) : WithPond(row, col);
            if (row <= 71 && col >= 58 && col <= 62) h = Math.Min(h, Pond + 1f - 0.15f * (71 - row));
            return h;
        }

        private static WorldLayers Made(Func<int, int, float> law, RegionRaster waterBodies = null, Region region = null)
            => WorldLayers.Compute(TestRasters.FromLaw(Side, TestRasters.MadeCellM, TestRasters.MadeExtentM, "valley", law), 1347UL, waterBodies, null, region);

        private static double Gathered(WorldLayers w, int row, int col) => w.Drainage.CatchmentM2(col, row);

        /// <summary>What crosses a row of the gully's flat floor: its columns' catchments summed, the water running north down
        /// whichever columns it entered by and never along a level row, so none is counted twice.</summary>
        private static double DownTheGully(WorldLayers w, int row)
        {
            double sum = 0.0;
            for (int col = 58; col <= 62; col++) sum += Gathered(w, row, col);
            return sum;
        }

        private static RegionRaster Outline(string name, string kind)
            => TestRasters.FromCodes(Side, TestRasters.MadeCellM, TestRasters.MadeExtentM, "valley_water", "water_bodies",
                (row, col) => row >= 72 && row <= 88 && col >= 50 && col <= 70 ? 1u : 0u,
                "\"bodies\":[{\"code\":1,\"osm\":\"way/7\",\"name\":\"" + name + "\",\"kind\":\"" + kind + "\"}]");

        [Test]
        public void APondOnAWatercoursePassesItsWaterOn()
        {
            WorldLayers plain = Made(Valley), pond = Made(WithPond);
            Assert.That((WaterClass)pond.Water[80 * Side + 60], Is.EqualTo(WaterClass.Lake), "the flat is a lake");
            Assert.That(Gathered(pond, 80, Below), Is.GreaterThanOrEqualTo(0.95 * Gathered(plain, 80, Below)),
                "below the pond the floor gathers what it gathers without one: " + Gathered(pond, 80, Below) + " m² against " + Gathered(plain, 80, Below));
        }

        [Test]
        public void ALakeBehindAFalseBarrierHasItsWayOutCutAndTheGroundKept()
        {
            WorldLayers plain = Made(Valley), walled = Made(BehindAWall);
            Assert.That(Gathered(walled, 80, Below), Is.GreaterThanOrEqualTo(0.95 * Gathered(plain, 80, Below)),
                "the water crosses the wall: " + Gathered(walled, 80, Below) + " m² against " + Gathered(plain, 80, Below));
            Assert.That(walled.Heights[80, 74], Is.EqualTo(Pond + 5f), "the wall is still in the ground");
            for (int row = 68; row <= 92; row++)
                for (int col = 71; col <= 73; col++)
                    Assert.That(walled.Drainage.WaterDepthAt(col, row), Is.LessThan(DrainageNetwork.PondDepthM),
                        "no pond stands behind the wall at row " + row + " column " + col);
        }

        [Test]
        public void ALakeItsRegionHoldsKeepsItsWater()
        {
            var held = new Region("made", "the made valley", -35.14, 150.675, TestRasters.MadeExtentM, 237, 8.0, heldLakes: new[] { "Made Pond" });
            WorldLayers plain = Made(Valley), kept = Made(WithPond, Outline("Made Pond", "lake"), held);
            Assert.That((WaterClass)kept.Water[80 * Side + 60], Is.EqualTo(WaterClass.Lake));
            Assert.That(Gathered(kept, 80, Below), Is.LessThan(0.5 * Gathered(plain, 80, Below)),
                "below a held lake only what drains in below it: " + Gathered(kept, 80, Below) + " m² against " + Gathered(plain, 80, Below));
        }

        [Test]
        public void AReservoirsWaterCrossesItsDamWhereTheRegionPutsIt()
        {
            var frame = new LocalFrame(-35.14, 150.675, GeoMath.EarthRadiusM);
            frame.ToLatLon(74.5 * TestRasters.MadeCellM - TestRasters.MadeExtentM / 2.0, TestRasters.MadeExtentM / 2.0 - 80 * TestRasters.MadeCellM, out double lat, out double lon);
            frame.ToLatLon(90 * TestRasters.MadeCellM - TestRasters.MadeExtentM / 2.0, TestRasters.MadeExtentM / 2.0 - 80 * TestRasters.MadeCellM, out double belowLat, out double belowLon);
            var dammed = new Region("made", "the made valley", -35.14, 150.675, TestRasters.MadeExtentM, 237, 8.0,
                dams: new[] { new Dam("Made Dam", lat, lon, belowLat, belowLon) });
            var undammed = new Region("made", "the made valley", -35.14, 150.675, TestRasters.MadeExtentM, 237, 8.0);
            const int gully = 5;
            // What reaches the reservoir: the plain valley's floor gathers it where the reservoir's east shore will be.
            double arriving = Gathered(Made(Valley), 80, 70);
            foreach (RegionRaster water in new[] { null, Outline("Made Reservoir", "reservoir") })
            {
                string how = water == null ? "a lake" : "a reservoir's outline";
                WorldLayers over = Made(BehindADam, water, undammed), through = Made(BehindADam, water, dammed);
                Assert.That(DownTheGully(over, gully), Is.GreaterThanOrEqualTo(0.9 * arriving),
                    how + ": without the dam named, the water leaves by the gully: " + DownTheGully(over, gully) + " m² of the " + arriving + " arriving");
                Assert.That(DownTheGully(over, gully) - DownTheGully(through, gully), Is.GreaterThanOrEqualTo(0.9 * arriving),
                    how + ": with it named, the gully loses the water: " + DownTheGully(over, gully) + " m² against " + DownTheGully(through, gully));
                Assert.That(Gathered(through, 80, Below) - Gathered(over, 80, Below), Is.GreaterThanOrEqualTo(0.9 * arriving),
                    how + ": and the floor below the dam gains it: " + Gathered(through, 80, Below) + " m² against " + Gathered(over, 80, Below));
                Assert.That(through.Heights[80, 74], Is.EqualTo(Pond + 8f), how + ": the dam is still in the ground");
            }
        }
    }
}
