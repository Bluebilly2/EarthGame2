using System;
using System.Collections.Generic;
using EarthGame.Engine;
using EarthGame.Protocol;

namespace EarthGame.ClientCore
{
    /// <summary>An entity as the client last heard of it: the server's fields at the tick they were stated.</summary>
    public sealed class EntityView
    {
        public EntityId Id;
        public Definition Definition;
        public Double3 Position;
        public float YawDeg;
        /// <summary>The server tick of the newest state applied.</summary>
        public long Tick;
        public bool HasItem;
        public ItemComponent Item;
        /// <summary>Whether it is an animal, and its pose (M1.7a).</summary>
        public bool HasAnimal;
        public AnimalComponent Animal;

        private const int Kept = 8;
        private readonly Double3[] _stated = new Double3[Kept];
        private readonly long[] _statedTicks = new long[Kept];
        private int _statedCount;

        /// <summary>
        /// Where the entity was at a server tick (M1.5a): between the two stated positions around it, as a remote body is
        /// drawn, so a thing let fall is drawn falling rather than stepping at the tick rate. Before the first position
        /// held it is where it was first said to be; past the newest, where it is now.
        /// </summary>
        public Double3 PositionAt(double tick)
        {
            if (_statedCount == 0) return Position;
            if (tick <= _statedTicks[0]) return _stated[0];
            int last = _statedCount - 1;
            if (tick >= _statedTicks[last]) return _stated[last];
            for (int i = 1; i <= last; i++)
            {
                if (_statedTicks[i] < tick) continue;
                double span = _statedTicks[i] - _statedTicks[i - 1];
                double t = span > 0 ? (tick - _statedTicks[i - 1]) / span : 1.0;
                Double3 a = _stated[i - 1], b = _stated[i];
                return new Double3(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t, a.Z + (b.Z - a.Z) * t);
            }
            return _stated[last];
        }

        /// <summary>A position the server stated at a tick; a repeat tick replaces the last, and past the few kept the oldest is let go.</summary>
        internal void Stated(Double3 position, long tick)
        {
            if (_statedCount > 0 && tick <= _statedTicks[_statedCount - 1])
            {
                if (tick == _statedTicks[_statedCount - 1]) _stated[_statedCount - 1] = position;
                return;
            }
            if (_statedCount == Kept)
            {
                Array.Copy(_stated, 1, _stated, 0, Kept - 1);
                Array.Copy(_statedTicks, 1, _statedTicks, 0, Kept - 1);
                _statedCount--;
            }
            _stated[_statedCount] = position;
            _statedTicks[_statedCount] = tick;
            _statedCount++;
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
    /// The entities the server has shown this client (M1.3, ARCHITECTURE §7): spawned in full, updated by the
    /// fields that changed, dropped when told they are gone, each keeping the last few positions it was stated at so
    /// it can be drawn between them (M1.5a). A state older than the one held is ignored, since
    /// states travel unreliably and can cross. The digest is computed the same way as the server's for the
    /// session's interest set, so a harness compares two strings.
    /// </summary>
    public sealed class EntityMirror
    {
        private readonly Dictionary<ulong, EntityView> _views = new Dictionary<ulong, EntityView>();
        private readonly List<EntityRecord> _records = new List<EntityRecord>();

        public IReadOnlyDictionary<ulong, EntityView> Views => _views;

        public int Count => _views.Count;

        public event Action<EntityView> Spawned;
        public event Action<EntityView> Updated;
        /// <summary>The view that is gone and why: <see cref="EntityGoneMessage.Died"/>, <see cref="EntityGoneMessage.Left"/> or <see cref="EntityGoneMessage.TakenUp"/>.</summary>
        public event Action<EntityView, byte> Gone;

        /// <summary>A spawn for an id already held replaces it (the server may re-show an entity that left and returned).</summary>
        public void Apply(in EntitySpawnMessage m)
        {
            if (!DefinitionCatalogue.TryById(new DefinitionId(m.DefinitionId), out Definition definition))
                throw new ProtocolException("entity " + m.Id + " has definition " + new DefinitionId(m.DefinitionId) + ", which this build does not know");
            EntityView view = new EntityView
            {
                Id = new EntityId(m.Id),
                Definition = definition,
                Position = new Double3(m.East, m.Up, m.North),
                YawDeg = m.YawDeg,
                Tick = m.ServerTick,
                HasItem = m.HasItem,
                Item = m.Item,
                HasAnimal = m.HasAnimal,
                Animal = m.Animal,
            };
            view.Stated(view.Position, m.ServerTick);
            _views[m.Id] = view;
            Spawned?.Invoke(view);
        }

        /// <summary>A state for an entity not held is dropped (it crossed a gone); an older one is dropped too.</summary>
        public void Apply(in EntityStateMessage m)
        {
            if (!_views.TryGetValue(m.Id, out EntityView view)) return;
            if (m.ServerTick < view.Tick) return;
            if ((m.Fields & EntityFields.Position) != 0)
            {
                view.Position = new Double3(m.East, m.Up, m.North);
                view.Stated(view.Position, m.ServerTick);
            }
            if ((m.Fields & EntityFields.Yaw) != 0) view.YawDeg = m.YawDeg;
            if ((m.Fields & EntityFields.Item) != 0)
            {
                view.HasItem = true;
                view.Item = m.Item;
            }
            if ((m.Fields & EntityFields.Pose) != 0)
            {
                view.HasAnimal = true;
                view.Animal = m.Animal;
            }
            view.Tick = m.ServerTick;
            Updated?.Invoke(view);
        }

        public void Apply(in EntityGoneMessage m)
        {
            if (!_views.TryGetValue(m.Id, out EntityView view)) return;
            _views.Remove(m.Id);
            Gone?.Invoke(view, m.Reason);
        }

        /// <summary>The name of what is held, as the server names a session's interest set.</summary>
        public string Digest()
        {
            _records.Clear();
            foreach (EntityView view in _views.Values) _records.Add(view.Record());
            return WorldDigest.Entities(_records);
        }

        /// <summary>How many held entities lie within a horizontal radius of a point.</summary>
        public int CountWithin(double east, double north, double radiusM)
        {
            double r2 = radiusM * radiusM;
            int n = 0;
            foreach (EntityView view in _views.Values)
            {
                double dx = view.Position.X - east, dz = view.Position.Z - north;
                if (dx * dx + dz * dz <= r2) n++;
            }
            return n;
        }
    }
}
