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
    /// Work on the standing world and the ground, committed by the server (BF.3 promises 2 to 6): a trunk and a tuft are
    /// named by their cell, refused when gone or out of reach, and each work's end is a change of the cell told to the
    /// client — the bark taken with the strips beside the trunk, the tuft taken with its fibre or its bundle, the cell
    /// cleared with its bundles, the hole dug with its tuber, the cut kept as it goes and the tree felled into logs down
    /// the fall's line that the hands refuse to lift.
    /// </summary>
    public sealed class StandingWorkServerTests
    {
        private const double Dt = 0.05;
        private const int TickRate = 20;
        private static readonly Region FixtureRegion = new Region("fixture", "Fixture", Region.Bherwerre.CentreLatitudeDeg,
            Region.Bherwerre.CentreLongitudeDeg, 40.0, 237, 8.0);
        private static readonly Definition Core = DefinitionCatalogue.CobbleOf(StoneType.Silcrete);
        private static readonly Definition Flake = DefinitionCatalogue.FlakeOf(StoneType.Silcrete);

        private sealed class Rig
        {
            public GameServer Server;
            public GameClient Client;
            public WorldState World;
            public readonly List<IntentResultMessage> Answers = new List<IntentResultMessage>();
            public readonly List<WorkStateMessage> States = new List<WorkStateMessage>();
            public readonly List<CellChange> Changes = new List<CellChange>();
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
        /// Flat ground at 100 m on a 10 m grid; a 3 m swamp paperbark on cell (2, 3), a 20 m bangalay on (1, 1); sedge with lomandra
        /// on cell (2, 2) where the founder stands, grass with kangaroo grass on (3, 2), sand on (2, 0); soil 0.5 m, sand 0.05.
        /// </summary>
        private static WorldState World()
        {
            RegionRaster ground = TestRasters.FromLaw(5, 10.0, 40.0, "st_ground", (row, col) => 100f);
            RegionRaster stand = TestRasters.FromCodes(5, 10.0, 40.0, "st_stand", "stand",
                (row, col) => row == 2 && col == 3 ? StandCodes.Pack(PlantSpecies.SwampPaperbark, 3.0) : row == 1 && col == 1 ? StandCodes.Pack(PlantSpecies.Bangalay, 20.0) : 0u, null);
            RegionRaster cover = TestRasters.FromCodes(5, 10.0, 40.0, "st_cover", "cover",
                (row, col) => row == 2 && col == 2 ? GroundCovers.Pack(GroundCover.Sedge, 2)
                    : row == 3 && col == 2 ? GroundCovers.Pack(GroundCover.Grass, 3)
                    : row == 2 && col == 0 ? GroundCovers.Pack(GroundCover.Sand, 0)
                    : GroundCovers.Pack(GroundCover.ForestFloor, 1), null);
            RegionRaster understory = TestRasters.FromCodes(5, 10.0, 40.0, "st_understory", "understory",
                (row, col) => row == 2 && col == 2 ? PlantCode(PlantSpecies.Lomandra)
                    : row == 3 && col == 2 ? PlantCode(PlantSpecies.KangarooGrass) : 0u, null);
            RegionRaster soil = TestRasters.FromLaw(5, 10.0, 40.0, "st_soil", (row, col) => row == 2 && col == 0 ? 0.05f : 0.5f);
            return new WorldState(1, FixtureRegion, FixtureRegion.WakeClock(), new Heightfield(ground), 0, null, null, cover, stand, null, null, null, null, understory, soil);
        }

        /// <summary>The understorey layer's code for a plant: its place in the table plus one.</summary>
        private static uint PlantCode(PlantSpecies species)
        {
            for (int i = 0; i < PlantSpecies.All.Count; i++) if (ReferenceEquals(PlantSpecies.All[i], species)) return (uint)(i + 1);
            return 0u;
        }

        private static Rig Connect(WorldState world)
        {
            InMemoryTransport.CreatePair(out IServerTransport st, out IClientTransport ct);
            Rig rig = new Rig { World = world };
            rig.Server = new GameServer(new ServerConfig { TickRate = TickRate }, st, world);
            rig.Server.Listen(1);
            rig.Client = new GameClient(ct);
            rig.Client.IntentAnswered += a => rig.Answers.Add(a);
            rig.Client.WorkStateChanged += s => rig.States.Add(s);
            rig.Client.ChangesChanged += c => rig.Changes.Add(c);
            rig.Client.Connect("memory", 1, "William", "");
            rig.Pump(6);
            Assert.That(rig.Client.IsInteractive, Is.True);
            world.Clock.Scale = 0.0;
            return rig;
        }

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

        private static IntentMessage OnTrunk(WorkKind kind, int row, int col) => new IntentMessage { Verb = Verb.Work, Kind = kind, Target = IntentMessage.TargetTrunk, Row = row, Col = col };
        private static IntentMessage OnTuft(WorkKind kind, int row, int col, int index) => new IntentMessage { Verb = Verb.Work, Kind = kind, Target = IntentMessage.TargetTuft, Row = row, Col = col, Index = index };
        private static IntentMessage OnGround(WorkKind kind, int row, int col) => new IntentMessage { Verb = Verb.Work, Kind = kind, Target = IntentMessage.TargetGround, Row = row, Col = col };
        private static IntentMessage PickUp(ulong id) => new IntentMessage { Verb = Verb.PickUp, Target = IntentMessage.TargetEntity, EntityId = id };

        private static Entity Spawn(WorldState world, Definition definition, in ThingState state, double east, double north)
        {
            Entity e = world.SpawnItem(definition, east, north);
            ItemComponent item = default;
            item.Resting = true;
            item.State = state;
            e.SetItem(item, world.Tick);
            return e;
        }

        private static ThingState Edge(float edge, float massKg)
        {
            ThingState s = default;
            s.SetMass(massKg);
            s.SetEdge(edge);
            return s;
        }

        private static ThingState PointedStick()
        {
            ThingState s = default;
            s.SetLength(1.0f);
            s.SetDiameter(0.025f);
            s.SetMass(0.5f);
            s.SetMarks(ThingMarks.Pointed);
            return s;
        }

        private static void Finish(Rig rig, float seconds) => rig.Pump((int)Math.Ceiling(seconds / Dt) + 3);

        private static int Count(WorldState world, Func<Entity, bool> which)
        {
            int n = 0;
            foreach (Entity e in world.Entities.All) if (!e.Killed && which(e)) n++;
            return n;
        }

        [Test]
        public void StrippingATrunkTakesItsBarkInTheCellsChangeAndLeavesTheStripsBesideItAndTheClientIsTold()
        {
            WorldState world = World();
            Rig rig = Connect(world);
            Assert.That(StandingThings.TryFindTrunk(world, 1, 1, out StandingTrunk bangalay), Is.True);
            Stand(rig, bangalay.Foot.X - 2.0, bangalay.Foot.Z);
            rig.Pump(2);
            IntentResultMessage answer = Ask(rig, OnTrunk(WorkKind.StripTrunk, 1, 1));
            Assert.That(answer.Outcome, Is.EqualTo(VerbOutcome.Done), "a bangalay's bark strips");
            Assert.That(answer.Seconds, Is.EqualTo((float)Work.StripTrunkSeconds));
            Assert.That(rig.Session.Work.Target, Is.EqualTo(IntentMessage.TargetTrunk));
            Finish(rig, answer.Seconds);
            Assert.That(rig.Session.Work, Is.Null);
            Assert.That(world.Changes.TrunkOf(1, 1).Flags & TrunkChange.BarkTaken, Is.EqualTo(TrunkChange.BarkTaken), "the bark taken is the cell's change");
            int strips = Count(world, e => e.Definition.Substance == Substance.Bark);
            ThingState state = StandingThings.TrunkState(bangalay);
            Assert.That(strips, Is.EqualTo(Math.Max(1, (int)Math.Round(Math.PI * state.DiameterM / Work.TrunkStripPerGirthM))), "a strip for every tenth of a metre of girth");
            foreach (Entity e in world.Entities.All)
                if (e.Definition.Substance == Substance.Bark) Assert.That(Double3.Distance(e.Position, bangalay.Foot), Is.LessThan(1.5), "beside the trunk");
            Assert.That(rig.States[rig.States.Count - 1].Ended, Is.EqualTo(WorkStateMessage.Done));
            Assert.That(rig.States[rig.States.Count - 1].Note, Does.Contain("round the trunk"));
            Assert.That(rig.Changes.Count, Is.GreaterThanOrEqualTo(1), "the client was told the change");
            Assert.That(rig.Client.Changes.TrunkOf(1, 1).Flags & TrunkChange.BarkTaken, Is.EqualTo(TrunkChange.BarkTaken), "and holds it");
            Assert.That(Ask(rig, OnTrunk(WorkKind.StripTrunk, 1, 1)).Outcome, Is.EqualTo(VerbOutcome.WontWork), "not twice");
            Assert.That(Ask(rig, OnTrunk(WorkKind.StripTrunk, 2, 3)).Outcome, Is.EqualTo(VerbOutcome.OutOfReach), "the banksia is across the world");
            Assert.That(Ask(rig, OnTrunk(WorkKind.StripTrunk, 0, 0)).Outcome, Is.EqualTo(VerbOutcome.NotThere), "no trunk there");
        }

        [Test]
        public void CuttingFibreTakesTheTuftAndLeavesItsStripsAndPullingAGrassTuftLeavesABundle()
        {
            WorldState world = World();
            Rig rig = Connect(world);
            CellTufts sedge = Tufts.OnCell((byte)world.Cover.Code(2, 2), 10.0, 2, 2);
            Assert.That(sedge.Count, Is.GreaterThan(0));
            int clump = -1;
            for (int k = 0; k < sedge.Count; k++)
                if (StandingThings.TryFindTuft(world, 2, 2, k, out StandingTuft t) && t.Shape == TuftShape.Clump) { clump = k; break; }
            Assert.That(clump, Is.GreaterThanOrEqualTo(0), "a lomandra clump on the sedge cell");
            StandingThings.TryFindTuft(world, 2, 2, clump, out StandingTuft tuft);
            Stand(rig, tuft.At.X - 1.5, tuft.At.Z);
            Entity flake = Spawn(world, Flake, Edge(0.6f, 0.02f), tuft.At.X - 1.0, tuft.At.Z);
            rig.Pump(2);
            Assert.That(Ask(rig, OnTuft(WorkKind.CutFibre, 2, 2, clump)).Outcome, Is.EqualTo(VerbOutcome.NoTool), "nothing in hand");
            Assert.That(Ask(rig, PickUp(flake.Id.Value)).Outcome, Is.EqualTo(VerbOutcome.Done));
            IntentResultMessage answer = Ask(rig, OnTuft(WorkKind.CutFibre, 2, 2, clump));
            Assert.That(answer.Outcome, Is.EqualTo(VerbOutcome.Done));
            Assert.That(answer.Note, Does.Contain("lomandra"));
            Finish(rig, answer.Seconds);
            Assert.That(world.Changes.IsTuftTaken(2, 2, clump), Is.True, "the tuft is taken");
            Assert.That(rig.Client.Changes.IsTuftTaken(2, 2, clump), Is.True, "and the client knows");
            Assert.That(Count(world, e => e.Definition.Substance == Substance.Fibre), Is.EqualTo(Work.FibreStrips));
            Assert.That(Ask(rig, OnTuft(WorkKind.CutFibre, 2, 2, clump)).Outcome, Is.EqualTo(VerbOutcome.NotThere), "gone");

            CellTufts grass = Tufts.OnCell((byte)world.Cover.Code(3, 2), 10.0, 3, 2);
            Assert.That(StandingThings.TryFindTuft(world, 3, 2, 0, out StandingTuft tussock), Is.True);
            Stand(rig, tussock.At.X - 1.5, tussock.At.Z);
            rig.Pump(2);
            Assert.That(Ask(rig, new IntentMessage { Verb = Verb.Hold, Place = 0 }).Outcome, Is.EqualTo(VerbOutcome.Done), "the hand emptied");
            IntentResultMessage pull = Ask(rig, OnTuft(WorkKind.PullTuft, 3, 2, 0));
            Assert.That(pull.Outcome, Is.EqualTo(VerbOutcome.Done));
            Finish(rig, pull.Seconds);
            Assert.That(world.Changes.IsTuftTaken(3, 2, 0), Is.True);
            Assert.That(Count(world, e => e.Definition.Substance == Substance.Plant), Is.EqualTo(1), "a bundle");
            Assert.That(Ask(rig, OnTuft(WorkKind.PullTuft, 3, 2, grass.Count + 5)).Outcome, Is.EqualTo(VerbOutcome.NotThere), "no such tuft");
        }

        [Test]
        public void ClearingACellTakesEveryTuftMarksItClearedAndLeavesTheBundlesAndDiggingKeepsTheDepthAndTurnsUpATuber()
        {
            WorldState world = World();
            Rig rig = Connect(world);
            StandingThings.TryGround(world, 3, 2, out GroundSite grass);
            Stand(rig, grass.Centre.X - 2.0, grass.Centre.Z);
            rig.Pump(2);
            int left = grass.TuftsLeft;
            IntentResultMessage clear = Ask(rig, OnGround(WorkKind.ClearGround, 3, 2));
            Assert.That(clear.Outcome, Is.EqualTo(VerbOutcome.Done));
            Assert.That(clear.Seconds, Is.EqualTo((float)(Work.ClearSecondsPerTuft * left)));
            Finish(rig, clear.Seconds);
            Assert.That(world.Changes.GroundOf(3, 2).Flags & GroundChange.Cleared, Is.EqualTo(GroundChange.Cleared));
            for (int k = 0; k < left; k++) Assert.That(world.Changes.IsTuftTaken(3, 2, k), Is.True, "tuft " + k);
            Assert.That(Count(world, e => e.Definition.Substance == Substance.Plant), Is.EqualTo(left), "a bundle a tuft");
            Assert.That(rig.Client.Changes.GroundOf(3, 2).Flags & GroundChange.Cleared, Is.EqualTo(GroundChange.Cleared), "the client holds the clearing");
            Assert.That(Ask(rig, OnGround(WorkKind.ClearGround, 3, 2)).Outcome, Is.EqualTo(VerbOutcome.WontWork), "cleared already");
            Assert.That(Ask(rig, OnGround(WorkKind.StripTrunk, 3, 2)).Outcome, Is.EqualTo(VerbOutcome.NotNow), "a thing's work names no cell");
            Assert.That(Ask(rig, OnTrunk(WorkKind.Dig, 1, 1)).Outcome, Is.EqualTo(VerbOutcome.NotNow), "a ground's work names no trunk");

            // The dig: on the sedge cell, with a pointed stick, twice; the depth is kept and the lomandra's tuber comes up.
            StandingThings.TryGround(world, 2, 2, out GroundSite sedge);
            Stand(rig, sedge.Centre.X - 2.0, sedge.Centre.Z);
            Entity stick = Spawn(world, DefinitionCatalogue.StickOf(PlantSpecies.Bangalay), PointedStick(), sedge.Centre.X - 1.5, sedge.Centre.Z);
            rig.Pump(2);
            Assert.That(Ask(rig, OnGround(WorkKind.Dig, 2, 2)).Outcome, Is.EqualTo(VerbOutcome.NoTool), "nothing in hand");
            Assert.That(Ask(rig, PickUp(stick.Id.Value)).Outcome, Is.EqualTo(VerbOutcome.Done));
            IntentResultMessage dig = Ask(rig, OnGround(WorkKind.Dig, 2, 2));
            Assert.That(dig.Outcome, Is.EqualTo(VerbOutcome.Done));
            Assert.That(dig.Seconds, Is.EqualTo((float)Work.DigSecondsSoil));
            Finish(rig, dig.Seconds);
            Assert.That(world.Changes.GroundOf(2, 2).DugCm, Is.EqualTo((byte)Work.DigStepCm));
            Assert.That(Count(world, e => e.Definition.Substance == Substance.Food), Is.EqualTo(1), "a lomandra tuber");
            Assert.That(rig.Client.Changes.GroundOf(2, 2).DugCm, Is.EqualTo((byte)Work.DigStepCm));
            IntentResultMessage again = Ask(rig, OnGround(WorkKind.Dig, 2, 2));
            Assert.That(again.Outcome, Is.EqualTo(VerbOutcome.Done));
            Finish(rig, again.Seconds);
            Assert.That(world.Changes.GroundOf(2, 2).DugCm, Is.EqualTo((byte)(2 * Work.DigStepCm)), "the hole is deeper, not dug again from the top");
            StandingThings.TryGround(world, 2, 0, out GroundSite sand);
            Stand(rig, sand.Centre.X + 2.0, sand.Centre.Z);
            rig.Pump(2);
            Assert.That(Ask(rig, OnGround(WorkKind.Dig, 2, 0)).Outcome, Is.EqualTo(VerbOutcome.WontWork), "five centimetres of sand over the stone");
        }

        [Test]
        public void ACutIsKeptInTheCellAsItGoesAndTheFelledTreeLiesDownTheFallsLineInLogsTheHandsRefuse()
        {
            WorldState world = World();
            Rig rig = Connect(world);
            Assert.That(StandingThings.TryFindTrunk(world, 2, 3, out StandingTrunk banksia), Is.True);
            // The founder stands west of the tree, so it falls east; a heavy keen core, to be done in minutes.
            Stand(rig, banksia.Foot.X - 2.0, banksia.Foot.Z);
            Entity core = Spawn(world, Core, Edge(0.8f, 1.5f), banksia.Foot.X - 1.5, banksia.Foot.Z);
            rig.Pump(2);
            Assert.That(Ask(rig, PickUp(core.Id.Value)).Outcome, Is.EqualTo(VerbOutcome.Done));
            IntentResultMessage answer = Ask(rig, OnTrunk(WorkKind.CutTrunk, 2, 3));
            Assert.That(answer.Outcome, Is.EqualTo(VerbOutcome.Done));
            float whole = answer.Seconds;
            Assert.That(whole, Is.InRange(10f, 900f), "a 3 m paperbark with a heavy keen core: well under a quarter of an hour");
            rig.Pump(2 * TickRate + 2);
            byte kept = world.Changes.TrunkOf(2, 3).Cut;
            Assert.That(kept, Is.GreaterThan((byte)0), "two seconds in, the cut is in the cell");
            Assert.That(kept, Is.LessThan((byte)255));
            Assert.That(rig.Client.Changes.TrunkOf(2, 3).Cut, Is.EqualTo(kept), "and the client holds it");
            Assert.That(Ask(rig, new IntentMessage { Verb = Verb.StopWork }).Outcome, Is.EqualTo(VerbOutcome.Done), "let go");
            IntentResultMessage resumed = Ask(rig, OnTrunk(WorkKind.CutTrunk, 2, 3));
            Assert.That(resumed.Outcome, Is.EqualTo(VerbOutcome.Done));
            Assert.That(resumed.Seconds, Is.LessThan(whole), "the rest of the cut, not the whole again");
            Finish(rig, resumed.Seconds);
            Assert.That(rig.Session.Work, Is.Null);
            Assert.That(world.Changes.TrunkOf(2, 3).Flags & TrunkChange.Felled, Is.EqualTo(TrunkChange.Felled), "felled");
            Assert.That(world.Changes.TrunkOf(2, 3).Cut, Is.EqualTo((byte)255));
            Assert.That(StandingThings.TryFindTrunk(world, 2, 3, out _), Is.False, "the trunk is gone");
            Assert.That(rig.Client.Changes.TrunkOf(2, 3).Flags & TrunkChange.Felled, Is.EqualTo(TrunkChange.Felled), "the client knows it fell");
            List<Entity> logs = new List<Entity>();
            List<Entity> limbs = new List<Entity>();
            foreach (Entity e in world.Entities.All)
            {
                if (DefinitionCatalogue.IsLog(e.Definition)) logs.Add(e);
                else if (ReferenceEquals(e.Definition, DefinitionCatalogue.StickOf(PlantSpecies.SwampPaperbark))) limbs.Add(e);
            }
            Assert.That(logs.Count, Is.GreaterThanOrEqualTo(1));
            Assert.That(limbs.Count, Is.GreaterThanOrEqualTo(1));
            foreach (Entity log in logs)
            {
                Assert.That(log.Position.X, Is.GreaterThan(banksia.Foot.X + 0.5), "down the line, east of the stump");
                Assert.That(Math.Abs(log.Position.Z - banksia.Foot.Z), Is.LessThan(0.01), "on the line");
            }
            foreach (Entity limb in limbs) Assert.That(Math.Abs(limb.Position.Z - banksia.Foot.Z), Is.EqualTo(0.8).Within(0.01), "the limbs either side");
            Assert.That(rig.States[rig.States.Count - 1].Note, Does.Contain("came down"));
            // A log heavier than the hands lift is answered over the wire; a small paperbark's log and a limb are lifted.
            ThingState heavyLog = default;
            heavyLog.SetMass(180f);
            heavyLog.SetLength(2f);
            heavyLog.SetDiameter(0.4f);
            Entity heavy = Spawn(world, DefinitionCatalogue.LogOf(PlantSpecies.Blackbutt), heavyLog, logs[0].Position.X, logs[0].Position.Z + 1.0);
            Stand(rig, logs[0].Position.X - 1.0, logs[0].Position.Z);
            rig.Pump(2);
            Assert.That(Ask(rig, PickUp(heavy.Id.Value)).Outcome, Is.EqualTo(VerbOutcome.TooHeavy), "180 kg of blackbutt stays where it lies");
            Assert.That(logs[0].Item.State.MassKg, Is.LessThan((float)Hands.MaxLiftKg), "a 3 m paperbark's one log is small enough to lift");
            Assert.That(Ask(rig, PickUp(logs[0].Id.Value)).Outcome, Is.EqualTo(VerbOutcome.Done));
            Stand(rig, limbs[0].Position.X - 1.0, limbs[0].Position.Z);
            rig.Pump(2);
            Assert.That(Ask(rig, PickUp(limbs[0].Id.Value)).Outcome, Is.EqualTo(VerbOutcome.Done), "a limb is lifted");
            Assert.That(Ask(rig, OnTrunk(WorkKind.CutTrunk, 2, 3)).Outcome, Is.EqualTo(VerbOutcome.NotThere), "nothing stands to cut");
        }
    }
}
