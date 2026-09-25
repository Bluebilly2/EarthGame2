using System;
using EarthGame.Engine;
using NUnit.Framework;

namespace EarthGame.Tests.Engine
{
    /// <summary>
    /// Water on a naked body (BF.7 part one, promise 3): the film the skin holds, the rain it catches by Lacy's relation, the
    /// evaporation by the Lewis relation and de Dear's coefficients, the skin's temperature from the balance's own numbers, and
    /// the wet cold's extra loss. The published numbers are written here again, not read from the class.
    /// </summary>
    public sealed class WetnessTests
    {
        private static Surroundings Air(double airC, double windMs, double rh) => new Surroundings(airC, windMs, 1.0, rh, -10.0, 0.5);

        [Test]
        public void TheSkinHoldsTheFilmMeasuredOnHands()
        {
            // US EPA 2011, table 7-24: 4.99e-3 cm of water left on a hand after a 30 s drip, 50 g/m²; Pitol et al. 2020 found 78 at
            // the moment of leaving the water and 38 after ten seconds.
            Assert.That(Wetness.FilmKgPerM2, Is.EqualTo(4.99e-3 / 100.0 * 1000.0).Within(0.001));
            Assert.That(Wetness.FilmKgPerM2, Is.InRange(0.038, 0.078));
            Assert.That(Wetness.CapacityKg, Is.EqualTo(0.050 * 1.8).Within(1e-12), "about 90 g over the whole skin");
        }

        [Test]
        public void RainFallsOnTheTopAndTheWindDrivesItOntoTheFront()
        {
            // Fanger's projected area factors for a standing body, 0.082 from above and 0.35 from in front, times f_eff 0.725.
            Assert.That(Wetness.TopAreaM2, Is.EqualTo(0.725 * 0.082 * 1.8).Within(1e-12));
            Assert.That(Wetness.FrontAreaM2, Is.EqualTo(0.725 * 0.35 * 1.8).Within(1e-12));
            // Lacy (Blocken and Carmeliet 2004): R_wdr = 0.222 U R_h^0.88, kg/(m²·h) for R_h in mm/h.
            double tenInFive = (10.0 * 0.107 + 0.222 * 5.0 * Math.Pow(10.0, 0.88) * 0.457) / 3600.0;
            Assert.That(Wetness.RainKgPerSecond(10.0, 5.0), Is.EqualTo(tenInFive).Within(1e-6));
            Assert.That(Wetness.RainKgPerSecond(10.0, 5.0) * 3600.0, Is.InRange(4.5, 5.3), "about five litres an hour in a steady rain and a breeze");
            Assert.That(Wetness.RainKgPerSecond(2.0, 0.0) * 3600.0, Is.EqualTo(2.0 * Wetness.TopAreaM2).Within(1e-9), "straight down it strikes only the top");
            Assert.That(Wetness.RainKgPerSecond(0.0, 8.0), Is.EqualTo(0.0));
            // Soaked in minutes: a drizzle of 2 mm/h in 3 m/s fills the skin's film within a quarter of an hour.
            Assert.That(Wetness.CapacityKg / Wetness.RainKgPerSecond(2.0, 3.0) / 60.0, Is.LessThan(15.0));
        }

        [Test]
        public void TheWetSkinEvaporatesByTheLewisRelation()
        {
            // ASHRAE 2017, ch. 9: E_max = h_e (p_sk,s − p_a), h_e = 16.5 h_c; de Dear 1997: standing h_c = 10.4 v^0.56, 3.4 still.
            Assert.That(Wetness.ConvectiveCoefficient(2.0), Is.EqualTo(10.4 * Math.Pow(2.0, 0.56)).Within(1e-12));
            Assert.That(Wetness.ConvectiveCoefficient(0.0), Is.EqualTo(3.4));
            double pSkin = 0.61094 * Math.Exp(17.625 * 25.0 / (25.0 + 243.04));
            double pAir = 0.8 * 0.61094 * Math.Exp(17.625 * 10.0 / (10.0 + 243.04));
            double eMax = 16.5 * 10.4 * Math.Pow(2.0, 0.56) * (pSkin - pAir) * 1.8;
            Assert.That(Wetness.MaxEvaporationW(25.0, 10.0, 0.8, 2.0), Is.EqualTo(eMax).Within(1e-9));
            Assert.That(eMax / 1.8, Is.InRange(540.0, 565.0), "about 550 W/m² from skin at 25 °C into 10 °C air at 80 per cent in 2 m/s");
            // A skin as cold and as damp as the air evaporates nothing.
            Assert.That(Wetness.MaxEvaporationW(10.0, 10.0, 1.0, 5.0), Is.EqualTo(0.0).Within(1e-9));
        }

        [Test]
        public void AFilmDriesInMinutesInWindAtAWarmSkin()
        {
            // At a skin held at 20 °C in 10 °C air at 80 per cent and 2 m/s, the film's 90 g leaves at E_max over the latent heat.
            double seconds = Wetness.CapacityKg * 2.43e6 / Wetness.MaxEvaporationW(20.0, 10.0, 0.8, 2.0);
            Assert.That(seconds / 60.0, Is.InRange(4.0, 8.0));
        }

        [Test]
        public void AWetBodyInColdWindLosesMoreAndItsSkinRunsColder()
        {
            // The balance's own numbers for a constricted naked body at 10 °C, 3 m/s, 80 per cent humidity, a degree down.
            Warmth body = new Warmth();
            Surroundings s = Air(10.0, 3.0, 0.8);
            double sensible = body.SensibleLossAt(s, 1.0);
            double boundary = body.TotalInsulationClo(3.0);
            Wetness wet = new Wetness();
            wet.Immerse(1.0);
            wet.Advance(1.0, s, 0.0, body.CoreC, sensible, boundary, 0.0);
            // The skin dry sits above the air by the loss over the air layer; wet it runs colder.
            double dryC = 10.0 + sensible * boundary * 0.155 / 1.8;
            Assert.That(wet.SkinC, Is.LessThan(dryC));
            Assert.That(wet.SkinC, Is.GreaterThan(10.0), "but not below the air: it evaporates less as it cools");
            // The evaporation is the wet skin's E_max at the skin's own temperature, solved together.
            Assert.That(wet.EvaporationW, Is.EqualTo(Wetness.MaxEvaporationW(wet.SkinC, 10.0, 0.8, 3.0)).Within(0.02 * wet.EvaporationW + 0.5));
            // The core pays the air layer's share of it: R_b / (R_t + R_b).
            double rb = boundary * 0.155, rTotal = (body.CoreC - 10.0) * 1.8 / sensible;
            Assert.That(wet.CoreLossW, Is.EqualTo(wet.EvaporationW * rb / rTotal).Within(1e-9));
            Assert.That(wet.CoreLossW, Is.InRange(0.1 * sensible, 0.6 * sensible), "tens of watts more, a wet body in a cold wind");
            Assert.That(wet.SkinWaterKg, Is.LessThan(Wetness.CapacityKg), "and it is drying");
        }

        [Test]
        public void RainKeepsItWetAndItsRunoffTakesHeatToo()
        {
            Warmth body = new Warmth();
            Surroundings s = Air(8.0, 4.0, 0.95);
            double sensible = body.SensibleLossAt(s, 1.0), boundary = body.TotalInsulationClo(4.0);
            Wetness wet = new Wetness();
            for (int i = 0; i < 600; i++) wet.Advance(1.0, s, 8.0, body.CoreC, sensible, boundary, 0.0);
            Assert.That(wet.Level, Is.EqualTo(WetLevel.Soaked), "ten minutes in 8 mm/h");
            Assert.That(wet.RunoffW, Is.GreaterThan(0.0), "the rain the skin cannot hold runs off warmed");
            Assert.That(wet.RunoffW, Is.EqualTo((Wetness.RainKgPerSecond(8.0, 4.0) - 0.0) * 4186.0 * (wet.SkinC - 8.0)).Within(0.5 * wet.RunoffW),
                        "about the rain's mass warmed to the skin, less what the evaporation made room for");
            // Out of the rain it dries.
            Wetness drying = new Wetness();
            drying.Immerse(1.0);
            Surroundings dry = Air(18.0, 2.0, 0.5);
            double dryLoss = body.SensibleLossAt(dry, 0.0), dryBoundary = body.TotalInsulationClo(2.0);
            for (int i = 0; i < 3600 && drying.Level != WetLevel.Dry; i++) drying.Advance(1.0, dry, 0.0, body.CoreC, dryLoss, dryBoundary, 0.0);
            Assert.That(drying.Level, Is.EqualTo(WetLevel.Dry), "an hour in a mild breeze dries the skin");
        }

        [Test]
        public void TheSunDriesTheSkinFaster()
        {
            Warmth body = new Warmth();
            Surroundings s = Air(14.0, 1.0, 0.7);
            double sensible = body.SensibleLossAt(s, 0.5), boundary = body.TotalInsulationClo(1.0);
            Wetness shade = new Wetness(), sun = new Wetness();
            shade.Immerse(1.0);
            sun.Immerse(1.0);
            shade.Advance(60.0, s, 0.0, body.CoreC, sensible, boundary, 0.0);
            sun.Advance(60.0, s, 0.0, body.CoreC, sensible, boundary, 150.0);
            Assert.That(sun.SkinC, Is.GreaterThan(shade.SkinC));
            Assert.That(sun.SkinWaterKg, Is.LessThan(shade.SkinWaterKg));
        }

        [Test]
        public void SoakedClothingKeepsCastellanisShareAndTheWordsFollowTheWater()
        {
            // Castellani et al. 2001: water-saturated clothing 0.75 clo against 1.1 dry.
            Assert.That(Wetness.WetClothingClo(1.1, 1.0), Is.EqualTo(0.75).Within(0.01));
            Assert.That(Wetness.WetClothingClo(1.1, 0.0), Is.EqualTo(1.1));
            Wetness w = new Wetness();
            Assert.That(w.Level, Is.EqualTo(WetLevel.Dry));
            w.Immerse(0.3);
            Assert.That(w.Level, Is.EqualTo(WetLevel.Damp), "wading to the knees");
            w.Immerse(0.2);
            Assert.That(w.Wettedness01, Is.EqualTo(0.3).Within(1e-12), "leaving shallower water dries nothing");
            w.Immerse(1.0);
            Assert.That(w.Level, Is.EqualTo(WetLevel.Soaked));
            Assert.That(Wetness.WordFor(WetLevel.Soaked), Is.EqualTo("soaked"));
            w.Restore(5.0);
            Assert.That(w.SkinWaterKg, Is.EqualTo(Wetness.CapacityKg));
        }
    }
}
