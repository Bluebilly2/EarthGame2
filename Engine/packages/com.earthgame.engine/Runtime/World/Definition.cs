using System;
using System.Collections.Generic;
using System.Text;

namespace EarthGame.Engine
{
    /// <summary>What kind of thing a definition describes; the first segment of its key.</summary>
    public enum DefinitionKind : byte
    {
        Player = 0,
        Plant = 1,
        Stone = 2,
        Animal = 3,
        Item = 4,
    }

    /// <summary>
    /// A definition's identity on the wire and in the save: FNV-1a 32 of its key, computed once at load and
    /// checked unique across the catalogue (plan §4.4). The key is what people read; the id is what travels.
    /// </summary>
    public readonly struct DefinitionId : IEquatable<DefinitionId>
    {
        public readonly uint Value;

        public DefinitionId(uint value) { Value = value; }

        public static DefinitionId Of(string key) => new DefinitionId(Fnv1a32(key));

        public static uint Fnv1a32(string text)
        {
            const uint Offset = 2166136261u;
            const uint Prime = 16777619u;
            uint hash = Offset;
            byte[] bytes = Encoding.UTF8.GetBytes(text ?? string.Empty);
            for (int i = 0; i < bytes.Length; i++)
            {
                hash ^= bytes[i];
                hash *= Prime;
            }
            return hash;
        }

        public bool Equals(DefinitionId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is DefinitionId other && Equals(other);
        public override int GetHashCode() => (int)Value;
        public override string ToString() => Value.ToString("x8", System.Globalization.CultureInfo.InvariantCulture);
        public static bool operator ==(DefinitionId a, DefinitionId b) => a.Value == b.Value;
        public static bool operator !=(DefinitionId a, DefinitionId b) => a.Value != b.Value;
    }

    /// <summary>
    /// One kind of thing the world can hold, by a stable string key (<c>plant/blackbutt</c>, <c>stone/silcrete</c>,
    /// <c>animal/eastern-grey-kangaroo</c>, <c>item/cobble</c>, <c>player</c>). Whether it is spawnable as an entity
    /// says what M1 materialises (items now; trees and animals when M1.6 and M1.7 land, when their bindings to
    /// prefabs must exist too). Physics values live here and nowhere else: a client that draws a cobble asks the
    /// engine how heavy it is.
    /// </summary>
    public sealed class Definition
    {
        public string Key { get; }
        public DefinitionId Id { get; }
        public DefinitionKind Kind { get; }
        public string DisplayName { get; }
        /// <summary>Whether an entity of this definition can be spawned in M1.</summary>
        public bool Spawnable { get; }
        /// <summary>An item's mass, kg; zero for a definition that is not an item.</summary>
        public double MassKg { get; }
        /// <summary>An item's bounding radius, m; zero for a definition that is not an item.</summary>
        public double RadiusM { get; }
        /// <summary>The table row behind a plant, stone or animal definition (<see cref="PlantSpecies"/>, <see cref="StoneType"/>, <see cref="AnimalSpecies"/>); null otherwise.</summary>
        public object Row { get; }

        internal Definition(string key, DefinitionKind kind, string displayName, bool spawnable, double massKg, double radiusM, object row)
        {
            Key = key;
            Id = DefinitionId.Of(key);
            Kind = kind;
            DisplayName = displayName;
            Spawnable = spawnable;
            MassKg = massKg;
            RadiusM = radiusM;
            Row = row;
        }

        public override string ToString() => Key;
    }

    /// <summary>
    /// Every definition this build knows, built once from the tables that exist (the plants, the stones and the
    /// animals of ECOSYSTEM.md) plus the items and the player, keyed and hashed. Two keys that hash alike, or a
    /// key not in the stated form, refuse to load: a collision found at the first run is a renamed key, one found
    /// on the wire is a founder holding the wrong thing.
    /// </summary>
    public static class DefinitionCatalogue
    {
        public const string PlayerKey = "player";

        /// <summary>The founder. Not spawnable as an entity in M1.3: the body stays a session's until the verbs land (M1.5).</summary>
        public static readonly Definition Player;
        /// <summary>A fist-sized beach cobble: 0.6 kg, 5 cm to its surface. The first thing the founder picks up (M1.5).</summary>
        public static readonly Definition Cobble;
        /// <summary>A metre of fallen branch, two or three centimetres thick: 0.3 kg, 2 cm to its surface.</summary>
        public static readonly Definition Stick;

        private static readonly List<Definition> _all = new List<Definition>();
        private static readonly List<Definition> _spawnable = new List<Definition>();
        private static readonly Dictionary<string, Definition> _byKey = new Dictionary<string, Definition>(StringComparer.Ordinal);
        private static readonly Dictionary<uint, Definition> _byId = new Dictionary<uint, Definition>();

        static DefinitionCatalogue()
        {
            Player = Add(new Definition(PlayerKey, DefinitionKind.Player, "the founder", false, 0.0, 0.0, null));
            foreach (PlantSpecies species in PlantSpecies.All)
                Add(new Definition("plant/" + Slug(species.Name), DefinitionKind.Plant, species.DisplayName, false, 0.0, 0.0, species));
            foreach (StoneType stone in StoneType.All)
                Add(new Definition("stone/" + Slug(stone.Name), DefinitionKind.Stone, stone.Name, false, 0.0, 0.0, stone));
            foreach (AnimalSpecies animal in AnimalSpecies.All)
                Add(new Definition("animal/" + Slug(animal.Name), DefinitionKind.Animal, animal.DisplayName, false, 0.0, 0.0, animal));
            Cobble = Add(new Definition("item/cobble", DefinitionKind.Item, "a cobble", true, 0.6, 0.05, null));
            Stick = Add(new Definition("item/stick", DefinitionKind.Item, "a stick", true, 0.3, 0.02, null));
        }

        private static Definition Add(Definition d)
        {
            if (!IsValidKey(d.Key)) throw new InvalidOperationException("definition key '" + d.Key + "' is not in the stated form (kind/slug)");
            if (_byKey.ContainsKey(d.Key)) throw new InvalidOperationException("definition key '" + d.Key + "' is defined twice");
            if (_byId.TryGetValue(d.Id.Value, out Definition other))
                throw new InvalidOperationException("definition keys '" + d.Key + "' and '" + other.Key + "' hash alike (" + d.Id + "); rename one");
            if (d.Spawnable && d.Kind == DefinitionKind.Item && d.MassKg <= 0.0)
                throw new InvalidOperationException("item '" + d.Key + "' is spawnable and has no mass");
            _all.Add(d);
            if (d.Spawnable) _spawnable.Add(d);
            _byKey[d.Key] = d;
            _byId[d.Id.Value] = d;
            return d;
        }

        /// <summary>Every definition, in catalogue order (the player, the plants, the stones, the animals, the items).</summary>
        public static IReadOnlyList<Definition> All => _all;

        /// <summary>The definitions an entity may be spawned from in M1; every one needs a prefab binding on the client.</summary>
        public static IReadOnlyList<Definition> Spawnable => _spawnable;

        public static Definition ByKey(string key)
        {
            if (!_byKey.TryGetValue(key ?? string.Empty, out Definition d)) throw new KeyNotFoundException("no definition '" + key + "'");
            return d;
        }

        public static bool TryByKey(string key, out Definition definition) => _byKey.TryGetValue(key ?? string.Empty, out definition);

        public static Definition ById(DefinitionId id)
        {
            if (!_byId.TryGetValue(id.Value, out Definition d)) throw new KeyNotFoundException("no definition with id " + id);
            return d;
        }

        public static bool TryById(DefinitionId id, out Definition definition) => _byId.TryGetValue(id.Value, out definition);

        /// <summary>A table name as a key segment: <c>OldManBanksia</c> becomes <c>old-man-banksia</c>; letters, digits and hyphens only.</summary>
        public static string Slug(string name)
        {
            StringBuilder sb = new StringBuilder(name.Length + 4);
            bool lastWasWord = false;
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                if (char.IsUpper(c))
                {
                    if (lastWasWord && i > 0 && (char.IsLower(name[i - 1]) || char.IsDigit(name[i - 1]))) sb.Append('-');
                    sb.Append(char.ToLowerInvariant(c));
                    lastWasWord = true;
                }
                else if (char.IsLetterOrDigit(c))
                {
                    sb.Append(char.ToLowerInvariant(c));
                    lastWasWord = true;
                }
                else
                {
                    if (lastWasWord) sb.Append('-');
                    lastWasWord = false;
                }
            }
            while (sb.Length > 0 && sb[sb.Length - 1] == '-') sb.Length--;
            return sb.ToString();
        }

        /// <summary>The stated form: <c>player</c>, or <c>kind/slug</c> with lower-case letters, digits and single hyphens.</summary>
        public static bool IsValidKey(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;
            if (key == PlayerKey) return true;
            int slash = key.IndexOf('/');
            if (slash <= 0 || slash == key.Length - 1 || key.IndexOf('/', slash + 1) >= 0) return false;
            for (int i = 0; i < slash; i++) if (key[i] < 'a' || key[i] > 'z') return false;
            char previous = '/';
            for (int i = slash + 1; i < key.Length; i++)
            {
                char c = key[i];
                bool word = (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9');
                if (!word && c != '-') return false;
                if (c == '-' && (previous == '-' || previous == '/')) return false;
                previous = c;
            }
            return previous != '-';
        }
    }
}
