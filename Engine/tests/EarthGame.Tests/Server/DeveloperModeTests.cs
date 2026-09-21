using EarthGame.ClientCore;
using EarthGame.Engine;
using EarthGame.Protocol;
using EarthGame.Server;
using EarthGame.Transport;
using NUnit.Framework;

namespace EarthGame.Tests.Server
{
    /// <summary>
    /// Developer mode switched inside the game (M1.E promise 2, CANON ruling 39): a server that may grant it grants it to
    /// the player who asks, and to nobody else; a player who has not asked, or has turned it off, flies no further than
    /// anyone and moves no developer's setting; and a server that may not grant it says no without closing on a player
    /// who pressed a key every game answers.
    /// </summary>
    public sealed class DeveloperModeTests
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

        private static Rig Connect(bool mayGrant)
        {
            InMemoryTransport.CreatePair(out IServerTransport st, out IClientTransport ct);
            Rig rig = new Rig();
            rig.World = new WorldState(1, Region.Bherwerre, Region.Bherwerre.WakeClock());
            rig.Server = new GameServer(new ServerConfig { Movement = new MovementRules { AllowFlight = mayGrant } }, st, rig.World);
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
        public void APlayerStartsWithDeveloperModeOffEvenOnAServerThatMayGrantIt()
        {
            Rig rig = Connect(mayGrant: true);
            Assert.That(rig.Session.DeveloperMode, Is.False, "nobody is a developer until they ask");
            Assert.That(rig.Client.DeveloperMode, Is.False);
        }

        [Test]
        public void AServerThatMayGrantItGrantsItAndSaysSo()
        {
            Rig rig = Connect(mayGrant: true);
            rig.Client.SendDeveloperMode(true);
            rig.Pump(2);
            Assert.That(rig.Session.DeveloperMode, Is.True);
            Assert.That(rig.Client.DeveloperMode, Is.True, "the client is told, or it cannot show the switch");
            Assert.That(rig.Client.DeveloperModeRefused, Is.False);

            rig.Client.SendDeveloperMode(false);
            rig.Pump(2);
            Assert.That(rig.Session.DeveloperMode, Is.False, "and takes it back when asked");
            Assert.That(rig.Client.DeveloperMode, Is.False);
        }

        /// <summary>
        /// F2 is a key every game answers, so a server that may not grant developer mode refuses it and keeps the player:
        /// unlike a developer's setting sent to such a server, which no client of this game sends and which still closes.
        /// </summary>
        [Test]
        public void AServerThatMayNotGrantItSaysNoAndKeepsThePlayer()
        {
            Rig rig = Connect(mayGrant: false);
            rig.Client.SendDeveloperMode(true);
            rig.Pump(2);
            Assert.That(rig.Session.DeveloperMode, Is.False);
            Assert.That(rig.Client.DeveloperMode, Is.False);
            Assert.That(rig.Client.DeveloperModeRefused, Is.True, "the refusal is said, so the switch can say it");
            Assert.That(rig.Client.State, Is.EqualTo(ClientState.Connected), "and the player stays");
        }

        [Test]
        public void ASettingIsMovedOnlyWhileTheSwitchIsOn()
        {
            Rig rig = Connect(mayGrant: true);
            AnimalStandUp animals = Animals(rig.World);
            rig.Client.SendDevSetting(DevSettings.AnimalsStandUpM, 800.0);
            rig.Pump(2);
            Assert.That(animals.StandUpRadiusM, Is.EqualTo(AnimalStandUp.DefaultStandUpRadiusM), "off: nothing moved");
            Assert.That(rig.Client.State, Is.EqualTo(ClientState.Connected), "and a setting raced past the switch does not close");

            rig.Client.SendDeveloperMode(true);
            rig.Client.SendDevSetting(DevSettings.AnimalsStandUpM, 800.0);
            rig.Pump(2);
            Assert.That(animals.StandUpRadiusM, Is.EqualTo(800.0), "on: taken, in the order sent");
        }

        [Test]
        public void FlightIsAllowedOnlyWhileTheSwitchIsOn()
        {
            Rig rig = Connect(mayGrant: true);
            Assert.That(rig.Server.MovementRulesFor(rig.Session).AllowFlight, Is.False, "off: a flying founder is corrected");
            rig.Client.SendDeveloperMode(true);
            rig.Pump(2);
            Assert.That(rig.Server.MovementRulesFor(rig.Session).AllowFlight, Is.True, "on: flight is let be");
            rig.Client.SendDeveloperMode(false);
            rig.Pump(2);
            Assert.That(rig.Server.MovementRulesFor(rig.Session).AllowFlight, Is.False, "off again: corrected again");
        }
    }
}
