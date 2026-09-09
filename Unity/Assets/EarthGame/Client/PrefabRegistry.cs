using System;
using System.Collections.Generic;
using EarthGame.Engine;
using UnityEngine;

namespace EarthGame.Client
{
    /// <summary>
    /// Definition key → prefab (plan §4.4, M1.3 promise 7): the one place the client says what a thing looks
    /// like. The engine's <see cref="DefinitionCatalogue"/> says what things exist; an edit-mode test holds the
    /// two together (every spawnable definition bound exactly once, every binding naming a definition), so a
    /// definition that becomes spawnable without a prefab is red in the editor, never a missing object in a
    /// world. Physics values are not here: a prefab is drawn at the size the definition states.
    /// </summary>
    [CreateAssetMenu(fileName = "PrefabRegistry", menuName = "EarthGame/Prefab Registry")]
    public sealed class PrefabRegistry : ScriptableObject
    {
        /// <summary>Where the runtime loads it from: <c>Resources/EarthGame/PrefabRegistry.asset</c>.</summary>
        public const string ResourcePath = "EarthGame/PrefabRegistry";

        [Serializable]
        public struct Binding
        {
            public string Key;
            public GameObject Prefab;
        }

        public List<Binding> Bindings = new List<Binding>();

        /// <summary>The prefab bound to a key, or null when nothing is.</summary>
        public GameObject PrefabFor(string key)
        {
            for (int i = 0; i < Bindings.Count; i++)
                if (string.Equals(Bindings[i].Key, key, StringComparison.Ordinal)) return Bindings[i].Prefab;
            return null;
        }

        /// <summary>Everything wrong with the registry against the catalogue, one line each; empty when it holds.</summary>
        public List<string> Audit()
        {
            List<string> wrong = new List<string>();
            Dictionary<string, int> seen = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < Bindings.Count; i++)
            {
                Binding b = Bindings[i];
                if (string.IsNullOrEmpty(b.Key)) { wrong.Add("binding " + i + " has no key"); continue; }
                seen[b.Key] = seen.TryGetValue(b.Key, out int n) ? n + 1 : 1;
                if (!DefinitionCatalogue.TryByKey(b.Key, out Definition d)) wrong.Add("binding '" + b.Key + "' names no definition");
                else if (!d.Spawnable) wrong.Add("binding '" + b.Key + "' is for a definition that is not spawnable");
                if (b.Prefab == null) wrong.Add("binding '" + b.Key + "' has no prefab");
            }
            foreach (KeyValuePair<string, int> pair in seen)
                if (pair.Value > 1) wrong.Add("'" + pair.Key + "' is bound " + pair.Value + " times");
            foreach (Definition d in DefinitionCatalogue.Spawnable)
                if (!seen.ContainsKey(d.Key)) wrong.Add("spawnable '" + d.Key + "' has no binding");
            return wrong;
        }
    }
}
