using System;
using EarthGame.Engine;
using EarthGame.Protocol;
using NUnit.Framework;

namespace EarthGame.Tests.Server
{
    /// <summary>
    /// The weather is the world's and nobody's to send (M1.8a promise 6): a client holding only what its Welcome carried,
    /// the seed, the region and the clock, works out the sky the server's world does, bit for bit, now and later.
    /// </summary>
    public sealed class WeatherFromTheWelcomeTests
    {
        [Test]
        public void AClientWorksOutTheServersSkyFromWhatItsWelcomeCarried()
        {
            WorldState world = new WorldState(90210UL, Region.Bherwerre, WorldClock.FromLocal(240, 5.25, Region.Bherwerre.CentreLongitudeDeg));
            WelcomeMessage sent = default;
            sent.Seed = world.Seed;
            sent.RegionId = world.RegionId;
            sent.TotalHours = world.Clock.TotalHours;
            PacketWriter w = new PacketWriter();
            sent.Write(w);
            PacketReader r = new PacketReader(w.ToArray());
            r.ReadByte();
            WelcomeMessage received = WelcomeMessage.Read(r);

            Region region = Region.ById(received.RegionId);
            Climate climate = Climate.ForRegion(region);
            Synoptic synoptic = new Synoptic(received.Seed);
            foreach (double hoursAhead in new[] { 0.0, 1.5, 23.75, 24.0 * 40.0 })
            {
                Weather server = Weather.At(world.Climate, world.Synoptic,
                    SolarClock.ForRegion(world.Region, new WorldClock(world.Clock.TotalHours + hoursAhead)), 12.0, 0.8);
                Weather client = Weather.At(climate, synoptic,
                    SolarClock.ForRegion(region, new WorldClock(received.TotalHours + hoursAhead)), 12.0, 0.8);
                string at = hoursAhead + " hours on";
                Assert.That(client.AirC, Is.EqualTo(server.AirC), at + ": the air");
                Assert.That(client.AnomalyC, Is.EqualTo(server.AnomalyC), at + ": the cold snap");
                Assert.That(client.WindMs, Is.EqualTo(server.WindMs), at + ": the wind");
                Assert.That(client.WindFromDeg, Is.EqualTo(server.WindFromDeg), at + ": where it blows from");
                Assert.That(client.CloudCover01, Is.EqualTo(server.CloudCover01), at + ": the cloud");
                Assert.That(client.RainRateMmPerHour, Is.EqualTo(server.RainRateMmPerHour), at + ": the rain");
                Assert.That(client.RelativeHumidity01, Is.EqualTo(server.RelativeHumidity01), at + ": the humidity");
            }
        }

        /// <summary>
        /// A world keeps its climate until something asks it about the weather, so a world of a region with no station record
        /// (every test fixture's) is made as before and refused only by what asks.
        /// </summary>
        [Test]
        public void AWorldOfARegionWithNoStationRecordIsRefusedOnlyWhenAskedAboutTheWeather()
        {
            Region fixture = new Region("fixture", "Fixture", Region.Bherwerre.CentreLatitudeDeg, Region.Bherwerre.CentreLongitudeDeg, 1600.0, 237, 8.0);
            WorldState world = null;
            Assert.DoesNotThrow(() => world = new WorldState(7UL, fixture, new WorldClock()));
            Assert.Throws<ArgumentException>(() => { Climate unused = world.Climate; });
            Assert.That(world.Synoptic.Seed, Is.EqualTo(7UL), "the fronts need only the seed");
        }
    }
}
