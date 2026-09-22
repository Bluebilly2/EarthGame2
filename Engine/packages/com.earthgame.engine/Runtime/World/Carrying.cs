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
        /// <summary>
        /// Strike the stone in hand on another stone (FP.3): one lying within reach, an item or one of the litter, or one held in
        /// another place. The wind-up says how hard; the stone decides the rest (<see cref="Knapping.Strike"/>).
        /// </summary>
        Knap = 5,
        /// <summary>Begin a work (BF.2): its kind on its target, an entity, a lying thing or a place of the hands; the server judges it and runs it over its seconds.</summary>
        Work = 6,
        /// <summary>Let go of the work in progress (BF.2).</summary>
        StopWork = 7,
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
        /// <summary>The blow (FP.3) bounced: not enough behind it to start a fracture. Nothing changed.</summary>
        Bounced = 9,
        /// <summary>The blow (FP.3) took a flake off the core, which now lies where it fell with its own mass and edge.</summary>
        Flaked = 10,
        /// <summary>The blow (FP.3) broke the core up instead of taking a flake: too hard, or too heavy a hammer for what was left.</summary>
        Shattered = 11,
        /// <summary>The blow (FP.3) crushed the edge: a stone that never flakes, a platform gone blunt, or nothing left to hold.</summary>
        Crushed = 12,
        /// <summary>The thing in hand is no stone to strike with (FP.3): a stick, say. An empty hand answers <see cref="NothingInHand"/>.</summary>
        NoHammer = 13,
        /// <summary>The thing aimed at is no stone to knap (FP.3): a stick, or a cobble of no stone the country names.</summary>
        NotStone = 14,
        /// <summary>The hand lacks what the work needs (BF.2): an edge to carve with, a strip to lay cord with. The words say which.</summary>
        NoTool = 15,
        /// <summary>The thing's own properties refuse the work (BF.2): too thick to break, no bark to strip, already pointed. The words say why.</summary>
        WontWork = 16,
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
        /// <summary>
        /// The state a stone earned by being struck (FP.3), kept while it is carried so a flake keeps its edge and a core its
        /// platform through the hands, a save and a load; its rest and fall mean nothing in a hand and are kept at rest.
        /// </summary>
        public ItemComponent Item;
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
            Insert(new CarriedThing { Id = e.Id.Value, Definition = e.Definition, SpawnTick = e.SpawnTick, Place = place, Item = Carried(e.Item) });
            if (!TryAt(Hand, out _)) Hand = place;
            return VerbOutcome.Done;
        }

        /// <summary>A thing's state as the hands keep it: what a blow made of it, at rest.</summary>
        public static ItemComponent Carried(in ItemComponent item)
        {
            ItemComponent kept = item;
            kept.Resting = true;
            kept.FallSpeed = 0f;
            return kept;
        }

        /// <summary>A thing's state as it is let go: what a blow made of it, falling from where it was released.</summary>
        public static ItemComponent LetGo(in ItemComponent item)
        {
            ItemComponent falling = item;
            falling.Resting = false;
            falling.FallSpeed = 0f;
            return falling;
        }

        /// <summary>A held stone's state after a blow on it (FP.3); false when nothing is in that place.</summary>
        public bool TryUpdate(byte place, in ItemComponent item)
        {
            for (int i = 0; i < _things.Count; i++)
            {
                if (_things[i].Place != place) continue;
                CarriedThing thing = _things[i];
                thing.Item = Carried(item);
                _things[i] = thing;
                return true;
            }
            return false;
        }

        /// <summary>A held thing gone for good (FP.3, a core spent under the hammer): the place empties; a hand pointing at it is an empty hand. False when nothing was there.</summary>
        public bool Discard(byte place)
        {
            if (!TryAt(place, out _)) return false;
            Remove(place);
            return true;
        }

        /// <summary>
        /// Takes a thing lying in the loose layer within reach of the eye into the first free place (M1.5b): the world keeps
        /// it as taken, and it becomes an item under an id of its own — a stick a stick, and a cobble one of the stone the
        /// stone layer names on its cell.
        /// </summary>
        public VerbOutcome PickUpLying(WorldState world, LyingThing thing, Double3 eye)
        {
            if (!LyingThings.TryFind(world, thing, out Double3 at)) return VerbOutcome.NotThere;
            LyingSite site = LyingSites.Of(world, thing);
            Definition definition = LyingProperties.DefinitionOf(thing.Kind, site);
            if (Double3.Distance(eye, at) > ReachM + definition.RadiusM) return VerbOutcome.OutOfReach;
            byte place = FreePlace();
            if (place == 0) return VerbOutcome.HandsFull;
            world.Taken.Take(thing);
            // What its place said of it comes with it (BF.1): its tree, its size, its water, the shape it lay in.
            ItemComponent item = default;
            item.Resting = true;
            item.State = LyingProperties.StateOf(thing, site);
            Insert(new CarriedThing { Id = world.Entities.AllocateId(), Definition = definition, SpawnTick = world.Tick, Place = place, Item = item });
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
            e.SetItem(LetGo(thing.Item), world.Tick);
            Remove(thing.Place);
            return VerbOutcome.Done;
        }

        /// <summary>
        /// Everything let go at once where the founder fell (FP.2, the Standard death): each thing returns to the world with
        /// the id it always had, a little above the ground at the point, falling; the hands are empty and no place is the
        /// hand. A thing picked up in this very tick is still in the store and is simply left there.
        /// </summary>
        public void LetGoOfEverything(WorldState world, Double3 at, float yawDeg)
        {
            double ground = world.GroundAt(at.X, at.Z);
            Double3 place = new Double3(at.X, Math.Max(at.Y, ground) + ReleaseM, at.Z);
            for (int i = 0; i < _things.Count; i++)
            {
                CarriedThing thing = _things[i];
                if (world.Entities.TryGet(thing.Id, out _)) continue;
                Entity e = world.Entities.Return(thing.Id, thing.Definition, place, yawDeg, thing.SpawnTick, world.Tick);
                e.SetItem(LetGo(thing.Item), world.Tick);
            }
            _things.Clear();
            Hand = 0;
        }

        /// <summary>Makes a place the hand; 0 empties the hand.</summary>
        public VerbOutcome Hold(byte place)
        {
            if (place > Places) return VerbOutcome.NoSuchPlace;
            Hand = place;
            return VerbOutcome.Done;
        }

        /// <summary>Puts a thing into a place, in place of whatever was there (BF.2: the cord laid where the strip was); the hand takes the place when it had none.</summary>
        public void Put(byte place, CarriedThing thing)
        {
            if (place < 1 || place > Places) throw new ArgumentOutOfRangeException(nameof(place), "a place is 1 to " + Places + ", not " + place);
            if (thing.Definition == null) throw new ArgumentException("a thing put into the hands has no definition", nameof(thing));
            Remove(place);
            thing.Place = place;
            Insert(thing);
            if (Hand == 0) Hand = place;
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
