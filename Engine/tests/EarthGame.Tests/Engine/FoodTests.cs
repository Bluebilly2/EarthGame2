using System;
using EarthGame.Engine;
using NUnit.Framework;

namespace EarthGame.Tests.Engine
{
    /// <summary>
    /// Food eaten (BF.7 part one, promise 2): each food's energy by the Australian food tables' own equation, what the gut
    /// digests raw and cooked, the water's scaling, and the foods of chain G8 against their sources. The published numbers are
    /// written here again, not read from the class, so a slip in the class is a red test.
    /// </summary>
    public sealed class FoodTests
    {
        [Test]
        public void TheTableEnergyIsTheFoodDatabasesOwnEquation()
        {
            // AFCD Release 3: Energy = protein*17 + sugars*16 + starch*17 + fat*37 + fibre*8. Its F009604, "Yam, wild harvested,
            // cooked": protein 3.2, fat 0.3, sugars 0.5, starch 20.4, fibre 3.5 g per 100 g, printed as 448 kJ.
            double afcd = 3.2 * 17.0 + 0.5 * 16.0 + 20.4 * 17.0 + 0.3 * 37.0 + 3.5 * 8.0;
            Assert.That(Food.LongYamTuber.TableKJPer100g, Is.EqualTo(afcd).Within(1e-9));
            Assert.That(afcd, Is.EqualTo(448.0).Within(0.5), "the database's own printed figure");
        }

        [Test]
        public void CookingATuberGivesAboutAThirdMoreAsCarmodyAndWranghamFound()
        {
            // Raw tuber starch is half digested in the small intestine (green banana 47.3 and 49.4 per cent, potato and plantain
            // 50.7 and 53.6 in vitro); cooked, 0.97. What escapes is fermented at 8 kJ/g (FAO/WHO 1998).
            double rawStarch = 17.0 * 0.50 + 8.0 * 0.50, cookedStarch = 17.0 * 0.97 + 8.0 * 0.03;
            double yamRaw = 3.2 * 17.0 * 0.65 + 0.3 * 37.0 + 0.5 * 16.0 + 20.4 * rawStarch + 3.5 * 8.0;
            double yamCooked = 3.2 * 17.0 * 0.91 + 0.3 * 37.0 + 0.5 * 16.0 + 20.4 * cookedStarch + 3.5 * 8.0;
            Assert.That(Food.LongYamTuber.KJPer100g(FoodPreparation.Raw), Is.EqualTo(yamRaw).Within(1e-9));
            Assert.That(Food.LongYamTuber.KJPer100g(FoodPreparation.Roasted), Is.EqualTo(yamCooked).Within(1e-9));
            // Their own figure for potato: cooking gives 30.5 per cent more energy.
            Assert.That(yamCooked / yamRaw, Is.InRange(1.25, 1.35));
            // Nothing digests more than the table counts, cooked.
            foreach (Food f in Food.All)
                Assert.That(f.KJPer100g(FoodPreparation.Roasted), Is.LessThanOrEqualTo(f.TableKJPer100g + 1e-9), f.Name);
        }

        [Test]
        public void BrackenIsThinFoodByTheKilogramDugAndRicherDried()
        {
            // Brand Miller's dry weight (protein 2.0, fat 1.0, carbohydrate 47.6, fibre 46.6) at McGlone's 90 per cent water; the
            // fibre is chewed and spat out, so it gives nothing.
            Food b = Food.BrackenRhizome;
            Assert.That(b.ProteinG, Is.EqualTo(2.0 * 0.1).Within(1e-9));
            Assert.That(b.StarchG, Is.EqualTo(47.6 * 0.1).Within(1e-9));
            Assert.That(b.FibreG, Is.EqualTo(46.6 * 0.1).Within(1e-9));
            Assert.That(b.FibreSpatOut, Is.True);
            double roasted = 0.2 * 17.0 * 0.91 + 0.1 * 37.0 + 4.76 * (17.0 * 0.97 + 8.0 * 0.03);
            Assert.That(b.KJPer100g(FoodPreparation.Roasted), Is.EqualTo(roasted).Within(1e-9));
            Assert.That(roasted, Is.InRange(80.0, 95.0), "under a hundred kilojoules in 100 g as dug");
            // Dried in the shade, as the Maori stacked it for a fortnight, the same solids in less water are richer by the kilogram.
            Assert.That(b.KJPer100g(FoodPreparation.Roasted, 0.15), Is.EqualTo(roasted * 0.85 / 0.10).Within(1e-9));
            // A day's food, as v1's food slice put it, is kilograms of rhizome out of cold ground.
            Assert.That(b.KgFor(9.5e6, FoodPreparation.Roasted), Is.GreaterThan(10.0));
        }

        [Test]
        public void EachFoodCarriesItsSourcesNumbers()
        {
            // Pigface: Njume's C. rossii, 84.4 per cent water, dry-weight protein 3.9, fat 1.0, fibre 11, ash 2.6; the rest sugars.
            double dry = 15.6;
            Assert.That(Food.PigfaceFruit.ProteinG, Is.EqualTo(dry * 0.039).Within(0.01));
            Assert.That(Food.PigfaceFruit.FatG, Is.EqualTo(dry * 0.010).Within(0.01));
            Assert.That(Food.PigfaceFruit.FibreG, Is.EqualTo(dry * 0.11).Within(0.01));
            Assert.That(Food.PigfaceFruit.SugarsG, Is.EqualTo(dry - dry * (0.039 + 0.010 + 0.11 + 0.026)).Within(0.01));
            Assert.That(Food.PigfaceFruit.TableKJPer100g, Is.InRange(225.0, 250.0), "about 233 kJ in 100 g of fruit");
            // The greenhood: dry weight 14.4, 1.3, 77.0, 5.3 at an estimated 84 per cent water.
            Assert.That(Food.GreenhoodTuber.StarchG, Is.EqualTo(77.0 * 0.16).Within(1e-9));
            Assert.That(Food.GreenhoodTuber.TableKJPer100g, Is.InRange(255.0, 270.0));
            // Lomandra: the class of pith, stalks and buds averaged 382 ± 357 kJ in the Aboriginal food tables.
            Assert.That(Food.LomandraLeafBase.TableKJPer100g, Is.InRange(382.0 - 357.0, 382.0 + 357.0));
            // Banksia nectar at 35 Brix: 35 g of sugar in 100 g, 560 kJ; a spike's morning crop about ten kilojoules.
            Assert.That(Food.BanksiaNectar.TableKJPer100g, Is.EqualTo(35.0 * 16.0).Within(1e-9));
            double spike = Food.BanksiaNectar.EnergyJ(Food.NectarPerSpikeKg, FoodPreparation.Raw) / 1000.0;
            Assert.That(spike, Is.InRange(8.0, 13.0));
            Assert.That(9.5e3 / spike, Is.GreaterThan(500.0), "a day's food would be hundreds of spikes: nectar is a sweetness, not a meal");
        }

        [Test]
        public void RawAndCookedDigestionAreTheMeasuredShares()
        {
            Assert.That(Food.StarchDigestedRaw, Is.InRange(0.47, 0.54), "type B starch raw, 47 to 54 per cent");
            Assert.That(Food.StarchDigestedCooked, Is.InRange(0.96, 0.99), "cooked, 96.7 to 98.8");
            Assert.That(Food.ProteinDigestedRaw, Is.InRange(0.51, 0.65), "raw egg: 51 in ileostomy patients, 65 in the healthy");
            Assert.That(Food.ProteinDigestedCooked, Is.InRange(0.91, 0.94));
            // Sugar needs no cooking: pigface raw and roasted differ only by its little protein.
            double gap = Food.PigfaceFruit.KJPer100g(FoodPreparation.Roasted) - Food.PigfaceFruit.KJPer100g(FoodPreparation.Raw);
            Assert.That(gap, Is.EqualTo(Food.PigfaceFruit.ProteinG * 17.0 * (0.91 - 0.65)).Within(1e-9));
        }

        [Test]
        public void TheRowsAreThePlantsOfTheCatalogueOrSayTheyAreNot()
        {
            Assert.That(Food.LomandraLeafBase.Species, Is.SameAs(PlantSpecies.Lomandra));
            Assert.That(Food.BrackenRhizome.Species, Is.SameAs(PlantSpecies.Bracken));
            Assert.That(Food.BanksiaNectar.Species, Is.SameAs(PlantSpecies.HeathBanksia), "the banksia in flower in late winter");
            Assert.That(Food.LongYamTuber.Species, Is.Null, "the long yam grows north of Stanwell Tops");
            Assert.That(Food.GreenhoodTuber.Species, Is.Null);
            Assert.That(Food.PigfaceFruit.Species, Is.Null);
            Assert.That(Food.Named("BrackenRhizome"), Is.SameAs(Food.BrackenRhizome));
            Assert.That(Food.Named("Nothing"), Is.Null);
            foreach (Food f in Food.All)
            {
                Assert.That(f.Source, Is.Not.Empty, f.Name);
                Assert.That(f.WaterFresh, Is.InRange(0.0, 0.95), f.Name);
            }
        }

        [Test]
        public void AMealIsSaidAsAPersonSaysIt()
        {
            string said = Food.BrackenRhizome.Describe(0.5, FoodPreparation.Roasted);
            Assert.That(said, Does.StartWith("0.5 kg of roasted bracken rhizome, about "), said);
            Assert.That(said, Does.EndWith(" kJ"), said);
            Assert.That(Food.PigfaceFruit.Describe(0.05, FoodPreparation.Raw), Does.StartWith("50 g of raw pigface fruit"));
        }
    }
}
