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
    /// The night's cold and death over the in-memory wire (FP.2): a founder's body cools in the world's own night and their
    /// client is told the core; a core moved below the lethal by the panel's row is a death in the next step, under the
    /// Standard mode: the things they carried lie where they fell, a new founder stands at the wake with a full body, and the
    /// client is told what killed them, the numbers and the one sentence; thirst's death is told as thirst; a saved core
    /// comes back at the join.
    /// </summary>
    public sealed class DeathWireTests
    {
        private const double Dt = 0.05;
        private const int TickRate = 20;

        private sealed class Rig
        {
            public GameServer Server;
            public GameClient Client;
            public WorldState World;
            public readonly List<Death> Deaths = new List<Death>();
            public readonly List<CorrectionMessage> Corrections = new List<CorrectionMessage>();
            public readonly List<(PlayerSession, Death)> ServerDeaths = new List<(PlayerSession, Death)>();
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

        private static Rig Connect(IEnumerable<SavedPlayer> remembered = null, bool bridge = false)
        {
            InMemoryTransport.CreatePair(out IServerTransport st, out IClientTransport ct);
            Rig rig = new Rig { World = new WorldState(1, Region.Bherwerre, Region.Bherwerre.WakeClock()) };
            // The beta arc's bridge is down here: these tests prove the death itself.
            rig.Server = new GameServer(new ServerConfig { TickRate = TickRate, Movement = new MovementRules { AllowFlight = true }, BetaArcBridge = bridge }, st, rig.World);
            if (remembered != null) rig.Server.RememberPlayers(remembered);
            rig.Server.FounderDied += (s, d) => rig.ServerDeaths.Add((s, d));
            rig.Server.Listen(1);
            rig.Client = new GameClient(ct);
            rig.Client.Died += d => rig.Deaths.Add(d);
            rig.Client.Corrected += c => rig.Corrections.Add(c);
            rig.Client.Connect("memory", 1, "William", "");
            rig.Pump(6);
            Assert.That(rig.Client.State, Is.EqualTo(ClientState.Connected));
            return rig;
        }

        private static void Stand(Rig rig, double east, double north)
        {
            PlayerSession s = rig.Session;
            s.Body = MoverState.AtRest(east, rig.World.GroundAt(east, north), north);
            s.Body.Grounded = true;
            s.HasBody = true;
            s.StoodUp = s.Body.Up;
        }

        [Test]
        public void TheBodyCoolsInTheWorldsNightAndTheClientIsToldTheCore()
        {
            Rig rig = Connect();
            rig.World.Clock.Scale = 0.0;
            Stand(rig, 0.0, 0.0);
            rig.Pump(2);
            Assert.That(rig.Session.Warmth.CoreC, Is.EqualTo(Warmth.NormalCoreC), "normal at the start of the night, the clock held");
            rig.Client.SendDevSetting(DevSettings.ClockLocalHour, 23.0);
            rig.Client.SendDevSetting(DevSettings.ClockScale, 60.0);
            // At sixty times the game's own forty-eight, a real second is forty-eight minutes of the world: two seconds are an hour and a half of the night.
            rig.Pump(TickRate * 2);
            Warmth body = rig.Session.Warmth;
            Assert.That(body.CoreC, Is.LessThan(Warmth.NormalCoreC - 0.3), "the core has fallen in the night; net " + body.NetHeatW.ToString("0") + " W in air " + rig.Session.Surroundings.AirC.ToString("0.0") + " °C, wind " + rig.Session.Surroundings.WindAtBodyMs.ToString("0.0") + " m/s");
            Assert.That(body.CoreC, Is.GreaterThan(Warmth.HypothermiaC), "but not yet hypothermic in an hour and a half");
            Assert.That(body.Shivering01, Is.GreaterThan(0.0), "shivering by now");
            // Told once a second, and at this rate a second is forty-eight minutes of the night: the word lags the body a little.
            Assert.That(rig.Client.LastCoreC, Is.EqualTo(body.CoreC).Within(0.2), "told within the second");
            Assert.That(rig.Session.Surroundings.SolarElevationDeg, Is.LessThan(0.0), "it is night");
            Assert.That(rig.Session.Hydration.LossLPerDay, Is.EqualTo(Hydration.BaseWaterLossLPerDay).Within(1e-9), "standing still and cold, no sweat: the resting loss alone");
        }

        [Test]
        public void ACoreMovedBelowTheLethalIsADeathTheThingsLieWhereTheyFellAndANewFounderWakesAtTheWake()
        {
            Rig rig = Connect();
            WorldState world = rig.World;
            Stand(rig, 30.0, 12.0);
            Entity stick = world.SpawnItem(DefinitionCatalogue.Stick, 30.5, 12.0);
            rig.Pump(1);
            Double3 eye = new Double3(30.0, world.GroundAt(30.0, 12.0) + 1.65, 12.0);
            Assert.That(rig.Session.Hands.PickUp(world, stick.Id.Value, eye), Is.EqualTo(VerbOutcome.Done));
            rig.Pump(2);
            Assert.That(world.Entities.TryGet(stick.Id.Value, out _), Is.False, "carried, out of the world");
            rig.Session.Hydration.Restore(0.95);

            rig.Client.SendDevSetting(DevSettings.FounderCoreC, 26.0);
            rig.Pump(3);
            Assert.That(rig.Deaths.Count, Is.EqualTo(1), "one death told");
            Death death = rig.Deaths[0];
            Assert.That(death.Cause, Is.EqualTo(CauseOfDeath.Cold));
            Assert.That(death.CoreC, Is.EqualTo(26.0).Within(0.01));
            Assert.That(death.WaterLoss, Is.EqualTo(0.05).Within(1e-4), "a twentieth lost, less the moments since");
            Assert.That(death.East, Is.EqualTo(30.0).Within(1e-9));
            Assert.That(death.North, Is.EqualTo(12.0).Within(1e-9));
            Assert.That(death.Explain(), Does.StartWith("You died of the cold at ").And.EndWith("what you carried lies where you fell."));
            Assert.That(rig.ServerDeaths.Count, Is.EqualTo(1), "and the server's own record");
            Assert.That(rig.ServerDeaths[0].Item2.Explain(), Is.EqualTo(death.Explain()), "the same sentence at both ends");

            Double3 wake = world.SpawnPoint();
            PlayerSession s = rig.Session;
            Assert.That(s.Body.East, Is.EqualTo(wake.X).Within(1e-9), "a new founder stands at the wake");
            Assert.That(s.Body.North, Is.EqualTo(wake.Z).Within(1e-9));
            Assert.That(s.Warmth.CoreC, Is.EqualTo(Warmth.NormalCoreC).Within(0.02), "with a normal core, less the moments since");
            Assert.That(s.Hydration.Water01, Is.EqualTo(1.0).Within(1e-4), "and a full body");
            Assert.That(s.Hands.Things.Count, Is.EqualTo(0), "empty-handed");
            Assert.That(world.Entities.TryGet(stick.Id.Value, out Entity lying), Is.True, "the stick is in the world again");
            Assert.That(lying.Position.X, Is.EqualTo(30.0).Within(1e-9), "where they fell");
            Assert.That(lying.Position.Z, Is.EqualTo(12.0).Within(1e-9));
            Assert.That(rig.Corrections.Count, Is.GreaterThanOrEqualTo(1), "the client is stood at the wake by a correction");
            CorrectionMessage last = rig.Corrections[rig.Corrections.Count - 1];
            Assert.That(last.Body.East, Is.EqualTo(wake.X).Within(1e-9));
            Assert.That(last.Reason, Does.Contain("died of the cold"));
            Assert.That(rig.Client.LastCoreC, Is.EqualTo(Warmth.NormalCoreC).Within(0.02), "told the new body");
            Assert.That(rig.Client.LastWater01, Is.EqualTo(1.0).Within(1e-4));
            Assert.That(rig.Client.Carrying.Things == null || rig.Client.Carrying.Things.Length == 0, Is.True, "and the empty hands");
        }

        /// <summary>
        /// The beta arc's bridge (CANON ruling 33): until fire and shelter exist, the cold reaches the edge of death and no
        /// further. The body still cools, the words still come; a core the panel puts below the lethal is held a hair above
        /// it in the next step (the clock runs: a held clock holds the body, and the bridge with it), nothing is let go, no
        /// one wakes at the wake, and the client is told the held core within the second; thirst still kills.
        /// </summary>
        [Test]
        public void UnderTheBetaArcsBridgeTheColdReachesTheEdgeOfDeathAndNoFurther()
        {
            Rig rig = Connect(bridge: true);
            Stand(rig, 30.0, 12.0);
            rig.Client.SendDevSetting(DevSettings.FounderCoreC, 26.0);
            rig.Pump(TickRate + 2);
            Assert.That(rig.Deaths.Count, Is.EqualTo(0), "no death under the bridge");
            Assert.That(rig.Session.Warmth.CoreC, Is.EqualTo(GameServer.BridgeCoreC).Within(0.1), "held a hair above the lethal, less the second's cooling since");
            Assert.That(rig.Session.Warmth.CoreC, Is.GreaterThan(Warmth.LethalCoreC), "and alive");
            Assert.That(rig.Session.Warmth.Cold, Is.EqualTo(ColdLevel.SeverelyHypothermic), "and the word is the worst but death's");
            Assert.That(rig.Session.Body.East, Is.EqualTo(30.0), "nobody moved");
            Assert.That(rig.Client.LastCoreC, Is.EqualTo(GameServer.BridgeCoreC).Within(0.1), "told the held core");
            rig.Session.Hydration.Restore(1.0 - Hydration.LethalWaterLoss - 0.001);
            rig.Pump(2);
            Assert.That(rig.Deaths.Count, Is.EqualTo(1), "thirst kills under the bridge: water can be drunk");
            Assert.That(rig.Deaths[0].Cause, Is.EqualTo(CauseOfDeath.Thirst));
        }

        [Test]
        public void ThirstsDeathIsToldAsThirst()
        {
            Rig rig = Connect();
            Stand(rig, 0.0, 0.0);
            rig.Session.Hydration.Restore(1.0 - Hydration.LethalWaterLoss - 0.001);
            rig.Pump(2);
            Assert.That(rig.Deaths.Count, Is.EqualTo(1));
            Assert.That(rig.Deaths[0].Cause, Is.EqualTo(CauseOfDeath.Thirst));
            Assert.That(rig.Deaths[0].Explain(), Does.StartWith("You died of thirst at ").And.Contain("15% of the body's water"));
            Assert.That(rig.Session.Hydration.Water01, Is.EqualTo(1.0).Within(1e-4), "a new founder, full");
        }

        [Test]
        public void TheFoundersCoreIsMovedByItsRowAndToldAtOnce()
        {
            Rig rig = Connect();
            rig.World.Clock.Scale = 0.0;
            Stand(rig, 0.0, 0.0);
            rig.Client.SendDevSetting(DevSettings.FounderCoreC, 30.5);
            rig.Pump(2);
            Assert.That(rig.Session.Warmth.CoreC, Is.EqualTo(30.5));
            Assert.That(rig.Client.LastCoreC, Is.EqualTo(30.5));
            Assert.That(rig.Deaths.Count, Is.EqualTo(0), "above the lethal core, no death");
        }

        [Test]
        public void ASavedCoreComesBackAtTheJoin()
        {
            SavedPlayer saved = new SavedPlayer { Name = "William", Body = MoverState.AtRest(0.0, 0.0, 0.0), CoreDeficitC = 2.0 };
            saved.Body.Grounded = true;
            Rig rig = Connect(new[] { saved });
            Assert.That(rig.Session.Warmth.CoreC, Is.EqualTo(35.0).Within(0.05), "two below normal, less the moments since");
            Assert.That(rig.Client.LastCoreC, Is.EqualTo(rig.Session.Warmth.CoreC).Within(0.05));
        }
    }
}
