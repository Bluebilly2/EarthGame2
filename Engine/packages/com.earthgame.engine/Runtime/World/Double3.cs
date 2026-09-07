using System;
using System.Globalization;

namespace EarthGame.Engine
{
    /// <summary>
    /// A double-precision 3D vector in metres, used for positions on a real-scale Earth.
    ///
    /// <para>Float32 has about seven significant digits, so at Earth's radius (6.371e6 m) its representable step
    /// is about half a metre: the planet's surface would quantise to half-metre steps and the camera would
    /// visibly shake. Double gives about 1e-9 m at the same distance, which is why every world position in the
    /// engine is a Double3 and the client's float positions are always relative to a nearby origin.</para>
    ///
    /// <para>Ported from v1 (Assets/EarthGame/Sim/World/Double3.cs) unchanged but for the namespace and
    /// culture-invariant text.</para>
    /// </summary>
    public readonly struct Double3 : IEquatable<Double3>
    {
        public readonly double X;
        public readonly double Y;
        public readonly double Z;

        public Double3(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public static Double3 Zero => new Double3(0.0, 0.0, 0.0);
        public static Double3 Up => new Double3(0.0, 1.0, 0.0);

        public double SqrLength => X * X + Y * Y + Z * Z;
        public double Length => Math.Sqrt(SqrLength);

        /// <summary>Unit vector in the same direction. A zero vector normalises to up, not NaN.</summary>
        public Double3 Normalized
        {
            get
            {
                double len = Length;
                if (len <= 0.0 || double.IsNaN(len) || double.IsInfinity(len))
                    return Up;
                double inv = 1.0 / len;
                return new Double3(X * inv, Y * inv, Z * inv);
            }
        }

        public static Double3 operator +(Double3 a, Double3 b) => new Double3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static Double3 operator -(Double3 a, Double3 b) => new Double3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static Double3 operator -(Double3 a) => new Double3(-a.X, -a.Y, -a.Z);
        public static Double3 operator *(Double3 a, double s) => new Double3(a.X * s, a.Y * s, a.Z * s);
        public static Double3 operator *(double s, Double3 a) => a * s;
        public static Double3 operator /(Double3 a, double s) => new Double3(a.X / s, a.Y / s, a.Z / s);

        public static double Dot(Double3 a, Double3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;

        public static Double3 Cross(Double3 a, Double3 b)
            => new Double3(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);

        public static double Distance(Double3 a, Double3 b) => (a - b).Length;

        /// <summary>
        /// Great-circle surface distance in metres between two directions from the planet centre. Uses the
        /// half-chord (haversine) form, which stays accurate for small separations where the naive acos(dot) form
        /// loses most of its digits, and small separations are exactly what a walking player generates.
        /// </summary>
        public static double GeodesicDistance(Double3 dirA, Double3 dirB, double radius)
        {
            Double3 a = dirA.Normalized;
            Double3 b = dirB.Normalized;
            double halfChord = (a - b).Length * 0.5;
            if (halfChord > 1.0) halfChord = 1.0;
            return 2.0 * radius * Math.Asin(halfChord);
        }

        public bool Equals(Double3 other) => X == other.X && Y == other.Y && Z == other.Z;
        public override bool Equals(object obj) => obj is Double3 other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int h = X.GetHashCode();
                h = (h * 397) ^ Y.GetHashCode();
                h = (h * 397) ^ Z.GetHashCode();
                return h;
            }
        }

        public override string ToString()
            => "(" + X.ToString("F3", CultureInfo.InvariantCulture) + ", " + Y.ToString("F3", CultureInfo.InvariantCulture)
               + ", " + Z.ToString("F3", CultureInfo.InvariantCulture) + ")";
    }
}
