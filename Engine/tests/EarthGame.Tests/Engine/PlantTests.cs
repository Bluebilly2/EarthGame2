using System;
using System.Collections.Generic;
using EarthGame.Engine;
using NUnit.Framework;

namespace EarthGame.Tests.Engine
{
    /// <summary>
    /// v1's PlantTests (slice E4), ported with the community and re-read for Bherwerre's plants (M1.2 promise 4).
    ///
    /// <para>Where a species grows is never asserted. Sites are described — this wet, this deep,
    /// this steep, this exposed — and the tests check that what wins them is what a person who can
    /// read country would expect to find standing there.</para>
    /// </summary>
    public sealed class PlantTests
    {
        [Test]
        public void P1_WalkingInlandFromTheBeachChangesTheVegetationInTheRightOrder()
        {
            // Foredune to swamp: bare sand, then the salt-blown dune, the deep hind-dune sand, the swamp's rim.
            PlantSpecies dune = Dominant(Site(wetness: 0.20, soil: 0.14, exposure: 0.95));
            PlantSpecies hindDune = Dominant(Site(wetness: 0.55, soil: 0.90, exposure: 0.35));
            PlantSpecies coastal = Dominant(Site(wetness: 0.60, soil: 0.50, exposure: 0.75));
            PlantSpecies swamp = Dominant(Site(wetness: 0.92, soil: 0.60, exposure: 0.20));

            Assert.That(dune, Is.EqualTo(PlantSpecies.CoastBanksia), "the salt-blown dune is coast banksia country, got " + Name(dune));
            Assert.That(hindDune, Is.EqualTo(PlantSpecies.Blackbutt), "the deep sand behind it is blackbutt forest, got " + Name(hindDune));
            Assert.That(coastal, Is.EqualTo(PlantSpecies.Bangalay), "the exposed coastal slope is bangalay, got " + Name(coastal));
            Assert.That(swamp, Is.EqualTo(PlantSpecies.SwampPaperbark), "the swamp's rim is paperbark, got " + Name(swamp));
        }

        [Test]
        public void TheChangeIsGradualRatherThanALine()
        {
            PlantSite between = Site(wetness: 0.62, soil: 0.70, exposure: 0.5);
            var seen = new HashSet<PlantSpecies>();
            for (int i = 0; i < 200; i++)
                seen.Add(PlantCommunity.Canopy(between, i / 200.0, Coast));
            Assert.That(seen.Count, Is.GreaterThan(1), "the ground between two communities should carry both, not switch at a line");
        }

        [Test]
        public void P4_TheDuneGoesToWhatCanStandTheSaltWind()
        {
            PlantSite crest = Site(wetness: 0.15, soil: 0.14, exposure: 0.98);

            double coast = PlantSpecies.CoastBanksia.Suitability(crest);
            double oldMan = PlantSpecies.OldManBanksia.Suitability(crest);
            double blackbutt = PlantSpecies.Blackbutt.Suitability(crest);

            Assert.That(coast, Is.GreaterThan(oldMan), "coast banksia must beat old-man banksia on the seaward face");
            Assert.That(blackbutt, Is.EqualTo(0.0).Within(1e-9), "there is not enough sand up there for a blackbutt");
        }

        [Test]
        public void P2_OneConditionItCannotMeetRulesItOut()
        {
            PlantSite thin = Site(wetness: 0.55, soil: 0.10, exposure: 0.2);
            Assert.That(PlantSpecies.Blackbutt.Suitability(thin), Is.EqualTo(0.0).Within(1e-9), "perfect water does not make up for no soil");
            Assert.That(PlantSpecies.Lomandra.Suitability(thin), Is.GreaterThan(0.0), "but something that does not need soil is perfectly happy there");
        }

        [Test]
        public void P5_NothingGrowsWhereNothingCan()
        {
            PlantSite rock = Site(wetness: 0.5, soil: 0.0, exposure: 0.5);
            PlantSite cliff = Site(wetness: 0.5, soil: 0.8, exposure: 0.5, slope: 0.9);
            Assert.That(PlantCommunity.Canopy(rock, 0.5, Coast), Is.Null, "bare rock carries no canopy");
            Assert.That(PlantCommunity.Understory(rock, 0.5, Coast), Is.Null, "nor any understory");
            Assert.That(PlantCommunity.Canopy(cliff, 0.5, Coast), Is.Null, "and nothing holds on to a cliff");
        }

        [Test]
        public void P3_BrackenWantsTheShadeAndGrassWantsTheOpen()
        {
            PlantSite open = Site(wetness: 0.65, soil: 0.45, exposure: 0.5);
            PlantSite under = Site(wetness: 0.65, soil: 0.45, exposure: 0.5, shaded: true);

            double brackenOpen = PlantSpecies.Bracken.Suitability(open);
            double brackenShade = PlantSpecies.Bracken.Suitability(under);
            double grassOpen = PlantSpecies.KangarooGrass.Suitability(open);
            double grassShade = PlantSpecies.KangarooGrass.Suitability(under);

            Assert.That(brackenShade, Is.GreaterThan(brackenOpen), "bracken does better under a canopy");
            Assert.That(grassShade, Is.LessThan(grassOpen), "kangaroo grass does worse under one");
            Assert.That(brackenShade, Is.GreaterThan(grassShade), "and in the shade the bracken wins: " + brackenShade.ToString("F2") + " against " + grassShade.ToString("F2"));
        }

        [Test]
        public void TheUnderstoryFollowsTheOverstory()
        {
            PlantSite site = Site(wetness: 0.68, soil: 0.50, exposure: 0.3);
            int brackenUnder = 0, brackenOpen = 0;
            for (int i = 0; i < 200; i++)
            {
                double roll = i / 200.0;
                PlantSite shaded = site;
                shaded.Shaded = true;
                if (PlantCommunity.Understory(shaded, roll, Coast) == PlantSpecies.Bracken) brackenUnder++;
                if (PlantCommunity.Understory(site, roll, Coast) == PlantSpecies.Bracken) brackenOpen++;
            }
            Assert.That(brackenUnder, Is.GreaterThan(brackenOpen * 1.35), "bracken should be clearly commoner under a canopy: " + brackenUnder + " against " + brackenOpen + " of 200");
        }

        [Test]
        public void P6_TheSameGroundGrowsTheSameThingEveryTime()
        {
            PlantSite site = Site(wetness: 0.55, soil: 0.4, exposure: 0.5);
            for (double roll = 0.0; roll < 1.0; roll += 0.05)
                Assert.That(PlantCommunity.Canopy(site, roll, Coast), Is.EqualTo(PlantCommunity.Canopy(site, roll, Coast)));
        }

        [Test]
        public void P7_AStandIsDominatedRatherThanUniformOrRandom()
        {
            PlantSite site = Site(wetness: 0.50, soil: 0.70, exposure: 0.4);
            var counts = new Dictionary<PlantSpecies, int>();
            const int n = 400;
            for (int i = 0; i < n; i++)
            {
                PlantSpecies s = PlantCommunity.Canopy(site, i / (double)n, Coast);
                if (s == null) continue;
                counts.TryGetValue(s, out int c);
                counts[s] = c + 1;
            }
            int best = 0;
            foreach (KeyValuePair<PlantSpecies, int> pair in counts)
                if (pair.Value > best) best = pair.Value;
            double share = best / (double)n;
            Assert.That(share, Is.InRange(0.40, 0.95), "a stand should be dominated but not pure, got " + (share * 100).ToString("F0") + "% for the commonest of " + counts.Count + " species");
            Assert.That(counts.Count, Is.GreaterThanOrEqualTo(2), "and something else should be growing among it");
        }

        /// <summary>
        /// Every plant wins somewhere among its region's plants (WG.2c, 2026-09-25: a site is contested only by its region's),
        /// and every plant in the catalogue is some region's: a plant no region carries is a line in a table too.
        /// </summary>
        [Test]
        public void P8_EverySpeciesWinsSomewhereOnRealGround()
        {
            var carried = new HashSet<PlantSpecies>();
            foreach (Region region in new[] { Region.Bherwerre, Region.KangarooValley, Region.KangarooValleyWhole })
            {
                var won = new HashSet<PlantSpecies>();
                for (double wet = 0.0; wet <= 1.0; wet += 0.05)
                    for (double soil = 0.0; soil <= 1.2; soil += 0.1)
                        for (double exposure = 0.0; exposure <= 1.0; exposure += 0.25)
                            for (int shade = 0; shade < 2; shade++)
                            {
                                PlantSite site = Site(wet, soil, exposure, shaded: shade == 1);
                                for (double roll = 0.05; roll < 1.0; roll += 0.2)
                                {
                                    PlantSpecies canopy = PlantCommunity.Canopy(site, roll, region.Plants);
                                    PlantSpecies under = PlantCommunity.Understory(site, roll, region.Plants);
                                    if (canopy != null) won.Add(canopy);
                                    if (under != null) won.Add(under);
                                }
                            }
                foreach (PlantSpecies species in region.Plants)
                    Assert.That(won.Contains(species), Is.True, species.DisplayName + " never wins anywhere in " + region.DisplayName + ", so it is not a plant there, it is a line in a table");
                carried.UnionWith(region.Plants);
            }
            foreach (PlantSpecies species in PlantSpecies.All)
                Assert.That(carried.Contains(species), Is.True, species.DisplayName + " is carried by no region");
        }

        [Test]
        public void APlantGrowsBiggerWhereItSuitsIt()
        {
            PlantSite good = Site(wetness: 0.55, soil: 1.0, exposure: 0.3);
            PlantSite marginal = Site(wetness: 0.25, soil: 0.55, exposure: 0.8);
            double tall = PlantSpecies.Blackbutt.HeightAt(good, 0.5);
            double stunted = PlantSpecies.Blackbutt.HeightAt(marginal, 0.5);
            Assert.That(tall, Is.GreaterThan(stunted), "the same tree should be bigger on better ground: " + tall.ToString("F1") + " m against " + stunted.ToString("F1") + " m");
            Assert.That(stunted, Is.GreaterThanOrEqualTo(PlantSpecies.Blackbutt.MinHeightM - 1e-9));
            Assert.That(tall, Is.LessThanOrEqualTo(PlantSpecies.Blackbutt.MaxHeightM + 1e-9));
        }

        [Test]
        public void TheSedgeKeepsToTheWaterAndTheLomandraRangesWider()
        {
            double sedgeAtSwamp = PlantSpecies.SawSedge.Suitability(Site(wetness: 0.95, soil: 0.3, exposure: 0.3));
            double sedgeOnSlope = PlantSpecies.SawSedge.Suitability(Site(wetness: 0.40, soil: 0.3, exposure: 0.3));
            Assert.That(sedgeAtSwamp, Is.GreaterThan(sedgeOnSlope * 5.0), "the sedge is a swamp plant: " + sedgeAtSwamp.ToString("F2") + " against " + sedgeOnSlope.ToString("F2"));
            double lomandraOnSlope = PlantSpecies.Lomandra.Suitability(Site(wetness: 0.40, soil: 0.3, exposure: 0.3));
            Assert.That(lomandraOnSlope, Is.GreaterThan(sedgeOnSlope * 3.0), "and the fibre plant ranges where the sedge will not");
        }

        [Test]
        public void APioneerIsAPlantThatLosesWhereverConditionsAreGood()
        {
            PlantSpecies pioneer = PlantSpecies.Spinifex;
            PlantSite sand = Site(wetness: 0.18, soil: 0.10, exposure: 0.95, slope: 0.06);
            Assert.That(pioneer.Suitability(sand), Is.GreaterThan(0.4), "the sand-binder must hold the ground nothing else will");

            PlantSite spur = Site(wetness: 0.22, soil: 0.30, exposure: 0.80, slope: 0.30);
            Assert.That(Math.Abs(spur.Wetness - pioneer.MoistureOptimum), Is.LessThan(pioneer.MoistureBreadth), "the spur is inside its moisture tolerance, so moisture is not what removes it");
            Assert.That(spur.Slope, Is.LessThan(pioneer.MaxSlope), "and the spur is not too steep for it either");
            Assert.That(pioneer.Suitability(spur), Is.EqualTo(0.0).Within(1e-9), "so a dry spur with 300 mm of soil must still be no place for a pioneer");

            foreach (PlantSite good in new[] { Site(wetness: 0.45, soil: 0.45, exposure: 0.45), Site(wetness: 0.90, soil: 0.60, exposure: 0.25) })
                Assert.That(pioneer.Suitability(good), Is.EqualTo(0.0).Within(1e-9), "and it must be absent from every site with real soil on it");
        }

        [Test]
        public void TheCeilingIsOnlyOnThePioneerAndInertEverywhereElse()
        {
            PlantSite deep = Site(wetness: 0.55, soil: 1.20, exposure: 0.35);
            int ceilinged = 0;
            foreach (PlantSpecies s in PlantSpecies.All)
            {
                if (double.IsPositiveInfinity(s.MaxSoilDepthM))
                {
                    Assert.That(s.Suitability(deep), Is.GreaterThanOrEqualTo(0.0));
                    continue;
                }
                ceilinged++;
                Assert.That(s.Suitability(deep), Is.EqualTo(0.0).Within(1e-9), s.DisplayName + " has a ceiling, so deep ground must rule it out");
            }
            Assert.That(ceilinged, Is.EqualTo(1), "exactly one plant on this coast is a pioneer; if that changes, this test should be the thing that notices");

            PlantSpecies pioneer = PlantSpecies.Spinifex;
            double atLimit = pioneer.Suitability(Site(wetness: 0.18, soil: pioneer.MaxSoilDepthM * 0.97, exposure: 0.95, slope: 0.06));
            double wellInside = pioneer.Suitability(Site(wetness: 0.18, soil: 0.10, exposure: 0.95, slope: 0.06));
            Assert.That(atLimit, Is.GreaterThan(0.0), "it is still there just inside its limit");
            Assert.That(atLimit, Is.LessThan(wellInside * 0.5), "but well thinned by then: " + atLimit.ToString("F3") + " against " + wellInside.ToString("F3"));
        }

        /// <summary>
        /// The salt specialist's floor (M1.2b, 2026-09-10): coast banksia stands the salt wind the trees cannot, and
        /// where the wind does not reach, the trees that wanted shelter take the ground. Without it the banksia held
        /// two fifths of the real peninsula, most of it far from any salt.
        /// </summary>
        [Test]
        public void ASaltSpecialistLosesWhereTheSaltWindDoesNotReach()
        {
            PlantSpecies coast = PlantSpecies.CoastBanksia;
            PlantSite dune = Site(wetness: 0.25, soil: 0.40, exposure: 0.90);
            PlantSite sheltered = Site(wetness: 0.25, soil: 0.40, exposure: 0.10);
            Assert.That(coast.Suitability(dune), Is.GreaterThan(0.3), "the dune in the salt wind is its country");
            Assert.That(coast.Suitability(sheltered), Is.EqualTo(0.0).Within(1e-9), "the same dry sand out of the wind is not");
            PlantSpecies instead = Dominant(sheltered);
            Assert.That(instead, Is.Not.Null.And.Not.EqualTo(coast), "and something else holds it: " + Name(instead));

            double justAbove = coast.Suitability(Site(wetness: 0.25, soil: 0.40, exposure: coast.MinExposure + 0.02));
            Assert.That(justAbove, Is.GreaterThan(0.0).And.LessThan(coast.Suitability(dune) * 0.5),
                "thinned rather than cut just above the floor: " + justAbove.ToString("F3"));

            int floored = 0;
            foreach (PlantSpecies s in PlantSpecies.All)
                if (s.MinExposure > 0.0) floored++;
            Assert.That(floored, Is.EqualTo(1), "exactly one plant on this coast needs the salt wind to hold its ground; if that changes, this test should be the thing that notices");
        }

        /// <summary>
        /// A tall plant stands on a cell as often as the ground suits the best of them (M1.2b, 2026-09-10). Drawn the
        /// old way, any tree that could stand at all took the cell, and the peninsula came out under a canopy on all
        /// but a tenth of a percent of its land.
        /// </summary>
        [Test]
        public void ATreeStandsAsOftenAsTheGroundSuitsTrees()
        {
            PlantSite forest = Site(wetness: 0.55, soil: 1.00, exposure: 0.20);
            PlantSite saltDune = Site(wetness: 0.20, soil: 0.14, exposure: 0.95);   // only coast banksia, and barely
            PlantSite rock = Site(wetness: 0.50, soil: 0.00, exposure: 0.50);
            const int n = 1000;
            foreach (PlantSite site in new[] { forest, saltDune })
            {
                int stands = 0;
                for (int i = 0; i < n; i++)
                    if (PlantCommunity.Canopy(site, (i + 0.5) / n, 0.5, Coast) != null) stands++;
                Assert.That(stands / (double)n, Is.EqualTo(PlantCommunity.CanopyCover(site, Coast)).Within(0.002),
                    "a canopy on as many cells as the ground suits the best of the trees");
            }
            Assert.That(PlantCommunity.CanopyCover(forest, Coast), Is.GreaterThan(0.5), "the deep moist sand is mostly under trees");
            Assert.That(PlantCommunity.CanopyCover(saltDune, Coast), Is.LessThan(0.2), "the salt-blown dune is mostly open");
            Assert.That(PlantCommunity.CanopyCover(rock, Coast), Is.EqualTo(0.0), "and bare rock carries none");
            Assert.That(PlantCommunity.Canopy(saltDune, 0.99, 0.5, Coast), Is.Null, "a roll past the cover leaves the cell open");
            Assert.That(PlantCommunity.Canopy(saltDune, 0.01, 0.5, Coast), Is.SameAs(PlantSpecies.CoastBanksia), "and a roll inside it draws the tree as before");
        }

        [Test]
        public void TheBarkThatStripsIsTheBarkAPersonWouldStrip()
        {
            Assert.That(PlantSpecies.SwampPaperbark.StrippableBarkM, Is.GreaterThan(PlantSpecies.Bangalay.StrippableBarkM), "paperbark sheets come away more readily than fibrous bark");
            Assert.That(PlantSpecies.Bangalay.StrippableBarkM, Is.GreaterThan(PlantSpecies.Blackbutt.StrippableBarkM), "rough-to-the-branches bark gives more than a half-barked trunk");
            Assert.That(PlantSpecies.CoastBanksia.StrippableBarkM, Is.EqualTo(0.0), "and a banksia gives none");
        }

        // ---- helpers ----

        /// <summary>The coast's twelve, which the sites here are contested by (WG.2c: a site is contested by its region's plants).</summary>
        private static readonly IReadOnlyList<PlantSpecies> Coast = Region.Bherwerre.Plants;

        internal static PlantSite Site(double wetness, double soil, double exposure, double slope = 0.15, bool shaded = false)
            => new PlantSite { Wetness = wetness, SoilDepthM = soil, Slope = slope, Exposure = exposure, Shaded = shaded };

        /// <summary>The species that wins this site most often, of the coast's plants unless others are named.</summary>
        internal static PlantSpecies Dominant(PlantSite site, IReadOnlyList<PlantSpecies> plants = null)
        {
            plants = plants ?? Coast;
            var counts = new Dictionary<PlantSpecies, int>();
            const int n = 400;
            for (int i = 0; i < n; i++)
            {
                PlantSpecies s = PlantCommunity.Canopy(site, i / (double)n, plants);
                if (s == null) continue;
                counts.TryGetValue(s, out int c);
                counts[s] = c + 1;
            }
            PlantSpecies best = null;
            int most = 0;
            foreach (KeyValuePair<PlantSpecies, int> pair in counts)
                if (pair.Value > most) { most = pair.Value; best = pair.Key; }
            return best;
        }

        private static string Name(PlantSpecies s) => s == null ? "nothing" : s.DisplayName;
    }
}
