using EarthGame.Engine;
using NUnit.Framework;

namespace EarthGame.Tests.Engine
{
    /// <summary>
    /// The wind at a founder's body (FP.2): the ground's openness read from the terrain by the layer's own rule, floored by
    /// the salt wind near the sea from the shore-distance layer, and the surroundings a world without a weather record gives.
    /// </summary>
    public sealed class SurroundingsTests
    {
        private static readonly Region FixtureRegion = new Region("fixture", "Fixture", Region.Bherwerre.CentreLatitudeDeg,
            Region.Bherwerre.CentreLongitudeDeg, 400.0, 237, 8.0);

        /// <summary>A 400 m square at 10 m cells: flat at 5 m but for a 20 m knoll in the north-west quarter; the sea 50 m off along the east, a kilometre and more to the west.</summary>
        private static WorldState World(bool withShore)
        {
            RegionRaster ground = TestRasters.FromLaw(41, 10.0, 400.0, "wind_ground", (row, col) => row < 12 && col < 12 ? 25f : 5f);
            RegionRaster shore = TestRasters.FromLaw(41, 10.0, 400.0, "wind_shore", (row, col) => (float)(50.0 + (40 - col) * 30.0));
            return new WorldState(1, FixtureRegion, FixtureRegion.WakeClock(), new Heightfield(ground), 0, null, null, null, null, null, null, null,
                                  withShore ? shore : null);
        }

        [Test]
        public void TheKnollIsOpenAndTheFlatBesideItIsNotByTheLayersRule()
        {
            WorldState world = World(false);
            // The knoll's top stands 20 m over the mean of its surroundings: full openness by the layer's twelve metres.
            Assert.That(world.ExposureAt(-150.0, 150.0), Is.EqualTo(1.0).Within(1e-9));
            // The flat far from the knoll, and from the sea by no record: level with its surroundings, no openness at all.
            Assert.That(world.ExposureAt(100.0, -100.0), Is.EqualTo(0.0).Within(1e-9));
        }

        [Test]
        public void TheSaltWindFloorsTheShoreAndFadesAKilometreInland()
        {
            WorldState world = World(true);
            // The east edge is 50 m from the sea: nineteen twentieths of the open's wind, though the ground is flat there.
            Assert.That(world.ExposureAt(190.0, -100.0), Is.EqualTo(1.0 - 50.0 / WorldLayers.CoastWindReachM).Within(0.05));
            // The west edge is 1,250 m from the sea: the floor is gone, and the flat is as sheltered as its ground says.
            Assert.That(world.ExposureAt(-190.0, -150.0), Is.EqualTo(0.0).Within(1e-9));
            // The knoll keeps its own openness whatever the shore says.
            Assert.That(world.ExposureAt(-150.0, 150.0), Is.EqualTo(1.0).Within(1e-9));
            Assert.That(World(false).ExposureAt(190.0, -100.0), Is.EqualTo(0.0).Within(1e-9), "without the layer, the shore blows as a hollow");
        }

        [Test]
        public void AWorldWithoutAWeatherRecordGivesStillAirNoSkyAndNoSun()
        {
            WorldState world = World(true);
            Surroundings s = world.SurroundingsAt(0.0, 5.0, 0.0);
            Assert.That(s.AirC, Is.EqualTo(WorldState.NoWeatherAirC));
            Assert.That(s.WindAtBodyMs, Is.EqualTo(0.0));
            Assert.That(s.SkyView01, Is.EqualTo(0.0));
            Assert.That(s.SolarElevationDeg, Is.LessThan(0.0));
            Warmth body = new Warmth();
            for (int i = 0; i < 120; i++) body.Tick(60.0, s, Exertion.Resting, 1.0, 1.0);
            Assert.That(body.CoreC, Is.EqualTo(Warmth.NormalCoreC).Within(0.3), "two hours in it, the body is near enough where it began: thermoneutral");
            Assert.That(body.SweatRateLPerHour, Is.EqualTo(0.0), "and dry");
        }

        [Test]
        public void TheWorldsOwnWeatherReachesTheBodyWhereItHasARecord()
        {
            WorldState world = new WorldState(1, Region.Bherwerre, WorldClock.FromLocal(Region.Bherwerre.WakeDayOfYear, 23.0, Region.Bherwerre.CentreLongitudeDeg));
            Surroundings s = world.SurroundingsAt(0.0, 2.0, 0.0);
            Assert.That(s.SolarElevationDeg, Is.LessThan(0.0), "eleven at night");
            Assert.That(s.AirC, Is.InRange(0.0, 20.0), "a late-winter night on this coast");
            Assert.That(s.WindAtBodyMs, Is.GreaterThan(0.0));
            Assert.That(s.SkyView01, Is.EqualTo(Warmth.StandingSkyView01));
        }
    }
}
