using System;
using System.Collections.Generic;
using System.IO;
using EarthGame.ClientCore;
using EarthGame.Engine;
using EarthGame.Protocol;
using EarthGame.Server;
using EarthGame.Transport;
using NUnit.Framework;

namespace EarthGame.Tests.Server
{
    /// <summary>
    /// The first stone over the in-memory wire (FP.3): a founder with a cobble in hand strikes another, the server commits
    /// the blow on the world it holds by the knapping physics and answers in its words, the flake comes into the world with
    /// its own mass and edge and the core is lighter, the client's mirror sees both; a tap bounces and changes nothing; a
    /// blow without a hammer, on no stone, out of reach or on nothing is refused as such; a core held in another place is
    /// struck where it is held; a cobble of the litter that a blow changes becomes an item where it lay; a spent core is
    /// gone; and what a blow made of a stone is saved and loaded, lying and carried.
    /// </summary>
    public sealed class KnapWireTests
    {
        private const double Dt = 0.05;
        private const int TickRate = 20;
        private const string Now = "2026-09-16T10:00:00Z";
        private static readonly Region FixtureRegion = new Region("fixture", "Fixture", Region.Bherwerre.CentreLatitudeDeg,
            Region.Bherwerre.CentreLongitudeDeg, 40.0, 237, 8.0);
        private static readonly Definition Silcrete = DefinitionCatalogue.CobbleOf(StoneType.Silcrete);

        private string _dir;

        [SetUp]
        public void SetUp() => _dir = Path.Combine(Path.GetTempPath(), "EarthGame2.Tests", "knap", Guid.NewGuid().ToString("N"));

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        private sealed class Rig
        {
            public GameServer Server;
            public GameClient Client;
            public WorldState World;
            public readonly List<IntentResultMessage> Answers = new List<IntentResultMessage>();
            public readonly List<CarryingMessage> Hands = new List<CarryingMessage>();
            public readonly List<EntityView> Spawned = new List<EntityView>();
            public readonly List<(ulong Id, byte Why)> Gone = new List<(ulong, byte)>();
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

        /// <summary>Flat ground at 100 m, 40 m square at 10 m cells; with a loose layer of two cobbles a cell over a stone layer of silcrete when asked.</summary>
        private static WorldState World(bool litter = false)
        {
            RegionRaster ground = TestRasters.FromLaw(5, 10.0, 40.0, "knap_ground", (row, col) => 100f);
            RegionRaster loose = null, stone = null;
            if (litter)
            {
                int silcrete = 0;
                for (int i = 0; i < StoneType.All.Count; i++) if (ReferenceEquals(StoneType.All[i], StoneType.Silcrete)) silcrete = i + 1;
                loose = TestRasters.FromCodes(5, 10.0, 40.0, "knap_loose", "loose", (row, col) => LooseCodes.Pack(0, 2), null);
                stone = TestRasters.FromCodes(5, 10.0, 40.0, "knap_stone", "stone", (row, col) => (uint)silcrete, null);
            }
            return new WorldState(1, FixtureRegion, FixtureRegion.WakeClock(), new Heightfield(ground), 0, null, null, null, null, loose, stone);
        }

        /// <summary>A client joined to a fresh server on the world, or to a server already listening on the transport given.</summary>
        private static Rig Connect(WorldState world, GameServer server = null, IServerTransport serverTransport = null)
        {
            IClientTransport ct;
            Rig rig = new Rig { World = world };
            if (server == null)
            {
                InMemoryTransport.CreatePair(out IServerTransport st, out ct);
                rig.Server = new GameServer(new ServerConfig { TickRate = TickRate }, st, world);
                rig.Server.Listen(1);
            }
            else
            {
                rig.Server = server;
                ct = InMemoryTransport.CreateClient(serverTransport);
            }
            rig.Client = new GameClient(ct);
            rig.Client.IntentAnswered += a => rig.Answers.Add(a);
            rig.Client.CarryingChanged += c => rig.Hands.Add(c);
            rig.Client.Entities.Spawned += v => rig.Spawned.Add(v);
            rig.Client.Entities.Gone += (v, why) => rig.Gone.Add((v.Id.Value, why));
            rig.Client.Connect("memory", 1, "William", "");
            rig.Pump(6);
            Assert.That(rig.Client.State, Is.EqualTo(ClientState.Connected));
            Assert.That(rig.Client.IsInteractive, Is.True);
            // A held clock holds the body, so the founder's work capacity stays at one through the test.
            world.Clock.Scale = 0.0;
            return rig;
        }

        /// <summary>The founder stood at a point, on the ground.</summary>
        private static void Stand(Rig rig, double east, double north, float yawDeg = 90f)
        {
            PlayerSession s = rig.Session;
            s.Body = MoverState.AtRest(east, rig.World.GroundAt(east, north), north);
            s.Body.Grounded = true;
            s.HasBody = true;
            s.StoodUp = s.Body.Up;
            s.YawDeg = yawDeg;
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

        private static IntentMessage PickUp(ulong id) => new IntentMessage { Verb = Verb.PickUp, Target = IntentMessage.TargetEntity, EntityId = id };
        private static IntentMessage KnapItem(ulong id, byte windUp) => new IntentMessage { Verb = Verb.Knap, Target = IntentMessage.TargetEntity, EntityId = id, WindUp = windUp };
        private static IntentMessage KnapHeld(byte place, byte windUp) => new IntentMessage { Verb = Verb.Knap, Target = IntentMessage.TargetPlace, Place = place, WindUp = windUp };
        private static IntentMessage KnapLying(LyingThing thing, byte windUp) => new IntentMessage { Verb = Verb.Knap, Target = IntentMessage.TargetLying, Lying = thing, WindUp = windUp };

        /// <summary>The flakes the client has been shown, newest last.</summary>
        private static List<EntityView> Flakes(Rig rig)
        {
            List<EntityView> flakes = new List<EntityView>();
            foreach (EntityView v in rig.Spawned) if (DefinitionCatalogue.IsFlake(v.Definition)) flakes.Add(v);
            return flakes;
        }

        /// <summary>What the physics says a full swing of one fresh silcrete cobble does to another: worked out here on a core of its own, so the wire's numbers can be held to it.</summary>
        private static KnapResult FullSwingOnFreshSilcrete(out StoneCore core)
        {
            core = new StoneCore(StoneType.Silcrete, Silcrete.MassKg);
            double energy = Knapping.SwingEnergyJ(1.0, Silcrete.MassKg, 1.0);
            return Knapping.Strike(core, StoneType.Silcrete, Silcrete.MassKg, energy);
        }

        [Test]
        public void AFullSwingTakesAFlakeOffTheCobbleAndTheClientSeesTheFlakeAndTheLighterCore()
        {
            WorldState world = World();
            Rig rig = Connect(world);
            Stand(rig, 0.0, 0.0);
            ulong hammer = world.SpawnItem(Silcrete, 1.0, 0.0).Id.Value;
            ulong core = world.SpawnItem(Silcrete, 2.5, 0.0).Id.Value;
            rig.Pump(2);
            Assert.That(Ask(rig, PickUp(hammer)).Outcome, Is.EqualTo(VerbOutcome.Done));
            int hands = rig.Hands.Count;

            IntentResultMessage answer = Ask(rig, KnapItem(core, 255));
            KnapResult expected = FullSwingOnFreshSilcrete(out StoneCore worked);
            Assert.That(expected.Outcome, Is.EqualTo(KnapOutcome.Flake), "the fixture: a full swing of a silcrete cobble takes a flake off another");
            Assert.That(answer.Outcome, Is.EqualTo(VerbOutcome.Flaked));
            Assert.That(answer.Note, Is.EqualTo(expected.Note), "the answer is the physics' own words");
            Assert.That(answer.Note, Does.Contain("flake"));

            Assert.That(world.Entities.TryGet(core, out Entity struck), Is.True, "the core lies on");
            Assert.That(struck.Item.MassKg, Is.EqualTo((float)worked.MassKg), "lighter by the flake");
            Assert.That(struck.Item.PlatformDeg, Is.EqualTo((float)worked.PlatformAngleDeg), "its platform worked toward ninety");
            Assert.That(struck.Item.FlakesTaken, Is.EqualTo((ushort)1));
            Assert.That(struck.Item.Resting, Is.True, "how it lay is not the blow's to change");
            Entity flake = null;
            foreach (Entity e in world.Entities.All) if (DefinitionCatalogue.IsFlake(e.Definition)) flake = e;
            Assert.That(flake, Is.Not.Null, "a flake came into the world");
            Assert.That(flake.Definition, Is.SameAs(DefinitionCatalogue.FlakeOf(StoneType.Silcrete)), "of the core's stone");
            Assert.That(flake.Item.MassKg, Is.EqualTo((float)expected.FlakeMassKg), "weighing what the blow took");
            Assert.That(flake.Item.Edge01, Is.EqualTo((float)expected.EdgeQuality), "with the edge the stone and the blow made");
            Assert.That(Knapping.IsUsableTool(expected), Is.True, "a tool, coarse though a full swing leaves it");
            Assert.That(flake.Position.X, Is.EqualTo(2.5 - 0.2).Within(1e-9), "beside the core, a step towards the founder");
            Assert.That(flake.Position.Z, Is.EqualTo(-0.1).Within(1e-9), "and to the founder's right (south, facing east), so the flakes scatter");
            Assert.That(rig.Hands.Count, Is.EqualTo(hands), "the hands did not change: the hammer is as it was");

            rig.Pump(12);
            Assert.That(flake.Item.Resting, Is.True, "and it fell to the ground");
            Assert.That(rig.Client.Entities.Views.TryGetValue(core, out EntityView coreView), Is.True);
            Assert.That(coreView.Item.MassKg, Is.EqualTo((float)worked.MassKg), "the client's mirror holds the core's new state");
            Assert.That(coreView.Item.FlakesTaken, Is.EqualTo((ushort)1));
            List<EntityView> flakes = Flakes(rig);
            Assert.That(flakes.Count, Is.EqualTo(1));
            Assert.That(flakes[0].Item.MassKg, Is.EqualTo((float)expected.FlakeMassKg), "and the flake's own mass");
            Assert.That(flakes[0].Item.Edge01, Is.EqualTo((float)expected.EdgeQuality));
            Assert.That(rig.Client.Entities.Views[flakes[0].Id.Value].Item.Resting, Is.True);
            Assert.That(rig.Client.Entities.Digest(), Is.EqualTo(rig.Server.EntityDigest(rig.Session)), "the two ends name the same things");

            // Two flakes off one core are different tools: the second blow lands on a worked platform and a lighter core.
            IntentResultMessage second = Ask(rig, KnapItem(core, 128));
            Assert.That(second.Outcome, Is.EqualTo(VerbOutcome.Flaked));
            rig.Pump(12);
            flakes = Flakes(rig);
            Assert.That(flakes.Count, Is.EqualTo(2));
            Assert.That(flakes[1].Item.MassKg, Is.Not.EqualTo(flakes[0].Item.MassKg), "a half swing takes a different flake");
            Assert.That(flakes[1].Item.Edge01, Is.GreaterThan(flakes[0].Item.Edge01), "gentler, so sharper");
            Assert.That(struck.Item.FlakesTaken, Is.EqualTo((ushort)2));
            Assert.That(flakes[1].Position.Z, Is.EqualTo(0.1).Within(1e-9), "the second flake falls on the other side of the core");
        }

        [Test]
        public void ATapBouncesAndChangesNothing()
        {
            WorldState world = World();
            Rig rig = Connect(world);
            Stand(rig, 0.0, 0.0);
            ulong hammer = world.SpawnItem(Silcrete, 1.0, 0.0).Id.Value;
            ulong core = world.SpawnItem(Silcrete, 2.5, 0.0).Id.Value;
            rig.Pump(2);
            Assert.That(Ask(rig, PickUp(hammer)).Outcome, Is.EqualTo(VerbOutcome.Done));
            int entities = world.Entities.All.Count;
            IntentResultMessage answer = Ask(rig, KnapItem(core, 20));
            Assert.That(answer.Outcome, Is.EqualTo(VerbOutcome.Bounced));
            Assert.That(answer.Note, Does.Contain("bounced"));
            Assert.That(world.Entities.TryGet(core, out Entity struck), Is.True);
            Assert.That(struck.Item.HasOwnState, Is.False, "nothing happened to the core");
            Assert.That(world.Entities.All.Count, Is.EqualTo(entities), "and no flake came away");
            Assert.That(Flakes(rig), Is.Empty);
        }

        [Test]
        public void WithoutAHammerOrOnNoStoneOrOutOfReachTheBlowIsRefusedAsSuch()
        {
            WorldState world = World();
            Rig rig = Connect(world);
            Stand(rig, 0.0, 0.0);
            ulong stick = world.SpawnItem(DefinitionCatalogue.Stick, 1.0, 0.0).Id.Value;
            ulong plain = world.SpawnItem(DefinitionCatalogue.Cobble, 1.0, 1.0).Id.Value;
            ulong hammer = world.SpawnItem(Silcrete, 1.0, -1.0).Id.Value;
            ulong core = world.SpawnItem(Silcrete, 2.5, 0.0).Id.Value;
            ulong far = world.SpawnItem(Silcrete, 12.0, 0.0).Id.Value;
            rig.Pump(2);
            Assert.That(Ask(rig, KnapItem(core, 255)).Outcome, Is.EqualTo(VerbOutcome.NothingInHand), "an empty hand");
            Assert.That(Ask(rig, PickUp(stick)).Outcome, Is.EqualTo(VerbOutcome.Done));
            Assert.That(Ask(rig, KnapItem(core, 255)).Outcome, Is.EqualTo(VerbOutcome.NoHammer), "a stick in hand");
            Assert.That(Ask(rig, PickUp(hammer)).Outcome, Is.EqualTo(VerbOutcome.Done));
            Assert.That(Ask(rig, new IntentMessage { Verb = Verb.Hold, Place = 2 }).Outcome, Is.EqualTo(VerbOutcome.Done), "the cobble to hand");
            Assert.That(Ask(rig, KnapItem(plain, 255)).Outcome, Is.EqualTo(VerbOutcome.NotStone), "a cobble of no stone the country names");
            Assert.That(Ask(rig, KnapItem(far, 255)).Outcome, Is.EqualTo(VerbOutcome.OutOfReach), "twelve metres off");
            Assert.That(Ask(rig, KnapItem(999, 255)).Outcome, Is.EqualTo(VerbOutcome.NotThere), "no such thing");
            Assert.That(Ask(rig, KnapHeld(1, 255)).Outcome, Is.EqualTo(VerbOutcome.NotStone), "the stick held in place 1");
            Assert.That(Ask(rig, KnapHeld(2, 255)).Outcome, Is.EqualTo(VerbOutcome.NotStone), "the hammer is not its own core");
            Assert.That(Ask(rig, KnapHeld(3, 255)).Outcome, Is.EqualTo(VerbOutcome.NotThere), "nothing in place 3");
            Assert.That(world.Entities.TryGet(core, out Entity untouched) && !untouched.Item.HasOwnState, Is.True, "and no blow landed");
            Assert.That(Flakes(rig), Is.Empty);

            rig.Server.Paused = true;
            Assert.That(Ask(rig, KnapItem(core, 255)).Outcome, Is.EqualTo(VerbOutcome.NotNow), "nothing is done while the world is held");
            rig.Server.Paused = false;
        }

        [Test]
        public void ACoreHeldInAnotherPlaceIsStruckWhereItIsHeldAndTheFlakeFallsAtTheFeet()
        {
            WorldState world = World();
            Rig rig = Connect(world);
            Stand(rig, 0.0, 0.0);
            ulong hammer = world.SpawnItem(Silcrete, 1.0, 0.0).Id.Value;
            ulong core = world.SpawnItem(Silcrete, 1.0, 1.0).Id.Value;
            rig.Pump(2);
            Assert.That(Ask(rig, PickUp(hammer)).Outcome, Is.EqualTo(VerbOutcome.Done));
            Assert.That(Ask(rig, PickUp(core)).Outcome, Is.EqualTo(VerbOutcome.Done));
            Assert.That(rig.Session.Hands.Hand, Is.EqualTo((byte)1), "the first taken is the hand: the hammer");
            int hands = rig.Hands.Count;
            IntentResultMessage answer = Ask(rig, KnapHeld(2, 255));
            KnapResult expected = FullSwingOnFreshSilcrete(out StoneCore worked);
            Assert.That(answer.Outcome, Is.EqualTo(VerbOutcome.Flaked));
            Assert.That(rig.Session.Hands.TryAt(2, out CarriedThing held), Is.True, "the core is still held");
            Assert.That(held.Item.MassKg, Is.EqualTo((float)worked.MassKg), "and lighter by the flake");
            Assert.That(held.Item.FlakesTaken, Is.EqualTo((ushort)1));
            Assert.That(rig.Hands.Count, Is.GreaterThan(hands), "the hands were told");
            Entity flake = null;
            foreach (Entity e in world.Entities.All) if (DefinitionCatalogue.IsFlake(e.Definition)) flake = e;
            Assert.That(flake, Is.Not.Null);
            Assert.That(flake.Item.MassKg, Is.EqualTo((float)expected.FlakeMassKg));
            Double3 feet = rig.Session.Body.Feet;
            double off = Math.Sqrt(Math.Pow(flake.Position.X - feet.X, 2) + Math.Pow(flake.Position.Z - feet.Z, 2));
            Assert.That(off, Is.EqualTo(0.5).Within(1e-6), "at the founder's feet, half a metre ahead");
            rig.Pump(12);
            Assert.That(flake.Item.Resting, Is.True);
        }

        [Test]
        public void ACobbleOfTheLitterThatABlowChangesBecomesAnItemWhereItLayAndATapLeavesItInTheLayer()
        {
            WorldState world = World(litter: true);
            LyingThing first = new LyingThing(2, 2, StandLayout.Kind.Cobble, 0), second = new LyingThing(2, 2, StandLayout.Kind.Cobble, 1);
            Assert.That(LyingThings.TryFind(world, first, out Double3 at), Is.True);
            Assert.That(LyingThings.DefinitionOf(world, first), Is.SameAs(Silcrete), "the cell's stone is silcrete");
            Rig rig = Connect(world);
            Stand(rig, at.X - 1.5, at.Z);
            ulong hammer = world.SpawnItem(Silcrete, at.X - 1.0, at.Z + 0.5).Id.Value;
            rig.Pump(2);
            Assert.That(Ask(rig, PickUp(hammer)).Outcome, Is.EqualTo(VerbOutcome.Done));

            int entities = world.Entities.All.Count;
            IntentResultMessage answer = Ask(rig, KnapLying(first, 255));
            // The litter cobble is struck as what its place says it is (BF.1): a core of its own hashed mass, not its kind's.
            StoneCore worked = new StoneCore(StoneType.Silcrete, LyingProperties.StateOf(first, LyingSites.Of(world, first)).MassKg);
            KnapResult expected = Knapping.Strike(worked, StoneType.Silcrete, Silcrete.MassKg, Knapping.SwingEnergyJ(1.0, Silcrete.MassKg, 1.0));
            Assert.That(answer.Outcome, Is.EqualTo(VerbOutcome.Flaked));
            Assert.That(world.Taken.IsTaken(first), Is.True, "the struck cobble left the layer for good");
            Assert.That(world.Entities.All.Count, Is.EqualTo(entities + 2), "and lies on as an item, with the flake beside it");
            Entity lying = null;
            foreach (Entity e in world.Entities.All) if (ReferenceEquals(e.Definition, Silcrete) && e.Item.HasOwnState) lying = e;
            Assert.That(lying, Is.Not.Null);
            Assert.That(lying.Position.X, Is.EqualTo(at.X).Within(1e-9), "where it lay");
            Assert.That(lying.Position.Z, Is.EqualTo(at.Z).Within(1e-9));
            Assert.That(lying.Item.MassKg, Is.EqualTo((float)worked.MassKg));
            Assert.That(lying.Item.Resting, Is.True);
            rig.Pump(3);
            Assert.That(rig.Client.Taken.IsTaken(first), Is.True, "the client is told the taking");
            Assert.That(rig.Client.Entities.Views.ContainsKey(lying.Id.Value), Is.True, "and shown the item");
            Assert.That(Flakes(rig).Count, Is.EqualTo(1));

            entities = world.Entities.All.Count;
            Assert.That(Ask(rig, KnapLying(second, 20)).Outcome, Is.EqualTo(VerbOutcome.Bounced));
            Assert.That(world.Taken.IsTaken(second), Is.False, "a blow that changed nothing leaves the cobble in the layer");
            Assert.That(world.Entities.All.Count, Is.EqualTo(entities));
            Assert.That(Ask(rig, KnapLying(first, 255)).Outcome, Is.EqualTo(VerbOutcome.NotThere), "the first is no longer of the litter");
            Assert.That(Ask(rig, KnapLying(new LyingThing(2, 2, StandLayout.Kind.Stick, 0), 255)).Outcome, Is.EqualTo(VerbOutcome.NotThere), "the cell holds no stick");
        }

        [Test]
        public void ASpentCoreIsGoneAndItsViewersAreTold()
        {
            WorldState world = World();
            Rig rig = Connect(world);
            Stand(rig, 0.0, 0.0);
            ulong hammer = world.SpawnItem(Silcrete, 1.0, 0.0).Id.Value;
            Entity core = world.SpawnItem(Silcrete, 2.5, 0.0);
            // A core with a hundred and thirty grams left: a full swing's energy is past what it can take, and what a shatter leaves is under the spent mass.
            core.SetItem(new ItemComponent { Resting = true, MassKg = 0.13f, PlatformDeg = 75f, FlakesTaken = 6 }, world.Tick);
            rig.Pump(2);
            Assert.That(rig.Client.Entities.Views.ContainsKey(core.Id.Value), Is.True);
            Assert.That(Ask(rig, PickUp(hammer)).Outcome, Is.EqualTo(VerbOutcome.Done));
            IntentResultMessage answer = Ask(rig, KnapItem(core.Id.Value, 255));
            Assert.That(answer.Outcome, Is.EqualTo(VerbOutcome.Shattered));
            Assert.That(answer.Note, Does.Contain("broke up"));
            rig.Pump(3);
            Assert.That(world.Entities.TryGet(core.Id, out _), Is.False, "spent, the core is gone");
            Assert.That(rig.Gone, Does.Contain((core.Id.Value, EntityGoneMessage.Died)), "and its viewers are told");
            Assert.That(Flakes(rig), Is.Empty, "a shatter gives no flake");
            Assert.That(Ask(rig, KnapItem(core.Id.Value, 255)).Outcome, Is.EqualTo(VerbOutcome.NotThere));
        }

        [Test]
        public void WhatABlowMadeOfAStoneIsSavedAndLoadedLyingAndCarried()
        {
            WorldState world = World();
            Rig rig = Connect(world);
            Stand(rig, 0.0, 0.0);
            ulong hammer = world.SpawnItem(Silcrete, 1.0, 0.0).Id.Value;
            ulong core = world.SpawnItem(Silcrete, 2.5, 0.0).Id.Value;
            rig.Pump(2);
            Assert.That(Ask(rig, PickUp(hammer)).Outcome, Is.EqualTo(VerbOutcome.Done));
            Assert.That(Ask(rig, KnapItem(core, 255)).Outcome, Is.EqualTo(VerbOutcome.Flaked));
            rig.Pump(12);
            ulong flakeId = Flakes(rig)[0].Id.Value;
            Assert.That(Ask(rig, PickUp(core)).Outcome, Is.EqualTo(VerbOutcome.Done), "the worked core into the hands");
            rig.Pump(2);
            CarriedThing carriedCore = rig.Session.Hands.Things[1];
            Assert.That(carriedCore.Item.HasOwnState, Is.True);

            string before = rig.Server.Digest();
            rig.Server.Save(_dir, Now);
            WorldSaveInfo info = WorldSave.Read(_dir);
            Assert.That(info.Digest, Is.EqualTo(before), "the digest file names the world with the stones' state");
            SavedEntity flake = info.Entities.Find(e => e.Id == flakeId);
            Assert.That(flake.Key, Is.EqualTo("item/flake-silcrete"));
            Assert.That(flake.Item.MassKg, Is.GreaterThan(0f), "the flake's own mass lies in the region file");
            Assert.That(info.Players["William"].Carried[1].Item.MassKg, Is.EqualTo(carriedCore.Item.MassKg), "the carried core's in the player file");

            WorldState loaded = WorldSave.Restore(info, world.Terrain, FixtureRegion);
            InMemoryTransport.CreatePair(out IServerTransport st, out IClientTransport _);
            GameServer server = new GameServer(new ServerConfig { TickRate = TickRate }, st, loaded);
            server.RememberPlayers(info.Players.Values);
            server.Listen(1);
            Assert.That(server.Digest(), Is.EqualTo(before), "the same name after the load");
            Rig again = Connect(loaded, server, st);
            Assert.That(again.Session.Hands.Things[1].Item.MassKg, Is.EqualTo(carriedCore.Item.MassKg), "the core comes back to the hands as the blows left it");
            Assert.That(again.Session.Hands.Things[1].Item.FlakesTaken, Is.EqualTo((ushort)1));
            Assert.That(loaded.Entities.TryGet(flakeId, out Entity lyingFlake), Is.True);
            Assert.That(lyingFlake.Item.Edge01, Is.EqualTo(flake.Item.Edge01));
            // The hand is still the hammer's place: the loaded core, struck again where it is held, is lighter still.
            Assert.That(Ask(again, KnapHeld(2, 255)).Outcome, Is.EqualTo(VerbOutcome.Flaked));
            Assert.That(again.Session.Hands.Things[1].Item.FlakesTaken, Is.EqualTo((ushort)2));
        }
    }
}
