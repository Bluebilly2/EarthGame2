using System;
using EarthGame.Engine;
using NUnit.Framework;

namespace EarthGame.Tests.Engine
{
    /// <summary>A developer's flight (M1.5e): where the view points, at v1's speeds, rising and sinking as asked, and never under the ground.</summary>
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
                                      double yawDeg, double pitchDeg, IHeightSource ground, int steps)
        {
            for (int i = 0; i < steps; i++)
            {
                s = Flight.Step(s, right, forward, rise, sink, run, yawDeg, pitchDeg, Dt, ground);
                Assert.That(s.IsFinite, Is.True, "state went non-finite at step " + i);
                Assert.That(s.Up, Is.GreaterThanOrEqualTo(ground.HeightAt(s.East, s.North)), "under the ground at step " + i);
            }
            return s;
        }

        [Test]
        public void FlightGoesWhereTheViewPointsAtItsSpeeds()
        {
            IHeightSource flat = new Ground((e, n) => 0.0);
            MoverState s = MoverState.AtRest(0.0, 50.0, 0.0);
            // Facing east and level: three seconds on, the ease has all but finished and the body goes east at the flight's speed.
            MoverState east = Fly(s, 0.0, 1.0, false, false, false, 90.0, 0.0, flat, 150);
            Assert.That(east.VelEast, Is.EqualTo(Flight.SpeedMs).Within(0.1));
            Assert.That(Math.Abs(east.VelNorth) + Math.Abs(east.VelUp), Is.LessThan(1e-6));
            Assert.That(east.East, Is.GreaterThan(50.0));
            Assert.That(east.Grounded || east.Wading || east.Swimming, Is.False);
            Assert.That(Fly(s, 0.0, 1.0, false, false, true, 90.0, 0.0, flat, 150).HorizontalSpeed,
                Is.EqualTo(Flight.SpeedMs * Flight.RunFactor).Within(0.5), "running, six times as fast");
            // Facing north and looking thirty degrees up, it climbs as it goes.
            MoverState climb = Fly(s, 0.0, 1.0, false, false, false, 0.0, -30.0, flat, 150);
            Assert.That(climb.VelUp / climb.VelNorth, Is.EqualTo(Math.Tan(30.0 * Math.PI / 180.0)).Within(1e-3));
            Assert.That(Fly(s, 1.0, 0.0, false, false, false, 0.0, 0.0, flat, 150).VelEast, Is.EqualTo(Flight.SpeedMs).Within(0.1),
                "the stick's right is the founder's: facing north, east");
            Assert.That(Fly(s, 0.0, 0.0, true, false, false, 0.0, 0.0, flat, 100).Up, Is.GreaterThan(s.Up + 20.0), "rising");
            Assert.That(Fly(s, 0.0, 0.0, false, true, false, 0.0, 0.0, flat, 100).Up, Is.LessThan(s.Up - 20.0), "sinking");
            Assert.That(Fly(east, 0.0, 0.0, false, false, false, 90.0, 0.0, flat, 250).HorizontalSpeed, Is.LessThan(0.01), "let go, it eases to a stop");
        }

        [Test]
        public void FlightNeverGoesUnderTheGround()
        {
            // A hill rising to the east, flown into looking steeply down, sinking and running: the feet stay on it.
            IHeightSource hill = new Ground((e, n) => Math.Max(0.0, e) * 0.5);
            MoverState low = Fly(MoverState.AtRest(0.0, 5.0, 0.0), 0.0, 1.0, false, true, true, 90.0, 60.0, hill, 300);
            Assert.That(low.East, Is.GreaterThan(100.0));
            Assert.That(low.Up, Is.EqualTo(hill.HeightAt(low.East, low.North) + Flight.ClearanceM).Within(1e-9), "pressed against the hill, not in it");
        }
    }
}
