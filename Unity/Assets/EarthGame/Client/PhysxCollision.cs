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
        private readonly ClientGround _walk;
        private readonly int _mask;

        /// <param name="water">Where water stands over the ground the client holds, NaN where none does; null for none anywhere.</param>
        /// <param name="walk">The one ground (BF.4), by whose raster's slope a walk on the Terrain is judged; null judges by the Terrain's own triangles.</param>
        public PhysxCollision(IHeightSource water, ClientGround walk = null)
        {
            _water = water;
            _walk = walk;
            _mask = Layers.Walkable;
        }

        public bool ProbeGround(Double3 feet, double radius, double stepUp, double maxDown, out double groundUp, out Double3 normal)
        {
            Vector3 origin = new Vector3((float)feet.X, (float)(feet.Y + stepUp + Skin), (float)feet.Z);
            float reach = (float)(stepUp + maxDown) + Skin;
            bool found = false;
            groundUp = 0.0;
            normal = Double3.Up;
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, reach, _mask, QueryTriggerInteraction.Ignore))
            {
                groundUp = hit.point.y;
                normal = new Double3(hit.normal.x, hit.normal.y, hit.normal.z);
                // On the ground itself the walk is judged by the raster's slope as the Terrain's posts sample it (BF.4), as it was
                // before the relief: the relief below the data moves the feet and the eye, and never makes ground the data calls
                // walkable slide, nor speeds or slows the pace. What stands on the ground (a trunk, a thing) keeps the normal it was met at.
                if (_walk != null && hit.collider is TerrainCollider && _walk.TryWalkNormalAt(hit.point.x, hit.point.z, TerrainTileBuilder.PostSpacingM, out Double3 walk))
                    normal = walk;
                found = true;
            }
            // What stands low on the ground, a rock (BF.4 stage three), is met by the body's round foot, not by a ray down its middle.
            // A body whose side met a low rock was stepped up and let down by the ray alone, which found the ground beside the rock:
            // the body stood inside it, and a capsule cast that starts inside a collider sees nothing, so it walked on through (the
            // rocks scenario's first walks, 2026-09-25). Felt for with the foot, a rock under any part of it is where the body
            // stands, on its top where the face there is one to stand on, and nowhere a steep side lets it stand: the step is not
            // taken, and the body slides along the rock. The Terrain keeps the ray, as it was.
            float foot = (float)radius - Skin;
            Vector3 centre = new Vector3((float)feet.X, (float)(feet.Y + stepUp + radius), (float)feet.Z);
            if (Physics.SphereCast(centre, foot, Vector3.down, out RaycastHit prop, reach, Layers.Mask(Layers.Props), QueryTriggerInteraction.Ignore))
            {
                // The foot's lowest point, where the body's feet are, when it rests on what it met.
                double rests = centre.y - prop.distance - radius;
                if (!found || rests > groundUp)
                {
                    groundUp = rests;
                    normal = new Double3(prop.normal.x, prop.normal.y, prop.normal.z);
                    found = true;
                }
            }
            return found;
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
