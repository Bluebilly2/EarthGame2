namespace EarthGame.Engine
{
    /// <summary>
    /// The world as something a capsule can be pushed against. The mover is a pure function over this seam
    /// (ARCHITECTURE §9): the client implements it with PhysX casts against the Terrain collider and prefab
    /// colliders, the server with a heightfield clamp (<see cref="HeightfieldCollision"/>), and the tests with
    /// analytic surfaces. Positions are the <b>feet</b> of the capsule in local metres (east, up, north).
    /// </summary>
    public interface IWorldCollision
    {
        /// <summary>
        /// Looks for ground under the feet, from <paramref name="stepUp"/> above them down to <paramref name="maxDown"/>
        /// below. True with the ground's height and unit normal when there is standable surface in that range.
        /// </summary>
        bool ProbeGround(Double3 feet, double radius, double stepUp, double maxDown, out double groundUp, out Double3 normal);

        /// <summary>
        /// Sweeps a capsule of this radius and height, feet at <paramref name="feet"/>, along <paramref name="delta"/>.
        /// False when the whole motion is free; true with the fraction of it that was free and the unit normal of
        /// what stopped it.
        /// </summary>
        bool SweepCapsule(Double3 feet, double radius, double height, Double3 delta, out double fraction, out Double3 normal);

        /// <summary>Height of the water surface over a place on the plane, or NaN where there is no water.</summary>
        double WaterSurfaceAt(double east, double north);
    }
}
