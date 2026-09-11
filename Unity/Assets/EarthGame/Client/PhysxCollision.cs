using EarthGame.ClientCore;
using EarthGame.Engine;
using EarthGame.Shared;
using UnityEngine;

namespace EarthGame.Client
{
    /// <summary>
    /// The client's <see cref="IWorldCollision"/>: PhysX casts against the Terrain and prop colliders
    /// (ARCHITECTURE §9). Ground is a ray straight down through the feet; the sweep is a capsule cast. Water is where
    /// the server says it stands (<see cref="StreamedWater"/>, M1.5d): the depth it streams over each tile the client
    /// holds, or the sea at the datum where no depth has come; where no tile is held there is no water. Until M1.5d it
    /// was the sea plane alone, and a lake was dry to the founder's legs. Local metres and Unity metres coincide: +X
    /// east, +Y up, +Z north from the region centre.
    /// </summary>
    public sealed class PhysxCollision : IWorldCollision
    {
        private const float Skin = 0.02f;
        private readonly IHeightSource _water;
        private readonly int _mask;

        /// <param name="water">Where water stands over the ground the client holds, NaN where none does; null for none anywhere.</param>
        public PhysxCollision(IHeightSource water)
        {
            _water = water;
            _mask = Layers.Walkable;
        }

        public bool ProbeGround(Double3 feet, double radius, double stepUp, double maxDown, out double groundUp, out Double3 normal)
        {
            Vector3 origin = new Vector3((float)feet.X, (float)(feet.Y + stepUp + Skin), (float)feet.Z);
            float reach = (float)(stepUp + maxDown) + Skin;
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, reach, _mask, QueryTriggerInteraction.Ignore))
            {
                groundUp = hit.point.y;
                normal = new Double3(hit.normal.x, hit.normal.y, hit.normal.z);
                return true;
            }
            groundUp = 0.0;
            normal = Double3.Up;
            return false;
        }

        public bool SweepCapsule(Double3 feet, double radius, double height, Double3 delta, out double fraction, out Double3 normal)
        {
            fraction = 1.0;
            normal = Double3.Up;
            double length = delta.Length;
            if (length <= 1e-9) return false;
            float r = (float)radius - Skin;
            Vector3 bottom = new Vector3((float)feet.X, (float)(feet.Y + radius + Skin), (float)feet.Z);
            Vector3 top = new Vector3((float)feet.X, (float)(feet.Y + height - radius), (float)feet.Z);
            if (top.y < bottom.y) top = bottom;
            Vector3 dir = new Vector3((float)(delta.X / length), (float)(delta.Y / length), (float)(delta.Z / length));
            if (Physics.CapsuleCast(bottom, top, r, dir, out RaycastHit hit, (float)length + Skin, _mask, QueryTriggerInteraction.Ignore))
            {
                double free = hit.distance - Skin;
                fraction = free <= 0.0 ? 0.0 : (free >= length ? 1.0 : free / length);
                normal = new Double3(hit.normal.x, hit.normal.y, hit.normal.z);
                return fraction < 1.0;
            }
            return false;
        }

        public double WaterSurfaceAt(double east, double north) => _water != null ? _water.HeightAt(east, north) : double.NaN;
    }
}
