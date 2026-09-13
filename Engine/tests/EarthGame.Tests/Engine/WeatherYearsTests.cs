using System;
using System.Buffers.Binary;
using System.IO;
using EarthGame.Engine;
using NUnit.Framework;

namespace EarthGame.Tests.Engine
{
    /// <summary>
    /// The modelled years `weather_check.py` reads (M1.8a promise 7): the files hold what the sky says at each sample's
    /// instant, in the layout ARCHITECTURE §10 contracts, so a check that reads them by that layout is reading the engine's
    /// weather and not a stray arrangement of it.
    /// </summary>
    public sealed class WeatherYearsTests
    {
        [Test]
        public void TheFilesHoldTheSkyAtTheMiddleOfEachStepInTheContractedLayout()
        {
            string dir = Path.Combine(Path.GetTempPath(), "EarthGame2.Tests", "weather", Guid.NewGuid().ToString("N"));
            try
            {
                const ulong FirstSeed = 41UL;
                const int Seeds = 2, Days = 30, Steps = 4;
                const double AltitudeM = 120.0, Exposure = 0.5;
                WeatherYears.Write(dir, Region.Bherwerre, FirstSeed, Seeds, Days, Steps, AltitudeM, Exposure);

                string sidecar = File.ReadAllText(Path.Combine(dir, "weather.json"));
                Assert.That(sidecar, Does.Contain("{\"format\":\"eg2.weather\",\"version\":1,\"region\":\"bherwerre\",\"latitude_deg\":-35.14,\"longitude_deg\":150.675"));
                Assert.That(sidecar, Does.Contain("\"altitude_m\":120,\"exposure\":0.5,\"first_seed\":41,\"seeds\":2,\"first_local_day\":0,\"days\":30,\"steps_per_day\":4"));
                Assert.That(sidecar, Does.Contain("\"sampled_at\":\"the middle of each step\""));
                Assert.That(sidecar, Does.Contain("\"fields\":[\"air_c\",\"rain_mm_per_hour\"],\"dtype\":\"f32\",\"byte_order\":\"little\",\"raw\":\"weather.f32\"}"));

                byte[] raw = File.ReadAllBytes(Path.Combine(dir, "weather.f32"));
                Assert.That(raw.Length, Is.EqualTo(Seeds * Days * Steps * 2 * 4), "four bytes for each field of each step of each day of each seed");

                Climate climate = Climate.ForRegion(Region.Bherwerre);
                double longitude = Region.Bherwerre.CentreLongitudeDeg;
                int offset = 0, raining = 0;
                for (int s = 0; s < Seeds; s++)
                {
                    Synoptic synoptic = new Synoptic(FirstSeed + (ulong)s);
                    for (int day = 0; day < Days; day++)
                        for (int step = 0; step < Steps; step++)
                        {
                            // The instant restated from the layout: the day of the year counted from 1 January, at the step's middle.
                            WorldClock clock = WorldClock.FromLocal(day + 1, (step + 0.5) * 24.0 / Steps, longitude);
                            Weather sky = Weather.At(climate, synoptic, SolarClock.ForRegion(Region.Bherwerre, clock), AltitudeM, Exposure);
                            float air = BinaryPrimitives.ReadSingleLittleEndian(raw.AsSpan(offset, 4));
                            float rain = BinaryPrimitives.ReadSingleLittleEndian(raw.AsSpan(offset + 4, 4));
                            string at = "seed " + (FirstSeed + (ulong)s) + ", day " + (day + 1) + ", step " + step;
                            Assert.That(air, Is.EqualTo((float)sky.AirC).Within(1e-4f), at + ": the air");
                            Assert.That(rain, Is.EqualTo((float)sky.RainRateMmPerHour).Within(1e-4f), at + ": the rain");
                            if (rain > 0f) raining++;
                            offset += 8;
                        }
                }
                Assert.That(raining, Is.GreaterThan(0), "two months of samples must hold some rain, or the rain's place in the layout went unread");
            }
            finally
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }
        }
    }
}
