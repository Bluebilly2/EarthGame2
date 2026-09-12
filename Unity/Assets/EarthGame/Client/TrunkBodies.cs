using System.Collections.Generic;
using EarthGame.ClientCore;
using EarthGame.Engine;
using EarthGame.Shared;
using UnityEngine;

namespace EarthGame.Client
{
    /// <summary>
    /// The trunks a founder can walk into (M1.6b): a pool of capsule colliders on the props layer, lent each frame to the
    /// trees the client holds within <see cref="ReachM"/> of the body and taken back as the founder walks on. Each stands
    /// where <see cref="StandViews"/> draws its tree, from the same tiles and the same placement, and is as thick as the
    /// trunk is drawn where a founder meets it (<see cref="StandForms.TrunkRadiusAt"/>), so a founder is stopped at the
    /// bark. Nothing is drawn here and no tree is an object: at the stand's densities a collider per tree in the nine
    /// tiles would be a hundred thousand of them.
    /// </summary>
    public sealed class TrunkBodies
    {
        /// <summary>How far round the body a trunk is given one, m: much further than a founder covers in a frame.</summary>
        public const double ReachM = 10.0;

        /// <summary>How many trunks have bodies at once; more stand within the reach only where the stand is thicker than any in this region.</summary>
        public const int Most = 64;

        /// <summary>How far up a trunk a founder meets it, m: the radius is taken there, at about the chest.</summary>
        public const double MeetsAtM = 1.2;

        /// <summary>How tall a trunk's body stands, m: over a standing founder and the half metre they jump.</summary>
        public const float StandsM = 4f;

        private readonly GameObject _root;
        private readonly CapsuleCollider[] _bodies;
        private readonly List<TrunkNearby> _near = new List<TrunkNearby>();
        private double _east, _north;
        private int _lent;

        /// <summary>How many trunks have a body now.</summary>
        public int Standing { get; private set; }

        /// <summary>How many trunks stood within the reach when the bodies were last lent, bodies or not.</summary>
        public int Near { get; private set; }

        public TrunkBodies(Transform parent)
        {
            _root = new GameObject("Trunks");
            if (parent != null) _root.transform.SetParent(parent, false);
            _bodies = new CapsuleCollider[Most];
            for (int i = 0; i < _bodies.Length; i++)
            {
                GameObject go = new GameObject("Trunk " + i);
                go.layer = Layers.Props;
                go.transform.SetParent(_root.transform, false);
                _bodies[i] = go.AddComponent<CapsuleCollider>();
                go.SetActive(false);
            }
        }

        /// <summary>
        /// The bodies lent to the trunks nearest a point: the nearest first, so that where more stand within the reach than
        /// there are bodies, the ones a founder could touch have them.
        /// </summary>
        public void Follow(double east, double north, TileReceiver tiles, TileGrid grid)
        {
            _near.Clear();
            TrunksNear.Find(east, north, ReachM, MeetsAtM, tiles, grid, _near);
            Near = _near.Count;
            _east = east;
            _north = north;
            if (_near.Count > _bodies.Length) _near.Sort(Nearest);
            int lent = 0;
            for (int i = 0; i < _bodies.Length; i++)
            {
                CapsuleCollider body = _bodies[i];
                if (i >= _near.Count)
                {
                    if (body.gameObject.activeSelf) body.gameObject.SetActive(false);
                    continue;
                }
                TrunkNearby trunk = _near[i];
                float radius = (float)trunk.RadiusM;
                float height = Mathf.Max(StandsM, 2.1f * radius);
                body.radius = radius;
                body.height = height;
                // The lower cap sits under the ground, so the trunk is its full width from the founder's feet up.
                body.transform.position = new Vector3((float)trunk.East, (float)trunk.Up + height * 0.5f - radius, (float)trunk.North);
                if (!body.gameObject.activeSelf) body.gameObject.SetActive(true);
                lent++;
            }
            Standing = lent;
            // A collider moved outside a physics step is not where PhysX thinks until the transforms are synced, and the
            // mover casts against these in its own step.
            if (lent > 0 || _lent > 0) Physics.SyncTransforms();
            _lent = lent;
        }

        public void Dispose()
        {
            if (_root != null) Object.Destroy(_root);
        }

        private int Nearest(TrunkNearby a, TrunkNearby b) => Distance2(a).CompareTo(Distance2(b));

        private double Distance2(TrunkNearby trunk)
        {
            double dx = trunk.East - _east, dz = trunk.North - _north;
            return dx * dx + dz * dz;
        }
    }
}
