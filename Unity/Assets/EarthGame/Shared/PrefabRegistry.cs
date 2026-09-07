using System;
using System.Collections.Generic;
using UnityEngine;

namespace EarthGame.Shared
{
    /// <summary>
    /// Maps an engine definition key (for example "species/blackbutt") to the prefab that draws it. The engine
    /// never knows a prefab exists; the client asks here when the server says an entity does. An edit-mode test
    /// asserts every key is unique and every prefab is set; a later one asserts the registry and the engine's
    /// definition catalogue cover each other exactly (plan §4.4).
    /// </summary>
    [CreateAssetMenu(fileName = "PrefabRegistry", menuName = "EarthGame/Prefab Registry")]
    public sealed class PrefabRegistry : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public string Key;
            public GameObject Prefab;
        }

        [SerializeField] private Entry[] _entries = Array.Empty<Entry>();
        private Dictionary<string, GameObject> _byKey;

        public IReadOnlyList<Entry> Entries => _entries;

        public bool TryGet(string key, out GameObject prefab)
        {
            if (_byKey == null)
            {
                _byKey = new Dictionary<string, GameObject>(StringComparer.Ordinal);
                foreach (Entry e in _entries)
                    if (!string.IsNullOrEmpty(e.Key)) _byKey[e.Key] = e.Prefab;
            }
            return _byKey.TryGetValue(key ?? string.Empty, out prefab);
        }

        /// <summary>Keys that appear more than once, so a test can name them rather than say "duplicate".</summary>
        public List<string> DuplicateKeys()
        {
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            List<string> dupes = new List<string>();
            foreach (Entry e in _entries)
                if (!seen.Add(e.Key ?? string.Empty)) dupes.Add(e.Key ?? "(empty)");
            return dupes;
        }
    }
}
