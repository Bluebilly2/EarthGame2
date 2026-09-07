using System;
using System.Collections.Generic;
using System.IO;
using EarthGame.Engine;
using NUnit.Framework;

namespace EarthGame.Tests.Engine
{
    /// <summary>
    /// The coordinate system, checked against arithmetic. An exact position that is exactly wrong is worse than no
    /// position at all, because everything measured from it inherits the error silently; v1 sampled a mirrored
    /// world for weeks because nothing had ever asked its frame which way east was. Ported from v1's
    /// CoordinateTests and extended with the fixture the Python bake writes, so the two languages' frames are
    /// held together by a file rather than by a shared belief.
    /// </summary>
    public sealed class CoordinateTests
    {
        private static readonly Region Region = Region.Bherwerre;
        private static readonly LocalFrame Frame = LocalFrame.ForRegion(Region);
        private const double R = GeoMath.EarthRadiusM;

        [Test]
        public void TheOriginIsTheRegionCentre()
        {
            Frame.ToLatLon(0.0, 0.0, out double lat, out double lon);
            Assert.That(lat, Is.EqualTo(Region.CentreLatitudeDeg).Within(1e-12), "zero metres from the origin has to be the origin");
            Assert.That(lon, Is.EqualTo(Region.CentreLongitudeDeg).Within(1e-12));
        }

        [Test]
        public void AKilometreNorthIsAKilometreOfLatitude()
        {
            Frame.ToLatLon(0.0, 1000.0, out double lat, out double lon);
            double expected = Region.CentreLatitudeDeg + 1000.0 / R * GeoMath.RadToDeg;
            Assert.That(lat, Is.EqualTo(expected).Within(1e-12), "one kilometre along the surface is 1000/R radians of arc, whatever the latitude");
            // Due north from the wake point in the southern hemisphere is toward the equator, so the latitude gets
            // less negative. Getting this backwards would put the whole world in the wrong place unnoticed.
            Assert.That(lat, Is.GreaterThan(Region.CentreLatitudeDeg), "north is toward the equator from 35 S");
            Assert.That(lon, Is.EqualTo(Region.CentreLongitudeDeg).Within(1e-12), "and it is not east or west of it");
        }

        [Test]
        public void AKilometreEastIsMoreDegreesThanAKilometreNorth()
        {
            Frame.ToLatLon(1000.0, 0.0, out double lat, out double lon);
            // Lines of longitude converge, so a kilometre east at 35 S is more degrees than a kilometre north is,
            // by exactly one over the cosine of the latitude.
            double expected = Region.CentreLongitudeDeg + 1000.0 / (R * Math.Cos(Region.CentreLatitudeDeg * GeoMath.DegToRad)) * GeoMath.RadToDeg;
            Assert.That(lon, Is.EqualTo(expected).Within(1e-12));
            Assert.That(lon, Is.GreaterThan(Region.CentreLongitudeDeg), "east increases longitude");
            Assert.That(lon - Region.CentreLongitudeDeg, Is.GreaterThan(1000.0 / R * GeoMath.RadToDeg), "a degree of longitude is shorter here than a degree of latitude");
            Assert.That(lat, Is.EqualTo(Region.CentreLatitudeDeg).Within(1e-12));
        }

        [Test]
        public void ThePositionRoundTrips()
        {
            // Out to a real place and back again, across the whole region. If the two directions disagree, every
            // distance in the game is quietly wrong.
            foreach (double east in new[] { -4000.0, -1200.0, -37.5, 0.0, 250.0, 1800.0, 4000.0 })
            {
                foreach (double north in new[] { -4000.0, -900.0, 0.0, 64.25, 1500.0, 4000.0 })
                {
                    WorldPoint point = Frame.ToWorld(east, 137.0, north);
                    Frame.FromWorld(point, out double e, out double u, out double n);
                    Assert.That(e, Is.EqualTo(east).Within(1e-6), "east at (" + east + ", " + north + ")");
                    Assert.That(n, Is.EqualTo(north).Within(1e-6), "north at (" + east + ", " + north + ")");
                    Assert.That(u, Is.EqualTo(137.0).Within(1e-6), "up at (" + east + ", " + north + ")");
                }
            }
        }

        [Test]
        public void LatitudeAndLongitudeRoundTripThroughTheFrame()
        {
            Frame.ToLatLon(-2500.0, 3100.0, out double lat, out double lon);
            Frame.FromLatLon(lat, lon, out double east, out double north);
            Assert.That(east, Is.EqualTo(-2500.0).Within(1e-9));
            Assert.That(north, Is.EqualTo(3100.0).Within(1e-9));
        }

        [Test]
        public void EastIsEastOnTheGlobe()
        {
            // The v1 defect, stated so it cannot recur: an east axis that points west. In the project's convention
            // (+Y north pole, longitude increasing from +X towards +Z) east at a point p is cross(p, up).
            Double3 origin = Frame.DirectionAt(0.0, 0.0);
            Double3 eastAxis = Double3.Cross(origin, Double3.Up).Normalized;
            Double3 northAxis = Double3.Cross(eastAxis, origin).Normalized;
            Double3 stepEast = Frame.DirectionAt(1000.0, 0.0) - origin;
            Double3 stepNorth = Frame.DirectionAt(0.0, 1000.0) - origin;
            Assert.That(Double3.Dot(stepEast, eastAxis), Is.GreaterThan(0.0), "a step east must move along the globe's east");
            Assert.That(Double3.Dot(stepNorth, northAxis), Is.GreaterThan(0.0), "a step north must move along the globe's north");
            // A kilometre along a parallel of latitude is a small-circle arc, and its chord leans poleward by half
            // the angular step times sin(latitude): 5.5e-5 rad here. That is the globe, not a defect; the mirror
            // bug this test exists for would show as a sign flip of order one.
            Assert.That(Double3.Dot(stepEast, northAxis) / stepEast.Length, Is.EqualTo(0.0).Within(1e-4), "and east has (almost) no north in it");
            Assert.That(Double3.Dot(stepNorth, eastAxis) / stepNorth.Length, Is.EqualTo(0.0).Within(1e-6), "nor north any east");
        }

        [Test]
        public void AKilometreOnThePlaneIsAKilometreOnTheGlobe()
        {
            double north = Double3.GeodesicDistance(Frame.DirectionAt(0.0, 0.0), Frame.DirectionAt(0.0, 1000.0), R);
            double east = Double3.GeodesicDistance(Frame.DirectionAt(0.0, 0.0), Frame.DirectionAt(1000.0, 0.0), R);
            Assert.That(north, Is.EqualTo(1000.0).Within(0.001), "a kilometre north is a kilometre of arc");
            Assert.That(east, Is.EqualTo(1000.0).Within(0.01), "a kilometre east is a kilometre of arc at the origin's latitude");
        }

        [Test]
        public void TheGroundFallsAwayAtTheRateAPlanetSays()
        {
            // The drop across a chord of length d on a sphere of radius R is about d squared over 2R: 7.8 cm at a
            // kilometre, which is why a flat local frame is honest here and would not be over a hundred.
            Assert.That(Frame.CurvatureDropAt(1000.0), Is.EqualTo(0.0785).Within(0.002));
            Assert.That(Frame.CurvatureDropAt(10000.0), Is.EqualTo(7.85).Within(0.2));
            Assert.That(Frame.CurvatureDropAt(0.0), Is.EqualTo(0.0).Within(1e-9));
        }

        [Test]
        public void TheFrameKnowsHowFarItCanBeTrusted()
        {
            double closeEnough = Frame.ValidRadiusFor(0.10);
            Assert.That(closeEnough, Is.GreaterThan(500.0).And.LessThan(5000.0), "ten centimetres of error is a kilometre or so out, got " + closeEnough.ToString("F0"));
            Assert.That(Frame.ValidRadiusFor(1.0), Is.GreaterThan(Frame.ValidRadiusFor(0.1)), "a looser tolerance reaches further");
        }

        [Test]
        public void MovingEastAndNorthAreIndependent()
        {
            Frame.ToLatLon(2000.0, 0.0, out _, out double lonEastOnly);
            Frame.ToLatLon(0.0, 2000.0, out double latNorthOnly, out _);
            Frame.ToLatLon(2000.0, 2000.0, out double latBoth, out double lonBoth);
            Assert.That(latBoth, Is.EqualTo(latNorthOnly).Within(1e-12), "going east does not change latitude");
            Assert.That(lonBoth, Is.EqualTo(lonEastOnly).Within(1e-12), "and going north does not change longitude");
        }

        [Test]
        public void ANearPolarFrameIsRefused()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new LocalFrame(89.5, 0.0, R));
        }

        /// <summary>
        /// The bake samples the tiles at latitudes and longitudes it computes from local metres with its own copy
        /// of the small-angle rule (Tools/data/terrarium.py). This fixture is that copy's output, written by
        /// Tools/data/write_fixtures.py; the engine must land on the same numbers or a raster cell's (row, column)
        /// stops meaning local (east, north).
        /// </summary>
        [Test]
        public void TheFrameReproducesTheBakesCorners()
        {
            string path = TestPaths.Fixture("frame", "bherwerre_corners.json");
            Assert.That(File.Exists(path), Is.True, "fixture missing: " + path + " (run python Tools/data/write_fixtures.py)");
            JsonObject doc = Json.ParseObject(File.ReadAllText(path));
            Assert.That(doc.String("format"), Is.EqualTo("eg2.frame-fixture"));
            Assert.That(doc.Int("version"), Is.EqualTo(1));
            Assert.That(doc.Number("centre_lat"), Is.EqualTo(Region.CentreLatitudeDeg).Within(1e-12), "the Python tools' default centre and Region.Bherwerre must be one fact");
            Assert.That(doc.Number("centre_lon"), Is.EqualTo(Region.CentreLongitudeDeg).Within(1e-12));
            Assert.That(doc.Number("extent_m"), Is.EqualTo(Region.ExtentM).Within(1e-9));
            Assert.That(doc.Number("earth_radius_m"), Is.EqualTo(R).Within(1e-6));
            List<object> points = doc.Array("points");
            Assert.That(points.Count, Is.GreaterThanOrEqualTo(5));
            foreach (object p in points)
            {
                JsonObject point = (JsonObject)p;
                Frame.ToLatLon(point.Number("east"), point.Number("north"), out double lat, out double lon);
                Assert.That(lat, Is.EqualTo(point.Number("lat")).Within(1e-9), "latitude at east " + point.Number("east") + ", north " + point.Number("north"));
                Assert.That(lon, Is.EqualTo(point.Number("lon")).Within(1e-9), "longitude at east " + point.Number("east") + ", north " + point.Number("north"));
            }
        }
    }
}
