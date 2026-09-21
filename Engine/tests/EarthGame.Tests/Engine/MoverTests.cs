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

        /// <summary>
        /// The client's blind spot, mimicked (2026-09-21): a PhysX capsule cast that starts with the capsule's foot already inside
        /// the sloping terrain reports no hit, so on a face too steep to stand on the sweep says the way is free. This wraps the
        /// exact heightfield collision and ignores any sweep whose capsule starts inside the ground within its own radius.
        /// </summary>
        private sealed class SweepThatIgnoresWhatItStartsInside : IWorldCollision
        {
            private readonly HeightfieldCollision _inner;
            private readonly Func<double, double, double> _ground;
            public SweepThatIgnoresWhatItStartsInside(HeightfieldCollision inner, Func<double, double, double> ground) { _inner = inner; _ground = ground; }
            public bool ProbeGround(Double3 feet, double radius, double stepUp, double maxDown, out double groundUp, out Double3 normal)
                => _inner.ProbeGround(feet, radius, stepUp, maxDown, out groundUp, out normal);
            public bool SweepCapsule(Double3 feet, double radius, double height, Double3 delta, out double fraction, out Double3 normal)
            {
                bool inside = false;
                foreach ((double dx, double dz) in new[] { (radius, 0.0), (-radius, 0.0), (0.0, radius), (0.0, -radius) })
                    inside |= _ground(feet.X + dx, feet.Z + dz) > feet.Y + 0.05;
                if (inside) { fraction = 1.0; normal = Double3.Up; return false; }
                return _inner.SweepCapsule(feet, radius, height, delta, out fraction, out normal);
            }
            public double WaterSurfaceAt(double east, double north) => _inner.WaterSurfaceAt(east, north);
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
        public void WalkingOnTheFlatReachesTheWalkersPace()
        {
            IWorldCollision flat = World((e, n) => 0.0);
            MoverState s = Run(MoverState.AtRest(0.0, 0.0, 0.0), MoverInput.Walk(1.0, 0.0), flat, 150);
            double expected = Locomotion.SpeedMs(0.0, Gait.Walking, 1.0);
            Assert.That(s.HorizontalSpeed, Is.EqualTo(expected).Within(1e-6), "three seconds in, the walker is at the flat walking speed");
            Assert.That(s.East, Is.LessThan(expected * 3.0).And.GreaterThan(expected * 2.0), "having spent the first steps getting there");
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
            Assert.That(up.HorizontalSpeed, Is.EqualTo(Locomotion.SpeedMs(grade, Gait.Walking, 1.0)).Within(1e-6), "at the walker's own speed for that grade, once at pace");
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
            MoverState s = Run(MoverState.AtRest(0.0, 0.0, 0.0), MoverInput.Walk(1.0, 0.0), step, 220);
            Assert.That(s.East, Is.GreaterThan(4.0), "walked past the step");
            Assert.That(s.Up, Is.EqualTo(0.3).Within(1e-9), "and is standing on it");
            Assert.That(s.Grounded, Is.True);
        }

        /// <summary>
        /// M1.5h (ruling 34): a body reaches its pace by the third step (Gait and Posture 83, 2021: 90 % of the steady speed by
        /// then), and is not at it in one tick; let go, it brakes at v1's four metres a second per second — a quarter of a
        /// metre from a walk — and a reversed wish passes through a stop, never flipping in a tick.
        /// </summary>
        [Test]
        public void ABodyReachesItsPaceByTheThirdStep()
        {
            IWorldCollision flat = World((e, n) => 0.0);
            double walk = Locomotion.SpeedMs(0.0, Gait.Walking, 1.0);
            MoverState oneTick = Run(MoverState.AtRest(0.0, 0.0, 0.0), MoverInput.Walk(1.0, 0.0), flat, 1);
            Assert.That(oneTick.HorizontalSpeed, Is.LessThan(0.5 * walk), "not at pace in one tick");
            MoverState later = Run(MoverState.AtRest(0.0, 0.0, 0.0), MoverInput.Walk(1.0, 0.0), flat, 75);
            Assert.That(later.HorizontalSpeed, Is.GreaterThan(0.9 * walk), "at nine tenths of the walk within a second and a half");
            double run = Locomotion.SpeedMs(0.0, Gait.Running, 1.0);
            MoverState running = Run(MoverState.AtRest(0.0, 0.0, 0.0), MoverInput.Walk(1.0, 0.0, sprint: true), flat, 60);
            Assert.That(running.HorizontalSpeed, Is.GreaterThan(0.9 * run), "a run is at nine tenths within about a second: the same count of quicker steps");
        }

        [Test]
        public void LetGoABodyStopsInItsOwnLength()
        {
            IWorldCollision flat = World((e, n) => 0.0);
            MoverState walking = Run(MoverState.AtRest(0.0, 0.0, 0.0), MoverInput.Walk(1.0, 0.0), flat, 150);
            MoverState afterOne = Run(walking, MoverInput.None, flat, 1);
            Assert.That(afterOne.HorizontalSpeed, Is.GreaterThan(0.0), "a stop is not instant");
            MoverState stopped = Run(walking, MoverInput.None, flat, 50);
            Assert.That(stopped.HorizontalSpeed, Is.LessThan(1e-6), "and is a stop within a second");
            Assert.That(stopped.East - walking.East, Is.GreaterThan(0.05).And.LessThan(0.5), "about a quarter of a metre of it from a walk");
            MoverState runningState = Run(MoverState.AtRest(0.0, 0.0, 0.0), MoverInput.Walk(1.0, 0.0, sprint: true), flat, 150);
            MoverState stoppedRun = Run(runningState, MoverInput.None, flat, 100);
            Assert.That(stoppedRun.East - runningState.East, Is.GreaterThan(1.0).And.LessThan(2.5), "a metre and a half or so from a run");
        }

        [Test]
        public void AReversedWishPassesThroughAStop()
        {
            IWorldCollision flat = World((e, n) => 0.0);
            MoverState walking = Run(MoverState.AtRest(0.0, 0.0, 0.0), MoverInput.Walk(1.0, 0.0), flat, 150);
            MoverState turned = Run(walking, MoverInput.Walk(-1.0, 0.0), flat, 1);
            Assert.That(turned.VelEast, Is.GreaterThan(0.0), "still moving east the tick after the wish turns west");
            MoverState later = Run(walking, MoverInput.Walk(-1.0, 0.0), flat, 150);
            Assert.That(later.VelEast, Is.LessThan(0.0), "and west in time");
            Assert.That(later.HorizontalSpeed, Is.EqualTo(Locomotion.SpeedMs(0.0, Gait.Walking, 1.0)).Within(1e-6));
        }

        /// <summary>
        /// Walked ground ends at the repose (M1.5h): a 30-degree face is walked, a 40-degree one, walked until 2026-09-21, is a slide,
        /// and so is a 50. The limit spent an evening at 45° after William fell through a face between 35 and 45; the slide keeps its
        /// feet now (M1.5i) and the repose is the limit again.
        /// </summary>
        [Test]
        public void AFortyDegreeFaceIsASlideAndAThirtyIsWalked()
        {
            IWorldCollision forty = World((e, n) => e * Math.Tan(40.0 * Math.PI / 180.0));
            MoverState onForty = Run(MoverState.AtRest(2.0, 2.0 * Math.Tan(40.0 * Math.PI / 180.0), 0.0), MoverInput.Walk(1.0, 0.0), forty, 100);
            Assert.That(onForty.Grounded, Is.False, "nothing loose stands at forty degrees, and nor does the founder");
            IWorldCollision fifty = World((e, n) => e * Math.Tan(50.0 * Math.PI / 180.0));
            MoverState onFifty = Run(MoverState.AtRest(2.0, 2.0 * Math.Tan(50.0 * Math.PI / 180.0), 0.0), MoverInput.Walk(1.0, 0.0), fifty, 100);
            Assert.That(onFifty.Grounded, Is.False, "there is no standing on fifty degrees");
            IWorldCollision thirty = World((e, n) => e * Math.Tan(30.0 * Math.PI / 180.0));
            MoverState onThirty = Run(MoverState.AtRest(2.0, 2.0 * Math.Tan(30.0 * Math.PI / 180.0), 0.0), MoverInput.Walk(-1.0, 0.0), thirty, 100);
            Assert.That(onThirty.Grounded, Is.True, "thirty degrees is walked");
            Assert.That(onThirty.East, Is.LessThan(2.0), "down it");
        }

        /// <summary>
        /// William fell through the world on a face too steep to stand on (2026-09-21): the sweep on the client is a capsule cast
        /// that starts inside the sloping terrain and reports nothing, so gravity took the body through. The mover itself now
        /// keeps a sliding body's feet on the surface under them, so the body slides down the face and never leaves it, whatever
        /// the sweep fails to see.
        /// </summary>
        [Test]
        public void ASlidingBodyKeepsItsFeetOnTheSurfaceEvenWhenTheSweepSeesNothing()
        {
            double grade = Math.Tan(50.0 * Math.PI / 180.0);
            Func<double, double, double> face = (e, n) => e * grade;
            IWorldCollision blind = new SweepThatIgnoresWhatItStartsInside(World(face), face);
            MoverState s = MoverState.AtRest(2.0, 2.0 * grade, 0.0);
            double lowestUnder = 0.0;
            for (int i = 0; i < 100; i++)
            {
                s = Mover.Step(s, MoverInput.Walk(1.0, 0.0), Dt, blind);
                lowestUnder = Math.Min(lowestUnder, s.Up - face(s.East, s.North));
            }
            Assert.That(lowestUnder, Is.GreaterThan(-0.05), "the body went through the face by " + (-lowestUnder).ToString("0.00") + " m");
            Assert.That(s.Grounded, Is.False, "there is no standing on fifty degrees");
            Assert.That(s.East, Is.LessThan(2.0), "and the body slid down it");
        }

        [Test]
        public void TooSteepToStandOnMeansSliding()
        {
            const double grade = 1.8; // 61 degrees, past the 35 walkable
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
            // 2.4 s of walking at 1.39 m/s, less the first steps' getting to pace, is about 2.4 m: past the edge at 2 m with
            // a quarter of a second of falling behind it.
            MoverState s = Run(MoverState.AtRest(0.0, 0.0, 0.0), MoverInput.Walk(1.0, 0.0), cliff, 120);
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
            MoverState s = Run(MoverState.AtRest(0.0, -0.6, 0.0), MoverInput.Walk(1.0, 0.0), shore, 100);
            Assert.That(s.Wading, Is.True, "sixty centimetres of sea over the feet is wading");
            Assert.That(s.Grounded, Is.True, "still on the bottom");
            Assert.That(s.HorizontalSpeed, Is.EqualTo(Locomotion.SpeedMs(0.0, Gait.Walking, 1.0) * MoverConfig.Default.WadeSpeedFactor).Within(1e-9), "at the wade's pace once there");
            MoverState sprint = Run(MoverState.AtRest(0.0, -0.6, 0.0), MoverInput.Walk(1.0, 0.0, sprint: true), shore, 100);
            Assert.That(sprint.HorizontalSpeed, Is.EqualTo(s.HorizontalSpeed).Within(1e-9), "there is no sprinting through water");
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
            MoverState s = Run(MoverState.AtRest(0.0, 10.0, 0.0), MoverInput.Walk(1.0, 0.0), lake, 50);
            Assert.That(s.Wading, Is.True, "sixty centimetres of lake over the feet is wading");
            Assert.That(s.HorizontalSpeed, Is.EqualTo(walking * MoverConfig.Default.WadeSpeedFactor).Within(1e-9));
            for (int i = 0; i < 2000 && s.East < 7.0; i++) s = Mover.Step(s, MoverInput.Walk(1.0, 0.0), Dt, lake);
            s = Mover.Step(s, MoverInput.Walk(1.0, 0.0), Dt, lake);
            Assert.That(s.East, Is.GreaterThan(7.0), "walked out of the water, and two metres on, at pace again");
            Assert.That(s.Wading, Is.False, "on the dry side");
            Assert.That(s.HorizontalSpeed, Is.EqualTo(walking).Within(1e-9));

            // Ground below the datum that the world's own water leaves dry is dry.
            Ground hollow = new Ground((e, n) => -0.6);
            IWorldCollision dryHollow = HeightfieldCollision.For(hollow, MoverConfig.Default, hasSea: true, water: hollow);
            Assert.That(Run(MoverState.AtRest(0.0, -0.6, 0.0), MoverInput.None, dryHollow, 5).Wading, Is.False, "the sea rule is not asked");
        }

        /// <summary>Water too deep to stand in is swum, with the eye above the surface, at the strokes' paces, and no jump out of it (M1.5e, CANON ruling 24).</summary>
        [Test]
        public void DeepWaterIsSwumWithTheEyeAboveTheSurface()
        {
            MoverConfig cfg = MoverConfig.Default;
            IWorldCollision sea = World((e, n) => -3.0, sea: true);
            MoverState s = Run(MoverState.AtRest(0.0, -3.0, 0.0), MoverInput.None, sea, 300);
            Assert.That(s.Swimming, Is.True, "three metres of sea is over the head");
            Assert.That(s.Grounded, Is.False);
            Assert.That(s.Wading, Is.False, "swimming is not wading");
            Assert.That(s.Up, Is.EqualTo(-cfg.SwimDepth).Within(0.01), "floating at the float line");
            Assert.That(s.Up + cfg.EyeHeight(Stance.Standing), Is.EqualTo(cfg.SwimEyeAboveWaterM).Within(0.01), "the eye a hand's breadth above the water");
            Assert.That(Run(s, MoverInput.Walk(1.0, 0.0), sea, 50).HorizontalSpeed, Is.EqualTo(Locomotion.SwimmingSpeedMs(false, 1.0)).Within(1e-9), "the breaststroke");
            Assert.That(Run(s, MoverInput.Walk(1.0, 0.0, sprint: true), sea, 50).HorizontalSpeed, Is.EqualTo(Locomotion.SwimmingSpeedMs(true, 1.0)).Within(1e-9), "the crawl");
            MoverInput jump = MoverInput.None;
            jump.Jump = true;
            MoverInput crouch = MoverInput.None;
            crouch.Crouch = true;
            Assert.That(Run(s, jump, sea, 25).Up, Is.EqualTo(s.Up).Within(0.01), "no jumping out of deep water");
            Assert.That(Run(s, crouch, sea, 5).Stance, Is.EqualTo(Stance.Standing), "no crouching in it");
        }

        /// <summary>A swimmer reaching a shore stands where the bottom comes within reach and wades out, and never goes below the bed (M1.5e).</summary>
        [Test]
        public void ASwimmerStandsWhereTheBottomComesBackAndWadesOut()
        {
            // The bed rises from three metres under the sea in the west to dry land past east 37.5.
            Func<double, double, double> bed = (e, n) => -3.0 + Math.Max(0.0, e) * 0.08;
            IWorldCollision shore = World(bed, sea: true);
            MoverState s = Run(MoverState.AtRest(0.0, -3.0, 0.0), MoverInput.None, shore, 300);
            Assert.That(s.Swimming, Is.True);
            bool stood = false, waded = false;
            for (int i = 0; i < 6000 && s.East < 40.0; i++)
            {
                s = Mover.Step(s, MoverInput.Walk(1.0, 0.0), Dt, shore);
                Assert.That(s.IsFinite, Is.True);
                Assert.That(s.Up, Is.GreaterThanOrEqualTo(bed(s.East, 0.0) - 1e-6), "never below the bed, at east " + s.East);
                if (s.Grounded && !s.Swimming) stood = true;
                if (s.Wading) waded = true;
            }
            Assert.That(s.East, Is.GreaterThanOrEqualTo(40.0), "out of the water");
            Assert.That(stood && waded, Is.True, "stood where the bottom came within reach, and waded out");
            Assert.That(s.Swimming || s.Wading, Is.False, "on dry land");
            Assert.That(s.Grounded, Is.True);
        }

        /// <summary>However hard a founder comes down into deep water, the water takes them before the eye goes under (M1.5e).</summary>
        [Test]
        public void AFallIntoDeepWaterNeverTakesTheEyeUnder()
        {
            MoverConfig cfg = MoverConfig.Default;
            IWorldCollision sea = World((e, n) => -10.0, sea: true);
            MoverState s = MoverState.AtRest(0.0, 8.0, 0.0);
            bool swam = false;
            for (int i = 0; i < 500; i++)
            {
                s = Mover.Step(s, MoverInput.None, Dt, sea);
                Assert.That(s.IsFinite, Is.True);
                Assert.That(s.Up + cfg.EyeHeight(s.Stance), Is.GreaterThan(0.0), "the eye under the water at step " + i);
                swam |= s.Swimming;
            }
            Assert.That(swam, Is.True);
            Assert.That(s.Up, Is.EqualTo(-cfg.SwimDepth).Within(0.01), "and settled at the float line");
        }

        /// <summary>A founder does not crouch where the crouched eye would be under the water, and does where it would not (M1.5e).</summary>
        [Test]
        public void NoCrouchingWhereTheCrouchedEyeWouldGoUnder()
        {
            MoverInput crouch = MoverInput.None;
            crouch.Crouch = true;
            MoverState chest = MoverState.AtRest(0.0, -1.2, 0.0);
            chest.Grounded = true;
            MoverState stood = Run(chest, crouch, World((e, n) => -1.2, sea: true), 5);
            Assert.That(stood.Stance, Is.EqualTo(Stance.Standing), "1.2 m of water is over a crouched eye");
            Assert.That(stood.Swimming, Is.False, "and a standing body is waist-deep in it, not swimming");
            MoverState knee = MoverState.AtRest(0.0, -0.5, 0.0);
            knee.Grounded = true;
            Assert.That(Run(knee, crouch, World((e, n) => -0.5, sea: true), 5).Stance, Is.EqualTo(Stance.Crouching), "0.5 m is not");
        }

        [Test]
        public void CrouchingIsSlowerAndSwapsTheStance()
        {
            IWorldCollision flat = World((e, n) => 0.0);
            MoverInput crouch = MoverInput.Walk(1.0, 0.0);
            crouch.Crouch = true;
            MoverState s = Run(MoverState.AtRest(0.0, 0.0, 0.0), crouch, flat, 100);
            Assert.That(s.Stance, Is.EqualTo(Stance.Crouching));
            Assert.That(s.HorizontalSpeed, Is.EqualTo(Locomotion.SpeedMs(0.0, Gait.Walking, 1.0) * MoverConfig.Default.CrouchSpeedFactor).Within(1e-9), "at the crouch's pace once there");
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
