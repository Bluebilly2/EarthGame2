using System;
using EarthGame.Engine;
using NUnit.Framework;

namespace EarthGame.Tests.Engine
{
    /// <summary>
    /// A developer's flight (M1.5e): where the view points, at v1's speeds, rising and sinking as asked, and through the ground
    /// (CANON ruling 25); and given a world (M1.D, the noclip switch off), stopped by the ground and never left under it.
    /// </summary>
    public sealed class FlightTests
    {
        private const double Dt = 0.02;

        private sealed class Ground : IHeightSource
        {
            private readonly Func<double, double, double> _f;
            public Ground(Func<double, double, double> f) { _f = f; }
            public double HeightAt(double east, double north) => _f(east, north);
        }

        private static MoverState Fly(MoverState s, double right, double forward, bool rise, bool sink, bool run,
                                      double yawDeg, double pitchDeg, int steps)
        {
            for (int i = 0; i < steps; i++)
            {
                s = Flight.Step(s, right, forward, rise, sink, run, yawDeg, pitchDeg, Dt);
                Assert.That(s.IsFinite, Is.True, "state went non-finite at step " + i);
            }
            return s;
        }

        [Test]
        public void FlightGoesWhereTheViewPointsAtItsSpeeds()
        {
            MoverState s = MoverState.AtRest(0.0, 50.0, 0.0);
            // Facing east and level: three seconds on, the ease has all but finished and the body goes east at the flight's speed.
            MoverState east = Fly(s, 0.0, 1.0, false, false, false, 90.0, 0.0, 150);
            Assert.That(east.VelEast, Is.EqualTo(Flight.SpeedMs).Within(0.1));
            Assert.That(Math.Abs(east.VelNorth) + Math.Abs(east.VelUp), Is.LessThan(1e-6));
            Assert.That(east.East, Is.GreaterThan(50.0));
            Assert.That(east.Grounded || east.Wading || east.Swimming, Is.False);
            Assert.That(Fly(s, 0.0, 1.0, false, false, true, 90.0, 0.0, 150).HorizontalSpeed,
                Is.EqualTo(Flight.SpeedMs * Flight.RunFactor).Within(0.5), "running, six times as fast");
            // Facing north and looking thirty degrees up, it climbs as it goes.
            MoverState climb = Fly(s, 0.0, 1.0, false, false, false, 0.0, -30.0, 150);
            Assert.That(climb.VelUp / climb.VelNorth, Is.EqualTo(Math.Tan(30.0 * Math.PI / 180.0)).Within(1e-3));
            Assert.That(Fly(s, 1.0, 0.0, false, false, false, 0.0, 0.0, 150).VelEast, Is.EqualTo(Flight.SpeedMs).Within(0.1),
                "the stick's right is the founder's: facing north, east");
            Assert.That(Fly(s, 0.0, 0.0, true, false, false, 0.0, 0.0, 100).Up, Is.GreaterThan(s.Up + 20.0), "rising");
            Assert.That(Fly(s, 0.0, 0.0, false, true, false, 0.0, 0.0, 100).Up, Is.LessThan(s.Up - 20.0), "sinking");
            Assert.That(Fly(east, 0.0, 0.0, false, false, false, 90.0, 0.0, 250).HorizontalSpeed, Is.LessThan(0.01), "let go, it eases to a stop");
        }

        [Test]
        public void FlightGoesThroughTheGround()
        {
            // A hill rising to the east, flown into looking steeply down, sinking and running: the founder goes into it and
            // out under it, which is what noclip is for (CANON ruling 25).
            IHeightSource hill = new Ground((e, n) => Math.Max(0.0, e) * 0.5);
            MoverState under = Fly(MoverState.AtRest(0.0, 5.0, 0.0), 0.0, 1.0, false, true, true, 90.0, 60.0, 300);
            Assert.That(under.East, Is.GreaterThan(100.0), "well into the hill");
            Assert.That(under.Up, Is.LessThan(hill.HeightAt(under.East, under.North) - 50.0), "and well under it");
            Assert.That(under.Grounded || under.Wading || under.Swimming, Is.False);
        }

        [Test]
        public void FlightGivenAWorldIsStoppedByTheGroundAndNeverLeftUnderIt()
        {
            // The same hill, flown into the same way but at the flight's walking pace, with the world to be stopped by (M1.D):
            // the founder lands on its face and skims up it. At the running pace the heightfield's own sweep, which judges a
            // rise by the stride, would call the hill a face; the game's ground is PhysX's, which judges the surface.
            IHeightSource hill = new Ground((e, n) => Math.Max(0.0, e) * 0.5);
            IWorldCollision world = HeightfieldCollision.For(hill, MoverConfig.Default, false);
            MoverState s = MoverState.AtRest(0.0, 5.0, 0.0);
            for (int i = 0; i < 300; i++)
            {
                s = Flight.Step(s, 0.0, 1.0, false, true, false, 90.0, 60.0, Dt, world, MoverConfig.Default);
                Assert.That(s.IsFinite, Is.True, "state went non-finite at step " + i);
                Assert.That(s.Up, Is.GreaterThanOrEqualTo(hill.HeightAt(s.East, s.North) - 0.05), "under the ground at step " + i);
            }
            Assert.That(s.East, Is.GreaterThan(5.0), "and still gets along the face");
            Assert.That(s.Grounded || s.Wading || s.Swimming, Is.False, "a flight is a flight, stopped or not");

            // Level over the flat, the world in the way changes nothing of the flight.
            IWorldCollision flat = HeightfieldCollision.For(new Ground((e, n) => 0.0), MoverConfig.Default, false);
            MoverState level = MoverState.AtRest(0.0, 50.0, 0.0);
            for (int i = 0; i < 150; i++) level = Flight.Step(level, 0.0, 1.0, false, false, false, 90.0, 0.0, Dt, flat, MoverConfig.Default);
            Assert.That(level.VelEast, Is.EqualTo(Flight.SpeedMs).Within(0.1));
            Assert.That(level.Up, Is.EqualTo(50.0).Within(1e-6));
        }
    }
}
