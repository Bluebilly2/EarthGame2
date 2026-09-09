using System.Collections.Generic;
using EarthGame.ClientCore;
using UnityEngine;

namespace EarthGame.Client
{
    /// <summary>
    /// The entities the client is shown, as objects in the scene (M1.3 promise 7): the registry's prefab for the
    /// definition, placed where the server says and raised by the definition's radius so the mesh rests on the
    /// ground rather than in it, moved on every state, destroyed when told the entity is gone. A definition the
    /// registry does not bind is drawn as a small magenta cube and logged once: a hole in the registry is meant
    /// to be seen, and the edit-mode test is meant to catch it first.
    /// </summary>
    public sealed class EntityViews
    {
        private readonly EntityMirror _mirror;
        private readonly PrefabRegistry _registry;
        private readonly Dictionary<ulong, Transform> _objects = new Dictionary<ulong, Transform>();
        private readonly Dictionary<ulong, Quaternion> _rest = new Dictionary<ulong, Quaternion>();
        private readonly HashSet<string> _unbound = new HashSet<string>();
        private Material _unboundMaterial;

        public int Count => _objects.Count;

        public EntityViews(EntityMirror mirror, PrefabRegistry registry)
        {
            _mirror = mirror;
            _registry = registry;
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
            foreach (Transform t in _objects.Values)
                if (t != null) Object.Destroy(t.gameObject);
            _objects.Clear();
            _rest.Clear();
        }

        private void OnSpawned(EntityView view)
        {
            if (_objects.TryGetValue(view.Id.Value, out Transform old) && old != null) Object.Destroy(old.gameObject);
            GameObject prefab = _registry != null ? _registry.PrefabFor(view.Definition.Key) : null;
            GameObject go;
            if (prefab != null) go = Object.Instantiate(prefab);
            else
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.transform.localScale = Vector3.one * 0.2f;
                Collider collider = go.GetComponent<Collider>();
                if (collider != null) Object.Destroy(collider);
                if (_unboundMaterial == null)
                {
                    _unboundMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    _unboundMaterial.SetColor("_BaseColor", Color.magenta);
                }
                go.GetComponent<Renderer>().sharedMaterial = _unboundMaterial;
                if (_unbound.Add(view.Definition.Key)) Debug.LogWarning("[client] no prefab bound for " + view.Definition.Key + "; drawing a cube");
            }
            go.name = view.Definition.Key + " " + view.Id;
            _objects[view.Id.Value] = go.transform;
            _rest[view.Id.Value] = prefab != null ? prefab.transform.rotation : Quaternion.identity;
            Place(view);
        }

        private void OnUpdated(EntityView view) => Place(view);

        private void OnGone(EntityView view, byte reason)
        {
            if (_objects.TryGetValue(view.Id.Value, out Transform t) && t != null) Object.Destroy(t.gameObject);
            _objects.Remove(view.Id.Value);
            _rest.Remove(view.Id.Value);
        }

        private void Place(EntityView view)
        {
            if (!_objects.TryGetValue(view.Id.Value, out Transform t) || t == null) return;
            t.position = new Vector3((float)view.Position.X, (float)(view.Position.Y + view.Definition.RadiusM), (float)view.Position.Z);
            t.rotation = Quaternion.Euler(0f, view.YawDeg, 0f) * _rest[view.Id.Value];
        }
    }
}
