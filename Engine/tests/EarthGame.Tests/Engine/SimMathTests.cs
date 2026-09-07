using System;
using EarthGame.Engine;
using NUnit.Framework;

namespace EarthGame.Tests.Engine
{
    public sealed class SimMathTests
    {
        [Test]
        public void Clamp01HoldsTheInterval()
        {
            Assert.That(SimMath.Clamp01(-0.5), Is.EqualTo(0.0));
            Assert.That(SimMath.Clamp01(0.25), Is.EqualTo(0.25));
            Assert.That(SimMath.Clamp01(1.5), Is.EqualTo(1.0));
        }

        [Test]
        public void NaNTakesTheLowBound()
        {
            Assert.That(SimMath.Clamp01(double.NaN), Is.EqualTo(0.0));
            Assert.That(SimMath.Clamp(double.NaN, 2.0, 3.0), Is.EqualTo(2.0));
        }

        [Test]
        public void MathClampCarriesTheHoleThisClassCloses()
        {
            // The doc comment claims Math.Clamp passes NaN through. Checkable, not remembered.
            Assert.That(double.IsNaN(Math.Clamp(double.NaN, 0.0, 1.0)), Is.True);
        }
    }
}
