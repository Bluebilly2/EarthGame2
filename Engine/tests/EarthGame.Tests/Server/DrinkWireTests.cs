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
    /// Thirst and the drink over the in-memory wire (FP.1), on a small world with a lake down its western columns and
    /// the sea down its eastern one: the founder's body loses water on the world's clock and their client is told; a
    /// drink from the lake gives a litre and a half and is told at once; the sea answers salt; a point out of reach or
    /// on dry ground is refused as such; and the client is held to the capacity it was told.
    /// </summary>
    public sealed class DrinkWireTests
    {
        private const double Dt = 0.05;
        private const int TickRate = 20;
        private static readonly Region FixtureRegion = new Region("fixture", "Fixture", Region.Bherwerre.CentreLatitudeDeg,
            Region.Bherwerre.CentreLongitudeDeg, 40.0, 237, 8.0);

        private sealed class Rig
        {
            public GameServer Server;
            public GameClient Client;
            public WorldState World;
            public readonly List<FounderStateMessage> Told = new List<FounderStateMessage>();
            public readonly List<IntentResultMessage> Answers = new List<IntentResultMessage>();
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

        /// <summary>
        /// Flat ground at 100 m, 40 m square at 10 m cells; the lake two metres deep over columns 0 and 1, the sea over column 4,
        /// and column 3 a creek bed the layer classes as a creek with no water standing in it (its surface at the ground).
        /// </summary>
        private static WorldState World()
        {
            RegionRaster ground = TestRasters.FromLaw(5, 10.0, 40.0, "drink_ground", (row, col) => 100f);
            RegionRaster surface = TestRasters.FromLaw(5, 10.0, 40.0, "drink_surface", (row, col) => col <= 1 || col == 4 ? 102f : 100f);
            RegionRaster classes = TestRasters.FromCodes(5, 10.0, 40.0, "drink_water", "water",
                (row, col) => col <= 1 ? (uint)WaterClass.Lake : col == 3 ? (uint)WaterClass.Creek : col == 4 ? (uint)WaterClass.Sea : 0u, null);
            return new WorldState(1, FixtureRegion, FixtureRegion.WakeClock(), new Heightfield(ground), 0, null, new WorldWater(surface, classes));
        }

        private static Rig Connect(WorldState world, bool development = false)
        {
            InMemoryTransport.CreatePair(out IServerTransport st, out IClientTransport ct);
            Rig rig = new Rig { World = world };
            rig.Server = new GameServer(new ServerConfig { TickRate = TickRate, Movement = new MovementRules { AllowFlight = development } }, st, world);
            rig.Server.Listen(1);
            rig.Client = new GameClient(ct);
            rig.Client.FounderStateChanged += m => rig.Told.Add(m);
            rig.Client.IntentAnswered += a => rig.Answers.Add(a);
            rig.Client.Connect("memory", 1, "William", "");
            rig.Pump(6);
            Assert.That(rig.Client.State, Is.EqualTo(ClientState.Connected));
            Assert.That(rig.Client.IsInteractive, Is.True);
            return rig;
        }

        /// <summary>The founder stood on the dry middle column, at the ground.</summary>
        private static void Stand(Rig rig, double east, double north)
        {
            PlayerSession s = rig.Session;
            s.Body = MoverState.AtRest(east, rig.World.GroundAt(east, north), north);
            s.Body.Grounded = true;
            s.HasBody = true;
            s.StoodUp = s.Body.Up;
        }

        private static VerbOutcome Ask(Rig rig, double east, double up, double north)
        {
            int before = rig.Answers.Count;
            rig.Client.SendIntent(new IntentMessage { Verb = Verb.Drink, East = east, Up = up, North = north });
            rig.Pump(2);
            Assert.That(rig.Answers.Count, Is.EqualTo(before + 1), "one answer to one intent");
            return rig.Answers[before].Outcome;
        }

        [Test]
        public void TheBodyLosesWaterOnTheWorldsClockAndTheClientIsToldOnceASecond()
        {
            WorldState world = World();
            Rig rig = Connect(world);
            Stand(rig, 0.0, 0.0);
            Assert.That(rig.Told.Count, Is.GreaterThanOrEqualTo(1), "the join's snapshot tells the founder's state");
            Assert.That(rig.Told[0].Water01, Is.EqualTo(1.0));
            int told = rig.Told.Count;
            world.Clock.Scale = 0.0;
            rig.Pump(TickRate * 2);
            Assert.That(rig.Session.Hydration.Water01, Is.EqualTo(1.0), "a held clock holds the body");
            world.Clock.Scale = 60.0;
            rig.Pump(TickRate * 3);
            // Three seconds at sixty times the game's rate is a tenth of a day: 0.24 litres of 42.
            double expected = 1.0 - Hydration.BaseWaterLossLPerDay * (3.0 * 60.0 / WorldClock.RealSecondsPerDay) / Hydration.TotalBodyWaterL;
            Assert.That(rig.Session.Hydration.Water01, Is.EqualTo(expected).Within(1e-9));
            Assert.That(rig.Told.Count - told, Is.InRange(4, 6), "told once a second over five seconds");
            Assert.That(rig.Client.LastWater01, Is.EqualTo(rig.Told[rig.Told.Count - 1].Water01));
            Assert.That(rig.Client.LastWater01, Is.LessThan(1.0).And.GreaterThan(expected - 1e-6));
        }

        [Test]
        public void ADrinkFromTheLakeGivesALitreAndAHalfAndIsToldAtOnce()
        {
            WorldState world = World();
            Rig rig = Connect(world);
            world.Clock.Scale = 0.0;
            rig.Session.Hydration.Restore(0.9);
            Stand(rig, -8.0, 0.0);
            int told = rig.Told.Count;
            // The lake's edge post is at east -10; the founder's eye at -8 is two metres off it.
            VerbOutcome outcome = Ask(rig, -11.0, 102.0, 0.0);
            Assert.That(outcome, Is.EqualTo(VerbOutcome.Done));
            Assert.That(rig.Session.Hydration.Water01, Is.EqualTo(0.9 + 1.5 / Hydration.TotalBodyWaterL).Within(1e-12));
            Assert.That(rig.Told.Count, Is.GreaterThan(told), "the drink is told without waiting for the second");
            Assert.That(rig.Told[rig.Told.Count - 1].Water01, Is.EqualTo(rig.Session.Hydration.Water01).Within(1e-12));
        }

        [Test]
        public void TheSeaAnswersSaltAndDryGroundHasNothingToDrink()
        {
            WorldState world = World();
            Rig rig = Connect(world);
            world.Clock.Scale = 0.0;
            rig.Session.Hydration.Restore(0.9);
            // The sea's post is at east 20 and the dry ones at 0 and 10: past 15 the water is the sea's, and between the dry posts none stands.
            Stand(rig, 14.0, 0.0);
            Assert.That(Ask(rig, 17.0, 102.0, 0.0), Is.EqualTo(VerbOutcome.Salt), "the sea over column 4");
            Stand(rig, 8.0, 0.0);
            Assert.That(Ask(rig, 6.0, 100.0, 1.0), Is.EqualTo(VerbOutcome.NoWater), "the dry ground between column 2 and the empty creek bed of column 3");
            Assert.That(Ask(rig, 10.0, 100.0, 1.0), Is.EqualTo(VerbOutcome.NoWater), "the creek bed itself: classed a creek, no water standing");
            Assert.That(rig.Session.Hydration.Water01, Is.EqualTo(0.9), "a refusal gives nothing");
        }

        /// <summary>
        /// The water the client streams stands between a wet post and a dry one, read between them; the first drink run's
        /// founder looked at a creek's edge and the server, judging the nearest post, said there was nothing there. The server
        /// reads the surface between posts as the client does, and the water is the nearest wet post's.
        /// </summary>
        [Test]
        public void AtTheWatersEdgeTheDrinkIsJudgedByTheWaterItStandsIn()
        {
            WorldState world = World();
            Rig rig = Connect(world);
            world.Clock.Scale = 0.0;
            rig.Session.Hydration.Restore(0.9);
            // Between the lake's post at east -10 (surface 102) and the dry one at 0 (surface 100), the water at -3 stands 0.6 m deep
            // by the same reading the client makes, and the nearest post is the dry one.
            Assert.That(world.WaterAt(-3.0, 0.0, out double depth), Is.EqualTo(WaterClass.Lake));
            Assert.That(depth, Is.EqualTo(0.6).Within(1e-9));
            Assert.That(world.WaterAt(5.0, 0.0, out depth), Is.EqualTo(WaterClass.Dry), "between two dry posts no water stands");
            Assert.That(depth, Is.EqualTo(0.0));
            Stand(rig, -1.0, 0.0);
            Assert.That(Ask(rig, -3.0, 100.6, 0.0), Is.EqualTo(VerbOutcome.Done));
            Assert.That(rig.Session.Hydration.Water01, Is.EqualTo(0.9 + 1.5 / Hydration.TotalBodyWaterL).Within(1e-12));
        }

        [Test]
        public void WaterOutOfReachIsRefusedAsSuch()
        {
            WorldState world = World();
            Rig rig = Connect(world);
            world.Clock.Scale = 0.0;
            Stand(rig, 5.0, 0.0);
            Assert.That(Ask(rig, -15.0, 102.0, 0.0), Is.EqualTo(VerbOutcome.OutOfReach), "the lake is twenty metres off");
            Assert.That(Ask(rig, 5.0, 102.0, 3000.0), Is.EqualTo(VerbOutcome.OutOfReach), "beyond the region");
        }

        [Test]
        public void TheCeilingIsTheCapacityTheClientWasToldNotTheServersNewerNumber()
        {
            WorldState world = World();
            Rig rig = Connect(world);
            world.Clock.Scale = 0.0;
            Stand(rig, 0.0, 0.0);
            rig.Pump(TickRate + 2);
            PlayerSession s = rig.Session;
            Assert.That(s.CeilingCapacity, Is.EqualTo(1.0));
            // The server's body dries to a tenth lost, told once: the ceiling stays at what the client walks on until told twice.
            s.Hydration.Restore(0.9);
            rig.Pump(TickRate);
            Assert.That(s.ToldCapacity, Is.EqualTo(Hydration.CapacityOf(0.9)).Within(1e-12));
            Assert.That(s.CeilingCapacity, Is.EqualTo(1.0), "the capacity told a second ago still holds the ceiling");
            rig.Pump(TickRate);
            Assert.That(s.CeilingCapacity, Is.EqualTo(Hydration.CapacityOf(0.9)).Within(1e-12), "told twice, the ceiling is the body's");
        }

        [Test]
        public void ASavedFoundersWaterComesBackAtTheJoin()
        {
            WorldState world = World();
            SavedPlayer saved = new SavedPlayer
            {
                Name = "William",
                Body = MoverState.AtRest(0.0, 100.0, 0.0),
                WaterLoss = 0.05,
            };
            saved.Body.Grounded = true;
            InMemoryTransport.CreatePair(out IServerTransport st, out IClientTransport ct);
            Rig rig = new Rig { World = world };
            rig.Server = new GameServer(new ServerConfig { TickRate = TickRate }, st, world);
            rig.Server.RememberPlayers(new[] { saved });
            rig.Server.Listen(1);
            rig.Client = new GameClient(ct);
            rig.Client.FounderStateChanged += m => rig.Told.Add(m);
            world.Clock.Scale = 0.0;
            rig.Client.Connect("memory", 1, "William", "");
            rig.Pump(6);
            Assert.That(rig.Session.Hydration.Water01, Is.EqualTo(0.95).Within(1e-12));
            Assert.That(rig.Client.LastWater01, Is.EqualTo(0.95).Within(1e-12), "and the client is told with its snapshot");
        }
    }
}
