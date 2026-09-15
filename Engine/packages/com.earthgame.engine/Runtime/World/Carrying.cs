using System;
using System.Collections.Generic;

namespace EarthGame.Engine
{
    /// <summary>What a founder's intent asks the world to do (M1.5a). Wire-visible and never renumbered.</summary>
    public enum Verb : byte
    {
        None = 0,
        /// <summary>Take a thing lying within reach into the first free place.</summary>
        PickUp = 1,
        /// <summary>Put the thing in hand down where the founder is looking; it is let go a little above the ground and falls.</summary>
        PutDown = 2,
        /// <summary>Make a place the hand; place 0 is an empty hand.</summary>
        Hold = 3,
        /// <summary>Drink from the water the founder is looking at (FP.1): fresh water gives, the sea refuses with its reason.</summary>
        Drink = 4,
    }

    /// <summary>What came of an intent (M1.5a). Wire-visible and never renumbered.</summary>
    public enum VerbOutcome : byte
    {
        Done = 0,
        OutOfReach = 1,
        NotThere = 2,
        HandsFull = 3,
        NothingInHand = 4,
        NoSuchPlace = 5,
        /// <summary>The founder cannot act yet (no body, the snapshot still owed), the world is held, or the thing is between the world and the hands in this tick.</summary>
        NotNow = 6,
        /// <summary>The water looked at is the sea (FP.1): the sea will not drink, and the reason is salt.</summary>
        Salt = 7,
        /// <summary>Nothing to drink where the founder is looking (FP.1): dry ground, or the wet ground of a swamp.</summary>
        NoWater = 8,
    }

    /// <summary>A thing a founder carries: out of the world, in one of the hands' places, keeping the id it had lying down.</summary>
    public struct CarriedThing
    {
        public ulong Id;
        public Definition Definition;
        /// <summary>The tick it first came into the world, kept so a thing put down is the thing that was picked up.</summary>
        public long SpawnTick;
        /// <summary>1 to <see cref="Hands.Places"/>.</summary>
        public byte Place;
    }

    /// <summary>One founder's hands as the save and the digest see them.</summary>
    public struct CarrierRecord
    {
        public string Name;
        public byte Hand;
        public IReadOnlyList<CarriedThing> Things;
    }

    /// <summary>
    /// A founder's hands (M1.5a): nine places, one to each of the keys 1–9, and one of them the hand. A thing picked up
    /// leaves the world for the first free place, and for the hand when the hand is empty; a thing put down returns to
    /// the world with the id it always had. Every verb is committed here, on the world the server holds, and the reach is
    /// measured from the founder's eye, so what the server allows is what the camera can touch.
    /// </summary>
    public sealed class Hands
    {
        public const int Places = 9;

        /// <summary>How far from the eye a thing can be taken or put, m, a thing's own radius beyond it for a pick-up (v1's reach).</summary>
        public const double ReachM = 4.0;

        /// <summary>How far above the ground a thing put down is let go, m, so it lands rather than appears.</summary>
        public const double ReleaseM = 0.3;

        private readonly List<CarriedThing> _things = new List<CarriedThing>();

        /// <summary>The place that is the hand; 0 is an empty hand no place was chosen for.</summary>
        public byte Hand { get; private set; }

        /// <summary>What is carried, in order of place.</summary>
        public IReadOnlyList<CarriedThing> Things => _things;

        public bool TryAt(byte place, out CarriedThing thing)
        {
            for (int i = 0; i < _things.Count; i++)
            {
                if (_things[i].Place != place) continue;
                thing = _things[i];
                return true;
            }
            thing = default;
            return false;
        }

        /// <summary>The first place nothing is in, or 0 when all of them are full.</summary>
        public byte FreePlace()
        {
            for (byte p = 1; p <= Places; p++)
                if (!TryAt(p, out _)) return p;
            return 0;
        }

        /// <summary>The hands as a save left them; a thing without a place, two in one place, or one without a definition is refused.</summary>
        public void Restore(IEnumerable<CarriedThing> things, byte hand)
        {
            _things.Clear();
            if (things != null)
                foreach (CarriedThing t in things)
                {
                    if (t.Place < 1 || t.Place > Places) throw new ArgumentException("a carried thing's place is 1 to " + Places + ", not " + t.Place, nameof(things));
                    if (TryAt(t.Place, out _)) throw new ArgumentException("two things in place " + t.Place, nameof(things));
                    if (t.Definition == null) throw new ArgumentException("carried thing " + t.Id + " has no definition", nameof(things));
                    Insert(t);
                }
            Hand = hand <= Places ? hand : (byte)0;
        }

        public CarrierRecord Record(string name) => new CarrierRecord { Name = name, Hand = Hand, Things = _things.ToArray() };

        /// <summary>Takes an item lying within reach of the eye into the first free place.</summary>
        public VerbOutcome PickUp(WorldState world, ulong entityId, Double3 eye)
        {
            if (!world.Entities.TryGet(entityId, out Entity e) || e.Killed || !e.HasItem) return VerbOutcome.NotThere;
            if (Double3.Distance(eye, e.Position) > ReachM + e.Definition.RadiusM) return VerbOutcome.OutOfReach;
            byte place = FreePlace();
            if (place == 0) return VerbOutcome.HandsFull;
            world.Entities.Take(e);
            Insert(new CarriedThing { Id = e.Id.Value, Definition = e.Definition, SpawnTick = e.SpawnTick, Place = place });
            if (!TryAt(Hand, out _)) Hand = place;
            return VerbOutcome.Done;
        }

        /// <summary>
        /// Takes a thing lying in the loose layer within reach of the eye into the first free place (M1.5b): the world keeps
        /// it as taken, and it becomes an item under an id of its own — a stick a stick, and a cobble one of the stone the
        /// stone layer names on its cell.
        /// </summary>
        public VerbOutcome PickUpLying(WorldState world, LyingThing thing, Double3 eye)
        {
            if (!LyingThings.TryFind(world, thing, out Double3 at)) return VerbOutcome.NotThere;
            Definition definition = LyingThings.DefinitionOf(world, thing);
            if (Double3.Distance(eye, at) > ReachM + definition.RadiusM) return VerbOutcome.OutOfReach;
            byte place = FreePlace();
            if (place == 0) return VerbOutcome.HandsFull;
            world.Taken.Take(thing);
            Insert(new CarriedThing { Id = world.Entities.AllocateId(), Definition = definition, SpawnTick = world.Tick, Place = place });
            if (!TryAt(Hand, out _)) Hand = place;
            return VerbOutcome.Done;
        }

        /// <summary>Puts the thing in hand down at a point within reach of the eye; it is let go above the ground there and falls.</summary>
        public VerbOutcome PutDown(WorldState world, Double3 at, Double3 eye, float yawDeg)
        {
            if (!TryAt(Hand, out CarriedThing thing)) return VerbOutcome.NothingInHand;
            if (Double3.Distance(eye, at) > ReachM) return VerbOutcome.OutOfReach;
            double half = world.Region.HalfExtentM;
            if (Math.Abs(at.X) > half || Math.Abs(at.Z) > half) return VerbOutcome.OutOfReach;
            // Picked up in this very tick, it is still in the store until the tick ends.
            if (world.Entities.TryGet(thing.Id, out _)) return VerbOutcome.NotNow;
            double ground = world.GroundAt(at.X, at.Z);
            Entity e = world.Entities.Return(thing.Id, thing.Definition, new Double3(at.X, Math.Max(at.Y, ground) + ReleaseM, at.Z), yawDeg, thing.SpawnTick, world.Tick);
            e.SetItem(new ItemComponent { Resting = false, FallSpeed = 0f }, world.Tick);
            Remove(thing.Place);
            return VerbOutcome.Done;
        }

        /// <summary>Makes a place the hand; 0 empties the hand.</summary>
        public VerbOutcome Hold(byte place)
        {
            if (place > Places) return VerbOutcome.NoSuchPlace;
            Hand = place;
            return VerbOutcome.Done;
        }

        private void Insert(CarriedThing thing)
        {
            int at = _things.Count;
            while (at > 0 && _things[at - 1].Place > thing.Place) at--;
            _things.Insert(at, thing);
        }

        private void Remove(byte place)
        {
            for (int i = 0; i < _things.Count; i++)
            {
                if (_things[i].Place != place) continue;
                _things.RemoveAt(i);
                return;
            }
        }
    }
}
