using EarthGame.Engine;
using NUnit.Framework;

namespace EarthGame.Tests.Engine
{
    /// <summary>The region table (CANON rulings 8 and 43): the two places the build knows, found by their ids and no other.</summary>
    public sealed class RegionTableTests
    {
        [Test]
        public void TheTwoRegionsAreFoundByTheirIdsAndTheValleyIsWhereTheContractPutsIt()
        {
            Assert.That(Region.ById("bherwerre"), Is.SameAs(Region.Bherwerre));
            Assert.That(Region.ById("kangaroo-valley"), Is.SameAs(Region.KangarooValley));
            Assert.That(Region.ById("wilsons-prom"), Is.Null, "measured, not chosen");
            Assert.That(Region.KangarooValley.CentreLatitudeDeg, Is.EqualTo(-34.660));
            Assert.That(Region.KangarooValley.CentreLongitudeDeg, Is.EqualTo(150.500));
            Assert.That(Region.KangarooValley.ExtentM, Is.EqualTo(8000.0));
            Assert.That(Region.KangarooValley.WakeDayOfYear, Is.EqualTo(Region.Bherwerre.WakeDayOfYear), "the same late-winter wake");
            Assert.That(Climate.HasRecordFor(Region.KangarooValley), Is.False, "no station bound until the world is built (WG.2)");
        }
    }
}
