using EarthGame.ClientCore;
using NUnit.Framework;

namespace EarthGame.Tests.ClientCore
{
    /// <summary>
    /// A table's colour made linear light (M1.6h): the sRGB standard's decoding (IEC 61966-2-1), a straight line to 0.04045 and
    /// a 2.4 power above it. The expected values are the standard's formula worked by hand in double precision, never this
    /// code's: 0.5 is the familiar 0.214, the knee's 0.04045 is 0.0031308, and black and white stay where they are.
    /// </summary>
    public sealed class TableColourTests
    {
        [TestCase(0.0f, 0.0)]
        [TestCase(0.02f, 0.0015480)]
        [TestCase(0.04045f, 0.0031308)]
        [TestCase(0.2f, 0.0331048)]
        [TestCase(0.5f, 0.2140411)]
        [TestCase(0.8f, 0.6038273)]
        [TestCase(1.0f, 1.0)]
        public void EveryChannelIsDecodedByTheStandardsCurve(float srgb, double linear)
        {
            Rgb decoded = new Rgb(srgb, srgb, srgb).ToLinear();
            Assert.That(decoded.R, Is.EqualTo(linear).Within(2e-6), "red");
            Assert.That(decoded.G, Is.EqualTo(linear).Within(2e-6), "green");
            Assert.That(decoded.B, Is.EqualTo(linear).Within(2e-6), "blue");
        }

        [Test]
        public void TheChannelsAreDecodedEachOnItsOwn()
        {
            Rgb decoded = new Rgb(0.2f, 0.5f, 0.8f).ToLinear();
            Assert.That(decoded.R, Is.EqualTo(0.0331048).Within(2e-6));
            Assert.That(decoded.G, Is.EqualTo(0.2140411).Within(2e-6));
            Assert.That(decoded.B, Is.EqualTo(0.6038273).Within(2e-6));
        }
    }
}
