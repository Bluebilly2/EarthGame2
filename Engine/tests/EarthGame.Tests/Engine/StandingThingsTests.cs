using System;
using System.Collections.Generic;
using EarthGame.ClientCore;
using EarthGame.Engine;
using EarthGame.Server;
using EarthGame.Transport;
using NUnit.Framework;

namespace EarthGame.Tests.Engine
{
    /// <summary>
    /// Standing things are targets (BF.3 promise 2): a trunk and a tuft are found from the world's own rasters and its
    /// changes, refused when felled, taken, cleared or inside a trunk, named by their plant, given a state of what they
    /// have of their own; and every tuft the client draws from its tiles is the tuft the server finds, to the centimetre.
    /// </summary>
    public sealed class StandingThingsTests
    {
        private static readonly Region Fixture = new Region("fixture", "Fixture", Region.Bherwerre.CentreLatitudeDeg,
            Region.Bherwerre.CentreLongitudeDeg, TestRasters.MadeExtentM, 237, 8.0);

        private const double East = 300.0, North = -300.0;
        private const int TreeRow = 110, TreeCol = 108;
        private const double TreeHeightM = 12.5;

        /// <summary>Heath west of the founder, sedge on the founder's own column, grass east, sand beyond; lomandra named on the sedge, bracken on the heath; soil 0.6 m deep.</summary>
        private static WorldState World()
        {
            RegionRaster cover = TestRasters.FromCodes(TestRasters.MadeSide, TestRasters.MadeCellM, TestRasters.MadeExtentM, "made_cover", "cover",
                (row, col) => col <= 110 ? GroundCovers.Pack(GroundCover.Heath, 1)
                    : col <= 113 ? GroundCovers.Pack(GroundCover.Sedge, 2)
                    : col <= 116 ? GroundCovers.Pack(GroundCover.Grass, 3)
                    : GroundCovers.Pack(GroundCover.Sand, 0), null);
            RegionRaster stand = TestRasters.FromCodes(TestRasters.MadeSide, TestRasters.MadeCellM, TestRasters.MadeExtentM, "made_stand", "stand",
                (row, col) => row == TreeRow && col == TreeCol ? StandCodes.Pack(PlantSpecies.OldManBanksia, TreeHeightM) : 0u, null);
            RegionRaster understory = TestRasters.FromCodes(TestRasters.MadeSide, TestRasters.MadeCellM, TestRasters.MadeExtentM, "made_understory", "understory",
                (row, col) => col <= 110 ? PlantCode(PlantSpecies.Bracken)
                    : col <= 113 ? PlantCode(PlantSpecies.Lomandra)
                    : col <= 116 ? PlantCode(PlantSpecies.KangarooGrass) : 0u, null);
            RegionRaster soil = TestRasters.FromLaw(TestRasters.MadeSide, TestRasters.MadeCellM, TestRasters.MadeExtentM, "made_soil", (row, col) => col >= 117 ? 0.05f : 0.6f);
            return new WorldState(1347UL, Fixture, Fixture.WakeClock(), new Heightfield(TestRasters.MadeCoast()), 0, null, null, cover, stand, null, null, null, null, understory, soil);
        }

        /// <summary>The understorey layer's code for a plant: its place in the table plus one.</summary>
        private static uint PlantCode(PlantSpecies species)
        {
            for (int i = 0; i < PlantSpecies.All.Count; i++) if (ReferenceEquals(PlantSpecies.All[i], species)) return (uint)(i + 1);
            return 0u;
        }

        private static void CellOf(WorldState world, double east, double north, out int row, out int col) =>
            TileCodec.CellOf(world.Cover.ExtentM, world.Cover.CellM, east, north, out row, out col);

        [Test]
        public void ATrunkIsFoundWhereTheStandPutsItAndIsGoneOnceFelled()
        {
            WorldState world = World();
            Assert.That(StandingThings.TryFindTrunk(world, TreeRow, TreeCol, out StandingTrunk trunk), Is.True);
            Assert.That(trunk.Species, Is.SameAs(PlantSpecies.OldManBanksia));
            Assert.That(trunk.HeightM, Is.EqualTo(TreeHeightM).Within(1e-9));
            StandLayout.CellCentre(TreeRow, TreeCol, world.Stand.CellM, world.Stand.ExtentM, out double ce, out double cn);
            StandLayout.Place(TreeRow, TreeCol, StandLayout.Kind.Trunk, 0, (int)Math.Round(world.Stand.CellM * 100.0), out int eCm, out int nCm, out _);
            Assert.That(trunk.Foot.X, Is.EqualTo(ce + eCm / 100.0).Within(1e-9), "the layout's own centimetre");
            Assert.That(trunk.Foot.Z, Is.EqualTo(cn + nCm / 100.0).Within(1e-9));
            Assert.That(trunk.Foot.Y, Is.EqualTo(world.GroundAt(trunk.Foot.X, trunk.Foot.Z)).Within(1e-9));
            Assert.That(trunk.Quarter, Is.EqualTo(1), "the heath's quarter");
            Assert.That(StandingThings.Describe(trunk), Is.EqualTo("an old-man banksia, 13 m"));
            Assert.That(StandingThings.TryFindTrunk(world, TreeRow, TreeCol + 1, out _), Is.False, "no trunk on the next cell");

            ThingState state = StandingThings.TrunkState(trunk);
            Assert.That(state.LengthM, Is.EqualTo((float)TreeHeightM));
            double breast = 2.0 * TreeGeometries.TrunkRadiusAt(TreeGeometries.OldManBanksia, TreeHeightM, TreeGeometries.BreastHeightM);
            Assert.That(state.DiameterM, Is.EqualTo((float)breast).Within(1e-6f), "its girth at breast height, by the taper the client draws");
            Assert.That(state.Moisture, Is.EqualTo(LyingProperties.MoistureByQuarter[1]));
            Assert.That(state.Condition01, Is.EqualTo(1f), "uncut");
            Assert.That(state.Has(ThingFields.Marks), Is.False, "its bark on");

            world.Changes.MarkTrunk(TreeRow, TreeCol, TrunkChange.BarkTaken);
            world.Changes.SetTrunkCut(TreeRow, TreeCol, 128);
            Assert.That(StandingThings.TryFindTrunk(world, TreeRow, TreeCol, out trunk), Is.True);
            ThingState stripped = StandingThings.TrunkState(trunk);
            Assert.That((stripped.Marks & ThingMarks.Stripped) != 0, Is.True);
            Assert.That(stripped.Condition01, Is.EqualTo(1f - 128f / 255f).Within(1e-6f));
            Assert.That(StandingThings.Describe(trunk), Does.Contain("stripped").And.Contain("50 % through"));
            world.Changes.MarkTrunk(TreeRow, TreeCol, TrunkChange.Felled);
            Assert.That(StandingThings.TryFindTrunk(world, TreeRow, TreeCol, out _), Is.False, "felled, it is gone");
        }

        [Test]
        public void ATuftIsFoundByTheRuleNamedByThePlantAndRefusedWhenTakenClearedOrInsideTheTrunk()
        {
            WorldState world = World();
            CellOf(world, East + 8.0, North, out int row, out int col);
            Assert.That(GroundCovers.CoverOf((byte)world.Cover.Code(row, col)), Is.EqualTo(GroundCover.Sedge));
            CellTufts grows = Tufts.OnCell((byte)world.Cover.Code(row, col), world.Cover.CellM, row, col);
            Assert.That(grows.Count, Is.GreaterThan(0), "sedge grows clumps");
            int lomandra = 0, grass = 0;
            int most = Math.Min(grows.Count, WorldChanges.MostTufts);
            if (grows.Count > most) Assert.That(StandingThings.TryFindTuft(world, row, col, most, out _), Is.False, "past the sixteen the change bits hold, none is a target (the fixture's cells are wider than the world's)");
            for (int k = 0; k < most; k++)
            {
                Assert.That(StandingThings.TryFindTuft(world, row, col, k, out StandingTuft tuft), Is.True, "tuft " + k);
                Assert.That(tuft.HeightM, Is.GreaterThan(0.0));
                Assert.That(tuft.Quarter, Is.EqualTo(2));
                if (tuft.Shape == TuftShape.Clump) { Assert.That(tuft.Species, Is.SameAs(PlantSpecies.Lomandra), "the understorey names the clump"); lomandra++; }
                else { Assert.That(tuft.Shape, Is.EqualTo(TuftShape.Tussock)); Assert.That(tuft.Species, Is.SameAs(PlantSpecies.KangarooGrass), "a tussock among the sedge stands for the grass"); grass++; }
                StandLayout.CellCentre(row, col, world.Cover.CellM, world.Cover.ExtentM, out double ce, out double cn);
                StandLayout.Place(row, col, StandLayout.Kind.Tuft, k, (int)Math.Round(world.Cover.CellM * 100.0), out int eCm, out int nCm, out _);
                Assert.That(tuft.At.X, Is.EqualTo(ce + eCm / 100.0).Within(1e-9));
                Assert.That(tuft.At.Z, Is.EqualTo(cn + nCm / 100.0).Within(1e-9));
            }
            Assert.That(lomandra, Is.GreaterThan(0));
            Assert.That(StandingThings.TryFindTuft(world, row, col, grows.Count + grows.Herbs, out _), Is.False, "the herbs past the count are no targets");
            Assert.That(StandingThings.TryFindTuft(world, row, col, -1, out _), Is.False);
            Assert.That(StandingThings.TryFindTuft(world, row, col, 0, out StandingTuft first), Is.True);
            Assert.That(StandingThings.Describe(first), Is.EqualTo(first.Shape == TuftShape.Clump ? "a lomandra tuft" : "a kangaroo grass tuft"));
            ThingState state = StandingThings.TuftState(first);
            Assert.That(state.LengthM, Is.EqualTo((float)first.HeightM));
            Assert.That(state.Moisture, Is.EqualTo(LyingProperties.MoistureByQuarter[2]));

            Assert.That(world.Changes.TakeTuft(row, col, 0), Is.True);
            Assert.That(StandingThings.TryFindTuft(world, row, col, 0, out _), Is.False, "taken, it is gone");
            Assert.That(StandingThings.TryFindTuft(world, row, col, 1, out _), Is.EqualTo(most > 1), "its neighbour stands");
            world.Changes.Clear(row, col);
            Assert.That(StandingThings.TryFindTuft(world, row, col, 1, out _), Is.False, "the cell cleared, none stands");

            // The tree's cell: whatever tuft would stand inside the trunk's bark is not there, and the rest are.
            int inside = 0, outside = 0;
            CellTufts treeCell = Tufts.OnCell((byte)world.Cover.Code(TreeRow, TreeCol), world.Cover.CellM, TreeRow, TreeCol);
            treeCell.Count = Math.Min(treeCell.Count, WorldChanges.MostTufts);
            StandLayout.CellCentre(TreeRow, TreeCol, world.Cover.CellM, world.Cover.ExtentM, out double tce, out double tcn);
            StandLayout.Place(TreeRow, TreeCol, StandLayout.Kind.Trunk, 0, (int)Math.Round(world.Cover.CellM * 100.0), out int te, out int tn, out _);
            double radius = TreeGeometries.TrunkRadiusAt(TreeGeometries.OldManBanksia, TreeHeightM, 0.0);
            for (int k = 0; k < treeCell.Count; k++)
            {
                StandLayout.Place(TreeRow, TreeCol, StandLayout.Kind.Tuft, k, (int)Math.Round(world.Cover.CellM * 100.0), out int eCm, out int nCm, out _);
                double dx = (eCm - te) / 100.0, dz = (nCm - tn) / 100.0;
                bool found = StandingThings.TryFindTuft(world, TreeRow, TreeCol, k, out _);
                Assert.That(found, Is.EqualTo(dx * dx + dz * dz >= radius * radius), "tuft " + k + " against the bark");
                if (found) outside++; else inside++;
            }
            Assert.That(outside + inside, Is.EqualTo(treeCell.Count));
        }

        [Test]
        public void TheGroundOfACellReadsItsTuftsSoilWaterAndPlant()
        {
            WorldState world = World();
            CellOf(world, East + 8.0, North, out int row, out int col);
            Assert.That(StandingThings.TryGround(world, row, col, out GroundSite site), Is.True);
            Assert.That(site.Cover, Is.EqualTo(GroundCover.Sedge));
            Assert.That(site.Quarter, Is.EqualTo(2));
            Assert.That(site.SoilDepthM, Is.EqualTo(0.6).Within(1e-6));
            Assert.That(site.WaterDepthM, Is.EqualTo(0.0));
            Assert.That(site.Understory, Is.SameAs(PlantSpecies.Lomandra));
            Assert.That(site.TuftsLeft, Is.EqualTo(Math.Min(site.Tufts.Count, WorldChanges.MostTufts)));
            Assert.That(site.Stands(0), Is.True);
            world.Changes.TakeTuft(row, col, 0);
            Assert.That(StandingThings.TryGround(world, row, col, out site), Is.True);
            Assert.That(site.Stands(0), Is.False);
            Assert.That(site.TuftsLeft, Is.EqualTo(Math.Min(site.Tufts.Count, WorldChanges.MostTufts) - 1));
            world.Changes.Clear(row, col);
            world.Changes.Dig(row, col, 20);
            Assert.That(StandingThings.TryGround(world, row, col, out site), Is.True);
            Assert.That(site.Cleared, Is.True);
            Assert.That(site.TuftsLeft, Is.EqualTo(0));
            Assert.That(site.DugCm, Is.EqualTo((byte)20));
            CellOf(world, East + 80.0, North, out int sandRow, out int sandCol);
            Assert.That(StandingThings.TryGround(world, sandRow, sandCol, out GroundSite sand), Is.True);
            Assert.That(sand.Cover, Is.EqualTo(GroundCover.Sand));
            Assert.That(sand.Tufts.Count, Is.EqualTo(0), "sand grows nothing");
            Assert.That(sand.SoilDepthM, Is.EqualTo(0.05).Within(1e-6));
            Assert.That(sand.Understory, Is.Null);
            Assert.That(StandingThings.TryGround(world, -1, 0, out _), Is.False);
        }

        /// <summary>A client joined to a server of that world, standing where the bands meet, with the tiles round it held.</summary>
        private static GameClient Joined(WorldState world)
        {
            InMemoryTransport.CreatePair(out IServerTransport st, out IClientTransport _);
            GameServer server = new GameServer(new ServerConfig { InterestRadiusM = 100.0, InterestMarginM = 50.0 }, st, world);
            server.Listen(1);
            SavedPlayer player = default;
            player.Name = "William";
            player.Body = MoverState.AtRest(East, world.Terrain.HeightAt(East, North), North);
            player.Body.Grounded = true;
            server.RememberPlayers(new[] { player });
            GameClient client = new GameClient(InMemoryTransport.CreateClient(st));
            client.Connect("memory", 1, "William", "");
            long ms = 0;
            for (int i = 0; i < 40; i++)
            {
                client.Update(ms);
                server.Update(0.05);
                client.Update(ms);
                ms += 50;
            }
            Assert.That(client.State, Is.EqualTo(ClientState.Connected));
            Assert.That(client.Tiles.Holding(TileLayer.GroundCover, client.Grid.ForPosition(East, North)), Is.Not.Null, "the cover tile arrived");
            return client;
        }

        [Test]
        public void EveryTuftTheClientDrawsIsTheTuftTheServerFindsToTheCentimetre()
        {
            WorldState world = World();
            GameClient client = Joined(world);
            List<UnderstoreyTuft> drawn = new List<UnderstoreyTuft>();
            Understorey.Find(East, North, 12.0, client.Tiles, client.Grid, drawn);
            int tufts = 0, herbs = 0;
            foreach (UnderstoreyTuft t in drawn)
            {
                CellTufts cell = Tufts.OnCell((byte)world.Cover.Code(t.Row, t.Col), world.Cover.CellM, t.Row, t.Col);
                if (t.Index >= cell.Count)
                {
                    herbs++;
                    Assert.That(t.Shape, Is.EqualTo(TuftShape.Herb));
                    Assert.That(StandingThings.TryFindTuft(world, t.Row, t.Col, t.Index, out _), Is.False, "a herb is no target");
                    continue;
                }
                if (t.Index >= WorldChanges.MostTufts)
                {
                    Assert.That(StandingThings.TryFindTuft(world, t.Row, t.Col, t.Index, out _), Is.False, "past the sixteen the change bits hold, none is a target");
                    continue;
                }
                tufts++;
                Assert.That(StandingThings.TryFindTuft(world, t.Row, t.Col, t.Index, out StandingTuft found), Is.True, "tuft " + t.Index + " of (" + t.Row + ", " + t.Col + ")");
                Assert.That(found.At.X, Is.EqualTo(t.East).Within(0.005), "the same centimetre east");
                Assert.That(found.At.Z, Is.EqualTo(t.North).Within(0.005), "and north");
                Assert.That(found.Shape, Is.EqualTo(t.Shape));
                Assert.That(found.HeightM, Is.EqualTo(t.HeightM).Within(1e-4), "the same height");
            }
            Assert.That(tufts, Is.GreaterThan(20), "a reach of tufts round the founder");
            Assert.That(herbs, Is.GreaterThan(0));
            // And nothing stands on the server that the client did not draw: every index under the count within the reach was drawn.
            HashSet<(int, int, int)> seen = new HashSet<(int, int, int)>();
            foreach (UnderstoreyTuft t in drawn) seen.Add((t.Row, t.Col, t.Index));
            CellOf(world, East, North, out int row, out int col);
            CellTufts here = Tufts.OnCell((byte)world.Cover.Code(row, col), world.Cover.CellM, row, col);
            for (int k = 0; k < Math.Min(here.Count, WorldChanges.MostTufts); k++)
                if (StandingThings.TryFindTuft(world, row, col, k, out StandingTuft t) && Double3.Distance(new Double3(East, t.At.Y, North), t.At) <= 12.0)
                    Assert.That(seen.Contains((row, col, k)), Is.True, "the server's tuft " + k + " on the founder's cell was drawn");
        }
    }
}
