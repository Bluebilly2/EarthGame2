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
    /// Carrying between founders over the in-memory transport (M1.5a promises 2 to 4): a verb is committed by the
    /// server and answered under its sequence, another founder is told the thing was taken up, it comes back with its
    /// own id when put down, and what is carried is kept through leaving, a save and a load.
    /// </summary>
    public sealed class CarryingSessionTests
    {
        private static readonly Region Fixture = new Region("fixture", "Fixture", Region.Bherwerre.CentreLatitudeDeg,
            Region.Bherwerre.CentreLongitudeDeg, TestRasters.MadeExtentM, 237, 8.0);
        private const string Now = "2026-09-11T10:00:00Z";

        private Heightfield _ground;
        private string _dir;

        [SetUp]
        public void SetUp()
        {
            _ground = new Heightfield(TestRasters.MadeCoast());
            _dir = Path.Combine(Path.GetTempPath(), "EarthGame2.Tests", "carrying", Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        private sealed class Rig
        {
            public GameServer Server;
            public IServerTransport ServerTransport;
            public readonly List<GameClient> Clients = new List<GameClient>();
            public long Ms;

            public void Pump(int rounds = 3, double dt = 0.05)
            {
                for (int i = 0; i < rounds; i++)
                {
                    foreach (GameClient c in Clients) c.Update(Ms);
                    Server.Update(dt);
                    foreach (GameClient c in Clients) c.Update(Ms);
                    Ms += (long)(dt * 1000);
                }
            }

            public GameClient Join(string name)
            {
                GameClient c = new GameClient(InMemoryTransport.CreateClient(ServerTransport));
                c.Connect("memory", 1, name, "");
                Clients.Add(c);
                return c;
            }
        }

        private Rig Start(WorldState world = null)
        {
            InMemoryTransport.CreatePair(out IServerTransport st, out IClientTransport _);
            Rig rig = new Rig { ServerTransport = st };
            rig.Server = new GameServer(new ServerConfig { InterestRadiusM = 100.0, InterestMarginM = 50.0 }, st,
                world ?? new WorldState(1347UL, Fixture, Fixture.WakeClock(), _ground));
            rig.Server.Listen(1);
            return rig;
        }

        private SavedPlayer At(string name, double east, double north)
        {
            SavedPlayer p = default;
            p.Name = name;
            p.Body = MoverState.AtRest(east, _ground.HeightAt(east, north), north);
            p.Body.Grounded = true;
            return p;
        }

        private Double3 Ground(double east, double north) => new Double3(east, _ground.HeightAt(east, north), north);

        private static PlayerSession Session(Rig rig, string name)
        {
            foreach (PlayerSession s in rig.Server.Sessions) if (s.Name == name) return s;
            return null;
        }

        private static IntentMessage PickUp(ulong id) => new IntentMessage { Verb = Verb.PickUp, Target = IntentMessage.TargetEntity, EntityId = id };
        private static IntentMessage PutDown(Double3 at) => new IntentMessage { Verb = Verb.PutDown, East = at.X, Up = at.Y, North = at.Z };
        private static IntentMessage Hold(byte place) => new IntentMessage { Verb = Verb.Hold, Place = place };

        /// <summary>Sends an intent, pumps until its answer is back, and returns the outcome.</summary>
        private static VerbOutcome Do(Rig rig, GameClient client, IntentMessage intent)
        {
            uint sequence = client.SendIntent(intent);
            Assert.That(sequence, Is.Not.EqualTo(0u), "sent while connected");
            for (int i = 0; i < 10 && client.LastIntentResult.Sequence != sequence; i++) rig.Pump(1);
            Assert.That(client.LastIntentResult.Sequence, Is.EqualTo(sequence), "answered");
            return client.LastIntentResult.Outcome;
        }

        [Test]
        public void AThingTakenUpIsGoneForTheOtherFounderAndComesBackWithItsIdWhenPutDown()
        {
            Rig rig = Start();
            rig.Server.RememberPlayers(new[] { At("A", 300, -300), At("B", 303, -300) });
            ulong cobble = rig.Server.SpawnItem("item/cobble", 301, -300).Id.Value;
            ulong stick = rig.Server.SpawnItem("item/stick", 305, -305).Id.Value;
            GameClient a = rig.Join("A"), b = rig.Join("B");
            List<(ulong, byte)> goneB = new List<(ulong, byte)>();
            b.Entities.Gone += (v, why) => goneB.Add((v.Id.Value, why));
            int told = 0;
            a.CarryingChanged += _ => told++;
            rig.Pump(30);
            Assert.That(a.State, Is.EqualTo(ClientState.Connected));
            Assert.That(told, Is.EqualTo(1), "the hands are told at the join");
            Assert.That(a.Carrying.Things, Is.Empty);
            Assert.That(b.Entities.Count, Is.EqualTo(2));

            Assert.That(Do(rig, a, PickUp(stick)), Is.EqualTo(VerbOutcome.OutOfReach), "7 m from A's eye");
            Assert.That(Do(rig, a, PickUp(cobble)), Is.EqualTo(VerbOutcome.Done));
            rig.Pump(2);
            Assert.That(a.Carrying.Hand, Is.EqualTo((byte)1));
            Assert.That(a.Carrying.Things.Length, Is.EqualTo(1));
            Assert.That(a.Carrying.Things[0].Id, Is.EqualTo(cobble));
            Assert.That(a.Carrying.Things[0].Definition, Is.SameAs(DefinitionCatalogue.Cobble));
            Assert.That(goneB, Is.EqualTo(new[] { (cobble, EntityGoneMessage.TakenUp) }), "B is told it was taken up, not that it died");
            Assert.That(a.Entities.Views.ContainsKey(cobble), Is.False);
            Assert.That(rig.Server.World.Entities.TryGet(cobble, out _), Is.False, "out of the world");
            Assert.That(Do(rig, b, PickUp(cobble)), Is.EqualTo(VerbOutcome.NotThere), "B cannot take what A holds");

            Double3 there = Ground(300, -298);
            Assert.That(Do(rig, a, PutDown(there)), Is.EqualTo(VerbOutcome.Done));
            rig.Pump(20);
            Assert.That(a.Carrying.Things, Is.Empty);
            Assert.That(b.Entities.Views.TryGetValue(cobble, out EntityView seen), Is.True, "back, with its own id");
            Assert.That(seen.Item.Resting, Is.True, "fallen and at rest");
            Assert.That(seen.Position.Y, Is.EqualTo(there.Y).Within(1e-9));
            Assert.That(seen.Position.Z, Is.EqualTo(-298.0));
            Assert.That(a.Entities.Digest(), Is.EqualTo(rig.Server.EntityDigest(Session(rig, "A"))));
            Assert.That(b.Entities.Digest(), Is.EqualTo(rig.Server.EntityDigest(Session(rig, "B"))));
            Assert.That(rig.Server.World.Entities.NextId, Is.EqualTo(3UL), "no id was allocated for the return");

            Assert.That(Do(rig, a, PutDown(there)), Is.EqualTo(VerbOutcome.NothingInHand));
            Assert.That(Do(rig, a, Hold(10)), Is.EqualTo(VerbOutcome.NoSuchPlace));
            Assert.That(Do(rig, a, Hold(0)), Is.EqualTo(VerbOutcome.Done));
            rig.Pump(1);
            Assert.That(a.Carrying.Hand, Is.EqualTo((byte)0));
            Assert.That(told, Is.EqualTo(4), "once at the join and once for each verb that changed the hands");
            Assert.That(a.CorrectionCount, Is.EqualTo(0));
        }

        [Test]
        public void NothingIsDoneWhileTheWorldIsHeld()
        {
            Rig rig = Start();
            rig.Server.RememberPlayers(new[] { At("A", 300, -300) });
            ulong cobble = rig.Server.SpawnItem("item/cobble", 301, -300).Id.Value;
            GameClient a = rig.Join("A");
            rig.Pump(10);
            rig.Server.Paused = true;
            Assert.That(Do(rig, a, PickUp(cobble)), Is.EqualTo(VerbOutcome.NotNow));
            Assert.That(rig.Server.World.Entities.TryGet(cobble, out Entity e) && !e.Killed, Is.True, "still lying there");
            rig.Server.Paused = false;
            Assert.That(Do(rig, a, PickUp(cobble)), Is.EqualTo(VerbOutcome.Done));
        }

        [Test]
        public void WhatIsCarriedIsKeptThroughLeavingASaveAndALoad()
        {
            Rig rig = Start();
            rig.Server.RememberPlayers(new[] { At("A", 300, -300) });
            ulong cobble = rig.Server.SpawnItem("item/cobble", 301, -300).Id.Value;
            ulong stick = rig.Server.SpawnItem("item/stick", 299, -300).Id.Value;
            GameClient a = rig.Join("A");
            rig.Pump(10);
            Assert.That(Do(rig, a, PickUp(cobble)), Is.EqualTo(VerbOutcome.Done));
            Assert.That(Do(rig, a, PickUp(stick)), Is.EqualTo(VerbOutcome.Done));
            Assert.That(Do(rig, a, Hold(2)), Is.EqualTo(VerbOutcome.Done));
            rig.Pump(2);

            a.Disconnect("bed");
            rig.Pump(2);
            Assert.That(rig.Server.Sessions.Count, Is.EqualTo(0));
            SavedPlayer kept = rig.Server.PlayersToSave()[0];
            Assert.That(kept.Hand, Is.EqualTo((byte)2));
            Assert.That(kept.Carried.Length, Is.EqualTo(2), "the hands are kept by name with the body");

            GameClient again = rig.Join("A");
            rig.Pump(10);
            Assert.That(again.State, Is.EqualTo(ClientState.Connected));
            Assert.That(again.Carrying.Hand, Is.EqualTo((byte)2));
            Assert.That(again.Carrying.Things.Length, Is.EqualTo(2));
            Assert.That(again.Carrying.Things[1].Id, Is.EqualTo(stick));

            string before = rig.Server.Digest();
            WorldSave.Write(_dir, rig.Server.World, rig.Server.PlayersToSave(), Now);
            WorldSaveInfo info = WorldSave.Read(_dir);
            Assert.That(info.Digest, Is.EqualTo(before), "the digest file names the world with its hands");
            Assert.That(info.Players["A"].Carried.Length, Is.EqualTo(2));
            Assert.That(info.Entities, Is.Empty, "what is carried lies in no region");

            Rig loaded = Start(WorldSave.Restore(info, _ground, Fixture));
            loaded.Server.RememberPlayers(info.Players.Values);
            Assert.That(loaded.Server.Digest(), Is.EqualTo(before), "the same name after the load");
            GameClient back = loaded.Join("A");
            loaded.Pump(10);
            Assert.That(back.Carrying.Things.Length, Is.EqualTo(2));
            Assert.That(Do(loaded, back, PutDown(Ground(300, -298))), Is.EqualTo(VerbOutcome.Done), "the stick, in the hand");
            loaded.Pump(20);
            Assert.That(loaded.Server.World.Entities.TryGet(stick, out Entity put), Is.True, "put down under the id it had before the save");
            Assert.That(put.Definition, Is.SameAs(DefinitionCatalogue.Stick));
            Assert.That(back.Entities.Views.ContainsKey(stick), Is.True);
            Assert.That(back.Carrying.Things.Length, Is.EqualTo(1));
        }
    }
}
