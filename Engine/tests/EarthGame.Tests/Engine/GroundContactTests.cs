using System;
using EarthGame.Engine;
using NUnit.Framework;

namespace EarthGame.Tests.Engine
{
    /// <summary>
    /// A lying body and the ground (BF.7 part one, promise 6): the soils by Kersten's equations and Farouki's measurements, the
    /// ground's pull falling from the transient to the steady disc, Sanak's measured flux between dry and moist sand, and a
    /// bed's insulation. The published numbers are written here again, not read from the class.
    /// </summary>
    public sealed class GroundContactTests
    {
        [Test]
        public void TheSoilsAreKerstensAndFaroukis()
        {
            // Farouki 1981, table 22: oven-dry quartz sand at 1576 kg/m³, 0.288 W/(m·K); its heat capacity (1576/1000)(0.18) of water's.
            Assert.That(SoilHeat.DrySand.ConductivityWmK, Is.EqualTo(0.288));
            Assert.That(SoilHeat.DrySand.HeatCapacityJPerM3K, Is.EqualTo(1.576 * 0.18 * 4.186e6).Within(1.0));
            Assert.That(SoilHeat.DrySand.Effusivity, Is.EqualTo(Math.Sqrt(0.288 * 1.576 * 0.18 * 4.186e6)).Within(1e-6));
            Assert.That(SoilHeat.DrySand.Effusivity, Is.InRange(570.0, 600.0));
            // Kersten's sandy soil: k = 0.1442 (0.7 log w + 0.4) 10^(0.6243 γd), here at 1.6 g/cm³ and 10 per cent water.
            double sand = 0.1442 * (0.7 * Math.Log10(10.0) + 0.4) * Math.Pow(10.0, 0.6243 * 1.6);
            Assert.That(SoilHeat.MoistSand.ConductivityWmK, Is.EqualTo(sand).Within(1e-9));
            Assert.That(sand, Is.InRange(1.5, 1.7));
            // Kersten's silt and clay: k = 0.1442 (0.9 log w − 0.2) 10^(0.6243 γd), 1.3 g/cm³ and 20 per cent.
            double loam = 0.1442 * (0.9 * Math.Log10(20.0) - 0.2) * Math.Pow(10.0, 0.6243 * 1.3);
            Assert.That(SoilHeat.Loam.ConductivityWmK, Is.EqualTo(loam).Within(1e-9));
            // The second source: Lienhard and Lienhard's table A.2 gives loams 0.95 to 2.2 and sands 0.78 to 2.2 W/(m·K), dry to wet.
            Assert.That(SoilHeat.MoistSand.ConductivityWmK, Is.InRange(0.78, 2.2));
            Assert.That(SoilHeat.Loam.ConductivityWmK, Is.InRange(0.8, 2.2));
            // Wetter sand pulls harder.
            Assert.That(SoilHeat.Sandy(1600.0, 0.20).Effusivity, Is.GreaterThan(SoilHeat.Sandy(1600.0, 0.05).Effusivity));
        }

        [Test]
        public void TheGroundTakesMostAtFirstAndSettlesToTheSteadyDisc()
        {
            SoilHeat soil = SoilHeat.MoistSand;
            // q = e ΔT / √(πt) (Lienhard eq. 5.54): the resistance √(πt)/e while it is the lesser.
            Assert.That(GroundContact.GroundResistanceM2KPerW(soil, 0.25), Is.EqualTo(Math.Sqrt(Math.PI * 900.0) / soil.Effusivity).Within(1e-12));
            // The steady disc: Q = 4 R k ΔT (table 5.4, item 12), over the contact's area.
            double area = 0.17 * 1.8, radius = Math.Sqrt(area / Math.PI);
            double disc = area / (4.0 * radius * soil.ConductivityWmK);
            Assert.That(GroundContact.GroundResistanceM2KPerW(soil, 48.0), Is.EqualTo(disc).Within(1e-12));
            double last = 0.0;
            foreach (double h in new[] { 0.1, 0.5, 1.0, 2.0, 4.0, 8.0, 16.0 })
            {
                double r = GroundContact.GroundResistanceM2KPerW(soil, h);
                Assert.That(r, Is.GreaterThanOrEqualTo(last), "the soil under the body warms and pulls less, at " + h + " h");
                Assert.That(r, Is.LessThanOrEqualTo(disc + 1e-12), "and never less than the disc's steady flow");
                last = r;
            }
            // Sanak et al.'s contact: 0.34 of 1.96 m², the share used.
            Assert.That(GroundContact.LyingContactShare, Is.EqualTo(0.34 / 1.96).Within(0.01));
        }

        [Test]
        public void SanaksMeasuredFluxLiesBetweenDrySandAndMoistSand()
        {
            // Supine volunteers on a spineboard over concrete for 20 min, skin 23.6 K above the board: 385 W/m² (Sanak et al. 2025).
            double hours = 20.0 / 60.0, dT = 23.6;
            double dry = dT / GroundContact.GroundResistanceM2KPerW(SoilHeat.DrySand, hours);
            double moist = dT / GroundContact.GroundResistanceM2KPerW(SoilHeat.MoistSand, hours);
            Assert.That(385.0, Is.InRange(dry, moist), "dry sand " + dry.ToString("0") + " W/m², moist " + moist.ToString("0"));
        }

        [Test]
        public void ABedIsItsPressedThicknessOverItsConductivity()
        {
            // Costes et al. 2017, straw: k = 0.0444 + 2.72e-4 ρ; 0.061 at 60 kg/m³ and 0.072 at 100, their measured band 0.045 to 0.08.
            Assert.That(GroundContact.LooseFibreConductivityWmK(60.0), Is.EqualTo(0.0444 + 2.72e-4 * 60.0).Within(1e-12));
            Assert.That(GroundContact.LooseFibreConductivityWmK(100.0), Is.InRange(0.045, 0.08));
            // Twenty kilograms of bracken at 40 kg/m³ over 1.2 m²: 0.42 m loose, 0.125 m under the body at the retained 0.3.
            double loose = 20.0 / (40.0 * 1.2), pressed = loose * 0.3;
            double k = 0.0444 + 2.72e-4 * (40.0 / 0.3);
            Assert.That(GroundContact.BeddingResistanceM2KPerW(20.0, 40.0, 1.2), Is.EqualTo(pressed / k).Within(1e-12));
            Assert.That(pressed / k, Is.InRange(1.0, 2.0), "some ten clo under the body");
            // The army's 30 cm lies 9 cm thick under a body; a bed of nothing is nothing.
            Assert.That(0.30 * GroundContact.BeddingRetainedUnderBody, Is.InRange(0.05, 0.10));
            Assert.That(GroundContact.BeddingResistanceM2KPerW(0.0, 40.0, 1.2), Is.EqualTo(0.0));
        }

        [Test]
        public void ANightOnBareGroundCostsTensOfWattsAndABedAFew()
        {
            // A cold-constricted body (0.9 clo of tissue) at 37 °C on ground at 10 °C.
            double tissue = 0.9 * 0.155;
            double bareFirst = GroundContact.LossW(37.0, 10.0, tissue, 0.0, GroundContact.GroundResistanceM2KPerW(SoilHeat.MoistSand, 1.0));
            double bareLate = GroundContact.LossW(37.0, 10.0, tissue, 0.0, GroundContact.GroundResistanceM2KPerW(SoilHeat.MoistSand, 8.0));
            double bed = GroundContact.BeddingResistanceM2KPerW(20.0, 40.0, 1.2);
            double bedded = GroundContact.LossW(37.0, 10.0, tissue, bed, GroundContact.GroundResistanceM2KPerW(SoilHeat.MoistSand, 8.0));
            Assert.That(bareFirst, Is.InRange(25.0, 60.0), "the first hour on moist sand");
            Assert.That(bareLate, Is.LessThan(bareFirst), "less once the sand has warmed");
            Assert.That(bedded, Is.LessThan(bareLate / 4.0), "a bed takes most of it away");
            // Written again: the contact's area over the three resistances in series.
            Assert.That(bedded, Is.EqualTo(0.17 * 1.8 * 27.0 / (tissue + bed + GroundContact.GroundResistanceM2KPerW(SoilHeat.MoistSand, 8.0))).Within(1e-9));
            // Dry sand pulls less than moist.
            Assert.That(GroundContact.LossW(37.0, 10.0, tissue, 0.0, GroundContact.GroundResistanceM2KPerW(SoilHeat.DrySand, 1.0)), Is.LessThan(bareFirst));
        }
    }
}
