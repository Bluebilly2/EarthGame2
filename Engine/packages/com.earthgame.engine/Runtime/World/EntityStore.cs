using System;
using System.Collections.Generic;

namespace EarthGame.Engine
{
    /// <summary>A stable identity for the life of a world: a count, allocated in order, never reused.</summary>
    public readonly struct EntityId : IEquatable<EntityId>, IComparable<EntityId>
    {
        public readonly ulong Value;

        public EntityId(ulong value) { Value = value; }

        public static readonly EntityId None = default;

        /// <summary>
        /// The top bit, which marks an id of the reserved range (M1.7a): an animal stood up from presence takes an id made
        /// from what it is rather than one from the store's counter, so it can be stood up again under the same id and never
        /// moves the count a world saves.
        /// </summary>
        public const ulong TransientBit = 1UL << 63;

        /// <summary>Whether an id is of the reserved range.</summary>
        public static bool IsTransientValue(ulong value) => (value & TransientBit) != 0;

        public bool Equals(EntityId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is EntityId other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public int CompareTo(EntityId other) => Value.CompareTo(other.Value);
        public override string ToString() => "#" + Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        public static bool operator ==(EntityId a, EntityId b) => a.Value == b.Value;
        public static bool operator !=(EntityId a, EntityId b) => a.Value != b.Value;
    }

    /// <summary>The fields of an entity that can change after its spawn, as a mask for the wire and the dirty tracking.</summary>
    [Flags]
    public enum EntityFields : byte
    {
        None = 0,
        Position = 1,
        Yaw = 2,
        Item = 4,
        /// <summary>An animal's pose (M1.7a, protocol 9).</summary>
        Pose = 8,
        All = Position | Yaw | Item | Pose,
    }

    /// <summary>
    /// A thing lying in the world: whether it has come to rest, how fast it is falling while it has not, and since FP.3 the
    /// state a stone earns by being struck. A thing's mass and edge live on its definition until a blow gives it its own:
    /// a flake weighs what the blow took and carries the edge the stone and the blow made, and a core grows lighter with
    /// every flake. Zero means "none of its own", so every stick and cobble written before FP.3 reads as it was.
    /// </summary>
    public struct ItemComponent
    {
        public bool Resting;
        public float FallSpeed;
        /// <summary>The thing's own mass, kg, once a blow has given it one; zero for a thing that weighs what its definition says.</summary>
        public float MassKg;
        /// <summary>The edge a flake carries, 0 to 1; zero for a thing with none.</summary>
        public float Edge01;
        /// <summary>The angle the struck edge of a core presents, degrees; zero for a stone never struck, which presents a fresh cobble's.</summary>
        public float PlatformDeg;
        /// <summary>Flakes taken off a core so far.</summary>
        public ushort FlakesTaken;

        /// <summary>Whether a blow has given this thing state of its own (a flake as struck, a core worked).</summary>
        public bool HasStoneState => MassKg > 0f;
    }

    /// <summary>What an animal is doing (M1.7a), as one of the codes <see cref="AnimalPose"/> names.</summary>
    public struct AnimalComponent
    {
        public byte Pose;
    }

    /// <summary>The poses an animal is shown in, as the wire carries them; zero is none.</summary>
    public static class AnimalPose
    {
        /// <summary>Lying up, outside its hours: a mob at noon.</summary>
        public const byte Resting = 1;

        /// <summary>Up and feeding: a kangaroo grazing a flat, a bird working the tideline.</summary>
        public const byte Grazing = 2;

        /// <summary>Running from a founder (M1.7c, protocol 12): a mob bounding, a pair flying off.</summary>
        public const byte Fleeing = 3;
    }

    /// <summary>An entity as the digest and the wire see it: what the server holds and the mirror repeats.</summary>
    public struct EntityRecord
    {
        public EntityId Id;
        public string Key;
        public Double3 Position;
        public float YawDeg;
        public bool HasItem;
        public ItemComponent Item;
        public bool HasAnimal;
        public AnimalComponent Animal;
    }

    /// <summary>
    /// One thing in the world: a stable id, a definition, a position and yaw in local metres, and plain-struct
    /// components. Every field that can change is stamped with the tick it changed at, so a session that fell
    /// behind on its byte budget is sent what it missed and nothing else: dirtiness is per viewer, read off the
    /// stamps, never a flag cleared for everyone at once.
    /// </summary>
    public sealed class Entity
    {
        public EntityId Id { get; }
        public Definition Definition { get; }
        public long SpawnTick { get; }
        public Double3 Position { get; private set; }
        public float YawDeg { get; private set; }
        public bool HasItem { get; private set; }
        public ItemComponent Item { get; private set; }
        public bool HasAnimal { get; private set; }
        public AnimalComponent Animal { get; private set; }
        /// <summary>The tick each field last changed at; a spawn stamps every field with its tick.</summary>
        public long PositionTick { get; private set; }
        public long YawTick { get; private set; }
        public long ItemTick { get; private set; }
        public long AnimalTick { get; private set; }
        /// <summary>True for an animal stood up from presence (M1.7a): held apart from the world's own entities, never saved or digested.</summary>
        public bool IsTransient => EntityId.IsTransientValue(Id.Value);
        /// <summary>True from the end of the tick after the spawn: the first tick systems see it.</summary>
        public bool Initialised { get; internal set; }
        /// <summary>True from <see cref="EntityStore.Kill"/> until the end of the tick removes it.</summary>
        public bool Killed { get; internal set; }
        /// <summary>True when a founder took it up rather than it dying (<see cref="EntityStore.Take"/>): its viewers are told so (M1.5a).</summary>
        public bool Taken { get; internal set; }

        internal Entity(EntityId id, Definition definition, Double3 position, float yawDeg, long spawnTick)
        {
            Id = id;
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            Position = position;
            YawDeg = yawDeg;
            SpawnTick = spawnTick;
            PositionTick = YawTick = ItemTick = AnimalTick = spawnTick;
        }

        public void Move(Double3 to, long tick)
        {
            Position = to;
            PositionTick = tick;
        }

        public void Turn(float yawDeg, long tick)
        {
            YawDeg = yawDeg;
            YawTick = tick;
        }

        public void SetItem(ItemComponent item, long tick)
        {
            HasItem = true;
            Item = item;
            ItemTick = tick;
        }

        public void SetAnimal(AnimalComponent animal, long tick)
        {
            HasAnimal = true;
            Animal = animal;
            AnimalTick = tick;
        }

        /// <summary>The fields stamped at or after a tick: what a viewer whose last send was that tick has not seen.</summary>
        public EntityFields ChangedSince(long tick)
        {
            EntityFields f = EntityFields.None;
            if (PositionTick >= tick) f |= EntityFields.Position;
            if (YawTick >= tick) f |= EntityFields.Yaw;
            if (HasItem && ItemTick >= tick) f |= EntityFields.Item;
            if (HasAnimal && AnimalTick >= tick) f |= EntityFields.Pose;
            return f;
        }

        public EntityRecord Record()
        {
            EntityRecord r;
            r.Id = Id;
            r.Key = Definition.Key;
            r.Position = Position;
            r.YawDeg = YawDeg;
            r.HasItem = HasItem;
            r.Item = Item;
            r.HasAnimal = HasAnimal;
            r.Animal = Animal;
            return r;
        }
    }

    /// <summary>
    /// The engine's entity store (ARCHITECTURE §5): ids allocated in order and never reused, entities kept in id
    /// order so every walk over them is the same walk, kills applied at the end of the tick so a system never
    /// sees half a removal. The server owns it through <see cref="WorldState.Entities"/>; a client mirrors it.
    /// </summary>
    public sealed class EntityStore
    {
        private readonly List<Entity> _entities = new List<Entity>();
        private readonly Dictionary<ulong, Entity> _byId = new Dictionary<ulong, Entity>();
        private readonly List<Entity> _retired = new List<Entity>();
        private readonly List<Entity> _transient = new List<Entity>();

        /// <summary>The id the next spawn takes. State: saved and restored, so a loaded world never reuses an id.</summary>
        public ulong NextId { get; private set; } = 1;

        public int Count => _entities.Count;

        /// <summary>Every live entity of the world's own, in id order: what a save writes and a digest names.</summary>
        public IReadOnlyList<Entity> All => _entities;

        /// <summary>Every animal stood up from presence, in id order (M1.7a): never saved, never digested.</summary>
        public IReadOnlyList<Entity> Transient => _transient;

        public Entity Spawn(Definition definition, Double3 position, float yawDeg, long tick)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (!definition.Spawnable) throw new ArgumentException("'" + definition.Key + "' is not spawnable as an entity", nameof(definition));
            Entity e = new Entity(new EntityId(NextId), definition, position, yawDeg, tick);
            NextId++;
            _entities.Add(e);
            _byId[e.Id.Value] = e;
            return e;
        }

        /// <summary>An entity as a save recorded it: its own id, kept in order; the next id moves past it.</summary>
        public Entity Restore(ulong id, Definition definition, Double3 position, float yawDeg, long spawnTick)
        {
            if (id == 0) throw new ArgumentException("entity ids start at 1", nameof(id));
            if (EntityId.IsTransientValue(id)) throw new ArgumentException("entity " + id + " is of the reserved range, which is never saved", nameof(id));
            if (_byId.ContainsKey(id)) throw new ArgumentException("entity " + id + " is already in the store", nameof(id));
            Entity e = new Entity(new EntityId(id), definition, position, yawDeg, spawnTick);
            e.Initialised = true;
            int at = _entities.Count;
            while (at > 0 && _entities[at - 1].Id.Value > id) at--;
            _entities.Insert(at, e);
            _byId[id] = e;
            if (id >= NextId) NextId = id + 1;
            return e;
        }

        /// <summary>
        /// Stands up an animal from presence (M1.7a) under an id of the reserved range: held apart from the world's own
        /// entities, in id order, its fields stamped with this tick, and initialised at the end of the tick as a spawn is.
        /// It never moves <see cref="NextId"/>, is never saved and never digested; taken away with <see cref="Kill"/>, it is
        /// retired, and its viewers are told it left.
        /// </summary>
        public Entity StandUp(ulong id, Definition definition, Double3 position, float yawDeg, AnimalComponent animal, long tick)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (!EntityId.IsTransientValue(id)) throw new ArgumentException("an animal stands up under an id of the reserved range, and " + id + " is not", nameof(id));
            if (definition.Kind != DefinitionKind.Animal) throw new ArgumentException("'" + definition.Key + "' is not an animal", nameof(definition));
            if (_byId.ContainsKey(id)) throw new ArgumentException("entity " + id + " is already in the store", nameof(id));
            Entity e = new Entity(new EntityId(id), definition, position, yawDeg, tick);
            e.SetAnimal(animal, tick);
            int at = _transient.Count;
            while (at > 0 && _transient[at - 1].Id.Value > id) at--;
            _transient.Insert(at, e);
            _byId[id] = e;
            return e;
        }

        /// <summary>
        /// An id for a thing that becomes the world's to count without lying in the store (M1.5b): a stick or a cobble of
        /// the loose layer taken straight into a founder's hands. It is never used again, and the thing enters the store
        /// under it when it is put down (<see cref="Return"/>).
        /// </summary>
        public ulong AllocateId() => NextId++;

        /// <summary>The next id as the save recorded it; never below what the entities present already need.</summary>
        public void SetNextId(ulong next)
        {
            ulong needed = _entities.Count == 0 ? 1UL : _entities[_entities.Count - 1].Id.Value + 1;
            if (next < needed) throw new ArgumentException("next id " + next + " would reuse an id; " + needed + " is the least that would not", nameof(next));
            NextId = next;
        }

        public bool TryGet(EntityId id, out Entity entity) => _byId.TryGetValue(id.Value, out entity);

        public bool TryGet(ulong id, out Entity entity) => _byId.TryGetValue(id, out entity);

        /// <summary>Marks an entity for removal at the end of the tick; its id is never used again.</summary>
        public void Kill(Entity entity)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            entity.Killed = true;
        }

        /// <summary>Takes an entity up into a founder's hands (M1.5a): it leaves at the end of the tick as a killed one does, and its viewers are told it was taken.</summary>
        public void Take(Entity entity)
        {
            Kill(entity);
            entity.Taken = true;
        }

        /// <summary>
        /// Returns a thing a founder carried to the world with the id it always had (M1.5a): it takes its place in id
        /// order, its fields stamped with this tick, and is initialised at the end of the tick as a spawn is. An id this
        /// store never allocated, or one still in it, is refused: a return is never a new thing.
        /// </summary>
        public Entity Return(ulong id, Definition definition, Double3 position, float yawDeg, long spawnTick, long tick)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (id == 0 || id >= NextId) throw new ArgumentException("entity " + id + " was never allocated here; the next id is " + NextId, nameof(id));
            if (_byId.ContainsKey(id)) throw new ArgumentException("entity " + id + " is already in the store", nameof(id));
            Entity e = new Entity(new EntityId(id), definition, position, yawDeg, spawnTick);
            e.Move(position, tick);
            e.Turn(yawDeg, tick);
            int at = _entities.Count;
            while (at > 0 && _entities[at - 1].Id.Value > id) at--;
            _entities.Insert(at, e);
            _byId[id] = e;
            return e;
        }

        /// <summary>The end of a tick: the killed leave (kept for <see cref="DrainRetired"/>), the rest are initialised; the world's own and the animals alike.</summary>
        public void EndTick()
        {
            EndTick(_entities);
            EndTick(_transient);
        }

        private void EndTick(List<Entity> entities)
        {
            for (int i = entities.Count - 1; i >= 0; i--)
            {
                Entity e = entities[i];
                if (e.Killed)
                {
                    entities.RemoveAt(i);
                    _byId.Remove(e.Id.Value);
                    _retired.Add(e);
                }
                else e.Initialised = true;
            }
        }

        /// <summary>The entities removed since the last drain, oldest first; the server tells their viewers they died.</summary>
        public int DrainRetired(List<Entity> into)
        {
            int n = _retired.Count;
            if (n > 0)
            {
                _retired.Sort((a, b) => a.Id.CompareTo(b.Id));
                into.AddRange(_retired);
                _retired.Clear();
            }
            return n;
        }

        /// <summary>The live entities within a horizontal radius of a point: the world's own in id order, then the animals in id order.</summary>
        public void Within(double east, double north, double radiusM, List<Entity> into)
        {
            Within(_entities, east, north, radiusM, into);
            Within(_transient, east, north, radiusM, into);
        }

        private static void Within(List<Entity> entities, double east, double north, double radiusM, List<Entity> into)
        {
            double r2 = radiusM * radiusM;
            for (int i = 0; i < entities.Count; i++)
            {
                Entity e = entities[i];
                double dx = e.Position.X - east, dz = e.Position.Z - north;
                if (dx * dx + dz * dz <= r2) into.Add(e);
            }
        }
    }
}
