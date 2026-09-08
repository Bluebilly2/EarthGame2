using EarthGame.Engine;
using EarthGame.Shared;
using UnityEngine;

namespace EarthGame.Client
{
    /// <summary>
    /// The client's <see cref="IWorldCollision"/>: PhysX casts against the Terrain and prop colliders
    /// (ARCHITECTURE §9). Ground is a ray straight down through the feet; the sweep is a capsule cast. Water is
    /// still the sea plane read off the client's copy of the heightfield until the water layer exists. Local
    /// metres and Unity metres coincide: +X east, +Y up, +Z north from the region centre.
    /// </summary>
    public sealed class PhysxCollision : IWorldCollision
    {
        private const float Skin = 0.02f;
        private readonly Heightfield _seaReference;
        private readonly int _mask;

        public PhysxCollision(Heightfield seaReference)
        {
            _seaReference = seaReference;
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

        public double WaterSurfaceAt(double east, double north)
        {
            if (_seaReference == null) return double.NaN;
            return _seaReference.HeightAt(east, north) < 0.0 ? 0.0 : double.NaN;
        }
    }
}
