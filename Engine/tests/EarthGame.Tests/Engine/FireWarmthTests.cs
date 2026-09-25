using System;
using EarthGame.Engine;
using NUnit.Framework;

namespace EarthGame.Tests.Engine
{
    /// <summary>
    /// The fire's warmth on a body and the night's arithmetic (BF.5 part one, promises 8 and 9): the point source, the radiant
    /// shares, Fanger's projected areas against their published fit and against the body's own sun, the warming fire against
    /// the night's deficit the body's balance works out, the heat that is too much, and the kilograms an hour and a night.
    /// </summary>
    public sealed class FireWarmthTests
    {
        [Test]
        public void ThePointSourceSpreadsTheRadiantShareOverASphere()
        {
            Assert.That(FireWarmth.RadiantW(10000.0, 4000.0), Is.EqualTo(0.30 * 10000.0 + 0.5 * 4000.0).Within(1e-9));
            Assert.That(FireWarmth.FluxAtWm2(3000.0, 1.5), Is.EqualTo(3000.0 / (4.0 * Math.PI * 2.25)).Within(1e-9));
            // The measured shares of wood's flames: 0.21 (McCarter and Broido) to a third (Sunahara).
            Assert.That(FireWarmth.FlameRadiantShare, Is.InRange(0.21, 1.0 / 3.0));
            Assert.That(FireWarmth.GlowRadiantShare, Is.GreaterThan(FireWarmth.FlameRadiantShare), "char radiates more of its heat than flame (NIST TN 2314)");
            Assert.That(FireWarmth.PointSourceFromWidths, Is.EqualTo(2.5));
        }

        [Test]
        public void FangersTablesAreReadAsASHRAEPrintsThem()
        {
            Assert.That(FireWarmth.ProjectedAreaFactor(Posture.Standing, 0.0, 0.0), Is.EqualTo(0.350).Within(1e-12));
            Assert.That(FireWarmth.ProjectedAreaFactor(Posture.Standing, 90.0, 40.0), Is.EqualTo(0.082).Within(1e-12), "from overhead a body is its shoulders");
            Assert.That(FireWarmth.ProjectedAreaFactor(Posture.Standing, 0.0, 90.0), Is.EqualTo(0.230).Within(1e-12), "side-on");
            Assert.That(FireWarmth.ProjectedAreaFactor(Posture.Sitting, 15.0, 0.0), Is.EqualTo(0.324).Within(1e-12));
            Assert.That(FireWarmth.ProjectedAreaFactor(Posture.Sitting, 90.0, 0.0), Is.EqualTo(0.177).Within(1e-12), "a seated lap shows more from above");
            Assert.That(FireWarmth.ProjectedAreaFactor(Posture.Standing, 37.5, 0.0), Is.EqualTo((0.314 + 0.258) / 2.0).Within(1e-12), "between the table's steps");
            Assert.That(FireWarmth.ProjectedAreaFactor(Posture.Standing, 0.0, 7.5), Is.EqualTo((0.350 + 0.342) / 2.0).Within(1e-12));
            Assert.That(FireWarmth.ProjectedAreaFactor(Posture.Standing, -30.0, 0.0), Is.EqualTo(FireWarmth.ProjectedAreaFactor(Posture.Standing, 30.0, 0.0)), "below read as above");
            Assert.That(FireWarmth.ProjectedAreaFactor(Posture.Sitting, 20.0, 190.0), Is.EqualTo(FireWarmth.ProjectedAreaFactor(Posture.Sitting, 20.0, 170.0)).Within(1e-12), "either side alike");
            Assert.That(FireWarmth.EffectiveAreaFactor(Posture.Standing), Is.EqualTo(0.725));
            Assert.That(FireWarmth.EffectiveAreaFactor(Posture.Sitting), Is.EqualTo(0.696));
            Assert.That(FireWarmth.ProjectedAreaM2(Posture.Standing, 0.0, 0.0), Is.EqualTo(0.350 * 0.725 * Warmth.SkinAreaM2).Within(1e-12));
        }

        [Test]
        public void TheStandingTableAgreesWithItsPublishedFit()
        {
            // The second source: f_p = 0.308 cos(g (0.998 - g^2/50000)) for a rotationally symmetric standing body, g the
            // source's altitude in degrees (Jendritzky et al. 1990 and VDI 3787, as Di Napoli, Hogan and Pappenberger 2020 give it).
            foreach (double g in new[] { 0.0, 15.0, 30.0, 45.0, 60.0, 75.0, 90.0 })
            {
                double fit = 0.308 * Math.Cos(g * (0.998 - g * g / 50000.0) * Math.PI / 180.0);
                double around = 0.0;
                for (int b = 0; b < 360; b += 5) around += FireWarmth.ProjectedAreaFactor(Posture.Standing, g, b);
                around /= 72.0;
                Assert.That(around, Is.EqualTo(fit).Within(0.015), "at " + g + " degrees: the table round the body " + around + ", the fit " + fit);
            }
        }

        [Test]
        public void TheFiresBodyAndTheSunsBodyAreTheSameBodyToAThird()
        {
            // Warmth works out the sun's projected share of its own (0.25 of the skin to a sun on the horizon, falling to a
            // twelfth overhead). Two owners of one fact, held together here until main gives it one (the contract, For main to
            // decide): the share is read back from Warmth.SolarGainAt through its public terms, not copied.
            Warmth body = new Warmth();
            foreach (double elevation in new[] { 5.0, 30.0, 60.0, 85.0 })
            {
                Surroundings s = new Surroundings(15.0, 1.0, 0.0, 0.6, elevation, Warmth.StandingSkyView01);
                double direct = Climate.DirectSolarWm2(elevation, 0.0), diffuse = Climate.DiffuseSolarWm2(elevation, 0.0);
                double sunShare = (body.SolarGainAt(s) / (Warmth.SkinAreaM2 * Warmth.SolarAbsorptance) - diffuse * Warmth.RadiatingAreaFraction * 0.5) / direct;
                double around = 0.0;
                for (int b = 0; b < 360; b += 5) around += FireWarmth.ProjectedAreaFactor(Posture.Standing, elevation, b);
                double fireShare = around / 72.0 * FireWarmth.EffectiveAreaFactor(Posture.Standing);
                Assert.That(fireShare / sunShare, Is.InRange(0.66, 1.5), "at " + elevation + " degrees: the fire's body " + fireShare + ", the sun's " + sunShare);
            }
        }

        /// <summary>A naked founder's shortfall at rest on a clear night, W, by the body's own terms: what the fire must make up.</summary>
        private static double NightDeficitW(double airC)
        {
            Warmth body = new Warmth();
            Surroundings s = new Surroundings(airC, 1.5, 0.2, 0.8, -20.0, Warmth.StandingSkyView01);
            return body.SensibleLossAt(s, 1.0) + body.SkyExcessLossAt(s) + body.RespiratoryAt(Warmth.BasalHeatW, s) - Warmth.BasalHeatW;
        }

        [Test]
        public void AWarmingFireMakesUpTheNightForAFounderSittingCloseAndNotForOneStandingBack()
        {
            double deficit = NightDeficitW(6.0);
            Assert.That(deficit, Is.InRange(150.0, 350.0), "a naked body at rest on a clear night at 6 C runs a couple of hundred watts short");
            double warming = FireFuel.HeatReleaseW(FireSize.Warming, Wood.Blackbutt, 0.15);
            FireOnBody close = FireWarmth.On(warming, 0.0, 0.5, 0.7, Posture.Sitting, 0.0);
            Assert.That(close.AbsorbedW, Is.GreaterThan(0.75 * deficit), "sitting 0.7 m from it, most of the night's shortfall: " + close.Words);
            Assert.That(close.TooHot, Is.False, "and bearable: " + close.FluxWm2 + " W/m2");
            Assert.That(close.Words, Does.Contain("warms you"));
            FireOnBody back = FireWarmth.On(warming, 0.0, 0.5, 2.0, Posture.Standing, 0.0);
            Assert.That(back.AbsorbedW, Is.LessThan(0.2 * deficit), "standing two metres back, little of it");
            Assert.That(FireWarmth.On(warming, 0.0, 0.5, 1.0, Posture.Standing, 0.0).AbsorbedW,
                        Is.LessThan(FireWarmth.On(warming, 0.0, 0.5, 1.0, Posture.Sitting, 0.0).AbsorbedW), "a fire on the ground warms a seated body more");
            Assert.That(close.Near, Is.True, "a metre from a campfire is inside the point source's range, and the result says so");
        }

        [Test]
        public void TooCloseIsTooHotAndYourBackIsNotWarmedLikeYourFront()
        {
            FireOnBody burning = FireWarmth.On(15000.0, 0.0, 0.5, 0.15, Posture.Sitting, 0.0);
            Assert.That(burning.TooHot, Is.True);
            Assert.That(burning.Words, Does.Contain("too hot"), burning.Words);
            FireOnBody front = FireWarmth.On(15000.0, 0.0, 0.5, 0.8, Posture.Sitting, 0.0);
            FireOnBody back = FireWarmth.On(15000.0, 0.0, 0.5, 0.8, Posture.Sitting, 180.0);
            Assert.That(back.AbsorbedW, Is.LessThan(front.AbsorbedW), "a seated body shows the fire less of its back than its front and lap");
            Assert.That(back.Words, Does.Contain("back"));
            Assert.That(FireWarmth.On(0.0, 0.0, 0.5, 1.0, Posture.Sitting, 0.0).Words, Does.Contain("no warmth"));
        }

        [Test]
        public void TheFireAsItBurnsWarmsAndItsEmbersGoOnWarming()
        {
            Fire fire = new Fire();
            FuelPiece tinder = FuelPiece.Bundle(Wood.SwampPaperbark, 0.010, 0.0003, 400.0, 0.10, 0.08);
            fire.Add(tinder);
            for (int i = 0; i < 20; i++) fire.Add(FuelPiece.Stick(Wood.CoastBanksia, 0.004, 0.3, 0.12));
            for (int i = 0; i < 10; i++) fire.Add(FuelPiece.Stick(Wood.CoastBanksia, 0.008, 0.4, 0.12));
            for (int i = 0; i < 6; i++) fire.Add(FuelPiece.Stick(Wood.CoastBanksia, 0.015, 0.4, 0.12));
            for (int i = 0; i < 3; i++) fire.Add(FuelPiece.Stick(Wood.Blackbutt, 0.05, 0.5, 0.15));
            fire.Kindle(tinder, FireAir.Still);
            fire.Advance(900.0, FireAir.Still);
            FireOnBody flaming = FireWarmth.On(fire, 0.5, 0.8, Posture.Sitting, 0.0);
            Assert.That(flaming.AbsorbedW, Is.InRange(40.0, 600.0), flaming.Words);
            for (int i = 0; i < 24 * 60 && fire.Phase != FirePhase.Embers && fire.Phase != FirePhase.Out; i++) fire.Advance(60.0, FireAir.Still);
            Assert.That(fire.Phase, Is.EqualTo(FirePhase.Embers));
            FireOnBody embers = FireWarmth.On(fire, 0.5, 0.8, Posture.Sitting, 0.0);
            Assert.That(embers.AbsorbedW, Is.GreaterThan(0.0), "a bed of coals still warms: " + embers.Words);
            Assert.That(FireWarmth.HeartM(0.0, 0.5), Is.EqualTo(0.1).Within(1e-12), "a bed of embers radiates from the bed");
        }

        // ---- the night's arithmetic ----

        [Test]
        public void TheSizesAreThePublishedBurnRates()
        {
            Assert.That(FireFuel.DryKgPerHour(FireSize.Small), Is.EqualTo(9.49 * 0.06).Within(1e-12), "the three-stone fire at simmer, 9.49 g a minute");
            Assert.That(FireFuel.DryKgPerHour(FireSize.Cooking), Is.InRange(24.08 * 0.06, 25.61 * 0.06), "at high power, 24.08 to 25.61 g a minute");
            Assert.That(FireFuel.DryKgPerHour(FireSize.Warming), Is.EqualTo(3.0), "a fireplace's typical 3 kg an hour");
        }

        [Test]
        public void AKilogramGivesTheFireWhatTheFireReleases()
        {
            double gas = Combustion.GasHeatMJPerKg(Wood.Blackbutt);
            Assert.That(FireFuel.EffectiveHeatMJPerKgDry(Wood.Blackbutt, 0.15), Is.EqualTo(0.8 * gas * 0.95 + 0.2 * 32.6 - 2.443 * 0.15).Within(1e-12));
            // The cooking fire's heat, in this model's accounting, against the Water Boiling Test's firepower (burn rate times the
            // fir's 19.26 MJ/kg): below it, by the smoke an open fire makes and the water it carries, and of its order.
            double cooking = FireFuel.HeatReleaseW(FireSize.Cooking, Wood.Blackbutt, 0.11);
            Assert.That(cooking / 8000.0, Is.InRange(0.75, 1.0), "the three-stone fire's 7.8 to 8.2 kW firepower");
        }

        [Test]
        public void TheNightsPileIsTensOfKilograms()
        {
            // A winter night on this coast is about thirteen hours from dusk to dawn.
            double pile = FireFuel.KgFor(FireSize.Warming, 13.0, 0.15);
            Assert.That(pile, Is.EqualTo(3.0 * 1.15 * 13.0).Within(1e-9));
            Assert.That(pile, Is.InRange(35.0, 55.0), "forty-odd kilograms: an evening's gathering");
            Assert.That(FireFuel.KgFor(FireSize.Small, 13.0, 0.15), Is.LessThan(FireFuel.KgFor(FireSize.Cooking, 13.0, 0.15)));
            // Wet wood asks for more of itself for the same heat: its water weighs and its water costs.
            Assert.That(FireFuel.KgPerHour(10000.0, Wood.Blackbutt, 0.6), Is.GreaterThan(1.4 * FireFuel.KgPerHour(10000.0, Wood.Blackbutt, 0.12)));
        }
    }
}
