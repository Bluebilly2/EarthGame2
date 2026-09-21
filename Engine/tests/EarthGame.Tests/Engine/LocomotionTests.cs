using System;
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
            Assert.That(Locomotion.JourneySpeedMs(0.0), Is.EqualTo(1.40).Within(0.02), "a person walks at about 1.4 m/s on the flat, and that is the whole point");
            // The peak is on a gentle descent, not on the flat: the surprising part of the function and the reason
            // real traverses zigzag rather than contouring dead level.
            double flat = Locomotion.JourneySpeedMs(0.0);
            double gentleDown = Locomotion.JourneySpeedMs(Locomotion.ToblerPeakSlope);
            Assert.That(gentleDown, Is.GreaterThan(flat), "the fastest walking is slightly downhill");
            for (double s = -0.6; s <= 0.6; s += 0.02)
                Assert.That(Locomotion.JourneySpeedMs(s), Is.LessThanOrEqualTo(gentleDown + 1e-9), "nothing beats the peak, and " + s.ToString("F2") + " did");
        }

        [Test]
        public void L2_UphillIsACrawl()
        {
            double flat = Locomotion.JourneySpeedMs(0.0);
            double steep = Locomotion.JourneySpeedMs(0.5); // 27 degrees
            Assert.That(steep, Is.LessThan(flat * 0.25), "a 27-degree slope is under a quarter of flat walking");
        }

        [Test]
        public void L3_SteepDescentIsNotFree()
        {
            Assert.That(Locomotion.JourneySpeedMs(-0.40), Is.LessThan(Locomotion.JourneySpeedMs(0.0)), "coming down something steep is slower than the flat, not faster");
            Assert.That(Locomotion.MetabolicCostW(BodyKg, 0, 0.5, -0.40), Is.GreaterThan(Locomotion.MetabolicCostW(BodyKg, 0, 0.5, -0.05)), "and it costs more than an easy descent, which is what braking is");
        }

        /// <summary>
        /// M1.5h (CANON ruling 34): the walker's own speed against the slope along the motion, from what people are measured
        /// doing on slopes and not from a journey's hour-average. The anchor points are the hiking speeds a treadmill study
        /// set for its grades (Applied Sciences 14 (2024) 4383: 5.0 km/h on the level and at -10 %, 3.5 at +10 % and at
        /// -20 %, 2.5 at +20 %); a gentle descent is walked no slower than the flat (Sun et al. 1996, 2 400 pedestrians).
        /// </summary>
        [Test]
        public void W1_TheWalkersTableHitsTheSourcesPoints()
        {
            Assert.That(Locomotion.WalkingSpeedMs(0.0), Is.EqualTo(1.39).Within(0.005), "5.0 km/h on the level");
            Assert.That(Locomotion.WalkingSpeedMs(-0.10), Is.EqualTo(1.39).Within(0.005), "a gentle descent is walked no slower than the flat");
            Assert.That(Locomotion.WalkingSpeedMs(-0.20), Is.EqualTo(0.97).Within(0.005), "3.5 km/h down a fifth");
            Assert.That(Locomotion.WalkingSpeedMs(0.10), Is.EqualTo(0.97).Within(0.005), "3.5 km/h up a tenth");
            Assert.That(Locomotion.WalkingSpeedMs(0.20), Is.EqualTo(0.69).Within(0.005), "2.5 km/h up a fifth");
        }

        /// <summary>The steeper the descent the shorter the step and the slower the walk (Kawamura 1991), but a walk: not Tobler's crawl.</summary>
        [Test]
        public void W2_ASteepDescentIsWalkedNotCrawled()
        {
            double thirtyDown = Locomotion.SpeedMs(-Math.Tan(30.0 * Math.PI / 180.0), Gait.Walking, 1.0);
            Assert.That(thirtyDown, Is.GreaterThan(0.5), "a 30-degree descent is a careful walk, where Tobler's hour-average gave 0.3 m/s floored to 0.45");
            Assert.That(thirtyDown, Is.LessThan(Locomotion.SpeedMs(0.0, Gait.Walking, 1.0)), "and slower than the flat");
            Assert.That(Locomotion.SpeedMs(-0.445, Gait.Walking, 1.0), Is.GreaterThan(thirtyDown), "24 degrees is walked faster than 30");
            Assert.That(Locomotion.SpeedMs(-0.20, Gait.Walking, 1.0), Is.GreaterThan(Locomotion.SpeedMs(-0.445, Gait.Walking, 1.0)), "and 11 faster than 24");
        }

        /// <summary>No travel-pace factor: the numbers are stepping speeds already; Tobler stays as the journey's, unchanged.</summary>
        [Test]
        public void W3_TheStepsPaceIsNotTheJourneys()
        {
            Assert.That(Locomotion.SpeedMs(0.0, Gait.Walking, 1.0), Is.EqualTo(Locomotion.WalkingSpeedMs(0.0)).Within(1e-9), "a full body at a walk on the flat moves at the table's own number");
            Assert.That(Locomotion.JourneySpeedMs(0.0), Is.EqualTo(1.40).Within(0.02), "Tobler's flat hour-average, kept for the journey");
            Assert.That(Locomotion.JourneySpeedMs(-0.05), Is.GreaterThan(Locomotion.JourneySpeedMs(0.0)), "with its peak on the gentle descent");
        }

        /// <summary>
        /// Nothing loose stands steeper than about 35 degrees, dry sand's angle of repose: the dune's face is the limit of walked ground,
        /// and the walker's table ends there with a careful pace. The limit spent an evening back at 45°, when William fell through
        /// the world on a face between the two; it is the repose again since the mover keeps a sliding body's feet on the surface (M1.5i).
        /// </summary>
        [Test]
        public void W4_WalkedGroundEndsAtTheRepose()
        {
            Assert.That(MoverConfig.Default.WalkableSlopeDeg, Is.EqualTo(35.0));
            double atTheLimit = Locomotion.SpeedMs(-Math.Tan(35.0 * Math.PI / 180.0), Gait.Walking, 1.0);
            Assert.That(atTheLimit, Is.GreaterThan(0.3).And.LessThan(0.6), "the last walked pace is a careful one");
            Assert.That(Locomotion.SpeedMs(-1.5, Gait.Walking, 1.0), Is.EqualTo(atTheLimit).Within(1e-3), "and past the limit the law has no more to say: the mover slides");
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
