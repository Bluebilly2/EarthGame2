using System;
using System.Collections.Generic;
using EarthGame.ClientCore;
using EarthGame.Engine;
using EarthGame.Protocol;
using EarthGame.Server;
using EarthGame.Transport;
using NUnit.Framework;

namespace EarthGame.Tests.Server
{
    /// <summary>
    /// Work takes time on the server and stops when it should (BF.2 promise 3): a start is judged as the client judged it
    /// and answered with its seconds; the work advances by the step and the body's capacity; it stops when the founder
    /// moves, when the target goes or when the hand changes; and at the end the things promised are in the store, the
    /// hands and the takings, told to every client.
    /// </summary>
    public sealed class WorkServerTests
    {
        private const double Dt = 0.05;
        private const int TickRate = 20;
        private static readonly Region FixtureRegion = new Region("fixture", "Fixture", Region.Bherwerre.CentreLatitudeDeg,
            Region.Bherwerre.CentreLongitudeDeg, 40.0, 237, 8.0);
        private static readonly Definition Bangalay = DefinitionCatalogue.StickOf(PlantSpecies.Bangalay);
        private static readonly Definition Banksia = DefinitionCatalogue.StickOf(PlantSpecies.CoastBanksia);
        private static readonly Definition Flake = DefinitionCatalogue.FlakeOf(StoneType.Silcrete);

        private sealed class Rig
        {
            public GameServer Server;
            public GameClient Client;
            public WorldState World;
            public readonly List<IntentResultMessage> Answers = new List<IntentResultMessage>();
            public readonly List<WorkStateMessage> States = new List<WorkStateMessage>();
            public readonly List<EntityView> Spawned = new List<EntityView>();
            private long _ms;
            public void Pump(int rounds = 1)
            {
                for (int i = 0; i < rounds; i++)
                {
                    Client.Update(_ms);
                    Server.Update(Dt);
                    Client.Update(_ms);
                    _ms += 50;
                }
            }
            public PlayerSession Session => Server.Sessions[0];
        }

        /// <summary>Flat ground at 100 m; bangalay trunks on every cell and three sticks of the litter on cell (2, 2).</summary>
        private static WorldState World()
        {
            RegionRaster ground = TestRasters.FromLaw(5, 10.0, 40.0, "work_ground", (row, col) => 100f);
            RegionRaster loose = TestRasters.FromCodes(5, 10.0, 40.0, "work_loose", "loose", (row, col) => row == 2 && col == 2 ? LooseCodes.Pack(3, 0) : 0u, null);
            RegionRaster stand = TestRasters.FromCodes(5, 10.0, 40.0, "work_stand", "stand", (row, col) => StandCodes.Pack(PlantSpecies.Bangalay, 20.0), null);
            RegionRaster cover = TestRasters.FromCodes(5, 10.0, 40.0, "work_cover", "cover", (row, col) => GroundCovers.Pack(GroundCover.ForestFloor, 0), null);
            return new WorldState(1, FixtureRegion, FixtureRegion.WakeClock(), new Heightfield(ground), 0, null, null, cover, stand, loose, null);
        }

        private static Rig Connect(WorldState world)
        {
            InMemoryTransport.CreatePair(out IServerTransport st, out IClientTransport ct);
            Rig rig = new Rig { World = world };
            rig.Server = new GameServer(new ServerConfig { TickRate = TickRate }, st, world);
            rig.Server.Listen(1);
            rig.Client = new GameClient(ct);
            rig.Client.IntentAnswered += a => rig.Answers.Add(a);
            rig.Client.WorkStateChanged += s => rig.States.Add(s);
            rig.Client.Entities.Spawned += v => rig.Spawned.Add(v);
            rig.Client.Connect("memory", 1, "William", "");
            rig.Pump(6);
            Assert.That(rig.Client.IsInteractive, Is.True);
            world.Clock.Scale = 0.0;
            return rig;
        }

        private static void Stand(Rig rig, double east, double north)
        {
            PlayerSession s = rig.Session;
            s.Body = MoverState.AtRest(east, rig.World.GroundAt(east, north), north);
            s.Body.Grounded = true;
            s.HasBody = true;
            s.StoodUp = s.Body.Up;
            s.YawDeg = 90f;
        }

        private static IntentResultMessage Ask(Rig rig, IntentMessage intent)
        {
            int before = rig.Answers.Count;
            uint sequence = rig.Client.SendIntent(intent);
            rig.Pump(2);
            Assert.That(rig.Answers.Count, Is.EqualTo(before + 1), "one answer to one intent");
            Assert.That(rig.Answers[before].Sequence, Is.EqualTo(sequence));
            return rig.Answers[before];
        }

        private static ThingState Stick(float length, float diameter, float moisture = 0.15f, double density = 900.0)
        {
            ThingState s = default;
            s.SetLength(length);
            s.SetDiameter(diameter);
            s.SetMoisture(moisture);
            s.SetMass((float)(density * Math.PI / 4.0 * diameter * diameter * length * (1.0 + moisture)));
            s.SetLook(1);
            return s;
        }

        private static Entity Spawn(WorldState world, Definition definition, in ThingState state, double east, double north)
        {
            Entity e = world.SpawnItem(definition, east, north);
            ItemComponent item = default;
            item.Resting = true;
            item.State = state;
            e.SetItem(item, world.Tick);
            return e;
        }

        private static IntentMessage WorkOn(WorkKind kind, ulong id) => new IntentMessage { Verb = Verb.Work, Kind = kind, Target = IntentMessage.TargetEntity, EntityId = id };
        private static IntentMessage WorkOnLying(WorkKind kind, LyingThing thing) => new IntentMessage { Verb = Verb.Work, Kind = kind, Target = IntentMessage.TargetLying, Lying = thing };
        private static IntentMessage WorkOnPlace(WorkKind kind, byte place) => new IntentMessage { Verb = Verb.Work, Kind = kind, Target = IntentMessage.TargetPlace, Place = place };
        private static IntentMessage PickUp(ulong id) => new IntentMessage { Verb = Verb.PickUp, Target = IntentMessage.TargetEntity, EntityId = id };

        [Test]
        public void AStartIsJudgedAndAnsweredWithItsSecondsAndTheWorkEndsWithTheThingsMade()
        {
            WorldState world = World();
            Rig rig = Connect(world);
            Stand(rig, 0.0, 0.0);
            Entity stick = Spawn(world, Bangalay, Stick(1.2f, 0.016f), 1.5, 0.0);
            rig.Pump(2);
            IntentResultMessage answer = Ask(rig, WorkOn(WorkKind.Break, stick.Id.Value));
            Assert.That(answer.Outcome, Is.EqualTo(VerbOutcome.Done), "the work started");
            Assert.That(answer.Seconds, Is.EqualTo((float)Work.BreakSeconds));
            Assert.That(answer.Note, Does.Contain("break"));
            Assert.That(rig.Session.Work, Is.Not.Null, "a work in progress");
            Assert.That(rig.Session.Work.Kind, Is.EqualTo(WorkKind.Break));

            int entities = world.Entities.All.Count;
            rig.Pump(2 * TickRate + 2);
            Assert.That(rig.Session.Work, Is.Null, "two seconds of steps at full capacity, and it is done");
            Assert.That(world.Entities.All.Count, Is.EqualTo(entities + 1), "one stick became two");
            Assert.That(world.Entities.TryGet(stick.Id, out _), Is.False, "the whole stick is gone");
            List<Entity> halves = new List<Entity>();
            foreach (Entity e in world.Entities.All) if (ReferenceEquals(e.Definition, Bangalay)) halves.Add(e);
            Assert.That(halves.Count, Is.EqualTo(2));
            Assert.That(halves[0].Item.State.LengthM + halves[1].Item.State.LengthM, Is.EqualTo(1.2f).Within(1e-5f));
            Assert.That(Double3.Distance(halves[0].Position, stick.Position), Is.LessThan(1.0), "where the stick lay");
            WorkStateMessage last = rig.States[rig.States.Count - 1];
            Assert.That(last.Ended, Is.EqualTo(WorkStateMessage.Done));
            Assert.That(last.Note, Does.Contain("two"));
            int seen = 0;
            foreach (EntityView v in rig.Spawned) if (ReferenceEquals(v.Definition, Bangalay)) seen++;
            Assert.That(seen, Is.GreaterThanOrEqualTo(3), "the client was shown the stick and its halves");
        }

        [Test]
        public void TheWorkAdvancesByTheStepAndTheBodysCapacityAndTellsItsProgressOnceASecond()
        {
            WorldState world = World();
            Rig rig = Connect(world);
            Stand(rig, 0.0, 0.0);
            Entity stick = Spawn(world, Bangalay, Stick(1.2f, 0.025f), 1.5, 0.0);
            rig.Pump(2);
            IntentResultMessage answer = Ask(rig, WorkOn(WorkKind.Strip, stick.Id.Value));
            Assert.That(answer.Outcome, Is.EqualTo(VerbOutcome.Done));
            Assert.That(answer.Seconds, Is.EqualTo(1.2f * (float)Work.StripSecondsPerMetre).Within(1e-5f), "7.2 s of stripping");
            rig.Pump(TickRate);
            Assert.That(rig.Session.Work.Progress, Is.EqualTo(1.0).Within(0.15), "a second of steps at full capacity");
            int running = 0;
            foreach (WorkStateMessage s in rig.States) if (s.Ended == WorkStateMessage.Running) running++;
            Assert.That(running, Is.GreaterThanOrEqualTo(1), "told at least once in the second");

            rig.Session.Hydration.Restore(0.90);
            double capacity = rig.Session.Hydration.WorkCapacity01;
            Assert.That(capacity, Is.LessThan(0.9), "a founder who has lost a tenth of their water works slower");
            double before = rig.Session.Work.Progress;
            rig.Pump(TickRate);
            Assert.That(rig.Session.Work.Progress - before, Is.EqualTo(capacity).Within(0.15), "a second of steps at that capacity");
            Assert.That(rig.Session.Work.Progress, Is.LessThan(rig.Session.Work.Seconds), "not done yet");
        }

        [Test]
        public void AWorkStopsWhenTheFounderMovesWhenTheTargetGoesAndWhenTheHandChanges()
        {
            WorldState world = World();
            Rig rig = Connect(world);
            Stand(rig, 0.0, 0.0);
            Entity stick = Spawn(world, Bangalay, Stick(1.2f, 0.025f), 1.5, 0.0);
            rig.Pump(2);
            Assert.That(Ask(rig, WorkOn(WorkKind.Strip, stick.Id.Value)).Outcome, Is.EqualTo(VerbOutcome.Done));
            rig.Pump(4);
            rig.Session.Body = MoverState.AtRest(1.0, world.GroundAt(1.0, 0.0), 0.0);
            rig.Session.Body.Grounded = true;
            rig.Pump(2);
            Assert.That(rig.Session.Work, Is.Null, "a metre away, the work stops");
            Assert.That(rig.States[rig.States.Count - 1].Ended, Is.EqualTo(WorkStateMessage.Stopped));
            Assert.That(rig.States[rig.States.Count - 1].Note, Does.Contain("moved"));
            Assert.That(world.Entities.TryGet(stick.Id, out Entity untouched) && (untouched.Item.State.Marks & ThingMarks.Stripped) == 0, Is.True, "nothing was made of it");

            Stand(rig, 0.0, 0.0);
            rig.Pump(2);
            Assert.That(Ask(rig, WorkOn(WorkKind.Strip, stick.Id.Value)).Outcome, Is.EqualTo(VerbOutcome.Done));
            rig.Pump(4);
            world.Entities.Kill(stick);
            rig.Pump(3);
            Assert.That(rig.Session.Work, Is.Null, "the target went");
            Assert.That(rig.States[rig.States.Count - 1].Ended, Is.EqualTo(WorkStateMessage.Stopped));

            Entity flake = Spawn(world, Flake, FlakeState(0.6f), 1.5, 0.5);
            Entity other = Spawn(world, Banksia, Stick(0.9f, 0.02f), 1.5, -0.5);
            rig.Pump(2);
            Assert.That(Ask(rig, PickUp(flake.Id.Value)).Outcome, Is.EqualTo(VerbOutcome.Done));
            Assert.That(Ask(rig, WorkOn(WorkKind.Point, other.Id.Value)).Outcome, Is.EqualTo(VerbOutcome.Done), "an edge in hand points a banksia stick");
            rig.Pump(4);
            Assert.That(Ask(rig, new IntentMessage { Verb = Verb.Hold, Place = 0 }).Outcome, Is.EqualTo(VerbOutcome.Done), "the hand emptied");
            rig.Pump(2);
            Assert.That(rig.Session.Work, Is.Null, "the hand changed");

            Assert.That(Ask(rig, new IntentMessage { Verb = Verb.Hold, Place = 1 }).Outcome, Is.EqualTo(VerbOutcome.Done));
            Assert.That(Ask(rig, WorkOn(WorkKind.Point, other.Id.Value)).Outcome, Is.EqualTo(VerbOutcome.Done));
            rig.Pump(4);
            Assert.That(Ask(rig, new IntentMessage { Verb = Verb.StopWork }).Outcome, Is.EqualTo(VerbOutcome.Done), "let go");
            Assert.That(rig.Session.Work, Is.Null);
        }

        private static ThingState FlakeState(float edge)
        {
            ThingState s = default;
            s.SetMass(0.02f);
            s.SetEdge(edge);
            return s;
        }

        [Test]
        public void ARefusalIsAnsweredInWordsAndStartsNothing()
        {
            WorldState world = World();
            Rig rig = Connect(world);
            Stand(rig, 0.0, 0.0);
            Entity thick = Spawn(world, Bangalay, Stick(1.2f, 0.035f), 1.5, 0.0);
            rig.Pump(2);
            IntentResultMessage answer = Ask(rig, WorkOn(WorkKind.Break, thick.Id.Value));
            Assert.That(answer.Outcome, Is.EqualTo(VerbOutcome.WontWork));
            Assert.That(answer.Note, Does.Contain("too thick"));
            Assert.That(rig.Session.Work, Is.Null);
            Assert.That(Ask(rig, WorkOn(WorkKind.Point, thick.Id.Value)).Outcome, Is.EqualTo(VerbOutcome.NoTool), "no edge in hand");
            Assert.That(Ask(rig, WorkOn(WorkKind.Break, 999)).Outcome, Is.EqualTo(VerbOutcome.NotThere));
            Entity far = Spawn(world, Bangalay, Stick(1.2f, 0.016f), 9.0, 0.0);
            rig.Pump(2);
            Assert.That(Ask(rig, WorkOn(WorkKind.Break, far.Id.Value)).Outcome, Is.EqualTo(VerbOutcome.OutOfReach));
        }

        [Test]
        public void StrippingAStickOfTheLitterTakesItFromTheLayerAndLeavesTheStripsBesideItAndCordIsLaidInTheHands()
        {
            WorldState world = World();
            Rig rig = Connect(world);
            LyingThing lying = new LyingThing(2, 2, StandLayout.Kind.Stick, 0);
            Assert.That(LyingThings.TryFind(world, lying, out Double3 at), Is.True);
            Stand(rig, at.X - 1.5, at.Z);
            rig.Pump(2);
            IntentResultMessage answer = Ask(rig, WorkOnLying(WorkKind.Strip, lying));
            Assert.That(answer.Outcome, Is.EqualTo(VerbOutcome.Done), "a bangalay stick of the litter strips");
            int steps = (int)Math.Ceiling(answer.Seconds / Dt) + 3;
            rig.Pump(steps);
            Assert.That(rig.Session.Work, Is.Null);
            Assert.That(world.Taken.IsTaken(lying), Is.True, "the stick left the layer for good, as a thing moved does");
            List<Entity> strips = new List<Entity>();
            Entity stripped = null;
            foreach (Entity e in world.Entities.All)
            {
                if (e.Definition.Substance == Substance.Bark) strips.Add(e);
                else if (ReferenceEquals(e.Definition, Bangalay)) stripped = e;
            }
            ThingState was = LyingProperties.StateOf(lying, LyingSites.Of(world, lying));
            Assert.That(strips.Count, Is.EqualTo((int)Math.Round(was.LengthM / Work.StripLengthM)), "a strip for every 0.3 m");
            Assert.That(stripped, Is.Not.Null, "the stick lies on as an item where it lay, stripped");
            Assert.That((stripped.Item.State.Marks & ThingMarks.Stripped) != 0, Is.True);
            Assert.That(Double3.Distance(stripped.Position, at), Is.LessThan(0.01));
            Assert.That(Double3.Distance(strips[0].Position, at), Is.LessThan(1.0), "the strips beside it");

            // Two strips into the hands, the second in another place, and cord laid from them.
            Assert.That(Ask(rig, PickUp(strips[0].Id.Value)).Outcome, Is.EqualTo(VerbOutcome.Done));
            Assert.That(Ask(rig, PickUp(strips[1].Id.Value)).Outcome, Is.EqualTo(VerbOutcome.Done));
            Assert.That(rig.Session.Hands.Hand, Is.EqualTo((byte)1));
            IntentResultMessage twist = Ask(rig, WorkOnPlace(WorkKind.Twist, 2));
            Assert.That(twist.Outcome, Is.EqualTo(VerbOutcome.Done), "a strip in hand, a strip in place 2");
            rig.Pump((int)Math.Ceiling(Work.TwistSecondsPerJoin / Dt) + 3);
            Assert.That(rig.Session.Work, Is.Null);
            Assert.That(rig.Session.Hands.Things.Count, Is.EqualTo(1), "two strips became one cord");
            CarriedThing held = rig.Session.Hands.Things[0];
            Assert.That(held.Definition, Is.SameAs(DefinitionCatalogue.Cord));
            Assert.That(held.Place, Is.EqualTo((byte)1), "in the hand");
            Assert.That(held.Item.State.LengthM, Is.EqualTo((float)(Work.CordPerStripLength * 2 * Work.StripLengthM)).Within(1e-5f));
            CarryingMessage told = rig.Client.Carrying;
            Assert.That(told.Things.Length, Is.EqualTo(1));
            Assert.That(told.Things[0].Definition, Is.SameAs(DefinitionCatalogue.Cord));
            Assert.That(told.Things[0].Item.State.LengthM, Is.EqualTo(held.Item.State.LengthM), "and the client holds its length");
        }
    }
}
