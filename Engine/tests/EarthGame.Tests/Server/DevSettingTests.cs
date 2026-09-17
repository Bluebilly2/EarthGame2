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
        public void TheAnimalsFlightRulesAreMovedByTheirRows()
        {
            Rig rig = Connect(true);
            AnimalStandUp animals = Animals(rig.World);
            AnimalFlightRules roo = animals.RulesFor(AnimalSpecies.EasternGreyKangaroo), bird = animals.RulesFor(AnimalSpecies.PiedOystercatcher);
            Assert.That(roo.FleeWithinM, Is.EqualTo(AnimalFlightRules.KangarooFleeWithinM));
            rig.Client.SendDevSetting(DevSettings.KangarooFleeWithinM, 40.0);
            rig.Client.SendDevSetting(DevSettings.KangarooRunM, 300.0);
            rig.Client.SendDevSetting(DevSettings.KangarooRunMs, 9.0);
            rig.Client.SendDevSetting(DevSettings.OystercatcherFleeWithinM, 30.0);
            rig.Client.SendDevSetting(DevSettings.OystercatcherRunM, 250.0);
            rig.Client.SendDevSetting(DevSettings.OystercatcherRunMs, 900.0);
            rig.Pump(2);
            Assert.That(roo.FleeWithinM, Is.EqualTo(40.0));
            Assert.That(roo.RunM, Is.EqualTo(300.0));
            Assert.That(roo.RunMs, Is.EqualTo(9.0));
            Assert.That(bird.FleeWithinM, Is.EqualTo(30.0));
            Assert.That(bird.RunM, Is.EqualTo(250.0));
            Assert.That(bird.RunMs, Is.EqualTo(30.0), "held to the table's most");
        }

        [Test]
        public void TheFoundersWaterIsMovedByItsRowForTheOneWhoMovesItAndHeldToItsLeast()
        {
            Rig rig = Connect(true);
            rig.World.Clock.Scale = 0.0;
            Assert.That(rig.Session.Hydration.Water01, Is.EqualTo(1.0));
            rig.Client.SendDevSetting(DevSettings.FounderWater, 0.9);
            rig.Pump(2);
            Assert.That(rig.Session.Hydration.Water01, Is.EqualTo(0.9));
            Assert.That(rig.Client.LastWater01, Is.EqualTo(0.9), "told at once");
            rig.Client.SendDevSetting(DevSettings.FounderWater, 0.2);
            rig.Pump(2);
            Assert.That(rig.Session.Hydration.Water01, Is.EqualTo(0.8), "held to the row's least, a fifth lost being past collapse");
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
        public void TheDayOfTheYearIsSetAtTheSameHourAndTheScaleRunsTheClockAndRidesThePong()
        {
            Rig rig = Connect(true);
            double longitude = Region.Bherwerre.CentreLongitudeDeg;
            double hour = rig.World.Clock.LocalHourOfDay(longitude);
            rig.Client.SendDevSetting(DevSettings.ClockDayOfYear, 120.0);
            rig.Pump(2);
            Assert.That(rig.World.Clock.LocalDayOfYear(longitude), Is.EqualTo(120), "the day asked for");
            Assert.That(rig.World.Clock.LocalHourOfDay(longitude), Is.EqualTo(hour).Within(0.01), "at the same hour");

            double before = rig.World.Clock.TotalHours;
            rig.Client.SendDevSetting(DevSettings.ClockScale, 12.0);
            rig.Pump(1);
            Assert.That(rig.World.Clock.Scale, Is.EqualTo(12.0));
            double marked = rig.World.Clock.TotalHours;
            rig.Pump(10);
            double moved = rig.World.Clock.TotalHours - marked;
            Assert.That(moved, Is.EqualTo(10 * 0.05 * 12.0 * 24.0 / WorldClock.RealSecondsPerDay).Within(1e-9), "ten steps at twelve times the rate");
            Assert.That(before, Is.LessThan(marked));
            rig.Client.Ping(0);
            rig.Pump(2);
            Assert.That(rig.Client.LastClockScale, Is.EqualTo(12.0), "and the pong carries the scale");
            rig.Client.SendDevSetting(DevSettings.ClockScale, 0.0);
            rig.Pump(1);
            marked = rig.World.Clock.TotalHours;
            rig.Pump(5);
            Assert.That(rig.World.Clock.TotalHours, Is.EqualTo(marked), "nought holds the sky still");
        }

        [Test]
        public void ASpawnSetsAThingTwoMetresAheadOfTheFounder()
        {
            Rig rig = Connect(true);
            MoverState standing = MoverState.AtRest(100.0, 0.0, 100.0);
            standing.Grounded = true;
            rig.Client.SendMove(MoverInput.None, 90f, 0f, standing);
            rig.Pump(2);
            rig.Client.SendDevSetting(DevSettings.SpawnStick, 0.0);
            rig.Pump(2);
            Assert.That(rig.World.Entities.All.Count, Is.EqualTo(1), "one thing spawned");
            Entity stick = rig.World.Entities.All[0];
            Assert.That(stick.Definition, Is.SameAs(DefinitionCatalogue.Stick));
            Assert.That(stick.Position.X, Is.EqualTo(102.0).Within(1e-6), "two metres east, the way the founder faces");
            Assert.That(stick.Position.Z, Is.EqualTo(100.0).Within(1e-6));
            rig.Client.SendDevSetting(DevSettings.SpawnCobble, 0.0);
            rig.Pump(2);
            Assert.That(rig.World.Entities.All.Count, Is.EqualTo(2));
            Assert.That(rig.World.Entities.All[1].Definition, Is.SameAs(DefinitionCatalogue.Cobble));
            // FP.3: a cobble that knaps, the coast's silcrete, for a flake to be struck and looked at.
            rig.Client.SendDevSetting(DevSettings.SpawnSilcreteCobble, 0.0);
            rig.Pump(2);
            Assert.That(rig.World.Entities.All.Count, Is.EqualTo(3));
            Assert.That(rig.World.Entities.All[2].Definition, Is.SameAs(DefinitionCatalogue.CobbleOf(StoneType.Silcrete)));
            Assert.That(rig.World.Entities.All[2].Position.X, Is.EqualTo(102.0).Within(1e-6), "the same two metres ahead");
            Assert.That(rig.World.Entities.All[2].Item.Resting, Is.True);
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
