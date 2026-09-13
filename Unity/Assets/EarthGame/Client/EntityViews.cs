using System.Collections.Generic;
using EarthGame.ClientCore;
using EarthGame.Engine;
using UnityEngine;
using UnityEngine.Rendering;

namespace EarthGame.Client
{
    /// <summary>
    /// The entities the client is shown, drawn (M1.3; since M1.5a from the stand's own meshes, <see cref="ItemLooks"/>,
    /// in the material the loose sticks and cobbles are drawn in). Each is placed where the server said it was a stated
    /// delay ago, between the positions it stated, so a thing let fall is drawn falling as a remote body is drawn walking
    /// rather than stepping twenty times a second, and each is destroyed when the entity is gone. A definition with no
    /// look is drawn as a small magenta cube and logged once: a hole in the table is meant to be seen, and the edit-mode
    /// test is meant to catch it first. An animal is mirrored and not drawn (M1.7a): no cube, and nothing for the crosshair
    /// to meet, until M1.7b gives it a look.
    ///
    /// <para>The crosshair asks what a ray meets (<see cref="Pick"/>) of each thing's own bounds rather than of a
    /// collider: nothing lying is a collider, so a founder never stumbles on a stick the server's ground does not
    /// have.</para>
    ///
    /// <para>A thing that falls and comes to rest is told as <see cref="Landed"/> when it is drawn landing (M1.5c), so the
    /// sound meets the sight rather than the server's word a few ticks ahead of it.</para>
    /// </summary>
    public sealed class EntityViews
    {
        /// <summary>How far the crosshair's catch round a thing is widened, m a side, so a stick two centimetres thick can be looked at.</summary>
        public const float PickMarginM = 0.04f;

        private sealed class Drawn
        {
            public EntityView View;
            public Transform Transform;
            public Bounds Local;
            public float Scale;
            /// <summary>The height a falling thing was first seen at, m; NaN for a thing not falling.</summary>
            public double FellFromUp = double.NaN;
            /// <summary>The tick a fall ended in, until the drawing reaches it; −1 when there is none to tell.</summary>
            public long LandedTick = -1;
        }

        /// <summary>A thing came down (M1.5c): what it is, where it rests, and the energy it came down with, J.</summary>
        public event System.Action<Definition, Vector3, double> Landed;

        private readonly EntityMirror _mirror;
        private readonly Material _material;
        private readonly Dictionary<ulong, Drawn> _drawn = new Dictionary<ulong, Drawn>();
        private readonly HashSet<string> _unbound = new HashSet<string>();
        private Material _unboundMaterial;

        public int Count => _drawn.Count;

        /// <param name="material">The stand's loose material; without one (a device that draws nothing instanced) the things are placed and picked but not drawn.</param>
        public EntityViews(EntityMirror mirror, Material material)
        {
            _mirror = mirror;
            _material = material;
            _mirror.Spawned += OnSpawned;
            _mirror.Updated += OnUpdated;
            _mirror.Gone += OnGone;
            foreach (EntityView view in _mirror.Views.Values) OnSpawned(view);
        }

        public void Dispose()
        {
            _mirror.Spawned -= OnSpawned;
            _mirror.Updated -= OnUpdated;
            _mirror.Gone -= OnGone;
            foreach (Drawn d in _drawn.Values)
                if (d.Transform != null) Object.Destroy(d.Transform.gameObject);
            _drawn.Clear();
            if (_unboundMaterial != null) Object.Destroy(_unboundMaterial);
        }

        private void OnSpawned(EntityView view)
        {
            if (view.Definition.Kind == DefinitionKind.Animal) return;
            if (_drawn.TryGetValue(view.Id.Value, out Drawn old) && old.Transform != null) Object.Destroy(old.Transform.gameObject);
            Drawn d = new Drawn { View = view };
            GameObject go;
            if (ItemLooks.TryLook(view.Definition, view.Id.Value, out Mesh mesh, out float scale))
            {
                go = new GameObject();
                d.Local = mesh.bounds;
                d.Scale = scale;
                if (_material != null)
                {
                    go.AddComponent<MeshFilter>().sharedMesh = mesh;
                    MeshRenderer renderer = go.AddComponent<MeshRenderer>();
                    renderer.sharedMaterial = _material;
                    // As the loose sticks and cobbles are drawn: no shadow, no probe.
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.lightProbeUsage = LightProbeUsage.Off;
                    renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                }
            }
            else
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Collider collider = go.GetComponent<Collider>();
                if (collider != null) Object.Destroy(collider);
                if (_unboundMaterial == null)
                {
                    _unboundMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    _unboundMaterial.SetColor("_BaseColor", Color.magenta);
                }
                go.GetComponent<Renderer>().sharedMaterial = _unboundMaterial;
                d.Local = new Bounds(Vector3.zero, Vector3.one);
                d.Scale = 0.2f;
                if (_unbound.Add(view.Definition.Key)) Debug.LogWarning("[client] no look for " + view.Definition.Key + "; drawing a cube");
            }
            go.name = view.Definition.Key + " " + view.Id;
            go.transform.localScale = Vector3.one * d.Scale;
            d.Transform = go.transform;
            if (view.HasItem && !view.Item.Resting) d.FellFromUp = view.Position.Y;
            _drawn[view.Id.Value] = d;
            Place(d, view.Position);
        }

        /// <summary>A thing's state: one that was falling and is now at rest has landed, in the tick the state names.</summary>
        private void OnUpdated(EntityView view)
        {
            if (!view.HasItem || !_drawn.TryGetValue(view.Id.Value, out Drawn d)) return;
            if (!view.Item.Resting)
            {
                if (double.IsNaN(d.FellFromUp)) d.FellFromUp = view.Position.Y;
                return;
            }
            if (!double.IsNaN(d.FellFromUp) && d.LandedTick < 0) d.LandedTick = view.Tick;
        }

        private void OnGone(EntityView view, byte reason)
        {
            if (_drawn.TryGetValue(view.Id.Value, out Drawn d) && d.Transform != null) Object.Destroy(d.Transform.gameObject);
            _drawn.Remove(view.Id.Value);
        }

        /// <summary>Places every thing where it was at a server tick: the estimated tick less the mirrors' delay, as the other bodies are sampled.</summary>
        public void Draw(double tick)
        {
            foreach (Drawn d in _drawn.Values)
            {
                Place(d, d.View.PositionAt(tick));
                if (d.LandedTick < 0 || tick < d.LandedTick) continue;
                // A fall is free fall (ItemFall), so what it came down with is its weight through the height it fell.
                Double3 rest = d.View.Position;
                double joules = d.View.Definition.MassKg * ItemFall.GravityMps2 * System.Math.Max(0.0, d.FellFromUp - rest.Y);
                d.LandedTick = -1;
                d.FellFromUp = double.NaN;
                Landed?.Invoke(d.View.Definition, new Vector3((float)rest.X, (float)rest.Y, (float)rest.Z), joules);
            }
        }

        private static void Place(Drawn d, Double3 at)
        {
            if (d.Transform == null) return;
            d.Transform.SetPositionAndRotation(new Vector3((float)at.X, (float)at.Y, (float)at.Z), Quaternion.Euler(0f, d.View.YawDeg, 0f));
        }

        /// <summary>
        /// The nearest thing a ray meets within a distance, by the bounds of its own mesh where it is drawn, widened by
        /// <see cref="PickMarginM"/>: the thing, and how far along the ray it was met, m.
        /// </summary>
        public bool Pick(Ray ray, float withinM, out EntityView view, out float distanceM)
        {
            view = null;
            distanceM = withinM;
            foreach (Drawn d in _drawn.Values)
            {
                if (d.Transform == null || !Meets(ray, d.Local, d.Transform.worldToLocalMatrix, out float metres) || metres > distanceM) continue;
                distanceM = metres;
                view = d.View;
            }
            return view != null;
        }

        /// <summary>
        /// Whether a ray meets a mesh's bounds, widened by <see cref="PickMarginM"/>, where a matrix takes the world into the
        /// mesh's own frame, and how far along the ray, m: how the crosshair meets a thing, whether it lies as an entity or
        /// in the litter (M1.5b).
        /// </summary>
        public static bool Meets(Ray ray, Bounds local, Matrix4x4 toLocal, out float metres)
        {
            metres = 0f;
            Vector3 direction = toLocal.MultiplyVector(ray.direction);
            float perMetre = direction.magnitude;
            if (perMetre < 1e-6f) return false;
            Bounds box = local;
            box.Expand(2f * PickMarginM * perMetre);
            if (!box.IntersectRay(new Ray(toLocal.MultiplyPoint3x4(ray.origin), direction / perMetre), out float along)) return false;
            metres = Mathf.Max(0f, along) / perMetre;
            return true;
        }
    }
}
