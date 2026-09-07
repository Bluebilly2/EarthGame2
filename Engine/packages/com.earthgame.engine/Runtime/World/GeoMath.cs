using System;

namespace EarthGame.Engine
{
    /// <summary>
    /// Latitude and longitude against the unit sphere, in the one convention the whole project uses, carried from
    /// v1's ElevationGrid and kept here so that <see cref="LocalFrame"/>, <see cref="WorldPoint"/> and the global
    /// grid loader do not each hold a copy: <b>+Y is the north pole, +X is (0° N, 0° E), +Z is (0° N, 90° E)</b>,
    /// and longitude increases from +X towards +Z.
    /// </summary>
    public static class GeoMath
    {
        /// <summary>
        /// Mean radius of the Earth in metres: the sea-level radius every frame and every bake shares. The Python
        /// bake carries the same number (Tools/data/terrarium.py); a fixture test holds the two together.
        /// </summary>
        public const double EarthRadiusM = 6371000.0;

        public const double DegToRad = Math.PI / 180.0;
        public const double RadToDeg = 180.0 / Math.PI;

        /// <summary>Latitude and longitude in degrees to a unit direction from the planet centre.</summary>
        public static void LatLonToDirection(double latDeg, double lonDeg, out double x, out double y, out double z)
        {
            double lat = latDeg * DegToRad;
            double lon = lonDeg * DegToRad;
            double c = Math.Cos(lat);
            x = c * Math.Cos(lon);
            y = Math.Sin(lat);
            z = c * Math.Sin(lon);
        }

        /// <summary>A direction from the planet centre (any length) to latitude and longitude in degrees.</summary>
        public static void DirectionToLatLon(double x, double y, double z, out double latDeg, out double lonDeg)
        {
            double len = Math.Sqrt(x * x + y * y + z * z);
            if (len > 0.0)
            {
                x /= len;
                y /= len;
                z /= len;
            }
            if (y > 1.0) y = 1.0;
            else if (y < -1.0) y = -1.0;
            latDeg = Math.Asin(y) * RadToDeg;
            lonDeg = Math.Atan2(z, x) * RadToDeg;
        }
    }
}
