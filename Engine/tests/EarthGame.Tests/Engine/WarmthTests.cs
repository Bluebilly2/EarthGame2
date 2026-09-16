using System;
using EarthGame.Engine;
using NUnit.Framework;

namespace EarthGame.Tests.Engine
{
    /// <summary>
    /// The warmth of a founder's body (FP.2), held to the physiology v1's BodyState restated: each term against its own
    /// formula written again here with the published numbers, the words at their thresholds, shivering's three hours,
    /// and a night at the wake's own date and place lived through in the engine. The numbers are written here, not
    /// read from the class, so a slip in the class is a red test.
    /// </summary>
    public sealed class WarmthTests
    {
        private const double Skin = 1.8, Clo = 0.155, Sigma = 5.670374419e-8;

        private static Surroundings Chill(double airC = 9.0, double windMs = 2.0, double cloud = 0.2) =>
            new Surroundings(airC, windMs, cloud, 0.75, -20.0, Warmth.StandingSkyView01);

        [Test]
        public void TheStillAirLayerThinsWithTheRootOfTheWindAndNeverQuiteVanishes()
        {
            Warmth body = new Warmth();
            Assert.That(body.TotalInsulationClo(0.0), Is.EqualTo(0.7).Within(1e-12), "bare skin's still-air layer");
            Assert.That(body.TotalInsulationClo(4.0), Is.EqualTo(0.7 / (1.0 + 0.55 * 2.0)).Within(1e-12));
            Assert.That(body.TotalInsulationClo(100.0), Is.EqualTo(0.12).Within(1e-12), "a gale leaves the floor");
            body.ClothingClo = 1.0;
            Assert.That(body.TotalInsulationClo(5.0), Is.EqualTo(1.0 / (1.0 + 0.08 * 5.0) + 0.7 / (1.0 + 0.55 * Math.Sqrt(5.0))).Within(1e-12), "clothing loses value in wind too");
        }

        [Test]
        public void ANakedBodyAtNineDegreesInABreezeLosesWhatTheCloFiguresSay()
        {
            Warmth body = new Warmth();
            Surroundings s = Chill(9.0, 2.0);
            // Cold enough to constrict fully (a degree down): the tissue at 0.9 clo, the boundary thinned by the 2 m/s.
            double boundary = 0.7 / (1.0 + 0.55 * Math.Sqrt(2.0));
            double expected = Skin * (37.0 - 9.0) / ((0.9 + boundary) * Clo);
            Assert.That(body.SensibleLossAt(s, 1.0), Is.EqualTo(expected).Within(1e-9));
            Assert.That(expected, Is.InRange(240.0, 265.0), "about a quarter of a kilowatt, three times the body's resting heat");
            // Warm and unconstricted, the tissue insulates a third as much and the loss is greater.
            Assert.That(body.SensibleLossAt(s, -0.6), Is.EqualTo(Skin * 28.0 / ((0.3 + boundary) * Clo)).Within(1e-9));
            // Warm air constricts nothing, however far the core is down.
            Assert.That(body.SensibleLossAt(new Surroundings(30.0, 0.0, 0.0, 0.5, 40.0, 0.5), 1.0), Is.EqualTo(Skin * 7.0 / ((0.3 + 0.7) * Clo)).Within(1e-9));
        }

        [Test]
        public void TheClearNightSkyCostsAboutFortyWattsInTheOpenAndNothingUnderCloud()
        {
            Warmth body = new Warmth();
            Surroundings clear = new Surroundings(0.0, 0.0, 0.0, 0.7, -30.0, 0.5);
            double air = 273.15, sky = 273.15 - 16.0;
            double expected = 0.98 * Sigma * (Skin * 0.7 * 0.5) * (Math.Pow(air, 4) - Math.Pow(sky, 4));
            Assert.That(body.SkyExcessLossAt(clear), Is.EqualTo(expected).Within(1e-9));
            Assert.That(expected, Is.InRange(35.0, 45.0), "half the body's resting heat, which is why shelters have roofs");
            Assert.That(body.SkyExcessLossAt(new Surroundings(0.0, 0.0, 1.0, 0.7, -30.0, 0.5)), Is.EqualTo(0.0), "overcast closes the sky's cold");
            Assert.That(body.SkyExcessLossAt(new Surroundings(0.0, 0.0, 0.0, 0.7, -30.0, 0.0)), Is.EqualTo(0.0), "under a roof there is no sky");
            double halfCloud = body.SkyExcessLossAt(new Surroundings(0.0, 0.0, 0.5, 0.7, -30.0, 0.5));
            Assert.That(halfCloud, Is.LessThan(expected).And.GreaterThan(0.0), "half cloud, half the gap: less than clear, more than none");
        }

        [Test]
        public void TheWinterNoonSunWarmsABareBodyByTheMeinelBeamOnItsProjectedArea()
        {
            Warmth body = new Warmth();
            double elevation = 32.0, sinE = Math.Sin(elevation * Math.PI / 180.0);
            double direct = 1353.0 * Math.Pow(0.7, Math.Pow(1.0 / sinE, 0.678));
            Assert.That(Climate.DirectSolarWm2(elevation, 0.0), Is.EqualTo(direct).Within(1e-9));
            Assert.That(direct, Is.InRange(700.0, 800.0), "the measured 600-800 at a winter noon");
            // The shares under cloud and in a clear sky are the site's (M1.8c, calibrated to the Bureau's satellite exposure): a
            // covered sky leaves a quarter of the beam, a clear one scatters fifteen hundredths of the beam's fall.
            Assert.That(Climate.DirectSolarWm2(elevation, 1.0), Is.EqualTo(direct * 0.25).Within(1e-9), "cloud cuts the beam to a quarter");
            double diffuse = 0.15 * direct * sinE;
            Assert.That(Climate.DiffuseSolarWm2(elevation, 0.0), Is.EqualTo(diffuse).Within(1e-9));
            Assert.That(Climate.DiffuseSolarWm2(elevation, 1.0), Is.EqualTo(0.28 * 1361.0 * sinE).Within(1e-9), "overcast: bright, not warm");
            double projected = 0.25 - 0.17 * (elevation / 90.0);
            double expected = (direct * projected + diffuse * 0.7 * 0.5) * Skin * 0.55;
            Surroundings noon = new Surroundings(15.0, 1.0, 0.0, 0.6, elevation, 0.5);
            Assert.That(body.SolarGainAt(noon), Is.EqualTo(expected).Within(1e-9));
            Assert.That(expected, Is.InRange(140.0, 180.0), "about twice the body's resting heat, the term that gets a naked founder through a winter day");
            Assert.That(body.SolarGainAt(Chill()), Is.EqualTo(0.0), "no sun at night");
            Assert.That(Climate.DirectSolarWm2(0.4, 0.0), Is.EqualTo(0.0), "and none at the horizon");
        }

        [Test]
        public void BreathingIsFangersShareOfWhatTheBodyMakes()
        {
            Warmth body = new Warmth();
            Surroundings s = Chill(9.0, 2.0, 0.2);
            double saturation = 0.61094 * Math.Exp(17.625 * 9.0 / (9.0 + 243.04));
            double expected = 0.0014 * 80.0 * (34.0 - 9.0) + 0.0173 * 80.0 * (5.87 - 0.75 * saturation);
            Assert.That(body.RespiratoryAt(80.0, s), Is.EqualTo(expected).Within(1e-9));
            Assert.That(body.RespiratoryAt(400.0, s), Is.EqualTo(5.0 * expected).Within(1e-9), "five times the work, five times the breath");
        }

        [TestCase(37.0, ColdLevel.Well)]
        [TestCase(36.71, ColdLevel.Well)]
        [TestCase(36.69, ColdLevel.Chilly)]
        [TestCase(36.01, ColdLevel.Chilly)]
        [TestCase(35.99, ColdLevel.Cold)]
        [TestCase(35.01, ColdLevel.Cold)]
        [TestCase(34.99, ColdLevel.Hypothermic)]
        [TestCase(32.01, ColdLevel.Hypothermic)]
        [TestCase(31.99, ColdLevel.SeverelyHypothermic)]
        [TestCase(28.5, ColdLevel.SeverelyHypothermic)]
        public void TheWordsComeAtTheirThresholds(double coreC, ColdLevel expected)
        {
            Assert.That(Warmth.LevelOf(coreC), Is.EqualTo(expected));
            Warmth body = new Warmth();
            body.Restore(coreC);
            Assert.That(body.Cold, Is.EqualTo(expected));
            Assert.That(body.IsAlive, Is.True, "alive down to the lethal core");
        }

        [Test]
        public void TheWordsAreTheFoundersOwnAndDeathIsAtTwentyEight()
        {
            Assert.That(Warmth.WordFor(ColdLevel.Well), Is.Empty);
            Assert.That(Warmth.WordFor(ColdLevel.Chilly), Is.EqualTo("chilly"));
            Assert.That(Warmth.WordFor(ColdLevel.Cold), Is.EqualTo("cold"));
            Assert.That(Warmth.WordFor(ColdLevel.Hypothermic), Is.EqualTo("hypothermic"));
            Assert.That(Warmth.WordFor(ColdLevel.SeverelyHypothermic), Is.EqualTo("severely hypothermic"));
            Warmth body = new Warmth();
            body.Restore(28.0);
            Assert.That(body.IsAlive, Is.False);
            body.Restore(28.01);
            Assert.That(body.IsAlive, Is.True);
            body.Restore(double.NaN);
            Assert.That(body.CoreC, Is.EqualTo(37.0));
            body.Restore(50.0);
            Assert.That(body.CoreC, Is.EqualTo(41.0));
            body.Restore(10.0);
            Assert.That(body.CoreC, Is.EqualTo(20.0));
        }

        [TestCase(0.0, Exertion.Resting)]
        [TestCase(0.19, Exertion.Resting)]
        [TestCase(0.2, Exertion.Walking)]
        [TestCase(1.4, Exertion.Walking)]
        [TestCase(2.79, Exertion.Walking)]
        [TestCase(2.8, Exertion.Running)]
        [TestCase(6.0, Exertion.Running)]
        [TestCase(double.NaN, Exertion.Resting)]
        public void WhatTheFounderIsDoingComesFromTheirSpeed(double speed, Exertion expected)
        {
            Assert.That(Warmth.ExertionOf(speed), Is.EqualTo(expected));
        }

        [Test]
        public void TheWorkOfMovingIsPricedInWattsAndInWater()
        {
            Assert.That(Warmth.ActivityHeatW(Exertion.Resting), Is.EqualTo(0.0));
            Assert.That(Warmth.ActivityHeatW(Exertion.Walking), Is.EqualTo(180.0));
            Assert.That(Warmth.ActivityHeatW(Exertion.Running), Is.EqualTo(420.0));
            Assert.That(Warmth.ExertionFactor(Exertion.Resting), Is.EqualTo(1.0));
            Assert.That(Warmth.ExertionFactor(Exertion.Walking), Is.EqualTo(3.25).Within(1e-12), "(80 + 180) / 80");
            Assert.That(Warmth.ExertionFactor(Exertion.Running), Is.EqualTo(6.25).Within(1e-12));
            Hydration water = new Hydration();
            water.Advance(1.0, Warmth.ExertionFactor(Exertion.Walking), 0.5);
            Assert.That(water.LossLPerDay, Is.EqualTo(2.4 * 3.25 + 12.0).Within(1e-12), "the resting loss times the factor, and the sweat by the day");
            Assert.That(water.Water01, Is.EqualTo(1.0 - (2.4 * 3.25 + 12.0) / 42.0).Within(1e-12));
            Hydration rest = new Hydration();
            rest.Advance(1.0);
            Assert.That(rest.LossLPerDay, Is.EqualTo(2.4), "at rest in the shade, the resting loss alone");
        }

        [Test]
        public void ShiveringComesAsTheCoreFallsAndItsThreeHoursRunOut()
        {
            Warmth body = new Warmth();
            Surroundings s = Chill(9.0, 2.0);
            body.Tick(1.0, s, Exertion.Resting, 1.0, 1.0);
            Assert.That(body.Shivering01, Is.EqualTo(0.0), "a normal core shivers not at all");
            Assert.That(body.NetHeatW, Is.LessThan(0.0), "and cools");
            body.Restore(36.0);
            body.Tick(1.0, s, Exertion.Resting, 1.0, 1.0);
            double demand = (1.0 - 0.15) / 1.2;
            Assert.That(body.Shivering01, Is.EqualTo(demand).Within(1e-6), "a degree down, seven tenths of the most");
            Assert.That(body.ProductionW, Is.EqualTo(80.0 + demand * 350.0).Within(0.5));
            // Held a degree down for three hours, the reserve decays with the shivering delivered, exponentially: what is left
            // is exp(-demand), since the integral of delivered shivering over the reserve's whole life is the three hours.
            for (int i = 0; i < 3 * 60; i++)
            {
                body.Restore(36.0);
                body.Tick(60.0, s, Exertion.Resting, 1.0, 1.0);
            }
            Assert.That(body.ShiverStamina01, Is.EqualTo(Math.Exp(-demand)).Within(0.01), "the reserve spent in proportion to the shivering delivered");
            // Thirst takes the muscles' work away: half the capacity, half the shivering's heat.
            Warmth thirsty = new Warmth();
            thirsty.Restore(36.0);
            thirsty.Tick(1.0, s, Exertion.Resting, 0.5, 1.0);
            Assert.That(thirsty.ProductionW, Is.EqualTo(80.0 + demand * 350.0 * 0.5).Within(0.5));
        }

        [Test]
        public void ASurplusIsSweatedOffAndChargedInLitres()
        {
            Warmth body = new Warmth();
            Surroundings hot = new Surroundings(38.0, 0.5, 0.0, 0.5, 60.0, 0.5);
            for (int i = 0; i < 30; i++) body.Tick(60.0, hot, Exertion.Running, 1.0, 1.0);
            Assert.That(body.CoreC, Is.LessThan(37.6), "sweat holds the core near its setpoint");
            Assert.That(body.SweatRateLPerHour, Is.GreaterThan(0.2).And.LessThanOrEqualTo(1.5), "at a real rate, never past the most a body can");
            Warmth parched = new Warmth();
            for (int i = 0; i < 30; i++) parched.Tick(60.0, hot, Exertion.Running, 1.0, 0.0);
            Assert.That(parched.SweatRateLPerHour, Is.EqualTo(0.0), "a body with no water to spare cannot sweat");
            Assert.That(parched.CoreC, Is.GreaterThan(body.CoreC), "and cooks");
        }

        /// <summary>
        /// The world's own weather at the wake's date and place (M1.8a), from eight in the evening, a naked body at the shore's
        /// height in the open. The path's canon death at about 03:09 was v1's, on an inland beach 687 m up with the founder lying
        /// on the ground. Under M1.8a's weather (v1's light night wind) an ordinary late-winter night left a founder who stood
        /// still hypothermic in the small hours and alive at dawn, lowest core 28.7 °C. Under the lighthouse's own record (M1.8c,
        /// 2026-09-16) the night is a degree milder but its wind is the coast's, about 2.5 m/s at a standing body in the open all
        /// night where v1 had 1.5, and that decides it: the same founder is hypothermic after midnight and dies at about dawn,
        /// the reserve spent before the sun can help; one who keeps walking is never hypothermic at all. The test holds what
        /// the physiology says of this coast, not the canon's hour, and prints the night so a change in the weather is seen.
        /// </summary>
        [Test]
        public void AnOrdinaryNightAtTheWakeKillsAStandingFounderAboutDawnAndAWalkingOneIsNeverCold()
        {
            Region region = Region.Bherwerre;
            Climate climate = Climate.ForRegion(region);
            Synoptic synoptic = new Synoptic(1347);
            Night standing = LiveTheNight(region, climate, synoptic, Exertion.Resting);
            Night walking = LiveTheNight(region, climate, synoptic, Exertion.Walking);
            string story = "standing: " + standing + "; walking: " + walking;
            TestContext.Out.WriteLine(TestContext.CurrentContext.Test.Name + ": " + story);
            Assert.That(standing.HypothermicAtH, Is.GreaterThan(2.0).And.LessThan(9.0), "hypothermic within the night, hours after dark; " + story);
            Assert.That(standing.DeathAtH, Is.GreaterThan(8.0).And.LessThan(12.5), "dead about dawn, between four and eight in the morning; " + story);
            Assert.That(standing.HypothermicAtH, Is.LessThan(standing.DeathAtH - 3.0), "hours of hypothermia before the death; " + story);
            Assert.That(double.IsNaN(walking.HypothermicAtH), Is.True, "walking's 180 W keeps a founder out of hypothermia through this night; " + story);
            Assert.That(walking.DeathAtH, Is.EqualTo(Night.Lived), "and alive at ten in the morning; " + story);
            Assert.That(walking.LowestCoreC, Is.GreaterThan(Warmth.ColdC), "never even cold; " + story);
        }

        /// <summary>
        /// A front's night: the air at 4 °C behind a cold front with the wind at 7 m/s under a clear sky, real weather for this
        /// coast in winter (M1.8a's cold snaps). It kills a founder standing still in the small hours, and walking buys hours
        /// without saving them: the night's only answer is a fire, which is a later beat.
        /// </summary>
        [Test]
        public void AFrontsNightKillsAStandingFounderInTheSmallHoursAndWalkingBuysHours()
        {
            Surroundings front = new Surroundings(4.0, 7.0, 0.0, 0.7, -25.0, Warmth.StandingSkyView01);
            Night standing = LiveANight(front, Exertion.Resting);
            Night walking = LiveANight(front, Exertion.Walking);
            string story = "standing: " + standing + "; walking: " + walking;
            TestContext.Out.WriteLine(TestContext.CurrentContext.Test.Name + ": " + story);
            Assert.That(standing.DeathAtH, Is.GreaterThan(2.0).And.LessThan(9.0), "dead in the small hours; " + story);
            Assert.That(standing.HypothermicAtH, Is.LessThan(standing.DeathAtH), story);
            Assert.That(walking.DeathAtH, Is.GreaterThan(standing.DeathAtH + 1.5), "walking's 180 W buys hours; " + story);
        }

        private readonly struct Night
        {
            public const double Lived = 14.0;
            public readonly double HypothermicAtH, DeathAtH, LowestCoreC;
            public Night(double hypothermicAtH, double deathAtH, double lowestCoreC)
            {
                HypothermicAtH = hypothermicAtH;
                DeathAtH = deathAtH;
                LowestCoreC = lowestCoreC;
            }
            public override string ToString() => "hypothermic " + HypothermicAtH.ToString("0.0") + " h after eight, dead at " + DeathAtH.ToString("0.0")
                                                 + " h (14 is lived to ten in the morning), lowest core " + LowestCoreC.ToString("0.0") + " °C";
        }

        /// <summary>Fourteen hours from eight in the evening under the world's weather at the wake, or until the core is lethal.</summary>
        private static Night LiveTheNight(Region region, Climate climate, Synoptic synoptic, Exertion exertion)
        {
            WorldClock clock = WorldClock.FromLocal(region.WakeDayOfYear, 20.0, region.CentreLongitudeDeg);
            return Live(exertion, hours =>
            {
                SolarClock sun = SolarClock.ForRegion(region, clock);
                Weather weather = Weather.At(climate, synoptic, sun, 2.0, 1.0);
                clock.Advance(30.0 * WorldClock.RealSecondsPerDay / 86400.0);
                return Surroundings.Of(weather, sun.SolarElevationDeg);
            });
        }

        /// <summary>Fourteen hours in one unchanging night, or until the core is lethal.</summary>
        private static Night LiveANight(Surroundings night, Exertion exertion) => Live(exertion, hours => night);

        private static Night Live(Exertion exertion, Func<double, Surroundings> surroundingsAt)
        {
            Warmth body = new Warmth();
            Hydration water = new Hydration();
            double hypothermicAt = double.NaN, lowest = body.CoreC;
            const double step = 30.0;
            for (double hours = 0.0; hours < Night.Lived; hours += step / 3600.0)
            {
                body.Tick(step, surroundingsAt(hours), exertion, water.WorkCapacity01, 1.0 - water.Loss / Hydration.LethalWaterLoss);
                water.Advance(step / 86400.0, Warmth.ExertionFactor(exertion), body.SweatRateLPerHour);
                lowest = Math.Min(lowest, body.CoreC);
                if (double.IsNaN(hypothermicAt) && body.Cold >= ColdLevel.Hypothermic) hypothermicAt = hours;
                if (!body.IsAlive) return new Night(hypothermicAt, hours, lowest);
            }
            return new Night(hypothermicAt, Night.Lived, lowest);
        }
    }
}
