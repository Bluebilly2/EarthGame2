using EarthGame.ClientCore;
using EarthGame.Engine;
using EarthGame.Protocol;
using EarthGame.Server;
using EarthGame.Transport;
using NUnit.Framework;

namespace EarthGame.Tests.Server
{
    /// <summary>
    /// A developer's settings (M1.D): a server started for development takes one it knows, holds it to its range and applies
    /// it, and refuses one it does not know; a server not started for development refuses them all and closes; and the Pong
    /// carries the server's clock, so a client learns a clock a developer has moved.
    /// </summary>
    public sealed class DevSettingTests
    {
        private sealed class Rig
        {
            public GameServer Server;
            public GameClient Client;
            public WorldState World;
            private long _ms;

            public void Pump(int rounds = 3)
            {
                for (int i = 0; i < rounds; i++)
                {
                    Client.Update(_ms);
                    Server.Update(0.05);
                    Client.Update(_ms);
                    _ms += 50;
                }
            }

            public PlayerSession Session => Server.Sessions[0];
        }

        private static Rig Connect(bool development)
        {
            InMemoryTransport.CreatePair(out IServerTransport st, out IClientTransport ct);
            Rig rig = new Rig();
            rig.World = new WorldState(1, Region.Bherwerre, Region.Bherwerre.WakeClock());
            rig.Server = new GameServer(new ServerConfig { Movement = new MovementRules { AllowFlight = development } }, st, rig.World);
            rig.Server.Listen(1);
            rig.Client = new GameClient(ct);
            rig.Client.Connect("memory", 1, "William", "");
            rig.Pump(5);
            Assert.That(rig.Client.State, Is.EqualTo(ClientState.Connected));
            return rig;
        }

        private static AnimalStandUp Animals(WorldState world)
        {
            foreach (IFastSystem system in world.Systems)
                if (system is AnimalStandUp animals) return animals;
            return null;
        }

        [Test]
        public void ADevelopmentServerTakesASettingItKnowsHeldToItsRange()
        {
            Rig rig = Connect(true);
            AnimalStandUp animals = Animals(rig.World);
            Assert.That(animals.StandUpRadiusM, Is.EqualTo(AnimalStandUp.DefaultStandUpRadiusM));
            rig.Client.SendDevSetting(DevSettings.AnimalsStandUpM, 800.0);
            rig.Pump(2);
            Assert.That(animals.StandUpRadiusM, Is.EqualTo(800.0));
            Assert.That(animals.TakeAwayRadiusM, Is.EqualTo(800.0), "the take-away is never nearer than the stand-up");
            rig.Client.SendDevSetting(DevSettings.AnimalsTakeAwayM, 5000.0);
            rig.Pump(2);
            Assert.That(animals.TakeAwayRadiusM, Is.EqualTo(1600.0), "held to the table's most");
            Assert.That(rig.Client.State, Is.EqualTo(ClientState.Connected), "and the client stays");
        }

        [Test]
        public void TheLocalHourIsSetOnTheSameDayAndThePongCarriesTheClock()
        {
            Rig rig = Connect(true);
            double longitude = Region.Bherwerre.CentreLongitudeDeg;
            int day = rig.World.Clock.LocalDayOfYear(longitude);
            rig.Client.SendDevSetting(DevSettings.ClockLocalHour, 21.5);
            rig.Pump(2);
            Assert.That(rig.World.Clock.LocalHourOfDay(longitude), Is.EqualTo(21.5).Within(0.01), "the hour asked for, at the region's centre");
            Assert.That(rig.World.Clock.LocalDayOfYear(longitude), Is.EqualTo(day), "on the same day");
            rig.Client.Ping(0);
            rig.Pump(2);
            Assert.That(rig.Client.LastServerTotalHours, Is.EqualTo(rig.World.Clock.TotalHours).Within(0.01), "and the pong carries the clock");
        }

        [Test]
        public void StandAtTheWakeCorrectsTheFounderThere()
        {
            Rig rig = Connect(true);
            MoverState away = MoverState.AtRest(100.0, 5.0, 100.0);
            away.Grounded = true;
            rig.Client.SendMove(MoverInput.None, 0f, 0f, away);
            rig.Pump(2);
            Assert.That(rig.Session.Body.East, Is.EqualTo(100.0));
            rig.Client.SendDevSetting(DevSettings.StandAtWake, 0.0);
            rig.Pump(2);
            Double3 wake = rig.World.SpawnPoint();
            Assert.That(rig.Session.Body.East, Is.EqualTo(wake.X).Within(1e-9));
            Assert.That(rig.Session.Body.North, Is.EqualTo(wake.Z).Within(1e-9));
            Assert.That(rig.Client.CorrectionCount, Is.EqualTo(1), "the client is told where it now stands");
            Assert.That(rig.Client.LastCorrection.Reason, Does.Contain("wake"));
            Assert.That(rig.Client.LastCorrection.Body.East, Is.EqualTo(wake.X).Within(1e-9));
        }

        [Test]
        public void ASettingThisBuildDoesNotKnowIsRefused()
        {
            Rig rig = Connect(true);
            rig.Client.SendDevSetting("nothing.of.the.kind", 1.0);
            rig.Pump(3);
            Assert.That(rig.Client.State, Is.EqualTo(ClientState.Refused));
            Assert.That(rig.Client.LastReason, Does.Contain("nothing.of.the.kind"));
        }

        [Test]
        public void AServerNotStartedForDevelopmentRefusesEverySettingAndCloses()
        {
            Rig rig = Connect(false);
            AnimalStandUp animals = Animals(rig.World);
            rig.Client.SendDevSetting(DevSettings.AnimalsStandUpM, 800.0);
            rig.Pump(3);
            Assert.That(rig.Client.State, Is.EqualTo(ClientState.Refused));
            Assert.That(rig.Client.LastReason, Does.Contain("not started for development"));
            Assert.That(animals.StandUpRadiusM, Is.EqualTo(AnimalStandUp.DefaultStandUpRadiusM), "and nothing moved");
        }
    }
}
