using System;

namespace EarthGame.Engine
{
    /// <summary>Standing or crouched. The capsule swaps height with the stance.</summary>
    public enum Stance : byte
    {
        Standing = 0,
        Crouching = 1,
    }

    /// <summary>
    /// The founder's body as the mover sees it: where the <b>feet</b> are in local metres (east, up, north), how
    /// fast they are going, and the flags the mover decides. A plain struct so that the client's prediction, the
    /// server's validation and the wire all carry the same eleven numbers.
    /// </summary>
    public struct MoverState
    {
        public double East;
        public double Up;
        public double North;
        public double VelEast;
        public double VelUp;
        public double VelNorth;
        public bool Grounded;
        public bool Wading;
        public Stance Stance;

        public Double3 Feet => new Double3(East, Up, North);
        public Double3 Velocity => new Double3(VelEast, VelUp, VelNorth);
        public double HorizontalSpeed => Math.Sqrt(VelEast * VelEast + VelNorth * VelNorth);

        public static MoverState AtRest(double east, double up, double north)
        {
            MoverState s = default;
            s.East = east;
            s.Up = up;
            s.North = north;
            return s;
        }

        /// <summary>True when every number is finite: the one check that catches a NaN before it becomes a position.</summary>
        public bool IsFinite
            => !(double.IsNaN(East) || double.IsInfinity(East) || double.IsNaN(Up) || double.IsInfinity(Up)
                 || double.IsNaN(North) || double.IsInfinity(North) || double.IsNaN(VelEast) || double.IsInfinity(VelEast)
                 || double.IsNaN(VelUp) || double.IsInfinity(VelUp) || double.IsNaN(VelNorth) || double.IsInfinity(VelNorth));
    }

    /// <summary>
    /// What the founder asks of their body this step: a wish direction on the plane (already turned by the look
    /// yaw, so the mover never knows where the camera points) of length at most one, and the buttons.
    /// </summary>
    public struct MoverInput
    {
        public double WishEast;
        public double WishNorth;
        public bool Jump;
        public bool Sprint;
        public bool Crouch;

        public static MoverInput None => default;

        public static MoverInput Walk(double wishEast, double wishNorth, bool sprint = false)
        {
            MoverInput i = default;
            i.WishEast = wishEast;
            i.WishNorth = wishNorth;
            i.Sprint = sprint;
            return i;
        }

        /// <summary>Length of the wish, after <see cref="Normalise"/> at most one.</summary>
        public double WishLength => Math.Sqrt(WishEast * WishEast + WishNorth * WishNorth);

        /// <summary>Holds the wish inside the unit disc and turns a NaN into no wish at all.</summary>
        public void Normalise()
        {
            if (double.IsNaN(WishEast) || double.IsInfinity(WishEast)) WishEast = 0.0;
            if (double.IsNaN(WishNorth) || double.IsInfinity(WishNorth)) WishNorth = 0.0;
            double len = WishLength;
            if (len > 1.0)
            {
                WishEast /= len;
                WishNorth /= len;
            }
        }
    }

    /// <summary>
    /// The numbers the mover walks by. One instance per world, shared by the client and the server; the owner's
    /// hands decide the feel numbers (CANON ruling 12) and this class is where they will be written down.
    /// </summary>
    public sealed class MoverConfig
    {
        public double CapsuleRadius = 0.35;
        public double StandingHeight = 1.8;
        public double CrouchHeight = 1.2;
        /// <summary>A rise this high in one stride is stepped onto rather than walked into.</summary>
        public double StepHeight = 0.4;
        /// <summary>Ground steeper than this cannot be stood on: the founder slides.</summary>
        public double WalkableSlopeDeg = 45.0;
        /// <summary>How far below the feet the ground may fall in one step before the founder is airborne.</summary>
        public double GroundSnapDistance = 0.3;
        public double Gravity = 9.81;
        /// <summary>Height of a standing jump; the take-off speed is sqrt(2 g h).</summary>
        public double JumpHeight = 0.5;
        /// <summary>How fast, per second, the airborne horizontal velocity eases toward what is asked for. Small: a jump is committed to.</summary>
        public double AirControl = 0.5;
        public double MaxFallSpeed = 50.0;
        public double CrouchSpeedFactor = 0.5;
        /// <summary>Water deeper than this over the ground is wading.</summary>
        public double WadeDepth = 0.4;
        public double WadeSpeedFactor = 0.5;
        /// <summary>Water deeper than this is over the chest: slower still, and no jumping out of it.</summary>
        public double DeepDepth = 1.3;
        public double DeepWadeSpeedFactor = 0.3;
        /// <summary>The body's work capacity fed to <see cref="Locomotion.SpeedMs"/>; physiology owns it later.</summary>
        public double WorkCapacity = 1.0;

        public static readonly MoverConfig Default = new MoverConfig();

        /// <summary>The fastest the mover can move horizontally on any ground: the validator's ceiling.</summary>
        public double MaxHorizontalSpeed => Locomotion.SpeedMs(Locomotion.ToblerPeakSlope, Gait.Running, WorkCapacity);
    }
}
