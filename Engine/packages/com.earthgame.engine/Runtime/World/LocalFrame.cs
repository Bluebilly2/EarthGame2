using System;
using System.Globalization;

namespace EarthGame.Engine
{
    /// <summary>
    /// A flat, local coordinate system tangent to the planet at the region centre: east, north and up in plain
    /// metres, with gravity straight down. Axes follow Unity's convention, <b>+X east, +Y up, +Z north</b>.
    ///
    /// <para><b>Why this exists rather than an invented flat world.</b> Standing on Earth the ground is flat: over
    /// a kilometre the surface drops 8 cm from the tangent plane. Building gameplay on a sphere therefore buys
    /// nothing and costs a great deal (floating origins, split cameras, double precision everywhere), so gameplay
    /// runs on the plane. But the plane is anchored to a <b>real place</b>: local coordinates convert to and from
    /// latitude and longitude, so terrain comes from real elevation data and every position still has a true
    /// place on Earth.</para>
    ///
    /// <para><b>The map, decided 2026-09-08.</b> Local (east, north) maps to (latitude, longitude) by the
    /// equirectangular small-angle rule about the centre: a metre north is 1/R degrees of latitude and a metre east
    /// is 1/(R cos φ₀) degrees of longitude, φ₀ the centre latitude. v1 used a spherical displacement instead
    /// (offset along the tangent axes, renormalise); at the corners of an 8 km box the two disagree by about
    /// 1.5 m because the spherical form takes the cosine at the displaced latitude. The bake
    /// (Tools/data/terrarium.py, local_to_lonlat) samples the tiles with the small-angle rule, which makes a
    /// raster cell's (row, column) equal to local (east, north) by definition; the engine adopts the same rule so
    /// that fact has one owner, and a fixture the Python side writes pins the two together.</para>
    ///
    /// <para>History worth keeping: v1's frame once had its east axis pointing west (cross product taken in the
    /// wrong order), and nothing complained because the north axis was flipped back by the next product, so the
    /// world was quietly sampled mirrored. The round-trip and direction tests in CoordinateTests are the reason
    /// that cannot recur unremarked.</para>
    /// </summary>
    public sealed class LocalFrame
    {
        /// <summary>Geographic origin of the plane, degrees, north and east positive.</summary>
        public double OriginLatDeg { get; }
        public double OriginLonDeg { get; }

        /// <summary>Sea-level radius of the planet the plane is tangent to.</summary>
        public double PlanetRadiusM { get; }

        /// <summary>Metres of ground per degree of latitude, and per degree of longitude at the origin's latitude.</summary>
        public double MetresPerDegreeLatitude { get; }
        public double MetresPerDegreeLongitude { get; }

        public LocalFrame(double originLatDeg, double originLonDeg, double planetRadiusM)
        {
            if (Math.Abs(originLatDeg) >= 89.0)
                throw new ArgumentOutOfRangeException(nameof(originLatDeg), "a tangent plane this near a pole has no east");
            OriginLatDeg = originLatDeg;
            OriginLonDeg = originLonDeg;
            PlanetRadiusM = planetRadiusM;
            MetresPerDegreeLatitude = planetRadiusM * GeoMath.DegToRad;
            MetresPerDegreeLongitude = planetRadiusM * Math.Cos(originLatDeg * GeoMath.DegToRad) * GeoMath.DegToRad;
        }

        /// <summary>The frame at a region's centre on the Earth.</summary>
        public static LocalFrame ForRegion(Region region)
            => new LocalFrame(region.CentreLatitudeDeg, region.CentreLongitudeDeg, GeoMath.EarthRadiusM);

        /// <summary>
        /// How far the true surface falls below the tangent plane at a horizontal distance: 8 cm at 1 km, 8 m at
        /// 10 km, 196 m at 50 km, which is why the playable area is bounded.
        /// </summary>
        public double CurvatureDropAt(double horizontalMetres)
        {
            double r = PlanetRadiusM;
            return Math.Sqrt(r * r + horizontalMetres * horizontalMetres) - r;
        }

        /// <summary>
        /// Radius within which the plane is honest to the given tolerance. At 1 m on Earth that is about 3.6 km;
        /// the world should not pretend to be flat much beyond it.
        /// </summary>
        public double ValidRadiusFor(double toleranceMetres)
        {
            double r = PlanetRadiusM;
            double t = r + toleranceMetres;
            return Math.Sqrt(t * t - r * r);
        }

        /// <summary>Latitude and longitude of a point on the plane.</summary>
        public void ToLatLon(double east, double north, out double latDeg, out double lonDeg)
        {
            latDeg = OriginLatDeg + north / MetresPerDegreeLatitude;
            lonDeg = OriginLonDeg + east / MetresPerDegreeLongitude;
        }

        /// <summary>A latitude and longitude to local metres on the plane.</summary>
        public void FromLatLon(double latDeg, double lonDeg, out double east, out double north)
        {
            north = (latDeg - OriginLatDeg) * MetresPerDegreeLatitude;
            double dlon = lonDeg - OriginLonDeg;
            // The sidecar's longitudes never straddle the antimeridian, but a caller's might.
            if (dlon > 180.0) dlon -= 360.0;
            else if (dlon < -180.0) dlon += 360.0;
            east = dlon * MetresPerDegreeLongitude;
        }

        /// <summary>Direction from the planet centre for a point on the plane, ignoring height.</summary>
        public Double3 DirectionAt(double east, double north)
        {
            ToLatLon(east, north, out double lat, out double lon);
            GeoMath.LatLonToDirection(lat, lon, out double x, out double y, out double z);
            return new Double3(x, y, z);
        }

        /// <summary>Local metres (east, up, north) to a real place on the planet.</summary>
        public WorldPoint ToWorld(double east, double up, double north)
            => new WorldPoint(FrameId.EarthFixed, DirectionAt(east, north) * (PlanetRadiusM + up));

        /// <summary>A real place back to local metres. Height comes out as altitude above sea level.</summary>
        public void FromWorld(WorldPoint point, out double east, out double up, out double north)
        {
            point.ToLatLon(out double lat, out double lon);
            FromLatLon(lat, lon, out east, out north);
            up = point.Radius - PlanetRadiusM;
        }

        public override string ToString()
            => "LocalFrame(" + OriginLatDeg.ToString("F3", CultureInfo.InvariantCulture) + ", "
               + OriginLonDeg.ToString("F3", CultureInfo.InvariantCulture) + ")";
    }
}
