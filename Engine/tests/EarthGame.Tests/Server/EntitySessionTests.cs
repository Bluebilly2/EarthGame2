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
    /// Two clients over the in-memory transport (M1.3 promises 5, 6 and 8): entities inside the interest radius
    /// are shown, changes follow, leaving and dying are told, and the mirrors name what the server names; since M1.7a,
    /// the animals stood up round the founders too.
    /// </summary>
    public sealed class EntitySessionTests
    {
        private static readonly Region Fixture = new Region("fixture", "Fixture", Region.Bherwerre.CentreLatitudeDeg,
            Region.Bherwerre.CentreLongitudeDeg, TestRasters.MadeExtentM, 237, 8.0);

        private Heightfield _ground;

        [SetUp]
        public void SetUp() => _ground = new Heightfield(TestRasters.MadeCoast());

        private sealed class Rig
        {
            public GameServer Server;
            public IServerTransport ServerTransport;
            public GameClient A;
            public GameClient B;
            public long Ms;

            public void Pump(int rounds = 3, double dt = 0.05)
            {
                for (int i = 0; i < rounds; i++)
                {
                    A?.Update(Ms);
                    B?.Update(Ms);
                    Server.Update(dt);
                    A?.Update(Ms);
                    B?.Update(Ms);
                    Ms += (long)(dt * 1000);
                }
            }

            public GameClient Join(string name)
            {
                IClientTransport ct = InMemoryTransport.CreateClient(ServerTransport);
                GameClient c = new GameClient(ct);
                c.Connect("memory", 1, name, "");
                return c;
            }
        }

        /// <param name="wrap">What the server is handed in place of the in-memory transport, which the clients still join.</param>
        private Rig Start(ServerConfig config, CapacitySquares capacity = null, Func<IServerTransport, IServerTransport> wrap = null)
        {
            WorldState world = new WorldState(1347UL, Fixture, Fixture.WakeClock(), _ground, capacity: capacity);
            InMemoryTransport.CreatePair(out IServerTransport st, out IClientTransport ct);
            Rig rig = new Rig { ServerTransport = st };
            rig.Server = new GameServer(config, wrap != null ? wrap(st) : st, world);
            rig.Server.Listen(1);
            return rig;
        }

        private static ServerConfig Config() => new ServerConfig { InterestRadiusM = 100.0, InterestMarginM = 50.0 };

        private SavedPlayer At(string name, double east, double north)
        {
            SavedPlayer p = default;
            p.Name = name;
            p.Body = MoverState.AtRest(east, _ground.HeightAt(east, north), north);
            p.Body.Grounded = true;
            p.YawDeg = 0f;
            p.PitchDeg = 0f;
            p.SavedTick = 0;
            return p;
        }

        private PlayerSession Session(Rig rig, string name)
        {
            foreach (PlayerSession s in rig.Server.Sessions) if (s.Name == name) return s;
            return null;
        }

        [Test]
        public void BothClientsMirrorTheEntitiesNearThemAndNameThemAsTheServerDoes()
        {
            Rig rig = Start(Config());
            rig.Server.RememberPlayers(new[] { At("A", 300, -300), At("B", 320, -300) });
            rig.Server.SpawnItem("item/cobble", 305, -305);
            rig.Server.SpawnItem("item/stick", 290, -290);
            rig.Server.SpawnItem("item/cobble", 300, -300, _ground.HeightAt(300, -300) + 2.0);
            rig.Server.SpawnItem("item/stick", 700, 300, null);   // 700 m away: nobody's
            rig.A = rig.Join("A");
            rig.B = rig.Join("B");
            List<(EntityView, byte)> goneA = new List<(EntityView, byte)>();
            rig.A.Entities.Gone += (v, why) => goneA.Add((v, why));
            rig.Pump(30);
            Assert.That(rig.A.State, Is.EqualTo(ClientState.Connected));
            Assert.That(rig.A.Entities.Count, Is.EqualTo(3), "the three near the wake, not the far one");
            Assert.That(rig.B.Entities.Count, Is.EqualTo(3));
            Assert.That(rig.A.Entities.Digest(), Is.EqualTo(rig.Server.EntityDigest(Session(rig, "A"))), "the mirror names what the server names");
            Assert.That(rig.B.Entities.Digest(), Is.EqualTo(rig.A.Entities.Digest()), "and both clients name the same");
            EntityView fallen = rig.A.Entities.Views[3];
            Assert.That(fallen.Item.Resting, Is.True, "the dropped cobble came to rest and the rest was told");
            Assert.That(fallen.Position.Y, Is.EqualTo(_ground.HeightAt(300, -300)).Within(1e-9));
            Assert.That(fallen.Definition, Is.SameAs(DefinitionCatalogue.Cobble));

            rig.Server.SpawnItem("item/stick", 310, -310);
            rig.Pump(2);
            Assert.That(rig.A.Entities.Count, Is.EqualTo(4), "a spawn after the join is shown");
            Assert.That(rig.B.Entities.Count, Is.EqualTo(4));

            Assert.That(rig.Server.KillEntity(1), Is.True);
            Assert.That(rig.Server.KillEntity(1), Is.False, "once");
            rig.Pump(2);
            Assert.That(rig.A.Entities.Count, Is.EqualTo(3), "a killed entity leaves both mirrors");
            Assert.That(rig.B.Entities.Count, Is.EqualTo(3));
            Assert.That(goneA.Count, Is.EqualTo(1));
            Assert.That(goneA[0].Item2, Is.EqualTo(EntityGoneMessage.Died));
            Assert.That(goneA[0].Item1.Id.Value, Is.EqualTo(1UL));
            Assert.That(rig.A.Entities.Digest(), Is.EqualTo(rig.Server.EntityDigest(Session(rig, "A"))));
            Assert.That(rig.A.CorrectionCount, Is.EqualTo(0));
        }

        [Test]
        public void AClientThatWalksOutOfReachIsToldTheEntitiesAreGoneAndSeesThemAgainWhenBack()
        {
            Rig rig = Start(Config());
            rig.Server.RememberPlayers(new[] { At("A", 300, -300) });
            rig.Server.SpawnItem("item/cobble", 305, -305);
            rig.Server.SpawnItem("item/stick", 290, -290);
            rig.A = rig.Join("A");
            List<byte> reasons = new List<byte>();
            rig.A.Entities.Gone += (v, why) => reasons.Add(why);
            rig.Pump(5);
            Assert.That(rig.A.Entities.Count, Is.EqualTo(2));

            MoverState body = MoverState.AtRest(300, _ground.HeightAt(300, -300), -300);
            body.Grounded = true;
            MoverInput input = default;
            input.WishEast = 1.0;
            int goneAtStep = -1;
            for (int step = 1; step <= 1800; step++)
            {
                body.East += 0.1;    // 2 m/s, a walk
                body.Up = _ground.HeightAt(body.East, body.North);
                rig.A.SendMove(input, 90f, 0f, body);
                rig.Pump(1);
                if (goneAtStep < 0 && rig.A.Entities.Count == 0) goneAtStep = step;
            }
            Assert.That(rig.A.CorrectionCount, Is.EqualTo(0), "the walk was legal: " + rig.A.LastCorrection.Reason);
            Assert.That(rig.A.Entities.Count, Is.EqualTo(0), "180 m away, both are out of reach");
            Assert.That(goneAtStep, Is.InRange(1450, 1600), "gone once 150 m away (the radius and the margin), at step " + goneAtStep);
            Assert.That(reasons, Is.EquivalentTo(new[] { EntityGoneMessage.Left, EntityGoneMessage.Left }));

            input.WishEast = -1.0;
            int backAtStep = -1;
            for (int step = 1; step <= 1800; step++)
            {
                body.East -= 0.1;
                body.Up = _ground.HeightAt(body.East, body.North);
                rig.A.SendMove(input, 270f, 0f, body);
                rig.Pump(1);
                if (backAtStep < 0 && rig.A.Entities.Count == 2) backAtStep = step;
            }
            Assert.That(rig.A.Entities.Count, Is.EqualTo(2), "back at the wake they are shown again");
            Assert.That(backAtStep, Is.InRange(880, 960), "shown again once both are within 100 m, at step " + backAtStep);
            Assert.That(rig.A.Entities.Digest(), Is.EqualTo(rig.Server.EntityDigest(Session(rig, "A"))));
        }

        [Test]
        public void TheByteBudgetDefersSpawnsWithoutLosingThem()
        {
            ServerConfig config = Config();
            config.EntityBytesPerTick = 74;    // one spawn of 69 bytes per tick (55 before protocol 15 carried a stone's state on an item)
            Rig rig = Start(config);
            rig.Server.RememberPlayers(new[] { At("A", 300, -300) });
            rig.A = rig.Join("A");
            rig.Pump(5);
            for (int i = 0; i < 10; i++) rig.Server.SpawnItem(i % 2 == 0 ? "item/cobble" : "item/stick", 300 + i, -300 - i);
            rig.Pump(1);
            Assert.That(rig.A.Entities.Count, Is.EqualTo(1), "one step shows one");
            rig.Pump(3);
            Assert.That(rig.A.Entities.Count, Is.EqualTo(4), "one more per step");
            rig.Pump(8);
            Assert.That(rig.A.Entities.Count, Is.EqualTo(10), "the rest follow, none lost");
            Assert.That(rig.A.Entities.Digest(), Is.EqualTo(rig.Server.EntityDigest(Session(rig, "A"))));
        }

        /// <summary>The kangaroo and the oystercatcher fed alike over the made coast, as a world folder's capacity layers would feed them.</summary>
        private static CapacitySquares Fed(float rooPerKm2 = 30f, float birdPerKm2 = 10f)
        {
            CapacitySquares capacity = new CapacitySquares(TestRasters.MadeExtentM);
            capacity.Add(AnimalSpecies.EasternGreyKangaroo, TestRasters.FromLaw(TestRasters.MadeSide, TestRasters.MadeCellM, TestRasters.MadeExtentM, "capacity_roo", (row, col) => rooPerKm2, CapacitySquares.Unit));
            capacity.Add(AnimalSpecies.PiedOystercatcher, TestRasters.FromLaw(TestRasters.MadeSide, TestRasters.MadeCellM, TestRasters.MadeExtentM, "capacity_bird", (row, col) => birdPerKm2, CapacitySquares.Unit));
            return capacity;
        }

        [Test]
        public void BothClientsAreShownTheSameAnimalsAndTheOnesAFounderLeavesBehindHaveLeft()
        {
            // The default reach, which from either founder takes in every animal standing round both.
            Rig rig = Start(new ServerConfig(), Fed());
            rig.Server.RememberPlayers(new[] { At("A", -400, -300), At("B", 400, -300) });
            rig.A = rig.Join("A");
            rig.B = rig.Join("B");
            List<(EntityView, byte)> goneA = new List<(EntityView, byte)>();
            rig.A.Entities.Gone += (v, why) => goneA.Add((v, why));
            rig.Pump(30);
            IReadOnlyList<Entity> animals = rig.Server.World.Entities.Transient;
            Assert.That(rig.A.State, Is.EqualTo(ClientState.Connected));
            Assert.That(animals.Count, Is.GreaterThan(0), "animals stand round the founders");
            Assert.That(rig.Server.World.Entities.NextId, Is.EqualTo(1UL), "and never move the world's count");
            Assert.That(rig.A.Entities.Count, Is.EqualTo(animals.Count), "A is shown every one");
            Assert.That(rig.B.Entities.Count, Is.EqualTo(animals.Count), "and so is B");
            Assert.That(rig.B.Entities.Digest(), Is.EqualTo(rig.A.Entities.Digest()), "the same animals, in the same places and poses");
            Assert.That(rig.A.Entities.Digest(), Is.EqualTo(rig.Server.EntityDigest(Session(rig, "A"))), "the mirror names what the server names");
            foreach (EntityView v in rig.A.Entities.Views.Values)
            {
                Assert.That(v.HasAnimal, Is.True, "each is shown as an animal: " + v.Definition.Key);
                Assert.That(v.Animal.Pose, Is.EqualTo(AnimalPose.Resting).Or.EqualTo(AnimalPose.Grazing));
            }

            int before = animals.Count;
            rig.B.Disconnect("home");
            rig.B = null;
            rig.Pump(25);
            Assert.That(animals.Count, Is.GreaterThan(0).And.LessThan(before), "the animals round B alone are taken away within the second");
            Assert.That(goneA.Count, Is.EqualTo(before - animals.Count), "and A is told of each");
            foreach ((EntityView view, byte why) in goneA)
            {
                Assert.That(why, Is.EqualTo(EntityGoneMessage.Left), "an animal taken away left; it did not die");
                Assert.That(EntityId.IsTransientValue(view.Id.Value), Is.True);
            }
            Assert.That(rig.A.Entities.Count, Is.EqualTo(animals.Count));
            Assert.That(rig.A.Entities.Digest(), Is.EqualTo(rig.Server.EntityDigest(Session(rig, "A"))));
            Assert.That(rig.A.CorrectionCount, Is.EqualTo(0));
        }

        [Test]
        public void AnAnimalsChangeOfPoseArrivesThoughEveryUnreliableMessageIsLost()
        {
            DeliveryRecorder link = null;
            Rig rig = Start(new ServerConfig(), Fed(rooPerKm2: 100f), inner => link = new DeliveryRecorder(inner));
            WorldState world = rig.Server.World;
            // Seven and a half seconds of the host's before the kangaroos get up for the evening, which the join and the first
            // stand-ups take less of: at the game's rate, since ruling 52 a real day, as many seconds of the world's.
            SolarClock sun = SolarClock.ForRegion(world.Region, world.Clock);
            double up = 12.0;
            while (up < 24.0 && AnimalPresence.Activity01(AnimalSpecies.EasternGreyKangaroo, up, sun.DaylightHours) < AnimalStandUp.GrazingActivity) up += 0.0001;
            double beforeHours = 7.5 * 24.0 / WorldClock.RealSecondsPerDay;
            world.Clock.Advance((up - beforeHours - sun.HourOfDay) / 24.0 * WorldClock.RealSecondsPerDay);
            rig.Server.RememberPlayers(new[] { At("A", 300, -300) });
            link.LoseUnreliable = true;
            rig.A = rig.Join("A");
            rig.Pump(60);
            Definition kangaroo = DefinitionCatalogue.AnimalOf(AnimalSpecies.EasternGreyKangaroo);
            List<ulong> lyingUp = new List<ulong>();
            foreach (EntityView v in rig.A.Entities.Views.Values)
                if (v.Definition == kangaroo && v.Animal.Pose == AnimalPose.Resting) lyingUp.Add(v.Id.Value);
            Assert.That(rig.A.State, Is.EqualTo(ClientState.Connected));
            Assert.That(lyingUp.Count, Is.GreaterThan(0), "kangaroos lying up in the afternoon, and shown so");

            rig.Pump(200);
            int gotUp = 0;
            foreach (EntityView v in rig.A.Entities.Views.Values)
            {
                Assert.That(world.Entities.TryGet(v.Id.Value, out Entity e), Is.True, "the mirror holds only what stands: " + v.Id);
                Assert.That(v.Animal.Pose, Is.EqualTo(e.Animal.Pose), "each shown in the pose the server holds: " + v.Id);
                if (lyingUp.Contains(v.Id.Value) && e.Animal.Pose == AnimalPose.Grazing) gotUp++;
            }
            Assert.That(gotUp, Is.GreaterThan(0), "kangaroos got up while the client watched");
            Assert.That(link.Sent.Exists(s => s.Lost), Is.True, "while every unreliable message was lost");
            Assert.That(link.Sent.Exists(s => s.Payload[0] == (byte)MessageKind.EntityState && (s.Payload[17] & (byte)EntityFields.Pose) != 0 && s.Delivery == Delivery.Reliable),
                Is.True, "because the states that carried the change went reliably");
        }
    }
}
