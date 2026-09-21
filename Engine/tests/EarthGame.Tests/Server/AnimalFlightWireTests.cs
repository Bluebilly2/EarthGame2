using System;
using System.Collections.Generic;
using EarthGame.ClientCore;
using EarthGame.Engine;
using EarthGame.Server;
using EarthGame.Tests.Engine;
using EarthGame.Transport;
using NUnit.Framework;

namespace EarthGame.Tests.Server
{
    /// <summary>
    /// A flight over the wire (M1.7c promise 5, and promise 8's last clause): two clients over the in-memory transport, one
    /// of whom walks up to a mob, both see it take flight, member by member, its pose fleeing and its members moved every
    /// tick, and once it has stopped both hold what the server holds. The corpus of 2026-09-14 walked its loop without
    /// coming within a mob's distance, so this is where the flight is proved to reach a client.
    /// </summary>
    public sealed class AnimalFlightWireTests
    {
        private const double Dt = 0.05;
        private static readonly AnimalSpecies Roo = AnimalStandUpTests.Roo;

        private sealed class Rig
        {
            public GameServer Server;
            public WorldState World;
            public GameClient A, B;
            private long _ms;

            public void Pump(int rounds = 1)
            {
                for (int i = 0; i < rounds; i++)
                {
                    A.Update(_ms);
                    B.Update(_ms);
                    Server.Update(Dt);
                    A.Update(_ms);
                    B.Update(_ms);
                    _ms += 50;
                }
            }
        }

        private static MoverState Standing(WorldState world, double east, double north)
        {
            MoverState s = MoverState.AtRest(east, world.GroundAt(east, north), north);
            s.Grounded = true;
            return s;
        }

        [Test]
        public void TwoClientsSeeTheSameMobRunFromTheOneWhoWalksUpToIt()
        {
            WorldState world = AnimalStandUpTests.World(17.0);
            world.Clock.Scale = 0.0;
            List<AnimalSighting> groups = AnimalStandUpTests.Groups(world, Roo, AnimalStandUpTests.RooPerKm2, 0.0, 0.0, 1000.0);
            Assert.That(groups.Count, Is.GreaterThan(0));
            AnimalSighting g = groups[0];
            ulong[] ids = new ulong[g.GroupSize];
            for (int m = 0; m < ids.Length; m++) ids[m] = AnimalStandUp.IdOf(Roo, g.CellX, g.CellZ, m);

            InMemoryTransport.CreatePair(out IServerTransport st, out IClientTransport ct);
            Rig rig = new Rig { World = world, Server = new GameServer(new ServerConfig(), st, world) };
            rig.Server.Listen(1);
            rig.A = new GameClient(ct);
            rig.A.Connect("memory", 1, "William", "");
            rig.B = new GameClient(InMemoryTransport.CreateClient(st));
            rig.B.Connect("memory", 1, "Guest", "");
            rig.Pump(6);
            Assert.That(rig.A.State, Is.EqualTo(ClientState.Connected));
            Assert.That(rig.B.State, Is.EqualTo(ClientState.Connected));
            List<AnimalFlight> flights = new List<AnimalFlight>();
            rig.Server.Animals().Fled += flights.Add;

            // A two hundred metres north of the mob, B three hundred south: both stand it up, neither startles it.
            double aNorth = g.NorthM + 200.0;
            rig.A.SendMove(MoverInput.None, 180f, 0f, Standing(world, g.EastM, aNorth));
            rig.B.SendMove(MoverInput.None, 0f, 0f, Standing(world, g.EastM, g.NorthM - 300.0));
            int rounds = 0;
            while (rounds++ < 60 && !(rig.A.Entities.Views.ContainsKey(ids[0]) && rig.B.Entities.Views.ContainsKey(ids[0]))) rig.Pump();
            Assert.That(rig.A.Entities.Views.ContainsKey(ids[0]) && rig.B.Entities.Views.ContainsKey(ids[0]), Is.True, "both clients are shown the mob");
            foreach (ulong id in ids)
                Assert.That(rig.A.Entities.Views[id].Animal.Pose, Is.Not.EqualTo(AnimalPose.Fleeing), "and nobody has startled it");
            Assert.That(flights, Is.Empty);

            // A walks south at four metres a second, under the run's ceiling (4.5 since M1.5h's walker's law), until the mob is seen to run.
            double seenAt = double.NaN;
            long seenTick = -1;
            for (int step = 0; step < 1000 && double.IsNaN(seenAt); step++)
            {
                aNorth -= 0.2;
                rig.A.SendMove(MoverInput.Walk(0.0, 1.0), 180f, 0f, Standing(world, g.EastM, aNorth));
                rig.Pump();
                EntityView first = rig.A.Entities.Views[ids[0]];
                if (first.Animal.Pose != AnimalPose.Fleeing) continue;
                double nearest = double.PositiveInfinity;
                foreach (ulong id in ids)
                {
                    EntityView v = rig.A.Entities.Views[id];
                    nearest = Math.Min(nearest, Math.Sqrt((v.Position.X - g.EastM) * (v.Position.X - g.EastM) + (v.Position.Z - aNorth) * (v.Position.Z - aNorth)));
                }
                seenAt = nearest;
                seenTick = first.Tick;
            }
            Assert.That(seenAt, Is.Not.NaN, "A's client saw the mob take flight");
            Assert.That(flights.Count, Is.EqualTo(1), "the server recorded one flight");
            Assert.That(flights[0].DistanceM, Is.LessThanOrEqualTo(AnimalFlightRules.KangarooFleeWithinM));
            Assert.That(seenAt, Is.LessThan(AnimalFlightRules.KangarooFleeWithinM + 10.0), "seen from within the kind's distance, the step's move and the mirror's lag allowed: " + seenAt.ToString("0.0") + " m");
            Assert.That(rig.Server.Sessions[0].Corrections + rig.Server.Sessions[1].Corrections, Is.Zero, "a legal walk");

            // While it runs, both clients are told every tick where each member is, and both see the same fleeing pose.
            rig.A.SendMove(MoverInput.None, 180f, 0f, Standing(world, g.EastM, aNorth));
            long lastA = rig.A.Entities.Views[ids[0]].Tick, lastB = rig.B.Entities.Views[ids[0]].Tick;
            int advancedA = 0, advancedB = 0;
            for (int step = 0; step < 40; step++)
            {
                rig.Pump();
                long tickA = rig.A.Entities.Views[ids[0]].Tick, tickB = rig.B.Entities.Views[ids[0]].Tick;
                if (tickA > lastA) advancedA++;
                if (tickB > lastB) advancedB++;
                lastA = tickA;
                lastB = tickB;
                foreach (ulong id in ids)
                {
                    Assert.That(rig.A.Entities.Views[id].Animal.Pose, Is.EqualTo(AnimalPose.Fleeing), "A sees every member fleeing");
                    Assert.That(rig.B.Entities.Views[id].Animal.Pose, Is.EqualTo(AnimalPose.Fleeing), "and so does B");
                }
            }
            Assert.That(advancedA, Is.GreaterThanOrEqualTo(38), "A was told a new place for the mob nearly every tick: " + advancedA + " of 40");
            Assert.That(advancedB, Is.GreaterThanOrEqualTo(38), "and so was B: " + advancedB + " of 40");

            // Once it has run its length and stands, both clients hold what the server holds, member by member.
            rounds = 0;
            while (rounds++ < 1200 && rig.Server.Animals().IsRunning(Roo, g.CellX, g.CellZ)) rig.Pump();
            Assert.That(rig.Server.Animals().IsRunning(Roo, g.CellX, g.CellZ), Is.False, "the run ends");
            rig.Pump(5);
            foreach (ulong id in ids)
            {
                Assert.That(world.Entities.TryGet(id, out Entity member), Is.True);
                EntityView a = rig.A.Entities.Views[id], b = rig.B.Entities.Views[id];
                Assert.That(a.Position.Equals(member.Position), Is.True, "A holds the server's place for member " + id);
                Assert.That(b.Position.Equals(member.Position), Is.True, "B holds the server's place for member " + id);
                Assert.That(a.Animal.Pose, Is.EqualTo(member.Animal.Pose).And.EqualTo(b.Animal.Pose), "and the server's pose, standing again");
                Assert.That(a.Animal.Pose, Is.Not.EqualTo(AnimalPose.Fleeing));
            }
            Assert.That(rig.A.Entities.Digest(), Is.EqualTo(rig.B.Entities.Digest()), "the two mirrors name the same world");
        }
    }
}
