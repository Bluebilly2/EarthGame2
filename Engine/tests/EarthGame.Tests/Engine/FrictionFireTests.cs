using System;
using EarthGame.Engine;
using NUnit.Framework;

namespace EarthGame.Tests.Engine
{
    /// <summary>
    /// Fire by friction and tinder by coal (BF.5 part one, promises 6 and 7): the power the hands put in, the heat's share to the
    /// hearth by the woods' effusivities, the surface's rise as a semi-infinite solid's, the coal by the pair of woods and their
    /// dryness, every failure with its reason in words, and the coal laid in tinder that flames, smokes when damp, smoulders
    /// unblown and dies when too small. The formulas are written here again; the placed estimates are held to the orders
    /// practice reports.
    /// </summary>
    public sealed class FrictionFireTests
    {
        private static readonly Wood Stalk = Wood.GrassTree;
        private static readonly Wood Banksia = Wood.CoastBanksia;

        private static FrictionAttempt Drill(double moisture, double drillM = 0.011, double capacity = 1.0) =>
            FrictionFire.Judge(FrictionMethod.HandDrill, Stalk, moisture, drillM, Banksia, moisture, capacity);

        [Test]
        public void TheRubsPowerIsItsFrictionTimesItsForceTimesItsSpeed()
        {
            // A spun disc's mean rubbing speed is two-thirds of its rim's.
            Assert.That(Drill(0.1).PowerW, Is.EqualTo(0.25 * 40.0 * 0.9 * 2.0 / 3.0).Within(1e-12));
            Assert.That(Drill(0.1).PowerW, Is.InRange(3.0, 15.0), "a few watts at the tip; the body spends several times that");
            FrictionAttempt plough = FrictionFire.Judge(FrictionMethod.FirePlough, Banksia, 0.1, 0.012, Stalk, 0.1, 1.0);
            Assert.That(plough.PowerW, Is.EqualTo(0.25 * 100.0 * 1.2).Within(1e-12), "the plough leans the body's weight into it");
            // Duncan's bow drill: 60 N and the bow at 1.86 m/s with his friction of 0.25, "about 21 W"; this reckoning's two-thirds
            // for a spun tip's mean speed puts it a little under.
            FrictionAttempt bow = FrictionFire.Judge(FrictionMethod.BowDrill, 600.0, 0.08, 0.0254, 600.0, "elm", 0.08, 1.0);
            Assert.That(bow.PowerW, Is.EqualTo(0.25 * 60.0 * 1.86 * 2.0 / 3.0).Within(1e-12));
            Assert.That(bow.PowerW / 21.0, Is.InRange(0.8, 1.0));
            Assert.That(Drill(0.1, capacity: 0.5).PowerW, Is.EqualTo(0.5 * Drill(0.1).PowerW).Within(1e-12), "thirst takes its share of the arms");
        }

        [Test]
        public void TheHearthTakesTheHeatByItsEffusivity()
        {
            // The Wood Handbook's conductivity: k = G (0.1941 + 0.4064 M) + 0.01864.
            Assert.That(FrictionFire.ConductivityWmK(500.0, 0.12), Is.EqualTo(0.5 * (0.1941 + 0.4064 * 0.12) + 0.01864).Within(1e-12));
            Assert.That(FrictionFire.ConductivityWmK(640.0, 0.12), Is.InRange(0.12, 0.2), "a light hardwood across the grain");
            double eh = FrictionFire.EffusivityOf(Banksia.DensityDryKgM3, 0.1), ed = FrictionFire.EffusivityOf(Stalk.DensityDryKgM3, 0.1);
            FrictionAttempt a = Drill(0.1);
            Assert.That(a.HearthShare, Is.EqualTo(eh / (eh + ed)).Within(1e-12));
            Assert.That(a.HearthShare, Is.GreaterThan(0.5), "the denser banksia takes more of the heat than the pithy stalk");
            Assert.That(FrictionFire.Judge(FrictionMethod.HandDrill, Banksia, 0.1, 0.011, Banksia, 0.1, 1.0).HearthShare, Is.EqualTo(0.5).Within(1e-12), "a pair of one wood halves it");
            Assert.That(a.HearthHeatW, Is.EqualTo(a.PowerW * a.HearthShare).Within(1e-12));
        }

        [Test]
        public void TheSurfaceRisesAsASemiInfiniteSolidsDoesAtTheRimWhereTheRubIsFastest()
        {
            // t = (pi/4) (e dT / q)^2 to the coal's temperature, slowed by the latent heat of the water on the way, under the rim's
            // heat: 1.5 times the tip's mean, the rub's speed going as the radius.
            FrictionAttempt a = Drill(0.0);
            double eh = FrictionFire.EffusivityOf(Banksia.DensityDryKgM3, 0.0);
            double rim = 1.5 * a.HearthHeatW / (Math.PI / 4.0 * 0.011 * 0.011);
            Assert.That(a.HearthFluxWm2, Is.EqualTo(rim).Within(1e-6));
            double x = eh * (FrictionFire.EmberC - 20.0) / rim;
            Assert.That(a.HotSeconds, Is.EqualTo(Math.PI / 4.0 * x * x).Within(1e-9));
            Assert.That(a.SmokeSeconds, Is.LessThan(a.HotSeconds), "it smokes before it glows");
            Assert.That(a.SmokeSeconds, Is.InRange(1.0, 30.0), "smoke within seconds of drilling");
            // The dust glows where the fire-dust tables put it: 5 mm layers of wood dust at 310 to 340 C (GESTIS-DUST-EX), 330 to 350
            // (Pastier et al. 2013); Duncan's 370 C at the top of his 340 to 430.
            Assert.That(a.EmberC, Is.InRange(310.0, 370.0));
        }

        [Test]
        public void TheChainMeetsTheTimesItWasMeasuredAndTimedAt()
        {
            // Duncan's bow drill (2021): elm spindle and hearth, a spindle about an inch across, air-dry; a coal at 23 to 24 s.
            FrictionAttempt duncan = FrictionFire.Judge(FrictionMethod.BowDrill, 600.0, 0.08, 0.0254, 600.0, "elm", 0.08, 1.0);
            Assert.That(duncan.Outcome, Is.EqualTo(FrictionOutcome.Coal), duncan.Words);
            Assert.That(duncan.CoalSeconds / 23.5, Is.InRange(1.0 / 1.5, 1.5), "within half again of his 23.5 s: " + duncan.CoalSeconds);
            // Hough (1890): "fire in thirty seconds by the twirling sticks and in five seconds with the bow drill"; the Samoan
            // plough "forty seconds".
            Assert.That(Drill(0.10).CoalSeconds, Is.InRange(15.0, 45.0));
            FrictionAttempt bowOnBanksia = FrictionFire.Judge(FrictionMethod.BowDrill, Stalk, 0.10, 0.011, Banksia, 0.10, 1.0);
            Assert.That(bowOnBanksia.CoalSeconds, Is.LessThan(15.0), "a thin spindle under a bow, in seconds");
            FrictionAttempt plough = FrictionFire.Judge(FrictionMethod.FirePlough, Banksia, 0.10, 0.012, Stalk, 0.10, 1.0);
            Assert.That(plough.CoalSeconds, Is.InRange(20.0, 60.0));
        }

        [Test]
        public void AGrassTreeDrillOnABanksiaHearthTakesACoalInUnderAMinuteWhenDry()
        {
            FrictionAttempt a = Drill(0.10);
            Assert.That(a.Outcome, Is.EqualTo(FrictionOutcome.Coal), a.Words);
            Assert.That(a.CoalSeconds, Is.InRange(15.0, 60.0), "the half-minute to a minute practice reports for a good hand drill");
            Assert.That(a.CoalKg, Is.EqualTo(FrictionFire.CoalKg));
            Assert.That(a.Words, Does.Contain("coal").And.Contain("smoked"));
            // The stalk on a hearth of its own kind heats faster still: a light pithy wood holds its heat at the surface.
            FrictionAttempt stalk = FrictionFire.Judge(FrictionMethod.HandDrill, Stalk, 0.10, 0.011, Stalk, 0.10, 1.0);
            Assert.That(stalk.CoalSeconds, Is.LessThan(a.CoalSeconds));
        }

        [Test]
        public void DampWoodTakesTheHeatAndSaysSo()
        {
            FrictionAttempt dry = Drill(0.10), wetter = Drill(0.15), damp = Drill(0.22);
            Assert.That(wetter.Outcome, Is.EqualTo(FrictionOutcome.Coal), "the driest wood the country's ground gives still makes one: " + wetter.Words);
            Assert.That(wetter.CoalSeconds, Is.GreaterThan(dry.CoalSeconds), "water lengthens it");
            Assert.That(damp.Outcome, Is.EqualTo(FrictionOutcome.TooWet), damp.Words);
            Assert.That(damp.Words, Does.Contain("22 % water").And.Contain("steams"), damp.Words);
            Assert.That(damp.Words, Does.Contain("air-dry it would have taken a coal"), "and says what dry wood would have done");
            // The limit is the dust's heat budget: a kilogram ground at 0.9 mg a joule is given 1.11 MJ, which must heat it to the
            // coal's 340 C (the Wood Handbook's heat) and boil its water.
            double limit = (1.0 / 0.9e-6 - Combustion.HeatToRaiseJPerKg(20.0, 340.0)) / (4186.0 * 80.0 + 2.2564e6);
            Assert.That(FrictionFire.MostDustMoisture, Is.EqualTo(limit).Within(1e-12));
            Assert.That(limit, Is.InRange(0.18, 0.22), "air-dry works, damp does not");
            Assert.That(Drill(0.30).Outcome, Is.EqualTo(FrictionOutcome.TooWet), "wood at the fibre saturation point only steams");
        }

        [Test]
        public void AHearthTooHardPolishesAndOneTooSoftCrumbles()
        {
            FrictionAttempt hard = FrictionFire.Judge(FrictionMethod.HandDrill, Stalk, 0.1, 0.011, Wood.Blackbutt, 0.1, 1.0);
            Assert.That(hard.Outcome, Is.EqualTo(FrictionOutcome.TooHard));
            Assert.That(hard.Words, Does.Contain("blackbutt").And.Contain("glaze"), hard.Words);
            Assert.That(hard.DustKgPerS, Is.EqualTo(0.0));
            FrictionAttempt punk = FrictionFire.Judge(FrictionMethod.HandDrill, Stalk.DensityDryKgM3, 0.1, 0.011, 150.0, "rotten log", 0.1, 1.0);
            Assert.That(punk.Outcome, Is.EqualTo(FrictionOutcome.TooSoft));
            Assert.That(punk.Words, Does.Contain("crumbles"), punk.Words);
        }

        [Test]
        public void AThickDrillOrATiredBodyGivesOutFirstAndSaysWhy()
        {
            FrictionAttempt thick = Drill(0.10, drillM: 0.02);
            Assert.That(thick.Outcome, Is.EqualTo(FrictionOutcome.Tired), thick.Words);
            Assert.That(thick.Words, Does.Contain("thick"), "a broad tip spreads the heat: the flux goes as the tip's area");
            Assert.That(thick.HearthFluxWm2, Is.EqualTo(Drill(0.10).HearthFluxWm2 * (0.011 * 0.011) / (0.02 * 0.02)).Within(1e-6));
            FrictionAttempt tired = Drill(0.10, capacity: 0.4);
            Assert.That(tired.Outcome, Is.EqualTo(FrictionOutcome.Tired), tired.Words);
            Assert.That(tired.Words, Does.Contain("arms gave out"));
            Assert.That(FrictionFire.Judge(FrictionMethod.HandDrill, Stalk, 0.1, 0.011, Banksia, 0.1, 0.0).Words, Does.Contain("no strength"));
        }

        [Test]
        public void TheFirePloughWantsASoftBoardAndABackIntoIt()
        {
            FrictionAttempt soft = FrictionFire.Judge(FrictionMethod.FirePlough, Banksia, 0.10, 0.012, Stalk, 0.10, 1.0);
            Assert.That(soft.Outcome, Is.EqualTo(FrictionOutcome.Coal), soft.Words);
            Assert.That(soft.CoalSeconds, Is.InRange(10.0, 90.0));
            Assert.That(soft.DustKgPerS, Is.GreaterThan(Drill(0.10).DustKgPerS), "more work, more dust");
            FrictionAttempt denser = FrictionFire.Judge(FrictionMethod.FirePlough, Banksia, 0.10, 0.012, Wood.SwampPaperbark, 0.10, 1.0);
            Assert.That(denser.CoalSeconds, Is.GreaterThan(soft.CoalSeconds), "a denser board is slower");
        }

        // ---- tinder by coal ----

        private static FuelPiece Bundle(double moisture) => FuelPiece.Bundle(Wood.SwampPaperbark, 0.010, 0.0003, 400.0, 0.10, moisture);

        [Test]
        public void ACoalGlowsAtTheCharsRate()
        {
            double coal = FrictionFire.CoalKg;
            double r = Math.Pow(3.0 * coal / 250.0 / (4.0 * Math.PI), 1.0 / 3.0);
            double area = 4.0 * Math.PI * r * r;
            Assert.That(Tinder.CoalSurfaceM2(coal), Is.EqualTo(area).Within(1e-12));
            Assert.That(Tinder.CoalHeatW(coal, 0.0), Is.EqualTo(1.5e-3 * area * 32.6e6).Within(1e-9));
            Assert.That(Tinder.CoalLifeSeconds(coal, 0.0), Is.InRange(60.0, 600.0), "a friction coal left alone glows for minutes");
            Assert.That(Tinder.CoalLifeSeconds(coal, Tinder.BlowMs), Is.LessThan(Tinder.CoalLifeSeconds(coal, 0.0)), "blown, it burns hotter and sooner out");
            Assert.That(Tinder.CoalHeatW(coal, Tinder.BlowMs), Is.GreaterThan(Tinder.CoalHeatW(coal, 0.0)));
        }

        [Test]
        public void ABlownCoalBringsDryTinderToFlameInSeconds()
        {
            TinderCatch c = Tinder.Catch(FrictionFire.CoalKg, Bundle(0.08), true, 0.0);
            Assert.That(c.Outcome, Is.EqualTo(TinderOutcome.Flame), c.Words);
            Assert.That(c.Seconds, Is.InRange(3.0, 40.0));
            Assert.That(c.Seconds, Is.LessThan(c.CoalLifeSeconds));
            Assert.That(Tinder.Catch(FrictionFire.CoalKg, Bundle(0.18), true, 0.0).Seconds, Is.GreaterThan(c.Seconds), "damper tinder takes longer");
        }

        [Test]
        public void DampTinderSmokesAndWillNotFlameAndSaysWhy()
        {
            TinderCatch c = Tinder.Catch(FrictionFire.CoalKg, Bundle(0.26), true, 0.0);
            Assert.That(c.Outcome, Is.EqualTo(TinderOutcome.TooDamp));
            Assert.That(c.Words, Does.Contain("26 % water").And.Contain("would not flame").And.Contain("24 %"), c.Words);
            // The threshold is Rothermel and Anderson's still-air extinction, not a number of this model's.
            Assert.That(Tinder.Catch(FrictionFire.CoalKg, Bundle(0.23), true, 0.0).Outcome, Is.EqualTo(TinderOutcome.Flame));
            Assert.That(Tinder.Catch(FrictionFire.CoalKg, Bundle(0.24), true, 0.0).Outcome, Is.EqualTo(TinderOutcome.TooDamp));
        }

        [Test]
        public void UnblownTheCoalOnlySmouldersIntoTheTinder()
        {
            TinderCatch c = Tinder.Catch(FrictionFire.CoalKg, Bundle(0.08), false, 0.0);
            Assert.That(c.Outcome, Is.EqualTo(TinderOutcome.Smoulders));
            Assert.That(c.Words, Does.Contain("blow"));
            Assert.That(Tinder.Catch(FrictionFire.CoalKg, Bundle(0.08), false, 2.0).Outcome, Is.EqualTo(TinderOutcome.Flame), "a breeze does the blowing");
        }

        [Test]
        public void ACoalTooSmallDiesAndACoalWillNotLightATwig()
        {
            TinderCatch tiny = Tinder.Catch(0.005e-3, Bundle(0.08), true, 0.0);
            Assert.That(tiny.Outcome, Is.EqualTo(TinderOutcome.CoalDied));
            Assert.That(tiny.Words, Does.Contain("burnt out"));
            TinderCatch twig = Tinder.Catch(FrictionFire.CoalKg, FuelPiece.Stick(Banksia, 0.004, 0.2, 0.10), true, 0.0);
            Assert.That(twig.Outcome, Is.EqualTo(TinderOutcome.CoalDied));
            Assert.That(twig.Words, Does.Contain("coarse"), twig.Words);
        }

        [Test]
        public void FromTheDrillToAFire()
        {
            // The whole chain: a coal from the drill, laid in tinder and blown, kindles a fire of the ladder's lay.
            FrictionAttempt drill = Drill(0.10);
            Assert.That(drill.Made, Is.True, drill.Words);
            FuelPiece bundle = Bundle(0.08);
            TinderCatch c = Tinder.Catch(drill.CoalKg, bundle, true, 0.0);
            Assert.That(c.Caught, Is.True, c.Words);
            Fire fire = new Fire();
            fire.Add(bundle);
            for (int i = 0; i < 20; i++) fire.Add(FuelPiece.Stick(Banksia, 0.004, 0.3, 0.12));
            for (int i = 0; i < 10; i++) fire.Add(FuelPiece.Stick(Banksia, 0.008, 0.4, 0.12));
            Assert.That(fire.Kindle(bundle, FireAir.Still), Is.True);
            fire.Advance(120.0, FireAir.Still);
            Assert.That(fire.Phase, Is.EqualTo(FirePhase.Flaming), fire.Describe());
        }
    }
}
