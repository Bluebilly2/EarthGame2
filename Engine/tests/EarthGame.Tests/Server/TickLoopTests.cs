using EarthGame.Engine;
using EarthGame.Server;
using EarthGame.Transport;
using NUnit.Framework;

namespace EarthGame.Tests.Server
{
    public sealed class TickLoopTests
    {
        private static GameServer NewServer(int tickRate = 20, int maxSteps = 5)
        {
            InMemoryTransport.CreatePair(out IServerTransport st, out IClientTransport _);
            WorldState world = new WorldState(1, Region.Bherwerre.Id, Region.Bherwerre.WakeClock());
            return new GameServer(new ServerConfig { TickRate = tickRate, MaxStepsPerUpdate = maxSteps }, st, world);
        }

        [Test]
        public void OneRealSecondIsTwentyTicks()
        {
            GameServer server = NewServer();
            for (int i = 0; i < 20; i++) server.Update(0.05);
            Assert.That(server.World.Tick, Is.EqualTo(20));
        }

        [Test]
        public void TicksMoveTheOneClock()
        {
            GameServer server = NewServer();
            double before = server.World.Clock.TotalHours;
            for (int i = 0; i < 20; i++) server.Update(0.05);
            // one real second is 24 h / 1800 s of world time
            Assert.That(server.World.Clock.TotalHours - before, Is.EqualTo(24.0 / 1800.0).Within(1e-9));
        }

        [Test]
        public void PauseStopsTheWorldNotTheTransport()
        {
            GameServer server = NewServer();
            server.Paused = true;
            for (int i = 0; i < 20; i++) server.Update(0.05);
            Assert.That(server.World.Tick, Is.EqualTo(0));
            server.Paused = false;
            server.Update(0.05);
            Assert.That(server.World.Tick, Is.EqualTo(1));
        }

        [Test]
        public void AStallIsDroppedNotCaughtUp()
        {
            GameServer server = NewServer(20, 5);
            server.Update(10.0); // a ten-second hitch
            Assert.That(server.World.Tick, Is.EqualTo(5));
            Assert.That(server.DroppedSeconds, Is.EqualTo(10.0 - 5 * 0.05).Within(1e-9));
        }

        [Test]
        public void FractionalTimeAccumulates()
        {
            FixedStepAccumulator acc = new FixedStepAccumulator(0.05, 5);
            int steps = 0;
            for (int i = 0; i < 3; i++)
            {
                acc.Accumulate(0.02);
                while (acc.TryStep()) steps++;
            }
            Assert.That(steps, Is.EqualTo(1)); // 0.06 s owes one 0.05 s step
            Assert.That(acc.DroppedSeconds, Is.EqualTo(0.0));
        }

        [Test]
        public void SteppedEventFiresPerTick()
        {
            GameServer server = NewServer();
            int fired = 0;
            server.Stepped += (w, dt) => { fired++; Assert.That(dt, Is.EqualTo(0.05).Within(1e-12)); };
            server.Update(0.15);
            Assert.That(fired, Is.EqualTo(3));
        }
    }
}
