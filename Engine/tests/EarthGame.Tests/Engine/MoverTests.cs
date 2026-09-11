using System;
using EarthGame.Engine;
using NUnit.Framework;

namespace EarthGame.Tests.Engine
{
    /// <summary>
    /// The mover on analytic ground: a plane, a slope, a wall, a step, a cliff and shallow water, each stated as a
    /// height function so what the test expects can be worked out by hand. The feel of the numbers is the owner's
    /// decision hands-on (ruling 12); what is asserted here is that the function is sound: nothing sinks, nothing
    /// climbs a wall, nothing produces a NaN, and the same inputs give the same output.
    /// </summary>
    public sealed class MoverTests
    {
        private const double Dt = 0.02;

        private sealed class Ground : IHeightSource
        {
            private readonly Func<double, double, double> _f;
            public Ground(Func<double, double, double> f) { _f = f; }
            public double HeightAt(double east, double north) => _f(east, north);
        }

        private static HeightfieldCollision World(Func<double, double, double> height, bool sea = false)
            => HeightfieldCollision.For(new Ground(height), MoverConfig.Default, sea);

        private static MoverState Run(MoverState s, MoverInput input, IWorldCollision world, int steps)
        {
            for (int i = 0; i < steps; i++)
            {
                s = Mover.Step(s, input, Dt, world);
                Assert.That(s.IsFinite, Is.True, "state went non-finite at step " + i);
            }
            return s;
        }

        [Test]
        public void StandingStillOnTheFlatStaysPut()
        {
            IWorldCollision flat = World((e, n) => 10.0);
            MoverState s = Run(MoverState.AtRest(1.0, 10.0, 2.0), MoverInput.None, flat, 50);
            Assert.That(s.East, Is.EqualTo(1.0).Within(1e-12));
            Assert.That(s.North, Is.EqualTo(2.0).Within(1e-12));
            Assert.That(s.Up, Is.EqualTo(10.0).Within(1e-12));
            Assert.That(s.Grounded, Is.True);
            Assert.That(s.Wading, Is.False);
        }

        [Test]
        public void WalkingOnTheFlatGoesAtToblersPace()
        {
            IWorldCollision flat = World((e, n) => 0.0);
            MoverState s = Run(MoverState.AtRest(0.0, 0.0, 0.0), MoverInput.Walk(1.0, 0.0), flat, 50);
            double expected = Locomotion.SpeedMs(0.0, Gait.Walking, 1.0);
            Assert.That(s.East, Is.EqualTo(expected * 1.0).Within(1e-9), "one second of walking east at the flat walking speed");
            Assert.That(s.HorizontalSpeed, Is.EqualTo(expected).Within(1e-9));
            Assert.That(s.Grounded, Is.True);
            Assert.That(s.Up, Is.EqualTo(0.0).Within(1e-12), "feet stay on the ground");
        }

        [Test]
        public void SprintingIsFasterAndADiagonalWishIsNotFasterThanAStraightOne()
        {
            IWorldCollision flat = World((e, n) => 0.0);
            MoverState run = Run(MoverState.AtRest(0.0, 0.0, 0.0), MoverInput.Walk(1.0, 0.0, sprint: true), flat, 50);
            MoverState walk = Run(MoverState.AtRest(0.0, 0.0, 0.0), MoverInput.Walk(1.0, 0.0), flat, 50);
            Assert.That(run.East, Is.GreaterThan(walk.East * 2.0), "running covers more than twice the ground");
            MoverState diagonal = Run(MoverState.AtRest(0.0, 0.0, 0.0), MoverInput.Walk(1.0, 1.0), flat, 50);
            Assert.That(Math.Sqrt(diagonal.East * diagonal.East + diagonal.North * diagonal.North), Is.EqualTo(walk.East).Within(1e-9), "a wish of (1, 1) is normalised: no strafe-running");
        }

        [Test]
        public void GravityBringsAFallingBodyToTheGroundAndNoFurther()
        {
            IWorldCollision flat = World((e, n) => 5.0);
            MoverState s = MoverState.AtRest(0.0, 8.0, 0.0);
            s = Mover.Step(s, MoverInput.None, Dt, flat);
            Assert.That(s.Grounded, Is.False, "three metres up is airborne");
            Assert.That(s.VelUp, Is.LessThan(0.0));
            s = Run(s, MoverInput.None, flat, 100);
            Assert.That(s.Up, Is.EqualTo(5.0).Within(1e-9), "landed exactly on the ground");
            Assert.That(s.Grounded, Is.True);
            Assert.That(s.VelUp, Is.EqualTo(0.0));
        }

        [Test]
        public void AJumpReachesItsHeightAndLands()
        {
            IWorldCollision flat = World((e, n) => 0.0);
            MoverState s = MoverState.AtRest(0.0, 0.0, 0.0);
            MoverInput jump = MoverInput.None;
            jump.Jump = true;
            s = Mover.Step(s, jump, Dt, flat);
            Assert.That(s.Grounded, Is.False);
            Assert.That(s.VelUp, Is.GreaterThan(0.0));
            double apex = 0.0;
            int stepsToLand = 0;
            for (int i = 0; i < 200 && !s.Grounded; i++)
            {
                s = Mover.Step(s, MoverInput.None, Dt, flat);
                apex = Math.Max(apex, s.Up);
                stepsToLand++;
            }
            Assert.That(s.Grounded, Is.True, "landed within four seconds");
            Assert.That(apex, Is.EqualTo(MoverConfig.Default.JumpHeight).Within(0.06), "the apex is the configured jump height");
            Assert.That(stepsToLand * Dt, Is.EqualTo(2.0 * Math.Sqrt(2.0 * MoverConfig.Default.JumpHeight / MoverConfig.Default.Gravity)).Within(0.06), "hang time is 2·sqrt(2h/g)");
            Assert.That(s.Up, Is.EqualTo(0.0).Within(1e-9));
        }

        [Test]
        public void WalkingUpASlopeFollowsItAndIsSlowerThanTheFlat()
        {
            const double grade = 0.3; // 16.7 degrees
            IWorldCollision slope = World((e, n) => e * grade);
            MoverState up = Run(MoverState.AtRest(0.0, 0.0, 0.0), MoverInput.Walk(1.0, 0.0), slope, 100);
            Assert.That(up.Grounded, Is.True);
            Assert.That(up.Up, Is.EqualTo(up.East * grade).Within(1e-9), "the feet are on the slope");
            double flatDistance = Locomotion.SpeedMs(0.0, Gait.Walking, 1.0) * 2.0;
            Assert.That(up.East, Is.LessThan(flatDistance), "uphill is slower");
            Assert.That(up.East, Is.EqualTo(Locomotion.SpeedMs(grade, Gait.Walking, 1.0) * 2.0).Within(1e-6), "at exactly Tobler's speed for that grade");
            MoverState down = Run(MoverState.AtRest(0.0, 0.0, 0.0), MoverInput.Walk(-1.0, 0.0), slope, 100);
            Assert.That(down.Grounded, Is.True, "walking down a gentle slope never leaves the ground");
            Assert.That(down.Up, Is.EqualTo(down.East * grade).Within(1e-9));
        }

        [Test]
        public void AWallStopsTheWalkWithoutPenetration()
        {
            IWorldCollision wall = World((e, n) => e > 5.0 ? 3.0 : 0.0);
            MoverState s = Run(MoverState.AtRest(0.0, 0.0, 0.0), MoverInput.Walk(1.0, 0.0, sprint: true), wall, 200);
            Assert.That(s.East, Is.LessThanOrEqualTo(5.0 + 1e-9), "never inside the wall");
            Assert.That(s.East, Is.GreaterThan(4.5), "and pressed up against it");
            Assert.That(s.Up, Is.EqualTo(0.0).Within(1e-9), "still on the low ground");
            Assert.That(s.Grounded, Is.True);
        }

        [Test]
        public void ALowStepIsSteppedOnto()
        {
            IWorldCollision step = World((e, n) => e > 3.0 ? 0.3 : 0.0);
            MoverState s = Run(MoverState.AtRest(0.0, 0.0, 0.0), MoverInput.Walk(1.0, 0.0), step, 150);
            Assert.That(s.East, Is.GreaterThan(4.0), "walked past the step");
            Assert.That(s.Up, Is.EqualTo(0.3).Within(1e-9), "and is standing on it");
            Assert.That(s.Grounded, Is.True);
        }

        [Test]
        public void TooSteepToStandOnMeansSliding()
        {
            const double grade = 1.8; // 61 degrees, past the 45 walkable
            IWorldCollision steep = World((e, n) => e * grade);
            MoverState s = Run(MoverState.AtRest(2.0, 3.6, 0.0), MoverInput.Walk(1.0, 0.0, sprint: true), steep, 100);
            Assert.That(s.Grounded, Is.False, "there is no standing on this");
            Assert.That(s.East, Is.LessThan(2.0), "gravity wins over the wish to climb");
            Assert.That(s.Up, Is.GreaterThanOrEqualTo(s.East * grade - 1e-6), "and the body slides on the surface, never through it");
        }

        [Test]
        public void WalkingOffACliffFallsToTheLowerGround()
        {
            IWorldCollision cliff = World((e, n) => e > 2.0 ? -4.0 : 0.0);
            // 1.6 s of walking at 1.82 m/s is 2.9 m: past the edge at 2 m with half a second of falling behind it.
            MoverState s = Run(MoverState.AtRest(0.0, 0.0, 0.0), MoverInput.Walk(1.0, 0.0), cliff, 80);
            Assert.That(s.East, Is.GreaterThan(2.0));
            Assert.That(s.Grounded, Is.False, "over the edge, in the air");
            s = Run(s, MoverInput.None, cliff, 100);
            Assert.That(s.Grounded, Is.True);
            Assert.That(s.Up, Is.EqualTo(-4.0).Within(1e-9), "landed on the ground below");
        }

        [Test]
        public void ShallowWaterIsWadedAtHalfPace()
        {
            IWorldCollision shore = World((e, n) => -0.6, sea: true);
            MoverState s = Run(MoverState.AtRest(0.0, -0.6, 0.0), MoverInput.Walk(1.0, 0.0), shore, 50);
            Assert.That(s.Wading, Is.True, "sixty centimetres of sea over the feet is wading");
            Assert.That(s.Grounded, Is.True, "still on the bottom");
            Assert.That(s.East, Is.EqualTo(Locomotion.SpeedMs(0.0, Gait.Walking, 1.0) * MoverConfig.Default.WadeSpeedFactor).Within(1e-9));
            MoverState sprint = Run(MoverState.AtRest(0.0, -0.6, 0.0), MoverInput.Walk(1.0, 0.0, sprint: true), shore, 50);
            Assert.That(sprint.East, Is.EqualTo(s.East).Within(1e-9), "there is no sprinting through water");
            IWorldCollision dry = World((e, n) => 0.6, sea: true);
            Assert.That(Run(MoverState.AtRest(0.0, 0.6, 0.0), MoverInput.None, dry, 5).Wading, Is.False, "ground above the sea is dry");
        }

        /// <summary>A lake in a world's own water is waded as the sea is, and where a world's water is given the sea rule is not asked (M1.5d).</summary>
        [Test]
        public void ALakeInTheWorldsOwnWaterIsWaded()
        {
            // Flat ground at 10 m; water stands at 10.6 m west of east 5, and the surface is the ground's elsewhere.
            Ground ground = new Ground((e, n) => 10.0);
            Ground surface = new Ground((e, n) => e < 5.0 ? 10.6 : 10.0);
            IWorldCollision lake = HeightfieldCollision.For(ground, MoverConfig.Default, hasSea: false, water: surface);
            double walking = Locomotion.SpeedMs(0.0, Gait.Walking, 1.0);
            MoverState s = Run(MoverState.AtRest(0.0, 10.0, 0.0), MoverInput.Walk(1.0, 0.0), lake, 5);
            Assert.That(s.Wading, Is.True, "sixty centimetres of lake over the feet is wading");
            Assert.That(s.HorizontalSpeed, Is.EqualTo(walking * MoverConfig.Default.WadeSpeedFactor).Within(1e-9));
            for (int i = 0; i < 2000 && s.East < 6.0; i++) s = Mover.Step(s, MoverInput.Walk(1.0, 0.0), Dt, lake);
            s = Mover.Step(s, MoverInput.Walk(1.0, 0.0), Dt, lake);
            Assert.That(s.East, Is.GreaterThan(6.0), "walked out of the water");
            Assert.That(s.Wading, Is.False, "on the dry side");
            Assert.That(s.HorizontalSpeed, Is.EqualTo(walking).Within(1e-9));

            // Ground below the datum that the world's own water leaves dry is dry.
            Ground hollow = new Ground((e, n) => -0.6);
            IWorldCollision dryHollow = HeightfieldCollision.For(hollow, MoverConfig.Default, hasSea: true, water: hollow);
            Assert.That(Run(MoverState.AtRest(0.0, -0.6, 0.0), MoverInput.None, dryHollow, 5).Wading, Is.False, "the sea rule is not asked");
        }

        [Test]
        public void CrouchingIsSlowerAndSwapsTheStance()
        {
            IWorldCollision flat = World((e, n) => 0.0);
            MoverInput crouch = MoverInput.Walk(1.0, 0.0);
            crouch.Crouch = true;
            MoverState s = Run(MoverState.AtRest(0.0, 0.0, 0.0), crouch, flat, 50);
            Assert.That(s.Stance, Is.EqualTo(Stance.Crouching));
            Assert.That(s.East, Is.EqualTo(Locomotion.SpeedMs(0.0, Gait.Walking, 1.0) * MoverConfig.Default.CrouchSpeedFactor).Within(1e-9));
        }

        [Test]
        public void TheSameInputsGiveTheSameOutputBitForBit()
        {
            IWorldCollision bumpy = World((e, n) => Math.Sin(e * 0.7) * 0.8 + Math.Cos(n * 0.4) * 0.5);
            MoverInput input = MoverInput.Walk(0.6, 0.8, sprint: true);
            MoverState a = MoverState.AtRest(0.0, 0.8, 0.0);
            MoverState b = a;
            for (int i = 0; i < 300; i++)
            {
                if (i % 40 == 0) input.Jump = true; else input.Jump = false;
                a = Mover.Step(a, input, Dt, bumpy);
                b = Mover.Step(b, input, Dt, bumpy);
            }
            Assert.That(a.East, Is.EqualTo(b.East));
            Assert.That(a.Up, Is.EqualTo(b.Up));
            Assert.That(a.North, Is.EqualTo(b.North));
            Assert.That(a.VelEast, Is.EqualTo(b.VelEast));
            Assert.That(a.IsFinite, Is.True);
        }

        [Test]
        public void ABadInputOrAZeroStepDoesNothingHarmful()
        {
            IWorldCollision flat = World((e, n) => 0.0);
            MoverState s = MoverState.AtRest(0.0, 0.0, 0.0);
            Assert.That(Mover.Step(s, MoverInput.Walk(1.0, 0.0), 0.0, flat).East, Is.EqualTo(0.0), "a zero step moves nothing");
            MoverState nan = Mover.Step(s, MoverInput.Walk(double.NaN, 3.0), Dt, flat);
            Assert.That(nan.IsFinite, Is.True);
            Assert.That(nan.East, Is.EqualTo(0.0), "a NaN wish is no wish east");
            Assert.That(nan.North, Is.GreaterThan(0.0), "the finite half of it still moves");
        }
    }
}
