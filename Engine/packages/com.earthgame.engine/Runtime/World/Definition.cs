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

    /// <summary>What a thing is made of (BF.1), so a rule asks its properties rather than its key: stone, wood, or nothing that has properties.</summary>
    public enum Substance : byte
    {
        None = 0,
        Stone = 1,
        Wood = 2,
        /// <summary>A strip of bark off a stick (BF.2): tinder and the fibre cord is laid from.</summary>
        Bark = 3,
        /// <summary>Cord laid from strips (BF.2).</summary>
        Cord = 4,
        /// <summary>Fibre strips cut from a tuft (BF.3): the cord is laid from them as from bark.</summary>
        Fibre = 5,
        /// <summary>A bundle of a plant pulled up whole (BF.3): bedding's material.</summary>
        Plant = 6,
        /// <summary>Something a founder could eat (BF.3): a tuber dug up; what is eaten arrives with BF.7.</summary>
        Food = 7,
    }

    /// <summary>
    /// One kind of thing the world can hold, by a stable string key (<c>plant/blackbutt</c>, <c>stone/silcrete</c>,
    /// <c>animal/eastern-grey-kangaroo</c>, <c>item/cobble</c>, <c>player</c>). Whether it is spawnable as an entity
    /// says what the world spawns as its own: the items. A tree stays a layer (M1.6a), and an animal is stood up from
    /// presence rather than spawned (M1.7a). Physics values live here and nowhere else: a client that draws a cobble asks the
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
        /// <summary>What an item is made of (BF.1): the stone of a cobble or a flake, the wood of a stick; none for what is not an item.</summary>
        public Substance Substance { get; }

        internal Definition(string key, DefinitionKind kind, string displayName, bool spawnable, double massKg, double radiusM, object row, Substance substance = Substance.None)
        {
            Key = key;
            Substance = substance;
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

        /// <summary>The density the plain cobble's 0.6 kg is of, kg/m³: a cobble of a named stone weighs by its own.</summary>
        public const double CobbleDensityKgM3 = 2600.0;

        /// <summary>
        /// What a flake's definition says it weighs, kg, for one that has no mass of its own (FP.3). A flake struck off a core
        /// always has its own, the blow's; this is the catalogue's word for the kind, twenty grams, and the mass a flake
        /// restored from before the state was kept would weigh.
        /// </summary>
        public const double FlakeMassKg = 0.02;
        /// <summary>A flake's bounding radius, m: a plate a few centimetres across.</summary>
        public const double FlakeRadiusM = 0.03;

        private static readonly Dictionary<StoneType, Definition> _cobbles = new Dictionary<StoneType, Definition>();
        private static readonly Dictionary<StoneType, Definition> _flakes = new Dictionary<StoneType, Definition>();
        private static readonly Dictionary<PlantSpecies, Definition> _sticks = new Dictionary<PlantSpecies, Definition>();
        private static readonly Dictionary<PlantSpecies, Definition> _barks = new Dictionary<PlantSpecies, Definition>();
        private static readonly Dictionary<PlantSpecies, Definition> _plants = new Dictionary<PlantSpecies, Definition>();
        private static readonly Dictionary<PlantSpecies, Definition> _fibres = new Dictionary<PlantSpecies, Definition>();
        private static readonly Dictionary<PlantSpecies, Definition> _bundles = new Dictionary<PlantSpecies, Definition>();
        private static readonly Dictionary<PlantSpecies, Definition> _tubers = new Dictionary<PlantSpecies, Definition>();
        private static readonly Dictionary<PlantSpecies, Definition> _logs = new Dictionary<PlantSpecies, Definition>();

        /// <summary>A log's kind (BF.3): a felled trunk in two-metre lengths, each with its own thickness and mass; the kind's mass is a small log's.</summary>
        public const double LogMassKg = 30.0;
        public const double LogRadiusM = 0.15;
        /// <summary>A fibre strip's kind (BF.3): fifteen grams dry; a bundle of a pulled plant three hundred.</summary>
        public const double FibreMassKg = 0.015;
        public const double BundleMassKg = 0.3;

        /// <summary>Cord laid from bark strips (BF.2): its length is its own; the kind's mass is a short length's.</summary>
        public static readonly Definition Cord;
        private static readonly Dictionary<AnimalSpecies, Definition> _animals = new Dictionary<AnimalSpecies, Definition>();
        private static readonly List<Definition> _all = new List<Definition>();
        private static readonly List<Definition> _spawnable = new List<Definition>();
        private static readonly Dictionary<string, Definition> _byKey = new Dictionary<string, Definition>(StringComparer.Ordinal);
        private static readonly Dictionary<uint, Definition> _byId = new Dictionary<uint, Definition>();

        static DefinitionCatalogue()
        {
            Player = Add(new Definition(PlayerKey, DefinitionKind.Player, "the founder", false, 0.0, 0.0, null));
            foreach (PlantSpecies species in PlantSpecies.All)
                _plants[species] = Add(new Definition("plant/" + Slug(species.Name), DefinitionKind.Plant, species.DisplayName, false, 0.0, 0.0, species));
            foreach (StoneType stone in StoneType.All)
                Add(new Definition("stone/" + Slug(stone.Name), DefinitionKind.Stone, stone.Name, false, 0.0, 0.0, stone));
            foreach (AnimalSpecies animal in AnimalSpecies.All)
                _animals[animal] = Add(new Definition("animal/" + Slug(animal.Name), DefinitionKind.Animal, animal.DisplayName, false, 0.0, 0.0, animal));
            Cobble = Add(new Definition("item/cobble", DefinitionKind.Item, "a cobble", true, 0.6, 0.05, null, Substance.Stone));
            Stick = Add(new Definition("item/stick", DefinitionKind.Item, "a stick", true, 0.3, 0.02, null, Substance.Wood));
            // A cobble taken up from the ground is of the stone its cell names (M1.5b): the same shape, weighing by its density.
            foreach (StoneType stone in StoneType.All)
            {
                string name = stone.Name.ToLowerInvariant();
                _cobbles[stone] = Add(new Definition("item/cobble-" + Slug(stone.Name), DefinitionKind.Item, (StartsWithVowel(name) ? "an " : "a ") + name + " cobble",
                    true, Cobble.MassKg * stone.DensityKgM3 / CobbleDensityKgM3, Cobble.RadiusM, stone, Substance.Stone));
            }
            // A flake struck off a core is of the core's stone (FP.3): one for every stone, its mass and edge its own once struck.
            foreach (StoneType stone in StoneType.All)
            {
                string name = stone.Name.ToLowerInvariant();
                _flakes[stone] = Add(new Definition("item/flake-" + Slug(stone.Name), DefinitionKind.Item, (StartsWithVowel(name) ? "an " : "a ") + name + " flake",
                    true, FlakeMassKg, FlakeRadiusM, stone, Substance.Stone));
            }
            // A stick taken from under a tree is of that tree (BF.1): the same shape, weighing by its wood against the plain stick's middle density.
            foreach (PlantSpecies species in PlantSpecies.All)
            {
                if (!StandCodes.IsTall(species)) continue;
                Wood wood = Wood.Of(species);
                string name = species.DisplayName;
                _sticks[species] = Add(new Definition("item/stick-" + Slug(species.Name), DefinitionKind.Item, (StartsWithVowel(name) ? "an " : "a ") + name + " stick",
                    true, wood != null ? Stick.MassKg * wood.DensityDryKgM3 / LyingProperties.PlainStickDensityKgM3 : Stick.MassKg, Stick.RadiusM, species, Substance.Wood));
            }
            // A strip of bark comes off a stick of a tree whose bark strips (BF.2): tinder, and the fibre cord is laid from.
            foreach (PlantSpecies species in PlantSpecies.All)
            {
                if (!StandCodes.IsTall(species) || species.StrippableBarkM <= 0.0) continue;
                _barks[species] = Add(new Definition("item/bark-" + Slug(species.Name), DefinitionKind.Item, "a strip of " + species.DisplayName + " bark",
                    true, 0.02, 0.02, species, Substance.Bark));
            }
            Cord = Add(new Definition("item/cord", DefinitionKind.Item, "a cord", true, 0.05, 0.03, null, Substance.Cord));
            // The standing world's yield (BF.3): fibre from the plants that give it, a bundle of any plant hands pull, the tubers
            // the table names, and a felled tree's logs.
            foreach (PlantSpecies species in PlantSpecies.All)
            {
                string name = species.DisplayName;
                if (species.Fibre)
                    _fibres[species] = Add(new Definition("item/fibre-" + Slug(species.Name), DefinitionKind.Item, "a strip of " + name + " fibre",
                        true, FibreMassKg, 0.02, species, Substance.Fibre));
                TuftShape? shape = Tufts.ShapeOf(species);
                if (shape != null)
                    _bundles[species] = Add(new Definition("item/bundle-" + Slug(species.Name), DefinitionKind.Item, "a bundle of " + name,
                        true, BundleMassKg, 0.15, species, Substance.Plant));
                if (species.TuberKg > 0.0)
                    _tubers[species] = Add(new Definition("item/tuber-" + Slug(species.Name), DefinitionKind.Item, (StartsWithVowel(name) ? "an " : "a ") + name + " tuber",
                        true, species.TuberKg, 0.03, species, Substance.Food));
                if (StandCodes.IsTall(species))
                    _logs[species] = Add(new Definition("item/log-" + Slug(species.Name), DefinitionKind.Item, (StartsWithVowel(name) ? "an " : "a ") + name + " log",
                        true, LogMassKg, LogRadiusM, species, Substance.Wood));
            }
        }

        /// <summary>A plant's own definition (BF.3): what a standing trunk or a tuft is named as a work's target.</summary>
        public static Definition PlantOf(PlantSpecies species)
        {
            if (species == null || !_plants.TryGetValue(species, out Definition d)) throw new KeyNotFoundException("no definition for the plant '" + (species == null ? "nothing" : species.Name) + "'");
            return d;
        }

        /// <summary>The fibre strip of a plant that gives fibre (BF.3); one that gives none has no strip.</summary>
        public static Definition FibreOf(PlantSpecies species)
        {
            if (species == null || !_fibres.TryGetValue(species, out Definition d)) throw new KeyNotFoundException("no fibre for the plant '" + (species == null ? "nothing" : species.Name) + "'");
            return d;
        }

        /// <summary>The bundle a plant of the understorey makes when pulled (BF.3); a tree makes none.</summary>
        public static Definition BundleOf(PlantSpecies species)
        {
            if (species == null || !_bundles.TryGetValue(species, out Definition d)) throw new KeyNotFoundException("no bundle for the plant '" + (species == null ? "nothing" : species.Name) + "'");
            return d;
        }

        /// <summary>The tuber a plant with one gives to a dig (BF.3).</summary>
        public static Definition TuberOf(PlantSpecies species)
        {
            if (species == null || !_tubers.TryGetValue(species, out Definition d)) throw new KeyNotFoundException("no tuber for the plant '" + (species == null ? "nothing" : species.Name) + "'");
            return d;
        }

        /// <summary>The log of a tall plant (BF.3), which a felled trunk falls into.</summary>
        public static Definition LogOf(PlantSpecies species)
        {
            if (species == null || !_logs.TryGetValue(species, out Definition d)) throw new KeyNotFoundException("no log for the plant '" + (species == null ? "nothing" : species.Name) + "'");
            return d;
        }

        /// <summary>Whether a definition is a log of some tree (BF.3): wood the hands do not lift.</summary>
        public static bool IsLog(Definition definition) =>
            definition != null && definition.Row is PlantSpecies species && _logs.TryGetValue(species, out Definition log) && ReferenceEquals(log, definition);

        /// <summary>The bark strip of a tall plant whose bark strips (BF.2); a plant whose bark stays on has none.</summary>
        public static Definition BarkOf(PlantSpecies species)
        {
            if (species == null || !_barks.TryGetValue(species, out Definition d)) throw new KeyNotFoundException("no bark strip for the plant '" + (species == null ? "nothing" : species.Name) + "'");
            return d;
        }

        /// <summary>The stick of a tall plant (BF.1), which a stick taken from under it becomes; the plain stick for a plant that stands as no tree, and for none.</summary>
        public static Definition StickOf(PlantSpecies species) => species != null && _sticks.TryGetValue(species, out Definition d) ? d : Stick;

        /// <summary>The wood an item is of (BF.1): a stick of a tree's; null for the plain stick, a stone and anything that is not an item.</summary>
        public static Wood WoodOf(Definition definition) => definition != null && definition.Kind == DefinitionKind.Item ? Wood.Of(definition.Row as PlantSpecies) : null;

        /// <summary>The cobble of a stone, as a thing taken from a cell of it becomes; the plain cobble for a stone this catalogue has none for.</summary>
        public static Definition CobbleOf(StoneType stone) => stone != null && _cobbles.TryGetValue(stone, out Definition d) ? d : Cobble;

        /// <summary>The flake of a stone (FP.3), which a blow on a core of that stone leaves lying; a flake is always of a stone.</summary>
        public static Definition FlakeOf(StoneType stone)
        {
            if (stone == null || !_flakes.TryGetValue(stone, out Definition d)) throw new KeyNotFoundException("no flake for the stone '" + stone + "'");
            return d;
        }

        /// <summary>Whether a definition is a flake of some stone (FP.3).</summary>
        public static bool IsFlake(Definition definition) =>
            definition != null && definition.Row is StoneType stone && _flakes.TryGetValue(stone, out Definition flake) && ReferenceEquals(flake, definition);

        /// <summary>
        /// The stone an item is of (FP.3): a cobble taken from a cell the stone layer names, or a flake; null for a stick, and for
        /// the plain cobble, which is of no stone the country names and so cannot be read for how it fractures.
        /// </summary>
        public static StoneType StoneOf(Definition definition) => definition != null && definition.Kind == DefinitionKind.Item ? definition.Row as StoneType : null;

        /// <summary>The definition of a kind of animal, which an animal stood up from presence is (M1.7a).</summary>
        public static Definition AnimalOf(AnimalSpecies species)
        {
            if (species == null || !_animals.TryGetValue(species, out Definition d)) throw new KeyNotFoundException("no definition for the animal '" + species + "'");
            return d;
        }

        private static bool StartsWithVowel(string word) => word.Length > 0 && "aeiou".IndexOf(word[0]) >= 0;

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

        /// <summary>The definitions an entity may be spawned from in M1; every one needs a look on the client (`ItemLooks`).</summary>
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
