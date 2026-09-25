using EarthGame.ClientCore;
using NUnit.Framework;

namespace EarthGame.Tests.ClientCore
{
    /// <summary>
    /// The haze's depth from the humidity (M1.6h): 40 km of visibility at the coast's usual 70 %, and the aerosol's growth with
    /// the humidity, f(RH) = a (1 − RH)^−0.6, making it (0.03 / 0.30)^0.6 of that at 97 %, about 10 km, and (0.60 / 0.30)^0.6
    /// of it at 40 % and below, about 61 km. The expected values are the law worked by hand.
    /// </summary>
    public sealed class HazeTests
    {
        [TestCase(0.70, 40.000)]
        [TestCase(0.97, 10.048)]
        [TestCase(0.99, 10.048)]
        [TestCase(0.40, 60.629)]
        [TestCase(0.10, 60.629)]
        [TestCase(0.85, 26.390)]
        public void TheVisibilityFollowsTheHumidity(double humidity01, double km)
        {
            Assert.That(Haze.VisibilityM(humidity01) / 1000.0, Is.EqualTo(km).Within(0.002));
        }

        [Test]
        public void AnUnknownHumidityIsTheUsualOne()
        {
            Assert.That(Haze.VisibilityM(double.NaN), Is.EqualTo(Haze.VisibilityAtUsualM).Within(1e-6));
        }

        [Test]
        public void TheExtinctionIsKoschmiedersOfTheVisibility()
        {
            Assert.That(Haze.PerMetre(Haze.UsualHumidity01) * 40000.0, Is.EqualTo(3.912).Within(1e-9));
        }

        [Test]
        public void TheDamperTheAirTheDeeperTheHaze()
        {
            double previous = 0.0;
            for (double rh = 0.40; rh <= 0.971; rh += 0.01)
            {
                double perMetre = Haze.PerMetre(rh);
                Assert.That(perMetre, Is.GreaterThan(previous), "at " + rh);
                previous = perMetre;
            }
        }
    }
}
