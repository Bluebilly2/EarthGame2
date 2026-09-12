using System;
using System.Collections.Generic;
using EarthGame.ClientCore;
using EarthGame.Engine;
using EarthGame.Server;
using EarthGame.Transport;
using NUnit.Framework;

namespace EarthGame.Tests.ClientCore
{
    /// <summary>
    /// The trunks a client finds round a body (M1.6b promises 3 and 6): the trees the drawing draws, in the same places, as
    /// thick as the mesh's rings draw them, read out of tiles a server streamed.
    /// </summary>
    public sealed class TrunksNearTests
    {
        private static readonly Region Fixture = new Region("fixture", "Fixture", Region.Bherwerre.CentreLatitudeDeg,
            Region.Bherwerre.CentreLongitudeDeg, TestRasters.MadeExtentM, 237, 8.0);

        /// <summary>The cell whose centre is (300, -300) on the made coast, and two of its neighbours.</summary>
        private const int Row = 110, Col = 110;
        private const double East = 300.0, North = -300.0;
        // Heights a stand code carries whole: its five low bits count steps of StandCodes.HeightStepM.
        private const double BlackbuttM = 30.0, CoastBanksiaM = 8.75, OldManBanksiaM = 6.25;

        private static WorldState World()
        {
            RegionRaster stand = TestRasters.FromCodes(TestRasters.MadeSide, TestRasters.MadeCellM, TestRasters.MadeExtentM, "made_stand", "stand",
                (row, col) => row == Row && col == Col ? StandCodes.Pack(PlantSpecies.Blackbutt, BlackbuttM)
                    : row == Row - 1 && col == Col + 1 ? StandCodes.Pack(PlantSpecies.CoastBanksia, CoastBanksiaM)
                    : row == Row + 1 && col == Col - 1 ? StandCodes.Pack(PlantSpecies.OldManBanksia, OldManBanksiaM)
                    : 0u, null);
            return new WorldState(1347UL, Fixture, Fixture.WakeClock(), new Heightfield(TestRasters.MadeCoast()), 0, null, null, null, stand);
        }

        /// <summary>A client joined to a server of that world, standing at the middle tree, with the tiles round it held.</summary>
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
            Assert.That(client.Tiles.Holding(TileLayer.Stand, client.Grid.ForPosition(East, North)), Is.Not.Null, "the stand tile arrived");
            return client;
        }

        [Test]
        public void TheTrunksNearABodyAreTheTreesTheDrawingDraws()
        {
            GameClient client = Joined(World());
            TileId id = client.Grid.ForPosition(East, North);
            PreparedStand drawn = StandPreparation.Prepare(client.Tiles.Holding(TileLayer.Stand, id), null,
                                                           client.Tiles.Holding(TileLayer.Ground, id), client.Grid);
            List<TrunkNearby> near = new List<TrunkNearby>();
            TrunksNear.Find(East, North, 25.0, 1.0, client.Tiles, client.Grid, near);

            Assert.That(near.Count, Is.EqualTo(3), "the tree of the cell and the two beside it");
            Assert.That(near.Count, Is.EqualTo(Within(drawn, 25.0)), "every tree the drawing draws within the reach, and no other");
            foreach (TrunkNearby trunk in near)
            {
                // The drawing keeps a tree's place in floats, so the two agree to a millimetre, not to the last bit.
                StandTree tree = Array.Find(drawn.Trees, t => Math.Abs(t.East - trunk.East) < 1e-3 && Math.Abs(t.North - trunk.North) < 1e-3);
                Assert.That(tree.HeightM, Is.GreaterThan(0f), "a trunk at east " + trunk.East + " north " + trunk.North + " that the drawing does not draw");
                Assert.That(trunk.Up, Is.EqualTo(tree.Up).Within(1e-3), "on the ground the drawing stands it on");
                Assert.That(trunk.HeightM, Is.EqualTo(tree.HeightM).Within(1e-3));
                TreeForm form = StandForms.ForTall(tree.Tall);
                Assert.That(trunk.RadiusM, Is.EqualTo(StandForms.TrunkRadiusAt(form, tree.HeightM, 1.0)).Within(1e-9), "as thick as it is drawn");
                Assert.That(trunk.TrunkM, Is.EqualTo(form.TrunkLength * tree.HeightM).Within(1e-3));
            }
            Assert.That(near.Exists(t => Math.Abs(t.HeightM - BlackbuttM) < 1e-9), Is.True, "the blackbutt");
            Assert.That(near.Exists(t => Math.Abs(t.HeightM - CoastBanksiaM) < 1e-9), Is.True, "the coast banksia");
            Assert.That(near.Exists(t => Math.Abs(t.HeightM - OldManBanksiaM) < 1e-9), Is.True, "the old-man banksia");

            // A trunk stands anywhere in its cell, so what a short reach finds is the drawing's own answer, not a count
            // this test can state: the cells are 10 m and a tree may stand 5 m from its cell's middle.
            near.Clear();
            TrunksNear.Find(East, North, 5.0, 1.0, client.Tiles, client.Grid, near);
            Assert.That(near.Count, Is.EqualTo(Within(drawn, 5.0)).And.LessThan(3), "a shorter reach finds what the drawing puts inside it");

            near.Clear();
            TrunksNear.Find(East + 60.0, North, 12.0, 1.0, client.Tiles, client.Grid, near);
            Assert.That(near, Is.Empty, "where no tree stands");

            near.Clear();
            TrunksNear.Find(East, North + 700.0, 12.0, 1.0, client.Tiles, client.Grid, near);
            Assert.That(near, Is.Empty, "and where the client holds no tile");
        }

        /// <summary>How many of the drawn trees stand within a distance of the point the body is at.</summary>
        private static int Within(PreparedStand drawn, double radiusM)
        {
            int count = 0;
            foreach (StandTree t in drawn.Trees)
                if ((t.East - East) * (t.East - East) + (t.North - North) * (t.North - North) <= radiusM * radiusM) count++;
            return count;
        }

        [Test]
        public void ATrunkTapersAsTheMeshDrawsIt()
        {
            TreeForm form = StandForms.Blackbutt;
            double height = 30.0, butt = form.TrunkRadius * height, trunk = form.TrunkLength * height;
            Assert.That(StandForms.TrunkRadiusAt(form, height, 0.0), Is.EqualTo(butt).Within(1e-9), "the butt");
            Assert.That(StandForms.TrunkRadiusAt(form, height, trunk), Is.EqualTo(butt * StandForms.TrunkTipRadiusShare).Within(1e-9), "where the crown begins");
            Assert.That(StandForms.TrunkRadiusAt(form, height, trunk * 2.0), Is.EqualTo(butt * StandForms.TrunkTipRadiusShare).Within(1e-9), "and no narrower above it");
            Assert.That(StandForms.TrunkRadiusAt(form, height, trunk * 0.5),
                Is.EqualTo(0.5 * (butt + butt * StandForms.TrunkTipRadiusShare)).Within(1e-9), "straight between the two");
            Assert.That(StandForms.TrunkRadiusAt(form, 2.0 * height, 1.0), Is.GreaterThan(StandForms.TrunkRadiusAt(form, height, 1.0)), "a taller tree is thicker");
            Assert.That(StandForms.TrunkRadiusAt(null, height, 1.0), Is.Zero);
            Assert.That(StandForms.TrunkRadiusAt(form, 0.0, 1.0), Is.Zero);
        }
    }
}
