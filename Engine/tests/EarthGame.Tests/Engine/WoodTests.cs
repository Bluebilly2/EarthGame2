using EarthGame.Engine;
using NUnit.Framework;

namespace EarthGame.Tests.Engine
{
    /// <summary>
    /// Wood as a material with published numbers (BF.1 promise 1): every tall plant has a row with a source, and what a
    /// drill or a hearth wants of a wood falls out of the numbers rather than being written on the species.
    /// </summary>
    public sealed class WoodTests
    {
        [Test]
        public void EveryTallPlantHasWoodWithASourceAndNumbersAWoodCouldHave()
        {
            foreach (PlantSpecies species in StandCodes.Tall)
            {
                Wood wood = Wood.Of(species);
                if (ReferenceEquals(species, PlantSpecies.CabbageTreePalm))
                {
                    // A palm, like every monocot, makes no wood: it has no secondary growth (WG.2c, 2026-09-25).
                    Assert.That(wood, Is.Null, "a palm stands as a tree and makes no wood");
                    continue;
                }
                Assert.That(wood, Is.Not.Null, species.Name + " stands as a tree and has no wood");
                Assert.That(wood.DensityDryKgM3, Is.InRange(200.0, 1200.0), species.Name);
                Assert.That(wood.GreenMoisture, Is.InRange(0.3, 2.0), species.Name + ": green wood's water as a share of its dry mass");
                Assert.That(wood.JankaKN, Is.InRange(0.5, 20.0), species.Name);
                Assert.That(wood.RuptureMPa, Is.InRange(5.0, 200.0), species.Name);
                Assert.That(wood.HeatMJPerKgDry, Is.EqualTo(19.0), species.Name + ": one figure for wood");
                Assert.That(wood.Source, Is.Not.Null.And.Not.Empty, species.Name + " has no source");
                Assert.That(wood.Description, Is.Not.Null.And.Not.Empty, species.Name);
            }
            Assert.That(Wood.Of(PlantSpecies.GrassTree), Is.Not.Null, "the grass tree's flower stalk, the coast's own fire drill");
            Assert.That(Wood.Of(PlantSpecies.Bracken), Is.Null, "a fern has no wood");
            Assert.That(Wood.Of(null), Is.Null);
        }

        [Test]
        public void SoftnessAndTheFrictionFireFallOutOfTheNumbers()
        {
            Wood blackbutt = Wood.Of(PlantSpecies.Blackbutt);
            Wood banksia = Wood.Of(PlantSpecies.CoastBanksia);
            Wood stalk = Wood.Of(PlantSpecies.GrassTree);
            Assert.That(blackbutt.Softness01, Is.LessThan(banksia.Softness01), "a blackbutt is the harder wood");
            Assert.That(stalk.Softness01, Is.GreaterThan(banksia.Softness01));
            Assert.That(blackbutt.FrictionFire01, Is.LessThan(0.05), "a wood of 900 kg/m3 takes no coal by friction");
            Assert.That(banksia.FrictionFire01, Is.GreaterThan(0.5), "a banksia's does");
            Assert.That(stalk.FrictionFire01, Is.GreaterThan(0.5));
            Assert.That(Wood.FrictionFireOf(450.0), Is.EqualTo(1.0).Within(1e-9), "the middle of the band");
            Assert.That(Wood.FrictionFireOf(850.0), Is.EqualTo(0.0).Within(1e-9));
            Assert.That(Wood.FrictionFireOf(150.0), Is.EqualTo(0.0).Within(1e-9), "punk is not a hearth either");
        }
    }
}
