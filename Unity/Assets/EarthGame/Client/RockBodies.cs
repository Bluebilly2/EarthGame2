using System.Collections.Generic;
using EarthGame.Engine;
using EarthGame.Shared;
using UnityEngine;

namespace EarthGame.Client
{
    /// <summary>
    /// The rocks a founder can walk into and stand on (BF.4 stage three): a pool of convex mesh colliders on the props layer,
    /// lent to the rocks the stand views have placed within <see cref="ReachM"/> of the body, nearest first, and lent again
    /// whenever the founder has moved <see cref="RefollowM"/> or the tiles placed have changed. Each is its rock's own shape at
    /// unit half-axes (<see cref="StandMeshes.RockBody"/>), scaled to the rock's, turned to its yaw and set at its middle, as
    /// <see cref="StandViews"/> draws it; so a founder is stopped by the rock drawn and stands on the top the server judges them
    /// by. Nothing is drawn here, and no rock is an object until it is this near.
    /// </summary>
    public sealed class RockBodies
    {
        /// <summary>How far round the body a rock is given one, m: further than a founder covers before the next lending.</summary>
        public const double ReachM = 12.0;

        /// <summary>How many rocks have bodies at once; more stand within the reach only on the thickest boulder fields.</summary>
        public const int Most = 48;

        /// <summary>How far the founder moves before the bodies are lent again, m.</summary>
        public const double RefollowM = 1.0;

        private readonly GameObject _root;
        private readonly MeshCollider[] _bodies;
        private readonly StandingRock[] _lentTo;
        private readonly Dictionary<Collider, int> _which = new Dictionary<Collider, int>();
        private readonly List<StandingRock> _near = new List<StandingRock>();
        private readonly HashSet<Mesh> _baked = new HashSet<Mesh>();
        private double _east = double.NaN, _north = double.NaN;
        private int _version = -1;
        private int _lent;

        /// <summary>How many rocks have a body now.</summary>
        public int Standing { get; private set; }

        public RockBodies(Transform parent)
        {
            _root = new GameObject("Rocks");
            if (parent != null) _root.transform.SetParent(parent, false);
            _bodies = new MeshCollider[Most];
            _lentTo = new StandingRock[Most];
            for (int i = 0; i < _bodies.Length; i++)
            {
                GameObject go = new GameObject("Rock " + i);
                go.layer = Layers.Props;
                go.transform.SetParent(_root.transform, false);
                _bodies[i] = go.AddComponent<MeshCollider>();
                _bodies[i].convex = true;
                _which[_bodies[i]] = i;
                go.SetActive(false);
            }
        }

        /// <summary>The rock a body is lent to now, for the crosshair that met the body; false for a collider that is not one of these or is not lent.</summary>
        public bool TryRockOf(Collider body, out StandingRock rock)
        {
            rock = default;
            if (body == null || !_which.TryGetValue(body, out int i) || i >= _lent || !_bodies[i].gameObject.activeSelf) return false;
            rock = _lentTo[i];
            return true;
        }

        /// <summary>The bodies lent to the rocks nearest a point, from what the stand views have placed.</summary>
        public void Follow(double east, double north, StandViews stand)
        {
            if (stand == null) return;
            if (stand.RocksVersion == _version && System.Math.Abs(east - _east) < RefollowM && System.Math.Abs(north - _north) < RefollowM) return;
            _version = stand.RocksVersion;
            _east = east;
            _north = north;
            _near.Clear();
            stand.RocksNear(east, north, ReachM, _near);
            if (_near.Count > _bodies.Length) _near.Sort(Nearest);
            int lent = 0;
            for (int i = 0; i < _bodies.Length; i++)
            {
                MeshCollider body = _bodies[i];
                if (i >= _near.Count)
                {
                    if (body.gameObject.activeSelf) body.gameObject.SetActive(false);
                    continue;
                }
                StandingRock rock = _near[i];
                _lentTo[i] = rock;
                Mesh mesh = StandMeshes.RockBody(rock.Stone, rock.Form, rock.Variant);
                // Cooked once a shape, whatever the rock it is lent to is scaled to: the physics scales a convex body as it goes.
                if (_baked.Add(mesh)) Physics.BakeMesh(mesh.GetInstanceID(), true);
                if (body.sharedMesh != mesh) body.sharedMesh = mesh;
                Transform t = body.transform;
                t.SetPositionAndRotation(new Vector3((float)rock.East, (float)rock.MidUp, (float)rock.North), Quaternion.Euler(0f, rock.YawDeg, 0f));
                t.localScale = new Vector3((float)rock.HalfWidth, (float)rock.HalfHeight, (float)rock.HalfLength);
                if (!body.gameObject.activeSelf) body.gameObject.SetActive(true);
                lent++;
            }
            Standing = lent;
            // A collider moved outside a physics step is not where PhysX thinks until the transforms are synced, and the mover
            // casts against these in its own step.
            if (lent > 0 || _lent > 0) Physics.SyncTransforms();
            _lent = lent;
        }

        public void Dispose()
        {
            if (_root != null) Object.Destroy(_root);
        }

        private int Nearest(StandingRock a, StandingRock b) => Distance2(a).CompareTo(Distance2(b));

        private double Distance2(StandingRock rock)
        {
            double dx = rock.East - _east, dz = rock.North - _north;
            return dx * dx + dz * dz;
        }
    }
}
