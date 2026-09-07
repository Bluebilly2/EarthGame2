using EarthGame.Engine;
using NUnit.Framework;

namespace EarthGame.Tests.Engine
{
    /// <summary>
    /// Two published functions, used unchanged: Tobler (1993) for how fast a person walks on a slope, and Pandolf,
    /// Givoni and Goldman (1977) for what it costs them to do it carrying something. What these tests are really
    /// checking is that neither has been quietly tuned into something that feels nice. Ported from v1.
    /// </summary>
    public sealed class LocomotionTests
    {
        private const double BodyKg = 70.0;

        [Test]
        public void L1_ToblerUnchanged()
        {
            Assert.That(Locomotion.WalkingSpeedMs(0.0), Is.EqualTo(1.40).Within(0.02), "a person walks at about 1.4 m/s on the flat, and that is the whole point");
            // The peak is on a gentle descent, not on the flat: the surprising part of the function and the reason
            // real traverses zigzag rather than contouring dead level.
            double flat = Locomotion.WalkingSpeedMs(0.0);
            double gentleDown = Locomotion.WalkingSpeedMs(Locomotion.ToblerPeakSlope);
            Assert.That(gentleDown, Is.GreaterThan(flat), "the fastest walking is slightly downhill");
            for (double s = -0.6; s <= 0.6; s += 0.02)
                Assert.That(Locomotion.WalkingSpeedMs(s), Is.LessThanOrEqualTo(gentleDown + 1e-9), "nothing beats the peak, and " + s.ToString("F2") + " did");
        }

        [Test]
        public void L2_UphillIsACrawl()
        {
            double flat = Locomotion.WalkingSpeedMs(0.0);
            double steep = Locomotion.WalkingSpeedMs(0.5); // 27 degrees
            Assert.That(steep, Is.LessThan(flat * 0.25), "a 27-degree slope is under a quarter of flat walking");
        }

        [Test]
        public void L3_SteepDescentIsNotFree()
        {
            Assert.That(Locomotion.WalkingSpeedMs(-0.40), Is.LessThan(Locomotion.WalkingSpeedMs(0.0)), "coming down something steep is slower than the flat, not faster");
            Assert.That(Locomotion.MetabolicCostW(BodyKg, 0, 0.5, -0.40), Is.GreaterThan(Locomotion.MetabolicCostW(BodyKg, 0, 0.5, -0.05)), "and it costs more than an easy descent, which is what braking is");
        }

        [Test]
        public void L4_PandolfAgainstThePublishedPoint()
        {
            // 70 kg, unloaded, 1.34 m/s (3 mph) on the flat is the standard reference case, around 330 W.
            double watts = Locomotion.MetabolicCostW(BodyKg, 0.0, 1.34, 0.0);
            Assert.That(watts, Is.GreaterThan(290.0).And.LessThan(380.0), "a flat unloaded walk is about 330 W, got " + watts.ToString("F0"));
        }

        [Test]
        public void L5_CarryingCosts()
        {
            double empty = Locomotion.MetabolicCostW(BodyKg, 0.0, 1.34, 0.0);
            double laden = Locomotion.MetabolicCostW(BodyKg, 25.0, 1.34, 0.0);
            Assert.That(laden, Is.GreaterThan(empty * 1.3), "twenty-five kilos is at least a third more work");
            Assert.That(Locomotion.MetabolicCostW(BodyKg, 40.0, 1.34, 0.0), Is.GreaterThan(laden), "and forty is more again");
        }

        [Test]
        public void L6_TheHillCostsMoreThanTheLoad()
        {
            double loadedFlat = Locomotion.MetabolicCostW(BodyKg, 25.0, 1.0, 0.0);
            double emptyHill = Locomotion.MetabolicCostW(BodyKg, 0.0, 1.0, 0.15);
            Assert.That(emptyHill, Is.GreaterThan(loadedFlat), "a 15% grade empty-handed is harder than 25 kg on the flat");
        }

        [Test]
        public void TheGroundUnderfootCosts()
        {
            double made = Locomotion.MetabolicCostW(BodyKg, 0, 1.34, 0.0, GroundType.Made);
            double brush = Locomotion.MetabolicCostW(BodyKg, 0, 1.34, 0.0, GroundType.HeavyBrush);
            double sand = Locomotion.MetabolicCostW(BodyKg, 0, 1.34, 0.0, GroundType.Loose);
            Assert.That(made, Is.LessThan(brush), "bracken to the waist is work");
            Assert.That(brush, Is.LessThan(sand), "and sand gives some of every step back");
        }

        [Test]
        public void L7_TheBodyCapsTheGait()
        {
            Assert.That(Locomotion.FastestGait(1.0), Is.EqualTo(Gait.Running));
            Assert.That(Locomotion.FastestGait(0.4), Is.EqualTo(Gait.Walking), "a founder at forty per cent is not running anywhere");
            Assert.That(Locomotion.SpeedMs(0.0, Gait.Walking, 0.4), Is.LessThan(Locomotion.SpeedMs(0.0, Gait.Walking, 1.0)), "and they walk slower than a fresh one");
        }

        [Test]
        public void L8_CrossingTheCatchmentIsAWalk()
        {
            // 2.4 km of rolling country is a serious part of a day, not a sprint: v1's first controller crossed it in
            // seven minutes and made the world feel like a room.
            double minutes = Locomotion.HoursToCover(2400.0, 0.08, Gait.Walking, 1.0) * 60.0;
            Assert.That(minutes, Is.GreaterThan(25.0), "crossing the modelled catchment takes real time: " + minutes.ToString("F0") + " min");
        }

        [Test]
        public void L9_NothingIsFree()
        {
            double basal = 1.5 * BodyKg;
            foreach (Gait gait in new[] { Gait.Walking, Gait.Jogging, Gait.Running })
            {
                for (double slope = -0.5; slope <= 0.5; slope += 0.1)
                {
                    double speed = Locomotion.SpeedMs(slope, gait, 1.0);
                    double cost = Locomotion.MetabolicCostW(BodyKg, 0.0, speed, slope);
                    Assert.That(cost, Is.GreaterThanOrEqualTo(basal), gait + " on " + slope.ToString("F1") + " must cost at least standing");
                    Assert.That(cost, Is.LessThan(3000.0), gait + " on " + slope.ToString("F1") + " came out at " + cost.ToString("F0") + " W, which no human produces");
                }
            }
        }

        [Test]
        public void RunningIsFasterThanWalkingEverywhere()
        {
            for (double slope = -0.5; slope <= 0.5; slope += 0.05)
                Assert.That(Locomotion.SpeedMs(slope, Gait.Running, 1.0), Is.GreaterThan(Locomotion.SpeedMs(slope, Gait.Walking, 1.0)), "at slope " + slope.ToString("F2"));
        }

        [Test]
        public void TheGaitMultipliersAreTheCodesUntilTheOwnerRules()
        {
            // The v1 audit found the code at 1.7 and 2.6 against a contract that said 2.1 and 3.6. Pinned as ported
            // so that the change, when the owner makes it hands-on, is a visible edit to a test, not a drift
            // (DEBTS.md, 2026-09-08).
            Assert.That(Locomotion.GaitMultiplier(Gait.Walking), Is.EqualTo(1.0));
            Assert.That(Locomotion.GaitMultiplier(Gait.Jogging), Is.EqualTo(1.7));
            Assert.That(Locomotion.GaitMultiplier(Gait.Running), Is.EqualTo(2.6));
            Assert.That(Locomotion.GaitMultiplier(Gait.Standing), Is.EqualTo(0.0));
        }
    }
}
