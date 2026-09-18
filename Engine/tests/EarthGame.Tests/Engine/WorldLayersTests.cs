using System;
using EarthGame.Engine;
using NUnit.Framework;

namespace EarthGame.Tests.Engine
{
    /// <summary>
    /// The chain over a made coast (M1.2 promises 6 to 8): a plain sloping to a sandy shore, a lake in a hollow
    /// on the plain, a hill with a crest. Where each thing is, is never asserted from a table: the layers are
    /// asked and the answers checked against what the ground was built to be.
    /// </summary>
    public sealed class WorldLayersTests
    {
        private const int Side = TestRasters.MadeSide;

        private static WorldLayers _layers;

        private static RegionRaster Made() => TestRasters.MadeCoast();

        private static WorldLayers Layers() => _layers ?? (_layers = WorldLayers.Compute(Made(), 1347UL));

        [Test]
        public void TheSeaIsSaltAndHasAFloorThatDeepensWithDistance()
        {
            WorldLayers w = Layers();
            Assert.That((WaterClass)w.Water[155 * Side + 80], Is.EqualTo(WaterClass.Sea));
            Assert.That(w.Has(155, 80, Topology.Sea), Is.True);
            Assert.That(w.HeightsWithFloor[149 * Side + 80], Is.EqualTo(w.Heights[149, 80]), "the shore cell, the last land, keeps its own height");
            Assert.That(w.HeightsWithFloor[150 * Side + 80], Is.EqualTo(-0.5f).Within(1e-3), "the first cell of sea, ten metres out, is half a metre down: wading begins at the shore");
            Assert.That(w.HeightsWithFloor[152 * Side + 80], Is.EqualTo(-1.5f).Within(1e-3), "thirty metres out, a metre and a half");
            Assert.That(w.HeightsWithFloor[160 * Side + 80], Is.EqualTo(-5.5f).Within(1e-3), "a hundred and ten metres out, five and a half");
            Assert.That(w.HeightsWithFloor[100 * Side + 80], Is.EqualTo(w.Heights[100, 80]), "the land is untouched");
        }

        [Test]
        public void TheShoreIsABeachWithADuneBehindItAndTheHillHasACrest()
        {
            WorldLayers w = Layers();
            Assert.That(w.Has(147, 80, Topology.Beach), Is.True, "thirty metres from the water on a soft coast is beach: " + WakeScorer.Topologies(w.TopologyMask[147 * Side + 80]));
            Assert.That(w.Has(147, 80, Topology.ShorePlatform), Is.False);
            Assert.That(w.Has(125, 80, Topology.Dune), Is.True, "the rise behind the beach is dune: " + WakeScorer.Topologies(w.TopologyMask[125 * Side + 80]));
            Assert.That(w.Has(20, 80, Topology.Crest), Is.True, "the hill's top is a crest: " + WakeScorer.Topologies(w.TopologyMask[20 * Side + 80]));
            Assert.That(w.Has(18, 80, Topology.Cliff), Is.True, "and its north face is a cliff: " + WakeScorer.Topologies(w.TopologyMask[18 * Side + 80]));
            Assert.That(w.Has(90, 80, Topology.Crest), Is.False, "the plain is not");
            Assert.That(w.ShoreDistanceM[149 * Side + 80], Is.EqualTo(0f), "the last land cell is the shore");
            Assert.That(w.ShoreDistanceM[100 * Side + 80], Is.EqualTo(490f).Within(1f));
        }

        [Test]
        public void TheHollowIsALakeAndItIsFresh()
        {
            WorldLayers w = Layers();
            Assert.That((WaterClass)w.Water[70 * Side + 80], Is.EqualTo(WaterClass.Lake), "the flat hollow is standing water");
            Assert.That(w.Has(70, 80, Topology.Lake), Is.True);
            Assert.That(WorldLayers.IsFresh((WaterClass)w.Water[70 * Side + 80]), Is.True);
            Assert.That(w.FreshWaterDistanceM[70 * Side + 80], Is.EqualTo(0f));
            Assert.That(w.FreshWaterDistanceM[100 * Side + 80], Is.LessThan(400f), "the plain south of it is within a walk of water");
            Assert.That((WaterClass)w.Water[100 * Side + 80], Is.Not.EqualTo(WaterClass.Lake));
        }

        [Test]
        public void TheCommunityStandsWhereItCanAndTheStoneLiesWhereItIs()
        {
            WorldLayers w = Layers();
            int trees = 0, canopies = 0, fibre = 0;
            for (int r = 40; r < 140; r++)
                for (int c = 0; c < Side; c++)
                {
                    PlantSpecies canopy = w.OverstoryAt(r, c);
                    if (canopy != null) canopies++;
                    if (canopy != null && canopy.Form == PlantForm.Tree) trees++;
                    PlantSpecies floor = w.UnderstoryAt(r, c);
                    if (floor != null && WakeScorer.IsFibre(floor)) fibre++;
                }
            Assert.That(canopies, Is.GreaterThan(1000), "the plain and the dune carry a canopy somewhere");
            Assert.That(fibre, Is.GreaterThan(100), "and fibre somewhere");
            Assert.That(w.OverstoryAt(70, 80), Is.Null, "nothing stands on the lake");
            Assert.That(w.OverstoryAt(155, 80), Is.Null, "nor on the sea");
            StoneType onTheBeach = w.StoneAt(147, 80);
            Assert.That(onTheBeach, Is.Not.Null);
            Assert.That(onTheBeach == StoneType.Rhyolite || onTheBeach == StoneType.Quartz || onTheBeach == StoneType.Quartzite, Is.True, "beach pebbles, got " + onTheBeach);
            Assert.That(w.StoneAt(70, 80), Is.Null, "no stone lies in a lake");
            Assert.That(w.StoneAt(155, 80), Is.Null, "nor on the sea");
        }

        /// <summary>
        /// The soil a root can use on sand is the soil that has formed there (M1.2b, 2026-09-10). The made coast's
        /// planes hold more than a metre of loose material right down to the water, and a plant reading that as soil
        /// would stand on the beach; the beach, which the waves rework, has none a root can use, and the dune has
        /// more the further it lies from the sea.
        /// </summary>
        [Test]
        public void OnTheSandTheSoilIsWhatHasFormedNotWhatHasPiledUp()
        {
            WorldLayers w = Layers();
            Assert.That(w.Has(147, 80, Topology.Beach), Is.True);
            Assert.That(w.Soil.DepthAt(80, 147), Is.GreaterThan(1.0f), "the soil model has sand under the beach");
            Assert.That(w.SiteAt(147, 80).SoilDepthM, Is.EqualTo(0.0), "none of which a root can use");
            Assert.That(w.OverstoryAt(147, 80), Is.Null);
            Assert.That(w.UnderstoryAt(147, 80), Is.Null, "so the beach carries nothing");

            // Up the dune well east of the lake, whose rim would put a slope of its own in the way; a flat that reads
            // as swamp is not dune, and is stepped over.
            double previous = 0.0;
            int dunes = 0;
            for (int row = 142; row >= 92; row -= 10)
            {
                if (!w.Has(row, 140, Topology.Dune)) continue;
                dunes++;
                double reads = w.SiteAt(row, 140).SoilDepthM;
                Assert.That(reads, Is.LessThan(w.Soil.DepthAt(140, row)), "the dune's sand is not all soil at row " + row);
                Assert.That(reads, Is.GreaterThan(previous), "and more of it is, the further from the sea: row " + row);
                previous = reads;
            }
            Assert.That(dunes, Is.GreaterThanOrEqualTo(4), "the walk up the dune crossed dune");
            Assert.That(w.Has(60, 140, Topology.Dune), Is.False, "the plain beyond the dunes");
            Assert.That(w.SiteAt(60, 140).SoilDepthM, Is.EqualTo((double)w.Soil.DepthAt(140, 60)), "reads the soil model's depth as its soil");
        }

        /// <summary>
        /// What the soil on the sand does to what grows (M1.2b): the sand-binder holds the young dune and is gone from
        /// the dune where soil has formed, and the salt specialist stands only where the salt wind reaches.
        /// </summary>
        [Test]
        public void TheSandBinderHoldsTheYoungDuneAndTheSaltSpecialistTheSaltWind()
        {
            WorldLayers w = Layers();
            int young = 0, older = 0, coast = 0;
            for (int r = 0; r < Side; r++)
                for (int c = 0; c < Side; c++)
                {
                    if (w.UnderstoryAt(r, c) == PlantSpecies.Spinifex && w.Has(r, c, Topology.Dune))
                    {
                        if (w.ShoreDistanceM[r * Side + c] <= 150f) young++;
                        else older++;
                    }
                    if (w.OverstoryAt(r, c) != PlantSpecies.CoastBanksia) continue;
                    coast++;
                    Assert.That(w.SiteAt(r, c).Exposure, Is.GreaterThan(PlantSpecies.CoastBanksia.MinExposure),
                        "the coast banksia at row " + r + " col " + c + " stands in the salt wind");
                }
            Assert.That(young, Is.GreaterThan(0), "the sand-binder holds the dune nearest the sea");
            Assert.That(older, Is.Zero, "and is gone from the dune where soil has formed");
            Assert.That(coast, Is.GreaterThan(0), "and the salt specialist has somewhere to stand");
        }

        /// <summary>
        /// A canopy stands as often as the ground suits trees (M1.2b): the salt-blown sand nearest the sea, where only
        /// coast banksia grows at all, is more open than the plain behind it, and the made coast is not one closed
        /// canopy.
        /// </summary>
        [Test]
        public void TheCanopyOpensWhereTreesBarelyGrow()
        {
            WorldLayers w = Layers();
            int land = 0, covered = 0, dune = 0, duneCovered = 0, plain = 0, plainCovered = 0;
            for (int r = 0; r < Side; r++)
                for (int c = 0; c < Side; c++)
                {
                    int i = r * Side + c;
                    var water = (WaterClass)w.Water[i];
                    if (water == WaterClass.Sea || water == WaterClass.Lake || WorldLayers.IsFresh(water)) continue;
                    bool stands = w.Overstory[i] != 0;
                    land++;
                    if (stands) covered++;
                    if (w.Has(r, c, Topology.Dune) && w.ShoreDistanceM[i] <= 150f) { dune++; if (stands) duneCovered++; }
                    if (r >= 95 && r <= 105 && c >= 110) { plain++; if (stands) plainCovered++; }
                }
            double onDune = duneCovered / (double)dune, onPlain = plainCovered / (double)plain;
            Assert.That(onDune, Is.LessThan(onPlain), "the young dune is more open than the plain: " + onDune.ToString("P0") + " against " + onPlain.ToString("P0"));
            Assert.That(covered / (double)land, Is.LessThan(0.95), "and the coast is not one closed canopy: " + (covered / (double)land).ToString("P0"));
        }

        /// <summary>The dune is asked what grows on it before it is called sand (M1.2b), on the made coast as on the peninsula.</summary>
        [Test]
        public void TheDuneCarriesWhatGrowsOnIt()
        {
            WorldLayers w = Layers();
            int dune = 0, held = 0;
            for (int r = 0; r < Side; r++)
                for (int c = 0; c < Side; c++)
                {
                    int i = r * Side + c;
                    var water = (WaterClass)w.Water[i];
                    if (!w.Has(r, c, Topology.Dune) || WorldLayers.IsFresh(water) || water == WaterClass.Swamp) continue;
                    dune++;
                    PlantSpecies under = w.UnderstoryAt(r, c), over = w.OverstoryAt(r, c);
                    GroundCover cover = GroundCovers.CoverOf(w.Cover[i]);
                    if ((under == null || under.IsPioneer) && over == null)
                        Assert.That(cover, Is.EqualTo(GroundCover.DuneSand), "nothing holds row " + r + " col " + c + ", so it is sand");
                    else
                    {
                        held++;
                        Assert.That(cover, Is.Not.EqualTo(GroundCover.DuneSand), "something grows at row " + r + " col " + c + ", so it is not bare sand");
                    }
                }
            Assert.That(held, Is.GreaterThan(dune / 2), "most of the dune is held by what grows on it: " + held + " of " + dune);
        }

        [Test]
        public void TheAnimalsFollowTheirLivings()
        {
            WorldLayers w = Layers();
            int roo = 0, bird = 1, wren = 2;
            Assert.That(AnimalSpecies.All[roo], Is.SameAs(AnimalSpecies.EasternGreyKangaroo));
            Assert.That(AnimalSpecies.All[bird], Is.SameAs(AnimalSpecies.PiedOystercatcher));
            Assert.That(w.Capacity[bird][147 * Side + 80], Is.GreaterThan(5f), "the beach holds the oystercatcher: " + w.Capacity[bird][147 * Side + 80]);
            Assert.That(w.Capacity[bird][60 * Side + 80], Is.EqualTo(0f), "and the plain a kilometre inland does not");
            double rooOnPlain = 0.0;
            int plainCells = 0;
            for (int r = 80; r < 110; r++)
                for (int c = 60; c < 100; c++) { rooOnPlain += w.Capacity[roo][r * Side + c]; plainCells++; }
            Assert.That(rooOnPlain / plainCells, Is.GreaterThan(1.0), "the plain near the lake feeds kangaroos: " + (rooOnPlain / plainCells).ToString("0.0"));
            Assert.That(w.Capacity[roo][155 * Side + 80], Is.EqualTo(0f), "the sea feeds none");
            Assert.That(w.Capacity[wren][100 * Side + 80], Is.GreaterThan(0f));
        }

        [Test]
        public void TheWakeIsChosenByTheCriteriaAndTheCensusSaysWhy()
        {
            WorldLayers w = Layers();
            WakeScorer scorer = new WakeScorer(w);
            WakeScore best = scorer.Best();
            Assert.That(best.Score, Is.GreaterThan(0.0), "somewhere on this coast meets the criteria");
            Assert.That(w.Drainage.IsSea(best.Col, best.Row), Is.False);
            Assert.That(w.Heights[best.Row, best.Col], Is.GreaterThanOrEqualTo(WakeScorer.MinHeightM));
            Assert.That(best.WaterM, Is.LessThanOrEqualTo(WakeScorer.WaterWithinM * 3.0));
            Assert.That(scorer.At(155, 80).Score, Is.EqualTo(0.0), "the sea scores nothing");
            Assert.That(scorer.At(70, 80).Score, Is.EqualTo(0.0), "nor does the lake");
            Assert.That(scorer.At(2, 80).Score, Is.EqualTo(0.0), "nor the region's edge, where the world stops");
            Assert.That(Math.Abs(best.East), Is.LessThanOrEqualTo(TestRasters.MadeExtentM * 0.5 - WakeScorer.EdgeMarginM));
            Assert.That(Math.Abs(best.North), Is.LessThanOrEqualTo(TestRasters.MadeExtentM * 0.5 - WakeScorer.EdgeMarginM));
            Assert.That(best.Score, Is.LessThan(1.0), "the criteria grade, so the best place is one place and not a plateau");
            string census = scorer.Census(best);
            Assert.That(census, Does.Contain("fresh water:"));
            Assert.That(census, Does.Contain("knappable stone:"));
            Assert.That(census, Does.Contain("shelter rock:"));
            Assert.That(census, Does.Contain("wake at east"));
            Assert.That(WakeScorer.Factor(0.0, 500.0), Is.EqualTo(1.0));
            Assert.That(WakeScorer.Factor(500.0, 500.0), Is.EqualTo(2.0 / 3.0).Within(1e-9), "two thirds at the criterion's own distance");
            Assert.That(WakeScorer.Factor(1500.0, 500.0), Is.EqualTo(0.0).Within(1e-9));
            Assert.That(WakeScorer.Factor(100.0, 500.0), Is.GreaterThan(WakeScorer.Factor(400.0, 500.0)), "nearer is better all the way in");
            float[] field = scorer.ScoreField();
            Assert.That(field[best.Row * Side + best.Col], Is.EqualTo((float)best.Score));
            Assert.That(WakeScorer.Factor(double.PositiveInfinity, 500.0), Is.EqualTo(0.0));
        }

        /// <summary>
        /// Water lies on the ground, never under it (2026-09-10). A flat read off the ground settles at one level,
        /// and the cells of the flat that stand above that level are its margin, not its water: the real world of
        /// 2026-09-09 had 5.6 ha of lake whose surface was below its own bed, by as much as half a metre, which
        /// would draw as ground poking through a lake and makes the depth a client is sent meaningless.
        /// </summary>
        [Test]
        public void ALakeCoversOnlyTheGroundBeneathItsOwnSurface()
        {
            // A dish 0.4 m deep in a plain at 22 m, rimmed steeply: flat by the window and inside the level band,
            // so the whole dish grows as one patch and about half of it stands above the level the patch settles at.
            const int Side = 81;
            RegionRaster dish = TestRasters.FromLaw(Side, 10.0, 800.0, "dish", (row, col) =>
            {
                double r = Math.Sqrt((row - 40) * (row - 40) + (col - 40) * (col - 40));
                if (r < 15.0) return (float)(19.6 + 0.4 * (r / 15.0));
                if (r < 17.0) return (float)(20.0 + 2.0 * ((r - 15.0) / 2.0));
                return 22.0f;
            });
            WorldLayers w = WorldLayers.Compute(dish, 1347UL);

            int water = 0, above = 0;
            double deepest = 0.0;
            for (int r = 0; r < Side; r++)
                for (int c = 0; c < Side; c++)
                {
                    int i = r * Side + c;
                    double under = w.Heights[r, c] - w.Surface[i];
                    if (under > deepest) deepest = under;
                    if ((WaterClass)w.Water[i] != WaterClass.Lake) continue;
                    water++;
                    if (w.Heights[r, c] > w.Surface[i] + 1e-4) above++;
                }
            Assert.That(water, Is.GreaterThan(100), "the dish is a lake at all");
            Assert.That(above, Is.Zero, above + " lake cells stand above their own water surface");
            Assert.That(deepest, Is.LessThanOrEqualTo(1e-4), "no cell anywhere has its surface below its ground, by " + deepest.ToString("0.000") + " m");
        }

        /// <summary>
        /// A creek or a stream carries water over its bed by the flow's law (2026-09-18): ankle-deep where the creek's catchment
        /// begins, deeper as the catchment grows, never past the cap; a trickle, damp ground, swamp and dry ground keep the
        /// ground's surface, and the lakes and the sea their levels as before.
        /// </summary>
        [Test]
        public void CreeksAndStreamsCarryWaterByTheirCatchment()
        {
            WorldLayers w = Layers();
            int creeks = 0, streams = 0, dryWithWater = 0;
            for (int r = 0; r < Side; r++)
                for (int c = 0; c < Side; c++)
                {
                    int i = r * Side + c;
                    WaterClass wc = (WaterClass)w.Water[i];
                    double depth = w.Surface[i] - w.Heights[r, c];
                    if (wc == WaterClass.Creek || wc == WaterClass.Stream)
                    {
                        double expected = WorldLayers.ChannelDepthM(w.Drainage.CatchmentM2(c, r));
                        Assert.That(depth, Is.EqualTo(expected).Within(1e-4), "the channel at row " + r + " col " + c);
                        Assert.That(expected, Is.GreaterThanOrEqualTo(WorldLayers.CreekDepthM - 1e-9).And.LessThanOrEqualTo(WorldLayers.StreamDepthMaxM));
                        if (wc == WaterClass.Creek) creeks++; else streams++;
                    }
                    else if (wc == WaterClass.Dry || wc == WaterClass.Damp || wc == WaterClass.Trickle || wc == WaterClass.Swamp)
                    {
                        if (depth > 1e-4) dryWithWater++;
                    }
                }
            Assert.That(creeks + streams, Is.GreaterThan(0), "the made country has a channel that keeps water: creeks " + creeks + ", streams " + streams);
            Assert.That(dryWithWater, Is.EqualTo(0), "no water stands where no channel or lake is");
            // The law itself: nothing under the creek's catchment, ankle-deep at it, a stream at five times the catchment about
            // twice as deep, and the cap far past it.
            Assert.That(WorldLayers.ChannelDepthM(DrainageNetwork.CreekM2 * 0.99), Is.EqualTo(0.0));
            Assert.That(WorldLayers.ChannelDepthM(DrainageNetwork.CreekM2), Is.EqualTo(WorldLayers.CreekDepthM).Within(1e-12));
            Assert.That(WorldLayers.ChannelDepthM(DrainageNetwork.StreamM2), Is.EqualTo(WorldLayers.CreekDepthM * Math.Pow(5.0, 0.4)).Within(1e-9));
            Assert.That(WorldLayers.ChannelDepthM(1e9), Is.EqualTo(WorldLayers.StreamDepthMaxM));
        }

        [Test]
        public void TheMappedBodiesStandAtTheirLevelsAndTheGroundsFlatsStillRead()
        {
            WorldLayers w = WorldLayers.Compute(Made(), 1347UL, TestRasters.MadeWaterBodies());
            Assert.That(w.Bodies.Count, Is.EqualTo(2));
            WorldLayers.WaterBody lake = w.Bodies[0], swamp = w.Bodies[1];
            Assert.That(lake.Name, Is.EqualTo("Made Lake"));
            Assert.That(lake.LevelM, Is.EqualTo(15.0).Within(0.06), "the median of the plain's ground inside the outline");
            Assert.That(lake.OutlineCells, Is.EqualTo(11 * 21));
            Assert.That(lake.WaterCells, Is.GreaterThan(100).And.LessThan(lake.OutlineCells), "the lower half stands under the level: " + lake.WaterCells);
            Assert.That((WaterClass)w.Water[54 * Side + 120], Is.EqualTo(WaterClass.Lake), "the low end of the outline is water");
            Assert.That(w.Surface[54 * Side + 120], Is.EqualTo((float)lake.LevelM));
            Assert.That(w.Surface[54 * Side + 120], Is.GreaterThan(w.Heights[54, 120]), "and stands above its bed");
            Assert.That((WaterClass)w.Water[46 * Side + 120], Is.Not.EqualTo(WaterClass.Lake), "the high end is margin");
            Assert.That(w.Surface[46 * Side + 120], Is.EqualTo(w.Heights[46, 120]), "where the surface is the ground's");
            Assert.That(swamp.Name, Is.EqualTo("Made Swamp"));
            Assert.That(swamp.Kind, Is.EqualTo("wetland"));
            Assert.That((WaterClass)w.Water[125 * Side + 30], Is.EqualTo(WaterClass.Swamp));
            Assert.That(w.Has(125, 30, Topology.Wetland), Is.True);
            Assert.That((WaterClass)w.Water[70 * Side + 80], Is.EqualTo(WaterClass.Lake), "the hollow is still read off the ground");
            Assert.That(w.Surface[155 * Side + 80], Is.EqualTo(0f), "the sea's surface is the datum");
            Assert.That(w.Surface[70 * Side + 80], Is.EqualTo(6f).Within(0.01), "the hollow's level is its flat");
            string census = w.WaterCensus();
            Assert.That(census, Does.Contain("Made Lake: "));
            Assert.That(census, Does.Contain("Made Swamp: "));
            Assert.That(census, Does.Contain("(way/1)"));
        }

        [Test]
        public void TheSameHeightsAndSeedMakeTheSameLayersAndAnotherSeedAnotherDraw()
        {
            WorldLayers a = WorldLayers.Compute(Made(), 1347UL);
            WorldLayers b = WorldLayers.Compute(Made(), 1347UL);
            WorldLayers c = WorldLayers.Compute(Made(), 1348UL);
            Assert.That(b.Overstory, Is.EqualTo(a.Overstory));
            Assert.That(b.Understory, Is.EqualTo(a.Understory));
            Assert.That(b.Stone, Is.EqualTo(a.Stone));
            Assert.That(b.Stand, Is.EqualTo(a.Stand), "the same trees stand in the same places");
            Assert.That(b.Loose, Is.EqualTo(a.Loose), "and the same things lie under them");
            Assert.That(b.TopologyMask, Is.EqualTo(a.TopologyMask));
            Assert.That(b.Water, Is.EqualTo(a.Water));
            Assert.That(b.HeightsWithFloor, Is.EqualTo(a.HeightsWithFloor));
            Assert.That(b.Capacity[0], Is.EqualTo(a.Capacity[0]));
            Assert.That(c.Overstory, Is.Not.EqualTo(a.Overstory), "the seed draws the community");
            Assert.That(c.Water, Is.EqualTo(a.Water), "and nothing else: the water is the ground's");
            Assert.That(c.TopologyMask, Is.Not.EqualTo(a.TopologyMask).Or.EqualTo(a.TopologyMask), "the forest bit may follow the draw");
        }
    }
}
