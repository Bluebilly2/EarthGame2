using EarthGame.Engine;
using NUnit.Framework;

namespace EarthGame.Tests.Engine
{
    /// <summary>
    /// The almanac line is what the owner's solar_check reads, so its shape is a contract (ARCHITECTURE §10) and
    /// its numbers must be the same sun SolarClock draws in the sky.
    /// </summary>
    public sealed class AlmanacTests
    {
        [Test]
        public void TheLineCarriesTheFormatAndTheSameSunAsTheClock()
        {
            Region region = Region.Bherwerre;
            AlmanacDay day = Almanac.ForRegion(region, region.WakeDayOfYear);
            string line = Almanac.ToJsonLine(day, region.WakeLocalHour);
            Assert.That(line.Contains("\n"), Is.False, "one line, so a verifier can read it with readline()");

            JsonObject doc = Json.ParseObject(line);
            Assert.That(doc.String("format"), Is.EqualTo("eg2.almanac"));
            Assert.That(doc.Int("version"), Is.EqualTo(1));
            Assert.That(doc.String("time_basis"), Does.Contain("no equation of time"));
            Assert.That(doc.Int("day_of_year"), Is.EqualTo(237));
            Assert.That(doc.Number("latitude_deg"), Is.EqualTo(region.CentreLatitudeDeg).Within(1e-6));
            Assert.That(doc.Number("longitude_deg"), Is.EqualTo(region.CentreLongitudeDeg).Within(1e-6));

            SolarClock wake = SolarClock.AtWake(region);
            Assert.That(doc.Number("declination_deg"), Is.EqualTo(wake.DeclinationDeg).Within(1e-6));
            Assert.That(doc.Number("daylight_hours"), Is.EqualTo(wake.DaylightHours).Within(1e-6));
            Assert.That(doc.Number("sunrise_local_hour"), Is.EqualTo(wake.SunriseHour).Within(1e-6));
            Assert.That(doc.Number("sunset_local_hour"), Is.EqualTo(wake.SunsetHour).Within(1e-6));
            Assert.That(doc.Number("wake_local_hour"), Is.EqualTo(region.WakeLocalHour).Within(1e-6));
            Assert.That(doc.Number("wake_elevation_deg"), Is.EqualTo(wake.SolarElevationDeg).Within(1e-6));
            Assert.That(doc.Number("wake_azimuth_deg"), Is.EqualTo(wake.SolarAzimuthDeg).Within(1e-6));
            Assert.That(doc.Number("noon_elevation_deg"), Is.GreaterThan(doc.Number("wake_elevation_deg")), "the noon sun is higher than the morning sun");
        }

        [Test]
        public void TheLineIsCultureInvariant()
        {
            AlmanacDay day = Almanac.Compute(-35.14, 150.675, 237, 8.0);
            string line = Almanac.ToJsonLine(day, 8.0);
            Assert.That(line.Contains(","), Is.True);
            Assert.That(line.Contains("-35.140000"), Is.True, "a decimal point, never a comma, whatever the machine's locale");
        }
    }
}
