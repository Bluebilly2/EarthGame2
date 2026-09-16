using EarthGame.Engine;
using NUnit.Framework;

namespace EarthGame.Tests.Engine
{
    /// <summary>A death's explanation (FP.2): the sentence made in one place carries the mechanism and the numbers the path asks for.</summary>
    public sealed class DeathTests
    {
        [Test]
        public void TheClockSaysTheHourAsAClockDoes()
        {
            Assert.That(new Death(CauseOfDeath.Cold, 3.15, 9, 2, 300, 80, 28, 0.05, 0, 0).Clock, Is.EqualTo("03:09"));
            Assert.That(new Death(CauseOfDeath.Cold, 0.0, 9, 2, 300, 80, 28, 0.05, 0, 0).Clock, Is.EqualTo("00:00"));
            Assert.That(new Death(CauseOfDeath.Cold, 23.999, 9, 2, 300, 80, 28, 0.05, 0, 0).Clock, Is.EqualTo("23:59"));
            Assert.That(new Death(CauseOfDeath.Cold, 25.5, 9, 2, 300, 80, 28, 0.05, 0, 0).Clock, Is.EqualTo("01:30"), "an hour past the day wraps");
        }

        [Test]
        public void TheColdsSentenceCarriesTheAirTheWindTheBalanceAndTheCore()
        {
            Death d = new Death(CauseOfDeath.Cold, 3.15, 8.6, 2.34, 297.0, 80.0, 27.96, 0.06, -1392.0, 2804.0);
            string s = d.Explain();
            Assert.That(s, Does.StartWith("You died of the cold at 03:09."));
            Assert.That(s, Does.Contain("9° air").And.Contain("2.3 m/s wind").And.Contain("loses about 297 W and makes 80"));
            Assert.That(s, Does.Contain("shivering held the core for a while and ran out").And.Contain("at 28.0° the heart stops"));
            Assert.That(s, Does.EndWith("A new founder wakes on the beach; what you carried lies where you fell."));
        }

        [Test]
        public void TheThirstsSentenceCarriesTheLitresAndTheShare()
        {
            Death d = new Death(CauseOfDeath.Thirst, 14.5, 22.0, 3.0, 150.0, 80.0, 36.8, 0.15, 0.0, 0.0);
            string s = d.Explain();
            Assert.That(s, Does.StartWith("You died of thirst at 14:30:"));
            Assert.That(s, Does.Contain("6.3 litres down, 15% of the body's water"), "15% of 42 litres");
            Assert.That(s, Does.Contain("nothing drunk in time"));
            Assert.That(s, Does.EndWith("what you carried lies where you fell."));
        }

        [Test]
        public void TheCausesKeepTheirNumbers()
        {
            Assert.That((byte)CauseOfDeath.None, Is.EqualTo((byte)0));
            Assert.That((byte)CauseOfDeath.Cold, Is.EqualTo((byte)1));
            Assert.That((byte)CauseOfDeath.Thirst, Is.EqualTo((byte)2));
        }
    }
}
