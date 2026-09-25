using System;
using System.Collections.Generic;
using EarthGame.Engine;
using NUnit.Framework;

namespace EarthGame.Tests.Engine
{
    /// <summary>
    /// WG.2c stage two (2026-09-25): a region names its plants and a site is contested only by them; the slope a plant keeps its
    /// vigour to; the Kangaroo Valley's six new plants; a palm that makes no wood.
    /// </summary>
    public sealed class RegionPlantsTests
    {
        private static readonly Region[] Regions = { Region.Bherwerre, Region.KangarooValley, Region.KangarooValleyWhole };

        [Test]
        public void ARegionsDrawNeverNamesAPlantOutsideItsList()
        {
            foreach (Region region in Regions)
            {
                var allowed = new HashSet<PlantSpecies>(region.Plants);
                int drawn = 0;
                for (double wet = 0.0; wet <= 1.0; wet += 0.1)
                    for (double soil = 0.0; soil <= 1.2; soil += 0.2)
                        for (double slope = 0.0; slope <= 0.9; slope += 0.15)
                            for (double exposure = 0.0; exposure <= 1.0; exposure += 0.25)
                                for (int shade = 0; shade < 2; shade++)
                                {
                                    PlantSite site = new PlantSite { Wetness = wet, SoilDepthM = soil, Slope = slope, Exposure = exposure, Shaded = shade == 1 };
                                    for (double roll = 0.05; roll < 1.0; roll += 0.1)
                                        foreach (PlantSpecies plant in new[] { PlantCommunity.Canopy(site, roll, region.Plants), PlantCommunity.Understory(site, roll, region.Plants) })
                                        {
                                            if (plant == null) continue;
                                            drawn++;
                                            Assert.That(allowed.Contains(plant), Is.True, plant.DisplayName + " was drawn in " + region.DisplayName + ", which does not carry it");
                                        }
                                }
                Assert.That(drawn, Is.GreaterThan(1000), region.DisplayName + " draws plants at all");
            }
        }

        [Test]
        public void TheCoastKeepsItsTwelveAndTheValleyHasItsFifteen()
        {
            Assert.That(Region.Bherwerre.Plants, Is.EqualTo(new[]
            {
                PlantSpecies.Blackbutt, PlantSpecies.Bangalay, PlantSpecies.OldManBanksia, PlantSpecies.CoastBanksia, PlantSpecies.SwampPaperbark,
                PlantSpecies.GrassTree, PlantSpecies.HeathBanksia, PlantSpecies.Bracken, PlantSpecies.Lomandra, PlantSpecies.SawSedge,
                PlantSpecies.KangarooGrass, PlantSpecies.Spinifex,
            }), "the coast's twelve, in the catalogue's order: every draw on the coast is summed as it was");
            Assert.That(Region.CoastPlants, Is.EqualTo(Region.Bherwerre.Plants));

            foreach (Region valley in new[] { Region.KangarooValley, Region.KangarooValleyWhole })
            {
                Assert.That(valley.Plants.Count, Is.EqualTo(15), valley.DisplayName);
                Assert.That(valley.Plants, Does.Contain(PlantSpecies.Bangalay), "bangalay, whose valley records lie half in the valley's own forests");
                foreach (PlantSpecies own in new[] { PlantSpecies.SydneyBlueGum, PlantSpecies.CabbageTreePalm, PlantSpecies.SilvertopAsh, PlantSpecies.RiverOak, PlantSpecies.ScribblyGum, PlantSpecies.LillyPilly })
                    Assert.That(valley.Plants, Does.Contain(own), valley.DisplayName + " carries its own " + own.DisplayName);
                foreach (PlantSpecies coastal in new[] { PlantSpecies.CoastBanksia, PlantSpecies.SwampPaperbark, PlantSpecies.Spinifex })
                    Assert.That(valley.Plants, Does.Not.Contain(coastal), valley.DisplayName + " does not carry " + coastal.DisplayName);
                for (int i = 1; i < valley.Plants.Count; i++)
                    Assert.That(PlantSpecies.NumberOf(valley.Plants[i]), Is.GreaterThan(PlantSpecies.NumberOf(valley.Plants[i - 1])), "in the catalogue's order");
            }
            Assert.That(Region.KangarooValley.Plants, Is.EqualTo(Region.KangarooValleyWhole.Plants), "one country, one list");
            foreach (PlantSpecies own in Region.KangarooValley.Plants)
                if (PlantSpecies.NumberOf(own) > 12) Assert.That(Region.Bherwerre.Plants, Does.Not.Contain(own), "nothing added for the valley reaches the coast");

            Region unnamed = new Region("fixture", "Fixture", -35.14, 150.675, 1600.0, 237, 8.0);
            Assert.That(unnamed.Plants, Is.SameAs(Region.CoastPlants), "a region that names none carries the coast's");
            Assert.That(Region.PlantsOf(null), Is.SameAs(Region.CoastPlants), "and so does a world of no region this build knows");
        }

        [Test]
        public void ARegionsListIsTakenInTheCataloguesOrderAndEachPlantOnce()
        {
            Region written = new Region("fixture", "Fixture", -35.14, 150.675, 1600.0, 237, 8.0,
                plants: new[] { PlantSpecies.LillyPilly, PlantSpecies.Bracken, PlantSpecies.Blackbutt });
            Assert.That(written.Plants, Is.EqualTo(new[] { PlantSpecies.Blackbutt, PlantSpecies.Bracken, PlantSpecies.LillyPilly }));
            Assert.Throws<ArgumentException>(() => new Region("fixture", "Fixture", -35.14, 150.675, 1600.0, 237, 8.0,
                plants: new[] { PlantSpecies.Blackbutt, PlantSpecies.Blackbutt }), "a plant named twice");
            Assert.Throws<ArgumentException>(() => new Region("fixture", "Fixture", -35.14, 150.675, 1600.0, 237, 8.0,
                plants: new PlantSpecies[] { null }), "no plant");
        }

        /// <summary>
        /// A world grows its region's plants and no others: the made coast grown with the valley's list stands none of the coast's
        /// own trees and some of the valley's, and grown with no region names none of the valley's.
        /// </summary>
        [Test]
        public void AWorldGrowsItsRegionsPlantsAndNoOthers()
        {
            Region valleyFixture = new Region("fixture", "Fixture", -35.14, 150.675, TestRasters.MadeExtentM, 237, 8.0, plants: Region.KangarooValley.Plants);
            WorldLayers valley = WorldLayers.Compute(TestRasters.MadeCoast(), 1347UL, region: valleyFixture);
            WorldLayers coast = WorldLayers.Compute(TestRasters.MadeCoast(), 1347UL);
            var valleyNumbers = new HashSet<int>();
            foreach (PlantSpecies p in Region.KangarooValley.Plants) valleyNumbers.Add(PlantSpecies.NumberOf(p));
            var ownGrown = new HashSet<PlantSpecies>();
            for (int i = 0; i < valley.Overstory.Length; i++)
            {
                if (valley.Overstory[i] != 0) Assert.That(valleyNumbers.Contains(valley.Overstory[i]), Is.True, "overstory " + valley.Overstory[i] + " at cell " + i);
                if (valley.Understory[i] != 0) Assert.That(valleyNumbers.Contains(valley.Understory[i]), Is.True, "understory " + valley.Understory[i] + " at cell " + i);
                if (valley.Stand[i] != 0) Assert.That(valleyNumbers.Contains(valley.Stand[i] >> StandCodes.SpeciesShift), Is.True, "a trunk at cell " + i);
                if (valley.Overstory[i] > 12) ownGrown.Add(PlantSpecies.ByNumber(valley.Overstory[i]));
                Assert.That(coast.Overstory[i], Is.LessThanOrEqualTo(12), "the coast grows none of the valley's own");
                Assert.That(coast.Understory[i], Is.LessThanOrEqualTo(12));
            }
            Assert.That(ownGrown.Count, Is.GreaterThanOrEqualTo(2), "the valley's list grows the valley's own trees: " + string.Join(", ", ownGrown));
        }

        // ---- the knee ----

        /// <summary>
        /// A site where a plant's every factor but the slope's is exactly one: its moisture optimum, soil in its full vigour, no
        /// wind, no shade; and for the salt specialist, the full salt wind, whose light is then its one other factor.
        /// </summary>
        private static PlantSite AtBest(PlantSpecies p, double slope)
        {
            // A pioneer's soil between the depth its floor's ramp reaches full vigour at and the depth its ceiling's ramp leaves it.
            double soil = p.IsPioneer
                ? 0.5 * (p.MinSoilDepthM + Math.Max(0.10, 0.35 * p.MinSoilDepthM) + p.MaxSoilDepthM - Math.Max(0.05, 0.35 * p.MaxSoilDepthM))
                : p.MinSoilDepthM + 1.0;
            return new PlantSite { Wetness = p.MoistureOptimum, SoilDepthM = soil, Slope = slope, Exposure = p.MinExposure > 0.0 ? 1.0 : 0.0, Shaded = false };
        }

        /// <summary>
        /// A knee of zero is the fall from level ground the coast's twelve had before the knee was a number, to the bit (WG.2c):
        /// the factor restated here, (steepest - slope) / steepest, times the salt specialist's light in the full wind.
        /// </summary>
        [Test]
        public void AKneeOfZeroIsTheFallFromLevelGroundToTheBit()
        {
            foreach (PlantSpecies p in Region.Bherwerre.Plants)
            {
                Assert.That(p.FullVigourSlope, Is.EqualTo(0.0), p.Name + ": the coast's plants keep the fall from level ground");
                double light = p.MinExposure > 0.0 ? 1.0 - 0.6 * (1.0 - p.ExposureTolerance) * 1.0 : 1.0;
                for (double s = 0.0; s <= p.MaxSlope + 0.05; s += 0.0125)
                {
                    double fall = s >= p.MaxSlope ? 0.0 : Math.Max(0.0, Math.Min(1.0, (p.MaxSlope - s) / p.MaxSlope));
                    Assert.That(p.Suitability(AtBest(p, s)), Is.EqualTo(Math.Max(0.0, Math.Min(1.0, fall * light))), p.Name + " at a slope of " + s);
                }
            }
        }

        [Test]
        public void AKneeHoldsFullVigourToItAndFallsStraightPastIt()
        {
            int kneed = 0;
            foreach (PlantSpecies p in PlantSpecies.All)
            {
                Assert.That(p.FullVigourSlope, Is.GreaterThanOrEqualTo(0.0).And.LessThan(p.MaxSlope), p.Name + ": a knee below its steepest face");
                if (p.FullVigourSlope <= 0.0) continue;
                kneed++;
                double best = p.Suitability(AtBest(p, 0.0));
                Assert.That(best, Is.EqualTo(1.0), p.Name + " at its best");
                Assert.That(p.Suitability(AtBest(p, 0.5 * p.FullVigourSlope)), Is.EqualTo(1.0), p.Name + " halfway to its knee");
                Assert.That(p.Suitability(AtBest(p, p.FullVigourSlope)), Is.EqualTo(1.0), p.Name + " at its knee");
                double midway = 0.5 * (p.FullVigourSlope + p.MaxSlope);
                Assert.That(p.Suitability(AtBest(p, midway)), Is.EqualTo(0.5).Within(1e-12), p.Name + " halfway from its knee to its steepest face");
                Assert.That(p.Suitability(AtBest(p, p.MaxSlope)), Is.EqualTo(0.0), p.Name + " at its steepest face");
            }
            Assert.That(kneed, Is.EqualTo(3), "Sydney blue gum, the cabbage tree palm and lilly pilly keep their vigour on the valley's slopes");
            Assert.That(PlantSpecies.SydneyBlueGum.FullVigourSlope, Is.EqualTo(0.60));
            Assert.That(PlantSpecies.CabbageTreePalm.FullVigourSlope, Is.EqualTo(0.70));
            Assert.That(PlantSpecies.LillyPilly.FullVigourSlope, Is.EqualTo(0.70));
        }

        [Test]
        public void TheValleysBlueGumHoldsTheSlopesItsRecordsLeanTo()
        {
            // 25 degrees, a moderate slope of the valley's walls: the blue gum keeps its vigour where a fall from level ground
            // would have halved it, and there it beats the blackbutt its records do not lean to.
            PlantSite wall = new PlantSite { Wetness = 0.30, SoilDepthM = 0.60, Slope = Math.Tan(25.0 * Math.PI / 180.0), Exposure = 0.2, Shaded = false };
            Assert.That(PlantSpecies.SydneyBlueGum.Suitability(wall), Is.GreaterThan(2.0 * PlantSpecies.Blackbutt.Suitability(wall)));
            Assert.That(PlantTests.Dominant(wall, Region.KangarooValley.Plants), Is.SameAs(PlantSpecies.SydneyBlueGum));
        }

        // ---- the palm ----

        [Test]
        public void APalmMakesNoWoodSoItGivesNoStickNoLogAndIsNotFelled()
        {
            PlantSpecies palm = PlantSpecies.CabbageTreePalm;
            Assert.That(StandCodes.IsTall(palm), Is.True, "it stands as a tree");
            Assert.That(Wood.Of(palm), Is.Null, "a monocot makes no wood");
            Assert.That(DefinitionCatalogue.StickOf(palm), Is.SameAs(DefinitionCatalogue.Stick), "no stick of its own");
            Assert.Throws<KeyNotFoundException>(() => DefinitionCatalogue.LogOf(palm), "and no log");
            Assert.Throws<KeyNotFoundException>(() => DefinitionCatalogue.ByKey("item/stick-cabbage-tree-palm"), "no stick is catalogued for it");
            Assert.That(palm.SticksPerMetre, Is.EqualTo(0.0), "it drops fronds, not sticks");

            Definition trunk = DefinitionCatalogue.PlantOf(palm);
            ThingState standing = default;
            standing.SetLength(22f);
            WorkOffer fell = Work.Judge(WorkKind.CutTrunk, null, default, trunk, standing);
            Assert.That(fell.Outcome, Is.EqualTo(VerbOutcome.WontWork), "whatever is in hand");
            Assert.That(fell.Words, Does.Contain("makes no wood"));
            WorkOffer strip = Work.Judge(WorkKind.StripTrunk, null, default, trunk, standing);
            Assert.That(strip.Outcome, Is.EqualTo(VerbOutcome.WontWork), "and its bark does not strip");

            Definition gum = DefinitionCatalogue.PlantOf(PlantSpecies.SydneyBlueGum);
            Assert.That(Work.Judge(WorkKind.CutTrunk, null, default, gum, standing).Outcome, Is.EqualTo(VerbOutcome.NoTool), "a gum is felled with an edge in hand");
            Assert.That(DefinitionCatalogue.LogOf(PlantSpecies.SydneyBlueGum).Substance, Is.EqualTo(Substance.Wood));
            Assert.That(DefinitionCatalogue.BarkOf(PlantSpecies.SilvertopAsh).Substance, Is.EqualTo(Substance.Bark), "the ash's rough bark strips");
        }

        // ---- the animals ----

        [Test]
        public void AnAnimalIsFedOnlyByItsRegionsPlants()
        {
            // Coast banksia's own ground: the salt-blown dune. The valley does not carry it, so the valley's low trees browse less there.
            PlantSite dune = new PlantSite { Wetness = 0.20, SoilDepthM = 0.14, Slope = 0.05, Exposure = 0.95, Shaded = false };
            double coastLow = PlantCommunity.TotalSuitability(dune, PlantForm.SmallTree, Region.Bherwerre.Plants);
            double valleyLow = PlantCommunity.TotalSuitability(dune, PlantForm.SmallTree, Region.KangarooValley.Plants);
            Assert.That(coastLow, Is.EqualTo(PlantSpecies.CoastBanksia.Suitability(dune)), "the premise: on the coast the banksia is the best low tree there");
            Assert.That(valleyLow, Is.LessThan(coastLow), "and the valley has none of it");
            // The insectivore is fed by every layer, the low trees among them (a browser here eats the grass tree either way).
            Assert.That(AnimalCapacity.ForageScore(ForageStyle.Insectivore, dune, Region.KangarooValley.Plants),
                Is.LessThan(AnimalCapacity.ForageScore(ForageStyle.Insectivore, dune, Region.Bherwerre.Plants)),
                "so a bird that eats insects finds less on the dune among the valley's plants");
        }
    }
}
