using System;
using EarthGame.Engine;
using NUnit.Framework;

namespace EarthGame.Tests.Engine
{
    /// <summary>The region table (CANON rulings 8, 43 and 45): the places the build knows, found by their ids and no other.</summary>
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
            Assert.That(Climate.HasRecordFor(Region.KangarooValley), Is.True, "Nowra's table (WG.2)");
            Assert.That(Climate.ForRegion(Region.KangarooValley).StationElevationM, Is.EqualTo(109.0), "Nowra stands at 109 m");
            Assert.That(Climate.ForRegion(Region.Bherwerre).StationElevationM, Is.EqualTo(85.0), "the lighthouse at 85");
            Assert.That(Climate.HasRecordFor(new Region("nowhere", "Nowhere", 0.0, 0.0, 8000.0, 1, 8.0)), Is.False);
        }

        /// <summary>
        /// The whole valley (CANON ruling 45, WG.2b, 2026-09-23): its own region beside the 8 km box, which stays with the worlds
        /// made in it, and a box that holds the valley rim to rim with room on the far side of each rim to stand and look back.
        /// The places are OpenStreetMap's (Nominatim, 2026-09-23), put through the engine's own frame: a box of 24 or 28 km
        /// leaves the valley's own outline within 3 km of the world's edge, and this one does not.
        /// </summary>
        [Test]
        public void TheWholeValleyHoldsItsFallsAndBothRimsWellInsideItsEdge()
        {
            Region whole = Region.ById("kangaroo-valley-whole");
            Assert.That(whole, Is.SameAs(Region.KangarooValleyWhole));
            Assert.That(whole.CentreLatitudeDeg, Is.EqualTo(-34.705), "the middle of the valley's own outline");
            Assert.That(whole.CentreLongitudeDeg, Is.EqualTo(150.589));
            Assert.That(whole.ExtentM, Is.EqualTo(32000.0), "his choice of the three boxes measured");
            Assert.That(whole.WakeDayOfYear, Is.EqualTo(Region.Bherwerre.WakeDayOfYear), "the same late-winter wake");
            Assert.That(whole.WakeLocalHour, Is.EqualTo(Region.Bherwerre.WakeLocalHour));
            Assert.That(Climate.ForRegion(whole).StationElevationM, Is.EqualTo(109.0), "Nowra's table, as the 8 km valley's");
            Assert.That(Region.ById("kangaroo-valley").ExtentM, Is.EqualTo(8000.0), "the 8 km box stays");

            Assert.That(LeastRoomM(whole, out string nearest), Is.GreaterThanOrEqualTo(3000.0), nearest);
            // The rule tells the boxes apart: the 28 km box recommended beside it, and not chosen, leaves the outline's corners
            // under 2 km from the edge.
            Region recommended = new Region("contrast-28", "the 28 km box", -34.705, 150.589, 28000.0, 237, 8.0);
            Assert.That(LeastRoomM(recommended, out string tight), Is.LessThan(3000.0), tight);
        }

        private static readonly (string Name, double Lat, double Lon)[] ValleyPlaces =
        {
            ("Fitzroy Falls", -34.6480, 150.4825),
            ("Belmore Falls", -34.6398, 150.5591),
            ("Carrington Falls", -34.6238, 150.6549),
            ("Cambewarra Mountain", -34.8002, 150.5774),
            ("the village of Kangaroo Valley", -34.7352, 150.5325),
            ("the valley outline's north-west corner", -34.6151, 150.4571),
            ("the valley outline's south-east corner", -34.7951, 150.7212),
        };

        /// <summary>The least distance, metres, from any of the valley's places to the region's edge, and which place that is.</summary>
        private static double LeastRoomM(Region region, out string which)
        {
            LocalFrame frame = LocalFrame.ForRegion(region);
            double least = double.PositiveInfinity;
            which = "";
            foreach (var place in ValleyPlaces)
            {
                frame.FromLatLon(place.Lat, place.Lon, out double east, out double north);
                double fromEdge = region.HalfExtentM - Math.Max(Math.Abs(east), Math.Abs(north));
                if (fromEdge < least)
                {
                    least = fromEdge;
                    which = place.Name + " at east " + east.ToString("0") + ", north " + north.ToString("0") + " is " + fromEdge.ToString("0")
                            + " m inside " + region.Id + "'s edge";
                }
            }
            return least;
        }
    }
}
