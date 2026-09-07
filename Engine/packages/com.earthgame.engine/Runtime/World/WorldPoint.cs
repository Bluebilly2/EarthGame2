using System;

namespace EarthGame.Engine
{
    /// <summary>
    /// Which reference frame a position is measured in. Space has no centre, so a position is meaningless
    /// without saying what it is relative to. Only one frame exists today because all gameplay is on Earth's
    /// surface; the seam is built anyway, because retrofitting a coordinate assumption is precisely what cost
    /// v1 its worst week.
    /// </summary>
    public enum FrameId
    {
        /// <summary>Rotates with Earth's surface. Where the founder lives.</summary>
        EarthFixed = 0,
    }

    /// <summary>
    /// A place: a reference frame plus an offset within it, in metres from the planet centre.
    ///
    /// <para>A bare <see cref="Double3"/> is a displacement or a direction, never a place. Keeping the frame welded
    /// to the offset is what stops "position" quietly meaning three different things in three different files.
    /// Ported from v1 (Assets/EarthGame/Sim/World/WorldPoint.cs); the lat/lon conversions now go through
    /// <see cref="GeoMath"/>.</para>
    /// </summary>
    public readonly struct WorldPoint : IEquatable<WorldPoint>
    {
        public readonly FrameId Frame;
        public readonly Double3 Position;

        public WorldPoint(FrameId frame, Double3 position)
        {
            Frame = frame;
            Position = position;
        }

        /// <summary>Distance from the frame's origin, in metres.</summary>
        public double Radius => Position.Length;

        /// <summary>Radial direction. Defaults to +Y exactly at the origin rather than NaN.</summary>
        public Double3 Up => Position.Normalized;

        /// <summary>Altitude above a given sea-level radius, in metres.</summary>
        public double AltitudeAbove(double seaLevelRadius) => Radius - seaLevelRadius;

        public WorldPoint Offset(Double3 delta) => new WorldPoint(Frame, Position + delta);

        /// <summary>
        /// Displacement between two places. Mixing frames is a hard error: silently subtracting coordinates from
        /// different frames yields a plausible number that means nothing.
        /// </summary>
        public static Double3 Displacement(WorldPoint from, WorldPoint to)
        {
            RequireSameFrame(from, to);
            return to.Position - from.Position;
        }

        public static double Distance(WorldPoint a, WorldPoint b)
        {
            RequireSameFrame(a, b);
            return (a.Position - b.Position).Length;
        }

        /// <summary>Great-circle surface distance between two places on a sphere of the given radius.</summary>
        public static double GeodesicDistance(WorldPoint a, WorldPoint b, double radius)
        {
            RequireSameFrame(a, b);
            return Double3.GeodesicDistance(a.Position, b.Position, radius);
        }

        private static void RequireSameFrame(WorldPoint a, WorldPoint b)
        {
            if (a.Frame != b.Frame)
                throw new InvalidOperationException("Cannot combine positions in different frames (" + a.Frame + " and "
                                                    + b.Frame + "). Convert one into the other's frame first.");
        }

        public void ToLatLon(out double latDeg, out double lonDeg)
            => GeoMath.DirectionToLatLon(Position.X, Position.Y, Position.Z, out latDeg, out lonDeg);

        /// <summary>Builds a place from geographic coordinates and an altitude above sea level.</summary>
        public static WorldPoint FromLatLonAltitude(FrameId frame, double latDeg, double lonDeg, double seaLevelRadius, double altitude)
        {
            GeoMath.LatLonToDirection(latDeg, lonDeg, out double x, out double y, out double z);
            return new WorldPoint(frame, new Double3(x, y, z) * (seaLevelRadius + altitude));
        }

        public bool Equals(WorldPoint other) => Frame == other.Frame && Position.Equals(other.Position);
        public override bool Equals(object obj) => obj is WorldPoint other && Equals(other);
        public override int GetHashCode() => ((int)Frame * 397) ^ Position.GetHashCode();
        public override string ToString() => Frame + " " + Position;
    }
}
